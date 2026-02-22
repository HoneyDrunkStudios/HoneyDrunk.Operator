# Operator Post-Merge Workflow — Placeholder

> **This is a design document, not a workflow file.** The actual YAML will be created when runtime implementation begins.

---

## Overview

This document describes the intended GitHub Actions workflow that Operator will execute after changes are merged to `main`. It defines the trigger, steps, and environment requirements for a full governance run.

---

## Trigger

```
on:
  push:
    branches: [main]
```

The workflow fires on every push to `main`, which in practice means every merged PR (since direct pushes to `main` are prohibited by convention).

---

## Steps (Planned)

| #  | Step                  | Description                                                        |
|----|-----------------------|--------------------------------------------------------------------|
| 1  | **Checkout repo**     | Clone the current repository at the merge commit.                  |
| 2  | **Load policies**     | Read allowlist, denylist, and risk threshold policies from `policies/`. |
| 3  | **Fetch Hive catalog**| Pull the current Hive catalog JSON from the Hive repository.       |
| 4  | **Run Drift Sentinel**| Compare repo state against Hive catalog entries. Generate drift findings. |
| 5  | **Run Doc Freshness** | Check documentation age and completeness against thresholds.       |
| 6  | **Run Content Forge** | If applicable, generate or refresh documentation using LLM prompts. |
| 7  | **Create PRs**        | For each actionable finding, open a PR with evidence and change summary. |
| 8  | **Emit Pulse Signals**| Send Signals to the Pulse layer reporting what Operator found and did. |

---

## Environment Variables (Required)

| Variable            | Description                                                  |
|---------------------|--------------------------------------------------------------|
| `HIVE_CATALOG_URL`  | URL to fetch the current Hive catalog JSON.                  |
| `OPERATOR_TOKEN`    | GitHub token with permissions to read repos and create PRs.  |
| `PULSE_ENDPOINT`    | Endpoint for emitting Signals to the Pulse awareness layer.  |

These variables must be configured as GitHub Actions secrets or environment variables before the workflow can run.

---

## Permissions

The workflow requires:

- `contents: read` — to checkout and inspect repositories.
- `pull-requests: write` — to create PRs with proposed changes.
- `issues: write` — to open issues for non-auto-correctable findings.

---

## Design Notes

- Each step is designed to be idempotent. Re-running the workflow on the same commit should produce the same findings.
- The workflow should complete within a reasonable time window. Long-running LLM operations (Content Forge) should have timeouts.
- Failures in one step should not block subsequent steps. Each module operates independently and logs its own status.
- Staging artifacts are written to `staging/` and referenced in PR descriptions.

---

## Status

**Not implemented.** This placeholder will be replaced by a working YAML workflow file when the following prerequisites are met:

1. Hive catalog schema is finalized and a catalog repository exists.
2. Drift Sentinel logic is implemented (as a script or action).
3. Pulse endpoint is available for Signal emission.
4. Operator token and secrets are configured in the repository.

---

> **Operator Note**: Design first, wire second. This document is the blueprint. The YAML is the circuit.
