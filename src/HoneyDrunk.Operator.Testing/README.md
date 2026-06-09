# HoneyDrunk.Operator.Testing

In-memory fixtures for composing `HoneyDrunk.Operator.Abstractions` contracts in tests and local
development. References **only** `HoneyDrunk.Operator.Abstractions` (invariant 3).

> **Not for production composition.** Production projects must not reference this package
> (invariant 116). Test projects use it to substitute Operator's contracts without standing up Vault,
> Kernel, or Audit.

| Fixture | Contract | Default behavior |
|---|---|---|
| `InMemoryApprovalGate` | `IApprovalGate` | Records requests; returns `Approved`. |
| `InMemoryCircuitBreaker` | `ICircuitBreaker` | Closed; honors trip/reset. |
| `InMemoryCostGuard` | `ICostGuard` | Accumulates spend against `DefaultLimit`. |
| `PermissiveDecisionPolicy` | `IDecisionPolicy` | Always `Allow`. |
| `PermissiveSafetyFilter` | `ISafetyFilter` | Always allowed. |
