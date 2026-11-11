# Changelog — HoneyDrunk.Operator.Abstractions

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2026-11-11

### Added

- Added `Approvals.PendingApprovalQuery` and the optional `ISubjectScopedApprovalDesk` capability. Queries copy their subject scope, accept at most 100 subjects and 100 results, and treat an empty scope as no matches.

## [0.2.0] - 2026-11-08

### Added

- `IApprovalDesk`, `IAgentPauses`, `PauseState`, `PendingApproval` and `CostWindows.Month`.

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

- Initial contract surface for ADR-0018: `IApprovalGate`, `ICircuitBreaker`, `ICostGuard`,
  `IDecisionPolicy`, `ISafetyFilter` (interfaces) and `ApprovalRequest`, `ApprovalDecision`,
  `CostEvent` (records), plus supporting records and enums (`CostCheckResult`, `CostStatus`,
  `ActionContext`, `PolicyDecision`, `PolicyOutcome`, `SafetyFilterRequest`, `SafetyFilterResult`,
  `BreakerState`, `ApprovalOutcome`).
- `ICostGuard` is the renamed successor to the seed-era `ICostController`.
- `IAuditLog` / `AuditEntry` are intentionally **not** declared here — relocated to
  `HoneyDrunk.Audit.Abstractions` per ADR-0030 D5 / ADR-0031.
