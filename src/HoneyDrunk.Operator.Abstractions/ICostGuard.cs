namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// Enforces per-scope inference and operation cost limits over a rolling window.
/// </summary>
/// <remarks>
/// Cost-rate tables and per-window budgets are sourced from Azure App Configuration via Vault's
/// configuration provider per ADR-0018 D6 — never hardcoded. <see cref="ICostGuard"/> is the
/// renamed successor to the seed-era <c>ICostController</c> contract.
/// </remarks>
public interface ICostGuard
{
    /// <summary>
    /// Checks whether an amount of spend is permitted against a scope's budget for a window.
    /// </summary>
    /// <param name="scope">The budget scope (for example an agent, workflow, or tenant identifier).</param>
    /// <param name="window">The budget window (for example <c>daily</c> or <c>monthly</c>).</param>
    /// <param name="amount">The proposed spend amount.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A result indicating whether the spend is allowed and the remaining budget.</returns>
    Task<CostCheckResult> CheckBudgetAsync(string scope, string window, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a cost event against a scope's accumulated spend.
    /// </summary>
    /// <param name="costEvent">The cost event to record.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task representing the record operation.</returns>
    Task RecordAsync(CostEvent costEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the current spend status for a scope and window.
    /// </summary>
    /// <param name="scope">The budget scope.</param>
    /// <param name="window">The budget window.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The current spend, limit, and remaining budget for the scope and window.</returns>
    Task<CostStatus> GetStatusAsync(string scope, string window, CancellationToken cancellationToken = default);
}
