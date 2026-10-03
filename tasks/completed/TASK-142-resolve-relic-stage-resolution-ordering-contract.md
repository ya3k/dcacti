# TASK-142 — Resolve the Relic Stage's Resolution-Ordering Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section.

  PROVENANCE: identified by TASK-133's "Known Deviations Reported (not silently
  resolved)" record. TASK-133 implemented GAME_RULES.md §17 step 11 ("Trigger
  Relics") in the Application-layer resolution pipeline, which runs AFTER the
  Domain Swap executor has already applied steps 12–14 for the same Swap. The
  deviation is observable only for a Relic condition that reads state steps
  12–14 change — HpPercentageBelow, whose comparison therefore reads the Pet's
  HP after the Swap's healing rather than before it.

  THIS TASK DECIDES NOTHING AND IMPLEMENTS NO BEHAVIOR. It resolves the
  ordering question from the authoritative documents (or reports that no
  document determines it), records the answer in the canonical owner, and
  identifies — but does not perform — any implementation correction.

  BOUNDARY: documentation only. Zero files under src/ or tests/.
-->

---

## Metadata

```text
Task ID:           TASK-142
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md)
Status:            DONE
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION LOW–MEDIUM; MEDIUM
                   because the resolved ordering governs every Relic
                   condition's observation point and is cross-referenced by
                   GAME_RULES.md §17, RELIC_RULES.md §8, COMBAT_RULES.md §2,
                   CARD_RULES.md §3.6/§4.1, and GAME_EVENTS.md §1)
Priority:          HIGH (the only open sequencing question on the landed Relic
                   stage; GAME_RULES.md §17 is the canonical order every
                   resolution stage is measured against)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review")
Supporting Agents: gameplay (GAME_RULES.md §17 / RELIC_RULES.md §3–§8 are the
                   owning documents for the order and the condition forms;
                   consulted for content accuracy, not to author the answer),
                   backend (the resolution pipeline's actual step placement is
                   an implementation observation only)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-133 (DONE — the stage whose placement raised the
                   question; its completion evidence is immutable here),
                   TASK-131 (DONE — the Condition contract §8.1),
                   ADR-018 (Accepted — the Relic contract's architectural
                   record; not modified by this task)
Blocks:            The implementation correction that would move the Relic
                   stage's evaluation point ahead of steps 12–14, if the
                   resolved contract requires one (not created here)
Estimate:          Simple (contract resolution and canonical-owner
                   documentation only; zero code, zero gameplay rules)
```

---

## Problem Statement

`GAME_RULES.md` §17's fixed logical order places step 11 ("Trigger Relics")
immediately after step 10 ("Charge Passive") and before step 12 ("Generate
Resources"), step 13 ("Update Power"), and step 14 ("Resolve Player Effects").

TASK-133's landed implementation evaluates and applies the Relic stage after
the Domain Swap executor has already applied steps 12–14 for the same Swap
(`src/backend/GameServer.Domain/Match3/SwapExecution.cs`'s
`ResourceGenerator.Generate` / `ApplyPower` / `ApplyHeal`), so every Relic
condition reads state that steps 12–14 have already changed. TASK-133's own
record states the consequence: "`HpPercentageBelow` reads the Pet's HP after
this Swap's healing rather than before it."

The question this task resolves, from the authoritative documents only:

```text
At what exact state snapshot are Relic conditions evaluated — the state as it
stands after step 10, or the state after steps 12–14 have been applied?
```

Two lower-precedence documents render the order differently from §17 (see
"Reported conflicts" in Completion Evidence); they are subordinate to §17 and
are reported, not followed.

---

## Authoritative References

- `docs/01-game-design/GAME_RULES.md` **§17** (the fixed logical order; the
  "canonical — domain documents may expand individual steps but must not
  reorder them" clause; "observable game behavior must preserve this logical
  ordering"), §5 (Combo), §12 (Power), §13 (Relic Rules), §16 (Battle Event
  Model), §20 (Rule Change Policy), §21 (Source of Truth Hierarchy)
- `docs/01-game-design/RELIC_RULES.md` **§8.1** (the three structured
  Condition forms, their evaluation against the current resolution state "at
  the point `GAME_RULES.md` §17 step 11 executes", and the no-persistent-counter
  rule), **§8.3** (the `lifetime` vocabulary), **§8.4** (lifetime vs
  re-evaluation independence), **§8.5** (the four provisioned rows), **§8.7**
  (startup status), §1, §3, §4, §6 note 2, §7
- `docs/01-game-design/COMBAT_RULES.md` **§2 item 5** (the HP-Gem heal pool:
  step 12 generates it, step 14 applies it), §1.1 (combat stats), §2 items 2/7
  (`EffectiveCrit`), **§3.3 items 7–10** (`NextAttack` Crit lifetime and the
  qualifying-attack consumption boundary), **§5.6/§5.6.6** (`EffectivePetATK`),
  §4 items 1/7 (Heal Resolution)
- `docs/01-game-design/CARD_RULES.md` **§3.6** (`EffectiveCardCost`, items 7
  and 8 — calculated once at the start of a cast resolution, read from the
  committed `PetState.CardCostModifiers[]`), §3 item 4 (cast sequence), §4.1
  (a modifier created by a step-14 resolution site is consumed by a later
  attack in the same resolution — "a consequence of `GAME_RULES.md` §17's fixed
  order"), §5 (the Cards-vs-Relic boundary)
- `docs/01-game-design/MATCH3_RULES.md` §2.1.6 (accepted Swap lifecycle),
  §4.1 item 5 (the loop is §17's logical order "expressed once per iteration"
  for the board steps), §5.7 (generation hooks), §5.8.2 (union semantics),
  §6 (Combo), §8.3 (update order within a resolution)
- `docs/01-game-design/PASSIVE_RULES.md` §2.1/§2.3 (per-Match charging,
  threshold evaluated once per Cascade resolution), §7 (the step-10 position)
- `docs/02-technical/GAME_STATE.md` **§2.2** (`BattleState.Combo`,
  `BattleState.MatchCount`), **§2.3** (`PetState`), §2.3.1/§2.3.4/§2.3.5/§2.3.7
  (the modifier carriers), §2.3.3 (no pending collections), **§5.1** (the
  single post-resolution write-back; "Nothing is written mid-resolution"),
  §5.1.2/§5.1.3/§5.1.4 (the modifier lifecycles), §3 (Transient Resolution
  State), §0 (staged implementation)
- `docs/02-technical/GAME_EVENTS.md` §1 ("Events are emitted in the same order
  as the steps in `GAME_RULES.md` §17"), §1.1 (the board cycle), §2
  (`RelicTriggered`, `PowerChanged`, `CardCast`), §3 item 7 (emission as a
  separate stage)
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 (no new client→server action),
  §3.2.23–§3.2.25, §4 (the `BattleState` projection)
- `docs/02-technical/REDIS_STATE.md` §2, §4 (one write-back, one
  `Sequence` compare-and-set)
- `docs/00-overview/GDD.md` §2 (the core loop narrative, which defers the exact
  order to `GAME_RULES.md`), `docs/00-overview/MVP_SCOPE.md` §1 (Relics IN)
- `docs/03-decisions/ADR/ADR-018-structured-relic-trigger-condition-effect-contract.md`
  (items 4/5 — conditions evaluated at §17 step 11; **not modified here**),
  `ADR-017` (the `NextAttack` Crit model this task must not reopen),
  `ADR-010` (committed-Swap state), `ADR-001` (server authority)
- `tasks/backlog/TASK-133-implement-server-authoritative-relic-trigger-and-effect-resolution.md`
  — its "Known Deviations Reported" record (the implementation observation
  this task starts from; left intact)
- `AGENTS.md` §2 (precedence), §4 (report a conflict, do not resolve it
  silently), §7 (invent nothing), §11 (determinism), §16 (task discipline),
  §17, §20, §21

---

## Scope

### In Scope

1. **Resolve the ordering from the authoritative documents:** determine at
   which state snapshot §17 step 11 evaluates and applies Relic triggers,
   conditions, and effects, relative to steps 10 and 12–14.
2. **Answer the decision set** (D1–D8): trigger timing, condition-evaluation
   state (`HpPercentageBelow`), `MatchCountAtLeast`, `ComboAtLeast`,
   Power-effect ordering, `CardCost` timing, `ATK` timing, and the
   `NextAttack` Crit timing inside one action.
3. **Record the answer at its canonical owner** — the overall action order
   stays in `GAME_RULES.md` §17; `RELIC_RULES.md` §8.1 carries a
   cross-reference stating the observation point for the Condition forms.
4. **Audit documentation consistency** after the change (§12 of the task
   brief): §17 deterministic; `RELIC_RULES.md` non-contradictory; state,
   Card-cost, and ATK contracts untouched; `NextAttack` lifetime unchanged; no
   new event, state carrier, or Redis behavior required.
5. **Record the contract conflict and identify the exact implementation
   correction** required if the resolved contract differs from TASK-133's
   placement — without performing it and without reopening TASK-133.

### Out of Scope

- **Any source or test change** — zero files under `src/` or `tests/`
- **Reopening TASK-133's implementation, its task record, or its completion
  evidence**
- **Creating the implementation-correction task** — identified, not created
  (see Completion Evidence)
- **Modifying ADR-018, TASK-131's, TASK-132's, TASK-134's, or TASK-133's
  completed contracts** — the Relic `EffectDefinition`/`Condition` shape, the
  lifetimes, the magnitudes, the carriers, and the `RelicTriggered` shape are
  unchanged
- **Reopening the `NextAttack` Crit gameplay decision** (TASK-116/TASK-117) or
  the `CardCost`/`ATK` composition decisions (TASK-134/TASK-136/TASK-137/TASK-138)
- **Redesigning `GAME_RULES.md` §17** — no step is added, removed, or
  reordered; no unrelated section is touched
- **Adding a Relic, Trigger, Condition form, effect, magnitude, event, state
  member, wire member, Redis key, or client action**
- **A broad gameplay audit** — the scope is the step-11 ordering question
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2

---

## Current State

```text
Authoritative documents:
  GAME_RULES.md §17              10 Charge Passive → 11 Trigger Relics →
                                 12 Generate Resources → 13 Update Power →
                                 14 Resolve Player Effects
  RELIC_RULES.md §8.1 item 2     conditions evaluated against the current
                                 resolution state "at the point
                                 GAME_RULES.md §17 step 11 executes"
  COMBAT_RULES.md §2 item 5      step 12 generates the HP-Gem heal pool;
                                 step 14 ("Resolve Player Effects") applies it

Implementation observation (TASK-133, not proof of intent):
  SwapExecution.cs               Generate → ApplyPower → ApplyHeal  (steps
                                 12/13/14) run inside the Swap executor
  BattleStateService.cs          PassiveTracker.Charge (step 10), then
                                 RelicResolver.Resolve (step 11), then the
                                 Damage Pipeline (steps 15–17)
  → the Relic stage reads a PetState whose HP already includes this Swap's
    step-14 healing

Reported-not-resolved (TASK-133 "Known Deviations Reported"):
  the same statement, recorded for a future sequencing task — this task
```

---

## Acceptance Criteria

- [x] The exact order of steps 10, 11, 12, 13, and 14 is stated
  deterministically from the authoritative documents, with each claim citing
  its owning section
- [x] The state snapshot Relic conditions are evaluated against is stated
  explicitly, including whether `HpPercentageBelow` reads the Pet's HP before
  or after this Swap's step-14 healing
- [x] `MatchCountAtLeast` and `ComboAtLeast` are pinned to the existing
  authoritative values (`BattleState.MatchCount`, `BattleState.Combo`) at the
  step-11 observation point, with **no** new Relic counter introduced
- [x] The Power, `CardCost`, `ATK`, and `NextAttack` Crit scoping answers
  follow `GAME_RULES.md` §17's order, not implementation convenience, and
  change no existing magnitude, lifetime, composition, or consumption rule
- [x] The ordering rule is authored at its canonical owner
  (`GAME_RULES.md` §17) and referenced — not duplicated — from
  `RELIC_RULES.md` §8.1
- [x] `RELIC_RULES.md`, `GAME_STATE.md`, `CARD_RULES.md`, and
  `COMBAT_RULES.md` are consistent with the recorded order after the change
- [x] The `NextAttack` Crit lifetime and consumption boundary are unchanged
  (`COMBAT_RULES.md` §3.3 items 7–10, `ADR-017`)
- [x] No new event, state carrier, Redis behavior, wire member, or client
  action is required by the recorded contract
- [x] **Zero files under `src/` or `tests/` are modified**; `tasks/completed/`
  is unmodified
- [x] TASK-133's completion evidence is left intact; the conflict and the exact
  required correction are recorded
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ (none — documentation-only task; verified zero diff)
[ ] src/frontend/client/ (none)
[ ] tests/ (none)
[x] docs/01-game-design/GAME_RULES.md (§17 — the step-11 evaluation-point
                                        statement; version header)
[x] docs/01-game-design/RELIC_RULES.md (§8.1 — the cross-reference pinning the
                                         observation point per Condition form;
                                         version header)
[ ] docs/02-technical/ (none expected — no event, state, or storage contract
                         changes)
[ ] docs/03-decisions/ADR/ (none — ADR-018 is not modified per this task's
                             boundary)
[ ] tasks/completed/ (MUST remain unmodified)
```

---

## Implementation Notes

- **The canonical owner is `GAME_RULES.md` §17.** It already owns the order;
  the change states the *evaluation point* the order implies, and does not
  restate any Condition form, magnitude, or lifetime.
- **`RELIC_RULES.md` §8.1 is touched by cross-reference only.** §8 owns the
  Condition forms; the ordering rule stays in §17
  (`documentation/documentation-change.md` §2 — no duplication).
- **"May split these into multiple internal steps" is not a reordering
  licence.** §17 permits an implementation to split a step; it does not permit
  executing step 11 after steps 12–14. `GAME_STATE.md` §5.1's **single**
  write-back defers *visibility* of the resolution's state; it does not move an
  evaluation point.
- **Do not treat the current source ordering as evidence of intent**
  (`AGENTS.md` §4, `.ai/README.md` §18 — the default assumption is that docs
  describe intended behavior).
- **Report, do not silently follow, the lower-precedence renderings** found in
  `ADR-018`'s Context prose, `GDD.md` §2's loop narrative, and TASK-133's
  acceptance criterion ("between resource generation and damage").
- Do not modify `tasks/completed/`.

---

## Testing Requirements

### Required Verification

```text
[x] Documentation consistency audit — the recorded order against
                                      GAME_RULES.md §17, RELIC_RULES.md §3/§7/
                                      §8.1–§8.7, COMBAT_RULES.md §2/§3.3/§4/
                                      §5.6.6, CARD_RULES.md §3.6/§4.1/§5,
                                      GAME_STATE.md §2.2/§5.1, GAME_EVENTS.md
                                      §1/§2, MATCH3_RULES.md §5.7/§8.3
[x] Determinism check              — §17 states one order and one observation
                                     point; no "either/or" remains
[x] Duplication check              — no second copy of the ordering rule
[x] Unmodified-file guard          — zero diff under src/, tests/, and
                                     tasks/completed/
[x] Unit tests                     — N/A: this task changes no code
[x] Integration tests              — N/A: this task changes no code
[x] Gameplay scenarios             — N/A: the scenarios the resolved contract
                                     implies are the correction task's
```

### Key Edge Cases

- A Relic whose Condition is met **exactly** at the threshold — unchanged by
  this task; the forms and their `N` values are `RELIC_RULES.md` §8.1/§8.5's
- A Swap that both triggers a Relic and heals the Pet above the threshold in
  the same Swap — the case the ordering decides
- A Relic whose effect is `Immediate` (`Mana Crystal`) versus `Battle`
  (`Berserker Core`) versus `NextAttack` (`Assassin Eye`) — the ordering
  answer must not disturb any lifetime
- An action other than a Swap (a Card cast) — the Relic stage has no
  evaluation site there for the provisioned rows; the `CardCost` modifier is
  read by the cast path, never created by it

---

## Stop Conditions

- **If the authoritative documents do not determine the intended ordering:
  STOP** and report `GAMEPLAY DECISION REQUIRED` with the competing
  deterministic options — do not choose from implementation convenience,
  algebraic equivalence, the current source ordering, or test convenience
- If resolving the order would require changing a completed Relic contract
  (`RelicDefinition`, `EffectDefinition[]`, `ATKModifiers[]`,
  `CardCostModifiers[]`, `NextAttackCritModifiers[]`, a magnitude, or a
  lifetime): STOP and report — do not silently change it
- If the task drifts into implementing the correction, editing `src/` or
  `tests/`, or reopening TASK-133: STOP
- If the task becomes a broad gameplay audit rather than the step-11 ordering
  question: STOP and re-scope
- If any resolution would place Relic evaluation or effect computation on the
  client: STOP per `AGENTS.md` §10
- If task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: STOP & decompose

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Decision Recorded

```text
Problem:
Relic Step-11 ordering was ambiguous relative to Steps 10–14.

Decision:
A committed Swap evaluates and applies step 11 ("Trigger Relics") after
step 10 ("Charge Passive") and BEFORE step 12 ("Generate Resources"),
step 13 ("Update Power"), and step 14 ("Resolve Player Effects"). The Relic
stage reads — and its effects land on — the resolution state as it stands at
that point: before this Swap's own steps 12–14 have been applied. The
implementation may split a step into internal operations, and the single
post-resolution write-back defers only when the resolution's result becomes
visible; neither moves the step-11 evaluation point.

Canonical owner:
docs/01-game-design/GAME_RULES.md §17 (the fixed logical order — this is
where the ordering rule is authored); docs/01-game-design/RELIC_RULES.md §8.1
item 8 is a cross-reference stating what the position means for the three
Condition forms.

Condition timing:
HpPercentageBelow reads the active Pet's HP at §17 step 11 — before this
Swap's step-14 player-effect resolution, i.e. before the HP-Gem heal pool
COMBAT_RULES.md §2 item 5 has step 12 generate and step 14 apply. It never
reads a post-healing HP for the Swap it is evaluated in.

MatchCountAtLeast:
BattleState.MatchCount (GAME_STATE.md §2.2) as of §17 step 9
("Count Matches") — the battle's cumulative count, this Swap's Matches
included. No Relic counter (RELIC_RULES.md §8.1 item 3).

ComboAtLeast:
BattleState.Combo (GAME_STATE.md §2.2) as of §17 step 8 ("Update Combo") —
this Swap's Combo. No Relic counter.

Power:
Mana Crystal's `Power` / `Flat` / `Immediate` effect is applied at step 11 —
before step 12's generation and before step 13 writes the generated Power into
PetState.Power, so the relic-source change precedes the step-13 match-source
change. Its position is fixed by §17; the identical resulting integer is a
consequence of the single §12 clamp for non-negative additions, not the basis
of the rule.

CardCost:
Emergency Core's applied modifier is created at step 11 of a Swap. A Card
cast is a separate action whose cost is composed once at the start of that
cast's resolution from the committed PetState.CardCostModifiers[]
(CARD_RULES.md §3.6 items 7–8). The modifier therefore affects a LATER
Card-cast action, never a cost calculation of the Swap that created it (a Swap
has no card cost), and the Card path never creates, refreshes, or removes an
entry (CARD_RULES.md §5).

ATK:
Berserker Core's `Battle`-lifetime ATK modifier is applied at step 11, which
precedes steps 15–17, so it participates in the CURRENT Swap's damage
computation through EffectivePetATK (COMBAT_RULES.md §5.6/§5.6.6). Its
magnitude, composition, and lifetime are unchanged.

NextAttack Crit:
Assassin Eye's `Crit` / `PercentagePoints` / `NextAttack` modifier is created
at step 11; the same Swap's Player→Boss damage at steps 15–17 is the owner's
next qualifying attack, so the modifier participates in that instance's
EffectiveCrit and is consumed by it (COMBAT_RULES.md §3.3 items 7–8,
RELIC_RULES.md §8.3 item 4) — the same same-resolution pattern CARD_RULES.md
§4.1 records for a step-14 creation site. Lifetime, consumption boundary,
composition, source-specific removal, and ADR-017 are unchanged.

TASK-133:
Requires separate implementation correction — see "Required implementation
correction (identified, not performed)". TASK-133 stays DONE and its
completion evidence is unmodified.
```

### Evidence — explicit rules vs implementation observations vs inference

| Source | Kind | Statement |
|---|---|---|
| `GAME_RULES.md` §17 | **explicit rule** | The Swap's canonical order is 10 Charge Passive → 11 Trigger Relics → 12 Generate Resources → 13 Update Power → 14 Resolve Player Effects; "domain documents may expand individual steps but must not reorder them"; "observable game behavior must preserve this logical ordering" |
| `GAME_RULES.md` §17 (new) | **explicit rule (this task)** | Step 11 is evaluated and its effects applied before steps 12–14; the split-step allowance and the single write-back do not move it |
| `RELIC_RULES.md` §8.1 item 2 (TASK-131 D5) | **explicit rule** | Conditions are evaluated "against the current resolution state, at the point `GAME_RULES.md` §17 step 11 executes" |
| `RELIC_RULES.md` §8.1 item 8 (new) | **cross-reference (this task)** | What that point means for `HpPercentageBelow` / `MatchCountAtLeast` / `ComboAtLeast` |
| `RELIC_RULES.md` §8.1 item 3, §8.4, §8.5 | **explicit rule** | No persistent Relic counters; lifetime independent of re-evaluation; the four provisioned rows |
| `COMBAT_RULES.md` §2 item 5 | **explicit rule** | Step 12 generates the HP-Gem heal pool; step 14 ("Resolve Player Effects") applies it to the active Pet's HP |
| `COMBAT_RULES.md` §3.3 items 7–10, §5.6.6 | **explicit rule** | `EffectiveCrit` / `EffectivePetATK` are composed at attack resolution from the committed carriers |
| `CARD_RULES.md` §3.6 items 7–8, §4.1, §5 | **explicit rule** | `EffectiveCardCost` is composed once per cast from the committed `CardCostModifiers[]`; a modifier created by an earlier step is consumed by a later attack in the same resolution |
| `GAME_STATE.md` §2.2, §5.1 | **explicit rule** | `MatchCount`/`Combo` ownership; the single post-resolution write-back, "Nothing is written mid-resolution" |
| `GAME_EVENTS.md` §1 | **explicit rule** | "Events are emitted in the same order as the steps in `GAME_RULES.md` §17" |
| `GDD.md` §2 | **subordinate narrative** | Its loop lists Generate Resources before Relics, and explicitly defers the exact order to `GAME_RULES.md` — not a conflict |
| `ADR-018` Context (lines 12–13) | **conflicting prose, lower precedence** | "places step 11 … between resource generation and damage" — contradicts §17 (see findings) |
| `src/.../SwapExecution.cs`, `.../BattleStateService.cs` | **implementation observation only** | Steps 12–14 run inside the Swap executor; the Relic stage is invoked after them. Treated as the (possibly incorrect) implementation, never as evidence of intent (`.ai/README.md` §18) |
| TASK-133 "Known Deviations Reported" | **implementation observation** | Records the same deviation and its `HpPercentageBelow` consequence |

### Reported findings (documentation consistency audit)

| # | Concept | Source A | Source B (or code) | Owner by precedence | Classification | Severity | Action |
|---|---|---|---|---|---|---|---|
| 1 | Step-11 evaluation point | `GAME_RULES.md` §17 | `src/.../SwapExecution.cs` + `.../BattleStateService.cs` | `GAME_RULES.md` §17 | Implementation bug (docs correct; code deviates) | HIGH — gameplay-visible for `HpPercentageBelow` | Correction identified, not performed |
| 2 | Ordering prose | `GAME_RULES.md` §17 | `ADR-018` Context lines 12–13 | `GAME_RULES.md` | Stale/incorrect ADR prose (not a decision item) | LOW–MEDIUM | Reported; ADR-018 not modified (task boundary) |
| 3 | Ordering phrasing | `GAME_RULES.md` §17 | TASK-133 acceptance criterion ("between resource generation and damage"); TASK-131 Problem Statement | `GAME_RULES.md` | Stale task text | LOW | Reported; both task records left intact |
| 4 | Loop narrative | `GAME_RULES.md` §17 | `GDD.md` §2 loop | `GAME_RULES.md` (GDD defers the exact order to it) | Not a conflict | LOW | None |
| 5 | Event rendering | `GAME_RULES.md` §17 | `GAME_EVENTS.md` §1.1 (`[PowerChanged]` inside the pass cycle) and §1's flat list (`PowerChanged` before `PassiveCharged`) | `GAME_RULES.md` §17 | Pre-existing rendering inconsistency in a Draft technical doc; §1 states it follows §17 | LOW | Reported; not introduced or worsened by this task; no action taken |
| 6 | Stale cross-reference | `RELIC_RULES.md` §8.1 item 3 cites `GAME_STATE.md` §2.4/§2.6 for the cumulative Match count / Combo | `GAME_STATE.md` §2.2 owns both (§2.4 is `BossState`, §2.6 is RNG) | `GAME_STATE.md` §2.2 | Documentation bug (stale section numbers) | LOW | Reported; not fixed — outside this task's boundary (unrelated to the ordering); smallest correction is §2.4/§2.6 → §2.2 |
| 7 | Relic `NextAttack` creation site | `GAME_RULES.md` §17 step 11 + `RELIC_RULES.md` §8.3 item 4 | `COMBAT_RULES.md` §3.3 item 8's parenthetical names only step 14 and step 10 sites | §17 + `RELIC_RULES.md` | Not a conflict (the parenthetical is illustrative; the rule is "the source's own existing site") | LOW | None; item 8 needs no change |

### Required implementation correction (identified, not performed)

Identified so a **separate** implementation task can be scoped; no `src/` or
`tests/` file was modified here, and TASK-133 is not reopened.

```text
Where:  src/backend/GameServer.Application/Battle/BattleStateService.cs
        (ResolveSwapAsync — the RelicResolver.Resolve call and the state it
         receives) together with
        src/backend/GameServer.Domain/Match3/SwapExecution.cs
        (SwapExecutor.Execute — which currently folds steps 12/13/14 into
         PetState before returning)

Required: the Relic stage must evaluate and apply against the PetState as it
        stands after step 9/step 10, with this Swap's steps 12–14 NOT yet
        folded in:
          - step 10 (PassiveTracker.Charge) and step 11 (RelicResolver.Resolve)
            run against the pre-step-12 PetState;
          - step 12's generation output is then applied as step 13
            (ApplyPower, single §12 clamp) and step 14 (ApplyHeal) onto the
            state step 11 produced, so a Relic Power effect (Mana Crystal) is
            already present when step 13 writes;
          - steps 15–17 then read the resulting state, unchanged;
          - the resolution still lands in ONE write-back (GAME_STATE.md §5.1).
        No new state member, event, wire member, Redis key, or client action
        is required by the correction.

Tests:  tests/backend/GameServer.Application.Tests/RelicStageResolutionTests.cs
        asserts step 11 after step 10 and before steps 15–17 and does not
        assert the step-12/13/14 relationship; the correction needs a scenario
        for the pre-step-14 HP snapshot (Emergency Core: HP below 30% healed
        above it by the same Swap). The existing Relic, Card-cost, and
        write-back suites must be re-run.

Not created: the correction task itself. The repository workflow does not
        require this documentation task to create it
        (documentation/documentation-change.md has no follow-up step;
        AGENTS.md §16 requires reporting a suggested follow-up, not creating
        it), so it is identified here for whoever schedules the next task.
```

### Changed Files

- `docs/01-game-design/GAME_RULES.md` — §17: one clarifying block after the
  step list ("Step 11's evaluation point, stated exactly") plus its four
  items; version header → 3.3. No step added, removed, or reordered; no
  Trigger, Condition form, effect, magnitude, threshold, or lifetime authored.
- `docs/01-game-design/RELIC_RULES.md` — §8.1: new item 8 (the observation
  point of the three Condition forms, by reference to `GAME_RULES.md` §17);
  version header → 1.12. §8.2–§8.7, §6's rows, and §3's Trigger list are
  unchanged.
- `tasks/active/TASK-142-resolve-relic-stage-resolution-ordering-contract.md`
  — this task file.

### Validation Results

```text
Documentation consistency audit   PASS — see the findings table; the ordering
                                  rule has exactly one owner (GAME_RULES.md
                                  §17) and RELIC_RULES.md §8.1 references it
Determinism check                 PASS — §17 now states one order and one
                                  observation point; no either/or remains
                                  (the "split into internal steps" allowance
                                  and GAME_STATE.md §5.1's single write-back
                                  are explicitly excluded as reorderings)
Duplication check                 PASS — the ordering text exists once; §8.1
                                  item 8 states only what the position means
                                  for forms §8.1 already owns
Cross-reference check             PASS — RELIC_RULES.md §8.1/§8.4/§8.5,
                                  COMBAT_RULES.md §2 item 5/§3.3/§5.6.6,
                                  CARD_RULES.md §3.6/§4.1/§5, GAME_STATE.md
                                  §2.2/§5.1, GAME_EVENTS.md §1, MATCH3_RULES.md
                                  §5.7/§8.3, PASSIVE_RULES.md §2/§7 remain
                                  consistent with the recorded order
No new event required             PASS — the contract emits only the existing
                                  RelicTriggered / PowerChanged
No new state carrier required     PASS — no counter, snapshot, or member
No new Redis behavior required    PASS — the existing record and CAS are
                                  unchanged
NextAttack Crit lifetime          PASS — unchanged; COMBAT_RULES.md §3.3
                                  items 7–10 and ADR-017 are untouched
Unmodified-file guard             PASS — no .cs/.csproj/.json file under
                                  src/ or tests/ was written by this task
                                  (mtime audit: the only files written after
                                  17:15 are the two docs and this task file);
                                  tasks/completed/ unmodified. The working
                                  tree carries TASK-133's pre-existing
                                  uncommitted changes, so a git-diff-based
                                  isolation is not available — the mtime
                                  audit is the evidence.
Unit tests                        N/A — this task changes no code
Integration tests                 N/A — this task changes no code
Gameplay scenarios                N/A — the scenario the contract implies is
                                  the correction task's (identified above)
```

### Quality Review Checklist (`quality/review.md` §1)

```text
1 Correctness        PASS — the recorded order is the canonical owner's own
                     text; no claim rests on the implementation
2 Architecture       N/A — no code or architecture change
3 Scope              PASS — two docs and one task file; no step reordered, no
                     value/lifetime/contract changed, no audit beyond step 11
4 Tests              N/A — no code; validation is the documentation audit
5 Documentation      PASS — canonical owner updated, cross-reference added,
                     dependent contracts re-read together, findings reported
6 Security           N/A — no security surface touched
7 Performance        N/A — no runtime path touched
8 Maintainability    PASS — one short block plus one item; no duplication, no
                     new abstraction
9 Determinism        N/A for this change's own artifact (no gameplay code);
                     the resolved contract's determinism is verified above
Final recommendation PASS
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — no client file
  touched and the recorded contract places all Relic evaluation server-side
  (`GAME_RULES.md` §18, ADR-001)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — no new Relic,
  Trigger, Condition, effect, system, or content
- [x] Confirmed zero files modified under `src/` or `tests/`
- [x] Confirmed `tasks/completed/` unmodified, including TASK-131 and TASK-133
- [x] Confirmed no completed contract modified — `RelicDefinition`,
  `EffectDefinition[]`, `ATKModifiers[]`, `CardCostModifiers[]`,
  `NextAttackCritModifiers[]`, the four magnitudes, and every lifetime and
  composition rule are byte-identical
- [x] Confirmed no ADR added or modified (`ADR-018` reported, not edited)
- [x] Confirmed no new event, wire member, state carrier, Redis key, REST
  endpoint, or SignalR method
