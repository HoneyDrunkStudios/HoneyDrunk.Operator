namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// Validates content against a configured safety rule set before it leaves the system.
/// </summary>
/// <remarks>
/// The default rule set is sourced from Azure App Configuration via Vault's configuration provider
/// per ADR-0018 D6. Consumers may register additional safety rules through dependency injection.
/// </remarks>
public interface ISafetyFilter
{
    /// <summary>
    /// Checks content against the active safety rule set.
    /// </summary>
    /// <param name="request">The content and context to evaluate.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A result indicating whether the content is allowed and which rules fired.</returns>
    Task<SafetyFilterResult> CheckAsync(SafetyFilterRequest request, CancellationToken cancellationToken = default);
}
