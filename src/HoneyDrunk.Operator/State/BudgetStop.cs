using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.State;

/// <summary>Pauses an agent when a charge brings its window's total to or above <paramref name="Limit"/>.</summary>
/// <param name="Limit">The limit, above zero.</param>
/// <param name="Pause">The pause to add. An existing pause of the same name, a person's or an earlier stop, is never replaced.</param>
/// <param name="Audits">The audit entries saved with the pause, only when this charge adds it.</param>
public sealed record BudgetStop(decimal Limit, PauseState Pause, IReadOnlyList<PendingAudit> Audits);
