# TASK-160 — Collect the Product Owner's Decisions on the Pet `StatusEffects[]` and Boss Live-HP Projection

<!--
  GEN-TASK EXECUTION MANIFEST — PRODUCT-OWNER DECISION-INPUT TASK
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy
  game rules, formulas, schemas, or contracts.

  THIS TASK DECIDES NOTHING, DOCUMENTS NOTHING, AND IMPLEMENTS NOTHING. Its
  only purpose is to capture the Product Owner's explicit answers to the two
  contract questions in §"Decision Options" below, so that the downstream
  documentation-resolution task can author an implementation-ready projection
  contract and TASK-159 can be reconsidered.

  AN AGENT MUST NOT ANSWER THESE QUESTIONS. If no Product Owner answer is
  present, the agent reports the task as awaiting input and stops. Choosing,
  recommending, ranking, or defaulting either answer is the single prohibited
  action of this task.

  PROVENANCE: TASK-159 was created as a DECISION-FIRST task and is BLOCKED
  (AGENTS.md §7 / §18). It recorded that the Pet/Boss status-effect and live
  Boss-HP projection is unreachable by the client for want of an authored
  contract, and that no implementation task may be created for it. This task is
  the smallest artifact that can supply the missing input. It mirrors TASK-104
  (CardCast wire + Shield contract decision collection) and TASK-061 (Pet XP
  decision collection) exactly.

  BOUNDARY: this file only. Zero files under docs/. Zero files under src/ or
  tests/. TASK-159 and every other task file are immutable. No SignalR method,
  event, payload member, or JSON shape is authored, named, or designed here.

  STOP CONDITIONS (see §"Stop Conditions"): if the required decision turns out
  to be broader than Pet StatusEffects and Boss live HP, or needs a new
  gameplay rule, a new architecture decision, a new event/method, an unclear
  protocol owner, or an undeterminable MVP requirement, this task STOPS rather
  than expanding.
-->

---

## Metadata

```text
Task ID:           TASK-160
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is a
                   recorded decision artifact. See "Type classification note".
                   This task itself edits no docs/ file; it collects the input
                   the downstream documentation task consumes.
Status:            DONE
Risk:              LOW (input capture only — no authoritative document is
                   edited, no rule is changed, no protocol member is authored,
                   and no code exists in scope. TASK_TYPES.md §4's DOCUMENTATION
                   baseline is LOW–MEDIUM; it is LOW here because this task
                   writes to no `docs/` file and touches no cross-referenced
                   contract — the contracts it collects decisions FOR remain
                   untouched by it. Risk rises to HIGH only downstream, when the
                   documentation-resolution task applies a supplied answer to a
                   SignalR wire projection and a state-delivery boundary.)
Priority:          HIGH (it is the sole unblocking input for the downstream
                   documentation-resolution task, which in turn is the sole
                   blocker on TASK-159. Both projected facts are documented MVP
                   IN items — MVP_SCOPE.md §1 "Combat: … Status Effects,
                   Damage" and "Bosses: Element, Passive, Skill per Boss".)
Primary Agent:     orchestrator (task-lifecycle / requester coordination — this
                   task records requester input; TASK_TYPES.md §2 DOCUMENTATION
                   names the Review Agent, and no domain agent may author these
                   values. The orchestrator's own contract permits "Task
                   classification artifacts only" and forbids it to "make game
                   design decisions" — .ai/agents/orchestrator.md §Scope /
                   §Decision Authority — which is exactly this task's posture.)
Supporting Agents: N/A (no domain agent may supply or review the VALUES. Review
                   is limited to confirming that both decision slots exist,
                   that neither is pre-judged, and that any supplied answer is
                   recorded verbatim.)
Workflow:          documentation/documentation-change.md
                   (no docs/ file is edited by this task. The workflow governs
                   the recording discipline — §2 no duplication, §3 canonical
                   owner — and §4 routes the final report through
                   quality/review.md + core/completion.md.)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   realtime/realtime-protocol-validation,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-159 (BLOCKED — the decision-first task these answers
                     unblock. Read as context; NOT modified, NOT moved, NOT
                     re-statused, NOT re-scoped),
                   TASK-153 (DONE — landed the step-18a Boss Passive effects
                     that made Boss status state non-trivial; IMMUTABLE,
                     read-only),
                   TASK-095 / TASK-096 (DONE — StatusEffect domain state and
                     serializer round-trip; the state this projection would
                     carry already exists and is not in question),
                   TASK-093 (DONE — authored the StatusEffect instance schema
                     GAME_STATE.md §2.3.1/§2.3.2/§2.3.3 + §5.1.1; immutable),
                   TASK-104 (DONE — the decision-collection precedent this task
                     follows; immutable),
                   TASK-061 (DONE/SUPERSEDED — the decision-collection
                     precedent this task follows; immutable)
Blocks:            The downstream documentation-resolution task, and through it
                   TASK-159. Rendering the active Pet's Status Effects in
                   `BattleScene` and rendering live Boss HP during a battle are
                   both MVP IN (MVP_SCOPE.md §1) and both remain unreachable by
                   the client until the projection contract is authored.
Estimate:          Simple (4 skills; no code, no tests, no document edits; the
                   answers are this task's INPUT, not its output)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`FEATURE`. `tasks/TASK_TYPES.md` §2 types a `GAMEPLAY-CHANGE` as a rule change
that is "propagat[ed] … through implementation and tests"; this task changes no
rule and touches no implementation — it only records requester decisions into a
task file. `TASK_TYPES.md` §3 selects the type by "*what the deliverable is*";
here the deliverable is a recorded decision artifact, and the only file written
is a `tasks/` file. This mirrors TASK-104 §"Type classification note" and
TASK-061 §"Type classification note" exactly.

**This task is NOT a substitute for the downstream documentation task.** It
records decisions; it does not write them into `docs/`. Transcribing the
recorded answers into the authoritative document (`SIGNALR_PROTOCOL.md`, and
only through its canonical-owner sections) remains the downstream task's
deliverable, per `documentation/documentation-change.md` §3 (canonical owner).

---

## Objective

Obtain and record the **Product Owner's explicit decisions** on the two
unresolved protocol-contract questions that block TASK-159 — whether the active
Pet's active Status Effects and the Boss's live HP are delivered to the client,
and by what contract — so that the downstream documentation-resolution task can
author an implementation-ready projection contract in `SIGNALR_PROTOCOL.md`,
and so that TASK-159 can be reconsidered without an agent guessing a protocol
member, a payload shape, or a delivery rule.

This task **collects and records only**. It does not choose, recommend, rank,
default, infer, or implement any answer, and it edits no authoritative
document.

---

## Why This Task Is Required

The server-authoritative battle loop is already implemented through
`BattleState → Redis → SignalR`. The remaining MVP synchronization gap is a
**protocol contract decision**, not an engineering effort: the authoritative
state exists, is mutated correctly, and round-trips through Redis — it is
simply not authorized to be delivered.

TASK-159 records this precisely and STOPS. This task supplies the input that
lets the documentation-resolution task proceed.

```text
The state EXISTS and is NOT in question:
  src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
  src/backend/GameServer.Domain/Battle/BossState.cs
  — mutated by the landed step-18a / 18b / 19a logic and round-tripped
    through the Redis serializer.

The DELIVERY is not authorized:
  SIGNALR_PROTOCOL.md §4.3 item 2  — fixes `petState` at exactly four members
                                     and names `StatusEffects[]` among the
                                     GAME_STATE.md §2.3 members that are
                                     explicitly NOT delivered.
  SIGNALR_PROTOCOL.md §4           — defines no `bossState` member at all.
  GAME_STATE.md §2.3.1             — "Not a wire member." `StatusEffects[]` is
                                     not part of any current wire payload;
                                     "delivering these instances is a protocol
                                     change owned by its own task."
  BOSS_RULES.md §6.2.4             — the current SignalR projection
                                     "intentionally does not expose BossState";
                                     this is "an intentional, recorded contract
                                     limitation, not a defect. Any requirement
                                     for client-visible Boss HP or Boss
                                     StatusEffects is a separate future protocol
                                     decision, and is not authorized by these
                                     effect rules."
```

**Why this is a decision and not a derivable fact.** `AGENTS.md` §20 lists
"Missing rule", "Ambiguous requirement", and "Data contract conflict" as stop
conditions, and `AGENTS.md` §7 requires a missing rule to be reported and
approved rather than guessed. Here the documents do not merely omit a rule —
they **affirmatively decline** to deliver the data and defer the question to a
future protocol decision that has not been made. No agent may make it
(`.ai/README.md` §8: an agent "is not a Product Owner and does not decide game
design").

**Does the existing documentation already determine the answers?** No — and
this was checked rather than assumed:

```text
D-1 Pet StatusEffects[]
  §4.3 item 2 and §4.3 item 2's closing sentence state the CURRENT projection
  and expressly foreclose reading it wider ("Referring to `petState` as a whole
  does not widen that rule"). They document the state of the contract; they do
  not DECIDE whether it should change. No document requires Pet Status Effects
  to be client-visible: the ONE visibility requirement in the tree —
  PASSIVE_RULES.md §6 item 1, which is what authorized `PassiveProgress`'s
  delivery under §4 item 13 — covers Passive progress, not Status Effects.
  → NOT DETERMINED.

D-2 Boss live HP
  §4 defines no `bossState` member; GAME_STATE.md §2.4 defines the BossState
  shape as authoritative state. BOSS_RULES.md §6.2.4 goes further and states
  the non-exposure is INTENTIONAL and that any requirement for client-visible
  Boss HP "is a separate future protocol decision, and is not authorized by
  these effect rules." That is an explicit deferral, not an answer — it names
  the decision without making it.
  → NOT DETERMINED.

Because neither answer is determined, the documentation workflow CANNOT resolve
this without a Product Owner decision. Per the task instructions (§4) and
AGENTS.md §7: STOP and create a decision-input task. This is that task.
```

---

## Authoritative References

<!-- Cited by path and section. No rule, formula, schema, or wire shape is copied. -->

**The blocking statements (read first):**

- `docs/02-technical/SIGNALR_PROTOCOL.md` **§4.3 item 2** — fixes `petState`
  at exactly **four** members and names `StatusEffects[]` among the
  `GAME_STATE.md` §2.3 members that are **not** delivered. Its closing sentence
  forecloses reading the projection wider. **This is the D-1 blocking
  statement.**
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§4** — the state push; defines **no
  `bossState` member at all**. **This is the D-2 blocking statement.**
- `docs/01-game-design/BOSS_RULES.md` **§6.2.4** — records the non-exposure as
  intentional and defers any client-visible Boss HP / Boss StatusEffects
  requirement to "a separate future protocol decision".
- `docs/02-technical/GAME_STATE.md` **§2.3.1** — "**Not a wire member.**"
  `StatusEffects[]` is not part of any current wire payload, and "delivering
  these instances is a protocol change owned by its own task."

**The state that is not delivered (not in question):**

- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState`, incl.
  `StatusEffects[]`), **§2.3.1** (the Status Effect instance schema), **§2.4**
  (`BossState` — incl. `HP`, `MaxHP`, and `StatusEffects[]`), **§2.4.1**
  (staged BossState fields), **§5.1.1** (`StatusEffects[]` lifecycle)
- `docs/02-technical/GAME_STATE.md` **§0 item 5** — one representation per
  fact; forbids a second spelling of one fact on the wire

**The protocol rules any answer must be consistent with:**

- `docs/02-technical/SIGNALR_PROTOCOL.md` **§0** (the delivery shapes and their
  non-interchangeability), **§4 item 4** (a payload carries only the implemented
  stage's own fields), **§4 item 6** (state push vs. events),
  **§4 item 11** (`BattleStateUpdated` is the only state-push method),
  **§4 item 13** (the `PassiveProgress` delivery precedent and why the
  exception was made), **§4.2 item 2** (`playerState` is fixed at two members),
  **§7** (reconnect/resync snapshot — a different path from §4),
  **§8 item 5** / **§8 item 7** (no board- or resolution-specific message may
  be introduced; extending the *state* is the documented mechanism, extending
  the *method list* is not), **§3.2.5** (optionality — omitted, never explicit
  `null`), **§3.2.4** (enum representation)
- `docs/02-technical/GAME_STATE.md` **§0 item 5**, **§2.0** (the staged state
  contract), **§2.0.3** (fields explicitly not present)
- `docs/02-technical/GAME_EVENTS.md` **§2** (`PassiveCharged` /
  `PassiveTriggered`, `BossSkillCast`, `DamageCalculated` / `DamageDealt` /
  `DamageTaken`, `BattleWon` / `BattleLost` and the terminal `finalBossHp` /
  `finalPlayerHp` pair) — the events that today carry the only Boss-HP and
  status-adjacent information the client receives; **§1.2** and **§3**
- `docs/02-technical/API_CONTRACTS.md` **§1**, **§3** (`POST /api/battle/start`
  and its `initialState`), **§7** ("What Is Not Here")

**The domain rules the delivered state would describe:**

- `docs/01-game-design/BOSS_RULES.md` **§5** (Boss State), **§6.2.4**
  (client visibility), **§8** (server authority)
- `docs/01-game-design/PASSIVE_RULES.md` **§6** (Visibility — the sole
  UI-facing exposure requirement in the tree)
- `docs/01-game-design/COMBAT_RULES.md` **§5.1** (Buff/Debuff), **§5.2**
  (shield/refresh semantics), **§5.3** (Turn-based duration lifecycle and its
  single step-19a decrement)
- `docs/01-game-design/GAME_RULES.md` **§16** (canonical event-name list),
  **§17** (the fixed resolution order, esp. steps 18a/18b and 19a), **§18**
  (server authority)

**Scope, architecture, and decisions:**

- `docs/00-overview/MVP_SCOPE.md` **§1** ("Combat: HP, ATK, DEF, Power, Crit,
  Status Effects, Damage, Element interaction"; "Bosses: Element, Passive,
  Skill per Boss"), **§2**, **§4**; `docs/00-overview/ROADMAP.md` §1 Phase 1
  ("Server-authoritative resolution over SignalR")
- `docs/02-technical/ARCHITECTURE.md` **§2.2.1** (Game Runtime Coordination —
  rule 4, events pass through unchanged; rule 5, client runtime state is
  technical only), **§2.2.3**, **§5** (anti-overengineering)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`,
  `ADR-004-signalr-realtime.md`, `ADR-008-battle-recovery.md`
- `docs/03-decisions/README.md` — ADR index and §3 (an ADR never overrides the
  what/how owned by `docs/02-technical/`)

**Governing contract and precedent:**

- `AGENTS.md` **§4** (never silently resolve a conflict), **§7** (invent no
  rule — the governing section here), **§10** (server authority), **§16**
  (report, do not fix inline), **§17** (documentation change rule), **§18**
  (architecture change rule), **§20** (stop conditions)
- `.ai/README.md` **§8** (an agent is not a Product Owner), **§13** (stop
  conditions and the stop-report format)
- `.ai/agents/orchestrator.md` §Scope / §Decision Authority; `.ai/agents/realtime.md`
  §Decision Authority / §Stop Conditions
- `tasks/README.md` **§9** (no business-rule duplication), **§12** (skill budget)
- `tasks/completed/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — the decision-collection precedent
- `tasks/completed/TASK-061-record-product-owner-pet-xp-decisions.md` — the
  decision-collection precedent
- `tasks/completed/TASK-014-resolve-petstate-delivery-contract.md` — the
  precedent for resolving a `PetState` delivery question against `§4 item 4`
- `tasks/completed/TASK-121-resolve-client-loadout-and-pet-skill-visibility-contract.md`
  — records the `petState` member set and its exclusions
- `tasks/backlog/TASK-159-deliver-pet-and-boss-status-effect-projection-in-battlestateupdated.md`
  — the BLOCKED task this input unblocks. Read-only; **must not be modified.**

---

## Current Contract

The current contract, stated as citations only (`tasks/README.md` §9 — no rule
or schema is restated here):

```text
DELIVERED TODAY — SIGNALR_PROTOCOL.md §4
  `BattleStateUpdated` is the only state-push method (§4 item 11).
  It carries exactly eight members: battleId, turn, sequence, rngSeed,
  rngState, board, playerState, petState.

  `playerState`  — fixed at exactly two members (§4.2 item 2).
  `petState`     — fixed at exactly four members (§4.3 item 2):
                   passiveId, passiveProgress, the conditional
                   passiveResetOverride, and equippedCards.

EXPLICITLY NOT DELIVERED — SIGNALR_PROTOCOL.md §4.3 item 2
  Named among the GAME_STATE.md §2.3 members that are NOT delivered:
  `StatusEffects[]`, `NextAttackCritModifiers[]`, `CardCostModifiers[]`,
  `ATKModifiers[]`, identity, Element, Tier/Star/Level, and combat stats.

NOT DEFINED AT ALL — SIGNALR_PROTOCOL.md §4
  No `bossState` member, and no Boss-HP member of any kind.

GAME_STATE.md §2.3.1
  "Not a wire member." Delivering these instances is a protocol change owned
  by its own task.

BOSS_RULES.md §6.2.4
  The non-exposure of BossState is an intentional, recorded contract
  limitation. Client-visible Boss HP or Boss StatusEffects is "a separate
  future protocol decision, and is not authorized by these effect rules."

WHERE BOSS HP REACHES THE CLIENT TODAY
  Only as the terminal `finalBossHp` inside BattleWon/BattleLost
  (GAME_EVENTS.md §2), and once in POST /api/battle/start's `initialState`
  (API_CONTRACTS.md §3). Neither is a live in-battle value.
```

---

## Exact Ambiguity

Two contract questions are unresolved. Neither is answered anywhere in `docs/`,
`tasks/`, or `.ai/`.

```text
D-1  PET STATUS EFFECTS
     `PetState.StatusEffects[]` is authoritative state that is fully
     implemented, mutated by the landed resolution pipeline, and persisted
     through Redis — but SIGNALR_PROTOCOL.md §4.3 item 2 explicitly names it
     as NOT delivered, and GAME_STATE.md §2.3.1 states it is not part of any
     current wire payload.

     UNRESOLVED: does the Product Owner intend the active Pet's active Status
     Effects to be delivered to the client, or to remain intentionally
     server-authoritative and undelivered through the current projection?

D-2  BOSS LIVE HP
     `BossState.HP` is authoritative state. SIGNALR_PROTOCOL.md §4 defines no
     `bossState` member, so no live Boss HP is authorized on any path;
     BOSS_RULES.md §6.2.4 records the non-exposure as intentional and defers
     the question.

     UNRESOLVED: does the Product Owner intend the Boss's live HP (and any
     associated Boss state) to be delivered to the client, or to remain
     intentionally undelivered through the current protocol?

WHY THESE ARE NOT DERIVABLE
  Every document that touches either question either states the CURRENT
  projection (which is the thing in question), affirms that the non-delivery
  is intentional, or defers the requirement to a future decision. A deferral
  names the decision; it does not make it. AGENTS.md §7 and §20 make guessing
  it a STOP, and .ai/README.md §8 states an agent is not a Product Owner.
```

---

## Decision Options

> **PRODUCT OWNER INPUT.** The two items below are contract decisions. Only the
> Product Owner may supply them. An agent executing this task must not fill in
> any `Decision:` field.
>
> Answer each item **independently**. Do not treat either item's answer as a
> default for the other. Each item states the coverage its answer must satisfy;
> "unspecified" counts as unanswered.
>
> **No answer is pre-judged anywhere in this task.** The options are candidate
> semantics drawn from the documents' own text — not a ranking, and not a
> recommendation. The Product Owner may select one, combine them, or define
> another, provided every coverage item is answered.
>
> **Do not design the JSON shape here.** Per the task instructions, no payload
> field is invented and no final wire shape is designed in this file. The
> member name, type, optionality, absence convention, and serialization details
> are authored by the **downstream** documentation task, from the Product
> Owner's answer. Where a coverage item below asks for an authoritative
> delivery carrier (D-3), that records *which already-authorized delivery
> path should own the fact* — it is not a wire-shape design and it does not
> ask the Product Owner to name a member.

### D-1 — Pet Status Effects projection

**Question.** Does `BattleStateUpdated` include the active Pet's active
`StatusEffects[]`, or do they remain intentionally server-authoritative and
undelivered through the current projection?

**Current evidence.**

- `SIGNALR_PROTOCOL.md` §4.3 item 2 fixes `petState` at **four** members and
  names `StatusEffects[]` among the §2.3 members that are **not** delivered,
  per §4 item 4's rule that a payload carries only the implemented stage's own
  fields. Its closing sentence: *"Referring to `petState` as a whole does not
  widen that rule."*
- `SIGNALR_PROTOCOL.md` §4 item 13 delivered `PassiveProgress` as an
  **exception**, made specifically because `PASSIVE_RULES.md` §6 item 1
  requires that value to be exposed to the player. No document makes an
  equivalent requirement for Status Effects.
- `SIGNALR_PROTOCOL.md` §4 items 14–16 each **decline** to deliver a
  `PetState` collection (`NextAttackCritModifiers[]`, `CardCostModifiers[]`,
  `ATKModifiers[]`), reasoning from the same absence-of-a-visibility-
  requirement test. `StatusEffects[]` is grouped with them in §4.3 item 2.
- `GAME_STATE.md` §2.3.1 states *"**Not a wire member.**"* and that delivering
  these instances *"is a protocol change owned by its own task."*
- `COMBAT_RULES.md` §5.1–§5.3 define what a Status Effect is, how it
  refreshes, and how it decays — but say nothing about client visibility.
- `MVP_SCOPE.md` §1 lists "Combat: … Status Effects, Damage" as MVP IN. It
  establishes that the **mechanic** is in scope; it does not state that the
  list must be delivered on the state push.

**Options.**

```text
D-1A  `BattleStateUpdated` DOES include the active Pet's active
      `StatusEffects[]`. The §4.3 `petState` member set is widened by a
      Product Owner decision, and §4.3 item 2 is restated accordingly.

D-1B  Pet Status Effects remain INTENTIONALLY server-authoritative and are NOT
      delivered through the current `BattleStateUpdated` projection. The
      client does not render them in MVP, and the exclusion in §4.3 item 2 /
      GAME_STATE.md §2.3.1 stands.

D-1C  Another explicitly stated rule.
```

**Coverage the answer must provide.**

```text
1. Whether D-1A, D-1B, or D-1C applies.
2. If D-1A: whether delivery is unconditional or limited (e.g. active instances
   only, or Pet-owned only vs. any target), and what the client renders from it.
3. If D-1B: whether the existing exclusion sentences in SIGNALR_PROTOCOL.md
   §4.3 item 2 and GAME_STATE.md §2.3.1 are left as-is or restated as an
   explicit MVP decision.
4. Whether either projected fact is required for MVP client synchronization —
   see D-4.
```

```text
Decision:
D-1A

The active Pet's active `StatusEffects[]` MUST be delivered to the client through
the existing `BattleStateUpdated` state synchronization path.

The projection is limited to the active Pet's currently active Status Effect
instances. It does not authorize delivery of unrelated hidden PetState fields.

The client may render the delivered active Status Effects for the active Pet.

This projection is required for MVP client synchronization because active status
effects are visible combat state that can affect the Pet's current behavior and
presentation.

D-3 carrier: `BattleStateUpdated`.

D-4 MVP requirement: Required for MVP client synchronization.

D-5: `BattleStateUpdated` remains the carrier.

D-6: No new SignalR method or event is required.

Rationale (optional):
<NOT SUPPLIED BY PRODUCT OWNER>
```

### D-2 — Boss live HP projection

**Question.** Does `BattleStateUpdated` include the required Boss state / live
HP, or does Boss live state remain intentionally undelivered through the
current protocol?

**Current evidence.**

- `SIGNALR_PROTOCOL.md` §4 lists the payload's members and defines **no
  `bossState` member at all**; §4 item 4 admits only the implemented stage's own
  fields.
- `SIGNALR_PROTOCOL.md` §4 item 11 fixes `BattleStateUpdated` as the only
  state-push method, and §8 items 5/7 forbid introducing a new message where
  extending the state is the documented mechanism.
- `GAME_STATE.md` §2.4 defines `BossState` (incl. `HP`/`MaxHP`) as authoritative
  state; `§2.4.1` stages its fields. `§2.0.3` lists fields explicitly not
  present.
- `BOSS_RULES.md` §6.2.4: the current projection *"intentionally does not expose
  `BossState`"*; this is *"an intentional, recorded contract limitation, not a
  defect. Any requirement for client-visible Boss HP or Boss StatusEffects is a
  separate future protocol decision, and is not authorized by these effect
  rules."*
- `BOSS_RULES.md` §5 item 2 states Boss State is server-authoritative and
  *"must be exposed to the client only through emitted events … and rendered
  state, never computed client-side."* The phrase "rendered state" is present
  but no document defines a carrier for it — this is part of what D-2 resolves.
- Boss HP reaches the client today only as the terminal `finalBossHp`
  (`GAME_EVENTS.md` §2) and once in `POST /api/battle/start`'s `initialState`
  (`API_CONTRACTS.md` §3). Neither is a live in-battle value, and
  `BOSS_RULES.md` §5 item 4's Enrage transition is evaluated server-side.
- `MVP_SCOPE.md` §1 lists "Bosses: Element, Passive, Skill per Boss" as MVP IN;
  it does not state that Boss HP must be delivered on the state push.

**Options.**

```text
D-2A  `BattleStateUpdated` DOES include the required Boss state / live HP. A
      Boss-state member is added to the §4 push by a Product Owner decision,
      and §4 is restated accordingly.

D-2B  Boss live state / HP remains INTENTIONALLY undelivered through the
      current protocol. The terminal `finalBossHp` event and the
      `initialState` value remain the only Boss-HP data the client receives in
      MVP, and BOSS_RULES.md §6.2.4's recorded limitation stands.

D-2C  Another explicitly stated rule.
```

**Coverage the answer must provide.**

```text
1. Whether D-2A, D-2B, or D-2C applies.
2. If D-2A: WHICH Boss facts are delivered — live HP alone, or also other
   documented `BossState` fields (e.g. State/Enrage, MaxHP, PassiveProgress,
   SkillCharge/SkillCooldown, Boss `StatusEffects[]`). Naming which facts are
   in is required; an answer that says only "Boss state" leaves this
   unspecified.
3. If D-2A: whether the Boss facts travel on the SAME push as the Pet
   projection or on a separate documented carrier — see D-5 and D-6.
4. If D-2B: whether BOSS_RULES.md §6.2.4's deferral sentence is left as-is or
   restated as an explicit MVP decision, and how the client is expected to
   present Boss HP in MVP (or that it is not presented).
5. Whether the delivered Boss fact is required for MVP client
   synchronization — see D-4.
```

```text
Decision:
D-2A

The Boss's live HP MUST be delivered to the client through the existing
`BattleStateUpdated` state synchronization path.

The required Boss projection for MVP is limited to:

HP
MaxHP

Do NOT expose the entire `BossState`.

The following remain outside this projection unless separately authorized by a
future contract:

ATK
Element
State
PassiveProgress
SkillCharge
SkillCooldown
StatusEffects[]
other hidden BossState fields

The client may render the Boss's current HP and MaxHP during the battle.

Live Boss HP is required for MVP client synchronization because the player must
be able to see the current Boss health during an active battle.

D-3 carrier: `BattleStateUpdated`.

D-4 MVP requirement: Required for MVP client synchronization.

D-5: `BattleStateUpdated` remains the carrier.

D-6: No new SignalR method or event is required.

Rationale (optional):
<NOT SUPPLIED BY PRODUCT OWNER>
```

<!--
  COMBINED DELIVERY DECISION (Product Owner input, recorded verbatim —
  §"Recording Discipline"): the two authorized projections are delivered
  through the existing `BattleStateUpdated` state-push mechanism. No new
  SignalR method or event is introduced. The downstream
  documentation-resolution task owns the exact wire contract — member names,
  types, optionality, absence convention, serialization details — and TASK-160
  does not define those details.
-->

---

## Required Decision Coverage

The recorded decision must explicitly resolve **all six** items below. Any item
left unspecified is recorded as unspecified — not inferred.

```text
D-1  Pet StatusEffects projection
     → answered by the D-1 slot above.

D-2  Boss live HP projection
     → answered by the D-2 slot above.

D-3  Authoritative delivery carrier for each decision
     → For each fact the Product Owner authorizes for delivery, identify the
       already-authorized delivery carrier/path that should own the fact.

       This records the carrier/ownership decision ONLY. It does NOT define:
       - member name
       - JSON shape
       - type
       - optionality
       - absence convention
       - serialization details

       Those are owned by the downstream documentation-resolution task.

       If the fact is intentionally undelivered:
       "none — server-authoritative".

       An answer that authorizes delivery but names no owning carrier leaves
       D-3 unspecified.

D-4  MVP requirement confirmation
     → For each authorized delivery decision, state whether the projection is
       required for MVP client synchronization.

       If this is already deterministically established by authoritative MVP
       documentation, the downstream task must cite that documentation rather
       than requesting a new Product Owner decision. "Not required for MVP" is
       a valid and complete answer; it is not a deferral.

       This item does NOT open a new gameplay or product-scope decision.

D-5  Whether existing `BattleStateUpdated` remains the carrier
     → Whether the §4 push carries the authorized facts, or the decision
       selects another ALREADY-DOCUMENTED path (§7's snapshot is the only
       other state path; §3's event batch is not state per §4 item 6).
       Selecting "another documented path" requires naming it. Selecting a
       path that does not exist raises the D-6 stop condition.

D-6  Whether any new SignalR method/event is required
     → Stated explicitly as yes/no with a reason. Note the standing
       constraint: SIGNALR_PROTOCOL.md §4 item 11 fixes `BattleStateUpdated`
       as the only state-push method, and §8 items 5/7 forbid introducing a
       message where extending the state is the documented mechanism. If the
       answer requires a new method or event, this task STOPS per §"Stop
       Conditions" rather than recording a decision that presupposes it.
```

---

## Recording Discipline

```text
Decision owner:     The Product Owner (human). Both items are PROTOCOL
                    decisions. No agent, and no domain agent, may supply them
                    (AGENTS.md §7; .ai/README.md §8; .ai/agents/orchestrator.md
                    §Decision Authority).

Recording agent:    An agent may ONLY transcribe a supplied answer verbatim.
                    It may fix formatting, never wording or values.

Answer verbatim:    Answers must be recorded exactly as the Product Owner gives
                    them. If an answer is ambiguous or incomplete relative to
                    its required coverage in §"Required Decision Coverage",
                    record it as given and note the gap — do not resolve it.

Ownership of the
follow-on edit:     Transcribing the recorded answers into docs/ is the
                    DOWNSTREAM documentation-resolution task's deliverable, per
                    documentation/documentation-change.md §3 (canonical owner).
                    This task does not do it.
```

---

## Out of Scope

- **Any implementation.** No source file, test file, migration, or configuration
  is added or modified by this task.
- **Any change under `docs/`.** Zero files under `docs/` are created, edited,
  renamed, or deleted. Not `SIGNALR_PROTOCOL.md`, not `GAME_STATE.md`, not
  `GAME_EVENTS.md`, not `BOSS_RULES.md`, not `PASSIVE_RULES.md`, not
  `COMBAT_RULES.md`.
- **Any change to TASK-159.** It is not edited, moved, re-statused, re-scoped,
  marked READY, or unblocked by this task. This task's output is its input.
- **Inventing any SignalR contract.** No payload member, member name, type,
  optionality, absence convention, JSON shape, discriminator, method, event, or
  subscription is proposed, named, authored, or designed in this file. The two
  `Decision:` slots are the Product Owner's to fill.
- **Creating a new SignalR method or event.** Not authorized, and not proposed.
- **Choosing an answer.** No option is marked recommended, preferred, or best
  anywhere in this task, and no default is applied.
- **Defining undocumented payload fields.** Explicitly prohibited.
- **The downstream documentation-resolution task.** Applying the recorded answer
  to the authoritative document is a separate task, not performed here.
- **The other blocked MVP items** — the 2 remaining Pets, the 2 remaining
  Bosses, the remaining Relics / Burning Curse, and
  `GET /api/battle/history`. Each needs its own missing rule or decision; each
  is out of scope here.
- **A general SignalR audit.** This task is scoped to D-1 and D-2 only. It must
  not be broadened into an audit of the protocol, the projection, the event
  schema, or any other undelivered `PetState` collection
  (`NextAttackCritModifiers[]`, `CardCostModifiers[]`, `ATKModifiers[]`).
- **The stale "not implemented" doc comments** in `BattleStartService.cs`,
  `SwapExecution.cs`, and `BattleStateService.cs` — reported only.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2, and any item not
  listed in §1 (treat unlisted as FUTURE per §4).

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

**This task STOPS instead of creating or expanding itself if any of the
following holds.** In each case, report the condition and do not proceed.

- **The existing authoritative docs already determine both answers.** If a
  document is found that unambiguously decides D-1 and D-2, this task is
  unnecessary: report that the documentation workflow may resolve it without a
  Product Owner decision, and stop. *(Checked in §"Why This Task Is Required":
  they do not.)*
- **The requested decision is broader than Pet StatusEffects and Boss live HP.**
  If answering requires also deciding `NextAttackCritModifiers[]`,
  `CardCostModifiers[]`, `ATKModifiers[]`, identity, progression, combat stats,
  or any other undelivered projection, that is a scope expansion: **STOP**. Do
  not add decision slots.
- **A new gameplay decision is required.** If an answer would change a gameplay
  rule rather than a delivery contract, the owning document is a
  `docs/01-game-design/` rule doc and the type is `GAMEPLAY-CHANGE`, not this
  task: **STOP** per `AGENTS.md` §7.
- **A new architecture decision is required.** If an answer would change the
  realtime strategy, the battle-state model, the authoritative model, or a
  module boundary, an ADR precedes it: **STOP** per `AGENTS.md` §18.
- **A new event/method is required before the Product Owner decision.** If the
  decision cannot be recorded without presupposing a new SignalR method or
  event, **STOP** — that presupposition is itself an unapproved contract change
  (`SIGNALR_PROTOCOL.md` §4 item 11, §8 items 5/7).
- **The protocol ownership is unclear.** If it cannot be determined which
  document owns authoring the widened projection (the expectation here is
  `SIGNALR_PROTOCOL.md`, with `GAME_STATE.md` owning the state shape), and two
  documents' stated purposes both plausibly own it: **STOP** and report the
  structural ambiguity per `documentation/documentation-change.md` §3.
- **The MVP requirement cannot be determined.** If D-4 cannot be answered
  because `MVP_SCOPE.md` §1 does not settle whether the fact is required for
  MVP synchronization: record it as unresolved and **STOP**; do not treat
  "unlisted" as IN (`MVP_SCOPE.md` §4).
- **An agent is asked to fill in a `Decision:` slot.** **STOP** — that is the
  single prohibited action of this task.
- **An answer requires inventing a value, shape, or optionality.** **STOP** —
  inventing it is the prohibited act.
- If the work requires changing a gameplay rule or expanding MVP scope:
  **STOP**.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: **STOP & decompose**.

---

## Downstream Dependency

The recorded decision is the input to a **documentation-resolution task**, which
owns the authoritative edit. This task does not perform it.

```text
TASK-159  (BLOCKED)
    the projection is unauthorized; no implementation may be created
        ↑
        │  unblocked only by an authored contract
        │
TASK-160  (this task — Product Owner decision input)
    Product Owner answers D-1 and D-2, with D-3…D-6 coverage.
    Records the DECISION only:
      "should this fact be delivered, and by which already-authorized
       carrier" — never a member name or a JSON shape.
        ↓
DOWNSTREAM DOCUMENTATION-RESOLUTION TASK  (not created here)
    defines the EXACT contract: member name, type, optionality, absence
    convention, serialization details, and the relationship to
    SIGNALR_PROTOCOL.md §4 item 4 and §4 item 11 — then writes it into the
    owning authoritative document (SIGNALR_PROTOCOL.md §4 / §4.3, and any
    §4.3-adjacent wording), producing an implementation-ready projection
    contract
        ↓
TASK-159  (BLOCKED → reconsidered)
    once the contract exists, the projection implementation may be
    re-scoped and authorized as its own task
        ↓
IMPLEMENTATION TASK  (not created here)
    projects the authorized members onto the wire
        ↓
CLIENT PRESENTATION TASK  (not created here)
    renders the delivered facts in BattleScene
```

**The boundary is deliberate: do not jump from TASK-160 straight to code.** A
decision to deliver is not a contract; the member set must be authored in
`docs/` before any implementation task may exist (`AGENTS.md` §7).

**This task does not create that downstream task, and does not implement it.**
Naming it here records the dependency edge only.

---

## Acceptance Criteria

All binary and testable. Criteria 1–6 concern the decision register; 7–13
concern scope, isolation, and authority.

- [ ] Both decision slots exist and are individually listed, each with its own
      `Decision:` field (D-1 and D-2).
- [ ] Every slot states the **coverage its answer must provide**.
- [ ] Every slot records its **current authoritative evidence** by file +
      section, without restating or copying a rule, formula, or schema
      (`tasks/README.md` §9).
- [ ] Every one of D-1…D-6 in §"Required Decision Coverage" is explicitly
      resolved by the recorded answer, or explicitly recorded as unspecified.
- [ ] Where options are presented, they are drawn from the documents' own text
      and **no option is marked recommended, preferred, or best** anywhere in
      this task.
- [ ] Each slot has a Product Owner input field (`Decision:` + optional
      `Rationale:`) initialized to `<PENDING PRODUCT-OWNER DECISION>`.
- [ ] No decision was made, guessed, ranked, defaulted, or partially answered by
      an agent.
- [ ] Any supplied answer is recorded **verbatim**; no value or wording was
      altered by an agent.
- [ ] **No source code** was modified: zero files under `src/` (byte-identical).
- [ ] **No authoritative documentation changes**: zero files under `docs/` were
      created, modified, renamed, or deleted (byte-identical).
- [ ] **No TASK-159 changes**: `tasks/backlog/TASK-159-*.md` is byte-identical,
      and no `tasks/completed/*` file was modified.
- [ ] **No invented SignalR contract**: no payload member, member name, type,
      optionality, JSON shape, discriminator, method, event, or subscription is
      proposed or authored anywhere in this file.
- [ ] The downstream documentation-resolution task is recorded as the owner of
      the authoritative edit, and is not created or performed here.
- [ ] No ADR was created, and `docs/03-decisions/` is unmodified.
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)   — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[ ] docs/ (SIGNALR_PROTOCOL.md / GAME_STATE.md / GAME_EVENTS.md /
      BOSS_RULES.md / PASSIVE_RULES.md / COMBAT_RULES.md)        — NONE
[ ] docs/00-overview/                                            — NONE
[ ] docs/03-decisions/ADR/                                        — NONE
[ ] tasks/backlog/TASK-159-*.md                                   — NONE (immutable)
[x] tasks/backlog/TASK-160-<this file>.md — this task file only
```

---

## Implementation Notes

- **This task is a decision register, not a resolution.** Its whole deliverable
  is the two recorded answers. If an answer is absent, the correct outcome is to
  report "awaiting Product Owner input" — **not** to author one.
- **Do not "execute" this task while it has no Product Owner answer.** With both
  `Decision:` slots empty, the only correct agent behavior is to report
  **awaiting input** and stop. There is no code to write, no test to run, and no
  document to edit. `Status: BACKLOG` is the correct resting state.
- **D-3 asks for a carrier, not a member.** If an agent or the Product Owner
  reads D-3 as requiring a member name, type, or JSON shape, that is a
  misreading: those belong to the downstream documentation-resolution task. D-3
  answers only *which already-authorized delivery path owns the fact* (or
  "none — server-authoritative").
- **Follow TASK-104's and TASK-061's shape exactly.** Their §4 (Decision
  Questions) is the model for §"Decision Options" here; their recording-
  discipline section is the model for §"Recording Discipline"; their binary
  acceptance criteria are the model for §"Acceptance Criteria"; their
  first stop condition is the model for the "normal starting state" outcome.
- **Cite, do not restate.** Use the `` `DOC.md` §N `` citation idiom. Never copy
  a magnitude, a formula, a schema, a member list, or a wire shape into this
  file (`AGENTS.md` §9, `documentation-change.md` §2, `tasks/README.md` §9).
- **Neutral presentation is the requirement, not a courtesy.** `AGENTS.md` §4
  forbids "pick[ing] whichever rule is easier to implement". An option list that
  ranks options has already made the decision.
- **D-2's coverage item 2 is the most likely source of an incomplete answer.**
  "Deliver Boss state" does not say which facts. If the supplied answer does not
  name them, record it as given and mark that coverage item unspecified — the
  downstream documentation task cannot author a member set from it, and it must
  not invent one.
- **D-6 constrains D-5.** The standing protocol rules
  (`SIGNALR_PROTOCOL.md` §4 item 11, §8 items 5/7) make
  `BattleStateUpdated` the documented carrier and forbid a new message where
  extending the state is the mechanism. If an answer requires a new method or
  event, the stop condition fires — do not record it as settled.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16) — the three stale
  "not implemented" doc comments in `BattleStartService.cs`, `SwapExecution.cs`,
  and `BattleStateService.cs` are reported in TASK-159 and are not this task's
  concern.
- **TASK-159 stays exactly as it is.** Do not edit it, move it, re-status it,
  mark it READY, or rewrite its sections. This task's output is its input.

---

## Testing Requirements

This task changes no code, so it produces no unit, integration, or gameplay
test. Its verification is an **input-completeness check**, a **pre-judgement
check**, a **recording-fidelity check**, and a **scope validation**, at the
depth `core/validation.md` requires for a LOW-risk DOCUMENTATION task.

### Required Verification

```text
[ ] Input completeness       — confirm both §"Decision Options" slots (D-1, D-2)
                               exist, each with its coverage list and a PENDING
                               Product Owner field
[ ] Coverage completeness    — confirm D-1…D-6 are each resolved or explicitly
                               recorded as unspecified
[ ] Pre-judgement check      — confirm no option is recommended, preferred,
                               ranked, or defaulted anywhere in this task
[ ] Recording fidelity check — confirm any supplied answer was transcribed
                               verbatim (wording and values unaltered)
[ ] No-invention check       — confirm no payload member, member name, type,
                               JSON shape, method, event, or subscription is
                               proposed anywhere in this file
[ ] Scope validation         — MVP_SCOPE.md §1/§2 (quality/scope-validation.md)
[ ] Isolation verification   — docs/ (0 files), src/ (0 files), tests/ (0
                               files), TASK-159 and tasks/completed/ unchanged
[ ] Unit tests               — N/A (no code)
[ ] Integration tests        — N/A (no code)
[ ] Gameplay scenarios       — N/A (no code; the scenarios are authored by the
                               implementation tasks downstream)
```

### Key Edge Cases

- **An answer that leaves one of its coverage items unspecified** — record it as
  unspecified and stop short of resolving it; do not infer the missing part.
- **"Boss state" answered without naming facts** — D-2 coverage item 2 is
  unspecified; the downstream task cannot author a member set from it.
- **An answer that implies a new method or event** — D-6's stop condition fires;
  do not record it as settled.
- **An answer that conflicts with an existing documented exclusion** — record
  the answer and report the conflict per `AGENTS.md` §4; do not silently
  reconcile it, and do not edit the document.
- **The two answers arriving separately** — record each independently; do not
  treat one as a default for the other.
- **The Product Owner answering "leave both as they are"** — this is a complete
  and valid outcome (D-1B + D-2B). Record it verbatim; it still requires D-3…D-6
  coverage, and it still unblocks the downstream task (which then records the
  intentional non-delivery explicitly rather than leaving it implicit).

---

## Completion Evidence

### Changed Files

- `tasks/completed/TASK-160-<this file>.md` — this task file only (Product Owner
  decisions D-1A/D-2A recorded with D-3…D-6 coverage). No other file created or
  modified. Zero files under `src/`, `tests/`, or `docs/`.

### Validation Results

```text
Product Owner decisions D-1A and D-2A recorded verbatim with full D-3…D-6
coverage. Decisions cited as authoritative decision source by downstream
documentation task TASK-161 and applied to SIGNALR_PROTOCOL.md v2.15 and
GAME_STATE.md v2.19.
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no code was written)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero files under `src/` modified
- [x] Confirmed zero files under `tests/` modified
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed TASK-159 and all completed tasks are byte-identical
- [x] Confirmed no invented SignalR contract (no member, method, or event)
- [x] Confirmed the downstream documentation-resolution task owns the
      authoritative edit and was not created here
