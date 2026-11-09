namespace HoneyDrunk.Operator.State;

/// <summary>Checks the short human-entered text Operator stores: who acted and why.</summary>
internal static class OperatorText
{
    /// <summary>The longest identity accepted.</summary>
    public const int MaxIdentityLength = 200;

    /// <summary>The longest reason accepted.</summary>
    public const int MaxReasonLength = 500;

    /// <summary>Checks who acted.</summary>
    /// <param name="value">The identity.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <returns>The identity, unchanged when valid.</returns>
    public static string Identity(string value, string paramName) => Check(value, MaxIdentityLength, paramName);

    /// <summary>Checks a reason.</summary>
    /// <param name="value">The reason.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <returns>The reason, unchanged when valid.</returns>
    public static string Reason(string value, string paramName) => Check(value, MaxReasonLength, paramName);

    private static string Check(string value, int maxLength, string paramName)
    {
        if (value is null)
        {
            // The caller's own parameter name is reported, not this helper's.
            throw new ArgumentNullException(paramName);
        }

        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength || value.Any(char.IsControl))
        {
            throw new ArgumentException($"The value must be 1 to {maxLength} characters, not blank, with no control characters.", paramName);
        }

        return value;
    }
}
