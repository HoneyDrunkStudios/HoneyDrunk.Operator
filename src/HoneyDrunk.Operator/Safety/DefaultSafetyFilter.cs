using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Telemetry;

namespace HoneyDrunk.Operator.Safety;

/// <summary>
/// Default <see cref="ISafetyFilter"/> that evaluates the registered <see cref="ISafetyRule"/> chain
/// and blocks content when any rule fires.
/// </summary>
/// <remarks>
/// The active rule set is composed through dependency injection; rule enablement and configuration
/// are sourced from Azure App Configuration via Vault per ADR-0018 D6 (invariant 117). With no rules
/// registered the filter is permissive by default.
/// </remarks>
/// <param name="rules">The registered safety rules.</param>
/// <param name="telemetry">The Operator telemetry helper.</param>
public sealed class DefaultSafetyFilter(IEnumerable<ISafetyRule> rules, OperatorTelemetry telemetry) : ISafetyFilter
{
    private readonly IReadOnlyList<ISafetyRule> rules = [.. rules ?? throw new ArgumentNullException(nameof(rules))];
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

    /// <inheritdoc />
    public async Task<SafetyFilterResult> CheckAsync(SafetyFilterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var activity = this.telemetry.Start("safety-filter", "check");

        var fired = new List<string>();
        foreach (var rule in this.rules)
        {
            if (await rule.IsViolatedAsync(request, cancellationToken).ConfigureAwait(false))
            {
                fired.Add(rule.RuleId);
            }
        }

        return fired.Count == 0
            ? new SafetyFilterResult(true, null, [])
            : new SafetyFilterResult(false, $"Blocked by {fired.Count} safety rule(s).", [.. fired]);
    }
}
