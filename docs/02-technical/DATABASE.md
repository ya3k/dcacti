# Database

**Version:** 1.9 (§1 `BossDefinitionId` semantics fixed per TASK-049 — an
independent stable persistence key supplied by content/Domain
(`required string BossDefinitionId`, stored as the PK), never
database-generated and never derived from `Identity` or the display name;
value form `boss-def-<ascii-kebab-case-name>` with canonical values
`boss-def-hoa-long` / `boss-def-thuy-ma` / `boss-def-moc-yeu`; §1 contract
note item 2 added and items renumbered, §3 `BossDefinition.BossDefinitionId`
constraint added; the TASK-046 three-way non-collapse rule is preserved
unchanged; prior 1.8: §1 Boss identity contract per TASK-046 — `BossDefinitionId`
= persistence PK, `Identity` = canonical technical Boss ID
(`BOSS_RULES.md` §6.4), display name not a column; `BattleResult.BossDefinitionId`
value sourced from `BattleState.BossState.BossId` via `Identity` lookup;
§3 `BossDefinition.Identity` NOT NULL/UNIQUE added; prior 1.7: §1 BossDefinition persistence contract documented per
TASK-045 — `PassiveDefinition`/`SkillDefinition` JSON member lists and
reset-token set {Default, Partial, Persistent} defined, `threshold` null
semantics (always-active, no 0-sentinel), persistent-identity vs
combat-stat source split, no-invented-provisioning guard and
content-defined-rows-only scope clarified; §3 BossDefinition constraints
added; prior 1.6: §1 BattleResult identity/reward sourcing documented —
`BattleResultId` = `BattleId` (one row per battle), `PlayerId` value from
`BattleState.PlayerId` (`GAME_STATE.md` §2.8), `PetInstanceId` value from
`BattleState.PetState.PetId` (`GAME_STATE.md` §2.3 — the owned Pet
instance), `RewardSummary` staging value = empty JSON object with no line
items until TASK-033 (ADR-014, TASK-042); prior 1.5: §1
CardDefinition.LoadoutCopyLimit added — required
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

BossDefinition                       (static content — MVP scope target:
 │                                    5 Bosses, MVP_SCOPE.md §1;
 │                                    content-defined: 3, BOSS_RULES.md
 │                                    §6; only content-defined rows may
 │                                    ever be provisioned — see contract
 │                                    note below)
├── BossDefinitionId (PK)             (independent stable persistence key —
 │                                     content/Domain supplied, never
 │                                     database-generated; distinct from
 │                                     `Identity` below and from the display
 │                                     name, and never derived from either,
 │                                     TASK-046/TASK-049; value form
 │                                     `boss-def-<ascii-kebab-case-name>`,
 │                                     e.g. "boss-def-hoa-long")
├── Identity                          (canonical technical Boss ID —
 │                                     BOSS_RULES.md §6.4, e.g.
 │                                     "boss-hoa-long"; never a display
 │                                     name; no display-name column exists
 │                                     on this table)
├── Element
├── PassiveDefinition                (JSON, NOT NULL — member list:
 │                                    TASK-045; BOSS_RULES.md §6,
 │                                    PASSIVE_RULES.md §4)
└── SkillDefinition                   (JSON, NOT NULL — member list:
                                      TASK-045; BOSS_RULES.md §6)

BattleResult
├── BattleResultId (PK)              (= the battle's own BattleId — one row
│                                      per battle, GAME_STATE.md §2)
├── PlayerId (FK → Player)           (value: BattleState.PlayerId —
│                                      GAME_STATE.md §2.8)
├── PetInstanceId (FK → Pet)         (value: BattleState.PetState.PetId —
│                                      GAME_STATE.md §2.3; the owned Pet
│                                      instance)
├── BossDefinitionId (FK → BossDefinition)
│                                      (value: the key of the row whose
│                                      `Identity` = BattleState.BossState.BossId —
│                                      GAME_STATE.md §2.4, BOSS_RULES.md §6.4,
│                                      derived on the battle-end path)
├── Outcome                            ("Won" | "Lost")
├── DurationTurns
├── CompletedAt
└── RewardSummary                        (JSON — member list owned by
                                          TASK-033; staging value until
                                          then: empty object, no reward
                                          line items; may include Player
                                          XP granting Player Level —
                                          PET_RULES.md §5, GDD §14)
```

**Persistence contract for `BossDefinition`.** (TASK-045)

1. **Persistent record vs combat-stat source.** The row is the
   persistent static identity/configuration record: `Identity`,
   `Element`, and the two JSON objects below. `Identity` holds the Boss's
   canonical technical ID (`BOSS_RULES.md` §6.4, e.g. `boss-hoa-long`) —
   it is **not** the display name, and it is **not** `BossDefinitionId`
   (this row's persistence primary key): the three are never collapsed
   (TASK-046). The display name is presentation content owned by
   `BOSS_RULES.md` §6 and is not a column of this table. The combat-definition
   values `MaxHP`, `ATK`, `DEF`, `EnrageThreshold` (`BOSS_RULES.md`
   §6.1) are **not** stored in this table — they remain sourced from the
   authoritative Domain `BossDefinition` content at battle creation. At
   battle creation the server combines the persisted
   identity/configuration with that combat-stat configuration to
   construct the existing `BossState` (`GAME_STATE.md` §2.4); after
   battle creation, the BattleState/Redis contract governs
   (`REDIS_STATE.md`, ADR-005). No column may be added unless an
   existing authoritative document explicitly requires it (anti-
   overengineering, `AGENTS.md` §9).
2. **`BossDefinitionId` is a caller/content-supplied stable key.** (TASK-049)
   It is the row's primary key and is **independent**: distinct from
   `Identity` and from the display name, and never derived from either at
   runtime or at content-authoring time.
   - **Value form:** a non-empty string, `boss-def-<ascii-kebab-case-name>`
     — ASCII only, lowercase, kebab-case, stable, no Vietnamese diacritics,
     no display/localization text, no spaces, no runtime-generated or
     runtime-slugified identifiers.
   - **Canonical values** for the three content-defined MVP Bosses
     (`BOSS_RULES.md` §6), which are **not** the `Identity` values:
     ```text
     Hỏa Long    boss-def-hoa-long      (Identity: boss-hoa-long)
     Thủy Ma     boss-def-thuy-ma       (Identity: boss-thuy-ma)
     Mộc Yêu     boss-def-moc-yeu       (Identity: boss-moc-yeu)
     ```
   - **Source / ownership:** supplied by game content/Domain, **not**
     database-generated. The Domain `BossDefinition` record carries it
     explicitly as `required string BossDefinitionId`, and persistence
     stores that value as the primary key. No GUID, integer, provider-
     generated, or database-generated key is used, and no `HasData`, seed,
     startup loader, or migration-inserted production row is introduced by
     this contract (note item 5 below; `§5` item 4).
   - **Relationship to `Identity`:** the two are separate values serving
     separate purposes — `Identity` is the canonical technical Boss ID that
     battle state, events, the API, and the `BattleResult` FK *lookup*
     (`§1`, "Identity and reward sourcing for `BattleResult`" item 2) all
     use; `BossDefinitionId` is the persistence key of the row itself.
     A caller resolves the row **by `Identity`** and then stores that row's
     `BossDefinitionId` as a foreign key; it must never substitute one value
     for the other.
3. **`PassiveDefinition` members** (JSON object, NOT NULL — every Boss
   carries exactly one Passive, `BOSS_RULES.md` §1/§6.4):
   ```json
   { "passiveId": "…", "threshold": 5, "resetBehavior": "Default" }
   ```
   - `passiveId` (string) — the Boss's canonical PassiveId
     (`BOSS_RULES.md` §6.4).
   - `threshold` (int | null) — the Passive's match-charging Threshold
     (`PASSIVE_RULES.md` §1). `null` means the Passive has **no
     threshold and is always active** (`BOSS_RULES.md` §6.2); `0` is
     never used as a "no threshold" sentinel.
   - `resetBehavior` (string) — exactly one of `Default` | `Partial` |
     `Persistent`, the storage names for the documented Reset Behavior
     variants Default Reset / Partial Reset / No Reset — Persistent
     (`PASSIVE_RULES.md` §4). No other token exists. The documented
     default when a rule states no override is `Default`
     (`PASSIVE_RULES.md` §1). Internal enum mapping
     (`PassiveResetBehavior`): `Default` → `Default`, `Partial` →
     `Partial`, `Persistent` → `NoReset` — the JSON/storage names are
     the contract; internal representation maps to them, not vice
     versa.
4. **`SkillDefinition` members** (JSON object, NOT NULL — every Boss has
   exactly one Skill, `BOSS_RULES.md` §1/§6.4):
   ```json
   { "skillId": "…", "baseDamage": 0, "chargeRequirement": 0,
     "cooldownTurns": 0 }
   ```
   - `skillId` (string) — the Boss's canonical SkillId
     (`BOSS_RULES.md` §6.4).
   - `baseDamage` (int) — Skill Base Dmg (`BOSS_RULES.md` §6.3).
   - `chargeRequirement` (int) — Charge Requirement (`BOSS_RULES.md`
     §6.3; battle-state member `SkillChargeRequirement`,
     `GAME_STATE.md` §2.4.3).
   - `cooldownTurns` (int) — Cooldown (CD) in turns (`BOSS_RULES.md`
     §6.3).

   These storage member names are fixed by TASK-045; battle-state
   member names (`GAME_STATE.md` §2.4) are a separate contract and are
   unchanged.
5. **Rows and provisioning.** Rows are static content derived from the
   canonical Boss definitions: provisioning must be deterministic, must
   be idempotent, and must never depend on a player's runtime battle.
   **No provisioning mechanism is documented, and none may be invented**
   (`AGENTS.md` §7/§9) — a future provisioning-contract decision is
   required before any row exists. Only content-defined Bosses
   (currently 3 — `BOSS_RULES.md` §6) may ever be provisioned; the
   5-Boss figure is the MVP scope target (`MVP_SCOPE.md` §1), not
   permission to create placeholder rows for undefined content. The
   `BossDefinitionId` of item 2 changes nothing here: no `HasData`, seed,
   startup loader, or migration-inserted production row is introduced.

**Identity and reward sourcing for `BattleResult`.**

1. **One row per battle.** `BattleResultId` **is** the battle's own
   `BattleId` (`GAME_STATE.md` §2) — no second identifier is introduced and
   no second row can exist; `GET /api/battle/{battleId}/result`
   (`API_CONTRACTS.md` §4) looks the row up by this key.
2. **Identity values come from battle state.** `PlayerId` is copied from
   `BattleState.PlayerId` (`GAME_STATE.md` §2.8) and `PetInstanceId` from
   `BattleState.PetState.PetId` (`GAME_STATE.md` §2.3 — the owned Pet
   instance) on the battle-end path; both are server-authoritative and are
   never re-derived from client input or from a session at battle end
   (`GAME_RULES.md` §18, ADR-001). Both identities are members of the
   state record and therefore round-trip through serialization with it
   (`REDIS_STATE.md` §2). `BossDefinitionId` follows the same rule: it is
   the key of the `BossDefinition` row whose `Identity` equals
   `BattleState.BossState.BossId` (the canonical technical Boss Identity —
   `GAME_STATE.md` §2.4, `BOSS_RULES.md` §6.4), resolved server-side on
   that path from battle state — not re-derived from client input at
   battle end, and never from a display name (uniqueness of `Identity`
   for this lookup: §3).
3. **`RewardSummary`'s member list is owned by TASK-033** (reward
   magnitudes, XP, and line-item shape — `PET_RULES.md` §5,
   `MVP_SCOPE.md` §1). Until that task defines it, the documented staging
   value is the **empty JSON object `{}`** — a value that is always
   present, never absent, for both `Outcome`s; a `Lost` battle carries no
   line items.

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
BossDefinition.BossDefinitionId     NOT NULL, UNIQUE, caller/content-supplied (independent persistence key, never
                                                                      database-generated; distinct from `Identity` and
                                                                      from the display name — §1 note item 2, TASK-049)
BossDefinition.PassiveDefinition  NOT NULL                           (every Boss has one Passive — BOSS_RULES.md §1)
BossDefinition.SkillDefinition    NOT NULL                           (every Boss has one Skill — BOSS_RULES.md §1)
BossDefinition.Identity           NOT NULL, UNIQUE                   (canonical technical Boss ID — BOSS_RULES.md
                                                                      §6.4; the unique target of the FK lookup in §1)
BossDefinition.PassiveDefinition.resetBehavior ∈ {Default, Partial, Persistent}   (PASSIVE_RULES.md §4)
BossDefinition.PassiveDefinition.threshold = null ⇔ always-active, no threshold    (BOSS_RULES.md §6.2)
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
4. Any seed/provisioning mechanism for static-content rows
   (`BossDefinition`, `PetDefinition`, `CardDefinition`, …) — none is
   documented; when rows become necessary, defining it is a separate
   documented decision (see §1, `BossDefinition` persistence contract
   item 5).
