# TASK-305 — ADR-029 Slice 2: Shared Cast Contract and Actor-Relative Effect Resolution

<!--
  GEN-TASK EXECUTION MANIFEST — STATUS: DONE

  Prepared by the ADR-029 Slice 2 readiness pass. No runtime code was written,
  no documentation was changed, and nothing was committed by that pass.
  Picked up by the executing agent: READY -> IN PROGRESS, backlog/ -> active/.
  Executed: IN PROGRESS -> IN REVIEW -> DONE (two-stage commit protocol,
  TASK_LIFECYCLE.md §6). Phase A commit `3e2518d`; this record is the Phase B
  filing.

  Scope decisions recorded in "Scope Decisions Taken by the Readiness Pass" were
  taken inside the authority ADR-029 / TASK-303 already granted. They are
  recorded so the executing agent does not re-derive them, and so the operator
  can overrule any of them before pickup (pre-pickup edits are permitted; the
  P-2/P-5 exact-set rule governs after pickup).
-->

---

## Metadata

```text
Task ID:           TASK-305
Type:              FEATURE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   testing/test-scenario-generation
Dependencies:      TASK-304 (DONE), TASK-303 (DONE), ADR-029 (Accepted),
                   TASK-028 / TASK-102 / TASK-107 / TASK-109 / TASK-111 (DONE —
                   the Player cast path this task generalizes)
Declared Files:    src/backend/GameServer.Domain/Cards/CardCastActor.cs
                   src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
                   src/backend/GameServer.Domain/Cards/CardCastRejectionReason.cs
                   src/backend/GameServer.Domain/Cards/CardCategory.cs
                   src/backend/GameServer.Domain/Cards/CardDefinition.cs
                   tests/backend/GameServer.Domain.Tests/Cards/BossCardCastExecutorTests.cs
                   tests/backend/GameServer.Domain.Tests/Cards/CardCastPlayerCompatibilityTests.cs
                   tests/backend/GameServer.Domain.Tests/PetStateEquippedCardsTests.cs
                   (re-planned before Phase A staging: the eighth entry is
                   strictly required to update ONE stale closed-set assertion; see
                   "Declared File Set Re-plan (Executing Agent)" below)
```

**Lifecycle note:** the readiness pass executed `BACKLOG -> READY` validation only.
`READY -> IN PROGRESS` (and the `backlog/ -> active/` move) is the executing
agent's transition.

---

## Objective

Make the existing Card cast contract serve both actors, without a second
executor and without changing Player behavior: generalize
`CardCastExecutor` behind an explicit cast actor so a `Boss Card` cast is
validated (actor identity, category, `EffectiveCardCost` with an empty modifier
set, `BossState.Power` sufficiency) and resolved (actor-relative self/opponent
effects, deduction from the Boss's own Power) through the same machinery the
Player's cast uses, and prove by regression tests that the Player's cast
contract, one-cast allowance, Relic triggers and cost modifiers are unchanged.
This is ADR-029 Slice 2's exit condition and nothing more.

---

## Authoritative References

- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` §2.3 items 8–10, §2.4 item 11, §2.5 items 12–15, §4 (Slice 2 exit), §3 (U-2 is explicitly not required by Slice 2)
- `docs/01-game-design/CARD_RULES.md` §3 item 9 — the canonical owner of the actor-relative split (shared: validation order, `EffectiveCardCost` composition, effect resolution, deduction→effect→event; Player-only: one-cast allowance, Relic trigger step, Relic `CardCost` modifiers, loadout/ownership)
- `docs/01-game-design/CARD_RULES.md` §1 item 1.1, §2, §3.6, §4.1 — the third category, the target statements for Basic/Pet Skill effects, `EffectiveCardCost`
- `docs/01-game-design/BOSS_RULES.md` §4.2 items 1–8 — Boss Card capability, cost carrier, effect ownership/target, cooldown carrier, event, no Player entitlement
- `docs/01-game-design/BOSS_RULES.md` §4.1 items 1–7 — `BossState.Power` ownership, 0–100, isolation from `PetState.Power`, no conversion from `SkillCharge`
- `docs/02-technical/GAME_STATE.md` §2.4.3 — `Power` shape/spend/initial/clamp, `SkillCooldown` carrier (retained), `SkillCharge` transitional
- `docs/02-technical/GAME_STATE.md` §5.1 item 9 — a Boss cast's `Power`/cooldown writes belong to the round's single write-back, never to a separate `Sequence`
- `docs/02-technical/GAME_EVENTS.md` §2 (`CardCast`, `PetSkillCast`, `PowerChanged`, `BossCardCast`) — `PowerChanged` reports `PetState.Power`; `BossCardCast` is the Boss cast's event
- `docs/02-technical/ARCHITECTURE.md` §3 — `CardCastExecutor` is the shared cast contract; `BossActionPolicy` is Slice 3
- `docs/00-overview/MVP_SCOPE.md` §1 (Boss Power + minimum Boss Card capability IN), §2/§3
- `tasks/completed/TASK-303-boss-power-and-boss-card-design-authority-reconciliation.md` §4 D4, §5, §6 (Slice 2 semantics and ordering), §11 item 3 (Boss Card persistence deliberately undefined)
- `tasks/completed/TASK-304-boss-power-slice-1-implementation-and-verification.md` §4 (the `BossState.Power` foundation this task consumes)

---

## Scope

### In Scope

- **Actor-aware cast entry point.** `CardCastExecutor` keeps its existing
  Player entry point (unchanged behavior) and gains an explicit cast-actor
  parameter/context so validation, cost composition, deduction and effect
  resolution branch on the acting side instead of being Pet-anchored.
- **`CardCategory.BossCard`** as the third Domain category value (`CARD_RULES.md`
  §1/§1.1), Domain-only: no `PlayerUnlockedCard` row, no loadout slot, no
  collection projection, no persistence, no database change.
- **Boss-actor validation**, in `CARD_RULES.md` §3's order: definition is the
  Boss's own `Boss Card` (`INVALID_CARD` otherwise) → `EffectiveCardCost` composed
  with an **empty** modifier set → `BossState.Power >= EffectiveCardCost`
  (`INSUFFICIENT_POWER` otherwise). No loadout lookup and no one-cast allowance
  read or write.
- **Actor-relative effect resolution** for the closed `CardEffectType` set:
  self effects (`Heal`, `Shield`, `Power`) resolve against the casting actor's own
  state (`BossState.HP`/`MaxHP`, `BossState.ActiveStatusEffects`,
  `BossState.Power`); the opponent effect (`Burn`) resolves onto the other
  actor's status effects with the casting actor's `StatusEffectSource`.
  `PercentMaxHp` interprets against the casting actor's `MaxHP`.
- **Explicit refusal of effect/actor combinations the documents cannot supply
  inputs for.** A Boss-actor `Damage` or `Crit` effect throws a documented
  `InvalidOperationException` instead of deriving a value (no `BossState.Crit`
  member exists, and `ADR-026` D3 keeps all five MVP abilities at
  `BaseDamage = 0` with the Boss's only damage being the ATK-gated instance).
- **Rejection contract preserved**: a rejected cast of either actor changes
  nothing and emits nothing (`CARD_RULES.md` §3 item 3); no rejection reason is
  added, so `CardCastRejectionCodes` and the wire are untouched.
- **Player regression coverage** proving the Player path is byte-identical in
  behavior: loadout check, `Basic`/`PetSkill` category gate,
  `PetState.CardCostModifiers` composition, `PetState.Power` deduction, the
  one-cast allowance, `CardCast`/`PetSkillCast`/`PowerChanged`/outcome events and
  `Sequence` advance all unchanged.

### Out of Scope

- **Boss AI / action selection / match-versus-cast choice** — `BossActionPolicy`,
  `BOSS_RULES.md` §4.3, ADR-029 Slice 3. Nothing in this task selects a cast.
- **Cooldown gating and cooldown setting** — `BOSS_RULES.md` §4.3 items 1 and 3,
  `GAME_STATE.md` §2.4.3: Slice 3's documented concern. This task only guarantees
  it writes neither `SkillCooldown` nor `SkillCharge`.
- **`BossCardCast` emission, wire projection and client presentation** —
  `GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §3.2.30; ADR-029 Slice 3 per
  TASK-303 §6. The Boss branch of the cast contract emits no cast event.
- **Wiring the Boss cast into `BattleStateService` / `BattleHub` / the round
  (step 18a/18c)** — Slice 3. No new hub method, no new REST member, no new
  public Application entry point.
- **Boss Card content provisioning, `PowerCost` values, persistence carrier,
  `DATABASE.md` change, migration** — U-2 is unresolved (`ADR-029` §3) and
  TASK-303 §11 item 3 leaves the carrier deliberately undefined.
- **Migration of `Earthquake`, `Flame Burst`, `Root`, `Drain Power`,
  `Thunder Strike`** — ADR-029 Slice 4, in that order with `thunder-strike` last.
- **`SkillCharge` retirement, `BossSkillCast` retirement, legacy Boss handler
  changes** — ADR-029 §2.9 parity gate, Slice 5.
- **Effect-vocabulary extension** (ATK buff, ATK debuff, Power drain,
  next-qualifying-attack modifier) — needed by Slice 4's migrated abilities; not
  authorized by Slice 2 and not added here.
- **Fifth Gem / Power-drain Gem, balance tuning, Relic behavior, frontend,
  database, unrelated refactoring.**

---

## Current State

`src/backend/GameServer.Domain/Cards/CardCastExecutor.cs` exposes
`Execute(BattleState, CardDefinition)` with no actor parameter: it validates the
card against `state.PetState.EquippedCards` (line 35–53), rejects any category
other than `Basic`/`PetSkill` (line 56), composes
`EffectiveCardCost.Compose(cardDefinition.PowerCost, state.PetState.CardCostModifiers)`
(line 61), gates on `state.PetState.Power` (line 64) and
`state.CardCastsUsedThisTurn` (line 82), resolves every effect against
`state.PetState` (self) or `state.BossState` (opponent), and returns a committed
state with `Sequence + 1` and the allowance spent (line 428–444). Every effect
site hardcodes the Player's side: `StatusEffectSource.Player` (lines 160, 237),
`HealResolution.ApplyPetHeal` (line 141), `DamageParty.Player → Boss` (line 288),
`Combat.HealResolution`/`AttackerCrit: state.PetState.Crit`/
`NextAttackCritModifiers` (lines 289–291), `BattleEvent.CreateCardCast` (line 109),
`BattleEvent.ForBattleWon` (line 356),
`BattleEvent.ForPowerChanged(… state.PetState.Power)` (lines 114–118, 179–183).

`CardCategory` declares exactly `Basic = 0`, `PetSkill = 1` and states the set is
closed; `CardDefinition.Category` and
`GameServer.Infrastructure/Postgres/Configurations/CardDefinitionConfiguration.cs`
repeat the two-value set (persisted as the enum's numeric value, with no database
CHECK). `BossState.Power` (integer 0–100, default 0) already exists and is
already persisted, projected and rendered (TASK-304). No `BossCard` symbol, no
`BattleEventType.BossCardCast`, and no `BossCardExecutor` exists anywhere.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-305:
```text
src/backend/GameServer.Domain/Cards/CardCastActor.cs
src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
src/backend/GameServer.Domain/Cards/CardCastRejectionReason.cs
src/backend/GameServer.Domain/Cards/CardCategory.cs
src/backend/GameServer.Domain/Cards/CardDefinition.cs
tests/backend/GameServer.Domain.Tests/Cards/BossCardCastExecutorTests.cs
tests/backend/GameServer.Domain.Tests/Cards/CardCastPlayerCompatibilityTests.cs
tests/backend/GameServer.Domain.Tests/PetStateEquippedCardsTests.cs
```

*(Note: The task manifest file itself is excluded from the implementation
declared file set. The list above must strictly match the `Declared Files:`
field in `## Metadata`. If implementation proves a further file is strictly
required, that is a re-plan of this record **before** pickup or before the
Phase A staging verification — never a post-hoc edit to legalize a commit.)*

---

## Declared File Set Re-plan (Executing Agent)

Recorded **before** the Phase A staging verification, per the note above, so the
eighth declared file is a recorded re-plan rather than a post-hoc edit.

**Finding.** Adding the manifest-required `CardCategory.BossCard` makes one
pre-existing Domain test fail:
`tests/backend/GameServer.Domain.Tests/PetStateEquippedCardsTests.cs`
`CardCategory_ShouldBeTheDocumentedClosedSet` asserted, at pickup, that
`Enum.GetNames<CardCategory>()` is exactly `{ "Basic", "PetSkill" }` with the
comment "CARD_RULES.md §1 / DATABASE.md §3: exactly Basic and PetSkill. No third
category exists in MVP."

**Why the test is stale, not the implementation.** `CARD_RULES.md` §1 and §1.1
were amended to three categories under `TASK-303` / `ADR-029` (`CARD_RULES.md`
v1.11) before this task existed, and `ADR-029` §2.5 items 12–13 put the Boss Card
category into MVP. `AGENTS.md` §2 puts `docs/01-game-design/CARD_RULES.md` above
the test, and `AGENTS.md` §15 requires deciding whether the implementation, the
test, or the documentation is wrong: the documentation is authoritative and
already changed, so the assertion is the outdated artefact. It cannot be made to
pass without editing it, because it enumerates the closed enum set itself — and
the manifest's own acceptance criterion requires `CardCategory.BossCard` to
exist.

**What changes.** That single assertion and its comment only: the expected set
becomes `{ "Basic", "BossCard", "PetSkill" }`, citing `CARD_RULES.md` §1 (v1.11)
/ §1.1 and noting that `DATABASE.md` §3's stored set for a provisioned
`CardDefinition` row is unchanged. **No other test in this file, and no other
pre-existing test, is modified.** Every mandated cast suite
(`CardCastExecutorTests`, `CardCastTurnAllowanceTests`, `EffectiveCardCostTests`,
`BattleStateServiceCardCastTests`, `CardCostRelicIntegrationTests`,
`PowerChangedEmissionTests`, `BattleHubCardCastTests`,
`BattleHubPetSkillCastTests`) runs unmodified.

**Operator attention.** This is the only declared-scope expansion in the slice:
one assertion in one test file, forced by the manifest-required enum value. It is
called out in the task's final report; the operator may overrule it.

---

## Scope Decisions Taken by the Readiness Pass

Recorded so they are not re-derived; each is inside documented authority, and
each may be overruled by the operator before pickup.

1. **The third category lands here, Domain-only.** `CardCategory.BossCard` is
   added because the actor-identity responsibility this task owns is exactly
   "a Player cast can never be a Boss Card, and a Boss cast can never be a
   Basic/Pet Skill Card" (`BOSS_RULES.md` §4.2 item 8, `CARD_RULES.md` §1.1
   items 2–4). It carries no persistence: `CARD_RULES.md` §1.1 item 5,
   `ADR-029` §2.5 item 15 and TASK-303 §11 item 3 all leave the Boss Card
   persistence carrier undefined, so `DATABASE.md` is **not** edited and no
   `BossCard` row may be provisioned. The enum value must be documented as
   domain-only in its own XML doc (the current text closes the set at two).
2. **No Boss cast event in this slice.** The Boss branch emits effect events
   (`PowerChanged` only where the Pet's Power is affected) and **no**
   `CardCast`/`PetSkillCast`/`BossCardCast`, no outcome event, and does **not**
   advance `Sequence` (the round's single write-back owns it,
   `GAME_STATE.md` §5.1 item 9). The cast-event surface is Slice 3's, per
   TASK-303 §6's slice map.
3. **Domain-only seam; no Application/hub caller.** Nothing calls a Boss cast
   until Slice 3, and ADR-029 forbids a second code path
   (`AGENTS.md` §9, `ARCHITECTURE.md` §5). The capability is exercised by the
   Domain tests in this task's declared set.
4. **No new rejection reason.** The Boss branch reuses `InvalidCard` and
   `InsufficientPower`, keeping `CardCastRejectionCodes.ToContractCode` and
   `SIGNALR_PROTOCOL.md` §5 item 3 untouched; the XML docs of those two members
   are widened to state both actors.

---

## Acceptance Criteria

- [ ] One cast contract serves both actors: `CardCastExecutor` exposes an actor-aware entry point, and no second executor, dispatcher, or duplicated effect-resolution path exists.
- [ ] The existing Player entry point's behavior is unchanged; every pre-existing Domain/Application/Api cast test passes **unmodified** (`CardCastExecutorTests`, `CardCastTurnAllowanceTests`, `BattleStateServiceCardCastTests`, `CardCostRelicIntegrationTests`, `PowerChangedEmissionTests`, `BattleHubCardCastTests`, `BattleHubPetSkillCastTests`).
- [ ] `CardCategory.BossCard` exists and its XML doc records that it is Domain-only with no persistence contract, no loadout slot, no collection visibility, and no Player entitlement.
- [ ] A Player-actor cast of a `BossCard` definition is rejected `INVALID_CARD`; a Boss-actor cast of a `Basic` or `PetSkill` definition is rejected `INVALID_CARD`.
- [ ] A Boss-actor cast composes `EffectiveCardCost` with an empty modifier set (equal to the authored `PowerCost`), validates `BossState.Power >= EffectiveCardCost` (`INSUFFICIENT_POWER` otherwise) and deducts exactly that cost from `BossState.Power`.
- [ ] A Boss-actor cast neither reads nor writes `CardCastsUsedThisTurn`, `PetState.Power`, `PetState.CardCostModifiers`, `PetState.EquippedCards`, `Sequence`, `RngState` unless an effect requires it, `SkillCharge`, or `SkillCooldown`.
- [ ] A Boss-actor cast emits no `CardCast`, `PetSkillCast`, `BossCardCast`, `BattleWon`, or `BattleLost` event, and no `PowerChanged` for a Boss-side Power change.
- [ ] Self effects (`Heal`, `Shield`, `Power`) resolve against the casting actor's own state, with `PercentMaxHp` interpreted against the casting actor's `MaxHP` and the Boss-side clamp/`ApplyBossHeal`/`StatusEffect` source rules unchanged.
- [ ] The opponent effect (`Burn`) resolves onto the other actor with the casting actor's `StatusEffectSource` and the authored magnitude/duration.
- [ ] A Boss-actor `Damage` or `Crit` effect throws a documented `InvalidOperationException` (no `BossState.Crit` exists; `ADR-026` D3 authors no Boss card damage); no Boss crit, damage or magnitude value is authored.
- [ ] A rejected cast of either actor changes no state value and emits no event (`CARD_RULES.md` §3 item 3).
- [ ] No database migration, `DATABASE.md` change, `SIGNALR_PROTOCOL.md`/wire change, API change, frontend change, or `SkillCharge`/`BossSkillCast`/legacy Boss Skill change.
- [ ] `dotnet test src/backend/GameServer.sln` passes at the risk-appropriate validation depth (`core/validation.md` §2), and `git diff --check` is clean.
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain only — Cards)
[ ] src/frontend/client/ (no change — the Player CardCast wire contract is untouched)
[x] tests/ (Domain unit + Player regression)
[ ] docs/ (no change — the contracts this task implements are already authoritative)
```

---

## Implementation Notes

- Keep `CardCastExecutor.Execute(BattleState, CardDefinition)` as the Player entry
  point delegating to the actor-aware overload, so no caller and no existing test
  needs editing.
- The actor context is a small Domain type (`CardCastActor` or an equivalent
  explicit context) that names the acting side; it must not become a generic
  multi-actor framework (`AGENTS.md` §9, `ADR-029` §5 rejects that alternative).
- Reuse existing write sites: `EffectiveCardCost.Compose`,
  `HealResolution.ApplyPetHeal` / `ApplyBossHeal`,
  `StatusEffectLifecycle.Apply` / `ApplyShield`, `StatusEffect.TriggerBased` /
  `TurnBased`, `NextAttackCritModifiers`, `DamagePipeline.Calculate`,
  `BattleEvent.ForPowerChanged`. This task adds no new effect math.
- The Next-Attack-Crit consumption block (current lines 401–412) stays exactly
  where it is for the Player path; the Boss path has no Crit collection to
  consume.
- `PowerChanged`'s `power` member is `PetState.Power`
  (`GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §3.2.24), so a Boss-side Power
  change is reported by the `bossState.power` state projection (TASK-304), never
  by `PowerChanged`.
- Test fixtures may use one synthetic Boss Card definition; those magnitudes are
  fixtures, **not** authored content — `U-2` (`PowerCost`) and `U-4`/`U-5`
  magnitudes remain unresolved and must not be recorded as rules anywhere.
- Do not add a `BossCardExecutor`, a `BossCardCastService`, an interface, a
  factory, or a dispatch table.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — Domain: actor identity, category gate per actor,
                         EffectiveCardCost with the empty modifier set, Boss
                         Power sufficiency and exact deduction, self effects on
                         BossState, opponent Burn onto the Pet, unsupported
                         Boss-actor Damage/Crit refusal, rejection writes nothing
[x] Integration tests  — no new boundary is introduced; the pre-existing
                         Application/Api cast suites are the integration proof
                         that the Player path did not move
[x] Gameplay scenarios — Given a Boss with Power P and its Boss Card of cost C,
                         When the cast resolves, Then BossState.Power = P − C,
                         the self effect lands on BossState, the opponent effect
                         lands on PetState, and the Pet's allowance, Power,
                         modifiers, Sequence and cast events are unchanged
```

### Key Edge Cases
- `BossState.Power` exactly equal to `EffectiveCardCost`, and one below it (the rejection path must change nothing).
- A zero-cost Boss Card: validated and deducted as `0` (`CARD_RULES.md` §3.6 item 5; the same Power Charge rule).
- `PercentMaxHp` self effect truncated against `BossState.MaxHP`, not `PetState.MaxHP`.
- Boss at `HP = 0`: a self `Heal` is a no-op (`COMBAT_RULES.md` §4 item 1's dead-target rule).
- A Boss self `Power` effect clamping at `100` (`GAME_RULES.md` §12, `GAME_STATE.md` §2.4.3 item 5).
- The Player path must reject `BossCard` and must still reject `Undetermined`/unsupported effect values exactly as before.

---

## Stop Conditions

- If a required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7.
- If implementing the Boss branch appears to require authoring a `PowerCost`, a cooldown value, an effect identity, or any magnitude: STOP — that is `U-2`/`U-4`/`U-5` (Slice 4) or a design change, not this task.
- If the Boss branch appears to require a change to `BattleStateService`, `BattleHub`, `BattleEvent`, `CardCastExecutionResult`, `DATABASE.md`, or any wire contract: STOP and re-plan the record — that is Slice 3 or a separate task.
- If a second cast path/executor looks necessary to make either actor work: STOP — `ADR-029` §2.3 and `AGENTS.md` §9 forbid it.
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose.

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `3e2518d3c6dc28621c4459ddf839bb2d62e21f23`
- Subject: `feat: add actor-aware cast contract and boss card effect resolution`
- Owns: `TASK-305`
- Files:
  ```text
  src/backend/GameServer.Domain/Cards/CardCastActor.cs
  src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
  src/backend/GameServer.Domain/Cards/CardCastRejectionReason.cs
  src/backend/GameServer.Domain/Cards/CardCategory.cs
  src/backend/GameServer.Domain/Cards/CardDefinition.cs
  tests/backend/GameServer.Domain.Tests/Cards/BossCardCastExecutorTests.cs
  tests/backend/GameServer.Domain.Tests/Cards/CardCastPlayerCompatibilityTests.cs
  tests/backend/GameServer.Domain.Tests/PetStateEquippedCardsTests.cs
  ```
- Shared with: `none`
- Unowned / pre-existing: `none`

*(Cosmetic note: the Phase A commit subject is prefixed by a UTF-8 BOM, an
artifact of the PowerShell commit-message file. The same artifact already exists
in earlier repository commits (`dbafdf3`, `996652c`). `P-6` forbids amending, so
it is disclosed rather than repaired. No file content in the commit carries a
BOM.)*

### Changed Files
- `src/backend/GameServer.Domain/Cards/CardCastActor.cs` — **new**. The explicit
  acting side (`Player`, `Boss`) the shared cast contract branches on
  (`CARD_RULES.md` §3 item 9, `ADR-029` §2.3 item 9). An identity only: no
  policy, no dispatch table, no second executor (`AGENTS.md` §9).
- `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs` — the pre-existing
  Player `Execute(state, card)` is kept and now delegates to a new
  `Execute(state, card, CardCastActor)`; the Player body was extracted verbatim
  into a private branch (verified character-for-character identical to the
  pre-change method body); a new Boss branch adds category gating, empty-modifier
  `EffectiveCardCost`, `BossState.Power` sufficiency and exact deduction,
  actor-relative self/opponent effect resolution, the explicit Damage/Crit
  representability refusal, and a result that advances no `Sequence` and emits no
  event.
- `src/backend/GameServer.Domain/Cards/CardCastRejectionReason.cs` — XML docs
  widened to state both actors for `InvalidCard` / `InsufficientPower` and to
  state that `CardNotInLoadout` / `CardCastAlreadyUsedThisTurn` are Player-only.
  **No enum member, value or contract code changed.**
- `src/backend/GameServer.Domain/Cards/CardCategory.cs` — adds
  `BossCard = 2` as the third category, documented Domain-only: no persistence
  contract, no loadout slot, no collection visibility, no Player entitlement
  (`CARD_RULES.md` §1.1).
- `src/backend/GameServer.Domain/Cards/CardDefinition.cs` — `Category` docs only:
  the third value and the unchanged stored-set constraint.
- `tests/backend/GameServer.Domain.Tests/Cards/BossCardCastExecutorTests.cs` —
  **new**. 34 focused Boss tests (actor/category both directions, empty modifier
  set, sufficient/insufficient/exact Power, zero cost, self Heal/Shield/Power,
  caster-MaxHP percentage, opponent Burn with Boss source, Damage/Crit refusal,
  no-side-effect rejection, no `Sequence`/allowance/`SkillCharge`/`SkillCooldown`
  change, no events).
- `tests/backend/GameServer.Domain.Tests/Cards/CardCastPlayerCompatibilityTests.cs`
  — **new**. 13 Player compatibility tests against the unchanged two-argument
  entry point (loadout, category gate, cost modifiers, allowance, `Sequence`,
  events, rejection codes) plus the `BossCard` rejection.
- `tests/backend/GameServer.Domain.Tests/PetStateEquippedCardsTests.cs` — one
  stale closed-set assertion updated to the documented three-category set
  (`CARD_RULES.md` §1 v1.11 / §1.1). See "Declared File Set Re-plan (Executing
  Agent)".

### Validation Results
```text
Suite                                                    Result
-------------------------------------------------------  ----------------------------------
GameServer.Domain.Tests (full, post-change)               PASS 1584 / 1584 (baseline 1537)
  CardCastExecutorTests (unmodified)                      PASS   35 /   35
  CardCastTurnAllowanceTests (unmodified)                 PASS    4 /    4
  EffectiveCardCostTests (unmodified)                     PASS   10 /   10
  BossCardCastExecutorTests (new)                         PASS   34 /   34
  CardCastPlayerCompatibilityTests (new)                  PASS   13 /   13
  PetStateEquippedCardsTests (one assertion re-planned)   PASS    8 /    8
GameServer.Application.Tests (full, post-change)          PASS 795 / 797 — 2 FAILED (pre-existing)
  BattleStateServiceCardCastTests (unmodified)            PASS   23 /   23
  CardCostRelicIntegrationTests (unmodified)              PASS    2 /    2
  PowerChangedEmissionTests (unmodified)                  PASS    9 /    9
GameServer.Api.Tests (full, post-change)                  PASS 382 / 382
  BattleHubCardCastTests (unmodified)                     PASS    5 /    5
  BattleHubPetSkillCastTests (unmodified)                 PASS    3 /    3
GameServer.Infrastructure.Tests (full, post-change)        PASS 472 / 476 — 4 FAILED (pre-existing)
Frontend vitest (src/frontend/client)                     PASS 1265 / 1265 (24 files)
Frontend build (npm run build)                            PASS
dotnet build src/backend/GameServer.sln                   PASS (0 errors)
git diff --check                                          clean
```

**Pre-existing failures, unrelated to this task and identical at baseline
(`a2c1ea2`) before any change:**
- `GameServer.Application.Tests.Balance.Tests.BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn`
  — `Assert.StartsWith` failure.
- `…BalanceEvidenceMeasurementCorrectionTests.TASK296_ReviewD_Canonical75RunCandidate_Verification`
  — `Assert.Equal` 8 vs 5.
- `GameServer.Infrastructure.Tests.BossPersistenceTests.Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments`
  and `…ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments` —
  `Assert.Contains` sub-string not found.
- `GameServer.Infrastructure.Tests.BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects`
  and `GameServer.Infrastructure.Tests.BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions`
  — `Assert.Equal` value mismatch (`flame-burst` `BaseDamage` 0 vs 150), a content
  provisioning drift in the Boss balance path.

None of them touches the Card cast contract, and none changed in count, name or
message between the baseline and post-change runs.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (the branch is a pure
      Domain function; no randomness, clock, or client input)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Boss Power + the
      minimum Boss Card capability are IN; the fifth Gem stays OUT)
- [x] Confirmed P-1…P-6 commit policy compliance (single Phase A commit; staged
      set equals the declared file set; subject carries no task ID; body carries
      `owns: TASK-305`; no amend/rebase/reset)
- [x] Confirmed Player cast path unchanged (the Player branch body is
      character-for-character identical to the pre-change method body; every
      Player regression suite passes unmodified)
- [x] Confirmed `SkillCharge`, `SkillCooldown`, `BossSkillCast` and the legacy
      Boss Skill path untouched (no file in that path changed; the Boss branch
      writes neither cooldown nor charge and emits no event)
- [x] Confirmed excluded layers untouched: no Application, Api, SignalR, Redis,
      persistence, migration, `DATABASE.md`, wire-contract, or frontend change
