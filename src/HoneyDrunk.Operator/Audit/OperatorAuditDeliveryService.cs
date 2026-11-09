using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Operator.Audit;

/// <summary>
/// Retries undelivered audit entries every <see cref="OperatorOptions.AuditDeliveryIntervalSeconds"/>, so an entry whose
/// first delivery failed, or one saved by a change that delivers nothing itself (an approval expiring when read), still
/// reaches the audit sinks.
/// </summary>
/// <param name="outbox">The outbox deliverer.</param>
/// <param name="options">The interval.</param>
/// <param name="timeProvider">The clock the timer runs on.</param>
public sealed class OperatorAuditDeliveryService(
    OperatorAuditOutbox outbox,
    IOptions<OperatorOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly OperatorAuditOutbox outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    private readonly TimeSpan interval = TimeSpan.FromSeconds(Math.Max(1, (options ?? throw new ArgumentNullException(nameof(options))).Value.AuditDeliveryIntervalSeconds));
    private readonly TimeProvider timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(this.interval, this.timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                // Drain what is waiting, a batch at a time, until a pass delivers less than a full batch.
                int delivered;
                do
                {
                    delivered = await this.outbox.DeliverAsync(stoppingToken).ConfigureAwait(false);
                }
                while (delivered == OperatorAuditOutbox.BatchSize);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }
}
