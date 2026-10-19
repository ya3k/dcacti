# TASK-307 — ADR-029 Reconciliation and Slice 3 Readiness

<!--
  GEN-TASK EXECUTION MANIFEST — STATUS: DONE

  Reconciliation and prerequisite task only. It resolves the *design* and
  *dependency* blockers left by TASK-306 to the smallest honest operator
  decision set, and prepares ONE end-to-end Slice 3 implementation task. It
  writes no runtime code, no tests, no migrations, no provisioning, no wire
  change, no frontend change, and no `docs/` change: every `docs/` edit the
  approved contract needs is still gated on operator approval (AGENTS.md §4,
  §7, §17, §20), and no approval was granted in this session.

  Task created directly in tasks/active/ with Status: IN PROGRESS because the
  requiring brief assigned it and its readiness basis was verified in-session
  (see `## Readiness Basis`): the authoritative documents exist, MVP scope is
  confirmed, dependencies TASK-303/304/305/306 are DONE, and no duplicate
  TASK-307 record or artifact exists. BACKLOG -> READY -> IN PROGRESS was
  executed as one verified step, the same basis TASK-304/TASK-306 recorded.
-->

---

## Metadata

```text
Task ID:           TASK-307
Type:              DOCUMENTATION
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     orchestrator
Supporting Agents: gameplay, backend, persistence, realtime, client, testing, review
Workflow:          documentation/documentation-change.md
                   (+ architecture/adr-change.md — PROPOSAL ONLY, no ADR edit)
Skills:            discovery/task-readiness-assessment,
                   discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/authority-determinism-audit,
                   quality/documentation-consistency,
                   quality/scope-validation,
                   realtime/realtime-protocol-validation,
                   backend/persistence-analysis
Dependencies:      TASK-303 (DONE — ADR-029 authorization and documented target
                   state), TASK-304 (DONE — BossState.Power foundation),
                   TASK-305 (DONE — actor-aware shared cast contract),
                   TASK-306 (DONE — design report and decision set),
                   ADR-029 (Accepted), ADR-024, ADR-026
Declared Files:    tasks/artifacts/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md
                   .ai/PROJECT_STATE.md
```

**Lifecycle path executed:**

```text
BACKLOG -> READY -> IN PROGRESS   (readiness basis below and in artifact §1)
IN PROGRESS -> IN REVIEW          (reconciliation report drafted, evidence verified)
IN REVIEW -> DONE                 (two-stage commit protocol, TASK_LIFECYCLE.md §6)
```

---

## Objective

Reconcile TASK-306's design report against the current authoritative documents
and source, correct what no longer holds, close every item that authoritative
evidence already settles, and reduce the remaining operator choices to the
smallest possible explicit set. Produce one consolidated contract for the Boss
Card definition/provisioning, cast-priority score, initial `Earthquake → Shield`
card, active form, cooldown handling, one-action policy, atomicity and
`BossCardCast` event surface; one decision table separating approved-by-authority
decisions from unresolved operator choices; one reconciled ADR-029 dependency
graph and Slice 3 exit criteria; and one proposed end-to-end Slice 3 task with an
acceptance checklist. No second lifecycle task is registered now.

---

## Readiness Basis

```text
[ x ] Task type confirmed (DOCUMENTATION — TASK_TYPES.md §2)
[ x ] Relevant documentation exists in docs/ (all cited sections verified)
[ x ] MVP scope confirmed (MVP_SCOPE.md §1: Boss Power and the minimum Boss Card
        capability are IN; §3 keeps the fifth Gem FUTURE)
[ x ] Not blocked by an unresolved dependency: TASK-303/304/305/306 are DONE
[ x ] Primary agent assigned (orchestrator)
[ x ] Workflow assigned (documentation-change.md; adr-change.md proposal-only)
[ x ] Acceptance criteria are testable (report + decision table + proposed task)
```

`.ai/PROJECT_STATE.md` recorded HEAD `0551386` and `STALE`; current HEAD is
`94d40d0` (TASK-306 Phase A `ccecba1` + Phase B `94d40d0`), working tree clean.
Because the recorded SHA differs from HEAD, `core/task-selection.md` §2 selects
**INCREMENTAL**, and the delta is exactly the TASK-306 artifact plus its
completion record plus the cache. The recorded lifecycle facts were re-derived
from Git rather than trusted (WIP is empty, `tasks/completed/` holds 308 records,
highest ID = 306). Step 5 requires the cache refresh this task declares.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` §2.5 items 12–15, §2.7 items 17–20, §2.8 items 21–23, §2.9 items 24–27, §3 (U-1…U-6), §4
- `docs/01-game-design/BOSS_RULES.md` §4.1–§4.4, §6.3, §6.3.1, §6.4, §7, §8
- `docs/01-game-design/CARD_RULES.md` §1, §1.1, §3 items 1–9, §3.6, §6
- `docs/01-game-design/GAME_RULES.md` §12, §15, §16, §17 step 18, §18, §20
- `docs/01-game-design/COMBAT_RULES.md` §1.2, §2, §3.4, §4, §5.3
- `docs/02-technical/GAME_STATE.md` §0 item 5, §2.2.2 item 7, §2.4, §2.4.1, §2.4.3, §5.1 item 9
- `docs/02-technical/GAME_EVENTS.md` §1, §2 (`PowerChanged`, `BossSkillCast`, `BossCardCast`), §3 item 8
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.2, §3.2.18, §3.2.24, §3.2.30, §4.4 item 12, §7
- `docs/02-technical/DATABASE.md` §1 (`CardDefinition`, `BossDefinition`), §3, §5 item 4
- `docs/02-technical/ARCHITECTURE.md` §3, §5
- `docs/02-technical/API_CONTRACTS.md` §5.3
- `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md` §2.2
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` item 4 (POWER), item 8, D1/D3/D6
- `docs/00-overview/MVP_SCOPE.md` §1, §3, §4
- `tasks/completed/TASK-303-boss-power-and-boss-card-design-authority-reconciliation.md` §1, §4, §8
- `tasks/completed/TASK-305-adr-029-slice-2-shared-cast-contract-and-actor-relative-effect-resolution.md` §8 (pre-existing failures)
- `tasks/completed/TASK-306-adr-029-slice-boundary-reconciliation-and-boss-card-definition-design.md` and `tasks/artifacts/TASK-306-adr-029-slice-boundary-reconciliation-and-boss-card-definition-design.md` §3–§14

---

## Scope

### In Scope

- Direct verification, against current `docs/` and source, of every TASK-306
  finding and recommendation this reconciliation relies on; correction of
  findings that no longer hold (reported, with evidence).
- One consolidated reconciliation report (declared artifact) containing the
  Boss Card contract, the decision table, the documentation revalidation, the
  reconciled ADR-029 dependency graph and executable Slice 3 exit criteria, the
  proposed single Slice 3 task and its acceptance checklist, and the verdict.
- Closing items that authoritative evidence already settles, and reducing the
  operator decision set to the minimum explicit choices.
- Task-lifecycle bookkeeping: this record, `.ai/PROJECT_STATE.md` refresh per
  `core/task-selection.md` Step 5, quality review, and the two-stage commit
  protocol.

### Out of Scope

- Any runtime, test, migration, provisioning, database, API, SignalR, Redis, or
  frontend change; any `BossAiEngine`, `BattleStateService`, `CardCastExecutor`
  modification (including their comments).
- Any `docs/` or ADR edit. Every candidate change is conditional on operator
  approval of the decision package; the report names the exact patches instead.
- Authoring a `PowerCost`, Shield magnitude, cast-priority score, cooldown,
  fallback weight, or any other balance/product value.
- Creating any additional lifecycle task, including the proposed Slice 3 task,
  Slice 4, Slice 5, or the independent defect items.
- Git history rewriting, fetching, pushing, amending, rebasing or resetting.

---

## Current State

At HEAD `94d40d0`, Slice 1 (`BossState.Power`) and Slice 2 (actor-aware shared
cast contract) are complete. The runtime still executes the legacy response
order: selection is not implemented; the Boss board action runs unconditionally
and the legacy Skill may additionally fire when `SkillCharge >= ChargeRequirement`
and `SkillCooldown == 0` (`BattleStateService.cs:1746-1800`, `:2074-2095`). There
is no `BossActionPolicy` implementation (`ARCHITECTURE.md:804-808` already names
it), no `BossCardCast` enum member/factory/projection, no provisioned Boss Card
row, and no cast-priority score carrier. `CardCategory.BossCard` exists
Domain-only.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-307:

```text
tasks/artifacts/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md
.ai/PROJECT_STATE.md
```

*(The task record itself is excluded from the declared implementation file set
per `tasks/TASK_LIFECYCLE.md` §6.1. No `docs/` file is in this set. No other file
is changed.)*

---

## Acceptance Criteria

- [x] Preflight evidence recorded: branch, HEAD, upstream, working-tree state, task-folder counts, highest ID, duplicate search for TASK-307, and the `PROJECT_STATE` audit-mode decision.
- [x] Every TASK-306 finding this task relies on is re-verified against current `docs/`/source with file-and-line evidence; findings that no longer hold are corrected, and the correction is evidenced.
- [x] One consolidated contract covers Boss Card definition and provisioning, identity, cost, effect, score carrier/comparison/timing/determinism/validation, active form, cooldown ordering, one-action policy, atomicity, and the `BossCardCast` event/wire contract.
- [x] One decision table separates decisions already settled by authority from unresolved operator choices, with the exact decision, evidence, viable options, recommended default and rationale, dependencies, approval requirement, and what each item blocks.
- [x] The operator decision set is reduced to the minimum explicit choices (no item retained merely because TASK-306 listed it); no approval or balance value is invented.
- [x] TASK-306 follow-up proposals P-1…P-9 are dispositioned: compatible work is consolidated into the approval package / one Slice 3 task; independent items are reported without creating tasks.
- [x] The documentation candidate list is revalidated, exact post-approval patches are named, and it is stated why no `docs/` edit is applied now.
- [x] Reconciled ADR-029 dependency graph and executable Slice 3 exit criteria are recorded, including the definition/provisioning prerequisite.
- [x] One proposed Slice 3 end-to-end task scope and acceptance checklist are recorded, not registered.
- [x] Final verdict uses exactly one of the three permitted verdicts and is supported by the evidence.
- [x] No runtime/test/migration/provisioning/API/SignalR/Redis/frontend/`docs/` path changed; changed-file scope scan shows only the declared set plus this record.
- [x] `git diff --check` clean; quality review checklist passes (`quality/review.md` §1); two-stage commit protocol executed and recorded.

---

## Affected Files & Areas

```text
[ ] src/backend/ (read-only inspection; NO change)
[ ] src/frontend/client/ (read-only inspection; NO change)
[ ] tests/ (read-only inspection; NO change)
[ ] docs/ (NO change — post-approval patch plan recorded in the artifact)
[x] tasks/artifacts/ (the reconciliation report)
[x] .ai/PROJECT_STATE.md (navigation-cache refresh only)
```

---

## Implementation Notes

- The report is an artifact, not an authoritative document: every rule
  statement cites the owning `docs/` section or the source location it was
  verified against. It authors no rule, balance value, schema or wire contract.
- Distinguish three states everywhere: **settled by existing authority**,
  **recommended by this task (needs operator approval)**, and **unresolved (no
  value may be assumed)**. ADR-029 §3's U-2…U-6 keep their identifiers.
- TASK-306 is evidence to be verified, not authority. Where it mis-attributed a
  source fact or over-scoped the decision set, this task corrects it and says so.
- The Slice 3 proposal must keep the definition/provisioning work, the policy,
  the event, the client presentation and the round integration inside **one**
  implementation boundary; it must not silently split them into extra tasks.
- If the proposed single-type assignment needs a split under
  `tasks/TASK_TYPES.md` §3, the report must explain why and request approval
  before any extra task is created.

---

## Testing Requirements

### Required Verification

```text
[x] Documentation gates   — git diff --check; changed-file scope scan; every
                            cited section/reference resolved; stale-reference
                            sweep for the claims this task restates.
[x] Lifecycle gates       — declared-set equality at Phase A staging; Phase B
                            single-record staging; ancestry and task-folder
                            counts after filing.
[ ] Unit tests            — N/A: no code changed.
[ ] Integration tests     — N/A: no code changed.
[ ] Gameplay scenarios    — N/A: no code changed; the report states the tests a
                            future slice must add, it does not add them.
```

### Key Edge Cases

- A TASK-306 finding contradicted by current source must be corrected, not
  repeated (`AGENTS.md` §16).
- An authoritative contradiction must be reported with both sources and its
  precedence, not resolved by choosing the easier option (`AGENTS.md` §4).
- An item that existing authority already settles must be closed rather than
  carried forward as an operator decision merely for consistency with TASK-306.

---

## Stop Conditions

- If a required balance or product value cannot be derived from existing
  authority: record it as unresolved and stop at the operator decision gate;
  never manufacture approval (`AGENTS.md` §7).
- If authoritative documents contradict each other: report both sources, their
  precedence under `AGENTS.md` §2, and the smallest correction; do not apply it
  without approval (`AGENTS.md` §4).
- If the work would require editing `docs/`: STOP — that is the
  operator-approval slice, not this task.
- If a source change appears necessary to inspect a contract: STOP; inspection
  is read-only.

---

## Completion Evidence

Reconciliation report delivered; no code, test, migration, provisioning, API,
SignalR, Redis, frontend, `docs/`, or ADR change was made. The task's verdict is
`READY FOR OPERATOR APPROVAL`: the consolidated contract is coherent and
verified, while the remaining product/balance values and the three
authorization choices are operator decisions that no evidence can settle.

Changed files:

- `tasks/artifacts/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md` — the
  consolidated reconciliation report: TASK-306 verification and four corrections,
  the Boss Card / score / initial-card / active-form / cooldown / one-action /
  event contract, the three-item operator decision table, the documentation patch
  plan, the reconciled Slice graph and exit criteria, the single Slice 3 task
  proposal with acceptance checklist, and the verdict.
- `.ai/PROJECT_STATE.md` — navigation-cache refresh at audited HEAD `94d40d0`
  (`core/task-selection.md` Step 5): lifecycle counts, the three-item operator
  gate, the unregistered Slice 3 candidate, and the next action.

### Commit

- Implementation slice (Phase A): `9de577e7592686cc5c6c084b4cc5ecd7f5d710db`
- Subject: `docs: reconcile the adr-029 boss card contract and slice 3 readiness`
- Owns: `TASK-307`
- Files: `tasks/artifacts/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md`, `.ai/PROJECT_STATE.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Validation Results

```text
[x] git diff --check                  clean before Phase A and after Phase A staging
[x] Changed-file scope scan           exactly the two declared files plus this record;
                                      no docs/, src/, tests/ or migration path changed
[x] Phase A staged-set equality       M .ai/PROJECT_STATE.md
                                      A tasks/artifacts/TASK-307-...-readiness.md
                                      zero extraneous paths; record excluded (P-5)
[x] Citation resolution               102 unique file:line citations extracted from the
                                      artifact; 102 resolve to an existing file with an
                                      in-range line; 0 missing / 0 out-of-range
[x] Artifact structure                993 lines; 24 code fences (even); sections 0-10
                                      with §4.2 decision tables and §7 Slice 3 proposal
[x] Section-reference validation      every cited docs/ section spot-verified against its
                                      real heading while reading the owning document
[x] Duplicate-record search           no pre-existing TASK-307 record, artifact, or
                                      partial work anywhere in the repository
[x] Task-folder counts                active 0 -> 1 at intake -> 0 at filing;
                                      completed 308 -> 309 after Phase B
[x] Ancestry                          Phase A `9de577e` descends from `94d40d0` on `master`
[x] Review (quality/review.md §1)     Correctness PASS (citations re-verified, TASK-306
                                      corrections evidenced); Architecture PASS (no change;
                                      proposal keeps BossActionPolicy in Domain, the cast
                                      executor policy-free, one round write-back);
                                      Scope PASS (declared set only); Documentation N/A as
                                      an edit (unapproved, patch plan recorded; cache
                                      refreshed); Security N/A; Performance N/A;
                                      Maintainability PASS (one artifact, no new
                                      abstraction); Determinism PASS (server authority,
                                      zero unseeded RNG, fixed content inputs)
[ ] Unit / integration / gameplay     N/A — no code, test, or migration changed; no test
                                      result could differ (same treatment TASK-306 recorded)
```

### Server Authority & Scope Verification

- [x] No client-authoritative gameplay logic added (no code changed)
- [x] Adherence to MVP Scope checked (`MVP_SCOPE.md` §1) — no scope move beyond ADR-029's approved boundary
- [x] P-1…P-6 commit policy compliance — single `master` branch, no per-task branch or tag (P-1); one Phase A commit plus one Phase B commit (P-2); Phase A authorized by the active manifest and its review gates, Phase B files the record only (P-3); task ID in the commit body only, never the subject (P-4); both staged sets verified exactly (P-5); no amend, rebase, force push, `git commit -a`, or batched completion records (P-6)
