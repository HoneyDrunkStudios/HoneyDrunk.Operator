using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.Telemetry;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;

namespace HoneyDrunk.Operator.Cost;

/// <summary>
/// In-process <see cref="ICostGuard"/> accumulator. Per-window budget limits are read from Azure
/// App Configuration via Vault's <see cref="IConfigProvider"/> per ADR-0018 D6 (invariant 117).
/// </summary>
/// <remarks>
/// Durable persistence of the accumulator (via HoneyDrunk.Data's repository surface per ADR-0018
/// D12) is deferred to a follow-up packet; v0.1.0 accumulates in process. Budget-exceeded denials
/// and recorded spend are emitted to the audit substrate. Check-then-record is intentionally split
/// across <see cref="CheckBudgetAsync"/> and <see cref="RecordAsync"/> per the contract and is not
/// atomic under concurrent callers in v0.1.0; hard enforcement under contention lands with the
/// durable (transactional) backing.
/// </remarks>
/// <param name="config">The Vault configuration provider.</param>
/// <param name="options">Startup fallback options.</param>
/// <param name="telemetry">The Operator telemetry helper.</param>
/// <param name="audit">The Operator audit writer.</param>
public sealed class DefaultCostGuard(
    IConfigProvider config,
    IOptions<OperatorOptions> options,
    OperatorTelemetry telemetry,
    OperatorAuditWriter audit) : ICostGuard
{
    private readonly IConfigProvider config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly OperatorOptions options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly OperatorAuditWriter audit = audit ?? throw new ArgumentNullException(nameof(audit));

    // TODO(data): back the accumulator with HoneyDrunk.Data's IRepository / IUnitOfWork for
    // durability across process restarts per ADR-0018 D12. Tracked as a follow-up packet.
    // Keyed by the (scope, window) tuple so delimiter-bearing scopes/windows can't collide.
    private readonly ConcurrentDictionary<(string Scope, string Window), decimal> spend = new();

    /// <inheritdoc />
    public async Task<CostCheckResult> CheckBudgetAsync(string scope, string window, decimal amount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(window);

        // A negative projected charge would reduce accumulated spend and could be used to slip
        // under a budget; cost is non-negative by contract.
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        using var activity = this.telemetry.Start("cost-guard", "check-budget");

        var limit = await this.ReadLimitAsync(scope, window, cancellationToken).ConfigureAwait(false);
        var current = this.spend.GetValueOrDefault((scope, window));
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

        // Recorded spend is non-negative; a negative amount would subtract from the accumulator
        // and let a scope drift back under budget.
        ArgumentOutOfRangeException.ThrowIfNegative(costEvent.Amount);
        using var activity = this.telemetry.Start("cost-guard", "record");
        this.spend.AddOrUpdate((costEvent.AgentId, costEvent.Window), costEvent.Amount, (_, existing) => existing + costEvent.Amount);

        await this.audit.WriteAsync(
            "operator.cost-guard.recorded",
            costEvent.AgentId,
            AuditOutcome.Succeeded,
            new AuditTarget("cost-scope", costEvent.AgentId),
            $"Recorded {costEvent.Amount} {costEvent.Unit} from {costEvent.Source}.",
            costEvent.OperationCorrelationId,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<CostStatus> GetStatusAsync(string scope, string window, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(window);
        using var activity = this.telemetry.Start("cost-guard", "get-status");
        var limit = await this.ReadLimitAsync(scope, window, cancellationToken).ConfigureAwait(false);
        var current = this.spend.GetValueOrDefault((scope, window));
        return new CostStatus(current, limit, limit > 0m ? Math.Max(0m, limit - current) : decimal.MaxValue, window);
    }

    private async Task<decimal> ReadLimitAsync(string scope, string window, CancellationToken cancellationToken)
    {
        var raw = await this.config.GetValueAsync(
            $"{this.options.ConfigKeyPrefix}:Budget:{scope}:{window}",
            this.options.DefaultBudgetLimit.ToString(CultureInfo.InvariantCulture),
            cancellationToken).ConfigureAwait(false);

        // A negative configured limit would read as "unlimited" (limit > 0 is false) and silently
        // disable enforcement, so reject it and fall back to the configured default.
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0m
            ? value
            : this.options.DefaultBudgetLimit;
    }
}
