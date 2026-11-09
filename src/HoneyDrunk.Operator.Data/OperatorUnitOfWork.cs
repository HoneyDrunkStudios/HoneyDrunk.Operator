using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HoneyDrunk.Operator.Data;

/// <summary>
/// Runs a change and its outbox entries in one database transaction, under the provider's execution strategy so a host
/// that enables retries on failure still works. Each attempt starts with an empty change tracker. When a commit's
/// outcome is unknown (the reply was lost), the change is not replayed if its audit entry is already stored: every
/// change saves exactly one entry with a unique id, so that entry is the proof the commit happened.
/// </summary>
internal static class OperatorUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> in a transaction and commits it.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="context">The context.</param>
    /// <param name="auditId">The id of the audit entry the change saves when it commits.</param>
    /// <param name="work">The change; it must save its own changes.</param>
    /// <param name="cancellationToken">Cancels the transaction; nothing is kept when it is cancelled before the commit.</param>
    /// <returns>The work's result.</returns>
    public static Task<T> RunAsync<T>(OperatorDbContext context, Guid auditId, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return strategy.ExecuteInTransactionAsync(
            async token =>
            {
                context.ChangeTracker.Clear();
                return await work(token).ConfigureAwait(false);
            },
            token => context.PendingAudits.AsNoTracking().AnyAsync(row => row.Id == auditId, token),
            cancellationToken);
    }
}
