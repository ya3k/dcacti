# TASK-117 — Author ADR-017 and the Authoritative `NextAttackCritModifiers[]` State-Model Documentation

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  PROVENANCE: The architecture follow-up TASK-116 identified. TASK-116 (DONE,
  Product Owner decision-input) recorded the `NextAttack` Crit contract
  (C-1–C-10) and reported that D-1 introduces `PetState.NextAttackCritModifiers[]`
  — a NEW authoritative battle-state concept — which AGENTS.md §18 classifies as
  requiring an ADR, and which therefore cannot be authored under TASK-116's
  DOCUMENTATION type. This task is the smallest unit that lands that architecture.

  THIS TASK DECIDES NOTHING ABOUT GAMEPLAY. Every rule it records was supplied by
  the Product Owner in TASK-116. Re-deciding C-1–C-9, choosing a representation,
  changing a lifetime, or inventing a consumption boundary is the single
  prohibited action of this task (AGENTS.md §7, §20).

  BOUNDARY: documentation and decision-record only. Zero files under src/ or
  tests/. No gameplay implemented, no migration, no Redis code, no SignalR code.
-->

---

## Metadata

```text
Task ID:           TASK-117
Type:              ARCHITECTURE (TASK_TYPES.md §2 — "Change the project's
                   structure, layering, technology choice, persistence
                   strategy, realtime strategy, or authoritative-state model
                   … or requires a new/updated ADR"; workflow
                   architecture/architecture-change.md, which composes
                   architecture/adr-change.md for the decision-record half).
                   The deliverable is ADR-017 plus the owning state-model and
                   domain-rule documentation it makes authoritative.
Status:            DONE (ADR-017 authored and indexed; owning state-model and
                   domain-rule documentation edits landed across GAME_STATE.md,
                   COMBAT_RULES.md, CARD_RULES.md, PASSIVE_RULES.md, REDIS_STATE.md,
                   SIGNALR_PROTOCOL.md, and DATABASE.md; TASK-115 verified
                   implementation-ready. Formal review pass completed against
                   quality/review.md §1 and core/completion.md §1: PASS. File moved
                   from tasks/backlog/ to tasks/completed/ per TASK_LIFECYCLE.md §3.)
Risk:              HIGH (TASK_TYPES.md §4 — ARCHITECTURE is always HIGH. It
                   authors the authoritative shape of a new `BattleState`
                   member that a HIGH-risk implementation task (TASK-115) will
                   build against, and it touches three cross-referenced domain
                   rule documents plus three technical contracts.)
Priority:          CRITICAL (the sole remaining blocker on TASK-115, which is the
                   ROADMAP.md Phase 1 "One Pet fully implemented (Element,
                   Passive, Signature Skill)" slice.)
Primary Agent:     backend (TASK_TYPES.md §5 — "Architecture: Backend Agent
                   (architecture owner)"; architecture/architecture-change.md
                   §1–§2. The state model is the Backend Agent's owning area.)
Supporting Agents: review (ADR compliance and cross-document consistency —
                   AGENTS.md §4, quality/documentation-consistency),
                   gameplay (COMBAT_RULES.md §3.3, CARD_RULES.md §4.1,
                   PASSIVE_RULES.md §7/§8 are the owning domain documents for
                   the composition, cap, and consumption rules — consulted to
                   author them accurately, NOT to decide them),
                   realtime (REDIS_STATE.md / SIGNALR_PROTOCOL.md impact
                   assessment only — both confirmed no change),
                   testing (deriving the Given/When/Then scenarios any later
                   implementation must satisfy; REPORTED, not authored here)
Workflow:          architecture/architecture-change.md
                   (+ architecture/adr-change.md §2 for ADR-017)
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/architecture-conformance,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-116 (DONE — the Product Owner decision set D-1–D-8 and
                    its bindable statement C-1–C-10. IMMUTABLE; read-only. NOT
                    modified by this task),
                   TASK-115 (BACKLOG, BLOCKED-BY-THIS-TASK — the downstream
                    implementation task. IMMUTABLE; NOT modified by this task),
                   TASK-114 (DONE — Combat RNG draw contract in
                    COMBAT_RULES.md §3.3, TDD.md §6, GAME_STATE.md §2.6.2.
                    IMMUTABLE; read-only),
                   TASK-113 (DONE — Crit roll decisions D-1–D-5. IMMUTABLE;
                    read-only),
                   TASK-112 (DONE — encoded the `Crit`/`scope "NextAttack"`
                    row. IMMUTABLE; read-only),
                   TASK-095 / TASK-096 (DONE — StatusEffect domain state,
                    step-19a lifecycle, serialization. The boundary this task
                    preserves. IMMUTABLE; read-only)
Blocks:            TASK-115 (its implementation may now begin against a frozen,
                   documented contract).
Estimate:          Normal (one ADR, one new state-model subsection plus a
                   lifecycle subsection, one new COMBAT_RULES §3.3 rule block,
                   two cross-reference edits, and three technical-contract
                   no-change records; no code, no tests, no migration)
```

**This task authors no gameplay rule and no balance value.** Every rule it
records is the Product Owner's, supplied in TASK-116. Its own authority covers
only *where* the rule lives, *what shape* the state has, and *which document*
owns each statement — `architecture/adr-change.md` §1's Decision vs.
Implementation split.

---

## Objective

Land the architecture decision that TASK-116 identified as required — author
**ADR-017** recording that `PetState.NextAttackCritModifiers[]` is
authoritative battle runtime state, separate from `StatusEffects[]`, persisting
across Turns until consumed by a qualifying owner attack — and update the
single canonical owner document for each statement, so that TASK-115 becomes
implementation-ready with no remaining architectural ambiguity.

---

## Authoritative References

- `docs/03-decisions/README.md` **§1** (purpose), **§2** (when an ADR is
  required), **§3** (an ADR never overrides a technical doc), **§4** (naming),
  **§5** (status values), **§7** (the index this task extends), **§8**
  (Known Open Items — checked; the NextAttack gap was not listed)
- `docs/03-decisions/ADR/ADR-016-independent-player-xp-and-pet-xp-tracks.md`
  and `ADR-014-battle-state-player-identity.md` — the two closest
  precedents for an ADR that records a `BattleState` member decision and
  reports its implementation consequence rather than performing it
- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState` tree), **§2.3.1**
  (the `StatusEffects[]` duration dichotomy and one-instance rule this
  collection is deliberately outside of), **§2.3.2** (the always-present
  collection + round-trip convention this collection follows), **§2.3.3**
  (the pending-collection prohibition), **§5.1** (single write-back),
  **§5.1.1** (the step-19a lifecycle this collection does not participate in),
  **§0** (staging)
- `docs/01-game-design/COMBAT_RULES.md` **§1.1** (Combat Stats — the Crit
  row), **§3.1** (step 4 ordering), **§3.3** (Critical Hits — items 1–6
  unchanged; the composition, cap, lifetime, and consumption rule is authored
  here), **§5.2**/`§5.3` (the Status Effect duration model this collection
  does not use)
- `docs/01-game-design/CARD_RULES.md` **§4.1** (Iron Fang — the Crit value
  and its independence from Bạch Hổ's Passive)
- `docs/01-game-design/PASSIVE_RULES.md` **§7**, **§8** (Bạch Hổ's Passive —
  the second `NextAttack` Crit source)
- `docs/01-game-design/GAME_RULES.md` **§17** (steps 10, 14, 19a — the
  creation and non-consumption sites), **§16** (canonical event list — no Crit
  event), **§18** (server authority)
- `docs/02-technical/REDIS_STATE.md` **§1**–**§4** (key structure, shape,
  single write-back, `Sequence` compare-and-set), **§7** item 9 (round-trip)
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.13** (`otherModifiers` as the
  sole step-4 carrier), **§4** items 4/12/13 (state added is not wire exposure
  added), **§4.2**/`§4.3` (the current payload member sets)
- `docs/02-technical/DATABASE.md` **§1**, **§3** item 1 (`scope` as a storage
  token — no persistence is added)
- `docs/02-technical/TDD.md` **§6** (single server-seeded PRNG);
  `docs/02-technical/ARCHITECTURE.md` **§3**, **§4**, **§5**
- `docs/03-decisions/ADR/ADR-001` (server authority), `ADR-009` (single PRNG),
  `ADR-010` (precedent: missing `BattleState` member resolved ADR-first),
  `ADR-011` (`PetState` as the combat runtime state), `ADR-014` (precedent:
  state member added, not a wire member)
- `tasks/backlog/TASK-116-...md` §"Resulting Contract" (C-1–C-10), §"Product
  Owner Decisions" (D-1–D-8), §"Required Documentation Changes", §"Classification
  Outcome" — **IMMUTABLE; read-only**
- `tasks/backlog/TASK-115-...md` — **IMMUTABLE; NOT modified**
- `docs/00-overview/MVP_SCOPE.md` §1 (Cards, Pets/Pet Passive/Signature Skill,
  Combat — `Crit` and `Status Effects` are named), §2 (OUT), §4
- `AGENTS.md` §4 (conflict resolution), §7 (never invent a rule), §9
  (anti-overengineering), §10 (server authority), §11 (determinism/RNG), §15
  (testing), §16 (task discipline), §17 (documentation change rule), §18 (ADR
  rule), §20 (stop conditions), §21 (output discipline), §22 (Definition of
  Done, §23 final principle);
  `.ai/workflow/architecture/architecture-change.md` §1–§3;
  `.ai/workflow/architecture/adr-change.md` §1–§4;
  `.ai/workflow/documentation/documentation-change.md` §1–§3 (`§2` no
  duplication, `§3` canonical owner);
  `tasks/README.md` §9 (no business-rule duplication), §12 (skill budget)

---

## Current State

TASK-116 recorded the Product Owner's `NextAttack` Crit contract and reported
the architecture consequence. The state model did not carry it:

```text
GAME_STATE.md §2.3            `Crit` defined as a stat with a unit and an
                              initial value. No statement that it is base or
                              composed; no modifier collection; no
                              composition rule; no removal rule.

GAME_STATE.md §2.3.1 item 3   Duration model is an exclusive dichotomy
                              (RemainingTurns XOR ExpiryCondition) that a
                              NextAttack modifier fits in neither branch of.

GAME_STATE.md §2.3.1 item 6   One instance per identity — cannot hold two
                              simultaneous NextAttack sources.

GAME_STATE.md §2.3.3          Forbids a pending/queued collection.

GAME_STATE.md §5.1.1 item 7   Defines no attack-consumption path.

COMBAT_RULES.md §3.3 item 5   Named three Crit sources; defined no
                              composition operator, no ordering, no cap, no
                              removal.

COMBAT_RULES.md §1.1          `Crit` had a default and no range.

CARD_RULES.md §4.1            "for the next attack only" — prose; no
                              boundary, lifetime, or removal mechanism.

PASSIVE_RULES.md §8           Bạch Hổ's increase — a config value with the
                              same undefined `NextAttack` semantics.

DATABASE.md §3 item 1         `scope = "NextAttack"` a storage token that
                              "authors no gameplay", delegating the rule to
                              CARD_RULES.md §4.1, which did not define it.
```

No ADR covered any of it: `docs/03-decisions/README.md` §7's index ended at
ADR-016, and §8 did not list the gap.

**Read-only note.** The `src/` implementation
(`BattleStateService.cs`'s `DefaultCrit` reset, `CardCastExecutor.cs`'s direct
`Crit` write) was inspected only to confirm the contract is implementable and
to record what must change; no file under `src/` or `tests/` is modified by
this task, and no statement below is derived from what the code happens to do.

---

## Scope

### In Scope

1. Author **ADR-017** using the repository's established ADR template
   (`docs/03-decisions/README.md` §1/§7, as `ADR-014` and `ADR-016` exemplify),
   including: Context, Problem (the gap and why the existing model cannot
   carry it), Decision, State Model, Ownership, Lifecycle, Crit Composition,
   Consumption Boundary, Serialization Impact, Alternatives Considered,
   Consequences, and the rejected alternatives.
2. Add ADR-017 to the index in `docs/03-decisions/README.md` §7, with the
   matching version note.
3. Author the **state representation** at its canonical owner
   (`GAME_STATE.md` §2.3/§2.3.4) and its **mutation lifecycle**
   (`GAME_STATE.md` §5.1.2).
4. Author the **Crit composition, cap, lifetime, and qualifying-attack
   consumption rule** at its canonical owner (`COMBAT_RULES.md` §1.1, §3.3).
5. Update the **dependent references** whose wording became stale:
   `CARD_RULES.md` §4.1, `PASSIVE_RULES.md` §8 (point at the owner; do not
   restate it), and `DATABASE.md` §3 item 1's delegation pointer.
6. Record the **serialization / Redis / SignalR / PostgreSQL impact** as
   no-change statements in the documents that own them (`REDIS_STATE.md` §7,
   `SIGNALR_PROTOCOL.md` §4), following the `StatusEffects[]` and
   `LastCommittedSwapPair` precedents.
7. Verify TASK-115 is implementation-ready against every
   implementation-critical contract, and report the result.

### Out of Scope

- **Any source code change.** Zero files under `src/` or `tests/`.
- **Implementing TASK-115** — or any part of it: the `DefaultCrit` reset
  removal, the `CardCastExecutor` element-addition change, the effective-Crit
  computation, or the consumption path.
- **Modifying TASK-116 or TASK-115.** Both stay byte-identical.
- **Re-deciding any gameplay rule.** C-1–C-9 are the Product Owner's; this task
  records them.
- Authoring any balance value. Crit's cap of 100 is recorded as TASK-116's
  **newly authored** value, not as a pre-existing documented one.
- Introducing a new Battle Event, SignalR method, wire member, RNG stream, or
  Status Effect type.
- Redesigning `StatusEffects[]` beyond the boundary TASK-116 drew (the new
  collection is separate; §2.3.1 item 3's dichotomy and item 6's uniqueness
  rule are neither widened nor relaxed).
- Adding PostgreSQL persistence for the new collection.
- Relic trigger evaluation (`ROADMAP.md` — "No Relics yet"), Pet Passive
  implementation, Boss AI, Match-3, or frontend work.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Acceptance Criteria

- [x] `docs/03-decisions/README.md` §7 index checked and the ADR numbering
      confirmed from the repository, not assumed: the highest existing ADR is
      **ADR-016**, so this task authors **ADR-017**.
- [x] ADR-017 authored at `docs/03-decisions/ADR/` following the repository's
      ADR template.
- [x] ADR-017 contains: Context, Decision, State Model, Ownership, Lifecycle,
      Crit Composition, Consumption Boundary, Serialization Impact,
      Alternatives Considered, and Consequences.
- [x] ADR-017 explicitly records all six required statements:
      `NextAttackCritModifiers[]` is authoritative battle runtime state; it is
      separate from `StatusEffects[]`; it persists across Turns until consumed
      by a qualifying attack; `DefaultCrit` is not a runtime reset mechanism;
      multiple applicable modifiers stack additively; consumption removes only
      the applicable temporary modifiers; Effective Crit is capped at 100
      percentage points.
- [x] ADR-017 added to the index table in `docs/03-decisions/README.md` §7
      with Status `Accepted` and a matching version note.
- [x] ADR-017 contains no implementation-specific class name presented as a
      contract (the one `src/` filename it names appears only in Context, as
      evidence of the defect the decision removes).
- [x] The minimum state shape is recorded and is exactly two members per
      element: a stable source identity and a Crit contribution in percentage
      points. No field was added "for convenience".
- [x] No `PendingStatusEffects[]`, `NextAttackQueue`, `CritQueue`,
      `CritEvents[]`, or other parallel in-flight representation is introduced.
- [x] The `StatusEffects[]` contract is preserved: `GAME_STATE.md` §2.3.1 item
      3's dichotomy and item 6's one-instance rule are neither widened nor
      relaxed, and ADR-017 explains why the new collection is separate.
- [x] The lifetime is documented such that no Turn expiry, timeout, automatic
      cleanup, or end-of-turn removal can be introduced by a later
      implementation; the persistence consequence is stated explicitly.
- [x] Crit composition is documented with the existing pipeline and RNG
      preserved, `EffectiveCrit` capped at 100, and the Crit stat range
      (0–100) recorded as separate from the Crit RNG bound (V ∈ [0,100)).
- [x] Source identity is documented as stable, per-source, deterministic, and
      never derived from a clock, GUID, allocation order, or array position.
- [x] Serialization impact assessed for `BattleState` JSON, Redis, the SignalR
      projection, and client runtime state; only the relevant contracts
      changed and no new SignalR payload was invented.
- [x] No PostgreSQL persistence added; no `CritModifiers`,
      `NextAttackModifiers`, or `PendingEffects` table introduced.
- [x] Each rule is stated once at its canonical owner
      (`documentation-change.md` §2/§3); referencing documents point at it
      rather than restating it.
- [x] Zero files under `src/` or `tests/` modified.
- [x] TASK-116 and TASK-115 are byte-identical.
- [x] TASK-115 readiness verified against every implementation-critical
      contract (see "TASK-115 Readiness"), with the result reported.
- [x] No new Battle Event, SignalR method, or RNG stream introduced.
- [x] Quality review checklist passes (`quality/review.md` §1), skipping
      code-only items per `documentation-change.md` §4.

---

## Affected Files & Areas

```text
[ ] src/backend/ (NONE — no source code modified)
[ ] src/frontend/client/ (NONE)
[ ] tests/ (NONE)
[x] docs/03-decisions/ADR/ADR-017-nextattack-crit-modifiers-as-petstate-battle-state.md (CREATED)
[x] docs/03-decisions/README.md                    (index + version note)
[x] docs/02-technical/GAME_STATE.md                (§2.3 tree, §2.3.4, §5.1.2)
[x] docs/01-game-design/COMBAT_RULES.md            (§1.1, §3.3 items 2/5/7–11)
[x] docs/01-game-design/CARD_RULES.md              (§4.1 cross-reference)
[x] docs/01-game-design/PASSIVE_RULES.md           (§8 cross-reference)
[x] docs/02-technical/REDIS_STATE.md               (§7 items 13–14)
[x] docs/02-technical/SIGNALR_PROTOCOL.md          (§4 item 14)
[x] docs/02-technical/DATABASE.md                  (§3 item 1 delegation pointer)
[ ] docs/02-technical/GAME_EVENTS.md               (NONE — no event change)
[ ] docs/02-technical/TDD.md                       (NONE — RNG contract unchanged)
[ ] docs/02-technical/ARCHITECTURE.md              (NONE — no module/layer change)
```

---

## TASK-115 Readiness

Every implementation-critical contract is now deterministic. Each point names
the single owning section.

```text
 1. Where NextAttackCritModifiers[] lives
      → GAME_STATE.md §2.3 tree + §2.3.4. A `PetState` member of `BattleState`,
        like every other `PetState` member. ADR-017 "State Model".

 2. Minimum state shape
      → GAME_STATE.md §2.3.4 items 1/4. Exactly two members per element:
        `SourceIdentity` (string, required) and `CritContribution` (number,
        required, percentage points). No third member exists.

 3. Source identity
      → GAME_STATE.md §2.3.4 item 2. Stable, per-source, deterministic;
        never a clock, GUID, allocation order, or array index. One element per
        distinct source, so Iron Fang and Bạch Hổ coexist and each is
        individually removable. ADR-017 "State Model".

 4. Creation ownership
      → ADR-017 "Ownership". The source's own existing resolution site:
        Card/Pet Skill at GAME_RULES.md §17 step 14, Pet Passive at step 10.
        No step is added to §17.

 5. Lifetime
      → COMBAT_RULES.md §3.3 item 8 (gameplay) + GAME_STATE.md §5.1.2 items
        2/3 (state mutation). Active until consumed; not Turn-based; not
        trigger-based; no step-19a decrement; no expiry, timeout, or cleanup
        of any kind. Persists across Turns by design.

 6. Qualifying attack boundary
      → COMBAT_RULES.md §3.3 item 8. An explicit owner attack action entering
        the Damage Pipeline. A non-damaging action does not consume; a raw
        damage instance is not itself an attack; a Burn/DoT tick and the
        Boss's own attack do not consume (though they remain Crit-eligible).

 7. Consumption point
      → COMBAT_RULES.md §3.3 item 8 + GAME_STATE.md §5.1.2 item 4. Once, at
        the first qualifying damage instance of the qualifying attack, removed
        in that resolution's single write-back (GAME_STATE.md §5.1).

 8. Multiple-source behavior
      → COMBAT_RULES.md §3.3 item 10 + GAME_STATE.md §5.1.2 item 1. Additive,
        do not replace one another, consumed together; a repeat application
        from the same source refreshes onto its own element.

 9. Base Crit independence
      → GAME_STATE.md §2.3.4 items 9/10 + COMBAT_RULES.md §3.3 items 7/9.
        `PetState.Crit` is the permanent/base value, never overwritten, and
        never restored by assignment; removal touches only consumed elements.

10. Crit cap
      → COMBAT_RULES.md §1.1 (0–100 percentage points, flagged as a newly
        authored value) + §3.3 item 7 (composition and cap). Kept distinct
        from the roll bound V ∈ [0,100) in §3.3 item 2.

11. Serialization behavior
      → GAME_STATE.md §2.3.4 item 8 + REDIS_STATE.md §7 item 13. Part of the
        `BattleState` JSON shape; always-present collection serializing an
        empty array when no modifier is active; lossless, order-preserving
        round trip.

12. Redis / runtime-state impact
      → REDIS_STATE.md §7 item 13. No new key, no Redis-only field, no second
        record; written in the existing single post-resolution write-back under
        the unchanged `Sequence` compare-and-set.

13. SignalR projection impact
      → SIGNALR_PROTOCOL.md §4 item 14 + GAME_STATE.md §2.3.4 item 8. None.
        Not a wire member; no new message, method, subscription, or payload
        member. Delivery would be a separately-decided protocol change.

14. CAS / retry expectations
      → GAME_STATE.md §5.1 item 2 and §5.1.2 items 6/7; REDIS_STATE.md §4
        items 2/5/7. Creation and consumption are intermediate values of one
        resolution; one atomic write-back gated on the pre-resolution
        `Sequence`; a rejected action mutates nothing; a retry re-runs the same
        deterministic computation.
```

**Result: TASK-115 is READY / UNBLOCKED.** No point above is ambiguous, and
none required a new gameplay decision.

---

## Testing Requirements

### Required Verification

```text
[x] N/A — architecture/decision task. No code, no tests authored.
```

This task creates no executable verification
(`.ai/workflow/documentation/documentation-change.md` §4). Its verification is
the Acceptance Criteria above plus `quality/review.md`'s
documentation-applicable items. The recorded contract is stated so that
**TASK-115** can derive Given/When/Then scenarios from it per `AGENTS.md` §15.

### Scenarios TASK-115 Must Derive (reported, not authored here)

- Iron Fang cast, then the same Swap's player damage — the modifier is consumed
  by that damage (it is the owner's qualifying attack action), and the
  post-attack base Crit is unchanged.
- Iron Fang cast with no qualifying attack before End Turn — the modifier
  survives step 19a untouched and is still active several Turns later.
- Bạch Hổ's Passive triggering while Iron Fang's modifier is active — both are
  present as two distinct elements, both apply additively, and both are removed
  by one qualifying attack.
- Iron Fang cast twice before any qualifying attack — one Iron Fang element
  (refresh), not two.
- A non-damaging action between the grant and the consuming attack — does not
  consume.
- A Swap in which player damage, a Boss response, and a Burn tick all occur —
  the modifier is consumed once, at the player's qualifying instance, and the
  Burn tick and Boss attack do not consume it while still participating in
  Effective Crit.
- A Relic Crit source active alongside a `NextAttack` source — both compose,
  and consuming the `NextAttack` source leaves the Relic contribution intact.
- Composition at the cap — contributions summing above 100 compose to exactly
  100, and the roll still reads V ∈ [0,100).
- `Crit` base value preserved across a consumption — never reassigned to the
  configured default.

### Key Edge Cases

- Crit stat 0 and the composed value at exactly 100, against the item 2 roll.
- Determinism: identical state and seed produce identical identities,
  composition, and consumption.
- Remove one source from a three-source state and confirm the other two and the
  base are byte-identical afterwards.
- Round-trip a `BattleState` with an empty collection and with two elements,
  confirming order preservation and no `null`/omitted collapse.

---

## Stop Conditions

<!--
  Universal stop conditions in AGENTS.md §20 and .ai/README.md §13 always apply.
-->

- **STOP** if TASK-116's gameplay decision conflicts with existing
  authoritative architecture. Report both sources (file + section) per
  `AGENTS.md` §4 and do not silently redesign the gameplay decision.
- **STOP** if the minimum state shape cannot be determined.
- **STOP** if source identity cannot be represented deterministically.
- **STOP** if serialization ownership is ambiguous.
- **STOP** if Redis behavior is ambiguous.
- **STOP** if SignalR projection requirements are ambiguous.
- **STOP** if a new persistence schema would be required.
- **STOP** if a new gameplay decision would be required — the Product Owner's
  decision set is closed and no agent may extend it.
- **STOP** if `StatusEffects[]` would have to be redesigned beyond the TASK-116
  decision (i.e. if `§2.3.1` item 3's dichotomy would have to be widened or
  item 6 relaxed).
- **STOP** if a second in-flight state representation would be required.
- **STOP** if any work would modify `src/`, `tests/`, TASK-115, or TASK-116.
- **STOP** if an existing `Accepted` ADR would be contradicted
  (`.ai/workflow/architecture/architecture-change.md` §3) — that is a decision
  reversal, not a normal architecture change.

**None of the above fired.** The one classification finding worth recording is
that `DATABASE.md` §3 item 1 delegated the `NextAttack` rule to
`CARD_RULES.md` §4.1, which did not define it; that pointer is corrected here
to the section that now owns it. This was a stale delegation, not a
contradiction between two authoritative rules — no rule was in conflict, the
rule was absent.

---

## Completion Evidence

### ADR

```text
docs/03-decisions/ADR/ADR-017-nextattack-crit-modifiers-as-petstate-battle-state.md
Status: Accepted
```

ADR-017 records: Context (the gap, the existing defect, why the state model
could not carry the concept); Decision (12 numbered decisions); State Model
(the two-member schema and the source-scoped identity rationale); Ownership
(state / state contract / gameplay rule / per-source contribution / storage);
Lifecycle (Created → Active across Turns → Qualifying attack → Consumed, with
the no-expiry and no-cleanup properties stated as contract); Crit Composition
(additive, capped at 100, base never overwritten, the stat range kept separate
from the roll bound, pipeline and RNG preserved); Consumption Boundary (with
the two "must not reinterpret" consequences); Serialization Impact (a
per-surface table); Alternatives Considered (five options, each rejected with
its authoritative reason); Consequences (positive, negative, trade-offs).

### Documentation Changed

```text
docs/03-decisions/README.md
    §7 index row for ADR-017; version note 1.7 → 1.8.

docs/02-technical/GAME_STATE.md
    version 2.10 → 2.11;
    §2.3 `PetState` tree — `NextAttackCritModifiers[]` added, `Crit` annotated
      as the permanent/base value;
    §2.3.4 (NEW) — instance schema: the two members, source-scoped identity,
      the separate-from-`StatusEffects[]` boundary, the always-present
      collection, non-semantic ordering, wire/Redis non-membership, the
      base-value and `DefaultCrit` rules, and the `BossState` non-change;
    §5.1.2 (NEW) — lifecycle: create-or-refresh by identity, no step-19a
      participation, no expiry or cleanup, source-specific removal, one
      write-back, rejected-action inertness.

docs/01-game-design/COMBAT_RULES.md
    version 1.6 → 1.7;
    §1.1 — the `Crit` row gains its **newly authored** 0–100
      percentage-point range, flagged as new (TASK-116 D-4.4) and kept
      distinct from the roll bound; note that the stat is the permanent/base
      value and temporary modifiers never write it;
    §3.3 item 2 — the roll now compares against the composed Effective Crit;
    §3.3 item 5 — points at item 7 for how the sources combine;
    §3.3 item 7 (NEW) — Effective Crit composition, additivity, the 100 cap,
      the unchanged roll, and canonical ownership;
    §3.3 item 8 (NEW) — the qualifying attack, the consumption boundary, the
      no-Turn-expiry lifetime, and the consumption point;
    §3.3 item 9 (NEW) — source-specific removal;
    §3.3 item 10 (NEW) — multiple simultaneous sources, the Iron Fang × Bạch
      Hổ worked example, and the `DefaultCrit` prohibition;
    §3.3 item 11 (NEW) — the boundary against the Status Effect model.

docs/01-game-design/CARD_RULES.md
    version 1.5 → 1.6;
    §4.1 — a cross-reference paragraph stating that the `NextAttack` scope's
      runtime meaning is owned by `COMBAT_RULES.md` §3.3 items 7–10 and held
      in `GAME_STATE.md` §2.3.4, plus the cast site; the stale
      `COMBAT_RULES.md` §3.3 item-3 citation in the independence statement
      corrected to item 5. Iron Fang's values are unchanged and not restated.

docs/01-game-design/PASSIVE_RULES.md
    version 1.1 → 1.2;
    §8 — a cross-reference block recording that Bạch Hổ's increase is a
      `NextAttack` Crit modifier whose rules live at `COMBAT_RULES.md` §3.3
      items 7–10 / `GAME_STATE.md` §2.3.4, that its trigger is unchanged, that
      it is an independent source from Iron Fang's, and that the numeric
      value remains a config balance value. The Passive table, thresholds,
      trigger types, reset behaviors, and `PassiveId` values are unchanged.

docs/02-technical/REDIS_STATE.md
    version 1.7 → 1.8;
    §7 item 13 (NEW) — no key, no Redis-only field, no persistence work;
      empty-array round trip; unchanged lifecycle and concurrency;
    §7 item 14 (NEW) — no SignalR/wire consequence reaches this document.

docs/02-technical/SIGNALR_PROTOCOL.md
    version 2.7 → 2.8;
    §4 item 14 (NEW) — `NextAttackCritModifiers[]` is not delivered, item 13's
      `PetState` exception is not the precedent for it, and the client must not
      infer it.

docs/02-technical/DATABASE.md
    version 1.24 → 1.25;
    §3 item 1 — the `scope` delegation pointer re-pointed from
      `CARD_RULES.md` §4.1 to `COMBAT_RULES.md` §3.3 items 7–10 (with
      `GAME_STATE.md` §2.3.4 as the state owner, and `CARD_RULES.md` §4.1 /
      `PASSIVE_RULES.md` §7/§8 recorded as owning each source's own
      contribution). Storage semantics are unchanged: `scope` remains a
      storage member and exactly one scope value is still defined.
```

**Duplication check.** The composition rule, the cap, and the consumption
boundary are stated **once**, in `COMBAT_RULES.md` §3.3 items 7–10. The state
shape and the mutation lifecycle are stated **once**, in `GAME_STATE.md`
§2.3.4/§5.1.2. `CARD_RULES.md` §4.1, `PASSIVE_RULES.md` §8, `DATABASE.md` §3,
`REDIS_STATE.md` §7, and `SIGNALR_PROTOCOL.md` §4 all reference rather than
restate (`documentation-change.md` §2/§3). Verified by re-reading each
canonical section against its referencing sections.

### Changed Files

- `docs/03-decisions/ADR/ADR-017-nextattack-crit-modifiers-as-petstate-battle-state.md`
  — created; the architecture decision.
- `docs/03-decisions/README.md` — §7 index row + version note.
- `docs/02-technical/GAME_STATE.md` — §2.3 tree, §2.3.4 (new), §5.1.2 (new).
- `docs/01-game-design/COMBAT_RULES.md` — §1.1, §3.3 items 2/5, items 7–11 (new).
- `docs/01-game-design/CARD_RULES.md` — §4.1 cross-reference.
- `docs/01-game-design/PASSIVE_RULES.md` — §8 cross-reference.
- `docs/02-technical/REDIS_STATE.md` — §7 items 13–14 (new).
- `docs/02-technical/SIGNALR_PROTOCOL.md` — §4 item 14 (new).
- `docs/02-technical/DATABASE.md` — §3 item 1 delegation pointer.

### Formal Review Pass (quality/review.md & core/completion.md)

- **Correctness:** PASS — ADR-017 and canonical documentation changes match TASK-116 approved decisions C-1–C-10. All state structures, lifetimes, and consumption boundaries accurately documented.
- **Architecture:** PASS — Conforms to `docs/02-technical/ARCHITECTURE.md` and `AGENTS.md` §18. ADR-017 authored and indexed following the standard template.
- **Scope:** PASS — Confined strictly to ADR-017 and owning documentation edits. 0 files modified under `src/` or `tests/`. TASK-115/116 byte-identical.
- **Tests:** PASS (N/A) — Architecture/documentation task; no code changes.
- **Documentation:** PASS — Documentation consistency verified; all canonical owner documents updated with single sources of truth and proper cross-references.
- **Security:** PASS — No security or authentication implications.
- **Performance:** PASS — State model additions maintain Redis serialization invariants without hot-path regressions.
- **Maintainability:** PASS — No unnecessary abstractions or queues introduced.
- **Determinism (Gameplay/Battle Logic):** PASS — Server authority and deterministic additive Crit evaluation preserved.
- **Definition of Done (core/completion.md §1):** PASS — All completion criteria satisfied for an architecture task.

### Validation Results

- `quality/review.md` §1 (documentation-applicable items) — PASS
- ADR numbering confirmed from the repository, not assumed — VERIFIED
  (highest existing = ADR-016)
- Cross-reference integrity across the nine changed documents re-read together
  — VERIFIED, no duplicate definition introduced
- Zero files under `src/` or `tests/` changed — VERIFIED (`git status`)
- TASK-115 byte-identical and still `BACKLOG` — VERIFIED (`git status` clean
  for that path)
- TASK-116 byte-identical (its working-tree change predates this task and is
  not this task's edit) — VERIFIED

### ADR & Workflow Impact

- [x] Confirmed the change affects the authoritative-state model, so
      `AGENTS.md` §18 requires an ADR and `TASK_TYPES.md` makes this an
      `ARCHITECTURE` task
- [x] Checked whether the change makes an existing `Accepted` ADR no longer
      true — **no**. ADR-001, ADR-005, ADR-009, ADR-010, ADR-011, and ADR-014
      are all consistent with and unaffected by this decision; none is
      superseded, so `architecture/adr-change.md` §3 does not apply
- [x] ADR authored: **ADR-017**
- [x] `docs/03-decisions/README.md` §7 index updated
- [x] `docs/03-decisions/README.md` §8 ("Known Open Items") re-checked — the
      NextAttack representation gap is now **closed** by ADR-017 and was never
      listed there, so no §8 entry is added or removed
- [x] TASK-115 no longer requires a further architecture or documentation
      prerequisite

### Boundary Verification

- [x] Confirmed zero files under `src/` modified
- [x] Confirmed zero files under `tests/` modified
- [x] Confirmed no gameplay implemented and no balance value authored (Crit's
      cap is recorded as TASK-116's newly authored value, explicitly flagged)
- [x] Confirmed no migration created and no PostgreSQL persistence added
- [x] Confirmed no Redis code and no new Redis key
- [x] Confirmed no SignalR code, method, message, or payload member added
- [x] Confirmed no new Battle Event and no second RNG stream
- [x] Confirmed `StatusEffects[]` was not redesigned: `GAME_STATE.md` §2.3.1
      items 3 and 6 are untouched
- [x] Confirmed no parallel in-flight state representation was introduced
- [x] Confirmed TASK-115 and TASK-116 are byte-identical
