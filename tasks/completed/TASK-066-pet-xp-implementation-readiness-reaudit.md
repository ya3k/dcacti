# TASK-066 — Pet XP Implementation-Readiness Re-Audit

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and src/ by path and section; copies only what an auditor
  needs to know where to look.

  THIS TASK AUDITS; IT DOES NOT IMPLEMENT.

  It answers one question:
      Is Pet XP implementation-ready after TASK-064 + TASK-065?

  TASK-063 STOPPED with two blockers (B1 retired-mechanism dependency,
  B2 missing Player.XP) and prescribed exactly two follow-up tasks plus a
  re-run of the audit. TASK-064 (B1) and TASK-065 (B2) are DONE. This task
  independently re-verifies both — it does NOT assume their reports — and
  re-checks every remaining TASK-063 finding (M1-M4, m1-m3, F0-F12) against
  the current tree.

  It does NOT implement Pet XP. It does NOT create the Pet XP implementation
  task. A READY verdict authorizes generating that task later; this task
  never creates it.
-->

---

## Metadata

```text
Task ID:           TASK-066
Type:              ARCHITECTURE
Status:            DONE
Risk:              MEDIUM (read-only re-audit — no source, test, migration,
                   or documentation file may be modified. Same rationale as
                   TASK-063: the deliverable is a readiness FINDING, not an
                   architecture change; tasks/TASK_TYPES.md §4 sets
                   ARCHITECTURE at HIGH "always", lowered to MEDIUM because
                   this task introduces no architectural decision. It
                   escalates to a STOP report if a genuine architecture
                   decision surfaces — it never designs one.)
Priority:          HIGH (it is the mandatory gate between the completed
                   TASK-064/TASK-065 follow-ups and any Pet XP implementation
                   task)
Primary Agent:     orchestrator (read-only cross-boundary audit spanning
                   Domain, Application, Infrastructure, Api, tests, and docs;
                   no single specialist owns it and none may change code
                   during an audit)
Supporting Agents: backend (battle-end reward flow + integration point),
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
Dependencies:      TASK-063 (DONE — verdict STOP, blockers B1/B2; historical
                   audit, read-only, must NOT be modified),
                   TASK-064 (DONE — PetLevelMultiplier retirement; read-only,
                   must NOT be modified),
                   TASK-065 (DONE — Player XP implementation; read-only, must
                   NOT be modified),
                   TASK-059 / TASK-061 / TASK-062 (DONE — contract history;
                   read-only),
                   TASK-060 (BLOCKED — unmodified)
Blocks:            the Pet XP implementation task (generated only after this
                   re-audit records READY — NOT created by this task)
Estimate:          Complex (7 skills; read-only; zero code, zero doc, zero
                   schema changes; output is a READY / NOT IMPLEMENTATION-READY
                   verdict plus the implementation task's input set)
```

**Type classification note.** `ARCHITECTURE`, not `DOCUMENTATION` — for the
same reason as TASK-063: the subject is whether the finalized contract fits
the existing architecture without an architecture change
(`tasks/TASK_TYPES.md` §2/§3). This task changes no document and builds
nothing.

**Lifecycle note (observed, not repaired).** At creation time TASK-063 and
TASK-064 remain physically in `tasks/backlog/` with `Status: BACKLOG` in
their metadata although their Completion Evidence records completion;
TASK-060 remains in `tasks/blocked/`; TASK-065 is in `tasks/completed/`
(`Status: DONE`). Per `TASK_LIFECYCLE.md` §4 a DONE file belongs in
`tasks/completed/`. **This task does not repair that** — moving or
re-statusing task files is a lifecycle action outside its scope
(`AGENTS.md` §16). Record it as a report-only finding (Q16); do not treat a
stale `Status:` field as evidence that a task is unfinished — read each
task's Completion Evidence.

---

## 1. Objective

Independently re-audit implementation readiness of the frozen Pet XP
contract and return exactly one verdict — **`READY`** or
**`NOT IMPLEMENTATION-READY`** — answering:

```text
After TASK-064 (PetLevelMultiplier retirement) and TASK-065 (Player XP
implementation) are DONE, can Pet XP now be implemented as a small,
deterministic progression change using the existing architecture, with no
new gameplay decision, no new architecture decision, no new persistence
decision, no new API-contract decision, and no undocumented behavior?
```

The re-audit **re-verifies every premise from source** — TASK-063's
blockers, TASK-064's and TASK-065's reports, and every remaining TASK-063
finding are inputs to check, never facts to inherit.

**This task does NOT implement Pet XP.**
**This task does NOT create the Pet XP implementation task (no TASK-067).**
**A READY verdict only authorizes generating that task in a later, separate
action.** If the verdict is `NOT IMPLEMENTATION-READY`, this task reports
the **smallest** prerequisite/decision task needed — it does not create it.

---

## 2. Historical Context

```text
TASK-059 (DONE)   two-track structure + frozen Player contract
        ↓
TASK-061 (DONE)   Product Owner decision record (the twelve values)
        ↓
TASK-062 (DONE)   finalized the Pet XP contract into authoritative docs/
        ↓
TASK-063 (DONE)   IMPLEMENTATION-READINESS AUDIT → verdict STOP
                  B1  active code still depends on retired
                      PetLevelMultiplier mechanism
                  B2  Player.XP absent while documented as persisted
                  (Pet XP was NOT blocked by battle-end architecture:
                   audits #4/#5/#7/#8/#11/#12 PASSED)
        ↓
TASK-064 (DONE)   retire PetLevelMultiplier (resolves B1)
                  — removed PetLevelDerivation.cs, PetLevelService.cs + its
                    DependencyInjection registration, PetDefinition.
                    PetLevelMultiplier + EF mapping + constraint,
                    migration 20260927124855_DropPetLevelMultiplier,
                    retired test suites; suite green at 1,499/1,499
        ↓
TASK-065 (DONE)   implement Player XP progression (resolves B2)
                  — Player.XP persisted (migration 20260927130330_AddPlayerXp),
                    LevelForXp/GrantBattleXp, +100/+0 on the canonical
                    battle-end path (GrantPlayerXpAsync), first-write flag on
                    BattleResultRepository.AddAsync; suite green at 1,557/1,557
        ↓
TASK-066 (this)   RE-AUDIT → READY / NOT IMPLEMENTATION-READY
        ↓
        READY ⇒ the Pet XP implementation task may be generated (NEW ID)
        NOT IMPLEMENTATION-READY ⇒ the smallest prerequisite/decision task
                                   is reported (NOT created)
```

Rules for handling this history:

- **TASK-063, TASK-064, TASK-065 are immutable inputs.** Do not modify,
  reopen, or alter their Completion Evidence (`AGENTS.md` §16).
- **Do not inherit their conclusions.** B1/B2 resolution and every "PASS" in
  TASK-063 must be re-proven from the current tree; a stale premise is
  itself a finding. TASK-064/TASK-065 completion reports are claims to
  verify, not evidence to cite as proof.
- **The frozen contract is fixed input.** Per TASK-063 §2, decided by the
  Product Owner and recorded by TASK-062 — never reopened, reinterpreted, or
  re-derived:

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
Pet XP hard cap:         4900 (enforced on write; overflow not stored)
Player/Pet progression:  independent XP pools
PetLevelMultiplier:      RETIRED
```

Player XP is **frozen** (COMBAT_RULES.md §7; implemented by TASK-065) and
out of this task's authority: +100/+0, uncapped XP, Level
`min(floor(XP/100)+1, 50)`, Level range [1, 50].

**Working-tree note.** The TASK-059…065 work is present but **uncommitted**
(`git status --porcelain` shows modified docs/src/tests and untracked
migrations/tasks as of task creation). The re-audit runs against the working
tree, and must not commit, stage, or revert anything.

---

## 3. Authoritative References

- `docs/01-game-design/PET_RULES.md` **§5.1–§5.5** — the finalized contract
  (§5.1 ownership, §5.2 initial values, §5.3 reward targeting, §5.4 formula,
  §5.5 cap/post-cap); §5.6 retired terms are not reused; §5.7 non-XP
  attributes unchanged; §1 data model
- `docs/01-game-design/COMBAT_RULES.md` §7 (§7.1–§7.6) — the frozen Player
  track; §7.3 reward amount vs. curve constant
- `docs/01-game-design/GAME_RULES.md` §9.3 (Pet rule), §12 (Power), §17
  (resolution order), §18 (server authority), §20 (Rule Change Policy)
- `docs/02-technical/DATABASE.md` §1 (`Pet` / `Player` entities, `Pet.XP`,
  `Pet.Level`, `BattleResult`, "Reward semantics for `RewardSummary`"), §3
  constraints, §5 what-is-not-here
- `docs/02-technical/API_CONTRACTS.md` §4 notes 1/3 (`rewards` both outcomes;
  event-vs-REST reconciliation)
- `docs/02-technical/GAME_EVENTS.md` §2 (`BattleWon` / `BattleLost` payload
  rule + reward-summary item)
- `docs/02-technical/GAME_STATE.md` §2.3 (`PetState`, `PetId`), §2.8
  (`BattleState.PlayerId`) — the active-combat-Pet identity contract
- `docs/02-technical/ARCHITECTURE.md` §1–§5 (module/layer boundaries,
  anti-overengineering notes); `docs/02-technical/TDD.md` §4 (persistence
  boundary), §6 (RNG — check whether Pet XP touches it)
- `docs/02-technical/REDIS_STATE.md` — for the Q14 question only
- `docs/00-overview/MVP_SCOPE.md` §1 (Pet progression IN), §2 (OUT), §4
  (FUTURE by default)
- `docs/00-overview/GDD.md` §6, §14
- `docs/03-decisions/README.md` §3, §5, §7; `ADR-016` (independent Player/Pet
  XP tracks, incl. the TASK-062 amendment), `ADR-011`, `ADR-012`
- `AGENTS.md` §4, §6, §7, §9, §10, §12, §13, §16, §17, §18, §20
- `tasks/backlog/TASK-063-pet-xp-implementation-readiness-audit.md` — the
  STOPPED audit this re-audit supersedes (immutable)
- `tasks/backlog/TASK-064-retire-pet-level-multiplier.md`,
  `tasks/completed/TASK-065-implement-player-xp-progression.md` — completion
  claims to verify (immutable)

Historical context only — **read, never treat as a Pet XP source, never
modify**: `tasks/backlog/TASK-059-*`, `TASK-061-*`, `TASK-062-*`,
`tasks/blocked/TASK-060-*`, `tasks/completed/TASK-024-*`.

---

## 4. Current State (verified at task creation)

Recorded so the audit confirms rather than rediscovers. **Re-verify each; a
stale premise is itself a finding.** Line numbers drift — cite current ones.

```text
src/backend/GameServer.Domain/Pets/Pet.cs
    Members: PetInstanceId (:60), PlayerId (:67), PetDefinitionId (:75),
    Tier (:83), Star (:91), Level (:107 — `public int Level { get; set; }`),
    AcquiredAt (:113). NO XP member.
    Comments (:31-32, :103-104) name Pet's own XP as the FUTURE source of
    Level (PET_RULES.md §5.4) — "not yet implemented".

src/backend/GameServer.Infrastructure/Postgres/Configurations/
    PetConfiguration.cs
    :35 "There is still no XP column on this table."
    :103-114 — Level mapped; CK_Pet_Level_Range ([1,50] via
    Player.MinLevel/MaxLevel).

grep 'PetLevelMultiplier|PetLevelDerivation|PetLevelService' over src/
    and tests/  → 0 matches (TASK-064 retirement appears to hold —
    re-verify, including the DependencyInjection registration, EF mappings,
    constraints, and the model snapshot; historical migrations that created
    the column are expected residue, not active references).

src/backend/GameServer.Domain/Players/Player.cs   [TASK-065]
    :155 `public int XP { get; set; } = InitialXp;` (uncapped)
    :234 LevelForXp(int) = min(floor(XP/100)+1,50)
    :249-273 GrantBattleXp(int) — BattleWon +100 / BattleLost +0, then
    Level = LevelForXp(XP)

src/backend/GameServer.Application/Battle/BattleResultService.cs
    :93  public const string EmptyRewardSummary = "{}";  :292 used
         → RewardSummary is STILL the staging constant after TASK-065
    :209 PersistTerminalResultAsync(...) → Task<bool> (first-write report)
    :218 petInstanceId = state.PetState.PetId.Value
    :321/:394 GrantPlayerXpAsync — Player grant on the canonical path
         (the pattern a Pet XP grant would mirror)
    (TASK-063's refs :76/:262 have drifted to :93/:292 — re-cite.)

src/backend/GameServer.Infrastructure/Postgres/Repositories/
    BattleResultRepository.cs
    :49 AddAsync → Task<bool> (reports first durable write)
    :87 single SaveChangesAsync — the transaction boundary

Migrations present (working tree): 20260927124855_DropPetLevelMultiplier
    (TASK-064), 20260927130330_AddPlayerXp (TASK-065).
    No Pet.XP migration exists.

Test suites added by TASK-065: PlayerXpProgressionTests (Domain),
    PlayerXpBattleRewardTests (Application), PlayerProgressionPersistenceTests
    + PlayerXpSchemaTests (Infrastructure). Retired suites
    (PetLevelDerivationTests, PetLevelRecomputeTests) deleted by TASK-064.

Working tree: TASK-059…065 changes are UNCOMMITTED. Audit runs as-is and
    must not commit/stage/revert.
```

---

## 5. Audit Scope

### In Scope

- Independent re-verification of TASK-063's blockers (B1, B2) against the
  current tree.
- Re-classification of every remaining TASK-063 finding (F0–F12, M1–M4,
  m1–m3) as resolved / still open / newly stale.
- The readiness questions in §6, each answered with classification +
  evidence.
- A single verdict: `READY` or `NOT IMPLEMENTATION-READY`.
- If READY: the implementation task's input set (information only).
- If NOT IMPLEMENTATION-READY: the smallest prerequisite/decision task
  (named, not created).

### Out of Scope

- Implementing Pet XP (or any part of it), migrating, or writing code/tests.
- Creating the Pet XP implementation task (no TASK-067) or any other task.
- Modifying, moving, or re-statusing any task file, including this one's
  predecessors.
- Resolving any conflict, ambiguity, or decision the audit finds — report
  only (`AGENTS.md` §4, §7).
- Reopening the frozen contract (§2) or the frozen Player track.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.
- Committing or staging anything in the working tree.

### Affected Areas (read-only)

```text
[ ] src/backend/GameServer.Domain/          (Pets/, Players/, Battle/)   READ ONLY
[ ] src/backend/GameServer.Application/     (Pets/, Battle/, Players/)   READ ONLY
[ ] src/backend/GameServer.Infrastructure/  (Postgres/, Migrations/)     READ ONLY
[ ] src/backend/GameServer.Api/                                            READ ONLY
[ ] src/frontend/client/                                                    READ ONLY
[ ] tests/                                                                  READ ONLY
[ ] docs/                                 (conformance reference)        READ ONLY
[ ] tasks/                                (this file only)
```

**No file in the first seven rows may be created, modified, or deleted.**

---

## 6. Audit Questions

Every question must be answered with **file:line (or doc section) evidence**
and one classification:

```text
ALREADY SUPPORTED      — existing code/contract already satisfies it
REQUIRES IMPLEMENTATION — a code/schema change is needed (describe it)
DOCUMENTATION-ONLY     — docs and code disagree, docs are correct
AMBIGUOUS              — cannot be determined; triggers a STOP (§9)
```

### Q1 Former B1 — retired PetLevelMultiplier mechanism (re-verify)

- Prove zero **active** references to `PetLevelMultiplier`,
  `PetLevelDerivation`, `PetLevelService` in src/ and tests/, including the
  DI registration, EF mappings, check constraints, and model snapshot.
- Distinguish historical migrations/changelogs and supersession notes in
  ADRs (not blockers) from active code (blockers).
- Confirm no replacement Pet Level derivation was introduced by TASK-064.

### Q2 Former B2 — Player XP track (re-verify, do not trust the report)

- Verify from source that Player XP conforms to COMBAT_RULES.md §7 and
  DATABASE.md §1/§3: persisted column + default + constraint, +100/+0
  awarded on the canonical battle-end path, uncapped XP, Level formula,
  exactly-once behavior.
- Confirm TASK-065's completion claims (migration, first-write flag,
  test counts) against the tree.

### Q3 Pet persistence readiness

- Confirm `Pet.XP` presence/absence against DATABASE.md §1/§3; enumerate
  exactly what implementation must add (column, NOT NULL, default 0,
  [0,4900] constraint, migration) — without designing it.
- Classify whether the 4900 cap is a DB-level check constraint or an
  application-layer invariant **as the contract states**; do not choose.
- Classify existing-Pet-row semantics (rows have Level today, no XP column
  yet): is any backfill/initial-state question open, or does the documented
  default answer it?

### Q4 Pet XP ownership

- Confirm XP attaches to the Pet instance (`Domain/Pets/Pet.cs`), not
  `PetDefinition`, not `Player`, not static/global state.

### Q5 Single Pet Level source (no competing derivation)

- Identify every current writer of `Pet.Level` and prove there is no
  competing, dormant, or reintroduced derivation.
- Confirm the contract's only documented future source of Pet Level is this
  Pet's own XP (PET_RULES.md §5.4).

### Q6 Battle reward integration point

- Confirm the canonical terminal path (`BattleResultService.
  PersistTerminalResultAsync`, current line ~:209) remains the single
  battle-end entry point, and that a Pet XP grant can extend it the same
  way `GrantPlayerXpAsync` does — no new subsystem, service, or pipeline.

### Q7 Active combat Pet identity

- Re-confirm the active combat Pet identity is deterministic,
  server-derived, and in hand at the reward point
  (`state.PetState.PetId.Value`, ~:218): exactly one recipient per battle.

### Q8 Exactly-once award feasibility

- Re-confirm the existing boundary (single `SaveChangesAsync`, first-write
  `bool` from `BattleResultRepository.AddAsync`) can award Pet XP exactly
  once, including the retry/reconcile path (TASK-063 M4; TASK-065 residual
  finding 1).
- Verify the first-write binding a Pet grant must use exists and is
  exercised by the Player grant — no new transaction semantics, locks, or
  Redis mechanisms.

### Q9 Cap, formula, and pool independence

- Confirm the cap asymmetry is representable: Pet XP capped at 4900 vs
  Player XP uncapped; formula `min(floor(XP/100)+1, 50)`; initial 0/1;
  range [1,50].
- Confirm Pet and Player pools share no storage, no grant path mutation,
  and no value; Player grant code must not be repurposed to move Pet XP.
- Confirm NO generic progression abstraction is required or warranted
  (`AGENTS.md` §9, `ARCHITECTURE.md` §5).

### Q10 RewardSummary / API / events

- Classify the current `{}` staging value (`EmptyRewardSummary`, ~:93/:292)
  versus DATABASE.md §1's delegation of the Pet member list to the
  implementation task: representation decision or gap?
- Confirm what API_CONTRACTS.md §4 and GAME_EVENTS.md §2 already require —
  no new field, event, or endpoint may be invented.

### Q11 Player-track protection

- Verify Pet XP can be added with zero edits to Player XP values, reward
  amounts, uncapped rule, Level formula, TASK-059, or TASK-065's outputs.
  Any required Player-track change is a blocker.

### Q12 Test readiness

- Classify whether existing suites/patterns cover the required scenarios
  (initial state, win/loss reward, active-Pet targeting, inactive-Pet
  exclusion, 4900 cap, no overflow, Level calculation, Player/Pet
  independence, exactly-once, applied-schema).
- Note the TASK-065 suites as templates (e.g. PlayerXpProgressionTests,
  PlayerXpBattleRewardTests, PlayerXpSchemaTests) and confirm the retired
  suites are gone. Identify gaps; **add or modify no tests.**

### Q13 Frontend scope

- Confirm no frontend change is required unless an authoritative contract
  explicitly demands Pet XP exposure (API_CONTRACTS.md §4 response fields;
  no document currently does). Do not create display requirements.

### Q14 Redis / SignalR scope

- Confirm Pet XP is persistent `PlayerPet` progression, not active
  `BattleState`: no `BattleState.PetXP`, no new SignalR event, no new Redis
  key (PET_RULES.md §5.1, DATABASE.md §5, GAME_STATE.md §2.3,
  REDIS_STATE.md, GAME_EVENTS.md §2).

### Q15 Documentation consistency

- Cross-check PET_RULES.md §5.1–§5.7 ↔ COMBAT_RULES.md §7 ↔ ADR-016 (+
  TASK-062 amendment) ↔ ADR-011/ADR-012 supersession notes ↔
  DATABASE.md/API_CONTRACTS.md/GAME_EVENTS.md/GAME_STATE.md ↔ the current
  implementation state after TASK-064/065.
- Verify retired terms are marked retired (PET_RULES.md §5.6,
  ADR-016 item 13) and not reused as active sources anywhere.
- Any conflict: report both sides per `AGENTS.md` §4 → STOP. Never resolve.

### Q16 Lifecycle and scope observations (report-only)

- Record task-file location/status inconsistencies (see Metadata lifecycle
  note) without repairing them.
- Confirm Pet progression is IN MVP scope (`MVP_SCOPE.md` §1) and nothing
  OUT (§2) is implied by the contract.

---

## 7. Acceptance Criteria

Exactly 21 binary checks. All must be satisfied before the verdict is
recorded. Evidence = file:line or doc section for each.

```text
[ ] 1  B1 re-verified: zero active PetLevelMultiplier/PetLevelDerivation/
       PetLevelService references in src/ and tests/, incl. DI registration,
       EF mapping, constraints, snapshot (historical migrations/ADR notes
       excluded and classified as non-blockers)
[ ] 2  B2 re-verified from source: Player.XP persisted (+ default,
       constraint, migration) and +100/+0 granted exactly once on the
       canonical path, conforming to COMBAT_RULES.md §7 and
       DATABASE.md §1/§3 — verified, not inherited from TASK-065's report
[ ] 3  Pet XP ownership confirmed as the Pet instance (not PetDefinition,
       not Player, not global) — PET_RULES.md §5.1 conformance
[ ] 4  Pet persistence gap classified: Pet.XP present/absent, and the
       exact column/constraint/migration work enumerated against
       DATABASE.md §1/§3
[ ] 5  Existing-Pet-row / initial-state (backfill) semantics classified —
       either answered by the documented default or reported as ambiguous
[ ] 6  Pet.Level has exactly one documented source (its own XP); every
       current writer identified; no competing/dormant derivation found
[ ] 7  Active combat Pet identity confirmed deterministic and
       server-authoritative at the reward point (exactly one recipient)
[ ] 8  Battle reward integration point confirmed (PersistTerminalResultAsync)
       as an extension of the existing path — no new subsystem required
[ ] 9  Exactly-once feasibility confirmed within the existing
       SaveChanges/first-write boundary incl. the retry path; caveats
       stated; no new transaction/lock/Redis semantics needed
[ ] 10 Cap asymmetry confirmed representable (Pet 4900 vs Player uncapped)
       with no shared abstraction and no Player-track edit
[ ] 11 Pet Level formula/initial/range conformance confirmed
       (min(floor(XP/100)+1, 50); XP 0 → Level 1; [1,50])
[ ] 12 Player-track protection confirmed: zero required edits to Player XP
       values, rewards, uncapped rule, Level formula, TASK-059/TASK-065
       outputs
[ ] 13 RewardSummary/API/event implications classified WITHOUT inventing
       fields — Pet member list recognized as the implementation task's
       delegated representation decision (DATABASE.md §1)
[ ] 14 Frontend scope classified: no change required unless an authoritative
       contract demands it (none found)
[ ] 15 Redis/SignalR scope classified: no BattleState.PetXP, no new event,
       no new key
[ ] 16 Test readiness classified against existing suites/patterns; gaps
       explicitly reported; no test added, modified, or executed
[ ] 17 Documentation consistency checked across PET_RULES §5.x,
       COMBAT_RULES §7, ADR-016/011/012, and the technical contracts;
       conflicts reported per AGENTS.md §4, none silently resolved
[ ] 18 Every §6 question carries a classification plus file:line (or doc
       section) evidence; a finding without a citation does not count
[ ] 19 Read-only constraint held: git status shows no change under src/,
       tests/, docs/, or migrations attributable to this task; only this
       task file was created
[ ] 20 Prior tasks unmodified (TASK-059/060/061/062/063/064/065); no task
       file moved or re-statused; no implementation task (TASK-067) created
[ ] 21 Final verdict recorded as exactly READY or NOT IMPLEMENTATION-READY
       with reasons; if NOT IMPLEMENTATION-READY, the single smallest
       prerequisite/decision task is NAMED (not created)
```

---

## 8. Read-Only Validation

This is a **read-only audit**: validation is by inspection, cross-reference,
and search — **not** test execution (`core/validation.md` §1 —
architecture/documentation validation layers). Tests are not run, added, or
modified; TASK-064/TASK-065 Completion Evidence records the suite results
(1,499 then 1,557 passing).

### Static Searches (read-only)

```text
[ ] PetLevelMultiplier | PetLevelDerivation | PetLevelService over src/ and
    tests/ — enumerate every hit; classify active vs historical
[ ] DependencyInjection.cs — confirm no PetLevelService registration
[ ] \bXP\b over Domain/Pets/Pet.cs and Domain/Players/Player.cs — confirm
    presence (Player) / absence (Pet) of an XP member
[ ] Pet.Level writers across src/ — enumerate every assignment site
[ ] PersistTerminalResultAsync / GrantPlayerXpAsync / EmptyRewardSummary /
    RewardSummary in Application + Api — confirm current line numbers and
    the {staging} state
[ ] AddAsync first-write flag + SaveChangesAsync on the battle-end path —
    confirm the defined boundary (Q8)
[ ] PetState/BattleState members — confirm no Pet XP field present or
    required (Q14)
[ ] Migrations inventory — confirm DropPetLevelMultiplier and AddPlayerXp
    exist and no Pet.XP migration exists
[ ] git status --porcelain before/after — the delta attributable to this
    task must be exactly this file
```

### Evidence Quality Bar

- A finding without a `file:line` (or doc section) citation is not a finding.
- "Looks fine" is not a classification; use the §6 vocabulary.
- Distinguish **absent** (feature does not exist — implementation work) from
  **contradictory** (exists but conflicts — blocker) from **ambiguous**
  (cannot be determined — STOP). They imply different follow-ups.
- Re-verify, never inherit: a claim from TASK-063/064/065 is an assertion
  until confirmed against the current tree.

---

## 9. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always
apply. **This task records `NOT IMPLEMENTATION-READY` rather than READY if
any of the following is true.** On STOP: write the STOP CONDITION report in
§11, state the exact reason, and name the smallest blocking follow-up task.
**Do not resolve the condition inside TASK-066.**

- Pet XP ownership is ambiguous.
- `PlayerPet` persistence semantics or existing-row/backfill semantics are
  ambiguous.
- A competing Pet Level source exists, is required, or cannot be excluded.
- Active combat Pet identity cannot be determined deterministically.
- Exactly-once award is infeasible within the existing boundary.
- The API / event / Redis / SignalR / frontend contract required for
  implementation is ambiguous or missing.
- A gameplay rule needed for implementation is missing or undocumented
  (`AGENTS.md` §7).
- Authoritative documents conflict (`AGENTS.md` §4) — report both sides;
  do not pick one.
- The Pet XP cap behavior or Level formula conflicts with the documents.
- Player XP would need modification.
- `RewardSummary` requires an undocumented API field; `BattleState` would
  need an undocumented Pet XP field; Redis/SignalR semantics are required
  but undefined.
- A new architecture decision is required, or a gameplay decision must be
  invented.
- The existing implementation reveals Pet XP cannot be implemented as a
  small isolated progression change.

Task-specific additional conditions:

- **If the verdict cannot be determined from evidence** → STOP; report which
  question is unanswerable and why.
- **If answering a §6 question requires modifying code or docs to find out**
  → STOP; an audit may read, not change.
- **If a §2 fixed-contract value appears contradicted by an authoritative
  document** → STOP; report the exact conflict (`AGENTS.md` §4). Do not
  choose an interpretation.
- **If the audit finds itself designing the implementation** → STOP;
  designing is the next task's job.
- **If the task expands beyond the §6 questions or the §5 read-only areas**
  → STOP; report it, do not apply it (`AGENTS.md` §16).
- **If the audit would need to create a task to proceed** → STOP and report;
  task creation is a separate, later action.

---

## 10. Expected Output

### Findings format

```text
F<n> · <one-line title>
  Question:      Q<n>
  Classification: ALREADY SUPPORTED | REQUIRES IMPLEMENTATION |
                  DOCUMENTATION-ONLY | AMBIGUOUS
  Evidence:      <file>:<line/section> — <what it shows>
  Impact:        <what the implementation task must do, or "none">
  Blocking?:     YES | NO  (YES ⇒ §9 STOP)
```

Register at minimum: the B1 re-verification, the B2 re-verification, and the
re-classification of TASK-063's F0–F12 / M1–M4 / m1–m3 (resolved / still
open / stale).

### Verdict (exactly one)

```text
READY                     — every §7 check satisfied, no §9 condition fired
NOT IMPLEMENTATION-READY  — ≥1 §9 condition fired (report each)
```

### If READY

Provide the **implementation-task input set** (information only — the task
itself is NOT created):

```text
Persistence:      <exact entity/config/migration work needed>
Domain:           <XP member, Level derivation, writer updates>
Application:      <reward integration point + identity source + exactly-once
                   binding>
API/Events:       <RewardSummary member-list decision delegated by
                   DATABASE.md §1; event/REST impact>
Transaction:      <existing boundary; no new semantics>
Tests:            <locations + patterns to extend>
Frontend:         <none | explicit authoritative requirement>
Redis/SignalR:    <none | explicit authoritative requirement>
Out of scope:     <what the implementation task must NOT touch>
```

### If NOT IMPLEMENTATION-READY

Write the STOP CONDITION report in this exact shape (§11), and name the
**single smallest** prerequisite/decision task (type, scope in one
paragraph, why it blocks) — **named only, never created**.

---

## 11. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AUDITOR.
  Keep concise and factual. Cite file:line for every finding.
-->

### Verdict

```text
READY
```

### Executive Result

```text
Pet XP is implementation-ready after TASK-064 and TASK-065.

Both TASK-063 blockers are independently re-proven as resolved in the
current working tree. B1: `PetLevelDerivation.cs` and `PetLevelService.cs`
are gone from disk, `DependencyInjection.cs` registers no `PetLevelService`,
`PetDefinition` carries no `PetLevelMultiplier` property, and every
remaining `PetLevelMultiplier` hit under src/ is either immutable migration
history or an explanatory comment (29 hits, zero live code references).
B2: `Player.XP` exists (`Player.cs:155`), is persisted with default 0 and
`CK_Player_XP_NonNegative` (`PlayerConfiguration.cs:84-90`, migration
20260927130330_AddPlayerXp.cs:13-23), and the +100/+0 grant is applied
exactly once on the canonical battle-end path (`BattleResultService.cs:319-322`
gated by the first-durable-write flag from `BattleResultRepository.cs:82-91`).

The remaining Pet XP gap is exactly one feature-shaped absence, not a
contract or architecture defect: `Pet.XP` does not exist
(`Pet.cs` has no XP member; `PetConfiguration.cs:35` "There is still no XP
column on this table"; `GameDbContextModelSnapshot.cs:149-188` maps Pet with
no XP). Every other precondition holds: Pet ownership is per-instance
(`Pet.cs:67`), `Pet.Level` has zero production writers
(`PetRepository.cs:13-18` — no recompute surface), the active combat Pet
identity is deterministic at the reward point
(`BattleResultService.cs:218` = `state.PetState.PetId.Value`), the reward
integration point is a single existing path, and exactly-once is already
implemented and exercised by the Player grant. No Redis, SignalR, API,
event, frontend, or Player-track change is required, and no new gameplay,
architecture, persistence, or API-contract decision is needed.

Every one of the 21 §7 acceptance checks is satisfied and no §9 STOP
condition fired. The Pet-track `RewardSummary` member list is explicitly
delegated to the implementation task by `DATABASE.md` §1 item 2/3 and is a
representation decision, NOT a missing contract — so it is not a STOP.
```

### Findings

```text
F1 · B1 RE-VERIFIED RESOLVED — retired PetLevelMultiplier mechanism fully gone
    Question:       Q1
    Classification: ALREADY SUPPORTED
    Evidence:       src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs
                    — DELETED (git status: " D").
                    src/backend/GameServer.Application/Pets/PetLevelService.cs
                    — DELETED (git status: " D").
                    grep 'PetLevelMultiplier|PetLevelDerivation|PetLevelService'
                    over src/ → 29 matches, ZERO live code references:
                      · migration history: 20260924163011_AddPetPersistence.cs:21,:28
                        + 6 *.Designer.cs snapshots (immutable)
                      · comments only: Pet.cs:27,:100 · PetConfiguration.cs:31 ·
                        PetDefinitionConfiguration.cs:23-24 · PetRepository.cs:15 ·
                        IPetRepository.cs:21
                    grep same pattern over tests/ → 5 matches, all comments/
                    assertions: PetPersistenceTests.cs:98-99,
                    PlayerXpSchemaTests.cs:167,171,177.
                    DependencyInjection.cs — no PetLevelService registration.
                    GameDbContextModelSnapshot.cs — no PetLevelMultiplier.
                    No replacement Pet Level derivation introduced.
    Impact:         none
    Blocking?:      NO

F2 · B2 RE-VERIFIED RESOLVED — Player.XP persisted + granted exactly once
    Question:       Q2
    Classification: ALREADY SUPPORTED
    Evidence:       src/backend/GameServer.Domain/Players/Player.cs:155
                      public int XP { get; set; } = InitialXp;   (uncapped)
                    Player.cs:234-246 LevelForXp = min(floor(XP/100)+1,50)
                    Player.cs:263-274 GrantBattleXp (won +100 / lost +0)
                    src/.../Configurations/PlayerConfiguration.cs:84-90
                      XP required, HasDefaultValue(InitialXp), CK_Player_XP_NonNegative
                    src/.../Migrations/20260927130330_AddPlayerXp.cs:13-23
                      AddColumn XP integer NOT NULL default 0 + check constraint
                    src/.../Migrations/GameDbContextModelSnapshot.cs:240 (XP),
                      :252-254 (both Player check constraints)
                    src/.../Battle/BattleResultService.cs:319-322 grant bound to
                      firstDurableWrite; :394-429 GrantPlayerXpAsync; :403-411
                      two outcome amounts only
                    src/.../Repositories/BattleResultRepository.cs:82 returns
                      false for an existing row (no double grant), :91 true for
                      the first write.
                    Tests: PlayerXpProgressionTests.cs, PlayerXpBattleRewardTests.cs,
                    PlayerProgressionPersistenceTests.cs, PlayerXpSchemaTests.cs.
                    TASK-065 claims (migration name, first-write flag, 1557
                    tests) all confirmed against the tree.
    Impact:         none — this is the pattern the Pet grant mirrors
    Blocking?:      NO

F3 · TASK-063 M1 RESOLVED — no competing/dormant Pet Level derivation
    Question:       Q5
    Classification: ALREADY SUPPORTED
    Evidence:       Only writer of Pet.Level is the property setter
                    (Pet.cs:107). grep '\.Level\s*=|Level =' over src/ returns
                    no Pet Level assignment: the matches are Player.MinLevel/
                    MaxLevel/InitialLevel constants, Player.cs:273 (Player
                    Level), BoardResolver.cs:437-463 (cascade ChainLevel,
                    unrelated), and migration/comment text.
                    PetRepository.cs:13-18 — "derives no progression value …
                    neither recomputes Pet.Level nor exposes a bulk-save step".
                    PET_RULES.md:31-33 names this Pet's own XP as the sole
                    future source; §5.4 (:203) is the single formula.
    Impact:         the implementation adds the XP writer; no competing
                    calculation may be introduced
    Blocking?:      NO

F4 · TASK-063 M2 STILL OPEN (feature-shaped) — Pet.XP persistence absent
    Question:       Q3, Q4
    Classification: REQUIRES IMPLEMENTATION
    Evidence:       src/backend/GameServer.Domain/Pets/Pet.cs — members are
                    PetInstanceId(:60), PlayerId(:67), PetDefinitionId(:75),
                    Tier(:83), Star(:91), Level(:107), AcquiredAt(:113).
                    NO XP member.
                    src/.../Configurations/PetConfiguration.cs:35 — "There is
                    still no XP column on this table."
                    src/.../Migrations/GameDbContextModelSnapshot.cs:149-188 —
                    Pet mapped with Star/Level check constraints, no XP.
                    src/.../Migrations/20260924163011_AddPetPersistence.cs:31-60
                    — Pet table created without an XP column.
                    PetPersistenceTests.cs:57-67 asserts no XP field exists.
                    No Pet.XP migration exists (migration inventory: 8
                    migrations, AddPlayerXp and DropPetLevelMultiplier the only
                    new ones).
    Impact:         the implementation must add Pet.XP (int, NOT NULL,
                    default 0, [0,4900] per DATABASE.md §3:685-689), map it,
                    and add one migration. DATABASE.md §1:132-140 and
                    §3:685-689 already own the exact shape — nothing is
                    invented.
    Blocking?:      NO (absent feature = implementation work, not a blocker)

F5 · Pet XP ownership is the Pet instance — confirmed
    Question:       Q4
    Classification: ALREADY SUPPORTED
    Evidence:       src/backend/GameServer.Domain/Pets/Pet.cs:60-75 — the row
                    carries PetInstanceId/PlayerId/PetDefinitionId (ownership).
                    PET_RULES.md §5.1:131-133 — "Pet.XP is a per-instance
                    progression attribute stored on the owned Pet instance —
                    not on PetDefinition and not on the Player".
                    DATABASE.md §1:126-146 — the Pet block carries XP; the
                    PetDefinition block (:148-154) carries none.
                    PetDefinitionConfiguration.cs:23-24 — the retired field was
                    removed; no substitution added.
    Impact:         XP goes on Pet, never on PetDefinition or Player
    Blocking?:      NO

F6 · Pet Level formula / initial values / range conformance
    Question:       Q9
    Classification: ALREADY SUPPORTED
    Evidence:       PET_RULES.md §5.2:154-158 (XP 0 / Level 1),
                    §5.4:203 (min(floor(XP/100)+1,50)), §5.5:248-249
                    (Level max 50, XP max 4900).
                    DATABASE.md §3:685-695 states all six Pet constraints.
                    CK_Pet_Level_Range already exists and enforces [1,50]:
                    PetConfiguration.cs:112-114, migration
                    20260924163011_AddPetPersistence.cs:46, snapshot :183.
                    Worked boundaries (:208-216) give 0→1, 100→2, 4900→50.
                    The Player twin is already implemented identically
                    (Player.cs:234-246) — a proven local pattern, not a new
                    design.
    Impact:         the implementation writes the same formula shape for Pet
                    with its own constants; no shared abstraction
    Blocking?:      NO

F7 · Cap asymmetry is representable; no shared abstraction, no Player edit
    Question:       Q9, Q11
    Classification: ALREADY SUPPORTED
    Evidence:       PET_RULES.md §5.5:264-280 states the deliberate
                    divergence (Pet hard-capped 4900; Player uncapped).
                    PlayerConfiguration.cs:84-90 — Player XP required, NO
                    upper bound; PlayerXpSchemaTests.cs:109-112 asserts the
                    4900 cap must NOT appear on Player.
                    Player.cs:219-223 explicitly refuses to generalize the
                    shared formula shape into a shared abstraction.
                    ARCHITECTURE.md §5 / AGENTS.md §9 — anti-overengineering.
                    Separate tables/columns: no shared storage exists
                    (snapshot :149-249).
    Impact:         the Pet cap is a separate constraint on a separate column
    Blocking?:      NO

F8 · Battle reward integration point — single existing path
    Question:       Q6, Q8
    Classification: ALREADY SUPPORTED
    Evidence:       src/.../Application/Battle/BattleResultService.cs:209-347
                    PersistTerminalResultAsync — the canonical terminal path,
                    invoked once per terminal battle.
                    :306-308 AddAsync → the single durable write.
                    :319-322 the first-write gate where the Player grant runs.
                    :337 the sole DeleteAsync.
                    :87 in BattleResultRepository — the single SaveChangesAsync
                    transaction boundary.
                    BattleResultServiceTests.cs:113 and
                    BattleResultTerminalFlowTests.cs:175 assert the current
                    behaviour end-to-end.
    Impact:         the Pet grant extends this same method beside
                    GrantPlayerXpAsync — no new subsystem, service, or pipeline
    Blocking?:      NO

F9 · Active combat Pet identity is deterministic and in hand
    Question:       Q7
    Classification: ALREADY SUPPORTED
    Evidence:       src/.../BattleResultService.cs:218
                      var petInstanceId = state.PetState.PetId.Value;
                    :223 ArgumentException.ThrowIfNullOrWhiteSpace(petInstanceId)
                    GAME_STATE.md §2.3:889 (PetId on PetState) — one active Pet.
                    BattleStartService.cs:300 PetId: new PetId(pet.PetInstanceId)
                    — set at creation from the validated owned instance.
                    PET_RULES.md §5.3 item 1:181-184 — exactly one recipient.
                    PetPersistenceTests / PetOwnershipResolutionTests cover the
                    ownership resolution.
    Impact:         the Pet grant reads the same already-resolved identity —
                    exactly one recipient per battle, server-derived
    Blocking?:      NO

F10 · Exactly-once award is safe within the existing boundary
    Question:       Q8
    Classification: ALREADY SUPPORTED
    Evidence:       src/.../Repositories/BattleResultRepository.cs:59-83 — an
                    existing row for the battle's own key (BattleResultId =
                    BattleId) is not rewritten and reports false; :87 the
                    single SaveChangesAsync.
                    src/.../Battle/BattleResultService.cs:319-322 — the grant
                    is bound to that flag.
                    DATABASE.md §1 sourcing item 1 (:450-453) — one row per
                    battle; the primary key IS the guard.
                    TASK-065 residual finding 1 records the retry defect this
                    closed; BattleResultPersistenceTests.cs has the
                    first-durable-write case.
                    No new idempotency table, lock, ledger, or XP transaction
                    service exists or is required.
    Impact:         the Pet grant uses the same first-write binding the Player
                    grant already uses; no new transaction semantics
    Blocking?:      NO

F11 · TASK-063 M3 / F6 RESOLVED-AS-CLASSIFIED — RewardSummary delegated
    Question:       Q10, Q13
    Classification: ALREADY SUPPORTED (delegation confirmed; not a blocker)
    Evidence:       src/.../Battle/BattleResultService.cs:93
                      public const string EmptyRewardSummary = "{}";  :292 used.
                    DATABASE.md §1:539-543 (member list owned by this document;
                    staging value `{}`), :545-616 "Reward semantics for
                    RewardSummary":
                      · item 1 (:551-571) the four Player-track members ARE frozen
                      · item 2 (:573-596) "the Pet-track member list is deferred
                        to the implementation task, which will define it from the
                        now-decided semantics above"
                      · item 3 (:598-602) "no placeholder members"
                      · item 4 (:604-610) "the `{}` staging value remains the
                        contract in force"
                    API_CONTRACTS.md §4 note 1 (:547-559) and GAME_EVENTS.md §2
                    BattleWon/BattleLost item 2 (:464-473) both defer the Pet
                    member list and freeze no Pet field.
                    ADR-016 Consequences (:212-215) — "a representation
                    decision, not an open gameplay decision".
                    BattleResultServiceTests.cs:273-292 asserts `{}` today.
                    Verdict: the authoritative contract EXPLICITLY requires the
                    representation to be decided by the implementation task. It
                    does NOT require Pet XP to be represented in RewardSummary by
                    any named field — so no field need be invented, and the
                    documentation is not silent: it directs when and by whom the
                    decision is made.
    Impact:         the implementation task owns the Pet member-list
                    representation decision (DATABASE.md §1 item 2). The audit
                    does not choose it and does not treat the current `{}` as a
                    violation.
    Blocking?:      NO (delegated representation decision, not a missing contract)

F12 · TASK-063 M4 RESOLVED — retry/reconcile cannot double-award
    Question:       Q8
    Classification: ALREADY SUPPORTED
    Evidence:       BattleResultRepository.cs:59-83 + test
                    BattleResultPersistenceTests.cs (first-durable-write case);
                    TASK-065 Completion Evidence residual finding 1 records the
                    pre-existing rewrite branch and its fix inside TASK-065.
    Impact:         none — the Pet grant inherits the fixed boundary
    Blocking?:      NO

F13 · TASK-063 F0 — lifecycle inconsistencies (report-only, unchanged)
    Question:       Q16
    Classification: ALREADY SUPPORTED (report-only; not repaired)
    Evidence:       git status shows
                    tasks/backlog/TASK-059-*, TASK-061-*, TASK-062-*,
                    TASK-063-*, TASK-064-* (untracked) and
                    tasks/blocked/TASK-060-* (untracked);
                    tasks/completed/TASK-065-* (untracked).
                    TASK-063 and TASK-064 are physically in backlog/ with
                    Status: BACKLOG in metadata although their Completion
                    Evidence records DONE (TASK-064:768 "DONE").
                    Per TASK_LIFECYCLE.md §4 a DONE file belongs in
                    tasks/completed/. Recorded as a finding; NOT repaired
                    (§5 Out of Scope; AGENTS.md §16).
    Impact:         none on Pet XP readiness
    Blocking?:      NO

F14 · Pet progression is IN MVP scope; nothing OUT is implied
    Question:       Q16
    Classification: ALREADY SUPPORTED
    Evidence:       docs/00-overview/MVP_SCOPE.md §1 "Pets" :49-57 — "Tier,
                    Star, Level progression (Pet XP / Pet Level — the Pet
                    instance's own progression … Pet Level range 1–50, Pet XP
                    hard-capped at 4900)".
                    §2 OUT (:93-109) lists no progression, XP, or Pet system.
                    No item in the Pet XP contract implies Gacha, PvP, Trading,
                    Evolution (§5.6 item 4 forbids Evolution), Prestige, or
                    Paragon (PET_RULES.md §5.5 item 3).
    Impact:         none — no scope change
    Blocking?:      NO

F15 · TASK-063 m1/m2/m3 — stale-artifact cleanups all resolved
    Question:       Q1, Q12, Q15
    Classification: ALREADY SUPPORTED
    Evidence:       m1: PetLevelDerivationTests.cs and PetLevelRecomputeTests.cs
                    no longer exist (glob of tests/backend/**/*.cs returns
                    neither; git status shows both " D").
                    m2: the stale comments were rewritten to state the
                    retirement — Pet.cs:24-38,99-105 · PetConfiguration.cs:28-35 ·
                    PetDefinitionConfiguration.cs:23-24 · PetRepository.cs:13-18 ·
                    IPetRepository.cs:20-21.
                    m3: PetRepository.cs:13-18 and IPetRepository.cs:20-25
                    document the removal and expose no recompute surface.
                    Remaining mentions are historical/comment-only (F1).
    Impact:         none
    Blocking?:      NO

F16 · Player track is untouched by Pet XP work
    Question:       Q11, Q12
    Classification: ALREADY SUPPORTED
    Evidence:       Player.cs:95 BattleWonXpReward=100, :104 BattleLostXpReward=0,
                    :83 XpPerLevelCurveConstant=100, :155 XP uncapped,
                    :234-246 LevelForXp. COMBAT_RULES.md §7.2:258-261,
                    §7.5:318-334 (uncapped XP, capped Level, no post-50 system).
                    BattleResultService.cs:68-69 — "must not touch Pet
                    progression"; :377-380 — GrantPlayerXpAsync touches no
                    Pet.XP and no Pet Level.
                    The Pet contract restates this independence
                    (PET_RULES.md §5.1 items 3/5, §5.3:196-198).
    Impact:         zero required edits to Player XP values, rewards, uncapped
                    rule, Level formula, or TASK-059/TASK-065 outputs
    Blocking?:      NO

F17 · No Redis / SignalR / frontend change required
    Question:       Q13, Q14
    Classification: ALREADY SUPPORTED
    Evidence:       GAME_STATE.md §2.3:887-908 — PetState carries PetId,
                    Element, Tier/Star/Level, combat stats, loadouts, PassiveId.
                    NO Pet XP member, and PET_RULES.md §5.1 items 1/6 plus
                    DATABASE.md §3:711-718 place Pet.XP in persistent storage,
                    not active battle state.
                    REDIS_STATE.md:310-324 — Match/Combo and PetState members
                    add no key; :324 explicitly records that there is no
                    `battle:{battleId}:progression` key and no per-player key.
                    GAME_EVENTS.md §2 — the terminal events carry Outcome +
                    reward summary; no Pet XP event exists or is required.
                    API_CONTRACTS.md §5:609-627 — /api/pets already returns
                    level; no Pet XP wire field is required by any document.
    Impact:         none — no BattleState.PetXP, no new event, no new Redis
                    key, no frontend work
    Blocking?:      NO

F18 · Documentation is internally consistent after TASK-062
    Question:       Q15
    Classification: ALREADY SUPPORTED
    Evidence:       PET_RULES.md §5.1–§5.7 (ownership, initial, targeting,
                    formula, cap, retired terms, non-XP attributes) ↔
                    COMBAT_RULES.md §7.1–§7.6 (two tracks, +100/+0, curve vs
                    reward, uncapped, no combat stats) ↔ ADR-016 items 1–13 +
                    the TASK-062 Amendment (:96-133) ↔ docs/03-decisions/README.md
                    :151-198 (ADR-011 item 7 / ADR-012 items 3,4,6 partial
                    supersession; "ADR-016 item 14 is now satisfied") ↔
                    DATABASE.md §1:111-146, §3:677-718 ↔
                    API_CONTRACTS.md §4 note 1 ↔ GAME_EVENTS.md §2 ↔
                    GAME_STATE.md §2.3 ↔ MVP_SCOPE.md §1.
                    Retired terms are marked retired and not reused:
                    PET_RULES.md §5.6:282-300, ADR-016 item 13:81-85,
                    ADR-016 Option C:150-154.
                    No document conflicts were found; nothing was resolved.
    Impact:         none — no documentation change is authorized or needed
    Blocking?:      NO

F19 · Test readiness — patterns exist; gaps enumerated, no test touched
    Question:       Q12
    Classification: ALREADY SUPPORTED
    Evidence:       Available templates:
                      · Domain formula/boundaries:
                        PlayerXpProgressionTests.cs
                      · Application reward + exactly-once + retry:
                        PlayerXpBattleRewardTests.cs (460 lines; harness with
                        InMemoryPlayerRepository, ScriptedBossDefinitionLookup)
                      · Persistence boundary + reload:
                        PlayerProgressionPersistenceTests.cs
                      · APPLIED PostgreSQL schema + migration history:
                        PlayerXpSchemaTests.cs:119-183
                      · Field-set / model-shape: PetPersistenceTests.cs:40-81,
                        :168-178
                    Retired suites confirmed absent (F15/m1).
                    Gaps the Pet implementation must fill (no test added here):
                      1. Pet.XP initial value 0 and Pet.Level initial 1
                      2. Pet Level formula boundaries 0/99/100/4899/4900
                      3. BattleWon → active Pet +100 / BattleLost → +0
                      4. inactive owned Pets receive +0 (one-recipient rule)
                      5. hard cap 4900: not awarded, not stored, no overflow
                      6. Player/Pet pool independence (neither reads the other)
                      7. Pet grant exactly-once across a retried terminal result
                      8. applied-schema assertion for the new Pet.XP column,
                         its default, and its [0,4900] constraint
                    PetPersistenceTests.cs:57-67 currently ASSERTS that Pet has
                    no XP field — the implementation task must update that
                    assertion, not delete the contract.
    Impact:         the implementation extends the existing suites/patterns;
                    the audit adds, modifies, and runs no test
    Blocking?:      NO

F20 · Pet.Level existing-row / initial-state semantics are ANSWERED
    Question:       Q3 (backfill)
    Classification: ALREADY SUPPORTED
    Evidence:       DATABASE.md §3:689 states "Pet.XP = 0 for a newly created
                    PlayerPet (PET_RULES.md §5.2)"; §1:132-140 states XP is
                    "int, NOT NULL, default 0"; PET_RULES.md §5.2:154-161
                    states the initial pair and that XP=0 yields Level 1 "by
                    construction".
                    The Pet.Level column is NOT NULL with NO database default
                    (migration 20260924163011_AddPetPersistence.cs:40;
                    PetConfiguration.cs:103-104 maps it required without
                    HasDefaultValue) — unlike Player.XP, which TASK-065 gave a
                    default (PlayerConfiguration.cs:86).
                    Consequence, from the documents alone: adding Pet.XP with
                    the documented default 0 leaves every existing Pet row at
                    XP = 0, which the documented formula maps to Level 1. No
                    Level → XP conversion is defined by any document, and none
                    is needed: the documented default fully determines the
                    existing-row outcome. There is no unresolved backfill rule.
                    (No Pet-creation path exists in src/ — IPetRepository.AddAsync
                    has no production caller — so the population of existing
                    rows is a test/deployment concern, not a contract gap.)
    Impact:         the implementation adds Pet.XP with the documented default
                    0 and backfills nothing; no undocumented transformation
    Blocking?:      NO

F21 · TASK-063 F3/F4/F5/F7-F12 re-classification
    Question:       Q6, Q7, Q8, Q9, Q10
    Classification: ALREADY SUPPORTED (all)
    Evidence:       F3 active-Pet determinism → F9 above (PASS).
                    F4 battle reward integration point → F8 above (PASS).
                    F5 cap asymmetry → F7 above (PASS).
                    F6 RewardSummary member list → F11 above (delegated, PASS).
                    F7 transaction boundary / duplicate reward → F10, F12
                      above (PASS).
                    F8 Player.XP entity-member gap → F2 above (RESOLVED).
                    F9 test-infrastructure readiness → F19 above (PASS).
                    F10 frontend scope → F17 above (no change).
                    F11 Redis/SignalR scope → F17 above (no change).
                    F12 (battle-end architecture) → F8/F9 above (PASS).
                    F1 Pet XP/Pet Level persistence gap → F4 above: still an
                      absent feature (REQUIRES IMPLEMENTATION), not a blocker.
                    F2 PetLevelMultiplier in active code → F1 above (RESOLVED).
    Impact:         none
    Blocking?:      NO
```

### Implementation-Task Input Set (required if verdict = READY)

```text
Persistence:
  - Domain: add `public int XP { get; set; } = InitialXp;` to
    src/backend/GameServer.Domain/Pets/Pet.cs (mirroring Player.cs:155),
    with the documented initial-0 constant, the 4900 hard-cap constant, and
    the Pet Level formula as a pure function (mirroring Player.LevelForXp).
  - Infrastructure: map Pet.XP in
    src/backend/GameServer.Infrastructure/Postgres/Configurations/
    PetConfiguration.cs as required with HasDefaultValue(0), and add a
    [0,4900] check constraint (DATABASE.md §3:685-689). Retain
    CK_Pet_Level_Range and CK_Pet_Star_Range unchanged.
  - One new EF migration adding Pet.XP (integer NOT NULL DEFAULT 0) plus the
    range constraint, scoped to the Pet table only; regenerate
    GameDbContextModelSnapshot.cs. Do NOT edit historical migrations.

Domain:
  - XP member + Level derivation on the Pet instance. Pet Level becomes
    min(floor(Pet.XP / 100) + 1, 50) (PET_RULES.md §5.4:203).
  - A grant method that adds the outcome amount and re-derives Level,
    modeling Player.GrantBattleXp (Player.cs:263-274), with the cap enforced
    on write: XP above 4900 is not awarded, not stored, no overflow
    (PET_RULES.md §5.5:252-257).
  - Pet Level has exactly ONE runtime source: Pet.XP. Do not restore
    Player.Level × PetLevelMultiplier and do not add a temporary writer.

Application:
  - Extend the existing single terminal path
    BattleResultService.PersistTerminalResultAsync
    (BattleResultService.cs:209) with a Pet grant beside GrantPlayerXpAsync
    (:394), gated by the same firstDurableWrite flag (:319). No second
    battle-resolution path.
  - Recipient identity is the already-resolved petInstanceId
    (BattleResultService.cs:218 = state.PetState.PetId.Value) — exactly one
    recipient per battle.
  - Requires a Pet progression write boundary analogous to
    IPlayerRepository.SaveProgressionAsync; the existing AddAsync-only
    surface (IPetRepository.cs:37) is the only Pet write today.

API/Events:
  - The RewardSummary Pet member list is the implementation task's own
    representation decision, explicitly delegated by DATABASE.md §1 item 2
    (:573-596) from the semantics fixed by PET_RULES.md §5.3–§5.5. Populating
    it (and with it the Player-track members already frozen by DATABASE.md §1
    item 1) replaces the EmptyRewardSummary constant (:93/:292) when the
    implementation lands.
  - No new endpoint, event, or wire field is required by any authoritative
    document. API_CONTRACTS.md §4 and GAME_EVENTS.md §2 need no change.

Transaction:
  - Existing boundary only: the single SaveChangesAsync
    (BattleResultRepository.cs:87) plus the first-durable-write flag (:82/:91).
    No new idempotency table, distributed lock, event-sourcing system, XP
    ledger, or XP transaction service.

Tests:
  - Extend the TASK-065 patterns: PlayerXpProgressionTests.cs (Domain),
    PlayerXpBattleRewardTests.cs (Application reward/exactly-once),
    PlayerProgressionPersistenceTests.cs (persistence),
    PlayerXpSchemaTests.cs (applied schema), and PetPersistenceTests.cs
    (field set / constraints).
  - Cover the eight gaps enumerated in finding F19, including the 4900 cap
    behavior and the inactive-Pet +0 case.
  - PetPersistenceTests.cs:57-67 must be updated — it currently asserts Pet
    has no XP field.

Frontend:
  - None. No authoritative contract requires Pet XP exposure.

Redis/SignalR:
  - None. Pet.XP is persistent per-instance progression, not BattleState
    (PET_RULES.md §5.1, DATABASE.md §3:711-718, REDIS_STATE.md:310-324).

Out of scope (must NOT be touched):
  - Player XP values, rewards, uncapped rule, Level formula, TASK-059 and
    TASK-065 outputs.
  - Match-3, Board, Gems, Swap, Cascade, combat formulas, Boss, Cards,
    Relics, Passives, Skills, Evolution, Tier, Star, Gacha.
  - Rewards beyond the documented XP; progression beyond XP/Level.
  - Frontend gameplay; Redis BattleState; SignalR protocol.
  - docs/ (no documentation change is required or authorized).
  - PetLevelMultiplier in any form (PET_RULES.md §5.6:284-289, ADR-016 item 13).
```

### STOP CONDITION (required if verdict = NOT IMPLEMENTATION-READY)

```text
STOP CONDITION

Problem:
<one or two sentences>

Relevant sources:
<file paths + section/line references, both/all sides if a conflict>

Conflict / missing information:
<what exactly is unresolved>

Proposed resolution:
<the smallest prerequisite/decision task — named, not created>

Waiting for:
<what kind of approval/input is needed to proceed>
```

### Changed Files

- `tasks/backlog/TASK-066-pet-xp-implementation-readiness-reaudit.md` — this
  re-audit's verdict and findings only. **No other file was modified.**
  No source, test, migration, documentation, or other task file was
  created, modified, moved, or deleted.

### Validation Results

Read-only inspection and search; no test was executed (task §8).

```text
PetLevelMultiplier|PetLevelDerivation|PetLevelService over src/   → 29 matches,
    all non-live: 8 in immutable migration files
    (20260924163011_AddPetPersistence.cs :21,:28 + 6 *.Designer.cs) and 21 in
    explanatory comments across Pet.cs, PetConfiguration.cs,
    PetDefinitionConfiguration.cs, PetRepository.cs, IPetRepository.cs.
    Zero live code references (no property, mapping, constraint, registration,
    or caller).
PetLevelMultiplier|PetLevelDerivation|PetLevelService over tests/ → 5 matches,
    all comments/assertions recording the retirement
    (PetPersistenceTests.cs:98-99, PlayerXpSchemaTests.cs:167,171,177).
    PetLevelDerivationTests.cs and PetLevelRecomputeTests.cs → 0 files (deleted).
Pet Pet.cs properties → PetInstanceId, PlayerId, PetDefinitionId, Tier, Star,
    Level, AcquiredAt; NO XP member.
Player Player.cs → XP present at :155 with InitialXp default; LevelForXp :234;
    GrantBattleXp :263.
Pet.Level writers across src/ → 0 production assignments; the only live
    assignment is Player.cs:273 (Player.Level). PetRepository derives no
    progression value.
PersistTerminalResultAsync → BattleResultService.cs:209; petInstanceId at :218;
    first-write gate at :319; GrantPlayerXpAsync at :394; EmptyRewardSummary
    at :93 used at :292 (still the {} staging value).
First-write boundary → BattleResultRepository.cs:49 AddAsync, :82 false for an
    existing row, :87 the single SaveChangesAsync.
PetState / BattleState members → no Pet XP field; PetState carries PetId,
    Element, Tier/Star/Level, combat stats, loadouts, PassiveId
    (GAME_STATE.md §2.3; PetState.cs:350-351).
Migrations inventory → 8 migrations. DropPetLevelMultiplier and AddPlayerXp
    present; NO Pet.XP migration exists.
Pet.XP column → absent from PetConfiguration.cs, from
    GameDbContextModelSnapshot.cs:149-188, and from migration
    20260924163011_AddPetPersistence.cs.
Pet.Level column → NOT NULL, no database default
    (20260924163011_AddPetPersistence.cs:40; PetConfiguration.cs:103-104);
    CK_Pet_Level_Range [1,50] enforced (:112-114).
git status --porcelain → shows the pre-existing TASK-059…065 working-tree
    changes only; no file touched by this task except this task file.
```

### Scope Verification

- [x] No source code modified (`git status -- src/` unchanged by this task)
- [x] No test modified or executed for validation
- [x] No migration created or modified
- [x] No documentation file modified (`git status -- docs/` unchanged by
      this task)
- [x] TASK-059 / TASK-060 / TASK-061 / TASK-062 unmodified
- [x] TASK-063 / TASK-064 / TASK-065 unmodified
- [x] No task file moved or re-statused (lifecycle note recorded, not
      repaired)
- [x] No implementation task created (no TASK-067)
- [x] No gameplay decision made
- [x] No architecture decision made
- [x] Nothing committed or staged
