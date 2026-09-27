# TASK-068 — Resolve the `RewardSummary` Defeat-Shape Contradiction in `DATABASE.md` §1

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section; copies only what the
  executing agent needs in order to locate, adjudicate, and minimally
  correct one self-contained documentation contradiction.

  THIS TASK IS DOCUMENTATION-ONLY. IT RESOLVES EXACTLY ONE CONTRADICTION:
  whether a "defeat" RewardSummary carries the Player-track line items or
  is an empty object. It implements nothing, changes no value, and does
  not create any follow-up task.

  THE TASK DOES NOT PRE-SELECT AN OPTION. It documents both readings,
  surveys the authoritative evidence, and either (a) records a resolution
  that the authoritative documentation decisively supports, with
  citations, or (b) explicitly requires a human gameplay/product decision
  between Option A and Option B. It must never choose by preference.
-->

---

## Metadata

```text
Task ID:           TASK-068
Type:              DOCUMENTATION
Status:            DONE (HUMAN DECISION RECEIVED — the human selected
                    **Option A**; resolution applied to `DATABASE.md` §1 and
                    synchronized into `API_CONTRACTS.md` §4 and
                    `GAME_EVENTS.md` §2. See §14.)
Risk:              MEDIUM (TASK_TYPES.md §4 caps DOCUMENTATION at MEDIUM
                   for "affects a cross-referenced contract"; this
                   contradiction gates a BLOCKED implementation task and
                   lives in one clause of a contract that three other
                   documents reference, so it is not LOW - but no value,
                   gameplay rule, or code changes, so not HIGH)
Priority:          HIGH (TASK-067 is BLOCKED on exactly this question;
                   its §12.1 STOP CONDITION REPORT names TASK-068 as the
                   recommended follow-up (:997-1001))
Primary Agent:     review (documentation consistency - tasks/TASK_TYPES.md
                   §2 sets DOCUMENTATION's Primary Agent to "Review Agent
                   (documentation consistency)")
Supporting Agents: persistence (DATABASE.md §1 is the persistence-owned
                   contract this task edits),
                   backend (API_CONTRACTS.md §4 note 1 / note 3 cross-
                   reference accuracy),
                   gameplay (reward-semantics accuracy against
                   COMBAT_RULES.md §7 / PET_RULES.md §5.3 - the decision
                   must not alter either)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills - Normal budget, tasks/README.md §12)
Dependencies:      TASK-067 (BLOCKED - its §12.1 STOP CONDITION REPORT is
                   this task's input; read-only, must NOT be modified or
                   resumed by this task),
                   TASK-066 (DONE - READY verdict; F11 scoped the delegated
                   representation decision to the Pet member list only;
                   read-only),
                   TASK-065 (DONE - Player XP implementation; read-only,
                   must NOT be reopened),
                   TASK-059 (DONE - two-track RewardSummary structure;
                   §6.4 names are illustrative only; read-only)
Blocks:            TASK-067's resumption (BLOCKED -> IN PROGRESS, once -
                   and only once - §1 shows exactly one authoritative
                   defeat-shape semantics). This task creates NO
                   implementation task and NO TASK-069.
Estimate:          Normal (4 skills; documentation-only; one
                   contradiction, one decision, at most three documents
                   synchronized, zero code)
```

**Type classification note.** `DOCUMENTATION`, per `tasks/TASK_TYPES.md`
§2: the primary output is a change to `docs/` itself. Not `BUG` (no
code misbehaves), not `GAMEPLAY-CHANGE` (no rule value changes - §5 and
§9 freeze every value), not `ARCHITECTURE` (no architectural decision is
in scope; STOP if one appears - §11). The precedent of contract-
formalization tasks TASK-045/046/048/050/051/056/058/059/062 as
`DOCUMENTATION` governs here.

**Decision note.** This task is a **contract clarification**, not a
design task. Its whole purpose is to make the existing `RewardSummary`
contract deterministic. It must not broaden itself, and it must not
silently pick a side: see §5.

---

## 1. Objective

Resolve the documented contradiction inside `docs/02-technical/DATABASE.md`
§1, "Reward semantics for `RewardSummary`": item 1 defines a Player-track
member set with a per-outcome value for `playerXpGained` ("100 on a win,
0 on a loss"), while item 5 (and the §1 entity-block staging sentence at
`:541-543`) states that a `"defeat"` battle "carries no line items". The
two statements describe the same artifact - the `RewardSummary` document
written for a defeat - with mutually exclusive member arity.

After this task, **exactly one** defeat-shape semantics is authoritative,
the three cross-referencing documents (`DATABASE.md` §1,
`API_CONTRACTS.md` §4, `GAME_EVENTS.md` §2) express no contradictory
defeat semantics, and every reward **value** is unchanged: Player XP
`+100` win / `+0` loss, Pet XP `+100` win / `+0` loss, Pet XP cap and
formula untouched, `{}` staging meaning unchanged.

> **Final rule for the executing agent:** this task exists only to make
> the existing `RewardSummary` contract deterministic. Do not broaden it.
> Do not implement Pet XP. Do not redesign rewards. Do not guess between
> Option A and Option B.

---

## 2. Authoritative References

**Documents to inspect (only these, per the generating request):**

```text
docs/02-technical/DATABASE.md            §1 "Reward semantics for
                                          RewardSummary" (items 1-5) and
                                          the §1 BattleResult entity-block
                                          staging sentence (:541-543) -
                                          THE CONTAINER OF THE CONTRADICTION
docs/02-technical/API_CONTRACTS.md       §4 note 1 (:547-560) and note 3
                                          (:562-568) - delegating
docs/02-technical/GAME_EVENTS.md         §2 BattleWon/BattleLost item 2
                                          (:464-473) and the payload block
                                          (:450-455) - delegating; the
                                          event-payload rule is
                                          BattleWon-only
docs/01-game-design/PET_RULES.md         §5.3 (Pet reward values - to be
                                          preserved verbatim, not edited)
docs/01-game-design/COMBAT_RULES.md      §7, esp. §7.2 and §7.5 item 4
                                          (:327 "Defeat changes nothing.
                                          A BattleLost grants +0") -
                                          values, to be preserved
docs/03-decisions/ADR/ADR-016-           Consequences (:212-215) - Pet
  independent-player-xp-and-pet-xp-       member list is "a
  tracks.md                               representation decision, not an
                                          open gameplay decision"; does
                                          not address the defeat shape
```

**Task files - historical/contextual evidence ONLY (read-only; do not
modify completed or blocked tasks):**

```text
tasks/blocked/TASK-067-implement-pet-xp-progression.md
                                        §12.1 STOP CONDITION REPORT
                                        (:815-1006) - this task's direct
                                        input; names TASK-068 as the
                                        recommended follow-up (:997-1001)
tasks/backlog/TASK-066-...-reaudit.md    F11 (:942-973) - the delegated
                                        representation decision is the
                                        PET member list only; the READY
                                        verdict never examined the defeat
                                        shape, so it does not resolve it
tasks/completed/TASK-065-implement-player-xp-progression.md
                                        Player XP implementation (do not
                                        reopen; its code says nothing
                                        about the defeat shape)
tasks/backlog/TASK-059-...-contract.md   §6.4 (:410-441) - RewardSummary
                                        illustrative names, explicitly
                                        "ONLY an example - not the final
                                        set"; NOT authoritative
```

**Governing process rules:** `AGENTS.md` §2 (precedence), §4 (conflict
resolution), §7 (no invented rules), §16 (task discipline), §17
(documentation change rule), §20 (stop conditions);
`.ai/workflow/documentation/documentation-change.md` §1 (smallest
authoritative source, dependent references only if stale) and §2 (no
duplication ever).

---

## 3. Current State — The Contradiction (verified at task creation)

Both sides live in the **same subsection of the same document**, and
neither is marked superseded:

**Side A — `DATABASE.md` §1 item 1 (`:551-571`), the frozen Player-track
member list:**

```text
Player track - decided, and therefore contracted here.
  playerXpGained     (int)  - Player XP granted by this battle:
                              100 on a win, 0 on a loss
  newPlayerXp        (int)  - Player.XP after applying the grant
  playerLeveledUp    (bool) - whether Player.Level changed
  newPlayerLevel     (int)  - Player.Level after applying the grant
These four members are the complete Player-track contract ...
```

A member defined as "0 on a loss" is by construction **present on a
loss** - it has a defined, asserted value there. Item 5's own first
clause reinforces presence for both outcomes: "`RewardSummary` is
present for both `Outcome` values" (`:612-613`).

**Side B — `DATABASE.md` §1 item 5 (`:612-616`) and the §1 entity-block
staging sentence (`:541-543`):**

```text
Outcome semantics are unchanged. RewardSummary is present for both
Outcome values (§1 entity block, API_CONTRACTS.md §4 note 1); a
"defeat" battle carries no line items, which matches a +0 Player XP
grant (COMBAT_RULES.md §7.2) and a +0 Pet XP grant to the active
combat Pet (PET_RULES.md §5.3 item 2).
```

"No line items" on a defeat is satisfiable only by **omitting** the
Player-track (and any Pet-track) members from a defeat row - which
cannot coexist with item 1's per-outcome value table for the same row.

**Delegating sources (neither settles the question):**

- `API_CONTRACTS.md` §4 note 1 (`:547-560`): `rewards` is always
  present for both outcomes; its value is "`BattleResult.RewardSummary`
  exactly as `DATABASE.md` §1 documents it (staging value `{}` with no
  reward line items; a `"defeat"` outcome carries no line items). The
  `RewardSummary` member list is owned by `DATABASE.md` §1 ..." - it
  restates both the staging value and the defeat shape by reference, so
  it inherits the contradiction.
- `API_CONTRACTS.md` §4 note 3 (`:562-568`): the event payload's
  reward summary is documented BattleWon-only while REST `rewards`
  "covers both outcomes"; "neither contradicts the other on the value
  that is in force (`{}`)" - written under staging, so it does not fix
  the landed shape either.
- `GAME_EVENTS.md` §2 BattleWon/BattleLost payload (`:450-455`):
  "reward summary (BattleWon only - this is the event-payload rule; the
  REST response's `rewards` field covers both outcomes ...)";
  item 2 (`:464-473`): "This document does not define its members" -
  explicitly non-defining.
- `DATABASE.md` §1 item 4 (`:604-610`): fixes only the **staging** value
  (`{}`) "until the implementation task lands"; says nothing about the
  landed defeat shape.
- `ADR-016` Consequences (`:212-215`): scopes the delegated
  representation decision to the Pet member list; silent on defeat shape.
- `TASK-066` F11: scopes the delegated representation decision to the
  Pet member list only. The READY verdict did **not** cover this
  conflict, so it cannot be cited as having resolved it.
- `TASK-059` §6.4: illustrative names only, explicitly not contract
  (and a task file could not override a technical document anyway -
  `AGENTS.md` §2).

**Why no precedence tie-break exists:** the conflict is intrinsic to a
single subsection, so `AGENTS.md` §2's "most specific governs" ordering
cannot break the tie - the only document that owns the contract is the
one that contradicts itself, and every other document that touches it
defers back to it. Per `AGENTS.md` §4 / §20 ("Rule conflict", "Data
contract conflict") this must be adjudicated explicitly, never by
preference.

**Not part of the problem:** the frozen value contracts are complete and
unambiguous - Player XP `+100`/`+0` (`COMBAT_RULES.md` §7), Pet XP
`+100`/`+0` + active-Pet-only recipient + inactive `+0` + 4900 hard cap
(`PET_RULES.md` §5.1-§5.5), the Pet column/constraints
(`DATABASE.md` §3), and the Player-track values in item 1. Only the
**defeat-row member presence/arity** is at issue.

---

## 4. Required Decision — Option A / Option B

The task must resolve **only** the defeat-shape semantics. Both readings
are to be documented as candidates; exactly one ends up authoritative.

### Option A — Same `RewardSummary` shape for both outcomes

Both victory and defeat contain the Player-track members:

```text
playerXpGained
newPlayerXp
playerLeveledUp
newPlayerLevel
```

with:

```text
BattleWon  → playerXpGained = 100
BattleLost → playerXpGained = 0
```

The remaining values reflect the resulting Player XP/Level state
according to the existing contract.

If Option A is the resolved decision, the authoritative wording must
explicitly state that the `RewardSummary` member set applies to **both**
victory and defeat, define `playerXpGained = 0` on defeat, and preserve
the existing Player XP contract.

### Option B — Empty defeat `RewardSummary`

Victory contains the documented Player-track reward members. Defeat
contains no reward line items:

```json
{}
```

The `playerXpGained = 0` wording must then be clarified so it does not
imply that those members exist on defeat.

If Option B is the resolved decision, the authoritative wording must
explicitly state that defeat has an empty `RewardSummary` / no line
items, clarify that `playerXpGained = 0` describes the reward amount
conceptually but does not imply a serialized member on defeat, and
preserve the existing Player XP contract.

---

## 5. Decision Process & Critical Rules

### 5.1 The decision is a game/API contract decision

Treat it as a **game/API contract decision**, not an implementation
detail. Do not infer a gameplay decision from code or convenience.

### 5.2 Evidence-first adjudication

1. Survey **all** of §2's documents and task evidence before editing
   anything.
2. Ask: does existing authoritative documentation **elsewhere** (i.e.
   outside the self-contradictory §1 item pair) clearly resolve the
   contradiction? Candidate evidence to weigh explicitly - neutrally:
   - `DATABASE.md` §1 item 5 and `:541-543` (defeat = no line items);
   - `DATABASE.md` §1 item 1 (per-outcome `playerXpGained` values);
   - `DATABASE.md` §1 item 5's first clause ("present for both
     `Outcome` values");
   - `API_CONTRACTS.md` §4 note 3 and `GAME_EVENTS.md:21` ("BattleWon-
     only is the event-payload rule; the REST `rewards` field covers
     both outcomes") - analyze whether "covers both outcomes" speaks to
     the **field's presence** (uncontested) or to **member presence on
     defeat** (the contested question);
   - `GAME_EVENTS.md:450-455` (event payload carries the reward summary
     on BattleWon only - note this is an event-payload rule and must not
     be silently conflated with the persisted/REST shape);
   - `COMBAT_RULES.md` §7.2 / §7.5 item 4 and `PET_RULES.md` §5.3
     (both fix **values** `+0`, not member presence);
   - the `{}` staging history (item 4) - see §5.3.
3. **If** an authoritative reading is decisive - one side provably
   misworded or superseded by a documented decision - apply the minimal
   documentation edit that records the resolution, with a full citation
   chain in this task's evidence, per `AGENTS.md` §17 (decide which
   source was correct, do not just make both match).
4. **If** the authoritative documentation does NOT determine the
   intended option - i.e. evidence supports both options, or the answer
   is genuinely a product choice - then **HUMAN DECISION REQUIRED**: the
   task must explicitly require a human gameplay/product decision
   between Option A and Option B. Record the ambiguity and identify the
   smallest human decision needed ("Choose Option A or Option B for
   defeat `RewardSummary` member presence"). **Do not implement a
   choice.** Do not edit any document to prefer a side. Transition this
   task to BLOCKED per `TASK_LIFECYCLE.md` §3 (STOP CONDITION report in
   §11 of this file) and await the human answer; once given, resume and
   apply only that answer's minimal wording from §8.

### 5.3 Critical rules - do NOT choose based on

Do **not** choose an option based on:

- implementation convenience;
- TASK-065 implementation;
- TASK-067 implementation plan (including whether §5.5's sequencing
  wrinkle "disappears" under one option);
- existing `{}` staging behavior (staging is explicitly interim - item 4
  - and cannot vote on the landed shape);
- what is easiest for API consumers;
- what seems cleaner technically.

Do **not** infer a gameplay decision. Do **not** silently select one.
If STOP is required, record the ambiguity and identify the smallest
human decision needed - never implement a choice.

---

## 6. Scope

**Inspection scope:** exactly §2's list (six documents, four task files).

**Edit scope:** only the minimum authoritative documentation required to
resolve the contradiction. In practice:

- `docs/02-technical/DATABASE.md` §1 (item 1 and/or item 5, and the
  `:541-543` entity-block sentence **only if** it also contradicts the
  resolved shape) - the canonical owner is edited first, per
  `documentation-change.md` §1;
- `docs/02-technical/API_CONTRACTS.md` §4 - **only** if note 1 / note 3
  remain contradictory after the owner is resolved;
- `docs/02-technical/GAME_EVENTS.md` §2 - **only** if item 2 or the
  payload block remains contradictory after the owner is resolved.

Update cross-references/delegations only where necessary to make the
resolved contract explicit. Do not rewrite unrelated sections. Do not
duplicate the resolved rule into referencing documents
(`documentation-change.md` §2).

---

## 7. Out of Scope — Do NOT

```text
implement Pet XP
implement Player XP
modify source code
modify tests
create migrations
modify Redis
modify SignalR
modify BattleState
modify API implementation
create new RewardSummary fields
change Player XP values
change Pet XP values
change Pet XP cap / formula
reopen TASK-065
reopen TASK-066
modify TASK-067
create TASK-069
perform a broad documentation audit
change unrelated reward semantics
```

---

## 8. Documentation Requirements

After the decision, the authoritative wording must be internally
consistent. Verify that `DATABASE.md` §1, `API_CONTRACTS.md` §4, and
`GAME_EVENTS.md` §2 do not continue to express contradictory defeat
semantics.

**If Option A:**

- explicitly state that the `RewardSummary` member set applies to both
  victory and defeat;
- define `playerXpGained = 0` on defeat;
- preserve the existing Player XP contract.

**If Option B:**

- explicitly state that defeat has an empty `RewardSummary` / no line
  items;
- clarify that `playerXpGained = 0` describes the reward amount
  conceptually but does not imply a serialized member on defeat;
- preserve the existing Player XP contract.

**Both options:**

- no value changes anywhere: Player XP `+100`/`+0`, Pet XP `+100`/`+0`,
  Pet XP cap/formula, `{}` staging semantics unchanged;
- the `{}` staging meaning (item 4: in force "until the implementation
  task lands") is changed only where required to remove the
  contradiction - the staging concept itself is not retired or redefined
  by this task;
- each edited document's version/changelog header is updated per its own
  existing convention (`DATABASE.md` `:3`, `API_CONTRACTS.md` `:3`,
  `GAME_EVENTS.md` `:3`);
- no new `RewardSummary` fields, no Pet member list decisions (that
  remains TASK-067's delegated decision under `DATABASE.md` §1 item 2),
  no second source of truth for the shape.

---

## 9. Acceptance Criteria

All criteria are binary and testable.

- [ ] 1. The contradiction between `DATABASE` §1 item 1 and item 5 is
      explicitly resolved.
- [ ] 2. Exactly one defeat-shape semantics is authoritative.
- [ ] 3. The Player XP reward values remain unchanged: `+100` win /
      `+0` loss.
- [ ] 4. No Pet XP semantics are changed.
- [ ] 5. `RewardSummary` member presence on defeat is unambiguous.
- [ ] 6. `DATABASE.md` is internally consistent.
- [ ] 7. `API_CONTRACTS.md` does not contradict the resolved shape.
- [ ] 8. `GAME_EVENTS.md` does not contradict the resolved shape.
- [ ] 9. No source code changed.
- [ ] 10. No tests changed.
- [ ] 11. TASK-067 remains BLOCKED and unmodified.
- [ ] 12. No new implementation task is created by this task.
- [ ] 13. No unrelated documentation is changed.
- [ ] 14. Every document modified by this task has its version/changelog
      header updated per that document's existing convention.
- [ ] 15. The Final Report is delivered in exactly the §13 format.

Criteria 1-2, 5-8 are satisfied **either** by a cited resolution **or**
by a completed HUMAN DECISION REQUIRED report (§5.2 step 4) in which
case criteria 1/2/5 record "pending human decision, ambiguity logged"
and the task is BLOCKED, not DONE - a BLOCKED completion still satisfies
criteria 9-13, 14-15.

---

## 10. Testing Requirements / Verification

Documentation-only task: no game-behavior tests apply. Verification
instead:

```text
1. Consistency re-read: DATABASE.md §1 + API_CONTRACTS.md §4 +
   GAME_EVENTS.md §2 read together; exactly one defeat-shape
   statement survives; grep for "carries no line items" /
   "0 on a loss" / "covers both outcomes" and confirm every surviving
   occurrence agrees with the resolved shape.
2. Value preservation check: grep COMBAT_RULES.md §7 and
   PET_RULES.md §5 unchanged (git diff shows no edit).
3. No-code/no-test proof: git status / git diff --stat shows ONLY the
   authorized docs (§6) and this task file's status/evidence fields.
   No src/, no tests/, no migrations/, no config changes.
4. TASK-067 untouched: file still exists at
   tasks/blocked/TASK-067-implement-pet-xp-progression.md with
   Status: BLOCKED, byte-identical except for nothing - zero edits.
5. No TASK-069 file exists anywhere under tasks/.
```

Do **not** run builds/tests to "prove" a docs change; do **not** modify
tests to match wording.

---

## 11. Stop Conditions

STOP instead of guessing if:

1. authoritative documents provide evidence for both options without a
   decisive resolution;
2. the intended outcome requires a new gameplay/product decision;
3. resolving the contradiction would require changing Player XP values;
4. resolving it would require changing Pet XP semantics;
5. resolving it requires an architectural decision (`AGENTS.md` §18);
6. multiple `RewardSummary` shapes are intentionally required but the
   conditions are undocumented;
7. another contradiction is discovered that materially affects the
   decision.

Universal stop conditions (`AGENTS.md` §20 - rule conflict, missing rule,
architecture conflict, scope violation, ambiguous requirement, data
contract conflict, destructive change) always apply. The
`documentation-change.md` §1 "Check for conflicts" rule also applies: if
the requested resolution itself would contradict a third document, STOP.

**If STOP is required:** record the ambiguity and identify the smallest
human decision needed (§5.2 step 4). Do not implement a choice. Write
the STOP CONDITION report into this section (per `TASK_LIFECYCLE.md` §3),
transition this task BACKLOG/READY → BLOCKED (file moves
`tasks/backlog/` → `tasks/blocked/`), and stop.

**Conditions 1-2 are the expected common path:** if the evidence survey
finds no decisive authoritative resolution, this task's correct outcome
is HUMAN DECISION REQUIRED - a completed ambiguity report, not a guess.

---

## 11. STOP CONDITION REPORT (HUMAN DECISION REQUIRED)

```text
STOP CONDITION FIRED — §4 Rule Conflict / §20 Data Contract Conflict

Problem:
    The evidence survey (§5.2) finds NO authoritative source outside
    DATABASE.md §1's self-contradictory item pair that resolves whether
    a defeat RewardSummary carries Player-track line items or is empty.
    Both Option A and Option B have supporting evidence within the same
    subsection, and neither side is provably misworded or superseded by
    an external documented decision.

    Side A support (defeat carries all four Player-track members):
      - DATABASE.md §1 item 1 (:562-571): playerXpGained defined with
        per-outcome values "100 on a win, 0 on a loss". Defining a loss
        value implies presence on loss.
      - DATABASE.md §1 item 5 (:612-613): first clause states
        "RewardSummary is present for both Outcome values" — the container
        exists on both outcomes.
      - COMBAT_RULES.md §7.5 item 4: "A BattleLost grants +0 Player XP"
        — conceptual reward of 0 on loss.

    Side B support (defeat carries no line items / {}):
      - DATABASE.md §1 item 3 (:541-543): staging sentence says the empty
        JSON object {} is "always present, never absent, for both Outcomes;
        a 'defeat' battle carries no line items."
      - DATABASE.md §1 item 5 (:614-616): "a 'defeat' battle carries no
        line items, which matches a +0 Player XP grant."
      - API_CONTRACTS.md §4 note 1 (:549-552): "staging value {} with no
        reward line items; a 'defeat' outcome carries no line items."
      - GAME_EVENTS.md §2 payload block (:450-455): "reward summary
        (BattleWon only" — event payload rule.

    Why neither side wins:
      - Item 1's per-outcome values could describe what the values WOULD BE
        if present (victory) while acknowledging the conceptual amount on
        loss is 0 — OR they could be definitive member definitions that
        apply to both outcomes.
      - Item 5's "carries no line items" could be literal serialized-shape
        ({} on defeat) — OR informal wording about absence of positive
        rewards.
      - Item 4 ({}) is explicitly STAGING, "in force until the
        implementation task lands." Staging cannot vote on landed shape.
      - COMBAT_RULES.md §7.2/PET_RULES.md §5.3 fix VALUES (+0), not
        serialization shapes. They are silent on member presence.
      - ADR-016 Consequences (:212-215): silent on defeat shape.
      - API_CONTRACTS.md §4 note 3 and GAME_EVENTS.md §2 item 2 both
        delegate the member list back to DATABASE.md §1. Neither breaks
        the tie.
      - TASK-066 F11 scoped its delegated representation decision to the
        Pet member list only; it did NOT examine the defeat shape.

    No game-rule document, ADR, or third technical document arbitrates
    between these two readings. The conflict is intrinsic to a single
    subsection, so AGENTS.md §2 precedence ordering cannot break the tie.

Authoritative sources inspected (and what each establishes):
    CONTRADICTION (both inside DATABASE.md §1):
      - Item 1 (:551-571): Per-outcome member definitions with explicit
        "0 on a loss" → supports Side A (members present on loss).
      - Item 3 (:541-543): Staging sentence — "{} always present ...
        defeat carries no line items" → supports Side B (no members on
        defeat). Explicitly interim (item 4).
      - Item 5 (:612-616): "present for both Outcomes; defeat carries no
        line items" → supports Side B (no members on defeat).
    DELEGATING (hands back to DATABASE.md §1):
      - API_CONTRACTS.md §4 note 1 (:547-560): Inherits contradiction.
      - API_CONTRACTS.md §4 note 3 (:562-568): Event-payload rule; REST
        covers both outcomes. Silent on member presence.
      - GAME_EVENTS.md §2 item 2 (:464-473): Explicitly non-defining.
      - GAME_EVENTS.md §2 payload (:450-455): BattleWon-only event rule.
    SILENT ON DEFEAT SHAPE:
      - COMBAT_RULES.md §7.2/§7.5: Values fixed (+0), not shapes.
      - PET_RULES.md §5.3: Values fixed (+0), not shapes.
      - ADR-016 Consequences (:212-215): Pet member list is a
        representation decision; silent on defeat shape.
    NOT AUTHORITATIVE:
      - TASK-066 F11: Scoped to Pet member list only.
      - TASK-059 §6.4: Illustrative names only.

Smallest human decision required:
    Choose ONE of the following options for the defeated `RewardSummary`:

    OPTION A: Same member set for both outcomes.
      Victory:   {playerXpGained: 100, newPlayerXp: ..., playerLeveledUp: ..., newPlayerLevel: ...}
      Defeat:    {playerXpGained: 0, newPlayerXp: <same>, playerLeveledUp: false, newPlayerLevel: <same>}
      Rationale: Item 1 defines per-outcome values; loss has a defined value.

    OPTION B: Empty RewardSummary on defeat.
      Victory:   {playerXpGained: 100, newPlayerXp: ..., playerLeveledUp: ..., newPlayerLevel: ...}
      Defeat:    {}
      Rationale: Items 3 and 5 state "carries no line items" on defeat.

    This is a product/gameplay contract decision. Do not infer from code,
    implementation convenience, or API cleanliness.
```

---

## 12. Status Transition & Next Step

**On a successful resolution (criteria 1-2, 5-8 satisfied by cited
evidence):**

```text
This task:      BACKLOG/READY -> DONE (tasks/backlog -> tasks/completed)
TASK-067:       resumes BLOCKED -> IN PROGRESS (by a SEPARATE later
                action, NOT by this task) with §5.2 step 12 of
                TASK-067 unambiguous
Next task:      TASK-067 — resume Pet XP implementation
```

**If HUMAN DECISION REQUIRED:**

```text
This task:      BLOCKED (tasks/backlog -> tasks/blocked) with the
                ambiguity report + the exact question
                ("Option A or Option B?") in §11
Docs:           UNCHANGED - no option implemented
TASK-067:       remains BLOCKED
Human:          answers A or B -> this task resumes and applies only
                that answer's minimal §8 wording
```

This task does NOT create TASK-069, does NOT resume TASK-067, and does
NOT implement anything. The expected next step after a successful
resolution is **TASK-067 — resume Pet XP implementation**, generated
later by a separate action (per `TASK-066`'s rule that a verdict/task
authorizes generation but never performs it).

---

## 13. Completion Evidence — Final Report

Report in exactly this format (binary items report pass/fail):

```text
1. Task ID and filename
2. Status
3. Exact contradiction
4. Evidence inspected
5. Decision: Option A / Option B / HUMAN DECISION REQUIRED
6. Exact documentation changes
7. Files modified
8. Acceptance criteria results
9. Confirmation that TASK-067 remains untouched and BLOCKED
10. Confirmation that no source/tests/migrations/Redis/SignalR changes
    were made
11. Next step
```

The Final Report is written here, in this section, on completion (or in
the STOP CONDITION report if BLOCKED), per `TASK_LIFECYCLE.md` §3.

---

## 14. FINAL REPORT (RESOLVED — OPTION A)

### 1. Status

```text
DONE
```

The human selected **Option A** after this task's evidence survey returned
HUMAN DECISION REQUIRED (recorded in §11). The resolution was then applied to
the canonical owner document and synchronized into its two dependent
references.

### 2. Contradiction (as reported)

Two mutually exclusive defeat-shape statements inside
`docs/02-technical/DATABASE.md` §1 "Reward semantics for `RewardSummary`":

- **Side A** (item 1, `:562-571`): The Player-track member list defines
  `playerXpGained` with per-outcome values "100 on a win, 0 on a loss."
  Defining a specific value for a loss implies the member IS present on a loss.
- **Side B** (item 5, `:614-616`; item 3, `:541-543`): a `"defeat"` battle
  "carries no line items" — meaning zero members in the `RewardSummary` JSON
  on defeat.

These cannot both be true of the same artifact: a JSON object either contains
the four named members or it does not.

### 3. Evidence Inspected

| Source | What it establishes |
|--------|-------------------|
| `DATABASE.md` §1 item 1 (`:551-571`) | Per-outcome member definitions with explicit "0 on a loss" â†’ supports Side A |
| `DATABASE.md` §1 item 3 (`:541-543`) | Staging sentence: "`{}` always present; defeat carries no line items" â†’ supported Side B (interim only) |
| `DATABASE.md` §1 item 5 (`:612-616`) | "present for both Outcomes; defeat carries no line items" â†’ supported Side B |
| `DATABASE.md` §1 item 4 (`:604-610`) | `{}` staging "in force until implementation task lands" â†’ cannot vote on the landed shape |
| `API_CONTRACTS.md` §4 note 1 (`:547-560`) | Delegated to `DATABASE.md` §1; inherited the contradiction |
| `API_CONTRACTS.md` §4 note 3 (`:562-568`) | Event-payload vs REST distinction; silent on member presence |
| `GAME_EVENTS.md` §2 payload (`:450-455`) | BattleWon-only event rule; REST covers both outcomes |
| `GAME_EVENTS.md` §2 item 2 (`:464-473`) | Explicitly non-defining ("does not define its members") |
| `COMBAT_RULES.md` §7.2 / §7.5 | Values fixed (`+0` on loss), NOT serialization shapes |
| `PET_RULES.md` §5.3 | Values fixed (`+0` on loss), NOT serialization shapes |
| `ADR-016` Consequences (`:212-215`) | Pet member list = representation decision; silent on defeat shape |
| `TASK-066` F11 (`:942-973`) | Scoped to the Pet member list only; did NOT examine the defeat shape |
| `TASK-059` §6.4 (`:410-441`) | Illustrative names only; not contract |

No authoritative source outside the self-contradictory pair resolved the
question, which is why §11 recorded HUMAN DECISION REQUIRED rather than
picking a side.

### 4. Decision

```text
OPTION A
```

**Selected by the human.** The `RewardSummary` Player-track member set applies
to **both** victory and defeat, with `playerXpGained = 100` on `BattleWon` and
`playerXpGained = 0` on `BattleLost`.

### 5. Documentation Changes

**`docs/02-technical/DATABASE.md`** — canonical owner, edited first per
`documentation-change.md` §1; version **1.16 â†’ 1.17**:

1. §1 "Reward semantics for `RewardSummary`" **item 5** rewritten. The
   contradictory clause ("a `"defeat"` battle carries no line items") is
   removed and replaced with an explicit statement that the Player-track member
   set defined in item 1 applies identically to both outcomes:
   `BattleWon â†’ playerXpGained = 100`, `BattleLost â†’ playerXpGained = 0`; on a
   `"defeat"` the four members are serialized with `playerXpGained = 0`,
   `playerLeveledUp = false`, and `newPlayerXp` / `newPlayerLevel` unchanged.
2. §1 **item 3** (the entity-block staging sentence): the defeat-specific
   "carries no line items" assertion is removed and reworded to "the `{}` value
   contains no reward data while the implementation task is pending" —
   preserving the staging concept (item 4) without implying a permanent
   defeat-specific empty shape.
3. Version header updated 1.16 â†’ 1.17, with the previous entry demoted to a
   "Prior 1.16:" changelog line per this document's existing convention. Every
   historical entry is preserved verbatim.
4. **Item 1 is unchanged** — the Player-track member list and its per-outcome
   values are the authoritative source the resolution now rests on.

**`docs/02-technical/API_CONTRACTS.md`** — dependent reference; version
**1.11 â†’ 1.12**:

1. §4 **note 1**: the parenthetical that restated the staging value together
   with "a `"defeat"` outcome carries no line items" is replaced with the
   resolved semantics — staging `{}` until the implementation task lands; once
   it lands, the Player-track member set is serialized for **both** outcomes,
   with `playerXpGained = 100` on `"victory"` and `playerXpGained = 0` on
   `"defeat"`.
2. §4 **note 3**: the closing clause that named `{}` as "the value that is in
   force" now names both the staging value and the landed shape, so it no
   longer implies `{}` is the permanent contract.
3. Version header updated 1.11 â†’ 1.12.

**`docs/02-technical/GAME_EVENTS.md`** — dependent reference; version
**2.6 â†’ 2.7**:

1. §2 `BattleWon`/`BattleLost` **payload block**: the delegating pointer said
   the contract included "which members are decided and which are still blocked
   on unresolved Pet XP decisions." Those Pet XP decisions were finalized by
   TASK-062 — this same document's item 2 says so — so that wording was stale,
   and it sat directly on the delegation for the contract being resolved. It
   now states that the Player-track member set applies to both outcomes and
   that only the Pet-track member list remains deferred.
2. Version header updated 2.6 â†’ 2.7.
3. **Item 1 (Outcome value set) and item 2 (non-defining delegation) are
   unchanged** — item 2 already stated this document "does not define its
   members and does not contradict the REST response," which remains true under
   Option A.

**Not modified** (verified): `PET_RULES.md`, `COMBAT_RULES.md`, `ADR-016`,
`GAME_STATE.md`, `REDIS_STATE.md`, `SIGNALR_PROTOCOL.md`, all source code, all
tests, `TASK-067`, and every other task file. No ADR amendment was required:
the resolution is a clarification inside the document that already owns the
contract (`DATABASE.md` §1), not an architectural decision, so `AGENTS.md` §18
was not triggered.

### 6. Contract After Resolution

```text
Victory RewardSummary:
    { playerXpGained: 100,
      newPlayerXp:     <Player.XP after applying the grant>,
      playerLeveledUp: <whether Player.Level changed>,
      newPlayerLevel:  <Player.Level after applying the grant> }

Defeat RewardSummary:
    { playerXpGained: 0,
      newPlayerXp:     <current Player.XP — unchanged>,
      playerLeveledUp: false,
      newPlayerLevel:  <current Player.Level — unchanged> }
        -- the SAME four-member set as victory; NOT {}

playerXpGained on defeat:
    Serialized, value 0. The member is PRESENT on defeat.
```

Interim state unchanged: until the implementation task lands, the
persisted/returned value remains the `{}` staging value for both outcomes
(`DATABASE.md` §1 item 4). The Pet-track member list remains deferred to the
implementation task (`DATABASE.md` §1 item 2) — that delegation is untouched by
TASK-068.

### 7. Validation

Searches and checks used to prove no contradiction remains:

```text
1. grep "carries no line items" over docs/
   -> exactly 1 hit, in DATABASE.md :6 ONLY (the version changelog quoting the
      REMOVED wording). No normative occurrence survives.

2. grep "no line items" over docs/
   -> 2 hits, both in version headers (DATABASE.md :6, API_CONTRACTS.md :6)
      describing what was removed. No normative occurrence survives.

3. grep "playerXpGained" over docs/
   -> DATABASE.md :8, :572, :627, :628, :632 (the resolved contract) and
      API_CONTRACTS.md :6, :557 (the delegating pointer mirroring it).
      Every occurrence agrees: 100 on victory, 0 on defeat, member present on
      both outcomes.

4. grep "0 on a loss" over docs/
   -> DATABASE.md :573 ONLY — item 1's authoritative member definition.
      No competing statement exists anywhere.

5. Read-together check of DATABASE.md §1 items 1/3/4/5 + API_CONTRACTS.md §4
   notes 1/3 + GAME_EVENTS.md §2 payload and item 2:
      item 1 defines the member set with per-outcome values;
      item 5 now states that set applies to both outcomes;
      item 3 no longer asserts a defeat-specific empty shape;
      item 4 keeps {} as interim staging only;
      API note 1 mirrors the resolution; API note 3 no longer implies {} is
        permanent;
      GAME_EVENTS item 2 stays explicitly non-defining.
   => Exactly ONE defeat representation is authoritative.

6. Value-preservation check (no value changed):
   COMBAT_RULES.md -> "BattleWon -> Player XP +100", "BattleLost -> Player XP
      +0", and "Player.XP is uncapped" all present and unedited.
   PET_RULES.md -> "+100 Pet XP", "+0 Pet XP", "hard maximum of 4900", and
      "min(floor(Pet.XP / 100) + 1, 50)" all present and unedited.
   git diff -> zero changes to PET_RULES.md, COMBAT_RULES.md, and ADR-016.

7. Scope proof:
   git diff --stat limited to the three authorized documents;
   git diff --stat over tasks/blocked/TASK-067-implement-pet-xp-progression.md
      -> EMPTY (byte-identical).
   No src/, tests/, migrations/, or config change introduced by this task.

8. No TASK-069 exists anywhere under tasks/ (glob "**/TASK-069*" -> no files).
```

No build or test execution was performed; none is required for a
documentation-only resolution (TASK-068 §10).

### 8. Acceptance Criteria Results

```text
[x]  1. DATABASE §1 item 1 / item 5 contradiction explicitly resolved    PASS
[x]  2. Exactly one defeat-shape semantics is authoritative              PASS (Option A)
[x]  3. Player XP reward values unchanged: +100 win / +0 loss            PASS
[x]  4. No Pet XP semantics changed                                      PASS
[x]  5. RewardSummary member presence on defeat unambiguous              PASS
[x]  6. DATABASE.md is internally consistent                             PASS
[x]  7. API_CONTRACTS.md does not contradict the resolved shape          PASS
[x]  8. GAME_EVENTS.md does not contradict the resolved shape            PASS
[x]  9. No source code changed                                           PASS
[x] 10. No tests changed                                                 PASS
[x] 11. TASK-067 remains BLOCKED and unmodified                          PASS
[x] 12. No new implementation task created by this task                  PASS
[x] 13. No unrelated documentation changed                               PASS
[x] 14. Version/changelog header updated on every modified document      PASS
        (DATABASE.md 1.16->1.17, API_CONTRACTS.md 1.11->1.12,
         GAME_EVENTS.md 2.6->2.7)
[x] 15. Final Report delivered in the §13 format                         PASS
```

### 9. TASK-067

Confirmed untouched and still BLOCKED. `git diff --stat` over
`tasks/blocked/TASK-067-implement-pet-xp-progression.md` is EMPTY; the file
remains at `tasks/blocked/TASK-067-implement-pet-xp-progression.md` with
`Status: BLOCKED`. It was not moved, not edited, and not resumed.

### 10. Scope Confirmation

```text
No source changes.           CONFIRMED
No test changes.             CONFIRMED
No Pet XP implementation.    CONFIRMED
No Player XP implementation. CONFIRMED
No Redis.                    CONFIRMED
No SignalR.                  CONFIRMED
No frontend.                 CONFIRMED
No gameplay changes.         CONFIRMED
No migrations.               CONFIRMED
No TASK-069 created.         CONFIRMED
```

### 11. Next Step

```text
Resume TASK-067 — Implement Pet XP Progression.
```

The defeat-shape question that blocked TASK-067 §5.6 / §5.5 is now
deterministic, so TASK-067 §5.2 step 12 can be executed without guessing.
TASK-067 must be resumed by a SEPARATE later action — this task does not resume
it, does not modify it, and does not create any follow-up task.