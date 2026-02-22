# Operator / Buzz Boundary Model

> Two agents. Two authorities. One Grid. Zero overlap in write access.

---

## Dual-Agent Model

The HoneyDrunk Grid runs on a strict separation of concerns between its two active agents:

| | **Operator** | **Buzz** |
|---|---|---|
| **Role** | Governance automation Node | Personal assistant persona |
| **Authority** | PR-only writes to allowlisted repos | No write access to Hive or repos |
| **Input** | Hive catalog, repo state, policy files | Public context, user instructions |
| **Output** | PRs with evidence + risk classification | Request packets (structured intent documents) |
| **Autonomy** | Executes within policy bounds | Hands off to Operator for any proposed changes |

---

## Operator

Operator is the enforcement arm. It operates under strict policy, reads from the Hive (the canonical truth registry), and proposes changes exclusively via PRs.

- Reads Hive catalog JSON for canonical truth
- Reads target repo state for drift detection
- Reads policy files for guardrail enforcement
- Writes PRs to allowlisted repos only
- Tags every proposal with evidence and risk classification (LOW / MEDIUM / HIGH)
- Emits Signals to Pulse for awareness (future)

Operator never guesses. It compares, classifies, and proposes.

---

## Buzz

Buzz is the personal assistant persona. It talks to the human, interprets intent, and produces structured **request packets** — but it never touches canon directly.

- Interprets user instructions into structured intent
- Generates request packets (not PRs, not commits, not direct edits)
- Has **zero write access** to Hive, repos, or any Grid artifact
- Hands off to Operator for any change that would touch truth or canon

Buzz is the voice. Operator is the hand. The hand only moves when policy says it can.

---

## Request Packet Flow

```
  User intent
      │
      ▼
  Buzz interprets
      │
      ▼
  Request packet generated
      │
      ▼
  Operator validates against policy
      │
      ▼
  Policy allows? ──── No ──→ Rejected (logged)
      │
     Yes
      │
      ▼
  Operator proposes via PR
      │
      ▼
  Human reviews and merges
```

A request packet is a structured intent document. It contains what Buzz wants to happen, why, and with what context. Operator treats it as input — not as authority.

---

## Authority Separation

| Access | Operator | Buzz |
|---|---|---|
| Read Hive catalog | ✅ | ❌ |
| Read repo state | ✅ | ❌ |
| Read policy files | ✅ | ❌ |
| Read public context | ✅ | ✅ |
| Read user instructions | ❌ | ✅ |
| Write PRs (allowlisted repos) | ✅ | ❌ |
| Write to Hive | ❌ | ❌ |
| Commit to main | ❌ | ❌ |
| Auto-merge | ❌ | ❌ |

---

## Guardrails

These rules are non-negotiable for both agents:

- **Neither agent commits to main.** All changes go through PRs.
- **Neither agent auto-merges.** Human review is mandatory.
- **Both agents are auditable.** Every action is traceable via PR history and Pulse Signals.
- **Buzz never bypasses Operator.** If it touches truth, it goes through Operator.
- **Operator never bypasses policy.** If policy denies it, the proposal is rejected and logged.

---

*Two agents, one principle: truth is proposed, never imposed.*
