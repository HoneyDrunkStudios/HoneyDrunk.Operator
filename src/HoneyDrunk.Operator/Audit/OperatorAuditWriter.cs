using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Kernel.Abstractions.Identity;

namespace HoneyDrunk.Operator.Audit;

/// <summary>
/// Emits durable, attributable audit entries for Operator decisions against the Grid audit
/// substrate (<see cref="IAuditLog"/> from <c>HoneyDrunk.Audit.Abstractions</c>) per the
/// ADR-0030 / ADR-0031 relocation of the audit contract out of Operator.
/// </summary>
/// <remarks>
/// The writer depends on a zero-or-more sequence of <see cref="IAuditLog"/> so a host that has not
/// composed the Audit Node degrades gracefully (no audit sink registered ⇒ emission is a no-op)
/// rather than failing dependency resolution. When the Audit Node is composed, every gate, breaker,
/// cost, decision, and safety-filter outcome is appended.
/// </remarks>
/// <param name="auditLogs">The registered audit sinks, if any.</param>
public sealed class OperatorAuditWriter(IEnumerable<IAuditLog> auditLogs)
{
    private readonly IReadOnlyList<IAuditLog> auditLogs = [.. auditLogs ?? throw new ArgumentNullException(nameof(auditLogs))];

    /// <summary>
    /// Gets a value indicating whether an audit sink is composed in the host.
    /// </summary>
    public bool IsEnabled => this.auditLogs.Count > 0;

    /// <summary>
    /// Appends an audit entry describing an Operator decision to every registered sink.
    /// </summary>
    /// <param name="eventName">The audited event name (for example <c>operator.cost-guard.denied</c>).</param>
    /// <param name="actor">The actor the decision concerns.</param>
    /// <param name="outcome">The outcome of the decision.</param>
    /// <param name="target">The resource the decision acted upon.</param>
    /// <param name="reason">An optional human-readable reason.</param>
    /// <param name="correlationId">An optional correlation identifier.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the append across all sinks.</returns>
    public async Task WriteAsync(
        string eventName,
        string actor,
        AuditOutcome outcome,
        AuditTarget target,
        string? reason = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (this.auditLogs.Count == 0)
        {
            return;
        }

        var entry = new AuditEntry(
            AuditEntryId.Empty,
            default,
            actor,
            eventName,
            AuditCategory.SystemAction,
            outcome,
            target,
            TenantId.Internal,
            correlationId,
            AuditOperation.None,
            Changes: null,
            Metadata: null,
            Reason: reason);

        foreach (var log in this.auditLogs)
        {
            await log.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
        }
    }
}
