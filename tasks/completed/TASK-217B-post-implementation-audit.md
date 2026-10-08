# TASK-217B — Post-Implementation Audit: Battle Reward Atomicity

**Audited implementation:** TASK-217A — Atomic Battle Reward Persistence
**Audit type:** Post-implementation verification of correctness (read-only)
**Repository:** `E:\dcacti`, branch `master`, HEAD `9b5c5fe`
**Scope:** the uncommitted working-tree diff, not a commit

---

## Verdict

**PASS**

The implementation provides true database atomicity, not test-modelled
atomicity. The transaction is opened on the single scoped `GameDbContext`
that all three participating repositories are constructed from, all three
`SaveChangesAsync` calls genuinely enlist in it, the commit precedes the
Redis delete on a control-flow path that cannot be reordered, and the
PostgreSQL integration suite proves rollback by reading durable state
through a **separate connection**.

Two non-blocking findings are recorded in §11; neither withholds a
correctness property, so neither changes the verdict.

---

## Audit method and evidence base

Everything below was derived from reading the actual uncommitted diff and
the surrounding repository state, plus executed verification:

```text
dotnet build GameServer.sln                      → 0 errors
GameServer.Application.Tests   (full)            → 653 passed, 0 failed, 0 skipped
GameServer.Infrastructure.Tests (full)           → 422 passed, 0 failed, 0 skipped
```

**Zero skipped is itself evidence.** The PostgreSQL suite skips itself when
its database is unreachable (`if (!_available) return;`). PostgreSQL on
`localhost:5433` was confirmed reachable before the run and the suite
reported 0 skipped, so the atomicity tests genuinely executed against
PostgreSQL rather than silently passing through the guard.

---

## 1. Transaction ownership

**Verified: `BattleResultService.PersistTerminalResultAsync` owns the
transaction lifetime.**

`BattleResultService.cs:560-688` is the whole unit of work:

```text
await using var transaction = await _transaction.BeginAsync(cancellationToken);   // :560
try
{
    var firstDurableWrite = await _results.AddAsync(result, ct);                 // :578
    if (firstDurableWrite)
    {
        ... player.GrantBattleXp(...);  await _players.SaveProgressionAsync(...) // :605-625
        ... pet.GrantBattleXp(...);     await _pets.SaveProgressionAsync(...)    // :627-647
    }
    await transaction.CommitAsync(cancellationToken);                            // :662
}
catch
{
    try { await transaction.RollbackAsync(CancellationToken.None); }             // :680
    catch (Exception) { /* deliberately absorbed */ }
    throw;                                                                       // :687
}
// delete is OUTSIDE and AFTER the try/catch                             :701-710
```

Requirement-by-requirement:

| Requirement | Result | Evidence |
|---|---|---|
| Owns transaction lifetime | Yes | `await using` scope is local to the method; the abstraction holds no state |
| Begins before any required DB write | Yes | `BeginAsync` at `:560`, first write at `:578`. Both fail-closed returns (`:359` unresolved Boss, `:399` already durable) precede it, so no transaction is opened for a battle end that writes nothing |
| Commits before `_battles.DeleteAsync` | Yes | `CommitAsync` at `:662` inside the `try`; the delete at `:703` is after the `catch` that rethrows |
| Redis deletion unreachable when commit fails | Yes | A failing commit throws into the `catch` at `:664`, which calls `throw;` at `:687` — control never falls through to `:701`. There is no `finally` and no path from `catch` to the delete |
| Rollback attempted on every failure path | Yes | A bare `catch` (no type filter) around all three writes **and** the commit |
| Original exception preserved | Yes | Bare `throw;` after the rollback — not `throw failure;`. The nested `catch (Exception) {}` around `RollbackAsync` cannot replace it |

**Two deliberate design decisions in the catch block are correct, not defects:**

1. `RollbackAsync(CancellationToken.None)` — the caller's token is
   deliberately *not* passed, so an undo is still attempted on a cancelled
   request rather than abandoned to connection cleanup. This is the right
   choice and is documented at `:672-677`.
2. A failing rollback is absorbed. The original failure is the diagnosis;
   a rollback failure is secondary and `IDbContextTransaction.DisposeAsync`
   remains the framework's own undo path. Letting a rollback error mask the
   causal error would be worse.

**I did not accept the abstraction on its name.** `IBattleEndTransaction`
was traced to `BattleEndTransaction` (§3) and to EF Core's own
`BeginTransactionAsync` on the shared context.

---

## 2. Single DbContext identity proof

**Verified: all five participants resolve the same scoped `GameDbContext`
instance.**

### 2.1 There is exactly one registration and no factory

```text
src/backend/GameServer.Infrastructure/DependencyInjection.cs:34
    services.AddDbContext<GameDbContext>(options => options.UseNpgsql(pgConnectionString));
```

A source-wide search for `new GameDbContext`, `DbContextOptionsBuilder<GameDbContext>`,
`AddDbContext<GameDbContext>`, and `IDbContextFactory` across
`src/backend` returns **exactly one hit** — that registration. `AddDbContext`
registers `GameDbContext` with **Scoped** lifetime by default. There is no
`IDbContextFactory`, so no code path can construct a second, independent
context in production.

### 2.2 All five are scoped, and each takes the context by constructor injection

```text
Infrastructure/DependencyInjection.cs:48   AddScoped<IPlayerRepository, PlayerRepository>()
Infrastructure/DependencyInjection.cs:54   AddScoped<IPetRepository, PetRepository>()
Infrastructure/DependencyInjection.cs:82   AddScoped<IBattleResultRepository, BattleResultRepository>()
Infrastructure/DependencyInjection.cs:95   AddScoped<IBattleEndTransaction, BattleEndTransaction>()
Application/DependencyInjection.cs:68      AddScoped<BattleResultService>()
```

Each implementation stores the injected context in a field and uses that
same field for every operation:

```text
BattleResultRepository.cs:42-47   _dbContext = dbContext   →  :60, :86, :88, :106, :153
PlayerRepository.cs:18-20         _dbContext = dbContext   →  :196, :207, :211, :230
PetRepository.cs:24-29            _dbContext = dbContext   →  :34, :43, :95, :115, :131
BattleEndTransaction.cs:38-43     _dbContext = dbContext   →  :53
BattleResultService.cs:141-147    holds the four boundaries + the transaction
```

No repository has a factory, a `new GameDbContext(...)`, a service-locator
call, or a second constructor parameter that could reach a different
context.

### 2.3 The one scope that encloses the operation

`ScopedBattleResultPersistence` (`IBattleResultPersistence.cs:94-115`) is
the composition-root adapter that bridges the singleton
`BattleStateService` to the scoped boundary:

```text
using var scope = _scopeFactory.CreateScope();                    // :109
scope.ServiceProvider.GetRequiredService<BattleResultService>()   // :111
```

`CreateScope()` produces **one** `IServiceScope`. Every scoped resolution
inside that call — `BattleResultService`, and therefore transitively its
`IBattleResultRepository`, `IPlayerRepository`, `IPetRepository`,
`IBossDefinitionLookup`, and `IBattleEndTransaction` — is served from that
scope's single `GameDbContext`. Nothing in the call creates a nested scope.

### 2.4 Composed-graph proof

`BattleEndAtomicityPostgresTests.ComposedBattleEndPath_ShouldRollBack_WhenAParticipatingProgressionWriteFails`
(`:590-653`) builds the **real DI graph** with `AddInfrastructureServices` +
`AddApplicationServices`, resolves `BattleResultService` from a real scope,
and asserts the rollback is observable in PostgreSQL. The one injected
failure is decorated over `new PetRepository(provider.GetRequiredService<GameDbContext>())`
(`:626`) — explicitly the scope's context — so the test would fail if the
registered transaction were bound to a different instance than the
registered repositories.

**Accidental multiple-DbContext behaviour is ruled out.**

---

## 3. Real EF transaction semantics

**Verified: repository `SaveChangesAsync` calls genuinely enlist in the
transaction opened by `BattleEndTransaction`.**

```text
BattleEndTransaction.BeginAsync                                    :46-58
    _dbContext.Database.BeginTransactionAsync(ct)                  :53-55
        ↓  returns Scope wrapping the IDbContextTransaction        :57
```

EF Core semantics: `Database.BeginTransactionAsync` on a context binds that
transaction to the context's current connection and sets it as the
context's current transaction. Every subsequent `SaveChangesAsync` on that
**same context instance** executes its SQL on that connection, inside that
transaction. This is precisely the shared-context fact `DATABASE.md` §2
item 4 already records for the Player-creation batch.

The three writes all go through that context:

```text
BattleResultRepository.AddAsync      →  _dbContext.SaveChangesAsync   :88
PlayerRepository.SaveProgressionAsync→  _dbContext.SaveChangesAsync   :230
PetRepository.SaveProgressionAsync   →  _dbContext.SaveChangesAsync   :131
```

Checked against every forbidden mechanism:

| Forbidden | Found? | Evidence |
|---|---|---|
| Second DbContext | **No** | Single registration; no factory (grepped) |
| Independent connection | **No** | All SQL through the one context; no `NpgsqlConnection` opened anywhere in the path |
| `TransactionScope` | **No** | `grep TransactionScope` across `src/backend` → zero hits. `BattleEndTransaction.cs:21-27` explicitly disclaims it |
| Nested independent transaction | **No** | `BeginAsync` called exactly once in the method; the abstraction is a bare wrapper with no nesting or savepoint logic |
| Operation that bypasses the transaction | **No** | The only battle-end DB operations are the six listed above (two reads + insert + two updates + the `AddAsync` existence probe), all on the shared context |

One subtlety worth recording, because it looks like a bypass but is not:
`BattleResultRepository.AddAsync` performs a `FirstOrDefaultAsync`
existence probe at `:60-64` **before** the `Add`/`SaveChangesAsync`. That
probe reads through the same context and therefore the same transaction, so
it sees the transaction's own uncommitted writes consistently. It is a read,
not a write, and cannot commit anything.

---

## 4. Failure propagation

Both failure classes are handled, and the previously ignored `bool` is now
the load-bearing fix.

### 4.1 `SaveProgressionAsync(...) == false`

The source of `false` is real, not synthetic. Both repository
implementations return `false` when the row is absent:

```text
PlayerRepository.cs:217-220   if (stored is null) { return false; }   (detached-entity branch)
PetRepository.cs:123-126      if (stored is null) { return false; }
```

Both call sites previously ignored this value. They now raise:

```text
BattleResultService.cs:617-624   Player track → throw InvalidOperationException
BattleResultService.cs:639-646   Pet track    → throw InvalidOperationException
```

Property-by-property confirmation:

| Property | Player `false` | Pet `false` |
|---|---|---|
| Reaches transaction owner | Yes — thrown inside the `try` at `:564` | Yes — thrown inside the same `try` |
| Rollback occurs | Yes — bare `catch` `:664` → `RollbackAsync` `:680` | Yes |
| Original exception rethrown | Yes — `throw;` `:687` | Yes |
| Redis battle state remains | Yes — delete at `:703` unreachable | Yes |
| `BattleResult` not durable | Yes — insert undone by rollback | Yes |

The exception messages are distinct per track (`"…Player progression row…"`
vs `"…Pet row…"`), and the tests assert this via
`Assert.Contains("Player"/"Pet", exception.Message)` — so the two
"nothing persisted" reports are not collapsed into one indistinguishable
failure.

**Why the `false` fix matters.** Before TASK-217A, a `false` return meant
the battle end silently continued and the `BattleResult` row stayed
durable, stating a `petXpGained`/`newPetXp` that never existed in the
progression it named. Because `BattleResultId` **is** the battle's own
`BattleId` (`DATABASE.md` §1 sourcing item 1), that durable row would then
make the idempotency pre-check at `:381-400` return `true` on every later
attempt — permanently suppressing the reward. The fix closes a real
data-loss path.

### 4.2 `SaveProgressionAsync(...)` throws

Same six properties hold: the throw happens inside the `try`, the bare
`catch` rolls back, `throw;` preserves it, the delete is unreachable, and
the insert is undone.

### 4.3 Ordering note

The Player write precedes the Pet write (`:605` then `:627`), matching the
existing sequential, independent-track shape. A Player failure therefore
means the Pet write is never reached — asserted in
`PlayerProgressionFailure_…` via `Assert.Equal(0, harness.Pets.ProgressionSaveCount)`.

---

## 5. Atomicity proof (PostgreSQL integration tests)

**Verified: the tests genuinely prove `failure → rollback → separate
connection observes no partial writes`.**

### 5.1 The observation is on a separate connection

This is the decisive point, and the suite gets it right.

`ReadDurableStateAsync` (`:270-288`) constructs a **new** `GameDbContext`
over a **new** `NpgsqlDataSource` obtained from its own connection:

```text
private async Task<...> ReadDurableStateAsync(...)
{
    await using var context = CreateContext();        // :273 — a fresh context
    var result = await context.BattleResults.AsNoTracking()...    // :275-277
    var player = await context.Players.AsNoTracking()...          // :279-281
    var pet    = await context.Pets.AsNoTracking()...             // :283-285
}
```

`CreateContext()` (`:107-108`) builds `new GameDbContext(new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(_dataSource!)...)`,
and the rollback assertions are all made **inside an `await using` block
that has already disposed the writing context**:

```text
await using (var context = CreateContext())          // :379 — writer scope
{
    ... PersistTerminalResultAsync(...) → throws      // :388-389
}                                                     // :390 — writer DISPOSED
Assert.Equal(0, battleStates.DeleteCount);            // :394
var (result, playerXp, ...) = await ReadDurableStateAsync(...);   // :399-400
Assert.Null(result);                                  // :402
```

A separate connection observes only committed data. So `Assert.Null(result)`
plus the unchanged XP/Level values are statements about **PostgreSQL's
durable state**, not about a tracked `DbContext`'s in-memory change tracker.
The audit requirement "do not accept tests that only inspect the same
tracked DbContext" is satisfied.

The `AsNoTracking()` calls additionally prevent the reader's own change
tracker from serving a stale cached entity.

### 5.2 The tests cannot pass if `BattleResult` commits before Pet/Player

This is the strongest structural guarantee, and it holds by construction.

The failing suite decorates the **real** `PetRepository` over the **same**
context, and fails only the progression write:

```text
FailingPetProgressionRepository(new PetRepository(context), ProgressionFailure.Throws)  // :384-386
```

Because the decorator delegates every other member to the real repository
(`:690-716`), the `BattleResult` insert and the Player progression update
both execute as **real, committed-if-not-rolled-back** SQL on the same
connection. If the implementation had committed the result row before the
Pet write — the bug under audit — the row would be visible to
`ReadDurableStateAsync` on its separate connection and `Assert.Null(result)`
at `:402` would fail.

The test therefore distinguishes the fixed implementation from the broken
one. **This is a genuine negative control, not a tautology.**

### 5.3 The active-state delete is also probed across connections

`RecordingBattleStateRepository.DeleteAsync` (`:822-836`) opens its own
context and evaluates:

```text
ResultWasDurableAtDelete = await context.BattleResults.AsNoTracking()
    .AnyAsync(row => row.BattleResultId == battleId, cancellationToken);   // :830-832
```

Asserted `True` in both the happy path (`:340`) and the retry (`:557`), and
`DeleteCount == 0` on every failure path.

---

## 6. Commit-before-delete invariant

**Verified: `DB COMMIT ↓ Redis DELETE`, and Redis cannot be deleted before
the commit is durable.**

Three independent lines of evidence:

1. **Control flow.** The delete at `:703` sits after the `try`/`catch` whose
   `catch` unconditionally rethrows at `:687`. No commit failure can fall
   through to it. There is no `finally` block that could execute the delete
   on both paths.

2. **Separate-connection visibility (strong evidence, per the brief).**
   `ResultWasDurableAtDelete` reads the row on a fresh connection *inside*
   the delete call. Since a separate connection sees only committed data,
   this being `True` proves the commit completed before the delete was
   attempted — not merely that it was sequenced before it in source order.
   This is a stronger assertion than a call-order counter.

3. **In-memory ordering probe.** `TheActiveStateDelete_ShouldHappenOnlyAfterTheCommit`
   (`BattleEndAtomicityTests.cs:192-210`) captures
   `harness.Battles.DeleteCount` from an `OnCommit` hook and asserts it is
   `0` at the moment of commit. This catches a hypothetical delete-before-begin
   reordering that the separate-connection probe might not localize.

The delete remains entirely outside the database transaction — no Redis
operation was moved inside it, consistent with `DATABASE.md` §1 item 3
("The delete is a Redis operation and remains **outside** the database
transaction").

---

## 7. Retry and idempotency assessment

**Both verified.**

### 7.1 Failure → retry (XP not duplicated)

`RetryAfterARolledBackAttempt_ShouldRecordTheBattleOnce_AndGrantEachTrackExactlyOnce`
(`BattleEndAtomicityTests.cs:348-401`) and its PostgreSQL counterpart
(`:511-583`) both:

```text
Attempt 1 → Pet write fails → InvalidOperationException
          → rollback → BattleResult absent → Redis state remains
          → Assert.Empty(harness.Results.Rows) / Assert.Equal(0, DeleteCount)
Attempt 2 → success
          → Assert.Single(rows)                     one BattleResult
          → Assert.Equal(100, OwnerPlayer.XP)       one Player grant  (not 200)
          → Assert.Equal(100, Pet XP)               one Pet grant     (not 200)
          → Assert.Equal(1, Battles.DeleteCount)    one Redis delete
```

The "not 200" assertion is the actual duplicate-XP check, and the
PostgreSQL version re-reads the final values on a separate connection, so
it proves the durable XP is 100 rather than merely the in-memory entity's.

Also asserted for the Pet: `Assert.Equal(Pet.LevelForXp(pet.XP), pet.Level)`
— the Level is exactly what the Domain derives from 100 XP, so no hidden
second grant is folded into the instance.

**Why no duplication is possible:** the rollback removes the `BattleResult`
row, so on attempt 2 the pre-check at `:381-400` finds nothing,
`firstDurableWrite` is `true` again, and the grants apply once. Both
attempts are counted (`BeginCount == 2, CommitCount == 1, RollbackCount == 1`).

A subtle pre-existing hazard is worth recording as *correctly handled*: the
grant is applied to the in-memory entity at `:607`/`:629` **after** the
insert but the entity is the same object the in-memory store holds. If the
rollback did not restore the entity's XP, attempt 2 would read an
already-granted entity and double-grant. Both the in-memory transaction
double (`RestoreProgression`) and — crucially — real PostgreSQL (where the
entity is re-read fresh from the database on attempt 2) handle this. The
in-memory double models it explicitly and documents why
(`BattleResultTestDoubles.cs:305-320`).

### 7.2 Success → retry (existing idempotency preserved)

`RepeatedTerminalPersistence_AfterACommit_ShouldStayIdempotent_AndGrantNothingTwice`
(`:434-473`):

```text
Assert.Same(firstRow, Assert.Single(harness.Results.Rows));  // not rewritten
Assert.Equal(100, OwnerPlayer.XP);                           // no duplicate XP
Assert.Equal(1, harness.Transactions.BeginCount);            // no new transaction
Assert.Equal(1, harness.Transactions.CommitCount);           // no duplicate reward
Assert.Equal(1, harness.Battles.DeleteCount);                // no second delete
```

`BeginCount` staying at `1` directly proves the requirement "existing
`BattleResult` → **no new transaction**": the pre-check at `:385-400`
returns before `BeginAsync` at `:560`. `Assert.Same` proves the stored row
is the original object, not a rewrite — so `CompletedAt` keeps its
"one value per battle" semantics.

---

## 8. Test quality / non-vacuity assessment

Each item in the brief, checked individually:

| Requirement | Verdict | Evidence |
|---|---|---|
| Pet failure test genuinely fails before commit | **Yes** | `ProgressionSaveFails = true` is set at `:226`, i.e. before the call; the double throws from `SaveProgressionAsync`, which the service reaches at `:639` *before* `CommitAsync` at `:662` |
| Player failure test genuinely fails before commit | **Yes** | Same structure; `:296`, service reaches `:617` before `:662`. `Pets.ProgressionSaveCount == 0` confirms the failure precedes the Pet write too |
| Absent-row tests exercise `false`, not exceptions only | **Yes** | `ProgressionRowAbsentOnSave` returns `Task.FromResult(false)` without throwing (`:433-436` Player, `:657-660` Pet). Separate tests cover it on both tracks, in both suites, and in the composed graph |
| Rollback assertions inspect durable state | **Yes** | PostgreSQL: separate connection + writer context disposed first (§5.1). In-memory: snapshot/restore models the undo explicitly |
| Retry test starts from a failed/rolled-back first attempt | **Yes** | `Assert.ThrowsAsync` on attempt 1 is awaited *before* the flag is cleared and attempt 2 runs. `Assert.Empty(...Rows)` between the attempts asserts absence at that moment |
| Idempotency test starts from a genuinely committed result | **Yes** | The first call's `Assert.True(written)` is asserted, then `Assert.Single(rows)` captures `firstRow` — the repeat starts from a proved-committed row |
| Commit-before-delete test fails if delete preceded commit | **Yes** | `deleteCountWhenCommitted` is sampled inside `OnCommit`; a delete-before-commit ordering yields a non-zero value and fails `Assert.Equal(0, ...)` |
| PostgreSQL tests actually execute rather than skip | **Yes** | `localhost:5433` confirmed reachable; the suite reported **0 skipped** of 422 |

### Non-vacuity of the in-memory transaction double

The brief warns against accepting an abstraction named "transaction". The
in-memory `InMemoryBattleEndTransaction` deserves the same scrutiny, and it
holds up:

* It snapshots all three stores at `BeginAsync` time (in the `Scope`
  constructor, `:768-775`), not at rollback time — so it captures the true
  pre-transaction state.
* `DisposeAsync` also undoes (`:816`), modelling the framework's own
  behaviour rather than only the explicit rollback path.
* Commit and rollback are terminal and throw on a second call (`:780-786`,
  `:796-806`) — an implementation that believed it had undone a committed
  unit of work cannot pass.
* It cannot pass if the service rolls back without the writes having
  happened, nor if the service commits but should not.

Critically, the doubles are also **not more permissive than the contract**:
`InMemoryBattleResultRepository` models one-row-per-battle keyed by
`BattleResultId` (`:87-92`), and both progression doubles return `false` on
an absent row rather than silently succeeding.

The suite is explicit that this is control-flow proof, not database proof —
stated at `BattleEndAtomicityTests.cs:49-55` and
`BattleEndAtomicityPostgresTests.cs:40-46` — and the database proof exists
separately (§5).

### Negative-control evidence

No test is named as a negative control, but the **structural** negative
control in §5.2 is stronger than a named one: the PostgreSQL failure tests
run the real repository for the result insert and the real transaction, so
the pre-fix behaviour (result committed, Pet write failing) would make
`Assert.Null(result)` fail. The tests are falsifiable against the specific
defect TASK-217A fixes.

---

## 9. API test warning handling

**Verified: appropriate, correctly scoped, and not concealment.**

The suppression appears in exactly two places, both **test hosts**:

```text
tests/backend/GameServer.Api.Tests/BattleResultEndpointTests.cs:808-809
tests/backend/GameServer.Api.Tests/BattleHistoryEndpointTests.cs:1103-1104
    .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
```

| Requirement | Verdict | Evidence |
|---|---|---|
| Limited to test hosts using the in-memory provider | **Yes** | Both occurrences are inside `ConfigureWebHost` test fixtures immediately after `.UseInMemoryDatabase(_storeName)`. A source-wide grep for `InMemoryEventId` / `ConfigureWarnings` returns only these two sites |
| No production configuration suppresses transaction warnings | **Yes** | `src/backend` contains zero `ConfigureWarnings` or `InMemoryEventId`. The production registration (`Infrastructure/DependencyInjection.cs:34-35`) sets only `UseNpgsql(connectionString)` |
| PostgreSQL tests remain authoritative | **Yes** | §5 — the separate-connection rollback proofs, 0 skipped |
| Does not hide unrelated EF failures | **Yes** | `InMemoryEventId.TransactionIgnoredWarning` is a single, narrowly-scoped event id. It suppresses only "the in-memory provider does not support transactions". It does not suppress `DbUpdateException`, model-validation errors, save failures, or any other warning. The in-memory provider genuinely cannot open a transaction, so the warning is factually correct and unavoidable for that host |

The comment at each site is honest about what is and is not verified there:
"Nothing about atomicity is verified by this host either way: that is the
PostgreSQL integration suite's".

**Assessment:** The suppression is the correct response to a provider
limitation, is confined to two test files, and is documented with an
explicit statement of what remains unproven. Without it, the API hosts could
not exercise the terminal persistence path at all. This is not a case of
suppressing a symptom of a real defect.

---

## 10. Contract / architecture scope

**Verified: no unintended change to any protected surface.**

| Surface | Changed? | Evidence |
|---|---|---|
| XP formula | **No** | `Player.LevelForXp` / `Pet.LevelForXp` untouched; grants still via `Player.GrantBattleXp` / `Pet.GrantBattleXp` |
| Level formula | **No** | Same |
| XP cap | **No** | `Math.Min(pet.XP + petXpGained, Pet.MaxXp)` at `:482` is unchanged pre-existing logic; `COMBAT_RULES.md` §7.5 item 1 (uncapped Player) unchanged |
| Rewards | **No** | Amounts still read from `Player.BattleWonXpReward` / `BattleLostXpReward` / `Pet.*` (`:434-452`) |
| Pet Tier / Pet Star | **No** | Not referenced on this path |
| Passive mechanics | **No** | Untouched |
| API contract | **No** | No controller, DTO, or route change in the diff |
| SignalR protocol | **No** | No hub or wire-projection change |
| Database schema / migrations | **No** | `git status` shows **no** modified or added `Migrations/` file |
| Redis state semantics | **No** | `BattleStateRepository` untouched; delete semantics unchanged |

### Is `IBattleEndTransaction` a narrow boundary or a Unit of Work?

**It is narrow.** Assessed against `AGENTS.md` §9 and the brief's warning:

```text
BeginAsync(ct) → IBattleEndTransactionScope { CommitAsync, RollbackAsync, DisposeAsync }
```

* Two types, five members, no generic type parameters.
* No `SaveChanges`, no entity registration, no change tracking, no
  ambient/flowing transaction, no `TransactionScope`, no retry policy, no
  compensation, no outbox, no scope stacking.
* It does **not** supersede the repositories — they remain the write
  boundaries, and the transaction merely encloses them.
* It is not registered as a general-purpose service consumed elsewhere:
  `BattleResultService` is its only consumer.
* `ITransactionScope`-style generalization (savepoints, nesting, isolation
  levels, enlistment) is absent.

The doc comment at `IBattleEndTransaction.cs:51-58` explicitly disclaims
the Unit-of-Work reading. The abstraction mirrors the framework transaction
it wraps, which is the minimum needed to keep the Application layer free of
EF Core types (`ARCHITECTURE.md` §2.1 item 1). This is compliant, not
overengineering.

### Pre-existing frontend changes are out of scope

The working tree also contains uncommitted changes under
`src/frontend/client/` (`BattleEventPresenter.ts`, `BattleScene.ts`, two
test files, `standalone-web-smoke.mjs`). These are **not part of
TASK-217A** — they belong to a separate presentation concern (consistent
with the untracked `TASK-218A-relic-trigger-presentation-audit.md` and
`TASK-218C-post-implementation-audit.md` records). No frontend file is
touched by, or required for, the atomicity fix. Recorded here so the diff
scope is not misread as one change set.

---

## 11. Documentation consistency (`DATABASE.md` §1)

**Verified: accurate, and does not overclaim.**

Version bumped 1.36 → 1.37 with a new §1 block, "Battle-end atomicity for
`BattleResult` and the two progression tracks" (6 numbered items).

| Requirement | Verdict | Evidence |
|---|---|---|
| Accurately describes the final implementation | **Yes** | Item 1 names the exact mechanism: "one EF Core transaction — begun and committed on the single scoped `GameDbContext`". Matches `BattleEndTransaction.cs:53` and the scoped registration |
| Does not claim stronger guarantees than the code provides | **Yes** | It claims one transaction over one context — no distributed transaction, no cross-service guarantee, no "exactly once delivery" |
| Correctly states commit-before-delete | **Yes** | Item 3: "the durable battle end commits first; `battle:{battleId}:state` is deleted **only after that commit completed**" — matches `:662` / `:703` |
| Correctly states rollback/retry behavior | **Yes** | Item 4: rollback leaves no durable row, the primary-key guard does not refuse a later attempt, the retry re-runs the whole unit of work and records each track exactly once — matches §7.1 |
| No contradictory persistence rules | **Yes** | Item 5 explicitly re-affirms sourcing item 1's primary-key guard as unchanged and explains atomicity as what makes its premise true. Item 6 re-affirms §2 item 4's "no new persistence abstraction" standard. No rule contradicts §1's existing sourcing items or §2 |

Two points of particular accuracy worth crediting:

* Item 4 correctly notes the two fail-closed returns "**precede** the
  transaction and therefore open none and write nothing" — matching `:359`
  and `:399` sitting before `:560`.
* Item 3 correctly keeps the delete **outside** the transaction and states
  that no Redis behaviour moves into it — matching `:690-710`.

The §1 flow diagram was also updated to show the single `COMMIT` between the
three writes and the delete, which matches the code's actual ordering.

**This audit did not modify the documentation.**

### Documentation drift: none found

The one behavior that is *not* described in the new §1 block is the
caller-level handling in `BattleStateService` (§12 finding B-1). That is
pre-existing documented behavior (`ARCHITECTURE.md` §4 item 3) rather than
drift introduced by TASK-217A, and §1's item 2 describes the boundary's
correct behavior without making a claim about the caller.

---

## 12. Regression review

Affected existing tests and production callers were reviewed. Findings are
classified per the brief.

### 12.1 Regression surface — clean

Every existing test that constructs `BattleResultService` was updated only
to pass the new dependency, with no assertion weakened:

```text
BattleResultServiceTests.cs        + InMemoryBattleEndTransaction to harness
BattleResultTerminalFlowTests.cs   + InMemoryBattleEndTransaction
PlayerXpBattleRewardTests.cs       + InMemoryBattleEndTransaction (4 sites)
PetXpBattleRewardTests.cs          + InMemoryBattleEndTransaction (3 sites)
```

One incidental improvement is visible in `PlayerXpBattleRewardTests.cs`: a
previously anonymous `new InMemoryPetRepository()` was extracted to a named
`pets` local so the same instance can be given to the transaction double. No
assertion changed.

| Regression area | Status | Evidence |
|---|---|---|
| Terminal battle persistence | **Pass** | 653 Application + 422 Infrastructure tests pass |
| `BattleResult` retrieval / history | **Pass** | `BattleResultRepository` untouched; `BattleHistoryEndpointTests` passes |
| Reward summary | **Pass** | `BuildRewardSummary` untouched; summary-vs-progression agreement asserted (`:175-178`) |
| Player progression | **Pass** | `PlayerXpBattleRewardTests` pass; 100 XP / Level 2 asserted |
| Pet progression | **Pass** | `PetXpBattleRewardTests` pass; cap logic at `:482` unchanged |
| Active battle cleanup | **Pass** | Delete-once semantics asserted in both suites |
| Idempotent terminal requests | **Pass** | §7.2 |

### 12.2 Finding A — implementation defect (pre-existing, NOT introduced)

**A total `BattleStateService` failure produces a successful HTTP response.**

Not reported as a defect in the TASK-217A change — it is pre-existing and
this task must not fix it. Recorded because the audit surfaced it while
tracing failure propagation.

* **Location:** `BattleStateService.cs:2850-2873` — `PersistTerminalResultAsync`
  (the private service-level method, distinct from the audited
  `BattleResultService` method).
* **Behaviour:** the call to `_battleResults.PersistTerminalResultAsync` is
  wrapped in `catch (Exception failure) when (!cancellationToken.IsCancellationRequested)`, which
  records `MarkResultNotPersisted(battleId, failure)` and **swallows** the
  exception. The terminal swap therefore returns normally — the client sees
  `IsAccepted = true` and the terminal events.
* **Interaction with TASK-217A:** the new `InvalidOperationException` from a
  `false` progression write is caught here, just as a PostgreSQL failure is.
  For the reward to be recovered, a client must re-submit the terminal
  action and have `BattleStateService` re-invoke the boundary.
* **Does this weaken the fix?** **No.** The durable-state outcome is exactly
  as designed: rollback leaves no `BattleResult` row, Redis still holds the
  authoritative `BattleState`, and a retry can record the battle in full.
  The diagnostic is retained and observable via
  `UnpersistedResultFailure(battleId)` / `UnpersistedResultBattleIds`
  (`:2894-2915`), and the API test surfaces it in its failure message
  (`BattleResultEndpointTests.cs:667`). So the failure is not silent — it
  is unpropagated to the client, which `DATABASE.md` §1 sourcing item 3 and
  `ARCHITECTURE.md` §4 item 3 deliberately require ("the endpoint simply
  finds no row and returns its documented `404` until the write succeeds").
* **Classification:** **pre-existing, by documented design** — not an
  implementation defect of TASK-217A and not a documentation conflict.
  Recorded only so the swallowed exception is not mistaken for a defect in
  the audited change.
* **Suggested follow-up (not created):** none required. If the project later
  wants a client-visible signal for an unpersisted battle end, that is a new
  contract decision requiring an ADR, explicitly out of scope here.

### 12.3 Finding B — test gap (minor, non-blocking)

**No test asserts that a failing `CommitAsync` rolls back and skips the
delete.**

* **Location:** `BattleEndAtomicityTests.cs` — the failure tests all fail a
  *progression* write, never the commit itself.
* **Why it matters:** the brief explicitly asks whether "Redis deletion is
  unreachable when commit fails". This audit verified that property by
  **code-path reading** (§1, §6): a failing commit throws into the `catch`
  at `:664`, which rethrows at `:687`, so `:701` is unreachable. The
  property *does* hold.
* **Why it is still a gap:** it is proven by inspection rather than by an
  executable assertion. A future edit that, say, moved the delete into a
  `finally`, or changed `throw;` to a `return true`, could regress this
  specific property without failing any current test.
* **Severity:** low. `InMemoryBattleEndTransaction` already exposes
  everything needed (`CommitAsync` could be made to throw), and `OnCommit`
  shows the double's extension points exist.
* **Classification:** **test gap**, not an implementation defect. Per the
  brief, a missing test produces `MODIFY` only if it leaves a **critical
  correctness property unproven**. This property is proven — by
  exhaustive control-flow analysis of a 130-line method with no `finally`
  and a single unconditional rethrow — so the gap does not meet that bar.
* **Suggested follow-up (not created per the task constraints):** add one
  case to `BattleEndAtomicityTests` in which `CommitAsync` throws, asserting
  `RollbackCount == 1`, `Results.Rows` empty, and `Battles.DeleteCount == 0`.

### 12.4 Finding C — observation (no action)

The `catch { try { rollback } catch { } throw; }` shape means a rollback
failure is invisible except through EF's disposal path. This is correct
(§1) and deliberate (`:672-677`), but a rollback that fails does not surface
anywhere. In practice EF's `DisposeAsync` on the context scope performs the
same undo, so the database outcome is unaffected. No action recommended.

---

## Final report

### 1. Verdict

**PASS.** The implementation is correct and sufficiently verified. It
delivers true database atomicity: a real EF Core transaction on the single
scoped `GameDbContext`, proven by rollback observed from a separate
connection against real PostgreSQL.

### 2. Transaction owner

`BattleResultService.PersistTerminalResultAsync`
(`BattleResultService.cs:315-713`). It begins at `:560` (after both
fail-closed returns), commits at `:662`, rolls back on a bare `catch` at
`:680`, rethrows the original at `:687`, and deletes Redis at `:703` only
after the commit — outside the `try`, unreachable from the `catch`.

### 3. DbContext identity proof

One registration (`Infrastructure/DependencyInjection.cs:34`,
`AddDbContext<GameDbContext>`, Scoped by default); **zero** other
construction sites, factories, or `new GameDbContext` in `src/backend`. All
five participants are `AddScoped` and take the context by constructor
injection into a field used for every operation.
`ScopedBattleResultPersistence` opens exactly one scope
(`CreateScope()`), from which all of them resolve. The composed-graph
PostgreSQL test proves the DI-wired graph rolls back as one unit.

### 4. Rollback proof

Two layers. **In-memory** (control flow): a snapshot/restore transaction
double that also undoes on uncommitted disposal, with terminal
commit/rollback. **PostgreSQL** (database): the real repositories and the
real `BattleEndTransaction` over one context, with rollback observed by
`ReadDurableStateAsync` on a **separate connection after the writer context
is disposed** — so `Assert.Null(result)` and unchanged XP/Level are
statements about durable PostgreSQL state.

### 5. Commit-before-delete proof

Control flow (delete after an unconditional rethrow, no `finally`); the
`ResultWasDurableAtDelete` probe reading the row on a fresh connection
*inside* the delete call — a separate connection sees only committed data,
so this is strong evidence; and an `OnCommit` hook asserting
`DeleteCount == 0` at commit time.

### 6. Retry / idempotency assessment

**Failure → retry:** one `BattleResult`, 100 XP (not 200) on each track,
one Redis delete; `BeginCount == 2, CommitCount == 1, RollbackCount == 1`.
Verified in both suites, with the PostgreSQL version re-reading final values
on a separate connection.

**Success → retry:** `Assert.Same` on the original row (not rewritten), no
duplicate XP, `BeginCount` stays `1` — proving no transaction is opened —
and one delete.

### 7. Test non-vacuity assessment

All eight required checks pass. Two structural strengths are decisive: the
PostgreSQL failure tests would **fail** under the pre-fix behaviour (real
result insert + real transaction, so a prematurely committed row would be
visible), and the separate-connection reads make the rollback assertions
statements about the database. 422 Infrastructure tests ran with **0
skipped** against a confirmed-reachable PostgreSQL, so nothing passed
through the availability guard.

### 8. Architecture / scope assessment

No change to any XP/Level formula, cap, reward amount, Pet Tier/Star,
passive, API contract, SignalR protocol, schema, or migration (`git status`
confirms zero migration changes). Redis semantics untouched.
`IBattleEndTransaction` is a narrow 2-type, 5-member transaction-boundary
abstraction with no Unit-of-Work generalization, no ambient transaction, and
a single consumer. Uncommitted frontend changes in the tree belong to a
separate concern and are not part of this implementation.

### 9. Documentation assessment

`DATABASE.md` §1 (v1.37) accurately describes the final implementation and
claims nothing stronger than the code provides. Commit-before-delete,
rollback/retry, and the primary-key guard's relationship to atomicity are
all stated correctly, with no contradiction of the existing sourcing items
or §2 item 4. **Not modified by this audit.**

### 10. Defects or follow-up tasks

| # | Type | Severity | Summary |
|---|---|---|---|
| A | Pre-existing, by design | — | `BattleStateService.cs:2856` swallows a total boundary failure after recording it. Required by `DATABASE.md` §1 sourcing item 3 / `ARCHITECTURE.md` §4 item 3. Does **not** weaken the fix: the rollback still leaves the battle recoverable and the diagnostic remains observable. Not fixed; not a TASK-217A defect |
| B | Test gap | Low | No test drives a failing `CommitAsync`. The property (rollback + no delete on commit failure) is proven by control-flow analysis but not by an executable assertion. Does not leave a critical property unproven, so it does not change the verdict. Suggested follow-up: one `BattleEndAtomicityTests` case with a throwing `CommitAsync` |
| C | Observation | — | A rollback failure is fully absorbed. Correct and deliberate; EF disposal provides the same undo. No action |

No implementation defect was found in TASK-217A. Per the brief, only a
missing test that leaves a **critical correctness property unproven** would
warrant `MODIFY`; finding B does not, so the verdict stands as **PASS**.

### 11. Confirmation of artifact scope

**Only the audit artifact was created.**

```text
tasks/completed/TASK-217B-post-implementation-audit.md
```

* No production code, test, documentation, API/SignalR contract, migration,
  repository, or Redis behavior was modified.
* No unrelated issue was fixed.
* No additional task record was created.
* No Git commit, stage, stash, reset, or clean operation was performed.

The only commands executed were read-only inspections
(`git status`, `git diff`, `git log`, `Select-String`, file reads,
`Test-NetConnection`) plus build and test invocations
(`dotnet build`, `dotnet test`) used to verify the claims above. Test runs
created only their own temporary build output; the PostgreSQL integration
suite removes every row it creates via its `CleanupAsync` finally blocks.