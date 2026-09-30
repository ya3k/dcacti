# TASK-105 — Change Shield Application Semantics from Additive Stacking to Refresh

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK CHANGES ONE GAMEPLAY RULE AND AUTHORIZES NOTHING ELSE. It is the
  GAMEPLAY-CHANGE re-typing that TASK-103 §6 and §9 item 7 required, and that
  TASK-104's Product Owner decisions (B-1/B-2/B-3) supplied the content for.

  THE RULES ARE ALREADY DECIDED. This task does not choose, invent, or
  re-open any gameplay semantic. It applies three already-approved Product
  Owner decisions to their canonical owner documents. Where a decision leaves a
  documentation detail unstated, that detail is resolved by the smallest
  faithful expression of the decision — never by authoring a new rule.

  BOUNDARY: documentation only. Zero files under src/ or tests/. No SignalR,
  Redis, API, or client change.
-->

---

## 0. Metadata

```text
Task ID:           TASK-105
Type:              GAMEPLAY-CHANGE (TASK_TYPES.md §2 — "Change a game rule in
                   docs/01-game-design/ and propagate the change through
                   implementation and tests"; workflow
                   development/gameplay-change.md). The rule being changed is
                   COMBAT_RULES.md §4's Shield stacking. See "Type
                   classification note".
Status:            DONE (B-1/B-2/B-3 APPLIED to `COMBAT_RULES.md` §4 —
                   the canonical owner. `GAME_STATE.md` verified to require NO
                   change. `GAME_EVENTS.md` verified to require NO change; the
                   `ShieldDepleted` determination is recorded as an internal
                   resolution trigger, NOT a Battle Event — no event name,
                   payload, or wire member authored. `src/`, `tests/`,
                   `SIGNALR_PROTOCOL.md`, `REDIS_STATE.md`, `DATABASE.md`,
                   `docs/00-overview/`, `docs/03-decisions/`, and
                   TASK-102/103/104 are byte-identical. Independent review
                   performed — see "Review Verdict".
                   `TASK_LIFECYCLE.md` §2: IN REVIEW → DONE, file moved
                   `active/` → `completed/`.)
Risk:              HIGH (TASK_TYPES.md §4 — GAMEPLAY-CHANGE baseline is always
                   HIGH. This changes a combat absorption rule that the Damage
                   Pipeline, the Shield Basic Card, Huyền Quy's Passive, and
                   Tidal Barrier all consume, and it corrects a live
                   contradiction between COMBAT_RULES.md and GAME_STATE.md.)
Priority:          HIGH (TASK-103's Blocker B — the last open half of the
                   CardCast/Shield contract work, and a prerequisite for
                   TASK-102's Shield acceptance criteria. ROADMAP.md Phase 1
                   names the Shield Basic Card in its "3 Basic Cards" slice.)
Primary Agent:     gameplay (TASK_TYPES.md §5 Domain x Type matrix — Combat ->
                   Gameplay. COMBAT_RULES.md §4 is the canonical owner of the
                   Heal and Shield rules.)
Supporting Agents: review (documentation consistency; confirm the two documents
                   agree afterwards and neither restates the other),
                   realtime (CONSULTED ONLY — to confirm whether the
                   ShieldDepleted trigger requires a GAME_EVENTS.md entry and
                   therefore any protocol consequence; this task changes no
                   protocol and no wire member)
Workflow:          development/gameplay-change.md
Skills:            gameplay/gameplay-behavior-derivation,
                   discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (5 skills — Complex budget, tasks/README.md §12)
Dependencies:      TASK-104 (READY — SUPPLIED the B-1/B-2/B-3 Product Owner
                     decisions this task applies. Read-only; NOT modified),
                   TASK-103 (PARTIALLY RESOLVED — recorded Blocker B and
                     required this re-typing. Read-only; NOT modified),
                   TASK-093 (DONE — authored the StatusEffect instance schema
                     GAME_STATE.md §2.3.1–§2.3.3 + §5.1.1 that B-2 retains;
                     immutable),
                   TASK-094 (DONE — authored COMBAT_RULES.md §5.3 DR1–DR6; the
                     apply/refresh mechanism B-1 now lands Shield on;
                     immutable),
                   TASK-095/TASK-096 (DONE — StatusEffect domain state,
                     step-19a lifecycle, serialization round trip; immutable),
                   TASK-085 (DONE — the card-shield / card-tidal-barrier
                     definition rows; immutable)
Blocks:            TASK-102 (its Shield absorption, multi-Shield, and
                     pool-depletion acceptance criteria depend on this
                     contract); and, through it, ROADMAP.md Phase 1 completion
Estimate:          Medium (one gameplay rule change, one state-contract
                   reconciliation, one event-vs-trigger determination, across
                   three candidate owner documents; no code)
```

**Type classification note.** `GAMEPLAY-CHANGE`, not `DOCUMENTATION`.
`TASK_TYPES.md` §2 reserves `GAMEPLAY-CHANGE` for a mechanic that "needs to
behave differently than the documentation currently says". That is exactly this
task: `COMBAT_RULES.md` §4 item 3 currently says multiple Shields **stack
additively**, and the approved decision is that they **refresh**. TASK-103 could
not make this edit under its own DOCUMENTATION type — §6 and §9 item 7 of that
task require exactly this re-typing — and TASK-104's §5A flagged it as required.

**This task chooses no new gameplay rule.** All three decisions it applies were
made by the Product Owner and are recorded verbatim in TASK-104 §4/§5A. This
task's job is to express them in their canonical owner documents, deterministically
and without contradiction.

**No ADR is required.** The change alters no layering, storage, transport, or
authoritative model. `docs/03-decisions/README.md` §8's "Known Open Items
(Not ADRs)" does not list Shield, and no ADR (ADR-001…ADR-016) addresses Shield
stacking or Status Effect stacking. `architecture/adr-change.md` §2's test —
architecturally important, difficult to reverse, cross-cutting — is not met by a
single-effect stacking rule that reuses the existing `StatusEffect` model.

**GAME_STATE.md is expected to require NO change, and that is not an omission.**
`GAME_STATE.md` §2.3.1 item 6, §2.3.3, and §5.1.1 item 1 already mandate
refresh-in-place and forbid independent-instance stacking. The approved decision
(B-1 = refresh, B-2 = keep `StatusEffect`) makes the **current** state contract
**correct** and the **current** gameplay rule **wrong**. The conflict therefore
resolves entirely on the gameplay side. This task must verify that conclusion
rather than assume it — see §3 step 5 — and must not edit `GAME_STATE.md` merely
to appear symmetrical.

---

## 1. Objective

Change the Shield rule in its canonical owner so that a Shield applied while a
Shield is already active on the same entity **refreshes that Shield** instead of
stacking additively into a larger pool, and define deterministically what happens
when a Shield's absorption pool reaches exactly zero — so that `COMBAT_RULES.md`
§4 and `GAME_STATE.md` §2.3.1/§5.1.1 state one consistent contract, and TASK-102
becomes implementable without guessing.

---

## 2. Authoritative References

### The decision input (read-only; supplies the answer, not the wording)

- `tasks/backlog/TASK-104-collect-cardcast-wire-and-shield-contract-decisions.md`
  **§4 (B-1, B-2, B-3) and §5A** — the Product Owner's answers, recorded
  verbatim: **B-1** "Refresh Shield"; **B-2** "Shield is StatusEffect";
  **B-3** "Remove at 0, emit ShieldDepleted, overflow damage continues". §5A's
  "Required Downstream Actions" item 2 is this task's mandate, and §5A's
  "GAMEPLAY-CHANGE re-typing flag" is why this task exists.
- `tasks/backlog/TASK-103-resolve-cardcast-wire-schema-and-shield-stacking-contract.md`
  **§3 Blocker B, its STOP CONDITION, and "Blocker B — Resolution (STILL OPEN)"**
  — the conflict analysis this task resolves. **Do not modify.**

### The gameplay rule to change (the canonical owner)

- `docs/01-game-design/COMBAT_RULES.md` **§4 (Healing and Shields), items 1–4** —
  item 2 the absorption pool "consumed first-in"; **item 3 the sentence this
  task changes** ("Multiple Shields stack additively into a single absorption
  pool unless a Relic specifies otherwise"); item 4 Heal/Shield are not subject
  to the Damage Pipeline.
- `docs/01-game-design/COMBAT_RULES.md` **§5.1** — the MVP Status Effect list,
  including `Shield` ("absorption pool, see §4").
- `docs/01-game-design/COMBAT_RULES.md` **§5.2 item 1** — every Status Effect has
  a source, a magnitude, and a duration **or** a trigger-based expiry (e.g.
  "until Shield is depleted"); **§5.2 item 2** — the stacking default ("refresh
  duration, do not stack magnitude **unless a Card/Relic explicitly says
  otherwise**") that this change formalizes for Shield.
- `docs/01-game-design/COMBAT_RULES.md` **§5.3 / §5.3.2** — DR1–DR6, the
  apply/refresh mechanism and the explicit exclusion of trigger-based effects
  from the Turn countdown. **Not changed by this task**; Shield is trigger-based.
- `docs/01-game-design/GAME_RULES.md` **§14** (Combat Rules — COMBAT_RULES.md's
  parent), **§16** (the canonical event list), **§17** step 18/19 and step 19a,
  **§18** (server authority), **§20** (Rule Change Policy).

### The state contract to reconcile against (expected UNCHANGED)

- `docs/02-technical/GAME_STATE.md` **§0 item 5** — "No stage introduces a
  parallel representation of a concept another stage already owns."
- `docs/02-technical/GAME_STATE.md` **§2.3.1** — the seven-member instance
  schema; **item 1** (`Id` is an identity, e.g. `"Shield"`, not a per-instance
  key), **item 2** (`Magnitude` is typed but not interpreted here), **item 3**
  (Shield uses `ExpiryCondition`, never `RemainingTurns`), **item 5**
  (`ExpiryCondition` "is a condition label, not a rule… The condition's behavior
  is owned by `COMBAT_RULES.md` §4 and §5.1 and is not restated here" — **the
  delegation this task must now satisfy**), **item 6** (never more than one
  instance per effect identity; re-application refreshes), items 7–11.
- `docs/02-technical/GAME_STATE.md` **§2.3.2** (serialization and round trip),
  **§2.3.3** ("No stacking model other than §5.2 item 2's refresh-in-place").
- `docs/02-technical/GAME_STATE.md` **§5.1** (the single post-resolution
  write-back), **§5.1.1** — **item 1** (apply is a "set", not an increment),
  **item 4** (expiry is a removal, not a stored zero — written for
  `RemainingTurns`-model instances), **item 5**, **item 7** (trigger-based
  instances "are not decremented… removed by its own documented trigger"), and
  **item 10** ("Nothing here is published. This lifecycle adds no event…").
- `docs/02-technical/REDIS_STATE.md` **§2 item 1** and **§7 item 9** — the
  round-trip obligation and the no-Redis-only-field rule.

### The events question (clarification only — no protocol change)

- `docs/02-technical/GAME_EVENTS.md` **§2** — the event reference. **Note:
  `ShieldDepleted` does not appear anywhere in this document.**
- `docs/02-technical/GAME_EVENTS.md` **§1** (the ordered event list), **§1.1**
  (the ordering contract), **§3 item 1** (the wire shape is owned by
  `SIGNALR_PROTOCOL.md` §3.2), **§3 item 5** ("every event defined in §2 belongs
  to the ordered list of §1 — none is defined as presentation-only"), **§3 item 7**
  (a sequencing position is not a scope reduction).
- `docs/01-game-design/GAME_RULES.md` **§16** — the canonical event name list.
  **`ShieldDepleted` is not in it.**
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.2** and **§3.2.25** —
  **CONTEXT ONLY. This task must not edit this document.** Cited solely so the
  task can state accurately whether a new event would have a wire consequence.

### Scope, content, and consumer references

- `docs/01-game-design/CARD_RULES.md` **§2** (the Shield Basic Card's Cost and
  Effect; target = the active Pet / `PetState`), **§3** (casting and validation,
  including item 4's ordered resolution and item 5's no-Turn/no-Combo rule),
  **§4.1** (`Tidal Barrier` — "Heal; Gain Shield", **no magnitude authored**).
- `docs/01-game-design/PASSIVE_RULES.md` **§8** and `docs/01-game-design/PET_RULES.md`
  **§8** — Huyền Quy's Passive ("Gain Shield = 15% Max HP"), a second and
  independent Shield source.
- `docs/01-game-design/BOSS_RULES.md` **§6.3.1** — the Boss Skill secondary
  effects; the sibling magnitude/duration precedent (Burn, Root).
- `docs/01-game-design/RELIC_RULES.md` **§3** — `OnCardCast` and the Relic
  trigger list. **COMBAT_RULES.md §4 item 3's "unless a Relic specifies
  otherwise" carve-out is retained; no MVP Relic uses it** (`ROADMAP.md` Phase 1:
  "No Relics yet").
- `docs/00-overview/MVP_SCOPE.md` **§1** (Cards — including the Shield Basic
  Card — and Combat/Status Effects are IN), **§2** (OUT), **§3** (FUTURE),
  **§4** (classification authority).
- `docs/00-overview/ROADMAP.md` — Phase 1 names "3 Basic Cards" and one Pet's
  Signature Skill; it is direction, not authorization (`ROADMAP.md` §2).
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority (context only; not modified).

**ADR check:** no ADR addresses Shield or Status Effect stacking
(`docs/03-decisions/README.md` §8 lists only the backend runtime assumption and
the PRNG algorithm). No ADR is created by this task.

---

## 3. Scope

### In Scope

1. **Change `COMBAT_RULES.md` §4 item 3** from additive stacking to refresh
   semantics, expressing the approved decision (B-1) in the Shield rule's
   canonical owner. The sentence to be replaced currently asserts a behavior the
   approved decision reverses.
2. **State Shield refresh deterministically** in `COMBAT_RULES.md` §4:
   - a Shield applied while a Shield of the **same effect identity** is already
     active on that entity **replaces that Shield's value** — it does not add to
     it, and it does not create a second Shield;
   - **no additive accumulation** occurs under any MVP condition;
   - whether the refreshed value is set to the new application's magnitude
     (rather than summed, maximized, or compared) is stated explicitly, so a
     test can assert it;
   - the retained carve-out ("unless a Relic specifies otherwise") is preserved
     and its scope stated (no MVP Relic exercises it).
3. **Define Shield depletion** in `COMBAT_RULES.md` §4, satisfying the
   delegation `GAME_STATE.md` §2.3.1 item 5 already makes to this section:
   - damage is absorbed by the pool before HP is affected;
   - the pool reaching **exactly 0** removes the Shield;
   - **overflow continues** to HP — damage greater than the pool reduces HP by
     exactly the remainder, with no double-counting;
   - the removal occurs **in the same resolution** that depleted it;
   - when `ShieldDepleted` is evaluated/emitted, and whether it is a Battle
     Event or an internal trigger (see item 6).
4. **Reconcile `COMBAT_RULES.md` §4 item 2's "consumed first-in" wording.** Under
   refresh, at most one Shield exists per entity, so "first-in" has nothing to
   order. Restate it as consumption **before HP** without implying a multi-pool
   ordering, or state explicitly that a single pool makes the ordering vacuous —
   whichever expresses the decision without inventing new semantics.
5. **Verify — and record the verification — that `GAME_STATE.md` requires no
   change.** Confirm §2.3.1 item 6, §2.3.3, and §5.1.1 item 1 already express
   B-1/B-2 exactly, and that the resolved rule introduces no
   `PetState.Shield`, no `PetState.ShieldPoints`, no `BattleState.Shield`, and no
   other parallel representation (`GAME_STATE.md` §0 item 5, TASK-102 §10). **If
   a change to `GAME_STATE.md` proves necessary, that is a STOP** — see §9.
6. **Determine whether `GAME_EVENTS.md` needs semantic clarification**, and
   record the determination with reasons. Specifically resolve the fact that
   B-3's "emit `ShieldDepleted`" names something that **does not exist** as an
   event today. Verified at TASK-105 authoring time:
   - `ShieldDepleted` occurs **exactly once in all of `docs/`** — as a schema
     *example value* in `GAME_STATE.md` §2.3.1's `ExpiryCondition` comment
     ("`ShieldDepleted`"). It is a label, not a defined event.
   - It is **not** in `GAME_RULES.md` §16's canonical event list.
   - It appears **nowhere** in `GAME_EVENTS.md` — not in §1's ordered list and
     not in §2's event reference (0 matches document-wide).
   - `GAME_STATE.md` §5.1.1 item 10 states the Status Effect lifecycle "adds no
     event, no payload member, and no SignalR method"; §2.3.1 item 5 calls
     `ExpiryCondition` "a condition label, not a rule".
   Determine and record whether B-3's "emit" means **(a)** a new Battle Event
   (which would require a `GAME_RULES.md` §16 name, a `GAME_EVENTS.md` §2 entry,
   a §1 ordering position, and — as a consequence for a *later* task, never this
   one — a `SIGNALR_PROTOCOL.md` §3.2 projection), or **(b)** an internal
   resolution trigger that is not a Battle Event. **Do NOT author an event
   name, a payload, or a wire member.** If the decision's own wording does not
   settle which it means, that is a STOP — see §9.

   > **Report either way.** If the determination is (a), flag that
   > `GAME_STATE.md` §5.1.1 item 10's "adds no event" clause becomes stale and
   > must be reconciled in the same change, and report the protocol consequence
   > for a later task. If it is (b), say so plainly and leave the event
   > documents untouched — a decision to *not* emit a Battle Event is a
   > determination, not an omission.
7. **Update the dependent references this change makes stale** — only where a
   reference's wording is now wrong, never to duplicate the rule
   (`documentation/documentation-change.md` §2). Candidates to check:
   `COMBAT_RULES.md` §5.1's `Shield` line and §5.2 item 2's carve-out wording;
   `CARD_RULES.md` §2's Shield Effect line and §4.1's Tidal Barrier line (if
   either asserts or implies stacking); `PASSIVE_RULES.md` §8.

### Out of Scope

- **Any implementation code** — no `DamagePipeline` absorption step, no Domain
  effect resolver, no `StatusEffectLifecycle` change, no `BattleEvent` member,
  no `BattleEventWireProjection` arm. Those are TASK-102's deliverables.
- **Any source code or test change**; `src/` and `tests/` are byte-identical.
- **Any SignalR / protocol change.** `SIGNALR_PROTOCOL.md` is **not** edited by
  this task; §3.2.2's discriminator set, §3.2.20–§3.2.25, and §3.2.2 item 5's
  projected-but-unemitted note stand as TASK-103 left them. If §3 item 6 concludes a
  new Battle Event is required, the protocol consequence is reported for a
  separate future task — not applied here.
- **Any Redis change** — no new key, field, TTL, record, or Redis-only field.
  The Shield pool remains part of the existing `BattleState` round trip.
- **Any API or PostgreSQL change**; no migration.
- **Any client / Phaser / React / UI work.**
- **Any Shield balance value.** The Shield Card's magnitude (`CARD_RULES.md` §2)
  and Huyền Quy's Passive magnitude (`PASSIVE_RULES.md` §8) are **not** changed.
- **Tidal Barrier's Shield magnitude** — unauthored (`CARD_RULES.md` §4.1) and
  **deferred by the Product Owner to a separate gameplay-content decision**
  (TASK-104 B-4 disposition). No value is invented here.
- **Any new Shield type, Status Effect type, status framework, or combat
  engine**; no new Status Effect, Card, Pet Skill, Pet, Boss, Relic, Element, or
  progression system.
- **Burn, Root, Stun, and the Turn-based duration rule.** `COMBAT_RULES.md` §5.3
  DR1–DR6 and §5.3.2's scope exclusion are unchanged; Shield is trigger-based
  and stays outside the Turn countdown (`GAME_STATE.md` §5.1.1 item 7).
- **The Damage Pipeline's formula.** `COMBAT_RULES.md` §3 steps 1–6 are
  unchanged; this task defines the absorption *rule*, not its pipeline
  implementation, and Shield amounts remain outside the pipeline (§4 item 4).
- **Changing any decision.** B-1/B-2/B-3 are settled input; they are applied,
  never re-opened, re-ranked, or softened.
- **Modifying TASK-103, TASK-104, TASK-102, TASK-036, TASK-079, TASK-099, or
  any `tasks/completed/*` file.**
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## 4. Current State

`COMBAT_RULES.md` §4 item 3 currently reads: *"Multiple Shields stack additively
into a single absorption pool unless a Relic specifies otherwise."*
`GAME_STATE.md` §2.3.1 item 6 states the opposite: there is never more than one
instance per effect identity per entity, a re-application refreshes that existing
instance "rather than appending a second one", and independent-instance stacking
"is not an MVP behavior and no second representation of it is introduced".
`GAME_STATE.md` §2.3.3 adds "No stacking model other than §5.2 item 2's
refresh-in-place", and §5.1.1 item 1 makes apply "a 'set', not an increment".

Neither document concedes to the other, and no third document adjudicates.
TASK-103 recorded this as Blocker B and stopped; TASK-104 obtained the Product
Owner's ruling (refresh, keep `StatusEffect`, remove at 0 with overflow
continuing to HP). The gameplay rule is therefore **known-stale** and this task
corrects it.

The Status Effect implementation already exists: `StatusEffect`,
`StatusEffectLifecycle`, and `StatusEffectType` are implemented (TASK-095), and
serialization round-trips (TASK-096). `StatusEffectType.Shield` already exists
and `StatusEffect.ShieldDepletedCondition` already carries the
`"ShieldDepleted"` label. `DamagePipeline`'s own documentation still states it
"applies no Shield (`COMBAT_RULES.md` §4 item 2 is not implemented)" — accurate,
and TASK-102's to correct, not this task's.

---

## 5. Acceptance Criteria

All binary. Criteria 1–8 concern the gameplay rule; 9–12 the reconciliation and
events; 13–19 scope and authority.

### The Shield rule

- [ ] `COMBAT_RULES.md` §4 item 3 no longer states that multiple Shields stack
      additively; it states that a Shield applied while a Shield of the same
      effect identity is already active **refreshes** that Shield.
- [ ] The rule states explicitly that the refreshed Shield's value is **not**
      summed with the existing value — no additive accumulation occurs.
- [ ] The rule states, deterministically, what the refreshed value becomes (a
      test asserts one outcome, not a range).
- [ ] The rule states that **no second Shield instance** is created and that at
      most one Shield is active per entity under MVP conditions.
- [ ] The "unless a Relic specifies otherwise" carve-out is preserved, and its
      scope is stated (no MVP Relic exercises it — `ROADMAP.md` Phase 1: "No
      Relics yet").
- [ ] `COMBAT_RULES.md` §4 defines Shield absorption as reducing incoming damage
      **before HP is affected**, with the pool consumed before HP.
- [ ] `COMBAT_RULES.md` §4 defines the pool reaching **exactly 0**: the Shield
      is removed, and the removal occurs **in the same resolution** that
      depleted it.
- [ ] `COMBAT_RULES.md` §4 defines **overflow**: damage greater than the pool
      reduces HP by exactly the remainder, with no double-counting; and damage
      exactly equal to the pool leaves HP unchanged.

### Reconciliation, ownership, and events

- [ ] `COMBAT_RULES.md` §4 item 2's "consumed first-in" wording is reconciled —
      either restated as consumption before HP, or explicitly recorded as
      vacuous under a single active Shield. No new ordering rule is invented.
- [ ] `GAME_STATE.md` §2.3.1 item 6, §2.3.3, and §5.1.1 item 1 are **verified**
      to already express B-1/B-2 exactly, and that verification is recorded in
      Completion Evidence. If any of them is found to require a change, the
      task STOPS per §9 item 3 instead of editing the state contract.
- [ ] The resolved rule introduces **no** `PetState.Shield`, **no**
      `PetState.ShieldPoints`, **no** `BattleState.Shield`, and no other parallel
      representation (`GAME_STATE.md` §0 item 5, TASK-102 §10).
- [ ] The **`ShieldDepleted` determination is explicit and recorded**:
      whether B-3's "emit" means a new Battle Event or an internal resolution
      trigger, with the reasoning, and — if a Battle Event — the exact set of
      downstream documents that would then require entries (`GAME_RULES.md` §16,
      `GAME_EVENTS.md` §1/§2, and, in a later task, `SIGNALR_PROTOCOL.md` §3.2).
      **No event name, payload, wire member, or ordering position is authored
      by this task.**
- [ ] If the determination is that `GAME_EVENTS.md` needs **no** semantic change,
      that conclusion is recorded with its reasons; if it needs a clarification
      that follows directly from B-3 without inventing an event, that minimal
      clarification is made and `GAME_EVENTS.md` §3 item 1 (wire shape owned
      elsewhere) is respected.

### Scope, ownership, and consistency

- [ ] No source code is changed: `src/` is byte-identical.
- [ ] No test is changed: `tests/` is byte-identical.
- [ ] `SIGNALR_PROTOCOL.md` is **byte-identical**; `REDIS_STATE.md`,
      `DATABASE.md`, and every file under `docs/00-overview/` and
      `docs/03-decisions/` are unmodified.
- [ ] No `COMBAT_RULES.md` §3 Damage Pipeline step is changed, and no Shield
      balance value, magnitude, Cost, or duration is introduced or altered.
- [ ] Tidal Barrier's Shield magnitude is **not** authored; it is recorded as
      deferred to a separate gameplay-content decision (TASK-104 B-4).
- [ ] The change stays within `MVP_SCOPE.md` §1 (Cards and Combat/Status Effects
      are IN; no OUT item is reached) — `quality/scope-validation`.
- [ ] `COMBAT_RULES.md` and `GAME_STATE.md` agree after the change; **neither
      contradicts the other and neither restates the other's rule**
      (`documentation/documentation-change.md` §2 — one concept, one owner).
- [ ] Documentation consistency audit passes across every document touched plus
      every document that references them
      (`quality/documentation-consistency`, Mode C).
- [ ] TASK-102, TASK-103, TASK-104, TASK-036, TASK-079, and TASK-099 are
      byte-identical; no `tasks/completed/*` file is modified.
- [ ] Quality review checklist passes (`quality/review.md` §1), skipping only the
      code-only items per `documentation/documentation-change.md` §4.
- [ ] No authoritative rule or contract is violated (`AGENTS.md` §10 / ADR-001);
      the change is server-authoritative and introduces no client-side rule.

---

## 6. Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)   — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[x] docs/01-game-design/COMBAT_RULES.md   (THE canonical owner of the Shield
      rule: §4 items 2–3 restated, and the depletion rule authored in §4;
      §5.1/§5.2 wording reconciled only if now stale)
[?] docs/02-technical/GAME_STATE.md        (expected NONE — verify §2.3.1
      item 6 / §2.3.3 / §5.1.1 item 1 already match B-1/B-2. An edit here is a
      STOP per §9 item 3, not a routine change)
[?] docs/02-technical/GAME_EVENTS.md       (ONLY if §3 item 6 determines a semantic
      clarification follows directly from B-3 without inventing an event)
[?] docs/01-game-design/CARD_RULES.md      (ONLY if §2's Shield line or §4.1's
      Tidal Barrier line is now stale; no magnitude is authored either way)
[?] docs/01-game-design/PASSIVE_RULES.md   (ONLY if §8's Huyền Quy line is now
      stale)
[ ] docs/00-overview/                      — NONE
[ ] docs/03-decisions/ADR/                  — NONE
[ ] docs/02-technical/SIGNALR_PROTOCOL.md   — NONE (must stay byte-identical)
[ ] docs/02-technical/REDIS_STATE.md        — NONE
[ ] docs/02-technical/DATABASE.md           — NONE
[x] tasks/ (this file only)
```

The executing agent must reduce each `[?]` to `[x]` or `[ ]` based on what the
change actually requires, and **must not touch a document it does not need to**
(`documentation/documentation-change.md` §1).

---

## 7. Implementation Notes

- **`COMBAT_RULES.md` §4 is the owner, and this is settled — do not
  re-litigate it.** TASK-103 classifies it as "the canonical owner of Heal and
  Shield rules", its §5.1 points at it for Shield, and `GAME_STATE.md` §2.3.1
  item 5 already delegates the depletion behavior to it. The rule text belongs
  there; `GAME_STATE.md` references it.
- **`documentation-change.md` §2 governs: one concept, one owner.** The stacking
  rule is a gameplay rule and belongs in `COMBAT_RULES.md`. If the resolved
  contract needs a sentence in `GAME_STATE.md`, that sentence describes
  **state**, not rules — and the expected outcome is that it needs none, because
  the state contract already says this. Do not duplicate the rule to make it
  easier to find, and do not edit `GAME_STATE.md` for symmetry.
- **Apply the decision; do not author a new rule.** Every semantic this task
  writes must be traceable to TASK-104 B-1/B-2/B-3. If expressing a decision
  seems to require a fourth semantic the Product Owner did not supply, that is a
  STOP (§9 item 4), not an inference.
- **The depletion delegation is the key gap this task closes.**
  `GAME_STATE.md` §2.3.1 item 5 says the condition's behavior "is owned by
  `COMBAT_RULES.md` §4 and §5.1" — and §4/§5.1 do not define it. That is a
  dangling delegation today; after this task it must resolve.
- **`ShieldDepleted` needs a determination, not an invention (§3 item 6).** Read
  `GAME_RULES.md` §16, `GAME_EVENTS.md` §1/§2, and `GAME_STATE.md` §5.1.1
  item 10 together before deciding. Note that `GAME_STATE.md` §5.1.1 item 10
  currently asserts the lifecycle "adds no event" — so if §3 item 6 concludes a Battle
  Event is required, that item is one of the statements that becomes stale and
  must be reconciled in the same change (and the protocol consequence reported
  for a later task). If §3 item 6 concludes it is an internal trigger, say so plainly
  and leave the event documents alone.
- **Do not let the change drift into the Turn-based rule.** Shield is
  `Type = "Shield"` and trigger-based; `COMBAT_RULES.md` §5.3.2 excludes
  trigger-based expiry from the Turn countdown, and `GAME_STATE.md` §5.1.1
  item 7 forbids the step 19a pass from inventing a duration for it. This change
  must not give Shield a duration.
- **Cite, do not restate.** Keep the `<c>DOC.md</c> §N` citation idiom. Never
  copy a Cost, a magnitude, a formula, or a schema into this task file or into a
  comment (`AGENTS.md` §9, `documentation-change.md` §2, `tasks/README.md` §9).
- **Follow `gameplay-change.md` §3's order.** Update the authoritative
  game-design documentation first (the owning domain rule doc), then review
  downstream implications, then the technical documentation **only if required**,
  then the ADR question (answered: not required), then review.
- **Re-check `MVP_SCOPE.md` at every step** (`gameplay-change.md` §4). This
  change adds no system and reaches no OUT item; if the smallest faithful
  expression of a decision would require one, STOP rather than expand scope.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). Known candidates, all
  previously reported and none belonging to this task: Tidal Barrier's
  unauthored Shield magnitude (deferred to a separate content decision);
  Thanh Xà / Sơn Hùng Signature Skill content; the `SIGNALR_PROTOCOL.md` "§3.3"
  and `BOSS_RULES.md` §4 citation nits TASK-103's verification found; and the
  `DamagePipeline.cs` comment TASK-102 must correct.
- **TASK-102 stays exactly as it is.** Do not edit it, move it, re-status it, or
  rewrite its acceptance criteria — even though its "Multiple Shields stack
  additively" criterion is now **superseded by this rule change**. Report that
  the criterion is stale and needs a follow-up correction; do not change it
  (`TASK_LIFECYCLE.md` §3, `tasks/README.md` §9).

---

## 8. Testing Requirements

This task changes no code, so it produces no unit, integration, or gameplay
test. Its verification is a **documentation consistency audit**, a **scope
validation**, and a **rule-determinism check**, at the depth
`core/validation.md` §2 requires for a HIGH-risk change — which for a
documentation-only gameplay change means the scenario analysis below must be
answerable unambiguously from the resulting rule text, so that the later
implementation task can test it.

### Required Verification

```text
[x] Documentation consistency audit — across every document touched AND every
                                      document that references them:
                                        COMBAT_RULES.md §4/§5.1/§5.2
                                          ↔ GAME_STATE.md §2.3.1/§2.3.3/§5.1.1
                                          ↔ CARD_RULES.md §2/§4.1
                                          ↔ PASSIVE_RULES.md §8
                                          ↔ GAME_EVENTS.md §1/§2 (if touched)
                                          ↔ BOSS_RULES.md §6.3.1 (the other
                                             StatusEffect consumer) — checked
                                             for staleness, not edited
[x] Rule-determinism check          — each scenario in "Key Edge Cases" is
                                      answerable from the new rule text alone,
                                      with no appeal to implementation
[x] Ownership check                 — one concept, one owner; no rule duplicated
                                      between COMBAT_RULES.md and GAME_STATE.md
[x] Round-trip obligation review    — confirm the Shield representation survives
                                      REDIS_STATE.md §7 item 9 with no new key
                                      and no Redis-only field
[x] Scope validation                — MVP_SCOPE.md §1/§2;
                                      quality/scope-validation
[x] Contract-completeness check     — read TASK-102's Shield acceptance criteria
                                      against the resolved contract and confirm
                                      each is implementable without guessing,
                                      EXCEPT the ones this change supersedes
                                      (which are reported, not edited)
[ ] Unit tests                      — N/A (no code)
[ ] Integration tests               — N/A (no code)
[ ] Gameplay scenarios              — N/A (no code; the scenarios below are the
                                      rule's required coverage, authored as
                                      tests by the implementation tasks)
```

### Key Edge Cases

Derived from the approved decisions. The resolved wording must answer each
unambiguously:

```text
Refresh (B-1)
- Shield applied with no active Shield      — one Shield of the applied value.
- Shield applied while a Shield is active   — the existing Shield is refreshed;
                                              exactly one Shield remains, and
                                              its value is NOT the sum.
- Refresh where the new value is smaller    — the resulting value is stated
                                              deterministically.
- Refresh where the new value is equal      — deterministic, idempotent.
- Two different Shield SOURCES (the Shield Card, Huyền Quy's Passive) applying
  to the same entity                        — both are the same effect identity
                                              ("Shield"), so the second refreshes
                                              the first rather than forming a
                                              second pool.

Depletion (B-3)
- Damage less than the pool                 — pool reduced, HP unchanged.
- Damage exactly equal to the pool          — pool reaches 0, HP unchanged,
                                              Shield removed.
- Damage greater than the pool              — pool reaches 0, Shield removed, HP
                                              reduced by exactly the remainder.
- The pool reaching 0                       — removal in the SAME resolution;
                                              0 is never an observable committed
                                              Shield value.
- ShieldDepleted evaluation/emission timing — stated in the rule (same
                                              resolution), with the event-vs-
                                              trigger determination recorded.
- A Shield already at 0                     — not a representable committed
                                              state.

Non-interaction
- Shield and the Turn countdown             — Shield is trigger-based and
                                              acquires NO duration
                                              (COMBAT_RULES.md §5.3.2,
                                              GAME_STATE.md §5.1.1 item 7).
- Shield and the Damage Pipeline formula    — steps 1–6 unchanged; Shield
                                              amounts remain outside the
                                              pipeline (§4 item 4).
- Shield and step 19a                       — the countdown leaves a trigger-based
                                              instance alone.
```

---

## 9. Stop Conditions

Universal stop conditions (`AGENTS.md` §20, `.ai/README.md` §13) always apply.
Task-specific:

1. **If applying B-1/B-2/B-3 would require authoring a gameplay semantic the
   Product Owner did not supply** — e.g. a magnitude comparison rule, a
   maximum/clamp behavior, a stacking cap, a duration, or a new Shield type —
   **STOP and report** (`AGENTS.md` §7, `GAME_RULES.md` §20). Do not infer it.
2. **If `GAME_EVENTS.md` §6's determination cannot be made from the approved
   decision's wording** — i.e. it is genuinely undetermined whether B-3's
   "emit `ShieldDepleted`" means a new Battle Event or an internal trigger —
   **STOP and record the required human/product-owner decision.** Do not author
   an event name, a payload, a `GAME_RULES.md` §16 entry, or a wire member.
3. **If the resolved Shield representation requires a change to `GAME_STATE.md`
   beyond reconciling a now-stale cross-reference** — e.g. a new member, a
   grouping key, an ordinal, a sequence, or any per-instance identity — **STOP**
   (`GAME_STATE.md` §0 item 5, TASK-102 §10). Do not add a state field.
4. **If the resolution would require a new architectural decision or an ADR** —
   **STOP** (`AGENTS.md` §18). This task creates no ADR.
5. **If two authoritative documents still conflict after the change** —
   including a conflict the change creates with a third document —
   **STOP per `AGENTS.md` §4** and report both sources.
6. **If satisfying any criterion requires modifying TASK-102, TASK-103,
   TASK-104, TASK-036, TASK-079, TASK-099, or any `tasks/completed/*` file** —
   **STOP**; report the stale statement instead (`TASK_LIFECYCLE.md` §3).
7. **If the change would require a SignalR / `SIGNALR_PROTOCOL.md` edit, a Redis
   key or field, an API change, a migration, or any client work** — **STOP**;
   report the consequence for a separate task rather than applying it here.
8. **If a Shield magnitude, Cost, balance value, or Tidal Barrier's amount would
   have to be chosen** — **STOP**; that is a separate gameplay-content decision
   (TASK-104 B-4). No value is invented.
9. **If the change would cross into `MVP_SCOPE.md` §2's OUT list, or reach an
   unlisted (FUTURE) system** — **STOP** (`AGENTS.md` §8, `MVP_SCOPE.md` §4).
10. **If the task exceeds 7 skills or crosses an uncoupled architectural
    boundary** — **STOP and decompose** (`tasks/README.md` §13).

When stopped, report the exact condition and **do not invent a resolution**.

---

## 10. Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual. Record a §9 STOP report here instead if a human
  decision or a state-contract change is required — do not claim a resolution
  that was not made.
-->

### Pickup Baseline (recorded)

```text
6484133C4123901276FA50AB2E4B7FFC40A20F5A7098AB128D75EA98CE5D944C  docs/01-game-design/COMBAT_RULES.md
7004B262B7B4CFDDA7FE479038D9C281299040FF983CDFF828AABE6C6481609B  docs/02-technical/GAME_STATE.md
454A6F82611B927F0C4987A9014ECBE75690AC93147225A490B5C43CF27E75CE  docs/02-technical/GAME_EVENTS.md
F08C2490306ED662A25E569EA5D9F1D25B6AA486BFDA4C568F4D5F02C690D82F  docs/01-game-design/CARD_RULES.md
D8F9ACFA698E8769EFFC67A2ECD47FDE7DE97021D2AB89546930F276B059031C  docs/01-game-design/PASSIVE_RULES.md
AE4FD980C6D5444148093A1BA76831586D45ABA9263A50DDAB447177B4CB2AA1  docs/02-technical/SIGNALR_PROTOCOL.md
7514786E52AFE7774CA64451275C66A3CA7BEE8AF2B9AC17D4C0CA96CD143C60  docs/01-game-design/GAME_RULES.md
24D14B21EDBDC6105C5BB61001253571CF91D47FBED64931184981BEEE45A773  tasks/backlog/TASK-102-implement-card-cast-server-path.md
```

`git status --short` at pickup recorded the same pre-existing modifications
TASK-103 recorded: `docs/01-game-design/{BOSS_RULES,COMBAT_RULES,GAME_RULES}.md`,
`docs/02-technical/{API_CONTRACTS,ARCHITECTURE,DATABASE,GAME_EVENTS,GAME_STATE,
SIGNALR_PROTOCOL,TDD}.md`, several `src/` and `tests/` files, and the untracked
`StatusEffect*` / `TASK-086…TASK-105` files. None was reverted, and none was
folded into this task. The baseline is the **working-tree content at pickup**,
not `HEAD`.

**Post-change re-verification:** only `COMBAT_RULES.md` moved
(`6484133C…` → `258B12A3E53962AD3983DFE6E9895FE4AD07762EF1997FFFF5FDA70802EF0EF6`).
All six other baseline files are byte-identical to pickup.

### Shield Rule — Resolution

```text
Conflict resolved:        YES — COMBAT_RULES.md §4 item 3  vs  GAME_STATE.md
                          §2.3.1 item 6. Resolved entirely on the gameplay
                          side, exactly as TASK-105 §0 predicted: the state
                          contract was already correct.
Ruling applied:           B-1 refresh (TASK-104 Product Owner, verbatim
                          "Refresh Shield"), B-2 (verbatim "Shield is
                          StatusEffect"), B-3 (verbatim "Remove at 0, emit
                          ShieldDepleted, overflow damage continues").
Canonical owner edited:   docs/01-game-design/COMBAT_RULES.md §4 (Healing and
                          Shields) — the canonical owner of the Shield rule,
                          and the section GAME_STATE.md §2.3.1 item 5 already
                          delegates the depletion behavior to. §5.1 and §5.2
                          reconciled as dependent references only.
Refresh semantics:        A Shield applied while a Shield of the same effect
                          identity is active REPLACES that Shield's value —
                          the pool is SET TO THE MAGNITUDE OF THE NEW
                          APPLICATION. It is not summed, not maximized, and
                          not compared, so a test asserts exactly one outcome
                          for the smaller / equal / larger cases alike.
Multiple Shields:         At most ONE Shield is active per entity, identified
                          by the Status Effect identity "Shield". No second
                          instance is created by any application.
Additive accumulation:    NEVER occurs under any MVP condition. Two different
                          Shield sources (Shield Basic Card, a Shield-granting
                          Passive) refresh one another rather than forming a
                          second pool.
Absorption:               The pool reduces incoming Final Damage BEFORE HP is
                          affected. §4 item 2's former "consumed first-in"
                          wording is reconciled: under a single pool there is
                          no multi-pool ordering to resolve and no "first-in"
                          tie-break exists. No new ordering rule was invented.
Depletion at 0:           The pool reaching EXACTLY 0 removes the Shield IN
                          THE SAME RESOLUTION that depleted it. A committed
                          Shield value of 0 is never observable as an active
                          Shield.
Overflow:                 Damage greater than the pool reduces HP by EXACTLY
                          the remainder (damage minus the pool). Damage exactly
                          equal to the pool leaves HP unchanged. No
                          double-counting: the absorbed portion never also
                          reduces HP.
Carve-out:                The "unless a Relic specifies otherwise" exception is
                          RETAINED with its scope stated. Scope: NO MVP Relic
                          exercises it (ROADMAP.md Phase 1 — "No Relics yet").
```

### Events Determination

```text
ShieldDepleted is a Battle Event:     NO — it is an internal combat
                                      resolution trigger (the trigger-based
                                      expiry condition of the Shield Status
                                      Effect instance).
Reasoning:                            The determination follows from the
                                      authoritative documents, and B-3's own
                                      wording does not require an event:
                                      1. ShieldDepleted is NOT in GAME_RULES.md
                                         §16's canonical event list — the
                                         repository's own "canonical list —
                                         do not duplicate elsewhere".
                                      2. It appears NOWHERE in GAME_EVENTS.md
                                         — 0 matches document-wide, in neither
                                         §1's ordered list nor §2's event
                                         reference. GAME_EVENTS.md §3 item 5
                                         ties "an event that exists" to being
                                         DELIVERED in the resolution batch, so
                                         authoring a ShieldDepleted entry would
                                         obligate a delivery this task is
                                         forbidden to add.
                                      3. Across all of docs/ the string occurs
                                         EXACTLY ONCE — as a schema example
                                         value in GAME_STATE.md §2.3.1's
                                         ExpiryCondition comment. It is a
                                         LABEL, not a defined event, and
                                         §2.3.1 item 5 states outright that
                                         "ExpiryCondition is a condition label,
                                         not a rule".
                                      4. GAME_STATE.md §5.1.1 item 10 states
                                         the Status Effect lifecycle "adds no
                                         event, no payload member, and no
                                         SignalR method".
                                      5. §5.1.1 item 7 already frames a
                                         trigger-based instance as removed "by
                                         its own documented trigger
                                         (COMBAT_RULES.md §4, §5.2 item 1)" —
                                         i.e. a trigger evaluated inside
                                         resolution, not an emitted event.
                                      6. Decisive: the Emission would require
                                         authoring a new event NAME in
                                         GAME_RULES.md §16 and a new §2 entry
                                         in GAME_EVENTS.md, and would make
                                         GAME_STATE.md §5.1.1 item 10 stale.
                                         TASK-105 §9 item 2 and the user's
                                         explicit instruction forbid inventing
                                         an event contract, and §3 item 6
                                         requires the (b) determination be
                                         "say[n] plainly". It is (b).
                                      Reading B-3's "emit ShieldDepleted" as
                                      the trigger firing (the documented
                                      condition being evaluated and the
                                      instance removed) is the smallest
                                      faithful expression of the decision and
                                      authors no new contract.
GAME_EVENTS.md changed:               NO — verified byte-identical
                                      (454A6F82…). No semantic clarification
                                      follows from B-3 that does not invent an
                                      event, so §3 item 6's "leave the event
                                      documents untouched" branch applies. A
                                      decision to NOT emit a Battle Event is a
                                      determination, not an omission.
GAME_RULES.md §16 changed:            NO — byte-identical (7514786E…). No name
                                      was added to the canonical list.
GAME_STATE.md §5.1.1 item 10 reconciled: NO CHANGE NEEDED — its "adds no
                                      event, no payload member, and no SignalR
                                      method" clause remains TRUE under the (b)
                                      determination. Had the determination been
                                      (a), this item would have become stale
                                      and required reconciliation.
Protocol consequence reported for a later task: NONE — because the
                                      determination is (b), no event reaches
                                      the wire, so SIGNALR_PROTOCOL.md §3.2
                                      needs no projection and no later task is
                                      created. SIGNALR_PROTOCOL.md is
                                      byte-identical (AE4FD980…) as required.
```

### Changed Files

- `docs/01-game-design/COMBAT_RULES.md` — **the canonical owner of the Shield
  rule**, and the only file changed by this task. §4 rewritten from four bare
  items to six: item 2's "consumed first-in" reconciled to consumption before HP
  under a single pool; item 3 changed from additive stacking to refresh
  (set-to-new-magnitude, one instance, no accumulation, carve-out preserved with
  scope stated); items 4–5 ADD the depletion rule (removal at exactly 0 in the
  same resolution; the `ShieldDepleted` label recorded as a trigger, not an
  event; overflow to HP by exactly the remainder, with the three boundary cases
  stated); the former item 4 (Heal/Shield not in the Damage Pipeline) is now
  item 6 with the absorption step placed after pipeline step 6, leaving §3
  steps 1–6 unchanged. §5.1's `Shield` line and §5.2 item 2's carve-out wording
  reconciled as dependent references. Version header advanced 1.5 → 1.6.
- `tasks/active/TASK-105-change-shield-application-semantics-to-refresh.md` —
  this file: `backlog/` → `active/`, Status `BACKLOG` → `IN REVIEW`, and this
  Completion Evidence.

**No other file was changed.** `GAME_STATE.md`, `GAME_EVENTS.md`,
`CARD_RULES.md`, `PASSIVE_RULES.md`, `GAME_RULES.md`, and
`SIGNALR_PROTOCOL.md` are byte-identical to pickup (hashes above).

### Documentation

- `COMBAT_RULES.md` §4 — **changed; it owns this rule.** TASK-103 classifies
  §4 as "the canonical owner of Heal and Shield rules", §5.1 points at it for
  Shield, and `GAME_STATE.md` §2.3.1 item 5 already delegates the depletion
  behavior to "`COMBAT_RULES.md` §4 and §5.1". That delegation was **dangling**
  before this change (those sections did not define depletion) and now
  resolves. The rule text belongs here; other documents reference it.
- `GAME_STATE.md` §2.3.1 item 6 / §2.3.3 / §5.1.1 item 1 — **NOT changed;
  verified to already express B-1/B-2 exactly.** item 6 states there is never
  more than one instance per effect identity and that re-application refreshes
  "rather than appending a second one"; §2.3.3 states "No stacking model other
  than §5.2 item 2's refresh-in-place"; §5.1.1 item 1 makes apply "a 'set', not
  an increment" that "does not add to the current value". The approved decision
  makes the **current state contract correct** and the **former gameplay rule
  wrong**, so the conflict resolved entirely on the gameplay side. Per TASK-105
  §3 item 5 and §9 item 3, editing `GAME_STATE.md` would have been a STOP — it
  was not needed and was not done.
- `CARD_RULES.md` §2 / §4.1, `PASSIVE_RULES.md` §8 — **checked for staleness,
  NOT changed.** Neither asserts nor implies Shield stacking: §2 states only
  "Active Pet gains Shield equal to 20% of its Max HP"; §4.1 states only "Heal;
  Gain Shield"; §8 states only "Gain Shield = 15% Max HP". All three already
  read as single applications and are consistent with refresh. No magnitude was
  authored or altered.

### Validation Results

- Documentation consistency audit — **PASS.** Compared after the change:
  `COMBAT_RULES.md` §4/§5.1/§5.2 ↔ `GAME_STATE.md`
  §2.3.1/§2.3.3/§5.1.1 ↔ `CARD_RULES.md` §2/§4.1 ↔ `PASSIVE_RULES.md` §8 ↔
  `BOSS_RULES.md` §6.3.1 (the other StatusEffect consumer — checked, no Shield
  statement, not edited) ↔ `GAME_EVENTS.md` §1/§2 (checked, not edited). A
  repo-wide sweep for `stack additively` / `stacking additively` /
  `Multiple Shields` returns **zero matches** in `docs/`. The only remaining
  `first-in` matches are `COMBAT_RULES.md`'s new text stating the ordering is
  vacuous and `MATCH3_RULES.md` §5.5.1's unrelated Special Gem **collision**
  rule, which this task does not touch.
- Rule-determinism check — **PASS.** Every Key Edge Case in TASK-105 §8 is
  answerable from the new §4 text alone, with no appeal to implementation:
  refresh with no active Shield → one Shield of the applied value (item 3);
  applied while active → existing Shield refreshed, exactly one remains, value
  is NOT the sum (item 3); new value smaller / equal → set to the new value,
  deterministic and idempotent (item 3); two different sources → same identity
  "Shield", so the second refreshes the first (item 3); damage less than the
  pool → pool reduced, HP unchanged (item 5); exactly equal → pool reaches 0,
  HP unchanged, Shield removed (items 4–5); greater → pool reaches 0, Shield
  removed, HP reduced by exactly the remainder (item 5); pool at 0 → removal in
  the same resolution, 0 never observable (item 4); `ShieldDepleted` timing →
  same resolution, internal trigger (item 4); Shield and the Turn countdown →
  trigger-based, NO duration acquired (preamble + §5.3.2); Shield and the
  pipeline formula → steps 1–6 unchanged, amounts outside the pipeline
  (item 6); Shield and step 19a → untouched by the countdown (item 4).
- Ownership check — **PASS.** One concept, one owner: the refresh / depletion /
  overflow rule is stated only in `COMBAT_RULES.md` §4; `GAME_STATE.md` states
  **state** (§2.3.1 schema, §5.1.1 mutation, zero-is-not-stored) and references
  §4 for the rule rather than restating it. No rule was duplicated, and
  `GAME_STATE.md` was not edited for symmetry.
- Scope validation (`MVP_SCOPE.md` §1/§2) — **PASS.** Cards (including the
  Shield Basic Card) and Combat/Status Effects are IN. This change adds no
  system, no Card, no Pet, no Boss, no Relic, no Element, and no Status Effect
  type; no OUT item is reached and no unlisted (FUTURE) system is touched.
- Round-trip obligation review — **PASS.** The Shield pool remains a
  `StatusEffect` instance inside the existing `PetState.StatusEffects[]` /
  `BossState.StatusEffects[]` collection, so it serializes with `BattleState`
  under the existing obligation (`REDIS_STATE.md` §7 item 9, §2 item 1;
  `GAME_STATE.md` §2.3.2). No new key, record, TTL, or Redis-only field.
- Contract-completeness / TASK-102 implementability check — **PASS with one
  reported stale criterion.** The following TASK-102 acceptance criteria are
  now implementable without guessing: "`Shield` grants … a Shield absorption
  pool" (§4 items 2–3), "Shield absorption works … reduced by the target's
  Shield pool before HP is affected" (§4 item 5, and item 2's reconciled
  wording), the damage-exceeding-the-pool case (§4 item 5), and the
  exact-0 / `ShieldDepleted` boundary (§4 item 4). **STALE — reported, NOT
  edited:** TASK-102's criterion *"Multiple Shields stack additively into one
  pool (`COMBAT_RULES.md` §4 item 3) — proven by a test"* is **superseded** by
  this change; its "consumed first-in" clause in the absorption criterion is
  likewise now vacuous. See "Unrelated Stale Documentation Discovered".
- `dotnet build` / `dotnet test` / `npm test` — **N/A and not run**: this task
  changes no code and the task's own §8 states it produces no unit,
  integration, or gameplay test. No test command was executed, and no
  documentation-lint target exists in the repository (no lint script or
  validation tooling found), so validation is the audit above.

### Contract Preservation Verification

```text
Card rules              UNCHANGED — CARD_RULES.md byte-identical (F08C2490…);
                        §2/§4.1 wording was not stale, and no magnitude, Cost,
                        or effect value was authored or altered.
Combat damage formula   UNCHANGED (COMBAT_RULES.md §3 pipeline steps 1–6; the
                        absorption rule is applied to Final Damage after
                        step 6 and does not alter the pipeline).
Shield rule             CHANGED — additive stacking → refresh (this task).
Status Effect duration  UNCHANGED (COMBAT_RULES.md §5.3 DR1–DR6; Shield stays
                        trigger-based and outside the Turn countdown).
BattleState schema      UNCHANGED — no new member; verified: no
                        PetState.Shield, no PetState.ShieldPoints, no
                        BattleState.Shield, no grouping key, no ordinal, no
                        sequence (GAME_STATE.md §0 item 5).
PetState                UNCHANGED — StatusEffects[] reused.
SignalR protocol        UNCHANGED (byte-identical, AE4FD980…; §3.2.2's
                        discriminator set and §3.2.20–§3.2.25 stand as
                        TASK-103 left them).
API                     UNCHANGED (no API_CONTRACTS.md edit by this task).
Redis                   UNCHANGED (no new key, TTL, record, or Redis-only field).
PostgreSQL              UNCHANGED (no schema, entity, column, or migration).
src/ and tests/         UNCHANGED (byte-identical — no entry added to or
                        removed from the pickup git status).
docs/00-overview/       UNCHANGED.
docs/03-decisions/      UNCHANGED (0 ADRs; no ADR required — verified against
                        docs/03-decisions/README.md §8).
TASK-102/103/104, TASK-036/079/099, tasks/completed/   UNCHANGED.
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic introduced
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed no Shield balance value or magnitude invented; Tidal Barrier's
      magnitude remains deferred
- [x] Confirmed the change applies B-1/B-2/B-3 only, and authored no new rule
- [x] Confirmed no SignalR, Redis, API, or client change

### Review Verdict — PASS

Independent review performed against `quality/review.md` §1, skipping only the
code-only items per `documentation/documentation-change.md` §4. Every claim in
the Completion Evidence above was independently re-verified rather than accepted.

```text
Correctness     PASS — COMBAT_RULES.md §4 now expresses B-1 (refresh),
                B-2 (StatusEffect representation), and B-3 (remove at 0,
                overflow continues) and nothing beyond them. §4 items 2–6
                were read in full.
Architecture    PASS (N/A for code) — no layering, module, or boundary touched.
Scope           PASS — verified by hash and by mtime, not by assertion:
                  * the 7 baseline hashes recomputed now match the recorded
                    post-change values EXACTLY, including COMBAT_RULES.md
                    = 258B12A3… and the 6 files claimed byte-identical;
                  * repo-wide grep of docs/ for `stack additively` /
                    `stacking additively` / `Multiple Shields` = 0 matches;
                  * the string `ShieldDepleted` occurs in docs/ only in
                    GAME_STATE.md §2.3.1's example label and in
                    COMBAT_RULES.md's new §4 text/version header;
                  * zero files under src/ or tests/ carry an mtime later than
                    COMBAT_RULES.md, confirming no code or test was touched.
                No unrelated refactor, addition, or "improvement" found.
Tests           PASS (N/A) — the task's own §8 declares unit/integration/
                gameplay tests N/A for a documentation-only change; §8's
                Required Verification items are all checked, and the
                rule-determinism check is independently reproducible: each
                Key Edge Case is answerable from §4 alone.
Documentation   PASS — one concept, one owner holds. The stacking/refresh/
                depletion/overflow rule is stated ONLY in COMBAT_RULES.md §4;
                GAME_STATE.md states state and references §4 (§2.3.1 item 5's
                delegated condition is now resolved — the delegation was
                dangling before). GAME_STATE.md was NOT edited for symmetry,
                correctly per §3 item 5 / §9 item 3.
Security        PASS (N/A) — no auth, credential, or data-exposure surface.
Performance     PASS (N/A) — no hot-path change.
Maintainability PASS — no abstraction introduced; AGENTS.md §9 respected.
Determinism     PASS — server authority unaffected; the rule adds no RNG and
                no client-side computation. §4 is now deterministic enough
                that TASK-102 can assert a single outcome per case.
```

**Reviewed against the task's five questions.**

```text
1. Applies ONLY B-1/B-2/B-3?           YES. Each semantic in §4 traces to a
                                       ruling: set-to-new-magnitude, one
                                       instance, no accumulation → B-1
                                       ("Refresh Shield"); the existing
                                       StatusEffect representation, no
                                       parallel member → B-2 ("Shield is
                                       StatusEffect"); removal at exactly 0 in
                                       the same resolution, overflow to HP by
                                       the remainder, ShieldDepleted as the
                                       trigger → B-3. No fourth semantic was
                                       authored; the smaller/equal/larger
                                       cases are derived from B-1's "set",
                                       not invented as a max/clamp rule.
2. Introduces NO new event, state   CONFIRMED, and this is the sharpest point
   field, protocol member, or         of the review. B-3's verbatim wording
   implementation behavior?           says "emit ShieldDepleted", which reads
                                       like a new event; the task correctly
                                       determined this is an INTERNAL
                                       RESOLUTION TRIGGER instead, on
                                       documentary evidence that holds up:
                                       ShieldDepleted is absent from
                                       GAME_RULES.md §16's canonical list,
                                       absent from GAME_EVENTS.md entirely,
                                       and appears in docs/ only as
                                       GAME_STATE.md §2.3.1's schema example
                                       label — which §2.3.1 item 5 itself calls
                                       "a condition label, not a rule".
                                       Treating it as an event would have
                                       obliged a §16 name, a §2 entry, a §1
                                       ordering slot, and a SIGNALR_PROTOCOL.md
                                       §3.2 projection — all forbidden to this
                                       task and all absent. §5.1.1 item 10's
                                       "adds no event, no payload member, and
                                       no SignalR method" therefore remains
                                       TRUE and required no reconciliation.
                                       No new state field: no PetState.Shield,
                                       no PetState.ShieldPoints, no
                                       BattleState.Shield, no grouping key,
                                       ordinal, or sequence (GAME_STATE.md §0
                                       item 5).
3. COMBAT_RULES.md sole owner of    YES. §4 now self-declares as "the canonical
   Shield semantics?                  owner of the Shield rule", and
                                       GAME_STATE.md §2.3.1 item 5 delegates
                                       the condition's behavior to
                                       "COMBAT_RULES.md §4 and §5.1" — a
                                       delegation that was DANGLING before this
                                       change (§4/§5.1 did not define depletion)
                                       and now resolves. GAME_STATE.md states
                                       state, not rules; no rule is duplicated.
4. All acceptance criteria          YES — all 19 (§5 criteria 1–19) are
   satisfied?                          satisfied; criteria 13–19 (scope,
                                       byte-identity, ownership, consistency)
                                       were re-verified independently here
                                       rather than taken on trust.
5. Validation results complete?     YES, with the honest note that build/test
                                       were correctly declared N/A because no
                                       code changed. The six checked Required
                                       Verification items are each backed by a
                                       reproducible check. The one item the
                                       task could not resolve — TASK-102's
                                       superseded "stack additively" criterion —
                                       was correctly REPORTED rather than
                                       edited (§9 item 6), and is handled under
                                       TASK-102's readiness review, not here.
```

**No TASK-105 §9 stop condition fired**, and no `AGENTS.md` §20 condition
remains open. `GAME_STATE.md` and `GAME_EVENTS.md` did not need editing, which
the task predicted correctly rather than assuming — the prediction was checked.

**One caveat recorded, not a defect.** The (b) determination reads B-3's word
"emit" as the trigger firing rather than as publishing a Battle Event. That is
the smallest faithful expression of the decision and is the reading the
authoritative documents compel (an event of that name does not exist anywhere),
so it is correct under §9 item 2 and §3 item 6. It is flagged here because it is
the single place where the applied change interprets, rather than transcribes,
the Product Owner's wording — so that a human can overturn it cheaply if the
Product Owner intended a real event. Overturning it would be a new task, not a
rework of this one.

### Unrelated Stale Documentation Discovered (REPORTED, NOT CHANGED)

- **TASK-102 acceptance criterion superseded by this change.**
  `tasks/backlog/TASK-102-implement-card-cast-server-path.md` line 404:
  *"Multiple Shields stack additively into one pool (`COMBAT_RULES.md` §4
  item 3) — proven by a test."* Impact: the criterion now contradicts the
  authoritative rule and cannot be implemented. Also line 398–400's
  *"consumed first-in"* clause is now vacuous. **Not edited** — TASK-105
  §9 item 6 and `TASK_LIFECYCLE.md` §3 require a reported stale statement, not
  an inline fix. Suggested follow-up: a small BUG/DOCUMENTATION task to
  re-point TASK-102's Shield criteria at refresh semantics before TASK-102 is
  picked up.
- **TASK-102 implementation note now stale.**
  `tasks/backlog/TASK-102-implement-card-cast-server-path.md` line 523 records
  that `DamagePipeline`'s own docs say it "applies no Shield" — still accurate,
  and TASK-102's to correct. Reported only; no action here.
- **Tidal Barrier's Shield magnitude remains unauthored** (`CARD_RULES.md`
  §4.1 — "Heal; Gain Shield" with no amount). Deferred by the Product Owner to
  a separate gameplay-content decision (TASK-104 B-4 disposition). Confirmed
  still deferred; **no value invented**.
- **Thanh Xà / Sơn Hùng Signature Skill content** remains undefined
  (`CARD_RULES.md` §4.1, `PET_RULES.md` §8) — unrelated, reported only.
- **Pre-existing note, not introduced here:** the `SIGNALR_PROTOCOL.md` "§3.3"
  and `BOSS_RULES.md` §4 citation nits TASK-103's verification found. Reported
  only; no action here.
