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
/// <c>operator:budget</c>) unless it is already paused. Only a person resumes it; a new window does not. The limit
/// is read before the spend is stored, so a configuration failure stores nothing. Once the spend is stored, the stop
/// runs before anything else and ignores the caller's cancellation, so it cannot be skipped. The budget check and
/// the record stay separate calls, so the stop is enforced on recorded spend; a
/// caller that skips the check can overshoot by one charge before the pause takes effect.
/// </remarks>
/// <param name="config">The Vault configuration provider.</param>
/// <param name="options">Startup fallback options.</param>
/// <param name="telemetry">The Operator telemetry helper.</param>
/// <param name="audit">The Operator audit writer.</param>
/// <param name="store">Where spend is kept.</param>
/// <param name="timeProvider">Stamps the budget stop.</param>
public sealed class DefaultCostGuard(
    IConfigProvider config,
    IOptions<OperatorOptions> options,
    OperatorTelemetry telemetry,
    OperatorAuditWriter audit,
    IOperatorStore store,
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

        // The limit is read first: if configuration cannot be read, nothing is stored, and a retry charges only once.
        var limit = await this.ReadLimitAsync(costEvent.AgentId, costEvent.Window, cancellationToken).ConfigureAwait(false);
        var total = await this.store.AddSpendAsync(costEvent.AgentId, costEvent.Window, costEvent.Amount, cancellationToken).ConfigureAwait(false);

        // The spend is stored. From here nothing uses the caller's token, and the stop comes first, so a failure or a
        // cancellation cannot leave spend over the limit without a pause.
        if (limit > 0m && total >= limit)
        {
            await this.StopAsync(costEvent.AgentId, total, limit, costEvent.Window).ConfigureAwait(false);
        }

        await this.audit.WriteAsync(
            "operator.cost-guard.recorded",
            costEvent.AgentId,
            AuditOutcome.Succeeded,
            new AuditTarget("cost-scope", costEvent.AgentId),
            $"Recorded {costEvent.Amount} {costEvent.Unit} from {costEvent.Source}.",
            costEvent.OperationCorrelationId,
            CancellationToken.None).ConfigureAwait(false);
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

    private async Task StopAsync(string agentId, decimal total, decimal limit, string window)
    {
        var name = "agent:" + agentId;
        var pause = new PauseState(name, BudgetPausedBy, BudgetPauseReason, this.timeProvider.GetUtcNow());
        if (!await this.store.TryAddPauseAsync(pause, CancellationToken.None).ConfigureAwait(false))
        {
            // Already stopped, by the budget or by a person; never replace that pause or record a second stop.
            return;
        }

        await this.audit.WriteAsync("operator.pauses.paused", BudgetPausedBy, AuditOutcome.Succeeded, new AuditTarget("pause", name), BudgetPauseReason, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        await this.audit.WriteAsync(
            "operator.cost-guard.budget-stop",
            agentId,
            AuditOutcome.Denied,
            new AuditTarget("cost-scope", agentId),
            $"Spend {total} reached limit {limit} for window {window}; the agent is paused.",
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
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
