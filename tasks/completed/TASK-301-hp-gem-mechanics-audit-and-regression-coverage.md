# TASK-301 — HP Gem Mechanics Audit and Regression Coverage

---

## Metadata

```text
Task ID:           TASK-301
Type:              BUG
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     testing
Supporting Agents: gameplay, backend, review
Workflow:          development/bug-fix.md
Skills:            gameplay/gameplay-behavior-derivation, gameplay/authority-determinism-audit,
                   testing/test-scenario-generation, quality/implementation-review,
                   quality/scope-validation
Dependencies:      TASK-260, TASK-261, TASK-299
Declared Files:    tests/backend/GameServer.Application.Tests/HpGemMechanicsAuditTests.cs
```

---

## Objective

Audit HP Gem mechanics end to end — from match confirmation through heal
resolution to state synchronization — against the authoritative rules, confirm
or refute each documented HP Gem contract with code-level and executed-test
evidence, and fix only the deviations that are actually confirmed. This task does
**not** continue TASK-300's balance re-baselining, does not tune any HP value, and
does not touch ATK, POW, or DEF Gem behavior.

---

## Authoritative References

- `docs/01-game-design/GAME_RULES.md` §1.4, §17 steps 12/14/18a — turn lifecycle and the resolution position of resource generation, Power, player effects, and the Boss response
- `docs/01-game-design/COMBAT_RULES.md` §2 items 5–6 — per-Gem BaseOutput, unique-cell counting, cascade accumulation, actor-relative ownership
- `docs/01-game-design/COMBAT_RULES.md` §4 items 1 and 7 — overheal clamp, dead-target prohibition, Heal Resolution and its Pet-only modifier scope
- `docs/01-game-design/MATCH3_RULES.md` §3.3, §5.7 — cleared-cell union and per-pass accumulation
- `docs/01-game-design/BOSS_RULES.md` §5 item 4, §6.2.2 — Boss terminal check before the Boss response; Thủy Ma's heal modifier
- `docs/02-technical/GAME_STATE.md` §2.3, §3, §5.1 — HP as authoritative Pet/Boss state, transient pools, one write-back per action
- `docs/02-technical/GAME_EVENTS.md` §1.1, §2 — no heal event exists; HP reaches the client as settled state
- `docs/03-decisions/ADR/ADR-026-*` — shared board, actor-relative resource ownership, atomic Round
- `docs/03-decisions/ADR/ADR-025-*` — action idempotency (retries must not re-apply a resolution)

---

## Scope

### In Scope
- Audit of the HP Gem resolution path: `MatchDetector` → `CascadeResolver` → `ResourceGenerator.Generate` → `ApplyHeal` / `ApplyBossHeal` → the single write-back → `BattleStateUpdated`
- Audit of the eight contract areas named in the task: output calculation, match counting, target selection, healing calculation, dead targets, lifecycle ordering, state consistency, and the named edge cases
- Evidence for each finding from code inspection plus executed tests
- Focused regression coverage for the audited invariants that existed with no direct test
- One task record documenting the audit and its evidence

### Out of Scope
- Any HP output value, rate, cap, or balance parameter (TASK-300 is not continued here)
- ATK, DEF, or POWER Gem behavior, including TASK-299's DEF buff
- Thủy Ma's modifier magnitude/duration, Mộc Yêu's regeneration cadence, Boss definitions, card/relic heal values
- Protected balance artifacts, benchmarks, and baseline matrices
- The two pre-existing Application failures and four pre-existing Infrastructure failures recorded by TASK-300

---

## Current State

The HP Gem path is implemented across `ResourceGenerator` (generation, Pet heal, Boss heal, actor-relative dispatch), `HealResolution` (Heal Resolution step, dead-target guard), `SwapExecutor` (Player step 14), and `BattleStateService` (Boss step 18a, plus the Player's step 13 Power write). Per-Gem rate, clamp, dead-target guard, Thủy Ma's modifier, cascade accumulation, and the "no heal event" rule all had unit-level coverage before this task. HP Gem **actor/target isolation** — the acting actor owns its own Heal pool and never the opponent's — had no direct test, and the Boss turn's heal was only ever pinned by replaying the production stages.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-301:
```text
tests/backend/GameServer.Application.Tests/HpGemMechanicsAuditTests.cs
```

*(Note: The task manifest file itself is excluded from the implementation declared file set. The list above strictly matches the `Declared Files:` field in `## Metadata`.)*

---

## Acceptance Criteria

- [x] HP Gem output, match counting, target selection, healing calculation, dead-target behavior, lifecycle ordering, and state consistency are each verified against the authoritative documents with expected-vs-actual evidence.
- [x] Every confirmed defect is fixed with the smallest correct change and covered by a focused regression test. (No defect was confirmed; no production code was changed.)
- [x] The audited invariants that had no direct test are covered by a focused regression suite.
- [x] Focused HP Gem tests pass.
- [x] The relevant regression suites were executed and their results recorded, including pre-existing failures.
- [x] No ATK/DEF/POW Gem behavior, balance parameter, protected artifact, or unrelated working-tree change was modified.
- [x] Documentation was updated only if verified behavior required it. (It did not; no behavior changed.)

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[x] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable)
```

---

## Implementation Notes

- Existing code under audit: `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs` (`Generate`, `HpGemBaseHeal`, `ApplyHeal`, `ApplyBossHeal`, `ApplyActorMatchEffects`), `src/backend/GameServer.Domain/Combat/HealResolution.cs`, `src/backend/GameServer.Domain/Match3/SwapExecution.cs`, `src/backend/GameServer.Application/Battle/BattleStateService.cs` (step 18a call site).
- The new suite lives in the Application test project because it asserts the committed atomic Round's delivered state and events; it reuses the existing `RoundTestSupport` pinned-board fixtures rather than introducing a second board builder.
- Expected values in the suite are derived from the round's delivered `GemMatched`/`DamageDealt` reports, not from re-running the production generation stage, so a wrong actor, double count, or wrong rate fails the equality instead of being masked.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — HP Gem rate, clamp, actor-relative dispatch (existing + new)
[x] Integration tests  — one committed atomic Round through BattleStateService (new)
[x] Gameplay scenarios — Given/When/Then derived from COMBAT_RULES.md §2/§4 and ADR-026 item 4
```

### Key Edge Cases
- `COMBAT_RULES.md` §2 item 6 — overlapping-shape dedup and cascade accumulation (shared, type-agnostic union logic)
- `COMBAT_RULES.md` §4 item 1 — full HP, near-maximum HP, overheal discard
- `COMBAT_RULES.md` §4 items 1/7 — dead target is not healed (Pet and Boss)
- Zero cleared HP Gems — no healing on either side of the actor rule

---

## Stop Conditions

- If required behavior cannot be fully derived from Authoritative References: STOP per `AGENTS.md` §7
- If task requires client-authoritative game logic calculation: STOP per `AGENTS.md` §10
- If the audit finds that the documented rule itself is ambiguous or self-contradictory: STOP that specific change and report it per `AGENTS.md` §4 / §20

---

## Audit Results (Expected vs Actual)

Every row below was established from the authoritative document **first**, then compared with the implementation.

| # | Contract | Expected (authoritative source) | Actual (evidence) | Verdict |
|---|---|---|---|---|
| 1 | Output calculation | `pool(HP) = 20 × count(unique HP cells)` (`COMBAT_RULES.md` §2 items 5–6) | `ResourceGenerator.HpGemBaseHeal = 20`; `Generate` adds `BaseOutputFor(Hp)` once per entry of `PassResult.ClearedGems`; no tier factor, no division (`ResourceGenerator.cs` 318–361) | MATCH |
| 2 | Match counting — one pass | Each cell counted once however many shapes name it (`MATCH3_RULES.md` §3.3 item 3) | `BoardResolver.ResolvePass` builds `ClearedGems` from `MatchDetector.ClearedCellUnion` (`SortedSet<int>`), so an L/T intersection is one entry | MATCH |
| 3 | Match counting — cascades | Pools accumulate per pass; the Swap total is the sum, applied once (`MATCH3_RULES.md` §5.7 item 7) | `Generate` iterates every `pass.ClearedGems`; `SwapExecutor` applies `generation.HealPool` once at step 14 (no per-pass heal) | MATCH |
| 4 | Target selection | Acting side owns the pool: Player → Pet HP, Boss → Boss HP (`COMBAT_RULES.md` §2 HP row; `ADR-026` item 4) | `SwapExecutor` heals `state.PetState`; `BattleStateService` step 18a calls `ApplyActorMatchEffects(DamageParty.Boss, …)` which heals only `bossState` | MATCH |
| 5 | Healing calculation — clamp | `min(HP + FinalHeal, MaxHP)`, overheal discarded, MaxHP unchanged (`COMBAT_RULES.md` §4 item 1) | `HealResolution.ApplyPetHeal` / `ApplyBossHeal` clamp to the state's own `MaxHP` | MATCH |
| 6 | Healing calculation — modifiers | Pet HP only; Thủy Ma −50% is one applicable modifier; Boss HP heal is direct (no modifier resolution) (`COMBAT_RULES.md` §4 item 7) | `ApplyPetHeal` → `ResolveHealAmount(rawHeal, bossStatusEffects)` then clamp; `ApplyBossHeal` applies clamp only | MATCH |
| 7 | Dead targets | No revival at `HP ≤ 0`, Pet and Boss (`COMBAT_RULES.md` §4 items 1/7, `GAME_RULES.md` §1.4) | `ApplyPetHeal`/`ApplyBossHeal` return the state unchanged when `HP <= 0`; the pipeline cannot reach them with a dead target anyway (step-8 Boss terminal check; a dead Pet cannot swap) | MATCH |
| 8 | Lifecycle ordering | Pet heal at step 14 before damage (15–17); Boss heal at 18a after the Player's instance (`GAME_RULES.md` §17) | `SwapExecutor` applies heal in the step-12/14 block before the damage stage; `BattleStateService` applies the Boss heal after `ResolveAndApplyPlayerAttack` | MATCH |
| 9 | Applied once (retries) | A replay re-delivers the committed state, never re-resolves (`ADR-025`) | `ExecuteSwapAsync` returns `SwapExecutionResult.IdempotentReplay(state)` before any resolution; a refused CAS re-runs from fresh state | MATCH |
| 10 | State consistency | No heal event; HP reaches the client as settled state in the one write-back (`GAME_EVENTS.md` §2, `GAME_STATE.md` §5.1) | No heal `BattleEventType` exists; `BattleHub` broadcasts `BattleStateUpdated` from `result.State`; `GameRuntime`/`BattleScene` render the delivered `petState.hp`/`bossState.hp` verbatim | MATCH |
| 11 | Edge — zero HP Gems | Pool 0 → HP unchanged (`COMBAT_RULES.md` §2 item 5) | `ResolveHealAmount(0)` returns 0; exercised end to end by `NoHpGemsCleared_ShouldHealNeitherActor` | MATCH |
| 12 | Edge — full / near-max HP | Stops exactly at MaxHP, overheal stored nowhere | `HpGems_AtFullHp_ShouldStopExactlyAtMaxHp_AndDiscardTheOverheal` | MATCH |

**Confirmed defects: none.** No production code was changed.

**Non-defect observations (reported, not changed):**
- `COMBAT_RULES.md` §2's actor-relative HP bullet says the Boss's HP-Gem heal "pass[es] through Heal Resolution (§4 item 7)", while §4 item 7's Scope clause says the Boss-side HP change "still does not route through this step" and applies only item 1's clamp and the dead-target guard. The implementation follows §4 item 7, which is the canonical owner of the Heal Resolution step and the more specific statement; the §2 wording is a cross-reference that reads slightly broadly. No behavior question is open, so nothing was changed; a future documentation pass may tighten that reference.
- `ResourceGenerator.ApplyHeal`'s own doc comment states a zero pool "leaves HP exactly unchanged, whatever it was"; with `HP > MaxHP` (unreachable in MVP — no temp-HP/overheal mechanic exists) the clamp would lower HP to `MaxHP`. Not reachable, therefore not a defect; noted for completeness only.
- `GAME_RULES.md` §17 places "Update Power" (13) before "Resolve Player Effects" (14), while `SwapExecutor` applies the Pet heal in its step-12/14 block and `BattleStateService` writes Power afterwards. The two writes touch disjoint fields, healing has no Power-dependent modifier in MVP, and no event ordering distinguishes them, so the committed state and event list are identical either way.

---

## Validation Results

| Command | Result |
|---|---|
| `dotnet test tests/backend/GameServer.Domain.Tests/GameServer.Domain.Tests.csproj --filter "FullyQualifiedName~PlayerEffectHealingTests\|…ResourceGenerationTests\|…ActorRelativeMatchEffectsTests\|…DefenseBuffTests"` | PASS — 103/103 |
| `dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj --filter "FullyQualifiedName~HpGemMechanicsAuditTests"` | PASS — 5/5 |
| `dotnet test src/backend/GameServer.sln` (baseline, before the new file) | Domain 1524/1524 PASS; Infrastructure 472/476 (4 pre-existing failures); Application 780/782 (2 pre-existing failures: `TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn`, `TASK296_ReviewD_Canonical75RunCandidate_Verification`, both recorded by TASK-300 as expected mechanical consequences); `GameServer.Api.Tests` did not build — `GameServer.Api` (PID 19308) held `bin/Debug` DLL locks, an environment condition, not a test failure |
| `dotnet test tests/backend/GameServer.Application.Tests/GameServer.Application.Tests.csproj` (full suite, after the new file) | 785 passed / 2 failed / 787 total. Exactly the same two pre-existing failures as the baseline (`TASK195_…`, `TASK296_…`); the 5 new audit tests are the whole +5 delta, so the new suite introduced no regression |
| `GameServer.Domain.Tests` full suite (after the new file) | 1524/1524 PASS — unchanged; no Domain file was modified by this task |
| `dotnet test tests/backend/GameServer.Infrastructure.Tests/…` (after the new file) | 472 passed / 4 failed / 476 total — the same 4 pre-existing Boss-persistence/provisioning failures TASK-300 recorded (`BossPersistenceTests.Task172ProvisioningMigration_…`, `BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_…`, `BossPersistenceTests.ProvisioningMigration_…`, `BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions`). None touches HP Gems |


---

## Completion Evidence

### Commit
- Implementation slice (Phase A): `37a1a39e177112ac7121ce2732c686ced7cd0ca2`
- Subject: `test: add hp gem mechanics audit regression coverage`
- Owns: `TASK-301`
- Files: `tests/backend/GameServer.Application.Tests/HpGemMechanicsAuditTests.cs`
- Shared with: `none`
- Unowned / pre-existing: `none` (the untracked TASK-300 files remain untracked and unstaged in the working tree)

### Changed Files
- `tests/backend/GameServer.Application.Tests/HpGemMechanicsAuditTests.cs` — new focused HP Gem audit suite (5 tests): actor/target isolation across one atomic Round, Boss-owned pool isolation, 20-per-unique-cell rate with cascade accumulation, full-HP clamp with overheal discard, and zero-HP-Gem no-op.
- `tasks/completed/TASK-301-hp-gem-mechanics-audit-and-regression-coverage.md` — this record.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (the client renders delivered `hp`/`maxHp` only)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no balance parameter, protected artifact, or TASK-299/TASK-300 artifact was modified

### Final Status
`DONE` — audit and verification complete, no defects confirmed, no production code changed. Phase A implementation commit recorded (`37a1a39e177112ac7121ce2732c686ced7cd0ca2`); Phase B completion record filed in `tasks/completed/`.

---

## Lifecycle Transition — DONE (Phase B Filing)

<!--
  LIFECYCLE TRANSITION RECORD: IN REVIEW -> DONE
  Governed by tasks/TASK_LIFECYCLE.md §1, §3, §4, §6.1, §6.4 and Operator Authorization.
-->

* **Transition:** `IN REVIEW` → `DONE`
* **Authorizing Basis:** Operator Authorization (TASK-301 Phase A/B commit execution).
* **Repository Policy:** `tasks/TASK_LIFECYCLE.md` §1 (DONE state definition), §3 (completed/ location), §4 (file move: `active/` → `completed/`), §6.1 (Phase B completion record filing commit).
* **Destination Path:** `tasks/completed/TASK-301-hp-gem-mechanics-audit-and-regression-coverage.md`
* **Phase A Commit SHA:** `37a1a39e177112ac7121ce2732c686ced7cd0ca2`
