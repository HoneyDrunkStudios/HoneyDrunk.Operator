using HoneyDrunk.Operator.Abstractions;
using HoneyDrunk.Operator.Telemetry;
using HoneyDrunk.Vault.Abstractions;
using Microsoft.Extensions.Options;

namespace HoneyDrunk.Operator.Policy;

/// <summary>
/// Default <see cref="IDecisionPolicy"/> that resolves an action's outcome from the policy rule set
/// in Azure App Configuration via Vault's <see cref="IConfigProvider"/> per ADR-0018 D6.
/// </summary>
/// <remarks>
/// Per ADR-0018 D5 the production decision path delegates authorization to HoneyDrunk.Auth's
/// authorization-policy surface. That delegation is wired in a follow-up packet once the Auth
/// abstraction is composed into the host (see the <c>TODO(auth)</c> below); v0.1.0 resolves the
/// configured outcome by action key and defaults to <see cref="PolicyOutcome.RequireApproval"/> for
/// unrecognized actions (fail-safe).
/// </remarks>
/// <param name="config">The Vault configuration provider.</param>
/// <param name="options">Startup fallback options.</param>
/// <param name="telemetry">The Operator telemetry helper.</param>
public sealed class AuthBackedDecisionPolicy(
    IConfigProvider config,
    IOptions<OperatorOptions> options,
    OperatorTelemetry telemetry) : IDecisionPolicy
{
    private readonly IConfigProvider config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly OperatorOptions options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    private readonly OperatorTelemetry telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));

    /// <inheritdoc />
    public async Task<PolicyDecision> EvaluateAsync(ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        using var activity = this.telemetry.Start("decision-policy", "evaluate");

        // TODO(auth): delegate to HoneyDrunk.Auth's authorization-policy surface per ADR-0018 D5
        //   once HoneyDrunk.Auth.Abstractions is composed into the host. Tracked as a follow-up packet.
        var ruleKey = $"{this.options.ConfigKeyPrefix}:Policy:{context.Action}";
        var configured = await this.config.GetValueAsync(ruleKey, "RequireApproval", cancellationToken).ConfigureAwait(false);

        var outcome = configured switch
        {
            "Allow" => PolicyOutcome.Allow,
            "Deny" => PolicyOutcome.Deny,
            _ => PolicyOutcome.RequireApproval,
        };

        return new PolicyDecision(outcome, $"Resolved '{context.Action}' from configuration.", [ruleKey]);
    }
}
