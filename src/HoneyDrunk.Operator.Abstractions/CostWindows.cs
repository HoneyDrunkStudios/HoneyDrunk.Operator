using System.Globalization;

namespace HoneyDrunk.Operator.Abstractions;

/// <summary>Standard budget windows for <see cref="ICostGuard"/>.</summary>
public static class CostWindows
{
    /// <summary>The monthly window an instant falls in: the UTC year and month, <c>yyyy-MM</c>.</summary>
    /// <param name="at">The instant.</param>
    /// <returns>The window key.</returns>
    public static string Month(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
