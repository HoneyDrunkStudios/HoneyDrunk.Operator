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

    public string? ThrowOn { get; set; }

    /// <summary>Gets or sets an event name whose append waits on <see cref="Release"/> after signalling <see cref="Held"/>.</summary>
    public string? HoldOn { get; set; }

    public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<(string eventName, bool canBeCanceled)> Tokens { get; } = [];

    public async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        bool hold;
        lock (this.entries)
        {
            this.Tokens.Add((eventName: entry.EventName, canBeCanceled: cancellationToken.CanBeCanceled));
            if (entry.EventName == this.ThrowOn)
            {
                throw new InvalidOperationException("audit sink failed");
            }

            hold = entry.EventName == this.HoldOn;
        }

        if (hold)
        {
            this.Held.TrySetResult();
            await this.Release.Task.ConfigureAwait(false);
        }

        lock (this.entries)
        {
            this.entries.Add(entry);
        }
    }
}
