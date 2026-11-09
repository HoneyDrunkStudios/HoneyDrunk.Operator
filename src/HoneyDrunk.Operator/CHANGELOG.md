# Changelog — HoneyDrunk.Operator

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-11-08

### Added

- `DefaultApprovalDesk` records a person's approve or deny once, by compare-and-set, and lists pending requests.
- `DefaultAgentPauses` pauses and resumes named breakers; pauses override every breaker state.
- The cost guard pauses an agent once when its monthly spend reaches its budget, never replacing a pause already in place. The charge, the pause and their audit entries are saved in one store write, so a charge reaching the limit is never stored without its stop.
- A scope's standing budget, `Budget:{scope}`, applies to every window that has no `Budget:{scope}:{window}` of its own.
- A pending approval asked for again is announced again, so a failed announcement is not final.
- `IOperatorStore` and the in-memory default.
- An audit outbox. Every request, decision, expiry, pause, resume, charge and budget stop is saved with its audit entry in one store write, and `OperatorAuditOutbox` delivers the entries to the audit sinks, oldest first, right after the change. Each entry is claimed for one minute, so hosts sharing a store deliver it once. A failing sink no longer fails the change: the entry stays in the outbox and `OperatorAuditDeliveryService`, a hosted service, retries it every `AuditDeliveryIntervalSeconds` (30 by default). Delivery is at least once. An entry the sink refuses does not hold back the ones after it; a pass stops after three failures in a row. Audit text longer than the outbox columns (reason 2000, correlation id 200 characters) is cut to fit, never refused.

### Changed

- Constructors: `DefaultApprovalGate` takes `IOperatorStore` and `OperatorAuditOutbox` in place of `OperatorAuditWriter`; `DefaultCircuitBreaker` takes `IAgentPauses`; `DefaultCostGuard` takes `IOperatorStore`, `OperatorAuditOutbox` and `TimeProvider`.
- `AddHoneyDrunkOperator` also registers logging, `OperatorAuditOutbox` and the `OperatorAuditDeliveryService` hosted service.
- `DefaultCostGuard.RecordAsync` refuses an agent id longer than 194 characters, so its pause name fits.
- `DefaultCostGuard.RecordAsync` reads the budget limit before storing the charge, so a configuration failure stores nothing and a retry charges once.
- An approval id reused for a different request is refused instead of returning the earlier decision.

## [0.1.2] - 2026-11-05

### Changed

- Refresh NuGet dependencies and adopt upstream package releases in dependency order. Preserve target frameworks and public contracts.

| Dependency | Previous | Updated |
| --- | --- | --- |
| HoneyDrunk.Audit.Abstractions | 0.2.1 | 0.2.2 |
| HoneyDrunk.Kernel.Abstractions | 0.8.1 | 0.8.2 |
| HoneyDrunk.Standards | 0.3.0 | 0.4.3 |
| HoneyDrunk.Standards.Tests | 0.3.0 | 0.4.3 |
| HoneyDrunk.Vault | 0.8.1 | 0.8.2 |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Logging.Abstractions | 10.0.12 | 10.0.14 |
| Microsoft.Extensions.Options | 10.0.12 | 10.0.14 |

## [0.1.1] - 2026-09-26

### Changed

- Refresh dependency and shared build-tooling versions; preserve target frameworks and existing public contracts. See the [repository dependency changes](../../CHANGELOG.md).

## [0.1.0] - 2026-06-09

### Added

- `AddHoneyDrunkOperator()` registering the five Operator-owned contracts.
- Default implementations: `DefaultApprovalGate`, `DefaultCircuitBreaker`, `DefaultCostGuard`,
  `AuthBackedDecisionPolicy`, `DefaultSafetyFilter`.
- `OperatorTelemetry` (Kernel `ITelemetryActivityFactory`, one-way to Pulse per ADR-0018 D7).
- `OperatorAuditWriter` emitting `AuditEntry` to `HoneyDrunk.Audit`'s `IAuditLog` per ADR-0030/0031;
  degrades to a no-op when no audit sink is composed.
- `ApprovalEventEmitter` + `IApprovalEventSink` event-out for approvals per ADR-0018 D8 (no
  Communications runtime edge — invariant 118).
- Composable `ISafetyRule` chain via `AddSafetyRule<T>()`.
- Non-negative cost enforcement: `DefaultCostGuard.CheckBudgetAsync`/`RecordAsync` reject negative
  amounts and clamp reported remaining budget to zero, so spend cannot be driven under a limit.
- Approval expiry: `DefaultApprovalGate.CheckStatusAsync` ages a pending request to
  `ApprovalOutcome.Expired` once `ApprovalRequest.Expiry` has passed, instead of returning `Pending`
  indefinitely.
- Bounded half-open probing: `DefaultCircuitBreaker` admits only `HalfOpenTrialCount` trial calls in
  the `HalfOpen` state (config-sourced; fallback `OperatorOptions.DefaultBreakerHalfOpenTrialCount`),
  then denies until the probe is resolved via `ResetAsync` or `TripAsync`.
- Config validation: negative configured budget limits and negative breaker reset-window / half-open
  trial counts are rejected and fall back to defaults, so a bad or tampered config value can't
  silently disable enforcement. `GetStatusAsync` clamps reported remaining budget to zero.
- Telemetry consistency: `DefaultCostGuard.GetStatusAsync`, `DefaultCircuitBreaker.GetStateAsync`, and
  `DefaultApprovalGate.CheckStatusAsync` now open telemetry activities like their sibling operations;
  `CheckStatusAsync` also persists the Expired transition once instead of recomputing it per poll.
- Clock policy: `DefaultApprovalGate` (decision stamping, expiry aging) and `DefaultCircuitBreaker`
  (reset-window timing) read time via an injected `TimeProvider` instead of `DateTimeOffset.UtcNow`.
  `AddHoneyDrunkOperator()` registers `TimeProvider.System` (TryAdd, so a host-supplied clock wins),
  making breaker recovery and approval expiry deterministically testable.

### Deferred

- Auto-trip on failure count (`OperatorOptions.DefaultBreakerFailureThreshold`) — reserved; requires
  a failure-reporting API on `ICircuitBreaker` not yet defined.

- HoneyDrunk.Auth delegation in `AuthBackedDecisionPolicy` (ADR-0018 D5) — `TODO(auth)`.
- Durable persistence of cost/approval state via HoneyDrunk.Data (ADR-0018 D12) — `TODO(data)`.
