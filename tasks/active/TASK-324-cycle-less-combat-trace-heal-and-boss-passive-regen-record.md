# TASK-324 — Cycle-less combat-trace heal and Boss Passive regeneration record

---

## Operator Decision Recorded Here (this session, 2026-10-23)

```text
OPTION 1 — AUTHORIZE PHASE-LIMITED DELIVERY.

Git operations of every kind remain PROHIBITED in this session.
The permitted terminal state for this delivery is: IN REVIEW.
```

This task's record therefore sits in `tasks/active/` with `Status: IN REVIEW`.
**It is not `DONE`.** The two lifecycle stages it cannot execute are disclosed in
`## Completion Evidence`:

- **Phase A implementation commit — NOT EXECUTED**, because Git operations are
  prohibited.
- **Phase B completion record — NOT FILED as a completed task**, because the
  required Phase A commit SHA does not exist.
- **Required operator action:** perform the authorized lifecycle finalization
  (the two-stage commit protocol, `tasks/TASK_LIFECYCLE.md` §6) separately when
  Git operations are permitted.

No commit SHA is reported, no lifecycle evidence is fabricated, and nothing in
this record claims the delivery reached `DONE`.

**Second operator decision read this session and recorded in
`.ai/PROJECT_STATE.md` §3:** OD-322-1 is answered **A** (`cycleOrdinal` becomes
optional end to end; an inapplicable ordinal is omitted, never `null`, never
`0`). It is identified there as an operator decision made in this session; it is
**not** presented as an earlier approval, and no historical OD-322-1 record is
claimed to exist. TASK-322 recorded it as *open*.

---

## Governance Preflight

Captured before any edit. **No Git command of any kind was executed in this
session** (`git status`, `git diff`, `git log`, `git add`, `git commit`, and
every other Git operation were forbidden), so branch, HEAD, and staging state
were **not** machine-read and no claim is made about them.

```text
Source of task ID:   tasks/README.md §6 (highest existing ID + 1). Highest
                     existing ID in the tree is TASK-323 (a DONE execution that
                     left its evidence in SIGNALR_PROTOCOL.md 2.33 but no record
                     file), so the next sequential ID is TASK-324.
Archive status:      The original TASK-324 patch archive was NOT AVAILABLE in any
                     searched location (repository tree, tasks/artifacts/, OS
                     temp, and a bounded filesystem search). The gameplay edit
                     was RECONSTRUCTED from verified current source, the
                     existing (defective) TASK-324 regression tests, and
                     BOSS_RULES.md §6.2.3's established passive-heal semantics.
                     It is NOT claimed to be the original archived patch.
Baseline captured:   before the first edit: dotnet build 0 errors; dotnet test
                     Domain 1700 / Application 803 / Api 408 / Infrastructure
                     507 = 3418, with the TWO defective TASK-324 regen tests
                     failing (Application 801 passed, 2 failed).
Unowned work:        the repository has pre-existing uncommitted edits with no
                     owning manifest (disclosed in .ai/PROJECT_STATE.md §6); none
                     is claimed, reverted, or reformatted by this task.
```

---

## Metadata

```text
Task ID:           TASK-324
Type:              GAMEPLAY-CHANGE
Status:            IN REVIEW
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     gameplay
Supporting Agents: backend, realtime, testing, review
Workflow:          development/gameplay-change.md
Skills:            realtime/realtime-protocol-validation, gameplay/gameplay-behavior-derivation, testing/test-scenario-generation, quality/scope-validation, quality/documentation-consistency
Dependencies:      TASK-321 (combatTrace, DONE); TASK-322 (omit-rule reconciliation, DONE); OD-322-1 = A (operator, this session)
Declared Files:    src/backend/GameServer.Domain/Match3/CombatTrace.cs, src/backend/GameServer.Api/Hubs/CombatTraceWireProjection.cs, src/backend/GameServer.Application/Battle/BattleStateService.cs, src/frontend/client/src/game/runtime/GameRuntimeEvents.ts, tests/backend/GameServer.Application.Tests/CombatTraceResolutionTests.cs, tests/backend/GameServer.Api.Tests/CombatTraceWireTests.cs, docs/02-technical/SIGNALR_PROTOCOL.md, .ai/PROJECT_STATE.md
```

---

## Objective

Make the `SIGNALR_PROTOCOL.md` §3.3.1 not-applicable omission for
`healing[].cycleOrdinal` **representable and round-trippable** (OD-322-1 = A),
and record `BOSS_RULES.md` §6.2.3's Boss Passive regeneration — a heal no actor
cycle produces — in `CombatTrace.Healing` exactly once, with no cycle ordinal, so
the trace accounts for the Boss's whole HP movement.

---

## Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md` §3 item 5, §3.2.5, §3.2.3, §3.3,
  §3.3.1 (Heal object + presence rule), §3.3.2 items 6–7, §8 item 10
- `docs/01-game-design/BOSS_RULES.md` §3.3 item 1, §6.2.3
- `docs/01-game-design/COMBAT_RULES.md` §4 items 1 and 7
- `docs/01-game-design/PASSIVE_RULES.md` §2 items 1 and 3, §4
- `docs/01-game-design/GAME_RULES.md` §17 (step 18b)
- `docs/02-technical/GAME_STATE.md` §5.1
- `docs/00-overview/MVP_SCOPE.md` §1
- `tasks/completed/TASK-321-server-authoritative-combat-trace-and-cascade-attribution.md`
- `tasks/completed/TASK-322-reconcile-combat-trace-not-applicable-member-omission.md`
- `AGENTS.md` §4, §10, §14, §15, §17, §20, §21

---

## Scope

### In Scope
1. Domain model, wire DTO, serializer behaviour, frontend type/reader, and the
   wire-test tripwire for an **optional cycle ordinal** (OD-322-1 = A).
2. Record the HP actually restored by `boss-moc-yeu-regen` **exactly once** in
   `CombatTrace`.
3. Use an **absent** cycle ordinal for that passive heal.
4. Repair the defective TASK-324 regression tests with verified passive-threshold
   semantics.
5. Verify the zero-effective-heal case and prove no duplicate trace entry.

### Out of Scope
- TASK-323's `SIGNALR_PROTOCOL.md` §3.3 example correction (re-read, untouched
  apart from the version/changelog and §8 clauses this task records).
- `statusTick` behaviour, card heal, pet heal, `ResourceGenerator`,
  `AttackResolution`, `PassiveTracker`, any combat value, formula, resolution
  order, RNG draw, event, state, Redis, PostgreSQL, hub method, or subscription.
- TASK-316, TASK-320, and any release/deployment action.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.
- Commits, staging, pushes, or history repair.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-324:
```text
src/backend/GameServer.Domain/Match3/CombatTrace.cs
src/backend/GameServer.Api/Hubs/CombatTraceWireProjection.cs
src/backend/GameServer.Application/Battle/BattleStateService.cs
src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
tests/backend/GameServer.Application.Tests/CombatTraceResolutionTests.cs
tests/backend/GameServer.Api.Tests/CombatTraceWireTests.cs
docs/02-technical/SIGNALR_PROTOCOL.md
.ai/PROJECT_STATE.md
```

*(The task manifest file itself is excluded from the declared implementation
file set, per `TASK_LIFECYCLE.md` §6.1 item 1.)*

---

## Problem Addressed and Evidence

**P-1 — the omission was stated but unrepresentable (OD-322-1, open at start).**
`SIGNALR_PROTOCOL.md` §3.3.1/§3.3.2 item 7 (as reconciled by TASK-322) require a
heal belonging to no actor cycle to omit `cycleOrdinal`. But
`CombatTraceHeal.CycleOrdinal` and `CombatTraceHealWireDto.CycleOrdinal` were
non-nullable `int`, with the sentinel `CombatTrace.NoCycle = 0` standing in for
"no cycle". A producer could therefore only write `0` — a value the presence rule
forbids — or a real cycle. The documented state was unreachable from the Domain.

**P-2 — the regeneration was never traced.** `BattleStateService`'s step-18b
`boss-moc-yeu-regen` branch updated `BossState.HP` directly and appended nothing
to `traceHeals`, so a Round's `healing[]` omitted the regen and
`trace.Boss.HpAfter - trace.Boss.HpBefore` did not reconcile with the trace's
damage and heals.

**P-3 — the TASK-324 regression tests were defective.** Both repaired tests
pinned the Boss Passive's progress to `threshold - 1` (7 at the authored 8) and
then expected the Passive to *fire*. Under `PASSIVE_RULES.md` §2 item 3 the
threshold is evaluated **once, after the whole batch**, at `P + N`, and a first
Match clears at least 3 cells against the lightest weight of 5, so `7 + N ≥ 10`
only after an unforeseen board — the tests' premise (`one Match below a
threshold`) was wrong, and the control arm's "reason" (threshold 1000) was not
what made it not fire. `BossPassiveEffectsTests` (TASK-200) derives the crossing
from the Turn's own batch instead (`P = 8 − N`); the repair adopts that verified
semantics.

**Resolution (reconstructed, not the archived patch).**
```text
Domain      CombatTraceHeal.CycleOrdinal: int -> int?
            CombatTrace.NoCycle sentinel removed (no value stands in for "no cycle")
Wire        CombatTraceHealWireDto.CycleOrdinal: int? + WhenWritingNull
Producer    the regen branch records ONE CombatTraceHeal with CycleOrdinal: null
            and Amount = the HP the MaxHP clamp actually restored
Frontend    CombatTraceHealing.cycleOrdinal?: number; reader omits it when absent
Tests       the wire tripwire asserts the REAL serialized member set in both
            directions; the Application tests arrange progress from the batch
```

---

## Acceptance Criteria

- [x] 1. `CombatTraceHeal.CycleOrdinal` is `int?`; a heal that belongs to no
      actor cycle carries `null`, never `0`.
- [x] 2. `CombatTraceHealWireDto.CycleOrdinal` is `int?` with the documented
      `WhenWritingNull` omission, so the member is absent from the JSON.
- [x] 3. A heal that names a cycle still serializes its numeric `cycleOrdinal`,
      and a real `0` `amount` is still written.
- [x] 4. Neither `null` nor `0` is substituted for an inapplicable ordinal
      anywhere in the serialized payload.
- [x] 5. The frontend reader accepts an absent `cycleOrdinal` without inventing a
      value, and still rejects a malformed (non-numeric) one.
- [x] 6. `boss-moc-yeu-regen` records exactly one `CombatTrace` heal naming the
      effective HP restored, with `RawAmount` = the authored 5% and an absent
      cycle ordinal.
- [x] 7. The full-HP (zero-effective-heal) case is reported once with
      `amount = 0`, not as "no heal", and produces no duplicate entry.
- [x] 8. The control arm is genuinely below its passive threshold.
- [x] 9. Every pre-existing test in all four backend projects and the whole
      frontend suite passes; both builds pass.
- [x] 10. `SIGNALR_PROTOCOL.md` states the now-reachable rule (version +
      changelog + §8 item 10) without changing any other presence decision.
- [x] 11. No authoritative rule or contract is violated (`AGENTS.md` §4, §10).

---

## Affected Files & Areas

```text
[x] src/backend/ (Domain / Application / Api)
[x] src/frontend/client/ (runtime)
[x] tests/ (unit / integration)
[x] docs/ (SIGNALR_PROTOCOL.md)
[x] .ai/ (PROJECT_STATE.md — operator-decision record)
```

---

## Implementation Notes

- The Domain stays serialization-free: the omission is the transport's job
  (`ARCHITECTURE.md` §2.1 item 4), exactly as `CombatTraceDamage.CycleOrdinal`
  and the cycle cells already work.
- The regen heal is recorded at the **single** site that writes Boss HP for that
  Passive, from the clamped result, so the amount is the effective increase and
  a second record is structurally impossible.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — serialized-wire omission/presence of heal cycleOrdinal
[x] Integration tests  — the delivered ReceiveEvents envelope still carries a trace
[x] Gameplay scenarios — the regen heal, its zero-effective case, and the
                         non-activating control
```

### Key Edge Cases
- A full-HP trigger: `amount = 0`, `rawAmount = 250`, exactly one record.
- A cycle-owning heal keeps a numeric ordinal (`PLAYER` heal names cycle 1).
- A malformed `cycleOrdinal` (non-numeric) still makes the frontend trace
  malformed rather than defaulting.

---

## Stop Conditions

- If resolving OD-322-1 required a new wire member or a new optionality decision
  for any other member: STOP (`AGENTS.md` §18). It did not.
- If the regen amount could not be derived from the authoritative value the
  resolution already computed: STOP (`AGENTS.md` §7). It is read from the
  clamped `BossState.HP`.
- If a change would make the client authoritative over HP: STOP
  (`AGENTS.md` §10). None did.

---

## Completion Evidence

### Commit

```text
Phase A implementation commit:  NOT EXECUTED
  Reason: Git operations of every kind are prohibited in this session (operator
          decision, this session). Per TASK_LIFECYCLE.md §6.1 the Phase A
          commit is executed inside the IN REVIEW -> DONE transition, so no
          SHA exists and none is reported or invented.

Phase B completion record:      NOT FILED
  Reason: Phase B files tasks/completed/<record>.md only after the Phase A SHA
          is captured (TASK_LIFECYCLE.md §6.2). With no Phase A SHA the record
          cannot be filed as DONE; it remains tasks/active/ with Status IN REVIEW.

Required operator action:
  Perform the authorized lifecycle finalization separately when Git operations
  are permitted: execute TASK_LIFECYCLE.md §6 Phase A over the declared file set
  below, capture the SHA, then execute Phase B to file this record in
  tasks/completed/ and set Status: DONE.
```

**Disclosure — the reconstructed edit is not the archived patch.** The original
TASK-324 patch archive could not be located. The gameplay change delivered here
was reconstructed from the verified current source, the existing regression
tests, and `BOSS_RULES.md` §6.2.3, and is recorded as a reconstruction. It is not
represented as the original archived patch.

### Changed Files

```text
src/backend/GameServer.Domain/Match3/CombatTrace.cs
  CombatTraceHeal.CycleOrdinal  int -> int?
  CombatTrace.NoCycle (const int = 0)  REMOVED — the sentinel is exactly the
    value the §3.3.1 presence rule and §3.3.2 item 7 forbid writing, and with a
    nullable member nothing substitutes for an inapplicable ordinal. Doc comments
    on CombatTrace.Healing and
    CombatTraceHeal re-state the omission as an absent member.
  No other member, list, ordinal, or snapshot changed.

src/backend/GameServer.Api/Hubs/CombatTraceWireProjection.cs
  CombatTraceHealWireDto.CycleOrdinal  int -> int? with
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    (same treatment CombatTraceDamageWireDto.CycleOrdinal already had).
  Projection unchanged (still a pure field mapping); the projection summary's
  list of optional members now names the uncycled heal's cycleOrdinal.

src/backend/GameServer.Application/Battle/BattleStateService.cs
  Step-18b `boss-moc-yeu-regen` branch only: captures bossHpBeforeRegen, applies
    the existing clamp unchanged, and appends exactly ONE CombatTraceHeal
    (Ordinal = ++traceHealOrdinal, CycleOrdinal: null, Target: Boss,
     Amount = clamped difference, RawAmount = totalRegen, HpBefore/HpAfter).
  No combat value, order, RNG draw, event, or state change; the HP write is the
    same expression as before.

src/frontend/client/src/game/runtime/GameRuntimeEvents.ts
  CombatTraceHealing.cycleOrdinal  required number -> optional number.
  readCombatTraceHealing reads it with readOptionalNumber (absent/null ->
    undefined, non-finite -> malformed) and spreads it only when present.
  No presentation consumer reads the member (BattleEventPresenter's
    combatTraceMoveLogRows aggregates heal amounts only), so nothing is defaulted.

tests/backend/GameServer.Application.Tests/CombatTraceResolutionTests.cs
  Repaired the two defective TASK-324 regen tests; added a shared
    ResolveMocYeuRoundAsync helper and MocYeuTracedRound record carrying the
    arm's pinned threshold/progress. Assertions now pin: exactly one Boss heal;
    amount/raw/Hp pair from the clamp; null cycle ordinal; non-vacuity guards on
    the batch; the control's exact below-threshold arrangement; and Boss HP
    reconciliation against the trace's own damage and heals.
  One pre-existing assertion adjusted for the widened type
    (CombatTrace_EveryHealAndAttackCycleOrdinal_ShouldNameAnExistingCycle now
    also rejects a NULL heal ordinal) — the test's guarantee is unchanged and
    strictly stronger on the pinned path.

tests/backend/GameServer.Api.Tests/CombatTraceWireTests.cs
  CombatTraceWireDto_HealCycleOrdinal_ShouldBeOmittedWhenTheHealBelongsToNoCycle
    no longer pins the DTO's *inability* to express the omission. It now asserts
    the ACTUAL serialized shape from the transport DTOs: the uncycled heal's
    exact member-name set (no cycleOrdinal), the cycled heal's numeric value, a
    real 0 amount still written, no null token, no "cycleOrdinal": 0, and an
    absent member that stays absent across a wire round trip.

docs/02-technical/SIGNALR_PROTOCOL.md
  Version 2.33 -> 2.34 with a TASK-324 changelog clause (OD-322-1 = A; member
  made representable; no shape change).
  §8 item 10: one clause recording that TASK-324 adds and removes no member and
  changes no member's required presence, so deployed clients and servers remain
  interoperable and no drain/deploy/version change is required.
  Unchanged: §3.3 example JSON (TASK-323's fix intact), every table, every
  other presence statement, §3.2.*, §4-§7.

.ai/PROJECT_STATE.md
  §3 gains the OD-322-1 = A operator decision (identified as made in this
  session, not a historical record) and the TASK-324 phase-limited-delivery
  decision. §6 gains the TASK-324 lifecycle-finalization item and two evidence
  disclosures. No other snapshot claim is altered.
```

### Validation Results

Executed in this session and observed:

```text
BASELINE (before the first edit)
dotnet build src/backend/GameServer.sln ....... 0 Error(s)
dotnet test  src/backend/GameServer.sln ....... Domain          1700 passed, 0 failed
                                                Application      803 total, 801 passed, 2 FAILED
                                                Api               408 passed, 0 failed
                                                Infrastructure    507 passed, 0 failed
                                                 (the 2 failures were exactly the defective
                                                  TASK-324 regen tests)

AFTER the change
dotnet build src/backend/GameServer.sln ....... 0 Error(s), 4 pre-existing MSB3277 warnings
dotnet test  src/backend/GameServer.sln ....... Domain          1700 passed, 0 failed, 0 skipped
                                                Application      803 passed, 0 failed, 0 skipped
                                                Api               408 passed, 0 failed, 0 skipped
                                                Infrastructure    507 passed, 0 failed, 0 skipped
                                                 = 3418 passed, 0 failed, 0 skipped
dotnet test --filter ~CombatTraceResolutionTests  Application 13 passed, 0 failed
dotnet test --filter ~CombatTraceWireTests        Api           7 passed, 0 failed
npm --prefix src/frontend/client run test:run ... 24 files, 1304 tests passed
npm --prefix src/frontend/client run build ...... built (0 TypeScript errors)
```

### Serialized Payload Verification (observed, not inferred)

An independent verifier constructed the real internal wire DTOs and projected a
real Domain `CombatTrace` through `CombatTraceWireProjection.Project`, then
serialized with the API's own `System.Text.Json`:

```text
cycled heal   {"ordinal":1,"cycleOrdinal":1,"target":"player","amount":0,"rawAmount":15,"hpBefore":100,"hpAfter":100}
uncycled heal {"ordinal":2,"target":"boss","amount":250,"rawAmount":250,"hpBefore":3000,"hpAfter":3250}
guards        no "cycleOrdinal":0 anywhere; no null token anywhere;
              the uncycled heal's member set is exactly
              {ordinal,target,amount,rawAmount,hpBefore,hpAfter}
fail-before   replaying the pre-change payload ("cycleOrdinal":0) fails the wire
              test's exact-member-set and DoesNotContain assertions
```

### Independent Verification (disclosed)

A separate verifier — its own execution, no Git, no writes in the repository —
re-ran the focused Application and Api suites, the whole frontend suite, built
the payload independently, and demonstrated fail-before in a temporary copy. It
reported **PASS on every check** (spec reading, serialized shape, fail-before,
single-record accounting, zero-effective-heal, control-below-threshold, preserved
numeric ordinals, frontend reader, encoding, scope). Its two observations are
non-defects: the documentation version is now 2.34 (this task's own documented
change), and `CombatTraceResolutionTests.cs` carries **pre-existing** mojibake on
lines this task did not introduce — those lines are untouched and reported, not
repaired (`AGENTS.md` §16).

### Acceptance Criteria Outcome

| AC | Verdict | Basis |
| --- | --- | --- |
| 1–2 | PASS | Both members are `int?`; the sentinel is gone. |
| 3–4 | PASS | Numeric ordinal written for a cycled heal; no `null`/`0` substitute observed in the serialized payload. |
| 5 | PASS | Reader omits an absent member; a non-numeric one still makes the trace malformed (checked independently). |
| 6–7 | PASS | Exactly one Boss regen record with the clamped amount; the full-HP case reports `amount = 0` once. |
| 8 | PASS | Control threshold = batch + 1 from progress 0; `P + N < threshold` asserted, `P + N` cannot fire. |
| 9 | PASS | 3418 backend + 1304 frontend tests pass; both builds pass. |
| 10 | PASS | Protocol version 2.34 + §8 item 10 clause; no other presence decision changed. |
| 11 | PASS | Domain/Application/Api/transport only; no client-authoritative value. |

### Server Authority & Scope Verification
- [x] No client-authoritative gameplay logic added; the frontend only parses.
- [x] No combat rule, formula, damage order, RNG draw, or balance value changed.
- [x] MVP scope unchanged (`MVP_SCOPE.md` §1); no new system or content.
- [x] `statusTick`, card heal, pet heal, and `ResourceGenerator` untouched.
- [x] **No Git operation performed; no file staged, committed, or pushed.**

---

## Lifecycle Status

ANCHOR-TASK-324-IN-REVIEW — this record is authoritative for this task's state.
Current state: `IN REVIEW`, in `tasks/active/`. Not `DONE`. Phase A and Phase B
are unexecuted by design of this session's operator decision; finalization is the
operator's to perform when Git operations are permitted.
