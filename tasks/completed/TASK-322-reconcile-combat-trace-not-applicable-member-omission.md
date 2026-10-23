# TASK-322 — Resolve the `combatTrace` not-applicable-member omission contradiction

---

## Governance Preflight (Phase 0)

Captured before any edit. **No Git command of any kind was executed in this
session** (the operative instruction forbids `git status`, `git diff`,
`git log`, `git add`, `git commit`, and every other Git operation), so the
branch, HEAD, and working-tree state were **not** machine-read and no claim is
made about them. What is recorded instead is what was observable without Git:

```text
Source of task ID:   tasks/README.md §6; highest existing ID is TASK-321
                     (tasks/completed/), so the next sequential ID is TASK-322.
Baseline captured:   before any edit, all four backend test projects, the whole
                     frontend suite, and both builds were executed and green
                     (see ## Baseline Evidence).
Unowned work:        the repository has pre-existing uncommitted edits with no
                     owning manifest; none is claimed, reverted, or reformatted
                     by this task.
```

---

## Metadata

```text
Task ID:           TASK-322
Type:              DOCUMENTATION
Status:            DONE
Risk:              LOW
Priority:          HIGH
Primary Agent:     realtime
Supporting Agents: backend, testing, review
Workflow:          documentation/documentation-change.md
Skills:            realtime/realtime-protocol-validation, quality/documentation-consistency, testing/test-scenario-generation, quality/scope-validation
Dependencies:      TASK-321 (combatTrace delivery, completed); no dependency on TASK-316 / TASK-320
Declared Files:    docs/02-technical/SIGNALR_PROTOCOL.md, tests/backend/GameServer.Application.Tests/CombatTraceResolutionTests.cs, tests/backend/GameServer.Api.Tests/CombatTraceWireTests.cs, tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
```

---

## Objective

Resolve, in favour of the document's own authored rule, the contradiction
inside `SIGNALR_PROTOCOL.md` §3.3 that §3.3.1's `Presence` column creates
against §3.3.2 item 7 — then close the representational gap that made
§3.3.2 item 7's `healing[].cycleOrdinal` omission unreachable, and pin both with
regression tests.

---

## Problem Addressed and Evidence

**P-1 — the documented contradiction (`AGENTS.md` §4, the reason this task
exists).** §3.3.2 item 7 states:

> "No member of this payload is optional except `fromCell`/`toCell` on a board
> cycle, `cardId` on a card cycle, and `cycleOrdinal` on a status tick — all of
> which are *not-applicable* omissions per §3.2.5."

`healing[].cycleOrdinal` is not in that list, so §3.3.2 item 7 makes it
mandatory and invalidates §3.3.1's own "omitted unless" cell for it (item 7's
supporting clause says "A real zero is `0`, an unknown value is omitted",
which does not apply to a member that is never unknown in practice). The two
readings are materially different for a client: reject the trace as malformed,
or treat it as absent.

**P-2 — the runtime makes `healing[].cycleOrdinal` non-omittable.** `CombatTraceHeal.CycleOrdinal` was
a non-nullable `int`, and every heal producer assigns it: the Pet's HP-gem heal
(`traceCycles` entry 1) and the Boss's (`traceCycles.Count`). So no delivery
can ever omit it, which means §3.3.1's "omitted unless" cell is permanently
dead text while §3.3.2 item 7 gives the member a rule that cannot fire.

**P-3 — a heal can legitimately belong to no cycle.** `ResourceGenerator`,
the Domain write site `COMBAT_RULES.md` §4 item 1 names, also exposes
independent HP writes (`ApplyHeal(PetState, int, …)`, `ApplyBossHeal(...)`) that
the heal stage of a Round does not own. The protocol cannot state a rule that
the Domain type makes unrepresentable.

**P-4 — the deliverable was unpinned.** No test asserted the omission for a
heal, and none asserted the `statusTick` omission on the serialized wire as a
*round trip with the member really absent* (the existing wire test only checked
the opposite direction — that a real `0` is written).

**Decision (resolution of P-1).** §3.3.2 item 7's rule wins and §3.3.1 is
corrected to agree with it — but **§3.3.1's cell alone cannot resolve the
conflict**, because item 7 is written as an *exhaustive closed list*: an
unedited item 7 would still make `healing[].cycleOrdinal` mandatory while a
corrected §3.3.1 called it omittable. The smallest change that actually removes
the double reading is therefore two coordinated edits:

```text
§3.3.2 item 7   the closed list gains its fourth not-applicable omission:
                cycleOrdinal on a heal that belongs to no actor cycle, and the
                item names §3.3.1 as the member-by-member statement of the rule
§3.3.1          the heal table's cycleOrdinal cell states that omission, and one
                short presence-rule paragraph under the tables points at item 7
```

No optionality decision for any other member changes, no schema changes, and the
§3.3 example is untouched. The normative statement lives in item 7 (as before);
§3.3.1 now agrees with it instead of contradicting it.

---

## Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.3, §3.3.1, §3.3.2 items 2, 3, 7,
  10; §3.2.5 (not-applicable omission); §8 item 10
- `docs/01-game-design/COMBAT_RULES.md` §4 item 1 (heal clamp), §4 item 7
- `docs/01-game-design/GAME_RULES.md` §17 (fixed resolution order)
- `docs/02-technical/GAME_STATE.md` §5.1 (single write-back)
- `docs/00-overview/MVP_SCOPE.md` §1
- `AGENTS.md` §4, §10, §14, §15, §17, §20, §21
- `tasks/completed/TASK-321-server-authoritative-combat-trace-and-cascade-attribution.md`

---

## Scope

### In Scope

- One presence-rule correction in `SIGNALR_PROTOCOL.md` §3.3.1 and version
  bookkeeping for it.
- **Deferred to the operator (OD-322-1, unanswered at closure):** the
  Domain-type widening that would make P-3 representable, and any related
  producer change. Not implemented here — see `## Remaining Decisions`.
- Regression tests pinning the omission rule as it now reads.

### Out of Scope

- Any change to combat values, formulas, resolution order, RNG draws, events,
  Battle State, Redis, or PostgreSQL.
- A new wire member, event discriminator, hub method, argument, or
  subscription; `CardCast`/`PetSkillCast` trace delivery.
- Commits, staging, pushes, rebases, amendments, or history repair; resolving
  TASK-316 or TASK-320.
- Any item listed as OUT in `MVP_SCOPE.md` §2.

---

## Declared File Set (P-2 / P-5)

Exact declared implementation files for TASK-322:
```text
docs/02-technical/SIGNALR_PROTOCOL.md
tests/backend/GameServer.Application.Tests/CombatTraceResolutionTests.cs
tests/backend/GameServer.Api.Tests/CombatTraceWireTests.cs
tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
```

*(The task manifest file itself is excluded from the declared implementation
file set — `tasks/TASK_TEMPLATE.md`. No test file is modified to make an
unrelated implementation pass; only the three listed suites gain new cases.)*

---

## Acceptance Criteria

- [ ] 1. `SIGNALR_PROTOCOL.md` §3.3 contains exactly one presence rule: §3.3.1's
      `Presence` column, §3.3.2 item 7 (the closed list, now including
      `healing[].cycleOrdinal`), and §3.3.2 item 10 no longer disagree about
      `healing[].cycleOrdinal` or any other member, and no other member's
      optionality decision changed.
- [ ] 2. No schema, member, optionality decision for any other member, event, or
      delivery path changes; the §3.3.1 example JSON is unchanged; §8 item 10's
      deployment/drain policy is unchanged in substance (a clause records that
      the reconciliation requires neither).
- [ ] 3. A regression test proves the Domain producer of every heal a Round
      applies always produces an applicable `cycleOrdinal`, so the corrected
      rule's mandatory case still holds for real gameplay.
- [ ] 4. A regression test proves `damage[].cycleOrdinal` is absent from the
      serialized wire for a `statusTick` instance and present for an `attack`
      instance, with no `null` written anywhere.
- [ ] 5. A regression test proves the delivered `ReceiveEvents` envelope for a
      committed Swap still carries a trace whose not-applicable members are
      omitted and whose applicable members are intact.
- [ ] 6. Every pre-existing test in all four backend projects and the whole
      frontend suite passes unchanged; both builds pass.
- [ ] 7. No authoritative rule or contract is violated (`AGENTS.md` §4, §10).

---

## Affected Files & Areas

```text
[ ] src/backend/
[ ] src/frontend/client/
[x] tests/ (unit / integration)
[x] docs/ (SIGNALR_PROTOCOL.md only)
```

---

## Implementation Notes

- The correction must stay inside §3.3.1's presentation of an existing rule.
  It may not restate a rule that §3.3.2 owns, and it may not invent a new
  optional member.
- The regression tests must fail if the rule is reverted: the wire test asserts
  absence of the serialized member, so a producer or DTO that wrote `0` or
  `null` instead of omitting it fails.

---

## Testing Requirements

### Required Verification
```text
[x] Unit tests         — serialized-wire omission and presence of cycleOrdinal
[x] Integration tests  — delivered ReceiveEvents envelope for a committed Swap
[x] Gameplay scenarios — the Round's own heal producers always name a cycle
```

### Key Edge Cases
- A real `0` must stay written and must never be confused with an omission
  (`SIGNALR_PROTOCOL.md` §3.3.2 item 7).
- A `statusTick` instance must not be attributed to a cycle.
- A heal that restores `0` at full HP is still applicable — it names its cycle.

---

## Stop Conditions

- If the resolution would change a combat rule, resolution order, or RNG draw:
  STOP (`AGENTS.md` §7).
- If it would require a new wire member or change an optionality decision:
  STOP — that is a protocol extension, not a reconciliation (§18).
- If a code change would become client-authoritative gameplay logic: STOP
  (`AGENTS.md` §10).

---

## Baseline Evidence

Captured before the first edit, in the repository as found:

```text
dotnet test src/backend/GameServer.sln
  Domain          1700 passed, 0 failed, 0 skipped
  Application      800 passed, 0 failed, 0 skipped
  Api              405 passed, 0 failed, 0 skipped
  Infrastructure   507 passed, 0 failed, 0 skipped
dotnet build src/backend/GameServer.sln
  0 Error(s)
npm --prefix src/frontend/client run test:run
  24 files, 1304 tests passed
npm --prefix src/frontend/client run build
  built in 2.78s
```

---

## Remaining Decisions

**OD-322-1 (OPEN — product/architecture decision required).** The rule is now
stated, but it is not *round-trippable* on the transport today, and this task
deliberately does not change the transport: `CombatTraceHealWireDto.CycleOrdinal`
and the two snapshot DTOs keep their non-nullable members, so nothing on the
Round's delivery path can produce the `healing[].cycleOrdinal` omission, and a
client that once read a `null` could not receive that shape at all. Closing this
needs an operator decision between:

```text
A. Widen the transport member (int -> int?) and the Domain CombatTraceHeal
   member to match, then let a heal that belongs to no actor cycle be omitted —
   the rule becomes reachable and round-trippable. Touches CombatTrace.cs,
   CombatTraceWireProjection.cs, BattleStateService.cs.
B. Narrow the rule back: `healing[].cycleOrdinal` is mandatory, §3.3.1's heal
   cell returns to `always`, and §3.3.2 item 7's list drops it. The original
   contradiction returns in the opposite direction unless the omission sentence
   is deleted rather than the member dropped.
C. Leave the contract as reconciled here and have the transport add a
   follow-up task to widen the member when a producer first needs it.
```

Recommendation: **A**, as one small follow-up task, because the Domain type
currently makes a documented state unrepresentable — but it is a wire/Domain
change and therefore outside this reconciliation's authorized scope
(`AGENTS.md` §17–§18). No guess is implemented in the meantime.

**Verification-coverage caveat (disclosed).** Acceptance criterion 5 cannot be
exercised end to end for `damage[].cycleOrdinal`: no provisioned MVP content
applies a step-19a damage-over-time effect, so no `ReceiveEvents` delivery
contains a status tick (the `Burn` Card's DoT ticks on the Boss, but a
`CardCast` delivery carries no `combatTrace`). It is covered at the
transport-serialization boundary instead, and the delivered-envelope shape is
already covered by the pre-existing TASK-321 integration assertions, which this
task leaves untouched and passing.

---

## Completion Evidence

### Commit
- **No commit exists and none is reported.** Git operations of every kind are
  forbidden by the operative instruction for this session, so this task's
  Phase A/Phase B stages of `TASK_LIFECYCLE.md` §6 are **not executed**, and no
  branch, HEAD, or staging state is claimed anywhere in this record.

### Changed Files

```text
docs/02-technical/SIGNALR_PROTOCOL.md
  Version 2.31 -> 2.32 with a TASK-322 changelog clause (§3.3.1 reconciled,
  no schema/member/delivery change).
  §3.3.1 Heal object: `cycleOrdinal` Presence `always` -> "omitted when the
  heal belongs to no actor cycle", meaning cell cites §3.2.5 / §3.3.2 item 7.
  §3.3.1: new "**Presence rule.**" paragraph — a not-applicable member is
  absent from the JSON and never `null`, every other member is mandatory
  including a real `0`, an unreceived member renders `?` (§3.3.2 item 7 only).
  §3.3.1: restored the blank line before the `move` table that an earlier edit
  removed (formatting only).
  §3.3.2 item 7: the closed list gains its fourth not-applicable omission
  (`cycleOrdinal` on a heal that belongs to no actor cycle) and states it is
  "stated member by member in §3.3.1 and omitted from the JSON rather than
  written as `null`". This is the deliberate item-7 edit described above.
  §8 item 10: one clause recording that TASK-322 changed no wire behaviour, so
  it requires no drain, no coordinated deploy, and no wire version change.
  Unchanged: §3.3 example JSON, every snapshot row, every other heal row, every
  other member's presence, §3.2.*, §4, §5, §6, §7.

tests/backend/GameServer.Application.Tests/CombatTraceResolutionTests.cs
  + CombatTrace_EveryHealAndAttackCycleOrdinal_ShouldNameAnExistingCycle —
  drives up to 40 committed Swap rounds through the real BattleStateService
  and requires every heal and every attack-phase damage entry to name a cycle
  present in `trace.Cycles`, with explicit non-vacuity guards (the sample must
  contain a Boss cycle, a damage instance, and a heal).

tests/backend/GameServer.Api.Tests/CombatTraceWireTests.cs
  + CombatTraceWireDto_DamageCycleOrdinal_ShouldBeOmittedForAStatusTickAndWrittenForAnAttack
    — both directions on one serialized trace: the attack instance writes
    `"cycleOrdinal": 1`, the status tick omits it, no `null` anywhere, and the
    deserialize/re-serialize round trip keeps both.
  + CombatTraceWireDto_HealCycleOrdinal_ShouldBeOmittedWhenTheHealBelongsToNoCycle
    — the documented-payload half asserts the rule for the uncycled heal; the
    transport half pins the rule's *current reach*: the non-nullable heal DTO
    cannot represent the omission and writes a forbidden `0`, which is the
    tripwire that goes red when OD-322-1 widens the member.
  + CombatTraceWireDto_ShouldKeepTheDocumentedMemberSetsWhenNotApplicableMembersAreOmitted
    — built from the transport DTOs (not a JSON literal): root 8 members, card
    cycle 6 members with the board cells omitted, status-tick omission, both
    snapshot 5-member sets with a real `0`, an empty-but-present `healing`, and
    no `null` token.
```

No production file changed. `ApiIntegrationTests.cs` was **not** edited.

### Validation Results

Executed in this session and observed:

```text
dotnet build src/backend/GameServer.sln ............ 0 Error(s), 4 pre-existing warnings
dotnet test  src/backend/GameServer.sln ............ Domain 1700, Application 801,
                                                     Api 408, Infrastructure 507
                                                     = 3416 passed, 0 failed, 0 skipped
  (Application 800 -> 801 for the one new case; Api 405 -> 408 for the three new cases)
dotnet test  --filter ~CombatTrace ................. Application 11 passed; Api 7 passed
npm --prefix src/frontend/client run test:run ...... 24 files, 1304 tests passed
npm --prefix src/frontend/client run build ......... built (0 TypeScript errors)
```

**Independent verification (separate agent, own execution, no Git, no writes in
the repository):** confirmed the contradiction is resolved as one rule; confirmed
every production file is byte-identical to its pre-change hash; re-ran all three
backend projects and the frontend suite and build with the same counts; and
demonstrated fail-before in an OS-temporary copy — reverting the damage DTO to a
non-nullable member turned 2 tests red, and widening the heal DTO turned the heal
test red — each restored to green afterwards. Its two mid-flight catches (the
item-7 edit needed declaring, and a wrong `§3.3.2 items 7–8` cross-reference) were
both corrected before this record.

### Acceptance Criteria Outcome

| AC | Verdict | Basis |
| --- | --- | --- |
| 1 | PASS | One presence rule; §3.3.1, §3.3.2 item 7 and item 10 agree; no member both mandatory and omittable. |
| 2 | PASS | Only the doc + 3 test files changed; §3.3 example, snapshot rows and every other presence unchanged; §8 item 10 policy unchanged in substance. |
| 3 | PASS | The new Application case pins the mandatory case on the real Round producers, non-vacuously. |
| 4 | PASS | Both wire omissions asserted, and fail-before demonstrated for both directions by the verifier. |
| 5 | **PARTIAL** | Delivered-envelope coverage is not extended by design: no MVP content produces a delivered status tick. Disclosed in `## Remaining Decisions`. |
| 6 | PASS | 3416 backend and 1304 frontend tests pass; both builds pass. |
| 7 | PASS | No combat value, order, RNG, event, state, wire member, hub method, or subscription changed. |

### Server Authority & Scope Verification
- [x] No client-authoritative gameplay logic added; the frontend is untouched.
- [x] No combat rule, DEF formula, damage order, RNG draw, or balance value changed.
- [x] MVP scope unchanged (`MVP_SCOPE.md` §1); no new system or content.
- [x] No Git operation performed; no file staged, committed, or pushed.
