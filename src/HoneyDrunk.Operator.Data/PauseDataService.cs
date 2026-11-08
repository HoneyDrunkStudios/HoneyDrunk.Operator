using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Operator.Data;

/// <summary>Reads and writes manual pauses. Each call uses its own context from the factory.</summary>
/// <param name="contexts">Creates a context per call.</param>
public sealed class PauseDataService(IDbContextFactory<OperatorDbContext> contexts)
{
    private readonly IDbContextFactory<OperatorDbContext> contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    /// <inheritdoc cref="IOperatorStore.GetPauseAsync" />
    public async Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await context.Pauses.AsNoTracking().SingleOrDefaultAsync(pause => pause.Name == name, cancellationToken).ConfigureAwait(false);
        return row is null ? null : new PauseState(row.Name, row.PausedBy, row.Reason, FromTicks(row.SinceTicks));
    }

    /// <inheritdoc cref="IOperatorStore.SetPauseAsync" />
    public async Task SetPauseAsync(PauseState pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pause);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var updated = await context.Pauses
            .Where(row => row.Name == pause.Name)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.PausedBy, pause.PausedBy)
                    .SetProperty(row => row.Reason, pause.Reason)
                    .SetProperty(row => row.SinceTicks, pause.Since.UtcTicks),
                cancellationToken)
            .ConfigureAwait(false);
        if (updated == 1)
        {
            return;
        }

        context.Pauses.Add(new PauseEntity { Name = pause.Name, PausedBy = pause.PausedBy, Reason = pause.Reason, SinceTicks = pause.Since.UtcTicks });
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException) when (this.PauseExists(pause.Name))
        {
            // Another host paused it first; a pause is in place either way. Any other failure propagates: a pause
            // that was never written must not look like one that was.
        }
    }

    /// <inheritdoc cref="IOperatorStore.TryAddPauseAsync" />
    public async Task<bool> TryAddPauseAsync(PauseState pause, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pause);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        if (await context.Pauses.AnyAsync(row => row.Name == pause.Name, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        context.Pauses.Add(new PauseEntity { Name = pause.Name, PausedBy = pause.PausedBy, Reason = pause.Reason, SinceTicks = pause.Since.UtcTicks });
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException) when (this.PauseExists(pause.Name))
        {
            // Another host paused it between the check and the insert; that pause stands.
            return false;
        }
    }

    /// <inheritdoc cref="IOperatorStore.RemovePauseAsync" />
    public async Task<bool> RemovePauseAsync(string name, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.Pauses.Where(row => row.Name == name).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    private static DateTimeOffset FromTicks(long ticks) => new(ticks, TimeSpan.Zero);

    private bool PauseExists(string name)
    {
        using var context = this.contexts.CreateDbContext();
        return context.Pauses.Any(row => row.Name == name);
    }
}
