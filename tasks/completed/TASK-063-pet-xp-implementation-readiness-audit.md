# TASK-063 — Pet XP Implementation-Readiness Audit

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an auditor
  needs to know where to look.

  THIS TASK AUDITS; IT DOES NOT IMPLEMENT.

  It answers one question: can the finalized Pet XP contract be implemented
  as a small deterministic progression change using the EXISTING
  architecture, without a new gameplay, architecture, persistence, or API
  decision?

  It does NOT implement Pet XP. It does NOT create the implementation task.
  A separate implementation task may be generated ONLY after this audit
  passes.
-->

---

## Metadata

```text
Task ID:           TASK-063
Type:              ARCHITECTURE
Status:            DONE
Risk:              MEDIUM (read-only audit — no code, no docs, no schema is
                   modified. tasks/TASK_TYPES.md §4 sets ARCHITECTURE at HIGH
                   "always"; lowered to MEDIUM because the deliverable is a
                   readiness FINDING, not an architecture change: this task
                   introduces no architectural decision and its output is
                   either "existing architecture suffices" or a STOP report.
                   Risk returns to HIGH if the audit finds a genuine
                   architecture decision is required — in which case this
                   task STOPS and reports rather than designing one.)
Priority:          HIGH (it is the mandatory gate between the finalized
                   contract and any Pet XP implementation task)
Primary Agent:     orchestrator (read-only cross-boundary audit — the task
                   spans Domain, Application, Infrastructure, Api, and tests;
                   no single specialist owns it, and none may change code
                   during an audit)
Supporting Agents: backend (reward-flow + battle-end integration point),
                   persistence (Pet XP column/constraint/migration readiness
                   and transaction boundary in DATABASE.md terms),
                   gameplay (contract conformance of the XP/Level rules),
                   testing (test-infrastructure readiness),
                   review (finding classification + STOP reporting)
Workflow:          architecture/architecture-change.md §1–§2
                   (readiness/impact analysis up to the decision gate; this
                   task stops at the gate and does not proceed to
                   implementation)
                   + core/validation.md (architecture validation layer)
                   + quality/review.md (finding classification)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/architecture-conformance,
                   quality/scope-validation,
                   backend/persistence-analysis,
                   gameplay/gameplay-behavior-derivation,
                   testing/test-scenario-generation
                   (7 skills — Complex budget ceiling is 7, tasks/README.md §12)
Dependencies:      TASK-059 (DONE — two-track structure + frozen Player
                   contract; read-only, must NOT be modified),
                   TASK-061 (DONE — Product Owner decision record; read-only),
                   TASK-062 (DONE — finalized the Pet XP contract into
                   docs/; read-only),
                   TASK-023 (DONE — Player persistence; read-only),
                   TASK-024 (DONE — the retired PetLevelMultiplier
                   derivation; read-only historical context, must NOT be
                   modified and must NOT be treated as a Pet XP source)
Blocks:            the Pet XP implementation task (generated only after this
                   audit PASSES — NOT created by this task)
Estimate:          Complex (7 skills; read-only audit; zero code, zero doc
                   changes, zero schema changes; output is a PASS/STOP
                   finding plus the information the implementation task needs)
```

**Type classification note.** `ARCHITECTURE`, not `DOCUMENTATION`.
`tasks/TASK_TYPES.md` §2 defines `ARCHITECTURE` as the type used when a task
"changes something `docs/02-technical/ARCHITECTURE.md` or
`docs/02-technical/TDD.md` currently governs, or requires a new/updated ADR",
and §3's guide maps "the architecture / technology / ADR needs to change" to
it. This task's actual subject is precisely whether the finalized contract
**fits the existing architecture without an architecture change** — an
architecture-conformance question, not a documentation edit. It is not
`DOCUMENTATION`: it changes no document. It is not `FEATURE`: it builds
nothing. §4 sets ARCHITECTURE at HIGH "always", but the risk notes above
justify MEDIUM for a read-only audit whose deliverable is a finding; the task
escalates itself to a STOP if a real architectural decision surfaces.

**Lifecycle-state note (observed, not repaired).** At creation time the
repository shows `TASK-059`, `TASK-061`, and `TASK-062` still physically in
`tasks/backlog/` with `Status: BACKLOG` in their metadata, although their
Completion Evidence records them as complete, and `TASK-060` still sits in
`tasks/blocked/`. Per `TASK_LIFECYCLE.md` §4 an `IN REVIEW → DONE` transition
moves the file to `tasks/completed/`. **This task does not repair that** —
moving or re-statusing completed tasks is a lifecycle action for the
orchestrator/requester, and `AGENTS.md` §16 forbids this task from editing
task files outside its own scope. It is recorded as **Finding F0** in §7
because a downstream implementation task must be generated against the
correct lifecycle state. Do not treat the stale `Status:` field as evidence
that TASK-062 is unfinished: its §15 Completion Evidence is the record of
completion, and the audit must read both.

---

## 1. Objective

Determine whether the finalized Pet XP contract can now be implemented as a
**small, deterministic progression change using the existing architecture** —
with no new gameplay decision, no new architecture decision, no new
persistence-strategy decision, no new API-contract decision, and no
undocumented behavior.

The audit answers, with citable evidence:

```text
Can Pet XP now be implemented as a small deterministic feature without
introducing a new gameplay decision, architecture decision, persistence
decision, API contract decision, or undocumented behavior?
```

**This task does NOT implement Pet XP.**
**This task does NOT create the Pet XP implementation task.**
**A separate implementation task may be generated only after TASK-063
passes.**

If the audit passes, its Completion Evidence must contain the information a
task generator needs to write that implementation task. If it fails, the
Completion Evidence must identify the **smallest** blocking
documentation/architecture task needed.

---

## 2. Final Pet XP Contract (FIXED INPUT — do not reopen)

Treat every value below as already decided by the Product Owner and already
recorded in the owning documents by TASK-062. **Do not reopen, reinterpret,
or re-derive any of it.** The audit's job is conformance, not design.

```text
Pet XP ownership:        PlayerPet / Pet instance
Initial Pet XP:          0
Initial Pet Level:       1
Pet Level range:         [1, 50]
BattleWon:               active combat Pet +100 Pet XP
BattleLost:              active combat Pet +0 Pet XP
Inactive owned Pets:     +0 Pet XP
Passive/shared/party/account-wide Pet XP:   none
Pet Level formula:       min(floor(Pet.XP / 100) + 1, 50)
Pet XP hard cap:         4900
After 4900:              additional Pet XP is not awarded / not stored
Overflow:                none
Player/Pet progression:  independent XP pools
PetLevelMultiplier:      RETIRED
```

Player XP is **frozen** and out of this task's authority:

```text
Player BattleWon:        +100 XP
Player BattleLost:       +0 XP
Player XP:               uncapped
Player Level formula:    min(floor(XP / 100) + 1, 50)
TASK-059:                frozen — must NOT be modified
```

Owner sections for the contract: `docs/01-game-design/PET_RULES.md` §5.1–§5.5
(§5.1 ownership, §5.2 initial values, §5.3 reward targeting, §5.4 formula,
§5.5 cap/post-cap).

---

## 3. Authoritative References

- `docs/01-game-design/PET_RULES.md` **§5.1–§5.5** — the finalized contract
  (primary input); §5.6 retired terms; §1 data model; §6 stat composition
- `docs/01-game-design/COMBAT_RULES.md` §7 — the frozen Player track; §7.3
  reward-amount vs. curve-constant
- `docs/01-game-design/GAME_RULES.md` §9.3 (Pet rule), §12 (Power),
  §17 (resolution order), §18 (server authority), §20 (Rule Change Policy)
- `docs/02-technical/DATABASE.md` §1 (`Pet` / `Player` entities, `Pet.XP`,
  `Pet.Level`, `BattleResult`, "Reward semantics for `RewardSummary`"),
  §3 constraints, §5 what-is-not-here
- `docs/02-technical/API_CONTRACTS.md` §4 notes 1/3 (`rewards` both
  outcomes; event-vs-REST reconciliation)
- `docs/02-technical/GAME_EVENTS.md` §2 (`BattleWon` / `BattleLost` payload
  rule + reward-summary item)
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState`, `PetId`),
  §2.8 (`BattleState.PlayerId`) — the active-combat-Pet identity contract
- `docs/02-technical/ARCHITECTURE.md` §1–§5 (module/layer boundaries,
  anti-overengineering notes), `docs/02-technical/TDD.md` §4 (persistence
  boundary), §6 (RNG — check whether Pet XP touches it)
- `docs/02-technical/REDIS_STATE.md` — for the §14 question only
- `docs/00-overview/MVP_SCOPE.md` §1 (Pet progression IN), §2 (OUT), §4
  (FUTURE by default)
- `docs/00-overview/GDD.md` §6, §14
- `docs/03-decisions/README.md` §3, §5, §7; `ADR-016` (+ its TASK-062
  Amendment), `ADR-011`, `ADR-012`
- `AGENTS.md` §6, §9, §10, §12, §13, §16, §17, §18, §20

Historical context only — **read, never treat as a Pet XP source, never
modify**: `tasks/completed/TASK-024-*`.

### Starting evidence already gathered at task creation

Recorded so the audit confirms rather than rediscovers. **Re-verify each;
a stale premise is itself a finding.**

```text
src/backend/GameServer.Domain/Pets/Pet.cs
    — no XP member; Level comment explicitly says "There is no XP column
      and no path that adds to this value".
src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs
    — the retired Player.Level × PetLevelMultiplier derivation still exists.
src/backend/GameServer.Application/Pets/PetLevelService.cs
    — the retired recompute service still exists and is DI-registered.
src/backend/GameServer.Infrastructure/Postgres/Configurations/PetConfiguration.cs
    — "There is no XP column"; CK_Pet_Level_Range check constraint.
src/backend/GameServer.Domain/Players/Player.cs
    — no XP member; Level is init-only; MinLevel/MaxLevel/InitialLevel.
src/backend/GameServer.Application/Battle/BattleResultService.cs
    — battle-end path; `state.PetState.PetId.Value` is already resolved
      there as `petInstanceId`; RewardSummary is the constant `"{}"`.
src/backend/GameServer.Domain/Battle/PetState.cs
    — `PetId` is the owned Pet instance identity (GAME_STATE.md §2.3).
```

The central open question these imply: **`Player.XP` does not exist in the
entity model while `PET_RULES.md`/`COMBAT_RULES.md`/`DATABASE.md` now
document it as a persisted column.** Determining whether that is in-scope
for Pet XP implementation, or a separate Player-track gap, is a required
audit answer (§4.1, §4.8).

---

## 4. Audit Scope — Questions the Task Must Answer

Every question must be answered with **file + line/section evidence** and one
of these classifications:

```text
ALREADY SUPPORTED      — existing code/contract already satisfies it
REQUIRES IMPLEMENTATION — a code/schema change is needed (describe it)
DOCUMENTATION-ONLY     — docs and code disagree, docs are correct
AMBIGUOUS              — cannot be determined; triggers a STOP (§8)
```

### 4.1 Pet persistence

- Does the owned-Pet persistence model already have fields for **Pet XP** and
  **Pet Level**? Verify `DATABASE.md` §1 against the actual entity +
  EF configuration.
- Determine exactly what is missing: new column, column modification, new
  constraint, new migration — or nothing.
- Does `PET_RULES.md` §5.5's 4900 XP cap imply a DB-level check constraint,
  or is it an application-layer invariant? Report which the contract
  requires; do not design it.
- **Do NOT implement any of it.**

### 4.2 Pet instance ownership

- Confirm XP attaches to **`PlayerPet` / the Pet instance**
  (`src/backend/GameServer.Domain/Pets/Pet.cs`), **not** `PetDefinition`.
- Confirm no existing architecture would accidentally store Pet XP globally,
  on static definition content, or on `Player`.
- Do not redesign the ownership model.

### 4.3 Pet Level derivation and the retired mechanism

- Determine whether the current `Pet.Level` is persisted, derived, or
  calculated — and what the finalized contract requires it to become.
- **Detect every remaining use of `PetLevelMultiplier` in ACTIVE
  implementation code** (Domain / Application / Infrastructure / Api) and in
  active tests, and classify each as REQUIRES IMPLEMENTATION.
- Historical documentation references explicitly marked retired/superseded
  (ADR-011/ADR-012 supersession notes, ADR-016 historical context, version
  changelogs) are **not** implementation blockers.
- Determine whether `PetLevelDerivation` / `PetLevelService` must be removed,
  replaced, or re-sourced — without designing the replacement.

### 4.4 Battle result / reward flow

Trace the existing flow end-to-end and identify the integration point:

```text
BattleWon / BattleLost
        ↓
reward calculation
        ↓
Player XP
        ↓
RewardSummary
        ↓
persistence / response
```

Answer explicitly:
- Is there already a **canonical battle reward calculation point**?
  (Candidate: `src/backend/GameServer.Application/Battle/BattleResultService.cs`.)
- Is the **active combat Pet identity available there**?
- Can the active Pet be identified **deterministically**?
- Is the reward flow **server-authoritative**?
- Does the current architecture support adding Pet XP **without creating a
  new subsystem**?

### 4.5 Active combat Pet identity (critical prerequisite)

- Trace `BattleState` → `PetState` → `PetId` → owned `Pet` instance and
  confirm that **"one active combat Pet per battle"** is already represented
  deterministically.
- Confirm the identity is **server-derived** and not taken from arbitrary
  client input.
- The contract requires exactly one recipient. If the implementation lacks
  enough information to identify that Pet → **STOP** (§8). **Do not invent a
  new identity mechanism.**

### 4.6 XP cap and Level calculation

- Determine whether reusable progression logic exists, and whether the
  existing **Player** XP calculation can safely share the **formula shape**
  without sharing the XP pool.
- Confirm the contract's asymmetry is representable: **Pet XP cap = 4900**
  while **Player XP remains uncapped**.
- **Do not create a generic abstraction** merely because both curves share a
  formula: no `UniversalProgressionService`, `ExperienceManager`, or
  `GenericLevelSystem` unless the existing architecture already requires one
  (`AGENTS.md` §9, `ARCHITECTURE.md` §5).

### 4.7 RewardSummary / API / events

- Determine whether the finalized contract requires changes to
  `RewardSummary`, `BattleResult`, the `BattleWon` event, or the
  `BattleLost` event — and classify each as already supported / requires
  implementation / documentation-only / ambiguous.
- Where the Pet member list is deferred to implementation
  (`DATABASE.md` §1), report what the implementation task must define, and
  confirm it is a **representation** decision, not a gameplay one.
- **Do not invent new API fields or events.**

### 4.8 Transaction / consistency

- Inspect the existing battle-result and reward persistence flow and
  determine whether Pet XP can be updated **atomically** with the
  corresponding battle result / reward operation.
- Identify, **only to the extent the architecture already defines them**:
  transaction boundary, `SaveChanges` boundary, concurrency behavior, and
  duplicate-reward risk.
- **Do not invent new transaction semantics.** Do not introduce Redis or
  distributed locking.
- If the existing architecture does not define sufficient semantics to award
  Pet XP **exactly once** → **STOP** and report the missing contract.

### 4.9 Player XP protection

- Verify that Pet XP can be added **without** modifying: Player XP
  `+100`/win, Player XP uncapped behavior, the Player Level formula, or
  TASK-059.
- Flag **any** existing code or documentation that would require changing the
  Player track. Such a finding is a **blocker** for implementation.
- Determine whether the missing `Player.XP` entity member (§3 starting
  evidence) is (a) required by the Pet XP task, (b) a separate pre-existing
  Player-track gap, or (c) ambiguous. Report which; do not resolve it.

### 4.10 Test readiness

- Determine whether implementation can be covered by **existing test
  patterns** for: Pet XP initial state, reward, loss, active-Pet targeting,
  inactive-Pet exclusion, the 4900 XP cap, no overflow, level calculation,
  Player/Pet XP independence, and battle-reward integration.
- Identify existing suitable test locations, missing test infrastructure, and
  integration-test feasibility.
- **Do NOT add or modify tests.**

### 4.11 Frontend scope

- Verify whether Pet XP requires **any** frontend change. It should not,
  unless an authoritative API/runtime contract explicitly requires Pet XP to
  be exposed there.
- Confirm no Phaser/React/`GameRuntime`/`BattleScene` change is required.
  **Do not create frontend requirements merely to display XP.**

### 4.12 Redis / SignalR

- Determine whether Pet XP belongs to **persistent `PlayerPet` progression**
  rather than **active `BattleState`**, per the authoritative documents.
- **Do not add Pet XP to `BattleState`** merely because it is gameplay state.
- **Do not invent SignalR events or Redis keys.**

---

## 5. Required Acceptance Criteria

All binary; each cites what must contain the evidence.

```text
[ ] Pet XP ownership is confirmed as PlayerPet / Pet instance (§4.2)
[ ] Existing persistence representation is identified (§4.1)
[ ] Required XP/Level persistence changes, if any, are identified (§4.1)
[ ] Active combat Pet identity is confirmed deterministic (§4.5)
[ ] BattleWon/BattleLost reward integration point is identified (§4.4)
[ ] Active Pet receives exactly the finalized reward (§4.4, §4.5)
[ ] Inactive Pets receive no reward (§4.3/§4.4 conformance)
[ ] Pet XP cap / overflow implementation requirements are identified (§4.6)
[ ] Pet Level calculation requirements are identified (§4.3, §4.6)
[ ] Player XP +100/win is confirmed untouched (§4.9)
[ ] Player XP remains uncapped (§4.9)
[ ] Pet XP and Player XP remain independent (§4.6, §4.9)
[ ] PetLevelMultiplier is confirmed not required by active implementation (§4.3)
[ ] RewardSummary / API implications identified WITHOUT inventing fields (§4.7)
[ ] Persistence / transaction implications identified (§4.8)
[ ] Existing test infrastructure judged sufficient, or gaps explicitly reported (§4.10)
[ ] No frontend changes required unless authoritative contracts require them (§4.11)
[ ] No Redis / SignalR changes invented (§4.12)
[ ] No new architecture decision is required (§1, §8)
[ ] No gameplay decision remains unresolved (§2, §8)
[ ] Every §4 question carries a classification and file+line evidence
[ ] The audit verdict is stated as PASS or STOP, with the exact reason
[ ] No source code, test, migration, or documentation file was modified
[ ] The Pet XP implementation task was NOT created
```

---

## 6. Affected Areas (read-only)

```text
[ ] src/backend/GameServer.Domain/          (Pets/, Players/, Battle/)   READ ONLY
[ ] src/backend/GameServer.Application/     (Pets/, Battle/, Players/)   READ ONLY
[ ] src/backend/GameServer.Infrastructure/  (Postgres/, Migrations/)     READ ONLY
[ ] src/backend/GameServer.Api/             (Controllers/, Auth/)        READ ONLY
[ ] src/frontend/client/                                                 READ ONLY
[ ] tests/                                                               READ ONLY
[ ] docs/                                  (conformance reference)       READ ONLY
[ ] tasks/                                 (this file only)
```

**No file in the first six rows may be created, modified, or deleted.**

---

## 7. Required Findings Format

Report each finding as:

```text
F<n> · <one-line title>
  Question:     §4.<n> <the audit question>
  Classification: ALREADY SUPPORTED | REQUIRES IMPLEMENTATION |
                  DOCUMENTATION-ONLY | AMBIGUOUS
  Evidence:     <file>:<line/section> — <what it shows>
  Impact:       <what the implementation task must do, or "none">
  Blocking?:    YES | NO  (YES ⇒ §8 STOP)
```

Register at minimum:

```text
F0  Lifecycle-state inconsistency of TASK-059/060/061/062 (see Metadata note)
F1  §4.1  Pet XP / Pet Level persistence gap
F2  §4.3  PetLevelMultiplier / PetLevelDerivation / PetLevelService in active code
F3  §4.5  active combat Pet identity determinism
F4  §4.4  battle reward integration point
F5  §4.6  cap asymmetry (Pet 4900 vs Player uncapped) representability
F6  §4.7  RewardSummary Pet member list deferral
F7  §4.8  transaction boundary / duplicate-reward risk
F8  §4.9  Player.XP entity-member gap (in-scope vs separate)
F9  §4.10 test-infrastructure readiness
F10 §4.11 frontend scope
F11 §4.12 Redis / SignalR scope
```

---

## 8. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always
apply. **This task STOPS rather than declaring implementation-ready if any of
the following is true.** On STOP: write the STOP CONDITION report in §11, set
the verdict to STOP, and identify the smallest blocking follow-up task.
**Do not resolve the condition inside TASK-063.**

- Pet XP ownership is ambiguous.
- `PlayerPet` persistence semantics are ambiguous.
- Active combat Pet identity cannot be determined deterministically.
- The battle reward integration point is ambiguous.
- Reward application requires undocumented transaction semantics.
- Pet XP cap behavior conflicts with the authoritative documents.
- The Pet Level formula conflicts with the authoritative documents.
- Player XP would need modification.
- `PetLevelMultiplier` is still required by active implementation code.
- `RewardSummary` requires an undocumented API field.
- `BattleState` would need an undocumented Pet XP field.
- Redis persistence semantics are required but undefined.
- SignalR behavior is required but undefined.
- A new architecture decision is required.
- A gameplay decision must be invented.
- Authoritative documents remain contradictory.
- The existing implementation reveals that Pet XP cannot be implemented as a
  small isolated progression change.

Task-specific additional conditions:

- **If the audit cannot determine its verdict from evidence** → STOP; report
  which question is unanswerable and why.
- **If answering a §4 question requires modifying code or docs to find out**
  → STOP; an audit may read, not change.
- **If a §2 value appears contradicted by an authoritative document** → STOP;
  report the exact conflict (`AGENTS.md` §4). Do not choose an interpretation.
- **If the audit finds itself designing the implementation** → STOP;
  designing is the next task's job, not this one's.
- **If the task expands beyond the §4 questions or the §6 read-only areas** →
  STOP; report it, do not apply it (`AGENTS.md` §16).

---

## 9. Validation Requirements

This is a **read-only audit**: validation is by inspection, cross-reference,
and search — not test execution. **Do not run implementation tests**
(`core/validation.md` §1 — architecture/documentation validation layers).

### Required Verification
```text
[ ] Every §4 question is answered with a classification + file:line evidence
[ ] Every acceptance criterion in §5 is addressed
[ ] The verdict (PASS / STOP) is explicit and justified
[ ] Any STOP condition that fired is reported in §11's exact format
[ ] The smallest blocking follow-up task is named if the verdict is STOP
[ ] The implementation-task input set is complete if the verdict is PASS
```

### Static Searches (read-only)
```text
[ ] PetLevelMultiplier in src/ and tests/ — enumerate every ACTIVE hit;
    classify each; confirm historical doc references are not blockers
[ ] PetLevelDerivation / PetLevelService — enumerate active references
    (incl. DI registration in DependencyInjection.cs)
[ ] "no XP column" / "There is no XP column" in src/ — enumerate
[ ] \bXP\b in Domain/Players/Player.cs and Domain/Pets/Pet.cs — confirm
    presence/absence of an XP member
[ ] PetState/BattleState members — confirm no Pet XP field is present or
    required (§4.12)
[ ] RewardSummary / EmptyRewardSummary references in src/ — confirm the
    current constant and where the member list would be produced
[ ] SaveChanges / transaction usage on the battle-end path — confirm the
    defined boundary (§4.8)
[ ] Player XP reward value in src/ and tests/ — confirm no "50" exists and
    that +100 assumptions are not contradicted
[ ] Confirm `git status --porcelain` shows NO change under src/, tests/, or
    docs/ attributable to this task (the task modifies only its own file)
```

### Evidence Quality Bar
- A finding without a `file:line` (or doc section) citation is not a finding.
- "Looks fine" is not a classification; use the §4 vocabulary.
- Distinguish **absent** (feature does not exist) from **contradictory**
  (exists but conflicts with the contract) — they imply different follow-up.

---

## 10. Relationship to Prior Tasks and the Implementation Task

```text
TASK-059 (DONE)   two-track structure + frozen Player contract
        ↓
TASK-061          Product Owner decision record (the twelve values)
        ↓
TASK-062 (DONE)   finalized the Pet XP contract into authoritative docs/
        ↓
TASK-063 (this)   IMPLEMENTATION-READINESS AUDIT  →  PASS / STOP
        ↓
        PASS ⇒ the Pet XP implementation task may be generated (NEW ID)
        STOP ⇒ the smallest blocking follow-up task is generated instead
```

- **This task does NOT implement Pet XP.**
- **This task does NOT create the Pet XP implementation task.**
- **A separate implementation task may be generated only after TASK-063
  passes.**
- **TASK-059, TASK-061, and TASK-062 are historical evidence.** Do not modify,
  reopen, or alter their Completion Evidence (`AGENTS.md` §16,
  `TASK_LIFECYCLE.md` §3 DONE → any invalid).
- **TASK-060** remains BLOCKED and is not modified by this task.

---

## 11. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AUDITOR.
  Keep concise and factual. Cite file:line for every finding.
-->

### Verdict

```text
STOP
```

### Executive Result

```text
NOT-IMPLEMENTATION-READY
```

Two independent blocking conditions were proven from source, neither of which
may be resolved inside this audit (§8, §10 of the task):

```text
BLOCKER 1  Active implementation still depends on the retired
           PetLevelMultiplier mechanism (PET_RULES.md §5.6 item 1, ADR-016
           item 13). PetLevelService is DI-registered and its only two
           methods are built entirely on PetLevelDerivation.Derive(
           Player.Level, PetDefinition.PetLevelMultiplier). The retired model
           is therefore not merely residue — it is the ONLY Pet Level
           computation the Application layer owns.

BLOCKER 2  Player.XP does not exist anywhere in source, while the frozen
           Player contract (COMBAT_RULES.md §7) and DATABASE.md §1/§3 require
           it as a persisted column with a documented reward path. Pet XP
           implementation would have to either (a) build the Player-XP
           persistence/reward path it does not own, or (b) implement Pet XP
           with no Player-XP counterpart, leaving the documented Player track
           permanently unimplemented behind it.
```

**Pet XP is NOT blocked by the battle-end architecture.** Audits #4, #5, #7,
#8, #11 and #12 all PASS: the active combat Pet identity is already
deterministic and server-authoritative at the canonical reward integration
point, the transaction boundary is defined, and no frontend/Redis/SignalR
contract is missing. The blockers are a **retired-mechanism dependency** and
an **unfinished Player-track prerequisite** — both upstream of Pet XP, and
neither within this task's authority to fix.

### Findings

```text
F1  · Pet XP / Pet Level persistence is ABSENT from the owned-Pet model
    Question:       §4.1
    Classification: REQUIRES IMPLEMENTATION
    Evidence:       src/backend/GameServer.Domain/Pets/Pet.cs:39-112 —
                    members are PetInstanceId, PlayerId, PetDefinitionId,
                    Tier, Star, Level, AcquiredAt. No XP member exists.
                    src/backend/GameServer.Infrastructure/Postgres/
                    Configurations/PetConfiguration.cs:28-32 — "Level is a
                    stored snapshot, not an independent store. ... There is
                    no XP column"; :101-112 maps Level with
                    CK_Pet_Level_Range (>= Player.MinLevel AND
                    <= Player.MaxLevel).
                    grep -n "\bXP\b" over src/ → 0 matches.
    Impact:         The implementation needs a new persisted Pet.XP column
                    (int, NOT NULL, default 0, range [0, 4900]) and a new EF
                    migration. The existing CK_Pet_Level_Range already
                    expresses [1, 50] and needs no change. Pet.Level's
                    documented meaning must change from "snapshot of
                    Player-derived value" to "function of this Pet's own XP".
    Blocking?:      NO — this is ordinary implementation work.

F2  · BLOCKER — active code still depends on the retired PetLevelMultiplier
    Question:       §4.3
    Classification: BLOCKER
    Evidence:       src/backend/GameServer.Application/DependencyInjection.cs:85
                    — services.AddScoped<PetLevelService>() (ACTIVE
                    registration).
                    src/backend/GameServer.Application/Pets/PetLevelService.cs:86
                    and :123 — the ONLY two assignments of Pet.Level in the
                    codebase, both `PetLevelDerivation.Derive(player.Level,
                    definition.PetLevelMultiplier)`.
                    src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs:69
                    — public static int Derive(int playerLevel, decimal
                    petLevelMultiplier).
                    src/backend/GameServer.Domain/Pets/PetDefinition.cs:90 —
                    public decimal PetLevelMultiplier { get; init; }
                    src/backend/GameServer.Infrastructure/Postgres/
                    Configurations/PetDefinitionConfiguration.cs:76-82 —
                    mapped column + CK_PetDefinition_PetLevelMultiplier_Positive.
                    Migration 20260924163011_AddPetPersistence.cs:21,28 —
                    PetLevelMultiplier column numeric(9,4) NOT NULL + the
                    check constraint.
                    Nuance proven: PetLevelService has NO production
                    consumer — grep for `PetLevelService` /
                    `RecomputeForPlayerAsync` / `RecomputeForDefinitionAsync`
                    across src/ returns only the type's own declaration, its
                    DI registration, and XML doc comments. It is registered
                    but never injected or invoked. It is therefore DORMANT
                    at runtime, not executed today.
                    Nevertheless it is ACTIVE, COMPILED, CONTAINER-REGISTERED
                    production code whose sole purpose is the retired
                    derivation, and PetDefinition still carries the retired
                    column.
    Impact:         The retired mechanism must be removed/replaced before or
                    alongside Pet XP implementation. Pet XP's Pet.Level must
                    come from Pet.XP, not from this service. This is the
                    §4.3 STOP: the audit must not delete it, and must not
                    fold the cleanup into Pet XP.
    Blocking?:      YES

F3  · BLOCKER — Player.XP is absent while documented as persisted
    Question:       §4.9 (raised via §3 starting evidence)
    Classification: BLOCKER (separate unfinished prerequisite)
    Evidence:       src/backend/GameServer.Domain/Players/Player.cs — members
                    are PlayerId, DiscordUserId, Level, CreatedAt; no XP
                    member. (grep -n "\bXP\b" src/ → 0 matches; therefore
                    "XP" exists in NO source file.)
                    src/backend/GameServer.Infrastructure/Postgres/
                    GameDbContext.cs:22-77 — 9 DbSets; Players and Pets
                    present, no XP-bearing set.
                    Migration 20260924130701_AddPlayerPersistence.cs — the
                    Player table is created without an XP column.
                    src/backend/GameServer.Application/Battle/
                    BattleResultService.cs:260-262 — RewardSummary is the
                    constant EmptyRewardSummary ("{}"); no XP is computed or
                    applied on the battle-end path at all.
                    Docs requiring it: docs/01-game-design/COMBAT_RULES.md
                    §7.2-§7.3 (Player XP +100/+0, persisted), 
                    docs/02-technical/DATABASE.md §1 (Player.XP int NOT NULL
                    default 0) and §3 (Player.XP >= 0, initial 0).
    Impact:         Answering the §10 questions from evidence: (1) Player XP
                    is NOT implemented anywhere; (2) it is NOT stored under
                    another field; (3) Player XP persistence IS unfinished;
                    (4) the documentation IS ahead of implementation; (5) Pet
                    XP does NOT depend on Player XP *infrastructure* — they
                    are independent pools with no shared storage or code;
                    (6) Pet XP CAN be implemented independently; (7) the
                    existing reward pipeline does NOT require Player XP to
                    function — it currently awards no XP to anyone.
                    Consequence: Pet XP is not mechanically blocked by the
                    Player gap, but implementing Pet XP while Player XP
                    remains unimplemented would leave the frozen Player
                    contract half-built and produce a repository where the
                    battle reward path grants Pet XP but no Player XP. That
                    is an ordering decision this audit must not make.
    Blocking?:      YES (as a sequencing prerequisite to be resolved by the
                    smallest separate follow-up task)

F4  · Active combat Pet identity is deterministic and server-authoritative
    Question:       §4.5
    Classification: ALREADY SUPPORTED
    Evidence:       src/backend/GameServer.Domain/Battle/PetState.cs:47-53,
                    330-351 — PetId is the owned Pet INSTANCE identity
                    (Pet.PetInstanceId), the `petId` submitted to
                    POST /api/battle/start; "never the display Identity
                    name". PetState.cs:347 — "supplies the identity of the
                    owned Pet it selected rather than letting one [be
                    inferred]".
                    src/backend/GameServer.Application/Battle/
                    BattleResultService.cs:188 — `var petInstanceId =
                    state.PetState.PetId.Value;` resolved at the battle-end
                    path; :191-194 rejects a null/empty identity.
                    DATABASE.md §1 sourcing item 2: PetInstanceId is copied
                    from BattleState.PetState.PetId, "never re-derived from
                    client input at battle end" (GAME_RULES.md §18, ADR-001).
    Impact:         The contract's "exactly one active combat Pet receives
                    the reward" is directly satisfiable: one PetState per
                    BattleState, identity server-derived and already in hand
                    at the exact point a reward would be applied. NO new
                    identity mechanism is needed. This is the single most
                    important PASS in the audit.
    Blocking?:      NO

F5  · Canonical battle reward integration point EXISTS
    Question:       §4.4
    Classification: ALREADY SUPPORTED (integration point), with the reward
                    computation itself REQUIRES IMPLEMENTATION
    Evidence:       src/backend/GameServer.Application/Battle/
                    BattleResultService.cs:179-295 —
                    PersistTerminalResultAsync(BattleState state,
                    BattleOutcome outcome) is the single battle-end entry
                    point. It resolves battleId (:186), playerId (:187),
                    petInstanceId (:188), bossIdentity (:189), takes the
                    `outcome` (:247), and writes via
                    _results.AddAsync(result) (:270).
                    Failure path :221-224 returns false (fail-closed);
                    exception path :267-270 propagates.
                    Current reward content: :260-262 — RewardSummary is the
                    constant "{}"; no XP is awarded.
    Impact:         Pet XP (+100 on BattleOutcome won, +0 on lost) belongs
                    at this same authoritative point, using the already-
                    resolved petInstanceId. No new reward subsystem, no new
                    service, and no BattleState change is required. The
                    reward VALUE computation is new work; the PLACE to put it
                    already exists.
    Blocking?:      NO

F6  · Pet XP cap / Level formula are implementable as plain arithmetic
    Question:       §4.6
    Classification: REQUIRES IMPLEMENTATION (trivial, no abstraction)
    Evidence:       No progression/XP formula exists in src/ (grep "\bXP\b"
                    → 0 matches). The only Level arithmetic in the codebase
                    is src/backend/GameServer.Domain/Pets/
                    PetLevelDerivation.cs (the RETIRED derivation) and
                    src/backend/GameServer.Domain/Players/Player.cs:36-58
                    (MinLevel=1, MaxLevel=50, InitialLevel=1 — constants
                    only, no XP math).
                    Contract check, edge cases evaluated against
                    min(floor(XP/100)+1, 50):
                      XP 0    → 1     XP 99   → 1     XP 100  → 2
                      XP 4899 → 49    XP 4900 → 50    XP >4900 → 50
                    (unreachable: stored XP is capped at 4900)
    Impact:         A single pure function suffices, mirroring the retired
                    PetLevelDerivation's shape but reading Pet.XP. The 4900
                    cap is enforced on WRITE (award → min(4900, XP+100)), not
                    by the formula. NO generic progression abstraction is
                    warranted (AGENTS.md §9, ARCHITECTURE.md §5): the Player
                    track has no XP code to share with, so a shared
                    UniversalProgressionService/ExperienceManager would be
                    speculative and is explicitly forbidden by the task.
    Blocking?:      NO

F7  · RewardSummary / API / events — no new field required to be invented
    Question:       §4.7
    Classification: DOCUMENTATION-ONLY (contract already resolved)
    Evidence:       docs/02-technical/DATABASE.md §1, "Reward semantics for
                    `RewardSummary`" item 2/3 — the Pet reward SEMANTICS are
                    decided (`PET_RULES.md` §5.3) and the Pet member LIST is
                    explicitly deferred to the implementation task, with the
                    `{}` staging value in force.
                    docs/02-technical/API_CONTRACTS.md §4 note 1 — `rewards`
                    always present, value = BattleResult.RewardSummary.
                    docs/02-technical/GAME_EVENTS.md §2 item 2 — payload
                    carries the reward summary on BattleWon only.
                    src/backend/GameServer.Application/Battle/
                    BattleResultService.cs:76 — public const string
                    EmptyRewardSummary = "{}"; :262 uses it.
                    BattleResult.RewardSummary is a JSON column
                    (DATABASE.md §1), so adding members is a value change,
                    not a schema change.
    Impact:         The implementation task must DECIDE the Pet member list
                    (a representation decision explicitly delegated to it by
                    DATABASE.md §1) and may then populate RewardSummary.
                    No API field, event, or endpoint needs to be invented.
                    BattleLost currently carries no reward summary on the
                    event (unchanged rule).
    Blocking?:      NO

F8  · Transaction boundary is defined; duplicate-reward risk is understood
    Question:       §4.8
    Classification: ALREADY SUPPORTED (mechanism), REQUIRES IMPLEMENTATION
                    (atomic inclusion of Pet XP)
    Evidence:       src/backend/GameServer.Application/Battle/
                    BattleResultService.cs:270 — `await _results.AddAsync(
                    result, ...)` is the durable write; the Redis delete
                    follows at :285 only after it.
                    src/backend/GameServer.Infrastructure/Postgres/
                    Repositories/BattleResultRepository.cs:59-83 — the
                    repository loads by BattleResultId, reconciles an
                    existing row, then `_dbContext.SaveChangesAsync(...)` at
                    :83. That single SaveChanges is the transaction boundary.
                    BattleResultId is the primary key (DATABASE.md §1
                    sourcing item 1) and is the documented duplicate guard
                    ("a failed delete cannot produce a second result",
                    REDIS_STATE.md §3; repository doc :21-30).
                    Retry is explicitly contemplated: DATABASE.md §1
                    sourcing item 3 keeps BattleState alive in Redis so the
                    battle-end write can be retried.
    Impact:         ANSWER to the §15 question: Player XP and Pet XP CAN be
                    awarded exactly once within the existing flow, because
                    the same SaveChanges that writes BattleResult can carry
                    the Player.XP and Pet.XP updates in one transaction, and
                    the retry path reconciles against the existing
                    BattleResult row instead of double-awarding. Caveat the
                    implementation must handle: the reconcile branch
                    (repository :78 removes and re-adds) must NOT re-apply an
                    XP grant on a retry — XP application must be tied to the
                    first-write branch, which the repository currently does
                    not distinguish for callers. This is implementable within
                    the existing boundary; NO new transaction model, no
                    distributed lock, no Redis mechanism is required.
    Blocking?:      NO

F9  · Player XP protection — no change to the Player contract is required
    Question:       §4.9
    Classification: ALREADY SUPPORTED
    Evidence:       src/backend/GameServer.Domain/Players/Player.cs:36-58 —
                    MinLevel=1, MaxLevel=50, InitialLevel=1; Level is
                    init-only. NO cap constant exists that conflicts with
                    "uncapped".
                    No Player-XP code exists to modify (grep → 0 matches).
                    docs/01-game-design/COMBAT_RULES.md §7.2-§7.3 —
                    +100/+0, uncapped XP, capped Level; §7.3 keeps reward
                    amount and curve constant separate.
                    Pet XP shares only the formula SHAPE
                    (PET_RULES.md §5.4 item 1) — a shape, not a variable.
    Impact:         Implementing Pet XP requires NO edit to any Player XP
                    value, formula, cap, or Level semantic. The Player
                    contract is untouched. The Pet cap (4900) and the Player
                    non-cap are independent and do not collide, because no
                    shared code path would exist between them.
    Blocking?:      NO

F10 · Test architecture is sufficient
    Question:       §4.10
    Classification: ALREADY SUPPORTED (patterns exist); REQUIRES
                    IMPLEMENTATION (new test cases)
    Evidence:       Existing suites to extend —
                    tests/backend/GameServer.Infrastructure.Tests/
                      PetPersistenceTests.cs (model/constraint assertions,
                      incl. Model_ShouldConstrainPetLevelMultiplierToBePositive)
                      PetLevelRecomputeTests.cs (the retired service's tests)
                      BattleResultPersistenceTests.cs / BattleResultPostgresTests.cs
                      PlayerPersistenceTests.cs / PlayerPostgresConstraintTests.cs
                    tests/backend/GameServer.Application.Tests/
                      BattleResultServiceTests.cs / BattleResultTerminalFlowTests.cs
                      BattleResultTestDoubles.cs (test-double harness)
                    tests/backend/GameServer.Domain.Tests/
                      PetLevelDerivationTests.cs (pure-function test pattern —
                      the exact template for the new XP→Level function)
                    tests/backend/GameServer.Api.Tests/
                      BattleResultEndpointTests.cs / BattleResultSmokeTest.cs
                    Four test projects exist (Domain, Application,
                    Infrastructure, Api) with established Postgres and
                    endpoint patterns.
                    No test references Player.XP or Pet.XP (grep → 0).
    Impact:         Every §4.10 scenario has a home: domain formula tests
                    (Domain.Tests, mirroring PetLevelDerivationTests),
                    reward integration (Application.Tests, mirroring
                    BattleResultServiceTests/TerminalFlow),
                    persistence/cap/constraint (Infrastructure.Tests,
                    mirroring PetPersistenceTests), API projection
                    (Api.Tests, mirroring BattleResultEndpointTests).
                    PetLevelRecomputeTests.cs and PetLevelDerivationTests.cs
                    will need to be REMOVED or REWRITTEN by the BLOCKER-1
                    cleanup. NO new test infrastructure is required.
    Blocking?:      NO

F11 · Frontend is NOT required
    Question:       §4.11
    Classification: ALREADY SUPPORTED
    Evidence:       API_CONTRACTS.md §4 defines the result response as
                    battleId/outcome/rewards/durationTurns — no Pet XP member
                    is required by any authoritative contract. PET_RULES.md
                    §5.1-§5.5 states progression rules only; no document
                    requires Pet XP to be displayed.
                    src/frontend/client/ was inspected for contractual
                    requirements only; no Pet XP runtime/API dependency
                    exists.
    Impact:         Frontend implementation: NOT REQUIRED. No Phaser UI, no
                    GameRuntime/BattleScene change. Adding XP display would
                    be a new product requirement, not part of this contract.
    Blocking?:      NO

F12 · Redis / SignalR are NOT required
    Question:       §4.12
    Classification: ALREADY SUPPORTED
    Evidence:       docs/01-game-design/PET_RULES.md §5.1 item 6 — "Pet XP
                    persists permanently with the Pet instance"; §5.3
                    rewards are applied at battle end. This is persistent
                    PlayerPet progression, not active battle state.
                    docs/02-technical/DATABASE.md §1 — Pet.XP is a Postgres
                    column; §5 item 3 — active battle state lives in Redis
                    only and is never written to Postgres until the battle
                    ends.
                    src/backend/GameServer.Domain/Battle/PetState.cs — the
                    active-state Pet members carry combat stats and
                    identities; NO XP member, and none is required: the
                    battle-end path already has the Pet identity and the
                    outcome, which is all the reward needs.
                    GAME_EVENTS.md §2 — the reward summary rides the EXISTING
                    BattleWon/BattleLost payload.
    Impact:         Do NOT add BattleState.PetXP. Do NOT add a PetXPUpdated /
                    PetLevelUpdated SignalR event. Do NOT add Redis keys. The
                    existing BattleWon/BattleLost events already carry the
                    reward summary and need no change.
    Blocking?:      NO

F0  · Task lifecycle inconsistency (orchestrator issue)
    Question:       Metadata lifecycle note
    Classification: DOCUMENTATION-ONLY (not Pet XP architecture evidence)
    Evidence:       tasks/backlog/TASK-059-*.md:24 "Status: BACKLOG" while its
                    §15 Completion Evidence records DONE;
                    tasks/backlog/TASK-061-*.md and
                    tasks/backlog/TASK-062-*.md likewise (TASK-062 §15 line
                    706-707 shows "## Status / DONE"); TASK-060 remains under
                    tasks/blocked/.
                    TASK_LIFECYCLE.md §4 specifies IN REVIEW → DONE moves the
                    file to tasks/completed/.
    Impact:         F0 is an orchestrator/lifecycle issue, not a Pet XP
                    implementation change. It does NOT gate Pet XP
                    implementation: Pet XP readiness depends on source code
                    and contracts, not on which folder a task file sits in.
                    It DOES affect how the downstream implementation task is
                    generated (correct lifecycle state), so it is reported.
                    No file was moved or re-statused by this audit.
    Blocking?:      NO
```

### Critical Findings

```text
BLOCKER
  B1  Active implementation still depends on the retired PetLevelMultiplier
      mechanism: PetLevelService is DI-registered
      (DependencyInjection.cs:85) and its only two methods assign Pet.Level
      via PetLevelDerivation.Derive(Player.Level,
      definition.PetLevelMultiplier) (PetLevelService.cs:86, :123);
      PetDefinition.PetLevelMultiplier is a mapped, constrained column
      (PetDefinitionConfiguration.cs:76-82; migration
      20260924163016_AddPetPersistence.cs:21,28). Contract requires RETIRED
      (PET_RULES.md §5.6 item 1, ADR-016 item 13).
      → §8 STOP condition FIRED.

  B2  Player.XP does not exist in source while COMBAT_RULES.md §7 and
      DATABASE.md §1/§3 require it as a persisted column and reward path.
      → §10 STOP condition FIRED (separate unfinished prerequisite).

MAJOR
  M1  Pet.Level is currently defined as a denormalized snapshot of a
      Player-derived value (Pet.cs:24-32, PetConfiguration.cs:28-32) and must
      be redefined as a function of the Pet's own XP. Two writers of the old
      meaning exist (PetLevelService.cs:86, :123) and both must go.
  M2  Pet.XP column, constraints ([0,4900]), NOT NULL, default 0, and a new
      EF migration are required; none exist (Pet.cs, PetConfiguration.cs,
      GameDbContext.cs:22-77, migrations).
  M3  RewardSummary Pet member list must be chosen by the implementation task
      — explicitly delegated by DATABASE.md §1 item 2/3 — and populated
      instead of the constant EmptyRewardSummary
      (BattleResultService.cs:76, :262).
  M4  The battle-end retry/reconcile branch
      (BattleResultRepository.cs:59-79) must not re-apply an XP grant on
      retry; XP application must bind to the first-write path. Implementable
      inside the existing SaveChanges boundary, but must be explicit.

MINOR
  m1  PetLevelDerivationTests.cs and PetLevelRecomputeTests.cs test the
      retired mechanism and will need removal/rewrite as part of B1 cleanup.
  m2  Stale XML doc comments assert the retired model in actively compiled
      files: Pet.cs:14, :25-32, :95, :101-103; PetDefinition.cs:14, :26-27,
      :84; PetConfiguration.cs:32 ("There is no XP column");
      PetRepository.cs:15; IPetRepository.cs:25, :59.
  m3  PetRepository.cs:15 and IPetRepository.cs document the retired recompute
      path, which no longer has a caller.
```

### Implementation Boundary (informational — the audit STOPPED, so this is not
an authorization to implement)

```text
Backend areas a future implementation task WOULD modify
  Domain/Pets/Pet.cs                     — add XP member; redefine Level
  Domain/Pets/PetLevelDerivation.cs      — remove or replace (B1)
  Domain/Pets/PetDefinition.cs           — remove PetLevelMultiplier (B1)
  Domain/Players/Player.cs               — add XP member (B2, separate)
  Application/Pets/PetLevelService.cs    — remove/replace (B1)
  Application/DependencyInjection.cs:85  — remove registration (B1)
  Application/Pets/IPetRepository.cs     — drop retired doc references
  Application/Battle/BattleResultService.cs — Pet XP award + RewardSummary
  Infrastructure/.../PetConfiguration.cs — map Pet.XP, [0,4900] constraint
  Infrastructure/.../PetDefinitionConfiguration.cs — drop multiplier mapping
  Infrastructure/.../Repositories/PetRepository.cs — retired-path cleanup
  Infrastructure/.../GameDbContextModelSnapshot.cs — regenerated
  Infrastructure/.../Migrations/         — NEW migration (Pet.XP; Player.XP)
  GameServer.Api/Controllers/BattleResultResponse.cs — only if the Pet
                                          RewardSummary member list is chosen

Backend areas that must REMAIN UNCHANGED
  Application/Battle/BattleResultRepository.cs transaction shape
  Domain/Battle/PetState.cs              — no Pet XP member
  BattleWon/BattleLost event contract
  Redis battle-state contract; SignalR protocol
  Player reward value (+100), Player XP uncapped rule, Player Level formula

Frontend areas remaining OUT OF SCOPE
  src/frontend/client/ — no change (F11)

Docs that remain AUTHORITATIVE (no doc change is authorized by this audit)
  docs/01-game-design/PET_RULES.md §5.1–§5.5 (the finalized contract)
  docs/01-game-design/COMBAT_RULES.md §7 (frozen Player track)
  docs/02-technical/DATABASE.md §1/§3
  docs/02-technical/API_CONTRACTS.md §4
  docs/02-technical/GAME_EVENTS.md §2
  docs/02-technical/GAME_STATE.md §2.3
  docs/03-decisions/ADR-016 (+ TASK-062 Amendment)
```

### Test Plan Inputs

```text
Domain.Tests      — new Pet XP → Level pure-function tests, mirroring
                    PetLevelDerivationTests.cs; boundaries 0/99/100/4899/4900.
                    PetLevelDerivationTests.cs must be removed with B1.
Application.Tests — extend BattleResultServiceTests.cs /
                    BattleResultTerminalFlowTests.cs: BattleWon → +100,
                    BattleLost → +0, inactive Pet exclusion, cap no-op.
                    Test doubles in BattleResultTestDoubles.cs to extend.
Infrastructure.Tests — extend PetPersistenceTests.cs: Pet.XP column,
                    default 0, [0,4900] constraint. Retire
                    PetLevelRecomputeTests.cs with B1.
Api.Tests         — extend BattleResultEndpointTests.cs only if the Pet
                    RewardSummary member list is chosen and projected.
```

### STOP CONDITION

```text
STOP CONDITION

Problem:
Two blocking conditions prevent a PASS. (1) The retired PetLevelMultiplier
mechanism is still depended on by actively registered production code —
PetLevelService is DI-registered and its only two methods compute Pet.Level
from Player.Level × PetDefinition.PetLevelMultiplier. (2) Player.XP does not
exist in source although the frozen Player contract and DATABASE.md require
it as a persisted column with a reward path.

Relevant sources:
Active retired dependency:
  src/backend/GameServer.Application/DependencyInjection.cs:85
  src/backend/GameServer.Application/Pets/PetLevelService.cs:86, :123
  src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs:69
  src/backend/GameServer.Domain/Pets/PetDefinition.cs:90
  src/backend/GameServer.Infrastructure/Postgres/Configurations/
    PetDefinitionConfiguration.cs:76-82
  migrations/20260924163011_AddPetPersistence.cs:21, :28
vs the contract requiring RETIRED:
  docs/01-game-design/PET_RULES.md §5.6 item 1
  docs/03-decisions/ADR/ADR-016-*.md item 13
Player.XP gap:
  src/backend/GameServer.Domain/Players/Player.cs:36-58 (no XP member)
  src/backend/GameServer.Infrastructure/Postgres/GameDbContext.cs:22-77
  migrations/20260924130701_AddPlayerPersistence.cs
  src/backend/GameServer.Application/Battle/BattleResultService.cs:76, :262
    (RewardSummary constant "{}"; no XP applied)
vs the frozen contract requiring persisted Player XP:
  docs/01-game-design/COMBAT_RULES.md §7.2-§7.3
  docs/02-technical/DATABASE.md §1, §3

Conflict / missing information:
The finalized Pet XP contract cannot be implemented without first deciding
how the retired PetLevelMultiplier mechanism is removed, and without deciding
whether the unimplemented Player XP track is a prerequisite or a parallel
concern. Both are outside this audit's authority (its §8/§10 STOP rules
forbid resolving them here).

Proposed resolution:
Generate the smallest follow-up task(s) — see Follow-up — that (a) retire the
PetLevelMultiplier dependency in active code and schema, and (b) establish
the Player XP persistence/reward path from the already-frozen contract. Then
re-run this audit; on PASS the Pet XP implementation task may be generated.

Waiting for:
An explicit decision on which of the two blockers is sequenced first, and
authorization to create the corresponding follow-up task(s).
```

### Follow-up

```text
Smallest blocking follow-up task:
  Not one task — two, in a sequence this audit must not choose. Both are
  documentation/implementation work outside TASK-063's read-only scope:

  (1) "Retire the PetLevelMultiplier dependency in active code and schema"
      Type: REFACTOR (no behavior change to the contract; removes a retired
      mechanism). Scope: remove PetLevelDerivation, PetLevelService + its DI
      registration, PetDefinition.PetLevelMultiplier (+ its EF mapping, its
      CK_PetDefinition_PetLevelMultiplier_Positive constraint, and a new
      migration dropping the column), retire PetLevelDerivationTests and
      PetLevelRecomputeTests, and clear the stale XML doc comments (m2/m3).
      Note: Pet.Level must remain a valid column while this lands (its new
      source, Pet.XP, does not exist yet) — so this task must either be
      merged with Pet XP implementation or preserve Level's value explicitly.

  (2) "Implement the Player XP persistence and reward path"
      Type: FEATURE. Scope: add Player.XP (int, NOT NULL, default 0) + EF
      mapping + constraint + migration; compute +100/+0 on BattleWon/
      BattleLost at the canonical integration point
      (BattleResultService.PersistTerminalResultAsync); persist Player.Level
      via min(floor(XP/100)+1, 50); populate the Player-track RewardSummary
      members already fixed by DATABASE.md §1.

Reason:
  Pet XP readiness fails on a retired-mechanism dependency that is live in
  production code and schema, and on an unimplemented Player track that the
  same battle reward path must serve. Retiring the multiplier is a
  precondition for defining Pet XP's Level source; the Player XP path is a
  precondition for a coherent battle reward contract that awards both tracks.

Why Pet XP implementation must wait:
  Implementing Pet XP now would either build on the retired Pet Level
  derivation (contradicting PET_RULES.md §5.6 item 1 / ADR-016 item 13 and
  the §8 STOP rule), or introduce a second Pet Level computation alongside
  it. And it would leave the frozen Player contract half-implemented, with a
  battle-end path that grants Pet XP but no Player XP — a state no
  authoritative document describes.
```

### Changed Files
- `tasks/backlog/TASK-063-pet-xp-implementation-readiness-audit.md` — this
  audit's verdict and findings only. **No other file was modified.**
  No source, test, migration, documentation, or other task file was created,
  modified, moved, or deleted.

### Validation Results
- `grep "\bXP\b" over src/` — 0 matches → Player.XP and Pet.XP are absent
  from all production source (F1, F3)
- `grep "PetLevelService|RecomputeFor*Async|Derive(" over src/` — 13 matches,
  all confined to the type's own declaration, its DI registration, and XML
  docs → registered but **no production caller** proven (F2)
- `grep "PetLevelMultiplier|PetLevelDerivation|PetLevelService" over src/` —
  51 matches: 12 in Domain, 4 in Application, 6 in Infrastructure
  configuration/repository, 29 across migrations/snapshots → retirement not
  implemented (F2)
- `test-suite inventory` — 4 test projects; 21 Pet/Reward/BattleResult/Player
  test files identified (F10)
- `git status --porcelain` — no change attributable to this audit under
  `src/`, `tests/`, or `docs/`

### Scope Verification
- [x] No source code modified (`git status -- src/` clean)
- [x] No test modified (`git status -- tests/` clean)
- [x] No migration created or modified
- [x] No documentation file modified (`git status -- docs/` clean)
- [x] TASK-059 / TASK-061 / TASK-062 unmodified
- [x] TASK-060 unmodified
- [x] No task file moved or re-statused (F0 recorded, not repaired)
- [x] No implementation task created (no TASK-064)
- [x] No gameplay decision made
- [x] No architecture decision made

### Findings

```text
F0  · <title>
    Question:       <§4.n>
    Classification: <ALREADY SUPPORTED | REQUIRES IMPLEMENTATION |
                     DOCUMENTATION-ONLY | AMBIGUOUS>
    Evidence:       <file>:<line> — <what it shows>
    Impact:         <what the implementation task must do, or "none">
    Blocking?:      YES | NO
... (repeat for every finding)
```

### Implementation-Task Input Set (required if verdict = PASS)

```text
Persistence:      <exact entity/config/migration work the implementation needs>
Domain:           <XP member, Level derivation, retired-code removal>
Application:      <reward integration point + identity source>
API/Events:       <RewardSummary member list decision, event payload impact>
Transaction:      <defined boundary, no new semantics>
Tests:            <locations + patterns to extend>
Frontend:         <none | explicit authoritative requirement>
Redis/SignalR:    <none | explicit authoritative requirement>
Out of scope:     <what the implementation task must NOT touch>
```

### STOP CONDITION (required if verdict = STOP)

```text
STOP CONDITION

Problem:
<one or two sentences>

Relevant sources:
<file paths + section/line references, both/all sides if a conflict>

Conflict / missing information:
<what exactly is unresolved>

Proposed resolution:
<the smallest documentation/architecture change that would resolve it>

Waiting for:
<what kind of approval/input is needed to proceed>
```

### Validation Results
- `<search / inspection>` — <result, with counts>

### Scope Verification
- [ ] No source code modified (`git status -- src/` clean)
- [ ] No test modified (`git status -- tests/` clean)
- [ ] No migration created or modified
- [ ] No documentation file modified (`git status -- docs/` clean)
- [ ] TASK-059 / TASK-061 / TASK-062 unmodified
- [ ] TASK-060 unmodified
- [ ] No implementation task created
- [ ] No gameplay decision made
- [ ] No architecture decision made
