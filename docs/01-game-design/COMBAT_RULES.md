# Combat Rules

**Version:** 2.4 (§4 item 7, §5.4.5, and §5.5.3 reconciled per TASK-155, applying
the TASK-154 Product Owner decision (Option B): an applicable Heal modifier may
be **held on the Boss**, and the Pet-scoped Heal Resolution step is **explicitly
authorized** to perform a **cross-entity read** of `BossState.StatusEffects[]` at
its Applicable Heal Modifiers stage, one-directionally and non-mutating, selecting
the applicable instance by its Status Effect `Id` (its value owned by
`BOSS_RULES.md` §6.2.2, referenced not restated). §4 item 7's **"Scope — Pet HP
only" clause is UNCHANGED and is NOT widened** — the authorization is a read, not
a scope change; a Boss-side HP change still does not route through the step, and
Option C (a target-aware/target-agnostic Heal Resolution) was not selected.
§5.4.5 and §5.5.3 each record that Thủy Ma's healing reduction is **not** a
`TargetStat`-consumed `BuffDebuff` and opens **no** new non-`"ATK"` `TargetStat`
case; their "any other stat would require its own recorded decision" boundary is
preserved in meaning and is not exercised. §4 item 7's reading order, §4 item 1's
clamp position (still last), and the MaxHP/Shield non-effects and
no-elemental-interaction statements are unchanged. No new `TargetStat` value, no
Battle Event, SignalR member, `BattleState` member, Redis key, or database column
is introduced, and no modifier's magnitude, source, duration, or activity window
is authored here. Decision source: TASK-154; applying task: TASK-155. Prior 2.3: (§5.4.1 / §5.6.5 / §5.6.6 reconciled — the Pet ATK modifier
composition contract is now unified under §5.6 / §5.6.6 per the TASK-138
Product-Owner decision set D1–D6. §5.6.6 is the canonical composition model
for all applicable Pet ATK modifiers, including when only Turn-based `BuffDebuff`
modifiers are active (D1, D2). §5.4.1's independent absolute-value ATK composition
formula is superseded/narrowed and no longer defines a separate calculation path (D2, D4);
§5.4 retains the Turn-based BuffDebuff StatusEffect consumption and lifetime contract (D2).
A BuffDebuff ATK reduction contributes -|Magnitude| signed percentage points to
TotalATKModifierPercentage, and an increase contributes +|Magnitude| signed percentage
points (D3); the stored StatusEffect Magnitude remains unchanged and does not encode sign (D3).
One unified composition calculation produces EffectivePetATK for all applicable Pet ATK
modifiers, applying the combined signed percentage to permanent base PetState.ATK and
truncating toward zero exactly once after the combined calculation, with no intermediate
per-modifier truncation (D4, D5). PetState.ATK remains permanent/base, never overwritten,
mutated, restored, or reset; EffectivePetATK remains derived at calculation time and is
never stored (D6). No new ATK cap is introduced (§5.6.1 item 5, §1.1). Resolves the
TASK-139 contract application of the TASK-138 decision. Prior 2.2: (§5.6.6 resolved — the interaction between a Relic
Battle-lifetime ATK modifier and a Turn-based `BuffDebuff` `TargetStat = "ATK"`
modifier is now authored at its canonical owner per the TASK-137 Product-Owner
decision set D1–D7. Both modifier classes coexist (D1) and compose
order-independently as a single signed percentage adjustment against permanent
Base Pet ATK (D2, D3), truncated toward zero exactly once (D4) to produce
integer `EffectivePetATK = truncate(PetState.ATK × (100 + TotalATKModifierPercentage) / 100)`,
where `TotalATKModifierPercentage` is the signed sum of all applicable Pet ATK
modifiers from `PetState.ATKModifiers[]` and applicable Turn-based `BuffDebuff`
ATK `StatusEffects[]` (D5). No new ATK cap is introduced (D5). The two modifier
carriers retain independent ownership and lifecycles (`PetState.ATKModifiers[] ≠ PetState.StatusEffects[]`);
expiry of one modifier removes only its own entry and does not remove or disturb
the other (D6). `PetState.ATK` remains the permanent/base ATK and is never mutated
or reset; `EffectivePetATK` remains derived state (D7). §5.6.6 is no longer
unresolved; the worked example demonstrates `50 × (100 − 25) / 100 = 37`. §5.4,
§5.5, §3, §3.1, §3.4, and §1.1 are unchanged. No new Battle Event, Status Effect
`Type`, `TargetStat` value, `effectType`, `BattleState` member, SignalR member,
Redis key, or database column is introduced. Resolves the TASK-137 documentation
application. Prior 2.1: (§5.6 added — the **Effective Pet ATK** composition for an
applied, Battle-lifetime ATK modifier sourced from a Relic is now authored at
its canonical owner, per the TASK-136 Product-Owner decision set D1–D12:
`EffectivePetATK = truncate(PetState.ATK × (100 + TotalATKModifierPercentage) /
100)`, where the modifiers are **signed percentage-point contributions** and
`TotalATKModifierPercentage` is their **sum** (D5/D6); the value is consumed at
the Player → Boss Damage Pipeline Step 1 `Attack` input, applies to
`PetState.ATK` **alone**, is truncated toward zero, and **no ATK cap is
authored** because `ATK` has no documented valid range in §1.1 and D5 forbids
inventing one; the base stat is **never overwritten** and has no restore step
(D5/D8). §5.6 is a **separate rule**, not an extension of §5.4: §5.4 remains
scoped by §5.4.5 to a Turn-based `BuffDebuff` instance and is **not** silently
reused for a Relic Battle-lifetime modifier (D11). §5.6.6 records an
**UNRESOLVED** open item: no authoritative document determines whether, or how,
a §5.4 `BuffDebuff` ATK modifier and a §5.6 Relic ATK modifier interact when
both are live on the same Pet attack — they are two rules producing the same
Step-1 input — so an implementation that would need to apply both must STOP per
`AGENTS.md` §7. **§5.4, §5.4.1–§5.4.5, §5.5, §5.5.1–§5.5.5, §3, §3.1, §3.4,
and §1.1 are unchanged**, including §5.4.5's `BuffDebuff`-only scope statement
and §3.4's Boss-side `Step 4 = 1.0`. No new Battle Event, Status Effect `Type`,
`TargetStat` value, `effectType`, `BattleState` member, SignalR member, Redis
key, database column, or RNG stream is introduced, and no authored balance value
changes. The state representation is `GAME_STATE.md` §2.3.7 and its lifecycle is
`GAME_STATE.md` §5.1.4; this document owns the composition only. Resolves the
TASK-136 documentation application; the decision input is TASK-136, not this
document. Prior 2.0: (§3.4 gained the **Boss Skill Step-1 composition** and §5.5.2's
composition note was resolved — the TASK-125 Product-Owner decision (Option B) is
now authored at its canonical owner. §3.4's Boss Skill clause now states the
composition: a Boss Skill's Step 1 Base Damage is the sum of its applicable
Step-1 contributions under §3 step 1 — `EffectiveBossATK` (§5.5.1) **and** the
Skill's authored Base Damage (`BOSS_RULES.md` §6.3/§6.3.1). A Boss ATK modifier
such as Hỏa Long's Rage therefore reaches a Boss Skill's Step-1 damage through
the `EffectiveBossATK` contribution, while the Skill's authored Base Damage
remains unchanged by it; the worked example is `EffectiveBossATK = 120`,
`+ 150` authored `= 270`. **This closes GAP-1**, which TASK-124 deliberately left
open: §5.5.2's composition note no longer describes the composition as an open,
pre-existing, or deliberately undecided question, and its "Does NOT reach" rows
and §5.5.3's scope row were reconciled to say the modifier never reaches the
authored Skill Base Damage value (which §5.5.3 previously, and now no longer,
expressed as excluding a Boss Skill outright). §3.4's Boss Basic Attack clause,
its Boss-side `Step 4 = 1.0`, §3.1's six-step order, §5.3's DR1–DR6, §5.2,
§5.4, §5.4.5, §5.5.1, §5.5.4, and §5.5.5 are unchanged; §3 step 1's sum rule is
referenced, not restated or altered. No new Battle Event, Status Effect `Type`,
`TargetStat` value, `BattleState` member, SignalR member, Redis key, database
column, or RNG stream is introduced — `EffectiveBossATK` remains derived,
non-stored terminology, and no authored balance value changes. Resolves the
TASK-126 documentation application of TASK-125's decision; the decision input is
TASK-125, not this document.
Prior 1.9: (§4 item 7 added and §5.5 added — the Boss Passive effect
contract TASK-123 decided is now authored at its canonical owners. §4 item 7
AUTHORS the **Heal Resolution** step: every Pet-HP healing source (Card Heal
and the §2 HP-Gem heal pool, now routed through it by §2 item 5) passes through
`Raw Heal → applicable Heal modifiers → Final Heal Amount → item 1's unchanged
MaxHP/overheal clamp → HP update`; Thủy Ma's −50% (`BOSS_RULES.md` §6.2) is one
applicable Heal modifier, not a special-cased site; the step is Pet-scoped and
does not govern Boss-side HP restoration; it modifies no MaxHP and does not
affect Shield; §4's self-description line widened to cover Heal resolution
alongside Shield. §5.5 added — the Boss-side `BuffDebuff` consumption rule,
mirroring §5.4: `TargetStat = "ATK"` on a Boss `StatusEffects[]` instance is
consumed at the Boss Damage Pipeline **Step 1 `Attack` input** as a derived,
non-stored `EffectiveBossATK`; it reaches only Boss damage whose Step-1 input
derives from `BossState.ATK` (the basic attack), not an independently authored
Boss Skill Base Damage; `BossState.ATK` is never overwritten. §5.3.3 gained
worked examples for a Battle-Start duration-3 apply, a step-18a duration-3
apply, and a duration-3 refresh. **§3.4 and §5.4.5 are unchanged** (both
retained — the modifier is a Step-1 input, not a Step-4 factor), §5.3's
DR1–DR6 are unchanged, §4 item 1's clamp is not re-ordered or reworded, and
§5.2/§5.4 are unchanged. No new Battle Event, Status Effect `Type`,
`TargetStat` value, `BattleState` member, SignalR member, Redis key, database
column, or RNG stream is introduced. Resolves the TASK-124 documentation
reconciliation of TASK-123's decisions; the decision input is TASK-123, not
this document. Reported open: the Boss Skill Step-1 composition question
(`§3.4` × `BOSS_RULES.md` §6.3.1) remains unresolved and is not authored here.)
Prior 1.8: §5.4 added — the canonical owner of how a Turn-based
`BuffDebuff` Status Effect's `Magnitude` reaches the stat its `TargetStat`
names. Resolves the TASK-119 Product Owner decision set: `TargetStat = "ATK"`
is consumed at the **Player → Boss Damage Pipeline Step 1 `Attack` input**; the
percentage applies to `PetState.ATK` **alone** (not to the Skill/Card base
value, the ATK-Gem-generated damage pool, the Step-1 sum, Step 4, or final
damage); `EffectiveATK = truncate(ATK × (100 − |Magnitude|) / 100)`, truncated
toward zero, applied before the pipeline runs; activity follows the committed
`StatusEffects[]` state at attack resolution and the unchanged §5.3 Turn-based
lifecycle; and the base stat is never overwritten nor reset to a configuration
default (the `ADR-017` / `GAME_STATE.md` §2.3.4 non-destructive precedent,
without touching `NextAttackCritModifiers[]`). §3, §3.1, §3.4, §5.1, §5.2, and
§5.3 are unchanged — including §3.4's Boss-side `Step 4 = 1.0` and §3.1's
six-step order. No new Battle Event, Status Effect `Type`, `TargetStat` value,
SignalR member, Redis key, database column, or RNG stream is introduced.
Prior 1.7: §3.3 items 7–11 added and §1.1's Crit row extended — the
`NextAttack` Crit contract TASK-116 resolved is now authored at its canonical
owner: item 7 owns Effective Crit composition (base + Passive + Relic +
applicable `NextAttack` modifiers, additive percentage points, capped at 100)
and the substitution of `EffectiveCrit` into the unchanged item 2 roll
procedure; item 8 owns the modifier lifetime (until consumed by a qualifying
owner attack — not Turn-based, not Shield-triggered, no cleanup) and the
qualifying-attack consumption boundary (non-damaging actions do not consume;
multiple damage instances from one attack consume once at the first
qualifying instance); item 9 owns source-specific removal; item 10 owns
multiple simultaneous sources and forbids the `DefaultCrit` reset; item 11
bounds the concept against the Status Effect model (§5.2/§5.3 unchanged).
§1.1's Crit row gains the **newly authored** 0–100 percentage-point range
(TASK-116 D-4.4 — not a pre-existing documented cap) and now states that the
stat is the permanent/base value. All other values, ranges, formulas, and
rules are unchanged; §3.3 items 1–6 are extended by reference, not reworded.
Prior 1.6: §4 — the Shield rule changed from additive stacking to
refresh: a Shield applied while a Shield of the same effect identity is active
replaces that Shield's value rather than adding to it, at most one Shield is
active per entity, and no second instance is created; the depletion rule is
authored — the pool reaching exactly 0 removes the Shield in the same
resolution, and overflow damage reduces HP by exactly the remainder; §4 item
2's "consumed first-in" wording reconciled to consumption before HP under a
single pool; §4 item 4's old Heal/Shield-not-in-the-pipeline item is now item 6.
`ShieldDepleted` is recorded as the trigger-based expiry condition, NOT a Battle
Event — no event name, payload, or wire member is authored. Resolves the
COMBAT_RULES.md §4 item 3 vs GAME_STATE.md §2.3.1 item 6 conflict per the
TASK-104 Product Owner rulings B-1 "Refresh Shield" / B-2 "Shield is
StatusEffect" / B-3 "Remove at 0, emit ShieldDepleted, overflow damage
continues". Prior 1.5: §5.3 added — canonical owner of Status Effect duration
consumption timing: the per-instance `remaining` counter, the single decrement
per Turn at `GAME_RULES.md` §17 step 19a, identical Apply/Refresh mechanism,
same-Turn reapplication, expiry at 0, the apply/refresh ordering invariant, and
the Turn-based vs. trigger-based scope; resolves the TASK-093 §11 blocking
decision recorded by TASK-094. Burn's tick schedule and
`BossState.SkillCooldown`'s decrement are unchanged and explicitly out of this
rule. Prior 1.4: §8 added — Player XP / Level progression contract: the
two-track ownership model, the battle-outcome XP reward, the reward-amount
vs. curve-constant distinction, and the uncapped-XP / capped-Level
semantics; supersedes the retired `Player.Level × PetLevelMultiplier` Pet
Level derivation owned by `PET_RULES.md` §5; prior 1.3: §1.1 renamed —
combat stats are the active Pet's
(PetState) stats, not a Player combat identity; §3.4 Final Damage target
corrected to active Pet HP; wire `target="player"` documented as a fixed
protocol label for the player's side — §3.2 Boss Damage — Element defender
corrected to Player's active Pet; Boss Damage moved to §3.4 after Critical
Hits to resolve the duplicate §3.2 heading — Defense Mitigation remains
§3.2, Critical Hits remains §3.3)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §14 (Combat Rules) and §12 (Power Rules). Conflicts
resolve in favor of GAME_RULES.md.

---

# 1. Combat Stats

## 1.1 Active Pet Battle Stats (PetState)

```text
HP        current health                          MVP default: 1000
Max HP    maximum health                          MVP default: 1000
ATK       attack power                            MVP default: 50
DEF       defense                                 MVP default: 25
Power     resource for casting Cards / Skills, range 0–100
Crit      critical hit chance (%), range 0–100 percentage points
                                              MVP default: 5%
Status    active Status Effects (see §5)
```

These stats belong to the active Pet (the combat character) and are held
in `PetState` during battle (`GAME_STATE.md` §2.3). The Player
(account/owner) carries no separate combat stats — there is no parallel
Player HP/ATK/DEF/Power pool.

These are the MVP baseline configuration values. They are not permanent
invariants — future Pet progression (Level, Star, Tier) may produce different
actual Battle Stats. Combat formulas consume the current Battle Stats rather
than assuming these exact numbers permanently. Changing balance values is a
configuration change, not a combat pipeline redesign.

**`Crit`'s 0–100 range is a newly authored value, not a pre-existing one.**
It was authored by the TASK-116 Product Owner decision set (D-4.4) and is
recorded in the table above so the composed Crit value has a documented
ceiling; §3.3 item 7 owns the composition and cap rule. It must not be
confused with the Crit roll's bound (V ∈ [0, 100), §3.3 item 2) — the two are
separate values with separate meanings, and neither is inferred from the
other. `Crit` was previously documented here with a default and no range.

**`Crit` is the permanent/base value, and temporary modifiers do not
overwrite it.** The stat holds the base percentage only. A temporary
source-specific Crit modifier is held in
`PetState.NextAttackCritModifiers[]` (`GAME_STATE.md` §2.3.4) and participates
in the composed value §3.3 items 7–10 define; it is never written into this
stat. This is what makes source-specific removal possible (§3.3 item 9) and
is why the configuration default above is an initialization value only and
never a runtime reset target (§3.3 item 10).

## 1.2 Boss Stats

```text
HP, Max HP, ATK, DEF, Element, Passive, Skill, State
```

Boss "State" is an internal enum (Idle / Charging / Enraged / Stunned —
BOSS_RULES.md §1) used by Boss mechanics; see BOSS_RULES.md.

---

# 2. Resource Generation (from Match-3)

Baseline generation per consumed Gem (MVP defaults, all configuration):

```text
Gem Type   Base Output (per Gem, Match-3 tier)
--------   -----------------------------------
ATK Gem    +10 Base Damage
DEF Gem    +5 Defense pool
HP Gem     +20 Heal pool
POWER Gem  +10 Power (flat, per GDD example)
```

Match tier multipliers (applied to the above base output, per Gem consumed in
that match):

```text
Match 3   → 1.0×
Match 4   → 1.5×  (+ Special Gem, see MATCH3_RULES.md §5.2)
Match 5   → 2.0×  (+ Special Gem, see MATCH3_RULES.md §5.3)
L/T       → 1.25× (+ Special Gem, see MATCH3_RULES.md §5.4)
```

**Scope of the tier multiplier — matched Gems only.** The table above applies to
the Gems **consumed by a Match**, at the tier of the shape they formed. It is
the sole source of truth for match-tier output, and it is the rate every other
document references.

1. **Special Gem activation clears are not a Match** and have **no match
   tier**: the cells a Special Gem's effect clears generate at the **Match-3
   base rate** (`1.0×`) above, because the effect is an explosion, not a match
   (`MATCH3_RULES.md` §5.5.5 item 8, `PASSIVE_RULES.md` §2.2).
2. **A Special Gem's creating Match still applies its own tier** to its own
   consumed Gems (`1.5×` / `2.0×` / `1.25×`), once each
   (`MATCH3_RULES.md` §5.5.4 item 5). The tier belongs to the shape, not to the
   Special Gem, so it is never applied a second time when that Special Gem
   later activates.
3. **Which cells generate, and how often**, is owned by `MATCH3_RULES.md`
   §5.7 and §5.8.2 (once per unique cleared cell, over the union). This
   document owns only the **rates**.
4. **Match 6+ has no multiplier of its own.** A run of 6 or more is a Match 5
   (`MATCH3_RULES.md` §5.3 item 3) and uses the Match-5 rate above. No sixth
   row exists in this table.
5. **The HP Gem heal pool is a healing source.** The HP Gem's generated output
   is a Heal pool: `GAME_RULES.md` §17 step 12 generates it and step 14
   ("Resolve Player Effects") applies it to the active Pet's HP. When it is
   applied, it passes through the **Heal Resolution** step (§4 item 7) — the
   same step every Pet-HP healing source uses — and therefore receives the
   applicable Heal modifiers and the item-1 overheal clamp. This item states
   only where the pool is **consumed**; the pool's rate is the table above and
   is not restated elsewhere.

These are default balance values owned by this document. They are
configuration, not hardcoded constants, and are not required to match any
flavor example elsewhere (GDD.md intentionally contains no exact numbers —
this document is the sole source of truth for match-tier resource output).

---

# 3. Damage Pipeline

Full conceptual pipeline (expands GAME_RULES.md §14):

```text
1. Base Damage           (from ATK stat, Skill/Card base value, and any
                           ATK-Gem-generated damage pool for this action)
2. × Combo Modifier      (GAME_RULES.md §5 table)
3. × Element Modifier    (ELEMENT_RULES.md §2.2)
4. × Other Modifiers     (Relic bonuses, Passive bonuses, Buffs/Debuffs,
                           Crit multiplier if a Crit roll succeeds)
5. − Defense Mitigation  (target DEF, reduced per the mitigation formula below)
6. → Final Damage        (minimum 0; cannot reduce HP-restoring effects negative)
```

## 3.1 Order Is Fixed

Steps 1–6 must execute in this order for every damage instance. Modifiers within
step 4 (multiple Relics/Buffs) apply in the deterministic trigger order defined
in RELIC_RULES.md §4, but step 4 as a whole always resolves before step 5
(Defense), and step 5 always resolves before Final Damage.

## 3.2 Defense Mitigation

Formula:

```text
Mitigated Damage = Pre-Defense Damage × ( K / (K + DEF) )
```

where `K` is a tunable constant controlling how quickly DEF diminishes
incoming damage. MVP default: `K = 100` (configuration).

`DEF` is the **defending target's** Defense: `BossState.DEF` for
player→Boss damage, the active Pet's `DEF` (in `PetState`) for
Boss→player damage — the defender is always the active Pet, never a
separate Player entity (`ELEMENT_RULES.md` §5).

## 3.3 Critical Hits

1. **Pipeline Position & Frequency:** Crit is evaluated once per damage instance,
   inside Damage Pipeline step 4 ("× Other Modifiers", after Element Modifier and
   before Defense Mitigation).
2. **Deterministic Roll Procedure:** The Crit roll is exactly one **bounded RNG
   selection with bound 100**, consuming from the single server-authoritative
   `RngState` (PCG32 stream, `GAME_STATE.md` §2.6, `ADR-009`).
   - The bounded selection follows the repository's rejection-sampling
     operation (`MATCH3_RULES.md` §1.2.1.4 item 2): it draws 32-bit PRNG
     output(s) until a value is accepted at or above the threshold, then reduces
     modulo 100 to yield an integer value $V \in [0, 100)$.
   - The roll **succeeds** if and only if $V < \text{current Crit stat}$,
     where the value compared against is the **Effective Crit** value item 7
     composes (the base `Crit` stat of §1.1 plus the applicable modifiers).
     With no modifier active, Effective Crit equals the base stat.
   - If $V \ge \text{current Crit stat}$, the roll **fails**.
   - Example: with MVP default `Crit = 5`, accepted values in $\{0, 1, 2, 3, 4\}$
     succeed, producing an observed rate of exactly 5%.
3. **Multiplier:** On a successful Crit roll, damage is multiplied by the Crit
   Multiplier (MVP default: `1.5×`, configuration). On failure, the Crit factor
   is `1.00×`.
4. **Scope:** Crit is evaluated for every damage instance traversing the Damage
   Pipeline, including Player → Boss damage, Boss → Pet damage, and
   damage-over-time ticks (Burn, §5.2 item 3).
5. **Modifier Sources:** Crit chance can be modified by Relics (e.g. "Assassin
   Eye": Combo ≥ 3 → increase Crit chance), Pet Passives (e.g. Bạch Hổ: next
   attack gains increased Crit chance), and Cards (e.g. Iron Fang: `CARD_RULES.md`
   §4.1). How those sources combine is owned by item 7 below.
6. **Result Representation:** The Crit outcome is carried within step 4's
   combined `otherModifiers` multiplier (`SIGNALR_PROTOCOL.md` §3.2.13). No
   separate Crit event, state property, or wire member is emitted.
7. **Effective Crit — Composition (Canonical Owner).** This item is the
   **canonical owner** of how the Crit sources compose, of the composed
   value's cap, and of the value the roll in item 2 compares against. Other
   documents reference it; they do not restate it
   (`.ai/workflow/documentation/documentation-change.md` §2).

   ```text
   EffectiveCrit =
         BaseCrit                                  the permanent value of the
                                                   Crit stat (§1.1)
       + PassiveCrit                               applicable and active
       + RelicCrit                                 applicable and active
       + the sum of applicable NextAttack Crit modifiers
           ↓
       capped at 100 percentage points
   ```

   - **`Crit` is the base.** The stat is the permanent/base value and is
     **never overwritten** by a temporary modifier (§1.1). Effective Crit is
     calculated at runtime **for the current Damage Pipeline execution** — it
     is a value used within one pipeline run, not a stored state member.
   - **Contributions are additive**, in **percentage points** — the unit
     `CARD_RULES.md` §4.1 and `DATABASE.md` §3 item 1 already use for a Crit
     element's `value`.
   - **Only sources that are active and applicable to the current attack
     participate.** A source that is not currently qualifying contributes
     nothing to this execution.
   - **Each source contributes independently**, so removing one source does
     not disturb the base, the Passive contribution, the Relic contribution,
     or any other temporary source (item 9).
   - **The composed value is capped at 100 percentage points** (the range
     §1.1 records for the Crit stat). The cap bounds the composed value; it
     does **not** change item 2's roll bound.
   - **Item 2's roll procedure is unchanged**, with `EffectiveCrit`
     substituted for the bare Crit stat: exactly one bounded RNG selection
     over bound 100 yields V ∈ [0, 100), and the roll succeeds iff
     V < `EffectiveCrit`. No second draw, stream, or generator is introduced
     (`ADR-009`, `GAME_STATE.md` §2.6.2).
8. **`NextAttack`-Scoped Crit Modifiers — Lifetime and the Qualifying Attack
   (Canonical Owner).** This item is the **canonical owner** of the
   lifetime and the consumption boundary of a Crit modification scoped to
   the next attack (`DATABASE.md` §3 item 1's `scope = "NextAttack"`). The
   state representation is `GAME_STATE.md` §2.3.4 and its mutation lifecycle
   is `GAME_STATE.md` §5.1.2; `ADR-017` records why it is represented that
   way. This item authors the gameplay rule; it does not describe storage.

   ```text
   lifetime      active until the owner's next qualifying attack consumes it
   not           Turn-based: no Turn countdown, no step 19a decrement, no
                 Timeout, no end-of-turn removal, and no automatic cleanup
   not           trigger-based: it does not use Shield depletion
   ```

   - **A qualifying attack is an explicit owner attack action that enters the
     Damage Pipeline.** Whether a given action is the owner's attack action
     is determined by `GAME_RULES.md` §17's resolution order; this rule does
     not add a step to it, and creation happens at the source's own existing
     site (a Card or Pet Skill at step 14 "Resolve Player Effects"; a Pet
     Passive at step 10 "Charge Passive").
   - **A raw damage instance is not itself a separate attack.** A
     non-damaging action (a Heal, Shield, or Power Charge Card cast, or a Swap
     producing no damage) **does not consume** a modifier.
   - **Multiple damage instances from one qualifying attack belong to the
     same attack.** The modifier is consumed at the **first qualifying damage
     instance** of that attack and must **not** be consumed again by later
     instances belonging to the same attack.
   - **Because a Burn/DoT tick and the Boss's own attack are not the owner's
     qualifying attack action, they do not consume a modifier** — even though
     item 4 makes them Crit-eligible and they therefore **do** participate in
     Effective Crit composition while a modifier is active. Eligibility
     decides which value the roll reads; consumption decides whether the
     modifier survives the instance. The two are different questions.
   - **An unconsumed modifier persists across Turns until a qualifying attack
     occurs.** This is intentional. No Turn-based expiry, timeout, or cleanup
     may be introduced for it, at any layer.
   - **Consumption point.** The consumed modifier(s) are removed in the same
     resolution as the consuming attack, in the single post-resolution
     write-back (`GAME_STATE.md` §5.1, §5.1.2 item 6). Consumption removes
     **only** the source-specific modifiers consumed by that attack — see
     item 9.
9. **Source-Specific Removal.** Consuming a `NextAttack` Crit modifier
   removes **only** the temporary modifier(s) the qualifying attack consumed.

   ```text
   It must NOT reset the Crit stat.
   It must NOT remove Passive Crit.
   It must NOT remove Relic Crit.
   It must NOT remove an unrelated temporary Crit source not consumed by
     that attack.
   ```

   Consumption is the deletion of identified elements, never an arithmetic
   inverse and never a recomputation from the base.
10. **Multiple Simultaneous `NextAttack` Sources, and the Configuration
    Default.** Multiple active `NextAttack` Crit modifiers **stack
    additively** and **do not replace one another**; a qualifying attack
    consumes **all applicable** modifiers assigned to that attack, and the
    permanent/base Crit remains unchanged afterwards.

    Worked example — Iron Fang together with Bạch Hổ's Passive:

    ```text
    Base Crit            = 5
    Iron Fang            = +10 percentage points
    Bạch Hổ's Passive    = +10 percentage points

    Effective Crit       = 25   (≥ … capped at 100)

    after the qualifying attack consumes both modifiers:
    Base Crit            = 5    (unchanged)
    Iron Fang modifier   = removed
    Bạch Hổ modifier     = removed
    ```

    `CARD_RULES.md` §4.1's statement that Iron Fang's value is independent of
    Bạch Hổ's configuration value remains in force: they are separate
    sources, neither derived from nor shared with the other, and each is
    individually removable.

    **`DefaultCrit` (or any equivalent configuration default) is
    initialization/default-state data only. It is never a runtime reset
    mechanism.** Consumption must never be expressed as assigning the Crit
    stat its configured default value — that comparison is source-blind and
    is forbidden (`GAME_STATE.md` §2.3.4 item 10, `ADR-017`).
11. **The `NextAttack` scope is not a Status Effect.** A `NextAttack` Crit
    modifier is **not** represented as a Status Effect instance and does not
    use the duration model `COMBAT_RULES.md` §5.2 item 1 states for Status
    Effects. §5.3's Turn-countdown consumption therefore does not apply to
    it, and §5.3.2's scope statement is unchanged: the Turn countdown governs
    **Turn-based** Buff/Debuff Status Effects, and this modifier is not one.
    It is held in `PetState.NextAttackCritModifiers[]` (`GAME_STATE.md`
    §2.3.4) — a collection separate from `StatusEffects[]`, whose
    two-duration-model dichotomy (`GAME_STATE.md` §2.3.1 item 3) is neither
    widened nor relaxed by it.

## 3.4 Boss Damage

Boss basic attacks and Boss Skills both use the same Damage Pipeline (steps 1–6):

```text
Boss Basic Attack:
  Step 1 — Base Damage = Boss.ATK
  Step 2 — Combo Modifier = 1 (Boss attacks are not part of a Combo chain)
  Step 3 — Element Modifier = Boss.Element vs. the active Pet's Element (ELEMENT_RULES.md §2, §5 — the defender is the Pet, not the Player)
  Step 4 — Other Modifiers = 1.0 (MVP: no Relic/Passive/Buff modifiers on Boss side)
  Step 5 — Defense Mitigation = Defender DEF (COMBAT_RULES.md §3.2)
  Step 6 — Final Damage applied to active Pet HP (PetState.HP)

Boss Skill:
  Same pipeline as above, but Step 1 Base Damage is defined per Skill
  (BOSS_RULES.md §6). Boss Skill damage may also include non-damage
  effects (debuffs, resource drain) which are applied outside the pipeline.
```

**Boss Skill Step-1 composition.** A Boss Skill's Step 1 Base Damage is the
**sum of its applicable Step-1 contributions**, per §3 step 1's existing rule —
the same rule this document already applies to every other pipeline user. Both
of the following contribute:

```text
1. EffectiveBossATK  — the Boss's `ATK` for this attack, after any applicable
                       Boss ATK modifier (§5.5.1). Derived at attack
                       resolution; not stored.
2. The Skill's authored Base Damage — the per-Skill value §3.4 names above,
                       owned by BOSS_RULES.md §6.3/§6.3.1.
```

```text
BossState.ATK
      ↓
Boss ATK modifiers (e.g. Hỏa Long Rage, BOSS_RULES.md §6.2)
      ↓
EffectiveBossATK            derived at attack resolution — NOT stored
      ↓
Boss Skill Step 1
      ↓
+ the Skill's authored Base Damage
      ↓
Step 1 Base Damage
```

The composition is therefore:

```text
Boss Skill Step 1 Base Damage = EffectiveBossATK + authored Skill Base Damage
```

Consequences, all of which follow from the two contributions above being
separate Step-1 contributions under §3 step 1:

- **A Boss ATK modifier reaches a Boss Skill's damage through
  `EffectiveBossATK`.** Hỏa Long's Rage (`BOSS_RULES.md` §6.2) therefore
  affects a Boss Skill's Step-1 damage — it modifies the `EffectiveBossATK`
  contribution (§5.5.1), not the Skill's authored value.
- **The Skill's authored Base Damage is unchanged by a Boss ATK modifier.**
  The percentage applies to `BossState.ATK` **alone** (§5.5.1 item 3), exactly
  as §5.4.1 item 2 applies the Pet-side modifier to `PetState.ATK` alone; a
  Skill/Card base value is a separate Step-1 contribution and is not modified.
- **This is the Boss-side counterpart of §5.4.1 item 2**, which spells the
  same sum out for the Pet (`EffectiveATK + Skill/Card base value +
  ATK-Gem-generated damage pool`). Unlike the Pet, a Boss Skill contributes
  **no ATK-Gem-generated damage pool** — Bosses match no Gems (§3.4 above).
- **Step 4 is unaffected.** The Boss side's `Step 4 = 1.0` (above) stands; a
  Boss ATK modifier is a Step-1 input, never a Step-4 factor (§5.5.3).

**Worked example** — Rage active, using the documents' existing values
(`§1.1`'s MVP `ATK` default, `BOSS_RULES.md` §6.2's `+20%`, and
`BOSS_RULES.md` §6.3.1 item 1's authored `150`):

```text
BossState.ATK                = 100
Hỏa Long Rage                = +20%
EffectiveBossATK             = truncate(100 × (100 + 20) / 100) = 120

Flame Burst authored Base Damage = 150        (unchanged by the modifier)

Step 1 Base Damage = 120 + 150 = 270
```

With Rage inactive (`EffectiveBossATK = 100`) the same Skill yields
`100 + 150 = 250`. The authored `150` is the same in both cases — only the
`EffectiveBossATK` contribution carries the modifier.

`EffectiveBossATK` is derived terminology for a value used within **one**
pipeline execution. It is **not** a `BattleState`/`BossState` member, not
persisted, and not a second representation of the Boss `ATK` stat (§5.5.4).
The resulting Step-1 value already reaches the client as
`DamageCalculated.base` (`SIGNALR_PROTOCOL.md` §3.2.13) — a value change, not a
member-set change.

The `DamageCalculated` event reports the full breakdown; `DamageDealt` and
`DamageTaken` report the final amount with `source = "boss"` and
`target = "player"`. The value `"player"` is a fixed wire label
identifying the player's side (the active Pet) — it does not imply a
separate Player HP pool (`SIGNALR_PROTOCOL.md` §3.2).

---

# 4. Healing and Shields

This section is the **canonical owner** of the Shield rule — its application,
refresh, absorption, and depletion — and of the **Heal Resolution** step
(item 7) — how a raw Heal amount becomes the HP change a healing source
applies. Other documents reference it; they do not restate it
(`.ai/workflow/documentation/documentation-change.md` §2). Shield is
represented as a Status Effect instance (`GAME_STATE.md` §2.3.1 item 3, `Type =
"Shield"`) and is **trigger-based**: it acquires no duration and is outside the
Turn countdown (§5.3.2).

1. Heal effects restore HP up to Max HP; overheal is discarded unless a Relic
   explicitly grants overheal/temp-HP.
2. Shield effects grant an absorption pool that reduces incoming damage before
   HP is affected. The pool is consumed **before HP** on any Final Damage
   applied to that target; because at most one Shield is active per entity
   (item 3), there is no multi-pool ordering to resolve and no "first-in"
   tie-break exists.
3. **Shield application refreshes; Shields do not stack.** Applying a Shield to
   an entity that already has an active Shield **refreshes that existing
   Shield** — the refreshed pool is **set to the magnitude of the new
   application**. It is **not** summed with the existing value, and no additive
   accumulation occurs under any MVP condition. No second Shield instance is
   created: **at most one Shield is active per entity**, identified by the
   Status Effect identity `"Shield"` (`GAME_STATE.md` §2.3.1 item 1, item 6) —
   so two different Shield sources (e.g. the Shield Basic Card and a
   Shield-granting Passive) applying to the same entity refresh one another
   rather than forming a second pool. This is `§5.2 item 2`'s refresh default
   applied to Shield, and the refresh is the same "set, not an increment"
   operation `GAME_STATE.md` §5.1.1 item 1 defines for every Status Effect. The
   carve-out is preserved: a **Relic** may explicitly specify otherwise. **No
   MVP Relic exercises this carve-out** (`ROADMAP.md` Phase 1 — "No Relics
   yet").
4. **Depletion at exactly 0.** When damage reduces the pool, the pool reaches
   **exactly 0**, and the Shield is **removed in that same resolution** — a
   committed Shield value of 0 is never observable as an active Shield
   (`GAME_STATE.md` §2.3.1 item 8's zero-is-not-a-stored-state rule, applied to
   this trigger-based instance; §5.1.1 item 7). This is the `"ShieldDepleted"`
   expiry condition (`GAME_STATE.md` §2.3.1 item 5) — the trigger that removes
   the instance, evaluated during the damage resolution that depleted it.
   `ShieldDepleted` is **not a Battle Event**: no event of that name exists in
   `GAME_RULES.md` §16's canonical list or in `GAME_EVENTS.md` §2, and this
   section authors none.
5. **Overflow continues to HP.** Damage is applied in this fixed order:
   ```text
   Final Damage
     ↓
   Shield absorbs (pool reduced)
     ↓
   pool reaches 0 → Shield removed (same resolution)
     ↓
   remaining damage reduces HP, by exactly the remainder
   ```
   - Damage **less than** the pool: the pool is reduced, HP is unchanged.
   - Damage **exactly equal to** the pool: the pool reaches 0, the Shield is
     removed, and HP is **unchanged** — the whole amount is absorbed.
   - Damage **greater than** the pool: the pool reaches 0, the Shield is
     removed, and HP is reduced by **exactly the remainder** (damage minus the
     pool). There is no double-counting: the absorbed portion never also
     reduces HP.
6. Heal and Shield amounts are NOT subject to the Damage Pipeline (§3) —
   they are not damage — but they ARE subject to their own explicit
   modifiers (e.g. a Relic that increases Heal Card effectiveness). Step 5's
   absorption is applied to the Final Damage the pipeline produces (§3 step 6);
   it does not change that pipeline, whose steps 1–6 are unchanged.
7. **Heal Resolution (canonical).** This item is the **canonical owner** of
   how a Raw Heal amount becomes the HP change a healing source applies. Every
   healing source that restores **Pet HP** passes through this step. Other
   documents reference it; they do not restate it
   (`.ai/workflow/documentation/documentation-change.md` §2).

   ```text
   Raw Heal
     ↓
   Applicable Heal Modifiers
     ↓
   Final Heal Amount
     ↓
   Existing MaxHP / overheal clamp   (item 1 — unchanged, still last)
     ↓
   HP update
   ```

   - **Raw Heal** is the amount the healing source itself produces, before any
     modifier — e.g. a Card Heal's authored percentage of Max HP
     (`CARD_RULES.md` §4.1), or the HP-Gem heal pool (§2) that
     `GAME_RULES.md` §17 step 12 generates and step 14 applies.
   - **Applicable Heal Modifiers** are the modifiers that currently apply to
     this healing instance, from the sources that are active and applicable to
     it. Each is applied to the Raw Heal to produce the Final Heal Amount. An
     applicable Heal modifier is **not** a special-cased site: any rule that
     reduces or increases healing received is one applicable Heal modifier
     here.
     - **Thủy Ma's −50% healing reduction (`BOSS_RULES.md` §6.2.2) is one
       applicable Heal modifier.** It is not a second mechanism and has no
       site of its own: it participates in this step exactly as any other
       applicable Heal modifier does.
     - **An applicable Heal modifier may be held on the Boss.** This Pet-scoped
       step is **explicitly authorized** to perform a **cross-entity read** of
       `BossState.StatusEffects[]` at this stage, for the instance applicable to
       Pet healing. The read is **one-directional** (Pet-side step →
       Boss-owned instance) and **non-mutating**: it does not write
       `BossState`, and it does not consume, decrement, or remove the instance.
       Which instance is applicable is determined by the Status Effect identity
       `Id` the effect's own rule authors, under `GAME_STATE.md` §2.3.1 item 1
       and item 6; the value is owned by the modifier's rule
       (`BOSS_RULES.md` §6.2.2) and is referenced, not restated, here. Thủy Ma's
       healing reduction is the MVP instance of this authorized read, and it is
       **not** a `TargetStat`-consumed `BuffDebuff` (§5.4.5 / §5.5.3).
     - **This authorization is a read, not a scope change.** It does not widen
       the "Scope — Pet HP only" clause below: this step still governs only
       healing that restores Pet HP, and a Boss-side HP change still does not
       route through this step. The effect's owner is the Boss; the resolution
       site is this Pet-scoped step; the authorized read is the bridge between
       them.
     - This item **authors the mechanism only**. It defines no modifier's
       magnitude, source, duration, or activity window; those belong to the
       rule that creates the modifier, and are referenced, not restated here.
   - **The Final Heal Amount is what item 1's clamp receives.** Item 1's clamp
     ("Heal effects restore HP up to Max HP; overheal is discarded unless a
     Relic explicitly grants overheal/temp-HP") is **unchanged and is still the
     last step**: it runs *after* the modifiers, on the Final Heal Amount, and
     the HP update applies its result. This item does not re-order, reword, or
     replace item 1.
   - **Scope — Pet HP only.** This step covers healing that restores **Pet
     HP**. It does **not** govern a Boss-side HP change: a Boss's own HP
     restoration is that effect's own rule and does not route through this
     step. The cross-entity read authorized above consults a Boss-**owned**
     modifier while resolving **Pet** healing; it does not add a second heal
     target and does not bring a Boss-side HP change into this step. Widening
     this step to another target would require its own recorded
     decision; this item does not do so
     (`BOSS_RULES.md` §6.2's Mộc Yêu regeneration).
   - **It does not modify MaxHP, and it does not affect Shield.** A Heal
     modifier changes the Heal amount only; MaxHP is untouched (§1.2) and
     Shield keeps its own §4 rules (items 2–5).
   - **No elemental interaction.** `ELEMENT_RULES.md` §2.2's Element Modifier
     does not apply to non-damage effects, so this step introduces no
     elemental interaction for healing.
   - **This is a combat-rule mechanism only.** It adds no `BattleState`
     member, no Battle Event (`GAME_RULES.md` §16), no SignalR member, no
     Redis key, and no database column. It introduces no generic
     abstraction beyond this documented step.

---

# 5. Status Effects

## 5.1 MVP Status Effects

```text
Burn      damage-over-time, ticks once per resolved Turn at End Turn
          (GAME_RULES.md §17 step 19a), Element = Hỏa for Element
          Modifier purposes
Shield    absorption pool, see §4; refresh-not-stack, removed at 0
          (trigger-based expiry, §4 items 3–5)
Buff/Debuff  temporary stat modification (ATK/DEF/Crit/etc.), with duration
          measured in Turns unless stated otherwise; how a `Magnitude`
          reaches the stat its `TargetStat` names is owned by §5.4
```

## 5.2 Status Rules

1. Every Status Effect has a source, a magnitude, and a duration (in Turns) or
   a trigger-based expiry (e.g. "until Shield is depleted" — §4 item 4).
2. Stacking behavior (refresh duration vs. stack magnitude vs. independent
   instances) is defined per-effect; default for MVP is **refresh duration, do
   not stack magnitude** unless a Card/Relic explicitly says otherwise
   (e.g. "Burning Curse" increases Burn damage, which is a magnitude modifier
   on the existing Burn, not a second stack). Shield follows this default and
   is stated explicitly in §4 item 3; no MVP Card or Relic carves Shield out
   of it.
3. Damage-over-time ticks (Burn) traverse the Damage Pipeline (§3) using the
   Effect's own Element, and participate in the step 4 Crit evaluation per
   §3.3, but do not consume Combo (Combo Modifier step uses Combo = 1 /
   neutral for DoT ticks, since a DoT tick is not itself part of a Swap's Combo
   chain).

## 5.3 Duration Consumption Timing

This section is the **canonical owner** of when one Turn of a Status Effect's
duration is consumed. Other documents reference it; they do not restate it
(`.ai/workflow/documentation/documentation-change.md` §2).

```text
DR1. Duration is a per-effect-instance counter, initialized to the
     applied/refreshed duration value.

DR2. Exactly one decrement occurs per Turn, at GAME_RULES.md §17 step 19a —
     regardless of how many apply/refresh operations occurred earlier in that
     same Turn.

DR3. Apply and Refresh use the SAME mechanism: `remaining = duration`
     (an initial Apply is not semantically different from a Refresh; Refresh
     simply re-executes the same "set remaining" operation on an already-active
     effect instance).

DR4. Same-Turn reapplication (Apply/Refresh occurring again within the Turn in
     which the effect is already active) resets `remaining` to the new duration
     value and does NOT trigger an additional consumption in that Turn. Only
     step 19a consumes.

DR5. Expiration occurs when `remaining` reaches 0 at step 19a. An effect at
     `remaining = 0` is inactive from that point forward (i.e. not active
     during the following Turn).
```

### 5.3.1 Apply/Refresh Ordering (DR6)

For any Turn-based Buff/Debuff Status Effect that is applied or refreshed at a
documented point before `GAME_RULES.md` §17 step 19a, the effect consumes one
Turn of duration at that Turn's step 19a.

`GAME_RULES.md` §17 defines Boss Response (step 18) before End Turn (step 19),
and step 19a is the last combat effect of the Turn. Therefore all currently
documented Buff/Debuff application sites occur before the consumption point.

No current gameplay rule defines an application or refresh after step 19a. If a
future gameplay source introduces such a site, its duration-consumption timing
must be explicitly defined before implementation; this rule does not infer or
create such an application point.

### 5.3.2 Scope

This rule applies to all **Turn-based** Buff/Debuff Status Effects. An effect
whose expiry is defined as trigger-based (§5.2 item 1, e.g. "until Shield is
depleted") does not use the Turn countdown. Root follows this general
Turn-based rule.

### 5.3.3 Worked Examples

```text
duration = 2, no refresh:
  Turn N:   Apply(2)              -> remaining = 2
            §17 step 19a          -> remaining = 1
  Turn N+1: active
            §17 step 19a          -> remaining = 0 -> expires
  Turn N+2: inactive

duration = 2, refreshed in Turn N+1:
  Turn N:   Apply(2)              -> remaining = 2
            §17 step 19a          -> remaining = 1
  Turn N+1: Refresh(2)            -> remaining = 2
            §17 step 19a          -> remaining = 1
  Turn N+2: §17 step 19a          -> remaining = 0 -> expires

duration = 1, no refresh:
  Turn N:   Apply(1)              -> remaining = 1
            §17 step 19a          -> remaining = 0 -> expires
  Turn N+1: inactive

duration = 2, applied and refreshed within the same Turn N (DR4):
  Turn N:   Apply(2)              -> remaining = 2
            Refresh(2)            -> remaining = 2 (reset; no extra consumption)
            §17 step 19a          -> remaining = 1
  Turn N+1: active
            §17 step 19a          -> remaining = 0 -> expires

duration = 3, applied at Battle Start — BEFORE the first counted Turn:
  (the apply point is not a Turn, so it consumes no duration unit on its own;
   the first consumed Turn is Turn 1 — §5.3.1/DR6)
  Battle Start:  Apply(3)         -> remaining = 3   (no Turn consumed)
  Turn 1:        active
                 §17 step 19a     -> remaining = 2
  Turn 2:        active
                 §17 step 19a     -> remaining = 1
  Turn 3:        active
                 §17 step 19a     -> remaining = 0 -> expires
  Turn 4:        inactive

duration = 3, applied at Boss Response step 18a of Turn N — BEFORE that same
Turn's step 19a:
  (the apply point is after step 15-17's already-resolved player damage, so
   this Turn's resolved damage is NOT modified retroactively — §5.4.3)
  Turn N:        Apply(3) at §17 step 18a  -> remaining = 3
                 §17 step 19a              -> remaining = 2
  Turn N+1:      active
                 §17 step 19a              -> remaining = 1
  Turn N+2:      active
                 §17 step 19a              -> remaining = 0 -> expires
  Turn N+3:      inactive

duration = 3, refreshed at Turn M while already active (DR3/DR4):
  Turn M:        Refresh(3)       -> remaining = 3 (reset; no extra consumption)
                 §17 step 19a     -> remaining = 2
  Turn M+1:      active
                 §17 step 19a     -> remaining = 1
  Turn M+2:      active
                 §17 step 19a     -> remaining = 0 -> expires
  Turn M+3:      inactive
```

### 5.3.4 No Change to Damage-over-time or Cooldown Rules

This section fixes the consumption point for **duration-based Buff/Debuff**
Status Effects. It does not change Burn's tick schedule, which
`BOSS_RULES.md` §6.3.1 item 1 owns and which is consistent with DR1–DR5. It
does not change `BossState.SkillCooldown`'s separate decrement rule
(`BOSS_RULES.md` §6.3, `GAME_STATE.md` §2.4.3) — that is a Boss Skill counter,
not a Status Effect, and no state field here adopts it.

## 5.4 Stat Modifiers (`BuffDebuff` Consumption)

This section is the **canonical owner** of how a Turn-based Buff/Debuff Status
Effect's `Magnitude` reaches the stat its `TargetStat` names. Other documents
reference it; they do not restate it
(`.ai/workflow/documentation/documentation-change.md` §2). Root
(`BOSS_RULES.md` §6.3.1 item 3) is the MVP instance of this rule; its magnitude,
its duration, and its `"ATK"` `TargetStat` are that document's and are not
restated here. `GAME_STATE.md` §2.3.1 types the instance and explicitly
declines to interpret `Magnitude`; this section supplies the meaning it hands
off.

### 5.4.1 `TargetStat = "ATK"` — the ATK Modifier Rule

A Turn-based `BuffDebuff` Status Effect instance whose `TargetStat` is `"ATK"`
modifies the **active Pet's ATK used by that Pet's own attack** — the Pet's
Player → Boss damage. The modifier is consumed when the Damage Pipeline call
for that attack is constructed, by participating in the **Step 1 `Attack` input**
it receives (`EffectivePetATK`).

**Canonical composition owner (TASK-138 D1, D2, D4):**
The arithmetic composition of Pet ATK modifiers is canonically owned by
**§5.6 / §5.6.6**. §5.4.1's historical absolute-value calculation path
(`truncate(ATK × (100 − |Magnitude|) / 100)`) is **superseded/narrowed** for ATK
composition and no longer defines a separate calculation path (D2, D4).
This section (§5.4) retains the Turn-based BuffDebuff StatusEffect consumption
and lifetime contract (activity, duration countdown per §5.3, non-retroactivity,
and base-stat independence); only its arithmetic contribution to `EffectivePetATK`
is governed by the unified composition model in §5.6.6 (D2).

```text
1. Consumption point
   Player → Boss Damage Pipeline Step 1 — the `Attack` argument the call
   receives is `EffectivePetATK`, produced by the canonical composition model
   in §5.6 / §5.6.6 (TASK-138 D4).

2. What the percentage applies to — PetState.ATK ALONE
   The percentage adjustment applies to permanent base PetState.ATK alone (§5.6.1, §5.6.6).
   Step 1 = EffectivePetATK
          + Skill/Card base value
          + ATK-Gem-generated damage pool

3. Percentage contribution and sign (TASK-138 D3)
   An active Turn-based BuffDebuff ATK instance contributes to TotalATKModifierPercentage
   in §5.6.6 with its documented sign:
   - ATK reduction (e.g. Root -30%): contributes -|Magnitude| signed percentage points (-30).
   - ATK increase (if authored): contributes +|Magnitude| signed percentage points.
   The stored StatusEffect.Magnitude remains the existing applied magnitude (30) and
   does not itself encode the sign (GAME_STATE.md §2.3.1 item 2).

4. Activity and evaluation timing
   The modifier participates if active according to the committed state of the
   instance at attack resolution, per §5.3 and §5.4.3.
```

**Worked example** (`PetState.ATK = 100`, ATK-Gem-generated pool `= 40`,
Root `Magnitude = 30`, no Relic equipped):

Under the canonical composition rule in §5.6.6:
```text
TotalATKModifierPercentage = -30
EffectivePetATK = truncate(100 × (100 + (-30)) / 100) = truncate(100 × 70 / 100) = 70

Step 1 Base Damage = 70 + 40 = 110
```

It is **not** `(100 + 40) × 70% = 98`. The Skill/Card base value and the
ATK-Gem-generated damage pool are separate Step-1 contributions
(§3 step 1) and are **not** modified by this rule. The modifier does not touch
Step 4, and it does not touch Step 6's Final Damage beyond the Step-1 input it
changed.

### 5.4.2 Rounding and Truncation

Rounding for Pet ATK modifier composition is canonically governed by **§5.6.1 item 4
and §5.6.6 item 4 (TASK-138 D5)**:
- The combined signed percentage (`TotalATKModifierPercentage`) is applied to `PetState.ATK`
  and truncated toward zero **exactly once** to produce integer `EffectivePetATK`.
- **No intermediate per-modifier truncation occurs.** When multiple modifiers are active,
  sequential per-instance truncation is prohibited.

For a single active BuffDebuff modifier (such as Root -30% with no Relic equipped),
the single combined truncation yields:

```text
ATK 50  → 35  (truncate(50 × 70 / 100) = 35)
ATK 51  → 35  (truncate(51 × 70 / 100) = 35.7 → 35)
ATK 99  → 69  (truncate(99 × 70 / 100) = 69.3 → 69)
ATK 100 → 70  (truncate(100 × 70 / 100) = 70)
ATK 101 → 70  (truncate(101 × 70 / 100) = 70.7 → 70)
```

The calculation must not depend on floating-point representation: the same
input ATK and active modifiers yield the same integer on every platform and in
every evaluation order (`TDD.md` §6). **§3 step 6's Final Damage rounding is
unchanged** — this rule rounds a Step-1 input, and adds, removes, or changes
no rounding anywhere else in the pipeline.

### 5.4.3 Activity and Duration

The modifier is active **according to the committed
`GAME_STATE.md` §2.3.1 instance state at attack resolution**, and it consumes
duration exactly as §5.3 defines — no new duration model, no new consumption
point, and no change to DR1–DR6. Because the Pet's attack is
`GAME_RULES.md` §17 step 15 and §5.3.1 DR6 places every documented
Buff/Debuff application before step 19a, an instance applied at a later step
of the **same** Turn cannot affect that Turn's already-resolved attack:

```text
Root applied at Turn N step 18b (Boss Skill secondary effect)
  Turn N   step 15   Root NOT yet applied — this attack is unaffected
           step 18b  Root applied, RemainingTurns = 2
           step 19a  RemainingTurns: 2 → 1

  Turn N+1 step 15   Root IS active — Pet ATK reduced by 30%
           step 19a  RemainingTurns: 1 → 0 → expires (§5.3 DR5)

  Turn N+2           inactive — Pet ATK is its normal derived value again
```

An instance whose `RemainingTurns` reaches 0 at step 19a is inactive from that
point forward (`GAME_STATE.md` §2.3.1 item 8: a stored zero is never an active
state), so the following Turn's attack uses the unreduced ATK.

### 5.4.4 The Base Stat Is Never Overwritten

The modifier is **non-destructive**, following the temporary-modifier
precedent `ADR-017` and `GAME_STATE.md` §2.3.4 record for the temporary Crit
modifier, and confirmed for Pet ATK composition by TASK-138 **D6**:

```text
PetState.ATK (permanent base stat)
      ↓
active modifiers (StatusEffects[], ATKModifiers[])
      ↓
EffectivePetATK        derived at attack resolution per §5.6.6 — NOT stored
      ↓
Damage Pipeline Step 1
```

- `PetState.ATK` **is never overwritten** by the modifier, and there is no
  "restore" step: the stored stat is unchanged throughout. An implementation
  that writes `PetState.ATK = PetState.ATK × 0.7` and later restores it is
  **not** this rule.
- The configured default is an **initialization value only** and is **never**
  an expiry or reset mechanism: `PetState.ATK = DefaultATK` is forbidden for
  the same source-blind reason §3.3 item 10 forbids `DefaultCrit`
  (`GAME_STATE.md` §2.3.4 item 10). No `DefaultATK`-style runtime reset
  mechanism exists (TASK-138 D6).
- `EffectivePetATK` is a value derived within **one** pipeline execution. It is
  **not** persisted, not stored in `BattleState`, and not a second
  representation of the ATK stat (`GAME_STATE.md` §0 item 5). Because the
  modifier lives in the existing `StatusEffects[]` instance
  (`GAME_STATE.md` §2.3.1 item 3, item 7), **no new state collection, member,
  or representation is introduced by this rule** — unlike ADR-017's Crit
  modifier, which needed its own collection. `NextAttackCritModifiers[]` is
  unaffected.

### 5.4.5 Scope and Boundaries

```text
Applies to      a Turn-based BuffDebuff instance with TargetStat = "ATK"
                consumed by the owning Pet's own attack (Player → Boss);
                its consumption and duration lifecycle are governed here,
                while its arithmetic contribution to EffectivePetATK is
                governed by the unified model in §5.6 / §5.6.6 (TASK-138 D2)
Does NOT apply  to the Boss's damage — §3.4 pins the Boss side's Step 4
                to 1.0, and this rule authors no Boss-side factor (§5.5
                governs the Boss side)
Does NOT apply  to a BuffDebuff naming any stat other than "ATK"; the
                consumer reads TargetStat explicitly and a non-"ATK" value
                modifies no ATK
Does NOT apply  to a DoT tick or a Shield: those Types have their own rules
                (§5.2 item 3, §4) and are not ATK modifiers
Does NOT        change §3.1's six-step order, §3.4's Boss-side value, or
                §5.3's duration consumption
```

A `BuffDebuff` whose `TargetStat` names a stat for which this document defines
no consumption rule is **not** silently treated as an ATK modifier: this
section defines the `"ATK"` case only, and any other stat would require its own
recorded decision before it could be implemented.

This section defines the `"ATK"` case only because that is the only
`TargetStat`-consumed case in MVP content. Thủy Ma's healing reduction
(`BOSS_RULES.md` §6.2.2) is held in the Boss's `StatusEffects[]` and read by
the Pet-side Heal Resolution step's authorized cross-entity read (§4 item 7),
which selects the instance by its Status Effect `Id`, not by `TargetStat`. It
is therefore **not** a `TargetStat`-consumed `BuffDebuff` and opens **no** new
non-`"ATK"` `TargetStat` case; no `TargetStat` value is added for it, and the
boundary above is neither weakened nor exercised.

This section adds no Battle Event (`GAME_RULES.md` §16), no Status Effect
`Type`, no `TargetStat` value, no SignalR member, no Redis key, and no
database column. The Step-1 value it produces already reaches the client as
`DamageCalculated.base` (`SIGNALR_PROTOCOL.md` §3.2.13) — a value change, not
a member-set change.

## 5.5 Boss Stat Modifiers (`BuffDebuff` Consumption — Boss Side)

This section is the **canonical owner** of how a Turn-based Buff/Debuff Status
Effect on the **Boss** reaches the stat its `TargetStat` names. It is the
Boss-side counterpart of §5.4 and mirrors its shape deliberately; the
Pet-side rule is §5.4 and is referenced, not restated
(`.ai/workflow/documentation/documentation-change.md` §2). Hỏa Long's Rage
(`BOSS_RULES.md` §6.2) is the MVP instance of this rule; its magnitude, its
duration, its trigger, and its repeat behavior are that document's and are not
restated here.

### 5.5.1 `TargetStat = "ATK"` — the Boss ATK Modifier Rule

A Turn-based `BuffDebuff` Status Effect instance held in the Boss's
`StatusEffects[]` (`GAME_STATE.md` §2.4/§2.4.1, same element schema and
lifecycle as the Pet's) whose `TargetStat` is `"ATK"` modifies the **Boss's ATK
used by ATK-derived Boss damage**. The modifier is consumed when the Damage
Pipeline call for that attack is constructed, by changing the **Step 1
`Attack` input** it receives.

```text
1. Consumption point
   Boss Damage Pipeline Step 1 — the `Attack` argument the call receives is
   the effective Boss ATK this rule produces.

2. The flow

   BossState.ATK
         ↓
   +20% Rage modifier (the active instance's Magnitude)
         ↓
   EffectiveBossATK        derived at attack resolution — NOT stored
         ↓
   Boss Damage Pipeline Step 1

3. What the percentage applies to — BossState.ATK ALONE

   EffectiveBossATK = truncate( BossState.ATK × (100 + Magnitude) / 100 )

   Step 1 = EffectiveBossATK

4. Percentage application and direction — the instance's Magnitude, signed

   The percentage value is supplied by the active instance's `Magnitude`
   (`BOSS_RULES.md` §6.2); this rule owns how that value is applied.

   The `Magnitude` is used WITH ITS OWN SIGN — it is the direction signal:

   Magnitude > 0  →  increase   EffectiveBossATK > BossState.ATK
   Magnitude < 0  →  decrease   EffectiveBossATK < BossState.ATK
   Magnitude = 0  →  unchanged  EffectiveBossATK = BossState.ATK

   A Boss-side ATK modifier therefore supports BOTH an increase and a
   decrease; there is no increase-only and no decrease-only semantic.
   Direction is carried by the sign of `Magnitude` itself — no separate
   direction field, flag, operation, or member exists or is introduced.
```

- **The modifier changes the Step-1 input, not Step 4.** `§3.4` pins the Boss
  side's Step 4 to `1.0` and receives no direct Relic/Passive/Buff modifier;
  **`§3.4` is unchanged by this rule.** Rage is not a Step-4 modifier — see
  §5.5.3. This is the same separation §5.4.1 item 1 draws for the Pet.

- **Rounding.** The formula's result is an integer, **truncated toward zero** —
  the same integer convention §5.4.2 and §3 step 6 already use. The applied
  arithmetic must not depend on floating-point representation: the same
  `BossState.ATK` and `Magnitude` yield the same `EffectiveBossATK` integer on
  every platform and in every evaluation order. This rule owns this formula;
  it authors no percentage of its own — the active instance's `Magnitude`
  supplies that value (`BOSS_RULES.md` §6.2).

- **Worked example.** `BOSS_RULES.md` §6.2.1's Hỏa Long Rage, using §1.1's MVP
  `ATK` default: `BossState.ATK = 100` with an active `Magnitude = +20` gives
  `truncate(100 × (100 + 20) / 100) = 120`. With `Magnitude = -30` the same
  stat gives `truncate(100 × (100 − 30) / 100) = 70`. `Magnitude = 0` leaves it
  at `100`. These values are the documents' own and are not restated here as
  new; `BOSS_RULES.md` §6.2.1 owns Rage's magnitude and duration, and §6.3.1
  owns the Bosses' authored Skill Base Damages.

- **This is NOT the Pet-side rule — the two conventions are deliberately
  different.** The Pet-side ATK composition is canonically governed by **§5.6 / §5.6.6**
  (where an active BuffDebuff reduction contributes ` -|Magnitude| ` signed percentage points
  per TASK-138 D3, and §5.4 governs consumption/lifetime); its scope statement records that
  it does not apply to the Boss's damage. This rule is the Boss-side counterpart and uses the
  **signed** `Magnitude` directly. The two must **not** be collapsed into one shared formula:
  the Pet-side composition uses a signed sum with negative contributions for debuffs against
  permanent base Pet ATK, while the Boss-side rule uses the signed `Magnitude` of an active
  `StatusEffects[]` entry against `BossState.ATK`. Each rule is referenced, not restated, by the
  other.

- **No new state, type, or protocol member.** The direction signal is the sign
  of the **existing** `Magnitude` field (`GAME_STATE.md` §2.3.1). This rule
  introduces no new `BossState`/`BattleState` member, no new `StatusEffect`
  member, no new Status Effect `Type`, no new `TargetStat` value, no wire
  field, no Redis key, and no database column.

- **Activity.** The committed state of the instance at attack resolution, per
  §5.3 — identical to §5.4.1 item 4. Duration, reapplication, and the
  refresh-not-stack default are §5.5.5's and are referenced, not re-authored
  here.

### 5.5.2 Damage Scope — Which Boss Damage the Modifier Reaches

The modifier enters through **the Step-1 `Attack` input derived from
`BossState.ATK`** — i.e. the `EffectiveBossATK` contribution — and it changes
that contribution only.

```text
Reaches        the Boss BASIC ATTACK — §3.4's "Step 1 — Base Damage =
               Boss.ATK", whose Step-1 input is EffectiveBossATK under this
               rule
Reaches        a Boss SKILL — through its EffectiveBossATK Step-1
               contribution only (§3.4 "Boss Skill Step-1 composition")
Does NOT       the Skill's authored Base Damage. That authored value is a
reach          separate Step-1 contribution (§3 step 1) and does not receive
               the modifier; it is not scaled by Boss ATK
```

The modifier's contribution is present in **every** Boss damage instance,
because every one has an `EffectiveBossATK` Step-1 contribution. What it never
reaches is an independently authored Skill Base Damage value, which stays at
its authored magnitude (`BOSS_RULES.md` §6.3.1).

This makes explicit what §3.4's Boss Skill Step-1 composition, and its
Boss-side counterpart §5.4.1 item 2 for the Pet, already establish: the
modifier applies to the stat **alone**, and a Skill/Card base value is a
separate Step-1 contribution that is **not** modified. `§3.4` is referenced,
not restated, and is not amended.

Composition: this rule fixes **which** pipeline invocations the modifier feeds,
and how it reaches them. A Boss Skill's Step 1 is composed of two separate
Step-1 contributions — `EffectiveBossATK` and the Skill's authored Base Damage
— and this modifier changes the `EffectiveBossATK` contribution only. The
composition and its consequences are owned by `§3.4` ("Boss Skill Step-1
composition") and are referenced, not restated, here. The Skill's authored
value does not receive the modifier; the Skill's Step-1 damage does, through
the `EffectiveBossATK` contribution.

### 5.5.3 Scope and Boundaries

```text
Applies to      a Turn-based BuffDebuff instance with TargetStat = "ATK"
                held in the Boss's StatusEffects[], consumed by a
                Boss attack, through that attack's Step-1
                EffectiveBossATK contribution (§5.5.1, §5.5.2)
Does NOT apply  to Step 4 — §3.4 pins the Boss side's Step 4 to 1.0 and is
                unchanged; this rule authors no Step-4 factor
Does NOT apply  to a BuffDebuff naming any stat other than "ATK"; the
                consumer reads TargetStat explicitly and a non-"ATK" value
                modifies no ATK
Does NOT apply  to a Boss Skill's independently authored Base Damage value,
                which stays at its authored magnitude and is not scaled by
                Boss ATK (§5.5.2; §3.4 "Boss Skill Step-1 composition")
Does NOT apply  to a DoT tick or a Shield: those Types have their own rules
                (§5.2 item 3, §4) and are not ATK modifiers
Does NOT        change §3.1's six-step order, §3.4's Boss-side value, §5.4's
                Pet-side rule, or §5.3's duration consumption
```

A `BuffDebuff` whose `TargetStat` names a stat for which this document defines
no consumption rule is **not** silently treated as an ATK modifier: this
section defines the `"ATK"` case only, and any other stat would require its own
recorded decision before it could be implemented — the same position §5.4.5
takes for the Pet side.

Thủy Ma's healing reduction (`BOSS_RULES.md` §6.2.2) is likewise **not** a
`TargetStat`-consumed `BuffDebuff` and opens **no** new non-`"ATK"` `TargetStat`
case. It is held in `BossState.StatusEffects[]` but is consumed by §4 item 7's
Pet-side Heal Resolution step through the authorized cross-entity read, which
selects the instance by its Status Effect `Id`; it is not consumed by a Boss
attack through a stat this section names, and no `TargetStat` value is added for
it. The boundary above is neither weakened nor exercised.

### 5.5.4 The Boss Base Stat Is Never Overwritten

The modifier is **non-destructive**, exactly as §5.4.4 is for the Pet:

- `BossState.ATK` **is never overwritten** by the modifier, and there is no
  "restore" step: the stored stat is unchanged throughout. An implementation
  that writes `BossState.ATK = BossState.ATK × 1.2` and later restores it is
  **not** this rule.
- The configured default is an **initialization value only** and is **never**
  an expiry or reset mechanism (§5.4.4's rule, applied to the Boss stat).
- `EffectiveBossATK` is a value used within **one** pipeline execution. It is
  **not** persisted, not stored in `BattleState`, and not a second
  representation of the Boss ATK stat (`GAME_STATE.md` §0 item 5). Because the
  modifier lives in the existing `StatusEffects[]` instance
  (`GAME_STATE.md` §2.3.1 item 3, item 7; §2.4.1), **no new state collection,
  member, or representation is introduced by this rule**.

### 5.5.5 Duration and Reapplication

Duration and reapplication are the **existing** rules and are **not**
re-authored here:

- **Duration** is consumed by the unchanged §5.3 Turn-based lifecycle
  (DR1–DR6). An instance applied at a documented point before
  `GAME_RULES.md` §17 step 19a consumes one Turn of duration at that Turn's
  step 19a (§5.3.1/DR6), and §5.4.3's non-retroactivity statement — an
  instance applied at a later step of the same Turn cannot affect that Turn's
  already-resolved attack — applies to a Boss-side instance identically.
- **Reapplication** follows §5.2 item 2's MVP default (**refresh duration, do
  not stack magnitude**) and `GAME_STATE.md` §2.3.1 item 6's
  one-instance-per-effect-identity rule. The refresh mechanism is §5.3
  DR3/DR4. No new stacking model is authored.

This section adds no Battle Event (`GAME_RULES.md` §16), no Status Effect
`Type`, no `TargetStat` value, no SignalR member, no Redis key, and no
database column. The Step-1 value it produces already reaches the client as
`DamageCalculated.base` (`SIGNALR_PROTOCOL.md` §3.2.13) — a value change, not
a member-set change, exactly as §5.4's closing paragraph states for the Pet
side.

---


## 5.6 Effective Pet ATK — Unified Pet ATK Modifier Composition

This section is the **canonical owner** of how all applicable Pet ATK modifiers —
both applied, Battle-lifetime ATK modifiers sourced from a **Relic** and active,
Turn-based **BuffDebuff** `TargetStat = "ATK"` modifiers — compose to modify the
active Pet's ATK. It is authored by the TASK-136 Product-Owner decision set
(D1–D12), the TASK-137 Product-Owner decision set (D1–D7), and the TASK-138
Product-Owner decision set (D1–D6).

This section is the **canonical composition model for all applicable Pet ATK
modifiers** (TASK-138 D2), including the BuffDebuff-only case (TASK-138 D1),
the Relic-only case, and the Relic + BuffDebuff coexistence case (§5.6.6).
§5.4 retains the Turn-based BuffDebuff StatusEffect consumption and lifetime
contract; this section owns the arithmetic composition of all active Pet ATK
modifiers into `EffectivePetATK`.
The state representation for Relic modifiers is `GAME_STATE.md` §2.3.7 and its
mutation lifecycle is `GAME_STATE.md` §5.1.4; Status Effect state representation is
`GAME_STATE.md` §2.3.1 and its lifecycle is `GAME_STATE.md` §5.1.1. This section
authors the gameplay composition rule and does not describe storage.

### 5.6.1 The Effective Pet ATK Composition

```text
1. Consumption point
   Player → Boss Damage Pipeline Step 1 — the `Attack` argument the call
   receives is `EffectivePetATK`, produced by this rule. Exactly ONE
   composition calculation produces EffectivePetATK for all applicable Pet ATK
   modifiers (TASK-138 D4). This rule governs even when no Relic ATK modifier is
   present (BuffDebuff-only case, TASK-138 D1).

2. The composition — signed, additive percentage points (TASK-138 D1–D4)

   TotalATKModifierPercentage =
       the signed sum of every applicable Pet ATK modifier:
       - applicable Battle-lifetime entries in PetState.ATKModifiers[]
       - applicable signed Turn-based BuffDebuff ATK entries in PetState.StatusEffects[]

   EffectivePetATK =
       truncate( PetState.ATK × (100 + TotalATKModifierPercentage) / 100 )

3. What the percentage applies to — PetState.ATK ALONE

   Step 1 = EffectivePetATK
          + Skill/Card base value
          + ATK-Gem-generated damage pool

   The Skill/Card base value and the ATK-Gem-generated damage pool are separate
   Step-1 contributions (§3 step 1) and are **not** modified by this rule.

4. Percentage application, direction, and rounding (TASK-138 D3, D5)

   Each modifier value is a **signed percentage-point contribution**:
   it is used WITH ITS OWN SIGN, so it supports both an increase and a decrease.
   - Relic ATK percentage: used with its authored sign (e.g. +5% contributes +5).
   - BuffDebuff ATK reduction: contributes -|Magnitude| signed percentage points (e.g. Root -30% contributes -30).
   - BuffDebuff ATK increase (if authored): contributes +|Magnitude| signed percentage points.
   The stored StatusEffect.Magnitude remains the existing applied magnitude and does not encode the sign.

   TotalATKModifierPercentage > 0  →  increase   EffectivePetATK > PetState.ATK
   TotalATKModifierPercentage < 0  →  decrease   EffectivePetATK < PetState.ATK
   TotalATKModifierPercentage = 0  →  unchanged  EffectivePetATK = PetState.ATK

   The formula's result is an integer, **truncated toward zero exactly once**
   after the combined calculation (TASK-138 D5) — the same integer convention
   §3 step 6 already uses. No intermediate per-modifier truncation occurs.

5. Cap — none is authored
   No ATK cap is defined by this rule. `ATK` has no documented valid range in
   §1.1 (unlike `Power`'s 0–100 and `Crit`'s 0–100 percentage points), and
   TASK-136 D5 / TASK-138 require clamping only to a **documented** valid ATK
   range and forbid inventing a new stat cap. Because no such range exists, this
   rule authors no clamp and the composed value is unbounded below by 0 and above
   by any ceiling. Authoring an ATK range is a separate balance decision
   (`GAME_RULES.md` §20) and is not made here.

6. Evaluation timing
   The composition is performed from the permanent base `PetState.ATK` plus all
   active modifiers at the moment ATK is required — i.e. when the Damage
   Pipeline call for that attack is constructed (TASK-136 D5, TASK-138 D4). It is
   not evaluated eagerly, not cached across resolutions, and not stored.
```

**Worked example** (`PetState.ATK = 50`, §1.1's MVP default; Berserker Core
`+5%`):

```text
TotalATKModifierPercentage = +5
EffectivePetATK = truncate(50 × (100 + 5) / 100) = truncate(52.5) = 52
```

With a second, simultaneous Battle-lifetime modifier of `-10`:

```text
TotalATKModifierPercentage = +5 + (-10) = -5
EffectivePetATK = truncate(50 × (100 − 5) / 100) = truncate(47.5) = 47
```

The example's magnitudes are illustrative arithmetic only; the provisioned
Relic magnitude is `RELIC_RULES.md` §8.5's and is not restated here.

### 5.6.2 Multiple Sources Compose Additively

Multiple simultaneously-active Battle-lifetime ATK modifiers are **summed**
(TASK-136 **D6**). The sum is taken over their `ATKModifierPercentage` values
with their signs, and the single `TotalATKModifierPercentage` is then applied
once through §5.6.1 item 2's formula.

```text
Correct     TotalATKModifierPercentage = Σ ATKModifierPercentage
            EffectivePetATK = truncate( ATK × (100 + Total) / 100 )

Incorrect   applying each modifier to the running result in sequence
            (that is a composition order this rule does not define, and it
            does not generally produce the same integer)
```

Each source contributes independently, so removing one source does not disturb
the base stat or any other source's contribution — the same independence §3.3
item 7 states for Effective Crit. There is no cap on the sum (§5.6.1 item 5).

### 5.6.3 Lifetime, Refresh, and Removal

The lifetime, refresh, and removal **mechanics** are owned by `GAME_STATE.md`
§5.1.4 and are not restated here. The gameplay statements are:

```text
Lifetime     Battle — a standing modification for the remainder of the battle
             (RELIC_RULES.md §8.3 item 4). Not Turn-based: no Turn countdown,
             no step 19a participation, and no expiry on Turn, Swap, or a
             non-damaging action (TASK-136 D4).
Refresh      the same source replaces/refreshes its existing modifier with the
             newly applied value; it does not create a duplicate and does not
             accumulate (TASK-136 D7, GAME_STATE.md §5.1.4 items 1–2).
Removal      when the source is removed or when the battle ends (TASK-136 D8).
Result       removing a modifier changes only the composed value; it never
             restores, rewrites, or recalculates a stored stat, because
             `PetState.ATK` was never modified (§5.6.4).
```

### 5.6.4 The Base Stat Is Never Overwritten

The modifier is **non-destructive**, following the same precedent §5.4.4 and
§5.5.4 record, and adopted here by TASK-136 **D5/D8** as an explicit decision
rather than inherited:

```text
PetState.ATK
      ↓
ATKModifiers[] entries (the active sources' signed percentages)
      ↓
EffectivePetATK        derived at attack resolution — NOT stored
      ↓
Damage Pipeline Step 1
```

- `PetState.ATK` **is never overwritten** by the modifier, and there is no
  "restore" step: the stored stat is unchanged throughout. An implementation
  that writes `PetState.ATK = PetState.ATK × 1.05` and later restores it is
  **not** this rule.
- The configured default is an **initialization value only** and is **never** an
  expiry or reset mechanism (`§5.4.4`, `§3.3` item 10).
- `EffectivePetATK` is a value used within **one** pipeline execution. It is
  **not** persisted, not stored in `BattleState`, and not a second
  representation of the ATK stat (`GAME_STATE.md` §0 item 5). It is not the
  `ATKModifiers[]` collection, and the collection is not a second `ATK` member.
- `PetState.ATK` remains the **permanent/base** stat (`TASK-136` D8).

### 5.6.5 Scope and Boundaries

```text
Applies to      all applicable Pet ATK modifiers — including applied, Battle-lifetime
                ATK modifiers in PetState.ATKModifiers[] and active Turn-based
                BuffDebuff ATK modifiers in PetState.StatusEffects[] — consumed by the
                owning Pet's own attack (Player → Boss) through that attack's
                Step-1 `Attack` input (EffectivePetATK)
Does NOT apply  to the Boss's damage — §3.4 pins the Boss side's Step 4 to
                1.0, and §5.5 is the Boss-side counterpart of §5.4, not of
                this rule
Does NOT apply  to Step 4 — this rule changes the Step-1 input only and
                authors no Step-4 factor
Does NOT apply  to a Skill/Card base value or the ATK-Gem-generated damage
                pool: those are separate Step-1 contributions (§3 step 1) and
                are not modified (§5.6.1 item 3)
Does NOT apply  to a DoT tick or a Shield: those have their own rules (§5.2
                item 3, §4) and are not ATK modifiers
Does NOT        change §3.1's six-step order, §3.4's Boss-side value, or
                §5.3's duration consumption. §5.6 does not replace §5.4's
                BuffDebuff lifecycle/consumption semantics; §5.6 owns the
                arithmetic composition of an active BuffDebuff ATK contribution
                (TASK-138 D2)
```

This section adds no Battle Event (`GAME_RULES.md` §16), no Status Effect
`Type`, no `TargetStat` value, no SignalR member, no Redis key, and no database
column. The Step-1 value it produces already reaches the client as
`DamageCalculated.base` (`SIGNALR_PROTOCOL.md` §3.2.13) — a value change, not a
member-set change, exactly as §5.4's closing paragraph states for that rule.

It introduces no new gameplay mechanic beyond the composition TASK-136 decided:
no new `effectType`, no new Trigger or Condition, no new Relic, and no new
magnitude. The provisioned magnitude and lifetime are `RELIC_RULES.md` §8.5's.

### 5.6.6 Unified Pet ATK Composition Contract — Canonical Composition Model (TASK-137 / TASK-138)

This section is the **canonical owner** of how all applicable Pet ATK modifiers
compose to produce the active Pet's `EffectivePetATK` used as the Player → Boss
Damage Pipeline Step 1 `Attack` input. It is authored by the **TASK-137
Product-Owner decision set (D1–D7)** and the **TASK-138 Product-Owner decision
set (D1–D6)**.

§5.6.6 is the **canonical composition model for all applicable Pet ATK modifiers**
(TASK-138 D2). It governs:
1. When Relic ATK modifiers and Turn-based `BuffDebuff` ATK modifiers coexist on the same attack.
2. When only Turn-based `BuffDebuff` ATK modifiers are active (no Relic equipped, TASK-138 D1).
3. When only Relic ATK modifiers are active.

Historical §5.4.1 absolute-value composition is superseded/narrowed and no longer
defines a separate calculation path (TASK-138 D2). §5.4 retains the Turn-based
BuffDebuff StatusEffect consumption and lifetime contract; only its contribution
to `EffectivePetATK` is governed by this unified composition model (TASK-138 D2).

Under this unified contract:

#### 1. Unified Model and Coexistence (TASK-137 D1, TASK-138 D1, D2)

- §5.6.6 governs all Pet ATK composition cases, including when no Relic ATK modifier is active (D1).
- When both a Relic Battle-lifetime ATK modifier and a Turn-based `BuffDebuff` ATK modifier are present, they **coexist** and both affect `EffectivePetATK`. Neither modifier suppresses, preempts, or invalidates the other.

#### 2. Composition Operator and Sign Semantics (TASK-137 D2, TASK-138 D3)

The modifiers are composed as a **single signed percentage adjustment** against
the permanent base `PetState.ATK`.

All applicable Pet ATK modifiers are summed with their documented signs:

```text
TotalATKModifierPercentage =
    Σ (applicable Relic ATKModifierPercentage entries in PetState.ATKModifiers[])
  + Σ (applicable signed Turn-based BuffDebuff ATK entries in PetState.StatusEffects[])
```

- **Positive ATK modifier = increase** (e.g. Berserker Core `+5%` contributes `+5`).
- **Negative ATK modifier = decrease** (e.g. Mộc Yêu Root `-30%` Pet ATK debuff contributes `-30`).
- **BuffDebuff sign mapping (TASK-138 D3):**
  - A `BuffDebuff` ATK reduction contributes ` -|Magnitude| ` signed percentage points to `TotalATKModifierPercentage`.
  - A `BuffDebuff` ATK increase, if introduced by an authoritative gameplay rule, contributes ` +|Magnitude| ` signed percentage points.
  - The stored `StatusEffect.Magnitude` remains the existing applied magnitude (e.g. `30` for Root) and does not itself encode the sign (`GAME_STATE.md` §2.3.1 item 2).
- The historical absolute value convention (`|Magnitude|`) from §5.4.1 is **not** used as a separate reduction-only calculation path.
- **No new ATK cap is introduced:** consistent with §5.6.1 item 5 and §1.1, neither the composed percentage nor the resulting ATK is clamped to an invented range.

#### 3. Order Independence (TASK-137 D3)

Composition is **order-independent**. All applicable modifier sources are combined
into `TotalATKModifierPercentage` before applying to Base Pet ATK. Modifiers must
**not** be applied sequentially in an order-dependent pipeline
(e.g. applying Relic percentage first then `BuffDebuff` percentage to an intermediate
value, or vice versa, is forbidden).

#### 4. Single Truncation Point (TASK-137 D4, TASK-138 D5)

Apply the combined signed percentage to Base Pet ATK and **truncate toward zero exactly
once** when producing integer `EffectivePetATK`.
**No intermediate per-modifier truncation occurs** (TASK-138 D5). Applying modifiers
sequentially with intermediate truncation is prohibited.

#### 5. EffectivePetATK Formula (TASK-137 D5, TASK-138 D4)

Use **one unified composition calculation** to produce `EffectivePetATK` for all
applicable Pet ATK modifiers (TASK-138 D4). No second stored or derived ATK representation
(`EffectiveBuffDebuffATK`, `EffectiveRelicATK`, etc.) is introduced.

```text
EffectivePetATK =
    truncate(
        PetState.ATK
        × (100 + TotalATKModifierPercentage)
        / 100
    )
```

The resulting integer `EffectivePetATK` is passed directly as the Step 1 `Attack`
argument to the Player → Boss Damage Pipeline (§3 step 1).

#### 6. Numerical Contract / Worked Examples

##### Example 1: Relic + BuffDebuff Coexistence
Provisioned MVP battle against Mộc Yêu (`BOSS_RULES.md` §6.3.1 item 3) with
Berserker Core equipped (`RELIC_RULES.md` §8.5):

```text
Base Pet ATK (PetState.ATK) = 50   (COMBAT_RULES.md §1.1 MVP default)
Berserker Core modifier      = +5%  (PetState.ATKModifiers[])
Mộc Yêu Root modifier        = -30% (PetState.StatusEffects[], BuffDebuff TargetStat = "ATK")

TotalATKModifierPercentage = (+5) + (-30) = -25

EffectivePetATK = truncate( 50 × (100 + (-25)) / 100 )
                = truncate( 50 × 75 / 100 )
                = truncate( 37.5 )
                = 37
```
**The resolved integer result is exactly 37.**
The divergence between sequential application models (which yielded 36) and combined
percentage (which yields 37) is authoritatively resolved: sequential application is
prohibited (D3), and single truncation of the combined percentage (D4/D5) produces **37**.

##### Example 2: BuffDebuff-Only (TASK-138 D1)
Mộc Yêu Root active with no Relic equipped:

```text
Base Pet ATK (PetState.ATK) = 50
Mộc Yêu Root modifier        = -30% (contributes -|30| = -30)

TotalATKModifierPercentage = -30

EffectivePetATK = truncate( 50 × (100 + (-30)) / 100 )
                = truncate( 50 × 70 / 100 )
                = truncate( 35.0 )
                = 35
```
**The resolved integer result is exactly 35.**

##### Example 3: Relic-Only
Berserker Core active with no debuff:

```text
Base Pet ATK (PetState.ATK) = 50
Berserker Core modifier      = +5%

TotalATKModifierPercentage = +5

EffectivePetATK = truncate( 50 × (100 + 5) / 100 )
                = truncate( 50 × 105 / 100 )
                = truncate( 52.5 )
                = 52
```
**The resolved integer result is exactly 52.**

##### Example 4: Multi-Modifier Divergence Illustration
Two simultaneous BuffDebuff instances (`-30%` and `-10%`), `PetState.ATK = 50`:

```text
Unified Combined Calculation (Authoritative):
TotalATKModifierPercentage = (-30) + (-10) = -40
EffectivePetATK = truncate( 50 × (100 + (-40)) / 100 )
                = truncate( 50 × 60 / 100 )
                = truncate( 30.0 )
                = 30

Sequential Intermediate Truncation (Prohibited):
truncate(50 × 70 / 100) = 35
truncate(35 × 90 / 100) = truncate(31.5) = 31  (diverges: 31 ≠ 30)
```
Single truncation of the combined signed percentage produces **30**.

#### 7. Independent Carrier Ownership and Lifetime (TASK-137 D6)

The two modifier carriers retain **independent ownership and lifetime**:

```text
PetState.ATKModifiers[]   ≠   PetState.StatusEffects[]
(Relic Battle lifetime)       (Turn-based BuffDebuff lifetime)
```

- When Mộc Yêu Root expires (at `GAME_RULES.md` §17 step 19a via §5.3 DR1–DR6),
  **only its `StatusEffects[]` entry is removed**.
- The Berserker Core `ATKModifiers[]` entry remains active for the rest of the
  Battle (removed only at battle end or source removal per `GAME_STATE.md` §5.1.4 item 4).
- A modifier is **never** removed merely because another modifier expires.
- Expiry of one carrier does not disturb, refresh, or mutate the other carrier.
- Upon expiry of one source, subsequent attacks derive `EffectivePetATK` from the
  remaining active modifiers without any history or reset mechanism.

#### 8. Base PetState.ATK Independence (TASK-137 D7, TASK-138 D6)

`PetState.ATK` remains the permanent/base Pet ATK value:

- Neither Relic ATK modifiers nor `BuffDebuff` ATK modifiers may overwrite,
  mutate, restore, or reset `PetState.ATK` (§5.4.4, §5.6.4, TASK-138 D6).
- No `DefaultATK`-style runtime reset mechanism exists or is introduced.
- `EffectivePetATK` remains **derived state** computed at calculation time;
  it is never persisted, never stored in `PetState` or `BattleState`, and does
  not create a second stored ATK representation (`GAME_STATE.md` §0 item 5).

# 6. Power

Range, generation triggers, and spend/reject rules are defined in
GAME_RULES.md §12 (range/cap invariant) and CARD_RULES.md §3 (cast
validation and rejection). This document owns only the numeric generation
rates per Gem/match-tier (§2 above) — it does not restate the cap or cast
validation flow to avoid duplicating those two owning documents.

---

# 7. Player XP & Level Progression

This section is the **canonical owner** of the Player XP → Player Level
progression contract. Other documents reference it; they do not restate it
(`.ai/workflow/documentation/documentation-change.md` §2).

## 7.1 Two Progression Tracks

```text
Player XP / Level   →  account / meta / content progression  (this section)
Pet XP / Level      →  combat-character progression          (PET_RULES.md §5)
```

The two tracks are **independent**. Player Level is not an input to Pet
Level, and Pet Level is not an input to Player Level. The Player is the
account/owner; a Pet is a combat character. `ADR-016` records why this
ownership split was chosen; `GAME_RULES.md` §9.3 states the Pet rule.

## 7.2 Player XP Reward (battle outcome)

```text
BattleWon   →  Player XP +100
BattleLost  →  Player XP +0
```

A battle awards XP as a single outcome-based amount, applied server-side on
the battle-end path (`GAME_RULES.md` §18). The client never determines the
amount granted (`GAME_RULES.md` §18, ADR-001).

## 7.3 Reward Amount vs. Curve Constant

These are **two independent concepts** and must never be collapsed into
one value. Both currently equal `100`; that equality is a balance
coincidence, not a shared definition.

```text
Player XP reward amount       = 100   (MVP initial value — CONFIGURATION)
XP-per-level curve constant   = 100   (part of the progression FORMULA)
```

These two concepts are also recorded separately: the reward figures in
`DATABASE.md` §1/§3 and the formula in `DATABASE.md` §3 cite this section
for both.

- The **reward amount** is a configuration value: the MVP initial amount
  granted by a won battle (§7.2). Retuning it is a balance change
  (`ROADMAP.md` Phase 3 configurable-value pass), not a rule change.
- The **curve constant** is a gameplay formula constant: the divisor in the
  Level formula (§7.4). It defines the shape of progression and is not a
  balance tuning knob of the same kind.

Changing either one must never silently rewrite the other.

## 7.4 Player Level Formula

```text
Player.Level = min(floor(Player.XP / 100), 49) + 1

equivalently:

Player.Level = min(floor(Player.XP / 100) + 1, 50)
```

The divisor `100` is the curve constant of §7.3. The formula is
deterministic and total: every non-negative integer `Player.XP` yields
exactly one Player Level.

Worked boundaries (authoritative):

```text
Player.XP = 0      →  Level 1
Player.XP = 100    →  Level 2
Player.XP = 400    →  Level 5
Player.XP = 4900   →  Level 50
Player.XP = 5000   →  Level 50
Player.XP = 10000  →  Level 50
```

## 7.5 XP Is Uncapped; Level Is Capped

1. **`Player.XP` is uncapped.** It is cumulative lifetime progression and
   keeps accumulating after Level 50. There is no XP ceiling, no XP reset,
   and no XP discard at the cap.
2. **`Player.Level` is capped at 50.** The `min(…, 50)` term of §7.4 is the
   whole cap: once the curve reaches 50 the Level stops increasing while XP
   continues to grow.
3. **Initial values.** A newly created Player has `Player.XP = 0` and
   `Player.Level = 1` (§7.4 at `XP = 0`). Both are persisted
   (`DATABASE.md` §1).
4. **Defeat changes nothing.** A `BattleLost` grants `+0` Player XP (§7.2)
   and therefore causes no Level change.
5. **Do not invent post-50 progression.** Prestige, Paragon, Season XP,
   additional XP currencies, and any other post-Level-50 progression system
   are **not** part of this contract and must not be introduced without a
   new human gameplay decision (`GAME_RULES.md` §20, `MVP_SCOPE.md` §4). XP
   continuing to accumulate past Level 50 (item 1) is the entirety of the
   documented post-50 behavior.

## 7.6 Player Level Has No Combat Stats

Player Level is an account/meta progression value only. It carries **no
combat stats** — there is no `PlayerAttack`, `PlayerDefense`, `PlayerHP`,
`PlayerCrit`, `PlayerPower`, or equivalent. Battle-time HP/ATK/DEF/Crit/
Power belong to the active Pet's `PetState` (`GAME_STATE.md` §2.3, §1.1,
ADR-011). Player Level grants no combat modifier of any kind.

---

# 8. Determinism & Authority

All formulas in this document execute server-side. No client-submitted value
for Base Damage, Modifiers, Defense, Final Damage, Player XP, or Player
Level is ever authoritative (`GAME_RULES.md` §18). The client renders the
`DamageCalculated` / `DamageDealt` / `DamageTaken` events
(`GAME_RULES.md` §16) produced by the server, together with the reward
summary carried by `BattleWon` / `BattleLost` (`GAME_EVENTS.md` §2).
