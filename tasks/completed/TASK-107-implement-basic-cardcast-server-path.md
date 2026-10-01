# TASK-107 — Implement the Basic CardCast Server Path (Shield / Heal / Power Charge)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK AUTHORS NO CONTRACT. Every contract it implements already
  exists: CARD_RULES.md §1–§3 (categories, the three Basic Cards' Costs and
  Effects, casting/validation/rejection), COMBAT_RULES.md §4 (Heal clamp,
  Shield — owned by TASK-105), GAME_RULES.md §11/§12/§17 step 14 (Card rules,
  Power range, Resolve Player Effects), SIGNALR_PROTOCOL.md §2 (method) and
  §5 (acknowledgement), GAME_EVENTS.md §2 and SIGNALR_PROTOCOL.md
  §3.2.20/§3.2.22 (the CardCast event and its emission position). The task
  implements them; it changes none of them.

  WHY THIS IS THE NEXT TASK, AND WHY IT IS ONLY THE BASIC-CARD HALF.
  TASK-102 implemented Shield *consumption* (DamagePipeline absorption,
  overflow, depletion) but deliberately not Shield *creation*: ApplyShield
  exists and nothing calls it, so no authoritative path can produce a Shield.
  The missing dependency is therefore Card-cast effect application. The full
  Card-cast path (Basic + PetSkill) is too large for one coherent unit — it
  spans the hub, an Application use case, a Domain effect resolver, two event
  types, two wire projections, and a content gap — so it is decomposed at the
  one seam the contracts already draw: CARD_RULES.md §2 (Basic Cards, fully
  specified) versus §4/§4.1 (Pet Skill Cards, one of which is NOT specified).
  This task takes §2 only.

  WHY PET SKILL CARDS ARE A SEPARATE TASK. Huyền Quy's Tidal Barrier is the
  only Pet Skill Card that grants Shield, and CARD_RULES.md §4.1 gives its
  effect as "Heal; Gain Shield" with NO magnitude. TASK-104 §5 and its B-4
  disposition record this as an unauthored content gap deferred by the
  Product Owner to a separate gameplay decision. Implementing it here would
  require inventing a balance value (AGENTS.md §7). The Shield Basic Card has
  no such gap: CARD_RULES.md §2 fully specifies its Cost and Effect.

  PROVENANCE: identified by the post-TASK-102 next-implementation-task
  discovery pass. ROADMAP.md Phase 1 ("Core Loop Vertical Slice") names
  "3 Basic Cards" as in-scope; BattleHub.cs L389–391 records that
  CardCast/PetSkillCast are "intentionally NOT implemented"; and CARD_RULES.md
  §2 defines all three Basic Cards with their exact Costs and Effects. This is
  the largest remaining Phase 1 gap on the documented critical path that has
  no unresolved content dependency.

  BOUNDARY: no docs/ change, no ADR, no Redis key, no PostgreSQL schema, no
  migration, no new SignalR method, no new event type beyond the two already
  documented, no client work.
-->

---

## Metadata

```text
Task ID:           TASK-107
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented
                   mechanic, capability, or system that already has a home
                   in docs/ but has not yet been built." CARD_RULES.md §1–§3,
                   GAME_RULES.md §11, SIGNALR_PROTOCOL.md §2/§5,
                   GAME_EVENTS.md §2, and SIGNALR_PROTOCOL.md
                   §3.2.20/§3.2.22 already define the entire mechanic. This
                   task builds what is documented and changes no rule.)
Status:            BACKLOG (per tasks/README.md §6 step 6. Ascends to READY
                   only through a lifecycle validation that confirms the
                   BACKLOG → READY criteria of TASK_LIFECYCLE.md §3 — see
                   "Readiness Pre-Check" below and the Reviewer's Checklist.)
Risk:              HIGH (TASK_TYPES.md §4 — FEATURE baseline MEDIUM, "can be
                   HIGH if it touches combat / battle state / auth". This
                   touches PetState.Power, PetState.HP, the StatusEffects[]
                   collection, the resolution write-back, and the SignalR
                   action surface.)
Priority:          HIGH (ROADMAP.md Phase 1 critical path — the documented
                   vertical slice names "3 Basic Cards"; none is castable
                   today, and the Shield Basic Card is meaningless until the
                   Shield it grants can be applied.)
Primary Agent:     gameplay (TASK_TYPES.md §5 / AGENT_SELECTION.md §1 —
                   "Card change: Primary Agent Gameplay". The core logic is
                   the Card effect resolution, and CARD_RULES.md §2/§3 is the
                   primary owner of this task's behaviour.)
Supporting Agents: backend (the Application cast use case and the hub
                   method — GameServer.Application/Battle/ +
                   GameServer.Api/Hubs/),
                   realtime (the two documented events' wire projection,
                   §3.2.20/§3.2.21/§3.2.22 — the arm is applied, not
                   authored),
                   testing, review
Workflow:          development/feature.md
Skills:            gameplay/gameplay-behavior-derivation,
                   discovery/documentation-discovery,
                   discovery/impact-analysis,
                   backend/api-contract-validation,
                   realtime/realtime-protocol-validation,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (7 skills — Complex budget ceiling, tasks/README.md §12)
Dependencies:      TASK-102 (the Shield absorption/refresh/depletion
                     implementation this task's Shield application is
                     consumed by — read its Shield step, do not modify it),
                   TASK-105 (DONE — COMBAT_RULES.md §4's refresh / one-instance
                     / no-accumulation / depletion-at-0 contract. IMMUTABLE,
                     NOT modified),
                   TASK-103 (the CardCast wire contract, APPLIED —
                     SIGNALR_PROTOCOL.md §3.2.20–§3.2.25; read-only),
                   TASK-104 (the Product Owner decisions A-1A…A-5A and their
                     B-4 disposition; read-only),
                   TASK-028 (DONE — Card ownership + PetState.EquippedCards[]
                     loadout snapshot),
                   TASK-030 (DONE — POST /api/battle/start; the battle the
                     cast targets exists),
                   TASK-018 (DONE — ResourceGenerator.ApplyPower /
                     ApplyHeal, the Power and Heal mechanics the Basic Cards
                     reuse),
                   TASK-095/TASK-096 (DONE — StatusEffect domain state,
                     step-19a lifecycle, and serialization; the collection a
                     Shield application writes),
                   TASK-085 (DONE — card-heal / card-shield /
                     card-power-charge definition rows are provisioned)
Blocks:            Nothing directly. It unblocks a follow-up PetSkillCast task
                   (which additionally requires TASK-104 B-4's Tidal Barrier
                   magnitude decision) and, after it, the client-side
                   Card-cast UI. It does NOT unblock TASK-036, TASK-079, or
                   TASK-099.
Estimate:          Complex (crosses Domain → Application → Api; one new
                   Domain effect resolver, one Application use case, one hub
                   method, one BattleEventType member and factory, and one
                   wire-projection arm, plus their tests)
```

**Type classification note.** `FEATURE`, not `GAMEPLAY-CHANGE`. Per
`TASK_TYPES.md` §2, a `GAMEPLAY-CHANGE` is used when "a gameplay mechanic needs
to behave differently than the documentation currently says, OR a new
undocumented mechanic is being authorized." Neither applies:
`CARD_RULES.md` §1–§3 fixes the three Basic Cards' Costs and Effects, the
validation contract, the rejection semantics, and the ordered resolution;
`COMBAT_RULES.md` §4 (as resolved by TASK-105) fixes Shield's application,
refresh, and depletion; `GAME_RULES.md` §11/§12/§17 fix the Card rules, the
Power range, and the resolution position; and `SIGNALR_PROTOCOL.md`
§2/§5/§3.2.20/§3.2.22 fixes the transport and the event. This task builds what
is documented and changes no rule.

**No ADR is required.** This implements an already-decided boundary rather than
changing one. `ADR-001` (server authority) governs and is satisfied by
construction — the server validates and resolves every cast. `ADR-011` item 4
(the loadout is selected at `POST /api/battle/start` for the one active Pet and
snapshotted into `PetState.EquippedCards[]`) already owns where the castable
Cards come from, and this task reads that snapshot rather than introducing a
second source. No layer, storage, realtime, or authoritative-state model
changes; `architecture/adr-change.md` §2's test (architecturally important,
difficult to reverse, cross-cutting) is not met.

**Shield semantics are frozen and are NOT this task's to change.** This task
*creates* a Shield by calling the existing application operation; it does not
redefine what a Shield does. The contract it must consume unchanged:

```text
one Shield instance per entity      COMBAT_RULES.md §4 item 3
refresh replaces magnitude          COMBAT_RULES.md §4 item 3
no additive stacking                COMBAT_RULES.md §4 item 3
absorption before HP                COMBAT_RULES.md §4 items 2, 5
overflow by remainder               COMBAT_RULES.md §4 item 5
ShieldDepleted is NOT a Battle Event COMBAT_RULES.md §4 item 4
```

If satisfying any criterion appears to require changing one of these, that is a
STOP (see Stop Conditions) — not an adjustment.

**This task does not implement Relics.** `ROADMAP.md` Phase 1 states "No Relics
yet"; the Relic trigger/effect system is Phase 2. `CARD_RULES.md` §3 item 4's
downstream `RelicTriggered` step (`RELIC_RULES.md` §3 `OnCardCast`) is therefore
**out of scope** and must be reported rather than implemented.

---

## Objective

Implement the documented **Basic Card** cast path end-to-end on the server: the
`CardCast(battleId, cardId, clientSequence)` `BattleHub` method
(`SIGNALR_PROTOCOL.md` §2) that validates a cast against `CARD_RULES.md` §3
item 2 (the Card is in the active Pet's battle loadout snapshot; the active Pet
has sufficient Power; no additional MVP Basic Card preconditions), deducts the
Cost, applies the Card's documented effect (`CARD_RULES.md` §2 — **Heal**
restores HP clamped to MaxHP, **Shield** grants the active Pet a Shield
absorption pool through the existing `StatusEffect` representation, **Power
Charge** adds Power clamped to the documented maximum), commits one write-back
under the existing `Sequence` compare-and-set, pushes the resulting ordered
Battle Events including the documented `CardCast` event, and returns the §5
`{ accepted, reason }` acknowledgement — while changing no rule, contract, or
schema, and while leaving `PetSkillCast` and the Pet Skill Cards to a separate
task.

---

## Authoritative References

- `docs/01-game-design/CARD_RULES.md` **§1** (Card categories; the battle
  loadout is snapshotted into `PetState.EquippedCards[]` — the castable set),
  **§2** the MVP Basic Cards with their exact Costs and Effects (Heal, Shield,
  Power Charge; target = the active Pet; item 3 records Power Charge's Cost 0
  "must never be blocked by insufficient Power"), **§3** the casting and
  validation rules (item 2 the rejection contract, item 3 the rejection
  semantics — "no state changes occur", item 4 the ordered resolution —
  deduct Cost → apply Effect → emit `CardCast` → (Relic step, out of scope),
  item 5 the no-Turn/no-Combo rule, item 6 the no-client-authority rule),
  **§6** the `CardCast` event and its emission point. **The primary owner of
  this task's behaviour.**
- `docs/01-game-design/COMBAT_RULES.md` **§4** — the frozen Shield contract
  (item 1 Heal clamps to Max HP and discards overheal; item 2 absorption
  before HP with no multi-pool ordering; item 3 refresh / one instance /
  set-to-new-magnitude / no additive accumulation; items 4–5 removal at
  exactly 0 in the same resolution and overflow by exactly the remainder;
  item 6 Heal and Shield are **not** subject to the Damage Pipeline);
  **§5.1 / §5.2** — Status Effects and the refresh-duration default Shield
  follows; **§5.3 / §5.3.2** — trigger-based instances take no Turn duration;
  **§6** — Power, and this document's ownership boundary with
  `GAME_RULES.md` §12.
- `docs/01-game-design/GAME_RULES.md` **§11** — Card rules (Cards are active
  actions, consume/generate Power, cannot bypass server validation); **§12** —
  the Power range 0–100 and its cap invariant; **§17 step 14** "Resolve Player
  Effects" — the resolution position a cast's effect occupies; **§17 step 19a**
  — where a Turn-based instance is consumed (Shield is trigger-based and is
  not); **§18** — server authority; **§16** — the canonical event list
  (`CardCast` is in it; no `ShieldDepleted`).
- `docs/02-technical/GAME_STATE.md` **§0 item 5** — no parallel representation
  of a concept another stage owns; **§2.3** — `PetState`, which carries
  `Power`, `HP`/`MaxHP`, `EquippedCards[]`, and `ActiveStatusEffects[]`;
  **§2.3.1** — the Status Effect instance schema a Shield application writes
  (item 1 `Id` is an identity, item 2 `Magnitude` is typed but not
  interpreted here, item 3 Shield uses `ExpiryCondition` and never
  `RemainingTurns`, item 6 never more than one instance per identity, item 7
  absence conventions, item 8 zero is never a stored state); **§2.3.2** — the
  serialization round trip (already satisfied by TASK-096); **§5.1** — the
  single post-resolution write-back; **§5.1.1** — the `StatusEffects[]`
  lifecycle (item 1 apply is a "set", not an increment; item 7 trigger-based
  instances are not decremented; item 10 nothing here is published; item 11 a
  rejected action mutates nothing).
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§2** — the
  `CardCast(battleId, cardId, clientSequence)` method contract (item 1
  `clientSequence` is an opaque correlation id and is NOT the authoritative
  `Sequence`; item 2 validation is owned by `CARD_RULES.md` §3; item 3 a
  rejection returns to the caller only and emits no events); **§5** — the
  `{ accepted, reason }` acknowledgement and its per-action reason ownership
  (item 3 — the domain document owns the codes; item 4 — caller only, never
  the group); **§3.2.20** — the `CardCast` wire shape (`type`, `cardId`; item
  2 records that MVP carries **no** Power cost member), **§3.2.22** — the
  emission order (`CardCast` first, then `PetSkillCast` when applicable; a
  Basic Card cast emits `CardCast` alone), **§3.2.25** — the `effect summary`
  omission convention; **§3.1 item 1 / §6 item 4** — `BattleStateUpdated`
  before the `ReceiveEvents` batch; **§4.2 item 2** — what the state push
  explicitly does not carry; **§8 item 7** — the gameplay methods are exactly
  §2's three.
- `docs/02-technical/GAME_EVENTS.md` **§1.1** — the ordered event list;
  **§2** — the `CardCast` event meaning and payload; **§3** — ordering, item
  1 (wire shape is `SIGNALR_PROTOCOL.md`'s), item 5 (an event defined in §2
  belongs to §1's ordered list).
- `docs/02-technical/ARCHITECTURE.md` **§2.1** — the layer boundary (Domain
  owns rules, Application orders the calls, Api is a thin transport);
  **§2.2.1** — the client → server action boundary; **§2.2.2** — the
  implemented `Swap` path as the precedent shape; **§5** — the
  anti-overengineering standard.
- `docs/02-technical/TDD.md` **§4 item 3** — PostgreSQL is never read on the
  hot resolution path; **§2.1** — the client is presentation-only; **§6** —
  determinism.
- `docs/02-technical/REDIS_STATE.md` **§4 items 2–3, 5, 7** — the `Sequence`
  compare-and-set write-back semantics and "a rejected action writes nothing";
  **§7 item 9** — the round-trip obligation the Shield instance already
  satisfies.
- `docs/02-technical/DATABASE.md` **§1** — `CardDefinition` (`Cost`, `Effect`,
  `Category`, `LoadoutCopyLimit`); **§2** — `PlayerUnlockedCard` ownership.
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; every cast is validated and resolved server-side.
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` item 4 — the
  loadout is selected at `POST /api/battle/start` and snapshotted into
  `PetState.EquippedCards[]`; the Pet is the combat character the Card effects
  target.
- `docs/00-overview/MVP_SCOPE.md` §1 — Cards (3 Basic + 5 Pet Skill) and
  Combat/Status Effects are IN; §2 — nothing here reaches an OUT item.
- `docs/00-overview/ROADMAP.md` — Phase 1 names "3 Basic Cards" in the core
  loop vertical slice (direction, not authorization — `ROADMAP.md` §2).
- `tasks/completed/TASK-021-implement-damage-pipeline.md` — the pipeline the
  damaging effects would use; read-only.
- `tasks/completed/TASK-018-implement-resource-generation.md` —
  `ResourceGenerator.ApplyPower` / `ApplyHeal`, which the Power Charge and
  Heal effects reuse; read-only.
- `tasks/completed/TASK-095-implement-statuseffect-domain-state-and-step-19a-lifecycle.md`
  and `TASK-096-serialize-statuseffects-round-trip.md` — the StatusEffect state
  and lifecycle a Shield application writes; read-only.
- `tasks/completed/TASK-105-change-shield-application-semantics-to-refresh.md`
  — the Shield contract this task consumes unchanged. **Immutable.**
- `tasks/backlog/TASK-102-implement-card-cast-server-path.md` — the Shield
  absorption implementation and its boundary. Note: TASK-102's own scope was
  broader (it also covered `PetSkillCast` and the Pet Skill Cards); this task
  delivers only its Basic-Card half and must NOT be read as completing it.
  Read-only.
- `tasks/backlog/TASK-103-resolve-cardcast-wire-schema-and-shield-stacking-contract.md`
  — the applied CardCast wire contract; read-only.
- `tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — the Product Owner decisions A-1A…A-5A, and **§5 / B-4**: Tidal Barrier's
  Shield magnitude is an unauthored content gap deferred to a separate
  gameplay decision, which is why Pet Skill Cards are out of scope here.
  Read-only.

**ADR check:** no ADR is required (see the Metadata note).

---

## Current State

The Card system is **documented and provisioned but not executable**. The
definitions, the ownership, the loadout snapshot, and the Shield consumption
step all exist; nothing applies a Card's effect.

### What exists (verified at discovery)

```text
docs/01-game-design/CARD_RULES.md      §1–§6 COMPLETE — categories, the three
                                       Basic Cards (Cost + Effect), the casting
                                       and validation rules, the Pet Skill
                                       Cards, and the two events.
docs/01-game-design/COMBAT_RULES.md    §4 COMPLETE — Heal clamp, Shield
                                       refresh / one-instance / depletion,
                                       authoritative as of TASK-105.

src/backend/GameServer.Domain/Cards/
  CardCategory.cs                      EXISTS
  CardDefinition.cs                    EXISTS (Cost / Effect / Category)
  EquippedCardIdentity.cs              EXISTS
  PlayerUnlockedCard.cs                EXISTS

src/backend/GameServer.Domain/Battle/
  PetState.cs                          Power, HP, MaxHP, EquippedCards[],
                                       ActiveStatusEffects[]  ALL EXIST
  StatusEffect.cs                      EXISTS — ShieldDepletedCondition,
                                       TurnBased / TriggerBased factories
  StatusEffectLifecycle.cs             EXISTS — Apply (Turn-based refresh),
                                       ApplyShield, RemoveDepletedShield,
                                       ShieldPool, ConsumeAtStep19a
  StatusEffectType.cs                  EXISTS — Shield = trigger-based
src/backend/GameServer.Domain/Combat/
  DamagePipeline.cs                    EXISTS — Shield absorption step
                                       (TASK-102), DAMAGE-side only

src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
  ApplyPower(...)  EXISTS — the Power mechanic + cap
  ApplyHeal(...)   EXISTS — HP restore clamped to MaxHP (COMBAT_RULES.md §4.1)

src/backend/GameServer.Application/Battle/BattleStateService.cs
  ExecuteSwapAsync(...)  EXISTS — the read → resolve → single
                         TryUpdateAsync(..., expectedSequence, ...) →
                         retry-on-mismatch precedent to mirror

src/backend/GameServer.Infrastructure/Postgres/Migrations/
  20260929152651_ProvisionPetCardRelicContentDefinitions.cs
  PROVISIONED — card-heal, card-shield, card-power-charge rows exist and are
                read, never written
```

### What is missing (verified at discovery)

```text
src/backend/GameServer.Api/Hubs/BattleHub.cs
  L389–391 states outright:
    "CardCast, PetSkillCast (§2) and GetBattleState (§7) are client → server
     methods that are intentionally NOT implemented"
  Hub methods present: JoinBattle, Swap, Ping. NO CardCast.

src/backend/GameServer.Domain/
  NO Card effect resolver — nothing reads CardDefinition.Effect and applies it.
  StatusEffectLifecycle.ApplyShield EXISTS but is called by NOBODY: no
  authoritative path can currently create a Shield.

src/backend/GameServer.Domain/Match3/BattleEvent.cs
  NO CardCast member exists. (L45–47 records that no CardCast / PetSkillCast /
  BossSkillCast event exists here.)

src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
  The discriminator switch covers the currently-emitted types and has NO arm
  for CardCast, although SIGNALR_PROTOCOL.md §3.2.20 already fixes its shape.
```

### The consequence

A player can Swap but cannot cast a Card, so `CARD_RULES.md` §2's three Basic
Cards — including the **Shield** Basic Card — are unreachable. Consequently
TASK-102's Shield absorption step, `ApplyShield`, and `RemoveDepletedShield`
have no producer: the Shield mechanic is implemented on the consumption side
only. `ROADMAP.md` Phase 1's "3 Basic Cards" is therefore not reachable.

---

## Scope

### In Scope

1. **The one hub method** (`SIGNALR_PROTOCOL.md` §2): `CardCast(battleId,
   cardId, clientSequence)`, mirroring the existing `Swap` method's structure
   (validate → delegate → commit → push state → push events → return the §5
   acknowledgement) and returning `{ accepted, reason }` per §5. The
   `clientSequence` is echoed/ignored as `Swap` does (item 1 — it is not the
   authoritative `Sequence`).
2. **A Domain Card-effect resolver for the three Basic Cards** that reads a
   `CardDefinition` and applies its documented effect to the active Pet's
   state (`CARD_RULES.md` §2):
   - **Heal** — restore HP, clamped to `MaxHP`, overheal discarded
     (`COMBAT_RULES.md` §4 item 1). Reuse `ResourceGenerator.ApplyHeal`.
   - **Shield** — grant the active Pet a Shield absorption pool **through the
     existing `StatusEffect` representation**, by calling the existing
     `StatusEffectLifecycle.ApplyShield` with a `StatusEffect.TriggerBased`
     instance of identity `"Shield"` and `ExpiryCondition =
     StatusEffect.ShieldDepletedCondition` (`GAME_STATE.md` §2.3.1 items 1–3;
     `COMBAT_RULES.md` §4 item 3). **No new state member.**
   - **Power Charge** — add Power, clamped to the documented maximum
     (`CARD_RULES.md` §2 item 3, `GAME_RULES.md` §12). Reuse
     `ResourceGenerator.ApplyPower`.
3. **The cast validation contract** (`CARD_RULES.md` §3 item 2): the Card is in
   the active Pet's `PetState.EquippedCards[]` snapshot for this battle; the
   active Pet's `Power ≥ CardDefinition.Cost`; no additional MVP Basic Card
   preconditions (item 2's third bullet). A rejected cast **changes nothing**
   (item 3): no Power spent, no effect applied, no `StatusEffect` written, no
   event emitted, and no write reaches the store (`REDIS_STATE.md` §4 item 7).
4. **The ordered resolution** (`CARD_RULES.md` §3 item 4): deduct Cost from
   Power → apply Effect → emit `CardCast` — committed as **one** write-back
   under the existing `Sequence` compare-and-set (`REDIS_STATE.md` §4 items
   2–3, 5), on the same `IBattleStateRepository.TryUpdateAsync` pattern
   `ExecuteSwapAsync` uses. **The §3 item 4 Relic sub-step is out of scope**
   (see Out of Scope) — the resolution ends at `CardCast`.
5. **The `CardCast` Battle Event** (`GAME_EVENTS.md` §2, `CARD_RULES.md` §6): a
   `BattleEventType` member and a `BattleEvent` factory, plus the
   `BattleEventWireProjection` arm producing the wire shape
   `SIGNALR_PROTOCOL.md` §3.2.20 **already fixes** (`type`, `cardId`; the
   §3.2.3/§3.2.4/§3.2.5 conventions apply; `effect summary` omitted per
   §3.2.25; no Power cost member per §3.2.20 item 2). **The shape is authored;
   this task applies it.**
6. **No Turn and no Combo** (`CARD_RULES.md` §3 item 5): a cast consumes no
   Turn and does not interact with Combo. It therefore does **not** increment
   `Turn`, does **not** run the §17 Swap pipeline, and does **not** run the
   Boss Response steps. Because Shield is trigger-based, the step 19a pass
   leaves it alone (`GAME_STATE.md` §5.1.1 item 7) — so a cast is not a
   resolution step 19a runs for; assert the documented absence of any
   Turn/Combo effect rather than running the Swap resolution.
7. **`PetSkillCast` is NOT implemented here**, but this task must not make it
   harder: the `CardCast` event and the shared validation/effect-resolution
   seam should be shaped so the Pet Skill half can reuse them. Do **not** add
   the `PetSkillCast` method, its `BattleEventType` member, or its projection
   arm.
8. **Tests**: Domain unit tests for the Basic Card effect resolver (each of the
   three Cards, the Heal clamp, the Power cap, and the Shield application's
   refresh semantics); Application tests for validation, rejection
   (writes-nothing), exact Cost deduction, the Power-Charge-cost-0 boundary,
   the no-Turn / no-Combo assertions, and the single write-back under the
   compare-and-set; Api/hub tests for the §5 acknowledgement shape for accept
   and reject, caller-only delivery, `BattleStateUpdated` before
   `ReceiveEvents`, and the `CardCast` wire projection round trip.

### Out of Scope

- **`PetSkillCast` and the Pet Skill Cards** (`CARD_RULES.md` §4/§4.1).
  Deferred to a separate task. **Reason (verified):** Huyền Quy's Tidal
  Barrier — the only Pet Skill Card that grants Shield — has **no authored
  Shield magnitude** (`CARD_RULES.md` §4.1 records only "Heal; Gain Shield"),
  and TASK-104 §5 / its B-4 disposition record this as a content gap deferred
  by the Product Owner to a separate gameplay decision. Implementing it here
  would require inventing a balance value (`AGENTS.md` §7). The Thanh Xà and
  Sơn Hùng Signature Skills are likewise TBD content (`PET_RULES.md` §8) —
  no placeholder, no invented Skill Card, no invented value.
- **Relics.** No `RelicTriggered` event, no `OnCardCast` trigger, no Relic
  effect engine — `CARD_RULES.md` §3 item 4's downstream Relic step is
  **explicitly skipped**, not implemented. `ROADMAP.md` Phase 1 states "No
  Relics yet". Report it; do not implement it.
- **Any rule, formula, cost, magnitude, or balance value that is not already
  written down.** In particular, no Shield magnitude is authored anywhere: the
  Shield Basic Card's magnitude is `CARD_RULES.md` §2's and must be read from
  the Card content, not hardcoded in the resolver.
- **Any change to Shield semantics.** One instance per entity, refresh
  replaces magnitude, no additive stacking, absorption before HP, overflow by
  remainder, `ShieldDepleted` is not a Battle Event — all frozen
  (`COMBAT_RULES.md` §4, TASK-105). This task only *calls* the operation that
  applies a Shield.
- **The "Burning Curse" Relic** — deferred under an unresolved
  `RELIC_RULES.md` §3-vs-note-1 tension; not this task's to resolve.
- **`GetBattleState`** — `SIGNALR_PROTOCOL.md` §7 reconnect/resync is
  **Phase 3** (`ROADMAP.md`); not implemented here. The `BattleHub.cs`
  L389–391 comment must be corrected to describe the Basic `CardCast`
  implemented state **while `GetBattleState` and `PetSkillCast` remain
  documented as not implemented**.
- **StatusEffects as a wire/state-push member** — `SIGNALR_PROTOCOL.md` §4.2
  item 2 explicitly excludes HP/ATK/DEF/Crit/Power/StatusEffects/
  EquippedRelics/EquippedCards from the `BattleStateUpdated` push. This task
  adds **no** new state-push field and no protocol decision about delivering
  StatusEffects.
- **Any new SignalR method** beyond §2's three.
  `SIGNALR_PROTOCOL.md` §8 item 7 is explicit: "The gameplay methods are
  exactly the three of §2." This task adds `CardCast` (already in §2) only.
- **Any Redis key, field, TTL, or record**; any new API endpoint; any
  PostgreSQL schema, entity, column, constraint, or migration. The definition
  rows already exist (TASK-085) and are read, never written. The Shield pool
  remains part of the existing `BattleState` round trip.
- **Any client/Phaser/React work** — the cast UI is a separate follow-up task.
  The client must not compute or authoritatively decide any Shield value.
- **Any other gameplay mechanic** — no new Pet, Boss, Relic, Element, Status
  Effect, resource, or progression system; no change to Swap, Match-3,
  Cascade, Combo, Passive, Boss Response, or the Damage Pipeline's existing
  formula steps.
- **`docs/`** — no authoritative document is modified. If implementation
  reveals a doc is wrong, STOP per `AGENTS.md` §4/§17.
- **TASK-102, TASK-103, TASK-104, TASK-105, TASK-106, and every
  `tasks/completed/*` file** — read-only; do not modify, re-status, or move.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Cards/ (NEW Basic Card effect resolver — the
      CardDefinition.Effect → active-Pet-state transformation, calling the
      existing StatusEffectLifecycle.ApplyShield for Shield)
[x] src/backend/GameServer.Application/Battle/ (a Card-cast use case /
      resolution method beside ExecuteSwapAsync — validation, Cost deduction,
      effect application, single write-back under the Sequence
      compare-and-set, retry-on-mismatch)
[x] src/backend/GameServer.Domain/Match3/BattleEvent.cs (ONE new
      BattleEventType member + factory, for CardCast)
[x] src/backend/GameServer.Api/Hubs/BattleHub.cs (the CardCast hub method; the
      L389–391 status comment corrected to the implemented state — leaving
      GetBattleState and PetSkillCast documented as not implemented)
[x] src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs (ONE arm, for
      the §3.2.20 CardCast shape)
[ ] src/backend/GameServer.Domain/Battle/ (StatusEffect / StatusEffectLifecycle
      / PetState / BattleState — READ-ONLY. ApplyShield, RemoveDepletedShield,
      ShieldPool, ApplyHeal, and ApplyPower already exist and are called, not
      modified. A required new state member would be a contract change → STOP.)
[ ] src/backend/GameServer.Domain/Combat/DamagePipeline.cs — READ-ONLY
      (TASK-102's absorption step is consumed, not changed)
[ ] src/backend/GameServer.Infrastructure/ (READ-ONLY — no Redis, no
      PostgreSQL, no migration, no new key or column)
[ ] src/frontend/client/ (NONE — the cast UI is a separate follow-up task)
[x] tests/ (Domain unit, Application, Api/hub)
[ ] docs/ (NONE)
[ ] docs/03-decisions/ADR/ (NONE)
[x] tasks/ (this file only)
```

---

## Acceptance Criteria

### Shield contract preservation (frozen — this task consumes, never changes)

- [ ] **The Shield contract is unchanged**: `docs/01-game-design/COMBAT_RULES.md`
      is **byte-identical** before and after (verified by SHA256), and
      `StatusEffectLifecycle.ApplyShield` / `RemoveDepletedShield` /
      `ShieldPool` and `DamagePipeline`'s absorption step are not modified in
      behaviour.
- [ ] **Shield application uses the existing `StatusEffect`
      representation**: the granted Shield is a `StatusEffect` instance with
      `Id = "Shield"`, `Type = StatusEffectType.Shield`, a
      `Magnitude` carrying the absorption pool, and `ExpiryCondition =
      StatusEffect.ShieldDepletedCondition` — constructed via
      `StatusEffect.TriggerBased` and applied via
      `StatusEffectLifecycle.ApplyShield` (`GAME_STATE.md` §2.3.1 items 1–3).
- [ ] **No new Shield state member exists**: no `PetState.ShieldPoints`, no
      `PetState.Shield`, no `BattleState.Shield`, no `ShieldPool` field, and no
      other parallel representation (`GAME_STATE.md` §0 item 5). `PetState.cs`
      and `BattleState.cs` are **byte-identical**.
- [ ] **No Shield Battle Event exists**: no `ShieldDepleted` (or similar)
      `BattleEventType` member, payload, or wire member is added
      (`COMBAT_RULES.md` §4 item 4; `GAME_RULES.md` §16).
- [ ] **No additive stacking**: applying a Shield while one is active
      **refreshes** it — exactly one `"Shield"` instance remains and its
      magnitude is **set to the new application's**, never summed
      (`COMBAT_RULES.md` §4 item 3). Proven by test.
- [ ] **No client-authoritative Shield mutation**: every Shield magnitude,
      Cost, and effect value is computed server-side (`AGENTS.md` §10,
      `ADR-001`); no client-side gameplay logic is introduced anywhere.

### The cast path

- [ ] `BattleHub` exposes `CardCast(battleId, cardId, clientSequence)` with
      exactly the parameters `SIGNALR_PROTOCOL.md` §2 documents, returning the
      §5 `{ accepted, reason }` acknowledgement shape.
- [ ] `BattleHub.cs`'s L389–391 comment no longer states that `CardCast` is not
      implemented; it is corrected to describe the implemented **Basic Card**
      state, while `PetSkillCast` and `GetBattleState` (§7, Phase 3)
      **remain** documented as not implemented.
- [ ] A cast is rejected when the Card is not in the active Pet's
      `PetState.EquippedCards[]` snapshot for this battle (`CARD_RULES.md` §3
      item 2, first bullet).
- [ ] A cast is rejected when the active Pet's `Power < CardDefinition.Cost`
      (`CARD_RULES.md` §3 item 2, second bullet), and the rejection reason is
      the documented `INSUFFICIENT_POWER` code (`SIGNALR_PROTOCOL.md` §5 item
      3 — the domain document owns the code; follow the existing
      `SwapRejectionCodes` spelling precedent rather than inlining string
      literals).
- [ ] **A rejected cast changes nothing**: no Power is spent, no effect is
      applied, no `StatusEffect` is written, no event is emitted, and no write
      reaches the store (`CARD_RULES.md` §3 item 3, `REDIS_STATE.md` §4 item
      7, `GAME_STATE.md` §5.1.1 item 11). Proven by asserting the persisted
      state is unchanged before and after.
- [ ] A successful cast deducts exactly the Card's documented Cost from
      `PetState.Power` (`CARD_RULES.md` §2; Power Charge costs 0 and therefore
      deducts 0).
- [ ] **`Heal`** restores the active Pet's HP by the documented amount, clamped
      to `MaxHP`, with overheal discarded (`CARD_RULES.md` §2,
      `COMBAT_RULES.md` §4 item 1). Proven at full HP and at a HP that would
      exceed `MaxHP`.
- [ ] **`Shield`** grants the active Pet a Shield absorption pool of the
      documented amount, represented as the `StatusEffect` instance above
      (`GAME_STATE.md` §2.3.1 item 3, `COMBAT_RULES.md` §4 item 2). A second
      cast of the same Card **refreshes** the existing instance and creates no
      second one (`COMBAT_RULES.md` §4 item 3). Proven by test.
- [ ] **`Power Charge`** adds the documented amount of Power, and Power never
      exceeds the documented maximum (`CARD_RULES.md` §2 item 3,
      `GAME_RULES.md` §12). A `Power Charge` at Cost 0 is **not** rejected by
      a naive `Power > 0` check (`CARD_RULES.md` §2 item 3).
- [ ] **The granted Shield is consumed by the existing absorption step**: after
      a successful Shield cast, a subsequent damage instance is absorbed by
      the pool before HP is affected, and overflow reduces HP by the remainder
      only (`COMBAT_RULES.md` §4 items 2, 5). This is the end-to-end proof that
      the cast path and TASK-102's absorption are connected — and it is proven
      **without modifying** `DamagePipeline` or `ApplyShield`.
- [ ] A cast consumes **no** Turn and does **not** interact with Combo
      (`CARD_RULES.md` §3 item 5): `BattleState.Turn`, `MatchCount`, and
      `Combo` are unchanged by a cast, proven by assertion.
- [ ] A cast does **not** run the §17 Swap pipeline and does **not** trigger a
      Boss Response; no Swap-only event (`MatchCreated`, `GemMatched`,
      `CascadeCreated`, `ComboChanged`) appears in a cast's event batch.
- [ ] A **single** write-back per accepted cast, under the `Sequence`
      compare-and-set (`REDIS_STATE.md` §4 items 2–3, 5), on the same
      `IBattleStateRepository.TryUpdateAsync` pattern `ExecuteSwapAsync` uses.
- [ ] The `CardCast` event is projected onto the §3.2 wire schema — a
      `BattleEventType` member and a `BattleEventWireProjection` arm exist, the
      payload matches `SIGNALR_PROTOCOL.md` §3.2.20 (`type`, `cardId`; no Power
      cost member per item 2; `effect summary` omitted per §3.2.25), and the
      round trip is asserted by a test.
- [ ] An accepted cast pushes `BattleStateUpdated` **before** its
      `ReceiveEvents` batch (`SIGNALR_PROTOCOL.md` §3.1 item 1, §6 item 4).
- [ ] The method returns the acknowledgement **to the caller only**, never to
      the group (`SIGNALR_PROTOCOL.md` §5 item 4).
- [ ] An unknown `battleId` resolves nothing and returns a rejection rather
      than creating or inventing a battle — consistent with the existing
      `Swap` path's `BATTLE_NOT_FOUND` behaviour.

### Existing behavior preserved

- [ ] **Existing `DamagePipeline` behavior remains valid**: TASK-102's
      absorption/overflow/depletion tests still pass **unmodified**, and no
      pipeline formula step changes.
- [ ] **All existing tests remain green unmodified**, and the new tests pass —
      Domain, Application, Infrastructure, and Api suites.
- [ ] Swap, Match-3, Cascade, Combo, Passive, Boss Response, and the step 19a
      lifecycle are unchanged: a Swap's resolution and its event batch are
      byte-identical to before this change.

### Scope and contracts

- [ ] **No `PetSkillCast`, no Pet Skill Card resolution, and no Tidal Barrier
      magnitude** is implemented or invented (`CARD_RULES.md` §4.1,
      `PET_RULES.md` §8, TASK-104 B-4).
- [ ] **No new SignalR method** beyond §2's three exists; the hub's method set
      is exactly `JoinBattle`, `Swap`, `CardCast`, `Ping` (plus
      `PetSkillCast` only when the separate task adds it).
- [ ] **No new Redis key, field, TTL, or record**; no new API endpoint; no
      PostgreSQL schema, entity, column, constraint, or migration; the
      definition rows are read, never written.
- [ ] **No new state-push member**: `BattleStateUpdated`'s payload gains no
      field, and StatusEffects remain undelivered there
      (`SIGNALR_PROTOCOL.md` §4.2 item 2).
- [ ] **No Relic trigger** is added (`ROADMAP.md` Phase 1 — "No Relics yet");
      a test asserts its absence.
- [ ] **Zero files under `docs/` are modified**; **zero ADRs** created or
      edited.
- [ ] TASK-102, TASK-103, TASK-104, TASK-105, TASK-106, TASK-036, TASK-079,
      TASK-099, and every `tasks/completed/*` file are **byte-identical**.
- [ ] Build is green with zero new errors and zero new warnings.
- [ ] Quality review checklist passes (`quality/review.md` §1).
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 /
      `ADR-001`).

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
No PetSkillCast, no Pet Skill Card resolution, no invented magnitude.
No Turn consumption, no Combo interaction, no Boss Response on a cast.
No invented content values.
No Shield contract change, no Shield state field, no Shield event.
No docs/ changes.
No ADR.
```

---

## Implementation Notes

- **Follow the existing `Swap` path's shape.** `BattleHub.Swap` is the
  template: validate via the Application layer, return a rejection without
  touching state, and on success push `BattleStateUpdated` **then**
  `ReceiveEvents`, then return `SwapResponse(Accepted: true)`. The Application
  layer's existing `ExecuteSwapAsync` is the template for the read → resolve →
  single `TryUpdateAsync(..., expectedSequence, ...)` → retry-on-mismatch
  pattern (`REDIS_STATE.md` §4 items 1–3, 5). Do not invent a second
  concurrency model, a new repository, or a new store abstraction.
- **Apply the Shield through the operation TASK-102 already left you.** Call
  `StatusEffectLifecycle.ApplyShield(effects, StatusEffect.TriggerBased(
  "Shield", StatusEffectType.Shield, StatusEffectSource.Player, magnitude,
  StatusEffect.ShieldDepletedCondition))`. It already implements
  refresh-in-place, one-instance-per-identity, and no-accumulation. Do **not**
  write a second Shield applier, do not touch `ApplyShield`, and do not
  construct the instance with `TurnBased` (it rejects Shield by design —
  `GAME_STATE.md` §2.3.1 item 3).
- **Derive the Shield magnitude from the Card content, not a constant.** The
  Shield Basic Card's magnitude is expressed as a percentage of the active
  Pet's `MaxHP` (`CARD_RULES.md` §2). Read it from the provisioned
  `CardDefinition` row / the Card's documented effect; do not hardcode the
  resulting number, and do not restate the percentage in a comment
  (`AGENTS.md` §9, `tasks/README.md` §9).
- **Reuse `ResourceGenerator.ApplyHeal` / `ApplyPower`; do not re-implement
  the clamp or the cap.** `COMBAT_RULES.md` §4 item 1's heal clamp and
  `GAME_RULES.md` §12's Power cap are already enforced at those write sites
  (`PetState.Power`'s own doc: "The range is **not** enforced by this type").
  A second clamp would be a second representation of the same rule.
- **Do not let the cast borrow the Swap pipeline.** `CARD_RULES.md` §3 item 5
  is explicit that a cast is independent of the Turn/Combo system. Reusing
  the Swap resolution wholesale would wrongly increment `Turn` and fire a Boss
  Response. Note also that Shield is trigger-based, so a cast is not a
  resolution for which step 19a consumes anything
  (`GAME_STATE.md` §5.1.1 item 7).
- **The `CardCast` wire shape is already authored — apply it, do not design
  it.** `SIGNALR_PROTOCOL.md` §3.2.20 fixes `type` + `cardId`, item 2 records
  that MVP carries **no** Power cost member, §3.2.22 fixes the emission order,
  and §3.2.25 disposes of `effect summary`. Follow the existing projection
  arms' conventions (§3.2.3 camelCase, §3.2.4 enum as its name, §3.2.5
  omitted never `null`).
- **Check whether the Shield pool belongs on `PetState` or only in
  `ActiveStatusEffects`.** It belongs **only** in `ActiveStatusEffects`
  (`GAME_STATE.md` §2.3.1 item 3, `StatusEffectType.Shield`,
  `ShieldDepletedCondition`), and the pool is the instance's `Magnitude`
  (`StatusEffectLifecycle.ShieldPool` reads it). A `ShieldPoints` member would
  violate `GAME_STATE.md` §0 item 5. If a new state member genuinely proves
  necessary, that is a contract change → **STOP** and report.
- **Cite, do not restate.** Use the `<c>CARD_RULES.md</c> §3` citation idiom.
  Never copy a cost, an effect magnitude, or the Shield formula into a comment
  (`AGENTS.md` §9, `documentation-change.md` §2).
- **`cardId` is a CardDefinition id.** Confirm the exact wire form against
  `DATABASE.md` §1, the provisioned rows (`card-heal`, `card-shield`,
  `card-power-charge`), and how `PetState.EquippedCards[]` stores them
  (`EquippedCardIdentity`). Do not invent a second identifier.
- **`clientSequence` is an opaque correlation id** and is not the authoritative
  `Sequence` (`SIGNALR_PROTOCOL.md` §2 item 1) — the existing `Swap` method
  deliberately ignores it (`_ = clientSequence;`). Match that.
- **Rejection codes are owned per action.** `SIGNALR_PROTOCOL.md` §5 item 3
  says the domain document owns them and the protocol keeps no parallel list.
  The existing `SwapRejectionCodes` mapping is the precedent for spelling them;
  add the Card ones rather than inlining string literals.
- **PostgreSQL stays off the hot path** (`TDD.md` §4 item 3). The loadout is
  already snapshotted into `PetState.EquippedCards[]` at battle start
  (`ADR-011` item 4); a cast must read that snapshot and must not query the
  Card tables per cast.
- **Correct the `BattleHub.cs` L389–391 comment in the same change**
  (`AGENTS.md` §17) — it becomes false the moment `CardCast` lands. Leave the
  `GetBattleState` and `PetSkillCast` clauses accurate.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16) — e.g. the
  `BattleStateJson.cs` items TASK-097/TASK-098 reported; Tidal Barrier's
  unauthored Shield magnitude (TASK-104 B-4); the deferred Thanh Xà / Sơn
  Hùng Signature Skill content; and the `SIGNALR_PROTOCOL.md` "§3.3" and
  `BOSS_RULES.md` §4 citation nits.
- **Do not read this task as completing TASK-102.** TASK-102's own scope also
  covered `PetSkillCast` and the Pet Skill Cards. This task delivers its
  Basic-Card half; the remainder is a separate task.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the Domain Basic Card effect resolver: Heal (normal,
                         at-full-HP overheal, and a value that would exceed
                         MaxHP), Shield (first application, refresh replacing
                         a smaller/equal/larger magnitude, one instance only),
                         and Power Charge (normal, at the documented cap, and
                         at Cost 0 with zero Power). Plus the Heal clamp and
                         Power cap boundary values themselves.
[x] Integration tests  — the Application cast use case: validation (not in
                         loadout, insufficient Power, valid), rejected-cast
                         writes-nothing (persisted state unchanged), exact
                         Cost deduction, one write-back under the Sequence
                         compare-and-set, retry-on-mismatch, unknown battleId,
                         no Turn increment, no Combo interaction, and the
                         absence of Swap-only events.
[x] Api/hub tests      — the §5 acknowledgement shape for accept and reject,
                         caller-only delivery, `BattleStateUpdated` before
                         `ReceiveEvents`, and the `CardCast` wire projection
                         round trip (§3.2.20).
[x] Gameplay scenarios — Given/When/Then derived from CARD_RULES.md §3 and
                         COMBAT_RULES.md §4, e.g.:
                           Given the active Pet holds the Shield Basic Card in
                             its battle loadout and has Power ≥ its Cost
                           When the player casts it
                           Then exactly the Cost is deducted,
                           And one "Shield" StatusEffect instance is active
                             with the documented magnitude,
                           And a CardCast event is emitted,
                           And the Turn and Combo do not change.
                         And the end-to-end Shield analogue (the connection to
                         TASK-102):
                           Given an active Shield granted by that cast
                           When a damage instance is applied
                           Then the pool absorbs it before HP is affected,
                           And overflow reduces HP by exactly the remainder,
                           And the Shield is removed when the pool reaches 0.
```

### Key Edge Cases

- **A cast that exactly consumes the remaining Power** vs one Power short —
  the boundary of `CARD_RULES.md` §3 item 2's `current Power ≥ Card Cost`.
- **Power Charge at Cost 0 with zero Power** — a legal cast that must not be
  rejected by a naive `Power > 0` check (`CARD_RULES.md` §2 item 3).
- **Power capping** — Power Charge when Power is already at or near the
  documented maximum (`GAME_RULES.md` §12).
- **Heal at full HP** — overheal is discarded, not banked
  (`COMBAT_RULES.md` §4 item 1).
- **Heal that would exceed MaxHP** — clamped exactly to MaxHP.
- **Shield cast while a Shield is already active** — the existing instance is
  refreshed to the new magnitude; still exactly one instance; the pool is not
  summed (`COMBAT_RULES.md` §4 item 3).
- **A refresh whose new magnitude is smaller or equal** — the pool is set to
  the new magnitude deterministically (not maximized, not compared), and the
  equal case is idempotent (`COMBAT_RULES.md` §4 item 3).
- **Shield cast, then damage less than / exactly equal to / greater than the
  pool** — HP unchanged / HP unchanged and Shield removed / HP reduced by
  exactly the remainder (`COMBAT_RULES.md` §4 items 4–5).
- **A refresh in the same resolution that depleted the Shield** — the new
  application applies normally; a pool at 0 is not a representable committed
  state (`GAME_STATE.md` §2.3.1 item 8).
- **A rejected cast followed by an accepted one** — the rejection must leave no
  residue that changes the accepted cast's outcome.
- **A cast against an unknown/expired battle** — no battle is invented.
- **A cast while a concurrent Swap commits** — the compare-and-set refuses
  rather than overwriting (`REDIS_STATE.md` §4 items 2–3).
- **A cast of a Card whose `Category` is `PetSkill`** — rejected by the
  Basic-Card validation of this task (the Card is not one this path resolves);
  the Pet Skill path is a separate task and must not be half-implemented.

### Explicitly Not Tested Here

- `PetSkillCast` and any Pet Skill Card effect — out of scope (blocked by the
  TASK-104 B-4 content gap).
- Relic triggering on a cast — out of scope (`ROADMAP.md` Phase 1).
- Client-side cast UI, optimistic prediction, or rendering — no client work.
- Any StatusEffects wire delivery — `SIGNALR_PROTOCOL.md` §4.2 item 2 keeps it
  undelivered.
- Burn's per-Turn damage tick magnitude and schedule — an existing documented
  boundary, untouched here.

---

## Stop Conditions

Universal `AGENTS.md` §20 / `.ai/README.md` §13 stops always apply.
Task-specific:

1. **If the Shield absorption rule cannot be consumed without changing a
   documented contract** — e.g. a new `PetState` member proves necessary, or
   `ApplyShield`'s existing semantics do not fit — **STOP per `AGENTS.md`
   §4/§17** and report the contract gap rather than adding a parallel
   representation (`GAME_STATE.md` §0 item 5).
2. **If satisfying any criterion would require modifying the Shield contract**
   in `COMBAT_RULES.md` §4, or `StatusEffectLifecycle.ApplyShield` /
   `RemoveDepletedShield` / `ShieldPool`, or `DamagePipeline`'s absorption
   step — **STOP**; Shield is resolved and frozen (TASK-105 / TASK-102).
3. **If any Basic Card's Cost or Effect is missing or ambiguous** for a Card
   this task must implement — **STOP per `AGENTS.md` §7.** The three §2 Basic
   Cards are fully specified; if one is found not to be, report it.
4. **If the Shield Basic Card's magnitude cannot be derived from the
   documented Card content without choosing a value** — **STOP.** The
   magnitude is `CARD_RULES.md` §2's; no value is invented.
5. **If implementing the cast appears to require implementing any Pet Skill
   Card, `PetSkillCast`, or Tidal Barrier's Shield magnitude** — **STOP**;
   that is the separate task, and Tidal Barrier's magnitude is an unresolved
   content decision (TASK-104 B-4, `CARD_RULES.md` §4.1).
6. **If implementing a cast would require running the §17 Swap pipeline, a
   Boss Response, or a Turn increment** — **STOP**; `CARD_RULES.md` §3 item 5
   forbids it.
7. **If implementing a cast would require Relic trigger evaluation** — **STOP
   and report**; `CARD_RULES.md` §3 item 4's Relic step is Phase 2 scope
   (`ROADMAP.md` Phase 1 — "No Relics yet").
8. **If the `CardCast` wire shape proves not to be derivable from
   `SIGNALR_PROTOCOL.md` §3.2.20 and `GAME_EVENTS.md` §2 without inventing a
   member** — **STOP and report**; a protocol contract decision is not this
   task's to make. (Verified at discovery: §3.2.20/§3.2.22 already fix it.)
9. **If the implementation would require a new SignalR method beyond §2's
   three, a new Redis key, a new API endpoint, or a schema/migration change**
   — **STOP**; `SIGNALR_PROTOCOL.md` §8 item 7 and the phase scope forbid it.
10. **If Player identity / session context is required by a cast but is not
    available on the existing `Swap` path** — **STOP and report.** Do not
    invent JWT/session behavior; the existing authenticated-session boundary
    (`ADR-015`, `SIGNALR_PROTOCOL.md` §1) is the only mechanism.
11. **If two authoritative documents conflict about a Card's or Shield's
    behaviour** — **STOP per `AGENTS.md` §4** and report both sources rather
    than choosing one.
12. **If the task would exceed 7 skills or cross multiple uncoupled
    architectural boundaries** — **STOP and decompose** (`tasks/README.md`
    §13) — do not expand it.
13. **If satisfying any criterion requires modifying TASK-102, TASK-103,
    TASK-104, TASK-105, TASK-106, TASK-036, TASK-079, TASK-099, or any
    `tasks/completed/*` file** — **STOP**; report the stale statement instead
    (`TASK_LIFECYCLE.md` §3 — completed tasks are immutable).

When stopped, report the exact condition and **do not invent a resolution**.

---

## Readiness Pre-Check (for the lifecycle validation that follows this task)

This task is `BACKLOG` per `tasks/README.md` §6. The discovery pass that
authored it verified the `TASK_LIFECYCLE.md` §3 BACKLOG → READY criteria as
follows, so the validating reviewer can confirm rather than rediscover:

```text
[x] Task type confirmed (TASK_TYPES.md)   FEATURE — a documented mechanic
                                          (Basic Card cast) with a home in
                                          docs/ that is not built. Confirmed
                                          still FEATURE, not GAMEPLAY-CHANGE:
                                          no rule changes, because CARD_RULES
                                          §2–§3, COMBAT_RULES §4, GAME_RULES
                                          §11/§12/§17, SIGNALR_PROTOCOL §2/§5
                                          already author every behavior.
[x] Relevant documentation exists in docs/ CARD_RULES.md §1–§3/§6,
                                          COMBAT_RULES.md §4, GAME_RULES.md
                                          §11/§12/§16–§18, SIGNALR_PROTOCOL.md
                                          §2/§3.2.20/§3.2.22/§3.2.25/§5,
                                          GAME_EVENTS.md §1–§3,
                                          GAME_STATE.md §2.3/§2.3.1/§5.1,
                                          REDIS_STATE.md §4/§7 — all present
                                          and authoritative.
[x] MVP scope confirmed (MVP_SCOPE.md §1) Cards (3 Basic) and Combat/Status
                                          Effects are IN; no OUT item reached.
[x] Not blocked by an unresolved dep.     The Shield Basic Card's magnitude is
                                          authored (CARD_RULES.md §2). The one
                                          open content gap — Tidal Barrier's
                                          magnitude — belongs to the EXCLUDED
                                          Pet Skill half, so it does not block
                                          this task.
[x] Primary agent assigned                gameplay; supporting: backend,
                                          realtime, testing, review.
[x] Workflow assigned                     development/feature.md
[x] Acceptance criteria are testable      All binary; each names the owning doc
                                          section or a proving assertion.
```

**Known open item carried forward, not resolved here:** Tidal Barrier's Shield
magnitude (`CARD_RULES.md` §4.1) remains unauthored per TASK-104 §5 / B-4. It
blocks the **Pet Skill** half only. A follow-up content decision is required
before a `PetSkillCast` task can be authored.

---

## Reviewer's Checklist

For the reviewer validating this task before it ascends to `READY`:

- [ ] Every acceptance criterion is binary and cites its owning document
      section or a concrete assertion.
- [ ] No acceptance criterion requires inventing a rule, cost, magnitude, or
      event member.
- [ ] The Shield preservation criteria match `COMBAT_RULES.md` §4's frozen
      contract exactly (one instance, refresh replaces, no stacking,
      absorption before HP, overflow by remainder, `ShieldDepleted` not an
      event).
- [ ] The in-scope/out-of-scope boundary correctly separates Basic Cards
      (`CARD_RULES.md` §2) from Pet Skill Cards (§4/§4.1), and states the
      reason.
- [ ] No criterion is duplicated from, or contradictory to, TASK-102's own
      acceptance criteria.
- [ ] Skill count is within the Complex ceiling (7,
      `tasks/README.md` §12).
- [ ] No source-of-truth rule is restated in this file
      (`tasks/README.md` §9).

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

**Picker-up note.** Record before editing: (a) the SHA256 of `BattleHub.cs`,
`BattleEvent.cs`, `BattleEventWireProjection.cs`, `StatusEffectLifecycle.cs`,
and `DamagePipeline.cs` as the change-isolation baseline; (b) the output of
`git status`; (c) the pre-edit `dotnet test` counts for the Domain,
Application, Infrastructure, and Api projects. The working tree carries
uncommitted changes from earlier tasks, so the baseline is the working-tree
content at pickup, **not** `HEAD`. Note that TASK-102 left
`DamagePipeline.cs`, `DamageEvents.cs`, and `StatusEffectLifecycle.cs`
modified/new relative to `HEAD`.

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<test command or suite>` — PASS (<N> tests)

### Contract Preservation Verification

```text
Card rules             UNCHANGED (CARD_RULES.md §1–§3 implemented, not edited)
Shield contract        UNCHANGED (COMBAT_RULES.md §4 byte-identical; TASK-105's
                         refresh/one-instance/no-accumulation/depletion intact)
Shield representation  UNCHANGED (existing StatusEffect; ApplyShield called,
                         not modified)
Damage Pipeline        UNCHANGED (TASK-102's absorption step consumed, not
                         modified; steps 1–6 untouched)
BattleState schema     UNCHANGED (no new member)
PetState               UNCHANGED (Power/HP/EquippedCards/StatusEffects reused)
Redis                  UNCHANGED (same key, same compare-and-set)
SignalR                UNCHANGED protocol; the §2 CardCast method added and
                         the §3.2.20 CardCast event projected
API                    UNCHANGED
PostgreSQL             UNCHANGED (definitions read, never written)
Tidal Barrier / Thanh Xà / Sơn Hùng   UNCHANGED (not implemented, not invented)
docs/                  UNCHANGED (0 files modified)
docs/03-decisions/     UNCHANGED (0 ADRs)
TASK-102/103/104/105/106, TASK-036/079/099, tasks/completed/   UNCHANGED
```

### Server Authority & Scope Verification
- [ ] Confirmed zero client-authoritative gameplay logic
- [ ] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [ ] Confirmed no Relic, no Turn consumption, no Boss Response on a cast
- [ ] Confirmed no Shield state field, no Shield event, no Shield stacking
- [ ] Confirmed no invented content value (Tidal Barrier / Thanh Xà / Sơn Hùng
      remain deferred)

### Unrelated Stale Documentation Discovered (REPORTED, NOT CHANGED)
- <item, location, impact, suggested follow-up task — per `AGENTS.md` §16>
