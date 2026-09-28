# TASK-062 — Finalize the Pet XP Progression Contract in Authoritative Documentation

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section; copies only the Product Owner
  decisions that are this task's input (they exist nowhere in docs/ yet).

  THIS TASK IS DOCUMENTATION-ONLY. It transcribes twelve ALREADY-DECIDED
  Product Owner values into the owning authoritative documents. It resolves
  and synchronizes a contract. It does NOT implement source code and does
  NOT create the Pet XP implementation task.

  THE VALUES ARE INPUT, NOT DELIBERATION. Do not re-derive, rebalance,
  optimize, or question any number in §2.
-->

---

## Metadata

```text
Task ID:           TASK-062
Type:              DOCUMENTATION
Status:            DONE
Risk:              HIGH (retires TASK-060's twelve open items inside a
                   cross-referenced progression contract, and the Pet XP cap
                   semantics deliberately DIVERGE from the Player track —
                   Player XP is uncapped, Pet XP is hard-capped at 4900.
                   tasks/TASK_TYPES.md §4 caps DOCUMENTATION at MEDIUM for
                   "affects a cross-referenced contract"; raised to HIGH for
                   the same reason TASK-059/TASK-060 were — the contract also
                   gates the RewardSummary Pet track and a downstream
                   implementation task cannot start until it lands)
Priority:          HIGH (closes TASK-060's blocking items and the
                   RewardSummary Pet-track dependency)
Primary Agent:     gameplay (domain-rule content owner — PET_RULES.md §5 /
                   the Pet XP progression contract)
Supporting Agents: review (contract consistency),
                   persistence (Pet.XP / Pet.Level persistence semantics in
                   DATABASE.md),
                   backend (RewardSummary Pet-track + event/API alignment)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-061 (the Product Owner decision record this task
                   transcribes — read-only, must not be modified),
                   TASK-060 (BLOCKED — the contract task whose §4 items this
                   finalization satisfies; NOT modified by this task),
                   TASK-059 (DONE — established the two-track structure and
                   the frozen Player contract; read-only),
                   TASK-023 (DONE — Player.XP / Player.Level persistence;
                   read-only)
Blocks:            TASK-060's resumption, the Pet XP implementation task
                   (NOT created here)
Estimate:          Complex (4 skills; documentation-only, zero code; all
                   twelve values are supplied, none is the agent's to choose)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE`:
`tasks/TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change
"propagat[ed] … through implementation and tests", and
`.ai/workflow/development/gameplay-change.md` §3 ends in
`core/implementation.md` → `quality/testing.md`. This task's scope (§8)
forbids all code, so that workflow cannot complete. The Pet XP
implementation is a **separate follow-up task** (§10). The precedent set by
TASK-045/046/048/050/051/056/058/059/060 — all contract-formalization tasks
typed `DOCUMENTATION` — governs here. Per §16 of the generating request, the
Product Owner decisions are already made; this task invents no gameplay rule.

**Input note.** Unlike TASK-060 (which had to *decide*), this task already
has **all twelve values as input** (§2). No agent deliberation is required or
permitted. If any value in §2 appears internally inconsistent during
execution, STOP (§9) — do not repair it silently.

---

## 1. Objective

Transcribe the twelve **already-decided** Product Owner Pet XP values (§2)
into the owning authoritative documents, replacing `PET_RULES.md` §5.2's open
list with the decided contract, so that the Pet XP progression contract
becomes complete, deterministic, and implementation-ready.

This task **records and synchronizes**; it does not decide, implement, or
create follow-up tasks. **No source code, no migration, no schema, no tests.**

---

## 2. Product Owner Decisions — Authoritative Input (FIXED)

These are decisions, not proposals. Write them into the owner documents per
§7. **Do not re-derive, adjust, round, optimize, or replace any value below.**

### 2.1 Persistence / Initial State — DECIDED

```text
Pet XP persists permanently with the Pet instance.

Storage ownership:  PlayerPet / Pet instance
Not owned by:       PetDefinition

Initial Pet XP    = 0
Initial Pet Level = 1
Pet Level range   = [1, 50]
```

### 2.2 Battle Reward Targeting — DECIDED (items 5–8 are ONE rule)

```text
BattleWon
    ├── Player receives +100 XP        (Player track — unchanged, §3)
    └── Active combat Pet receives +100 Pet XP

BattleLost
    ├── Player receives +0 XP          (Player track — unchanged, §3)
    └── Active combat Pet receives +0 Pet XP

Inactive owned Pets
    └── +0 XP from that battle
```

```text
Only the ACTIVE combat Pet receives battle XP.
Inactive owned Pets receive 0 XP from that battle.
There is no passive / shared / passive-income XP mechanism for inactive Pets.
To train a Pet, that Pet must be the active combat Pet.
```

### 2.3 Progression — DECIDED

```text
Pet.Level = min(floor(Pet.XP / 100) + 1, 50)
```

Worked values (authoritative):

```text
Pet.XP = 0      →  Pet Level 1
Pet.XP = 100    →  Pet Level 2
Pet.XP = 4900   →  Pet Level 50
```

`Pet.XP = 4900` corresponds to 49 `BattleWon` rewards of 100 Pet XP.

### 2.4 Relationship With the Player Track — DECIDED

```text
10. Pet XP STOPS accumulating at Level 50.
    Maximum stored Pet XP = 4900.
    No XP overflow is retained beyond 4900.
    (This DIVERGES from Player XP, which is uncapped — §3.)

11. SAME XP → Level curve shape as Player.
    Both use: Level = min(floor(XP / 100) + 1, 50)
    The two XP pools remain completely independent.

12. NOT the same battle XP reward as Player — the amounts are independent.
    Player XP per BattleWon = 100   (Player track — unchanged, §3)
    Pet XP per BattleWon    = 100   (Pet track — decided here)
```

```text
IMPORTANT — items 5/6 (Pet reward amount) and item 9 (Pet curve constant)
are TWO INDEPENDENT CONCEPTS that happen to share the value 100. They must
be documented as separate concepts (COMBAT_RULES.md §7.3 is the structural
precedent). Changing one must never silently rewrite the other.
```

### 2.5 Product Owner Rationale (record where appropriate)

```text
Pet is the primary combat progression track, so it reaches its cap faster
than the Player track.

Pet target pacing:    ~49 wins to reach Pet Level 50.
Player target pacing: ~49 wins to reach Player Level 50 at 100 XP per win.
Player Level is account/content progression and does not directly increase
combat power.
Both tracks use the same formula shape to keep MVP progression simple, while
reward amounts remain independently tunable.
Pet XP is hard-capped at 4900 at Level 50 to avoid unnecessary MVP
overflow/prestige complexity.
```

**Do not add new balance mechanics.** Do not introduce prestige, paragon,
season XP, rested XP, or any overflow/currency system.

---

## 3. Player XP Contract — FROZEN, DO NOT TOUCH

The Player track is **frozen**. It is **not** one of the twelve decisions and
is **not** open for reconsideration by this task.

```text
BattleWon  →  Player XP +100        (COMBAT_RULES.md §7.2 — FROZEN)
BattleLost →  Player XP +0          (COMBAT_RULES.md §7.2 — FROZEN)
Player.Level = min(floor(Player.XP / 100) + 1, 50)
Player XP is UNCAPPED; Player Level is capped at 50.
Initial Player XP = 0; initial Player Level = 1; range [1, 50].
```

**Do not modify, reinterpret, or reopen any Player value.** In particular:

```text
Player XP reward amount      = 100   (CONFIGURATION)
Player XP curve constant     = 100   (FORMULA)
```

Keep the Player reward-amount vs. curve-constant distinction intact
(`COMBAT_RULES.md` §7.3). Do not confuse:

```text
Player reward amount = 100  and  Pet reward amount = 100   (two tracks)
```

with:

```text
the shared curve constant = 100                            (both formulas)
```

These are three separate concepts. The Player contract must remain
byte-for-byte semantically unchanged by this task.

---

## 4. Cap Semantics — MUST BE EXPLICIT

The finalized documentation must state the cap distinction plainly. This is
the Pet track's deliberate divergence from the Player track and must not be
left implicit:

```text
                         Player track        Pet track
──────────────────────   ────────────────    ────────────────
Reward per BattleWon     +100                +100
Level formula            min(floor(XP/100)+1, 50)   min(floor(XP/100)+1, 50)
Level maximum            50                  50
XP maximum               NONE (uncapped)     4900 (HARD CAP)
XP after Level 50        keeps accumulating  NOT awarded / NOT stored
```

```text
Pet Level maximum = 50
Pet XP maximum    = 4900
```

**Do not document this as merely "Level is capped" while leaving XP
accumulation ambiguous.** For the Pet track, state explicitly that once a Pet
reaches Level 50, additional Pet XP is neither awarded nor stored, and no
overflow is retained.

---

## 5. Ownership — MUST BE EXPLICIT

```text
Pet XP belongs to the Pet INSTANCE (the PlayerPet / owned Pet row).
PetDefinition does NOT own runtime Pet XP (or runtime Pet Level).
```

The `PetDefinition` table remains static content. Do **not** introduce
historical XP tables or progression entities:

```text
PetXPHistory · PetXPLog · PetLevelHistory · PetProgression   — FORBIDDEN
```

**No new table, entity, or column beyond the `Pet.XP` / `Pet.Level` columns
already documented in `DATABASE.md` §1.** Do not write migrations or modify
EF Core models.

---

## 6. RewardSummary Dependency

`DATABASE.md` §1 currently documents the Pet track as "NOT finalizable yet"
because `PET_RULES.md` §5.2 items 5–9 were undecided. This task resolves them.

Determine and apply exactly one outcome:

```text
IF the finalized Pet XP contract determines the RewardSummary Pet semantics
   → document the minimum necessary Pet members in DATABASE.md §1, consistent
     with the existing API contract conventions, and reconcile
     API_CONTRACTS.md §4 / GAME_EVENTS.md §2 so all three describe ONE
     contract. Remove the "NOT finalizable yet" dependency wording.

IF the authoritative API contract INTENTIONALLY keeps the Pet track absent
   until implementation
   → preserve that contract and document only the remaining dependency
     explicitly. Do not add placeholder Pet fields.
```

The finalized contract now determines: which Pet is targeted (the active
combat Pet — a single, unambiguous recipient), the Pet XP amount per outcome,
and the Pet XP → Level formula. The arity that previously blocked the shape is
therefore **resolved**: one Pet, the active combat Pet.

**Do not invent unrelated reward fields.** Do not freeze a Pet member whose
meaning is not fully determined by §2.

---

## 7. Documentation Ownership

Confirm these owners before editing, and use the **smallest set that actually
changes** (`documentation/documentation-change.md` §1–§3 — one concept, one
owner; do not edit every referencing document).

| Concept | Owner |
|---|---|
| Pet XP / Pet Level progression rules, reward targeting, XP → Level formula, cap semantics | `docs/01-game-design/PET_RULES.md` §5 (replace §5.2's open list with the decided contract) |
| Pet XP / Pet Level persistence semantics | `docs/02-technical/DATABASE.md` §1 + §3 |
| `RewardSummary` member list + Pet-track dependency | `docs/02-technical/DATABASE.md` §1 ("Reward semantics for `RewardSummary`") |
| Reward-summary REST/event reconciliation | `docs/02-technical/API_CONTRACTS.md` §4 notes 1/3 (only if wording goes stale) |
| Reward-summary event-payload rule | `docs/02-technical/GAME_EVENTS.md` §2 (only if wording goes stale) |
| Pet rule cross-reference in the core rule set | `docs/01-game-design/GAME_RULES.md` §9.3 (only if stale) |
| MVP scope classification of Pet progression | `docs/00-overview/MVP_SCOPE.md` §1 (only if stale) |
| Meta-progression overview wording | `docs/00-overview/GDD.md` §6/§14 (only if stale) |
| Decision rationale | `ADR-016` — per §11 |

`COMBAT_RULES.md` §7 owns the **Player** track. It is **not** the owner of any
Pet value and must not be edited to carry Pet rules. `GAME_STATE.md` is **not**
to be modified unless a concrete authoritative BattleState contract requires
it (expected: no change). `ROADMAP.md` is expected to need no change.

For each modification: identify the exact section; preserve existing
terminology; **remove obsolete `<PENDING PRODUCT-OWNER DECISION>` and
`UNRESOLVED` placeholders** that §2 now resolves; remove contradictory old Pet
XP assumptions; preserve historical changelog information where required.

---

## 8. Scope

### In Scope

1. Record all twelve Product Owner decisions (§2) in `PET_RULES.md` §5,
   replacing §5.2's open list with the decided contract.
2. State **PET BATTLE REWARD TARGETING SEMANTICS** explicitly as one coherent
   rule (items 5–8): the `BattleWon` grant, the `BattleLost` grant, the active
   combat Pet as sole recipient, and inactive owned Pets receiving 0.
3. Document the **cap semantics** (§4): Pet Level max 50, Pet XP max 4900,
   no award/storage after Level 50, no overflow — as distinct facts.
4. Record the **reward amount** and the **curve constant** as two independent
   concepts that happen to share the value 100.
5. Record items 10, 11, 12 as **three separate** stated answers.
6. Document Pet persistence semantics in `DATABASE.md` §1 + §3.
7. Resolve the `RewardSummary` Pet-track dependency per §6.
8. Update `MVP_SCOPE.md` §1 and `GDD.md` §6/§14 only where §2 makes their
   wording stale.
9. Determine the §11 ADR outcome and keep `docs/03-decisions/README.md` §7
   consistent if anything changes.
10. Update TASK-060 §4's decision slots to reference the finalized contract —
    **only if** the repository workflow requires TASK-060's stop-condition
    record to reflect its now-satisfied input. **This task must not resolve,
    close, or re-activate TASK-060's lifecycle** (§10).

### Out of Scope — MUST NOT be implemented by this task

```text
Any source code (src/backend/, src/frontend/) · any test change (tests/) ·
any EF Core entity/configuration change · any database migration · any new
table, entity, or column · PetXPHistory / PetXPLog / PetLevelHistory /
PetProgression or any other speculative entity · Redis / SignalR / Phaser /
React changes · Pet XP calculation code · Pet Level calculation code ·
reward application · reward persistence · reward API implementation ·
BattleState runtime changes · Pet XP services · Pet level services ·
RewardSummary implementation ·
Pet combat mechanics · Pet evolution · Pet tiers · Pet stars ·
skills · passives · relics · cards · rewards beyond XP ·
Match-3 / board / gems / swap / match detection / cascade · combat damage ·
Boss gameplay · gacha · quests · energy · prestige · paragon ·
season progression · rested XP · overflow/currency systems ·
any MVP_SCOPE.md §2 OUT item
```

Also out of scope: **modifying any Player XP value** (§3), reopening the Pet
ownership structure, reviving `PetLevelMultiplier`, modifying TASK-059 /
TASK-061 / TASK-024 / any completed task (`AGENTS.md` §16), and creating the
Pet XP implementation task (§10).

---

## 9. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always
apply. Task-specific conditions:

- **If a Product Owner value in §2 appears internally inconsistent** → STOP;
  report the exact inconsistency; do not repair it silently.
- **If a §2 value conflicts with existing authoritative architecture** →
  STOP per `AGENTS.md` §4; report both sources.
- **If any Player XP value would need to change** → STOP. §3 is frozen and
  out of this task's authority.
- **If `PetLevelMultiplier` is proposed for reuse** in any form → STOP
  (`PET_RULES.md` §5.3, `ADR-016` item 13).
- **If `RewardSummary` semantics conflict and require a NEW gameplay or API
  decision** not determined by §2 → STOP; document the dependency instead.
- **If ADR handling requires an unresolved architectural decision** → STOP
  (`architecture/adr-change.md` §2–§3).
- **If implementation details become necessary to define the contract** →
  STOP (this task is documentation-only).
- **If a Pet rule would reintroduce `Player.Level × PetLevelMultiplier`** →
  STOP; that model is retired.
- **If the finalized contract appears to make Pet XP uncapped, restore
  overflow, or grant inactive Pets XP** → STOP; that contradicts §2.
- **If a §2 answer would require an `MVP_SCOPE.md` §2 OUT item** → STOP
  (`AGENTS.md` §8).
- **If the task expands beyond the §7 owner documents** without a documented
  reason → STOP; report it, do not apply it (`AGENTS.md` §16).
- **If this task is asked to resolve or re-activate TASK-060's lifecycle** →
  STOP; that is TASK-060's own action when it resumes (`TASK_LIFECYCLE.md` §3).
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries** → STOP & decompose.

When stopped, report the exact conflict and **do not invent a resolution**.

---

## 10. Relationship to TASK-061 / TASK-060 / the Implementation Task

```text
TASK-061 (Product Owner decision record)
        ↓  supplies the twelve values
TASK-062 (this task) — finalizes those values into authoritative docs/
        ↓  contract becomes complete and deterministic
TASK-060 (BLOCKED) — consumes the finalized contract; its §4 items are now
        │            satisfiable. TASK-060's lifecycle transition happens
        │            when TASK-060 itself resumes.
        ↓
Implementation-readiness audit
        ↓
Pet XP implementation task (NEW ID — NOT created here)
```

- **TASK-061 supplies the Product Owner input. This task finalizes that input
  into authoritative documentation. TASK-060 consumes the finalized
  contract.**
- **Do not modify TASK-060** as part of this task beyond the optional §8 item
  10 record update. **Do not mark TASK-060 DONE. Do not implement TASK-060.**
  Its lifecycle transition occurs only when TASK-060 resumes under the
  repository workflow.
- **Do not modify TASK-061.** It records the Product Owner's decisions; this
  task transcribes them.
- **Do not create the Pet XP implementation task.**

---

## 11. ADR Considerations

Inspect `ADR-016` before deciding whether any ADR change is needed. ADR-016
already records the independent-progression architecture (items 9–12) and
explicitly defers the balance values (item 14, pointing at `PET_RULES.md`
§5.2).

```text
Option A — No ADR change.
    Use when the finalized values are BALANCE/CONFIGURATION and change no
    ownership, persistence strategy, or architectural boundary. ADR-016
    item 14 already points at PET_RULES.md §5.2 as the single enumeration;
    a balance number is not architectural
    (architecture/adr-change.md §1; development/gameplay-change.md §3
    "a balance number change is not architectural").

Option B — Minimal ADR-016 amendment.
    Use ONLY if finalizing the contract makes a statement ADR-016 currently
    makes stale — in particular item 14's "remain UNRESOLVED" wording, and
    the closing sentence "This ADR does not authorize implementation of
    unresolved Pet XP balance or reward-targeting semantics."
    Preserve historical rationale; change only what is now stale
    (architecture/adr-change.md §3, docs/03-decisions/README.md §5).

Option C — New ADR (next free number, verified at execution time).
    Use ONLY if finalization introduces a genuinely new architectural
    decision. Expected: NOT required — §2 changes no ownership, no
    persistence strategy, and no authority boundary.
```

**Do not create a duplicate ADR merely because TASK-060 is being finalized.**
A balance-value finalization is not an architectural decision. If the
numbering or the amendment-vs-new-ADR choice is ambiguous → **STOP** (§9).

---

## 12. Acceptance Criteria

All binary; each cites the section that must contain it.

**Decisions recorded**
- [ ] All twelve Product Owner decisions are represented in authoritative
      documentation
- [ ] Pet XP persistence with the Pet instance is documented
- [ ] Initial Pet XP = `0` is documented
- [ ] Initial Pet Level = `1` is documented
- [ ] Pet Level range `[1, 50]` is documented
- [ ] `BattleWon` awards **100** Pet XP to the **active combat Pet**
- [ ] `BattleLost` awards **0** Pet XP
- [ ] Inactive owned Pets receive **0** XP
- [ ] No passive/shared Pet XP mechanism exists in the documented contract
- [ ] Pet Level formula is exactly `min(floor(XP / 100) + 1, 50)`
- [ ] **Pet XP stops at 4900** and Pet Level cannot exceed 50
- [ ] Post-Level-50 behavior states that additional Pet XP is **not awarded
      or stored**, and no overflow is retained
- [ ] `PET_RULES.md` §5.2's open list is replaced by the decided contract;
      no `UNRESOLVED` / `<PENDING PRODUCT-OWNER DECISION>` placeholder
      remains for a now-resolved item

**Player track preserved**
- [ ] Player XP remains independently defined and semantically unchanged
- [ ] Player `BattleWon` XP remains **100** (frozen — §3)
- [ ] Player `BattleLost` XP remains **0**
- [ ] Player XP remains **uncapped**; Player Level remains capped at 50
- [ ] Pet and Player XP pools are documented as completely independent
- [ ] The cap divergence (Player uncapped vs. Pet hard-capped at 4900) is
      stated as an explicit, deliberate difference

**Concepts kept separate**
- [ ] Pet reward amount and Pet level-curve constant are documented as two
      independent concepts that share the value 100
- [ ] Items 10, 11, 12 are documented as three separate answers
- [ ] `PetLevelMultiplier` remains **RETIRED** and is not given a new purpose

**Persistence**
- [ ] `Pet.XP` / `Pet.Level` persistence semantics are explicit in
      `DATABASE.md` §1/§3
- [ ] `PetDefinition` is not recorded as owning runtime Pet XP or Level
- [ ] No speculative persistence entity or new table/column was introduced

**RewardSummary**
- [ ] Required `RewardSummary` Pet semantics are explicitly documented **or**
      explicitly confirmed as not yet required with the dependency stated
- [ ] No speculative or unrelated Pet reward field is frozen
- [ ] `DATABASE.md` §1, `API_CONTRACTS.md` §4, and `GAME_EVENTS.md` §2 do not
      contradict each other
- [ ] The stale "NOT finalizable yet" dependency wording is resolved

**Documentation**
- [ ] Only the smallest set of owner documents actually changed
      (no unrelated cleanup; `AGENTS.md` §16)
- [ ] No rule is duplicated across documents
      (`documentation/documentation-change.md` §2)
- [ ] `COMBAT_RULES.md` §7 (Player track) is semantically unmodified
- [ ] `GAME_STATE.md` was not modified unless a concrete BattleState contract
      required it, and the reason is recorded

**ADR**
- [ ] The §11 outcome is determined and justified (No change / Amend / New)
- [ ] If amended: historical rationale preserved; only stale statements
      changed; `docs/03-decisions/README.md` §7 consistent
- [ ] If a new ADR: the next free number was **verified** at execution time
      and no existing ADR was overwritten

**Task discipline**
- [ ] No source code modified (`git status -- src/` clean)
- [ ] No test modified (`git status -- tests/` clean)
- [ ] No migration created or modified
- [ ] No Redis/SignalR/Phaser/React behavior modified
- [ ] No completed task modified; TASK-059 and TASK-061 unmodified
- [ ] TASK-060 is not resolved, closed, or re-activated by this task
- [ ] No gameplay outside Pet XP progression is introduced
- [ ] No implementation task is created

---

## 13. Validation Requirements

Documentation-only task: verification is by search and re-read, not test
execution (`core/validation.md` §1 — documentation validation layer).
**Do not run implementation tests.**

### Required Verification
```text
[ ] 1.  All twelve decisions present in the owning documents
[ ] 2.  Pet XP formula appears in ONE owner section, referenced elsewhere
[ ] 3.  Pet XP max 4900 and Pet Level max 50 both explicit
[ ] 4.  Post-Level-50 Pet XP behavior explicit (not merely "Level capped")
[ ] 5.  Reward targeting is one coherent rule covering items 5–8
[ ] 6.  Items 10, 11, 12 are three separate stated answers
[ ] 7.  Pet reward amount and Pet curve constant are separate concepts
[ ] 8.  Player contract semantically unchanged (reward 100 / 0, uncapped)
[ ] 9.  RewardSummary Pet dependency resolved or explicitly preserved
[ ] 10. DATABASE.md / API_CONTRACTS.md / GAME_EVENTS.md agree
[ ] 11. ADR outcome justified; numbering verified if a new ADR was made
[ ] 12. No completed task modified; TASK-059 / TASK-061 unmodified
[ ] 13. No source / test / migration changes
[ ] 14. All changed documents are internally consistent
```

### Static Searches
```text
[ ] "UNRESOLVED" / "<PENDING PRODUCT-OWNER DECISION>" in PET_RULES.md §5:
    0 remaining for now-resolved items
[ ] "PetLevelMultiplier": 0 NEW authoritative-doc hits; retirement intact
[ ] no "Pet.Level = ... Player.Level ..." derivation reintroduced
[ ] no invented persistence entity token
    (PetXPHistory|PetXPLog|PetLevelHistory|PetProgression)
[ ] no forbidden token: PetAttack|PetDefense|PetHP|PetCrit|PetPower
[ ] no token: Prestige|Paragon|Season XP|ascension|Evolution|rested XP|
    energy|gacha|quest
[ ] Pet XP values in docs resolve to §2 (0, 1, [1,50], 100, 0, 4900)
[ ] Player XP reward is still 100 everywhere it appears
[ ] Consistency re-read — PET_RULES.md, COMBAT_RULES.md, GAME_RULES.md,
    DATABASE.md, API_CONTRACTS.md, GAME_EVENTS.md, MVP_SCOPE.md, GDD.md,
    ADR-016 read together: no duplicated definition, no residual
    contradiction
[ ] Git scope — `git status --porcelain` shows only docs/ (+ this task file
    and any explicitly permitted tasks/ change); nothing in src/ or tests/
```

### Key Edge Cases
- `Pet.XP = 4900 → Level 50` is the exact boundary; `5000` and above must be
  documented as **unreachable** for Pet (unlike Player), because Pet XP stops
  at 4900.
- The Player/Pet cap divergence must be stated, not left for a reader to infer
  from two separate sections.
- A `BattleLost` Pet grant of `0` must be documented explicitly, not inferred
  from the Player's `+0`.
- Inactive Pet behavior must be explicit, not inferred from "active Pet only".

---

## 14. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `docs/01-game-design/PET_RULES.md` — §5 restructured and finalized:
  §5.1 ownership, §5.2 Initial Values, §5.3 Pet Battle Reward Targeting
  Semantics, §5.4 XP → Pet Level Formula (incl. reward-vs-curve and
  Player-curve relationship), §5.5 Pet XP Cap and Post-Cap Behavior (incl.
  the deliberate Player/Pet cap divergence table), §5.6 Retired Terms,
  §5.7 Non-XP Pet Attributes; §1 data model XP/Level annotations;
  §6 reference corrected; version 3.0.
- `docs/02-technical/DATABASE.md` — §1 `Pet.XP`/`Pet.Level` annotations
  finalized; §1 "Reward semantics for `RewardSummary`" item 2 rewritten from
  "not finalizable" to "semantics decided, member list deferred"; §3 Pet
  constraints replaced (range, initial values, formula, 4900 cap);
  version 1.16.
- `docs/01-game-design/GAME_RULES.md` — §9.3 Pet XP reference finalized;
  version 3.1.
- `docs/00-overview/GDD.md` — §6 Pet System finalized; version 2.4.
- `docs/00-overview/MVP_SCOPE.md` — §1 Pets block finalized; version 1.4.
- `docs/02-technical/GAME_EVENTS.md` — §2 reward-summary item 2 finalized;
  version 2.6.
- `docs/02-technical/API_CONTRACTS.md` — §4 note 1 finalized; version 1.11.
- `docs/03-decisions/README.md` — §7 ADR-016 index row updated; partial
  supersession block: ADR-012 item 4 and the no-Evolution reference
  corrected; a note added recording that ADR-016 item 14 is satisfied;
  version 1.7.
- `docs/03-decisions/ADR/ADR-016-*.md` — **Amendment** added discharging
  item 14 and the authorization bar for the now-resolved Pet XP semantics;
  Context, Consequences, Option D, and Related Documents de-staled.
  Items 1–13 unchanged; rationale preserved.
- `docs/03-decisions/ADR/ADR-011-*.md` — Related Documents reference
  corrected.
- `docs/03-decisions/ADR/ADR-012-*.md` — supersession note: item 4 range now
  decided; no-Evolution reference corrected.
- `tasks/backlog/TASK-062-*.md` — this Completion Evidence.

### Decisions Recorded
- `§2 item 1` — Pet XP persistence → `PET_RULES.md` §5.1 item 6, `DATABASE.md` §1
- `§2 item 2` — Initial Pet XP `0` → `PET_RULES.md` §5.2, `DATABASE.md` §3
- `§2 item 3` — Initial Pet Level `1` → `PET_RULES.md` §5.2, `DATABASE.md` §3
- `§2 item 4` — Pet Level range `[1, 50]` → `PET_RULES.md` §5.5, `DATABASE.md` §3
- `§2 item 5` — `BattleWon` +100 → `PET_RULES.md` §5.3
- `§2 item 6` — `BattleLost` +0 → `PET_RULES.md` §5.3 item 2
- `§2 item 7` — active combat Pet only → `PET_RULES.md` §5.3 item 1
- `§2 item 8` — inactive Pets +0 → `PET_RULES.md` §5.3 item 3
- `§2 item 9` — `min(floor(XP/100)+1, 50)` → `PET_RULES.md` §5.4
- `§2 item 10` — hard cap 4900 → `PET_RULES.md` §5.5
- `§2 item 11` — same formula shape, independent pools → `PET_RULES.md` §5.4
- `§2 item 12` — independent reward amounts → `PET_RULES.md` §5.4

### Validation Results
- `stale-reference sweep (open/unresolved/not-finalizable)` — PASS after
  fixes; remaining hits are only inside `Prior N:` version-changelog
  paragraphs or the ADR-016 amendment's own historical framing.
- `broken cross-reference sweep` — PASS: 0 remaining `PET_RULES.md §5.2
  item N` references; the 3 `§5.3 item 4` (no-Evolution) pointers corrected
  to `§5.6 item 4`.
- `Pet XP cap semantics` — PASS: 4900 hard cap stated in `PET_RULES.md`
  §5.5, `DATABASE.md` §1/§3, `MVP_SCOPE.md` §1, `docs/03-decisions/README.md`;
  Player uncapped preserved and the divergence stated explicitly.
- `Player XP reward` — PASS: `+100` preserved at every site; no `50` reward
  figure anywhere; no `98 wins` claim.
- `forbidden tokens` — PASS: 0 speculative persistence entities; the
  `PlayerAttack`/`Prestige`/`Paragon` hits are prohibitions, and the single
  `Player.Level × PetLevelMultiplier` hit is the preserved ADR-012
  supersession history.
- `GAME_STATE.md` — not modified.
- `git status` — no changes under `src/`, `tests/`, or `tasks/completed/`.

### Final Report (required format)

```text
## Status
DONE

## Pet Contract
Pet.XP ownership:            the Pet INSTANCE (PlayerPet); NOT PetDefinition
Pet.XP persistence:          persists permanently with the Pet instance
Initial Pet XP:              0
Initial Pet Level:           1
Pet Level range:             [1, 50]
Pet Level formula:           min(floor(Pet.XP / 100) + 1, 50)
Pet Level maximum:           50
Pet XP maximum:              4900 (HARD cap)
Post-Level-50 Pet XP:        stops accumulating; not awarded or stored;
                             no overflow, no hidden XP, no prestige XP
BattleWon Pet XP:            +100 to the active combat Pet
BattleLost Pet XP:           +0 to the active combat Pet
XP recipient:                the active combat Pet ONLY (one Pet per battle)
Inactive Pet behavior:       +0; no passive/shared/party-wide/account-wide XP
Pet curve == Player curve shape?:  YES — identical formula shape, but the
                             pools are completely independent
Pet reward amount vs Player: INDEPENDENT amounts; both currently 100, which
                             does not imply a shared pool or variable
Pet XP cap divergence:       DELIBERATE — Player XP uncapped, Pet XP capped
                             at 4900; neither cap rule applies to the other

## Player Contract
Player reward (BattleWon/BattleLost):  +100 / +0  (FROZEN, unchanged)
Player XP cap:               uncapped (unchanged); Player Level capped at 50
Player contract changed?:    NO

## RewardSummary
The Pet reward SEMANTICS are now decided (PET_RULES.md §5.3): one battle
awards Pet XP to exactly one Pet — the active combat Pet. The arity that
previously blocked the shape is resolved. The Pet member LIST remains
deferred to the implementation task, which will define the representation
from the decided semantics (DATABASE.md §1). No placeholder field was
frozen; the `{}` staging value stays in force. DATABASE.md §1,
API_CONTRACTS.md §4, and GAME_EVENTS.md §2 remain one coherent contract.

## ADR
Amended ADR-016 (minimal, via architecture/adr-change.md §3). An Amendment
records that the twelve Pet XP decisions deferred by item 14 are finalized,
discharging (a) item 14's deferral and (b) the "does not authorize
implementation of unresolved Pet XP balance or reward-targeting semantics"
bar for those now-resolved semantics. No new ADR was created: the finalized
values are balance/configuration and introduce no architectural change
(adr-change.md §1). Decision items 1–13 and all historical rationale are
preserved unmodified. ADR-016 remains Accepted. Indexed consistently in
docs/03-decisions/README.md §7.

## Documentation Changed
docs/01-game-design/PET_RULES.md (v3.0 — primary owner, §5 restructured)
docs/02-technical/DATABASE.md (v1.16)
docs/01-game-design/GAME_RULES.md (v3.1)
docs/00-overview/GDD.md (v2.4)
docs/00-overview/MVP_SCOPE.md (v1.4)
docs/02-technical/GAME_EVENTS.md (v2.6)
docs/02-technical/API_CONTRACTS.md (v1.11)
docs/03-decisions/README.md (v1.7)
docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md
docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md
docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md
tasks/backlog/TASK-062-finalize-pet-xp-progression-contract.md (§14)

COMBAT_RULES.md — NOT modified (Player track frozen; correct as-is)
GAME_STATE.md — NOT modified (no contradiction found)
ROADMAP.md — NOT modified

## Untouched
TASK-059 · TASK-060 · TASK-061 · TASK-024 · completed tasks ·
COMBAT_RULES.md §7 (Player track) · GAME_STATE.md · Player XP values

## Source Code
No source code changed.

## Tests
No implementation tests run; documentation validation only.

## Implementation Readiness
YES for the contract: every gameplay decision required to implement Pet XP
is now deterministic — ownership, persistence, initial values, Level range,
both reward amounts, recipient, inactive-Pet behavior, the XP → Level
formula, the Level-50 rule, the 4900 XP cap, and both Player-track
relationships. No gameplay ambiguity remains. The Pet `RewardSummary` member
list is a representation choice for the implementation task, not an
undecided gameplay decision.

## Next Step
TASK-060 resumes on the finalized contract; the Pet XP implementation task
follows. Do not create the implementation task here.
```

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (no code written)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Pet progression
      IN; no §2 OUT item introduced)
- [x] Confirmed no `PlayerAttack`/`PlayerDefense`/`PlayerHP`/`PlayerCrit`/
      `PlayerPower` or equivalent introduced
- [x] Confirmed no Prestige, Paragon, Season XP, Evolution, or other extra
      progression system introduced
- [x] Confirmed no Player XP value was changed
