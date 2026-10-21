# TASK-316 — Breaking Wire-Contract Release Preparation Only

---

## Metadata

```text
Task ID:           TASK-316
Type:              DOCUMENTATION
Status:            BLOCKED
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     backend
Supporting Agents: realtime, client, persistence, review
Workflow:          documentation/documentation-change.md
Skills:            discovery/impact-analysis, quality/scope-validation, documentation/spec-reconciliation
Dependencies:      TASK-313, TASK-315
Declared Files:    docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md
```

---

## Objective

Prepare the operational runbook and deployment checklist for the coordinated breaking wire-contract release associated with the retired `BossSkillCast` event under operator decision OD-316-1 = PREPARATION ONLY:
1. Define release preconditions, authorization requirements, and rollback criteria.
2. Formalize the Decision B-2 active-battle drain procedure (`REDIS_STATE.md` §2.1 item 7) to ensure in-flight battles safely terminate before server/client deployment.
3. Establish coordinated deployment ordering between authoritative backend server and Phaser/React client assets.
4. Distinguish confirmed repository facts from operational assumptions requiring operator verification.
5. Record explicit stop conditions and outstanding release prerequisites. Do NOT execute production migrations, battle drains, deployments, or remote pushes.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — MVP scope boundaries
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.18, §3.2.30, §8 item 10 — Breaking contract change for `BossSkillCast` retirement; coordinated deployment required
- `docs/02-technical/REDIS_STATE.md` §2.1 item 7, §3.10 — Decision B-2 active-battle drain procedure and tolerant deserialization basis
- `docs/02-technical/DATABASE.md` §1 note item 4 — OD-311-1 = B storage compatibility only (no database cleanup migration)
- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` — Slice 5 removal delivered

---

## Scope

### In Scope
- Authoring breaking wire-contract release runbook in `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md`.
- Enforcing hard prohibitions on active battle mutation, deployment execution, and remote push.
- Recording outstanding prerequisites and maintaining accurate lifecycle state (`BLOCKED`).

### Out of Scope
- Draining or mutating active production battles in Redis.
- Executing production migrations, infrastructure changes, or deployments.
- Pushing commits to remote tracking branches.
- Adding wire tolerant-reader shims or altering protocol schemas.

---

## Current State

The code changes retiring `BossSkillCast` and `BossState.SkillCharge` were implemented and verified in TASK-313. Stale documentation specifications were reconciled in TASK-315. The release runbook has been authored in `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md`. However, production deployment authorization, scheduled maintenance window, and live deployment operations remain unperformed and pending operator execution.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-316:
```text
docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md
```

---

## Acceptance Criteria

- [x] 1. Operational runbook `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md` is authored, covering preconditions, drain procedure, deployment ordering, smoke tests, rollback criteria, and operator sign-off.
- [x] 2. Confirmed repository facts are strictly distinguished from operational assumptions requiring operator verification.
- [x] 3. Zero active production battles are drained or mutated.
- [x] 4. Zero production deployments or migrations are executed.
- [x] 5. Zero commits are pushed to remote origin.
- [ ] 6. Live release execution completed. **STOPPED / BLOCKED:** Deployment is strictly prohibited in this session (OD-316-1 = PREPARATION ONLY).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios / balance telemetry)
[x] docs/ (technical runbook only)
```

---

## Implementation Notes

Runbook authored in `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md`. The task is transitioned to BLOCKED due to live operational prohibitions.

---

## Testing Requirements

### Required Verification
```text
[x] Documentation validation — verify runbook references to SIGNALR_PROTOCOL.md and REDIS_STATE.md
[x] Scope verification        — confirm zero production deployment actions executed
```

---

## Stop Conditions / Blocking Report

### Status: Blocked

### Blocking Condition
The release associated with the breaking wire contract retirement of `BossSkillCast` requires:
1. Written Production Authorization from the Product Owner / Operator.
2. Scheduled Maintenance Window for ingress freezing (`POST /api/battle/start` returning HTTP 503).
3. Live operational access to observe and verify active battle drain in Redis (`SCAN 0 MATCH "battle:*:state"` returning 0 keys).
4. Coordinated live deployment of backend GameServer and frontend client assets with CDN invalidation.

Per operator decision **OD-316-1 = PREPARATION ONLY**, automated agent sessions are strictly prohibited from draining active production battles, executing deployments, or pushing commits. Therefore, the task cannot transition to DONE and must remain BLOCKED in `tasks/blocked/`.

### Completed So Far
- Authoritative operational runbook and deployment checklist authored at `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md` (Implementation commit `8424a9e2d0a432281b3d67cc528af901fdae4497`).
- Ingress freeze procedure, battle drain window derived from the documented inactivity expiry (`REDIS_STATE.md` §3 — 30 minutes, refreshed on every successful resolution and not on a rejected action), an explicit verified zero-active-battle confirmation gate, coordinated deployment ordering, smoke test procedure, and rollback criteria fully specified.
- Confirmed repository facts separated from operational assumptions.

> **Evidence correction (TASK-319).** This section previously recorded the runbook
> implementation commit as `8424a9e5251648a474fe5e0cb2aa55c28591a27e`. Git reported
> no object for that identifier (`git cat-file` failed), so it was not a valid
> commit ID. It has been replaced with the exact full SHA Git reports,
> `8424a9e2d0a432281b3d67cc528af901fdae4497` (`git rev-parse`, verified with
> `git show --stat` to be the commit that added
> `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md` — 1 file, 143 insertions).
> Nothing else in this record's lifecycle state changed: the task remains
> **BLOCKED** pending explicit production release authorization.
>
> The runbook's drain guidance was corrected under TASK-317 after a
> post-release-readiness audit found that it had been written against an
> unsupported 15-minute expiry. The corrected runbook derives its drain gate
> from `REDIS_STATE.md` §1/§3 and requires an explicit, verified
> zero-active-battle confirmation before Phase 3, and it no longer treats any
> elapsed wait as proof that the drain completed. TASK-317 remains preparation
> documentation only; no production operation was performed.

### What Is Needed to Unblock
- Operator schedules production maintenance window.
- Operator grants formal deployment authorization.
- Designated release engineer executes the runbook steps in production.
- Upon verified production deployment completion, operator may transition this task or file release sign-off evidence.
