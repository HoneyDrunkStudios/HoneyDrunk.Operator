namespace HoneyDrunk.Operator.Data;

/// <summary>A manual pause.</summary>
public sealed class PauseEntity
{
    /// <summary>Gets or sets the breaker name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets who paused it.</summary>
    public string PausedBy { get; set; } = string.Empty;

    /// <summary>Gets or sets why.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets when the pause began, in UTC ticks.</summary>
    public long SinceTicks { get; set; }
}
