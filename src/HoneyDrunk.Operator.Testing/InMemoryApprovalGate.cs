using System.Collections.Concurrent;
using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.Testing;

/// <summary>
/// In-memory <see cref="IApprovalGate"/> for tests. Records every received request for assertions and
/// returns a configurable outcome (default <see cref="ApprovalOutcome.Approved"/>) immediately.
/// </summary>
public sealed class InMemoryApprovalGate : IApprovalGate
{
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

        var decision = new ApprovalDecision(request.ApprovalId, this.DefaultOutcome, "in-memory", DateTimeOffset.UtcNow, null);
        this.decisions[request.ApprovalId] = decision;
        return Task.FromResult(decision);
    }

    /// <inheritdoc />
    public Task<ApprovalDecision?> CheckStatusAsync(string approvalId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalId);
        return Task.FromResult(this.decisions.TryGetValue(approvalId, out var decision) ? decision : null);
    }
}
