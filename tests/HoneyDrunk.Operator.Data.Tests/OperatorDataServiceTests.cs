using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.State;
using HoneyDrunk.Operator.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HoneyDrunk.Operator.Data.Tests;

/// <summary>The store contract against the Data provider on a SQLite file, plus restart survival.</summary>
public sealed class OperatorDataServiceTests : OperatorStoreContract, IDisposable
{
    private readonly SqliteDatabase database = new();
    private readonly List<ServiceProvider> providers = [];

    /// <summary>Everything written through one store reads back the same through a new one over the same database.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Restart_keeps_approvals_decisions_pauses_and_spend()
    {
        var before = this.CreateStore();
        var request = new ApprovalRequest("a1", "agent:a", "tool.x", new Dictionary<string, string> { ["requestKey"] = "tool-abc" }, "capability/x/1.0.0", Now.AddHours(1), "corr");
        await before.TryAddApprovalAsync(new StoredApproval(request, new ApprovalDecision("a1", ApprovalOutcome.Pending, string.Empty, Now, null), Now), Audit("requested"), CancellationToken.None);
        await before.TryAddApprovalAsync(new StoredApproval(request with { ApprovalId = "a2" }, new ApprovalDecision("a2", ApprovalOutcome.Pending, string.Empty, Now, null), Now), Audit("requested"), CancellationToken.None);
        await before.TryUpdateDecisionAsync("a2", ApprovalOutcome.Pending, new ApprovalDecision("a2", ApprovalOutcome.Approved, "founder", Now, "fine"), Audit("decided"), CancellationToken.None);
        await before.SetPauseAsync(new PauseState("agent:a", "founder", "checking", Now), Audit("paused"), CancellationToken.None);
        await before.AddSpendAsync(Charge(2.25m), CancellationToken.None);

        var after = this.CreateStore();

        var pending = Assert.Single(await after.ListPendingAsync(Now, 10, CancellationToken.None));
        Assert.Equal("a1", pending.Request.ApprovalId);
        Assert.Equal("tool-abc", pending.Request.Context["requestKey"]);
        Assert.Equal(Now.AddHours(1), pending.Request.Expiry);
        var decided = await after.GetApprovalAsync("a2", CancellationToken.None);
        Assert.Equal(new ApprovalDecision("a2", ApprovalOutcome.Approved, "founder", Now, "fine"), decided!.Decision);
        Assert.Equal(new PauseState("agent:a", "founder", "checking", Now), await after.GetPauseAsync("agent:a", CancellationToken.None));
        Assert.Equal(2.25m, await after.GetSpendAsync("agent-a", "2026-11", CancellationToken.None));
    }

    /// <summary>A charge smaller than one micro-unit rounds up, so no positive charge is ever lost.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Tiny_charge_rounds_up_and_is_never_lost()
    {
        var store = this.CreateStore();

        await store.AddSpendAsync(Charge(0.0000001m), CancellationToken.None);
        var total = await store.AddSpendAsync(Charge(0.0000001m), CancellationToken.None);

        Assert.Equal(0.000002m, total.Total);
    }

    /// <summary>A caller cancelling after the spend update has committed still gets the new total.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Total_is_returned_when_cancelled_after_the_update()
    {
        var interceptor = new CancelAfterSpendUpdate();
        using var interceptedDatabase = new SqliteDatabase(interceptor);
        var store = this.Store(options => options.UseSqlite(interceptedDatabase.ConnectionString).AddInterceptors(interceptor));
        await store.AddSpendAsync(Charge(6m), CancellationToken.None);

        var total = await store.AddSpendAsync(Charge(4m), interceptor.Cancellation.Token);

        Assert.True(interceptor.Cancellation.IsCancellationRequested);
        Assert.Equal(10m, total.Total);
    }

    /// <summary>The first charge in a window returns the total as stored, rounded up.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task First_charge_returns_the_stored_total()
    {
        var store = this.CreateStore();

        var total = await store.AddSpendAsync(Charge(0.9999999m), CancellationToken.None);

        Assert.Equal(1m, total.Total);
        Assert.Equal(1m, await store.GetSpendAsync("agent-a", "2026-11", CancellationToken.None));
    }

    /// <summary>A pause that fails to write for any reason but a duplicate surfaces the failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Failed_pause_write_is_not_swallowed()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<DbUpdateException>(() => store.SetPauseAsync(new PauseState("agent:a", null!, "checking", Now), Audit("paused"), CancellationToken.None));
        Assert.Null(await store.GetPauseAsync("agent:a", CancellationToken.None));
    }

    /// <summary>An approval that fails to write is not reported as an existing id.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Failed_approval_write_is_not_reported_as_duplicate()
    {
        var store = this.CreateStore();
        var request = new ApprovalRequest("a1", null!, "tool.x", new Dictionary<string, string>(), "capability/x/1.0.0", Now.AddHours(1), "corr");

        await Assert.ThrowsAsync<DbUpdateException>(() => store.TryAddApprovalAsync(new StoredApproval(request, new ApprovalDecision("a1", ApprovalOutcome.Pending, string.Empty, Now, null), Now), Audit("requested"), CancellationToken.None));
    }

    /// <summary>A charge reaching the limit is never stored without its stop: if the pause cannot be written, nothing is.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Charge_and_its_stop_are_kept_together()
    {
        var failPause = new FailInsertInto("Pauses");
        using var interceptedDatabase = new SqliteDatabase(failPause);
        var store = this.Store(options => options.UseSqlite(interceptedDatabase.ConnectionString).AddInterceptors(failPause));
        await store.AddSpendAsync(Charge(6m), CancellationToken.None);
        failPause.Armed = true;

        var stop = new BudgetStop(10m, new PauseState("agent:agent-a", "operator:budget", "monthly budget reached", Now), [Audit("stopped")]);
        await Assert.ThrowsAsync<DbUpdateException>(() => store.AddSpendAsync(Charge(4m, stop), CancellationToken.None));

        Assert.Equal(6m, await store.GetSpendAsync("agent-a", "2026-11", CancellationToken.None));
        Assert.Null(await store.GetPauseAsync("agent:agent-a", CancellationToken.None));
        Assert.Equal(["recorded"], (await store.ClaimAuditsAsync(Now, Now + Lease, 10, CancellationToken.None)).Select(entry => entry.EventName));
    }

    /// <summary>A resume whose audit entry cannot be saved removes nothing, so a retry still resumes and records it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Resume_and_its_audit_are_kept_together()
    {
        var failAudit = new FailInsertInto("PendingAudits");
        using var interceptedDatabase = new SqliteDatabase(failAudit);
        var store = this.Store(options => options.UseSqlite(interceptedDatabase.ConnectionString).AddInterceptors(failAudit));
        await store.SetPauseAsync(new PauseState("agent:a", "founder", "checking", Now), Audit("paused"), CancellationToken.None);
        failAudit.Armed = true;

        await Assert.ThrowsAsync<DbUpdateException>(() => store.RemovePauseAsync("agent:a", Audit("resumed"), CancellationToken.None));
        Assert.NotNull(await store.GetPauseAsync("agent:a", CancellationToken.None));

        failAudit.Armed = false;
        Assert.True(await store.RemovePauseAsync("agent:a", Audit("resumed"), CancellationToken.None));
        Assert.Equal(["paused", "resumed"], (await store.ClaimAuditsAsync(Now, Now + Lease, 10, CancellationToken.None)).Select(entry => entry.EventName));
    }

    /// <summary>A charge whose commit succeeded but whose reply was lost is not replayed: it is stored once and reported as stored.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Lost_commit_reply_is_not_replayed()
    {
        var lostReply = new LostCommitReply();
        var store = this.Store(options => options
            .UseSqlite(this.database.ConnectionString, sqlite => sqlite.ExecutionStrategy(dependencies => new RetryOnLostReply(dependencies)))
            .AddInterceptors(lostReply));
        await store.AddSpendAsync(Charge(6m), CancellationToken.None);
        await store.SetPauseAsync(new PauseState("agent:a", "founder", "checking", Now), Audit("paused"), CancellationToken.None);

        lostReply.Armed = true;
        var result = await store.AddSpendAsync(Charge(4m), CancellationToken.None);
        lostReply.Armed = true;
        var resumed = await store.RemovePauseAsync("agent:a", Audit("resumed"), CancellationToken.None);

        Assert.Equal(10m, result.Total);
        Assert.Equal(10m, await store.GetSpendAsync("agent-a", "2026-11", CancellationToken.None));
        Assert.True(resumed);
        Assert.Equal(["recorded", "paused", "recorded", "resumed"], (await store.ClaimAuditsAsync(Now, Now + Lease, 10, CancellationToken.None)).Select(entry => entry.EventName));
    }

    /// <summary>
    /// A charge whose reply is lost is not replayed even when another host delivers its audit entry before the retry
    /// checks for it: a delivered entry is kept as the proof that its change committed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Lost_commit_reply_is_not_replayed_after_delivery()
    {
        var lostReply = new LostCommitReply();
        var store = this.Store(options => options
            .UseSqlite(this.database.ConnectionString, sqlite => sqlite.ExecutionStrategy(dependencies => new RetryOnLostReply(dependencies)))
            .AddInterceptors(lostReply));
        var otherHost = this.CreateStore();
        await store.AddSpendAsync(Charge(6m), CancellationToken.None);
        lostReply.MeanwhileAsync = async () =>
        {
            foreach (var entry in await otherHost.ClaimAuditsAsync(Now, Now + Lease, 10, CancellationToken.None))
            {
                await otherHost.CompleteAuditAsync(entry.Id, Now, CancellationToken.None);
            }
        };

        lostReply.Armed = true;
        var result = await store.AddSpendAsync(Charge(4m), CancellationToken.None);

        Assert.Equal(10m, result.Total);
        Assert.Equal(10m, await store.GetSpendAsync("agent-a", "2026-11", CancellationToken.None));
        Assert.Empty(await store.ClaimAuditsAsync(Now, Now + Lease, 10, CancellationToken.None));
    }

    /// <summary>A delivered entry is kept for the retention period and removed by a later delivery once it has passed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Delivered_entry_is_kept_for_a_day_then_removed()
    {
        var store = this.CreateStore();
        var first = await Deliver(store, "first", Now);
        var second = await Deliver(store, "second", Now.AddHours(1));
        var keptIds = await this.StoredAuditIds();
        var third = await Deliver(store, "third", Now.Add(PendingAuditDataService.DeliveredRetention).AddHours(1));

        Assert.Equal([first, second], keptIds);
        Assert.Equal([second, third], await this.StoredAuditIds());
    }

    /// <summary>Registration replaces the in-memory store whichever order the host calls it in.</summary>
    [Fact]
    public void Data_store_replaces_inmemory()
    {
        var services = new ServiceCollection();
        services.AddOperatorDataStore(options => options.UseSqlite(this.database.ConnectionString));
        services.AddHoneyDrunkOperator();

        using var provider = services.BuildServiceProvider();

        Assert.Equal(typeof(OperatorDbContext).Assembly, provider.GetRequiredService<IOperatorStore>().GetType().Assembly);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var provider in this.providers)
        {
            provider.Dispose();
        }

        this.database.Dispose();
    }

    /// <inheritdoc />
    protected override IOperatorStore CreateStore() => this.Store(options => options.UseSqlite(this.database.ConnectionString));

    private IOperatorStore Store(Action<DbContextOptionsBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddOperatorDataStore(configure);
        var provider = services.BuildServiceProvider();
        this.providers.Add(provider);
        return provider.GetRequiredService<IOperatorStore>();
    }

    private static async Task<Guid> Deliver(IOperatorStore store, string name, DateTimeOffset at)
    {
        await store.SetPauseAsync(new PauseState("agent:" + name, "founder", "checking", at), Audit(name, at), CancellationToken.None);
        var entry = Assert.Single(await store.ClaimAuditsAsync(at, at + Lease, 10, CancellationToken.None));
        await store.CompleteAuditAsync(entry.Id, at, CancellationToken.None);
        return entry.Id;
    }

    private async Task<Guid[]> StoredAuditIds()
    {
        await using var context = await this.database.Factory.CreateDbContextAsync(CancellationToken.None);
        return await context.PendingAudits.OrderBy(row => row.Sequence).Select(row => row.Id).ToArrayAsync(CancellationToken.None);
    }
}
