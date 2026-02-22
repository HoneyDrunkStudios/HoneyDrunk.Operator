# HoneyDrunk.Operator

> **Governance-grade control-plane Node for the HoneyDrunk Grid.**
> Proposes changes to truth. Never writes it directly.

---

## What Operator Is

Operator is the automation Node that keeps the Grid honest. It watches for drift, generates structured change proposals, and enforces a PR-only mutation workflow across every Sector it touches.

**Core capabilities (designed, not yet runtime-wired):**

- **Repo / Docs drift detection** — compares live repo state against The Hive catalog and flags mismatches.
- **Hive catalog JSON update proposals** — when reality diverges from canon, Operator drafts a PR to reconcile.
- **Content draft generation** — produces structured summaries, changelogs, and doc patches from merge events.
- **PR-only mutation workflow** — every proposed change ships as a pull request with evidence and risk classification. No exceptions.
- **Optional Pulse Signal emission** — Operator can emit Signals to the Pulse awareness layer so other Nodes stay informed.

## What Operator Is Not

| Myth | Reality |
|------|---------|
| Production runtime | Structured for future implementation — scaffolding and contracts only today. |
| Auto-merge bot | Operator **never** merges its own PRs. A human reviews every proposal. |
| Direct committer | No commits to `main`. Ever. All mutations flow through PRs. |
| CI/CD modifier | Operator does not touch pipelines, workflows, or deploy configs in other repos. |

---

## Where Operator Sits in the Grid

```
┌─────────────────────────────────────────────────┐
│                 HoneyDrunk Grid                 │
│                                                 │
│   ┌───────────┐  Signals  ┌───────────┐        │
│   │   Pulse   │◄─────────►│  Operator  │        │
│   │ Awareness │           │ Governance │        │
│   │   Layer   │           │    Node    │        │
│   └───────────┘           └─────┬──────┘        │
│                                 │ PRs only      │
│                           ┌─────▼──────┐        │
│                           │  The Hive  │        │
│                           │   Truth    │        │
│                           │  Registry  │        │
│                           │ (JSON)     │        │
│                           └────────────┘        │
└─────────────────────────────────────────────────┘
```

- **The Hive** — canonical JSON registry. Single source of truth for every Sector.
- **Pulse** — awareness and Signal layer. Nodes publish and subscribe to Signals here.
- **Operator** — governance automation Node. Reads from The Hive, proposes changes via PR, emits Signals to Pulse. Never writes directly to canon.

---

## How Operator Is Triggered

Operator follows a **post-merge model**.

1. A merge lands on `main` in a watched repo.
2. Operator detects the change (webhook, poll, or workflow dispatch — implementation TBD).
3. Operator evaluates drift, generates proposals, and opens PRs as needed.

It does **not** run pre-merge. It does **not** gate other pipelines. It reacts to committed truth, never speculative state.

---

## High-Level Jobs

### Drift Sentinel

Detects mismatches between live repo state and The Hive catalog.

- Compares repo metadata (structure, tags, descriptions) against Hive JSON entries.
- Flags new repos not yet cataloged, stale entries for archived repos, and field-level drift.
- Outputs a structured drift report attached to the resulting PR.

### Doc Freshness

Monitors documentation Sectors for staleness and proposes updates.

- Tracks doc-to-source coupling — when source changes, linked docs get flagged.
- Generates PR proposals with suggested patches or "needs review" annotations.
- Risk-classifies each proposal: `low`, `medium`, `high`.

### Content Forge

Produces structured change summaries and content drafts from merge activity.

- Summarizes what changed, why it matters, and what downstream Sectors are affected.
- Drafts release notes, changelog fragments, and cross-repo digest entries.
- Outputs land in `staging/` for human review before promotion.

---

## How Buzz Hands Off Request Packets

**Buzz** is the personal assistant persona — conversational, context-aware, opinionated. Buzz talks to humans. Operator talks to repos.

The handoff works like this:

1. Buzz interprets user intent and produces a **request packet** — a structured JSON document describing the desired change, target Sector, evidence, and risk level.
2. Buzz drops the packet into a known location (staging area or dispatch queue).
3. **Buzz never touches canon directly.** It does not open PRs, edit Hive JSON, or commit to any repo.
4. Operator picks up the packet, validates it against current Hive state, and decides whether to propose a PR.
5. If the packet is malformed or conflicts with current truth, Operator rejects it and emits a Signal to Pulse.

This separation keeps the conversational layer clean and the governance layer auditable.

---

## PR-Only Governance

Every mutation Operator proposes follows this protocol:

- **Pull request with evidence.** Every PR includes what changed, why, the drift report or request packet that triggered it, and the computed risk classification.
- **Risk classification.** Each proposal is tagged `low`, `medium`, or `high` risk. Higher risk = more review scrutiny.
- **No direct commits.** Operator never pushes to `main`, `master`, or any protected branch.
- **No auto-merge.** Operator opens the PR and walks away. A human approves or closes it.
- **Audit trail.** Every proposal is a PR. Every PR is searchable, reviewable, and revertable.

---

## Repo Structure

```
HoneyDrunk.Operator/
├── .github/
│   ├── ISSUE_TEMPLATE/      # Issue scaffolding for Operator tasks
│   └── workflows/           # GitHub Actions (future automation wiring)
├── contracts/                # Interface contracts — schemas, request packet specs
├── docs/
│   ├── architecture/         # Grid topology, Node interaction diagrams
│   ├── runbooks/             # Operational procedures for Operator jobs
│   └── signals/              # Pulse Signal definitions and formats
├── policies/                 # Governance rules, risk classification criteria
├── prompts/
│   ├── jobs/                 # Job-specific prompt templates (Drift, Forge, etc.)
│   ├── system/               # System-level prompt scaffolding
│   └── templates/            # Reusable prompt fragments
├── staging/                  # Draft outputs awaiting human review
├── LICENSE
└── README.md
```

---

## Non-Negotiable Guardrails

These rules are baked into the design. They are not configurable.

1. **PR-only mutation.** Operator never commits directly to any branch. All changes are proposed via pull request.
2. **No auto-merge.** Operator does not approve or merge its own PRs. Ever.
3. **No CI/CD tampering.** Operator does not modify workflows, pipelines, or deploy configurations in other repos.
4. **Post-merge activation only.** Operator reacts to merged state. It does not interfere with in-flight work.
5. **Human-in-the-loop.** Every proposal requires human review before it becomes truth.
6. **Audit everything.** Every action Operator takes is traceable through PRs, Signals, and drift reports.
7. **Separation of concerns.** Buzz handles conversation. Operator handles governance. The Hive holds truth. Pulse carries awareness. No Node crosses its boundary.

---

## Incremental Adoption

No homelab required. No infrastructure to provision. Start small, expand when it earns trust.

### Phase 1 — Docs & Contracts

Start by adding architecture docs to `docs/` and interface schemas to `contracts/`. Define the Signal formats, the request packet schema, and the Hive catalog structure. This is the foundation.

### Phase 2 — Prompts & Policies

Build out `prompts/` and `policies/`. These define how Operator reasons about drift, risk, and content generation. Customize them for your Sectors.

### Phase 3 — Wire Up Automation

Connect GitHub Actions in `.github/workflows/` to trigger Operator jobs on merge events. Start with Drift Sentinel on a single low-risk repo. Expand from there.

Each phase is independently useful. You don't need Phase 3 to benefit from Phase 1.

---

## Next Steps — Future Runtime Implementation

Operator is scaffolding today. The contracts, policies, and prompt templates are real. The runtime is not — yet.

Planned implementation work:

- [ ] **Drift Sentinel runtime** — GitHub Actions workflow that compares repo metadata against Hive catalog JSON on merge events.
- [ ] **Doc Freshness scanner** — Workflow that detects stale docs and opens PRs with patch suggestions.
- [ ] **Content Forge pipeline** — Automated summarization and draft generation from merge diffs.
- [ ] **Request packet ingestion** — Staging area watcher that picks up Buzz-produced packets and validates them.
- [ ] **Pulse Signal emitter** — Lightweight webhook or event dispatch to notify downstream Nodes.
- [ ] **Risk classification engine** — Rule-based tagger that scores proposals before PR creation.
- [ ] **Hive catalog sync** — Bidirectional drift resolution between repo state and canonical JSON.

All implementation will follow the same PR-only governance model that Operator enforces on everything else.

---

## License

See [LICENSE](LICENSE) for details.

---

*Operator watches the Grid so you don't have to. But it never acts without your say-so.*