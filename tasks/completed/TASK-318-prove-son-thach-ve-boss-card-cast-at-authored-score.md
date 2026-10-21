# TASK-318 — Prove Sơn Thạch Vệ Actually Casts Its Boss Card at the Authored Score

---

## Metadata

```text
Task ID:           TASK-318
Type:              BUG
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     testing
Supporting Agents: gameplay, backend, review
Workflow:          development/bug-fix.md
Skills:            testing/test-scenario-generation, discovery/impact-analysis, quality/scope-validation
Dependencies:      TASK-314
Declared Files:    tests/backend/GameServer.Application.Tests/Balance/Tests/ModernBossCardCadenceMeasurementTests.cs
```

---

## Objective

Strengthen the modern Boss Card cadence suite so that it *proves* a Boss Card
cast actually happens under a controlled, deterministic fixture instead of
asserting that a response phase merely occurred. For Sơn Thạch Vệ — the one MVP
Boss whose authored cast-priority score is not above the documented minimum
board score — the suite must resolve one committed round through the real
production pipeline at the **authored** definition and the **provisioned**
Boss Card, and verify the full cast contract: the canonical `BossCardCast`
event exists exactly once, carries the expected Boss Card identity and Boss
identity, is serialized under the canonical wire contract, is counted exactly
once by M-13 `BossCardCastCount`, leaves M-08 `CastsTotal` (player-card-only) at
zero, sets the authored post-cast cooldown, and keeps the one-action boss
response invariant intact (no Boss board action, no Boss attack in that phase).

No balance value, cooldown, cost, score, historical baseline, or production
runtime code may be changed. If the production selection rules genuinely prevent
a cast under the chosen fixture, the test must diagnose that reason rather than
weaken the assertion.

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Boss Card capability is IN; the fifth Gem is not
- `docs/01-game-design/BOSS_RULES.md` §4.2 — Canonical Boss Cards (one per Boss, Boss-only)
- `docs/01-game-design/BOSS_RULES.md` §4.3 — Response-phase one-action policy, eligibility gates, cast-priority score, tie-break, cooldown handling, consequences of a cast phase
- `docs/01-game-design/BOSS_RULES.md` §6.3, §6.3.1 item 4 — Earthquake's approved `PowerCost = 30`, `Cooldown = 0`, `Shield 150 Flat`
- `docs/02-technical/GAME_EVENTS.md` §2 — Canonical `BossCardCast` event
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.30 — `BossCardCast` wire schema (`cardId`, `sourceId`, nothing else)
- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` — Boss Power, Boss Cards, one-action response policy

---

## Scope

### In Scope
- Replacing the response-phase-only assertion in
  `ModernBossCardCadenceMeasurementTests` with a controlled, deterministic
  committed-round fixture that proves an actual cast.
- Covering Sơn Thạch Vệ at the real authored `CastPriorityScore = 16` and the
  real `BossDefinitions.SonThachVe` definition (no score override, no MaxHP/ATK
  override, no Passive override).
- Verifying the M-13/M-08 telemetry split, the canonical cast identity, the
  canonical wire projection, the authored post-cast cooldown, and the
  one-action invariant on the observed round.
- Adding the Api-side canonical `BossCardCast` wire-schema assertion (§3.2.30)
  to the existing Boss wire-projection suite if it is not already asserted
  there.

### Out of Scope
- Any change to `BossActionPolicy`, `BattleStateService`, `CardCastExecutor`,
  or any other production source file.
- Any change to Boss stats, Boss Card costs, cooldowns, cast-priority scores,
  scoring weights, or the `16`/`85` authored values.
- Any change to `BalanceBaselineMatrix`, historical seeds, historical claims, or
  protected artifact files.
- The Fifth Gem / Power-drain Gem (`MVP_SCOPE.md` §3).
- Weakening any existing TASK-314 assertion to make the new scenario pass.

---

## Current State

`ModernBossCardCadenceMeasurementTests.TASK314_InBattleExecution_MeasuresActualCadenceAcrossAllFiveBosses`
plays up to 30 arbitrary swaps per Boss and increments an internal counter when a
`BossCardCast` happens to appear, but its only failure condition is
`totalResponsePhases > 0`. A response phase can be a plain Boss board action, so
the suite passes even if no Boss Card is ever cast. Under the real selection rule
`CastPriorityScore > BoardScore` (`BOSS_RULES.md` §4.3 item 5), Sơn Thạch Vệ's
authored `16` can only be selected when the deterministic board action's score is
exactly the documented minimum `15`.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-318:
```text
tests/backend/GameServer.Application.Tests/Balance/Tests/ModernBossCardCadenceMeasurementTests.cs
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above must strictly match the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [ ] 1. The suite contains a deterministic, controlled fixture under which Sơn Thạch Vệ commits a round that actually casts its provisioned Boss Card at the authored `CastPriorityScore = 16`.
- [ ] 2. The observed round emits exactly one `BossCardCast`, and the test asserts it is *present* (not merely that a response phase occurred).
- [ ] 3. The emitted event carries `cardId = "earthquake"` (the provisioned Boss Card / ability identity) and `sourceId = "boss-son-thach-ve"` (the canonical Boss identity).
- [ ] 4. The same observed event's canonical `§3.2.30` shape is verified against the production projection (`BattleEventWireProjection`): `type = "BossCardCast"`, `cardId`, `sourceId`, and no other member. (The projection assertions live in `GameServer.Api.Tests/BossWireProjectionTests.cs`, which already covers §3.2.30; this task cites that coverage rather than duplicating it, so the record must name the citing test.)
- [ ] 5. Feeding that observed round's event list to `MetricAccumulator` yields `BossCardCastCount == 1` for exactly one canonical cast, and `CastsTotal == 0` (M-08 remains player-card-only).
- [ ] 6. The committed post-cast `BossState.SkillCooldown` equals the authored `BossDefinitions.SonThachVe.SkillDefinition.CooldownTurns`, and `BossState.Power` decreased by exactly the provisioned `Earthquake` `PowerCost`.
- [ ] 7. The one-action invariant holds on the observed round: no Boss `SwapStarted`, no Boss board cycle, and no Boss-sourced damage event in that response phase.
- [ ] 8. The fixture remains deterministic (re-running it reproduces the same cast round) and the authorization role of the cast-priority score is asserted explicitly, so the fixture cannot silently stop exercising the cast branch.
- [ ] 9. No production source file, balance value, historical seed set, or protected artifact is modified.
- [ ] 10. All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] 11. Quality review checklist passes (`quality/review.md` §1)
- [ ] 12. No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios / balance telemetry)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- The observable production seam is `BattleStateService.ExecuteSwapAsync` over
  the suite's in-memory `IBattleStateRepository` double, with the provisioned
  Boss Card content behind `ICardDefinitionLookup`
  (`TestCardDefinitionLookup.WithProvisionedBossCards`), as in
  `BossCardCastRuntimeTests` and `TASK309BossCardEffectRuntimeTests`.
- The pre-existing integrated scenario
  `BossCardCastRuntimeTests.Round_WithTheRealAuthoredScore_ShouldSelectTheCastOnACrafted15PointBoard`
  establishes that the authored score can win on a 15-point board; this task
  moves that evidence into the cadence suite and adds the cadence/telemetry,
  wire-contract, and one-action assertions the audit found missing.
- `BattleEventWireProjection` and `BattleEventWireDto` live in
  `GameServer.Api.Hubs`; `GameServer.Api.Tests` references the Api project.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — MetricAccumulator M-13/M-08 split from the observed round's events
[x] Integration tests  — BattleStateService committed round at the authored score and provisioned Boss Card
[x] Gameplay scenarios — BOSS_RULES.md §4.3: one action per response phase; §6.3.1 item 4 cost/cooldown
```

### Key Edge Cases
- `BOSS_RULES.md` §4.3 item 5 — a cast must strictly out-score the board action; a tie goes to the board, so the fixture must assert the authored score is what authorizes the cast.
- `BOSS_RULES.md` §4.3 item 4 — a cast phase produces no Boss board resolution and no Boss attack.
- `SIGNALR_PROTOCOL.md` §3.2.30 — the cast carries no cost, Power, cooldown, or effect summary.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If the production selection rules legitimately prevent a cast for Sơn Thạch Vệ: STOP and diagnose the reason; do not weaken the assertion or change a balance value (`AGENTS.md` §16)
- If the scenario requires changing production gameplay logic: STOP per `AGENTS.md` §16

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Commit
- Implementation slice (Phase A): `c4a582361b2cf89295d3a00f8c2e12ff5c41f55b`
- Subject: `test: prove the modern boss card cast under the authored selection rule`
  — **known defect, not amended:** the stored commit object begins the message with
  a UTF-8 byte-order mark (`EF BB BF`) before `test:`, because the message was
  written through a PowerShell file redirect. The *text* of the subject is correct;
  the leading three bytes are not part of it. `TASK_LIFECYCLE.md` §6.2 P-6 and §6.4
  item 6 forbid `git commit --amend`/`git reset` even for a defect of this class, so
  the commit was left exactly as created and the defect is recorded here instead of
  being repaired by rewriting history. The task ID is absent from the subject as
  P-4 requires, the body records `owns: TASK-318`, and the committed file set is
  exactly the declared set.
- Owns: `TASK-318`
- Files: `tests/backend/GameServer.Application.Tests/Balance/Tests/ModernBossCardCadenceMeasurementTests.cs`
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files
- `tests/backend/GameServer.Application.Tests/Balance/Tests/ModernBossCardCadenceMeasurementTests.cs` — replaced the response-phase-only in-battle scenario with a controlled committed-round fixture that proves an actual Sơn Thạch Vệ Boss Card cast at the authored cast-priority score `16` and the provisioned `earthquake` content, asserting canonical identity, the M-13/M-08 telemetry split, the authored Power cost, the authored cooldown, and the one-action invariant; added a determinism replay test; documented the selection-rule diagnosis in the fixture's remarks.

### Validation Results
- `dotnet build src/backend/GameServer.sln` — **PASS** (0 errors)
- `dotnet test … --filter "FullyQualifiedName~ModernBossCardCadenceMeasurementTests"` — **PASS** (9/9 executed, both TASK-318 cases included)
- `dotnet test src/backend/GameServer.sln` — **PASS** (Domain 1674, Infrastructure 507, Api 399, Application 785; 0 failed, 0 skipped)
- `npm run test:run` (`src/frontend/client`) — **PASS** (24 files, 1267 passed; unchanged by this task)
- `git diff --check` — **PASS** (0 errors)

### Diagnosis Recorded
`BOSS_RULES.md` §4.3 item 5 requires `CastPriorityScore > BoardScore`. A first-pass
Match always clears ≥ 3 same-type cells and the lightest Gem weight is DEF's `5`, so
a board action never scores below `15`; the authored `16` can therefore win only
against a board score of exactly `15`. Measured over 500 generated boards and
~13.7k evaluated candidates, the minimum board score observed was `25` (and `30` for
the selected moves), so the cast is a near-exceptional case on generated boards.
The fixture supplies a crafted board whose enumerable candidates score exactly `15`,
and the selection is still made by the production `BossActionPolicy` from the
production `BossAiEngine` enumeration. No balance value was changed to make the test
pass.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed P-1…P-6 commit policy compliance (subject BOM defect disclosed above; deliberately not repaired by amendment)
