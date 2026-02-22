---
name: Drift Report
about: Report drift between repo state and Hive catalog
title: "[DRIFT] "
labels: drift, operator
assignees: ''
---

## Node / Repo

**Name**: _e.g., HoneyDrunk.Core_
**Repo URL**: _e.g., https://github.com/HoneyDrunkStudios/HoneyDrunk.Core_

## Fields Affected

_Which metadata or structural fields are out of sync?_

- [ ] `status`
- [ ] `description`
- [ ] `sector`
- [ ] `topics`
- [ ] `last_verified`
- [ ] README content
- [ ] Directory structure (`docs/`, `contracts/`, `policies/`)
- [ ] Other: _specify_

## Expected vs. Actual

| Field        | Hive (Expected) | Repo (Actual) |
|------------- |-----------------|---------------|
| _field name_ | _value_         | _value_       |
| _field name_ | _value_         | _value_       |

## Severity

- [ ] **Low** — cosmetic or minor metadata mismatch
- [ ] **Medium** — structural gap or outdated information
- [ ] **High** — status inconsistency or missing critical elements

## Evidence

_How was this drift detected? Link to Drift Sentinel output, manual inspection, or Pulse Signal._

## Proposed Resolution

_What should be done to resolve the drift? Auto-correctable by Operator, or requires human intervention?_
