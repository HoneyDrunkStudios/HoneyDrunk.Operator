# Changelog — HoneyDrunk.Operator

All notable changes to this repository are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
