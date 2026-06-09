namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// A single recorded unit of spend attributable to an actor, window, and source.
/// </summary>
/// <remarks>
/// When recorded via <see cref="ICostGuard.RecordAsync"/>, <see cref="AgentId"/> is the budget scope
/// and <see cref="Window"/> the budget window — the same pair passed to
/// <see cref="ICostGuard.CheckBudgetAsync"/>. <see cref="TenantId"/> is carried for attribution only.
/// </remarks>
/// <param name="EventId">The stable identifier for this cost event.</param>
/// <param name="AgentId">The agent or actor that incurred the cost. Used as the budget scope when recorded.</param>
/// <param name="TenantId">The tenant the cost is attributed to. Plain string by design — Abstractions carries no HoneyDrunk identity types per invariant 1.</param>
/// <param name="Window">The budget window the cost falls in.</param>
/// <param name="Amount">The cost amount.</param>
/// <param name="Unit">The unit of the amount (for example <c>usd</c> or <c>tokens</c>).</param>
/// <param name="Source">The source of the cost (for example a model or provider identifier).</param>
/// <param name="OccurredAt">The instant the cost was incurred.</param>
/// <param name="OperationCorrelationId">The correlation identifier of the originating operation.</param>
public sealed record CostEvent(
    string EventId,
    string AgentId,
    string TenantId,
    string Window,
    decimal Amount,
    string Unit,
    string Source,
    DateTimeOffset OccurredAt,
    string OperationCorrelationId);
