# Database

**Version:** 1.36 (§2's MVP starter ownership contract **synchronized** with the
TASK-213 decision and the TASK-221 implementation: the Player-creation grant IS
the MVP content grant — **5 Pets, 3 Basic Cards, 10 Relics** as **18 ownership
rows**, up from 1 / 3 / 3 (7 rows) — because MVP has no post-creation
acquisition system (`MVP_SCOPE.md` §1, §3). The composition's §2 item 1 content
lists, its item 3 row counts, and its item 4 row count are updated; the
mechanism, the semantic classification, the idempotency rule, the atomicity
requirement, the no-new-abstraction rule, and the schema are unchanged, and
**no table, column, constraint, index, migration, token, enum mapping, or
gameplay value changes**. Prior 1.35: (§1's Relic contract "encoded in these shapes" note, §2's
starter-relic exclusion note, and §5 item 4's provisioning record
**synchronized** with TASK-184: the six remaining canonical `RelicDefinition`
rows (`relic-burning-curse`, `relic-combo-fang`, `relic-arcane-battery`,
`relic-execution-mark`, `relic-cascade-core`, `relic-battle-instinct`) are now
provisioned via migration `20261004153916_ProvisionRemainingMvpRelicDefinitions`,
so the provisioned content-defined set is `PetDefinition` (5),
`CardDefinition` (8), and `RelicDefinition` (10) and **no Relic row remains
deferred**. **No table, column, constraint, index, schema shape, token, enum
mapping, vocabulary, or gameplay value changes**, and no value is authored: the
six rows' every value is transcribed from `RELIC_RULES.md` §8.5, and the
statements about what TASK-132/TASK-168 did at the time are preserved. Prior
1.34 (§1 note item 5 and §5 item 4 `BossDefinition` provisioning
references **reconciled** per TASK-175: all five MVP `BossDefinition` rows are
now provisioned via EF Core migrations — the initial three via
`20260926151112_ProvisionBossDefinitions` (TASK-053) and the remaining two
(`boss-def-son-thach-ve`, `boss-def-kim-loi-vuong`) via
`20261004094100_ProvisionTwoRemainingMvpBosses`. **No table, column, constraint,
index, migration, stored value, schema shape, token, enum mapping, or gameplay
value changes**; the clarification aligns the documentation with the existing
provisioning migrations. Decision source: TASK-175. Prior 1.33: (§1 note item 3's `resetBehavior` contract **clarified** per
TASK-173: for a **Boss** Passive, the existing `Persistent` token additionally
carries the once-per-battle firing eligibility contract defined in
`PASSIVE_RULES.md` §4 and `BOSS_RULES.md` §6.2.4, so the runtime reads that
eligibility from the definition's declared value. **No table, column,
constraint, index, migration, stored value, schema shape, token, enum mapping, or
gameplay value changes**; the token set remains exactly
`Default` | `Partial` | `Persistent` with `Persistent` → `NoReset`, and the
clarification is a statement about the existing value's meaning. Decision
source: TASK-173. Prior 1.32 (§1 `BossDefinition` **content-set reconciliation** per
TASK-172: the document described the content-defined Boss set as **3** and the
row set as **exactly three**, all written before the TASK-171 Product Owner
decisions were applied to `BOSS_RULES.md` §6. The set is now stated as
**content-defined: 5** — the three provisioned rows plus **Sơn Thạch Vệ /
`boss-def-son-thach-ve`** and **Kim Lôi Vương / `boss-def-kim-loi-vuong`** —
correcting §1's `BossDefinition` entity block, the §1 item-2 canonical-value
list, the §1 note item 5 row-set and row-content statements, and §5 item 4's
rule-(a) deferral record. **No table, column, constraint, index, migration,
stored value, schema shape, vocabulary, gameplay value, or provisioned row
changes**: every corrected count, key, and identity is transcribed from
`BOSS_RULES.md` §6/§6.4 and TASK-171, and the TASK-052/TASK-053 provisioning
record is preserved as statements about what those tasks did at the time. The
two new rows remain **provisioned-later**, not content-blocked — no row is
inserted by this document. Prior 1.31 (§1 CardDefinition content-set reconciliation per TASK-169:
the document described the complete content-defined `CardDefinition` set as
**3 Basic + 3 Pet Skill = six rows** in five places, all written before TASK-168
provisioned the two remaining Pet Skill Cards. The set is now stated as **3 Basic
Cards (`CARD_RULES.md` §2) + 5 Pet Skill Cards (`CARD_RULES.md` §4.1)** — the
three authored first plus **Venomous Bloom / `card-venomous-bloom`** and
**Earthshaker / `card-earthshaker`** — correcting §1's `CardDefinition` block,
§1 Card contract item 7, §1 Card contract item 8, §3's `effectType`
constraint-line parenthetical, and §5 item 4's provisioning record.
**No table, column, constraint, index, migration, stored value, schema shape,
vocabulary, or gameplay value changes**: every corrected count, key, and name is
transcribed from `CARD_RULES.md` §2/§4.1 and from what TASK-168 already
provisioned, and the historical migration records (TASK-085's six-row Card set,
TASK-112's encoding, TASK-132's Relic re-encoding) are preserved as statements
about what those tasks did at the time. Prior 1.30 (§5 item 4 rule (a) — **stale-example correction only** per
TASK-167: the rule cited "the two TBD Signature Skills — `CARD_RULES.md` §4.1" as
an example of a deferred row, which the TASK-166 Signature Skill decisions
retired. The rule itself is **unchanged** — only content-defined rows may be
inserted — and its example now records that no Pet row remains content-deferred
while the Relic deferral stands. **No table, column, constraint, index,
migration, stored value, schema shape, or vocabulary changes**, and no row is
provisioned by this edit. Prior 1.29: (§1's Relic contract note item 5 synchronized with the landed
Relic resolution stage — `GAME_RULES.md` §17 step 11 now evaluates the stored
`Trigger`/`Condition`/`EffectDefinition` values, so the item no longer records
that step as unimplemented (TASK-133). **This is a status synchronization only: no
table, column, constraint, index, migration, or stored value changes**, no
gameplay rule is added, and no value is authored — the structured shape and the
four encoded rows are unchanged. Prior 1.28: (§1's Relic contract note item 5 clarified — it now records
that the `CardCost` (`GAME_STATE.md` §2.3.5, TASK-134) and `ATK`
(`GAME_STATE.md` §2.3.7, TASK-136) runtime carriers are **active battle state,
not persisted content**, so neither is a `RelicDefinition` member and neither
adds a table, a column, or a migration. This is a scope clarification only: **no
schema, column, constraint, index, or migration changes**, no gameplay rule is
added, and no value is authored. The `RelicDefinition` structured shape and the
four encoded rows are unchanged. Prior 1.27: (§1's `RelicDefinition` block, the Relic
`EffectDefinition`/`Condition` contract note, §3's constraints, and §5 item 4's
status — **structured Relic storage landed** per TASK-132, implementing
TASK-131 D1/D2/D3/D5/D9: `RelicDefinition.Condition` and
`RelicDefinition.EffectDefinition` are now `jsonb` holding the structured shapes
`RELIC_RULES.md` §8.1–§8.3 defines, the migration's requirement is satisfied,
and the four provisioned rows hold §8.5's values. The note now records the two
stored member shapes and that the four rows are encoded. **No gameplay rule,
magnitude, threshold, target, lifetime, or vocabulary changed** — every encoded
value is transcribed from `RELIC_RULES.md` §8.5, `RelicDefinition.Trigger` is
unchanged (§8.5 item 3), no column or table was added, and `Burning Curse`
stays deferred [TASK-132]. Prior 1.26: §1 item 3's `threshold` note, the §3 provisioning row-content
note, and the §6 constraint entry — **stale characterization correction only**
per TASK-124: `threshold = null` was described as meaning the Passive "is
always active", a semantic the TASK-123 D-2a decision retired when Thủy Ma's
trigger became Battle Start. `null` now reads as "no match-charging threshold",
with the activation trigger explicitly defined per Passive by
`BOSS_RULES.md` §6.2. **The storage constraint itself is unchanged** — `null`
still denotes a non-match-charged Passive, `0` is still never the sentinel, and
**no table, column, storage member, schema shape, or vocabulary changed**.
Prior 1.25: §1 Card `EffectDefinition` contract item 1's closing
paragraph — **dependency-pointer correction only**: the sentence that
delegated `scope = "NextAttack"` to `CARD_RULES.md` §4.1 as the owning rule is
re-pointed at the document that now owns it, `COMBAT_RULES.md` §3.3 items 7–10
(modifier lifetime, qualifying-attack consumption boundary, source-specific
removal), with the state named as `GAME_STATE.md` §2.3.4 (`ADR-017`);
`CARD_RULES.md` §4.1 and `PASSIVE_RULES.md` §7/§8 are recorded as owning each
source's own contribution. **No storage member, schema shape, value, vocabulary,
or `valueType` semantic changed** — `scope` is still a storage member, exactly
one scope value is still defined, and no column or table is added [TASK-117].
Prior 1.24: (§1 Card `EffectDefinition` contract item 8 — **status
synchronization only**: the six content-defined `CardDefinition` rows are now
**encoded** in the ARRAY shape v1.23 defined, so the "not yet re-encoded" /
"no row conforms to the full contract" wording is retired and the recorded
`card-iron-fang` identity correction is marked applied (`Damage` + `Crit`, in
agreement with `CARD_RULES.md` §4.1). The three `Undetermined` markers are
retired because §4.1 authors every Pet Skill magnitude (TASK-110); the member
itself remains valid for an effect whose magnitude is not yet authored (item 9).
**No rule, magnitude, schema shape, vocabulary, or valueType semantic changed** —
item 9's statement of `Undetermined` and items 1–7's contract are untouched
[TASK-112]. Prior 1.23: (§1 Card `EffectDefinition` contract **extended** per TASK-111
Product Owner decisions D-1/D-2/D-3/D-4/D-5: the stored value is now an **ARRAY**
of effect objects, one element per effect, each keeping its own
`effectType`/`valueType`/`value` triple (D-1/D-1a/D-1b, uniform for one- and
multi-effect Cards); the `effectType` closed set becomes
`Heal | Shield | Power | Damage | Burn | Crit` (D-2, with `Damage` distinct from
`Power`, which still denotes Power Charge — D-4); the `valueType` set becomes
`Flat | PercentMaxHp | PercentagePoints | Undetermined` (D-3, with
`Undetermined` still valid); `Burn` elements carry `duration` (Turns, `value` =
damage per tick) and `Crit` elements carry `scope` = `NextAttack` (`value` =
percentage points) as extra members beside `value` (D-3); element order is
**not** semantic (D-5). **No gameplay rule, magnitude, or balance value changed:**
every extra member stores a rule already owned by `CARD_RULES.md` §4.1,
`COMBAT_RULES.md` §5 and `GAME_RULES.md` §17 step 19a, and §2's values are
byte-identical. The six provisioned rows are **not yet re-encoded** into the
array shape, and retiring their `Undetermined` markers is the downstream
encoding task's act (D-6/D-6b); the stale "§4.1 authors no magnitude" statements
TASK-110 made false are corrected to reference the authored §4.1 [TASK-111].
Prior 1.22.1: (§1 Relic `EffectDefinition`/`Condition` contract recorded per
TASK-131 D1/D2/D3/D5/D9 — the Relic member is now STRUCTURED, superseding
TASK-082 R2-7 for it and closing the boundary TASK-109 left open; the
`character varying(128)` columns are recorded as insufficient and the migration
to `jsonb` is a separate follow-up task. `RELIC_RULES.md` §8 is the canonical
owner of the representation. No schema change and no gameplay value changed
here [TASK-131]. Prior 1.22: (§1 `CardDefinition.EffectDefinition` became a STRUCTURED
effect rule — a `jsonb` object carrying `effectType`, `valueType`, and `value` —
per TASK-108 decisions D-1/D-2, implemented by TASK-109. This **supersedes
TASK-082 decision R2-7 for the `CardDefinition` member only**: that decision had
required the owning domain document's verbatim rule text within a 128-character
prose column and barred any effect-id vocabulary. The `RelicDefinition` block was
**unchanged** by that task and R2-7 remained in force for it (Relic effect
resolution is `ROADMAP.md` Phase 2; no Relic decision was taken then — now
superseded by TASK-131). A new §1 note records the
contract, the supersession, and its exact scope, and §3 gains the corresponding
constraint. No gameplay value, rule, or balance figure changed: every encoded
magnitude is transcribed from `CARD_RULES.md` §2/§4.1, and §2's values are
byte-identical [TASK-109].
Prior 1.21: (§5 item 4 Pet/Card/Relic provisioning implementation status
synchronized with the landed migration — the item now records the completed
`20260929152651_ProvisionPetCardRelicContentDefinitions` migration (TASK-085:
`PetDefinition` 3 rows, `CardDefinition` 6 rows, `RelicDefinition` 4 rows) and the
completed TASK-083 starter-ownership initialization, in the same form the item
already uses for the completed `BossDefinition` migration; the provisioning
contract itself — mechanism, permitted rows, value sourcing, and the §1 entity
definitions — is unchanged [TASK-097].
Prior 1.20: (§1 `BattleResult` entity definition and "Reward semantics for
`RewardSummary`" synchronized with the landed 8-member `RewardSummary` contract
per TASK-089 [TASK-065, TASK-067, TASK-068] — records the 8-member projection
covering Player and Pet progression tracks [`playerXpGained`, `newPlayerXp`,
`playerLeveledUp`, `newPlayerLevel`, `petXpGained`, `newPetXp`, `petLeveledUp`,
`newPetLevel`]; removes obsolete `{}` staging phrases and deferred Pet member
list wording; preserves symmetrical victory/defeat shape per TASK-068 Option A).
Prior 1.19: (TASK-084 MVP starter ownership contract resolved: §2 deterministic starter
composition recorded [1 Pet: Xích Lang / `pet-xich-lang`; 3 Basic Cards: Heal / `card-heal`,
Shield / `card-shield`, Power Charge / `card-power-charge`; 3 Relics: Berserker Core /
`relic-berserker-core`, Mana Crystal / `relic-mana-crystal`, Assassin Eye / `relic-assassin-eye`];
semantic classification recorded as MVP bootstrap/test content, not final acquisition gameplay;
Player-creation single-grant semantics defined; atomicity confirmed achievable via single
scoped `GameDbContext.SaveChangesAsync` with concurrent-first-login batch-discard prerequisite;
no new architectural abstraction or gameplay system introduced). Prior 1.18: (TASK-082 Pet/Card/Relic content-provisioning contract
recorded: §1 content-definition key value forms (`pet-`/`card-`/`relic-` +
ASCII kebab-case slug of the documented display name), `PetDefinition.Identity`
= display text, `EffectDefinition` = the owning domain document's verbatim
effect rule text (≤128 chars, no effect-id vocabulary), `LoadoutCopyLimit`
concrete values owned by `CARD_RULES.md` §1 (MVP value 1); §2 MVP
starter-ownership composition decided (1 Pet / 3 Cards / 3–5 Relics from
content-defined rows only; exact IDs + mechanism deferred to a follow-up
implementation task); §5 item 4 provisioning mechanism now defined for
`PetDefinition`/`CardDefinition`/`RelicDefinition` (EF Core migration-INSERT,
the TASK-052 precedent; no `HasData`/seed/startup loader/JSON pipeline/
external content service). Prior 1.17: §1 "Reward semantics for `RewardSummary`" **defeat shape
resolved** per TASK-068 human decision (**Option A**) — the contradiction
between item 1's per-outcome Player-track member list and the former
"a `"defeat"` battle carries no line items" wording is removed: the
Player-track member set applies to **both** outcomes, with
`playerXpGained = 100` on `BattleWon` and `playerXpGained = 0` on
`BattleLost`; item 5 now states the member set explicitly, the §1 entity-block
staging sentence no longer asserts a defeat-specific empty shape, and every
reward value is unchanged (Player XP `+100`/`+0`, Pet XP `+100`/`+0`, Pet XP
cap/formula, `{}` staging semantics). Prior 1.16: §1/§3 Pet XP contract **finalized** per TASK-062 — the
twelve Pet XP decisions are now decided: `Pet.XP` initial `0`, hard-capped at
`4900` with no overflow, `Pet.Level` range 1–50, initial `1`, formula
`min(floor(Pet.XP / 100) + 1, 50)`; the §3 "UNRESOLVED" Pet constraints are
replaced by concrete constraints, and the `RewardSummary` Pet track moves
from "not finalizable" to "semantics decided, member list deferred to the
implementation task". Prior 1.15: §1/§3 XP persistence contract resolved per TASK-059:
`Player.XP` added as a persisted column with the decided Player XP →
Player Level contract, `Pet.XP` added as a per-instance Pet column with
its balance values at that time explicitly recorded as unresolved
(**superseded by version 1.16 — they are now decided**), the retired
`PetDefinition.PetLevelMultiplier` column removed from the documented
contract and `Pet.Level` re-sourced from the Pet's own XP, and the
`RewardSummary` member list re-assigned to this document with the
Pet-track members explicitly dependent on the Pet XP decisions then
unresolved (**superseded by version 1.16**);
ADR-016. Prior 1.14: §1 "Identity and reward sourcing for `BattleResult`"
clarified per TASK-056 human decision: **`BattleResult` persistence requires
no Player combat-readiness condition** — the documented prerequisites are
unchanged and no `IsCombatReady` predicate is part of the contract;
the pre-existing duplicate item-3 numbering is deliberately left as-is
(the new item is inserted as a third item 3, adjacent to the
unresolved-`BossDefinition` fail-closed rule it belongs with, and no item
was renumbered); no ADR is required because this document is the canonical
owner of the contract; prior 1.13: §1 `BossDefinition` provisioning **implementation
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
semantics (no match-charging threshold; then characterized as "always-active",
superseded above in 1.26, no 0-sentinel), persistent-identity vs
combat-stat source split, no-invented-provisioning guard and
content-defined-rows-only scope clarified; §3 BossDefinition constraints
added; prior 1.6: §1 BattleResult identity/reward sourcing documented —
`BattleResultId` = `BattleId` (one row per battle), `PlayerId` value from
`BattleState.PlayerId` (`GAME_STATE.md` §2.8), `PetInstanceId` value from
`BattleState.PetState.PetId` (`GAME_STATE.md` §2.3 — the owned Pet
instance), `RewardSummary` staging value = empty JSON object with no line
items until its member list is defined (ADR-014, TASK-042 — that member
list is owned by this document as of version 1.15; prior 1.6 wording
attributed it to TASK-033); prior 1.5: §1
CardDefinition.LoadoutCopyLimit added — required
per-battle-loadout copy limit per CARD_RULES.md §1; explicit value
required, no default; concrete values deferred to content/balance;
prior 1.4: §1 PetLevelMultiplier type/range and Pet.Level floor
semantics synchronized with PET_RULES.md §5 derivation contract;
§3 item reference corrected to §5 item 10; prior 1.3: §3 initial
Player.Level value added — a newly created
Player starts at Level 1, per PET_RULES.md §5 item 10 (that rule now lives
at PET_RULES.md §5.4 item 5 after the TASK-059 §5 rewrite — this is a
historical changelog entry, not a current attribution); prior 1.2: §1
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
Account                          (web user account; ADR-020)
├── AccountId (PK)                (UUID, primary key)
├── Username                     (varchar(32), NOT NULL, UNIQUE, case-insensitive,
│                                 alphanumeric + underscore, 3–32 chars)
├── PasswordHash                 (varchar, NOT NULL — PBKDF2 hash with salt)
└── CreatedAt                    (timestamp with time zone, NOT NULL)

Player
├── PlayerId (PK)
├── AccountId (FK → Account, unique, NOT NULL; ADR-020)
├── XP                              (int, NOT NULL, default 0 — persisted
│                                    account progression; UNCAPPED, keeps
│                                    accumulating after Level 50;
│                                    COMBAT_RULES.md §7)
├── Level                           (1–50 — Player XP → Player Level
│                                    contract owned by COMBAT_RULES.md §7;
│                                    derived from Player.XP, persistent
│                                    account attribute, capped at 50, NO
│                                    combat stats; ADR-016)
└── CreatedAt

Pet                              (a player's OWNED instance of a Pet)
├── PetInstanceId (PK)
├── PlayerId (FK → Player)
├── PetDefinitionId (FK → PetDefinition)
├── Tier
├── Star
├── XP                              (int, NOT NULL, default 0 — per-INSTANCE
│                                    combat progression; belongs to the Pet
│                                    instance, NOT to PetDefinition
│                                    (PET_RULES.md §5.1, ADR-016). Range
│                                    0–4900 with a HARD cap of 4900 —
│                                    Pet XP stops accumulating at Level 50
│                                    and no overflow is retained
│                                    (PET_RULES.md §5.5). Initial value 0
│                                    (PET_RULES.md §5.2))
├── Level                           (per-instance Pet Level derived from this
│                                    Pet's own XP — PET_RULES.md §5.4;
│                                    independent of Player Level. Range
│                                    1–50, initial value 1
│                                    (PET_RULES.md §5.2, §5.5))
└── AcquiredAt

PetDefinition                    (static content — only content-defined
│                                  rows may be provisioned, §5 item 4;
│                                  TASK-082 decision A)
├── PetDefinitionId (PK)          (value form `pet-<ascii-kebab-case-name>`,
│                                  e.g. "pet-xich-lang" — ASCII kebab-case of
│                                  the Pet's documented display name
│                                  (TASK-082 decision B / R2-9), mirroring
│                                  `boss-def-<ascii-kebab-case-name>`; this
│                                  key IS the Pet's technical identity —
│                                  `PET_RULES.md` §1 (R2-10); content-supplied,
│                                  never database-generated, never slugified
│                                  at runtime)
├── Identity                      (display text — "Thanh Xà", "Xích Lang",
│                                  ...; NOT a technical identity;
│                                  `PET_RULES.md` §1, `API_CONTRACTS.md` §5.1)
├── Element
├── PassiveDefinition              (threshold/effect reference —
│                                  PASSIVE_RULES.md)
└── SignatureSkillCardId (FK → CardDefinition)

CardDefinition                    (static content — MVP scope target
│                                  `MVP_SCOPE.md` §1; only content-defined
│                                  rows may be provisioned, §5 item 4; the
│                                  defined rows are the 3 Basic Cards
│                                  (`CARD_RULES.md` §2) and the 5 Pet Skill
│                                  Cards (`CARD_RULES.md` §4.1) — the three
│                                  authored first (TASK-110) plus Venomous
│                                  Bloom / `card-venomous-bloom` and
│                                  Earthshaker / `card-earthshaker`
│                                  (TASK-166 decisions, authored by TASK-167,
│                                  provisioned by TASK-168) — TASK-082
│                                  decision A / R1-1)
├── CardDefinitionId (PK)         (value form `card-<ascii-kebab-case-name>`,
│                                  e.g. "card-heal" — ASCII kebab-case of the
│                                  Card's documented name (TASK-082 decision
│                                  B / R2-9); content-supplied, never
│                                  database-generated)
├── Name
├── Category                       ("Basic" | "PetSkill")
├── PowerCost
├── LoadoutCopyLimit               (required — max occurrences of this
│                                  CardDefinition in one submitted 3-card
│                                  Basic loadout; `CARD_RULES.md` §1; explicit
│                                  value required, no default; concrete MVP
│                                  values are owned by `CARD_RULES.md` §1
│                                  (TASK-082 decision C / R1-5 / R2-6))
└── EffectDefinition               (JSON ARRAY, NOT NULL — the Card's
                                   STRUCTURED effect rules, one element per
                                   effect: which domain effect the Card
                                   applies and its value with the value's
                                   interpretation. Member list below.
                                   `CARD_RULES.md` §2/§4.1 owns
                                   the effect values; THIS document owns
                                   the storage shape.
                                   TASK-108 decisions D-1/D-2 SUPERSEDE
                                   TASK-082 decision R2-7 FOR THIS MEMBER
                                   ONLY — the previous contract was the
                                   owning document's verbatim rule text in
                                   a ≤128-char prose column, with no
                                   effect-id vocabulary. R2-7 was ALSO
                                   superseded for `RelicDefinition.
                                   EffectDefinition` by TASK-131 D1 — no
                                   member remains under it. TASK-111 decisions
                                   D-1/D-2/D-3/D-4/D-5 EXTEND the contract:
                                   the value is an ARRAY of effect objects,
                                   the `effectType` set gains Damage/Burn/
                                   Crit, `valueType` gains PercentagePoints,
                                   `Burn`/`Crit` carry `duration`/`scope`
                                   beside `value`, and element order is not
                                   semantic. See "Card EffectDefinition
                                   contract" below.)

PlayerUnlockedCard               (Player owns Card unlocks — ADR-012;
│                                  MVP Cards have no Tier/Star/Level, so an
│                                  unlock flag is sufficient)
├── PlayerId (FK → Player)
└── CardDefinitionId (FK → CardDefinition)

RelicDefinition                    (static content — MVP scope target
│                                   `MVP_SCOPE.md` §1; only content-defined
│                                   rows may be provisioned, §5 item 4; the
│                                   canonical defined rows are owned by
│                                   `RELIC_RULES.md` §6 — TASK-176)
├── RelicDefinitionId (PK)         (value form `relic-<ascii-kebab-case-name>`,
│                                   e.g. "relic-berserker-core" — ASCII
│                                   kebab-case of the Relic's documented name
│                                   (TASK-082 decision B / R2-9);
│                                   content-supplied, never
│                                   database-generated)
├── Name
├── Trigger
├── Condition                       (jsonb, NULL — STRUCTURED, optional:
│                                    `conditionType` + `threshold`, per
│                                    RELIC_RULES.md §8.1. The absent case is
│                                    the documented "no extra condition"
│                                    (§8.1 item 4), not a sentinel)
└── EffectDefinition                (jsonb, NOT NULL — STRUCTURED: an ARRAY
                                     of effect objects, per TASK-131
                                     D1/D2/D3 and RELIC_RULES.md §8.2–§8.4.
                                     This SUPERSEDES TASK-082 R2-7 for this
                                     member, which had required the owning
                                     domain document's verbatim rule text
                                     within a 128-char prose column. The
                                     migration from `character varying(128)`
                                     to `jsonb`, for this member and for
                                     `Condition`, is LANDED — TASK-132,
                                     migration
                                     `20261003074309_StructureRelicDefinitionStructuredColumns`;
                                     see §1 "Relic `EffectDefinition` and
                                     `Condition` contract" below)

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
 │                                    content-defined: 5, BOSS_RULES.md
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
└── RewardSummary                        (JSON — member list owned by THIS
                                           document; see "Reward semantics"
                                           below. The 8-member projection
                                           covering Player and Pet tracks
                                           for both victory and defeat)
```

**Card `EffectDefinition` contract.** (TASK-109, implementing TASK-108 D-1/D-2;
extended by TASK-111 D-1/D-2/D-3/D-4/D-5)

1. **The stored value is a structured effect rule, not prose, and it is an
   ARRAY of effects.** The column is `jsonb` (NOT NULL). Since TASK-111 **D-1**
   its value is a **sequence of effect objects** — a Card with one effect stores
   a one-element array, and a Card with several effects stores one element per
   effect. The shape is uniform for every Card:

   ```json
   [ { "effectType": "Shield", "valueType": "PercentMaxHp", "value": 20 } ]
   ```

   Each element carries **its own** `effectType`/`valueType`/`value` triple
   (TASK-111 **D-1a**), and an effect that needs more than a single magnitude
   carries **additional members beside `value` in the same element** (TASK-111
   **D-3** — see item 3).

   - `effectType` (string) — which domain effect the Card applies. The closed
     set is `Heal` | `Shield` | `Power` | `Damage` | `Burn` | `Crit`.
     `Heal`, `Shield`, and `Power` are the three effects `CARD_RULES.md` §2
     documents; `Damage`, `Burn`, and `Crit` are the three the Product Owner
     named for the §4.1 Pet Skill Cards (TASK-111 **D-2**, which also confirms
     the first three remain). No other identity is defined. It is the effect
     identity carrier (TASK-108 **D-1**), so the runtime must never derive the
     effect from parsed prose, from the Card's `Name`, from `CardDefinitionId`
     mapping, or from hardcoded card-specific logic.
     `Damage` is a member **distinct from `Power`** (TASK-111 **D-4**): `Power`
     continues to denote Power Charge (`CARD_RULES.md` §2), while `Damage`
     carries a damage-dealing effect's base value — the input to
     `COMBAT_RULES.md` §3 step 1, whose magnitudes `CARD_RULES.md` §4.1 owns.
   - `valueType` (string) — how `value` is interpreted: `Flat` | `PercentMaxHp` |
     `PercentagePoints`, or `Undetermined`. `Flat` and `PercentMaxHp` are
     exactly the interpretations `CARD_RULES.md` §2 distinguishes ("20% of its
     Max HP" versus "25 Power"); `PercentagePoints` is the percentage-point
     interpretation `CARD_RULES.md` §4.1's Crit increase states (TASK-111
     **D-3**). A percentage is **not** pre-resolved to an absolute amount: the
     active Pet's `MaxHP` is battle state (`GAME_STATE.md` §2.3) and is read when
     the effect is applied. `Undetermined` is **not an interpretation** — it
     records that the owning document states no magnitude for the effect yet,
     and it **remains valid** for such effects (TASK-111 **D-3**). It is why no
     magnitude has to be invented to make such a row representable (item 7).
   - `value` (int) — the effect's magnitude, transcribed from
     `CARD_RULES.md` §2/§4.1 through the `valueType` above. It is the **effect**
     value, never the Card's Cost: Power Charge's Cost is `0` by design while
     its effect grants Power (`CARD_RULES.md` §2 item 3), and the Cost is the
     separate `PowerCost` column. It is **present iff `valueType` interprets
     one** — an `Undetermined` effect carries no `value` member at all, never
     `0` and never `null`, so an unauthored magnitude cannot be read as a number.
   - **Per-effect extra members** (TASK-111 **D-3**) — present only for effects
     that require a parameter `value` alone cannot carry:
     - `duration` (int) — on a `Burn` element: the number of Turns the effect
       lasts, in the authoritative Turn / End-Turn-tick unit
       (`GAME_RULES.md` §17 step 19a, `COMBAT_RULES.md` §5.2). `value` on a
       `Burn` element is the **damage per tick**. Example:

       ```json
       [ { "effectType": "Burn", "valueType": "Flat", "value": 50,
           "duration": 2 } ]
       ```

     - `scope` (string) — on a `Crit` element: which damage instances the
       increase applies to. The defined value is `NextAttack`. `value` on a
       `Crit` element is the **increase in percentage points**. Example:

       ```json
       [ { "effectType": "Crit", "valueType": "PercentagePoints", "value": 10,
           "scope": "NextAttack" } ]
       ```

     `duration` and `scope` are **storage members for rules the owning domain
     documents already state**; they author no gameplay. Burn's tick schedule
     and duration unit are owned by `GAME_RULES.md` §17 step 19a and
     `COMBAT_RULES.md` §5.1–§5.3; Crit's next-attack scope — the modifier's
     lifetime, the qualifying-attack consumption boundary, and the Crit
     source composition it participates in — is owned by
     `COMBAT_RULES.md` §3.3 items 7–10, with the state it is held in defined
     by `GAME_STATE.md` §2.3.4 (`ADR-017`). `CARD_RULES.md` §4.1 owns Iron
     Fang's own Crit `value` (+10 percentage points) and `PASSIVE_RULES.md`
     §7/§8 owns Bạch Hổ's Passive contribution; both use the same scope and
     both reference the composition rule rather than restating it. Note that
     `scope` remains a **storage** member and is unchanged by that ownership:
     this contract still defines exactly one scope value, `NextAttack`, and
     the runtime meaning behind it now lives in the document named above. No
     further extra member is defined, and none may be added without a recorded
     owner decision.
2. **The member names are the contract.** `effectType`, `valueType`, and
   `value` are TASK-108 **D-2**'s, and `duration` / `scope` are TASK-111
   **D-3**'s; they are fixed here. The internal representation maps to them, not
   vice versa — the same convention `PassiveDefinition` and `SkillDefinition`
   follow (note 3/note 4 above). The type members are stored as their **member
   names**, not as enum ordinals, so a persisted row is self-describing.
3. **Ordering within the array is NOT semantic.** (TASK-111 **D-5**) The stored
   sequence is a storage sequence only: no rule reads element positions, no
   effect resolves "before" or "after" another because of its index, and a
   serializer must not imply an order carries meaning — the same non-semantic
   convention `GAME_STATE.md` §2.3.1 item 10 records for `StatusEffects[]`.
   This is a **storage** statement: it authors no resolution step, no priority,
   and no ordering rule, and it does not change `GAME_RULES.md` §17 or
   `CARD_RULES.md` §3/§4.
4. **The effect magnitude is data-driven and value-sourced, not authored
   here.** `CARD_RULES.md` §2/§4.1 remains the sole owner of every magnitude;
   the migration that populates this column transcribes those values and
   computes and invents none (`§5` item 4 rule (b)). TASK-108 supplied its
   JSON shapes as **examples only** — they disagree with `CARD_RULES.md` §2
   and are deliberately not the encoded values (TASK-108 **D-6** boundary).
5. **The value is read, never executed, by this contract.** Nothing in this
   document, and nothing in the task that introduced or extended it, applies a
   Card effect: Heal, Shield, Power, damage, Burn, and Crit application remain
   owned by `COMBAT_RULES.md` §3/§4/§5 and `GAME_RULES.md` §12 through their
   existing Domain write sites. Card casting and effect resolution are
   `CARD_RULES.md` §3's separate, unimplemented concern.
6. **A stored value that is not a well-formed structured effect is rejected
   loudly.** There is no fallback magnitude, no default, no prose fallback,
   and no silent no-op for an unrecognized `effectType`, an unrecognized
   `valueType`, a missing `value`, or a missing required extra member (`duration`
   on `Burn`, `scope` on `Crit`) — the same "explicit value required, no
   default" standard `LoadoutCopyLimit` carries below. A malformed or truncated
   value therefore surfaces as a failure at the read, not as a Card that quietly
   does nothing.
7. **THIS SUPERSEDES TASK-082 DECISION R2-7 FOR `CardDefinition` ONLY.**
   R2-7 (DONE, immutable) required `EffectDefinition` to be the owning domain
   document's **verbatim rule text**, and explicitly forbade "an
   `effect-{slug}` vocabulary or any new effect-reference identifier system".
   TASK-108 **D-1** requires exactly such a structured contract, so the two
   cannot both hold and D-1 governs here. This supersession is **scoped to
   the `CardDefinition` member**:
   - `RelicDefinition.EffectDefinition` **was** left under R2-7's verbatim
     rule text in its unchanged `character varying(128)` column by that task.
     **TASK-131 D1 has since superseded R2-7 for this member**: the Relic
     contract is now structured (`RELIC_RULES.md` §8), and TASK-131 D9 records
     that its `varchar(128)` storage is insufficient, with the migration left
     to a separate follow-up task. See the "Relic `EffectDefinition` and
     `Condition` contract" note above. That task's own statement that "No Relic
     decision exists" described the repository at that time and no longer
     holds.
   - Only the `CardDefinition` rows in provisioning were migrated, per
     `CARD_RULES.md` §2/§4.1. The three Basic Cards carry a full structured
     effect because §2 authors their effect completely. The three Pet Skill
     Cards authored at that time carry the effect identity §4.1 named, because
     §4.1 now authors their magnitudes (TASK-110) — **see item 8, which now
     records the current encoded state.** The **five** Pet Skill Cards §4.1 now
     defines are the current set (item 8); the two later-authored ones
     (Venomous Bloom, Earthshaker) were provisioned already encoded, so no
     identity-only stage applies to them.
   - TASK-082 itself is not modified, re-opened, or re-statused
     (`TASK_LIFECYCLE.md` §3 — completed tasks are immutable).
8. **The provisioned rows are encoded in this shape.** (TASK-111 **D-6**;
   encoded by TASK-112; extended by TASK-168) **All eight** content-defined
   `CardDefinition` rows now hold the array shape above — the three §2 Basic
   Cards, the three §4.1 Pet Skill rows authored at the time of the encoding,
   and the two §4.1 Pet Skill rows added since (Venomous Bloom /
   `card-venomous-bloom` and Earthshaker / `card-earthshaker`, TASK-166
   decisions applied by TASK-167). The three §2 Basic Card rows are
   **contract-compatible** with the shape — their single effect is the array's
   single element, stored as a one-element array (D-1b) — and the §4.1 Pet
   Skill rows store **one element per effect** §4.1 states (two each for
   Inferno, Tidal Barrier, and Venomous Bloom; one for Iron Fang and
   Earthshaker), with every magnitude transcribed from `CARD_RULES.md`
   §2/§4.1 and no value computed or invented (§5 item 4 rule (b)). The two
   TASK-168 rows were written **directly in that ARRAY shape** at provisioning
   time — no further encoding migration exists or is needed for them. Under
   **D-6b** the three `Undetermined` markers are **retired**: §4.1
   now authors every Pet Skill magnitude (TASK-110), so **no provisioned content
   row remains in that state** — the member itself stays valid for an effect
   whose magnitude is not yet authored (item 9), but nothing provisioned needs
   it. The `card-iron-fang` identity correction recorded here for that task is
   **applied**: the row's damage effect now carries `effectType` `Damage` and its
   second element is `Crit` (`PercentagePoints`, `scope` `NextAttack`), so the
   row agrees with `CARD_RULES.md` §4.1 (Iron Fang deals damage and raises Crit
   chance) rather than the superseded `Power` placeholder. **Every row now
   conforms to the full contract**, and a reader may treat the present rows as
   the contract's encoded form. No rule, magnitude, or schema shape was changed
   by that encoding or by the later provisioning: the column remained
   `jsonb NOT NULL`, no column or table was added, `RelicDefinition` was
   untouched, and no effect is applied anywhere
   (item 5).
9. **An unauthored magnitude is represented, never invented.** (TASK-111
   **D-3**: `Undetermined` "remains valid for effects whose authored magnitude
   is not yet determined.") An `Undetermined` effect stores the effect identity
   with `valueType` `Undetermined` and no `value`, so the element is
   representable and readable while no percentage, HP value, borrowed
   Basic-Card value, balance-derived figure, or placeholder is authored
   (`AGENTS.md` §7). An `Undetermined` effect is **not resolvable**: a caller
   must treat it as an open content gap and must never substitute a value.
   The authored rule text and magnitudes remain owned by `CARD_RULES.md` §4.1
   and are not restated here as a second source for them.

**Relic `EffectDefinition` and `Condition` contract.** (TASK-131, implementing
D1/D2/D3/D5/D9; the Relic counterpart of the Card contract above)

1. **The representation is owned by `RELIC_RULES.md` §8 and is not restated
   here.** That section is the canonical owner of the Relic
   Trigger/Condition/Effect contract: `EffectDefinition` as a structured
   `EffectDefinition[]`, its `valueType` set, its `target`/`lifetime`
   vocabulary, the `Condition` forms, and their allowed combinations. This
   document records only the **storage consequence** below.
2. **THIS SUPERSEDES TASK-082 DECISION R2-7 FOR `RelicDefinition.EffectDefinition`.**
   R2-7 (DONE, immutable) required `EffectDefinition` to be the owning domain
   document's **verbatim rule text** and expressly forbade "an `effect-{slug}`
   vocabulary or any new effect-reference identifier system". TASK-131 **D1**
   requires a structured contract, so the two cannot both hold and D1 governs
   here. Combined with TASK-109's earlier Card-scoped supersession, **R2-7 is
   now superseded for both members it governed and no member remains under it.**
   TASK-082 itself is not modified, re-opened, or re-statused
   (`TASK_LIFECYCLE.md` §3 — completed tasks are immutable). This closes the
   boundary TASK-109 recorded when it stated R2-7 "REMAINS IN FORCE for
   `RelicDefinition.EffectDefinition`" — that statement described the repository
   at that time and is superseded by a recorded decision, not by an edit to it.
3. **The previous `character varying(128)` columns are INSUFFICIENT.**
   (TASK-131 **D9**) `RelicDefinition.EffectDefinition` and
   `RelicDefinition.Condition` cannot hold the decided structured
   representation in a 128-character prose column. The `CardDefinition`
   precedent already required `jsonb` for the identical contract shape (see the
   Card contract above). Both columns are now `jsonb` — see item 4.
4. **The migration was a SEPARATE follow-up task, and it has LANDED.** (TASK-131
   **D9**; performed by TASK-132) Per `AGENTS.md` §18 the schema change is gated
   on its own task; that task is TASK-132, whose migration
   `20261003074309_StructureRelicDefinitionStructuredColumns` moves
   `RelicDefinition.Condition` and `RelicDefinition.EffectDefinition` from
   `character varying(128)` to `jsonb` and encodes the four provisioned rows of
   `RELIC_RULES.md` §8.5. **This document records only that storage consequence
   and the shapes below; the representation itself remains `RELIC_RULES.md`
   §8's and is not restated here.** `Condition` stays NULLABLE, because §8.1
   item 4 keeps it optional; `EffectDefinition` stays NOT NULL, because every
   Relic states at least one effect. `RelicDefinition.Trigger` is **unchanged**:
   `RELIC_RULES.md` §8.5 item 3 (TASK-131 D8) leaves §3's closed list alone, so
   it remains a bounded identity string and is not restructured.
5. **No value is authored here, and nothing in this document executes.** Every
   magnitude and threshold the structured form carries is owned by
   `RELIC_RULES.md` §6 and §8.1/§8.5; the migration transcribes those values and
   computes and invents none (`§5` item 4 rule (b)). This document's scope is the
   stored shape only: it evaluates no trigger, applies no effect, and emits no
   `RelicTriggered` event. Those are `GAME_RULES.md` §17 step 11's stage, which
   reads the values stored here and is **implemented** (`RELIC_RULES.md` §8.7) —
   it reads this shape as data and adds no persisted member to it.
   The landed migration changes storage only; it applies no effect and fires no
   trigger, and it introduces no battle-state member, no Redis key, no SignalR
   member, and no Card-cost or ATK modifier carrier **in this document's
   scope**. The `CardCost` runtime carrier (`GAME_STATE.md` §2.3.5, TASK-134)
   and the `ATK` runtime carrier (`GAME_STATE.md` §2.3.7, TASK-136) are
   **active battle state**, not persisted content: neither adds a table, a
   column, or a migration, and neither is a `RelicDefinition` member. This
   document records the content-side storage shape only.
6. **A stored value that is not a well-formed structured effect or condition is
   rejected loudly**, under the same "explicit value required, no default"
   standard item 6 of the Card contract applies — no fallback magnitude, no
   prose fallback, no default threshold, and no silent no-op. This is what makes
   a corrupt column value surface as a failure at the read rather than as a
   Relic that quietly does nothing.
7. **The two stored member shapes.** (TASK-132; the storage consequence of
   `RELIC_RULES.md` §8.1–§8.3, whose vocabulary and allowed combinations this
   document does not restate)
   - `Condition` — a JSON **object** carrying exactly the two members the §8.1
     forms need: the form and its threshold.

     ```json
     { "conditionType": "MatchCountAtLeast", "threshold": 3 }
     ```

   - `EffectDefinition` — a JSON **ARRAY** of effect objects, one element per
     effect, each carrying exactly the five members §8.2–§8.3 define:

     ```json
     [ { "effectType": "ATK", "valueType": "Percentage", "value": 5,
         "target": "Pet", "lifetime": "Battle" } ]
     ```

     The Card-only `duration` (Burn) and `scope` (Crit) members have **no Relic
     counterpart**: `RELIC_RULES.md` §8.3 item 5 defines no member beyond
     `target` and `lifetime`, so a Relic payload carrying one is rejected rather
     than partially accepted.
   - The member names are the contract, and the type members are stored as
     their **member names**, not as enum ordinals — the same convention item 2
     of the Card contract fixes, so a persisted row is self-describing.
   - Array order is **not** semantic (`RELIC_RULES.md` §8.2 item 4), and a
     Relic's resolution order is §4's equip-slot order rather than an effect's
     index within one Relic.
   - **The six remaining rows are encoded in these shapes too.** (TASK-184)
     All ten canonical `RelicDefinition` rows now hold them. The six that
     followed the original four — Burning Curse, Combo Fang, Arcane Battery,
     Execution Mark, Cascade Core, and Battle Instinct — were written **born
     structured** by migration
     `20261004153916_ProvisionRemainingMvpRelicDefinitions`, with every value
     transcribed from `RELIC_RULES.md` §8.5 and none computed or invented
     (`§5` item 4 rule (b)). TASK-176 had content-defined all 10 Relics in
     `RELIC_RULES.md` §6/§8.5 (Burning Curse's static-modifier conflict resolved
     to `OnBattleStart`), and that downstream provisioning task has now landed:
     **no `RelicDefinition` row remains unprovisioned.** No provisioned row
     carries an `Undetermined` element, because §8.5 authors every magnitude; the
     member itself remains valid for an effect whose magnitude is not yet
     authored (`RELIC_RULES.md` §8.2 item 2). No placeholder row was inserted for
     any of the ten. [Prior note: §6 note 3 / §8.5 item 4 kept Burning Curse
     deferred prior to TASK-176, and no placeholder row, Trigger, value, or
     condition is inserted for it. The column set, the `Relic` ownership table,
     and every other table are unchanged by those migrations.

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
   - **Canonical values** for the content-defined MVP Bosses
     (`BOSS_RULES.md` §6), which are **not** the `Identity` values. All
     five rows are provisioned through migrations
     `20260926151112_ProvisionBossDefinitions` and
     `20261004094100_ProvisionTwoRemainingMvpBosses`:
     ```text
     Hỏa Long      boss-def-hoa-long       (Identity: boss-hoa-long)
     Thủy Ma       boss-def-thuy-ma        (Identity: boss-thuy-ma)
     Mộc Yêu       boss-def-moc-yeu        (Identity: boss-moc-yeu)
     Sơn Thạch Vệ  boss-def-son-thach-ve   (Identity: boss-son-thach-ve)
     Kim Lôi Vương boss-def-kim-loi-vuong  (Identity: boss-kim-loi-vuong)
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
     (`PASSIVE_RULES.md` §1). `null` means the Passive has **no match-charging
      threshold**; `0` is never used as a "no threshold" sentinel.
      A null threshold is **not** a statement that the Passive is
      always-active: the activation trigger is defined by the Passive
      itself (`BOSS_RULES.md` §6.2), and Thủy Ma's trigger is Battle
      Start — a one-time trigger, not a permanent always-on state.
   - `resetBehavior` (string) — exactly one of `Default` | `Partial` |
     `Persistent`, the storage names for the documented Reset Behavior
     variants Default Reset / Partial Reset / No Reset — Persistent
     (`PASSIVE_RULES.md` §4). No other token exists. The documented
     default when a rule states no override is `Default`
     (`PASSIVE_RULES.md` §1). Internal enum mapping
     (`PassiveResetBehavior`): `Default` → `Default`, `Partial` →
     `Partial`, `Persistent` → `NoReset` — the JSON/storage names are
     the contract; internal representation maps to them, not vice
     versa. For a **Boss** Passive, the `Persistent` token additionally
     carries the once-per-battle firing eligibility contract defined in
     `PASSIVE_RULES.md` §4 and `BOSS_RULES.md` §6.2.4 — the
     definition-level declaration is what selects it, so the runtime
     reads it from this stored value rather than from any new member.
     That is a statement about the existing value's meaning: the token
     set, the mapping above, the stored shape, and the schema are
     unchanged, and no row, column, or migration is added.
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
   (all 5 — `BOSS_RULES.md` §6) may ever be provisioned; the
   5-Boss figure is the MVP scope target (`MVP_SCOPE.md` §1), not
   permission to create placeholder rows for undefined content. All five
   Bosses are content-defined (`BOSS_RULES.md` §6), and all five
   rows are provisioned via EF Core migrations: the initial three
   (`boss-def-hoa-long`, `boss-def-thuy-ma`, `boss-def-moc-yeu`) via
   `20260926151112_ProvisionBossDefinitions` (TASK-053), and the remaining two
   (`boss-def-son-thach-ve`, `boss-def-kim-loi-vuong`) via
   `20261004094100_ProvisionTwoRemainingMvpBosses`. The
   `BossDefinitionId` of item 2 adds no seed of its own: no `HasData`,
   seed, or startup loader exists for this model, and the only
   provisioning path is the migration INSERT decided below.
   - **Mechanism — decided.** (TASK-052) The rows are provisioned
     by **EF Core migrations that INSERT them** into `BossDefinition`,
     applied through the project's existing `dotnet ef database update`
     workflow. `HasData`, a seed, a startup loader/upsert, a separate
     manual-SQL deployment path, and any runtime provisioning
     infrastructure are **not** used, and no fourth mechanism may be
     introduced by any other task. The decision is scoped to
     `BossDefinition`: no other content table's provisioning
     (`PetDefinition`, `CardDefinition`, `RelicDefinition`, …) is decided
     here, and each remains open (`§5` item 4).
   - **Row set — all five content-defined rows are provisioned.**
     The five provisioned rows are the canonical `BossDefinitionId`
     values of item 2 (`boss-def-hoa-long`, `boss-def-thuy-ma`,
     `boss-def-moc-yeu`, `boss-def-son-thach-ve`, `boss-def-kim-loi-vuong`),
     each with its `Identity` from `BOSS_RULES.md` §6.4 (`boss-hoa-long`,
     `boss-thuy-ma`, `boss-moc-yeu`, `boss-son-thach-ve`, `boss-kim-loi-vuong`).
     No placeholder rows (first paragraph). The initial three were provisioned
     by `20260926151112_ProvisionBossDefinitions` (TASK-053), and the remaining
     two were provisioned by `20261004094100_ProvisionTwoRemainingMvpBosses`.
   - **Row content — transcribed, never invented.** Each row's five
     columns are sourced per column, with no value computed or invented
     at provisioning time: `BossDefinitionId` from item 2; `Identity`
     from `BOSS_RULES.md` §6.4; `Element` from `BOSS_RULES.md` §6;
     `PassiveDefinition` per item 3's member list with `passiveId` and
     `threshold` from `BOSS_RULES.md` §6.2/§6.4 (including `null` for
      Thủy Ma's non-match-charged Passive — `§3`: `threshold = null`
      means no match-charging threshold, **not** always-active; the
      trigger is Battle Start, `BOSS_RULES.md` §6.2 — and the same `null`
      for Sơn Thạch Vệ's and Kim Lôi Vương's non-match-charged Passives,
      whose triggers are `Boss HP ≤ 50%` and `Player Combo ≥ 4`,
      `BOSS_RULES.md` §6.2) and `resetBehavior` from `PASSIVE_RULES.md` §4 (the documented
     default when a rule states no override is `Default`; Sơn Thạch Vệ's
     Passive is the one documented override — the non-default
     **No reset / persistent** form, `BOSS_RULES.md` §6.2.4,
     `PASSIVE_RULES.md` §4 item 3 — whose storage token is the existing
     `Persistent`, no new token being introduced);
     `SkillDefinition` per item 4's
     member list with `skillId`, `baseDamage`, `chargeRequirement`, and
     `cooldownTurns` from `BOSS_RULES.md` §6.3/§6.4. The authoritative
     documents own the values and remain the only source for them —
     nothing is duplicated here as a second source. Combat stats
     (`BOSS_RULES.md` §6.1) and display names are **not** columns
     (item 1) and are never written by provisioning; the storage
     encoding of `Element` is an implementation detail (header, `§5`
     item 1).
   - **Idempotency and uniqueness.** The migrations insert each row at
     most once and are tracked in EF's migration history, so re-running
     `dotnet ef database update` inserts nothing further; the end state
     is identical whether the migrations ran once or were retried —
     all five rows with identical content (first paragraph).
     `§3`'s `BossDefinitionId` primary key and `Identity NOT NULL,
     UNIQUE` are the database-level guarantee against duplicates.
     Provisioning never updates, overwrites, or deletes an existing row.
   - **Availability guarantee (ordering, every battle-capable
     environment).** (TASK-052) The migrations are applied in **every
     battle-capable environment** — any environment in which a battle can
     be started and ended — and within each such environment are
     applied **before that environment's first battle-end write**: all
     five rows exist before any `BattleResult` insert can attempt the
     FK. Until the migrations have been applied in an environment, no
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
3. **`BattleResult` persistence requires NO Player combat-readiness condition.**
   (TASK-056 human decision) The prerequisites for writing a `BattleResult` row
   are exactly the ones documented in this section — item 1 (one row per
   battle), item 2 (identity sourcing from authoritative battle state), the
   unresolvable-`BossDefinition` fail-closed rule above, and the duration/
   completion/outcome sourcing and `RewardSummary` contract documented
   below. **There is no separate Player combat-readiness prerequisite**, and no
   `IsCombatReady` predicate is part of the `BattleResult` persistence contract.
   The battle-end path must not consult, require, or evaluate any Player-side
   eligibility condition before writing the durable result:
   - **Not a condition of the write, in either direction.** An account being
     "ready" or "not ready" is not an input: the battle-end write neither
     requires a positive readiness value nor fails closed on a negative one.
     The only fail-closed condition on this path is the unresolved
     `BossDefinition` above, which is unchanged and independent of this item.
   - **No Player-side predicate may be substituted for it.** Authenticated
     session validity, battle ownership, owning a Pet, owning Cards or Relics,
     a valid loadout, or any Player Level threshold are governed by their own
     contracts — authentication and result-read authorization by §1's global
     session rule and `API_CONTRACTS.md` §2.8/§4 note 7, loadout validity by
     `API_CONTRACTS.md` §3 at **battle start** — and none of them is a
     battle-end persistence condition. Conflating any of them with
     `BattleResult` persistence would add a prerequisite this contract does not
     define.
   - **The Player entity gains no column and no state member.** `Player`
     gains nothing from this item: its documented columns are the ones in
     the entity block above (`PlayerId`, `AccountId`, `XP`, `Level`,
     `CreatedAt`), unchanged by TASK-056 (`ADR-011`, `ADR-012`,
     `ADR-016`); `BattleState` gains no lifecycle or readiness
     member (`GAME_STATE.md` §2.0.3).
   - **This item resolves a code↔contract mismatch, not a design change.**
     The authoritative documents never defined a readiness concept; an
     implementation of the battle-end path (TASK-041) introduced one together
     with a citation to this section that this section never contained.
     TASK-056 recorded the human decision that the implementation, not this
     contract, was wrong; removing that unsupported mechanism is the subject of
     a separate implementation reconciliation task and is not a change to any
     rule recorded here.
3. **`RewardSummary`'s member list is owned by THIS document.** (TASK-059;
   reassigned from TASK-033 — see "Reward semantics" below for the complete
   contract.) The landed contract is the **8-member JSON object** — a value
   that is always present, never absent, for both `Outcome`s, projecting
   Player-track and Pet-track progression post-grant.

**Reward semantics for `RewardSummary`.** (TASK-059, TASK-067)

`RewardSummary` is the persisted record of what a battle awarded. Its shape
projects both the **Player track** and the **Pet track**:

1. **Player track — decided, and therefore contracted here.** The Player
   reward is owned by `COMBAT_RULES.md` §7:

   ```text
   BattleWon   →  Player XP +100
   BattleLost  →  Player XP +0
   ```

   `Player.XP` and `Player.Level` are persisted columns (`§1`, `§3`). The
   Player-track members of `RewardSummary` are:

   ```text
   playerXpGained     (int)  — Player XP granted by this battle:
                               100 on a win, 0 on a loss
   newPlayerXp        (int)  — Player.XP after applying the grant
   playerLeveledUp    (bool) — whether Player.Level changed
   newPlayerLevel     (int)  — Player.Level after applying the grant
   ```

   These four members are the complete Player-track contract and are
   sourced from the authoritative Player XP rule, not invented here.

2. **Pet track — contracted per TASK-067.** The Pet XP reward contract is
   finalized in `PET_RULES.md` §5.3–§5.5:

   ```text
   PET_RULES.md §5.3   the active combat Pet is the sole recipient of
                       battle Pet XP (+100 on BattleWon, +0 on BattleLost,
                       and +0 for every inactive owned Pet)
   PET_RULES.md §5.4   Pet.Level = min(floor(Pet.XP / 100) + 1, 50)
   PET_RULES.md §5.5   Pet.XP is hard-capped at 4900 (no overflow)
   ```

   The four Pet-track members landed in TASK-067 are:

   ```text
   petXpGained        (int)  — Pet XP granted to active combat Pet:
                               100 on a win, 0 on a loss
   newPetXp           (int)  — active Pet.XP after applying the grant
   petLeveledUp       (bool) — whether active Pet.Level changed
   newPetLevel        (int)  — active Pet.Level after applying the grant
   ```

   Together with the Player track, these form the canonical 8-member
   `RewardSummary` contract.

3. **No placeholder members.** A field must not be added to `RewardSummary`
   merely to "complete" the schema — `AGENTS.md` §7, `AGENTS.md` §9. No item,
   currency, streak, bonus, or curve value appears: none is documented
   (`PET_RULES.md` §5.3 item 5, `AGENTS.md` §7).

4. **The 8-member projection is the contract in force.** The landed
   `RewardSummary` JSON document contains all 8 members across both the
   Player and Pet tracks. `BattleResult.RewardSummary` is `JSON`, and the
   `GET /api/battle/{battleId}/result` `rewards` member (`API_CONTRACTS.md`
   §4 note 1) returns this object document directly.

5. **Outcome semantics.** `RewardSummary` is present for both
   `Outcome` values (§1 entity block, `API_CONTRACTS.md` §4 note 1). The
   8-member set applies identically to both outcomes (TASK-068 Option A):

   ```text
   BattleWon   → playerXpGained = 100, petXpGained = 100
   BattleLost  → playerXpGained = 0,   petXpGained = 0
   ```

   On a `"defeat"` the members are serialized with `playerXpGained = 0`,
   `petXpGained = 0`, `playerLeveledUp = false`, `petLeveledUp = false`, and
   the respective `newPlayerXp` / `newPlayerLevel` and `newPetXp` /
   `newPetLevel` equal to their unchanged pre-battle values, matching the
   `+0` Player XP grant (`COMBAT_RULES.md` §7.2) and `+0` Pet XP grant to the
   active combat Pet (`PET_RULES.md` §5.3 item 2).

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

**MVP starter ownership contract (TASK-084 / TASK-082 decision E / R1-4;
composition amended by the TASK-213 decision, implemented by TASK-221).**
For MVP, every newly created Player receives a deterministic ownership grant that
**is the MVP content set** (`MVP_SCOPE.md` §1 "Content ownership & reachability"):
MVP has no acquisition system, so the battle-start flow
(`POST /api/battle/start`, `API_CONTRACTS.md` §3) is exercised with the whole
provisioned content set, and no path adds content to an account afterwards.

**1. Exact starter composition and canonical identities:**
- **Starter Pets (5 owned `Pet` rows):** exactly one Pet instance per MVP Pet
  definition — `pet-xich-lang` (**Xích Lang**, Element Hỏa), `pet-bach-ho`
  (**Bạch Hổ**, Kim), `pet-huyen-quy` (**Huyền Quy**, Thủy), `pet-thanh-xa`
  (**Thanh Xà**, Mộc), and `pet-son-hung` (**Sơn Hùng**, Thổ)
  (`PET_RULES.md` §8). Each instance carries the documented creation values
  (`DATABASE.md` §3): `Tier = Common` (the MVP data default), `Star = 1`
  (`Pet.MinStar`), `XP = 0` (`Pet.InitialXp`), `Level = 1` (`Pet.InitialLevel`,
  `PET_RULES.md` §5.2), `AcquiredAt` server timestamp. *No Pet is privileged:*
  which one is the active Pet is a battle-loadout choice
  (`PET_RULES.md` §2.1 item 1), not an ownership property.
- **Starter Basic Cards (3 `PlayerUnlockedCard` rows):** all three content-defined
  Basic Cards (`CARD_RULES.md` §2) referencing canonical definition IDs:
  `card-heal` (**Heal**), `card-shield` (**Shield**), and `card-power-charge`
  (**Power Charge**), each with `Category = Basic` and `LoadoutCopyLimit = 1`.
  *Excluded:* Pet Skill Cards (`Category = PetSkill` — `card-inferno`,
  `card-tidal-barrier`, `card-iron-fang`, `card-venomous-bloom`,
  `card-earthshaker`) are derived from the active Pet's `SignatureSkillCardId` at
  battle start and are never granted as unlocked Basic Cards
  (`CARD_RULES.md` §1 item 4). The Card slot is fixed by **content**, not by
  ownership: the loadout is exactly 3 Basic Cards and exactly 3 exist.
- **Starter Relics (10 owned `Relic` rows):** one owned instance per MVP Relic
  definition (`RELIC_RULES.md` §6) — `relic-berserker-core` (**Berserker Core**),
  `relic-mana-crystal` (**Mana Crystal**), `relic-assassin-eye` (**Assassin
  Eye**), `relic-emergency-core` (**Emergency Core**), `relic-burning-curse`
  (**Burning Curse**), `relic-combo-fang` (**Combo Fang**), `relic-arcane-battery`
  (**Arcane Battery**), `relic-execution-mark` (**Execution Mark**),
  `relic-cascade-core` (**Cascade Core**), and `relic-battle-instinct`
  (**Battle Instinct**). Exactly one owned instance is granted per definition;
  each row carries a distinct, server-minted `RelicInstanceId` (never collapsed
  with `RelicDefinitionId`, `RELIC_RULES.md` §2.2) and an `AcquiredAt` server
  timestamp. Owning all ten is what makes the 3–5 Relic loadout a real build
  decision (`RELIC_RULES.md` §2.1 item 1); nothing is equipped by the grant.
  *Selection basis:* The grant references the provisioned content set — it is
  NOT derived from document ordering, alphabetical ordering, migration ordering,
  or database ordering, and `RELIC_RULES.md` §2.3 item 2 forbids ordering a
  loadout by `AcquiredAt`.

**2. Semantic classification — MVP bootstrap, not acquisition gameplay:**
These rows represent **MVP bootstrap / test content** for newly created Players.
They exist solely so a newly created Player has the owned content the
`POST /api/battle/start` loadout validation rules require (`API_CONTRACTS.md`
§3: 1 Pet, 3 Basic Cards, 3–5 Relics). They do **not** define or constrain future
gameplay acquisition systems (starter choice UX, tutorial rewards, quests, gacha,
shops, drops, events, or progression). Future tasks may alter the starter flow
without altering the underlying collection ownership model (`DATABASE.md` §1–§2)
— and, per `MVP_SCOPE.md` §3, adding content to an account after creation
requires a Rule Change (`GAME_RULES.md` §20) before implementation.

**3. Player creation semantics and idempotency:**
- **New Player creation:** Starter ownership rows (5 `Pet`, 3 `PlayerUnlockedCard`,
  10 `Relic`) are created and committed atomically with the `Player` row on the
  Player-creation branch (`POST /api/auth/register` / `/api/auth/login`,
  `ARCHITECTURE.md` §2.3 item 3).
- **Existing Player:** Authenticating an existing Player performs no starter grant;
  the starter initialization path is reachable only when a new `Player` row is
  inserted.
- **Not a repair / top-up mechanism:** The initialization is strictly bound to
  Player creation. The system must NOT evaluate conditional top-ups (e.g. "if
  Player has no Pet / fewer than 3 Cards / no Relics → grant").

**4. Persistence atomicity assessment and concurrency prerequisite (TASK-083):**
- **Atomicity:** Achievable on the existing persistence boundary. All four entity
  types (`Player`, `Pet`, `PlayerUnlockedCard`, `Relic`) share the single scoped
  `GameDbContext` (`src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs`).
  A single `SaveChangesAsync` commits the Player and all 18 starter ownership rows
  in one atomic database transaction. No new persistence abstraction (`IUnitOfWork`,
  `StarterOwnershipManager`, outbox, domain events) is required.
- **Commit-scope surface:** Because existing repository methods (`PlayerRepository`,
  `PetRepository`, etc.) each commit independently, TASK-083 must expose exactly
  one combined commit-scope surface on the existing boundary.
- **Concurrency race-safety:** On concurrent registration for the same username,
  the `Username` UNIQUE constraint on `Accounts` rolls back the losing batch.
  The error-handling catch (`PlayerRepository.cs:69-85`) must discard/detach the
  **entire staged batch** (Player + 18 ownership entities), not only the `Player`
  entity, ensuring no orphaned ownership entities remain tracked. TASK-083 must
  verify this concurrent first-login behavior in integration tests.

---

# 3. Constraints

```text
Player.XP             int, NOT NULL, default 0                       (COMBAT_RULES.md §7)
Player.XP             >= 0   (no UPPER bound — XP is uncapped and       (COMBAT_RULES.md §7.5 item 1)
                              only Level is capped at 50)
Player.XP             = 0 for a newly created Player                 (COMBAT_RULES.md §7.5 item 3)
Player.Level        ∈ [1, 50]                                      (COMBAT_RULES.md §7)
Player.Level        = min(floor(Player.XP / 100) + 1, 50)          (COMBAT_RULES.md §7.4)
Player.Level        = 1 for a newly created Player                 (COMBAT_RULES.md §7.5 item 3)
Pet.XP                int, NOT NULL, default 0                       (§1; PER-INSTANCE —
                                                                      PET_RULES.md §5.1)
Pet.XP                ∈ [0, 4900]   (HARD cap — Pet XP stops at 4900    (PET_RULES.md §5.5)
                                     and no overflow is retained)
Pet.XP                = 0 for a newly created PlayerPet               (PET_RULES.md §5.2)
Pet.Tier          ∈ {Common, Rare, Epic, Legendary, Mythic}      (PET_RULES.md §3)
Pet.Star           ∈ [1, 5]                                       (PET_RULES.md §4)
Pet.Level             derived from this Pet instance's own Pet.XP    (PET_RULES.md §5.4)
Pet.Level          ∈ [1, 50]                                       (PET_RULES.md §5.5)
Pet.Level             = min(floor(Pet.XP / 100) + 1, 50)           (PET_RULES.md §5.4)
Pet.Level             = 1 for a newly created PlayerPet             (PET_RULES.md §5.2)
CardDefinition.Category  ∈ {Basic, PetSkill}                        (CARD_RULES.md §1)
CardDefinition.EffectDefinition     jsonb, NOT NULL — an ARRAY of effect       (§1 "Card EffectDefinition
  objects, one per effect                contract"; TASK-108 D-1/D-2,
                                          TASK-109; ARRAY shape + vocabulary
                                          extended by TASK-111 D-1/D-2/D-3;
                                          supersedes TASK-082 R2-7 for THIS
                                          member only)
CardDefinition.EffectDefinition.effectType ∈ {Heal, Shield, Power, Damage,   (§1; CARD_RULES.md §2 for the
  Burn, Crit}                            first three, §4.1 for the last three
                                          — D-2 named all six; the §4.1 Pet
                                          Skill set is since five Cards, which
                                          adds no member — `Damage` is
                                          distinct from `Power`, which
                                          denotes Power Charge — D-4)
CardDefinition.EffectDefinition.valueType ∈ {Flat, PercentMaxHp,              (§1; §2's two interpretations,
  PercentagePoints, Undetermined}        plus §4.1's Crit percentage-point
                                          unit — D-3; plus the
                                          unauthored-magnitude marker —
                                          §1 item 9)
CardDefinition.EffectDefinition.value  int, > 0, present iff                 (§1; the effect magnitude,
  valueType interprets one                valueType ≠ Undetermined    transcribed from CARD_RULES.md
                                          §2/§4.1 through valueType — NOT
                                          the Card's Cost; absent, never 0,
                                          when Undetermined)
CardDefinition.EffectDefinition.duration  int, > 0, present iff             (§1 item 1; ONLY on a `Burn`
  effectType = Burn                       element — the effect's duration in
                                          Turns; `value` is damage per tick;
                                          the tick schedule and unit are
                                          owned by GAME_RULES.md §17 step 19a
                                          and COMBAT_RULES.md §5.2 — D-3)
CardDefinition.EffectDefinition.scope  string = "NextAttack", present iff    (§1 item 1; ONLY on a `Crit`
  effectType = Crit                       element — which damage instances the
                                          increase applies to; the rule is
                                          owned by CARD_RULES.md §4.1 — D-3)
CardDefinition.EffectDefinition     REJECTED, never defaulted, when  (§1 item 6; no fallback
  not a well-formed structured rule   the stored value is malformed   magnitude, no prose fallback,
                                                                      no silent no-op, and no
                                                                      missing required extra
                                                                      member (`duration` on Burn,
                                                                      `scope` on Crit))
CardDefinition.EffectDefinition     element ORDER is NOT semantic       (§1 item 3 — D-5; a storage
  ordering                                                             statement only; authors no
                                                                       resolution step or priority)
RelicDefinition.Condition           jsonb, NULL — a structured condition   (§1 "Relic EffectDefinition
  object (`conditionType` +           and Condition" contract" items 3–4,
  `threshold`), or NULL               7; RELIC_RULES.md §8.1; TASK-131
                                      D5/D9; TASK-132. NULL is the
                                      documented "no extra condition"
                                      (§8.1 item 4), never a sentinel)
RelicDefinition.Condition.conditionType ∈ {MatchCountAtLeast,              (§1 item 7; the §8.1
  ComboAtLeast, HpPercentageBelow}       grammar — the closed form set)
RelicDefinition.Condition.threshold  int, > 0, always present             (§1 item 7; §8.1 item 1 —
                                                                          the threshold is part of the
                                                                          value, never prose; never
                                                                          defaulted)
RelicDefinition.EffectDefinition    jsonb, NOT NULL — an ARRAY of      (§1 "Relic EffectDefinition
  effect objects, one per effect      and Condition" contract" items 4,
                                      7; TASK-131 D1/D2/D3, TASK-132;
                                      supersedes TASK-082 R2-7 for this
                                      member)
RelicDefinition.EffectDefinition.effectType ∈ {ATK, Power, Crit,          (§1 item 7; RELIC_RULES.md
  CardCost}                             §8.2 item 1 — the closed
                                        Relic identity set, which is
                                        NOT the Card set)
RelicDefinition.EffectDefinition.valueType ∈ {Flat, Percentage,           (§1 item 7; §8.2 item 2 —
  PercentagePoints, Undetermined}       `Percentage` is not the Card
                                        set's `PercentMaxHp`)
RelicDefinition.EffectDefinition.value  int, > 0, present iff              (§1 item 7; §8.2 item 3 —
  valueType interprets one                valueType ≠ Undetermined         transcribed from §8.5
                                                                          through valueType; absent,
                                                                          never 0, when Undetermined)
RelicDefinition.EffectDefinition.target ∈ {Pet}                            (§1 item 7; §8.3 item 1)
RelicDefinition.EffectDefinition.lifetime ∈ {Immediate, Battle,            (§1 item 7; §8.3 item 2 /
  NextAttack}                             ADR-018 item 3; the allowed
                                          value is fixed per effectType
                                          by §8.3's table)
RelicDefinition.EffectDefinition     REJECTED, never defaulted, when        (§1 item 6; no fallback
  not a well-formed structured rule or  the stored value is malformed     magnitude, no default
  condition                                                              threshold, no prose
                                                                          fallback, no silent no-op,
                                                                          and no Card-only
                                                                          `duration`/`scope` member —
                                                                          §8.3 item 5)
RelicDefinition.EffectDefinition     element ORDER is NOT semantic          (§1 item 7; §8.2 item 4 — a
  ordering                                                               storage statement only; a
                                                                          Relic's resolution order is
                                                                          §4's equip-slot order)
RelicDefinition.Trigger              unchanged — a §3 identity string,      (§1; RELIC_RULES.md §8.5
  never NULL                            NOT restructured by §8            item 3 / TASK-131 D8 — §3's
                                                                          closed list is unchanged)
BossDefinition.BossDefinitionId     NOT NULL, UNIQUE, caller/content-supplied (independent persistence key, never
                                                                      database-generated; distinct from `Identity` and
                                                                      from the display name — §1 note item 2, TASK-049)
BossDefinition.PassiveDefinition  NOT NULL                           (every Boss has one Passive — BOSS_RULES.md §1)
BossDefinition.SkillDefinition    NOT NULL                           (every Boss has one Skill — BOSS_RULES.md §1)
BossDefinition.Identity           NOT NULL, UNIQUE                   (canonical technical Boss ID — BOSS_RULES.md
                                                                      §6.4; the unique target of the FK lookup in §1)
BossDefinition.PassiveDefinition.resetBehavior ∈ {Default, Partial, Persistent}   (PASSIVE_RULES.md §4)
BossDefinition.PassiveDefinition.threshold = null ⇔ no match-charging threshold (NOT always-active;   (BOSS_RULES.md §6.2,
                                              the trigger is defined per Passive, not by null)          BOSS_RULES.md §6.2.1-§6.2.5)
Player.PlayerId (per battle) must own exactly one active Pet selection
  at battle start — enforced at the Application layer (ARCHITECTURE.md),
  not purely at the DB level, since it is a request-time rule
  (API_CONTRACTS.md §2), not a stored invariant.

Ownership model (ADR-011, ADR-012, ADR-016): Player FKs on Pet/Relic (and
PlayerUnlockedCard rows) are collection ownership only. There are no
combat-stat columns on Player — HP/ATK/DEF/Crit/Power are battle-time
`PetState` (GAME_STATE.md §2.3, REDIS_STATE.md). `Player.XP` and
`Player.Level` are persistent account progression values, not combat
stats. `Pet.XP` and `Pet.Level` are persistent per-instance progression
values owned by the Pet instance — they are not `PetDefinition` columns and
are not derived from any Player attribute (PET_RULES.md §5.1, ADR-016).
Equipped Relic/Card loadout is battle-scoped, selected at
POST /api/battle/start for the active Pet and snapshotted into PetState; it
is not stored as Player-owned or Pet-owned equip slots.
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
4. Provisioning mechanisms for static-content rows: for
   `BossDefinition` the mechanism **is** defined by this document
   (TASK-052): an EF Core migration that INSERTs the three
   content-defined rows **that were defined at that time**, applied through the existing
   `dotnet ef database update` workflow in every battle-capable
   environment before that environment's first `BattleResult` write —
   no `HasData`, no seed, no startup loader, no separate manual-SQL
   deployment path, no runtime provisioning (§1, `BossDefinition`
   persistence contract item 5). For `PetDefinition`, `CardDefinition`,
   and `RelicDefinition` the mechanism is **also now defined, on the
   same terms** (TASK-082 decision D): EF Core migration-INSERT data
   through the existing migration workflow — deterministically, from
   content defined by the owning domain documents; **no** startup seed
   runner, external content service, JSON content pipeline, or other
   new persistence mechanism. Two rules bind those migrations: (a) only
   **content-defined rows** may be inserted — a row whose required
   members have no documented value stays unprovisioned and deferred in
   its owning domain document (the rows still deferred are recorded in
   `RELIC_RULES.md` §6; **no Pet row remains deferred** as of TASK-167 —
   all five MVP Pets' Signature Skills are now content-defined in
   `CARD_RULES.md` §4.1, so the Thanh Xà and Sơn Hùng rows are merely
   **provisioned-later**, not content-blocked; and **no Boss row remains
   content-blocked** — all five MVP Bosses are content-defined in
   `BOSS_RULES.md` §6 and provisioned across migrations
   `20260926151112_ProvisionBossDefinitions` and
   `20261004094100_ProvisionTwoRemainingMvpBosses`); (b) the
   migration's row values (keys, names, and each table's own members —
   `LoadoutCopyLimit`, `EffectDefinition`, `Trigger`, per the `§1`
   entity blocks) are copied from the owning domain documents, never
   invented. The **implementation** (migration script,
   the exact row values, and the TASK-083 starter-ownership initialization per §2)
   is **complete** (TASK-085) — migration
   `20260929152651_ProvisionPetCardRelicContentDefinitions` provisions the
   `PetDefinition` (3), `CardDefinition` (6), and `RelicDefinition` (4)
   content-defined rows through the same `dotnet ef database update`
   workflow, and the TASK-083 starter-ownership initialization grants the
   §2 starter rows in a single scoped `GameDbContext.SaveChangesAsync`. Those
   six `CardDefinition` rows were the complete content-defined Card set at the
   time of that migration; **two further `CardDefinition` rows and two further
   `PetDefinition` rows are now provisioned** by a second, same-mechanism
   data-only migration (TASK-168: `card-venomous-bloom`, `card-earthshaker`,
   `pet-thanh-xa`, `pet-son-hung`, in migration
   `20261004055006_ProvisionThanhXaAndSonHungSignatureSkills`), so the
   currently provisioned content-defined set is **`PetDefinition` (5),
   `CardDefinition` (8), and `RelicDefinition` (10)**. The four `RelicDefinition`
   rows were subsequently **re-encoded into the
   structured shape** `RELIC_RULES.md` §8 defines (TASK-132, migration
   `20261003074309_StructureRelicDefinitionStructuredColumns`) — a
   representation migration over the same four rows, with every value
   transcribed from `RELIC_RULES.md` §8.5 and none computed or invented
   (rule (b) above). That migration provisions no row: it inserts none and
   deletes none. **The remaining six canonical `RelicDefinition` rows were
   subsequently provisioned** by a third, same-mechanism data-only migration
   (TASK-184: `relic-burning-curse`, `relic-combo-fang`,
   `relic-arcane-battery`, `relic-execution-mark`, `relic-cascade-core`, and
   `relic-battle-instinct`, in migration
   `20261004153916_ProvisionRemainingMvpRelicDefinitions`), each written born
   structured from `RELIC_RULES.md` §8.5. **No Relic row remains deferred** as
   of TASK-184: rule (a) above is satisfied for the complete canonical set,
   because TASK-176 content-defined all ten Relics and resolved the Trigger
   tension that had deferred `Burning Curse` (`RELIC_RULES.md` §6 note 3,
   §8.5 item 5). The `BossDefinition` migrations are
   **complete** — migration `20260926151112_ProvisionBossDefinitions`
   (TASK-053) provisioned the initial three canonical rows, and migration
   `20261004094100_ProvisionTwoRemainingMvpBosses` provisioned the final two
   MVP rows (`boss-def-son-thach-ve`, `boss-def-kim-loi-vuong`); all five
   canonical rows are provisioned and every `BattleResult` write's FK target
   exists wherever the migrations have been applied (TASK-051 decision A3).
