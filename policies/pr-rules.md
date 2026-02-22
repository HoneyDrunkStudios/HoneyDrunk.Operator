# Operator — PR Rules

> Every PR is a signal. Make it count.

---

## PR Title Format

```
[Operator/JobName] target-repo — short description
```

- **JobName**: The Operator job that generated this PR (e.g., `DriftSentinel`, `DocFreshness`, `ContentForge`).
- **target-repo**: The repo being modified.

---

## PR Description — Required Sections

Every PR opened by Operator MUST include the following sections:

### 1. Evidence

| Field | Description |
|---|---|
| **Compared** | What was compared (e.g., Hive catalog vs. repo README) |
| **Mismatches** | Specific drifts or gaps found |
| **Source of Truth** | Reference to the Hive entry or contract used |

No evidence, no PR. Period.

### 2. Risk Level

One of: `LOW` | `MEDIUM` | `HIGH`

See [risk-classification.md](./risk-classification.md) for definitions and examples.

### 3. Proposed Actions

Bullet list of exact changes being made and why.

### 4. Rollback Notes

How to revert this change if it causes issues. Every PR must answer:
- What to revert (files, fields, values).
- How to revert (manual steps or linked rollback PR).
- Who to notify if rollback is needed.

### 5. Artifacts

List of files added, modified, or removed.

---

## Review Requirements

- **All PRs require human review.** No exceptions.
- **No auto-merge.** Operator proposes. Humans decide.
- **No self-approval.** Operator cannot approve its own PRs.

---

## Branch Rules

- Operator creates PRs from **feature branches only**.
- Never commits directly to `main` or any protected branch.
- Branch naming: `operator/<job-name>/<short-description>`

---

## PR Labels

Apply labels to keep the Grid scannable:

| Label | Use When |
|---|---|
| `operator-drift` | PR fixes a detected drift |
| `operator-doc` | PR updates documentation |
| `operator-content` | PR modifies content files |
| `risk-low` | Risk classified as LOW |
| `risk-medium` | Risk classified as MEDIUM |
| `risk-high` | Risk classified as HIGH |

---

## Guardrails

These are hard rules. Operator does not bend them.

1. **Operator never auto-merges.** Every PR waits for human approval.
2. **All PRs include evidence.** No blind changes.
3. **All PRs include risk classification.** No unclassified modifications hit the Grid.
4. **All PRs include rollback notes.** If it can't be undone, it doesn't ship.
