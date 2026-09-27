# TASK-065 — Implement Player XP Persistence and Battle Reward Path

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an
  implementer needs to know where to look.

  THIS TASK IMPLEMENTS AN ALREADY-FROZEN CONTRACT. IT DECIDES NOTHING.

  The Player XP contract is fully decided (COMBAT_RULES.md §7, ADR-016) and
  already written into the authoritative documents. TASK-063 proved the
  implementation is absent (B2). This task makes the code match the docs.

  It does NOT implement Pet XP. It does NOT change any gameplay rule.
-->

---

## Metadata

```text
Task ID:           TASK-065
Type:              FEATURE
Status:            DONE
Risk:              MEDIUM (adds a persisted Player column + narrow migration,
                   and wires a reward grant into the EXISTING canonical
                   battle-end path. Not HIGH: no gameplay rule changes, no
                   API/event/Redis/SignalR contract changes, no new
                   subsystem, and the integration point already exists.
                   tasks/TASK_TYPES.md §4 sets FEATURE at MEDIUM
                   ("gameplay feature"; MEDIUM "backend/realtime/db feature").
                   It touches persistence and the battle-end path, so it is
                   not LOW.)
Priority:          HIGH (TASK-063 blocker B2; prerequisite for the Pet XP
                   implementation task and the Pet XP re-audit)
Primary Agent:     persistence (owns the Player.XP column, EF mapping,
                   constraint, and migration)
Supporting Agents: backend (reward grant in the Application layer's
                   battle-end path + the XP → Level calculation),
                   gameplay (contract conformance of the frozen reward and
                   formula),
                   testing (persistence, formula-boundary, reward,
                   retry-idempotency, and independence coverage),
                   review (scope + no-second-source-of-truth verification)
Workflow:          development/feature.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   backend/persistence-analysis,
                   gameplay/gameplay-behavior-derivation,
                   testing/test-scenario-generation,
                   quality/scope-validation
                   (6 skills — Normal/Complex budget, tasks/README.md §12)
Dependencies:      TASK-063 (DONE as an audit — identified blocker B2 and
                   the canonical integration point; read-only, must NOT be
                   modified),
                   TASK-064 (DONE — retired PetLevelMultiplier; read-only,
                   must NOT be modified),
                   TASK-023 (DONE — Player persistence + ownership; the
                   Player entity and IPlayerRepository this task extends),
                   TASK-059 (DONE — froze the Player XP contract),
                   TASK-061 (DONE — Product Owner decisions),
                   TASK-062 (DONE — finalized the contract into docs/)
Blocks:            the Pet XP implementation task (which needs a coherent
                   battle reward path carrying both tracks),
                   the Pet XP re-audit
Estimate:          Normal (6 skills; one Domain property, one EF mapping +
                   constraint, one migration, one XP→Level function, one
                   reward grant at the existing integration point, one
                   migration-application step, and test coverage across four
                   existing suites)
```

**Type classification note.** `FEATURE`, per `tasks/TASK_TYPES.md` §2: "Implement
a documented mechanic, capability, or system that already has a home in
`docs/` but has not yet been built." The Player XP contract has a complete
`docs/` home (`COMBAT_RULES.md` §7, `DATABASE.md` §1/§3, `ADR-016`) and is not
built. This is not `GAMEPLAY-CHANGE` (no rule changes — `TASK_TYPES.md` §2
requires a GAMEPLAY-CHANGE to *change* a rule and propagate through
implementation), not `BUG` (nothing misbehaves; the feature is simply
absent), not `REFACTOR` (behavior is added, not restructured), and not
`ARCHITECTURE` (no new architectural decision — the persistence boundary, the
transaction boundary, and the battle-end integration point all already
exist).

**Contract-frozen note.** Every value below is already decided and already
authoritative. If execution finds a value that cannot be implemented as
written, that is a STOP (§12) — not license to choose a different value.

---

## 1. Objective

Implement the frozen Player XP contract end-to-end: a persisted `Player.XP`
column with its documented initial value and constraint, a deterministic
`XP → Level` calculation, and the documented `BattleWon +100` / `BattleLost +0`
grant applied exactly once on the existing canonical battle-end path.

**This task implements only Player XP.** Pet XP is explicitly out of scope
(§6).

---

## 2. Authoritative References

The contract is completely specified; do not restate or re-derive it.

- `docs/01-game-design/COMBAT_RULES.md` **§7** — the canonical owner of the
  Player XP / Level contract:
  - §7.2 — `BattleWon` → `Player XP +100`; `BattleLost` → `Player XP +0`
  - §7.3 — reward amount (config, `100`) vs. curve constant (formula, `100`)
    are two independent concepts
  - §7.4 — `Player.Level = min(floor(Player.XP / 100), 49) + 1`, equivalently
    `min(floor(Player.XP / 100) + 1, 50)`; worked boundaries `0→1`, `100→2`,
    `400→5`, `4900→50`, `5000→50`, `10000→50`
  - §7.5 item 1 — `Player.XP` is **uncapped**; it keeps accumulating after
    Level 50. Item 2 — `Player.Level` is capped at 50. Item 3 — initial
    `XP = 0`, initial `Level = 1`. Item 4 — defeat changes nothing. Item 5 —
    no post-50 progression system
  - §7.6 — Player Level has **no** combat stats
- `docs/02-technical/DATABASE.md` §1 (`Player.XP` / `Player.Level` columns;
  "Reward semantics for `RewardSummary`" — Player-track member list), §3
  constraints (`Player.XP int, NOT NULL, default 0`; `Player.XP >= 0` with no
  upper bound; `Player.Level ∈ [1, 50]`; `Player.Level = 1` for a new Player)
- `docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md`
  items 1–8 (the Player decisions) and item 12 (tracks are separate)
- `docs/02-technical/GAME_EVENTS.md` §2 (`BattleWon` / `BattleLost` payload
  rule; reward-summary item)
- `docs/02-technical/API_CONTRACTS.md` §4 notes 1/3 (`rewards` both outcomes;
  event-vs-REST reconciliation)
- `docs/02-technical/GAME_STATE.md` §2.3, §2.8 (`PetState`; `BattleState`
  owner identity — for the §6 "do not touch" boundary)
- `docs/02-technical/ARCHITECTURE.md` §4 item 4 (write-then-delete ordering),
  §5 (anti-overengineering); `docs/02-technical/TDD.md` §4 (persistence
  boundary)
- `docs/00-overview/MVP_SCOPE.md` §1 (Player XP/Level IN), §2, §4
- `AGENTS.md` §7, §9, §10, §16, §17, §18, §20

**Frozen values (do not change any of these):**

```text
Owner:            Player
Initial XP:       0
Initial Level:    1
Level range:      [1, 50]
BattleWon:        +100 Player XP
BattleLost:       +0 Player XP
Level formula:    min(floor(XP / 100) + 1, 50)
XP cap:           NONE (uncapped — accumulates past Level 50)
Level cap:        50
Pet XP:           independent pool; NOT this task
```

**Explicitly forbidden:** `+50` for `BattleWon` (a stale figure that was
never authoritative), the retired `Player.Level × PetLevelMultiplier` model,
and copying Pet XP's `4900` hard cap onto Player XP.

---

## 3. Current State (verified at task creation)

Re-verify each before editing; line numbers may drift.

```text
Domain/Players/Player.cs
    Members: PlayerId, DiscordUserId, Level, CreatedAt.
    NO XP member.  `public int Level { get; init; } = InitialLevel;`
    (init-only — see §5.3 for the implication).
    Constants: MinLevel = 1, MaxLevel = 50, InitialLevel = 1.
    Class comment currently says the entity has "no XP column and no reward
    field" — stale once this task lands.

Domain/Pets/PetLevelDerivation.cs / Application/Pets/PetLevelService.cs
    DELETED by TASK-064. grep over src+tests → 0 matches. Do not reintroduce.

Infrastructure/Postgres/Configurations/PlayerConfiguration.cs
    Maps PlayerId, DiscordUserId (+ unique index), and:
        builder.Property(player => player.Level)
            .IsRequired().HasDefaultValue(Player.InitialLevel);
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_Player_Level_Range",
            $"\"Level\" >= {Player.MinLevel} AND \"Level\" <= {Player.MaxLevel}"));
    No XP mapping.

Application/Players/IPlayerRepository.cs + Infrastructure .../PlayerRepository.cs
    Surface is only:
        Task<Player> GetOrCreateByDiscordUserIdAsync(...)
        Task<Player?> GetByIdAsync(...)          (AsNoTracking)
    There is NO save/update method today. Adding Player XP needs a way to
    persist a mutated Player — see §5.1.

Application/Battle/BattleResultService.cs
    `PersistTerminalResultAsync(BattleState state, BattleOutcome outcome,
    CancellationToken)` (~:179) is the CANONICAL terminal battle-result
    persistence point. It already resolves:
        battleId         :186
        playerId         :187   state.PlayerId.Value
        petInstanceId    :188   state.PetState.PetId.Value
        bossIdentity     :189
    …and constructs BattleResult with
        RewardSummary: EmptyRewardSummary      (~:262)  // the constant "{}"
    then `await _results.AddAsync(result, ct)`  (~:270)   ← the durable write
    then deletes the Redis active state          (~:285)
    Today it awards NO XP to anyone.

Infrastructure/Postgres/Repositories/BattleResultRepository.cs
    `AddAsync` (~:49) loads by BattleResultId, and:
      - if an identical row exists → returns WITHOUT writing (:67-71)
      - if a differing row exists → removes it and re-adds (:78)
      - always ends in ONE `_dbContext.SaveChangesAsync(ct)` (:83)
    `BattleResultId` is the PK (= the battle's BattleId), so at most one row
    per battle exists. This is the documented duplicate guard.

Application/Battle/BattleStateService.cs
    ~:761 `TryUpdateAsync(state, expectedSequence, ct)` — an OPTIMISTIC-
    CONCURRENCY write-back guarded by `Sequence`.
    ~:765 only when `written` is true does it call
    `PersistTerminalResultAsync` (~:773). A refused write re-reads fresh
    state and re-resolves (~:783).
    ~:1345 a failed durable write is ABSORBED: the resolution is still
    reported, the Redis state is retained under its sliding TTL, and the
    battle remains retryable. There is no retry worker.

Latest migrations:
    20260927075413_AddBattleResultPersistence
    20260927124855_DropPetLevelMultiplier          (TASK-064)
    GameDbContextModelSnapshot.cs
```

**Confirmed unchanged since TASK-063/TASK-064:**
`grep "\bXP\b" src/` → **0 matches**. No partial Player XP implementation
appeared. `PetLevelDerivation` / `PetLevelService` → 0 matches.

---

## 4. Scope

### In Scope

1. **Domain** — add `Player.XP` with the documented initial value, and the
   documented `XP → Level` calculation, without disturbing the existing
   `Level` constraint constants.
2. **Persistence** — map `Player.XP` (EF), add the documented constraint(s),
   and add exactly **one** narrowly-scoped migration for the column.
3. **Application / Infrastructure** — extend the Player persistence boundary
   with the smallest operation needed to persist a mutated Player's XP and
   Level, and apply the reward on the existing canonical battle-end path.
4. **Tests** — cover persistence, the formula boundaries, both reward
   outcomes, retry idempotency, and Player/Pet independence.

### Out of Scope — MUST NOT be implemented by this task

```text
Pet.XP · Pet XP rewards · Pet XP cap (4900) · Pet XP → Level formula ·
active-Pet XP targeting · inactive-Pet XP behavior · Pet XP persistence ·
any Pet Level derivation · any Player.Level × PetLevelMultiplier revival ·
Redis BattleState changes · SignalR changes · new events ·
undocumented API/event fields · frontend (Phaser/React/GameRuntime/
BattleScene) · Match-3 · board · gems · swap · match detection · cascade ·
combat · damage · Boss · Cards · Relics · Passives · skills ·
reward redesign beyond the documented Player XP grant ·
progressions beyond Player XP (Prestige/Paragon/Season XP/Evolution) ·
speculative abstractions (PlayerProgression · PlayerExperience ·
XPWallet · ProgressionState · XPService · UniversalProgressionService ·
ProgressionManager)
```

Also out of scope: modifying `PET_RULES.md`, `COMBAT_RULES.md`, `DATABASE.md`,
`GAME_STATE.md`, `API_CONTRACTS.md`, `GAME_EVENTS.md`, any ADR, TASK-063,
TASK-064, or any completed task (`AGENTS.md` §16). **Documentation is already
correct**; this task makes the code match it.

---

## 5. Implementation Notes

### 5.1 The Player persistence boundary gains no second abstraction

`IPlayerRepository` today exposes only `GetOrCreateByDiscordUserIdAsync` and
`GetByIdAsync` (no-tracking). Persisting a mutated Player needs **one**
addition. Keep it minimal and inside the existing boundary — extend
`IPlayerRepository`/`PlayerRepository` rather than creating a service,
manager, or wallet (`AGENTS.md` §9, `ARCHITECTURE.md` §5).

The reward path already has `playerId` from `state.PlayerId.Value`
(`BattleResultService.cs:187`), so no new identity mechanism is required.

### 5.2 The reward integration point is already correct

Use `BattleResultService.PersistTerminalResultAsync` — the canonical terminal
point. Do **not** create a second reward pipeline, a parallel service, or a
new event. `GAME_EVENTS.md` §2 keeps the reward summary on `BattleWon` only;
`API_CONTRACTS.md` §4 note 1 covers both outcomes. Follow whichever the
authoritative documents already fix; if they conflict, STOP (§12).

### 5.3 `Player.Level` is init-only — resolve this deliberately

`public int Level { get; init; }` cannot be updated after construction.
`DATABASE.md` §1/§3 states **both `Player.XP` and `Player.Level` are
persisted columns**, and `COMBAT_RULES.md` §7.4 defines Level as a function
of XP. So Level must be recomputed and stored when XP changes. Widening the
setter from `init` to `set` (or adding an equivalent mutation path) is a
mechanical necessity of the frozen contract, not a design choice — but keep
it minimal and document it. **Do not** introduce a second source of truth
(e.g. a computed property that shadows the column) and do not change the
documented persistence model.

### 5.4 The XP → Level calculation

Implement exactly `min(floor(XP / 100) + 1, 50)`. It reproduces the same
formula *shape* as the Pet contract but shares **no variable, no pool, and no
stored value** (`PET_RULES.md` §5.4; `ADR-016` item 12). An independent,
small, pure function is correct — do **not** build a shared/generic
progression abstraction for a formula that currently has exactly one
implementation.

Boundaries to satisfy (§7):

```text
XP 0 → 1 · XP 99 → 1 · XP 100 → 2 · XP 4900 → 50 · XP > 4900 → 50
```

`Player.XP` itself is **never clamped** — `> 4900` XP is legal and stored.

### 5.5 Exactly-once / retry safety — the critical correctness point

Two distinct mechanisms exist and must both be respected:

1. **Guarded write-back.** `BattleStateService` only calls the terminal
   persist when `TryUpdateAsync(state, expectedSequence, ct)` returned
   `true` (`:761-774`). A refused write re-resolves the fresh state rather
   than persisting, so a stale resolution cannot double-award.
2. **Primary-key reconciliation.** `BattleResultRepository.AddAsync` treats an
   existing row for the same `BattleResultId` as the same battle's already-
   durable result. An identical row returns **without writing** (`:67-71`).

**Requirement:** the Player XP grant must be idempotent under this existing
reconciliation. The grant must NOT be re-applied when
`AddAsync` takes its "already durable, identically — nothing to write" path.
Bind the XP grant to the **first durable write**, not to every call, and
prove it with the §8 retry test. **Do not** invent event sourcing, a
distributed lock, a new idempotency table, or new transaction semantics — the
existing `SaveChanges` boundary and PK guard are the documented mechanism
(`DATABASE.md` §1 sourcing item 1; `REDIS_STATE.md` §3).

If exactly-once cannot be guaranteed within these existing mechanisms → STOP
(§12).

---

## 6. Pet XP Separation

```text
Player XP  ≠  Pet XP
```

This task must not read, write, or depend on `Pet.XP`, Pet Level,
`PlayerPet` progression, or `PetDefinition`. Player XP implementation is
independent; TASK-064 removed the last coupling between Player progression
and Pet progression, and this task must not reintroduce one.

A later Pet XP task may reuse the same battle-end path. That is future work
and must not be pre-built here.

---

## 7. Affected Files & Areas

```text
MODIFY (Domain)
[x] src/backend/GameServer.Domain/Players/Player.cs
        add `XP`; widen `Level` to be settable per §5.3; add the XP → Level
        calculation; refresh the now-stale "no XP column" class comment

MODIFY (Persistence)
[x] src/backend/GameServer.Infrastructure/Postgres/Configurations/PlayerConfiguration.cs
        map `XP`; add the documented constraint(s) from DATABASE.md §3

MODIFY (Application / Infrastructure)
[x] src/backend/GameServer.Application/Players/IPlayerRepository.cs
        one minimal member to persist a mutated Player
[x] src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs
        its EF implementation
[x] src/backend/GameServer.Application/Battle/BattleResultService.cs
        apply the documented Player XP grant at the existing integration point

ADD (migration, tool-generated)
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/<ts>_AddPlayerXp.cs (+ .Designer.cs)
[x] src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs (regenerated)

MODIFY (tests)
[x] tests/backend/GameServer.Infrastructure.Tests/PlayerPersistenceTests.cs
[x] tests/backend/GameServer.Infrastructure.Tests/PlayerPostgresConstraintTests.cs
[x] tests/backend/GameServer.Application.Tests/BattleResultServiceTests.cs
[x] tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs
[x] tests/backend/GameServer.Domain.Tests/  — add the XP → Level boundary tests
        (mirror the removed PetLevelDerivationTests' pure-function shape)
[x] tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs
        extend the existing doubles for the new boundary member

READ ONLY (verify unchanged; do NOT edit)
[ ] docs/**   [ ] src/frontend/**   [ ] tasks/**   [ ] any ADR
[ ] src/backend/GameServer.Domain/Battle/**         (BattleState/PetState)
[ ] src/backend/GameServer.Api/**                   (unless a documented
        contract requires it — expected: no change)
```

---

## 8. Acceptance Criteria

All binary and objectively verifiable.

**Persistence**
- [ ] `Player` has a persisted `XP` member matching `DATABASE.md` §1
      (`int`, NOT NULL, default `0`)
- [ ] A newly created Player has `XP = 0` and `Level = 1`
- [ ] `Player.Level` remains constrained to `[1, 50]`
      (`CK_Player_Level_Range` retained)
- [ ] The `DATABASE.md` §3 constraint `Player.XP >= 0` (no upper bound) is
      represented
- [ ] Exactly **one** new migration is added for `Player.XP`; it touches only
      the `Player` table
- [ ] The migration must NOT include `Pet.XP`, `PetLevelMultiplier`,
      `Pet.Level`, `BattleState`, `BattleResult`, Redis, or SignalR changes
- [ ] `GameDbContextModelSnapshot.cs` reflects the addition (tool-regenerated)
- [ ] `PlayerId`, `DiscordUserId`, `DiscordUserId` uniqueness, and `CreatedAt`
      semantics are unchanged

**XP → Level formula**
- [ ] `XP = 0` → Level 1
- [ ] `XP = 99` → Level 1
- [ ] `XP = 100` → Level 2
- [ ] `XP = 4900` → Level 50
- [ ] `XP > 4900` (e.g. `5000`, `10000`) → Level 50
- [ ] `Player.XP` is **NOT** clamped: `XP > 4900` is stored as given
- [ ] Only `Player.Level` is capped at 50

**Battle reward**
- [ ] `BattleWon` awards exactly **+100** Player XP
- [ ] `BattleLost` awards exactly **+0** Player XP (and no Level change)
- [ ] The grant is applied on the existing canonical terminal path
      (`PersistTerminalResultAsync`) — no second reward pipeline
- [ ] The grant is server-authoritative; no client input influences the amount
- [ ] XP is persisted (survives reload) and Level is stored consistently with
      the formula

**Idempotency**
- [ ] A retried/reconciled terminal persistence for the same battle does
      **not** award Player XP a second time
- [ ] No new idempotency mechanism, lock, or table was introduced

**Independence**
- [ ] Player XP changes do not read or modify Pet XP or Pet Level
- [ ] Pet XP is not implemented by this task
- [ ] No `Player.Level × PetLevelMultiplier` path or `PetLevelMultiplier`
      reference is reintroduced

**Boundaries**
- [ ] No Redis `BattleState` change
- [ ] No SignalR change
- [ ] No undocumented API or event field introduced
- [ ] No gameplay rule changed
- [ ] No speculative abstraction introduced
- [ ] No authoritative document modified (`git status -- docs/` clean)
- [ ] TASK-063, TASK-064, and all completed tasks unmodified
- [ ] Existing backend tests remain green

---

## 9. Testing Requirements

Use the existing test architecture (four projects: Domain, Application,
Infrastructure, Api). Do not create a new framework.

### Required Verification

```text
[x] Unit / Domain tests
        XP → Level boundaries: 0, 99, 100, 4900, >4900 (5000, 10000)
        and that XP itself is never clamped
[x] Persistence tests
        Player.XP defaults to 0; the column is NOT NULL
        Player.XP persists across a reload
        CK_Player_Level_Range still enforced
        Player.XP >= 0 enforced
[x] Application tests
        BattleWon → +100 Player XP (Level recomputed)
        BattleLost → +0 Player XP (Level unchanged)
        Retry/reconcile of the SAME battle does not double-award
        Player XP grant does not touch Pet state
[x] Integration tests
        The new migration applies cleanly (dotnet ef database update)
        A Postgres round-trip persists XP and Level
```

### Commands (repository conventions)

```text
dotnet build src/backend/GameServer.sln
dotnet test  src/backend/GameServer.sln

dotnet ef migrations add AddPlayerXp \
    --project src/backend/GameServer.Infrastructure \
    --startup-project src/backend/GameServer.Api

dotnet ef database update \
    --project src/backend/GameServer.Infrastructure \
    --startup-project src/backend/GameServer.Api
```

Design-time configuration must be supplied through the documented
non-tracked providers (`ADR-015` D10 — never a tracked file). The local
development database is the compose service on `localhost:5433`
(`dcacti_db`).

### Key Edge Cases
- `XP = 4900` is the exact Level-50 boundary; `XP = 4901` and above still
  yield Level 50 while XP continues to grow (`COMBAT_RULES.md` §7.5 items
  1–2).
- `BattleLost` must **not** change Level.
- Retry after a **successful** durable write must not re-award.
- Retry after a **failed** write (the absorbed-exception path,
  `BattleStateService` ~:1345) is the documented recoverable case — verify
  the battle's eventual successful persistence awards once, not twice.

---

## 10. Migration Notes

- Verify how existing Player rows receive the initial value. The documented
  contract is `default 0`, and `DATABASE.md` §3 states `Player.XP = 0 for a
  newly created Player`; existing rows receive `0` via the column default.
  Existing valid `Player.Level` values are **preserved untouched**.
- **Do not invent backfill behavior** the documents do not define: there is no
  documented rule mapping an existing Player's Level back to an XP value, and
  inventing one would create a second source of truth. If execution concludes
  a backfill is required to keep `Level` consistent with `XP` for existing
  rows, that is a genuine ambiguity → **STOP** (§12) and report it.
- The migration must be narrowly scoped: `Player` only.

---

## 11. Frontend / Redis / SignalR

- **Frontend:** no authoritative API/runtime contract exposes Player XP
  (verified: no `playerXp` / `xpGained` / `newPlayerLevel` in
  `API_CONTRACTS.md` or `GAME_EVENTS.md`). **Frontend remains out of scope.**
  Do not add Player XP UI; do not modify GameRuntime or BattleScene.
- **Redis:** Player XP is persistent Player progression, not active battle
  state. Do **not** add Player XP to `BattleState`.
- **SignalR:** do not invent `PlayerXpUpdated` / `PlayerLevelUpdated` events.
  Preserve the existing protocol unchanged.

If execution finds an authoritative document that *does* require an API,
event, Redis, or SignalR exposure, do not invent it — report the requirement
and STOP (§12).

---

## 12. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always
apply. Task-specific conditions:

- **If Player XP has already been implemented** → STOP; the task is obsolete.
- **If the Player XP contract is contradictory across authoritative docs** →
  STOP; report both sources (`AGENTS.md` §4).
- **If the XP numeric type is ambiguous** → STOP.
- **If existing-Player migration/backfill semantics are ambiguous** → STOP
  (§10).
- **If Level persistence-vs-derivation semantics are ambiguous** → STOP.
  (`DATABASE.md` §1/§3 says persisted; a contradiction with that is a STOP,
  not a design choice.)
- **If terminal-reward exactly-once cannot be determined or guaranteed** →
  STOP (§5.5); report the missing contract.
- **If implementing Player XP requires a new architectural decision** → STOP
  (`AGENTS.md` §18).
- **If implementing Player XP requires changing a gameplay rule** → STOP
  (`AGENTS.md` §7).
- **If API/event requirements conflict, or a new field appears to be
  required** → STOP; do not invent a field.
- **If a separate prerequisite must be completed first** → STOP and report.
- **If `+50` or the retired `Player.Level × PetLevelMultiplier` model appears
  anywhere as guidance** → STOP; `+100` and the two-track model are
  authoritative.
- **If Pet XP work becomes necessary to complete Player XP** → STOP; it is
  separately sequenced (§6).
- **If the migration generator detects unrelated pending model changes** →
  STOP; do not bundle them (`AGENTS.md` §16).
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
TASK-064 (DONE)  — retired PetLevelMultiplier                 ← step 1 ✓
        ↓
TASK-065 (this)  — implement Player XP persistence + reward   ← step 2
        ↓
Pet XP re-audit                                               ← step 3
        ↓
Pet XP implementation task                                    ← step 4
```

- **TASK-065 is step 2 only.** It does not begin steps 3–4.
- **TASK-065 is a prerequisite for the later Pet XP implementation**, which
  needs a coherent battle-reward path that already carries the Player track.
- **Do not create the Pet XP implementation task or the re-audit from this
  task**, and do not create TASK-066.
- **Do not modify TASK-063 or TASK-064.** Their evidence is historical record.

---

## 14. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files

**Domain**
- `src/backend/GameServer.Domain/Players/Player.cs` — added `XP` (uncapped,
  `int`, settable) and the documented initial-value/reward/curve constants
  (`InitialXp`, `XpPerLevelCurveConstant`, `BattleWonXpReward`,
  `BattleLostXpReward`); widened `Level` from `init` to `set`; added the
  documented pure functions `LevelForXp(int)` =
  `min(floor(XP / 100) + 1, 50)` and `GrantBattleXp(int)`; refreshed the stale
  "no XP column" class comment. No Pet or combat member added.

**Persistence**
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PlayerConfiguration.cs`
  — mapped `XP` as required with default `Player.InitialXp`; added check
  constraint `CK_Player_XP_NonNegative` (`"XP" >= 0`, no upper bound);
  `CK_Player_Level_Range` retained.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/20260927130330_AddPlayerXp.cs`
  (+ `.Designer.cs`) — the one new migration.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs`
  — tool-regenerated.

**Application / Infrastructure**
- `src/backend/GameServer.Application/Players/IPlayerRepository.cs` — one new
  member, `SaveProgressionAsync(Player, CancellationToken)`; refreshed the
  stale `GetByIdAsync` Pet-recompute doc.
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerRepository.cs`
  — implemented `SaveProgressionAsync`; creation path now writes
  `XP = Player.InitialXp` alongside `Level = Player.InitialLevel`.
- `src/backend/GameServer.Application/Battle/BattleResultService.cs` — applies
  the documented Player XP grant on the canonical terminal path
  (`PersistTerminalResultAsync` → `GrantPlayerXpAsync`), bound to the first
  durable write; constructor takes the `IPlayerRepository` boundary; class doc
  updated (no new pipeline, service, or event).
- `src/backend/GameServer.Application/Battle/IBattleResultRepository.cs` —
  `AddAsync` now returns `Task<bool>` reporting whether the call was the first
  durable write (the existing primary-key guard's own answer).
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/BattleResultRepository.cs`
  — returns that flag; an already-durable row for the battle's key is no longer
  rewritten by a retry.

**Tests**
- ADD `tests/backend/GameServer.Domain.Tests/PlayerXpProgressionTests.cs` — the
  XP → Level contract, boundaries, caps, grants, and cross-track separation.
- ADD `tests/backend/GameServer.Application.Tests/PlayerXpBattleRewardTests.cs`
  — the battle reward, exactly-once/retry, failure, and Pet-independence cases.
- ADD `tests/backend/GameServer.Infrastructure.Tests/PlayerProgressionPersistenceTests.cs`
  — the progression write boundary and reload behaviour.
- ADD `tests/backend/GameServer.Infrastructure.Tests/PlayerXpSchemaTests.cs`
  — the **applied** PostgreSQL schema (column, constraints, migration history,
  no XP column elsewhere, `PetLevelMultiplier` still removed).
- MODIFY `tests/backend/GameServer.Infrastructure.Tests/PlayerPersistenceTests.cs`
  — five-field Player record; the stale "no XP field" assertion replaced by the
  documented XP/Level constraints and initial values.
- MODIFY `tests/backend/GameServer.Infrastructure.Tests/PlayerPostgresConstraintTests.cs`
  — creation values (XP 0 / Level 1), uncapped-XP round trip, negative-XP
  rejection.
- MODIFY `tests/backend/GameServer.Infrastructure.Tests/BattleResultPersistenceTests.cs`
  — added the first-durable-write reporting case.
- MODIFY `tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs`
  — the result double models the primary-key guard and reports first-write;
  added `InMemoryPlayerRepository`.
- MODIFY `tests/backend/GameServer.Application.Tests/BattleResultServiceTests.cs`,
  `BattleResultTerminalFlowTests.cs` — harnesses compose the Player boundary.

**Task lifecycle**
- `tasks/backlog/TASK-065-implement-player-xp-progression.md` →
  `tasks/active/…` → `tasks/completed/…` (BACKLOG → IN PROGRESS → IN REVIEW →
  DONE).

### Migration
- Name: `20260927130330_AddPlayerXp`
- Operation: adds `Player.XP` (`integer`, `NOT NULL`, `DEFAULT 0`) and check
  constraint `CK_Player_XP_NonNegative`. Touches the `Player` table only — no
  `Pet`, `PetDefinition`, `BattleState`, `BattleResult`, Redis, or SignalR
  change.
- Applied via: `dotnet ef database update` (applied cleanly: "Applying
  migration '20260927130330_AddPlayerXp'. Done.")
- Existing-row behaviour: existing rows receive the documented column default
  `XP = 0`. Their `Level` values are **preserved untouched** — no Level → XP
  conversion exists in any document, and none was invented (§10; the documented
  contract is `default 0`).

### Validation Results
- `dotnet build src/backend/GameServer.sln` — **PASS** (0 errors; warnings at
  baseline — see Residual Findings)
- `dotnet test src/backend/GameServer.sln` — **PASS** (1557 tests, 0 failed)
  - Domain 921 · Application 267 · Infrastructure 182 · Api 187
- `dotnet ef database update` — applied cleanly
- `dotnet ef migrations has-pending-model-changes` — **"No changes have been
  made to the model since the last migration."**
- Formula boundaries verified: 0→1, 99→1, 100→2, 199→2, 200→3, 4900→50,
  5000→50, 10000→50; XP above 4900 retained in full (5000, 12345)
- Retry/idempotency verified: the same terminal result processed twice (and
  five times, and after a failed-then-recovered write) awards **once**
- Applied-schema verified against real PostgreSQL (6 assertions incl. migration
  history and "no XP column on any other table")

### Final Report (required format)

```text
## Status
DONE

## Summary
Implemented the frozen Player XP progression contract end-to-end: a persisted
Player.XP column with its documented default and constraint, the deterministic
XP → Player.Level calculation, and the documented BattleWon +100 / BattleLost +0
grant applied exactly once on the existing canonical battle-end path
(BattleResultService.PersistTerminalResultAsync). Player XP remains independent
of Pet progression. No gameplay rule, API, event, Redis, or SignalR contract was
changed.

## Player XP Contract (as implemented)
Owner          · Player (DATABASE.md §1; COMBAT_RULES.md §7)
Initial XP     · 0            (Player.InitialXp)
Initial Level  · 1            (Player.InitialLevel)
Level range    · [1, 50]      (Player.MinLevel/MaxLevel; CK_Player_Level_Range)
Level formula  · min(floor(XP / 100) + 1, 50)   (Player.LevelForXp)
BattleWon      · +100 XP      (Player.BattleWonXpReward)
BattleLost     · +0 XP        (Player.BattleLostXpReward)
XP cap         · NONE — uncapped, accumulates past Level 50
Level cap      · 50

## Unchanged (verified)
Pet XP · Pet Level · PetDefinition · PetState · BattleState · Redis ·
SignalR · API/event contracts · RewardSummary shape ({}) · docs/ ·
TASK-063 · TASK-064 · completed tasks.
`git status -- docs/` shows only the pre-existing TASK-059/062/064 working-tree
changes; this task modified no authoritative document.

## Not Implemented (intentionally)
Pet.XP · Pet XP rewards/cap/formula/targeting · Pet Level XP derivation ·
PlayerPet changes · PetDefinition changes · PetLevelMultiplier (stays retired)

## Tests
4 suites added, 6 suites extended; full backend suite green
(1557 passed, 0 failed, 0 skipped).
Baseline before this task: 1499 passed, 0 failed.

## Migration
20260927130330_AddPlayerXp — adds Player.XP (integer NOT NULL DEFAULT 0) and
CK_Player_XP_NonNegative ("XP" >= 0); Player table only. Applied via
`dotnet ef database update`; existing rows receive XP = 0 by the column default
and keep their existing Level untouched.

## Residual Findings
1. `BattleResultRepository.AddAsync` previously rewrote a stored battle-result
   row whenever the incoming row differed. Because CompletedAt is a fresh
   server clock reading per call (DATABASE.md §1 sourcing item 2), a retry was
   never byte-identical, so that branch fired on every retry. Left as-is it
   would have re-awarded Player XP. Fixed inside this task by treating an
   existing row for the battle's own key as the already-durable terminal
   result (it is — the key IS the BattleId) and reporting it as "not the first
   write". This is the documented primary-key guard; no new mechanism.
2. Pre-existing, out of scope, NOT fixed: `BattleResultPersistenceTests.cs:524`
   emits CS8602 (`index.IsDescending` nullability) and
   `GameServer.Infrastructure/DependencyInjection.cs:6` emits CS0105 (duplicate
   using). Both predate this task and are unrelated to Player XP. Suggested
   follow-up: a trivial warning-cleanup task.

## Next Step
The Pet XP re-audit (TASK-063 step 3), then the Pet XP implementation task
(step 4). Neither was created by this task. TASK-066 does not exist.
```

### Server Authority & Scope Verification
- [x] Confirmed the reward grant is server-authoritative (no client input) —
      the amount is selected from the outcome the resolution itself reported
      (`GAME_EVENTS.md` §2); the boundary accepts no client-supplied value
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Player XP/Level IN;
      no scope change)
- [x] Confirmed no `PlayerAttack`/`PlayerDefense`/`PlayerHP`/`PlayerCrit`/
      `PlayerPower` or equivalent introduced (`COMBAT_RULES.md` §7.6)
- [x] Confirmed no Prestige, Paragon, Season XP, Evolution, or other extra
      progression system introduced (`COMBAT_RULES.md` §7.5 item 5)
- [x] Confirmed no speculative abstraction introduced (no
      `PlayerProgression`/`XPWallet`/`ProgressionState`/`XPService`/
      `UniversalProgressionService`)
- [x] Confirmed Pet XP was not implemented
- [x] Confirmed no Match-3 / board / gems / swap / match detection / cascade /
      combat / damage / Boss / Card / Relic / Passive change
- [x] Confirmed no Redis `BattleState`, SignalR, or undocumented API/event
      change
- [x] Confirmed `Player.XP` is the only new progression field introduced
- [x] Confirmed TASK-063 and TASK-064 unmodified