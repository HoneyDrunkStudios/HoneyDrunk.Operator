using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.Telemetry;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Globalization;

namespace HoneyDrunk.Operator.Breaker;

/// <summary>
/// In-process <see cref="ICircuitBreaker"/> implementing the Closed → Open → HalfOpen → Closed
/// lifecycle. Thresholds are read from Azure App Configuration via Vault's
/// <see cref="IConfigProvider"/> per ADR-0018 D6 (invariant 117).
/// </summary>
/// <param name="config">The Vault configuration provider.</param>
/// <param name="options">Startup fallback options.</param>
/// <param name="telemetry">The Operator telemetry helper.</param>
/// <param name="audit">The Operator audit writer.</param>
/// <param name="timeProvider">The clock used for reset-window timing (Grid clock policy).</param>
public sealed class DefaultCircuitBreaker(
    IConfigProvider config,
    IOptions<OperatorOptions> options,
    OperatorTelemetry telemetry,
    OperatorAuditWriter audit,
    TimeProvider timeProvider) : ICircuitBreaker
{
    private readonly IConfigProvider config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly OperatorOptions options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly OperatorAuditWriter audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ConcurrentDictionary<string, BreakerEntry> entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(string breakerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakerName);
        using var activity = this.telemetry.Start("circuit-breaker", "is-allowed");
        var entry = this.entries.GetOrAdd(breakerName, static _ => new BreakerEntry());
        var resetWindow = await this.ReadIntAsync($"Breaker:{breakerName}:ResetWindowSeconds", this.options.DefaultBreakerResetWindowSeconds, cancellationToken).ConfigureAwait(false);
        var trialCount = await this.ReadIntAsync($"Breaker:{breakerName}:HalfOpenTrialCount", this.options.DefaultBreakerHalfOpenTrialCount, cancellationToken).ConfigureAwait(false);

        lock (entry.Gate)
        {
            if (entry.State == BreakerState.Open && this.timeProvider.GetUtcNow() - entry.OpenedAt >= TimeSpan.FromSeconds(resetWindow))
            {
                // Reset window elapsed: admit a bounded number of trial calls to probe recovery.
                entry.State = BreakerState.HalfOpen;
                entry.HalfOpenTrialsRemaining = trialCount;
            }

            if (entry.State == BreakerState.Open)
            {
                return false;
            }

            // While HalfOpen, allow only the configured trial budget; once exhausted, deny until a
            // caller resolves the probe via ResetAsync (recovered) or TripAsync (still failing).
            if (entry.State == BreakerState.HalfOpen)
            {
                if (entry.HalfOpenTrialsRemaining <= 0)
                {
                    return false;
                }

                entry.HalfOpenTrialsRemaining--;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public async Task TripAsync(string breakerName, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakerName);
        using var activity = this.telemetry.Start("circuit-breaker", "trip");
        var entry = this.entries.GetOrAdd(breakerName, static _ => new BreakerEntry());
        lock (entry.Gate)
        {
            entry.State = BreakerState.Open;
            entry.OpenedAt = this.timeProvider.GetUtcNow();
        }

        await this.audit.WriteAsync(
            "operator.circuit-breaker.tripped",
            breakerName,
            AuditOutcome.Succeeded,
            new AuditTarget("circuit-breaker", breakerName),
            reason,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task ResetAsync(string breakerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakerName);
        using var activity = this.telemetry.Start("circuit-breaker", "reset");
        var entry = this.entries.GetOrAdd(breakerName, static _ => new BreakerEntry());
        lock (entry.Gate)
        {
            entry.State = BreakerState.Closed;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<BreakerState> GetStateAsync(string breakerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakerName);
        using var activity = this.telemetry.Start("circuit-breaker", "get-state");
        var entry = this.entries.GetOrAdd(breakerName, static _ => new BreakerEntry());
        lock (entry.Gate)
        {
            return Task.FromResult(entry.State);
        }
    }

    private async Task<int> ReadIntAsync(string key, int fallback, CancellationToken cancellationToken)
    {
        var raw = await this.config.GetValueAsync(
            $"{this.options.ConfigKeyPrefix}:{key}",
            fallback.ToString(CultureInfo.InvariantCulture),
            cancellationToken).ConfigureAwait(false);

        // Negative windows/trial counts produce surprising breaker behavior (instant Open exit,
        // negative trial budget), so treat them as invalid and fall back to the default.
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : fallback;
    }

    private sealed class BreakerEntry
    {
        public object Gate { get; } = new();

        public BreakerState State { get; set; } = BreakerState.Closed;

        public DateTimeOffset OpenedAt { get; set; }

        public int HalfOpenTrialsRemaining { get; set; }
    }
}
