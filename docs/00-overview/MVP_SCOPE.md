# MVP Scope

**Version:** 1.2 (§1 Pets Pet Level formula synchronized with the
completed derivation contract — floor before clamp — PET_RULES.md §5;
prior 1.1: §1 Player Level added — required by the Pet Level
formula; PET_RULES.md §5, ADR-012)
**Status:** Canonical — this document is the single source of truth for
IN / OUT / FUTURE classification. `GAME_RULES.md` and `GDD.md` reference
this document rather than repeating the list.

> This document answers: **"What is explicitly inside and outside MVP?"**

---

# 1. IN — MVP

## Match-3
```text
8×8 board
4 Gem types (ATK, DEF, HP, POWER)
Match 3 / Match 4 / Match 5
L/T matches
Cascade
Combo
```

## Elements
```text
5 Elements (Mộc, Hỏa, Thổ, Kim, Thủy)
Tương Khắc relationship only
Element damage modifiers (Advantage / Neutral / Disadvantage)
```

## Player
```text
Player account (Discord identity, collection owner)
Player Level (1–50) — persistent account attribute; no combat stats.
  Source of Pet Level via PET_RULES.md §5. Increases through battle
  Rewards (Meta Progression — GDD §14). Exact XP curve is a balance
  concern, not a scope item.
```

## Pets
```text
5 Pets (Thanh Xà, Xích Lang, Sơn Hùng, Bạch Hổ, Huyền Quy)
Pet Element, Passive, Signature Skill
Tier, Star, Level progression
  (Pet Level = clamp(floor(Player Level × Pet Level Multiplier), 1, 50) —
   PET_RULES.md §5; no Pet XP; concrete MVP multiplier values deferred
   to balance/config)
```

## Cards
```text
3 Basic Cards (Heal, Shield, Power Charge)
5 Pet Skill Cards (one per Pet)
```

## Relics
```text
~10 Relics
Trigger system (OnMatch, OnCombo, OnHpBelow, etc.)
3–5 equipped Relics per battle
```

## Bosses
```text
5 Bosses
Element, Passive, Skill per Boss
```

## Combat
```text
HP, ATK, DEF, Power, Crit, Status Effects, Damage, Element interaction
```

## Technical (MVP-required infrastructure)
```text
Server-authoritative battle resolution
Realtime communication (SignalR)
Active battle state store (Redis)
Persistent storage (PostgreSQL)
```

---

# 2. OUT — Explicitly Excluded from MVP

```text
Map Mechanics
Terrain
Weather
Dynamic Environment / Dynamic Board Environment
Tương Sinh (generating cycle)
PvP
Gacha
Guild
Trading
Complex Equipment System
Microservices
Kubernetes
Kafka
```

Notes:

1. A Map may exist as **visual background only**; it must not modify
   gameplay in MVP.
2. Any task that implies one of the above (even partially — e.g. "add a
   weather modifier to damage") is out of scope and must be reported per
   `AGENTS.md` §2, not implemented.

---

# 3. FUTURE — Direction Only, Not Designed

These are acknowledged as intended future direction (see `ROADMAP.md`) but
have **no rules, no formulas, and no implementation scope** yet:

```text
More Pets
More Cards / Relics
Boss Phases
Map (gameplay-affecting)
Environment
Dynamic Board
Advanced Element System (e.g. Tương Sinh)
PvP / Multiplayer
```

Nothing in this list may be implemented, partially implemented, or
scaffolded "for later" without first going through the Rule Change Policy
(`GAME_RULES.md` §20) to move it from FUTURE to IN.

---

# 4. Classification Authority

If a feature's status is unclear:

1. Check this document first.
2. If absent from both §1 and §2, treat it as **FUTURE** by default — not
   IN.
3. Report the ambiguity rather than assuming IN status to "be helpful."
