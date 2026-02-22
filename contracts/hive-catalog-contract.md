# Hive Catalog Contract

> Defines the interface between Operator and the Hive — the single source of truth for Node metadata in the HoneyDrunk Grid.

---

## 1. Purpose

The Hive catalog is a canonical JSON registry that describes every Node in the Grid. Operator depends on the Hive to detect drift, validate metadata, and propose corrections. This contract specifies what the catalog contains, how Operator interacts with it, and how conflicts are resolved.

---

## 2. Catalog Entry Schema

Every Node in the Hive catalog is represented by a JSON object with the following fields.

### Required Fields

| Field         | Type   | Description                                      |
|-------------- |--------|--------------------------------------------------|
| `name`        | string | Display name of the Node (PascalCase).           |
| `repo_url`    | string | Full URL to the Node's GitHub repository.        |
| `status`      | string | Current lifecycle status. See §3.                |
| `sector`      | string | Domain grouping the Node belongs to.             |
| `description` | string | One-line summary of the Node's purpose.          |

### Optional Fields

| Field              | Type    | Description                                         |
|------------------- |---------|-----------------------------------------------------|
| `topics`           | array   | List of topic tags for discoverability.             |
| `visibility`       | string  | `public` or `private`. Defaults to `public`.        |
| `last_verified`    | string  | ISO 8601 timestamp of last Operator verification.   |
| `signals_enabled`  | boolean | Whether the Node emits Signals to Pulse.            |
| `created_at`       | string  | ISO 8601 timestamp of catalog entry creation.       |

### Catalog Root

The catalog itself includes a top-level `version` field (semver string) and a `nodes` array containing entries conforming to the schema above.

```json
{
  "version": "1.0.0",
  "nodes": [ ]
}
```

---

## 3. Valid Statuses

| Status   | Meaning                                                    |
|--------- |------------------------------------------------------------|
| `Awake`  | Node exists and is recognized, but not yet wired into the Grid. |
| `Wiring` | Node is actively being configured, documented, or integrated. |
| `Live`   | Node is fully operational and governed by Operator.        |

Status transitions must follow this order: **Awake → Wiring → Live**. Reverse transitions are permitted only via explicit Status Change Proposals with documented justification.

---

## 4. Operator Access Rules

- **Read**: Operator reads the Hive catalog at the start of every governance run to build its view of the Grid.
- **Write**: Operator never writes directly to the Hive. All proposed changes are submitted as Pull Requests against the Hive repository.
- **Validation**: Operator validates each catalog entry against this schema before proposing any update. Invalid entries are flagged in drift reports.

---

## 5. Schema Versioning

The catalog carries a `version` field at its root. Operator must check this version before proposing changes.

- If the catalog version is ahead of Operator's known version, Operator halts and logs a compatibility warning.
- If the catalog version matches, Operator proceeds normally.
- Version bumps to the catalog schema require a corresponding update to Operator's validation logic.

---

## 6. Conflict Resolution

When the Hive catalog and a Node's repository disagree:

| Data Type             | Source of Truth | Rationale                                        |
|---------------------- |-----------------|--------------------------------------------------|
| Status and metadata   | **Hive**        | The Hive is the canonical registry.              |
| File contents         | **Repo**        | The repo owns its own code and documentation.    |
| Structural compliance | **Operator**    | Operator enforces contracts and policies.        |

Operator reports conflicts as drift findings. Resolution is always proposed, never forced.

---

## 7. Enforcement

This contract is enforced by Operator's Drift Sentinel module. Violations produce drift reports and, where applicable, automated PR proposals to bring the catalog and repo into alignment.

---

> **Grid Law**: The Hive speaks for the Grid. Repos speak for themselves. Operator keeps them honest.
