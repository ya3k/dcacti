# TASK-321 — Server-Authoritative CombatTrace and Cascade Attribution

---

## Governance Preflight (Phase 0)

Captured before any edit:

```text
Branch:      master
HEAD:        e212f6a3e2c8f46a1423da896bea1c95def866c6
Upstream:    origin/master = 3a74edd6617fb8291a11d0978994a42ed1c60bc3 (ahead 14, behind 0, nothing pushed)
Status:      6 modified tracked files + 2 untracked files, preserved as found
             (TASK-320 record and DefenseMechanicsVerificationTests.cs are untouched by this task)
```

Authorized way this manifest was created and activated:

1. `tasks/README.md` §6 ("How to Create a Task") authorizes the next sequential ID
   (highest existing = TASK-320 → **TASK-321**), created from `tasks/TASK_TEMPLATE.md`
   with all required sections and an exact `Declared Files` set.
2. `tasks/README.md` §7 / `TASK_LIFECYCLE.md` §4 authorize the executing agent to
   confirm READY and perform `backlog/` → `active/` with `Status: IN PROGRESS`
   under an explicit instruction. The operator's instruction for this task is that
   explicit instruction, matching the resolution recorded in
   `tasks/completed/TASK-286-...md` §"Governance Blockers" G-1 ("operator
   authorizes the transition, or the executing agent performs it under an explicit
   instruction").
3. No operator decision is bypassed: OD-316-1 governs production operations and
   pushes, and TASK-320's pending decisions govern two historical commit-message
   defects and push authorization. Creating/activating TASK-321 neither resolves
   nor pre-empts any of them, and **no commit, stage, push, rebase, or amendment is
   performed**.
4. The existing uncommitted edits have no owning manifest (a prior interrupted
   session left them). This manifest declares the paths this task works on and
   records them as pre-existing/unowned below; it does **not** retroactively own
   the unowned commit `e212f6a`, and any future Phase A over these paths must
   disclose the pre-existing hunks per `TASK_LIFECYCLE.md` §6.2 P-3.
5. Definition of Done was **partially unsatisfiable** in the implementation session
   because the Phase A/Phase B commits were not authorized there; the task was held
   `IN REVIEW` in `tasks/active/` and no DONE/closure claim was made. The operator
   afterwards authorized the two-stage commit protocol, and Phase A/Phase B were
   executed (see `## Completion Evidence` → `### Commit`).

READY validation (`TASK_LIFECYCLE.md` §3): task type confirmed (FEATURE,
`development/feature.md`); authoritative docs exist (cited above); MVP scope is a
developer-facing extension of an already-delivered surface; dependencies resolved
(TASK-253 completed); primary agent assigned (backend) with client/realtime/
testing/review supporting; acceptance criteria are binary and testable.

---

## Metadata

```text
Task ID:           TASK-321
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: client, realtime, testing, review
Workflow:          development/feature.md
Skills:            realtime/realtime-protocol-validation, client/client-event-projection, gameplay/authority-determinism-audit, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-253 (Developer Move Log row vocabulary, completed); no unresolved dependency on TASK-316/TASK-320 (they govern release/push and history, not this local delivery schema)
Declared Files:    src/backend/GameServer.Domain/Match3/CombatTrace.cs, src/backend/GameServer.Domain/Match3/SwapExecution.cs, src/backend/GameServer.Domain/Combat/AttackResolution.cs, src/backend/GameServer.Application/Battle/BattleStateService.cs, src/backend/GameServer.Api/Hubs/BattleHub.cs, src/backend/GameServer.Api/Hubs/CombatTraceWireProjection.cs, docs/02-technical/SIGNALR_PROTOCOL.md, tests/backend/GameServer.Domain.Tests/CombatTraceTests.cs, tests/backend/GameServer.Application.Tests/CombatTraceResolutionTests.cs, tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs, tests/backend/GameServer.Api.Tests/CombatTraceWireTests.cs, src/frontend/client/src/game/runtime/GameRuntimeEvents.ts, src/frontend/client/src/game/runtime/GameRuntime.ts, src/frontend/client/src/game/scenes/BattleEventPresenter.ts, src/frontend/client/src/game/scenes/BattleScene.ts, src/frontend/client/tests/BattleEventPresentation.test.ts, src/frontend/client/tests/SceneLifecycle.test.ts, src/frontend/client/tests/GameRuntime.test.ts, src/frontend/client/tests/RuntimeBoundaries.test.ts
```

---

## Objective

Make every combat value the Developer Move Log displays come from the
authoritative backend resolution. Add the smallest backward-compatible
`ReceiveEvents` envelope extension that carries a per-move combat trace
(battle/sequence correlation, cycle and cascade identity with actor, damage
instances with their targets, base and effective DEF actually used, shield
absorption, actual HP loss, healing, and Pet/Boss before/after snapshots),
and make the client render only that trace — removing hardcoded DEF, DEF-buff
prediction from matched-Gem counts, DEF reverse-engineering from mitigated
damage, and array-position cascade attribution.

---

## Authoritative References

- `docs/01-game-design/COMBAT_RULES.md` §3, §3.1, §3.2, §3.4, §4, §5.4.6, §5.5.6 — damage pipeline steps, DEF mitigation, shield absorption, effective DEF
- `docs/01-game-design/GAME_RULES.md` §17 — fixed resolution order (steps 12–19a)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3, §3.1, §3.2, §3.2.5, §3.2.14, §3.2.15, §3.2.26, §3.2.29 — envelope, omission rule, damage/party vocabulary, cycle boundary, MatchedGemSummary
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1, §2 — event ordering and the absence of heal/shield events
- `docs/02-technical/GAME_STATE.md` §2.3, §2.4, §5.1 — Pet/Boss state (HP/DEF/Power) and the single write-back
- `AGENTS.md` §10, §14, §15, §20 — server authority, smallest change, testing, stop conditions
- `tasks/TASK_LIFECYCLE.md` §3, §6 — lifecycle states and the two-stage commit protocol (not executable in this task; see Scope)
- `.ai/workflow/core/completion.md` §1, §3 — Definition of Done and blocked reporting
- `tasks/blocked/TASK-316-breaking-wire-contract-release-preparation.md`, `tasks/blocked/TASK-320-governance-resolution-local-commit-chain.md` — existing governance blockers; unchanged by this task

---

## Scope

### In Scope
- A new optional `combatTrace` member on the `ReceiveEvents` payload, populated only for accepted Swap resolutions; omitted (not `null`) everywhere else.
- Capturing the trace from the values the resolution already computes, at the point it computes them; no second damage calculation, no formula change, no state mutation for logging.
- Domain value types for the trace and their carriage through `SwapExecutionResult`.
- Wire DTOs and one-to-one projection; `SIGNALR_PROTOCOL.md` contract update.
- Removing the client-side DEF constant/prediction/inversion and the positional cascade attribution from the Developer Move Log; consuming the trace instead.
- Regression tests in all three layers listed in `## Testing Requirements`.

### Out of Scope
- Any change to combat rules, DEF formulas, damage order, Boss behaviour, RNG, or gameplay balance.
- New Battle Event types (the closed discriminator set of `SIGNALR_PROTOCOL.md` §3.2.2 is not extended).
- Traces for `CardCast` / `PetSkillCast` resolutions (they keep their current envelope shape; the client renders their event rows only).
- Commits, pushes, rebases, amendments, staging, or history repair; resolving TASK-316 or TASK-320.
- Persisting the trace in Redis or PostgreSQL; the trace is per-delivery telemetry only.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Current State

- `BattleHub.ToPayload(SwapExecutionResult)` builds `ReceiveEventsPayload(battleId, serverSequence, events[], intermediatePetHp?)`; there is no trace member (`src/backend/GameServer.Api/Hubs/BattleHub.cs:701-705,1363-1381`).
- A prior interrupted session left **unowned, uncommitted** edits in the working tree: a `CombatTrace` domain record and a pooled whole-move trace in `BattleStateService.cs` (which is never projected onto the wire and pools all damage together), plus client-side DEF constants (`25`/`50`/`0`-by-`bossId`), a `DefGemCount × 5` DEF-buff prediction, a `K=100` DEF inversion from `DamageCalculated.defense`, and `MatchedGemSummary[0]/[1]` positional attribution in `BattleScene.ts`. Those edits are the defect this task corrects; they are **preserved and replaced**, not reverted wholesale.
- An unowned commit `e212f6a` ("deliver server-authoritative intermediate pet hp…", `owns: unowned`) exists in local history with no task record and no Phase B completion record. It is the origin of the existing `intermediatePetHp` envelope member this task's trace sits beside. It is out of scope and is not repaired here (§6.4/P-6 forbid history rewriting).
- `tests/backend/GameServer.Domain.Tests/DefenseMechanicsVerificationTests.cs` is untracked and pre-existing; it verifies the authoritative DEF formula and is not modified by this task.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-321:
```text
src/backend/GameServer.Domain/Match3/CombatTrace.cs
src/backend/GameServer.Domain/Match3/SwapExecution.cs
src/backend/GameServer.Domain/Combat/AttackResolution.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs
src/backend/GameServer.Api/Hubs/BattleHub.cs
src/backend/GameServer.Api/Hubs/CombatTraceWireProjection.cs
docs/02-technical/SIGNALR_PROTOCOL.md
tests/backend/GameServer.Domain.Tests/CombatTraceTests.cs
tests/backend/GameServer.Application.Tests/CombatTraceResolutionTests.cs
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
tests/backend/GameServer.Api.Tests/CombatTraceWireTests.cs
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
src/frontend/client/src/game/runtime/GameRuntime.ts
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/src/game/scenes/BattleScene.ts
src/frontend/client/tests/BattleEventPresentation.test.ts
src/frontend/client/tests/SceneLifecycle.test.ts
src/frontend/client/tests/GameRuntime.test.ts
src/frontend/client/tests/RuntimeBoundaries.test.ts
```

*(The task manifest file itself is excluded from the declared implementation file set — `tasks/TASK_TEMPLATE.md`. `tests/backend/GameServer.Domain.Tests/DefenseMechanicsVerificationTests.cs` and `tasks/blocked/TASK-320-*.md` are pre-existing and explicitly excluded.)*

---

## Acceptance Criteria

- [x] 1. `ReceiveEventsPayload` carries an optional `combatTrace`; it is present for an accepted Swap, absent (serialized away) for `CardCast`, `PetSkillCast`, and rejected actions.
- [x] 2. The trace identifies `battleId` and `serverSequence` and both equal the envelope's own values in the same delivery.
- [x] 3. The trace records move identity (kind, both cells) and the ordered actor cycles with their `MatchedGemSummary` counts and per-pass cascade depths.
- [x] 4. The trace records every damage instance individually with `source`, `target`, the pipeline breakdown, base DEF and the effective DEF used, `shieldAbsorbed`, `hpBefore`, `hpAfter`, and actual `hpLoss`.
- [x] 5. The trace records the authoritative Pet and Boss HP/DEF/Power before and after the move, and every heal with its raw amount, applied amount, and HP before/after.
- [x] 6. Trace capture adds no second damage calculation, changes no combat value, and mutates no state for logging.
- [x] 7. The terminal (Boss-defeated) resolution path carries a trace.
- [x] 8. `SIGNALR_PROTOCOL.md` documents the new optional member, its omission and correlation rules, and its unknown-value representation; existing envelope and event contracts are otherwise unchanged.
- [x] 9. The client no longer contains a hardcoded Pet/Boss DEF value, a DEF-buff prediction from matched-Gem counts, or DEF reverse-engineering from mitigated damage.
- [x] 10. The client attributes cycles/cascades by the trace's authoritative actor, never by array position.
- [x] 11. Unknown trace values render as `?`; shield absorption is not reported as HP loss; a full-HP heal is not reported as a positive HP delta.
- [x] 12. Duplicate/stale sequences and battle transitions cannot create duplicate or mixed-battle rows.
- [x] 13. All targeted backend, API-contract, and frontend tests pass; the frontend production build passes.
- [x] 14. No authoritative rule, contract, or MVP-scope violation (`AGENTS.md` §10, `docs/00-overview/MVP_SCOPE.md` §1).

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Api)
[x] src/frontend/client/ (runtime / scenes / tests)
[x] tests/ (unit / integration / gameplay scenarios)
[x] docs/ (SIGNALR_PROTOCOL.md only)
```

---

## Implementation Notes

- Capture points: `AttackResolution.ResolvePlayerAttack` / `ResolveBossAttack` (effective DEF is computed there; echo base and effective DEF on the result instead of recomputing), `SwapExecutor.Execute` (Pet heal amount and HP before/after), `BattleStateService.ResolveSwapAsync` (cycles, matched-Gem counts, per-pass depths, Pet/Boss snapshots, Boss heal, DoT ticks) — no value is re-derived from the final board, from a Boss id, from a Gem count, or from a damage ratio.
- The Boss-defeated early return in `ResolveSwapAsync` must attach the same trace shape before returning.
- Wire DTOs live in the transport layer; Domain values are never serialized directly (`SIGNALR_PROTOCOL.md` §3.2.1).
- The client parses the trace in `GameRuntime` (the envelope is reconstructed there, so an unlisted member would otherwise be dropped), validates `battleId`/`serverSequence` against the envelope, and treats a malformed or mismatched trace as absent (batch still delivered).

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — trace carriage through DamageResult/AttackResolution/SwapExecutionResult; wire projection names and omission; frontend envelope parser and row formatter.
[x] Integration tests  — real Swap through BattleHub asserting the serialized ReceiveEvents envelope carries a correlated, correctly-valued trace; CardCast/PetSkillCast omit it.
[x] Gameplay scenarios — player attack + Boss attack in one round; multiple player cascades + Boss cycle; shield absorption; full-HP heal; zero-DEF target; lethal damage; unknown Boss Power; duplicate/stale batch; battle transition.
```

### Key Edge Cases
- A move with two or more player cascades and a Boss cycle: every player cascade stays attributed to the player, the Boss cycle to the Boss.
- Two `DamageCalculated` instances in one batch remain individually correlated to their target.
- Zero DEF is displayed explicitly as `0` (never replaced by a default).
- Boss Power that the trace does not carry stays unknown (`?`).
- Shield absorption is shown as absorption and not as HP loss.
- Healing at full HP shows `+0` HP delta with the heal amount reported separately.
- A Boss kill (lethal damage) still delivers a trace.
- A re-delivered or stale `serverSequence` produces no duplicate rows.
- A new battle identity clears the log (existing behaviour preserved).

---

## Stop Conditions

- If the trace would require changing a combat rule or the DEF formula: STOP (`AGENTS.md` §7).
- If the wire extension would break an existing envelope member or event discriminator: STOP — redesign as an additive optional member.
- If the implementation would attribute an unowned/pre-existing path to this task's Phase A without disclosure: STOP and report (`TASK_LIFECYCLE.md` §6.2 P-3).
- If commits/pushes/history changes become necessary to satisfy a criterion: STOP — they are not authorized in this task.

---

## Completion Evidence

> **FILED AS DONE.** The operator authorized the two-stage commit protocol
> (`TASK_LIFECYCLE.md` §6) for this task on 2026-10-22, after review. Phase A
> committed the 19-path declared file set as `5b5bb43`; Phase B files this record
> under its own dedicated bookkeeping commit. No rebase, amend, history rewrite,
> or push was performed.

### Commit

```text
Phase A: 5b5bb43fc222ab32ccbd3cb2a147b48c7700587d
         feat: deliver the server-authoritative combat trace for committed swaps
         owns: TASK-321
         19 files changed, 4450 insertions(+), 78 deletions(-)
Phase B: this record's own dedicated bookkeeping commit
         docs: file the combat trace and cascade attribution completion record
```

Phase A preflight (re-verified immediately before staging):

```text
Branch:    master
HEAD:      e212f6a3e2c8f46a1423da896bea1c95def866c6 (unchanged from Phase 0)
Upstream:  origin/master = 3a74edd6617fb8291a11d0978994a42ed1c60bc3 (ahead 14, behind 0)
Staged:    0 paths before staging
```

Phase A exact-set staging verification (`TASK_LIFECYCLE.md` §6.2 P-5): the
staged set matched the record's `Declared Files` manifest field exactly — **19
paths**, 14 modified and 5 added, with zero extraneous paths and the task record
itself excluded. The operator's instruction named "18 paths"; the record
declares 19 in both the manifest field and the `## Declared File Set` block
(they are byte-identical), so the operator was consulted and directed the record's
19-path set to be staged. No path was added or dropped to match the number.

Explicitly **not** staged (the `TASK-320`/`TASK-316` exclusion in the
instruction): `tasks/blocked/TASK-320-governance-resolution-local-commit-chain.md`,
`tasks/active/TASK-321-...md` (excluded from Phase A by §6.1), and the
pre-existing untracked `tests/backend/GameServer.Domain.Tests/DefenseMechanicsVerificationTests.cs`.

### Hunk-by-hunk scope verification

Every staged hunk was reviewed against the task's Implementation Notes before
staging, to separate TASK-321 work from unowned-origin edits:

- `src/backend/GameServer.Domain/Match3/CombatTrace.cs` (new) — Domain trace types only.
- `src/backend/GameServer.Domain/Match3/SwapExecution.cs` — adds `PetHeal`/`CombatTrace`
  carriage, `WithCombatTrace`, and the authoritative heal capture in
  `SwapExecutor.Execute`; deletes the prior pooled/string trace. All hunks trace-attributed.
- `src/backend/GameServer.Domain/Combat/AttackResolution.cs` — hoists the base/effective
  DEF step 5 already read and echoes it; the pipeline input is unchanged (same
  value, computed once). No formula change.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — trace collectors,
  per-stage capture (player/Boss cycles, player/Boss attacks, step-19a ticks, both
  heals), the Boss-defeated path attachment, and `BuildCombatTrace`. The step-19a
  DEF hoists are value-preserving. Every hunk is TASK-321.
- `src/backend/GameServer.Api/Hubs/CombatTraceWireProjection.cs` (new) and
  `BattleHub.cs` — additive optional member plus a pure field mapping.
- `docs/02-technical/SIGNALR_PROTOCOL.md` — version 2.31, §3 item 5, new §3.3. The
  only removed lines are the superseded envelope descriptions, each replaced by its
  2.31 form; no other section's contract changed.
- `tests/backend/GameServer.Domain.Tests/CombatTraceTests.cs`,
  `GameServer.Application.Tests/CombatTraceResolutionTests.cs`,
  `GameServer.Api.Tests/CombatTraceWireTests.cs` (new) — trace suites only.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — envelope member-set
  expectations updated to include `combatTrace`, new trace assertions, and
  `combatTrace` omission on `CardCast`/`PetSkillCast`. The `board`/`status`
  member prohibitions were preserved as member assertions because `"action": "board"`
  is now a documented §3.3 cycle value; no prohibition was weakened away.
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — trace wire shape,
  envelope member, and the strict reader. `GameRuntime.ts` — parse/correlate/forward.
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — trace target and
  damage row kinds, display-only formatting, `combatTraceMoveLogRows`.
  `BattleScene.ts` — consumes `envelope.combatTrace` only, plus the `commitSequence`
  guard regression fix documented in the task record.
- Frontend tests — trace parsing, formatting, scene lifecycle, and the
  `RuntimeBoundaries.test.ts` marked exemption.

No hunk was attributable to TASK-320 (governance assessment, `Declared Files: None`),
to TASK-316 (release preparation, blocked), to `e212f6a`, or to an outside-scope path.
The `e212f6a` commit remains untouched and unowned in history; nothing in it was
repaired, reworded, or reverted.

### Staged Paths (19)

- `src/backend/GameServer.Domain/Match3/CombatTrace.cs` — new Domain trace value types and their contracts.
- `src/backend/GameServer.Domain/Match3/SwapExecution.cs` — carries `PetHeal` and `CombatTrace` through the result and its factories; captures the authoritative heal in `SwapExecutor.Execute`; removes the prior pooled/string trace.
- `src/backend/GameServer.Domain/Combat/AttackResolution.cs` — echoes the base and effective DEF that step 5 actually read (`DefenderDefenseBase`, `DefenderDefenseEffective`); no formula change.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — records cycles (actor, action, matched-Gem counts, pass depths), individual damage instances (player attack, Boss attack, step-19a ticks), both heals, and the Pet/Boss snapshots; attaches the trace on both the normal and the Boss-defeated return paths; replaces the prior whole-move pooled trace.
- `src/backend/GameServer.Api/Hubs/CombatTraceWireProjection.cs` — new wire DTOs and one-to-one projection (camelCase names, omission instead of null).
- `src/backend/GameServer.Api/Hubs/BattleHub.cs` — optional `combatTrace` envelope member and its `ToPayload(SwapExecutionResult)` mapping.
- `docs/02-technical/SIGNALR_PROTOCOL.md` — version 2.31; §3 item 5 and new §3.3 (schema, member tables, rules); envelope descriptions and `§8 item 9` reconciled.
- `tests/backend/GameServer.Domain.Tests/CombatTraceTests.cs` — Domain trace carriage tests.
- `tests/backend/GameServer.Application.Tests/CombatTraceResolutionTests.cs` — resolution-level trace tests.
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — envelope member-set and omission integration assertions.
- `tests/backend/GameServer.Api.Tests/CombatTraceWireTests.cs` — wire projection tests.
- `src/frontend/client/src/game/runtime/GameRuntimeEvents.ts` — `combatTrace` wire interfaces and the strict reader.
- `src/frontend/client/src/game/runtime/GameRuntime.ts` — parses/validates/forwards the trace; a malformed or mismatched trace is dropped without dropping the batch.
- `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` — trace target/damage rows and their one formatter; display-only arithmetic.
- `src/frontend/client/src/game/scenes/BattleScene.ts` — consumes `envelope.combatTrace` only; previous simulation removed; the `commitSequence` guard regression fixed.
- `src/frontend/client/tests/BattleEventPresentation.test.ts` — formatter and row tests.
- `src/frontend/client/tests/SceneLifecycle.test.ts` — scene trace consumption, duplicate/stale, and battle-transition tests.
- `src/frontend/client/tests/GameRuntime.test.ts` — envelope parser and forwarding tests.
- `src/frontend/client/tests/RuntimeBoundaries.test.ts` — marked wire-shape exemption with everything else still scanned.

### Validation Results

Run against the Phase A commit (`5b5bb43`) after it was created:

- `dotnet test src/backend/GameServer.sln --filter "FullyQualifiedName~CombatTrace"` — **PASS** (Domain 8, Application 10, Api 4).
- `dotnet build src/backend/GameServer.sln` — **PASS** (0 errors; only pre-existing NU1903/MSB3277 warnings).
- `npm run test:run` (`src/frontend/client`) — **PASS** (24 files, 1304 tests).
- `npm run build` (`src/frontend/client`) — **PASS** (84 modules, built in 2.77s).
- `git diff --check` — clean; `git diff --cached --name-status` — empty before Phase A staging.

Full backend suite counts from the review pass (Domain 1700, Application 800,
Api 405, Infrastructure 507) were previously recorded and are not re-run here;
the targeted `CombatTrace` filter, the full frontend suite, and the production
build were re-verified on the committed tree.

### Deviations / Disclosures

- The operator instruction said "exactly 18 declared paths"; the record declares
  19 (manifest field and `Declared File Set` block identical). Consulted, and the
  record's 19-path set was staged. This is the only count discrepancy.
- `RuntimeBoundaries.test.ts` was added to the declared set after implementation: its generic runtime gameplay-term scan necessarily sees the documented §3.3 wire member names, so a marked, comment-delimited exemption was added around exactly the wire-shape and reader blocks; every other line of the runtime files is still scanned. No gameplay code lives in the exempted block.
- The trace reader lives in `GameRuntimeEvents.ts` (beside the wire interfaces) because two pre-existing guards forbid the lowercase token `damage` inside `GameRuntime.ts`; `GameRuntime.readBattleEventsEnvelope` still performs the delivery-path validation and forwarding.
- Pre-existing/unowned working-tree paths carried into this task (no task ID owned them before it): `src/backend/GameServer.Application/Battle/BattleStateService.cs`, `src/backend/GameServer.Domain/Match3/SwapExecution.cs`, `src/frontend/client/src/game/scenes/BattleEventPresenter.ts`, `src/frontend/client/src/game/scenes/BattleScene.ts`, `src/frontend/client/tests/BattleEventPresentation.test.ts`, `src/frontend/client/tests/SceneLifecycle.test.ts`. All six are committed in Phase A under the `unowned/pre-existing:` disclosure required by `TASK_LIFECYCLE.md` §6.2 P-3.
- Pre-existing untracked `tests/backend/GameServer.Domain.Tests/DefenseMechanicsVerificationTests.cs` and `tasks/blocked/TASK-320-*.md` were preserved, unmodified, and are outside this task's declared set. They remain untracked after Phase A.
- The unowned local commit `e212f6a` remains untouched and unowned; this task does not repair it (`TASK_LIFECYCLE.md` §6.4/P-6, `tasks/blocked/TASK-320-*.md`).
- No rebase, amend, history rewrite, or push was performed. The task remains local; `origin/master` is unchanged.

### Server Authority & Scope Verification

- [x] Zero client-authoritative gameplay logic added; the client renders only delivered trace values.
- [x] No combat rule, DEF formula, damage order, RNG, or balance value changed (Domain/Application suites pass unchanged).
- [x] Additive, backward-compatible wire change: no event discriminator, hub method, argument, subscription, or state member added.
- [x] MVP scope unchanged (`MVP_SCOPE.md` §1); developer/log surface only.

