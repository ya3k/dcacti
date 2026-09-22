# TASK-018 — Implement Resource Generation from Cleared Gems

---

## Metadata

```text
Task ID:           TASK-018
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          MEDIUM
Primary Agent:     backend
Supporting Agents: gameplay, testing
Workflow:          development/feature.md
Skills:            dotnet-backend-patterns
Dependencies:      TASK-016 (combat stats in PlayerState — DONE)
```

---

## Objective

Implement resolution step 12 (Generate Resources) per `GAME_RULES.md §17`: convert cleared Gem types from the Match-3 cascade resolution into resource values using the documented per-Gem rates and match-tier multipliers (`COMBAT_RULES.md §2`), and make these resources available to downstream resolution steps (13–17).

**Resource pool ownership (corrected):**
- **Persistent** — `PlayerState.Power` (0–100, `COMBAT_RULES.md §1.1`, `GAME_STATE.md §2.2`)
- **Transient per-Swap** — Base Damage Pool, Defense Pool, Heal Pool (consumed by downstream steps within the same resolution; never stored in `PlayerState`, `BattleState`, Redis, or SignalR wire)

---

## Context

Steps 1–10 of the battle resolution pipeline are implemented: Match-3 board resolution (swap validation, match detection, cascade, passive tracking) and combat stats in PlayerState. Step 12 (Generate Resources) converts the Gem types cleared during steps 1–9 into resource values that feed downstream consumers. The per-Gem rates and match-tier multipliers are documented in `COMBAT_RULES.md §2` (sole source of truth for match-tier output). Cleared gems are transient resolution state tracked during the match-cascade pipeline (steps 1–9) and are available at the point where resource generation executes. This step does not require Relics (Step 11) to be implemented first — Relic effects modify stats in the damage pipeline, not resource generation rates.

**Ownership:**
- `PlayerState.Power` is the only persistent resource field (`GAME_STATE.md §2.2`, `COMBAT_RULES.md §1.1`). POWER gems add flat Power per `COMBAT_RULES.md §2`.
- Base Damage Pool, Defense Pool, and Heal Pool are **transient per-Swap resolution values** — they exist only within the `ResolutionContext` (`GAME_STATE.md §3`) and are consumed by downstream steps (13–17) within the same resolution. They must NOT be added to `PlayerState`, `BattleState`, Redis state, or the SignalR `BattleStateUpdated` wire payload.

---

## Authoritative Sources

- `docs/00-overview/MVP_SCOPE.md` §1 — confirm resource generation is IN scope
- `docs/01-game-design/GAME_RULES.md` §17 — resolution order (step 12 = Generate Resources)
- `docs/01-game-design/COMBAT_RULES.md` §2 — per-Gem rates and match-tier multipliers
- `docs/02-technical/GAME_STATE.md` §2.0/§2.0.5 — BattleState shape, PlayerState fields (Power, etc.)
- `docs/02-technical/GAME_EVENTS.md` — resource generation event contract (if any)
- `docs/02-technical/ARCHITECTURE.md` — resolution pipeline structure
- `docs/01-game-design/MATCH3_RULES.md` §3–§5 — match detection and cascade resolution (cleared gem source)

---

## Scope

### In Scope

- Tracking cleared Gem types during Match-3 cascade resolution (steps 1–9)
- Accumulating cleared gems by type (ATK, DEF, HP, POWER) per cascade
- Applying per-Gem conversion rates from `COMBAT_RULES.md §2`
- Applying match-tier multipliers from `COMBAT_RULES.md §2`:
  ```text
  Match 3   → 1.0×
  Match 4   → 1.5×
  Match 5   → 2.0×
  L/T       → 1.25×
  Match 6+  → uses Match-5 rate (2.0×) — no separate tier
  ```
  Special Gem activation clears generate at base rate (1.0×) per `COMBAT_RULES.md §2` items 1–4.
- Generating `PlayerState.Power` (persistent, clamped to 0–100 per `GAME_RULES.md §12`)
- Generating transient per-Swap resource values: Base Damage Pool, Defense Pool, Heal Pool (consumed by downstream steps within the same resolution)
- Making generated resources available to downstream resolution steps (13–17)
- Unit tests for resource generation logic

### Out of Scope

- Relic effects on resource generation (Relics modify damage pipeline stats, not resource rates)
- Consuming the generated resources in downstream steps (Steps 13–17 are separate tasks)
- Boss resource generation (BossState is deferred per TASK-015)
- Redis persistence of transient resolution state
- Adding BaseDamagePool / DefensePool / HealPool to `PlayerState`, `BattleState`, Redis, or SignalR wire (they are transient per-Swap values)
- Any system listed in `docs/00-overview/MVP_SCOPE.md` §2 (OUT)

---

## Current State

- Steps 1–10 of the resolution pipeline are implemented (Match-3 + Passive tracker)
- PlayerState has combat stats (HP, ATK, DEF, Power, CritChance) per TASK-016
- Cleared gems are tracked transiently during match-cascade resolution but not yet accumulated by type for resource conversion
- No resource generation logic exists yet
- `COMBAT_RULES.md §2` documents the conversion rates and multipliers (sole source of truth for match-tier output)
- `GAME_STATE.md §3` defines `ResolutionContext` as the transient resolution state container — resource pools (BaseDamagePool, DefensePool, HealPool) should be added here, not to `PlayerState` or `BattleState`

---

## Acceptance Criteria

- [ ] Cleared gems are accumulated by type during cascade resolution
- [x] Per-Gem conversion rates match `COMBAT_RULES.md §2` exactly (ATK → +10 Base Damage, DEF → +5 Defense Pool, HP → +20 Heal Pool, POWER → +10 Power)
- [x] Match-tier multipliers match `COMBAT_RULES.md §2` exactly: Match 3 = 1.0×, Match 4 = 1.5×, Match 5 = 2.0×, L/T = 1.25×, Match 6+ = Match-5 rate (2.0×)
- [x] Special Gem activation clears generate at base rate (1.0×) per `COMBAT_RULES.md §2` items 1–4
- [x] `PlayerState.Power` is updated persistently (clamped to 0–100 per `GAME_RULES.md §12`)
- [x] Base Damage Pool, Defense Pool, and Heal Pool are transient per-Swap values — NOT stored in `PlayerState`, `BattleState`, Redis, or SignalR wire
- [x] Generated resources are available to downstream resolution steps (13–17)
- [x] Multiple cascades within a single turn correctly accumulate resources
- [x] Empty cascade (no matches) produces zero resources
- [x] All relevant tests pass at the depth required by `core/validation.md §2` for MEDIUM risk
- [x] `quality/review.md §1` checklist passes
- [x] Documentation impact addressed (§ Documentation Impact below)

---

## Affected Areas

```text
[x] Domain (GameServer.Domain/) — resource generation logic in Match-3 resolution pipeline
[ ] Application (GameServer.Application/)
[ ] API (GameServer.Api/)
[ ] Client (client/)
[ ] SignalR / Redis (GameServer.Infrastructure/SignalR/, /Redis/)
[ ] PostgreSQL (GameServer.Infrastructure/Postgres/)
[x] Tests (tests/)
[ ] Documentation (docs/)
[ ] ADR (docs/03-decisions/ADR/)
```

---

## Implementation Notes

- The cleared gems from match-cascade resolution (steps 1–9) are transient state. Accumulate them by type within the existing resolution pipeline context.
- Resource generation (step 12) executes after passive tracking (step 10) and before power update (step 13) per the documented resolution order.
- **Persistent:** `PlayerState.Power` is updated by POWER gem generation (flat +10 per gem, per `COMBAT_RULES.md §2`). Clamped to 0–100 per `GAME_RULES.md §12`.
- **Transient per-Swap:** Base Damage Pool (ATK gems × tier multiplier), Defense Pool (DEF gems × tier multiplier), Heal Pool (HP gems × tier multiplier). These exist only in `ResolutionContext` (`GAME_STATE.md §3`) and are consumed by downstream steps within the same resolution. They must NOT be added to `PlayerState`, `BattleState`, Redis, or SignalR wire.
- Match-tier multipliers (`COMBAT_RULES.md §2` sole source): Match 3 = 1.0×, Match 4 = 1.5×, Match 5 = 2.0×, L/T = 1.25×, Match 6+ = 2.0× (Match-5 rate).
- Special Gem activation clears generate at base rate (1.0×) per `COMBAT_RULES.md §2` items 1–4 — they are not a Match and have no match tier.
- See `COMBAT_RULES.md §2` for exact per-Gem rates: ATK → +10 Base Damage, DEF → +5 Defense Pool, HP → +20 Heal Pool, POWER → +10 Power.
- The existing PassiveTracker (step 10) is already integrated — resource generation runs after it.
- `GAME_EVENTS.md §2` documents `PowerChanged` (Delta, new Power value, source) — emit when Power changes from gem generation.

---

## Testing Requirements

### Test Types Required

```text
[x] Unit tests         — resource generation logic: per-Gem rates, tier multipliers, accumulation
[ ] Integration tests  — resource generation within full cascade resolution pipeline
[x] Gameplay scenarios — Given cleared gems of various types, when resource generation runs, then correct resource pools are populated
[ ] API tests          — N/A (no API changes)
[ ] Realtime tests     — N/A (no realtime changes)
[ ] Persistence tests  — N/A (no persistence changes)
```

### Key Edge Cases

- See `COMBAT_RULES.md §2` — per-Gem rates and multipliers (Match 3 = 1.0×, Match 4 = 1.5×, Match 5 = 2.0×, L/T = 1.25×, Match 6+ = 2.0×)
- See `MATCH3_RULES.md §5.3` — Match-6+ uses Match-5 rate (2.0×), not a separate multiplier
- Special Gem activation clears generate at base rate (1.0×) — see `COMBAT_RULES.md §2` items 1–4
- `PlayerState.Power` must be clamped to 0–100 (`GAME_RULES.md §12`)
- BaseDamagePool / DefensePool / HealPool are transient — must NOT appear in `PlayerState`, `BattleState`, Redis, or SignalR
- Empty cascade produces zero resources
- Multiple cascades in a single turn accumulate resources across cascades
- Mixed gem types in a single match correctly split by type

---

## Documentation Impact

**Option A — None:**
> This task implements already-documented behavior per `COMBAT_RULES.md §2` and `GAME_RULES.md §17`. No doc changes required.

---

## Stop Conditions

- If the required behavior cannot be fully derived from the Authoritative Sources listed above: STOP per `AGENTS.md §7`
- If cleared gem tracking during cascade resolution is not possible with the current architecture: STOP and report

---

## Dependencies

- TASK-016 (Add combat stats to PlayerState) — DONE
- TASK-017 (Update game state docs for combat stats) — DONE

---

## Completion Evidence

### Summary

Implemented resolution step 12 (Generate Resources) per `GAME_RULES.md` §17:
the cleared Gems the Match-3 cascade resolution already records are converted
into the documented resource pools at the rates and match-tier multipliers of
`COMBAT_RULES.md` §2.

**Two outputs, two ownership classes.**

- **Persistent — `PlayerState.Power`.** POWER Gems generate flat Power, written
  into the existing `PlayerState.Power` field and clamped to the documented
  0–100 range (`GAME_RULES.md` §12, `COMBAT_RULES.md` §1.1). The clamp lives at
  the single point the persistent value is written, so the transient pool keeps
  reporting what the Swap generated while the state holds what is in force.
- **Transient per-Swap — Base Damage Pool, Defense Pool, Heal Pool.** These are
  Transient Resolution State (`GAME_STATE.md` §3). They are carried on
  `SwapExecutionResult` for the downstream steps (13–17) of the same resolution
  and were **not** added to `PlayerState`, `BattleState`, Redis, or the SignalR
  wire. `BattleState`'s field list is unchanged.

**Per-Gem rates and tiers.** The four base outputs are `COMBAT_RULES.md` §2's
table (ATK +10, DEF +5, HP +20, POWER +10). The multipliers are applied as exact
rationals over a denominator of 4 (`1.0× / 1.5× / 2.0× / 1.25×`), read from one
place, and applied once per consumed cell at that cell's tier. A run of 6+ uses
the Match-5 rate and no sixth tier exists.

**Which cells generate at which tier.** A cell consumed by a Match generates at
that shape's tier; a cell cleared only by a Special Gem activation generates at
the base rate, because an activation is not a Match and has no tier
(`COMBAT_RULES.md` §2 item 1, `MATCH3_RULES.md` §5.7 item 6). Accounting is over
the union of each pass's cleared cells, each cell once, so overlapping effects
produce no double resources (`MATCH3_RULES.md` §5.8.2 items 2, 6).

**No stop condition fired.** The conflict the previous attempt on this task
reported is resolved: the task now states `COMBAT_RULES.md` §2's multipliers
verbatim, and the transient-pool ownership question is answered by the task
itself. Cleared-gem data was already available at the required stage —
`PassResult` carried the cleared union; this task added each cleared cell's Gem
type and tier alongside it, reusing the existing pipeline rather than adding a
second one.

### Changes

Created:
- `src/backend/GameServer.Domain/Match3/MatchTier.cs` — `MatchTier`
  (`Base`/`Match3`/`Match4`/`Match5`/`Lt`) and `MatchTierMultipliers` (the
  documented numerators over a denominator of 4, plus `ForStraightRun` mapping a
  run's length to its tier). The `Base` member names "no match tier" for
  activation clears instead of leaving a bare multiplier at the call site.
- `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs` —
  `ResourceGeneration` (the four pools plus the cleared-cell counts),
  `ClearedGemCounts`, and `ResourceGenerator` (the per-Gem rates as documented
  constants, `Generate`, and `ApplyPower`).
- `tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs` — 33 tests.

Modified:
- `src/backend/GameServer.Domain/Match3/BoardResolver.cs` — added `ClearedGem`
  (cell, Gem type, tier) and `PassResult.ClearedGems`, the per-pass record of
  what it cleared. Each cell's type is read from the board the pass was resolved
  against (the state in which it was still present); its tier comes from the
  shape that consumed it, or `Base` when only an activation cleared it. A cell
  named by both sub-steps takes the Match's tier and is recorded once.
- `src/backend/GameServer.Domain/Match3/SwapExecution.cs` — `SwapExecutor`
  runs `ResourceGenerator.Generate` and `ApplyPower` in the documented step-12
  position and carries the result on `SwapExecutionResult.Resources`; the
  `WithEvents`/`WithState` factories carry the resources across unchanged; the
  stale "Resource Generation (TASK-017) is the task that will change Power"
  comment was corrected to name this task and the real stage.

Unchanged (verified): `PlayerState`, `BattleState`, `BattleEvent`,
`BattleEventType`, `BattleEventWireProjection`, `SignalR`/hub code, and every
Redis or PostgreSQL path.

### Tests

Added `ResourceGenerationTests` — 33 tests, all passing, covering the nine
required areas:

| Area | Tests |
| --- | --- |
| 1. ATK gem generation | `Generate_ShouldProduceBaseDamageFromAtkGemsAtMatchTier` |
| 2. DEF gem generation | `Generate_ShouldProduceDefenseFromDefGemsAtMatchTier` |
| 3. HP gem generation | `Generate_ShouldProduceHealFromHpGemsAtMatchTier` |
| 4. POWER gem generation | `Generate_ShouldProducePowerFromPowerGemsAtMatchTier` |
| 5. Tier multipliers | `MatchTierMultipliers_ShouldCarryTheDocumentedNumerators`, `…ShouldPlaceMatch6AndAboveAtTheMatch5Tier`, `…ShouldRejectARunShorterThanAMatch`, `Generate_ShouldApplyTheMatch4MultiplierToAMatch4`, `…Match5MultiplierToAMatch5`, `…UseTheMatch5RateForARunOfSixOrMore`, `…ApplyTheLtMultiplierOncePerConsumedCell` |
| 6. Multiple matches / cascades per Swap | `Generate_ShouldSumBothMatchesOfAPass`, `…ShouldAccumulateAcrossCascadesInOneSwap`, `…ShouldAddRatherThanReplaceAcrossPasses`, `…ShouldSplitAMatchByGemType` |
| 7. Zero cleared gems | `Generate_ShouldProduceNothingForAResolutionWithNoPasses`, `…ProduceNothingWhenNothingWasCleared`, `RejectedSwap_ShouldGenerateNoResources` |
| 8. Accumulation across the flow | `Power_ShouldAccumulateAcrossCommittedSwaps`, `…ShouldBeClampedToTheDocumentedRange`, `…ShouldFillUpToTheCapWithoutExceedingIt`, `ApplyPower_ShouldClampAtBothEndsOfTheRange`, `…ShouldChangeOnlyPower`, `TransientPools_ShouldNotBeStoredInPlayerState`, `DefAndHpPools_ShouldNotBeStoredInPersistentStats`, `ResourceGeneration_ShouldBeDeterministicForTheSameResolution`, `Generate_ShouldNotConsumeRng`, `ClearedGems_ShouldCarryTheTypeTheCellHeldBeforeRemoval` |
| 9. Existing Match-3 unchanged | `ExistingMatchAndComboAccounting_ShouldBeUnchanged`, `ExistingSwapCounters_ShouldBeUnchanged`, `ExistingBoardResolution_ShouldBeUnchanged`, `ExistingEventStream_ShouldBeUnchanged`, `ExistingRoundTripFactories_ShouldCarryResourcesAcross` |

Expected values are read from `COMBAT_RULES.md` §2's tables, not from the
implementation (`quality/testing.md` §2). Two fixture-driven corrections were
found and fixed during the run — a background Gem merging with a declared run,
and a pre-existing vertical run in a hand-written row set — both of which made a
Match 3 into a Match 4. The implementation was correct in both cases; the
fixtures were rebuilt to declare every cell that could join the run under test.

Results:

```text
Focused   ResourceGenerationTests                 33 passed,  0 failed
Full      GameServer.Domain.Tests                678 passed,  0 failed
          GameServer.Application.Tests            53 passed,  0 failed
          GameServer.Infrastructure.Tests          1 passed,  0 failed
          GameServer.Api.Tests                    51 passed,  0 failed
          ─────────────────────────────────────────────────────────
          Total                                  783 passed,  0 failed
Build     dotnet build src/backend/GameServer.sln  succeeded, 0 warnings, 0 errors
```

No frontend tests were run: this repository contains no client project
(`E:\dcacti` holds `.ai`, `docs`, `src`, `tasks`, `tests` only), so the task's
`[ ] Client (client/)` affected area does not apply.

### Documentation Consulted

- `docs/01-game-design/COMBAT_RULES.md` §2 — per-Gem base outputs and the
  match-tier multiplier table, including §2 items 1–4 (activation clears at the
  base rate, the creating Match's tier applied once, union scope, Match 6+ has
  no multiplier of its own)
- `docs/01-game-design/COMBAT_RULES.md` §1.1, §3, §4 — the Power range and the
  stat/Heal distinctions the pools are kept separate from
- `docs/01-game-design/GAME_RULES.md` §12 (Power range 0–100), §16 (event list),
  §17 (the fixed resolution order; step 12 = Generate Resources), §21 (source of
  truth hierarchy)
- `docs/01-game-design/MATCH3_RULES.md` §1.1, §3.2, §3.3, §4.1–§4.5, §5.1–§5.4,
  §5.5.2–§5.5.5, §5.7 (resource generation hooks — *when*, not *how much*),
  §5.8.2 (union semantics), §7.2 (what consumes RNG)
- `docs/02-technical/GAME_STATE.md` §0, §2, §2.0.5, §2.1, §2.2 (PlayerState field
  list and Power's initial value), §3 (Transient Resolution State /
  `ResolutionContext`), §5.1 (the single post-resolution write-back)
- `docs/02-technical/GAME_EVENTS.md` §1, §1.1 (where `PowerChanged` sits in the
  cycle), §2 (`PowerChanged`'s trigger and payload), §3 items 6–7
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.1–§3.2.2 (the closed `events[]`
  discriminator set), §4.2 item 2 (which `PlayerState` members reach the wire)
- `docs/02-technical/ARCHITECTURE.md` §2.1, §4.1, §5
- `docs/00-overview/MVP_SCOPE.md` — resource generation is IN scope
- `AGENTS.md` §2, §4, §6, §7, §9, §10, §11, §12, §13, §14, §15, §16, §17, §22
- `.ai/README.md`, `.ai/agents/backend.md`, `.ai/workflow/development/feature.md`,
  `.ai/workflow/core/validation.md`, `.ai/workflow/quality/testing.md`,
  `.ai/workflow/quality/review.md`
- `tasks/completed/TASK-015-resolve-combat-state-contract.md` §2 — the prior
  adjudication that `COMBAT_RULES.md` §2's table is the confirmed source for the
  match-tier multipliers

### Documentation Changed

None. `Option A` of the task's Documentation Impact applies: this task
implements already-documented behaviour, and no value, rule, or contract was
invented. `docs/02-technical/GAME_STATE.md` was already modified in the working
tree by the preceding TASK-017 (combat stats) and was **not** touched by this
task.

### Validation

Depth per `core/validation.md` §2 for **MEDIUM** risk: build/compile validation
+ focused unit tests + gameplay scenarios + documentation validation + review.

- **Correctness** — every rate and multiplier is read from `COMBAT_RULES.md` §2;
  no value was chosen or adjusted. The tier of each shape follows
  `MATCH3_RULES.md` §5.5.2, with an L/T counted once at the L/T tier (§5.4
  item 6) and 6+ at the Match-5 rate (§5.3 item 3).
- **Architecture** — logic sits in `GameServer.Domain/Match3/`, reached by the
  existing `SwapExecutor` sequence. No new interface, factory, manager, or
  abstraction was introduced; the two new types are values plus one static
  function (`AGENTS.md` §9, `ARCHITECTURE.md` §5). No Domain → Infrastructure/Api
  reference was added; `ArchitectureTests` passes.
- **Scope** — only the three in-scope files changed. No Relic, damage, crit,
  element, boss, player-effect, card, pet-skill, victory/defeat, or end-turn
  behaviour was touched, and no new Match-3 mechanic was added.
- **Determinism / authority** — `Generate` draws no RNG, reads no clock, and
  depends on no enumeration order; asserted by
  `Generate_ShouldNotConsumeRng` and
  `ResourceGeneration_ShouldBeDeterministicForTheSameResolution`. Every value is
  computed server-side; nothing is exposed for client authority
  (`AGENTS.md` §10, §11).
- **Wire/state boundary** — `BattleEventType` and
  `BattleEventWireProjection` are unchanged, so `PowerChanged` was **not**
  emitted. `SIGNALR_PROTOCOL.md` §3.2.2 item 2 closes the `events[]`
  discriminator set, and §4.2 item 2 delivers only `combo`/`matchCount`, so
  emitting it or adding a `playerState` member would be a wire-contract change —
  explicitly out of scope. Power reaches downstream steps as state
  (`PlayerState.Power`), which is where the docs put it.
- **Tests** — 33 new tests at state-transition depth (input → resolved Swap →
  state + pools), plus the full suite re-run to prove existing Match-3 behaviour
  is unchanged.
- **Review** — `quality/review.md` §1 checklist walked: Correctness ✅,
  Architecture ✅, Scope ✅, Tests ✅, Documentation ✅ (no change required),
  Security ✅ (no auth or data-exposure surface touched), Performance ✅ (pure
  in-memory conversion over the already-computed cleared cells; no query on the
  resolution path per `TDD.md` §4), Maintainability ✅ (two small types, no new
  abstraction), Determinism ✅.

### Risks

- **The accumulated pools are computed but not yet consumed.** Steps 13–17 are
  separate tasks, so `BaseDamagePool`, `DefensePool`, and `HealPool` currently
  reach no consumer. They are carried on `SwapExecutionResult.Resources` so the
  owning step can read them without re-deriving them; if a later task decides
  those steps should consume them differently, that is that task's decision, not
  a change here.
- **The `ResetBehavior`-style staging position of Power delivery is unchanged.**
  `Power` is now written server-side but still not delivered to the client
  (`SIGNALR_PROTOCOL.md` §4.2 item 2). A client cannot display Power yet; that is
  a protocol change owned by its own task, and this task deliberately did not
  make it.
- **Integer application of the L/T rate.** `COMBAT_RULES.md` §2 states the
  multiplier applies per Gem consumed, so each individual Gem's output is
  computed as `(base × numerator) / 4` and the per-Gem integer results are
  summed. For `1.25×` on a `+10` base this yields 12 per ATK Gem (from 12.5)
  rather than 12.5, so 6 Gems give 72, not 75. The alternative reading — summing
  bases first, then applying the multiplier once — would give 75. The per-Gem
  reading is the one §2 states verbatim ("per Gem consumed in that match"), and
  it keeps every pool an integer as `PlayerState` requires; the choice is pinned
  by `Generate_ShouldApplyTheLtMultiplierOncePerConsumedCell` so a future
  balance pass sees it explicitly. If the design intends the shape-total
  reading, that is a one-line change at a single call site.

### Remaining Issues

Found but deliberately not fixed (`AGENTS.md` §16):

1. **`COMBAT_RULES.md` §2's stale provenance parenthetical.** The POWER row reads
   `+10 Power (flat, per GDD example)`, but `GDD.md` §9 contains no numeric
   example and the same document states "GDD.md intentionally contains no exact
   numbers". The `+10` value is correct and owned by `COMBAT_RULES.md` §2; only
   the attribution is unsupported. Already flagged by
   `TASK-015-resolve-combat-state-contract.md` §4 for its own documentation
   correction task. Impact: cosmetic. Suggested follow-up: a documentation task
   removing the parenthetical.
2. **`GAME_EVENTS.md` §1.1 `[PowerChanged] per resource generation` is not
   deliverable under the current protocol.** The cycle places `PowerChanged` in
   the per-pass loop, but `SIGNALR_PROTOCOL.md` §3.2.2 item 2 closes the wire
   `events[]` set and does not include it. Emitting it would require a protocol
   change. Impact: Power changes are not observable by the client. Suggested
   follow-up: a Realtime task deciding whether `PowerChanged` is delivered,
   delivered as state, or removed from the §1.1 diagram.
3. **`ResolutionContext` is documented but has no code counterpart.**
   `GAME_STATE.md` §3 describes it as "built and discarded entirely within
   `BattleResolutionService` (`ARCHITECTURE.md` §4)", but no such type or service
   exists; the transient bookkeeping lives on `PassResult` and
   `SwapExecutionResult`. This task followed the existing code rather than
   introducing the documented-but-absent container. Impact: a doc/code shape
   mismatch that predates this task. Suggested follow-up: a documentation-
   consistency task reconciling §3's `ResolutionContext` sketch with the staged
   implementation.
4. **`ARCHITECTURE.md` §4 names `BattleResolutionService`, which does not
   exist.** The `GAME_RULES.md` §17 sequence is currently staged across
   `SwapExecutor` (Domain, steps 1–9, 12) and `BattleStateService` (Application,
   step 10). Impact: a stale component name in the architecture doc. Suggested
   follow-up: fold into follow-up 3, since both describe the same staging
   position.

### Agent

backend (primary), with gameplay and testing responsibilities exercised in the
same execution per the task's `Supporting Agents`.

### Workflow Used

`development/feature.md` — MVP scope check, context discovery, conflict
detection, implementation, `quality/testing.md`, `quality/review.md`,
documentation-impact check, `core/completion.md`.

### Skills Used

None were invoked by name: `.ai/skills/` contains no `dotnet-backend-patterns`
skill as the task's metadata names, and the skills that do exist
(`quality/implementation-review`, `quality/documentation-consistency`,
`gameplay/gameplay-behavior-derivation`) are prose procedures already reflected
in the workflow steps followed. No skill file was created or modified.

### Status

DONE

---

## Handoff

<!-- TO BE FILLED BY THE AGENT if the task is handed off mid-execution -->
