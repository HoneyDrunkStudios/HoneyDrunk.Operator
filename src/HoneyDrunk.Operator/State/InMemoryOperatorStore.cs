using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.State;

/// <summary>
/// The default store: process memory only, so nothing survives a restart. Use HoneyDrunk.Operator.Data in production.
/// One lock guards every write, so each change and its audit entry are saved together.
/// </summary>
public sealed class InMemoryOperatorStore : IOperatorStore
{
    private readonly Lock sync = new();
    private readonly Dictionary<string, StoredApproval> approvals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PauseState> pauses = new(StringComparer.Ordinal);
    private readonly Dictionary<(string scope, string window), decimal> spend = [];
    private readonly SortedDictionary<long, Outboxed> outbox = [];
    private long sequence;

    /// <inheritdoc />
    public Task<StoredApproval?> GetApprovalAsync(string approvalId, CancellationToken cancellationToken)
    {
        lock (this.sync)
        {
            return Task.FromResult(this.approvals.GetValueOrDefault(approvalId));
        }
    }

    /// <inheritdoc />
    public Task<bool> TryAddApprovalAsync(StoredApproval approval, PendingAudit audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approval);
        ArgumentNullException.ThrowIfNull(audit);
        var copy = approval with { Request = approval.Request with { Context = new Dictionary<string, string>(approval.Request.Context, StringComparer.Ordinal) } };
        lock (this.sync)
        {
            if (!this.approvals.TryAdd(approval.Request.ApprovalId, copy))
            {
                return Task.FromResult(false);
            }

            this.Enqueue(audit);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> TryUpdateDecisionAsync(string approvalId, ApprovalOutcome expected, ApprovalDecision decision, PendingAudit audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(audit);
        lock (this.sync)
        {
            if (!this.approvals.TryGetValue(approvalId, out var current) || current.Decision.Outcome != expected)
            {
                return Task.FromResult(false);
            }

            // A person's decision dated after the expiry is refused here, under the same lock as the write.
            if (decision.Outcome is ApprovalOutcome.Approved or ApprovalOutcome.Denied && decision.DecidedAt > current.Request.Expiry)
            {
                return Task.FromResult(false);
            }

            this.approvals[approvalId] = current with { Decision = decision };
            this.Enqueue(audit);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredApproval>> ListPendingAsync(DateTimeOffset now, int limit, CancellationToken cancellationToken)
    {
        lock (this.sync)
        {
            IReadOnlyList<StoredApproval> pending =
            [
                .. this.approvals.Values
                    .Where(approval => approval.Decision.Outcome == ApprovalOutcome.Pending && approval.Request.Expiry > now)
                    .OrderByDescending(approval => approval.RequestedAt)
                    .ThenBy(approval => approval.Request.ApprovalId, StringComparer.Ordinal)
                    .Take(limit),
            ];
            return Task.FromResult(pending);
        }
    }

    /// <inheritdoc />
    public Task<PauseState?> GetPauseAsync(string name, CancellationToken cancellationToken)
    {
        lock (this.sync)
        {
            return Task.FromResult(this.pauses.GetValueOrDefault(name));
        }
    }

    /// <inheritdoc />
    public Task SetPauseAsync(PauseState pause, PendingAudit audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pause);
        ArgumentNullException.ThrowIfNull(audit);
        lock (this.sync)
        {
            this.pauses[pause.Name] = pause;
            this.Enqueue(audit);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> RemovePauseAsync(string name, PendingAudit audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        lock (this.sync)
        {
            if (!this.pauses.Remove(name))
            {
                return Task.FromResult(false);
            }

            this.Enqueue(audit);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<SpendResult> AddSpendAsync(SpendCharge charge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(charge);
        ArgumentOutOfRangeException.ThrowIfNegative(charge.Amount);
        lock (this.sync)
        {
            var key = (scope: charge.Scope, window: charge.Window);
            var total = this.spend.GetValueOrDefault(key) + charge.Amount;
            this.spend[key] = total;
            this.Enqueue(charge.Recorded);
            var stopped = charge.Stop is { } stop && total >= stop.Limit && this.pauses.TryAdd(stop.Pause.Name, stop.Pause);
            if (stopped)
            {
                foreach (var audit in charge.Stop!.Audits)
                {
                    this.Enqueue(audit);
                }
            }

            return Task.FromResult(new SpendResult(total, stopped));
        }
    }

    /// <inheritdoc />
    public Task<decimal> GetSpendAsync(string scope, string window, CancellationToken cancellationToken)
    {
        lock (this.sync)
        {
            return Task.FromResult(this.spend.GetValueOrDefault((scope, window)));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingAudit>> ClaimAuditsAsync(DateTimeOffset now, DateTimeOffset claimUntil, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        lock (this.sync)
        {
            var claimed = this.outbox
                .Where(item => item.Value.ClaimedUntil is null || item.Value.ClaimedUntil <= now)
                .Take(limit)
                .ToArray();
            foreach (var item in claimed)
            {
                this.outbox[item.Key] = item.Value with { ClaimedUntil = claimUntil };
            }

            IReadOnlyList<PendingAudit> entries = [.. claimed.Select(item => item.Value.Audit)];
            return Task.FromResult(entries);
        }
    }

    /// <inheritdoc />
    /// <remarks>Nothing here checks for an entry after a commit, so a delivered entry is removed at once.</remarks>
    public Task CompleteAuditAsync(Guid auditId, DateTimeOffset deliveredAt, CancellationToken cancellationToken)
    {
        lock (this.sync)
        {
            foreach (var item in this.outbox.Where(item => item.Value.Audit.Id == auditId).ToArray())
            {
                this.outbox.Remove(item.Key);
            }
        }

        return Task.CompletedTask;
    }

    // Called under the lock.
    private void Enqueue(PendingAudit audit) => this.outbox.Add(++this.sequence, new Outboxed(audit, null));

    private sealed record Outboxed(PendingAudit Audit, DateTimeOffset? ClaimedUntil);
}
