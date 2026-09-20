# Pet Rules

**Version:** 1.0
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

1. A player may own an arbitrary number of Pets (collection).
2. Exactly one Pet is selected as "active" per battle (GAME_RULES.md §9.2).
3. Selecting a Pet locks in that Pet's Element, Passive, and Signature Skill
   for the duration of the battle. Mid-battle Pet swapping is out of MVP
   scope.

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
   concerns (GDD §17) and are not defined in this Battle-facing rules
   document.

---

# 5. Level

```text
Range: 1–50
```

1. Level primarily scales base stats (HP/ATK/DEF) via a stat curve.
2. Level does not change Element, Tier, Passive trigger type, or Signature
   Skill identity — only magnitude, where applicable.
3. Exact level curve (linear/exponential/tabled) is a balance concern defined
   in COMBAT_RULES.md / config, not here.

---

# 6. Stat Composition

Final in-battle Pet stats are derived from all progression axes combined:

```text
Final Stat = f(Base Stat[Tier], Level Curve[Level], Star Bonus[Star])
```

The exact function `f` is a balance/config concern. This document only fixes
that all three axes (Tier, Level, Star) contribute, and that none of them
alone is the sole source of power growth (reinforcing GAME_RULES.md §9.8).

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
