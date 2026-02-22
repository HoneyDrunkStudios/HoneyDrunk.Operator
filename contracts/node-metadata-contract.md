# Node Metadata Contract

> Defines the metadata and structural requirements that every Node in the HoneyDrunk Grid must maintain.

---

## 1. Purpose

Every Node is a unit in the Grid. For the Grid to function — for Operator to govern, for Pulse to signal, for the Hive to catalog — each Node must carry a minimum set of metadata and structural elements. This contract specifies those requirements.

---

## 2. Required Structure

Every Node repository must contain the following at its root:

| Item             | Type      | Description                                                  |
|----------------- |-----------|--------------------------------------------------------------|
| `README.md`      | File      | Must state what the Node does, its current status, and how it fits in the Grid. |
| `docs/`          | Directory | Documentation home. May contain architecture notes, guides, or references. |
| Purpose statement | Section   | A clear, scannable statement in the README explaining the Node's role. |

A Node missing any of these items is considered **non-compliant** and will be flagged during Drift Sentinel runs.

---

## 3. Recommended Structure

These elements are not enforced but are expected for Nodes at `Wiring` or `Live` status:

| Item          | Type      | Description                                                    |
|-------------- |-----------|----------------------------------------------------------------|
| `contracts/`  | Directory | Governance contracts the Node adheres to or defines.           |
| `policies/`   | Directory | Operational policies (allowlists, denylists, risk thresholds). |
| `prompts/`    | Directory | LLM prompt templates, if the Node uses AI-assisted automation. |

---

## 4. Metadata Fields in the Hive

The following fields are tracked in the Hive catalog for each Node. Operator validates these during governance runs.

| Field            | Type   | Required | Description                                      |
|----------------- |--------|----------|--------------------------------------------------|
| `name`           | string | Yes      | Node display name (PascalCase).                  |
| `status`         | string | Yes      | Lifecycle status: `Awake`, `Wiring`, or `Live`.  |
| `sector`         | string | Yes      | Domain grouping (Sector) the Node belongs to.    |
| `description`    | string | Yes      | One-line summary of purpose.                     |
| `repo_url`       | string | Yes      | Full GitHub repository URL.                      |
| `created_at`     | string | Yes      | ISO 8601 timestamp of when the Node was registered. |
| `last_verified`  | string | Yes      | ISO 8601 timestamp of last Operator verification. |

---

## 5. Validation Rules

Operator applies the following checks during Drift Sentinel runs:

1. **Existence check**: Does the repo exist and is it accessible?
2. **Structure check**: Does the repo contain `README.md` and `docs/`?
3. **Purpose check**: Does the README contain a discernible purpose statement?
4. **Metadata sync**: Do the Hive catalog fields match what the repo actually reflects?
5. **Status coherence**: Is the Node's status consistent with its structural maturity?

---

## 6. Non-Compliance

When a Node fails validation:

- A **drift finding** is generated with the specific violation.
- Operator proposes a **correction PR** where possible (e.g., missing docs directory, outdated description).
- For issues that cannot be auto-corrected (e.g., missing purpose statement), Operator opens an **issue** on the target repo.
- Repeated non-compliance across multiple runs may trigger a status downgrade proposal.

---

## 7. Lifecycle Expectations

| Status   | Minimum Requirements                                              |
|--------- |-------------------------------------------------------------------|
| `Awake`  | `README.md` exists. Hive catalog entry is present.               |
| `Wiring` | `README.md` + `docs/` exist. Purpose statement is present. Metadata fields are populated. |
| `Live`   | All required and recommended structures are in place. Metadata is current. Signals are enabled. |

---

> **Grid Law**: A Node that cannot describe itself cannot be trusted by the Grid.
