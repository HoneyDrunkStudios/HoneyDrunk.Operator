# Operator — Pulse Event Taxonomy

> **Status:** Design only — not implemented. Targeted for post-v1.
> **Layer:** Pulse (awareness/signal layer)

---

## Overview

Every action the Operator takes — or decides *not* to take — emits a **Signal** to the **Pulse** layer. Signals are the nervous system of the Grid: they let Nodes observe, react, and coordinate without tight coupling.

This document defines the minimal event taxonomy for Operator Pulse signals.

---

## Event Catalog

### 1. `Operator.DriftDetected`

Emitted when Drift Sentinel finds a mismatch between repo state and the **Hive** (truth registry) catalog.

| Payload Field | Type | Description |
|---|---|---|
| `repo` | string | Repository name where drift was detected |
| `fields_mismatched` | string[] | Field(s) that diverged from Hive truth |
| `expected` | object | Expected values per the Hive catalog |
| `actual` | object | Actual values found in the repo |
| `timestamp` | ISO 8601 | When the drift was detected |

---

### 2. `Operator.ActionProposed`

Emitted when the Operator decides to propose a change — *before* PR creation. This is the intent signal; the action has not yet been executed.

| Payload Field | Type | Description |
|---|---|---|
| `action_type` | enum | One of: `drift_fix`, `doc_update`, `content_draft` |
| `target_repo` | string | Repository the action targets |
| `risk_level` | enum | `LOW`, `MEDIUM`, `HIGH` |
| `evidence_summary` | string | Brief rationale for the proposed action |

---

### 3. `Operator.PRCreated`

Emitted after the Operator successfully creates a pull request.

| Payload Field | Type | Description |
|---|---|---|
| `pr_url` | string | Full URL of the created PR |
| `target_repo` | string | Repository the PR was opened against |
| `risk_level` | enum | `LOW`, `MEDIUM`, `HIGH` |
| `triggered_by` | string | Job or workflow that initiated the PR |
| `artifacts` | string[] | List of artifacts included in or referenced by the PR |

---

### 4. `Operator.PolicyBlocked`

Emitted when a proposed action is blocked by policy — denylist, allowlist, or risk threshold.

| Payload Field | Type | Description |
|---|---|---|
| `blocked_action` | string | Description of the action that was rejected |
| `policy_rule` | string | Identifier of the policy rule that triggered the block |
| `target_repo` | string | Repository the action would have targeted |
| `reason` | string | Human-readable explanation of why the action was blocked |

---

### 5. `Operator.RunCompleted`

Emitted at the end of every Operator run, regardless of outcome.

| Payload Field | Type | Description |
|---|---|---|
| `job_name` | string | Name of the completed job |
| `duration_ms` | integer | Total run duration in milliseconds |
| `actions_proposed` | integer | Count of actions proposed during this run |
| `actions_blocked` | integer | Count of actions blocked by policy |
| `errors` | string[] | Error messages, if any. Empty array on clean runs |

---

## Event Envelope Format

Every Signal transmitted to Pulse is wrapped in a standard envelope. This keeps consumers decoupled from payload internals.

```json
{
  "event_type": "Operator.DriftDetected",
  "source_node": "operator-prime",
  "timestamp": "2025-07-14T03:22:41Z",
  "correlation_id": "run-a7c9e3f1-0042",
  "payload": { }
}
```

| Envelope Field | Type | Description |
|---|---|---|
| `event_type` | string | Fully qualified event name from the catalog above |
| `source_node` | string | Identity of the Node in the Grid that emitted the Signal |
| `timestamp` | ISO 8601 | Emission time in UTC |
| `correlation_id` | string | Groups Signals from the same run or workflow for tracing |
| `payload` | object | Event-specific data as defined per event type |

---

## Notes

- This taxonomy is **design-only for v1**. Implementation will follow once the Pulse transport layer is defined.
- Envelope schema may evolve. Consumers should tolerate unknown fields.
- All timestamps are UTC. No exceptions.
