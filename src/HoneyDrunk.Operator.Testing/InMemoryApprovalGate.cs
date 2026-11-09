using HoneyDrunk.Operator.Abstractions;
using System.Collections.Concurrent;

namespace HoneyDrunk.Operator.Testing;

/// <summary>
/// In-memory <see cref="IApprovalGate"/> for tests. Records every received request for assertions and
/// returns a configurable outcome (default <see cref="ApprovalOutcome.Approved"/>) immediately.
/// </summary>
/// <param name="timeProvider">
/// Optional clock used to stamp decisions (Grid clock policy); defaults to <see cref="TimeProvider.System"/>.
/// </param>
public sealed class InMemoryApprovalGate(TimeProvider? timeProvider = null) : IApprovalGate
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ConcurrentDictionary<string, ApprovalDecision> decisions = new(StringComparer.Ordinal);
    private readonly List<ApprovalRequest> received = [];

    /// <summary>Gets or sets the outcome returned for new requests.</summary>
    public ApprovalOutcome DefaultOutcome { get; set; } = ApprovalOutcome.Approved;

    /// <summary>Gets the requests this gate has received, in order.</summary>
    public IReadOnlyList<ApprovalRequest> ReceivedRequests
    {
        get
        {
            lock (this.received)
            {
                return [.. this.received];
            }
        }
    }

    /// <inheritdoc />
    public Task<ApprovalDecision> RequestAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (this.received)
        {
            this.received.Add(request);
        }

        var decision = new ApprovalDecision(request.ApprovalId, this.DefaultOutcome, "in-memory", this.timeProvider.GetUtcNow(), null);
        this.decisions[request.ApprovalId] = decision;
        return Task.FromResult(decision);
    }

    /// <summary>
    /// Records a decision for a request this gate has received, as a person at the approval desk would.
    /// </summary>
    /// <param name="approvalId">The approval id.</param>
    /// <param name="outcome">The outcome to record.</param>
    /// <param name="approver">Who decided.</param>
    /// <param name="reason">Why.</param>
    /// <returns>The recorded decision, or <see langword="null"/> when the id is unknown.</returns>
    public ApprovalDecision? Decide(string approvalId, ApprovalOutcome outcome, string approver = "in-memory", string? reason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        if (!this.decisions.ContainsKey(approvalId))
        {
            return null;
        }

        var decision = new ApprovalDecision(approvalId, outcome, approver, this.timeProvider.GetUtcNow(), reason);
        this.decisions[approvalId] = decision;
        return decision;
    }

    /// <inheritdoc />
    public Task<ApprovalDecision?> CheckStatusAsync(string approvalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        return Task.FromResult(this.decisions.TryGetValue(approvalId, out var decision) ? decision : null);
    }

    /// <summary>Lists the received requests whose current decision is still pending, newest first.</summary>
    /// <returns>The pending requests with the time each was recorded.</returns>
    internal IReadOnlyList<PendingApproval> Pending()
    {
        var requests = this.ReceivedRequests;
        var pending = requests
            .Where(request => this.decisions.TryGetValue(request.ApprovalId, out var decision) && decision.Outcome == ApprovalOutcome.Pending)
            .DistinctBy(request => request.ApprovalId, StringComparer.Ordinal)
            .Select(request => new PendingApproval(request, this.decisions[request.ApprovalId].DecidedAt))
            .Reverse();
        return [.. pending];
    }
}
