# Operator — Status Transition Rules

> A Node earns its status. Nothing is granted without proof.

---

## Node Statuses

| Status | Meaning |
|---|---|
| **Awake** | Repo exists. Minimal structure. The Node is alive but not yet wired into the Grid. |
| **Wiring** | Under active development. Structure is taking shape. Docs and contracts are in progress. |
| **Live** | Stable, documented, operational. The Node is fully connected to the Grid and pulling its weight. |

---

## Transition Rules

### Awake → Wiring

The Node has moved beyond a bare repo. Structure is emerging.

**Required evidence:**

- [ ] README exists and is non-empty.
- [ ] Basic folder structure is present (e.g., `docs/`, `contracts/`, or equivalent).
- [ ] At least one documentation file exists beyond the README.

---

### Wiring → Live

The Node is ready for operational duty. Everything is in place.

**Required evidence:**

- [ ] All required documentation is present and current.
- [ ] Contracts are defined and committed.
- [ ] Policies are in place (or inherited from parent).
- [ ] Job prompts are ready (if applicable).
- [ ] Hive catalog entry is accurate and complete.
- [ ] No unresolved drift detected by Operator.

---

### Live → Wiring (Regression)

A Live Node can regress. Trust is maintained, not assumed.

**Triggered by:**

- Missing or deleted required documentation.
- Broken or invalid contracts.
- Stale Hive catalog entries (e.g., description mismatch, missing fields).
- Unresolved drift that persists beyond one Pulse cycle.

---

## State Diagram

```
┌───────────┐         ┌───────────┐         ┌───────────┐
│           │         │           │         │           │
│   Awake   │───────▶ │  Wiring   │───────▶ │   Live    │
│           │         │           │         │           │
└───────────┘         └─────┬─────┘         └─────┬─────┘
                            │                     │
                            │◀────────────────────┘
                            │    (regression)
                            │
```

- **Awake → Wiring**: Forward transition. Evidence required.
- **Wiring → Live**: Forward transition. Full evidence required.
- **Live → Wiring**: Regression. Triggered by drift or missing artifacts.
- **No direct Awake → Live path.** Every Node passes through Wiring.
- **No backward transition to Awake.** Once wired, a Node stays at Wiring or above.

---

## How Transitions Happen

1. **Operator detects** that a Node meets (or no longer meets) the criteria for a status.
2. **Operator opens a PR** with the proposed transition and evidence checklist.
3. **Human reviews** the evidence and approves or rejects the transition.
4. **Hive catalog is updated** to reflect the new status upon merge.

Operator proposes. Humans approve. The Hive records the truth.
