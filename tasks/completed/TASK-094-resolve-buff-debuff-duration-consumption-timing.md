# TASK-094 — Resolve Buff/Debuff Status Effect Duration Consumption Timing

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK OBTAINS EXACTLY ONE GAMEPLAY/PRODUCT DECISION:
  For a non-DoT Buff/Debuff Status Effect with duration N Turns, at what
  exact point is one Turn of duration consumed, relative to application
  and `GAME_RULES.md` §17 step 19a?

  IT IS A DECISION-INPUT TASK. It does NOT implement code, does NOT define
  the `StatusEffects[]` schema, and does NOT pre-select an answer. See §4.
-->

---

## Metadata

```text
Task ID:           TASK-094
Type:              GAMEPLAY-CHANGE
Status:            READY (decision complete — DR1–DR6 recorded in the
                   "Decision Record" section. Decision-input only: no
                   authoritative documentation was modified by this task.
                   Handoff to TASK-093 is complete-but-unapplied; applying
                   DR1–DR6 to `COMBAT_RULES.md` §5.2 is the downstream act.)
Risk:              MEDIUM (TASK_TYPES.md §4: GAMEPLAY-CHANGE baseline is HIGH,
                    but this task records no rule itself — it obtains the
                    decision. Risk is classified at the higher end of the
                    DOCUMENTATION band: it affects a cross-referenced contract,
                    `COMBAT_RULES.md` §5.2, which TASK-093 is blocked on. The
                    HIGH-risk GAMEPLAY-CHANGE execution applies to whichever
                    task applies the decision, not to this decision-input task.)
Priority:          HIGH (TASK-093 is BLOCKED on this decision; Status Effects
                    and Boss Skill secondary-effect implementation cannot
                    proceed until duration semantics are deterministic)
Primary Agent:     gameplay (owns `COMBAT_RULES.md` §5 — TASK_TYPES.md §5,
                    Domain x Type matrix: Combat -> Gameplay)
Supporting Agents: review (documentation consistency, and the decision must be
                    recorded against the canonical owner document)
Workflow:          development/gameplay-change.md
Skills:            gameplay/gameplay-behavior-derivation,
                   discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/scope-validation
                   (4 skills — Simple/Normal budget, tasks/README.md §12)
Dependencies:      TASK-093 (BLOCKED on this decision), TASK-091 (tick timing
                   at §17 step 19a — input, not modified), TASK-092
                   (Root magnitude/duration — input, not modified)
Blocks:            TASK-093 (StatusEffects[] Battle State contract)
Estimate:          Simple (documentation/decision only, one question, zero code)
```

**Type classification note.** `GAMEPLAY-CHANGE` because resolving this
question introduces a gameplay rule that does not currently exist in `docs/`
(`TASK_TYPES.md` §2: GAMEPLAY-CHANGE covers "a new undocumented mechanic is
being authorized"). It is not `DOCUMENTATION`: DOCUMENTATION is for
correcting or adding documentation of an already-decided contract, whereas
here the rule itself must be decided by the product/gameplay owner first
(`AGENTS.md` §7 — a missing rule is a stop condition, not an edit).

**Decision-input note.** This task's only output is the recorded decision.
Applying it to authoritative documentation is a separate act — see §8.

---

## Objective

Obtain and record the single gameplay/product decision that makes non-DoT
Buff/Debuff Status Effect duration deterministic: **at what exact point one
Turn of duration is consumed**, relative to Status Effect application and the
authoritative End Turn Status Effect timing in `GAME_RULES.md` §17 step 19a.

This task resolves **timing only**. It does not define the `StatusEffects[]`
state schema, does not define magnitudes, does not change Burn, and does not
implement anything.

> **Final rule for the executing agent:** present the ambiguity neutrally,
> obtain the owner's explicit selection, and record it. Do not choose an
> option. Do not narrow the options to one. If the owner's answer leaves any
> item in §5 undetermined, record which items remain undetermined and keep the
> task BLOCKED. Do not infer an answer from implementation convenience, from
> existing code, from Burn's behavior, from `BossState.SkillCooldown`'s
> behavior, or from common RPG conventions.

---

## Decision Required

The single question:

> **For a non-DoT Buff/Debuff Status Effect with duration N Turns, at what
> exact point is one Turn of duration consumed, relative to Status Effect
> application and `GAME_RULES.md` §17 step 19a?**

This is a gameplay/product decision. It determines when an effect with a given
duration stops applying, and therefore what "2 Turns" means in play. It is not
a documentation typo and not an implementation detail: no citation in §3
answers it (§7).

---

## Authoritative References

```text
docs/00-overview/MVP_SCOPE.md §1 — Status Effects are IN scope (Combat block)
docs/01-game-design/COMBAT_RULES.md
  §5.1 — MVP Status Effect list; Buff/Debuff = "temporary stat modification
         (ATK/DEF/Crit/etc.), with duration measured in Turns unless stated
         otherwise"
  §5.2 item 1 — "Every Status Effect has a source, a magnitude, and a duration
         (in Turns) or a trigger-based expiry"  ← requires a duration, does
         not state when it is consumed
  §5.2 item 2 — stacking is per-effect; MVP default "refresh duration, do not
         stack magnitude"  ← DISPUTED? No. This is settled; see §7
  §5.2 item 3 — DoT ticks go through the Damage Pipeline with Combo = 1
docs/01-game-design/GAME_RULES.md
  §2 — Turn Rules (a Turn = one player Swap/Action). NOTE: §17 step 19a is
         named for damage-over-time Status Effects.
  §17 step 19a — "Tick Status Effects" — the authoritative End Turn Status
         Effect timing, established by TASK-091: one tick per resolved Turn
docs/01-game-design/BOSS_RULES.md
  §6.3.1 item 1 — Burn: tick schedule fully specified (tick #1 at Turn N step
         19a, tick #2 at Turn N+1 step 19a, expires before Turn N+2)
  §6.3.1 item 3 — Root: "-30% Pet ATK debuff", "Root Duration: 2 Turns using
         the authoritative Turn model"  ← magnitude and duration stated; the
         consumption point is NOT stated
  §6.3 — "Decrements by 1 at each Turn increment" — scoped to
         BossState.SkillCooldown ONLY
docs/02-technical/GAME_STATE.md
  §2.3 / §2.4 — StatusEffects[] recorded as "not yet implemented"; no schema
  §2.4.3 — SkillCooldown decrement, scoped to SkillCooldown
  §5.1 — single post-resolution write-back
docs/03-decisions/ADR/ADR-001 — server authority
```

Referenced for context only — **not modified by this task**:

```text
tasks/blocked/TASK-093-resolve-status-effects-battle-state-contract.md §11
  — the STOP report that identified this exact decision as the blocker
tasks/completed/TASK-091-resolve-status-effect-tick-timing.md
  — established §17 step 19a tick timing; §5.3 forbids generalizing the
    SkillCooldown decrement precedent to Status Effects
tasks/completed/TASK-092-resolve-boss-skill-effect-magnitudes.md
  — established Root's magnitude and "2 Turns" duration
```

**ADR check:** no ADR addresses Status Effect duration consumption.

---

## Current Evidence

Verified against the current text of each document (not summaries):

1. **Burn's timing is fully determined.** `BOSS_RULES.md` §6.3.1 item 1 gives
   Burn an explicit tick schedule and an explicit expiry point. Burn is a
   damage-over-time effect and `GAME_RULES.md` §17 step 19a is scoped to
   damage-over-time Status Effects. Burn is therefore **not** the ambiguous
   case and must not be used to answer this question by analogy.

2. **Root's duration consumption point is not determined.** `BOSS_RULES.md`
   §6.3.1 item 3 states Root's magnitude (`-30% Pet ATK`) and its duration
   ("2 Turns using the authoritative Turn model"), but states no tick, no
   consumption point, and no expiry point. `COMBAT_RULES.md` §5.2 item 1
   requires a duration to exist without stating when one Turn of it is
   consumed, and §5.1 defines Buff/Debuff duration as "measured in Turns
   unless stated otherwise" — "otherwise" is stated nowhere for Root.

3. **The nearest Turn-decrement precedent is out of bounds.**
   `BossState.SkillCooldown` decrements "by 1 at each Turn increment"
   (`BOSS_RULES.md` §6.3, `GAME_STATE.md` §2.4.3), but that rule is scoped to
   a Boss Skill counter. TASK-091 §5.3 states explicitly that generalizing it
   to Status Effects "invents a rule not in the docs."

4. **Reapplication is settled and is not part of this decision.**
   `COMBAT_RULES.md` §5.2 item 2 fixes reapplication as refresh-duration,
   do-not-stack-magnitude. This task does not reopen it; §5 item 6 asks only
   how the refreshed duration is then consumed.

5. **Two materially different semantics are both consistent with the text**
   and produce different gameplay — see §4.

**Consequence:** the consumption point cannot be derived from `docs/`. It is
an `AGENTS.md` §20 "missing rule" / "ambiguous requirement" stop condition,
and is the precise blocker recorded in TASK-093 §11.

---

## Decision Options

Presented **neutrally**. These are candidate semantics, not a recommendation.
The owner may select one, may combine them, or may define a third — provided
every item in §5 is answered.

### Option A — Consume at the End Turn Status Effect timing, counting the application Turn

A newly applied Buff/Debuff is active for the remainder of the Turn it is
applied in, and one duration unit is consumed at the authoritative End Turn
Status Effect timing (`GAME_RULES.md` §17 step 19a).

```text
Turn N
  Root applied (step 18b)
  Root is active during Turn N
  End Turn step 19a -> consumes 1 duration unit

Turn N+1
  Root remains active
  End Turn step 19a -> consumes the final duration unit

Before Turn N+2
  Root expires
```

- Effect with `duration = 2` is active during Turns N and N+1.
- Effect with `duration = 1` is active during Turn N only.
- The application Turn counts as the first Turn of the duration.

### Option B — Consume at a separately defined Turn boundary, not counting the application Turn

Duration is consumed at a Turn increment/boundary point that the decision must
name explicitly. A newly applied effect does not lose a duration unit merely
because it was applied during the current Turn.

```text
Turn N
  Root applied (step 18b)
  Root is active during Turn N
  (no duration consumed for having been applied this Turn)

Turn N+1 begins
  Turn boundary -> consumes 1 duration unit
  Root remains active during Turn N+1

Turn N+2 begins
  Turn boundary -> consumes the final duration unit
  Root expires
```

- Effect with `duration = 2` is active during Turns N, N+1 and N+2.
- Effect with `duration = 1` is active during Turns N and N+1.
- The application Turn does not count as a Turn of the duration.

> **Note on Option B:** the exact authoritative timing point (a Turn
> increment, a Turn boundary, or another named point in the resolution order)
> **must be named by the decision**. Selecting Option B without naming the
> point leaves this task's question unanswered and the task BLOCKED.

### Option C — A third semantics defined by the owner

If the owner identifies materially different semantics supported by the
documentation, or prefers different semantics, the decision may define them —
provided every item in §5 is answered explicitly. Do not invent additional
alternatives merely to lengthen this list.

### Interaction to be decided alongside the selection

Whichever option is chosen, the decision must state how consumption interacts
with `GAME_RULES.md` §17 step 19a, given that step 19a is scoped to
damage-over-time Status Effects and a Buff/Debuff is not one. Either:

```text
(i)  Buff/Debuff duration is consumed at the same step 19a point
     (which then means step 19a also handles non-DoT duration), or
(ii) Buff/Debuff duration is consumed at a different named point.
```

---

## Required Decision Coverage

The recorded decision must be sufficient to define deterministic behavior for
all of the following. Each item must be answered explicitly; "unspecified"
counts as unanswered.

```text
1.  Initial application of a Buff/Debuff — when does it begin applying.
2.  When exactly one duration unit is consumed (the named timing point).
3.  Whether an effect applied during Turn N is active during Turn N.
4.  When an effect with duration = 1 expires (name the Turn, relative to
    application).
5.  When an effect with duration = 2 expires (name the Turn, relative to
    application).
6.  Reapplication before expiry — how the refreshed duration is then consumed.
    (Reapplication itself = refresh duration, do not stack magnitude,
     `COMBAT_RULES.md` §5.2 item 2; this item asks only how the refreshed
     duration is consumed. Not reopened.)
7.  Reapplication during the same Turn the effect was applied.
8.  Interaction with `GAME_RULES.md` §17 step 19a (see §4's interaction note).
9.  Whether the rule applies to all duration-based Buff/Debuff Status Effects,
    or only to Root (and if only Root, what governs the others).
```

This task does **not** ask for, and must not record, actual gameplay values,
magnitudes, or the `StatusEffects[]` schema.

---

## Scope

### In Scope

- Presenting the ambiguity neutrally, with the citations in §3 and evidence in
  §7.
- Obtaining the product/gameplay owner's explicit selection.
- Recording the selection in this task file, covering every item in §5.
- Identifying the canonical owner document for the decision
  (`COMBAT_RULES.md` §5.2) and confirming the recording workflow.

### Out of Scope

- Modifying `docs/02-technical/GAME_STATE.md` (including adding a §2.5
  heading — the §2.4 → §2.6 gap is intentional and must not be filled).
- Defining the `StatusEffects[]` schema, its fields, types, or serialization.
- Implementing Status Effects, Root, Burn, damage, or Boss Skills in `src/`.
- Modifying `tasks/blocked/TASK-093-*.md`, `TASK-091`, `TASK-092`,
  `TASK-079`, or any other existing task.
- Modifying `SIGNALR_PROTOCOL.md`, `GAME_EVENTS.md`, `API_CONTRACTS.md`,
  `REDIS_STATE.md`, `DATABASE.md`, or any ADR.
- Modifying Burn's tick timing or magnitude, Root's magnitude, or Drain
  Power's classification.
- Reopening the reapplication/stacking rule (`COMBAT_RULES.md` §5.2 item 2).
- Creating implementation tasks for Status Effects.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [x] A gameplay/product owner has explicitly selected the duration-consumption semantics. (Option C, §4; DR1–DR6)
- [x] The decision defines when a newly applied Buff/Debuff begins consuming duration. (DR1, DR3)
- [x] The decision defines the exact duration-consumption timing point (named, not described as "per Turn"). (DR2 — §17 step 19a)
- [x] The decision defines the behavior of duration `1` (name the expiry Turn relative to application). (DR5 + worked example)
- [x] The decision defines the behavior of duration `2` (name the expiry Turn relative to application). (DR5 + worked example)
- [x] The decision defines reapplication behavior for the refreshed duration. (DR3, DR4)
- [x] The decision defines same-Turn reapplication behavior. (DR4)
- [x] The decision states whether the rule applies to all duration-based Buff/Debuff Status Effects. (§5 item 9)
- [x] The decision states its interaction with `GAME_RULES.md` §17 step 19a. (§5 item 8, item (i); DR6)
- [x] The decision is completely recorded in TASK-094 with all required
    decision coverage.
- [x] The decision is explicitly identified as input for TASK-093.
- [x] No authoritative documentation was modified by TASK-094.
- [x] Every item in §5 (Required Decision Coverage) is answered; none is left unspecified. (9 of 9)
- [x] No source code was modified (`src/` and `tests/` untouched).
- [x] No authoritative gameplay/technical contract was silently changed.
- [x] TASK-093 remains unchanged.

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)
[ ] src/frontend/client/ (scenes / runtime / services / state / ui)
[ ] tests/ (unit / integration / gameplay scenarios)
[ ] docs/ (documentation updates if applicable — see §8)
[x] tasks/ (this task file only)
```

---

## Implementation Notes

- Canonical domain owner for Status Effect rules is `docs/01-game-design/COMBAT_RULES.md` §5 — that document owns any new Status Effect duration rule.
- The End Turn Status Effect timing is owned by `docs/01-game-design/GAME_RULES.md` §17 step 19a (TASK-091). This task must not change it.
- Root's magnitude and duration are owned by `docs/01-game-design/BOSS_RULES.md` §6.3.1 item 3. This task must not change them.
- The blocking report this task answers is `tasks/blocked/TASK-093-resolve-status-effects-battle-state-contract.md` §11.
- This task creates no follow-up task. TASK-093 resumes once the decision is recorded.

---

## Testing Requirements

### Required Verification

```text
[ ] Evidence audit confirms the decision is consistent with the cited
    existing rules and does not contradict TASK-091 or TASK-092.

[ ] Confirm the decision is complete enough for TASK-093 to consume.

[ ] Confirm no authoritative documentation was modified.
[ ] Scope validation against MVP_SCOPE.md §1
[ ] Confirm no existing task was modified (TASK-093/091/092/079)
[ ] Confirm no source code changed
```

### Key Edge Cases

- Duration `1` — the single most ambiguous case; its expiry Turn differs between the options in §4.
- Effect applied and re-applied within the same Turn.
- Effect applied during Turn N step 18b, needing to state whether Turn N counts.

---

## Stop Conditions

Universal `AGENTS.md` §20 stops always apply. Task-specific:

- If the current task-generation workflow does not permit decision-input tasks: STOP and report.
- If the lifecycle requires a different status or folder for an unanswered product decision: STOP and report instead of inventing a state.
- If the repository already contains an equivalent unresolved decision task: STOP and report the duplicate.
- If the authoritative documentation has already explicitly resolved this exact question: STOP and report the citation.
- If answering the question would require a broader gameplay decision than Status Effect duration timing: STOP and report the broader decision needed.
- If the owner's answer leaves any item in §5 unspecified: record which items remain unspecified and keep this task BLOCKED. Do not fill the gap by inference.
- If applying the decision would require modifying an authoritative technical contract (e.g. `GAME_STATE.md`): STOP — that is TASK-093's act, not this task's.

---

## 8. Recording the Decision

This task records the gameplay/product decision only.

The decision must NOT be applied to authoritative documentation by TASK-094.
In particular, TASK-094 must not modify:

- COMBAT_RULES.md
- BOSS_RULES.md
- GAME_RULES.md
- GAME_STATE.md
- SIGNALR_PROTOCOL.md
- API_CONTRACTS.md
- REDIS_STATE.md
- any ADR

The canonical gameplay owner for the resolved rule is
`docs/01-game-design/COMBAT_RULES.md` §5.2.

After TASK-094 obtains a complete decision, the decision becomes input to
TASK-093. TASK-093 is responsible for determining and documenting the
StatusEffects[] Battle State contract according to its own authoritative
documentation workflow.

TASK-094 must never imply that the authoritative documentation has already
been updated.

If the repository workflow requires the gameplay decision to be recorded in
an authoritative document before TASK-093 can proceed, STOP after recording
the decision in TASK-094 and report that documentation application remains
the next required action. Do not perform that documentation change here.

---

## Decision Record

<!--
  RECORDED BY THE EXECUTING AGENT from the gameplay/product owner's explicit
  selection. This section is the decision-input deliverable. It records the
  decision ONLY — it does not apply it to any authoritative document (§8).
-->

**Selection:** **Option C — a third semantics defined by the owner** (§4).

**Owner's §4 interaction selection:** item **(i)** — Buff/Debuff duration is
consumed at the **same** `GAME_RULES.md` §17 step 19a point as damage-over-time
Status Effects, which means step 19a also handles non-DoT duration.

### Decision Record — DR1–DR6 (final)

```text
DR1. Duration is a per-effect-instance counter, initialized to the
     applied/refreshed duration value.

DR2. Exactly one decrement occurs per Turn, at §17 step 19a — regardless
     of how many apply/refresh operations occurred earlier in that same
     Turn.

DR3. Apply and Refresh use the SAME mechanism: `remaining = duration`
     (an initial Apply is not semantically different from a Refresh;
     Refresh simply re-executes the same "set remaining" operation on
     an already-active effect instance).

DR4. Same-Turn reapplication (Apply/Refresh occurring again within the
     Turn in which the effect is already active) resets `remaining` to
     the new duration value and does NOT trigger an additional
     consumption in that Turn. Only step 19a consumes.

DR5. Expiration occurs when `remaining` reaches 0 at step 19a. An
     effect at `remaining = 0` is inactive from that point forward
     (i.e. not active during the following Turn).
```

### DR6 — Defensive duration-consumption invariant

For any Turn-based Buff/Debuff Status Effect that is applied or refreshed at a
documented point before `GAME_RULES.md` §17 step 19a, the effect consumes one
Turn of duration at that Turn's step 19a.

`GAME_RULES.md` §17 currently defines Boss Response (step 18) before End Turn
(step 19), and step 19a is the last combat effect of the Turn. Therefore all
currently documented Buff/Debuff application sites occur before the consumption
point.

No current gameplay rule defines an application or refresh after step 19a. If a
future gameplay source introduces such a site, its duration-consumption timing
must be explicitly defined before implementation; this rule does not infer or
create such an application point.

### Supporting Citations for DR6

- `GAME_RULES.md` §17 — the resolution order is canonical and "domain documents
  may expand individual steps but must not reorder them"; `18. Resolve Boss
  Response` precedes `19. End Turn`.
- `GAME_RULES.md` §17 step 18b — the documented non-DoT Buff/Debuff apply site
  ("execute the Skill … and apply non-damage effects" — this is Root).
- `GAME_RULES.md` §17 step 19a — "This is the last combat effect of the Turn —
  it runs after the Boss Response (step 18) — and it fires once per resolved
  Turn."

### Worked Examples (derived from DR1–DR5; verified consistent)

```text
duration = 2, no refresh:
  Turn N:   Apply Root(2)         -> remaining = 2
            §17 step 19a          -> remaining = 1
  Turn N+1: Root active
            §17 step 19a          -> remaining = 0 -> expires
  Turn N+2: Root inactive

duration = 2, refreshed in Turn N+1:
  Turn N:   Apply Root(2)         -> remaining = 2
            §17 step 19a          -> remaining = 1
  Turn N+1: Refresh Root(2)       -> remaining = 2
            §17 step 19a          -> remaining = 1
  Turn N+2: §17 step 19a          -> remaining = 0 -> expires

duration = 1, no refresh:
  Turn N:   Apply(1)              -> remaining = 1
            §17 step 19a          -> remaining = 0 -> expires
  Turn N+1: inactive

duration = 2, applied and refreshed within the same Turn N (DR4):
  Turn N:   Apply(2)              -> remaining = 2
            Refresh(2)            -> remaining = 2 (reset; no extra consumption)
            §17 step 19a          -> remaining = 1
  Turn N+1: active
            §17 step 19a          -> remaining = 0 -> expires
```

### §5 Required Decision Coverage — Item-by-Item

```text
 1. Initial application —            ANSWERED
    An effect begins applying when it is applied during Turn N (e.g. Boss
    Skill secondary effect at step 18b) and is active during Turn N.

 2. Exact consumption point —        ANSWERED
    One Turn of duration is consumed at `GAME_RULES.md` §17 step 19a.

 3. Active during application Turn — ANSWERED
    Yes. "Application Turn counts" — an effect applied during Turn N is
    active during Turn N.

 4. duration = 1 expiry —            ANSWERED
    DR1 + DR2 + DR5: applied Turn N -> remaining = 1 -> step 19a consumes it
    -> remaining = 0 -> expires at Turn N step 19a; inactive from Turn N+1.
    (Worked example above.)

 5. duration = 2 expiry —            ANSWERED
    DR1 + DR2 + DR5: applied Turn N -> remaining = 2 -> step 19a Turn N ->
    remaining = 1 -> step 19a Turn N+1 -> remaining = 0 -> expires at Turn N+1
    step 19a; inactive from Turn N+2. (Worked example above.)

 6. Reapplication before expiry —    ANSWERED
    DR3 + DR4: Refresh re-executes `remaining = duration` on the
    already-active instance; the refreshed counter is then consumed exactly
    as in DR2 (one decrement per Turn at step 19a). Magnitude is not
    stacked, consistent with `COMBAT_RULES.md` §5.2 item 2.
    (Worked example: duration = 2 refreshed in Turn N+1.)

 7. Same-Turn reapplication —        ANSWERED
    DR4: resets `remaining` to the new duration value and does NOT trigger an
    additional consumption in that Turn. Only step 19a consumes.
    (Worked example above.)

 8. Interaction with §17 step 19a —  ANSWERED
    Item (i): consumed at the same step 19a point, which then also handles
    non-DoT duration.

 9. Rule scope —                     ANSWERED
    General rule for Turn-based Buff/Debuff Status Effects; Root follows the
    general Turn-based rule. Effects explicitly defined as trigger-based
    expiry do not use the Turn countdown (`COMBAT_RULES.md` §5.2 item 1
    already allows a duration "or a trigger-based expiry").
```

**Coverage result: 9 of 9 answered.** Items 1, 2, 3, 8, 9 by the owner's
selection; items 4, 5, 6, 7 by **DR1–DR5** as supplied by the owner; the
apply/refresh-ordering edge case by **DR6** as supplied by the owner.

No §5 item remains unspecified.

### Status Effect of This Record

**Decision complete for the §5 coverage requirement.** Every item in §5 is
answered by DR1–DR6 as supplied by the gameplay/product owner. The previously
outstanding items 4, 5, 6 (consumption half), and 7 were resolved by DR1–DR5,
and the apply/refresh-ordering edge case was resolved by DR6.

### No Documentation Applied

The decision above has **not** been applied to any authoritative document. No
file under `docs/` was modified by TASK-094. `COMBAT_RULES.md` §5.2 remains
unchanged and does not yet state this duration rule. Applying DR1–DR6 to
`COMBAT_RULES.md` §5.2 (and, per DR6's citation, the §17 step 19a scoping) is
the downstream contract-resolution act owned by TASK-093 — see §8.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the decision is received
  and recorded. While the decision is outstanding, this task is BLOCKED and
  this section must not be written as a completion record.
-->

### Changed Files

This task file only. No file under `docs/`, `src/`, or `tests/` was modified.

### Decision Recorded

**Yes — complete.** The gameplay/product owner supplied **DR1–DR6**; see the
"Decision Record" section above for the verbatim rules, the §5 item-by-item
coverage, and the worked examples.

All **9 of 9** §5 Required Decision Coverage items are answered. The
apply/refresh-ordering edge case that was open at the previous revision is
resolved by **DR6**, recorded as the owner supplied it.

### Documentation

No authoritative documentation was modified by TASK-094. In particular,
`COMBAT_RULES.md` §5.2, `BOSS_RULES.md` §6.3.1, `GAME_RULES.md` §17,
`GAME_STATE.md`, and TASK-093/091/092/079 are unchanged.

The resolved rule is **not yet in force**: no authoritative document states
DR1–DR6. Applying them to `COMBAT_RULES.md` §5.2 is the downstream
contract-resolution act owned by TASK-093 (§8).

### Handoff

The recorded decision is available as **complete** input to TASK-093.

TASK-093 must independently apply DR1–DR6 to its StatusEffects[]
contract/documentation workflow according to its own authoritative
documentation workflow. TASK-094 has not applied it and must not.

### Validation Results

Ran — evidence audit only, no documentation or source change:

- Decision is consistent with the cited existing rules
  (`COMBAT_RULES.md` §5.1, §5.2 item 1, item 2; `GAME_RULES.md` §2, §17
  step 18, step 19a; `BOSS_RULES.md` §6.3.1 item 1, item 3).
- Does not contradict TASK-091: consumption at step 19a is the timing
  TASK-091 established, and no SkillCooldown precedent was generalized.
- Does not contradict TASK-092: Root's magnitude (`-30% Pet ATK`) and
  "2 Turns" duration are untouched; DR1–DR6 address only the consumption
  point and counter mechanics.
- Decision is **complete enough for TASK-093 to consume** (9 of 9 §5 items
  answered).
- Confirmed no authoritative documentation was modified.
- Confirmed no source code changed; confirmed no existing task was modified.
- Scope validated against `MVP_SCOPE.md` §1 (Status Effects are IN scope).

Note: DR1–DR5 coincide with `BOSS_RULES.md` §6.3.1 item 1's existing Burn
schedule (tick #1 at Turn N step 19a, tick #2 at Turn N+1 step 19a, expires
before Turn N+2). This task does not extend Burn's rule to non-DoT effects;
the interaction is recorded under §5 item 8 as selected by the owner.

### Server Authority & Scope Verification

- [x] Confirmed no client-authoritative gameplay logic was added
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1 — Status Effects are IN scope)
- [x] Confirmed TASK-093 was not modified
- [x] Confirmed no source code was modified
- [x] Confirmed no authoritative documentation was modified by TASK-094
