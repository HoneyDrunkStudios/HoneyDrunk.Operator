# Staging Area

> Operator's workspace. Drafts land here before they become proposals.

---

## Purpose

This folder is where Operator deposits generated artifacts during governance runs — drift reports, document update drafts, change summaries, and status proposals. Everything here is **transient**. Contents are created during job execution and referenced in PR descriptions or issue bodies.

---

## Rules

1. **Do not commit production content here.** Staging is a workspace, not an archive.
2. **Contents are ephemeral.** Files may be created, referenced, and cleaned up within a single workflow cycle.
3. **PRs reference staging artifacts.** When Operator opens a PR, it links to the relevant staging files as evidence.
4. **Manual edits are discouraged.** Let Operator manage this directory. If you need to place something here manually, follow the naming convention below.

---

## Naming Convention

Files in staging follow this pattern:

```
{type}-{target}-{date}.md
```

| Segment   | Description                                    | Example                    |
|-----------|------------------------------------------------|----------------------------|
| `type`    | Kind of artifact                               | `drift-report`, `doc-draft`, `change-summary`, `status-proposal` |
| `target`  | Node or repo the artifact relates to           | `HoneyDrunk.Core`, `HoneyDrunk.Operator` |
| `date`    | ISO date of generation                         | `2025-01-15`               |

**Example filenames:**

- `drift-report-HoneyDrunk.Core-2025-01-15.md`
- `change-summary-HoneyDrunk.Operator-2025-01-20.md`
- `status-proposal-HoneyDrunk.Pulse-2025-02-01.md`
- `doc-draft-HoneyDrunk.Hive-2025-02-10.md`

---

## What Goes Here

| Artifact Type       | Description                                                     |
|---------------------|-----------------------------------------------------------------|
| **Drift Reports**   | Findings from Drift Sentinel comparing repo state to Hive catalog. |
| **Doc Drafts**      | Generated or refreshed documentation before it is proposed via PR. |
| **Change Summaries**| Public-facing summaries of what changed, why, and what it affects. |
| **Status Proposals**| Evidence packages for Node status transitions (e.g., Wiring → Live). |

---

> **Operator Note**: If this directory is empty, that is normal. It means no governance run has pending output.
