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
/// and recorded spend are emitted to the audit substrate.
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
    private readonly ConcurrentDictionary<string, decimal> spend = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<CostCheckResult> CheckBudgetAsync(string scope, string window, decimal amount, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(window);
        using var activity = this.telemetry.Start("cost-guard", "check-budget");

        var limit = await this.ReadLimitAsync(scope, window, cancellationToken).ConfigureAwait(false);
        var current = this.spend.GetValueOrDefault(Key(scope, window));
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
            return new CostCheckResult(false, limit - current, limit, $"Budget exceeded for scope '{scope}' in window '{window}'.");
        }

        return new CostCheckResult(true, limit > 0m ? limit - projected : decimal.MaxValue, limit, null);
    }

    /// <inheritdoc />
    public async Task RecordAsync(CostEvent costEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(costEvent);
        using var activity = this.telemetry.Start("cost-guard", "record");
        this.spend.AddOrUpdate(Key(costEvent.AgentId, costEvent.Window), costEvent.Amount, (_, existing) => existing + costEvent.Amount);

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
        var limit = await this.ReadLimitAsync(scope, window, cancellationToken).ConfigureAwait(false);
        var current = this.spend.GetValueOrDefault(Key(scope, window));
        return new CostStatus(current, limit, limit > 0m ? limit - current : decimal.MaxValue, window);
    }

    private static string Key(string scope, string window) => $"{scope}::{window}";

    private async Task<decimal> ReadLimitAsync(string scope, string window, CancellationToken cancellationToken)
    {
        var raw = await this.config.GetValueAsync(
            $"{this.options.ConfigKeyPrefix}:Budget:{scope}:{window}",
            this.options.DefaultBudgetLimit.ToString(CultureInfo.InvariantCulture),
            cancellationToken).ConfigureAwait(false);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : this.options.DefaultBudgetLimit;
    }
}
