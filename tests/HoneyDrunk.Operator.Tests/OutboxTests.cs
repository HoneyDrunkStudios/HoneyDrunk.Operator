using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>The audit outbox delivers every entry it can, whatever one entry does.</summary>
public sealed class OutboxTests
{
    /// <summary>An entry the sink keeps rejecting does not hold back the entries after it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Rejected_entry_does_not_block_the_rest()
    {
        var fixture = new OperatorFixture();
        var now = fixture.Clock.GetUtcNow();
        foreach (var name in new[] { "bad", "good-1", "good-2" })
        {
            await fixture.Store.SetPauseAsync(new PauseState("agent:" + name, "founder", "checking", now), Entry(name, now), CancellationToken.None);
        }

        fixture.AuditLog.ThrowOn = "bad";

        await fixture.Outbox.DeliverAsync();

        Assert.Equal(["good-1", "good-2"], fixture.AuditLog.Entries.Select(entry => entry.EventName));
    }

    /// <summary>
    /// An entry delivered late carries the time its change happened, not the time the sink finally took it, so the
    /// order of policy changes can still be read from the audit log.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Delayed_delivery_keeps_the_time_of_the_change()
    {
        var fixture = new OperatorFixture();
        var pausedAt = fixture.Clock.GetUtcNow();
        fixture.AuditLog.ThrowOn = "operator.pauses.paused";
        await fixture.Pauses.PauseAsync("agent:a", "founder", "checking");
        fixture.AuditLog.ThrowOn = null;
        fixture.Clock.Advance(TimeSpan.FromHours(2));
        var resumedAt = fixture.Clock.GetUtcNow();
        await fixture.Pauses.ResumeAsync("agent:a", "founder", "checked");

        Assert.Equal(
            [(name: "operator.pauses.paused", at: pausedAt), (name: "operator.pauses.resumed", at: resumedAt)],
            fixture.AuditLog.Entries.Select(entry => (name: entry.EventName, at: entry.OccurredAt)));
    }

    /// <summary>Audit text longer than the outbox columns is cut to fit, so a long correlation id or reason never blocks the change.</summary>
    [Fact]
    public void Long_audit_text_is_cut_to_fit()
    {
        var entry = PendingAudit.Create("e", "actor", AuditOutcome.Succeeded, new AuditTarget("t", "id"), new string('r', 5000), new string('c', 500), DateTimeOffset.UnixEpoch);

        Assert.Equal(PendingAudit.MaxReasonLength, entry.Reason!.Length);
        Assert.Equal(PendingAudit.MaxCorrelationIdLength, entry.CorrelationId!.Length);
    }

    private static PendingAudit Entry(string name, DateTimeOffset at) =>
        PendingAudit.Create(name, "founder", AuditOutcome.Succeeded, new AuditTarget("pause", name), null, null, at);
}
