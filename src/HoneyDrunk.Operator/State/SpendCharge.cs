namespace HoneyDrunk.Operator.State;

/// <summary>A charge to add to a budget window, the audit entry that records it, and the stop to apply if it reaches a limit.</summary>
/// <param name="Scope">The budget scope, an agent id for agents.</param>
/// <param name="Window">The window key.</param>
/// <param name="Amount">The non-negative amount.</param>
/// <param name="Recorded">The audit entry recording the charge.</param>
/// <param name="Stop">The stop to apply when the new total reaches its limit; null when there is no limit.</param>
public sealed record SpendCharge(string Scope, string Window, decimal Amount, PendingAudit Recorded, BudgetStop? Stop = null);
