# TASK-159 — Deliver the Pet / Boss Status-Effect Projection in `BattleStateUpdated`

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section — it does NOT copy
  game rules, formulas, schemas, or contracts.

  PROVENANCE: the task-generation audit following TASK-153 and TASK-158. Both
  are DONE. The audit re-read source and tests rather than trusting task
  descriptions, and established that the server-authoritative battle loop is
  implemented end-to-end: every one of GAME_RULES.md §17's 19 steps (validate
  swap → resolve board → match/remove/gravity/spawn/cascade → combo → count →
  charge passive → trigger relics → generate resources → update power → player
  effects → damage → element modifier → final damage → Boss Response 18a/18b/18c
  → End Turn 19a), plus CardCast, PetSkillCast, Relics, Status Effects, Crit,
  Enrage, terminal Victory/Defeat, durable result/rewards persistence, and
  Redis CAS write-back. Card casting and Pet-skill casting also exist on the
  client.

  THE GAP THIS TASK CLOSES: the client cannot render the two combat facts that
  the landed server loop now produces as first-class state — the active Pet's
  active Status Effects, and the Boss's live HP. `BattleStateUpdated`
  (SIGNALR_PROTOCOL.md §4) is the only state push (§4 item 11) and today carries
  exactly eight members: battleId, turn, sequence, rngSeed, rngState, board,
  playerState, petState. `petState` carries four members (§4.3 item 2) and
  `StatusEffects[]` is explicitly named among the §2.3 members it does NOT
  deliver. No bossState member exists at all. Boss HP therefore reaches the
  client only as the terminal `finalBossHp` inside BattleWon/BattleLost, and
  once in POST /api/battle/start's `initialState`.

  THIS TASK RE-DECIDES NOTHING AND ADDS NO WIRE MEMBER.

  It is a SERVER-SIDE PROJECTION CORRECTION, not a protocol change. §4.3 item 2
  states the current four-member projection and lists `StatusEffects[]` as not
  delivered. Adding it — or a bossState member — would be a new protocol
  contract requiring its own decision, member shapes, optionality rules, and
  agreement with §4 item 4 / §4 item 11. That decision does NOT exist in docs/.
  Per AGENTS.md §7, §18 and §20 this task MUST NOT invent it.

  BOUNDARY: this file only. Zero files under docs/. Zero production source.
  TASK-153, TASK-158, and every other task file are immutable.
-->

---

## Metadata

```text
Task ID:           TASK-159
Type:              BLOCKED-CANDIDATE — created as a DECISION-FIRST task per
                   AGENTS.md §7 / §18 (missing contract before implementation).
                   It is NOT a FEATURE task: the deliverable it would gate has
                   no authored contract. Recorded here so the next
                   implementation dependency is named precisely rather than
                   guessed at.
Status:            SUPERSEDED — 100% of intended deliverables satisfied by
                   downstream work (TASK-160, TASK-161, and landed projection
                   implementation) without direct execution of this task.
                   See "Supersession Determination & Traceability Matrix".
Risk:              HIGH (TASK_TYPES.md §4 / core/task-intake.md §3 — the
                   gated work would change the SignalR wire projection, which
                   is a protocol contract and a state-delivery boundary.
                   Classified at the higher level per §3's tie-break.)
Priority:          HIGH (it gates the client's ability to render Status Effects
                   and live Boss HP — two documented MVP IN items,
                   MVP_SCOPE.md §1 "Combat: … Status Effects, Damage" and
                   "Bosses: Element, Passive, Skill per Boss".)
Primary Agent:     orchestrator (this task's act is classification and
                   reporting; the gated implementation would be realtime +
                   client, per AGENT_SELECTION.md: "SignalR change → Realtime")
Supporting Agents: realtime (owns SIGNALR_PROTOCOL.md §4 state delivery),
                   client (owns the BattleScene presentation the projection
                   would feed), review
Workflow:          architecture/architecture-change.md (the boundary under
                   question is a state-delivery/projection contract, not a
                   gameplay rule and not an isolated code fix)
Skills:            realtime/realtime-protocol-validation,
                   discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12; the gated
                   implementation, if authorized, is a separate task with its
                   own budget)
Dependencies:      TASK-153 (DONE — landed the step-18a Boss Passive effects
                     that made Boss status state non-trivial; IMMUTABLE,
                     read-only),
                   TASK-158 (DONE — restored a green backend suite;
                     IMMUTABLE, read-only),
                   TASK-095 / TASK-096 (DONE — StatusEffect domain state and
                     serializer round-trip; the state this projection would
                     carry already exists and is not in question),
                   TASK-143 / TASK-144 (DONE — reconnect/resync recovery; the
                     §7 snapshot path is implemented and is a separate delivery
                     shape from §4)
Blocks:            Rendering the active Pet's Status Effects in `BattleScene`;
                   rendering live Boss HP during a battle. Both are MVP IN
                   (MVP_SCOPE.md §1) and both are currently unreachable by the
                   client for want of a delivered member.
Estimate:          N/A — no implementation is performed. The gated
                   implementation is not sized here because it is not yet
                   authorized, and sizing it would presuppose the decision.
```

---

## Objective

Record that the next MVP dependency after TASK-153/TASK-158 — making the
active Pet's Status Effects and the Boss's live HP visible to the client — is
**blocked on a missing protocol contract**, and STOP rather than implement it:
`SIGNALR_PROTOCOL.md` §4.3 item 2 currently fixes the `petState` projection at
exactly four members and explicitly names `StatusEffects[]` as **not
delivered**, and §4 defines no `bossState` member at all. No authoritative
document authorizes widening either projection, so no implementation task may
be created for it.

---

## Why This Task Is Next

The audit performed before this file was created classified every remaining MVP
item against the source, not against task descriptions:

```text
DONE
  The whole GAME_RULES.md §17 pipeline, all 19 steps, in
  BattleStateService.cs / SwapExecution.cs / CascadeResolver.cs /
  BoardResolver.cs / ResourceGenerator.cs / DamagePipeline.cs /
  StatusEffectLifecycle.cs.
  Match-3 (all 6 MVP_SCOPE.md §1 rows), Elements, Player account + XP/Level,
  Combat, Card cast, Pet Skill cast, Relics (4 provisioned, trigger evaluation
  + effect application + RelicTriggered all IMPLEMENTED per RELIC_RULES.md
  §8.7), Bosses 3/5 (Passive + Skill each), Redis active state, PostgreSQL
  persistence, Battle Results + Rewards, terminal Victory/Defeat.

PARTIALLY DONE
  Pet/Boss Status Effects: fully implemented and persisted server-side, but
  NOT delivered on the wire and NOT rendered by the client.          ← this task
  Boss HP: implemented server-side, delivered only at battle start (REST) and
  at battle end (`finalBossHp`).                                     ← same cause

BLOCKED (missing rule or missing decision — AGENTS.md §7 / §20)
  "5 Pets":      Thanh Xà and Sơn Hùng Signature Skills are not
                 content-defined (PET_RULES.md §8; CARD_RULES.md §4.1).
  "5 Bosses":    2 of 5 are not content-defined (BOSS_RULES.md §6).
  "~10 Relics":  only 5 named rows; Burning Curse is deferred on the
                 unresolved §3-vs-§6-note-1 conflict (RELIC_RULES.md §6 note 3,
                 §8.5 item 5).
  GET /api/battle/history: listed in API_CONTRACTS.md §1 with no defining
                 section — no response shape, ordering, pagination, or
                 ownership contract (DATABASE.md §4 defines only an index).
```

Every UNIMPLEMENTED row is blocked on a **missing rule or a missing decision**,
not on missing engineering. `AGENTS.md` §7 and §20 make each of those a STOP,
and the task instructions prohibit inventing them. That is why no
implementation task is created here.

Among those, the Status-Effect/Boss-HP projection is the **first** dependency
in the documented battle loop's own order (`… → Damage → Turn progression →
Boss Passive → Boss Skill → SignalR/client synchronization`). It is the
synchronization step of a loop whose server half is complete: the state exists,
is mutated correctly, and round-trips through Redis — it simply is not
delivered. It is therefore the smallest genuinely-next MVP dependency, and it
is recorded here so the missing contract is named precisely instead of being
papered over with an invented member.

---

## Authoritative References

- `docs/02-technical/SIGNALR_PROTOCOL.md` **§4** — the state push, including
  **§4 item 11** (`BattleStateUpdated` is the only state-push method) and
  **§4 item 4** (a payload carries only the implemented stage's own fields);
  **§4.3** ("Delivering the Pet / Passive Stage") and specifically **§4.3
  item 2**, which fixes `petState` at **four** members and states that the rest
  of `GAME_STATE.md` §2.3 — explicitly including `StatusEffects[]` — is **not**
  delivered. **This section is the whole reason this task is BLOCKED.**
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§0** (the two non-interchangeable
  delivery shapes — subscription push §4 vs. `ReceiveEvents` §3 vs.
  `GetBattleState` §7), **§3.2.24** / **§3.1**, **§6** (sequencing), **§7**
  (reconnect/resync snapshot — a different path from §4), **§8 item 5** and
  **§8 item 7** (no board-specific or resolution-specific message may be
  introduced; extending the *state* is the documented way to deliver new data,
  and extending the *method list* is not)
- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState`, incl.
  `StatusEffects[]`), **§2.3.1** (the Status Effect model — identity, type,
  source, magnitude, remaining turns, and the `TargetStat` pairing as relaxed
  by v2.18), **§2.4** (`BossState` — `HP`, `MaxHP`, `ATK`, `Element`,
  `ActiveStatusEffects[]`, `PassiveProgress`), **§2.4.1** (Boss Status Effects),
  **§0 item 5** (one representation per fact — forbids a second spelling on the
  wire)
- `docs/02-technical/GAME_EVENTS.md` **§2** (`PassiveCharged` /
  `PassiveTriggered`, `BossSkillCast`, `DamageCalculated` / `DamageDealt` /
  `DamageTaken`, `BattleWon` / `BattleLost` and the terminal `finalBossHp` /
  `finalPlayerHp` pair) — the events that today carry the only Boss-HP and
  status-adjacent information the client receives
- `docs/01-game-design/COMBAT_RULES.md` **§5.1** (Buff/Debuff), **§5.2**
  (shield/refresh semantics), **§5.3** (Turn-based duration lifecycle and its
  single step-19a decrement) — the rules the status state carries
- `docs/01-game-design/GAME_RULES.md` **§16** (canonical event-name list),
  **§17** (the fixed resolution order, esp. step 18a/18b and step 19a),
  **§18** (server authority)
- `docs/00-overview/MVP_SCOPE.md` **§1** ("Combat: HP, ATK, DEF, Power, Crit,
  Status Effects, Damage, Element interaction"; "Bosses: Element, Passive,
  Skill per Boss"), **§2**, **§4**; `docs/00-overview/ROADMAP.md` §1 Phase 1
  ("Server-authoritative resolution over SignalR")
- `docs/02-technical/ARCHITECTURE.md` **§2.2.1** (Game Runtime Coordination —
  rule 4, events pass through unchanged; rule 5, client runtime state is
  technical only) and **§2.2.3**; **§5** (anti-overengineering)
- `docs/02-technical/API_CONTRACTS.md` **§1** (endpoint summary, incl. the
  `GET /api/battle/history` entry that has no defining section) and **§7**
  ("What Is Not Here")
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`,
  `ADR-004-signalr-realtime.md`, `ADR-008-battle-recovery.md`
- `AGENTS.md` **§4** (never silently resolve a conflict), **§7** (invent no
  rule — the governing section here), **§9** (anti-overengineering), **§10**
  (server authority), **§12**/`docs/AGENTS.md` §12 (domain boundaries),
  **§16** (report, do not fix inline), **§17** (documentation change rule),
  **§18** (architecture change rule), **§20** (stop conditions)
- `tasks/completed/TASK-041-implement-battle-result-persistence.md` — records
  `GET /api/battle/history` as "listed in `API_CONTRACTS.md` §1 with no defining
  section. Not implemented; no query was invented for it."
- `tasks/completed/TASK-153-implement-mvp-boss-passive-effects-at-step-18a.md` —
  its Scope records §6.2.4's intentional non-exposure of `BossState` through the
  current SignalR projection, and states that any requirement for it is "a
  separate future protocol decision, not authorized by these effect rules".
- `tasks/completed/TASK-158-correct-stale-step-18b-expectation-derivation-in-swap-event-order-integration-test.md`

---

## Current State

The server half is complete and verified; the delivery half is not authorized.

```text
src/backend/GameServer.Api/Hubs/BattleHub.cs
    BattleStateUpdated is produced here and serializes exactly eight members:
    BattleId, Turn, Sequence, RngSeed, RngState, Board, PlayerState, PetState.
    The Pet projection carries passiveId, passiveProgress, the conditional
    passiveResetOverride, and equippedCards — and no combat stats, no
    StatusEffects[], and no BossState.

src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
src/backend/GameServer.Domain/Battle/BossState.cs
    The status state itself exists, is mutated by the landed step-18a / 18b /
    19a logic, and round-trips through the Redis serializer. Nothing about the
    state is in question; only its delivery is.
```

`ARCHITECTURE.md` §2.2.1's implementation-status paragraph confirms the
CardCast / PetSkillCast / reconnect surface is implemented, so the client-side
transport needed to *receive* a widened projection already exists — which is
precisely why the missing piece is a contract, not an engineering effort.

Three doc comments in `src/backend` are stale and understate what exists
(`BattleStartService.cs`, `SwapExecution.cs`, `BattleStateService.cs` all still
say steps 15–19 or `ReceiveEvents` are unimplemented). They are comment-only
inaccuracies with no runtime effect, and they are out of this task's scope
(`AGENTS.md` §16 — report, do not fix inline).

---

## Scope

### In Scope

1. Recording, precisely and with citations, that the Pet/Boss Status-Effect
   (and live Boss-HP) client-visible projection is blocked on a missing
   contract in `SIGNALR_PROTOCOL.md` §4 / §4.3.
2. Recording the exact statement that must change, and by whom, before any
   implementation task may be created.
3. Recording the classification of every other remaining MVP item, so the next
   task-generation pass does not re-derive it.

### Out of Scope

- **Any implementation.** No source file, test file, or wire member is added or
  modified by this task.
- **Any change under `docs/`.** The missing contract is a decision for the
  owning document (`SIGNALR_PROTOCOL.md` §4); authoring it here would be
  inventing a protocol contract (`AGENTS.md` §7, §18).
- **Inventing a `statusEffects` member, a `bossState` member, a Boss-HP member,
  their shapes, their optionality, or their delivery rule.** None exists.
- **The four other blocked MVP items** — the 2 remaining Pets, the 2 remaining
  Bosses, the remaining Relics / Burning Curse, and `GET /api/battle/history`.
  Each needs its own missing rule or decision; each is reported, not actioned.
- **The stale "not implemented" doc comments** in `BattleStartService.cs`,
  `SwapExecution.cs`, and `BattleStateService.cs` — reported only.
- **`GET /api/battle/history`** — it is NOT implementation-ready: `API_CONTRACTS.md`
  §1 lists it but no section defines its response shape, ordering, pagination,
  or ownership, and `DATABASE.md` §4 defines only an index
  (`BattleResult(PlayerId, CompletedAt DESC)`). Building it would require
  inventing all four.
- **`Player.IsCombatReady`** — a defined-but-unimplemented contract member with
  no rule for what makes an account not ready (recorded by TASK-041).
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2, and any item not
  listed in §1 (treat unlisted as FUTURE per §4).

---

## Dependencies

```text
TASK-153   DONE   landed the step-18a Boss Passive effects; makes Boss status
                  state non-trivial. IMMUTABLE; read-only.
TASK-158   DONE   restored a green backend suite (2638 passed, 0 failed).
                  IMMUTABLE; read-only.
TASK-095   DONE   StatusEffect domain state and its step-19a lifecycle.
TASK-096   DONE   StatusEffect serializer round trip.
TASK-143   DONE   server battle-state reconnect recovery.
TASK-144   DONE   client reconnect/resync recovery.
TASK-041   DONE   battle result persistence; records the history-endpoint gap.
```

No dependency above is BLOCKED. The block is **not** a dependency — it is a
missing authoritative contract, which no task may supply on its own authority.

---

## Acceptance Criteria

- [ ] This task records the blocking statement verbatim from
      `SIGNALR_PROTOCOL.md` §4.3 item 2 (the four-member `petState` projection
      and the explicit `StatusEffects[]` exclusion), with its section citation.
- [ ] This task records that `SIGNALR_PROTOCOL.md` §4 defines no `bossState`
      member, with its section citation.
- [ ] This task names the owning document and section that must authorize any
      widened projection before an implementation task may exist.
- [ ] This task records that no implementation was performed: zero files under
      `src/` and zero files under `tests/` were modified.
- [ ] This task records that zero files under `docs/` were modified.
- [ ] No wire member, event name, Redis key, or database column is proposed or
      invented anywhere in this file.
- [ ] The four other blocked MVP items are each recorded with the exact
      document and section that blocks them.
- [ ] No completed task is reopened or modified.
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001).

---

## Affected Files / Areas

```text
[ ] src/backend/ (Domain / Application / Infrastructure / Api) — NONE
[ ] src/frontend/client/ (scenes / runtime / services / state / ui) — NONE
[ ] tests/ (unit / integration / gameplay scenarios) — NONE
[ ] docs/ — NONE (the missing contract belongs to SIGNALR_PROTOCOL.md §4 and
        may only be authored by a task authorized to change it)
[x] tasks/backlog/TASK-159-<this file>.md — this task file only
[ ] all other task files — NONE (TASK-153, TASK-158, and every completed task
        are immutable)
```

---

## Implementation Notes

None — this task performs no implementation. The evidence it records was
gathered by reading the current source and the authoritative documents in this
session:

- `BattleHub.cs` builds `BattleStateUpdated` from `BattleId`, `Turn`,
  `Sequence`, `RngSeed`, `RngState`, `BoardState`, the Combo/MatchCount pair,
  and `PetState`. No boss member is read on that path.
- `SIGNALR_PROTOCOL.md` §4.3 item 2 is the operative constraint: the projection
  "carries **four members**" and the remainder of §2.3 — naming
  `StatusEffects[]` explicitly — "is **not** delivered, per §4 item 4's rule
  that a payload carries only the implemented stage's own fields."
- §4.3 item 2's closing sentence ("Referring to `petState` as a whole does not
  widen that rule") forecloses reading the projection as implicitly wider than
  its enumerated members.

The correction path, if the Product Owner wants it, is a documentation task
against `SIGNALR_PROTOCOL.md` §4.3 (and, for Boss HP, §4) — not a code task,
and not this one.

---

## Testing Requirements

### Required Verification

```text
[ ] Unit tests         — N/A. No production behavior is added or changed.
[ ] Integration tests  — N/A. No boundary is modified.
[ ] Gameplay scenarios — N/A. No rule is implemented.
```

### Required Verification That This Task Is Correctly BLOCKED

```text
[ ] Grep docs/ for any authorization widening the §4.3 petState projection or
    defining a bossState member. Expected result: none.
[ ] Grep src/ for a bossState / statusEffects member on the BattleStateUpdated
    payload. Expected result: none.
[ ] Confirm no task file other than this one exists for the projection.
```

### Key Edge Cases

- **`GetBattleState` (§7) is not §4.** A snapshot method already exists for
  reconnect. It does not make the §4 projection complete, and it is not a
  substitute: §0 states the delivery shapes are not interchangeable, and §4
  item 11 fixes `BattleStateUpdated` as the only state push. Do not "solve"
  this by pointing the client at §7.
- **Events are not state.** `PassiveCharged`/`PassiveTriggered`,
  `BossSkillCast`, `DamageCalculated`/`DamageDealt`/`DamageTaken`, and the
  terminal `finalBossHp` are events. They do not give a client the settled
  current Status Effect list or live Boss HP, and §4 item 6 keeps the state push
  and the events non-interchangeable.
- **Do not infer a member from `GAME_EVENTS.md` §2.** No event carries a
  status-effect list; nothing there authorizes a wire member.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **The primary stop condition is already fired.** `SIGNALR_PROTOCOL.md` §4.3
  item 2 fixes the `petState` projection, explicitly excludes
  `StatusEffects[]`, and defines no `bossState` member. Widening it requires a
  decision the documents do not contain → STOP per `AGENTS.md` §7.
- If a widened projection would change the SignalR protocol or the state model:
  **STOP** per `AGENTS.md` §18 — an ADR or an `architecture-change` task
  precedes any code.
- If the work would require a new Battle Event, a new hub method, or a new
  subscription: **STOP** — `SIGNALR_PROTOCOL.md` §8 items 5 and 7 forbid
  introducing a message where extending the state is the documented mechanism,
  and §4 item 11 fixes the single state-push method.
- If any value, shape, or optionality for a proposed member has to be invented:
  **STOP** — inventing it is the prohibited act.
- If this task is asked to implement the projection rather than record its
  block: **STOP** — that is the separate, currently unauthorized task.
- If the work requires changing a gameplay rule or expanding MVP scope:
  **STOP**.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural
  boundaries: **STOP & decompose**.

---

## Stop Condition Report

**Status: BLOCKED — no implementation performed. Zero files under `src/`,
`tests/`, or `docs/` were modified. No implementation task was created.**

### Problem

The next MVP dependency in the client-synchronization half of the battle loop
is not implementable, because the contract it needs does not exist.

`SIGNALR_PROTOCOL.md` §4.3 item 2 states the current `petState` projection in
the `BattleStateUpdated` push and fixes it at **four** members — `passiveId`,
`passiveProgress`, the conditional `passiveResetOverride`, and `equippedCards` —
and then states that the rest of `GAME_STATE.md` §2.3, **naming
`StatusEffects[]` among them**, "is **not** delivered, per §4 item 4's rule that
a payload carries only the implemented stage's own fields." The same item closes
the reading that `petState` is implicitly wider than its enumerated members:
"Referring to `petState` as a whole does not widen that rule."

`SIGNALR_PROTOCOL.md` §4 defines **no `bossState` member at all**. So neither
the active Pet's Status Effects nor the Boss's live HP is authorized to be
delivered, and the client cannot render either from state.

The state itself is not in question. `StatusEffectLifecycle.cs`, `BossState.cs`,
and the Redis serializer already carry it, and the §17 pipeline already mutates
it correctly. What is missing is purely the authorization to project it.

### Relevant sources

```text
docs/02-technical/SIGNALR_PROTOCOL.md §4.3 item 2   the four-member petState
                                                    projection and the explicit
                                                    StatusEffects[] exclusion
docs/02-technical/SIGNALR_PROTOCOL.md §4            no bossState member; item 4
                                                    (payload carries only the
                                                    implemented stage's fields),
                                                    item 11 (single state push)
docs/02-technical/SIGNALR_PROTOCOL.md §0            the three delivery shapes are
                                                    not interchangeable
docs/02-technical/GAME_STATE.md §2.3, §2.4          the state that is not delivered
docs/02-technical/GAME_STATE.md §0 item 5           one representation per fact
docs/01-game-design/BOSS_RULES.md §6.2.4            "The current SignalR
                                                    projection intentionally does
                                                    not expose BossState … an
                                                    intentional, recorded contract
                                                    limitation, not a defect. Any
                                                    requirement for client-visible
                                                    Boss HP or Boss StatusEffects
                                                    is a separate future protocol
                                                    decision, and is not authorized
                                                    by these effect rules."
tasks/completed/TASK-153-*.md Scope                 same statement, applied
```

### Why this is not resolvable in code

This is a **missing contract**, not an implementation detail. Deciding to widen
`BattleStateUpdated` requires choosing member names, shapes, optionality and
absence conventions (§3.2.5), the relationship to §4 item 4's staged-fields
rule, whether Boss HP joins the same push or a different one, and agreement with
`GAME_EVENTS.md` §2 and `GAME_STATE.md` §0 item 5 — each of which changes a
documented protocol contract and needs a Product Owner decision
(`AGENTS.md` §4, §7, §17, §18, §20). `BOSS_RULES.md` §6.2.4 already records that
satisfying such a requirement "is a separate future protocol decision, and is
not authorized by these effect rules."

### Waiting for

A Product Owner decision, applied by a documentation task against its owning
document, that does two things:

```text
1. States whether the active Pet's active Status Effects are delivered in the
   §4 state push, and if so, authored as a member of GAME_STATE.md §2.3's
   projection under SIGNALR_PROTOCOL.md §4.3 — with its member shape, its
   absence convention, and its relationship to §4 item 4.
2. States whether the Boss's live HP (and any Boss status state) is delivered
   in the §4 state push or by another documented means — with the same
   detail — or records explicitly that it remains intentionally undelivered.

Until both are authored in docs/, no implementation task may be created for
either (AGENTS.md §7), and this block stands.
```

---

## Completion Evidence

<!--
  HISTORICAL EXECUTION NOTE: This task was not directly executed and stopped per
  AGENTS.md §7 when first picked up (see §"Stop Condition Report" above).
  Per tasks/TASK_LIFECYCLE.md §1/§3, it is reconciled as SUPERSEDED because 100%
  of its intended deliverables and scope were subsequently satisfied downstream
  by TASK-160, TASK-161, and the landed server/client projection implementation.
-->

### Supersession Determination & Traceability Matrix

```text
Status: SUPERSEDED (tasks/TASK_LIFECYCLE.md §1/§3)
Reconciliation: BLOCKED → SUPERSEDED

TASK-159 DELIVERABLE TRACEABILITY:
  D-1  Record that the Pet/Boss Status-Effect (and live Boss-HP) client-visible
       projection is blocked on a missing contract
       → SATISFIED BY TASK-161, which authored the contract this task recorded
         as missing (SIGNALR_PROTOCOL.md §4.3 item 14, §4.4; the §4.3 item 2
         exclusion wording this task quoted was re-worded there).
  D-2  Record the exact statement that must change, and by whom
       → SATISFIED BY TASK-160 (which made the decision) and TASK-161 (which
         applied it at the owning document). This task named
         SIGNALR_PROTOCOL.md §4.3 as the statement to change; TASK-161 changed it.
  D-3  Record the classification of every other remaining MVP item
       → SATISFIED BY TASK-160's §"Out of Scope", which enumerates the same
         remaining blocked items (the 2 Pets, the 2 Bosses, the remaining
         Relics / Burning Curse, and GET /api/battle/history) and is the later,
         authoritative record of that classification.
  D-4  (implied by "Blocks") make the active Pet's Status Effects and the
       Boss's live HP reachable by the client
       → SATISFIED BY the landed projection implementation (BattleHub.cs payload
         members projected from PetState.ActiveStatusEffects and
         BossState.HP/MaxHP; client model and rendering in SignalRService.ts /
         GameRuntime.ts / BattleScene.ts) and its passing suites (Backend 2642
         passed, Frontend 537 passed, TypeScript PASS, Vite build PASS).

Independent actionable scope remaining: NONE (100% satisfied downstream).
```

### Changed Files

- `tasks/completed/TASK-159-<this file>.md` — this task file only (reconciled as
  SUPERSEDED per TASK-162 audit and moved to tasks/completed/). Zero files under
  `src/`, `tests/`, or `docs/`.

### Validation Results

```text
Lifecycle reconciliation: SUPERSEDED determination verified against
tasks/TASK_LIFECYCLE.md §1/§3. 100% downstream deliverable satisfaction verified
across TASK-160, TASK-161, and landed projection implementation.
```

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic (no code was written by this task)
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1) — the blocked
      dependency is an MVP IN item ("Server-authoritative resolution over
      SignalR")
- [x] Confirmed zero files under `src/` modified by this task
- [x] Confirmed zero files under `tests/` modified by this task
- [x] Confirmed zero files under `docs/` modified by this task
- [x] Confirmed no completed task modified (TASK-153 and TASK-158 untouched)
- [x] Confirmed no new task other than this one was created
