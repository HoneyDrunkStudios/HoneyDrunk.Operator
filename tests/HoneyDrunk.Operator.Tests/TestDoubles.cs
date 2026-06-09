using HoneyDrunk.Vault.Abstractions;
using NSubstitute;

namespace HoneyDrunk.Operator.Tests;

/// <summary>Shared test-double factories for the Operator runtime tests.</summary>
internal static class TestDoubles
{
    /// <summary>
    /// Creates an <see cref="IConfigProvider"/> substitute that returns the supplied fallback for
    /// every key, optionally overriding specific keys.
    /// </summary>
    /// <param name="overrides">Optional key/value overrides.</param>
    /// <returns>The configured substitute.</returns>
    public static IConfigProvider ConfigProvider(IReadOnlyDictionary<string, string>? overrides = null)
    {
        var config = Substitute.For<IConfigProvider>();
        config.GetValueAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var key = call.ArgAt<string>(0);
                var fallback = call.ArgAt<string>(1);
                return Task.FromResult(overrides is not null && overrides.TryGetValue(key, out var value) ? value : fallback);
            });
        return config;
    }
}
