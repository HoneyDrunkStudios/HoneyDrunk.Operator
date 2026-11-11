using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Abstractions.Approvals;
using HoneyDrunk.Operator.State;
using NSubstitute;
using Xunit;

namespace HoneyDrunk.Operator.Tests.Approvals;

/// <summary>Scoped reads use the store capability before limiting approvals and keep legacy behavior intact.</summary>
public sealed class ScopedApprovalDeskTests
{
    /// <summary>Unrelated requests cannot crowd out older selected subjects; decided and expired requests remain excluded.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Filter_precedes_limit_and_preserves_pending_order()
    {
        var fixture = new OperatorFixture();
        await fixture.Gate.RequestAsync(fixture.Request("b"));
        await fixture.Gate.RequestAsync(fixture.Request("a") with { Subject = "agent:b" });
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        await fixture.Gate.RequestAsync(fixture.Request("expired") with { Expiry = fixture.Clock.GetUtcNow().AddSeconds(1) });
        await fixture.Gate.RequestAsync(fixture.Request("decided"));
        await fixture.Desk.DecideAsync("decided", ApprovalOutcome.Denied, "operator", "not allowed");
        fixture.Clock.Advance(TimeSpan.FromSeconds(2));
        for (var number = 0; number < 101; number++)
        {
            await fixture.Gate.RequestAsync(fixture.Request($"noise-{number}") with { Subject = "user:a" });
        }

        var result = await fixture.Desk.ListPendingAsync(new PendingApprovalQuery(["agent:a", "agent:b"], 2));

        Assert.Equal(["a", "b"], result.Select(item => item.Request.ApprovalId));
        Assert.Equal(100, (await fixture.Desk.ListPendingAsync()).Count);
        Assert.Empty(await fixture.Desk.ListPendingAsync(new PendingApprovalQuery([])));
    }

    /// <summary>A custom legacy store is not silently queried without the required subject scope.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Unsupported_store_fails_without_unscoped_fallback()
    {
        var store = Substitute.For<IOperatorStore>();
        var fixture = new OperatorFixture(store);

        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Desk.ListPendingAsync(new PendingApprovalQuery(["agent:a"])));
        Assert.Empty(store.ReceivedCalls());
        Assert.Empty(await fixture.Desk.ListPendingAsync(new PendingApprovalQuery([])));
    }

    /// <summary>Cancellation is honored before a scoped store read.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Canceled_read_does_not_query_the_store()
    {
        var fixture = new OperatorFixture();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Desk.ListPendingAsync(new PendingApprovalQuery(["agent:a"]), cancellation.Token));
    }
}
