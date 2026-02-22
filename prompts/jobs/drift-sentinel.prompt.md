# Job Prompt — Drift Sentinel

> Detect drift between the Hive catalog and actual repo state. Produce an
> evidence-backed drift report and propose corrections via PR.

---

## Job Identity

- **Job Name**: Drift Sentinel
- **Agent**: Operator
- **Type**: Deterministic audit
- **Cadence**: On-demand or scheduled
- **Output**: Drift report + optional correction PR

---

## Input Requirements

You must receive all of the following before executing:

| Input | Description |
|---|---|
| **Hive catalog entry** | The canonical JSON object for the target Node from the Hive catalog. |
| **Repo file listing** | Complete list of files and directories in the target repo's default branch. |
| **README content** | Full text of the repo's README.md (or flag if missing). |
| **Repo metadata** | GitHub API metadata: description, topics, visibility, default branch, license. |
| **Policy files** | Loaded contents of: path-denylist.md, repo-allowlist.template.json, pr-rules.md, risk-classification.md. |

If any required input is missing or unparseable, halt and emit an error Signal
to Pulse. Do not proceed with partial data.

---

## Task

Compare each field in the Hive catalog entry against the actual repo state.
Check for mismatches in the following fields:

### Required Comparisons

| Field | Source: Hive Catalog | Source: Repo State |
|---|---|---|
| Repo name | `name` | GitHub API `name` |
| Description | `description` | GitHub API `description` |
| Status | `status` | Inferred from repo structure + Hive value |
| Topics | `topics[]` | GitHub API `topics[]` |
| Visibility | `visibility` | GitHub API `visibility` |
| Required files | `required_files[]` | File listing presence check |
| Doc structure | `docs.structure` | Actual `/docs` directory layout |
| Sector membership | `sector` | Cross-reference with Hive Sector registry |
| License | `license` | LICENSE file presence and content match |
| Contract presence | `contracts` | `/contracts` directory listing |

### Detection Logic

For each field:

1. Extract the expected value from the Hive catalog entry.
2. Extract the actual value from the repo state.
3. Compare using exact match for strings, set equality for arrays, structural
   comparison for nested objects.
4. If mismatched, record a finding with severity classification.

---

## Evidence List

For each mismatch found, record a structured finding:

```markdown
### Finding: {Field Name}

- **Expected** (Hive): {value from catalog}
- **Actual** (Repo): {value from repo state}
- **Severity**: {INFO | WARN | ERROR}
- **Details**: {Human-readable explanation of the discrepancy.}
```

### Severity Classification

| Severity | Criteria |
|---|---|
| **INFO** | Cosmetic or non-functional differences (e.g., topic order). |
| **WARN** | Meaningful drift that should be corrected (e.g., description mismatch, missing optional file). |
| **ERROR** | Critical drift that indicates broken state (e.g., missing required file, status contradiction, visibility mismatch). |

---

## Risk Classification

Assign an overall risk level based on the aggregate findings:

| Risk Level | Criteria |
|---|---|
| **LOW** | Only INFO findings, or 1–2 WARN findings with no ERROR. |
| **MEDIUM** | Multiple WARN findings, or 1 ERROR finding. |
| **HIGH** | Multiple ERROR findings, or any finding involving security-relevant fields (visibility, secrets exposure, CI/CD). |

Reference `/policies/risk-classification.md` for detailed guidance.

---

## Proposed Actions

For each mismatch, propose exactly one of the following actions:

| Action | When to use |
|---|---|
| **Update Catalog** | Repo state is correct and the Hive entry is stale. |
| **Update Repo** | Hive catalog is correct and the repo has drifted. |
| **Flag for Human Review** | Ambiguous — both sources may be partially correct, or the change has security implications. |

Format each proposed action as:

```markdown
- **{Field Name}**: {Action} — {Brief justification}.
```

---

## Output Artifacts

Write the drift report to `/staging` using the following path convention:

```
/staging/drift-report-{repo-name}-{YYYY-MM-DD}.md
```

The report must follow the format defined in
`/prompts/templates/drift-report.template.md`.

If corrections are proposed, prepare the modified files in `/staging` for
inclusion in the PR.

---

## PR Creation

If any proposed action is "Update Catalog" or "Update Repo":

1. Create a branch: `operator/drift-sentinel/{repo-name}-{date}`
2. Commit the drift report and any proposed file changes to the branch.
3. Open a PR following `/policies/pr-rules.md`:
   - Title: `[Operator/DriftSentinel] Drift corrections — {repo-name}`
   - Include the full drift report in the PR description.
   - Apply labels: `operator-drift`, `risk-{level}`.
   - Add rollback notes.
4. Never auto-merge. Require human review.

If all proposed actions are "Flag for Human Review," do not create a PR. Instead,
write the drift report to `/staging` and emit a Signal to Pulse for human
attention.

---

## Guardrails

- Respect `/policies/path-denylist.md`. Skip any file matching denylist
  patterns during comparison. Do not propose changes to denylisted paths.
- Only operate on repos listed in `/policies/repo-allowlist.template.json`.
  Verify the repo is allowlisted before starting. Check that `drift-fix` is
  in the repo's `scopes` array.
- Never commit directly to `main` or protected branches.
- Include rollback notes in every PR: how to revert, what state to restore.
- If the drift report exceeds 50 findings, flag for human review regardless of
  individual severities. Something systemic may be wrong.
