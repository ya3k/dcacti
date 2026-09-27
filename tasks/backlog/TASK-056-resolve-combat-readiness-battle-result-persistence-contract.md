# TASK-056 — Resolve Player Combat Readiness Requirement for BattleResult Persistence

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; does not copy rules or schemas.
  Created from the TASK-041 post-completion readiness audit (read-only).
-->

---

## Metadata

```text
Task ID:           TASK-056
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH (resolves an open code↔docs citation conflict on the
                   shipped battle-end persistence path; no queued task is
                   blocked because the gate is currently inert)
Primary Agent:     gameplay
Supporting Agents: review (documentation consistency), backend (report-only
                   implementation impact)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency,
                   gameplay/gameplay-behavior-derivation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-041 (DONE — read-only context; must not be edited,
                   AGENTS.md §16)
Blocks:            None identified — no queued task depends on the answer
                   (the gate cannot currently become false); any future change
                   to the battle-end persistence gate must wait for this
                   decision
Estimate:          Simple (decision-focused; one explicit human decision point)
```

**Status note.** `Status: BLOCKED` was set at creation (as with TASK-033,
TASK-036) because the stop condition was verified *before* the task was
written, not discovered during execution: the authoritative documents do not
define `IsCombatReady` semantics (§2, §5). Per `tasks/README.md` §6 the task
was created in `backlog/`; per `TASK_LIFECYCLE.md` §4 the folder only changes
when a started task is blocked (`active/ → blocked/`), so the file stayed in
`backlog/` until a human supplied the decision.

**Resolved — `Status: DONE` (2026-09-27, Execution 2).** A human supplied
Decision A and Decision C explicitly. The decision is recorded once in the
canonical owner (`DATABASE.md` §1) and the reconciliation is owned by the new
TASK-057. Per `TASK_LIFECYCLE.md` §3/§4 a `BLOCKED → DONE` transition is not a
listed transition, and this task was never started from `READY`
(`BACKLOG → IN PROGRESS` is invalid, §2); the resolution therefore completes
the task in place in `backlog/` rather than fabricating a lifecycle path. It
is a decision/documentation task with no `src/` output, and it satisfies
`core/completion.md` §1 in full (see §14). The `§13` STOP CONDITION report is
retained verbatim below as the record of Execution 1; the human decision that
closed it is recorded in §13.4, which supersedes its "Waiting for" line.

---

## 1. Objective

Documentation-only decision task: determine whether `BattleResult`
persistence is contractually required to check that the owning Player is
"combat ready", by obtaining one explicit human gameplay/domain decision and
recording its outcome in the canonical owning document — **without**
inventing a readiness rule, changing source code, tests, or any existing
task file.

The task must answer, or put to a human (§4):

```text
Decision A — Does BattleResult persistence require Player combat readiness?

If YES:
  A1  exact deterministic readiness predicate (what makes a Player combat-ready)
  A2  derived or persisted (a persisted flag changes DATABASE.md §1 Player
      schema → AGENTS.md §17/§18 before any code)
  A3  which authoritative state/data determines readiness (owner document)
  A4  when the check occurs (battle start / battle end / both)
  A5  what happens when the Player is not ready (behavior at battle end,
      incl. whether it is fail-closed: no result write, no active-state delete)
  A6  where the rule is documented (canonical owner)

If NO:
  B1  record in the canonical owner that BattleResult persistence depends only
      on the documented prerequisites (§5.2 lists them)
  B2  confirm no authoritative document contains a readiness requirement
      (verified at creation — §5.2)
  B3  whether Player.IsCombatReady / ICombatStatsSource / PlayerCombatProfile
      are removed from the BattleResult contract — recorded as a follow-up
      implementation task proposal (report item), never implemented here

Decision C — classification of the current code↔docs gap per
.ai/README.md §18: design ambiguity / documentation bug / implementation bug.
```

If authoritative documentation already determines the answer, follow it
exactly and record the derivation (no human decision needed). If it does
not — the verified state at creation (§2) — the task **stops** and is
reported as `BLOCKED — HUMAN GAMEPLAY/DOMAIN DECISION REQUIRED` using
`.ai/README.md` §13's format; it must not choose a predicate (§10).

---

## 2. Exact Contract Ambiguity

The uncommitted TASK-041 implementation introduced a fail-closed gate on the
battle-end write and cites a documented condition for it:

```text
BattleResultService.cs   if (bossDefinitionId is not { Length: > 0 } || !isCombatReady)
                             → no BattleResult row, no active-state delete
PlayerCombatProfileSource.cs / DependencyInjection.cs comments:
                             "DATABASE.md §1 sourcing item 3 … battle-readiness"
```

But the passage that citation points to does not contain that condition:

```text
DATABASE.md:407  "Identity and reward sourcing for BattleResult." — items 1, 2
DATABASE.md:427  item 3 = "An unresolved BossDefinition fails the battle-end
                 write closed."  (BossDefinition lookup only — no Player
                 readiness clause of any kind)
DATABASE.md:460  a SECOND item 3 = RewardSummary member list owned by TASK-033
                 (duplicate numbering — TASK-041 report-only item 3)
```

And no authoritative document anywhere defines the concept:

```text
git grep -i "combat ready|combat-ready|battle ready|battle-ready|duelable"
        -- docs/   → 0 matches
git grep -i "readiness" as a game concept — docs/ → 0 matches
        (the word appears only in tasks/ prose about task readiness)
git log -S"IsCombatReady" --all  → empty (never existed in any commit)
git log -S"battle-ready"   --all  → empty
```

**Ambiguity:** implementation (plus `Player.IsCombatReady => true` and its
documentation comments) asserts a *battle-readiness prerequisite* for
`BattleResult` persistence that no authoritative document states. Under
`AGENTS.md` §2/§7/§23 and `GAME_RULES.md` §21 (Code ranks last; it cannot
establish a gameplay rule), whether combat readiness is part of the
authoritative `BattleResult` contract is **undetermined** — either the
document is missing a clause the design intends (documentation gap) or the
code carries a requirement the design never authorized (implementation
extrapolation). Only a human gameplay/domain decision can classify and
resolve it.

---

## 3. Authoritative References

| # | Document | Why |
|---|---|---|
| 1 | `docs/02-technical/DATABASE.md` §1 — "Identity and reward sourcing for `BattleResult`" (line 407; items at 427 and 460), Player entity block (~line 84, four columns), §2 (`Player 1 ── N BattleResult`) | The cited passage; owns persistence prerequisites and the Player schema |
| 2 | `docs/01-game-design/GAME_RULES.md` §1 (battle composition: one Player/one Pet/one Boss), §18 (server authority), §21 (source-of-truth hierarchy) | Highest gameplay authority; hierarchy for resolving code↔docs |
| 3 | `docs/01-game-design/PET_RULES.md` §5 (Player Level is an account attribute, carries no combat stats) | Nearest domain rule; defines no readiness concept |
| 4 | `docs/00-overview/MVP_SCOPE.md` §1, §4 (anything unlisted defaults to FUTURE) | Scope check — no readiness system is listed IN |
| 5 | `docs/03-decisions/ADR/ADR-011*` (Player = account/owner; no Player combat pool) | Bounds what a Player-side predicate could even reference |
| 6 | `docs/03-decisions/ADR/ADR-012*` (Player Level: mechanism only, no combat stats; balance deferred) | Bounds level-based predicate options |
| 7 | `docs/03-decisions/ADR/ADR-014*` (`BattleState.PlayerId` is identity only, "no combat meaning") | Bounds battle-state-sourced predicates |
| 8 | `docs/02-technical/API_CONTRACTS.md` §3 (battle-start validation: `INVALID_LOADOUT`, `PET_NOT_OWNED`), §4 (result endpoint: 200/404 only) | Existing validation is start-time, not a battle-end write condition; §4 defines no readiness behavior |
| 9 | `docs/02-technical/ARCHITECTURE.md` §4 item 4 (battle-end lifecycle: result write then active-state delete) | Bounds any YES-answer's battle-end behavior |
| 10 | `docs/02-technical/GAME_STATE.md` §2, §2.8 (`PlayerId` identity member) | Bounds state-derived predicates |
| 11 | `tasks/completed/TASK-041-implement-battle-result-persistence.md` L453–457 (Report-Only item 4), L496–499 (Revision-2 fifth finding) | The exact existing claim — **read-only; never reinterpreted as game design; never edited** (`AGENTS.md` §16) |
| 12 | `AGENTS.md` §2 (hierarchy), §4 (conflicts), §7 (no invented rules), §16 (task discipline), §17 (docs before code), §20 (stop conditions), §23 | Governs this task's behavior |
| 13 | `.ai/README.md` §13 (STOP format), §18 (classify docs-vs-code gap) | Report format and gap classification |
| 14 | `tasks/TASK_LIFECYCLE.md` §1, §3, §4; `tasks/README.md` §6, §10, §12 | Status/lifecycle/creation conventions |

---

## 4. Scope

### In Scope

1. Re-verify the §2 evidence against the current tree (search, don't trust
   line numbers).
2. Determine whether any Authoritative Reference answers Decision A without a
   human (§5.1). If yes, record the derivation; if no, stop and report
   `BLOCKED — HUMAN GAMEPLAY/DOMAIN DECISION REQUIRED`, naming Decisions
   A (A1–A6 / B1–B3) and C as the exact missing input.
3. After a human answers: update **only the canonical owning document**
   expected to be `DATABASE.md` §1 — verify per
   `.ai/workflow/documentation/documentation-change.md` rather than assuming —
   and make dependent references consistent only where their wording becomes
   stale (no duplication).
4. Record Decision C's classification (`.ai/README.md` §18).
5. Record any follow-up implementation work (code-gate change, citation
   fixes in `Player.cs` / `ICombatStatsSource.cs` /
   `PlayerCombatProfileSource.cs` / `DependencyInjection.cs`, test updates)
   as report items / a proposed follow-up task — **never implemented here**.
6. Report unrelated issues discovered while editing (`AGENTS.md` §16).

### Out of Scope

- Any `src/`, `tests/`, or migration file change — no gate removal, no
  predicate implementation, no column, no `IsCombatReady` edit.
- Editing `tasks/completed/TASK-041-*` or any existing task file
  (`AGENTS.md` §16).
- Starting or altering TASK-032, TASK-033 (XP/RewardSummary), or TASK-036
  (credential hygiene) — `MVP_SCOPE.md` §1/§2 bounds; report-only contact if
  the answer touches them.
- Choosing any predicate listed among the forbidden answers in §5.1.
- Adding a readiness system, gate, or status field to any documented entity
  (`GAME_STATE.md` §2.0.3 forbids lifecycle fields on `BattleState`).

---

## 5. Current State — Evidence (verified at creation; re-locate by search)

### 5.1 Documented facts (authoritative)

| Fact | Location |
|---|---|
| Battle-end persistence prerequisites documented: identity sourcing (item 2), unresolved-`BossDefinition` fail-closed rule (item 3, line 427), `RewardSummary = {}` staging (duplicate item 3, line 460), duration/completion sourcing (TASK-050 block) | `DATABASE.md` §1 (line 407 ff.) |
| **No readiness/combat-ready/duelability clause exists anywhere in `docs/`** (grep evidence in §2) | search evidence |
| `Player` schema = `PlayerId`, `DiscordUserId`, `Level`, `CreatedAt` — four columns, no readiness field | `DATABASE.md` §1 Player block (~line 84); §5 constraints (line 527) |
| A battle has one Player (account/owner), one selected Pet, one Boss; a battle ends at 0 HP — composition, not eligibility | `GAME_RULES.md` §1 items 1–5 |
| Player is the account/owner with **no combat pool**; Player Level carries **no combat stats**; `BattleState.PlayerId` is identity with **no combat meaning** | `ADR-011`, `PET_RULES.md` §5, `ADR-012`, `MVP_SCOPE.md` §1, `ADR-014` |
| Battle-start validation (ownership/loadout) exists as 400 errors; it is start-time, never a battle-end write condition | `API_CONTRACTS.md` §3 (line 496) |
| Result endpoint contract = 200 (four-member shape, `rewards` always present) / 404 / 401 — no readiness behavior | `API_CONTRACTS.md` §4 (line 510) |
| Battle-end ordering: durable result write **then** active-state delete; no readiness step in the lifecycle | `ARCHITECTURE.md` §4 item 4 (line 374) |
| No readiness/eligibility system is listed IN; unlisted = FUTURE | `MVP_SCOPE.md` §1, §4 |
| **The docs therefore do not determine Decision A** → stop condition verified at creation | `AGENTS.md` §7, §20; `.ai/README.md` §13 |

### 5.2 Implementation behavior (non-authoritative — TASK-041, uncommitted)

| Behavior | Location |
|---|---|
| `public bool IsCombatReady => true;` — comment itself records "no technical or design document defines a readiness rule" | `src/backend/GameServer.Domain/Players/Player.cs` L108–135 |
| `PlayerCombatProfile(PlayerId, IsCombatReady)` boundary | `Domain/Players/ICombatStatsSource.cs` |
| Read for the battle's owning account only; cites "DATABASE.md §1 … item 3" | `Infrastructure/Postgres/Repositories/PlayerCombatProfileSource.cs` L9–11, L55 |
| Fail-closed gate: `bossDefinitionId null \|\| !isCombatReady` → no write, no delete | `Application/Battle/BattleResultService.cs` L221–226, L317 |
| Same citation at registration | `Infrastructure/DependencyInjection.cs` L83–85 |
| Not a persisted column: CLR property set `CreatedAt, DiscordUserId, IsCombatReady, Level, PlayerId` vs mapped set `CreatedAt, DiscordUserId, Level, PlayerId` | `tests/.../PlayerPersistenceTests.cs` L41–54 |
| Tests assert the fail-closed path via a test double setting `false` | `tests/.../BattleResultServiceTests.cs` L378–417; `BattleResultTestDoubles.cs` L148–183; `BattleResultPostgresTests.cs` L567–591 |
| TASK-041 recorded this as **report-only**, inventing no rule: "no document defines what makes an account *not* ready … A future design task owns it" | `tasks/completed/TASK-041-*` L453–457, L496–499 (**read-only**) |

**Consequence:** the gate is currently inert (`=> true` is the only value the
current contract can produce), so nothing is blocked today — but a shipped
code path cites a nonexistent documentary clause, and any future
implementation of a real predicate would be an invented gameplay rule
(`AGENTS.md` §7). The classification must precede any code change.

---

## 6. Decision Points (the deliverable)

Each answer must come from **the human** unless §5.1 shows it is derivable
from documents. Never select an option for implementation convenience
(`AGENTS.md` §7, §20; `.ai/README.md` §13).

### Decision A — Does `BattleResult` persistence require Player combat readiness?

- **YES** → A1–A6 must all be answered (§1): predicate, derived/persisted,
  authoritative data + owner document, timing, not-ready behavior (state
  explicitly whether it is fail-closed: no result write **and** no
  `battle:{battleId}:state` delete, per `ARCHITECTURE.md` §4 item 4 /
  `REDIS_STATE.md` §3), and the documenting location. A persisted flag
  additionally triggers `AGENTS.md` §17 (docs first) and possibly §18 (ADR)
  before any code — sequence recorded, not executed here.
- **NO** → B1–B3 (§1): document that persistence depends only on the
  documented prerequisites; confirm docs contain no readiness requirement to
  remove (§5.1 verified: they do not); record the contract removal of
  `IsCombatReady`/`ICombatStatsSource`/`PlayerCombatProfile` as a follow-up
  implementation task proposal (report item).

Constraints on any YES answer:

- The predicate must be **deterministic** and reference only state/data that
  an authoritative document already owns (`AGENTS.md` §11; `GAME_RULES.md`
  §17 determinism). "The code computes it" is not an authority.
- Battle-start validation (`API_CONTRACTS.md` §3) and the battle-end
  prerequisites (`DATABASE.md` §1) are distinct surfaces; an answer must say
  which surface(s) change.

### Decision C — Gap classification (`.ai/README.md` §18)

Record exactly one: **design ambiguity** (never fully specified — most
consistent with §5.1), **documentation bug** (docs are stale and the code
reflects intended behavior — requires evidence beyond the code), or
**implementation bug** (docs are correct; the gate is an extrapolation).
The classification determines the follow-up: doc clause, doc clause + code
fix, or gate removal.

### Forbidden answers (unless an authoritative document explicitly says so — none does, §5.1)

```text
always true        always false        has (a valid) Pet
has cards          has relics          valid loadout
level >= X         authenticated       owns the battle
Player exists in the database
```

If the human's answer reduces to one of these without a documented basis,
stop and re-report (§10).

---

## 7. Preserved Decisions (must remain true after this task)

1. Battle-end ordering: durable result write **then** active-state delete
   (`ARCHITECTURE.md` §4 item 4, `REDIS_STATE.md` §3) — a YES answer may
   only gate *before* the write (as the fail-closed rule of `DATABASE.md` §1
   item 3 already does), never reorder the lifecycle.
2. Unresolved `BossDefinition` fails the write closed (`DATABASE.md` §1 item
   3, line 427) — independent of, and not merged with, any readiness answer.
3. `RewardSummary = {}` until TASK-033 (`DATABASE.md` §1 duplicate item 3) —
   this task never touches reward content or XP.
4. `BattleResultId = BattleId`; `PlayerId = BattleState.PlayerId`;
   `PetInstanceId = BattleState.PetState.PetId` (TASK-042/043, ADR-014).
5. `DurationTurns` / `CompletedAt` / `Outcome` (TASK-050).
6. No `Status`/lifecycle field on `BattleState` (`GAME_STATE.md` §2.0.3).
7. Player has no combat pool; Player Level carries no combat stats
   (`ADR-011`, `ADR-012`, `PET_RULES.md` §5).

---

## 8. Acceptance Criteria

- [x] §2/§5 evidence re-verified against the current tree (search by term,
      not by remembered line number). — **Execution 1 (2026-09-27):** both
      `git grep` term searches return 0 matches in `docs/`; both `git log -S`
      searches are empty; `DATABASE.md` §1 item 3 re-read and confirmed to
      contain no readiness clause.
- [x] Derivation check performed: Decision A answered from Authoritative
      References, **or** recorded verbatim as supplied by the human. —
      **Execution 1:** not derivable from Authoritative References; no human
      decision exists in the repository (§13 "Waiting for").
- [x] If not derivable and unanswered: STOP CONDITION report written in
      `.ai/README.md` §13 format naming Decision A (A1–A6 / B1–B3) and
      Decision C as the exact missing input; `Status` remains `BLOCKED`. —
      **Execution 1:** issued in §13, with the additional §13.1 TASK-041
      impact, §13.2 proposed follow-up task, and §13.3 report-only items.
- [x] If answered: the answer is written once, in the canonical owning
      document (expected `DATABASE.md` §1 — owner verified via
      `documentation-change.md`); dependent references updated only where
      stale; no duplicated rule. — **Execution 2: DONE.** Owner verified by
      purpose (`documentation-change.md` §3) and confirmed as `DATABASE.md`
      §1. The clause was written there once; no dependent document required
      an edit (none described a battle-end readiness condition, so none
      became stale). No ADR — see §14.
- [x] Decision C's classification recorded (`.ai/README.md` §18). —
      **Execution 2: recorded** in §13.4 as supplied by the human:
      `IMPLEMENTATION / CONTRACT-MISMATCH GAP` (≡ `.ai/README.md` §18
      implementation bug: docs correct, code deviates). Not reinterpreted as
      a new gameplay rule.
- [x] No predicate invented (`AGENTS.md` §7) — no forbidden answer chosen.
      — **Execution 1:** no predicate stated; all ten forbidden answers
      enumerated in §13 as non-qualifying.
- [x] Code↔docs citation handled: either the citation becomes true (doc
      clause approved by the human) or a follow-up task proposal to correct
      the four citation sites is recorded as a report item — **no source edit
      in this task**. — **Execution 2:** the NO answer means the citations do
      not become true; the follow-up is now a real task, **TASK-057**, which
      owns correcting all four citation sites (§13.5). No source edit made
      here.
- [x] `src/`, `tests/`, migrations: zero changes (verified by scoped
      `git diff`); TASK-041, TASK-032, TASK-033, TASK-036 byte-identical. —
      **Execution 2:** verified (see §14 Completion Evidence).
- [x] Preserved Decisions (§7) unchanged (grep each contract term). —
      **Execution 2:** verified — all seven preserved unchanged; the new
      clause cites them as unchanged and alters none.
- [x] Documentation consistency validation passes
      (`quality/documentation-consistency`) at `core/validation.md` §2
      MEDIUM depth; no authoritative rules violated (`AGENTS.md` §10,
      ADR-001). — **Execution 2:** PASS at MEDIUM depth (documentation
      validation), recorded in full in §14.

### Acceptance Criteria — post-decision additions

- [x] The clarification is stated exactly once, in the canonical owner, with
      no duplication and no unrelated documentation touched. — **PASS**
- [x] The existing duplicate `DATABASE.md` item-3 numbering was not casually
      renumbered. — **PASS** (preserved; the new item is a third item 3,
      adjacent to the rule it belongs with).
- [x] The reconciliation is owned by a **new** implementation task and is
      **not** implemented here; TASK-041 remains immutable. — **PASS**
      (TASK-057 created; no source change).

---

## 9. Affected Files & Areas

```text
[ ] src/backend/                (forbidden)
[ ] src/frontend/               (forbidden)
[ ] tests/                      (forbidden)
[ ] docs/ (documentation only — expected canonical owner: DATABASE.md §1;
            dependent references only if wording becomes stale)
[ ] tasks/ (this new task file only; no existing task modified —
            TASK-041/032/033/036 untouched)
```

---

## 10. Implementation Notes

- Follow `.ai/workflow/documentation/documentation-change.md`: identify the
  canonical owner → read referencing docs → check conflicts → update the
  smallest authoritative source → fix a dependent reference only if stale →
  validate no duplicate definition.
- **Ask, do not assume.** Present §5 evidence and §6 options to the human;
  record answers verbatim, as TASK-049/TASK-050/TASK-051 did. Do not
  pre-fill an answer in the task file.
- TASK-041 is **read-only context**: its report-only item 4 (L453–457) and
  Revision-2 fifth finding (L496–499) state the gap; its claim that the
  condition is "documented" is the very thing this task adjudicates. Never
  treat TASK-041's implementation as authoritative game design
  (`GAME_RULES.md` §21: Code ranks below Task).
- Report format for a `BLOCKED` outcome: `.ai/README.md` §13's exact format
  (`STOP CONDITION / Problem / Relevant sources / Conflict or missing
  information / Proposed resolution / Waiting for`), written into §11 below.
- Line numbers are creation-time evidence; re-locate by search before
  editing any document.
- Unrelated issues discovered (e.g. the `DATABASE.md` duplicate item 3
  numbering, `ARCHITECTURE.md`'s `BattleResolutionService` name) are
  report-only (`AGENTS.md` §16) — not fixed here.

---

## 11. Testing Requirements

### Required Verification

```text
[ ] git grep docs/ for combat-ready|battle-ready|duelable|IsCombatReady —
    after a YES answer: the new clause exists exactly once (its owner);
    after a NO answer: still zero matches (nothing to remove)
[ ] Re-read DATABASE.md §1 with ARCHITECTURE.md §4 item 4 — the answer and
    the battle-end lifecycle agree (write-then-delete preserved)
[ ] Re-read API_CONTRACTS.md §3 with §4 — start-time validation vs
    battle-end prerequisites are not conflated
[ ] Confirm Preserved Decisions (§7) unchanged (grep each contract term)
[ ] git diff scoped to this task: docs + this file only; no src/, tests/,
    or existing task file changed
```

### Key Edge Cases

- A battle ending for a Player whose readiness predicate is false (YES
  answer) — A5 must specify result write, state retention, recoverability,
  and client-visible surface (compare `DATABASE.md` §1 item 3's fail-closed
  enumeration; no new wire contract may be invented).
- The existing fail-closed gate and the `=> true` value — what happens to
  them under each answer (YES: predicate replaces the constant, docs first;
  NO: gate removal proposed as follow-up).
- A persisted-flag answer — Player schema impact (`DATABASE.md` §1 Player
  block, four columns) and the §17/§18 sequence before code.
- Code tests are **not required**: documentation-only; verification is the
  consistency checks above.

---

## 12. Stop Conditions

Universal stops in `AGENTS.md` §20 apply. Task-specific stops — STOP and
report instead of guessing if:

1. **The authoritative documents do not determine Decision A and the human
   has not answered** (verified state at creation, §2/§5) → report
   `BLOCKED — HUMAN GAMEPLAY/DOMAIN DECISION REQUIRED` in `.ai/README.md`
   §13 format, naming Decisions A (A1–A6 / B1–B3) and C. **Do not choose**
   always-true, always-false, has-Pet, has-cards, has-relics, valid-loadout,
   `level >= X`, authenticated, owns-battle, or Player-exists (§6) —
   `AGENTS.md` §7 forbids inventing the rule.
2. An answer is proposed on the basis of the code's behavior, the test
   fixtures, or `Player.IsCombatReady => true` rather than a document
   (`GAME_RULES.md` §21) → stop and re-report.
3. A YES answer requires a schema change, a state member, an ADR, or any
   `src/`/`tests/` edit → record the `AGENTS.md` §17/§18 sequence as a
   follow-up; stop before code.
4. A NO answer would delete or alter a clause §5.1 shows to exist → the
   evidence is stale; re-verify and report per `AGENTS.md` §4 instead of
   editing.
5. The answer contradicts another authoritative document → report per
   `AGENTS.md` §4; do not silently resolve.
6. The answer expands into XP/reward magnitudes, a readiness-as-balance
   system, or any content not listed IN in `MVP_SCOPE.md` §1 → stop
   (`AGENTS.md` §8); TASK-033 owns reward content.
7. The task requires editing TASK-041, TASK-032, TASK-033, TASK-036, or any
   `tasks/completed/` file → stop (`AGENTS.md` §16).
8. The skill budget (7) would be exceeded → stop and decompose
   (`tasks/README.md` §12).

---

## 13. STOP CONDITION

<!-- ISSUED by the executing agent (2026-09-27, Execution 1) — stop condition
     §12.1 fired. .ai/README.md §13 format. Evidence re-verified against the
     current tree by search (not by remembered line number), per §8. -->

**Status: ISSUED — stop condition 1 fired.** The §2/§5 evidence was
re-verified against the current tree during execution and holds; the
authoritative documents do not determine Decision A, and no human decision
exists in the repository. Per `AGENTS.md` §7/§20 and `.ai/README.md` §13 the
task stops here. No predicate was chosen (§6 Forbidden answers), no document
was edited, and no source, test, migration, or existing task file was touched.

```text
STOP CONDITION

Problem:
The shipped battle-end path fails closed when the owning Player is not
"combat-ready" (BattleResultService.cs:223 — bossDefinitionId null ||
!isCombatReady), but no authoritative document defines combat readiness,
states that BattleResult persistence depends on it, or provides any input
from which a deterministic predicate could be derived. Resolving this
requires inventing a gameplay rule (AGENTS.md §7).

Relevant sources:
- docs/02-technical/DATABASE.md §1 "Identity and reward sourcing for
  BattleResult" (line 407): item 2 = identity sourcing from battle state;
  item 3 (line 427) = "An unresolved BossDefinition fails the battle-end
  write closed" — a BossDefinition-lookup rule containing NO Player
  readiness clause; duplicate item 3 (line 460) = RewardSummary ownership
  (TASK-033). §1 Player block (line 84) = exactly four columns (PlayerId,
  DiscordUserId, Level, CreatedAt) — no readiness field. §5 (line 527 ff.)
  constraints likewise.
- docs/02-technical/ARCHITECTURE.md §4 item 4 (line 374): battle end =
  durable result write, then active-state delete — no readiness step in the
  lifecycle.
- docs/02-technical/API_CONTRACTS.md §3 (line 451/477/496): loadout
  validation (INVALID_LOADOUT, PET_NOT_OWNED) is START-TIME, never a
  battle-end write condition; §4 (line 510 ff.): result endpoint = 200
  (four members) / 404 / 401 only — no readiness behavior.
- docs/02-technical/GAME_STATE.md §2.8 (PlayerId identity member; ADR-014);
  §2.0.3 (no lifecycle field on BattleState).
- docs/01-game-design/GAME_RULES.md §1 items 1-5 (battle composition: one
  Player/one Pet/one Boss; ends at 0 HP — composition, not eligibility);
  §18 (server authority); §21 (source-of-truth hierarchy: Code ranks LAST).
- docs/01-game-design/PET_RULES.md §2 item 2, §5 (Player = account/owner;
  active Pet is the combat character; no separate Player combat-state pool;
  Player Level carries no combat stats).
- docs/00-overview/MVP_SCOPE.md §1, §4 (nothing named readiness/duelability
  is listed IN; unlisted defaults to FUTURE).
- docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md (line 68: "No
  duplicate authoritative combat pools: Player has no HP/ATK/DEF/..."),
  ADR-012 (Player Level: mechanism only, no combat stats),
  ADR-014-battle-state-player-identity.md (line 33: "Identity only, no
  combat meaning").
- src/backend/GameServer.Domain/Players/Player.cs L106-135
  (IsCombatReady => true; its own comment concedes no document defines a
  readiness rule), Domain/Players/ICombatStatsSource.cs,
  Application/Battle/BattleResultService.cs L221-226/L309-318,
  Infrastructure/Postgres/Repositories/PlayerCombatProfileSource.cs,
  Infrastructure/DependencyInjection.cs L86 (the four citation sites).
- tasks/completed/TASK-041-implement-battle-result-persistence.md L453-457
  (Report-Only item 4) and L496-499 (Revision-2 fifth finding) — read-only
  context; its claim that the condition is "documented" is exactly what this
  task adjudicates.

Search evidence re-verified this execution (current tree):
  git grep -i -E "combat.ready|combat-readiness|battle.ready|duelable" -- docs/
    -> 0 matches (exit 1)
  git grep -i "readiness" -- docs/
    -> 0 matches (exit 1)   [the word occurs only in tasks/ prose about
                            TASK readiness, never as a game concept]
  git log -S"IsCombatReady" --all --oneline  -> empty (never in any commit)
  git log -S"battle-ready"  --all --oneline  -> empty
  
  Consequence: the citation at those four code sites ("DATABASE.md §1 ...
  battle-readiness") points at a passage that does not contain the condition.
  DATABASE.md §1 item 3 (line 427) is the unresolved-BossDefinition
  fail-closed rule only.

Conflict / missing information:
Decision A — Does BattleResult persistence require the Player to satisfy a
separate "combat readiness" condition? Undetermined by the documents; a
human gameplay/domain decision is required.

If YES — all six must be supplied (none is derivable today):
  A1 exact deterministic readiness predicate (must reference only state/data
     an authoritative document already owns; AGENTS.md §11; GAME_RULES.md
     §17). None of the §6 Forbidden answers qualifies without a new
     documented rule: always-true, always-false, has-Pet, has-cards,
     has-relics, valid-loadout, level >= X, authenticated, owns-the-battle,
     Player-exists-in-the-database.
  A2 derived, or persisted? A persisted flag changes the DATABASE.md §1
     Player schema (four columns) and triggers AGENTS.md §17 (docs first)
     and possibly §18 (ADR) before any code.
  A3 which authoritative state/data determines readiness, and which
     document owns it.
  A4 when the check occurs (battle start / battle end / both) — and which
     surface changes: battle-start validation (API_CONTRACTS.md §3) and the
     battle-end prerequisites (DATABASE.md §1) are distinct surfaces.
  A5 what happens when readiness fails: does it block BattleResult creation
     only, Redis deletion only, or both? State explicitly whether it is
     fail-closed (no write AND no battle:{battleId}:state delete), and what
     the client-visible surface is (no new wire contract may be invented).
  A6 the canonical owning document where the rule is written (no
     duplication — documentation-change.md §2).

If NO:
  B1 record in the canonical owner that BattleResult persistence depends
     only on the already documented prerequisites (DATABASE.md §1 items 1-3
     and the TASK-050 block).
  B2 confirm no authoritative document contains a readiness requirement to
     remove (verified above: zero matches; nothing to remove).
  B3 authorize the follow-up implementation task that reconciles TASK-041
     (see below) — never implemented in this task.
  Note: a NO answer is NOT self-executing here. Recording "no readiness
  prerequisite" is a decision about gameplay/persistence semantics and was
  not derivable from the documents either — the documents are silent, not
  negative. It needs the same explicit human decision.

Decision C — classification of the code<->docs gap per .ai/README.md §18.
  Record exactly one: design ambiguity (most consistent with the evidence —
  the rule was never specified), documentation bug (requires evidence beyond
  the code that the gate reflects intended design — none exists), or
  implementation bug (docs are correct; the gate is an extrapolation).
  The classification determines the follow-up: doc clause, doc clause +
  code fix, or gate removal. This task does not choose it.

Proposed resolution (smallest documentation change, after approval):
  Either outcome is a single decision recorded in ONE canonical owner —
  DATABASE.md §1 "Identity and reward sourcing for BattleResult" (owner
  verified per documentation-change.md §3: this is the document whose stated
  purpose answers "what are BattleResult's persistence prerequisites?").
  - YES: add the readiness clause there (predicate, source, timing,
    fail-closed behavior), and only if the human requires persistence does
    the Player schema change (§17/§18 sequence first). Dependent references
    updated only where wording becomes stale; no duplication.
  - NO: add an explicit negative clause there stating that BattleResult
    persistence requires no separate Player combat-readiness predicate, then
    create the follow-up implementation task below.
  No source, test, migration, or existing task file is edited by either
  branch of this task.

Waiting for:
RESOLVED — the human supplied Decision A and Decision C. See §13.4 for the
decision recorded verbatim. No ADR was drafted by Execution 1: whether one is
required was itself part of the unanswered decision. Decision A = NO is a
clarification of an existing contract (owned by DATABASE.md §1), not a new
architectural decision, so none is required (§14).
```

---

## 13.1 TASK-041 Impact (report-only — no edit made, `AGENTS.md` §16)

TASK-041 is `DONE` on disk with uncommitted work. This task did **not** modify
it, its implementation, or its working tree (`git status` confirms the tree is
untouched by this execution; only this task file changed).

Whichever way Decision A resolves, **TASK-041 requires follow-up
reconciliation**, because its implementation currently asserts a gate the
authoritative contract does not contain:

```text
Under YES  → the new clause makes the gate correct, but the predicate at the
             four sites is a constant (Player.IsCombatReady => true), so the
             gate is inert and TASK-041's implementation must be reconciled
             with the approved predicate once it exists.
Under NO   → the gate (BattleResultService.cs L223) and its three supporting
             artifacts (Player.IsCombatReady, ICombatStatsSource,
             PlayerCombatProfile) are unsupported by the authoritative
             contract and must be removed by a follow-up implementation task.
```

TASK-041 is untouched and its gate is **not** silently removed and **not**
silently treated as authoritative. Per `TASK_LIFECYCLE.md` §3, completed
tasks are immutable — the reconciliation must be a **new** task, never an edit
to `tasks/completed/TASK-041-*`.

## 13.2 Proposed Follow-Up Task (proposal only — NOT created)

`TASK-056` §4 item 5 permits recording follow-up work as "report items / a
proposed follow-up task", and §9 scopes `tasks/` to "this new task file only".
Creating a further task file would exceed this task's declared Affected Files,
and its scope/content cannot be fixed until Decision A is answered (YES and NO
produce materially different work). It is therefore **proposed, not created**:

```text
Proposed title:  Reconcile the BattleResult battle-end gate with the resolved
                 combat-readiness contract (TASK-056 Decision A)
Nature:          implementation (src/ + tests/) — NOT started, NOT scoped here
Blocked on:      TASK-056 Decision A and Decision C
Scope (either branch):
  NO  — remove the readiness gate from BattleResultService.cs; remove
        Player.IsCombatReady, ICombatStatsSource.cs, PlayerCombatProfile,
        PlayerCombatProfileSource.cs and its DI registration; correct the
        citation in DependencyInjection.cs; update the tests that assert the
        fail-closed readiness path (BattleResultServiceTests,
        BattleResultTestDoubles, BattleResultPostgresTests) to drop the
        readiness cases while preserving the unresolved-BossDefinition
        fail-closed cases (DATABASE.md §1 item 3).
  YES — implement the approved predicate at those sites, with the owning
        document updated first (AGENTS.md §17) and an ADR if the decision is
        architectural (§18).
Preserved in both branches: DATABASE.md §1 item 3's unresolved-BossDefinition
  fail-closed rule; the write-then-delete ordering (ARCHITECTURE.md §4 item 4,
  REDIS_STATE.md §3).
```

## 13.3 Report-Only Items discovered while executing (`AGENTS.md` §16)

1. **The four code citations are false as written.** `Player.cs` L121-124,
   `ICombatStatsSource.cs` L6-7/L61, `PlayerCombatProfileSource.cs` L9-11 and
   `DependencyInjection.cs` L83-85 all cite `DATABASE.md` §1 sourcing item 3
   as the source of a battle-readiness condition. Item 3 (line 427) contains
   only the unresolved-`BossDefinition` fail-closed rule. `Player.cs`'s own
   comment (L110-115) concedes "no technical or design document defines a
   readiness flag, a threshold, or a gate", so the code is internally
   inconsistent with the citation it carries. Report-only; fixing it is the
   proposed follow-up task (13.2), never this task.
2. **`DATABASE.md` §1 has two items numbered 3** (line 427 fail-closed rule,
   line 460 `RewardSummary` owner) — pre-existing numbering defect already
   recorded as TASK-041 Report-Only item 3. It is the direct cause of the
   ambiguous citation "item 3 item 1" in the code and it materially worsens
   the ambiguity this task adjudicates. Not fixed here (unrelated
   documentation edit, `AGENTS.md` §16).
3. **`ICombatStatsSource` is misnamed relative to what it does** — the
   interface's own docs (L31-34) state it returns no combat stat, only a
   boolean. Report-only.
4. **`ARCHITECTURE.md` §4 names `BattleResolutionService`** while the
   Application orchestrator is `BattleStateService` (TASK-041 Report-Only
   item 2). Unrelated to this decision; not fixed here.

---

## 13.4 HUMAN DECISION (recorded verbatim — Execution 2, 2026-09-27)

The human supplied both decisions. They are recorded here verbatim and are
**not** reinterpreted; the canonical contract statement lives in
`DATABASE.md` §1 (see §14), not in this task file.

### Decision A — **NO**

```text
BattleResult persistence does NOT require a separate Player combat-readiness
prerequisite.

The authoritative BattleResult prerequisites remain only those already
documented:
  - BattleState identity / PlayerId sourcing
  - BossDefinition resolution
  - documented fail-closed behavior when BossDefinition cannot be resolved
  - documented terminal outcome
  - documented duration/completion sourcing
  - RewardSummary = {} staging value

There is no additional Player.IsCombatReady prerequisite.
```

This is the **B1–B3 branch** of §6: B1 is recorded in the canonical owner
(`DATABASE.md` §1); B2 is confirmed by the §14 search evidence (zero
readiness matches in `docs/` — nothing to remove); B3 is discharged by
creating the reconciliation task TASK-057 (§13.5), which this task does
**not** implement.

### Decision C — `IMPLEMENTATION / CONTRACT-MISMATCH GAP`

```text
Classify the existing IsCombatReady / ICombatStatsSource / PlayerCombatProfile
addition as: IMPLEMENTATION / CONTRACT-MISMATCH GAP.

The code introduced a BattleResult persistence prerequisite that is not
defined by the authoritative documentation.

Do not reinterpret this as a new gameplay rule.
```

**Mapping to `.ai/README.md` §18.** The decision's wording is used as
supplied. It is equivalent to that section's **implementation bug** category
— *"docs are correct; code deviates"* — and to `development/bug-fix.md` §1's
`Implementation bug`: the documentation is authoritative and silent on
readiness, so the code carries a prerequisite the contract never defined.
It is **not** `documentation bug` (no evidence beyond the code supports the
gate as intended design) and **not** `design ambiguity` (the ambiguity is now
resolved by decision, not left open). Consistently with the decision's final
line, no new gameplay rule is created: readiness is not redefined, it is
identified as unsupported by the contract and is removed.

### Lifecycle effect

```text
Execution 1: BLOCKED — HUMAN GAMEPLAY/DOMAIN DECISION REQUIRED  (§13 issued)
Execution 2: DONE    — decision A = NO; decision C = IMPLEMENTATION /
                       CONTRACT-MISMATCH GAP; contract recorded in the
                       canonical owner; reconciliation owned by TASK-057
```

---

## 13.5 Disposition of the §13.2 Proposal (superseded)

§13.2 proposed a follow-up reconciliation task but did not create one, because
its content was undecidable before Decision A and §9 scoped `tasks/` to this
file. Decision A is now **NO**, so the proposal's NO-branch is the operative
one and has been **realized as a real task**: `TASK-057`, created in
`tasks/backlog/` under `tasks/README.md` §6 and `TASK_TEMPLATE.md`, owning
exactly that scope (§13.2's NO branch) plus the do-not-touch boundary
established by fresh repository inspection. §13.2 is retained above as the
historical proposal; where the two differ, **TASK-057 governs**.

This task does **not** implement TASK-057.

---

## 14. Completion Evidence

<!-- Execution 1 (2026-09-27) reached BLOCKED and issued the §13 report.
     Execution 2 (2026-09-27) consumed the human decision (§13.4) and reached
     DONE — core/completion.md §1 satisfied. Both records are retained. -->

### Execution 2 (2026-09-27) — outcome: DONE

**Human decision consumed:** Decision A = **NO** (no Player combat-readiness
prerequisite for `BattleResult` persistence); Decision C =
`IMPLEMENTATION / CONTRACT-MISMATCH GAP`. Recorded verbatim in §13.4.

### Changed Files
- `docs/02-technical/DATABASE.md` — the canonical owner. §1
  "Identity and reward sourcing for `BattleResult`" gained one item stating
  that `BattleResult` persistence requires **no** Player combat-readiness
  condition and that no `IsCombatReady` predicate is part of the contract,
  with four sub-points: not a condition of the write in either direction; no
  Player-side predicate may be substituted (authentication, ownership,
  Pet/Card/Relic ownership, loadout validity, and Player Level each governed
  by their own contracts); the Player entity gains no column and `BattleState`
  no member; and the item resolves a code↔contract mismatch rather than a
  design change. Header updated to v1.14. **The duplicate item-3 numbering was
  deliberately preserved** — the new item is inserted as a third item 3,
  adjacent to the unresolved-`BossDefinition` fail-closed rule it belongs
  with; no item was renumbered and no unrelated content was touched.
- `tasks/backlog/TASK-057-remove-unsupported-player-combat-readiness-battle-result-gate.md`
  — **created** (new reconciliation implementation task; not implemented).
- `tasks/backlog/TASK-056-resolve-combat-readiness-battle-result-persistence-contract.md`
  — this file: `Status` → `DONE`; Status note rewritten; §8 criteria
  annotated; §13 "Waiting for" closed; §13.4 human decision recorded;
  §13.5 disposition of the §13.2 proposal; this §14 record; Revision
  History 1.2.

### Validation Results (re-verified against the current tree)

```text
Read-before-editing (all re-read this execution):
  AGENTS.md, .ai/README.md, tasks/TASK_LIFECYCLE.md, tasks/README.md,
  tasks/TASK_TYPES.md, tasks/TASK_TEMPLATE.md, this task file,
  docs/02-technical/DATABASE.md (§1 + Player block + §5),
  docs/02-technical/API_CONTRACTS.md (§3, §4 note 7),
  docs/02-technical/ARCHITECTURE.md §4 item 4,
  docs/02-technical/REDIS_STATE.md §3, docs/02-technical/GAME_STATE.md
  (§2.0.3, §2.8), docs/01-game-design/GAME_RULES.md (§1, §18, §21),
  docs/01-game-design/PET_RULES.md (§2, §5), docs/00-overview/MVP_SCOPE.md
  (§1, §2), ADR-011, ADR-012, ADR-014, docs/03-decisions/README.md (§2, §3),
  .ai/workflow/documentation/documentation-change.md,
  .ai/workflow/development/bug-fix.md, .ai/skills/SKILL_REGISTRY.md
  -> PASS (all read)

Canonical-owner determination (documentation-change.md §3 — "the owner is
whichever document's question the information actually answers"):
  PASS — DATABASE.md states in its own header that it answers the persistent
  data contract, and §1 already owns every BattleResult persistence
  prerequisite (items 1-3, the fail-closed rule, the TASK-050 block). No
  competing owner exists; API_CONTRACTS.md §4 is the read-side contract and
  ARCHITECTURE.md §4 item 4 is the lifecycle, neither of which owns
  persistence prerequisites. No STOP.

ADR necessity (docs/03-decisions/README.md §2):
  PASS — NOT required. Decision A is a clarification of an existing contract
  already fully owned by a technical document; §2 says do not create an ADR
  for "one that duplicates content already fully owned by a technical
  document", and §1 states an ADR documents an already-made decision rather
  than creating one. The document itself already set this precedent for the
  same section (v1.12: "this document is the canonical owner of the contract
  and no ADR is required").

git grep -i -E "combat.ready|combat-readiness|battle.ready|duelable" -- docs/
  -> PASS before edit (0 matches) and after edit: the only new occurrences
     are the intentionally negative clause in DATABASE.md §1 + its header
     note — no readiness concept is defined anywhere.
git grep -i "readiness" -- docs/  -> only the new DATABASE.md clause and
  header note (negative statements), plus nothing elsewhere
git log -S"IsCombatReady" --all --oneline   -> PASS (empty)
git log -S"battle-ready"  --all --oneline   -> PASS (empty)

Preserved Decisions (§7) re-grepped, all unchanged:
  write-then-delete ordering (ARCHITECTURE.md §4 item 4, REDIS_STATE.md §3)
  unresolved-BossDefinition fail-closed rule (DATABASE.md §1)
  RewardSummary = {} (DATABASE.md §1)
  BattleResultId = BattleId; PlayerId = BattleState.PlayerId;
    PetInstanceId = BattleState.PetState.PetId
  DurationTurns / CompletedAt / Outcome (TASK-050)
  no Status/lifecycle field on BattleState (GAME_STATE.md §2.0.3)
  Player has no combat pool; Player Level carries no combat stats
  -> PASS (none altered; the new clause cites them as unchanged)

Scope checks:
  src/ + tests/ + migrations: ZERO changes (git status)          -> PASS
  TASK-041 file: byte-identical, never edited                    -> PASS
  TASK-032 / TASK-033 / TASK-036: byte-identical, untouched      -> PASS
  No source code changed; no implementation performed            -> PASS
```

### Documentation consistency validation (`quality/documentation-consistency`)

```text
[ ] New rule stated exactly once, in the canonical owner            -> PASS
    (one clause, DATABASE.md §1; TASK-056 and TASK-057 reference it and
     restate no rule — no duplication, documentation-change.md §2)
[ ] Dependent references checked for staleness                       -> PASS
    API_CONTRACTS.md §3/§4, ARCHITECTURE.md §4 item 4, REDIS_STATE.md §3,
    GAME_STATE.md §2.0.3/§2.8: none describes a battle-end readiness
    condition, so none became stale; no dependent edit required
[ ] No duplicated definition introduced                              -> PASS
[ ] No authoritative rule violated (AGENTS.md §10, ADR-001)          -> PASS
[ ] MVP scope respected (MVP_SCOPE.md §1)                            -> PASS
    (nothing added to scope; a non-existent system was not introduced)
[ ] Player schema unchanged (DATABASE.md §1 Player block)            -> PASS
[ ] No architecture / wire / event / state contract changed          -> PASS
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code changed)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no gameplay rule invented (`AGENTS.md` §7) — the clause is
      negative (it removes a prerequisite); it defines no predicate and
      authorizes no replacement
- [x] Confirmed TASK-041 / TASK-032 / TASK-033 / TASK-036 untouched
- [x] Confirmed no ADR created (not required — see above)
- [x] Confirmed the reconciliation task is created but **not implemented**

### Execution 1 (2026-09-27) — outcome: BLOCKED (stop condition §12.1)

### Changed Files
- `tasks/backlog/TASK-056-resolve-combat-readiness-battle-result-persistence-contract.md`
  — §13 STOP CONDITION report issued; §13.1 TASK-041 impact; §13.2 proposed
  follow-up task (not created); §13.3 report-only items; §8 Acceptance
  Criteria annotated with the execution outcome; this §14 record; Revision
  History 1.1. **No other file changed.**

### Validation Results (re-verified against the current tree by search)

```text
git grep -i -E "combat.ready|combat-readiness|battle.ready|duelable" -- docs/
  -> PASS (0 matches; exit 1) — no readiness concept in any authoritative doc
git grep -i "readiness" -- docs/
  -> PASS (0 matches; exit 1) — the word occurs only in tasks/ prose
git log -S"IsCombatReady" --all --oneline   -> PASS (empty)
git log -S"battle-ready"  --all --oneline   -> PASS (empty)
DATABASE.md:427 "An unresolved BossDefinition fails the battle-end write
  closed." -> PASS (re-read: BossDefinition lookup only; no readiness clause)
DATABASE.md §1 Player block (line 84) -> PASS (four columns; no readiness
  field)
ARCHITECTURE.md §4 item 4 / REDIS_STATE.md §3 -> PASS (write-then-delete
  order preserved and unmodified)
API_CONTRACTS.md §3 (start-time validation) vs §4 (200/404/401) -> PASS
  (distinct surfaces; not conflated in the report)
git status --porcelain -> PASS: no src/, tests/, migration, docs/, or
  existing task file modified by this execution; TASK-041's uncommitted
  working tree intact (not committed, not reset, not discarded)
Preserved Decisions (§7) -> PASS: unchanged (no document was edited; the
  report restates none of them as a new rule)
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code changed)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no readiness
      system proposed or added; unlisted content remains FUTURE
- [x] Confirmed no gameplay rule invented (`AGENTS.md` §7) — no predicate,
      no threshold, no forbidden answer chosen
- [x] Confirmed TASK-041 / TASK-032 / TASK-033 / TASK-036 untouched
- [x] Confirmed no ADR drafted (whether one is required is part of the
      unanswered decision — drafting it would invent the decision)
- [x] Confirmed decision/architecture/ownership scope of this task only
      (`AGENTS.md` §16); four unrelated findings reported (§13.3), not fixed

### Post-decision template — SUPERSEDED

The placeholder that stood here was filled by the Execution 2 record above;
it is retained only as a marker that Execution 1's template was completed
rather than left unfilled (`core/completion.md` §3).

---

## Revision History

| Rev | Date | Summary |
|---|---|---|
| 1.2 | 2026-09-27 | **Resolved — `Status: DONE`.** Human Decision A = **NO** (no Player combat-readiness prerequisite for `BattleResult` persistence) and Decision C = `IMPLEMENTATION / CONTRACT-MISMATCH GAP` recorded verbatim in §13.4. Contract recorded once in the canonical owner `docs/02-technical/DATABASE.md` §1 (+ header v1.14) — the smallest appropriate authoritative location, with the duplicate item-3 numbering deliberately preserved and nothing renumbered. **No ADR created:** `docs/03-decisions/README.md` §2 excludes an ADR that duplicates content already fully owned by a technical document, and the document itself set that precedent for the same section at v1.12. Reconciliation implementation task **TASK-057 created** (not implemented) per §13.2's NO branch. §13 "Waiting for" closed; §13.5 records the disposition. No `src/`, `tests/`, migration, or existing task file changed; TASK-041 / TASK-032 / TASK-033 / TASK-036 byte-identical. |
| 1.1 | 2026-09-27 | Executed (decision task). §2/§5 evidence re-verified against the current tree by search: `git grep -i -E "combat.ready\|combat-readiness\|battle.ready\|duelable" -- docs/` and `git grep -i "readiness" -- docs/` both return **0 matches**, `git log -S"IsCombatReady"` and `git log -S"battle-ready"` both empty. `DATABASE.md` §1 item 3 (line 427) confirmed to contain only the unresolved-`BossDefinition` fail-closed rule — no Player readiness clause. Decision A therefore **not derivable**; stop condition §12.1 fired and the `.ai/README.md` §13 STOP CONDITION report was issued in §13, naming Decision A (A1–A6 / B1–B3) and Decision C as the exact missing input. §13.1 records the TASK-041 reconciliation requirement (read-only; TASK-041 not modified); §13.2 records the proposed follow-up implementation task (**not created** — §9 scopes `tasks/` to this file, and its content is undecidable before Decision A); §13.3 records four report-only items. No predicate chosen, no forbidden answer selected. `Status` remained `BLOCKED — HUMAN GAMEPLAY/DOMAIN DECISION REQUIRED`; no `docs/`, `src/`, `tests/`, or existing task file changed (only this file). |
| 1.0 | 2026-09-27 | Created (BLOCKED) from the TASK-041 post-completion audit. `git grep docs/` and `git log -S` confirm no authoritative definition of combat/battle readiness exists in any document or in history; `DATABASE.md` §1 item 3 (line 427) contains only the unresolved-`BossDefinition` fail-closed rule and no readiness clause; `DATABASE.md` has a duplicate item 3 (line 460, RewardSummary). Decision A and Decision C marked HUMAN GAMEPLAY/DOMAIN DECISION REQUIRED — no answer pre-filled, no source, test, or existing task file touched. |
