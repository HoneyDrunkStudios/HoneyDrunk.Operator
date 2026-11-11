# HoneyDrunk.Operator

> **The human control plane for the HoneyDrunk Grid.**
> The system that decides *what to do* must never be the system that decides *whether it's allowed*.

`HoneyDrunk.Operator` is the AI sector's **human-policy enforcement substrate** (ADR-0018). It owns
the primitives that constrain AI operations: approval gates, circuit breakers, cost guards, decision
policy, and safety filters. Operator does not reason — it *constrains*.

> **Note:** this scaffold supersedes earlier placeholder README content that described a
> drift-detection / PR-automation node. The canonical Operator identity is the ADR-0018
> policy-enforcement substrate, as registered in the Grid catalog (`catalogs/nodes.json`).

## SDK requirement

Build and test with the .NET 11 SDK selected by `global.json`. Runtime packages target `net11.0`; consuming applications must also target .NET 11 or later. The contracts-only Abstractions package targets both `net10.0` and `net11.0` to preserve existing contract consumers.

## Packages

| Package | Role |
|---|---|
| `HoneyDrunk.Operator.Abstractions` | Contracts only — what downstream Nodes compile against (invariant 116). |
| `HoneyDrunk.Operator` | Default runtime composition. |
| `HoneyDrunk.Operator.Data` | Durable storage for approvals, decisions, pauses and spend, over HoneyDrunk.Data. |
| `HoneyDrunk.Operator.Testing` | In-memory fixtures for tests and local development. |

## Contracts (ADR-0018 D3)

`IApprovalGate`, `ICircuitBreaker`, `ICostGuard`, `IDecisionPolicy`, `ISafetyFilter` (interfaces) and
`ApprovalRequest`, `ApprovalDecision`, `CostEvent` (records).

Since 0.2.0, two contracts let a person act on what the gate and the guard hold:

- `IApprovalDesk` lists pending approvals and records a person's approve or deny. A decision is recorded once;
  the same decision again is idempotent, a different one is refused, and an expired request cannot be decided.
- `IAgentPauses` pauses a named breaker (`agent:{agentId}` for agents) until a person resumes it. A pause
  overrides every breaker state, including the automatic half-open reset.

The cost guard also stops an agent when its spend in the current UTC month (`CostWindows.Month`) reaches its
budget: it pauses `agent:{agentId}` once, as `operator:budget`, and only a person lifts that pause. A budget is read
from `Budget:{agentId}:{yyyy-MM}` for one month, or `Budget:{agentId}` for every month. Every
decision, pause, resume and budget stop writes an audit entry, saved with the change in one store write and delivered
from the store's outbox, so a failing audit sink delays an entry but never loses it.

Requests, decisions, pauses and spend live behind `IOperatorStore`. The default store is in memory; add
`HoneyDrunk.Operator.Data` to keep them across restarts and share them between hosts.

### Subject-scoped approval reads (0.3.0)

Desks that implement `HoneyDrunk.Operator.Abstractions.Approvals.ISubjectScopedApprovalDesk` accept a
`PendingApprovalQuery` with up to 100 subject identities and a result limit from 1 to 100. Filtering happens
before ordering by request time descending and approval id ascending, so newer requests for other workloads
cannot hide a selected workload's pending approval. An empty subject scope returns no results.

The default desk and both production stores support this capability. Custom `IOperatorStore` implementations
can opt in through `ISubjectScopedOperatorStore`; otherwise the default desk throws `NotSupportedException`
for a scoped read rather than returning an incomplete unscoped page. Existing `IApprovalDesk` and
`IOperatorStore` methods are unchanged. Scoped reads neither grant approval nor change decisions.

### Audit relocation (ADR-0030 / ADR-0031)

`IAuditLog` and `AuditEntry` are **not** owned by Operator. Per the 2026-05-16 ADR-0030 D5 amendment
they live in `HoneyDrunk.Audit.Abstractions`; Operator is a *consumer* and emits `AuditEntry` for
every decision via `HoneyDrunk.Audit`'s `IAuditLog` (graceful no-op when Audit isn't composed).

## How to consume

```csharp
services
    .AddHoneyDrunkOperator()
    .AddSafetyRule<MyContentSafetyRule>();

// Optional: keep approvals, pauses and spend in a database (HoneyDrunk.Operator.Data).
services.AddOperatorDataStore(options => options.UseSqlServer(connectionString));

// Subscribe to approval-needed events by registering an IApprovalEventSink (event-out per D8).
services.AddSingleton<IApprovalEventSink, MyTransportApprovalSink>();
```

The host composes Vault (`IConfigProvider`), Kernel (`ITelemetryActivityFactory`), and — optionally —
the Audit Node (`IAuditLog`). See the runtime package README for host responsibilities.

## Invariants

- **116** — downstream Nodes depend only on `HoneyDrunk.Operator.Abstractions`.
- **117** — cost tables, breaker thresholds, decision policies, and safety-filter config come from App
  Configuration via Vault.
- **118** — approval notifications are event-out; no runtime dependency on Communications.
- **119** — CI carries a contract-shape canary over the entire `HoneyDrunk.Operator.Abstractions`
  public surface (all five contracts — `IApprovalGate`, `ICircuitBreaker`, `ICostGuard`,
  `IDecisionPolicy`, `ISafetyFilter` — and their supporting DTOs).

## Standup

Governed by [ADR-0018](https://github.com/HoneyDrunkStudios/HoneyDrunk.Studio/blob/main/adrs/ADR-0018-stand-up-honeydrunk-operator-node.md).
