# TASK-058 — Record the MVP Player XP / Level Progression Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; copies only the human decisions that
  are this task's input (they exist nowhere in docs/ yet).
  Created from the TASK-033 readiness audit (2026-09-27) + product-owner
  decisions supplied the same day.
-->

---

## Metadata

```text
Task ID:           TASK-058
Type:              DOCUMENTATION
Status:            BACKLOG
Risk:              MEDIUM (cross-referenced contract: domain rule doc +
                   DATABASE.md + API_CONTRACTS.md / GAME_EVENTS.md;
                   tasks/TASK_TYPES.md §4 "MEDIUM if it affects a
                   cross-referenced contract")
Priority:          HIGH (closes the blocker on TASK-033, a HIGH task)
Primary Agent:     review
Supporting Agents: gameplay (domain-rule content accuracy),
                   backend, persistence (Player.XP persistence contract)
Workflow:          documentation/documentation-change.md
                   + architecture/adr-change.md (§2 — mandated ADR-016)
Skills:            discovery/documentation-discovery, discovery/impact-analysis,
                   quality/documentation-consistency, quality/scope-validation,
                   backend/persistence-analysis, quality/architecture-conformance
                   (6 skills — Complex budget, tasks/README.md §12)
Dependencies:      TASK-023 (DONE — Player entity + `Player.Level` persistence;
                   read as a constraint, AGENTS.md §16 — must not be reopened)
Blocks:            TASK-033 (readiness re-audit only after this task is DONE —
                   its Status is NOT changed automatically, §12 below)
Estimate:          Complex (6 skills; documentation-only, no code; one human
                   decision point — already supplied, §2 below)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE`:
`tasks/TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change that is
"propagat[ed] … through implementation and tests", and
`.ai/workflow/development/gameplay-change.md` §3 ends in
`core/implementation.md` → `quality/testing.md`. This task's scope (§4)
forbids all code, so that workflow cannot complete; the gameplay
implementation is TASK-033 after re-audit (§12). `documentation/documentation-change.md`
§1 ("a task whose primary output is a change to `docs/` itself") and the
precedent set (TASK-045/046/048/050/051/056 — all contract-formalization
tasks typed `DOCUMENTATION`) govern here. The domain rule content is still
reviewed for accuracy by the `gameplay` supporting agent.

**Status note.** `BACKLOG`, not `BLOCKED`: the stop condition that blocked
TASK-033 was closed by the human decisions recorded in §2 before this task was
written. Per `tasks/README.md` §6 the file lives in `backlog/`; per
`TASK_LIFECYCLE.md` §3 it may move `BACKLOG → READY` once an orchestrator
confirms the §7 criteria.

---

## 1. Objective

Formalize the human-decided MVP Player XP / Level progression contract in the
owning documents — one gameplay contract in `COMBAT_RULES.md`, one persistence
contract in `DATABASE.md`, one `RewardSummary` shape, one ADR recording the
decision — with **no source code, no migration, and no implementation of
TASK-033**. The outcome is that TASK-033's previously missing design inputs
(§2 of `tasks/backlog/TASK-033-player-reward-xp-level-progression.md`) all
resolve to a citable document section.

---

## 2. Human Decisions — Authoritative Input (product owner, 2026-09-27)

These are the decision, not a proposal. Write them into the owner documents
per §6. Do not re-derive, adjust, round, or replace any value below.

### 2.1 XP persistence

```text
Player.XP  MUST be persisted.
type: int   NOT NULL   default: 0
XP belongs to Player, not Pet. There is no Pet XP.
```

### 2.2 XP reward

```text
BattleWon  → +100 XP        (MVP initial configuration value)
BattleLost → +0 XP
```

- The reward amount MUST be **configurable**, never hard-coded into the
  gameplay contract (same treatment as `PetLevelMultiplier` — `DATABASE.md`
  §1 "config — never hard-coded").
- Out of scope for MVP: Boss-tier XP scaling, XP multipliers, boosts, rested
  XP, quests, or any other reward modifier.

### 2.3 XP curve — linear

Authoritative:

```text
Total XP required for Level N = 100 × (N - 1)

Level 1 = 0 XP      Level 49 = 4800 XP
Level 2 = 100 XP    Level 50 = 4900 XP
Level 3 = 200 XP    per-level cost = 100 XP (constant, flat/linear)
...
```

**Rejected wording — MUST NOT appear in any document:**

```text
XP_to_level_N = 100 × N        (WRONG — do not use)
```

### 2.4 Level formula

```text
Level = min(floor(XP / 100) + 1, 50)      range [1, 50]
Initial state: XP = 0, Level = 1
XP = 4900 → Level 50
```

### 2.5 Level cap / XP cap

```text
Player Level is capped at 50.   Player XP is NOT capped.
XP 4900 → 50 ; XP 5000 → 50 ; XP 10000 → 50
```

- XP keeps accumulating after Level 50 (cumulative/lifetime progression).
- Do NOT add Prestige, Paragon, Season XP, or post-50 progression.

### 2.6 Ownership / architecture

```text
Player (XP, Level)  →  Pet Level  →  Pet Combat Stats
```

Player = account/owner; Pet = combat character. No Player combat-stat system:
`PlayerAttack`, `PlayerDefense`, `PlayerHP`, `PlayerCrit`, `PlayerPower` and
equivalents must not appear.

### 2.7 `RewardSummary` MVP shape — exactly three members

```json
{ "xpGained": 100, "leveledUp": true, "newLevel": 5 }
```

```json
{ "xpGained": 0, "leveledUp": false, "newLevel": <current level> }   // defeat
```

- `xpGained` = XP actually granted by the outcome; `leveledUp` = whether this
  reward changed Player Level; `newLevel` = resulting Player Level.
- No item/currency/line-item reward system exists in MVP. No additional
  `RewardSummary` members.
- The existing rule stands: BattleResult REST exposes `rewards` for **both**
  victory and defeat (`API_CONTRACTS.md` §4 note 1). Unrelated BattleResult
  semantics do not change.

### 2.8 Owner documents

| Concept | Owner |
|---|---|
| XP → Level progression formula (gameplay contract) | `docs/01-game-design/COMBAT_RULES.md` (new smallest section) |
| XP reward amount (MVP initial config value, tunable) | the config value referenced from that section — never baked into the contract as unchangeable |
| `Player.XP` persistence (int, NOT NULL, default 0) | `docs/02-technical/DATABASE.md` §1 (+ §3 constraints) |
| `RewardSummary` MVP member list | `docs/02-technical/DATABASE.md` §1 / §5 item 3 (handoff in §6.2) |
| Decision rationale | new ADR-016 (§9.2) |

The section MUST distinguish the **XP reward amount** from the **XP → Level
progression formula**. The formula is the gameplay contract; `100 XP` is an
MVP balance/configuration value. Both currently equal `100` — they are two
independent values and must be documented as such, so tuning the reward does
not silently rewrite the curve.

---

## 3. Authoritative References

- `docs/00-overview/MVP_SCOPE.md` §1 — Player Level (1–50) is IN; "Exact XP
  curve is a balance concern, not a scope item" (see §6.4)
- `docs/00-overview/GDD.md` §14 — Meta Progression; Player Level increases
  through battle Rewards
- `docs/01-game-design/PET_RULES.md` §5 items 1, 2, 4, 6, 10, §5.1 item 4 —
  Player Level range/initial value; no Pet XP; curve home is
  `COMBAT_RULES.md` / config (item 6 already points here)
- `docs/01-game-design/COMBAT_RULES.md` — target owner section (currently §1–§7;
  no XP/Level content exists)
- `docs/01-game-design/GAME_RULES.md` §9.3 (Player/Pet Level), §20 (Rule Change
  Policy — human approval obtained, §2), §21 (source-of-truth hierarchy)
- `docs/02-technical/DATABASE.md` §1 (Player schema, `RewardSummary`),
  §3 (constraints), §5 item 3 (`RewardSummary` member-list ownership)
- `docs/02-technical/API_CONTRACTS.md` §4 note 1 (`rewards` always present,
  both outcomes)
- `docs/02-technical/GAME_EVENTS.md` §2 (`BattleWon`/`BattleLost` reward
  summary payload rule), `docs/02-technical/GAME_STATE.md` §2.3 (no Player
  combat pool)
- `docs/03-decisions/ADR/ADR-011*` item 5 (no Player combat stats),
  `ADR-012*` items 1, 2, 4, 6 + Consequences (Player Level range, mechanism-
  level XP, 1–50 cap, no Pet XP — see §6.3), `ADR-014*` (`RewardSummary`
  member-list ownership reference)
- `docs/00-overview/ROADMAP.md` Phase 3 ("Balance pass on all configurable
  values") — remains valid; the reward stays tunable
- `AGENTS.md` §4, §7, §17, §20; `docs/03-decisions/README.md` §5, §7;
  `.ai/workflow/architecture/adr-change.md` §2, §4

---

## 4. Scope

### In Scope

1. Add the XP / Level progression section to `COMBAT_RULES.md` (§2.3–§2.5,
   §2.8) with the reward/config distinction of §2.8.
2. Update `PET_RULES.md` **only if** a reference becomes stale (§7 points at
   `COMBAT_RULES.md` already; the Pet Level derivation itself is untouched).
3. Define `Player.XP` in `DATABASE.md` §1 + §3 (`int`, NOT NULL, default 0),
   preserving all existing Player fields and constraints.
4. Formalize the three-member `RewardSummary` shape in `DATABASE.md`
   (§1 / §5 item 3 handoff — §6.2) and align `API_CONTRACTS.md` §4 note 1
   wording if it becomes stale.
5. Targeted consistency pass (§6) over the nine documents listed in the
   request — contradictions **caused by this XP contract only**.
6. Create ADR-016 recording the eleven decision points (§9.2) and add it to
   `docs/03-decisions/README.md` §7.

### Out of Scope — MUST NOT be implemented by this task

```text
Player.XP migration · Player XP service · XP calculation code ·
Level calculation code · battle reward application · Reward persistence ·
Reward API implementation · Redis changes · SignalR changes · frontend /
Phaser changes · Match-3 / board / gems / swap / cascade · combat damage ·
Pet / Boss / Card / Relic / Passive gameplay · any progression system
beyond Player XP/Level · Prestige / Paragon / Season XP · Boss-tier XP
scaling, multipliers, boosts, rested XP, quests · XPHistory / PlayerXPLog /
RewardHistory / LevelHistory tables · any MVP_SCOPE.md §2 OUT item
```

Also out of scope: editing TASK-033 (§12) or reopening any completed task
(`AGENTS.md` §16).

---

## 5. Current State (verified at creation, 2026-09-27)

- **No XP contract exists anywhere.** `git grep -i "xp" -- docs/` returns only
  deferral statements (`ADR-012` item 2 + Consequences, `PET_RULES.md` §5
  items 4/6/10, `MVP_SCOPE.md` §1, `DATABASE.md` §1 "may include" permissive
  clause). No `appsettings*.json` carries XP/reward values.
- **No conflicting XP decision exists**, and no other task owns this contract:
  `backlog/` holds TASK-033 (implementation), TASK-035/036 (Discord), TASK-056/
  057 (readiness). This satisfies the §11 stop conditions 1–2 at creation —
  re-verify both by search before editing.
- **Player model is contract-clean:** `Player` = `PlayerId`, `DiscordUserId`,
  `Level` (`MinLevel` 1 / `MaxLevel` 50 / `InitialLevel` 1), `CreatedAt`; no XP
  column, no XP config (`src/backend/GameServer.Domain/Players/Player.cs`,
  `PlayerConfiguration.cs`, `GameDbContextModelSnapshot.cs`).
- **`RewardSummary` is in staging state:** JSON NOT NULL, value `{}`
  (`BattleResultService.EmptyRewardSummary`), `rewards` always present for both
  outcomes (`API_CONTRACTS.md` §4 note 1), member list currently assigned to
  TASK-033 (`DATABASE.md` §5 item 3) — see §6.2.
- **`COMBAT_RULES.md` has sections §1–§7 and no XP/Level content.**
- Highest ADR is ADR-015 → this task creates **ADR-016**.

---

## 6. Owner Assignment & Consistency Handoffs (smallest edits only)

Each item is a *targeted* edit caused by this contract. Anything else found
during the pass is reported, not fixed (`AGENTS.md` §16).

### 6.1 Owner documents
Per §2.8: `COMBAT_RULES.md` owns the formula; `DATABASE.md` owns `Player.XP`
and the `RewardSummary` member list; the ADR owns the rationale.
No concept may be defined twice (`documentation-change.md` §2).

### 6.2 `RewardSummary` ownership handoff (known overlap — declare, do not
override silently)
`DATABASE.md` §5 item 3 currently reads "member list is owned by TASK-033 …
until that task defines it". This task defines it earlier, under direct human
authorization (§2.7). Smallest correction: §5 item 3 / §1 cite the new
documented shape (and ADR-016), and TASK-033's role becomes *implementing* the
defined contract — TASK-033's own file is **not** edited (§12).
If this read proves wrong in execution, stop (§11 item 3).

### 6.3 `ADR-012` — not superseded
`ADR-012` still owns Player Level definition, the 1–50 clamp, and "no Pet XP /
no Evolution". Its Consequences line "exact XP numbers remain a future balance
task" is *fulfilled* by ADR-016, not replaced. Per
`adr-change.md` §3 do **not** set `ADR-012` to `Superseded` and do not rewrite
its content; at most add a non-semantic `Related: ADR-016` pointer.

### 6.4 Documents to inspect (nine + ADRs) — fix only XP-caused contradictions
`GAME_RULES.md` (§9.3), `PET_RULES.md` (§5 items 3/6/10, §5.1 item 4),
`COMBAT_RULES.md` (new section), `DATABASE.md` (§1, §3, §5), `API_CONTRACTS.md`
(§4 note 1), `GAME_STATE.md` (§2.3), `GAME_EVENTS.md` (§2), `MVP_SCOPE.md` §1
("Exact XP curve is a balance concern, not a scope item" — may read stale once
the curve exists; smallest edit or none), `ROADMAP.md` (Phase 3 balance pass —
expected to need no change), plus `ADR-011`, `ADR-012`, `ADR-014`.
Stop and report any contradiction that is *not* caused by this contract.

---

## 7. Acceptance Criteria

All binary; each cites the section that must contain it.

- [ ] `DATABASE.md` §1 defines `Player.XP` as persisted Player state
- [ ] `Player.XP` is documented `int`, NOT NULL, default `0`
- [ ] The contract states BattleWon grants **100 XP** in MVP
- [ ] The contract states BattleLost grants **0 XP**
- [ ] The XP reward value is documented as **configurable** (not hard-coded)
- [ ] The XP curve is explicitly **linear** (constant 100 XP per level)
- [ ] Level formula is explicitly `min(floor(XP / 100) + 1, 50)`
- [ ] Player Level range is explicitly `[1, 50]`
- [ ] Level 50 is documented as requiring **4900 XP** (Level 1 = 0 XP)
- [ ] XP is explicitly **NOT capped** at 4900 (accumulates past Level 50)
- [ ] Level is explicitly **capped at 50**
- [ ] **Player** owns XP (ownership chain Player → Pet Level → Pet combat stats)
- [ ] Pet XP is explicitly documented as not existing
- [ ] `RewardSummary` documents exactly `xpGained`, `leveledUp`, `newLevel`
- [ ] Victory `RewardSummary` semantics are documented
- [ ] Defeat `RewardSummary` semantics are documented (`xpGained: 0`,
      `leveledUp: false`, `newLevel` = current level)
- [ ] `COMBAT_RULES.md` owns the progression formula (single definition; the
      rejected `100 × N` wording appears nowhere)
- [ ] `DATABASE.md` owns the `Player.XP` persistence contract
- [ ] `API_CONTRACTS.md` §4 / `GAME_EVENTS.md` §2 remain consistent with the
      three-member shape and with `rewards` present for both outcomes
- [ ] ADR-016 exists and records all eleven points in §9.2, Status `Accepted`,
      indexed in `docs/03-decisions/README.md` §7
- [ ] No source code is modified (`git status -- src/` clean)
- [ ] No database migration is created
- [ ] No Redis/SignalR/frontend implementation is added
- [ ] No unrelated gameplay contract is changed (§6 scope; §16 report)
- [ ] Documentation-only validation passes at `core/validation.md` §2
      (MEDIUM depth) and the `quality/review.md` §1 checklist (doc items only)

---

## 8. Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   FORBIDDEN
[ ] src/frontend/client/                                          FORBIDDEN
[ ] tests/                                                        FORBIDDEN
[x] docs/01-game-design/COMBAT_RULES.md   — new progression section
[x] docs/01-game-design/PET_RULES.md      — reference only, if stale
[x] docs/01-game-design/GAME_RULES.md     — §9.3 consistency only, if stale
[x] docs/02-technical/DATABASE.md         — §1, §3 Player.XP; §1/§5 RewardSummary
[x] docs/02-technical/API_CONTRACTS.md    — §4 note 1, if stale
[x] docs/02-technical/GAME_EVENTS.md      — §2, if stale
[x] docs/02-technical/GAME_STATE.md       — §2.3, if stale
[x] docs/00-overview/MVP_SCOPE.md         — §1, if stale
[x] docs/00-overview/ROADMAP.md           — expected unchanged
[x] docs/03-decisions/ADR/ADR-016-*.md    — NEW
[x] docs/03-decisions/README.md           — §7 index row + version bump
```

---

## 9. Implementation Notes

1. **Edit order** (`documentation-change.md` §1, `AGENTS.md` §17):
   canonical owner first (`COMBAT_RULES.md`, then `DATABASE.md`), then the ADR,
   then dependent references — only where wording became stale.
2. **Section placement in `COMBAT_RULES.md`:** add the next free top-level
   number (after §7). If any insertion would renumber an existing section,
   grep for `COMBAT_RULES.md §<n>` citations first (existing citations are
   §2, §3, §3.2, §5) — no citation may silently change target.
3. **Formula vs reward separation** (§2.8): the formula's constant and the
   reward's `100` are documented as independent values; the reward is
   referenced as a configuration value with MVP initial value `100 XP`.
4. **ADR-016** per `adr-change.md` §2: next sequential number (016), template
   sections Context / Decision / Alternatives Considered / Why / Consequences /
   Related Documents, Status `Accepted` (decision is confirmed now — §4), index
   row added in `docs/03-decisions/README.md` §7 with a version bump (precedent:
   "ADR-015 added" in v1.5). The eleven required points:
   1. XP persisted on Player · 2. BattleWon +100 XP (MVP) · 3. BattleLost +0 XP ·
   4. reward amount configurable · 5. XP→Level linear ·
   6. `Level = min(floor(XP / 100) + 1, 50)` · 7. Level capped at 50 ·
   8. XP NOT capped at 4900 · 9. Player owns XP; Pet does not ·
   10. `RewardSummary` MVP shape = exactly the three documented fields ·
   11. numeric balance tunable later without changing the persistence model.
   Record **why** each was chosen — never "how" the code will realize it
   (`adr-change.md` §1 "Decision vs. Implementation").
5. **No duplication:** other documents cite the owner section; they must not
   restate the formula, the reward table, or the JSON shape
   (`documentation-change.md` §2).
6. **Do not touch:** `tasks/backlog/TASK-033-*.md`, any file under
   `tasks/completed/`, and all `src/` + `tests/` (§4).

---

## 10. Testing Requirements

Documentation-only task: verification is by search, not test execution.

### Required Verification
```text
[ ] Static search — `100 × N` / "XP_to_level_N" rejected wording: 0 hits in docs/
[ ] Static search — XP contract terms ("floor(XP / 100)", "xpGained",
                        "NOT NULL" near XP) each resolve to exactly one owner section
[ ] Static search — no `PlayerAttack|PlayerDefense|PlayerHP|PlayerCrit|PlayerPower`
                        token introduced anywhere
[ ] Static search — `RewardSummary` members enumerated as exactly three
[ ] Consistency re-read — the nine documents of §6.4 read together, no
                        duplicated definition and no XP-caused contradiction left
[ ] ADR index — ADR-016 row present, Status Accepted, number not reused
```

### Key Edge Cases
- Level boundary: `XP = 0 → 1`, `XP = 4900 → 50`, `XP > 4900 → 50` documented
  with Level capped and XP uncapped.
- Defeat row: `newLevel` equals the unchanged current level, `xpGained: 0`.
- Reward config vs curve constant independence (§2.8) is stated explicitly.

---

## 11. Stop Conditions

- If a **conflicting XP decision** is found in `docs/` or ADRs during
  execution → STOP per `AGENTS.md` §4 (report both sources; do not pick the
  convenient one). *(Verified absent at creation, §5 — re-verify.)*
- If **another existing task** is found to own this exact contract → STOP.
- If `RewardSummary` ownership cannot be handed off as described in §6.2
  without contradicting `DATABASE.md` → STOP.
- If the `Player Level → Pet Level` derivation conflicts with
  `PET_RULES.md` §5 → STOP (Pet Level derivation must not change).
- If the workflow/template cannot express this documentation-decision task →
  STOP and report (do not force a `GAMEPLAY-CHANGE` implementation stage).
- If resolution requires a gameplay decision **not** covered by §2 → STOP per
  `AGENTS.md` §7; do not invent an XP model, a cap, an increasing curve, or
  Boss-tier scaling.
- If any change beyond §6/§8 appears necessary → report it, do not apply it
  (`AGENTS.md` §16).
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries → STOP & decompose.

---

## 12. TASK-033 Relationship

```text
TASK-058 (this task)  →  DONE
        ↓
Re-run the TASK-033 readiness audit (read-only)
        ↓
TASK-033 → READY only if every other dependency and READY criterion is met
        ↓
TASK-033 is then implemented under its own workflow — NOT by this task.
```

- Do **not** modify TASK-033's implementation content, Status, or file.
- Do **not** automatically change TASK-033's Status to READY.
- Do not implement TASK-033.

---

## 13. Completion Evidence

### Changed Files
- `<file path>` — <summary of change>

### Validation Results
- `<static searches / consistency re-read>` — PASS (0 unexpected hits)

### Final Report (required format)

```text
## Status
DONE / BLOCKED

## Decision
<one paragraph: the finalized XP contract>

## Formula
Level = min(floor(XP / 100) + 1, 50)

## XP
BattleWon = +100
BattleLost = +0
XP cap = none
Level cap = 50

## RewardSummary
{ "xpGained": <int>, "leveledUp": <bool>, "newLevel": <int> }

## Documentation Changed
<every changed/created documentation file>

## ADR
ADR-016 <title> (Accepted, indexed in docs/03-decisions/README.md §7)

## Source Code
No source code changed.
No migration created.

## Next Step
Re-run TASK-033 readiness audit.
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code written)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Player Level IN;
      no §2 OUT item introduced)
- [x] Confirmed no Pet XP, no Evolution, no Player combat stats introduced
      (`ADR-011` item 5, `ADR-012` item 6)
