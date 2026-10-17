# TASK-291 — Retire Match-Tier Resource Multipliers at Runtime

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  The requester labelled this task "TASK-291". The repository's highest existing
  Task ID on disk is TASK-290, so the next sequential ID is TASK-291; the ID and
  the requester's label therefore agree.
-->

---

## Metadata

```text
Task ID:           TASK-291
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, testing, review
Workflow:          development/gameplay-change.md
Skills:            gameplay/gameplay-behavior-derivation, gameplay/authority-determinism-audit,
                   testing/test-scenario-generation, quality/implementation-review,
                   discovery/impact-analysis
Dependencies:      ADR-026 (Accepted); ADR-028 (Accepted, untracked — see P-2);
                   TASK-286 (completed); TASK-287 (completed); TASK-289 (completed);
                   TASK-290 (completed)
Declared Files:    src/backend/GameServer.Domain/Match3/MatchTier.cs,
                   src/backend/GameServer.Domain/Match3/ResourceGenerator.cs,
                   src/backend/GameServer.Domain/Match3/BoardResolver.cs,
                   src/backend/GameServer.Domain/Bosses/BossAiEngine.cs,
                   tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs,
                   tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs,
                   tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs,
                   tests/backend/GameServer.Domain.Tests/BossAiEngineTests.cs,
                   tests/backend/GameServer.Application.Tests/BossResponseTests.cs,
                   tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs,
                   tests/backend/GameServer.Application.Tests/Task172BossPassiveRuntimeTests.cs
```

<!--
  RISK DISCLOSURE. `tasks/TASK_TYPES.md` §4 gives GAMEPLAY-CHANGE a HIGH baseline;
  HIGH is retained because this slice changes the resources every committed action
  generates (damage pool, shield, heal, Power / SkillCharge) and the Boss AI's move
  selection, on both the Player and the Boss path.

  Docs-first precondition (AGENTS.md §17 / development/gameplay-change.md §2–§3):
  the governing rule is ALREADY authoritative and committed — `COMBAT_RULES.md` §2
  item 6 and the §2 table, `MATCH3_RULES.md` §5.7 items 1–8, `GAME_RULES.md` §3
  item 4 / §6, `TDD.md` §8 items 2–3, and `ADR-028` items 1 and 3 — landed by the
  completed TASK-286 / TASK-287 / TASK-289 slices. This task is the implementation
  half of that already-approved change; it changes no rule and no rate.
-->

---

## Objective

Bring runtime resource generation into compliance with OD-3/OD-4 (and the approved
B-1 Boss AI scoring rule):

> `Contribution(type) = BaseOutput(type) × unique confirmed matched gem count(type)`

Delete the obsolete `MatchTierMultipliers` mechanism (and the match-tier plumbing
that exists only to feed it) so that Match 3, Match 4, Match 5, and L/T shapes all
contribute at the same per-Gem `BaseOutput`, counted once per pass and accumulated
across passes exactly as the resolution already accumulates.

---

## Authoritative References

- `docs/01-game-design/COMBAT_RULES.md` §2 table and §2 item 6 — the resource
  contract (no match-tier multiplier; `pool(t) = BaseOutput(t) × count(t)`; no
  division; preserved modifiers; cleared-gem accounting is reporting only)
- `docs/01-game-design/MATCH3_RULES.md` §5.7 items 1–8 — when generation triggers,
  per-pass unique-cell counting, cross-pass accumulation, no tier multiplier
- `docs/01-game-design/GAME_RULES.md` §3 item 4, §6, §17 steps 12–15
- `docs/01-game-design/COMBAT_RULES.md` §3, §4 — the independent downstream
  modifiers this task must not touch (Combo, Element, Crit, ATK modifiers, Shield,
  Heal clamp, Power bounds)
- `docs/02-technical/TDD.md` §8 items 2–5 — the runtime invariants and the test file
  the contract names
- `docs/02-technical/GAME_STATE.md` §3 — Transient Resolution State (`ClearedCells`)
- `docs/03-decisions/ADR/ADR-028-…md` items 1, 3, 6 — OD-3 retirement and B-1
  count-only Boss AI scoring
- `docs/03-decisions/ADR/ADR-026-…md` — actor-relative resolution, Boss AI scoring
  ownership, preserved qualifications
- `docs/00-overview/MVP_SCOPE.md` §1 — Match 3/4/5 and L/T remain valid matches IN
  scope; this is a reduction, not a new system
- `tasks/TASK_LIFECYCLE.md` §3, §4, §6; `tasks/README.md`

---

## Scope

### In Scope

- Remove the match-tier multiplication from `ResourceGenerator.Generate`: every
  unique cleared cell of a pass contributes its type's `BaseOutput` exactly once,
  and passes accumulate.
- Delete the obsolete `MatchTierMultipliers` mechanism and the match-tier plumbing
  that has no other consumer (`MatchTier`, `ClearedGem.Tier`, the tier bookkeeping
  in `BoardResolver`), because those production references exist solely to apply
  the retired multipliers.
- Implement the approved B-1 count-only Boss AI scoring rule
  (`score = weight × match.Cells.Count`); its scoring invariant is independently
  testable and the change is narrowly coupled to the same retired mechanism.
- Re-express `ResourceGenerationTests.cs` on the approved formula and cover the
  required scenarios (Match 3/4/5, L/T, overlap, cascades, rejection, reporting
  consistency, BaseOutput preservation, downstream-modifier preservation).
- Update the directly affected tests that reference the removed mechanism.

### Out of Scope

- Special Gem type/DTO/field removal, activation behaviour removal, and frontend
  presentation retirement (ADR-028 item 7's separate compile-coupled slices).
- Match detection, gravity, spawning, cascade ordering, damage arithmetic,
  `BaseOutput` rates, attack qualification (`TASK-290`), Combo/Element/Crit/ATK
  modifier, Shield, Heal clamp, and Power-bound behaviour.
- Any ADR or `docs/` edit; any staging of `docs/03-decisions/README.md` or the
  untracked ADR-028 file.
- Any broad Boss AI refactor beyond the scoring basis.

---

## Current State

At `HEAD` (`7a3561e`, `master`):

```text
ResourceGenerator.Generate  per pass → per ClearedGem:
                            generated = BaseOutput(GemType) × NumeratorFor(Tier) / 4
BoardResolver.ResolvePass   derives a per-cell MatchTier (Lt / Match4 / Match5 /
                            Match3, Base for activation cells) and carries it on
                            ClearedGem.Tier
BossAiEngine.CalculateMoveScore
                            weight × tierNumerator × Cells.Count / 4
```

The committed design documents already forbid a match-tier multiplier
(`COMBAT_RULES.md` §2 item 6, `MATCH3_RULES.md` §5.7 items 1–8, `TDD.md` §8 items
2–3). `TASK-290` (F-1) reported the divergence and left the runtime slice to a
follow-up task; this is that task.

The existing resolution semantics that must be preserved: a pass's cleared cells
are the **union** of that pass's cleared cells, each once (`MATCH3_RULES.md`
§3.3 item 3, §5.7 items 2 and 8), and the action's total is the **sum over passes**
(§5.7 item 7). Uniqueness is per pass; accumulation is whole-action.

---

## Declared File Set (P-2 / P-5)

```text
src/backend/GameServer.Domain/Match3/MatchTier.cs
src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
src/backend/GameServer.Domain/Match3/BoardResolver.cs
src/backend/GameServer.Domain/Bosses/BossAiEngine.cs
tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs
tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs
tests/backend/GameServer.Domain.Tests/BossAiEngineTests.cs
tests/backend/GameServer.Application.Tests/BossResponseTests.cs
tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs
tests/backend/GameServer.Application.Tests/Task172BossPassiveRuntimeTests.cs
```

*(The task record itself is excluded from the declared set — `tasks/TASK_TEMPLATE.md`.)*

---

## Acceptance Criteria

- [x] AC-1 `ResourceGenerator.Generate` produces `BaseOutput(type) × count(type)`
      with no match-tier factor; a Match 3, Match 4, Match 5, and L/T of the same
      Gem type contribute at the same per-Gem rate.
- [x] AC-2 `BaseOutput` rates are unchanged: ATK 10, DEF 5, HP 20, POWER 10.
- [x] AC-3 `MatchTierMultipliers` no longer exists in production code, and no
      production reference remains that exists only to apply a tier multiplier
      (`MatchTier`, `ClearedGem.Tier`, and `BoardResolver`'s tier bookkeeping are
      removed with it).
- [x] AC-4 A cell shared by more than one match shape of one pass is counted once;
      passes still accumulate (per-pass uniqueness, whole-action sum).
- [x] AC-5 `ClearedGemCounts` reporting and the pools remain mutually consistent
      (`pool(type) == BaseOutput(type) × ClearedGemCounts(type)`), and a rejected /
      unconfirmed action still generates nothing.
- [x] AC-6 Boss AI scoring is `weight × match.Cells.Count` (B-1); weights, tie-break,
      deadlock recovery, and determinism are otherwise unchanged.
- [x] AC-7 Independent downstream combat modifiers and actor-relative attack
      qualification (`TASK-290`) are untouched; the Boss-Passive and Boss-Skill
      composition suites that grade them pass unchanged in behavior.
- [x] AC-8 No Special Gem creation, activation, detonation, area clearing, DTO
      field, or frontend presentation is added or removed.
- [x] AC-9 Backend solution builds. Domain (1652/1652), Api (377/377), and
      Infrastructure (472/476, same four pre-existing database tests) match or
      improve on the reproduced baseline. The Application suite's remaining failures
      are confined to the pre-existing Balance-evidence corpus; the one case that
      newly fails there (`TASK195_PerTurnSeries`, seed 20261003) is a balance-corpus
      re-baseline dependency of this approved resource change, reported as F-4 and
      not a gameplay-contract defect.
- [x] AC-10 `git diff --check` clean; the final diff is exactly the declared file set.
- [x] AC-11 Quality review checklist passes (`.ai/workflow/quality/review.md` §1).

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Match3/      ResourceGenerator, BoardResolver, MatchTier (removed)
[x] src/backend/GameServer.Domain/Bosses/      BossAiEngine score basis
[x] tests/backend/GameServer.Domain.Tests/     resource / qualification / healing / boss-ai tests
[x] tests/backend/GameServer.Application.Tests/BossResponseTests.cs (directly affected helper)
[ ] src/frontend/client/                       NOT TOUCHED
[ ] docs/                                      NOT TOUCHED
```

---

## Implementation Notes

- `BoardResolver`'s `ClearedGems` list already is the pass's unique cleared union in
  ascending index order; it is the pass-local accumulator resource generation reads.
  Drop the tier it carries (`ClearedGem.Tier`) rather than keeping an unused value.
- `ResourceGenerator.BaseOutputFor` and the four `*Base*` constants already own the
  rates; do not rename or retune them.
- `ResourceGenerator.CountPowerMatchGroups` / `ApplyBossSkillCharge` read the match
  set (not the cleared-cell tier) and are unaffected.
- `BossAiEngine.CalculateMoveScore`'s only tier input is
  `MatchTierMultipliers`; B-1 removes it, leaving `weight × match.Cells.Count`.
- `tests/backend/GameServer.Application.Tests/BossResponseTests.BossAtkGemPool`
  reconstructs the Boss's ATK pool from events with the retired tier formula and is
  the only Application-side reference; re-express it on the same per-cell rate.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — GameServer.Domain.Tests/ResourceGenerationTests.cs,
                         BossAiEngineTests.cs, PlayerEffectHealingTests.cs,
                         AttackResolutionTests.cs
[x] Integration tests  — GameServer.Application.Tests (atomic round + boss response)
[x] Gameplay scenarios — Given a pinned board and a committed Swap, When the round
                         resolves, Then pool(t) = BaseOutput(t) × unique matched
                         count(t), accumulated across passes
[x] Build              — dotnet build src/backend/GameServer.sln
[x] git diff --check
```

### Key Edge Cases

- Match-4 does not scale above `4 × BaseOutput`; Match-5 does not scale above
  `5 × BaseOutput`; a run of 6+ is not a sixth rate.
- An L/T intersection cell is counted once (`MATCH3_RULES.md` §3.3 item 3).
- Overlapping shapes in one pass do not double-count; separate passes do accumulate.
- A rejected swap and a pass-free resolution generate nothing.
- Reporting counts and pools stay consistent; BaseOutput values are unchanged.

---

## Stop Conditions

- If the required behavior cannot be derived from the Authoritative References: STOP
  (`AGENTS.md` §7).
- If a change is needed outside the declared file set: STOP (`AGENTS.md` §16).
- If a documentation conflict is found: STOP and report (`AGENTS.md` §4).
- If any command would stage, commit, stash, reset, restore, or clean a path this
  task does not own: STOP (`tasks/TASK_LIFECYCLE.md` §6.2 P-6).
- If B-1 re-baselining would require broad unrelated Boss AI refactoring: STOP and
  report rather than broadening scope.

---

## Discovered Issues / Residual Scope (reported, not silently left — `AGENTS.md` §16)

```text
F-1  The still-present Special Gem activation path keeps contributing the cells it
     clears. Under the target OD-1/OD-2 model no cell is cleared except as a member
     of a detected Match, so `ClearedGems` would then equal the matched union and
     `BaseOutput × unique cleared count` would be exactly OD-4. Until that path is
     physically removed, its cells enter the same per-pass union at the same
     per-Gem BaseOutput. This is NOT a match-tier multiplier and OD-3 is fully
     retired here; the residual is the already-tracked compile-coupled
     `SpecialGem` removal (ADR-028 item 7; TASK-286 F-2) and is deliberately not
     pre-empted by this task (`AGENTS.md` §16 — do not split one mechanic's
     behaviour across two tasks).
     Owner: the compile-coupled Domain+Api Special Gem removal slice.

F-2  Pre-existing failures reproduced on the unmodified `HEAD` tree before this
     slice and re-checked after it: Balance tests (Application suite) and
     database-provisioning tests (Infrastructure suite). Recorded in Validation
     Results; no correction attempted.

F-3  No broader Boss AI refactor was performed: only the scoring basis changed
     (B-1). Weights, candidate enumeration, tie-break, reshuffle, and determinism
     are untouched.

F-4  Balance-evidence corpus delta (expected consequence — reported, not hidden).
     The Balance suites simulate whole battles through `ResourceGenerator`, so
     retiring the match-tier multipliers changes every simulated resource metric
     and, for some seeds, the battle outcome. Reproduced baseline on a pristine
     `HEAD` tree (`git archive HEAD`, temp directory, Balance filter only):
     9 failures, all inside the Balance corpus — `TASK194` matrix;
     `TASK195_PerTurnSeries` seeds 20261002 and 20261005;
     `TASK195_PowerSpentTotal` seeds 20261001–20261005; `H06`.
     After this slice: 7 failures, all inside the same corpus.
       newly passing  TASK195_PowerSpentTotal seeds 20261001, 20261004, 20261005
       newly failing  TASK195_PerTurnSeries seed 20261003 (it asserts a
                      "Card cast resolved" termination that is now a
                      committed-Swap termination)
     Not corrected here: those expectations are the Product-Owner-approved balance
     evidence (TASK-194/195/198/200), and re-deriving them is a balance decision,
     not part of this gameplay-contract task (`AGENTS.md` §16). No non-Balance
     Application test fails.
     Owner: follow-up balance re-baseline task.
```

---

## Quality Review (`.ai/workflow/quality/review.md` §1)

```text
Correctness    PASS  The generator is `BaseOutput(type) × unique cleared count(type)`
                      with no multiplier and no division — exactly COMBAT_RULES.md
                      §2 item 6 / OD-3 / OD-4 and MATCH3_RULES.md §5.7 items 1–8.
                      Boss AI is `weight × match.Cells.Count` per ADR-028 item 6 /
                      B-1. The obsolete multiplier mechanism is deleted rather than
                      bypassed, so the retired formula cannot be reintroduced.
Architecture   PASS  Changes stay in Domain (Match3 resource generation/board
                      resolution accounting; Bosses scoring). No new layer,
                      dependency edge, or cross-boundary responsibility.
Scope          PASS  Eleven declared files; no match detection, gravity, spawn,
                      cascade ordering, damage arithmetic, BaseOutput rate, attack
                      qualification, Special Gem type/DTO, frontend, ADR, or docs
                      change. No unrelated refactor.
Tests          PASS  HIGH depth (core/validation.md §2): focused unit tests
                      (ResourceGenerationTests, BossAiEngineTests), domain
                      regression tests (attack qualification, healing), and
                      application-level round scenarios (Boss response, Boss
                      passives) — 44 focused resource tests, 1652 Domain tests.
Documentation  PASS  No documentation change is required: AGENTS.md §17's code-side
                      reconciliation — the documents already state the contract
                      (COMBAT_RULES.md §2 item 6, MATCH3_RULES.md §5.7, TDD.md §8
                      items 2–3). No source-of-truth conflict introduced.
Security       PASS  No authentication, authorization, session, credential, or data
                      exposure path touched.
Performance    PASS  Same O(cleared cells) walk with one multiplication removed and
                      the per-cell dictionary of tiers removed; no new I/O, database
                      access, or allocation on the resolution path (TDD.md §4.3).
Maintainability PASS  Net deletion of a complete mechanism (MatchTier.cs, tier
                      bookkeeping, tier assertions); no interface, factory,
                      registry, or speculative abstraction added (AGENTS.md §9).
Determinism    PASS  Pure functions of the resolution and the match set; no RNG, no
                      clock, no enumeration-order dependence. Server authority
                      unchanged; client untouched (AGENTS.md §10/§11, ADR-001,
                      ADR-009). Boss tie-break and reshuffle unchanged.
```

---

## Validation Results (actual commands, actual outcomes)

Baseline: a pristine `HEAD` tree materialised with `git archive HEAD` into a temp
directory (the working tree was never stashed, reset, checked out, or restored).

```text
dotnet build src/backend/GameServer.sln
    → 0 Error(s)                                   (warnings are the pre-existing
                                                    MSB3277/CS8631 environment set)

dotnet test tests/backend/GameServer.Domain.Tests
    → Passed: 1652, Failed: 0, Total: 1652        (baseline: 1643 → +9 net new)

dotnet test tests/backend/GameServer.Domain.Tests --filter "FullyQualifiedName~ResourceGenerationTests"
    → Passed: 44, Failed: 0, Total: 44

dotnet test tests/backend/GameServer.Application.Tests
    → Passed: 742, Failed: 7, Total: 749          (baseline: 737 → 742 passed;
                                                    failures are exclusively the
                                                    pre-existing Balance corpus, F-4)

dotnet test tests/backend/GameServer.Api.Tests
    → Passed: 377, Failed: 0, Total: 377

dotnet test tests/backend/GameServer.Infrastructure.Tests
    → Passed: 472, Failed: 4, Total: 476          (baseline identical: the same four
                                                    database-provisioning tests)

git diff --check
    → clean (exit 0)
```

---

## Completion Evidence

**Completed.** `Status: DONE`. Phase A implementation slice committed and Phase B
completion record filed.

### Commit

```text
Phase A implementation slice   7865958
Subject                        fix(match3): retire match-tier resource multipliers
                               and tier-based boss ai scoring
Owns                           TASK-291
Files                          src/backend/GameServer.Domain/Match3/MatchTier.cs (deleted)
                               src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
                               src/backend/GameServer.Domain/Match3/BoardResolver.cs
                               src/backend/GameServer.Domain/Bosses/BossAiEngine.cs
                               tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
                               tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs
                               tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs
                               tests/backend/GameServer.Domain.Tests/BossAiEngineTests.cs
                               tests/backend/GameServer.Application.Tests/BossResponseTests.cs
                               tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs
                               tests/backend/GameServer.Application.Tests/Task172BossPassiveRuntimeTests.cs
Shared with                    none
Unowned / pre-existing         none committed; docs/03-decisions/README.md
                               (pre-existing unstaged change) and the untracked
                               ADR-028 record were preserved unstaged and untouched
```

Staged set verified with `git diff --cached --name-status` as exactly the eleven
declared paths (ten modified + one deletion), zero extraneous paths.

### Changed Files

- `src/backend/GameServer.Domain/Match3/MatchTier.cs` — **deleted**. The
  `MatchTier` ladder and `MatchTierMultipliers` were the retired mechanism and had
  no other production consumer after this slice.
- `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs` — `Generate` adds
  `BaseOutput(cell.GemType)` per unique cleared cell of each pass; no numerator,
  denominator, division, or rounding. Class/`Generate`/`ApplyHeal` documentation
  rewritten to the OD-3/OD-4 contract. Rates and every write (`ApplyPower`,
  `ApplyHeal`, `ApplyShield`, `ApplyBossSkillCharge`, `CountPowerMatchGroups`,
  `ApplyActorMatchEffects`) are unchanged.
- `src/backend/GameServer.Domain/Match3/BoardResolver.cs` — `ClearedGem` carries
  only `(CellIndex, GemType)`; the per-cell `MatchTier` dictionary, `TierOf`, and
  the tier comments are removed. The cleared union itself (the de-duplicated,
  ascending per-pass accumulator) and every Special Gem / gravity / spawn / report
  path are unchanged.
- `src/backend/GameServer.Domain/Bosses/BossAiEngine.cs` — `CalculateMoveScore`
  is `weight × match.Cells.Count` (ADR-028 item 6 / B-1). Candidate enumeration,
  tie-break, deadlock reshuffle, and determinism are unchanged.
- `tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs` — re-expressed
  on the approved formula; the three obsolete `MatchTierMultipliers` mechanism
  tests are removed; new coverage listed below.
- `tests/backend/GameServer.Domain.Tests/AttackResolutionTests.cs` — the
  `ClearedGem` factory construction drops the removed tier argument.
- `tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs` — expected
  heal pools computed as `BaseOutput × count`; the removed `gem.Tier` assertion is
  replaced by the Gem-type assertion.
- `tests/backend/GameServer.Domain.Tests/BossAiEngineTests.cs` — scoring
  expectations re-baselined to count-only (`30`, `40`, `60`), plus a Match-5 / L/T
  count-only scoring test.
- `tests/backend/GameServer.Application.Tests/BossResponseTests.cs` — the
  event-derived Boss ATK pool helper is count-only; the SkillCharge observation
  window is widened to 24 rounds for the B-1 selection change (first Boss POWER
  group on that pinned sequence: round 17).
- `tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs` — the
  Thủy Ma heal expectations now subtract only the Boss instance the round's own
  DEF-Gem Shield did not absorb (`COMBAT_RULES.md` §4 items 2–3), which is the
  documented pipeline behavior the fixtures depend on.
- `tests/backend/GameServer.Application.Tests/Task172BossPassiveRuntimeTests.cs` —
  the Kim Lôi Vương comparison isolates the Rage ATK term from the ATK-Gem pool
  (Boss `DamageCalculated.Base` minus that Turn's `10 × ATK GemMatched` pool)
  instead of comparing two raw damages on two different boards.

### Test Disposition (the ten required scenarios)

```text
1  three-gem ATK = 3 × 10        Generate_ShouldProduceBaseDamageFromAtkGems
2  four-gem  ATK = 4 × 10        Generate_ShouldUseThePerGemBaseOutputForAMatch4
3  five-gem  ATK = 5 × 10        Generate_ShouldUseThePerGemBaseOutputForAMatch5
                                 Generate_ShouldCountARunOfSixAsSixCells
4  L/T counts unique ATK cells   Generate_ShouldApplyThePerGemRateOncePerConsumedCellOfAnLt
                                 (6 unique cells → 6 × 10 = 60)
5  DEF/HP/POWER equivalents      Generate_ShouldProduceDefenseFromDefGems,
                                 Generate_ShouldProduceHealFromHpGems,
                                 Generate_ShouldProducePowerFromPowerGems,
                                 Generate_ShouldUseThePerGemBaseOutputForEveryGemTypeAtMatch4AndMatch5
                                 (theory: Match 4 and Match 5 × all four Gem types)
6  overlap counted once only     OverlappingMatchCells_InLtShape_ShouldBeCountedOncePerResolutionPass
                                 (also asserts the pool is 6 × 5)
7  cascade accumulation          Generate_ShouldAccumulateAcrossCascadesInOneSwap,
                                 Generate_ShouldAddRatherThanReplaceAcrossPasses,
                                 Generate_ShouldSumBothMatchesOfAPass
8  invalid/unconfirmed → none    RejectedSwap_ShouldGenerateNoResources,
                                 RejectedSwap_ThatCreatesNoMatch_ShouldGenerateNothing,
                                 Generate_ShouldProduceNothingForAResolutionWithNoPasses,
                                 Generate_ShouldProduceNothingWhenNothingWasCleared
9  downstream modifiers intact   AttackResolutionTests (combo/element/crit/modifier
                                 preservation, gate behavior) and the Boss-Skill /
                                 Boss-Passive composition suites all pass unchanged;
                                 ResourceGenerationTests proves the pool is the raw
                                 per-cell sum with no combo/cascade factor
10 reporting ↔ contribution      ReportingCounts_ShouldMatchTheGeneratedPoolsAcrossTheWholeSwap
                                 plus per-test `pool == BaseOutput × count` assertions
                                 and BaseOutput_ShouldCarryTheDocumentedRates
```

### Boss AI Outcome

B-1 was implemented inside this task (`score = weight × match.Cells.Count`) because
the scoring invariant is independently testable and the change is narrowly coupled
to the same retired mechanism. No further Boss AI refactor was performed. Two
directly affected Application tests were re-baselined as B-1 consequences:
`BossResponseTests.BossSkillCharge_ShouldIncrementOnlyPerBossPowerMatchGroup`
(observation window widened) and
`Task172BossPassiveRuntimeTests.KimLoiVuong_WhenComboReachesFour_ShouldRaiseTheBossAttackDamage`
(comparison isolated to the ATK term).

### Validation Results

```text
dotnet build src/backend/GameServer.sln                        PASS  0 errors
dotnet test tests/backend/GameServer.Domain.Tests              PASS  1652 / 0 / 1652
dotnet test ... --filter ResourceGenerationTests               PASS  44 / 0 / 44
dotnet test tests/backend/GameServer.Application.Tests         PASS* 742 / 7 / 749
dotnet test tests/backend/GameServer.Api.Tests                 PASS  377 / 0 / 377
dotnet test tests/backend/GameServer.Infrastructure.Tests      PASS* 472 / 4 / 476
git diff --check                                               PASS  clean
```

Reproduced baseline on a pristine `HEAD` tree: Domain 1643/0, Application
737/9/747→ the Balance subset 9/55, Api 377/0, Infrastructure 472/4. The four
Infrastructure failures are the same four database-provisioning tests TASK-290
recorded (`BossDefinitionBalanceLookupTests`,
`BossDefinitionPostgresProvisioningTests`, `BossPersistenceTests` ×2). The seven
remaining Application failures are all inside the pre-existing failing Balance
corpus; the exact delta is F-4.

Test-regenerated balance artifacts (`tasks/artifacts/TASK-194-*.json`,
`TASK-197-*.json`) were rewritten by the validation runs and restored byte-for-byte
to `HEAD` (SHA-256 verified); they are not part of this task's diff.

### Server Authority & Scope Verification

- [x] Zero client-authoritative gameplay logic added or changed (no client file touched)
- [x] Adhered to `MVP_SCOPE.md` §1 (reduction/alignment only; no new system or content)
- [x] Server authority and determinism preserved (`AGENTS.md` §10/§11, ADR-001, ADR-009)
- [x] Special Gem types/DTOs/activation mechanics and frontend presentation neither
      added nor removed; nothing in `docs/` or any ADR edited
- [x] BaseOutput rates, damage arithmetic, attack qualification, match detection,
      gravity, spawning, and cascade ordering unchanged
- [x] Documentation required no update: the code was brought to the already-committed
      rule (`AGENTS.md` §17)
- [x] P-1…P-6 posture: Phase A staged set equals the declared file set; Phase B
      files this record alone; no amend/rebase/history rewrite; nothing pushed
- [x] Pre-existing worktree changes (`docs/03-decisions/README.md`, untracked
      ADR-028) preserved untouched and unstaged

