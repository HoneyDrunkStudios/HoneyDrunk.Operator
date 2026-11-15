using HoneyDrunk.Operator.State;

namespace HoneyDrunk.Operator.Tests;

/// <summary>The store contract against the in-memory provider.</summary>
public sealed class InMemoryOperatorStoreTests : OperatorStoreContract
{
    /// <inheritdoc />
    protected override IOperatorStore CreateStore(TimeProvider? clock = null) => new InMemoryOperatorStore(clock ?? new MutableTimeProvider(Now));
}
