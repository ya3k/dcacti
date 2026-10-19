# TASK-306 — ADR-029 Slice Boundary Reconciliation and Boss Card Definition Design

<!--
  GEN-TASK EXECUTION MANIFEST — STATUS: IN PROGRESS

  Design-and-documentation task only. It resolves the design and dependency
  blockers identified by the TASK-306 Slice 3 readiness assessment and produces
  an operator-reviewable design decision report. It writes NO runtime code, no
  tests, no migrations, no provisioning, no wire changes and no frontend change.

  Authority boundary: ADR-029 already decided the *structure* of the Boss action
  policy. It did NOT decide (a) the Boss Card definition/persistence carrier,
  (b) the U-3 cast-priority score's authored form/values, (c) the first Boss
  Card's authored cost/effect values, or (d) the active-form migration carrier.
  Those four are operator decisions; this task prepares them and MUST NOT apply
  them to authoritative documents before approval (AGENTS.md §7, §20).

  Task created in tasks/active/ because the requiring brief assigned it directly
  and its readiness basis was verified in-session (see §1): docs exist, MVP scope
  is confirmed, dependencies TASK-303/304/305 are DONE, and no duplicate record
  exists. BACKLOG -> READY -> IN PROGRESS was executed as one verified step, the
  same basis TASK-304 recorded.
-->

---

## Metadata

```text
Task ID:           TASK-306
Type:              DOCUMENTATION
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     orchestrator
Supporting Agents: gameplay, backend, persistence, client, testing, review
Workflow:          documentation/documentation-change.md
                   (+ architecture/adr-change.md — PROPOSAL ONLY, no ADR edit)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   gameplay/authority-determinism-audit,
                   quality/documentation-consistency,
                   quality/scope-validation,
                   realtime/realtime-protocol-validation,
                   backend/persistence-analysis
Dependencies:      TASK-303 (DONE — authorized ADR-029 and the documented target
                   state), TASK-304 (DONE — BossState.Power foundation),
                   TASK-305 (DONE — actor-aware cast contract), ADR-029
                   (Accepted), ADR-024, ADR-026
Declared Files:    tasks/artifacts/TASK-306-adr-029-slice-boundary-reconciliation-and-boss-card-definition-design.md
                   .ai/PROJECT_STATE.md
```

**Lifecycle path executed:**

```text
BACKLOG -> READY -> IN PROGRESS   (this session, readiness basis recorded in §1
                                   and in the design report §1)
IN PROGRESS -> IN REVIEW          (report drafted, evidence verified)
IN REVIEW -> DONE                 (two-stage commit protocol, TASK_LIFECYCLE.md §6)
```

---

## Objective

Resolve the design and dependency blockers that the TASK-306 Slice 3 readiness
assessment identified, before runtime implementation is authorized, by producing
one evidence-based, operator-reviewable design decision report covering: the Boss
Card definition source and provisioning authorization (Decision A), the U-3
cast-priority score contract (Decision B), one initial Boss Card vertical slice
with all balance values exposed (Decision C), the legacy-Skill-versus-Boss-Card
active-form migration contract (Decision D), the remaining action-selection and
cooldown ambiguities (Decision E), and a reconciled ADR-029 slice graph with
executable exit criteria (Decision F). The task states a verdict of
`READY FOR OPERATOR APPROVAL` (design), `NEEDS FURTHER DESIGN`, or `BLOCKED`
while runtime implementation remains blocked.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` §2.3–§2.7, §2.9, §3 (U-2…U-6), §4 (slice plan and ordering constraints)
- `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md` §2.2 (Domain baseline + PostgreSQL overlay, fail-closed validation)
- `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md` item 4 (POWER bullet), item 8 (deterministic board-action scoring), D1/D3/D6
- `docs/01-game-design/BOSS_RULES.md` §4.1–§4.4 (Boss Power, Boss Card capability, action policy, staged migration), §6.3, §6.3.1, §6.4 (timing, effect magnitudes, identities)
- `docs/01-game-design/CARD_RULES.md` §1, §1.1 (categories, Boss Card), §3 items 1–9 (cast contract, actor-relative split), §3.6 (`EffectiveCardCost`), §6
- `docs/01-game-design/GAME_RULES.md` §12, §15, §16, §17 step 18 (one-action policy, event names, response-phase order), §20
- `docs/01-game-design/COMBAT_RULES.md` §1.2, §2, §4 (Shield contract), §5.3 (duration consumption), §6
- `docs/02-technical/GAME_STATE.md` §2.2.2 item 7, §2.4, §2.4.1, §2.4.3 (`Power`, `SkillCharge`, `SkillCooldown` carriers), §5.1 item 9 (single write-back)
- `docs/02-technical/GAME_EVENTS.md` §2 (`CardCast`, `BossSkillCast`, `BossCardCast`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.2, §3.2.30, §4.4 item 12, §7
- `docs/02-technical/DATABASE.md` §1 (`CardDefinition`, `BossDefinition`, `BossSkillDefinition`), §3 (stored-value constraints)
- `docs/02-technical/ARCHITECTURE.md` §2.2.2, §3 (component ownership)
- `docs/00-overview/MVP_SCOPE.md` §1, §3, §4
- `tasks/completed/TASK-303-…-reconciliation.md` §4 D2/D3/D5, §5, §11
- `tasks/completed/TASK-304-boss-power-slice-1-implementation-and-verification.md` §1, §7
- `tasks/completed/TASK-305-adr-029-slice-2-shared-cast-contract-and-actor-relative-effect-resolution.md` §Scope, §"Scope Decisions Taken by the Readiness Pass", §8.1

---

## Scope

### In Scope

- Direct, file-and-line source inspection of every contract this task decides
  on: the Boss response phase, `BossAiEngine`, `BossState`, `CardCastExecutor`'s
  Boss branch, the card/boss definition carriers, the provisioning path, the
  persistence constraints, the event/projection surface, and the tests that pin
  the legacy behaviour.
- A written design decision report (declared artifact) covering Decisions A–F
  and the twelve required report sections, including explicit
  decisions-versus-unresolved-operator-decisions separation.
- A proposed, non-applied documentation change list (exact files and sections)
  and a proposed follow-up task graph with dependencies and exit criteria.
- Verification of legal values for any proposed contract parameter (type, range,
  validation behavior) WITHOUT inventing a balance value.
- Task-lifecycle bookkeeping: the task record itself, `.ai/PROJECT_STATE.md`
  refresh per `core/task-selection.md` Step 5, and the two-stage commit protocol.

### Out of Scope

- Any runtime, test, migration, provisioning, database, API, SignalR, Redis, or
  frontend change; any `BossAiEngine`, `BattleStateService`, `CardCastExecutor`
  modification.
- Applying any Decision A/B/C/D recommendation to `docs/` — those await operator
  approval; this task may only *propose* the exact edits.
- Authoring a `PowerCost`, cast-priority score, Shield magnitude/duration,
  cooldown, or any fallback weight.
- Effect-vocabulary extension and migration of any Boss ability.
- Retiring `SkillCharge`, `BossSkillCast`, or the legacy secondary-effect handler.
- The fifth Gem / Power-drain Gem, unrelated comment/test/protocol-numbering debt,
  and Git history rewriting (no fetch/push/rebase/amend/reset).

---

## Current State

At HEAD `0551386` (`docs: file the adr-029 slice 2 shared cast contract completion
record`), Slice 1 (Boss Power) and Slice 2 (actor-aware cast contract) are
complete and committed. The runtime still executes the legacy response order
(board action then Skill cast when `SkillCharge >= ChargeRequirement`), still
carries `SkillCooldown`, emits `BossSkillCast`, and has no Boss Card definition
row, no cast-priority score, and no `BossCardCast` emission. The Boss Card
category exists Domain-only (`CardCategory.BossCard`) with no persistence
contract, exactly as TASK-305 left it.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-306:

```text
tasks/artifacts/TASK-306-adr-029-slice-boundary-reconciliation-and-boss-card-definition-design.md
.ai/PROJECT_STATE.md
```

*(The task record itself is excluded from the implementation declared file set,
per `tasks/TASK_LIFECYCLE.md` §6.1. `.ai/PROJECT_STATE.md` is included because
`core/task-selection.md` Step 5 requires the navigation cache to be refreshed
after a selection/audit run, and the cache is currently materially stale — it
records HEAD `1d62313`, an active TASK-263, and a dirty tree, none of which are
true. No authoritative `docs/` file is in this set.)*

---

## Acceptance Criteria

- [x] Repository and task-lifecycle evidence recorded: branch, HEAD, upstream, working tree, task-folder counts, highest ID, duplicate search for TASK-306, and the project-state freshness/deep-mode decision.
- [x] Every disputed contract verified by direct source inspection with file-and-line evidence; the readiness report (absent from the repository) is not treated as evidence.
- [x] Decision A: options A/B/C compared on identity, cost/effect ownership, provisioning, DB constraints/documentation consistency, runtime lookup and reconnect determinism, Player compatibility, five-ability migration, scope/maintenance/duplicate-source risk; one recommendation with every affected schema/document/provisioning contract named.
- [x] Decision B: U-3 score contract states type, range, ownership/load location, comparison to the board candidate score, post-deadlock-recovery timing, deterministic tie-break, RNG consumption, and missing/invalid-data validation — with every authored value it requires marked unresolved.
- [x] Decision C: `Earthquake → Shield` verified against the authoritative effect vocabulary and the Shield contract; identity, category, cost, effect type/value type/target/magnitude, duration/expiry, cooldown interaction, TASK-305 cast-contract executability, and required tests stated; every unapproved balance value marked unresolved; any precise vocabulary mismatch reported instead of extended.
- [x] Decision D: explicit one-active-form rule, selection carrier, legacy path for unmigrated Bosses, enablement scope, duplicate-execution prevention, cooldown interpretation during the window, and a migration test strategy that does not retire `SkillCharge`/`BossSkillCast`/the legacy handler; `SkillCooldown` is not used as an ability-identity or migration-state signal.
- [x] Decision E: board score versus cast score, deadlock-recovery ordering, board-wins-on-tie, the "neither action available" branch, cooldown decrement/check/set ordering, cast-phase consequences, and CAS-retry determinism each reconciled with source; contradictions reported per AGENTS.md §4 rather than resolved silently.
- [x] Decision F: revised ADR-029 dependency graph and per-slice executable exit criteria, distinguishing domain fixtures from provisioned-runtime integration, real-event client presentation, full ability migration, and legacy retirement.
- [x] Report contains all twelve required sections, an explicit decisions-versus-unresolved table, the exact proposed documentation change list, the proposed follow-up task graph, and a single verdict.
- [x] No runtime/test/migration/provisioning/API/SignalR/frontend file changed; no balance value invented or recorded as a rule.
- [x] `git diff --check` clean; changed-file scope scan shows only the declared set plus the task record; section references in the report validated against the cited documents.
- [x] Quality review checklist passes (`quality/review.md` §1); two-stage commit protocol executed and recorded.

---

## Affected Files & Areas

```text
[ ] src/backend/ (read-only inspection; NO change)
[ ] src/frontend/client/ (read-only inspection; NO change)
[ ] tests/ (read-only inspection; NO change)
[x] tasks/artifacts/ (the design report)
[x] .ai/PROJECT_STATE.md (navigation-cache refresh only)
[ ] docs/ (NO change in this task — a proposed change list is produced, not applied)
```

---

## Implementation Notes

- The report is an artifact, not an authoritative document: no game rule, wire
  contract, or schema is authored there. Every rule statement must cite the
  owning document/section or the source location it was verified against.
- Distinguish three states everywhere: **decided by existing authority**,
  **recommended by this task (needs operator approval)**, and **unresolved
  (no value may be assumed)**. ADR-029 §3's U-2…U-6 keep their identifiers.
- The U-3 contract may define the score's *type, range and comparison semantics*
  only; the per-Boss values and their carrier's authored columns remain an
  operator decision if the documents do not already name the carrier.
- The initial Boss Card proposal must not introduce a value that does not already
  exist in the repository; where an existing implemented value is found, cite it
  and mark it as *implemented-but-unrecorded*, not as approved.
- The slice graph must state the **definition/content/provisioning prerequisite**
  explicitly; Slice 3 is not end-to-end complete while the production runtime has
  no Boss Card definition to execute.

---

## Testing Requirements

### Required Verification

```text
[x] Documentation gates   — git diff --check; changed-file scope scan;
                            cross-reference validation against cited sections;
                            stale-reference sweep for touched claims.
[x] Lifecycle gates       — declared-set equality at Phase A staging; Phase B
                            single-record staging; ancestry and task-folder
                            counts after filing.
[ ] Unit tests            — N/A: no code changed.
[ ] Integration tests     — N/A: no code changed.
[ ] Gameplay scenarios    — N/A: no code changed; the report states the tests a
                            future slice must add, it does not add them.
```

### Key Edge Cases

- A defect in the current source or docs must be reported, not fixed
  (`AGENTS.md` §16, `tasks/README.md` §9).
- An authoritative contradiction must be reported with both sources, not
  resolved by choosing one (`AGENTS.md` §4).

---

## Stop Conditions

- If a required balance or product value cannot be derived from existing
  authority: record it as unresolved and stop at the operator decision gate;
  never manufacture approval (`AGENTS.md` §7).
- If authoritative documents contradict each other: report both sources and stop
  (`AGENTS.md` §4).
- If producing the report would require editing `docs/`: STOP — that is the
  operator-approval slice, not this task.
- If a source change appears necessary to inspect a contract: STOP; inspection
  is read-only.

---

## Completion Evidence

Design report delivered; no code, test, migration, provisioning, API, SignalR,
Redis, frontend, or `docs/` change was made. The task's runtime work remains
operator-gated by design — the report's verdict is `READY FOR OPERATOR APPROVAL`
for the design and `BLOCKED` for runtime implementation on the decision set in
report §10.2 (U-2, U-3, U-4, U-7…U-12).

### Commit

- Implementation slice (Phase A): `ccecba130012fdce8da454e54c82b9333315d840`
- Subject: `docs: record ADR-029 slice boundary reconciliation and Boss Card design`
- Owns: `TASK-306`
- Files: `tasks/artifacts/TASK-306-adr-029-slice-boundary-reconciliation-and-boss-card-definition-design.md`, `.ai/PROJECT_STATE.md`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Validation Results

```text
[x] git diff --check                  clean before Phase A and after Phase B staging
[x] Changed-file scope scan           exactly the two declared files; task record excluded
[x] Phase A staged-set equality       M .ai/PROJECT_STATE.md
                                      A tasks/artifacts/TASK-306-...-design.md
                                      zero extraneous paths (P-5)
[x] Section-reference validation      75 `docs/*.md` "section §N" references resolved
                                      against real headings in the cited document;
                                      plus .ai/workflow/core/task-selection.md §2,
                                      tasks/TASK_TYPES.md §2 and §3, and
                                      documentation-change.md §2 — 0 unresolved
[x] Artifact structure               1071 lines; 20 code fences (even); sections 0–14
                                      with the twelve required sections indexed in §0
[x] Duplicate-record search          no pre-existing TASK-306 record, no readiness
                                      assessment artifact present — recorded as absent
[x] Task-folder counts               active 1 -> 0; completed 307 -> 308 after filing
[x] Ancestry                         Phase A descends from 0551386 on `master`
[x] Documentation gates              no `docs/` file staged; the exact change list is
                                      proposed in report §11 and not applied
[ ] Unit / integration / gameplay    N/A — no code, test, or migration changed
```

### Server Authority & Scope Verification

- [x] No client-authoritative gameplay logic added (no code changed)
- [x] Adherence to MVP Scope checked (`MVP_SCOPE.md` §1) — no scope move proposed outside ADR-029's approved boundary
- [x] P-1…P-6 commit policy compliance — single `master` branch, no per-task branch or
      tag (P-1); one Phase A commit plus one Phase B commit (P-2); Phase A authorized
      by the active manifest and its review gates, Phase B files the record only (P-3);
      task ID in the commit body only, never the subject (P-4); both staged sets verified
      exactly (P-5); no amend, rebase, force push, `git commit -a`, or batched
      completion records (P-6)

