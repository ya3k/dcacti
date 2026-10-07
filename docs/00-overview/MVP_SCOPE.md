# MVP Scope

**Version:** 1.6 (§1/§3 updated per TASK-213 — the MVP content set is owned by a
newly created Player's deterministic starter grant, MVP has no post-creation
acquisition system, and per-Pet Passive effect application, Pet Star progression
and post-creation content acquisition are explicitly FUTURE; no other IN/OUT
classification changed. Prior 1.5: §1 Player account updated per ADR-020 — standalone web account with username/password registration/login, PostgreSQL Accounts, and JWT/ApplicationSession; prior 1.4: §1 Pets block finalized per TASK-062 — the Pet XP
balance/reward decisions are now decided (`PET_RULES.md` §5.1–§5.5):
Pet Level range 1–50, Pet XP hard-capped at 4900. Prior 1.3: §1 Player and
Pets blocks updated per TASK-059 — Player
XP / Level is the account progression track (`COMBAT_RULES.md` §7) and is
no longer the source of Pet Level; Pet XP / Level is the Pet instance's own
independent track (`PET_RULES.md` §5); ADR-016. Prior 1.2: §1 Pets Pet Level
formula synchronized with the
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
Player account (standalone web account with username/password registration/login, PostgreSQL Accounts, JWT/ApplicationSession, collection owner)
Player XP / Level — persistent account progression (COMBAT_RULES.md §7).
  XP uncapped; Level 1–50. BattleWon +100 XP, BattleLost +0 XP.
  No combat stats.
```

## Pets
```text
5 Pets (Thanh Xà, Xích Lang, Sơn Hùng, Bạch Hổ, Huyền Quy)
Pet Element, Signature Skill
Pet Passive — the charge / threshold / reset mechanism and its in-battle
  presentation (passive identity, progress counter, PassiveCharged /
  PassiveTriggered)
Pet Level progression
  (Pet XP / Pet Level — the Pet instance's own progression, independent of
   Player Level; PET_RULES.md §5. Battle-won rewards go to the active
   combat Pet; Pet Level range 1–50, Pet XP hard-capped at 4900)
Tier is a fixed per-Pet value (PET_RULES.md §3.4 — MVP ships one Tier
  instance per Pet; no Tier-up rule exists and none is introduced)
Deferred to FUTURE by the TASK-213 decision: per-Pet Passive effect
  application, and Pet Star progression (§3; see "Content ownership &
  reachability" below)
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

## Content ownership & reachability (TASK-213 decision)
```text
The whole MVP content set is owned by a newly created Player. The
deterministic Player-creation starter grant (DATABASE.md §2) IS the MVP
content grant — it is not a minimum bootstrap:
  5 Pets        one owned Pet instance per MVP Pet definition
  3 Basic Cards all three, as PlayerUnlockedCard unlock rows
  10 Relics     one owned Relic instance per MVP Relic definition
  5 Bosses      no ownership relationship — all five are always selectable
A Pet Skill Card is derived from the active Pet's SignatureSkillCardId at
battle start and is never an owned/unlock row (CARD_RULES.md §1 item 4).

MVP has NO acquisition system. No unlock, drop, purchase, claim,
reward-grant, gacha, shop, quest, or progression path adds content to an
account after creation: an account's owned content set is fixed at
creation. Post-creation content acquisition, and any content beyond the set
above, are FUTURE (§3) and require a Rule Change (GAME_RULES.md §20) before
implementation.

Card build diversity is a CONTENT limit, not a reachability limit. MVP
defines exactly 3 Basic Cards and the battle loadout is exactly 3 Basic
Cards + 1 derived Pet Skill Card (CARD_RULES.md §1), so the Card slot is
fixed by content, not by ownership. More Cards is FUTURE (§3).
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
Post-creation content acquisition / unlocking (TASK-213)
Per-Pet Passive effect application (TASK-213; magnitudes are unauthored —
  PASSIVE_RULES.md §8)
Pet Star progression (TASK-213; no star-up cost or curve is authored —
  PET_RULES.md §4 item 3)
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
