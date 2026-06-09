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

/// <summary>
/// A request to evaluate content against the safety rule set.
/// </summary>
/// <param name="Content">The content to evaluate.</param>
/// <param name="ContentKind">The kind of content (for example <c>chat-output</c> or <c>tool-argument</c>).</param>
/// <param name="Context">Additional attributes available to safety rules.</param>
public sealed record SafetyFilterRequest(string Content, string ContentKind, IReadOnlyDictionary<string, string> Context);

/// <summary>
/// The outcome of a safety-filter evaluation.
/// </summary>
/// <param name="Allowed">Whether the content is permitted.</param>
/// <param name="BlockReason">The reason the content was blocked, or <see langword="null"/> when allowed.</param>
/// <param name="FiredRules">The identifiers of the rules that fired.</param>
public sealed record SafetyFilterResult(bool Allowed, string? BlockReason, string[] FiredRules);
