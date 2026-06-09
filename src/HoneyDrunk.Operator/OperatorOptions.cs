namespace HoneyDrunk.Operator;

/// <summary>
/// Options for HoneyDrunk.Operator runtime registration.
/// </summary>
/// <remarks>
/// These values are startup fallbacks only. Live cost-rate tables, breaker thresholds,
/// decision-policy rule sets, and safety-filter configuration are sourced from Azure App
/// Configuration via Vault's configuration provider per ADR-0018 D6 (invariant 117).
/// </remarks>
public sealed class OperatorOptions
{
    /// <summary>Gets or sets the configuration key prefix Operator reads settings under.</summary>
    public string ConfigKeyPrefix { get; set; } = "HoneyDrunk:Operator";

    /// <summary>Gets or sets the fallback failure count that trips a breaker when config is absent.</summary>
    public int DefaultBreakerFailureThreshold { get; set; } = 5;

    /// <summary>Gets or sets the fallback half-open trial count when config is absent.</summary>
    public int DefaultBreakerHalfOpenTrialCount { get; set; } = 1;

    /// <summary>Gets or sets the fallback reset window, in seconds, when config is absent.</summary>
    public int DefaultBreakerResetWindowSeconds { get; set; } = 30;

    /// <summary>Gets or sets the fallback per-window budget limit when config is absent.</summary>
    public decimal DefaultBudgetLimit { get; set; } = 0m;
}
