using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Operator.Data;

/// <summary>Reads and writes manual pauses, each write with its outbox entry in one transaction. Each call uses its own context from the factory.</summary>
/// <param name="contexts">Creates a context per call.</param>
public sealed class PauseDataService(IDbContextFactory<OperatorDbContext> contexts)
{
    private const int MaxAttempts = 3;

    private readonly IDbContextFactory<OperatorDbContext> contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    /// <inheritdoc cref="IOperatorStore.GetPauseAsync" />
    public async Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await context.Pauses.AsNoTracking().SingleOrDefaultAsync(pause => pause.Name == name, cancellationToken).ConfigureAwait(false);
        return row is null ? null : new PauseState(row.Name, row.PausedBy, row.Reason, new DateTimeOffset(row.SinceTicks, TimeSpan.Zero));
    }

    /// <inheritdoc cref="IOperatorStore.SetPauseAsync" />
    public async Task SetPauseAsync(PauseState pause, PendingAudit audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pause);
        ArgumentNullException.ThrowIfNull(audit);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                await OperatorUnitOfWork.RunAsync(context, audit.Id, token => Set(context, pause, audit, token), cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateException) when (attempt < MaxAttempts && this.PauseExists(pause.Name))
            {
                // Another host inserted the same pause first; the next attempt updates it instead.
            }
        }
    }

    /// <inheritdoc cref="IOperatorStore.RemovePauseAsync" />
    public async Task<bool> RemovePauseAsync(string name, PendingAudit audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await OperatorUnitOfWork.RunAsync(
            context,
            audit.Id,
            async token =>
            {
                if (await context.Pauses.Where(row => row.Name == name).ExecuteDeleteAsync(token).ConfigureAwait(false) == 0)
                {
                    return false;
                }

                context.PendingAudits.Add(PendingAuditRows.ToRow(audit));
                await context.SaveChangesAsync(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> Set(OperatorDbContext context, PauseState pause, PendingAudit audit, CancellationToken cancellationToken)
    {
        var updated = await context.Pauses
            .Where(row => row.Name == pause.Name)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.PausedBy, pause.PausedBy)
                    .SetProperty(row => row.Reason, pause.Reason)
                    .SetProperty(row => row.SinceTicks, pause.Since.UtcTicks),
                cancellationToken)
            .ConfigureAwait(false);
        if (updated == 0)
        {
            context.Pauses.Add(new PauseEntity { Name = pause.Name, PausedBy = pause.PausedBy, Reason = pause.Reason, SinceTicks = pause.Since.UtcTicks });
        }

        context.PendingAudits.Add(PendingAuditRows.ToRow(audit));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private bool PauseExists(string name)
    {
        using var context = this.contexts.CreateDbContext();
        return context.Pauses.Any(row => row.Name == name);
    }
}
