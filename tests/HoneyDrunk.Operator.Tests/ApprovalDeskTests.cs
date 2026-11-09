using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using HoneyDrunk.Operator.State;
using NSubstitute;
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

    /// <summary>An id already used for one request cannot carry a different request to its decision.</summary>
    /// <param name="change">What differs in the second request.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("action")]
    [InlineData("subject")]
    [InlineData("scope")]
    [InlineData("context")]
    public async Task Rerequest_with_different_content_is_refused(string change)
    {
        var fixture = new OperatorFixture();
        var original = fixture.Request();
        await fixture.Gate.RequestAsync(original);
        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "looks fine");
        var different = change switch
        {
            "action" => original with { Action = "tool.delete-everything" },
            "subject" => original with { Subject = "agent:someone-else" },
            "scope" => original with { RequestedScope = "capability/other/1.0.0" },
            _ => original with { Context = new Dictionary<string, string> { ["tool"] = "other" } },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gate.RequestAsync(different));
    }

    /// <summary>
    /// What a caller reads from the desk or the gate's store cannot be used to change the stored request: after an
    /// approval, editing a listed request's context does not let a different request inherit that approval.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Listed_request_cannot_be_edited_into_an_approved_one()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request(context: new Dictionary<string, string> { ["path"] = "/reports" }));
        var listed = Assert.Single(await fixture.Desk.ListPendingAsync());
        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "reports only");

        try
        {
            ((IDictionary<string, string>)listed.Request.Context)["path"] = "/secrets";
        }
        catch (NotSupportedException)
        {
            // The context is read-only, which is the point.
        }

        var different = fixture.Request(context: new Dictionary<string, string> { ["path"] = "/secrets" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gate.RequestAsync(different));
        Assert.Equal("/reports", (await fixture.Store.GetApprovalAsync("a1", CancellationToken.None))!.Request.Context["path"]);
    }

    /// <summary>
    /// A repeated request is compared key by key, exactly, whatever comparer its context dictionary uses: a
    /// case-insensitive dictionary cannot make a different request look like the approved one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Rerequest_is_compared_exactly_whatever_its_dictionary_comparer()
    {
        var fixture = new OperatorFixture();
        var twoCases = fixture.Request("a1", new Dictionary<string, string>(StringComparer.Ordinal) { ["path"] = "/reports", ["PATH"] = "/reports" });
        var oneKey = fixture.Request("a2", new Dictionary<string, string>(StringComparer.Ordinal) { ["tool"] = "search" });
        foreach (var request in new[] { twoCases, oneKey })
        {
            await fixture.Gate.RequestAsync(request);
            await fixture.Desk.DecideAsync(request.ApprovalId, ApprovalOutcome.Approved, "founder", "as requested");
        }

        var smuggled = twoCases with { Context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["path"] = "/reports", ["command"] = "delete" } };
        var otherCase = oneKey with { Context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["TOOL"] = "search" } };
        var same = oneKey with { Context = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["tool"] = "search" } };

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gate.RequestAsync(smuggled));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Gate.RequestAsync(otherCase));
        Assert.Equal(ApprovalOutcome.Approved, (await fixture.Gate.RequestAsync(same)).Outcome);
    }

    /// <summary>
    /// The request that is announced is the request that was stored, even when the caller edits its context while
    /// the request is being raised, and each announcement's context is read-only.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Announcement_carries_the_request_as_it_was_stored()
    {
        var fixture = new OperatorFixture();
        var announced = new List<ApprovalRequest>();
        fixture.Sink.EmitApprovalNeededAsync(Arg.Do<ApprovalRequest>(announced.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var context = new Dictionary<string, string> { ["path"] = "/secrets" };
        fixture.AuditLog.HoldOn = "operator.approval-gate.requested";

        var raising = fixture.Gate.RequestAsync(fixture.Request(context: context));
        await fixture.AuditLog.Held.Task;
        context["path"] = "/reports";
        fixture.AuditLog.Release.SetResult();
        await raising;
        await fixture.Gate.RequestAsync(fixture.Request(context: new Dictionary<string, string> { ["path"] = "/secrets" }));

        Assert.Equal(2, announced.Count);
        Assert.All(announced, request => Assert.Equal("/secrets", request.Context["path"]));
        Assert.All(announced, request => Assert.Throws<NotSupportedException>(() => ((IDictionary<string, string>)request.Context)["path"] = "/reports"));
        Assert.Equal("/secrets", (await fixture.Store.GetApprovalAsync("a1", CancellationToken.None))!.Request.Context["path"]);
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

    /// <summary>
    /// A decision whose read of the request stalls past the expiry is not recorded: the request expires, whatever
    /// the desk saw before the stall.
    /// </summary>
    /// <param name="outcome">The person's decision.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(ApprovalOutcome.Approved)]
    [InlineData(ApprovalOutcome.Denied)]
    public async Task Decision_that_stalls_past_expiry_expires_the_request(ApprovalOutcome outcome)
    {
        var inner = new InMemoryOperatorStore();
        var store = Substitute.For<IOperatorStore>();
        OperatorFixture? fixture = null;
        var stallNextRead = false;
        store.TryAddApprovalAsync(null!, null!, default).ReturnsForAnyArgs(call => inner.TryAddApprovalAsync(call.Arg<StoredApproval>(), call.Arg<PendingAudit>(), CancellationToken.None));
        store.TryUpdateDecisionAsync(null!, default, null!, null!, default).ReturnsForAnyArgs(call => inner.TryUpdateDecisionAsync(call.Arg<string>(), call.Arg<ApprovalOutcome>(), call.Arg<ApprovalDecision>(), call.Arg<PendingAudit>(), CancellationToken.None));
        store.GetApprovalAsync(null!, default).ReturnsForAnyArgs(async call =>
        {
            var read = await inner.GetApprovalAsync(call.Arg<string>(), CancellationToken.None);
            if (stallNextRead)
            {
                // The read answers with the request still pending, but only after its expiry has passed.
                stallNextRead = false;
                fixture!.Clock.Advance(TimeSpan.FromHours(2));
            }

            return read;
        });
        fixture = new OperatorFixture(store);
        await fixture.Gate.RequestAsync(fixture.Request());
        stallNextRead = true;

        var decision = await fixture.Desk.DecideAsync("a1", outcome, "founder", "decided before the stall");

        Assert.Equal(ApprovalOutcome.Expired, decision!.Outcome);
        Assert.Equal(ApprovalOutcome.Expired, (await inner.GetApprovalAsync("a1", CancellationToken.None))!.Decision.Outcome);
    }

    /// <summary>A request with no context is refused before anything is stored or announced, and the error names the context.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Request_without_a_context_is_refused()
    {
        var fixture = new OperatorFixture();

        var failure = await Assert.ThrowsAsync<ArgumentNullException>(() => fixture.Gate.RequestAsync(fixture.Request() with { Context = null! }));

        Assert.Equal("request.Context", failure.ParamName);
        Assert.Null(await fixture.Store.GetApprovalAsync("a1", CancellationToken.None));
        await fixture.Sink.DidNotReceiveWithAnyArgs().EmitApprovalNeededAsync(null!, default);
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

    /// <summary>A pending request asked for again is announced again, so a failed announcement is not final.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pending_rerequest_announces_again_after_a_failed_announcement()
    {
        var fixture = new OperatorFixture();
        fixture.Sink.EmitApprovalNeededAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new OperationCanceledException()), _ => Task.CompletedTask);
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Gate.RequestAsync(fixture.Request()));

        var again = await fixture.Gate.RequestAsync(fixture.Request());

        Assert.Equal(ApprovalOutcome.Pending, again.Outcome);
        await fixture.Sink.Received(2).EmitApprovalNeededAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>());
    }

    /// <summary>A decided request asked for again is not announced again.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Decided_rerequest_is_not_announced_again()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());
        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "fine");

        await fixture.Gate.RequestAsync(fixture.Request());

        await fixture.Sink.Received(1).EmitApprovalNeededAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Once a request or decision is stored, its audit entry does not depend on the caller's token.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Audit_after_a_stored_change_does_not_use_the_callers_token()
    {
        var fixture = new OperatorFixture();
        using var cancellation = new CancellationTokenSource();

        await fixture.Gate.RequestAsync(fixture.Request(), cancellation.Token);
        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "fine", cancellation.Token);
        await fixture.Pauses.PauseAsync("agent:a", "founder", "checking", cancellation.Token);
        await fixture.Pauses.ResumeAsync("agent:a", "founder", "done", cancellation.Token);

        Assert.Equal(4, fixture.AuditLog.Tokens.Count);
        Assert.All(fixture.AuditLog.Tokens, token => Assert.False(token.canBeCanceled, token.eventName));
    }

    /// <summary>A failing audit sink does not fail a decision; its entry stays in the outbox and is delivered later.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Decision_audit_is_delivered_after_a_sink_failure()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());
        fixture.AuditLog.ThrowOn = "operator.approval-desk.decided";

        var decided = await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "fine");
        Assert.DoesNotContain(fixture.AuditLog.Entries, item => item.EventName == "operator.approval-desk.decided");

        fixture.AuditLog.ThrowOn = null;
        fixture.Clock.Advance(OperatorAuditOutbox.ClaimLease);
        await fixture.Outbox.DeliverAsync();

        Assert.Equal(ApprovalOutcome.Approved, decided!.Outcome);
        var entry = Assert.Single(fixture.AuditLog.Entries, item => item.EventName == "operator.approval-desk.decided");
        Assert.Equal("founder", entry.Actor);
    }

    /// <summary>An audited decision made again is not audited a second time.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Audited_decision_is_not_audited_again()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());
        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "fine");

        await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "fine");

        Assert.Single(fixture.AuditLog.Entries, item => item.EventName == "operator.approval-desk.decided");
    }

    /// <summary>The same decision made while the first one's audit entry is still being written is not audited twice.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Overlapping_same_decisions_write_one_audit_entry()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request());
        fixture.AuditLog.HoldOn = "operator.approval-desk.decided";
        var first = fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "fine");
        await fixture.AuditLog.Held.Task;
        fixture.AuditLog.HoldOn = null;

        var second = await fixture.Desk.DecideAsync("a1", ApprovalOutcome.Approved, "founder", "fine");
        fixture.AuditLog.Release.SetResult();
        await first;

        Assert.Equal(ApprovalOutcome.Approved, second!.Outcome);
        Assert.Single(fixture.AuditLog.Entries, item => item.EventName == "operator.approval-desk.decided");
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
