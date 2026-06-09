# HoneyDrunk.Operator

> **The human control plane for the HoneyDrunk Grid.**
> The system that decides *what to do* must never be the system that decides *whether it's allowed*.

`HoneyDrunk.Operator` is the AI sector's **human-policy enforcement substrate** (ADR-0018). It owns
the primitives that constrain AI operations: approval gates, circuit breakers, cost guards, decision
policy, and safety filters. Operator does not reason — it *constrains*.

> **Note:** this scaffold supersedes earlier placeholder README content that described a
> drift-detection / PR-automation node. The canonical Operator identity is the ADR-0018
> policy-enforcement substrate, as registered in the Grid catalog (`catalogs/nodes.json`).

## Packages

| Package | Role |
|---|---|
| `HoneyDrunk.Operator.Abstractions` | Contracts only — what downstream Nodes compile against (invariant 116). |
| `HoneyDrunk.Operator` | Default runtime composition. |
| `HoneyDrunk.Operator.Testing` | In-memory fixtures for tests and local development. |

## Contracts (ADR-0018 D3)

`IApprovalGate`, `ICircuitBreaker`, `ICostGuard`, `IDecisionPolicy`, `ISafetyFilter` (interfaces) and
`ApprovalRequest`, `ApprovalDecision`, `CostEvent` (records).

### Audit relocation (ADR-0030 / ADR-0031)

`IAuditLog` and `AuditEntry` are **not** owned by Operator. Per the 2026-05-16 ADR-0030 D5 amendment
they live in `HoneyDrunk.Audit.Abstractions`; Operator is a *consumer* and emits `AuditEntry` for
every decision via `HoneyDrunk.Audit`'s `IAuditLog` (graceful no-op when Audit isn't composed).

## How to consume

```csharp
services
    .AddHoneyDrunkOperator()
    .AddSafetyRule<MyContentSafetyRule>();

// Subscribe to approval-needed events by registering an IApprovalEventSink (event-out per D8).
services.AddSingleton<IApprovalEventSink, MyTransportApprovalSink>();
```

The host composes Vault (`IConfigProvider`), Kernel (`ITelemetryActivityFactory`), and — optionally —
the Audit Node (`IAuditLog`). See the runtime package README for host responsibilities.

## Invariants

- **54** — downstream Nodes depend only on `HoneyDrunk.Operator.Abstractions`.
- **55** — cost tables, breaker thresholds, decision policies, and safety-filter config come from App
  Configuration via Vault.
- **56** — approval notifications are event-out; no runtime dependency on Communications.
- **57** — CI carries a contract-shape canary on `IApprovalGate`, `ICircuitBreaker`, `ICostGuard`,
  `ISafetyFilter`.

## Standup

Governed by [ADR-0018](https://github.com/HoneyDrunkStudios/HoneyDrunk.Architecture/blob/main/adrs/ADR-0018-stand-up-honeydrunk-operator-node.md).
