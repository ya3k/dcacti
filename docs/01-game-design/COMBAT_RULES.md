# Combat Rules

**Version:** 1.0
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §14 (Combat Rules) and §12 (Power Rules). Conflicts
resolve in favor of GAME_RULES.md.

---

# 1. Combat Stats

## 1.1 Player / Pet Stats

```text
HP        current health                          MVP default: 1000
Max HP    maximum health                          MVP default: 1000
ATK       attack power                            MVP default: 50
DEF       defense                                 MVP default: 25
Power     resource for casting Cards / Skills, range 0–100
Crit      critical hit chance (%)                 MVP default: 5%
Status    active Status Effects (see §5)
```

These are the MVP baseline configuration values. They are not permanent
invariants — future Pet progression (Level, Star, Tier) may produce different
actual Battle Stats. Combat formulas consume the current Battle Stats rather
than assuming these exact numbers permanently. Changing balance values is a
configuration change, not a combat pipeline redesign.

## 1.2 Boss Stats

```text
HP, Max HP, ATK, DEF, Element, Passive, Skill, State
```

Boss "State" is an internal enum (e.g. Idle / ChargingSkill / Enraged) used by
Boss mechanics; see BOSS_RULES.md.

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

## 3.3 Critical Hits

1. Crit is evaluated once per damage instance, after Element Modifier and
   before Defense Mitigation (i.e., inside "Other Modifiers", step 4).
2. On a successful Crit roll, damage is multiplied by a Crit Multiplier
   (default: 1.5×, configuration).
3. Crit chance can be modified by Relics (e.g. "Assassin Eye": Combo ≥ 3 →
   increase Crit chance) and Pet Passives (e.g. Bạch Hổ: next attack gains
   increased Crit chance).

---

# 4. Healing and Shields

1. Heal effects restore HP up to Max HP; overheal is discarded unless a Relic
   explicitly grants overheal/temp-HP.
2. Shield effects grant an absorption pool that reduces incoming damage before
   HP is affected, consumed first-in on any Final Damage applied to that
   target.
3. Multiple Shields stack additively into a single absorption pool unless a
   Relic specifies otherwise.
4. Heal and Shield amounts are NOT subject to the Damage Pipeline (§3) —
   they are not damage — but they ARE subject to their own explicit
   modifiers (e.g. a Relic that increases Heal Card effectiveness).

---

# 5. Status Effects

## 5.1 MVP Status Effects

```text
Burn      damage-over-time, ticks each Turn (or configured interval),
          Element = Hỏa for Element Modifier purposes
Shield    absorption pool, see §4
Buff/Debuff  temporary stat modification (ATK/DEF/Crit/etc.), with duration
          measured in Turns unless stated otherwise
```

## 5.2 Status Rules

1. Every Status Effect has a source, a magnitude, and a duration (in Turns) or
   a trigger-based expiry (e.g. "until Shield is depleted").
2. Stacking behavior (refresh duration vs. stack magnitude vs. independent
   instances) is defined per-effect; default for MVP is **refresh duration,
   do not stack magnitude** unless a Card/Relic explicitly says otherwise
   (e.g. "Burning Curse" increases Burn damage, which is a magnitude modifier
   on the existing Burn, not a second stack).
3. Damage-over-time ticks (Burn) go through the Damage Pipeline (§3) using the
   Effect's own Element, but do not consume Combo (Combo Modifier step uses
   Combo = 1 / neutral for DoT ticks, since a DoT tick is not itself part of a
   Swap's Combo chain).

---

# 6. Power

Range, generation triggers, and spend/reject rules are defined in
GAME_RULES.md §12 (range/cap invariant) and CARD_RULES.md §3 (cast
validation and rejection). This document owns only the numeric generation
rates per Gem/match-tier (§2 above) — it does not restate the cap or cast
validation flow to avoid duplicating those two owning documents.

---

# 7. Determinism & Authority

All formulas in this document execute server-side. No client-submitted value
for Base Damage, Modifiers, Defense, or Final Damage is ever authoritative
(GAME_RULES.md §18). The client renders the `DamageCalculated` /
`DamageDealt` / `DamageTaken` events (GAME_RULES.md §16) produced by the server.
