using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.State;
using HoneyDrunk.Operator.Telemetry;

namespace HoneyDrunk.Operator.Pauses;

/// <summary>The default <see cref="IAgentPauses"/>: pauses live in the <see cref="IOperatorStore"/> and are audited.</summary>
/// <param name="store">Where pauses are kept.</param>
/// <param name="audit">The audit writer.</param>
/// <param name="telemetry">Operator telemetry.</param>
/// <param name="timeProvider">The clock.</param>
public sealed class DefaultAgentPauses(
    IOperatorStore store,
    OperatorAuditWriter audit,
    OperatorTelemetry telemetry,
    TimeProvider timeProvider) : IAgentPauses
{
    private readonly IOperatorStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly OperatorAuditWriter audit = audit ?? throw new ArgumentNullException(nameof(audit));
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async Task<PauseState> PauseAsync(string name, string pausedBy, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        OperatorText.Identity(pausedBy, nameof(pausedBy));
        OperatorText.Reason(reason, nameof(reason));
        using var activity = this.telemetry.Start("pauses", "pause");
        var existing = await this.store.GetPauseAsync(name, cancellationToken).ConfigureAwait(false);
        var pause = new PauseState(name, pausedBy, reason, existing?.Since ?? this.timeProvider.GetUtcNow());
        await this.store.SetPauseAsync(pause, cancellationToken).ConfigureAwait(false);
        await this.audit.WriteAsync("operator.pauses.paused", pausedBy, AuditOutcome.Succeeded, new AuditTarget("pause", name), reason, cancellationToken: cancellationToken).ConfigureAwait(false);
        return pause;
    }

    /// <inheritdoc />
    public async Task<bool> ResumeAsync(string name, string resumedBy, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        OperatorText.Identity(resumedBy, nameof(resumedBy));
        OperatorText.Reason(reason, nameof(reason));
        using var activity = this.telemetry.Start("pauses", "resume");
        if (!await this.store.RemovePauseAsync(name, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await this.audit.WriteAsync("operator.pauses.resumed", resumedBy, AuditOutcome.Succeeded, new AuditTarget("pause", name), reason, cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public Task<PauseState?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return this.store.GetPauseAsync(name, cancellationToken);
    }
}
