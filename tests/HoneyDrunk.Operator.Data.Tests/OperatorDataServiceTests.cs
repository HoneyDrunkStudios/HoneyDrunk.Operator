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

    /// <summary>Registration replaces the in-memory store whichever order the host calls it in.</summary>
    [Fact]
    public void Data_store_replaces_inmemory()
    {
        var services = new ServiceCollection();
        services.AddOperatorDataStore(options => options.UseSqlite(this.database.ConnectionString));
        services.AddHoneyDrunkOperator();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<OperatorDataService>(provider.GetRequiredService<IOperatorStore>());
    }

    /// <inheritdoc />
    public void Dispose() => this.database.Dispose();

    /// <inheritdoc />
    protected override IOperatorStore CreateStore() => new OperatorDataService(this.database.Factory);
}
