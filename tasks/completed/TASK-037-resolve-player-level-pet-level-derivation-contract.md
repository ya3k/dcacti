# TASK-037 — Resolve Player.Level ↔ Pet.Level Derivation Contract

---

## Metadata

```text
Task ID:           TASK-037
Type:              DOCUMENTATION
Status:            DONE
Risk:              MEDIUM
Priority:          HIGH
Primary Agent:     review
Supporting Agents: gameplay, backend
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery, quality/documentation-consistency,
                   gameplay/gameplay-behavior-derivation, quality/scope-validation
Dependencies:      TASK-023 (DONE — provides the Player.Level contract this task
                   clarifies downstream of). TASK-024 consumes this task's output
                   and must not implement derivation until this contract is
                   unambiguous.
```

---

## Objective

Close the remaining documentation-level ambiguity in the
`Player.Level → Pet.Level` derivation contract so that a future
implementation task can compute `Pet.Level` deterministically — without
guessing multiplier semantics, rounding, or clamp interactions — by
updating only the authoritative documents that own this contract. This
task is **documentation-only**: no source code, no migrations, no tests
are written or modified.

---

## Current Contract

Already established and **not to be revisited** (TASK-023 DONE;
`PET_RULES.md` §5; `GAME_RULES.md` §9.3; ADR-011 item 7; ADR-012 items
1–6, 12):

```text
Player = persistent account / owner
  Player entity fields: PlayerId, DiscordUserId, Level, CreatedAt
  Player.Level ∈ [1, 50], initial value 1, no combat stats

Pet = combat character; combat authority lives in BattleState → PetState

Pet Level model (authoritative, derived — Option A):
  Pet Level = clamp(Player Level × Pet Level Multiplier, 1, 50)
  Multiplier = per-Pet configuration on PetDefinition (never hard-coded)
  Pet.Level range [1, 50]; no Pet XP; no independent Pet progression;
  Tier/Star remain independent axes; formula is authoritative, not temporary
```

The relationship to resolve is exactly:

```text
Player.Level
      ↓
 [defined rule]      ← this task must make every input to this rule explicit
      ↓
Pet.Level
```

---

## Exact Ambiguity

The owning documents fix the formula shape, clamp bounds, storage
location, and MVP status — but leave the following **operationally
unspecified**. Each item below is a gap an implementer would have to
guess:

```text
A1. Multiplier numeric type
    Integer? decimal? floating-point? No document states a type for
    PetDefinition.PetLevelMultiplier (DATABASE.md §1 says only "config").

A2. Multiplier allowed range / validity
    Must it be > 0? ≥ 1? Is there an upper bound? May it be < 1 (so a
    Pet lags its owner)? No document constrains the value domain.

A3. Rounding semantics of the product
    When Player.Level × Multiplier is non-integer (e.g. 3 × 1.5 = 4.5),
    which deterministic rule applies — floor, ceil, round-half, truncate,
    or is the multiplier required to be integer-only so the question
    cannot arise? No document states a rounding rule anywhere in docs/.

A4. Order of operations: rounding vs clamp
    Is the clamp applied to the raw product, or to the rounded product?
    Must be stated explicitly even if the bounds make them equivalent in
    practice.

A4b. Default / MVP Pet multiplier values
    No document provides multiplier values for the five MVP Pets, nor
    states whether those values are balance/config concerns deferred to
    a later balance pass (like the XP curve) or required MVP content
    now. Do NOT invent values (AGENTS.md §7).

A5. Whether the stored Pet.Level snapshot (DATABASE.md §1) and any
    battle-time PetState.Level must use the identical rounding rule
    (they must — state it once, canonically, so there is one rule).
```

Items already resolved and **out of this task's ambiguity list**: whether
the model is derived vs independent (derived), where the multiplier is
stored (PetDefinition), clamp bounds ([1, 50]), Pet Level range ([1, 50]),
whether Pet Level progresses independently (no), whether Player Level is
MVP (yes), whether the formula is temporary (no).

---

## Authoritative Evidence

Read before deciding; cite by path + section in the final report. Do not
copy their contents into this task file.

- `AGENTS.md` §2 (source-of-truth order), §4 (conflict resolution), §7
  (no invented rules), §8 (MVP protection), §17 (documentation change rule)
- `docs/01-game-design/PET_RULES.md` §5, §5.1, §6 — canonical owner of the
  Pet Level formula and multiplier
- `docs/01-game-design/GAME_RULES.md` §9.3 — parent-rule restatement of the
  formula; conflicts resolve in favor of `GAME_RULES.md`, details in
  `PET_RULES.md`
- `docs/00-overview/MVP_SCOPE.md` §1, §4 — Player Level and Pet Level
  progression IN; classification authority for anything unlisted
- `docs/00-overview/GDD.md` §6, §14 — Pet Level derived from Player Level;
  Meta Progression
- `docs/00-overview/ROADMAP.md` Phase 2/3 — progression scope; balance pass
- `docs/02-technical/DATABASE.md` §1 (Pet, PetDefinition.PetLevelMultiplier),
  §3 (Level constraints) — storage shape the contract must be expressible in
- `docs/02-technical/GAME_STATE.md` §2.3 — PetState fields (Level presence)
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` items 5, 7
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` items
  1–6, Consequences — formula ownership, clamp semantics, no Pet XP
- `docs/03-decisions/README.md` §2, §5 — when an ADR may be created; never
  for a decision not yet made
- Completed tasks (evidence of prior decisions, not authority over docs):
  `tasks/completed/TASK-005A*`, `TASK-009*`, `TASK-013*`, `TASK-014*`,
  `TASK-016*`, `TASK-017*`; `tasks/backlog/TASK-023*` (DONE),
  `TASK-024*`, `TASK-033*`
- Inspect current source **read-only** for assumptions the docs must
  account for (e.g. `Player.cs`, `PlayerConfiguration.cs`). Existing code
  is evidence of behavior, never authority over an unresolved rule
  (`AGENTS.md` §2).

---

## Decision Required

Determine, **from the authoritative evidence only**, the single
deterministic answer for:

```text
Q1. Is the derived model (Option A) confirmed authoritative,
    or do the docs support Option B (independent Pet Level)?
    — Expected from evidence: Option A is already authoritative;
      do not reopen unless a genuine docs-vs-docs conflict is found.

Q2. Multiplier type and allowed value range (A1, A2).

Q3. Rounding rule for a non-integer product (A3), and its order
    relative to the clamp (A4).

Q4. Are concrete MVP Pet multiplier values required now, or are they
    balance/config values deferred like the XP curve (A4b)?
    — If deferred: record the deferral explicitly; do not invent numbers.

Q5. Which document canonically owns the rounding/type/range statement
    (expected: PET_RULES.md §5 as formula owner; DATABASE.md §3 only for
    storage constraints; ADR only if a genuinely new cross-cutting
    decision is made — docs/03-decisions/README.md §2/§5).
```

**If any of Q2–Q4 cannot be answered from the evidence**, do not choose
the easiest or most implementation-friendly option. Execute the Stop
Conditions below.

---

## Deterministic Rules

The resolved contract — whichever branch the evidence supports — must end
up stating all of the following explicitly in the owning document(s).
This section is the checklist of what "unambiguous" means; it does not
pre-decide values:

```text
[ ] Formula statement (derived) OR explicit independence statement
[ ] Multiplier: storage location, type, allowed range, default/deferral
[ ] Rounding rule (or integer-only configuration that makes it moot)
[ ] Clamp bounds and operation order relative to rounding
[ ] Pet.Level range and initial-value semantics for a newly acquired Pet
    (only to the extent already documented — do not invent acquisition rules)
[ ] One canonical rule reused by stored Pet.Level and battle PetState.Level
[ ] MVP classification of every value introduced or deferred
```

No numeric balance value may appear unless an authoritative document
already contains it.

---

## MVP Scope

Verify against `MVP_SCOPE.md` §1/§4 before editing:

```text
Player Level (1–50)            — IN (MVP_SCOPE §1)
Pet Tier/Star/Level progress.  — IN (MVP_SCOPE §1, formula cited)
Pet Level Multiplier           — required configuration for the IN formula;
                                 its concrete MVP values: classify per Q4
Player Level → Pet Level link  — IN; this task may not remove or weaken it
Pet XP / Evolution             — OUT/FUTURE; must not appear
```

Do not expand MVP scope to make the contract convenient. If the evidence
shows Player Level should not affect Pet Level in MVP, that is a genuine
docs conflict — report it (§4 stop), do not silently rewrite scope.

---

## Scope

### In Scope
- Resolve Q1–Q5 using authoritative evidence; if unresolvable, emit the
  stop report instead of guessing
- Update the canonical owner document(s) so the contract is deterministic
  (expected primary: `PET_RULES.md` §5; secondary only if stale:
  `GAME_RULES.md` §9.3 reference, `DATABASE.md` §1/§3 constraint notes,
  `MVP_SCOPE.md`/`GDD.md` only if classification wording changes)
- Update or create an ADR **only if** a genuinely new cross-cutting
  decision is made and approved (`docs/03-decisions/README.md` §2/§5) —
  ADR-012 already owns the formula/clamp decision; do not duplicate it
- Record an Implementation Impact section (see below) so TASK-024/025
  need not guess
- Documentation consistency pass across referencing documents (no
  duplicate rule restatements — `documentation-change.md` §2)

---

## Out of Scope
- Any source-code or test change (src/, tests/) — zero code edits
- `PlayerState → PetState` combat migration (TASK-025/026)
- Moving HP/ATK/DEF/Crit/Power anywhere
- Modifying `DamagePipeline`, `BattleStateService`, `PassiveTracker`,
  `ResourceGenerator`, Boss Response, SignalR, Redis
- Pet persistence, Pet XP, rewards, session/JWT, OAuth (TASK-023/024/
  033/034 boundaries — do not revisit)
- Inventing multiplier numeric values, XP curves, or any progression
  mechanic not already documented
- Editing `tasks/completed/*` or TASK-016/018/019/021/022/023
- Any item listed as OUT in `MVP_SCOPE.md` §2

---

## Documentation Changes

Edit only where the resolved contract actually belongs
(`documentation/documentation-change.md` §1, §2 — smallest authoritative
source, no duplication):

```text
Primary (expected):
  docs/01-game-design/PET_RULES.md §5 — add/repair the missing
    type / range / rounding / order-of-operations statements, or the
    explicit deferral of MVP multiplier values, per the decision

Reference-only fixes (only if wording becomes stale):
  docs/01-game-design/GAME_RULES.md §9.3
  docs/02-technical/DATABASE.md §1, §3 (storage constraints only)
  docs/00-overview/MVP_SCOPE.md §1 / GDD.md §6 — only if classification
    wording changes

Conditional:
  docs/03-decisions/ADR/* — new or superseding ADR only if Q2–Q4 require
    a genuinely new approved decision; otherwise none (README §2/§5)
```

Do not mechanically touch every document that mentions Level.

---

## Implementation Impact

Record this in the final report (documentation only — implement nothing)
so the next implementation task does not guess:

```text
Player entity          — unchanged (four fields; Level [1,50] init 1)
PetDefinition          — PetLevelMultiplier gains documented type/range
                         constraints; values sourced per Q4 decision
PetInstance/PlayerPet  — stored Pet.Level = exact canonical rule; recompute
                         hook inputs unchanged (TASK-024)
PetState               — battle-time Level snapshot uses the same rule;
                         no second rounding path
BattleState            — unaffected shape; Level flows in via PetState
PlayerState removal    — unaffected (TASK-025; combat stats not touched)
DamagePipeline         — consumes Pet Level indirectly via stat curve
                         (PET_RULES §6); determinism guaranteed by this
                         contract; no pipeline code change from this task
BattleStateService /   — no change from this task; downstream tasks read
PassiveTracker /          the resolved contract instead of guessing
ResourceGenerator
Cards / Relics         — unaffected (battle-scoped loadouts; ADR-012)
Redis runtime state    — no schema change; PetState.Level serializes the
                         already-documented value
SignalR projection     — no contract change; Level travels as data
Tests                  — future derivation tests assert the documented
                         rounding/clamp/type rules (TASK-024), not
                         implementation convenience
```

---

## Acceptance Criteria

- [ ] Player Level semantics remain unambiguous and unchanged: `[1, 50]`,
      initial `1`, persistent, no combat stats (no regression vs TASK-023)
- [ ] Pet Level semantics are unambiguous: range, ownership, and the
      derived-vs-independent answer are stated explicitly with a single
      canonical owner document
- [ ] The formula (or an explicit independence statement) is explicit and
      identical across `PET_RULES.md` §5 and its references — no
      conflicting variant remains in `docs/`
- [ ] Multiplier semantics are explicit: storage location, type, allowed
      range, and default-or-deferral are each stated (or the task has
      legally STOPPED per Stop Conditions instead of inventing them)
- [ ] Rounding is explicit (rule named, or integer-only configuration
      stated so rounding cannot arise)
- [ ] Clamp behavior is explicit: bounds and order of operations vs
      rounding
- [ ] MVP scope is explicit: every value introduced is classified IN, and
      every deferred value (e.g. concrete MVP multiplier numbers, if Q4
      defers them) is recorded as balance/config — not silently omitted
- [ ] No undocumented progression mechanic was invented (no Pet XP, no
      Evolution, no XP curve, no fabricated multiplier numbers)
- [ ] Player/Pet ownership remains consistent with ADR-011/ADR-012 and
      TASK-023 (Player = account; Pet = combat; combat stats only on
      PetState)
- [ ] Source code modified: **NO**; `tasks/completed/*` and TASK-016/
      018/019/021/022/023 untouched
- [ ] Implementation Impact section present in the final report
- [ ] Quality review checklist passes (`quality/review.md` §1), docs-only
      items only
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10/§17)

---

## Affected Files & Areas

```text
[ ] src/backend/         — NO (documentation-only)
[ ] src/frontend/client/ — NO
[ ] tests/               — NO
[x] docs/                — PET_RULES.md §5 (primary); conditional
                           reference fixes listed in Documentation Changes
```

---

## Stop Conditions

Universal stops from `AGENTS.md` §20 and `.ai/README.md` §13 apply.
Task-specific:

- **If the evidence cannot determine derived-vs-independent, multiplier
  type/range, rounding semantics, clamp order, or the MVP classification
  of multiplier values → STOP.** Set Status BLOCKED, move to
  `tasks/blocked/`, and write the report in the exact form:

### STOP CONDITION — FIRED (RESOLVED)

**Resolution (human gameplay decision received):**

```text
PetLevelMultiplier type:  decimal
Allowed range:            > 0
Rounding:                 floor
Order:                    floor → clamp [1, 50]

Canonical rule:
  Pet.Level = clamp(
      floor(Player.Level × PetDefinition.PetLevelMultiplier),
      1,
      50
  )

Concrete MVP multiplier values: DEFERRED TO BALANCE / CONFIGURATION
```

The four decisions are authoritative and must not be reopened. The
original STOP CONDITION report is retained below as historical evidence
of the block.

### Original STOP CONDITION report

```text
NOT READY — HUMAN GAMEPLAY DECISION REQUIRED

Problem:
Q2 (multiplier numeric type and allowed value range), Q3 (rounding
semantics for a non-integer Player.Level × Multiplier product), and A4
(order of rounding relative to the clamp) are undeterminable from the
authoritative evidence. Q1 (derived model) and Q4 (MVP concrete values
= deferred balance/config) are determinable; the derivation contract as
a whole still cannot be closed without Q2/Q3/A4.

Relevant sources:
- docs/01-game-design/PET_RULES.md §5 item 1 — formula and
  "per-Pet configuration value (never hard-coded)"; no type, no range,
  no rounding rule, no round-vs-clamp order
- docs/01-game-design/GAME_RULES.md §9.3 — parent restatement of the
  same formula; adds "(config multiplier, not hard-coded)" only
- docs/02-technical/DATABASE.md §1 (PetDefinition.PetLevelMultiplier
  "config — PET_RULES.md §5; never hard-coded"), §3 (Pet.Level ∈ [1,50])
  — storage shape only; no type/range constraint on the multiplier
- docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md
  items 3–4 — resolved formula and clamp-bounds-the-result; silent on
  type, range, rounding, and round-vs-clamp order
- docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md item 7 —
  formula reference only
- docs/00-overview/MVP_SCOPE.md §1 (Pets: formula cited; Player Level
  "Exact XP curve is a balance concern, not a scope item" precedent),
  §4 (absent = FUTURE default / report ambiguity)
- docs/00-overview/ROADMAP.md Phase 3 — "Balance pass on all
  configurable values" (supports Q4 deferral of concrete numbers)
- docs/00-overview/GDD.md §6, §14 — Level derived from Player Level;
  no type/rounding statement
- docs/02-technical/GAME_STATE.md §2.3 — PetState lists Tier/Star/Level
  with no statement that battle-time Level uses the stored snapshot's
  derivation rule (A5)
- tasks/backlog/TASK-024*, TASK-033* — consumers; TASK-033 BLOCKED on
  the parallel XP-curve gap (precedent for not inventing numbers)
- src/ (read-only): Player.cs / PlayerConfiguration.cs implement only
  Player.Level [1,50] init 1; no PetLevelMultiplier, Pet entity, or
  derivation code exists — code supplies no type/rounding assumption
  either

Conflict / missing information:
1. Multiplier numeric type — integer, decimal, or floating-point? Not
   stated in any document (A1).
2. Multiplier allowed range — must it be > 0? ≥ 1? Is there an upper
   bound? May it be < 1 so a Pet lags its owner? Not stated (A2).
3. Rounding rule when the product is non-integer (e.g. 3 × 1.5 = 4.5)
   — floor, ceil, round-half, truncate, or integer-only multiplier that
   makes the question moot? No rounding rule exists anywhere in docs/
   for this formula (A3). Integer-only is also undetermined because the
   type is undetermined.
4. Order of operations — clamp applied to the raw product or to the
   rounded product? PET_RULES §5 says the clamp bounds "the result of
   the product," but without a rounding rule the order remains open
   (A4); floor-then-clamp vs clamp-then-floor can differ at the
   boundaries.
No docs-vs-docs conflict exists: every source that mentions the formula
agrees on its shape; the gaps are missing rules, not contradictions
(AGENTS.md §7 / §20 "Missing rule", not §4).

Proposed resolution:
Smallest documentation change once the human decides — edit only
docs/01-game-design/PET_RULES.md §5 (formula owner; Q5), adding:
(a) the multiplier's numeric type and allowed range (human choice);
(b) a named rounding rule for the product, or an integer-only
    configuration statement that makes rounding moot (human choice);
(c) the explicit order: round first then clamp, or clamp first then
    round (human choice, coupled to (b));
(d) one sentence that stored Pet.Level (DATABASE.md §1) and battle
    PetState.Level (GAME_STATE.md §2.3) both use this single canonical
    rule (A5);
(e) an explicit deferral line for concrete MVP multiplier values to
    balance/config (Q4 — already supported by ROADMAP Phase 3 and the
    XP-curve precedent; no numbers invented).
Reference-only touch-ups only if wording becomes stale:
GAME_RULES.md §9.3, DATABASE.md §1/§3. No new ADR: ADR-012 already owns
the formula/clamp decision; type/rounding is domain-rule detail owned by
PET_RULES.md (docs/03-decisions/README.md §2/§5 — do not record an
unapproved decision).

Waiting for:
Human gameplay decision on: (1) PetLevelMultiplier numeric type,
(2) its allowed range (including whether values < 1 are legal),
(3) the rounding rule for a non-integer product (or integer-only
configuration), (4) rounding-vs-clamp operation order.
```

Task-specific (continued):

```text
NOT READY — HUMAN GAMEPLAY DECISION REQUIRED

Problem:
<which of Q1–Q4 is undeterminable, in one or two sentences>

Relevant sources:
<file + section, both/all sides if a conflict>

Conflict / missing information:
<the exact undecidable question(s)>

Proposed resolution:
<the smallest documentation change that would close it — no invented
 values>

Waiting for:
<human gameplay decision on the listed question(s)>
```

- If two authoritative documents genuinely disagree about the formula or
  clamp → STOP per `AGENTS.md` §4; do not pick the convenient side
- If resolving the contract would require adding Player combat stats,
  Pet XP, Evolution, or any `MVP_SCOPE.md` §2 item → STOP per §8/§7
- If the change would require code edits to "make the docs true" → STOP;
  this task never edits code
- If an ADR would record a decision not actually approved → STOP per
  `docs/03-decisions/README.md` §5
- If scope grows into combat migration, Pet persistence, or progression
  implementation → decompose; those are separate tasks

---

## Final Report

The executing agent's completion report must contain, in order:

```text
## Objective            — what was resolved, one paragraph
## Current Contract     — the Player.Level ↔ Pet.Level rule as it now
                         stands after this task
## Exact Ambiguity      — which of A1–A5 were open at start
## Authoritative Evidence — docs consulted, by path + section
## Decision Required     — Q1–Q5 answers, each traced to evidence
## Deterministic Rules   — the explicit formula/type/rounding/clamp
                         statements now recorded (or the STOP report)
## MVP Scope            — classification of every value touched
## Documentation Changes — files + sections actually edited (and ones
                         deliberately not edited, with reason)
## Implementation Impact — the section above, confirmed present
## Out of Scope         — confirm nothing out-of-scope was touched
## Stop Conditions      — either "none fired" or the full
                         NOT READY block
## Acceptance Criteria  — each criterion checked with evidence
## Final Report         — this section, complete
```

If any acceptance criterion cannot be checked truthfully, the task is
BLOCKED, not DONE.

---

## Completion Evidence

### Human Decisions Applied (authoritative; not reopened)

```text
PetLevelMultiplier type:  decimal
Allowed range:            > 0   (< 1 legal; zero/negative not)
Rounding:                 floor
Order:                    floor → clamp [1, 50]   (clamp always last)

Canonical rule:
  Pet.Level = clamp(
      floor(Player.Level × PetDefinition.PetLevelMultiplier),
      1,
      50
  )

Concrete MVP multiplier values: DEFERRED to balance/config
  (ROADMAP Phase 3 balance pass on all configurable values;
   XP-curve precedent — MVP_SCOPE.md §1, PET_RULES.md §5 item 3)

Authoritative examples:
  3 × 1.5 = 4.5 → floor 4 → clamp 4
  40 × 2 = 80   → floor 80 → clamp 50
  1 × 0.5 = 0.5 → floor 0  → clamp 1
```

### Changed Files
- `docs/01-game-design/PET_RULES.md` — **primary.** Version 1.3 → 1.4.
  §5 item 1: multi-line canonical formula, 4-step operation order,
  type `decimal`, range `> 0`, floor-before-clamp, "clamp is always the
  last operation", three authoritative worked examples. NEW §5 items
  2–3: one canonical rule for stored `Pet.Level` and
  `BattleState.PetState.Level`; concrete MVP multipliers deferred to
  balance/config. Prior items renumbered 4–10 (no-XP, no-combat-stats,
  stat scaling, element/tier independence, curve deferral, Tier/Star
  independent, initial Player.Level = 1). §5.1 refs updated to items
  1/4/5/9/10; §6 formula updated to floor form.
- `docs/01-game-design/GAME_RULES.md` — §9.3 (line ~150): formula
  restatement → floor form with `decimal > 0` / floor-before-clamp note.
  Version 2.3 → 2.4.
- `docs/00-overview/MVP_SCOPE.md` — §1 Pets formula → floor form; noted
  multipliers deferred. Version 1.1 → 1.2.
- `docs/02-technical/DATABASE.md` — §1 `Pet.Level` description → floor
  + same-rule-as-PetState note; `PetLevelMultiplier` → `decimal > 0`
  with deferral note; §3 added
  `PetDefinition.PetLevelMultiplier > 0 (decimal)`; stale "§5 item 8"
  refs → "§5 item 10". Version 1.3 → 1.4.
- `docs/03-decisions/ADR/ADR-011-player-owner-pet-combat.md` — item 7
  + diagram line: floor form; type/range/order ownership stated as
  PET_RULES §5. No new ADR.
- `docs/03-decisions/ADR/ADR-012-player-level-pet-level-ownership.md` —
  item 3: multi-line floor form; item 4 notes clamp after floor; type /
  range / rounding / ownership pointer to PET_RULES §5. Context line 13
  left as historical narrative (pre-completion formula — not a live
  restatement). No new ADR.

### Deliberately Not Edited
- `docs/00-overview/GDD.md` §6/§14 — already references PET_RULES §5
  without restating the formula.
- `docs/02-technical/GAME_STATE.md` §2.3 — lists PetState fields only;
  coverage of the shared rule is PET_RULES §5 item 2 (canonical owner).
- `docs/00-overview/ROADMAP.md` — Phase 3 balance pass already covers
  deferred multipliers.
- `docs/01-game-design/PET_RULES.md` version-history lines prior 1.2/1.3
  and ADR-012 Context — historical narrative of pre-completion state.
- `tasks/backlog/TASK-023*`, `TASK-024*`, other tasks — out of scope
  (immutable completed history / consumer backlog).
- `src/`, `tests/`, `migrations/` — documentation-only task.

### Validation Results
- Documentation consistency pass — PASS. Greps over `docs/`:
  - No live formula variant without `floor` remains (only historical
    changelog / ADR Context narrative).
  - `floor(Player` appears consistently in PET_RULES §5/§6, GAME_RULES
    §9.3, MVP_SCOPE §1, DATABASE §1, ADR-011 items 7 + diagram,
    ADR-012 item 3.
  - Multiplier `decimal > 0` stated in PET_RULES §5 and DATABASE §3.
  - No invented MVP multiplier values (1.0/1.2/1.5/2.0 as content)
    anywhere outside the three authoritative worked examples.
  - No Pet XP / Evolution / independent Pet Level axis introduced;
    Tier/Star independence reaffirmed (§5 item 9, ADR-012 item 5).
  - Stale "§5 item 8" cross-references corrected to item 10.
- `git status` / `git diff --stat` — only the six listed docs files
  modified; `src/`, `tests/`, `migrations/` unchanged.

### Server Authority & Scope Verification
- [x] Confirmed zero source-code changes
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — formula
      and multiplier config IN; concrete values deferred as balance
      config (not invented); no new systems
- [x] Confirmed no invented balance values or progression mechanics

### Acceptance Criteria
- [x] Player Level [1,50] init 1 persistent no-combat-stats unchanged
- [x] Pet Level derived, range, owner explicit — PET_RULES §5 sole
      canonical owner
- [x] Formula identical across PET_RULES §5 and all live restatements
- [x] Multiplier storage/type/range/deferral explicit
- [x] Rounding explicit: floor
- [x] Clamp explicit: [1,50] after floor; clamp last
- [x] MVP classification explicit: multipliers deferred to balance/config
- [x] No invented progression (no Pet XP, no Evolution, no fabricated
      multiplier numbers)
- [x] Ownership consistent with ADR-011/012 and TASK-023
- [x] Source code modified: NO; completed tasks and TASK-023 untouched
- [x] Implementation Impact present (recorded in Final Report below)
- [x] Docs-only quality items pass; no authoritative conflict introduced
