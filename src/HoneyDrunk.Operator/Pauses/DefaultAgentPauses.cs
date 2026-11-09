using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.State;
using HoneyDrunk.Operator.Telemetry;

namespace HoneyDrunk.Operator.Pauses;

/// <summary>
/// The default <see cref="IAgentPauses"/>: pauses live in the <see cref="IOperatorStore"/>. Each pause and resume is saved
/// with its audit entry in one write, so who paused or resumed an agent, and why, is never lost to a failing audit sink;
/// the entry is delivered from the store's outbox.
/// </summary>
/// <param name="store">Where pauses are kept.</param>
/// <param name="outbox">Delivers the saved audit entries.</param>
/// <param name="telemetry">Operator telemetry.</param>
/// <param name="timeProvider">The clock.</param>
public sealed class DefaultAgentPauses(
    IOperatorStore store,
    OperatorAuditOutbox outbox,
    OperatorTelemetry telemetry,
    TimeProvider timeProvider) : IAgentPauses
{
    private readonly IOperatorStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly OperatorAuditOutbox outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    public async Task<PauseState> PauseAsync(string name, string pausedBy, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        OperatorText.Identity(pausedBy, nameof(pausedBy));
        OperatorText.Reason(reason, nameof(reason));
        using var activity = this.telemetry.Start("pauses", "pause");
        var now = this.timeProvider.GetUtcNow();
        var existing = await this.store.GetPauseAsync(name, cancellationToken).ConfigureAwait(false);
        var pause = new PauseState(name, pausedBy, reason, existing?.Since ?? now);
        var audit = PendingAudit.Create("operator.pauses.paused", pausedBy, AuditOutcome.Succeeded, new AuditTarget("pause", name), reason, null, now);
        await this.store.SetPauseAsync(pause, audit, cancellationToken).ConfigureAwait(false);
        await this.outbox.DeliverAsync(CancellationToken.None).ConfigureAwait(false);
        return pause;
    }

    /// <inheritdoc />
    public async Task<bool> ResumeAsync(string name, string resumedBy, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        OperatorText.Identity(resumedBy, nameof(resumedBy));
        OperatorText.Reason(reason, nameof(reason));
        using var activity = this.telemetry.Start("pauses", "resume");
        var audit = PendingAudit.Create("operator.pauses.resumed", resumedBy, AuditOutcome.Succeeded, new AuditTarget("pause", name), reason, null, this.timeProvider.GetUtcNow());
        if (!await this.store.RemovePauseAsync(name, audit, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await this.outbox.DeliverAsync(CancellationToken.None).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc />
    public Task<PauseState?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return this.store.GetPauseAsync(name, cancellationToken);
    }
}
