# Database

**Version:** 1.13 (§1 `BossDefinition` provisioning **implementation
complete** in TASK-053 — migration
`20260926151112_ProvisionBossDefinitions` carries the three `InsertData`
rows, is applied through the existing `dotnet ef database update` workflow,
and the **three canonical rows are provisioned**; the TASK-052 provisioning
contract itself is unchanged, and §1 note item 5 and §5 item 4 status
wording updated; prior 1.12: §1 `BossDefinition` provisioning contract decided in
TASK-052 — mechanism (human decision P1): an EF Core migration that INSERTs the
three content-defined rows (`boss-def-hoa-long`, `boss-def-thuy-ma`,
`boss-def-moc-yeu`), applied through the existing `dotnet ef database
update` workflow; scope: every battle-capable environment, applied before
that environment's first `BattleResult` write; no `HasData`, no seed, no
startup loader, no separate manual-SQL deployment path, no runtime
provisioning infrastructure; this document is the canonical owner of the
contract and no ADR is required; §1 note item 5 and §5 item 4 updated;
prior 1.11: §1 `BossDefinition` resolution, provisioning dependency,
and missing-definition behaviour documented per TASK-051 human decisions —
`Identity` → `BossDefinitionId` is a PostgreSQL lookup by `Identity` owned by
the Infrastructure layer via the existing `PersistenceRepository (Postgres)`
component, with `BattleState` unchanged; a separate BossDefinition
provisioning/content task is a **prerequisite** for any `BattleResult` write
because `BossDefinitionId` is an FK; and an unresolved `BossDefinition` fails
the battle-end write **closed** — no `BattleResult` row, no
`battle:{battleId}:state` delete, active state retained for recovery. §5
item 4 updated: the provisioning dependency is decided, the mechanism still
is not; prior 1.10: §1 `Outcome` vocabulary changed to `"victory" |
"defeat"` per TASK-050 human Decision C — the single battle-outcome
vocabulary owned by `GAME_EVENTS.md` §2 and shared with
`API_CONTRACTS.md` §4 and `SIGNALR_PROTOCOL.md` §3.2.19; §1 contract
notes added for `DurationTurns` (derived from `BattleState.Turn` at
battle end, terminal Turn counted, `Sequence` never the source) and
`CompletedAt` (server clock captured on the battle-end path, never
client-sourced, no timezone asserted); prior 1.9: §1 `BossDefinitionId` semantics fixed per TASK-049 — an
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
├── Outcome                            ("victory" | "defeat" — value set
│                                      owned by GAME_EVENTS.md §2)
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
     generated, or database-generated key is used, and this key contract
     introduces no seed of its own — no `HasData` and no startup loader
     exists for this model; the provisioning contract (mechanism, row
     set, ordering) is note item 5 below and `§5` item 4.
   - **Relationship to `Identity`:** the two are separate values serving
     separate purposes — `Identity` is the canonical technical Boss ID that
     battle state, events, the API, and the `BattleResult` FK *lookup*
     (`§1`, "Identity and reward sourcing for `BattleResult`" item 2) all
     use; `BossDefinitionId` is the persistence key of the row itself.
     A caller resolves the row **by `Identity`** and then stores that row's
     `BossDefinitionId` as a foreign key; it must never substitute one value
     for the other.
   - **Resolution mechanism and resolver owner.** (TASK-051) The resolution
     `Identity` → `BossDefinitionId` is a **PostgreSQL lookup by `Identity`**,
     performed on the battle-end path against the persisted `BossDefinition`
     row whose `Identity` equals `BattleState.BossState.BossId`
     (`§3` — `Identity NOT NULL, UNIQUE` is the unique target of that lookup).
     The **`Infrastructure` layer owns the lookup**, expressed through the
     `PersistenceRepository (Postgres)` component that `ARCHITECTURE.md` §3
     already designates for `DATABASE.md` data; the Application layer
     orchestrates the battle-end step and the Domain layer supplies the
     Identity value but neither performs nor owns the query
     (`ARCHITECTURE.md` §2.1 — layer direction, `TDD.md` §4 item 3 — Postgres
     is touched only on the terminal path, never on the hot resolution path).
     This introduces **no new abstraction, resolver service, registry, or
     read model**: the lookup is a query on the existing persistence boundary
     (`ARCHITECTURE.md` §5 item 3 — "no separate read-model store").
     `BattleState` is **unchanged** by this contract: no
     `BossDefinitionId` member is added to `BossState` or to `BattleState`,
     so no Redis serialization, snapshot, or wire contract is affected
     (`GAME_STATE.md` §2.4 — "No second identity field … is added to
     `BossState`"; `REDIS_STATE.md` §2 item 1).
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
   Only content-defined Bosses
   (currently 3 — `BOSS_RULES.md` §6) may ever be provisioned; the
   5-Boss figure is the MVP scope target (`MVP_SCOPE.md` §1), not
   permission to create placeholder rows for undefined content. The
   `BossDefinitionId` of item 2 adds no seed of its own: no `HasData`,
   seed, or startup loader exists for this model, and the only
   provisioning path is the migration INSERT decided below.
   - **Mechanism — decided.** (TASK-052) The three rows are provisioned
     by an **EF Core migration that INSERTs them** into `BossDefinition`,
     applied through the project's existing `dotnet ef database update`
     workflow. `HasData`, a seed, a startup loader/upsert, a separate
     manual-SQL deployment path, and any runtime provisioning
     infrastructure are **not** used, and no fourth mechanism may be
     introduced by any other task. The decision is scoped to
     `BossDefinition`: no other content table's provisioning
     (`PetDefinition`, `CardDefinition`, `RelicDefinition`, …) is decided
     here, and each remains open (`§5` item 4).
   - **Row set — exactly three rows.** The canonical `BossDefinitionId`
     values of item 2 (`boss-def-hoa-long`, `boss-def-thuy-ma`,
     `boss-def-moc-yeu`), each with its `Identity` from `BOSS_RULES.md`
     §6.4 (`boss-hoa-long`, `boss-thuy-ma`, `boss-moc-yeu`). No
     placeholder rows (first paragraph), and no rows for the two MVP
     Bosses that are not yet content-defined.
   - **Row content — transcribed, never invented.** Each row's five
     columns are sourced per column, with no value computed or invented
     at provisioning time: `BossDefinitionId` from item 2; `Identity`
     from `BOSS_RULES.md` §6.4; `Element` from `BOSS_RULES.md` §6;
     `PassiveDefinition` per item 3's member list with `passiveId` and
     `threshold` from `BOSS_RULES.md` §6.2/§6.4 (including `null` for
     Thủy Ma's always-active Passive — `§3`: `threshold = null` ⇔
     always-active) and `resetBehavior` = `Default` (the documented
     default when a rule states no override — no Boss Passive documents
     one, `PASSIVE_RULES.md` §4 item 3); `SkillDefinition` per item 4's
     member list with `skillId`, `baseDamage`, `chargeRequirement`, and
     `cooldownTurns` from `BOSS_RULES.md` §6.3/§6.4. The authoritative
     documents own the values and remain the only source for them —
     nothing is duplicated here as a second source. Combat stats
     (`BOSS_RULES.md` §6.1) and display names are **not** columns
     (item 1) and are never written by provisioning; the storage
     encoding of `Element` is an implementation detail (header, `§5`
     item 1).
   - **Idempotency and uniqueness.** The migration inserts each row at
     most once and is tracked in EF's migration history, so re-running
     `dotnet ef database update` inserts nothing further; the end state
     is identical whether the migration ran once or was retried —
     exactly three rows with identical content (first paragraph).
     `§3`'s `BossDefinitionId` primary key and `Identity NOT NULL,
     UNIQUE` are the database-level guarantee against duplicates.
     Provisioning never updates, overwrites, or deletes an existing row.
   - **Availability guarantee (ordering, every battle-capable
     environment).** (TASK-052) The migration is applied in **every
     battle-capable environment** — any environment in which a battle can
     be started and ended — and within each such environment it is
     applied **before that environment's first battle-end write**: the
     three rows exist before any `BattleResult` insert can attempt the
     FK. Until the migration has been applied in an environment, no
     `BossDefinition` row exists there and the FK cannot be satisfied.
   - **Missing provisioning surfaces only as the documented fail-closed
     behaviour.** When the rows are absent, the failure occurs on the
     battle-end path and is governed entirely by "Identity and reward
     sourcing for `BattleResult`" item 3 (fail closed: no `BattleResult`
     row, no `battle:{battleId}:state` delete, battle recoverable and
     retryable, documented `404` until the write succeeds, no new error
     code or wire contract). This contract adds **no** startup
     validation, pre-battle gate, health check, retry worker, or
     provisioning monitoring — none is documented, and inventing one
     would violate `ARCHITECTURE.md` §5 / `AGENTS.md` §9
     (anti-overengineering).
   - **Provisioning precedes `BattleResult` persistence, and both the
     decision and its implementation are now complete.** (TASK-051)
     `BattleResult.BossDefinitionId` is a foreign key to `BossDefinition`
     (`§1` entity block, `§2`), so resolving the *value* (item 2) does not
     by itself satisfy the constraint — the referenced **row must already
     exist** at insert time. The resolution mechanism (item 2) is a lookup
     that only succeeds when a row is present. The separate documented
     provisioning decision TASK-051 left open was made by TASK-052 (the
     migration INSERT mechanism above); **its implementation is complete
     (TASK-053)**: migration `20260926151112_ProvisionBossDefinitions`
     applies through `dotnet ef database update`, and **the three canonical
     rows are provisioned**, so the FK target exists in every environment
     where the migration has been applied. In an environment where it has
     not yet been applied, no row exists and the FK cannot be satisfied —
     which surfaces only as the fail-closed behaviour above. No persistence
     task may introduce a different mechanism: no `HasData`, no seed, no
     startup loader, no manual SQL path, no runtime provisioning.

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
3. **An unresolved `BossDefinition` fails the battle-end write closed.**
   (TASK-051) If the lookup in item 2 finds **no** `BossDefinition` row whose
   `Identity` equals `BattleState.BossState.BossId`, the battle-end path
   treats it as a **server-side battle-resolution failure**:
   - **No `BattleResult` row is written.** An absent definition must never
     produce a fabricated, null, empty-string, fallback, or default
     `BossDefinitionId`, and must never be written by skipping or disabling
     the foreign key (`§2`, `§3`). The FK is never satisfied by anything other
     than a real, provisioned `BossDefinition` row.
   - **`battle:{battleId}:state` is NOT deleted.** The documented battle-end
     ordering is result-write **then** active-state delete
     (`ARCHITECTURE.md` §4 item 4, `REDIS_STATE.md` §3); because the result
     write did not happen, the delete must not happen either. Deleting the
     active state would destroy the only authoritative copy of the battle
     (`REDIS_STATE.md` §2 item 2, §7 item 5) for a battle that was never
     durably recorded.
   - **The battle remains recoverable and the failure is repairable.** The
     authoritative `BattleState` is still in Redis under its normal sliding
     TTL (`REDIS_STATE.md` §3), so the still-unresolved battle is not lost.
     Once the configuration/provisioning issue is resolved (a
     `BossDefinition` row exists for that `Identity` — item 5), the battle-end
     write can be retried from that authoritative state and complete normally.
   - **No new error code, API response, or wire contract is introduced.** This
     is an internal battle-end persistence outcome, not a client-facing
     contract: `GET /api/battle/{battleId}/result` (`API_CONTRACTS.md` §4)
     simply finds no row and returns its documented `404` until the write
     succeeds. No `Status`/lifecycle field is added to `BattleState`
     (`GAME_STATE.md` §2.0.3), and no retry worker, queue, or rollback policy
     is introduced (`ARCHITECTURE.md` §5 — anti-overengineering).
   - **This is the fail-closed counterpart of item 2.** Item 2 guarantees the
     value is resolved from authoritative data; this item guarantees that a
     missing definition can never corrupt the FK contract or destroy
     recoverable state.
3. **`RewardSummary`'s member list is owned by TASK-033** (reward
   magnitudes, XP, and line-item shape — `PET_RULES.md` §5,
   `MVP_SCOPE.md` §1). Until that task defines it, the documented staging
   value is the **empty JSON object `{}`** — a value that is always
   present, never absent, for both `Outcome`s; a `"defeat"` battle carries
   no line items.

**Duration and completion sourcing for `BattleResult`.** (TASK-050)

1. **`DurationTurns` is `BattleState.Turn` at terminal resolution** —
   the battle's Turn count under `GAME_RULES.md` §2 item 1 (a Turn is a
   successfully resolved player Swap/Action), captured on the battle-end
   path. Under documented rules it equals the number of committed Swaps:
   one committed Swap begins exactly one Turn, and a rejected Swap, board
   generation, and a Card cast begin none (`MATCH3_RULES.md` §8.1 items
   1–5, `CARD_RULES.md` §3 item 5). The terminal Turn **is** counted —
   the resolution order places `TurnEnded` before `BattleWon` /
   `BattleLost` (`GAME_EVENTS.md` §1). A battle reaching a terminal state
   before any committed Swap records `0` (`GAME_STATE.md` §2.0.2).
   `Sequence` is a different counter (`MATCH3_RULES.md` §8.2,
   `GAME_STATE.md` §5.1) and is **never** the source; the source of
   truth is `BattleState.Turn` at battle end, not an event count.
2. **`CompletedAt` is the server clock reading captured on the
   battle-end path when the durable result is written** — one value per
   battle (`ARCHITECTURE.md` §4 item 4, `TDD.md` §4 item 2). It is
   server-authoritative and never re-derived from client input or from a
   session at battle end (`GAME_RULES.md` §18, ADR-001,
   `API_CONTRACTS.md` §7 item 1), and it orders the battle-history index
   (`§4` — `BattleResult(PlayerId, CompletedAt DESC)`). No timezone is
   asserted here; the exact column type is an implementation detail
   (header).

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
4. Provisioning mechanisms for static-content rows **other than**
   `BossDefinition` (`PetDefinition`, `CardDefinition`,
   `RelicDefinition`, …) — none is defined here; each remains open
   (`TASK-045` §6 issue 1). No `HasData`, seed, or startup loader exists
   for any of them. For `BossDefinition` the mechanism **is** now defined
   by this document (TASK-052): an EF Core migration that INSERTs the
   three content-defined rows, applied through the existing
   `dotnet ef database update` workflow in every battle-capable
   environment before that environment's first `BattleResult` write —
   no `HasData`, no seed, no startup loader, no separate manual-SQL
   deployment path, no runtime provisioning (§1, `BossDefinition`
    persistence contract item 5). The migration script itself remains an
    implementation detail (item 1); that implementation is **complete**
    (TASK-053) — migration `20260926151112_ProvisionBossDefinitions` was
    applied through `dotnet ef database update`, so the three canonical
    rows are provisioned and every `BattleResult` write's FK target exists
    wherever the migration has been applied (TASK-051 decision A3).
