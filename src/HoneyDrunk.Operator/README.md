# HoneyDrunk.Operator

Runtime composition for the **HoneyDrunk.Operator** human-policy enforcement substrate (ADR-0018).

```csharp
services
    .AddHoneyDrunkOperator(options =>
    {
        options.ConfigKeyPrefix = "HoneyDrunk:Operator";
    })
    .AddSafetyRule<MyCustomSafetyRule>();
```

## What it registers

| Contract | Default implementation |
|---|---|
| `IApprovalGate` | `DefaultApprovalGate` (event-out + in-process pending state) |
| `ICircuitBreaker` | `DefaultCircuitBreaker` (Closed → Open → HalfOpen state machine) |
| `ICostGuard` | `DefaultCostGuard` (in-process accumulator, config-sourced budgets) |
| `IDecisionPolicy` | `AuthBackedDecisionPolicy` (config-sourced outcomes) |
| `ISafetyFilter` | `DefaultSafetyFilter` (composable `ISafetyRule` chain) |

Plus `OperatorTelemetry` (Kernel `ITelemetryActivityFactory`), `OperatorAuditWriter` (Audit
`IAuditLog`), and `ApprovalEventEmitter` (`IApprovalEventSink` event-out).

## Host responsibilities

- **Vault** — register an `IConfigProvider` so cost-rate tables, breaker thresholds, decision
  policies, and safety-filter config resolve from Azure App Configuration (ADR-0018 D6 / invariant 117).
- **Kernel** — register an `ITelemetryActivityFactory` (ADR-0018 D7).
- **Audit** — register an `IAuditLog` from `HoneyDrunk.Audit` to capture decisions (ADR-0030 / ADR-0031).
  When absent, audit emission is a no-op (graceful degradation). Entries for changes wait in the store's outbox
  until delivered; run the host as an `IHost` so `OperatorAuditDeliveryService` retries any that failed.
- **Approval transport** — register an `IApprovalEventSink` to deliver approval-needed events; the
  default is a no-op sink. The wire shape is deferred per ADR-0018 D8.

## Standards notes

- Per invariants 1/2, the runtime references peer Nodes' contract surfaces, never their runtime
  composition: `HoneyDrunk.Kernel.Abstractions` and `HoneyDrunk.Audit.Abstractions`. Vault does not
  yet publish a standalone `.Abstractions` package, so `IConfigProvider` (namespace
  `HoneyDrunk.Vault.Abstractions`) is consumed from the `HoneyDrunk.Vault` package — the contract
  type only; no Vault runtime services are composed here.
- **v0.1.0 deferrals:** the `AuthBackedDecisionPolicy` → HoneyDrunk.Auth delegation (ADR-0018 D5) and
  durable persistence of cost/approval state (HoneyDrunk.Data, ADR-0018 D12) were deferred from v0.1.0.
  Since 0.2.0, state lives behind `IOperatorStore`: in memory by default, or durable with
  `HoneyDrunk.Operator.Data`. The Auth delegation is still marked `TODO(auth)`.
