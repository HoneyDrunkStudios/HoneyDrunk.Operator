namespace HoneyDrunk.Operator.Tests;

/// <summary>A <see cref="TimeProvider"/> whose current instant can be advanced by tests.</summary>
/// <param name="start">The starting instant.</param>
internal sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => this.now;

    public void Advance(TimeSpan delta) => this.now += delta;
}
