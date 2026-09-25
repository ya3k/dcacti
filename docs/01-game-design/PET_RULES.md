# Pet Rules

**Version:** 1.4 (§5 derivation contract completed — multiplier type
`decimal`, range `> 0`, `floor` rounding, floor → clamp order, one
canonical rule for stored and battle Pet Level, concrete MVP multipliers
deferred; prior 1.3: §5 item 8 added — a newly created Player starts at
Level 1; prior 1.2: §5 resolved — Player Level defined in MVP, Pet Level =
clamp(Player Level × Pet Level Multiplier, 1, 50), Tier/Star remain
independent, Evolution out of scope; §5.1 OPEN conflicts closed per
ADR-012; prior 1.1: §2 ownership clarified)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

Expands GAME_RULES.md §9 (Pet Rules). Conflicts resolve in favor of
GAME_RULES.md.

---

# 1. Pet Data Model

```text
Pet
├── Identity           (unique name/id, e.g. "Thanh Xà")
├── Element            (exactly one of the Five Elements)
├── Tier                (Common / Rare / Epic / Legendary / Mythic)
├── Star                (1–5)
├── Level               (1–50)
├── Stats               (HP, ATK, DEF, Crit, ... derived from Tier/Star/Level)
├── Passive             (see PASSIVE_RULES.md)
└── Signature Skill      (see CARD_RULES.md §2, Pet Skill Card)
```

---

# 2. Ownership & Selection

1. A player (the account/owner) may own an arbitrary number of Pets
   (collection), Relics, and Cards.
2. Exactly one Pet is selected as "active" per battle (GAME_RULES.md §9.2).
   The Player is the owner; the active Pet is the combat character — its
   HP, battle stats (ATK/DEF/Crit/Power), Status Effects, equipped Relics,
   and equipped Cards are what the battle uses. Authoritative battle-time
   values live in `PetState` (`GAME_STATE.md` §2.3); there is no separate
   Player combat-state pool.
3. Selecting a Pet locks in that Pet's Element, Passive, and Signature Skill
   for the duration of the battle. Mid-battle Pet swapping is out of MVP
   scope.
4. Relic loadout: the Player equips 3–5 owned Relics onto the active Pet
   before battle start (`RELIC_RULES.md` §2). Relics are a per-Pet loadout
   for that battle, not a global Player loadout.

---

# 3. Tier

```text
Common → Rare → Epic → Legendary → Mythic
```

Rules (GAME_RULES.md §9.8–9.9):

1. Higher Tier must NOT be implemented as a flat statistical multiplier over
   the same Passive/Skill. That produces a "power creep clone," which is
   explicitly disallowed.
2. A higher Tier may differ from a lower Tier via any combination of:
   * Higher base stats (allowed, but not the *only* differentiator).
   * A different or enhanced Passive threshold/effect.
   * A stronger or mechanically distinct Signature Skill.
   * A distinct strategic identity (e.g. a Legendary version of a Pet may
     lean into Crit/burst instead of sustain, even if the Common version
     leans sustain).
3. Tier does not change a Pet's Element.
4. Whether Tier variants are the *same* Pet Identity re-skinned at a higher
   power budget, or fully distinct Pet Identities that happen to share a
   theme, is a content decision outside this rules document. MVP ships one
   Tier instance per the 5 MVP Pets (see §6); the multi-Tier system described
   here is the rule framework for future Pet additions.

---

# 4. Star

```text
Range: 1–5
```

1. Star is a progression axis distinct from Tier and Level.
2. Star may improve Pet power (stat growth and/or minor Passive/Skill
   magnitude increases), per GAME_RULES.md §9.6.
3. Exact star-up costs, materials, and stat curves are Meta Progression
   concerns (GDD §14) and are not defined in this Battle-facing rules
   document.

---

# 5. Level

```text
Player Level range:  1–50   (persistent account attribute)
Pet Level range:     1–50   (clamped result of the formula below)
```

1. Pet Level is derived from the account's Player Level. The **canonical
   rule** is exactly:

   ```text
   Pet.Level = clamp(
       floor(Player.Level × PetDefinition.PetLevelMultiplier),
       1,
       50
   )
   ```

   Operation order (this sequence and no other):

   ```text
   1. Calculate Player.Level × PetDefinition.PetLevelMultiplier
   2. Apply floor to the product
   3. Clamp the floored result to [1, 50]
   4. Result is Pet.Level
   ```

   `Pet Level Multiplier` (`PetDefinition.PetLevelMultiplier`) is a
   per-Pet **configuration value** owned by `PetDefinition` (never
   hard-coded inside gameplay logic). Its semantics are:

   ```text
   Type:  decimal
   Range: > 0        (values below 1 are legal — a Pet may lag its owner;
                      zero and negative values are not)
   ```

   Rounding is **`floor`**, applied to the product **before** the clamp.
   The clamp then bounds the floored result to the 1–50 range above; it
   does not re-define Player Level's own 1–50 range and does not
   supersede either range. There is no round-after-clamp step: clamp is
   always the last operation.

   Worked examples (authoritative):

   ```text
   Player.Level = 3,  Multiplier = 1.5  →  3 × 1.5 = 4.5  → floor = 4  → clamp = 4
   Player.Level = 40, Multiplier = 2    →  40 × 2 = 80    → floor = 80 → clamp = 50
   Player.Level = 1,  Multiplier = 0.5  →  1 × 0.5 = 0.5  → floor = 0  → clamp = 1
   ```

2. **One canonical rule for every representation of Pet.Level.** The
   formula and operation order in item 1 are the single derivation rule
   for Pet Level. They apply identically to:

   ```text
   persistent Pet representation   (Pet.Level stored with the Pet instance —
                                    DATABASE.md §1)
   BattleState.PetState.Level      (battle-time Level under BattleState —
                                    GAME_STATE.md §2.3)
   ```

   Implementation must not define a different battle-time formula,
   rounding rule, or clamp order. The battle-time Level is a snapshot of
   the same derived value, not a second derivation path.

3. **Concrete MVP Pet Level Multiplier values are deferred.** The
   five MVP Pets' `PetLevelMultiplier` numbers are balance/configuration
   values (same classification as the exact Player Level XP curve —
   ROADMAP Phase 3 balance pass on all configurable values). They are
   not defined in this document. Do not invent them in rules, tasks, or
   code comments.

4. Pets have no independent XP progression — there is no Pet XP bar, no
   XP gain from battles, and no Pet-level-up action. Player Level itself
   increases through Meta Progression battle Rewards (GDD §14,
   `MVP_SCOPE.md` §1); the exact XP curve is a balance/config concern and
   is not defined in this document.

5. Player Level carries **no combat stats**. It is an account-level
   progression value only; battle-time HP/ATK/DEF/Crit/Power live on
   `PetState` (`GAME_STATE.md` §2.3, ADR-011).

6. Level primarily scales base stats (HP/ATK/DEF) via a stat curve.
7. Level does not change Element, Tier, Passive trigger type, or Signature
   Skill identity — only magnitude, where applicable.
8. Exact level curve (linear/exponential/tabled) is a balance concern defined
   in COMBAT_RULES.md / config, not here.
9. **Tier and Star remain independent progression axes.** They are not
   derived from Player Level. Only Pet Level is account-derived
   (resolution recorded in §5.1).
10. **A newly created Player starts at Level 1.** This is the documented
    initial value of the `Player.Level` attribute defined above — the
    value a Player row carries when it is first created. It is an
    initial-value rule only: it defines no XP amount, no XP curve, no
    level-up threshold, and no rate of increase. How Player Level
    increases is stated at the mechanism level in item 4, and the
    increase curve remains undefined (item 4, §5.1 item 4).

## 5.1 Former OPEN Conflicts — Resolved (ADR-012)

The three conflicts previously reported under this heading are resolved
as follows, and item 4 records the initial value added in version 1.3;
this document no longer carries open items in §5:

1. **Player Level is defined for MVP.** Range 1–50, persistent on the
   Player account (`DATABASE.md` §1), listed IN in `MVP_SCOPE.md` §1,
   increases via battle Rewards (Meta Progression). No combat stats
   (§5 item 5).
2. **The 1–50 level cap clamps the formula result.** `Pet.Level =
   clamp(floor(Player.Level × PetDefinition.PetLevelMultiplier), 1, 50)`
   (§5 item 1 — floor before clamp). Both Player Level and Pet Level
   independently respect 1–50.
3. **Tier and Star are not account-derived.** They remain independent
   axes alongside the account-derived Level (§5 item 9);
   `PET_RULES.md` §3–§4 are unchanged.
4. **The initial Player Level is specified.** A newly created Player
   starts at Level 1 (§5 item 10). This closes the one value the 1–50
   range statement left open: a range defines the legal values an
   attribute may hold, not the value it holds at creation. Item 10
   records the initial value only; the increase curve remains a
   balance/config concern and is not defined here (§5 item 4).

**Evolution is out of scope.** No Evolution system exists in any rule
document; if introduced later it must go through `GAME_RULES.md` §20 and
`MVP_SCOPE.md` §4 (FUTURE by default). There is no Level/Evolution
interaction to specify.

---

# 6. Stat Composition

Final in-battle Pet stats are derived from all progression axes combined:

```text
Final Stat = f(Base Stat[Tier], Level Curve[Level], Star Bonus[Star])
```

Here `Level` is the Pet Level defined in §5 (`clamp(floor(Player Level × Pet
Level Multiplier), 1, 50)`, config). The exact function `f` is a
balance/config concern. This document only fixes that all three axes
(Tier, Level, Star) contribute, and that none of them alone is the sole
source of power growth (reinforcing GAME_RULES.md §9.8).

---

# 7. Pet Identity Principle

Per GAME_RULES.md §9.9 and GDD §21, a Pet's identity must come primarily from
the combination of:

```text
Element + Passive + Signature Skill + Statistics
```

not from statistics alone. Any new Pet design must define a distinct Passive
trigger/effect and a distinct Signature Skill before it is considered
complete — a Pet cannot be added to the game as "existing Pet + higher
numbers."

---

# 8. MVP Pets

```text
Pet         Element   Passive (see PASSIVE_RULES.md)         Signature Skill (see CARD_RULES.md)
---------   -------   --------------------------------       ------------------------------------
Thanh Xà    Mộc       Every 7 Matches → Restore 8% HP         (Signature Skill: TBD content)
Xích Lang   Hỏa       Every 5 Matches → Empower + Burn        Inferno
Sơn Hùng    Thổ       Every 5 Matches → Temp Defense          (Signature Skill: TBD content)
Bạch Hổ     Kim       Every 4 Matches → Crit chance up        Iron Fang
Huyền Quy   Thủy      Every 6 Matches → Shield 15% Max HP     Tidal Barrier
```

Each MVP Pet ships as a single Tier instance for MVP; the Tier/Star/Level
system above governs how these (and future) Pets scale, not how many Tier
variants exist at MVP launch (GAME_RULES.md §19 scope: "5 Pets").
