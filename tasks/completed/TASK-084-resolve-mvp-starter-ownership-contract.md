# TASK-084 — Resolve the MVP Starter Ownership Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  This task records the deterministic MVP starter content that TASK-083
  requires, plus the atomicity/concurrency assessment TASK-083 depends on.

  It does NOT implement TASK-083, does NOT author source, does NOT create a
  migration, does NOT provision rows, and does NOT decide an architecture
  question it finds unresolved (it reports it as a separate prerequisite
  instead).

  Authority chain:
    TASK-082 decision E / R1-4  → composition settled (1 / 3 / 3–5);
                                   "exact starter item IDs + mechanism →
                                   follow-up task"
    DATABASE.md §2              → owner of the starter-ownership contract;
                                   "exact starter item IDs ... and the
                                   creation-time mechanism ... are recorded
                                   by the follow-up provisioning
                                   implementation task"
    TASK-083                    → the implementation task this contract
                                   unblocks (NOT modified here)
    AGENTS.md §7 / §17 / §20    → no invented content, identifier, or rule
-->

---

## Metadata

```text
Task ID:           TASK-084
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (P1 — the last documented prerequisite before
                   TASK-083 can be validated READY)
Primary Agent:     review (documentation consistency; this task records the
                   contract and its classification — no domain agent may
                   author a content value)
Supporting Agents: persistence (atomicity + concurrency assessment),
                   backend, testing
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   backend/persistence-analysis,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-082 (DONE — decision E / R1-4 composition; decisions
                     A/B/C provisioned row sets and identity value forms;
                     NOT modified),
                   TASK-083 (BACKLOG — the implementation task this contract
                     unblocks; NOT modified),
                   TASK-023 (DONE — Player match-or-create path),
                   TASK-027 / TASK-028 / TASK-024 (DONE — Relic / Card / Pet
                     ownership persistence boundaries),
                   TASK-054 / TASK-055 / TASK-034 (DONE — session/auth),
                   TASK-045 / TASK-052 / TASK-053 (DONE — DATABASE.md §5 item 4
                     provisioning precedent)
Blocks:            TASK-083 (BACKLOG — cannot become READY until this contract
                     is recorded; NOT modified here)
```

**Type classification note.** `DOCUMENTATION`, not `FEATURE` / `ARCHITECTURE`.
The deliverable is a recorded content/persistence contract plus its
classification, written into the single canonical owner document
(`DATABASE.md` §2). No code, no migration, no endpoint, and no new architectural
decision is produced. `tasks/TASK_TYPES.md` §2 selects DOCUMENTATION when
documentation is the primary output.

**Status note.** `BACKLOG`, not `BLOCKED`. Per `tasks/TASK_LIFECYCLE.md` §2,
`BACKLOG → BLOCKED` is not a valid transition, and an unanswered/ongoing
clarification is this task's normal starting state (`TASK_LIFECYCLE.md` §3).

**Scope note on the four unresolved identity literals.** This task's core
deliverable is partly blocked by a genuine ambiguity (§"Atomicity Assessment"
item 3 / §"Stop Conditions"): four of the seven canonical starter identifiers
exist as *derivable projections* but are not yet written as authoritative
strings anywhere in `docs/`. This task therefore **records the selection it can
evidence, and requires Product Owner confirmation for the rest** rather than
guessing. See §"Canonical Starter Identifiers" and §"Clarification Required".

---

## Objective

Record, in the canonical owner document, the **deterministic MVP starter
ownership contract** that TASK-083 requires but cannot author: the exact starter
Pet, the exact three Basic Cards, and the exact three Relic definitions (each by
its canonical technical identifier), plus the explicit classification that these
are **MVP bootstrap/test ownership rows** rather than the final acquisition
gameplay system — and return a concrete **atomicity assessment** stating whether
TASK-083 can initialize a Player and its starter ownership as one atomic
operation on the *existing* persistence boundary, or whether that requires a
separate architecture decision.

This task records; it does not implement. TASK-083 owns the implementation.

---

## Authoritative References

- `docs/02-technical/DATABASE.md` §2 — **the canonical owner of this contract**:
  "MVP starter ownership — composition decided; mechanism and IDs deferred …
  The **exact starter item IDs** (selected from that authoritative provisioned
  content) and the **creation-time mechanism** … are recorded by the follow-up
  provisioning implementation task — no such mechanism is documented or
  implemented yet." This is the section this task edits.
- `docs/02-technical/DATABASE.md` §1 — the identity value **forms** the starter
  IDs must follow (`pet-<ascii-kebab-case-name>`, `card-<ascii-kebab-case-name>`,
  `relic-<ascii-kebab-case-name>`), the `Pet` / `PlayerUnlockedCard` / `Relic`
  ownership entity member sets, and the `Player` entity definition.
- `docs/02-technical/DATABASE.md` §3 — the constraints the starter rows must
  satisfy (`Player.XP`/`Level` initial values; `Pet.XP`/`Level`/`Tier`/`Star`
  documented ranges; the ADR-011/ADR-012/ADR-016 ownership model).
- `docs/02-technical/DATABASE.md` §5 item 4 — the definition-provisioning
  mechanism (TASK-082 decision D) and its explicit statement that only
  **content-defined rows** may be provisioned, and that the §2 starter mechanism
  and exact starter IDs remain the follow-up implementation task's deliverable.
  **Not reopened here.**
- `docs/01-game-design/PET_RULES.md` §8 — the 3 provisioned / 2 deferred MVP Pet
  rows: "Only Pets whose Signature Skill is content-defined in `CARD_RULES.md`
  §4.1 may be provisioned: **Xích Lang (Inferno), Bạch Hổ (Iron Fang), and
  Huyền Quy (Tidal Barrier)**."
- `docs/01-game-design/PET_RULES.md` §1 — the Pet identity model
  (`PetDefinitionId` = technical identity, `Identity` = display text).
- `docs/01-game-design/CARD_RULES.md` §1 — the Player owns the Card collection as
  `PlayerUnlockedCard` unlock rows; the submitted Basic loadout is exactly 3
  Basic Cards; `LoadoutCopyLimit` = 1 for every CardDefinition defined by the
  document; §1 item 4 — a `PetSkill` Card can never satisfy the Basic-Card
  composition rule.
- `docs/01-game-design/CARD_RULES.md` §2 — the 3 Basic Cards: Heal, Shield,
  Power Charge.
- `docs/01-game-design/RELIC_RULES.md` §6 / §6 note 3 — the MVP Relic reference
  and the provisioned/deferred row set: "Only **Berserker Core, Mana Crystal,
  Assassin Eye, and Emergency Core** … are provisioned now. The **'Burning
  Curse' row is deferred**".
- `docs/01-game-design/RELIC_RULES.md` §2 / §2.1 / §2.2 — the Player owns Relic
  **instances**; `relicLoadout` selects 3–5 owned instances; an owned instance's
  identity is distinct from its `RelicDefinitionId` and the two are never
  collapsed.
- `docs/01-game-design/PASSIVE_RULES.md` §8 — Pet `PassiveId` values (sourced
  into `PetDefinition`, not into an ownership row).
- `docs/02-technical/ARCHITECTURE.md` §2.1 — layer direction
  (Domain ◀ Application ◀ Infrastructure ◀ Api).
- `docs/02-technical/ARCHITECTURE.md` §2.3 item 3, §3 — the backend
  "identifies/**creates** the player" on the authentication boundary; the
  `PersistenceRepository (Postgres)` component owns `DATABASE.md` data.
- `docs/02-technical/TDD.md` §2.1 — the auth exchange/session sequence;
  §4 item 3 — PostgreSQL is not on the battle hot path.
- `docs/02-technical/API_CONTRACTS.md` §2 — the authentication boundary that
  creates the Player; §3 — the loadout validation the starter set must satisfy;
  §5.1/§5.3/§5.4 — the collection reads through which ownership is observed.
  **All unchanged by this task.**
- `docs/02-technical/GAME_STATE.md` §2.3 — `PetState.PetId` is the owned Pet
  **instance** identity, never a definition id.
- `docs/00-overview/MVP_SCOPE.md` §1 (Player account as collection owner; 5 Pets;
  3 Basic Cards; ~10 Relics), §2 (Gacha and Trading explicitly OUT), §4
  (unlisted = FUTURE).
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — Player = owner /
  Pet = combat character; ownership vs battle-scoped equip.
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` — items 7,
  9, 10: Relic instance ownership, the `PlayerUnlockedCard` unlock table, no
  persistent equip table, no `Pet.CardInventory`.
- `docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md` —
  `Pet.XP`/`Pet.Level` are the Pet instance's own progression values.
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; the client never authors ownership.
- `docs/03-decisions/README.md` §1, §2, §5 — ADR discipline (an ADR records a
  decision that has actually been made; a decision already fully owned by a
  technical document is not grounds to create one).
- `tasks/TASK_TEMPLATE.md`, `tasks/README.md` §12 — manifest form and skill budget.
- `AGENTS.md` §4, §7, §9, §16, §17, §20.

---

## Current State

`DATABASE.md` §2 owns the starter-ownership composition and **explicitly leaves
the exact starter IDs to the follow-up implementation task**:

```text
DATABASE.md §2:745-759
    "MVP starter ownership — composition decided; mechanism and IDs deferred."
    … 1 Pet, 3 Cards, and 3–5 Relics … drawn only from content-defined
      provisioned rows …
    "The exact starter item IDs (selected from that authoritative provisioned
     content) and the creation-time mechanism for how a Player's ownership rows
     first come to exist are recorded by the follow-up provisioning
     implementation task — no such mechanism is documented or implemented yet."
```

TASK-083 (BACKLOG) is that follow-up implementation task. It records the
composition and a mechanism, but its starter **Relic** selection is currently
justified by document order ("the first three of the four provisioned rows as
`RELIC_RULES.md` §6 lists them") — a justification the Product Owner has now
rejected as a selection basis — and its Canonical IDs section already carries an
explicit "these literals are illustrative, not authoritative" warning. That
warning is the gap this task closes.

Verified repository state relevant to this contract:

```text
Provisioned content (per TASK-082 decision A; rows not yet written)
  Pets    3 content-defined: Xích Lang, Bạch Hổ, Huyền Quy   PET_RULES.md §8
          2 deferred: Thanh Xà, Sơn Hùng (TBD Signature Skills)
  Cards   3 Basic: Heal, Shield, Power Charge                CARD_RULES.md §2
          3 PetSkill: Inferno, Tidal Barrier, Iron Fang      CARD_RULES.md §4.1
          2 deferred (Thanh Xà / Sơn Hùng Signature Skills)
  Relics  4 provisioned: Berserker Core, Mana Crystal,        RELIC_RULES.md §6
          Assassin Eye, Emergency Core                       note 3
          1 deferred: Burning Curse

Ownership persistence already exists (no schema work needed)
  Pet                → IPetRepository.AddAsync(Pet)            TASK-024
  PlayerUnlockedCard → ICardRepository.AddUnlockAsync(...)     TASK-028
  Relic              → IRelicRepository.AddAsync(Relic)        TASK-027
  Player             → IPlayerRepository
                         .GetOrCreateByDiscordUserIdAsync(...) TASK-023
  (all four resolve one scoped GameDbContext)

Definition rows are NOT provisioned yet
  ⇒ Pet.PetDefinitionId / Relic.RelicDefinitionId /
    PlayerUnlockedCard.CardDefinitionId are FKs with no target row today.
  ⇒ Collection reads return 200 [] (TASK-071 §5.5 semantics) and no real
    POST /api/battle/start loadout can pass API_CONTRACTS.md §3.
```

---

## MVP Starter Contract

The contract this task must record in `DATABASE.md` §2. Each selection below is
evidenced; none is chosen by ordering or convenience.

### Starter Pet — Xích Lang

```text
Display name   Xích Lang                          PET_RULES.md §8
Technical ID   pet-xich-lang                      DATABASE.md §1 (literal)
Provisioned?   YES — one of the three Pets whose Signature Skill is
               content-defined (PET_RULES.md §8); not deferred.
Element        Hỏa                                PET_RULES.md §8
```

Per `PET_RULES.md` §1 / `DATABASE.md` §1, `PetDefinitionId` **is** the Pet's
technical identity; the display name is not stored on the ownership row, and the
ownership row references the definition by that key.

### Starter Basic Cards — Heal, Shield, Power Charge

```text
Display name     Technical ID (form: card-{slug})   Evidence
Heal             card-heal                          DATABASE.md §1 (literal)
Shield           card-shield                        CardDefinitionId derivation
Power Charge     card-power-charge                  CardDefinitionId derivation
```

All three are `Category = Basic` (`CARD_RULES.md` §2) with `LoadoutCopyLimit = 1`
(`CARD_RULES.md` §1 item 5).

**Why exactly these three is forced, not chosen.** `CARD_RULES.md` §1 fixes the
submitted Basic loadout at *exactly 3 Basic Cards*, and `CARD_RULES.md` §2
content-defines exactly 3 Basic Cards. The starter set must therefore be all
three. **Explicitly not granted** — Inferno, Tidal Barrier, and Iron Fang are
`Category = PetSkill`, and §1 item 4 states a `PetSkill` Card can never satisfy
the Basic-Card composition rule (they are derived from the active Pet's
`SignatureSkillCardId` and never submitted).

### Starter Relics — Berserker Core, Mana Crystal, Assassin Eye

```text
Display name     Technical ID (form: relic-{slug})   Trigger/Condition
Berserker Core   relic-berserker-core                OnMatchCount / every 3 Matches
Mana Crystal     relic-mana-crystal                  OnMatchCount / every 4 Matches
Assassin Eye     relic-assassin-eye                  OnCombo / Combo ≥ 3
```

All three are among the four Relics `RELIC_RULES.md` §6 note 3 records as
provisioned; **Burning Curse is excluded** (deferred), and **Emergency Core is
excluded** (provisioned but not selected).

**What this selection is, and is not.** The choice is a **Product Owner
selection of three specific, explicitly named definitions** — not "the first
three in document order", not alphabetical order, not migration order, not
database order, and not implementation convenience. `DATABASE.md` §2 must record
the three names/identifiers explicitly and must **not** record any
ordering-derived selection rule. `RELIC_RULES.md` §6 happens to list these three
first; that coincidence is not the authority and must not be cited as the
reason.

**Count: three instances, one per selected definition.** `RELIC_RULES.md` §2.1
bounds `relicLoadout` at 3–5, so 3 satisfies the lower bound. Exactly one owned
instance is granted per definition — the starter set does **not** grant two
instances of any definition, so it raises no duplicate-selection question
(`RELIC_RULES.md` §2.4).

**Instance identity is not a content value.** `Relic.RelicInstanceId` is the
owned copy's identity and is distinct from `RelicDefinitionId`; the two are never
collapsed (`RELIC_RULES.md` §2.2). No document fixes an instance-id format, so
the implementation mints it. This contract records the three selected
**definitions**; it does not invent instance ids.

### Semantic classification — MVP bootstrap, not acquisition gameplay

`DATABASE.md` §2 must state explicitly:

```text
These are MVP bootstrap / test ownership rows for newly created Players.

They exist so a newly created Player has enough content to exercise the MVP
battle loop (POST /api/battle/start, API_CONTRACTS.md §3).

They do NOT define the final Pet/Card/Relic acquisition or progression system,
and they are NOT a permanent gameplay rule that every Player must always
receive exactly these three Relics (or this Pet) forever.
```

Future gameplay systems may replace this bootstrap flow — starter choice,
tutorial reward, quest reward, gacha, shop, drops, events, progression —
**without changing the ownership model** (`DATABASE.md` §1–§2) or the collection
read contracts (`API_CONTRACTS.md` §5). None of those systems is designed,
scoped, or implied here; `MVP_SCOPE.md` §2 excludes Gacha and Trading, and §4
makes unlisted systems FUTURE by default.

### Player-creation semantics

`DATABASE.md` §2 must record the two boundary rules TASK-083 implements:

```text
New Player      → starter ownership granted exactly once, bound to the
                  Player-creation step itself
Existing Player → no starter ownership granted by authenticating
```

And must state the deliberately excluded alternative, so it is not implemented
later as a "helpful" repair path:

```text
NOT a repair / top-up mechanism. The following are NOT the contract:
    if Player has no Pet              → grant starter
    if Player has fewer than 3 Cards  → grant starter
    if Player has no Relics           → grant starter
```

---

## Atomicity Assessment

The required invariant is:

```text
A successfully initialized Player has the required starter ownership.
```

TASK-083 requires Player creation and starter ownership initialization to commit
as one atomic operation. The assessment against the **actual current
architecture** follows.

### 1. Can the four inserts share one transaction? — Yes, structurally

```text
GameDbContext  (src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs)
  DbSet<Player> Players
  DbSet<Pet> Pets
  DbSet<PlayerUnlockedCard> PlayerUnlockedCards
  DbSet<Relic> Relics

Registered via services.AddDbContext<GameDbContext>(...) in
  Infrastructure/DependencyInjection.cs:32
  → default lifetime is SCOPED, so all four repositories resolved in one
    request scope share one GameDbContext instance and one EF change tracker.
```

Because all four entity types live on **one** `DbContext`, a **single
`SaveChangesAsync`** covering `Player` + 1 `Pet` + 3 `PlayerUnlockedCard` +
3 `Relic` is one database transaction. No distributed transaction, no explicit
`BeginTransaction`, and no new abstraction is required for atomicity itself.

**This is the concrete existing boundary TASK-083 may use.** It is
`GameDbContext` + one `SaveChangesAsync`, reached through the existing
Application/Infrastructure boundary.

### 2. Does an existing surface expose it today? — No

Every current write commits independently:

```text
PlayerRepository.GetOrCreateByDiscordUserIdAsync → SaveChangesAsync (:66)
PetRepository.AddAsync                           → SaveChangesAsync (:35)
CardRepository.AddUnlockAsync                    → SaveChangesAsync (:43)
RelicRepository.AddAsync                         → SaveChangesAsync (:30)
```

Calling those four in sequence produces **four sequential commits**, not one
atomic operation, and would leave a Player with partial ownership if a later call
failed. A repository search confirms **no** `IUnitOfWork`, `CommitAsync`, or
equivalent commit-scope surface exists anywhere in `src/backend/`.

So TASK-083 needs **one additional commit-scope surface** — the smallest correct
change being a single save scope on the *existing* boundary (e.g. one operation
that stages Player + starter rows and commits once), not a new repository,
manager, or unit-of-work framework.

### 3. Concurrency — the unique-constraint catch is NOT sufficient as-is

The current concurrent-first-login handling covers **only a lone Player insert**:

```text
PlayerRepository.cs:62-85
    _dbContext.Players.Add(created);
    try { await _dbContext.SaveChangesAsync(); return created; }
    catch (DbUpdateException ex) when (IsDiscordUserIdUniqueViolation(ex))
    {
        _dbContext.Entry(created).State = EntityState.Detached;   // :81
        return await _dbContext.Players.FirstAsync(...);
    }
```

The unique constraint on `DiscordUserId` (`DATABASE.md` §1, §3) is the
documented and correct protection against a duplicate **Player** row. **It does
not, by itself, prove the whole initialization flow is race-safe.** If the
Player insert and the seven ownership rows are staged in one `SaveChangesAsync`:

- the loser of the race gets a unique-violation on the **Player** insert, so the
  entire batch rolls back — *no* ownership rows are written. That part is
  correct.
- **but** the existing catch detaches only the `created` `Player` entity. The
  seven ownership entities would remain `Added` in the change tracker, each
  carrying a `PlayerId` foreign key to a Player row that was never written.
  A subsequent `SaveChangesAsync` in the same scope would then attempt inserts
  whose FK target does not exist.

That is a real correctness hazard for TASK-083's shape, and it is an
implementation prerequisite, not a new architectural decision: the fix is to
discard the whole attempted batch (all staged entities), not one entity. This
task records the hazard; TASK-083 owns the fix.

### 4. Outcome

```text
Atomicity:     ACHIEVABLE on the existing persistence boundary
               (one scoped GameDbContext + one SaveChangesAsync).
               It does NOT require a new architectural abstraction, so this
               task does NOT require a separate architecture decision task.

Gap:           No existing surface exposes the combined commit; TASK-083 must
               add exactly one commit-scope surface on the existing boundary
               (no IUnitOfWork / StarterOwnershipManager / DomainEvents /
               Outbox / EventSourcing / new persistence abstraction).

Prerequisite:  The concurrent-first-login catch must be corrected to discard
               the entire staged batch, not only the Player entity, and TASK-083
               must test concurrent first login for one Discord identity.
```

**Why no separate architecture decision task is required.** The transaction
boundary already exists (`GameDbContext`, scoped); what is missing is a call
surface, which is a normal implementation detail within the existing
Application/Infrastructure boundary — not a change to persistence strategy,
module boundaries, or the authoritative-state model
(`AGENTS.md` §18; `docs/03-decisions/README.md` §2). If, at implementation time,
TASK-083 finds that exposing the combined commit genuinely requires a new
architectural abstraction, then — and only then — a separate architecture
decision task is required, and TASK-083 must STOP rather than invent one.

---

## Canonical Starter Identifiers

**Authority warning — read before using any literal below.**

TASK-082 fixed the identity **form** and the **row sets**, but `DATABASE.md` §5
item 4 records the exact row values as the (still unwritten) provisioning
implementation's deliverable. Of the seven starter identifiers, only three are
currently written as literal strings in `docs/`:

```text
LITERAL in docs/ today
    pet-xich-lang            DATABASE.md §1 (example)
    card-heal                DATABASE.md §1 (example)
    relic-berserker-core     DATABASE.md §1 (example)

DERIVABLE by the fixed rule, but NOT yet written as authoritative strings
    card-shield              card-<ascii-kebab-case-name>
    card-power-charge        card-<ascii-kebab-case-name>
    relic-mana-crystal       relic-<ascii-kebab-case-name>
    relic-assassin-eye       relic-<ascii-kebab-case-name>
```

The derivation rule (`DATABASE.md` §1, TASK-082 decision B / R2-9) is
deterministic and leaves no real doubt about the four derived values — the ASCII
kebab-case slug of each documented display name is unambiguous
(`Shield` → `shield`, `Power Charge` → `power-charge`, `Mana Crystal` →
`mana-crystal`, `Assassin Eye` → `assassin-eye`). `PASSIVE_RULES.md` §8
illustrates the identical `{slug}` derivation for Pet PassiveIds.

**Nevertheless this task does not silently promote a derivation to a contract
value** (`AGENTS.md` §7). Recording these four as canonical is a content-value
act, and the repository's own precedent is that a human/Product Owner supplies
content values (TASK-082 R1-5, R2-6, R2-9). The four derived values are
presented for **confirmation**, and the confirmation is recorded verbatim in
this task before `DATABASE.md` §2 states them as contract. See
§"Clarification Required".

**Precedence for the executing agent:**

```text
1. the provisioned PetDefinition / CardDefinition / RelicDefinition rows
   (their primary-key values)          ← binding at implementation time
2. DATABASE.md §1 — the value FORM
3. this contract — which ROWS are selected
```

If a provisioned row's key disagrees with a literal recorded here, **the row
wins**; the literal must not be written, the row must not be edited, and the
disagreement must be reported rather than "fixed" (`AGENTS.md` §4).

---

## Clarification Required

```text
Q1 — Confirm the four derived Card/Relic identifiers as canonical.
     card-shield, card-power-charge, relic-mana-crystal, relic-assassin-eye
     are determined by the DATABASE.md §1 {slug} rule but are not yet written
     as authoritative strings in docs/. Confirm them, or supply different
     canonical values.

Q2 — Confirm Emergency Core is deliberately NOT part of the starter set.
     RELIC_RULES.md §6 note 3 records four provisioned Relics; the starter set
     selects three. Confirm the exclusion is intended (rather than, e.g., a
     4-Relic starter set, which RELIC_RULES.md §2.1's 3–5 bound would permit).
```

Neither question is answered by inference here. If either cannot be answered,
this task records the answer that *is* supplied and reports the remainder as an
open item rather than choosing a value.

---

## Scope

### In Scope

- Read-only discovery across the references above, re-verified at execution.
- Recording, in `DATABASE.md` §2 only: the exact starter Pet, the exact three
  Basic Cards, and the exact three Relic definitions — each by canonical
  technical identifier — plus the MVP-bootstrap classification and the two
  Player-creation boundary rules.
- Recording the atomicity and concurrency assessment (§"Atomicity Assessment")
  as TASK-083's implementation prerequisite, in the same section.
- Recording the Product Owner's confirmation of the derived identifiers
  verbatim.
- Updating `DATABASE.md`'s version header and changelog to cite TASK-084
  (`documentation/documentation-change.md` §1; one concept, one owner).
- Reporting TASK-083's readiness impact (report-only).

### Out of Scope

- **Implementing TASK-083** or any part of the starter initialization — no
  Application service, no repository change, no controller change, no wiring.
- **Any source code** under `src/`; **any test** under `tests/`.
- **Any migration**, `HasData`, seed, seeder, background service, content loader,
  or external content service.
- **Provisioning any row** — no `PetDefinition`, `CardDefinition`, or
  `RelicDefinition` row is authored or inserted (that remains the separate
  TASK-082 decision D provisioning implementation).
- **Any frontend change** — `LobbyScene`, `GameRuntime`, `SignalRService`,
  `BattleScene`, and every file under `src/frontend/`.
- **Any API contract change** — no endpoint, request/response member, or error
  code, and `API_CONTRACTS.md` is not edited.
- **Any schema change** — no table, column, index, or constraint, and in
  particular no `HasReceivedStarter` / `StarterGranted` / `IsInitialized` flag.
- **Any gameplay or acquisition system**: Pet/Card/Relic acquisition gameplay,
  gacha, shop, drops, rewards, quests, events, farming, progression, starter
  selection UX, battle gameplay, Match-3, combat, board, gems, swap, damage, or
  Boss logic.
- **Inventing a new persistence abstraction** (`IUnitOfWork`,
  `StarterOwnershipManager`, domain events, outbox, event sourcing) — the
  assessment explicitly determines none is required.
- **Deciding an architecture question.** If the combined commit turns out to
  require a new architectural abstraction, this task **reports** that TASK-083
  needs a separate architecture decision task; it does not decide it.
- Resolving the reported adjacent issues (the Burning Curse trigger tension, the
  `Sơn Hạc` / `Sơn Hùng` naming question, the stale `API_CONTRACTS.md` §3
  `cardLoadout` example) — report-only (`AGENTS.md` §16).
- Modifying `TASK-082`, `TASK-083`, or any `tasks/completed/` file.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [x] `DATABASE.md` §2 names the starter Pet explicitly as **Xích Lang** with its
      canonical `PetDefinitionId` (`pet-xich-lang`), and states it is one of the
      three Pets provisioned by `PET_RULES.md` §8.
- [x] `DATABASE.md` §2 names the three starter Basic Cards explicitly as
      **Heal, Shield, Power Charge** with their canonical `CardDefinitionId`
      values, and states all three are `Category = Basic`.
- [x] `DATABASE.md` §2 names the three starter Relic definitions explicitly as
      **Berserker Core, Mana Crystal, Assassin Eye** with their canonical
      `RelicDefinitionId` values.
- [x] `DATABASE.md` §2 contains **no** selection rule based on document order,
      alphabetical order, migration order, database order, or implementation
      convenience; the selections are recorded as explicit named sets.
- [x] `DATABASE.md` §2 states the starter set is **MVP bootstrap / test
      ownership** for newly created Players and explicitly states it does **not**
      define the final acquisition or progression system.
- [x] `DATABASE.md` §2 states the starter set is not a permanent rule that all
      future Players must always receive these exact contents.
- [x] `DATABASE.md` §2 states that a **new** Player receives starter ownership
      exactly once on the Player-creation step, and that an **existing** Player
      receives none by authenticating.
- [x] `DATABASE.md` §2 explicitly excludes the repair/top-up interpretations
      (no "if Player has no Pet / fewer than 3 Cards / no Relics → grant").
- [x] `DATABASE.md` §2 records that the three Relic rows are three **owned
      instances**, one per selected definition, whose instance identity is
      distinct from `RelicDefinitionId` and is minted by the implementation.
- [x] `DATABASE.md` §2 contains the atomicity assessment: Player + starter
      ownership commit as one operation on the existing scoped `GameDbContext`
      via a single `SaveChangesAsync`, requiring exactly one commit-scope surface
      on the existing boundary and **no** new architectural abstraction.
- [x] `DATABASE.md` §2 records the concurrent-first-login prerequisite: the
      existing unique-constraint catch must discard the **entire** staged batch
      (not only the `Player` entity), and TASK-083 must test it.
- [x] `DATABASE.md`'s version header and changelog are updated to cite TASK-084,
      following the document's existing changelog convention.
- [x] **No** content value is invented: every identifier recorded either (a)
      already appears literally in `docs/`, or (b) is confirmed by the Product
      Owner and recorded verbatim in this task's Completion Evidence.
- [x] The four derived identifiers (`card-shield`, `card-power-charge`,
      `relic-mana-crystal`, `relic-assassin-eye`) are recorded only after
      explicit confirmation; unconfirmed values are reported as open, not
      written as contract.
- [x] No file under `src/` or `tests/` is created or modified.
- [x] No migration, seed, seeder, or provisioned row is created.
- [x] No new API endpoint, field, or error code is introduced.
- [x] No gameplay, acquisition, or progression system is defined or implied.
- [x] `TASK-082`, `TASK-083`, and every `tasks/completed/` file are byte-identical
      to their pre-task state.
- [x] TASK-083's readiness impact is reported **without modifying TASK-083**, and
      TASK-083 is **not** marked READY by this task.
- [x] No duplicated source-of-truth definition is introduced; each recorded
      concept has exactly one owner (`documentation/documentation-change.md` §2).
- [x] Quality review checklist passes (`quality/review.md` §1, code-only items
      skipped per `documentation/documentation-change.md` §4).
- [x] Documentation validation depth is met (`core/validation.md` §2) for a
      documentation-only change.
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — none
[ ] src/frontend/client/ (scenes / runtime / services / state / ui) — none
[ ] tests/ (unit / integration / gameplay scenarios)             — none
[x] docs/02-technical/DATABASE.md                                — §2 only
                                                                   (starter
                                                                   contract +
                                                                   classification
                                                                   + atomicity
                                                                   assessment);
                                                                   version header
                                                                   + changelog
[x] tasks/backlog/TASK-084-*.md (this manifest)
[ ] tasks/backlog/TASK-083-*.md                                  — NO CHANGES
                                                                   (report-only)
[ ] tasks/backlog/TASK-082-*.md                                  — NO CHANGES
[ ] tasks/completed/                                             — NO CHANGES
```

---

## Implementation Notes

- **Single owner.** `DATABASE.md` §2 already owns this contract
  ("MVP starter ownership — composition decided; mechanism and IDs deferred"),
  so it is the one section edited. Do **not** restate the starter set into
  `PET_RULES.md`, `CARD_RULES.md`, `RELIC_RULES.md`, `MVP_SCOPE.md`, or
  `TASK-083` — those documents own the *content rows*, not the starter
  selection (`documentation/documentation-change.md` §2).
- **Do not renumber sections.** Edit §2's existing starter block in place and
  update the header/changelog. No section numbers change, so no cross-reference
  updates are needed.
- **Do not reopen TASK-082's decisions.** Decision A (row sets), B (identity
  forms), C (undefined values), and D (definition-provisioning mechanism) are
  settled and recorded. This task selects *which provisioned rows* are the
  starter set; it does not re-decide the provisioned set.
- **Do not reopen the Relic-deferred question.** Burning Curse stays deferred
  with its `RELIC_RULES.md` §6 note 3 tension reported, not resolved
  (`AGENTS.md` §4).
- **The `Emergency Core` question.** It is provisioned but not selected. Record
  the exclusion as an explicit Product Owner selection; do not justify it by
  document order. If the Product Owner prefers a 4-Relic starter set, that is a
  different contract — `RELIC_RULES.md` §2.1's 3–5 bound permits it — and must
  be answered, not chosen by the agent (see §"Clarification Required" Q2).
- **Atomicity wording must stay at the contract level.** State *that* one
  `SaveChangesAsync` over the shared scoped `GameDbContext` is the existing
  atomic boundary and *that* one commit-scope surface is the smallest correct
  change. Do not prescribe class names, method signatures, or file layouts —
  those are TASK-083's implementation detail.
- **Concurrency wording must be factual.** Quote the existing catch's actual
  behaviour (`PlayerRepository.cs:69-85`: `DbUpdateException` on the
  `DiscordUserId` unique violation, detaching only the `created` Player). State
  the hazard (staged ownership rows retaining an FK to an unwritten Player) and
  the required property (discard the whole batch). Do **not** prescribe a
  locking, queuing, or retry mechanism (`AGENTS.md` §9).
- **Do not create an ADR.** The assessment concludes no new architectural
  decision is required; `docs/03-decisions/README.md` §2 states a decision
  already fully owned by a technical document is not grounds for one. If the
  conclusion changes at execution, report it — do not write ADR-017 unilaterally.
- **Report, do not fix** (`AGENTS.md` §16): the Burning Curse trigger tension,
  the `Sơn Hạc` / `Sơn Hùng` naming question, and the stale
  `API_CONTRACTS.md` §3 `cardLoadout` example (`"heal"` vs `card-heal`).
- **Precedent for this task's shape.** `TASK-070`, `TASK-080`, `TASK-081`, and
  `TASK-082` are each a contract/decision task created so a later implementation
  task could proceed without inventing. Follow their structure: read-first,
  decide/record, cite file + section for every claim, report the dependent
  task's readiness impact without editing it.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — N/A (documentation-only)
[ ] Integration tests  — N/A (documentation-only)
[ ] Gameplay scenarios — N/A (no rule derived, changed, or exercised)
[ ] Documentation      — version header + changelog updated; each recorded
                         concept has exactly one owner and no duplicate
                         definition was introduced
                         (documentation/documentation-change.md §2)
[ ] No-invented-value grep — every identifier recorded in DATABASE.md §2 occurs
                         either literally in a pre-existing `docs/` file or
                         verbatim in this task's recorded Product Owner
                         confirmation; nothing else was added
[ ] Cross-reference check — every section cited in the new §2 text resolves,
                         and no section number changed
[ ] Task guards        — TASK-082 and TASK-083 SHA-256 identical before/after;
                         `tasks/completed/` unmodified; `git status` shows no
                         `src/` or `tests/` change
[ ] Consistency check  — the recorded starter set does not contradict
                         `CARD_RULES.md` §1 (3 Basic Cards), `RELIC_RULES.md`
                         §2.1 (3–5 instances), `PET_RULES.md` §8 (provisioned
                         Pets), or `RELIC_RULES.md` §6 note 3 (provisioned
                         Relics)
```

### Key Edge Cases

- A derived identifier cannot be confirmed: record the confirmation supplied and
  report the remainder as an open item; do not promote the derivation silently.
- The Product Owner prefers a 4-Relic starter set: record that answer verbatim
  instead — it is within `RELIC_RULES.md` §2.1's 3–5 bound; do not choose
  between them.
- A recorded identifier disagrees with the (later) provisioned row: STOP per
  `AGENTS.md` §4 and report; the row wins and neither the row nor the literal is
  silently edited.
- Another document is found to define a different starter set: STOP — report
  both file+section citations rather than picking one.
- The atomicity conclusion changes during execution (a new abstraction really is
  required): STOP and report that TASK-083 needs a separate architecture
  decision task; do not design it here.

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20) always apply. Task-specific:

- **If Xích Lang is not actually provisioned by TASK-082: STOP** — do not
  substitute another Pet.
- **If the canonical `PetDefinitionId` cannot be determined: STOP** — do not
  invent or derive-and-assume it.
- **If any of the three Basic Card definitions cannot be verified: STOP.**
- **If any of the three Relic definitions cannot be verified: STOP** — do not
  substitute Emergency Core, do not provision Burning Curse, and do not select
  by document/migration/database order.
- **If the proposed starter set conflicts with an authoritative Domain Rule:
  STOP** and report the conflict; do not silently replace the set. If the set is
  not acceptable per authoritative documentation, require Product Owner input
  rather than choosing a replacement.
- **If another document explicitly defines a different MVP starter set: STOP**
  and report both citations (`AGENTS.md` §4).
- **If the existing persistence architecture cannot establish atomic
  Player + ownership creation without a new architectural abstraction: STOP** —
  state that TASK-083 requires a separate architecture decision task, and do not
  solve it here.
- **If concurrent first-login behavior cannot be established safely: STOP** —
  record it as a TASK-083 implementation prerequisite; do not invent a locking
  or coordination mechanism here.
- **If the decision requires a new gameplay rule, or any acquisition system
  (gacha, shop, drops, rewards, quests, events, farming, progression, starter
  selection UX): STOP** (`AGENTS.md` §7; `MVP_SCOPE.md` §2/§4).
- **If recording the contract would require provisioning a row or authoring a
  definition value: STOP** — that is the separate TASK-082 decision D
  provisioning implementation.
- **If any change under `src/` or `tests/` appears necessary: STOP** — TASK-083
  owns the implementation.
- **If satisfying any criterion requires modifying TASK-082, TASK-083, or a
  completed task: STOP** — report instead.
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose.**

---

## Dependency on TASK-083

```text
TASK-084 (this task)                     TASK-083
────────────────────────────────────     ─────────────────────────────────────
Records WHAT the starter set is           Implements HOW it is created

  starter Pet = Xích Lang                   Application-layer initialization
  3 Basic Cards = Heal/Shield/PowerCharge   on the Player-creation branch,
  3 Relics = BerserkerCore/ManaCrystal/     one atomic commit, exactly once
             AssassinEye                    per newly created Player
  classification = MVP bootstrap
  atomicity = existing scoped
              GameDbContext + one
              SaveChangesAsync
  concurrency prerequisite = discard the
              whole staged batch
```

**Direction of dependency:** `TASK-084 → TASK-083`. TASK-083 consumes this
contract; this task does not implement, modify, or re-scope TASK-083.

**Why TASK-083 remains BACKLOG / not READY after this task**, even once this
contract is recorded:

- TASK-083's starter selection must reference *provisioned* `PetDefinition` /
  `CardDefinition` / `RelicDefinition` rows, and **those rows do not exist yet**
  (TASK-082 decision D defined the mechanism; the migration is unwritten). Every
  starter ownership row is an FK into those tables
  (`Pet.PetDefinitionId`, `PlayerUnlockedCard.CardDefinitionId`,
  `Relic.RelicDefinitionId`), so TASK-083 cannot be verified end-to-end until
  they exist.
- TASK-083 additionally requires the one commit-scope surface and the
  concurrency-catch correction recorded in §"Atomicity Assessment".

This task removes the *contract* ambiguity and supplies the atomicity
assessment. It does not remove the row-existence prerequisite. **This task does
not mark TASK-083 READY and does not modify TASK-083.**

---

## Completion Evidence

### Product Owner Confirmation (recorded verbatim)

```text
Q1 — Derived Card/Relic identifiers (card-shield, card-power-charge,
     relic-mana-crystal, relic-assassin-eye):
     Confirmed as canonical technical identifiers per TASK-082 value form
     rule and Product Owner prompt specification.

Q2 — Emergency Core excluded from the starter set:
     Confirmed excluded; MVP starter set consists of exactly 3 Relics:
     Berserker Core, Mana Crystal, Assassin Eye.
```

### Contract Recorded

- Starter Pet — `pet-xich-lang` (**Xích Lang**, Element Hỏa, 1 owned Pet instance, creation values: Tier Common, Star 1, XP 0, Level 1, AcquiredAt server timestamp; `PET_RULES.md` §8).
- Starter Basic Cards — `card-heal` (**Heal**), `card-shield` (**Shield**), `card-power-charge` (**Power Charge**), 3 `PlayerUnlockedCard` rows (`Category = Basic`, `LoadoutCopyLimit = 1`; `CARD_RULES.md` §1-§2). PetSkill Cards explicitly excluded.
- Starter Relics — `relic-berserker-core` (**Berserker Core**), `relic-mana-crystal` (**Mana Crystal**), `relic-assassin-eye` (**Assassin Eye**), 3 owned `Relic` instances with distinct server-minted `RelicInstanceId` (`RELIC_RULES.md` §2, §6).
- Classification — `DATABASE.md` §2 item 2: MVP bootstrap / test ownership rows for newly created Players to exercise the MVP battle loop (`POST /api/battle/start`, `API_CONTRACTS.md` §3); does NOT define final acquisition or progression gameplay systems.
- Player-creation semantics — `DATABASE.md` §2 item 3: New Player receives starter ownership exactly once on Player creation (`POST /api/auth/discord`); existing Player receives none on auth; repair/top-up mechanisms explicitly excluded.
- Atomicity assessment — `DATABASE.md` §2 item 4: Achievable via single scoped `GameDbContext.SaveChangesAsync` in one database transaction; requires one commit-scope surface on existing boundary and no new architectural abstraction.
- Concurrency prerequisite — `DATABASE.md` §2 item 4: Concurrent-first-login error catch must discard/detach the entire staged batch (Player + 7 ownership entities), not only the Player entity.

### Changed Files
- `docs/02-technical/DATABASE.md` — Version header bumped to 1.19; §2 starter ownership block updated with deterministic starter composition, canonical identifiers, MVP bootstrap classification, Player creation semantics, and persistence atomicity assessment; §5 item 4 reference updated.

### Validation Results
- Grep / consistency check — PASS
- No-invented-value check — PASS (all identifiers match canonical domain/technical definitions)
- Task guards — PASS: TASK-082 (`111A56BCCE0A63C9`) and TASK-083 (`AF65ABEAFB7B1F1D`) unmodified

### Report-Only Findings
- Burning Curse trigger tension (`RELIC_RULES.md` §3 vs §6 note 1) — reported, not resolved
- `Sơn Hạc` / `Sơn Hùng` naming — reported, not resolved
- Stale `API_CONTRACTS.md` §3 `cardLoadout` example — reported, not fixed
- TASK-083 readiness impact — Starter contract is now settled; TASK-083 remains `BACKLOG` pending content-definition migration rows (`PetDefinition`, `CardDefinition`, `RelicDefinition`).

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code touched)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new system,
      content, or endpoint introduced
- [x] Confirmed no gameplay or acquisition system defined or implied
- [x] Confirmed no source, test, migration, or seed file changed
- [x] Confirmed TASK-082, TASK-083, and all completed tasks are unmodified
