# TASK-317 — Correct the Breaking Wire-Release Runbook's Redis Drain Guidance

---

## Metadata

```text
Task ID:           TASK-317
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: realtime, backend, persistence
Workflow:          documentation/documentation-change.md
Skills:            documentation/spec-reconciliation, discovery/impact-analysis, quality/scope-validation
Dependencies:      TASK-316
Declared Files:    docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md
```

---

## Objective

Correct `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md` so that its Phase 2
"Active Battle Drain" guidance states only what the repository and the owning
technical contract actually establish about the active battle-state key and its
expiry, and so that it requires an explicit, verified zero-active-battle
confirmation before deployment instead of presenting an elapsed-wait argument
as proof that the drain succeeded. The runbook stays preparation documentation
under `OD-316-1 = PREPARATION ONLY`; no production operation is authorized by
this task.

---

## Authoritative References

- `docs/02-technical/REDIS_STATE.md` §1 — Active battle-state key structure
- `docs/02-technical/REDIS_STATE.md` §2.1 item 7 — `SkillCharge` retirement, tolerant read, pending drain prerequisite
- `docs/02-technical/REDIS_STATE.md` §3 — TTL / lifecycle (creation, sliding refresh, explicit delete, delete-failure cleanup path)
- `docs/02-technical/REDIS_STATE.md` §4 items 2–7 — Compare-and-set, one write-back, rejected-write-writes-nothing
- `docs/02-technical/REDIS_STATE.md` §7 status note (TASK-040 / TASK-041 / TASK-217A) — implemented delete and TTL cleanup path
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.18, §8 item 10 — Breaking wire-contract removal and coordinated deployment requirement
- `docs/00-overview/MVP_SCOPE.md` §1 — Boss Card capability is IN; no new system is introduced here

---

## Scope

### In Scope
- Correcting the runbook's Phase 2 drain instructions and its maintenance-window
  sizing so they agree with `REDIS_STATE.md` §1/§3 rather than an unsupported
  TTL value.
- Stating which parts of the drain procedure are confirmed repository facts and
  which are operator-verified production assumptions.
- Requiring an explicit, verified zero-active-battle confirmation as the gate
  for Phase 3, and naming the production tooling that must exist for it.
- Removing every claim that waiting a fixed number of minutes guarantees a
  successful drain.

### Out of Scope
- Any execution against a production Redis instance, ingress, CDN, or deployment.
- Authoring a new deployment policy, drain mechanism, key format, or TTL value
  (the owner is `REDIS_STATE.md`; a divergence is a §4 conflict to report, not to
  fix here).
- Changing `REDIS_STATE.md` or any gameplay/runtime behavior.
- Transitioning TASK-316 out of BLOCKED.

---

## Current State

The runbook is DRAFT / PREPARATION ONLY and was authored under TASK-316. Its
Phase 2 maintains that "Standard battle sliding TTL is 15 minutes
(`REDIS_STATE.md` §3)" and derives a "minimum 30 minutes recommended" window
from it, and its Phase 2 step 3 accepts a scan result consisting of
"expired/terminal battle keys". The owning contract states a different TTL, and
the record can remain discoverable after terminal resolution, so neither the
elapsed-wait argument nor the "only expired/terminal keys" acceptance is
supported as written.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-317:
```text
docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above must strictly match the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [ ] 1. The runbook states the active battle-state key format exactly as `REDIS_STATE.md` §1 defines it, and states that it is the only state key the server writes.
- [ ] 2. The runbook's stated inactivity expiry matches `REDIS_STATE.md` §3 and the implemented store; every occurrence of the contradicted value is removed.
- [ ] 3. The runbook states when the expiry is refreshed and when it is not (successful resolution only; a rejected action writes nothing), citing `REDIS_STATE.md` §3/§4 item 7.
- [ ] 4. The runbook states that a terminal battle's record is deleted only after the durable battle-end unit of work commits, and that a record can therefore survive terminal resolution (committed-result delete failure, failed durable write), so a discovered key is not by itself evidence of a battle in progress or of an expired one.
- [ ] 5. The runbook's drain gate is an explicit, verified zero-active-battle confirmation, and no runbook statement claims that waiting a fixed duration guarantees a successful drain.
- [ ] 6. The runbook distinguishes confirmed repository facts from production assumptions the release engineer must confirm, and names the required production tooling and operator checks.
- [ ] 7. The runbook remains preparation-only: no step authorizes or records a production drain, deployment, migration, CDN operation, or push.
- [ ] 8. All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] 9. Quality review checklist passes (`quality/review.md` §1)
- [ ] 10. No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (technical runbook correction)
```

---

## Implementation Notes

- Verification sources for the key format, the expiry, and the refresh/delete
  points: `BattleStateRepository` (`Infrastructure/Redis`),
  `IBattleStateRepository`, `BattleResultService` (post-commit delete),
  `BattleStateService` (terminal-persistence path) and
  `RedisBattleStateRepositoryTests` (key shape and TTL assertions).
- A contradiction between the runbook and `REDIS_STATE.md` is resolved in the
  runbook's favour only where the contract is silent; where the contract is the
  owner of the statement, the runbook follows it (`AGENTS.md` §2, §4).

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — N/A (documentation-only task; no runtime change)
[ ] Integration tests  — N/A
[ ] Gameplay scenarios — N/A
```

### Key Edge Cases
- `REDIS_STATE.md` §3 — a failed battle-end delete leaves the record to the TTL
  cleanup path; a failed durable write leaves the record as the recoverable
  authoritative state.
- `REDIS_STATE.md` §4 item 7 — a rejected action does not reset the TTL.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the runbook and `REDIS_STATE.md` disagree on a statement the contract owns: report the conflict per `AGENTS.md` §4 rather than editing the contract inline
- If the task would require executing any production operation: STOP (OD-316-1 = PREPARATION ONLY)

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `1a159a3c6bef8c871f0f9ed471d09b09ded4ae25`
- Subject: **DEFECTIVE — no conventional-commit subject.** The recorded subject is
  the whole first paragraph of the intended body (329 characters, beginning
  "TASK-317 - the breaking wire-release runbook's …"). The commit carries no
  `docs:` type prefix and no blank-line separator, because the message was passed
  with the subject merged into the body. `TASK_LIFECYCLE.md` §6.2 P-4 requires a
  conventional-commit subject style and P-6 forbids `--amend`/`reset`, so the commit
  was left exactly as created and the defect recorded instead of being repaired by
  rewriting history. The committed file set is exactly the declared set and the
  content is correct.
- Owns: `TASK-317`
- Files: `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `docs/02-technical/RUNBOOK_BREAKING_WIRE_RELEASE.md` — new §3.2 records the contract facts the drain rests on (`REDIS_STATE.md` §1/§3/§4): the single `battle:{battleId}:state` key, the 30-minute inactivity expiry, when it is and is not refreshed, the post-commit battle-end delete, and why a surviving key can belong to a finished or abandoned battle. New §3.3 lists the production assumptions the operator must confirm and §3.4 states what the runbook cannot establish. §4 window sizing now derives from the documented expiry. §5 Phase 1/Phase 2 replace the elapsed-wait drain with a `SCAN`-based enumeration plus per-key classification (HP pair, `Turn`, `TTL`) and an explicit verified zero-active-battle gate, with no `DEL`/patch/rewrite. §7 adds the §7.1 gate-evidence block. §8 states the preparation-only scope. §3.1 item 2's stale `powerCost` wire description corrected to the two identity members.

### Validation Results
- `dotnet build src/backend/GameServer.sln` — **PASS** (0 errors; documentation-only change, no code impact)
- `dotnet test src/backend/GameServer.sln` — **PASS** (Domain 1674, Infrastructure 507, Api 399, Application 785; 0 failed, 0 skipped)
- Manual contract cross-check — every §3.2 assertion traced to `REDIS_STATE.md` §1/§3/§4 and to `BattleStateRepository` (`KeyPrefix`, `KeySuffix`, `StateTtl`, compare-and-set script) and `BattleResultService` (post-commit `DeleteAsync`).
- `git diff --check` — **PASS** (0 errors)

### Contract Verification (Workstream A, as requested)
```text
Authoritative key prefix/suffix   "battle:" + {battleId} + ":state"  (REDIS_STATE.md §1;
                                  BattleStateRepository.KeyPrefix/KeySuffix)
Actual state TTL                  30 minutes                      (REDIS_STATE.md §3;
                                  StateTtl = TimeSpan.FromMinutes(30))
TTL refresh                       creation + every successful resolution; never on a
                                  rejected action                 (REDIS_STATE.md §3, §4 item 7)
Terminal battles discoverable?    YES — the delete follows the committed durable write and,
                                  when it fails or no durable result exists, the record
                                  survives until the TTL clears it  (REDIS_STATE.md §3;
                                  DATABASE.md §1 sourcing item 3)
Does "battle:*:state" cover all
  live state keys?                YES — it is the only state key the server writes; the
                                  optional §4 item 4 lock key is deliberately not created
Production assumptions needing
  operator confirmation           ingress freeze mechanism; Redis observation access,
                                  deployment, topology, and permission scope; window
                                  sizing; CDN purge; rollback artifact retention
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance (subject defect disclosed above; deliberately not repaired by amendment)
- [x] Confirmed preparation-only: no production scan, ingress freeze, drain, migration, deployment, CDN operation, or push
