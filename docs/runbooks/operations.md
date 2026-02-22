# Operator — Operations Runbook

> Governance automation doesn't sleep. But it does need care and feeding.

---

## Terminology

| Term       | Definition                              |
|------------|-----------------------------------------|
| Hive       | Truth registry — the canonical catalog  |
| Pulse      | Awareness/signal layer                  |
| Node       | Unit in the Grid                        |
| Signal     | Event emitted to Pulse                  |
| Operator   | Governance automation agent             |

---

## How to Run Operator

Operator uses a **post-merge trigger model**. It activates after merges to `main` in watched repositories. There is no manual invocation, no cron — the merge _is_ the ignition.

- Operator watches a defined set of repos via the **repo-allowlist**.
- On merge to `main`, the trigger fires and Operator begins its job cycle.
- All output lands in `/staging` as artifacts. Nothing touches `main` directly.

---

## Pre-Flight Checks

Before Operator can run clean, verify:

- [ ] **Repo-allowlist** is configured and contains only intended targets.
- [ ] **Path-denylist** is loaded — sensitive paths are excluded from Operator's reach.
- [ ] **Tokens** have correct scopes (least privilege). No admin access. No broad org scopes.
- [ ] **Hive catalog** is accessible — Operator reads truth from the Hive. If the Hive is down, Operator is blind.

---

## Job Execution Flow

Every Operator run follows this pipeline:

```
1. Trigger received (merge to main)
       ↓
2. Load policies (repo-allowlist, path-denylist, risk thresholds)
       ↓
3. Run job
   ├── Drift Sentinel — detect configuration or contract drift
   ├── Doc Freshness — flag stale documentation
   └── Content Forge — generate or update content from prompts
       ↓
4. Generate artifacts in /staging
       ↓
5. Propose PR with evidence and risk classification
       ↓
6. Emit Pulse signal (future)
```

Each PR includes:
- **Evidence** — what triggered the change, what was found.
- **Risk classification** — severity tag based on policy thresholds.

---

## Monitoring

### Pulse Signals (Future)

Watch for these Signals on the Pulse layer:

| Signal                    | Meaning                                      |
|---------------------------|----------------------------------------------|
| `Operator.RunCompleted`   | Job finished successfully, artifacts staged   |
| `Operator.PolicyBlocked`  | Job was blocked by allowlist or denylist rule  |

### Audit Trail

- Review **PR history** in watched repos. Every Operator action produces a PR — that's the audit log.
- No PR means no action. If you expected a run and see nothing, check troubleshooting below.

---

## Common Operations

### Adding a New Repo to the Allowlist

1. Update the repo-allowlist configuration.
2. Verify the token has read/write access to the new repo.
3. Merge the allowlist change. Operator will pick up the new target on next trigger.

### Updating the Path Denylist

1. Add paths that Operator must never touch (secrets, CI configs, vendor dirs).
2. Merge the denylist change.
3. Verify by checking subsequent PRs — denylisted paths should never appear.

### Adjusting Risk Classification Thresholds

1. Review current thresholds in the policy configuration.
2. Adjust severity boundaries (e.g., what counts as high-risk drift vs. low-risk).
3. Merge and monitor next Operator run for correct classification.

### Reviewing Staging Artifacts

1. Check `/staging` for generated artifacts after a run.
2. Artifacts are ephemeral — they exist to support the PR proposal.
3. If artifacts look wrong, check the job prompts and policies.

---

## Token Rotation

> Tokens are the keys to the city. Rotate them before someone else does.

- Rotate tokens on a **regular schedule** (minimum quarterly, prefer monthly).
- Update tokens in the **secret store** — never in the repo, never in config files.
- After rotation, verify Operator can still authenticate by checking the next run.
- Revoke old tokens immediately after rotation is confirmed.

**Never store tokens in the repo. Ever.**

---

## Troubleshooting

### Operator Is Not Creating PRs

| Check                  | What to verify                                                  |
|------------------------|-----------------------------------------------------------------|
| Token scope            | Does the token have `repo` and `pull_request` write access?     |
| Repo-allowlist         | Is the target repo in the allowlist?                            |
| Path-denylist          | Is Operator trying to touch a denylisted path?                  |
| Hive catalog access    | Can Operator reach the Hive? If the Hive is unreachable, Operator halts. |
| Trigger                | Did a merge to `main` actually occur? Check the event log.      |

### PRs Have Incorrect Content

| Check                  | What to verify                                                  |
|------------------------|-----------------------------------------------------------------|
| Job prompt drift       | Have the prompts in `/prompts` changed unexpectedly?            |
| Stale policies         | Are policies in `/policies` current and correct?                |
| Hive catalog state     | Is the Hive returning stale or incorrect truth data?            |
| Risk thresholds        | Are classification thresholds producing wrong severity tags?    |

---

> _The grid doesn't forgive sloppy ops. Keep your allowlists tight, your tokens rotated, and your Hive honest._
