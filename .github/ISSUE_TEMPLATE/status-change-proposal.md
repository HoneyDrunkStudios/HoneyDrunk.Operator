---
name: Status Change Proposal
about: Propose a Node status transition in the Hive catalog
title: "[STATUS] "
labels: status-change, operator
assignees: ''
---

## Target Node

**Name**: _e.g., HoneyDrunk.Core_
**Repo URL**: _e.g., https://github.com/HoneyDrunkStudios/HoneyDrunk.Core_

## Status Transition

| Field              | Value      |
|--------------------|------------|
| **Current Status** | _Awake / Wiring / Live_ |
| **Proposed Status**| _Awake / Wiring / Live_ |

## Evidence / Justification

_Why is this transition warranted? Reference specific conditions, completed work, or contract compliance._

### Checklist (for transitions to the proposed status)

**Awake → Wiring:**
- [ ] `README.md` exists with purpose statement
- [ ] `docs/` directory created
- [ ] Hive catalog entry is populated with all required fields

**Wiring → Live:**
- [ ] All required and recommended structures are in place
- [ ] Metadata in Hive is current and verified
- [ ] Signals are enabled (if applicable)
- [ ] No outstanding drift findings

**Downgrade (any → lower status):**
- [ ] Justification documented
- [ ] Impact on dependent Nodes assessed
- [ ] Notification plan for affected Sectors

## Risk Level

- [ ] **Low** — expected progression, all criteria met
- [ ] **Medium** — some criteria are borderline or recently met
- [ ] **High** — exceptional circumstance, requires discussion

## Additional Context

_Any supporting information, related PRs, or Operator reports that inform this proposal._
