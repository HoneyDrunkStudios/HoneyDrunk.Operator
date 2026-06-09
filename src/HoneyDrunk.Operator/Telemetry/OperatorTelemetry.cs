using System.Diagnostics;
using HoneyDrunk.Kernel.Abstractions.Telemetry;

namespace HoneyDrunk.Operator.Telemetry;

/// <summary>
/// Emits per-call telemetry activities for every gate, breaker, cost, decision, and safety-filter
/// operation via Kernel's <see cref="ITelemetryActivityFactory"/> per ADR-0018 D7.
/// </summary>
/// <remarks>
/// Operator emits telemetry one-way; it takes no runtime dependency on Pulse.
/// </remarks>
/// <param name="activityFactory">The Kernel telemetry activity factory.</param>
public sealed class OperatorTelemetry(ITelemetryActivityFactory activityFactory)
{
    private const string ActivityPrefix = "honeydrunk.operator";

    private readonly ITelemetryActivityFactory activityFactory =
        activityFactory ?? throw new ArgumentNullException(nameof(activityFactory));

    /// <summary>
    /// Starts a telemetry activity for an Operator surface call.
    /// </summary>
    /// <param name="surface">The surface name (for example <c>approval-gate</c> or <c>cost-guard</c>).</param>
    /// <param name="operation">The operation name (for example <c>request</c> or <c>check-budget</c>).</param>
    /// <param name="tags">Optional per-call tags.</param>
    /// <returns>The started activity, or <see langword="null"/> when not sampled.</returns>
    public Activity? Start(string surface, string operation, IReadOnlyDictionary<string, object?>? tags = null) =>
        this.activityFactory.Start($"{ActivityPrefix}.{surface}.{operation}", tags);
}
