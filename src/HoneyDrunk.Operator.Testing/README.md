# HoneyDrunk.Operator.Testing

In-memory fixtures for composing `HoneyDrunk.Operator.Abstractions` contracts in tests and local
development. References **only** `HoneyDrunk.Operator.Abstractions` (invariant 3).

> **Not for production composition.** Production projects must not reference this package
> (invariant 116). Test projects use it to substitute Operator's contracts without standing up Vault,
> Kernel, or Audit.

| Fixture | Contract | Default behavior |
|---|---|---|
| `InMemoryApprovalGate` | `IApprovalGate` | Records requests; returns `Approved`. `Decide(id, outcome)` changes a decision. |
| `InMemoryApprovalDesk` | `IApprovalDesk` | Lists the gate's pending requests and decides them through `Decide`. |
| `InMemoryAgentPauses` | `IAgentPauses` | Pauses until resumed; pausing again keeps the first pause. |
| `InMemoryCircuitBreaker` | `ICircuitBreaker` | Closed; honors trip/reset. |
| `InMemoryCostGuard` | `ICostGuard` | Accumulates spend against `DefaultLimit`. |
| `PermissiveDecisionPolicy` | `IDecisionPolicy` | Always `Allow`. |
| `PermissiveSafetyFilter` | `ISafetyFilter` | Always allowed. |

Since 0.3.0, `InMemoryApprovalDesk` also implements `Approvals.ISubjectScopedApprovalDesk`: it filters the
fixture's pending requests by subject before newest-first ordering and the result limit. This fixture keeps
all requests in memory and is not a production storage or expiry-enforcement substitute.
