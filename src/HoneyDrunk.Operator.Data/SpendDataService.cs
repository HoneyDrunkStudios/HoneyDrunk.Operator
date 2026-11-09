using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Operator.Data;

/// <summary>
/// Adds and reads spend per budget window, in whole micro-units. A charge, its outbox entry and, when it reaches the
/// limit, the budget stop's pause and entries are saved in one transaction. Each call uses its own context from the factory.
/// </summary>
/// <param name="contexts">Creates a context per call.</param>
public sealed class SpendDataService(IDbContextFactory<OperatorDbContext> contexts)
{
    private const decimal MicrosPerUnit = 1_000_000m;
    private const int MaxAttempts = 3;

    private readonly IDbContextFactory<OperatorDbContext> contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    /// <inheritdoc cref="IOperatorStore.AddSpendAsync" />
    public async Task<SpendResult> AddSpendAsync(SpendCharge charge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(charge);
        ArgumentOutOfRangeException.ThrowIfNegative(charge.Amount);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // A charge that starts is finished: the caller's cancellation is observed only before the transaction, so a
        // cost already incurred is not dropped halfway. The transaction itself keeps or loses the whole charge.
        cancellationToken.ThrowIfCancellationRequested();
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await OperatorUnitOfWork.RunAsync(context, charge.Recorded.Id, token => Charge(context, charge, token), CancellationToken.None).ConfigureAwait(false);
            }
            catch (DbUpdateException) when (attempt < MaxAttempts)
            {
                // Another writer created the window's row, or the stop's pause, first; the next attempt sees it.
            }
        }
    }

    /// <inheritdoc cref="IOperatorStore.GetSpendAsync" />
    public async Task<decimal> GetSpendAsync(string scope, string window, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var micros = await context.Spend.AsNoTracking()
            .Where(row => row.Scope == scope && row.Window == window)
            .Select(row => (long?)row.AmountMicros)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return (micros ?? 0) / MicrosPerUnit;
    }

    private static async Task<SpendResult> Charge(OperatorDbContext context, SpendCharge charge, CancellationToken cancellationToken)
    {
        var micros = ToMicros(charge.Amount);
        var updated = await context.Spend
            .Where(row => row.Scope == charge.Scope && row.Window == charge.Window)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AmountMicros, row => row.AmountMicros + micros), cancellationToken)
            .ConfigureAwait(false);
        if (updated == 0)
        {
            context.Spend.Add(new SpendEntity { Scope = charge.Scope, Window = charge.Window, AmountMicros = micros });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var total = await context.Spend.AsNoTracking()
            .Where(row => row.Scope == charge.Scope && row.Window == charge.Window)
            .Select(row => row.AmountMicros)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false) / MicrosPerUnit;
        context.PendingAudits.Add(PendingAuditRows.ToRow(charge.Recorded));
        var stopped = false;
        if (charge.Stop is { } stop && total >= stop.Limit
            && !await context.Pauses.AnyAsync(row => row.Name == stop.Pause.Name, cancellationToken).ConfigureAwait(false))
        {
            context.Pauses.Add(new PauseEntity { Name = stop.Pause.Name, PausedBy = stop.Pause.PausedBy, Reason = stop.Pause.Reason, SinceTicks = stop.Pause.Since.UtcTicks });
            context.PendingAudits.AddRange(stop.Audits.Select(PendingAuditRows.ToRow));
            stopped = true;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new SpendResult(total, stopped);
    }

    // Rounds up, so a positive charge smaller than one micro-unit still counts towards the budget.
    private static long ToMicros(decimal amount) => (long)decimal.Ceiling(amount * MicrosPerUnit);
}
