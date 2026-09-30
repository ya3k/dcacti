# Combat Rules

**Version:** 1.6 (§4 — the Shield rule changed from additive stacking to
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

This section is the **canonical owner** of the Shield rule — its application,
refresh, absorption, and depletion. Other documents reference it; they do not
restate it (`.ai/workflow/documentation/documentation-change.md` §2). Shield is
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
          measured in Turns unless stated otherwise
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
3. Damage-over-time ticks (Burn) go through the Damage Pipeline (§3) using the
   Effect's own Element, but do not consume Combo (Combo Modifier step uses
   Combo = 1 / neutral for DoT ticks, since a DoT tick is not itself part of a
   Swap's Combo chain).

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
```

### 5.3.4 No Change to Damage-over-time or Cooldown Rules

This section fixes the consumption point for **duration-based Buff/Debuff**
Status Effects. It does not change Burn's tick schedule, which
`BOSS_RULES.md` §6.3.1 item 1 owns and which is consistent with DR1–DR5. It
does not change `BossState.SkillCooldown`'s separate decrement rule
(`BOSS_RULES.md` §6.3, `GAME_STATE.md` §2.4.3) — that is a Boss Skill counter,
not a Status Effect, and no state field here adopts it.

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
