# Operator — Risk Classification

> Know the blast radius before you pull the trigger.

---

## Risk Levels

### LOW

Minimal impact. No behavioral changes to the Grid.

- Metadata-only changes.
- README updates (typos, formatting, non-structural).
- Non-breaking doc corrections.
- Hive catalog field additions (new optional fields only).

**Example:** Updating a repo description in the Hive catalog.

---

### MEDIUM

Visible impact. Changes affect public-facing content or Node state.

- Content changes to public-facing documentation.
- Status field changes (e.g., Wiring → Live).
- Changes to multiple files in a single PR.
- Pulse signal metadata updates.

**Example:** Proposing a status transition for a Node in the Grid.

---

### HIGH

Structural impact. Changes could ripple across Nodes or alter trust boundaries.

- Changes to security-related files.
- Changes to contracts or policy files.
- Any change that could affect other Nodes' behavior.
- Changes to Hive catalog structure or schema.
- Modifications to the Pulse layer configuration.

**Example:** Modifying the Hive catalog schema or updating security-model.md.

---

## Decision Matrix

| Change Type | Scope | Risk Level |
|---|---|---|
| README typo fix | Single file, cosmetic | LOW |
| New optional Hive field | Catalog metadata | LOW |
| Doc content rewrite | Public-facing docs | MEDIUM |
| Status transition proposal | Node state change | MEDIUM |
| Multi-file doc update | Multiple files | MEDIUM |
| Contract modification | Cross-Node behavior | HIGH |
| Policy file edit | Governance rules | HIGH |
| Hive schema change | Catalog structure | HIGH |
| Security doc update | Trust boundaries | HIGH |

---

## Escalation Guidance

Escalate to the next risk level when:

| From | To | Trigger |
|---|---|---|
| LOW | MEDIUM | Change touches more than one file, or modifies any field that is displayed publicly. |
| MEDIUM | HIGH | Change affects contracts, policies, security docs, or Hive schema. Change could alter how other Nodes operate or interpret data. |

**When in doubt, escalate.** A MEDIUM classified as HIGH costs a review cycle. A HIGH classified as LOW costs trust.

---

## Classification Responsibility

- Operator assigns initial risk level based on these rules.
- Human reviewer validates or overrides the classification.
- If a reviewer escalates risk, Operator logs the override for future calibration.
