# Job Prompt — Content Forge

> Generate structured content drafts — change summaries, release notes, status
> updates — based on recent Grid activity. All output is proposed via PR for
> human review.

---

## Job Identity

- **Job Name**: Content Forge
- **Agent**: Operator
- **Type**: Generative (bounded)
- **Cadence**: On-demand, triggered by merges, status transitions, or request
  packets from Buzz
- **Output**: Content drafts in `/staging` + PR

---

## Input Requirements

You must receive all of the following before executing:

| Input | Description |
|---|---|
| **Change context** | List of recently merged PRs and/or commits: titles, descriptions, affected files, authors, merge dates. |
| **Hive catalog entry** | Canonical JSON for the target Node — name, description, status, Sector, topics. |
| **Content type** | One of: `change-summary`, `release-notes`, `status-update`, `node-doc-update`. |
| **Target audience** | Who the content is for: `internal` (Grid maintainers), `public` (external users), or `cross-node` (other Node owners). |
| **Policy files** | Loaded contents of: path-denylist.md, repo-allowlist.template.json, pr-rules.md, risk-classification.md. |

If any required input is missing, halt and emit an error Signal to Pulse.

---

## Task

Generate structured content drafts based on the change context and content
type requested.

### Content Type: `change-summary`

Produce a concise summary of recent changes suitable for the target audience:

- **What changed**: List of meaningful changes, grouped by category (features,
  fixes, docs, governance).
- **Why it matters**: Brief context on the impact of each change.
- **What's next**: Any follow-up work flagged in the PRs or commits.
- **Scope**: Cover the specified time window or PR set only. Do not speculate
  beyond the input data.

### Content Type: `release-notes`

Produce release note fragments following a standard structure:

- **Version/Tag**: If available from input, otherwise label as "unreleased."
- **Added**: New features, files, or capabilities.
- **Changed**: Modifications to existing behavior, structure, or content.
- **Fixed**: Bug fixes or drift corrections applied.
- **Removed**: Deprecated or deleted items.
- **Security**: Any security-relevant changes (always flag these).

### Content Type: `status-update`

Produce a Grid status update for the target Node:

- **Current status**: From the Hive catalog.
- **Recent activity summary**: Based on change context.
- **Health indicators**: Doc freshness, drift status, open issues count (if
  available).
- **Trajectory**: Whether the Node is progressing toward the next status
  transition or regressing.
- **Blockers**: Any known blockers for the next transition (reference
  `/policies/status-transition-rules.md`).

### Content Type: `node-doc-update`

Produce updated documentation sections based on recent changes:

- **Sections to update**: Identify which sections of the Node's docs are
  affected by the changes.
- **Proposed content**: Generate replacement text for affected sections.
- **Unchanged sections**: List sections that were reviewed but require no
  changes.

---

## Evidence List

For every content draft, include a source evidence section:

```markdown
## Source Evidence

| Source | Reference | Date |
|---|---|---|
| PR | {owner/repo}#{number} — {title} | {merge date} |
| Commit | {sha short} — {message} | {date} |
| Hive Catalog | {Node name} entry, field: {field} | {catalog timestamp} |
```

List all PRs, commits, and catalog fields used to generate the content. Do not
include information that cannot be traced to a specific source.

---

## Risk Classification

| Risk Level | Criteria |
|---|---|
| **LOW** | Standard content drafts with no security or status implications. Most change summaries and release notes. |
| **MEDIUM** | Content that references security changes, status transitions, or breaking changes. Content for public audience that could affect user expectations. |
| **HIGH** | Content involving security disclosures, license changes, or policy modifications. Escalate immediately. |

Reference `/policies/risk-classification.md` for detailed guidance.

---

## Proposed Actions

Content Forge always follows the same action pattern:

1. **Generate** the content draft based on inputs and content type.
2. **Write** the draft to `/staging`.
3. **Propose** the draft via PR for human review and editing.

Content drafts are never final. They are starting points. The PR description
must clearly state: "This is a generated draft. Human review and editing are
required before publication."

---

## Output Artifacts

Write content drafts to `/staging` using the following path convention:

```
/staging/{content-type}-{repo-name}-{YYYY-MM-DD}.md
```

Examples:
- `/staging/change-summary-HoneyDrunk.Hive-2025-01-15.md`
- `/staging/release-notes-HoneyDrunk.Operator-2025-01-15.md`
- `/staging/status-update-HoneyDrunk.Pulse-2025-01-15.md`

Each draft must include a header:

```markdown
<!-- Content draft generated by Operator/ContentForge on {date}.
     Content type: {type}. Target audience: {audience}.
     This is a DRAFT. Requires human review before publication. -->
```

---

## PR Creation

For every content draft produced:

1. Create a branch: `operator/content-forge/{repo-name}-{content-type}-{date}`
2. Commit the draft(s) to the branch.
3. Open a PR following `/policies/pr-rules.md`:
   - Title: `[Operator/ContentForge] {Content type} — {repo-name}`
   - Include source evidence summary in the PR description.
   - Apply labels: `operator-content`, `risk-{level}`.
   - Add rollback notes (for content: "Revert this PR to remove the draft").
4. Never auto-merge. Content always requires human review and editing.

---

## Guardrails

- Respect `/policies/path-denylist.md`. Never reference or include content
  from denylisted paths.
- Only produce content for repos in `/policies/repo-allowlist.template.json`.
  Verify the repo is allowlisted and that `content-sync` is in its `scopes`
  array.
- Never commit directly to `main` or protected branches.
- Never publish content directly. All content goes through PR review.
- Do not fabricate changes. Every statement in a content draft must trace to a
  specific PR, commit, or Hive catalog field from the input.
- If the change context includes security-sensitive changes, classify risk as
  MEDIUM or higher and flag explicitly in the draft.
- Generated content is a draft. Mark it as such. Humans decide what gets
  published.
