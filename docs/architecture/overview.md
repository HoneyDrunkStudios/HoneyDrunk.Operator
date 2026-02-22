# Operator Node — Architecture Overview

> The Operator is the governance automation Node in the HoneyDrunk Grid.
> It watches, compares, and proposes — never commits, never merges, never overrides.

---

## Purpose

Operator exists for one reason: **keep the Grid honest.**

Post-merge, Operator wakes up. It reads the Hive catalog (the canonical truth registry), compares it against actual repo state, and files PRs to correct any drift it finds. Every action is scoped, auditable, and human-gated.

---

## Scope

| In Scope | Out of Scope |
|---|---|
| Drift detection (Hive vs. repo state) | Direct commits to any branch |
| Doc freshness checks | Auto-merge of any PR |
| Content draft generation | CI/CD modification in other repos |
| Hive catalog update proposals | Admin-level repo operations |
| Pulse Signal emission (design-only, v1) | Secret management or rotation |

---

## Interfaces

- **Reads from** — Hive catalog JSON (canonical truth registry)
- **Reads from** — Target repo state (files, metadata, structure)
- **Writes to** — Target repos via PRs only
- **Emits to** — Pulse awareness layer via Signals (future; design-only in v1)

---

## Jobs

### Drift Sentinel
Watches for divergence between Hive catalog truth and actual repo state. When drift is detected, Operator proposes a correction PR with evidence and risk classification.

### Doc Freshness
Scans documentation files for staleness indicators — outdated references, broken links, version mismatches. Proposes update PRs when thresholds are exceeded.

### Content Forge
Generates content drafts (docs, templates, catalog entries) based on policy rules and Hive schema. Output lands in `/staging` before being proposed via PR.

---

## Activation Model

```
merge to main (watched repo)
        │
        ▼
  Operator triggers
        │
        ▼
  Read Hive catalog
        │
        ▼
  Compare repo state
        │
        ▼
  Propose corrections (PR)
        │
        ▼
  Emit Signal to Pulse (future)
```

Operator runs **post-merge only**. It does not run on push to feature branches, on PR creation, or on schedule (unless explicitly configured). The trigger is a completed merge to `main` in a watched repo.

---

## Output Artifacts

All generated content lands in `/staging` before being proposed in PRs. Nothing goes directly to production paths without human review.

```
/staging/
  ├── drafts/        # Content Forge output
  ├── patches/       # Drift Sentinel corrections
  └── docs/          # Doc Freshness updates
```

---

## Hard Boundaries

- **No direct commits.** Ever. All mutations are PRs.
- **No auto-merge.** Operator proposes. Humans decide.
- **No CI/CD tampering.** Operator does not modify workflows in other repos.
- **No scope creep.** Operator reads Hive truth and repo state. That's it.

---

*Operator is a proposal engine, not an execution engine. It illuminates drift — it does not resolve it unilaterally.*
