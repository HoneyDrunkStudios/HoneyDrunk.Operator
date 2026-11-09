using HoneyDrunk.Operator.State;
using Microsoft.EntityFrameworkCore;

namespace HoneyDrunk.Operator.Data;

/// <summary>Claims and completes outbox entries for delivery. Each call uses its own context from the factory.</summary>
/// <param name="contexts">Creates a context per call.</param>
public sealed class PendingAuditDataService(IDbContextFactory<OperatorDbContext> contexts)
{
    private readonly IDbContextFactory<OperatorDbContext> contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));

    /// <inheritdoc cref="IOperatorStore.ClaimAuditsAsync" />
    public async Task<IReadOnlyList<PendingAudit>> ClaimAuditsAsync(DateTimeOffset now, DateTimeOffset claimUntil, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var nowTicks = now.UtcTicks;
        var untilTicks = claimUntil.UtcTicks;
        var candidates = await context.PendingAudits.AsNoTracking()
            .Where(row => row.ClaimedUntilTicks == null || row.ClaimedUntilTicks <= nowTicks)
            .OrderBy(row => row.Sequence)
            .Take(limit)
            .Select(row => row.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Each claim is its own conditional update, so of two hosts claiming at once each row goes to one.
        var claimed = new List<long>(candidates.Count);
        foreach (var sequence in candidates)
        {
            var updated = await context.PendingAudits
                .Where(row => row.Sequence == sequence && (row.ClaimedUntilTicks == null || row.ClaimedUntilTicks <= nowTicks))
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ClaimedUntilTicks, untilTicks), cancellationToken)
                .ConfigureAwait(false);
            if (updated == 1)
            {
                claimed.Add(sequence);
            }
        }

        var rows = await context.PendingAudits.AsNoTracking()
            .Where(row => claimed.Contains(row.Sequence))
            .OrderBy(row => row.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return [.. rows.Select(PendingAuditRows.ToAudit)];
    }

    /// <inheritdoc cref="IOperatorStore.CompleteAuditAsync" />
    public async Task CompleteAuditAsync(Guid auditId, CancellationToken cancellationToken)
    {
        await using var context = await this.contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.PendingAudits.Where(row => row.Id == auditId).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
