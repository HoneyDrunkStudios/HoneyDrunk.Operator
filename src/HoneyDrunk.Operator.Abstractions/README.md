# HoneyDrunk.Operator.Abstractions

Dependency-light contracts for **HoneyDrunk.Operator** — the AI sector's human-policy enforcement substrate (ADR-0018).

Downstream Nodes compile against this package only (invariant 116). Runtime composition lives in `HoneyDrunk.Operator`; in-memory fixtures live in `HoneyDrunk.Operator.Testing`.

## Contracts

| Contract | Kind | Purpose |
|---|---|---|
| `IApprovalGate` | interface | Require human sign-off before a constrained action proceeds. |
| `ICircuitBreaker` | interface | Halt agent or workflow execution when thresholds are breached. |
| `ICostGuard` | interface | Enforce per-scope cost limits over a rolling window. |
| `IDecisionPolicy` | interface | Allow / deny / require-approval evaluation of an action. |
| `ISafetyFilter` | interface | Validate content against a safety rule set. |
| `ApprovalRequest` | record | A request for human approval. |
| `ApprovalDecision` | record | The decision recorded against an approval request. |
| `CostEvent` | record | A single recorded unit of attributable spend. |

## Audit relocation (ADR-0030 / ADR-0031)

`IAuditLog` and `AuditEntry` are **not** owned here. They live in `HoneyDrunk.Audit.Abstractions`
per the 2026-05-16 ADR-0030 D5 amendment. Operator is a *consumer* of `IAuditLog`, not its owner.

## Invariants

- Carries **zero** `HoneyDrunk.*` runtime references (invariant 1) — only `HoneyDrunk.Standards`.
- All record types drop the `I` prefix and use `kind: "type"` in the Grid catalog; interfaces keep it.
