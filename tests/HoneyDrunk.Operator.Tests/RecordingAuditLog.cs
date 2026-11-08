using HoneyDrunk.Audit.Abstractions;

namespace HoneyDrunk.Operator.Tests;

/// <summary>An audit log that keeps every entry.</summary>
internal sealed class RecordingAuditLog : IAuditLog
{
    private readonly List<AuditEntry> entries = [];

    public IReadOnlyList<AuditEntry> Entries
    {
        get
        {
            lock (this.entries)
            {
                return [.. this.entries];
            }
        }
    }

    public Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        lock (this.entries)
        {
            this.entries.Add(entry);
        }

        return Task.CompletedTask;
    }
}
