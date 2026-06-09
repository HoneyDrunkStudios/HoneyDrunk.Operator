using System.Collections.Concurrent;
using System.Globalization;
using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.Telemetry;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.Options;

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
public sealed class DefaultCircuitBreaker(
    IConfigProvider config,
    IOptions<OperatorOptions> options,
    OperatorTelemetry telemetry,
    OperatorAuditWriter audit) : ICircuitBreaker
{
    private readonly IConfigProvider config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly OperatorOptions options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly OperatorAuditWriter audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly ConcurrentDictionary<string, BreakerEntry> entries = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(string breakerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(breakerName);
        using var activity = this.telemetry.Start("circuit-breaker", "is-allowed");
        var entry = this.entries.GetOrAdd(breakerName, static _ => new BreakerEntry());
        var resetWindow = await this.ReadIntAsync($"Breaker:{breakerName}:ResetWindowSeconds", this.options.DefaultBreakerResetWindowSeconds, cancellationToken).ConfigureAwait(false);

        lock (entry.Gate)
        {
            if (entry.State == BreakerState.Open && DateTimeOffset.UtcNow - entry.OpenedAt >= TimeSpan.FromSeconds(resetWindow))
            {
                entry.State = BreakerState.HalfOpen;
            }

            return entry.State != BreakerState.Open;
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
            entry.OpenedAt = DateTimeOffset.UtcNow;
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
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private sealed class BreakerEntry
    {
        public object Gate { get; } = new();

        public BreakerState State { get; set; } = BreakerState.Closed;

        public DateTimeOffset OpenedAt { get; set; }
    }
}
