using HoneyDrunk.Operator.Abstractions;

namespace HoneyDrunk.Operator.Testing;

/// <summary>
/// In-memory <see cref="ISafetyFilter"/> that always allows. Intended for tests where safety is not
/// under test.
/// </summary>
public sealed class PermissiveSafetyFilter : ISafetyFilter
{
    /// <inheritdoc />
    public Task<SafetyFilterResult> CheckAsync(SafetyFilterRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new SafetyFilterResult(true, null, []));
    }
}
