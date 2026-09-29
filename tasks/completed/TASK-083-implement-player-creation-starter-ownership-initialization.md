# TASK-083 — Implement Player Creation Starter Ownership Initialization

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  This task implements the mechanism TASK-082 Decision E deliberately deferred.
  It does NOT re-decide the starter composition (settled: 1 Pet / 3 Basic
  Cards / 3–5 Relics), does NOT author content values (TASK-082 settled every
  provisioned row), and does NOT redesign the definition-provisioning
  mechanism (TASK-082 Decision D: EF Core migration-INSERT).

  Authority chain:
    TASK-082 Decision E / R1-4  → composition settled; mechanism + exact IDs
                                   deferred to this follow-up task
    DATABASE.md §2              → "MVP starter ownership — composition decided;
                                   mechanism and IDs deferred"
    DATABASE.md §5 item 4       → definition provisioning defined (TASK-082 D);
                                   "the §2 starter-ownership mechanism + exact
                                   starter IDs remains an implementation detail"
    AGENTS.md §7 / §20          → no invented content, identifier, or rule
-->

---

## Metadata

```text
Task ID:           TASK-083
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (P1 — closes TASK-082 Decision E; the last documented
                   prerequisite before a real `POST /api/battle/start` loadout
                   can pass §3 ownership validation)
Primary Agent:     backend (Application use case: starter-grant orchestration
                   on the Player-creation path; ARCHITECTURE.md §2.1)
Supporting Agents: persistence (EF Core write path + migration boundary),
                   testing (integration + guard tests),
                   review
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   backend/persistence-analysis,
                   quality/architecture-conformance,
                   testing/test-scenario-generation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-082 (DONE — Decision A/B/C/E contract recorded in
                     docs/; Decision D definition-provisioning mechanism
                     defined; NOT modified),
                   TASK-053 (DONE — EF Core migration-INSERT precedent),
                   TASK-023 (DONE — the Player match-or-create path this task
                     extends),
                   TASK-028 / TASK-027 (DONE — the ownership tables and their
                     existing insert operations),
                   TASK-035 / TASK-054 / TASK-055, TASK-034 (DONE — the
                     authentication flow that creates the Player; context only)
Depends-on-the-content-provisioning-implementation-task:
                   RESOLVED — TASK-085 (DONE) provisioned the PetDefinition /
                   CardDefinition / RelicDefinition rows this task references
                   (`20260929152651_ProvisionPetCardRelicContentDefinitions`).
                   The FK targets exist; this task provisions nothing.
Blocks:            TASK-078 (BACKLOG — client LobbyScene match-start trigger;
                     NOT modified by this task). TASK-078 additionally remains
                     blocked by D2 and D4 — see §"Dependencies".
                   TASK-079 (BLOCKED — NOT modified, NOT reopened)
```

**Type classification note.** `FEATURE`, not `DOCUMENTATION` / `ARCHITECTURE`.
The mechanism this task implements is fully determined by existing authoritative
documents: `DATABASE.md` §2 records the composition (TASK-082 Decision E), the
Player-creation boundary already exists and is documented
(`API_CONTRACTS.md` §2, `ARCHITECTURE.md` §2.3 item 3, TASK-023), the three
ownership tables and their insert operations already exist (TASK-023, TASK-027,
TASK-028), and the idempotency boundary already exists (the Player row's own
creation step). The work is therefore "build what `docs/` already defines" —
`tasks/TASK_TYPES.md` §2's FEATURE definition. No new architecture, endpoint,
schema, or gameplay rule is required.

**Status note.** `READY`. Per `tasks/TASK_LIFECYCLE.md` §3, the task reached
`READY` when every BACKLOG → READY criterion passed (`tasks/TASK_LIFECYCLE.md`
§3) together with this task's own two implementation prerequisites: the
definition rows now exist (TASK-085, DONE — see the dependency entry above), and
the atomicity/concurrency assessment recorded by TASK-084 (`DATABASE.md` §2
item 4) confirms both are achievable on the existing persistence boundary
without a new architectural abstraction.

---

## Objective

Implement, server-side and deterministically, the **MVP starter ownership
initialization** that TASK-082 Decision E decided but deliberately deferred:
when a Player is newly created on the documented authentication path
(`POST /api/auth/discord`, `API_CONTRACTS.md` §2), that Player receives exactly
one starter ownership set — **1 MVP Pet instance, 3 Basic Card unlock rows, and
3 MVP Relic instances** — drawn only from the authoritative provisioned content,
so that the documented `POST /api/battle/start` loadout validation
(`API_CONTRACTS.md` §3) can pass for a real Player. The initialization must run
exactly once per Player, must be server-authoritative, must introduce no new
endpoint, schema column, seeder, or acquisition rule, and must not alter any
existing contract.

---

## Authoritative References

- `docs/02-technical/DATABASE.md` §2 — **the owner of this task's contract**:
  "MVP starter ownership — composition decided; mechanism and IDs deferred.
  … **1 Pet, 3 Cards, and 3–5 Relics** … drawn only from content-defined
  provisioned rows (§5 item 4) … The exact starter item IDs … and the
  **creation-time mechanism** for how a Player's ownership rows first come to
  exist are recorded by the follow-up provisioning implementation task".
- `docs/02-technical/DATABASE.md` §1 — the three ownership entity definitions
  (`Pet`, `PlayerUnlockedCard`, `Relic`) and their per-column constraints, and
  the `Player` entity definition (the creation boundary).
- `docs/02-technical/DATABASE.md` §3 — the documented constraints the starter
  rows must satisfy: `Pet.XP = 0` / `Pet.Level = 1` / `Pet.XP ∈ [0, 4900]` /
  `Pet.Level ∈ [1, 50]` for a newly created PlayerPet (`PET_RULES.md` §5.2),
  `Pet.Tier ∈ {Common, Rare, Epic, Legendary, Mythic}`, `Pet.Star ∈ [1, 5]`, and
  the ADR-011/ADR-012/ADR-016 ownership model.
- `docs/02-technical/DATABASE.md` §5 item 4 — the definition-provisioning
  mechanism (TASK-082 Decision D, EF Core migration-INSERT) and the explicit
  statement that the §2 starter-ownership mechanism and exact starter IDs remain
  the follow-up implementation task's deliverable. **Not reopened here.**
- `docs/02-technical/API_CONTRACTS.md` §2 — the authentication boundary that
  creates the Player (`§2.5` response shape `{ sessionToken, playerId }`;
  `§2.8` the session). **Unchanged by this task.**
- `docs/02-technical/API_CONTRACTS.md` §3 — the loadout validation the starter
  ownership must be sufficient for: `petId` owned; `cardLoadout` exactly 3
  Basic Cards, each backed by a `PlayerUnlockedCard` row; `relicLoadout` 3–5
  owned, distinct `RelicInstanceId` values. **Unchanged by this task.**
- `docs/02-technical/API_CONTRACTS.md` §5.1/§5.3/§5.4/§5.5 — the collection
  reads through which the client observes the resulting ownership (the client's
  only role in this task). **Unchanged by this task.**
- `docs/02-technical/ARCHITECTURE.md` §2.1 — layer direction
  (Domain ◀ Application ◀ Infrastructure ◀ Api) and "Application orchestrates;
  it does not contain game-rule logic".
- `docs/02-technical/ARCHITECTURE.md` §2.3 item 3, §3 — the backend
  "identifies/**creates** the player" on the auth boundary; the
  `PersistenceRepository (Postgres)` component owns the `DATABASE.md` data.
- `docs/02-technical/ARCHITECTURE.md` §5 — anti-overengineering: no new
  abstraction, service, registry, or read model.
- `docs/02-technical/TDD.md` §2.1 — the Discord auth exchange / session
  sequence; §4 item 3 — PostgreSQL is not on the battle hot path (this task
  writes only on the Player-creation path).
- `docs/02-technical/GAME_STATE.md` §2.3 — `PetState.PetId` is the owned Pet
  **instance** identity, not a definition id; relevant to what the starter Pet
  instance must be resolvable as.
- `docs/00-overview/MVP_SCOPE.md` §1 (Player account "collection owner", 5 Pets,
  3 Basic Cards, ~10 Relics) and §2 (Gacha / Trading excluded — no acquisition
  system is introduced).
- `docs/01-game-design/PET_RULES.md` §2 (collection ownership; exactly one
  active Pet per battle), §5.2 (new PlayerPet initial values `XP = 0`,
  `Level = 1`), §8 (the 3 provisioned / 2 deferred Pet rows).
- `docs/01-game-design/CARD_RULES.md` §1 (the Player owns the Card collection as
  `PlayerUnlockedCard` unlock rows; the submitted Basic loadout is exactly 3
  Basic Cards; `LoadoutCopyLimit` = 1 for every defined Card), §2 (the 3 Basic
  Cards: Heal, Shield, Power Charge).
- `docs/01-game-design/RELIC_RULES.md` §2 / §2.1 (Player owns Relic *instances*;
  `relicLoadout` selects 3–5 **owned instances** and the count bound is stated
  there), §2.2 (the element is the owned instance identity), §6 note 3 (the 4
  provisioned / 1 deferred Relic rows).
- `docs/01-game-design/PASSIVE_RULES.md` §8 — the Pet `PassiveId` values, sourced
  into `PetDefinition` (not into the ownership row).
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — Player = owner /
  Pet = combat character; ownership vs battle-scoped equip.
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` — items 7,
  9, 10: Relic instance ownership, the `PlayerUnlockedCard` unlock table, no
  persistent equip table and no `Pet.CardInventory`.
- `docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md` —
  `Pet.XP`/`Pet.Level` are the Pet instance's own progression values.
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; the client never authors ownership.

---

## Current State

The Player-creation path exists and is complete for its own contract, but
nothing initializes ownership.

```text
POST /api/auth/discord                      API_CONTRACTS.md §2
        ↓
AuthController.AuthenticateDiscord          src/backend/GameServer.Api/Controllers/AuthController.cs
        ↓                                   (step 2 → step 3, no ownership step between)
IPlayerRepository
    .GetOrCreateByDiscordUserIdAsync(...)   src/backend/GameServer.Application/Players/IPlayerRepository.cs
        ↓
PlayerRepository                            src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs
    find by DiscordUserId → return unchanged
    else create (PlayerId = $"player_{Guid:N}", XP = 0, Level = 1, CreatedAt)
        ↓
ApplicationSessionTokenService.Issue(...)   session issued
```

Verified facts that shape this task:

```text
Ownership tables + insert operations already exist (no schema work needed):
  Pet                    → IPetRepository.AddAsync(Pet)                TASK-024
  PlayerUnlockedCard     → ICardRepository.AddUnlockAsync(...)         TASK-028
  Relic                  → IRelicRepository.AddAsync(Relic)            TASK-027
  (each AddAsync/AddUnlockAsync calls GameDbContext.SaveChangesAsync itself —
   src/backend/GameServer.Infrastructure/Postgres/Repositories/*.cs)

Definition rows are provisioned (TASK-085, DONE):
  BossDefinition          3 rows provisioned   (migration 20260926151112, TASK-053)
  PetDefinition           3 rows provisioned   (migration 20260929152651, TASK-085)
  CardDefinition          6 rows provisioned   (migration 20260929152651, TASK-085)
  RelicDefinition         4 rows provisioned   (migration 20260929152651, TASK-085)
  ⇒ Pet.PetDefinitionId / Relic.RelicDefinitionId / PlayerUnlockedCard.CardDefinitionId
    are FKs (OnDelete Restrict) whose target rows now exist. This task
    provisions no definition row and authors no migration.

Consequence today (TASK-071 §5.5 semantics):
  GET /api/pets → 200 []
  GET /api/cards → 200 []
  GET /api/relics → 200 []
  ⇒ the ownership rows are absent, so no real POST /api/battle/start loadout
    can pass API_CONTRACTS.md §3 until this task creates them.
```

The starter set this task must create is bounded by the three ownership rows'
own documented member sets — no additional value has to be chosen:

```text
Pet                member set: PetInstanceId, PlayerId, PetDefinitionId,
                   Tier, Star, XP, Level, AcquiredAt        (DATABASE.md §1, §3)
                   every member's creation value is documented (see §"Exact
                   Starter Composition" below)

PlayerUnlockedCard member set: PlayerId, CardDefinitionId  (DATABASE.md §1)
                   no third column exists and none may be added

Relic              member set: RelicInstanceId, PlayerId, RelicDefinitionId,
                   AcquiredAt                               (DATABASE.md §1)
```

---

## Exact Starter Composition

Settled by TASK-082 Decision E / R1-4 and recorded in `DATABASE.md` §2. This
task implements it verbatim; it does not re-decide it.

```text
Starter ownership, one per newly created Player:
    1 MVP Pet instance
    3 Basic Card unlock rows
    3 MVP Relic instances
```

**Card slot — "3 Basic Cards" is the loadout rule, not a free choice.**
`CARD_RULES.md` §1 fixes the submitted Basic loadout at exactly 3 Basic Cards and
`API_CONTRACTS.md` §3 step 2 requires every submitted `CardDefinitionId` to have a
`PlayerUnlockedCard` row. The three Basic Cards are therefore *all three*
content-defined Basic Cards, and no other category may be substituted:

```text
Heal            card-heal            Category = Basic    CARD_RULES.md §2
Shield          card-shield          Category = Basic    CARD_RULES.md §2
Power Charge    card-power-charge    Category = Basic    CARD_RULES.md §2
```

**Explicitly NOT granted as part of the three Basic Cards** (`AGENTS.md` §7 —
they are `Category = PetSkill`, and `CARD_RULES.md` §1 item 4 states a `PetSkill`
Card "can never satisfy this section's Basic-Card composition rule" and is
derived from the active Pet, never submitted):

```text
card-inferno        (Inferno — Xích Lang Signature Skill)
card-tidal-barrier  (Tidal Barrier — Huyền Quy Signature Skill)
card-iron-fang      (Iron Fang — Bạch Hổ Signature Skill)
```

**Relic slot — 3 instances, not 4 and not 5.** `RELIC_RULES.md` §2.1 and
`API_CONTRACTS.md` §3 bound `relicLoadout` to "3–5 elements", so the starter set
must satisfy the *lower* bound; four provisioned definitions exist
(`RELIC_RULES.md` §6 note 3, `Burning Curse` deferred), so a 5-instance starter
set would require a second instance of a definition and a duplicate-selection
question this task must not open. The single deterministic choice inside the
settled 3–5 range that needs no new rule, no duplicate instance, and no deferral
is therefore **one owned instance of each of the three Relic definitions named
first in the authoritative provisioned order** — i.e. the first three of the four
provisioned rows as `RELIC_RULES.md` §6 lists them. The Relic instance identity
member is free (`Relic.RelicInstanceId` is a PK with no format rule in any
document — `RELIC_RULES.md` §2.4's `relic-a`/`relic-b` strings are examples, not
contracts), so the implementation must mint it.

---

## Canonical IDs

The starter references are **the canonical technical identities TASK-082 fixed**
(`DATABASE.md` §1; `PET_RULES.md` §1/§8; `PASSIVE_RULES.md` §8;
`RELIC_RULES.md` §6 note 3; `CARD_RULES.md` §2).

### Content-definition identities (the FKs the ownership rows store)

**Authority warning — read this before using any literal below.**

TASK-082 fixed the identity **form** (`{kind}-{slug}`, slug = ASCII kebab-case of
the documented display name; `DATABASE.md` §1) and the **row sets**; TASK-085
(DONE) subsequently **provisioned** those rows
(`20260929152651_ProvisionPetCardRelicContentDefinitions`), and `DATABASE.md` §2
now records the exact starter identities as the TASK-084 contract. The literals
below are therefore stated here for orientation only — the **provisioned rows'
primary-key values are the binding source**.

```text
AUTHORITATIVE, in priority order:
  1. the provisioned PetDefinition / CardDefinition / RelicDefinition rows
     (their primary-key values)                ← the only binding source
  2. DATABASE.md §1   — the value FORM of each key
  3. the owning domain document + this document — which ROWS are in the set
     (PET_RULES.md §8, CARD_RULES.md §2, RELIC_RULES.md §6 note 3)

NOT authoritative: the literal strings in the block below.

The executing agent MUST read the provisioned rows and use their exact
primary-key values, and MUST NOT:
  · treat a literal below as a value to write if it disagrees with a row;
  · "correct" a provisioned row to match this file;
  · report a mismatch as a defect in the provisioning implementation —
    the row is authoritative and this file is not.
```

Row set and derived projections:

```text
Starter Pet  — one of the three provisioned MVP Pets (PET_RULES.md §8)
    Xích Lang    ≈ pet-xich-lang      (Element Hỏa, PASSIVE_RULES.md §8)
    Bạch Hổ      ≈ pet-bach-ho        (Element Kim, PASSIVE_RULES.md §8)
    Huyền Quy    ≈ pet-huyen-quy      (Element Thủy, PASSIVE_RULES.md §8)
    NOT Thanh Xà / Sơn Hùng — deferred rows
        (PET_RULES.md §8: their SignatureSkillCardId targets do not exist)

Starter Basic Cards — all three, in the CARD_RULES.md §2 order
    Heal          ≈ card-heal
    Shield        ≈ card-shield
    Power Charge  ≈ card-power-charge

Starter Relic definitions — the three provisioned definitions in the
RELIC_RULES.md §6 order (Burning Curse deferred, §6 note 3)
    Berserker Core  ≈ relic-berserker-core
    Mana Crystal    ≈ relic-mana-crystal
    Assassin Eye    ≈ relic-assassin-eye
```

Only `pet-xich-lang`, `card-heal`, and `relic-berserker-core` appear as written
examples in `DATABASE.md` §1 today; the remaining projections follow the same
fixed rule (`PASSIVE_RULES.md` §8 illustrates the identical derivation for
`passive-bach-ho` / `passive-huyen-quy`). The **selection** above is binding; the
**strings** are not.

### Values that must NOT be invented

```text
- No pet-001 / card-001 / relic-001 or any sequential placeholder.
- No definition row of any kind (that is the separate TASK-082 Decision D
  provisioning implementation — §"Stop Conditions").
- No Pet Tier / Star / Level / XP / Element / PassiveId value: each is read
  from the authoritative document named in §"Implementation Notes".
- No new Relic definition, no Burning Curse row, no substitute for the
  deferred Thanh Xà / Sơn Hùng rows.
- No Player column, no ownership column, no "starter granted" flag.
- No RelicInstanceId format rule: the instance identity is a free PK and the
  implementation mints it (it must not be the RelicDefinitionId).
```

---

## Ownership Initialization Mechanism

This is the mechanism determination the task deliverable rests on. Every element
is sourced; nothing is chosen because it is convenient.

### Where it runs

```text
POST /api/auth/discord                       API_CONTRACTS.md §2 (unchanged)
        ↓
identity resolved (verified DiscordUserId)   ADR-013 / API_CONTRACTS.md §2.4
        ↓
Player matched-or-created                    TASK-023 (existing)
        ↓
   ┌────┴──────────────────────────────────────────────────────┐
   │ the *newly created* Player is the initialization boundary  │
   │  existing Player → NO starter step at all                  │
   │  new Player      → starter ownership initialized exactly   │
   │                    once, before the session is issued      │
   └────┬──────────────────────────────────────────────────────┘
        ↓
session issued                               API_CONTRACTS.md §2.5/§2.8
```

Two authoritative statements fix this location:

- `ARCHITECTURE.md` §2.3 item 3: the backend "identifies/**creates** the
  player" on the authentication boundary — Player creation is already an
  Application-layer step of the auth flow, not a separate system.
- `DATABASE.md` §2: the rows' existence is tied to the **creation-time**
  mechanism ("how a Player's ownership rows first come to exist"),
  and Player creation happens exactly once per Player (`PlayerRepository`
  returns an existing Player unchanged; `DiscordUserId` is UNIQUE,
  `DATABASE.md` §1/§3).

**Application, not Domain and not the controller.** The composition ("which
content defines a starter set") is an Application orchestration concern
(`ARCHITECTURE.md` §2.1: Application orchestrates, Domain decides rules); the
Api controller stays a thin boundary (`ARCHITECTURE.md` §2.1 item 4, and
`AuthController`'s own documented role). The DbContext write stays in
Infrastructure through the existing repositories.

### Why this is not a migration, a seeder, or a new endpoint

```text
Not a data migration
    A migration runs once per DATABASE, at deploy time. It cannot know which
    Player rows will be created later at runtime, and TASK-082 Decision D's
    migration-INSERT mechanism is explicitly scoped to *definition* content
    (DATABASE.md §5 item 4: only content-defined rows; content transcribed,
    never player-derived). Granting ownership to a fixed Player from a
    migration is prohibited by this task's own scope.

Not a startup seeder / HasData / content pipeline
    Forbidden by TASK-082 Decision D and DATABASE.md §5 item 4 ("no startup
    seed runner, external content service, JSON content pipeline, or other new
    persistence mechanism"), and by §"No Startup Seed Runner" below.

Not a new endpoint
    API_CONTRACTS.md §1's endpoint list is the complete REST surface; §5.6 and
    the collection endpoints are read-only. The existing auth/player-creation
    flow already has the one hook this needs.

Not a client responsibility
    The client never decides which Pet, Cards, or Relics a Player owns
    (GAME_RULES.md §18, ADR-001). It only reads the result through
    GET /api/pets, /api/cards, /api/relics (API_CONTRACTS.md §5).
```

### Idempotency

**No new flag is introduced.** The existing creation boundary is the guarantee:

```text
Player row creation happens exactly once per Player
    PlayerRepository.GetOrCreateByDiscordUserIdAsync:
        existing DiscordUserId → return the stored row, write nothing
        new DiscordUserId      → insert exactly one Player row
    DATABASE.md §1/§3: DiscordUserId is UNIQUE, so a concurrent double-insert
    is rejected by the database and the loser re-reads the winner's row.

⇒ the starter step must be reachable only on the branch that actually created
  the row. That branch is where the guarantee lives; no HasReceivedStarter /
  StarterGranted / IsInitialized column, and no "have I granted this yet?"
  query, is required or permitted (AGENTS.md §9).

Second authentication for the same Player
    → player exists → matching branch → starter step not reached
    → GET /api/pets /api/cards /api/relics return the same sets as before.
```

The `PlayerUnlockedCard` composite PK `(PlayerId, CardDefinitionId)` is a second,
database-level guarantee against a duplicated Card unlock. `Pet` and `Relic`
have no such natural key — the creation-branch rule above is what covers them,
and it is the documented one.

### Transactionality

`DATABASE.md` §2's documented invariant is "a successfully initialized Player has
the required starter ownership", and the binding boundary is the one the
document already places Player creation on. This task must satisfy it without
inventing a transactional contract:

- The starter writes must be **inside the same unit of work as the Player
  creation** on the newly-created branch, i.e. the Player row and its starter
  ownership commit together or not at all.
- The existing `PlayerRepository.GetOrCreateByDiscordUserIdAsync` currently
  calls `SaveChangesAsync` itself on the create branch. Reaching that atomicity
  therefore requires the creation path to expose an explicit unit-of-work
  boundary (an `IUnitOfWork`-free approach is preferred: a single
  `SaveChangesAsync` covering the Player plus the three starter sets, expressed
  through the existing repository/persistence boundary). Adding one save-scope
  parameter or one commit operation to the **existing** boundary is the smallest
  correct change; a new repository, a new "starter service", a domain event, or
  an outbox is not (`AGENTS.md` §9, `ARCHITECTURE.md` §5).
- If, while implementing, the existing repository surface genuinely cannot
  express "Player + starter rows commit once" without a new abstraction,
  **STOP and report** — that is a persistence-boundary decision, not something
  to absorb silently (`AGENTS.md` §18).
- The failure direction is the documented one: a starter write failure must
  leave **no Player row and no partial ownership set**, and must not issue a
  session. `API_CONTRACTS.md` §2.6 rule 5's "no Player is created on any failure
  path" and `DATABASE.md` §2's invariant are the same rule here.

### Server authority

```text
The starter set is fixed server-side. No request member, query parameter,
header, or client-supplied value selects, adds to, or replaces it
(GAME_RULES.md §18, ADR-001).

API_CONTRACTS.md §2's request carries exactly one member (`code`), and §2.5's
response shape (`sessionToken`, `playerId`) is unchanged — ownership is not
reported to the client by a new member. The client observes it only through the
existing §5 collection reads.
```

---

## Scope

### In Scope

- Determining (read-only) and implementing the creation-time starter-ownership
  initialization on the existing Player match-or-create path, for a **newly
  created** Player only.
- Creating exactly: 1 `Pet` row, 3 `PlayerUnlockedCard` rows, 3 `Relic` rows —
  referencing the authoritative provisioned definition rows, with each row's
  documented creation values.
- The minimal change to the Player-creation persistence boundary required to make
  the Player row and its starter ownership commit as one unit of work (§
  "Transactionality"), including its wiring in `Infrastructure/DependencyInjection.cs`
  and `Application/DependencyInjection.cs` if a registration is required.
- Tests at the documented depth: new-Player starter set; repeat authentication;
  existing-Player non-duplication; ownership integrity (every FK resolves);
  battle-start compatibility; determinism/repeatability; server authority.
- Reporting (not applying) the effect on TASK-078's readiness.

### Out of Scope

- **Any definition/content provisioning.** No `PetDefinition`,
  `CardDefinition`, or `RelicDefinition` row is authored, and no migration for
  them is created here — that is the separate TASK-082 Decision D provisioning
  implementation (`DATABASE.md` §5 item 4).
- **Re-deciding the starter composition** (1/3/3–5 is settled) or **inventing
  any starter identifier** (`AGENTS.md` §7).
- **Any gameplay**: Match-3, board generation, gems, swaps, match detection,
  cascades, damage, combat, boss logic, passive calculation, card effects, relic
  effects, pet skills, rewards, progression, gacha, shop, drops, or any
  acquisition system. The starter grant is initialization, not a reward system.
- **Any client change.** `LobbyScene`, `GameRuntime`, `SignalRService`,
  `BattleScene`, `GameRuntimePort`, `ApiService`, and every other file under
  `src/frontend/` are untouched. No client-side starter initialization exists.
- **Any API contract change.** No new endpoint; `POST /api/battle/start`
  (`API_CONTRACTS.md` §3) and the §5 collection response contracts are unchanged;
  no undocumented field is added; `API_CONTRACTS.md` §2's response shape is
  unchanged.
- **Any schema change.** No new table, column, index, or constraint. In
  particular, no `HasReceivedStarter` / `StarterGranted` / `IsInitialized` flag
  and no ownership uniqueness constraint beyond the existing
  `PlayerUnlockedCard(PlayerId, CardDefinitionId)` composite PK.
- **A data migration that grants ownership.** A migration cannot assume the
  runtime-authenticated Player exists.
- **A startup seeder**, `HasData`, background service, JSON content loader, or
  external content service (TASK-082 Decision D; `DATABASE.md` §5 item 4).
- **Any persistence of gameplay state in PostgreSQL**, and any PostgreSQL access
  on the battle resolution hot path (`TDD.md` §4 item 3).
- **The Relic ownership storage-shape question** (`DATABASE.md` §1's open note).
  This task writes the `Relic` instance table the current EF Core model and
  `DATABASE.md` §1's primary shape already define; it does not decide the
  alternative join-table form.
- Fixing the reported adjacent issues: `RELIC_RULES.md` §3 vs §6 note 1
  (Burning Curse trigger), the `Sơn Hạc` / `Sơn Hùng` naming question, the stale
  `API_CONTRACTS.md` §3 `cardLoadout` example (`"heal"` vs `card-heal`), and the
  stale `BOSS_RULES.md` §6.4 Pet-PassiveId example. Each is a report-only
  finding (`AGENTS.md` §16).
- Modifying `TASK-078`, `TASK-079`, `TASK-082`, or any `tasks/completed/` file.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [x] A newly created Player receives exactly **1** `Pet` row, **3**
      `PlayerUnlockedCard` rows, and **3** `Relic` rows, created atomically with
      the Player row on the same creation step.
- [x] The starter `Pet` row references the **provisioned** `PetDefinitionId` of
      one of the three `PET_RULES.md` §8 provisioned Pets (Xích Lang / Bạch Hổ /
      Huyền Quy — never a deferred one), read from the definition rows; the
      literal in §"Canonical IDs" is the projected form, not the asserted value.
      The row carries the documented creation values: `Tier` a member of the
      `DATABASE.md` §3 set, `Star` = `Pet.MinStar` (the documented 1–5 floor),
      `XP` = `Pet.InitialXp` (0), `Level` = `Pet.InitialLevel` (1),
      `AcquiredAt` server-set, `PlayerId` the new Player.
- [x] The 3 starter `PlayerUnlockedCard` rows reference the **provisioned**
      `CardDefinitionId` values of the three Basic Cards (`CARD_RULES.md` §2:
      Heal, Shield, Power Charge), each `Category = Basic`.
- [x] **No `Category = PetSkill` Card is granted** — the Inferno, Tidal Barrier,
      and Iron Fang definitions are absent from the starter set.
- [x] The 3 starter `Relic` rows reference the three provisioned
      `RelicDefinitionId` values (the first three rows `RELIC_RULES.md` §6
      lists; `Burning Curse` absent), each with a `RelicInstanceId` that is
      distinct from its `RelicDefinitionId` and unique across the set,
      `PlayerId` the new Player, and `AcquiredAt` server-set.
- [x] **A repeated authentication for the same Discord identity creates no
      additional `Pet`, `PlayerUnlockedCard`, or `Relic` row** for that Player,
      and returns the same `playerId` and the unchanged §2.5 response shape.
- [x] **An existing Player created before this change receives no starter rows**
      by authenticating; the starter step is reachable only on the
      newly-created branch.
- [x] Every starter ownership row's foreign key resolves to an existing
      definition row (no orphan `PetDefinitionId`, `CardDefinitionId`, or
      `RelicDefinitionId`), verified by a test that resolves each reference.
- [x] The resulting ownership is sufficient for the documented
      `POST /api/battle/start` validation: a starter-only Player can submit
      `petId` = the created Pet instance, a 3-entry `cardLoadout` of the three
      starter Basic Cards, and a `relicLoadout` of 3 distinct starter
      `RelicInstanceId` values, and does not fail `API_CONTRACTS.md` §3's
      ownership, category, copy-limit, count, or distinctness checks.
- [x] No new API endpoint, request/response member, or error code exists; the
      `API_CONTRACTS.md` §2.5 response is byte-shape identical to before.
- [x] No schema change exists: the applied EF Core model has no new table,
      column, index, or constraint, and in particular no starter/granted flag.
- [x] No definition/content row is inserted by this task, and no migration is
      authored for `PetDefinition`, `CardDefinition`, or `RelicDefinition`.
- [x] No startup seeder, `HasData`, background service, content loader, or
      external content service is introduced.
- [x] No file under `src/frontend/` is created or modified.
- [x] The client authors nothing: no request member selects the starter set, and
      the client's only observation path is the existing §5 collection reads.
- [x] No gameplay behavior is implemented or changed (no Match-3, combat, Card
      effect, Relic effect, Pet Skill, reward, progression, or acquisition
      logic).
- [x] `TASK-078`, `TASK-079`, `TASK-082`, and every `tasks/completed/` file are
      byte-identical to their pre-task state.
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/GameServer.Domain/            — expected none: the ownership
                                                 entities and their documented
                                                 creation constants already
                                                 exist (Pet.InitialXp /
                                                 InitialLevel / MinStar)
[x] src/backend/GameServer.Application/       — the starter-set composition +
                                                 orchestration on the
                                                 Player-creation boundary, and
                                                 the minimal unit-of-work
                                                 surface on the existing
                                                 persistence boundary
[x] src/backend/GameServer.Infrastructure/    — the persistence-side change that
                                                 makes Player + starter rows
                                                 commit once (existing
                                                 repositories / GameDbContext);
                                                 DI registration if required
[x] src/backend/GameServer.Api/               — only the call site that invokes
                                                 the application use case; the
                                                 controller stays thin and no
                                                 contract changes
[ ] src/frontend/client/ (scenes / runtime / services / state / ui) — none
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (none expected — see Implementation Notes; the mechanism this task
           implements is already owned by DATABASE.md §2)
```

---

## Implementation Notes

- **Single content source, no restatement.** The starter-set definition must
  reference the provisioned `PetDefinition` / `CardDefinition` /
  `RelicDefinition` rows by identity and must not carry a second copy of any
  content value (name, cost, effect, trigger). If a definition row is absent at
  runtime, the starter set must not be fabricated or substituted — see Stop
  Conditions.
- **Existing boundaries to reuse, not replace.**
  `IPetRepository.AddAsync` / `GetDefinitionAsync`,
  `ICardRepository.AddUnlockAsync` / `GetDefinitionAsync`,
  `IRelicRepository.AddAsync` / `GetDefinitionAsync`,
  and `PlayerRepository.GetOrCreateByDiscordUserIdAsync`.
- **Player branch signal.** The starter step must key on "this call created the
  Player row", not on a re-query. `GetOrCreateByDiscordUserIdAsync`'s current
  return type (`Task<Player>`) does not carry that signal; widening it to report
  the branch (or performing the starter set inside the same creation operation)
  is the minimal change. Do **not** add a `Player` column and do **not** probe
  for existing ownership to detect "first login" — the documented model has no
  such field, and probing would introduce a second, weaker idempotency rule.
- **Value sourcing per ownership row.**
  ```text
  Pet.PetDefinitionId    ← the chosen provisioned PetDefinition PK
  Pet.PlayerId           ← the newly created Player's PlayerId
  Pet.Tier               ← DATABASE.md §3's closed set (PET_RULES.md §3);
                           no document fixes a per-Pet MVP Tier, so the
                           current data model's own default member is used and
                           no new balance value is authored (PET_RULES.md §3
                           item 4: MVP ships one Tier instance per Pet)
  Pet.Star               ← Pet.MinStar (the documented 1–5 floor, PET_RULES.md
                           §4; DATABASE.md §3)
  Pet.XP                 ← Pet.InitialXp (0)              PET_RULES.md §5.2
  Pet.Level              ← Pet.InitialLevel (1)           PET_RULES.md §5.2
  Pet.AcquiredAt         ← server clock, set once

  PlayerUnlockedCard.PlayerId          ← the new Player's PlayerId
  PlayerUnlockedCard.CardDefinitionId  ← the three Basic CardDefinition PKs

  Relic.RelicInstanceId  ← minted by the server; must NOT equal the
                           RelicDefinitionId (RELIC_RULES.md §2.2: the two are
                           distinct and never collapsed)
  Relic.PlayerId         ← the new Player's PlayerId
  Relic.RelicDefinitionId← the three provisioned RelicDefinition PKs
  Relic.AcquiredAt       ← server clock, set once
  ```
- **Determinism.** The starter set is a fixed server-side set: no RNG, no
  ordering by time, no per-Player variation. Two newly created Players receive
  the same set of definition references. `AcquiredAt` and the minted instance
  identities are the only Player-varying values, and neither is gameplay input
  (`RELIC_RULES.md` §2.3 item 2 forbids ordering by `AcquiredAt`).
- **No `Pet.CardInventory`, no equip column, no equip table.** Ownership only
  (`DATABASE.md` §2, ADR-012 items 7–10).
- **`POST /api/battle/start` is not touched.** This task makes a valid loadout
  *possible*; `BattleStartService`, `CardLoadoutService`,
  `RelicLoadoutService`, and `BattleController` are unchanged.
- **Documentation expectation.** No `docs/` change is expected:
  `DATABASE.md` §2 already owns the composition and states the mechanism is this
  follow-up task's deliverable, and `DATABASE.md` §5 item 4 already owns the
  mechanism distinction. If implementation appears to require a documentation
  change (e.g. `DATABASE.md` §2 should now state the settled mechanism and exact
  IDs), that is the §17 documentation-change step *within* this task — record it
  in `DATABASE.md` §2 only, keep the version header/changelog updated, and do not
  restate it into a second document. Do **not** write a new owner document.
- **If the mechanism turns out to be underdetermined** (for example: the
  provisioned content cannot supply a required column; the atomicity requirement
  cannot be met without a new persistence abstraction; the "created" branch
  cannot be signalled without a new Player column; a fifth Relic is required):
  **STOP** — do not invent a flag, a column, an abstraction, or content. See Stop
  Conditions.
- **Report-only, do not fix inline** (`AGENTS.md` §16): the three TASK-082
  report-only findings (Burning Curse trigger tension, `Sơn Hạc`/`Sơn Hùng`
  naming, the stale `API_CONTRACTS.md` §3 `cardLoadout` example) and the stale
  `BOSS_RULES.md` §6.4 Pet-PassiveId example.
- **Precedent for the shape of this task's evidence.** `TASK-023` (Player
  match-or-create), `TASK-028` (Card unlock rows), `TASK-027` (Relic instance
  rows) — read their Completion Evidence for the existing repository and test
  conventions before writing new ones.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — the starter-set composition (exactly 1 Pet / 3 Basic
                         Cards / 3 Relics; the exact definition references; no
                         PetSkill Card included), and the new-Player-only
                         branch condition.
[x] Integration tests  — end-to-end against real persistence (isolated store,
                         following PlayerMatchOrCreateTests /
                         BattleStartEndpointTests conventions):
                           · new Discord identity → Player + starter rows
                             persisted, Player row count = 1
                           · repeat authentication → no additional ownership
                             row of any of the three kinds
                           · pre-existing Player → no starter rows created
                           · ownership integrity → every FK resolves
                           · atomicity → a starter write failure leaves no
                             Player row and no partial ownership set
[x] Gameplay scenarios — N/A: this task derives no gameplay rule and changes no
                         gameplay behavior. The nearest scenario is the
                         battle-start compatibility check below, which asserts
                         an API_CONTRACTS.md §3 validation outcome, not a
                         gameplay rule.
[x] API_CONTRACTS.md §3 compatibility — a starter-only Player's three starter
                         entries pass §3's count / ownership / category /
                         copy-limit checks for `cardLoadout`, and the
                         count / ownership / distinctness checks for
                         `relicLoadout`.
[x] Guard tests        — no new API member, no new schema member, no new
                         endpoint, and no `HasReceivedStarter`/`StarterGranted`/
                         `IsInitialized`-style flag anywhere in the applied
                         model.
```

### Key Edge Cases

- **Concurrent first login for one Discord identity** — the documented
  `DiscordUserId` UNIQUE constraint lets one insert win; the loser re-reads the
  winner's row and must not grant a second starter set
  (`PlayerRepository`'s existing handling; `DATABASE.md` §1/§3). Verify no
  duplicated ownership results.
- **Identity resolution failure** — no Player and therefore no starter rows
  (`API_CONTRACTS.md` §2.6 rule 5).
- **A required provisioned definition row is absent** — the starter set must not
  be partially created, and no value may be invented to fill the gap. This is a
  STOP condition, not a fallback (`AGENTS.md` §7).
- **Repeat authentication after the starter set is modified by the Player's own
  play** (e.g. Pet XP gained) — authentication must not reset, re-derive, or
  re-grant anything (`PET_RULES.md` §5.1 item 6: Pet XP persists permanently
  with the instance).
- **An existing Player with partial/legacy ownership** (created before this
  change, or with rows removed out of band) — authentication must not "top up"
  ownership. The starter grant is creation-time only; no repair path exists or
  is added.

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20) always apply. Task-specific:

- **If a literal identity string in §"Canonical IDs" disagrees with the
  provisioned definition row: STOP treating the literal as authority.** The row
  wins (§"Canonical IDs" authority warning). Do not write the literal, do not
  edit the provisioned row, and do not report the provisioning implementation as
  defective. If instead the *selection* cannot be mapped to provisioned rows at
  all, use the provisioned-content stop condition below.
- **If the starter mechanism is still ambiguous after reading the references:
  STOP** — do not choose a mechanism.
- **If the exact starter Pet cannot be determined** (e.g. the provisioned
  `PetDefinition` rows do not exist, or none matches a `PET_RULES.md` §8
  provisioned Pet): **STOP** — the definition-provisioning implementation must
  land first.
- **If the exact starter Relic definition IDs cannot be determined** from the
  provisioned `RelicDefinition` rows: **STOP**. Do not substitute a definition,
  do not provision `Burning Curse`, and do not add a second instance of a
  definition merely to reach 5.
- **If a required ownership-row field has no authoritative initial value**:
  **STOP** — do not author it. In particular, `Pet.Tier` has no per-Pet MVP value
  in any document; if the chosen implementation cannot use the existing data
  model's own default member without deciding a balance value, STOP and report.
- **If Player-creation lifecycle is unclear** for the implementation (for
  example the "created vs matched" branch cannot be signalled without a new
  Player column): **STOP** — that is a schema/contract decision, not an
  in-task addition.
- **If idempotency cannot be guaranteed without a new schema element** (a
  starter/granted flag or a new uniqueness constraint): **STOP** — do not add it.
- **If atomicity genuinely requires a new persistence abstraction**
  (a new repository, an `IUnitOfWork`, an outbox, a domain event): **STOP and
  report** — propose the smallest boundary change for approval
  (`AGENTS.md` §18).
- **If the Relic ownership storage shape blocks the write** (`DATABASE.md` §1's
  open note): **STOP** — that is an unresolved storage decision, not something to
  settle silently.
- **If the solution requires a new API contract, endpoint, or response member:
  STOP.**
- **If the solution requires a new architectural decision: STOP** (propose an
  ADR rather than deciding inline).
- **If the solution requires a gameplay or acquisition rule** (a reward, drop,
  shop, gacha, or progression path): **STOP** — `MVP_SCOPE.md` §2 excludes it.
- **If implementing this task requires authoring a `PetDefinition`,
  `CardDefinition`, or `RelicDefinition` row or a migration for one: STOP** —
  that is the separate TASK-082 Decision D provisioning implementation.
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose.**
- **If any criterion requires modifying `TASK-078`, `TASK-079`, `TASK-082`, or a
  completed task: STOP** — report instead (`AGENTS.md` §16).

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Lifecycle

```text
BACKLOG → READY          Status field only (no file move); the two
                         implementation prerequisites recorded by TASK-084
                         were satisfied: TASK-085 provisioned the definition
                         rows, and the atomicity/concurrency assessment
                         confirmed both are achievable on the existing
                         persistence boundary without a new abstraction.
READY → IN PROGRESS      tasks/backlog/ → tasks/active/
IN PROGRESS → IN REVIEW  Status field only
IN REVIEW → DONE         tasks/active/ → tasks/completed/
```

### Changed Files

**Application (`src/backend/GameServer.Application/`)**
- `Players/PlayerStarterGrant.cs` — new: the staged starter ownership set (1 Pet
  instance + 3 `PlayerUnlockedCard` rows + 3 Relic instances) carried to the
  persistence boundary as one value.
- `Players/PlayerStarterGrantFactory.cs` — new: the deterministic starter
  composition (`pet-xich-lang`; `card-heal`/`card-shield`/`card-power-charge`;
  `relic-berserker-core`/`relic-mana-crystal`/`relic-assassin-eye`), resolved
  against the provisioned definition rows; refuses the whole grant when a
  required definition is absent.
- `Players/IPlayerRepository.cs` — modified: `GetOrCreateByDiscordUserIdAsync`
  now takes a `Func<CancellationToken, Task<PlayerStarterGrant>>` composition
  callback, invoked on the creation branch only.
- `DependencyInjection.cs` — modified: registers `PlayerStarterGrantFactory`
  (scoped).

**Infrastructure (`src/backend/GameServer.Infrastructure/`)**
- `Postgres/Repositories/PlayerRepository.cs` — modified: invokes the composition
  on the creation branch, stages the Player + 7 ownership rows, and commits them
  through **one** `SaveChangesAsync`; the unique-violation catch now discards the
  **entire** staged batch.
- `DependencyInjection.cs` — modified: removed one duplicate `using` (warning
  `CS0105`), which is the only change to this file.

**Api (`src/backend/GameServer.Api/`)**
- `Controllers/AuthController.cs` — modified: passes the composition callback to
  the creation boundary. No contract change.

**Tests (`tests/backend/`)**
- `GameServer.Application.Tests/PlayerStarterGrantFactoryTests.cs` — new (21
  tests).
- `GameServer.Application.Tests/ApplicationRegistrationTests.cs` — modified
  (+1 test).
- `GameServer.Application.Tests/BattleResultTestDoubles.cs` — modified: the
  `IPlayerRepository` double follows the new signature and invokes the callback
  on the creation branch only.
- `GameServer.Infrastructure.Tests/PlayerStarterOwnershipTests.cs` — new (13
  tests, InMemory).
- `GameServer.Infrastructure.Tests/PlayerStarterOwnershipPostgresTests.cs` — new
  (9 tests, real PostgreSQL).
- `GameServer.Infrastructure.Tests/PlayerStarterOwnershipGuardTests.cs` — new
  (18 tests, applied-model guards).
- `GameServer.Infrastructure.Tests/TestStarterGrants.cs` — new: shared
  starter-grant fixtures + Player/ownership cleanup helper.
- `GameServer.Infrastructure.Tests/PlayerMatchOrCreateTests.cs` — modified:
  follows the new signature.
- `GameServer.Infrastructure.Tests/PlayerPostgresConstraintTests.cs` — modified:
  follows the new signature, uses the real resolved starter set (the FK is
  enforced on PostgreSQL), and cleans up ownership rows with the Player.
- `GameServer.Api.Tests/AuthStarterOwnershipTests.cs` — new (9 tests, end-to-end
  through `POST /api/auth/discord`).
- `GameServer.Api.Tests/TestProvisionedContent.cs` — new: seeds the provisioned
  definition rows into an in-memory API test host (production has them from the
  TASK-085 migration).
- `GameServer.Api.Tests/AuthPlayerOwnershipTests.cs` — modified: seeds the
  provisioned content into its host.
- `GameServer.Api.Tests/ApiAuthorityRegressionTests.cs` — modified (+3 guard
  tests).

`tasks/backlog/TASK-083-*.md` was moved to `tasks/completed/` — status,
lifecycle, and completion evidence only.

### Starter Bootstrap

```text
Player:                   1
Pet ownership:            1   (pet-xich-lang, Tier Common / Star 1 / Level 1 / XP 0)
Card ownership:           3   (card-heal, card-shield, card-power-charge)
Relic ownership:          3   (relic-berserker-core, relic-mana-crystal, relic-assassin-eye)
─────────────────────────────
Total ownership rows:     7
Total database rows:      8   (1 Player + 7 ownership rows)
```

### Atomicity

```text
Authenticated Discord identity
        ↓
PlayerRepository.GetOrCreateByDiscordUserIdAsync
        ↓
find by DiscordUserId
        ↓  (existing Player → return unchanged; composition NOT invoked)
CREATE BRANCH
        ↓
mint PlayerId
        ↓
composeStarterGrant(ct)   → Application: resolves provisioned definitions,
                             mints Pet/Relic instance ids, returns the 7 rows
        ↓
StageStarterOwnership(PlayerId, grant)   → re-parents all 7 rows to the minted
                             PlayerId and Adds them + the Player to the SAME
                             scoped GameDbContext change tracker
        ↓
ONE _dbContext.SaveChangesAsync()   → one EF transaction boundary
        ↓
all 8 rows commit, or none does
```

All four entity types (`Player`, `Pet`, `PlayerUnlockedCard`, `Relic`) live on the
single scoped `GameDbContext` (`Infrastructure/DependencyInjection.cs` —
`AddDbContext<GameDbContext>`, default scoped lifetime), so one `SaveChangesAsync`
is one database transaction. The composition runs **before** anything is added to
the change tracker, so a missing definition throws with nothing staged and no
Player row attempted.

No `IUnitOfWork`, `TransactionManager`, `UniversalRepository`,
`StarterOwnershipManager`, event-sourcing engine, explicit `BeginTransaction`, or
new DbContext was introduced: the one combined commit-scope surface is the
`composeStarterGrant` parameter on the **existing** Player-creation boundary.
`PlayerStarterOwnershipGuardTests` asserts the absence of those type names in both
assemblies.

### Concurrency

```text
Request A ─┐
           ├─ DiscordUserId = X   (both miss the find, both insert)
Request B ─┘
        ↓
Winner commits 8 rows
        ↓
Loser's Player insert → DbUpdateException (23505, DiscordUserId unique)
        ↓
catch (IsDiscordUserIdUniqueViolation)      ← unchanged predicate
        ↓
DiscardStagedBatch(created, stagedStarter)  ← THE FIX
        Player entity        → Detached
        1 Pet entity         → Detached
        3 Card entities      → Detached
        3 Relic entities     → Detached
        ↓
re-read the winner's Player row (existing contract)
```

The previous behavior detached only the `created` Player. The seven ownership
entities would have remained `Added`, each carrying a `PlayerId` foreign key to a
row that was never written — so any later `SaveChangesAsync` in the same scope
would have attempted inserts whose FK target did not exist, and the loser would
have retained a starter set belonging to no Player. The catch now detaches the
whole staged batch.

No distributed lock, Redis lock, retry loop, queue, or weakened constraint was
introduced: the `DiscordUserId` UNIQUE constraint remains the mechanism
(`DATABASE.md` §1, §3).

### Tests

```text
First creation:               PASS  (PlayerStarterOwnershipTests 13/13 InMemory;
                                     PlayerStarterOwnershipPostgresTests 9/9 real
                                     PostgreSQL; AuthStarterOwnershipTests 9/9
                                     end-to-end via POST /api/auth/discord)
Exact starter contents:       PASS  (xich-lang; the three Basic Cards; the three
                                     selected Relics — asserted by definition id)
Atomic rollback:              PASS  (Postgres_FailedCommit_ShouldRollBackThePlayer
                                     AndTheWholeStarterSet — forced CK_Pet_Star_Range
                                     violation → 0 Player rows, 0 ownership rows;
                                     Postgres_FailedStarterStage_ShouldLeaveNoPlayer
                                     AndNoPartialOwnership)
Concurrent first-login:       PASS  (Postgres_ConcurrentFirstLogin_ShouldProduce
                                     ExactlyOneStarterSet — 4 concurrent requests,
                                     4 separate contexts/connections → 1 Player,
                                     1 Pet, 3 Cards, 3 Relics, 7 ownership rows;
                                     ran against live PostgreSQL, NOT skipped)
Existing Player:              PASS  (no second starter set; composition not even
                                     invoked on the match branch; no top-up of a
                                     legacy Player with no ownership; progression
                                     not reset)
Definition FK:                PASS  (Postgres_EveryStarterForeignKey_ShouldResolveTo
                                     AProvisionedDefinition + the FK constraints
                                     themselves; every starter Card is Category=Basic)
Relic instance uniqueness:    PASS  (3 distinct server-minted instance ids; each
                                     distinct from its RelicDefinitionId; distinct
                                     PetInstanceId across Players)
Deferred content:             PASS  (no pet-bach-ho / pet-huyen-quy / card-inferno
                                     / card-tidal-barrier / card-iron-fang /
                                     relic-emergency-core / relic-burning-curse —
                                     asserted with all of them present in the store)
Backend regression:           PASS  (1874 tests total, 1873 pass, 1 pre-existing
                                     environment failure — see below)
Build/static:                 PASS  (dotnet build: Build succeeded, 0 errors)
```

```text
Suite              Baseline            After TASK-083           Delta
─────────────────  ──────────────────  ───────────────────────  ─────
Domain             951                 951                      —
Application        350                 372                      +22
Infrastructure     257                 297                      +40
Api                242 (241P / 1F)     254 (253P / 1F)          +12
─────────────────  ──────────────────  ───────────────────────  ─────
Total              1800 (1799P / 1F)   1874 (1873P / 1F)        +74
```

### PostgreSQL-dependent tests: PASS (not skipped)

PostgreSQL was reachable at `localhost:5433/dcacti_db` for this run
(`_available = true`, `_schemaApplied = true`), so **every** PostgreSQL-dependent
test executed rather than auto-skipping. Verified by running
`PlayerStarterOwnershipPostgresTests` in isolation: `Passed: 9, Skipped: 0`, and
the concurrency test individually reported `Passed … [1 s]` with no skip marker.

The two suites that auto-skip without a database
(`PlayerStarterOwnershipPostgresTests`, `PlayerStarterOwnershipGuardTests`'s
sibling `PlayerPostgresConstraintTests`) reported **0 skipped** in this run.
No concurrency, transaction, or FK-resolution coverage was skipped.

### Pre-existing failure (reported, not introduced)

`GameServer.Api.Tests.BattleResultSmokeTest.SmokeTest_AuthoritativeBattleActionTo
ResultRead_ShouldWalkTheWholeDocumentedPath` — **fails identically at baseline**
(confirmed: baseline run before any TASK-083 change reported the same 1 failure in
the same test).

Cause: the test host logs a message, `Microsoft.Extensions.Logging.EventLog`
tries to open the `.NET Runtime` Windows event-log source, and the process lacks
write access (`Win32Exception: Access is denied`), which surfaces as
`AggregateException : An error occurred while writing to logger(s)`. It is an
environment/host condition, not an application defect: with
`Logging__EventLog__LogLevel__Default=None` the same test **passes**
(`Passed: 1, Skipped: 0`).

Reproduced with TASK-083's changes present and with them irrelevant to the test —
it does not touch authentication, Player creation, or ownership. **Not fixed**, per
`AGENTS.md` §16 (out of scope; report rather than fix inline).

Pre-existing build warnings also reproduced unchanged (`NU1900` audit timeouts,
`MSB3277` EF Core Relational 10.0.4-vs-10.0.12, `CS8602`/`xUnit2000`/`xUnit2013`
in unrelated test files). One pre-existing warning **was** removed —
`CS0105` duplicate `using` at `Infrastructure/DependencyInjection.cs:6`, because
that file is part of this task's delivery and the fix is a pure duplicate-line
removal with no behavior change.

### Scope Verification

```text
No gameplay.                    Confirmed — no Match-3, board, gem, swap, match,
                                cascade, combo, combat, damage, healing, shield,
                                card effect, relic effect, passive, pet skill, or
                                boss behavior added or changed.
No Match-3 / board / gems.      Confirmed — no Domain file touched at all.
No combat.                      Confirmed.
No acquisition system.          Confirmed — the bootstrap creates ownership rows
                                only; there is no drop, reward, or acquisition path.
No gacha / shop / rewards.      Confirmed — MVP_SCOPE.md §2 respected.
No progression.                 Confirmed — no XP, Level, Tier, or Star behavior
                                added; the starter Pet simply carries the
                                documented creation constants.
No schema changes.              Confirmed — no migration authored, no new table,
                                column, index, or constraint; GameDbContextModel
                                Snapshot.cs byte-unmodified. The applied model is
                                asserted against DATABASE.md §1's table list and
                                each ownership table's exact member set. In
                                particular there is no HasReceivedStarter /
                                StarterGranted / IsInitialized flag.
No API contract change.         Confirmed — no new endpoint, request member,
                                response member, or error code. DiscordAuthRequest
                                carries only `Code`; DiscordAuthResponse carries
                                only `SessionToken` and `PlayerId`; both asserted.
                                POST /api/battle/start is unchanged and now
                                accepts a real starter-only loadout.
No new undocumented architecture.
                                Confirmed — no IUnitOfWork, TransactionManager,
                                UniversalRepository, StarterOwnershipManager,
                                event-sourcing engine, or new DbContext; asserted
                                by guard tests over both assemblies.
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed the starter set is fixed server-side and unreachable from client
      input (`GAME_RULES.md` §18, ADR-001) — the composition takes no request
      value, and `DiscordAuthRequest` has exactly one member (`code`)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no gameplay implemented; no board, gem, swap, match, cascade,
      combat, Pet, Boss, Card, Relic, Passive, XP, or reward behavior added or
      changed
- [x] Confirmed no API, SignalR, Redis, or existing PostgreSQL contract changed
- [x] Confirmed no new architecture introduced (no new service, manager,
      interface, or abstraction beyond the minimal unit-of-work surface)

### Protected Tasks

```text
TASK-082   SHA-256 111A56BCCE0A63C9A56CE6CD85AD77CD27A6D1F2D84A29CB4EBFEDA7A74E8381 — UNCHANGED
TASK-084   SHA-256 6A973589BF28364BE4BA7ADAEA9D765673F9A0214B12956A428D38FFBBD33CCF — UNCHANGED
TASK-085   SHA-256 CE1E61B9BD649992AF9301AA63699FFCC88140595026F57CFDE93A0D4180CC3C — UNCHANGED
docs/02-technical/DATABASE.md
           SHA-256 F3DD7FFC8F74DEACB991BF21726A282F4974D38E7FD55606630642D25A6678E7 — UNCHANGED
src/frontend/            no file created or modified
tasks/completed/         no file modified (TASK-083 added only on completion)
```

`TASK-085` was **not** modified, and no definition row or migration was authored by
this task. `DATABASE.md` needed no change: §2 already owns the composition, the
creation-time mechanism, and the atomicity/concurrency contract this task
implements, exactly as §"Implementation Notes" predicted.

### Report-Only Findings (not fixed — `AGENTS.md` §16)

- Burning Curse trigger tension (`RELIC_RULES.md` §3 vs §6 note 1) — carried
  forward from TASK-082 / TASK-084, still open.
- `Sơn Hạc` / `Sơn Hùng` naming question — carried forward, still open.
- Stale `API_CONTRACTS.md` §3 `cardLoadout` example (`"heal"` vs `card-heal`) —
  carried forward, still open. This task's own tests submit the real
  `CardDefinitionId` values (`card-heal`, …), which is what the endpoint actually
  accepts; the example in the document remains stale.
- Stale `BOSS_RULES.md` §6.4 Pet-PassiveId example — carried forward, still open.
- `BattleResultSmokeTest` EventLog condition — pre-existing environment failure,
  unchanged; see §"Pre-existing failure".
- TASK-078 readiness impact: the Definition-row and starter-ownership
  prerequisites are now both satisfied, so a real `POST /api/battle/start` loadout
  can pass `API_CONTRACTS.md` §3. TASK-078 remains blocked by its own D2/D4 — this
  task does not modify TASK-078.

