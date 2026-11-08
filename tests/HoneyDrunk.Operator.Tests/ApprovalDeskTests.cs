using HoneyDrunk.Operator.Abstractions;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>A person's approve or deny is recorded once and never overwritten.</summary>
public sealed class ApprovalDeskTests
{
    /// <summary>Requesting the same id again never resets a decision.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Rerequest_never_overwrites_a_decision()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());
        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "looks fine");

        var again = await fixture.Gate.RequestAsync(fixture.Request());

        Assert.Equal(ApprovalOutcome.Approved, again.Outcome);
        Assert.Equal(ApprovalOutcome.Approved, (await fixture.Gate.CheckStatusAsync("a1"))!.Outcome);
    }

    /// <summary>An approval is visible to the gate.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Approve_then_status_reports_approved()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());

        var decision = await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "looks fine");

        Assert.Equal(ApprovalOutcome.Approved, decision!.Outcome);
        Assert.Equal(ApprovalOutcome.Approved, (await fixture.Gate.CheckStatusAsync("a1"))!.Outcome);
    }

    /// <summary>A denial records who and why.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Deny_records_who_and_why()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());

        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Denied, "founder", "not today");

        var status = await fixture.Gate.CheckStatusAsync("a1");
        Assert.Equal(ApprovalOutcome.Denied, status!.Outcome);
        Assert.Equal("founder", status.ApproverIdentity);
        Assert.Equal("not today", status.Reason);
        Assert.Equal(fixture.Clock.GetUtcNow(), status.DecidedAt);
    }

    /// <summary>The same decision twice returns the stored one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Same_decision_twice_is_idempotent()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());
        var first = await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "looks fine");
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        var second = await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "someone", "again");

        Assert.Equal(first, second);
    }

    /// <summary>A different decision after one is recorded is refused.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Conflicting_decision_throws()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());
        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "looks fine");

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Desk.DecideAsync("a1", ApprovalOutcome.Denied, "founder", "changed my mind"));
    }

    /// <summary>Two people deciding at once store exactly one decision.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Concurrent_decisions_store_one()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());

        var attempts = await Task.WhenAll(
            Attempt(() => fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "one", "yes")),
            Attempt(() => fixture.Desk.DecideAsync("a1", ApprovalOutcome.Denied, "two", "no")));

        Assert.Single(attempts, succeeded => succeeded);
        Assert.Single(fixture.AuditLog.Entries, entry => entry.EventName == "operator.approval-desk.decided");
    }

    /// <summary>An expired request cannot be decided.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Expired_approval_cannot_be_decided()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());
        fixture.Clock.Advance(TimeSpan.FromHours(2));

        var decision = await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "too late");

        Assert.Equal(ApprovalOutcome.Expired, decision!.Outcome);
        Assert.Equal(ApprovalOutcome.Expired, (await fixture.Gate.CheckStatusAsync("a1"))!.Outcome);
    }

    /// <summary>An unknown id gives null.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Unknown_id_returns_null()
    {
        var fixture = new OperatorFixture();

        Assert.Null(await fixture.Desk.DecideAsync("nope", ApprovalOutcome.Approved, "founder", "yes"));
    }

    /// <summary>Only Approved or Denied may be recorded.</summary>
    /// <param name="outcome">An outcome a person may not record.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(ApprovalOutcome.Pending)]
    [InlineData(ApprovalOutcome.Expired)]
    public async Task Only_approved_or_denied_may_be_recorded(ApprovalOutcome outcome)
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Desk.DecideAsync("a1", outcome, "founder", "why"));
    }

    /// <summary>Approver and reason text are checked.</summary>
    /// <param name="approver">The approver.</param>
    /// <param name="reason">The reason.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("", "why")]
    [InlineData("founder", " ")]
    [InlineData("found\ner", "why")]
    public async Task Decision_text_is_validated(string approver, string reason)
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, approver, reason));
    }

    /// <summary>The pending list leaves out decided and expired requests.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pending_list_excludes_decided_and_expired()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request("decided"));
        await fixture.Gate.RequestAsync(fixture.Request("short") with { Expiry = fixture.Clock.GetUtcNow().AddMinutes(1) });
        fixture.Clock.Advance(TimeSpan.FromMinutes(2));
        await fixture.Gate.RequestAsync(fixture.Request("waiting"));
        await fixture.Desk.DecideAsync("decided", ApprovalOutcome.Denied, "founder", "no");

        var pending = await fixture.Desk.ListPendingAsync();

        Assert.Equal(["waiting"], pending.Select(item => item.Request.ApprovalId));
    }

    /// <summary>A decision writes one audit entry with its reason.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Decision_writes_one_audit_entry()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());

        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "looks fine");

        var entry = Assert.Single(fixture.AuditLog.Entries, item => item.EventName == "operator.approval-desk.decided");
        Assert.Equal("founder", entry.Actor);
        Assert.Equal("looks fine", entry.Reason);
        Assert.Equal("a1", entry.Target.Id);
    }

    /// <summary>The request context is stored exactly as the caller gave it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Request_context_is_stored_as_given()
    {
        var fixture = new OperatorFixture();
        var context = new Dictionary<string, string>(StringComparer.Ordinal) { ["tool"] = "search@1.0.0", ["requestKey"] = "tool-abc" };
        await fixture.Gate.RequestAsync(fixture.Request(context: context));

        var pending = Assert.Single(await fixture.Desk.ListPendingAsync());

        Assert.Equal(context.Keys.Order(StringComparer.Ordinal), pending.Request.Context.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("tool-abc", pending.Request.Context["requestKey"]);
    }

    private static async Task<bool> Attempt(Func<Task<ApprovalDecision?>> decide)
    {
        try
        {
            await decide();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
