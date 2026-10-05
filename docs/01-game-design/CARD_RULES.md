# Card Rules

**Version:** 1.9 (§3 — the Card-cast restriction approved by `TASK-191` Q-4
(OPTION B) is recorded: **a player may successfully cast at most one Card during
each committed Match-3 Turn** (new §3 item 6; former item 6 promoted to item 7).
This is a **cast-count constraint, not Turn consumption** — §3 item 5's rule that
a Card cast consumes no Turn and does not interact with Combo is **unchanged and
explicitly preserved**, and item 6 states that a cast does not resolve the
Match-3 board and does not by itself trigger the boss response. The Match-3 Turn
remains the authoritative unit of combat progression, and the boss response
still arrives through `GAME_RULES.md` §17 step 18 on the committed Swap. No Card
cost, effect, magnitude, column, or category is changed — Power Charge's `0`
cost (§2 item 3) is explicitly retained. Rationale: ADR-021. Prior 1.8: (§4.1 — the two remaining MVP Pet Skill Cards are now
content-defined per the TASK-166 Product Owner decisions D-1/D-2: **Thanh Xà —
Venomous Bloom** (80 Power; 80 flat Mộc damage → Boss; Burn 25/tick for 2 Turns,
Element Hỏa) and **Sơn Hùng — Earthshaker** (100 Power; 150 flat Thổ damage →
Boss). Both resolve immediately at `GAME_RULES.md` §17 step 14 and introduce no
cooldown, resource, pending state, event, or `EffectDefinition` member. §4.1
gains a statement that each effect carries its own Element and an
immediate-resolution statement covering all four Pet Skill Cards; the closing
"not yet content-defined" sentence is retired because all five Pets' Skills are
now authored. **No existing Skill, magnitude, cost, or column is changed** —
Inferno, Tidal Barrier, and Iron Fang are untouched, and `DATABASE.md` §1's
storage shape and vocabulary are unchanged. Prior 1.7: (§3.6 added — the canonical owner of **Effective Card Cost**,
per TASK-134 D4/D5 and the explicit Product Owner `EffectiveCardCost`
fractional-value decision: `TotalReduction = min(sum(CostReductionPercentage),
100)`, `RawEffectiveCardCost = CardDefinition.PowerCost × (100 − TotalReduction)
/ 100`, `EffectiveCardCost = truncate(RawEffectiveCardCost)` toward zero, an
integer with no minimum-cost rule; `CardDefinition.PowerCost` remains the
authored/base value and is never mutated. §3's items 1–6 are unchanged except
for a clarifying sentence that "Card Cost" in items 2 and 4 means
`EffectiveCardCost`; §3 item 2's validation and item 4's deduction now have a
definition of the value they read, and the value is used consistently for
validation, deduction, and `CardCast` reporting. `EffectiveCardCost` is a
derived runtime value — not stored state and not a wire member. The composition
rule is `CardCost`-specific and defines no stacking for any other Relic
`effectType`. §5 gains a cost-modification boundary sentence (declaration is
`RELIC_RULES.md`'s, the calculation is this document's). No authored Card cost,
magnitude, effect, or column is changed. Prior 1.6: §4.1 — Iron Fang's `NextAttack` Crit scope now points at its
canonical owner instead of being left as prose: `COMBAT_RULES.md` §3.3 items
7–10 own the modifier's lifetime, the qualifying-attack consumption boundary,
the composition, and source-specific removal, and `GAME_STATE.md` §2.3.4 owns
the state it is held in. §4.1's magnitudes, costs, and the Iron Fang × Bạch Hổ
independence statement are unchanged; the `COMBAT_RULES.md` §3.3 cross-reference
in that statement was corrected from item 3 to item 5 (Modifier Sources). The
detail is referenced, not restated (documentation-change.md §2). Prior 1.5: §4.1 — the three MVP Pet Skill Card effect magnitudes are
authored per TASK-110 Product Owner decisions D-1…D-6: Inferno 100 flat Fire
damage + Burn 50/tick for 2 Turns; Tidal Barrier Heal 20% Max HP + Shield
20% Max HP (refresh-not-stack); Iron Fang 120 flat damage + Crit +10
percentage points for the next attack only, independent of Bạch Hổ's Passive
config value. Prior 1.4: §1 item 5 — MVP `LoadoutCopyLimit` values recorded per
TASK-082 decisions C / R1-5 / R2-6: the value is **1** for every
CardDefinition defined by this document (§2 Basic Cards and §4.1 Pet
Skill Cards); prior 1.3: §1 loadout copy limit — each CardDefinition's explicit
`LoadoutCopyLimit` governs how many times it may appear in the submitted
3-card Basic loadout; explicit value required, no default; concrete
values are content/balance configuration; prior 1.2: §1 ownership
clarified — Player owns the Card collection; loadout is battle-scoped
for the active Pet; no `Pet.CardInventory`; prior 1.1: Basic Card
effects and Power cost clarify target = active Pet / PetState)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §11 (Card Rules). Conflicts resolve in favor of
GAME_RULES.md.

---

# 1. Card Categories

```text
Basic Card       shared across all Pets
Pet Skill Card    tied to the active Pet's Signature Skill (exactly one per Pet)
```

A battle loadout always contains exactly 3 Basic Cards + 1 Pet Skill Card
(GDD §3), for 4 total Cards available to cast.

**Ownership vs. loadout.** The Player owns the Card *collection*
(unlock rows in `DATABASE.md` §2 — `PlayerUnlockedCard`, ADR-012). The
battle loadout is selected at `POST /api/battle/start` for the one active
Pet and is snapshotted into `PetState.EquippedCards[]`
(`GAME_STATE.md` §2.3) for that battle only. There is **no**
`Pet.CardInventory` and no persistent per-Pet Card ownership: Cards are
owned by the Player, equipped per battle for the active Pet.

**Loadout copy limit (per CardDefinition).** The number of times one
Basic `CardDefinition` may appear in the submitted 3-card loadout is
governed by that CardDefinition's **loadout copy limit**
(`LoadoutCopyLimit`, `DATABASE.md` §1):

```text
For each CardDefinitionId in cardLoadout:
    occurrence count ≤  that CardDefinition's LoadoutCopyLimit
```

1. Duplicates are permitted up to the limit; a limit of 1 makes a
   Basic Card loadout-unique. `cardLoadout = [A, A, B]` is valid only
   if `LoadoutCopyLimit(A) ≥ 2`, and each entry independently satisfies
   this section's count (exactly 3), category (all `Category = Basic`),
   and ownership (unlocked `PlayerUnlockedCard` row) rules.
2. Every CardDefinition must define its limit explicitly; a missing
   value is invalid definition data. There is no default.
3. The limit counts occurrences in **one submitted battle loadout**.
   It is not inventory quantity, ownership quantity, or a collection
   limit: ownership remains one unlock row per (`Player`,
   `CardDefinition`) regardless of allowed copies (`PlayerUnlockedCard`,
   ADR-012 item 9), and no Card instance exists at any time.
4. The limit is read only for the submitted Basic loadout. The derived
   Signature Skill Card (`PetDefinition.SignatureSkillCardId`) is not
   submitted, never counted, and keeps its own composition slot (the
   loadout remains 3 Basic + 1 Pet Skill); a `PetSkill` CardDefinition
   can never satisfy this section's Basic-Card composition rule.
5. Concrete MVP limit values are recorded here (TASK-082 decisions C,
   R1-5, R2-6): **`LoadoutCopyLimit` = 1 for every CardDefinition
   defined by this document** — the §2 Basic Cards and the §4.1 Pet
   Skill Cards. For a `PetSkill` row the value is persisted only
   because the database column is required and non-nullable; it is
   never read for the submitted Basic loadout (item 4), so it
   introduces no additional gameplay behavior.

---

# 2. Basic Cards (MVP)

```text
Heal
  Cost:   20 Power
  Effect: Restore the active Pet's HP by 20% of its Max HP

Shield
  Cost:   20 Power
  Effect: Active Pet gains Shield equal to 20% of its Max HP

Power Charge
  Cost:   0 Power
  Effect: Active Pet gains 25 Power
```

Basic Card HP/Shield/Power effects target the **active Pet** (the combat
character, `PetState`) — not a separate Player entity.

Rules:

1. Basic Cards are identical for every Pet; no Pet-specific variation in MVP.
2. Basic Cards have no Element (ELEMENT_RULES.md §1.1) and resolve at Neutral
   modifier if they ever deal damage (none currently do).
3. "Power Charge" costs 0 Power by design — it exists as a resource-recovery
   option and must never be blocked by insufficient Power.

---

# 3. Casting Rules

1. A Card cast is an explicit player action, distinct from a Swap
   (GAME_RULES.md §11.1, §11.4).
2. On cast request, the server validates:
   * The Card is in the active Pet's battle loadout for this battle.
   * The active Pet has sufficient Power (`current Power ≥ Card Cost`).
   * Any additional Card-specific preconditions (none in MVP Basic Cards).
3. If validation fails, the cast is rejected and no state changes occur (no
   Power spent, no Effect applied, no Event emitted beyond a rejection
   response to the client).
4. If validation succeeds:
   ```text
   Deduct Cost from Power
        ↓
   Apply Effect
        ↓
   Emit CardCast event (and PetSkillCast, if applicable)
        ↓
   Allow Effect to trigger downstream Relics (RELIC_RULES.md §3, OnCardCast)
   ```
5. Casting a Card does NOT consume a Turn and does NOT interact with Combo —
   it is independent of the Match-3 Turn/Combo system (GAME_RULES.md §2, §5)
   unless a specific Card/Relic explicitly says otherwise. A cast does NOT
   resolve the Match-3 board and does NOT trigger the boss response on its own.
6. **A player may successfully cast at most one Card during each committed
   Match-3 Turn.** This is a **cast-count constraint, not Turn consumption**:
   the cast still consumes no Turn (item 5), and the Match-3 Turn remains the
   authoritative unit of combat progression (MATCH3_RULES.md §8.1). After one
   successful cast, any further cast request is rejected (item 3) until the next
   committed Match-3 Turn begins. The lifecycle is:

   ```text
   New committed Match-3 Turn
        ↓
   Card cast available
        ↓
   Card successfully cast
        ↓
   Card cast unavailable for the remainder of that Turn
        ↓
   Match-3 Turn resolves — Turn++, boss response
        ↓
   Next Turn — Card cast available again
   ```

   The limit applies per **committed** Match-3 Turn. A rejected Swap begins no
   Turn (MATCH3_RULES.md §2.1.5 item 2), so it neither restores nor consumes the
   allowance. The constraint is enforced by the server at cast validation
   (item 2); the client only reflects the server's result.
7. Card casts are never authoritative from the client; the client sends a
   cast *request*, and the server computes and emits the actual result
   (GAME_RULES.md §18).

**"Card Cost" in items 2 and 4 is the Effective Card Cost**, defined in §3.6 —
not the authored `CardDefinition.PowerCost` on its own. §3.6 owns that value's
composition, its integer/truncation semantics, and the single point at which it
is calculated.

## 3.6 Effective Card Cost (Canonical Owner)

**Canonical owner.** This subsection owns **"what a Card cast actually costs"**
— the runtime, composed cost that §3 items 2 and 4 read and that `CardCast`
reports. It is the owner of the `EffectiveCardCost` term. `RELIC_RULES.md`
declares the Relic effect that can modify a Card's cost (§8.2–§8.5) and
`GAME_STATE.md` §2.3.5 owns the state the applied modifier is held in; neither
restates this calculation
(`.ai/workflow/documentation/documentation-change.md` §2).

**Decided** — Product Owner decision **D4** and **D5** (TASK-134) plus the
explicit `EffectiveCardCost` fractional-value decision.

```text
CardDefinition.PowerCost          the authored/base value
                                  (DATABASE.md §1 — never mutated)
        ↓
PetState.CardCostModifiers[]      every applied, Battle-scoped CardCost
                                  modifier currently active on the active Pet
                                  (GAME_STATE.md §2.3.5)
        ↓
TotalReduction = min( sum(CostReductionPercentage), 100 )
        ↓
RawEffectiveCardCost =
        CardDefinition.PowerCost × (100 − TotalReduction) / 100
        ↓
EffectiveCardCost = truncate(RawEffectiveCardCost)
        ↓  truncate toward zero; the result is an integer
        ↓
validation → deduction → CardCast reporting
```

1. **`CardDefinition.PowerCost` is the authored/base value and is never
   mutated.** It is the content-side definition value (`DATABASE.md` §1,
   `CARD_RULES.md` §2/§4.1), and it remains exactly what its owning content
   section authored. No modifier writes to it, no resolution rewrites it, and
   no new column or definition member is introduced for the runtime result. It
   is the **input** to the calculation below, not the cost of a cast.
2. **`EffectiveCardCost` is a runtime value, not stored state.**
   `PetState.CardCostModifiers[]` stores the *modifiers*; the composed cost is
   **derived** for the cast being resolved and is not itself a `PetState`
   member, a `BattleState` member, a Redis field, or a wire member. There is
   exactly one representation of a Card's cost and exactly one of each
   modifier — no second spelling of either (`GAME_STATE.md` §0 item 5).
3. **`TotalReduction` is additive and capped at 100%.** Multiple
   simultaneously-active `CardCost` modifiers compose by **adding** their
   `CostReductionPercentage` values; the total is capped at `100`. The cap
   bounds the composed percentage — it does not change the formula below, and
   it means `RawEffectiveCardCost` is never negative and `EffectiveCardCost`
   is never below `0`.

   ```text
   TotalReduction = min( sum(CostReductionPercentage), 100 )

   Emergency Core alone        50          → TotalReduction = 50
   two independent 50% sources 50 + 50     → capped to 100
   ```

   This composition rule is **`CardCost`-specific**. It does not define
   stacking, scaling, or interaction for any other Relic `effectType`
   (`ATK`, `Power`, `Crit`, …) — those remain undefined and are a future rule
   change (`RELIC_RULES.md` §2.4 item 6, `GAME_RULES.md` §20).
4. **The formula, and the truncation rule.**

   ```text
   RawEffectiveCardCost = CardDefinition.PowerCost × (100 − TotalReduction) / 100

   EffectiveCardCost    = truncate(RawEffectiveCardCost)
   ```

   `EffectiveCardCost` is an **integer**. A fractional
   `RawEffectiveCardCost` is **truncated toward zero** — the direction rule is
   fixed by the Product Owner decision and is not `floor`, `ceil`,
   round-half-up, or banker's rounding.

   ```text
   PowerCost = 15, Reduction =  50%  →  Raw = 7.5  →  EffectiveCardCost =  7
   PowerCost = 25, Reduction =  50%  →  Raw = 12.5 →  EffectiveCardCost = 12
   PowerCost = 15, Reduction = 100%  →  Raw = 0    →  EffectiveCardCost =  0
   PowerCost = 20, Reduction =  50%  →  Raw = 10   →  EffectiveCardCost = 10
   ```

5. **There is no minimum-cost rule, and cost `0` is a real cost.** No
   "minimum cost of 1" floor exists: a `100%` total reduction yields
   `EffectiveCardCost = 0`, and a cost of `0` is validated and deducted as
   `0`. This is consistent with `CARD_RULES.md` §2's Power Charge, which
   already costs `0` Power by design, and with §2 item 3's rule that a
   `0`-cost Card "must never be blocked by insufficient Power".
6. **`Power` remains an integer-valued resource.** The truncation keeps the
   cost integral because `PetState.Power` is integral (`COMBAT_RULES.md` §1.1,
   `GAME_RULES.md` §12, `GAME_STATE.md` §2.3). No decimal Power, no decimal
   Card cost, and no fractional resource pool is introduced anywhere. The
   truncation is defined **for `EffectiveCardCost` only** and generalizes to
   no other calculation — it is not a new global rounding convention, and it
   changes no damage, stat-composition, or progression rule
   (`COMBAT_RULES.md` §3 step 6, §5.4.2, §5.5.1, §7 are untouched).
7. **When `EffectiveCardCost` is calculated — before validation, deduction,
   and reporting.** One calculation, at the start of the cast resolution, and
   the same value is used by all three consumers below. It is not recomputed
   between them, so a cast cannot be validated against one cost and charged
   another.

   ```text
   EffectiveCardCost calculated ONCE, before all three
        ↓
   1. validation   §3 item 2 — the check is
                   current Power ≥ EffectiveCardCost
        ↓
   2. deduction    §3 item 4 — the amount removed from PetState.Power is
                   exactly EffectiveCardCost
        ↓
   3. reporting    CardCast reports the cost actually deducted, which is
                   EffectiveCardCost
   ```

   - **Validation reads it.** §3 item 2's "`current Power ≥ Card Cost`" is
     evaluated against `EffectiveCardCost`, compared with the active Pet's
     current `PetState.Power`.
   - **Deduction reads it.** §3 item 4's "Deduct Cost from Power" removes
     exactly `EffectiveCardCost` from `PetState.Power`. The resulting Power is
     the value delivered by the `BattleState` push (§6 below).
   - **Reporting reads it.** `CardCast`'s cost is the effective cost that was
     actually deducted (`GAME_EVENTS.md` §2). On the wire, MVP `CardCast`
     carries no cost member at all (`SIGNALR_PROTOCOL.md` §3.2.20 item 2) and
     the authoritative record of the resulting `Power` is the state push and
     `PowerChanged` (`SIGNALR_PROTOCOL.md` §3.2.24) — this subsection does not
     add a wire member or change that event's shape.
8. **Activity is read from the committed state at cast resolution.** Which
   modifiers participate is decided by the committed
   `PetState.CardCostModifiers[]` contents at the moment the cast resolves
   (`GAME_STATE.md` §5.1.3) — not by a re-evaluation performed by the Card
   path, and not by anything the client supplies. The Card path reads the
   collection; it does not create, refresh, or remove an entry
   (`AGENTS.md` §12 — Card cost is a Card concern, the Relic side declares the
   effect).
9. **This subsection adds no gameplay rule beyond the decided ones.** It
   authors no Card, no Relic, no effect magnitude, no threshold, and no new
   cost-modifying source. `CardDefinition.PowerCost`'s authored values
   (`§2`, `§4.1`) are unchanged, no Card content is re-encoded, and no
   database column is added.

---

# 4. Pet Skill Card (Signature Skill)

1. Each Pet has exactly one Signature Skill, expressed as one Pet Skill Card
   (GAME_RULES.md §9.5).
2. The Pet Skill Card is only available while that Pet is the active Pet for
   the battle (PET_RULES.md §2.3).
3. Pet Skill Cards generally cost more Power than Basic Cards (see examples
   below), reflecting the "spend now vs. save for Skill" tension (GDD §10).
4. Pet Skill Cards may carry an Element (inherited from the Pet, or
   explicitly stated) if they deal damage, and go through the full Damage
   Pipeline including Element Modifier (COMBAT_RULES.md §3, ELEMENT_RULES.md §5).

## 4.1 MVP Pet Skill Examples

```text
Xích Lang — Inferno
  Cost:   100 Power
  Effect: Deal 100 Fire (Hỏa) damage (flat base value, entering the Damage
          Pipeline as the Card/Skill base value — COMBAT_RULES.md §3 step 1);
          apply Burn
  Burn:   50 damage per tick for 2 Turns (fixed; ticks once per resolved Turn
          at End Turn — GAME_RULES.md §17 step 19a, COMBAT_RULES.md §5.1/§5.2)

Huyền Quy — Tidal Barrier
  Cost:   80 Power
  Effect: Heal the active Pet for 20% of its Max HP; the active Pet gains
          Shield equal to 20% of its Max HP (refresh-not-stack —
          COMBAT_RULES.md §4)

Bạch Hổ — Iron Fang
  Cost:   100 Power
  Effect: Deal 120 damage (flat base value, entering the Damage Pipeline as
          the Card/Skill base value — COMBAT_RULES.md §3 step 1); increase
          Crit chance by 10 percentage points for the next attack only

Thanh Xà — Venomous Bloom
  Cost:   80 Power
  Effect: Deal 80 Mộc (Wood) damage (flat base value, entering the Damage
          Pipeline as the Card/Skill base value — COMBAT_RULES.md §3 step 1);
          apply Burn 25 damage per tick for 2 Turns, Element Hỏa (Fire)

Sơn Hùng — Earthshaker
  Cost:   100 Power
  Effect: Deal 150 Thổ (Earth) damage (flat base value, entering the Damage
          Pipeline as the Card/Skill base value — COMBAT_RULES.md §3 step 1)
```

Effect magnitudes above are the Product Owner's decided values and are owned
by this section. Burn ticks and Shield application follow the frozen contracts
cited inline; neither is restated here.

**Each effect carries its own Element.** A damage-dealing Pet Skill Card's
Element is stated per effect, and an effect's Element is what the Damage
Pipeline's Element Modifier step reads for that damage instance
(`ELEMENT_RULES.md` §1.1, §5; `COMBAT_RULES.md` §3 step 3). Venomous Bloom's two
effects therefore carry **different** Elements: its damage is Mộc (the Pet's own
Element, `ELEMENT_RULES.md` §6) while its Burn is Hỏa — the Element
`COMBAT_RULES.md` §5.1 assigns to Burn generally. The Skill's Mộc Element is
**not** inherited by its Burn effect, and Earthshaker's single effect carries
Thổ (`ELEMENT_RULES.md` §6). This states which Element each authored effect
carries; it changes no Element rule, which `ELEMENT_RULES.md` owns.

**All four §4.1 Pet Skill Cards resolve at the same point.** A Pet Skill's
effects resolve when the player casts it, at `GAME_RULES.md` §17 step 14
("Resolve Player Effects"), and emit the existing `PetSkillCast` event
(`§6` below; `GAME_EVENTS.md` §2). **Venomous Bloom and Earthshaker are
immediate**: once the cast resolves, nothing of the Skill persists as pending or
active state, and Burn's lifetime is the applied Status Effect's own, governed
by `COMBAT_RULES.md` §5.1–§5.3 and the single `GAME_RULES.md` §17 step 19a
duration decrement. No Pet Skill Card defined by this document has a cooldown,
consumes a resource other than `Power`, or introduces an event, and none adds a
member to `EffectDefinition` (`DATABASE.md` §1).

**Iron Fang's Crit increase is the Card's own value.** It is **independent**
of Bạch Hổ's Pet Passive configuration value (`PASSIVE_RULES.md` §7/§8) — the
two are separate sources that both modify Crit chance
(`COMBAT_RULES.md` §3.3 item 5) and neither is derived from, nor shared with,
the other.

**The Crit increase is scoped to the next attack, and that rule is not owned
here.** Iron Fang's `Crit` element is stored with `scope = "NextAttack"`
(`DATABASE.md` §3 item 1), and what that scope means in play — when the
modifier stops applying, which attack consumes it, and how it composes with
the other Crit sources — is owned by **`COMBAT_RULES.md` §3.3 items 7–10**.
It is not restated here
(`.ai/workflow/documentation/documentation-change.md` §2). The state it is
held in is `PetState.NextAttackCritModifiers[]` (`GAME_STATE.md` §2.3.4).

This Card's cast is a resolution site for that modifier, not a rule about it:
the Card applies its Crit element when the cast resolves at `GAME_RULES.md`
§17 step 14 ("Resolve Player Effects"), and the modifier then persists until a
qualifying owner attack consumes it (`COMBAT_RULES.md` §3.3 item 8). That the
cast's own damage and the modifier's consumption can occur in the same Swap
is a consequence of `GAME_RULES.md` §17's fixed order, not a separate rule
here.

Thanh Xà and Sơn Hùng Signature Skills are now content-defined above
(**Venomous Bloom** and **Earthshaker**); all five MVP Pets' Signature Skills
are authored in this section.

---

# 5. Cards vs. Relics

Per GAME_RULES.md §13.2 and GDD §9: Cards are **active** — the player chooses
when to cast them. Relics are **passive** — they react automatically to
events and are never directly cast by the player. A system must never blur
this line (e.g. a "Relic" that requires manual activation should instead be
modeled as a Card).

**Cost modification does not blur the line.** A Relic may modify a Card's cost
(`RELIC_RULES.md` §8.3's `CardCost` effect type, e.g. Emergency Core's
`-50%`). The boundary is unchanged and the ownership is split:

```text
Relic declares the effect          RELIC_RULES.md §8.2–§8.5
                                   (effectType/valueType/value/target/lifetime)
applied modifier is held in        GAME_STATE.md §2.3.5
                                   (PetState.CardCostModifiers[] — state)
Card cost is composed and read     CARD_RULES.md §3.6 — THIS document
                                   (TotalReduction → EffectiveCardCost)
```

A Relic never casts, and a Card never evaluates a Relic Trigger or Condition.
Reading `PetState.CardCostModifiers[]` at cast time is a Card-side *read* of
authoritative state: the Card path creates, refreshes, and removes no entry in
that collection (`AGENTS.md` §12, `GAME_STATE.md` §5.1.3).

---

# 6. Events

```text
CardCast        emitted for every successful Basic Card or Pet Skill Card cast
PetSkillCast    emitted specifically when the cast Card is the active Pet's
                 Signature Skill (in addition to CardCast)
```

Both are part of the Battle Event Model (GAME_RULES.md §16) and fire at the
point shown in the Event Resolution Rules (GAME_RULES.md §17, step 14
"Resolve Player Effects").
