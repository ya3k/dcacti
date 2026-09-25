# Database

**Version:** 1.5 (§1 CardDefinition.LoadoutCopyLimit added — required
per-battle-loadout copy limit per CARD_RULES.md §1; explicit value
required, no default; concrete values deferred to content/balance;
prior 1.4: §1 PetLevelMultiplier type/range and Pet.Level floor
semantics synchronized with PET_RULES.md §5 derivation contract;
§3 item reference corrected to §5 item 10; prior 1.3: §3 initial
Player.Level value added — a newly created
Player starts at Level 1, per PET_RULES.md §5 item 10; prior 1.2: §1
Player.Level added; §2 Card ownership ASSUMPTION resolved to
`PlayerUnlockedCard` join table per ADR-012; prior 1.1: §3 ownership
model note per ADR-011 — Player FKs = collection ownership; no
combat-stat columns on Player; equip is battle-scoped)
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
├── Level                           (1–50 — PET_RULES.md §5, MVP_SCOPE.md §1;
│                                    persistent account attribute, NO combat
│                                    stats; ADR-012)
└── CreatedAt

Pet                              (a player's OWNED instance of a Pet)
├── PetInstanceId (PK)
├── PlayerId (FK → Player)
├── PetDefinitionId (FK → PetDefinition)
├── Tier
├── Star
├── Level                           (floor(Player.Level ×
│                                    PetLevelMultiplier) clamped to 1–50 —
│                                    PET_RULES.md §5; denormalized snapshot
│                                    of the derived value, not an
│                                    independent XP store; same canonical
│                                    rule as BattleState.PetState.Level)
└── AcquiredAt

PetDefinition                    (static content, one row per MVP Pet)
├── PetDefinitionId (PK)
├── Identity                      ("Thanh Xà", "Xích Lang", ...)
├── Element
├── PetLevelMultiplier            (decimal > 0 — PET_RULES.md §5; never
│                                  hard-coded; concrete MVP values deferred
│                                  to balance/config)
├── PassiveDefinition              (threshold/effect reference —
│                                  PASSIVE_RULES.md)
└── SignatureSkillCardId (FK → CardDefinition)

CardDefinition                    (static content: 3 Basic + 5 Pet Skill)
├── CardDefinitionId (PK)
├── Name
├── Category                       ("Basic" | "PetSkill")
├── PowerCost
├── LoadoutCopyLimit               (required — max occurrences of this
│                                  CardDefinition in one submitted 3-card
│                                  Basic loadout; CARD_RULES.md §1; explicit
│                                  value required, no default; concrete
│                                  values are content/balance configuration)
└── EffectDefinition                 (CARD_RULES.md)

PlayerUnlockedCard               (Player owns Card unlocks — ADR-012;
│                                  MVP Cards have no Tier/Star/Level, so an
│                                  unlock flag is sufficient)
├── PlayerId (FK → Player)
└── CardDefinitionId (FK → CardDefinition)

RelicDefinition                    (static content: ~10 MVP Relics)
├── RelicDefinitionId (PK)
├── Name
├── Trigger
├── Condition
└── EffectDefinition                (RELIC_RULES.md)

Relic                                (a player's OWNED instance, if Relics
│                                     have per-instance state; otherwise
│                                     ownership is a join table — see §2 note;
│                                     see also: this storage-shape question
│                                     remains OPEN and is NOT decided by
│                                     RELIC_RULES.md §2.2–§2.5, which fix the
│                                     battle-state element, the loadout
│                                     validation, and the slot order — an
│                                     owned Relic instance identity — not how
│                                     ownership rows are stored)
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
└── RewardSummary                        (JSON — reward line items; may
                                          include Player XP granting
                                          Player Level — PET_RULES.md §5,
                                          GDD §14)
```

---

# 2. Relationships

```text
Player          1 ── N   Pet
Player          1 ── N   Relic (owned instances)
Player          1 ── N   PlayerUnlockedCard
Player          1 ── N   BattleResult
Pet (instance)  N ── 1   PetDefinition
Relic(instance) N ── 1   RelicDefinition
PlayerUnlockedCard N ── 1 CardDefinition
PetDefinition   1 ── 1   CardDefinition (SignatureSkillCardId)
BattleResult    N ── 1   Pet (instance used)
BattleResult    N ── 1   BossDefinition (opponent)
```

Note: **Card ownership is settled (ADR-012):** MVP Cards have no
progression (no Tier/Star/Level per `CARD_RULES.md`), so ownership is a
`PlayerUnlockedCard(PlayerId, CardDefinitionId)` join table of unlock
flags — not a per-instance table. Battle equip of Cards is **not**
persisted here; it is battle-scoped and snapshotted into
`PetState.EquippedCards[]` at `POST /api/battle/start`
(`CARD_RULES.md` §1, `GAME_STATE.md` §2.3). There is no
`Pet.CardInventory` table.

There is likewise **no persistent Relic-equip table**: Player owns Relic
instances; which 3–5 are equipped for a given battle is request-time
loadout only (`RELIC_RULES.md` §2, `API_CONTRACTS.md` §3).

---

# 3. Constraints

```text
Player.Level        ∈ [1, 50]                                      (PET_RULES.md §5)
Player.Level        = 1 for a newly created Player                 (PET_RULES.md §5 item 10)
Pet.Tier          ∈ {Common, Rare, Epic, Legendary, Mythic}      (PET_RULES.md §3)
Pet.Star           ∈ [1, 5]                                       (PET_RULES.md §4)
Pet.Level           ∈ [1, 50]                                      (PET_RULES.md §5)
PetDefinition.PetLevelMultiplier > 0 (decimal)                    (PET_RULES.md §5)
CardDefinition.Category  ∈ {Basic, PetSkill}                        (CARD_RULES.md §1)
Player.PlayerId (per battle) must own exactly one active Pet selection
  at battle start — enforced at the Application layer (ARCHITECTURE.md),
  not purely at the DB level, since it is a request-time rule
  (API_CONTRACTS.md §2), not a stored invariant.

Ownership model (ADR-011, ADR-012): Player FKs on Pet/Relic (and
PlayerUnlockedCard rows) are collection ownership only. There are no
combat-stat columns on Player — HP/ATK/DEF/Crit/Power are battle-time
`PetState` (GAME_STATE.md §2.3, REDIS_STATE.md). `Player.Level` is a
persistent progression value (1–50), not a combat stat. Equipped
Relic/Card loadout is battle-scoped, selected at POST /api/battle/start
for the active Pet and snapshotted into PetState; it is not stored as
Player-owned or Pet-owned equip slots.
```

---

# 4. Indexes

```text
Pet(PlayerId)              — list a player's Pets
Relic(PlayerId)             — list a player's Relics
PlayerUnlockedCard(PlayerId) — list a player's unlocked Cards
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
