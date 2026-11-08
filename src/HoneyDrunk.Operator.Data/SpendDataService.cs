using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Operator.Data;

/// <summary>Adds and reads spend per budget window, in whole micro-units. Each call uses its own context from the factory.</summary>
/// <param name="contexts">Creates a context per call.</param>
public sealed class SpendDataService(IDbContextFactory<OperatorDbContext> contexts)
{
    private const decimal MicrosPerUnit = 1_000_000m;

    private readonly IDbContextFactory<OperatorDbContext> contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    /// <inheritdoc cref="IOperatorStore.AddSpendAsync" />
    public async Task<decimal> AddSpendAsync(string scope, string window, decimal amount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        var micros = ToMicros(amount);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // From here the caller's cancellation is not observed: a charge that commits must also return its total, or the
        // budget check after it would never run.
        cancellationToken.ThrowIfCancellationRequested();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var updated = await context.Spend
                .Where(row => row.Scope == scope && row.Window == window)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.AmountMicros, row => row.AmountMicros + micros), CancellationToken.None)
                .ConfigureAwait(false);
            if (updated == 1)
            {
                return await this.GetSpendAsync(scope, window, CancellationToken.None).ConfigureAwait(false);
            }

            context.Spend.Add(new SpendEntity { Scope = scope, Window = window, AmountMicros = micros });
            try
            {
                await context.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
                return micros / MicrosPerUnit;
            }
            catch (DbUpdateException) when (this.SpendExists(scope, window))
            {
                // Another writer created the row first; add to it on the next pass.
                context.ChangeTracker.Clear();
            }
        }

        throw new InvalidOperationException($"Spend for '{scope}' in '{window}' could not be recorded after repeated conflicts.");
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

    // Rounds up, so a positive charge smaller than one micro-unit still counts towards the budget.
    private static long ToMicros(decimal amount) => (long)decimal.Ceiling(amount * MicrosPerUnit);

    private bool SpendExists(string scope, string window)
    {
        using var context = this.contexts.CreateDbContext();
        return context.Spend.Any(row => row.Scope == scope && row.Window == window);
    }
}
