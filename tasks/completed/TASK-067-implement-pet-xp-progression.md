# TASK-067 — Implement Pet XP Persistence and Battle Reward Path

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  THIS TASK IMPLEMENTS AN ALREADY-FROZEN CONTRACT. IT DECIDES NOTHING.

  The Pet XP contract is fully decided (PET_RULES.md A5.1A5.6, ADR-016,
  DATABASE.md A1/A3) and already written into the authoritative documents.
  TASK-066 re-audited readiness and returned verdict READY; its
  "Implementation-Task Input Set" is this task's blueprint. This task makes
  the code match the docs.

  It does NOT change any gameplay rule, architecture, API, event, Redis, or
  SignalR contract, and it does NOT touch the Player XP track.
-->

---

## Metadata

```text
Task ID:           TASK-067
Type:              FEATURE
Status:            DONE (resumed from BLOCKED — the single blocking
                    condition, the RewardSummary defeat-shape contradiction,
                    was resolved by explicit Product Owner decision as
                    **Option A**, recorded by TASK-068 §14. See §12.4 for the
                    block resolution and §14 for the Completion Evidence and
                    Final Report. Nothing else in this manifest was rewritten;
                    §12.1-§12.3 are preserved verbatim as the historical record
                    of the block.)
Risk:              MEDIUM (adds a persisted Pet column + one narrow migration,
                   and wires a Pet grant + RewardSummary population into the
                   EXISTING canonical battle-end path. Not HIGH: no gameplay
                   rule changes, no API/event/Redis/SignalR contract changes,
                   no new subsystem, and the integration point, transaction
                   boundary, and exactly-once guard already exist and are
                   verified by TASK-066. tasks/TASK_TYPES.md A4 sets FEATURE
                   at MEDIUM for a backend/db feature. It touches persistence
                   and the battle-end path, so it is not LOW.)
Priority:          HIGH (final step of the TASK-063 readiness sequence; Pet
                   XP / Pet Level progression is IN MVP scope -
                   MVP_SCOPE.md A1)
Primary Agent:     persistence (owns the Pet.XP column, EF mapping,
                   constraint, and the one migration)
Supporting Agents: backend (Pet grant + RewardSummary population in the
                   Application layer's battle-end path, and the narrow
                   Pet progression write boundary),
                   gameplay (contract conformance of the frozen Pet XP
                   reward, formula, cap, and targeting rules),
                   testing (formula-boundary, reward, exactly-once,
                   independence, cap, applied-schema coverage),
                   review (scope + no-second-source-of-truth verification)
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   backend/persistence-analysis,
                   gameplay/gameplay-behavior-derivation,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (6 skills - Normal/Complex budget, tasks/README.md A12)
Dependencies:      TASK-066 (DONE - verdict READY; its "Implementation-Task
                   Input Set" is the authoritative blueprint for this task;
                   read-only, must NOT be modified),
                   TASK-065 (DONE - Player XP persistence + the battle reward
                   path and exactly-once boundary this task mirrors;
                   read-only),
                   TASK-064 (DONE - retired PetLevelMultiplier; read-only),
                   TASK-063 (DONE - readiness audit; read-only),
                   TASK-059 / TASK-061 / TASK-062 (DONE - froze the Pet XP
                   contract into docs/; read-only)
Blocks:            None known - final step of the Pet XP sequence. The Final
                   Report identifies (does not create) any next task.
Estimate:          Normal (6 skills; one Domain member + formula + grant, one
                   EF mapping + constraint, one migration, one narrow
                   repository member, one grant + RewardSummary population at
                   the existing integration point, and test coverage across
                   the existing suites)
```

**Type classification note.** `FEATURE`, per `tasks/TASK_TYPES.md` A2:
"Implement a documented mechanic, capability, or system that already has a
home in `docs/` but has not yet been built." The Pet XP contract has a
complete `docs/` home (`PET_RULES.md` A5.1A5.6, `DATABASE.md` A1/A3,
`ADR-016`) and is not built (TASK-066 finding F4). This is not
`GAMEPLAY-CHANGE` (no rule changes), not `BUG` (nothing misbehaves; the
feature is absent), not `REFACTOR` (behavior is added), and not
`ARCHITECTURE` (no new architectural decision - the persistence boundary,
the transaction boundary, the exactly-once guard, and the battle-end
integration point all already exist and were verified READY by TASK-066).

**Contract-frozen note.** Every value below is already decided and already
authoritative. If execution finds a value that cannot be implemented as
written, that is a STOP (A12) - not license to choose a different value.

---

## 1. Objective

Implement the frozen Pet XP contract end-to-end: a persisted `Pet.XP` column
with its documented initial value and `[0, 4900]` constraint, a deterministic
`XP -> Level` calculation with `Pet.Level` remaining a persisted column, and
the documented `BattleWon +100` / `BattleLost +0` Pet XP grant applied
exactly once to the active combat Pet on the existing canonical battle-end
path - plus the `RewardSummary` population explicitly delegated to this task
by `DATABASE.md` A1 item 2.

**This task implements only Pet XP / Pet Level progression.** The Player
track, Match-3, combat, Boss, Card, Relic, Passive, frontend, Redis, and
SignalR are out of scope (A4).

---

## 2. Authoritative References

The contract is completely specified; do not restate or re-derive it.

- `docs/00-overview/MVP_SCOPE.md` A1 - Pet XP / Pet Level progression is IN
  MVP scope (Tier, Star, Level progression; Pet Level 1A50, Pet XP
  hard-capped at 4900)
- `docs/01-game-design/PET_RULES.md` **A5** - the canonical owner of the
  Pet XP contract:
  - A5.1 - Pet instance owns its own XP and Level; Pet Level is a function
    of Pet XP; two independent tracks that neither read nor modify each
    other; XP persists permanently with the instance
  - A5.2 - initial `Pet.XP = 0`, `Pet.Level = 1` (consistent by
    construction)
  - A5.3 - reward targeting: active combat Pet sole recipient; `BattleWon`
    `+100` / `BattleLost` `+0`; inactive owned Pets `+0`; no passive,
    shared, party-wide, or account-wide Pet XP; no other reward cases
  - A5.4 - `Pet.Level = min(floor(Pet.XP / 100) + 1, 50)`; worked
    boundaries `0->1`, `100->2`, `4900->50`; reward amount (`100`,
    configuration) and curve constant (`100`, formula) are independent
    concepts; same formula *shape* as Player, independent pools
  - A5.5 - `Pet.XP` hard maximum `4900`; post-cap rewards are "neither
    awarded nor stored"; no overflow, hidden XP, prestige, or post-50
    accumulation; Level cap and XP cap are separate facts; deliberate
    divergence from the uncapped Player track - neither track's cap rule
    may be applied to the other
  - A5.6 - `PetLevelMultiplier` derivation RETIRED (item 1); `PetDefinition`
    never owns instance XP/Level (item 2); no Evolution system (item 4)
  - A5.7 - Pet XP grants no combat stats
- `docs/01-game-design/COMBAT_RULES.md` **A7** - the Player track
  (`+100`/`+0`, uncapped XP, Level formula/cap). Owner of the *Player* side
  of the two-track model; this task must not change any of it
- `docs/02-technical/DATABASE.md`
  - A1 - Pet block carries `XP` (int, NOT NULL, default 0) on the owned
    Pet row; PetDefinition carries none; **"Reward semantics for
    `RewardSummary`" (A1 items 1-5)** - item 1 freezes the four
    Player-track members (`playerXpGained`, `newPlayerXp`,
    `playerLeveledUp`, `newPlayerLevel`); item 2 delegates the **Pet-track
    member list to this implementation task** (semantics fixed by
    `PET_RULES.md` A5.3-A5.5; illustrative names in TASK-059 are NOT part
    of the contract); item 3 forbids placeholder members; item 4 keeps `{}`
    in force **until this implementation task lands**; item 5 (both
    Outcomes present; defeat carries no line items)
  - A3 - Pet constraints: `Pet.XP int, NOT NULL, default 0`; `Pet.XP`
    range `[0, 4900]`; `Pet.Level` `[1, 50]`; Pet XP is persistent storage,
    not BattleState
- `docs/02-technical/GAME_EVENTS.md` A2 - `BattleWon`/`BattleLost` item 2:
  the Pet member list is deferred; no Pet XP event exists or is required
- `docs/02-technical/API_CONTRACTS.md` A4 note 1 - `rewards` returns the
  stored `RewardSummary` unchanged; no new endpoint or wire field
- `docs/02-technical/GAME_STATE.md` A2.3 - `PetState` carries `PetId` (the
  recipient identity) and no Pet XP member
- `docs/02-technical/REDIS_STATE.md` (:310-324) - no progression key exists;
  Pet XP must not become one
- `docs/02-technical/ARCHITECTURE.md` A5 - anti-overengineering notes
- `docs/02-technical/TDD.md` A4 - layering (Domain entity, Infrastructure
  mapping, Application orchestration)
- `docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md`
  items 1-14 (two independent tracks; item 13 `PetLevelMultiplier` stays
  retired; Consequences: RewardSummary Pet member list is "a representation
  decision, not an open gameplay decision")
- `AGENTS.md` A7 (no invented rules), A9 (no overengineering), A16 (task
  discipline), A17 (documentation change rule), A18 (architecture change
  rule), A20 (stop conditions)

---

## 3. Current State (verified at task creation)

Everything below was re-verified in the working tree while creating this
task and matches TASK-066's Completion Evidence (findings F1-F21).

**Gate**
- TASK-066 is DONE with verdict **READY**
  (`tasks/backlog/TASK-066-pet-xp-implementation-readiness-reaudit.md:703-707`);
  its Implementation-Task Input Set (`:1167-1250`) is this task's blueprint.
  TASK-066 is read-only evidence; do not modify it.

**Domain - the absent feature**
- `src/backend/GameServer.Domain/Pets/Pet.cs` members:
  `PetInstanceId`(:60), `PlayerId`(:67), `PetDefinitionId`(:75), `Tier`(:83),
  `Star`(:91), `Level`(:107, `set`, zero production writers), `AcquiredAt`(:113).
  **No XP member.** The class comment still records XP as unimplemented.
- Mirror pattern: `src/backend/GameServer.Domain/Players/Player.cs` -
  `InitialXp`(:70), `BattleWonXpReward`(:95), `XP`(:155),
  `LevelForXp(int)`(:234), `GrantBattleXp(int)`(:263-273 re-derives `Level`).

**Persistence - the absent column**
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetConfiguration.cs:35`
  - "There is still no XP column on this table." Level mapping `:103`;
  `CK_Pet_Star_Range` `:109`; `CK_Pet_Level_Range` `:113-114`.
- `.../Migrations/GameDbContextModelSnapshot.cs:149-188` - Pet mapped with
  Star/Level check constraints, **no XP**.
- Migration inventory: 8 migrations; `20260924163011_AddPetPersistence.cs`
  created the Pet table without XP (`:31-60`); **no Pet.XP migration exists.**

**Application - the single integration point (already correct)**
- `src/backend/GameServer.Application/Battle/BattleResultService.cs`
  - `PersistTerminalResultAsync` `:209` - the canonical terminal path
  - `var petInstanceId = state.PetState.PetId.Value` `:218`; validated `:223`
  - `EmptyRewardSummary = "{}"` `:93`, assigned when the row is built `:292`
  - `AddAsync` `:306-308` -> `firstDurableWrite` flag `:306`
  - first-write gate `:319-322` -> `GrantPlayerXpAsync` `:321`
  - `GrantPlayerXpAsync` `:394` (the pattern to mirror; touches no Pet state)
  - constructor takes `IPlayerRepository` `:145`
- `src/backend/GameServer.Application/Pets/IPetRepository.cs` - `AddAsync`
  `:37`, `GetDefinitionAsync` `:48`, `GetByIdAsync` `:78`. **No progression
  write boundary exists yet** (Player's analog:
  `IPlayerRepository.SaveProgressionAsync` `:99`).
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/BattleResultRepository.cs`
  - `AddAsync` `:49` returns `Task<bool>`; an existing row for the battle's
    own key reports `false` `:82`; the single `SaveChangesAsync` `:87`.
  This is the documented exactly-once guard (DATABASE.md A1 sourcing item 1).

**Retired mechanism - stays retired**
- `grep PetLevelMultiplier|PetLevelDerivation|PetLevelService` over `src/`
  and `tests/` -> zero live code references (TASK-066 F1: 29 matches, all
  immutable migration history or explanatory comments).

**Tests - starting point**
- `tests/backend/GameServer.Infrastructure.Tests/PetPersistenceTests.cs:57-67`
  (`Pet_ShouldExposeNoXpOrEvolutionField`) currently **forbids** an `XP`
  member - it must be updated, not deleted (Evolution must stay forbidden).
- Template suites from TASK-065: `PlayerXpProgressionTests.cs` (Domain),
  `PlayerXpBattleRewardTests.cs` (Application, incl. in-memory repository
  harness), `PlayerProgressionPersistenceTests.cs`,
  `PlayerXpSchemaTests.cs` (applied schema + migration history).
- `RewardSummary == "{}"` assertions exist in: `BattleResultServiceTests.cs`
  (`:150`, `:273-292`), `BattleResultTerminalFlowTests.cs:175`,
  `BattleResultPersistenceTests.cs:254`, `:385-403`,
  `BattleResultSmokeTest.cs:221`, `BattleResultEndpointTests.cs:289`
  (comment). These will need updating once `RewardSummary` is populated -
  expected and documented (DATABASE.md A1 item 4), not a regression.

**Baseline / working tree**
- Recorded suite baseline: **1557 passed / 0 failed** (TASK-065). Record the
  actual result again at execution; do not assume.
- The working tree carries the pre-existing, uncommitted TASK-059...TASK-066
  changes. TASK-063/064 remain physically in `tasks/backlog/` with DONE
  evidence (TASK-066 F13) - report-only, do not repair, do not move
  (`AGENTS.md` A16).

---

## 4. Scope

### In Scope

1. `Pet.XP` domain member, documented constants, the Pet Level formula, and
   the Pet grant method (cap enforced on write).
2. EF mapping of `Pet.XP` (required, default 0, `[0, 4900]` check) with the
   existing Pet constraints retained.
3. Exactly **one** new migration scoped to the Pet table; snapshot
   regeneration; migration application + model-change verification.
4. One minimal Pet progression write boundary on
   `IPetRepository`/`PetRepository` (analogous to `SaveProgressionAsync`).
5. The Pet XP grant beside `GrantPlayerXpAsync` inside the existing
   `PersistTerminalResultAsync`, gated by the same first-durable-write flag,
   recipient = the already-resolved `petInstanceId`.
6. The delegated `RewardSummary` representation decision: define the
   Pet-track member list from the decided semantics and populate the summary
   (replacing the `{}` staging value on the win path) per `DATABASE.md` A1
   items 1-5.
7. Test coverage across Domain / Application / Infrastructure (and Api where
   `{}` is asserted), including all eight gaps enumerated in TASK-066 F19.

### Out of Scope - MUST NOT be implemented by this task

- **Player track:** `Player.cs` XP values, reward amounts, uncapped rule,
  Level formula, and all TASK-059/TASK-065 outputs. The Pet grant must not
  touch them; the Player grant already exists and must not be reworked.
- **Match-3, Board, Gems, Swap, Match detection, Cascade, special gems,
  Element, Combat formulas/damage, Boss, Cards, Relics, Passives, Skills,
  Evolution, Tier, Star, Gacha.**
- **Any reward or progression beyond the documented Pet XP** (no bonus XP,
  no streak XP, no daily XP, no XP items, no prestige, no post-50 system).
- **Frontend gameplay** (React / Phaser / GameRuntime / BattleScene /
  SignalRService).
- **Redis `BattleState`, SignalR protocol, new events, new endpoints,
  new wire fields.**
- **`docs/`** - no documentation change is required or authorized (the
  documents already describe the final state; TASK-066 F18).
- **`PetLevelMultiplier` in any form** - stays retired
  (`PET_RULES.md` A5.6 item 1, ADR-016 item 13).
- **Task files** - TASK-059...TASK-066 must remain byte-identical; this
  manifest does not modify them, and no follow-up task may be created from
  this task (the Final Report identifies, but does not create, a next step).
- Any item listed as OUT in `MVP_SCOPE.md` A2 (PvP, Gacha, Guild, Trading,
  Map, Terrain, Weather, microservices, etc.).

---

## 5. Implementation Notes

### 5.1 The frozen Pet XP contract (values - already decided)

```text
Owner          A- Pet instance                (PET_RULES.md A5.1; DATABASE.md A1)
Initial XP     A- 0                           (A5.2)
Initial Level  A- 1                           (A5.2; consistent by construction)
Level range    A- [1, 50]                     (existing CK_Pet_Level_Range)
Level formula  A- min(floor(XP / 100) + 1, 50)   (A5.4)
BattleWon      A- +100 Pet XP                 (A5.3; reward amount 100, configuration)
BattleLost     A- +0 Pet XP                   (A5.3 item 2; Level unchanged)
Recipient      A- active combat Pet ONLY      (A5.3 item 1 = state.PetState.PetId.Value)
Inactive Pets  A- +0 (no passive/shared/party/account-wide XP)  (A5.3 item 3)
XP cap         A- 4900 HARD                   (A5.5 items 1-3: not awarded, not
                   stored, no overflow, no hidden XP, no prestige)
Level cap      A- 50                          (A5.5 item 4)
Curve constant A- 100 (formula) - independent of the reward amount  (A5.4)
Player track   A- +100/+0, uncapped, untouched (COMBAT_RULES.md A7; pools
                   independent per A5.1 item 5)
```

### 5.2 The 15-step implementation boundary

Ordered, minimal, and closed. If a step turns out to require anything beyond
this list, that is a STOP (A12), not an invitation to expand.

1. Add `XP` to `Pet.cs` with the documented initial-0 constant, the `4900`
   hard-cap constant, and the reward-amount constant (independent of the
   curve constant).
2. Add the Pet Level pure function (mirroring `Player.LevelForXp`,
   `Player.cs:234`) - `Pet.Level` stays a persisted column and is re-derived
   from `XP`.
3. Add the Pet grant method (mirroring `Player.GrantBattleXp`,
   `Player.cs:263-273`): adds the outcome amount, enforces the hard cap
   **on write** (stored XP never exceeds 4900; a grant at 4900 is neither
   awarded nor stored), then re-derives `Level`.
4. Refresh the now-stale Pet/PetConfiguration comments about "no XP" to
   describe the implemented state (comment-only; keep the retirement notes).
5. Map `Pet.XP` in `PetConfiguration.cs`: required, `HasDefaultValue(0)`,
   `[0, 4900]` check constraint; retain `CK_Pet_Level_Range` and
   `CK_Pet_Star_Range` unchanged.
6. Generate **exactly one** new EF migration scoped to the Pet table (XP
   column + range constraint); regenerate `GameDbContextModelSnapshot.cs`.
   Never edit a historical migration.
7. Apply the migration (`dotnet ef database update`); existing Pet rows
   receive `XP = 0` via the column default; **no Level->XP backfill**; no
   other table touched.
8. Add ONE minimal progression-write member to `IPetRepository` and
   implement it in `PetRepository` (analogous to `SaveProgressionAsync`);
   no speculative or unused members.
9. Inject the Pet boundary into `BattleResultService` and extend the
   existing `PersistTerminalResultAsync` with a Pet grant beside
   `GrantPlayerXpAsync` (`:394`), gated by the same `firstDurableWrite` flag
   (`:319`). No second battle-resolution path, service, event, or pipeline.
10. Recipient = the already-resolved `petInstanceId` (`:218` =
    `state.PetState.PetId.Value`); exactly one recipient per battle; every
    other owned Pet untouched.
11. Player grant path and Player files: no behavioral change; the two grants
    must not read or write each other's pool.
12. Define the Pet-track `RewardSummary` member list (the delegated
    representation decision, `DATABASE.md` A1 item 2) as the **smallest**
    member set that projects the decided semantics of `PET_RULES.md`
    A5.3-A5.5, and populate `RewardSummary` with it plus the four frozen
    Player-track members (item 1), replacing `EmptyRewardSummary` (`:93` /
    `:292`) on the path where members exist; defeat shape per item 5. No
    placeholder members (item 3). See A5.6 below - STOP on conflict.
13. Add/update tests covering the eight gaps in A9 (incl. cap behaviour,
    inactive-Pet +0, applied schema, exactly-once retry) and update
    `PetPersistenceTests.cs:57-67`.
14. Run the full verification set: `dotnet build`, `dotnet test` (record
    actual counts vs. the recorded baseline 1557/0),
    `dotnet ef migrations has-pending-model-changes` (must be clean).
15. Move this task file backlog -> active -> completed per
    `TASK_LIFECYCLE.md` and write the Completion Evidence + Final Report in
    the required format (A14).

### 5.3 Domain: XP member, formula, grant

- One runtime source for `Pet.Level`: `Pet.XP`. The grant method re-derives
  it; nothing else may write `Level` (today: zero production writers - keep
  it that way apart from the grant/creation path).
- Model `Player` exactly: same formula *shape*, **separate constants, no
  shared abstraction** (`Player.cs:219-223` explicitly refuses to
  generalize; `AGENTS.md` A9). Do not create a shared `XP` base class,
  helper service, or static utility used by both tracks.
- Cap enforcement happens where XP is written: stored `XP` can never exceed
  `4900`, negative XP is impossible, and nothing is retained "to clamp
  later" (`PET_RULES.md` A5.5).
- New-Pet creation (tests / any future path) starts from `XP = 0`,
  `Level = 1`.

### 5.4 Persistence and migration

- Column shape is owned by `DATABASE.md` A1/A3 - nothing invented:
  `XP integer NOT NULL DEFAULT 0` + `XP >= 0 AND XP <= 4900` check.
- Narrow scope: the Pet table only. If the migration tool reports unrelated
  pending model changes, STOP - do not bundle them (`AGENTS.md` A16).
- Existing rows: column default gives `XP = 0` (`DATABASE.md` A3:689;
  TASK-066 F20). No document defines a Level->XP conversion, and none may be
  invented. Stored `Level` values are preserved untouched (same treatment
  TASK-065 applied to Player rows).
- Verification commands are mandatory (A9), including
  `has-pending-model-changes`.

### 5.5 Reward integration and exactly-once

- The grant lives inside the **existing** `PersistTerminalResultAsync` and
  is gated by the **existing** `firstDurableWrite` flag produced by
  `BattleResultRepository.AddAsync` (primary key = BattleId is the guard;
  `DATABASE.md` A1 sourcing item 1). A retried terminal persistence for the
  same battle reports `false` and must not award again.
- No new idempotency table, ledger, distributed lock, event-sourcing layer,
  XP transaction service, or second transaction boundary. The single
  `SaveChangesAsync` (`BattleResultRepository.cs:87`) is retained as-is.
- **Ordering wrinkle (read before editing `:292`/`:306`/`:319`):** today the
  row is built with `RewardSummary = {}` (`:292`) and persisted (`:306`)
  *before* the grants run (`:319`), while the populated summary contains
  post-grant values. Reconcile this with a **minimal, deliberate sequencing
  change inside this same method** that preserves all three of:
  (a) the primary-key insert remains the exactly-once source of truth;
  (b) a retry neither re-awards nor rewrites an already-durable row's stored
  result (population happens only in the first-writer branch of the first
  call);
  (c) no second reward pipeline, service, or transaction architecture.
  If these three cannot all hold, or if any authoritative document conflicts
  with the minimal sequencing - STOP and report; do not redesign
  `BattleResult` persistence (TASK-050 owns that contract).
- Failure semantics mirror the Player grant exactly (post-first-write grants
  persist through their repository boundary). Do not invent stronger
  atomicity than the Player track has; if a document appears to require it,
  STOP and report.

### 5.6 RewardSummary - the delegated representation decision

- **Who decides:** this task. `DATABASE.md` A1 item 2 explicitly defers the
  Pet-track member list to the implementation task, projecting semantics
  already fixed by `PET_RULES.md` A5.3-A5.5. TASK-066 classified this as a
  delegated representation decision - NOT a missing contract (finding F11).
- **What is frozen already:** the four Player-track member names
  (`playerXpGained`, `newPlayerXp`, `playerLeveledUp`, `newPlayerLevel`,
  item 1); no placeholder members (item 3); `rewards` returns the stored
  value unchanged (`API_CONTRACTS.md` A4 note 1); both Outcomes carry the
  member (item 5).
- **What this task must do:** define the smallest Pet-track member set that
  conveys the decided semantics (recipient grant amount, resulting XP,
  resulting Level - `PET_RULES.md` A5.3-A5.5), populate the summary with
  both tracks where members exist, and record the chosen member list in the
  Final Report. TASK-059 A6.4 names are examples only, not contract.
- **Known tension to resolve from the documents at execution:** A1 item 1
  documents `playerXpGained ... 0 on a loss`, while A1 item 5 states a
  `"defeat"` battle carries no line items. Derive the correct defeat-shape
  answer from the documents; if two authoritative readings genuinely
  conflict, STOP per `AGENTS.md` A4 - identify both sources, propose the
  smallest correction, and wait for approval. Do not silently pick the
  convenient shape.
- Updating the existing tests that assert the pre-landing `{}` staging value
  is **expected** (item 4: `{}` is in force "until the implementation task
  lands") - update those assertions to the landed representation; do not
  delete coverage. Editing those *tests* is not "modifying a completed
  task"; editing any file under `tasks/` is.

### 5.7 Repository boundary

- Exactly one new member on `IPetRepository`/`PetRepository` - a narrow
  progression write (the analog of `IPlayerRepository.SaveProgressionAsync`,
  `IPlayerRepository.cs:99`). `AddAsync`/`GetDefinitionAsync`/
  `GetByIdAsync` keep their current contracts.
- No new repository interface, unit-of-work, generic pattern, bulk
  recompute, or speculative query. PetRepository continues to derive no
  progression value (`PetRepository.cs:13-18`).

### 5.8 Forbidden identifiers and architectures

Absent from `src/` and `tests/` after this task - as live code, registration,
mapping, or helper:

```text
PetLevelMultiplier      PetLevelDerivation     PetLevelService
PetLevelManager         ProgressionManager     XPService
UniversalProgressionService
GrantExperienceToAllPets()   RecalculateAllPetLevels()   SyncPetProgression()
```

Also forbidden: a second reward pipeline; a second Pet.Level source; a
shared Player/Pet XP abstraction; XP ledger / idempotency table / distributed
lock / event sourcing / new transaction service; any new Redis key, SignalR
event, or API field.

---

## 6. Player / Pet Track Separation

- The two grants are independent (`PET_RULES.md` A5.1 item 5; ADR-016):
  the Pet grant reads/writes no `Player` member, and the Player grant
  (`BattleResultService.cs:68-69`, `:377-380`) reads/writes no `Pet` member.
  Both calls remain in the same first-write branch, side by side.
- Constants are duplicated per track, never shared: Pet reward `100`,
  Pet curve `100`, Pet cap `4900` - none derived from `Player.*`.
- `Player.cs`, `PlayerConfiguration.cs`, the `AddPlayerXp` migration, and
  the Player test suites are read-only for behavior. If completing Pet XP
  appears to require changing any of them - STOP (A12).
- Cap rules are not transposed: the Pet `4900` cap never appears on Player
  (uncapped), and Player's uncapped behavior never applies to Pet.

---

## 7. Affected Files & Areas

```text
MODIFY (Domain)
[x] src/backend/GameServer.Domain/Pets/Pet.cs
        add `XP` + documented constants; add the XP -> Level pure function;
        add the grant method (cap on write); refresh the stale "no XP"
        comments (keep the retirement notes)

MODIFY (Persistence)
[x] src/backend/GameServer.Infrastructure/Postgres/Configurations/PetConfiguration.cs
        map `XP` required, HasDefaultValue(0), [0,4900] check;
        retain CK_Pet_Level_Range / CK_Pet_Star_Range; refresh :35 comment

MODIFY (Application / Infrastructure)
[x] src/backend/GameServer.Application/Pets/IPetRepository.cs
        one minimal progression-write member
[x] src/backend/GameServer.Infrastructure/Postgres/Repositories/PetRepository.cs
        its EF implementation
[x] src/backend/GameServer.Application/Battle/BattleResultService.cs
        Pet grant beside GrantPlayerXpAsync (same first-write gate);
        RewardSummary population per A5.6 (minimal sequencing change per A5.5);
        constructor takes the Pet boundary

ADD (migration, tool-generated)
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/<ts>_AddPetXp.cs (+ .Designer.cs)
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs (regenerated)

MODIFY (tests - update assertions; never delete coverage)
[x] tests/backend/GameServer.Infrastructure.Tests/PetPersistenceTests.cs
        :57-67 must now ALLOW `XP` while still forbidding Evolution and the
        other retired/forbidden members; refresh field-set comments
[x] tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs
        add the in-memory Pet boundary double (mirror InMemoryPlayerRepository)
[x] tests/backend/GameServer.Application.Tests/BattleResultServiceTests.cs
        harness composition + RewardSummary assertions (:150, :273-292)
[x] tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs
        :175 RewardSummary assertion

ADD (tests)
[x] tests/backend/GameServer.Domain.Tests/PetXpProgressionTests.cs
[x] tests/backend/GameServer.Application.Tests/PetXpBattleRewardTests.cs
[x] tests/backend/GameServer.Infrastructure.Tests/PetProgressionPersistenceTests.cs
[x] tests/backend/GameServer.Infrastructure.Tests/PetXpSchemaTests.cs

MODIFY (tests - only if the landed representation changes them)
[x] tests/backend/GameServer.Infrastructure.Tests/BattleResultPersistenceTests.cs (:254, :385-403)
[x] tests/backend/GameServer.Api.Tests/BattleResultSmokeTest.cs (:221)
[x] tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs (:289 comment)

READ ONLY (verify unchanged; do NOT edit)
[ ] docs/**   [ ] src/frontend/**   [ ] tasks/** (esp. TASK-059...TASK-066)
[ ] src/backend/GameServer.Domain/Players/**        (Player track)
[ ] src/backend/GameServer.Application/Players/**
[ ] src/backend/GameServer.Infrastructure/Postgres/Repositories/BattleResultRepository.cs
        (the first-write guard is documented sufficient - TASK-066 F10;
         if it appears to require a change, STOP per A12)
[ ] src/backend/GameServer.Api/**                   (expected: no change -
        TASK-066 F17 found no documented contract requiring it)
[ ] src/backend/GameServer.Domain/Battle/**         (BattleState/PetState)
[ ] any ADR / any migration history file
```

---

## 8. Acceptance Criteria

Exactly 31 binary, objectively verifiable conditions. The template-standard
conditions are represented by items 27 (authoritative rules), 29 (tests
pass), and 31 (validation depth + quality review).

**A. Domain model and formula (4)**
- [ ] 1. `Pet` has a persisted `XP` member (`int`, column NOT NULL, initial
      `0`) owned by the Pet instance - not by `PetDefinition` and not by
      `Player` (`PET_RULES.md` A5.1; `DATABASE.md` A1/A3)
- [ ] 2. A newly created Pet has `XP = 0` and `Level = 1`; `Pet.Level`
      remains a **persisted** column re-derived by
      `min(floor(Pet.XP / 100) + 1, 50)` and stays within `[1, 50]`
      (`CK_Pet_Level_Range` retained)
- [ ] 3. Hard cap honoured on write: stored `Pet.XP` never exceeds `4900`
      (a grant crossing the boundary leaves exactly `4900`; a grant at
      `4900` is neither awarded nor stored); no overflow, hidden, prestige,
      or post-cap XP; negative XP is impossible (`[0,4900]` check)
      (`PET_RULES.md` A5.5)
- [ ] 4. `Pet.Level` has exactly ONE runtime source - `Pet.XP`; no second
      writer, no Player-derived path, no temporary/dormant derivation

**B. Retired mechanism stays retired (2)**
- [ ] 5. `PetLevelMultiplier`, `PetLevelDerivation`, `PetLevelService`
      appear nowhere as live code in `src/` or `tests/` (immutable migration
      history and explanatory comments excluded)
- [ ] 6. None of the forbidden new identifiers exist (A5.8): no
      `PetLevelManager`, `ProgressionManager`, `XPService`,
      `UniversalProgressionService`, `GrantExperienceToAllPets()`,
      `RecalculateAllPetLevels()`, `SyncPetProgression()`

**C. Battle reward (5)**
- [ ] 7. `BattleWon` awards exactly `+100` Pet XP to the active combat Pet
- [ ] 8. `BattleLost` awards exactly `+0` Pet XP (Level unchanged)
- [ ] 9. Exactly one recipient per battle - the already-resolved
      `state.PetState.PetId.Value` (`PET_RULES.md` A5.3 item 1); every other
      owned Pet is untouched (+0); no passive/shared/party/account-wide XP
- [ ] 10. The grant runs inside the existing
      `BattleResultService.PersistTerminalResultAsync` beside
      `GrantPlayerXpAsync`, gated by the same first-write flag; no second
      reward pipeline, service, flow, or event
- [ ] 11. Exactly-once: a retried/reconciled terminal persistence for the
      same battle does not award Pet XP twice; no new idempotency mechanism
      was introduced

**D. Track independence (2)**
- [ ] 12. Player track unchanged: Player XP values, `+100`/`+0`, uncapped
      rule, Level formula, and TASK-059/TASK-065 outputs behave exactly as
      before; `Player` files carry no behavioral edit
- [ ] 13. The pools are independent: the Pet grant reads/writes no
      `Player.XP`/`Player.Level` value and the Player grant reads/writes no
      `Pet.XP`/`Pet.Level` value (`PET_RULES.md` A5.1 item 5; ADR-016)

**E. Persistence and migration (5)**
- [ ] 14. EF mapping: `Pet.XP` required with `HasDefaultValue(0)` and a
      `[0, 4900]` check constraint (`DATABASE.md` A3); `CK_Pet_Level_Range`
      and `CK_Pet_Star_Range` retained unchanged
- [ ] 15. Exactly ONE new migration; it touches only the `Pet` table (XP
      column + range constraint); no historical migration edited; no other
      table, Redis, or SignalR artifact in it
- [ ] 16. Existing Pet rows receive `XP = 0` via the column default; no
      Level->XP backfill or conversion invented; stored `Level` values
      preserved
- [ ] 17. `GameDbContextModelSnapshot.cs` regenerated by the tool;
      `dotnet ef database update` applies the migration cleanly
- [ ] 18. `dotnet ef migrations has-pending-model-changes` reports no
      pending changes

**F. RewardSummary (2)**
- [ ] 19. The Pet-track member list is defined as the smallest
      representation of `PET_RULES.md` A5.3-A5.5 (delegated by `DATABASE.md`
      A1 item 2) and populated together with the four frozen Player-track
      members; `EmptyRewardSummary` is no longer the value on the path where
      members exist; no placeholder members; the chosen member list is
      recorded in the Final Report
- [ ] 20. The populated `RewardSummary` conforms to `DATABASE.md` A1 items
      1-5 (win shape, defeat shape, both Outcomes present); any tension
      between documents was resolved per `AGENTS.md` A4 and recorded in the
      report - or the task STOPPED rather than guessing

**G. Repository and transaction boundary (2)**
- [ ] 21. `IPetRepository`/`PetRepository` gain exactly one minimal
      progression-write member; no speculative/unused members; no new
      interface or generic pattern
- [ ] 22. No new idempotency table, ledger, distributed lock, event-sourcing
      system, XP transaction service, or second transaction boundary (the
      single `SaveChangesAsync` is retained)

**H. Boundaries (5)**
- [ ] 23. No frontend change (React / Phaser / GameRuntime / BattleScene /
      SignalRService)
- [ ] 24. No Redis `BattleState` or SignalR protocol change; no new event,
      endpoint, or wire field
- [ ] 25. No Match-3 / Board / Gems / Swap / Cascade / Combat / Boss /
      Card / Relic / Passive / Skill / Evolution / Tier / Star / Gacha
      change
- [ ] 26. No `docs/` file modified, no ADR created, no gameplay rule
      changed; no task file under `tasks/` modified (esp.
      TASK-059...TASK-066)
- [ ] 27. No authoritative rule or contract violated (`AGENTS.md` A10 /
      ADR-001 server authority; scope adherence per `MVP_SCOPE.md` A1)

**I. Validation (4)**
- [ ] 28. `dotnet build src/backend/GameServer.sln` passes with 0 errors
- [ ] 29. `dotnet test src/backend/GameServer.sln` fully passes; actual
      per-layer counts recorded against the recorded baseline
      (1557 passed / 0 failed per TASK-065); no pre-existing failure
      ignored, and no test weakened to make the implementation pass
- [ ] 30. Tests cover all eight gaps from TASK-066 F19 (initial values;
      formula boundaries 0/99/100/4899/4900; `+100`/`+0`; inactive Pet +0;
      cap behaviour; Player/Pet independence; exactly-once across retry;
      applied-schema column/default/constraint) and
      `PetPersistenceTests.cs:57-67` is updated (Evolution still forbidden),
      not deleted
- [ ] 31. All relevant tests pass at the required validation depth
      (`.ai/workflow/core/validation.md` A2) and the quality review
      checklist passes (`.ai/workflow/quality/review.md` A1)

---

## 9. Testing Requirements

Use the existing four test projects (Domain, Application, Infrastructure,
Api). Do not create a new framework. Mirror the TASK-065 suite shapes.

### Required Verification

```text
[ ] Domain tests (new: PetXpProgressionTests.cs)
        initial XP 0 / Level 1
        XP -> Level boundaries: 0->1, 99->1, 100->2, 4899->49, 4900->50
        grant behaviour: +100 on win, +0 on loss, Level re-derived
        cap: XP stays <= 4900 (crossing grant lands on 4900; grant AT 4900
             not awarded/stored); no negative XP
        independence: Pet formula/grant touch no Player member, and vice
             versa (mirror PlayerXpProgressionTests' cross-track cases)
[ ] Application tests (new: PetXpBattleRewardTests.cs; extend
        BattleResultServiceTests / BattleResultTerminalFlowTests / doubles)
        BattleWon -> active Pet +100; BattleLost -> active Pet +0
        exactly one recipient: a second owned Pet is untouched (+0)
        retry/reconcile of the SAME battle does NOT award a second time
        (also after a failed-then-recovered durable write)
        Player grant still exactly-once and numerically unchanged
        RewardSummary: populated representation on a win (both tracks),
             defeat shape per DATABASE.md A1 item 5, both Outcomes present
[ ] Persistence tests (new: PetProgressionPersistenceTests.cs; modify
        PetPersistenceTests.cs)
        Pet.XP defaults to 0, NOT NULL, persists across reload
        progression write boundary round-trip (XP + re-derived Level)
        field-set: XP allowed; Evolution and other forbidden members still
             rejected (update :57-67, do not delete)
[ ] Schema tests (new: PetXpSchemaTests.cs - applied PostgreSQL)
        column exists: integer, NOT NULL, DEFAULT 0
        check constraint [0,4900] present; CK_Pet_Level_Range and
             CK_Pet_Star_Range unchanged; no XP on any other table
        migration history contains exactly the new Pet XP migration
[ ] Api tests (update where "{}" is asserted once the representation lands)
        rewards member returns the stored RewardSummary unchanged
        (API_CONTRACTS.md A4 note 1); defeat/win shapes per DATABASE.md A1
```

### Commands (repository conventions)

```text
dotnet build src/backend/GameServer.sln
dotnet test  src/backend/GameServer.sln

dotnet ef migrations add AddPetXp --project src/backend/GameServer.Infrastructure --startup-project src/backend/GameServer.Api
dotnet ef database update          --project src/backend/GameServer.Infrastructure --startup-project src/backend/GameServer.Api
dotnet ef migrations has-pending-model-changes --project src/backend/GameServer.Infrastructure --startup-project src/backend/GameServer.Api
```

Design-time configuration must be supplied through the documented
non-tracked providers (`ADR-015` D10 - never a tracked file). The local
development database is the compose service on `localhost:5433`
(`dcacti_db`).

### Key Edge Cases

- `4900` = 49 `BattleWon` rewards (the exact Level-50 / XP-max boundary);
  `4901+` is unrepresentable - a crossing grant must store exactly `4900`.
- `BattleLost` must not change `Level`, and must not touch `Player` state.
- The recipient Pet is fixed at battle start
  (`BattleStartService.cs:300`); a battle must credit the Pet that fought,
  not whichever Pet is active later.
- Retry after a **successful** durable write must not re-award (Pet or
  Player), and must not rewrite the stored `RewardSummary` of an
  already-durable row (A5.5 (b)).
- Retry after a **failed** write (the absorbed-exception path,
  `BattleStateService` ~:1345) awards once when the write eventually
  succeeds.
- Defeat `RewardSummary` shape per `DATABASE.md` A1 item 5 (and see the
  A5.6 tension - resolve from docs or STOP).
- `PetPersistenceTests` forbidden-member list must still reject Evolution
  (`ADR-012` items 5-6) after `XP` becomes legal.

---

## 10. Migration Notes

- The documented contract is `XP int, NOT NULL, default 0`
  (`DATABASE.md` A1/A3); existing rows receive `0` via the column default,
  which the `A5.4` formula maps to `Level 1`. No document defines a
  Level->XP conversion, and **none may be invented** (TASK-066 F20).
  Existing stored `Level` values are preserved untouched.
- If execution concludes a backfill/conversion is needed for consistency,
  that is a genuine ambiguity - STOP (`AGENTS.md` A10/A12) and report it
  rather than choosing a rule.
- Narrow scope: Pet table only. Verify **before** adding that the model has
  no unrelated pending changes; if the generator reports any, STOP - do not
  bundle (`AGENTS.md` A16).
- Never edit historical migrations (incl. `AddPetPersistence`,
  `DropPetLevelMultiplier`, `AddPlayerXp`).

---

## 11. Frontend / Redis / SignalR

- **Frontend:** no authoritative contract exposes Pet XP (`API_CONTRACTS.md`
  A5 `/api/pets` returns level; no Pet XP wire field required;
  TASK-066 F17). Frontend remains out of scope - do not add Pet XP UI, do
  not modify GameRuntime, BattleScene, or SignalRService.
- **Redis:** Pet XP is persistent per-instance progression, not active
  battle state (`PET_RULES.md` A5.1 item 6; `DATABASE.md` A3; `REDIS_STATE.md`
  :310-324 - no progression key). Do not add a `BattleState` Pet XP member
  or any new key.
- **SignalR:** no new event exists or is required (`GAME_EVENTS.md` A2
  defers the member list only). Preserve the protocol unchanged.

If execution finds an authoritative document that *does* require an API,
event, Redis, or SignalR exposure - do not invent it; report the requirement
and STOP (`AGENTS.md` A12).

---

## 12. Stop Conditions

> **A STOP CONDITION HAS FIRED. See `§12.1` immediately below for the
> report and `§12.2` for the exact execution status. No code was written.**

### 12.1 STOP CONDITION REPORT (fired during execution)

```text
STOP CONDITION

Problem:
    The task-manifest bullet that was supposed to be this task's single
    delegated representation decision — DATABASE.md §1 item 2, the
    `RewardSummary` Pet-track member list — is not the only thing in
    DATABASE.md §1 that is undetermined. The SAME subsection's item 1
    (the Player-track members, presented as "decided, and therefore
    contracted here") pins `playerXpGained ... 0 on a loss` — i.e. a
    defeat row DOES carry Player-track line items — while item 5 of the
    same subsection states a "defeat" battle "carries no line items".
    The two items are in the same list, in the same section, in the same
    document, and neither is marked superseded. This is the tension
    TASK-067 §5.6 and its Stop Conditions flagged, and it CANNOT be
    resolved from the documents: the only textual authority for the
    defeat shape is the pair that contradicts itself, and no third
    document arbitrates between them (both API_CONTRACTS.md §4 note 1 and
    GAME_EVENTS.md §2 item 2 explicitly DELEGATE the shape to DATABASE.md
    §1 rather than settling it).

    Consequence for this task: acceptance criterion 20 ("The populated
    `RewardSummary` conforms to `DATABASE.md` item 1-5 ... or the task
    STOPPED rather than guessing") cannot be satisfied by implementing.
    Item 1 and item 5 cannot both hold for a defeat row, and choosing
    either reading is "resolving a contradiction by preference" — which
    TASK-067 §9 and AGENTS.md §4 forbid.

    Note the task's own §5.6 already anticipated exactly this and
    instructed: "if two authoritative readings genuinely conflict, STOP
    per AGENTS.md §4 - identify both sources, propose the smallest
    correction, and wait for approval. Do not silently pick the
    convenient shape." That instruction is what is being executed here.

Authoritative sources:
    CONTRADICTION (both inside docs/02-technical/DATABASE.md §1,
    "Reward semantics for `RewardSummary`"):
      - Side A - item 1 (:551-571), the frozen Player-track member list:
            playerXpGained  (int) - Player XP granted by this battle:
                                    100 on a win, 0 on a loss
                                  (:563-564)
            newPlayerXp     (int) - Player.XP after applying the grant
            playerLeveledUp (bool) - whether Player.Level changed
            newPlayerLevel  (int) - Player.Level after applying the grant
        This block is explicitly labelled "Player track - decided, and
        therefore contracted here" / "These four members are the complete
        Player-track contract" (:551, :570-571). A member defined as
        "0 on a loss" is by construction PRESENT on a loss - it has a
        defined, asserted value there. Presence of all four on BOTH
        outcomes is reinforced by item 5's own first clause
        (":612-613"): "`RewardSummary` is present for both `Outcome`
        values".

      - Side B - item 5 (:612-616), the same list:
            "Outcome semantics are unchanged. `RewardSummary` is present
             for both `Outcome` values (`§1` entity block,
             `API_CONTRACTS.md` §4 note 1); a `"defeat"` battle carries no
             line items ..."
        "carries no line items" on a defeat is only satisfiable by
        OMITTING the Player-track members (and any Pet-track members) from
        a defeat row - which contradicts item 1's per-outcome value table
        for `playerXpGained` / `newPlayerXp` / `playerLeveledUp` /
        `newPlayerLevel`.

    DELEGATING (neither settles the question; both hand it back to the
    same self-contradictory location):
      - docs/02-technical/API_CONTRACTS.md §4 note 1 (:547-559):
        "`rewards` is always present ... Its value is
         `BattleResult.RewardSummary` exactly as `DATABASE.md` §1
         documents it (staging value `{}` with no reward line items; a
         `"defeat"` outcome carries no line items). The `RewardSummary`
         member list is owned by `DATABASE.md` §1 ..." (:549-552)
        It restates BOTH the staging value AND the defeat shape by
        reference, so it inherits, and cannot break, the contradiction.
      - docs/02-technical/GAME_EVENTS.md §2 `BattleWon`/`BattleLost`
        item 2 (:464-473): "The reward summary in this payload is
        `RewardSummary` - the one contract owned by `DATABASE.md` §1.
        This document does not define its members ..." (:464-466).
        Explicitly non-defining.
      - docs/02-technical/DATABASE.md §1 item 4 (:604-610): fixes only
        the STAGING value (`{}`) "until the implementation task lands",
        and says nothing about the landed defeat shape.
      - docs/03-decisions/ADR/ADR-016 Consequences (:212-215): calls the
        Pet member list "a representation decision, not an open gameplay
        decision" and likewise does not address the defeat shape.
      - tasks/backlog/TASK-066-...md F11 (:942-973): classifies ONLY the
        Pet member list as the delegated representation decision, and
        records that "the documentation is not silent: it directs when
        and by whom the decision is made." That finding correctly
        covers DATABASE.md §1 item 2; it does NOT cover the item 1 vs
        item 5 defeat-shape conflict, which is a separate question F11
        never examined. The READY verdict therefore does not clear this.

    NOT AUTHORITATIVE (do not settle it either):
      - tasks/backlog/TASK-059-...md §6.4 (:410-441) supplies
        illustrative names only and states they are "ONLY an example -
        not the final set". TASK-067 §5.6 and TASK-066 F11 both confirm
        TASK-059's names are not contract. A task file cannot override a
        technical document anyway (AGENTS.md §2 precedence).

Why they conflict:
    The two items describe the SAME artifact (the `RewardSummary` document
    written for a defeat) with mutually exclusive member arity:
      - item 1 asserts a defeat row CONTAINS `playerXpGained = 0`,
        `newPlayerXp`, `playerLeveledUp = false`, `newPlayerLevel`.
      - item 5 asserts a defeat row CONTAINS no line items.
    "No line items" versus "four members each carrying a defined value on
    a loss" is not a wording nuance that interpretation can reconcile:
    the members either appear in the document or they do not. There is no
    reading under which both statements are true of the same row.

    The conflict is intrinsic to a single subsection, so AGENTS.md §2's
    precedence ordering cannot break the tie either - there is no
    "more specific" document to appeal to, because the only document that
    owns the contract is the one that contradicts itself, and every other
    document that touches it defers back to it. Per AGENTS.md §4 (and §20
    "Rule conflict" / "Data contract conflict") this must be reported and
    left unresolved pending a human decision; per TASK-067 §5.6/§9 and
    §12 it must NOT be settled by preference.

    Why this blocks the whole task rather than just the summary:
    TASK-067 §5.2 step 12 makes populating `RewardSummary` (replacing the
    `EmptyRewardSummary` `{}` staging value, :93/:292) an In-Scope
    deliverable, and acceptance criteria 19/20 plus the §9 testing
    requirements make its shape a completion condition. The sequencing
    constraint in §5.5 also depends on the answer: whether the populated
    summary must be written for BOTH outcomes or only for a victory
    determines how the row must be built relative to the grant
    (:292/:306/:319), because on the "no line items on defeat" reading
    the defeat row has nothing to populate and the sequencing wrinkle
    §5.5 asks to reconcile does not arise at all - whereas on the item 1
    reading it does. Implementing the Pet XP grant while leaving the
    summary decision open is not available either: §5.5 requires the
    grant and the population to be sequenced together inside the same
    first-write branch, and shipping the domain/persistence half while
    deliberately leaving the summary at `{}` would contradict item 4's
    "until the implementation task lands" and fail criteria 19/20.

    Explicitly NOT the problem: this is NOT the Pet XP contract itself.
    `PET_RULES.md` §5.1-§5.5 (initial 0 / Level 1, formula
    min(floor(XP/100)+1, 50), Level range [1,50], BattleWon +100 /
    BattleLost +0, active-combat-Pet-only recipient, inactive Pets +0,
    4900 hard cap with no overflow) is complete, self-consistent and
    unambiguous, as is DATABASE.md §3's Pet column/constraint block and
    the Player track. Every other TASK-067 acceptance criterion (1-18,
    21-31) is implementable as written. ONLY the `RewardSummary`
    representation is blocked.

Smallest documentation/decision follow-up:
    A single human decision, recorded as the smallest possible edit to
    docs/02-technical/DATABASE.md §1 item 5 (the item that conflicts with
    the frozen item 1 list), choosing ONE of:

      (A) Line items are present for BOTH outcomes - item 1 governs, and
          item 5's "carries no line items" describes only that a defeat
          grants no POSITIVE reward (all four Player-track members are
          still written, each holding its documented 0/false value).
          Suggested minimal wording: replace "a `"defeat"` battle carries
          no line items" with "a `"defeat"` battle grants no reward, so
          its line items hold the documented `+0` / unchanged values".
          Then TASK-067 populates both outcomes, rejecting `{}` for a
          landed implementation.

      (B) A defeat row is the empty object `{}` - item 5 governs, and
          item 1's "100 on a win, 0 on a loss" describes the RULE's grant
          amount rather than a persisted member's presence on a defeat.
          Suggested minimal wording: amend item 1 to state the four
          Player-track members are written only when the battle grants
          (i.e. on a victory), and that a defeat's `RewardSummary` is
          `{}`. Then TASK-067 populates the victory path only and leaves
          the defeat path at `{}` - which also removes the §5.5
          sequencing wrinkle.

    Either choice is a one-clause correction to existing wording; NEITHER
    adds a member, changes a value (`+100`/`+0`/`{}` are unaffected), or
    touches the Pet XP contract. This task did NOT make that edit: it is
    not authorized to modify `docs/` (TASK-067 §4 Out of Scope, §5.6,
    §13) and doing so would itself be the "silently alter the contract"
    failure AGENTS.md §4 forbids.

    Recommended follow-up (named, NOT created by this task):
      TASK-068 - Resolve the `RewardSummary` defeat-shape contradiction in
      `DATABASE.md` §1 item 5 (DOCUMENTATION; Review Agent; single human
      decision A or B; no code). TASK-067 then resumes BLOCKED -> IN
      PROGRESS with §5.2 step 12 unambiguous.

Waiting for:
    A human decision selecting (A) or (B) above (or a third explicit
    shape), applied as the minimal docs/02-technical/DATABASE.md §1 edit.
```

### 12.2 Execution status at the STOP

```text
Code changes made:        NONE
docs/ changes made:       NONE
Migration created:        NONE
Tests added:              NONE
Task files changed:       THIS file only (status field + this report,
                          per TASK_LIFECYCLE.md §3 "The STOP CONDITION
                          report is written in the task's Stop Conditions
                          section")
Lifecycle transition:     BACKLOG/READY -> BLOCKED
                          (file moved tasks/backlog/ -> tasks/blocked/,
                          TASK_LIFECYCLE.md §3 BLOCKED / §4)

Pre-change verification (run before stopping, on the unmodified tree):
    dotnet build src/backend/GameServer.sln
        -> PASS, 0 Errors, 6 Warnings (pre-existing MSB3277 EF Core
           version-unification warnings in GameServer.Api.Tests.csproj)
    dotnet test src/backend/GameServer.sln --no-build
        -> PASS  1557 passed / 0 failed / 0 skipped
             GameServer.Domain.Tests          921
             GameServer.Application.Tests     267
             GameServer.Infrastructure.Tests  182
             GameServer.Api.Tests             187
        This reproduces TASK-065's recorded baseline (1557/0/0) exactly,
        so the environment is confirmed healthy and the STOP is not
        caused by a broken or drifted tree.

Pet XP contract verification (read-only, against the unmodified tree):
    The frozen contract is implementable as written; this was confirmed
    before stopping so the STOP is scoped precisely to RewardSummary:
      - PET_RULES.md §5.1-§5.5 complete and internally consistent
        (initial 0 / Level 1, formula, Level range [1,50],
         BattleWon +100 / BattleLost +0, active-Pet-only recipient,
         inactive Pets +0, 4900 hard cap, no overflow/prestige).
      - DATABASE.md §3:685-695 fixes the Pet column and constraints.
      - Pet.cs has no XP member; PetConfiguration.cs:35 "There is still
        no XP column on this table"; no Pet.XP migration exists -> the
        feature is genuinely absent, so the task is not obsolete.
      - PetLevelMultiplier / PetLevelDerivation / PetLevelService remain
        absent from live code (TASK-066 F1 re-confirmed).
      - state.PetState.PetId.Value is available and validated at
        BattleResultService.cs:218/:223 as the recipient.
      - The first-durable-write guard (BattleResultRepository.cs:82/:91)
        and the single SaveChangesAsync (:87) are intact.
    No STOP condition other than the RewardSummary one fired.
```

### 12.3 Task-specific conditions (unchanged reference)

Universal stop conditions (`AGENTS.md` A20) always apply. Task-specific
conditions - on any of these, STOP, write the
`STOP CONDITION` report (problem / sources / conflict / proposed smallest
follow-up task - named, not created / waiting-for), change nothing else, and
do not invent a resolution:

- **A contract value cannot be implemented as written** (initial values,
  range, `+100`/`+0`, recipient, formula, `4900` cap) - the implementation
  differs from `PET_RULES.md` A5 - STOP (`AGENTS.md` A7).
- **Completing Pet XP would require changing the Player track** (values,
  reward, uncapped rule, formula, TASK-059/TASK-065 outputs) - STOP
  (`AGENTS.md` A16).
- **`PetLevelMultiplier` (or `PetLevelDerivation`/`PetLevelService`) would
  have to be reintroduced or reinterpreted** - STOP
  (`PET_RULES.md` A5.6 item 1; ADR-016 item 13).
- **A second `Pet.Level` runtime source** (Player-derived, definition-
  derived, cached, or temporary writer) would be required - STOP.
- **A new or changed gameplay rule is needed** (new reward case, party-wide
  XP, prestige, post-50 system, Evolution interaction) - STOP
  (`AGENTS.md` A7; `PET_RULES.md` A5.3 item 5, A5.5 item 3, A5.6 item 4).
- **The `RewardSummary` member list conflicts with an authoritative
  document, or the defeat-shape tension (A1 item 1 vs item 5) cannot be
  resolved from the documents** - STOP (`AGENTS.md` A4); identify both
  sources, propose the smallest correction, wait for approval.
- **Exactly-once cannot be guaranteed within the existing boundary**
  (primary-key guard + first-write flag), or the A5.5 sequencing
  constraints (a)-(c) cannot all hold - STOP; do not add locks, ledgers, or
  a second transaction architecture (`AGENTS.md` A18).
- **A second reward pipeline / battle-resolution path appears necessary** -
  STOP.
- **A new Redis key, `BattleState` member, SignalR event, API endpoint, or
  wire field appears required** - STOP (`AGENTS.md` A18); do not invent it.
- **Pet XP ownership or the `[0,4900]` / `[1,50]` ranges would have to
  change** to proceed - STOP.
- **Any predecessor or completed task (TASK-059...TASK-066) would have to be
  modified** (their files, or a behavioral contract they froze other than
  the explicitly delegated RewardSummary population) - STOP and report it as
  a separate follow-up task.
- **The migration reports unrelated pending model changes** - STOP; do not
  bundle (`AGENTS.md` A16).
- **The TASK-064 retirement would be contradicted** (any live
  `PetLevelMultiplier` reference in code, mapping, constraint, or
  registration) - STOP.
- **Implementing this would require a new architectural decision**
  (persistence strategy, transaction model, battle-state model, authoritative
  model) - STOP (`AGENTS.md` A18); propose an ADR first.
- **Behaviour required by the implementation is not derivable from the
  Authoritative References** (invention needed) - STOP (`AGENTS.md` A7).
- **Pet XP is already implemented** when execution begins - STOP; the task
  is obsolete.
- **The task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries** - STOP and decompose.

### 12.4 Block resolution (resumed execution)

The §12.1 STOP CONDITION fired on exactly one question: the `RewardSummary`
defeat-shape contradiction inside `DATABASE.md` §1 (item 1's per-outcome Player
member values vs. item 5's "carries no line items"). No code was written at that
time.

**Resolved.** The Product Owner selected **Option A**, and the resolution was
applied to the authoritative documents by TASK-068 (DONE, see its §14 "FINAL
REPORT (RESOLVED — OPTION A)"): the Player-track member set defined in item 1
applies identically to **both** outcomes, with `playerXpGained = 100` on
`BattleWon` and `playerXpGained = 0` on `BattleLost`; a defeat serializes the same
members with `playerLeveledUp = false` and `newPlayerXp` / `newPlayerLevel`
unchanged. `DATABASE.md` §1 item 3's defeat-specific "carries no line items"
clause was removed, `API_CONTRACTS.md` §4 notes 1/3 and `GAME_EVENTS.md` §2's
payload block were synchronized, and no reward **value** changed.

**Consequence for this task.** §5.2 step 12 is now unambiguous and §5.6's "known
tension to resolve from the documents at execution" is closed by that decision
rather than by preference. The §5.5 sequencing constraints (a)–(c) were
implemented as required; see §14 Completion Evidence. Nothing else in this
manifest was rewritten — §12.1…§12.3 above are preserved verbatim as the
historical record of the block.

Lifecycle: `BLOCKED -> IN PROGRESS` (`tasks/blocked/` -> `tasks/active/`) per
`TASK_LIFECYCLE.md` §2 "BLOCKED -> IN PROGRESS: Blocking condition resolved by
human decision" — not via `BACKLOG -> READY`, which §2 lists as an *invalid*
transition (`BACKLOG -> IN PROGRESS (must pass through READY)`), and which is
unnecessary because this task had already passed READY before it was blocked
(TASK-066 verdict READY).

---

## 13. Relationship to the TASK-063 Follow-up Sequence

```text
TASK-063 (DONE)  readiness audit          -> blockers B1 (retired multiplier
                                             live), B2 (Player.XP absent)
     |
TASK-064 (DONE)  step 1 - retire PetLevelMultiplier
     |
TASK-065 (DONE)  step 2 - Player XP persistence + battle reward path
     |
TASK-066 (DONE)  step 3 - readiness RE-AUDIT -> verdict READY + the
                                                  Implementation-Task
                                                  Input Set (blueprint)
     |
TASK-067 (this)  step 4 - implement Pet XP   <- FINAL step of this sequence
```

- **This task is authorized solely by TASK-066's READY verdict.** Its
  input set (`TASK-066...md:1167-1250`) is the authoritative blueprint;
  this manifest operationalizes it.
- **Read-only predecessors:** do not modify TASK-059, TASK-061, TASK-062,
  TASK-063, TASK-064, TASK-065, or TASK-066. Their evidence is historical
  record (`AGENTS.md` A16).
- **No further task may be created from this task.** The Final Report's
  "Next Step" identifies (and does not create) whatever follows.
- **Lifecycle:** move this file `backlog -> active -> ... -> completed` per
  `TASK_LIFECYCLE.md`. The stale physical location of older DONE tasks
  (TASK-063/064 in `backlog/`) is a known report-only finding
  (TASK-066 F13) - do not repair it from this task.

---

## 14. Completion Evidence

**REACHED — Status is DONE.** The block described in §12.1 was resolved by the
TASK-068 Option A decision (§12.4), implementation then proceeded as this
manifest specifies, and every acceptance criterion below is satisfied.

### Changed Files

**Domain**
- `src/backend/GameServer.Domain/Pets/Pet.cs` — added the `XP` member
  (initial `0`) and the `MinLevel`/`MaxLevel`/`InitialLevel`/`InitialXp`/
  `XpPerLevelCurveConstant`/`BattleWonXpReward`/`BattleLostXpReward`/`MaxXp`
  constants, the `LevelForXp` pure function
  (`min(floor(XP / 100) + 1, 50)`) and `GrantBattleXp` (adds the outcome amount,
  enforces the `4900` hard cap **on write**, re-derives `Level`). Refreshed the
  stale "no XP" / "future source" comments and the class field diagram. No other
  member added; no `PetLevelMultiplier` and no Evolution member.

**Persistence**
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetConfiguration.cs`
  — maps `Pet.XP` required + `HasDefaultValue(Pet.InitialXp)` + check constraint
  `CK_Pet_XP_Range` (`"XP" >= 0 AND "XP" <= 4900`). `CK_Pet_Level_Range` and
  `CK_Pet_Star_Range` retained; the Level range now reads this track's own
  `Pet.MinLevel`/`Pet.MaxLevel` instead of `Player.*`.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260927144250_AddPetXp.cs`
  (+ `.Designer.cs`) — the one new migration: adds the `Pet.XP` column
  (`integer`, `NOT NULL`, `DEFAULT 0`) and `CK_Pet_XP_Range`. Pet table only.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs`
  — tool-regenerated.

**Application / Infrastructure**
- `src/backend/GameServer.Application/Pets/IPetRepository.cs` — exactly one new
  member, `SaveProgressionAsync(Pet, CancellationToken) : Task<bool>` (the analog
  of `IPlayerRepository.SaveProgressionAsync`). No other member changed.
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/PetRepository.cs`
  — its EF implementation (primary-key lookup, absent row reported as absence,
  writes `XP` + `Level` only).
- `src/backend/GameServer.Application/Battle/BattleResultService.cs` — the Pet
  grant beside the existing Player grant inside the existing
  `PersistTerminalResultAsync`, the populated `RewardSummary`, the eight member
  name constants, and the §5.5 sequencing change (see "Reward Flow" below). The
  constructor takes the `IPetRepository` boundary.

**Tests**
- ADD `tests/backend/GameServer.Domain.Tests/PetXpProgressionTests.cs` — 20
  tests: formula boundaries across the whole `[0, 4900]` domain, initial values,
  ranges, both outcome grants, cap/no-overflow, Level consistency, cross-track
  independence.
- ADD `tests/backend/GameServer.Application.Tests/PetXpBattleRewardTests.cs` — 20
  tests: victory `+100` / defeat `+0` to the active combat Pet, inactive Pet `+0`,
  recipient from `BattleState.PetState.PetId`, cap behaviour through the battle
  path, exactly-once across retries and a failed-then-recovered write.
- ADD `tests/backend/GameServer.Infrastructure.Tests/PetProgressionPersistenceTests.cs`
  — 9 tests: defaults, round-trip, capped value, identity preservation, absence,
  single-instance scope.
- ADD `tests/backend/GameServer.Infrastructure.Tests/PetXpSchemaTests.cs` — 7
  tests against applied PostgreSQL: column type/nullability/default, constraint
  bounds, migration history, retired column absent, `Pet` the only new XP table,
  cap round-trip + refusal of a value past the cap.
- MODIFY `tests/backend/GameServer.Infrastructure.Tests/PetPersistenceTests.cs` —
  `:57-67` (`Pet_ShouldExposeNoXpOrEvolutionField`) replaced by
  `Pet_ShouldExposeXp_ButStillNoEvolutionOrRetiredMultiplierField`, which now
  **requires** `XP` and still **forbids** Evolution and the retired multiplier.
  Field-set assertion updated; new XP mapping/constraint assertions added.
- MODIFY `tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs`
  — added the in-memory `IPetRepository` double (`InMemoryPetRepository`).
- MODIFY `tests/backend/GameServer.Application.Tests/BattleResultServiceTests.cs`,
  `BattleResultTerminalFlowTests.cs`, `PlayerXpBattleRewardTests.cs`,
  `BattleStartServiceTests.cs` — harnesses compose the Pet boundary; `{}`
  RewardSummary assertions replaced with the landed member assertions.
- MODIFY `tests/backend/GameServer.Infrastructure.Tests/BattleResultPersistenceTests.cs`
  (no change needed — its `{}` assertions are model/default assertions, verified
  still passing), `PlayerXpSchemaTests.cs` ("no XP on any other table" narrowed to
  `NOT IN ('Player','Pet')`), `tests/backend/GameServer.Api.Tests/BattleResultSmokeTest.cs`
  and `BattleResultEndpointTests.cs` (`{}` assertions replaced with the landed
  shape for both outcomes).

**Task lifecycle**
- `tasks/blocked/TASK-067-implement-pet-xp-progression.md` → `tasks/active/…` →
  `tasks/completed/…` (BLOCKED → IN PROGRESS → IN REVIEW → DONE).

### Migration
- Name: `20260927144250_AddPetXp`
- Operation: adds `Pet.XP` (`integer`, `NOT NULL`, `DEFAULT 0`) and check
  constraint `CK_Pet_XP_Range` (`"XP" >= 0 AND "XP" <= 4900`). Touches the `Pet`
  table only — no other table, no Redis, no SignalR, no API artifact.
- Applied via: `dotnet ef database update` (applied cleanly: "Applying migration
  '20260927144250_AddPetXp'. Done."; a re-run reports "The database is already up
  to date.")
- Existing-row behaviour: existing rows receive the documented column default
  `XP = 0` (`DATABASE.md` §3; `PET_RULES.md` §5.2 maps it to Level 1). Their
  stored `Level` values are **preserved untouched** — no Level → XP conversion
  exists in any document and none was invented (TASK-066 F20).
- **Snapshot drift noted (report-only):** regenerating the snapshot also synced
  two pre-existing, unrelated drifts that earlier *uncommitted* migrations had
  left behind — `DropPetLevelMultiplier` (TASK-064) and `AddPlayerXp` (TASK-065)
  were never reflected in the snapshot. Both are other tasks' intended model
  state; the generator reported no pending changes afterwards. No historical
  migration was edited.

### Validation Results
- `dotnet build src/backend/GameServer.sln` — **PASS** (0 errors, 6 warnings,
  all pre-existing: MSB3277 EF Core version unification + NU1900 offline NuGet
  audit)
- `dotnet test src/backend/GameServer.sln` — **PASS, 1634 passed / 0 failed /
  0 skipped**
  - Domain 951 · Application 294 · Infrastructure 202 · Api 187
  - Baseline before this task: 1557 passed / 0 failed / 0 skipped
    (Domain 921 · Application 267 · Infrastructure 182 · Api 187) — re-measured
    on the unmodified tree at the start of this execution, reproducing TASK-065's
    recorded baseline exactly. Net `+77` tests, no test weakened or deleted.
- `dotnet ef database update` — **applied cleanly**
- `dotnet ef migrations has-pending-model-changes` — **"No changes have been
  made to the model since the last migration."**
- Contract boundaries verified: `0→1`, `99→1`, `100→2`, `4899→49`, `4900→50`;
  `4800+100→4900`, `4850+100→4900`, `4899+100→4900`, grant at `4900 → 4900`
  (never `5000`); 49 wins → `4900`/Level 50; 120 wins never exceeds `4900`.
- Exact-once verified: the same terminal result persisted twice, five times, and
  after a failed-then-recovered write grants **once** on each track, leaves one
  row, and does not rewrite the stored `RewardSummary`.

### Final Report

```text
## Status
DONE

## Summary
Implemented the frozen Pet XP progression contract end-to-end: a persisted
Pet.XP column with its documented default and [0, 4900] hard cap, the
deterministic XP -> Pet.Level calculation with Pet.Level re-derived from this
Pet instance's own XP, and the documented BattleWon +100 / BattleLost +0 grant
applied exactly once to the active combat Pet inside the EXISTING canonical
battle-end path (BattleResultService.PersistTerminalResultAsync), together with
the RewardSummary representation DATABASE.md §1 item 2 delegated to this task.
The Player XP track is behaviorally untouched and remains an independent pool.
No gameplay rule, architecture, API, event, Redis, or SignalR contract changed.

## Files Changed
Domain       src/backend/GameServer.Domain/Pets/Pet.cs
Persistence  src/backend/GameServer.Infrastructure/Postgres/Configurations/PetConfiguration.cs
             src/backend/GameServer.Infrastructure/Postgres/Migrations/20260927144250_AddPetXp.cs (+ .Designer.cs)
             src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs
Application  src/backend/GameServer.Application/Pets/IPetRepository.cs
             src/backend/GameServer.Infrastructure/Postgres/Repositories/PetRepository.cs
             src/backend/GameServer.Application/Battle/BattleResultService.cs
Tests        ADD  Domain.Tests/PetXpProgressionTests.cs
             ADD  Application.Tests/PetXpBattleRewardTests.cs
             ADD  Infrastructure.Tests/PetProgressionPersistenceTests.cs
             ADD  Infrastructure.Tests/PetXpSchemaTests.cs
             MOD  Infrastructure.Tests/PetPersistenceTests.cs
             MOD  Infrastructure.Tests/PlayerXpSchemaTests.cs
             MOD  Application.Tests/BattleResultTestDoubles.cs
             MOD  Application.Tests/BattleResultServiceTests.cs
             MOD  Application.Tests/BattleResultTerminalFlowTests.cs
             MOD  Application.Tests/PlayerXpBattleRewardTests.cs
             MOD  Application.Tests/BattleStartServiceTests.cs
             MOD  Api.Tests/BattleResultSmokeTest.cs
             MOD  Api.Tests/BattleResultEndpointTests.cs
Task         tasks/blocked/ -> tasks/active/ -> tasks/completed/TASK-067-...md

## Pet XP Contract (as implemented)
Owner          - Pet instance                (PET_RULES.md §5.1, DATABASE.md §1)
Initial XP     - 0                           (Pet.InitialXp; PET_RULES.md §5.2)
Initial Level  - 1                           (Pet.InitialLevel; §5.2)
Level range    - [1, 50]                     (Pet.MinLevel/MaxLevel; CK_Pet_Level_Range)
Level formula  - min(floor(XP / 100) + 1, 50) (Pet.LevelForXp; §5.4)
BattleWon      - +100 Pet XP                 (Pet.BattleWonXpReward; §5.3 item 1)
BattleLost     - +0 Pet XP                   (Pet.BattleLostXpReward; §5.3 item 2)
Recipient      - active combat Pet only      (state.PetState.PetId.Value; §5.3 item 1)
Inactive Pets  - +0                          (§5.3 item 3; no passive/shared/party XP)
XP cap         - 4900 HARD                   (Pet.MaxXp; §5.5 — not awarded, not
                                              stored, no overflow, no hidden or
                                              prestige XP)
Level cap      - 50                          (§5.5 item 4)
Curve constant - 100 (formula)               (Pet.XpPerLevelCurveConstant; §5.4)

## Persistence
Entity member  Pet.XP (int, initial 0) — per-instance, never on PetDefinition
               and never on Player (PET_RULES.md §5.1 items 1-2).
EF mapping     builder.Property(pet => pet.XP).IsRequired()
                 .HasDefaultValue(Pet.InitialXp);
               check CK_Pet_XP_Range: "XP" >= 0 AND "XP" <= 4900.
               CK_Pet_Level_Range and CK_Pet_Star_Range retained unchanged.
Migration      20260927144250_AddPetXp — Pet table only.
Existing rows  receive XP = 0 by the column default; stored Level preserved; no
               Level -> XP conversion invented.

## Reward Flow
Integration point: BattleResultService.PersistTerminalResultAsync
(src/backend/GameServer.Application/Battle/BattleResultService.cs:238).
Gate: the existing first-durable-write guard (BattleResultRepository.AddAsync
returning false for an already-stored row, keyed by BattleResultId = BattleId —
DATABASE.md §1 sourcing item 1). No new idempotency mechanism.
Recipient: state.PetState.PetId.Value, already resolved and validated at the top
of the method (:218/:223 area) — exactly one recipient per battle.
Both grants sit side by side and sequential in the same method: the Player grant
(player.GrantBattleXp) and the Pet grant (pet.GrantBattleXp), each reading and
writing only its own pool.

Sequencing change (TASK-067 §5.5) and why it satisfies (a)-(c):
Because RewardSummary must record POST-grant values, the grants can no longer be
applied after the row is built. The method therefore:
  1. probes the store for the battle's own key (GetByIdAsync) and returns
     immediately when a durable result already exists — satisfying (b): a retry
     re-awards nothing, rewrites nothing, and does not even mutate the
     progression entities;
  2. computes the prospective post-grant XP/Level WITHOUT mutating the entities,
     and builds RewardSummary from those computed values;
  3. performs the durable write (unchanged, and still the exactly-once source of
     truth — satisfying (a));
  4. applies and persists both grants only inside the `firstDurableWrite` branch.
Satisfying (c): still one method, one primary-key guard, one SaveChangesAsync per
repository, no second pipeline/service/transaction architecture.

The intermediate "compute values, then mutate after the write" step is not
cosmetic: applying grants before a write that then failed would leak a partially
applied reward into the tracked entities, so a failing write would still have
advanced progression. Computing first and mutating last keeps a failed write
fully inert, which the failed-write tests assert.

## Exactly Once
Guard: BattleResultRepository.AddAsync (src/backend/GameServer.Infrastructure/
Postgres/Repositories/BattleResultRepository.cs:49-92) — an existing row for the
battle's own key returns false; the service's pre-probe keys on the same primary
key. No new mechanism.
Retry tests:
  PetXpBattleRewardTests.RepeatedTerminalPersistence_ShouldGrantPetXpOnlyOnce
  PetXpBattleRewardTests.ManyRepeatedTerminalPersistences_ShouldStillGrantPetXpOnce
  PetXpBattleRewardTests.RepeatedTerminalPersistence_ShouldNotRewriteTheStoredRewardSummary
  PetXpBattleRewardTests.RepeatedTerminalPersistence_AfterAFailedWrite_ShouldGrantPetXpOnce
  PetXpBattleRewardTests.FailedDurableWrite_ShouldGrantNoPetXp
  PetXpBattleRewardTests.PlayerGrant_ShouldRemainExactlyOnce_WhilePetXpIsGranted
  PlayerXpBattleRewardTests.* (the TASK-065 exactly-once suite, still green)

## RewardSummary
Member list defined (this task's delegated representation decision, DATABASE.md
§1 item 2):
  Player track (frozen by item 1):
    playerXpGained, newPlayerXp, playerLeveledUp, newPlayerLevel
  Pet track (defined here from PET_RULES.md §5.3-§5.5 — the smallest set that
  projects the decided semantics: recipient grant amount, resulting XP, resulting
  Level, plus the same "did it change" bit the Player track carries):
    petXpGained, newPetXp, petLeveledUp, newPetLevel
Names are declared once as constants on BattleResultService and asserted in
Application.Tests (RewardSummaryMemberNames_ShouldBeTheDocumentedContract).
No placeholder members (item 3); no item, currency, streak, bonus, or curve
value; TASK-059's illustrative names were not adopted as contract.
Win shape : { playerXpGained: 100, newPlayerXp: <post>, playerLeveledUp: <bool>,
              newPlayerLevel: <post>, petXpGained: 100, newPetXp: <post>,
              petLeveledUp: <bool>, newPetLevel: <post> }
Loss shape: { playerXpGained: 0,   newPlayerXp: <unchanged>,
              playerLeveledUp: false, newPlayerLevel: <unchanged>,
              petXpGained: 0,      newPetXp: <unchanged>,
              petLeveledUp: false, newPetLevel: <unchanged> }
Both Outcomes carry the same member set — the TASK-068 Option A resolution frozen
in DATABASE.md §1 item 5. EmptyRewardSummary ("{}") is no longer produced on the
landed path; item 4 keeps {} only as the pre-implementation staging value, which
this implementation has now replaced. A track whose row does not exist writes the
documented grant amount with JSON null for the resulting values rather than
inventing a number, and creates no row.
The §5.6 defeat-shape tension is RESOLVED (not worked around) by the TASK-068
Option A decision; see §12.4.

## Tests
Per-layer ACTUAL counts (full suite, `dotnet test`, 0 failed / 0 skipped):
    Domain          951   (baseline  921, +30)
    Application     294   (baseline  267, +27)
    Infrastructure  202   (baseline  182, +20)
    Api             187   (baseline  187,  +0)
    TOTAL          1634   (baseline 1557, +77)
Suites added: 4 (PetXpProgressionTests, PetXpBattleRewardTests,
PetProgressionPersistenceTests, PetXpSchemaTests).
Suites extended: 9 (PetPersistenceTests, PlayerXpSchemaTests,
BattleResultTestDoubles, BattleResultServiceTests,
BattleResultTerminalFlowTests, PlayerXpBattleRewardTests, BattleStartServiceTests,
BattleResultSmokeTest, BattleResultEndpointTests).
All eight TASK-066 F19 gaps are covered (initial values; formula boundaries
0/99/100/4899/4900; +100/+0; inactive Pet +0; cap behaviour; Player/Pet
independence; exactly-once across retry; applied-schema column/default/constraint).
No test was weakened, skipped, or deleted: the two updated assertions
(PetPersistenceTests' forbidden-member list, PlayerXpSchemaTests' "no XP on any
other table") were narrowed to the new documented state rather than removed, and
the `{}` rewards assertions were replaced by the landed-shape assertions.

## Build / Migration
Build:     dotnet build src/backend/GameServer.sln -> PASS, 0 errors, 6 warnings
           (all pre-existing).
Migration: 20260927144250_AddPetXp applied via `dotnet ef database update`
           ("Applying migration ... Done."); re-run reports "up to date".
Model:     dotnet ef migrations has-pending-model-changes ->
           "No changes have been made to the model since the last migration."

## Scope Verification
CONFIRMED unchanged by this task:
  Match-3, Board, Gems, Swap, Match detection, Cascade, special gems, Element,
  Combat formulas/damage, Boss, Cards, Relics, Passives, Skills, Evolution, Tier,
  Star, Gacha                                            - no change
  PetLevelMultiplier / PetLevelDerivation / PetLevelService - remain RETIRED
    (no live reference in src/; only comments, immutable migration history, and
     tests that assert their absence)
  Player XP values, +100/+0, uncapped rule, Level formula, Player persistence
    and all TASK-059/TASK-065 outputs                     - no behavioral change
    (Player.cs, PlayerConfiguration.cs, PlayerRepository.cs untouched by this task)
  Redis / BattleState / progression keys                  - no change
  SignalR protocol, battle events, REST endpoints, wire fields - no change
  Frontend (React / Phaser / GameRuntime / BattleScene / SignalRService)
    - git status -- src/frontend is EMPTY
  Second Pet.Level writer / duplicate reward pipeline / new abstraction
    - none (Pet.Level is written only via Pet.GrantBattleXp; one grant path;
       no PetLevelManager / ProgressionManager / XPService /
       UniversalProgressionService / shared Player-Pet XP base)
  Pet XP > 4900 - impossible (enforced on write, in the entity and in the
    applied schema constraint; asserted by tests)
  Pet XP from any source other than the terminal battle reward - none
    (no other production caller of GrantBattleXp or SaveProgressionAsync)
  TASK-068 and every other completed/historical task - unmodified
  TASK-069 - does not exist (glob "**/TASK-069*" -> no files)

## Documentation
No documentation changes. `git status -- docs/` shows only the pre-existing
TASK-059...TASK-068 working-tree changes; `docs/` file mtimes are all earlier
than this task's first source edit. No ADR created or amended; no gameplay rule
changed. The authoritative documents already described this final state
(DATABASE.md §1/§3, PET_RULES.md §5, ADR-016), so the implementation was written
to match them rather than the reverse (AGENTS.md §17).

## Remaining Issues
1. REPORT-ONLY, not fixed (out of scope, AGENTS.md §16): the
   GameDbContextModelSnapshot had pre-existing drift before this task — the
   uncommitted DropPetLevelMultiplier (TASK-064) and AddPlayerXp (TASK-065)
   migrations were never reflected in it. Regenerating the snapshot for this
   task's migration necessarily corrected that too. If those two migrations are
   later committed without the snapshot, the same drift returns. Suggested
   follow-up: a snapshot-consistency check (or simply committing the snapshot
   with each migration).
2. REPORT-ONLY, pre-existing, not fixed: 6 build warnings at baseline (MSB3277
   EF Core version unification in the test projects; NU1900 offline NuGet audit
   timeouts). Unrelated to this task.
3. REPORT-ONLY, pre-existing (TASK-063/064 physical location, TASK-066 F13):
   tasks/backlog/TASK-063 and TASK-064 carry DONE evidence while still living in
   backlog/. Not repaired here (AGENTS.md §16).
4. Apache-.../n/a: `BattleResultService` now performs one extra read
   (GetByIdAsync) on the terminal path. It is a primary-key lookup on the
   battle's own id, on the battle-end path where DATABASE.md §1 already permits
   PostgreSQL reads, and it is what makes the retry path not even load the
   progression rows. No hot-path (per-Swap) query was added (TDD.md §4).

## Next Step
Identify only — nothing was implemented or created.
The TASK-063 Pet XP readiness sequence is now COMPLETE (TASK-063 -> 064 -> 065 ->
066 -> 067 all DONE). The Pet XP feature itself is finished, so there is no
obvious in-sequence successor.
Candidate next steps, in order of how directly they follow from this task's own
evidence:
  (a) A small lifecycle/HYGIENE task moving TASK-063 and TASK-064 from
      tasks/backlog/ to tasks/completed/ (finding 3 above) — the only concrete
      loose end this task surfaced that is purely procedural.
  (b) A warning-cleanup task for the 6 pre-existing build warnings (finding 2).
  (c) A snapshot-consistency guard for migrations (finding 1).
None of these was created.
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (the grant amounts come
      from the server-resolved outcome; the recipient comes from server state —
      `AGENTS.md` §10 / ADR-001); the boundary accepts no client input at all
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Pet XP/Level IN; no
      scope change)
- [x] Confirmed exactly one Pet recipient per battle, server-derived
      (`state.PetState.PetId.Value`), with every other owned Pet asserted at `+0`
- [x] Confirmed `PetLevelMultiplier` was not reintroduced in any form (no live
      reference in `src/`; asserted absent in Domain, model, and applied schema)
- [x] Confirmed no prestige / paragon / evolution / post-50 / party-wide /
      account-wide progression system introduced
- [x] Confirmed no speculative abstraction (no `PetLevelManager` /
      `ProgressionManager` / `XPService` / `UniversalProgressionService` /
      shared Player-Pet XP base); one new repository member only
- [x] Confirmed the Player track is behaviorally untouched
- [x] Confirmed no Match-3 / board / gems / swap / match detection / cascade /
      combat / Boss / Card / Relic / Passive change
- [x] Confirmed no Redis `BattleState`, SignalR, or undocumented API/event change
- [x] Confirmed `Pet.XP` is the only new progression field introduced
- [x] Confirmed TASK-059…TASK-068 unmodified; TASK-068 untouched; no TASK-069
      created; nothing committed or staged

<!--
  ORIGINAL PLACEHOLDER PRESERVED BELOW AS THE MANIFEST'S SPECIFICATION of what
  this section must contain. It is superseded by the filled-in evidence above.
-->

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual. Cite file:line for every claim.
-->

### Changed Files (after resume)

**Domain**
- `src/backend/GameServer.Domain/Pets/Pet.cs` - <XP member + constants,
  formula, grant, comment refresh; no other member added>

**Persistence**
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetConfiguration.cs`
  - <mapping + constraints>
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/<ts>_AddPetXp.cs`
  (+ `.Designer.cs`) - <the one new migration>
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs`
  - <tool-regenerated>

**Application / Infrastructure**
- `src/backend/GameServer.Application/Pets/IPetRepository.cs` - <one member>
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/PetRepository.cs`
  - <implementation>
- `src/backend/GameServer.Application/Battle/BattleResultService.cs` - <Pet
  grant + RewardSummary population, with the minimal sequencing change and
  its justification (A5.5)>

**Tests**
- <add/modify list with one-line summaries>

**Task lifecycle**
- `tasks/backlog/TASK-067-implement-pet-xp-progression.md` -> `tasks/active/...`
  -> `tasks/completed/...`

### Migration
- Name: `<timestamp>_AddPetXp`
- Operation: <adds `Pet.XP` integer NOT NULL DEFAULT 0 + `[0,4900]` check;
  Pet table only - no other table, Redis, or SignalR change>
- Applied via: `dotnet ef database update` (<result>)
- Existing-row behaviour: <rows receive XP = 0 by column default; stored
  Level preserved; no backfill invented>

### Validation Results
- `dotnet build src/backend/GameServer.sln` - <PASS/FAIL (errors)>
- `dotnet test src/backend/GameServer.sln` - <PASS/FAIL, actual counts per
  layer, vs. recorded baseline 1557/0>
- `dotnet ef database update` - <result>
- `dotnet ef migrations has-pending-model-changes` - <"No changes ..." expected>

### Final Report (required format)

```text
## Status
DONE | STOPPED

## Summary
<one paragraph: what was implemented, in which existing boundary, with no
contract/architecture change>

## Files Changed
<grouped list: Domain / Persistence / Application / Tests / task file>

## Pet XP Contract (as implemented)
Owner          - Pet instance               (PET_RULES.md A5.1)
Initial XP     - 0
Initial Level  - 1
Level range    - [1, 50]
Level formula  - min(floor(XP / 100) + 1, 50)
BattleWon      - +100 Pet XP
BattleLost     - +0 Pet XP
Recipient      - active combat Pet only (state.PetState.PetId.Value)
Inactive Pets  - +0
XP cap         - 4900 HARD (not awarded / not stored / no overflow)
Level cap      - 50

## Persistence
<entity member, EF mapping + constraints, migration name + scope,
existing-row behaviour>

## Reward Flow
<integration point (file:line), gate, recipient resolution, both grants
side by side; sequencing change if any and why it satisfies A5.5 (a)-(c)>

## Exactly Once
<evidence: guard (file:line), retry test names, no new mechanism>

## RewardSummary
<member list defined (Pet track names), where recorded, Player-track members,
defeat/win shapes per DATABASE.md A1 items 1-5, resolution of the A5.6
tension if one was found>

## Tests
<per-layer ACTUAL counts: Domain n, Application n, Infrastructure n, Api n;
suites added / extended; baseline before this task: 1557 passed / 0 failed>

## Build / Migration
<build result; migration applied; has-pending-model-changes result>

## Scope Verification
<explicitly confirm NO changes to: Match-3, Board, Gems, Swap, Cascade,
Combat, Boss, Cards, Relics, Passives, PetLevelMultiplier (stays retired),
Player XP values, Redis, SignalR, API/events, frontend, docs/ task files>

## Documentation
<docs/ unchanged (git status -- docs/); no ADR created; no rule changed>

## Remaining Issues
<residual findings, warnings at baseline, report-only lifecycle notes -
or "None">

## Next Step
<identify ONLY - do not implement, do not create any task file>
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (the grant amount
      comes from the server-resolved outcome; the recipient comes from
      server state - `AGENTS.md` A10 / ADR-001)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` A1 - Pet XP/Level IN;
      no scope change)
- [x] Confirmed exactly one Pet recipient per battle, server-derived
- [x] Confirmed `PetLevelMultiplier` was not reintroduced in any form
- [x] Confirmed no prestige / paragon / evolution / post-50 / party-wide /
      account-wide progression system introduced
- [x] Confirmed no speculative abstraction (no `PetLevelManager` /
      `ProgressionManager` / `XPService` / `UniversalProgressionService` /
      shared Player-Pet XP base)
- [x] Confirmed the Player track is behaviorally untouched
- [x] Confirmed no Match-3 / board / gems / swap / match detection /
      cascade / combat / Boss / Card / Relic / Passive change
- [x] Confirmed no Redis `BattleState`, SignalR, or undocumented API/event
      change
- [x] Confirmed `Pet.XP` is the only new progression field introduced
- [x] Confirmed TASK-059...TASK-068 unmodified; TASK-068 untouched; no TASK-069
      created; nothing committed or staged

-->
