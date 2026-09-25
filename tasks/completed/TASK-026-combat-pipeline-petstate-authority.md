# TASK-026 — Combat Pipeline PetState Authority

---

## Metadata

```text
Task ID:           TASK-026
Type:              REFACTOR
Status:            DONE
Risk:              HIGH
Priority:          CRITICAL
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/refactor.md
Skills:            gameplay/authority-determinism-audit, gameplay/gameplay-behavior-derivation, quality/architecture-conformance, testing/test-scenario-generation, quality/implementation-review
Dependencies:      TASK-025
```

---

## Objective

Audit and correct the combat pipeline so every Player-side combat read/write (damage, heal, shield, Power, Crit, passive charge, skill resolution, Boss response, victory/defeat, turn resolution) sources `PetState` exclusively, the Player is never a combat target or HP subject, and invariant tests enforce single-combat-stat-home per ADR-011 items 3 and 5.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` items 3, 5 — PetState as sole combat home; no Player combat pool
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` item 12 — no Player combat pool introduced
- `docs/01-game-design/COMBAT_RULES.md` §1.1, §3 — combat stats are the active Pet's; Damage Pipeline inputs
- `docs/01-game-design/GAME_RULES.md` §1, §14, §17 — Pet as combat character; fixed resolution order; server authority
- `docs/01-game-design/PET_RULES.md` §2 — Pet as combat identity
- `docs/02-technical/GAME_STATE.md` §2.3, §3 — PetState fields; Damage Pipeline state references

---

## Scope

### In Scope
- Semantic audit of `DamagePipeline`, `BattleStateService` resolution steps, Boss response, victory/defeat checks, and Power/Crit paths after TASK-025
- Correct any residual code that still treats a Player combat value as authoritative (including parameter names, doc comments implying a Player pool, and healing/shield targets)
- Invariant tests: no Player combat-stat reads; Player never the HP subject of DamageDealt/DamageTaken; Boss→player damage lands on PetState.HP
- Preserve numeric outcomes and event ordering exactly (behavior-preserving per `development/refactor.md`)

### Out of Scope
- Changing damage formulas, Crit rules, element multipliers, or resolution order (COMBAT/ELEMENT/MATCH3 rules unchanged)
- Wire label renames (ADR-011 item 6)
- Relic/Card loadout mechanics (TASK-027, TASK-028)
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

After TASK-025, combat fields live on `PetState`, but pipeline code and comments were written against `PlayerState` (e.g. `DamagePipeline.cs` references to `PlayerState.ATK`/`PlayerState.DEF`; `BattleStateService.cs` boss-response path writing `resolved.PlayerState` HP/DEF). These access paths and their semantic intent must be verified against PetState authority, not merely recompiled.

---

## Acceptance Criteria

- [x] Every damage/heal/shield/Power/Crit/passive/skill/Boss-response/victory/defeat/turn-resolution read of Player-side combat state sources `PetState` (or BattleState-root Combo/MatchCount for accounting only)
- [x] No production code path treats Player as an HP/ATK/DEF/Power/Crit subject or combat target
- [x] Boss→player DamageTaken/DamageDealt wire events continue to carry `target="player"` as the fixed label while the HP value comes from PetState
- [x] Invariant tests exist that fail if a Player combat-stat read is reintroduced (e.g. compile-time absence of a Player combat type plus behavioral assertions)
- [x] Numeric outcomes and event ordering of existing scenarios are unchanged from TASK-025 baseline
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation already correct — ADR-011/GAME_STATE describe PetState authority)
```

---

## Implementation Notes

- Audit checklist (file → concern): `GameServer.Domain/Combat/DamagePipeline.cs` (ATK/DEF/HP inputs and Crit comment), `GameServer.Application/Battle/BattleStateService.cs` (attack resolution L571–587, boss response L737–773, defeat check L814–816, victory L651), `GameServer.Domain/Battle/BossState.cs` (cross-references), any healing/shield write-backs.
- Doc comments that still describe a `PlayerState` combat pool are defects under ADR-011 — correct them as part of this task's code comments (docs/ prose is already correct).
- Wire projection: `finalPlayerHp` maps PetState.HP (already correct in `BattleEventWireProjection.cs` factories); verify end-to-end after pipeline fixes.
- Completed tasks affected in spirit only (do not edit `tasks/completed/`): TASK-015, TASK-016, TASK-017, TASK-019, TASK-021, TASK-022.

---

## Testing Requirements

### Required Verification
```text
[ ] Unit tests         — invariant suite: no Player combat reads; PetState is damage/heal target; Crit/Power source assertions
[ ] Integration tests  — full battle resolution regression: identical events/values vs TASK-025 baseline
[x] Gameplay scenarios — Given boss→player damage, When resolved, Then PetState.HP changes and wire target="player" label is preserved (COMBAT_RULES §1.1, GAME_STATE §3)
```

### Key Edge Cases
- See `docs/01-game-design/COMBAT_RULES.md` §1.1 — active Pet as damage subject; no Player HP pool
- See `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.15 — `target` values are fixed labels, not state locations

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If a combat rule appears to require a Player combat pool: STOP — conflicts with ADR-011 item 5; report per `AGENTS.md` §4/§18
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Changed Files

TASK-025's ownership migration was **preserved unchanged** — no field, access path,
wire member, formula, or ordering was altered. This task is comment/doc-comment
cleanup plus regression verification only (no executable line changed).

Production (`src/backend/`):

- `GameServer.Domain/Combat/DamagePipeline.cs` — `DamageInputs.DefenderHp` doc
  comment: `COMBAT_RULES.md` §3.4's Boss instance was described as writing
  `Player.HP`; corrected to the active Pet's `PetState.HP` (§2.3, ADR-011 item 3).
  The authoritative rule text is `COMBAT_RULES.md` §3.4 step 6 — "Final Damage
  applied to active Pet HP (PetState.HP)".
- `GameServer.Domain/Battle/BossState.cs` — "does not derive HP from the player's
  HP … the player's ATK/DEF" → "the active Pet's", since the Pet is the combat
  character and the Player has no such pool.
- `GameServer.Domain/Bosses/BossDefinitions.cs` — same "player's HP/ATK/DEF"
  negation corrected to the active Pet for the same reason.
- `GameServer.Domain/Match3/BattleEvent.cs` — `BattleWonEvent`/`BattleLostEvent`
  `FinalPlayerHp` docs said "The player's HP at battle end (`GAME_STATE.md` §2.2)";
  corrected to the active Pet's HP under `GAME_STATE.md` §2.3 with the fixed
  protocol label preserved. Same correction on the two `BattleEventType` summaries,
  the two `ForBattleWon`/`ForBattleLost` parameter docs, and the "damaged the
  player" → "damaged the Pet" phrasing in the `BattleLost` factory.
- `GameServer.Application/Battle/BattleStateService.cs` — the Boss→Player write-back
  comment cited `Player.HP` as the write target; clarified that the quoted
  `COMBAT_RULES.md` §3.4 phrase names the Player *side* while the write is onto the
  active Pet's `PetState.HP`. The terminal-check comment now says the Pet HP check
  is the Player side's.
- `GameServer.Api/Hubs/BattleHub.cs` — `BattleStateUpdated.PlayerState` parameter
  doc said "the authoritative `PlayerState` projection"; re-pointed to the
  Combo/MatchCount projection (BattleState root, §2.2) with the fixed wire label and
  ADR-011 items 1/2/6 stated. `PlayerStatePayload` type doc said "the two
  `PlayerState` fields this stage implements"; re-pointed to the two root
  Match/Combo values, noting the type name and wire member are protocol labels, not
  an ownership path.
- `GameServer.Api/Hubs/BattleEventWireProjection.cs` — the three `FinalPlayerHp`
  docs (`BattleEventWireDto` parameter, `BattleWon`, `BattleLost` factories) said
  "the player's HP at battle end (`GAME_STATE.md` §2.2)"; corrected to the active
  Pet's HP (§2.3, ADR-011 items 3 and 5) with the label unchanged.

Tests (`tests/backend/`):

- `GameServer.Domain.Tests/DamagePipelineTests.cs` — `BossAttack_ShouldWriteThePlayersHp`
  comment: quoted §3.4 phrase now qualified as naming the Player side with
  `PetState.HP` as the write target; `BossAttackInputs` summary "the player's
  DEF/Element" → the active Pet's.
- `GameServer.Application.Tests/BossResponseTests.cs` — the Boss→Player write-back
  comment and the step-5/"player's DEF" comment corrected to the active Pet
  (`created.PetState.DEF` is what the code already read).
- `GameServer.Domain.Tests/BossStateTests.cs` — removed the stale comparison to
  "`PlayerState` and `PetState`" (the type no longer exists); "the player's HP/ATK/
  DEF" → the active Pet's.
- `GameServer.Domain.Tests/MatchComboAccountingTests.cs` — "`PlayerState` no longer
  exists" → the accurate statement that there is no `PlayerState` type or node in
  the Domain.
- `GameServer.Domain.Tests/BattleEventEmissionTests.cs` — same "no longer exists"
  phrasing corrected.
- `GameServer.Domain.Tests/ResourceGenerationTests.cs` — two Power comments cited
  `GAME_STATE.md` §2.2 (Match/Combo accounting); Power is a `PetState` member, so
  corrected to §2.3 (`COMBAT_RULES.md` §1.1).
- `GameServer.Application.Tests/VictoryDefeatTests.cs` — "The Player HP terminal
  check" → the Pet HP check (the Player side's); the `finalPlayerHp` assertion
  comment names the active Pet.

Task files:

- `tasks/backlog/TASK-026-…md` → `tasks/active/TASK-026-…md` (READY → IN PROGRESS →
  IN REVIEW → DONE), then moved to `tasks/completed/`

Not changed (deliberate):

- `docs/` — **no change.** In particular `docs/02-technical/GAME_STATE.md` was
  **not** edited; its stale implementation notes are recorded under "Documentation
  Follow-up" below for a separate synchronization task.
- `tasks/completed/*` — untouched (TASK-025 remains the immutable baseline).
- `src/frontend/client/` — untouched; its 174 tests pass unchanged against the
  preserved wire contract.
- Infrastructure / Redis / migrations / Pet persistence / Pet Level derivation —
  untouched.

Deliberately retained verbatim (protocol labels and quoted rule text — not defects):

- the wire members `playerState`, `finalPlayerHp`, `target="player"`, and the DTO
  type name `PlayerStatePayload` (ADR-011 item 6 forbids renaming them);
- the quoted rule phrases `"Final Damage applied to Player.HP"` and
  `BOSS_RULES.md` §5 item 4's resolution-order line "→ terminal Player HP check"
  in four comments, each now explicitly qualified as naming the Player *side*
  (the Pet being that side's combat character) rather than a Player state pool.

### Validation Results

Baseline (before this task's edits) — `dotnet test src/backend/GameServer.sln -c Debug`:

```text
Passed!  Domain:         844  Failed: 0
Passed!  Application:    101  Failed: 0
Passed!  Infrastructure:  44  Failed: 0
Passed!  Api:             85  Failed: 0
Total: 1074 passed, 0 failed
```

After this task's edits:

- `dotnet build src/backend/GameServer.sln -c Debug` — PASS (0 errors)
- `dotnet test src/backend/GameServer.sln -c Debug` — PASS (1074 tests total):
  - Domain: 844 PASS, 0 FAIL
  - Application: 101 PASS, 0 FAIL
  - Infrastructure: 44 PASS, 0 FAIL
  - Api: 85 PASS, 0 FAIL
- `npm run test:run` (`src/frontend/client`) — PASS (174 tests, 12 files), unchanged
- Identical counts and zero failures before and after, with no assertion weakened
  and no expected value changed.
- Pre-existing `MSB3277` EF `Microsoft.EntityFrameworkCore.Relational` version
  warnings (10.0.4 vs 10.0.12) reproduced unchanged in both runs; not suppressed.

### Invariant Coverage (existing tests — none were added or duplicated)

```text
Player has no combat authority
  MatchComboAccountingTests.BattleState_ShouldExposeNoPlayerStateNode
    → no `PlayerState` member on BattleState AND no type of that name in the
      Domain assembly (ADR-011 items 1 and 5)
  MatchComboAccountingTests.CombatStats_ShouldBeOwnedByPetStateAndNotFlatOnBattleState
    → HP/MaxHP/ATK/DEF/Power/Crit are not flat BattleState members

BattleState ownership
  MatchComboAccountingTests — root Combo/MatchCount shape and initial values

Player-side combat authority is PetState
  DamagePipelineTests.BossAttack_ShouldWriteThePlayersHp / …ClampThePlayersHpAtZeroOnOverkill
  BossResponseTests.BossBasicAttack_ShouldWriteThePlayersHp
  VictoryDefeatTests.BattleLost_* — Boss→Player damage lands on PetState.HP,
    read through result.Value.State.PetState.HP
  ResourceGenerationTests / PlayerEffectHealingTests — Power and Heal write PetState
    and leave Combo/MatchCount (root) untouched

Wire
  ApiIntegrationTests.WireContract_ShouldBeUnchangedByTheCombatStateOwnershipRefactor
    → `playerState` == exactly { combo, matchCount }; `petState` == passive-only;
      no hp/maxHp/atk/def/crit/power leaks into either; envelope unchanged
  ApiIntegrationTests.WireContract_FinalPlayerHp_ShouldCarryTheActivePetsHp
    → `finalPlayerHp` == Domain PetState.HP end-to-end
  BossResponseWireTests / BossWireProjectionTests
    → source="boss", target="player" fixed labels preserved on the wire
```

No required invariant was found missing, so no test was added (per the task's
"do not create duplicate tests" instruction).

### Quality Review (`quality/review.md` §1)

- **Correctness** — every corrected comment now matches the authoritative text:
  `COMBAT_RULES.md` §1.1/§3.2/§3.4 step 6 ("active Pet HP (PetState.HP)"),
  `GAME_STATE.md` §2.2 (root accounting) / §2.3 (PetState combat), ADR-011 items
  1–3/5–6. No comment now asserts a Player combat pool.
- **Architecture** — no layering, dependency-direction, or module-boundary change;
  Domain stays framework-independent; the Api still projects one-to-one and
  computes nothing.
- **Scope** — comment/doc-comment text only. No field, access path, signature,
  formula, ordering, event payload, Redis/database schema, migration, or
  persistence behavior touched. No unrelated refactor; scope discipline
  (`development/refactor.md` §4) held.
- **Tests** — HIGH-risk depth (`core/validation.md` §2): full suite re-run both
  before and after, plus the wire-contract regressions, plus frontend suite.
- **Documentation** — no `docs/` edit was required or authorized by this task's
  scope; the stale `GAME_STATE.md` notes are reported as a follow-up, not silently
  corrected.
- **Security** — nothing touched; no auth/permission or data-exposure surface.
- **Performance** — no executable line changed, so no hot-path regression is
  possible (no new query or allocation).
- **Maintainability** — comments were corrected, not abstracted; no interface,
  factory, event bus, generic repository, compatibility shim, or new type was
  added (`AGENTS.md` §9). Net complexity is unchanged.
- **Determinism** — server authority and RNG untouched; the Damage Pipeline remains
  pure and parameter-driven; the fixed resolution order (`GAME_RULES.md` §17) and
  the documented term ("active Pet HP") are now correctly traced in the comments
  rather than mis-described.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no migrations and no database schema change
- [x] Confirmed no Redis schema or behavior change
- [x] Confirmed no gameplay/formula/crit/element/combo/passive/boss/Match-3 change
- [x] Confirmed no Player combat pool, no `PlayerState` Domain type, no
      `CombatPlayerState`/`LegacyPlayerState`/compatibility shim
- [x] Confirmed no SignalR protocol rename: `playerState`, `PlayerStatePayload`,
      `finalPlayerHp`, `target="player"` all unchanged
- [x] Confirmed no Pet persistence or Pet Level derivation change
- [x] Confirmed TASK-027/028/029/030/031/032 not implemented

### Documentation Follow-up (NOT fixed by TASK-026)

`docs/02-technical/GAME_STATE.md` retains implementation notes that are stale
relative to TASK-025 and were **not modified by TASK-026**:

- §2.2 "Implementation note" (≈L849–853) — "The Domain model currently nests these
  two values under a type named `PlayerState`."
- §2.2 item 2 (≈L841–847) — "`BossState` (§2.4) still does not exist, so §2's full
  shape still cannot be produced and `POST /api/battle/start` still cannot create a
  battle."
- §2.3 closing note (≈L893–895) — "The Domain model currently nests the combat
  stats under a type named `PlayerState`."

These describe the pre-TASK-025 Domain. `PlayerState.cs` is deleted, `BossState`
exists, `POST /api/battle/start` creates battles, and the Damage Pipeline is
implemented. `GAME_STATE.md` is the authoritative technical contract, so correcting
it is a documentation-synchronization change outside TASK-026's authorized scope
(`AGENTS.md` §17; the task's Affected Files section marks `docs/` as "already
correct"). **Requires a separate documentation synchronization task.** No ADR was
created for it.

### Next Step

TASK-027 — Relic loadout and equipment mechanics (not implemented here).
