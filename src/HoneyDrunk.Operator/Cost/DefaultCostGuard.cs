using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.State;
using HoneyDrunk.Operator.Telemetry;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace HoneyDrunk.Operator.Cost;

/// <summary>
/// The default <see cref="ICostGuard"/>. Spend is kept per (scope, window) in the <see cref="IOperatorStore"/>, so it
/// survives a restart when the store is durable. Limits are read from Azure App Configuration via Vault's
/// <see cref="IConfigProvider"/> per ADR-0018 D6 (invariant 117): <c>Budget:{scope}:{window}</c> for one window,
/// falling back to <c>Budget:{scope}</c> for every window. A limit of zero means unlimited.
/// </summary>
/// <remarks>
/// When recorded spend for an agent reaches a limit above zero, the agent is paused (<c>agent:{agentId}</c>, by
/// <c>operator:budget</c>) unless it is already paused. Only a person resumes it; a new window does not. The limit is
/// read first, then the charge, the pause and their audit entries are saved in one store write: a charge reaching the
/// limit is never stored without its stop, and a failed write stores nothing, so a retry charges once. The budget
/// check and the record stay separate calls, so the stop is enforced on recorded spend; a caller that skips the check
/// can overshoot by one charge before the pause takes effect.
/// </remarks>
/// <param name="config">The Vault configuration provider.</param>
/// <param name="options">Startup fallback options.</param>
/// <param name="telemetry">The Operator telemetry helper.</param>
/// <param name="audit">Writes the audit entry for a denied budget check, which changes nothing.</param>
/// <param name="store">Where spend is kept.</param>
/// <param name="outbox">Delivers the audit entries saved with a charge.</param>
/// <param name="timeProvider">Stamps the budget stop.</param>
public sealed class DefaultCostGuard(
    IConfigProvider config,
    IOptions<OperatorOptions> options,
    OperatorTelemetry telemetry,
    OperatorAuditWriter audit,
    IOperatorStore store,
    OperatorAuditOutbox outbox,
    TimeProvider timeProvider) : ICostGuard
{
    /// <summary>Who a budget stop is recorded as.</summary>
    public const string BudgetPausedBy = "operator:budget";

    /// <summary>The reason a budget stop is recorded with.</summary>
    public const string BudgetPauseReason = "monthly budget reached";

    /// <summary>The longest agent id whose pause name, <c>agent:{agentId}</c>, fits the 200-character limit.</summary>
    public const int MaxAgentIdLength = 194;

    private readonly IConfigProvider config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly OperatorOptions options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly OperatorAuditWriter audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly IOperatorStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly OperatorAuditOutbox outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async Task<CostCheckResult> CheckBudgetAsync(string scope, string window, decimal amount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(window);

        // A negative projected charge would reduce accumulated spend and could be used to slip under a budget.
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        using var activity = this.telemetry.Start("cost-guard", "check-budget");
        var limit = await this.ReadLimitAsync(scope, window, cancellationToken).ConfigureAwait(false);
        var current = await this.store.GetSpendAsync(scope, window, cancellationToken).ConfigureAwait(false);
        var projected = current + amount;
        if (limit > 0m && projected > limit)
        {
            await this.audit.WriteAsync(
                "operator.cost-guard.denied",
                scope,
                AuditOutcome.Denied,
                new AuditTarget("cost-scope", scope),
                $"Projected spend {projected} exceeds limit {limit} for window {window}.",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return new CostCheckResult(false, Math.Max(0m, limit - current), limit, $"Budget exceeded for scope '{scope}' in window '{window}'.");
        }

        return new CostCheckResult(true, limit > 0m ? limit - projected : decimal.MaxValue, limit, null);
    }

    /// <inheritdoc />
    public async Task RecordAsync(CostEvent costEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(costEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(costEvent.AgentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(costEvent.Window);
        if (costEvent.AgentId.Length > MaxAgentIdLength)
        {
            // Refused before any spend is stored, so the stop can always write its pause.
            throw new ArgumentException($"Agent id must be at most {MaxAgentIdLength} characters.", nameof(costEvent));
        }

        // Recorded spend is non-negative; a negative amount would let a scope drift back under budget.
        ArgumentOutOfRangeException.ThrowIfNegative(costEvent.Amount);
        using var activity = this.telemetry.Start("cost-guard", "record");

        // The limit is read first: if configuration cannot be read, nothing is stored.
        var limit = await this.ReadLimitAsync(costEvent.AgentId, costEvent.Window, cancellationToken).ConfigureAwait(false);
        var now = this.timeProvider.GetUtcNow();
        var target = new AuditTarget("cost-scope", costEvent.AgentId);
        var recorded = PendingAudit.Create(
            "operator.cost-guard.recorded",
            costEvent.AgentId,
            AuditOutcome.Succeeded,
            target,
            $"Recorded {costEvent.Amount} {costEvent.Unit} from {costEvent.Source}.",
            costEvent.OperationCorrelationId,
            now);
        var charge = new SpendCharge(costEvent.AgentId, costEvent.Window, costEvent.Amount, recorded, limit > 0m ? Stop(costEvent, limit, now) : null);

        // One write: the charge, and the stop when it reaches the limit. Delivery ignores the caller's cancellation.
        await this.store.AddSpendAsync(charge, cancellationToken).ConfigureAwait(false);
        await this.outbox.DeliverAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CostStatus> GetStatusAsync(string scope, string window, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(window);
        using var activity = this.telemetry.Start("cost-guard", "get-status");
        var limit = await this.ReadLimitAsync(scope, window, cancellationToken).ConfigureAwait(false);
        var current = await this.store.GetSpendAsync(scope, window, cancellationToken).ConfigureAwait(false);
        return new CostStatus(current, limit, limit > 0m ? Math.Max(0m, limit - current) : decimal.MaxValue, window);
    }

    // A negative configured limit would read as unlimited and silently disable enforcement, so it is not a limit.
    private static bool TryParseLimit(string? raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value >= 0m;

    private static BudgetStop Stop(CostEvent costEvent, decimal limit, DateTimeOffset now)
    {
        var name = "agent:" + costEvent.AgentId;
        return new BudgetStop(
            limit,
            new PauseState(name, BudgetPausedBy, BudgetPauseReason, now),
            [
                PendingAudit.Create("operator.pauses.paused", BudgetPausedBy, AuditOutcome.Succeeded, new AuditTarget("pause", name), BudgetPauseReason, null, now),
                PendingAudit.Create(
                    "operator.cost-guard.budget-stop",
                    costEvent.AgentId,
                    AuditOutcome.Denied,
                    new AuditTarget("cost-scope", costEvent.AgentId),
                    $"Spend reached limit {limit} for window {costEvent.Window}; the agent is paused.",
                    costEvent.OperationCorrelationId,
                    now),
            ]);
    }

    private async Task<decimal> ReadLimitAsync(string scope, string window, CancellationToken cancellationToken)
    {
        // A window's own limit wins; without one, the scope's standing limit applies, so a new window never starts
        // without a budget.
        var forWindow = await this.config.GetValueAsync($"{this.options.ConfigKeyPrefix}:Budget:{scope}:{window}", string.Empty, cancellationToken).ConfigureAwait(false);
        if (TryParseLimit(forWindow, out var value))
        {
            return value;
        }

        var forScope = await this.config.GetValueAsync($"{this.options.ConfigKeyPrefix}:Budget:{scope}", string.Empty, cancellationToken).ConfigureAwait(false);
        return TryParseLimit(forScope, out value) ? value : this.options.DefaultBudgetLimit;
    }
}
