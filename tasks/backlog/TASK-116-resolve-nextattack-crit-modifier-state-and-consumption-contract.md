# TASK-116 — Resolve the `NextAttack` Crit Modifier Contract (Representation, Lifetime, Attack Boundary, and Crit Source Composition)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine contract gap and requires the
  appropriate human/Product-Owner decision, then records it in the canonical
  owner document(s). Choosing a representation for a source-specific
  NextAttack modifier, inventing the meaning of "next attack", defining how
  the Crit sources compose, or ruling on source-specific removal is the
  single prohibited action of this task (AGENTS.md §7, §20).

  PROVENANCE: identified by the TASK-115 review. TASK-115 (BACKLOG, BLOCKED
  by this gap) was instructed to implement Iron Fang's `Crit` element
  (`scope` = `NextAttack`, `DATABASE.md` §3 item 1). The review stopped
  because `docs/` defines the NextAttack *token* but never defines how a
  NextAttack modifier is represented in authoritative Battle State, composed
  with the other Crit sources, or consumed by an attack. This task is the
  smallest unit that resolves it.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per AGENTS.md §20, "Missing
  rule" and "Ambiguous requirement" are stop conditions. `DATABASE.md` §3
  item 1 states the `scope` member "author[s] no gameplay" and hands the
  rule to `CARD_RULES.md` §4.1; `CARD_RULES.md` §4.1 states "for the next
  attack only" and says nothing further. No implementation task may proceed
  against it.

  BOUNDARY: documentation only. Zero files under src/ or tests/. This task
  creates no ADR unless the recorded answer requires one (reported, not
  authored — AGENTS.md §18), authors no balance value, adds no Battle Event,
  adds no SignalR method, and does not fix TASK-115.
-->

---

## Metadata

```text
Task ID:           TASK-116
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is a
                   recorded contract decision plus its entry in the canonical
                   owner document(s). See "Type classification note". If the
                   decision requires a code, schema, or ADR change, that change
                   is a SEPARATE follow-up task — not this task's act.
Status:            IN REVIEW (the Product Owner answered all decisions; D-1,
                   D-1a, D-2, D-3, D-3a, D-3b, D-4–D-4.6, D-5, D-5.1–D-5.5,
                   Iron Fang × Bạch Hổ, D-6, D-7, and D-8 are recorded verbatim
                   in "Product Owner Decisions" below, and the resulting state
                   is recorded in "Resulting Contract" and "Classification
                   Outcome". Zero files under `docs/`, `src/`, or `tests/`
                   changed. This follows the TASK-113 / TASK-108 precedent.
                   Per TASK_LIFECYCLE.md §4, landing in `completed/` requires
                   review; the file stays in `backlog/` until then.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the decision it records governs the state,
                   lifetime, and consumption contract of every
                   source-specific stat modifier, and is cross-referenced by
                   COMBAT_RULES.md §3.3, CARD_RULES.md §4.1, PASSIVE_RULES.md
                   §7/§8, GAME_STATE.md §2.3.1/§5.1.1, and DATABASE.md §3.
                   The decision may change the documented meaning of an
                   existing state collection.)
Priority:          CRITICAL (the sole contract blocker on TASK-115, which is
                   the ROADMAP.md Phase 1 "One Pet fully implemented
                   (Element, Passive, Signature Skill)" slice. Iron Fang
                   cannot be implemented without it, and neither can Bạch Hổ's
                   Passive, which `PASSIVE_RULES.md` §8 defines with the same
                   `NextAttack` Crit semantics.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (CARD_RULES.md §4.1, COMBAT_RULES.md §3.3/§5.2,
                   and PASSIVE_RULES.md §7/§8 are the owning domain documents
                   for the Crit modifier and its sources — consulted to CONFIRM
                   the semantics the contract must be able to express, not to
                   author the rule),
                   backend (GAME_STATE.md §2.3.1/§5.1.1 and DATABASE.md §3 own
                   the state and storage contract — consulted to state
                   accurately what the current representation does and does not
                   carry),
                   testing (any recorded boundary must yield a Given/When/Then
                   scenario per AGENTS.md §15; REPORTED, not authored here),
                   realtime (only if the recorded answer implies an event or
                   wire consequence, which is REPORTED for a separate task,
                   never applied here)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-115 (BACKLOG — the blocked implementation task this
                     contract unblocks. IMMUTABLE; read-only. NOT modified by
                     this task),
                   TASK-114 (DONE — recorded the Combat RNG draw contract into
                     COMBAT_RULES.md §3.3, TDD.md §6, GAME_STATE.md §2.6.2.
                     IMMUTABLE; read-only),
                   TASK-113 (DONE — Product Owner decisions D-1–D-5 on the Crit
                     roll procedure, draw point/count, DoT-can-crit, result
                     representation, and Burn events. IMMUTABLE; read-only),
                   TASK-112 (DONE — encoded the `Crit` element row
                     `PercentagePoints, 10, scope "NextAttack"` and recorded
                     in-line that "NOTHING IS APPLIED HERE ... no Crit roll
                     exists". IMMUTABLE; read-only),
                   TASK-111 (DONE — the `scope` storage member's owner
                     decision, TASK-111 D-3. IMMUTABLE; read-only),
                   TASK-095 / TASK-096 (DONE — StatusEffect domain state,
                     step-19a lifecycle, serialization. IMMUTABLE; read-only),
                   TASK-105 (DONE — Shield refresh-not-stack and depletion,
                     the trigger-based expiry precedent. IMMUTABLE; read-only)
Blocks:            TASK-117 (ARCHITECTURE — authors ADR-017 and the owning
                     `GAME_STATE.md` / `COMBAT_RULES.md` / `CARD_RULES.md` edits
                     the recorded decisions require; see "Classification
                     Outcome"), and through it TASK-115 (Iron Fang's Crit and
                     Bạch Hổ's Passive Crit cannot be implemented until the
                     state model and the owning rules are authored), and
                     through that ROADMAP.md Phase 1's "One Pet fully
                     implemented".
Estimate:          Simple (present the evidence, obtain and record one
                   decision set across at most four owner documents; no code,
                   no tests, no migration, no ADR unless reported)
```

**Status note.** `BACKLOG`, which is this task's normal starting state while a
decision set is unanswered — it is what the task exists to collect.
`TASK_LIFECYCLE.md` §2 does not permit `BACKLOG → BLOCKED`, so it does not
start `BLOCKED` even though the underlying gap is a blocker. This follows the
TASK-113 / TASK-108 / TASK-104 precedent exactly.

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`ARCHITECTURE`. The deliverable is a recorded contract decision in its
canonical owner document. Whether the answer *implies* a new Domain member, a
changed state collection, an event, or an ADR is a consequence the task
**reports** — and if the answer requires one, that is a separate follow-up task
created after the decision is recorded, not an act of this task. Authoring the
answer itself is the Product Owner's, not an agent's (`AGENTS.md` §7).

**Type Re-Classification Condition.** One case would re-type this task: if the
Product Owner's answer **changes how an already-documented mechanic behaves**
rather than filling in an unspecified rule. Specifically, if D-1's answer
requires `GAME_STATE.md` §2.3.1 item 3's two-duration-model rule ("never both
and never neither") to be **widened** to admit a third model, or item 6's
one-instance-per-identity rule to be relaxed, that is a change to an existing,
already-authored state contract — not a gap. In that case the correctness route
is a `GAMEPLAY-CHANGE` task (`TASK_TYPES.md` §2,
`development/gameplay-change.md` §3) and possibly an `ARCHITECTURE` task
(`AGENTS.md` §18, since §2.3.1 is the battle-state model). That determination
is made **after** the answer is obtained and is reported here, not pre-judged.
The default expectation is `DOCUMENTATION`, because the NextAttack *rule* is
currently **absent**, not **different**.

**This task authors no rule and no value.** It must not choose a
representation, a lifetime model, an attack boundary, a composition operator,
an ordering, a priority, or a removal mechanism. Its job is to state the gap
precisely, present the viable options with their documented consequences,
obtain the decision, and record it — the same shape TASK-113 used to capture
D-1–D-5 and TASK-108 used for D-1/D-2.

**No balance value is authored.** Iron Fang's +10 percentage points is
`CARD_RULES.md` §4.1's; Bạch Hổ's Crit increase is `PASSIVE_RULES.md` §8's (a
config value, unstated numerically); Assassin Eye's increase is
`RELIC_RULES.md` §5's; Crit's MVP default (5%) and multiplier (1.5×) are
`COMBAT_RULES.md` §1.1/§3.3's. **None is this task's to change.** Only the
missing *representation, lifetime, boundary, and composition rules* that
consume those already-authored values are at issue.

**This task does not fix TASK-115.** TASK-115 remains blocked and byte-identical.

---

## Objective

Resolve, from authoritative documents and a human/Product-Owner answer only, the
**`NextAttack` Crit modifier contract** that TASK-115 requires and that `docs/`
does not currently define: how a source-specific Crit modifier scoped to
`NextAttack` is represented in authoritative Battle State, when it expires, which
damage instance consumes it, and how it composes with the other independent Crit
sources — so that Iron Fang's Crit and Bạch Hổ's Passive Crit can be implemented
deterministically without inventing a gameplay rule.

Concretely, this task makes the decision points explicit and evidenced against
`docs/`, obtains a human/Product-Owner answer for each, and records each settled
answer — without authoring any representation, lifetime, boundary, or composition
rule on the agent's own authority.

**Status: the decisions have been obtained and recorded.** See "Product Owner
Decisions" (recorded as supplied) and "Resulting Contract" (their bindable
statement, C-1–C-10). Two classification findings are reported rather than
absorbed:

```text
1. D-1 introduces `PetState.NextAttackCritModifiers[]` — a new authoritative
   battle-state concept. Per AGENTS.md §18 this requires an ADR, so the owning
   documentation edits are a SEPARATE ARCHITECTURE task (TASK-117), not this
   task's act. See "Classification Outcome".

2. D-4.4 caps composed Crit at 100, described as "the existing documented
   maximum". No such documented maximum exists — COMBAT_RULES.md §1.1 gives
   `Crit` a default (5%) and no range; the 0–100 range in that table is
   `Power`'s, and the `100` derives from the Crit roll bound (V ∈ [0,100)).
   Per Product Owner confirmation this is recorded as a NEW AUTHORED VALUE.
```

---

## Authoritative References

### The gap and its owners (READ ONLY — this task records, it does not redefine)

- `docs/02-technical/DATABASE.md` **§3 item 1** (the `scope` member) — the
  **only** place `NextAttack` is defined, and it defines it as a **storage
  token, not a runtime rule**: "`scope` (string) — on a `Crit` element: which
  damage instances the increase applies to. The defined value is `NextAttack`.
  `value` on a `Crit` element is the **increase in percentage points**."
  The section then states the decisive boundary: "`duration` and `scope` are
  **storage members for rules the owning domain documents already state**; they
  author no gameplay. ... Crit's next-attack scope is owned by `CARD_RULES.md`
  §4.1 (`PASSIVE_RULES.md` §7 uses the same scope for Bạch Hổ's Passive). No
  further extra member is defined, and none may be added without a recorded
  owner decision." **This sentence is why an agent cannot close the gap: the
  member is explicitly declared to carry no gameplay, and the document it
  delegates to does not define the rule either.**
- `docs/01-game-design/CARD_RULES.md` **§4.1** — Iron Fang ("Deal 120 damage
  ...; increase Crit chance by 10 percentage points for the next attack only")
  and the note that its Crit increase "is the Card's own value. It is
  **independent** of Bạch Hổ's Pet Passive configuration value ... the two are
  separate sources that both modify Crit chance (`COMBAT_RULES.md` §3.3 item 3)
  and neither is derived from, nor shared with, the other." **The owner of the
  NextAttack rule, which states the scope in prose ("for the next attack only")
  and defines no boundary, lifetime, or removal mechanism.**
- `docs/01-game-design/COMBAT_RULES.md` **§3.3** — Critical Hits. Item 1 fixes
  the pipeline position; item 2 the deterministic bounded roll procedure; item 3
  the multiplier; item 4 the scope ("every damage instance traversing the Damage
  Pipeline, including Player → Boss damage, Boss → Pet damage, and
  damage-over-time ticks"); **item 5 "Modifier Sources"** names exactly three
  independent sources — "Relics (e.g. 'Assassin Eye': Combo ≥ 3 → increase Crit
  chance), Pet Passives (e.g. Bạch Hổ: next attack gains increased Crit chance),
  and Cards (e.g. Iron Fang)" — **but defines no composition operator, no
  ordering, no cap, and no removal**; item 6 fixes the result representation
  (`otherModifiers` only). **§3.3 item 5 names three sources and composes none
  of them: this is Decision C's gap.**
- `docs/01-game-design/COMBAT_RULES.md` **§5.1 / §5.2 / §5.3** — the Status
  Effect rules. §5.1's MVP list is "Burn / Shield / Buff/Debuff"; §5.2 item 1
  gives every Status Effect "a duration (in Turns) or a trigger-based expiry";
  §5.2 item 2 fixes the MVP stacking default ("refresh duration, do not stack
  magnitude"); §5.3 (DR1–DR6) is the **canonical owner** of Turn-based duration
  consumption and defines it entirely in terms of the step-19a countdown.
  **§5.3.2 scopes that rule to "all Turn-based Buff/Debuff Status Effects".**
- `docs/01-game-design/PASSIVE_RULES.md` **§7 / §8** — Bạch Hổ's Passive ("Next
  attack gains increased Crit chance"), the second documented source carrying
  the same `NextAttack` semantics, and §8's note that "Exact numeric effect
  magnitudes (... Crit increase %) are balance values and live in config, not in
  this document". **This is Decision D's second source, and it is documented as
  a Passive — i.e. its modifier is not produced by a Card cast.**
- `docs/01-game-design/GAME_RULES.md` **§17** — the canonical resolution order
  (steps 1–19), including **step 14 "Resolve Player Effects"**, **step 18
  "Resolve Boss Response"** with its 18a/18b/18c expansion (Boss Passive, Boss
  Skill, Boss Attack), and **step 19 "End Turn"** with **19a** (DoT tick and the
  single duration-consumption point). **One Swap therefore contains multiple
  distinct damage-instance sites — player damage (15–17), a possible Boss Skill
  or Boss Attack (18b/18c), and a possible Burn tick (19a) — and no document
  states which of them is "the next attack". This is Decision B's gap.**
- `docs/01-game-design/GAME_RULES.md` **§18** (server authority) and **§16**
  (canonical event list — no Crit event, no status-application event).

### The state model the answer must reconcile with

- `docs/02-technical/GAME_STATE.md` **§2.3.1** — the `StatusEffect` instance
  schema: `Id` / `Type` / `Source` / `Magnitude` / `TargetStat?` /
  `RemainingTurns?` / `ExpiryCondition?`. Item 3 is decisive: "**`Type` selects
  exactly one duration model, and the two are exclusive.** An instance uses
  **either** the Turn countdown (`RemainingTurns`, item 4) **or** a
  trigger-based expiry (`ExpiryCondition`, item 5) — **never both and never
  neither**." Item 3's bullets assign `RemainingTurns` to `DoT` and
  `BuffDebuff` and `ExpiryCondition` to `Shield` alone. Item 5 makes
  `ExpiryCondition` "a condition label, not a rule". Item 6: "**There is never
  more than one instance per effect identity per entity.**" Item 7 fixes the
  **`TargetStat`-iff-`BuffDebuff`** pairing. **A `NextAttack` Crit modifier is
  the exact shape item 3's dichotomy cannot express: it is a stat modification
  (so `BuffDebuff`), but it is not Turn-based and its trigger is not
  Shield depletion.**
- `docs/02-technical/GAME_STATE.md` **§2.3.3** — "What This Model Does Not
  Add". It explicitly forbids the obvious escape hatch: "No
  `PendingStatusEffects[]`, no queued/pending application collection, and no
  second representation of an in-flight application: an application during a
  resolution is Transient Resolution State (§3) until the write-back (§5.1
  item 2)." **A pending queue is therefore not available as an answer.**
- `docs/02-technical/GAME_STATE.md` **§5.1.1** — the lifecycle. Item 1 (Apply is
  a "set", not an increment), item 2 (**exactly one** decrement per Turn, at
  step 19a), item 4 (expiry is a removal, never a stored zero), item 6 (the
  pass order is by `Id` ordinal ascending, and is "part of the contract"),
  item 7 ("**Trigger-based instances are not decremented.** ... The step 19a
  pass must not invent a duration for it"), item 10 ("Nothing here is
  published. This lifecycle adds no event, no payload member, and no SignalR
  method"). **The lifecycle defines consumption only for Turn-countdown
  instances and Shield's own depletion trigger; it defines no consumption path
  for a modifier consumed by an attack.**
- `docs/02-technical/GAME_STATE.md` **§2.3** / `PetState` — `Crit` is listed
  among the combat stats as a Crit percentage, initialized at battle creation to
  the `COMBAT_RULES.md` §1.1 MVP default (`5`). §2.3 states these defaults "are
  not permanent invariants" and that `Crit` "is the percent §1.1 defines ..., so
  the value is `5` and not `0.05`". **The document defines `Crit` as a stat with
  a unit and an initial value. It never says whether `Crit` is the composed
  runtime value or a base value, and it defines no modifier list, no
  composition, and no removal. This is Decision C's gap.**
- `docs/02-technical/GAME_STATE.md` **§5.1** — the single atomic write-back: a
  resolution's intermediate values are not observable, and the committed
  `BattleState` is written once. **Any NextAttack modifier's apply/consume must
  therefore be expressible within one resolution's write-back.**
- `docs/02-technical/DATABASE.md` **§1 / §3** — the structured
  `CardEffectDefinitions` storage contract and its six encoded rows, including
  Iron Fang's `{ "effectType": "Crit", "valueType": "PercentagePoints",
  "value": 10, "scope": "NextAttack" }`. Item 1's closing note (quoted above) and
  item 3 ("Ordering within the array is NOT semantic") fix what storage does and
  does not mean. **Storage is already complete; only the runtime rule is missing.**

### The implementation evidence (READ ONLY — states what exists, not what is correct)

- `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs` — the Pet Skill Card
  resolver. `case CardEffectType.Crit:` computes `newCrit += critAmount` and
  writes `Crit = newCrit` into `PetState` (line 133, line 227). **It creates no
  `StatusEffect` instance and records no source.** Its Crit branch is therefore a
  direct mutation of the composed stat.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — the Swap
  resolution path. It contains the review-identified defect verbatim:
  "`// Consume NextAttack Crit modifier if active`" followed by
  `if (resolved.PetState.Crit != PetState.DefaultCrit)` → reset `Crit` to
  `PetState.DefaultCrit` (lines 1179–1186). **This resets the stat by comparing
  it to a default constant, which is source-blind: it cannot distinguish Iron
  Fang's modifier from Bạch Hổ's Passive, from a Relic's, or from a future
  change to the base value — and `AGENTS.md` §7 forbids shipping a guessed
  version of a missing rule.**
- `src/backend/GameServer.Domain/Battle/StatusEffect.cs`,
  `StatusEffectType.cs` — the Domain model. `StatusEffectType` is
  `DoT = 0 | BuffDebuff = 1 | Shield = 2 | State = 3`; `StatusEffect.TurnBased`
  rejects `Shield`, requires `TargetStat` **iff** `BuffDebuff`, and requires a
  duration `>= 1`; `StatusEffect.TriggerBased` accepts **only** `Shield`.
  **Consequently a `NextAttack` Crit modifier can be built as neither model
  today: the Turn-based factory would require a Turn duration the rule does not
  define, and the trigger-based factory rejects every non-Shield type.**
- `src/backend/GameServer.Domain/Cards/CardEffectDefinition.cs` —
  `NextAttackScope = "NextAttack"` is the **only** scope value the contract
  defines, and a `Crit` element must carry it. **The stored scope token is
  validated at the definition boundary and has no runtime consumer.**
- `src/backend/GameServer.Domain/Combat/DamagePipeline.cs` — the Step-4 region
  that reads the Crit stat (`AttackerCrit: state.PetState.Crit` is supplied by
  `CardCastExecutor`, line 178), post-TASK-114. **The pipeline reads whatever
  value `PetState.Crit` holds; it holds no notion of a source or a scope.**

**Read-only note.** These files are inspected to establish what exists. No file
under `src/` or `tests/` is modified by this task, and no finding below is
derived from what the code happens to do.

### Technical and governance contracts the answer must not contradict

- `docs/02-technical/TDD.md` **§6** — the single server-seeded PRNG; the answer
  must not introduce a second stream (`ADR-009`, `GAME_STATE.md` §2.6.3).
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.13** — `otherModifiers` as the
  sole combined step-4 carrier; **§4.2 / §4.3** — what the state push does and
  does not carry. TASK-113 D-4 fixed that a Crit is not separately observable.
  **The answer must not require a new wire member.**
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; the modifier's apply/consume is server-side.
- `docs/03-decisions/README.md` **§8** — "Known Open Items (Not ADRs)", which
  must be checked before asserting an ADR is needed.

### Scope, governance, and precedent

- `docs/00-overview/MVP_SCOPE.md` §1 (Cards, Pets/Pet Passive/Signature Skill,
  and Combat — `Crit` and `Status Effects` are named), §2 (OUT), §4 (unlisted is
  not implicitly IN).
- `docs/00-overview/ROADMAP.md` — Phase 1 names "One Pet fully implemented
  (Element, Passive, Signature Skill)"; "No Relics yet" (so Relic Crit is
  documented but not yet implemented content).
- `tasks/backlog/TASK-115-implement-server-authoritative-petskillcast-crit-and-burn.md`
  — the blocked downstream implementation task, and the review that stopped it.
  **IMMUTABLE; NOT modified by this task.**
- `tasks/completed/TASK-113-resolve-combat-rng-draw-contract.md` — the closest
  decision-capture precedent: it presents evidenced options as evidence, records
  the Product Owner answer verbatim, records a "Balance-Value Boundary", and
  reports (rather than authors) follow-up work. **IMMUTABLE; read-only.**
- `tasks/backlog/TASK-108-resolve-card-effect-resolution-contract.md` — the
  decision-capture precedent for how an answer is recorded verbatim and how a
  deferral is recorded as a deferral. Read-only.
- `tasks/completed/TASK-111-resolve-multi-effect-card-effectdefinition-contract.md`
  — the owner decision (D-3) that introduced the `scope` storage member. Read-only.
- `tasks/completed/TASK-105-change-shield-application-semantics-to-refresh.md`
  — the precedent for how a Status Effect whose expiry is trigger-based rather
  than Turn-based was decided and recorded. Read-only.
- `AGENTS.md` §4 (conflict resolution), §7 (never invent a rule or content),
  §9 (anti-overengineering), §10 (server authority), §11 (Determinism & RNG),
  §15 (testing), §17 (documentation change rule), §18 (ADR rule), §20 (stop
  conditions — "Missing rule", "Ambiguous requirement");
  `.ai/workflow/documentation/documentation-change.md` §2 (no duplication) and
  §3 (determining the canonical owner); `tasks/README.md` §9 (no business-rule
  duplication in task files), §12 (skill budget).

**ADR check (to be confirmed, not assumed, by the executing agent):** this task
is expected to require **no** ADR if the answer fits inside the existing
`StatusEffects[]` model or the existing Crit stat, because either is a rule
detail rather than a change to the authoritative model. It **would** require one
if the answer introduces a new battle-state concept (a modifier collection, a
source registry, or a per-source Crit representation) — that is a battle-state
model change under `AGENTS.md` §18 and `TASK_TYPES.md`'s ARCHITECTURE type, and
it is **reported** and becomes a separate task, not authored here.
`docs/03-decisions/README.md` §8 must be checked before asserting an ADR is needed.

---

## Current State

The `NextAttack` Crit modifier contract does not exist. Verified directly in the
working tree:

```text
DATABASE.md §3 item 1          "NextAttack" IS defined — but as a STORAGE TOKEN:
                               "`scope` (string) — on a `Crit` element: which
                               damage instances the increase applies to. The
                               defined value is `NextAttack`."
                               and then explicitly: "`duration` and `scope` are
                               storage members for rules the owning domain
                               documents already state; they author no
                               gameplay. ... Crit's next-attack scope is owned
                               by CARD_RULES.md §4.1". The delegated owner does
                               not define the rule. Repo-wide, "NextAttack"
                               occurs in docs/ ONLY in DATABASE.md (§3 item 1
                               and the §1/§3 schema rows' notes) — never in
                               CARD_RULES.md, COMBAT_RULES.md, PASSIVE_RULES.md,
                               or GAME_STATE.md.

CARD_RULES.md §4.1             Iron Fang's Crit increase is "for the next attack
                               only" — PROSE. No definition of which damage
                               instance that is, when it expires, whether a
                               non-damaging action consumes it, or how it is
                               removed. The section's only further statement is
                               that Iron Fang's value is INDEPENDENT of Bạch
                               Hổ's — an independence claim, not a composition
                               rule.

COMBAT_RULES.md §3.3 item 5    Names THREE independent Crit sources (Relics, Pet
                               Passives, Cards) and defines NO composition
                               operator, NO ordering, NO cap, and NO removal.
                               Item 1 fixes the pipeline position; §3.1 fixes
                               that "modifiers within step 4 (multiple
                               Relics/Buffs) apply in the deterministic trigger
                               order defined in RELIC_RULES.md §4" — an ordering
                               rule for RELIC triggers, not a Crit composition
                               rule, and it does not cover Passive or Card Crit.

GAME_STATE.md §2.3.1 item 3    The StatusEffect duration model is a strict
                               DICHOTOMY: RemainingTurns (DoT, BuffDebuff,
                               State) XOR ExpiryCondition (Shield) — "never both
                               and never neither". A NextAttack modifier is
                               neither: it is a stat modification (so
                               BuffDebuff by item 7's TargetStat pairing) whose
                               trigger is not Shield depletion and whose
                               lifetime is not a Turn countdown.

GAME_STATE.md §2.3.3           FORBIDS the escape hatch: "No
                               PendingStatusEffects[], no queued/pending
                               application collection, and no second
                               representation of an in-flight application".

GAME_STATE.md §5.1.1           Defines consumption ONLY for Turn-countdown
                               instances (item 2/item 7) and Shield's own
                               depletion trigger. Item 7: "The step 19a pass
                               must not invent a duration for it." No
                               attack-consumption path is defined for any
                               instance.

GAME_STATE.md §2.3 (PetState)  `Crit` is a stat with a unit and an initial value
                               (5). No statement that it is composed or base;
                               no modifier list; no composition; no removal.

GAME_RULES.md §17              One Swap contains multiple damage-instance sites:
                               player damage (steps 15–17), a possible Boss
                               Skill or Boss Attack (18b/18c), and a possible
                               Burn tick (19a). No document says which is "the
                               next attack", and COMBAT_RULES.md §3.3 item 4
                               makes Crit apply to ALL of them.

src/.../CardCastExecutor.cs    `newCrit += critAmount` → `Crit = newCrit`.
                               Creates no instance, records no source.
src/.../BattleStateService.cs  `if (resolved.PetState.Crit != PetState.DefaultCrit)`
                               → reset to `PetState.DefaultCrit`. Source-blind;
                               resets by comparison to a default constant.
src/.../StatusEffectType.cs    DoT | BuffDebuff | Shield | State — a CLOSED set;
                               `TurnBased` rejects Shield and requires a
                               duration >= 1; `TriggerBased` accepts Shield only.
```

**The gap.** TASK-115 must resolve, for Iron Fang and Bạch Hổ:

```text
How is a NextAttack Crit modifier represented in Battle State?
        → NOT SPECIFIED. NextAttack is a storage token; §2.3.1's
          two-duration-model dichotomy cannot express it; §2.3.3 forbids a
          pending collection.

When does it expire / what ends it?
        → NOT SPECIFIED. It is not Turn-based, and its trigger is not
          Shield depletion.

Which damage instance is "the next attack"?
        → NOT SPECIFIED. §17 yields several damage-instance sites per Swap and
          §3.3 item 4 applies Crit to all of them.

How does it compose with other Crit sources, and how is it removed
without resetting them?
        → NOT SPECIFIED. §3.3 item 5 names three sources and composes none;
          §2.3 defines `Crit` but not as base-or-composed; removal is
          source-specific and undefined.
```

`CARD_RULES.md` §4.1's "for the next attack only" is a resolved **game intent**.
"A source-specific Crit modifier is represented, composed, and consumed
deterministically and reproducibly" is an **unresolved contract**. An agent
cannot close it by choosing a representation: `GAME_STATE.md` §2.3.1's
dichotomy is explicit, §2.3.3 forbids the pending collection that would be the
easy answer, and §2.3.3's closing note states that introducing another Status
Effect type "is a gameplay decision owned by `COMBAT_RULES.md`, not by this
state". Choosing composition arithmetic is equally unavailable: §3.3 item 5
names independent sources and defines no operator, and any choice silently
changes the meaning of the Crit stat every existing and future source reads.

**What this does NOT change.** Crit's pipeline position (step 4), roll procedure,
comparison operator, and multiplier are frozen by `COMBAT_RULES.md` §3.3 and
TASK-113 D-1/D-2/D-3 and must not be re-decided. Burn's tick timing, duration
model, and expiry are frozen by `GAME_RULES.md` §17 step 19a and
`COMBAT_RULES.md` §5.3 and must not be re-decided. Shield's trigger-based expiry
and refresh-not-stack semantics are frozen by `COMBAT_RULES.md` §4 and
`GAME_STATE.md` §2.3.1/§5.1.1. Crit's result representation is frozen by
TASK-113 D-4. The sole open questions are the **representation, lifetime, attack
boundary, and source composition** of a `NextAttack`-scoped modifier.

---

## Product Owner Decisions

<!--
  ANSWERED — Product Owner. These decisions are the deliverable of this task.
  An agent authored none of them (AGENTS.md §7).
-->

**All decisions in this section were supplied by the Product Owner. The
"Resulting Contract" below is the bindable statement of them.**

### D-1 — Representation

**Decision: represent a temporary NextAttack Crit modifier as a dedicated
source-specific collection on the authoritative `PetState`.**

```text
NextAttackCritModifiers[]
```

Each modifier must contain enough information to identify and remove its own
contribution independently.

```text
Do NOT represent NextAttack Crit by mutating the permanent/base
  `PetState.Crit`.
Do NOT represent NextAttack Crit through `PendingStatusEffects[]`.
Do NOT introduce a second queued/in-flight state representation.
```

### D-1a — Instance Identity

Each NextAttack Crit modifier has a **stable source/instance identity**
sufficient to remove exactly that modifier after consumption.

Consumption must not reset or remove unrelated Crit sources.

### D-2 — Lifetime

A NextAttack Crit modifier remains active **until the owner's next qualifying
attack consumes it**.

```text
It is NOT TurnBased.
It is NOT Shield-triggered.
It does NOT use `RemainingTurns`.
It does NOT use Shield depletion as its expiry condition.
```

### D-3 — Attack Boundary

A **qualifying attack** is an explicit owner attack action that enters the
Damage Pipeline.

The NextAttack Crit modifier is evaluated for that attack and **consumed once
for that attack**.

A raw damage instance is not itself a separate attack for NextAttack
consumption purposes.

### D-3a — Non-Damaging Action

A non-damaging action does **not** consume a NextAttack Crit modifier.

### D-3b — Multiple Damage Instances

If one qualifying attack action produces multiple damage instances, **all damage
instances belong to the same attack** for NextAttack consumption purposes.

The NextAttack Crit modifier is consumed at the **first qualifying damage
instance** of that attack.

It must **not** be consumed again by later damage instances belonging to the
same attack.

### D-4 — Crit Composition

Effective Crit is composed from the attacker's base/permanent Crit sources plus
all currently applicable temporary Crit modifiers.

```text
EffectiveCrit =
      BaseCrit
    + PassiveCrit
    + RelicCrit
    + applicable NextAttackCritModifiers
```

Only sources that are **active and applicable to the current attack** participate.

### D-4.1 — Base vs Composed Crit

`PetState.Crit` remains the **permanent/base** Crit value.

Temporary NextAttack modifiers **must not overwrite** `PetState.Crit`.

The runtime calculates the effective Crit value for the current Damage Pipeline
execution.

### D-4.2 — Composition Operator

Crit contributions are **additive**.

### D-4.3 — Source-Specific Contribution

Each Crit source contributes **independently**.

Removing or consuming one source must not reset or remove other Crit sources.

### D-4.4 — Cap

**Decision: the composed Crit value is capped at 100.**

**Recorded as a NEW authored value** (Product Owner confirmed on review). No
Crit cap exists anywhere in `docs/` today: `COMBAT_RULES.md` §1.1 lists a 0–100
range for `Power` only, and gives `Crit` just "critical hit chance (%) — MVP
default: 5%". The value `100` in the decision originates from the Crit **roll
bound** (`COMBAT_RULES.md` §3.3 item 2, $V \in [0, 100)$) — a bound on the
random draw, not a documented ceiling on the Crit stat. This decision therefore
**authors** the stat's range rather than confirming one.

This is recorded explicitly so no downstream task reads it as pre-existing
documentation, and so the owning document edit is not mistaken for a
restatement. See "Required Documentation Changes" for the owner.

### D-4.5 — Source Removal

NextAttack Crit consumption removes **only** the source-specific temporary
modifier(s) consumed by the qualifying attack.

```text
It must NOT reset `PetState.Crit`.
It must NOT remove Passive Crit.
It must NOT remove Relic Crit.
It must NOT remove unrelated temporary Crit sources not consumed by that attack.
```

### D-4.6 — Unit

Crit values are **percentage points**.

The existing bounded Crit procedure remains:

```text
V ∈ [0,100)

Crit succeeds iff:

V < EffectiveCrit
```

### D-5 — Multiple NextAttack Sources

Multiple active NextAttack Crit modifiers **stack additively**.

```text
They do NOT replace each other.
They remain source-specific so that consumption can remove their individual
  contributions correctly.
A qualifying attack consumes ALL applicable NextAttack Crit modifiers assigned
  to that attack.
```

### D-5.1–D-5.5 — Multiple Source Interaction

When multiple applicable NextAttack Crit sources are active for the same attack:

```text
1. All applicable source contributions are included in EffectiveCrit.
2. Contributions are summed additively.
3. The composed value is capped at 100.
4. The attack uses that composed value in the existing Crit pipeline.
5. The consumed NextAttack modifiers are removed after their qualifying attack.
6. Permanent/base Crit sources remain unchanged.
```

### Iron Fang × Bạch Hổ

If Iron Fang provides a NextAttack Crit modifier and Bạch Hổ provides its own
NextAttack Crit modifier for the same attack, **both modifiers apply
additively**.

```text
Base Crit      = 5
Iron Fang      = +10
Bạch Hổ        = +10

Effective Crit = 25
```

After the qualifying attack consumes both modifiers:

```text
Base Crit          = 5
Iron Fang modifier = removed
Bạch Hổ modifier   = removed
```

The permanent/base Crit remains unchanged.

### D-6 — DefaultCrit

`DefaultCrit` is an **initialization/default-state value only**.

It is **not** a runtime reset mechanism. Runtime consumption must never restore
Crit by assigning:

```text
PetState.Crit = PetState.DefaultCrit
```

### D-7 — Balance Values

Do not change any existing Crit balance value as part of TASK-116. (The D-4.4
cap is recorded as a **new authored value**, per the Product Owner's
confirmation; every other value in the D-7 balance table below remains
UNCHANGED.)

### D-8 — Preserved Constraints

```text
server authority                          AGENTS.md §10, GAME_RULES.md §18, ADR-001
existing Damage Pipeline                  COMBAT_RULES.md §3
existing bounded Crit RNG procedure       COMBAT_RULES.md §3.3 item 2,
                                          TASK-113 D-1/D-2
existing single RngState                  ADR-009, GAME_STATE.md §2.6.2
existing Burn Crit participation          COMBAT_RULES.md §3.3 item 4,
                                          TASK-113 D-3
CAS/retry semantics                       GAME_STATE.md §5.1, REDIS_STATE.md §4
no client-authoritative Crit state        AGENTS.md §10
no `PendingStatusEffects[]`               GAME_STATE.md §2.3.3
no new Redis schema                       REDIS_STATE.md
no new PostgreSQL schema                  DATABASE.md
no new SignalR method                     SIGNALR_PROTOCOL.md
no new dedicated Crit/NextAttack event    GAME_RULES.md §16, TASK-113 D-4
no new gameplay mechanic beyond the
  resolved NextAttack Crit contract       AGENTS.md §7
```

---

## Resulting Contract (Deterministic, Implementation-Ready)

<!--
  The bindable statement of the Product Owner's decisions above. Each rule
  traces to a decision; none is authored here.
-->

### C-1. Representation

A temporary NextAttack Crit modifier is held in a **dedicated, source-specific
collection on `PetState`**:

```text
PetState.NextAttackCritModifiers[]
```

- Each element is one modifier, carrying a **stable source/instance identity**
  and its magnitude. The identity is what makes **source-specific removal**
  (C-6) possible. (D-1, D-1a)
- Each element carries a Crit increase in **percentage points**. (D-4.6)
- The collection is **empty** when no modifier is active — the same
  always-present-collection convention `GAME_STATE.md` §2.3.2 item 1 fixes for
  `StatusEffects[]`.
- A modifier is **not** a `StatusEffect` instance. It does not use
  `RemainingTurns` and does not use `ExpiryCondition`/Shield depletion, so it
  does **not** engage `GAME_STATE.md` §2.3.1 item 3's duration-model dichotomy
  and does **not** require that rule to be widened. (D-2)
- `PendingStatusEffects[]` is **not** used, and no second queued or in-flight
  representation is introduced. (D-1, D-8)

### C-2. Lifetime and Expiry

- A modifier remains active **until the owner's next qualifying attack consumes
  it**. (D-2)
- It is not TurnBased; it carries no `RemainingTurns` and is **not** decremented
  by the step-19a pass. (`GAME_STATE.md` §5.1.1 item 7's "the step 19a pass must
  not invent a duration for it" is respected.) (D-2)
- It is not Shield-triggered and does not use Shield depletion as its expiry. (D-2)
- Consequence: an unconsumed modifier **persists across Turns** until a
  qualifying attack occurs. (D-2, read with D-3a)

### C-3. Attack Boundary (Consumption)

- A **qualifying attack is an explicit owner attack action that enters the
  Damage Pipeline**. (D-3)
- A raw damage instance is **not** itself a separate attack for NextAttack
  consumption purposes. (D-3)
- A **non-damaging action does not consume** a modifier. (D-3a)
- If one qualifying attack action produces **multiple damage instances, all of
  them belong to the same attack**; the modifier is consumed at the **first
  qualifying damage instance** of that attack and **must not be consumed again**
  by later instances of the same attack. (D-3b)
- Explicitly resolved consequence: because a Burn/DoT tick and the Boss's own
  attack are **not** the owner's qualifying attack action, they do not consume
  the modifier — even though `COMBAT_RULES.md` §3.3 item 4 makes them
  Crit-eligible and they therefore **do** participate in Effective Crit
  composition when applicable. (D-3, D-3a, D-4)

### C-4. Effective Crit Composition

```text
EffectiveCrit =
      BaseCrit                                  (PetState.Crit, permanent)
    + PassiveCrit                               (applicable, active)
    + RelicCrit                                 (applicable, active)
    + applicable NextAttackCritModifiers[]       (summed)
```

- `PetState.Crit` **remains the permanent/base value** and is **never
  overwritten** by a temporary modifier. (D-4.1)
- The runtime calculates Effective Crit **for the current Damage Pipeline
  execution**. (D-4.1)
- Contributions are **additive**. (D-4.2)
- Each source contributes **independently**; removing one does not disturb the
  others. (D-4.3)
- Only sources that are **active and applicable to the current attack**
  participate. (D-4)
- The composed value is **capped at 100**. (D-4.4 — new authored value)

### C-5. Crit Roll

Unchanged from `COMBAT_RULES.md` §3.3 item 2 and TASK-113 D-1/D-2, with
`EffectiveCrit` substituted for the Crit stat:

```text
one bounded RNG selection over bound 100   →   V ∈ [0, 100)
Crit succeeds iff V < EffectiveCrit
```

Crit values are **percentage points**. (D-4.6)

### C-6. Source-Specific Removal

Consumption removes **only** the source-specific temporary modifier(s) consumed
by the qualifying attack.

```text
It must NOT reset `PetState.Crit`.
It must NOT remove Passive Crit.
It must NOT remove Relic Crit.
It must NOT remove unrelated temporary Crit sources not consumed by that attack.
```

(D-4.5)

### C-7. Multiple Simultaneous Sources

- Multiple active NextAttack Crit modifiers **stack additively** and **do not
  replace each other**. (D-5)
- They remain source-specific so consumption removes their individual
  contributions correctly. (D-5)
- A qualifying attack consumes **all applicable** NextAttack Crit modifiers
  assigned to that attack. (D-5)

### C-8. Iron Fang × Bạch Hổ

Both modifiers apply additively to the same attack: Base 5 + Iron Fang 10 +
Bạch Hổ 10 = **Effective Crit 25**; after the qualifying attack both modifiers
are removed and base Crit remains 5. (See the worked example under
"Iron Fang × Bạch Hổ" above.)

This preserves `CARD_RULES.md` §4.1's independence claim — the two remain
separate sources, neither derived from nor sharing the other — while defining
the composition `CARD_RULES.md` §4.1 and `COMBAT_RULES.md` §3.3 item 5 left
open.

### C-9. `DefaultCrit` Is Not a Reset Mechanism

`DefaultCrit` is an initialization value only. Runtime consumption must **never**
restore Crit by assigning `PetState.Crit = PetState.DefaultCrit`. (D-6, and the
D-6 recording below of the review-identified defect.)

### C-10. Preserved Constraints

All of D-8. In particular: no new Battle Event, no new SignalR method, no new RNG
stream, no client-authoritative Crit, no `PendingStatusEffects[]`, no new Redis or
PostgreSQL schema, and Crit remains inside Damage Pipeline step 4.

---

## Required Authoritative Result — Coverage

```text
 1. NextAttack Crit representation        → C-1   (PetState.NextAttackCritModifiers[])
 2. NextAttack lifetime/expiry            → C-2   (until consumed; not Turn/Shield)
 3. attack consumption boundary           → C-3   (owner attack action into pipeline)
 4. multiple damage instances             → C-3   (same attack; first instance;
                                                   not consumed again)
 5. Crit source composition               → C-4   (additive, capped 100)
 6. source-specific removal               → C-6
 7. multiple simultaneous NextAttack srcs → C-7   (additive, all consumed)
 8. interaction with Passive Crit         → C-4, C-8
 9. interaction with Relic Crit           → C-4, C-6
10. interaction with Card/Pet Skill Crit  → C-4, C-8
```

---

## Required Documentation Changes

<!--
  Per .ai/workflow/documentation/documentation-change.md §3, each concept has
  ONE canonical owner. These are the owning edits the decisions require. They
  are NOT performed by TASK-116 — see "Classification Outcome (AGENTS.md §18)".
-->

The Product Owner's answer introduces a **new battle-state concept**, so the
owning edits belong to the **new ADR's and a separate ARCHITECTURE task**, not
this task's. What follows is the required-edit register, reported for that task.

```text
CONCEPT                                CANONICAL OWNER               EDIT REQUIRED
-------------------------------------  ----------------------------  ----------------------------------
NextAttack modifier representation     GAME_STATE.md §2.3 (PetState) add
  (NextAttackCritModifiers[])            + a new §2.3.x subsection     NextAttackCritModifiers[]
                                                                       schema, identity, always-present
                                                                       collection, serialization note
NextAttack lifetime & attack boundary  COMBAT_RULES.md (new §3.3     define "qualifying attack",
  + consumption                          subsection or §1.1 note)      lifetime, first-instance rule
Crit source composition (additive)     COMBAT_RULES.md §3.3 item 5   extend item 5 from "names three
                                                                       sources" to a composition rule
Crit cap of 100  ← NEW AUTHORED VALUE  COMBAT_RULES.md §1.1          add a range to the Crit row
                                                                       (currently: "MVP default: 5%")
DefaultCrit is not a reset mechanism   GAME_STATE.md §2.3 / §5.1     state that the base value is
                                                                       never restored by assignment
Iron Fang × Bạch Hổ composition        CARD_RULES.md §4.1            reference the composition rule;
                                                                       do NOT restate it
```

**Duplication rule.** `documentation-change.md` §2 applies: the composition rule
is written **once**, in `COMBAT_RULES.md`, and `CARD_RULES.md` §4.1 only points
at it. `GAME_STATE.md` types the state and points at the owning gameplay rule —
it does not restate the boundary.

```text
NOT required to change:
  docs/02-technical/DATABASE.md          `scope` = "NextAttack" storage token is
                                         already correct and complete; §3 item 1's
                                         "owned by CARD_RULES.md §4.1" pointer may
                                         be re-pointed to COMBAT_RULES.md if the
                                         owner moves — REPORTED, not assumed.
  docs/02-technical/SIGNALR_PROTOCOL.md  no wire change (D-8); the new collection
                                         is state, not a wire member, exactly as
                                         StatusEffects[] is (GAME_STATE.md §2.3.1)
  docs/01-game-design/GAME_RULES.md §16  no event change (D-8)
  docs/02-technical/REDIS_STATE.md       no new key (D-8) — the collection rides
                                         the existing BattleState write-back
  docs/02-technical/DATABASE.md          no new column (D-8)
```

---

## Classification Outcome (`AGENTS.md` §18)

**This task's own "Type Re-Classification Condition" fired, and this task also
triggers `AGENTS.md` §18.**

D-1 introduces `NextAttackCritModifiers[]` — a **new authoritative battle-state
concept**, which `AGENTS.md` §18 lists as requiring an ADR ("changing … the
battle-state model"). `TASK_TYPES.md` §2 likewise makes a change requiring a
new/updated ADR an `ARCHITECTURE` task.

**Product Owner ruling (on review):** TASK-116 **remains `DOCUMENTATION`** and
records the decision; the ADR and the state-model documentation are a
**separate `ARCHITECTURE` task**.

```text
TASK-116  (this task, DOCUMENTATION)   records the decisions
        ↓
TASK-117  (ARCHITECTURE)               authors ADR-017 + the GAME_STATE.md /
                                       COMBAT_RULES.md / CARD_RULES.md edits
        ↓
TASK-115  (FEATURE)                    implements against the frozen contract
        ↓
regression tests → review
```

**ADR numbering.** The next sequential ADR is **ADR-017** (highest existing is
ADR-016; `docs/03-decisions/README.md` §7's index ends at ADR-016).

**Not re-typed to `GAMEPLAY-CHANGE`.** D-1's representation does **not** widen
`GAME_STATE.md` §2.3.1 item 3's two-duration-model dichotomy and does **not**
relax item 6's one-instance-per-identity rule — the new collection is separate
from `StatusEffects[]` (C-1). So the second re-classification trigger does not
fire, and the `DOCUMENTATION` classification holds for the decision-record act.

**Not deferred to a balance task.** D-4.4's cap is a **new authored value**, but
the Product Owner supplied it as part of this decision set, so it is recorded
here and carried into TASK-117's owning edit. It is flagged as newly authored so
no downstream reader mistakes it for pre-existing documentation.

---

## Decision Inputs (Retained as Evidence)

<!--
  RETAINED AS EVIDENCE — the pre-decision analysis the Product Owner decided
  against. Retained per the TASK-113 precedent so the reasoning is auditable.
  The "Product Owner Decisions" section above supersedes any open question
  framed here, and the option lists below were NOT ranked or preselected.

  A PRODUCT OWNER / HUMAN must answer these. An agent must NOT answer them.
  Choosing, recommending, ranking, or defaulting any answer is the single
  prohibited action of this task (AGENTS.md §7, §20).

  Each item states the question, the documented evidence for and against each
  viable option, and what the option would cost downstream. THE OPTIONS ARE
  PRESENTED AS EVIDENCE, NOT AS A RECOMMENDATION. An agent must not rank them.
-->

### D-1 — How is a `NextAttack` Crit modifier represented in authoritative Battle State?

```text
Question: A source-specific Crit modification scoped to `NextAttack`
          (DATABASE.md §3 item 1) is stored on the Card definition. HOW is it
          represented at runtime, in `BattleState`, between the moment its
          source resolves and the moment an attack consumes it?

Evidence: DATABASE.md §3 item 1 defines the token and then explicitly declines
          to give it meaning: "they author no gameplay ... Crit's next-attack
          scope is owned by CARD_RULES.md §4.1". CARD_RULES.md §4.1 states the
          scope in prose only. There is no runtime representation anywhere.

          The existing candidates, and why each is not currently available:
            - StatusEffect, Type = "BuffDebuff": the type exists and its
              TargetStat member is exactly the "which stat is modified" shape a
              Crit modifier needs (GAME_STATE.md §2.3.1 item 7). BUT item 3
              forces a BuffDebuff onto the Turn countdown (RemainingTurns), and
              a NextAttack modifier's lifetime is an attack, not Turns; item 6
              permits only ONE instance per Id, so two simultaneous NextAttack
              Crit sources cannot both be held under one Id.
            - StatusEffect, Type = "Shield"-style trigger expiry: item 3 assigns
              the trigger-based model to Shield ALONE, and the trigger-based
              factory rejects every other type (StatusEffect.cs). ExpiryCondition
              is a "condition label, not a rule" (item 5).
            - A pending/queued collection: EXPLICITLY FORBIDDEN by §2.3.3 ("No
              PendingStatusEffects[], no queued/pending application collection,
              and no second representation of an in-flight application").
            - Directly mutating `PetState.Crit`: what TASK-115's code does today
              (`newCrit += critAmount`). It is source-blind, cannot be removed
              selectively, and is reset by comparison to `DefaultCrit`.

Consequence: this is the highest-stakes decision. It determines whether the
          existing `StatusEffects[]` model can carry the concept at all, or
          whether a new battle-state concept is required — and a new concept is
          a battle-state model change (AGENTS.md §18), which is REPORTED here
          and becomes a separate ARCHITECTURE task, not an act of this task.
```

**Candidate options (evidence only — NOT a recommendation, NOT a ranking):**

```text
Option A — Extend the existing StatusEffect model with a documented
           NextAttack lifetime/expiry semantic.
    Would require reconciling GAME_STATE.md §2.3.1 item 3's exclusive
    dichotomy with a third lifetime model, or defining the attack boundary as
    a trigger-based expiry analogous to Shield's. Item 3, §2.3.3's closing
    note ("Introducing another [Status Effect type] is a gameplay decision
    owned by COMBAT_RULES.md"), and item 6's one-instance-per-Id rule are the
    constraints this option must answer. Effect: keeps ONE representation of
    status-like state; may require documenting a third lifetime model or
    re-opening item 3.

Option B — Define another existing-state-compatible representation.
    E.g. a NextAttack-scoped modifier carried as a distinguished instance
    under the existing schema, keyed by source identity so that item 6's
    uniqueness rule still holds and two sources can coexist. Constraint: must
    not become a "second representation of an in-flight application"
    (§2.3.3) and must state its consumption point (§5.1.1 defines none for a
    non-Turn-based, non-Shield instance). Effect: no new top-level concept;
    requires precise new wording in §2.3.1/§5.1.1.

Option C — Redesign Crit modifiers as an explicit source-based composition
           model.
    E.g. define `PetState.Crit` explicitly as a BASE value and introduce a
    documented, source-keyed modifier composition that step 4 reads, so
    removal is source-specific by construction. Effect: makes composition and
    source-specific removal first-class (answering D-3 directly), but
    `GAME_STATE.md` §2.3 currently defines `Crit` without saying whether it is
    base or composed, so this option must say so explicitly; it is the option
    most likely to require an ADR (battle-state model, AGENTS.md §18) and
    therefore most likely to become a separate task.

Option D — An answer this list does not anticipate.
    The Product Owner is not limited to the above. If the answer requires a
    new battle-state concept, that requirement is recorded and REPORTED as a
    separate ARCHITECTURE/ADR task (AGENTS.md §18) — it is not authored here.
```

```text
Open sub-question D-1a (representation): if the modifier is held as a
StatusEffect instance, what is its `Id`? GAME_STATE.md §2.3.1 item 1 makes `Id`
"an identity, not a definition" and item 6 makes it the uniqueness key. Two
simultaneous NextAttack Crit sources (D-4) must both be representable, so
either their identities differ, or item 6's uniqueness rule must be documented
as not applying here. Neither is documented.
```

### D-2 — What ends a `NextAttack` Crit modifier (lifetime and expiry)?

```text
Question: A NextAttack modifier is not Turn-based (COMBAT_RULES.md §5.3.2
          scopes the countdown to Turn-based Buff/Debuffs) and its trigger is
          not Shield depletion. So what consumes or expires it, and what is
          its state if no qualifying attack ever occurs?

Evidence: COMBAT_RULES.md §5.2 item 1 requires every Status Effect to have "a
          duration (in Turns) or a trigger-based expiry". GAME_STATE.md §2.3.1
          item 3 makes those two exhaustive ("never both and never neither")
          and §5.1.1 item 7 states the step 19a pass "must not invent a
          duration for it". COMBAT_RULES.md §5.3 DR1–DR5 govern Turn-based
          duration only. No document states whether a NextAttack modifier
          survives an indefinite number of Turns, survives a non-damaging
          action, or is discarded at any boundary.

Consequence: without this, the modifier either leaks forever (a permanent Crit
          buff, which is not what "the next attack only" says) or expires on an
          invented boundary. Both are guessed gameplay rules.
```

### D-3 — Which damage instance is "the next attack"?

```text
Question: GAME_RULES.md §17 gives one Swap several sites that can produce
          damage. Which one is the "next attack" that consumes a NextAttack
          Crit modifier?

Evidence: GAME_RULES.md §17's canonical order contains, in one Swap:
            step 14  Resolve Player Effects   (CARD_RULES.md §6: CardCast /
                                               PetSkillCast fire here)
            steps 15–17  Calculate Damage / Apply Element Modifier /
                                               Apply Final Damage
            step 18  Resolve Boss Response → 18b Boss Skill (may deal damage)
                                              18c Boss Attack (deals damage)
            step 19a Tick Status Effects  (Burn DoT damage, COMBAT_RULES.md
                                               §5.2 item 3)
          COMBAT_RULES.md §3.3 item 4 makes Crit apply to EVERY damage instance
          traversing the pipeline, explicitly including "Player → Boss damage,
          Boss → Pet damage, and damage-over-time ticks (Burn ...)". So all of
          the above are Crit-eligible, and none is documented as "the next
          attack". CARD_RULES.md §4.1 says only "for the next attack only".
          CARD_RULES.md §3 item 5 additionally establishes that "Casting a Card
          does NOT consume a Turn" — so a Card cast resolves at step 14, and the
          Swap's own damage follows in the same resolution.

Consequence: this determines whether a NextAttack buff is consumed by the very
          action that granted it (Iron Fang deals 120 damage and grants the
          buff in the same cast), by that Swap's player damage, by the Boss's
          response, by a Burn tick, or by none until a later Swap. It also
          determines whether the Boss taking burn damage at step 19a consumes a
          player-scoped buff, and whether a single Swap can consume the same
          modifier more than once.
```

**Candidate semantics (evidence only — NOT a recommendation, NOT a ranking):**

```text
(i)    Player direct damage only.
       The modifier is consumed by the next player-originated damage instance
       (steps 15–17) and is untouched by Boss Response or Burn ticks.
(ii)   First applicable damage instance.
       The modifier is consumed by whichever damage instance next traverses the
       pipeline, in §17 order — which could be the Boss's own attack.
(iii)  Entire attack action.
       The modifier applies to every damage instance the consuming action
       produces and is consumed once, at the action's end.
(iv)   All damage instances generated by the attack, until the boundary.
       A variant of (iii) scoped to a named boundary the answer must define.
(v)    An answer this list does not anticipate.
```

```text
Open sub-questions D-3a / D-3b: does a NON-DAMAGING action (a Basic Heal or
Shield Card cast, a Power Charge, or a Swap that produces no damage) consume
the modifier? And can one Swap consume it more than once (e.g. by both the
player's damage and a Burn tick)? Neither is documented.
```

### D-4 — How do the Crit sources compose?

```text
Question: COMBAT_RULES.md §3.3 item 5 names three independent Crit sources —
          Relics, Pet Passives, and Cards. How do they combine into the Crit
          value step 4 reads, and is `PetState.Crit` that composed value or a
          base?

Evidence: COMBAT_RULES.md §3.3 item 1 says the roll compares against "the
          current Crit stat ... from `PetState.Crit`". §3.3 item 5 names the
          three sources and defines no operator. GAME_STATE.md §2.3 lists
          `Crit` as a combat stat with an MVP default and a unit, and never
          says whether it is base or composed. CARD_RULES.md §4.1 states only
          that Iron Fang's value and Bạch Hổ's value are "separate sources ...
          neither is derived from, nor shared with, the other" — an
          independence claim, which does not resolve additivity.
          COMBAT_RULES.md §3.1 fixes an ordering for "modifiers within step 4
          (multiple Relics/Buffs)" by reference to RELIC_RULES.md §4's
          deterministic TRIGGER order — that is a Relic trigger ordering, and
          it does not define a Crit composition rule for Passive or Card Crit.

Consequence: the answer fixes the observable Crit chance for every
          combination, and decides whether a temporary modifier can be removed
          without disturbing the base, Passive, and Relic contributions.
```

**Sub-questions the answer must settle explicitly:**

```text
D-4.1  Is `PetState.Crit` the COMPOSED runtime value, or a BASE/current value
       with modifiers applied elsewhere (at step 4 read time)?
D-4.2  How are multiple independent Crit modifiers combined — additive
       percentage points, multiplicative, or another rule?
D-4.3  What exactly does the roll compare against (COMBAT_RULES.md §3.3
       item 2: "V < current Crit stat")?
D-4.4  What happens when multiple modifiers target Crit simultaneously?
D-4.5  Is REMOVAL source-specific — i.e. can one modifier be removed without
       resetting the others or the base?
D-4.6  Is there a Crit chance CAP or clamp? No document states one, and
       `GAME_STATE.md` §2.3 explicitly declines to enforce the stat's range.
D-4.7  Are modifiers additive PERCENTAGE POINTS (the unit CARD_RULES.md §4.1
       and DATABASE.md §3 item 1 both use for `value`)?
```

### D-5 — Multiple simultaneous `NextAttack` sources: Iron Fang × Bạch Hổ

```text
Question: PASSIVE_RULES.md §8 gives Bạch Hổ a Passive whose effect is "Next
          attack gains increased Crit chance", and CARD_RULES.md §4.1 gives
          Iron Fang "increase Crit chance by 10 percentage points for the next
          attack only". Both are NextAttack-scoped Crit increases. What happens
          when BOTH are active at once?

Evidence: CARD_RULES.md §4.1's closing note is explicit that they are
          separate, independent sources: "It is independent of Bạch Hổ's Pet
          Passive configuration value (PASSIVE_RULES.md §7/§8) — the two are
          separate sources that both modify Crit chance (COMBAT_RULES.md §3.3
          item 3) and neither is derived from, nor shared with, the other."
          PASSIVE_RULES.md §8 states Bạch Hổ's numeric Crit increase is a
          config balance value and is not stated in that document.
          GAME_STATE.md §2.3.1 item 6 permits only ONE instance per Id per
          entity, which is the direct state-level obstacle to holding two.

Consequence: the independence claim rules out deriving one from the other, but
          it does not say whether both apply to the SAME attack, whether they
          stack, whether one is consumed before the other, or whether both are
          consumed by one attack. Note also that the two sources are produced at
          DIFFERENT resolution points: a Card at step 14 (CARD_RULES.md §6) and
          a Passive at step 10 (GAME_RULES.md §17 "Charge Passive",
          PASSIVE_RULES.md §7) — so "which is consumed first" is not answerable
          from resolution order alone.
```

**Sub-questions the answer must settle explicitly:**

```text
D-5.1  Do both modifiers apply to the same attack?
D-5.2  If yes, are they additive (and in which unit)?
D-5.3  Does one consume before the other, or are both consumed by one attack?
D-5.4  Can different sources carry different priority?
D-5.5  If Bạch Hổ's Passive triggers and Iron Fang is cast before the
       consuming attack, is the result the same as the reverse order?
```

### D-6 — Recorded Consequence: the `PetState.DefaultCrit` Reset Is Not an Answer

Recorded here because TASK-115's code contains a source-blind reset that must
not be carried forward as a de facto contract.

```text
BattleStateService.cs (lines 1179–1186):
    // Consume NextAttack Crit modifier if active
    if (resolved.PetState.Crit != PetState.DefaultCrit)
    {
        resolved = resolved with
        {
            PetState = resolved.PetState with { Crit = PetState.DefaultCrit },
        };
    }
```

This compares the Crit stat to a **configuration default constant** and resets it.
It is not a representation, a lifetime, or a consumption rule:

```text
- It cannot distinguish Iron Fang's modifier from Bạch Hổ's Passive, from a
  Relic's, or from any other source (COMBAT_RULES.md §3.3 item 5 names three).
- It silently couples "no modifier active" to "Crit equals the MVP default
  constant", which GAME_STATE.md §2.3 states is CONFIGURATION and "not a
  permanent invariant" — so a balance change to the default would change this
  logic's meaning.
- It consumes at the Boss-damage site, which is a guess at D-3's boundary.
- `PetState.DefaultCrit` is documented (PetState.cs) purely as the INITIAL
  value of a created battle, not as a resting value to restore.
```

**This task records that the reset is not the answer. It does not fix it, and it
does not authorize a different guess in its place.** TASK-115's correction is
downstream of this contract.

### D-7 — Balance-Value Boundary (recorded, not to be crossed)

```text
These decisions define RUNTIME REPRESENTATION, LIFETIME, BOUNDARY, and
COMPOSITION rules only. Every balance value remains owned by its current
authoritative document.
```

```text
Crit chance default (5%)             COMBAT_RULES.md §1.1          UNCHANGED
Crit multiplier (1.5×)               COMBAT_RULES.md §3.3 item 3   UNCHANGED
Crit pipeline position (step 4)      COMBAT_RULES.md §3.3 item 1   UNCHANGED
Crit roll procedure / comparison     COMBAT_RULES.md §3.3 item 2   UNCHANGED
                                     (TASK-113 D-1)
Crit draw point and count            COMBAT_RULES.md §3.3 item 2   UNCHANGED
                                     (TASK-113 D-2)
Crit result representation           COMBAT_RULES.md §3.3 item 6   UNCHANGED
  (otherModifiers only; no event)    (TASK-113 D-4)
Iron Fang Crit +10 pct points        CARD_RULES.md §4.1            UNCHANGED
Iron Fang Damage 120                 CARD_RULES.md §4.1            UNCHANGED
Bạch Hổ Passive Crit increase        PASSIVE_RULES.md §8 (config)  UNCHANGED
Bạch Hổ Threshold 4 Matches          PASSIVE_RULES.md §8            UNCHANGED
Assassin Eye Crit increase           RELIC_RULES.md §5             UNCHANGED
Burn / Shield / Heal magnitudes      CARD_RULES.md §4.1            UNCHANGED
```

No balance value is authored, changed, or confirmed by TASK-116.

### D-8 — Constraints the Answer Must Preserve

```text
Server-authoritative state                     GAME_RULES.md §18, ADR-001
No client-authoritative Crit state             AGENTS.md §10
No new SignalR method                          TASK-115 Out of Scope
No undocumented Battle Event                   GAME_RULES.md §16
No second RNG stream                           ADR-009, TDD.md §6
Crit remains Damage Pipeline step 4            COMBAT_RULES.md §3.3 item 1
Burn remains step 19a via the pipeline         GAME_RULES.md §17 step 19a
EffectDefinition[] remains the structured
  source of Card/Pet Skill effects             DATABASE.md §1/§3
No PendingStatusEffects[] or queued
  application collection                       GAME_STATE.md §2.3.3
No undocumented TargetStat or Type value       GAME_STATE.md §2.3.1 items 3, 7
```

---

## Scope

### In Scope

1. Present the four contract gaps (D-1 representation, D-2 lifetime, D-3 attack
   boundary, D-4/D-5 composition and multiple sources) with their documented
   evidence, clearly separating **explicit contract**, **inference**, and
   **missing contract**.
2. Present candidate options as evidence only, without ranking or defaulting.
3. Obtain the human/Product-Owner answer for each decision point, or an explicit
   recorded deferral.
4. Record each settled answer **verbatim** in this task file, in the form
   TASK-113 used for D-1–D-5.
5. Identify, for each answer, the single **canonical owner** document that must
   carry it — per `.ai/workflow/documentation/documentation-change.md` §3 — and
   report whether a separate documentation-synchronization task is required.
6. Check `docs/03-decisions/README.md` §8 and report whether any answer requires
   an ADR.
7. Record the exact downstream handoff to TASK-115.

### Out of Scope

- **Any source code change.** Zero files under `src/` or `tests/`.
- **Modifying TASK-115.** It stays byte-identical and blocked.
- **Implementing TASK-115's correction**, or any part of it.
- Implementing any representation, storage, pipeline, executor, or service change.
- Authoring any balance value, or changing any value in D-7.
- Choosing a representation, lifetime, boundary, composition operator, ordering,
  or priority on the agent's own authority.
- Introducing a new Battle Event, SignalR method, wire member, or RNG stream.
- Introducing a new Status Effect type or a new battle-state concept **without**
  the recorded Product Owner decision and, if required, its separate ADR task.
- Fixing the `PetState.DefaultCrit` reset (D-6) — recorded, not fixed.
- Relic trigger evaluation (`ROADMAP.md` — "No Relics yet").
- Pet Passive implementation, Boss AI, Match-3, or frontend work.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Affected Files & Areas

```text
[ ] src/backend/ (NONE — no source code modified)
[ ] src/frontend/client/ (NONE)
[ ] tests/ (NONE)
[x] tasks/backlog/TASK-116-...md (this task file — the decision record)
[ ] docs/01-game-design/CARD_RULES.md   (candidate owner: the NextAttack rule
                                          and Iron Fang's §4.1 wording)
[ ] docs/01-game-design/COMBAT_RULES.md (candidate owner: Crit source
                                          composition, §3.3 item 5; Status
                                          Effect lifetime, §5.2/§5.3)
[ ] docs/01-game-design/PASSIVE_RULES.md(candidate owner: Bạch Hổ's Passive
                                          Crit semantics, §7/§8)
[ ] docs/02-technical/GAME_STATE.md     (candidate owner: the §2.3.1 instance
                                          schema and §5.1.1 lifecycle, if the
                                          representation lives in
                                          StatusEffects[])
[ ] docs/02-technical/DATABASE.md       (ONLY if the recorded answer requires
                                          the `scope` storage note in §3 item 1
                                          to be re-pointed at a new owner)
[ ] docs/03-decisions/README.md / ADR/  (ONLY if the answer requires an ADR —
                                          REPORTED first, per AGENTS.md §18)
```

**The `[ ]` marks above are CANDIDATES, not a plan.** Which of them is actually
edited is determined by the recorded answer and by
`documentation-change.md` §3's canonical-owner rule — not decided in advance.
The default expectation is that **at most two** owner documents change.

---

## Implementation Notes

- This task produces **a recorded decision and nothing else** in its first pass.
  It follows `tasks/completed/TASK-113-resolve-combat-rng-draw-contract.md`'s
  shape: a "Decision Inputs" section per question, an "ANSWERED — Product Owner"
  block replacing each once answered, and retained evidence blocks marked
  `RETAINED AS EVIDENCE`.
- **Skill budget is 4** (`tasks/README.md` §12, Simple): `documentation-discovery`,
  `impact-analysis`, `documentation-consistency`, `scope-validation`. This task
  does not cross multiple uncoupled architectural boundaries, so no decomposition
  is required.
- `.ai/workflow/documentation/documentation-change.md` §2 governs every edit: one
  concept, one owner. Do not restate a decided rule in a second document; point
  at the owner.
- If the recorded answer requires a **new battle-state concept** (a modifier
  collection, a source registry, or a per-source Crit representation), that is an
  `AGENTS.md` §18 battle-state model change. **Report it and STOP** rather than
  editing `GAME_STATE.md` into a new model — the ADR and the model change are a
  separate task.
- If the recorded answer requires **widening `GAME_STATE.md` §2.3.1 item 3's
  two-duration-model dichotomy** or **relaxing item 6's one-instance-per-identity
  rule**, apply the "Type Re-Classification Condition" above and report the
  re-classification rather than proceeding under this task's `DOCUMENTATION` type.
- `AGENTS.md` §15 applies to any registered boundary: the record must be
  expressible as Given/When/Then. Report the required scenarios; do not author
  tests here.

---

## Acceptance Criteria

- [x] All four contract gaps (representation, lifetime, attack boundary, Crit
      source composition) are explicitly identified with file + section evidence.
- [x] Evidence from `CARD_RULES.md`, `COMBAT_RULES.md`, `PASSIVE_RULES.md`, and
      `GAME_STATE.md` is documented, each item classified as explicit contract,
      inference, or missing contract.
- [x] `NextAttack` Crit representation is explicitly defined by recorded Product
      Owner decision, not by an agent. (D-1 → C-1)
- [x] `NextAttack` lifetime/expiry semantics are explicitly defined. (D-2 → C-2)
- [x] The exact attack/damage consumption boundary is explicitly defined,
      including the non-damaging-action and multiple-instance sub-questions.
      (D-3, D-3a, D-3b → C-3)
- [x] Crit source composition is explicitly defined. (D-4, D-4.1–D-4.3 → C-4)
- [x] Source-specific removal is explicitly defined. (D-4.5 → C-6)
- [x] Multiple simultaneous `NextAttack` Crit sources are explicitly defined.
      (D-5, D-5.1–D-5.5 → C-7)
- [x] The Iron Fang × Bạch Hổ interaction is explicitly defined. (→ C-8)
- [x] The resulting contract is deterministic and implementation-ready: it
      answers every point in "Required Authoritative Result — Coverage" without
      reference to any implementation detail.
- [x] Each required owning edit names its single canonical owner document
      (`documentation-change.md` §3) — see "Required Documentation Changes".
- [x] The ADR requirement is explicitly reported: **required** — ADR-017, per
      `AGENTS.md` §18 (new battle-state concept). `docs/03-decisions/README.md`
      §8 checked; the gap was not listed there.
- [x] Whether a separate documentation-synchronization task is required is
      explicitly reported: **yes** — TASK-117 (ARCHITECTURE), because the
      owning edits cannot be authored under this task's `DOCUMENTATION` type or
      without the ADR.
- [x] Zero files under `src/` or `tests/` modified.
- [x] TASK-115 is byte-identical and explicitly identified as the downstream
      implementation task, still blocked.
- [x] No new SignalR method introduced.
- [x] No new Battle Event introduced.
- [x] No gameplay behavior implemented.
- [x] No **existing** balance value authored or changed (D-7). D-4.4's cap of
      `100` is recorded as a **new authored value** per Product Owner
      confirmation, and is flagged as such.
- [ ] Quality review checklist passes (`quality/review.md` §1), skipping
      code-only items per `documentation-change.md` §4.

---

## Required Authoritative Result

```text
SATISFIED — see "Required Authoritative Result — Coverage" for the mapping.
```

The completed decision defines a deterministic contract for all ten points,
implementable without guessing:

```text
 1. NextAttack Crit representation          → C-1
 2. NextAttack lifetime/expiry              → C-2
 3. attack consumption boundary             → C-3
 4. multiple damage instances               → C-3
 5. Crit source composition                 → C-4
 6. source-specific removal                 → C-6
 7. multiple simultaneous NextAttack sources→ C-7
 8. interaction with Passive Crit           → C-4, C-8
 9. interaction with Relic Crit             → C-4, C-6
10. interaction with Card/Pet Skill Crit    → C-4, C-8
```

---

## TASK-115 Handoff

```text
TASK-115 remains blocked until this contract and TASK-117's owning edits land.
```

The contract TASK-115 must implement is now **frozen** in "Resulting Contract"
(C-1 through C-10) above. TASK-115 **cannot** begin against it yet, because the
recording act was not the authoring act: `AGENTS.md` §18 requires the ADR and the
owning documentation before implementation (`AGENTS.md` §17 items 1–2).

```text
TASK-116  (this task, DOCUMENTATION)   records the decisions          ← DONE
        ↓
TASK-117  (ARCHITECTURE)               authors ADR-017 + the owning
                                       GAME_STATE.md / COMBAT_RULES.md /
                                       CARD_RULES.md edits
        ↓
TASK-115  (FEATURE)                    implements against the frozen contract
        ↓
regression tests
        ↓
review
```

**Do not implement TASK-115 as part of this task.** TASK-115 is not modified,
re-scoped, re-statused, or unblocked by TASK-116. It becomes implementable when
TASK-117's owning edits land.

**TASK-115's specific correction scope, once TASK-117 lands** (reported, not
implemented here):

```text
1. Remove the `PetState.DefaultCrit` reset in BattleStateService.cs
   (the review-identified defect; now forbidden by C-9 / D-6).
2. Stop `CardCastExecutor`'s Crit branch writing directly into
   `PetState.Crit` (`newCrit += critAmount`); it must instead add a
   source-identified modifier to `PetState.NextAttackCritModifiers[]` (C-1).
3. Compute Effective Crit additively, capped at 100, at the point the
   Damage Pipeline consumes it (C-4), leaving `PetState.Crit` untouched.
4. Implement the consumption boundary of C-3 — first qualifying damage
   instance of a qualifying owner attack action; not a Burn tick, not the
   Boss's attack, not a non-damaging action.
5. Remove only the consumed source-specific modifiers (C-6).
```

---

## Testing Requirements

### Required Verification

```text
[x] N/A — documentation/decision task. No code, no tests authored.
```

This task creates no executable verification (`.ai/workflow/documentation/documentation-change.md`
§4). Its "verification" is the set of Acceptance Criteria above plus
`quality/review.md`'s documentation-applicable items. The recorded contract must
nonetheless be stated so that a **later** task can derive Given/When/Then
scenarios from it per `AGENTS.md` §15; report the required scenario list as a
handoff item if it is not obvious from the recorded rules.

### Key Edge Cases

The recorded contract must be able to answer at minimum:

- Iron Fang cast, then the same Swap's player damage — is the modifier consumed
  by that damage, or does it survive to the next Swap?
- Iron Fang cast with no subsequent damage instance before End Turn.
- Bạch Hổ's Passive triggering in the same Turn Iron Fang is cast (D-5).
- Bạch Hổ's Passive alone, with no Card cast.
- A non-damaging action (Basic Heal / Shield / Power Charge) between the grant
  and the consuming attack (D-3a).
- A Swap in which player damage, a Boss response, and a Burn tick all occur
  (D-3, D-3b).
- Two `NextAttack` Crit sources active simultaneously (D-5.1–D-5.5).
- A Relic Crit source active alongside a `NextAttack` source (D-4, result point 9).
- Removal of one source leaving the base, Passive, and Relic contributions
  intact (D-4.5).
- Crit chance after composition, against the roll threshold of
  `COMBAT_RULES.md` §3.3 item 2 (D-4.3, D-4.6).

---

## Stop Conditions

<!--
  Universal stop conditions in AGENTS.md §20 and .ai/README.md §13 always apply.
-->

- **STOP instead of deciding** if the Product Owner's answer cannot be obtained.
  Record the exact unanswered decision; do not guess. (`AGENTS.md` §7, §20.)
- **STOP** if existing authoritative documents are found to contain
  **contradictory** rules on this contract. Report both sources (file + section)
  per `AGENTS.md` §4 and do not silently choose one.
- **STOP** if the representation requires a new `BattleState` concept without
  Product Owner approval. Report it as a separate ARCHITECTURE/ADR task
  (`AGENTS.md` §18).
- **STOP** if the attack boundary cannot be selected from gameplay intent.
- **STOP** if multiple Crit source composition requires a balance decision — that
  is a balance change, not a contract decision (`TASK_TYPES.md` §4).
- **STOP** if Iron Fang × Bạch Hổ interaction requires a gameplay decision not
  already specified.
- **STOP & report re-classification** if the answer requires widening
  `GAME_STATE.md` §2.3.1 item 3's dichotomy or relaxing item 6 — see "Type
  Re-Classification Condition".
- **STOP** if the repository workflow requires a separate task before
  documentation can be changed; report that instead of expanding scope.
- **STOP** if this task appears to require a new Battle Event, a new SignalR
  method, or a second RNG stream (`AGENTS.md` §11, TASK-113 D-4/D-5).
- **STOP** if any work would modify `src/`, `tests/`, or TASK-115.

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Product Owner Decisions

- D-1 (representation): **`PetState.NextAttackCritModifiers[]`** — a dedicated
  source-specific collection. `PetState.Crit` is not mutated;
  `PendingStatusEffects[]` is not used; no second queued/in-flight
  representation is introduced.
- D-1a (instance identity): each modifier carries a **stable source/instance
  identity** sufficient to remove exactly that modifier after consumption;
  consumption must not reset unrelated Crit sources.
- D-2 (lifetime/expiry): active **until the owner's next qualifying attack
  consumes it**; not TurnBased, not Shield-triggered, no `RemainingTurns`, no
  Shield depletion.
- D-3 (attack boundary): a qualifying attack is an **explicit owner attack
  action that enters the Damage Pipeline**; consumed once per that attack; a raw
  damage instance is not a separate attack.
- D-3a: a non-damaging action does **not** consume.
- D-3b: multiple damage instances from one attack belong to the **same attack**;
  consumed at the **first qualifying damage instance**; not consumed again by
  later instances of the same attack.
- D-4 (composition): `EffectiveCrit = Base + Passive + Relic + applicable
  NextAttackCritModifiers`; additive; only active and applicable sources
  participate.
- D-4.1: `PetState.Crit` remains the **permanent/base** value and is never
  overwritten; Effective Crit is computed for the current pipeline execution.
- D-4.2: additive. D-4.3: each source independent.
- D-4.4 (cap): composed value capped at **100** — recorded as a **NEW authored
  value** (no Crit cap exists in `docs/` today; the `100` derives from the roll
  bound, not a documented stat ceiling).
- D-4.5 (source removal): remove only the consumed source-specific modifiers;
  must not reset `PetState.Crit`, nor remove Passive/Relic/unrelated Crit.
- D-4.6 (unit): percentage points; existing bounded procedure retained
  (`V ∈ [0,100)`, succeeds iff `V < EffectiveCrit`).
- D-5 / D-5.1–D-5.5 (multiple sources): stack **additively**, do not replace
  each other, remain source-specific; a qualifying attack consumes **all**
  applicable modifiers; base Crit unchanged.
- Iron Fang × Bạch Hổ: both apply additively (Base 5 + 10 + 10 = **25**); both
  removed after the qualifying attack; base Crit remains 5.
- D-6: `DefaultCrit` is initialization only, **never** a runtime reset
  mechanism; `PetState.Crit = PetState.DefaultCrit` must never be used.
- D-7: no existing Crit balance value changed.
- D-8: all preserved constraints confirmed (server authority, existing pipeline,
  existing bounded Crit RNG, single `RngState`, Burn Crit participation,
  CAS/retry, no client-authoritative Crit, no `PendingStatusEffects[]`, no new
  Redis/PostgreSQL schema, no new SignalR method, no new Crit/NextAttack event,
  no new gameplay mechanic).

### Changed Files
- `tasks/backlog/TASK-116-resolve-nextattack-crit-modifier-state-and-consumption-contract.md`
  — recorded the Product Owner decisions verbatim; added the bindable
  "Resulting Contract" (C-1–C-10) and the "Required Authoritative Result —
  Coverage" mapping; added the "Required Documentation Changes" register and the
  "Classification Outcome"; retained the pre-decision analysis as evidence.

### Validation Results
- `quality/review.md` §1 (documentation-applicable items) — PENDING REVIEW
- Zero files under `docs/`, `src/`, or `tests/` changed — VERIFIED

### ADR & Workflow Impact
- [x] Confirmed `docs/03-decisions/README.md` §8 checked — the NextAttack
      representation gap was **not** listed there
- [x] ADR required: **YES** — **ADR-017**, per `AGENTS.md` §18 (D-1 introduces a
      new authoritative battle-state concept). Not authored by this task.
- [x] Separate documentation-synchronization task required: **YES** — **TASK-117**
      (`ARCHITECTURE`), which authors ADR-017 and the owning `GAME_STATE.md` /
      `COMBAT_RULES.md` / `CARD_RULES.md` edits registered in "Required
      Documentation Changes".
- [x] Type re-classification considered and resolved: D-1 does **not** widen
      `GAME_STATE.md` §2.3.1 item 3's dichotomy nor relax item 6, so the task is
      **not** re-typed `GAMEPLAY-CHANGE`; the ADR path applies instead.

### Boundary Verification
- [x] Confirmed zero files under `src/` modified
- [x] Confirmed zero files under `tests/` modified
- [x] Confirmed TASK-115 byte-identical and still blocked
- [x] Confirmed no new Battle Event, SignalR method, or RNG stream introduced
- [x] Confirmed no **existing** balance value authored or changed; the one new
      authored value (D-4.4 cap = 100) is flagged explicitly
