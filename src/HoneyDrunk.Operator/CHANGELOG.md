# Changelog — HoneyDrunk.Operator

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

### Deferred

- Auto-trip on failure count (`OperatorOptions.DefaultBreakerFailureThreshold`) — reserved; requires
  a failure-reporting API on `ICircuitBreaker` not yet defined.

- HoneyDrunk.Auth delegation in `AuthBackedDecisionPolicy` (ADR-0018 D5) — `TODO(auth)`.
- Durable persistence of cost/approval state via HoneyDrunk.Data (ADR-0018 D12) — `TODO(data)`.
