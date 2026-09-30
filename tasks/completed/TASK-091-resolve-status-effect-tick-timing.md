# TASK-091 — Resolve Status Effect Tick Timing Within the Combat Resolution Order

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section; copies only what the
  executing agent needs to locate, adjudicate, and minimally correct one
  self-contained gameplay-contract ambiguity.

  THIS TASK RESOLVES EXACTLY ONE AMBIGUITY:
  WHERE IN `GAME_RULES.md` §17 does a Status Effect (specifically the Burn
  damage-over-time, and by extension any future DoT) tick?

  THE TASK DOES NOT PRE-SELECT AN OPTION. It documents the candidate
  placements, surveys the authoritative evidence, and either (a) records a
  resolution that the authoritative documentation decisively supports, with
  citations, or (b) explicitly requires a human gameplay/product decision
  between the candidate placements. It must never choose by preference or by
  implementation convenience.

  IT IS A SINGLE-CONTRACT DECISION TASK. It does NOT:
    - define new Status Effect types or a Status Effects system,
    - define Burn magnitude / duration / Element beyond what COMBAT §5 already says,
    - define Boss Skill magnitudes, Power-drain, or ATK-reduction,
    - define any wire / SignalR / event payload for a status tick,
    - change combat semantics beyond the tick's placement in §17.
-->

---

## Metadata

```text
Task ID:           TASK-091
Type:              DOCUMENTATION
Status:            DONE (HUMAN DECISION RECEIVED — product/gameplay owner
                    selected **Option C**: the tick fires at `GAME_RULES.md`
                    §17 step 19 "End Turn" (sub-step 19a), with the single MVP
                    rule "one tick per resolved Turn". Applied to
                    `GAME_RULES.md §17` and `COMBAT_RULES.md §5.1`. See §13.)
Risk:              MEDIUM (TASK_TYPES.md §4: DOCUMENTATION is MEDIUM when it
                    "affects a cross-referenced contract". This timing sits in
                    `GAME_RULES.md §17`, the canonical resolution order that
                    `GAME_EVENTS.md` §1 and the Damage Pipeline reference, and
                    it gates Burn/DoT implementation — but no value, mechanic,
                    wire payload, or code changes, so not HIGH)
Priority:          HIGH (every Status Effect / DoT implementation task is
                    blocked on the tick's §17 position; deterministic
                    StatusEffects processing cannot be specified without it)
Primary Agent:     review (documentation consistency — TASK_TYPES.md §2 sets
                    DOCUMENTATION's Primary Agent to "Review Agent")
Supporting Agents: gameplay (Burn/DoT semantics accuracy against
                    COMBAT_RULES.md §5 — the placement must not alter
                    magnitude, Element, or duration),
                    backend (§17 is the order the server resolution pipeline
                    implements — the chosen step must be implementable there
                    without reordering existing steps)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                    discovery/impact-analysis,
                    quality/documentation-consistency,
                    quality/scope-validation
                    (4 skills — Normal budget, tasks/README.md §12)
Dependencies:      None (self-contained contract ambiguity; read-only inputs
                    listed in §2)
Blocks:            Any future task that implements Burn or a general
                    StatusEffects tick (no such task currently exists — this
                    task creates none)
Estimate:          Normal (4 skills; documentation-only; one ambiguity, one
                    decision, at most two documents touched, zero code)
```

**Type classification note.** `DOCUMENTATION`, per `tasks/TASK_TYPES.md` §2:
the primary output is a change to `docs/` itself. Not `FEATURE` (nothing is
being built), not `BUG` (no code misbehaves — the rules simply do not yet pin
the tick to a step), not `GAMEPLAY-CHANGE` (no rule *value* changes; §5 and
§9 freeze every magnitude/Element/duration — this task places an existing,
already-documented tick on the timeline, it does not create a new mechanic).
The precedent of contract-formalization tasks TASK-019 / TASK-046 / TASK-050 /
TASK-068 as `DOCUMENTATION` governs here.

**Decision note.** This is a **contract clarification**, not a design task. Its
whole purpose is to make the Burn/DoT tick deterministic. It must not broaden
itself, and it must not silently pick a placement: see §5.

---

## 1. Objective

Resolve exactly one ambiguity: the **position within `GAME_RULES.md` §17
(Event Resolution Rules) at which a Status Effect tick occurs**.

Concretely, the tick (the Burn damage-over-time defined in
`COMBAT_RULES.md` §5.1, and by extension any future DoT) must be anchored to
**one explicit, numbered step** in the fixed §17 order, and the MVP Turn
relationship must be stated as a single deterministic rule (one tick point per
resolved Turn).

After this task, **exactly one** tick placement is authoritative:

- a reader of `GAME_RULES.md §17` can see the tick as an explicit numbered
  sub-step (or an explicitly-expanded named step) in the canonical order;
- `COMBAT_RULES.md §5.1` no longer carries the ambiguous
  "each Turn (or configured interval)" wording — it states the single MVP
  tick point and cross-references the owning §17 step;
- every existing §17 step keeps its current relative order (no reordering);
- no Status Effect value, Element, duration, Boss Skill magnitude, or wire
  payload is changed.

> **Final rule for the executing agent:** this task exists only to place the
> existing, already-documented tick on the §17 timeline. Do not broaden it. Do
> not define new status types. Do not define Burn magnitude/duration. Do not
> invent a placement that the authoritative docs do not support. If the docs do
> not pick one placement, STOP and request the product/gameplay decision (§11).

---

## 2. Authoritative References

**Documents to inspect (only these, per the generating request):**

```text
docs/01-game-design/GAME_RULES.md
  §17 (Event Resolution Rules) — the fixed 19-step order (lines 299–350).
    - step 14 "Resolve Player Effects" (line 318)
    - step 18 "Resolve Boss Response" + 18a/18b/18c expansion (lines 326–347)
    - step 19 "End Turn" (line 323)
  §2 (Turn Rules) — what a Turn is (one player Swap/Action)

docs/01-game-design/COMBAT_RULES.md
  §5.1 (line 205) — "Burn ... ticks each Turn (or configured interval)"
  §5.2 item 1 (line 214) — "duration (in Turns)" or trigger-based expiry
  §5.2 item 3 (lines 221–224) — DoT ticks go through the Damage Pipeline,
      Element = the Effect's own Element, Combo = 1 / neutral
  §3 / §3.1 — the Damage Pipeline order the tick must respect

docs/02-technical/GAME_STATE.md
  §2.4.3 (lines 1127–1145) — SkillCharge / SkillCooldown; the only documented
      "Turn increment" tick point, scoped to the boss skill cooldown counter

docs/02-technical/GAME_EVENTS.md
  §1 item 4 (lines 121–123) — TurnStarted/TurnEnded bracket the whole
      resolution; events emit in §17 order
  §2 TurnStarted/TurnEnded (lines 252–262) — Turn begins on commit; TurnEnded
      follows the last Cascade

docs/01-game-design/CARD_RULES.md
  §6 (lines 187–189) — CardCast/PetSkillCast "fire at ... step 14
      'Resolve Player Effects'"

docs/01-game-design/MATCH3_RULES.md
  §8.1 (lines 2789–2810, esp. item 5: "A Card cast begins no Turn")
  §8.3 (line 2843) — "4. Begin the Turn (§8.1) – Turn takes effect here"

docs/01-game-design/RELIC_RULES.md
  §3 (lines 327–328) — OnTurnStart / OnTurnEnd trigger definitions

docs/01-game-design/BOSS_RULES.md
  §6.3 (lines 201–213) — Skill Cooldown "Decrements by 1 at each Turn
      increment"
```

**Supporting (read-only, to confirm step-14's current meaning):**

```text
tasks/completed/TASK-019-resolve-player-effects-healing.md —
  established that §17 step 14 "Resolve Player Effects" is, for now, the
  consumption of the transient HealPool into active-Pet HP.
```

**ADR check:** No ADR addresses Status Effect / DoT tick timing. `docs/03-
decisions/ADR/` was searched for `Burn` / `Status Effect` / `resolution order`;
the only hits are ADR-001 (references §17 as the pipeline), ADR-004 (ordered
delivery), and ADR-008 (recovery) — none places the tick.

---

## 3. Current State — The Ambiguity (verified at task creation)

The rules give the tick a **frequency** but not a **position**:

1. `COMBAT_RULES.md §5.1` (line 205) says Burn "ticks each Turn **(or
   configured interval)**". This is a frequency, not a step in §17, and the
   "(or configured interval)" alternative is defined nowhere in `docs/` — so
   even the MVP rule is not a single deterministic value yet.
2. `GAME_RULES.md §17` (lines 304–324) lists 19 fixed steps. **None of them
   is a Status Effect tick.** Step 14 "Resolve Player Effects" (line 318) is
   the nearest named candidate, but it is already specifically the healing
   step (TASK-019) and the CardCast emission anchor (CARD_RULES §6:187-189).
   Step 19 "End Turn" (line 323) is not expanded anywhere.
3. The only documented "Turn increment" tick point is the **boss skill
   cooldown**: `GAME_STATE.md §2.4.3` (1135–1138) and `BOSS_RULES.md §6.3`
   (209–213) — "decrements by 1 at each Turn increment (`MATCH3_RULES.md`
   §8.1)". That rule is scoped to `BossState.SkillCooldown`, not to Status
   Effects. `RELIC_RULES.md §3` (327–328) adds OnTurnStart/OnTurnEnd as *relic
   triggers*. None of these states that a Status Effect ticks at Turn increment
   or at Turn end.

Because §17 says domain docs "may expand individual steps but must not reorder
them" (line 301–302), the tick must either (a) be a new numbered step added to
the canonical list, or (b) be an explicit sub-step folded into an existing
named step. Neither has been chosen.

**Net effect:** three materially-different placements remain equally valid and
produce different gameplay (see §4). This is a §20 stop condition ("multiple
placements remain equally valid"; "choosing a placement requires a new
gameplay/product decision").

---

## 4. Required Decision — Option A / B / C

The single decision: **at which §17 step does the Status Effect tick occur?**
The three candidates, with their concrete gameplay consequences:

### Option A — Tick at Turn start ("Turn increment" / "Begin the Turn")

- Placement: the tick fires when the Turn takes effect — the point
  `MATCH3_RULES.md §8.3` step 4 ("Begin the Turn – Turn takes effect here",
  line 2843) and `GAME_STATE.md §2.4.3`'s "Turn increment" both point at.
- Ordering: tick precedes the board/combat steps of this Turn (i.e. before the
  player's own damage at §17 step 15 and before the Boss Response at step 18).
- Consequence: a Burn **applied by the Boss during this Turn (step 18b) does
  NOT tick this Turn** — it ticks at the start of the *next* Turn.
- Precedent weight: SkillCooldown decrements "at each Turn increment";
  OnTurnStart "fires at the start of a Turn." (Both are counter/relic rules,
  not status rules — the analogy is the only support.)

### Option B — Tick at §17 step 14 "Resolve Player Effects"

- Placement: the tick is an explicit sub-step of step 14, after resources/Power
  (steps 12–13) and before the player's damage calculation (step 15).
- Ordering: Burn damages the active Pet before that Pet's own damage for this
  Turn is dealt; a Burn applied this Turn (step 18) ticks next Turn.
- Consequence: "next-turn" tick, but positioned in the middle of the resolution
  as a *player-side effect*.
- Precedent weight: step 14 is the designated "player effects" step — it is the
  healing step (TASK-019) and the CardCast emission point (CARD_RULES
  §6:187-189). The tick would be a *new* sub-step under an already-busy step.

### Option C — Tick at §17 step 19 "End Turn"

- Placement: the tick is an explicit sub-step of step 19, after the Boss
  Response (step 18) and before `TurnEnded`.
- Ordering: the tick is the last combat effect of the Turn.
- Consequence: a Burn **applied by the Boss this Turn (step 18b) ticks the SAME
  Turn it is applied** — "same-turn" tick.
- Precedent weight: "ticks each Turn" reads most naturally as a turn boundary;
  OnTurnEnd "fires at the end of a Turn" (RELIC_RULES §3:328).

The decision also fixes the shared sub-rule for all three: **"each Turn" means
exactly one tick at that one step in every resolved Turn** — and the "(or
configured interval)" wording is removed for MVP (no configurable interval is
defined anywhere in `docs/`).

---

## 5. Decision Process & Critical Rules

### 5.1 The decision is a gameplay/product decision
The tick's position determines (a) whether a freshly-applied Burn ticks on the
turn it is applied or the next, and (b) the tick's ordering relative to the
player's own damage and the Boss's response. Both are gameplay semantics, not
document typos. This is owned by `GAME_RULES.md` §17 (the canonical order) with
`COMBAT_RULES.md` §5 as the domain owner of the tick.

### 5.2 Evidence-first adjudication
Every candidate above must be traceable to the citations in §2. If, on
re-reading, a placement is **decisively** supported by the docs (e.g. a
sentence that explicitly says the DoT ticks at step N), record that resolution
with citations and proceed to §8. Absent such a sentence, STOP (§11).

### 5.3 Critical rules — do NOT choose based on
- **Implementation convenience.** Where it is easiest to insert a hook in
  `SwapExecutor`/the pipeline is NOT a gameplay reason.
- **The SkillCooldown precedent.** `GAME_STATE.md §2.4.3` / `BOSS_RULES.md
  §6.3` decrement a *boss skill cooldown* at Turn increment. That is a specific
  counter rule. Generalizing it to Status Effects invents a rule not in the
  docs.
- **"It's a player effect, so step 14."** Step 14 is already the healing step
  and the CardCast emission anchor; assigning it to Burn by name alone is an
  inference, not a citation.
- **A5.4 / preference.** "I think Burn should tick at the end" is not a reason.

If none of the three is decisively documented, the task is a human-decision
task, and the executing agent records that in §11 and halts.

---

## 6. Scope

### In Scope
- Determining whether the tick position is already uniquely implied by §2.
- If and only if decisively determined: adding the tick to `GAME_RULES.md`
  §17 as an explicit numbered sub-step (or expanding the chosen named step),
  preserving all other step order.
- If and only if the decision is applied: correcting `COMBAT_RULES.md` §5.1
  to state the single MVP tick point and cross-reference the owning §17 step
  (removing the ambiguous "(or configured interval)").
- Recording the decision (or the required human decision) in this task.

### Out of Scope
- Defining any new Status Effect type, or a StatusEffects data structure
  (`GAME_STATE.md` §2.3 / §2.4 `StatusEffects[]` stay "not yet implemented").
- Burn magnitude, duration, or Element beyond what `COMBAT_RULES.md` §5 already
  states.
- Boss Skill effect magnitudes (Power drain amount, ATK-reduction amount, Burn
  application on Flame Burst).
- Always-active heal-reduction representation (Thủy Ma).
- Any wire / SignalR / event payload for a status tick; any new `GAME_EVENTS.md`
  event.
- Pet HP / Boss HP / Power wire delivery.
- `CardCast` / `PetSkillCast` wire schema.
- `GET /api/battle/history`, the BattleResult client trigger, the TDD §2.1
  staging note.
- Any source code.
- Any item listed as OUT in `MVP_SCOPE.md` §2.

---

## 7. Documentation Requirements (applies ONLY if §5.2 finds a decisive placement)

Edit the smallest authoritative source, no duplication
(`.ai/workflow/documentation/documentation-change.md` §2):

1. `docs/01-game-design/GAME_RULES.md` §17 — the canonical owner of the order.
   Add the tick as an explicit numbered sub-step at the chosen position, or
   expand the chosen named step with a sub-step (the way step 18 already
   expands to 18a/18b/18c). Do not reorder steps 1–19.
2. `docs/01-game-design/COMBAT_RULES.md` §5.1 — replace "ticks each Turn
   (or configured interval)" with the single MVP rule and a cross-reference to
   the owning §17 step. This removes the ambiguity the task targets.

Do **not** touch `GAME_STATE.md`, `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`,
`API_CONTRACTS.md`, `REDIS_STATE.md`, or any ADR unless the chosen placement
materially changes one of them — and if it does, that is a §20 STOP (report),
not an inline edit, because this task is timing-only.

---

## 8. Acceptance Criteria

- [ ] `GAME_RULES.md §17` explicitly defines the Status Effect tick timing as
      an explicit numbered step / sub-step.
- [ ] The timing has exactly one deterministic position in the resolution order.
- [ ] The MVP Turn relationship is unambiguous (one tick per resolved Turn).
- [ ] `COMBAT_RULES.md` §5.1 no longer uses the ambiguous configurable-interval
      wording; it states the single MVP rule and cross-references §17.
- [ ] No unrelated gameplay rule or value is changed.
- [ ] No source code is modified.
- [ ] No SignalR / API / Redis contract is modified.
- [ ] No new gameplay mechanic (status type, magnitude, duration) is introduced.
- [ ] `GAME_RULES.md §17`, `COMBAT_RULES.md §5`, and `GAME_EVENTS.md §1`
      remain internally consistent (the tick's events, if any, still line up
      with §17 order).
- [ ] Relevant documentation is reviewed together (re-read the edited doc and
      its referencing docs).

> If the task ends in §11 (human decision required), the applicable acceptance
> criteria are: the three options are documented, the evidence is cited, no
> rule doc is edited, and the exact unresolved choice is identified.

---

## 9. Testing Requirements / Verification

Documentation-only; no unit/integration tests exist for a doc placement.
Verification is consistency:

```text
[ ] Re-read GAME_RULES.md §17 and confirm the tick is a single numbered
    sub-step and that steps 1–19 order is unchanged.
[ ] Re-read COMBAT_RULES.md §5 and confirm §5.1 cross-references the §17
    step and no "(or configured interval)" remains.
[ ] Confirm GAME_EVENTS.md §1 (event ordering) still holds: if the tick emits
    Damage events, they appear at the §17 step chosen (COMBAT §5.2 item 3).
```

---

## 10. Stop Conditions

Universal AGENTS.md §20 stops always apply. Task-specific:

- If `GAME_RULES.md §17` plus the ADRs do **not** determine the tick's
  position (which is the verified state at creation — §3): STOP.
- If more than one of Option A / B / C remains equally valid: STOP.
- If choosing a placement requires inventing a rule not in §2 (e.g. generalizing
  the SkillCooldown "Turn increment" precedent to Status Effects): STOP.
- If the decision would require changing combat semantics **beyond timing**
  (a new status type, a magnitude, a wire payload): STOP — that is a separate
  task.
- If the chosen placement would force a change to `GAME_STATE.md`,
  `SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`, or `REDIS_STATE.md`: STOP and
  report; this task is timing-only.

---

## 11. STOP CONDITION REPORT (HUMAN DECISION REQUIRED)

**Status at task creation: NOT DETERMINABLE from authoritative documentation.**

The authoritative docs fix the tick's *frequency* ("each Turn", `COMBAT_RULES.md
§5.1`) and the *pipeline* the tick runs through (the Damage Pipeline,
`COMBAT_RULES.md §5.2 item 3`, Combo = 1), but they do **not** assign the tick
to any step of `GAME_RULES.md` §17. Three materially-different placements
remain equally valid (§4):

```text
Option A  Tick at Turn start ("Turn increment" / "Begin the Turn")
          -> next-turn tick; precedes player damage and boss response.
Option B  Tick at §17 step 14 "Resolve Player Effects"
          -> next-turn tick; mid-resolution, a new sub-step on a busy step.
Option C  Tick at §17 step 19 "End Turn"
          -> same-turn tick; last combat effect, after the boss response.
```

The load-bearing ambiguity, in one question:

> **Does a Burn applied by the Boss during a Turn tick on that same Turn
> (Option C), or on the next Turn (Option A or B)? And at what point relative
> to the player's own damage and the Boss's response does the tick fire?**

No citation in §2 answers that. The SkillCooldown "Turn increment" precedent
(`GAME_STATE.md §2.4.3`, `BOSS_RULES.md §6.3`) is scoped to a counter, not to
Status Effects, and must not be read as deciding Burn.

**Decision required from the product/gameplay owner:** choose Option A, B, or C
(and confirm the single-MVP rule: one tick per resolved Turn, no configurable
interval). On receipt, apply §7 (edit `GAME_RULES.md §17` +
`COMBAT_RULES.md §5.1`), run §9 verification, and complete the task per §8.

---

## 12. Status Transition & Next Step

- Created: `BACKLOG` in `tasks/backlog/`.
- Executing agent's first action: confirm §3's "not determinable" finding still
  holds (re-read §2). If a decisive placement is found, apply §7 and move to
  `IN REVIEW` → `DONE`. If not (the expected case), record §11 and hold for the
  product/gameplay decision; on decision, apply §7, run §9, and mark `DONE`.
- This task does **not** create any implementation task and does **not** create
  TASK-092. It unblocks whichever future task implements Burn/DoT.

---

## 13. Completion Evidence — Final Report

### Decision
- **Option C** — the Status Effect (Burn/DoT) ticks at `GAME_RULES.md §17`
  step 19 "End Turn" (sub-step `19a`): the last combat effect of the Turn,
  after the Boss Response (step 18). A Boss-applied Burn therefore ticks the
  **same Turn** it is applied. Single MVP rule: **one tick per resolved Turn**
  (no configurable interval). Confirmed by the product/gameplay owner.

### Changed Files
- `docs/01-game-design/GAME_RULES.md` §17 — added `Step 19 ("End Turn")
  expands to: 19a. Tick Status Effects` (a DoT tick once per resolved Turn,
  through the Damage Pipeline per `COMBAT_RULES.md` §3 / §5.2 item 3). Steps
  1–19 order and the step-18 expansion (18a/18b/18c) are unchanged.
- `docs/01-game-design/COMBAT_RULES.md` §5.1 — Burn now reads "ticks once per
  resolved Turn at End Turn (`GAME_RULES.md §17` step 19a)"; the ambiguous
  "(or configured interval)" wording is removed.

### Validation Results
- Re-read `GAME_RULES.md §17`: `19a` is a single numbered sub-step; steps 1–19
  and the 18a/18b/18c expansion are unchanged; no reordering.
- Re-read `COMBAT_RULES.md §5`: §5.1 cross-references §17 step 19a; a
  docs-wide grep for "configured interval" / "ticks each Turn" is clean.
- `GAME_EVENTS.md §1` (events follow §17 order) still holds — the tick's
  damage events, if emitted, fall at step 19a per `COMBAT_RULES.md` §5.2 item
  3; no §1/§2 edit required for this timing-only task.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic (doc-only)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Status Effects are
      IN scope; no OUT/FUTURE system introduced)
- [x] Confirmed no Status Effect value / Boss Skill magnitude / wire payload
      changed
