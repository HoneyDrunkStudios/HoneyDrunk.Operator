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
/// survives a restart when the store is durable. Per-window limits are read from Azure App Configuration via Vault's
/// <see cref="IConfigProvider"/> per ADR-0018 D6 (invariant 117); a limit of zero means unlimited.
/// </summary>
/// <remarks>
/// When recorded spend for an agent reaches a limit above zero, the agent is paused (<c>agent:{agentId}</c>, by
/// <c>operator:budget</c>) per ADR-0109 D11. Only a person resumes it; a new window does not. The budget check and
/// the record stay separate calls, so the stop is enforced on recorded spend; a caller that skips the check can
/// overshoot by one charge before the pause takes effect.
/// </remarks>
/// <param name="config">The Vault configuration provider.</param>
/// <param name="options">Startup fallback options.</param>
/// <param name="telemetry">The Operator telemetry helper.</param>
/// <param name="audit">The Operator audit writer.</param>
/// <param name="store">Where spend is kept.</param>
/// <param name="pauses">Pauses the agent when its budget is spent.</param>
public sealed class DefaultCostGuard(
    IConfigProvider config,
    IOptions<OperatorOptions> options,
    OperatorTelemetry telemetry,
    OperatorAuditWriter audit,
    IOperatorStore store,
    IAgentPauses pauses) : ICostGuard
{
    /// <summary>Who a budget stop is recorded as.</summary>
    public const string BudgetPausedBy = "operator:budget";

    /// <summary>The reason a budget stop is recorded with.</summary>
    public const string BudgetPauseReason = "monthly budget reached";

    private readonly IConfigProvider config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly OperatorOptions options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly OperatorAuditWriter audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly IOperatorStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IAgentPauses pauses = pauses ?? throw new ArgumentNullException(nameof(pauses));

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

        // Recorded spend is non-negative; a negative amount would let a scope drift back under budget.
        ArgumentOutOfRangeException.ThrowIfNegative(costEvent.Amount);
        using var activity = this.telemetry.Start("cost-guard", "record");
        var total = await this.store.AddSpendAsync(costEvent.AgentId, costEvent.Window, costEvent.Amount, cancellationToken).ConfigureAwait(false);
        await this.audit.WriteAsync(
            "operator.cost-guard.recorded",
            costEvent.AgentId,
            AuditOutcome.Succeeded,
            new AuditTarget("cost-scope", costEvent.AgentId),
            $"Recorded {costEvent.Amount} {costEvent.Unit} from {costEvent.Source}.",
            costEvent.OperationCorrelationId,
            cancellationToken).ConfigureAwait(false);

        var limit = await this.ReadLimitAsync(costEvent.AgentId, costEvent.Window, cancellationToken).ConfigureAwait(false);
        if (limit > 0m && total >= limit)
        {
            await this.StopAsync(costEvent.AgentId, total, limit, costEvent.Window, cancellationToken).ConfigureAwait(false);
        }
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

    private async Task StopAsync(string agentId, decimal total, decimal limit, string window, CancellationToken cancellationToken)
    {
        var name = "agent:" + agentId;
        if (await this.pauses.GetAsync(name, cancellationToken).ConfigureAwait(false) is not null)
        {
            // Already stopped, by the budget or by a person; never write a second pause or record.
            return;
        }

        await this.pauses.PauseAsync(name, BudgetPausedBy, BudgetPauseReason, cancellationToken).ConfigureAwait(false);
        await this.audit.WriteAsync(
            "operator.cost-guard.budget-stop",
            agentId,
            AuditOutcome.Denied,
            new AuditTarget("cost-scope", agentId),
            $"Spend {total} reached limit {limit} for window {window}; the agent is paused.",
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<decimal> ReadLimitAsync(string scope, string window, CancellationToken cancellationToken)
    {
        var raw = await this.config.GetValueAsync(
            $"{this.options.ConfigKeyPrefix}:Budget:{scope}:{window}",
            this.options.DefaultBudgetLimit.ToString(CultureInfo.InvariantCulture),
            cancellationToken).ConfigureAwait(false);

        // A negative configured limit would read as unlimited and silently disable enforcement, so reject it.
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0m
            ? value
            : this.options.DefaultBudgetLimit;
    }
}
