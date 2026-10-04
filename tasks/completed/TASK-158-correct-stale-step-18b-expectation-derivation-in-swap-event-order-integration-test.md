# TASK-158 — Correct the Stale Step-18b Expectation Derivation in the Swap Event-Order Integration Test

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy
  game rules, formulas, schemas, or contracts.

  PROVENANCE: the task-generation audit following TASK-153. TASK-153 is DONE
  and its three MVP Boss Passive effects are genuinely implemented in source
  (Hỏa Long's Rage and Mộc Yêu's regeneration applied at GAME_RULES.md §17
  step 18a; Thủy Ma's healing reduction applied at Battle Start and consumed
  by COMBAT_RULES.md §4 item 7). The TASK-154 → TASK-157 decision/documentation
  chain is likewise complete and applied.

  THE DEFECT: with TASK-153 landed, exactly ONE backend test fails:

      GameServer.Api.Tests.ApiIntegrationTests
          .Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly

      Expected {"type":"DamageCalculated","base":250,...,"defense":200,"finalDamage":200}
      got      {"type":"DamageCalculated","base":270,...,"defense":216,"finalDamage":216}

  The test derives its own expectation by calling the documented consumer
  `StatusEffectLifecycle.EffectiveBossAttack` — but it passes the PRE-resolution
  snapshot (`before.BossState.ActiveStatusEffects`, line 2561). The server
  applies Hỏa Long's Rage at step 18a and then derives EffectiveBossATK from the
  POST-18a state at step 18b. The test's premise is written into its own comment
  at lines 2556-2558: "no MVP path applies one yet (Rage's application is the
  unimplemented step 18a half)". TASK-153 implemented exactly that half, so the
  test's stated assumption is now false and its derived expectation is stale.

  THIS TASK IS A TEST BUG, NOT AN IMPLEMENTATION BUG. The implementation is
  correct and is independently asserted by passing tests in other assemblies
  (BossPassiveEffectsTests, BossResponseTests, BossSkillStep1CompositionTests).
  Per development/bug-fix.md §1 this is "Test bug — the test itself asserts the
  wrong thing", and per §4 "Never change behavior merely to make a test pass if
  that behavior contradicts the source of truth."

  THIS TASK RE-DECIDES NOTHING. Every value it aligns the test to
  (EffectiveBossATK = 100 × 120/100 = 120; Step 1 = 120 + 150 = 270) is already
  authored in COMBAT_RULES.md §3.4 / §5.5.1 and BOSS_RULES.md §6.2.1/§6.3.1 and
  is implemented in TASK-153.

  BOUNDARY: ONE test file, ONE expectation derivation. Zero files under
  docs/. Zero production files under src/. No new test scenario, no new
  coverage, no refactor. No task file other than this one is modified —
  TASK-153, TASK-154, TASK-155, TASK-156, and TASK-157 are all immutable.
-->

---

## Metadata

```text
Task ID:           TASK-158
Type:              BUG (TASK_TYPES.md §2 — "Fix behavior that deviates from
                   documented intent… This includes implementation bugs,
                   documentation bugs (docs are stale), and test bugs (test
                   asserts the wrong thing)." This is the third case: the test
                   asserts the wrong thing. It is NOT FEATURE — no documented
                   mechanic is being built; it is NOT GAMEPLAY-CHANGE — no game
                   rule changes; it is NOT DOCUMENTATION — no docs/ file is
                   edited. Per development/bug-fix.md §1 the category is
                   "Test bug".)
Status:            DONE — corrected the stale step-18b expectation derivation
                   in ApiIntegrationTests
                   .Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly
                   and removed the false step-18a comment. Full backend suite
                   green: 2638 passed, 0 failed.
Risk:              LOW (TASK_TYPES.md §4 — BUG baseline LOW–MEDIUM. LOW here:
                   the change is confined to one expectation derivation inside
                   one test method. No production code path is touched, no
                   gameplay value is changed, no contract is altered, no state
                   model is affected. The test's assertion mechanism — whole
                   item, member-for-member comparison against the §3.2 wire
                   representation — is deliberately left intact and unchanged;
                   only the inputs to its own derivation are corrected.)
Priority:          HIGH (the API test assembly is RED: 296/297 pass, 1 fails.
                   A red suite is a release blocker for the MVP-required
                   "Server-authoritative resolution over SignalR" path, and it
                   masks any future genuine regression in the same batch —
                   a failing test that "everyone knows is stale" is exactly the
                   condition under which a real ordering defect would ship
                   unnoticed. TASK-153's Completion Evidence claims a green
                   suite; the current tree does not satisfy that claim.)
Primary Agent:     testing (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   AGENT_SELECTION.md line 27 maps "Bug fix" to a
                   domain-determined agent and line 62 resolves it: "Bug in
                   test assertions → Testing Agent". The defect is in an
                   assertion, not in the Application layer it exercises.)
Supporting Agents: backend (owns the step 18a/18b ordering in
                   BattleStateService.cs and the EffectiveBossAttack consumer;
                   consulted to CONFIRM the implementation's post-18a read is
                   the intended, documented behavior — NOT to change it),
                   gameplay (BOSS_RULES.md §6.2.1/§6.3.1 and COMBAT_RULES.md
                   §3.4/§5.5.1 own the values the corrected expectation must
                   equal; consulted to confirm the Rage-ACTIVE arithmetic is
                   the documented one, not to author it)
Workflow:          development/bug-fix.md
Skills:            testing/test-scenario-generation,
                   discovery/documentation-discovery,
                   gameplay/authority-determinism-audit,
                   quality/documentation-consistency
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-153 (DONE — the implementation whose landed step 18a
                     falsified the test's stated premise. IMMUTABLE; read-only;
                     must NOT be modified),
                   TASK-127 (DONE — closed GAP-1, the Step-1 composition the
                     test's expectation models. IMMUTABLE; read-only),
                   TASK-118 (DONE — the step 18b Boss Skill path the test
                     exercises. IMMUTABLE; read-only)
Blocks:            A green backend suite, and therefore TASK-153's own
                   stated completion claim ("All 2,634 tests green"), which
                   the current tree does not satisfy.
Estimate:          Simple (one test file, one method, one derivation; the
                   correct values are already documented and already asserted
                   by passing tests in other assemblies)
```

---

## Objective

Correct the stale step-18b expectation derivation in
`ApiIntegrationTests.Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly`
so that the test derives `EffectiveBossATK` from the Boss state **after** the
step-18a Boss Passive application, matching the documented
`GAME_RULES.md` §17 ordering and the implemented read, and so the API test
assembly passes at full depth — without changing any production behavior,
any documented rule, or the assertion mechanism itself.

---

## Why This Task Is Next

The audit that produced this task established the true baseline, which differs
materially from the task ledger's apparent state:

```text
TASK-153   DONE — all three MVP Boss Passive effects are implemented in source
                  (step 18a for Hỏa Long/Mộc Yêu; Battle Start for Thủy Ma).
TASK-154   DECIDED — GAP-5 Product Owner decision (Option B).
TASK-155   DONE — applied to BOSS_RULES.md / COMBAT_RULES.md / GAME_STATE.md.
TASK-156   DECIDED — the TargetStat representation decision (Option B).
TASK-157   DONE — applied to GAME_STATE.md §2.3.1 item 7 / schema / §2.3.2.
```

With that chain complete, the MVP battle loop itself is NOT materially
incomplete: battle initialization, session identity, Redis persistence, SignalR
synchronization, Card casting, Pet Skill casting, Relics, Passives, Boss
Passives, Boss Skills, Damage, Crit, Burn, Match-3, turn progression, Boss
Response, Victory/Defeat, Rewards, and ResultScene are all implemented and
documented as such (`ARCHITECTURE.md` §2.2.2; `RELIC_RULES.md` §8.7;
`REDIS_STATE.md` §7).

The one genuinely actionable, unblocked, acceptance-affecting defect in the
current tree is this red test. It is selected because it satisfies every
selection rule:

- **Priority 4** (`test/integration task required to prove an existing
  documented MVP path`) — this is the integration proof for the full §17
  pipeline event ordering over SignalR.
- **Not already done** — it currently **FAILS**; verified by direct execution
  this session, not inferred from a task name.
- **Not blocked** — the authoritative contract exists, the correct values are
  already authored, and passing tests in other assemblies already assert the
  same arithmetic. No Product Owner decision, no ADR, and no new
  representation is required.
- **No ambiguity to resolve** — the classification is decided by
  `development/bug-fix.md` §1 (Test bug) and the evidence is a single
  reproducible diff.
- **No reopening of a completed task** — the fix goes in the test, not in
  TASK-153.

The larger MVP gaps that a naive reading would select first are **blocked** and
are therefore deliberately NOT this task (see "Out of Scope"):

```text
5 Pets / 5 Bosses   MVP_SCOPE.md §1 requires 5 of each; only 3 of each are
                    content-defined. The 2 remaining Pets are deferred because
                    their Signature Skills have no rule (PET_RULES.md §8;
                    CARD_RULES.md §4.1: "not yet content-defined"), and the 2
                    remaining Bosses are explicitly "not yet content-defined"
                    (BOSS_RULES.md §6). Both require a Product Owner rule
                    decision before any implementation task can exist — a
                    AGENTS.md §7 / §20 stop condition, not a build task.
```

---

## Authoritative References

- `AGENTS.md` **§4** (never silently resolve a conflict), **§6** (required
  reading), **§15** (testing requirements; do not modify a test to make an
  incorrect implementation pass), **§16** (task discipline), **§17**
  (documentation change rule), **§20** (stop conditions), **§23** (implement
  documented intent)
- `.ai/README.md` **§16** (determinism), **§17** (testing must verify the full
  state transition, not only the final number), **§18** (classify before
  editing)
- `.ai/workflow/development/bug-fix.md` **§1** (bug category — "Test bug: the
  test itself asserts the wrong thing"), **§2** (flow), **§4** (hard rule:
  never change behavior to make a test pass when the behavior contradicts the
  source of truth)
- `.ai/agents/AGENT_SELECTION.md` — line 27 ("Bug fix" → domain-determined) and
  line 62 ("Bug in test assertions → Testing Agent")
- `tasks/TASK_TYPES.md` **§2** BUG — explicitly includes "test bugs (test
  asserts the wrong thing)"; **§4** risk baseline
- `tasks/README.md` **§9** (no business-rule duplication in task files),
  **§12** (skill budget)
- `docs/01-game-design/GAME_RULES.md` **§17** — the fixed resolution order: the
  step-18a Boss Passive application precedes the step-18b/18c Boss Skill or
  Basic Attack, and step 19a is the single duration-consumption point. **This
  ordering is the entire subject of the defect.**
- `docs/01-game-design/BOSS_RULES.md` **§6.2** / **§6.2.1** — Hỏa Long's Rage:
  `+20%` ATK for 3 turns, its apply point, and its re-trigger refresh rule
- `docs/01-game-design/BOSS_RULES.md` **§6.3** / **§6.3.1 item 1** — Flame
  Burst's authored `150` Skill Base Damage and its additive relationship to the
  ATK term
- `docs/01-game-design/COMBAT_RULES.md` **§3** (the Damage Pipeline steps),
  **§3.4** ("Boss Skill Step-1 composition" — Step 1 is the sum of
  `EffectiveBossATK` and the Skill's authored Base Damage), **§5.5.1**
  (`EffectiveBossATK` derivation), **§5.5.2** (the modifier reaches the Skill's
  Step-1 damage only through the `EffectiveBossATK` contribution), **§5.5.3**
  (the modifier does not touch Step 4)
- `docs/02-technical/GAME_EVENTS.md` **§2** — the `DamageCalculated` payload
  contract the assertion compares against
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3**, **§3.1**, **§3.2** — the
  `ReceiveEvents` batch atomicity, ordering guarantee, and per-event wire
  schema
- `docs/02-technical/TDD.md` **§6** (determinism; the single `RngState` stream)
- `docs/03-decisions/ADR/ADR-009*` — the deterministic PRNG the expectation
  must continue from `playerDamage.UpdatedRngState` (unchanged by this task)
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — **the defect
  site**: lines 2552–2565 (the derivation) and line 2683 (the assertion)
- `tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs` —
  `HoaLongRage_ShouldApplyRageStatusEffectAtStep18a` (line 90): asserts the
  `+20` / `TargetStat = "ATK"` instance at step 18a and that base ATK stays 100
- `tests/backend/GameServer.Application.Tests/BossResponseTests.cs` — line 323
  records the Rage-INACTIVE case (`100 + 150 = 250`) and points at the
  Rage-ACTIVE case (`120 + 150 = 270`) asserted elsewhere
- `tasks/completed/TASK-153-implement-mvp-boss-passive-effects-at-step-18a.md` —
  the landed implementation whose step 18a falsified the test's premise.
  IMMUTABLE; read-only

---

## Current State

The backend suite is **red** at the current working tree:

```text
GameServer.Domain.Tests          1469 passed, 0 failed
GameServer.Application.Tests      508 passed, 0 failed
GameServer.Infrastructure.Tests   364 passed, 0 failed
GameServer.Api.Tests              296 passed, 1 failed   ← the defect
```

The single failure, reproduced directly this session:

```text
GameServer.Api.Tests.ApiIntegrationTests
    .Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly

Error Message:
  Event 46 differs from the §3.2 wire representation.
  Expected {"type":"DamageCalculated","base":250,"comboModifier":1,
            "elementModifier":1,"otherModifiers":1,"defense":200,
            "finalDamage":200},
  got      {"type":"DamageCalculated","base":270,"comboModifier":1,
            "elementModifier":1,"otherModifiers":1,"defense":216,
            "finalDamage":216}.
```

### Root cause

The test builds its own expected event list by calling the documented consumer
`StatusEffectLifecycle.EffectiveBossAttack`, but supplies the **pre-resolution**
snapshot:

```text
ApiIntegrationTests.cs:2431   var before = (await service.GetBattleAsync(battleId))!;
ApiIntegrationTests.cs:2556-2558  // "… no MVP path applies one yet (Rage's
                                  //  application is the unimplemented step 18a
                                  //  half), so the derived value equals the
                                  //  stored stat here."
ApiIntegrationTests.cs:2560-2561  var effectiveBossAtk =
                                      StatusEffectLifecycle.EffectiveBossAttack(
                                          before.BossState.ATK,
                                          before.BossState.ActiveStatusEffects);
```

Meanwhile the server applies Rage at step 18a and reads the modifier at step
18b from the **post-18a** state:

```text
BattleStateService.cs:1442-1460   step 18a — applies "boss-hoa-long-rage"
                                  (magnitude 20, TargetStat "ATK", duration 3)
BattleStateService.cs:1514-1516   step 18b — EffectiveBossAttack(bossState.ATK,
                                  bossState.ActiveStatusEffects)
```

The two therefore disagree by exactly the Rage modifier:

```text
test  : EffectiveBossATK = 100  → Step 1 = 100 + 150 = 250
server: EffectiveBossATK = 120  → Step 1 = 120 + 150 = 270   (100 × 120/100)
```

`before.BossState.ActiveStatusEffects` is empty for this scenario, so the test's
call degenerates to the unmodified stat. The test's own comment states the
assumption that TASK-153 invalidated; the comment and the derivation both need
correcting, and the derivation is the load-bearing part.

### Why this is a test bug and not an implementation bug

The implementation is independently corroborated by passing tests in the other
assemblies, so there is no genuine regression to find:

- `BossPassiveEffectsTests.HoaLongRage_ShouldApplyRageStatusEffectAtStep18a`
  asserts the Rage instance is applied and that base ATK stays `100`.
- `BossResponseTests.BossSkill_ShouldUseBossAttackPlusSkillBaseDamage` asserts
  the Rage-INACTIVE composition (`250`) in a scenario that explicitly arranges
  `Assert.Empty(created.BossState.ActiveStatusEffects)`.
- `BossSkillStep1CompositionTests` asserts the Rage-ACTIVE composition (`270`).

Per `development/bug-fix.md` §4 the correct action is to fix the test, never to
neuter the step-18a application so the old expectation passes.

---

## Scope

### In Scope

1. Correct the `effectiveBossAtk` derivation inside
   `Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly`
   (`ApiIntegrationTests.cs:2552-2565`) so it reflects the Boss state as it
   exists at the step-18b read point — i.e. after the step-18a Boss Passive
   application that the resolution performs in the same Swap.
2. Replace the now-false explanatory comment at lines 2556–2558 (which asserts
   that no MVP path applies a Boss ATK modifier and that step 18a is
   unimplemented) with an accurate statement of the §17 step-18a → step-18b
   ordering it is modelling.
3. Run the API test assembly and the full backend suite, and confirm the
   previously failing test and the whole suite are green.

### Out of Scope

- **Any change under `docs/`.** No authoritative document is modified. Every
  value involved is already authored and is cited, not restated.
- **Any change under `src/`.** No production file is touched. In particular
  the step-18a application, the step-18b `EffectiveBossAttack` read, the
  `HealResolution` path, and `StatusEffect.TurnBased` are all correct and stay
  byte-identical.
- **Weakening or restructuring the assertion.** The whole-item,
  member-for-member comparison against the §3.2 wire representation
  (`ApiIntegrationTests.cs:2681-2687`) and the positive ordering assertions
  that follow it are the value of this test and must remain.
- **Changing any expected gameplay value.** `EffectiveBossATK`,
  `SkillBaseDamage`, the Rage magnitude, and the 3-turn duration are all
  already documented; none is re-derived, rounded, or re-chosen by this task.
- **Adding new test coverage, new scenarios, or new assertions** beyond
  restoring the existing test to correctness. Coverage expansion is a separate
  concern.
- **Fixing the other stale comments found during the audit** (e.g. the
  superseded comment at `BattleStateService.cs:1416-1418` that still says
  "Passive EFFECT application is out of this task's scope … applies nothing"
  directly above the code that now applies it; and the stale
  `BossResponseTests.cs:319-324` wording). These are real but cosmetic
  discrepancies in a **production** file and a **different** test file. They
  are reported here for a follow-up task (`AGENTS.md` §16: report, do not fix
  inline). This task stays in its own file.
- **Correcting `ROADMAP.md`**, which the audit found materially stale (its
  Phase 1 notes "No Relics yet" while `RELIC_RULES.md` §8.7 records six
  `IMPLEMENTED` lines). That is a documentation task with its own
  impact-analysis obligations, not this bug fix.
- The blocked MVP content gaps — the 2 remaining Pets and 2 remaining Bosses
  (`MVP_SCOPE.md` §1; `PET_RULES.md` §8; `CARD_RULES.md` §4.1;
  `BOSS_RULES.md` §6). Both need a Product Owner rule decision first.
- `Burning Curse` (`RELIC_RULES.md` §6 note 3 / §8.5 item 4) — deferred pending
  an unresolved §3-vs-§6-note-1 conflict; not MVP-blocking and not this task.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Dependencies

```text
TASK-153   DONE      — implemented step 18a; the landed behavior this test must
                       model. IMMUTABLE; read-only; must NOT be modified.
TASK-127   DONE      — closed GAP-1 (step-18b Step-1 composition).
                       IMMUTABLE; read-only.
TASK-118   DONE      — implemented step 18b Boss Skill secondary effects; the
                       path this test exercises. IMMUTABLE; read-only.
TASK-154   DECIDED   — GAP-5 decision. IMMUTABLE; read-only; unaffected.
TASK-155   DONE      — GAP-5 documentation application. IMMUTABLE; unaffected.
TASK-156   DECIDED   — TargetStat representation decision. IMMUTABLE; unaffected.
TASK-157   DONE      — TargetStat relaxation applied to GAME_STATE.md.
                       IMMUTABLE; unaffected.
```

No unresolved dependency. No Product Owner decision is required. No ADR is
required: this task changes no architecture, no contract, and no state model —
it corrects a test's own derivation to match an already-decided, already-
implemented, already-documented ordering.

---

## Affected Files / Areas

```text
[x] tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
      — ONE method's expectation derivation and its explanatory comment:
        Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly
        (derivation at lines ~2552-2565)
[ ] src/backend/ (Domain / Application / Infrastructure / Api) — NONE
[ ] src/frontend/client/ — NONE
[ ] docs/ — NONE
[x] tasks/backlog/TASK-158-<this file>.md — this task file only
[ ] all other task files — NONE (TASK-153 … TASK-157 are immutable)
```

---

## Implementation Notes

- **The load-bearing question is only: which Boss state does the step-18b read
  see?** `GAME_RULES.md` §17 places the step-18a Boss Passive application
  before step 18b/18c, and `BattleStateService.cs:1442-1460` mutates
  `bossState` in place at 18a, so the step-18b read at `:1514-1516` sees the
  applied modifier. The test must model that same post-18a state.
- **Prefer modelling the application over hardcoding `120`.** The test's design
  intent (stated at its 2552–2559 comment) is to read through the documented
  consumer rather than assume the number. Preserve that intent: derive the
  expected post-18a effect list the same way the resolution does (i.e. include
  the `boss-hoa-long-rage` instance when the Passive's threshold is met for
  this Swap), then pass that list to `EffectiveBossAttack`. Do not replace the
  derivation with a literal, and do not disable or skip the assertion.
- **Read the scenario's actual Boss Passive configuration before choosing the
  shape of the fix.** Whether this particular rig reaches the Rage threshold in
  the asserted Swap determines whether the corrected expectation legitimately
  contains a Rage instance. Confirm against the rig's Boss definition and
  Swap, and record what you determined — do not assume either way.
- **Do not touch the assertion loop** (`:2681-2687`) or the ordering assertions
  after it. The failure is upstream of them; a member-for-member comparison
  against a correctly derived expectation is exactly what makes this an
  ordering contract test.
- **`ApiIntegrationTests.cs` is an explicitly derived-expectation test**: it
  recomputes the whole batch through the documented pipeline rather than
  hardcoding a golden blob. That is why the defect is a one-line-class
  staleness rather than a mass re-baseline — expect a single localized change,
  not a rewrite.
- **Do not "fix" the test by relaxing it to a subset or a type-only
  comparison.** That would discard the contract it exists to prove
  (`SIGNALR_PROTOCOL.md` §3.1 batch atomicity and ordering) and is prohibited
  by `development/bug-fix.md` §4.
- **Do not modify production code to make the old expectation pass.** Changing
  step 18a's ordering or the step-18b read to restore `250` would contradict
  `GAME_RULES.md` §17, `COMBAT_RULES.md` §3.4/§5.5.1, and `BOSS_RULES.md`
  §6.2.1 — and would break the three passing tests that assert the correct
  behavior.

---

## Acceptance Criteria

```text
[x] The `effectiveBossAtk` derivation in
    Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly no longer
    reads `before.BossState.ActiveStatusEffects` as the modifier source for the
    step-18b composition.
[x] The derivation reflects the Boss state at the step-18b read point,
    consistent with GAME_RULES.md §17 ordering and
    BattleStateService.cs:1442-1460 → :1514-1516.
[x] The expectation continues to be derived through the documented consumer
    (StatusEffectLifecycle.EffectiveBossAttack), not replaced by a hardcoded
    literal.
[x] The now-false comment stating that step 18a is unimplemented and that no
    MVP path applies a Boss ATK modifier is removed or corrected.
[x] The whole-item member-for-member wire comparison at the assertion site is
    unchanged in mechanism, and the ordering assertions that follow it are
    unchanged.
[x] `Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly` PASSES.
[x] GameServer.Api.Tests passes with 0 failures.
[x] GameServer.Domain.Tests, GameServer.Application.Tests, and
    GameServer.Infrastructure.Tests each pass with 0 failures.
[x] No file under src/ is modified.
[x] No file under docs/ is modified.
[x] No task file other than this one is created or modified.
[x] No gameplay value, formula, magnitude, duration, or contract is changed.
[x] No production behavior is altered to make the test pass
    (development/bug-fix.md §4).
[x] Relevant tests pass at the required validation depth (core/validation.md §2)
[x] Quality review checklist passes (quality/review.md §1)
[x] No authoritative rules or contracts violated (AGENTS.md §10 / ADR-001)
```

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — N/A. This task adds no production behavior and no new
                         unit. The corrected method IS a test.
[x] Integration tests  — REQUIRED. The affected test is an API-level SignalR
                         integration test for the full GAME_RULES.md §17
                         pipeline event ordering. It must pass.
                         Command:
                           dotnet test tests/backend/GameServer.Api.Tests/
                             GameServer.Api.Tests.csproj
                         then the full suite:
                           dotnet test src/backend/GameServer.sln
[x] Gameplay scenarios — The scenario is already authored inside the test
                         (Given a created battle with a match-producing Swap /
                         When the player swaps / Then the emitted batch equals
                         the documented §3.2 wire representation in §17 order).
                         This task restores it to correctness rather than
                         authoring a new one.
```

### Required end state

```text
GameServer.Domain.Tests          1469 passed, 0 failed
GameServer.Application.Tests      508 passed, 0 failed
GameServer.Infrastructure.Tests   364 passed, 0 failed
GameServer.Api.Tests              297 passed, 0 failed
                                  ────────────────────
Total                            2638 passed, 0 failed
```

### Key Edge Cases

- **The rig may or may not reach the Rage threshold in the asserted Swap.**
  Determine which, from the rig's Boss definition and Swap, and make the
  corrected derivation correct for the case that actually occurs — not for the
  case that would be convenient. Record the determination in the Completion
  Evidence.
- **Rage is applied by step 18a and decremented by step 19a in the same
  resolution.** The expectation models the batch, so the instance's presence at
  the step-18b read is what matters; its end-of-resolution `RemainingTurns`
  (3 → 2) is a state assertion, not an event in this batch
  (`BossPassiveEffectsTests.HoaLongRage_ShouldApplyRageStatusEffectAtStep18a`
  already covers the decrement).
- **The expectation must keep continuing the same RNG stream**
  (`playerDamage.UpdatedRngState`, ADR-009). Do not reorder or re-seed the
  derivation; only the modifier source is at issue.
- **`EffectiveBossATK` must never be written back to `BossState.ATK`.** The
  corrected derivation must not mutate the state it reads
  (`COMBAT_RULES.md` §5.5.4, cited at `BattleStateService.cs:1505-1508`).
- **A passing suite is not sufficient on its own.** Confirm the corrected
  expectation differs from the old one by exactly the Rage modifier
  (`250 → 270`, `200 → 216`). A fix that makes the test pass by coincidence —
  e.g. by loosening the comparison — is not acceptable.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **If the corrected expectation cannot be derived from the authoritative
  documents** (`GAME_RULES.md` §17; `COMBAT_RULES.md` §3.4/§5.5.1;
  `BOSS_RULES.md` §6.2.1/§6.3.1): STOP per `AGENTS.md` §7. Do not invent a
  value or re-baseline to whatever the server happens to emit.
- **If investigation shows the implementation — not the test — is wrong**
  (i.e. step 18a genuinely must not precede the step-18b read): STOP and report
  per `AGENTS.md` §4. This task is a Test bug fix; an implementation bug is a
  different task, and `TASK-153` is immutable and may not be reopened.
- **If making this test pass requires changing any file under `src/`:** STOP.
  That would mean the defect is not a test bug, and the classification must be
  revisited before proceeding.
- **If the corrected derivation requires a documentation change** to
  `GAME_RULES.md`, `COMBAT_RULES.md`, `BOSS_RULES.md`, or any §3.2 wire
  contract: STOP and report — the edit belongs to a documentation task under
  `AGENTS.md` §17.
- **If other tests in the suite are found to fail** beyond the one documented
  here: STOP and report them as separate findings. Do not expand this task to
  cover them (`AGENTS.md` §16 — report, do not fix inline).
- **If the fix would require weakening, skipping, or splitting the assertion**
  (type-only comparison, subset comparison, or a `Skip` attribute): STOP. That
  defeats the contract the test proves and is prohibited by
  `development/bug-fix.md` §4.
- **If this task appears to require a new Boss Passive, a new event, a new
  wire member, or a new state member:** STOP per `AGENTS.md` §7 — none is
  needed, and any such need signals a misdiagnosis.
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` (+27 / −5, one localized
  region at lines ~2552–2583) — the ONE file this task changed:
  - added `bossEffectsAfterStep18a`, which models the step-18a Boss Passive application
    (the `boss-hoa-long-rage` instance per BOSS_RULES.md §6.2.1, applied when the §6.2
    threshold is met for this Swap) and otherwise carries the pre-existing collection;
  - changed `StatusEffectLifecycle.EffectiveBossAttack`'s modifier input from
    `before.BossState.ActiveStatusEffects` (pre-resolution) to `bossEffectsAfterStep18a`
    (the step-18b read point);
  - removed the now-false comment stating that step 18a is unimplemented and that no MVP
    path applies a Boss ATK modifier, replacing it with an accurate statement of the
    `GAME_RULES.md` §17 step-18a → step-18b ordering.
- No other file was changed by this task. The uncommitted `src/`, `docs/`, and `tasks/`
  edits visible in `git status` are pre-existing TASK-153/TASK-155/TASK-157 work that was
  already present before this task began; `git diff --stat` for those paths is identical
  to the session-start state.

### Validation Results
- Affected test, smallest relevant scope first:
  `dotnet test tests/backend/GameServer.Api.Tests/GameServer.Api.Tests.csproj --filter "FullyQualifiedName~Swap_ReceiveEvents_ShouldPreserveTheExecutorEventOrderExactly"`
  → Passed, 1/1 (run 3× consecutively, green each time).
- Full required suite: `dotnet test src/backend/GameServer.sln` →
  ```text
  GameServer.Application.Tests      508 passed, 0 failed
  GameServer.Domain.Tests          1469 passed, 0 failed
  GameServer.Infrastructure.Tests   364 passed, 0 failed
  GameServer.Api.Tests              297 passed, 0 failed
  ─────────────────────────────────────────────────────
  Total                            2638 passed, 0 failed
  ```
  This matches the task's Required end state exactly.
- Pre-fix reproduction of the defect: the full suite was run 4× before the correction and
  failed once with the documented diff —
  `Event 90 differs … Expected {"type":"DamageCalculated","base":250,…,"defense":200,"finalDamage":200},
  got {"type":"DamageCalculated","base":270,…,"defense":216,"finalDamage":216}.`
  After the correction, 8 consecutive full-solution runs were all green.
- Test intent preserved: the whole-item `JsonElement.DeepEquals` member-for-member wire
  comparison and the `CascadeCreated`/`ComboChanged` ordering assertions are unchanged. No
  `Skip`, no subset comparison, no type-only comparison, and no hard-coded `270` (the
  expectation remains derived through the documented consumer).

### Root Cause Determination
- Confirmed **Test bug** per `development/bug-fix.md` §1: the test derived
  `EffectiveBossATK` from the PRE-resolution snapshot (`before.BossState.ActiveStatusEffects`)
  under a comment asserting that step 18a was unimplemented. TASK-153 implemented that half:
  the resolution applies Rage at step 18a (`BattleStateService.cs:1441-1460`, mutating
  `bossState`) and reads `EffectiveBossATK` at step 18b from the POST-18a state
  (`:1514-1516`). When the Rage threshold is met the two disagree by exactly the Rage
  modifier: test `100 → 100 + 150 = 250`, server `120 → 120 + 150 = 270` (`100 × 120/100`).
  The implementation is correct and is independently asserted by passing tests in other
  assemblies (`BossPassiveEffectsTests`, `BossResponseTests`, `BossSkillStep1CompositionTests`,
  `EffectiveBossAttackTests`). No production behavior was changed to make the test pass.
- **Rage-threshold determination for the asserted Swap** (the Key Edge Case; measured
  directly over 12 created battles): the rig's Swap produces **1–3 Matches** (observed
  3, 2, 2, 1, 1, 1, 2, 1, 1, 1, 1, 2) against Hỏa Long's threshold of **5**
  (`BossDefinitions.HoaLong`, BOSS_RULES.md §6.2 "every 5 Player Matches"). Therefore, in
  the common case, `rageTriggers = 0` and `skillFires = false`, and the modified board does
  not reach the Rage threshold.
- **Correction to the task's "Current State"**: the defect is real but **intermittent**, not
  deterministically red. The battle seed is drawn from fresh server entropy per battle
  (`SystemEntropyRngSeedSource`), so the generated board — and thus the Match count — varies
  per run. Most runs (1–3 Matches) coincidentally satisfy the stale derivation and the test
  passes; a run that reaches 5+ Matches fails. This is why isolated `--filter` runs pass
  while the full suite fails roughly one run in four. The corrected derivation was verified
  to be right in **both** cases: `matches = 5` yields `EffectiveBossATK = 120` and Step 1
  `= 270`, while `matches = 1–3` yields `100` and `100` — i.e. it now tracks the post-18a
  state rather than the pre-resolution snapshot.
- The diagnostic probe/proof test files used for this measurement were temporary and have
  been deleted; they are not part of the deliverable.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero files under `src/` modified
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed no completed task modified (TASK-153 / TASK-154 / TASK-155 / TASK-156 /
      TASK-157 are untouched, and no task file other than this one was edited)

### Known Limitation (outside this task's scope)
- The test's coverage of the Rage-ACTIVE path remains probabilistic: it is exercised only
  when the randomly seeded board happens to produce 5+ Matches. The corrected derivation is
  now correct for both cases, so this no longer causes failures — but deterministic coverage
  would require a fixed `BattleSeed` (`BattleStateService.CreateBattleAsync` accepts an
  optional seed) or a crafted board. That is new test coverage and is explicitly out of this
  task's scope; it was reported rather than implemented.
