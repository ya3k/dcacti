# TASK-310 — Reconcile TASK-309 review findings in authoritative documentation and close the thunder-strike non-qualifying-round gap

<!--
  GEN-TASK EXECUTION MANIFEST — STATUS: DONE

  Follow-up defect task opened from the independent TASK-309 review. The review
  found TASK-309's implementation, migrations, tests, scope, security and
  determinism correct, but three authoritative documents still state that ADR-029
  §3's U-2 (per-ability Boss Card PowerCost) is unresolved, and it found one
  verification-completeness gap in the TASK-309 V-2 evidence. TASK-309 is a
  completed, immutable record; this task closes the findings.
-->

---

## Metadata

```text
Task ID:           TASK-310
Type:              BUG
Status:            DONERisk:              LOW
Priority:          HIGH
Primary Agent:     orchestrator
Supporting Agents: gameplay, testing, review
Workflow:          development/bug-fix.md
Skills:            quality/documentation-consistency,
                   quality/implementation-review,
                   gameplay/gameplay-behavior-derivation,
                   testing/test-scenario-generation
Dependencies:      TASK-309 (DONE — immutable record; its review findings are
                   this task's input), ADR-029 (Accepted, amended by TASK-309)
Declared Files:    see §5
```

**Lifecycle path:**

```text
BACKLOG -> READY -> IN PROGRESS   (defect findings recorded in §1)
IN PROGRESS -> IN REVIEW          (documentation + test complete)
IN REVIEW -> DONE                 (two-stage commit protocol, TASK_LIFECYCLE.md §6)
```

---

## 1. Defect Findings (TASK-309 independent review)

The TASK-309 independent review returned **NOT READY** on documentation
consistency only, with the implementation/migrations/tests/scope/security/
determinism checks passing. The findings are reproduced here as the bug
statement; no approved value is changed by this task.

### Blocking — documentation bugs (stale statements)

Three authoritative documents still state that ADR-029 §3 **U-2**
(per-ability Boss Card `PowerCost` and cost re-tuning) is unresolved. TASK-309
resolved U-2 for all five abilities in `ADR-029` §3, `BOSS_RULES.md` §4.1 item 7
and §6.3; these three docs were outside TASK-309's declared file set and were
left stale (`AGENTS.md` §17/§23):

1. `docs/01-game-design/CARD_RULES.md` §3 item 9 — "Every Boss Card's
   `PowerCost` and every cost re-tuning are **UNRESOLVED** balance values".
2. `docs/01-game-design/COMBAT_RULES.md` §2 POWER bullet — "Whether ... alongside
   per-ability costs (U-2) remains an unresolved balance decision".
3. `docs/02-technical/GAME_STATE.md` §2.4.3 item 8 — U-2 "remains an unresolved
   balance decision".

### Non-blocking findings closed here

4. `docs/01-game-design/BOSS_RULES.md` §4.4 item 3 still lists `Earthquake`
   first in the Slice 4 migration order. `earthquake` was migrated by Slice 3
   (TASK-308); ADR-029 §4 and TASK-309's migration order are
   `flame-burst` → `root` → `drain-power` → `thunder-strike`.
5. `docs/02-technical/GAME_EVENTS.md` §2 `PassiveCharged` item 4 still uses the
   pre-ADR-029 step labels ("Boss Passive fires at Step 18a ... before Boss Skill
   (Step 18b) and Boss Attack (Step 18c)"). `GAME_RULES.md` §17 numbers the Boss
   phase 18a selection/board, 18b Passive, 18c card/skill, 18d attack.
6. The TASK-309 completion record claims `thunder-strike` "survives
   non-qualifying rounds and is consumed exactly once"; the migrated-card test
   covers cast → next qualifying attack but no migrated-card no-ATK-round
   survival test existed. This task adds that test so the claim is directly
   evidenced.
7. `src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs` uses
   `(CardEffectType)7` as its example of an undefined identity; 7 is now
   `PowerDrain`. A comment-only correction uses an actually undefined value.

### Explicitly not changed

- TASK-309's completed record is immutable (`TASK_LIFECYCLE.md` §3). Its §10
  checkboxes remain as filed; this task records the closure instead of editing
  the completed record.
- The in-battle artifact's exact counts remain unasserted by design (the
  analysis test asserts contractual invariants only); the review reproduced
  them and they are unchanged.
- V-3's disposable cluster was deleted after the recorded run; that evidence
  remains prose in the TASK-309 migration test.
- The pre-existing `BossDefinitionPostgresProvisioningTests` method name and the
  other pre-existing documentation drift reported in `.ai/PROJECT_STATE.md` §6
  are not renamed/reconciled here.

---

## 2. Readiness Basis

```text
[ x ] Task type confirmed: BUG (development/bug-fix.md §1: documentation bugs —
        stale docs — plus a verification-completeness gap; no gameplay/code rule
        changes)
[ x ] Relevant documentation exists in docs/ (the three stale owners and the
        amended ADR-029/BOSS_RULES that falsify them)
[ x ] MVP scope confirmed: documentation reconciliation only
[ x ] Not blocked: TASK-309 DONE; no dependency on an unresolved decision — the
        approved U-2 values already exist in ADR-029/BOSS_RULES
[ x ] Primary agent assigned and workflow assigned
[ x ] Acceptance criteria are testable (§7)
[ x ] Preflight: branch master, HEAD 857aa985a67caabb2f7aa85e973482e79237e970,
        working tree clean, completed/ = 311 records (through TASK-309),
        backlog/active/blocked empty (only .gitkeep), highest existing task
        ID = 309 -> TASK-310 allocated (tasks/README.md §3)
[ x ] Duplicate search: no TASK-310 record, artifact or partial work exists
```

---

## 3. Objective

Remove the TASK-309 review's blocking contradiction by reconciling the three
authoritative documents that still call U-2 unresolved, correct the two stale
documentation statements tied to the Boss phase numbering/order, and add the
missing migrated-card `thunder-strike` non-qualifying-round lifecycle test so
the TASK-309 completion record's V-2 claim is directly evidenced.

---

## 4. Authoritative References

- `docs/03-decisions/ADR/ADR-029-boss-as-player-opponent-boss-power-and-action-policy.md` §3 (U-2/U-3/U-4/U-5 resolved), §4 (Slice 4)
- `docs/01-game-design/BOSS_RULES.md` §4.1 item 7, §4.4, §6.3, §6.3.1
- `docs/01-game-design/CARD_RULES.md` §1.1, §3 item 9
- `docs/01-game-design/COMBAT_RULES.md` §2
- `docs/02-technical/GAME_STATE.md` §2.4.3
- `docs/02-technical/GAME_EVENTS.md` §2
- `docs/01-game-design/GAME_RULES.md` §17 step 18
- `tasks/completed/TASK-309-adr-029-slice-4-remaining-boss-card-abilities.md`
- `tasks/TASK_LIFECYCLE.md` §3 (completed records are immutable)

---

## 5. Declared File Set (P-2 / P-5)

```text
docs/01-game-design/CARD_RULES.md
docs/01-game-design/COMBAT_RULES.md
docs/01-game-design/BOSS_RULES.md
docs/02-technical/GAME_STATE.md
docs/02-technical/GAME_EVENTS.md
src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs   (comment only)
tests/backend/GameServer.Application.Tests/TASK309BossCardEffectRuntimeTests.cs
.ai/PROJECT_STATE.md
```

*(The TASK-310 record itself is excluded from the Phase A set.)*

---

## 6. Scope

### In Scope

- W1: update the three U-2 statements to record resolution (TASK-309 values).
- W2: correct `BOSS_RULES.md` §4.4 item 3's Slice 4 migration order (Earthquake
  migrated by Slice 3; TASK-309 order recorded).
- W3: correct `GAME_EVENTS.md` §2's Boss-phase step labels to GAME_RULES §17.
- W4: add the migrated-card `thunder-strike` non-qualifying-round survival test
  in `TASK309BossCardEffectRuntimeTests.cs`.
- W5: comment-only stale-example correction in `CardEffectDefinition.cs`.
- W6: refresh `.ai/PROJECT_STATE.md` for the pre-commit state (HEAD 857aa98,
  TASK-310 in flight, TASK-309 DONE at 4d97c74/857aa98, review findings closed).

### Out of Scope

- Any change to TASK-309's approved values, code behavior, migrations or tests
  other than W4's additional coverage.
- Editing the completed TASK-309 record (immutable).
- The pre-existing test-method name and the pre-existing documentation drift
  listed in PROJECT_STATE §6.
- Pushing.

---

## 7. Acceptance Criteria

- [x] `CARD_RULES.md` §3 item 9 no longer calls U-2 unresolved; it records the
      five approved Boss Card costs and points at ADR-029 §3/BOSS_RULES.
- [x] `COMBAT_RULES.md` §2 POWER bullet no longer calls U-2 unresolved.
- [x] `GAME_STATE.md` §2.4.3 item 8 no longer calls U-2 unresolved.
- [x] `BOSS_RULES.md` §4.4 item 3 records the TASK-309 migration order without
      listing Earthquake in Slice 4.
- [x] `GAME_EVENTS.md` §2 Boss Passive step labels match GAME_RULES §17
      (18a board/selection, 18b Passive, 18c card/skill, 18d attack).
- [x] The new `thunder-strike` test proves, through the real pipeline, that the
      modifier survives a no-qualifying-ATK Boss round and is consumed exactly
      once by the next qualifying Boss attack.
- [x] `CardEffectDefinition.cs`'s undefined-enum example is not a now-valid
      member.
- [x] No approved value or behavior outside this scope changes.
- [x] Tests pass at the required depth; no new failure.
- [x] `git diff --check` clean; two-stage commit with the exact declared set.

---

## 8. Affected Files & Areas

```text
[x] docs/ (BOSS_RULES, CARD_RULES, COMBAT_RULES, GAME_STATE, GAME_EVENTS)
[ ] src/backend/ (comment-only CardEffectDefinition.cs)
[x] tests/backend/GameServer.Application.Tests/ (one new lifecycle test)
[x] .ai/PROJECT_STATE.md (navigation cache refresh)
[ ] src/frontend/client/ (no change)
```

---

## 9. Implementation Notes

- Docs edits are the smallest corrections that remove the contradiction; no
  new values are authored (the five costs already exist in ADR-029 §3/
  BOSS_RULES §6.3).
- The new test uses the existing `BossCardTestHarness`/`BossRoundFixtures`
  production-derived fixtures: a fixture with no Player ATK match and no Boss
  qualifying ATK match, then the existing attack fixture; the cast round is
  entered at Boss Power 100/cooldown 0, later rounds at Power 0 so the
  conditional cast is unaffordable and the board action resolves.
- `PROJECT_STATE` records the pre-commit HEAD and the two TASK-309 commits; it
  invents no future SHA.

---

## 10. Testing Requirements

```text
[x] `dotnet test ... --filter TASK309` focused suites (Domain/Application/Api);
    the new test is the 11th in TASK309BossCardEffectRuntimeTests
[x] Focused Application depth for this LOW-risk BUG (core/validation.md §2);
    TASK-309's full-suite baseline remains the last full run
[x] `dotnet build src/backend/GameServer.sln` — 0 errors
[x] `git diff --check` — exit 0
```

---

## 11. Stop Conditions

- If any authoritative document still needs the three documents' original
  wording for a reason unknown here: STOP and report instead of editing.
- If the new test cannot be constructed from production-derived fixtures: STOP
  and report; do not weaken the claim by hand-authoring a gameplay value.

---

## 12. Completion Evidence

### Commit

- Implementation slice (Phase A): `211272380f3734618026590598bef5316d83500c`
- Subject: `docs: reconcile the resolved boss card costs and lifecycle evidence`
- Owns: `TASK-310`
- Files: the exact 8-file set in §5 (staged-set equality verified:
  `staged=8 expected=8`, task record excluded)
- Shared with: `none`
- Unowned / pre-existing: `none`

### Changed Files

- `docs/01-game-design/CARD_RULES.md` — §3 item 9 records the five approved
  Boss Card Power costs and U-2's resolution (TASK-309 / ADR-029 §3).
- `docs/01-game-design/COMBAT_RULES.md` — §2 POWER bullet records U-2 resolved.
- `docs/02-technical/GAME_STATE.md` — §2.4.3 item 8 records U-2 resolved.
- `docs/01-game-design/BOSS_RULES.md` — §4.4 item 3 records the TASK-309 order
  (`flame-burst` → `root` → `drain-power` → `thunder-strike`) and notes
  `earthquake` was migrated by Slice 3.
- `docs/02-technical/GAME_EVENTS.md` — §2 Boss Passive timing corrected to
  GAME_RULES §17's 18b Passive / 18c cast / 18d attack numbering.
- `src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs` — comment-only
  undefined-enum example corrected from the now-valid `(CardEffectType)7` to
  `(CardEffectType)99`.
- `tests/backend/GameServer.Application.Tests/TASK309BossCardEffectRuntimeTests.cs`
  — new migrated-card `thunder-strike` no-qualifying-Boss-round survival test
  plus its production-derived fixture.
- `.ai/PROJECT_STATE.md` — navigation-cache refresh (HEAD `857aa98`, TASK-309
  DONE, TASK-310 in flight, review findings recorded).

### Validation Results

```text
[x] dotnet build src/backend/GameServer.sln --nologo --verbosity q
      0 Error(s); pre-existing warnings only
[x] Focused Application tests
      TASK309BossCardEffectRuntimeTests  11/11 passed (the new test is the 11th)
[x] git diff --check                  exit 0
[x] Documentation consistency verification
      - the three stale U-2-as-unresolved statements are gone and record the
        approved values;
      - BOSS_RULES §4.4 item 3 lists only the TASK-309 Slice 4 order;
      - GAME_EVENTS §2 matches GAME_RULES §17's 18b/18c/18d numbering;
      - the stale enum example is no longer a now-valid member.
```

Validation depth: LOW-risk BUG — build + focused tests + review per
`core/validation.md` §2. TASK-309's full-suite baseline remains the last full
run; no production behavior changed (one comment, documentation, one added
test).

### Server Authority & Scope Verification

- [x] No gameplay value, code behavior, migration or contract changed; the only
      source-file edit is a comment.
- [x] The added test drives the real `BattleStateService` / `CardCastExecutor` /
      `AttackResolution` pipeline with production-derived fixtures; it recomputes
      no gameplay value.
- [x] TASK-309's completed record was not edited (immutable).
- [x] No client file changed; no push; the Phase A commit uses the exact
      declared set.
