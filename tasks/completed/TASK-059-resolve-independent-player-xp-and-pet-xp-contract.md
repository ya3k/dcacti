# TASK-059 — Resolve the Independent Player XP + Pet XP Progression Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; copies only the human decisions that
  are this task's input (they exist nowhere in docs/ yet).
  Created from the two-track Player XP / Pet XP design change request. This
  task REPLACES the single-track contract that tasks/backlog/TASK-058
  proposed; TASK-058 is Superseded (§12).

  THIS TASK IS DOCUMENTATION-ONLY. It resolves and synchronizes a gameplay
  contract. It does NOT implement source code and does NOT create the
  implementation task.
-->

---

## Metadata

```text
Task ID:           TASK-059
Type:              DOCUMENTATION
Status:            DONE
Risk:              HIGH (retires a documented derivation model that is
                   currently implemented and covered by tests, and
                   supersedes two existing records: the PET_RULES.md §5
                   formula, ADR-012 items 3/4/6, ADR-011 item 6, and the
                   TASK-058 contract. tasks/TASK_TYPES.md §4 caps
                   DOCUMENTATION at MEDIUM for "affects a cross-referenced
                   contract"; raised to HIGH because it additionally
                   retires an Accepted ADR decision and leaves the
                   repository in a deliberate documentation-vs-code
                   divergence state until a follow-up implementation task
                   lands — §16 makes that divergence an explicit,
                   tracked deliverable rather than a silent one)
Priority:          HIGH (unblocks TASK-033 and clears the TASK-058
                   contradiction)
Primary Agent:     review
Supporting Agents: gameplay (domain-rule content accuracy — owns
                   PET_RULES.md / COMBAT_RULES.md content),
                   persistence (XP column contracts in DATABASE.md),
                   backend (RewardSummary + API/event alignment)
Workflow:          documentation/documentation-change.md
                   + architecture/adr-change.md (§9 — mandated ADR-016)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation,
                   quality/architecture-conformance (5 skills — Complex
                   budget ceiling is 7, tasks/README.md §12)
Dependencies:      TASK-023 (DONE — Player entity + Player.Level
                   persistence; read as a constraint, AGENTS.md §16),
                   TASK-024 (DONE — Pet entity + Pet.Level + the
                   PetLevelMultiplier mechanism this task retires; read
                   only, must not be modified)
Blocks:            TASK-033 (superseded by this contract — §10),
                   TASK-058 (superseded by this task — §12)
Estimate:          Complex (5 skills; documentation-only, zero code; the
                   Pet XP balance values in §7 are UNRESOLVED and are the
                   expected outcome of this task, not an input)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE`:
`tasks/TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change that is
"propagat[ed] … through implementation and tests", and
`.ai/workflow/development/gameplay-change.md` §3 ends in
`core/implementation.md` → `quality/testing.md`. This task's scope (§4)
forbids all code, so that workflow cannot complete. The gameplay
implementation is a **separate follow-up task generated only after this one
is DONE** (§10). The precedent set by TASK-045/046/048/050/051/056/058 — all
contract-formalization tasks typed `DOCUMENTATION` — governs here. The
domain rule content is still reviewed for accuracy by the `gameplay`
supporting agent.

**Status note.** `BACKLOG`, not `BLOCKED`: per `tasks/README.md` §6 the file
lives in `backlog/`; per `TASK_LIFECYCLE.md` §3 it may move `BACKLOG →
READY` once an orchestrator confirms the §3 READY criteria. The Pet XP
balance values being unresolved (§7) does **not** make this task BLOCKED —
recording them as unresolved human decisions *is* this task's deliverable.
The task would only become BLOCKED if execution tried to invent them.

---

## 1. Objective

Resolve and synchronize the **independent two-track progression model** in
the owning documents, so that `Player.XP → Player.Level` (account/content
progression) and `Pet.XP → Pet.Level` (combat-character progression) are
each explicitly defined, and `Pet.Level` is **no longer derived from
`Player.Level`**. The outcome is a citable document section for every
decided item, an explicit and complete list of the **still-unresolved Pet XP
balance decisions** that block implementation, and a single new ADR
recording the ownership change. **No source code, no migration, no
implementation.**

---

## 2. Human Decisions — Authoritative Input (product owner)

These are decisions, not proposals. Write them into the owner documents per
§6. Do not re-derive, adjust, round, or replace any Player value below.

### 2.1 Player progression — DECIDED

```text
Player has persistent XP.
Player has Level.
Player Level range = [1, 50].
Player Level is independent from Pet Level.
Player Level provides NO combat stats.
XP itself is NOT capped. Only Level is capped at 50.
```

### 2.2 Player XP reward and curve — DECIDED

```text
BattleWon   → Player XP +100
BattleLost  → Player XP +0

Level = min(floor(XP / 100) + 1, 50)

XP 0     → Level 1
XP 100   → Level 2
XP 400   → Level 5
XP 4900  → Level 50
XP 5000  → Level 50
XP 10000 → Level 50
```

- Both the reward amount and the curve constant are `100`. They are **two
  independent values** and must be documented as such (§6.3), so that tuning
  the reward never silently rewrites the curve.
- `Player.XP` is **NOT** capped at 4900; XP keeps accumulating after Level
  50 as cumulative/lifetime progression.
- Do **not** add Prestige, Paragon, Season XP, or any post-50 progression.

### 2.3 Player persistence — DECIDED

```text
Player.XP   int   NOT NULL   default 0   (persisted Player state)
Initial Player XP    = 0
Initial Player Level = 1
```

### 2.4 Pet progression — STRUCTURE DECIDED, BALANCE UNRESOLVED

**Structure — confirmed by the product owner (§6):**

```text
Pet has its own XP.
Pet has its own Level.
Pet Level is independent from Player Level.
Pet Level = function(Pet.XP).
Pet XP belongs to the Pet INSTANCE.
PetDefinition does not own Pet instance XP.
```

**Balance — NOT decided anywhere in the repository.** §7 lists the twelve
open items. This task records them as **unresolved human gameplay
decisions**; it must not invent, infer, copy, or placeholder them.

Explicitly forbidden sources for Pet XP values:

```text
the Player XP values (§2.2)         — do NOT copy across
TASK-024                            — historical; not a Pet XP decision
PetLevelMultiplier                  — do NOT infer Pet XP from it
implementation convenience          — never a gameplay decision
```

### 2.5 Ownership model — DECIDED

```text
Player XP / Level   →  account / meta / content progression
Pet XP / Level      →  combat-character progression
```

Player = account/owner; Pet = combat character. The two tracks are
independent. No Player combat-stat system exists:
`PlayerAttack`, `PlayerDefense`, `PlayerHP`, `PlayerCrit`, `PlayerPower` and
equivalents must not appear.

### 2.6 Pet battle reward targeting — NOT DECIDED

The proposed semantics:

```text
BattleWon  →  Player receives Player XP  +  active combat Pet receives Pet XP
Other owned Pets  →  0 XP
```

are **not authoritative anywhere in the repository** and must **not** be
assumed. They must be recorded as unresolved human gameplay decisions (§7
items 5–8, which jointly define Pet battle reward targeting — active-Pet-only
scope, inactive-Pet treatment, and both outcome amounts), never decided on
implementation-convenience grounds.

### 2.7 Owner documents

| Concept | Owner |
|---|---|
| Player XP → Level progression (reward + curve) | `docs/01-game-design/COMBAT_RULES.md` (new smallest section) |
| Pet XP → Pet Level progression | `docs/01-game-design/PET_RULES.md` §5 (rewrite) |
| Player XP / Pet XP persistence | `docs/02-technical/DATABASE.md` §1 (+ §3 constraints) |
| `RewardSummary` two-track member list | `docs/02-technical/DATABASE.md` §1 / §5 item 3 |
| Decision rationale | new ADR-016 (§9) |

The Player XP section MUST distinguish the **reward amount** from the
**progression curve constant** (§2.2).

### 2.8 Required reading before any change

```text
AGENTS.md

.ai/README.md
.ai/agents/orchestrator.md · backend.md · client.md · realtime.md ·
    testing.md · review.md

.ai/workflow/documentation/documentation-change.md
.ai/workflow/architecture/adr-change.md
.ai/workflow/core/validation.md
.ai/workflow/quality/review.md

tasks/README.md · TASK_TYPES.md · TASK_LIFECYCLE.md
tasks/backlog/ · tasks/active/ · tasks/blocked/ · tasks/completed/

docs/01-game-design/PET_RULES.md · GAME_RULES.md · COMBAT_RULES.md
docs/02-technical/DATABASE.md · API_CONTRACTS.md · GAME_EVENTS.md ·
    GAME_STATE.md · ARCHITECTURE.md · TDD.md
docs/00-overview/GDD.md · MVP_SCOPE.md · ROADMAP.md
docs/03-decisions/README.md · docs/03-decisions/ADR/

Implementation history (HISTORICAL CONTEXT ONLY — do not modify):
TASK-023 · TASK-024 · TASK-025 · TASK-032 · TASK-037 · TASK-058
```

---

## 3. Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Player Level (1–50) IN; Pet Level
  currently `clamp(floor(Player Level × Pet Level Multiplier), 1, 50)`,
  "no Pet XP"; §2 OUT list; §4 FUTURE-by-default rule
- `docs/00-overview/GDD.md` §14 — Meta Progression; Player Level increases
  through battle Rewards; §6 Pet Level currently "derived from the
  account's Player Level"
- `docs/00-overview/ROADMAP.md` Phase 3 — balance pass on configurable values
- `docs/01-game-design/PET_RULES.md` §5 (items 1–10), §5.1, §6 — **the
  current Pet Level derivation contract this task retires**
- `docs/01-game-design/GAME_RULES.md` §9.3 (Pet Level formula; "There is no
  independent Pet XP system"), §20 (Rule Change Policy), §21 (hierarchy)
- `docs/01-game-design/COMBAT_RULES.md` — target owner for the XP/Level
  section (currently §1–§7; no XP/Level content)
- `docs/02-technical/DATABASE.md` §1 (Player/Pet/PetDefinition schemas,
  `RewardSummary`), §3 (constraints), §5 item 3 (`RewardSummary` ownership)
- `docs/02-technical/API_CONTRACTS.md` §4 + notes 1/3 (`rewards` always
  present, both outcomes)
- `docs/02-technical/GAME_EVENTS.md` §2 (`BattleWon`/`BattleLost` payload
  rule)
- `docs/02-technical/GAME_STATE.md` §2.3 (no Player combat pool)
- `docs/03-decisions/ADR/ADR-011*` item 6, `ADR-012*` items 1–6 +
  Consequences — **both explicitly close Pet XP and own the derivation this
  task retires**; `ADR-014*` (`RewardSummary` ownership reference)
- `docs/03-decisions/README.md` §5, §7, §8 — ADR status rules and index
- `AGENTS.md` §4, §7, §16, §17, §18, §20; `tasks/TASK_LIFECYCLE.md` §3;
  `.ai/workflow/architecture/adr-change.md` §2, §3, §4

---

## 4. Scope

### In Scope

1. Add the Player XP / Level progression section to `COMBAT_RULES.md`
   (§2.1–§2.3, §2.7) with the reward-vs-curve distinction (§6.3).
2. Rewrite `PET_RULES.md` §5 to define Pet XP / Pet Level as an independent
   track fed by `Pet.XP`, retiring the `Player.Level × PetLevelMultiplier`
   derivation; resolve the fate of `PetLevelMultiplier` (§6.2).
3. Define `Player.XP` and the Pet-instance `XP` in `DATABASE.md` §1 + §3
   (`int`, NOT NULL, default, level relationship, post-50 accumulation
   semantics), preserving all existing fields and constraints.
4. Define the two-track `RewardSummary` shape (§6.4) and align
   `API_CONTRACTS.md` §4 note 1/note 3 and `GAME_EVENTS.md` §2 so REST and
   event semantics do not contradict each other.
5. Update `MVP_SCOPE.md` §1 and `GDD.md` §6/§14 where the stale derivation
   and "no Pet XP" statements appear.
6. Create ADR-016 recording the ownership change and add it to
   `docs/03-decisions/README.md` §7; set the superseded ADR-012 items per
   §9.3.
7. Perform the repository-wide stale-reference consistency audit (§8) and
   record remaining source-code references as implementation follow-up.
8. Record the unresolved Pet XP balance decisions (§7) explicitly in the
   owning docs and in this task's Completion Evidence.

### Out of Scope — MUST NOT be implemented by this task

```text
Any source code (src/backend/, src/frontend/) · any test change (tests/) ·
any EF Core entity/configuration change · any database migration ·
Redis / SignalR / Phaser / React changes · XP calculation code ·
Level calculation code · battle reward application · Reward persistence ·
Reward API implementation · Match-3 / board / gems / swap / cascade ·
combat damage · Boss / Card / Relic / Passive gameplay ·
any progression system beyond the two XP tracks ·
XP potions / training items / ascension / talents / evolution / prestige /
multiple XP currencies / rest systems / energy systems / complex
progression trees · Boss-tier XP scaling, multipliers, boosts, rested XP,
quests · XPHistory / PlayerXPLog / RewardHistory / LevelHistory tables ·
any MVP_SCOPE.md §2 OUT item
```

Also out of scope: creating the XP implementation task (§10) or the TASK-033
replacement, editing TASK-033, editing TASK-024/TASK-037 or any completed
task (`AGENTS.md` §16, `tasks/TASK_LIFECYCLE.md` §3 "DONE → any" invalid).

---

## 5. Current State (verified at creation)

- **The old single-track model is authoritative and implemented.**
  `PET_RULES.md` §5 item 1 + `GAME_RULES.md` §9.3 + `ADR-012` item 3 define
  `Pet.Level = clamp(floor(Player.Level × PetDefinition.PetLevelMultiplier), 1, 50)`.
  It is realized in `src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs`,
  `src/backend/GameServer.Application/Pets/PetLevelService.cs`, and
  `PetConfiguration.cs` ("There is no XP column").
- **No XP contract exists in `docs/`.** `Player` is `PlayerId`,
  `DiscordUserId`, `Level`, `CreatedAt` — no XP column; `Pet` is
  `PetInstanceId`, `PlayerId`, `PetDefinitionId`, `Tier`, `Star`, `Level`,
  `AcquiredAt` — no XP column; `DATABASE.md` §1 explicitly calls
  `Pet.Level` "not an independent XP store".
- **Pet XP is explicitly closed in four places** (`PET_RULES.md` §5 item 4,
  `GAME_RULES.md` §9.3, `MVP_SCOPE.md` §1, `ADR-011` item 6 / `ADR-012`
  item 6). Retiring these is a Rule Change Policy action (`GAME_RULES.md`
  §20) authorized by §2.4 of this task.
- **`RewardSummary` is in staging state:** JSON NOT NULL, value `{}`,
  `rewards` always present for both outcomes (`API_CONTRACTS.md` §4 note 1),
  member list currently assigned to TASK-033 (`DATABASE.md` §5 item 3).
- **`COMBAT_RULES.md` has §1–§7 with no XP/Level content.**
- **Highest task ID is 058 → this task is TASK-059.**
- **Highest ADR is ADR-015** (ADR-013 is the Discord identity exchange
  contract, *not* XP) → **the next free ADR number is ADR-016.**

---

## 6. Required Decisions & Owner Assignment

### 6.1 Decided vs unresolved

```text
DECIDED (§2)        Player XP persistence, reward, curve, cap semantics,
                    ownership model, RewardSummary two-track intent
UNRESOLVED (§7)     every Pet XP balance value, and the Pet XP reward
                    semantics (active-Pet-only, inactive Pets, cap, curve,
                    initial values, post-50 accumulation)
```

For every **decided** item this task writes the contract. For every
**unresolved** item this task writes only the *question*, marked as a human
gameplay decision, in the owning document and in §7/§13. **No placeholder
values. No "configurable placeholder" substitutes for a missing gameplay
decision.**

### 6.2 `PetLevelMultiplier` — resolve to exactly one outcome

The new model must explicitly choose **exactly one** outcome, and state it
in `PET_RULES.md` §5 and `DATABASE.md` §1:

```text
Option A — RETIRED
    The old derivation Player.Level × PetLevelMultiplier is retired.
    PetLevelMultiplier has NO role in the new Pet XP model.
    Remove it from the documented PetDefinition contract, and record its
    source-code removal as a later implementation task (= follow-up, §8).

Option B — RETAINED
    Permitted ONLY if an existing authoritative gameplay rule or an
    explicit human decision establishes a legitimate Pet-XP-related
    purpose. State that purpose explicitly.
```

Forbidden:

```text
retaining it merely because it already exists in source code
silently reinterpreting it as an XP curve multiplier
retaining it while leaving its purpose undocumented
```

**If neither outcome can be determined from authoritative rules plus
explicit human input → STOP (§14).** Do not guess. Expected resolution is
Option A, but the executing agent must confirm rather than assume.

### 6.3 Reward amount vs curve constant

`COMBAT_RULES.md` must document these as **two independent concepts**
(`§2.2`), so that changing the reward later does not silently redefine the
progression curve:

```text
Player XP reward amount      = 100   (MVP initial value; a CONFIGURATION value)
XP-per-level curve constant  = 100   (the progression FORMULA constant)
```

Both currently equal `100`. They are nonetheless independent and must be
written as such. The reward is configuration with MVP initial value `100 XP`;
the curve constant is part of the gameplay formula.

### 6.4 `RewardSummary` — Player contract vs Pet semantics

`DATABASE.md` owns the member list. The contract must keep two things
distinct and must **not** freeze invented Pet semantics:

```text
Player reward contract   →  finalizable NOW from §2.1–§2.3
Pet reward semantics     →  DEPENDENT on unresolved §7 items
```

- Define only the `RewardSummary` structure **justified by the authoritative
  contract**.
- **Do not invent Pet reward field names merely to fill a schema.**
- If the exact Pet-track members cannot be finalized without deciding §7
  (especially item 5 Pet XP per BattleWon, items 7–8 active-Pet-only, item 6
  BattleLost behavior), **explicitly document that dependency** in
  `DATABASE.md` and in §7/§13.
- **Do not turn unresolved gameplay decisions into placeholder API fields.**

The illustrative fields:

```text
playerXpGained · playerLeveledUp · newPlayerLevel ·
petXpGained · petLeveledUp · newPetLevel · petId
```

are **ONLY an example — not the final set.** Then update the `DATABASE.md` §5
item 3 handoff, `API_CONTRACTS.md` §4 notes, and `GAME_EVENTS.md` §2 so REST
and event documentation do **not contradict** each other
(`API_CONTRACTS.md` §4 note 3 is the existing event-vs-REST reconciliation
point). `RewardSummary` must remain **one coherent contract across those
three documents**.

### 6.5 Ownership handoff (declare, do not override silently)

`DATABASE.md` §5 item 3 currently assigns the `RewardSummary` member list to
TASK-033. This task defines it earlier. TASK-033's own file is **not**
edited (§10). If this read proves wrong in execution, STOP (§14).

### 6.6 `GAME_STATE.md` — reference only

`GAME_STATE.md` §2.3 is **not** to be modified unless execution proves it
contains a *current contradiction* that cannot be resolved by reference to
the owning documents (§11). Expected: no change.

---

## 7. Unresolved Human Gameplay Decisions (BLOCK IMPLEMENTATION)

Every item below is **not decided anywhere in the repository** and is
**not covered by §2**. Record each in the owning document as an **open human
gameplay decision required before implementation**. **No placeholder values.
No guessed defaults. No "configurable later" substitute. Do not invent,
infer, or placeholder any of them**, and do not infer them from TASK-024 or
from the old `PetLevelMultiplier`.

```text
Pet XP items
[ ] 1.  Pet XP persistence
[ ] 2.  Pet XP initial value
[ ] 3.  Pet Level initial value
[ ] 4.  Pet Level range
[ ] 5.  Pet XP awarded per BattleWon        ┐
[ ] 6.  Pet XP awarded on BattleLost        │ items 5–8 jointly define
[ ] 7.  Whether only the active combat      │ PET BATTLE REWARD TARGETING
        Pet receives XP                     │ SEMANTICS
[ ] 8.  Whether inactive owned Pets         ┘
        receive 0 XP
[ ] 9.  Pet XP → Pet Level formula          ← BLOCKS the §6.4 Pet-track shape
[ ] 10. Whether Pet XP continues accumulating after Level 50
[ ] 11. Whether Pet XP uses the same progression curve as Player XP
[ ] 12. Whether Pet XP reward amount equals Player XP reward amount
```

These decisions remain unresolved until explicitly decided by the human
product owner.

The `GAME_RULES.md` §20 Rule Change Policy applies: a proposed mechanic may
be **recorded** as a proposal, but only a human decision makes it
authoritative.

---

## 8. Consistency Audit (required, repository-wide)

Search the repository for stale references to the retired model:

```text
PetLevelMultiplier · Pet Level Multiplier · PetLevelDerivation ·
PetLevelService · Player.Level × · Player Level × Pet ·
Pet Level derived from Player Level · no Pet XP ·
no independent Pet XP · Pet XP does not exist
```

Classify **every** occurrence as one of:

```text
Authoritative documentation · Historical completed task · Source code ·
Test · Comment · Migration · Configuration
```

Known occurrences at creation (re-verify by search during execution):

| Location | Class | Action |
|---|---|---|
| `PET_RULES.md` §5 (items 1–3, 9, 10), §5.1, §6 | Authoritative doc | **Rewrite** (§4 item 2) |
| `GAME_RULES.md` §9.3 | Authoritative doc | **Update** (retire derivation + "no Pet XP") |
| `MVP_SCOPE.md` §1 (Pets block; "no Pet XP") | Authoritative doc | **Update** |
| `GDD.md` §6, §14 | Authoritative doc | **Update** |
| `DATABASE.md` §1, §3 | Authoritative doc | **Update** (XP columns) |
| `ADR-012` items 3, 4, 6 + Consequences | Authoritative ADR | **Supersede affected items** (§9.3) |
| `ADR-011` item 6 | Authoritative ADR | **Supersede** Pet XP closure |
| `TASK-024`, `TASK-032`, `TASK-037`, `TASK-045` | Historical completed task | **DO NOT MODIFY** |
| `src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs`, `Pet.cs`, `PetDefinition.cs`, `PetTier.cs` | Source code | **Report as implementation follow-up** |
| `src/backend/GameServer.Application/Pets/PetLevelService.cs`, `DependencyInjection.cs`, `IPetRepository.cs` | Source code | Report as follow-up |
| `src/backend/GameServer.Infrastructure/.../PetConfiguration.cs`, `PetDefinitionConfiguration.cs`, `GameDbContextModelSnapshot.cs`, migrations | Source code / Migration / Config | Report as follow-up |
| `tests/.../PetLevelDerivationTests.cs`, `PetLevelRecomputeTests.cs`, `PetPersistenceTests.cs`, `AuthorityRegressionSuiteTests.cs`, `ApplicationRegistrationTests.cs` | Test | Report as follow-up |
| `tasks/backlog/TASK-033-*.md`, `TASK-058-*.md` | Task | **DO NOT MODIFY** TASK-033; TASK-058 superseded per §12 |

**Goal:** no *current authoritative documentation* still contradicts the new
model. The source code still implementing the old model is **expected** at
this stage and must be **reported as implementation follow-up, not fixed
here**.

---

## 9. ADR-016 (§6.1 owner: rationale)

### 9.1 Numbering — verify before creating, do not assume

```text
ADR-013 = Discord identity exchange contract   (NOT XP — do not reuse)
ADR-014 = BattleState identity carriage
ADR-015 = application session / JWT authentication
Highest existing ADR verified at task creation = ADR-015
→ ADR-016 is free; this task creates ADR-016
```

**Re-verify the ADR directory and index at execution time.** If another ADR
has appeared meanwhile (`ADR-016` no longer free), use the **next genuinely
free number** instead. Never overwrite or repurpose an existing ADR. If
numbering is ambiguous → STOP (§14).

### 9.2 ADR-016 content

Per `adr-change.md` §2: Context / Decision / Alternatives Considered / Why /
Consequences / Related Documents; Status `Accepted`; indexed in
`docs/03-decisions/README.md` §7 with a version bump. Record **why**, never
*how* (`adr-change.md` §1). Do not describe implementation details.

```text
1.  Player owns persistent XP / Level for account progression.
2.  Player Level range = [1, 50].
3.  Player Level has NO combat stats.
4.  BattleWon → Player XP +100.
5.  BattleLost → Player XP +0.
6.  Player XP reward amount is a configuration value.
7.  Player XP curve = min(floor(XP / 100) + 1, 50).
8.  Player XP is uncapped; Player Level is capped at 50.
9.  Pet owns independent XP / Level.
10. Pet Level is NOT derived from Player Level.
11. Pet XP belongs to the Pet INSTANCE, not PetDefinition.
12. Player progression and Pet combat progression are separate.
13. The old Player.Level × PetLevelMultiplier derivation is retired
    (if §6.2 resolves to Option A RETIRED).
14. Pet XP balance values remain unresolved human gameplay decisions.
```

The ADR **must explicitly state**:

```text
ADR-016 does not authorize implementation of unresolved Pet XP balance
or reward-targeting semantics.
```

### 9.3 Superseding `ADR-011` item 6 / `ADR-012` items 3, 4, 6

**Inspect the exact contents and status of `ADR-011` and `ADR-012` before
changing anything.** Do not blindly supersede an entire ADR if only specific
items are obsolete.

Per `adr-change.md` §3, where the old Pet XP / Pet Level decisions are
superseded:

```text
- preserve historical content (do not rewrite historical rationale);
- mark ONLY the affected decision items as superseded where the ADR
  format permits;
- point to ADR-016;
- leave unrelated decisions in force;
- update the ADR index entries in docs/03-decisions/README.md §7.
```

ADR-012 also owns Relic/Card ownership and MVP scope closure, which are
**unaffected** — state precisely which items are superseded and leave the
rest in force. Do not blanket-supersede an ADR whose other decisions hold.

**If the repository's ADR workflow requires whole-ADR status changes and
the partial-item treatment is ambiguous → STOP rather than guessing (§14).**

---

## 10. Acceptance Criteria

All binary; each cites the section that must contain it.

**Player track**
- [ ] Player XP persistence is documented
- [ ] `Player.XP` is documented `int`, NOT NULL, default `0`
- [ ] Initial Player XP = `0` is documented
- [ ] Initial Player Level = `1` is documented
- [ ] Player Level range `[1, 50]` is explicitly documented
- [ ] Player Level formula is exactly `min(floor(XP / 100) + 1, 50)`
- [ ] BattleWon grants **+100** Player XP
- [ ] BattleLost grants **+0** Player XP
- [ ] Player XP **reward amount** and XP-per-level **curve constant** are
      documented as two separate concepts (§6.3)
- [ ] Player XP is explicitly **uncapped**
- [ ] Player Level is explicitly **capped at 50**
- [ ] XP accumulation after Level 50 is explicitly documented
- [ ] Player Level is documented as having **no combat stats**

**Pet track**
- [ ] Pet XP is documented as belonging to the **Pet instance**
- [ ] Pet XP is documented as **not** belonging to `PetDefinition`
- [ ] Pet Level is explicitly defined as **independent from Player Level**
- [ ] Pet Level is defined as a **function of Pet XP**
- [ ] **Every** unresolved Pet XP decision (§7 items 1–12) is explicitly
      recorded as an open human decision
- [ ] No Pet XP balance value is invented
- [ ] No Player XP value is silently copied to Pet XP
- [ ] Active-Pet reward semantics are **not** assumed
- [ ] Pet Level-50 behavior is **not** invented

**Old derivation**
- [ ] Current authoritative docs no longer define
      `Player.Level × PetLevelMultiplier` as the Pet Level formula
- [ ] `PetLevelMultiplier` is explicitly **RETIRED** or explicitly
      **RETAINED with a documented purpose** (§6.2)
- [ ] No current authoritative doc says "there is no independent Pet XP
      system"

**RewardSummary**
- [ ] Player reward semantics are documented
- [ ] Pet reward semantics remain **explicitly dependent** on the unresolved
      Pet XP decisions where applicable
- [ ] No speculative Pet reward field is frozen
- [ ] `DATABASE.md`, `API_CONTRACTS.md`, and `GAME_EVENTS.md` do **not**
      contradict each other

**ADR**
- [ ] Next free ADR number is **verified** at execution time
- [ ] ADR-016 is created if still free (otherwise the next free number)
- [ ] The ADR records the two-track ownership decision
- [ ] The ADR records the decided Player XP contract (§9.2 items 1–8)
- [ ] The ADR explicitly states unresolved Pet XP balance decisions
- [ ] The ADR explicitly states it does **not** authorize implementation of
      unresolved Pet XP balance or reward-targeting semantics
- [ ] The ADR is indexed correctly in `docs/03-decisions/README.md` §7
- [ ] Only the affected previous ADR decisions are superseded (§9.3)
- [ ] Historical ADR content is preserved

**Task discipline**
- [ ] No source code is modified (`git status -- src/` clean)
- [ ] No test is modified (`git status -- tests/` clean)
- [ ] No migration is created or modified
- [ ] No completed task is modified (`git status -- tasks/completed/` clean)
- [ ] TASK-033 is **unchanged**
- [ ] TASK-058 content is **unchanged**; only the §12 lifecycle/status
      handling is applied
- [ ] No implementation task is created by this task
- [ ] No gameplay system is introduced
- [ ] No undocumented architecture is introduced
- [ ] All relevant validation passes at `core/validation.md` §2 and the
      `quality/review.md` §1 checklist (doc items only)

---

## 11. Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   FORBIDDEN
[ ] src/frontend/client/                                          FORBIDDEN
[ ] tests/                                                        FORBIDDEN
[x] docs/01-game-design/COMBAT_RULES.md   — NEW Player XP/Level section
[x] docs/01-game-design/PET_RULES.md      — §5 rewritten (Pet XP/Level)
[x] docs/01-game-design/GAME_RULES.md     — §9.3 derivation retired
[x] docs/02-technical/DATABASE.md         — §1/§3 XP columns; §1/§5 RewardSummary
[x] docs/02-technical/API_CONTRACTS.md    — §4 notes 1/3 reconciliation
[x] docs/02-technical/GAME_EVENTS.md      — §2 reward payload rule
[x] docs/00-overview/MVP_SCOPE.md         — §1 Pets/Player blocks
[x] docs/00-overview/GDD.md               — §6, §14
[x] docs/03-decisions/ADR/ADR-016-*.md    — NEW
[x] docs/03-decisions/ADR/ADR-011-*.md    — Status/pointer only (§9.3)
[x] docs/03-decisions/ADR/ADR-012-*.md    — Status/pointer only (§9.3)
[x] docs/03-decisions/README.md           — §7 index rows + version bump
[x] tasks/backlog/TASK-058-*.md           — Status: Superseded (status only, §12)
```

`GAME_STATE.md` §2.3 is to be modified **only** if execution proves it
contains a *current contradiction* that cannot be resolved through
references to the owning documentation (§6.6). Expected: no change.
`ROADMAP.md` is expected to need no change. Do not perform unrelated
documentation cleanup.

---

## 12. TASK-058 / TASK-033 Relationship

```text
TASK-058 — single-track contract (Player XP only; "There is no Pet XP")
        ↓  SUPERSEDED by this task (human decision)
TASK-059 (this task)  →  DONE
        ↓
Generate the XP implementation task (new ID) — NOT by this task
        ↓
TASK-033 — superseded by this contract; do NOT modify its file
```

- **TASK-058** is `BACKLOG` and therefore may not be edited as an
  implementation record. Its Status is set to `Superseded` with a one-line
  pointer to TASK-059 — **status/pointer only; its content is preserved** as
  the historical record. Per `TASK_LIFECYCLE.md` §5 this is an orchestrator
  transition.
- **TASK-033** is not touched. This task states, per §19 of the request,
  that TASK-033 becomes **superseded / ready for replacement by a new
  implementation task** — the lifecycle wording is recorded in this task's
  Completion Evidence, not by rewriting TASK-033.
- The XP implementation task is generated **only after** this documentation
  contract is complete and audited, and **only after** the §7 Pet XP
  decisions are supplied.

---

## 13. Testing / Validation Requirements

Documentation-only task: verification is by search, not test execution.

### Required Verification
```text
[ ] 1.  Current authoritative docs contain NO old Player.Level → Pet.Level
        formula
[ ] 2.  Current authoritative docs contain NO "no Pet XP" contract
[ ] 3.  Player XP formula occurs in ONE authoritative owner section and is
        referenced elsewhere rather than duplicated unnecessarily
[ ] 4.  Player reward amount (+100) is explicit
[ ] 5.  Player curve constant (100) is explicit and independent from the
        reward amount
[ ] 6.  Player XP remains uncapped while Player Level is capped at 50
[ ] 7.  All twelve Pet XP decisions are either explicitly resolved by
        existing human input OR explicitly recorded as unresolved
[ ] 8.  No Pet XP balance value has been invented
[ ] 9.  RewardSummary does not freeze speculative Pet reward semantics
[ ] 10. ADR numbering is verified and no existing ADR is overwritten
[ ] 11. Historical completed tasks remain unchanged
[ ] 12. TASK-033 remains unchanged
[ ] 13. TASK-058 content remains unchanged
[ ] 14. No source/test/migration changes exist
[ ] 15. All affected documentation is internally consistent

Static searches
[ ] "Player.Level ×" / "Player Level × Pet" / "derived from Player Level":
        0 hits in current authoritative docs/
[ ] "PetLevelMultiplier": every remaining hit classified per §8
        (docs retired; src/tests/migrations = follow-up)
[ ] "no Pet XP" / "no independent Pet XP" / "Pet XP does not exist":
        0 hits in current authoritative docs/
[ ] "floor(XP / 100)", "xpGained", Player XP "NOT NULL": each resolves to
        exactly one owner section
[ ] no PlayerAttack|PlayerDefense|PlayerHP|PlayerCrit|PlayerPower token
        introduced anywhere
[ ] RewardSummary members enumerated as ONE shape, with the Pet-track
        dependency on §7 stated
[ ] no XP potions / training items / ascension / talents / evolution /
        prestige / multiple XP currencies / rested XP / energy /
        progression-tree token introduced
[ ] Consistency re-read — COMBAT_RULES.md, PET_RULES.md, GAME_RULES.md,
        DATABASE.md, API_CONTRACTS.md, GAME_EVENTS.md, MVP_SCOPE.md, GDD.md
        read together: no duplicated definition, no residual contradiction
[ ] ADR index — ADR-016 row present, Status Accepted; ADR-011/ADR-012 rows
        show only the affected superseded items; number not reused
[ ] Git scope — `git status --porcelain` shows only docs/ + the permitted
        tasks/backlog/TASK-058 status change
```

Do **not** run implementation tests — this task is documentation-only
(`core/validation.md` §1: documentation validation layer).

### Key Edge Cases
- Player boundaries: `XP = 0 → Level 1`, `XP = 4900 → 50`, `XP > 4900 → 50`
  documented with Level capped and XP uncapped.
- Player defeat row: `+0 XP`, no level change.
- Reward constant vs curve constant independence stated (§6.3).
- Pet track: every §7 item either answered by a human decision or recorded
  as explicitly unresolved — no third state, no silent default.

---

## 14. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always
apply. Task-specific conditions:

- If **any Player XP human decision conflicts with an authoritative game
  rule** → STOP per `AGENTS.md` §4 (report both sources; do not pick the
  convenient one).
- If the **Pet XP reward amount cannot remain unresolved** without breaking
  the documentation workflow → STOP.
- If the **Pet XP curve cannot remain unresolved** without forcing an
  invented value → STOP.
- If `RewardSummary` **cannot be structurally documented without inventing
  Pet semantics** → STOP; document the dependency instead.
- If `PetLevelMultiplier` **cannot be classified as RETIRED or RETAINED**
  from authoritative rules plus explicit human input → STOP (§6.2).
- If **ADR numbering is ambiguous** → STOP (§9.1).
- If **ADR partial-supersession semantics are ambiguous** → STOP (§9.3).
- If the **task lifecycle does not permit the proposed TASK-058 status
  transition** → STOP; follow the documented lifecycle rather than inventing
  a status (§12).
- If a **completed task would need modification** → STOP; create a
  follow-up task instead (`AGENTS.md` §16).
- If **TASK-033 would need modification** → STOP.
- If **source code changes appear necessary** to complete the documentation
  contract → STOP (this task is documentation-only).
- If a **new gameplay rule is required but has not been decided** by the
  human product owner → STOP per `AGENTS.md` §7; do not invent it. This
  explicitly includes choosing any Pet XP amount, curve, cap, initial value,
  post-50 rule, or active-Pet-only semantics because §7 is unanswered —
  recording them as unresolved is correct; resolving them is not this
  task's authority.
- If a **new architectural decision is required beyond the scoped ADR** →
  STOP and report.
- If `DATABASE.md` **ownership of XP is ambiguous** (Player vs Pet instance
  vs `PetDefinition`) → STOP; `.ai/README.md` §13.
- If an **ADR conflicts with the game-design documents** → STOP; the ADR
  never wins (`docs/03-decisions/README.md` §3).
- If the **workflow/template cannot represent** this documentation-only task
  → STOP and report (do not force a `GAMEPLAY-CHANGE` implementation stage).
- If an `MVP_SCOPE.md` §2 OUT item or any §4-forbidden system is needed →
  STOP per `AGENTS.md` §8.
- If the **task expands beyond the affected documentation areas** (§11)
  without a documented reason → STOP; report it, do not apply it
  (`AGENTS.md` §16).
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries → STOP & decompose.

When stopped, report the exact conflict and **do not invent a resolution**.

---

## 15. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `docs/01-game-design/COMBAT_RULES.md` — §7 added (Player XP / Level
  contract: two tracks, `+100`/`+0` reward, reward-vs-curve distinction,
  `min(floor(XP / 100) + 1, 50)`, uncapped XP / capped Level, no combat
  stats); former §7 renumbered to §8 and its authority note extended to
  Player XP/Level; version 1.4.
- `docs/01-game-design/PET_RULES.md` — §5 rewritten: §5.1 Pet-instance XP /
  Level ownership and independence from Player Level, §5.2 the twelve
  **OPEN HUMAN GAMEPLAY DECISION** items, §5.3 retired terms
  (`PetLevelMultiplier` RETIRED; not to be reinterpreted), §5.4 non-XP
  attributes preserved; §1 data model gains `XP` and re-sources `Level`;
  §6 reference updated; former §5.1 removed; version 2.0.
- `docs/01-game-design/GAME_RULES.md` — §9.3 rewritten (derivation retired,
  "no independent Pet XP system" removed); version 3.0.
- `docs/02-technical/DATABASE.md` — §1 `Player.XP` and `Pet.XP` columns
  added, `Pet.Level` re-sourced, `PetDefinition.PetLevelMultiplier` removed
  from the documented contract, `RewardSummary` ownership moved to this
  document with a new "Reward semantics for `RewardSummary`" block; §3
  constraints updated; version 1.15.
- `docs/02-technical/API_CONTRACTS.md` — §4 notes 1 and 3 reconciled to the
  single `RewardSummary` contract; version 1.10.
- `docs/02-technical/GAME_EVENTS.md` — §2 `BattleWon`/`BattleLost` payload
  item 2 added (one reward-summary contract; Pet track unresolved);
  version 2.5.
- `docs/00-overview/MVP_SCOPE.md` — §1 Player and Pets blocks updated;
  version 1.3.
- `docs/00-overview/GDD.md` — §6 and §14 updated; version 2.3.
- `docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md`
  — **NEW** (Accepted).
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — Status note
  + supersession note for item 7's Pet XP/formula clause only.
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` —
  Status note + supersession note for items 3, 4, 6 only.
- `docs/03-decisions/README.md` — §7 index rows for ADR-016 and the two
  partially superseded entries, plus an explicit partial-supersession block;
  version 1.6.
- `tasks/backlog/TASK-058-record-mvp-player-xp-level-progression-contract.md`
  — already carried `Status: SUPERSEDED (by TASK-059)` with a content-
  preserving status note at intake; **not modified by this execution**
  (verified).
- `tasks/backlog/TASK-059-*.md` — this Completion Evidence (§15).

### Validation Results
- Stale-reference audit (§8) — PASS: every occurrence classified; 0 old-
  model hits remain in current authoritative `docs/` except inside the
  preserved historical rationale of ADR-011/ADR-012 and the supersession
  notes that point to ADR-016.
- `"Player.Level ×"` / `"Player Level × Pet"` / `"derived from Player Level"`
  in current authoritative docs — PASS: 0 live hits (remaining hits are
  ADR historical text + supersession notes, and `Pet.cs`'s
  "never derived from Player Level" for Tier/Star, which is still true).
- `"no Pet XP"` / `"no independent Pet XP"` / `"Pet XP does not exist"` in
  current authoritative docs — PASS: 0 hits. `GAME_RULES.md` §9.3 and
  `PET_RULES.md` §5 no longer close Pet XP.
- `"PetLevelMultiplier"` — PASS: retired in `PET_RULES.md`, `DATABASE.md`,
  `MVP_SCOPE.md`, `GAME_RULES.md`; remaining hits are source code, tests,
  migrations, historical completed tasks, and `TASK-059`'s own text →
  implementation follow-up.
- `"floor(XP / 100)"`, Player XP reward, Player XP `NOT NULL` — PASS: each
  defined in exactly one owner section (`COMBAT_RULES.md` §7,
  `DATABASE.md` §1/§3); other documents reference rather than restate.
- `PlayerAttack|PlayerDefense|PlayerHP|PlayerCrit|PlayerPower` — PASS: 0
  hits introduced.
- Speculative-progression tokens (`Prestige`, `Paragon`, `Season XP`,
  `Evolution`, `rested XP`, `energy`, XP potions, talents, ascension) —
  PASS: none introduced; `COMBAT_RULES.md` §7.5 item 5 and
  `PET_RULES.md` §5.3 item 4 forbid them explicitly.
- `RewardSummary` — PASS: one shape, owned by `DATABASE.md` §1; Player
  members fixed; Pet members explicitly dependent on `PET_RULES.md` §5.2
  items 5–9; no Pet field frozen.
- ADR index — PASS: ADR-016 present with Status Accepted; no number reused;
  ADR-011/ADR-012 rows carry only the affected superseded items.
- Git scope — PASS: `git status --porcelain` shows only `docs/` (plus the
  pre-existing TASK-058 status change and TASK-059's own file).
- Consistency re-read of `COMBAT_RULES.md`, `PET_RULES.md`,
  `GAME_RULES.md`, `DATABASE.md`, `API_CONTRACTS.md`, `GAME_EVENTS.md`,
  `MVP_SCOPE.md`, `GDD.md` together — PASS: no duplicated definition, no
  residual contradiction.

### Unresolved Human Decisions Carried Forward
All twelve §7 items remain open, recorded in `PET_RULES.md` §5.2 and
referenced from `DATABASE.md` §1 and `API_CONTRACTS.md` §4 / `GAME_EVENTS.md`
§2 (reward-summary dependency):

- `§7 item 1` — Pet XP persistence semantics — `PET_RULES.md` §5.2 item 1
- `§7 item 2` — Pet XP initial value — `PET_RULES.md` §5.2 item 2
- `§7 item 3` — Pet Level initial value — `PET_RULES.md` §5.2 item 3
- `§7 item 4` — Pet Level range — `PET_RULES.md` §5.2 item 4
- `§7 item 5` — Pet XP per `BattleWon` — `PET_RULES.md` §5.2 item 5
- `§7 item 6` — Pet XP on `BattleLost` — `PET_RULES.md` §5.2 item 6
- `§7 item 7` — active-Pet-only targeting — `PET_RULES.md` §5.2 item 7
- `§7 item 8` — inactive owned Pets receive 0 — `PET_RULES.md` §5.2 item 8
- `§7 item 9` — Pet XP → Pet Level formula — `PET_RULES.md` §5.2 item 9
  (also blocks the Pet-track `RewardSummary` members — `DATABASE.md` §1)
- `§7 item 10` — post-Level-50 accumulation — `PET_RULES.md` §5.2 item 10
- `§7 item 11` — Pet curve == Player curve? — `PET_RULES.md` §5.2 item 11
- `§7 item 12` — Pet reward == Player reward? — `PET_RULES.md` §5.2 item 12

### Implementation Follow-Up (source code still on the old model)
This divergence is expected and is **not** fixed by TASK-059:

- `src/backend/GameServer.Domain/Pets/PetLevelDerivation.cs` — the retired
  `Player.Level × PetLevelMultiplier` derivation must be removed; Pet Level
  must be sourced from `Pet.XP`.
- `src/backend/GameServer.Domain/Pets/Pet.cs` — `Level` is documented as a
  denormalized snapshot of `PetLevelDerivation.Derive`; needs a `XP` member
  and re-sourced `Level` semantics.
- `src/backend/GameServer.Domain/Pets/PetDefinition.cs` — the
  `PetLevelMultiplier` property and its doc comments must be removed.
- `src/backend/GameServer.Application/Pets/PetLevelService.cs`,
  `DependencyInjection.cs`, `IPetRepository.cs` — the recompute service
  and its registration must be retired or re-sourced from Pet XP.
- `src/backend/GameServer.Infrastructure/Postgres/Configurations/PetConfiguration.cs`
  ("There is no XP column"), `PetDefinitionConfiguration.cs`,
  `GameDbContextModelSnapshot.cs` — need the `Pet.XP` and `Player.XP`
  columns and the `PetLevelMultiplier` column/check-constraint removed.
- `src/backend/GameServer.Infrastructure/Postgres/Migrations/*` — a new
  migration (not created here) is required for the XP columns and the
  `PetLevelMultiplier` removal; existing migrations must not be edited.
- `tests/backend/**` — `PetLevelDerivationTests.cs`, `PetLevelRecomputeTests.cs`,
  `PetPersistenceTests.cs`, `AuthorityRegressionSuiteTests.cs`,
  `ApplicationRegistrationTests.cs`, and every test constructing a
  `PetLevelMultiplier` fixture must be updated with the new model.
- Reward application/persistence for `Player.XP` and the Player-track
  `RewardSummary` members is **not implemented** and is part of the
  follow-up implementation task.

### Final Report (required format)

```text
## Status
DONE

## Decision
The repository now documents two independent progression tracks. Player
owns account/meta XP and Level; Pet owns combat-character XP and Level per
instance; neither is derived from the other. The retired
`Player.Level × PetLevelMultiplier` derivation is RETIRED and the
`PetLevelMultiplier` field is removed from the documented contract. The
Player contract is fully decided and written. The Pet contract's STRUCTURE
is written; its BALANCE (all twelve items) is explicitly recorded as
unresolved human gameplay decisions in PET_RULES.md §5.2 and is not
invented anywhere.

## Player Track
Player.XP:              persists; int, NOT NULL, default 0; initial 0
Player.Level:           persists; range [1, 50]; initial 1; NO combat stats
Formula:                min(floor(XP / 100) + 1, 50)
                        (= min(floor(XP / 100), 49) + 1)
BattleWon:              +100 Player XP  (reward amount — CONFIGURATION)
BattleLost:             +0 Player XP
Curve constant:         100  (progression FORMULA constant — independent
                        concept from the reward amount; COMBAT_RULES.md §7.3)
Level cap:              50 (capped)
XP cap:                 none — uncapped, keeps accumulating after Level 50

## Pet Track
Pet.XP ownership:       the Pet INSTANCE (Pet.XP column, DATABASE.md §1);
                        PetDefinition does NOT own instance XP; DECIDED
Pet.Level ownership:    the Pet INSTANCE; DECIDED
Pet.Level derivation:   a function of that Pet's own XP, independent of
                        Player Level; the FUNCTION ITSELF IS UNRESOLVED
                        (PET_RULES.md §5.2 item 9)
Pet XP reward:          UNRESOLVED — §5.2 items 5 and 6
Pet XP curve:           UNRESOLVED — §5.2 items 9 and 11
Pet Level range:        UNRESOLVED — §5.2 item 4
Pet XP initial value:   UNRESOLVED — §5.2 item 2
Pet Level initial:      UNRESOLVED — §5.2 item 3
Post-Level-50 behavior: UNRESOLVED — §5.2 item 10
Active-Pet-only:        UNRESOLVED — §5.2 items 7 and 8
Pet XP persistence:     UNRESOLVED beyond the structural fact that Pet.XP
                        is a per-instance persisted column — §5.2 item 1
Pet reward == Player:   UNRESOLVED — §5.2 item 12
Each item above is marked unresolved EXACTLY as recorded in
PET_RULES.md §5.2. No Pet XP balance value was invented and no Player XP
value was copied to the Pet track.

## RewardSummary
Owned by DATABASE.md §1, "Reward semantics for `RewardSummary`" (ownership
moved from TASK-033; DATABASE.md §5 item 3 handoff updated accordingly).
Player track — FIXED: `playerXpGained`, `newPlayerXp`, `playerLeveledUp`,
`newPlayerLevel`, sourced from COMBAT_RULES.md §7.
Pet track — EXPLICITLY DEPENDENT on the unresolved Pet XP decisions
(PET_RULES.md §5.2 items 5–9): because items 7–8 decide which Pets a Pet
reward targets at all, even the arity of the Pet members is undecided, so
NO Pet member name, type, or count is contracted, and no placeholder is
introduced. The value in force remains the always-present `{}` for both
outcomes. DATABASE.md §1, API_CONTRACTS.md §4 notes 1/3, and GAME_EVENTS.md
§2 item 2 now describe one coherent contract and do not contradict each
other.

## PetLevelMultiplier
RETIRED
Resolved to exactly one outcome from authoritative rules plus explicit
human input. The product owner retired the `Player.Level × PetLevelMultiplier`
derivation (§2.4 "Pet Level is derived from Pet XP"; §2.2 of the request).
With Pet Level no longer derived from Player Level, the field has no
remaining documented purpose, and no existing authoritative rule or human
decision assigns it a Pet-XP-related purpose. It is NOT reinterpreted as an
XP curve multiplier, XP reward multiplier, or Pet Level input. Removed from
the documented `PetDefinition` contract (DATABASE.md §1/§3, PET_RULES.md
§5.3); its source-code and migration removal is recorded as implementation
follow-up. Retaining it merely because it exists in source code is
explicitly rejected (ADR-016, Option C).

## ADR
ADR-016 — "Independent Player XP and Pet XP Progression Tracks"
  File:   docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md
  Status: Accepted
  Index:  docs/03-decisions/README.md §7 (version 1.6)
  Numbering verified at execution: ADR-001..ADR-015 exist (highest =
  ADR-015); ADR-016 was free and is created. No number reused or repurposed.
  Records: Player owns account/meta XP+Level; Pet owns independent combat
  XP+Level; Player Level [1,50]; the Player XP reward (+100/+0) and curve
  (min(floor(XP/100)+1,50)); XP uncapped with Level capped; Pet Level not
  derived from Player Level; Pet XP belongs to the Pet instance; the old
  derivation RETIRED; the twelve Pet XP balance/reward decisions remain
  unresolved.
  States explicitly: "This ADR does not authorize implementation of
  unresolved Pet XP balance or reward-targeting semantics."
  Contains no implementation detail (why, not how — adr-change.md §1).
Superseded affected ADR items (partial; historical content preserved):
  ADR-011 item 7's Pet XP/formula clause (the "no Pet XP system" statement
    and the `Player Level × Pet Level Multiplier` formula) — superseded.
    ADR-011's role model, items 1–5, and the wire-label decision remain in
    force. Status: "Accepted (item 6 partially superseded by ADR-016)".
  ADR-012 items 3, 4, 6 — superseded as scoped to the formula, the clamp on
    that formula's result, and "There is no Pet XP system". ADR-012 items
    1, 2, 5, 7–12 (including Relic/Card ownership, loadout snapshots, and
    MVP scope closure) remain in force. Status: "Accepted (items 3, 4, 6
    partially superseded by ADR-016)".
  Neither ADR was blanket-superseded; the index rows carry the partial
  status, and an explicit supersession block in docs/03-decisions/README.md
  §7 names exactly which items changed. Partial supersession was therefore
  representable — no STOP condition fired.

## TASK-058
Unchanged by this execution. Its Status already read
`SUPERSEDED (by TASK-059)` with a content-preserving status note at intake
(git-verified as a pre-existing working-tree change). Only status/pointer
wording is present; substantive content is preserved. The §12 lifecycle
handling is satisfied — no content rewrite was performed or required.
Note: `TASK_LIFECYCLE.md` §1 does not enumerate a `Superseded` state; this
is recorded as an observation, not a blocker, because the transition was
already applied under an explicit product-owner decision and the §12
fallback ("if the lifecycle does not permit the proposed transition") was
therefore never reached.

## TASK-033
Unchanged. Verified by git: `tasks/backlog/TASK-033-*.md` has no diff. Per
§12, its superseded/replacement lifecycle wording is recorded here rather
than by rewriting the file: TASK-033 becomes superseded by this contract and
ready for replacement by a new implementation task. This task did not create
that implementation task.

## Documentation Changed
docs/01-game-design/COMBAT_RULES.md
docs/01-game-design/PET_RULES.md
docs/01-game-design/GAME_RULES.md
docs/02-technical/DATABASE.md
docs/02-technical/API_CONTRACTS.md
docs/02-technical/GAME_EVENTS.md
docs/00-overview/MVP_SCOPE.md
docs/00-overview/GDD.md
docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md (NEW)
docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md
docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md
docs/03-decisions/README.md
tasks/backlog/TASK-059-resolve-independent-player-xp-and-pet-xp-contract.md
(§15 Completion Evidence only)

GAME_STATE.md — NOT modified (§6.6 contradiction condition not proven; §2.3
already states "no authoritative battle-time combat pool", which does not
contradict the new model).
ROADMAP.md — NOT modified (no change required).

## Stale References
Full audit performed over the whole repository for: PetLevelMultiplier ·
Pet Level Multiplier · PetLevelDerivation · PetLevelService · Player.Level ×
· Player Level × Pet · Pet Level derived from Player Level · no Pet XP ·
no independent Pet XP · Pet XP does not exist.

Authoritative documentation (UPDATED / now clean):
  PET_RULES.md §5, §5.1, §6 · GAME_RULES.md §9.3 · MVP_SCOPE.md §1 ·
  GDD.md §6, §14 · DATABASE.md §1, §3 → rewritten; 0 live old-model hits.
Authoritative ADR (PARTIALLY SUPERSEDED, rationale preserved):
  ADR-011 item 7 clause · ADR-012 items 3, 4, 6 → supersession notes added;
  their historical text still contains the old formula, which is intentional
  and is labelled.
Historical completed tasks (UNTOUCHED, by design):
  TASK-024 · TASK-032 · TASK-037 · TASK-045.
Open tasks (UNTOUCHED):
  TASK-033 (BLOCKED, unchanged) · TASK-058 (status only, pre-existing).
Source code (REPORTED AS FOLLOW-UP, not fixed):
  Domain/Pets/PetLevelDerivation.cs · Pet.cs · PetDefinition.cs · PetTier.cs ·
  Application/Pets/PetLevelService.cs · DependencyInjection.cs ·
  IPetRepository.cs ·
  Infrastructure/Postgres/Repositories/PetRepository.cs ·
  Configurations/PetConfiguration.cs · PetDefinitionConfiguration.cs ·
  Migrations/* (5 Designer/Snapshot files + AddPetPersistence).
Tests (REPORTED AS FOLLOW-UP, not modified):
  Domain.Tests/PetLevelDerivationTests.cs ·
  Infrastructure.Tests/PetLevelRecomputeTests.cs · PetPersistenceTests.cs ·
  PetOwnershipResolutionTests.cs · CardPersistenceTests.cs ·
  BattleResultPostgresTests.cs · BattleResultPersistenceTests.cs ·
  Application.Tests/AuthorityRegressionSuiteTests.cs ·
  ApplicationRegistrationTests.cs · BattleStartServiceTests.cs ·
  Api.Tests/BattleStartSmokeTest.cs · BattleStartEndpointTests.cs ·
  BattleResultSmokeTest.cs · BattleResultEndpointTests.cs ·
  ApplicationSessionRESTTests.cs · RedisBattleStateSmokeTest.cs.
Configuration / Migration: the PetLevelMultiplier column and its
  CK_PetDefinition_PetLevelMultiplier_Positive check constraint.

## Source Code
No source code changed. `git status --porcelain` shows no modification under
`src/`, `tests/`, or `Migrations/`.

## Tests
No implementation tests run; documentation validation only
(core/validation.md §1 — documentation validation layer). No test file was
modified.

## Unresolved Human Decisions
The twelve Pet XP items, all still open and all blocking implementation:
  1. Pet XP persistence semantics
  2. Pet XP initial value
  3. Pet Level initial value
  4. Pet Level range
  5. Pet XP awarded per BattleWon
  6. Pet XP awarded on BattleLost
  7. whether only the active combat Pet receives XP
  8. whether inactive owned Pets receive 0 XP
  9. Pet XP → Pet Level formula  (also blocks the RewardSummary Pet track)
 10. whether Pet XP continues accumulating after Level 50
 11. whether Pet XP uses the same curve as Player XP
 12. whether the Pet XP reward amount equals the Player XP reward amount
Recorded in PET_RULES.md §5.2 as "OPEN HUMAN GAMEPLAY DECISION".

## Next Step
Resolve the remaining Pet XP human gameplay decisions,
then generate a separate implementation task.
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code written)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — both XP tracks
      IN; no §2 OUT item introduced)
- [x] Confirmed no `PlayerAttack`/`PlayerDefense`/`PlayerHP`/`PlayerCrit`/
      `PlayerPower` or equivalent introduced (ADR-011 item 5)
- [x] Confirmed no Prestige, Paragon, Season XP, Evolution, or other extra
      progression system introduced
- [x] Confirmed no Pet XP balance value invented and none copied from
      Player XP
