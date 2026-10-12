# TASK-269 — Persist the Terminal Result on the CAS Contention Fallback

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  Origin: corrective production slice raised by the independent post-commit review of
  TASK-267 (recorded as observation 2(a) in
  tasks/completed/TASK-268-reconcile-match3-documentation-with-the-runtime-recovery-rule.md
  §Remaining Issues), and reported as F-01 by the requester.
  Committed jointly with TASK-270 (shared implementation file) per
  tasks/TASK_LIFECYCLE.md §6.1 Phase A item 5.
-->

---

## Metadata

```text
Task ID:           TASK-269
Type:              BUG
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     backend
Supporting Agents: testing, review
Workflow:          development/bug-fix.md
Skills:            backend/persistence-analysis,
                   gameplay/authority-determinism-audit,
                   testing/test-scenario-generation,
                   quality/implementation-review,
                   quality/scope-validation
Dependencies:      TASK-267 (DONE), TASK-268 (DONE), TASK-270 (joint group commit peer)
Declared Files:    src/backend/GameServer.Application/Battle/BattleStateService.cs,
                   tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs
```

---

## Objective

Make the Swap resolution's retry-exhausted **contention fallback** complete the same
documented battle end as the normal committed path: when that final compare-and-set
succeeds and the resolution's own event list states `BattleWon`/`BattleLost`, the
durable `BattleResult` must be written, the Player/Pet XP grants must be applied, and
the active Redis record must then be cleared — exactly once, in the documented order
(`ARCHITECTURE.md` §4 item 4, `REDIS_STATE.md` §3, `DATABASE.md` §1). No new
mechanism, seam, event, code, or retry bound is introduced.

---

## Authoritative References

- `docs/02-technical/ARCHITECTURE.md` §4 item 4 — the battle end: durable battle end
  completes first, then the active state is cleared; it is one database unit of work.
- `docs/02-technical/REDIS_STATE.md` §3 — "Deleted: explicitly, when
  `BattleWon`/`BattleLost` is resolved and the result has been written to PostgreSQL",
  and the sliding-TTL cleanup path for a failed delete.
- `docs/02-technical/REDIS_STATE.md` §4 items 1–7 — the `Sequence` compare-and-set, the
  abort-and-retry-against-fresh-state rule, one write-back per resolution, and
  "a rejected action writes nothing".
- `docs/02-technical/DATABASE.md` §1 and its sourcing item 3 — one `BattleResult` row per
  battle sourced from authoritative server state; the fail-closed case.
- `docs/01-game-design/GAME_RULES.md` §1.4 — the terminal condition of a battle.
- `docs/02-technical/GAME_EVENTS.md` §2 — `BattleWon`/`BattleLost` are the outcome
  statement; their presence is the only documented end signal.
- `docs/00-overview/MVP_SCOPE.md` §1

---

## Scope

### In Scope
- One call to the existing terminal-result workflow on the final fallback's successful
  commit in `BattleStateService.ExecuteSwapAsync`, placed after the write-back and after
  the once-per-battle Boss Passive consumption, matching the normal committed path's
  order.
- Focused regression tests covering that fallback commit.

### Out of Scope
- The normal loop commit's persistence behaviour (already correct; its tests must keep
  passing unchanged).
- The `MaxAttempts = 8` retry bound, the CAS semantics, the rejected-path behaviour, the
  `Sequence` token, the event contract, and any Redis key or schema.
- Any production seam added only for testability.
- The terminal Card/Pet Skill cast guard (TASK-270, joint group commit) and the deferred
  test-quality findings F-03 … F-07 (`AGENTS.md` §16 — reported, not fixed here).

---

## Current State

`BattleStateService.ExecuteSwapAsync` has two commit sites. The bounded loop's accepted
path calls `PersistTerminalResultAsync(committed.State, committed.Events, …)` after the
write-back (the documented battle end). The path reached when contention does not settle
within the bound — the final `ResolveSwapAsync` plus one more `TryUpdateAsync` — consumes
the once-per-battle Boss Passive and returns the result **without** calling
`PersistTerminalResultAsync`, so a terminal result committed there leaves a terminal
record in Redis with no durable `BattleResult` row, no XP grant, and no active-state
clear.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-269:
```text
src/backend/GameServer.Application/Battle/BattleStateService.cs
tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs
```

*(Note: the task manifest file itself is excluded from the implementation declared file
set. `src/backend/GameServer.Application/Battle/BattleStateService.cs` is **shared** with
TASK-270, which is active at the same time; per `TASK_LIFECYCLE.md` §6.1 Phase A item 5
the two tasks are committed together in one joint group commit and neither is committed
separately. `tasks/artifacts/*` is regenerated by the balance suites and is restored to
`HEAD` — it is not part of the declared set.)*

---

## Acceptance Criteria

- [x] A final fallback commit that is terminal writes the durable result and then clears
      the active state, in that order, exactly as the loop's committed path does.
- [x] The same fallback commit grants Player/Pet XP per the outcome, exactly once.
- [x] A final fallback commit that is **non-terminal** performs no durable write and no
      delete (`PersistTerminalResultAsync` reads the resolution's own events).
- [x] A final fallback compare-and-set that **fails** performs no durable write and no
      delete.
- [x] The fallback performs exactly one state write-back; no second commit site, event,
      rejection code, or retry bound is introduced.
- [x] A replay/retry after a terminal fallback commit produces no second result row and no
      second XP grant.
- [x] The normal loop commit's terminal persistence and the existing CAS, idempotency,
      and rejection behaviour are unchanged.
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/ (Application)
[ ] src/frontend/client/ — not touched (no client-facing contract change)
[x] tests/ (Application)
[ ] docs/ — not touched (the required behaviour is already documented)
```

---

## Implementation Notes

- The ordering that must be reproduced is the loop's committed path, and it is the
  documented one: write-back → consume the once-per-battle Boss Passive firing
  (`BOSS_RULES.md` §6.2.4, `PASSIVE_RULES.md` §4) → persist the terminal result → return.
- `PersistTerminalResultAsync` is called unconditionally on the committed path, because it
  reads the resolution's own event list and returns without writing for a non-terminal
  one; the call site therefore needs no extra terminal predicate, and none is introduced.
- `BattleResultId` is the battle's own `BattleId` (`DATABASE.md` §1 sourcing item 1), so
  the existing row-key is the duplicate protection; no idempotency mechanism is added.
- The fallback already reads its own `SwapResolution`; the events and state it persists
  are that resolution's own values, so no value is recomputed or re-derived.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — not the selected depth; the change is a boundary ordering
[x] Integration tests  — Application: the fallback commit through the real resolution
                         pipeline and the real terminal-result workflow
[x] Gameplay scenarios — Given/When/Then at state-transition level (terminal win,
                         terminal loss, non-terminal, failed commit, replay)
```

### Key Edge Cases
- `REDIS_STATE.md` §4 item 7 — a failed final compare-and-set is not a commit: nothing may
  be persisted and nothing cleared.
- `REDIS_STATE.md` §3 — a failed delete after a successful write keeps the result and
  performs no retry; the row key makes a second row impossible.
- `DATABASE.md` §1 sourcing item 3 — a failed durable write must leave the active state
  intact and the resolution still reported as it happened.

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If the fix appears to require a second commit site, a new retry bound, or a production
  seam: STOP and report per `AGENTS.md` §16 instead of expanding the declared scope

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `80b9defb68eb88b51bd31b0a31d941e1cc46d88e`
- Subject: `fix(battle): persist the terminal result on the CAS contention fallback`
- Owns: `TASK-269`, `TASK-270` (joint group commit — `BattleStateService.cs` is shared)
- Files: `src/backend/GameServer.Application/Battle/BattleStateService.cs`,
  `tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs`
- Shared with: `TASK-270` (`src/backend/GameServer.Application/Battle/BattleStateService.cs`)
- Unowned / pre-existing: `none` — `git status --porcelain --untracked-files=all` was empty at
  preflight; the 13 paths TASK-267/TASK-268 disclose as unowned had already been committed by
  `af64ff6` before this task began, so no unowned working-tree change existed to disclose or
  preserve, and none was staged.

### Changed Files
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — the retry-exhausted
  contention fallback now completes the documented battle end: after its `TryUpdateAsync`
  lands and the once-per-battle Boss Passive firing is consumed, it calls
  `PersistTerminalResultAsync(lastAttempt.Result.State, lastAttempt.Result.Events, …)`
  exactly as the loop's committed path does. The helper's own event-list non-terminal no-op is
  relied on rather than a repeated terminal predicate, so no second write, event, reward, or
  cleanup path is added. No other method changed. (Shared with TASK-270, whose four terminal
  cast guards are the other half of this file's diff.)
- `tests/backend/GameServer.Application.Tests/BattleResultTerminalFlowTests.cs` — 5 new tests,
  plus the Boss-instance round helper gains `forcedConflictsPerRound` and a conforming request.
  The conforming request is required by the forced-conflict path rather than cosmetic: a
  request that states no precondition is refused as stale on its first refused write and never
  retried (TASK-257), so it could not reach the fallback under test, and each Round needs its
  own action identity because the recognition window is one committed Swap wide
  (`MATCH3_RULES.md` §2.1.4 item 2). Existing call sites are unchanged in behaviour
  (`forcedConflictsPerRound` defaults to `0`).

### Validation Results

Every count below is from a `dotnet test` run in this session. The baseline column is an
**isolated `git archive HEAD` tree** (the committed tree, without this session's working-tree
changes), so the comparison is like-for-like rather than against a historical record.

| Suite (command: `dotnet test src/backend/GameServer.sln`) | Post-change | Isolated `HEAD` baseline |
| :--- | :--- | :--- |
| `GameServer.Domain.Tests` | **PASS** 1623 passed / 0 failed (1623) | 1623 / 0 — identical |
| `GameServer.Api.Tests` | **PASS** 365 passed / 0 failed (365) | 365 / 0 — identical |
| `GameServer.Infrastructure.Tests` | 472 passed / **4 failed** (476) | 472 / **4 failed** — identical set |
| `GameServer.Application.Tests` (full, `Balance` included) | 724 passed / **10 failed** (734) | 707 passed / **10 failed** (717) |
| `GameServer.Application.Tests` excl. `FullyQualifiedName~Balance` | **PASS** 672 passed / 0 failed | 655 passed / 0 failed → **+17 new** |
| `GameServer.Application.Tests` `--filter "FullyQualifiedName~Balance"` | 52 passed / **10 failed** | 52 / **10** — unchanged |
| `…~BattleResultTerminalFlowTests\| …~BattleStateServiceTerminalActionGuardTests` | **PASS** 37 passed / 0 failed | n/a (new + grown suites) |

- **The +17 is exactly this task's and TASK-270's new tests** (5 fallback tests here, 12 cast
  tests there), all passing; no existing test changed result, and the `Balance` set is
  numerically unchanged (52 passed / 10 failed) before and after.
- **The 14 pre-existing failures are the recorded TASK-267/268 baseline set.** A programmatic
  `Compare-Object` of the failing test names in the baseline log and the post-change log reports
  **IDENTICAL failing sets**: 10 `Balance` failures (`BalanceEvidenceMeasurementCorrectionTests`
  ×8 — `TASK195_PowerSpentTotal_…` seeds 20261001–20261005 and
  `TASK195_PerTurnSeries_…` seeds 20261002/03/05; `BalanceSimulatorTests.H06_…`;
  `BalanceBaselineMatrixTests.TASK194_…`) and 4 `Infrastructure` failures
  (`BossPersistenceTests.ProvisioningMigration_…`, `BossPersistenceTests.Task172ProvisioningMigration_…`,
  `BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_…`,
  `BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions`).
  All four Infrastructure failures are assertion failures about boss-definition JSON/balance
  configuration data (e.g. expected `BossSkillDefinition.BaseDamage = 0`, actual `150`; expected
  secondary effect `Burn`, actual `AtkBuff`), not reaches of the changed code. **None was
  touched, repaired, or reclassified by this task.**
- **The new tests are discriminating, and that was verified rather than asserted.** The same
  filter was run in a second isolated `git archive HEAD` tree overlaid with only this task's test
  files and TASK-270's Domain carrier — i.e. with `BattleStateService.cs` at `HEAD`, without the
  fix — and reported **12 failed / 25 passed of the same 37**. The 12 are this task's three
  fallback persistence assertions (`FallbackCommit_WithATerminalVictory_…`,
  `FallbackCommit_WithATerminalDefeat_…`, `FallbackCommit_TerminalReplay_…`) plus TASK-270's nine
  guard assertions. The two fallback controls (`FallbackCommit_ThatIsNotTerminal_…`,
  `FallbackCommit_ThatDoesNotLand_…`) pass in both trees **by design**: they assert the absence
  of a write and of a delete, which the defect satisfied too.
- **Live Redis integration actually ran:** `Infrastructure.Tests/RedisBattleStateRepositoryTests`
  — **PASS 19 passed / 0 failed / 0 skipped** on an explicit filtered run (Redis reachable at
  `127.0.0.1:6379`).
- **Not run — no live PostgreSQL.** `127.0.0.1:5432` is closed and `docker` is not installed in
  this environment, so no live-database PostgreSQL integration was verified; the four
  Infrastructure failures above are document/JSON assertions, not a connection failure. **Not
  run:** the browser E2E / local runtime verification harnesses (their prerequisites are absent).
  **Not run: frontend suites** — this task changes no client-facing contract (no new code, event,
  discriminator, hub method, or state-push member). None of these is reported as passing.
- `tasks/artifacts/TASK-194-baseline-simulation-results.json` is regenerated by the `Balance`
  suites; after the runs it was returned to `HEAD` with `git checkout -- tasks/artifacts/` and
  `git status` for that path is empty. It is not part of the declared set.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic: the change is server-side Application
      sequencing; no value is computed or trusted from the client, and the tests assert the state
      the store holds.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1): no new system, content, mechanic,
      event, key, or wire member.
- [x] Confirmed P-1…P-6 commit policy compliance: Phase A staged exactly the union of the two
      declared file sets (5 modified files + 1 shared, no task record, no artifact, no unowned
      path) in one joint group commit naming both tasks; conventional subject carrying no task
      ID; body carrying `owns:`/`files:`/`shared with:`/`unowned/pre-existing:`.

### Review Pass (`quality/review.md` §1)

- **Correctness — PASS.** The fix implements `ARCHITECTURE.md` §4 item 4 / `REDIS_STATE.md` §3,
  which are unambiguous and already documented: a committed resolution whose events state the
  outcome has ended the battle. The call site, its order relative to the write-back and the
  Passive consumption, and its unconditional form all match the loop's committed path, and the
  non-terminal no-op is the helper's documented behaviour rather than an assumption.
- **Architecture — PASS.** No layer, boundary, contract, or state model changes. The write stays
  the single `Sequence`-guarded write-back; the durable step stays behind the existing
  `IBattleResultPersistence`; the delete stays after the write. No interface, factory,
  repository, or module was added.
- **Scope — PASS.** Six declared files; the CAS semantics, retry bound, event contract, Redis
  keys, and the rejected path are untouched, and no production seam was introduced for testability.
- **Tests — PASS** at HIGH depth (`core/validation.md` §2): integration through the real
  resolution pipeline and the real battle-end boundary, plus state-transition gameplay scenarios
  on the terminal-win, terminal-loss, non-terminal, refused-commit, and replay paths, with
  exactly-once assertions on both XP tracks and on the write/delete accounting.
- **Documentation — PASS.** No document change was required and none was made: the required
  behaviour was already stated by `ARCHITECTURE.md` §4 item 4 and `REDIS_STATE.md` §3.
- **Security — PASS.** Nothing touched: no authentication, authorization, session, credential, or
  data-exposure surface.
- **Performance — PASS.** The added work is the same durable battle end the loop's committed path
  already performs, on the rare contention fallback only; no new store read or query is added to
  the resolution path (`TDD.md` §4.3).
- **Maintainability — PASS.** One call site and one comment; the existing helper is reused rather
  than duplicated, and no abstraction was added (`AGENTS.md` §9).
- **Determinism — PASS.** Server authority is preserved; nothing is randomised; the persisted
  values are the resolution's own, so a retried resolution reproduces the same result
  (`REDIS_STATE.md` §4 item 6).

### Remaining Issues
1. **The fallback is a ninth write attempt.** `MaxAttempts = 8` is the loop's bound and the
   fallback performs one more compare-and-set; that shape is `REDIS_STATE.md` §4 item 2's
   existing behaviour and was deliberately not changed (out of scope).
2. **The refusals are arranged, not raced.** The path is driven by the store double's documented
   `ForcedConflicts` switch — the same idiom `BattleStatePersistenceContractTests` uses — so what
   is proven is the service's behaviour on a refused write. The store's own concurrent behaviour
   is `RedisBattleStateRepositoryTests`' subject against live Redis.
3. **Reported, not fixed (`AGENTS.md` §16).** `ResolveBoardAsync` still commits a board with no
   invariant check (test-only callers today), and the five test-quality findings recorded in
   TASK-267's completion record (the deferred F-03…F-07 items) remain open. They are reserved for
   the separate test-hardening task and were not started here.

