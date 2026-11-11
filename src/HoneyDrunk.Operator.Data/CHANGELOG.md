# Changelog — HoneyDrunk.Operator.Data

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.3.0] - 2026-11-11

### Added

- Added scoped pending approval reads through `ApprovalDataService` and `OperatorDataStore`. Subject, pending and expiry filters, ordering and the result limit execute in SQL before entity materialization. No schema change is required.

## [0.2.0] - 2026-11-08

### Added

- First release: `AddOperatorDataStore` registers a durable `IOperatorStore` over HoneyDrunk.Data.EntityFramework, built from `ApprovalDataService`, `PauseDataService`, `SpendDataService` and `PendingAuditDataService`. Each change is saved with its audit entries in one transaction (`operator.PendingAudits`), and a charge reaching its budget is saved with its pause. A delivered entry is marked and kept for a day (`PendingAuditDataService.DeliveredRetention`) as the proof that its change committed, so a retried write whose commit reply was lost is never applied twice, then removed.
