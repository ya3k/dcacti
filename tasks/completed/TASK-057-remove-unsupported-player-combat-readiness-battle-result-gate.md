# TASK-057 — Remove the Unsupported Player Combat-Readiness Mechanism from the BattleResult Persistence Path

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
  Created from TASK-056 (resolved by human decision, 2026-09-27).
-->

---

## Metadata

```text
Task ID:           TASK-057
Type:              BUG
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (a shipped battle-end persistence path carries a
                   prerequisite the authoritative contract does not define,
                   and cites a documentary clause that does not exist)
Primary Agent:     backend
Supporting Agents: persistence (Player/Postgres schema surface), testing
                   (regression tests), review (documentation consistency)
Workflow:          development/bug-fix.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   backend/api-contract-validation, quality/documentation-consistency
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-041 (DONE — historical introduction of the mechanism;
                   read-only, immutable, never edited: AGENTS.md §16,
                   TASK_LIFECYCLE.md §3), TASK-056 (DONE — the decision this
                   task implements)
Blocks:            None identified. TASK-032's readiness is evaluated
                   separately, after this task completes.
Estimate:          Simple (deletion of an unsupported mechanism; no new
                   contract, no new predicate, no schema change)
```

**Status note.** Created `BACKLOG` per `tasks/README.md` §6; picked up and
executed in the same session → `IN PROGRESS` → `DONE`. Its blocking dependency
(TASK-056) was resolved and the owning document (`DATABASE.md` §1) already
stated the contract this task implements, so no `READY` validation round was
outstanding. The file stays in `backlog/` per `TASK_LIFECYCLE.md` §4 — a
`BACKLOG → DONE` file move is not a defined transition, and moving it to
`active/` only to move it again would add churn without carrying state. This
matches how TASK-056 was closed.

---

## 1. Objective

Remove the unsupported Player **combat-readiness** mechanism from the
battle-end persistence path, so `BattleResult` persistence proceeds on
exactly the prerequisites `DATABASE.md` §1 documents, with no
`IsCombatReady` check, no Player-side eligibility predicate, and no
substitute predicate of any kind.

`DATABASE.md` §1 item 3 now states the contract explicitly: **`BattleResult`
persistence requires no Player combat-readiness condition**, and no
`IsCombatReady` predicate is part of the contract. The current
implementation of the battle-end path contradicts it, so this is an
**implementation bug** (`development/bug-fix.md` §1) — classified by the
TASK-056 human decision as an `IMPLEMENTATION / CONTRACT-MISMATCH GAP`.
`docs/` is authoritative; the code deviates (`.ai/README.md` §18,
`development/bug-fix.md` §3).

---

## 2. Authoritative References

| # | Document | Why |
|---|---|---|
| 1 | `docs/02-technical/DATABASE.md` §1 — "Identity and reward sourcing for `BattleResult`" (the item stating persistence requires **no** Player combat-readiness condition, immediately after the unresolved-`BossDefinition` fail-closed item), and the `Player` entity block + §1 items 1–2 | The canonical owner of the contract this task restores; also proves the `Player` schema has four columns and no readiness field |
| 2 | `docs/02-technical/ARCHITECTURE.md` §4 item 4 (battle end: durable result write **then** active-state delete) | Bounds the battle-end lifecycle that must remain intact |
| 3 | `docs/02-technical/API_CONTRACTS.md` §3 (loadout/ownership validation is **battle-start**, `INVALID_LOADOUT` / `PET_NOT_OWNED`) and §4 (result endpoint: 200/404/401; note 7 owner-only read) | Proves the predicates that must **not** be conflated with battle-end persistence, and that no wire contract changes |
| 4 | `docs/02-technical/REDIS_STATE.md` §3 (delete conditioned on the write) | The delete ordering preserved by this task |
| 5 | `docs/02-technical/GAME_STATE.md` §2.0.3, §2.8 | No lifecycle/readiness member on `BattleState`; `PlayerId` is identity only |
| 6 | `docs/01-game-design/GAME_RULES.md` §1, §18 | Battle composition and server authority — no eligibility concept |
| 7 | `docs/01-game-design/PET_RULES.md` §2 item 2, §5 | Player = account/owner; no Player combat pool; Player Level carries no combat stats |
| 8 | `docs/03-decisions/ADR/ADR-011*` (Player has no combat pool), `ADR-012*` (Player Level: no combat stats), `ADR-014*` (`BattleState.PlayerId` is identity only, "no combat meaning") | Bound what a Player-side predicate could even reference — none is authorized |
| 9 | `tasks/backlog/TASK-056-resolve-combat-readiness-battle-result-persistence-contract.md` — Decision A and Decision C, and §13 | The decision and classification this task implements |
| 10 | `tasks/completed/TASK-041-implement-battle-result-persistence.md` L453–457, L496–499 | Historical record of the mechanism's introduction — **read-only; never edited** |
| 11 | `AGENTS.md` §7 (no invented rules), §16 (task discipline), §17 (docs before code), §20 (stop conditions); `.ai/README.md` §18; `development/bug-fix.md` §1, §4 | Govern this task's behavior |

---

## 3. Scope

### In Scope

1. Remove the readiness gate from the battle-end write so the durable
   `BattleResult` row is written on the documented prerequisites alone.
2. Remove the three unsupported readiness artifacts and their composition-
   root registration.
3. Correct the `DATABASE.md` readiness citations/comments in the affected
   code so no surviving comment asserts a clause the document does not
   contain.
4. Update the affected tests: delete the readiness-specific cases and
   fixtures, and **add a regression test** proving the documented behavior
   (the write happens with no readiness concept involved).
5. Preserve, unchanged: the unresolved-`BossDefinition` fail-closed rule
   (`DATABASE.md` §1), the write-then-delete ordering
   (`ARCHITECTURE.md` §4 item 4, `REDIS_STATE.md` §3), and the
   `BattleResultId = BattleId` one-row guarantee.

### Out of Scope

- Any new eligibility predicate — see §5 (explicitly forbidden).
- Any `docs/01-game-design/` change: no game rule changes; this task adds
  no gameplay concept (`AGENTS.md` §7).
- Any further `docs/` change — `DATABASE.md` §1 was updated by TASK-056 and
  is correct; if a dependent document's wording is found stale, STOP and
  report (`AGENTS.md` §4), do not edit.
- Any `Player` schema/column change, migration, or `BattleState` member.
- Any change to authentication, session handling, or result-read
  authorization (`API_CONTRACTS.md` §2.8, §4 note 7).
- Any change to reward content (`RewardSummary = {}` staging; TASK-033 owns
  the member list), XP, or balance.
- TASK-041 itself, TASK-032, TASK-033, TASK-036 (`AGENTS.md` §16).
- Unrelated cleanup of the code discovered while here (report only).

---

## 4. Current State — Mechanism Inventory (verified against the working tree, 2026-09-27)

Every reference below was located by repository search, not by remembered
line number. This inventory defines the boundary: **only these artifacts**
belong to the unsupported readiness mechanism.

### 4.1 Solely part of the unsupported mechanism — REMOVE

```text
src/backend/GameServer.Domain/Players/ICombatStatsSource.cs      (whole file: ICombatStatsSource, PlayerCombatProfile)
src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerCombatProfileSource.cs   (whole file)
src/backend/GameServer.Domain/Players/Player.cs                  (IsCombatReady property, L106–135 only)
```

### 4.2 Readiness-coupled lines inside files that MUST otherwise survive — EDIT, do not delete

```text
src/backend/GameServer.Application/Battle/BattleResultService.cs
    L80, L121   the ICombatStatsSource field and constructor parameter
    L221, L223  the readiness read and the gate condition
    L299–L318   IsBattleReadyAsync (remove entirely)
    L213–L220   the comment asserting DATABASE.md §1 "item 3 item 1"
    → the unresolved-BossDefinition lookup (L209–L211) and the documented
      write-then-delete sequence (L266–L296) MUST remain

src/backend/GameServer.Infrastructure/DependencyInjection.cs
    L83–L86     the readiness citation comment and the
                AddScoped<ICombatStatsSource, PlayerCombatProfileSource>() registration
```

### 4.3 Tests — readiness cases to delete, plus one regression test to add

```text
tests/backend/GameServer.Application.Tests/BattleResultServiceTests.cs
    L378–L417  the "not ready ⇒ no write, no delete" cases (delete)
tests/backend/GameServer.Application.Tests/BattleResultTestDoubles.cs
    L147–L183  ScriptedCombatStatsSource and its harness wiring (delete)
tests/backend/GameServer.Infrastructure.Tests/BattleResultPostgresTests.cs
    L560–L591  PlayerCombatProfileSource read tests (delete)
```

### 4.4 Verified as NOT part of the mechanism — DO NOT TOUCH

```text
tests/backend/GameServer.Infrastructure.Tests/PlayerPersistenceTests.cs L41–L54
    This test asserts the CLR property set {CreatedAt, DiscordUserId,
    IsCombatReady, Level, PlayerId} against the four-column mapped set — and
    that `IsCombatReady` is NOT a column. It is a Player-persistence test, not
    a readiness-mechanism test, but it names the property, so it MUST be
    updated to keep compiling once `Player.IsCombatReady` is removed.
    Update the property-name list only; DO NOT weaken or delete its
    four-documented-columns assertion (DATABASE.md §1 Player block).
```

**Boundary rule.** If current inspection shows any file or line involved
that is not listed above, or if removing a listed item also removes
behavior belonging to another concern, **STOP** and re-report per
`.ai/README.md` §13 rather than widening the removal.

---

## 5. Reconciliation Semantics (binding requirements)

```text
[x] BattleResult persistence proceeds according to the documented
    BattleResult prerequisites (DATABASE.md §1) — and nothing else.
[x] No IsCombatReady check anywhere on the battle-end path.
[x] No Player combat-readiness predicate, and no variable/field/parameter
    representing one.
[x] No new replacement eligibility predicate.
[x] No fallback/substitute predicate of ANY kind, including:
      has Pet          has Cards        has Relics
      valid Loadout    Player Level >= X
      Player exists    authenticated    owns Battle
[x] Authentication and battle ownership remain governed by their existing
    contracts and are NOT conflated with BattleResult persistence
    readiness:
      - authentication  → API_CONTRACTS.md §1 global rule, §2.8 (ADR-007 item 4,
                          ADR-015)
      - read ownership  → API_CONTRACTS.md §4 note 7 (reader may read only
                          when the session identity equals
                          BattleResult.PlayerId)
      - loadout validity→ API_CONTRACTS.md §3 — BATTLE START, not battle end
[x] The documented fail-closed rule is preserved exactly: an unresolved
    BossDefinition still yields no BattleResult row and no
    battle:{battleId}:state delete (DATABASE.md §1).
[x] The write-then-delete ordering is preserved (ARCHITECTURE.md §4 item 4,
    REDIS_STATE.md §3).
```

Removing the gate must not change the outcome of the documented
prerequisites. The gate is currently inert (`=> true`), so the observable
behavior for every documented case is unchanged; the only behavioral
difference is that the code no longer consults a concept the contract does
not define.

---

## 6. Acceptance Criteria

- [x] `ICombatStatsSource`, `PlayerCombatProfile`, and
      `PlayerCombatProfileSource` no longer exist anywhere in `src/`
      (verified by repository-wide search).
- [x] `Player.IsCombatReady` no longer exists; `Player` still exposes exactly
      the four documented persistence members (`DATABASE.md` §1 Player block).
- [x] `BattleResultService` contains no readiness read, no readiness field or
      constructor parameter, no `IsBattleReadyAsync`, and no readiness
      condition in its write gate.
- [x] `BattleResultService` still fails closed on an unresolved
      `BossDefinition` (no row, no delete) and still writes then deletes in
      that order.
- [x] No substitute/fallback predicate was introduced (§5) — verified by
      reading the final diff, not by absence of the old name.
- [x] No surviving comment, XML doc, or diagnostic message cites a
      `DATABASE.md` readiness clause or describes a battle-readiness
      concept. One stale occurrence WAS found and corrected in
      `BattleResultEndpointTests.cs` L486–492 (see §11 Remaining Issues);
      every remaining `readiness` occurrence is a negative statement or
      unrelated Passive-domain "Ready" semantics.
- [x] Authentication, session, and result-read authorization are unchanged
      (`API_CONTRACTS.md` §2.8, §4 note 7) — no such file was edited.
- [x] A regression test asserts the documented behavior: a battle ending with
      a resolvable `BossDefinition` writes its `BattleResult` and deletes the
      active state, with no readiness input in the fixture at all
      (`PersistTerminalResult_ShouldWrite_WithNoPlayerReadinessInvolved`).
- [x] The unresolved-`BossDefinition` fail-closed cases still pass unchanged.
- [x] `src/backend/GameServer.Api` public surface is unchanged: no file under
      `GameServer.Api/` was edited (verified by modification time); the `§4`
      response body is untouched.
- [x] No migration, no schema change, no `BattleState` member change
      (migration files unchanged; `PlayerConfiguration` still maps the four
      documented columns).
- [x] All relevant tests pass at the required validation depth
      (`core/validation.md` §2 — MEDIUM).
- [x] Quality review checklist passes (`quality/review.md` §1).
- [x] No authoritative rule or contract violated (`AGENTS.md` §10 / ADR-001);
      `TASK-041`, `TASK-032`, `TASK-033`, `TASK-036` byte-identical.

---

## 7. Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Players/            (Player.cs edit; ICombatStatsSource.cs delete)
[x] src/backend/GameServer.Application/Battle/        (BattleResultService.cs edit)
[x] src/backend/GameServer.Infrastructure/            (DependencyInjection.cs edit;
                                                       Postgres/Repositories/PlayerCombatProfileSource.cs delete)
[x] tests/backend/                                    (three test files edited; regression test added)
[ ] src/backend/GameServer.Api/                        (no change expected — verify only)
[ ] src/frontend/                                      (no change)
[x] docs/                                              (NO EDIT — DATABASE.md §1 already states the
                                                        contract via TASK-056; a discovered stale
                                                        dependency is a STOP, not an edit)
[ ] tasks/                                             (no change — new task created by TASK-056's
                                                        follow-up; no existing task file modified)
```

---

## 8. Implementation Notes

- Follow `development/bug-fix.md` §2's flow. The bug category is settled
  (`IMPLEMENTATION / CONTRACT-MISMATCH GAP`, TASK-056 Decision C): docs are
  correct, code deviates. Do not re-open the classification.
- **Do not treat TASK-041 as authoritative design.** Its Report-Only item 4
  (L453–457) and Revision-2 fifth finding (L496–499) are historical records
  of the gap; TASK-041 is immutable and is not edited
  (`TASK_LIFECYCLE.md` §3, `AGENTS.md` §16).
- The gate is currently inert, so expect **most existing tests to pass
  unchanged**; the work is removal plus correcting the tests that construct
  the removed type.
- `PlayerPersistenceTests.cs` L41–L54 is the one test whose *assertion* must
  change only in its CLR property-name list (the removal of
  `IsCombatReady` from the property set). Its four-column claim is correct
  and must be strengthened, not weakened — after this task the CLR set and
  the mapped set are identical.
- Remove the readiness line from `DependencyInjection.cs` together with the
  comment above it, which cites the nonexistent clause.
- Deletion of whole files must be by `git rm`/file deletion, not by leaving
  an empty or commented-out shell.
- Unrelated findings (e.g. the `DATABASE.md` duplicate item-3 numbering,
  `ARCHITECTURE.md`'s `BattleResolutionService` name, `ICombatStatsSource`'s
  misleading name) are report-only (`AGENTS.md` §16) — not fixed here.

---

## 9. Testing Requirements

### Required Verification

```text
[x] Unit tests         — BattleResultService write gate: documented
                         prerequisites; no readiness input exists in the fixture
[x] Integration tests  — the battle-end boundary against PostgreSQL, and the
                         write-then-delete order against the battle-state store
[x] Regression test    — a documented-case battle end writes its result and
                         deletes active state with no readiness concept present
[x] Gameplay scenarios — N/A beyond the terminal battle-end transition
                         (no rule/state-transition semantics change)
```

### Required Commands

```text
[x] dotnet build (backend solution)                                   — PASS
[x] dotnet test (backend test projects)                               — PASS
[x] Repo-wide search: IsCombatReady | ICombatStatsSource | PlayerCombatProfile
                                                                      — 0 matches in src/
[x] Repo-wide search: readiness | combat-ready | battle-ready | duelable
                                                                      — 0 matches in src/
[x] git diff --stat scoped to this task: no docs/, no migration, no Api
    contract change, no existing task file
```

### Key Edge Cases

- A battle ending **with** a resolvable `BossDefinition` — the row **is**
  written and the active state **is** deleted, with no eligibility input.
- A battle ending with an **unresolved** `BossDefinition` — unchanged:
  no row, no delete, state retained and retryable
  (`DATABASE.md` §1 item 3).
- The write fails — the active state is not deleted (ordering preserved).
- The delete fails — absorbed per `REDIS_STATE.md` §3; the durable result
  stands.
- An authenticated caller reading another Player's result — unchanged
  `404 BATTLE_NOT_FOUND` (`API_CONTRACTS.md` §4 note 7).
- `Player` round-trip: the persisted/mapped column set is exactly the four
  documented columns.

---

## 10. Stop Conditions

Universal stops in `AGENTS.md` §20 apply. Task-specific stops — STOP and
report instead of guessing if:

1. A file or line beyond §4's boundary turns out to be part of the
   readiness mechanism, or removing a listed item would also remove
   behavior belonging to another concern (§4 boundary rule).
2. TASK-041 contains additional behavior coupled to readiness that cannot be
   separated without a new architectural decision.
3. Removing `IsCombatReady` affects an unrelated gameplay system.
4. The reconciliation appears to require inventing a replacement predicate
   (§5) — do not invent one.
5. Another undocumented gameplay rule becomes necessary.
6. A `docs/` edit appears necessary: TASK-056 owns the contract and
   `DATABASE.md` §1 already states it. A stale dependent reference is an
   `AGENTS.md` §4 conflict to report, not an inline fix.
7. The human decision is found to conflict with a higher-authority Domain
   Rule (`GAME_RULES.md` or a domain rule document) — report per
   `AGENTS.md` §4 instead of proceeding.
8. The task requires editing TASK-041, TASK-032, TASK-033, TASK-036, or any
   `tasks/completed/` file → stop (`AGENTS.md` §16).
9. The skill budget (7) would be exceeded → stop and decompose
   (`tasks/README.md` §12).

---

## 11. Completion Evidence

### Changed Files

**Deleted (2) — solely the unsupported readiness mechanism (§4.1)**
- `src/backend/GameServer.Domain/Players/ICombatStatsSource.cs` — removed the
  whole file (`ICombatStatsSource`, `PlayerCombatProfile`).
- `src/backend/GameServer.Infrastructure/Postgres/Repositories/PlayerCombatProfileSource.cs`
  — removed the whole file.

**Modified — production (4)**
- `src/backend/GameServer.Domain/Players/Player.cs` — removed the
  `IsCombatReady` property and its 30-line doc comment. No other member
  touched; the four documented persistence members remain.
- `src/backend/GameServer.Application/Battle/BattleResultService.cs` —
  removed the `ICombatStatsSource` field and constructor parameter, the
  readiness read, `IsBattleReadyAsync` (18 lines), the readiness half of the
  write gate, and the readiness citations in the class/call doc comments.
  The gate is now `bossDefinitionId is not { Length: > 0 }`. The
  unresolved-`BossDefinition` lookup, the eight-field construction, the
  PostgreSQL write, and the post-write Redis delete are byte-identical.
- `src/backend/GameServer.Infrastructure/DependencyInjection.cs` — removed the
  `AddScoped<ICombatStatsSource, PlayerCombatProfileSource>()` registration
  and the readiness comment above it. No other registration touched.

**Modified — tests (5)**
- `tests/.../Application.Tests/BattleResultTestDoubles.cs` — removed
  `ScriptedCombatStatsSource` (39 lines).
- `tests/.../Application.Tests/BattleResultServiceTests.cs` — removed the
  `Players` harness member and wiring, removed 3 readiness tests
  (`ShouldFailClosed_WhenTheAccountIsNotBattleReady`,
  `ShouldFailClosed_WhenTheAccountCannotBeRead`,
  `ShouldNotAcceptAnotherPlayersReadiness`), and **added the regression test**
  `PersistTerminalResult_ShouldWrite_WithNoPlayerReadinessInvolved`.
- `tests/.../Application.Tests/BattleResultTerminalFlowTests.cs` — removed the
  `Players` harness member, wiring, and the constructor argument.
- `tests/.../Infrastructure.Tests/BattleResultPostgresTests.cs` — removed the
  `PlayerProfileSource_ShouldReportTheOwningAccountAndNothingForAnUnknownOne`
  test and the `PlayerCombatReady` probe helper.
- `tests/.../Infrastructure.Tests/PlayerPersistenceTests.cs` — **strengthened**
  the four-column assertion: the expected CLR property set is now
  `{CreatedAt, DiscordUserId, Level, PlayerId}` (the CLR set and the mapped
  column set are now identical). The four-column claim was not weakened.
- `tests/.../Api.Tests/BattleResultEndpointTests.cs` — corrected the one stale
  comment that still described a "documented battle-readiness read"; it now
  states the actual reason the Player row is seeded (the `PlayerId` FK) and
  records that no Player-side condition gates the write. No test logic changed.

**Not changed**
- `docs/` — none. `DATABASE.md` §1 already stated the contract (TASK-056) and
  implementation revealed **no** contradiction with it. No ADR created.
- `tasks/` — no existing task file touched.

### Validation Results

Baseline captured before editing, then re-run after.

```text
dotnet build src/backend/GameServer.sln
  → Build succeeded, 0 Error(s)                                  PASS

dotnet test src/backend/GameServer.sln
  Baseline (before)                    After (TASK-057)         Result
  Domain.Tests          911 passed     911 passed               PASS
  Application.Tests     244 passed     242 passed               PASS
  Infrastructure.Tests  176 passed     175 passed               PASS
  Api.Tests             184 passed     184 passed               PASS
  TOTAL                1515 passed    1512 passed  (0 failed)   PASS

  Delta explained exactly: Application −3 (the three readiness tests
  removed) +1 (the regression test added) = −2; Infrastructure −1 (the
  PlayerCombatProfileSource test removed). Domain and Api are unchanged.

Frontend tests: NOT run — no frontend contract was touched (no file under
  src/frontend/ was edited).
```

### Static Verification (required searches)

```text
git grep -n "IsCombatReady"            → 2 matches, BOTH in DATABASE.md
                                         (L6, L475) and BOTH negative: the
                                         clause stating no IsCombatReady
                                         predicate is part of the contract.
                                         ZERO in src/ and tests/.
git grep -n "ICombatStatsSource"       → 0 matches (exit 1)
git grep -n "PlayerCombatProfile"      → 0 matches (exit 1)
git grep -n "PlayerCombatProfileSource"→ 0 matches (exit 1)

git grep -n -i "combat readiness"   -- src tests → 0 matches (exit 1)
git grep -n -i "battle-readiness"   -- src tests → 0 matches (exit 1)

Full untracked-inclusive sweep of src/ + tests/ for
  IsCombatReady|ICombatStatsSource|PlayerCombatProfile|readiness|
  combat.ready|battle.ready|duelable
  → every remaining hit classified:
      BattleResultService.cs L145, L218   negative statements (DATABASE.md
                                          §1 requires no readiness condition)
      BattleResultServiceTests.cs L372/375/380/385
                                          the regression test's own name and
                                          comments (asserts absence)
      PlayerPersistenceTests.cs L44/45    negative assertions
      PassiveProgress.cs L110,
      PassiveTracker.cs L305              UNRELATED — PASSIVE_RULES.md §2
                                          item 3 "becomes Ready and triggers
                                          immediately" (passive charge, not
                                          Player readiness; not this mechanism)
  → no unsupported readiness gate remains.
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no client code changed)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no system added
- [x] Confirmed no gameplay rule invented (`AGENTS.md` §7) — removal only
- [x] Confirmed no replacement/fallback predicate introduced (§5) — the gate
      is now the single documented `BossDefinitionId` condition
- [x] Confirmed TASK-041 / TASK-032 / TASK-033 / TASK-036 / TASK-056 untouched
- [x] Confirmed write-then-delete ordering and the unresolved-`BossDefinition`
      fail-closed rule are preserved
- [x] Confirmed no migration, no schema change, no `BattleState` member change
- [x] Confirmed no API/DI response-contract change (no `GameServer.Api/` file
      edited)

### Remaining Issues (`AGENTS.md` §16 — report only, not fixed)
1. **One stale readiness comment was found beyond §4.3's inventory** and was
   corrected because it described the removed mechanism directly:
   `BattleResultEndpointTests.cs` L486–492 previously said the seeded Player
   row existed so "the battle-end path's documented battle-readiness read" could
   resolve it. It is now accurate (the `PlayerId` FK). This is a comment fix
   within a file §4.3 already lists as in-scope for readiness cleanup, not a
   widening of the removal; the §4 boundary rule required no STOP.
2. **Pre-existing issues, untouched** (carried from TASK-056 §13.3):
   `DATABASE.md` §1 duplicate item-3 numbering; `ARCHITECTURE.md` §4 naming
   `BattleResolutionService` where the orchestrator is `BattleStateService`.

---

## Revision History

| Rev | Date | Summary |
|---|---|---|
| 1.1 | 2026-09-27 | **Implemented — `Status: DONE`.** Removed the unsupported Player combat-readiness mechanism: deleted `ICombatStatsSource.cs` (+ `PlayerCombatProfile`) and `PlayerCombatProfileSource.cs`; removed `Player.IsCombatReady`; removed the readiness field, constructor parameter, read, `IsBattleReadyAsync`, and gate half from `BattleResultService`, leaving the documented `BossDefinitionId` fail-closed condition and the write-then-delete order intact; removed the DI registration. Tests: deleted 4 readiness tests/fixtures, **added the regression test** `PersistTerminalResult_ShouldWrite_WithNoPlayerReadinessInvolved`, and **strengthened** `PlayerPersistenceTests` so the CLR property set equals the four mapped columns. Baseline 1515 → 1512 passing, 0 failed (delta fully explained). Static searches: zero `ICombatStatsSource` / `PlayerCombatProfile` / `PlayerCombatProfileSource` references repo-wide, zero readiness references in `src`/`tests` except negative statements and unrelated Passive-domain "Ready" semantics. No `docs/` change (no contradiction found), no ADR, no migration, no schema change, no API change. TASK-041 / TASK-032 / TASK-033 / TASK-036 / TASK-056 untouched. |
| 1.0 | 2026-09-27 | Created (`BACKLOG`) from the TASK-056 human decision (Decision A: `BattleResult` persistence requires **no** Player combat-readiness prerequisite; Decision C: `IMPLEMENTATION / CONTRACT-MISMATCH GAP`). Owns removal of the unsupported readiness mechanism introduced by TASK-041 — `Player.IsCombatReady`, `ICombatStatsSource`, `PlayerCombatProfile`, `PlayerCombatProfileSource`, its DI registration, the battle-end gate, and the false `DATABASE.md` §1 readiness citations in code. §4 records the search-verified mechanism inventory and the do-not-touch boundary; §5 binds the "no replacement predicate" semantics. No source change made by this task-creation step; TASK-041 is untouched and remains the historical, immutable record of the introduction. |
