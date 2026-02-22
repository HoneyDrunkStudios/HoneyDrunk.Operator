# Operator — Incident Response Runbook

> When the grid glitches, you need a plan — not a prayer.

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

## Incident Types

| ID   | Type                                  | Description                                                      |
|------|---------------------------------------|------------------------------------------------------------------|
| IT-1 | Incorrect changes proposed            | Operator generates a PR with wrong or harmful content            |
| IT-2 | Non-allowlisted repo access           | Operator targets a repo outside the approved allowlist           |
| IT-3 | Denylisted path modification          | Operator attempts to modify a path on the denylist               |
| IT-4 | Token compromise                      | Operator's authentication token is leaked or stolen              |
| IT-5 | Pulse Signal flood                    | Operator emits excessive Signals, overwhelming the Pulse layer   |

---

## Severity Levels

| Level | Name             | Criteria                                                        | Response Window |
|-------|------------------|-----------------------------------------------------------------|-----------------|
| SEV-1 | **Critical**    | Security breach. Token compromise. Unauthorized repo access.    | Immediate       |
| SEV-2 | **High**        | Incorrect changes proposed **and merged** past human review.    | < 1 hour        |
| SEV-3 | **Medium**      | Incorrect changes proposed but **caught in review**. No merge.  | < 4 hours       |
| SEV-4 | **Low**         | Noisy Signals, non-critical drift, cosmetic issues.             | Next business day |

---

## Immediate Actions by Severity

### SEV-1 — Critical

> Kill the signal. Lock the doors.

1. **Revoke all Operator tokens immediately.** Do not wait for confirmation.
2. **Disable the Operator trigger** — stop all post-merge activations.
3. **Close all open Operator PRs** across every watched repo.
4. **Notify the security team** and incident commander.
5. **Audit access logs** — determine what the compromised token touched.
6. **Rotate all related secrets** in the secret store.

### SEV-2 — High

> Damage is done. Contain and revert.

1. **Revert the merged PR** immediately.
2. **Close all open Operator PRs** pending investigation.
3. **Disable the Operator trigger** until root cause is identified.
4. **Notify the team lead** and repo maintainers.
5. **Review the Hive catalog** — determine if Operator was acting on bad truth data.

### SEV-3 — Medium

> The guardrails held. Investigate why they had to.

1. **Close the incorrect PR** with a comment explaining the issue.
2. **Review the job prompts and policies** that produced the bad output.
3. **Check the Hive catalog** for stale or incorrect entries.
4. **Update the path-denylist or repo-allowlist** if scope was too broad.
5. **Document the finding** for the post-incident review.

### SEV-4 — Low

> Noise in the signal. Tune the frequency.

1. **Acknowledge the noisy Signals** on the Pulse layer.
2. **Adjust risk classification thresholds** if drift is being over-reported.
3. **Review and tighten job prompts** to reduce false positives.
4. **No immediate action required** — address in the next ops cycle.

---

## Recovery Steps

After containment, follow this recovery sequence:

1. **Audit PR history** — review every Operator PR since the last known-good state.
2. **Review Pulse Signals** — look for anomalous `Operator.RunCompleted` or `Operator.PolicyBlocked` events.
3. **Verify Hive catalog integrity** — confirm truth data is accurate and untampered.
4. **Rotate tokens** — even if the incident wasn't token-related, rotate as a precaution.
5. **Update policies** — tighten allowlists, denylists, and risk thresholds based on findings.
6. **Re-enable the Operator trigger** only after all checks pass.
7. **Monitor the next three runs** closely for recurrence.

---

## Guardrails Reminder

These are non-negotiable. If any of these are violated, treat it as SEV-1.

| Guardrail                          | Status     |
|------------------------------------|------------|
| Operator **never** commits to main | Enforced   |
| Operator **never** auto-merges     | Enforced   |
| Human review is the **final gate** | Enforced   |

Operator proposes. Humans approve. That's the protocol.

---

## Escalation Path

| Severity | First Contact             | Escalation                     | Executive Notification |
|----------|---------------------------|--------------------------------|------------------------|
| SEV-1    | Security team immediately | Incident commander within 15m  | Yes — within 1 hour   |
| SEV-2    | Team lead immediately     | Security team within 1 hour    | If data was affected   |
| SEV-3    | Repo maintainer           | Team lead if pattern recurs    | No                     |
| SEV-4    | Ops on-call               | Team lead if persistent        | No                     |

---

## Post-Incident Checklist

- [ ] Root cause identified and documented.
- [ ] Path-denylist updated if Operator touched restricted paths.
- [ ] Repo-allowlist updated if Operator targeted unauthorized repos.
- [ ] Job prompts reviewed and tightened.
- [ ] Tokens rotated.
- [ ] Pulse Signals reviewed for anomalies.
- [ ] Hive catalog verified.
- [ ] Lessons learned documented and shared with the team.
- [ ] Runbook updated if new incident patterns were discovered.

---

> _The grid is only as strong as its incident response. When the Operator misfires, speed and clarity are everything. Follow the protocol. Trust the guardrails. Fix the root cause._
