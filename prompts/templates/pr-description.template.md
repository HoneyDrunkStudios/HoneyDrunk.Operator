# PR Description Template

> Standard template for all Operator-generated pull requests.
> Fill every section. Do not omit sections — mark as "N/A" if not applicable.

---

## Title Format

```
[Operator/{JobName}] {Brief description} — {TargetRepo}
```

**Examples:**
- `[Operator/Drift Sentinel] Drift corrections — HoneyDrunk.Hive`
- `[Operator/Doc Freshness] Doc updates — HoneyDrunk.Pulse`
- `[Operator/Content Forge] Change summary — HoneyDrunk.Operator`

---

## PR Body

```markdown
## Summary

{One to three sentences describing what was detected or requested, and what
this PR proposes to do about it.}

**Job**: {Drift Sentinel | Doc Freshness | Content Forge}
**Target**: {owner/repo-name}
**Node**: {Node name from Hive catalog}
**Triggered by**: {Scheduled scan | Request packet from Buzz | Manual dispatch}

---

## Evidence

{Detailed findings that justify this PR. Include field-level comparisons,
file-level observations, or source references as appropriate.}

| # | Field / File | Expected | Actual | Severity |
|---|---|---|---|---|
| 1 | {field or path} | {expected value} | {actual value} | {INFO/WARN/ERROR} |
| 2 | ... | ... | ... | ... |

{If this is a Content Forge PR, list source PRs/commits instead:}

| Source | Reference | Date |
|---|---|---|
| PR | {owner/repo}#{number} — {title} | {merge date} |
| Commit | {sha} — {message} | {date} |

---

## Risk Level

**{LOW | MEDIUM | HIGH}**

{One to two sentences justifying the risk classification. Reference
/policies/risk-classification.md criteria.}

---

## Proposed Changes

- [ ] {Change 1: specific file, field, or content modification}
- [ ] {Change 2}
- [ ] {Change 3}

---

## Rollback Notes

{How to revert this change if it causes issues.}

- Revert this PR to restore the previous state.
- {Any additional rollback steps, such as re-running a job or restoring a
  Hive catalog entry.}

---

## Artifacts

{Links to staging files included in or referenced by this PR.}

- `/staging/{artifact-1}`
- `/staging/{artifact-2}`

---

## Policy Compliance

- [ ] Target repo is on the allowlist (`/policies/repo-allowlist.template.json`)
- [ ] Required scope is authorized for this repo
- [ ] No denylisted paths touched (`/policies/path-denylist.md`)
- [ ] Risk level is within the repo's `max_risk_level`
- [ ] PR follows branch naming convention (`operator/{job}/{description}`)
- [ ] PR follows title and label conventions (`/policies/pr-rules.md`)
- [ ] Human review required — no auto-merge
```

---

## Labels

Apply the following labels to the PR:

| Label | When |
|---|---|
| `operator-drift` | Drift Sentinel job |
| `operator-doc` | Doc Freshness job |
| `operator-content` | Content Forge job |
| `risk-low` | Risk level is LOW |
| `risk-medium` | Risk level is MEDIUM |
| `risk-high` | Risk level is HIGH |

---

## Branch Naming

```
operator/<job-name>/<short-description>
```

Examples:
- `operator/drift-sentinel/hive-corrections-2025-01-15`
- `operator/doc-freshness/pulse-readme-update`
- `operator/content-forge/operator-release-notes`
