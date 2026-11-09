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
        await before.TryAddApprovalAsync(new StoredApproval(request, new ApprovalDecision("a1", ApprovalOutcome.Pending, string.Empty, Now, null), Now), CancellationToken.None);
        await before.TryAddApprovalAsync(new StoredApproval(request with { ApprovalId = "a2" }, new ApprovalDecision("a2", ApprovalOutcome.Pending, string.Empty, Now, null), Now), CancellationToken.None);
        await before.TryUpdateDecisionAsync("a2", ApprovalOutcome.Pending, new ApprovalDecision("a2", ApprovalOutcome.Approved, "founder", Now, "fine"), CancellationToken.None);
        await before.SetPauseAsync(new PauseState("agent:a", "founder", "checking", Now), CancellationToken.None);
        await before.AddSpendAsync("agent-a", "2026-11", 2.25m, CancellationToken.None);

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

        await store.AddSpendAsync("agent-a", "2026-11", 0.0000001m, CancellationToken.None);
        var total = await store.AddSpendAsync("agent-a", "2026-11", 0.0000001m, CancellationToken.None);

        Assert.Equal(0.000002m, total);
    }

    /// <summary>A caller cancelling after the spend update has committed still gets the new total.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Total_is_returned_when_cancelled_after_the_update()
    {
        var interceptor = new CancelAfterSpendUpdate();
        using var interceptedDatabase = new SqliteDatabase(interceptor);
        var store = this.Store(options => options.UseSqlite(interceptedDatabase.ConnectionString).AddInterceptors(interceptor));
        await store.AddSpendAsync("agent-a", "2026-11", 6m, CancellationToken.None);

        var total = await store.AddSpendAsync("agent-a", "2026-11", 4m, interceptor.Cancellation.Token);

        Assert.True(interceptor.Cancellation.IsCancellationRequested);
        Assert.Equal(10m, total);
    }

    /// <summary>The first charge in a window returns the total as stored, rounded up.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task First_charge_returns_the_stored_total()
    {
        var store = this.CreateStore();

        var total = await store.AddSpendAsync("agent-a", "2026-11", 0.9999999m, CancellationToken.None);

        Assert.Equal(1m, total);
        Assert.Equal(1m, await store.GetSpendAsync("agent-a", "2026-11", CancellationToken.None));
    }

    /// <summary>A pause that fails to write for any reason but a duplicate surfaces the failure.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Failed_pause_write_is_not_swallowed()
    {
        var store = this.CreateStore();

        await Assert.ThrowsAsync<DbUpdateException>(() => store.SetPauseAsync(new PauseState("agent:a", null!, "checking", Now), CancellationToken.None));
        Assert.Null(await store.GetPauseAsync("agent:a", CancellationToken.None));
    }

    /// <summary>An approval that fails to write is not reported as an existing id.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Failed_approval_write_is_not_reported_as_duplicate()
    {
        var store = this.CreateStore();
        var request = new ApprovalRequest("a1", null!, "tool.x", new Dictionary<string, string>(), "capability/x/1.0.0", Now.AddHours(1), "corr");

        await Assert.ThrowsAsync<DbUpdateException>(() => store.TryAddApprovalAsync(new StoredApproval(request, new ApprovalDecision("a1", ApprovalOutcome.Pending, string.Empty, Now, null), Now), CancellationToken.None));
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
}
