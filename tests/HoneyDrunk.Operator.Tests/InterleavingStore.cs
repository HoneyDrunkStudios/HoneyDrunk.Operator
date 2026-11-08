using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;

namespace HoneyDrunk.Operator.Tests;

/// <summary>
/// A store that lets a person's pause land at the first pause read or insert, as if they paused between the cost
/// guard's threshold check and its stop. A read at that moment still reports no pause.
/// </summary>
internal sealed class InterleavingStore(IOperatorStore inner, PauseState personPause) : IOperatorStore
{
    private int fired;

    public Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken) => inner.GetApprovalAsync(approvalId, cancellationToken);

    public Task<bool> TryAddApprovalAsync(StoredApproval approval, CancellationToken cancellationToken) => inner.TryAddApprovalAsync(approval, cancellationToken);

    public Task<bool> TryUpdateDecisionAsync(string approvalId, ApprovalOutcome expected, ApprovalDecision decision, CancellationToken cancellationToken) =>
        inner.TryUpdateDecisionAsync(approvalId, expected, decision, cancellationToken);

    public Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken) => inner.ListPendingAsync(now, limit, cancellationToken);

    public async Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken)
    {
        if (await this.FireAsync(name, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return await inner.GetPauseAsync(name, cancellationToken).ConfigureAwait(false);
    }

    public Task SetPauseAsync(PauseState pause, CancellationToken cancellationToken) => inner.SetPauseAsync(pause, cancellationToken);

    public async Task<bool> TryAddPauseAsync(PauseState pause, CancellationToken cancellationToken)
    {
        await this.FireAsync(pause.Name, cancellationToken).ConfigureAwait(false);
        return await inner.TryAddPauseAsync(pause, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> RemovePauseAsync(string name, CancellationToken cancellationToken) => inner.RemovePauseAsync(name, cancellationToken);

    public Task<decimal> AddSpendAsync(string scope, string window, decimal amount, CancellationToken cancellationToken) => inner.AddSpendAsync(scope, window, amount, cancellationToken);

    public Task<decimal> GetSpendAsync(string scope, string window, CancellationToken cancellationToken) => inner.GetSpendAsync(scope, window, cancellationToken);

    private async Task<bool> FireAsync(string name, CancellationToken cancellationToken)
    {
        if (name != personPause.Name || Interlocked.Exchange(ref this.fired, 1) == 1)
        {
            return false;
        }

        await inner.SetPauseAsync(personPause, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
