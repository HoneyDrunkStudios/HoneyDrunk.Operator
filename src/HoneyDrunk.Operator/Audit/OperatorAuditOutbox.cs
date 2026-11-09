using HoneyDrunk.Operator.State;
using Microsoft.Extensions.Logging;

namespace HoneyDrunk.Operator.Audit;

/// <summary>
/// Delivers the audit entries waiting in the <see cref="IOperatorStore"/> outbox to the audit sinks, oldest first.
/// Each entry is claimed for <see cref="ClaimLease"/> before it is written, so callers on several hosts deliver it once;
/// it is marked delivered once written. An entry whose write fails stays in the outbox and is tried again after its lease
/// lapses, and the entries after it are still delivered; after <see cref="MaxConsecutiveFailures"/> failures in a row
/// the pass stops, as the sink is likely down. Delivery is at least once: if an entry is written but marking it
/// delivered fails, it is written again.
/// </summary>
/// <param name="store">The store holding the outbox.</param>
/// <param name="writer">Writes to the audit sinks.</param>
/// <param name="timeProvider">The clock.</param>
/// <param name="logger">Logs delivery failures.</param>
public sealed partial class OperatorAuditOutbox(
    IOperatorStore store,
    OperatorAuditWriter writer,
    TimeProvider timeProvider,
    ILogger<OperatorAuditOutbox> logger)
{
    /// <summary>The most entries one delivery pass claims.</summary>
    public const int BatchSize = 50;

    /// <summary>Failures in a row after which a pass stops; its remaining claims lapse and are retried.</summary>
    public const int MaxConsecutiveFailures = 3;

    /// <summary>How long a claimed entry is held before another caller may deliver it.</summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(1);

    private readonly IOperatorStore store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly OperatorAuditWriter writer = writer ?? throw new ArgumentNullException(nameof(writer));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger<OperatorAuditOutbox> logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Delivers one batch of waiting entries. A failure is logged, not thrown: the change it records is already saved,
    /// and a caller that saw an exception could retry the change itself. Only a process out of memory or stack throws.
    /// </summary>
    /// <param name="cancellationToken">Stops between entries; an entry being written is finished.</param>
    /// <returns>How many entries were delivered.</returns>
    public async Task<int> DeliverAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PendingAudit> claimed;
        try
        {
            var now = this.timeProvider.GetUtcNow();
            claimed = await this.store.ClaimAuditsAsync(now, now + ClaimLease, BatchSize, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            // Delivery must not fail a change that is already saved; the entries are retried.
            this.LogClaimFailed(ex);
            return 0;
        }

        var delivered = 0;
        var failures = 0;
        foreach (var audit in claimed)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await this.writer.WriteAsync(audit, CancellationToken.None).ConfigureAwait(false);
                await this.store.CompleteAuditAsync(audit.Id, this.timeProvider.GetUtcNow(), CancellationToken.None).ConfigureAwait(false);
                delivered++;
                failures = 0;
            }
            catch (Exception ex) when (IsRecoverable(ex))
            {
                // The entry stays in the outbox and is retried after its lease. One entry may be refused on its own;
                // keep going unless the sink looks down.
                this.LogDeliveryFailed(ex, audit.EventName, audit.Id);
                if (++failures >= MaxConsecutiveFailures)
                {
                    break;
                }
            }
        }

        return delivered;
    }

    // A process out of memory or stack is not in a state to keep delivering; let that failure through.
    private static bool IsRecoverable(Exception exception) => exception is not (OutOfMemoryException or InsufficientExecutionStackException);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Operator could not read its audit outbox; entries are retried later.")]
    private partial void LogClaimFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Operator could not deliver audit entry {EventName} ({AuditId}); it is retried after its lease.")]
    private partial void LogDeliveryFailed(Exception exception, string eventName, Guid auditId);
}
