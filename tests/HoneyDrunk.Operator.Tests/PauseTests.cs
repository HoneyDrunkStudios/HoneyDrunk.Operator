using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Audit;
using Xunit;

namespace HoneyDrunk.Operator.Tests;

/// <summary>A manual pause stops an agent until a person resumes it.</summary>
public sealed class PauseTests
{
    private const string Agent = "agent:a";

    /// <summary>A paused name is refused by the breaker.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Paused_agent_is_refused_by_the_breaker()
    {
        var fixture = new OperatorFixture();

        await fixture.Pauses.PauseAsync(Agent, "founder", "checking");

        Assert.False(await fixture.Breaker.IsAllowedAsync(Agent));
        Assert.Equal(BreakerState.Open, await fixture.Breaker.GetStateAsync(Agent));
        Assert.True(await fixture.Breaker.IsAllowedAsync("agent:other"));
    }

    /// <summary>The breaker's reset window never lifts a pause.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Reset_window_never_lifts_a_pause()
    {
        var fixture = new OperatorFixture();
        await fixture.Pauses.PauseAsync(Agent, "founder", "checking");

        fixture.Clock.Advance(TimeSpan.FromDays(3));

        Assert.False(await fixture.Breaker.IsAllowedAsync(Agent));
    }

    /// <summary>Resetting the breaker does not lift a pause.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Breaker_reset_does_not_lift_a_pause()
    {
        var fixture = new OperatorFixture();
        await fixture.Pauses.PauseAsync(Agent, "founder", "checking");

        await fixture.Breaker.ResetAsync(Agent);

        Assert.False(await fixture.Breaker.IsAllowedAsync(Agent));
    }

    /// <summary>Resuming lets calls through again.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Resume_lets_calls_through()
    {
        var fixture = new OperatorFixture();
        await fixture.Pauses.PauseAsync(Agent, "founder", "checking");

        Assert.True(await fixture.Pauses.ResumeAsync(Agent, "founder", "all clear"));

        Assert.True(await fixture.Breaker.IsAllowedAsync(Agent));
        Assert.Null(await fixture.Pauses.GetAsync(Agent));
    }

    /// <summary>Resuming something that is not paused is a no-op.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Resume_when_not_paused_returns_false()
    {
        var fixture = new OperatorFixture();

        Assert.False(await fixture.Pauses.ResumeAsync(Agent, "founder", "nothing to lift"));
        Assert.DoesNotContain(fixture.AuditLog.Entries, entry => entry.EventName == "operator.pauses.resumed");
    }

    /// <summary>Pausing again keeps the first time and takes the new who and why.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pause_again_keeps_the_first_since()
    {
        var fixture = new OperatorFixture();
        var first = await fixture.Pauses.PauseAsync(Agent, "founder", "checking");
        fixture.Clock.Advance(TimeSpan.FromMinutes(5));

        var second = await fixture.Pauses.PauseAsync(Agent, "operator:budget", "monthly budget reached");

        Assert.Equal(first.Since, second.Since);
        Assert.Equal("operator:budget", second.PausedBy);
    }

    /// <summary>Pause and resume each write an audit entry.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Pause_and_resume_write_audit_entries()
    {
        var fixture = new OperatorFixture();

        await fixture.Pauses.PauseAsync(Agent, "founder", "checking");
        await fixture.Pauses.ResumeAsync(Agent, "founder", "all clear");

        Assert.Single(fixture.AuditLog.Entries, entry => entry.EventName == "operator.pauses.paused" && entry.Actor == "founder" && entry.Reason == "checking");
        Assert.Single(fixture.AuditLog.Entries, entry => entry.EventName == "operator.pauses.resumed" && entry.Reason == "all clear");
    }

    /// <summary>Pause text is checked.</summary>
    /// <param name="pausedBy">Who.</param>
    /// <param name="reason">Why.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("", "why")]
    [InlineData("founder", "")]
    [InlineData("founder", "line\nbreak")]
    public async Task Pause_text_is_validated(string pausedBy, string reason)
    {
        var fixture = new OperatorFixture();

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Pauses.PauseAsync(Agent, pausedBy, reason));
    }

    /// <summary>A resume whose audit sink fails still records who lifted the pause and why, delivered later.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Resume_audit_survives_a_sink_failure()
    {
        var fixture = new OperatorFixture();
        await fixture.Pauses.PauseAsync("agent:a", "founder", "checking");
        fixture.AuditLog.ThrowOn = "operator.pauses.resumed";

        Assert.True(await fixture.Pauses.ResumeAsync("agent:a", "founder", "all clear"));
        Assert.Null(await fixture.Pauses.GetAsync("agent:a"));
        Assert.False(await fixture.Pauses.ResumeAsync("agent:a", "founder", "again"));

        fixture.AuditLog.ThrowOn = null;
        fixture.Clock.Advance(OperatorAuditOutbox.ClaimLease);
        await fixture.Outbox.DeliverAsync();

        var resumed = Assert.Single(fixture.AuditLog.Entries, entry => entry.EventName == "operator.pauses.resumed");
        Assert.Equal("founder", resumed.Actor);
        Assert.Equal("all clear", resumed.Reason);
    }
}
