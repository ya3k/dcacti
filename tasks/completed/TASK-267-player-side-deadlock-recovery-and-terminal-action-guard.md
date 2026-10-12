# TASK-267 — Player-Side Deadlock Recovery and Terminal Action Guard

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  Authorization: the four Product Owner decisions approved in TASK-266 (D1 board
  validity invariant, D2 rule ownership in MATCH3_RULES.md, D3 commit-boundary
  recovery retaining the Boss pre-move recovery, D4 terminal action guard).
-->

---

## Metadata

```text
Task ID:           TASK-267
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/gameplay-change.md
Skills:            gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   testing/test-scenario-generation,
                   quality/documentation-consistency,
                   quality/implementation-review,
                   quality/scope-validation
Dependencies:      TASK-266 (DONE), TASK-265 (DONE), TASK-264 (DONE), TASK-263 (DONE),
                   TASK-261 (DONE), TASK-260 (DONE), TASK-259 (DONE), TASK-258 (DONE)
Declared Files:    docs/01-game-design/MATCH3_RULES.md,
                   docs/01-game-design/GAME_RULES.md,
                   docs/02-technical/GAME_STATE.md,
                   docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md,
                   src/backend/GameServer.Application/Battle/BattleStateService.cs,
                   tests/backend/GameServer.Domain.Tests/RuntimeBoardDeadlockTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs,
                   tests/backend/GameServer.Application.Tests/BattleStateServiceTerminalActionGuardTests.cs
```

---

## Objective

Implement the four Product Owner decisions approved in TASK-266: a playable-board
invariant for every committed non-terminal board (`MATCH3_RULES.md` §1.6), the
authoritative rule home in `MATCH3_RULES.md`, commit-boundary deadlock recovery
inside the existing atomic round while retaining the Boss pre-move recovery, and a
terminal-battle action guard at the Swap action boundary. No new event, wire
discriminator, rejection code, RNG algorithm, generator algorithm, or retry bound
is introduced.

---

## Authoritative References

- `docs/01-game-design/MATCH3_RULES.md` §1.2.1, §1.3–§1.5, §1.6 (new), §2.1.2, §2.1.4, §3, §4.3, §7
- `docs/01-game-design/GAME_RULES.md` §1.4, §16, §17
- `docs/01-game-design/BOSS_RULES.md` §5 item 4, §7
- `docs/02-technical/GAME_STATE.md` §2.0.3, §2.6.2, §5.1
- `docs/02-technical/GAME_EVENTS.md` §1.1, §2, §3 item 8
- `docs/02-technical/REDIS_STATE.md` §3, §4
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1, §3.2.26–§3.2.27, §4, §5 item 3, §7
- `docs/03-decisions/ADR/ADR-008*`, `ADR-009*`, `ADR-025*`, `ADR-026*` §8
- `docs/00-overview/MVP_SCOPE.md` §1

---

## Scope

### In Scope
- `MATCH3_RULES.md` §1.6: the playable-board invariant, deadlock definition, commit
  boundary, deterministic bounded recovery, terminal exemption, Player/Boss recovery
  relationship, and server-internal visibility.
- `MATCH3_RULES.md` §2.1.4 item 7: a terminal battle admits no Swap action.
- One commit-boundary check in `BattleStateService.ResolveSwapAsync`, before the
  existing single write-back.
- One terminal-state entry guard in `BattleStateService.ExecuteSwapAsync`, after the
  ADR-025 identity recognition and before any resolution, plus the same guard on the
  fresh state of the CAS retry path.
- Cross-references in `GAME_RULES.md` §17, `GAME_STATE.md` §5.1, `ADR-026` item 8.
- Focused Domain, orchestration, and guard tests.

### Out of Scope
- G-2 (`Combo`/`MatchCount` redesign), any `BoardReshuffled` or other SignalR
  discriminator, the `SwapStarted`/`SwapResolved` contract, TASK-264 presentation,
  combat balance, Boss AI difficulty, gem probabilities, RNG algorithm, board
  generation algorithm, and generation retry limits.
- Frontend work (the change adds no client-facing contract: the guard reuses the
  existing `STALE_ACTION` code, and recovery is server-internal).

---

## Current State

`BossAiEngine.SelectMove` recovers a deadlocked board only before the Boss's own
move; the board the Boss's cascades leave is committed unchecked, and
`BossState.HP == 0` short-circuits the round before that recovery. `ExecuteSwapAsync`
has no terminal-state guard.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-267:
```text
docs/01-game-design/MATCH3_RULES.md
docs/01-game-design/GAME_RULES.md
docs/02-technical/GAME_STATE.md
docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md
src/backend/GameServer.Application/Battle/BattleStateService.cs
tests/backend/GameServer.Domain.Tests/RuntimeBoardDeadlockTests.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs
tests/backend/GameServer.Application.Tests/BattleStateServiceTerminalActionGuardTests.cs
```

*(Note: the task manifest file itself is excluded from the implementation declared
file set, and `tasks/artifacts/*` are regenerated by the balance suites and restored
to `HEAD` — neither is part of the declared set.)*

---

## Acceptance Criteria

- [x] Every committed non-terminal board the Player may act on has at least one legal
      match-producing adjacent swap, evaluated at the round commit boundary.
      (`EnsurePlayableBoard` at the end of `ResolveSwapAsync`; `ResolveSwapAsync` has no
      later board write, and both return sites leave through it.)
- [x] A terminal board is exempt: neither terminal win nor terminal loss triggers
      recovery, and a generation failure cannot invalidate a terminal result.
      (Both sides tested on **deadlocked** boards, where recovery would otherwise fire.)
- [x] The Boss pre-move recovery in `BossAiEngine.SelectMove` is retained unchanged.
      (A pre-Boss deadlock is tested through the real round: the engine reshuffles, the
      Boss acts, and the commit does not recover a second time.)
- [x] A playable board is preserved exactly and consumes no recovery RNG.
      (Committed board and `RngState` compared to the replayed pre-commit stages.)
- [x] The recovered board and the advanced `RngState` are committed by the existing
      single Redis CAS; no second write, round, or persistence path is added.
      (Store re-read equals the returned state; write count increases by exactly one.)
- [x] Recovery uses the battle's existing `Pcg32` stream and the unchanged
      `BoardGenerator.MaxAttempts = 64` bound; exhaustion fails closed before CAS.
      (Stream equality asserted; bound asserted as a literal 64; the fail-closed write
      accounting asserted when the compare-and-set never lands.)
- [x] A new Swap against an already-terminal battle is rejected before any gameplay
      resolution or RNG consumption, reusing the established rejection contract.
      (Both terminal sides; `STALE_ACTION`; no write, no event, no RNG movement.)
- [x] Idempotent replay, missing-battle, invalid-action, and CAS-retry behavior are
      unchanged. (Replay of the terminal action; replay of a recovery action; the legacy
      no-precondition path; the CAS-conflict path; `NO_MATCH_FROM_SWAP` on a live battle.)
- [x] No new event, wire discriminator, rejection code, `BattleState` member, or
      Redis key is introduced. (Enum-set assertions for both sets; the delivered event
      types are compared against the canonical list; no `BoardReshuffled`.)
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application)
[x] src/frontend/client/ — not touched (no client-facing contract change)
[x] tests/ (Domain and Application)
[x] docs/ (domain rule, core rules, technical state, ADR alignment)
```

---

## Implementation Notes

- The runtime deadlock predicate is the existing `BossAiEngine.EnumerateLegalMoves`
  (the same predicate `SwapValidator` accepts a Swap against); no new detector is added.
  `BoardGenerationValidator.HasValidSwap` is generation-time only (`MATCH3_RULES.md`
  §1.4.1) and is deliberately not called at runtime.
- Recovery is `BoardGenerator.Generate(Pcg32)` over the battle's retained stream; the
  advanced state is `rng.CurrentState`.
- The guard reuses `SwapRejectionReason.StaleAction` — the established
  state-precondition rejection — so no rejection code, response shape, or wire member
  is added (`SIGNALR_PROTOCOL.md` §5 item 3 delegates the code to `MATCH3_RULES.md`
  §2.1.2/§2.1.4).
- Deterministic fixtures are pinned by `RoundTestSupport.PlayerMatch` plus a fixed
  `Pcg32` stream position; each scenario asserts its own deadlock precondition.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — Domain: detection, recovery determinism, RNG advance, bound
[x] Integration tests  — Application: commit boundary, terminal paths, CAS, idempotency
[x] Gameplay scenarios — Given/When/Then derived from MATCH3_RULES.md §1.6 and §2.1.4 item 7
```

### Key Edge Cases
- `MATCH3_RULES.md` §1.6.2 item 3 (no unnecessary reshuffle), §1.6.3 item 4 (bounded
  failure fails closed), §1.6.4 (both terminal paths), §1.6.5 item 3 (no double recovery).
- `MATCH3_RULES.md` §2.1.4 item 2 (a retry of the terminal action is still a replay).

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose

---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `439d34cf2e3b0abb3a7c5ab4a7efc9c82497aa17`
- Subject: `feat(match3): keep committed boards playable and reject terminal swaps`
- Owns: `TASK-267`
- Files: `docs/01-game-design/MATCH3_RULES.md`, `docs/01-game-design/GAME_RULES.md`,
  `docs/02-technical/GAME_STATE.md`,
  `docs/03-decisions/ADR/ADR-026-shared-board-gameplay-and-actor-relative-match-resolution.md`,
  `src/backend/GameServer.Application/Battle/BattleStateService.cs`,
  `tests/backend/GameServer.Domain.Tests/RuntimeBoardDeadlockTests.cs`,
  `tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs`,
  `tests/backend/GameServer.Application.Tests/BattleStateServiceTerminalActionGuardTests.cs`
- Shared with: `none`
- Unowned / pre-existing: `.ai/HARNESS_COMPATIBILITY.md`, `.ai/HARNESS_MAPPING.md`,
  `.ai/agents/AGENT_SELECTION.md`, `.ai/agents/orchestrator.md`, `.ai/skills/README.md`,
  `.ai/skills/SKILL_REGISTRY.md`, `.ai/workflow/README.md`, `.gitignore`, `.agents/`,
  `.ai/PROJECT_STATE.md`, `.ai/skills/discovery/task-readiness-assessment.md`,
  `.ai/workflow/core/task-selection.md`, `.claude/` — disclosed in the Phase A commit
  body and **not staged**; all 13 were present before this task began and are
  byte-preserved by it.

### Changed Files
- `docs/01-game-design/MATCH3_RULES.md` — new §1.6 "Runtime Board Deadlock Recovery"
  (§1.6.1 invariant and deadlock definition, §1.6.2 commit boundary, §1.6.3 deterministic
  bounded mechanism, §1.6.4 terminal exemption, §1.6.5 relationship to the Boss pre-move
  recovery, §1.6.6 server-internal visibility); new §2.1.4 item 7 (a terminal battle
  admits no Swap and rejects it as `STALE_ACTION`).
- `docs/01-game-design/GAME_RULES.md` — §17 paragraph: the committed board of a
  non-terminal Round is playable, evaluated after step 18/19a and before the
  `GAME_STATE.md` §5.1 write-back; adds and reorders no step.
- `docs/02-technical/GAME_STATE.md` — §5.1 diagram line and new item 8: the invariant's
  result is part of the single write-back (board and `RngState` together, no second write),
  and a bounded regeneration failure writes nothing.
- `docs/03-decisions/ADR/ADR-026-...md` — item 8 now names `MATCH3_RULES.md` §1.6 as the
  rule's owner and the legal-move enumeration as the runtime predicate in both
  recoveries, plus an in-place amendment recording the ownership change.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — terminal-battle
  guard at the Swap action boundary (after ADR-025 identity recognition, before any
  resolution) and on the fresh state of the CAS retry path; `EnsurePlayableBoard`
  applied at the end of `ResolveSwapAsync` before the single write-back;
  `IsTerminalBattle`; two new private helpers. No other method changed.
- `tests/backend/GameServer.Domain.Tests/RuntimeBoardDeadlockTests.cs` — 10 tests:
  detection (playable vs the deterministic dead board, plus a cross-check against the
  generation-time validator on stable boards), recovery determinism, stream advancement
  by whole 64-selection attempt passes, the unchanged bound, and the bounded-failure
  contract.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceDeadlockRecoveryTests.cs`
  — 13 tests: post-Boss deadlock recovery (with the exact expected board and `RngState`),
  determinism, the Boss-not-reshuffled case, pre-Boss deadlock via the retained Boss
  recovery, no unnecessary reshuffle, terminal win at the Boss check, terminal win and
  terminal loss on **deadlocked** boards (exemption), idempotent replay, snapshot
  recovery, CAS conflict, commit-that-never-lands, and no-extra-event/no-new-discriminator.
- `tests/backend/GameServer.Application.Tests/BattleStateServiceTerminalActionGuardTests.cs`
  — 10 tests: both terminal sides, independence from the state-version precondition, no
  resolution/RNG/write on rejection, no Boss response or new Round, idempotent replay of
  the terminal action, a different action against the same ended battle, missing-battle
  vs terminal rejection, the unchanged rejection-code set, and live-battle compatibility.

### Validation Results

| Suite | Result | Baseline (TASK-261/263 records) |
| :--- | :--- | :--- |
| `GameServer.Domain.Tests` (full) | **PASS** 1623 passed / 0 failed | 1613 → +10 new |
| `GameServer.Application.Tests` (excluding `Balance`) | **PASS** 655 passed / 0 failed | 632 → +23 new |
| `GameServer.Api.Tests` (full) | **PASS** 365 passed / 0 failed | 365 (unchanged) |
| `GameServer.Infrastructure.Tests` (full) | 472 passed / **4 failed** | 476 total; failures pre-existing and unrelated (below) |
| `GameServer.Application.Tests --filter "FullyQualifiedName~Balance"` | 52 passed / **10 failed** | identical to the recorded baseline (52/10) |

- **The 10 balance failures are the recorded TASK-263 baseline set, unchanged and not
  reclassified**: `BalanceEvidenceMeasurementCorrectionTests` ×8
  (`TASK195_PowerSpentTotal_RecordsDrainPowerWithoutPlayerCardCasts` seeds
  20261001–20261005, `TASK195_PerTurnSeries_SatisfiesInvariantOnCastTerminatedRuns`
  seeds 20261002/20261003/20261005), `BalanceSimulatorTests.H06_EveryMetric_IsPopulatedPerItsDefinition`,
  and `BalanceBaselineMatrixTests.TASK194_AllSeventyFiveRuns_CompleteWithCompleteMetrics`.
  They assert archived TASK-194/TASK-197 evidence that the ADR-026 mechanics change
  invalidated; none was repaired, touched, or reclassified.
- **The invariant had no effect on the simulated balance runs (controlled experiment).**
  `BalanceSimulator` drives the production `ExecuteSwapAsync`, so
  `tasks/artifacts/TASK-194-baseline-simulation-results.json` was regenerated twice —
  once with the commit-boundary recovery enabled and once with its call site disabled —
  and the file was **byte-identical** in both runs
  (SHA-256 `DC62B91DC1777590016BCE39E4480BBC0EB20050B462B4F4EE06E1C578A68582`), and
  identical again after the call site was restored. No simulated run reached a deadlocked
  board, which is why the failing set is unchanged. The call site was restored exactly
  (verified: zero experiment markers, identical diff shape, successful build, and the
  same artifact hash) and `tasks/artifacts/` was returned to `HEAD` with
  `git checkout -- tasks/artifacts/`.
- **The 4 Infrastructure failures are pre-existing and unrelated, and were verified so.**
  `BossPersistenceTests` ×2, `BossDefinitionBalanceLookupTests` ×1,
  `BossDefinitionPostgresProvisioningTests` ×1 — e.g. an expected
  `BossSkillDefinition.BaseDamage = 0` against an actual `150` in the boss-definition
  JSON/balance-configuration area. None of those three test classes references
  `BattleStateService` (`InfrastructureRegistrationTests` is the only class in the
  project that does, and it passes). Re-running exactly those three classes with the
  recovery call site disabled produced the **same 4 failures** (92 passed / 4 failed), so
  they are independent of this task's change. This project recorded no baseline in
  TASK-261/263 (recorded there as not runnable); the comparison above is against the same
  tree with this task's change reverted, not against a historical record.
- **Live integration that actually ran:** Redis was reachable (`127.0.0.1:6379`), so
  `Infrastructure.Tests/RedisBattleStateRepositoryTests` executed and passed (19/19, none
  skipped) and the `Api.Tests` Redis-backed smoke/integration tests passed. **Not run:**
  PostgreSQL-backed suites (no PostgreSQL/docker in this environment) and the browser E2E
  and runtime verification harnesses (their prerequisites are absent). None is reported as
  passing.
- **Not run: frontend suites.** This task changes no client-facing contract: no new
  rejection code (the guard reuses `STALE_ACTION`, which
  `src/frontend/client/src/game/scenes/BattleEventPresenter.ts` already maps), no new
  event or discriminator, no hub method, and no state-push member. Running `vitest` would
  not exercise the changed code path.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic: the recovery and the guard are
      server-side Application/Domain work; no client value is derived or trusted, and the
      new tests assert the committed state the store holds.
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1): no new system, content,
      currency, mechanic, event, or wire member.
- [x] Confirmed P-1…P-6 commit policy compliance: Phase A staged exactly the declared
      file set (`git diff --cached --name-status` = 4 M + 4 A, no record, no artifact, no
      unowned path), one commit for the slice, conventional subject with no task ID, body
      carrying `owns:`/`files:`/`shared with:`/`unowned/pre-existing:`; the 13
      unowned/pre-existing paths are disclosed and were not staged.
- [x] Out of scope and untouched: G-2 (`Combo`/`MatchCount`), any new SignalR
      discriminator, the `SwapStarted`/`SwapResolved` contract, TASK-264 presentation,
      combat balance, Boss AI difficulty, gem probabilities, the RNG algorithm, the board
      generation algorithm, and `BoardGenerator.MaxAttempts`.

### Review Pass (`quality/review.md` §1)

- **Correctness — PASS.** The implementation matches `MATCH3_RULES.md` §1.6 and §2.1.4
  item 7, which were authored first (per `development/gameplay-change.md` §3) and
  describe exactly what the code does: the terminal-first exemption, the
  preserve-if-playable rule, the generation mechanism and its bound, and the guard's
  position and rejection. Every behaviour claimed in a doc comment has a test that
  asserts it, and every test's precondition is asserted rather than assumed.
- **Architecture — PASS.** No dependency direction changes: the legality predicate
  (`BossAiEngine.EnumerateLegalMoves`) and the generator (`BoardGenerator`) stay in
  Domain; the ordering and the single write-back stay in the Application boundary
  (`ARCHITECTURE.md` §2.1, §4). No interface, factory, repository, or module was added.
- **Scope — PASS.** Eight declared files changed; nothing else is staged. G-2, the event
  contract, presentation, balance, the RNG algorithm, the generator algorithm, and the
  retry bound are untouched.
- **Tests — PASS** at HIGH depth (`core/validation.md` §2): build, unit, state-transition
  gameplay scenarios on both terminal paths and both deadlock boundaries, integration
  through the store double's documented compare-and-set semantics and the live-Redis
  suites, plus documentation validation. Deterministic fixtures only; no test hopes for
  a cascade to deadlock.
- **Documentation — PASS.** The rule's owner is `MATCH3_RULES.md`; `GAME_RULES.md`,
  `GAME_STATE.md`, and `ADR-026` were updated only where they carry the cross-reference,
  and no document was made to promise an event, code, or member the code does not have.
- **Security — PASS.** No authentication, authorization, session, credential, or data
  exposure surface is touched; the guard only refuses an action on an ended battle.
- **Performance — PASS.** The added work is one legality enumeration on the finished
  board of each committed round, plus a regeneration only when that board is deadlocked.
  No new store read or query is added on the resolution path (`TDD.md` §4.3).
- **Maintainability — PASS.** Two small private helpers and one call site; no new
  abstraction, and the existing legality enumeration and generator are reused rather than
  duplicated (`AGENTS.md` §9).
- **Determinism — PASS.** Server authority is preserved (the server decides the board,
  the recovery, and the rejection); RNG is the battle's seeded `Pcg32` stream only
  (`TDD.md` §6, `ADR-009`); the recovery is a pure function of board and stream position;
  no client value is trusted or derived.

### Remaining Issues
1. **The generation-exhaustion path is not driveable end to end.** `BoardGenerator.Generate`
   fails only after 64 consecutive rejected candidates, which no chosen PRNG state
   produces (the same reason the generation suite drives that contract directly in
   `BoardGeneratorFailureTests`). The failure contract is therefore covered at the Domain
   level (`RuntimeBoardDeadlockTests`) and the write accounting that makes it fail closed
   is covered at the service level (`CommitThatNeverLands_ShouldPersistNoBoardOrRngMutation`),
   not by forcing a real 64-attempt exhaustion. No seam was added to production code for it.
2. **The deadlocked fixtures are searched constants.** The bounded and terminal scenarios
   pin `(GemType, seed)` pairs established by a deterministic search and each asserts its
   own deadlock precondition, so a stale fixture fails loudly; the search itself is not
   retained as a test.
3. **`ResolveBoardAsync` and the Card/PetSkill cast paths were reviewed, not changed.**
   They cannot introduce a deadlock: a committed board is already stable, so
   `ResolveBoardAsync` re-stores it unchanged (its own tests assert that), and the cast
   paths do not touch the board. `ResolveBoardAsync` is reachable only from tests and the
   Api integration test, not from the running hub. No recovery was added there because
   the authoritative round boundary is the Swap commit.
4. **`GAME_EVENTS.md` §3 item 8 was not updated** (the task scoped documentation changes
   to `MATCH3_RULES.md`, `GAME_RULES.md`, `GAME_STATE.md`, and `ADR-026`). Its statement is
   general — "an operation that produces no Battle Event at all" — and remains accurate for
   the new commit-boundary recovery; it names the Boss-phase reshuffle as the documented
   case, and `MATCH3_RULES.md` §1.6.6 now records the second instance.

### Findings of the independent post-commit reviews (reported, not fixed)

An independent adversarial review of the committed slice (implementation and test quality)
was run after Phase A. Its implementation findings are recorded below only if it reported
any; its test-quality findings are recorded here. **They are not fixed in this task**: P-2
requires the Phase A staged set to equal the declared file set exactly and P-6 prohibits a
second implementation commit for the same slice, so hardening the tests is follow-up work
under `AGENTS.md` §16 rather than an edit to the committed slice. None of these findings
changes what the committed code does; each is about how strongly a test proves it.

1. **The `§1.6.3` item 4 exhaustion test is partly vacuous.**
   `RuntimeBoardDeadlockTests.RecoveryExhaustion_ShouldCarryTheAttemptCountAndOfferNoFallbackBoard`
   constructs the exception itself and asserts its own inputs (including the tautological
   `Assert.IsNotType<BoardGenerationResult>(failure)`), so it adds nothing over the existing
   `BoardGeneratorFailureTests`. Follow-up: delete the self-assertions and rely on the
   recorded limitation, or add a minimal seam and assert the throw escaping
   `ExecuteSwapAsync` with zero writes.
2. **The CAS-conflict test cannot discriminate "fresh state".**
   `ForcedConflicts = 1` makes the double refuse the write while leaving the record
   untouched, so the state re-read is character-identical to the one attempt 1 used; a
   regression that dropped `state = fresh;` would still pass. The no-double-commit half is
   proven. Follow-up: drive the scenario over `ContendedBattleStateRepository` with a
   same-`Sequence` competitor whose content differs observably.
3. **The no-extra-events test proves the name set, not the instance list.**
   `RoundTestSupport.BossBoardEvents` returns the *second* contiguous board-event run, so an
   appended third run or an extra non-board event is invisible; one `Assert.All` is a
   tautology over `Enum.GetNames`. Follow-up: assert the delivered list has exactly two
   board-event runs and nothing after the Boss's run (or compare the full ordered type
   sequence) and drop the tautology.
4. **The guard's documented precedence over `§2.1.2`'s checks has no test.**
   `MATCH3_RULES.md` §2.1.4 item 7 states that an action against a terminal battle is
   rejected as `STALE_ACTION` even when it would also fail an earlier §2.1.2 check, and the
   implementation does exactly that — but every terminal scenario uses a *legal* pair, so
   nothing distinguishes "guard fired first" from "the validator would have rejected
   anyway". Follow-up: add one terminal-battle scenario carrying an invalid request
   (out-of-range, `from == to`, diagonal, or adjacency with no match) and assert
   `STALE_ACTION` rather than the validator's reason.
5. **Whole-record comparisons are reference comparisons for the board.**
   `BoardState` is a plain class with no value equality, so `Assert.Equal(result.State, stored)`
   passes only because the in-memory double hands back the same instance. The board-content
   assertions in the same tests (`Cells` element-wise, `RngState`, `CellsEqual`) carry the
   real weight; `SnapshotRecovery_ShouldReturnTheCommittedRecoveredBoard` relies on the weak
   form. Follow-up: use the `CellsEqual` + explicit-member idiom the other suites use.
6. **Two smaller accuracy points.** `TerminalWin_AtTheBossCheck_...` cites §1.6.4 although
   that path returns before the commit boundary ever runs (the exemption is proven by the
   two deadlocked-board terminal tests); and the Domain `Recovery_*` tests call
   `BoardGenerator.Generate`, whose only input is the stream, so their titles imply a board
   input they do not have (the runtime wiring is covered in the Application suite).
   `CommitThatNeverLands_ShouldPersistNoBoardOrRngMutation` asserts `null` for a battle whose
   record still exists, which matches the caller's `BATTLE_NOT_FOUND` mapping but not the
   method's own documented null semantics; the fail-closed half does not need that assertion.
