# TASK-133 — Implement Server-Authoritative Relic Trigger and Effect Resolution

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section.

  PROVENANCE: created by TASK-131 per its follow-up identification. TASK-131
  D1–D11 decided the Relic Trigger/Condition/Effect contract; ADR-018 records
  the architectural consequence; TASK-132 lands the structured storage this
  task reads. This task is the GAME_RULES.md §17 step 11 implementation —
  "Trigger Relics" — which RELIC_RULES.md §8.7 records as NOT IMPLEMENTED.
-->

---

## Metadata

```text
Task ID:           TASK-133
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented mechanic,
                   capability, or system that already has a home in docs/ but
                   has not yet been built." Relic triggers, conditions, and
                   effects now have a complete documented contract:
                   RELIC_RULES.md §1–§8 + ADR-018. This is building it.)
Status:            DONE (implemented server-side: the Relic trigger/condition/
                   effect resolver in the Domain Relic module, its invocation at
                   GAME_RULES.md §17 step 11 in the existing BattleStateService
                   resolution pipeline, the RelicTriggered/PowerChanged events and
                   their §3.2.23/§3.2.24 wire projections, the equipped Relics'
                   definition resolution at battle start through
                   IRelicDefinitionLookup, and the unified EffectivePetATK
                   composition (COMBAT_RULES.md §5.6.6) the ATK effect feeds.
                   Zero gameplay values invented; zero new state, Redis key,
                   SignalR method, schema, or client logic. All backend and client
                   suites pass — see Completion Evidence.)
                   Lifecycle reconciliation: recorded DONE while the file remained
                   in tasks/backlog/; moved to tasks/completed/ per
                   TASK_LIFECYCLE.md §3 (direct execution verified, including
                   TASK-142's required implementation correction).
Risk:              HIGH (TASK_TYPES.md §4 — FEATURE baseline MEDIUM, "can be
                   HIGH if it touches combat / battle state / auth." This
                   touches the Damage Pipeline, the Resolution order, Crit
                   composition, battle-state write-back, and the SignalR
                   action/event surface.)
Priority:          CRITICAL (ROADMAP.md Phase 2's "All ~10 Relics + trigger/
                   stacking system"; GAME_RULES.md §17 step 11 is the last
                   unimplemented step of the documented resolution order.)
Primary Agent:     gameplay (TASK_TYPES.md §5 — Relic domain → Gameplay.
                   Supporting: backend for the Application-layer resolution
                   wiring and write-back, realtime for RelicTriggered emission.)
Supporting Agents: backend (BattleStateService resolution wiring, write-back,
                   DI), realtime (BattleEvent emission and the
                   SIGNALR_PROTOCOL.md §3.2.23 wire shape), testing
                   (rule-derived scenarios), review
Workflow:          development/feature.md
Skills:            gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   realtime/realtime-protocol-validation,
                   testing/test-scenario-generation,
                   quality/implementation-review
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-131 (DONE — the contract decision),
                   ADR-018 (DONE — the architectural record),
                   TASK-132 (the structured storage migration — BLOCKS this
                   task; the resolver cannot read a structured effect until it
                   lands)
Blocks:            A complete end-to-end MVP battle loop with Relics active
Estimate:          Complex (new resolution stage + condition evaluation +
                   effect application across ATK/Power/Crit/CardCost + event
                   emission + write-back)
```

---

## Objective

Implement `GAME_RULES.md` §17 step 11 — "Trigger Relics" — server-side: evaluate each equipped Relic's Trigger and structured Condition at the documented point in the resolution order, apply its structured effect when eligible, and emit `RelicTriggered` for each Relic whose effect actually applies.

The Relic's Trigger, Condition, and Effect come from its definition row
(`RELIC_RULES.md` §8) and its equipped-slot order from
`PetState.EquippedRelics[]` (`RELIC_RULES.md` §2.2/§4). The client requests
nothing new: Relics are never cast (`RELIC_RULES.md` §1, `CARD_RULES.md` §5).

---

## Authoritative References

- `docs/01-game-design/GAME_RULES.md` **§17 step 11 ("Trigger Relics")** — the emission point and its position in the fixed resolution order; §16 (Battle Event Model), §18 (server authority), §13 (Relic Rules), §13.7 (deterministic multi-Relic order), §13.8 (anti-infinite-chain)
- `docs/01-game-design/RELIC_RULES.md` **§8** — the Trigger/Condition/Effect contract this task implements: §8.1 (the three structured Condition forms and their evaluation against the current resolution state, with **no persistent Relic counters**), §8.2 (structured `EffectDefinition[]`, `valueType` set, loud rejection), §8.3 (`target`/`lifetime` vocabulary and per-`effectType` allowed combinations), §8.4 (effect lifetime is **independent** of trigger re-evaluation), §8.5 (the four provisioned rows as encoded values), §8.7 (startup status this task changes)
- `docs/01-game-design/RELIC_RULES.md` §1 (Relic structure and the `Reset/Cooldown` re-fire default), §2 + §2.1–§2.5 (equip rules; the battle-start snapshot into `PetState.EquippedRelics[]`; slot order — **already implemented, do not re-implement**), **§3 (the closed Trigger list and what each trigger means)**, **§4 (deterministic trigger order: equip-slot order, breadth-first chain queue)**, **§5 (anti-infinite-chain rule)**, §6 + notes 1–3 (the MVP Relic Reference; Emergency Core's continuous re-evaluation; the deferred Burning Curse row), §7 (Events)
- `docs/01-game-design/COMBAT_RULES.md` §2 items 2/3/5/7 — the Crit roll, its multiplier, its modifier sources naming Relics, and the **canonical `EffectiveCrit` composition** that Assassin Eye's `Crit` effect feeds (do not restate or re-derive it); §3 (the Damage Pipeline steps the applied effects feed); §5 (Burn damage)
- `docs/02-technical/ADR/ADR-018` — the architectural record of this contract; items 2–6 (the representation, the condition forms, no Relic counters, lifetime independence), item 10 (`RelicTriggered` stays `{ type, relicId }`)
- `docs/02-technical/ADR/ADR-017` — the `NextAttack` Crit modifier model whose consumption boundary the `NextAttack` lifetime reuses
- `docs/02-technical/ADR/ADR-001` — server authority
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState` — `HP`, `ATK`, `Power`, `Crit`, `EquippedRelics[]`, `StatusEffects[]`), §2.3.x (`NextAttackCritModifiers[]`), §2.4 (`BossState`), §5.1 (the single write-back boundary), §0 (implemented-state staging), §5.1.1 (the step 19a Status Effect pass this task must not duplicate)
- `docs/02-technical/GAME_EVENTS.md` §1.1 (the sequencing position of step 11's event), §2/§3 item 7 (`RelicTriggered` payload)
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.23** (`RelicTriggered` = `{ type, relicId }`, unchanged), §3.2.24 (`PowerChanged`, whose `"relic"` source value now has its first emitter if a Relic changes Power), §3.2.25 (the `effect summary` omission convention — do **not** add an effect summary member), §3.1/§4 (delivery and the `BattleState` projection), §2 (the client→server action list — **no new action is added**)
- `docs/02-technical/REDIS_STATE.md` §2 (active battle state serialization), §4 (the single write-back and `Sequence` compare-and-set)
- `docs/02-technical/DATABASE.md` §1 (the `RelicDefinition` structured shape and the Card contract precedent), §5 item 4 (provisioned content)
- `docs/00-overview/MVP_SCOPE.md` §1 (Relics — "~10 Relics", trigger system, 3–5 equipped), §4
- `AGENTS.md` §7 (invent no rule), §9 (anti-overengineering), §10 (server authority), §11 (determinism/RNG — a Relic effect introduces no new RNG), §12 (domain boundaries), §14, §15 (testing), §16 (task discipline), §20, §22
- `Existing implementation to extend (do not rewrite):` `src/backend/GameServer.Application/Battle/BattleStateService.cs` (`ResolveSwapAsync` — the resolution pipeline where step 11 belongs; `ExecuteCardCastAsync`; `ExecutePetSkillCastAsync`), `src/backend/GameServer.Domain/Passives/PassiveTracker.cs` (the charging precedent), `src/backend/GameServer.Domain/Combat/DamagePipeline.cs`, `src/backend/GameServer.Domain/Battle/NextAttackCritModifiers.cs`, `src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs`, `src/backend/GameServer.Application/Relics/RelicLoadoutService.cs`
- `tasks/completed/TASK-131-resolve-relic-trigger-and-effect-resolution-contract.md` — D1–D11
- `tasks/backlog/TASK-132-migrate-relic-structured-condition-and-effectdefinition-storage.md` — the storage prerequisite
- `tasks/completed/TASK-115-implement-server-authoritative-petskillcast-crit-and-burn.md` and `TASK-127-implement-boss-skill-step-1-damage-composition.md` — the precedent for implementing a documented resolution stage in this pipeline

---

## Scope

### In Scope

1. **Trigger evaluation at step 11:** evaluate each equipped Relic's Trigger against the resolution event being processed, at the point `GAME_RULES.md` §17 step 11 fixes, within the existing resolution pipeline.
2. **Structured Condition evaluation:** evaluate `MatchCountAtLeast(N)`, `ComboAtLeast(N)`, and `HpPercentageBelow(N)` (`RELIC_RULES.md` §8.1) against the current resolution state — reading the battle's **existing** cumulative Match count and Combo value and the active Pet's HP. **Introduce no Relic counter and no new battle-state member.**
3. **Deterministic ordering:** resolve eligible Relics in equip-slot order (`RELIC_RULES.md` §4.1–§4.2), read from `PetState.EquippedRelics[]`'s order. No re-sorting.
4. **Effect application:** apply each structured effect for its `effectType` — `ATK` (`Percentage`, `Battle`), `Power` (`Flat`, `Immediate`), `Crit` (`PercentagePoints`, `NextAttack`), `CardCost` (`Percentage`, `Battle`) — per `RELIC_RULES.md` §8.3's allowed combinations, through the existing documented write sites.
5. **Effect lifetime handling, independent of re-evaluation:** `Immediate` applies once and leaves no standing modification; `Battle` persists for the battle; `NextAttack` uses the existing `ADR-017` consumption boundary. A Relic whose effect lifetime ends **remains eligible to re-trigger** (`RELIC_RULES.md` §8.4).
6. **Anti-infinite-chain compliance:** implement `RELIC_RULES.md` §4.3's breadth-first chain queue and §5's per-Relic once-per-root-event safeguard — not a depth-first recursion and not an unbounded loop.
7. **`RelicTriggered` emission:** emit `{ type, relicId }` for each Relic whose effect actually applies, in the resolution's event sequence (`GAME_EVENTS.md` §1.1, §3 item 7). **Add no wire member**, including no effect summary (`SIGNALR_PROTOCOL.md` §3.2.25). Emit `PowerChanged` with `source: "relic"` where a Relic changes Power.
8. **Write-back:** persist the resulting `PetState`/`BattleState` through the existing single write-back boundary (`GAME_STATE.md` §5.1, `REDIS_STATE.md` §4). No new persistence path.
9. **Tests:** rule-derived Given/When/Then scenarios for each trigger, each condition form, each effect type, the ordering rule, the lifetime/re-evaluation independence, and the anti-infinite-chain safeguard.

### Out of Scope

- **The structured storage migration** — TASK-132 (this task's prerequisite)
- **Any new SignalR action** — Relics are never cast; the client→server action list is unchanged (`SIGNALR_PROTOCOL.md` §2)
- **Any new wire member or event type** — `RelicTriggered`'s shape is fixed (`ADR-018` item 10); `PowerChanged` already exists
- **Adding a member to `GAME_STATE.md`, `PetState`, or Redis** — `ADR-018` item 5 forbids a persistent Relic counter
- **Relic loadout validation, ownership, or the battle-start snapshot** — already implemented (TASK-027/038/080/081); do not re-implement
- **`Burning Curse`** — deferred; its Trigger conflict is unresolved (`RELIC_RULES.md` §6 note 3, TASK-131 D7)
- **New trigger types** — §3's closed list is unchanged (TASK-131 D8); a new trigger is a rule change (`GAME_RULES.md` §20)
- **Relic stacking behavior** — `RELIC_RULES.md` §2.4 item 6
- **The Card and Pet Skill resolution paths** — already implemented; do not refactor them
- **Client-side Relic presentation or UI** — a separate client task if required (`ADR-003`, `ARCHITECTURE.md` §2.2)
- **Any balance change to a Relic magnitude** — every value is `RELIC_RULES.md` §6/§8.5's
- **Modifying any completed or superseded task**; **reopening TASK-079/099/102**; **TASK-036's Discord decisions**

---

## Current State

```text
Implemented — do not re-implement:
  RelicLoadoutService / RelicLoadoutValidation   RELIC_RULES.md §2 loadout,
                                                 slot order, battle-start
                                                 snapshot into
                                                 PetState.EquippedRelics[]
  BattleStateService.ResolveSwapAsync            Match-3 → Passive → Damage →
                                                 Boss response → outcome
  PassiveTracker                                 the charging precedent for a
                                                 counter-based evaluation
  NextAttackCritModifiers                        ADR-017 consumption boundary
  BattleEventBuilder                             event emission
  CardCastExecutor                               the structured-effect
                                                 application precedent

NOT implemented — this task's work:
  GAME_RULES.md §17 step 11                      no Relic step exists anywhere
                                                 in ResolveSwapAsync
  Relic trigger evaluation                       absent
  Structured condition evaluation                absent
  Relic effect application                       absent
  RelicTriggered emission                        absent — SIGNALR_PROTOCOL.md
                                                 §3.2.23 item 4
  PowerChanged with source "relic"               absent
```

---

## Acceptance Criteria

- [ ] `GAME_RULES.md` §17 step 11 executes in the documented position, between resource generation and damage
- [ ] Each of §3's provisioned Triggers (`OnMatchCount`, `OnCombo`, `OnHpBelow`) is evaluated as §3 defines it
- [ ] Each of §8.1's three Condition forms is evaluated against the current resolution state, with its threshold read from the structured value — not parsed from prose
- [ ] **No persistent Relic counter and no new battle-state member is introduced**; `MatchCountAtLeast`/`ComboAtLeast` read the battle's existing values (`ADR-018` item 5)
- [ ] Eligible Relics resolve in equip-slot order from `PetState.EquippedRelics[]`, deterministically, with no re-sorting (`RELIC_RULES.md` §4)
- [ ] Each effect type applies per §8.3's allowed combination: `ATK` (Percentage/Battle), `Power` (Flat/Immediate), `Crit` (PercentagePoints/NextAttack), `CardCost` (Percentage/Battle)
- [ ] Assassin Eye applies **+10 percentage points Crit** for the next qualifying attack, composed through `COMBAT_RULES.md` §2 item 7's `EffectiveCrit` — not a second composition rule and not a direct stat overwrite
- [ ] Effect lifetime and trigger re-evaluation are independent: an `Immediate` or `NextAttack` effect ending does not disable the Relic's Trigger (`RELIC_RULES.md` §8.4)
- [ ] The chain queue is breadth-first per §4.3, and §5's once-per-root-event safeguard prevents a Relic self-loop from re-triggering within one root event
- [ ] `RelicTriggered` is emitted as `{ type, relicId }` for each Relic whose effect actually applies, with no added member and no effect summary
- [ ] `PowerChanged` carries `source: "relic"` when a Relic changes Power
- [ ] No client→server action, event type, or state member is added
- [ ] The result is persisted through the existing single write-back boundary; Redis round-trip is verified
- [ ] The stage is server-authoritative: zero Relic evaluation or effect computation occurs on the client (`AGENTS.md` §10, `ADR-001`)
- [ ] No RNG is introduced by a Relic effect (`AGENTS.md` §11); any randomness is `COMBAT_RULES.md` §2's existing seeded Crit roll
- [ ] `Burning Curse` remains unprovisioned and unevaluated; no placeholder is added
- [ ] `tasks/completed/` is unmodified; all completed tasks are byte-identical
- [ ] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/ (a Relic trigger/condition/effect resolution
                                     component in the Relic domain module —
                                     AGENTS.md §12: Relic logic belongs in the
                                     Relic module)
[x] src/backend/GameServer.Application/Battle/BattleStateService.cs
                                    (invoke step 11 at its documented position;
                                     no new persistence path)
[x] src/backend/GameServer.Domain/Match3/BattleEventBuilder.cs
                                    (RelicTriggered / PowerChanged emission)
[x] src/backend/GameServer.Application/DependencyInjection.cs (registration)
[ ] src/backend/GameServer.Api/ (no change expected — no new action or endpoint)
[ ] src/frontend/client/ (none — no client-side Relic logic; a presentation
                          task, if needed, is separate)
[x] tests/ (unit + integration + gameplay scenarios)
[x] docs/ (only if implementation reveals a gap — then STOP per AGENTS.md §7;
           RELIC_RULES.md §8.7's status updates when this lands)
[ ] tasks/completed/ (MUST remain unmodified)
```

---

## Implementation Notes

- **Step 11 has no existing hook.** `ResolveSwapAsync` in `BattleStateService.cs` runs the implemented stages; locate where resource generation ends and damage begins, and place the Relic stage exactly between them (`GAME_RULES.md` §17).
- **Follow `PassiveTracker`, do not fork it.** Passive charging is the closest existing pattern for evaluating a documented counter/threshold against resolution state. Reuse the approach; do not modify the Passive domain (`AGENTS.md` §12, §9).
- **`NextAttack` Crit must reuse `ADR-017`'s model.** `PetState.NextAttackCritModifiers[]` already exists with a source-specific identity and an established consumption boundary. Assassin Eye is a `NextAttack` Crit source and should enter that collection rather than adding a parallel modifier concept. `COMBAT_RULES.md` §2 item 7 owns the composition; do not restate it.
- **`Emergency Core` is the awkward one, deliberately.** Its Condition re-evaluates continuously while HP < 30% (`RELIC_RULES.md` §6 note 2) and its `CardCost` lifetime is `Battle` — while §8.4 keeps re-evaluation independent of lifetime. Implement exactly that; do not "simplify" it into a one-shot trigger, and do not add a cooldown to model it.
- **`CardCost` requires knowing where Card cost is read.** Locate the existing Card cost consumption site before implementing the modifier; if no documented site exists for a Relic-sourced cost modifier, that is a `docs/` gap — STOP per `AGENTS.md` §7 rather than inventing one.
- **Emit only when the effect actually applies.** `RELIC_RULES.md` §7 defines `RelicTriggered` as "emitted each time a Relic's Effect actually applies" — a Trigger that fires but whose Condition fails, or whose effect is a no-op, emits nothing.
- **Do not add an `effect summary` member.** `SIGNALR_PROTOCOL.md` §3.2.25 governs its omission; the resulting state reaches the client through the `BattleState` projection.
- **This is a large task.** If during planning it proves to exceed 7 skills or to cross multiple uncoupled boundaries (e.g. the client presentation layer is pulled in), STOP and decompose per `tasks/README.md` §13 rather than expanding it.
- Do not modify `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — trigger evaluation per §3's values; the three
                         Condition forms at, below, and above threshold; each
                         effectType's application; the lifetime model
[x] Integration tests  — step 11's position in the resolution order; equip-slot
                         ordering; the breadth-first chain queue; §5's
                         once-per-root-event safeguard; write-back and Redis
                         round-trip; RelicTriggered/PowerChanged emission order
                         and shape
[x] Gameplay scenarios — Given/When/Then derived from RELIC_RULES.md
                         §3/§4/§5/§8 and GAME_RULES.md §17 step 11
```

### Key Scenarios

```text
Given a battle with Berserker Core equipped (OnMatchCount, MatchCountAtLeast(3))
When the cumulative Match count reaches 3
Then the Relic's +5% ATK effect applies for the battle
And RelicTriggered is emitted for that Relic's identity
And the applied effect modifies ATK without overwriting the base stat

Given a battle with Assassin Eye equipped (OnCombo, ComboAtLeast(3))
When a Swap produces a Combo of 3 or more
Then a +10 percentage-point Crit modifier is created with NextAttack lifetime
And it composes through the documented EffectiveCrit rule
And it is consumed by the next qualifying owner attack (ADR-017 boundary)
And the Relic remains eligible to trigger again on a later qualifying Combo

Given a battle with Mana Crystal equipped (OnMatchCount, MatchCountAtLeast(4))
When the condition is met
Then +10 Power is applied to PetState.Power once
And no standing modification remains afterwards
And the Relic remains eligible to trigger again later

Given an active Pet whose HP is below 30% with Emergency Core equipped
When its Trigger is evaluated
Then its CardCost effect is in force while the condition holds
And it reverts when HP rises back above 30%
And no cooldown or charge state is introduced

Given two Relics eligible from the same Match
When step 11 executes
Then they resolve in equip-slot order, deterministically

Given a Relic whose effect would make itself eligible again from its own effect
When the chain queue is processed
Then it does not re-trigger within the same root event (RELIC_RULES.md §5)

Given a Relic is equipped
When the resolution reaches step 11
Then no client-provided computation is read for any trigger, condition, or
  effect value (AGENTS.md §10)
```

### Key Edge Cases

- A Condition threshold met exactly (`==`) versus exceeded — §8.1's forms are "at least N" / "below N", and the boundary behaviour is asserted for each
- A Relic equipped in slot 5 versus slot 1 — ordering is the array's, never a sort
- Two distinct instances of one `RelicDefinition` equipped together (`RELIC_RULES.md` §2.4 item 3) — each resolves as its own slot, and §5's safeguard is per equipped instance (§4 note)
- A Relic whose Trigger fires but whose Condition fails — no effect, no event
- An `Undetermined` effect — not resolvable; it must be treated as an open content gap and never substituted with a value

---

## Stop Conditions

- **If TASK-132 has not landed: STOP.** The structured storage must exist before a resolver can read a Relic's Condition or EffectDefinition.
- If any trigger, condition, effect, magnitude, or lifetime needed is not fully determined by `RELIC_RULES.md` §3–§8 and `ADR-018`: STOP per `AGENTS.md` §7 — invent nothing
- If the implementation appears to need a persistent Relic counter or a new battle-state member: STOP — `ADR-018` item 5 forbids it
- If the implementation appears to need a new wire member, event type, or client→server action: STOP — `ADR-018` item 10 and `SIGNALR_PROTOCOL.md` §2 fix the surfaces
- If a Relic effect appears to require a `docs/` rule that does not exist (e.g. where a Relic-sourced Card-cost modifier is consumed): STOP and report per `AGENTS.md` §7
- If `RELIC_RULES.md` §3's "exactly one primary Trigger" rule and a Relic's actual behaviour conflict: STOP per `AGENTS.md` §4
- If the task would require touching `CardDefinition`, the Card path, or the Passive domain to make Relics work: STOP and report — that is a domain-boundary violation (`AGENTS.md` §12)
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose (`tasks/README.md` §13)
- If the task is asked to provision `Burning Curse`: STOP — `RELIC_RULES.md` §6 note 3 / TASK-131 D7
- If the task is asked to modify a completed or superseded task: STOP — `TASK_LIFECYCLE.md` §3
- If the task is asked to reopen TASK-079, TASK-099, or TASK-102: STOP — terminal `SUPERSEDED`
- If any part would place Relic evaluation or effect computation on the client: STOP per `AGENTS.md` §10

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

Domain
- `src/backend/GameServer.Domain/Relics/RelicResolver.cs` — **new.** `GAME_RULES.md` §17 step 11: evaluates each equipped Relic's `Trigger` (§3's `OnMatchCount`/`OnCombo`/`OnHpBelow`) and structured `Condition` (§8.1) against the resolution state and applies its `EffectDefinition[]` (§8.2–§8.4) through the documented carriers, in equip-slot order (§4.2), with §5's once-per-root-event safeguard. Pure and deterministic; no content-name branch, no RNG, no counter.
- `src/backend/GameServer.Domain/Relics/EquippedRelicContent.cs` — **new.** One equip slot: the owned instance identity (§2.2) paired with the resolved definition (§8), so the resolver never confuses the two.
- `src/backend/GameServer.Domain/Relics/RelicEvents.cs` — **new.** The `RelicTriggered` payload (`{ relicId }`, an owned instance identity; no effect summary, no order index).
- `src/backend/GameServer.Domain/Relics/RelicResolutionResult.cs` — **new.** The resulting `PetState` plus the ordered `RelicTriggered`/`PowerChanged` reports.
- `src/backend/GameServer.Domain/Battle/PowerEvents.cs` — **new.** The `PowerChanged` payload and its documented `source` value set (`match`/`card`/`relic`).
- `src/backend/GameServer.Domain/Battle/EffectivePetATK.cs` — **new.** `COMBAT_RULES.md` §5.6.6's unified composition: the signed sum of `PetState.ATKModifiers[]` and the applicable `BuffDebuff` ATK contributions, applied to the base stat with one truncation, uncapped, never stored.
- `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` — `EffectiveAttack` now takes the Relic carrier and delegates to `EffectivePetATK.Compose`; §5.4.1's superseded sequential absolute-value path is gone and the shared ATK-modifier selector has one owner.
- `src/backend/GameServer.Domain/Cards/EffectiveCardCost.cs` — **new.** `CARD_RULES.md` §3.6: additive reductions capped at 100%, truncated toward zero, no minimum cost.
- `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs` — composes `EffectiveCardCost` once and uses that one value for validation, deduction, and the `CardCast` report (§3.6 item 7).
- `src/backend/GameServer.Domain/Match3/BattleEvent.cs` — `RelicTriggered` and `PowerChanged` added to the closed event model with their payloads and factories.
- `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs` — added the signed-change overload of the single Power clamp site (`GAME_RULES.md` §12, §17 step 13); the generation path delegates to it unchanged.

Application
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — invokes step 11 between Charge Passive and the Damage Pipeline, reads the battle's per-battle equipped-Relic content (attached at creation, so PostgreSQL is never queried on the Swap path — `TDD.md` §4 item 3), appends the stage's events, and composes the Pet ATK through the unified rule.
- `src/backend/GameServer.Application/Battle/BattleStartService.cs` — resolves each equipped instance's shared definition through `IRelicRepository` + `IRelicDefinitionLookup` in equip-slot order and hands the set to battle creation.
- `src/backend/GameServer.Application/DependencyInjection.cs` — comment updated (the battle-start boundary now resolves the Relic content).

Api
- `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs` — `relicId`, `delta`, and `power` slots plus the `RelicTriggered`/`PowerChanged` factories and projection cases (§3.2.23, §3.2.24). No new hub method or message.

Docs (status synchronization only — no rule, value, or contract changed)
- `docs/01-game-design/RELIC_RULES.md` — §8.7's five `NOT IMPLEMENTED` lines → `IMPLEMENTED`; version → 1.11.
- `docs/02-technical/SIGNALR_PROTOCOL.md` — §3.2.2 item 5, §3.2.23 item 4, §3.2.24 item 5 corrected; version → 2.12.
- `docs/02-technical/GAME_EVENTS.md` — §2 `RelicTriggered`/`PowerChanged` emission items corrected; version → 2.10.
- `docs/02-technical/DATABASE.md` — §1 Relic note item 5's "step 11 remains unimplemented" clause corrected; version → 1.29.

Tests
- `tests/backend/GameServer.Domain.Tests/RelicResolverTests.cs` — **new.** 31 tests: each condition form below/at/above threshold, each provisioned Trigger, each `effectType` on its carrier, same-source refresh/replacement, coexistence, source-specific reversion, `NextAttack` non-removal, re-trigger after an ended lifetime, equip-slot order, the once-per-root-event safeguard, unresolved content, and an `Undetermined` effect.
- `tests/backend/GameServer.Domain.Tests/Battle/EffectivePetATKTests.cs` — **new.** 18 tests: §5.6.6's four worked examples, order independence, single truncation, the no-cap rule, selection by `Type`/`TargetStat`, and base-stat independence.
- `tests/backend/GameServer.Domain.Tests/Battle/EffectiveCardCostTests.cs` — **new.** 9 tests: §3.6's worked values, the 100% cap, additive composition, no minimum cost, and that nothing is mutated.
- `tests/backend/GameServer.Domain.Tests/RelicProvisionedDefinitions.cs` — **new.** §8.5's four provisioned rows as Domain values, transcribed.
- `tests/backend/GameServer.Application.Tests/RelicStageResolutionTests.cs` — **new.** 8 tests: step 11's position between the Pet Passive stage and the damage instance, the carriers in the committed state, `RelicTriggered` in slot order, `PowerChanged`, an unmet condition emitting nothing, a battle with no content, an unresolved slot, the one CAS write-back, and the retry path.
- `tests/backend/GameServer.Application.Tests/Battle/CardCostRelicIntegrationTests.cs` — **new.** 2 tests: a Relic-applied `CardCost` modifier is what validation, deduction, and reporting read, and the control case against the authored cost.
- `tests/backend/GameServer.Api.Tests/RelicWireProjectionTests.cs` — **new.** 10 tests: `RelicTriggered` = `{ type, relicId }` only, the omitted members, `PowerChanged`'s three source names, zero/negative deltas, and batch order/arity.
- `tests/backend/GameServer.Infrastructure.Tests/RedisBattleStateRepositoryTests.cs` — 1 test added: `RelicResolver` output serializes into the existing `battle:{battleId}:state` record under the `Sequence` compare-and-set, reads back element-for-element, and adds no key.
- `tests/backend/GameServer.Infrastructure.Tests/RelicProvisionedContent.cs` — `Definition(id)` helper added.
- `tests/backend/GameServer.Domain.Tests/EffectiveAttackTests.cs`, `EffectiveBossAttackTests.cs` — signature updated for the unified composition; the one multi-instance expectation corrected from the superseded sequential 35 to the canonical single-truncation 20 (`COMBAT_RULES.md` §5.6.6 items 3–4, TASK-138 D3/D5).
- `tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs` — the closed event-name and payload-member sets updated with the two documented events.
- `tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs` — Relic definition lookup double added; constructor and field-set assertions updated.
- `tests/backend/GameServer.Api.Tests/BattleResultSmokeTest.cs` — the scenario Relic row is re-asserted to the structured shape each run (an earlier run's row predates TASK-132's columns).

### Validation Results

```text
GameServer.Domain.Tests           PASS — 1463 tests (79 new across all suites)
GameServer.Application.Tests      PASS —  475 tests
GameServer.Api.Tests              PASS —  275 tests
GameServer.Infrastructure.Tests   PASS —  363 tests (live PostgreSQL + Redis)
src/frontend/client (vitest)      PASS —  477 tests, 18 files (client untouched)
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — no client file changed; the client renders the `BattleState` projection and ignores the two event names it does not parse
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new Relic, Trigger, Condition, effect, or system
- [x] Confirmed `RelicTriggered` = `{ type, relicId }` with no added member
- [x] Confirmed no new battle-state member, wire member, or client action
- [x] Confirmed `tasks/completed/` unmodified by this task
- [x] Confirmed no PostgreSQL schema change, no new Redis key, no new SignalR method or message

### Known Deviations Reported (not silently resolved)

- `GAME_RULES.md` §17 lists steps 12–14 (Generate Resources, Update Power, Resolve Player Effects) *after* step 11, and the implementation applies them *before* step 10/11 because they belong to the Domain Swap executor that owns the single write-back. The Relic stage is placed where the pipeline can place it and where this task's acceptance criteria fix it (after Charge Passive, before the Damage Pipeline). The result is identical for every provisioned effect — the Power grant goes through the same clamped write site, and `clamp(clamp(P+M)+G) = clamp(P+M+G)` for non-negative terms — with one exception: `HpPercentageBelow` reads the Pet's HP after this Swap's healing rather than before it. Reported for a future sequencing task; not worked around.
- `ADR-018`'s Consequences line "the Relic stage … remains UNIMPLEMENTED" is now a point-in-time statement superseded by `RELIC_RULES.md` §8.7's synchronized status. ADR-018 was left byte-identical per this task's instruction; a reviewer may wish to append a completion amendment.
