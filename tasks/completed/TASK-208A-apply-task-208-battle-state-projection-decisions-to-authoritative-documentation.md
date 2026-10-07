# TASK-208A — Apply the TASK-208 Battle-State Projection Decisions to the Authoritative Documentation

<!--
  COMPLETION RECORD. Documentation only: zero files under src/ or tests/.
  TASK-208 is the immutable decision record this task consumes and applies.

  DECIDES NOTHING. Every member, name, type, presence rule and client
  boundary this task wrote is either (a) TASK-208's recorded Product Owner
  decision (D-208-01 … D-208-04), or (b) an element already owned by an
  existing authoritative section, referenced rather than invented.

  The wire wording lives at its owner — SIGNALR_PROTOCOL.md §4.3 item 15 and
  §4.4 item 10 — and is cited here, not restated (tasks/README.md §9).
-->

---

## Metadata

```text
Task ID:           TASK-208A
Type:              DOCUMENTATION (TASK_TYPES.md §2 — docs/ content is the
                   primary output; workflow documentation/documentation-change.md)
Status:            DONE
Risk:              HIGH (widens a frozen realtime projection that is
                   cross-referenced by GAME_STATE.md, BOSS_RULES.md,
                   REDIS_STATE.md, API_CONTRACTS.md, ADRs and several
                   contract-pinning tests; same level TASK-161 recorded)
Priority:          P0 (the sole unblocking artifact for TASK-209)
Primary Agent:     review (authors a contract from a recorded decision)
Supporting Agents: realtime (SIGNALR_PROTOCOL.md §4 owns the projection),
                   gameplay (GAME_STATE.md §2.3/§2.4 and BOSS_RULES.md §6.2.6
                   own the state and the visibility constraint)
Workflow:          documentation/documentation-change.md
Skills:            realtime/realtime-protocol-validation,
                   discovery/impact-analysis,
                   discovery/documentation-discovery,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (5 skills — Complex budget, tasks/README.md §12)
Dependencies:      TASK-208 (DONE — the Product Owner decision register
                   D-208-01 … D-208-05; IMMUTABLE, read-only, NOT modified),
                   TASK-160 / TASK-161 (DONE — the D-1A/D-2A widening and its
                   amendment, the structural precedent; immutable)
Blocks:            TASK-209 (projection + battle HUD). No code may implement
                   the contract before this amendment; it has now landed.
Estimate:          Complex (five docs sections across three files, one task file)
```

**Type classification note.** `DOCUMENTATION`, not `ARCHITECTURE` and not
`GAMEPLAY-CHANGE`. No gameplay rule, value, magnitude, trigger, target, or
threshold changed. No architectural decision changed: the transport, the single
state-push method, the storage strategy and the authoritative model are
untouched, and no ADR is created or amended. What changed is the *content* of an
existing projection — `TASK_TYPES.md` §2's DOCUMENTATION definition, and the
classification `TASK-161` already applied to the structurally identical
`TASK-160` widening.

---

## Objective

Author the exact, implementation-ready `BattleStateUpdated` projection contract
TASK-208 authorizes — the active Pet's current `HP`, `MaxHP` and `Power`, and
the Boss's canonical technical Identity — into the documents that own each part
of it, so TASK-209 can implement the projection and the battle HUD without
inventing a member, a name, a type, a presence rule, or a client boundary.

---

## Decision source (TASK-208, immutable)

```text
D-208-01  Pet HP projection   — the active Pet's current HP and Max HP are
                                delivered under the existing `petState`
D-208-02  Power projection    — the existing authoritative Power is exposed
                                (not deferred, no gameplay decision)
D-208-03  Boss presentation   — the minimum Boss presentation contract is ONE
                                fact: the canonical technical Identity
D-208-04  Contract amendment  — extend the two existing narrowed projections on
                                the existing `BattleStateUpdated` carrier
D-208-05  Pet Tier/Star/Level — SEPARATE product decision; NOT exposed here

Carrier:                  the existing `BattleStateUpdated` only
New SignalR method/event: none
Authoritative sources:    PetState.HP / MaxHP / Power, BossState.BossId
```

Member wording is authored at its owner (`SIGNALR_PROTOCOL.md` §4.3 item 15 and
§4.4 item 10) and cited, not restated, in this record.

---

## In Scope / Out of Scope

**In scope:** the version/revision entry and the projection sections of
`SIGNALR_PROTOCOL.md`; the delivery-split statements of `GAME_STATE.md`
§2.3/§2.4; the visibility constraint of `BOSS_RULES.md` §6.2.6.

**Out of scope (untouched):** all of `src/` and `tests/`; `API_CONTRACTS.md`,
`DATABASE.md`, `REDIS_STATE.md`, `GAME_EVENTS.md`, `ARCHITECTURE.md`, `TDD.md`,
`GDD.md`, `MVP_SCOPE.md`, `ROADMAP.md`, `PET_RULES.md`, `COMBAT_RULES.md`,
`CARD_RULES.md`, `PASSIVE_RULES.md`, `ELEMENT_RULES.md`; `docs/03-decisions/`
(no ADR); TASK-207 and TASK-208 (immutable). Deliberately deferred to their own
tasks: card cost / affordability (TASK-212), Pet Tier/Star/Level meaning
(TASK-215), Player-XP persistence verification (TASK-216), unrelated
documentation reconciliation (TASK-217), and any Boss telegraph or Element /
presentation metadata on the wire (no task; not authorized).

---

## Amendment Applied

Cited by owner section; the authored wording is in the documents themselves.

**`docs/02-technical/SIGNALR_PROTOCOL.md`** (→ **v2.17**)

- Version header: revision entry recording the decision, the widened member
  sets, the reconstruction-safe-carrier rationale, and explicitly what does
  **not** change (method list, state-push method, triggers, storage, event
  schema, every other excluded member).
- §4 **item 18 (new)** — the Pet combat-stat subset is delivered, narrowed to
  three members, under the existing `petState`; records that this was the task
  §4.3 item 2 previously deferred to, and that the state projection is the
  reconstruction-safe carrier.
- §4 **item 15** — the false "the state push was already the authoritative
  record of the resulting `Power`" claim corrected; `CardCostModifiers[]` stays
  undelivered and `power` authorizes no client-side cost/affordability/legality
  computation.
- §4 **item 16** and **item 17** — their restated member counts replaced by a
  reference to §4.3 item 2 / scoped so neither contradicts the new set.
- §4.3 **tree** and **item 2** — the member set is now eight members; `HP`,
  `MaxHP` and `Power` removed from the exclusion list while every other named
  member stays excluded.
- §4.3 **item 15 (new)** — owns source, wire name, type, always-present /
  non-nullable / zero-as-zero, `maxHp`-not-derived-from-`hp`, no `maxPower`
  member, the client boundary, and state-vs-event.
- §4.4 **tree**, **items 2, 3, 4, 5, 7, 8** — `bossState` restated as a
  three-member projection; `BossId`/Identity removed from the exclusion list;
  item 8's stale citation of a non-existent §4 item 2 sentence removed.
- §4.4 **item 10 (new)** — owns the Identity's source, string type, identity-only
  boundary (never a display name, never `BossDefinitionId`, no presentation
  metadata), client catalog resolution, and its non-conflict with the events.
- §7 — the parity paragraph names both widened member sets and states the
  invariant **join snapshot = resolution snapshot = resync snapshot** across
  `JoinBattle`, `BattleStateUpdated` and `GetBattleState`.
- §8 item 9 — extended so the "no message of its own" list covers the new
  members.

**`docs/02-technical/GAME_STATE.md`** (→ **v2.25**)

- §2.3's "Domain state implemented is not the same as client wire delivery"
  summary and its combat-stats paragraph now name the delivered subset
  (`hp`/`maxHp`/`power`) instead of stating that no combat stat is delivered,
  and record the **state projection vs transient event** split and that `Power`
  is now available through the reconstruction-safe state projection. The
  sentence that deferred delivery to "its own task" records that this **was**
  that task. No state-shape change.
- §2.4 — three client-visible `BossState` members instead of two; `BossId`/
  Identity removed from the server-side list; the delivered Identity recorded as
  an identity only, with presentation content resolved from the client catalog.
- §2.3.1's cross-entity note and §2.3.4/§2.3.5/§2.3.9's "not a wire member"
  arguments no longer restate a stale `petState` member list; they reference
  §4.3 item 2's enumerated set.

**`docs/01-game-design/BOSS_RULES.md`** (→ **v2.12**)

- §6.2.6 — the client-visibility constraint reworded: live `HP`/`MaxHP` **and**
  the canonical technical Identity are client-visible and nothing else is.
  Wording only: no stat, magnitude, duration, trigger, target, threshold,
  operator, effect, skill, passive or content value changed, and no telegraph,
  Element or presentation metadata authorized. Boss `StatusEffects[]` remains
  explicitly not authorized.

---

## Acceptance Criteria

- [x] The four authorized members are documented at their owners with source,
      wire name, type, presence, zero-as-zero, and client boundary
- [x] All four are always present and non-nullable; none is optional and none
      has an absence or `null` case
- [x] `maxHp` is explicitly not derived from `hp`; no `maxPower` member invented
- [x] `bossId` is the canonical technical Identity only — no display name,
      Element, portrait, asset key, passive/skill metadata or `BossDefinitionId`
      on the wire
- [x] §7 states the member-for-member parity of join, resolution and resync
- [x] §4 item 15's false claim is corrected and no contradictory statement
      remains in the same document
- [x] No member outside the approved sets is authorized anywhere, at any depth
- [x] `BattleStateUpdated` remains the only state-push method; §2's method list
      is unchanged at three gameplay methods
- [x] No new SignalR method, event, subscription, store, key, column, ADR or
      presentation-state object
- [x] No gameplay rule, state member, Redis contract, database contract or API
      contract changed
- [x] Zero files under `src/` or `tests/` modified; TASK-207 and TASK-208 are
      byte-identical and were not modified

---

## Affected Files & Areas

```text
[ ] src/backend/ · src/frontend/client/          — NONE
[ ] tests/                                        — NONE
[x] docs/02-technical/SIGNALR_PROTOCOL.md         (version header; §4 items
                                                   15/16/17 + item 18 new;
                                                   §4.3 tree, item 2 + item 15
                                                   new; §4.4 tree, items 2/3/4/
                                                   5/7/8 + item 10 new; §7;
                                                   §8 item 9)
[x] docs/02-technical/GAME_STATE.md               (version header; §2.3 delivery
                                                   summary + combat-stats
                                                   paragraph; §2.3.1 note;
                                                   §2.3.4/§2.3.5/§2.3.9 member-set
                                                   references; §2.4 paragraph)
[x] docs/01-game-design/BOSS_RULES.md             (version header; §6.2.6)
[x] tasks/completed/TASK-208A-<this file>.md      — this record only
[ ] docs/02-technical/API_CONTRACTS.md            — NONE (initialState already
                                                    carries the facts)
[ ] docs/02-technical/REDIS_STATE.md              — NONE (no storage change)
[ ] docs/02-technical/GAME_EVENTS.md              — NONE (no event change)
[ ] docs/03-decisions/                            — NONE (no ADR)
```

---

## Validation

### Required checks

```text
git diff --check                       PASS (exit 0, no whitespace errors)
Changed-file scope                     PASS — exactly the three authorized
                                       docs/ files (plus this task record);
                                       zero .cs/.ts/.tsx/.json/schema files
No production/test/API/DB change       PASS — git diff --name-only over src/,
                                       tests/ and code globs is empty
Repository documentation validator      NONE EXISTS — the only script under
                                       scripts/ is standalone-web-smoke.mjs
                                       (runtime smoke test, not documentation);
                                       `git diff --check` plus the grep-based
                                       consistency sweep below is the available
                                       documentation validation
Markdown code-fence balance            PASS (SIGNALR 68, GAME_STATE 72,
                                       BOSS_RULES 14 — all even)
Production test suite                  NOT RUN (documentation-only task; the
                                       risk is that the contract-pinning tests
                                       still assert the pre-amendment sets,
                                       which is TASK-209's required change)
```

### Consistency sweep (the six required terms)

Searched `petState`, `bossState`, `power`, `BattleStateUpdated`,
`GetBattleState`, `resync` across `docs/` and `tasks/`, checking exact member
sets, nullable vs non-nullable wording, initial vs update state, event-only vs
state-projection wording, Power delivery and resync parity:

```text
Member sets            PASS — SIGNALR §4.3 item 2 says eight; §4.4 item 2 says
                       three; §7, GAME_STATE §2.3/§2.4 and BOSS_RULES §6.2.6
                       agree; no remaining statement fixes `petState` or
                       `bossState` at a pre-amendment count except the revision
                       log's per-revision history entries (see Remaining Issues)
Nullable / presence    PASS — the four members are always-present, non-nullable,
                       zero-as-zero on every path, and explicitly not governed by
                       §3.2.5's omitted-when-not-applicable convention
Initial vs update      PASS — the members are delivered on the join push and on
                       every committed Swap's resolved-state push by the same
                       projection, so no "initial-only" or "update-only" wording
                       remains
Event vs state         PASS — `PowerChanged` and the Boss/Passive `sourceId`
                       events are consistently described as the change/occurrence
                       feed, never as the reconstruction-safe carrier
Power delivery         PASS — the previously false "the state push" claim in §4
                       item 15 is corrected; no document claims Power is
                       undelivered or event-only
Resync parity          PASS — §7's join = resolution = resync invariant is stated
                       once and referenced; no §7 member set of its own
```

### Duplication check

Each fact has one owner: `SIGNALR_PROTOCOL.md` §4.3 item 15 / §4.4 item 10 own
the wire rules; `GAME_STATE.md` §2.3/§2.4 own the state and the membership/
delivery split; `BOSS_RULES.md` §6.2.6 owns the gameplay-visibility constraint.
None restates another's content.

### Stop-condition check

**None fired.** No rule conflict was resolved silently; no gameplay rule was
missing; no architecture conflict appeared (no ADR is contradicted — ADR-014's
decision concerns `BattleState.PlayerId` and `PetState.PetId`, not
`BossState.BossId`); no scope violation occurred; and the one genuinely open
product question (D-208-05, Pet Tier/Star/Level) was reported, not decided.

---

## TASK-209 Unblock Check

| # | Item TASK-209 needs | Where it is now determined |
|---|---|---|
| 1 | Pet HP / MaxHP source | `GAME_STATE.md` §2.3 `PetState.HP`/`MaxHP`; `SIGNALR_PROTOCOL.md` §4.3 item 15 |
| 2 | Power source | `GAME_STATE.md` §2.3 `PetState.Power`; `SIGNALR_PROTOCOL.md` §4.3 item 15 |
| 3 | Pet member names / types / presence | `SIGNALR_PROTOCOL.md` §4.3 tree + item 15 (`hp`/`maxHp`/`power`, int, always present, zero as `0`) |
| 4 | Boss identity source | `GAME_STATE.md` §2.4 `BossState.BossId`; `BOSS_RULES.md` §6.4; `SIGNALR_PROTOCOL.md` §4.4 item 10 |
| 5 | Boss member name / type / presence | `SIGNALR_PROTOCOL.md` §4.4 tree + items 2/4/10 (`bossId`, string, always present) |
| 6 | Carrier and trigger | `SIGNALR_PROTOCOL.md` §4 item 11, §4.1 item 1, §4.3 item 1 |
| 7 | Resync parity | `SIGNALR_PROTOCOL.md` §7 — join = resolution = resync |
| 8 | Client boundary | §4.3 item 15 and §4.4 item 10 (render only; no derivation; no cost/affordability/legality; no matchup logic; no second catalog) |
| 9 | No new method / event / store | `SIGNALR_PROTOCOL.md` §2, §4 item 11, §8 item 9; `GAME_STATE.md` §2.3/§2.4 |
| 10 | Tests to update | `tasks/completed/TASK-208-*.md` §L lists the contract-pinning locations |

**Result: UNBLOCKED.** Every member is determined by an authoritative section or
by TASK-208's recorded decision; no value must be invented.

---

## Remaining Issues

Reported, not fixed (`AGENTS.md` §16). All are outside this task's boundary.

1. **The implementation is still pre-amendment.** `BattleHub`'s
   `PetStatePayload` carries no `hp`/`maxHp`/`power` and `BossStatePayload`
   carries no `bossId`, and the client wire types and runtime readers are
   unchanged. Expected: the contract lands first (`AGENTS.md` §17). TASK-209
   owns it.
2. **Contract-pinning tests still assert the pre-amendment sets.** e.g.
   `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` (the `petState`
   `permitted` list and combat-stat blocklist, the `bossState` member set, the
   `bossId` identity-exclusion test), `Hubs/BattleHubReconnectRecoveryTests.cs`
   (the snapshot's `["hp","maxHp"]` set), and the client runtime/BattleScene
   fixtures. They were correct for the contract in force until now and must be
   updated to the amended sets by TASK-209 — not weakened, and not edited here.
3. **Implementation-side doc comments assert the old contract.** e.g.
   `BattleHub.cs`'s "the two-member Boss HP projection" note and its
   `petState` member comment, and `PetState.cs`/`BossState.cs` wire notes.
   Comment-only, no runtime effect; implementation-side, so not edited here.
4. **Retained revision-log history.** The `SIGNALR_PROTOCOL.md` header's
   per-revision entries (2.11 "`petState` carries exactly four members"; 2.15
   "no `BossState` member beyond `hp`/`maxHp` becomes client-visible") are
   historical records of what earlier revisions established and are left
   intact, following the precedent that 2.15 did not rewrite 2.11. The current
   contract is stated by the 2.17 entry and by §4.3 item 2 / §4.4 item 2.
5. **Pre-existing stale cross-references (not caused by this amendment).**
   `SIGNALR_PROTOCOL.md` cites "§7.1" for the reconnect snapshot (the section
   is §7), and the header's prior-2.15 entry cites a "§4 item 2's closing
   sentence" that §4 item 2 ("Direction") does not contain. The same
   non-existent §4 item 2 sentence was cited by §4.4 item 8; that citation was
   removed as part of this amendment's rewrite of the sentence. The header
   instance is revision history and was left. `GAME_STATE.md` §2.8 item 3
   likewise cites `SIGNALR_PROTOCOL.md` §7.1. These are unrelated
   documentation drift (TASK-217), not contradictions of the amended contract.
6. **Incomplete sibling-collection arguments now reference the owner.** Three
   `GAME_STATE.md` items (§2.3.4, §2.3.5, §2.3.9) previously justified their
   "not delivered" claim by restating an incomplete `petState` member list; they
   now reference §4.3 item 2's enumerated set. Their conclusions are unchanged.
7. **`CARD_RULES.md` §3.6 became accurate.** Two of its sentences ("The
   resulting Power is the value delivered by the `BattleState` push"; "the
   authoritative record of the resulting `Power` is the state push and
   `PowerChanged`") were false before this amendment and are true after it. No
   edit was needed; recorded so the change in status is not mistaken for drift.
8. **Unrelated documentation drift remains** (e.g. `ARCHITECTURE.md`'s "No
   gameplay HUD exists yet"). Reported by TASK-207 §J-4; owned by TASK-217. Not
   touched.
