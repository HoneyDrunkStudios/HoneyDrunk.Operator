# Repo Conventions Contract

> Defines the conventions that repositories in the HoneyDrunk Grid must follow for consistency, traceability, and governance.

---

## 1. Purpose

A Grid without conventions is noise. This contract establishes the structural and procedural standards that every repo in the HoneyDrunk ecosystem adheres to — from branch strategy to file naming to PR discipline.

---

## 2. Branch Strategy

| Rule                          | Detail                                                       |
|-------------------------------|--------------------------------------------------------------|
| **Default branch**            | `main` is the default and protected branch.                  |
| **Direct pushes**             | Prohibited on `main`. All changes arrive via Pull Request.   |
| **Branch naming**             | Use descriptive names: `feat/`, `fix/`, `docs/`, `chore/`.  |
| **Stale branches**            | Branches merged or abandoned should be deleted promptly.     |

---

## 3. Pull Request Requirements

All PRs — whether human-authored or Operator-generated — must meet the following standards:

| Element             | Requirement                                                    |
|---------------------|----------------------------------------------------------------|
| **Title**           | Descriptive and specific. State what changed and where.        |
| **Description**     | Explain what, why, and impact. Use the PR template if available. |
| **Evidence**        | For Operator PRs: link to drift reports, catalog diffs, or change summaries. |
| **Risk Level**      | Operator PRs must declare risk: `LOW`, `MEDIUM`, or `HIGH`.   |
| **Linked Issues**   | Reference related issues where applicable.                     |

---

## 4. File Naming Conventions

| Context                  | Convention           | Example                        |
|--------------------------|----------------------|--------------------------------|
| Documentation files      | `lowercase-kebab-case` | `drift-sentinel-policy.md`   |
| Node names               | `PascalCase`         | `HoneyDrunk.Operator`         |
| Template files           | `lowercase-kebab-case` with `.template` suffix | `change-summary.template.md` |
| Staging artifacts        | `{type}-{target}-{date}.md` | `drift-report-HoneyDrunk.Core-2025-01-15.md` |

---

## 5. Folder Structure

Repos in the Grid should maintain the following top-level directories where applicable:

```
repo-root/
├── docs/           # Documentation and architecture notes
├── contracts/      # Governance contracts
├── policies/       # Operational policies (allowlists, denylists, thresholds)
├── prompts/        # LLM prompt templates (if applicable)
├── staging/        # Operator workspace for generated artifacts
├── .github/        # GitHub-specific config (workflows, templates, CODEOWNERS)
└── README.md       # Node identity and purpose
```

Not every repo needs every directory. The Node Metadata Contract (see `contracts/node-metadata-contract.md`) defines what is required vs. recommended based on Node status.

---

## 6. README Requirements

Every repo's `README.md` must include:

1. **Node name and one-line description** — what this is.
2. **Current status** — `Awake`, `Wiring`, or `Live`.
3. **Grid context** — how this Node fits into the HoneyDrunk Grid.
4. **Key contents** — summary of what the repo contains.
5. **Links** — to relevant contracts, policies, or related Nodes.

---

## 7. CODEOWNERS

A `CODEOWNERS` file in `.github/` is recommended for all repos. It defines default reviewers and ownership boundaries. Even single-maintainer projects benefit from explicit ownership declarations.

---

## 8. Enforcement

Operator validates these conventions during governance runs:

- **Structure checks**: verifies expected directories and files exist.
- **Naming checks**: flags files that deviate from naming conventions.
- **PR checks**: Operator-generated PRs always comply. Human PRs are checked if review automation is enabled.

Violations are reported as drift findings. Operator proposes corrections where feasible.

---

> **Grid Law**: Convention is not bureaucracy. It is the difference between a network and a tangle.
