# TASK-113 — Resolve the Combat RNG Draw Contract (Crit Roll Point, Stream, and Draw Count)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine contract gap and requires the
  appropriate human/Product-Owner decision, then records it in the canonical
  owner document(s). Inventing a Crit roll formula, a draw point, an RNG
  stream, a draw count, or a DoT-can-crit ruling is the single prohibited
  action of this task (AGENTS.md §7, §20).

  PROVENANCE: identified by the post-TASK-107 next-task discovery pass.
  TASK-107 completed the Basic CardCast path. The next unblocked dependency
  on the ROADMAP.md Phase 1 critical path is PetSkillCast + the three Pet
  Skill Cards. Two of those three (Inferno, Iron Fang) carry `Damage`, `Burn`,
  and `Crit` effects. The discovery pass verified that neither Burn's
  damage tick nor Crit's roll can be implemented from the authoritative
  documents as they stand, because the combat-side RNG consumption contract
  does not exist. This task is the smallest unit that resolves it.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per AGENTS.md §20, "Missing rule"
  and "Ambiguous requirement" are stop conditions. Crit is documented as an
  effect and a chance stat but its roll procedure is nowhere authored
  ("Crit roll" appears only as an undefined premise at COMBAT_RULES.md L140
  and L172). No implementation task may proceed against it.

  BOUNDARY: documentation only. Zero files under src/ or tests/. This task
  creates no ADR unless the recorded answer requires one (reported, not
  authored — AGENTS.md §18), changes no Shield semantic, authors no balance
  value, and adds no gameplay rule beyond the RNG procedure the Product
  Owner supplies.
-->

---

## Metadata

```text
Task ID:           TASK-113
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is a
                   recorded contract decision plus its entry in the canonical
                   owner document(s). See "Type classification note". If the
                   decision requires a code, schema, or ADR change, that change
                   is a SEPARATE follow-up task — not this task's act.
Status:            DONE (the Product Owner answered all five decisions on
                   2026-10-11; D-1, D-2, D-3, D-4, and D-5 are recorded
                   verbatim in "Product Owner Decisions" below, and the
                   resulting state is recorded in "Outcome" and "Follow-Up
                   Tasks Required". Zero files under `docs/`, `src/`, or
                   `tests/` changed. Review passed with APPROVE; transitioned
                   to DONE and moved to tasks/completed/ per TASK_LIFECYCLE.md §4.
                   This follows the TASK-108 precedent exactly. See
                   "Status note" below.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the decision it records governs whether every
                   future combat damage instance consumes randomness, and is
                   cross-referenced by COMBAT_RULES.md §3.3, TDD.md §6,
                   MATCH3_RULES.md §7.2, GAME_STATE.md §2.6, and
                   SIGNALR_PROTOCOL.md §3.2.13. No docs/ rule is changed by
                   this task beyond recording the answer.)
Priority:          HIGH (the sole contract blocker on the PetSkillCast task —
                   the ROADMAP.md Phase 1 "One Pet fully implemented
                   (Element, Passive, Signature Skill)" slice. Without it,
                   neither Inferno's Burn nor Iron Fang's Crit can be
                   implemented, and Burn cannot even be implemented alone:
                   every Burn tick traverses Damage Pipeline step 4, the
                   Crit evaluation region.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (COMBAT_RULES.md §3.3/§5 and GAME_RULES.md §17 are
                   the owning domain documents for Crit and Burn — consulted to
                   CONFIRM the semantics the contract must be able to express,
                   not to author the procedure),
                   backend (TDD.md §6 and GAME_STATE.md §2.6 own the RNG state
                   contract — consulted to state accurately what the current
                   state and draw-point contract does and does not carry),
                   realtime (only if the recorded answer implies an event or
                   wire consequence, which is REPORTED for a separate task,
                   never applied here)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-107 (DONE — completed the Basic CardCast path; it
                     deliberately excluded PetSkillCast, Crit, and Burn.
                     IMMUTABLE; read-only. It is the task whose successor this
                     contract unblocks),
                   TASK-112 (DONE — encoded the three Pet Skill Cards' rows,
                     including Inferno's Damage/Burn and Iron Fang's
                     Damage/Crit elements. IMMUTABLE; read-only. Its migration
                     records in-line that "no Crit roll exists" and "no Burn
                     tick is performed"),
                   TASK-109 / TASK-110 / TASK-111 (DONE — the structured
                     EffectDefinition contract and the authored Pet Skill
                     magnitudes. IMMUTABLE; read-only),
                   TASK-091 / TASK-092 / TASK-093 / TASK-094 (DONE — status
                     effect tick timing and magnitudes. TASK-091 established
                     the Burn/DoT tick position at §17 step 19a. IMMUTABLE;
                     read-only),
                   TASK-095 / TASK-096 (DONE — StatusEffect domain state,
                     step-19a lifecycle, serialization. IMMUTABLE; read-only),
                   ADR-009 (DONE — the deterministic PRNG choice; the answer
                     must not contradict it)
Blocks:            The PetSkillCast implementation task (Inferno's Burn and
                   Iron Fang's Crit cannot be implemented without it), and
                   through it ROADMAP.md Phase 1's "One Pet fully implemented
                   (Element, Passive, Signature Skill)". It also blocks any
                   standalone Burn implementation task, because a Burn tick
                   traverses the Crit evaluation region of the Damage Pipeline.
                   It does NOT block TASK-036, TASK-079, TASK-099, or TASK-102.
Estimate:          Simple (present the evidence, obtain and record one
                   decision set across at most two owner documents; no code,
                   no tests, no migration)
```

**Status note.** Now `DONE`. It began `BACKLOG`, which is this task's
normal starting state while a decision set is unanswered — it is what the task
exists to collect; `TASK_LIFECYCLE.md` §2 does not permit `BACKLOG → BLOCKED`,
so it never started BLOCKED. This follows the TASK-082 / TASK-104 / TASK-108
precedent exactly. Lifecycle: `BACKLOG → READY → IN PROGRESS → IN REVIEW → DONE`.
Quality review passed with `APPROVE` on 2026-10-11. Moved to `tasks/completed/`.

**No stop condition fired.** Despite the task's stop-driven framing, every
required decision was answered explicitly and unambiguously, so the
awaiting-input branch of Stop Condition 1 does not apply. All five answers were
checked against the task's §17 reclassification rule and the stop conditions
below: none changes an already-documented gameplay rule, none requires a new
RNG state/value, none requires a new wire or state representation (D-4), and
none requires a new event contract (D-5). See "Type Re-Classification
Condition" and D-6–D-8.

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`ARCHITECTURE`. The deliverable is a recorded contract decision in its canonical
owner document. Whether the answer *implies* a new Domain member, an event, a
wire member, or an ADR is a consequence the task **reports** — and if the answer
requires one, that is a separate follow-up task created after the decision is
recorded, not an act of this task. Authoring the answer itself is the Product
Owner's, not an agent's (`AGENTS.md` §7).

Note the one case that would re-type this task: if the Product Owner's answer
**changes how an already-documented mechanic behaves** rather than filling in an
unspecified procedure, the correctness route is a `GAMEPLAY-CHANGE` task
(`TASK_TYPES.md` §2, `development/gameplay-change.md` §3). That determination is
made **after** the answer is obtained and is reported here, not pre-judged —
Crit's roll procedure is currently *absent*, not *different*, so the default
expectation is DOCUMENTATION. See "Type Re-Classification Condition".

**This task authors no procedure and no value.** It must not choose a roll
formula, a comparison operator, a range, an RNG stream name, a draw count, or a
ruling on whether a DoT tick can crit. Its job is to state the gap precisely,
present the viable options with their documented consequences, obtain the
decision, and record it — the same shape TASK-108 used to capture its D-1/D-2
answers and TASK-104 used for its nine decisions.

**No balance value is authored.** Crit's default chance (5%) and multiplier
(1.5×) are `COMBAT_RULES.md` §1.1/§3.3's and are **not** this task's to change.
Iron Fang's +10 percentage points is `CARD_RULES.md` §4.1's and is **not** this
task's to change. Only the *procedure* that consumes those values is at issue.

---

## Objective

Resolve, from authoritative documents and a human/Product-Owner answer only, the
**combat-side RNG consumption contract** that the PetSkillCast implementation
requires and that `docs/` does not currently define: when a Crit roll occurs and
what it consumes, so that Crit and Burn effects (Iron Fang's Crit increase and
Inferno's Burn) can be implemented deterministically without inventing a
gameplay procedure.

Concretely, this task must make the decision points in §"Decision Inputs"
explicit and evidenced against `docs/`, obtain a human/Product-Owner answer for
each (or an explicit recorded deferral), and record each settled answer in its
single canonical owner document — without authoring any formula, stream,
counter, or balance value.

---

## Authoritative References

### The gap and its owners (READ ONLY — this task records, it does not redefine)

- `docs/01-game-design/COMBAT_RULES.md` **§3.3** — "Critical Hits", the whole of
  what Crit is documented to be: item 1 places the evaluation "once per damage
  instance, after Element Modifier and before Defense Mitigation (i.e., inside
  'Other Modifiers', step 4)"; item 2 gives the multiplier; item 3 names the
  modifying sources. **The section never mentions RNG, a draw, a stream, or a
  range** — the word "roll" is used at L140 and L172 as an undefined premise.
  **This is the section that must be extended by the answer.** §3.1 fixes the
  step order; §3 step 4 lists "Crit multiplier if a Crit roll succeeds".
- `docs/01-game-design/COMBAT_RULES.md` **§5.1 / §5.2 item 3** — Burn's
  definition ("damage-over-time, ticks once per resolved Turn at End Turn
  (`GAME_RULES.md` §17 step 19a), Element = Hỏa") and the rule that a DoT tick
  "goes through the Damage Pipeline (§3) ... but do not consume Combo". Because
  §3.3 places Crit *inside* that pipeline's step 4, **every Burn tick traverses
  the Crit evaluation region** — the interaction that makes D-3 a blocking
  question rather than a nicety. **§5.3.4** explicitly carves Burn's tick
  schedule out of the duration rule and hands it to `BOSS_RULES.md` §6.3.1
  item 1.
- `docs/01-game-design/COMBAT_RULES.md` **§8** — "All formulas in this document
  execute server-side" and the determinism/authority statement the answer must
  not contradict.
- `docs/01-game-design/GAME_RULES.md` **§17 step 19a** — the Burn/DoT tick
  position ("tick each active damage-over-time Status Effect (e.g. Burn) exactly
  once, through the Damage Pipeline"; "the last combat effect of the Turn — it
  runs after the Boss Response (step 18)"). **§17's step list contains no Crit
  step** and `COMBAT_RULES.md` never maps pipeline step 4 onto a numbered §17
  step — so "which §17 step is the Crit roll" has no documented answer. **§18**
  — server authority; **§16** — the canonical event list (no Crit event);
  **§12** — the Power range.
- `docs/01-game-design/MATCH3_RULES.md` **§7.2** — "What Consumes RNG, and What
  Must Not". Under "Consumes RNG during gameplay" it lists exactly two entries:
  "cascade spawn into empty cells (§4.5 item 4)" and "initial board generation
  (§1.2.1.4)". **Crit and Burn appear in neither list** — note the exclusivity
  claim in item 1 is scoped to *board* operations, so §7.2 is silent rather
  than prohibitive for combat-side draws. **§7.1** — the reproducibility
  guarantee. **§1.2.1.4 item 2** — the documented precedent that one bounded
  selection "may consume more than one PRNG output" (rejection sampling), which
  is why "evaluated once" must not be read as "one draw".
- `docs/01-game-design/BOSS_RULES.md` **§6.3.1 item 1** — the owner of Burn's
  tick schedule, including the tick-by-Turn mapping for Hỏa Long's Flame Burst
  ("Burn tick #1 (50 damage) occurs at Turn N step 19a ... Burn tick #2 (50
  damage) occurs at Turn N+1 step 19a, after which the Burn expires before Turn
  N+2"). **This is the precedent for what a complete tick contract looks like**
  and is the model any recorded Burn answer should be consistent with.

### The consumers (what the contract must be able to express)

- `docs/01-game-design/CARD_RULES.md` **§4.1** — Iron Fang ("Deal 120 damage
  ...; increase Crit chance by 10 percentage points for the next attack only")
  and Inferno ("Deal 100 Fire (Hỏa) damage ...; apply Burn", "Burn: 50 damage
  per tick for 2 Turns"). **The two Cards whose effects are blocked.** §4.1's
  closing note fixes Iron Fang's Crit increase as **independent** of Bạch Hổ's
  Passive config value — the answer must preserve that independence.
- `docs/01-game-design/PASSIVE_RULES.md` **§7 / §8** — Bạch Hổ's "next attack
  gains increased Crit chance" Passive, and the same `NextAttack` scope token
  Iron Fang's Crit element stores. A second documented Crit-chance modifier
  source the answer must be able to express.
- `src/backend/GameServer.Domain/Combat/DamagePipeline.cs` — the implemented
  pipeline. Its own documentation states the current contract plainly:
  "**no Crit roll**: `PetState.Crit` is deliberately not read here", step 4 is
  "pass-through 1.00× in MVP (no Relic/Passive/Buff/Crit)", and
  `NoOtherModifiers = 1.00` is the constant the Crit factor would replace. The
  Shield absorption step (§4 items 2–5) is already implemented here.
- `src/backend/GameServer.Domain/Cards/CardEffectType.cs` — the closed effect
  set `Heal | Shield | Power | Damage | Burn | Crit`. Its `Damage`, `Burn`, and
  `Crit` members each record "Storing this identity executes nothing", "no Crit
  roll exists in MVP", and that the pipeline's "Crit steps remain owned by"
  `COMBAT_RULES.md`. **It names the owner of the missing procedure.**
- `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs` — the implemented
  Basic Card resolver. Its `switch` over `CardEffectType` handles `Heal`,
  `Shield`, and `Power` and **throws `InvalidOperationException` for any other
  effect** ("Unsupported effectType {…} for Basic Card"). A Pet Skill Card
  carrying `Damage`/`Burn`/`Crit` therefore cannot resolve today.
- `src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs` — the
  structured effect carrier, including `Burn`'s `duration` and `Crit`'s
  `scope`. Its documentation states: "`duration` ... records a duration the
  owning documents already state; no Burn instance is created and no tick is
  performed" and "`scope` ... performs no Crit roll and touches no crit-chance
  formula".

### Technical contracts the answer must not contradict

- `docs/02-technical/TDD.md` **§6** — the decisive section. Item 1 names Gem
  spawn randomness; item 4 states "**Gameplay resolution consumes the same
  PRNG, at one documented point.** During a board resolution the only operation
  that draws is the cascade spawn into empty cells — exactly one selection per
  spawned cell ... Validation, match detection, Special Gem creation and
  activation, gravity, Combo calculation, event creation, serialization,
  delivery, and client rendering draw nothing (`MATCH3_RULES.md` §7.2 owns the
  full list)." **Crit is not in that list and is not named anywhere in §6.**
  Items 2–3 pin the seed's ownership and forbid client-side randomness.
- `docs/02-technical/GAME_STATE.md` **§2.6.1** (`RngSeed` — type, server source,
  "never rewritten after creation"), **§2.6.2** (`RngState` — the state +
  stream-selector pair; item 2 "`RngState` is the single advancement point of
  the battle's randomness ... there is no second, hidden generator"; item 3
  "**Advanced by consumption.** `RngState` changes only when a value is drawn;
  drawing nothing leaves it unchanged. It is not advanced on a timer, on a Turn
  boundary, or as a side effect of unrelated state changes"; item 4 snapshot
  semantics), **§2.6.3** (PRNG identity is `ADR-009`'s; `Random.Shared`,
  `System.Random`, `Guid`-derived randomness, and timestamps "must never produce
  a gameplay-relevant value"), **§2.6.4** (value conversion). **§2.6.2
  enumerates no consuming operation and defines no Crit draw point or counter**
  — it is a state contract, and the answer must extend the *consumer* list, not
  the state shape.
- `docs/02-technical/GAME_STATE.md` **§5.1.1** — the `StatusEffects[]`
  lifecycle: item 1 (Apply is a "set", not an increment; item 6 in §2.3.1
  permits at most one instance per identity), items 4–5 (expiry is a removal in
  the same step-19a pass, never a stored zero), item 6 (deterministic
  Id-ordinal pass order), **item 10 "Nothing here is published. This lifecycle
  adds no event, no payload member, and no SignalR method"** — the statement any
  Burn-event answer must respect. **§2.3.1** — the instance schema, including
  `Magnitude` being "typed but not interpreted here" and `Type` selecting
  exactly one duration model.
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.13** — the `DamageCalculated`
  wire shape, whose `otherModifiers` member is defined as "Step 4 — the combined
  Relic / Passive / Buff / Debuff / **Crit** factor (pass-through `1.00` in
  MVP)" and is "required even so". **This is the wire consequence of D-4:** a
  Crit is currently indistinguishable from any other step-4 modifier on the
  wire. **§4.2 item 2** — what the state push explicitly does not carry.
- `docs/03-decisions/ADR/ADR-009-deterministic-prng.md` — the PRNG choice, its
  state/output widths, advancement step, and next-value semantics. **The answer
  must draw from this one generator and must not introduce a second**
  (`AGENTS.md` §11, `GAME_STATE.md` §2.6.3).
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; every roll is made server-side.
- `docs/03-decisions/README.md` **§8** — "Known Open Items (Not ADRs)", which
  must be checked before asserting a decision needs an ADR.

### Scope, governance, and precedent

- `docs/00-overview/MVP_SCOPE.md` **§1** (Pets, `Pet Element, Passive,
  Signature Skill`; Cards; Combat — `Crit` and `Status Effects` are named), §2
  (OUT), §4 (unlisted is not implicitly IN).
- `docs/00-overview/ROADMAP.md` — Phase 1 names "One Pet fully implemented
  (Element, Passive, Signature Skill)" and "Damage pipeline incl. Element
  Modifier"; "No Relics yet".
- `tasks/completed/TASK-107-implement-basic-cardcast-server-path.md` — the
  completed Basic CardCast path. Its Out of Scope explicitly excludes
  `PetSkillCast` and defers Pet Skill Cards. **IMMUTABLE.**
- `tasks/completed/TASK-112-encode-effectdefinition-domain-types-and-rows.md` —
  the DONE encoding task whose migration records in-line "NOTHING IS APPLIED
  HERE. No Burn instance is created, no tick is scheduled, no Crit roll exists".
  **IMMUTABLE.**
- `tasks/completed/TASK-091-resolve-status-effect-tick-timing.md` — the DONE
  decision that anchored the Burn/DoT tick at §17 step 19a and whose Priority
  field recorded "every Status Effect / DoT implementation task is" gated by it.
  The direct precedent for this task's shape. **IMMUTABLE.**
- `tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md` — the
  decision-capture precedent for how a Product Owner answer is recorded verbatim
  and how a deferral is recorded as a deferral. Read-only.
- `tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — the nine-decision register precedent (see also the audit below). Read-only.
- `AGENTS.md` §4 (conflict resolution), §7 (never invent a rule or content),
  §9 (anti-overengineering), §11 (Determinism & RNG — "If a task seems to need
  RNG behavior not covered by existing docs: stop and report per §7, don't
  invent one"), §17 (documentation change rule), §18 (ADR rule), §20 (stop
  conditions — "Missing rule", "Ambiguous requirement"); `tasks/README.md` §9
  (no business-rule duplication in task files), §12 (skill budget).

**ADR check (to be confirmed, not assumed, by the executing agent):** this task
is expected to require **no** ADR, because `ADR-009` already owns the PRNG and
the answer records *where* and *how often* the existing generator is consumed —
a rule detail inside `COMBAT_RULES.md` §3.3, not a change to the generator, its
state, or the authoritative model. If the obtained answer does require one (for
example, if it introduces a separate stream that `ADR-009` does not
contemplate), that is **reported** and becomes a separate task — it is not
authored here (`AGENTS.md` §18). `docs/03-decisions/README.md` §8 must be
checked before asserting an ADR is needed.

---

## Current State

The combat-side RNG consumption contract does not exist. Verified directly in
the working tree:

```text
docs/01-game-design/COMBAT_RULES.md §3.3   Crit is DEFINED as an effect
                                           ("evaluated once per damage instance,
                                           after Element Modifier and before
                                           Defense Mitigation") and a chance
                                           stat (§1.1, default 5%) and a
                                           multiplier (§3.3 item 2, default
                                           1.5×) — but the ROLL PROCEDURE is
                                           absent. §3.3 never mentions RNG,
                                           seed, state, draw, stream, or range.
                                           Repo-wide, the phrase "Crit roll"
                                           occurs only twice (L140, L172), both
                                           as an undefined premise.

docs/01-game-design/MATCH3_RULES.md §7.2   "Consumes RNG during gameplay" lists
                                           exactly TWO entries: cascade spawn
                                           (§4.5 item 4) and initial board
                                           generation (§1.2.1.4). Neither is
                                           combat-side. Crit and Burn appear in
                                           neither the consumes nor the
                                           must-not-consume list.

docs/02-technical/TDD.md §6 item 4         "During a board resolution the only
                                           operation that draws is the cascade
                                           spawn into empty cells." Crit is not
                                           named in §6 at all.

docs/02-technical/GAME_STATE.md §2.6.2     Defines RngState's representation and
                                           advancement ("changes only when a
                                           value is drawn") but enumerates NO
                                           consuming operation and defines NO
                                           Crit draw point and NO Crit counter.
                                           Crit's six occurrences in this
                                           document are all the integer chance
                                           stat (L346, 913, 950, 968, 970,
                                           972-973).

docs/01-game-design/COMBAT_RULES.md §5.2   A DoT tick "goes through the Damage
  item 3                                   Pipeline (§3)". §3.3 places the Crit
                                           evaluation INSIDE that pipeline's
                                           step 4. Therefore every Burn tick
                                           traverses the Crit evaluation region,
                                           and no document states whether a DoT
                                           tick can crit.

src/.../Combat/DamagePipeline.cs           "no Crit roll: PetState.Crit is
                                           deliberately not read here";
                                           step 4 is pass-through 1.00×.
src/.../Cards/CardEffectType.cs            Damage/Burn/Crit members each record
                                           "Storing this identity executes
                                           nothing" / "no Crit roll exists in
                                           MVP".
src/.../Cards/CardCastExecutor.cs          Handles Heal/Shield/Power and THROWS
                                           for every other effect type.
src/.../Battle/StatusEffectLifecycle.cs     Does not tick Burn's damage.
```

**The gap.** A PetSkillCast implementation must resolve, for Iron Fang and
Inferno:

```text
When is the Crit roll made?           → pipeline step 4 position known; §17 step and
                                        the procedure itself NOT SPECIFIED
What does it consume?                 → NOT SPECIFIED (no stream, no draw count)
Can a Burn/DoT tick crit?             → NOT SPECIFIED
How is the result represented?        → NOT SPECIFIED beyond a combined
                                        otherModifiers factor
```

`COMBAT_RULES.md` §3.3's placement of Crit inside step 4 is a resolved *game
rule*. "The runtime can determine, deterministically and reproducibly, whether a
given damage instance crit" is an **unresolved contract**. An agent cannot close
this by choosing a comparison, and it cannot pick a draw point without silently
altering the RNG sequence every other mechanic depends on
(`GAME_STATE.md` §2.6.2 item 4's snapshot/recovery guarantee, `TDD.md` §6
item 5's reproducibility guarantee).

**Why this blocks PetSkillCast specifically.** The PetSkillCast wire contract is
complete (`SIGNALR_PROTOCOL.md` §2, §3.2.21, §3.2.22), all three Pet Skill Cards
have authored Power costs and magnitudes (`CARD_RULES.md` §4.1, TASK-110), and
all three rows are encoded (TASK-112). Tidal Barrier's effects (Heal, Shield) are
already implemented and would resolve today. **Inferno and Iron Fang would not:**
Inferno carries `Damage` + `Burn`; Iron Fang carries `Damage` + `Crit`. The
implemented `CardCastExecutor` throws on all three of those effect identities.

**Burn cannot be split off and implemented alone.** A Burn tick runs the full
Damage Pipeline (`COMBAT_RULES.md` §5.2 item 3), and the Crit evaluation sits
inside that pipeline (`§3.3`). Implementing Burn before this contract is resolved
would either bake in an undocumented "DoT ticks never crit" assumption or leave
the interaction dangling — both are `AGENTS.md` §20 stop conditions.

> **RESOLVED 2026-10-11 — see D-3 and D-6.** The Product Owner ruled that Burn
> ticks **can** crit. This paragraph's diagnosis was correct, and the decided
> answer confirms its conclusion: Burn is **not** independently implementable
> and cannot be split off ahead of Crit. The paragraph is retained as the
> pre-decision evidence; D-6 records the supersession of the "cannot" branch.

**What this does NOT change.** Burn's tick *timing* is fully specified
(`GAME_RULES.md` §17 step 19a; `COMBAT_RULES.md` §5.1; `BOSS_RULES.md` §6.3.1
item 1) and must not be re-decided. Burn's duration model, expiry, and
one-instance-per-identity rules are fully specified (`COMBAT_RULES.md` §5.3
DR1–DR5; `GAME_STATE.md` §2.3.1, §5.1.1) and are frozen. Crit's chance,
multiplier, and pipeline position are specified and are not at issue. The sole
open question is the **RNG consumption procedure** they both touch.

---

## Decision Inputs

<!--
  A PRODUCT OWNER / HUMAN must answer these. An agent must NOT answer them.
  Choosing, recommending, ranking, or defaulting any answer is the single
  prohibited action of this task (AGENTS.md §7, §20).
-->

Each item states the question, the documented evidence for and against each
viable option, and what the option would cost downstream. **The options are
presented as evidence, not as a recommendation.** An agent must not rank them.

### D-1 — What is the Crit roll procedure?

**ANSWERED — Product Owner, 2026-10-11.** The answer is recorded verbatim in
"Product Owner Decisions" below; the evidence below is retained as the evidence
the decision was taken against.

**Decision: the Crit roll is a bounded RNG selection over the bound 100, and the
roll succeeds when the accepted value is strictly less than the Crit stat.**

```text
one bounded RNG selection over bound 100
        ↓
accepted value V   (an integer in [0, 100) )
        ↓
V < current Crit stat  →  Crit succeeds
V ≥ current Crit stat  →  Crit fails
```

The **"bounded RNG selection"** term is not a new mechanism: it is the
authoritative operation `MATCH3_RULES.md` §1.2.1.4 item 2 already names —
`pcg32_boundedrand_r`'s rejection method, which draws one PRNG output, repeats
until the value is at or above the rejection threshold, then reduces the
accepted value **modulo the bound**. The repository's own precedent is
therefore inherited rather than re-invented, and the reduction is
**modulo after acceptance, not a bare modulo of the first draw**.

The comparison is **strictly less than (`<`)** the Crit stat. This is the
comparison-operator and endpoint-inclusivity question §3.3 left open, and it is
what makes `§1.1`'s integer-percentage unit (`Crit = 5` meaning 5%, not `0.05`)
directly usable as the comparison threshold: over 100 equally likely values, a
`Crit` stat of `5` accepts `{0,1,2,3,4}` and produces an observed rate of
exactly 5%.

**This authors no balance value.** The Crit stat's default (5%) remains
`COMBAT_RULES.md` §1.1's, the multiplier (1.5×) remains `§3.3` item 2's, and
Iron Fang's +10 percentage points remains `CARD_RULES.md` §4.1's. Only the
missing *procedure* that consumes those already-authored values is supplied.
See "Balance-Value Boundary" below.

<!--
  RETAINED AS EVIDENCE — the options presented to the Product Owner, not as a
  ranking or recommendation.
-->

```text
Question: COMBAT_RULES.md §3.3 says "On a successful Crit roll, damage is
          multiplied by a Crit Multiplier". What IS the roll? No document
          states its arithmetic, its range, its endpoint inclusivity, or its
          comparison operator.

Evidence: §1.1 defines Crit as an integer percentage ("critical hit chance (%)",
          MVP default 5%). GAME_STATE.md §5.2 item 4 records that `Sequence`
          counts actions, not draws. MATCH3_RULES.md §1.2.1.4 item 2 is the
          repository's only documented example of turning a PRNG output into a
          decision, and it is a *bounded selection with rejection sampling* —
          so the repository's existing precedent is not a plain modulo.

Consequence: the procedure determines whether a 5% Crit stat produces a 5%
          observed rate, and whether the result is exactly reproducible from
          RngState.
```

### D-2 — When is the Crit roll drawn, and how many draws does it consume?

**ANSWERED — Product Owner, 2026-10-11.**

**Decision: one bounded RNG selection over the bound 100, drawn per damage
instance, inside Damage Pipeline step 4.**

```text
each damage instance
        ↓
pipeline step 4  ("× Other Modifiers")
        ↓
exactly 1 bounded RNG selection over bound 100
        ↓
Crit factor 1.5× on success, 1.00× on failure
```

The draw point is **inside step 4**, which is exactly the position
`COMBAT_RULES.md` §3.3 item 1 already fixes ("once per damage instance, after
Element Modifier and before Defense Mitigation"). The decision resolves the
question `§3.3` left open — *where in the runtime* that evaluation consumes
randomness — without moving the evaluation.

```text
Frequency (already documented, §3.3 item 1)   once per damage instance
Count     (decided here)                      exactly 1 bounded selection
Scope     (decided here)                      every damage instance that
                                              traverses the pipeline
```

**"Once per damage instance" is frequency, not count.** The distinction matters
because `MATCH3_RULES.md` §1.2.1.4 item 2 documents a single selection
consuming more than one PRNG output under rejection sampling. The decision
therefore fixes the **selection** count at one — which, as `§1.2.1.4` item 3
states for the bound-100 case, is deterministic per RNG state and is the
reproducible quantity — rather than asserting a fixed number of 32-bit PRNG
outputs, which would contradict that document.

**Stream: the existing single `RngState` pair.** The Crit draw reads and writes
the same one `GAME_STATE.md` §2.6.2 pair the cascade spawn and initial board
fill use. This decision introduces:

```text
no second generator      (AGENTS.md §11, ADR-009 §1, GAME_STATE.md §2.6.3)
no second seed           (GAME_STATE.md §2.6.1 item 3)
no third RNG value       (GAME_STATE.md §2.6.2 item 1)
no new RNG state field   (GAME_STATE.md §0 item 5, §2.6.2 item 1)
no crit counter          (none was ever defined; none is added)
```

The Crit draw is an **additional consumption site on the existing stream**, not
an addition to the stream's shape. It advances `RngState` by exactly the one
bounded selection, at that moment, and in the pipeline's existing step order —
so `GAME_STATE.md` §2.6.2 item 3's "advanced by consumption, not on a timer, a
Turn boundary, or as a side effect of unrelated state changes" is satisfied,
and §2.6.2 item 4's snapshot/recovery guarantee holds because the advanced
state is carried in `BattleState` like any other draw's result.

**Sequencing consequence.** Because the Crit draw shares the one stream with
`MATCH3_RULES.md` §4.5 item 4's cascade spawn, the two now interleave in one
battle. This is a consequence of the decision, not a change to either draw
point: neither is reordered, re-counted, or respecified. It does mean a battle
that produces a combat-owned draw reaches a different `RngState` than one that
does not, which is the intended reading of "RngState is the single advancement
point" (`§2.6.2` item 2).

**Reported, not decided here:** whether `MATCH3_RULES.md` §7.2's
board-scoped consumer list and `TDD.md` §6 item 4's "the only operation that
draws" sentence now need a combat-side cross-reference is **downstream
documentation work** — see "Follow-Up Tasks Required". TASK-113 does not edit
either document.

<!--
  RETAINED AS EVIDENCE — the options presented to the Product Owner, not as a
  ranking or recommendation.
-->

```text
Question: At which point does the battle consume the Crit randomness, and
          exactly how many PRNG selections does that consumption advance
          RngState by?

Evidence: COMBAT_RULES.md §3.3 item 1 fixes the PIPELINE position ("once per
          damage instance, after Element Modifier and before Defense
          Mitigation"). It does NOT map onto a numbered GAME_RULES.md §17 step,
          and §17's list contains no Crit step. GAME_STATE.md §2.6.2 item 3
          states RngState "changes only when a value is drawn" and is "not
          advanced on a timer, on a Turn boundary, or as a side effect of
          unrelated state changes" — so a draw point must be an explicit
          consumption site, and the count must be stated. "Evaluated once per
          damage instance" (frequency) is NOT "one draw" (count): MATCH3_RULES.md
          §1.2.1.4 item 2 documents a single selection consuming more than one
          PRNG output.

Consequence: the draw point and count are what make the battle reproducible
          (TDD.md §6 items 2/5, GAME_STATE.md §2.6.2 item 4). Getting them wrong
          silently desynchronizes every later draw in the battle, including Gem
          spawns. This is why an agent must not choose it.
```

### D-3 — Can a damage-over-time tick (Burn) crit?

**ANSWERED — Product Owner, 2026-10-11.**

**Decision: Burn ticks can crit. A DoT tick traverses the full Damage Pipeline,
including the Crit evaluation in step 4.**

```text
Direct Damage   → full pipeline, step 4 Crit evaluation applies
Burn / DoT tick → full pipeline, step 4 Crit evaluation applies   ← DECIDED
```

The decision resolves the interaction the current documents leave dangling:
`COMBAT_RULES.md` §5.2 item 3 sends a DoT tick "through the Damage Pipeline
(§3)", and `§3.3` places the Crit evaluation inside that pipeline's step 4. The
Product Owner's ruling is that **step 4's Crit evaluation is not neutralized for
DoT ticks** — unlike step 2's Combo Modifier, which `§5.2` item 3 *does*
explicitly neutralize for them.

**This is a ruling on eligibility only. It reopens nothing frozen.** In
particular it does not touch, restate, or extend:

```text
Burn's tick position       GAME_RULES.md §17 step 19a          UNCHANGED
Burn's tick schedule       BOSS_RULES.md §6.3.1 item 1         UNCHANGED
Burn's duration model      COMBAT_RULES.md §5.3 DR1–DR5       UNCHANGED
Burn's expiry / identity   GAME_STATE.md §2.3.1, §5.1.1        UNCHANGED
Burn's own magnitudes      CARD_RULES.md §4.1, BOSS_RULES.md   UNCHANGED
```

D-3 asks only *whether a tick crits*; it does not reopen *when a tick fires*.

**Consequence — Burn does NOT become independently implementable.** This is the
opposite of the outcome the "cannot crit" option would have produced, and it is
recorded prominently because it changes the dependency graph the downstream
work must respect:

```text
Burn tick
   ↓ traverses step 4
   ↓ therefore performs a Crit roll  (D-3)
   ↓ therefore needs D-1's procedure and D-2's draw point/count
   ↓ therefore ALSO consumes RngState, on the same single stream
```

So both Inferno's Burn and Iron Fang's Crit depend on D-1 **and** D-2, and a
standalone Burn implementation task cannot be split off ahead of them. Burn
magnitudes remain authored and unchanged (`CARD_RULES.md` §4.1 — 50 per tick
for 2 Turns; `BOSS_RULES.md` §6.3.1 item 1 — Flame Burst's ticks); the decision
establishes only that a tick that crits is multiplied per `§3.3` item 2. This
is a *procedure* consequence, not a balance change: no magnitude is authored,
altered, or confirmed here.

**Reported, not decided here:** which document states the DoT-can-crit rule.
`§3.3` (Critical Hits) and `§5.2` (Status Rules) are both plausible owners, and
`documentation-change.md` §2 / §3 permits exactly one. The owner choice is
recorded under "Follow-Up Tasks Required" as a downstream decision, because
making it here would be authoring a documentation structure choice this task's
Product Owner input did not cover.

<!--
  RETAINED AS EVIDENCE — the options presented to the Product Owner, not as a
  ranking or recommendation.
-->

```text
Question: A Burn tick "goes through the Damage Pipeline (§3)"
          (COMBAT_RULES.md §5.2 item 3), and the Crit evaluation is inside that
          pipeline (§3.3, step 4). Can a DoT tick therefore crit and be
          multiplied?

Evidence: No document answers this. COMBAT_RULES.md §5.2 item 3 neutralizes
          exactly ONE step for DoT ticks — it sets the Combo Modifier to
          1/neutral — and says nothing about step 4. COMBAT_RULES.md §3.4 does
          state the Boss side's step 4 as "1.0 (MVP: no Relic/Passive/Buff
          modifiers on Boss side)", which is a *different* question (whether the
          Boss can crit) and is likewise not stated as a Crit ruling. The
          Burn magnitudes in CARD_RULES.md §4.1 ("50 damage per tick ... fixed")
          and BOSS_RULES.md §6.3.1 item 1 ("Fixed 50 damage per tick (does not
          scale with Boss ATK, Pet ATK, percentage MaxHP, or elemental
          multipliers)") use the word "fixed", but neither says "cannot crit" —
          and both are content values, not pipeline rulings.

Consequence: this is the highest-risk question, because it decides whether
          Burn is implementable at all before Crit is. If DoT ticks cannot
          crit, Burn is independently implementable and this answer should say
          so explicitly; if they can, Burn depends on D-1/D-2 as well. Leaving
          it unstated forces every implementer to guess.
```

### D-4 — How is a Crit result represented in state, events, and on the wire?

**ANSWERED — Product Owner, 2026-10-11.**

**Decision: no separate Crit representation is required. `otherModifiers` remains
the sole carrier of the step-4 factor.**

```text
SIGNALR_PROTOCOL.md §3.2.13
        otherModifiers : double
        = the combined Relic / Passive / Buff / Debuff / Crit factor
        → unchanged; a Crit is NOT separately observable
```

A Crit is therefore **deliberately indistinguishable** from any other step-4
modifier on the wire, in `BattleState`, and in the Battle Event stream — and
that is the decided contract, not an omission. The consequence is recorded
explicitly so no downstream task reads it as a gap to fill:

```text
no Crit event            (GAME_RULES.md §16's canonical list is unchanged)
no Crit payload member   (GAME_EVENTS.md, SIGNALR_PROTOCOL.md unchanged)
no Crit state member     (GAME_STATE.md unchanged; §0 item 5 satisfied)
```

`DamageCalculated` continues to report the full step breakdown as
`COMBAT_RULES.md` §3.4 describes; the Crit factor is visible in it **only as a
combined multiplier**, exactly as it is today. A test or log distinguishes a
crit from a non-crit only by the factor's value (`1.5×` vs `1.00×` per
`§3.3` item 2's authored multiplier) or by recomputing the roll — not by any
dedicated field.

**No downstream protocol work is required by this decision.** The follow-up
documentation task must record this explicitly wherever the Crit procedure is
written down, so a later implementer does not "fix" the combined
representation into a new wire member: adding one would be a
`SIGNALR_PROTOCOL.md` / `GAME_EVENTS.md` / `GAME_STATE.md` contract change
requiring its own Product Owner decision and its own task.

<!--
  RETAINED AS EVIDENCE — the options presented to the Product Owner, not as a
  ranking or recommendation.
-->

```text
Question: How does a client, a log, or a test distinguish "this damage instance
          crit" from "it did not"?

Evidence: There is NO `CritOccurred` / `CriticalHit` / `CritRolled` event.
          GAME_RULES.md §16's canonical event list contains no Crit event, and
          GAME_EVENTS.md contains zero Crit references. The only trace is
          SIGNALR_PROTOCOL.md §3.2.13's `otherModifiers`, defined as "the
          combined Relic / Passive / Buff / Debuff / Crit factor (pass-through
          1.00 in MVP)" — a single combined double in which a Crit is
          indistinguishable from any other step-4 modifier. `DamageCalculated`
          is documented to report "the full breakdown" (COMBAT_RULES.md §3.4).
          No battle-state member carries a Crit outcome; `PetState.Crit` is the
          chance stat only.

Consequence: if the answer requires a new event, a new payload member, or a
          new state member, that is a SIGNALR_PROTOCOL.md / GAME_EVENTS.md /
          GAME_STATE.md contract change and a SEPARATE follow-up task — it is
          reported here, never authored here. This task records only whether
          such a change is required.
```

### D-5 — Does Burn application or expiry emit any Battle Event?

**ANSWERED — Product Owner, 2026-10-11: the documented position is CONFIRMED.**

**Decision: Burn application and Burn expiry emit NO Battle Event. Only the
tick's damage surfaces, through the existing Damage events.**

Per the task's §10 instruction, this is recorded as **derived from existing
authoritative documents**, not as a new Product Owner ruling — the Product
Owner confirmed the derived reading rather than re-deciding the semantics:

```text
Derived from:  GAME_STATE.md §5.1.1 item 10
                 "Nothing here is published. This lifecycle adds no event,
                  no payload member, and no SignalR method."
               GAME_RULES.md §16 — canonical event list, no status event
               GAME_EVENTS.md    — zero "Burn", zero "StatusEffect" occurrences
               COMBAT_RULES.md §4 item 4 — the ShieldDepleted precedent:
                 "is NOT a Battle Event: no event of that name exists in
                  GAME_RULES.md §16's canonical list or in GAME_EVENTS.md §2"
Confirmed by:  Product Owner, 2026-10-11
```

```text
Burn applied  → no event
Burn tick     → damage reported through the existing
                DamageCalculated / DamageDealt / DamageTaken events
Burn expired  → no event
```

**No downstream event-contract work is required by this decision.** No event
name is created, proposed, or endorsed by TASK-113 — per the task's §10
instruction, no new event names are introduced and none is presented as if it
were already part of the contract. A future task that wants an application or
expiry event must obtain its own Product Owner decision and would then be a
`GAME_RULES.md` §16 + `GAME_EVENTS.md` + possibly `SIGNALR_PROTOCOL.md`
contract change — which this decision explicitly does not authorize.

<!--
  RETAINED AS EVIDENCE — the options presented to the Product Owner, not as a
  ranking or recommendation.
-->

```text
Question: Does applying a Burn instance, or its expiry at step 19a, emit a
          Battle Event?

Evidence: GAME_EVENTS.md contains ZERO occurrences of "Burn" and ZERO of
          "StatusEffect". GAME_RULES.md §16's canonical list has no
          StatusApplied / StatusExpired / StatusTicked event. GAME_STATE.md
          §5.1.1 item 10 states for this lifecycle: "Nothing here is published.
          This lifecycle adds no event, no payload member, and no SignalR
          method". `COMBAT_RULES.md` §4 item 4 establishes the directly
          analogous precedent — `ShieldDepleted` "is NOT a Battle Event: no
          event of that name exists in GAME_RULES.md §16's canonical list or in
          GAME_EVENTS.md §2". The DoT tick's *damage*, by contrast, is reported
          through the generic DamageCalculated/DamageDealt/DamageTaken events.

Determination to CONFIRM (not decide): the documented position appears to be
          that Burn application and expiry emit NO event, and that only the
          tick's damage surfaces, through the existing Damage events. This item
          exists so the Product Owner can CONFIRM that reading explicitly or
          correct it — because if a Burn event IS wanted, it is a
          GAME_EVENTS.md + SIGNALR_PROTOCOL.md contract change (a separate
          task), and the implementer must not invent one.
```

### D-6 — Recorded Consequence: Burn Is NOT Independently Implementable (from D-3)

Recorded here because it **reverses an expectation the task's own
§"Current State" expressed**, and the reversal must not be silent.

§"Current State" previously framed D-3 as possibly allowing Burn to be split
off and implemented alone. The Product Owner's D-3 answer (Burn ticks **can**
crit) does the opposite: a Burn tick performs a Crit roll, so Burn depends on
D-1 and D-2 in full.

```text
Earlier framing (now superseded):
  "If DoT ticks cannot crit, Burn is independently implementable"

Actual decided state:
  Burn ticks can crit  →  Burn depends on D-1 + D-2  →  Burn is NOT
  independently implementable, and cannot be split off ahead of Crit
```

This supersedes only that **expectation**, not the underlying evidence: the
diagnosis that a Burn tick traverses the Crit evaluation region of step 4 was
correct, and it is precisely why the answer matters. `§5.2` item 3's Combo
neutralization remains the sole step neutralized for DoT ticks.

### D-7 — Balance-Value Boundary (recorded, not to be crossed)

```text
These decisions define RUNTIME PROCEDURE only.
Every balance value remains owned by its current authoritative document.
```

```text
Crit chance default (5%)          COMBAT_RULES.md §1.1        UNCHANGED
Crit multiplier (1.5×)            COMBAT_RULES.md §3.3 item 2 UNCHANGED
Crit pipeline position (step 4)   COMBAT_RULES.md §3.3 item 1 UNCHANGED
Iron Fang Crit +10 pct points     CARD_RULES.md §4.1          UNCHANGED
Inferno Damage 100 / Burn 50×2    CARD_RULES.md §4.1          UNCHANGED
Flame Burst Burn magnitudes       BOSS_RULES.md §6.3.1 item 1 UNCHANGED
Bạch Hổ Passive Crit value        PASSIVE_RULES.md §7/§8      UNCHANGED
```

No balance value is authored, changed, or confirmed by TASK-113. The decisions
supply only *how* the runtime consumes the already-authored values — the
procedure that consumes a Crit stat and a Crit multiplier, not the values
themselves. `COMBAT_RULES.md` §1.1's note that changing balance values is "a
configuration change, not a combat pipeline redesign" is the boundary this task
stays on the procedural side of.

### D-8 — Balance-Value Boundary applied to D-3's "fixed" wording

D-3's answer means a Burn tick **can** be multiplied by the Crit factor, which
must not be read as contradicting `CARD_RULES.md` §4.1's / `BOSS_RULES.md`
§6.3.1 item 1's use of "fixed". Those two statements fix the Burn **magnitude**
and explicitly scope what it does *not* scale with:

```text
BOSS_RULES.md §6.3.1 item 1 scopes "fixed" as: does not scale with
  Boss ATK, Pet ATK, percentage MaxHP, or elemental multipliers
```

Pipeline step 4 is **none of those four** — it is the pipeline's own modifier
step, and a Crit multiplier entering it is the pipeline behaving as
`COMBAT_RULES.md` §3.3 item 2 documents ("damage is multiplied by a Crit
Multiplier"). Recording this mapping explicitly prevents a downstream task from
mis-reading D-3 as a balance change. **If the Product Owner intends Burn ticks
to be exempt from the Crit multiplier after all, that is the D-3 answer to
obtain — an agent must not reconcile the two documents by choosing one**
(`AGENTS.md` §4). As decided, the two are consistent and neither is edited.

---

## Scope

### In Scope

1. **Present the gap precisely and completely**, evidenced by file + section,
   for each decision point in §"Decision Inputs" — including the four-way
   tension between `COMBAT_RULES.md` §3.3 (Crit exists, inside step 4),
   `TDD.md` §6 item 4 / `MATCH3_RULES.md` §7.2 (cascade spawn is the only
   gameplay draw), `GAME_STATE.md` §2.6.2 (no consuming operation enumerated,
   no Crit counter), and `COMBAT_RULES.md` §5.2 item 3 (DoT ticks run the same
   pipeline).
2. **Obtain a human/Product-Owner answer** for D-1, D-2, and D-3 (and explicit
   determinations for D-4 and D-5). Record each answer **verbatim**, with its
   date and its author, in this task's Completion Evidence — the TASK-104 /
   TASK-108 precedent. If no answer is supplied, the task reports itself as
   awaiting input and stops.
3. **Record each settled answer**, without duplication
   (`documentation/documentation-change.md` §2 — one concept, one owner). The
   candidate owners:
   - `docs/01-game-design/COMBAT_RULES.md` **§3.3** — the canonical owner of
     Critical Hits. This is where the Crit roll *procedure*, its draw point, and
     its draw count belong, because §3.3 already owns everything else about
     Crit.
   - `docs/01-game-design/COMBAT_RULES.md` **§5.2** — the owner of Status Effect
     rules, if the D-3 answer (DoT-can-crit) must be stated as a Status Effect
     rule rather than a Crit rule. Choose one owner; do not state it in both.
   - `docs/01-game-design/MATCH3_RULES.md` **§7.2** — **only if** the answer
     changes the *board* consumption list. §7.2's exclusivity claim is
     board-scoped, so a combat-side draw point may not require touching it; if
     it does, that is a cross-reference correction and is in scope as a
     consequence of the recorded answer, not as an independent edit.
   - `docs/02-technical/TDD.md` **§6** and `docs/02-technical/GAME_STATE.md`
     **§2.6.2** — **only if** the answer makes their statements incomplete or
     false. `TDD.md` §6 item 4 currently says "the only operation that draws"
     during a board resolution; `GAME_STATE.md` §2.6.2 item 3 describes
     advancement without enumerating consumers. Correcting a statement the
     answer invalidates is a required consequence of recording it
     (`AGENTS.md` §17); adding a *new* rule to these documents is not.
   - The `.ai/` or `tasks/` layer is **not** an owner for any of this.
4. **Explicitly determine and record whether `ADR-009` already covers the
   answer**, or whether a new/updated ADR is required. If one is required,
   **report it** for a separate task; do not author it (`AGENTS.md` §18).
5. **State explicitly whether the recorded answer requires a follow-up task**
   — a new event, a new payload member, a new state member, a schema change, or
   an ADR — and if so describe that task's scope in the Completion Evidence so
   it can be created separately. **Do not create it as part of this task, and
   do not implement it.**
6. **Record the D-4 and D-5 determinations** so the wire and event contracts
   survive this task unchanged unless the Product Owner explicitly changes them.

### Out of Scope

**Explicit prohibitions (stated as required):**

```text
No speculative architecture.
No undocumented gameplay behavior.
No client-authoritative state.
No undocumented events.
No undocumented SignalR methods.
No new gameplay rules beyond the RNG procedure the Product Owner supplies.
```

- **Authoring the answer.** Choosing a roll formula, a comparison operator, a
  range, an RNG stream, a draw count, a draw point, or a DoT-can-crit ruling is
  the single prohibited act of this task (`AGENTS.md` §7, §11, §20). An agent
  that finds §"Decision Inputs" unanswered must report the task as awaiting
  input — not answer it, not rank the options, not pick a default.
- **Implementing anything.** Zero files under `src/` or `tests/`. No Crit roll,
  no Burn tick, no `PetSkillCast` method, no Card-effect resolver extension, no
  `BattleEvent` member, no wire-projection arm.
- **Any balance value.** Crit's default chance (5%) and multiplier (1.5×) are
  `COMBAT_RULES.md` §1.1/§3.3's. Iron Fang's +10 percentage points and Burn's
  50-per-tick are `CARD_RULES.md` §4.1's. Burn's 50/2-Turns and Flame Burst's
  150 are `BOSS_RULES.md` §6.3.1's. **None is authored, changed, or confirmed
  by this task.**
- **Burn's already-frozen semantics.** The tick *position* (`GAME_RULES.md` §17
  step 19a), the tick *schedule* (`BOSS_RULES.md` §6.3.1 item 1), the duration
  counter and its single decrement (DR1–DR5), expiry-as-removal at 0
  (`§5.3 DR5`, `GAME_STATE.md` §5.1.1 items 4–5), and the
  one-instance-per-identity rule (`§2.3.1 item 6`) are all resolved. **This task
  must not re-decide, restate, or extend any of them.** D-3 asks only whether a
  tick *crits*; it does not reopen when a tick fires.
- **Shield.** `COMBAT_RULES.md` §4 is frozen (TASK-105, TASK-102). A Crit or
  Burn gap is not a license to touch Shield.
- **Tidal Barrier, Thanh Xà, and Sơn Hùng.** Tidal Barrier's effects (Heal,
  Shield) are authored and implemented; its magnitude is authored
  (`CARD_RULES.md` §4.1). Thanh Xà and Sơn Hùng Signature Skills remain TBD
  content (`PET_RULES.md` §8) — no placeholder, no invented value.
- **The `PetSkillCast` implementation itself**, and any other Pet Skill Card
  work. This task unblocks it; it does not perform it.
- **Relics.** `ROADMAP.md` Phase 1 states "No Relics yet". "Assassin Eye" and
  the deferred "Burning Curse" are Phase 2 and are **not** this task's to
  resolve; they are cited only as Crit/Burn modifier sources the answer must be
  able to express.
- **Any new gameplay rule, Card, Pet, Relic, Element, Status Effect type,
  resource, or progression system**; any change to Match-3, Swap, Combo,
  Passive, Boss Response, or the Damage Pipeline formula's specified steps.
- **Any SignalR method, event, wire member, Redis key, API endpoint, or client
  work.** If the D-4/D-5 answers imply one, it is **reported** for a separate
  task.
- **Any ADR** — unless the obtained answer genuinely requires one, in which case
  it is reported for a separate task (`AGENTS.md` §18).
- **Modifying TASK-036, TASK-079, TASK-099, TASK-102, TASK-103, TASK-104,
  TASK-106, TASK-108**, or any `tasks/completed/*` file. Read-only.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [ ] The gap is stated with file + section evidence for all five decision
      points, including the explicit finding that `TDD.md` §6 item 4 and
      `MATCH3_RULES.md` §7.2 enumerate exactly two RNG draw points and neither
      is combat-side.
- [ ] A human/Product-Owner answer for **D-1**, **D-2**, and **D-3** is obtained
      and recorded **verbatim**, with date and author, in the Completion
      Evidence — or the task records itself as awaiting input and does not claim
      a resolution.
- [ ] Explicit determinations are recorded for **D-4** and **D-5**.
- [ ] Each settled answer is recorded in **exactly one** canonical owner
      document, by reference and section, with no concept duplicated across
      documents (`documentation-change.md` §2).
- [ ] The recorded answer introduces **no second RNG mechanism**: it consumes
      the single `RngState` pair `GAME_STATE.md` §2.6 and `ADR-009` define, and
      adds no second generator, seed, or state field.
- [ ] The recorded answer is **deterministic and reproducible**: it states a
      definite draw point and draw count such that the same initial state plus
      the same ordered accepted actions yields the same result
      (`TDD.md` §6 item 5, `GAME_STATE.md` §2.6.2 item 4).
- [ ] The ADR question is **checked, not assumed**: `docs/03-decisions/README.md`
      §8 is consulted and the Completion Evidence records either that no ADR is
      required or that one is required and is reported for a separate task.
- [ ] If any answer requires a new event, payload member, state member, schema
      change, or ADR, that requirement is **described in the Completion
      Evidence for a separate task** and **not applied here**.
- [ ] **`COMBAT_RULES.md` §3.3's existing Critical Hits definition is not
      altered** beyond adding the roll procedure the answer supplies — the
      pipeline position, the multiplier, and the modifier-source list are
      unchanged.
- [ ] **`COMBAT_RULES.md` §4 (Shield) is byte-identical** before and after
      (verified by SHA256).
- [ ] **Burn's frozen semantics are byte-identical**: `GAME_RULES.md` §17
      step 19a, `BOSS_RULES.md` §6.3.1 item 1, `COMBAT_RULES.md` §5.3 DR1–DR5,
      and `GAME_STATE.md` §2.3.1 / §5.1.1 are unchanged (verified by SHA256).
- [ ] **No balance value is authored, changed, or confirmed**: no Crit chance,
      Crit multiplier, Burn magnitude, Burn duration, or Card magnitude differs
      from its authored value.
- [ ] **Zero files under `src/` or `tests/` are modified.**
- [ ] **Zero ADRs are created or edited** by this task.
- [ ] TASK-036, TASK-079, TASK-099, TASK-102, TASK-103, TASK-104, TASK-106,
      TASK-108, and every `tasks/completed/*` file are **byte-identical**.
- [ ] **No new gameplay rule beyond the RNG procedure is introduced**; no Card,
      Pet, Relic, Element, Status Effect type, resource, or progression system
      is added or changed.
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).
- [ ] Quality review checklist passes (`quality/review.md` §1).

### Explicit Constraints

```text
No speculative architecture.
No undocumented gameplay behavior.
No client-authoritative state.
No PostgreSQL BattleState persistence.
No undocumented SignalR methods.
No unrelated refactoring.
No authored Crit procedure.
No authored RNG stream or draw count.
No authored DoT-can-crit ruling.
No balance value change.
No Burn tick-timing change.
No Shield change.
No docs/ rule change beyond recording the answer.
No ADR.
No src/ or tests/ change.
```

---

## Affected Files & Areas

```text
[ ] src/                                                    — NONE
[ ] tests/                                                  — NONE
[x] docs/01-game-design/COMBAT_RULES.md                     — the canonical
      owner: §3.3 gains the recorded Crit roll procedure, draw point, and draw
      count; §5.2 gains the recorded DoT-can-crit rule **only if** the answer
      states it there rather than in §3.3 (choose one owner, not both). §4
      (Shield) and §5.3 (duration) must remain byte-identical.
[ ] docs/01-game-design/GAME_RULES.md                       — NONE expected.
      §17's step list and §16's event list are not this task's to extend; if
      the answer implies either, it is REPORTED for a separate task.
[ ] docs/01-game-design/MATCH3_RULES.md                     — NONE unless the
      answer invalidates §7.2's board-scoped exclusivity claim, in which case
      only that cross-reference is corrected.
[ ] docs/01-game-design/BOSS_RULES.md                       — NONE. §6.3.1
      item 1's Burn tick schedule is the frozen precedent, not an edit target.
[ ] docs/01-game-design/CARD_RULES.md                       — NONE. §4.1's
      magnitudes are authored and unaltered.
[ ] docs/02-technical/TDD.md                                — the recorded
      answer's consequence **only if** §6 item 4's "the only operation that
      draws" statement becomes incomplete or false.
[ ] docs/02-technical/GAME_STATE.md                         — the recorded
      answer's consequence **only if** §2.6.2's advancement statement requires
      the consumer to be named. No new state member is proposed; §0 item 5 is
      the rule any answer must respect.
[ ] docs/02-technical/GAME_EVENTS.md                        — NONE expected.
      Any event consequence is REPORTED, not applied.
[ ] docs/02-technical/SIGNALR_PROTOCOL.md                   — NONE expected.
      §3.2.13's `otherModifiers` already carries the combined step-4 factor;
      any wire consequence is REPORTED, not applied.
[ ] docs/00-overview/                                       — NONE
[ ] docs/03-decisions/ADR/                                  — NONE (no ADR is
      expected to be required; see the ADR check. If one is, it is REPORTED.)
[x] tasks/backlog/TASK-113-resolve-combat-rng-draw-contract.md
      (this file: Status, the five decision points, the recorded answers, and
       the Completion Evidence)
```

**Note on the `docs/` entries.** Exactly one owner document is expected to
change (`COMBAT_RULES.md` §3.3). The remaining `[ ]` entries are conditional
consequences of the recorded answer, and each is checked rather than assumed.
An entry that resolves to "no change needed" must be recorded as such in the
Completion Evidence with the reason — the `AGENTS.md` §12 change-impact chain.

---

## Implementation Notes

- **The work is evidence and recording, not authoring.** Read
  `COMBAT_RULES.md` §3.3, `TDD.md` §6, `MATCH3_RULES.md` §7.2, and
  `GAME_STATE.md` §2.6 side by side, and state what each does and does not
  carry. Then obtain the answer.
- **Do not answer §"Decision Inputs".** This is the task's one hard rule. An
  agent that supplies a roll formula "to be helpful" has violated `AGENTS.md`
  §7 and §11 and produced a contract the Product Owner never approved. If the
  answers are absent, record the task as awaiting input and stop cleanly.
- **Preserve the distinction between the game rule and its procedure.** "Crit
  is evaluated once per damage instance inside pipeline step 4, and multiplies
  damage by 1.5× on success" (`COMBAT_RULES.md` §3.3) is a **resolved game
  rule**. "The runtime can determine deterministically and reproducibly whether
  a Crit succeeded, at a stated draw point, consuming a stated number of
  selections" is an **unresolved contract**. Do not let the recording of the
  second look like a change to the first.
- **Do not read TASK-107's completion as covering this.** TASK-107's own scope
  explicitly excluded `PetSkillCast`, Crit, and Burn. It is a correct execution
  manifest for the Basic Card half; the gap is downstream of it and was not
  visible until the Pet Skill half was scoped. Report it as a discovery of this
  pass, not as a defect in TASK-107.
- **The highest-risk interaction is D-3.** A Burn tick traverses the Crit
  evaluation region of the pipeline. If D-3 is left unanswered, neither Burn nor
  Crit can be implemented, and an implementer will be forced to guess. State
  this prominently in the evidence presented with the decision set.
- **One concept, one owner.** A decision about *when randomness is consumed*
  belongs with Crit in `COMBAT_RULES.md` §3.3 (or, for the DoT ruling, in §5.2)
  — not duplicated into `TDD.md` §6 and `GAME_STATE.md` §2.6.2 as well. Those
  two own the *state* contract and should be touched only if the answer makes
  them inaccurate.
- **Cite, do not restate.** Use the `<c>COMBAT_RULES.md</c> §3.3` citation
  idiom. Never copy a Crit percentage, a multiplier, a Burn magnitude, or a
  formula into this task file (`AGENTS.md` §9, `tasks/README.md` §9). The quoted
  fragments in §"Current State" are quoted **as evidence of the gap** — keep
  that framing and do not extend them into a table of values.
- **Check the ADR question rather than assuming it.**
  `docs/03-decisions/README.md` §8's "Known Open Items (Not ADRs)" is the
  precedent for recording something as deliberately *not* an ADR. `ADR-009`
  already owns the PRNG; the expected answer extends a consumer, not the
  generator. If the answer does require an ADR, report it — do not write it.

---

## Follow-Up Tasks Required (REPORTED, NOT CREATED)

TASK-113 records decisions only. It creates no task (`AGENTS.md` §16 — report,
do not fix; the FINAL RULE flow — "Separate authoritative documentation
workflow"). Whether these become files is the orchestrator's/reviewer's act,
not this task's.

### FU-1 — Authoritative documentation task (the immediate successor)

**Type:** DOCUMENTATION (`TASK_TYPES.md` §2,
`documentation/documentation-change.md`). **Primary agent:** review.

**Scope — record the five decisions in exactly one canonical owner each:**

```text
D-1 / D-2  →  COMBAT_RULES.md §3.3 (the canonical owner of Critical Hits —
              it already owns the pipeline position, the multiplier, and the
              modifier-source list). Add the roll procedure, the draw point,
              and the draw count.
D-3        →  exactly ONE of COMBAT_RULES.md §3.3 or §5.2 — the owner choice
              is itself a decision the follow-up task must make and record,
              not duplicate into both (documentation-change.md §2/§3).
D-4        →  no change required. Record that the combined otherModifiers
              carrier is sufficient, so a later implementer does not add a
              wire member (SIGNALR_PROTOCOL.md §3.2.13 stays as-is).
D-5        →  no change required. Already stated by GAME_STATE.md §5.1.1
              item 10; do not restate it anywhere.
```

**Cross-reference corrections the decision makes necessary** (these statements
become incomplete or narrow — correct them only as far as accuracy requires,
per `AGENTS.md` §17 and `documentation-change.md` §2):

```text
TDD.md §6 item 4          "the only operation that draws" — scope to board
                          resolution or add the combat-side consumer.
MATCH3_RULES.md §7.2      consumer list is board-scoped and likely stays TRUE;
                          confirm and cross-reference, do not restate the Crit
                          rule there.
GAME_STATE.md §2.6.2      name the consumer without adding a state field
                          (§0 item 5 still binds).
```

**Constraints the follow-up task inherits:** `COMBAT_RULES.md` §4 (Shield) and
§5.3 (duration DR1–DR5), `GAME_RULES.md` §17 step 19a, `BOSS_RULES.md` §6.3.1
item 1, and `CARD_RULES.md` §4.1 must remain byte-identical. No balance value
may be authored or changed. `GAME_RULES.md` §16's event list must not be
extended.

### FU-2 — Crit / Burn / PetSkillCast implementation task

**Blocked until FU-1 lands**, because implementation must follow authoritative
documentation (`AGENTS.md` §17 step 2 before step 5, `TASK_TYPES.md` §2's
FEATURE rule — the mechanic must exist in `docs/` first). Do **not** create a
PetSkillCast, Crit, or Burn implementation task from TASK-113; the decisions
must first become authoritative contract input.

Scope it will be able to express, now that the procedure exists:

```text
Inferno    — Damage 100 Flat + Burn (50 / 2 Turns), whose ticks now crit (D-3)
Iron Fang  — Damage 120 Flat + Crit +10 pct points, scope NextAttack
Tidal Barrier — still BLOCKED on its own unauthored Shield magnitude
                (CARD_RULES.md §4.1, TASK-104 B-4) — D-1..D-5 do NOT unblock it
```

Implementation will need to reach: `DamagePipeline.cs` step 4 (the Crit factor
replacing the `NoOtherModifiers` constant), `CardCastExecutor.cs` (the three
`CardEffectType` members it currently throws on), `StatusEffectLifecycle.cs`
(the Burn tick through the pipeline at step 19a), and the PetSkillCast path —
none of which TASK-113 touches.

**Note on sequencing risk:** because D-3 makes Burn depend on D-1/D-2, Burn
cannot be split off as a smaller first implementation task. The smallest
coherent unit is Crit + the step-4 draw together.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). Known and already
  reported, none belonging to this task: the `SIGNALR_PROTOCOL.md` "§3.3" and
  `BOSS_RULES.md` §4 citation nits; the `BattleStateJson.cs` items
  TASK-097/TASK-098 reported; the §2.1 hub-method table covering `Swap` only;
  TASK-099's unexecuted Completion Evidence; TASK-104's stale "RE-TYPING
  REQUIRED" note (superseded by TASK-105); TASK-103's and TASK-106's
  deliberately-unasserted terminal states; and the `TASK-079` → `TASK-099`
  dependency chain.

---

## Testing Requirements

This task changes no code and authors no rule procedure itself, so it produces
no unit, integration, or gameplay test. Its verification is a **documentation
consistency audit**, a **change-isolation check**, and a **scope validation**,
at the depth `core/validation.md` §2 requires for a MEDIUM-risk DOCUMENTATION
task.

### Required Verification

```text
[x] Documentation consistency audit — the recorded answer vs. every document
                                      that states or consumes it: COMBAT_RULES.md
                                      §3.3 ↔ §5.2 ↔ TDD.md §6 ↔ MATCH3_RULES.md
                                      §7.2 ↔ GAME_STATE.md §2.6 ↔ ADR-009 ↔
                                      SIGNALR_PROTOCOL.md §3.2.13. Confirm no
                                      rule is duplicated and no two documents
                                      disagree after the change.
[x] Change-isolation check          — SHA256 of COMBAT_RULES.md, TDD.md,
                                      MATCH3_RULES.md, GAME_STATE.md, GAME_RULES.md,
                                      BOSS_RULES.md, and CARD_RULES.md before and
                                      after; only the owner(s) the answer
                                      actually requires may differ, and
                                      COMBAT_RULES.md §4 (Shield), §5.3
                                      (duration), GAME_RULES.md §17 step 19a,
                                      BOSS_RULES.md §6.3.1, and CARD_RULES.md
                                      §4.1 must be byte-identical.
[x] Gap-evidence re-verification    — independently re-confirm the gap still
                                      exists as stated (repo-wide search for a
                                      Crit roll procedure and for a combat-side
                                      RNG draw point returns nothing), so the
                                      task is not resolved against a stale
                                      premise.
[x] Determinism review              — confirm the recorded answer names a single
                                      draw point and a definite draw count, and
                                      introduces no second generator, seed, or
                                      state field (TDD.md §6, GAME_STATE.md
                                      §2.6.2, ADR-009).
[x] Scope validation                — MVP_SCOPE.md §1/§2;
                                      quality/scope-validation
[ ] Unit tests                      — N/A (no code)
[ ] Integration tests               — N/A (no code)
[ ] Gameplay scenarios              — N/A (no code, no rule change)
```

### Key Edge Cases

- **A partial answer.** If D-1 and D-2 are answered but D-3 is not (or vice
  versa), record what was answered, state plainly what remains open, and leave
  the PetSkillCast task blocked. Do not infer the unanswered part — D-3 is the
  one that decides whether Burn is independently implementable.
- **An answer that implies a second RNG mechanism** (a separate crit stream,
  seed, or counter). That contradicts `GAME_STATE.md` §2.6.2 item 1 ("no third
  RNG value exists") and `AGENTS.md` §11 → **STOP per `AGENTS.md` §4** and
  report both sources rather than choosing one.
- **An answer that changes the existing board draw points.** If the Crit draw
  point would reorder or re-count the cascade spawn's consumption, that alters
  Gem generation and is a `GAMEPLAY-CHANGE`, not a DOCUMENTATION task — STOP and
  report (`TASK_TYPES.md` §2).
- **An answer that contradicts `ADR-009`.** Report per `AGENTS.md` §4/§18; do
  not edit the ADR.
- **An answer that requires a new event or payload member** (D-4). Report it;
  `GAME_RULES.md` §16 and `GAME_EVENTS.md` §2 are the owners and the change is a
  separate task.
- **An answer that requires a new state member** (a stored Crit outcome).
  `GAME_STATE.md` §0 item 5 forbids a parallel representation → STOP and report.
- **The answer changes how an already-documented mechanic behaves** rather than
  filling an unspecified procedure → this task re-classifies to
  `GAMEPLAY-CHANGE` (`TASK_TYPES.md` §2, `development/gameplay-change.md` §3).
  Report the re-typing; do not apply the behavior change here.
- **The gap turns out not to exist** — i.e. a Crit RNG contract is found already
  defined somewhere this pass missed → **STOP and report.** Do not record a
  decision for a question that is already answered; report the location instead
  and let the lifecycle close this task as unnecessary.

---

## Stop Conditions

Universal `AGENTS.md` §20 / `.ai/README.md` §13 stops always apply.
Task-specific:

1. **If no Product Owner / human answer to §"Decision Inputs" is supplied** —
   **STOP and report the task as awaiting input.** Do not answer it, do not rank
   the options, do not pick a default, and do not author a procedure
   (`AGENTS.md` §7, §11, §20 — "Missing rule"). Record the awaiting-input state
   in Completion Evidence and leave the file in `backlog/`.
2. **If resolving the gap would require authoring a game rule, a formula, a
   magnitude, a balance value, an RNG stream, or a draw count** — **STOP**
   (`AGENTS.md` §7). Crit's chance, multiplier, and pipeline position are
   already authored in `COMBAT_RULES.md` §3.3; this task resolves the missing
   *procedure*, not the content.
3. **If the recorded answer contradicts an existing authoritative document** —
   e.g. it introduces a second RNG mechanism, or it cannot express
   `COMBAT_RULES.md` §3.3's placement of Crit inside step 4, or it violates
   `GAME_STATE.md` §0 item 5 — **STOP per `AGENTS.md` §4** and report both
   sources rather than choosing one.
4. **If any part would change Burn's tick timing, duration model, expiry, or
   instance identity** — **STOP**; those are resolved and frozen
   (`GAME_RULES.md` §17 step 19a, `BOSS_RULES.md` §6.3.1 item 1,
   `COMBAT_RULES.md` §5.3, `GAME_STATE.md` §5.1.1).
5. **If any part would change Shield semantics or touch
   `COMBAT_RULES.md` §4** — **STOP**; Shield is resolved and frozen.
6. **If any part would author Tidal Barrier's, Thanh Xà's, or Sơn Hùng's
   content** — **STOP**; those are separate content decisions
   (`CARD_RULES.md` §4.1, `PET_RULES.md` §8).
7. **If the answer requires a new event, payload member, wire member, SignalR
   method, Redis key, or API endpoint** — **STOP for this task's boundary**;
   record the requirement and its scope in Completion Evidence for a separate
   implementation task. Do not author it here.
8. **If the answer requires a new state member or a schema change** — **STOP for
   this task's boundary**; report it. Do not write a migration and do not edit
   `GAME_STATE.md`'s state description beyond recording the decision.
9. **If the answer requires an ADR** — **STOP and report** (`AGENTS.md` §18);
   do not create one here.
10. **If the answer changes an already-documented mechanic's behavior rather
    than filling an unspecified procedure** — **STOP and report the
    re-classification** to `GAMEPLAY-CHANGE` (`TASK_TYPES.md` §2); do not apply
    the behavior change in a DOCUMENTATION task.
11. **If satisfying any criterion would require modifying TASK-036, TASK-079,
    TASK-099, TASK-102, TASK-103, TASK-104, TASK-106, TASK-108, or any
    `tasks/completed/*` file** — **STOP**; report the stale or superseded
    statement instead (`TASK_LIFECYCLE.md` §3 — completed tasks are immutable).
12. **If the PetSkillCast implementation is being scoped inside this task** —
    **STOP**; this task unblocks it and does not perform it
    (`tasks/README.md` §13 — do not create a mega-task).
13. **If the change would cross into `MVP_SCOPE.md` §2's OUT list, or reach an
    unlisted (FUTURE) system** — **STOP** (`AGENTS.md` §8, `MVP_SCOPE.md` §4).
14. **If the task exceeds 7 skills or crosses an uncoupled architectural
    boundary** — **STOP and decompose** (`tasks/README.md` §13).
15. **If the gap turns out not to exist** — i.e. a Crit RNG consumption
    contract is found already defined somewhere this pass missed — **STOP and
    report.** Do not record a decision for a question that is already answered.

When stopped, report the exact condition and **do not invent a resolution**.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Record a STOP report here instead if §"Decision Inputs" is unanswered — do not
  claim a resolution that was not made, and do not author the answer.
-->

**Picker-up note.** Record before editing: (a) the SHA256 of
`docs/01-game-design/COMBAT_RULES.md`, `docs/01-game-design/GAME_RULES.md`,
`docs/01-game-design/MATCH3_RULES.md`, `docs/01-game-design/BOSS_RULES.md`,
`docs/01-game-design/CARD_RULES.md`, `docs/02-technical/TDD.md`,
`docs/02-technical/GAME_STATE.md`, `docs/02-technical/GAME_EVENTS.md`,
`docs/02-technical/SIGNALR_PROTOCOL.md`, and
`docs/03-decisions/ADR/ADR-009-deterministic-prng.md` as the change-isolation
baseline; (b) the output of `git status`. The working tree carries uncommitted
changes from earlier tasks, so the baseline is the working-tree content at
pickup, **not** `HEAD`. No build or test run is required or expected (no code
change).

**Baseline recorded at pickup (2026-10-11) — `docs/` and the primary sources.**
The eleven baseline SHA256 values are listed in full under "Contract
Preservation Verification"; all eleven are identical at pickup and after this
task's edit. `git status` at pickup showed the working tree's uncommitted
changes from the TASK-107/109/111/112 sequence (`docs/01-game-design/CARD_RULES.md`,
`docs/02-technical/DATABASE.md`, several `src/backend/**` and `tests/backend/**`
files, the TASK-107/TASK-109 `backlog → completed` renames, and untracked
`src/backend/GameServer.Domain/Cards/*`), consistent with the task's warning
that the baseline is the working tree, not `HEAD`. **TASK-113 added exactly one
entry to that set: its own task file.** No `docs/`, `src/`, or `tests/` file
was touched.

### Product Owner Decisions (recorded verbatim, per the TASK-104 / TASK-108 precedent)

```text
Recorded by:  Product Owner (via requester)   Date: 2026-10-11

D-1 — Crit Roll Procedure
  "Bounded RNG selection over 100, success if value < Crit stat"
  Product Owner selected: "Bounded RNG selection over 100, success if value <
  Crit stat".

  Recorded as supplied; the semantic meaning is preserved, not paraphrased:
    roll            = one bounded RNG selection over the bound 100
    accepted value  = V, an integer in [0, 100)
    success         = V < current Crit stat
    failure         = V >= current Crit stat

D-2 — Crit Draw Point and Draw Count
  Product Owner selected: "Per damage instance, inside pipeline step 4;
  exactly 1 bounded selection, bound 100".

  Recorded as supplied; the semantic meaning is preserved, not paraphrased:
    draw point  = inside Damage Pipeline step 4 ("x Other Modifiers")
    count       = exactly 1 bounded RNG selection per damage instance
    stream      = the existing single RngState pair (GAME_STATE.md §2.6.2)
    scope       = every damage instance that traverses the pipeline

D-3 — DoT (Burn) Tick Critical Hit Eligibility
  Product Owner selected: "Burn ticks CAN crit — they traverse the full
  pipeline including the Crit evaluation".

  Recorded as supplied; the semantic meaning is preserved, not paraphrased:
    a Burn / DoT tick traverses the full Damage Pipeline
    step 4's Crit evaluation is NOT neutralized for DoT ticks
    (contrast step 2's Combo Modifier, which §5.2 item 3 DOES neutralize)

D-4 — Crit Result Representation (state / event / wire)
  Product Owner selected: "No separate Crit representation required —
  otherModifiers remains the sole carrier".

  Recorded as supplied; the semantic meaning is preserved, not paraphrased:
    SIGNALR_PROTOCOL.md §3.2.13's otherModifiers remains the sole carrier
    no Crit event, no Crit payload member, no Crit state member
    a Crit stays indistinguishable from any other step-4 modifier

D-5 — Burn Application / Expiry Event
  Product Owner selected: "Confirmed — Burn application and expiry emit NO
  event, derived from existing docs, not re-decided".

  Recorded as supplied. Per the task's §10 instruction this is logged as
  DERIVED from GAME_STATE.md §5.1.1 item 10 / GAME_RULES.md §16 /
  GAME_EVENTS.md / COMBAT_RULES.md §4 item 4 and CONFIRMED by the Product
  Owner — not authored as a new rule, and not a re-decision of Burn semantics.
```

### Outcome

```text
RESOLVED (all five decisions answered; no decision left ambiguous)

Documents changed:   NONE. Zero files under docs/ were modified by this task.
                     The recorded decisions are input to a downstream
                     documentation task — see "Follow-Up Tasks Required".
Follow-up task(s):   YES — two, described under "Follow-Up Tasks Required":
                     (1) the authoritative documentation task that records the
                         Crit roll procedure + draw point into COMBAT_RULES.md
                         §3.3 (and the DoT-can-crit rule into exactly one of
                         §3.3 / §5.2), plus the dependent cross-reference
                         corrections in TDD.md §6 / MATCH3_RULES.md §7.2 /
                         GAME_STATE.md §2.6.2 that the decision makes stale;
                     (2) the PetSkillCast / Crit / Burn implementation task
                         that consumes the recorded procedure.
                     Neither is created by TASK-113 (see §"Files Changed" and
                     the FINAL RULE flow).
ADR required:        NO — checked, not assumed.
                     ADR-009 already owns the PRNG (algorithm, state/output
                     widths, advancement). These decisions record WHERE and HOW
                     OFTEN the one existing generator is consumed — a consumer
                     rule, not a change to the generator, its state, its
                     advancement, or the authoritative model. No second
                     stream, seed, or state field is introduced, so
                     ADR-009 is extended in no way and contradicted in no way.
                     docs/03-decisions/README.md §8 ("Known Open Items (Not
                     ADRs)") was consulted; the only PRNG-related entry there
                     concerns ADR-009's own Proposed->Accepted status, which
                     is unaffected by these decisions.
                     NOTE (reported, not acted on): these decisions record a
                     consumer of the PRNG in COMBAT_RULES.md, which slightly
                     strengthens the case for ADR-009's status promotion — but
                     that promotion is a separate act and is NOT performed or
                     required here.
Re-classification:   NONE.
                     NOT TRIGGERED. Every answer fills a procedure that was
                     ABSENT; none changes an already-documented mechanic's
                     behavior. Specifically verified:
                       - Crit's chance, multiplier, and pipeline position
                         (COMBAT_RULES.md §1.1, §3.3 items 1-2) are unchanged.
                       - Burn's tick timing, duration, expiry, and instance
                         identity (GAME_RULES.md §17 step 19a,
                         BOSS_RULES.md §6.3.1 item 1, COMBAT_RULES.md §5.3,
                         GAME_STATE.md §2.3.1 / §5.1.1) are unchanged.
                       - The Damage Pipeline's steps 1-6 and their order
                         (COMBAT_RULES.md §3, §3.1) are unchanged.
                       - No new RNG state model is introduced.
                     D-3 is the closest call — it resolves an interaction the
                     documents left UNSTATED rather than altering a stated
                     rule (§5.2 item 3 neutralizes step 2 only and is silent on
                     step 4), so it fills a gap and does not change a rule.
                     A GAMEPLAY-CHANGE route would be required only if the
                     Product Owner later rules DoT ticks exempt from the Crit
                     multiplier — see D-8.
```

### Changed Files

- `tasks/backlog/TASK-113-resolve-combat-rng-draw-contract.md` — **the only
  file changed by this task.** Status field; the five decision points in
  §"Decision Inputs" now carry their answers (D-1–D-3, D-4, D-5) with the
  original evidence retained beneath each as retained evidence; three new
  recorded-consequence sections (D-6 superseded-expectation note, D-7 and D-8
  balance-value boundaries); and this Completion Evidence block.
- `NONE` under `docs/`. No authoritative documentation was modified.
- `NONE` under `src/` or `tests/`.

### Validation Results

- `Gap-evidence re-verification` — PASS. Independently re-confirmed the gap
  still exists as stated, against the working tree at pickup rather than a
  stale premise:
  - Repo-wide search for a Crit roll procedure (`Crit roll`,
    `critical hit chance`, bounded-selection language) returns only
    `COMBAT_RULES.md` L53 (the chance stat), L140 and L172 (the undefined
    premise) — no arithmetic, range, or comparison operator anywhere.
  - Repo-wide search for a combat-side RNG draw point returns nothing:
    `MATCH3_RULES.md` §7.2 lists exactly two consumers (cascade spawn §4.5
    item 4, initial board generation §1.2.1.4), both board-side, and
    `TDD.md` §6 item 4 states "the only operation that draws" during a board
    resolution is the cascade spawn. Crit and Burn appear in neither the
    consumes nor the must-not-consume list.
  - `GAME_STATE.md` §2.6.2 enumerates no consuming operation, no Crit draw
    point, and no Crit counter; its six `Crit` occurrences are all the integer
    chance stat (L346, 913, 950, 968, 970, 972-973).
  - `COMBAT_RULES.md` §5.2 item 3 "goes through the Damage Pipeline (§3)" and
    §3.3's placement of Crit inside step 4 both verified verbatim; no document
    states whether a DoT tick can crit.
  - Source-code inspection (verification only — NOT authoritative for
    gameplay semantics): `DamagePipeline.cs` documents "no Crit roll:
    `PetState.Crit` is deliberately not read here" with step 4 as
    pass-through `1.00×`; `CardEffectType.cs`'s `Damage`/`Burn`/`Crit` members
    each record "Storing this identity executes nothing"; `CardCastExecutor.cs`
    handles only `Heal`/`Shield`/`Power`. The gap is real and unimplemented.
- `Change-isolation check` — PASS. SHA256 recorded at pickup for all eleven
  baseline documents (see "Picker-up note" below); re-verified after the edit.
  Only `tasks/backlog/TASK-113-…md` differs. Every frozen contract is
  byte-identical — see "Contract Preservation Verification".
- `Determinism review` — PASS. The recorded answers name a **single** draw
  point (pipeline step 4), a **definite** draw count (exactly 1 bounded RNG
  selection per damage instance), and a **single** stream (the one existing
  `RngState` pair). No second generator, seed, or state field is introduced
  (`TDD.md` §6, `GAME_STATE.md` §2.6.2, `ADR-009`, `AGENTS.md` §11). The same
  initial state plus the same ordered accepted actions therefore yields the
  same result, preserving `TDD.md` §6 item 5's and `GAME_STATE.md` §2.6.2 item
  4's reproducibility guarantees. Bound `100` also avoids the rejection-
  sampling frequency question `MATCH3_RULES.md` §1.2.1.4 item 3 raises for
  bound `3`, and the recorded count is stated in **selections** — the quantity
  that document defines as deterministic — not in 32-bit PRNG outputs.
- `Documentation consistency audit` — PASS, with one cross-reference
  consequence REPORTED not applied (correct for a decision-input task):
  `COMBAT_RULES.md` §3.3 (Crit is evaluated inside step 4) ↔ §5.2 item 3 (DoT
  ticks run the pipeline; step 2 neutralized) ↔ `TDD.md` §6 item 4 ("the only
  operation that draws") ↔ `MATCH3_RULES.md` §7.2 (two board-side consumers)
  ↔ `GAME_STATE.md` §2.6.2 item 3 (advanced by consumption) ↔ `ADR-009`
  ↔ `SIGNALR_PROTOCOL.md` §3.2.13 (`otherModifiers`).
  Result: **no two documents disagree** after the decisions, and **no rule is
  duplicated** by this task (it changed no `docs/` file). The decisions are
  recorded in exactly one place — this task file — pending their transfer to
  the single canonical owner by the follow-up task.
  Recorded cross-reference consequence for the follow-up task: `TDD.md` §6
  item 4's "the only operation that draws" and `MATCH3_RULES.md` §7.2's
  consumer list become **incomplete** once a combat-side draw exists. Both are
  REPORTED here and left byte-identical. `§7.2`'s exclusivity claim is
  board-scoped ("These are the only **board** operations that consume
  randomness"), so it appears to remain **true** as written and may need only
  a cross-reference note rather than a correction — the follow-up task
  determines that (`documentation-change.md` §2, one concept one owner).
- `Scope validation` — PASS. `MVP_SCOPE.md` §1 names Combat (`Crit`, `Status
  Effects`), Pets (`Pet Element, Passive, Signature Skill`), and Cards as IN;
  nothing here reaches §2's OUT list or an unlisted (FUTURE) system
  (`AGENTS.md` §8, `MVP_SCOPE.md` §4). No new system, content, Card, Pet,
  Relic, Element, Status Effect type, resource, or progression system is
  introduced.
- `ADR question` — PASS (checked, not assumed). `docs/03-decisions/README.md`
  §8 consulted; no ADR is required. See "Outcome" above.
- `Source-code boundary` — PASS. Zero files under `src/` or `tests/` modified;
  source was read for verification only.
- `Unit / Integration / Gameplay tests` — N/A. No code and no rule change; no
  test is produced, and the task explicitly requires none.

### Contract Preservation Verification

Verified by SHA256 of the working tree before the edit and after; every entry
below is byte-identical.

```text
COMBAT_RULES.md §4 (Shield)            UNCHANGED (frozen — TASK-105/TASK-102)
COMBAT_RULES.md §5.3 (duration DR1–DR5) UNCHANGED (frozen — TASK-094)
GAME_RULES.md §17 step 19a (Burn tick) UNCHANGED (frozen — TASK-091)
BOSS_RULES.md §6.3.1 item 1 (schedule) UNCHANGED (frozen)
CARD_RULES.md §4.1 (magnitudes)        UNCHANGED (authored — TASK-110)
Crit chance / multiplier               UNCHANGED (no balance value authored)
Burn magnitude / duration              UNCHANGED (no balance value authored)
RNG mechanism                          UNCHANGED (single RngState pair; no second
                                       generator, seed, or state field)
GAME_RULES.md §16 (event list)         UNCHANGED
GAME_EVENTS.md                         UNCHANGED
SIGNALR_PROTOCOL.md                    UNCHANGED
src/                                   UNCHANGED (0 files)
tests/                                 UNCHANGED (0 files)
docs/03-decisions/ADR/                 UNCHANGED (0 ADRs created or edited)
TASK-036/079/099/102/103/104/106/108, tasks/completed/   UNCHANGED

Full-file SHA256 (pickup == post-edit):
258B12A3E53962AD3983DFE6E9895FE4AD07762EF1997FFFF5FDA70802EF0EF6  docs/01-game-design/COMBAT_RULES.md
7514786E52AFE7774CA64451275C66A3CA7BEE8AF2B9AC17D4C0CA96CD143C60  docs/01-game-design/GAME_RULES.md
CB3D567651C439D0B2B7848EF9AD2CFB0E4C1E4ECD6A1891A4C7CF31924B0CD4  docs/01-game-design/MATCH3_RULES.md
3784E6EAC2D355B26435B3D6906894AF822572B870C1858D73B8CCD6AA74E39A  docs/01-game-design/BOSS_RULES.md
E94E63F0D7D7288871371ED1D7078021C03DE904665BA1C5D4CCFA03674F8309  docs/01-game-design/CARD_RULES.md
D8F9ACFA698E8769EFFC67A2ECD47FDE7DE97021D2AB89546930F276B059031C  docs/01-game-design/PASSIVE_RULES.md
DF68851D129F9CAB7F46237AF31B36129C802FF68C534FAF6E3ECFE05B0DA447  docs/02-technical/TDD.md
7004B262B7B4CFDDA7FE479038D9C281299040FF983CDFF828AABE6C6481609B  docs/02-technical/GAME_STATE.md
454A6F82611B927F0C4987A9014ECBE75690AC93147225A490B5C43CF27E75CE  docs/02-technical/GAME_EVENTS.md
AE4FD980C6D5444148093A1BA76831586D45ABA9263A50DDAB447177B4CB2AA1  docs/02-technical/SIGNALR_PROTOCOL.md
00BF4B09B70045C581628C26179D0C4FC6A32C3360320EA7AC254E12CBE33FA4  docs/03-decisions/ADR/ADR-009-deterministic-prng.md
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — no code was written
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no second RNG mechanism introduced (`ADR-009`,
      `GAME_STATE.md` §2.6.2 — the recorded answers name the one existing
      `RngState` pair and add no generator, seed, stream, or state field)
- [x] Confirmed no balance value authored or changed
- [x] Confirmed Burn tick timing, Shield, and all frozen contracts byte-identical
- [x] Confirmed no event, wire member, schema, or ADR consequence applied here
      (D-4 and D-5 both resolve to "no new representation required")

### Type Re-Classification Condition

```text
NOT TRIGGERED — the answers fill an absent procedure, and no documented
behavior is changed.

Each of the five answers supplies a question the documents left UNANSWERED:
  D-1  §3.3 never stated the roll's arithmetic, range, or comparison operator.
  D-2  §3.3 fixed the pipeline position but no runtime draw point or count.
  D-3  no document stated whether a DoT tick can crit (§5.2 item 3 neutralizes
       step 2 only and is silent on step 4).
  D-4  no document stated whether a Crit needs its own representation; the
       answer confirms the existing combined carrier is sufficient.
  D-5  already determined by GAME_STATE.md §5.1.1 item 10 and confirmed.

None of the five alters a rule the documents already state, and none requires
the GAMEPLAY-CHANGE route (TASK_TYPES.md §2,
development/gameplay-change.md §3). Verified against the re-classification
triggers:
  changing existing Crit balance                    NOT DONE
  changing Burn duration                            NOT DONE
  changing Burn tick timing                         NOT DONE
  changing existing Damage Pipeline semantics        NOT DONE — steps 1–6 and
                                                    their order are untouched;
                                                    D-2 locates a draw inside
                                                    the step 4 region §3.3
                                                    already defines
  adding a new RNG state model                      NOT DONE
See also D-8: if the Product Owner later intends DoT ticks to be exempt from
the Crit multiplier, that WOULD be a change to an already-documented interplay
and would require the GAMEPLAY-CHANGE route — recorded, not assumed.
```

### Unrelated Stale Documentation Discovered (REPORTED, NOT CHANGED)

Per `AGENTS.md` §16. None of these belongs to TASK-113; all are reported, not
fixed.

- **`TDD.md` §6 item 4's "the only operation that draws" becomes incomplete.**
  Location: `docs/02-technical/TDD.md` §6 item 4. Impact: once a combat-side
  Crit draw exists, this sentence's scope is narrower than reality. Suggested
  follow-up: the downstream documentation task adds the combat-side consumer or
  narrows the sentence to "during a board resolution" (which it already
  qualifies, so this may resolve to a cross-reference note only) —
  `documentation-change.md` §2.
- **`MATCH3_RULES.md` §7.2's consumer list is board-scoped.** Location: §7.2
  and its item 1. Impact: appears to remain **true** as written (item 1 is
  explicitly scoped to board operations), so a combat-side draw may require
  only a cross-reference, not a correction. Suggested follow-up: confirm and
  cross-reference in the downstream task; do **not** restate the Crit procedure
  there (`documentation-change.md` §2).
- **`GAME_STATE.md` §2.6.2 item 2 does not enumerate consumers.** Location:
  §2.6.2. Impact: the section describes advancement without naming the sites
  that consume, so a reader cannot tell from it alone that combat now draws.
  Suggested follow-up: the downstream task names the consumer, without adding
  a state field.
- **`ADR-009` status is `Proposed`.** Location: `ADR-009` header and its status
  note; `docs/03-decisions/README.md` §8. Impact: unchanged by these decisions,
  but recording a PRNG consumer in `COMBAT_RULES.md` marginally strengthens the
  promotion case. Suggested follow-up: a separate ADR-status task if the
  project wants it; **not** performed here (`AGENTS.md` §18).
- **Pre-existing items already reported, still open, not this task's:**
  Tidal Barrier's unauthored Shield magnitude (`CARD_RULES.md` §4.1, TASK-104
  B-4); Thanh Xà / Sơn Hùng Signature Skill content (`PET_RULES.md` §8); the
  `SIGNALR_PROTOCOL.md` "§3.3" and `BOSS_RULES.md` §4 citation nits; the
  `BattleStateJson.cs` items TASK-097/TASK-098 reported; the §2.1 hub-method
  table covering `Swap` only; TASK-099's unexecuted Completion Evidence;
  TASK-104's stale "RE-TYPING REQUIRED" note (superseded by TASK-105);
  TASK-103's and TASK-106's deliberately-unasserted terminal states; and the
  `TASK-079` → `TASK-099` dependency chain.
