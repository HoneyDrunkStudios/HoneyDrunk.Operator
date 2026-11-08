namespace HoneyDrunk.Operator.Data;

/// <summary>Spend in one budget window.</summary>
public sealed class SpendEntity
{
    /// <summary>Gets or sets the budget scope.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Gets or sets the window key.</summary>
    public string Window { get; set; } = string.Empty;

    /// <summary>Gets or sets the total in micro-units (one millionth of the currency unit).</summary>
    public long AmountMicros { get; set; }
}
