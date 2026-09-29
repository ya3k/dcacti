# Pet Rules

**Version:** 3.1 (§1 Pet identity model recorded per TASK-082 decision B /
R2-10 — `PetDefinitionId` is the technical identity, `Identity` is display
text, no new column; §8 MVP provisioned/deferred row set recorded per
TASK-082 decision A — 3 Pets with content-defined Signature Skills
provisionable, the 2 TBD-Skill Pets deferred; prior 3.0: §5 rewritten and
**finalized** — the twelve Pet XP
progression/reward decisions are now decided, not open: Pet.XP persists per
instance, initial `0`, initial Level `1`, range 1–50, `BattleWon` grants the
active combat Pet `+100`, `BattleLost` `+0`, inactive owned Pets `+0`,
`Pet.Level = min(floor(Pet.XP / 100) + 1, 50)`, and Pet XP is hard-capped at
`4900` (a deliberate divergence from the uncapped Player track). §5.2's open
list is replaced by §5.2–§5.5; retired terms moved to §5.6; non-XP
attributes moved to §5.7. Prior 2.0: §5 rewritten — Pet XP / Pet Level are
now an independent
two-track progression owned by the Pet instance, superseding the
`Player.Level × PetDefinition.PetLevelMultiplier` derivation, which is
**RETIRED**; the twelve open Pet XP balance decisions are recorded in §5.2
as unresolved human gameplay decisions; superseded decisions recorded in
ADR-016. Prior 1.4: §5 derivation contract completed — multiplier type
`decimal`, range `> 0`, `floor` rounding, floor → clamp order, one
canonical rule for stored and battle Pet Level, concrete MVP multipliers
deferred; prior 1.3: §5 item 8 added — a newly created Player starts at
Level 1; prior 1.2: §5 resolved — Player Level defined in MVP, Pet Level =
clamp(Player Level × Pet Level Multiplier, 1, 50) (**RETIRED** as of 2.0 —
see `ADR-016`), Tier/Star remain
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
├── XP                  (per-instance progression; feeds Level — §5;
│                        0–4900, hard-capped — §5.5)
├── Level               (per-instance progression derived from this Pet's
│                        own XP — §5.4; range 1–50)
├── Stats               (HP, ATK, DEF, Crit, ... derived from Tier/Star/Level)
├── Passive             (see PASSIVE_RULES.md)
└── Signature Skill      (see CARD_RULES.md §2, Pet Skill Card)
```

`XP` and `Level` belong to the **owned Pet instance**, not to
`PetDefinition` (§5.1, `DATABASE.md` §1).

**Identity model.** (TASK-082 decision B / R2-10) `PetDefinitionId` is
the Pet's **technical identity** (its value form is owned by
`DATABASE.md` §1); `PetDefinition.Identity` remains the **display
identity** and continues to project the display text defined by
`API_CONTRACTS.md` §5.1 (e.g. "Xích Lang"). No separate
technical-identity column or new Pet identity concept is introduced —
the TASK-046 three-way distinction stands: display name →
`PetDefinition.Identity`; technical identity → `PetDefinitionId`;
persistence identity → the database primary key.

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
Pet Level range:     1–50   (§5.5)
```

This section is the **canonical owner** of the Pet XP → Pet Level
progression contract. Other documents reference it; they do not restate it
(`.ai/workflow/documentation/documentation-change.md` §2). The twelve
decisions below were finalized by explicit product-owner decision; `ADR-016`
records why the two-track ownership split exists.

## 5.1 Pet XP and Pet Level Ownership

1. **A Pet instance owns its own XP.** `Pet.XP` is a per-instance
   progression attribute stored on the owned Pet instance
   (`DATABASE.md` §1) — not on `PetDefinition` and not on the Player.
2. **A Pet instance owns its own Level.** `Pet.Level` is a per-instance
   progression attribute of the same owned Pet.
3. **Pet Level is independent of Player Level.** Player Level is not an
   input to Pet Level. Player owns account/meta progression
   (`COMBAT_RULES.md` §7, ADR-016); the Pet owns independent
   combat-character progression.
4. **Pet Level is a function of Pet XP.** Pet Level is derived from the
   Pet's own accumulating XP (§5.4) — not from any Player attribute and not
   from `PetDefinition`.
5. **Two independent tracks.** Player XP → Player Level and Pet XP → Pet
   Level are separate tracks that neither read nor modify each other
   (ADR-016). They share a formula *shape* (§5.4) but no variable, no pool,
   and no stored value.
6. **Pet XP persists permanently with the Pet instance.** It is not reset
   by battle outcome, by changing the active Pet, or by any other event.
   `PetDefinition` remains static template data and never holds instance
   progression.

## 5.2 Initial Values

```text
A newly created PlayerPet:
    Pet.XP    = 0
    Pet.Level = 1
```

`Pet.XP = 0` yields `Pet.Level = 1` under the §5.4 formula, so the two
initial values are consistent by construction, not by separate tuning.

## 5.3 Pet Battle Reward Targeting Semantics

This subsection is the canonical owner of the Pet reward rule. Items 5–8 of
the finalized decision set are **one** coherent rule, stated here end-to-end:

```text
BattleWon
 ├── Player    receives  +100 Player XP   (Player track — COMBAT_RULES.md §7)
 └── Active combat Pet receives  +100 Pet XP

BattleLost
 ├── Player    receives  +0 Player XP     (Player track — COMBAT_RULES.md §7)
 └── Active combat Pet receives  +0 Pet XP

Inactive owned Pets
 └── +0 Pet XP from that battle
```

1. **Only the active combat Pet receives battle XP.** The recipient is the
   Pet associated with that battle's combat state — the same Pet that
   fought (`GAME_STATE.md` §2.3). One battle awards Pet XP to exactly one
   Pet.
2. **A `BattleLost` grants the active combat Pet `+0` Pet XP.** This is an
   explicit Pet decision; it is not inherited from the Player track's `+0`.
3. **Inactive owned Pets receive `+0` Pet XP from that battle.** There is
   **no** passive XP, **no** shared XP, **no** party-wide XP, and **no**
   account-wide Pet XP distribution.
4. **To train a Pet, that Pet must be the active combat Pet.** There is no
   alternative progression path.
5. **No other reward cases exist.** A draw, timeout, disconnect, or
   spectator state grants no Pet XP, and no additional reward case may be
   introduced without a new human gameplay decision (`GAME_RULES.md` §20).

**Player XP is unaffected by this subsection.** The Player reward is owned
by `COMBAT_RULES.md` §7 and is frozen; this subsection neither modifies nor
re-derives it.

## 5.4 Pet XP → Pet Level Formula

```text
Pet.Level = min(floor(Pet.XP / 100) + 1, 50)
```

Worked boundaries (authoritative):

```text
Pet.XP = 0      →  Pet Level 1
Pet.XP = 100    →  Pet Level 2
Pet.XP = 4900   →  Pet Level 50
```

The formula is deterministic and total over the valid Pet XP domain
`[0, 4900]` (§5.5). `Pet.XP = 4900` corresponds to 49 `BattleWon` rewards
of 100 Pet XP.

### Reward Amount vs. Curve Constant

Following the same distinction the Player track documents
(`COMBAT_RULES.md` §7.3), these are **two independent concepts** that
currently happen to share the value `100`:

```text
Pet XP reward amount          = 100   (MVP initial value — CONFIGURATION)
Pet XP-per-level curve constant = 100 (part of the progression FORMULA)
```

Changing either one must never silently rewrite the other, and neither is
derived from the Player track's corresponding value.

### Relationship to the Player Curve

1. **Same formula shape.** Player and Pet use the identical formula shape
   `min(floor(XP / 100) + 1, 50)` (`COMBAT_RULES.md` §7.4). This is an
   explicit decision, chosen to keep MVP progression simple — not an
   inherited default.
2. **Independent pools.** The two tracks remain independent progression
   pools. Pet XP is never derived from Player XP, and Pet Level is never
   derived from Player Level.
3. **Independent reward amounts.** The reward amounts are independently
   configurable. Both currently equal `100`, which does **not** mean the two
   tracks share an XP pool or a progression variable.

## 5.5 Pet XP Cap and Post-Cap Behavior

```text
Pet Level maximum = 50
Pet XP maximum    = 4900
```

1. **`Pet.XP` has a hard maximum of 4900.** It is not uncapped.
2. **Pet XP stops accumulating at Level 50.** Once a Pet reaches Level 50
   its stored XP is `4900` and further Pet XP rewards do not accumulate —
   they are neither awarded nor stored.
3. **No overflow is retained.** There is no XP overflow, no hidden XP, no
   prestige XP, and no post-Level-50 accumulation.
4. **Level and XP caps are separate facts, and both are documented.** The
   Level cap (50) and the XP cap (4900) are distinct: the formula's
   `min(…, 50)` bounds the Level, while the XP cap bounds the stored XP.
   Stating only "Level is capped" would leave XP accumulation ambiguous,
   which is why both are fixed here.

### Deliberate Divergence From the Player Track

```text
                         Player track          Pet track
──────────────────────   ──────────────────    ──────────────────
Reward per BattleWon     +100                   +100
Level formula            min(floor(XP/100)+1, 50)  min(floor(XP/100)+1, 50)
Level maximum            50                     50
XP maximum               NONE (uncapped)        4900 (HARD CAP)
XP after Level 50        keeps accumulating     not awarded / not stored
```

This is an **intentional divergence**: Player XP is uncapped (it is
account/content progression that keeps accumulating), while Pet XP is
hard-capped at 4900 at Level 50 to avoid unnecessary MVP
overflow/prestige complexity. Neither track's cap rule may be applied to
the other.

## 5.6 Retired Terms Are Not Reused

1. **The `Player.Level × PetLevelMultiplier` derivation is RETIRED.** Pet
   Level is no longer derived from Player Level, and `PetLevelMultiplier`
   has **no role** in the Pet XP model. It is not an XP curve multiplier,
   not an XP reward multiplier, and not a Pet Level input, and it must not
   be retained or reinterpreted for any such purpose without an explicit
   human gameplay decision (`GAME_RULES.md` §20).
2. **`PetDefinition` does not own instance XP or instance Level.**
   `PetDefinition` remains static content (Identity, Element, and its
   content-defined fields); per-instance progression belongs to the owned
   `Pet` row (`DATABASE.md` §1, ADR-016).
3. **The term `Player Level` in this document means the Player's own
   account Level only** (`COMBAT_RULES.md` §7). Nothing in this document
   derives any Pet attribute from it.
4. **There is no Evolution system.** No Evolution system exists in any
   rule document; if introduced later it must go through
   `GAME_RULES.md` §20 and `MVP_SCOPE.md` §4 (FUTURE by default). There is
   no Level/Evolution interaction to specify.

## 5.7 Non-XP Pet Attributes (unchanged)

1. Player Level carries **no combat stats**. It is an account-level
   progression value only; battle-time HP/ATK/DEF/Crit/Power live on
   `PetState` (`GAME_STATE.md` §2.3, ADR-011).
2. Level scales base stats (HP/ATK/DEF) via a stat curve. The curve itself
   is a balance concern, not defined here.
3. Level does not change Element, Tier, Passive trigger type, or Signature
   Skill identity — only magnitude, where applicable.
4. **Tier and Star remain independent progression axes.** They are not
   derived from Player Level and not derived from Pet XP. §3–§4 are
   unchanged.
5. **A newly created Player starts at Level 1.** This is the documented
   initial value of the `Player.Level` attribute (`COMBAT_RULES.md` §7.5
   item 3, `DATABASE.md` §3). It is the *Player's* initial value only. The
   corresponding Pet initial values are `Pet.XP = 0` / `Pet.Level = 1`
   (§5.2) — a separate decision.

---

# 6. Stat Composition

Final in-battle Pet stats are derived from all progression axes combined:

```text
Final Stat = f(Base Stat[Tier], Level Curve[Level], Star Bonus[Star])
```

Here `Level` is the Pet's own Level defined in §5, derived from that Pet
instance's own XP (§5.4). The exact function `f` is a balance/config
concern. This document only fixes that all three axes (Tier, Level, Star)
contribute, and that none of them alone is the sole source of power growth
(reinforcing GAME_RULES.md §9.8).

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

**Provisioned vs. deferred row set.** (TASK-082 decision A) Only Pets
whose Signature Skill is content-defined in `CARD_RULES.md` §4.1 may be
provisioned: **Xích Lang (Inferno), Bạch Hổ (Iron Fang), and Huyền Quy
(Tidal Barrier)**. The **Thanh Xà and Sơn Hùng rows are deferred** until
their Signature Skills are content-defined — their
`SignatureSkillCardId` targets do not exist (`CARD_RULES.md` §4.1) and
the FK is required (`DATABASE.md` §1). Their Passive thresholds above
are unchanged and stay documented; no placeholder row, invented Skill
Card, or invented value may be provisioned (`DATABASE.md` §5 item 4).
