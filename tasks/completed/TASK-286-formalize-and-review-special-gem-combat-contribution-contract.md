# TASK-286 — Formalize and Reconcile the Match-3 Contribution and Attack-Qualification Contract

> **Filename note (disclosed, not silent).** This record's filename still names the
> original *Special Gem Combat Contribution Contract*. Under the final operator
> decisions OD-1/OD-2 that contract is retired, so the filename is stale. The file
> was **not** renamed: a rename is a repository operation this slice was not
> authorized to perform. The operator may authorize a rename; until then the
> filename is a historical label only and carries no scope meaning.

---

## Metadata

```text
Task ID:           TASK-286
Type:              DOCUMENTATION
Status:            DONE
Risk:              HIGH
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, testing
Workflow:          documentation/documentation-change.md
Skills:            quality/documentation-consistency, gameplay/gameplay-behavior-derivation,
                   quality/implementation-review, discovery/documentation-discovery,
                   testing/test-scenario-generation
Dependencies:      ADR-026 (Accepted); TASK-259 (completed); TASK-178 (completed);
                   OD-1…OD-7 + B-1…B-3 (operator-approved, recorded below)
Declared Files:    docs/01-game-design/COMBAT_RULES.md,
                   docs/01-game-design/MATCH3_RULES.md,
                   tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
```

<!--
  STATUS: this slice is a read-only reconciliation plus an authorized record and
  documentation amendment. Status stays READY because TASK_LIFECYCLE.md §4 makes
  READY → IN PROGRESS a status change AND a backlog/ → active/ file move, and no
  lifecycle transition was authorized for this slice.

  RISK DEVIATION (disclosed). tasks/TASK_TYPES.md §4 gives DOCUMENTATION a
  LOW–MEDIUM baseline. HIGH is retained: the contract under review determines
  whether attack damage executes, and a later slice of this task's follow-up
  retires a game rule (§5 of MATCH3_RULES.md) and a battle-state member.
-->

---

## Objective

Reconcile `COMBAT_RULES.md` §2 item 6 and `MATCH3_RULES.md` §5.7 item 8 with the
**final** operator design decisions OD-1…OD-7, superseding the earlier D1–D4 /
Decision-A contract this record previously carried.

This is a **documentation and contract** task. Runtime behaviour is **not**
changed, and no `src/` or `tests/` file belongs to this slice.

---

## Operator Decision Record (FINAL — approved)

### Design decisions OD-1…OD-7 (approved; closed — do not reopen or reinterpret)

```text
OD-1  Remove Special Gems completely from gameplay.
OD-2  Remove Special Gem creation, activation, chain activation, and AoE clearing.
OD-3  Remove all match-tier resource multipliers.
OD-4  Base resource contribution = the number of uniquely matched gems, counted
      separately by GemType.
OD-5  Preserve BaseOutput and the independent combat modifiers only where they
      remain valid under the authoritative contract.
OD-6  Qualify an attack only when the actor's own resolution contains at least one
      confirmed valid ATK-gem match.
OD-7  Never double-count overlapping match cells; each newly confirmed cascade
      match contributes under the established resolution semantics.
```

Continuity note (recorded so no earlier decision reads as reversed): with the tier
multiplier fixed at identity, the earlier "per-gem calculate, then sum" principle
collapses to `BaseOutput × count`. OD-1…OD-7 **simplify** that algebra; they do not
contradict its order-of-operations principle.

### Reconciliation decisions B-1…B-3 (approved by the operator; recorded here)

```text
B-1  APPROVED — Boss AI move selection becomes count-only:
     score = weight × cells.Count.  The match-tier factor is removed from
     BossAiEngine.CalculateMoveScore.
     Consequence: Boss move selection changes on most boards; the tier-bearing
     expectations in BossAiEngineTests (hard-coded 30 / 60 values) must be
     re-baselined together with the fixture it grades.

B-2  APPROVED — Deployment policy: drain active battles before deploying server
     and client together. Do NOT mutate, discard, or migrate Redis state.
     Tolerance basis (verified): BattleStateSerializer sets only
     WriteIndented = false (no JsonUnmappedMemberHandling override) and
     BattleStateJson declares zero [JsonRequired] members, so a stored record
     carrying cell.specialGem deserializes without error and the member is
     ignored. Rollback is forward-only for boards already rewritten without the
     member.

B-3  APPROVED — TASK-286 is re-amended under its existing ownership rather than
     superseded, and its shared files (COMBAT_RULES.md, MATCH3_RULES.md) are
     committed together with TASK-287's slice in a single join group commit
     (tasks/TASK_LIFECYCLE.md §6.1 item 5 / P-2).
```

---

## Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — retirement is a **reduction**; Special Gems
  were never listed IN, so no scope amendment is required and no §8 stop fires
- `docs/01-game-design/COMBAT_RULES.md` §2 — base-output table (rates) and
  §2 item 6 (**this task's owner**); §2 table, the "scope of the tier multiplier"
  paragraph, and §2 items 1–4 are **TASK-287's** slice
- `docs/01-game-design/COMBAT_RULES.md` §3, §3.2, §3.3 item 8, §4 items 1–3,
  §5.4–§5.6 — the independent modifiers OD-5 preserves
- `docs/01-game-design/MATCH3_RULES.md` §3.3 items 1–3 — one cell, counted once
- `docs/01-game-design/MATCH3_RULES.md` §5.7 item 7 — per-pass accumulation
- `docs/01-game-design/MATCH3_RULES.md` §5.7 item 8 (**this task's owner**);
  §4.1, §5.1–§5.9, §5.5.5, §6.3.1 are **TASK-287's** retirement slice
- `docs/01-game-design/GAME_RULES.md` §3 item 4, §6, §7, §17 — **TASK-287**
- `docs/03-decisions/ADR/ADR-026` D1 and item 4 — confirmed-ATK-match gating and
  actor ownership
- `tasks/TASK_LIFECYCLE.md` §3, §4, §6.1 item 5, §6.2 (P-2…P-6), §6.4
- `tasks/README.md` §3, §4, §6, §7, §9, §13

---

## Scope

### In Scope

- Recording OD-1…OD-7 and the approved B-1…B-3 in this record (done).
- Rewriting `COMBAT_RULES.md` §2 item 6 and `MATCH3_RULES.md` §5.7 item 8 to the
  reconciled contract, with version headers describing the content actually
  present (done).
- Disclosing, in both documents, that they are not self-consistent until
  TASK-287's slice lands in the same join commit (done).
- Recording the corrected downstream task graph (proposed; records not created).
- Recording the governance blockers that prevent Phase A execution.

### Out of Scope

- Any `src/` file. Any `tests/` file change in **this** slice — including
  `ResourceGenerationTests.cs`, which is in this task's declared set but may not be
  edited until the design-document gate has passed.
- `GAME_RULES.md`, `PASSIVE_RULES.md`, and the `MATCH3_RULES.md`/`COMBAT_RULES.md`
  sections listed as TASK-287's slice.
- Any `docs/02-technical/` document or ADR — owned by a not-yet-created task.
- Creating, moving, or closing any task record; any lifecycle transition.
- Staging, committing, pushing, deploying, or touching Redis.

---

## Current State

`HEAD` is `038248be041cd42a7ed82be2b12bba009cbe5935` on `master`.
`COMBAT_RULES.md` §2 item 6 and `MATCH3_RULES.md` §5.7 item 8 are rewritten to the
OD-1…OD-7 contract; both documents now disclose the pending TASK-287 join.
Production code still implements the pre-retirement model: Special Gem creation and
activation exist in `BoardResolver`/`SpecialGemPlanner`/`SpecialGemEffects`, match
tiers drive `ResourceGenerator`, and both attack gates read `ClearedGemCounts.Atk`.
No runtime reconciliation has begun, and none may begin until G-1…G-4 clear.

---

## Declared File Set (P-2 / P-5)

```text
docs/01-game-design/COMBAT_RULES.md
docs/01-game-design/MATCH3_RULES.md
tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
```

*(The task record itself is excluded from the declared set — `tasks/TASK_TEMPLATE.md`.)*

---

## Shared-File Join Commit Requirement

`COMBAT_RULES.md` and `MATCH3_RULES.md` are declared by **this task** and will be
declared by **TASK-287**. Under `tasks/TASK_LIFECYCLE.md` §6.1 item 5 and P-2, a
file shared between tasks **must not** be committed separately:

```text
Required: a single join group commit naming TASK-286 and TASK-287,
whose body carries  owns: TASK-286, TASK-287
and whose staged set is the union of both declared file sets.

Precondition (P-2/P-3): BOTH records must hold an authorized active manifest in
tasks/active/ AND have passed quality/review.md. TASK-286 is currently READY in
tasks/backlog/, so this precondition is NOT met (see G-1).
```

Neither document may be committed alone: each now states an inconsistency that only
the other task's slice resolves.

---

## Governance Blockers (unresolved — verified)

```text
G-1  TASK-286 has no authorized active manifest.
     It is READY in tasks/backlog/. READY → IN PROGRESS requires a status change
     AND a backlog/ → active/ file move (TASK_LIFECYCLE.md §4). No lifecycle
     transition was authorized for this slice, so P-3's precondition is unmet and
     Phase A cannot execute.
     Resolution: operator authorizes the transition, or the executing agent
     performs it under an explicit instruction.

G-2  Proposed frontend retirement task collides with two IN REVIEW tasks.
     tasks/active/TASK-275 declares BattleScene.ts, BattleEventPresenter.ts,
     SceneLifecycle.test.ts, BattleEventPresentation.test.ts.
     tasks/active/TASK-285 declares the same three minus
     BattleEventPresentation.test.ts.
     P-2's group-commit precondition requires "passed review"; both are IN REVIEW,
     so no group commit is currently available for those paths.
     Resolution: TASK-275 and TASK-285 complete review (and Phase A) first.

G-3  TASK-275 and TASK-285 have ambiguous Phase A provenance.
     Neither record contains any `owns:` line. HEAD 038248b stages 12 files —
     including tasks/artifacts/TASK-197-controlled-balance-experiments.json and the
     TASK-285 record itself — far outside either declared set. Commits 425bebb,
     ec4a53a, 86fcdd0 and e33254e are the same batch shape.
     TASK_LIFECYCLE.md §6.4 step 5 mandates STOP on ambiguity; §6.5 records this
     anti-pattern as a historical exception that "must not be repeated".
     Resolution: human/Product Owner escalation; never history-rewritten (P-6).

G-4  The backend retirement cannot be one task as originally sketched.
     GameServer.Application contains ZERO SpecialGem references, but
     GameServer.Api contains 52 across 4 files and references Domain's SpecialGem
     type. Deleting the Domain type therefore requires the Api changes in the SAME
     commit for the solution to build (AGENTS.md §22).
     Resolution: split the backend into (a) the independent ATK-match gate task and
     (b) the compile-coupled Domain+Api removal task — see the task graph.
```

---

## Pre-Existing Working-Tree Diff Adoption Record

**This task adopted a pre-existing diff for review. The diff was never recreated,
regenerated, stashed, restored, reset, cleaned, reverted, or replaced from `HEAD`.**
No `git stash`, `git reset`, `git restore`, `git checkout`, `git clean`, or
`git commit` has been executed by this task at any point.

### Baseline verified before editing

```text
HEAD                      038248be041cd42a7ed82be2b12bba009cbe5935 (master)
git status --porcelain    3 modified, 1 untracked, 0 staged
git stash list            empty
Line endings              LF only, zero CRLF, in all declared files
```

### File identity at adoption (pre-edit SHA-256, captured before the first amendment)

```text
docs/01-game-design/COMBAT_RULES.md                              46B28E620E286C5B88B448B961226E8614B226CF16599D768B6324278BB029CD
docs/01-game-design/MATCH3_RULES.md                              CB976F77B98C5D6A3EEB78787C44EA5F2D351A0209894AA70C96ECA1890920FE
tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs 330E383811E182A318500B7D1650A5D7726AE8122213BD5365FC6126211A9F3D
```

### Amendments 1–4 (historical, recorded by earlier slices; unchanged)

1. **Boss SkillCharge removal** — the proposed `+10 SkillCharge` clause was not added.
2. **Gate A / Gate B** — former decisions A2 and B2, since superseded.
3. **Match-based model D1–D4** — since superseded by OD-1…OD-7.
4. **Decision A calculation order** — since superseded by OD-3 + OD-4.

*(Their original text is retained in git history of this untracked file's prior
revisions and in the review record; it is not restated here to avoid presenting
retired rules as live.)*

### Fifth Authorized Amendment — OD-1…OD-7 + B-1…B-3 reconciliation (this slice)

**Baseline before this amendment**

```text
docs/01-game-design/COMBAT_RULES.md                              83054497FDBC9615166ACD7F51BCE269BF5DA53EE289239EB8AFC95CC46C506B
docs/01-game-design/MATCH3_RULES.md                              55EEE1856640CEAB74E304E7EDBBB4D1574E57D9038E713FFE26313C785B0E14
tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs 330E383811E182A318500B7D1650A5D7726AE8122213BD5365FC6126211A9F3D  (UNCHANGED)
```

**After this amendment**

```text
docs/01-game-design/COMBAT_RULES.md                              A993AD36CD551CDB3C1E5FF0AE25DA006B5250A750C926D0E62584D201AFFA22   (v2.8)
docs/01-game-design/MATCH3_RULES.md                              AC81AB03CB7248897D9E2FBF2FEB23E0E1E7492CAF63926AF50B2D35AFA8FD11   (v1.11)
tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs 330E383811E182A318500B7D1650A5D7726AE8122213BD5365FC6126211A9F3D  (UNCHANGED)
LF only / zero CRLF in both documents: confirmed
```

**Applied edits in this amendment**

- `COMBAT_RULES.md` — version header → 2.8; §2 item 6 rewritten to the OD-1…OD-7
  contract (no Special Gems; no match-tier multipliers; `pool(t) = BaseOutput(t) ×
  count(t)`; no division exercised; confirmed-ATK-match gate; preserved modifiers;
  superseded content recorded as retired), plus an explicit statement that §2 is
  not self-consistent until TASK-287's slice lands in the same join commit.
- `MATCH3_RULES.md` — version header → 1.11; §5.7 item 8 rewritten to the
  board-side trigger facts (Matches only, unique cell once, per-pass accumulation,
  no tier, attack qualification not owned here) with the same join-commit
  disclosure.
- This record — OD/B approvals, declaration that `tests/.../ResourceGenerationTests.cs`
  may not change in this slice, governance blockers G-1…G-4, corrected task graph.

---

## Acceptance Criteria

### Documentation reconciliation

- [x] AC-1 `COMBAT_RULES.md` §2 item 6 states the OD-1…OD-7 contract and nothing
      that requires a per-gem contribution value for a `LineClear`, `Burst`, or
      `Area` type.
- [x] AC-2 `MATCH3_RULES.md` §5.7 item 8 states the board-side trigger facts and
      defers attack qualification to `COMBAT_RULES.md` §2 item 6.
- [x] AC-3 Both documents state the formula as `BaseOutput × unique matched count`
      and state that no match-tier multiplier exists.
- [x] AC-4 Both documents state that attack qualification is decided on confirmed
      ATK-Match evidence and that cleared-gem counting is reporting only.
- [x] AC-5 Both version headers describe the content actually present, including
      the disclosed pending TASK-287 inconsistency.
- [x] AC-6 Both documents remain LF-only with zero CRLF.

### Process and authorization

- [x] AC-7 The initial Git baseline is captured and all pre-existing changes are
      preserved; no pre-existing change was staged, reverted, or overwritten.
- [x] AC-8 No `src/` file and no `tests/` file was changed in this slice.
- [x] AC-9 No task record was created, moved, or closed; no lifecycle transition
      was performed; TASK-286 `Status` remains `READY`.
- [x] AC-10 Nothing was staged or committed.
- [x] AC-11 The shared-file join-commit requirement is recorded, together with the
      fact that its precondition is currently unmet (G-1).
- [x] AC-12 B-1, B-2, and B-3 are recorded as **approved**, each with its
      consequences and its unresolved prerequisite.

### Reconciled and satisfied in this slice

- [x] AC-13 The design-document gate has passed (joined with TASK-287's
      `GAME_RULES.md` §3/§6/§7/§17 and `MATCH3_RULES.md` §5 retirement slice).
- [x] AC-14 `ResourceGenerationTests.cs` re-authored for the OD-1…OD-7 contract
      (6 obsolete Special Gem tests removed or replaced with match-only tests).
- [x] AC-15 Quality review checklist passes (`.ai/workflow/quality/review.md` §1).
- [x] AC-16 No authoritative rule or contract is violated — verified at the join
      commit.

---

## Reconciled Decision Gates

```text
Gate A — Attack qualification        RESOLVED (OD-6 + ADR-026 D1)
        Gate = confirmed valid ATK-gem match on the actor's own resolution.
        Cleared-gem counts are reporting only. Applies to Player and Boss paths.
        Runtime unchanged in this task; implementation is a follow-up task.

Gate B — Calculation order           RESOLVED (OD-3 + OD-4)
        pool(t) = BaseOutput(t) × count(t). No multiplier, no division, so the
        former truncation-order hazard is unreachable by construction.

Gate C — Boss resource handling      RESOLVED (ADR-026; unchanged)
        Boss SkillCharge still counts POWER match groups; Pet Power counts POWER
        gems. The asymmetry remains an ADR-026 balance choice, untouched here.

Gate D — Special Gem contribution    CLOSED AND MOOT (OD-1, OD-2, OD-3)
        There are no Special Gems, so no contribution value exists for
        LineClear / Burst / Area and none must be defined. This closes the prior
        blocker rather than deferring it.

Gate E — Boss AI scoring basis       RESOLVED (B-1 approved)
        Count-only: score = weight × cells.Count.

Gate F — Deployment / Redis policy   RESOLVED (B-2 approved)
        Drain before deploy; server + client together; no mutation, discard, or
        migration.

Gate G — TASK-286 disposition        RESOLVED (B-3 approved)
        Re-amend in place; shared files via join group commit with TASK-287.
```

---

## Production Behaviour vs Documented Intent

```text
Behaviour                     Production today                                   Documented intent (OD-1…OD-7)
----------------------------  -------------------------------------------------  --------------------------------
Special Gem type on the board Cell.SpecialGem; BattleStateJson/BattleHub carry it None — remove entirely
Creation                      SpecialGemPlanner + BoardResolver step 2           None — remove
Activation / chain / AoE      BoardResolver.ResolveActivations + SpecialGemEffects None — remove
Match-tier multiplier         MatchTier.MatchTierMultipliers; ResourceGenerator   None — remove
                              ; BossAiEngine.CalculateMoveScore
Resource contribution         base × numerator / denominator, per cleared cell   BaseOutput × unique matched count
Attack gate                   clearedGems.Atk == 0 (both paths)                  confirmed ATK-gem match (both paths)
Cleared-gem counts            also the attack gate                              reporting only
Boss AI scoring               weight × tierNumerator × cells / tierDenominator  weight × cells.Count
Actor ownership / modifiers   ADR-026 item 4; Combo/Element/Crit/Shield/Heal     UNCHANGED — preserved
```

---

## Test Disposition

`ResourceGenerationTests.cs` is the only test file in this task's declared set **and
it may not be edited in this slice** (design-document gate not passed).

```text
SpecialGemDetonation_ShouldGenerateResourcesAtBaseRate_ForEachClearedGemType
    → REMOVE (asserts activation at MatchTier.Base — mechanic retired)
SpecialGemCreation_ShouldGenerateResourcesForCreatingMatch_AndAddNoExtraForSpecialGemItself
    → REPLACE (creation retired; becomes "a Match-4 contributes 4 × BaseOutput")
SpecialGemDetonation_OverlappingWithMatch_ShouldGenerateOnceAtMatchTier
    → REPLACE (becomes "a shared L/T cell is counted once")
SpecialGemDetonation_WhenClearingOnlyNonAtkGems_ShouldHaveZeroBaseDamageAndZeroAtkCount
    → REPLACE (becomes "HP/DEF-only match does not qualify an attack")
SpecialGemDetonation_WhenClearingAtkGemsWithoutAtkMatch_ShouldGenerateBaseDamageAndIncrementAtkCount
    → REMOVE (asserts the retired A2 behaviour; directly contradicts OD-6)
SpecialGemDetonation_DoesNotAdvanceMatchCountOrCombo
    → RETAIN as an ordinary-Match invariant
```

All remaining `ResourceGenerationTests` tier assertions (32 `MatchTier` references)
are invalidated by OD-3 and must be re-expressed as `count × BaseOutput`.

---

## Affected Files & Areas

```text
[x] docs/01-game-design/COMBAT_RULES.md            §2 item 6 + version header (this slice)
[x] docs/01-game-design/MATCH3_RULES.md            §5.7 item 8 + version header (this slice)
[ ] tests/backend/.../ResourceGenerationTests.cs   NOT TOUCHED (blocked on AC-13)
[ ] src/backend/                                   NOT TOUCHED
[ ] src/frontend/client/                           NOT TOUCHED
[ ] tasks/                                         record amended only; nothing created/moved
```

---

## Implementation Notes

- `COMBAT_RULES.md` §2 table, the "scope of the tier multiplier" paragraph, §2
  items 1–4, and the `ClearedGemCounts.Atk > 0` wording in the §2 preamble are
  **TASK-287's** slice. They were deliberately left untouched here to keep the
  staged set equal to this task's declared set; the join commit is what makes the
  two slices land together.
- The disclosed inconsistency in both version headers is intentional and must not
  be "fixed" by a reviewer broadening this task's scope.
- `MVP_SCOPE.md` needs no task: retirement is a reduction and Special Gems were
  never listed IN.

---

## Testing Requirements

### Required Verification for this slice

```text
[x] git diff --check                     clean on docs/
[x] Line endings                        LF only, zero CRLF in both documents
[x] No src/ or tests/ change            verified by hash + git status
[ ] Unit / integration / E2E tests      NOT EXECUTED — this slice changes no code
```

### Historical validation (recorded by an earlier slice; NOT re-executed here)

```text
dotnet test ... --filter "FullyQualifiedName~ResourceGenerationTests"
  → Failed: 0, Passed: 39, Skipped: 0, Total: 39        (earlier slice)
dotnet test tests/backend/GameServer.Domain.Tests
  → Failed: 0, Passed: 1636, Skipped: 0, Total: 1636    (earlier slice)
```

These results belong to the **pre-OD** contract and are **not** evidence for it.
No test has been executed in this slice; none is claimed.

---

## Stop Conditions

- If any attempt is made to reconcile runtime behaviour before AC-13 passes: STOP
  (`AGENTS.md` §17 documentation-before-code).
- If a change is needed outside the three declared files: STOP (`AGENTS.md` §16).
- If any command would stage, commit, stash, reset, restore, clean, or overwrite a
  declared file from `HEAD`: STOP (`tasks/TASK_LIFECYCLE.md` §6.2 P-6).
- If a lifecycle transition (move/close/supersede) is required: STOP — it is not
  authorized in this slice (G-1).
- If a reviewer finds an authoritative rule conflict: STOP (`AGENTS.md` §4).
- If TASK-275/TASK-285 ownership remains ambiguous: STOP and escalate
  (`tasks/TASK_LIFECYCLE.md` §6.4 step 5) (G-3).

---

## Follow-Up Task Graph (proposed — records NOT created)

```text
DOC-286  THIS TASK (amended)      COMBAT_RULES §2 item 6, MATCH3_RULES §5.7 item 8
                                  ─┐ sits with DOC-287 in ONE join group commit
DOC-287  TO BE CREATED (TASK-287) │  GAME_RULES §3/§6/§7/§17, PASSIVE_RULES §2,
                                  │  MATCH3_RULES §4.1/§5/§6.3.1/§7.2,
                                  ┘  COMBAT_RULES §2 table + items 1–4
                                  Declared files (proposed):
                                    docs/01-game-design/GAME_RULES.md
                                    docs/01-game-design/MATCH3_RULES.md   ← shared with TASK-286
                                    docs/01-game-design/COMBAT_RULES.md   ← shared with TASK-286
                                    docs/01-game-design/PASSIVE_RULES.md
DOC-288  TO BE CREATED            GAME_STATE, GAME_EVENTS, SIGNALR_PROTOCOL,
                                  REDIS_STATE, TDD, ARCHITECTURE, ADR-027,
                                  NEW ADR-028 (retirement record), ADR index
IMP-289  TO BE CREATED            ATK-match gate only — Combat/AttackResolution.cs,
                                  Application/Battle/BattleStateService.cs + tests.
                                  Independent of SpecialGem (Application has none).
IMP-290  TO BE CREATED            Domain Match3 + Domain Battle/Serialization +
                                  ALL of Api + Domain/Api/Infrastructure tests
                                  (compile-coupled per G-4).
IMP-291  TO BE CREATED            Frontend presentation retirement — BLOCKED by G-2/G-3
                                  (TASK-275 and TASK-285 own those paths, IN REVIEW).
ADR-028  Retired-Special-Gem record — required by AGENTS.md §18 (battle-state model
                                  change); owned by DOC-288.
```

Ordering: `DOC-286 + DOC-287 (join)` → `DOC-288` → `IMP-289` → `IMP-290` → `IMP-291`.
No implementation task may start before both documentation gates pass.

None of DOC-287/288 or IMP-289/290/291 has been created. Creating them is a
separate, authorized step.

---

## Completion Evidence

**Completed.** `Status: DONE`. Phase A implementation slice committed and Phase B completion record filed.

```text
Phase A implementation slice       9fd779a3ca90b3e53cb6e488031304464f448a8e
Phase B completion record          tasks/completed/TASK-286-formalize-and-review-special-gem-combat-contribution-contract.md
Owns                               TASK-286 (joint group commit with TASK-287)
Files                              docs/01-game-design/COMBAT_RULES.md
                                   docs/01-game-design/MATCH3_RULES.md
                                   tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs
Shared with                        TASK-287 on COMBAT_RULES.md and MATCH3_RULES.md
Unowned / pre-existing             none
```

### Changed Files

- `docs/01-game-design/COMBAT_RULES.md` — v2.9; §2 item 6 rewritten to OD-1…OD-7.
- `docs/01-game-design/MATCH3_RULES.md` — v1.12; §5.7 item 8 rewritten to OD-1…OD-7.
- `tests/backend/GameServer.Domain.Tests/ResourceGenerationTests.cs` — 6 obsolete Special Gem tests removed/replaced with match-only regression tests.
- `tasks/completed/TASK-286-…md` — completion record filed.

### Server Authority & Scope Verification

- [x] Zero client-authoritative gameplay logic added or changed (no client file touched)
- [x] MVP Scope adhered to (`MVP_SCOPE.md` §1) — reduction only, no amendment needed
- [x] No runtime code changed (`src/` clean)
- [x] `ADR-026` not edited
- [x] P-1…P-6 posture: exact-set staging, joint commit protocol followed
- [x] Quality review passes (`.ai/workflow/quality/review.md` §1)
- [x] Backend tests pass: `dotnet test tests/backend/GameServer.Domain.Tests/` (1633 passed)
