# TASK-161 — Apply the TASK-160 Pet `StatusEffects[]` / Boss Live-HP Decisions to the Authoritative Documentation

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  PROVENANCE: TASK-159 was created as a DECISION-FIRST task and is BLOCKED
  (AGENTS.md §7 / §18). It recorded that the Pet/Boss status-effect and live
  Boss-HP projection is unreachable by the client for want of an authored
  contract. TASK-160 then recorded the Product Owner's decisions (D-1A, D-2A,
  D-3 = `BattleStateUpdated`, D-4 = required for MVP, D-5/D-6 = no new SignalR
  method or event) and explicitly left the exact wire contract — member names,
  types, optionality, absence convention, serialization details — to a
  DOWNSTREAM documentation-resolution task. This is that task.

  THIS TASK DECIDES NOTHING. Every value it writes is either (a) TASK-160's
  recorded Product Owner decision, or (b) an element already owned by an
  existing authoritative section that this task references rather than
  invents. Where the repository's existing conventions did not determine a
  semantic, this task STOPPED instead of choosing one (see §"Stop-Condition
  Check" — none fired; every question was determined).

  BOUNDARY: documentation only. Zero files under src/ or tests/. TASK-159 and
  TASK-160 are immutable and were not modified. This task implements nothing.
-->

---

## Metadata

```text
Task ID:           TASK-161
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md)
Status:            DONE
Risk:              HIGH (TASK_TYPES.md §4 classifies DOCUMENTATION as
                   LOW–MEDIUM and notes MEDIUM "if it affects a cross-referenced
                   contract"; classified HIGH because this change widens the
                   SignalR wire projection, which is a protocol contract and a
                   state-delivery boundary, and is cross-referenced by
                   GAME_STATE.md, BOSS_RULES.md, GAME_EVENTS.md, REDIS_STATE.md
                   and multiple ADRs. The same level TASK-159 recorded for the
                   gated work. Per §4's tie-break, classify at the higher level.)
Priority:          HIGH (it is the sole unblocking artifact for TASK-159, and
                   both projected facts are documented MVP IN items —
                   MVP_SCOPE.md §1 "Combat: … Status Effects, Damage" and
                   "Bosses: Element, Passive, Skill per Boss")
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md —
                   "Documentation change: Primary Agent Review"; this task
                   authors a contract from a recorded decision and does not
                   decide any value, which is the Review Agent's posture)
Supporting Agents: realtime (SIGNALR_PROTOCOL.md §4 is the owner of the wire
                   projection this task widens), gameplay (GAME_STATE.md §2.3.1
                   / §2.4 and BOSS_RULES.md §6.2.4 are the owning documents for
                   the state shape and the gameplay-visibility constraint;
                   consulted for content accuracy, not to author the answer)
Workflow:          documentation/documentation-change.md
Skills:            realtime/realtime-protocol-validation,
                   discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (5 skills — Complex budget, tasks/README.md §12)
Dependencies:      TASK-160 (the recorded Product Owner decisions D-1A/D-2A and
                     the D-3…D-6 coverage — this task's INPUT; IMMUTABLE,
                     read-only, NOT modified),
                   TASK-159 (BLOCKED — the task this documentation unblocks;
                     IMMUTABLE, read-only, NOT modified, NOT re-statused),
                   TASK-095 / TASK-096 (DONE — the StatusEffect domain state and
                     serializer round trip the projection carries; immutable),
                   TASK-143 / TASK-144 (DONE — reconnect/resync; the §7
                     snapshot path this task confirms carries the same
                     projection; immutable)
Blocks:            TASK-159 (unblocked by this task's authored contract).
                   Rendering the active Pet's Status Effects and live Boss HP
                   in `BattleScene` both remain unreachable until TASK-159's
                   implementation lands.
Estimate:          Complex (5 skills; three docs/, one task file; no code, no
                   tests)
```

**Type classification note.** `DOCUMENTATION`, not `ARCHITECTURE` and not
`GAMEPLAY-CHANGE`. No gameplay rule changed: the effects, magnitudes,
durations, triggers, and targets in `BOSS_RULES.md` §6.2/§6.3 are
byte-identical, and no `StatusEffect` member, value, or `Type` was added. No
architectural decision changed either: `SIGNALR_PROTOCOL.md` §4 item 11's
single state-push method, the Redis record, the storage strategy, and the
authoritative model are all untouched, and the transport is still SignalR
(`ADR-004`). What changed is the *content* of an existing projection, which is
exactly `TASK_TYPES.md` §2's DOCUMENTATION definition, and which
`SIGNALR_PROTOCOL.md`'s own revision history already treats as a protocol
revision rather than an ADR (`TASK-150` / v2.14 is the precedent).

---

## Objective

Author the exact, implementation-ready `BattleStateUpdated` projection contract
the TASK-160 Product Owner decisions authorize — the active Pet's active
`StatusEffects[]` and the Boss's `HP`/`MaxHP` — into the authoritative
documents that own each part of it, so that TASK-159's implementation agent can
determine every member's source, name, type, optionality, absence convention
and serialization rule without inventing one.

---

## Authoritative References

<!-- Cited by path and section. No rule, formula, schema, or wire shape is copied. -->

**The decision this task applies:**

- `tasks/backlog/TASK-160-collect-product-owner-decisions-pet-statuseffects-and-boss-live-hp-projection.md`
  — its **D-1** `Decision:` block (D-1A: the active Pet's active
  `StatusEffects[]` MUST be delivered through the existing `BattleStateUpdated`
  path; limited to the active Pet's currently active instances; the client may
  render them; required for MVP client synchronization) and its **D-2**
  `Decision:` block (D-2A: Boss live `HP`/`MaxHP` delivered through
  `BattleStateUpdated`; do NOT expose the entire `BossState`; explicit
  exclusions listed; required for MVP). This is the task's **INPUT**, recorded
  by the Product Owner; nothing in it is re-interpreted here. **IMMUTABLE.**

**The wire owner this task edits:**

- `docs/02-technical/SIGNALR_PROTOCOL.md` **§0** (the delivery shapes and their
  non-interchangeability), **§3.2.3** (property casing — camelCase, explicitly
  named, never a serializer default), **§3.2.4** (enum representation — a
  string carrying the contract name, never an ordinal), **§3.2.5**
  (optionality — omitted, never explicit `null`), **§4** (the state push,
  including **item 4**'s staged-fields rule, **item 11**'s single state-push
  method, **item 12**'s exclusion precedent, **item 13**'s `PassiveProgress`
  delivery precedent, and **items 14–16**'s sibling-collection declines),
  **§4.2 item 2** (`playerState` fixed at two members), **§4.3** (the
  `petState` projection; **item 2**'s member-set rule), **§7.1** (the
  reconnect snapshot), **§8 items 5/7** (no message where extending the state
  is the mechanism)
- `docs/02-technical/GAME_STATE.md` **§0 item 5** (one representation per
  fact), **§2.3** (`PetState`), **§2.3.1** (the `StatusEffects[]` instance
  schema; item 7 absence conventions; item 10 non-semantic ordering),
  **§2.3.2** (the collection's JSON serialization, element shape, and
  round-trip obligation), **§2.3.3** (what the model does not add — incl. the
  no-`PendingStatusEffects[]` prohibition), **§2.3.4/§2.3.5/§2.3.7** (the
  sibling collections), **§2.4** (`BossState`), **§2.4.1** (staged BossState
  fields), **§5.1** (the single post-resolution write-back), **§5.1.1** (the
  `StatusEffects[]` lifecycle and its idempotency/determinism rules)
- `docs/01-game-design/BOSS_RULES.md` **§5** (Boss State and server
  authority), **§6.2** (the Boss Passive rows), **§6.2.1–§6.2.4** (the three
  effects and the client-visibility statement), **§6.4** (the identity
  contract), **§7** (events), **§8** (server authority)

**The contracts this change must not disturb:**

- `docs/02-technical/GAME_EVENTS.md` **§1**/**§1.1** (the event ordering) and
  **§2** (each event's meaning and payload; no event carries a Status Effect
  collection), **§3 items 5–7** (delivery, state-vs-event, and emission
  staging) — no event is added or changed by this task
- `docs/02-technical/REDIS_STATE.md` **§2 item 1** (the record matches
  `GAME_STATE.md` §2 exactly, no Redis-only fields), **§3** (TTL/lifecycle),
  **§4** (the `Sequence` compare-and-set), **§6 item 1** (no field-by-field
  Redis schema), **§7 item 9** (round-trip losslessness) — no storage contract
  changes
- `docs/02-technical/API_CONTRACTS.md` **§3** (`POST /api/battle/start`'s
  `initialState` summary; its `petState`/`bossState` objects are a **different**
  projection for a **different** path), **§5.1** (the Element wire value set,
  scoped to that document's REST surface)
- `docs/01-game-design/COMBAT_RULES.md` **§4 item 7**, **§5.1–§5.5** (the
  Status Effect rules the delivered instances describe), `PASSIVE_RULES.md`
  **§6** (the sole pre-existing UI-visibility requirement, which authorized
  `PassiveProgress`), `GAME_RULES.md` **§16/§17/§18**
- `docs/00-overview/MVP_SCOPE.md` **§1** ("Combat: … Status Effects, Damage";
  "Bosses: Element, Passive, Skill per Boss"), **§2**, **§4**
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`,
  `ADR-004-signalr-realtime.md`, `ADR-008-battle-recovery.md`,
  `ADR-011-player-pet-role-model.md`, `ADR-014-battle-state-player-identity.md`
  — checked for conflict; **none modified**

**Governing contract and precedent:**

- `AGENTS.md` **§2** (precedence), **§4** (never silently resolve a conflict),
  **§7** (invent nothing), **§10** (server authority), **§16** (task
  discipline), **§17** (documentation change rule), **§18** (architecture
  change rule), **§20** (stop conditions), **§21** (output discipline)
- `.ai/README.md` **§8** (an agent is not a Product Owner), **§13** (stop
  condition format), **§18** (documentation-update classification)
- `.ai/workflow/documentation/documentation-change.md` **§1** (flow), **§2**
  (no duplication — the governing rule for this task's shape), **§3**
  (canonical owner), **§4** (review + completion)
- `tasks/README.md` **§9** (no business-rule duplication), **§12** (skill
  budget)
- `tasks/completed/TASK-151-apply-powerchanged-source-decisions-to-authoritative-documentation.md`
  and `tasks/completed/TASK-152-implement-authoritative-powerchanged-emission.md`
  — the precedent pair for "apply a TASK-150-style Product Owner decision to
  the wire owner, then implement it downstream"
- `tasks/completed/TASK-157-apply-thuy-ma-buffdebuff-targetstat-invariant-relaxation-to-game-state.md`
  — the closest structural precedent: a decision task whose downstream task
  edits `GAME_STATE.md` and states what did **not** change
- `tasks/backlog/TASK-159-*.md` — the BLOCKED task this unblocks. Read-only;
  **not modified.**

---

## Current Contract (before this task)

Stated as citations only (`tasks/README.md` §9 — no rule or schema is restated
here):

```text
SIGNALR_PROTOCOL.md §4.3 item 2   `petState` fixed at FOUR members and
                                  `StatusEffects[]` named among the §2.3
                                  members explicitly NOT delivered; closing
                                  sentence: "Referring to `petState` as a whole
                                  does not widen that rule."
SIGNALR_PROTOCOL.md §4           the push carries eight members; defines no
                                  `bossState` member of any kind
SIGNALR_PROTOCOL.md §4 item 11   `BattleStateUpdated` is the only state push
GAME_STATE.md §2.3.1             "**Not a wire member.**" — `StatusEffects[]`
                                  is not part of any current wire payload, and
                                  "delivering these instances is a protocol
                                  change owned by its own task"
BOSS_RULES.md §6.2.4             the non-exposure of `BossState` is "an
                                  intentional, recorded contract limitation";
                                  client-visible Boss HP or Boss StatusEffects
                                  is "a separate future protocol decision"
```

**Two of those statements were the TASK-159 block.** TASK-160 has now made the
decision both deferred to; this task records the outcome at each owner.

---

## Scope

### In Scope

1. Widen the existing `BattleStateUpdated` projection by exactly the two
   authorized facts, at its canonical owner (`SIGNALR_PROTOCOL.md` §4).
2. Fix the exact member name, type, optionality, absence convention and
   serialization source for each — deriving each from an existing authoritative
   section rather than choosing one.
3. Resolve the `GAME_STATE.md` §2.3.1 contradiction by stating authoritative
   membership and wire membership separately, at their respective owners.
4. Reconcile `BOSS_RULES.md` §6.2.4's deferral with the now-made D-2A decision
   by the smallest correction.
5. Confirm no new SignalR method, event, subscription, persistence semantic,
   Redis key, or database column is introduced.

### Out of Scope

- **Any implementation.** Zero files under `src/` are modified. The projection
  is not written into `BattleHub`, and the client is not changed.
- **Any test change.** Zero files under `tests/` are modified.
- **TASK-159.** Not edited, moved, re-statused, re-scoped, or implemented.
- **TASK-160.** Not edited — it is the immutable decision record this task
  consumes.
- **Any other completed task.** `tasks/completed/` is unmodified.
- **Boss `StatusEffects[]`.** D-2A excluded it; this task does not authorize it.
- **The whole `BossState`.** D-2A excluded every member but `HP`/`MaxHP`.
- **The sibling `PetState` collections.** `NextAttackCritModifiers[]`,
  `CardCostModifiers[]`, and `ATKModifiers[]` keep their existing declines;
  TASK-160's scope note forbade broadening into them.
- **`POST /api/battle/start`'s `initialState`.** It is a REST summary with its
  own shape (`API_CONTRACTS.md` §3) and is not the §4 push; this task does not
  align, widen, or re-scope it.
- **Non-MVP or out-of-scope items** — any item listed as OUT in
  `MVP_SCOPE.md` §2, and anything not listed in §1 (treat unlisted as FUTURE
  per §4).

---

## Exact Wire Contract (this task's deliverable)

Derived per §"Derivation of Each Semantic" below. Every line cites the section
that owns it.

```text
Pet StatusEffects
  Carrier     `petState.statusEffects` — a member of the EXISTING §4.3
              `petState` object, on the EXISTING `BattleStateUpdated` push
  Name        `statusEffects` (§3.2.3 item 1 camelCase; the name §2.3.2 item 1
              already fixes for the serialized collection)
  Type        array of Status Effect instance objects
  Element     the element shape §2.3.2 item 3 fixes for the serialized
              collection — `id`, `type`, `source`, `magnitude` (required),
              `targetStat`, `remainingTurns`, `expiryCondition` (optional,
              absent-when-inapplicable per §2.3.1 item 7). Referenced, not
              restated on the wire side.
  Scope       the ACTIVE Pet only (§4.3's subject; D-1A)
  Presence    ALWAYS present. An active Pet with no active effect is sent `[]`.
              Never omitted, never `null` (§2.3.2 item 1, carried to the wire).
  Optionality §3.2.5 (omitted, never explicit `null`) governs the OPTIONAL
              MEMBERS WITHIN an element, not the collection.
  Order       not semantic (§2.3.1 item 10); delivered in state order, which
              the round trip preserves (§2.3.2 item 6)
  Client      renders only; applies/refreshes/consumes/expires nothing, and
              re-derives the collection from nothing

Boss HP / Boss MaxHP
  Carrier     `bossState` — a NEW nested object on the EXISTING
              `BattleStateUpdated` push
  Members     `hp` and `maxHp` — exactly two (§2.4 `BossState.HP`/`MaxHP`)
  Name        camelCase per §3.2.3 item 1; `maxHp` matches the casing the
              existing REST battle-start summary already uses for the same
              state member (`API_CONTRACTS.md` §3)
  Type        integer, both (§2.4; `COMBAT_RULES.md` §1.2 / `BOSS_RULES.md` §6.1)
  Presence    BOTH always present. Never omitted, never `null`, never optional.
              `hp = 0` is a real published value sent as `0` (§4.2 item 3's /
              §4.3 item 4's always-present convention).
  Absence     There is NO "Boss unavailable" or terminal variant to represent:
              a battle always has its one Boss, present from creation at full
              health (§2.4, §2.4.1). A terminal battle is expressed by the
              existing `BattleWon`/`BattleLost` events and their terminal
              `finalBossHp` (§3.2.19), and a removed record by the existing
              §7 item 3 outcome — both unchanged.
  Excluded    `BossId`/Identity, `Element`, `ATK`, `DEF`, `State`, `PassiveId`,
              `PassiveProgress`, `SkillCharge`, `SkillCooldown`,
              `StatusEffects[]` — exhaustively, per D-2A and §2.4

Carrier (both)  the EXISTING `BattleStateUpdated` (§4 item 11), on its EXISTING
                trigger — join (§4.1 item 1) and every committed Swap's
                resolved-state push (§2.1, §5). The §7 reconnect snapshot
                carries the same projection (§7.1).
New SignalR method / event  NONE.
```

---

## Derivation of Each Semantic

The task's instruction was to determine each value from the repository's
existing conventions rather than invent one. Each answer below cites the
section that already answered it.

| Question | Answer | Determined by |
|---|---|---|
| Pet collection's member name | `statusEffects` | `GAME_STATE.md` §2.3.2 item 1 fixes the serialized collection's member name as `statusEffects`; §2.3.2 item 2 records that *spelling* is the serializer's while *existence/type/meaning* are the state's. `SIGNALR_PROTOCOL.md` §3.2.3 item 1 requires camelCase and §3.2.3 item 2 requires the contract to state it explicitly. `equippedCards`/`passiveId` already use the state document's own member names on this same object. |
| Pet element's shape | `GAME_STATE.md` §2.3.2 item 3's seven members | §2.3.2 item 3 tabulates the element's exact member set, types and requiredness for serialization. Referenced, not restated. |
| Pet collection's type | array | §2.3.2 item 1: "serializes as a JSON array of instance objects". |
| Pet empty case | `[]`, always present | §2.3.2 item 1: "An entity with no active effect serializes an **empty array** — the collection always exists (§0 item 4), so it is never omitted and never `null`." |
| Pet absence convention for *element* members | omitted, never `null` | §2.3.1 item 7 (state side) and `SIGNALR_PROTOCOL.md` §3.2.5 (wire side) already agree. |
| Where the collection rides | `petState`, not a new top-level member | §4.3 already delivers the active Pet's state under `petState`; `petState` is the active Pet's own object, so the collection has no other subject. A new top-level member would add a second subject for one entity. |
| Boss member name | `bossState` | The state tree's own member name (`GAME_STATE.md` §2.4 nests `BossState` under `BattleState`); `petState`/`playerState` already project state-tree subtree names to the wire. The runtime persistence mapping already spells the same subtree `bossState` (`BattleStateJsonNames`), so the name is not new coinage. |
| Boss members | `hp`, `maxHp` | `GAME_STATE.md` §2.4's `HP`/`MaxHP`; `maxHp` matches the casing `API_CONTRACTS.md` §3's existing `bossState.maxHp` already uses for the same state member. |
| Boss presence | both always present | §2.4/§2.4.1: the Boss exists from battle creation with `HP = MaxHP`; there is no absent case (contrast `LastCommittedSwapPair`, §2.1.10 item 3). §4.2 item 3 and §4.3 item 4 fix the always-present convention for a member with no "not yet" state. |
| Boss type | integer | §2.4 (`HP`/`MaxHP` are `int` members; `BOSS_RULES.md` §6.1's values are integers). |
| Ordering | not semantic, preserved | §2.3.1 item 10. |
| New method / event | none | §4 item 11 + §8 items 5/7; D-5/D-6. |

**Every question the task listed as needing a deterministic answer was
determined by an existing section. No semantic had to be invented, and no
STOP condition fired** (see §"Stop-Condition Check").

---

## Acceptance Criteria

- [x] `BattleStateUpdated` remains the sole carrier and the only state-push
      method; no new SignalR method, event, batch, or transport exists
      (`SIGNALR_PROTOCOL.md` §4 item 11, §2, §8 items 5/7)
- [x] All pre-existing payload members and their semantics are preserved
      byte-for-byte in meaning (`battleId`, `turn`, `sequence`, `board`,
      `rngSeed`, `rngState`, `playerState`, `petState`)
- [x] The active Pet's active `StatusEffects[]` is authorized and its member
      name, type, presence, absence, ordering and client boundary are
      deterministic (§4.3 item 14)
- [x] Boss `HP` and `MaxHP` are authorized and deterministic, and **only** those
      two (§4.4)
- [x] Every other `BossState` member remains excluded and explicitly listed
      (§4.4 item 3, `GAME_STATE.md` §2.4)
- [x] Boss `StatusEffects[]` is **not** authorized (§4.4 item 3, §4 item 17,
      `BOSS_RULES.md` §6.2.4)
- [x] No contradiction remains between `GAME_STATE.md` and
      `SIGNALR_PROTOCOL.md` on `StatusEffects[]`: the membership/delivery split
      is stated and both owners agree (`GAME_STATE.md` §2.3.1, §2.4)
- [x] `BOSS_RULES.md` §6.2.4 no longer says client-visible Boss HP awaits a
      future decision; it records the outcome
- [x] The sibling `PetState` collections' declines are unchanged and unaffected
      (§4 items 14–16, `GAME_STATE.md` §2.3.4/§2.3.5/§2.3.7 item 10)
- [x] No new persistence semantic, Redis key, Redis-only field, or database
      column exists (`REDIS_STATE.md` §2 item 1, §7 item 9 unchanged; no ADR
      created; `DATABASE.md` untouched)
- [x] No `PendingStatusEffects[]`, queued state, second in-flight
      representation, or duplicate `StatusEffects` storage is introduced
- [x] No new `StatusEffect` `Type`, `TargetStat` value, member, or magnitude
- [x] No gameplay rule, magnitude, duration, trigger, or target changed
- [x] **TASK-160 is byte-identical** (not modified)
- [x] **TASK-159 is byte-identical** — not modified, moved, re-statused, or
      implemented
- [x] Zero files under `src/` or `tests/` modified
- [x] TASK-159's nine unblock items are each determinable (§"TASK-159 Unblock
      Check")

---

## Affected Files & Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api)   — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state)   — NONE
[ ] tests/ (unit / integration / gameplay scenarios)             — NONE
[x] docs/02-technical/SIGNALR_PROTOCOL.md (§4 push; §4 item 4, items 14/17;
                                          §4.3 items 2/14; §4.4 new; §7;
                                          §8 item 9; version header)
[x] docs/02-technical/GAME_STATE.md       (§2.3 delivery pointer; §2.3.1 wire
                                          note → membership/delivery split;
                                          §2.3's combat-stats note; §2.4 Boss
                                          visibility paragraph; version header)
[x] docs/01-game-design/BOSS_RULES.md     (§6.2.4; version header)
[ ] docs/02-technical/GAME_EVENTS.md      — NONE (no event added or changed)
[ ] docs/02-technical/REDIS_STATE.md      — NONE (no storage contract change)
[ ] docs/02-technical/API_CONTRACTS.md    — NONE (different path, untouched)
[ ] docs/03-decisions/ADR/                — NONE (no ADR created or modified)
[x] tasks/backlog/TASK-161-<this file>.md — this task file only
[ ] tasks/backlog/TASK-159-*.md           — NONE (immutable)
[ ] tasks/backlog/TASK-160-*.md           — NONE (immutable)
[ ] tasks/completed/                      — NONE (immutable)
```

---

## Implementation Notes

- **The canonical owners were respected.** `GAME_STATE.md` owns the
  `BattleState` shape; `SIGNALR_PROTOCOL.md` owns the wire projection;
  `BOSS_RULES.md` owns the gameplay-visibility constraint. Each statement was
  written once, at its owner, and referenced elsewhere
  (`documentation/documentation-change.md` §2).
- **The contradiction was resolved by separating two facts, not by deleting
  one.** `GAME_STATE.md` §2.3.1 previously said `StatusEffects[]` "is not part
  of any current wire payload". That statement was *true of the contract at the
  time* and is now *stale in one direction only* — the collection is still
  authoritative state and is now also wire-delivered **for the active Pet**.
  The section therefore states authoritative membership and wire membership as
  two columns, which is the smallest correction that removes the contradiction
  without deleting the Boss-side exclusion (which remains true).
- **§4.3 item 2's exclusion list was re-worded, not emptied.** Only
  `StatusEffects[]` was removed from it; every other excluded member is named
  and explicitly remains excluded, so the sentence that closes the projection
  ("Referring to `petState` as a whole does not widen that rule") keeps its
  force over everything it still lists.
- **The always-present-versus-omitted distinction is stated explicitly**,
  because it is the one place where two existing conventions meet:
  `SIGNALR_PROTOCOL.md` §3.2.5 says "omitted, never explicit `null`", while
  `GAME_STATE.md` §2.3.2 item 1 says the collection "is never omitted".
  §4.3 item 14 and §4.4 item 4 resolve it by scope — §3.2.5 governs a member
  that does not apply, and neither of these members has a non-applicable case.
  This is the same resolution §4.2 item 3 already applies to
  `combo`/`matchCount` and §4.3 item 4 to `current`.
- **`maxHp`'s casing was taken, not chosen.** `API_CONTRACTS.md` §3's existing
  `bossState.maxHp` is the repository's own established spelling for that state
  member on a client-facing payload.
- **The §4 push's member list in prose was updated** (`§4`'s signature block and
  its staged list) so the document does not contradict its own prose.
- **`BOSS_RULES.md` states the constraint, not the wire contract.** Its §6.2.4
  names the delivered pair and points at `SIGNALR_PROTOCOL.md` §4.4 for the
  member rules, avoiding the duplication `documentation-change.md` §2 forbids.
- **Report, do not fix, adjacent gaps** (`AGENTS.md` §16). Items observed but
  left untouched are listed under §"Remaining Issues".

---

## Testing Requirements

### Required Verification

```text
[x] Documentation consistency audit — the recorded contract re-read against
                                      SIGNALR_PROTOCOL.md §0/§3.2.3/§3.2.4/
                                      §3.2.5/§4/§4.2/§4.3/§7/§8,
                                      GAME_STATE.md §0/§2.3/§2.3.1/§2.3.2/
                                      §2.3.3/§2.4/§2.4.1/§5.1/§5.1.1,
                                      BOSS_RULES.md §5/§6.2/§6.2.4/§8,
                                      GAME_EVENTS.md §1/§2/§3,
                                      REDIS_STATE.md §2/§4/§7,
                                      API_CONTRACTS.md §3/§5.1
[x] Duplication check              — each of the two projections has exactly one
                                     owner section; §4.3 item 14 and §4.4 carry
                                     the wire rules, GAME_STATE.md carries the
                                     state, BOSS_RULES.md carries the
                                     visibility constraint, and none restates
                                     another's content
[x] Cross-reference resolution     — every `DOC.md §N` cited by the new text
                                     resolves to a section that exists and says
                                     what the citation claims
[x] Single-carrier check           — `BattleStateUpdated` is still the only
                                     state-push method; §2's method list is
                                     unchanged at three gameplay methods
[x] No-new-contract check          — no new method, event, subscription,
                                     transport, payload member outside the two
                                     authorized projections, Redis key, or
                                     database column
[x] Isolation verification         — src/ (0 files), tests/ (0 files),
                                     TASK-160 byte-identical, TASK-159
                                     byte-identical, tasks/completed/ unchanged,
                                     docs/03-decisions/ unchanged
[x] Unblock check                  — TASK-159's nine items each determinable
[x] Unit tests                     — N/A: this task changes no code
[x] Integration tests              — N/A: this task changes no code
[x] Gameplay scenarios             — N/A: the scenarios the contract implies
                                     belong to TASK-159's implementation
```

### Key Edge Cases

- **No active Pet Status Effects** — sent as `[]`, never omitted, never `null`
  (§4.3 item 14; the case `GAME_STATE.md` §2.3.2 item 1 already fixed for the
  state).
- **A Pet with a Status Effect whose optional members do not apply** — the
  element's optional members are omitted, not `null` (§2.3.1 item 7,
  `SIGNALR_PROTOCOL.md` §3.2.5).
- **A battle at creation** — `statusEffects` is `[]` (no effect has been
  applied) and `bossState` is `{ hp: MaxHP, maxHp: MaxHP }` (`GAME_STATE.md`
  §2.4/§2.4.1: the Boss starts at full health in its Initial State). Note the
  `Thủy Ma` Battle-Start instance is applied at creation
  (`BOSS_RULES.md` §6.2.2), so its collection is non-empty at creation for that
  Boss — the empty case is a *different* Boss's, not a general rule.
- **A terminal battle** — `hp = 0` is a real published value sent as `0`; the
  outcome is still expressed by `BattleWon`/`BattleLost` and `finalBossHp`, and
  no "terminal" variant of `bossState` is introduced (§4.4 item 6).
- **A Boss whose `MaxHP` is not its current `hp`** — both are reported
  independently and neither is re-derived from the other (§4.4 item 5).
- **A Boss-applied effect that sits on the Pet** (e.g. Hỏa Long's Burn,
  `BOSS_RULES.md` §6.3.1 item 1) — it appears in the **Pet's**
  `statusEffects[]`, because that is the collection delivered; `source` is
  `"boss"` inside the element. This does not make the Boss's collection
  delivered.
- **A Pet-held effect that a Boss-side rule consumes** (e.g. the Thủy Ma
  healing reduction is Boss-held and Pet-read, `BOSS_RULES.md` §6.2.2) — the
  Boss-held instance is **not** delivered; the read relationship is unchanged
  and is state-side only.
- **A reconnect mid-battle** — the §7 snapshot carries the same projection, so
  the recovered state matches the push (§7.1).
- **A `StatusEffects[]` element whose `Id` repeats** — impossible in a
  committed state (`GAME_STATE.md` §2.3.1 item 6); the wire adds no
  de-duplication of its own.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- If exact wire field naming could not be determined from repository
  conventions: STOP.
- If type/optionality/absence semantics were ambiguous: STOP.
- If `GAME_STATE.md` ownership conflicted with `SIGNALR_PROTOCOL.md`: STOP.
- If `BOSS_RULES.md` conflicted with the explicit D-2 decision: STOP.
- If implementing the contract required a new Product Owner decision: STOP.
- If a new SignalR method or event appeared necessary: STOP.
- If a new gameplay or architecture decision was required: STOP.
- If the task drifted into implementing TASK-159 or editing `src/`/`tests/`:
  STOP.
- If task exceeds 7 skills or crosses multiple uncoupled boundaries: STOP &
  decompose.

---

## Stop-Condition Check

**No stop condition fired.** Each is recorded as checked, with its evidence:

| Stop condition | Outcome | Evidence |
|---|---|---|
| Wire field naming undeterminable | **Did not fire** | `statusEffects` is fixed by `GAME_STATE.md` §2.3.2 item 1; `bossState`/`hp`/`maxHp` follow the state tree's own names plus the existing `API_CONTRACTS.md` §3 casing. See the derivation table. |
| Type/optionality/absence ambiguous | **Did not fire** | §2.3.2 item 1/2/3 fix type, presence and element shape; §3.2.5 plus §4.2 item 3 / §4.3 item 4 fix the always-present-versus-omitted scope. |
| `GAME_STATE` vs `SIGNALR_PROTOCOL` ownership conflict | **Did not fire** | The stated purposes divide cleanly: `GAME_STATE.md` answers "what state exists during a running battle?" and disclaims serialization; `SIGNALR_PROTOCOL.md` answers "how does realtime communication work?". The apparent conflict was a *stale statement*, not a contested owner, and it was resolved at its owner. |
| `BOSS_RULES.md` conflicts with D-2 | **Did not fire** | §6.2.4 deferred the question rather than answering it; D-2A answers it, and §6.2.4 was corrected to record the answer. No magnitude, rule, or trigger is affected. |
| New Product Owner decision required | **Did not fire** | D-1A/D-2A plus D-3…D-6 cover every question this task had to answer. |
| New SignalR method/event necessary | **Did not fire** | Both facts ride the existing push (§4 item 11, §8 items 5/7); D-6 states none is required. |
| New gameplay/architecture decision required | **Did not fire** | No rule changed; the transport, storage, and authoritative model are untouched, and `ADR-004`/`ADR-005`/`ADR-008` are consistent with the change. |

---

## TASK-159 Unblock Check

Re-read after the documentation update. Each item is determinable by an
implementation agent from the authoritative documents alone:

| # | Item TASK-159 needs | Where it is now determined |
|---|---|---|
| 1 | Pet `StatusEffects[]` source | `GAME_STATE.md` §2.3 `PetState.StatusEffects[]` (§2.3.1 schema, §5.1.1 lifecycle) — the active Pet's own collection. `SIGNALR_PROTOCOL.md` §4.3 item 14 ("Source"). |
| 2 | Pet `StatusEffects[]` wire representation | `SIGNALR_PROTOCOL.md` §4.3 item 14: `petState.statusEffects`, array, element shape by reference to `GAME_STATE.md` §2.3.2 item 3, always present as `[]` when empty. |
| 3 | Boss HP source | `GAME_STATE.md` §2.4 `BossState.HP`. `SIGNALR_PROTOCOL.md` §4.4 item 5. |
| 4 | Boss MaxHP source | `GAME_STATE.md` §2.4 `BossState.MaxHP`. `SIGNALR_PROTOCOL.md` §4.4 item 5. |
| 5 | Boss wire representation | `SIGNALR_PROTOCOL.md` §4.4: `bossState.{hp,maxHp}`, both `int`, both always present, exactly two members, all other `BossState` members excluded (item 3). |
| 6 | `BattleStateUpdated` carrier | `SIGNALR_PROTOCOL.md` §4 item 11 (the only state push), §4.1 item 1 (trigger), §4.3 item 1 / §4.4 item 1 (delivery on join and on every resolution push). |
| 7 | Serialization / absence semantics | Pet: §4.3 item 14 (always present; `[]` when none; element-level absence by omission). Boss: §4.4 item 4 (both always present; `hp = 0` sent as `0`; no absent case). Casing §3.2.3; enum representation §3.2.4. |
| 8 | MVP synchronization requirement | D-4 in TASK-160, recorded for both facts as "required for MVP client synchronization"; `GAME_STATE.md` §2.4 and `BOSS_RULES.md` §6.2.4 record the visibility constraint. |
| 9 | No new SignalR method/event required | `SIGNALR_PROTOCOL.md` §4 item 11, §8 item 9, §2 (three gameplay methods); D-6. |

**Result: UNBLOCKED.** All nine items are answered by an authoritative section
or by TASK-160's recorded decision. No item requires an agent to invent a
member, a shape, an optionality rule, or an absence convention.

---

## Completion Evidence

### Decision source

```text
TASK-160 — the Product Owner's recorded decisions.

D-1: the active Pet's active StatusEffects[] are delivered via
     BattleStateUpdated (D-1A).
D-2: Boss live HP and MaxHP are delivered via BattleStateUpdated (D-2A), and
     nothing else from BossState.

Carrier:                  the existing BattleStateUpdated.
New SignalR method/event: none.
```

### Changed Files

- `docs/02-technical/SIGNALR_PROTOCOL.md` — version header → 2.15;
  §4's signature block and staged field list gain `bossState`; **§4 item 4**
  states that several members are narrowed projections;
  **§4 item 17 (new)** records that `PetState.StatusEffects[]` is delivered and
  that the sibling declines are unaffected; **§4.3's diagram and item 2** move
  `petState` from four to five members and re-word the exclusion list;
  **§4.3 item 14 (new)** owns the Pet projection's name, type, presence,
  absence, ordering and client boundary; **§4.4 (new)** owns the `bossState`
  projection; §7 states the reconnect snapshot carries the same projection;
  **§8 item 9 (new)** records that no Status-Effect- or Boss-HP-specific message
  exists.
- `docs/02-technical/GAME_STATE.md` — version header → 2.19; §2.3's delivery
  pointer updated; §2.3's combat-stats note updated; **§2.3.1's "Not a wire
  member" note replaced** by the authoritative-membership / wire-membership
  split (the contradiction this task resolves); **§2.4 gains a paragraph**
  recording that exactly `HP`/`MaxHP` are client-visible and everything else,
  including the Boss's `StatusEffects[]`, is not.
- `docs/01-game-design/BOSS_RULES.md` — version header → 2.8; **§6.2.4**
  reconciled with D-2A by the smallest correction.
- `tasks/backlog/TASK-161-<this file>.md` — this task file.

### Validation Results

```text
Documentation consistency audit   PASS — see §"Validation Detail"; the two
                                  projections have one owner each, no statement
                                  is duplicated across owners, and every cited
                                  section resolves
Single-carrier check              PASS — BattleStateUpdated is still the only
                                  state push; §2's gameplay methods remain
                                  exactly three
No-new-contract check             PASS — no new method, event, subscription,
                                  transport, Redis key, or database column
Unblock check                     PASS — all nine TASK-159 items determinable
Unmodified-file guard             PASS — zero files under src/ or tests/;
                                  TASK-159 and TASK-160 byte-identical;
                                  tasks/completed/ and docs/03-decisions/
                                  unchanged
Unit / integration tests          N/A — this task changes no code
Gameplay scenarios                N/A — TASK-159's implementation owns them
```

### Validation Detail

```text
Carrier                        PASS — §4 item 11 unchanged; §4.3 item 1 and
                               §4.4 item 1 both ride the existing push and its
                               existing trigger
Existing members preserved     PASS — battleId/turn/sequence/board/rngSeed/
                               rngState/playerState unchanged; petState's
                               existing five members unchanged in name, type,
                               presence and meaning (a sixth is added)
Pet StatusEffects authorized   PASS — §4.3 item 14 + §2.3.1
Boss HP + MaxHP authorized     PASS — §4.4 items 2/4/5
Boss hidden state hidden       PASS — §4.4 item 3 lists every excluded member
                               exhaustively; §2.4 repeats the boundary from the
                               state side; BOSS_RULES.md §6.2.4 from the
                               gameplay side
GAME_STATE ↔ SIGNALR agreement PASS — both now state membership and delivery
                               as separate facts with the same two answers
BOSS_RULES §6.2.4              PASS — no longer defers; records the outcome;
                               still does not authorize Boss StatusEffects
No new SignalR method/event    PASS — §2 (three methods), §4 item 11, §8 item 9
No new persistence semantics   PASS — REDIS_STATE.md §2 item 1 / §7 item 9
                               unchanged; no key, no Redis-only field, no
                               column; §4.4 item 9 states this explicitly
TASK-160 unchanged             PASS — not written by this task
TASK-159 unchanged and not
  implemented                  PASS — not written, not moved, not re-statused
```

### Quality Review Checklist (`quality/review.md` §1)

```text
1 Correctness        PASS — every authored statement cites the section that owns
                     it; nothing rests on the current source code or on a
                     preference
2 Architecture       PASS — the transport, storage, and authoritative model are
                     unchanged; no ADR conflict (ADR-001/004/005/008 checked)
3 Scope              PASS — three docs and one task file; only the two
                     authorized projections added; no sibling collection
                     touched; no unrelated section edited
4 Tests              N/A — no code; validation is the documentation audit
5 Documentation      PASS — canonical owners updated, no duplication, dependent
                     contracts re-read together, findings reported
6 Security           N/A — no authentication or data-exposure surface beyond the
                     two facts the Product Owner authorized
7 Performance        N/A — no runtime path touched
8 Maintainability    PASS — one short section per projection plus one split
                     statement; no new abstraction or convention
9 Determinism        PASS — the delivered values are server-authored state
                     (GAME_RULES.md §18, ADR-001); no client computation,
                     prediction, or RNG is introduced anywhere
Final recommendation PASS
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic — no client file
      touched, and both delivery rules state that the client renders and
      computes none of the delivered values
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — both projected
      facts are MVP IN; nothing OUT or FUTURE was introduced
- [x] Confirmed zero files modified under `src/`
- [x] Confirmed zero files modified under `tests/`
- [x] Confirmed TASK-160 is byte-identical
- [x] Confirmed TASK-159 is byte-identical and was not implemented
- [x] Confirmed `tasks/completed/` and `docs/03-decisions/` are unmodified
- [x] Confirmed no new SignalR method, event, batch, or transport
- [x] Confirmed no Boss `StatusEffects[]` projection and no whole-`BossState`
      projection
- [x] Confirmed no persistence change: no Redis key, no Redis-only field, no
      database column, no migration

---

## Remaining Issues

Reported, not fixed (`AGENTS.md` §16). Each is outside this task's boundary.

1. **Implementing the projection is TASK-159's work, not done here.** The
   contract now exists; the `BattleHub.ToPayload` projection still emits eight
   members and no `bossState`, and the client still models neither fact.
2. **Three stale "not implemented" doc comments** in `BattleStartService.cs`,
   `SwapExecution.cs`, and `BattleStateService.cs` (already recorded by
   TASK-159) remain stale. Comment-only, no runtime effect.
3. **Implementation-side doc comments that assert the old contract.** Several
   `src/` XML comments still state that `StatusEffects[]` is "not a wire member"
   or that `petState` is fixed at four members — e.g. `BossState.cs`'s
   `StatusEffects[]` note, `PetState.cs`'s wire note, `StatusEffectLifecycle.cs`
   §2.3.1 reference, `PetId.cs` §4.3 item 2 reference, `BossStateKind.cs`'s
   "declares no `bossState` payload member", and `BattleHub.cs`'s record
   documentation. These are **implementation-side comments**, not authoritative
   documentation, and this task does not edit `src/`. They are the
   implementation task's to correct.
4. **Test assertions that pin the old member set.** Several tests assert the
   previous projection as a negative contract — e.g.
   `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs`'s
   `BattleStateUpdated_PetState_ShouldCarryNoMemberOutsideTheDocumentedWireMembers`
   (its `permitted` list and its `statusEffects`/`bossState` blocklist) and the
   field-set assertions around it; `GameServer.Domain.Tests/BattleStateTests.cs`'s
   `bossState` naming note; and the client-side
   `GameRuntime.test.ts` / `RuntimeBoundaries.test.ts` /
   `BattleService.test.ts` assertions that `statusEffects` is absent. These
   must be updated by the implementation task as part of implementing the
   contract — per `AGENTS.md` §15, they were correct for the contract in force
   until now and are not to be edited in advance of the implementation. This
   task modifies no test.
5. **`API_CONTRACTS.md` §3's `initialState` remains a different projection.**
   Its `petState` has no `statusEffects` and its `bossState` carries more
   members than the §4 push (including `atk`/`def`/`state`). That is a
   REST-at-creation summary on its own path, not a contradiction of §4, and
   D-1A/D-2A said nothing about it. Aligning or narrowing it would be its own
   contract decision and is **not** performed or proposed here.
6. **`GameRuntime.ts`'s `readBattleState` treats an unknown-extra-member
   payload as acceptable.** It validates shape only, so it will accept the
   widened payload without change; the client-side task will decide what to
   model. Noted so the implementation agent does not mistake acceptance for
   support.
