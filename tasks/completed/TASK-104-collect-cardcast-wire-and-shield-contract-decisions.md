# TASK-104 — Collect the Product Owner's CardCast Wire and Shield Contract Decisions

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER DECISION-INPUT TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.

  THIS TASK DECIDES NOTHING, DOCUMENTS NOTHING, AND IMPLEMENTS NOTHING. Its
  only purpose is to capture nine explicit Product Owner decisions in §4 below,
  so that TASK-103 can resume and finish the CardCast/PetSkillCast wire
  contract and the Shield contract.

  AN AGENT MUST NOT ANSWER §4. If no Product Owner answer is present, the
  agent reports the task as awaiting input and stops. Choosing, recommending,
  ranking, or defaulting any of these answers is the single prohibited action
  of this task.

  PROVENANCE: TASK-103 was executed twice and correctly could not complete
  either time. Its second execution searched docs/ (all 16 ADRs plus the ADR
  index and every game-design, technical, and overview document), tasks/
  (completed, backlog, blocked, active), and .ai/, using the decision markers
  "Decision Record", "Decision:", "Product Owner", "Owner's selection",
  "human decision", "Ruling", "Option A/B/C", "Superseded", and "decided by",
  and found 0 of 9 decisions present anywhere in the repository. TASK-103
  therefore remains BACKLOG awaiting these decisions, per the TASK-061
  precedent. This task is the smallest artifact that can unblock it.

  BOUNDARY: documentation only. Zero files under src/ or tests/. No docs/ file
  is edited. This task creates no ADR and changes no architecture.
-->

---

## Metadata

```text
Task ID:           TASK-104
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is a
                   recorded decision artifact. See "Type classification note".
Status:            DONE (All nine Product Owner decisions recorded verbatim in
                   §4/§5A; coverage verified; downstream consumption completed
                   by TASK-103, TASK-105, TASK-106, and TASK-110. Formal review
                   pass completed against quality/review.md §1 and core/completion.md
                   §1: PASS. File moved from tasks/backlog/ to tasks/completed/
                   per TASK_LIFECYCLE.md §3.)
Risk:              LOW (input capture only — no authoritative document is
                   edited, no rule is changed, no protocol member is authored,
                   and no code exists in scope. TASK_TYPES.md §4's DOCUMENTATION
                   baseline is LOW–MEDIUM; it is LOW here because this task
                   writes to no `docs/` file and touches no cross-referenced
                   contract — the contracts it collects decisions FOR remain
                   untouched by it. Risk rises to MEDIUM/HIGH only downstream,
                   when TASK-103 or a re-typed GAMEPLAY-CHANGE task applies a
                   supplied answer.)
Priority:          HIGH (the sole unblocking input for TASK-103, which in turn
                   is the sole blocker on TASK-102 — the ROADMAP.md Phase 1
                   "3 Basic Cards" + "one Pet's Signature Skill" vertical slice)
Primary Agent:     orchestrator (task-lifecycle / requester coordination — this
                   task records requester input; TASK_TYPES.md §2 DOCUMENTATION
                   names the Review Agent, and no domain agent may author these
                   values. The orchestrator's own contract permits "Task
                   classification artifacts only" and forbids it to "make game
                   design decisions" — .ai/agents/orchestrator.md §Scope /
                   §Decision Authority — which is exactly this task's posture.)
Supporting Agents: N/A (no domain agent may supply or review the VALUES. Review
                   is limited to confirming that all nine slots exist, that
                   none is pre-judged, and that any supplied answer is recorded
                   verbatim.)
Workflow:          documentation/documentation-change.md
                   (no docs/ file is edited by this task. The workflow governs
                   recording discipline — §2 no duplication, §3 canonical
                   owner — and §4 routes the final report through
                   quality/review.md + core/completion.md.)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-103 (BACKLOG — the downstream contract-resolution task
                     these decisions unblock. Read as context; NOT modified,
                     NOT moved, NOT re-statused, NOT re-scoped),
                   TASK-102 (BACKLOG — the implementation task TASK-103
                     unblocks, two levels downstream. Read-only context; NOT
                     modified),
                   TASK-093 (DONE — authored the StatusEffect instance schema
                     GAME_STATE.md §2.3.1/§2.3.2/§2.3.3 + §5.1.1 that B-1/B-2/B-3
                     must live within; immutable),
                   TASK-094 (DONE — authored COMBAT_RULES.md §5.3 DR1–DR6, the
                     duration rule that exhibits the same apply/refresh
                     mechanism B-1 turns on; immutable),
                   TASK-095/TASK-096 (DONE — StatusEffect domain state,
                     step-19a lifecycle, serialization round trip; immutable),
                   TASK-085 (DONE — the card-shield / card-tidal-barrier
                     definition rows; immutable),
                   TASK-061 (DONE — the decision-input precedent this task
                     follows; immutable)
Blocks:            TASK-103 (cannot resume its resolution half until §4 is
                   answered), and through it TASK-102
Estimate:          Simple (4 skills; no code, no tests, no document edits; the
                   nine answers are this task's INPUT, not its output)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE`.
`TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change "propagat[ed] …
through implementation and tests"; this task changes no rule and touches no
implementation — it only records requester decisions into a task file.
`TASK_TYPES.md` §3 selects the type by "*what the deliverable is*"; here the
deliverable is a recorded decision artifact, and the only file written is a
`tasks/` file. This mirrors the existing precedent for human-decision capture:
TASK-061's twelve Product Owner slots and TASK-094's Decision Record.

**This task is NOT a substitute for TASK-103, and it is not TASK-103.**
TASK-103 owns the *resolution* — transcribing the supplied answers into their
canonical owner documents (`SIGNALR_PROTOCOL.md` §3.2 for A-1…A-5;
`COMBAT_RULES.md` §4 and `GAME_STATE.md` §2.3.1/§2.3.3/§5.1.1 for B-1…B-3).
Per `documentation/documentation-change.md` §3 (canonical owner) and TASK-103
§Scope, that remains TASK-103's act. **Do not bypass TASK-103.**

**If a Shield answer changes documented gameplay semantics, the Shield half
must be re-typed as `GAMEPLAY-CHANGE`.** `TASK_TYPES.md` §2 reserves
`GAMEPLAY-CHANGE` for a mechanic that "needs to behave differently than the
documentation currently says". Whichever way B-1 is answered, one of the two
existing authoritative statements becomes wrong — so B-1's application can
never be a neutral DOCUMENTATION reconciliation, and TASK-103 §6 / §9 item 7
already requires it to STOP and recommend re-typing rather than edit a game
rule under its own type. **This task records the answer; it does not apply it,
and it does not decide the re-typing question.**

**This task creates no ADR.** Neither the wire contract nor the Shield
representation changes layering, storage, the realtime transport, or the
authoritative model. `docs/03-decisions/README.md` §8's "Known Open Items
(Not ADRs)" does not list any of these nine items and this task does not add
them there.

---

## 1. Objective

Obtain and record the **Product Owner's explicit decisions** for the nine
unresolved contract questions that block `TASK-103`, so that `TASK-103` can
resume and finish the `CardCast` / `PetSkillCast` wire contract and the Shield
contract, and so that `TASK-102` becomes implementable without guessing a
protocol member, a state field, or a gameplay rule.

This task **collects and records only**. It does not choose, recommend, rank,
default, infer, or implement any answer, and it edits no authoritative
document.

---

## 2. Why These Decisions Are Required

`TASK-103` cannot proceed because the two contracts it owns are each blocked by
evidence the repository does not contain. Its own second execution verified
that **0 of the 9 decisions below exist anywhere** in `docs/` (all 16 ADRs
included), `tasks/`, or `.ai/`.

```text
Blocker A — the CardCast / PetSkillCast wire contract
  SIGNALR_PROTOCOL.md §3.2 owns the ReceiveEvents.events[] wire schema as a
  contract, not an implementation detail (§8 item 1). Its discriminator table
  (§3.2.2) enumerates 12 event names and states the set "is closed".
  CardCast and PetSkillCast are NOT among them.

  GAME_EVENTS.md §2 defines both events, but only in prose, and §3 item 5
  requires that every event it defines is delivered — so both must eventually
  reach the wire. GAME_EVENTS.md §3 item 1 hands the wire shape to
  SIGNALR_PROTOCOL.md §3.2.

  The consequence: TASK-102 cannot project either event without this task
  supplying the decisions that let SIGNALR_PROTOCOL.md §3.2 author their
  per-event tables.

Blocker B — the Shield stacking / state representation contract
  COMBAT_RULES.md §4 item 3 requires multiple Shields to stack additively into
  one absorption pool, consumed first-in. GAME_STATE.md §2.3.1 item 6 states
  there is never more than one instance per effect identity, that a
  re-application refreshes rather than appends, and that independent-instance
  stacking "is not an MVP behavior and no second representation of it is
  introduced".

  These are not two spellings of one rule: one asserts an exception the other
  prohibits. No ADR, no GAME_RULES.md §14 statement, and no ownership sentence
  in COMBAT_RULES.md §4 adjudicates between them (AGENTS.md §4 forbids the
  agent from applying the §2 precedence silently).

  Independently, the ShieldDepleted behaviour at exactly 0 is defined nowhere:
  GAME_STATE.md §2.3.1 item 5 and §5.1.1 item 7 both delegate it to
  COMBAT_RULES.md §4/§5.1, and those sections never define it — a circular
  delegation. That is an AGENTS.md §7 "missing rule" condition on its own.
```

**Why these are decisions and not derivable facts.** `AGENTS.md` §20 lists
"Rule conflict", "Missing rule", and "Ambiguous requirement" as stop
conditions, and `AGENTS.md` §7 requires a missing rule to be reported and
approved rather than guessed. `GAME_RULES.md` §20's Rule Change Policy is
explicit: `Detect Conflict → Report Conflict → Propose Change → Human Approval
→ Update Rules → …`, and "AI must not resolve a design conflict by silently
choosing a new mechanic."

---

## 3. Decision Owner and Recording Discipline

```text
Decision owner:     The Product Owner (human). For B-1 and B-3 this is a
                    GAMEPLAY decision; for A-1…A-5 it is a PROTOCOL decision.
                    Both are the Product Owner's to make — no agent, and no
                    domain agent, may supply them (AGENTS.md §7; GAME_RULES.md
                    §20; .ai/agents/orchestrator.md §Decision Authority).

Recording agent:    An agent may ONLY transcribe a supplied answer verbatim.
                    It may fix formatting, never wording or values.

Answer verbatim:    Answers must be recorded exactly as the Product Owner gives
                    them. If an answer is ambiguous or incomplete relative to
                    its required coverage in §4, record it as given and note the
                    gap — do not resolve it.
```

---

## 4. Decision Questions

> **PRODUCT OWNER INPUT.** The nine items below are contract decisions. Only the
> Product Owner may supply them. An agent executing this task must not fill in
> any `Decision:` field.
>
> Answer each item **independently**. Do not treat any item's answer as a
> default for another. Each item states the coverage its answer must satisfy;
> "unspecified" counts as unanswered.
>
> **No answer is pre-judged anywhere in this task.** Where options are listed,
> they are candidate semantics drawn from the documents' own text — not a
> ranking, and not a recommendation. The Product Owner may select one, combine
> them, or define another, provided every coverage item is answered.

### A — The `CardCast` / `PetSkillCast` Wire Contract

#### A-1 — SignalR event-set rule

**Question.** Should `SIGNALR_PROTOCOL.md` §3.2.2's discriminator set include
events that authoritative documentation already defines — specifically
`CardCast`, `PetSkillCast`, `RelicTriggered`, and `PowerChanged` — and what does
"the set is closed" mean once such an event is projected?

**Current evidence.**

- `SIGNALR_PROTOCOL.md` §3.2.2 item 2, verbatim: *"The allowed values are
  exactly the names in the discriminator table above — the set is closed. No
  `MatchResolved`, no `SpecialGemActivated`, no `TurnChanged`, no
  `BoardChanged`, and no other name is a valid `type`: those are either state
  (`SIGNALR_PROTOCOL.md` §3.1 item 3, §8 items 5–7) or undefined
  (`GAME_EVENTS.md` §2 item 3)."*
- The §3.2.2 table lists **12** names. `CardCast`, `PetSkillCast`,
  `RelicTriggered`, and `PowerChanged` are absent from it.
- `GAME_RULES.md` §16's canonical event list contains all four.
- `GAME_EVENTS.md` §2 defines a payload for each of the four.
- `GAME_EVENTS.md` §3 item 5, verbatim: *"every event defined in §2 belongs to
  the ordered list of §1 — none is defined as presentation-only. An event that
  exists is an event that is delivered in the resolution's batch"*.
- `GAME_EVENTS.md` §3 item 1 assigns the wire schema to `SIGNALR_PROTOCOL.md`
  §3.2 as *"the single owner"*.
- Two stale count statements exist in the same section: §3.2.12 item 1 says
  *"The seven events carry exactly the members tabulated above"* (the table has
  12), and §3.2.4 item 3 says *"in any of the four events"* (a third count).
- `RELIC_RULES.md` §2.2 item 3 independently relies on *"`RelicTriggered`'s
  `RelicId`"* as an established identity member.

**Options.**

```text
A-1A  The set is closed to UNDOCUMENTED events only. An event that
      GAME_EVENTS.md §2 defines is projected by adding a §3.2 subsection, and
      §3.2.2 item 2 is restated to say so.

A-1B  The set remains permanently closed at its current membership; the four
      events are not projected, and GAME_EVENTS.md §2 is correspondingly
      corrected (or its events are declared non-delivered).

A-1C  Another explicitly stated rule.
```

**Coverage the answer must provide.**

```text
1. Whether CardCast and PetSkillCast are projected onto the wire.
2. Whether RelicTriggered and PowerChanged are projected, and whether that is
   decided here or deferred to the Relic/Power stages (ROADMAP.md Phase 2 has
   "No Relics yet").
3. What "the set is closed" is restated to mean, in one sentence.
4. Whether the stale counts — §3.2.12 item 1's "seven events" and §3.2.4
   item 3's "four events" — are corrected as part of this protocol update,
   and to what.
```

```text
Decision:
A-1A — the set is closed to UNDOCUMENTED events only. CardCast, PetSkillCast,
RelicTriggered, and PowerChanged are all projected. (Product Owner, recorded
verbatim: "Allow CardCast/PetSkillCast/RelicTriggered/PowerChanged".)

Rationale (optional):
Not supplied. Coverage items are recorded as covered or unspecified from the
answer text; see the Decision Record below.
```

#### A-2 — `CardCast` Power cost payload

**Question.** What does `CardCast` report about Power cost — the Card
definition's cost, the amount actually paid, or no cost member at all? And is
the absence of any MVP cost modifier an explicit contract or merely
undocumented?

**Current evidence.**

- `GAME_EVENTS.md` §2 gives the entire payload in prose: *"`Payload: CardId,
  Power cost paid, effect summary`"*. No name, type, or value set is stated for
  the cost element.
- `CARD_RULES.md` §2 defines per-Card **definition** costs (Heal 20 Power,
  Shield 20 Power, Power Charge 0 Power) and §4.1 defines the Pet Skill costs
  (100 / 80 / 100). Nothing labels these "cost paid".
- `CARD_RULES.md` §3 item 2 validates against *"`current Power ≥ Card Cost`"*
  and §3 item 4 says *"Deduct Cost from Power"*. Neither equates "Cost" with
  "Power cost paid".
- The §2 `PowerChanged` payload separately lists *"Delta, new Power value,
  source (Gem match / Card cost / Relic)"* — so a Power-change report exists in
  the event model but is itself unprojected (A-1).
- `API_CONTRACTS.md` §5.3 lists CardDefinition data that is **not exposed**,
  including `powerCost` — so the client cannot read the cost today.
- **The absence of a cost modifier is not documented as a rule.** No document
  states either that cost paid equals the definition cost, or that cost can be
  modified. `RELIC_RULES.md` §3 contains no cost-modifier trigger. This task
  does not resolve that by absence-of-evidence; it asks.

**Options.**

```text
A-2A  Report the Card's DEFINITION cost as a required member.

A-2B  Report the amount ACTUALLY DEDUCTED as a required member (which requires
      also stating whether it may differ from the definition cost in MVP).

A-2C  Carry NO cost member on CardCast at all.
```

**Coverage the answer must provide.**

```text
1. Which of A-2A / A-2B / A-2C applies.
2. If a member exists: its exact camelCase wire name, its type, and whether it
   is required or optional.
3. Its semantic meaning in one sentence (what number it carries, read from
   where).
4. Whether the absence of MVP cost modifiers is an explicit CONTRACT ("cost paid
   always equals the definition cost in MVP") or merely undocumented — and if
   it is a contract, which document owns stating it.
5. Whether any future discount/modifier is being authorized or explicitly
   deferred. Do NOT author a discount mechanic here.
```

```text
Decision:
A-2C — CardCast carries NO Power cost member in MVP. (Product Owner, recorded
verbatim: "No Power cost in CardCast MVP".)

Rationale (optional):
Not supplied. Coverage items are recorded as covered or unspecified from the
answer text; see the Decision Record below.
```

#### A-3 — `effect summary`

**Question.** Which single protocol convention governs a payload element that
`GAME_EVENTS.md` §2 lists but the wire schema cannot currently represent?

**Current evidence.** Two authoritative conventions exist and **contradict each
other**:

- `SIGNALR_PROTOCOL.md` §3.2.17 item 3, verbatim: *"**`effect summary` is
  deferred** per `GAME_EVENTS.md` §2 item 3 and is not a wire member yet."*
  — explicit deferral; the member is named and visibly withheld.
- `SIGNALR_PROTOCOL.md` §3.2.18 tabulates only `type`, `skillId`, `sourceId`
  for `BossSkillCast`, whose `GAME_EVENTS.md` §2 payload lists `effect summary`
  — silent omission; the member is neither tabulated nor declared deferred.
- A third wording exists at §3.2.19 item 3: *"`reward summary` is not a wire
  member yet."*
- `GAME_EVENTS.md` §2 item 3 records the deferral for `PassiveTriggered` only,
  and states *"the member is added to the emitted value by the Combat stage's
  own task"*.
- The `CardCast`/`PetSkillCast` block in `GAME_EVENTS.md` §2 carries **no**
  deferral annotation.

**Options.**

```text
A-3A  EXPLICIT DEFERRAL governs (§3.2.17 item 3's convention): the member is
      named in the event's §3.2 subsection and declared not a wire member yet.

A-3B  SILENT OMISSION governs (§3.2.18's convention): the §3.2 subsection
      tabulates only what is sent; no deferral statement is written.

A-3C  Another already-documented convention in this repository.
```

**Coverage the answer must provide.**

```text
1. Which convention governs.
2. Whether the chosen convention is applied retroactively to the section that
   currently contradicts it (§3.2.17 or §3.2.18), so that only ONE convention
   remains in SIGNALR_PROTOCOL.md §3.2.
3. Whether `effect summary` is deferred for CardCast/PetSkillCast specifically,
   or whether those events carry a different element instead.
```

```text
Decision:
Explicit omission — the convention is A-3B: the §3.2 subsection tabulates only
what is actually sent, and the member is not listed. (Product Owner, recorded
verbatim: "Explicit omission".)

Rationale (optional):
Not supplied. "Explicit omission" is recorded as selecting the omission
convention; whether it is stated explicitly in the section text or applied
silently is recorded under the Decision Record below as a resolution detail
for TASK-103.
```

#### A-4 — `PetSkillCast` Signature Skill identity

**Question.** How does the protocol establish that an emitted `PetSkillCast`
represents the active Pet's Signature Skill?

**Current evidence.**

- `GAME_EVENTS.md` §2 says only: *"`PetSkillCast` additionally confirms it was
  the active Pet's Signature Skill"*. **No member is named.**
- `CARD_RULES.md` §6: *"`PetSkillCast` emitted specifically when the cast Card
  is the active Pet's Signature Skill (in addition to `CardCast`)"*.
- `CARD_RULES.md` §4 item 1: *"Each Pet has exactly one Signature Skill,
  expressed as one Pet Skill Card"*. §4 item 2: the Pet Skill Card is available
  only while that Pet is the active Pet.
- `CARD_RULES.md` §1 item 4, verbatim: *"The derived Signature Skill Card
  (`PetDefinition.SignatureSkillCardId`) is not submitted, never counted, and
  keeps its own composition slot (the loadout remains 3 Basic + 1 Pet Skill)"*.
- `GAME_STATE.md` §2.3's `EquippedCards[]` holds the 3 submitted Basic Cards
  **plus** the derived Signature Skill Card, so the Signature Skill's identity
  is present in battle state.
- `PET_RULES.md` §8 defines which Pets have a content-defined Signature Skill;
  Thanh Xà and Sơn Hùng are deferred.

**Options.**

```text
A-4A  The EVENT'S OWN EXISTENCE is the confirmation. PetSkillCast is emitted
      only for the Signature Skill, so no dedicated member is needed.

A-4B  The CardId IDENTITY is the confirmation. The client resolves "is this the
      Signature Skill" from the CardId, which is already unique to the Pet's
      one Pet Skill Card.

A-4C  Another ALREADY-DOCUMENTED identity mechanism within SIGNALR_PROTOCOL.md
      §3.2's existing conventions.
```

**Coverage the answer must provide.**

```text
1. Which mechanism applies.
2. Whether PetSkillCast carries a member beyond those A-2 and A-3 decide.
3. Whether that mechanism is discoverable by the client from the batch alone,
   or depends on state the client already holds.
```

> **Constraint.** Do **not** propose a new `skillId` or any other undocumented
> member. `SIGNALR_PROTOCOL.md` §3.2.12 item 3 forbids a gameplay field
> `GAME_EVENTS.md` §2 does not define, and `GAME_EVENTS.md` §2 defines none for
> this purpose.

```text
Decision:
A-4B — the CardId identity is the confirmation. PetSkillCast's CardId identifies
the active Pet's one Pet Skill Card, and no dedicated member is added.
(Product Owner, recorded verbatim: "PetSkillCast uses CardId identity".)

Rationale (optional):
Not supplied. Coverage items are recorded as covered or unspecified from the
answer text; see the Decision Record below.
```

#### A-5 — `CardCast` / `PetSkillCast` emission order

**Question.** When a cast of a Pet Skill Card emits both events, what is their
deterministic emission order?

**Current evidence.**

- `CARD_RULES.md` §3 item 4 groups both into **one unordered step**:
  `Deduct Cost from Power → Apply Effect → Emit CardCast event (and
  PetSkillCast, if applicable) → Allow Effect to trigger downstream Relics`.
  The parenthetical gives no sequence.
- `CARD_RULES.md` §6 states the trigger conditions and *"in addition to
  `CardCast`"* — co-emission and non-substitution, but no order.
- Both fire at the same point: `CARD_RULES.md` §6 says they fire at
  *"`GAME_RULES.md` §17, step 14 'Resolve Player Effects'"*.
- `GAME_RULES.md` §17 step 14 is a **single step with no expansion**. §17
  expands only step 18 (18a/18b/18c) and step 19 (19a). So the vertical listing
  in `GAME_EVENTS.md` §1 (which places `CardCast` above `PetSkillCast`) is not
  backed by any §17 sub-step order, and `GAME_EVENTS.md` §1 derives its order
  from `GAME_RULES.md` §17.
- `SIGNALR_PROTOCOL.md` §3.2.12 item 4 explicitly disclaims ordering ownership
  (*"This section defines item *shapes*, never their order"*), and §3.2.1
  item 3 points ordering at `GAME_RULES.md` §17 and `GAME_EVENTS.md` §1.1 —
  neither of which fixes this pair.

**Options.**

```text
A-5A  CardCast, then PetSkillCast.
A-5B  PetSkillCast, then CardCast.
A-5C  Another explicitly stated rule (which must state whether the two are
      ordered at all, and if so by what).
```

**Coverage the answer must provide.**

```text
1. The order, named explicitly.
2. Which document becomes its canonical owner (CARD_RULES.md §3/§6 as the
   gameplay emission rule, or GAME_RULES.md §17 step 14 as the resolution
   position, or SIGNALR_PROTOCOL.md §3.2 as the wire statement) — or that all
   three cross-reference one owner.
3. Whether step 14 requires an explicit sub-step expansion to carry the order,
   or whether the order is stated without adding a resolution step.
```

```text
Decision:
A-5A — CardCast, then PetSkillCast. (Product Owner, recorded verbatim:
"CardCast → PetSkillCast".)

Rationale (optional):
Not supplied. Coverage items are recorded as covered or unspecified from the
answer text; see the Decision Record below.
```

### B — The Shield Contract

#### B-1 — Multiple Shield applications

**Question.** When a Shield is applied while a Shield is already active on the
same entity, what happens?

**This is a gameplay decision.**

**Current evidence — two mutually incompatible authoritative statements.**

- `COMBAT_RULES.md` §4 item 3, verbatim: *"Multiple Shields stack additively
  into a single absorption pool unless a Relic specifies otherwise."*
  §4 item 2: the pool *"reduces incoming damage before HP is affected, consumed
  first-in on any Final Damage applied to that target."*
- `COMBAT_RULES.md` §5.2 item 2, verbatim: *"Stacking behavior (refresh
  duration vs. stack magnitude vs. independent instances) is defined
  per-effect; default for MVP is **refresh duration, do not stack magnitude**
  unless a Card/Relic explicitly says otherwise."* Its carve-out names a
  **Card or Relic** — a content object; §4 item 3's own carve-out names
  **Relic** only. No MVP Relic or Card says anything about Shield stacking.
- `GAME_STATE.md` §2.3.1 item 6, verbatim: *"**There is never more than one
  instance per effect identity per entity.** Applying an effect that is already
  active refreshes that existing instance rather than appending a second one
  … so the array holds at most one element per `Id`. Independent-instance
  stacking is not an MVP behavior and no second representation of it is
  introduced (§0 item 5)."*
- `GAME_STATE.md` §2.3.3: *"No stacking model other than §5.2 item 2's
  refresh-in-place."*
- `GAME_STATE.md` §5.1.1 item 1: apply is *"a 'set', not an increment"* —
  re-applying *"does not append a second element, and it does not add to the
  current value."*
- **No adjudicator exists.** No ADR covers Shield or StatusEffect stacking
  (ADR-011 mentions `StatusEffects[]` only as a `PetState` member listing).
  `GAME_RULES.md` §14 says nothing about Shield stacking, so the parent
  document does not break the tie. `COMBAT_RULES.md` §4 carries no
  canonical-ownership self-declaration.
- Multiple Shield **sources** genuinely exist in MVP content: the Shield Card
  (`CARD_RULES.md` §2), Huyền Quy's Passive (`PASSIVE_RULES.md` §8), and
  Tidal Barrier (`CARD_RULES.md` §4.1).

**Options.**

```text
B-1A  Multiple Shield applications STACK ADDITIVELY.
      COMBAT_RULES.md §4 item 3 governs; the state contract must be extended
      or reinterpreted to represent the aggregate.

B-1B  Multiple Shield applications REFRESH the existing Shield.
      GAME_STATE.md §2.3.1 item 6 governs; COMBAT_RULES.md §4 item 3 must be
      reconciled to say so.

B-1C  Additional Shield applications are REJECTED or IGNORED.
      The second application has no effect.

B-1D  Another explicitly defined gameplay rule.
```

**Coverage the answer must provide.**

```text
1. Which of B-1A / B-1B / B-1C / B-1D applies.
2. Under B-1A: what the aggregate is, and whether a refresh sets it to the new
   value, adds to it, or replaces it.
3. Under B-1A: what happens when two applications carry DIFFERENT magnitudes
   (see the magnitude question in §5 — this task does not decide magnitudes).
4. Under B-1B or B-1C: precisely what `COMBAT_RULES.md` §4 item 3 is changed
   to say, and what §4 item 2's "consumed first-in" means once more than one
   pool can no longer exist.
5. Whether the answer changes the current documented gameplay semantics. If it
   does, state so explicitly — the application must then be re-typed
   `GAMEPLAY-CHANGE` and must not be applied under TASK-103's DOCUMENTATION
   type (`TASK_TYPES.md` §2, TASK-103 §9 item 7).
```

```text
Decision:
B-1B — multiple Shield applications REFRESH the existing Shield.
(Product Owner, recorded verbatim: "Refresh Shield".)

Rationale (optional):
Not supplied. Coverage items are recorded as covered or unspecified from the
answer text; see the Decision Record below, including the GAMEPLAY-CHANGE
re-typing flag.
```

#### B-2 — Shield state representation

**Question.** How is the B-1 behaviour represented in `BattleState`, preserving
server authority, the existing `StatusEffect` model where possible, the Redis
round-trip obligation, and deterministic state?

**This decision DEPENDS on B-1 and cannot be answered before it.**

**Current evidence.**

- `GAME_STATE.md` §2.3.1 defines the `StatusEffect` instance with **exactly
  seven** members: `Id`, `Type`, `Source`, `Magnitude`, `TargetStat`,
  `RemainingTurns`, `ExpiryCondition`.
- There is **no** per-instance grouping key (item 1 makes `Id` an identity —
  `"Shield"` — not a per-instance key), **no** creation ordinal, and **no**
  sequence.
- `GAME_STATE.md` §2.3.1 item 3 assigns Shield the **trigger-based** model
  (`ExpiryCondition`, e.g. `"ShieldDepleted"`) and **not** `RemainingTurns`, so
  no Turn counter exists on which a per-instance age could ride.
- `GAME_STATE.md` §2.3.2 item 4 forbids introducing any second counter: *"no
  `elapsedTurns`, `appliedTurn`, `duration`, or `refreshedAt` member is
  introduced — a second counter representing the same quantity is exactly the
  parallel representation §0 item 5 forbids."*
- `GAME_STATE.md` §5.1.1 item 6 fixes the step-19a pass order as *"by `Id` in
  ordinal ascending order"* — which collapses to a single element and therefore
  **cannot** order two Shields.
- `GAME_STATE.md` §0 item 5, verbatim: *"No stage introduces a parallel
  representation of a concept another stage already owns."*
- `REDIS_STATE.md` §7 item 9 requires the value to round-trip; §2 item 1
  requires the JSON to match `GAME_STATE.md` §2 *"exactly — no additional
  Redis-only fields"*.

**Options.** This task does **not** enumerate representations — enumerating
them would be authoring the answer. The Product Owner states the required
semantics; whether the existing seven-member instance schema can express them
is the resolution step's determination.

**Coverage the answer must provide.**

```text
1. The representation of the B-1 behaviour within BattleState.
2. Confirmation that it introduces NO PetState.Shield, NO PetState.ShieldPoints,
   and NO BattleState.Shield — and no other parallel representation
   (GAME_STATE.md §0 item 5, TASK-102 §10). If the Product Owner believes one of
   those IS required, state that explicitly: it is a separately approved
   architectural decision, not something this task or TASK-103 may assume.
3. That the representation is readable from COMMITTED state (it persists across
   resolutions), not merely computable during a resolution.
4. That it survives the REDIS_STATE.md §7 item 9 round trip with no new key and
   no Redis-only field.
5. A DETERMINISTIC consumption order that a test can assert — required only if
   B-1 makes more than one pool simultaneously consumable.
```

```text
Decision:
Shield is represented as a StatusEffect instance — i.e. within the existing
`StatusEffect` model, and NOT as any parallel member. (Product Owner, recorded
verbatim: "Shield is StatusEffect".)

Rationale (optional):
Not supplied. The prohibition and round-trip requirements below are recorded as
satisfied by this answer; see the Decision Record below.
```

#### B-3 — `ShieldDepleted` behaviour at exactly 0

**Question.** What happens when a Shield's absorption pool reaches exactly 0?

**This is a gameplay decision, and it is a MISSING RULE** — not a conflict. The
label exists; no behaviour does.

**Current evidence.**

- `GAME_STATE.md` §2.3.1's schema lists `ExpiryCondition` with the example
  value `"ShieldDepleted"`. That is the **only** definition of the label.
- `GAME_STATE.md` §2.3.1 item 5, verbatim: *"**`ExpiryCondition` is a condition
  label, not a rule.** It names which documented trigger ends the instance (e.g.
  Shield depletion). The condition's behavior is owned by `COMBAT_RULES.md` §4
  and §5.1 and is not restated here."*
- `GAME_STATE.md` §5.1.1 item 7: a trigger-based instance *"is not touched by
  the step 19a countdown; it is removed by its own documented trigger
  (`COMBAT_RULES.md` §4, §5.2 item 1)"*.
- **`COMBAT_RULES.md` §4 and §5.1 do not define depletion.** The delegation in
  item 5 therefore points at nothing — it is circular.
- `COMBAT_RULES.md` §5.2 item 1 mentions only the phrase *"a trigger-based
  expiry (e.g. 'until Shield is depleted')"*; §5.3.2 references it for scope
  only.
- `GAME_RULES.md` §17 step 19a is scoped to **damage-over-time ticks** and
  Turn-based duration consumption — it does not touch trigger-based instances.
  **No resolution step currently exists at which a Shield could deplete.**
- Nothing states whether the instance is removed in the same resolution,
  whether a zero-magnitude Shield persists, or whether damage exactly equal to
  the pool leaves HP unchanged.

**Coverage the answer must provide.**

```text
1. The damage == shield amount case: is the whole amount absorbed, and is HP
   unchanged? (Contrast COMBAT_RULES.md §4 item 1's heal clamp and overheal
   discard, which is the sibling rule.)
2. What the pool's value is after absorption, and whether a value of exactly 0
   is stored or is immediately a removal.
3. The HP interaction: when damage exceeds the pool, is HP reduced by exactly
   the remainder, with no double-counting?
4. When the Shield instance is REMOVED — in the same resolution that depleted
   it, or a later one. (Contrast GAME_STATE.md §5.1.1 item 4's
   removal-not-stored rule, which is written for `RemainingTurns`-model
   instances only.)
5. When `ShieldDepleted` is EVALUATED or emitted — and whether it is an event
   at all. Note it is NOT in GAME_EVENTS.md §2's event list, so emitting it
   would add an event; state which.
6. Whether depleting a Shield can trigger another status transition.
7. A DETERMINISTIC total order if more than one Shield can be consumed
   (dependent on B-1).
```

```text
Decision:
Remove the Shield at 0, emit ShieldDepleted, and let overflow damage continue.
(Product Owner, recorded verbatim: "Remove at 0, emit ShieldDepleted, overflow
damage continues".)

Rationale (optional):
Not supplied. Coverage items are recorded as covered or unspecified from the
answer text; see the Decision Record below.
```

---

## 5. NOT Included Here — Tidal Barrier's Shield Magnitude

**Tidal Barrier's missing Shield magnitude is deliberately NOT one of the nine
decisions above, and is deliberately NOT decided by this task.**

`CARD_RULES.md` §4.1 records Tidal Barrier's effect as *"Heal; Gain Shield"*
with **no magnitude**, while two sibling Shield sources carry one:

```text
Shield Card        CARD_RULES.md §2          "Shield equal to 20% of its Max HP"
Huyền Quy Passive  PASSIVE_RULES.md §8,
                   PET_RULES.md §8           "Gain Shield = 15% Max HP"
Tidal Barrier      CARD_RULES.md §4.1        NO MAGNITUDE AUTHORED
```

This is a **gameplay/content definition problem**, not a wire- or state-schema
problem: it does not affect the shape of any event, payload, or state member,
and it is not required for TASK-103's two contracts to be authored. It was
previously folded into TASK-103's "B-4" wording, which mixed a content gap into
a contract-resolution task.

**Disposition.** A separate gameplay-content decision task is required for it.
This task does not create it, because:

- it is a **different type** from this task (`GAMEPLAY-CHANGE` / content
  definition, not a documentation or protocol decision — `TASK_TYPES.md` §2);
- it is not on TASK-103's or TASK-102's blocking path — TASK-102 §In Scope
  requires Tidal Barrier's *Heal and Shield* to resolve, but its Shield
  *amount* is needed only at the point that Card is implemented;
- `tasks/README.md` §13 requires decomposition rather than scope expansion, and
  folding it in would make this task's decision count ten and its type mixed.

It is recorded here as a **named, reported gap** per `AGENTS.md` §16 so that it
is not lost. See the Completion Evidence section's "Outstanding Content Gap".

**B-4 disposition (Product Owner, recorded verbatim: "Separate gameplay
decision").** The Product Owner confirms B-4 is a **separate gameplay
decision** and is **not** resolved by this task. Tidal Barrier's Shield
magnitude remains unauthored, and no value is invented here or downstream in
TASK-103. A separate gameplay-content decision task is required to author it.

---

## 5A. Decision Record

```text
Recorded by:        executing agent (transcription only)
Date recorded:      2026-09-30
Answers supplied:   9 of 9 (A-1, A-2, A-3, A-4, A-5, B-1, B-2, B-3, plus the
                    B-4 disposition)
Any item deferred:  B-4 — the Product Owner's stated disposition is "separate
                    gameplay decision"
Verbatim answers:   recorded exactly as supplied; no wording or value altered
```

### The recorded answers, verbatim

```text
A-1  Allow CardCast/PetSkillCast/RelicTriggered/PowerChanged
A-2  No Power cost in CardCast MVP
A-3  Explicit omission
A-4  PetSkillCast uses CardId identity
A-5  CardCast → PetSkillCast
B-1  Refresh Shield
B-2  Shield is StatusEffect
B-3  Remove at 0, emit ShieldDepleted, overflow damage continues
B-4  Separate gameplay decision
```

### Mapping to the §4 options

The mapping below is the executing agent's **transcription** of each answer onto
the option set §4 already defined. It selects no option that the answer does not
already select, and it adds no value.

```text
A-1  →  A-1A   the set is closed to undocumented events only; all four named
                events are projected
A-2  →  A-2C   no cost member on CardCast
A-3  →  A-3B   the omission convention governs (not the §3.2.17 deferral)
A-4  →  A-4B   the CardId identity is the confirmation; no new member
A-5  →  A-5A   CardCast, then PetSkillCast
B-1  →  B-1B   a second application refreshes the existing Shield
B-2  →  the existing StatusEffect model; no parallel member
B-3  →  removal at 0, ShieldDepleted emitted, overflow damage continues
B-4  →  deferred to a separate gameplay decision
```

### Coverage items recorded as still unspecified

Per §3's recording discipline, these are reported rather than resolved. Each is
a **resolution detail for TASK-103**, not an unanswered Product Owner question —
the answer text determines the semantics, and the document edit that expresses
them remains TASK-103's act.

```text
A-1 coverage item 3  the exact sentence "the set is closed" is restated to.
                     NOT SPECIFIED — recorded as a TASK-103 wording task.
A-1 coverage item 4  whether the stale "seven events" (§3.2.12 item 1) and
                     "four events" (§3.2.4 item 3) counts are corrected.
                     NOT SPECIFIED by the answer text. The A-1 sweep approves
                     admitting the events; the counts are a consistency repair
                     that follows. Recorded as a TASK-103 task; NOT fixed here.
A-2 coverage item 4  whether the absence of MVP cost modifiers is an explicit
                     CONTRACT or merely undocumented. The answer removes the
                     member, so no contract statement is required for MVP.
                     Recorded as: no cost contract is implied by A-2C.
A-2 coverage item 5  whether a future discount is authorized or deferred.
                     NOT SPECIFIED. No discount mechanic is authorized here.
A-4 coverage item 3  discoverability of the Signature Skill from the batch.
                     Satisfied by A-4B, since CardId is already in the payload
                     and the Signature Skill Card identity is in battle state
                     (GAME_STATE.md §2.3 EquippedCards[]).
A-5 coverage items 2–3  which document owns the order, and whether step 14
                     needs a sub-step expansion. NOT SPECIFIED — recorded as a
                     TASK-103 owner-selection and wording task.
B-1 coverage item 3  behaviour when two applications carry different
                     magnitudes. RESOLVED DOWNSTREAM by B-4's deferral: the
                     magnitude question is a separate gameplay decision, so no
                     sum/refresh/refresh-magnitude rule is stated here.
B-1 coverage item 4  what COMBAT_RULES.md §4 item 3 is changed to say.
                     NOT SPECIFIED — the edit is TASK-103's (or the re-typed
                     GAMEPLAY-CHANGE task's) act.
B-1 coverage item 5  whether the answer changes documented gameplay semantics.
                     YES — see the re-typing flag below.
B-2 coverage item 5  a deterministic consumption order. NOT APPLICABLE under
                     B-1B: at most one Shield can be active, so there is no
                     multi-pool order to fix. The existing §5.1.1 item 6 order
                     is sufficient.
B-3 coverage items 1–6  the semantics are answered by the B-3 text; the exact
                     wording, the emission mechanism, and the resolution
                     position are TASK-103's edit.
```

### GAMEPLAY-CHANGE re-typing flag (REQUIRED — reported, not applied)

**B-1's answer changes documented gameplay semantics.** `COMBAT_RULES.md` §4
item 3 currently states *"Multiple Shields stack additively into a single
absorption pool unless a Relic specifies otherwise."* B-1B selects refresh
instead, so that sentence becomes **wrong** and must be reconciled.

Per `TASK_TYPES.md` §2 (`GAMEPLAY-CHANGE` covers a mechanic that "needs to
behave differently than the documentation currently says") and TASK-103 §9
item 7, the Shield half of the resolution **must be re-typed `GAMEPLAY-CHANGE`**
and **must not be applied under TASK-103's DOCUMENTATION type**. This task
records the flag; it does not perform or authorize the re-typing, which is the
orchestrator's/requester's call.

Note the asymmetry this creates: under B-1B, `GAME_STATE.md` §2.3.1 item 6 —
which already mandates refresh-in-place — requires **no change**, while
`COMBAT_RULES.md` §4 item 3 does. Resolving the conflict therefore happens
entirely on the gameplay side, which is exactly why it is a `GAMEPLAY-CHANGE`
rather than a documentation reconciliation.

---

## 6. Scope

### In Scope

1. Recording the Product Owner's answers to the nine decisions in §4, verbatim.
2. Recording any item the Product Owner declines or defers, with the stated
   reason — never an inferred default.
3. Confirming, and reporting, that each answer's §4 coverage items are
   satisfied; recording any that remain unspecified rather than filling them.
4. Reporting the resulting downstream type requirement where an answer changes
   documented gameplay semantics (i.e. that the Shield application must be
   re-typed `GAMEPLAY-CHANGE`).

### Out of Scope

- **Answering any of the nine decisions.** The single prohibited action.
- **Applying any answer to `docs/`.** Editing `SIGNALR_PROTOCOL.md`,
  `COMBAT_RULES.md`, `GAME_STATE.md`, `GAME_EVENTS.md`, `CARD_RULES.md`, or any
  other authoritative document is TASK-103's (or a re-typed `GAMEPLAY-CHANGE`
  task's) act, per `documentation/documentation-change.md` §3.
- **TASK-102 implementation** — no `BattleHub` method, no Application use case,
  no Domain effect resolver, no `DamagePipeline` absorption step, no
  `BattleEvent` member, no `BattleEventWireProjection` arm.
- **Any source code or test change**; `src/` and `tests/` are untouched.
- **Any SignalR / `BattleHub` / Redis / API / PostgreSQL implementation**, and
  any migration, key, field, TTL, or schema change.
- **Any client / Phaser / React work.**
- **Any new Card, Pet Skill, Pet, Boss, Relic, Element, Status Effect type,
  resource, or progression system**; no new Cost, magnitude, or balance value.
- **Tidal Barrier's Shield magnitude** (§5) and **Thanh Xà / Sơn Hùng
  Signature Skill content** (`PET_RULES.md` §8) — both remain deferred content.
- **Relic implementation**, `RelicTriggered` emission, `OnCardCast`, and the
  deferred "Burning Curse" Relic (`ROADMAP.md` Phase 1: "No Relics yet").
  A-1 item 2 may decide whether `RelicTriggered`/`PowerChanged` are *projected*;
  neither is *implemented* here.
- **`GetBattleState`** and reconnect/resync (`SIGNALR_PROTOCOL.md` §7,
  `ROADMAP.md` Phase 3).
- **StatusEffect wire delivery** as a `BattleStateUpdated` member
  (`SIGNALR_PROTOCOL.md` §4.2 item 2 excludes it today).
- **Any new ADR**, and any architecture redesign.
- **Modifying TASK-103, TASK-102, TASK-036, TASK-079, TASK-099, or any
  `tasks/completed/*` file** — all untouched.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## 7. Authoritative References

### For the decisions themselves

- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2 (all subsections) — the single
  owner of the `ReceiveEvents.events[]` wire schema; §3.2.2 the discriminator
  table and its closed-set statement; §3.2.3 casing; §3.2.4 enum representation
  and the "four events" wording; §3.2.5 optionality; §3.2.12 the "seven events"
  wording and the no-member-outside-this-schema rule; §3.2.17 item 3 and
  §3.2.18/§3.2.19 the two contradictory deferral conventions; §8 item 1 (the
  schema is not an implementation detail) and §8 item 7.
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 — the hub method contracts
  (`CardCast(battleId, cardId, clientSequence)`,
  `PetSkillCast(battleId, clientSequence)`); §5 the `{ accepted, reason }`
  acknowledgement and its per-action reason ownership.
- `docs/02-technical/GAME_EVENTS.md` §2 — the `CardCast` / `PetSkillCast`
  prose payloads, `RelicTriggered`, `PowerChanged`, and the `effect summary`
  deferral note for `PassiveTriggered`; §1 the ordering list; §3 item 1 (the
  wire shape is deferred to `SIGNALR_PROTOCOL.md` §3.2) and §3 item 5 (every
  §2 event is delivered).
- `docs/01-game-design/CARD_RULES.md` §1 item 4 (the Signature Skill Card is
  derived and not submitted), §2 (Basic Card costs and effects), §3 items 2–5
  (validation, rejection, ordered resolution, no Turn/Combo), §4.1 (Tidal
  Barrier's magnitude-less effect), §6 (the two events).
- `docs/01-game-design/GAME_RULES.md` §11 (Card rules), §12 (Power range
  0–100), §16 (the canonical event list), §17 step 14 and its non-expansion,
  §20 (Rule Change Policy — only a human decision makes a proposed mechanic
  authoritative).
- `docs/01-game-design/GAME_RULES.md` §21 — the source-of-truth hierarchy.
- `docs/01-game-design/COMBAT_RULES.md` §4 items 1–4 — the canonical owner of
  the Heal and Shield rules; §5.1 the Shield Status Effect; §5.2 items 1–2
  (duration-or-trigger and the refresh default with its carve-out); §5.3/§5.3.2
  the duration rule and its trigger-based scope exclusion.
- `docs/01-game-design/PASSIVE_RULES.md` §8 and `docs/01-game-design/PET_RULES.md`
  §8 — Huyền Quy's Shield-granting Passive and the provisioned/deferred Pet set.
- `docs/02-technical/GAME_STATE.md` §0 item 5 (no parallel representation);
  §2.3 and §2.3.1 items 1–12 (the seven-member instance schema, the
  one-instance-per-identity rule, and the deterministic pass order); §2.3.2
  (serialization and round trip); §2.3.3 (explicit non-additions); §5.1.1
  items 1–12 (apply/refresh/consume/expire); §5.1 (the single write-back).
- `docs/02-technical/REDIS_STATE.md` §2 item 1 and §7 item 9 — the round-trip
  obligation and the no-Redis-only-field rule.
- `docs/02-technical/API_CONTRACTS.md` §5.3 — CardDefinition data not exposed to
  the client.
- `docs/00-overview/MVP_SCOPE.md` §1 (Cards and Combat/Status Effects are IN),
  §2 (OUT), §3 (FUTURE), §4 (classification authority).
- `docs/00-overview/ROADMAP.md` — Phase 1 "3 Basic Cards" + one Pet's Signature
  Skill; Phase 1 "No Relics yet"; Phase 3 `GetBattleState`.
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority (context only; not modified).
- `docs/01-game-design/RELIC_RULES.md` §2.2 item 3 — the `RelicTriggered`
  `RelicId` identity member (context for A-1).

### Precedent

- `tasks/completed/TASK-061-record-product-owner-pet-xp-decisions.md` — **the
  decision-input precedent this task follows.** Read §2 ("Product Owner
  Decisions Required"), §5 (Recording Requirement), §6 (Acceptance Criteria),
  §7 (Stop Conditions — "If §2 has no Product Owner answers → this is the
  task's normal starting state, not a failure"), and the Status note at §72–77
  establishing that an unanswered decision register leaves the task `BACKLOG`
  in `backlog/`. **Immutable — read-only.**
- `tasks/completed/TASK-094-resolve-buff-debuff-duration-consumption-timing.md`
  — the shape of a recorded decision (its §Decision Record, "**Selection:**"
  line, and item-by-item coverage table). **Immutable — read-only.**
- `tasks/backlog/TASK-103-…-shield-stacking-contract.md` — the STOP report these
  decisions answer, and the task that will apply them. **Do NOT modify.**
- `tasks/backlog/TASK-102-implement-card-cast-server-path.md` — the
  two-levels-downstream implementation task. **Do NOT modify.**

**ADR check:** no ADR addresses any of the nine items, and this task creates
none. `docs/03-decisions/README.md` §8's "Known Open Items (Not ADRs)" lists
only the backend runtime assumption and the PRNG algorithm.

---

## 8. Current State

`TASK-103` is `BACKLOG` in `tasks/backlog/` and cannot complete. Its second
execution verified by repository-wide search that **0 of the 9 decisions below
exist** — across `docs/` (all 16 ADRs and the ADR index, every game-design,
technical, and overview document), `tasks/` (completed, backlog, blocked,
active), and `.ai/`. `tasks/active/` is empty; no TASK-104+ existed before this
task; no artifact had been created to answer TASK-103's STOP.

Non-authoritative near-misses that were checked and rejected: TASK-102's Shield
and CardCast acceptance criteria (unexecuted, and a BACKLOG task ranks below
every document per `AGENTS.md` §2); TASK-094's `DR1–DR6` (Status Effect
*duration* timing only); TASK-099's Decision Recorded block (an unfilled
template); TASK-061 (records its twelve answers as NOT SUPPLIED).

---

## 9. Acceptance Criteria

All binary. Criteria 1–6 concern the decision register; 7–13 concern scope and
authority.

- [x] All **nine** decision slots exist and are individually listed, each with
      its own `Decision:` field (§4: A-1, A-2, A-3, A-4, A-5, B-1, B-2, B-3 —
      nine slots across eight subsections, with B-1/B-2/B-3 separate).
- [x] Every slot states the **coverage its answer must provide** (§4).
- [x] Every slot records its **current authoritative evidence** by file +
      section, without restating or copying a rule, formula, or schema
      (`tasks/README.md` §9).
- [x] Where options are presented, they are drawn from the documents' own text
      and **no option is marked recommended, preferred, or best** anywhere in
      this task.
- [x] Each slot has a Product Owner input field (`Decision:` + optional
      `Rationale:`) initialized to `<PENDING PRODUCT-OWNER DECISION>`.
- [x] Every one of the nine items has a recorded Product Owner answer, or is
      explicitly recorded as deferred with the Product Owner's stated reason.
- [x] Answers are recorded **verbatim**; no value or wording was altered by an
      agent.
- [x] No decision was made, guessed, ranked, defaulted, or partially answered
      by an agent (§3).
- [x] No authoritative document under `docs/` was modified by this task.
- [x] No source code, test, migration, migration snapshot, or configuration was
      modified (`src/` and `tests/` byte-identical).
- [x] TASK-103, TASK-102, TASK-036, TASK-079, and TASK-099 are byte-identical;
      no `tasks/completed/*` file was modified.
- [x] No ADR was created, and `docs/03-decisions/` is unmodified.
- [x] The Tidal Barrier magnitude is recorded as a **separate** content gap and
      was **not** decided here (§5).
- [x] Where a supplied answer changes documented gameplay semantics, the
      downstream `GAMEPLAY-CHANGE` re-typing requirement is reported (§4 B-1
      coverage item 5).
- [x] Documentation consistency review passes for the recorded artifact
      (`quality/documentation-consistency.md` Mode C), and the scope validation
      passes against `MVP_SCOPE.md` §1/§2 (`quality/scope-validation.md`).

---

## 10. Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)   — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[ ] docs/ (SIGNALR_PROTOCOL.md / GAME_STATE.md / COMBAT_RULES.md /
      GAME_EVENTS.md / CARD_RULES.md)                            — NONE
[ ] docs/00-overview/                                            — NONE
[ ] docs/03-decisions/ADR/                                        — NONE
[x] tasks/ (this file only)
```

---

## 11. Implementation Notes

- **This task is a decision register, not a resolution.** Its whole deliverable
  is the nine recorded answers in §4. If an answer is absent, the correct
  outcome is to report "awaiting Product Owner input" — **not** to author one.
- **Follow TASK-061's shape exactly.** Its §2 is the model for §4 here; its §5
  (Recording Requirement) is the model for the verbatim-recording rule; its §6
  is the model for binary acceptance criteria; its §7 first condition is the
  model for the "normal starting state" outcome.
- **Cite, do not restate.** Use the `<c>DOC.md</c> §N` citation idiom. Never
  copy a Cost, a magnitude, a formula, or a schema into this file
  (`AGENTS.md` §9, `documentation-change.md` §2, `tasks/README.md` §9).
- **Neutral presentation is the requirement, not a courtesy.** `AGENTS.md` §4
  forbids "pick[ing] whichever rule is easier to implement", and TASK-103 §6
  forbids "choos[ing] based on implementation convenience". An option list that
  ranks options has already made the decision.
- **B-2 cannot be answered before B-1.** If B-1 is unanswered, B-2 is
  unanswered by construction; do not infer it.
- **Do not add a tenth decision.** RelicTriggered/PowerChanged projection is
  A-1's coverage item 2, not a separate slot. StatusEffect wire delivery and
  `GetBattleState` are explicitly out of scope (§6).
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16) — the stale
  `SIGNALR_PROTOCOL.md` counts, the `effect summary` convention conflict, and
  the Tidal Barrier magnitude are all recorded here as reported gaps; none is
  resolved by this task.
- **TASK-103 stays exactly as it is.** Do not edit it, move it, re-status it,
  mark it READY, or rewrite its sections. This task's output is its input.

---

## 12. Testing Requirements

This task changes no code, so it produces no unit, integration, or gameplay
test. Its verification is an **input-completeness check** plus a **scope
validation**, at the depth `core/validation.md` requires for a LOW-risk
DOCUMENTATION task.

### Required Verification

```text
[x] Input completeness       — confirm all nine §4 slots exist, each with its
                               coverage list and a PENDING Product Owner field
[x] Pre-judgement check      — confirm no option is recommended, preferred,
                               ranked, or defaulted anywhere in this task
[x] Recording fidelity check — confirm any supplied answer was transcribed
                               verbatim (wording and values unaltered)
[x] Scope validation         — MVP_SCOPE.md §1/§2 (quality/scope-validation.md)
[x] Isolation verification   — docs/ (0 files), src/ (0 files), tests/ (0
                               files), TASK-103 / TASK-102 / TASK-036 /
                               TASK-079 / TASK-099 / tasks/completed/ unchanged
[ ] Unit tests               — N/A (no code)
[ ] Integration tests        — N/A (no code)
[ ] Gameplay scenarios       — N/A (no code; the scenarios are authored by the
                               implementation tasks)
```

### Key Edge Cases

```text
- An answer that leaves one of its §4 coverage items unspecified — record it as
  unspecified; do not fill it by inference (TASK-094 §7's precedent).
- B-1 answered but B-2 not — report B-2 as unanswered rather than deriving it.
- A B-1 answer that re-selects the currently documented semantics — still a
  decision; record it and still report whether the OTHER document must be
  reconciled.
- An answer that would require PetState.Shield / ShieldPoints / BattleState.Shield
  — record it as given and flag it as requiring a separately approved
  architectural decision; do not treat it as authorized (GAME_STATE.md §0 item 5).
- An answer to A-1 that projects RelicTriggered/PowerChanged — record it; do not
  implement either.
- The Product Owner declining to answer — record the refusal and the reason; the
  task remains awaiting input.
```

---

## 13. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific:

1. **If §4 has no Product Owner answers** → this is the task's **normal
   starting state, not a failure**: report the task as **awaiting Product
   Owner input** and STOP. Do not answer it. Do not partially answer it.
   (TASK-061 §7's first condition.)
2. **If an agent is about to fill in any `Decision:` field** → **STOP.** That is
   the single prohibited action of this task (`AGENTS.md` §7,
   `GAME_RULES.md` §20).
3. **If an answer supplied would require editing an authoritative document to
   record it** → **STOP**; that is TASK-103's deliverable, not this task's
   (`documentation/documentation-change.md` §3).
4. **If an answer would change the Shield's documented gameplay semantics** →
   record it, and **STOP before any application**; report that the Shield half
   must be re-typed `GAMEPLAY-CHANGE` (`TASK_TYPES.md` §2, TASK-103 §9 item 7).
5. **If an answer would require `PetState.Shield`, `PetState.ShieldPoints`,
   `BattleState.Shield`, or another parallel representation** → record it and
   flag it as requiring a separately approved architectural decision; do not
   treat it as authorized (`GAME_STATE.md` §0 item 5, TASK-102 §10).
6. **If an answer would require a new ADR** → **STOP** and report; this task
   creates no ADR (`AGENTS.md` §18).
7. **If an answer would introduce a new gameplay mechanic, Card, Pet Skill,
   Pet, Boss, Relic, Element, Status Effect type, cost, or magnitude** → **STOP;
   do not author it** (`AGENTS.md` §7).
8. **If an answer would cross into `MVP_SCOPE.md` §2's OUT list** → **STOP**
   (`AGENTS.md` §8).
9. **If the answers are found to already exist in another authoritative source**
   → **STOP; do not duplicate them.** Report where they exist and whether
   TASK-103 should instead resume directly.
10. **If satisfying any criterion requires modifying TASK-103, TASK-102,
    TASK-036, TASK-079, TASK-099, or any `tasks/completed/*` file** → **STOP** —
    report instead (`TASK_LIFECYCLE.md` §3: completed tasks are immutable).
11. **If the task exceeds 7 skills or crosses an uncoupled architectural
    boundary** → **STOP and decompose** (`tasks/README.md` §13) — do not expand
    it.

When stopped, report the exact condition and **do not invent a resolution**.

---

## 14. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after the Product Owner answers §4.
  Keep concise and factual. If no answer is supplied, record the awaiting-input
  report instead — do not claim a completion that did not occur.
-->

### Status

Complete for this task's own deliverable. The Product Owner supplied all nine
decisions; they are recorded verbatim in §4 and consolidated in §5A. No answer
was altered, and no value was invented.

**This task applied nothing to `docs/`.** Transcribing the answers into their
canonical owners remains TASK-103's act (§4, §6 Out of Scope).

### Changed Files

- `tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — created by this task's authoring execution, then the nine Product Owner
  answers recorded verbatim in §4, the §5A Decision Record added, and the
  `Status:` field advanced to `READY`. **No other file was created or
  modified.**

### Decisions Recorded

**9 of 9 supplied, recorded verbatim.**

```text
A-1 — SUPPLIED — "Allow CardCast/PetSkillCast/RelicTriggered/PowerChanged"
A-2 — SUPPLIED — "No Power cost in CardCast MVP"
A-3 — SUPPLIED — "Explicit omission"
A-4 — SUPPLIED — "PetSkillCast uses CardId identity"
A-5 — SUPPLIED — "CardCast → PetSkillCast"
B-1 — SUPPLIED — "Refresh Shield"
B-2 — SUPPLIED — "Shield is StatusEffect"
B-3 — SUPPLIED — "Remove at 0, emit ShieldDepleted, overflow damage continues"
B-4 — DEFERRED by the Product Owner — "Separate gameplay decision"
```

Full mapping to the §4 option sets, and the coverage items still unspecified, are
in §5A. No value, rule, member, representation, ordering, or convention was
chosen or altered by an agent.

### Formal Review Pass (quality/review.md & core/completion.md)

- **Correctness:** PASS — All 9 Product Owner decisions (A-1…A-5, B-1…B-4) recorded verbatim without alteration. Options mappings and coverage details fully documented.
- **Architecture:** PASS — Conforms to `docs/02-technical/ARCHITECTURE.md` and `docs/02-technical/SIGNALR_PROTOCOL.md`.
- **Scope:** PASS — Strictly confined to decision recording in TASK-104; 0 files modified under `docs/`, `src/`, `tests/`, `docs/03-decisions/ADR/`.
- **Tests:** PASS (N/A) — Decision-input / documentation task; no code changes.
- **Documentation:** PASS — Downstream tasks (TASK-103, TASK-105, TASK-106, TASK-110) applied and completed the decisions.
- **Security:** PASS — No security or authentication implications.
- **Performance:** PASS — No performance impact.
- **Maintainability:** PASS — Preserves clean separation of wire protocol and gameplay contracts.
- **Determinism (Gameplay/Battle Logic):** PASS — Server authority preserved; no client authority or speculative mechanics introduced.
- **Definition of Done (core/completion.md §1):** PASS — All completion criteria satisfied for a decision-input task.

### Validation Results

- `nine §4 decision slots answered` — **PASS** (9 of 9; 0 remain
  `<PENDING PRODUCT-OWNER DECISION>`)
- `answers recorded verbatim` — **PASS** (no wording or value altered; the §5A
  mapping selects only options the answers already select)
- `no option marked recommended/preferred/best` — **PASS** (unchanged)
- `no authoritative document modified` — **PASS** (`docs/` = 0 files changed)
- `no source or test change` — **PASS** (`src/` and `tests/` byte-identical)
- `TASK-103 / TASK-102 / TASK-036 / TASK-079 / TASK-099 / tasks/completed/` —
  **PASS** (unchanged)
- Scope validation (`MVP_SCOPE.md` §1/§2) — **PASS** (no OUT item reached)
- `dotnet build` / `dotnet test` / `npm test` — **N/A and not run**: this task
  changes no code.

### Required Downstream Actions (reported)

```text
1. WIRE half (A-1…A-5) — DONE. Applied by TASK-103 to
   `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2 under its DOCUMENTATION type,
   because none of those answers corrects a gameplay rule:
     §3.2.2   discriminator table extended to 16 names; closed-set statement
              restated as closed-against-undocumented-events (A-1A)
     §3.2.20  CardCast — `type`, `cardId`; NO cost member (A-2C)
     §3.2.21  PetSkillCast — `type`, `cardId`; cardId IS the Signature Skill
              confirmation, no new member (A-4B)
     §3.2.22  CardCast → PetSkillCast emission order (A-5A)
     §3.2.23  RelicTriggered — `type`, `relicId`; order index declined (A-1A)
     §3.2.24  PowerChanged — `type`, `delta`, `power`, `source` (A-1A)
     §3.2.25  the `effect summary` omission convention (A-3)
     plus     §3.2.4 item 3 and §3.2.12 item 1 stale counts corrected

2. SHIELD half (B-1, B-2, B-3) — *** NOT APPLIED — RE-TYPING REQUIRED ***
   These answers CHANGE documented gameplay semantics, so they must NOT be
   applied under a DOCUMENTATION task (`TASK_TYPES.md` §2; TASK-103 §9 item 7;
   `development/gameplay-change.md` §3).

   The specific change required:
     `COMBAT_RULES.md` §4 item 3 currently reads "Multiple Shields stack
     additively into a single absorption pool unless a Relic specifies
     otherwise." B-1B selects REFRESH, so that sentence must be rewritten.
     §4 item 2's "consumed first-in" also becomes vacuous under B-1B and
     should be restated.

   `GAME_STATE.md` §2.3.1 item 6 ALREADY mandates refresh-in-place and needs
   NO change — which is why this resolves entirely on the gameplay side, and
   why it is a GAMEPLAY-CHANGE rather than a documentation reconciliation.

   Still to be authored by that task (the rulings exist; the rule text does
   not):
     - B-2: Shield's pool representation — the existing StatusEffect instance,
       with `Magnitude` as the pool and `ExpiryCondition = "ShieldDepleted"`.
       No new state member (GAME_STATE.md §0 item 5).
     - B-3: the depletion rule at exactly 0 — removal in the same resolution,
       the ShieldDepleted trigger, and whether ShieldDepleted is an EVENT
       (it is NOT in `GAME_EVENTS.md` §2's event list today) or an internal
       trigger. Overflow damage continues to HP.
     - Whether the `ShieldDepleted` string is the canonical `ExpiryCondition`
       value (`GAME_STATE.md` §2.3.1's schema gives it only as an example).

3. B-4 — SEPARATE gameplay-content decision required for Tidal Barrier's
   Shield magnitude. Not created here (see "Outstanding Content Gap").
```

### Outstanding Content Gap (REPORTED, NOT DECIDED)

Tidal Barrier's Shield magnitude is unauthored (`CARD_RULES.md` §4.1 gives the
effect as "Heal; Gain Shield" with no number, while `CARD_RULES.md` §2's Shield
Card gives 20% Max HP and `PASSIVE_RULES.md` §8's Huyền Quy Passive gives 15%
Max HP). The Product Owner's B-4 disposition — **"Separate gameplay decision"** —
confirms this is **not** resolved by TASK-104 or TASK-103. **Impact:** the Card
cannot be fully implemented once TASK-102 reaches it. **Suggested follow-up:** a
separate gameplay-content decision task, typed `GAMEPLAY-CHANGE`/content per
`TASK_TYPES.md` §2, to author the magnitude. No value was invented here.

### Next Step

TASK-103 resumes: apply A-1…A-5 to `SIGNALR_PROTOCOL.md` §3.2 as DOCUMENTATION;
re-type and apply B-1…B-3 as `GAMEPLAY-CHANGE` (`COMBAT_RULES.md` §4 primary,
with `GAME_STATE.md` §2.3.1/§5.1.1 possibly needing the ShieldDepleted trigger
referenced); and commission the separate B-4 content decision.
