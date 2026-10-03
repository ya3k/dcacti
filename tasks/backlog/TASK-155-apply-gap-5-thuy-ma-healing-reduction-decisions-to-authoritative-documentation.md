# TASK-155 — Apply the TASK-154 GAP-5 Decision to the Authoritative Documentation

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS TASK IS THE CONTRACT-APPLICATION HALF OF THE DECISION WORKFLOW.

    TASK-154 decision record (D-1 … D-12 / Option B)
            ↓
    Apply the decision to its canonical owners      ← this task
            ↓
    TASK-153 becomes implementation-ready
            ↓
    STOP

  It performs the canonical-owner documentation write that
  documentation/documentation-change.md §1 describes. It consumes TASK-154's
  record; it does NOT re-open it, re-interpret it, re-decide it, or add a
  decision to it.

  THE DECISION IS ALREADY MADE. TASK-154 is DECIDED. Nothing in this task is a
  Product Owner question. If applying the decision surfaces something the
  decision did not cover, that is a STOP (this file's Stop Conditions), not a
  new decision to be authored here.

  BOUNDARY: documentation only. Zero files under src/ or tests/.
  Zero changes to TASK-154, TASK-153, or any other task.

  THE ONE AUTHORING ACT THIS TASK MUST PERFORM. TASK-154 D-2 deliberately left
  the exact StatusEffect `Id` string as a documentation naming detail, and
  recorded it as "Reported open detail (not a Stop Condition, not a decision
  gap)". TASK-154 also forbids introducing a new `TargetStat` value (D-10).
  Those two together mean the applicable-instance SELECTOR has to be authored
  here, at the canonical owner, out of the EXISTING identity vocabulary —
  `GAME_STATE.md` §2.3.1 item 1/item 6. This task does NOT invent an identity
  model and does NOT invent a TargetStat. If the existing vocabulary genuinely
  cannot supply the identity without guessing, this task STOPS (§5 of its
  brief; Stop Conditions below) rather than inventing one.
-->

---

## Metadata

```text
Task ID:           TASK-155
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change `docs/` content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md. Not GAMEPLAY-CHANGE:
                   the gameplay rule was decided by the Product Owner in
                   TASK-154, and this task authors no rule of its own. It
                   records a decided contract at its canonical owners, exactly
                   as TASK-124 and TASK-151 did for TASK-123 and TASK-150.)
Status:            BACKLOG (per tasks/README.md §6 item 6 — created at
                   BACKLOG; moves to READY when the Orchestrator sequences it)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION LOW–MEDIUM; MEDIUM
                   because it resolves a cross-referenced contract gap across
                   three authoritative documents and is the sole gate on
                   TASK-153's implementation. It is not HIGH: no magnitude,
                   duration, trigger, or state member changes, no new event or
                   wire member is introduced, and every statement it writes is
                   a transcription of an already-decided item.)
Priority:          HIGH (TASK-154 "Blocks" names this task first, and
                   AGENTS.md §17 places the authoritative-documentation update
                   before implementation. TASK-153 cannot leave BLOCKED and
                   ROADMAP.md §1 Phase 1's "Boss Response (Passive → Skill →
                   Attack → Victory/Defeat)" for the 3 MVP Bosses cannot
                   complete until this lands.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review")
Supporting Agents: gameplay (BOSS_RULES.md §6.2.2 owns the Thủy Ma effect;
                   COMBAT_RULES.md §4 item 7 / §5.4 / §5.5 own Heal Resolution
                   and BuffDebuff consumption),
                   backend (GAME_STATE.md §2.3.1 owns the StatusEffect instance
                   schema and identity/uniqueness; §2.4.1 owns the Boss
                   carrier)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   gameplay/gameplay-behavior-derivation,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-154 (DECIDED — the Product Owner decision record
                     D-1 … D-12 this task applies. IMMUTABLE; read-only; must
                     NOT be modified),
                   TASK-153 (BLOCKED — the downstream implementation this task
                     unblocks by making the contract determinate. IMMUTABLE;
                     read-only; must NOT be modified, re-statused, or moved),
                   TASK-124 (DONE — applied TASK-123's decisions and recorded
                     GAP-5 as deferred to a future task; the precedent for this
                     task's shape. IMMUTABLE; read-only),
                   TASK-123 (DONE — the decision set whose D-2/D-2b/D-2c
                     created the split TASK-154 reconciled. IMMUTABLE;
                     read-only)
Blocks:            (1) TASK-153 leaving BLOCKED and being implemented;
                   (2) the Thủy Ma half of the MVP Boss Passive set;
                   (3) ROADMAP.md §1 Phase 1 "Boss Response" completeness for
                   the 3 MVP Bosses.
Estimate:          Normal (three authoritative documents, one narrow
                   cross-referenced contract, no code and no tests; the
                   decisions are already made and the edit sites are
                   enumerated below)
```

---

## Objective

Apply the TASK-154 decision record (D-1 … D-12, Option B) to the canonical owners of the Thủy Ma healing-reduction contract — `BOSS_RULES.md` §6.2.2, `COMBAT_RULES.md` §4 item 7 and §5.4.5/§5.5.3, and `GAME_STATE.md` §2.4.1 — so that the Boss-carried instance in `BossState.StatusEffects[]` and the Pet-scoped Heal Resolution step form one deterministic, non-contradictory authoritative contract: the read the Pet step performs on the Boss-owned modifier is explicitly authorized, the applicable-instance selection is determinable from the existing identity vocabulary, and no reader is left to infer how a Pet-scoped step reaches a Boss-held effect.

This task changes no source code, authors no gameplay rule, and re-decides nothing.

---

## Authoritative References

- `AGENTS.md` **§4** — never silently resolve a conflict; **§7** — invent no rule; **§9** — anti-overengineering; **§12** — domain boundaries (a Boss-reactive effect's mechanics belong to the Boss/Combat owners, not bolted elsewhere); **§17** — documentation change precedes code; **§20** — stop conditions; **§23** — implement documented intent, do not design on the project's behalf
- `.ai/README.md` **§6** — source-of-truth rule; **§13** — stop conditions; **§18** — documentation-update policy (classify the situation before editing)
- `.ai/workflow/documentation/documentation-change.md` **§1** — the flow this task executes (identify the canonical owner → read related docs → check conflicts → update the smallest authoritative source → update dependent references only if stale → validate consistency); **§2** — no duplication, ever; **§3** — determining the canonical owner; **§4** — this workflow composes with `quality/review.md` and `core/completion.md`
- `tasks/README.md` **§9** — no business-rule duplication in task files; **§10** — stop conditions; **§12** — skill budget
- `docs/00-overview/MVP_SCOPE.md` §1 (Thủy Ma, the Boss Passive system, and healing are IN), §2/§4 (no OUT-of-scope system may be introduced)
- `docs/01-game-design/BOSS_RULES.md` **§6.2.2** — the Thủy Ma effect this task re-states with the authorized read boundary; **§6.2** (the trigger table and the Battle Start trigger), **§6.2.3** (Mộc Yêu regeneration, deliberately outside Heal Resolution), **§6.2.4** (server authority and the intentional `BossState` client-invisibility), **§3.3** (step-18a timing)
- `docs/01-game-design/COMBAT_RULES.md` **§4 item 7** — the **canonical owner** of Heal Resolution, its ordering, its clamp position, and its "Scope — Pet HP only" clause; **§4 item 1** (the overheal clamp, unchanged and still last); **§4 item 6** (Heal is not subject to the Damage Pipeline); **§5.2 item 2** (refresh-not-stack MVP default); **§5.3 / §5.3.1 / §5.3.3 / §5.3.4** (DR1–DR6 duration consumption, the single step-19a decrement, and the worked "duration = 3, applied at Battle Start" example); **§5.4.5** and **§5.5.3** (the `"ATK"`-only scope boundaries and the "would require its own recorded decision" clause); **§5.4.1 / §5.5.1** (the existing signed-`Magnitude` percentage convention)
- `docs/02-technical/GAME_STATE.md` **§2.3.1** — the StatusEffect instance schema; **item 1** (`Id` is an identity, not a definition; the effect's rules are not copied into the instance), **item 2** (`Magnitude` typed but not interpreted), **item 3** (the two exclusive duration models), **item 6** (at most one instance per effect identity per entity; `Id` is the uniqueness key), **item 7** (`TargetStat`-iff-`BuffDebuff` pairing; absence "never `null`, never a sentinel string"), **item 8** (a stored zero is never an active state), **item 12** (the section adds no gameplay rule); **§2.3.3** (the `PendingStatusEffects[]` / queued-collection prohibition); **§2.4** / **§2.4.1** (the `BossState` tree and the Boss carrier contract); **§5.1.1** (the Status Effect mutation lifecycle); **§0 item 5** (one representation per fact)
- `docs/01-game-design/GAME_RULES.md` **§17** — the fixed resolution order: step 18a (match-charged Boss Passive — **not** this effect's application point), step 18b/18c, and step 19a (the single duration-consumption point); **§16** (the canonical event list — contains no Boss-Passive-effect event); **§18** (server authority)
- `docs/01-game-design/PASSIVE_RULES.md` **§3** — the Battle Start one-time trigger form (unchanged by this task)
- `docs/02-technical/ARCHITECTURE.md` **§2.1** (Application sequences; Domain owns the rules), **§5** (anti-overengineering)
- `docs/02-technical/TDD.md` **§6** (determinism)
- **`tasks/backlog/TASK-154-resolve-gap-5-thuy-ma-healing-reduction-representation-and-heal-resolution-boundary.md`** — **THE DECISION SOURCE.** Its "Decision Record" section (D-1 … D-12) is the complete, Product-Owner-supplied contract this task applies, including the "Reported open detail" note on the exact `Id` string. **Read it; do not modify it.**
- `tasks/backlog/TASK-153-implement-mvp-boss-passive-effects-at-step-18a.md` — the blocked implementation this task must make determinate; its "Stop Condition Report" records the contradiction this task removes
- `tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md` — the precedent for applying a decision set at canonical owners, and the record deferring GAP-5
- `tasks/completed/TASK-123-resolve-boss-passive-effect-contract.md` — D-2 / D-2b / D-2c / D-2c-duration / D-2d, the original split; its GAP-5 entry
- `tasks/completed/TASK-151-apply-powerchanged-source-decisions-to-authoritative-documentation.md` — the most recent documentation-apply precedent (shape, version-block convention, Completion Evidence form)

---

## Current State

The three documents currently cannot be read together without a contradiction, and that contradiction is exactly what TASK-153 stopped on.

```text
BOSS_RULES.md §6.2.2     the instance is "held in BossState.StatusEffects[]"
                         AND the −50% "is applied" at COMBAT_RULES.md §4 item 7
                         (the Pet-scoped step)
                              ↓  no documented mechanism connects them
COMBAT_RULES.md §4 item 7  declares "Scope — Pet HP only" and states a Boss's
                         own HP restoration "does not route through this step"
```

Today the two halves are unreconciled: `BOSS_RULES.md` §6.2.2 asserts both carriers, and `COMBAT_RULES.md` §4 item 7's "Applicable Heal Modifiers" bullet already names Thủy Ma's −50% as one applicable Heal modifier, but nothing authorizes a **Pet-side** step to **read** a **Boss-held** instance. `GAME_STATE.md` §2.4.1 already records that Thủy Ma's healing reduction is a Turn-based instance held in `BossState.StatusEffects[]` and that its gameplay rule is "owned by `COMBAT_RULES.md` (§5.3 duration, §5.5 Boss ATK modifiers, §4 item 7 Heal resolution)" — but §4 item 7 does not itself admit a Boss-owned modifier.

Version state at the time of writing (verify before editing; do not assume):

```text
docs/01-game-design/BOSS_RULES.md     Version 2.6
docs/01-game-design/COMBAT_RULES.md   Version 2.3
docs/02-technical/GAME_STATE.md       Version 2.16
```

### The one thing the decision deliberately left open, and what this task must do about it

TASK-154 D-2 fixed the identity **model** ("a single effect-identity `Id` value, with at most one instance per identity" — `GAME_STATE.md` §2.3.1 item 6) but explicitly left the exact `Id` **string** as a documentation naming detail, and TASK-154 D-10 forbids introducing a new `TargetStat` value.

The existing `Id` vocabulary in the authoritative documents is the closed illustrative set at `GAME_STATE.md` §2.3.1 line ~1206:

```text
Id   (string, required — the Status Effect identity, e.g. "Burn", "Root", "Shield", "Stun")
```

No authoritative document names an `Id` for Thủy Ma's healing reduction. (Verified: a repository-wide search of `docs/` for a healing-reduction `Id` returns nothing.) Therefore this task must author the identity at its canonical owner **out of that existing vocabulary**, consistently with `GAME_STATE.md` §2.3.1 item 1 ("`Id` is an identity, not a definition") and item 6 (uniqueness key). It may not invent a new identity model, and it may not introduce a `TargetStat` to carry the selection.

If — and only if — the existing vocabulary cannot supply a determinate identity without guessing, this task STOPS and reports per its Stop Conditions. It does not invent one.

---

## Exact Decision Input

The authoritative contract to be applied, transcribed from TASK-154's "Decision Record" (D-1 … D-12). **This section restates the decision's shape only; TASK-154 remains the source of record.**

```text
State carrier:      BossState.StatusEffects[]            (D-1, Option B)
Identity:           one effect-identity Id; at most one instance per identity,
                    Id is the uniqueness key            (D-2, §2.3.1 item 6)
Target:             Pet HP healing ONLY                  (D-3)
Magnitude:          −50%                                 (unchanged)
Trigger:            Battle Start, one-time, before Turn 1 (unchanged)
Duration:           3 turns → RemainingTurns = 3          (unchanged)
Lifetime:           Battle Start → active Turns 1–3 →
                    decrement at step 19a → expires before Turn 4 (D-4)
Consumption:        persistent / Turn-based; NOT consumed by healing, by an
                    action, or once; the read is pure and non-mutating;
                    removal only via the existing turn-duration lifecycle (D-7)
Reapplication:      refresh the existing instance to the full 3 turns; no
                    additive stack; at most one instance; same source
                    identity                                (D-8)
Heal Resolution:    COMBAT_RULES.md §4 item 7 REMAINS "Scope — Pet HP only"
                                                             (D-11)
Cross-entity read:  the Pet-side Heal Resolution step is EXPLICITLY AUTHORIZED
                    to read the applicable BossState.StatusEffects[] instance
                                                             (D-5)
Evaluation point:   the Applicable Heal Modifiers stage of §4 item 7  (D-6)
Ordering:           Raw Heal → Applicable Heal Modifiers ← the −50% applies
                    HERE, as ONE applicable Heal modifier → Final Heal Amount
                    → existing item 1 overheal clamp (UNCHANGED, STILL LAST)
                    → HP update                                (D-6)
Arithmetic:         no new rule is authored; the existing signed-Magnitude
                    percentage convention (§5.4/§5.5) is the mechanism (D-6)
State invariants:   GAME_STATE.md §2.3.1 UNCHANGED — no new TargetStat, no
                    PendingStatusEffects[], no second in-flight representation,
                    no sentinel TargetStat, no undocumented StatusEffect shape
                                                             (D-10)
MVP sources:        only Thủy Ma is defined; no future multi-source behavior is
                    invented                                     (D-9)
ADR:                NOT REQUIRED                              (D-12)
```

**Do not add to this list, and do not drop an item from it.** Every statement this task writes traces to one of these items or to the decision's D-12 documentation-consequence list.

---

## Documentation Ownership

Per `.ai/workflow/documentation/documentation-change.md` §1 and §3, each concept has exactly one canonical owner. This task edits only the owner of each concept it touches, and does not duplicate a rule into the documents that merely reference it.

```text
Concept                                       Canonical owner (this task edits)
--------------------------------------------  ---------------------------------
Thủy Ma's effect — magnitude, trigger,        docs/01-game-design/BOSS_RULES.md
  duration, carrier, and its consumption        §6.2.2
  boundary
Heal Resolution — its ordering, its clamp      docs/01-game-design/COMBAT_RULES.md
  position, its scope, and what may be an        §4 item 7
  applicable Heal modifier
BuffDebuff consumption scope — which           docs/01-game-design/COMBAT_RULES.md
  TargetStat cases exist and the boundary        §5.4.5 / §5.5.3
  for any other stat
StatusEffect instance schema, identity,        docs/02-technical/GAME_STATE.md
  uniqueness, and the Boss carrier contract     §2.3.1 (read-only) / §2.4.1
```

Cross-owner constraint (`documentation-change.md` §2): the Boss effect's arithmetic, magnitude, duration, and reapplication rule stay in `BOSS_RULES.md` §6.2.2 and are **referenced** by `COMBAT_RULES.md`, never restated there; and `COMBAT_RULES.md`'s Heal Resolution mechanism is **referenced** by `BOSS_RULES.md` §6.2.2, never restated there. That split already exists and must be preserved.

---

## Required Changes

The smallest exact edits that make the contract determinate. Each is tied to its decision item.

### 1. `docs/01-game-design/BOSS_RULES.md` §6.2.2 — the consumption boundary and the authorized read

```text
[D-1/D-5]  Keep "held in BossState.StatusEffects[]" as the carrier — UNCHANGED.
           ADD the missing half: state explicitly that the Pet-side Heal
           Resolution step (COMBAT_RULES.md §4 item 7) is authorized to READ
           that Boss-held instance, i.e. the read is cross-entity:
           Pet-side step → Boss-owned instance.

[D-2]      State the exact applicable-instance identity, authored out of the
           existing GAME_STATE.md §2.3.1 identity vocabulary and its item 6
           uniqueness rule. This is the selector an implementer must be able to
           read off. It is an Id-based identity, NOT a TargetStat value.

[D-5]      State that the read is ONE-DIRECTIONAL and NON-MUTATING: it does not
           write BossState and does not consume, decrement, or remove the
           instance.

[D-6]      State the evaluation point: the Applicable Heal Modifiers stage of
           §4 item 7, as ONE applicable Heal modifier, before item 1's clamp —
           which stays last and unchanged. Reference §4 item 7; do not restate
           its ordering.

[D-3]      Target stays "Active Pet healing" — Pet HP only.

[D-4]      Duration and window: Battle Start, RemainingTurns = 3, active Turns
           1–3, decrement at step 19a, expires before Turn 4. Confirm unchanged;
           survive Turn transitions, Swaps, non-healing actions, multiple heals.

[D-7]      State that the modifier is not consumed by healing and is removed
           only through the existing turn-duration lifecycle.

[D-8]      Reapplication: refresh to full 3 turns, no stack, one instance, same
           source identity — confirm unchanged.

Confirm unchanged:  magnitude 50%, duration 3 turns, Battle Start trigger, the
                    Card Heal and HP-Gem reach, the MaxHP and Shield non-effects,
                    and the "no new event or protocol" boundary (no
                    HealingReduced / BossPassiveApplied / BossPassiveExpired
                    event; no PassiveCharged/PassiveTriggered emission).
```

### 2. `docs/01-game-design/COMBAT_RULES.md` §4 item 7 — the applicable Heal Modifier may be Boss-owned

```text
[D-5/D-11] The "Applicable Heal Modifiers" bullet already names Thủy Ma's −50%
           as one applicable Heal modifier. ADD what is missing: that an
           applicable Heal modifier may be held on the BOSS
           (BossState.StatusEffects[]) and is read by this Pet-scoped step
           through the authorized cross-entity read, one-directionally and
           non-mutating.

[D-11]     The "Scope — Pet HP only" clause REMAINS and is NOT widened. Make it
           explicit that the authorization is a READ, not a scope change: this
           step still governs only healing that restores Pet HP, and still does
           not govern a Boss-side HP change. Do not reword the clause into a
           target-aware/target-agnostic mechanism — Option C was NOT selected.

[D-6]      Ordering, the clamp's last position, the MaxHP and Shield
           non-effects, the no-elemental-interaction statement, and the
           "authors the mechanism only" boundary are UNCHANGED. Do not re-order,
           reword, or replace item 1's clamp.

Keep this item the owner of the mechanism. Do NOT restate the Thủy Ma
magnitude, duration, trigger, or reapplication rule here — those stay in
BOSS_RULES.md §6.2.2 and are referenced.
```

### 3. `docs/01-game-design/COMBAT_RULES.md` §5.4.5 / §5.5.3 — no new non-`"ATK"` `TargetStat` case

```text
[D-10] Make explicit, at the boundary where it matters, that Thủy Ma's Healing
       Reduction is NOT represented as a new TargetStat-based BuffDebuff case
       and therefore opens NO new non-"ATK" TargetStat case.

Preserve both sections' existing closing boundary verbatim in meaning: a
BuffDebuff naming any stat other than "ATK" "would require its own recorded
decision before it could be implemented". Do NOT weaken, widen, or delete that
boundary, and do NOT add a TargetStat value. The boundary is NOT exercised by
this decision.

Do not change either section's "Applies to" list, its DoT/Shield exclusions, or
its "Does NOT change" list.
```

### 4. `docs/02-technical/GAME_STATE.md` §2.4.1 — the authorized read reference

```text
[D-5] §2.4.1 already records that Thủy Ma's healing reduction is a Turn-based
      instance held in BossState.StatusEffects[] and that its gameplay rule is
      "owned by COMBAT_RULES.md (§5.3 duration, §5.5 Boss ATK modifiers, §4
      item 7 Heal resolution)". Bring that reference into line with the now-
      authored contract by recording that the Boss-held instance is read by the
      Pet-side Heal Resolution step through the authorized cross-entity read.

[D-10] ADD NO MEMBER, VALUE, TYPE, OR COLLECTION. The §2.4 tree is unchanged,
      §2.3.1's member set is unchanged, and no new TargetStat is introduced.
      Do not restate the gameplay rule — reference its owner.
```

### 5. Version blocks — all three documents

```text
Record this revision in each modified document's version block, following the
existing convention in those files (the newest revision is stated first, prior
revisions remain as "Prior N.N:"). State what changed and, explicitly, what did
NOT: no magnitude changed, no new TargetStat, no new event, no SignalR member,
no BattleState member, no Redis key, no database column, no invariant changed.
Cite TASK-154 as the decision source and TASK-155 as the applying task.
```

---

## Explicit Non-Changes

Per TASK-154 D-12's "EVALUATED — NO CHANGE REQUIRED" list. **Do not modify these.** The only permitted exception is an unavoidable cross-reference correction — a reference that has become factually stale because of the edits above — and any such correction must be reported in Completion Evidence with its justification.

```text
docs/02-technical/GAME_STATE.md §2.3.1   NO substantive change. Its invariants
                                         are CONFIRMED UNCHANGED by D-10. Do not
                                         add a TargetStat, an Id value in the
                                         schema block, a member, or a new
                                         convention.
docs/01-game-design/GAME_RULES.md        NO change. §17 step 18a is the
                                         match-charged application point and is
                                         NOT this effect's; step 19a already
                                         owns the single decrement; §16's event
                                         list is unchanged; §18 is unaffected.
docs/02-technical/GAME_EVENTS.md         NO change. No event is added.
docs/02-technical/SIGNALR_PROTOCOL.md    NO change. No wire member is added, and
                                         §6.2.4's intentional BossState
                                         client-invisibility limitation stands.
docs/02-technical/REDIS_STATE.md         NO change. No new key; the effect rides
                                         the existing single write-back.
docs/02-technical/DATABASE.md            NO change. No column, no schema.
docs/02-technical/ARCHITECTURE.md        NO change. No boundary moves; the
                                         cross-entity read is a read within the
                                         existing authoritative BattleState
                                         aggregate.
docs/02-technical/TDD.md                 NO change. No determinism or hot-path
                                         contract changes.
docs/01-game-design/PASSIVE_RULES.md     NO change. The Battle Start trigger form
                                         is unchanged.
```

**No ADR.** TASK-154 D-12 records "ADR required: NO" with its reasoning, and this task must not create one. Creating an ADR here would contradict the decision (`AGENTS.md` §18, `architecture/adr-change.md`).

**No new mechanic.** Mộc Yêu's regeneration (`BOSS_RULES.md` §6.2.3) and Hỏa Long's Rage (`§6.2.1`) are not touched: §6.2.3 deliberately stays outside Heal Resolution, and this task must not route it through the step.

---

## TASK-153 Unblock Criteria

The point of this task. After applying the documentation above, TASK-153's implementation must be able to determine each of the following **from the authoritative documents alone, with no further decision**. Each is a binary check.

```text
[ ] 1.  Where Thủy Ma Healing Reduction lives
        → BOSS_RULES.md §6.2.2 states BossState.StatusEffects[].
[ ] 2.  Which instance is applicable
        → BOSS_RULES.md §6.2.2 states the exact Id / identity selector, and
          GAME_STATE.md §2.3.1 item 6 supplies the uniqueness rule.
[ ] 3.  Which target is affected
        → Pet HP healing only; Boss HP healing unaffected.
[ ] 4.  When it becomes active
        → at Battle Start, before the first Turn (not step 18a, not
          match-charged).
[ ] 5.  How long it remains active
        → RemainingTurns = 3; Turns 1–3; expires before Turn 4.
[ ] 6.  How Heal Resolution discovers it
        → COMBAT_RULES.md §4 item 7 states the authorized cross-entity read of
          the applicable BossState.StatusEffects[] instance at the Applicable
          Heal Modifiers stage, one-directional and non-mutating.
[ ] 7.  Where −50% enters the calculation
        → as ONE applicable Heal modifier, applied to the Raw Heal.
[ ] 8.  Ordering relative to the existing clamp
        → before item 1's clamp; the clamp is unchanged and still last.
[ ] 9.  How reapplication behaves
        → refresh to full 3 turns; no stack; one instance; same source
          identity.
[ ] 10. How expiry works
        → the existing step-19a turn-duration lifecycle removes it; not
          consumed by healing; a stored zero is never active
          (GAME_STATE.md §2.3.1 item 8).
[ ] 11. That no new TargetStat is required
        → COMBAT_RULES.md §5.4.5/§5.5.3 say so explicitly, and the selector is
          an Id-based identity.
[ ] 12. That no new event/wire/storage contract is required
        → BOSS_RULES.md §6.2.2's "no new event or protocol" stands; no member,
          key, column, or event was added.
```

**If any item remains ambiguous after the documentation changes, this task STOPS.** The ambiguity is reported. TASK-153 is **not** modified to hide it — TASK-153 is immutable and read-only.

---

## Acceptance Criteria

All binary and testable.

- [ ] `BOSS_RULES.md` §6.2.2 still states `BossState.StatusEffects[]` as the carrier, unchanged
- [ ] `BOSS_RULES.md` §6.2.2 states that the Pet-side Heal Resolution step (`COMBAT_RULES.md` §4 item 7) is authorized to read that Boss-held instance
- [ ] `BOSS_RULES.md` §6.2.2 states the exact applicable-instance identity, authored from the existing `GAME_STATE.md` §2.3.1 identity vocabulary and consistent with its item 6 uniqueness rule
- [ ] `BOSS_RULES.md` §6.2.2 states that the read is one-directional and non-mutating
- [ ] `BOSS_RULES.md` §6.2.2 states the evaluation point (the Applicable Heal Modifiers stage) and that §4 item 1's clamp remains last
- [ ] `BOSS_RULES.md` §6.2.2 states that the modifier is not consumed by healing, by an action, or once, and is removed only by the existing turn-duration lifecycle
- [ ] `BOSS_RULES.md` §6.2.2's magnitude (`50%`), duration (`3 turns`), Battle Start trigger, reapplication rule, and "no new event or protocol" boundary are unchanged
- [ ] `BOSS_RULES.md` §6.2.2 introduces no new Battle Event, SignalR member, `BattleState` member, Redis key, or database column
- [ ] `COMBAT_RULES.md` §4 item 7 states that an applicable Heal modifier may be held on the Boss and is read by this Pet-scoped step through the authorized cross-entity read
- [ ] `COMBAT_RULES.md` §4 item 7 still reads "Scope — Pet HP only" and is NOT widened to a target-aware/target-agnostic mechanism
- [ ] `COMBAT_RULES.md` §4 item 7 makes explicit that the authorization is a read, not a scope change, and that a Boss-side HP change still does not route through the step
- [ ] `COMBAT_RULES.md` §4 item 7's reading order and §4 item 1's clamp position are unchanged, and the clamp is still last
- [ ] `COMBAT_RULES.md` §4 item 7's MaxHP and Shield non-effects, its no-elemental-interaction statement, and its "authors the mechanism only" boundary are unchanged
- [ ] `COMBAT_RULES.md` §5.4.5 and §5.5.3 each state that Thủy Ma's Healing Reduction opens no new non-`"ATK"` `TargetStat` case
- [ ] No new `TargetStat` value appears in any document
- [ ] `COMBAT_RULES.md` §5.4.5's and §5.5.3's "would require its own recorded decision" boundary is preserved in meaning and is not weakened or deleted
- [ ] `GAME_STATE.md` §2.4.1 records the authorized cross-entity read reference and adds no member, value, type, or collection
- [ ] `GAME_STATE.md` §2.3.1 is substantively unchanged; its items 1, 2, 3, 6, 7, 8, and 12 and §0 item 5 still hold as written
- [ ] `GAME_STATE.md` §2.3.1 still contains no `PendingStatusEffects[]` and no second in-flight representation reference, and §2.3.3's prohibition is intact
- [ ] All three modified documents' version blocks record this revision and state explicitly what did not change
- [ ] Zero files under `docs/` other than `BOSS_RULES.md`, `COMBAT_RULES.md`, and `GAME_STATE.md` are modified
- [ ] Zero files under `src/` are modified
- [ ] Zero files under `tests/` are modified
- [ ] `TASK-154` is byte-identical to its DECIDED state
- [ ] `TASK-153` is byte-identical and still `BLOCKED`
- [ ] No other task file is modified or created
- [ ] No ADR is created
- [ ] Every statement written traces to a TASK-154 D-item; no gameplay rule was authored, reinterpreted, or added
- [ ] No rule is duplicated across owners (`documentation-change.md` §2): §6.2.2 still references §4 item 7 rather than restating it, and §4 item 7 still references §6.2.2's values rather than restating them
- [ ] The TASK-153 Unblock Criteria checklist is satisfied for all twelve items
- [ ] Quality review checklist passes (`quality/review.md` §1)
- [ ] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[ ] src/backend/ — NONE
[ ] src/frontend/client/ — NONE
[ ] tests/ — NONE
[x] docs/01-game-design/BOSS_RULES.md    §6.2.2 + version block
[x] docs/01-game-design/COMBAT_RULES.md  §4 item 7, §5.4.5, §5.5.3 + version block
[x] docs/02-technical/GAME_STATE.md      §2.4.1 + version block
[ ] docs/ — every other document: NONE (see "Explicit Non-Changes")
[ ] tasks/backlog/TASK-154-*.md — NONE (immutable; read-only decision source)
[ ] tasks/backlog/TASK-153-*.md — NONE (immutable; stays BLOCKED)
[ ] docs/03-decisions/ADR/ — NONE (no ADR; TASK-154 D-12)
```

---

## Implementation Notes

- **The decision is complete; this is transcription, not design.** Every statement traces to a TASK-154 D-item. If a statement cannot be traced, do not write it.
- **The two halves must be joined, not one side changed.** The contradiction is not that `BOSS_RULES.md` §6.2.2 or `COMBAT_RULES.md` §4 item 7 is individually wrong — it is that nothing connects them. The fix is the authorized read: the step's SCOPE stays Pet-only (D-11), and the READ is what the decision adds (D-5). Do not resolve the contradiction by widening the scope; Option C was explicitly not selected.
- **The `Id` is the one thing to author, and it must come from existing vocabulary.** TASK-154 D-2 fixed the model and left the string; D-10 forbids a new `TargetStat`. So the selector is an `Id`-based identity consistent with `GAME_STATE.md` §2.3.1 item 1 ("`Id` is an identity, not a definition") and item 6 (uniqueness key). Do not invent a new identity model, and do not reach for `TargetStat` to carry the selection — that would contradict D-10 and `§5.4.5`/`§5.5.3`.
- **Do not put the `Id` into §2.3.1's schema block.** D-10 confirms §2.3.1 is unchanged, and §2.3.1 item 1 already establishes that an `Id` names an instance without the schema enumerating an effect registry. The identity belongs with the effect's rule, at `BOSS_RULES.md` §6.2.2.
- **§4 item 7 already names the modifier.** Its "Applicable Heal Modifiers" bullet already says Thủy Ma's −50% "is one applicable Heal modifier" and that the item "authors the mechanism only" and defines "no modifier's magnitude, source, duration, or activity window". That existing boundary is correct and must be preserved: add the read authorization, and leave the values to §6.2.2.
- **This decision adds no arithmetic.** D-6 confirms the existing signed-`Magnitude` percentage convention (`COMBAT_RULES.md` §5.4/§5.5) is the mechanism. Do not author a new formula, rounding rule, or truncation rule.
- **Version blocks are the convention, not optional.** All three documents carry a long inline version history; follow the existing style (newest first, prior revisions retained as "Prior N.N:").
- **TASK-124 and TASK-151 are the precedents.** Both applied a decided contract at canonical owners without re-deciding it. Match their shape and their Completion Evidence form.
- **Determinism.** The read is a pure function of already-committed state; introduce no ordering ambiguity and no RNG (`AGENTS.md` §11, `TDD.md` §6).

---

## Validation Requirements

### Required Verification

```text
[x] Unit tests         — N/A. No code is modified by this task.
[x] Integration tests  — N/A. No code is modified by this task.
[x] Gameplay scenarios — N/A. No gameplay behavior is implemented by this task.
```

This task's verification is a documentation-consistency review (`quality/review.md` §1, `quality/documentation-consistency.md`):

```text
[ ] Every changed statement traces to a named TASK-154 D-item
[ ] No statement contradicts another authoritative document — specifically, the
    BOSS_RULES.md §6.2.2 ↔ COMBAT_RULES.md §4 item 7 contradiction this task
    exists to remove is gone and no new one is introduced
[ ] §4 item 7's "Scope — Pet HP only" clause is still present and still Pet-only
[ ] No new TargetStat value exists in any document (targeted search)
[ ] No PendingStatusEffects[] or second in-flight representation is referenced
[ ] No new Battle Event, SignalR member, Redis key, or database column is named
[ ] Mộc Yêu's §6.2.3 regeneration is unchanged and still outside Heal Resolution
[ ] Hỏa Long's §6.2.1 Rage is unchanged
[ ] No rule is duplicated across owners; each owner still owns its concept
[ ] Every section and document cross-reference introduced or touched resolves
[ ] The TASK-153 Unblock Criteria checklist passes all twelve items
[ ] The three version blocks are internally consistent and state what did not
    change
[ ] Scope: docs/ files other than the three listed = 0; src/ = 0; tests/ = 0;
    TASK-154, TASK-153, and all other tasks = 0
```

### Key Edge Cases

- **The read is authorized but its target is not stated.** Every one of D-3/D-5/D-6/D-11 must be legible together: Pet HP only, cross-entity read, Applicable Heal Modifiers stage, scope unchanged. A statement that authorizes the read without naming the target is incomplete.
- **The scope clause gets "clarified" into a widening.** Rewording §4 item 7 toward target-aware/target-agnostic language is Option C, which was not selected — STOP and revert to the decision.
- **A `TargetStat` value is introduced to carry the selection.** Directly contradicts D-10 and §5.4.5/§5.5.3 — STOP.
- **The `Id` is invented where the existing vocabulary does not supply one.** Per this task's brief and Stop Conditions — STOP and report the ambiguity rather than guessing.
- **The `Id` is placed in `GAME_STATE.md` §2.3.1's schema block.** Contradicts D-10's "§2.3.1 unchanged" — STOP and relocate it to §6.2.2.
- **The §5.4.5/§5.5.3 "would require its own recorded decision" boundary is weakened** to accommodate this effect. D-10 says the boundary is preserved and is not exercised — STOP.
- **§6.2.2 restates §4 item 7's ordering, or §4 item 7 restates §6.2.2's magnitude.** Duplication across owners (`documentation-change.md` §2) — STOP and reference instead.
- **An event, wire member, Redis key, or column appears necessary.** Contradicts §6.2.2's "no new event or protocol" and D-10 — STOP and report.
- **`GAME_STATE.md` §2.3.1 needs a substantive change to express the contract.** Contradicts D-10's invariant confirmation — STOP and report the conflict rather than editing §2.3.1.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- **If the exact `StatusEffect` identity cannot be determined from the existing authoritative vocabulary** (`GAME_STATE.md` §2.3.1 item 1/item 6 and the documented `Id` set) without guessing: **STOP** and report. Do not invent an `Id` and do not fall back to a `TargetStat`.
- **If the TASK-154 decision conflicts with another authoritative rule:** **STOP** per `AGENTS.md` §4 — report the conflict with both sources (file + section); do not pick a side.
- **If applying the decision requires a new gameplay decision:** **STOP**. A new decision is a new task, not a detail of this one.
- **If applying the decision requires a new `TargetStat` value:** **STOP**. TASK-154 D-10 forbids it and `COMBAT_RULES.md` §5.4.5/§5.5.3 close the case.
- **If applying the decision requires changing an existing invariant** (`GAME_STATE.md` §2.3.1, §2.3.3, or §0 item 5): **STOP**. D-10 confirms every invariant unchanged; a required change means the decision and the state contract disagree.
- **If `COMBAT_RULES.md` §4 item 7's scope cannot remain "Pet HP only":** **STOP**. D-11 decided it remains; widening it is Option C, which was not selected.
- **If a new event, SignalR member, Redis key, or database column becomes necessary:** **STOP**. §6.2.2's "no new event or protocol" and D-10 both forbid it.
- **If the documentation would require an ADR:** **STOP**. D-12 records "ADR required: NO"; an ADR needs its own task (`AGENTS.md` §18).
- **If TASK-153 is still ambiguous after the documentation changes** — i.e. any of the twelve Unblock Criteria cannot be answered from the documents alone: **STOP** and report which item remains ambiguous. **Do not modify TASK-153** to hide it.
- **If TASK-154's decision is found to be incomplete or internally inconsistent:** **STOP**. TASK-154 is DECIDED and immutable; reopening it is not this task's authority.
- **If the edits cannot be confined to the four sites in "Required Changes"** plus version blocks: **STOP** and report — that indicates the contract's ownership is more entangled than TASK-154 D-12 recorded.
- **If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries:** **STOP & decompose** (`tasks/README.md` §12).

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Decision source
- **TASK-154** — `tasks/backlog/TASK-154-resolve-gap-5-thuy-ma-healing-reduction-representation-and-heal-resolution-boundary.md`,
  "Decision Record" (D-1 … D-12), **Status: DECIDED**, Option B. TASK-154 was
  **not modified** by this task.

### Canonical documentation updated

```text
docs/01-game-design/BOSS_RULES.md    Version 2.6 → 2.7
    §6.2.2  — added the applicable-instance selector; added the authorized
              cross-entity read; added the persistent/Turn-based non-consumption
              statement; stated the −50%-applies-to-Raw-Heal ordering and that
              §4 item 1's clamp stays last
docs/01-game-design/COMBAT_RULES.md  Version 2.3 → 2.4
    §4 item 7 — added the "an applicable Heal modifier may be held on the Boss"
              bullet (authorized cross-entity read, one-directional, non-mutating,
              selected by Status Effect `Id`); added the "read, not a scope
              change" bullet; clarified the "Scope — Pet HP only" clause without
              widening it
    §5.4.5 / §5.5.3 — recorded that Thủy Ma's Healing Reduction is not a
              TargetStat-consumed BuffDebuff and opens no new non-"ATK"
              TargetStat case
docs/02-technical/GAME_STATE.md      Version 2.16 → 2.17
    §2.4.1  — added the authorized-cross-entity-read reference (no member,
              value, type, or collection added)
    §2.3.1  — NOT modified (substantively unchanged; confirmed below)
```

### Changed Files
- `docs/01-game-design/BOSS_RULES.md` — §6.2.2 (three bullets added/extended) and
  the `**Version:**` block.
- `docs/01-game-design/COMBAT_RULES.md` — §4 item 7 (three bullets) and §4 item 7's
  "Scope — Pet HP only" clause, §5.4.5, §5.5.3, and the `**Version:**` block.
- `docs/02-technical/GAME_STATE.md` — §2.4.1 and the `**Version:**` block.
- `tasks/backlog/TASK-155-…md` — this Completion Evidence section.

**No other file was created, modified, or deleted by this task.** Zero files under
`src/`, zero under `tests/`, zero `docs/` files other than the three above.
`TASK-154` and `TASK-153` are byte-identical to their pre-task state.

### Decision Traceability

```text
D-1  State carrier BossState.StatusEffects[]
     → BOSS_RULES.md §6.2.2 "Representation" (carrier wording UNCHANGED)
D-2  Identity model: one effect-identity Id, at most one instance per identity
     → BOSS_RULES.md §6.2.2 new "Applicable-instance selector" bullet, citing
       GAME_STATE.md §2.3.1 item 1 and item 6; COMBAT_RULES.md §4 item 7's new
       cross-entity-read bullet ("determined by the Status Effect identity Id the
       effect's own rule authors, under GAME_STATE.md §2.3.1 item 1 and item 6")
       and GAME_STATE.md §2.4.1's new paragraph ("authored the selector: the
       instance's Status Effect Id, under §2.3.1 item 1 and item 6")
D-3  Target: Pet HP healing only
     → BOSS_RULES.md §6.2.2 "Where the −50% is applied" (Pet-healing reach
       UNCHANGED); COMBAT_RULES.md §4 item 7's "Scope — Pet HP only" clause
       (UNCHANGED in substance, clarified as not widened)
D-4  Lifetime: Battle Start → Turns 1–3 → step-19a decrement → expires before
     Turn 4; survives Turn transitions, Swaps, non-healing actions, multiple
     heals
     → BOSS_RULES.md §6.2.2 "Duration" (window UNCHANGED; the survival list added)
D-5  Cross-entity read explicitly authorized; one-directional; non-mutating
     → BOSS_RULES.md §6.2.2 new "The authorized cross-entity read" bullet;
       COMBAT_RULES.md §4 item 7 new "An applicable Heal modifier may be held on
       the Boss" + "This authorization is a read, not a scope change" bullets;
       GAME_STATE.md §2.4.1 new paragraph
D-6  Evaluation point = Applicable Heal Modifiers; −50% applies to Raw Heal;
     clamp unchanged and still last; no new arithmetic
     → BOSS_RULES.md §6.2.2 "Where the −50% is applied" (Raw Heal / Final Heal
       Amount / clamp-still-last added); COMBAT_RULES.md §4 item 7's Applicable
       Heal Modifiers stage (ordering UNCHANGED)
D-7  Persistent/Turn-based; not consumed by healing, action, or once; removal
     only via the existing turn-duration lifecycle
     → BOSS_RULES.md §6.2.2 "Duration" (added); COMBAT_RULES.md §4 item 7
       ("non-mutating … does not consume, decrement, or remove the instance")
D-8  Reapplication: refresh to full 3 turns, no stack, one instance, same
     identity
     → BOSS_RULES.md §6.2.2 "Reapplication" (UNCHANGED; the selector bullet
       states the item-6 refresh consequence)
D-9  Only Thủy Ma is defined; no future multi-source behavior invented
     → no text added beyond the single documented effect
D-10 §2.3.1 invariants unchanged; no new TargetStat; no PendingStatusEffects[]
     → GAME_STATE.md §2.3.1 NOT modified; COMBAT_RULES.md §5.4.5 / §5.5.3 record
       the no-new-TargetStat-case position; the three version blocks state it
D-11 §4 item 7 remains "Scope — Pet HP only"; Option C not selected
     → COMBAT_RULES.md §4 item 7 clause UNCHANGED, with the read/scope-change
       distinction added
D-12 ADR NOT required
     → no ADR created; no architecture/database/realtime/module boundary touched
```

### Identity Resolution
- **Authored identifier:** `Id = "boss-thuy-ma-heal"`, at
  `BOSS_RULES.md` §6.2.2's new "Applicable-instance selector" bullet.
- **Existing vocabulary it was drawn from:** `BOSS_RULES.md` §6.4's Identity
  Contract row for Thủy Ma, which already records
  `PassiveId = "boss-thuy-ma-heal"` — a value this repository already treats as
  Thủy Ma's canonical technical effect identity (it is also the
  `BossPassiveDefinition` identity keyed on by `StatusEffectLifecycle.Apply`'s
  one-instance-per-identity refresh in `src/backend/GameServer.Domain/Battle/`).
- **Why it satisfies §2.3.1 item 1 and item 6:** it is a string identity naming
  which effect the instance is (item 1 — the effect's rules stay in
  `BOSS_RULES.md` §6.2.2 and are not copied into the instance), and it is unique
  per Thủy Ma's `BossState`, so the array holds at most one element for it and a
  re-application refreshes in place (item 6).
- **No new identity model and no `TargetStat`:** the value is the already-recorded
  `PassiveId`, not a new token; the selector is the `Id`, explicitly **not** a
  `TargetStat` value, so D-10 and `COMBAT_RULES.md` §5.4.5 / §5.5.3 are honored.
  `GAME_STATE.md` §2.3.1's schema block was **not** touched and no `Id` value was
  added to it.
- **No new gameplay decision was made.** The decision left the `Id` string as a
  documentation naming detail (TASK-154 "Reported open detail"); it was resolved
  by reusing a recorded value rather than by choosing a semantics.

### TASK-153 Unblock Criteria

```text
[x]  1. Where Thủy Ma Healing Reduction lives
        → BOSS_RULES.md §6.2.2 "Representation": BossState.StatusEffects[].
[x]  2. Which instance is applicable
        → BOSS_RULES.md §6.2.2 "Applicable-instance selector":
          Id = "boss-thuy-ma-heal"; GAME_STATE.md §2.3.1 item 6 supplies the
          uniqueness/refresh rule.
[x]  3. Which target is affected
        → Pet HP healing only; Boss HP healing unaffected
          (COMBAT_RULES.md §4 item 7 "Scope — Pet HP only", unchanged).
[x]  4. When it becomes active
        → BOSS_RULES.md §6.2.2 "Trigger": Battle Start, one-time, before Turn 1;
          not step 18a, not match-charged.
[x]  5. How long it remains active
        → BOSS_RULES.md §6.2.2 "Duration": RemainingTurns = 3; Turns 1–3;
          expires before Turn 4.
[x]  6. How Heal Resolution discovers it
        → COMBAT_RULES.md §4 item 7's new bullets: the authorized cross-entity
          read of the applicable BossState.StatusEffects[] instance at the
          Applicable Heal Modifiers stage, one-directional and non-mutating,
          selected by the instance's Status Effect Id.
[x]  7. Where −50% enters the calculation
        → as ONE applicable Heal modifier applied to the Raw Heal
          (BOSS_RULES.md §6.2.2 "Where the −50% is applied";
          COMBAT_RULES.md §4 item 7's order).
[x]  8. Ordering relative to the existing clamp
        → before item 1's clamp; the clamp is unchanged and still last
          (COMBAT_RULES.md §4 item 7 / item 1; BOSS_RULES.md §6.2.2).
[x]  9. How reapplication behaves
        → refresh to full 3 turns; no stack; one instance; same source identity
          (BOSS_RULES.md §6.2.2 "Reapplication", unchanged).
[x] 10. How expiry works
        → the existing step-19a Turn-duration lifecycle removes it; not consumed
          by healing; a stored zero is never active
          (BOSS_RULES.md §6.2.2 "Duration"; COMBAT_RULES.md §5.3;
          GAME_STATE.md §5.1.1 item 5, §2.3.1 item 8).
[x] 11. That no new TargetStat is required
        → COMBAT_RULES.md §5.4.5 / §5.5.3 state it explicitly and the selector
          is an Id-based identity.
[x] 12. That no new event/wire/storage contract is required
        → BOSS_RULES.md §6.2.2 "No new event or protocol" stands; no member,
          key, column, or event was added.
```

### Validation Results
```text
[x] Every changed statement traces to a named TASK-154 D-item (table above).
[x] The BOSS_RULES.md §6.2.2 ↔ COMBAT_RULES.md §4 item 7 contradiction is
    removed: §6.2.2 states the Pet-side step is authorized to read the Boss-held
    instance, and §4 item 7 states an applicable Heal modifier may be Boss-owned
    and names the selector. No new contradiction was introduced.
[x] §4 item 7's "Scope — Pet HP only" clause is present and still Pet-only; the
    added sentence states the authorization is a read, not a scope change.
[x] Targeted search for a new TargetStat value: docs/ contains no TargetStat
    spelling other than the pre-existing "ATK" (and ADR-017's historical "Crit"
    note). No "HEAL" or equivalent value appears anywhere in docs/.
[x] Targeted search for PendingStatusEffects[]: the only docs/ occurrences are
    the existing PROHIBITION statements (§2.3.3, ADR-017, and the version blocks).
    No new reference was added; BOSS_RULES.md's version block cites it only as a
    non-change.
[x] Targeted search for new event/wire/storage names: no HealingReduced,
    BossPassiveApplied, or BossPassiveExpired event exists; no SignalR member,
    Redis key, or database column was named or added.
[x] Mộc Yêu's §6.2.3 regeneration is byte-identical in content and still states
    it is not routed through §4 item 7 ("whose scope is Pet-HP healing only") —
    consistent with the newly explicit read boundary.
[x] Hỏa Long's §6.2.1 Rage is unchanged.
[x] No rule is duplicated across owners: §4 item 7 does not restate Thủy Ma's
    magnitude/duration/trigger/reapplication (it references §6.2.2 and states the
    value is owned there), and §6.2.2 does not restate §4 item 7's ordering (it
    references §4 items 7 and 1).
[x] Every introduced cross-reference resolves: COMBAT_RULES.md §4 item 7 items
    1/7, §5.2 item 2, §5.3, §5.4.5, §5.5.3; GAME_STATE.md §2.3.1 items 1/6/8,
    §2.4.1, §5.1.1 item 5; BOSS_RULES.md §6.2.2, §6.4.
[x] The three version blocks are internally consistent and each states what did
    NOT change (no magnitude, no new TargetStat, no new event, no SignalR member,
    no BattleState member, no Redis key, no database column, no invariant change).
[x] TASK-153 Unblock Criteria: all twelve satisfied (above).
```

### Scope Verification
- [x] Confirmed zero files under `src/` modified by this task
- [x] Confirmed zero files under `tests/` modified by this task
- [x] Confirmed only `BOSS_RULES.md`, `COMBAT_RULES.md`, and `GAME_STATE.md`
      modified under `docs/` by this task
- [x] Confirmed `GAME_STATE.md` §2.3.1 substantively unchanged (not edited)
- [x] Confirmed TASK-154 byte-identical (immutable decision source)
- [x] Confirmed TASK-153 byte-identical and still BLOCKED
- [x] Confirmed no new TargetStat, no `PendingStatusEffects[]`, no second
      in-flight representation
- [x] Confirmed no new Battle Event, SignalR member, Redis key, or DB column
- [x] Confirmed no ADR created
- [x] Confirmed no new task created
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero client-authoritative gameplay logic (no code produced)

### New gameplay decisions
**NONE.** Every statement written is a transcription of a TASK-154 D-item or of
an existing rule referenced by it. The one authored detail — the selector's
spelling — was resolved by reusing Thủy Ma's already-recorded `PassiveId` from
`BOSS_RULES.md` §6.4, which required no new gameplay decision.

### TASK-153
```text
TASK-153:
UNBLOCKED BY DOCUMENTATION
```
Every one of the twelve Unblock Criteria is now answerable from the authoritative
documents alone, with no further decision. `TASK-153` was **not modified** and
remains `BLOCKED` in its own metadata; leaving BLOCKED is its own task's act.

