namespace HoneyDrunk.Operator.State;

/// <summary>What a charge did.</summary>
/// <param name="Total">The window's total after the charge, as stored.</param>
/// <param name="Stopped">True when this charge added the budget stop's pause.</param>
public sealed record SpendResult(decimal Total, bool Stopped);
