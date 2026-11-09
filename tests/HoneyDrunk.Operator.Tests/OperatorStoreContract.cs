using HoneyDrunk.Audit.Abstractions;
using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>Behavior every <see cref="IOperatorStore"/> must have. Each provider's tests derive from this.</summary>
public abstract class OperatorStoreContract
{
    /// <summary>The lease the outbox tests claim entries for.</summary>
    protected static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

    /// <summary>Gets a fixed instant for test data.</summary>
    protected static DateTimeOffset Now { get; } = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Adding an approval twice under one id keeps the first, and only the first saves an audit entry.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Add_approval_refuses_duplicate_id()
    {
        var store = this.CreateStore();

        Assert.True(await store.TryAddApprovalAsync(Pending("a1", Now), Audit("requested"), CancellationToken.None));
        Assert.False(await store.TryAddApprovalAsync(Pending("a1", Now.AddMinutes(1)), Audit("requested again"), CancellationToken.None));

        Assert.Equal(Now, (await store.GetApprovalAsync("a1", CancellationToken.None))!.RequestedAt);
        Assert.Equal(["requested"], await ClaimNames(store));
    }

    /// <summary>A decision update applies only while the stored outcome is the expected one, and only the winner saves an entry.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Decision_update_is_compare_and_set()
    {
        var store = this.CreateStore();
        await store.TryAddApprovalAsync(Pending("a1", Now), Audit("requested"), CancellationToken.None);
        var approved = new ApprovalDecision("a1", ApprovalOutcome.Approved, "founder", Now, "ok");
        var denied = new ApprovalDecision("a1", ApprovalOutcome.Denied, "founder", Now, "no");

        Assert.True(await store.TryUpdateDecisionAsync("a1", ApprovalOutcome.Pending, approved, Audit("approved"), CancellationToken.None));
        Assert.False(await store.TryUpdateDecisionAsync("a1", ApprovalOutcome.Pending, denied, Audit("denied"), CancellationToken.None));

        var stored = await store.GetApprovalAsync("a1", CancellationToken.None);
        Assert.Equal(ApprovalOutcome.Approved, stored!.Decision.Outcome);
        Assert.Equal("ok", stored.Decision.Reason);
        Assert.Equal(["requested", "approved"], await ClaimNames(store));
    }

    /// <summary>The pending list is newest first and leaves out decided and expired requests.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pending_list_is_newest_first_and_skips_expired_and_decided()
    {
        var store = this.CreateStore();
        await store.TryAddApprovalAsync(Pending("old", Now.AddMinutes(-2)), Audit("r"), CancellationToken.None);
        await store.TryAddApprovalAsync(Pending("new", Now.AddMinutes(-1)), Audit("r"), CancellationToken.None);
        await store.TryAddApprovalAsync(Pending("expired", Now.AddMinutes(-3), Now.AddSeconds(-1)), Audit("r"), CancellationToken.None);
        await store.TryAddApprovalAsync(Pending("decided", Now), Audit("r"), CancellationToken.None);
        await store.TryUpdateDecisionAsync("decided", ApprovalOutcome.Pending, new ApprovalDecision("decided", ApprovalOutcome.Denied, "founder", Now, "no"), Audit("d"), CancellationToken.None);

        var pending = await store.ListPendingAsync(Now, 10, CancellationToken.None);

        Assert.Equal(["new", "old"], pending.Select(item => item.Request.ApprovalId));
    }

    /// <summary>Request context round-trips exactly.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Request_context_round_trips()
    {
        var store = this.CreateStore();
        var context = new Dictionary<string, string>(StringComparer.Ordinal) { ["tool"] = "search@1.0.0", ["requestKey"] = "tool-abc" };
        await store.TryAddApprovalAsync(Pending("a1", Now, context: context), Audit("r"), CancellationToken.None);

        var stored = await store.GetApprovalAsync("a1", CancellationToken.None);

        Assert.Equal(context.OrderBy(pair => pair.Key, StringComparer.Ordinal), stored!.Request.Context.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    /// <summary>Pauses are set, read and removed; a remove that finds nothing saves no entry.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pause_set_get_remove()
    {
        var store = this.CreateStore();
        await store.SetPauseAsync(new PauseState("agent:a", "founder", "checking", Now), Audit("paused"), CancellationToken.None);
        await store.SetPauseAsync(new PauseState("agent:a", "founder", "still checking", Now), Audit("paused again"), CancellationToken.None);

        Assert.Equal("still checking", (await store.GetPauseAsync("agent:a", CancellationToken.None))!.Reason);
        Assert.True(await store.RemovePauseAsync("agent:a", Audit("resumed"), CancellationToken.None));
        Assert.False(await store.RemovePauseAsync("agent:a", Audit("resumed again"), CancellationToken.None));
        Assert.Null(await store.GetPauseAsync("agent:a", CancellationToken.None));
        Assert.Equal(["paused", "paused again", "resumed"], await ClaimNames(store));
    }

    /// <summary>Concurrent spend additions are all counted, each with its entry.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Add_spend_is_atomic_under_concurrency()
    {
        var store = this.CreateStore();

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => store.AddSpendAsync(Charge(1m), CancellationToken.None))));

        Assert.Equal(50m, await store.GetSpendAsync("agent-a", "2026-11", CancellationToken.None));
        Assert.Equal(0m, await store.GetSpendAsync("agent-a", "2026-12", CancellationToken.None));
        Assert.Equal(50, (await store.ClaimAuditsAsync(Now, Now + Lease, 100, CancellationToken.None)).Count);
    }

    /// <summary>The charge that reaches the limit adds the pause and its entries once; later charges do not.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Charge_reaching_the_limit_stops_once()
    {
        var store = this.CreateStore();

        var below = await store.AddSpendAsync(Charge(6m, Stop(10m)), CancellationToken.None);
        var reaching = await store.AddSpendAsync(Charge(4m, Stop(10m)), CancellationToken.None);
        var after = await store.AddSpendAsync(Charge(1m, Stop(10m)), CancellationToken.None);

        Assert.Equal(new SpendResult(6m, false), below);
        Assert.Equal(new SpendResult(10m, true), reaching);
        Assert.Equal(new SpendResult(11m, false), after);
        Assert.Equal("operator:budget", (await store.GetPauseAsync("agent:agent-a", CancellationToken.None))!.PausedBy);
        Assert.Equal(["recorded", "recorded", "stopped", "recorded"], await ClaimNames(store));
    }

    /// <summary>A stop never replaces a pause already in place.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Stop_never_replaces_an_existing_pause()
    {
        var store = this.CreateStore();
        await store.SetPauseAsync(new PauseState("agent:agent-a", "founder", "checking", Now), Audit("paused"), CancellationToken.None);

        var result = await store.AddSpendAsync(Charge(10m, Stop(10m)), CancellationToken.None);

        Assert.False(result.Stopped);
        Assert.Equal("founder", (await store.GetPauseAsync("agent:agent-a", CancellationToken.None))!.PausedBy);
        Assert.Equal(["paused", "recorded"], await ClaimNames(store));
    }

    /// <summary>A claimed entry is not claimed again until its lease lapses, and a completed one is gone.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Claimed_audit_is_held_until_its_lease_lapses()
    {
        var store = this.CreateStore();
        await store.SetPauseAsync(new PauseState("agent:a", "founder", "checking", Now), Audit("first", Now), CancellationToken.None);
        await store.SetPauseAsync(new PauseState("agent:b", "founder", "checking", Now), Audit("second", Now.AddSeconds(1)), CancellationToken.None);
        await store.SetPauseAsync(new PauseState("agent:c", "founder", "checking", Now), Audit("third", Now.AddSeconds(2)), CancellationToken.None);

        var firstTwo = await store.ClaimAuditsAsync(Now, Now + Lease, 2, CancellationToken.None);
        var rest = await store.ClaimAuditsAsync(Now.AddSeconds(30), Now.AddSeconds(30) + Lease, 10, CancellationToken.None);
        await store.CompleteAuditAsync(firstTwo[0].Id, Now, CancellationToken.None);
        var lapsed = await store.ClaimAuditsAsync(Now + Lease, Now + Lease + Lease, 10, CancellationToken.None);

        Assert.Equal(["first", "second"], firstTwo.Select(entry => entry.EventName));
        Assert.Equal(["third"], rest.Select(entry => entry.EventName));
        Assert.Equal(["second"], lapsed.Select(entry => entry.EventName));
    }

    /// <summary>An entry reads back exactly as it was saved.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Audit_round_trips()
    {
        var store = this.CreateStore();
        var entry = PendingAudit.Create("operator.pauses.paused", "founder", AuditOutcome.Succeeded, new AuditTarget("pause", "agent:a"), "checking", "corr", Now);
        await store.SetPauseAsync(new PauseState("agent:a", "founder", "checking", Now), entry, CancellationToken.None);

        var claimed = Assert.Single(await store.ClaimAuditsAsync(Now, Now + Lease, 10, CancellationToken.None));

        Assert.Equal(entry, claimed);
    }

    /// <summary>Creates an audit entry for store tests.</summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="at">When; defaults to <see cref="Now"/>.</param>
    /// <returns>The entry.</returns>
    protected static PendingAudit Audit(string eventName, DateTimeOffset? at = null) =>
        PendingAudit.Create(eventName, "test", AuditOutcome.Succeeded, new AuditTarget("test", eventName), null, null, at ?? Now);

    /// <summary>Creates a charge against agent-a in 2026-11.</summary>
    /// <param name="amount">The amount.</param>
    /// <param name="stop">The stop, if any.</param>
    /// <returns>The charge.</returns>
    protected static SpendCharge Charge(decimal amount, BudgetStop? stop = null) => new("agent-a", "2026-11", amount, Audit("recorded"), stop);

    /// <summary>Creates the store under test.</summary>
    /// <returns>A new, empty store.</returns>
    protected abstract IOperatorStore CreateStore();

    private static BudgetStop Stop(decimal limit) =>
        new(limit, new PauseState("agent:agent-a", "operator:budget", "monthly budget reached", Now), [Audit("stopped")]);

    private static async Task<IReadOnlyList<string>> ClaimNames(IOperatorStore store) =>
        [.. (await store.ClaimAuditsAsync(Now, Now + Lease, 100, CancellationToken.None)).Select(entry => entry.EventName)];

    private static StoredApproval Pending(string id, DateTimeOffset requestedAt, DateTimeOffset? expiry = null, IReadOnlyDictionary<string, string>? context = null) =>
        new(
            new ApprovalRequest(id, "agent:a", "tool.x", context ?? new Dictionary<string, string>(), "capability/x/1.0.0", expiry ?? Now.AddHours(1), "corr"),
            new ApprovalDecision(id, ApprovalOutcome.Pending, string.Empty, requestedAt, null),
            requestedAt);
}
