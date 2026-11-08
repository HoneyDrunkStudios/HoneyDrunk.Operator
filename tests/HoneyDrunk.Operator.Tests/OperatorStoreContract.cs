using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>Behavior every <see cref="IOperatorStore"/> must have. Each provider's tests derive from this.</summary>
public abstract class OperatorStoreContract
{
    /// <summary>Gets a fixed instant for test data.</summary>
    protected static DateTimeOffset Now { get; } = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Adding an approval twice under one id keeps the first.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Add_approval_refuses_duplicate_id()
    {
        var store = this.CreateStore();

        Assert.True(await store.TryAddApprovalAsync(Pending("a1", Now), CancellationToken.None));
        Assert.False(await store.TryAddApprovalAsync(Pending("a1", Now.AddMinutes(1)), CancellationToken.None));

        Assert.Equal(Now, (await store.GetApprovalAsync("a1", CancellationToken.None))!.RequestedAt);
    }

    /// <summary>A decision update applies only while the stored outcome is the expected one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Decision_update_is_compare_and_set()
    {
        var store = this.CreateStore();
        await store.TryAddApprovalAsync(Pending("a1", Now), CancellationToken.None);
        var approved = new ApprovalDecision("a1", ApprovalOutcome.Approved, "founder", Now, "ok");
        var denied = new ApprovalDecision("a1", ApprovalOutcome.Denied, "founder", Now, "no");

        Assert.True(await store.TryUpdateDecisionAsync("a1", ApprovalOutcome.Pending, approved, CancellationToken.None));
        Assert.False(await store.TryUpdateDecisionAsync("a1", ApprovalOutcome.Pending, denied, CancellationToken.None));

        var stored = await store.GetApprovalAsync("a1", CancellationToken.None);
        Assert.Equal(ApprovalOutcome.Approved, stored!.Decision.Outcome);
        Assert.Equal("ok", stored.Decision.Reason);
    }

    /// <summary>The pending list is newest first and leaves out decided and expired requests.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pending_list_is_newest_first_and_skips_expired_and_decided()
    {
        var store = this.CreateStore();
        await store.TryAddApprovalAsync(Pending("old", Now.AddMinutes(-2)), CancellationToken.None);
        await store.TryAddApprovalAsync(Pending("new", Now.AddMinutes(-1)), CancellationToken.None);
        await store.TryAddApprovalAsync(Pending("expired", Now.AddMinutes(-3), Now.AddSeconds(-1)), CancellationToken.None);
        await store.TryAddApprovalAsync(Pending("decided", Now), CancellationToken.None);
        await store.TryUpdateDecisionAsync("decided", ApprovalOutcome.Pending, new ApprovalDecision("decided", ApprovalOutcome.Denied, "founder", Now, "no"), CancellationToken.None);

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
        await store.TryAddApprovalAsync(Pending("a1", Now, context: context), CancellationToken.None);

        var stored = await store.GetApprovalAsync("a1", CancellationToken.None);

        Assert.Equal(context.OrderBy(pair => pair.Key, StringComparer.Ordinal), stored!.Request.Context.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    /// <summary>A decision starts unaudited and is marked audited once its audit entry is written.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Decision_audit_mark_round_trips()
    {
        var store = this.CreateStore();
        var request = new ApprovalRequest("a1", "agent:a", "tool.x", new Dictionary<string, string>(), "capability/x/1.0.0", Now.AddHours(1), "corr");
        await store.TryAddApprovalAsync(new StoredApproval(request, new ApprovalDecision("a1", ApprovalOutcome.Pending, string.Empty, Now, null), Now), CancellationToken.None);
        await store.TryUpdateDecisionAsync("a1", ApprovalOutcome.Pending, new ApprovalDecision("a1", ApprovalOutcome.Approved, "founder", Now, "fine"), CancellationToken.None);
        Assert.False((await store.GetApprovalAsync("a1", CancellationToken.None))!.DecisionAudited);

        await store.MarkDecisionAuditedAsync("a1", CancellationToken.None);

        Assert.True((await store.GetApprovalAsync("a1", CancellationToken.None))!.DecisionAudited);
    }

    /// <summary>Adding a pause inserts only when none exists, and never replaces one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task TryAddPause_inserts_only_when_absent()
    {
        var store = this.CreateStore();

        Assert.True(await store.TryAddPauseAsync(new PauseState("agent:a", "operator:budget", "monthly budget reached", Now), CancellationToken.None));
        Assert.False(await store.TryAddPauseAsync(new PauseState("agent:a", "someone", "other", Now), CancellationToken.None));

        Assert.Equal("operator:budget", (await store.GetPauseAsync("agent:a", CancellationToken.None))!.PausedBy);
    }

    /// <summary>Pauses are set, read and removed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pause_set_get_remove()
    {
        var store = this.CreateStore();
        await store.SetPauseAsync(new PauseState("agent:a", "founder", "checking", Now), CancellationToken.None);

        Assert.Equal("checking", (await store.GetPauseAsync("agent:a", CancellationToken.None))!.Reason);
        Assert.True(await store.RemovePauseAsync("agent:a", CancellationToken.None));
        Assert.False(await store.RemovePauseAsync("agent:a", CancellationToken.None));
        Assert.Null(await store.GetPauseAsync("agent:a", CancellationToken.None));
    }

    /// <summary>Concurrent spend additions are all counted.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Add_spend_is_atomic_under_concurrency()
    {
        var store = this.CreateStore();

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => store.AddSpendAsync("agent-a", "2026-11", 1m, CancellationToken.None))));

        Assert.Equal(50m, await store.GetSpendAsync("agent-a", "2026-11", CancellationToken.None));
        Assert.Equal(0m, await store.GetSpendAsync("agent-a", "2026-12", CancellationToken.None));
    }

    /// <summary>Creates the store under test.</summary>
    /// <returns>A new, empty store.</returns>
    protected abstract IOperatorStore CreateStore();

    private static StoredApproval Pending(string id, DateTimeOffset requestedAt, DateTimeOffset? expiry = null, IReadOnlyDictionary<string, string>? context = null) =>
        new(
            new ApprovalRequest(id, "agent:a", "tool.x", context ?? new Dictionary<string, string>(), "capability/x/1.0.0", expiry ?? Now.AddHours(1), "corr"),
            new ApprovalDecision(id, ApprovalOutcome.Pending, string.Empty, requestedAt, null),
            requestedAt);
}
