# Changelog — HoneyDrunk.Operator.Abstractions

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-06-09

### Added

- Initial contract surface for ADR-0018: `IApprovalGate`, `ICircuitBreaker`, `ICostGuard`,
  `IDecisionPolicy`, `ISafetyFilter` (interfaces) and `ApprovalRequest`, `ApprovalDecision`,
  `CostEvent` (records), plus supporting records and enums (`CostCheckResult`, `CostStatus`,
  `ActionContext`, `PolicyDecision`, `PolicyOutcome`, `SafetyFilterRequest`, `SafetyFilterResult`,
  `BreakerState`, `ApprovalOutcome`).
- `ICostGuard` is the renamed successor to the seed-era `ICostController`.
- `IAuditLog` / `AuditEntry` are intentionally **not** declared here — relocated to
  `HoneyDrunk.Audit.Abstractions` per ADR-0030 D5 / ADR-0031.
