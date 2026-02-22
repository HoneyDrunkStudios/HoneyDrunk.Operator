# Operator — Signal Severity Rules

> **Layer:** Pulse (awareness/signal layer)
> **Applies to:** All Signals emitted by the Operator Node

---

## Overview

Not every Signal deserves a human's attention. Severity levels exist to separate the critical from the routine — and to keep the Grid quiet enough that when it *does* speak, operators listen.

---

## Severity Levels

### `info` — The Grid hums normally

Routine operations. Nothing demands human action.

- Drift detected but within expected tolerance.
- Run completed successfully with zero or low-risk findings.
- PR created for a `LOW`-risk change.
- Scheduled job finished on time, no anomalies.

> **Rule of thumb:** If no one needs to do anything, it's `info` at most.

---

### `warn` — Something wants eyes on it

The system is functioning, but conditions are trending toward a problem.

- Drift detected in a **critical field** (e.g., license, visibility, security policy).
- Doc freshness threshold exceeded — content is going stale.
- Multiple mismatches found in a single repo during one scan.
- Risk level `MEDIUM` on a proposed change.

> **Rule of thumb:** A human should review this soon, but the world isn't on fire.

---

### `error` — The circuit is broken

Something failed or was forcibly stopped. Action required.

- Policy blocked a proposed action (`Operator.PolicyBlocked` fired).
- Token scope insufficient to complete an operation.
- **Hive** catalog inaccessible — the truth registry is dark.
- Risk level `HIGH` on a proposed change.

> **Rule of thumb:** If the Operator can't do its job or a guardrail activated, it's `error`.

---

## Noise Reduction

Signals are only useful if they're scarce enough to matter. The following rules keep Pulse clean.

### Deduplication

Repeated identical drift detections **without a state change** must be deduplicated. If the same repo shows the same mismatch on consecutive runs, emit the Signal once and suppress subsequent duplicates until the state changes.

### Zero-finding runs

A run that scans repos and finds nothing should emit a single `info`-level `Operator.RunCompleted` Signal. It must **not** emit individual "no drift found" events per repo. Silence *is* the signal.

### Actionability filter

Before assigning severity, ask: *Does a human need to do something?*

| Human action needed? | Maximum severity |
|---|---|
| No | `info` |
| Review recommended | `warn` |
| Intervention required | `error` |

---

## Signal Volume Management

### Keep the channel clear

- **Batch over broadcast.** Aggregate findings per run rather than emitting per-repo Signals when possible.
- **Correlate, don't repeat.** Use `correlation_id` in the event envelope to group related Signals. Consumers can expand a run's details on demand instead of drowning in individual events.
- **Escalate, don't echo.** If the same condition persists across multiple runs, escalate severity once rather than repeating the same `warn` indefinitely.

### Avoiding alert fatigue

Alert fatigue kills response times. These guardrails exist to prevent it:

1. **No severity inflation.** A low-risk drift fix is `info`, not `warn`, regardless of how many repos it touches.
2. **Suppress known states.** If a drift is acknowledged or tracked in an issue, suppress further Signals for that specific mismatch.
3. **Cap volume per run.** If a single run produces more than a configurable threshold of Signals (default: 25), roll them into a single summary Signal with an attached manifest. The Grid should whisper, not scream.

---

## Quick Reference

| Event | Typical Severity | Escalation Trigger |
|---|---|---|
| `Operator.DriftDetected` | `info` | Critical field → `warn` |
| `Operator.ActionProposed` | `info` | Risk `MEDIUM` → `warn`, `HIGH` → `error` |
| `Operator.PRCreated` | `info` | Risk `MEDIUM` → `warn`, `HIGH` → `error` |
| `Operator.PolicyBlocked` | `error` | — (always `error`) |
| `Operator.RunCompleted` | `info` | Errors present → `error` |
