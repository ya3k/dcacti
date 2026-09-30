# TASK-103 — Resolve the CardCast/PetSkillCast Wire Schema and the Shield Stacking Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK AUTHORS NO CODE AND IMPLEMENTS NO BEHAVIOR. It resolves two
  independent authoritative-document conflicts that currently block
  TASK-102, and records the resulting contract in the canonical owner
  documents.

  BOUNDARY: documentation only. Zero files under src/ or tests/.

  PROVENANCE: the TASK-102 implementation pickup (2026) STOPPED before
  modifying any source file, on two of TASK-102's own named stop
  conditions — §16/§34 item 1 (the CardCast/PetSkillCast wire payload is
  not derivable without inventing a protocol member) and §10/§11/§34
  item 2 (Shield additive stacking cannot be represented on the existing
  StatusEffect contract). TASK-102 was left unchanged in tasks/backlog/;
  this task exists to make it implementable. It resolves ONLY those two
  topics.

  THIS TASK DOES NOT PRESELECT EITHER ANSWER. Both conflicts are recorded
  as open questions with the evidence on each side. Where the
  authoritative evidence does not determine the answer, the executing
  agent must STOP and record the required human/product-owner decision
  rather than choose.
-->

---

## Metadata

```text
Task ID:           TASK-103
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). Both deliverables
                   are contract text in docs/. See "Type classification note".
Status:            BOTH BLOCKERS RESOLVED — Blocker A APPLIED and VERIFIED;
                   Blocker B RESOLVED BY TASK-105 (not by this task).
                   The Product Owner supplied all nine decisions via TASK-104.
                   The wire half (A-1A/A-2C/A-3/A-4B/A-5A) changed no gameplay
                   rule, so it was applied here to `SIGNALR_PROTOCOL.md` §3.2 and
                   `GAME_EVENTS.md` §2; its ten acceptance criteria were
                   independently verified as satisfied by TASK-106. The Shield
                   half (B-1B/B-2/B-3) CHANGES documented gameplay semantics, so
                   per TASK_TYPES.md §2 and §9 item 7 this DOCUMENTATION task did
                   not apply it — it was re-typed GAMEPLAY-CHANGE and delivered
                   by TASK-105 to `COMBAT_RULES.md` §4 (v1.6). This task's
                   recorded Blocker B conflict is therefore SUPERSEDED, not
                   open. See "Blocker A — Resolution (APPLIED)", "Blocker B —
                   Resolution (SUPERSEDED BY TASK-105)", and TASK-106's
                   Completion Evidence.
                   NOTE — the correct lifecycle state of this file is NOT
                   asserted here. Its Blocker B half was closed by a different
                   task, so whether that completes this task is a
                   human/orchestrator call (`TASK_LIFECYCLE.md` §3); no terminal
                   status is claimed by this correction.
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM,
                   MEDIUM "if it affects a cross-referenced contract". Both
                   topics are cross-referenced contracts: the §3.2 event wire
                   schema is cited by GAME_EVENTS.md §3 item 1, and the
                   StatusEffect model is cited by COMBAT_RULES.md §5, BOSS_RULES.md
                   §6.3.1, and GAME_STATE.md §2.4. No code, no protocol
                   implementation, and no gameplay behavior changes here.)
Priority:          HIGH (TASK-102 — ROADMAP.md Phase 1 critical path, the
                   "3 Basic Cards" + "one Pet's Signature Skill" vertical slice —
                   cannot start until both are resolved)
Primary Agent:     review (documentation consistency — TASK_TYPES.md §2
                   DOCUMENTATION "Primary Agent: Review Agent")
Supporting Agents: gameplay (the Shield gameplay rule's canonical owner is
                   COMBAT_RULES.md; the Card effect contract is CARD_RULES.md),
                   realtime (the §3.2 wire schema's canonical owner is
                   SIGNALR_PROTOCOL.md)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation,
                   realtime/realtime-protocol-validation,
                   gameplay/gameplay-behavior-derivation
                   (6 skills — Complex budget, tasks/README.md §12)
Dependencies:      TASK-102 (BACKLOG — the blocked implementation task this
                     task unblocks; read-only, NOT modified, NOT moved, NOT
                     marked READY, and its acceptance criteria NOT rewritten),
                   TASK-095 (DONE — StatusEffect domain state + step-19a
                     lifecycle; context, immutable),
                   TASK-096 (DONE — StatusEffect serialization round trip;
                     context, immutable),
                   TASK-093 (DONE — the StatusEffects instance schema
                     §2.3.1/§2.3.2/§2.3.3 + §5.1.1; precedent for this task's
                     shape, immutable),
                   TASK-094 (DONE — COMBAT_RULES.md §5.3 duration-consumption
                     rule DR1–DR6; context, immutable),
                   TASK-085 (DONE — card-heal / card-shield /
                     card-power-charge / card-inferno / card-tidal-barrier /
                     card-iron-fang definition rows; context, immutable)
Blocks:            TASK-102 (and, through it, the client-side Card-cast UI
                   follow-up and ROADMAP.md Phase 1 completion)
Estimate:          Complex (two independent contracts, four candidate
                   canonical-owner documents, and one cross-document conflict
                   each; no code)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE`.
`TASK_TYPES.md` §2 reserves `GAMEPLAY-CHANGE` for a mechanic that "needs to
behave differently than the documentation currently says, OR a new
undocumented mechanic is being authorized". Neither blocker is a request for
new gameplay: Blocker A's events are already defined by `CARD_RULES.md` §6 and
`GAME_EVENTS.md` §2 (only their *wire shape* is unauthored), and Blocker B is
a **conflict between two existing authoritative statements**, not a request to
change one. The output of both is `docs/` text. However — see §6 — if the
human ruling on Blocker B selects a semantic that genuinely changes the Shield
rule, the executing agent must **STOP** and recommend re-typing that half as
`GAMEPLAY-CHANGE` (`development/gameplay-change.md` §3), rather than editing a
game rule under a DOCUMENTATION task.

**This task authors no ADR and changes no architecture.** Neither resolution
changes layering, storage, the realtime transport, or the authoritative model.
If either proves to require an ADR, that is a STOP condition (§9 item 3).

**This task does not implement TASK-102.** It produces contract text only. The
implementation remains TASK-102's.

---

## Objective

Resolve — and record in their canonical owner documents — the two
authoritative-document conflicts that block `TASK-102` from being implemented,
so that TASK-102 becomes implementation-ready without inventing a protocol
member, a state field, or a gameplay rule:

```text
A. The `CardCast` / `PetSkillCast` wire schema
   GAME_EVENTS.md §2 defines both events semantically; SIGNALR_PROTOCOL.md
   §3.2 owns the wire schema and its discriminator set is documented as
   CLOSED and does not contain them.

B. The Shield stacking / state representation contract
   COMBAT_RULES.md §4 item 3 says multiple Shields stack additively into a
   single absorption pool; GAME_STATE.md §2.3.1 item 6 says there is never
   more than one instance per effect identity and that re-application
   refreshes rather than appends.
```

Both must end in **one unambiguous, implementable contract per topic**, with
`docs/` internally consistent and TASK-102's acceptance criteria implementable
without guessing. Where the authoritative evidence does not determine the
answer, this task must record the required human/product-owner decision
instead of choosing (§6, §9).

---

## Authoritative References

### Blocker A — the CardCast / PetSkillCast wire schema

- `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2 — **the single owner of the
  `ReceiveEvents.events[]` wire schema**; §3.2.1 the serialization boundary;
  §3.2.2 the discriminator table and its closed-set statement; §3.2.3 property
  casing; §3.2.4 enum representation; §3.2.5 optionality (omitted, never
  explicit `null`); §3.2.6–§3.2.19 the per-event member tables and the
  conventions each establishes (`§3.2.14` source/target party strings;
  `§3.2.16` identity-member shape; `§3.2.17` item 3's deferred `effect
  summary` precedent); §3.2.12 "no member outside this schema".
- `docs/02-technical/SIGNALR_PROTOCOL.md` §2 — the method contracts and their
  exact parameters; §2 item 2 (validation ownership); §3 / §3.1 the batch and
  its delivery point; §5 the `{ accepted, reason }` acknowledgement; §6
  sequencing and ordering; §8 item 1 (the wire schema is not an implementation
  detail) and §8 item 7 (the gameplay methods are exactly §2's three).
- `docs/02-technical/GAME_EVENTS.md` §2 — the `CardCast` / `PetSkillCast`
  event **semantics** and their payload description in prose; §3 item 1 — the
  explicit deferral of wire shape to `SIGNALR_PROTOCOL.md` §3.2.
- `docs/01-game-design/CARD_RULES.md` §3 item 4 — the ordered resolution
  (`Deduct Cost → Apply Effect → Emit CardCast, and PetSkillCast if
  applicable`); §6 — the two events' meanings and the
  "in addition to `CardCast`" relationship.
- `docs/01-game-design/GAME_RULES.md` §16 — the canonical event name list
  (`CardCast`, `PetSkillCast` present).
- `docs/02-technical/ARCHITECTURE.md` §2.1 item 4 — the
  `Domain → Application/API transport projection → SignalR → Frontend`
  direction; §2.2.1 rule 4 — the frontend stays opaque to the batch.
- `docs/03-decisions/ADR/ADR-004-signalr-realtime.md` — SignalR as the
  realtime transport (context only; not modified).

### Blocker B — the Shield stacking / state representation contract

- `docs/01-game-design/COMBAT_RULES.md` §4 — **the canonical owner of Heal and
  Shield rules**; item 1 heal clamp and overheal discard; **item 2** the
  absorption pool reduces incoming damage before HP and is "consumed first-in
  on any Final Damage applied to that target"; **item 3** "Multiple Shields
  stack additively into a single absorption pool unless a Relic specifies
  otherwise"; item 4 Heal/Shield are not subject to the Damage Pipeline.
- `docs/01-game-design/COMBAT_RULES.md` §5.1 (the MVP Status Effect list,
  including `Shield` as "absorption pool, see §4"); §5.2 item 1 (every Status
  Effect has a source, a magnitude, and a duration **or** a trigger-based
  expiry) and **item 2** (stacking: "default for MVP is refresh duration, do
  not stack magnitude unless a Card/Relic explicitly says otherwise");
  §5.3 / §5.3.2 (the duration-consumption rule and its explicit
  trigger-based-expiry scope exclusion).
- `docs/02-technical/GAME_STATE.md` §2.3.1 — the `StatusEffect` instance
  schema; **item 3** (`Type` selects exactly one duration model;
  `ExpiryCondition` present for `Shield`, e.g. `"ShieldDepleted"`); **item 6**
  ("There is never more than one instance per effect identity per entity …
  Independent-instance stacking is not an MVP behavior and no second
  representation of it is introduced"); items 7–11 (absence conventions, the
  zero/expired rule, and the deterministic step-19a iteration order);
  §2.3.2 serialization and round trip; §2.3.3 the explicit non-additions;
  §5.1.1 — the lifecycle (item 1 Apply-as-set/refresh, item 7
  "Trigger-based instances are not decremented … it is removed by its own
  documented trigger"); §5.1 the single post-resolution write-back;
  §0 item 5 (no parallel representation).
- `docs/01-game-design/CARD_RULES.md` §2 — the `Shield` Basic Card's Cost and
  Effect (target = the active Pet / `PetState`) and item 3 (Power Charge's
  Cost is 0 by design); §4.1 — `Tidal Barrier`'s "Heal; Gain Shield" effect.
- `docs/01-game-design/GAME_RULES.md` §12 — the Power range invariant;
  §17 step 14 and the Event Resolution order a cast participates in; §18 —
  server authority.
- `docs/00-overview/MVP_SCOPE.md` §1 — Cards (3 Basic + 5 Pet Skill) and
  Combat (including Status Effects) are IN; §2 — the OUT list.
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority (context only).

### Both — precedent for this task's own shape

- `tasks/completed/TASK-093-resolve-status-effects-battle-state-contract.md` —
  the precedent for a DOCUMENTATION contract-resolution task that authored
  `GAME_STATE.md` §2.3.1–§2.3.3 + §5.1.1 and whose §12 records the applied
  decision. **Read-only; immutable.**
- `tasks/completed/TASK-094-resolve-buff-debuff-duration-consumption-timing.md`
  — the precedent for delivering a blocking rule decision (DR1–DR6) that a
  documentation task then applies. **Read-only; immutable.**

---

## Current State

`TASK-102` is `BACKLOG` in `tasks/backlog/` and is **blocked at implementation
pickup**. Its implementation agent stopped before modifying any source file
and reported two of TASK-102's own named stop conditions. Nothing in `src/` was
changed by that attempt, and TASK-102 itself is unmodified.

### Blocker A — `CardCast` / `PetSkillCast` have no wire schema

`SIGNALR_PROTOCOL.md` §3.2 owns the `ReceiveEvents.events[]` wire schema as a
**contract, not an implementation detail** (§8 item 1). Its discriminator table
(§3.2.2) enumerates exactly the 12 event names the protocol currently projects:

```text
MatchCreated, CascadeCreated, ComboChanged, GemMatched, DamageCalculated,
DamageDealt, DamageTaken, PassiveCharged, PassiveTriggered, BossSkillCast,
BattleWon, BattleLost
```

`CardCast` and `PetSkillCast` are **not** among them, and §3.2.2 item 2 states
that set "is closed". §3.2.12 item 1 adds that "The seven events carry exactly
the members tabulated above", and item 3 that no event carries a gameplay field
§2 does not define.

Meanwhile `GAME_EVENTS.md` §2 does define both events, with a payload line
given in prose (`CardId`, `Power cost paid`, `effect summary`; plus, for
`PetSkillCast`, a confirmation that the cast Card was the active Pet's
Signature Skill) — and `GAME_EVENTS.md` §3 item 1 explicitly hands the wire
shape to `SIGNALR_PROTOCOL.md` §3.2.

The consequence, verified in the current code: `BattleEventWireProjection.cs`'s
discriminator switch covers the 12 types and has no arm for either event;
`BattleEvent.cs` has no `BattleEventType` member for either; and
`CARD_RULES.md` §6 + `GAME_RULES.md` §16 define the events with no wire form to
project them to.

**Therefore TASK-102 cannot project either event without this task defining
their wire contract** — adding two names to a set the protocol declares closed
and naming the members of a payload the protocol has never authored. That is a
protocol contract decision, which TASK-102 §16 reserves to this point.

### Blocker B — Shield stacking is contradicted by the StatusEffect contract

`COMBAT_RULES.md` §4 item 3 (canonical owner of the Shield rule) requires
multiple Shields to **stack additively into a single absorption pool**, and
item 2 requires that pool to be **consumed first-in**.

`GAME_STATE.md` §2.3.1 item 6 (canonical owner of the state contract) states
that there is **never more than one instance per effect identity per entity**,
that applying an already-active effect **refreshes** it "rather than appending
a second one", and that "Independent-instance stacking is not an MVP behavior
and no second representation of it is introduced". `§5.1.1` item 1 implements
exactly that (apply-as-set/refresh). §2.3.3 records "No stacking model other
than §5.2 item 2's refresh-in-place".

Three further facts make the gap concrete rather than stylistic:

```text
1. GAME_STATE.md §2.3.2 item 3 gives a `Shield` instance `expiryCondition`
   and NOT `remainingTurns` (§2.3.1 item 3), so there is no Turn counter on
   which a per-instance identity or age could ride.
2. The instance schema (§2.3.1) carries no per-instance grouping key, no
   creation ordinal, and no sequence, so "first-in" consumption across two
   Shields has no representable ordering.
3. §2.3.1 item 6 permits at most one element per `Id`, so two active Shields
   are not representable at all — while §2.3.1 item 1 keeps `Id` an identity
   ("Shield") and not a per-instance key.
```

TASK-102 §10 forbids the obvious escape (no `PetState.Shield`, no
`PetState.ShieldPoints`, no `BattleState.Shield`, and no other parallel
representation), and TASK-102 §11 requires the additive-stacking behaviour to
be proven by a test. The one new `DamagePipeline` step TASK-102 must add
(Shield absorption) therefore has no defined pool to consume.

Note `COMBAT_RULES.md` §5.2 item 2 itself carves Shield's case out of the
refresh default — "**unless a Card/Relic explicitly says otherwise**" — while
§4 item 3 says Shield does otherwise. So the game-design side already asserts
an exception; it is the **state contract** that has no way to express it.
That asymmetry is what this task must settle (§6).

### Consequence

`ROADMAP.md` Phase 1's "3 Basic Cards" and "one Pet fully implemented
(Element, Passive, Signature Skill)" remain unreachable: a Signature Skill
*is* a Pet Skill Card (`CARD_RULES.md` §4 item 1), and the `Shield` Basic Card
is meaningless while its absorption pool has no representation.

---

## Scope

### In Scope

**A. Resolve and record the `CardCast` / `PetSkillCast` wire contract.**

1. Determine the authoritative wire contract for both events from the
   evidence in Authoritative References, and record it in the canonical owner
   document — `SIGNALR_PROTOCOL.md` §3.2 (see §5 for the ownership analysis).
   The resolution must fix, **as applicable to each event**, exactly:
   - the discriminator value for `CardCast` and for `PetSkillCast`;
   - the exact payload member names;
   - the property casing (per `§3.2.3`);
   - which members are required and which are optional (per `§3.2.5`'s
     omitted-never-`null` convention);
   - each member's type;
   - enum representation, if any member is enum-valued (per `§3.2.4`);
   - each member's event-specific semantics;
   - the relationship between `CardCast` and `PetSkillCast`;
   - the emission order when a Pet Skill Card emits both (`CARD_RULES.md`
     §3 item 4 and §6).
2. Reconcile the §3.2.2 discriminator set with the new events — including
   restating what "the set is closed" means once two documented events are
   projected — and reconcile §3.2.12's member-count wording if the resolution
   changes it.
3. Where a payload element is genuinely underspecified by every authoritative
   source (the candidate being `GAME_EVENTS.md` §2's `effect summary`), decide
   its fate **from existing precedent only** — `§3.2.17` item 3 already
   establishes a documented "deferred member" treatment for exactly this
   situation — or STOP and record the required human/product-owner decision
   (§9 item 1). Do not invent a member and do not invent a value set for one.

**B. Resolve and record the Shield stacking / state representation contract.**

4. Resolve the conflict between `COMBAT_RULES.md` §4 item 3 and
   `GAME_STATE.md` §2.3.1 item 6 into **one** authoritative statement, and
   determine the authoritative semantics for all of:
   - a single Shield (apply, magnitude, active lifetime);
   - multiple Shields (whether a second application stacks, refreshes, or is
     rejected);
   - stacking semantics (if additive: what the aggregate is, and on which
     owner it lives);
   - the Shield pool's state representation;
   - consumption order (what "first-in" means deterministically, and how the
     order is fixed — cf. `§2.3.1` item 11 / `§5.1.1` item 6's existing
     determinism precedent);
   - depletion (the `ShieldDepleted` boundary and what happens at exactly 0);
   - the refresh/expiry interaction, including Shield's trigger-based expiry
     under `§5.1.1` item 7.
5. Record the resolution in the smallest set of canonical owner documents
   (§5). If additive stacking remains authoritative, define the aggregate's
   representation **within the existing `StatusEffect` model** where the
   evidence supports it — deriving the pool from
   `PetState.StatusEffects[]`/`BossState.StatusEffects[]` rather than adding a
   member. If the evidence instead supports "Shield does not stack", reconcile
   `COMBAT_RULES.md` §4 item 3 to that and record the reconciliation. If the
   evidence supports neither, STOP (§9 item 2).
6. Confirm, and record in the task's Completion Evidence, that the resolved
   Shield contract satisfies TASK-102's §10 prohibition (no `PetState.Shield`,
   no `PetState.ShieldPoints`, no `BattleState.Shield`, no parallel
   representation) and `GAME_STATE.md` §0 item 5.

### Out of Scope

- **Implementing TASK-102 or any part of it.** No `BattleHub` method, no
  Application use case, no Domain effect resolver, no `DamagePipeline`
  absorption step, no `BattleEvent` member, no
  `BattleEventWireProjection` arm. Those are TASK-102's deliverables.
- **Any source code or test change.** `src/` and `tests/` are untouched
  (§8).
- **Relic triggering**, `RelicTriggered`, `OnCardCast`, and the deferred
  "Burning Curse" Relic (`ROADMAP.md` Phase 1: "No Relics yet").
  `CARD_RULES.md` §3 item 4's downstream Relic step is Phase 2 and is
  reported, not resolved.
- **`GetBattleState`** and reconnect/resync (`SIGNALR_PROTOCOL.md` §7,
  `ROADMAP.md` Phase 3).
- **StatusEffect wire delivery**: whether `StatusEffects[]` ever becomes a
  `BattleStateUpdated` member is a separate protocol decision
  (`SIGNALR_PROTOCOL.md` §4.2 item 2 excludes it today). This task fixes the
  Shield *state* contract and the two *event* wire schemas; it adds no
  state-push member.
- **Any new SignalR method** beyond `SIGNALR_PROTOCOL.md` §2's three.
- **Any new Redis key, field, TTL, or record**; any new API endpoint; any
  PostgreSQL schema, entity, column, constraint, or migration. The Card
  definition rows already exist (TASK-085) and are read, never written.
- **Any client / Phaser / React work**, optimistic prediction, or client-side
  Card validation.
- **Any new gameplay mechanic, Card, Pet Skill, Pet, Boss, Element, Status
  Effect type, resource, or progression system**; no new Cost, magnitude, or
  balance value.
- **Thanh Xà and Sơn Hùng Signature Skills** (`PET_RULES.md` §8,
  `CARD_RULES.md` §4.1) — TBD content; nothing is authored, and no
  placeholder appears anywhere.
- **Match-3, Combo, Boss Response, and damage-formula rules unrelated to the
  Shield step**; no change to any `COMBAT_RULES.md` §3 pipeline step.
- **Player authentication or session behavior** (`ADR-015`).
- **TASK-036, TASK-079, TASK-099, TASK-102, and every `tasks/completed/*`
  file** — untouched.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

All criteria are binary. Criteria 1–9 concern Blocker A; 10–19 Blocker B;
20–23 scope. Criteria marked **(or recorded as a STOP)** are satisfied either
by a recorded resolution or by a §9 STOP report naming the required human
decision — never by a guess.

### Blocker A — the wire contract

<!--
  VERIFIED BY TASK-106 against the applied contract text. Each ticked criterion
  cites the subsection that satisfies it. See TASK-106's Completion Evidence.
-->

- [x] `CardCast`'s wire discriminator value is explicitly defined in
      `SIGNALR_PROTOCOL.md` §3.2 (or recorded as a STOP).
      — §3.2.2's discriminator table lists `CardCast`; §3.2.20's `type` row
      fixes the value `"CardCast"`.
- [x] `PetSkillCast`'s wire discriminator value is explicitly defined in
      `SIGNALR_PROTOCOL.md` §3.2 (or recorded as a STOP).
      — §3.2.2's discriminator table lists `PetSkillCast`; §3.2.21's `type` row
      fixes the value `"PetSkillCast"`.
- [x] The exact payload member names for both events are explicitly defined.
      — §3.2.20 and §3.2.21 each carry a member table: `type`, `cardId` for
      both. No other member is tabulated (§3.2.12 item 1).
- [x] Field casing is explicitly defined and matches `§3.2.3`'s camelCase
      convention.
      — §3.2.20 item 1 and §3.2.21 item 1 fix `cardId` as camelCase and state
      that this section, not `GAME_EVENTS.md` §2's PascalCase prose, fixes the
      wire spelling (§3.2.3 item 1).
- [x] Required-vs-optional status is explicit for every member of both events
      and follows `§3.2.5`'s omitted-never-`null` rule.
      — both member tables carry a `Presence` column reading `always` for
      `type` and `cardId`; §3.2.20 item 3 and §3.2.21 item 4 dispose of the one
      non-member element (`effect summary`) by omission per §3.2.25.
- [x] Every member's type is explicitly defined.
      — both member tables carry a `Type` column; `type` and `cardId` are each
      `string`.
- [x] Enum representation is defined for any enum-valued member, per `§3.2.4`
      (contract-name string, never an ordinal).
      — neither `CardCast` nor `PetSkillCast` carries an enum-valued member, so
      the rule is satisfied vacuously; §3.2.2 item 3 states the discriminator
      itself is projected to its name, not its ordinal.
- [x] The `CardCast` / `PetSkillCast` relationship and their relative emission
      order are explicitly defined, consistent with `CARD_RULES.md` §3 item 4
      and §6.
      — §3.2.22 item 1 fixes `CardCast` then `PetSkillCast` (TASK-104 A-5A);
      item 2 places both at `CARD_RULES.md` §3 item 4's post-Effect step and
      `GAME_RULES.md` §17 step 14; item 4 states a Basic Card cast emits
      `CardCast` alone; §3.2.21 item 5 states non-substitution ("in addition
      to").
- [x] `SIGNALR_PROTOCOL.md` §3.2 no longer contradicts `GAME_EVENTS.md` §2
      (the discriminator set, the §3.2.12 member wording, and the payload
      descriptions agree).
      — §3.2.2 item 2 restates the closed set as closed against events
      `GAME_EVENTS.md` §2 does not define; §3.2.12 item 1's stale "seven
      events" count is corrected to the 16-name enumeration; `GAME_EVENTS.md`
      §2 items 2–5 cross-reference §3.2.21/§3.2.22 and record the same
      `CardId` confirmation and emission order, and its `effect summary`
      wording matches §3.2.25.
- [x] No undocumented wire member remains necessary for TASK-102 to project
      either event — verified by reading TASK-102's acceptance criteria
      against the authored schema.
      — TASK-102's two projection criteria require a `BattleEventType` member
      and a `BattleEventWireProjection` arm producing the protocol's
      conventions; §3.2.20–§3.2.22 give the complete shapes, and §3.2.25
      disposes of `effect summary` without requiring a member. No gap remains.

### Blocker B — the Shield contract

- [ ] The conflict between `COMBAT_RULES.md` §4 item 3 and `GAME_STATE.md`
      §2.3.1 item 6 is explicitly resolved in the canonical owner document(s),
      or recorded as a STOP naming the required human gameplay decision.
- [ ] One-Shield behavior is deterministic (apply, magnitude, active lifetime).
- [ ] Multiple-Shield behavior is deterministic.
- [ ] Stacking semantics are deterministic.
- [ ] The Shield pool's state representation is deterministic.
- [ ] Shield consumption order is deterministic ("first-in" is defined in
      terms a test can assert).
- [ ] Shield depletion behavior is deterministic, including the exact-0
      boundary and the `ShieldDepleted` expiry.
- [ ] The Shield refresh/expiry interaction is deterministic, including its
      relationship to `§5.1.1` item 7's trigger-based (non-decremented) rule.
- [ ] The resolved representation requires no undocumented parallel `Shield`
      field — no `PetState.Shield`, no `PetState.ShieldPoints`, no
      `BattleState.Shield` (`GAME_STATE.md` §0 item 5).
- [ ] `COMBAT_RULES.md` and `GAME_STATE.md` agree after the decision; neither
      contradicts the other, and neither restates the other's rule
      (`.ai/workflow/documentation/documentation-change.md` §2).
- [ ] TASK-102's Shield acceptance criteria (its "Shield absorption works",
      "Multiple Shields stack additively", and "`Shield` grants … a Shield
      absorption pool" items) can be implemented from the resolved contract
      without guessing.

### Scope, authority, and consistency

- [ ] No source code is changed: `src/` is byte-identical.
- [ ] No test is changed to encode the new contract: `tests/` is
      byte-identical.
- [ ] No unrelated gameplay rule is changed, and no new gameplay mechanic is
      introduced.
- [ ] TASK-102 remains unchanged — same content, same `Status: BACKLOG`, same
      file location, acceptance criteria not rewritten.
- [ ] No `tasks/completed/*` file is modified; TASK-036, TASK-079, and
      TASK-099 are unmodified.
- [ ] No ADR is created or edited; no `docs/00-overview/` file is modified.
- [ ] Zero files under `docs/03-decisions/` are modified.
- [ ] Documentation consistency audit passes across every document touched
      plus every document that references them (`quality/documentation-consistency`).
- [ ] Scope validated against `MVP_SCOPE.md` §1 (Cards and Combat/Status
      Effects are IN; no OUT item is reached).
- [ ] Quality review checklist passes (`quality/review.md` §1), skipping only
      the code-only items per `.ai/workflow/documentation/documentation-change.md`
      §4.
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 /
      ADR-001).

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)   — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[x] docs/02-technical/SIGNALR_PROTOCOL.md   (Blocker A — canonical owner of
      the §3.2 wire schema; the discriminator set and two event subsections)
[x] docs/02-technical/GAME_STATE.md          (Blocker B — the StatusEffect
      state contract; §2.3.1/§2.3.2/§2.3.3/§5.1.1 as the resolution requires)
[?] docs/01-game-design/COMBAT_RULES.md      (Blocker B — ONLY if the
      resolution requires the Shield gameplay rule to be reconciled or
      cross-referenced; its §4 is the canonical owner)
[?] docs/02-technical/GAME_EVENTS.md         (Blocker A — ONLY if the
      resolution changes the events' payload description; §2 is the
      semantics owner and §3 item 1 defers wire shape elsewhere)
[ ] docs/00-overview/                        — NONE
[ ] docs/03-decisions/ADR/                    — NONE
[x] tasks/ (this file only)
```

Per §5, the executing agent must reduce `[?]` to `[x]` or `[ ]` based on the
resolution it actually records, and **must not touch a document it does not
need to**.

---

## Implementation Notes

- **`documentation-change.md` §2 is the governing rule: one concept, one
  owner.** Do not duplicate a rule to make it easier to find. Where a
  resolution lands in one document, every other document **references** it —
  it does not restate it.
- **Use TASK-093 §12 and TASK-099 as the shape precedent.** Both are recent
  DOCUMENTATION contract-resolution tasks; TASK-093 §12 is the model for
  recording *where* a decision was applied and *why* that owner was chosen,
  and for preserving an original STOP report unaltered as historical record.
- **Blocker A's owner is `SIGNALR_PROTOCOL.md` §3.2, and this is already
  settled by the docs — do not re-litigate it.** `GAME_EVENTS.md` §3 item 1
  and §3.2.1's boundary both assign the wire schema to §3.2 ("the split is
  *semantics vs. wire schema*, and §3.2 is the single owner of the latter").
  `GAME_EVENTS.md` §2 therefore needs an edit only if the resolution changes
  what it says the payload *contains*.
- **Blocker A: read the existing conventions before naming anything.**
  `§3.2.14` (party strings `"player"`/`"boss"`), `§3.2.16`/`§3.2.18` (the
  identity-member shape `skillId`/`sourceId`/`passiveId`), `§3.2.13` (the
  all-members-always-present pipeline breakdown), and `§3.2.17` item 3 (a
  documented deferred member) are the four precedents that should drive the
  new schema. Do not invent a fifth convention.
- **Blocker A: `CardId` and the definition identity.** Any `cardId` wire
  member must agree with `PetState.EquippedCards[]`'s element
  (`GAME_STATE.md` §2.3 — a `CardDefinitionId`, `DATABASE.md` §1) and with the
  provisioned rows (`card-heal`, `card-shield`, `card-power-charge`,
  `card-inferno`, `card-tidal-barrier`, `card-iron-fang`, TASK-085). Do not
  define a second identifier.
- **Blocker A: the two documented rejections are not wire members.**
  `SIGNALR_PROTOCOL.md` §5's `{ accepted, reason }` is the direct invocation
  result, not a Battle Event (§5 items 1 and 4); no `reason` belongs in an
  `events[]` item. Rejection *codes* are owned per action by the domain
  document (§5 item 3) — `CARD_RULES.md` §3 for a cast — and are therefore
  TASK-102's concern, not a member to add here.
- **Blocker B: derive the pool from `StatusEffects[]` if the evidence allows
  it.** `GAME_STATE.md` §2.3.1 item 3 already assigns `Shield` the
  trigger-based expiry model and `§5.1.1` item 7 already exempts it from the
  step-19a countdown, so the collection is the natural owner. A new
  `PetState`/`BattleState` member would violate `GAME_STATE.md` §0 item 5 and
  is the STOP condition in §9 item 3.
- **Blocker B: the resolution must be expressible as state.** Whatever
  semantics are chosen must be representable with the §2.3.1 instance schema
  as resolved — additive magnitude and a deterministic consumption order must
  both be *readable from the committed state*, not merely computable during a
  resolution, because the pool persists across resolutions and must survive
  the `REDIS_STATE.md` §7 round trip.
- **Blocker B: do not assume additive stacking is the answer.** The evidence
  points both ways (`COMBAT_RULES.md` §5.2 item 2's "unless a Card/Relic
  explicitly says otherwise" versus §4 item 3's flat assertion). Analyze
  §6's evidence, then record the smallest deterministic resolution — or STOP.
  Do not choose based on implementation convenience, and do not silently
  privilege one document.
- **Cite, do not restate.** Keep the `<c>DOC.md</c> §N` citation idiom the
  existing contracts use. Never copy a Cost, an effect magnitude, or a
  formula into a task file or a comment (`AGENTS.md` §9,
  `documentation-change.md` §2).
- **`AGENTS.md` §4 governs.** Both blockers are documentation conflicts, so
  the resolution path is: detect → identify both sources → explain → determine
  the owner per `AGENTS.md` §2 → propose the smallest correction → **obtain
  the human approval the conflict requires** → only then edit. A DOCUMENTATION
  task may apply a decision the evidence already determines; it may not
  author a gameplay or protocol decision on its own authority.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16) — e.g. the
  `BattleStateJson.cs` items TASK-097/TASK-098 reported, and the deferred
  Thanh Xà / Sơn Hùng Signature Skill content. Record them in Completion
  Evidence; do not resolve them here.
- **TASK-102 stays exactly as it is.** Do not edit it, move it, re-status it,
  mark it READY, or rewrite its acceptance criteria. This task's output is the
  contract TASK-102 will later be implemented against.

---

## Testing Requirements

This task changes no code, so it produces no unit, integration, or gameplay
test. Its verification is a **documentation consistency audit** plus **scope
validation**, at the depth `core/validation.md` requires for a MEDIUM-risk
DOCUMENTATION task.

### Required Verification

```text
[x] Documentation consistency audit — across every document touched AND every
                                     document that references them:
                                       SIGNALR_PROTOCOL.md §3.2 ↔ GAME_EVENTS.md
                                         §2/§3 ↔ CARD_RULES.md §6
                                       GAME_STATE.md §2.3.1/§2.3.2/§2.3.3/
                                         §5.1.1 ↔ COMBAT_RULES.md §4/§5.1/§5.2
                                       BOSS_RULES.md §6.3.1 (the other
                                         StatusEffect consumer) — checked for
                                         staleness, not edited
[x] Round-trip obligation review      — confirm the resolved Shield
                                     representation survives the
                                     REDIS_STATE.md §7 item 9 / GAME_STATE.md
                                     §2.3.2 round trip with no new key or
                                     Redis-only field
[x] Scope validation                  — MVP_SCOPE.md §1/§2; `quality/scope-validation`
[x] Contract-completeness check       — read TASK-102's acceptance criteria
                                     against the authored contract and confirm
                                     each is implementable without guessing
[ ] Unit tests                        — N/A (no code)
[ ] Integration tests                 — N/A (no code)
[ ] Gameplay scenarios                — N/A (no code; the scenarios are authored
                                     by the implementation tasks)
```

### Key Edge Cases

Derived from the two contracts' own boundary statements — these are the cases
the *resolved wording* must answer unambiguously, so that the later
implementation task can test them:

```text
Wire contract
- A Pet Skill Card cast: BOTH events in one batch, in the documented order
  (CARD_RULES.md §3 item 4, §6) — the schema must make the pair unambiguous.
- A Basic Card cast: only CardCast — the schema must not imply PetSkillCast
  is always present.
- A Pet whose Signature Skill is not content-defined (Thanh Xà, Sơn Hùng,
  PET_RULES.md §8) — the schema states nothing content-specific; no member
  may encode a Skill identity that does not exist.
- An optional member absent: omitted, never explicit null (§3.2.5).

Shield contract
- Shield applied once        — one active pool of the documented magnitude.
- Shield applied twice       — the resolved rule's outcome, stated so a test
                               can assert it.
- Damage exactly equal to the pool  — the §2.3.1 item 3 / "ShieldDepleted"
                               boundary: pool reaches 0, HP unchanged.
- Damage less than the pool — pool reduced, HP unchanged.
- Damage greater than the pool — HP reduced by exactly the remainder.
- The pool at 0             — whether the instance is removed in the same
                               resolution (§5.1.1 item 4's removal-not-stored
                               precedent applied to a trigger-based instance).
- Shield and a Turn countdown — Shield is trigger-based (§5.1.1 item 7) and
                               must not acquire a duration.
```

---

## Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific:

1. **If the intended `CardCast` / `PetSkillCast` payload cannot be determined
   from authoritative product/game-design decisions** — specifically, if a
   member `GAME_EVENTS.md` §2 lists only in prose (`effect summary` is the
   known candidate) has no value set or representation derivable from an
   existing precedent such as `SIGNALR_PROTOCOL.md` §3.2.17 item 3 —
   **STOP and record the required human/product-owner decision.** Do not
   invent a member or a value set.
2. **If the Shield stacking semantics cannot be determined without a human
   gameplay decision** — i.e. the evidence in `COMBAT_RULES.md` §4 item 3 /
   §5.2 item 2 and `GAME_STATE.md` §2.3.1 item 6 does not determine whether
   Shield stacks, refreshes, or rejects a second application — **STOP per
   `AGENTS.md` §4/§7 and record the decision required.**
3. **If the required Shield representation would require a new architectural
   decision not already authorized** — e.g. a new `PetState`/`BattleState`
   member, a new persistence shape, or an ADR — **STOP
   (`GAME_STATE.md` §0 item 5, TASK-102 §10).**
4. **If two authoritative documents still conflict after analysis** —
   including a conflict this task's edit would create with a third document —
   **STOP per `AGENTS.md` §4** and report both sources.
5. **If a decision would change the MVP scope** — e.g. it requires Relics,
   `GetBattleState`, or a StatusEffect wire member — **STOP per `AGENTS.md`
   §8** (`MVP_SCOPE.md` §2).
6. **If a decision would introduce a new gameplay mechanic** — a new Status
   Effect type, a new Card or Pet Skill, an overheal/temp-HP rule, or any new
   Cost or magnitude — **STOP; do not author it.**
7. **If the repository's task workflow does not permit a
   documentation/contract-resolution task for this situation** — e.g. the
   resolution proves to be a `GAMEPLAY-CHANGE` (`COMBAT_RULES.md`'s Shield
   rule genuinely changing) rather than a documentation reconciliation —
   **STOP and recommend re-typing that half per `TASK_TYPES.md` §2 /
   `development/gameplay-change.md` §3**, rather than editing a game rule
   under this task's type.
8. **If satisfying any criterion requires modifying TASK-102, TASK-036,
   TASK-079, TASK-099, or any `tasks/completed/*` file: STOP** — report
   instead (`TASK_LIFECYCLE.md` §3: completed tasks are immutable).
9. **If the task exceeds 7 skills or crosses an uncoupled architectural
   boundary: STOP and decompose** (`tasks/README.md` §13) — do not expand it.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual. Record a §9 STOP report here instead if a
  human decision is required — do not claim a resolution that was not made.
-->

**Picker-up note.** Record before editing: (a) the SHA256 of
`docs/02-technical/SIGNALR_PROTOCOL.md`, `docs/02-technical/GAME_STATE.md`,
`docs/01-game-design/COMBAT_RULES.md`, `docs/02-technical/GAME_EVENTS.md`, and
`tasks/backlog/TASK-102-implement-card-cast-server-path.md` as the
change-isolation baseline; (b) the output of `git status --short`. The working
tree carries pre-existing uncommitted changes from earlier tasks (including
several `docs/` files), so the baseline is the working-tree content at pickup,
**not** `HEAD`, and no unrelated change may be reverted or folded in.

### Pickup Baseline (recorded)

```text
8B9832AE25D779B3AF9EAC108BEA333066383BAE20BE6DADE8081A2B007D9E25  docs/02-technical/SIGNALR_PROTOCOL.md
7004B262B7B4CFDDA7FE479038D9C281299040FF983CDFF828AABE6C6481609B  docs/02-technical/GAME_STATE.md
6484133C4123901276FA50AB2E4B7FFC40A20F5A7098AB128D75EA98CE5D944C  docs/01-game-design/COMBAT_RULES.md
299A318EBB8D638B746371363DD529A28969F9C9163039D4E1AD2A74611198B5  docs/02-technical/GAME_EVENTS.md
24D14B21EDBDC6105C5BB61001253571CF91D47FBED64931184981BEEE45A773  tasks/backlog/TASK-102-implement-card-cast-server-path.md
```

`git status --short` at pickup recorded pre-existing modifications to
`docs/01-game-design/{BOSS_RULES,COMBAT_RULES,GAME_RULES}.md`,
`docs/02-technical/{API_CONTRACTS,ARCHITECTURE,DATABASE,GAME_STATE,TDD}.md`,
several `src/` and `tests/` files, and the untracked
`StatusEffect*`/TASK-086…TASK-102 files. None was reverted, and none was
folded into this task.

### Status: PARTIALLY RESOLVED — Blocker A APPLIED; Blocker B STOPPED

> **UPDATE (third execution, 2026-09-30).** The Product Owner supplied all nine
> decisions via `TASK-104`. **Blocker A is now RESOLVED and APPLIED** to
> `SIGNALR_PROTOCOL.md` §3.2 (see "Blocker A — Resolution (APPLIED)" below).
> **Blocker B remains STOPPED**, because the B-1B ruling changes documented
> gameplay semantics and must be re-typed `GAMEPLAY-CHANGE` before any
> `COMBAT_RULES.md` §4 edit — which this DOCUMENTATION task may not perform
> (§9 item 7). See "Blocker B — STOP CONDITION (STILL OPEN)".

**First/second execution: both blockers fired their task-specific stop
conditions and no authoritative document was modified.** Those two §9 STOP
reports are retained below unaltered as the historical record of why the
contracts could not be authored then.

---

## STOP CONDITION — Blocker A (`CardCast` / `PetSkillCast` wire schema)

> **RESOLVED AND APPLIED.** The decisions this report asked for were supplied as
> TASK-104's A-1A / A-2C / A-3 / A-4B / A-5A, and they have been applied to
> `SIGNALR_PROTOCOL.md` §3.2. The original STOP report is retained below
> unaltered as the historical record.

```text
Problem:
  The `CardCast` / `PetSkillCast` wire payload cannot be defined from existing
  authoritative evidence. The task's §9 item 1 named `effect summary` as "the
  known candidate", but the audit found the underivable set is wider: TASK-102
  cannot project either event without inventing several protocol members and
  the events' relative emission order.
```

**Relevant sources:**

```text
SIGNALR_PROTOCOL.md §3.2.2 item 2   "The allowed values are exactly the names in
                                    the discriminator table above — the set is
                                    closed." CardCast/PetSkillCast are
                                    affirmatively EXCLUDED, not merely unstated.
SIGNALR_PROTOCOL.md §3.2.3 item 1   "All wire property names are camelCase,
                                    spelled exactly as the tables below give
                                    them." There is NO table for either event,
                                    so the casing rule has no spelling source.
SIGNALR_PROTOCOL.md §3.2.12 item 1  "The seven events carry exactly the members
                                    tabulated above." The table has 12 names and
                                    there are 11 per-event subsections (§3.2.19
                                    covers two) — the "seven" wording is already
                                    internally inconsistent. (§3.2.4 item 3's
                                    "in any of the four events" is a third,
                                    stale count.)
SIGNALR_PROTOCOL.md §3.2.17 item 3  the ONLY explicit deferred-member precedent:
                                    "`effect summary` is deferred per
                                    GAME_EVENTS.md §2 item 3 and is not a wire
                                    member yet."
SIGNALR_PROTOCOL.md §3.2.18         the CONTRADICTING precedent: BossSkillCast's
                                    §2 `effect summary` is disposed of by SILENT
                                    OMISSION (only type/skillId/sourceId are
                                    tabulated) — two inconsistent precedents.
SIGNALR_PROTOCOL.md §2 line 103     the hub method spells the card id `cardId`
                                    (client → server surface, not `events[]`).
SIGNALR_PROTOCOL.md §3.2.16/§3.2.18 the identity-member shape: `passiveId`,
                                    `skillId`, `sourceId` — a pattern, not a
                                    stated naming rule.
GAME_EVENTS.md §2 CardCast block    the entire definition is four lines with no
                                    numbered items:
                                      "Payload: CardId, Power cost paid,
                                       effect summary
                                       PetSkillCast additionally confirms it was
                                       the active Pet's Signature Skill"
GAME_EVENTS.md §2 PassiveCharged    item 1 names "`CardCast`'s `CardId`" as an
  item 1                            identity member in PascalCase — semantics,
                                    not wire shape.
GAME_EVENTS.md §3 item 1            the wire schema is deferred to
                                    SIGNALR_PROTOCOL.md §3.2, "the single owner".
GAME_EVENTS.md §3 item 5            "every event defined in §2 belongs to the
                                    ordered list of §1 — none is defined as
                                    presentation-only", so both events MUST
                                    eventually be on the wire.
CARD_RULES.md §3 item 4             "Emit CardCast event (and PetSkillCast, if
                                    applicable)" — the two are grouped into ONE
                                    unordered step.
CARD_RULES.md §6                    "in addition to CardCast" — co-emission and
                                    non-substitution, but NO sequence.
CARD_RULES.md §2 / §4.1             per-Card DEFINITION costs only ("Cost: 20
                                    Power" etc.). Nothing labels them "cost paid".
GAME_RULES.md §16                   lists both event names (canonical list).
GAME_RULES.md §17 step 14           "Resolve Player Effects" is a SINGLE step with
                                    NO 18a/19a-style expansion, so GAME_EVENTS.md
                                    §1's vertical order has nothing to rest on.
API_CONTRACTS.md §5.3               REST spells a card identity `cardId` — a
                                    genuine camelCase precedent, but a REST
                                    member, not an event member.
API_CONTRACTS.md line 776           CardDefinition data "not exposed" includes
                                    `powerCost` — so the client cannot derive it.
```

**Conflict / missing information:**

```text
1. Discriminator values — AFFIRMATIVELY excluded by §3.2.2 item 2, which is a
   positive prohibition, not a gap.

2. `Power cost paid` — MEMBER NAME undefined (`powerCostPaid`? `costPaid`?
   `powerCost`? `cost`?), TYPE undefined, VALUE SET undefined. A repo-wide grep
   of docs/ for `discount` / cost reduction returns ZERO matches, so whether
   "paid" equals the definition cost is UNANSWERED rather than answered. The two
   readings are extensionally identical in MVP only by absence of any cost
   modifier — an inference from a documentation void, not a documented rule.

3. `effect summary` — name, type, and value set undefined for this event. Unlike
   `PassiveTriggered`, its own §2 block carries NO deferral annotation, and the
   two §3.2 precedents for disposing of an unrepresentable member CONTRADICT
   each other (explicit deferral §3.2.17 item 3 vs. silent omission §3.2.18).

4. PetSkillCast's "confirmation ... Signature Skill" — NO MEMBER IS NAMED AT
   ALL. Three readings are equally consistent with the four lines of §2:
   (a) a distinct unnamed/untyped member, (b) carried by the event's own
   existence, (c) carried by the `CardId` value alone.

5. The card identity MEMBER NAME on the event — `CardId` (§2, PascalCase) vs.
   `cardId` (§2 hub method, §3.2.3, API_CONTRACTS.md §5.3). No document states
   the event member; only cross-event pattern inference points to `cardId`, and
   §3.2.3 fixes casing via tables that do not exist for this event.

6. Emission order of CardCast vs PetSkillCast — UNDEFINED. CARD_RULES.md §3
   item 4 groups them as one unordered step; §6 gives "in addition to" with no
   sequence; GAME_RULES.md §17 has no step-14 expansion for GAME_EVENTS.md §1's
   vertical listing to rest on.

7. Same class, discovered and REPORTED not resolved: `RelicTriggered` and
   `PowerChanged` are ALSO absent from the 12-name "closed" set despite
   GAME_EVENTS.md §2 payload definitions and §1 emission-order placement — a
   second and third instance of the identical gap.
```

**Proposed resolution:**

```text
The wire schema owner is SIGNALR_PROTOCOL.md §3.2 (GAME_EVENTS.md §3 item 1
names it "the single owner"), so the correction lands there — not in
GAME_EVENTS.md, whose §2 semantics are not in question.

The smallest correction is ONE product-owner decision covering the payload,
then a §3.2.20/§3.2.21 pair of per-event member tables written to the existing
conventions. The decision required:

  A-1. Ratify the cardinality change. §3.2.2 item 2's "the set is closed" must
       be restated as "closed against events GAME_EVENTS.md §2 does not define"
       so that a §2-defined event is projected by adding a §3.2 subsection.
       GAME_EVENTS.md §3 item 5 already requires this ("every event defined in
       §2 belongs to the ordered list of §1 — none is defined as presentation-
       only"). The same restatement covers RelicTriggered and PowerChanged.
       Also correct §3.2.12 item 1's "the seven events" and §3.2.4 item 3's
       "the four events" to the actual counts.

  A-2. `Power cost paid` — decide whether this member exists, and if so its
       name, type, and value set. Options: (i) omit it and rely on the
       definition cost (which the client already cannot read —
       API_CONTRACTS.md line 776 withholds `powerCost`); (ii) send the
       definition cost; (iii) send the amount actually deducted, which requires
       stating that paid ≡ defined for MVP. Do NOT invent a name.

  A-3. `effect summary` — decide whether to apply the §3.2.17 item 3 explicit
       deferral (recommended: it is the only precedent that keeps the member
       visible as "not yet reported" rather than silently absent) or the
       §3.2.18 silent-omission style. The two precedents must be reconciled so
       one convention governs.

  A-4. PetSkillCast's Signature-Skill confirmation — decide which of the three
       readings governs. Reading (c) needs no member and matches CARD_RULES.md
       §1 item 4 (the Signature Skill Card is DERIVED from
       `PetDefinition.SignatureSkillCardId` and "is not submitted"), so the fact
       is server-known and the client can resolve it from `cardId` — but no
       document says this, so it must be stated.

  A-5. Emission order — state CardCast before PetSkillCast (the order
       GAME_EVENTS.md §1's list already shows) or the reverse. Either way it
       must be written down, because GAME_RULES.md §17 step 14 is not expanded.
```

**Waiting for:**

```text
A human/product-owner decision on A-1 through A-5. AGENTS.md §4 requires the
conflict to be reported and the smallest correction proposed — not applied —
and forbids choosing on implementation convenience. No protocol member, value
set, or ordering was invented in the meantime.
```

---

## STOP CONDITION — Blocker B (Shield stacking / state representation)

```text
Problem:
  COMBAT_RULES.md §4 item 3 and GAME_STATE.md §2.3.1 item 6 assert mutually
  incompatible rules, and NO document adjudicates between them. Resolving it
  in either direction requires an act this DOCUMENTATION task may not take:
  either changing a gameplay rule (→ GAMEPLAY-CHANGE, §9 item 7) or adding a
  state member (§9 item 3, GAME_STATE.md §0 item 5).
```

**Relevant sources:**

```text
COMBAT_RULES.md §4 item 3   "Multiple Shields stack additively into a single
                            absorption pool unless a Relic specifies otherwise."
COMBAT_RULES.md §4 item 2   the pool "reduces incoming damage before HP is
                            affected, consumed first-in on any Final Damage
                            applied to that target." "First-in" is a bare
                            phrase — no ordering key or tie-break exists
                            anywhere in docs/.
COMBAT_RULES.md §5.2 item 2 "default for MVP is refresh duration, do not stack
                            magnitude unless a Card/Relic explicitly says
                            otherwise" — the carve-out is a CONTENT OBJECT,
                            not a sibling rule. §4 item 3's own carve-out names
                            RELIC only, and no MVP Relic or Card says anything
                            about Shield.
COMBAT_RULES.md §5.3.2      trigger-based expiry "does not use the Turn
                            countdown".
GAME_STATE.md §2.3.1 item 6 "There is never more than one instance per effect
                            identity per entity ... the array holds at most one
                            element per `Id`. Independent-instance stacking is
                            not an MVP behavior and no second representation of
                            it is introduced (§0 item 5)." — cites §5.2 item 2
                            as authority FOR refresh-in-place and NEVER honors
                            the carve-out.
GAME_STATE.md §2.3.1 item 1 "`Id` is an identity, not a definition" — so it is
                            "Shield", NOT a per-instance key.
GAME_STATE.md §2.3.1 item 3 Shield uses `ExpiryCondition`, NOT `RemainingTurns`
                            — no Turn counter exists to carry an age.
GAME_STATE.md §2.3.1 item 5 "`ExpiryCondition` is a condition label, not a
                            rule ... The condition's behavior is owned by
                            COMBAT_RULES.md §4 and §5.1 and is not restated
                            here." — CIRCULAR: those sections do not define it.
GAME_STATE.md §2.3.2 item 4 no `elapsedTurns`/`appliedTurn`/`duration`/
                            `refreshedAt` — "a second counter representing the
                            same quantity is exactly the parallel
                            representation §0 item 5 forbids."
GAME_STATE.md §2.3.3        "No stacking model other than §5.2 item 2's
                            refresh-in-place."
GAME_STATE.md §5.1.1 item 1 Apply is "a 'set', not an increment" — re-applying
                            "does not append a second element, and it does not
                            add to the current value."
GAME_STATE.md §5.1.1 item 6 the consumption order is "by `Id` in ordinal
                            ascending order" — but `Id` is the effect identity,
                            so the order collapses to ONE element and cannot
                            order two Shields. This precedent does NOT rescue
                            "first-in".
GAME_STATE.md §5.1.1 item 7 Shield "is not touched by the step 19a countdown;
                            it is removed by its own documented trigger
                            (COMBAT_RULES.md §4, §5.2 item 1)" — a deferral with
                            no destination.
GAME_STATE.md §0 item 5     "No stage introduces a parallel representation of a
                            concept another stage already owns." — the hard
                            constraint forbidding a `ShieldPoints` pool.
GAME_STATE.md §2.3 tree     the instance schema has EXACTLY 7 members
                            (`Id`/`Type`/`Source`/`Magnitude`/`TargetStat`/
                            `RemainingTurns`/`ExpiryCondition`): NO grouping
                            key, NO creation ordinal, NO sequence.
CARD_RULES.md §2            the Shield Card's magnitude ("20% of its Max HP").
CARD_RULES.md §4.1          Tidal Barrier: "Heal; Gain Shield" — NO magnitude.
PASSIVE_RULES.md §8 /       Huyền Quy's Passive: "Gain Shield = 15% Max HP" — a
  PET_RULES.md §8           THIRD Shield source with a SECOND magnitude.
REDIS_STATE.md §7 item 9    the round-trip obligation: the pool "must survive
                            the round trip" as committed state.
REDIS_STATE.md §2 item 1    the JSON "matches the shape in GAME_STATE.md §2
                            exactly — no additional Redis-only fields".
BOSS_RULES.md §5 item 5     Stun's cross-reference — checked for staleness, NOT
                            edited (see "Stale-documentation check" below).
```

**Conflict / missing information:**

```text
1. THE CORE CONFLICT IS TWO-SIDED AND UNADJUDICATED. COMBAT_RULES.md §4 item 3
   asserts an exception; GAME_STATE.md §2.3.1 item 6 + §2.3.3 forbid it flatly
   and without carve-out. They are not two spellings of one rule — one asserts
   an exception the other prohibits.

   AGENTS.md §2's precedence would rank COMBAT_RULES.md (specific domain rule)
   above GAME_STATE.md (technical doc), BUT AGENTS.md §4 forbids applying the
   hierarchy silently — "STOP — do not change behavior until a human approves
   the correction" — and the winning reading still collides with §0 item 5.

2. ADDITIVE STACKING IS UNREPRESENTABLE AS WRITTEN. Two concurrent Shields
   cannot exist (item 6 caps at one element per `Id`); even if they could, there
   is no member for "first-in" to ride (no ordinal, no grouping key, no
   sequence, and item 3 denies Shield a Turn counter).

3. THE `ShieldDepleted` RULE IS MISSING ENTIRELY. §2.3.1 item 5 and §5.1.1
   item 7 both delegate the depletion behavior to COMBAT_RULES.md §4/§5.1 — and
   those sections do not define it. Nothing states whether the instance is
   removed in the same resolution, whether a 0-magnitude Shield persists, or
   whether damage exactly equal to the pool leaves HP unchanged. GAME_RULES.md
   §17 step 19a covers ONLY DoT ticks and Turn-based durations, so NO resolution
   step exists at which a Shield can deplete. This is an independent AGENTS.md
   §7 "missing rule" stop. Even the label's canonical value set is unauthored.

4. THE MAGNITUDE QUESTION IS ALSO OPEN. Shield arrives from three sources with
   two magnitudes (CARD_RULES.md §2 "20% Max HP"; PASSIVE_RULES.md §8 "15% Max
   HP"; CARD_RULES.md §4.1 Tidal Barrier gives NO number). If Shields stack,
   whether unlike magnitudes sum, refresh, or are rejected is unstated.

5. NO ADJUDICATOR EXISTS. No ADR covers Shield or StatusEffect stacking
   (ADR-011 mentions `StatusEffects[]` only as a `PetState` member listing).
   GAME_RULES.md §14 — COMBAT_RULES.md's parent — says nothing about Shield
   stacking, so the parent document does not break the tie. COMBAT_RULES.md §4
   itself carries no canonical-ownership self-declaration.
```

**Proposed resolution:**

```text
This is a GAMEPLAY decision, not an edit this DOCUMENTATION task may make
(§5, §9 item 2, §9 item 7). Two coherent rulings exist, each with a different
downstream obligation:

  B-1. RETAIN additive stacking (COMBAT_RULES.md §4 item 3 governs, by §2
       precedence). Then the STATE CONTRACT must be extended to represent an
       aggregate pool, and GAME_STATE.md §2.3.1 item 6 / §2.3.3 / §5.1.1 item 1
       must be reconciled to it. Because §0 item 5 forbids a parallel
       `PetState.Shield`/`ShieldPoints`/`BattleState.Shield` member, the
       aggregate must be carried WITHIN the existing instance schema — which
       today has no member that can hold it. This half is therefore an
       ARCHITECTURE-adjacent state-contract change, and it requires stating a
       deterministic consumption order (a property the existing §5.1.1 item 6
       `Id`-ordinal order cannot supply).

  B-2. RULE THAT SHIELD DOES NOT STACK (GAME_STATE.md §2.3.1 item 6 governs).
       A second Shield application refreshes the existing instance. Then
       COMBAT_RULES.md §4 item 3 must be RECONCILED to say so, and §4 item 2's
       "consumed first-in" becomes vacuous (there is only ever one pool) and
       should be restated as "consumed before HP".

  Under EITHER ruling these must also be decided, because neither document
  answers them:

  B-3. The `ShieldDepleted` boundary: what happens at exactly 0, and in which
       resolution the instance is removed. This is a MISSING RULE and must be
       authored in COMBAT_RULES.md §4 (the owning document) before §2.3.1 item 5
       and §5.1.1 item 7 can resolve their circular delegation.

  B-4. Whether unlike Shield magnitudes (20% vs 15%) sum, refresh, or are
       rejected, and whether Tidal Barrier's undefined Shield amount
       (CARD_RULES.md §4.1) is content that needs authoring.

  If the ruling is B-1 or any variant that changes what "Multiple Shields stack
  additively" means in play, §6 of this task requires the Shield half to be
  RE-TYPED as GAMEPLAY-CHANGE (TASK_TYPES.md §2,
  development/gameplay-change.md §3) rather than edited under a DOCUMENTATION
  task. A DOCUMENTATION task may apply a decision the evidence already
  determines; it may not author a gameplay decision on its own authority.
```

**Waiting for:**

```text
A human gameplay/product-owner ruling selecting B-1 or B-2 (or a third
semantics), plus decisions on B-3 and B-4. AGENTS.md §4/§7 and this task's §9
items 2, 3, 4 and 7 are all live. Neither GAME_STATE.md nor COMBAT_RULES.md was
rewritten to make TASK-102 implementable, and no gameplay rule was changed to
make the technical model easier.
```

---

## Files changed before STOP

```text
None. Zero files under docs/ were modified. No source, test, migration, Redis,
API, SignalR, or client file was touched. This task file is the only file
changed, and only in this Completion Evidence section.

Baseline hashes re-verified after the analysis (byte-identical to pickup):

  8B9832AE25D779B3AF9EAC108BEA333066383BAE20BE6DADE8081A2B007D9E25  docs/02-technical/SIGNALR_PROTOCOL.md
  7004B262B7B4CFDDA7FE479038D9C281299040FF983CDFF828AABE6C6481609B  docs/02-technical/GAME_STATE.md
  6484133C4123901276FA50AB2E4B7FFC40A20F5A7098AB128D75EA98CE5D944C  docs/01-game-design/COMBAT_RULES.md
  299A318EBB8D638B746371363DD529A28969F9C9163039D4E1AD2A74611198B5  docs/02-technical/GAME_EVENTS.md
  24D14B21EDBDC6105C5BB61001253571CF91D47FBED64931184981BEEE45A773  tasks/backlog/TASK-102-implement-card-cast-server-path.md
```

---

## Blocker A — Resolution (APPLIED)

**Applied to `docs/02-technical/SIGNALR_PROTOCOL.md` §3.2 by TASK-104's
Product Owner rulings.** This half changed no gameplay rule, so it was applied
under this task's DOCUMENTATION type.

```text
Discriminator values:   CardCast = "CardCast"; PetSkillCast = "PetSkillCast".
                        Both added to the §3.2.2 table, which now lists 16 names
                        (ruling A-1A). RelicTriggered and PowerChanged were added
                        with them, closing the same gap.
Payload members:        CardCast:      type, cardId            (both always)
                        PetSkillCast:  type, cardId            (both always)
                        RelicTriggered: type, relicId          (both always)
                        PowerChanged:   type, delta, power, source (all always)
                        §3.2.20, §3.2.21, §3.2.23, §3.2.24.
Casing:                 All camelCase, fixed by each new subsection's table as
                        §3.2.3 item 1 requires. `type` per §3.2.2 item 1.
Enum representation:    N/A for CardCast/PetSkillCast (no enum-valued member).
                        `PowerChanged.source` uses the documented lowercase
                        string set "match"/"card"/"relic" per §3.2.4 (A-2C/A-1A
                        context), never an ordinal.
Relationship & order:   CardCast precedes PetSkillCast (ruling A-5A), owned by
                        the new §3.2.22. A Basic Card cast emits CardCast alone.
                        Both travel in one batch under one serverSequence.
Canonical owner edited: docs/02-technical/SIGNALR_PROTOCOL.md §3.2 —
                        §3.2.2 (table + closed-set restatement), §3.2.4 item 3
                        and §3.2.12 item 1 (stale counts corrected), and NEW
                        §3.2.20–§3.2.25.
Deferred members:       `effect summary` — OMITTED, not deferred. The new
                        §3.2.25 owns the single convention (ruling A-3),
                        superseding §3.2.17 item 3's deferral wording and
                        reconciling §3.2.18. No member was added.
No cost member:         CardCast carries NO Power cost member (ruling A-2C,
                        §3.2.20 item 2). Nothing was invented in its place.
Signature Skill:        PetSkillCast's `cardId` IS the confirmation (ruling
                        A-4B, §3.2.21 item 2). No new `skillId` or flag member.
```

---

## Blocker B — Resolution (STILL OPEN — RE-TYPING REQUIRED)

> ### ⚠ SUPERSEDED BY TASK-105 — THIS SECTION IS HISTORICAL RECORD
>
> **Added by TASK-106. The two records below are preserved unaltered on
> purpose — do not edit, delete, or "update" them.**
>
> This section was **accurate when written**: on that date the Shield rule had
> not been reconciled and the re-typing was genuinely required. What it
> describes has since been **delivered by a different task**:
>
> ```text
> Superseded by:      TASK-105 — "Change Shield Application Semantics from
>                     Additive Stacking to Refresh"
>                     (GAMEPLAY-CHANGE; tasks/completed/, DONE)
> What it delivered:  The GAMEPLAY-CHANGE re-typing this section called for.
>                     It changed `COMBAT_RULES.md` §4 from additive stacking to
>                     refresh and authored the depletion rule — the canonical
>                     owner, as this section said it should be.
> Contract as it now stands:
>                     `COMBAT_RULES.md` v1.6 §4 (SHA256 258B12A3…) — refresh
>                     with the pool set to the new application's magnitude; at
>                     most one Shield per entity, identity "Shield"; no additive
>                     accumulation; removal at exactly 0 in the same resolution;
>                     overflow to HP by exactly the remainder; the
>                     "unless a Relic specifies otherwise" carve-out retained
>                     with its scope stated.
> `ShieldDepleted`:   Determined by TASK-105 to be the trigger-based expiry
>                     condition, NOT a Battle Event — no event name, payload, or
>                     wire member was authored. `GAME_EVENTS.md` needed no change.
> `GAME_STATE.md`:    Verified to require NO change — §2.3.1 item 6, §2.3.3, and
>                     §5.1.1 item 1 already expressed refresh-in-place. The
>                     conflict resolved entirely on the gameplay side, exactly as
>                     this section anticipated.
> ```
>
> **What this means for a reader today:** the "STILL OPEN", "NOT APPLIED", and
> "RE-TYPING REQUIRED" wording below is **no longer true of the repository**. It
> is retained as the historical STOP record `TASK_LIFECYCLE.md` §3 and the
> TASK-093 §12 precedent exist to preserve. **Do not re-reconcile the Shield
> rule and do not commission the re-typing task** — TASK-105 already did both.
>
> **What this does NOT mean:** TASK-106 asserted no lifecycle transition on this
> file's behalf. Blocker B was closed by another task, so whether that completes
> TASK-103 is a human/orchestrator decision, not one this note takes.

**NOT applied.** The B-1B ruling changes documented gameplay semantics, so per
`TASK_TYPES.md` §2 and this task's §9 item 7 the Shield half must be re-typed
`GAMEPLAY-CHANGE` before any `COMBAT_RULES.md` §4 edit. This DOCUMENTATION task
stops here and does not edit the game rule.

```text
Conflict resolved:        NO — NOT APPLIED. COMBAT_RULES.md §4 item 3 and
                          GAME_STATE.md §2.3.1 item 6 remain unreconciled in the
                          repository, and both documents are byte-identical to
                          pickup. The RULING exists (TASK-104 B-1B) but its
                          APPLICATION requires a GAMEPLAY-CHANGE task.
Ruling:                   B-1B — a second Shield application REFRESHES the
                          existing Shield. Supplied by the Product Owner via
                          TASK-104 ("Refresh Shield").
One Shield:               Magnitude is documented per source (CARD_RULES.md §2
                          "20% of its Max HP"; PASSIVE_RULES.md §8 "15% Max
                          HP"); Tidal Barrier's amount remains UNAUTHORED and is
                          deferred to a separate gameplay-content decision
                          (TASK-104 B-4 disposition).
Multiple Shields:         RESOLVED BY RULING — refresh, not additive stacking.
                          NOT yet written into COMBAT_RULES.md §4 item 3, which
                          still says "stack additively" and is therefore now
                          KNOWN-STALE pending the GAMEPLAY-CHANGE task.
Pool representation:      RESOLVED BY RULING — "Shield is StatusEffect"
                          (TASK-104 B-2). The existing 7-member instance schema
                          already expresses it: one instance, `Magnitude` as the
                          pool, `ExpiryCondition = "ShieldDepleted"`. This
                          requires NO new state member, so GAME_STATE.md §0
                          item 5 and TASK-102 §10 are satisfied and §2.3.1
                          item 6 needs NO change. `Magnitude` on refresh is set
                          to the new value (a "set", per §5.1.1 item 1 / DR3).
Consumption order:        NOT APPLICABLE under B-1B — at most one Shield is
                          active, so there is no multi-pool "first-in" order to
                          fix. §5.1.1 item 6's `Id`-ordinal pass order suffices.
                          §4 item 2's "consumed first-in" becomes vacuous and
                          should be restated when the GAMEPLAY-CHANGE task edits
                          §4.
Depletion:                RESOLVED BY RULING — "Remove at 0, emit
                          ShieldDepleted, overflow damage continues" (TASK-104
                          B-3). NOT yet written into COMBAT_RULES.md §4 / §5.1,
                          which still do not define it. The resolution step at
                          which a Shield depletes, and whether ShieldDepleted is
                          an EVENT (it is not in GAME_EVENTS.md §2's list) or an
                          internal trigger, remain to be authored by the
                          GAMEPLAY-CHANGE task.
Refresh/expiry:           Apply-as-set/refresh is already well defined
                          (§5.1.1 item 1, COMBAT_RULES.md §5.3 DR3), and B-1B
                          lands Shield squarely on it. Shield's trigger-based
                          removal (§5.1.1 item 7) now has a ruling but still no
                          authored rule in COMBAT_RULES.md §4.
Canonical owner:          NOT EDITED — COMBAT_RULES.md and GAME_STATE.md are
                          byte-identical to pickup.
```

```text
Conflict resolved:        NO — STOP. COMBAT_RULES.md §4 item 3 vs
                          GAME_STATE.md §2.3.1 item 6 remain unreconciled and
                          both documents are byte-identical to pickup.
Ruling:                   NONE — requires the B-1/B-2 human gameplay ruling.
One Shield:               Magnitude is documented per source (CARD_RULES.md §2
                          "20% of its Max HP"; PASSIVE_RULES.md §8 "15% Max
                          HP"); Tidal Barrier's amount is NOT defined
                          (CARD_RULES.md §4.1). Lifetime is label-only —
                          §2.3.1 item 5 delegates depletion to a rule that does
                          not exist.
Multiple Shields:         UNRESOLVED — the exact conflict under adjudication.
Pool representation:      NONE AVAILABLE. The instance schema has exactly 7
                          members, with no grouping key, creation ordinal, or
                          sequence, and Shield is denied `RemainingTurns`
                          (§2.3.1 item 3). Any new pool member is forbidden by
                          §0 item 5 and TASK-102 §10.
Consumption order:        UNRESOLVED — and §5.1.1 item 6's `Id`-ordinal order
                          cannot supply "first-in", because `Id` is the effect
                          identity so the order collapses to one element.
Depletion:                UNRESOLVED AND MISSING — the `ShieldDepleted`
                          boundary at exactly 0 is defined nowhere, and no
                          resolution step exists at which a Shield can deplete.
Refresh/expiry:           Apply-as-set/refresh is well defined (§5.1.1 item 1,
                          DR3); Shield's trigger-based removal (§5.1.1 item 7)
                          is a deferral with no destination.
Canonical owner:          NOT EDITED — GAME_STATE.md and COMBAT_RULES.md are
                          byte-identical to pickup.
```

### Changed Files

- `docs/02-technical/SIGNALR_PROTOCOL.md` — **the wire owner.** §3.2.2
  discriminator table extended to 16 names and its closed-set statement
  restated (A-1A); §3.2.4 item 3 and §3.2.12 item 1 stale counts corrected;
  NEW §3.2.20 `CardCast`, §3.2.21 `PetSkillCast`, §3.2.22 emission order,
  §3.2.23 `RelicTriggered`, §3.2.24 `PowerChanged`, §3.2.25 the
  `effect summary` omission convention; §3.2.17 item 3 and §3.2.19 item 3
  reconciled to §3.2.25; §3.2.19 item 1 amended for the shared `source`
  name; version header advanced to 2.7.
- `docs/02-technical/GAME_EVENTS.md` — **the semantics owner.** §2's
  `CardCast`/`PetSkillCast`, `RelicTriggered`, and `PowerChanged` payload items
  authored, and `PassiveTriggered` item 3 reworded from "deferred" to "not
  populated yet" with a cross-reference to the wire owner's §3.2.25 — so the
  two owners stop disagreeing about the same four events. Version header
  advanced to 2.8. **No event, trigger, ordering rule, or gameplay semantic
  was changed.**
- `tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  — the decision register that supplied the nine answers (recorded verbatim);
  its Status advanced to `READY` and the B-half re-typing requirement recorded.
- `tasks/backlog/TASK-103-resolve-cardcast-wire-schema-and-shield-stacking-contract.md`
  — this file: status, the applied Blocker A record, and the Blocker B
  re-typing requirement.

**No other file was changed.** `COMBAT_RULES.md`, `GAME_STATE.md`, and
`CARD_RULES.md` are byte-identical to pickup: the Shield half was deliberately
NOT applied, and the wire half required no gameplay-document edit.

### Documentation

- `SIGNALR_PROTOCOL.md` §3.2 — **changed and applied.** It is the single owner
  of the `events[]` wire schema (`GAME_EVENTS.md` §3 item 1), so the wire
  contract belongs there and nowhere else.
- `GAME_EVENTS.md` §2 — **changed and applied, as the semantics owner.** Its §3
  item 1 defers wire shape to `SIGNALR_PROTOCOL.md` §3.2, and its §2 owns what
  each payload contains; once the wire shape was fixed, §2's payload prose for
  the four events had to be reconciled so the two owners did not state
  incompatible conventions for the same elements. The change preserves
  `GAME_EVENTS.md`'s ownership: it states semantics and cross-references the
  wire rule rather than restating it (`.ai/workflow/documentation/
  documentation-change.md` §2).
- `COMBAT_RULES.md` §4 / `GAME_STATE.md` §2.3.1 — **NOT changed.** The Shield
  ruling exists but its application is a `GAMEPLAY-CHANGE`, not a documentation
  reconciliation.

### Validation Results

- Documentation consistency audit (wire half) — **PASS**: `SIGNALR_PROTOCOL.md`
  §3.2 ↔ `GAME_EVENTS.md` §2/§3 ↔ `CARD_RULES.md` §3/§6 were compared after the
  change; the discriminator set, the §3.2.12 member wording, and the payload
  descriptions now agree, and every cross-reference in the new subsections
  resolves to an existing section.
- Documentation consistency audit (Shield half) — **STILL CONTRADICTORY, by
  design**: `COMBAT_RULES.md` §4 item 3 and `GAME_STATE.md` §2.3.1 item 6 remain
  unreconciled in the repository. Not silently reconciled; a `GAMEPLAY-CHANGE`
  re-typing is required.
- Scope validation (`MVP_SCOPE.md` §1/§2) — **PASS**: no item touched reaches an
  OUT item. Cards and Combat/Status Effects are IN (`MVP_SCOPE.md` §1). No
  Relic is implemented, no `GetBattleState`, and no StatusEffect wire member.
- TASK-102 implementability check — **PARTIAL**: the two event projections and
  the emission-order criterion are now implementable without guessing; the
  Shield criteria ("Shield grants … a Shield absorption pool", "Shield
  absorption works", "Multiple Shields stack additively") are **NOT** — the last
  of those is also now factually superseded by the B-1B ruling.
- `dotnet build` / `dotnet test` — **N/A and not run**: this task changes no
  code. `npm test` likewise not run. No source or test file was modified to
  satisfy any guard.

### Contract Preservation Verification

```text
Card rules             UNCHANGED (CARD_RULES.md §2–§6)
Combat damage formula  UNCHANGED (COMBAT_RULES.md §3 pipeline steps)
Shield rule            UNCHANGED and STILL UNRECONCILED (COMBAT_RULES.md §4) —
                       Blocker B remains open; the B-1B ruling re-types it
SignalR protocol       CHANGED — §3.2 discriminator set extended to 16 names and
                       §3.2.20–§3.2.25 added (A-1A/A-2C/A-3/A-4B/A-5A applied).
                       No new method; §4/§6 delivery paths unchanged
BattleState schema     UNCHANGED — no new member
PetState               UNCHANGED — Power/HP/EquippedCards/StatusEffects reused
API                    UNCHANGED
Redis                  UNCHANGED (no new key, TTL, record, or Redis-only field)
PostgreSQL             UNCHANGED (no schema, entity, column, or migration)
src/ and tests/        UNCHANGED (byte-identical)
docs/00-overview/      UNCHANGED
docs/03-decisions/     UNCHANGED (0 ADRs)
TASK-102               UNCHANGED (byte-identical, Status: BACKLOG, same location)
TASK-036/079/099, tasks/completed/   UNCHANGED
TASK-104               the decision-input task that supplied these answers
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no Relic implementation, no `GetBattleState`, no StatusEffect
      wire member (RelicTriggered/PowerChanged are SCHEMA only — nothing emits
      them)
- [x] Confirmed no invented content value (Thanh Xà / Sơn Hùng remain deferred;
      Tidal Barrier's magnitude remains unauthored)
- [ ] Confirmed TASK-102 is implementation-ready — **NOT YET.** The wire half is
      applied, but the Shield half remains open, so TASK-102's Shield acceptance
      criteria are still not implementable without guessing.

### Stale-documentation check — `BOSS_RULES.md` §6.3.1 (checked, NOT edited)

`BOSS_RULES.md` §6.3.1 was checked for stale StatusEffect assumptions, per this
task's §9. Item 3's Root cross-reference and item 1's Burn tick schedule are
consistent with the landed `GAME_STATE.md` §2.3.1/§5.1.1 contract and with
`COMBAT_RULES.md` §5.3, and `BOSS_RULES.md` §5 item 5's Stun cross-reference to
`GAME_STATE.md` §2.3.1/§5.1.1 resolves correctly. **No stale assumption was
found and the document was not modified.**

### Unrelated Stale Documentation Discovered (REPORTED, NOT CHANGED)

Per `AGENTS.md` §16. These are adjacent gaps found during this task's reading.
Items 1 and 2 were resolved by the A-1A ruling; items 3–5 remain open.

1. ~~**`SIGNALR_PROTOCOL.md` §3.2.2's "closed" set omits `RelicTriggered` and
   `PowerChanged`**~~ — **RESOLVED by A-1A.** Both are now in the discriminator
   table with member tables at §3.2.23/§3.2.24, and `GAME_EVENTS.md` §2's
   payload prose for both was reconciled. Neither is *implemented*: emission
   remains the Relic stage's and the Power stage's (`ROADMAP.md` Phase 1: "No
   Relics yet").
2. ~~**Stale counts: §3.2.12 item 1 "The seven events" and §3.2.4 item 3 "in any
   of the four events"**~~ — **RESOLVED.** §3.2.12 item 1 now enumerates all 16
   correctly, and §3.2.4 item 3's phrase (which described enum-member scope,
   not the discriminator set) is widened to "in any event".
3. **`CARD_RULES.md` §4.1 gives Tidal Barrier "Heal; Gain Shield" with no
   magnitude**, while Huyền Quy's Passive (`PASSIVE_RULES.md` §8,
   `PET_RULES.md` §8) gives "Shield 15% Max HP" and the Shield Card
   (`CARD_RULES.md` §2) gives "20% of its Max HP". **Impact:** Tidal Barrier's
   Shield amount is unauthored content. **Status:** the Product Owner's B-4
   disposition — "Separate gameplay decision" — confirms it is **not** resolved
   by TASK-103 or TASK-104. **Suggested follow-up:** a separate gameplay-content
   decision task. No value was invented.
4. **`SIGNALR_PROTOCOL.md` §3.2.17 item 3 and §3.2.18 dispose of a §2
   `effect summary` member by two different conventions** (explicit deferral vs.
   silent omission), and §3.2.19 item 3 uses a third wording ("is not a wire
   member yet"). **Impact:** no single documented convention governs a member a
   §2 payload lists but the wire cannot yet carry. **Suggested follow-up:** fold
   into decision A-3.
5. **`DamagePipeline.cs`'s own doc comment states "it applies no Shield
   (`COMBAT_RULES.md` §4 item 2 is not implemented)"** — this remains accurate at
   pickup and is not stale; recorded only so the later implementation task
   corrects it in the same change (`AGENTS.md` §17). **Not a defect today.**
6. **`SIGNALR_PROTOCOL.md` cites a non-existent "§3.3" in two places** — §3 has
   only §3.1 and §3.2; the group-broadcast rule is §3 item 3. **Pre-existing and
   cosmetic** (found by this task's verification, not introduced by it). At
   `§3.2.12` item 5 ("keeps exactly its three members (§3, §3.3)") and at
   `§4` item 3 ("Unlike `ReceiveEvents` (§3.3)"). **Suggested follow-up:** a
   one-line cross-reference correction; no task currently depends on it.
7. **`SIGNALR_PROTOCOL.md` §3.2.18 item 3 cites `BOSS_RULES.md` §4 for
   Boss→Player damage**, but `§4` there is Boss Skill *timing*; the damage-event
   content is elsewhere in that document. **Pre-existing and cosmetic** (found by
   this task's verification, not introduced by it). **Suggested follow-up:**
   re-point the citation.

### Human Decisions Required

**RESOLVED for Blocker A; STILL REQUIRED for Blocker B.** The Product Owner
supplied all nine decisions via TASK-104:

```text
A-1 … A-5   SUPPLIED and APPLIED   (see "Blocker A — Resolution (APPLIED)")
B-1, B-2,
B-3         SUPPLIED, NOT APPLIED  — re-typing as GAMEPLAY-CHANGE required
B-4         DEFERRED by the Product Owner to a separate gameplay decision
```

The remaining human action is therefore **not a new decision** but an
**authorization**: re-type the Shield half as `GAMEPLAY-CHANGE` so
`COMBAT_RULES.md` §4 item 3 can be changed from additive stacking to refresh.

No follow-up task was created, and TASK-102 was not modified, moved, re-statused,
or started.

---

## Decision Input Required

> **SUPERSEDED — the decisions were supplied.** All nine answers were delivered
> by the Product Owner via TASK-104 and are recorded verbatim there (its §4 and
> §5A). The wire half has been applied; the Shield half awaits a
> `GAMEPLAY-CHANGE` re-typing. The register below is retained as the historical
> record of what was asked and what the verification found.
>
> ~~**AWAITING PRODUCT-OWNER INPUT — execution attempted, no decisions supplied.**~~
>
> This section is the second execution of TASK-103. Its purpose was to *apply*
> the nine decisions identified by the first execution's STOP report. It was
> attempted and stopped here: **none of the nine decision slots below carries a
> Product Owner answer**, and no answer exists anywhere else in the repository.
>
> Per the `tasks/completed/TASK-061-record-product-owner-pet-xp-decisions.md`
> §7 first condition, this is this task's **normal starting state, not a
> failure** — no stop condition has fired. The task remains **`BACKLOG`** in
> `tasks/backlog/`.

### Verification performed (search, not assumption)

A repository-wide sweep was executed for an existing authoritative decision on
each of the nine items — across `docs/` (all 16 ADRs and
`docs/03-decisions/README.md`, all `docs/01-game-design/`, `docs/02-technical/`,
`docs/00-overview/`), `tasks/` (completed, backlog, blocked, active), and `.ai/`
— using the decision markers `Decision Record`, `Decision:`, `Product Owner`,
`Owner's selection`, `human decision`, `Decision C`, `Ruling`, `Option A/B/C`,
`re-typed`, `GAMEPLAY-CHANGE`, `Superseded`, `decided by`.

```text
Result: 0 of 9 items have an explicit authoritative decision.

docs/03-decisions/ADR/          16 ADRs, ZERO coverage of any of the nine items.
                                ADR-004 lists CardCast/PetSkillCast only as hub
                                METHOD names; no ADR addresses the wire schema.
docs/03-decisions/README.md §8  "Known Open Items (Not ADRs)" lists only the
                                backend runtime assumption and the PRNG
                                algorithm — none of these nine are tracked there.
docs/                           No document rules on any of the nine items.
                                Repo-wide grep of docs/ for the labels A-1…B-4
                                or "TASK-103" returns ZERO relevant matches.
tasks/backlog/                  Exactly 3 files (TASK-099, TASK-102, TASK-103).
                                No TASK-104+ exists; no decision artifact was
                                created to answer the prior STOP.
tasks/active/                   EMPTY (.gitkeep only).
tasks/blocked/                  TASK-036 and TASK-079 only — neither concerns
                                these items.
git                             No commit resolves any of the nine items.

Genuine "Decision Record" sections that DO exist are all on unrelated topics:
  TASK-094  DR1–DR6  Status Effect DURATION CONSUMPTION timing — does not
                     address Shield stacking, ShieldDepleted, or the wire schema.
  TASK-047  P1       Boss technical identities.
  TASK-050  Decision C  Outcome vocabulary victory/defeat.
  TASK-056  Decision C  BattleResult persistence gap classification.
  TASK-099           Loadout selection surface (D1 = Option A) — unrelated.
  TASK-061           Product Owner Pet XP decisions — records that all twelve
                     were NOT SUPPLIED; unrelated topic.

Explicitly NOT treated as decisions (per the search's own instruction):
  TASK-102's Shield/CardCast acceptance criteria — UNEXECUTED, and a BACKLOG
    task ranks below every document (`AGENTS.md` §2). TASK-102's own stop
    conditions state a protocol contract decision "is not this task's to make".
  TASK-103's own A-1…A-5 / B-1…B-4 — these are REQUIRED decisions, not answers;
    its own text reads "Ruling: NONE" and "A human/product-owner decision on
    A-1 through A-5."
  Code comments (e.g. `DamagePipeline.cs`'s "applies no Shield") — implementation
    notes, not decisions.
  `MATCH3_RULES.md` §6.3.1's "Explicit Ruling" — a real ruling, but about
    Match-3 Special Gem activation, unrelated.
```

**No documentation was modified by this execution.** No authoritative document,
ADR, source file, test, migration, Redis key, API endpoint, or SignalR artifact
was changed. This task file is the only file touched, and only in this section
and the `Status:` field.

### The nine decisions owed

The questions, their current evidence, their options, and the authority that
owns each are recorded **in full** in the two STOP CONDITION blocks above. This
is the required register; the Product Owner answers it in place.

```text
Decision A-1  Event-set rule for CardCast / PetSkillCast, and the same gap for
              RelicTriggered / PowerChanged. Must rule whether §3.2.2's "the
              set is closed" means closed-to-undocumented-events or permanently
              closed, and thereby authorise (or refuse) the correction of the
              stale "seven events" (§3.2.12 item 1) and "four events"
              (§3.2.4 item 3) counts.
              Authority: SIGNALR_PROTOCOL.md §3.2.2, §3.2.12 (§3.2 owner).

Decision A-2  What CardCast reports about Power cost: definition cost, actual
              cost paid, or no cost member at all — and the member's name, type,
              and required/optional status. Must also rule whether "no MVP cost
              modifier exists" is an explicit CONTRACT or merely an absence of
              documentation (not to be inferred from a grep).
              Authority: SIGNALR_PROTOCOL.md §3.2 (wire); CARD_RULES.md §2/§3
              (the cost rule).

Decision A-3  Which convention governs an `effect summary` the wire cannot yet
              carry: §3.2.17 item 3's explicit DEFERRAL or §3.2.18's silent
              OMISSION. The two authoritative conventions contradict each other
              and must be reconciled to one — no new convention may be invented.
              Authority: SIGNALR_PROTOCOL.md §3.2.17 / §3.2.18 / §3.2.19.

Decision A-4  How PetSkillCast establishes that the cast Card was the active
              Pet's Signature Skill: the event's existence, the CardId identity,
              or a separate member. No new member (e.g. a `skillId`) may be
              invented.
              Authority: SIGNALR_PROTOCOL.md §3.2 (wire); CARD_RULES.md §1 item 4
              and §4.1, PET_RULES.md §8 (the fact).

Decision A-5  The deterministic emission order of CardCast vs PetSkillCast.
              CARD_RULES.md §3 item 4 groups both into ONE unordered step and §6
              gives "in addition to" with no sequence; GAME_RULES.md §17 step 14
              is a single step with no 18a/19a-style expansion, so no existing
              visual ordering is authoritative.
              Authority: CARD_RULES.md §3 item 4 / §6 (gameplay emission);
              SIGNALR_PROTOCOL.md §3.2 (wire order statement).

Decision B-1  Multiple Shield applications: B-1A stack additively, B-1B refresh
              the existing Shield, B-1C reject/ignore the second application, or
              B-1D another explicitly defined rule. This is the unadjudicated
              conflict between COMBAT_RULES.md §4 item 3 and GAME_STATE.md
              §2.3.1 item 6.
              Authority: COMBAT_RULES.md §4 (gameplay owner).
              NOTE: if B-1A — or any ruling that changes the current documented
              semantics — is selected, this half MUST be re-typed as
              GAMEPLAY-CHANGE and not edited under this DOCUMENTATION task.

Decision B-2  The Shield state representation consistent with B-1. If B-1
              selects additive stacking, the decided semantics must be
              representable WITHOUT PetState.Shield, PetState.ShieldPoints,
              BattleState.Shield, or any parallel representation
              (GAME_STATE.md §0 item 5, TASK-102 §10), must be readable from
              committed state, must survive the REDIS_STATE.md §7 item 9 round
              trip, and must define a deterministic consumption order a test can
              assert. The current 7-member instance schema has no grouping key,
              no creation ordinal, and no sequence, and Shield is denied
              RemainingTurns — so if additive stacking is selected and cannot be
              represented within the existing model, that is a STOP and no new
              state field may be invented here.
              Authority: GAME_STATE.md §2.3.1 / §2.3.3 / §5.1.1.

Decision B-3  The ShieldDepleted boundary: what happens when Shield reaches
              exactly 0, the damage == pool case, the HP interaction, the
              StatusEffect removal timing (same-resolution vs next-resolution),
              and the ShieldDepleted trigger itself. This is a MISSING RULE —
              GAME_STATE.md §2.3.1 item 5 and §5.1.1 item 7 both delegate the
              behaviour to COMBAT_RULES.md §4/§5.1, and those sections never
              define it (a circular delegation). The label must not be left with
              no behaviour.
              Authority: COMBAT_RULES.md §4 (gameplay owner).

Decision B-4  Shield magnitudes across the three MVP sources. The Shield Card
              (CARD_RULES.md §2) = 20% Max HP; Huyền Quy's Passive
              (PASSIVE_RULES.md §8, PET_RULES.md §8) = 15% Max HP; Tidal Barrier
              (CARD_RULES.md §4.1) = NO NUMBER AUTHORED. Whether unlike
              magnitudes sum, refresh, or are rejected follows from B-1. The
              missing Tidal Barrier magnitude is a gameplay/content decision.
              Authority: CARD_RULES.md §2 / §4.1, PASSIVE_RULES.md §8,
              PET_RULES.md §8.
```

### Prohibited actions observed

Per `AGENTS.md` §7 / §20 and `GAME_RULES.md` §20, no decision slot was filled in,
partially answered, defaulted, ranked, or inferred. No gameplay rule, protocol
member, or content value was invented. Neither `COMBAT_RULES.md` nor
`GAME_STATE.md` was rewritten — not to make TASK-102 implementable, and not to
make the technical model easier.

### Lifecycle note (`tasks/README.md` §10, `TASK_LIFECYCLE.md` §2–§4)

The lifecycle transition `BACKLOG → BLOCKED` **does not exist**.
`TASK_LIFECYCLE.md` §2's "Allowed transitions" list contains exactly six edges
and only `IN PROGRESS → BLOCKED` reaches BLOCKED; §2's "Invalid transitions"
list routes a blocker found before pickup back to BACKLOG, and §4's file-movement
table maps `IN PROGRESS → BLOCKED` to `active/ → blocked/` only. This task was
never picked up through `BACKLOG → READY → IN PROGRESS`, so **no move to
`tasks/blocked/` is authorized and the file remains in `tasks/backlog/`**. The
earlier `Status: BLOCKED` value written by this task's first execution was
therefore not a legal lifecycle state for a file in `backlog/`; it is corrected
to `BACKLOG` by this execution, consistent with the `TASK-061` precedent
("An unanswered §2 is this task's normal starting state … The task remains
`BACKLOG` in `backlog/`"). TASK-102 was not moved.

### What is needed to proceed

The Product Owner fills in the nine decision slots in the register above (or
supplies them in a new decision-input artifact). An agent must not fill them in.
Once supplied, the resolution proceeds per §17 of this task's execution brief:
apply the smallest set of canonical-owner documentation edits, then re-check
TASK-102's implementability. If the B-1 ruling changes the Shield rule, the
Shield half is re-typed as `GAMEPLAY-CHANGE` and is **not** applied under this
task's DOCUMENTATION type.
