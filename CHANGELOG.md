# Changelog — HoneyDrunk.Operator

## [0.1.1] - 2026-09-26

### Changed

- Refresh stable NuGet dependencies; preserve target frameworks and HoneyDrunk public contracts.

| Dependency | Previous | Updated |
| --- | --- | --- |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.8 | 10.0.12 |
| Microsoft.Extensions.Hosting.Abstractions | 10.0.8 | 10.0.12 |
| Microsoft.Extensions.Logging.Abstractions | 10.0.8 | 10.0.12 |
| Microsoft.Extensions.Options | 10.0.8 | 10.0.12 |
| NSubstitute | 5.3.0 | 6.2.0 |


All notable changes to this repository are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).




### Verified HoneyDrunk dependencies

- HoneyDrunk.Audit.Abstractions: 0.1.0 -> 0.2.1 (verified on NuGet.org).
- HoneyDrunk.Kernel.Abstractions: 0.8.0 -> 0.8.1 (verified on NuGet.org).
- HoneyDrunk.Standards: 0.2.9 -> 0.3.0 (verified on NuGet.org).
- HoneyDrunk.Standards.Tests: 0.2.9 -> 0.3.0 (verified on NuGet.org).
- HoneyDrunk.Vault: 0.5.0 -> 0.8.1 (verified on NuGet.org).

## [0.1.0] - 2026-06-09

### Added

- Stood up the `HoneyDrunk.Operator` Node per ADR-0018: solution, three packages
  (`Abstractions`, runtime, `Testing`), the eight Operator-owned D3 contracts, default runtime
  implementations, in-memory fixtures, full CI pipeline, and the contract-shape canary scoped to
  `HoneyDrunk.Operator.Abstractions`.
- Audit emission wired against `HoneyDrunk.Audit.Abstractions`' `IAuditLog` per the ADR-0030/0031
  relocation (Operator consumes, does not own, the audit contract).

### Notes

- The repository README previously described a drift-detection / PR-automation node; this scaffold
  establishes the canonical ADR-0018 policy-enforcement identity recorded in the Grid catalog.
- `AuthBackedDecisionPolicy` → HoneyDrunk.Auth delegation (D5) and durable cost/approval persistence
  via HoneyDrunk.Data (D12) are config-sourced/in-process at v0.1.0 and carry `TODO(auth)` /
  `TODO(data)` markers for follow-up packets.
