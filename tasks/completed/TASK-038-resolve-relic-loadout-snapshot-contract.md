# TASK-038 — Resolve Relic Loadout Snapshot Contract

---

## Metadata

```text
Task ID:           TASK-038
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, persistence, backend
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, quality/documentation-consistency,
                   discovery/impact-analysis, gameplay/gameplay-behavior-derivation,
                   quality/scope-validation
Dependencies:      None (blocks TASK-027; TASK-027 must not start until this is DONE)
```

---

## Objective

Resolve the three documentation-level ambiguities that currently prevent
TASK-027 from becoming READY — (A) the element representation of
`PetState.EquippedRelics[]`, (B) the source of equip slot index `1–5`, and
(C) the duplicate-Relic selection policy — so that a future implementation task
can build the Relic ownership/battle-start snapshot deterministically without
guessing gameplay rules. This task is **documentation-only**: no code, no
migrations, no entities, no endpoint, and no Relic trigger/stacking logic.

---

## Why This Task Exists

TASK-027 (Relic Ownership & Battle-Start Snapshot) is **BLOCKED** as a READY
candidate. Its audit found three gameplay-contract gaps. The authoritative
documents constrain the *shape* of the Relic loadout but not the *semantics*
of three specific questions, and implementation cannot proceed without them:

```text
A. EquippedRelics element type     — what does one array element contain?
B. Equip slot index source         — how is the `1 → 5` ordering key assigned?
C. Duplicate selection policy      — may one Relic instance occupy two slots?
```

Each is a genuine `AGENTS.md` §20 "Ambiguous requirement" / "Missing rule"
condition, not a §4 documentation *conflict*: every document that speaks to the
loadout agrees on what it says; the gaps are unstated rules, not contradictions.

**This task resolves the gaps; it does not resolve them by choosing a
convenient answer.** Where the evidence does not determine an answer, the task
STOPs and records a required human gameplay decision.

---

## Exact Ambiguities

### Blocker A — `PetState.EquippedRelics[]` element type is undefined

`docs/02-technical/GAME_STATE.md` §2.3 lists the member:

```text
├── EquippedRelics[]    (3–5, slot order fixed at battle start,
│                        RELIC_RULES.md §4 — not yet implemented)
```

but no authoritative document states what **one element** of that array
contains. Candidate representations, all currently consistent with the literal
text:

```text
A1. RelicInstanceId                (owned instance identity — DATABASE.md §1 `Relic.RelicInstanceId`)
A2. RelicDefinitionId              (static content identity — DATABASE.md §1 `RelicDefinition.RelicDefinitionId`)
A3. resolved Relic definition data (Name/Trigger/Condition/EffectDefinition inlined)
A4. a wrapper record               (instance id + definition id + slot index, or similar)
A5. another representation         (undocumented)
```

Evidence currently **points toward A1** (instance identity):

```text
- RELIC_RULES.md §2.1        ownership is instance-based ("the Player owns Relic *instances*")
- API_CONTRACTS.md §3        validation "checks Player ownership of the selected instances"
- RELIC_RULES.md §2.3        the array "is the only equip representation inside active battle state"
- DATABASE.md §2             no persistent equip table; ownership is `Player 1─N Relic`
- RELIC_RULES.md §4.1        ordering is by "equip slot index", an index into an array of 3–5
```

Evidence that must **also** be weighed and is currently silent or ambiguous:

```text
- GAME_STATE.md §2.3         does not define the element shape at all (and §0 item 5 forbids a second representation)
- GAME_STATE.md §2.1.7 item 4 / §2.1.9 item 3   precedent for how the board serializes entries — analogy only, not authority
- DATABASE.md §1             `Relic` is conditional: "a player's OWNED instance, IF Relics have per-instance state; otherwise ownership is a join table"
- RELIC_RULES.md §1–§6       no MVP Relic in the §6 reference list has per-instance state (duration, stacks, charges)
```

**Do not assume A1 from the weight of the list above.** Determine the
authoritative representation from the documents, per the Decision Required
section. If the documents do not conclusively determine it, mark it as a
required human gameplay decision.

### Blocker B — equip slot index source is undefined

`docs/01-game-design/RELIC_RULES.md` §4 requires deterministic ordering:

```text
1. Order equipped Relics by their equip slot index (1 → 5), fixed at
   battle start.
```

No authoritative document defines **how the slot index is assigned**. Candidate
interpretations:

```text
B1. request `relicLoadout` array position   (API_CONTRACTS.md §3 request order)
B2. acquisition order                        (DATABASE.md §1 `Relic.AcquiredAt`)
B3. RelicInstanceId ordering                  (DATABASE.md §1 `Relic.RelicInstanceId`)
B4. RelicDefinitionId ordering                (DATABASE.md §1 `RelicDefinition.RelicDefinitionId`)
B5. another documented rule                   (undocumented)
```

What the documents do and do not say:

```text
RELIC_RULES.md §4      ordering is by "equip slot index (1 → 5)"; says the order is
                       "fixed at battle start" and "cannot change mid-battle" —
                       it never says what assigns the index
RELIC_RULES.md §4.2    the trigger order is the slot order — so B decides gameplay
                       resolution order, not just storage layout
RELIC_RULES.md §2.2    the loadout is selected at POST /api/battle/start — the request
                       is the only documented point at which a selection order exists
API_CONTRACTS.md §3    `relicLoadout` is documented as an array; no statement that its
                       position carries gameplay meaning, and no statement that it does not
GAME_STATE.md §2.3     "slot order fixed at battle start" — again unnamed
GAME_STATE.md §2.1.10  precedent: an *unordered* pair is stored canonically so "a request's
                       argument order can never make the stored value ambiguous" — tells us
                       the docs care about canonicalizing request order, but it is about
                       Swap pairs, not Relic slots, and is analogy only
GDD.md §2 / §10        "Equip Relics" step; per-Pet loadout selected by the Player —
                       no ordering statement
MVP_SCOPE.md §1        "3–5 equipped Relics per battle" — no ordering statement
```

B is **gameplay-visible**: §4.2 makes it the trigger resolution order, so this
is not an implementation convenience choice.

**Do not select an interpretation for implementation convenience.** If the
authoritative documentation clearly implies one interpretation, record that
interpretation explicitly; if not, STOP and mark it as a required human
gameplay decision.

### Blocker C — duplicate selection policy is undefined

`docs/02-technical/API_CONTRACTS.md` §3 says:

```text
relicLoadout must be 3–5 Relics owned by the player   — RELIC_RULES.md §2
```

and `RELIC_RULES.md` §2.1 says the active Pet "carries 3–5 of those owned
Relics for one battle". **Neither defines:**

```text
C1. whether the same RelicInstanceId may appear more than once in relicLoadout
C2. whether duplicate instance selection is rejected (and with what error code)
C3. whether multiple instances of the same RelicDefinition are allowed
    (distinct RelicInstanceIds, same RelicDefinitionId)
C4. whether each equipped slot must contain a distinct Relic instance
```

What the documents do and do not say:

```text
RELIC_RULES.md §2.1    "carries 3–5 of those owned Relics" — a *count* bound only;
                       silent on distinctness
RELIC_RULES.md §4      "Order equipped Relics by their equip slot index (1 → 5)" —
                       presupposes 3–5 array positions; silent on whether two
                       positions may name the same instance
RELIC_RULES.md §5      anti-infinite-chain rule operates *per Relic* ("the same
                       Relic does not re-trigger again within the same root event") —
                       it does not define what identifies "the same Relic"
                       (instance? definition?), and its §6 note permits a Relic to fire
                       once per Match in a Cascade
RELIC_RULES.md §6      MVP Relic reference list has 5 Relics, and none is documented as
                       stackable or duplicable
API_CONTRACTS.md §3    count + ownership only; no distinctness statement
API_CONTRACTS.md §6    error envelope exists; no documented code for a duplicate
                       selection (only `INVALID_LOADOUT` is named in §3 as the
                       loadout-related response, and §6 does not enumerate codes)
DATABASE.md §1–§3      no constraint of any kind on Relic selection or distinctness
GAME_STATE.md §2.3     `EquippedRelics[]` — an array, so it can express a duplicate
```

C affects, at minimum: request validation, slot assignment, snapshot contents,
deterministic trigger order (`RELIC_RULES.md` §4.2), and any future
trigger/stacking behavior (`RELIC_RULES.md` §5).

**Do not infer the answer from implementation convenience.** If existing
authoritative rules already resolve it, record that; if not, STOP and mark it as
a required human gameplay decision.

---

## Authoritative Evidence

Read before deciding; cite by path + section in the final report. Do not copy
their contents into this task file, and do not restate their rules there.

```text
AGENTS.md §2 (source-of-truth order), §4 (conflict resolution), §7 (no invented
  rules), §8 (MVP protection), §11 (determinism), §17 (documentation change rule)
.ai/README.md §6, §13, §18
.ai/workflow/documentation/documentation-change.md §1–§3
.ai/workflow/core/context-discovery.md §3
.ai/skills/quality/documentation-consistency.md (Mode A/C)
tasks/TASK_LIFECYCLE.md §3 (BACKLOG → READY criteria)

docs/01-game-design/RELIC_RULES.md            §1, §2, §3, §4, §5, §6  (canonical owner)
docs/01-game-design/GAME_RULES.md             §13 (Relic Rules), §17 (fixed resolution order),
                                              §20 (rule change policy)
docs/02-technical/GAME_STATE.md               §2, §2.3, §0 item 5 (no parallel representation),
                                              §2.1.7 / §2.1.10 (serialization + ordering precedent)
docs/02-technical/API_CONTRACTS.md            §3 (battle start), §6 (error convention)
docs/02-technical/DATABASE.md                 §1, §2, §3, §4
docs/02-technical/REDIS_STATE.md              §2 (serialization boundary), §7 (deferral)
docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md   items 3–5
docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md  items 7–8
docs/03-decisions/README.md                   §2, §3, §5 (when an ADR may be created)
docs/00-overview/MVP_SCOPE.md                 §1 Relics, §2, §4
docs/00-overview/GDD.md                       §2, §10
docs/00-overview/ROADMAP.md                   Phase 2

tasks/backlog/TASK-027-relic-ownership-and-battle-start-snapshot.md
tasks/backlog/TASK-029-battlestate-runtime-serialization-mapping.md
tasks/backlog/TASK-030-post-api-battle-start-endpoint.md
tasks/completed/TASK-037-resolve-player-level-pet-level-derivation-contract.md
  (precedent for this task's shape — a documentation-only contract-closing task)
```

Existing source code may be inspected **read-only** as evidence of current
behavior only (e.g. `src/backend/GameServer.Domain/Battle/PetState.cs`,
`src/backend/GameServer.Domain/Battle/BattleState.cs`). Code is never authority
over an unresolved rule (`AGENTS.md` §2); it cannot decide A, B, or C.

---

## Decision Required

Determine, **from the authoritative evidence only**, the single deterministic
answer for each item. Each answer must be either *derived from evidence* (with
the exact path + section cited) or *escalated as a required human gameplay
decision*.

```text
Q1 (Blocker A). What does one `PetState.EquippedRelics[]` element contain?
    — Is it conclusively determined by the documents?
    — If yes: name the representation and the owning document/section that
      determines it.
    — If no: STOP and list it as a required human gameplay decision, with the
      candidate set and the evidence for and against each candidate.

Q2 (Blocker B). What assigns the equip slot index `1 → 5`?
    — Is one interpretation clearly implied by the documents?
    — If yes: state it explicitly and name the owning document/section.
    — If no: STOP and list it as a required human gameplay decision.
    — Note: whatever is chosen becomes the gameplay trigger resolution order
      (RELIC_RULES.md §4.2), so a convenience choice is not a neutral choice.

Q3 (Blocker C). Are duplicate selections legal?
    — Resolve each of C1–C4: same instance twice; duplicate of the same
      definition via distinct instances; distinctness requirement per slot;
      and the rejection behavior/error code if duplicates are invalid.
    — If the documents already resolve any of C1–C4, record it with its source.
    — For anything unresolved: STOP and list it as a required human gameplay
      decision.

Q4. Ownership. Which document canonically owns each resolved answer?
    — Expected by the "This document answers…" purpose lines and AGENTS.md §2:
      the Relic *rule* (element semantics insofar as it is a gameplay rule,
      slot index source, duplicate policy) belongs to
      docs/01-game-design/RELIC_RULES.md, because it owns Relic equip and
      trigger ordering and is the more specific domain rule.
      The *state shape* of `PetState.EquippedRelics[]` belongs to
      docs/02-technical/GAME_STATE.md §2.3.
      The *request validation* wording belongs to
      docs/02-technical/API_CONTRACTS.md §3.
      Confirm this split against the purpose lines rather than assuming it; if
      two documents' purposes both plausibly cover one answer, report that as a
      structural ambiguity (documentation-change.md §3) instead of guessing.

Q5. Is a new ADR required?
    — Expected: no. These are domain-rule and state-shape details, not new
      architectural decisions; ADR-011 item 4 and ADR-012 items 7–8 already
      decide ownership-vs-equip and the battle-scoped snapshot.
    — An ADR may be created only if a genuinely new cross-cutting decision is
      actually made and approved (docs/03-decisions/README.md §2/§5). Do not
      record an unapproved decision.
```

**If any of Q1–Q3 cannot be answered from the evidence**, do not choose the
easiest or most implementation-friendly option. Execute the Stop Conditions.

---

## Deterministic Rules

Whichever branch the evidence supports, the resolved contract must end up
stating all of the following explicitly in the owning document(s). This section
is the checklist of what "unambiguous" means for TASK-027; it does **not**
pre-decide any answer:

```text
[ ] A: the exact element representation of PetState.EquippedRelics[]  (one
       representation, no parallel representation — GAME_STATE.md §0 item 5)
[ ] A: what identifies a Relic for ownership-validation purposes, and how that
       identity reaches the snapshot
[ ] B: the single named source of the equip slot index, and the statement that
       it is fixed at battle start and cannot change mid-battle
[ ] B: the relationship between slot index and the request array, stated
       explicitly (positional or explicitly not positional)
[ ] C: whether a Relic instance may occupy more than one slot, stated explicitly
[ ] C: whether two instances of the same RelicDefinition may be equipped
       simultaneously, stated explicitly
[ ] C: the validation outcome for a selection that violates the policy, and
       which documented error code covers it (API_CONTRACTS.md §6 envelope)
[ ] Deterministic behavior for a VALID loadout: the resulting snapshot contents
       and their order, stated as a rule
[ ] Deterministic behavior for an INVALID loadout: which conditions are rejected,
       at which layer (Application validation vs DB constraint), and with what
       documented response
[ ] The rule is stated once, in one canonical owner, and every other document
       references it by path + section (documentation-change.md §2)
[ ] No new gameplay mechanic is introduced beyond what is needed to close A/B/C
```

No gameplay value may be invented. If closing a gap would require a genuinely
new mechanic or a balance number, that is a design change requiring human
approval — not something this task may decide.

---

## MVP Scope

Verify against `MVP_SCOPE.md` §1/§4 before editing:

```text
~10 Relics                                   — IN (MVP_SCOPE §1)
Trigger system                               — IN (MVP_SCOPE §1)
3–5 equipped Relics per battle               — IN (MVP_SCOPE §1)
Battle-scoped loadout for the active Pet     — IN (ADR-011 item 4, ADR-012 items 7–8)
Relic instance ownership (Player 1─N Relic)  — IN (DATABASE.md §2)
Relic trigger / effect / stacking RUNTIME    — NOT this task (no trigger logic is
                                               implemented or newly specified here;
                                               only the ordering semantics B already
                                               references are made explicit)
Any item in MVP_SCOPE.md §2                  — OUT
```

This task **must not** expand scope to make an answer convenient, and must not
classify a new Relic system as IN. If closing A/B/C would require new MVP
content (new Relics, new trigger types, an acquisition/reward system, a
stacking mechanic), that is a separate design decision — STOP and report.

---

## Scope

### In Scope

- Identify blockers A, B, and C precisely, with file + section on every claim
- Inspect all authoritative Relic rules and the documents that constrain the
  loadout snapshot
- Resolve the `PetState.EquippedRelics[]` element representation, **or** record
  it as a required human gameplay decision
- Resolve the source of equip slot index `1–5`, **or** record it as a required
  human gameplay decision
- Resolve duplicate selection semantics (C1–C4), **or** record the unresolved
  parts as required human gameplay decisions
- Define deterministic behavior for valid and invalid Relic loadouts
- Update **only** the authoritative documentation necessary to record those
  decisions (canonical owner first; references only where wording goes stale)
- Record an Implementation Impact section so TASK-027 need not guess
- Record the non-blocking findings listed below as separate follow-ups, if the
  repository's workflow requires them tracked

### Out of Scope

- Any implementation work — including the battle-start endpoint
- Database migrations of any kind
- Relic entities (`RelicDefinition`, `Relic`) or any EF configuration / DbSet
- `PetState.EquippedRelics` code changes in `GameServer.Domain`
- Relic trigger, effect, cooldown, reset, or stacking resolution logic
- Relic acquisition paths, Relic rewards, Relic Tier/Star/Level
- Card loadout (TASK-028), Player Level (TASK-023/033), Boss state (TASK-020)
- Redis schema changes — `REDIS_STATE.md` §7 deferral must remain in force
- Any gameplay *design change* beyond the minimum needed to close A/B/C; a
  genuinely new mechanic is a separate approved design change
- Broad documentation cleanup, and anything in the Non-Blocking Findings below
- Editing `tasks/completed/*` (completed tasks are immutable — `TASK_LIFECYCLE.md` §3)
- Editing TASK-027's scope, objective, or acceptance criteria
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

```text
src/backend/GameServer.Domain/Battle/PetState.cs   — PetState exists with combat
                                                     stats and Passive members;
                                                     no EquippedRelics member
src/backend/GameServer.Domain/Battle/BattleState.cs — BattleState root Combo/
                                                     MatchCount + PetState
src/backend/GameServer.Infrastructure/Postgres/    — GameDbContext has Player and
                                                     Pet persistence (TASK-023/024);
                                                     no Relic sets
POST /api/battle/start                             — not implemented (TASK-030)
Relic trigger resolution                           — not implemented
```

TASK-027 is in `tasks/backlog/` with `Status: BACKLOG` and is not READY. This
task is its prerequisite.

---

## Documentation Changes

Edit only where each resolved answer actually belongs
(`documentation/documentation-change.md` §1–§3 — smallest authoritative source,
no duplication). Expected owners:

```text
Primary (expected):
  docs/01-game-design/RELIC_RULES.md       — slot index source (B); duplicate
                                             selection policy (C); deterministic
                                             valid/invalid loadout behavior; the
                                             element semantics insofar as they are
                                             a Relic gameplay rule
  docs/02-technical/GAME_STATE.md §2.3      — the `EquippedRelics[]` element
                                             representation (A) as a state-shape
                                             statement, with no rule duplication

Reference-only fixes (only if wording becomes stale):
  docs/02-technical/API_CONTRACTS.md §3     — request validation wording for
                                             duplicates / distinctness, only if
                                             §3's current wording is incomplete
  docs/02-technical/DATABASE.md §1–§3       — only if an answer imposes a storage
                                             or constraint statement
  docs/01-game-design/GAME_RULES.md §13     — only if §13's parent restatement
                                             becomes inconsistent
  docs/00-overview/GDD.md §10, MVP_SCOPE §1 — only if classification wording changes

Conditional:
  docs/03-decisions/ADR/*                   — only if Q5 finds a genuinely new
                                             cross-cutting decision requiring
                                             approval (README.md §2/§5)
```

Do not mechanically touch every document that mentions Relics. Do not restate a
resolved rule in a second document; reference the owner by path + section.

---

## Implementation Impact

Record this in the final report (documentation only — implement nothing) so
TASK-027 does not guess:

```text
PetState.EquippedRelics[]   — element representation fixed by this task
                              (owned Relic instance identity, in submitted
                              order); no code change here
PetState (other members)    — unaffected (combat stats, Passive)
BattleState                 — shape unaffected; EquippedRelics continues to
                              travel inside it (ADR-012 item 8, REDIS_STATE §2)
RelicDefinition / Relic     — storage shape unchanged by this task; DATABASE.md
                              §1's conditional instance-vs-join wording is
                              recorded as a non-blocking finding, not resolved here
POST /api/battle/start      — request contract unchanged in shape; §3's
                              validation wording is now complete (count,
                              ownership, and duplicate-instance rejection)
Relic trigger resolution    — consumes slot order (RELIC_RULES §4.2); this task
                              must not implement or newly design the trigger engine
Redis runtime state         — no schema change; REDIS_STATE §7 deferral unchanged
Tests                       — none written by this task; TASK-027's tests will
                              assert the rules recorded here
```

---

## Acceptance Criteria

- [ ] Blocker A is closed **or** explicitly recorded as a required human gameplay decision with the candidate set and the evidence for and against each candidate
- [ ] Blocker B is closed **or** explicitly recorded as a required human gameplay decision, including the note that B determines gameplay trigger order (`RELIC_RULES.md` §4.2) and therefore cannot be chosen for convenience
- [ ] Blocker C is closed for each of C1–C4 **or** the unresolved parts are explicitly recorded as required human gameplay decisions
- [ ] Deterministic behavior is stated for a valid loadout (snapshot contents + order) and for an invalid loadout (rejected conditions + documented error code)
- [ ] Every resolved decision is recorded in exactly one canonical owner document, with other documents referencing it by path + section (no duplicate definition introduced)
- [ ] Every claim cites a file + section; no answer is justified by "it is easier to implement"
- [ ] No new gameplay mechanic, balance value, Relic, or trigger was invented (`AGENTS.md` §7)
- [ ] Implementation Impact section present in the final report
- [ ] Source code modified: **NO**; `src/`, `tests/`, and migrations untouched
- [ ] No database migration, Relic entity, battle-start endpoint, or Relic trigger/stacking logic was created
- [ ] `tasks/completed/*` untouched; TASK-027's scope/objective/acceptance criteria unchanged
- [ ] The Non-Blocking Findings section is present in the final report, each recorded as a separate follow-up rather than fixed inline
- [ ] Quality review checklist passes (`quality/review.md` §1), docs-only items only
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §4, §10, §17)

---

## Affected Files & Areas

```text
[ ] src/backend/         — NO (documentation-only)
[ ] src/frontend/client/ — NO
[ ] tests/               — NO
[x] docs/                — RELIC_RULES.md and GAME_STATE.md §2.3 (expected
                           primary owners); conditional reference-only fixes
                           listed in Documentation Changes
```

---

## Non-Blocking Findings (track only — do not fix here)

These were found by the TASK-027 audit and are **not blockers for A/B/C**. Do
not expand this task into a broad documentation cleanup (`AGENTS.md` §16). The
executing agent reports them as separate follow-ups; if the repository's
workflow requires them tracked, record each as its own backlog task rather than
folding it into this one.

```text
N1. Stale implementation notes in `GAME_STATE.md` §2.3 / §2.2 item 2 — text
    stating that `BossState` does not exist and that `POST /api/battle/start`
    cannot create a battle, which no longer matches TASK-020/025/026.
    Location: docs/02-technical/GAME_STATE.md §2.2 item 2, §2.3.
    Impact: doc-vs-code staleness; readers may believe Boss state is unbuilt.
    Follow-up: a separate documentation-consistency task (not this one).

N2. Minor path/reference typo in TASK-027 (its title differs from its filename,
    e.g. `relic-ownership-and-battle-start-snapshot` vs
    "Relic Ownership & Battle-Start Snapshot"). Impact: reference hygiene only.
    Follow-up: correct when TASK-027 is next revised; do not rename files in a
    way that breaks existing references.

N3. `DATABASE.md` §1's conditional wording for `Relic` ("if Relics have
    per-instance state; otherwise ownership is a join table — see §2 note").
    Impact: unresolved instance-vs-join question. It is adjacent to Blocker A
    but is not itself an A/B/C blocker; if the A decision resolves it, note that
    in the report; otherwise track separately.

N4. No documented Relic acquisition path (how a Player obtains a Relic
    instance). Impact: TASK-027 can persist/validate owned Relics without it,
    but nothing can create ownership rows in a real flow.
    Follow-up: separate design/feature task.

N5. No dedicated future task for Relic trigger/effect/stacking implementation.
    Impact: the Relic subsystem has no owner task after TASK-027.
    Follow-up: separate backlog task.
```

---

## Stop Conditions

Universal stops from `AGENTS.md` §20 and `.ai/README.md` §13 apply.
Task-specific:

- **If any of Q1 (element type), Q2 (slot index source), or Q3 (duplicate policy)
  cannot be determined from the authoritative evidence → STOP.** Set Status to
  BLOCKED, move the file to `tasks/blocked/` (`TASK_LIFECYCLE.md` §3), and write
  the report in the exact `.ai/README.md` §13 format:

```text
STOP CONDITION

Problem:
<one or two sentences: which of A/B/C is undeterminable>

Relevant sources:
<file + section for every document consulted, both/all sides with the exact
 wording relied on>

Conflict / missing information:
<the exact undecidable question(s), spelled out as a question a human can answer>

Proposed resolution:
<the smallest documentation change that would close it — no invented rules, no
 chosen answer>

Waiting for:
<human gameplay decision on the listed question(s)>
```

- If two authoritative documents genuinely disagree (not merely fall silent) →
  STOP per `AGENTS.md` §4; do not pick the convenient side
- If closing a gap would introduce a new gameplay mechanic, new Relic content,
  a stacking system, an acquisition/reward system, or any balance value →
  STOP per `AGENTS.md` §7 / `MVP_SCOPE.md` §4
- If closing a gap would require adding an out-of-scope system →
  STOP per `AGENTS.md` §8
- If resolving A would require a second representation of a concept
  `GAME_STATE.md` already owns → STOP per `GAME_STATE.md` §0 item 5
- If the change would require code edits, a migration, or an entity to "make the
  docs true" → STOP; this task never edits code (`AGENTS.md` §17)
- If an ADR would record a decision not actually approved → STOP per
  `docs/03-decisions/README.md` §5
- If Q2 would be answered by selecting a trigger-resolution order the gameplay
  design has not decided → STOP; that is a gameplay decision, not a technical one
- If scope grows into TASK-028/029/030 boundaries, or into the Non-Blocking
  Findings → STOP & decompose into separate tasks

---

## Final Report

The executing agent's completion report must contain, in order:

```text
## Objective             — what was resolved, one paragraph
## Exact Ambiguities     — A, B, C as found, each with file + section
## Authoritative Evidence — every document consulted, by path + section, with
                           what it does and does not say about A/B/C
## Decision Required     — Q1–Q5 answers, each traced to evidence, or marked
                           as a required human gameplay decision
## Deterministic Rules   — the resolved element type / slot index source /
                           duplicate policy / valid-loadout behavior /
                           invalid-loadout behavior now recorded (or the STOP block)
## MVP Scope             — classification of everything touched
## Documentation Changes — files + sections actually edited, and files
                           deliberately not edited with the reason
## Implementation Impact — the section above, confirmed present
## Non-Blocking Findings — N1–N5, each with location, impact, and follow-up
## Out of Scope          — confirm nothing out-of-scope was touched
## Stop Conditions       — "none fired" or the full STOP CONDITION block
## Acceptance Criteria   — each criterion checked with evidence
```

If any acceptance criterion cannot be checked truthfully, the task is BLOCKED,
not DONE. If the task STOPs, it is BLOCKED and TASK-027 remains BACKLOG.

---

## Handoff to TASK-027

When this task reaches DONE, TASK-027 is re-audited **only** for:

```text
A. EquippedRelics element type
B. slot index source
C. duplicate policy
```

and may then transition `BACKLOG → READY` (`TASK_LIFECYCLE.md` §2). If this task
STOPs with an open human decision, TASK-027 stays BACKLOG.

**This task does not implement TASK-027**, does not change its scope, and does
not move it between lifecycle states.

---

## Completion Evidence

### Final Outcome — Q1, Q2 and Q3 ALL RESOLVED (TASK-038 DONE)

```text
Q1  EquippedRelics[] element representation   RESOLVED from evidence
                                              = RelicInstanceId (one owned
                                                Relic instance identity)
                                              RELIC_RULES.md §2.2

Q2  equip slot index source                   RESOLVED (human gameplay decision H1)
                                              slot index = request array position + 1
                                              RELIC_RULES.md §2.3

Q3  duplicate selection policy                RESOLVED (human gameplay decisions H2–H4)
                                              same instance twice  → REJECTED
                                              distinct instances of one definition
                                                                   → PERMITTED
                                              error code           → INVALID_LOADOUT
                                              RELIC_RULES.md §2.4
```

TASK-027 therefore becomes READY. See "TASK-027 Readiness Impact".

### Resolution History

This task first reached **BLOCKED** with Q2/Q3 escalated (see the STOP CONDITION
block below, retained as historical evidence). Human gameplay decisions H1–H4
were subsequently supplied and applied; the two OPEN records are now decided
rules and the task is DONE.

```text
H1  slot index = request `relicLoadout` array position + 1
    (array order authoritative; no sort by RelicInstanceId,
     RelicDefinitionId, AcquiredAt, or database order)
H2  the same RelicInstanceId MUST NOT appear more than once in one relicLoadout
    (an instance occupies at most one slot)
H3  multiple DISTINCT instances referencing the same RelicDefinitionId MAY be
    equipped simultaneously (ownership-instance rule, not definition-level)
H4  duplicate instance selection is rejected with the existing INVALID_LOADOUT
    (no new error code)
```

No stacking, trigger, effect, chain, damage, Passive, or Card semantics were
invented to apply these; `RELIC_RULES.md` §2.4 item 6 states explicitly that
item 3 does not define combined effects.

### Changed Files

- `docs/01-game-design/RELIC_RULES.md` — **primary owner.** Version 1.3 → 1.4
  (the §2.1–§2.5 structure was added at 1.3; §2.3 and §2.4 are now decided):
  - §2.1 Loadout Selection and Validation — count/ownership/server-side
    statements, all cited to `API_CONTRACTS.md` §3, `DATABASE.md` §2–§3,
    ADR-011 item 4 (nothing restated as a new rule)
  - §2.2 Element Representation of `PetState.EquippedRelics[]` — **Q1**:
    each element is one owned Relic **instance identity**; identity-not-
    definition; the same identity `GAME_EVENTS.md` §2 reports as `RelicId`
  - §2.3 Equip Slot Index Source — **Q2 RESOLVED (H1)**: the OPEN record and
    its candidate analysis are replaced by the decided rule (slot = array
    position + 1, no other property may order, `1..N`, fixed at battle start,
    snapshot preserves order)
  - §2.4 Duplicate Relic Selection — **OPEN (blocking)**: records C1–C4, each
    NOT DEFINED, with the §5 "same Relic" ambiguity and the §6 error-code gap
  - §2.5 Deterministic Behavior — states only what is DETERMINED (valid/invalid
    loadout) and names what depends on §2.3/§2.4
  - §4 — added the OPEN pointer to §2.3 and the §2.4 note on §5's "same Relic";
    §4.1–§4.3 unchanged; corrected a stale "§2.2" self-reference to "§2 item 2"
- `docs/02-technical/GAME_STATE.md` — Version 2.2 → 2.3. §2.3 field comment for
  `EquippedRelics[]` now states "3–5 owned Relic instance identities" and cites
  both OPEN records; §2.3 prose gains the identity-not-definition statement for
- `docs/02-technical/GAME_STATE.md` — Version 2.3 → 2.4. §2.3 field comment for
  `EquippedRelics[]` now carries the resolved slot rule; §2.3 prose states the
  identity-not-definition rule for this member (owner: `RELIC_RULES.md` §2.2)
  and that the array preserves submitted order. Shape/order statement only —
  no validation rule duplicated here.
- `docs/02-technical/API_CONTRACTS.md` — Version 1.2 → 1.3. §3 validation list
  and a new paragraph state the deterministic contract: `relicLoadout` is an
  **ordered** array whose order is authoritative (position + 1 = slot); the
  three-step validation order (count → ownership → distinctness); duplicate
  instance rejection as `INVALID_LOADOUT`; same-definition distinct instances
  permitted. No request/response shape changed.
- `docs/02-technical/DATABASE.md` — §1 `Relic` comment only: cross-reference
  updated to §2.2–§2.5, still noting the instance-vs-join **storage** question
  is not decided by the Relic rules (finding N3 left open, not resolved).
- `tasks/backlog/TASK-027-...md` — re-audited A/B/C (all resolved), obsolete
  BLOCKED wording removed, prerequisite contract recorded, acceptance criteria
  made testable, `Status: BACKLOG → READY`.
- `tasks/active/TASK-038-...md` — this file: status/lifecycle and evidence.

### Deliberately Not Edited

- `docs/01-game-design/GAME_RULES.md` §13 — its parent restatement ("3–5
  Relics", deterministic trigger order) remains consistent; no stale wording.
- `docs/00-overview/GDD.md` §2/§10, `docs/00-overview/MVP_SCOPE.md` §1 — no
  classification wording changed; nothing to synchronize.
- `docs/03-decisions/ADR/ADR-011`, `ADR-012` — Q5: no new ADR required, and
  their items 4 / 7–8 already own ownership-vs-equip and the battle-scoped
  snapshot. Historical ADR context untouched.
- `docs/03-decisions/README.md` — no ADR added, so no index change.
- `docs/02-technical/REDIS_STATE.md` — the deferral in §7 is unchanged; the
  array's serialization boundary is untouched.
- `docs/01-game-design/CARD_RULES.md`, `PET_RULES.md` — mention the Relic
  loadout only by reference; no wording became stale.
- `docs/01-game-design/RELIC_RULES.md` §4.1–§4.3, §5 — the trigger-ordering and
  anti-chain rules are unchanged; only the surrounding pointers were updated to
  cite the now-decided §2.3/§2.4.
- `tasks/completed/*` — none modified.
- TASK-027's game-scope sections (Affected Files, Implementation Notes) —
  unchanged in substance; only references and criteria were made precise.

### Validation Results

- **Documentation consistency pass (`documentation-consistency.md` Mode C) —
  PASS.** Greps over `docs/`:
  - `EquippedRelics` — all 18 occurrences accounted for; the two prose owners
    (`RELIC_RULES.md` §2.2, `GAME_STATE.md` §2.3) agree; `SIGNALR_PROTOCOL.md`
    §4.3 item 2 lists the member as not-delivered only (unaffected).
  - `slot order` / `equip slot` / `slot index` — every live mention now either
    states the ordering rule (`RELIC_RULES.md` §4.1–§4.3, unchanged) or points
    to the OPEN §2.3. No document asserts a slot-index source.
  - duplicate selection — no document asserts a policy; §2.4 records the gap.
  - Relic instance identity — `RELIC_RULES.md` §2.2 and `GAME_STATE.md` §2.3
    agree; `DATABASE.md` §1 cross-references without contradicting.
  - Section references resolve: RELIC_RULES.md has §2.1–§2.5 as cited; no
    dangling citation introduced.
  - No duplicate definition introduced: the element rule is stated once
    (`RELIC_RULES.md` §2.2) and referenced elsewhere (`documentation-change.md`
    §2).
- `git status` / timestamps — only the four `docs/` files above were written;
  no `src/`, `tests/`, migration, entity, endpoint, or SignalR file changed.

### MVP Scope Verification

- [x] `~10 Relics`, `Trigger system`, `3–5 equipped Relics per battle` remain IN
      (`MVP_SCOPE.md` §1) and were not reclassified.
- [x] No new system classified IN; no `MVP_SCOPE.md` §2 item touched.
- [x] No balance value, Relic, trigger type, stacking rule, or acquisition
      mechanic was invented.

### Server Authority & Scope Verification

- [x] Confirmed zero source-code changes
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no gameplay rule invented beyond closing A/B/C — Q1 was derived
      from evidence; Q2/Q3 were applied only as the supplied human decisions
      H1–H4, with no stacking/trigger/effect semantics added
- [x] Confirmed no database migration, Relic entity, battle-start endpoint, or
      Relic trigger/stacking logic created
- [x] Confirmed `tasks/completed/*` untouched

### TASK-027 Readiness Impact
**TASK-027 is now READY.**

```text
A. EquippedRelics element type   RESOLVED  → RELIC_RULES.md §2.2
B. slot index source             RESOLVED  → RELIC_RULES.md §2.3
C. duplicate policy              RESOLVED  → RELIC_RULES.md §2.4
```

All three re-audit items are closed, so TASK-027 has transitioned
`BACKLOG → READY` (`TASK_LIFECYCLE.md` §2, status-field change only — the file
stays in `tasks/backlog/` until an implementation agent picks it up).

---

## STOP CONDITION — FIRED, THEN RESOLVED

Formatted per `.ai/README.md` §13 / `core/context-discovery.md` §3.

**RESOLVED.** The human gameplay decisions requested below were supplied
(H1–H4) and applied to `RELIC_RULES.md` §2.3/§2.4 and the referencing
documents. The STOP CONDITION report is retained verbatim as historical
evidence of the block; it no longer describes the current state.

Formatted per `.ai/README.md` §13 / `core/context-discovery.md` §3.

```text
STOP CONDITION

Problem:
Two of the three questions this task exists to close cannot be determined
from the authoritative documentation. The source of the equip slot index
`1 → 5` that RELIC_RULES.md §4.1 orders by (B), and the duplicate-Relic
selection policy (C), are stated nowhere in docs/. §4.1's ordering is
gameplay-visible (§4.2 makes it the trigger resolution order), so B is a
gameplay decision; C governs request validation and the snapshot contents.
Neither may be chosen by an implementer (AGENTS.md §7, §20).

Relevant sources:
- docs/01-game-design/RELIC_RULES.md §4.1 — "Order equipped Relics by their
  equip slot index (1 → 5), fixed at battle start." Names the key, never its
  source.
- docs/01-game-design/RELIC_RULES.md §4.2 — the order IS the resolution order
  for a single event (why B is gameplay-significant).
- docs/01-game-design/RELIC_RULES.md §2.1 (pre-edit §2.1) — "the active Pet
  carries 3–5 of those owned Relics": a COUNT bound only, silent on
  distinctness.
- docs/01-game-design/GAME_RULES.md §13.2–§13.3 — "3–5 Relics … there is no
  global Player relic loadout"; no ordering or distinctness statement.
- docs/02-technical/API_CONTRACTS.md §3 — "relicLoadout must be 3–5 Relics
  owned by the player"; the request is an array, and no statement says its
  position carries gameplay meaning or that it does not. §6 defines the error
  envelope but "does not enumerate an exhaustive global error list".
- docs/02-technical/GAME_STATE.md §2.3 — "slot order fixed at battle start";
  the source is likewise unnamed.
- docs/02-technical/DATABASE.md §1–§4 — Relic/RelicDefinition fields exist;
  no constraint or rule on loadout ordering or distinctness.
- docs/00-overview/GDD.md §2, §10; docs/00-overview/MVP_SCOPE.md §1 — no
  ordering or distinctness statement.
- docs/03-decisions/ADR/ADR-011 item 4, ADR-012 items 7–8 — decide ownership-
  vs-equip, the battle-scoped snapshot, and "no persistent equip table"; none
  addresses slot-index assignment or duplicate selection.
- docs/02-technical/GAME_EVENTS.md §2 — RelicTriggered carries a single
  RelicId plus "deterministic order index for this event"; consistent with
  duplicates producing two indistinguishable events at different slots, which
  is why the policy must be stated rather than inferred.
- Negative result: a repository-wide search of docs/ for loadout ordering
  ("order", "position", "array", "sequence" in API_CONTRACTS.md §3; "slot",
  "order", "index", "equip" in RELIC_RULES.md) returns only §4's own rule.
  No document assigns the index.

Conflict / missing information:
No docs-vs-docs CONFLICT exists: every document that speaks to the loadout
agrees on what it says. These are MISSING RULES (AGENTS.md §7 / §20
"Missing rule" and "Ambiguous requirement"), not §4 contradictions.

B. What assigns equip slot index 1 vs 5?
   Candidate interpretations, none excluded by any document:
     B1 request `relicLoadout` array position
     B2 acquisition order (DATABASE.md §1 Relic.AcquiredAt)
     B3 RelicInstanceId ordering
     B4 RelicDefinitionId ordering
     B5 another documented rule (none exists)
   Note: GAME_STATE.md §2.1.10 item 2 canonicalizes an UNORDERED Swap pair so
   "a request's argument order can never make the stored value ambiguous".
   That is about Swap pairs, not Relic slots; §4 does not say slots are
   unordered. It is analogy only and argues neither for nor against B1.

C. May a selection contain a duplicate?
   C1 May the same Relic instance identity appear more than once?  NOT DEFINED
   C2 Must every selected element be a distinct owned instance?     NOT DEFINED
   C3 May two distinct instances of the SAME RelicDefinition be
      equipped simultaneously?                                     NOT DEFINED
   C4 What is the rejection behavior / documented error code for a
      violating selection?                                         NOT DEFINED
   Note: §5's anti-chain rule operates "per Relic" without defining what
   identifies "the same Relic"; it therefore neither permits nor forbids
   duplicates. No stacking behavior is documented or implied.

Proposed resolution:
Smallest documentation change once a human decides — edit only
docs/01-game-design/RELIC_RULES.md, replacing the two OPEN records (§2.3,
§2.4) with the decided rules:
 (a) §2.3 — name the ONE source of the equip slot index, and state the index
     assigned to each submitted element explicitly (e.g. "Slot 1 = …,
     Slot 2 = …"), keeping §4.1's fixed-at-battle-start property;
 (b) §2.4 — state whether a Relic instance may occupy more than one slot,
     whether two instances of one definition may be equipped together, and
     the rejection code for a violating selection;
 (c) §2.5 — promote "NOT DETERMINED" to the decided behavior.
Reference-only touch-ups only if wording becomes stale:
GAME_STATE.md §2.3, API_CONTRACTS.md §3. No new ADR:
ADR-011 item 4 / ADR-012 items 7–8 already own the ownership-vs-equip and
battle-scoped-snapshot decisions (docs/03-decisions/README.md §2/§5 — do not
record an unapproved decision).

Waiting for:
Human gameplay decision on:
 (1) the source of equip slot index 1 → 5 (B; one of B1–B5 or another stated
     rule) — this sets the gameplay trigger resolution order;
 (2) whether a Relic instance may be selected more than once in one loadout,
     and whether two distinct instances of the same RelicDefinition may be
     equipped simultaneously;
 (3) if duplicates are invalid, which documented error code covers it.
```

---

## Final Report

### 1. Status

**DONE** — Q1 determined from evidence; Q2 and Q3 determined by the supplied
human gameplay decisions H1–H4. All three TASK-027 blockers are closed, and
TASK-027 has been promoted to READY.

```text
Q1  EquippedRelics element representation   RESOLVED (evidence-determined)
                                            = RelicInstanceId
Q2  equip slot index source                 RESOLVED (H1) = array position + 1
Q3  duplicate selection policy              RESOLVED (H2–H4)
Q4  snapshot contract / ownership           RESOLVED (split confirmed; §2.5 fully DETERMINED)
Q5  new ADR required?                       NO
```

Lifecycle: the task first reached BLOCKED with Q2/Q3 escalated. On resumption it
moved `BLOCKED → IN PROGRESS` (the only allowed exit per `TASK_LIFECYCLE.md` §2
— there is no `BLOCKED → IN REVIEW`), then `IN PROGRESS → IN REVIEW → DONE`.
The STOP CONDITION block below is retained as historical evidence of the block.

### 2. Questions Resolved

**Q1 — `PetState.EquippedRelics[]` element representation: RESOLVED.**

Each element is **one owned Relic instance's identity** — the instance the
Player owns (`DATABASE.md` §1 `Relic.RelicInstanceId`) — **not**
`RelicDefinitionId` and **not** resolved `RelicDefinition` data.

Derived from four independent pieces of evidence that converge:

```text
1. GAME_STATE.md §2.3 names PetState.EquippedRelics[] among "the sibling
   IDENTITY fields" alongside BossState.BossId and PetState.PassiveId
   (lines 959–961: "the same member shape the sibling identity fields
   elsewhere in state use (BossState.BossId, PetState.EquippedRelics[],
   PetState.EquippedCards[])").
2. GAME_STATE.md §2.3 items 1–4 define that shape: "It is an identity, not a
   definition. The field carries the identifier only… no second copy of the
   definition is introduced." So resolved definition data (A3) is excluded by
   §0 item 5's no-parallel-representation ban.
3. GAME_EVENTS.md §2 defines RelicTriggered's payload as a single RelicId —
   one identity member, not a resolved effect. The array element and RelicId
   are the same identity.
4. Ownership is instance-based (RELIC_RULES.md §2.1, DATABASE.md §1–§2
   Player 1─N Relic, ADR-012 item 7) and API_CONTRACTS.md §3 validates
   "Player ownership of the selected instances". RelicDefinitionId is a
   property of the instance (DATABASE.md §1 FK), so the definition is
   reachable from the element rather than being the element.
```

This is why A1 wins over A2/A3 rather than by weight of evidence: the
"identity, not a definition" rule is explicit and already applied to the two
identity fields the document groups this member with.

**Q2 — equip slot index source: RESOLVED by human gameplay decision H1.**

The source was **not** determinable from the documents: `RELIC_RULES.md` §4.1
named the key ("equip slot index 1 → 5") but never its source, and all five
candidates (B1–B5) remained consistent with the literal text. It was therefore
escalated, and **H1** decided it:

```text
slot index = request `relicLoadout` array position + 1

relicLoadout[0] → slot 1   ...   relicLoadout[4] → slot 5
```

The array order is authoritative; sorting by `RelicInstanceId`,
`RelicDefinitionId`, `AcquiredAt`, or database order is forbidden. Because
§4.2 makes this the trigger resolution order, the decision was correctly a
gameplay one rather than a technical convenience choice. Recorded in
`RELIC_RULES.md` §2.3.

**Q3 — duplicate selection policy: RESOLVED by human gameplay decisions H2–H4.**

Not determinable from the documents for any of C1–C4. Escalated, then decided:

```text
H2  the same RelicInstanceId MUST NOT appear more than once → REJECTED
H3  distinct instances of the same RelicDefinitionId MAY be equipped together
H4  rejection uses the existing INVALID_LOADOUT (no new code)
```

`RELIC_RULES.md` §2.4 item 6 records explicitly that H3 does **not** define
combined/stacking effects, so no stacking semantics were invented. Recorded in
`RELIC_RULES.md` §2.4.

**Q4 — documentation ownership: RESOLVED, and confirmed rather than assumed.**

The expected split held against the purpose lines:

```text
Relic gameplay rules (slot index source, duplicate policy, valid/invalid
  loadout behavior, element meaning as a Relic rule) → RELIC_RULES.md
State shape of PetState.EquippedRelics[]              → GAME_STATE.md §2.3
Request validation wording                            → API_CONTRACTS.md §3
Storage shape (instance vs join)                      → DATABASE.md (left OPEN, N3)
```

No two documents' stated purposes both plausibly covered one answer, so no
structural ambiguity needed reporting (`documentation-change.md` §3).

**Q5 — new ADR: NOT REQUIRED.**

No genuinely new cross-cutting decision was made. ADR-011 item 4 and ADR-012
items 7–8 already own ownership-vs-equip and the battle-scoped snapshot.
Creating an ADR would have meant recording a decision nobody made
(`docs/03-decisions/README.md` §2/§5), so no ADR was created and the ADR
index is unchanged.

### 3. Evidence Used

Read and relied on (path + section):

```text
AGENTS.md §2, §4, §7, §8, §11, §16, §17, §20
tasks/TASK_LIFECYCLE.md §1–§5; tasks/README.md §3, §7, §9, §12, §13
.ai/README.md §6, §13, §18
.ai/workflow/documentation/documentation-change.md §1–§3
.ai/workflow/core/context-discovery.md §3
.ai/skills/quality/documentation-consistency.md (Modes A/C)
.ai/skills/gameplay/gameplay-behavior-derivation.md

RELIC_RULES.md §1, §2, §3, §4, §5, §6, §7        (canonical owner)
GAME_RULES.md §13, §16, §17, §18, §21
GAME_STATE.md §0 items 4–5, §2, §2.1.7, §2.1.10, §2.2, §2.3, §2.4
GAME_EVENTS.md §1, §2 (RelicTriggered, PassiveCharged/PassiveTriggered)
API_CONTRACTS.md §1, §3, §5, §6
DATABASE.md §1, §2, §3, §4
REDIS_STATE.md §2, §7
SIGNALR_PROTOCOL.md §4.2, §4.3
ADR-011 items 3–5; ADR-012 items 7–8; docs/03-decisions/README.md §2, §3, §5
MVP_SCOPE.md §1 (Relics), §2, §4
GDD.md §2, §3, §10
ROADMAP.md §1 Phase 2
CARD_RULES.md §1, §3 (sibling loadout pattern — reference only)

tasks/backlog/TASK-027-*.md, TASK-029-*.md, TASK-030-*.md
tasks/completed/TASK-037-*.md (precedent for this task's shape)
```

Existing source inspected **read-only** as evidence only (never authority,
`AGENTS.md` §2): `GameServer.Domain/Battle/PetState.cs`,
`GameServer.Domain/Battle/BattleState.cs`,
`GameServer.Domain/Passives/PassiveProgress.cs`,
`GameServer.Domain/Bosses/BossId.cs`. These corroborate that the identity
fields (`PassiveId`, `BossId`) are documented as "an identity, not a
definition" and that `BossId`'s own doc-comment names `PetState.EquippedRelics[]`
as a sibling identity field — supporting evidence for Q1. No code was changed.

### 4. Documentation Files Changed

```text
docs/01-game-design/RELIC_RULES.md    v1.2 → v1.3   §2.1–§2.5 added; §4 pointer
docs/02-technical/GAME_STATE.md       v2.2 → v2.3   §2.3 field comment + prose
docs/02-technical/API_CONTRACTS.md    §3            OPEN-record paragraph
docs/02-technical/DATABASE.md         §1            cross-reference comment only
tasks/active/TASK-038-...md           evidence + final report
```

Deliberately not edited, with reason: `GAME_RULES.md` §13 (still consistent),
`GDD.md` §2/§10 and `MVP_SCOPE.md` §1 (no classification change), `ADR-011` /
`ADR-012` / `docs/03-decisions/README.md` (no new ADR; historical context not
rewritten), `REDIS_STATE.md` (§7 deferral unchanged), `CARD_RULES.md` /
`PET_RULES.md` (reference the loadout only), `tasks/completed/*`, and TASK-027's
Objective/Scope/Acceptance Criteria.

### 5. Exact Contract Now Established

```text
ELEMENT (Q1, closed) — RELIC_RULES.md §2.2, cited by GAME_STATE.md §2.3
  PetState.EquippedRelics[]  → 3–5 elements
  each element               → one owned Relic instance identity
  identity only — Trigger/Condition/Effect/Reset are RelicDefinition's and are
  NOT copied into the element (§0 item 5)
  the same identity GAME_EVENTS.md §2 reports as RelicTriggered's RelicId

SELECTION (determined) — RELIC_RULES.md §2.1, §2.5
  3–5 Relics owned by the requesting Player (API_CONTRACTS.md §3) — count bound
  ownership validated server-side, request-time (Application layer)
  not owned / out-of-count  → rejected, API_CONTRACTS.md §6 envelope,
                              INVALID_LOADOUT (API_CONTRACTS.md §3)
  rejection writes no battle state

SNAPSHOT (determined) — RELIC_RULES.md §2 item 3, §2.5
  array written once at battle start (POST /api/battle/start)
  fixed for the battle; no mid-battle re-equip or swap
  the only equip representation in active battle state (REDIS_STATE.md §2)
  no live inventory read after battle start (ADR-012 item 8)
  no write-back to PostgreSQL ownership rows (ADR-012 item 8)

SLOT ORDER (RESOLVED, H1) — RELIC_RULES.md §2.3
  slot index = request `relicLoadout` array position + 1
  the array order is authoritative; no sort by RelicInstanceId,
  RelicDefinitionId, AcquiredAt, or database order
  fixed at battle start; preserved into the snapshot

DUPLICATES (RESOLVED, H2–H4) — RELIC_RULES.md §2.4
  C1 same instance twice?                  REJECTED (at most one slot each)
  C2 all selected instances distinct?      YES (pairwise distinct)
  C3 two instances of one definition
     simultaneously?                       PERMITTED (distinct instances)
  C4 rejection code                        INVALID_LOADOUT (existing code)
  no stacking/combined-effect behavior is defined or implied; §5 untouched
```

### 6. Validation

Performed per the task's Validation list:

```text
1. Re-read all modified sections — done (RELIC_RULES.md §2–§4,
   GAME_STATE.md §2.3, API_CONTRACTS.md §3, DATABASE.md §1).
2. Stale/conflicting-description search — done for EquippedRelics,
   RelicInstanceId, slot index, relicLoadout, duplicate Relic, and
   INVALID_LOADOUT. No contradiction remains; every live slot-order mention
   states the array-position rule, and every duplicate mention states the
   reject/permit split.
3. No implementation files changed — verified by timestamp sweep of src/ and
   tests/ (zero files written this session).
4. No completed task changed — verified; tasks/completed/ untouched.
5. TASK-027 promoted BACKLOG → READY after re-audit — verified.
6. TASK-027 carries TASK-038 as completed prerequisite evidence — verified
   (Dependencies line + "Prerequisite Contract — Resolved by TASK-038" section).
7. Repository documentation-consistency validation — run per
   documentation-consistency.md Mode C: PASS (no duplicate definition, no
   stale reference, no contradiction; all section citations resolve).
8. No code tests run — the documentation workflow defines none, and no source
   code was modified.
```

The task's own 12-point validation list was run in full; results are recorded
in §5 "Validation" of the final response and in the Validation Results above.

### 7. Remaining Human Decisions

**None.** All four requested decisions were supplied and applied:

```text
H1. Slot index source            DECIDED — request `relicLoadout` array position + 1
H2. Same instance in two slots   DECIDED — forbidden (at most one slot per instance)
H3. Two instances, one definition DECIDED — permitted (distinct instances)
H4. Duplicate rejection code     DECIDED — existing INVALID_LOADOUT
```

No further gameplay decision is outstanding for the Relic loadout snapshot
contract.

### 8. Out of Scope

Confirmed untouched: no source code, tests, migrations, Relic entities,
battle-start endpoint, Relic trigger/effect/stacking logic, Redis schema, Card
loadout, Player Level, Boss state, `tasks/completed/*`, or TASK-027's gameplay
scope. The five Non-Blocking Findings (N1–N5) were recorded, not fixed, with one
exception justified below.

**N3 exception (recorded explicitly, not silently fixed):** `DATABASE.md` §1's
conditional `Relic` instance-vs-join wording was **not** resolved. Only a
cross-reference comment was updated, because the Q1 answer touches that line
directly and leaving it unqualified risked a reader inferring that Q1 settled
the storage question. The wording itself is unchanged and the question remains
open for a separate task.

### 9. Next Step

```text
TASK-038 is DONE. Its output is now the implementation contract for TASK-027.

1. Re-audit TASK-027 for A / B / C — COMPLETE:
      A  RESOLVED  (RELIC_RULES.md §2.2)
      B  RESOLVED  (RELIC_RULES.md §2.3, decision H1)
      C  RESOLVED  (RELIC_RULES.md §2.4, decisions H2–H4)
2. TASK-027 has transitioned BACKLOG → READY (status field only; it stays in
   tasks/backlog/ until an implementation agent picks it up).
3. Next action: implement TASK-027 — not as part of TASK-038.
```

**File-location note (historical).** While BLOCKED, this task was placed in
`tasks/blocked/` as the Stop Conditions and `TASK_LIFECYCLE.md` §3/§4 direct.
On resumption it followed the lifecycle's only allowed exit —
`BLOCKED → IN PROGRESS` (there is no `BLOCKED → IN REVIEW`) — moving
`blocked/ → active/`, and then progressed `IN PROGRESS → IN REVIEW → DONE`
into `tasks/completed/`.

### 10. Acceptance Criteria

- [x] Blocker A closed — Q1 resolved from evidence, with the evidence cited
      (`RELIC_RULES.md` §2.2; `GAME_STATE.md` §2.3; `GAME_EVENTS.md` §2)
- [x] Blocker B closed — `RELIC_RULES.md` §2.3 states the slot index source
      explicitly (request array position + 1), names the prohibited sort keys,
      and records that it is fixed at battle start (decision H1)
- [x] Blocker C closed — `RELIC_RULES.md` §2.4 resolves C1–C4: same instance
      twice rejected, selected instances pairwise distinct, distinct instances
      of one definition permitted, rejection uses `INVALID_LOADOUT`
      (decisions H2–H4)
- [x] Deterministic behavior stated for a valid loadout (snapshot contents +
      order) and an invalid loadout (rejected conditions + `INVALID_LOADOUT`)
      (`RELIC_RULES.md` §2.5) — no part remains NOT DETERMINED
- [x] Each resolved decision recorded in exactly one canonical owner, others
      referencing by path + section; no duplicate definition introduced
- [x] Every claim cites a file + section; no answer justified by ease of
      implementation
- [x] No new gameplay mechanic, balance value, Relic, or trigger invented;
      no stacking/effect semantics defined by the duplicate rule
- [x] Implementation Impact section present (see Completion Evidence above)
- [x] Source code modified: NO; `src/`, `tests/`, migrations untouched
- [x] No migration, Relic entity, battle-start endpoint, or trigger/stacking
      logic created
- [x] `tasks/completed/*` untouched; TASK-027's gameplay scope unchanged (only
      references, criteria, and status were made precise)
- [x] Non-Blocking Findings recorded as separate follow-ups (N1–N5)
- [x] Quality review checklist passed for docs-only items
- [x] No authoritative rules or contracts violated (`AGENTS.md` §4, §10, §17)

**All Completion Criteria are satisfied: this task is DONE.** Q1 was determined
from evidence; Q2 and Q3 were resolved by the supplied human gameplay decisions
H1–H4. TASK-027 has been re-audited (A/B/C all resolved) and promoted to READY.
