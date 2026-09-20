# Database

**Version:** 1.0
**Status:** Draft

> This document answers: **"What persistent data exists and how is it
> stored?"** It does not redefine game rules — field meanings are owned by
> `GAME_RULES.md` and the domain rule documents. This is not an ORM
> implementation guide; exact column types/migrations are an implementation
> detail.

---

# 1. Entities

```text
Player
├── PlayerId (PK)
├── DiscordUserId (unique)
└── CreatedAt

Pet                              (a player's OWNED instance of a Pet)
├── PetInstanceId (PK)
├── PlayerId (FK → Player)
├── PetDefinitionId (FK → PetDefinition)
├── Tier
├── Star
├── Level
└── AcquiredAt

PetDefinition                    (static content, one row per MVP Pet)
├── PetDefinitionId (PK)
├── Identity                      ("Thanh Xà", "Xích Lang", ...)
├── Element
├── PassiveDefinition              (threshold/effect reference —
│                                  PASSIVE_RULES.md)
└── SignatureSkillCardId (FK → CardDefinition)

CardDefinition                    (static content: 3 Basic + 5 Pet Skill)
├── CardDefinitionId (PK)
├── Name
├── Category                       ("Basic" | "PetSkill")
├── PowerCost
└── EffectDefinition                 (CARD_RULES.md)

RelicDefinition                    (static content: ~10 MVP Relics)
├── RelicDefinitionId (PK)
├── Name
├── Trigger
├── Condition
└── EffectDefinition                (RELIC_RULES.md)

Relic                                (a player's OWNED instance, if Relics
│                                     have per-instance state; otherwise
│                                     ownership is a join table — see §2 note)
├── RelicInstanceId (PK)
├── PlayerId (FK → Player)
├── RelicDefinitionId (FK → RelicDefinition)
└── AcquiredAt

BossDefinition                       (static content: 5 MVP Bosses)
├── BossDefinitionId (PK)
├── Identity
├── Element
├── PassiveDefinition
└── SkillDefinition                   (BOSS_RULES.md)

BattleResult
├── BattleResultId (PK)
├── PlayerId (FK → Player)
├── PetInstanceId (FK → Pet)
├── BossDefinitionId (FK → BossDefinition)
├── Outcome                            ("Won" | "Lost")
├── DurationTurns
├── CompletedAt
└── RewardSummary                        (JSON — reward line items)
```

---

# 2. Relationships

```text
Player          1 ── N   Pet
Player          1 ── N   Relic (owned instances)
Player          1 ── N   BattleResult
Pet (instance)  N ── 1   PetDefinition
Relic(instance) N ── 1   RelicDefinition
PetDefinition   1 ── 1   CardDefinition (SignatureSkillCardId)
BattleResult    N ── 1   Pet (instance used)
BattleResult    N ── 1   BossDefinition (opponent)
```

Note: whether Cards need a per-player "owned instance" row (like Pet and
Relic) or are simply unlocked flags on `Player` is not settled by any prior
design document — MVP Cards have no progression (no Tier/Star/Level per
`CARD_RULES.md`), so a simple `PlayerUnlockedCard(PlayerId, CardDefinitionId)`
join table is assumed sufficient rather than a full instance table.
**ASSUMPTION** — flag for confirmation if Card progression is later added.

---

# 3. Constraints

```text
Pet.Tier          ∈ {Common, Rare, Epic, Legendary, Mythic}      (PET_RULES.md §3)
Pet.Star           ∈ [1, 5]                                       (PET_RULES.md §4)
Pet.Level           ∈ [1, 50]                                      (PET_RULES.md §5)
CardDefinition.Category  ∈ {Basic, PetSkill}                        (CARD_RULES.md §1)
Player.PlayerId (per battle) must own exactly one active Pet selection
  at battle start — enforced at the Application layer (ARCHITECTURE.md),
  not purely at the DB level, since it is a request-time rule
  (API_CONTRACTS.md §2), not a stored invariant.
```

---

# 4. Indexes

```text
Pet(PlayerId)              — list a player's Pets
Relic(PlayerId)             — list a player's Relics
BattleResult(PlayerId, CompletedAt DESC)  — battle history, most recent first
```

No further indexes are specified — additional indexes should be added only
when a real query pattern requires them (anti-overengineering,
`AGENTS.md` §2.5), not speculatively.

---

# 5. What Is Not Here

1. Migration scripts / exact SQL types — implementation detail.
2. Any table for PvP, Gacha, Guild, or Trading — excluded by
   `MVP_SCOPE.md` §2.
3. Active battle state — lives in Redis only (`REDIS_STATE.md`), never
   written to PostgreSQL until the battle ends.
