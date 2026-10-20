# TASK-309 — ADR-029 Slice 4: Remaining Boss Card Abilities

<!--
  GEN-TASK EXECUTION MANIFEST — STATUS: DONE

  One consolidated GAMEPLAY-CHANGE task per the operator's explicit
  authorization in the TASK-309 Operator Decision Record (B-1…B-4, D-1,
  Power Cost table). All four remaining Boss abilities share one acceptance
  boundary, one declared file set, and one two-stage commit sequence.
-->

---

## Metadata

```text
Task ID:           TASK-309
Type:              GAMEPLAY-CHANGE
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, persistence, realtime, client, testing, review
Workflow:          development/gameplay-change.md
                   (+ architecture/adr-change.md — ADR-029's Slice 4 record is
                   amended in place by the operator-approved values)
Skills:            gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   discovery/impact-analysis,
                   backend/persistence-analysis,
                   realtime/realtime-protocol-validation,
                   client/client-event-projection,
                   testing/test-scenario-generation
Dependencies:      TASK-303 (DONE), TASK-304 (DONE), TASK-305 (DONE),
                   TASK-306 (DONE), TASK-307 (DONE), TASK-308 (DONE),
                   ADR-024 (Accepted, amended), ADR-029 (Accepted, amended),
                   operator approval of B-1/B-2/B-3/B-4/D-1 and the four Power
                   costs/vALUES recorded in §1 below
Declared Files:    see §5 (canonical list; the four migration file names are
                   recorded verbatim before Phase A staging)
```

**Lifecycle path:**

```text
BACKLOG -> READY                  (operator authorization recorded in §1;
                                   readiness basis in §2)
READY -> IN PROGRESS              (agent picks up the task; active/)
IN PROGRESS -> IN REVIEW          (implementation + tests complete)
IN REVIEW -> DONE                 (two-stage commit protocol, TASK_LIFECYCLE.md §6)
```

---

## 1. Operator-Approved Contract (recorded before implementation)

The operator approved the following decisions and values in the TASK-309
Operator Decision Record. They are the only values this task may implement;
no approved value may be replaced, retuned, or generalized during
implementation.

### B-1 — Cast priority score

- `CastPriorityScore = 85` for the four remaining Bosses: Hỏa Long, Thủy Ma,
  Mộc Yêu, Kim Lôi Vương.
- The score is a starting value to be monitored and adjusted from real
  gameplay data. It is not globally optimal and authorizes no in-battle
  cast-rate guarantee.
- The existing rule is unchanged: a Boss Card is selected only when
  `CastPriorityScore > BoardScore`; a tie goes to the Board Action.

### B-2 — `flame-burst` duration

- Duration `= 3` Turns; Boss ATK buff `+50%`; Power Cost `40`; Cooldown `2`.
- Duration 3 yields at most two buffed attack opportunities under the
  existing lifecycle; no guarantee that both attacks occur.

### B-3 — `root` duration

- Duration `= 3` Turns; Player (active Pet) ATK debuff `-30%`; Power Cost
  `35`; Cooldown `2`.
- Duration 3 yields at most two affected Player attacks under the existing
  lifecycle; no guarantee that both attacks occur.

### B-4 — ATK modifier stacking

- Preserve the existing multiplicative stacking: `1.20 × 1.50 = 1.80`.
- Do not change to additive stacking in TASK-309; a future evaluation
  requires its own task, impact analysis and regression tests.

### D-1 — `CardEffectType` vocabulary extension

Add exactly four effect types:

```text
AtkBuff        Boss self ATK buff (authored magnitude and duration)
PowerDrain     active Pet Power reduction (flat, instant; no transfer to Boss)
AtkDebuff      active Pet ATK debuff (authored magnitude and duration)
NextAtkBuff    Boss next qualifying attack ATK buff (trigger-based)
```

Implementation requirements:

- Update the Domain enum, factory/validation and the shared execution path.
- Update `DATABASE.md` and the related contract documentation.
- Update persistence fixtures, serialization/deserialization coverage and
  regression tests.
- Unknown or unsupported effect values must continue to fail closed.
- Player Cards must not be allowed to use the Boss-only effects.
- `PowerDrain` subtracts only 20 Player Power; it transfers no Power to the
  Boss and grants the Boss none.
- `NextAtkBuff` lasts until the Boss's next qualifying attack and must be
  tested across the whole cast-to-consumption lifecycle.

### Power Cost

| Boss | Ability | Power Cost |
|---|---|---:|
| Hỏa Long | `flame-burst` | 40 |
| Thủy Ma | `drain-power` | 25 |
| Mộc Yêu | `root` | 35 |
| Kim Lôi Vương | `thunder-strike` | 40 |

Preserved existing cooldowns and approved legacy semantics:

- `flame-burst`: 2 Turns cooldown.
- `drain-power`: 3 Turns cooldown; drains 20 Player Power.
- `root`: 2 Turns cooldown; reduces Player ATK by 30%.
- `thunder-strike`: 0 Turns cooldown; +50% ATK on the next qualifying Boss
  attack.

All four effects retain `BaseDamage = 0`. Direct damage and Burn DoT remain
retired under the governing decisions.

### Required verification gates (operator-recorded)

- **V-1** — reproducible 10,000-board simulation baseline (methodology, seeds
  and score distribution preserved; board-only probability kept separate from
  actual in-battle cast frequency; Power and cooldown eligibility included in
  the post-implementation analysis).
- **V-2** — status-effect lifecycle tests: `flame-burst` duration 3 affects at
  most two qualifying Boss attacks and decrements at the documented round-end
  step; `root` duration 3 affects at most two Player attacks; expiry is
  correct including rounds with no qualifying attack; multiplicative ATK
  stacking is unchanged.
- **V-3** — the migration's `Up`/`Down` applied and reverted against a
  disposable test PostgreSQL database through the repository's supported
  migration workflow (`dotnet ef database update`), with source inspection
  alone insufficient.

---

## 2. Readiness Basis

```text
[ x ] Task type confirmed: GAMEPLAY-CHANGE (TASK_TYPES.md §2)
[ x ] Relevant documentation exists in docs/ (TASK-307 §5 owners verified;
        TASK-308 Slice 3 delivered)
[ x ] MVP scope confirmed: Boss Power / minimum Boss Card capability are IN
        (MVP_SCOPE.md §1); the fifth Gem stays FUTURE (§3)
[ x ] Not blocked by an unresolved dependency: TASK-303/304/305/306/307/308
        DONE; every value this task needs is approved in §1
[ x ] Primary agent assigned (gameplay) and workflow assigned
[ x ] Acceptance criteria are testable (§7 + the V-1/V-2/V-3 gates)
[ x ] Preflight: branch master, HEAD 081f9d76a97e4ad964fa69b5a844a23785ebdc97,
        working tree clean, completed/ = 310 records, backlog/active/blocked
        empty (only .gitkeep), highest existing task ID = 308 -> TASK-309
        allocated (tasks/README.md §3)
[ x ] Duplicate search: no TASK-309 record, artifact or partial work exists
[ x ] .ai/PROJECT_STATE.md recorded HEAD f6ddbd9 / STALE -> INCREMENTAL mode;
        refreshed by this task
```

---

## 3. Objective

Migrate the four remaining MVP Boss abilities — `flame-burst`, `root`,
`drain-power` and `thunder-strike` (last) — to the Boss Card form approved by
ADR-029 Slice 4: provision the four `BossCard` `CardDefinition` rows with the
approved costs and effects, set the approved cast-priority score `85` on the
four Boss definitions and rows, extend the `CardEffectType` vocabulary with the
four Boss-only effect identities, resolve them through the shared cast path,
and prove the approved durations, trigger consumption, stacking and migration
reversibility with executable gates.

---

## 4. Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1, §3
- `docs/01-game-design/BOSS_RULES.md` §4.1–§4.4, §6.3, §6.3.1, §6.4, §7, §8
- `docs/01-game-design/CARD_RULES.md` §1, §1.1, §3, §3.6
- `docs/01-game-design/GAME_RULES.md` §12, §16, §17 step 18
- `docs/01-game-design/COMBAT_RULES.md` §4, §5 (§5.4, §5.5, §5.6)
- `docs/02-technical/GAME_STATE.md` §2.3, §2.4, §5.1
- `docs/02-technical/GAME_EVENTS.md` §1, §2 (`BossCardCast`, `PowerChanged`)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2.2, §3.2.24, §3.2.30, §4.4, §7
- `docs/02-technical/DATABASE.md` §1 (Card `EffectDefinition` contract),
  §1 note item 5, §3, §5 item 4
- `docs/03-decisions/ADR/ADR-024-database-driven-boss-balance-configuration.md`
- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md`
  §2.5, §2.7, §2.9, §3, §4
- `tasks/completed/TASK-307-adr-029-reconciliation-and-slice-3-readiness.md`
- `tasks/completed/TASK-308-adr-029-slice-3-boss-card-end-to-end.md`
- The TASK-309 Operator Decision Record (this task's authorization; §1 above)

---

## 5. Declared File Set (P-2 / P-5)

The Phase A commit must contain exactly this set (the task record itself is
excluded, per `TASK_LIFECYCLE.md` §6.1). The four data-only migration file
pairs are recorded verbatim here before Phase A staging.

### Authoritative documentation

```text
docs/01-game-design/BOSS_RULES.md
docs/02-technical/DATABASE.md
docs/02-technical/GAME_EVENTS.md
docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md
docs/03-decisions/README.md
.ai/PROJECT_STATE.md
```

### Domain — effect vocabulary and shared execution

```text
src/backend/GameServer.Domain/Cards/CardEffectType.cs
src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs
src/backend/GameServer.Domain/Cards/CardEffectDefinitions.cs
src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
```

### Domain — Boss content and Application integration

```text
src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs
```

### Persistence — four data-only migrations (ordered; names recorded verbatim)

```text
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261020111406_ProvisionFlameBurstBossCard.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261020111406_ProvisionFlameBurstBossCard.Designer.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261020111411_ProvisionRootBossCard.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261020111411_ProvisionRootBossCard.Designer.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261020111415_ProvisionDrainPowerBossCard.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261020111415_ProvisionDrainPowerBossCard.Designer.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261020111419_ProvisionThunderStrikeBossCard.cs
src/backend/GameServer.Infrastructure/Postgres/Migrations/20261020111419_ProvisionThunderStrikeBossCard.Designer.cs
```

*(Applied in stamp order, which is the mandated ability order
`flame-burst` → `root` → `drain-power` → `thunder-strike`. No schema
operation and `GameDbContextModelSnapshot.cs` unchanged.)*

### Tests — Domain

```text
tests/backend/GameServer.Domain.Tests/CardEffectDefinitionTests.cs
tests/backend/GameServer.Domain.Tests/CardEffectDefinitionsTests.cs
tests/backend/GameServer.Domain.Tests/Cards/BossCardCastExecutorTests.cs
tests/backend/GameServer.Domain.Tests/Cards/CardCastPlayerCompatibilityTests.cs
tests/backend/GameServer.Domain.Tests/Task172BossDefinitionsContentTests.cs
tests/backend/GameServer.Domain.Tests/TASK309BossCastPriorityBoardSimulationTests.cs   (new)
```

### Tests — Application

```text
tests/backend/GameServer.Application.Tests/BattleStartServiceTests.cs
tests/backend/GameServer.Application.Tests/BossCardCastRuntimeTests.cs
tests/backend/GameServer.Application.Tests/RoundTestSupport.cs
tests/backend/GameServer.Application.Tests/TASK309BossCardEffectRuntimeTests.cs        (new)
tests/backend/GameServer.Application.Tests/TASK309StatusEffectLifecycleTests.cs        (new)
tests/backend/GameServer.Application.Tests/Balance/TASK309/BossCardCastFrequencyAnalysisTests.cs (new)
```

### Tests — Infrastructure

```text
tests/backend/GameServer.Infrastructure.Tests/PetCardRelicDefinitionProvisioningTests.cs
tests/backend/GameServer.Infrastructure.Tests/PetCardRelicDefinitionPostgresProvisioningTests.cs
tests/backend/GameServer.Infrastructure.Tests/BossDefinitionPostgresProvisioningTests.cs
tests/backend/GameServer.Infrastructure.Tests/BossDefinitionBalanceLookupTests.cs
tests/backend/GameServer.Infrastructure.Tests/BossPersistenceTests.cs
tests/backend/GameServer.Infrastructure.Tests/CardEffectDefinitionPersistenceTests.cs
tests/backend/GameServer.Infrastructure.Tests/TASK309BossCardProvisioningMigrationTests.cs (new)
```

### Tests — Api

```text
tests/backend/GameServer.Api.Tests/TestProvisionedContent.cs
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
tests/backend/GameServer.Api.Tests/BossResponseWireTests.cs
tests/backend/GameServer.Api.Tests/BossWireProjectionTests.cs
tests/backend/GameServer.Api.Tests/TASK309BossCardWireTests.cs                        (new)
```

### Engineering-record artifacts

```text
tasks/artifacts/TASK-309-boss-cast-priority-baseline.json          (new)
tasks/artifacts/TASK-309-in-battle-cast-frequency.json             (new)
```

*(Final set size is revalidated against the actual implementation before
Phase A; the exact migration stamps are substituted verbatim above.)*

---

## 6. Scope

### In Scope

- W1: the approved documentation/ADR amendments (§1 values and resolved U-2/
  U-3/U-4/U-5 entries; the DATABASE.md effect-type/duration/scope contract;
  the provisioned set and cast-priority values; the ADR index).
- W2: the four data-only migrations — one per ability, applied in the mandated
  order `flame-burst` → `root` → `drain-power` → `thunder-strike` — each
  inserting exactly one `BossCard` `CardDefinition` row and setting that
  Boss's `CastPriorityScore = 85`; each `Down` removing only its row and
  restoring `NULL`.
- W3: the four `CardEffectType` members and their structured definition
  validation/serialization: `duration` on `AtkBuff`/`AtkDebuff`,
  `scope = NextAttack` on `NextAtkBuff`, `Flat` magnitude on `PowerDrain`;
  unknown/unsupported values still fail closed.
- W4: the shared execution path resolves the four effects actor-relatively —
  Boss self ATK buff, active Pet ATK debuff, active Pet Power drain (with the
  authoritative `PowerChanged` event), and Boss next-qualifying-attack buff —
  and rejects the Boss-only effects for a Player cast.
- W5: the Application phase carries the cast's authoritative effect events
  into the round's single ordered batch before `BossCardCast`; the four
  provisioned abilities select through the unchanged one-action policy and
  set their authored cooldowns.
- W6: the V-1 reproducible 10,000-board baseline and the post-implementation
  in-battle cast-frequency analysis, both recorded as engineering artifacts.
- W7: the V-2 lifecycle tests (durations, expiry with/without qualifying
  attacks, trigger consumption, multiplicative stacking).
- W8: the V-3 migration `Up`/`Down` execution against a disposable test
  PostgreSQL database via `dotnet ef database update`.
- W9: regression/integration/wire verification and the full validation gates.

### Out of Scope

- Retiring `SkillCharge`, `BossSkillCast`, or the legacy handler (Slice 5's
  parity gate is not met by this task).
- Changing the legacy `BossSkill` form's behavior or its persisted
  `SecondaryEffect` values; the four bosses' runtime form becomes `BossCard`
  through the provisioned content.
- Additive ATK stacking (B-4 preserves multiplicative stacking).
- Rebalancing any other Boss, Player Card, Relic, Gem or encounter mechanic.
- The fifth Gem / Power-drain Gem (FUTURE; `MVP_SCOPE.md` §3).
- Client-authoritative gameplay logic, new wire discriminators, a Boss loadout
  or any Player-visible Boss Card surface.
- Fixing the pre-existing Boss provisioning `BaseDamage` drift, the stale
  `BattleHub` projection comment, or the Signature-Skill category guard (P-8).
- Pushing to the remote.

---

## 7. Acceptance Criteria

Derived from the operator decision record and the authoritative owners:

- [x] Approved decisions/values are recorded in §1 before any implementation
      edit; no approved value is changed.
- [x] `CardEffectType` exposes exactly the four new Boss-only identities;
      unknown/unsupported values and malformed extra members fail closed.
- [x] `DATABASE.md`'s effect contract documents the four identities and the
      `duration`/`scope` usage; serialization round-trips every one.
- [x] A Player cast cannot use any of the four Boss-only effects; a Boss cast
      cannot use `Damage`/`Crit`; all rejections write no state and spend no
      Power.
- [x] `flame-burst` Boss Card: cost 40, +50% Boss ATK, duration 3, cooldown 2;
      the buff applies to at most two qualifying Boss attacks and expires at
      the documented step even across rounds with no qualifying attack.
- [x] `root` Boss Card: cost 35, -30% Pet ATK, duration 3, cooldown 2; it
      applies to at most two Player attacks and expires at the documented
      step even across rounds with no Player attack.
- [x] `drain-power` Boss Card: cost 25, cooldown 3, subtracts exactly 20 flat
      Pet Power (floor 0) once, transfers no Power to the Boss, emits the
      authoritative `PowerChanged`, and creates no StatusEffect instance.
- [x] `thunder-strike` Boss Card: cost 40, cooldown 0, +50% Boss ATK modifier
      that survives non-qualifying rounds and is consumed exactly once by the
      next qualifying Boss attack.
- [x] Multiplicative ATK stacking is unchanged (`1.20 × 1.50 = 1.80`).
- [x] Each migrated Boss resolves the `BossCard` form at battle creation with
      `CastPriorityScore = 85`; selection stays `cast only if strictly
      greater than the board score`, board wins ties, and exactly one action
      resolves per response phase.
- [x] The four migrations are ordered `flame-burst` → `root` → `drain-power` →
      `thunder-strike`; `Up` creates exactly the four rows and sets the four
      scores; `Down` removes only those rows and restores `NULL`; Player
      Cards, `earthquake` and Sơn Thạch Vệ's score `16` are unchanged; the
      schema is unchanged; V-3 runs against a disposable test PostgreSQL
      database.
- [x] V-1: the 10,000-board methodology, seeds and score distribution are
      recorded reproducibly; the board-only cast probability is kept separate
      from the in-battle measurement; the in-battle analysis includes Power
      and cooldown eligibility; no global-optimality or in-battle-rate
      guarantee is stated.
- [x] `BossCardCast` remains the only Boss cast event; `BossSkillCast` is not
      emitted for a migrated ability; Player `CardCast`/`PetSkillCast`
      contracts are byte-identical; the Boss's own Power spend emits no
      `PowerChanged`.
- [x] Full gates: `dotnet build`, backend tests, frontend `test:run` and
      `build`, `git diff --check`; pre-existing failures documented as
      unchanged; quality review; two-stage commit with the exact declared
      set.

---

## 8. Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Infrastructure)
[x] tests/ (Domain / Application / Infrastructure / Api)
[x] docs/ (BOSS_RULES, DATABASE, GAME_EVENTS, ADR-029, ADR index)
[x] .ai/PROJECT_STATE.md (navigation cache refresh)
[ ] src/frontend/client/ (no change expected: the existing Boss display-name
    resolver already covers all five abilities and BossCardCast presentation
    is unchanged)
```

---

## 9. Implementation Notes

- **Form resolution is unchanged**: the four provisioned `BossCard` rows make
  the four Bosses resolve the `BossCard` form at battle creation
  (`BOSS_RULES.md` §4.4 item 6); no second resolution path is added.
- **Vocabulary**: `AtkBuff`/`NextAtkBuff` are self effects on
  `BossState.ActiveStatusEffects`; `AtkDebuff`/`PowerDrain` are opponent
  effects on `PetState`. Identity is the Boss Card's own definition id
  (= the ability `SkillId`). `AtkBuff`/`AtkDebuff` use the existing Turn-based
  `BuffDebuff` lifecycle; `NextAtkBuff` uses the existing trigger-based
  `NextQualifyingAttack` condition; `PowerDrain` is the existing instant
  `PetState.Power` reduction (no instance).
- **Stacking**: `StatusEffectLifecycle.EffectiveBossAttack`'s sequential
  multiplicative fold is unchanged; no new stacking model is introduced.
- **Events**: a Boss Card cast still emits exactly one `BossCardCast` at 18c;
  `PowerDrain` additionally emits the existing authoritative `PowerChanged`
  for the Pet-side mutation, carried before `BossCardCast` inside the round's
  single ordered batch. The Boss's Power spend itself emits no `PowerChanged`.
- **Migrations**: data-only `InsertData` + `UpdateData`, mirror `DeleteData` +
  `UpdateData` in `Down`; no schema operation and no
  `GameDbContextModelSnapshot.cs` change (validate with
  `dotnet ef migrations has-pending-model-changes`).
- **V-1 harness**: deterministic `Pcg32` board stream from a fixed master
  seed, `BoardGenerator.Generate` + `BossAiEngine.SelectMove` (real
  production scoring, including deadlock recovery), 10,000 boards, recorded
  histogram and `P(boardScore < score)` for `25`, `85`, `100`; the artifact
  records the methodology, seed and distribution. The in-battle measurement
  drives real rounds/battles for the four migrated Bosses and reads the
  committed state/events; it reports actual cast frequency separately from
  the board-only probability.
- **V-3 harness**: a downloaded portable PostgreSQL distribution initializes a
  disposable cluster on a non-production port/database, applies the migration
  chain via `dotnet ef database update`, verifies `Up`, reverts to the prior
  migration, verifies `Down`, and removes the cluster.

---

## 10. Testing Requirements

### Required Verification

```text
[ ] Unit tests — CardEffectType/definition validation and serialization;
                 shared executor for each new effect and each rejection path.
[ ] Integration tests — real round with provisioned content for the four
                 migrated Bosses: policy selection, cost, cooldown, effect
                 state, events, duration/expiry and trigger consumption.
[ ] Gameplay scenarios — Given a migrated Boss with enough Power and an
                 available board action, When the cast wins selection, Then
                 exactly one BossCardCast occurs with the approved effect and
                 no Boss board resolution/attack; When the board wins, the
                 legacy board action is unchanged.
[ ] Persistence — migration Up/Down against disposable PostgreSQL (V-3);
                 provisioned row set and cast-priority values.
[ ] Simulation — reproducible 10,000-board baseline and in-battle cast
                 frequency analysis (V-1).
```

### Key Edge Cases

- Power exactly equal to the authored cost and one below it; cooldown 0 vs 1.
- `flame-burst`/`root` duration 3 with 0, 1 and 2 qualifying attacks in the
  window; expiry after a round with no qualifying attack.
- `thunder-strike` modifier surviving multiple non-qualifying rounds and being
  consumed exactly once by the first qualifying Boss attack.
- `drain-power` at Pet Power 0/10/20/21/100 (floor and exact delta); no Boss
  Power gain; one `PowerChanged`.
- Player cast attempt with a Boss-only effect; Boss cast with
  `Damage`/`Crit`; undefined effect identity; malformed `duration`/`scope`.
- Board score exactly 85 (tie → board) and 84 (cast wins).
- Migration `Down` restores `NULL` only for the four rows.

---

## 11. Stop Conditions

- If an authoritative document contradicts an approved value in a way that
  cannot be reconciled through the documented amendment process: STOP and
  report the exact conflict (`AGENTS.md` §4/§20).
- If a Boss-only effect cannot be represented through the approved closed
  vocabulary without inventing a new gameplay rule: STOP.
- If the V-3 disposable PostgreSQL workflow cannot be executed: STOP and
  report the environment blocker rather than substituting source inspection.
- If implementation would require client-authoritative logic: STOP.
- If a test can only pass by weakening an authoritative contract: STOP/report.

---

## 12. Completion Evidence

### Commit

- Implementation slice (Phase A): `4d97c7407095ec6a8320c1d43bce9a386fc183d1`
- Subject: `feat: migrate the four remaining boss abilities to boss cards`
- Owns: `TASK-309`
- Files: the exact 46-file set in §5 (staged-set equality verified:
  `staged=46 expected=46`, zero missing/extra, task record excluded)
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files

Authoritative documentation and cache (6)
- `docs/01-game-design/BOSS_RULES.md` — v2.16: §4.3 item 5 score 85, §4.4 items
  5–6 migrated abilities, §6.3/§6.3.1 approved costs/magnitudes/durations and the
  explicit legacy-vs-BossCard 2/3-Turn divergence, B-4 multiplicative stacking.
- `docs/02-technical/DATABASE.md` — v1.41: four Boss-only effect identities,
  `duration` extended to AtkBuff/AtkDebuff, `scope` extended to NextAtkBuff
  (`NextAttack` only), 13-row provisioned set, 16/85 scores, cast-boundary
  refusal wording.
- `docs/02-technical/GAME_EVENTS.md` — v2.19: PowerDrain's Pet-side
  `PowerChanged` before `BossCardCast`, no Boss-spend `PowerChanged`, no new
  discriminator; legacy drain step reference corrected to 18c.
- `docs/03-decisions/ADR/ADR-029-…md` — in-place Slice 4 amendment (U-2/U-3/
  U-4/U-5 resolved, Slice 4 delivered, Slice 5 gated, B-4 recorded).
- `docs/03-decisions/README.md` — v1.24 ADR index/in-place-amendment record.
- `.ai/PROJECT_STATE.md` — v1.4 navigation-cache refresh for the pre-commit
  HEAD `081f9d7`, TASK-309 IN REVIEW, gate evidence, next action.

Domain (5)
- `CardEffectType.cs` — `AtkBuff`, `PowerDrain`, `AtkDebuff`, `NextAtkBuff`
  as Boss-only identities with documented ownership.
- `CardEffectDefinition.cs` — factories, fail-closed validation/serialization
  (`duration` iff Burn/AtkBuff/AtkDebuff; `scope` iff Crit/NextAtkBuff, value
  `NextAttack`; fixed `Flat`/`PercentagePoints` units).
- `CardEffectDefinitions.cs` — contract notes/examples synchronized.
- `CardCastExecutor.cs` — actor-relative resolution of the four effects; the
  Boss branch returns the PowerDrain `PowerChanged`; Player cast refuses the
  four Boss-only identities before any state change; Boss Damage/Crit refusal
  retained.
- `BossDefinitions.cs` — `CastPriorityScore = 85` on Hỏa Long, Thủy Ma, Mộc Yêu,
  Kim Lôi Vương; Sơn Thạch Vệ remains 16; legacy secondary effects/durations
  unchanged.

Application (1)
- `BattleStateService.cs` — 18c cast effect events carried into the round's
  single ordered batch before the one `BossCardCast`.

Persistence (8)
- Four data-only migration pairs in the mandated order: `flame-burst` →
  `root` → `drain-power` → `thunder-strike`; each inserts one `BossCard` row
  and sets that Boss's score 85; each `Down` deletes only its row and restores
  `NULL`. No schema change; model snapshot unchanged; existing migrations
  untouched.

Tests (24)
- Domain (6): card effect definition/definitions, Boss cast executor, Player
  compatibility, Boss content scores, V-1 board simulation.
- Application (6): round support/provisioned fixtures, Boss Card creation and
  cast regression, TASK-309 effect runtime (10 tests), V-2 lifecycle (5 tests),
  V-1 in-battle frequency analysis (2 tests).
- Infrastructure (7): provisioning/migration/score/persistence suites updated
  for the 13-row set and 16/85 scores; new V-3 migration test.
- Api (5): 13-row fixture, closed discriminator/no-extra-member/order
  expectations, five-ability `BossCardCast` projection, new end-to-end wire
  suite (8 tests).

Engineering artifacts (2)
- `tasks/artifacts/TASK-309-boss-cast-priority-baseline.json`
- `tasks/artifacts/TASK-309-in-battle-cast-frequency.json`

### Validation Results

```text
[x] dotnet build src/backend/GameServer.sln --nologo --verbosity q
      0 Error(s); pre-existing warnings only
[x] Full backend suites (Lead final gate, --no-build)
      Domain           1691 passed / 0 failed / 1691
      Application       827 passed / 2 failed / 829   (2 pre-existing, unchanged)
      Infrastructure    502 passed / 4 failed / 506   (4 pre-existing, unchanged)
      Api               405 passed / 0 failed / 405
[x] Frontend (cwd src/frontend/client; no client file changed)
      npm run test:run   24 files, 1271 / 1271 PASS
      npm run build      tsc + vite build success
[x] git diff --check   exit 0
[x] dotnet ef migrations has-pending-model-changes
      "No changes have been made to the model since the last migration."
[x] V-1 baseline — 10,000 boards, frozen Pcg32 stream
      seed 0x5EED030900000001 / increment 0x0309; P(<25)=0.00%,
      P(<85)=36.97%, P(<100)=66.43%; min 30 / max 180 / mean 88.6915;
      ordered-score SHA-256 7b477ada5ed81d5f064a9ff752a3d62996a42531f7a465e64bdffab35488f940
[x] V-1 in-battle — real pipeline, 40 battles/boss, seeds 0x5EED030900000001+b
      9,665 response phases / 2,903 casts; cast frequency flame-burst 30.28%,
      drain-power 26.85%, root 31.66%, thunder-strike 31.20%,
      aggregate 30.04%; Power eligible 71.30%, cooldown eligible 68.47%,
      both 48.49%; zero contractual violations
[x] V-2 — flame-burst duration 3 affects at most two qualifying Boss attacks
      and expires through a no-attack round; root mirrors it for Player
      attacks; thunder-strike consumed exactly once after non-qualifying
      rounds; multiplicative 1.20 × 1.50 = 180 (not additive 170)
[x] V-3 — disposable portable PostgreSQL 18.6.1 on port 5544:
      `dotnet ef database update` applied the four migrations in order
      (13 CardDefinition rows; scores 85/85/85/16/85; earthquake and Player
      Cards unchanged; schema unchanged); reverting to the prior migration
      removed only the four rows and restored NULL; re-apply was byte-identical;
      cluster stopped/deleted; no production database touched
```

Known pre-existing failures (unchanged; reproduced on the final tree):
```text
Application (2, BalanceEvidenceMeasurementCorrectionTests):
  TASK195_PerTurnSeries_ReconcilesTerminalCardCastWithoutPhantomTurn
  TASK296_ReviewD_Canonical75RunCandidate_Verification
Infrastructure (4):
  BossPersistenceTests.ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments
  BossPersistenceTests.Task172ProvisioningMigration_ShouldWriteTheDocumentedPassiveAndSkillDocuments
  BossDefinitionPostgresProvisioningTests.Postgres_ShouldRoundTripBothJsonDocumentsToTheDomainDefinitions
  BossDefinitionBalanceLookupTests.GetDefinitionByIdentityAsync_ShouldPreserveTheNonPersistedSecondaryEffects
```

### Server Authority & Scope Verification

- [x] No client-authoritative gameplay logic; the only client surface
      (`BossCardCast` presentation) is unchanged and the client resolves the
      existing ability display names.
- [x] MVP scope: Boss Card capability remains IN; the fifth Gem is FUTURE and
      untouched; no new system or content beyond the approved four Boss Cards.
- [x] `SkillCharge`, `BossSkillCast` and the legacy handler are retained; no new
      work builds on them; all five MVP Bosses resolve the BossCard form from
      provisioned content.
- [x] Server authority, determinism and RNG: selection remains the pure
      `BossActionPolicy` over committed state; no new RNG, clock or client input.
- [x] One action per response phase, one atomic write, one ordered event batch,
      CAS-retry determinism preserved; a Boss Card cast's effect events precede
      the single `BossCardCast`.
- [x] Player `CardCast`/`PetSkillCast` contracts byte-identical; Boss-only
      effects are refused for a Player cast.
- [x] P-1…P-6 commit policy: single `master`, no branch/tag, Phase A one commit
      with the exact declared set, task record excluded, no amend/rebase/force
      push/`commit -a`, no push.

### Remaining Limitations

- The legacy `BossSkill` form's persisted `flame-burst`/`root` duration remains
  its historical 2 Turns and is not reachable for a migrated Boss at runtime;
  it is retired with `SkillCharge` under Slice 5's parity gate.
- The V-1 in-battle analysis reads the Boss's selected board score indirectly
  (the score is not emitted), so it reports observed eligible-but-no-cast counts
  rather than per-phase board-threshold verification.
- The V-1 in-battle fixtures provide no Player Card/Relic loadout and use 40
  shared seeds with an 80-round cap; the numbers are evidence, not a balance
  target, and 85 remains a starting value to monitor.
- Pre-existing drift reported, not fixed: the historical Boss provisioning
  `BaseDamage` values, the stale `BattleHub` comment, the Signature-Skill
  category guard (P-8), and `BOSS_RULES.md` §6's retired direct-damage rows.

