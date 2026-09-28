# TASK-064 — Retire the PetLevelMultiplier Pet Level Derivation

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  THIS TASK REMOVES A RETIRED MECHANISM. IT ADDS NO REPLACEMENT.

  Pet Level is no longer derived from Player.Level × PetLevelMultiplier
  (PET_RULES.md §5.6 item 1, ADR-016 item 13). This task deletes that
  machinery from active implementation, EF mapping, the database schema, and
  the tests that exist solely to validate it.

  It does NOT implement Pet XP. It does NOT implement Player XP. It does NOT
  redefine Pet Level or introduce any new formula.
-->

---

## Metadata

```text
Task ID:           TASK-064
Type:              REFACTOR
Status:            BACKLOG
Risk:              MEDIUM (removes a production DI registration, a Domain
                   type, a mapped `PetDefinition` column, a database CHECK
                   constraint, and a column-dropping migration; it also
                   removes the ONLY production writers of `Pet.Level`. It is
                   a behavior-preserving refactor with respect to every
                   reachable runtime path — `PetLevelService` has no
                   production caller (TASK-063 F2) — but the schema change is
                   destructive, so MEDIUM rather than LOW.
                   tasks/TASK_TYPES.md §4 sets REFACTOR at LOW–MEDIUM
                   ("LOW for isolated, MEDIUM if it crosses a contract
                   boundary"). This crosses the persistence boundary.
                   It is NOT HIGH: no gameplay rule, no API, no event, no
                   Redis/SignalR contract, and no authoritative document
                   changes.)
Priority:          HIGH (it is step 1 of the TASK-063 follow-up sequence and
                   a prerequisite for both the Player XP and Pet XP
                   implementation tasks)
Primary Agent:     persistence (owns the EF mapping, the migration, and the
                   schema removal — the largest and riskiest surface)
Supporting Agents: backend (Application-layer DI registration + service
                   removal),
                   gameplay (confirming no gameplay rule depends on the
                   removed derivation),
                   testing (removing the two retired-mechanism suites and
                   updating the fixtures that construct PetDefinition),
                   review (confirming historical ADR evidence is preserved)
Workflow:          development/refactor.md
                   (`TASK_TYPES.md` §2: "Restructure existing code without
                   any intended behavior change. … the externally observable
                   behavior (APIs, events, state, game rules) must remain
                   identical.")
Skills:            discovery/impact-analysis,
                   backend/persistence-analysis,
                   quality/architecture-conformance,
                   quality/scope-validation,
                   quality/implementation-review
                   (5 skills — Normal/Complex budget, tasks/README.md §12)
Dependencies:      TASK-063 (DONE as an audit — the readiness audit that
                   proved B1; read-only, must NOT be modified),
                   TASK-024 (DONE — the historical task that INTRODUCED this
                   mechanism; read-only, must NOT be modified),
                   TASK-059 (DONE — retired the derivation in the contract;
                   read-only),
                   TASK-061 (DONE — Product Owner decisions; read-only),
                   TASK-062 (DONE — finalized the contract; read-only)
Blocks:            the Player XP implementation task (TASK-063 B2),
                   the Pet XP implementation task (TASK-063 B1 resolution),
                   the Pet XP re-audit
Estimate:          Normal (5 skills; one Domain type + one Application
                   service + one property removed, one EF mapping removed,
                   one migration added, two test suites retired, fixture
                   updates across ~8 test files)
```

**Type classification note.** `REFACTOR`, per `tasks/TASK_TYPES.md` §2
("Restructure existing code without any intended behavior change") and §3
("The code structure needs cleaning without behavior change"). This is not
`FEATURE` (it builds nothing), not `BUG` (nothing deviates from the contract
in a *reachable* behavior — the mechanism is correct code implementing a
retired rule), not `DOCUMENTATION` (it changes code and schema), and not
`ARCHITECTURE` (it introduces no architectural decision; it removes one that
was already retired by ADR-016).

**Refactor purity check (`development/refactor.md` §1).** A REFACTOR must not
silently change behavior. Here the observable behavior change is exactly the
intended one: a retired rule stops being expressible. Every *reachable*
runtime path is unaffected because `PetLevelService` has no caller
(TASK-063 F2, re-verified below), and no API, event, Redis, or SignalR
contract reads `PetDefinition.PetLevelMultiplier`. The task therefore
qualifies as a refactor; if execution finds a reachable caller, that is a
STOP condition (§10), not a reason to widen scope.

---

## 1. Objective

Remove the retired `Player.Level × PetLevelMultiplier → Pet.Level` derivation
from the active implementation and the database schema, so the repository no
longer contains an active implementation path contradicting
`PET_RULES.md` §5.6 item 1 and `ADR-016` item 13.

**This task adds no replacement.** Pet Level's new source (`Pet.XP`) is Pet XP
work and is explicitly out of scope (§6, §7). After this task, `Pet.Level`
remains a persisted column with no production writer — which is the correct
intermediate state, because the writer that replaced it does not exist yet.

---

## 2. Authoritative Contract

**The rule this task enforces** (do not restate it as a new rule; it is
already decided):

- `docs/01-game-design/PET_RULES.md` **§5.6 item 1** — "The
  `Player.Level × PetLevelMultiplier` derivation is RETIRED. Pet Level is no
  longer derived from Player Level, and `PetLevelMultiplier` has **no role**
  in the Pet XP model. It is not an XP curve multiplier, not an XP reward
  multiplier, and not a Pet Level input, and it must not be retained or
  reinterpreted…"
- `docs/01-game-design/PET_RULES.md` §5.6 item 2 — `PetDefinition` does not
  own instance XP or instance Level; it remains static content.
- `docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md`
  item 13 — the old derivation is RETIRED; removing it from the persistent
  contract and from the implementation is follow-up work.
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` —
  supersession note for items 3, 4, 6 (item 3's formula and the
  `PetLevelMultiplier` per-Pet configuration value are superseded).

Supporting references (read to confirm scope, do not edit):

- `docs/01-game-design/GAME_RULES.md` §9.3 (Pet rule, current wording)
- `docs/02-technical/DATABASE.md` §1 (`PetDefinition` no longer lists
  `PetLevelMultiplier`), §3 (`PetDefinition.PetLevelMultiplier > 0` is no
  longer a documented constraint)
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState.Level` — must NOT change)
- `docs/02-technical/API_CONTRACTS.md`, `docs/02-technical/GAME_EVENTS.md`
  (must NOT change)
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` (item 7's
  supersession note — historical evidence, must remain intact)

**Documentation is already correct.** `DATABASE.md` §1/§3 were written by
TASK-059/062 to describe the *retired* state. This task brings the
implementation into line with documents that are already authoritative — it
does **not** change them (§8).

---

## 3. Current State (verified at task creation)

Re-verify each line before editing; line numbers may have drifted.

```text
Domain/Pets/PetLevelDerivation.cs
    public static class PetLevelDerivation — public static int Derive(
    int playerLevel, decimal petLevelMultiplier). Implements
    clamp(floor(Player.Level × PetLevelMultiplier), 1, 50).
    Throws ArgumentOutOfRangeException when multiplier <= 0.

Domain/Pets/PetDefinition.cs:90
    public decimal PetLevelMultiplier { get; init; }   (non-nullable)

Application/Pets/PetLevelService.cs:86, :123
    The ONLY production assignments of Pet.Level in the entire codebase:
    pet.Level = PetLevelDerivation.Derive(player.Level,
                                          definition.PetLevelMultiplier);
    Both methods (RecomputeForPlayerAsync, RecomputeForDefinitionAsync)
    build entirely on the retired derivation.

Application/DependencyInjection.cs:85
    services.AddScoped<PetLevelService>();

Infrastructure/Postgres/Configurations/PetDefinitionConfiguration.cs:76-82
    builder.Property(d => d.PetLevelMultiplier).HasPrecision(9, 4).IsRequired();
    table.HasCheckConstraint("CK_PetDefinition_PetLevelMultiplier_Positive",
                             "\"PetLevelMultiplier\" > 0");

Infrastructure/Postgres/Migrations/20260924163011_AddPetPersistence.cs:21, :28
    PetLevelMultiplier = table.Column<decimal>(numeric(9,4), nullable: false)
    table.CheckConstraint("CK_PetDefinition_PetLevelMultiplier_Positive", ...)
```

**Verified consumer analysis (TASK-063 F2, re-confirmed):**

```text
`PetLevelService` appears exactly three times in src/:
  its own declaration, its DI registration, and XML doc references.
It is registered but NEVER injected, resolved, or invoked. There is no
production caller. grep for RecomputeForPlayerAsync /
RecomputeForDefinitionAsync across src/ returns only the declarations.

`PetDefinition.PetLevelMultiplier` is read ONLY by PetLevelService(:86,:123)
and set ONLY by test fixtures and (potentially) content provisioning.

The only other `.Level =` assignments in src/ are XML doc comments; there is
no production writer of `Player.Level`, `PetState.Level`, or `Pet.Level`
outside PetLevelService.
```

`PetLevelMultiplier` is **non-nullable** (`decimal`, `IsRequired()`), so the
column can be dropped without a nullable-column/backfill ambiguity.

---

## 4. Scope

### In Scope

1. **ACTIVE IMPLEMENTATION — remove**
   - Delete `src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs`.
   - Delete `src/backend/GameServer.Application/Pets/PetLevelService.cs`.
   - Remove `services.AddScoped<PetLevelService>();` from
     `src/backend/GameServer.Application/DependencyInjection.cs`.
   - Remove `PetDefinition.PetLevelMultiplier`
     (`src/backend/GameServer.Domain/Pets/PetDefinition.cs`) and its
     `<summary>` block.
   - Remove the `PetLevelMultiplier` EF mapping and the
     `CK_PetDefinition_PetLevelMultiplier_Positive` check constraint from
     `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetDefinitionConfiguration.cs`.
   - Remove any now-dead `IPetRepository` members that existed only to serve
     the retired recompute path **only if** they have no other caller.
     Verify each with a search before removing; if a member has another
     caller, leave it and report it.

2. **DATABASE SCHEMA — remove the obsolete column**
   - Add **one** EF Core migration, generated with the repository's existing
     convention (`dotnet ef migrations add <Name>`), that drops
     `PetDefinition.PetLevelMultiplier` and its check constraint.
   - Regenerate/refresh `GameDbContextModelSnapshot.cs` as the tooling
     produces it. Do not hand-edit the snapshot.
   - Name the migration consistently with existing ones (e.g.
     `DropPetLevelMultiplier`); follow the existing timestamp-prefixed
     generator output, do not hand-author a timestamp.
   - Follow the existing migration workflow exactly (the repository applies
     migrations via `dotnet ef database update` — `DATABASE.md` §1/§5).

3. **TESTS — remove only the retired-mechanism suites**
   - Delete `tests/backend/GameServer.Domain.Tests/PetLevelDerivationTests.cs`
     — it tests `PetLevelDerivation.Derive` exclusively.
   - Delete
     `tests/backend/GameServer.Infrastructure.Tests/PetLevelRecomputeTests.cs`
     — it tests `PetLevelService` exclusively.
   - Remove the `PetLevelMultiplier` assertions that exist solely to validate
     the retired column:
     `PetPersistenceTests.Model_ShouldConstrainPetLevelMultiplierToBePositive`
     and `PetPersistenceTests.Model_ShouldStorePetLevelMultiplierAsADecimal`,
     plus the `"PetLevelMultiplier"` entry in that file's expected-column-set
     assertion.
   - Remove
     `ApplicationRegistrationTests.AddApplicationServices_ShouldRegisterPetLevelService`
     (the registration no longer exists).
   - Remove the `PetLevelMultiplier = …` initializer from every test fixture
     that constructs a `PetDefinition` (see §5 for the file list).
   - Update `AuthorityRegressionSuiteTests`' doc comment that cites
     `PetLevelDerivationTests` as a coverage source.

4. **DOCUMENTATION COMMENTS — correct stale implementation-facing text**
   - Remove or correct XML/inline comments in actively compiled files that
     assert the retired rule. Known sites (verify current line numbers):
     - `Domain/Pets/Pet.cs` — the class header tree line
       (`Level (denormalized snapshot of PetLevelDerivation.Derive)`), the
       "Level is a denormalized snapshot, not an XP store" paragraph, and the
       `Level` property's summary ("holds the value
       `PetLevelDerivation.Derive` produced …", "There is no XP column…").
     - `Domain/Pets/PetDefinition.cs` — remaining references to
       `PetLevelDerivation` after `PetLevelMultiplier` is removed.
     - `Infrastructure/Postgres/Configurations/PetConfiguration.cs` — the
       comment citing `PetLevelDerivation` and "There is no XP column".
     - `Infrastructure/Postgres/Repositories/PetRepository.cs` — the comment
       citing `PetLevelDerivation` and "the Application recompute".
     - `Application/Pets/IPetRepository.cs` — the `<see cref>` references to
       `PetLevelDerivation` / `PetDefinition.PetLevelMultiplier`.
   - **Correct the reference; do not invent new semantics.** `Pet.Level`
     must not be described as XP-derived by this task — the XP source does
     not exist yet. Where a comment must change, describe the *current*
     truth: the stored Level column exists but has no production writer after
     this change, and its future source is owned by Pet XP work
     (`PET_RULES.md` §5.1/§5.4). Keep it to one factual sentence; do not
     speculate about implementation.

### Out of Scope — MUST NOT be implemented by this task

```text
Pet.XP · Pet XP rewards · Pet XP cap (4900) · Pet XP → Level formula ·
any Pet Level formula · Pet.Level writers · level-up logic ·
Player.XP · Player XP persistence · Player XP rewards (+100/+0) ·
Player Level recalculation · Player.Level writers ·
Battle rewards redesign · RewardSummary member list · BattleResult changes ·
BattleState changes · PetState changes · Redis changes · SignalR changes ·
Match-3 · board · gems · swap · match detection · cascade · combat · damage ·
Pet gameplay · Boss gameplay · Cards · Relics · Passives ·
new migrations beyond the single PetLevelMultiplier drop ·
schema changes beyond that drop · new abstractions of any kind ·
any authoritative gameplay document change
```

Also out of scope (§8): editing `PET_RULES.md`, `GAME_RULES.md`,
`DATABASE.md`, `GAME_STATE.md`, `API_CONTRACTS.md`, `GAME_EVENTS.md`, any ADR,
or any task file — including TASK-063 and any completed task
(`AGENTS.md` §16).

---

## 5. Affected Files & Areas

```text
DELETE
[x] src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs
[x] src/backend/GameServer.Application/Pets/PetLevelService.cs
[x] tests/backend/GameServer.Domain.Tests/PetLevelDerivationTests.cs
[x] tests/backend/GameServer.Infrastructure.Tests/PetLevelRecomputeTests.cs

MODIFY (production)
[x] src/backend/GameServer.Application/DependencyInjection.cs        (:85)
[x] src/backend/GameServer.Domain/Pets/PetDefinition.cs              (:90 + summary)
[x] src/backend/GameServer.Domain/Pets/Pet.cs                        (comments)
[x] src/backend/GameServer.Infrastructure/Postgres/Configurations/PetDefinitionConfiguration.cs (:76-82)
[x] src/backend/GameServer.Infrastructure/Postgres/Configurations/PetConfiguration.cs (comment)
[x] src/backend/GameServer.Infrastructure/Postgres/Repositories/PetRepository.cs (comment)
[x] src/backend/GameServer.Application/Pets/IPetRepository.cs        (comments; members ONLY if uncalled)

ADD (migration, tool-generated)
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/<ts>_DropPetLevelMultiplier.cs (+ .Designer.cs)
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs (regenerated)

MODIFY (tests — fixture initializers only)
[x] tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs             (:853)
[x] tests/backend/GameServer.Application.Tests/ApplicationRegistrationTests.cs        (:23-34 remove test)
[x] tests/backend/GameServer.Application.Tests/AuthorityRegressionSuiteTests.cs       (:30 comment)
[x] tests/backend/GameServer.Infrastructure.Tests/BattleResultPersistenceTests.cs     (:80)
[x] tests/backend/GameServer.Infrastructure.Tests/BattleResultPostgresTests.cs        (:121)
[x] tests/backend/GameServer.Infrastructure.Tests/CardPersistenceTests.cs             (:476)
[x] tests/backend/GameServer.Infrastructure.Tests/PetOwnershipResolutionTests.cs      (:51, :198)
[x] tests/backend/GameServer.Infrastructure.Tests/PetPersistenceTests.cs              (:91, :111, :178-200)
[x] tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs                   (:526)
[x] tests/backend/GameServer.Api.Tests/BattleStartEndpointTests.cs                    (:695)
[x] tests/backend/GameServer.Api.Tests/BattleStartSmokeTest.cs                        (:269)
[x] tests/backend/GameServer.Api.Tests/BattleResultSmokeTest.cs                       (:458)
[x] tests/backend/GameServer.Api.Tests/ApplicationSessionRESTTests.cs                 (:669)
[x] tests/backend/GameServer.Api.Tests/RedisBattleStateSmokeTest.cs                   (:529)

DO NOT TOUCH
[ ] docs/**            (authoritative; already correct — §8)
[ ] tasks/**           (including TASK-063)
[ ] src/frontend/**
[ ] any ADR
```

**Note.** The fixture files in the "MODIFY (tests)" list each need only the
single `PetLevelMultiplier = …` initializer line removed. They are otherwise
unrelated to this task — do not restructure them.

---

## 6. Critical Boundary — Do NOT Redefine Pet Level

Removing the old mechanism does **not** authorize implementing a new one.

```text
FORBIDDEN in this task:
    Pet.XP                          (column, property, or concept)
    Pet.Level = min(floor(XP/100)+1, 50)
    Pet.Level = <anything new>
    a replacement derivation service, helper, or extension
    hard-coding a Level value to "preserve" behavior
```

After this task, `Pet.Level` is a persisted column with **no production
writer**. That is the intended intermediate state. Do not invent a stopgap
writer, a default, or a recompute path to "keep it working" — a stopgap would
be exactly the undocumented architecture `AGENTS.md` §9 and §18 forbid, and it
would contradict `PET_RULES.md` §5.6 item 1's instruction not to reinterpret
`PetLevelMultiplier`.

`Pet.Level` **may remain** as a column and property (§7 of the request:
"do not remove Pet Level"). Only its derivation is removed.

---

## 7. Critical Boundary — Player XP Is Not This Task

`TASK-063` finding B2 records that `Player.XP` is absent from source while the
frozen contract requires it. **This task must not absorb that work.**

```text
FORBIDDEN in this task:
    Player.XP column or property
    Player XP persistence
    +100 / +0 Player XP reward application
    Player Level recalculation
    any change to COMBAT_RULES.md §7 or the Player contract
```

The Player XP contract is frozen and correct in documentation. Implementing it
is a separate task in the TASK-063 sequence.

---

## 8. Documentation Boundary

**Do not modify any authoritative document.**

- `DATABASE.md` §1/§3 already describe `PetDefinition` **without**
  `PetLevelMultiplier`; this task makes the code agree with them.
- `PET_RULES.md` §5.6 already states the retirement; this task enforces it.
- `GAME_STATE.md` §2.3 (`PetState.Level`) is **not** to be modified — the
  battle-time Level member is a separate representation and this task does not
  touch it. Do not renumber `GAME_STATE.md`; do not restore §2.5.
- **Historical ADR evidence must remain intact.** `ADR-011` item 7's
  supersession note, `ADR-012` items 3/4/6 and its supersession note, and
  `ADR-016` item 13 all *mention* the retired mechanism deliberately, to
  record what was superseded and why. Do not delete, reword, or "clean up"
  those references.
- If, and only if, execution finds a concrete **stale implementation
  reference** in a non-authoritative implementation-facing comment, fix that
  comment under §4 item 4. Do not use that as license to edit `docs/`.

If the current authoritative documents were found to still *require*
`PetLevelMultiplier`, that would be a STOP (§10) — report, do not edit.

---

## 9. Architectural Requirements

Preserve the documented role model and boundaries:

```text
Player        = account/owner
PlayerPet/Pet = owned combat Pet instance
PetDefinition = static Pet definition (static content only)
Server        = authoritative (GAME_RULES.md §18, ADR-001)
```

Do not introduce any abstraction, including but not limited to:

```text
PetLevelManager · ProgressionManager · XPService · GenericLevelService ·
UniversalProgressionService · ExperienceManager
```

`AGENTS.md` §9 and `ARCHITECTURE.md` §5 forbid speculative abstraction. This
task **deletes** an abstraction; it must not create one.

Do not change, unless source inspection proves direct coupling to the retired
mechanism (in which case report it precisely rather than widening scope):

```text
BattleState · PetState · SignalR · Redis · BattleResult · RewardSummary
```

Expected finding: **no coupling exists.** `PetLevelService` reads only
`IPlayerRepository` and `IPetRepository`, and writes only `Pet.Level`.

---

## 10. Acceptance Criteria

All binary and objectively verifiable.

**Active implementation**
- [ ] `src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs` does not exist
- [ ] `src/backend/GameServer.Application/Pets/PetLevelService.cs` does not exist
- [ ] `PetLevelService` is no longer registered in dependency injection
      (`DependencyInjection.cs` has no `PetLevelService` reference)
- [ ] `PetDefinition` no longer contains the `PetLevelMultiplier` property
- [ ] A repository-wide search of `src/` for `PetLevelMultiplier` returns
      **0** matches outside migration-history artifacts
- [ ] A repository-wide search of `src/` for `PetLevelDerivation` and
      `PetLevelService` returns **0** matches
- [ ] No active production code calculates `Pet.Level` from
      `Player.Level × PetLevelMultiplier`
- [ ] The solution builds with no errors and no new warnings referable to
      this change

**Database schema**
- [ ] EF configuration no longer maps `PetLevelMultiplier`
- [ ] The `CK_PetDefinition_PetLevelMultiplier_Positive` check constraint is
      removed from the EF model
- [ ] Exactly **one** new migration is added; it drops the
      `PetDefinition.PetLevelMultiplier` column and its check constraint
- [ ] `GameDbContextModelSnapshot.cs` reflects the removal (tool-regenerated,
      not hand-edited)
- [ ] `PetDefinitionId`, `Identity`, `Element`, `PassiveId`,
      `PassiveThreshold`, and `SignatureSkillCardId` are **unchanged**
- [ ] `Pet.Level` and its `CK_Pet_Level_Range` constraint are **unchanged**
- [ ] No `Pet.XP`, `Player.XP`, or any other new column is introduced
- [ ] No unrelated table, constraint, or index is modified

**Tests**
- [ ] `tests/.../Domain.Tests/PetLevelDerivationTests.cs` is removed
- [ ] `tests/.../Infrastructure.Tests/PetLevelRecomputeTests.cs` is removed
- [ ] The two `PetLevelMultiplier`-specific `PetPersistenceTests` cases are
      removed, and its expected-column-set assertion no longer lists
      `PetLevelMultiplier`
- [ ] `ApplicationRegistrationTests` no longer asserts a `PetLevelService`
      registration
- [ ] No test fixture constructs a `PetDefinition` with `PetLevelMultiplier`
- [ ] No test asserts `Pet.Level` equals a Player-derived value
- [ ] **Unrelated Pet Level behavior and tests are not removed** — in
      particular `PetPersistenceTests`' `CK_Pet_Level_Range` coverage,
      `PetOwnershipResolutionTests`, and all `PetState` tests remain
- [ ] The full backend test suite passes

**Boundaries**
- [ ] No `Pet.XP` / Pet XP reward / Pet XP cap / Pet Level formula added
- [ ] No `Player.XP` / Player XP reward / Player Level recalculation added
- [ ] No gameplay behavior added; no gameplay rule changed
- [ ] No `BattleState`, `PetState`, `SignalR`, `Redis`, `BattleResult`, or
      `RewardSummary` contract changed
- [ ] No authoritative document modified (`git status -- docs/` clean)
- [ ] Historical ADR supersession text remains intact
- [ ] TASK-063 and all completed tasks unmodified (`AGENTS.md` §16)
- [ ] No new abstraction introduced

---

## 11. Testing Requirements

### Required Verification

```text
[x] Domain tests        — PetLevelDerivationTests removed; no replacement
                          invented (Pet XP behavior is later work)
[x] Application tests   — ApplicationRegistrationTests updated; existing
                          BattleResult* / BattleStart* suites pass
[x] Infrastructure tests — PetPersistenceTests updated and passing; model
                          snapshot assertion reflects the dropped column
[x] Api tests           — BattleResult* / BattleStart* / session suites pass
                          (fixtures updated only)
[x] Integration         — the new migration applies cleanly against the
                          existing database (`dotnet ef database update`)
```

### Commands (repository conventions)

```text
dotnet build
dotnet test                       (all four test projects)
dotnet ef migrations add DropPetLevelMultiplier \
    --project src/backend/GameServer.Infrastructure \
    --startup-project src/backend/GameServer.Api
dotnet ef database update         (per DATABASE.md §1/§5 workflow)
```

### Required Evidence (proof of removal)

```text
[ ] git grep -n "PetLevelMultiplier" -- src tests
      → only the new migration's DropColumn/DropConstraint statements and any
        historical migration Designer files (which are immutable history)
[ ] git grep -n "PetLevelDerivation\|PetLevelService" -- src tests
      → 0 matches
[ ] dotnet test → all green
```

### Do Not
- Do not add tests for future XP-based Pet Level behavior — that is Pet XP
  work and inventing it here would create the exact gameplay decision this
  task must not make.
- Do not modify a test merely to make it compile if the correct action is
  deletion (the retired-mechanism suites) — and conversely do not delete a
  test that merely *mentions* Pet.

---

## 12. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always
apply. Task-specific conditions — **STOP and report rather than widening
scope**:

- **If current source differs materially from §3's evidence** → STOP;
  re-verify before editing and report the divergence.
- **If `PetLevelMultiplier` is already fully removed** → STOP; the task is
  obsolete.
- **If removing it would require redefining Pet Level** → STOP. Adding a new
  derivation is Pet XP work (§6).
- **If removing it would require a new gameplay decision** → STOP
  (`AGENTS.md` §7).
- **If removing it would require changing an authoritative contract** → STOP;
  report the document and section. Do not edit `docs/`.
- **If `PetLevelService` turns out to have a production caller** (a reachable
  runtime path, not the DI registration) → STOP. TASK-063 F2 found no caller;
  a caller would mean the removal changes reachable behavior and is no longer
  a pure refactor.
- **If database ownership of the field is ambiguous**, or **migration history
  makes safe removal ambiguous** (e.g. a prior migration depends on the
  column's presence, or a data-backfill question arises) → STOP.
- **If removal requires implementing Pet XP** → STOP (§6).
- **If removal requires implementing Player XP** → STOP (§7).
- **If `Pet.Level` cannot remain a valid column** after the derivation is
  removed → STOP; `Pet.Level` must stay (§6).
- **If a `PetDefinition` fixture cannot be updated without changing unrelated
  test semantics** → STOP; report the file and the conflict.
- **If `IPetRepository` members cannot be removed because they have other
  callers** → do **not** force removal; leave them and report it as a
  finding (this is not a STOP, it is a scope note).
- **If another architectural decision is required** → STOP
  (`AGENTS.md` §18).
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries** → STOP & decompose.

When stopped, report the exact conflict and **do not invent a resolution**.

---

## 13. Relationship to the TASK-063 Follow-up Sequence

```text
TASK-063 (DONE, STOPPED/NOT-IMPLEMENTATION-READY)
  B1  retired PetLevelMultiplier live in production code/schema
  B2  Player.XP absent from source
        ↓
TASK-064 (this task)  — retire PetLevelMultiplier          ← step 1
        ↓
Player XP implementation task (FEATURE)                    ← step 2
        ↓
Pet XP re-audit                                            ← step 3
        ↓
Pet XP implementation task                                 ← step 4
```

- **TASK-064 is step 1 only.** It does not create or begin steps 2–4.
- **It is a prerequisite** for both the Player XP implementation task and the
  Pet XP re-audit: B1 must clear before Pet XP readiness can pass.
- **Do not create the Player XP or Pet XP implementation task from this
  task**, and do not create TASK-065.
- **Do not modify TASK-063.** Its audit result is historical evidence.

---

## 14. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

**Deleted (active implementation)**
- `src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs` — the retired
  `clamp(floor(Player.Level × PetLevelMultiplier), 1, 50)` derivation
- `src/backend/GameServer.Application/Pets/PetLevelService.cs` — the only
  production writer of `Pet.Level`, built entirely on that derivation

**Deleted (retired-mechanism test suites)**
- `tests/backend/GameServer.Domain.Tests/PetLevelDerivationTests.cs`
- `tests/backend/GameServer.Infrastructure.Tests/PetLevelRecomputeTests.cs`

**Modified (production)**
- `src/backend/GameServer.Application/DependencyInjection.cs` — removed the
  `services.AddScoped<PetLevelService>();` registration and its comment
- `src/backend/GameServer.Domain/Pets/PetDefinition.cs` — removed the
  `PetLevelMultiplier` property and the header/summary paragraphs describing
  it; the class header tree no longer lists it
- `src/backend/GameServer.Domain/Pets/Pet.cs` — corrected the class header
  tree, the "Level is a denormalized snapshot" paragraph, the `Level`
  property summary, and the `PetDefinitionId` summary
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetDefinitionConfiguration.cs`
  — removed the `PetLevelMultiplier` property mapping (precision 9,4,
  required) and the `CK_PetDefinition_PetLevelMultiplier_Positive` check
  constraint; corrected the class header
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetConfiguration.cs`
  — corrected the `Level` header line and the stale derivation paragraph
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/PetRepository.cs`
  — removed the three uncalled recompute-support members
  (`ListByPlayerIdAsync`, `ListByDefinitionIdAsync`, `SaveChangesAsync`) and
  corrected the class summary
- `src/backend/GameServer.Application/Pets/IPetRepository.cs` — removed the
  same three uncalled members and corrected the interface summary + the
  `AddAsync` summary that referenced the derivation
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs`
  — tool-regenerated; `PetLevelMultiplier` no longer present,
  `CK_Pet_Level_Range` retained

**Added (migration, tool-generated)**
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260927124855_DropPetLevelMultiplier.cs`
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260927124855_DropPetLevelMultiplier.Designer.cs`

**Modified (tests — retired-mechanism cleanup only)**
- `tests/backend/GameServer.Infrastructure.Tests/PetPersistenceTests.cs` —
  removed `Model_ShouldConstrainPetLevelMultiplierToBePositive` and
  `Model_ShouldStorePetLevelMultiplierAsADecimal`; dropped
  `"PetLevelMultiplier"` from the documented-field-set assertion and updated
  its comment
- `tests/backend/GameServer.Application.Tests/ApplicationRegistrationTests.cs`
  — removed `AddApplicationServices_ShouldRegisterPetLevelService`
- `tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs` —
  removed the three uncalled members from the `FakePetRepository` test double
- `tests/backend/GameServer.Application.Tests/AuthorityRegressionSuiteTests.cs`
  — replaced the `PetLevelDerivationTests` coverage citation with
  `PetPersistenceTests`
- Fixture initializer `PetLevelMultiplier = …m,` removed (one line each,
  except `PetOwnershipResolutionTests` which had two):
  `Application.Tests/BattleStartServiceTests.cs` ·
  `Infrastructure.Tests/BattleResultPersistenceTests.cs` ·
  `Infrastructure.Tests/BattleResultPostgresTests.cs` ·
  `Infrastructure.Tests/CardPersistenceTests.cs` ·
  `Infrastructure.Tests/PetOwnershipResolutionTests.cs` ·
  `Api.Tests/ApplicationSessionRESTTests.cs` ·
  `Api.Tests/BattleStartSmokeTest.cs` ·
  `Api.Tests/BattleResultSmokeTest.cs` ·
  `Api.Tests/BattleResultEndpointTests.cs` ·
  `Api.Tests/RedisBattleStateSmokeTest.cs` ·
  `Api.Tests/BattleStartEndpointTests.cs`

**Not modified:** `docs/**` (authoritative and already correct),
`tasks/**` other than this file, `src/frontend/**`, any ADR.

### Migration
- Name: `20260927124855_DropPetLevelMultiplier`
- Operations (exactly two, both scoped to `PetDefinition`):
  1. `DropCheckConstraint("CK_PetDefinition_PetLevelMultiplier_Positive", "PetDefinition")`
  2. `DropColumn("PetLevelMultiplier", "PetDefinition")`
- `Down()` restores both, so the change is reversible.
- Safety check: the migration references **only** the `PetDefinition` table.
  No `Player`, `Pet`, `BattleResult`, `BossDefinition`, or other entity was
  touched; EF detected no unrelated pending model changes.
- Applied via: `dotnet ef database update` against the documented local
  development database (`localhost:5433/dcacti_db`). Applied cleanly.

### Removal Evidence
```text
git grep -n "PetLevelDerivation" -- src tests   → 0 matches
git grep -n "PetLevelService"    -- src tests   → 0 matches
git grep -n "PetLevelMultiplier" -- src tests   → 20 matches, ALL in one of
    two permitted classes:
      (a) immutable migration history — the original
          20260924163011_AddPetPersistence.cs (+ .Designer.cs) and the five
          later Designer.cs snapshots that still record the column as it
          existed when they were generated. Migration history must not be
          rewritten.
      (b) explanatory comments stating the removal — Pet.cs:27,101 ·
          IPetRepository.cs:21 · PetConfiguration.cs:31 ·
          PetDefinitionConfiguration.cs:23,24 · PetRepository.cs:15 ·
          PetPersistenceTests.cs:98,99
    Zero live code references remain (no property, mapping, constraint,
    registration, or caller).
```

### Validation Results
- `dotnet build src/backend/GameServer.sln` — **PASS** (0 errors; 22
  pre-existing warnings, unchanged from the pre-change baseline)
- `dotnet test src/backend/GameServer.sln` — **PASS, 1,499 tests, 0 failed**
  - `GameServer.Domain.Tests` — 897 passed
  - `GameServer.Application.Tests` — 251 passed
  - `GameServer.Infrastructure.Tests` — 164 passed
  - `GameServer.Api.Tests` — 187 passed
- `dotnet ef database update` — applied cleanly
- `GameDbContextModelSnapshot.cs` — `PetLevelMultiplier` 0 occurrences;
  `CK_Pet_Level_Range` retained
- Confirmed no unrelated Pet assertion was removed: `PetPersistenceTests`
  retains its field-set, `CK_Pet_Level_Range`, `CK_Pet_Star_Range`,
  Tier/Star, and index assertions; all `PetOwnershipResolutionTests`,
  `PetState*`, and `BattleStart*` coverage remains green.

### Final Report (required format)

```text
## Status
DONE

## Summary
Retired the `Player.Level × PetLevelMultiplier → Pet.Level` mechanism from
active implementation, EF mapping, database schema, and the tests that
existed solely to validate it. No replacement was added: `Pet.Level` remains
a persisted, constrained column with no production writer, which is the
intended intermediate state until the Pet XP task supplies its XP-based
source.

## Removed
Files deleted: PetLevelDerivation.cs · PetLevelService.cs ·
  PetLevelDerivationTests.cs · PetLevelRecomputeTests.cs
Property removed: PetDefinition.PetLevelMultiplier
EF mapping removed: the PetLevelMultiplier property mapping and
  CK_PetDefinition_PetLevelMultiplier_Positive
DI registration removed: services.AddScoped<PetLevelService>()
Uncalled repository members removed: IPetRepository.ListByPlayerIdAsync,
  ListByDefinitionIdAsync, SaveChangesAsync (+ PetRepository impls)
Tests removed: 2 suites, 2 PetPersistenceTests cases, 1 registration test
Fixtures cleaned: 12 initializer lines across 11 files
Comments corrected: Pet.cs · PetDefinition.cs · PetConfiguration.cs ·
  PetDefinitionConfiguration.cs · PetRepository.cs · IPetRepository.cs

## Unchanged (verified)
Pet.Level property + Pet.Level column + CK_Pet_Level_Range ·
PetState · BattleState · Redis · SignalR · BattleResult · RewardSummary ·
docs/ (0 files) · all historical ADR evidence (ADR-011/012/016 untouched) ·
TASK-063 and every completed task

## Not Implemented (intentionally)
Pet.XP · Pet XP rewards/cap/formula · any replacement Pet Level formula ·
Player.XP · Player XP rewards · Player Level recalculation

## Tests
2 suites removed, 2 cases removed, 1 test removed, 12 fixture lines cleaned,
4 test-double members removed; full backend suite green at 1,499/1,499.

## Migration
20260927124855_DropPetLevelMultiplier — drops the
CK_PetDefinition_PetLevelMultiplier_Positive check constraint and the
PetLevelMultiplier column, both on PetDefinition only; applied cleanly to
the local development database. Model snapshot regenerated.

## Residual Findings
1. The three `IPetRepository` members removed had no production caller
   outside the deleted service, so removal was safe. `GetDefinitionAsync`
   and `GetByIdAsync` were RETAINED — `BattleStartService.cs:183,198` calls
   both.
2. One comment reference at `Pet.cs:73` (the `PetDefinitionId` summary) was
   corrected in the same pass; it named the removed field as read through
   the definition reference.
3. A pre-existing test-database state requirement: the local Postgres
   database had to have this migration applied before the Postgres-backed
   tests passed. Four tests failed on first run for exactly that reason, and
   passed after `dotnet ef database update`. This is expected for a schema
   change and is not a defect.

## Next Step
The Player XP implementation task (TASK-063 step 2) may now be generated,
followed by the Pet XP re-audit. Do not create them from this task.
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no gameplay touched)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — no scope change)
- [x] Confirmed no `PlayerAttack`/`PlayerDefense`/`PlayerHP`/`PlayerCrit`/
      `PlayerPower` or equivalent introduced
- [x] Confirmed no Prestige, Paragon, Season XP, Evolution, or other extra
      progression system introduced
- [x] Confirmed no new abstraction (`PetLevelManager`, `ProgressionManager`,
      `XPService`, `GenericLevelService`, or equivalent) introduced
- [x] Confirmed the Player XP contract is unchanged