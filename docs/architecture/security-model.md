# Security Model

> Least privilege. PR-only writes. No secrets in code. Every action logged. No exceptions.

---

## Token Scopes

Operator runs on **least-privilege tokens**. The principle is simple: request only what the job demands, nothing more.

| Scope | Granted | Rationale |
|---|---|---|
| `repo` (read) | ✅ | Read repo state for drift detection |
| `pull_request` (create) | ✅ | Propose corrections and updates via PRs |
| `pull_request` (comment) | ✅ | Annotate PRs with evidence and risk classification |
| `admin` | ❌ | Never granted. No admin operations permitted. |
| `workflow` (write, other repos) | ❌ | Operator does not modify CI/CD in other repos. |
| `secrets` (read/write) | ❌ | Operator never accesses secret stores directly. |

---

## Repo Allowlist

Operator only touches repos explicitly listed in `repo-allowlist.template.json`. Everything else is off-limits — no exceptions, no overrides.

- Allowlist is version-controlled and auditable
- Adding a repo requires a reviewed and merged PR
- Operator validates against the allowlist before every write operation
- Unlisted repos are treated as **forbidden targets**

---

## Path Denylist

Certain paths are unconditionally forbidden from Operator modifications, regardless of repo allowlist status.

| Denied Pattern | Reason |
|---|---|
| `**/.env*` | Environment files may contain secrets |
| `**/secrets/**` | Secret storage directories |
| `**/.github/workflows/**` (other repos) | CI/CD configs are off-limits |
| `**/credentials*` | Credential files |
| `**/*.pem`, `**/*.key` | Private keys and certificates |

Full denylist is maintained in `/policies/path-denylist.md`. Operator checks every proposed file path against this denylist before including it in a PR.

---

## PR-Only Workflow

All mutations follow one path: **propose via PR, wait for human review.**

- ✅ Create PRs to allowlisted repos
- ❌ Direct commits to any branch
- ❌ Force pushes
- ❌ Auto-merges
- ❌ Branch deletion
- ❌ Tag creation or modification

```
Operator detects drift
        │
        ▼
  Validate against policy
        │
        ▼
  Check repo allowlist
        │
        ▼
  Check path denylist
        │
        ▼
  Create PR with evidence + risk level
        │
        ▼
  Human reviews → merges or rejects
```

---

## Secrets Management

**Secrets are NEVER stored in the repository.** This is absolute.

- All secrets are referenced as environment variables or secret store lookups
- Secret references are documented in runbooks, never in code
- Operator does not read, write, or rotate secrets
- If a job requires a secret, the secret is injected at runtime via the environment
- Any PR that introduces a secret pattern is blocked by policy

---

## Risk Classification

Every PR proposed by Operator includes a risk assessment:

| Level | Criteria | Example |
|---|---|---|
| **LOW** | Documentation-only, no behavioral change | README update, comment fix |
| **MEDIUM** | Config or catalog changes, non-breaking | Hive catalog field addition, template update |
| **HIGH** | Structural changes, potential downstream impact | Schema migration, cross-Sector dependency update |

Risk level is included in the PR body and as a label. Reviewers can filter and prioritize accordingly.

---

## Audit Trail

Every Operator action is traceable:

- **PR history** — Every proposal, every comment, every update is recorded in Git
- **Pulse Signals** — Operator emits Signals for key events (drift detected, PR created, policy violation) to the Pulse awareness layer (future)
- **Job logs** — Execution logs for each job (Drift Sentinel, Doc Freshness, Content Forge) are retained

Nothing happens in the dark. If Operator did it, there is a record.

---

## Guardrails Summary

| Rule | Enforced |
|---|---|
| No commits to main or protected branches | ✅ |
| No auto-merge of any PR | ✅ |
| No CI/CD workflow modification in other repos | ✅ |
| No secret storage in repo | ✅ |
| No access to unlisted repos | ✅ |
| No modification of denylisted paths | ✅ |
| Risk classification on every PR | ✅ |
| Audit trail for every action | ✅ |

---

*Security is not a feature. It is the architecture.*
