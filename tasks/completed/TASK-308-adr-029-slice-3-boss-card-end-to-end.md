# TASK-308 — ADR-029 Slice 3: Boss Card End-to-End Implementation

<!--
  GEN-TASK EXECUTION MANIFEST — STATUS: IN PROGRESS

  One consolidated GAMEPLAY-CHANGE task per the operator's explicit
  authorization. The definition/content provisioning prerequisite is folded
  into this task (OD-1d); no separate provisioning task exists. All work items
  W1-W7 share one acceptance boundary, one declared file set, and one
  two-stage commit sequence.
-->

---

## Metadata

```text
Task ID:           TASK-308
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, persistence, realtime, client, testing, review
Workflow:          development/gameplay-change.md
                   (+ architecture/adr-change.md — ADR-024/ADR-029 in-place
                   amendments authorized by OD-1d/OD-3a)
Skills:            gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   discovery/impact-analysis,
                   backend/api-contract-validation,
                   realtime/realtime-protocol-validation,
                   client/client-event-projection,
                   testing/test-scenario-generation
Dependencies:      TASK-303 (DONE), TASK-304 (DONE), TASK-305 (DONE),
                   TASK-306 (DONE), TASK-307 (DONE — readiness contract),
                   ADR-024 (Accepted), ADR-029 (Accepted),
                   operator approval of OD-1c/OD-1d/OD-2a/OD-2b/OD-3a/OD-3b
                   recorded in §1 below
Declared Files:    see §5 (canonical list; migration file names are recorded
                   verbatim before Phase A staging)
```

**Lifecycle path:**

```text
BACKLOG -> READY -> IN PROGRESS   (operator authorization recorded in §1;
                                   readiness basis below)
IN PROGRESS -> IN REVIEW          (implementation + tests complete)
IN REVIEW -> DONE                 (two-stage commit protocol, TASK_LIFECYCLE.md §6)
```

---

## 1. Operator-Approved Contract (recorded verbatim before implementation)

The operator explicitly approved the following decisions and values. They are
the only values this task may implement; no approved value may be replaced,
retuned, or generalized during implementation.

### OD-1c — Active-form resolution

- Resolve Boss active form from the presence of its provisioned Boss Card.
- Resolve the form exactly once when the battle is created.
- Fail closed when required definition or form data is missing or invalid.
- Do not silently fall back to the legacy Skill form.
- Initially enable Boss Card form only for Sơn Thạch Vệ.

### OD-1d — Slice boundary

- Fold Boss Card definition/content provisioning into Slice 3.
- Reconcile ADR-029 and its Slice 3 exit criteria.
- Do not create a separate provisioning task.

### OD-3a — Cast-priority score carrier

- Approve a nullable `CastPriorityScore` overlay column.
- Amend ADR-024's documented overlay-value count from four to five.
- Preserve the approved score contract and validation rules in TASK-307.

### OD-2a — Initial Power cost

`earthquake.PowerCost = 30`

### OD-2b — Initial Shield effect

`earthquake` grants `150 Flat` Shield.

### OD-3b — Initial cast-priority score

`earthquake.CastPriorityScore = 16`

These values are approved for initial implementation and testing. They are not
evidence that final game balance has been optimized.

### Previously approved clauses confirmed by the operator

- Reuse `CardDefinition` for Boss Cards.
- Use the approved Boss Card identity exception based on the ability `SkillId`.
- Fold definition/content provisioning into Slice 3.
- Use the approved nullable `CastPriorityScore` contract.
- Preserve the documented one-action-per-response-phase policy and atomic
  round-resolution semantics.

---

## 2. Readiness Basis

```text
[ x ] Task type confirmed: GAMEPLAY-CHANGE (TASK_TYPES.md §2; the operator
        explicitly authorized one consolidated GAMEPLAY-CHANGE task and
        TASK-307 §7.1 records why the doc/schema/runtime/test work shares one
        acceptance boundary)
[ x ] Relevant documentation exists in docs/ (all TASK-307 §5 owners verified)
[ x ] MVP scope confirmed: Boss Power / minimum Boss Card capability are IN
        (MVP_SCOPE.md §1); the fifth Gem stays FUTURE (§3)
[ x ] Not blocked by an unresolved dependency: TASK-303/304/305/306/307 DONE;
        OD-1c/1d/2a/2b/3a/3b recorded in §1
[ x ] Primary agent assigned (gameplay) and workflow assigned
[ x ] Acceptance criteria are testable (this record §7 + TASK-307 §7.3)
[ x ] Preflight: branch master, HEAD f6ddbd90c042e57c50288ed732a7deb63a419089,
        working tree clean, upstream origin/master (ahead 4, not pushed),
        completed/ = 309 records, backlog/active/blocked empty,
        highest existing task ID = 307 -> TASK-308 allocated (README.md §3)
[ x ] Duplicate search: no TASK-308 record, artifact, or partial work exists
[ x ] .ai/PROJECT_STATE.md recorded HEAD 94d40d0 / STALE -> INCREMENTAL mode;
        refreshed by this task
```

---

## 3. Objective

Implement ADR-029 Slice 3 end to end: record the approved contract, reconcile
the authoritative documentation and the ADR-024/ADR-029 amendments, provision
the `earthquake` Boss Card and the nullable `CastPriorityScore` overlay, add a
deterministic one-action `BossActionPolicy`, integrate the selected action into
the atomic round (cost, Shield, cooldown, suppression of board action/attack),
emit and project `BossCardCast`, present it on the client, and prove the real
provisioned card casts in a battle execution path.

---

## 4. Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1, §3
- `docs/01-game-design/BOSS_RULES.md` §4.1–§4.4, §6.3, §6.3.1, §6.4, §7
- `docs/01-game-design/CARD_RULES.md` §1, §1.1, §3 item 9, §6
- `docs/01-game-design/GAME_RULES.md` §12, §16, §17 step 18
- `docs/01-game-design/COMBAT_RULES.md` §4 (Shield), §5 (status effects)
- `docs/02-technical/GAME_STATE.md` §2.3.1, §2.4, §2.4.3, §5.1
- `docs/02-technical/GAME_EVENTS.md` §1, §2 (`BossCardCast`), §3 item 8
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.1, §3.2.2, §3.2.30, §4.4, §7
- `docs/02-technical/DATABASE.md` §1 (`CardDefinition`, `BossDefinition`), §3, §5 item 4
- `docs/02-technical/ARCHITECTURE.md` §2.1, §3, §5
- `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md` §2.2, §2.4, §2.5
- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` §2.5, §2.7, §2.8, §2.9, §3, §4
- `tasks/artifacts/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md` §3–§7, §10
- `tasks/completed/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md`

---

## 5. Declared File Set (P-2 / P-5)

The Phase A commit must contain exactly this set (the task record itself is
excluded, per `TASK_LIFECYCLE.md` §6.1). This is the final, revalidated set
against the actual implementation; the two migration file names are the
verbatim generated ids.

### Authoritative documentation (W1)

```text
docs/02-technical/DATABASE.md
docs/02-technical/ARCHITECTURE.md
docs/02-technical/GAME_STATE.md
docs/01-game-design/BOSS_RULES.md
docs/01-game-design/CARD_RULES.md
docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md
docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md
docs/03-decisions/README.md
.ai/PROJECT_STATE.md
```

### Domain (W3, W5)

```text
src/backend/GameServer.Domain/Bosses/BossActionPolicy.cs        (new)
src/backend/GameServer.Domain/Bosses/BossDefinition.cs
src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
src/backend/GameServer.Domain/Match3/BattleEvent.cs
src/backend/GameServer.Domain/Cards/CardDefinition.cs           (comment-only)
```

### Persistence / provisioning (W2)

```text
src/backend/GameServer.Infrastructure/Postgres/Configurations/BossDefinitionConfiguration.cs
src/backend/GameServer.Infrastructure/Postgres/Configurations/CardDefinitionConfiguration.cs  (comment-only)
src/backend/GameServer.Infrastructure/Postgres/Repositories/BossDefinitionLookup.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261019125417_AddBossCastPriorityScore.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261019125417_AddBossCastPriorityScore.Designer.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261019125446_ProvisionBossCardContent.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261019125446_ProvisionBossCardContent.Designer.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/GameDbContextModelSnapshot.cs
```

### Application + Api (W4, W5)

```text
src/backend/GameServer.Application/Battle/BattleStateService.cs
src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs
```

### Client (W6)

```text
src/frontend/client/src/game/scenes/BattleEventPresenter.ts
src/frontend/client/src/game/scenes/BattleScene.ts
```

### Tests (W7)

```text
tests/backend/GameServer.Domain.Tests/BossActionPolicyTests.cs                          (new)
tests/backend/GameServer.Domain.Tests/BattleEventEmissionTests.cs
tests/backend/GameServer.Domain.Tests/Task172BossDefinitionsContentTests.cs
tests/backend/GameServer.Application.Tests/BossCardCastRuntimeTests.cs                  (new)
tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs
tests/backend/GameServer.Infrastructure.Tests/BossDefinitionBalanceLookupTests.cs
tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs
tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs
tests/backend/GameServer.Infrastructure.Tests/PetCardRelicDefinitionProvisioningTests.cs
tests/backend/GameServer.Infrastructure.Tests/PetCardRelicDefinitionPostgresProvisioningTests.cs
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
tests/backend/GameServer.Api.Tests/BossResponseWireTests.cs
tests/backend/GameServer.Api.Tests/BossWireProjectionTests.cs
tests/backend/GameServer.Api.Tests/TestProvisionedContent.cs
src/frontend/client/tests/BattleEventPresentation.test.ts
```

*(Set size: 8 docs + 1 cache + 5 Domain + 8 persistence + 2 Application/Api +
3 client + 15 test files = 41 implementation files.)*

---

## 6. Scope

### In Scope

- W1: the approved documentation/ADR amendments (§1 values).
- W2: one provisioned `earthquake` `CardDefinition` row (`BossCard`,
  `PowerCost = 30`, `LoadoutCopyLimit = 1`, `Shield`/`Flat`/`150`), the
  nullable `BossDefinition.CastPriorityScore` column (`earthquake = 16`, others
  `NULL`), overlay lookup + validation, battle-start resolution of the Boss
  Card/active form/score cached with the Boss content input.
- W3: deterministic `BossActionPolicy` (eligibility, comparison, board-wins-tie,
  neither-action branch, fail-closed invalid content).
- W4: atomic round integration — selection at 18a, board or cast branch,
  suppressions, Passive 18b, cast at 18c, cooldown set/decrement, single
  write-back, CAS retry determinism, stale cooldown comment correction.
- W5: `BattleEventType.BossCardCast` (ordinal 20), payload/factory,
  wire projection (`cardId`, `sourceId`), Application/Api tests.
- W6: client parsing/presentation/callout/hold, reusing the Boss Skill display
  name resolver (identity is the `SkillId`), with vitest coverage.
- W7: regression/integration/acceptance verification, including the real
  provisioned card in a battle execution path.

### Out of Scope

- Migrating the other four Boss abilities; any effect-vocabulary extension.
- Retiring `SkillCharge`, `BossSkillCast`, or the legacy handler.
- Adding the fifth Gem or a Power-drain Gem.
- Rebalancing unrelated Bosses or Player Cards.
- API/collection/loadout surface changes (the Boss Card stays unowned/invisible).
- Fixing the pre-existing `BaseDamage` provisioning drift, the stale
  `BattleHub` projection comment, or the Signature-Skill category guard (P-8).
- Client-authoritative logic; a second state write; a separate cast endpoint.
- Pushing.

---

## 7. Acceptance Criteria

Derived from TASK-307 §7.3 and the operator's consolidated plan:

- [ ] Approved decisions/values recorded in §1 before any implementation edit.
- [ ] W1 docs/ADR amendments applied; no unresolved documentation conflict;
        ADR-024 overlay count four -> five; ADR-029 §2.5 item 15 superseded and
        Slice 3 exit criteria reconciled.
- [ ] One `CardDefinition` row `earthquake` (`BossCard`, `Earthquake`,
        `PowerCost = 30`, `LoadoutCopyLimit = 1`, `Shield` `Flat` `150`) is
        provisioned by an insert-only migration with mirrored `Down`; the
        provisioned CardDefinition set is 9; no `PlayerUnlockedCard`, loadout or
        API surface changes.
- [ ] `BossDefinition.CastPriorityScore` is nullable with
        `CHECK (CastPriorityScore IS NULL OR CastPriorityScore >= 0)`;
        `earthquake = 16`; the overlay loads/validates it and fails closed for a
        migrated Boss with `NULL`/negative.
- [ ] At battle creation the Boss Card, active form and score resolve exactly
        once and are cached; invalid present content fails creation (no legacy
        fallback).
- [ ] Selection is deterministic and exhaustive over the five cases; board
        wins ties; scores `0..15` lose to a board action; `16` can win.
- [ ] A cast-selected round: no Boss swap/cascade/generation/board event, no
        Boss attack/damage event, Passive 18b still runs, exactly one
        `BossCardCast`, Power deducted exactly once, Shield applied under
        `COMBAT_RULES.md` §4, cooldown set to the authored value.
- [ ] A board-selected round for the migrated Boss: board action as today; no
        `BossSkillCast`; cooldown ticks.
- [ ] Unmigrated Bosses: legacy board-then-Skill order unchanged; their suites
        pass unmodified.
- [ ] `BossCardCast` wire item has `type`/`cardId`/`sourceId` only, inside the
        round's single ordered batch, after 18b events; never for a board phase;
        never a `CardCast`; never with `BossSkillCast` for the same ability; the
        client parses and presents it with the Boss Skill display-name resolver.
- [ ] CAS retry: one state write, one ordered batch, one deduction, one
        cooldown write, one event; the same committed state selects the same
        action.
- [ ] Boss Power spend emits no `PowerChanged`; Power/cooldown reach the client
        only through `bossState`.
- [ ] Player `CardCast`/`PetSkillCast` contracts byte-identical; the
        one-cast-per-Turn allowance is neither read nor spent by a Boss cast.
- [ ] Balance cadence metrics re-derived with a new metric (not hand-edited);
        protected historical artifacts untouched.
- [ ] Full gates: `dotnet build`, backend tests, frontend tests/build,
        `git diff --check`; pre-existing failures documented as unchanged;
        quality review; two-stage commit with the exact declared set.

---

## 8. Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure / Api)
[x] src/frontend/client/ (BattleEventPresenter, BattleScene)
[x] tests/ (Domain / Application / Infrastructure / Api, frontend vitest)
[x] docs/ (DATABASE, GAME_STATE, ARCHITECTURE, BOSS_RULES, CARD_RULES,
            ADR-024, ADR-029, ADR index)
[x] .ai/PROJECT_STATE.md (navigation cache refresh)
```

---

## 9. Implementation Notes

- **Content resolution**: `BattleStateService.CreateBattleAsync` resolves
  `CardDefinition` by `bossDefinition.SkillId` through the existing
  `ICardDefinitionLookup`, once, and caches `CardDefinition?` beside the Boss
  definition. Presence is the form signal (OD-1c). Present-but-invalid content
  (wrong category, missing/negative score) throws (fail closed).
- **Policy**: Domain `BossActionPolicy` is pure/deterministic; the board score
  is the existing `BossMoveSelectionResult.Score` (weights 10/5/20/10, no tier
  multiplier); cast score is the authored `CastPriorityScore`; a cast must
  strictly out-score the board; board ties keep the existing lowest-`From`/
  `To` rule.
- **Recovery**: `BossAiEngine.SelectMove` remains the only deadlock-recovery
  path. A cast-selected phase may still carry the pre-selection reshuffle and
  its seeded RNG consumption; the reshuffle emits no event.
- **Cast execution**: reuse `CardCastExecutor` (Boss branch); the executor stays
  policy-free. The Application phase owner applies the cooldown set and emits
  `BossCardCast` after the executor commits, inside the round's single write.
- **Cooldown**: existing step-3 decrement is untouched; the Card form sets the
  authored value in the same write-back; `N` blocks `N−1` subsequent rounds.
  Correct the stale inline comment in the touched file (TASK-307 C-3).
- **Events**: domain event member 20; payload `cardId` = Boss Card definition id
  (= ability `SkillId`), `sourceId` = `BossState.BossId`; projection arm; client
  parser/presenter. Unknown-event tolerance preserved.

---

## 10. Testing Requirements

### Required Verification

```text
[x] Unit tests — BossActionPolicy matrix; BossDefinition score overlay;
                 effect/cost/Shield validation; event factory/payload.
[x] Integration tests — Application round with real provisioned content:
                 selection, deduction, Shield, cooldown, event, suppressions,
                 CAS retry; wire projection; client presentation.
[x] Gameplay scenarios — given Sơn Thạch Vệ with the provisioned card and
                 enough Power, when a response phase occurs, then exactly one
                 action occurs (cast or board), with the documented state,
                 event and presentation.
[x] Production integration — real PostgreSQL row + real lookup + battle
                 execution path (not Domain fixtures alone).
```

### Key Edge Cases

- Board-only, cast-only, both (board higher / cast higher), tie -> board,
  neither action, missing/invalid card/score.
- Power exactly `30` / `29`; Shield refresh/replace; no double deduction.
- CAS retry after a refused write: one commit, one event, one batch.
- Migrated Boss board-selected phase: no `BossSkillCast`, cooldown ticks.
- Unmigrated Boss: legacy order unchanged.
- Player CardCast/PetSkillCast byte-identical.
- Reconnect state projection includes the Boss Power/cooldown consequences;
  the cast is not re-delivered.

---

## 11. Stop Conditions

- If an authoritative document contradicts the approved contract in a way that
  cannot be reconciled without changing an approved value: STOP and report the
  exact conflict (`AGENTS.md` §4/§20).
- If the approved `Shield 150 Flat` cannot be represented by the closed effect
  vocabulary: STOP (it can — `Shield`/`Flat` are defined).
- If a required `CastPriorityScore` cannot be represented nullable: STOP.
- If implementation would require client-authoritative logic: STOP.
- If a test can only pass by weakening an authoritative contract: STOP/report.

---

## 12. Completion Evidence

### Commit

- Implementation slice (Phase A): `242c0d7c0ac9a527da0c9bd991c071fc8fd5f773`
- Subject: `feat: implement adr-029 slice 3 boss card end to end`
- Owns: `TASK-308`
- Files: the exact 41-file set in §5 (staged-set equality verified:
  `staged=41 expected=41`, zero missing/extra, task record excluded)
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files

Authoritative documentation (8 + cache)
- `docs/02-technical/DATABASE.md` — CardDefinition category/identity/provisioning
  exception, 9-row set, nullable `CastPriorityScore` + CHECK, §5 item 4.
- `docs/02-technical/ARCHITECTURE.md` — persistence boundary names the Card
  definition read and the once-at-creation Boss Card resolution.
- `docs/02-technical/GAME_STATE.md` — Boss Card/form/score are content inputs;
  cooldown `N` blocks `N−1` reading stated.
- `docs/01-game-design/BOSS_RULES.md` — §4.3 item 5 score carrier/value,
  §4.4 item 6 active form, §6.3/§6.3.1 authored `earthquake` values.
- `docs/01-game-design/CARD_RULES.md` — §1.1 items 3/5 identity exception and
  provisioning contract.
- `docs/03-decisions/ADR/ADR-024-…md` — in-place five-value overlay amendment.
- `docs/03-decisions/ADR/ADR-029-…md` — in-place Slice 3 amendment
  (§2.5 item 15 superseded, §2.7 item 20, U-2/U-3/U-4, §4 exit criteria).
- `docs/03-decisions/README.md` — ADR index/amendment record.
- `.ai/PROJECT_STATE.md` — navigation-cache refresh.

Domain/Application/Api/client
- `BossActionPolicy.cs` (new) — pure deterministic one-action selection.
- `BossDefinition.cs` / `BossDefinitions.cs` — nullable score carrier +
  `SonThachVe.CastPriorityScore = 16`.
- `BattleEvent.cs` — `BossCardCast = 20`, payload, factory, accessor.
- `CardDefinition.cs` — comment-only category correction.
- `BossDefinitionConfiguration.cs` — nullable column + CHECK.
- `CardDefinitionConfiguration.cs` — comment-only category-set correction.
- `BossDefinitionLookup.cs` — fifth overlay value + fail-closed validation.
- Two migrations (`AddBossCastPriorityScore`, `ProvisionBossCardContent`) +
  `GameDbContextModelSnapshot.cs`.
- `BattleStateService.cs` — creation-time content resolution; policy at 18a;
  cast at 18c; board/attack suppression; cooldown set; stale comment corrected.
- `BattleEventWireProjection.cs` — `BossCardCast` projection arm + factory.
- `BattleEventPresenter.ts` / `BattleScene.ts` — parse, adapt, callout
  (`BOSS CARD: <name>`), hold.

Tests — 15 files: Domain policy/event/content (3), Application round/creation
(2), Infrastructure lookup/provisioning/persistence (5), Api wire/integration
(4), frontend presentation (1).

### Validation Results

```text
[x] dotnet build src/backend/GameServer.sln --nologo -v q
      0 Error(s); only pre-existing warnings
[x] Backend full suite (dotnet test src/backend/GameServer.sln --no-build)
      Domain           1639 / 1639  PASS
      Application       808 / 810   (2 pre-existing failures, unchanged)
      Infrastructure    491 / 495   (4 pre-existing failures, unchanged)
      Api               390 / 390   PASS
      TOTAL            3328 passed, 6 failed — all 6 reproduced at HEAD f6ddbd9
[x] Frontend (cwd src/frontend/client)
      npm run test:run   24 files, 1271 / 1271 PASS (baseline 1265 + 6 new)
      npm run build      tsc + vite success
[x] git diff --check   exit 0
[x] EF model          "No changes have been made to the model since the last migration."
[x] Local PostgreSQL  migrations applied; earthquake row present
      earthquake | Earthquake | Category 2 (BossCard) | PowerCost 30 | limit 1
      EffectDefinition round-trips to Shield/Flat/150
      boss-def-son-thach-ve.CastPriorityScore = 16; other four NULL
      CK_BossDefinition_CastPriorityScore_NonNegative present
      CardDefinition provisioned set = 9
```

### Integration Evidence — the real provisioned Boss Card

```text
[1] Production provisioning
    The insert-only migration wrote exactly one `earthquake` CardDefinition row
    with the approved identity/category/cost/effect; the Postgres provisioning
    suite asserts the applied row and the 9-row set, and the score overlay suite
    asserts DB → Domain (`16` for Sơn Thạch Vệ, NULL for the other four).

[2] Creation-time active form
    BossCardCastRuntimeTests.CreateBattle_WithProvisionedBossCard_ShouldResolveTheFormAndScoreExactlyOnce
    — the lookup is read exactly once by the ability SkillId; a committed round
    performs no second read. Wrong category / NULL-or-negative score / negative
    cost each fail creation closed and write no battle (3 tests).

[3] Selection and cast in the real round pipeline
    Round_WhenCastSelected_... — 50 Power − 30 = 20 exactly once; one Boss
    Shield instance (`Shield`, Boss source, 150); cooldown set in the same
    write-back; exactly one `BossCardCast` after the 18b Passive events; no Boss
    SwapStarted/SwapResolved/Match/MatchedGemSummary; no Boss damage; no
    `PowerChanged`; no `BossSkillCast`.
    Round_WithTheRealAuthoredScore_ShouldSelectTheCastOnACrafted15PointBoard —
    uses the real `SonThachVe` definition (score 16) and the approved card
    definition; on a crafted board whose only Boss move scores exactly 15 the
    integrated round selects the cast and deducts 30.
    Round_WhenCastSelected_ShouldSurviveARejectedWrite... — one commit, one
    deduction, one event after a CAS-retried write.
    Round_WhenBoardActionSelectedForTheMigratedBoss... — no BossSkillCast;
    cooldown ticks; legacy form unchanged for unmigrated Bosses.
    PlayerCardCast_ShouldStayUnchangedOnAMigratedBattle — Player path untouched.

[4] Wire
    BossResponseWireTests.ReceiveEvents_BossCardCast_... drives a committed round
    through the hub batch and asserts the exact item:
      { "type":"BossCardCast", "cardId":"earthquake", "sourceId":"boss-son-thach-ve" }
    with no other member, no Boss board/attack event, no Boss `PowerChanged`, and
    no `BossSkillCast` — inside the round's single ordered batch.
    BossWireProjectionTests adds exact-JSON, no-extra-member and one-to-one/
    order-preserving assertions.

[5] Client presentation
    BattleEventPresentation.test.ts parses/adapts/formats the event, resolves
    `BOSS CARD: Earthquake` through the existing Boss Skill display-name table
    (no second table), falls back to `BOSS CARD`, and a scene-level test prints
    it on the Boss callout row while the Move Log records the event verbatim.

[6] Compatibility
    API integration tests start a battle against the migrated boss on the real
    endpoint (seeded provisioned content) and prove the form resolves; wrong
    category and NULL score fail as server-side errors. All existing Player
    CardCast/PetSkillCast, legacy Boss, reconnect, wire closed-set and client
    suites pass unchanged except the documented pre-existing failures.
```

### Known Failures (pre-existing; unchanged)

Verified by re-running the same tests in a detached worktree at HEAD `f6ddbd9`:

```text
Infrastructure (4):
  BossPersistenceTests.ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments
  BossPersistenceTests.Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments
  BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions
  BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects
  → historical migration JSON carries BaseDamage 150/180 while ADR-026 D3 set 0; the
    flame-burst expectation still says Burn although D5 made it AtkBuff.
Application (2):
  BalanceEvidenceMeasurementCorrectionTests.TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn
  BalanceEvidenceMeasurementCorrectionTests.TASK296_ReviewD_Canonical75RunCandidate_Verification
  → pre-existing historical-evidence expectations (8/41/26) that already fail at HEAD.
```

The one extra HEAD-worktree failure (`Postgres_ShouldCarryTheFourBalanceRangeConstraints`)
was a shared-database artifact: the TASK-308 fifth constraint was already applied
while the HEAD test expected four. The in-slice test now expects the five
documented constraints and passes.

### Server Authority & Scope Verification

- [x] No client-authoritative gameplay logic: the client only parses and
      presents delivered members; Power/cooldown arrive through `bossState`.
- [x] MVP scope: the Boss Card capability is IN (`MVP_SCOPE.md` §1); no fifth
      Gem/Power-drain Gem; no new system.
- [x] `SkillCharge`, `BossSkillCast` and the legacy handler are retained; no
      new work builds on them.
- [x] Remaining four abilities not migrated; no effect-vocabulary extension.
- [x] Player `CardCast`/`PetSkillCast` contracts byte-identical; the
      one-cast-per-Turn allowance is neither read nor spent.
- [x] One committed Turn/Sequence, one atomic state write, one ordered event
      batch, CAS-retry determinism.
- [x] P-1…P-6 commit policy: single `master`, no branch/tag, Phase A one commit
      with the exact declared set, task record excluded, no amend/rebase/force
      push/`commit -a`, no push.

### Remaining Limitations

- The battle-scoped content cache remains in-process (pre-existing R-4): a
  Redis-recovered battle on another process faults as before; TASK-308 adds no
  second recovery assumption.
- With the approved score `16`, the cast is selectable only when the best Boss
  board move scores `15`; a deterministic sample of ordinary post-player boards
  had a minimum best score of `40`, so the cast is technically reachable but
  rare in normal play. The value is operator-approved and was not re-tuned;
  cadence tuning remains future balance work.
- No live browser/Playwright session was run for this slice; the client is
  covered by the presenter/scene unit suite, and the wire by the hub batch test.
- The pre-existing `BaseDamage` provisioning drift and the P-8 Signature-Skill
  category guard are reported, not fixed (out of scope).
