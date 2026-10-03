# TASK-150 — Resolve the Remaining `PowerChanged.source` Semantics for the Boss Drain and Card Power-Charge Mutations

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK IS STRICTLY A DECISION-INPUT TASK.

    Documented ambiguity
            ↓
    Present authoritative evidence
            ↓
    Obtain explicit Product Owner decisions (D-6, D-7, D-8)
            ↓
    Record the decisions in TASK-150
            ↓
    STOP

  THIS TASK MUST NOT APPLY THE DECISION TO AUTHORITATIVE DOCUMENTATION.
  A subsequent documentation/contract task consumes this record and updates the
  canonical documents; a still-later implementation task performs the emission
  work. TASK-150's deliverable is the RECORD, not the documentation change.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine `PowerChanged.source` value-set gap and
  requires the appropriate human/Product-Owner decision. Inventing a `source`
  value, narrowing D-1, or selecting an option because it is easier to
  implement is the single prohibited action of this task (AGENTS.md §7, §20).

  PROVENANCE: identified during the TASK-149-follow-on task-generation pass.
  The Product Owner decision set D-1–D-5 resolved the CORE Finding A ambiguity
  — "every authoritative gameplay mutation of `PetState.Power` must emit
  `PowerChanged` from the stage that owns the mutation" — but that decision's
  scope reaches two mutation sites whose `source` value is not decided:

    D-6  Boss `Drain Power` (step 18b)  — no `"boss"` value exists at all
    D-7  Card `Power Charge` (+25)      — `"card"` is documented as a COST SPEND

  TASK-150 resolves only what D-1–D-5 left open. It does not re-open D-1–D-5,
  does not modify TASK-148, and does not create the downstream documentation or
  implementation task.

  BOUNDARY: decision recording only. Zero files under docs/, src/, or tests/.
  This task creates no ADR, changes no gameplay rule, adds no event, and
  modifies no completed task.
-->

---

## Metadata

```text
Task ID:           TASK-150
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). This task is the
                   DECISION-INPUT half of that workflow only: it obtains and
                   records the Product Owner decisions. It does NOT perform the
                   canonical-owner documentation write that the workflow's
                   later steps describe — that is the subsequent
                   contract-application task's act, consuming this record.
Status:            DONE (the Product Owner supplied explicit answers for all
                   three decision items — D-6, D-7, and D-8. The answers were
                   recorded verbatim in "Decision Record" without
                   reinterpretation or added assumption, and every "Required
                   Decision Coverage" item (D-6.1–D-6.5, D-7.1–D-7.4,
                   D-8.1–D-8.4) is resolved. TASK-150 modified no file other
                   than this one: zero `docs/`, zero `src/`, zero `tests/`, no
                   ADR, and no completed task. The canonical documentation
                   write is the SUBSEQUENT contract-application task's act,
                   consuming this record — TASK-150 does not perform it.
                   Lifecycle reconciliation: recorded DONE while the file
                   remains in tasks/backlog/ per TASK_LIFECYCLE.md §3, matching
                   the TASK-136 precedent for a decision-input task whose
                   deliverable is the record itself.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because D-6 Option A extends a CLOSED wire enum value
                   set owned by SIGNALR_PROTOCOL.md §3.2.24 — the same class of
                   protocol-visible change that required an explicit Product
                   Owner ruling to author — and because D-8 makes the current
                   CardCastExecutor composition non-conforming. Neither
                   consequence is applied by this task; no `docs/` file is
                   modified.)
Priority:          HIGH (the Finding A blocker is now RESOLVED. The subsequent
                   documentation task and backend emission task are both
                   unblocked by this record; this task remains their dependency
                   of record.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: realtime (SIGNALR_PROTOCOL.md §3.2.24 owns the
                   `PowerChanged` wire member table and its `source` value set
                   — consulted to CONFIRM the closed value set and the
                   §3.2.2/§3.2.4 conventions any extension must satisfy, not to
                   author the answer),
                   backend (BattleStateService.cs / CardCastExecutor.cs own the
                   mutation sites; consulted to CONFIRM which sites exist and
                   which stage owns each, not to author the answer),
                   gameplay (GAME_RULES.md §12 owns Power; BOSS_RULES.md §6.3.1
                   owns Drain Power; CARD_RULES.md §2 owns Power Charge —
                   consulted to CONFIRM the documented semantics)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   realtime/realtime-protocol-validation,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      D-1 (resolved — every authoritative `PetState.Power` mutation
                   emits `PowerChanged`),
                   D-2 (resolved — the event carries `delta`, resulting
                   `power`, and `source`),
                   D-3 (resolved — the owning stage emits; no centralized
                   manager),
                   D-4 (resolved — no new event type; existing `PowerChanged`
                   is reused),
                   D-5 (resolved — no client-side Power calculation),
                   TASK-133 (DONE — the Relic stage, the existing `"relic"`
                   emitter, `BattleStateService.cs:1335`),
                   TASK-148 (DONE — client presentation of `PowerChanged`;
                   unaffected by this task and NOT modified by it)
Blocks:            The documentation reconciliation task (must correct
                   `GAME_EVENTS.md` §2 item 4 and `SIGNALR_PROTOCOL.md` §3.2.24
                   item 5 to require owning-stage emission, and must carry the
                   D-6/D-7/D-8 value-set outcome),
                   the backend `PowerChanged` emission implementation task
```

---

## Objective

Obtain and record the explicit Product Owner decisions that resolve the two `PowerChanged.source` semantic gaps the D-1–D-5 decision set left open: what `source` value the Boss `Drain Power` mutation carries (D-6), whether a Card's Power *gain* is a `"card"` mutation (D-7), and — if D-7 is answered yes — how a Card cast whose cost and gain partially offset is reported (D-8).

This task produces a **record only**. It changes no documentation, no source, and no test.

---

## Current State

### Resolved: D-1–D-5 (the core Finding A ambiguity)

The Product Owner resolved the core ambiguity. The governing intent is:

```text
D-1  Every authoritative gameplay mutation of PetState.Power emits
     PowerChanged from the stage that owns the mutation.
D-2  The event carries delta, the resulting power, and source.
D-3  The emitting stage remains responsible for emission; no centralized
     PowerChanged manager is introduced.
D-4  No new SignalR event is introduced; PowerChanged already exists.
D-5  No client-side Power calculation is introduced.
```

### What D-1–D-5 unblock (not this task's work)

D-1–D-5 establish the governing intent that the two conflicting passages
recorded below must be corrected to state. Both currently describe the match
and card sources as *not* emitting:

```text
docs/02-technical/GAME_EVENTS.md
  §2 item 4 (lines 397–403)
    "Whether and when the Power stage emits this event is owned by that stage
     ... while the "match" and "card" sources remain their own stages' and are
     unchanged."

docs/02-technical/SIGNALR_PROTOCOL.md
  §3.2.24 item 5 (lines 1214–1220)
    "The first emitter is GAME_RULES.md §17 step 11's Relic stage ... The
     "match" and "card" sources remain their own stages' and are unchanged by
     that."
```

Correcting those two passages is the **subsequent documentation task's** act.
TASK-150 does not perform it and does not create that task.

### Unresolved: the `source` value set does not cover every mutation site

D-1's scope is "**every** authoritative gameplay mutation of
`PetState.Power`". Auditing the mutation sites against D-2's `source` member
shows that two live sites are not covered by the documented value set.

The documented value set is **closed at exactly three values**, authored in
both owning documents:

```text
docs/02-technical/GAME_EVENTS.md
  §2 PowerChanged, payload line (line 379)
    "Payload:  Delta, new Power value, source (Gem match / Card cost / Relic)"
  §2 item 2 (lines 387–388)
    "source answers "what changed Power" — a Gem match, a Card cost, or a
     Relic — and its three values are owned by this payload line."

docs/02-technical/SIGNALR_PROTOCOL.md
  §3.2.24 member table (line 1186)
    | source | string | always | "match", "card", or "relic" |
  §3.2.24 item 3 (lines 1199–1201)
    "... projected to its documented lowercase contract name: "match",
     "card", or "relic"."
```

No `"boss"` value, and no fourth value of any kind, exists anywhere in
`docs/`. A repository-wide search for a Boss-associated `PowerChanged` source
returns zero matches.

### The complete `PetState.Power` mutation inventory

Every site that writes `PetState.Power` in the resolved pipeline, with its
owning stage:

```text
#  Mutation site                              Stage (§17)      source
-  -----------------------------------------  ---------------  --------------
1  Match resource generation                  Power, step 13   "match"      ✓
     BattleStateService.cs:1351-1354
     (via ResourceGenerator.ApplyPower,
      ResourceGenerator.cs:394-410)

2  Card/Pet Skill cost deduction              Card, step 14    "card"       ✓
     CardCastExecutor.cs:77
     (newPower = PetState.Power - effectiveCardCost)

3  Relic Power effect                         Relic, step 11   "relic"      ✓
     RelicResolver.cs:401
     (via ResourceGenerator.ApplyPower;
      emitted at BattleStateService.cs:1335)

4  Boss Skill secondary effect: Drain Power   Boss, step 18b   NO VALUE     ✗
     BattleStateService.cs:1820-1822            (D-6)
     (assignment: Power = Math.Max(0,
      PetState.Power - (int)skillEffect.Magnitude);
      block spans 1816-1824)
     -20 flat per BOSS_RULES.md §6.3.1 item 2

5  Card effect: Power Charge (+25)            Card, step 14    AMBIGUOUS    ✗
     CardCastExecutor.cs:150-159                (D-7)
     (newPower = Math.Clamp(newPower +
      powerAmount, 0, 100))
     +25 per CARD_RULES.md §2
```

Sites 1–3 are covered by the existing three values and require no decision.

### Site 4 — why it is in scope and has no value

`GAME_STATE.md` §2.3.1 item 9 states the mutation is authoritative battle
state, not a Status Effect:

```text
9. **Instant, non-duration effects create no instance.** Drain Power is an
   immediate `PetState.Power` mutation, not a Status Effect
   (`BOSS_RULES.md` §6.3.1 item 2: "Duration: None (instant stat reduction,
   not a persistent status effect)"), so it never produces an element here.
```

`BOSS_RULES.md` §6.3.1 item 2 owns the magnitude:

```text
- **Secondary Effect (Drain Power):** Instantly subtracts 20 flat Power from
  the active Pet (`PetState.Power = max(0, PetState.Power - 20)` per
  `GAME_RULES.md` §12).
```

So Drain Power is unambiguously an "authoritative gameplay mutation of
`PetState.Power`" and therefore falls inside D-1's plain scope. No
documented `source` value denotes it.

**Structural note (evidence, not a decision).** Site 4 does **not** route
through `ResourceGenerator.ApplyPower`; it writes `Power` directly with its own
`Math.Max(0, …)` floor (`BattleStateService.cs:1820-1822`). The other three
sites share the single `ApplyPower` clamp. This is recorded because it is
relevant to the later implementation task's shape, and is not itself a
contract question.

### Site 5 — why the sign semantics conflict

`SIGNALR_PROTOCOL.md` §3.2.24 item 1 ties the `delta` sign to the source kind:

```text
1. **`delta` is signed.** A generation is positive and a Card cost spend is
   negative; `delta = 0` is a real value where it occurs and is sent as `0`,
   following §3.2.8 item 2's rule for a zero-valued member.
```

`source` is documented as "a **Card cost**" (`GAME_EVENTS.md` §2 item 2;
`SIGNALR_PROTOCOL.md` §3.2.24 item 3). `Power Charge` is a Card **gain**:

```text
docs/01-game-design/CARD_RULES.md §2 (lines 114–116)
Power Charge
  Cost:   0 Power
  Effect: Active Pet gains 25 Power
```

It is provisioned and live:

```text
src/backend/GameServer.Infrastructure/Postgres/Migrations/
  20260929152651_ProvisionPetCardRelicContentDefinitions.cs
values: new object[] { "card-power-charge", "Power Charge", 0, 0, 1,
                        "Active Pet gains 25 Power" });
```

Labelling this mutation `"card"` therefore requires deciding whether `"card"`
means "a Card **cost** was paid" (its current documented reading) or "a Card
cast changed Power" (a widened reading).

### Site 5's composition question (D-8)

`CardCastExecutor` accumulates every effect into one `newPower` and writes it
**once** (`CardCastExecutor.cs:381-383`). A cast therefore produces one net
`Power` mutation, not one per effect. This is observable only when a Card's
cost and its Power effect are both non-zero — which no MVP-provisioned Card
does today (`Power Charge` has cost `0`), but which the general Card contract
permits.

D-7 does not by itself answer how such a cast is reported:

```text
(a) one PowerChanged carrying the NET delta of the whole cast
(b) two PowerChanged events — the cost, then the effect — in §17 order
(c) one PowerChanged per non-zero contributor, ordered per §17
```

This is asked as D-8 so the downstream implementation task has no open
question. If D-7 is answered Option B (no Card gain emission), D-8 still
matters for a Card whose cost is non-zero and whose effects are non-Power,
because that is the plain item-1 "Card cost spend" case — but in that case
D-8's answer is already fixed by §3.2.24 item 1 and D-8 may be recorded
`N/A — resolved by §3.2.24 item 1`.

---

## Problem / Ambiguity

```text
D-1 requires EVERY authoritative PetState.Power mutation to emit
PowerChanged.

D-2 fixes the event's `source` member, whose documented value set is CLOSED at
exactly three values: "match", "card", "relic".

Two live mutation sites are not covered by that set:

  Site 4  Boss Drain Power (step 18b)     — no value denotes the Boss stage
  Site 5  Card Power Charge gain (+25)    — "card" is documented as a cost spend

Neither can be resolved from the documentation, and neither may be resolved by
the executing agent:

  - Adding a "boss" value is adding a WIRE ENUM VALUE to a closed set owned by
    SIGNALR_PROTOCOL.md §3.2.24. That is a protocol contract change
    (AGENTS.md §17, §18; SIGNALR_PROTOCOL.md §3.2.2 item 5).
  - Mapping Drain Power to "relic" or any existing value is semantically false
    and is prohibited by this task.
  - Excluding site 4 or site 5 from D-1 is NARROWING A PRODUCT OWNER DECISION,
    which only the Product Owner may do.
  - Treating the site-5 gain as covered by "card" changes what the documented
    value means.

The ambiguity is therefore a Product Owner decision, not an implementation or
documentation-authoring decision.
```

---

## Authoritative References

- `AGENTS.md` **§4** — conflict resolution: detect, identify both sources, report, do not silently resolve
- `AGENTS.md` **§7** — game rule protection: no invented source value or narrowed decision
- `AGENTS.md` **§17** / **§18** — documentation and architecture change rules; a wire-contract change precedes implementation
- `AGENTS.md` **§20** — stop conditions (ambiguous requirement; conflicting documents)
- `docs/00-overview/MVP_SCOPE.md` §1 — Power and Status Effects are IN scope; Drain Power and Power Charge are provisioned MVP content
- `docs/01-game-design/GAME_RULES.md` **§12** — Power range 0–100; "generated by POWER Gem matches, Special Matches, Relics, and other defined effects"
- `docs/01-game-design/GAME_RULES.md` **§16** — the canonical event name list containing `PowerChanged`
- `docs/01-game-design/GAME_RULES.md` **§17** — the fixed resolution order; step 11 (Relics), step 13 (Update Power), step 14 (Resolve Player Effects), step 18b (Boss Skill)
- `docs/01-game-design/BOSS_RULES.md` **§6.3** / **§6.3.1 item 2** — Drain Power's -20 flat Pet Power, instant, no duration
- `docs/01-game-design/BOSS_RULES.md` **§7** — the Boss event list (which does not name `PowerChanged`)
- `docs/01-game-design/CARD_RULES.md` **§2** — Power Charge: cost 0, "Active Pet gains 25 Power"
- `docs/01-game-design/CARD_RULES.md` **§3 item 4** / **§3.6** — the cost deduction and `EffectiveCardCost`
- `docs/02-technical/GAME_EVENTS.md` **§2 `PowerChanged`** — the payload line (line 379) and items 1–4 (lines 382–403)
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.2 item 5** — admitting/extending an event's schema is owned by §3.2
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.4** — enum-valued wire members are strings carrying the documented contract name
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.24** — the `PowerChanged` member table (line 1186) and items 1–5 (lines 1188–1220)
- `docs/02-technical/GAME_STATE.md` **§2.3** — `PetState.Power`
- `docs/02-technical/GAME_STATE.md` **§2.3.1 item 9** — Drain Power is an immediate `PetState.Power` mutation
- `docs/02-technical/ARCHITECTURE.md` §5 — anti-overengineering (D-3's no-centralized-manager rule)

---

## Decision Question

All three questions below have been answered by the Product Owner. The answers
are recorded verbatim in "Decision Record".

```text
D-6  What `source` value does the Boss Drain Power mutation (step 18b) carry,
     given that D-1 requires it to emit and no documented value denotes it?
     ANSWERED: "boss" (Option A).

D-7  Does a Card's Power GAIN (Power Charge, +25) emit PowerChanged with
     source "card", given that "card" is documented as a Card COST spend?
     ANSWERED: YES — "card" denotes a Card-owned Power mutation (Option A).

D-8  When one Card cast both pays a cost and changes Power by an effect, how
     many PowerChanged events are emitted and what does each carry?
     ANSWERED: one PowerChanged per authoritative Power mutation, in
     authoritative execution order — not one collapsed net event.
```

---

## Required Decision Coverage

The Product Owner's answer must cover each item below. An answer that leaves
any item open leaves this task BLOCKED.

```text
D-6  Boss Drain Power                                   DECIDED — Option A
  [x] D-6.1  Does Drain Power emit PowerChanged at all?
             YES. "Boss Drain Power is an authoritative PetState.Power
             mutation and therefore emits."
  [x] D-6.2  If yes, what is its `source` value — is "boss" added, and is it
             the exact lowercase contract spelling?
             YES, "boss" — "Add "boss" to the allowed PowerChanged.source
             vocabulary." The spelling given is the exact lowercase contract
             form.
  [x] D-6.3  Scope of the new value — PowerChanged only, or other events too?
             DECIDED FOR THIS EVENT ONLY, by the answer's own wording: the
             vocabulary named is "PowerChanged.source". The answer is therefore
             read as scoping "boss" to the PowerChanged `source` set, and as
             saying nothing about any other event's `source` set — which
             matches SIGNALR_PROTOCOL.md §3.2.24 item 3's per-event rule. The
             later documentation task must NOT extend the value to another
             event's set on the strength of this decision.
  [x] D-6.4  If Drain Power does NOT emit: is D-1 narrowed?
             N/A — Option A. The answer states positively: "Do not narrow D-1
             to exclude Boss Drain."
  [x] D-6.5  Which stage emits it and at what point?
             DECIDED: "Boss Drain is owned by the Boss Response stage." The
             answer states stage ownership and does not restate a position
             within step 18; the authoritative position is therefore the one
             GAME_RULES.md §17 already fixes for the Boss Skill's secondary
             effect (step 18b), and no new position is invented.

D-7  Card Power Charge                                 DECIDED — Option A
  [x] D-7.1  Does a Card Power gain emit PowerChanged with source "card"?
             YES. "Therefore Card Power Charge emits: PowerChanged {
             source: "card" }".
  [x] D-7.2  Is `"card"` redefined, and does the wording replace the current
             "Card cost" reading in both owning documents?
             YES to both. ""card" identifies a Card-owned Power mutation, not
             only a Card cost spend"; and "Update the later authoritative
             wording so "card" is not defined as cost-only."
  [x] D-7.3  Does the §3.2.24 item 1 `delta` sign rule need rewording?
             IMPLICITLY YES, and it is left to the later documentation task.
             The answer supplies the governing semantics — "delta preserves the
             actual sign", and D-8's worked example carries a positive delta
             under source "card" — which item 1's current wording ("a Card cost
             spend is negative") does not express. The decision fixes the
             semantics; the wording change is the later task's act.
  [x] D-7.4  If no: is D-1 narrowed?
             N/A — Option A. No narrowing: "Do not introduce a separate
             "card-power" source."

D-8  Card cast composition                             DECIDED — per mutation
  [x] D-8.1  One net event, or one event per contributor?
             ONE EVENT PER MUTATION. "two events, one per authoritative Power
             mutation" ... "Do NOT collapse them into one net event."
  [x] D-8.2  If more than one: what order?
             DECIDED: "emit one PowerChanged per mutation in authoritative
             execution order", and the D-8 Ordering record adds: "The event
             ordering must follow the actual authoritative mutation order. Do
             not invent a new ordering rule. Do not introduce a new event
             type." The order is therefore the existing authoritative mutation
             order — no new ordering rule is authored here.
  [x] D-8.3  Does a zero-delta / no-op mutation emit?
             DECIDED: "If the authoritative Card execution path determines that
             no Power mutation occurred, no PowerChanged event is emitted for
             that operation." (This is the "no mutation occurred" case, which
             is distinct from a mutation that legitimately produces delta 0;
             that distinction remains §3.2.24 item 1's to state, and the later
             documentation task must not conflate the two.)
  [x] D-8.4  If N/A, state that §3.2.24 item 1 already resolves it.
             N/A — D-8 is decided, not deferred.
```

**All coverage items are resolved. No item remains open**, so the task is no
longer BLOCKED on input.

The following two consequences are recorded for the later tasks and are NOT
resolved here:

```text
R-1  D-8 makes the current CardCastExecutor composition non-conforming: it
     accumulates every effect into one newPower and writes it once
     (CardCastExecutor.cs:381-383), producing one net mutation rather than one
     event per mutation as D-8 requires. The later implementation task must
     address this. TASK-150 changes no code.

R-2  D-6's "boss" value extends a CLOSED wire enum owned by
     SIGNALR_PROTOCOL.md §3.2.24. AGENTS.md §17/§18 place the contract change
     before the implementation, so the later documentation task precedes the
     later implementation task.
```


---

## Decision Options

Presented for the Product Owner's selection. **No option may be selected by
the executing agent** (`AGENTS.md` §7, §20). Options are listed for
completeness and are NOT a recommendation.

```text
OUTCOME: the Product Owner selected Option A for both D-6 and D-7, and
decided D-8 as one PowerChanged per authoritative Power mutation. The options
below are retained as the record of what was presented; the authoritative
statement of each decision is the "Decision Record" section.
```

### D-6 — Boss Drain Power

#### Option A — Drain Power emits; `"boss"` is added to the value set

```text
PowerChanged {
  type:   "PowerChanged"
  delta:  -20
  power:  <resulting PetState.Power>
  source: "boss"
}
```

- **Contract owner:** `SIGNALR_PROTOCOL.md` §3.2.24 (the member table at
  line 1186 and item 3's set) and `GAME_EVENTS.md` §2 (the payload line at
  line 379 and item 2's set).
- **Consequence:** the closed `PowerChanged.source` set becomes four values.
  Any downstream consumer that validates the set must accept it.
- **Consistency reading:** `PassiveCharged`/`PassiveTriggered` already use
  `"boss"` as an entity-owner `source` value, so `"boss"` is a spelling the
  protocol already carries — although `SIGNALR_PROTOCOL.md` §3.2.24 item 3
  states explicitly that the value sets are per-event and a shared member name
  does not fix a value set.
- **Downstream impact:** a documentation task must extend both owning
  documents; then an implementation task.

#### Option B — Drain Power does not emit; D-1 is narrowed

```text
Drain Power mutates PetState.Power with no PowerChanged event.
D-1's scope explicitly excludes Boss-originated Power mutations.
```

- **Contract owner:** the D-1 decision record itself, restated in whichever
  document records the emission scope.
- **Consequence:** D-1's "every authoritative gameplay mutation" becomes a
  qualified statement. The requester of this task stated that D-1 includes
  Boss Drain "unless the Product Owner explicitly narrows D-1" — Option B is
  that explicit narrowing, and it must be written as a scope statement.
- **Downstream impact:** smaller — no wire value-set extension.

### D-7 — Card Power Charge

#### Option A — Yes; `"card"` means a Card-owned Power mutation

```text
Both a Card cost spend and a Card Power gain emit source "card".
"card" is redefined as "a Card cast changed Power".
```

- **Consequence:** `GAME_EVENTS.md` §2 item 2's wording ("a Gem match, a Card
  cost, or a Relic") and `SIGNALR_PROTOCOL.md` §3.2.24 item 3's projection
  ("Card cost") both widen. Item 1's sign sentence may need rewording.
- **Note:** `Power Charge`'s cost is `0`, so today only the `+25` gain would
  be reported — making the event a positive delta with source `"card"`, which
  is the case item 1's current wording does not describe.

#### Option B — No; `"card"` remains strictly the Card cost mutation

```text
A Card Power gain emits no PowerChanged.
"card" remains "a Card cost was paid".
D-1's scope explicitly excludes Card Power-gain mutations.
```

- **Consequence:** D-1 is narrowed for site 5. The `Power Charge` Card's `+25`
  reaches the client only through the `BattleState` state push.
- **Note:** This makes Card-cast Power reporting asymmetric — a cost emits, a
  gain does not — which the D-1 record should state explicitly if chosen.

---

## Scope

### In Scope

- Present the authoritative evidence that D-1's scope reaches the Boss Drain
  and Card Power-Charge mutation sites.
- Present the evidence that the `PowerChanged.source` value set is closed at
  three values and covers neither site.
- Present the D-6, D-7, and D-8 decision questions, the required decision
  coverage, and the available options.
- Record the Product Owner's answers **verbatim** in the Decision Record
  section, once supplied.
- Record any consequence the Product Owner attaches to an answer, without
  reinterpretation or added assumption.

### Out of Scope

- **Modifying any file under `docs/`.** In particular `GAME_EVENTS.md` §2
  item 4 and `SIGNALR_PROTOCOL.md` §3.2.24 item 5 are NOT edited by this task,
  even though D-1–D-5 make their current wording wrong. Correcting them is the
  subsequent documentation task's act.
- **Modifying any file under `src/` or `tests/`.** This task implements no
  emission and changes no mutation site.
- **Creating the downstream documentation/contract task**, and **creating the
  implementation task**. Both are separate, later acts consuming this record.
- **Re-opening D-1–D-5.** This task resolves only what they left open.
- **Modifying TASK-148** or any other completed task (`TASK_LIFECYCLE.md` §3).
- **Selecting an option.** No D-6/D-7/D-8 answer may be chosen by the executing
  agent, and none may be chosen because it is easier to implement, smaller in
  diff, consistent with the existing code, or convenient for the tests.
- **Inventing a `source` value**, adding a fourth value, or inventing a new
  event.
- **Mapping Boss Drain to `"relic"`** or to any other semantically incorrect
  value.
- **Creating an ADR.** Whether D-6 Option A needs one is a consequence to be
  determined after the answer; no ADR is created here.
- **Auditing for other Power mutation sites beyond the five inventoried.**
  The inventory above is complete for the current pipeline; a broad repository
  audit is not this task's purpose.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Decision Record (D-6, D-7, D-8)

```text
SUPPLIED BY THE PRODUCT OWNER.

Recorded verbatim below. Nothing in this section is reinterpreted, extended,
or applied to any authoritative document by this task.
```

### D-6 — Boss Drain Power

```text
D-6 = Option A

Boss Drain Power is an authoritative PetState.Power mutation and therefore
emits:

PowerChanged {
  source: "boss"
}

Add "boss" to the allowed PowerChanged.source vocabulary.

This is a later documentation/protocol change, not an implementation change
in TASK-150.

Required semantics:

* Boss Drain is owned by the Boss Response stage.
* It emits exactly one PowerChanged for its Power mutation.
* delta reflects the actual mutation.
* power is the resulting authoritative Power.
* source = "boss".
* Do not map Boss Drain to "relic".
* Do not narrow D-1 to exclude Boss Drain.
```

### D-7 — Card Power Charge

```text
D-7 = Option A

"card" identifies a Card-owned Power mutation, not only a Card cost spend.

Therefore Card Power Charge emits:

PowerChanged {
  source: "card"
}

Required semantics:

* Card cost spend uses source = "card".
* Card Power gain uses source = "card".
* delta preserves the actual sign.
* Do not introduce a separate "card-power" source.
* Update the later authoritative wording so "card" is not defined as
  cost-only.
```

### D-8 — Card cast composition (multiple Power mutations within one CardCast)

```text
D-8 = two events, one per authoritative Power mutation.

If one CardCast performs multiple sequential Power mutations, emit one
PowerChanged per mutation in authoritative execution order.

Example:

Initial Power = 50
Card cost = -10
Power effect = +25

produces:

PowerChanged
delta = -10
power = 40
source = "card"

PowerChanged
delta = +25
power = 65
source = "card"

Do NOT collapse them into one net event:

delta = +15

Each event must report:

delta
resulting authoritative power
source

If the authoritative Card execution path determines that no Power mutation
occurred, no PowerChanged event is emitted for that operation.
```

### D-8 Ordering

```text
The event ordering must follow the actual authoritative mutation order.

Do not invent a new ordering rule.

Do not introduce a new event type.
```

### Decision Consequences Recorded By The Product Owner

```text
D-6  The PowerChanged.source vocabulary gains a fourth value, "boss". Applying
     it is a later documentation/protocol change (SIGNALR_PROTOCOL.md §3.2.24
     and GAME_EVENTS.md §2), NOT an implementation change in TASK-150.
     D-1 is NOT narrowed: Boss Drain remains inside "every authoritative
     gameplay mutation of PetState.Power".

D-7  "card" is redefined as a Card-owned Power mutation rather than a Card
     cost spend only. Applying the reworded definition to
     GAME_EVENTS.md §2 item 2 and SIGNALR_PROTOCOL.md §3.2.24 items 1/3 is a
     later documentation change, NOT a TASK-150 change.

D-8  One PowerChanged per authoritative Power mutation within a CardCast, in
     authoritative execution order. The single-net-event composition the
     current CardCastExecutor performs (CardCastExecutor.cs:381-383) therefore
     does NOT satisfy D-8 and must be addressed by the later implementation
     task. TASK-150 records this; it does not change the code.
```

---

## Decision Recording

When the Product Owner supplies the answers, the executing agent must:

```text
1. Copy each answer VERBATIM into the Decision Record section above.
2. Set Status accordingly and advance the lifecycle per TASK_LIFECYCLE.md §3.
3. Confirm each "Required Decision Coverage" item is now answered; if any is
   still open, leave the task BLOCKED and report which item is open.
4. Confirm no answer requires inventing a rule, an event, a member, or a
   source value beyond what the answer itself states. If one does, STOP per
   the Stop Conditions below.
5. STOP.
```

The agent must NOT proceed to the documentation write, must not create the
downstream task, and must not implement anything.

---

## Acceptance Criteria

- [x] The `PowerChanged.source` value set is documented in this task as closed at exactly three values, with both owning documents cited by section and line (`GAME_EVENTS.md` §2 line 379 and item 2; `SIGNALR_PROTOCOL.md` §3.2.24 line 1186 and item 3)
- [x] The complete `PetState.Power` mutation inventory is recorded, with each site's file:line, owning `GAME_RULES.md` §17 stage, and current `source` coverage
- [x] Sites 4 (Boss Drain Power) and 5 (Card Power Charge) are identified as not covered by the documented value set
- [x] `GAME_STATE.md` §2.3.1 item 9 is cited as the authoritative evidence that Drain Power is an immediate `PetState.Power` mutation and therefore inside D-1's scope
- [x] `BOSS_RULES.md` §6.3.1 item 2 is cited as the owner of Drain Power's `-20` magnitude and its "instant, no duration" semantics
- [x] `CARD_RULES.md` §2 is cited as the owner of Power Charge's cost `0` and "gains 25 Power" effect, and its provisioned row is identified
- [x] `SIGNALR_PROTOCOL.md` §3.2.24 item 1's `delta` sign wording is cited as the reason a Card gain is not covered by the current `"card"` reading
- [x] D-6 is presented with exactly two options, and neither maps Drain Power to `"relic"` or another semantically incorrect value
- [x] D-7 is presented with exactly two options, and neither invents a new `source` value
- [x] D-8 is presented as the Card-cast composition question, with the `CardCastExecutor.cs:381-383` single-write evidence recorded
- [x] Every Required Decision Coverage item (D-6.1–D-6.5, D-7.1–D-7.4, D-8.1–D-8.4) is enumerated
- [x] The Decision Record section is present and carries the Product Owner's D-6, D-7, and D-8 answers verbatim, with no answer authored by the executing agent
- [x] No `docs/` file is modified by this task
- [x] No `src/` or `tests/` file is modified by this task
- [x] No completed task is modified by this task
- [x] No downstream documentation or implementation task is created by this task
- [x] No `source` value, event, member, or gameplay rule is invented by this task
- [x] Zero files are created or modified by this task other than this task file

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api) — NONE
[ ] src/frontend/client/ — NONE
[ ] tests/ — NONE
[ ] docs/ — NONE (the documentation write is the SUBSEQUENT task's act)
[x] tasks/ — this task file only
```

---

## Implementation Notes

- The D-6 question exists because of a genuine scope collision, not a wording
  preference: D-1 says "every", and `GAME_STATE.md` §2.3.1 item 9 confirms
  Drain Power is an "immediate `PetState.Power` mutation". Cite both when
  presenting D-6.
- When presenting D-6 Option A, state plainly that the `PowerChanged.source`
  set is **closed** and that adding a value is a wire-contract change owned by
  `SIGNALR_PROTOCOL.md` §3.2.24, not an implementation detail. Do not present
  the extension as routine.
- `SIGNALR_PROTOCOL.md` §3.2.24 item 3 already states that `source`'s value
  sets are per-event and that a shared member name does not fix a value set.
  Record this when presenting D-6.3, because it means `PassiveCharged`'s
  existing `"boss"` value is a spelling precedent only, not a binding one.
- For D-7, the operative conflict is narrow and should be quoted: item 1 says
  "a generation is positive and a Card **cost spend** is negative", and item 2
  defines `"card"` as "a Card **cost**". A `+25` Card gain matches neither
  clause. Quote both.
- For D-8, the evidence is `CardCastExecutor.cs:381-383`: one `newPetState`
  write of one accumulated `newPower` for the whole cast. Record that no
  MVP-provisioned Card currently has both a non-zero cost and a Power effect,
  so D-8 is a general-contract question rather than a currently observable one.
- Do not treat the absence of a `"boss"` value as an oversight to be corrected
  by inference. `BOSS_RULES.md` §7 enumerates the Boss event list and does not
  name `PowerChanged`; that is evidence the omission has never been decided,
  not evidence of intent either way.
- Bias toward recording the Product Owner's words exactly. If an answer is
  ambiguous, ask for clarification rather than resolving it — an ambiguous
  answer leaves the task BLOCKED.

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — N/A. This task modifies no code. There is no unit to test.
[x] Integration tests  — N/A. This task modifies no code and no contract.
[x] Gameplay scenarios — N/A. This task records a decision; it implements none.
```

This task's verification is an **evidence-accuracy review**, not a test run:

```text
[ ] Every file:line citation in this task resolves to the quoted content
[ ] Every cited section number resolves in the current document revision
[ ] The mutation inventory is complete for the current pipeline
[ ] No acceptance criterion depends on a decision the Product Owner has not made
```

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- If **D-6 cannot be resolved without inventing a gameplay rule**: STOP and report — do not create a `source` value or narrow D-1 on the Product Owner's behalf.
- If **D-7 cannot be resolved without inventing a gameplay rule**: STOP and report.
- If **additional `PetState.Power` mutation sites with unresolved `source` semantics are discovered** beyond the five inventoried: STOP and report them, and add them as decision items rather than resolving them.
- If **D-1–D-5 are found to conflict with authoritative documentation in another way** beyond the two `PowerChanged` emission passages already recorded: STOP and report the additional conflict per `AGENTS.md` §4.
- If an answer supplied by the Product Owner requires inventing a `source` value, an event, or a member it does not itself state: STOP and report.
- If an answer is ambiguous, or leaves any Required Decision Coverage item open: leave the task BLOCKED and report the open item.
- If resolving D-6 Option A is found to require a new ADR or an architecture change: STOP and report — that decision is not this task's.
- If this task appears to require creating the downstream documentation or implementation task to make progress: STOP — those are separate later acts.
- If task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: STOP & decompose.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Changed Files
- `tasks/backlog/TASK-150-resolve-powerchanged-source-semantics-for-boss-drain-and-card-power-charge.md` — this task file. Recorded the Product Owner's D-6, D-7, and D-8 answers verbatim in "Decision Record"; resolved every "Required Decision Coverage" item (D-6.1–D-6.5, D-7.1–D-7.4, D-8.1–D-8.4) against those answers; recorded the two consequences R-1 and R-2 that the later tasks must carry; advanced Status from BLOCKED to DONE and marked the acceptance criteria satisfied.

  No other file was created or modified. By timestamp verification: **0** files under `docs/`, **0** under `src/`, **0** under `tests/`, and **0** completed task files.

### Validation Results
This is a decision-recording task; it modifies no code and no contract, so there is no test suite to run and none was run. Its verification is an evidence-accuracy review, completed when the task was authored:

- Every `docs/` citation resolves to the quoted content at the cited line — re-verified this session: `GAME_EVENTS.md` §2 line 379 and items 2 (387–388) / 4 (397–403); `SIGNALR_PROTOCOL.md` §3.2.24 line 1186 and items 1 (1188–1189) / 3 (1199–1201) / 5 (1214–1220); `GAME_STATE.md` §2.3.1 item 9 (1278–1281).
- Every code citation resolves: `BattleStateService.cs:1351-1354` (match generation via `ApplyPower`), `:1820-1822` (Boss Drain direct write), `:1335` (the sole `ForPowerChanged` emitter); `CardCastExecutor.cs:77` (cost), `:150-159` (Power Charge), `:381-383` (single net write); `RelicResolver.cs:401`; `ResourceGenerator.cs:394-410`.
- The mutation inventory is complete for the current pipeline — an exhaustive scan of `PetState.Power` writes in `GameServer.Domain` and `GameServer.Application` returns exactly the five inventoried sites, so the "additional mutation sites" stop condition did not fire.
- Every `PowerChanged` emission in the backend resolves to one call site (`BattleStateService.cs:1335`), confirming the Relic stage is the only current emitter.

### Decision Application Status
The decisions are RECORDED, not APPLIED. This task deliberately does not:

- edit `GAME_EVENTS.md` §2 item 4 or `SIGNALR_PROTOCOL.md` §3.2.24 item 5, which D-1–D-5 make wrong;
- add `"boss"` to any wire enum;
- reword `"card"` in either owning document;
- change `CardCastExecutor`'s single-net-write composition, which D-8 makes non-conforming (consequence R-1);
- create the downstream documentation task or the downstream implementation task.

### Server Authority & Scope Verification
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed zero files under `src/` or `tests/` modified
- [x] Confirmed zero completed task files modified
- [x] Confirmed no `source` value, event, member, or gameplay rule invented — every decision recorded is the Product Owner's, verbatim
- [x] Confirmed no downstream documentation or implementation task created
- [x] Confirmed no ADR created

