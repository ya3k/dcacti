# TASK-102 — Implement the Card Cast Server Path (`CardCast` / `PetSkillCast`)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK AUTHORS NO CONTRACT. Every contract it implements already
  exists: CARD_RULES.md §2–§4 (Costs/Effects/validation), COMBAT_RULES.md
  §4 (Heal/Shield), GAME_RULES.md §11 (Card rules), SIGNALR_PROTOCOL.md
  §2/§5 (method + acknowledgement), GAME_EVENTS.md §2 (CardCast /
  PetSkillCast). The task implements them; it changes none of them.

  PROVENANCE: identified by the post-TASK-101 next-implementation-task
  discovery pass. ROADMAP.md Phase 1 ("Core Loop Vertical Slice") names
  "3 Basic Cards" and "One Pet fully implemented (Element, Passive,
  Signature Skill)" as in-scope, and BattleHub.cs L389–391 records that
  CardCast/PetSkillCast are "intentionally NOT implemented". This is the
  largest remaining Phase 1 gap on the documented critical path.
-->

TASK-102 was authored before TASK-105 changed the Shield rule. The stale
Shield wording it carried has been corrected by this readiness review — see
"Readiness Review (TASK-102)" in the Completion Evidence.

---

## Metadata

```text
Task ID:           TASK-102
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented
                   mechanic, capability, or system that already has a home
                   in docs/ but has not yet been built." The Card system,
                   its casting rules, its Basic Card effects, and its
                   SignalR method are all fully documented; none is built.)
Status:            SUPERSEDED (by TASK-107, TASK-115, TASK-120, TASK-122.
                   Task was never directly executed; its complete scope was decomposed
                   and fully implemented/verified across downstream tasks.
                   Reconciled and moved to tasks/completed/ per tasks/TASK_LIFECYCLE.md §3
                   and TASK-130 policy.)
Risk:              HIGH (TASK_TYPES.md §4 — FEATURE baseline MEDIUM, "can be
                   HIGH if it touches combat / battle state / auth". This
                   touches the Damage Pipeline, PetState, the resolution
                   write-back, and the SignalR action surface.)
Priority:          HIGH (ROADMAP.md Phase 1 critical path — the documented
                   vertical slice names 3 Basic Cards and one Pet's
                   Signature Skill; neither is castable today.)
Primary Agent:     backend (GameServer.Application/ + GameServer.Api/ — the
                   hub method, the cast use case, and the orchestration)
Supporting Agents: gameplay (Domain Card-effect resolution and the Shield
                   absorption step in the Damage Pipeline),
                   realtime (SignalR method + ReceiveEvents projection),
                   testing, review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   gameplay/gameplay-behavior-derivation,
                   backend/api-contract-validation,
                   realtime/realtime-protocol-validation,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (6 skills — Complex budget, tasks/README.md §12)
Dependencies:      TASK-105 (DONE — changed the Shield rule in its canonical
                     owner, `COMBAT_RULES.md` §4, from additive stacking to
                     refresh and authored the depletion/overflow rule. Its
                     resolved contract is what this task's Shield absorption,
                     refresh, and depletion criteria are written against;
                     immutable),
                   TASK-028 (DONE — Card ownership + loadout snapshot into
                     PetState.EquippedCards[]),
                   TASK-030 (DONE — POST /api/battle/start; the battle the
                     cast targets exists),
                   TASK-021 (DONE — Damage Pipeline the Pet Skill Cards use),
                   TASK-018 (DONE — ResourceGenerator.ApplyPower /
                     ApplyHeal, the Power and Heal mechanics the Basic
                     Cards reuse),
                   TASK-095/TASK-096 (DONE — StatusEffect domain state +
                     step-19a lifecycle + serialization; a Pet Skill Card's
                     Burn/Shield application writes this collection),
                   TASK-085 (DONE — card-heal / card-shield /
                     card-power-charge / card-inferno / card-tidal-barrier /
                     card-iron-fang definition rows are provisioned)
Blocks:            Nothing directly. It completes the Phase 1 action
                   surface (Swap + CardCast + PetSkillCast) and unblocks the
                   client-side Card-cast UI as a follow-up task. It does NOT
                   unblock TASK-036, TASK-079, or TASK-099.
Estimate:          Complex (crosses Domain → Application → Api; one new
                   Domain effect resolver, one Damage Pipeline step, one
                   Application use case, one hub method, both event
                   projections, and their tests)
```

**Type classification note.** `FEATURE`, not `GAMEPLAY-CHANGE`. Per
`TASK_TYPES.md` §2, a `GAMEPLAY-CHANGE` is used when "a gameplay mechanic needs
to behave differently than the documentation currently says, OR a new
undocumented mechanic is being authorized." Neither applies: `CARD_RULES.md`
§2–§4, `COMBAT_RULES.md` §4, `GAME_RULES.md` §11, `GAME_EVENTS.md` §2, and
`SIGNALR_PROTOCOL.md` §2/§5 already define the entire mechanic. This task builds
what is documented and changes no rule.

**No ADR is required.** This implements an already-decided boundary rather than
changing one. `ADR-001` (server authority) governs and is satisfied by
construction — the server validates and resolves every cast. `ADR-011` item 4
(the loadout is selected at `POST /api/battle/start` for the one active Pet and
snapshotted into `PetState.EquippedCards[]`) already owns where the castable
Cards come from, and this task reads that snapshot rather than introducing a
second source. No layer, storage, realtime, or authoritative-state model
changes.

**This task does not implement Relics.** `ROADMAP.md` Phase 1 states "No Relics
yet"; the Relic trigger/effect system is Phase 2. `CARD_RULES.md` §3 item 4's
downstream `RelicTriggered` step (`RELIC_RULES.md` §3 `OnCardCast`) is therefore
**out of scope** and must be reported rather than implemented.

---

## Objective

Implement the documented Card cast path end-to-end on the server: the
`CardCast` and `PetSkillCast` `BattleHub` methods (`SIGNALR_PROTOCOL.md` §2)
that validate a cast against `CARD_RULES.md` §3 item 2 (the Card is in the
active Pet's battle loadout; the active Pet has sufficient Power; no additional
MVP Basic Card preconditions), deduct the Cost, apply the Card's documented
effect (`CARD_RULES.md` §2 Basic Cards — Heal, Shield, Power Charge; §4.1 Pet
Skill Cards — Inferno, Tidal Barrier, Iron Fang, each through the Damage
Pipeline where it deals damage), commit one write-back under the existing
`Sequence` compare-and-set, and push the resulting ordered Battle Events — while
adding the documented Shield absorption step (`COMBAT_RULES.md` §4 item 2) that
`DamagePipeline` currently omits, and changing no rule, contract, or schema.

---

## Authoritative References

- `docs/01-game-design/CARD_RULES.md` §1 — Card categories; **§2** the MVP
  Basic Cards with their exact Costs and Effects (Heal 20 Power, Shield
  20 Power, Power Charge 0 Power); **§3** the casting and validation rules
  (item 2 is the rejection contract, item 3 the rejection semantics, item 4
  the ordered resolution, item 5 the no-Turn/no-Combo rule); **§4 / §4.1** the
  Pet Skill Card and the three content-defined Signature Skills; **§6** the
  `CardCast` / `PetSkillCast` events. **The primary owner of this task's
  behaviour.**
- `docs/01-game-design/COMBAT_RULES.md` §4 — **the Shield absorption contract
  this task must add to the Damage Pipeline**: item 1 heal clamps to Max HP and
  discards overheal; item 2 Shield is "an absorption pool that reduces incoming
  damage before HP is affected" — the pool is consumed before HP, and because
  at most one Shield is active per entity there is no multi-pool ordering;
  item 3 **Shield application refreshes; Shields do not stack** — applying a
  Shield while one is active refreshes that existing Shield, at most one Shield
  is active per entity (the Status Effect identity `"Shield"`), and no additive
  accumulation occurs; items 4–5 depletion at exactly 0 removes the Shield in
  the same resolution, with overflow reducing HP by exactly the remainder;
  item 6 Heal and Shield are **not** subject to the Damage Pipeline. §3 / §3.4
  — the Damage Pipeline the Pet Skill Cards run; §5.1 / §5.3 — Status Effects
  and their duration-consumption point (Burn/Shield are applied here, consumed
  at §17 step 19a by existing code).
- `docs/01-game-design/GAME_RULES.md` §11 — Card rules (Cards are active
  actions, consume/generate Power, cannot bypass server validation, may trigger
  Relics); §12 — Power range 0–100; §17 step 14 "Resolve Player Effects" and
  step 18 — the resolution order a cast participates in; §18 — server
  authority; §16 — the canonical event list.
- `docs/01-game-design/PET_RULES.md` §8 — the provisioned/deferred Pet row set:
  only Xích Lang, Bạch Hổ, and Huyền Quy carry a content-defined Signature
  Skill; **Thanh Xà and Sơn Hùng Signature Skills are TBD content** and must
  not be invented.
- `docs/01-game-design/ELEMENT_RULES.md` §5 — the Element Modifier the
  damaging Pet Skill Cards pass through (Inferno is Hỏa).
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 — the `CardCast(battleId, cardId,
  clientSequence)` and `PetSkillCast(battleId, clientSequence)` method
  contracts; §2 item 2 (`CARD_RULES.md` §3 owns their validation); §2 item 3
  (a rejection returns to the caller and emits no events); §5 — the
  `{ accepted, reason }` acknowledgement and its per-action reason ownership;
  §3 / §3.2 — the `ReceiveEvents` batch and wire schema; §4 — `BattleStateUpdated`;
  §6 — sequencing; §8 — what is explicitly not here.
- `docs/02-technical/GAME_EVENTS.md` §1.1 — the ordered event list; §2 — the
  `CardCast` / `PetSkillCast` event meanings; §3 — ordering.
- `docs/02-technical/GAME_STATE.md` §2.3 — `PetState`, which carries `Power`,
  `HP`/`MaxHP`, `EquippedCards[]`, and `ActiveStatusEffects[]`; §2.3.1 — the
  Status Effect instance schema a Shield application writes; §5.1 — the single
  post-resolution write-back; §5.1.1 — the step-19a lifecycle.
- `docs/02-technical/REDIS_STATE.md` §4 items 2–3, 5, 7 — the `Sequence`
  compare-and-set write-back semantics and "a rejected action writes nothing".
- `docs/02-technical/ARCHITECTURE.md` §2.1 — the layer boundary (Domain owns
  rules, Application sequences, Api is a thin transport); §2.2.1 — the
  client → server action boundary.
- `docs/02-technical/TDD.md` §4 item 3 — PostgreSQL is never read on the hot
  resolution path; §2.1 — the client is presentation-only.
- `docs/02-technical/DATABASE.md` §1 — `CardDefinition` (`Cost`, `Effect`,
  `Category`, `LoadoutCopyLimit`); §2 — `PlayerUnlockedCard` ownership.
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; every cast is validated and resolved server-side.
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 4 — the
  loadout is selected at `POST /api/battle/start` for the one active Pet and
  snapshotted into `PetState.EquippedCards[]`; the Pet is the combat character
  the Card effects target.
- `docs/00-overview/ROADMAP.md` — Phase 1 names "3 Basic Cards" and "One Pet
  fully implemented (… Signature Skill)"; it states "No Relics yet".
- `docs/00-overview/MVP_SCOPE.md` §1 — Cards (3 Basic + 5 Pet Skill) and Combat
  are IN; §2 — nothing here reaches an OUT item.
- `tasks/completed/TASK-030-post-api-battle-start-endpoint.md` — the battle the
  cast targets is created there; read-only.
- `tasks/completed/TASK-028-card-ownership-and-loadout-snapshot.md` — how
  `EquippedCards[]` is populated and validated; read-only.
- `tasks/completed/TASK-021-implement-damage-pipeline.md` — the pipeline the
  Pet Skill Cards call; read-only.
- `tasks/completed/TASK-095-implement-statuseffect-domain-state-and-step-19a-lifecycle.md`
  and `TASK-096-serialize-statuseffects-round-trip.md` — the StatusEffect state
  and lifecycle a Shield/Burn application uses; read-only.

**ADR check:** no ADR is required (see the Metadata note).

---

## Current State

The Card system is **documented and provisioned but not executable**. The
definitions, the ownership, and the loadout snapshot all exist; nothing casts
them.

### What exists (verified at discovery)

```text
docs/01-game-design/CARD_RULES.md     §1–§6 COMPLETE — categories, the three
                                       Basic Cards (Cost + Effect), the casting
                                       and validation rules, the Pet Skill
                                       Cards, and the two events.
docs/01-game-design/COMBAT_RULES.md   §4 COMPLETE — Heal clamp, Shield
                                       absorption, Shield refresh semantics.

src/backend/GameServer.Domain/Cards/
  CardCategory.cs                      EXISTS
  CardDefinition.cs                    EXISTS (Cost / Effect / Category)
  EquippedCardIdentity.cs               EXISTS
  PlayerUnlockedCard.cs                 EXISTS

src/backend/GameServer.Application/Cards/
  CardLoadoutService.cs                 EXISTS — validates the submitted loadout
  CardLoadoutValidation.cs              EXISTS
  CardLoadoutRejectionReason.cs         EXISTS
  ICardRepository.cs                    EXISTS

src/backend/GameServer.Domain/Battle/PetState.cs
  Power, HP, MaxHP, EquippedCards[], ActiveStatusEffects[]   ALL EXIST
  (the state a cast reads and writes is already carried)

src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
  ApplyPower(...)  EXISTS — the Power mechanic
  ApplyHeal(...)   EXISTS — HP restore clamped to MaxHP (COMBAT_RULES.md §4 item 1)

src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
  apply / refresh / ConsumeAtStep19a   EXISTS (TASK-095)

src/backend/GameServer.Infrastructure/Postgres/Migrations/
  20260929152651_ProvisionPetCardRelicContentDefinitions.cs
  PROVISIONED — card-heal, card-shield, card-power-charge,
                card-inferno, card-tidal-barrier, card-iron-fang
```

### What is missing (verified at discovery)

```text
src/backend/GameServer.Api/Hubs/BattleHub.cs
  L389–391 states outright:
    "CardCast, PetSkillCast (§2) and GetBattleState (§7) are client → server
     methods that are intentionally NOT implemented"
  Hub methods present: JoinBattle, Swap, Ping. NO CardCast, NO PetSkillCast.

src/backend/GameServer.Domain/
  NO Card effect resolver — nothing reads CardDefinition.Effect and applies it.

src/backend/GameServer.Domain/Combat/DamagePipeline.cs
  L61 states: "it applies no Shield (COMBAT_RULES.md §4 item 2 is not
  implemented)". The Shield absorption pool is therefore NOT subtracted from
  incoming damage anywhere.

src/backend/GameServer.Domain/Match3/BattleEvent.cs
  L45–47 states: "No RelicTriggered, CardCast, PetSkillCast, or BossSkillCast
  exists here — those are other owning stages." No CardCast / PetSkillCast
  BattleEventType member exists.

src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
  The discriminator switch (L656–667) covers 12 event types and has NO arm for
  CardCast or PetSkillCast.
```

### The consequence

A player can Swap but cannot cast a Card. `ROADMAP.md` Phase 1's "3 Basic
Cards" and "One Pet fully implemented (Element, Passive, Signature Skill)" are
therefore not reachable, because a Signature Skill *is* a Pet Skill Card
(`CARD_RULES.md` §4 item 1, `PET_RULES.md` §2).

---

## Scope

### In Scope

1. **The two hub methods** (`SIGNALR_PROTOCOL.md` §2): `CardCast(battleId,
   cardId, clientSequence)` and `PetSkillCast(battleId, clientSequence)`,
   mirroring the existing `Swap` method's structure (validate → delegate →
   commit → push state → push events → return the §5 acknowledgement) and
   returning `{ accepted, reason }` per §5. `PetSkillCast` resolves the active
   Pet's Signature Skill Card (`CARD_RULES.md` §4 item 1–2).
2. **A Domain Card-effect resolver** that reads a `CardDefinition` and applies
   its documented effect to the state — Heal, Shield, Power Charge
   (`CARD_RULES.md` §2), and the three content-defined Pet Skill Cards
   (§4.1: Inferno, Tidal Barrier, Iron Fang). Damage-dealing Pet Skill Cards go
   through the existing `DamagePipeline.Calculate`; Heal and Power reuse
   `ResourceGenerator.ApplyHeal` / `ApplyPower`; Shield is applied as a
   `StatusEffect` instance of the documented trigger-based type
   (`GAME_STATE.md` §2.3.1 item 3, `StatusEffectType.Shield`).
3. **The Shield absorption step** in `DamagePipeline` (`COMBAT_RULES.md` §4
   items 2–5): incoming Final Damage is reduced by the target's Shield
   absorption pool before HP is affected; a Shield reapplication **refreshes**
   the existing Shield instance, the new application's magnitude **replacing**
   the previous magnitude; there is **no additive stacking**, and **one Shield
   instance per entity** exists (the Status Effect identity `"Shield"`), so two
   different Shield sources refresh one another rather than forming a second
   pool. This is the one documented rule the pipeline currently omits, and the
   Shield Basic Card is meaningless without it. The pipeline still owns the
   absorption step.
4. **The cast validation contract** (`CARD_RULES.md` §3 item 2): the Card is in
   the active Pet's `PetState.EquippedCards[]` snapshot for this battle; the
   active Pet's `Power ≥ CardDefinition.Cost`; no additional MVP Basic Card
   preconditions (item 2's third bullet). A rejected cast **changes nothing**
   (item 3): no Power spent, no effect applied, no event emitted beyond the
   rejection response.
5. **The ordered resolution** (`CARD_RULES.md` §3 item 4): deduct Cost from
   Power → apply Effect → emit `CardCast` (and `PetSkillCast` when the cast
   Card is the active Pet's Signature Skill) — committed as **one** write-back
   under the existing `Sequence` compare-and-set (`REDIS_STATE.md` §4 items
   2–3, 5).
6. **The two Battle Events** (`GAME_EVENTS.md` §2, `CARD_RULES.md` §6):
   `CardCast` for every successful cast, `PetSkillCast` additionally when the
   cast Card is the Signature Skill. Both need a
   `BattleEventType` member and a `BattleEventWireProjection` arm producing the
   wire shape the protocol's existing conventions require (§3.2.1–§3.2.5:
   discriminator, casing, enum representation, optionality). If the wire shape
   proves genuinely unspecified, STOP per the Stop Conditions — do not invent
   a payload.
7. **No Turn and no Combo** (`CARD_RULES.md` §3 item 5): a cast consumes no
   Turn and does not interact with Combo. It therefore does **not** increment
   `Turn`, does **not** run the §17 Swap pipeline, and does **not** run the
   Boss Response steps — the resolution is the cast's own.
8. **Tests**: Domain unit tests for the effect resolver and the Shield
   absorption maths; Application tests for validation, rejection, Power
   deduction, and the single write-back; Api/hub tests for the §5
   acknowledgement and the two event projections. Rejected-cast and
   shield-absorption edge cases are the priority.

### Out of Scope

- **Relics.** No `RelicTriggered` event, no `OnCardCast` trigger, no Relic
  effect engine. `ROADMAP.md` Phase 1 states "No Relics yet" and
  `CARD_RULES.md` §3 item 4's downstream Relic step is Phase 2. Report it; do
  not implement it.
- **Any rule, formula, cost, magnitude, or balance value that is not already
  written down.** In particular the Thanh Xà and Sơn Hùng Signature Skills are
  **TBD content** (`PET_RULES.md` §8, `CARD_RULES.md` §4.1) — no placeholder,
  no invented Skill Card, no invented value.
- **The "Burning Curse" Relic** — deferred under an unresolved `RELIC_RULES.md`
  §3-vs-note-1 tension; not this task's to resolve.
- **`GetBattleState`** — `SIGNALR_PROTOCOL.md` §7 reconnect/resync is
  **Phase 3** (`ROADMAP.md`); not implemented here.
- **StatusEffects as a wire/state-push member** — `SIGNALR_PROTOCOL.md` §4.2
  item 2 explicitly excludes HP/ATK/DEF/Crit/Power/StatusEffects/EquippedRelics/
  EquippedCards from the `BattleStateUpdated` push. This task adds **no** new
  state-push field and no protocol decision about delivering StatusEffects.
- **Any new SignalR method** beyond the two documented in §2.
  `SIGNALR_PROTOCOL.md` §8 item 7 is explicit: "The gameplay methods are
  exactly the three of §2."
- **Any new Redis key, field, TTL, or record**; any new API endpoint; any
  PostgreSQL schema, entity, column, constraint, or migration. The definition
  rows already exist (TASK-085) and are read, not written.
- **Any client/Phaser/React work** — the cast UI is a separate follow-up task.
- **Any other gameplay mechanic** — no new Pet, Boss, Relic, Element, Status
  Effect, resource, or progression system; no change to Swap, Match-3,
  Cascade, Combo, Passive, Boss Response, or the Damage Pipeline's existing
  formula steps.
- **`docs/`** — no authoritative document is modified. If implementation
  reveals a doc is wrong, STOP per `AGENTS.md` §4/§17.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [ ] `BattleHub` exposes `CardCast(battleId, cardId, clientSequence)` and
      `PetSkillCast(battleId, clientSequence)` with exactly the parameters
      `SIGNALR_PROTOCOL.md` §2 documents, returning the §5 `{ accepted, reason }`
      acknowledgement shape.
- [ ] `BattleHub.cs`'s L389–391 comment no longer states that `CardCast` and
      `PetSkillCast` are not implemented — it is corrected to describe the
      implemented state, while `GetBattleState` (§7, Phase 3) **remains**
      documented as not implemented.
- [ ] A cast is rejected when the Card is not in the active Pet's
      `PetState.EquippedCards[]` snapshot for this battle (`CARD_RULES.md` §3
      item 2, first bullet).
- [ ] A cast is rejected when the active Pet's `Power < CardDefinition.Cost`
      (`CARD_RULES.md` §3 item 2, second bullet), and the rejection reason is
      the documented `INSUFFICIENT_POWER` code (`SIGNALR_PROTOCOL.md` §5).
- [ ] **A rejected cast changes nothing**: no Power is spent, no effect is
      applied, no `StatusEffect` is written, no event is emitted, and no write
      reaches the store (`CARD_RULES.md` §3 item 3, `REDIS_STATE.md` §4 item 7).
      Proven by asserting the persisted state is byte-identical before and
      after.
- [ ] A successful cast deducts exactly the Card's documented Cost from
      `PetState.Power` (`CARD_RULES.md` §2; Power Charge costs 0 and therefore
      deducts 0).
- [ ] `Heal` restores the active Pet's HP by the documented amount, clamped to
      `MaxHP`, with overheal discarded (`CARD_RULES.md` §2, `COMBAT_RULES.md`
      §4 item 1).
- [ ] `Shield` grants the active Pet a Shield absorption pool of the documented
      amount, represented as a `StatusEffect` instance of the documented
      trigger-based type (`GAME_STATE.md` §2.3.1 item 3, `COMBAT_RULES.md`
      §4 item 2).
- [ ] `Power Charge` adds the documented amount of Power, and Power never
      exceeds the documented maximum (`CARD_RULES.md` §2 item 3,
      `GAME_RULES.md` §12).
- [ ] **Shield absorption works**: incoming Final Damage is reduced by the
      target's Shield pool before HP is affected (`COMBAT_RULES.md` §4 item 2,
      item 5). Because at most one Shield is active per entity, there is no
      multi-pool ordering to resolve and no "first-in" tie-break exists
      (`COMBAT_RULES.md` §4 item 2). Proven by a test in which damage that would
      otherwise reduce HP leaves HP unchanged while the pool absorbs it, and a
      test in which damage exceeding the pool reduces HP only by the remainder.
- [ ] **Shield depletion at exactly 0** (`COMBAT_RULES.md` §4 item 4): damage
      exactly equal to the pool leaves HP **unchanged**, the pool reaches 0, and
      the Shield is **removed in that same resolution** — a committed Shield
      value of 0 is never observable as an active Shield. Damage greater than
      the pool removes the Shield in the same resolution and reduces HP by
      **exactly the remainder**, with no double-counting (`COMBAT_RULES.md` §4
      item 5). Each case is proven by a test.
- [ ] `ShieldDepleted` is treated as the Shield instance's **trigger-based
      expiry condition**, evaluated during the damage resolution that depleted
      it — **not** as a Battle Event. No event of that name is emitted, and no
      `BattleEventType` member, payload, or wire member is added for it
      (`COMBAT_RULES.md` §4 item 4; `GAME_STATE.md` §2.3.1 item 5 — "a condition
      label, not a rule").
- [ ] **Shield reapplications refresh; Shields do not stack**
      (`COMBAT_RULES.md` §4 item 3) — proven by tests asserting each of:
      (a) at most **one** Shield instance is active per entity, identified by
      the Status Effect identity `"Shield"` (`GAME_STATE.md` §2.3.1 items 1 and
      6); (b) a reapplication **refreshes** the existing Shield and creates no
      second instance; (c) the refreshed pool is **set to the magnitude of the
      new application**, replacing the current magnitude — it is **not** summed
      with the existing value; (d) no additive accumulation occurs, including
      when the two applications come from different Shield sources.
- [ ] A fully-absorbed damage instance still reports its documented
      `DamageCalculated` / `DamageDealt` / `DamageTaken` events, with the HP
      effect reflecting the absorption — the existing event contract is not
      silently changed by the new step.
- [ ] The three content-defined Pet Skill Cards resolve: **Inferno** (damage
      through the Damage Pipeline with its Fire Element, and Burn applied),
      **Tidal Barrier** (Heal and Shield), **Iron Fang** (damage; increased
      Crit chance). Each is covered by a test.
- [ ] A cast of a Pet Skill Card emits **both** `CardCast` and `PetSkillCast`;
      a Basic Card cast emits **only** `CardCast` (`CARD_RULES.md` §6).
- [ ] `PetSkillCast` resolves the active Pet's Signature Skill Card
      (`CARD_RULES.md` §4 items 1–2) and rejects when that Card is not
      available.
- [ ] A cast consumes **no** Turn and does **not** interact with Combo
      (`CARD_RULES.md` §3 item 5): `BattleState.Turn`, `MatchCount`, and
      `Combo` are unchanged by a cast, proven by assertion.
- [ ] A cast does **not** run the §17 Swap pipeline and does **not** trigger a
      Boss Response; no Swap-only event (`MatchCreated`, `GemMatched`,
      `CascadeCreated`, `ComboChanged`) appears in a cast's event batch.
- [ ] A **single** write-back per accepted cast, under the `Sequence`
      compare-and-set (`REDIS_STATE.md` §4 items 2–3, 5), on the same
      `IBattleStateRepository.TryUpdateAsync` pattern `ExecuteSwapAsync` uses.
- [ ] Both new events are projected onto the §3.2 wire schema — a
      `BattleEventType` member and a `BattleEventWireProjection` arm exist, the
      payload matches the protocol's discriminator/casing/optionality
      conventions, and the round trip is asserted by a test.
- [ ] An accepted cast pushes `BattleStateUpdated` **before** its
      `ReceiveEvents` batch (`SIGNALR_PROTOCOL.md` §3.1 item 1, §6 item 4).
- [ ] Both methods return the acknowledgement **to the caller only**, never to
      the group (`SIGNALR_PROTOCOL.md` §5 item 4).
- [ ] An unknown `battleId` resolves nothing and returns a rejection rather
      than creating or inventing a battle — consistent with the existing
      `Swap` path's `BATTLE_NOT_FOUND` behaviour.
- [ ] **No new SignalR method** beyond §2's three exists; the hub's method set
      is exactly `JoinBattle`, `Swap`, `CardCast`, `PetSkillCast`, `Ping`.
- [ ] **No new Redis key, field, TTL, or record**; no new API endpoint; no
      PostgreSQL schema, entity, column, constraint, or migration; the
      definition rows are read, never written.
- [ ] **No new state-push member**: `BattleStateUpdated`'s payload gains no
      field, and StatusEffects remain undelivered there
      (`SIGNALR_PROTOCOL.md` §4.2 item 2).
- [ ] **No client-authoritative gameplay logic** is introduced anywhere
      (`AGENTS.md` §10, `ADR-001`): every cost, effect, and damage value is
      computed server-side.
- [ ] **No Turn increment, no Boss Response, no Relic trigger** is added by
      this task, and a test asserts each absence.
- [ ] **Zero files under `docs/` are modified**; **zero ADRs** created or
      edited.
- [ ] **No Thanh Xà or Sơn Hùng Signature Skill content is invented**
      (`PET_RULES.md` §8): their rows remain deferred and no placeholder value
      appears in source, tests, or data.
- [ ] Build is green with zero new errors and zero new warnings.
- [ ] All existing tests remain green **unmodified**, and the new tests pass.
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

### Explicit Constraints

```text
No gameplay outside documented scope.
No speculative architecture.
No undocumented API.
No undocumented SignalR method.
No undocumented Redis behavior.
No client-authoritative state.
No speculative PostgreSQL BattleState persistence.
No Relics.
No Turn consumption, no Combo interaction, no Boss Response on a cast.
No invented content values.
No docs/ changes.
No ADR.
```

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Cards/ (NEW card-effect resolver — the
      CardDefinition.Effect → state transformation, plus Shield application)
[x] src/backend/GameServer.Domain/Combat/DamagePipeline.cs (the Shield
      absorption step only — COMBAT_RULES.md §4 item 2–3; the existing formula
      steps are untouched)
[x] src/backend/GameServer.Domain/Match3/BattleEvent.cs (two new
      BattleEventType members + factories)
[x] src/backend/GameServer.Application/Battle/ (a Card-cast use case /
      resolution method beside ExecuteSwapAsync)
[x] src/backend/GameServer.Api/Hubs/BattleHub.cs (two hub methods; the
      L389–391 status comment corrected)
[x] src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs (two arms)
[ ] src/backend/GameServer.Domain/Battle/ (PetState / StatusEffectLifecycle /
      BattleState — READ-ONLY unless a member proves genuinely required;
      PetState already carries Power, HP/MaxHP, EquippedCards, StatusEffects)
[ ] src/backend/GameServer.Infrastructure/ (READ-ONLY — no Redis, no
      PostgreSQL, no migration; no new key or column)
[ ] src/frontend/client/ (none — the cast UI is a separate follow-up task)
[x] tests/ (Domain unit, Application, Api/hub)
[ ] docs/ (NONE)
[ ] docs/03-decisions/ADR/ (NONE)
[x] tasks/completed/TASK-102-implement-card-cast-server-path.md (this file)
```

---

## Implementation Notes

- **Follow the existing `Swap` path's shape.** `BattleHub.Swap`
  (L567–612) is the template: validate via the Application layer, return a
  rejection without touching state, and on success push `BattleStateUpdated`
  **then** `ReceiveEvents`, then return `SwapResponse(Accepted: true)`.
  `ExecuteSwapAsync` is the template for the read → resolve → single
  `TryUpdateAsync(..., expectedSequence, ...)` → retry-on-mismatch pattern
  (`REDIS_STATE.md` §4 items 1–3, 5). Do not invent a second concurrency
  model, a new repository, or a new store abstraction.
- **The Shield step is the one genuinely new Domain behaviour.** Its rule is
  fully written (`COMBAT_RULES.md` §4 items 2–5), so implement it exactly:
  the pool is consumed **before HP**, and a reapplication **refreshes** the
  existing Shield — set to the new application's magnitude, never summed, with
  at most one Shield instance per entity (`§4 item 3`). Note `DamagePipeline`'s
  own docs currently say it "applies no Shield (`COMBAT_RULES.md` §4 item 2 is
  not implemented)" — still **accurate** today (the step is not implemented),
  and that comment becomes false the moment the step lands and
  **must be corrected in the same change** (`AGENTS.md` §17).
- **Check whether the Shield pool belongs on `PetState` or only in
  `ActiveStatusEffects`.** `GAME_STATE.md` §2.3.1 item 3 already assigns Shield
  the trigger-based expiry model on the existing `StatusEffect` collection
  (`StatusEffectType.Shield`, `ShieldDepletedCondition`). Derive the
  absorption pool from that collection rather than adding a parallel
  `ShieldPoints` member — a second representation would violate
  `GAME_STATE.md` §0 item 5. If a new state member genuinely proves necessary,
  that is a contract change → **STOP** and report.
- **Do not let the cast borrow the Swap pipeline.** `CARD_RULES.md` §3 item 5
  is explicit that a cast is independent of the Turn/Combo system. Reusing
  `SwapExecutor`/`ResolveSwapAsync` wholesale would wrongly increment Turn and
  fire a Boss Response.
- **Cite, do not restate.** This file's domain comments use the
  `<c>CARD_RULES.md</c> §3` citation idiom. Keep it: never copy a cost, an
  effect magnitude, or the Shield formula into a comment (`AGENTS.md` §9,
  `documentation-change.md` §2).
- **`cardId` is a CardDefinition id.** Confirm the exact wire form against
  `DATABASE.md` §1 and the provisioned rows (`card-heal`, `card-shield`,
  `card-power-charge`, `card-inferno`, `card-tidal-barrier`, `card-iron-fang`)
  and against how `PetState.EquippedCards[]` stores them
  (`EquippedCardIdentity`). Do not invent a second identifier.
- **`clientSequence` is an opaque correlation id** and is not the authoritative
  `Sequence` (`SIGNALR_PROTOCOL.md` §2 item 1) — the existing `Swap` method
  deliberately ignores it (`_ = clientSequence;`). Match that.
- **Rejection codes are owned per action.** `SIGNALR_PROTOCOL.md` §5 item 3
  says the domain document owns them and the protocol keeps no parallel list.
  The existing `SwapRejectionCodes` mapping (`BattleHub.cs`) is the precedent
  for spelling them; add the Card ones rather than inlining string literals.
- **PostgreSQL stays off the hot path** (`TDD.md` §4 item 3). The loadout is
  already snapshotted into `PetState.EquippedCards[]` at battle start
  (`ADR-011` item 4); a cast must read that snapshot and must not query the
  Card tables per cast.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16) — e.g. the
  `BattleStateJson.cs` item TASK-097/TASK-098 reported, and the deferred
  Thanh Xà / Sơn Hùng Signature Skill content.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the Domain card-effect resolver (each Basic Card and
                         each content-defined Pet Skill Card), and the Shield
                         absorption maths in DamagePipeline (partial
                         absorption, full absorption, Shield refresh behavior,
                         single Shield instance behavior, Shield depletion
                         behavior, overflow damage to HP). These are the
                         highest-value tests in this task.
[x] Application tests  — cast validation (not in loadout, insufficient Power,
                         valid), rejection-writes-nothing, exact Cost
                         deduction, no Turn increment, no Combo interaction,
                         no Swap-only events, one write-back under the
                         Sequence compare-and-set.
[x] Api/hub tests      — the §5 acknowledgement shape for accept and reject,
                         caller-only delivery, `BattleStateUpdated` before
                         `ReceiveEvents`, and the wire projection of both new
                         events.
[x] Gameplay scenarios — Given/When/Then derived from CARD_RULES.md §3 and
                         COMBAT_RULES.md §4, e.g.:
                           Given an active Pet with Power ≥ a Basic Card's Cost
                           And that Card in the battle loadout
                           When the player casts it
                           Then the Cost is deducted, the effect applies,
                           And a CardCast event is emitted,
                           And the Turn does not change.
                         And the Shield analogue:
                           Given an entity with an active Shield instance
                           When the Boss deals Final Damage
                           Then the Shield absorbs it before HP is affected,
                           And if the damage exceeds the Shield's magnitude
                               only the remaining damage affects HP,
                           And the Shield is removed when its magnitude
                               reaches exactly 0 during the same resolution.
```

### Key Edge Cases

- **A cast that exactly consumes the remaining Power** vs one Power short —
  the boundary of `CARD_RULES.md` §3 item 2's `current Power ≥ Card Cost`.
- **Power Charge at Cost 0** — a legal cast with zero Power; it must not be
  rejected by a naive `Power > 0` check.
- **Power capping** — Power Charge when Power is already near the documented
  maximum (`GAME_RULES.md` §12).
- **Heal at full HP** — overheal is discarded, not banked
  (`COMBAT_RULES.md` §4 item 1).
- **Heal that would exceed MaxHP** — clamped exactly to MaxHP.
- **Shield absorbing damage less than the pool** — the pool is reduced by the
  absorbed amount and **HP is unchanged** (`COMBAT_RULES.md` §4 item 5).
- **Shield absorbing damage precisely equal to the pool** — HP unchanged and
  the pool reaching 0, with the Shield removed in that same resolution
  (`COMBAT_RULES.md` §4 items 4–5).
- **Damage exceeding the pool** — HP reduced by exactly the remainder, no
  double-counting (`COMBAT_RULES.md` §4 item 5).
- **Two Shields applied** — the second **refreshes** the first into one pool set
  to the new application's magnitude; the pool is not summed
  (`COMBAT_RULES.md` §4 item 3).
- **A refresh whose new magnitude is smaller or equal** — the pool is set to the
  new magnitude deterministically (not maximized, not compared), and the equal
  case is idempotent (`COMBAT_RULES.md` §4 item 3).
- **Two different Shield sources applying to one entity** (the Shield Basic Card
  and a Shield-granting Passive) — both share the effect identity `"Shield"`, so
  the second refreshes the first rather than forming a second pool
  (`COMBAT_RULES.md` §4 item 3).
- **A cast of the Signature Skill vs a Basic Card** — `PetSkillCast` +
  `CardCast` vs `CardCast` alone (`CARD_RULES.md` §6).
- **A rejected cast followed by an accepted one** — the rejection must leave
  no residue that changes the accepted cast's outcome.
- **A cast against an unknown/expired battle** — no battle is invented.
- **A cast while a concurrent Swap commits** — the compare-and-set refuses
  rather than overwriting (`REDIS_STATE.md` §4 items 2–3).

### Explicitly Not Tested Here

- Relic triggering on a cast — out of scope (`ROADMAP.md` Phase 1).
- Burn's per-Turn damage tick magnitude and schedule — `DamagePipeline`'s
  existing documented boundary; this task applies Burn as a Status Effect, and
  its tick remains as already implemented (`COMBAT_RULES.md` §5.3.4).
- Client-side cast UI, optimistic prediction, or rendering — no client work.
- Any StatusEffects wire delivery — `SIGNALR_PROTOCOL.md` §4.2 item 2 keeps it
  undelivered.

---

## Stop Conditions

Universal `AGENTS.md` §20 stops always apply. Task-specific:

- **If the `CardCast` / `PetSkillCast` wire shape for the new events is not
  derivable from `GAME_EVENTS.md` §2 and `SIGNALR_PROTOCOL.md` §3.2 without
  inventing a member: STOP and report** — a protocol contract decision is not
  this task's to make.
- **If the Shield absorption rule cannot be implemented without changing a
  documented contract** (e.g. a new `PetState` member proves necessary):
  **STOP per `AGENTS.md` §4/§17** — report the contract gap rather than adding
  a parallel representation (`GAME_STATE.md` §0 item 5).
- **If any Card's Cost or Effect is missing or ambiguous** for a Card this task
  must implement: **STOP per `AGENTS.md` §7.** The Thanh Xà and Sơn Hùng
  Signature Skills are already known-TBD (`PET_RULES.md` §8) and must simply
  be left unimplemented, not invented.
- **If implementing a cast would require running the §17 Swap pipeline, a Boss
  Response, or a Turn increment: STOP** — `CARD_RULES.md` §3 item 5 forbids it.
- **If implementing a cast would require Relic trigger evaluation: STOP and
  report** — out of Phase 1 scope; it is a separate task.
- **If the implementation would require a new SignalR method beyond §2's
  three, a new Redis key, a new API endpoint, or a schema/migration change:
  STOP** — `SIGNALR_PROTOCOL.md` §8 item 7 and the phase scope forbid it.
- **If Player identity / session context is required by a cast but is not
  available on the existing `Swap` path: STOP and report.** Do not invent
  JWT/session behavior; the existing authenticated-session boundary
  (`ADR-015`, `SIGNALR_PROTOCOL.md` §1) is the only mechanism.
- **If two authoritative documents conflict about a Card's behaviour: STOP per
  `AGENTS.md` §4** — report both sources rather than choosing one.
- **If the task would exceed 7 skills or cross multiple uncoupled
  architectural boundaries: STOP and decompose**
  (`tasks/README.md` §13) — do not expand it.
- **If satisfying any criterion requires modifying a completed task, or
  TASK-036 / TASK-079 / TASK-099: STOP** — report instead.

---

## Completion Evidence

### Readiness Review (TASK-102) — BACKLOG → READY

**Result: READY.** All `TASK_LIFECYCLE.md` §3 BACKLOG → READY criteria hold.

```text
[x] Task type confirmed (TASK_TYPES.md)      FEATURE — a documented mechanic
                                             (Card cast) that has a home in
                                             docs/ and is not built. Confirmed
                                             still FEATURE, not GAMEPLAY-CHANGE:
                                             TASK-105 has now authored every
                                             rule this task consumes, so this
                                             task changes no rule.
[x] Relevant documentation exists in docs/    CARD_RULES.md §1–§6,
                                             COMBAT_RULES.md §3–§5,
                                             GAME_RULES.md §11/§12/§16–§18,
                                             SIGNALR_PROTOCOL.md §2/§3/§5,
                                             GAME_EVENTS.md §1–§3,
                                             GAME_STATE.md §2.3/§5.1,
                                             REDIS_STATE.md §4/§7 — all
                                             present and authoritative.
[x] MVP scope confirmed (MVP_SCOPE.md §1)     Cards (3 Basic + 5 Pet Skill) and
                                             Combat/Status Effects are IN; no
                                             OUT item is reached.
[x] Not blocked by an unresolved dependency   TASK-105 is DONE. See Dependency
                                             Review below.
[x] Primary agent assigned                    backend; supporting: gameplay,
                                             realtime, testing, review.
[x] Workflow assigned                         development/feature.md
[x] Acceptance criteria are testable          All binary; each names the owning
                                             doc section and a proving test.
```

#### Dependency Review

```text
TASK-105   DONE     Resolved. Applied B-1/B-2/B-3 to COMBAT_RULES.md §4, the
                    canonical owner of the Shield rule. This task's Shield
                    acceptance criteria are written against its resolved
                    contract — refresh (set to the new magnitude), one
                    instance per entity, no additive accumulation, removal at
                    exactly 0 in the same resolution, overflow to HP by the
                    remainder. No gameplay decision remains open for this
                    task's Shield path.
TASK-028   DONE     Card ownership + loadout snapshot (EquippedCards[]).
TASK-030   DONE     POST /api/battle/start — the battle a cast targets.
TASK-021   DONE     Damage Pipeline the damaging Pet Skill Cards use.
TASK-018   DONE     ResourceGenerator.ApplyPower / ApplyHeal.
TASK-095/  DONE     StatusEffect domain state, step-19a lifecycle, and
TASK-096            serialization round trip — the collection a Shield
                    application writes.
TASK-085   DONE     card-heal / card-shield / card-power-charge / card-inferno
                    / card-tidal-barrier / card-iron-fang definition rows.
```

#### Contract Consistency Review — TASK-102 vs `COMBAT_RULES.md` §4

```text
Shield absorbs before HP          MATCHES §4 item 2 (absorption pool reduces
                                  incoming damage before HP is affected).
No multi-pool ordering            MATCHES §4 item 2 (at most one Shield per
                                  entity means no ordering exists).
One Shield instance per entity    MATCHES §4 item 3 (identity "Shield";
                                  GAME_STATE.md §2.3.1 items 1 and 6).
Shield reapplication refreshes    MATCHES §4 item 3.
New magnitude replaces previous   MATCHES §4 item 3 ("set to the magnitude of
                                  the new application"; not summed, not
                                  maximized, not compared).
No additive stacking              MATCHES §4 item 3 ("no additive accumulation
                                  occurs under any MVP condition").
Shield removed at exactly 0 in
  the same resolution             MATCHES §4 item 4.
Overflow damage goes to HP by
  the remainder                   MATCHES §4 item 5 (HP reduced by exactly the
                                  remainder; no double-counting; equal-to-pool
                                  leaves HP unchanged).
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no Relic, no Turn consumption, no Boss Response on a cast
- [x] Confirmed no invented content value (Thanh Xà / Sơn Hùng remain deferred)

---

## Supersession / Downstream Satisfaction Evidence

### Supersession Reason
TASK-102 was originally authored as a broad monolithic implementation task for the Card Cast and Pet Skill Cast server path. It was never directly executed. Instead, its scope was decomposed, refined by subsequent contract tasks (TASK-108..112 structured `EffectDefinition[]` and TASK-113..114 Crit/Burn/RNG rules), and fully implemented across downstream tasks `TASK-107`, `TASK-115`, `TASK-120`, and `TASK-122`.

### Direct Execution
NOT EXECUTED.

### Traceability Mapping
| TASK-102 Original Deliverable / Acceptance Criterion | Satisfying Task(s) | Evidence / Implementation Artifact |
|---|---|---|
| **Basic CardCast Server Path** (`CardCast` SignalR method, Power deduction, Heal/Shield/Power Charge execution) | `TASK-107` (DONE) | `GameServer.Domain/Cards/CardCastExecutor.cs`, `GameServer.Application/Battle/BattleStateService.cs`, `BattleHub.CardCast`, unit/integration tests. |
| **DamagePipeline Shield Absorption Step** (Shield absorbs before HP, refresh on reapply, depletion at 0, overflow to HP) | `TASK-107` (DONE) | `GameServer.Domain/Combat/DamagePipeline.cs`, `StatusEffectLifecycle.cs`. |
| **CardCast Event & Wire Projection** (`CardCast` BattleEvent, SignalR §3.2.20 projection) | `TASK-107` (DONE) | `BattleEventType.CardCast`, `BattleEventWireProjection.cs`, SignalR tests. |
| **PetSkillCast Server Path & Multi-Effect Execution** (`PetSkillCast` method, multi-effect execution: Damage, Burn, Crit, Heal, Shield) | `TASK-115` (DONE) | `BattleHub.PetSkillCast`, `CardCastExecutor.Execute` for Pet Skill Cards with structured `EffectDefinition[]`. |
| **Combat Crit Evaluation in DamagePipeline** (Step 4 deterministic Crit roll from RngState, 1.5× multiplier) | `TASK-115` (DONE) | `DamagePipeline.cs` Step 4 Crit evaluation, `NextAttackCritModifiers[]` consumption. |
| **Step-19a Burn DoT Lifecycle** (Burn DoT ticking through DamagePipeline and duration decrement) | `TASK-115` (DONE) | `StatusEffectLifecycle.cs`, `CombatService.cs`. |
| **Client-Side CardCast & PetSkillCast Action Paths** (Client interaction, runtime port request, UI integration) | `TASK-120` (DONE) | `src/frontend/client/src/services/runtime/GameRuntime.ts`, `BattleScene.ts`. |
| **EquippedCards Projection onto PetState** (EquippedCards[] serialized in PetState wire DTO) | `TASK-122` (DONE) | `BattleStateWireProjection.cs`, SignalR client contract tests. |

### Independent Actionable Scope Remaining
NONE. All server-side and client-side casting logic, event emission, wire projection, status effect integration, and test verification originally scoped in TASK-102 are 100% implemented, passing all test suites.

### Reconciled File Location
- Moved from `tasks/backlog/TASK-102-implement-card-cast-server-path.md` to `tasks/completed/TASK-102-implement-card-cast-server-path.md` per `tasks/TASK_LIFECYCLE.md` §3 and TASK-130 policy.
