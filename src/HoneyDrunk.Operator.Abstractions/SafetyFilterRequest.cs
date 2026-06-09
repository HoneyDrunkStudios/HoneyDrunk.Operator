namespace HoneyDrunk.Operator.Abstractions;

/// <summary>
/// A request to evaluate content against the safety rule set.
/// </summary>
/// <param name="Content">The content to evaluate.</param>
/// <param name="ContentKind">The kind of content (for example <c>chat-output</c> or <c>tool-argument</c>).</param>
/// <param name="Context">Additional attributes available to safety rules.</param>
public sealed record SafetyFilterRequest(string Content, string ContentKind, IReadOnlyDictionary<string, string> Context);
