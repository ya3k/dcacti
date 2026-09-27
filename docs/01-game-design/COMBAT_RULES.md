# Combat Rules

**Version:** 1.4 (§8 added — Player XP / Level progression contract: the
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
Crit      critical hit chance (%)                 MVP default: 5%
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

1. Crit is evaluated once per damage instance, after Element Modifier and
   before Defense Mitigation (i.e., inside "Other Modifiers", step 4).
2. On a successful Crit roll, damage is multiplied by a Crit Multiplier
   (default: 1.5×, configuration).
3. Crit chance can be modified by Relics (e.g. "Assassin Eye": Combo ≥ 3 →
   increase Crit chance) and Pet Passives (e.g. Bạch Hổ: next attack gains
   increased Crit chance).

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

The `DamageCalculated` event reports the full breakdown; `DamageDealt` and
`DamageTaken` report the final amount with `source = "boss"` and
`target = "player"`. The value `"player"` is a fixed wire label
identifying the player's side (the active Pet) — it does not imply a
separate Player HP pool (`SIGNALR_PROTOCOL.md` §3.2).

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
