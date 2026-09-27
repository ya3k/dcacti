# TASK-060 — Resolve the Pet XP Progression and Reward Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; copies only the open questions that
  are this task's input (they exist nowhere in docs/ yet).

  THIS TASK IS DOCUMENTATION-ONLY. It turns the twelve unresolved Pet XP
  decisions left by TASK-059 into a complete, deterministic, implementation-
  ready gameplay contract. It does NOT implement source code and does NOT
  create the Pet XP implementation task.

  SUCCESS CONDITION: when DONE, a separate implementation task can be
  generated that implements Pet XP WITHOUT making any gameplay decision.
-->

---

## Metadata

```text
Task ID:           TASK-060
Type:              DOCUMENTATION
Status:            BLOCKED (execution attempted — §4 product-owner decisions
                   were not supplied; STOP CONDITION recorded in §14)
Risk:              HIGH (completes a cross-referenced progression contract
                   that TASK-059 deliberately left open and that currently
                   blocks every Pet XP implementation path; the twelve
                   decisions ripple into PET_RULES.md, DATABASE.md,
                   GAME_EVENTS.md / API_CONTRACTS.md RewardSummary
                   semantics, and possibly a new ADR. tasks/TASK_TYPES.md
                   §4 caps DOCUMENTATION at MEDIUM for "affects a
                   cross-referenced contract"; raised to HIGH for the same
                   reason TASK-059 was — the contract also gates the
                   RewardSummary Pet track and a downstream implementation
                   task cannot start until it lands)
Priority:          HIGH (unblocks the Pet XP implementation task and closes
                   the RewardSummary Pet-track dependency)
Primary Agent:     gameplay (domain-rule content owner — PET_RULES.md §5 /
                   the Pet progression contract)
Supporting Agents: review (contract consistency + ADR routing),
                   persistence (Pet.XP / Pet.Level persistence semantics
                   in DATABASE.md),
                   backend (RewardSummary Pet-track + event/API alignment)
Workflow:          documentation/documentation-change.md
                   + architecture/adr-change.md ONLY IF §9 resolves to
                   "new ADR" or "amendment"
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation,
                   gameplay/gameplay-behavior-derivation
                   (5 skills — Complex budget ceiling is 7, tasks/README.md §12)
Dependencies:      TASK-059 (DONE — the two-track contract this task
                   completes; read as a constraint, AGENTS.md §16, must
                   not be modified),
                   TASK-023 (DONE — Player entity + Player.XP/Level
                   contract; read only, must not be reopened),
                   TASK-024 (DONE — historical Pet Level derivation this
                   task must NOT read as a Pet XP source; read-only,
                   must not be modified)
Blocks:            the Pet XP implementation task (generated only AFTER
                   this task is DONE — NOT created by this task),
                   the RewardSummary Pet track
Estimate:          Complex (5 skills; documentation-only, zero code; the
                   twelve values are the EXPECTED OUTPUT of this task, not
                   its input)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE`:
`tasks/TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change that is
"propagat[ed] … through implementation and tests", and
`.ai/workflow/development/gameplay-change.md` §3 ends in
`core/implementation.md` → `quality/testing.md`. This task's scope (§7)
forbids all code, so that workflow cannot complete. The Pet XP
implementation is a **separate follow-up task generated only after this one
is DONE** (§12). The precedent set by
TASK-045/046/048/050/051/056/058/059 — all contract-formalization tasks
typed `DOCUMENTATION` — governs here.

**Decision-input note.** Unlike TASK-059, this task's authoritative input
**does not yet exist**. TASK-059 could write its contract because the Player
values were supplied by the product owner. This task's twelve Pet XP values
have **not** been supplied. Executing this task therefore requires the
product owner's decisions to be recorded in this file's §4 **before**
execution begins (see §3 Status note and §6 Stop Conditions). An agent that
starts this task without §4 populated MUST STOP — it may not derive them.

**Status note.** `BACKLOG`, not `BLOCKED`: per `tasks/README.md` §6 the file
lives in `backlog/`; per `TASK_LIFECYCLE.md` §3 it may move `BACKLOG →
READY` once an orchestrator confirms the READY criteria. The twelve
decisions being unanswered does **not** by itself make this task BLOCKED —
recording them is its deliverable. It becomes BLOCKED only under the §6
stop conditions (e.g. execution attempted with §4 empty).

---

## 1. Objective

Resolve the twelve Pet XP decisions left open by TASK-059
(`PET_RULES.md` §5.2 items 1–12) into a **complete, deterministic,
implementation-ready gameplay contract**, written into the owning
authoritative documents, such that a later implementation task can implement
`Pet.XP → Pet.Level` **without making any gameplay decision**.

This task resolves and documents; it does not implement. **No source code,
no migration, no schema, no tests.**

---

## 2. Current Authoritative State (input — do NOT reconsider)

### 2.1 Player track — DECIDED by TASK-059, NOT open here

Owned by `docs/01-game-design/COMBAT_RULES.md` §7. Frozen for this task:

```text
Player.XP persists; int NOT NULL default 0; initial 0; UNCAPPED.
Player.Level range [1, 50]; initial 1; capped at 50.
BattleWon → +100 Player XP.   BattleLost → +0 Player XP.
Player.Level = min(floor(Player.XP / 100) + 1, 50).
Player Level has NO combat stats.
Reward amount (100, CONFIGURATION) and curve constant (100, FORMULA)
  are two independent concepts (COMBAT_RULES.md §7.3).
```

**Do not modify, restate, or re-derive any Player value.** This task must
not touch `COMBAT_RULES.md` §7 except to add a cross-reference if the Pet
contract requires one.

### 2.2 Pet track — STRUCTURE decided, BALANCE unresolved

Owned by `docs/01-game-design/PET_RULES.md` §5. Confirmed and not
reversible:

```text
Pet instance owns Pet.XP.
Pet instance owns Pet.Level.
PetDefinition does NOT own Pet instance XP or instance Level.
Pet.Level is independent from Player.Level.
Pet.Level is a function of Pet.XP.
```

### 2.3 Retired — must not be revived

```text
Pet.Level = clamp(floor(Player.Level × PetLevelMultiplier), 1, 50)   RETIRED
PetLevelMultiplier                                                    RETIRED
```

`PetLevelMultiplier` has **no role** in the Pet XP model and must not be
given a new purpose, reused as an XP curve/reward multiplier, or
reinterpreted. See `PET_RULES.md` §5.3 and `ADR-016` item 13.

---

## 3. Human Decisions Required

All twelve items below are **currently unresolved** — verified in
`PET_RULES.md` §5.2. Each must end this task in exactly one of two states:

```text
RESOLVED   — a value/rule is written into the owning document, traceable to
             an explicit product-owner decision recorded in §4 of this file
OPEN HUMAN GAMEPLAY DECISION
           — explicitly deferred by the product owner (only if the product
             owner chooses to defer; §6 STOP if deferred silently)
```

**No third state. No placeholder value. No "configurable later"
substitute.** A "make it configurable" answer is NOT a decision — it moves
the undecided gameplay value into configuration and is a §6 STOP.

The decisions required, in resolution order (later items depend on earlier
ones):

```text
Block A — persistence & initial state
  1.  Pet XP persistence semantics
  2.  Initial Pet XP
  3.  Initial Pet Level
  4.  Pet Level range

Block B — PET BATTLE REWARD TARGETING SEMANTICS (items 5–8 jointly)
  5.  Pet XP awarded per BattleWon
  6.  Pet XP awarded on BattleLost
  7.  Whether only the active combat Pet receives XP
  8.  Whether inactive owned Pets receive 0 XP

Block C — the progression function
  9.  Pet XP → Pet Level formula

Block D — relationships to the Player track (must remain SEPARATE decisions)
  10. Whether Pet XP continues accumulating after Level 50
  11. Whether Pet XP uses the same progression curve as Player XP
  12. Whether Pet XP reward amount equals Player XP reward amount
```

**Record every product-owner decision in §4 below before/while executing.**
An agent must not supply these values itself.

---

## 4. Decision Matrix

`Current Evidence` records only what an authoritative document actually
says. `Human Decision Required?` is `YES` for all twelve at task creation.
This table must be updated to the resolved state as the task executes.

| #  | Decision                      | Current Evidence                                                                 | Human Decision Required? |
| -- | ----------------------------- | -------------------------------------------------------------------------------- | ------------------------ |
| 1  | Pet XP persistence            | `PET_RULES.md` §5.1 item 1 (instance-owned); `DATABASE.md` §1 `Pet.XP` column exists — but §1 states "this document contracts persistence only, not behavior"; the persistence *semantics* are recorded UNRESOLVED at `PET_RULES.md` §5.2 item 1 | **YES** |
| 2  | Initial Pet XP                | No value anywhere. `PET_RULES.md` §5.2 item 2 = UNRESOLVED. TASK-059 forbade copying the Player initial XP (`COMBAT_RULES.md` §7.5 item 3) across | **YES** |
| 3  | Initial Pet Level             | No value anywhere. `PET_RULES.md` §5.2 item 3 = UNRESOLVED. §5.4 item 5 states the Player's initial Level 1 says nothing about a Pet's initial Level | **YES** |
| 4  | Pet Level range               | `PET_RULES.md` §5 header states range = UNRESOLVED (§5.2 item 4). The old 1–50 range died with the retired derivation; `DATABASE.md` §3 records it UNRESOLVED | **YES** |
| 5  | XP per BattleWon              | No value anywhere. `PET_RULES.md` §5.2 item 5 = UNRESOLVED. The Player's `+100` is Player-track only | **YES** |
| 6  | XP on BattleLost              | No value anywhere. `PET_RULES.md` §5.2 item 6 = UNRESOLVED. The Player's `+0` is Player-track only | **YES** |
| 7  | Active Pet targeting          | No rule anywhere. `PET_RULES.md` §5.2 item 7 = UNRESOLVED. `PET_RULES.md` §2 item 2 establishes only that one Pet is *selected as active* per battle — it says nothing about reward targeting | **YES** |
| 8  | Inactive Pet behavior         | No rule anywhere. `PET_RULES.md` §5.2 item 8 = UNRESOLVED | **YES** |
| 9  | XP → Level formula            | No formula anywhere. `PET_RULES.md` §5.1 item 4 states the function is "**not defined**"; §5.2 item 9 = UNRESOLVED. Item 9 blocks the RewardSummary Pet-track shape (`DATABASE.md` §1) | **YES** |
| 10 | Post-Level-50 XP              | No rule anywhere. `PET_RULES.md` §5.2 item 10 = UNRESOLVED. The Player's "XP uncapped / Level capped" answer is Player-track only | **YES** |
| 11 | Same curve as Player?         | No rule anywhere. `PET_RULES.md` §5.2 item 11 = UNRESOLVED. `ADR-016` Decision item 7 defines the Player curve; item 12 says the tracks are separate and neither may be derived from the other | **YES** |
| 12 | Same reward amount as Player? | No rule anywhere. `PET_RULES.md` §5.2 item 12 = UNRESOLVED. `ADR-016` Decision item 6 defines the Player reward amount; item 12 forbids deriving one track from the other | **YES** |

**Product-owner decisions (to be recorded by the requester; do not invent):**

```text
1.  Pet XP persistence semantics:  <PENDING PRODUCT-OWNER DECISION>
2.  Initial Pet XP:                <PENDING PRODUCT-OWNER DECISION>
3.  Initial Pet Level:             <PENDING PRODUCT-OWNER DECISION>
4.  Pet Level range:               <PENDING PRODUCT-OWNER DECISION>
5.  Pet XP per BattleWon:          <PENDING PRODUCT-OWNER DECISION>
6.  Pet XP on BattleLost:          <PENDING PRODUCT-OWNER DECISION>
7.  Active-Pet-only targeting:     <PENDING PRODUCT-OWNER DECISION>
8.  Inactive Pet XP:               <PENDING PRODUCT-OWNER DECISION>
9.  Pet XP → Pet Level formula:    <PENDING PRODUCT-OWNER DECISION>
10. Post-Level-50 XP:              <PENDING PRODUCT-OWNER DECISION>
11. Pet curve == Player curve:     <PENDING PRODUCT-OWNER DECISION>
12. Pet reward == Player reward:   <PENDING PRODUCT-OWNER DECISION>
```

---

## 5. Authoritative References

- `docs/01-game-design/PET_RULES.md` §5.1 (ownership), **§5.2 (the twelve
  open items — this task's primary input)**, §5.3 (retired terms), §5.4
  (non-XP attributes), §2 (active Pet selection), §6 (stat composition)
- `docs/01-game-design/COMBAT_RULES.md` §7 — the Player track contract; the
  structural precedent for how a two-track XP contract is written (§7.3
  reward-vs-curve, §7.4 formula, §7.5 cap semantics, §7.6 no combat stats).
  Read as a **pattern**, never as a source of Pet values
- `docs/01-game-design/GAME_RULES.md` §9.3 (Pet rule), §20 (Rule Change
  Policy — only a human decision makes a proposed mechanic authoritative),
  §21 (hierarchy)
- `docs/02-technical/DATABASE.md` §1 (`Pet.XP` / `Pet.Level` columns;
  "Reward semantics for `RewardSummary`"), §3 (constraints)
- `docs/02-technical/API_CONTRACTS.md` §4 notes 1/3 (`rewards` both
  outcomes; event-vs-REST reconciliation)
- `docs/02-technical/GAME_EVENTS.md` §2 (`BattleWon` / `BattleLost` payload
  rule + reward-summary item 2)
- `docs/00-overview/MVP_SCOPE.md` §1 (Pet progression IN), §2 OUT, §4
  FUTURE-by-default
- `docs/00-overview/GDD.md` §6 (Pet System), §14 (Meta Progression)
- `docs/00-overview/ROADMAP.md` Phase 3 (balance pass on configurable
  values — a tuning pass, NOT authority to invent a value now)
- `docs/03-decisions/README.md` §3, §5, §7, §8; `ADR-016` items 9–14 (the
  Pet-structure decisions and the explicit deferral of these twelve)
- `AGENTS.md` §4, §7, §16, §17, §20; `tasks/TASK_LIFECYCLE.md` §3

Historical context only — **read, never cite as a Pet XP source, never
modify**: `tasks/completed/TASK-024-*`.

---

## 6. Scope

### In Scope

1. Resolve all twelve `PET_RULES.md` §5.2 items into explicit, written
   gameplay rules, traceable to §4 decisions.
2. Write the resolved Pet XP / Pet Level contract into
   `PET_RULES.md` §5 (replacing §5.2's open list with the decided rules;
   keeping any item the product owner explicitly defers marked
   `OPEN HUMAN GAMEPLAY DECISION`).
3. State **PET BATTLE REWARD TARGETING SEMANTICS** explicitly as its own
   rule (items 5–8), covering: what a `BattleWon` grants, what a
   `BattleLost` grants, which Pet(s) receive it, and what happens to owned
   Pets that are not targeted.
4. Resolve the **reward amount** and the **progression curve constant** for
   the Pet track as two independent values (`COMBAT_RULES.md` §7.3 is the
   structural precedent), and keep items 10, 11, 12 as **three separate**
   stated answers.
5. Document the Pet persistence semantics required for implementation
   (`Pet.XP` / `Pet.Level`: initial value, how Level relates to stored XP,
   what changes on a level-up) in `DATABASE.md` §1 + §3 — **without creating
   schema, entities, or migrations**.
6. Resolve the `RewardSummary` Pet-track dependency (§8) and update
   `DATABASE.md` §1, `API_CONTRACTS.md` §4, and `GAME_EVENTS.md` §2 so all
   three describe **one** coherent contract.
7. Update `MVP_SCOPE.md` §1 and `GDD.md` §6/§14 only where the resolved Pet
   contract makes their current wording stale.
8. Determine and apply the §9 ADR outcome (no ADR / amend ADR-016 / new
   ADR) and keep `docs/03-decisions/README.md` §7 consistent.
9. State, in the Completion Evidence, that the contract is
   implementation-ready (§10) — or report exactly which decision is still
   missing.

### Out of Scope — MUST NOT be implemented by this task

```text
Any source code (src/backend/, src/frontend/) · any test change (tests/) ·
any EF Core entity/configuration change · any database migration · any new
table or entity · PetXPHistory / PetXPLog / PetLevelHistory / PetProgression
or any other speculative entity · Redis / SignalR / Phaser / React changes ·
XP calculation code · Level calculation code · reward application ·
reward persistence · reward API implementation ·
Pet combat mechanics · Pet evolution · Pet tiers · Pet stars ·
skills · passives · relics · cards · rewards beyond XP ·
Match-3 / board / gems / swap / cascade · combat damage · Boss gameplay ·
gacha · quests · energy · prestige · ascension · rested XP ·
multiple XP currencies · any MVP_SCOPE.md §2 OUT item
```

Also out of scope: **modifying any Player XP value** (§2.1), **reopening
the Pet ownership structure** (§2.2), **reviving `PetLevelMultiplier`**
(§2.3), modifying TASK-059 / TASK-024 / any completed task (`AGENTS.md`
§16), and creating the Pet XP implementation task (§12).

---

## 7. Documentation Owners

The executing agent must confirm these owners before editing and use the
smallest set that actually changes (`documentation/documentation-change.md`
§1–§3 — one concept, one owner; do not edit every referencing document).

| Concept | Owner |
|---|---|
| Pet XP / Pet Level progression rules, reward targeting semantics, XP → Level formula | `docs/01-game-design/PET_RULES.md` §5 |
| Pet XP / Pet Level persistence semantics | `docs/02-technical/DATABASE.md` §1 + §3 |
| `RewardSummary` member list + Pet-track dependency | `docs/02-technical/DATABASE.md` §1 ("Reward semantics for `RewardSummary`") |
| Reward-summary REST/event reconciliation | `docs/02-technical/API_CONTRACTS.md` §4 notes 1/3 (reference only if wording goes stale) |
| Reward-summary event-payload rule | `docs/02-technical/GAME_EVENTS.md` §2 (reference only if wording goes stale) |
| MVP scope classification of Pet progression | `docs/00-overview/MVP_SCOPE.md` §1 (only if stale) |
| Meta-progression overview wording | `docs/00-overview/GDD.md` §6/§14 (only if stale) |
| Decision rationale | `ADR-016` or a new ADR — per §9 |

`COMBAT_RULES.md` §7 owns the **Player** track — it is **not** the owner of
any Pet value and must not be edited to carry Pet rules. `GAME_STATE.md` is
**not** to be modified unless a concrete current contradiction is proven.

---

## 8. RewardSummary Dependency

TASK-059 left the Pet portion of `RewardSummary` unresolved because items
5–9 were unresolved (`DATABASE.md` §1). This task must determine the
outcome:

```text
IF items 5–9 are all resolved
    → the Pet-track RewardSummary semantics become determinable.
      Document the member list in DATABASE.md §1 and reconcile
      API_CONTRACTS.md §4 / GAME_EVENTS.md §2 so the three describe ONE
      contract.

IF any of items 5–9 is explicitly deferred by the product owner
    → the dependency stays open; document it explicitly as a dependency,
      and DO NOT introduce placeholder Pet fields to fill the schema.
      (`AGENTS.md` §7; TASK-059 §6.4.)
```

**Do not invent Pet field names in advance.** If the final semantics require
a particular shape, the implementation task defines it later — but only
after this contract is complete. A `RewardSummary` Pet member may only be
named here if its meaning is fully determined by a §4 decision.

---

## 9. ADR Considerations

ADR-016 already records the Pet ownership decisions (items 9–12 of its
Decision) and explicitly defers the balance values (item 14). The executing
agent must determine the correct outcome **before** creating or amending
anything:

```text
Option A — No ADR change.
    Use when the resolved Pet XP values are BALANCE/CONFIGURATION and
    change no ownership, persistence strategy, or architectural boundary.
    ADR-016 item 14 already points at PET_RULES.md §5.2 as the single
    enumeration; a balance number is not architectural
    (architecture/adr-change.md §1, development/gameplay-change.md §3
    "a balance number change is not architectural"). Expected outcome.

Option B — Amend ADR-016.
    Use ONLY if the resolution changes a statement ADR-016 currently
    makes. Do not rewrite its historical rationale
    (architecture/adr-change.md §3, docs/03-decisions/README.md §5).

Option C — New ADR (next free number, verified at execution time).
    Use ONLY if the resolution introduces a genuinely new architectural
    decision — e.g. Pet XP crosses a storage/authority boundary that
    ADR-016 does not cover.
```

**Do not create an ADR merely because new gameplay decisions exist.** If
the numbering or the amendment-vs-new-ADR choice is ambiguous → **STOP**
(§11). Verify the next free ADR number at execution time; do not assume.

---

## 10. Acceptance Criteria

All binary; each cites the section that must contain it.

**Decisions**
- [ ] All twelve `PET_RULES.md` §5.2 items are classified as
      **RESOLVED** or as an explicit **OPEN HUMAN GAMEPLAY DECISION**
- [ ] No Pet XP value was invented — every resolved value traces to a §4
      product-owner decision
- [ ] No value was supplied by the executing agent on the product owner's
      behalf

**Structure preserved**
- [ ] Pet XP ownership remains the **Pet instance** (`PET_RULES.md` §5.1)
- [ ] Pet Level remains **independent** of Player Level
- [ ] `PetDefinition` is not recorded as owning instance XP or Level
- [ ] `PetLevelMultiplier` remains **RETIRED** and is not given a new purpose

**Reward targeting (items 5–8)**
- [ ] PET BATTLE REWARD TARGETING SEMANTICS is stated explicitly in
      `PET_RULES.md` §5 as its own rule
- [ ] The `BattleWon` Pet XP grant is explicit
- [ ] The `BattleLost` Pet XP grant is explicit
- [ ] Which Pet(s) receive XP is explicit — active-Pet-only is **not**
      assumed without a recorded decision
- [ ] Non-targeted owned Pets' treatment is explicit — "0 XP" is **not**
      assumed without a recorded decision

**Progression (items 9–12)**
- [ ] The `Pet.XP → Pet.Level` function is explicit and deterministic, or
      explicitly deferred
- [ ] Post-Level-50 Pet XP behavior is stated as its **own** answer
- [ ] Whether the Pet curve equals the Player curve is stated as its **own**
      answer, not collapsed into item 10/12
- [ ] Whether the Pet reward amount equals the Player reward amount is
      stated as its **own** answer
- [ ] The Pet **reward amount** and the Pet **curve constant** are recorded
      as two independent values (`COMBAT_RULES.md` §7.3 pattern)

**Persistence**
- [ ] `Pet.XP` and `Pet.Level` persistence semantics are explicit in
      `DATABASE.md` §1/§3
- [ ] No new table, entity, column beyond those the resolved contract
      requires, or speculative structure was introduced
- [ ] `DATABASE.md` §3 states the Pet XP/Level constraints as far as they
      are decided, and marks the remainder as unresolved if deferred

**RewardSummary**
- [ ] The `RewardSummary` Pet-track dependency is **resolved**: either the
      member list is documented from decided semantics, or the dependency is
      explicitly documented as still open
- [ ] No speculative Pet reward field is frozen
- [ ] `DATABASE.md` §1, `API_CONTRACTS.md` §4, and `GAME_EVENTS.md` §2 do
      **not** contradict each other

**Documentation**
- [ ] Only the smallest set of owner documents actually changed
      (no unrelated cleanup; `AGENTS.md` §16)
- [ ] No rule is duplicated across documents
      (`documentation/documentation-change.md` §2)
- [ ] `COMBAT_RULES.md` §7 (Player track) is unmodified except for a
      cross-reference if required
- [ ] `GAME_STATE.md` was not modified unless a concrete contradiction was
      proven and reported

**ADR**
- [ ] The §9 outcome is determined and justified (No change / Amend / New)
- [ ] If a new ADR: the next free number was **verified** at execution time
      and no existing ADR was overwritten
- [ ] If amended: historical rationale preserved; only affected statements
      changed
- [ ] `docs/03-decisions/README.md` §7 is consistent with the outcome

**Task discipline**
- [ ] No source code modified (`git status -- src/` clean)
- [ ] No test modified (`git status -- tests/` clean)
- [ ] No migration created or modified
- [ ] No completed task modified (`git status -- tasks/completed/` clean)
- [ ] TASK-059 is **unmodified**
- [ ] No gameplay outside Pet XP progression is introduced
- [ ] No implementation task is created by this task

**Implementation readiness (§13)**
- [ ] Every value in the §13 readiness list is deterministic, or explicitly
      deferred by the product owner
- [ ] A separate implementation task can be generated afterwards **without
      making any further gameplay decision**

---

## 11. Validation Requirements

Documentation-only task: verification is by search and re-read, not test
execution (`core/validation.md` §1 — documentation validation layer).
**Do not run implementation tests.**

### Required Verification
```text
[ ] 1.  Every §5.2 item is RESOLVED or explicitly OPEN — no third state
[ ] 2.  No third state, no "configurable later" substitute for a value
[ ] 3.  No Pet XP value lacks a §4 product-owner decision behind it
[ ] 4.  PET_RULES.md §5.1 ownership statements are intact
[ ] 5.  PET_RULES.md §5.3 retirement statements are intact
[ ] 6.  Reward targeting is a single explicit rule covering items 5–8
[ ] 7.  Items 10, 11, 12 are three separate stated answers
[ ] 8.  Pet reward amount and Pet curve constant are separate concepts
[ ] 9.  RewardSummary Pet-track dependency resolved (§8)
[ ] 10. DATABASE.md / API_CONTRACTS.md / GAME_EVENTS.md agree
[ ] 11. ADR outcome justified; numbering verified if a new ADR was made
[ ] 12. No completed task modified; TASK-059 unmodified
[ ] 13. No source/test/migration changes
[ ] 14. All changed documents are internally consistent
```

### Static Searches
```text
[ ] "PlayerLevelMultiplier" / "PetLevelMultiplier": 0 NEW hits in
    authoritative docs; retirement statements intact
[ ] no Player XP constant (100) copied into a Pet rule without an
    explicit §4 decision authorizing that equality
[ ] no "Pet.Level = ... Player.Level ..." derivation reintroduced
[ ] no invented Pet entity/table token (PetXPHistory|PetXPLog|
    PetLevelHistory|PetProgression)
[ ] no forbidden token: PetAttack|PetDefense|PetHP|PetCrit|PetPower
[ ] no token: Prestige|Paragon|Season XP|ascension|Evolution|
    rested XP|energy|gacha|quest
[ ] every Pet XP number that appears in docs/ resolves to a §4 decision
[ ] Consistency re-read — PET_RULES.md, DATABASE.md, API_CONTRACTS.md,
    GAME_EVENTS.md, MVP_SCOPE.md, GDD.md, ADR-016 read together:
    no duplicated definition, no residual contradiction
[ ] Git scope — `git status --porcelain` shows only docs/ (+ this task file
    and any explicitly permitted tasks/ change); nothing in src/ or tests/
```

### Key Edge Cases
- A deferred item must be visibly `OPEN HUMAN GAMEPLAY DECISION`, not a
  quiet gap a reader could mistake for an oversight.
- Item 9 must not be answered by pointing at the Player formula without an
  explicit decision that the two curves are the same.
- Items 11 and 12 are relationship decisions, not values: answering
  "same as Player" still requires the explicit decision to be recorded.
- Any Pet `BattleLost` grant of `0` requires a recorded decision — it may
  not be inherited from the Player's `+0`.

---

## 12. Relationship to TASK-059 / the Implementation Task

```text
TASK-059 (DONE) — two-track structure + full Player contract;
                  Pet structure decided, Pet balance deferred to §5.2
        ↓
TASK-060 (this task) — resolves the twelve Pet XP decisions into a
                  deterministic, implementation-ready Pet contract
        ↓
Pet XP implementation task (NEW ID) — generated ONLY after this task is
                  DONE, and ONLY if §13 readiness holds. NOT created here.
```

- **TASK-059 is DONE and immutable** — do not modify it (`AGENTS.md` §16,
  `TASK_LIFECYCLE.md` §3).
- **The implementation task is out of scope** for this task. Do not create
  it, do not scaffold it, do not partially specify it in code.
- The implementation task must not need to make a gameplay decision. If it
  would, this task is not DONE — report the gap instead (§11 item 1).

---

## 13. Implementation-Readiness Requirement

This task is successful **only if**, when DONE, another task can implement
Pet XP without making gameplay decisions. The contract must be deterministic
for every item below (or explicitly deferred by the product owner):

```text
Pet XP ownership
Pet XP persistence
Initial Pet XP
Initial Pet Level
Pet Level range
Pet XP reward on win
Pet XP reward on loss
Reward target
Inactive Pet behavior
Pet XP → Level formula
Level 50 behavior
Progression curve relationship
Reward amount relationship
```

Report this list in the Completion Evidence, marking each
`DETERMINISTIC` or `DEFERRED BY PRODUCT OWNER`.

---

## 14. Stop Conditions

### STOP CONDITION — FIRED AT EXECUTION START

```text
STOP CONDITION

Problem:
Execution was attempted, but §4's twelve product-owner Pet XP decisions
were not supplied. §4 still reads "<PENDING PRODUCT-OWNER DECISION>" for
all twelve items, and no authoritative document, ADR, task, or
repository file supplies any of them. The task's own stop condition
"§4 is still unpopulated at execution time" fired before any contract
decision could be made.

Relevant sources:
tasks/blocked/TASK-060-resolve-pet-xp-progression-and-reward-contract.md
  §3 (Human Decisions Required) and §4 (Decision Matrix — the PENDING
  block, lines 228–239)
AGENTS.md §7, §20 (missing rule → STOP, do not invent)
GAME_RULES.md §20 (Rule Change Policy — only a human decision makes a
  proposed mechanic authoritative)
.ai/README.md §13 (stop-condition format)
docs/01-game-design/PET_RULES.md §5.2 (the twelve items, all still
  "OPEN HUMAN GAMEPLAY DECISION")

Conflict / missing information:
The twelve Pet XP balance/reward decisions (persistence, initial values,
range, reward targeting, per-outcome amounts, XP → Level formula, and the
three Player-track relationship answers) are gameplay/product decisions.
They are not derivable from any authoritative source. Deriving them from
the Player track, from TASK-024, from the retired PetLevelMultiplier, or
from implementation behavior is explicitly forbidden by this task's §3,
§6, §7 and §14.

Proposed resolution:
The human product owner supplies all twelve decisions by replacing the
"<PENDING PRODUCT-OWNER DECISION>" placeholders in §4 with explicit
values/rules. No documentation change is required to supply them — the
task file is the designated input channel.

Waiting for:
Explicit product-owner decisions for §4 items 1–12.
```

The task may resume (`BLOCKED → IN PROGRESS`, `TASK_LIFECYCLE.md` §3)
once §4 is populated. No authoritative documentation was modified.

### Task-Specific Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always
apply. Task-specific conditions:

- **If a Pet XP value must be invented** → STOP; do not guess
  (`AGENTS.md` §7, `GAME_RULES.md` §20).
- **If Player XP values are being copied into Pet XP without explicit human
  authorization** → STOP. The Player's `+100`, `+0`, curve constant `100`,
  range `[1, 50]`, and initial values are Player-track decisions only.
- **If `PetLevelMultiplier` is proposed for reuse** in any form → STOP
  (`PET_RULES.md` §5.3, `ADR-016` item 13).
- **If implementation behavior is treated as gameplay authority** → STOP
  (`.ai/README.md` §18 — docs describe intended behavior).
- **If `RewardSummary` requires speculative Pet fields** → STOP; document
  the dependency instead (`AGENTS.md` §7, §8 of this task).
- **If an unresolved gameplay decision is silently converted into
  "configurable later"** → STOP. A configuration hook is not a decision.
- **If ADR ownership is ambiguous** (No change vs Amend vs New, or the next
  free number is unclear) → STOP (`architecture/adr-change.md` §2–§3).
- **If the task lifecycle requires modifying a completed task** → STOP;
  create a follow-up task instead (`AGENTS.md` §16).
- **If implementation becomes necessary** to complete the contract → STOP
  (this task is documentation-only).
- **If a new architectural decision is required beyond §9's options** →
  STOP and report.
- **If §4 is still unpopulated at execution time** (no product-owner
  decisions recorded) → STOP; this task cannot invent them.
- **If a Player XP value would need to change** → STOP; §2.1 is frozen by
  TASK-059 and out of this task's authority.
- **If a Pet XP answer would require an `MVP_SCOPE.md` §2 OUT item** → STOP
  (`AGENTS.md` §8).
- **If the resolved contract cannot be made deterministic for §13** → STOP
  and report which decision is missing; do not silently defer it.
- **If the task expands beyond the §7 owner documents** without a
  documented reason → STOP; report it, do not apply it (`AGENTS.md` §16).
- **If the task exceeds 7 skills or crosses multiple uncoupled
  architectural boundaries** → STOP & decompose.

When stopped, report the exact conflict and **do not invent a resolution**.

---

## 15. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- None. No authoritative documentation was modified.

### Decision Resolution
- `§5.2 item 1` — NOT RESOLVED (§4 pending) — Pet XP persistence semantics
- `§5.2 item 2` — NOT RESOLVED (§4 pending) — Initial Pet XP
- `§5.2 item 3` — NOT RESOLVED (§4 pending) — Initial Pet Level
- `§5.2 item 4` — NOT RESOLVED (§4 pending) — Pet Level range
- `§5.2 item 5` — NOT RESOLVED (§4 pending) — Pet XP per BattleWon
- `§5.2 item 6` — NOT RESOLVED (§4 pending) — Pet XP on BattleLost
- `§5.2 item 7` — NOT RESOLVED (§4 pending) — active-Pet-only targeting
- `§5.2 item 8` — NOT RESOLVED (§4 pending) — inactive Pet behavior
- `§5.2 item 9` — NOT RESOLVED (§4 pending) — Pet XP → Pet Level formula
- `§5.2 item 10` — NOT RESOLVED (§4 pending) — post-Level-50 Pet XP
- `§5.2 item 11` — NOT RESOLVED (§4 pending) — Pet curve == Player curve
- `§5.2 item 12` — NOT RESOLVED (§4 pending) — Pet reward == Player reward

None of the twelve could be resolved, because none has a product-owner
decision behind it. Zero values were supplied by the executing agent.

### Validation Results
- `§4 PENDING-block scan (whole repo)` — PASS: all twelve items still read
  `<PENDING PRODUCT-OWNER DECISION>`; no other file supplies them
- `Pet XP numeric-value scan across docs/` — PASS: no Pet XP reward, curve,
  cap, initial value, or range appears anywhere in authoritative docs
- `git status --porcelain` — only the pre-existing TASK-059 docs changes
  plus TASK-060's own status move (see below)
- No documentation validation was applicable: no documentation changed

### Implementation Readiness (§13)
Blocked before assessment — the contract cannot be written, so no readiness
item is DETERMINISTIC. All thirteen remain **undecided**:

```text
Pet XP ownership          — structure DECIDED (TASK-059); semantics blocked
Pet XP persistence        — NOT DETERMINISTIC (§5.2 item 1)
Initial Pet XP            — NOT DETERMINISTIC (§5.2 item 2)
Initial Pet Level         — NOT DETERMINISTIC (§5.2 item 3)
Pet Level range           — NOT DETERMINISTIC (§5.2 item 4)
Pet XP reward on win      — NOT DETERMINISTIC (§5.2 item 5)
Pet XP reward on loss     — NOT DETERMINISTIC (§5.2 item 6)
Reward target             — NOT DETERMINISTIC (§5.2 item 7)
Inactive Pet behavior     — NOT DETERMINISTIC (§5.2 item 8)
Pet XP → Level formula    — NOT DETERMINISTIC (§5.2 item 9)
Level 50 behavior         — NOT DETERMINISTIC (§5.2 item 10)
Progression curve rel.    — NOT DETERMINISTIC (§5.2 item 11)
Reward amount rel.        — NOT DETERMINISTIC (§5.2 item 12)
```

A Pet XP implementation task **cannot** be generated. §12 of this task
explicitly forbids creating one while gameplay decisions are outstanding.

### Final Report (required format)

```text
## Status
BLOCKED

## Blocking Condition
See the STOP CONDITION report in §14. The twelve product-owner Pet XP
decisions were not supplied; §4 still reads <PENDING PRODUCT-OWNER
DECISION> for all twelve items.

## Completed So Far
- Read the task contract, workflow, template, agent roles, and all
  authoritative documents listed in §5.
- Verified the current authoritative state: TASK-059's two-track
  structure and the full Player contract are in force; PET_RULES.md §5.2
  still records all twelve Pet XP items as OPEN HUMAN GAMEPLAY DECISION.
- Verified no product-owner decision exists anywhere in the repository
  (task files, docs/, ADRs, agents, workflows).
- Verified no Pet XP numeric value appears in any authoritative document.
- Applied the required lifecycle transition and recorded this STOP
  CONDITION.

## What Is Needed to Unblock
Explicit product-owner decisions for all twelve items in §4.

## Source Code
No source code changed.

## Tests
No tests run; no documentation changed, so no documentation validation
was applicable.
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code written)
- [x] Confirmed no documentation modified — nothing to violate MVP scope
- [x] Confirmed no `PlayerAttack`/`PlayerDefense`/`PlayerHP`/`PlayerCrit`/
      `PlayerPower` or equivalent introduced
- [x] Confirmed no Prestige, Paragon, Season XP, Evolution, or other extra
      progression system introduced
- [x] Confirmed no Player XP value was copied into a Pet rule (no Pet rule
      was written at all)
