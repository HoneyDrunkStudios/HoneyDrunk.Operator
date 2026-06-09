using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.Testing;

/// <summary>
/// In-memory <see cref="IDecisionPolicy"/> that always allows. Intended for tests where policy is
/// not under test.
/// </summary>
public sealed class PermissiveDecisionPolicy : IDecisionPolicy
{
    /// <inheritdoc />
    public Task<PolicyDecision> EvaluateAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(new PolicyDecision(PolicyOutcome.Allow, "permissive", []));
    }
}
