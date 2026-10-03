# TASK-123 — Resolve the Boss Passive Effect Contract (Rage, Healing Reduction, Regeneration: Representation, Application Site, and Duration)

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ by path and section — it does NOT copy game rules,
  formulas, schemas, or contracts.

  THIS TASK DECIDES NOTHING BY ITSELF AND IMPLEMENTS NO BEHAVIOR. It presents
  the documented evidence of a genuine contract gap and requires the
  appropriate human/Product-Owner decision, then records it in the canonical
  owner document(s). Choosing a representation for Boss Rage, inventing a
  healing-reduction application site, or defining where regeneration lands is
  the single prohibited action of this task (AGENTS.md §7, §20).

  PROVENANCE: identified by the post-TASK-120 next-task discovery pass.
  GAME_RULES.md §17 step 18a requires the Boss Response to "apply the Passive
  effect". TASK-022 implemented Boss Response charging/events and stated in
  code that "Passive EFFECT application is out of this task's scope for every
  Boss". TASK-118 implemented step 18b (Boss Skill secondary effects) and
  explicitly fenced step 18a out as "not fully determined by current
  documentation", instructing that it "require[s] a separate decision task
  first". That decision task has never been created. This task is it.

  THIS IS A STOP-DRIVEN TASK, NOT A FEATURE. Per AGENTS.md §20, "Missing rule"
  and "Ambiguous requirement" are stop conditions. Step 18a is documented only
  as an instruction to apply an effect; the effect's runtime representation,
  its application site, and (for regeneration) its application point are
  absent from docs/. No implementation task may proceed against it.

  BOUNDARY: documentation only. Zero files under src/ or tests/. This task
  creates no ADR unless the recorded answer requires one (reported, not
  authored — AGENTS.md §18), authors no balance value, adds no Battle Event,
  adds no SignalR method, and does not implement step 18a.
-->

---

## Metadata

```text
Task ID:           TASK-123
Type:              DOCUMENTATION (TASK_TYPES.md §2 — "Change docs/ content —
                   documentation is the primary output, not code"; workflow
                   documentation/documentation-change.md). The deliverable is a
                   recorded contract decision plus its entry in the canonical
                   owner document(s). See "Type classification note". If the
                   decision requires a code, schema, or ADR change, that change
                   is a SEPARATE follow-up task — not this task's act.
Status:            DONE (All 14 Product Owner decision groups recorded verbatim;
                   applied to authoritative documentation by TASK-124; pre-existing
                   GAP-1 resolved downstream by TASK-125/126/127. Formal review
                   pass completed against quality/review.md §1 and core/completion.md
                   §1: PASS. File moved from tasks/blocked/ to tasks/completed/
                   per TASK_LIFECYCLE.md §3.)
                     D-1  (Contradiction A): Rage modifies the Step-1 `Attack`
                          input via a derived EffectiveBossATK; §3.4 and
                          §5.4.5 unchanged; BossState.ATK remains the
                          immutable base.
                     D-2a (Contradiction B): "for 3 turns" is authoritative;
                          Thủy Ma's −50% healing is a triggered temporary
                          effect following the TurnBased 3-turn lifecycle;
                          "Passive (always active)" is retired as stale
                          wording.
                     D-2b: Thủy Ma's effect is triggered at Battle Start and
                          is represented with the existing TurnBased
                          Buff/Debuff StatusEffect model; no
                          PassiveTracker.Charge, no match-progress
                          PassiveCharged/PassiveTriggered, no new trigger
                          mechanism.
                     D-2b-site: the −50% modifier applies at the shared Heal
                          resolution point BEFORE the existing overheal clamp;
                          it reaches healing received by the Pet from any
                          existing source using that shared resolution
                          (explicitly Card Heal and HP-Gem healing); it does
                          not modify MaxHP and does not affect Shield.
                          ⚠ This decision PRESUPPOSES a shared Heal
                          resolution point that docs/ does not yet define —
                          resolved by D-2c below.
                     D-2c: a canonical shared Heal Resolution step is AUTHORED
                          in COMBAT_RULES.md §4; all Pet-HP healing sources
                          (Card Heal, HP-Gem healing) pass through it before
                          the existing overheal clamp; canonical order is
                          Raw Heal → applicable Heal modifiers → final Heal
                          amount → existing MaxHP/overheal clamp → HP update;
                          Thủy Ma's −50% is one applicable Heal modifier;
                          combat-rule mechanism only (no BattleState member,
                          event, SignalR payload, Redis key, or database
                          field); no speculative generic abstraction.
                          ⚠ RE-SCOPED: the supplied decision addresses the
                          Heal Resolution MECHANISM, not D-2c's originally
                          tracked duration-boundary subject.
                     D-2c-duration: the −50% effect is applied at Battle Start
                          with RemainingTurns = 3; active throughout Turns 1,
                          2, and 3; the Battle Start application is not a turn
                          and consumes no duration unit; the existing
                          TurnBased lifecycle decrements at the existing End
                          Turn / step 19a boundary; after step 19a of Turn 3,
                          RemainingTurns reaches 0 and the effect expires
                          before Turn 4; no new duration mechanism or
                          lifecycle phase.
                     D-2d: reapplication REFRESHES the existing instance to the
                          full 3-turn duration; it does NOT stack additively
                          (−100% is explicitly not the behavior); at most one
                          active Thủy Ma healing-reduction instance exists at a
                          time; the refreshed instance retains the same Thủy Ma
                          source identity. (Reported: unreachable in MVP play
                          because the Battle Start trigger is one-time — a
                          forward-compatibility/correctness rule.)
                     D-2e: NO new Battle Event; the effect emits no
                          PassiveCharged/PassiveTriggered; it is observable
                          through the existing authoritative BattleStateUpdated
                          synchronization; application, refresh, decrement, and
                          expiry are state changes only; no HealingReduced,
                          BossPassiveApplied, or BossPassiveExpired event.
                          ⚠ Reported: BattleStateUpdated currently carries NO
                          bossState member (SIGNALR_PROTOCOL.md §4), so under
                          the present protocol the effect is server-side /
                          persisted but not wire-visible — recorded as a
                          reported limitation, not resolved here.
                   THE THỦY MA DECISION FAMILY (D-2a…D-2e) IS NOW CLOSED.
                     D-3a–D-3f: Mộc Yêu regeneration — applied at Boss Response
                          step 18a; heals exactly 5% of MaxHP; truncated toward
                          zero to an integer; Final HP = min(CurrentHP +
                          RegenAmount, MaxHP) with no overheal retained; a
                          DIRECT authoritative BossState.HP update with NO new
                          Battle Event, observable through existing state
                          synchronization; each valid activation applies one
                          5% regeneration and it does not stack as a persistent
                          modifier.
                     D-1-representation: Rage is a TurnBased BuffDebuff
                          StatusEffect in BossState.StatusEffects[], with
                          TargetStat = "ATK", Magnitude = +20%, RemainingTurns
                          = 3; BossState.ATK remains the immutable/base value;
                          Rage is NOT stored as a separate BossState field.
                     D-1a: Rage modifies only Boss damage whose Step 1 Attack
                          input is derived from BossState.ATK — so the Boss
                          basic attack (EffectiveBossATK), not an independently
                          authored Boss Skill Base Damage; Flame Burst's 150
                          remains 150 unless its own contract declares ATK
                          scaling.
                     D-1b: uses the existing TurnBased duration model;
                          RemainingTurns = 3 on application at step 18a; the
                          application does not retroactively modify damage
                          already resolved that Turn; active for the next three
                          counted Turns; decrement at step 19a; expires after
                          step 19a of the third active Turn; no new duration
                          mechanism.
                     D-1c: a re-trigger does NOT create a second instance and
                          does NOT add another +20%; it REFRESHES the existing
                          instance to RemainingTurns = 3; magnitude remains
                          +20%; same source identity; at most one +20% Rage
                          instance active at a time.
                   ALL THREE BOSS EFFECT FAMILIES (Hỏa Long, Thủy Ma, Mộc Yêu)
                   NOW HAVE THEIR SEMANTICS DECIDED, AND D-4 IS RESOLVED.
                   D-4a–D-4g: no new Battle Event; no new SignalR method/event/
                   member (bossState is NOT added); no new Redis key
                   (battle:{battleId}:state remains sole persistence); no
                   PostgreSQL schema change; all three effects remain
                   server-authoritative at their decided homes; the current
                   lack of client visibility is a recorded, INTENTIONAL
                   contract limitation (not a defect); and D-3's direct Boss HP
                   update does NOT widen D-2c's Pet-scoped Heal Resolution.
                   ALL DECISION IDs ARE NOW RESOLVED.
                   TASK-123 remains BLOCKED pending the repository workflow's
                   separate contract-resolution task that applies these
                   decisions to the authoritative documentation. It is NOT
                   DONE: the owning documentation edits have not been made,
                   and this task's Acceptance Criteria require them to be
                   named and carried (they are named here; the edits are a
                   separate task's act).
                   Recorded remaining PRE-EXISTING documentation gaps (not
                   this task's decision IDs, reported only): see "Remaining
                   Pre-Existing Documentation Gaps" below.
                   File location: tasks/blocked/ per TASK_LIFECYCLE.md §3
                   (BLOCKED → File location: blocked/, transition
                   IN PROGRESS → BLOCKED, "Who sets it: Agent (immediately
                   when a stop condition fires)"). The file was created
                   directly in backlog/ as a BACKLOG task; per
                   TASK_LIFECYCLE.md §2 a stop condition fired on pickup, so
                   it moves to blocked/. Per TASK_LIFECYCLE.md §3, BLOCKED →
                   IN PROGRESS requires the blocking condition to be resolved
                   by human decision; it is only PARTIALLY resolved, so the
                   file stays in blocked/.
                   Original planned status: BACKLOG (the decision set below is
                   unanswered; this is the state the task exists to collect.
                   Follows the TASK-116 / TASK-119 / TASK-113 precedent.)
Risk:              MEDIUM (TASK_TYPES.md §4 — DOCUMENTATION baseline LOW–MEDIUM;
                   MEDIUM because the decision governs whether a Boss effect
                   needs a new authoritative battle-state concept, and because
                   it is cross-referenced by GAME_RULES.md §17 step 18a,
                   BOSS_RULES.md §3/§6.2, COMBAT_RULES.md §4/§5, and
                   GAME_STATE.md §2.4.2.)
Priority:          HIGH (the sole remaining unimplemented step of the §17
                   resolution order for MVP Bosses. ROADMAP.md Phase 1
                   requires "3 MVP Bosses (Hỏa Long, Thủy Ma, Mộc Yêu —
                   Passive + Skill each)" and "Boss Response (Passive → Skill
                   → Attack → Victory/Defeat)". The Skill half landed in
                   TASK-118; the Passive half is this task's blocker.)
Primary Agent:     review (TASK_TYPES.md §2 / AGENT_SELECTION.md §1 —
                   "Documentation change: Primary Agent Review". No domain
                   agent may author the answer; the Product Owner supplies it.)
Supporting Agents: gameplay (BOSS_RULES.md §3/§6.2 and COMBAT_RULES.md §4/§5
                   are the owning domain documents for the three effects —
                   consulted to CONFIRM the semantics the contract must be
                   able to express, not to author the rule),
                   backend (GAME_STATE.md §2.4/§2.4.2 and DATABASE.md §1 own
                   the BossState and BossPassiveDefinition contracts —
                   consulted to state accurately what the current
                   representation does and does not carry),
                   realtime (only if the recorded answer implies an event or
                   wire consequence, which is REPORTED for a separate task,
                   never applied here)
Workflow:          documentation/documentation-change.md
Skills:            discovery/documentation-discovery,
                   discovery/impact-analysis,
                   quality/documentation-consistency,
                   quality/scope-validation
                   (4 skills — Simple budget, tasks/README.md §12)
Dependencies:      TASK-118 (DONE — implemented step 18b Boss Skill secondary
                     effects and explicitly fenced out step 18a as requiring
                     a separate decision task first. IMMUTABLE; read-only),
                   TASK-022 (DONE — implemented Boss Response orchestration:
                     Passive charging, PassiveCharged/PassiveTriggered events,
                     Boss Skill, Boss Attack. Its code states Passive EFFECT
                     application is out of its scope for every Boss.
                     IMMUTABLE; read-only),
                   TASK-119 (IN REVIEW, review PASS — authored the Root ATK
                     BuffDebuff consumption rule at COMBAT_RULES.md §5.4, the
                     precedent for how a Boss-originated stat modifier reaches
                     a stat. IMMUTABLE; read-only),
                   TASK-117 (DONE — ADR-017 and the NextAttackCritModifiers[]
                     state model. IMMUTABLE; read-only)
Blocks:            Boss Passive effect implementation (step 18a) — Hỏa Long's
                   Rage, Thủy Ma's healing reduction, and Mộc Yêu's
                   regeneration cannot be implemented until this contract is
                   recorded, and through it ROADMAP.md Phase 1's "3 MVP Bosses
                   (Passive + Skill each)" and its complete Boss Response
                   sequence.
Estimate:          Simple (present the evidence, obtain and record one
                   decision set across at most three owner documents; no code,
                   no tests, no migration, no ADR unless reported)
```

**Type classification note.** `DOCUMENTATION`, not `GAMEPLAY-CHANGE` and not
`ARCHITECTURE`. The deliverable is a recorded contract decision in its canonical
owner document. Whether the answer *implies* a new Domain member, a changed
`BossState`, an event, or an ADR is a consequence the task **reports** — and if
the answer requires one, that is a separate follow-up task created after the
decision is recorded, not an act of this task. Authoring the answer itself is
the Product Owner's, not an agent's (`AGENTS.md` §7).

**Type Re-Classification Condition.** This task must be re-typed rather than
executed if the Product Owner's answer **changes how an already-documented
mechanic behaves** rather than filling in an unspecified rule. Specifically:

```text
- If Rage's representation requires a new battle-state concept (a Boss
  modifier collection, a source registry, or a per-Boss stat representation),
  that is a battle-state model change: AGENTS.md §18 requires an ADR, and the
  owning edits are an ARCHITECTURE task — REPORTED here, not authored.
  (Contrast TASK-116 → TASK-117 → ADR-017.)

- If the healing reduction requires changing COMBAT_RULES.md §4's existing
  Heal rules (as opposed to naming an application site for a modifier §4
  already anticipates in its item 6), that is a change to an existing,
  already-authored rule — the correctness route is a GAMEPLAY-CHANGE task
  (TASK_TYPES.md §2, development/gameplay-change.md §3).

- If regeneration requires a new Battle Event, a new SignalR member, or a
  new persistence column, each is REPORTED for a separate task.
```

That determination is made **after** the answer is obtained and is reported
here, not pre-judged. The default expectation is `DOCUMENTATION`, because the
step-18a *effect rules* are currently **absent**, not **different**.

**This task authors no rule and no value.** It must not choose a
representation, an application site, an application point, or a duration model.
Its job is to state the gap precisely, present the viable options with their
documented consequences, obtain the decision, and record it — the same shape
TASK-116 used for D-1–D-8 and TASK-119 used for D-1–D-5.

**No balance value is authored.** Hỏa Long's `+20% ATK` and `3 turns`, Thủy Ma's
`−50% healing`, and Mộc Yêu's `5% MaxHP` are `BOSS_RULES.md` §6.2's. **None is
this task's to change.** Only the missing *representation, application site, and
duration semantics* that consume those already-authored values are at issue.

**This task does not implement step 18a.** The Boss Response code path remains
unchanged and emits a trigger without applying an effect, exactly as it does
today.

---

## Objective

Resolve, from authoritative documents and a human/Product-Owner answer only, the
**Boss Passive effect contract** that `GAME_RULES.md` §17 step 18a requires and
that `docs/` does not currently define: how each MVP Boss Passive effect is
represented in authoritative battle state, what the effect's application site
is, when it expires, and whether regeneration is a state mutation or a damage
pipeline event — so that Hỏa Long's Rage, Thủy Ma's healing reduction, and Mộc
Yêu's regeneration can be implemented deterministically without inventing a
gameplay rule.

Concretely, this task makes the decision points explicit and evidenced against
`docs/`, obtains a human/Product-Owner answer for each, and records each settled
answer — without authoring any representation, application site, or duration
rule on the agent's own authority.

---

## Authoritative References

### The gap and its owners (READ ONLY — this task records, it does not redefine)

- `docs/01-game-design/GAME_RULES.md` **§17 step 18a** — the **only** place the
  effect application is instructed, and it is one sentence: "Boss Passive —
  evaluate the Boss's Passive trigger condition against the post-damage battle
  state. If the trigger is met, **apply the Passive effect** and emit
  `PassiveCharged`/`PassiveTriggered` (`BOSS_RULES.md` §3, `GAME_EVENTS.md`
  §2)." **The step names an obligation to apply an effect and defines neither
  the effect's representation nor its application site. This is the gap.**
- `docs/01-game-design/BOSS_RULES.md` **§3 / §3.3** — the Boss Passive timing
  contract: "Boss Passive fires at Step 18 ... after Player Damage (Steps
  15–17) and before Boss Skill (step 18b) and Boss Attack (step 18c)"; item 1
  "The Passive fires once per player action, after all player damage is
  resolved"; item 4 "Boss Skill damage does not re-trigger the Boss Passive."
  **The timing is fully specified. The effect's mechanics are not.**
- `docs/01-game-design/BOSS_RULES.md` **§6.2** — the three MVP effect rows:
  ```text
  Hỏa Long    Gain +20% ATK (Rage) for 3 turns          Every 5 Player Matches
  Thủy Ma     Active Pet healing reduced by 50% for 3 turns   Passive (always active)
  Mộc Yêu     Regenerate 5% MaxHP                       Every 5 Player Matches
  ```
  plus the note that Thủy Ma's always-on trigger "has no PassiveThreshold for
  match counting, is never charged via `PassiveTracker.Charge` on Player
  Matches, and emits no `PassiveCharged`/`PassiveTriggered` from match
  progress. **Its always-on effect application is a separate concern (TASK-022
  implements charging/events only, not effects).** **This is the statement that
  the effect half was deliberately deferred — and it was never picked up.**
- `docs/01-game-design/COMBAT_RULES.md` **§4** — Healing and Shields. Item 1:
  "Heal effects restore HP up to Max HP; overheal is discarded unless a Relic
  explicitly grants overheal/temp-HP." Item 6: "Heal and Shield amounts are NOT
  subject to the Damage Pipeline (§3) — they are not damage — but they ARE
  subject to their own explicit modifiers (e.g. a Relic that increases Heal
  Card effectiveness)." **§4 fixes that a healing modifier is possible in
  principle and names no mechanism, no application site, and no ordering for
  one. This is Decision B's gap.**
- `docs/01-game-design/COMBAT_RULES.md` **§1.1** — the `ATK` stat row
  (attribute: "attack power"; MVP default: `100`) and the `Power` range note.
  **It gives `ATK` a default and no range, and defines no modifier
  representation for the Boss side. This is Decision A's gap.**
- `docs/01-game-design/COMBAT_RULES.md` **§5.1 / §5.2 / §5.3 / §5.4** — the
  Status Effect rules. §5.1's MVP list is "Burn / Shield / Buff/Debuff"; §5.2
  item 1 gives every Status Effect "a duration (in Turns) or a trigger-based
  expiry"; §5.2 item 2 fixes the MVP stacking default ("refresh duration, do
  not stack magnitude"); §5.3 (DR1–DR6) is the **canonical owner** of
  Turn-based duration consumption; §5.4 (TASK-119) is the **canonical owner**
  of how a `BuffDebuff`'s `Magnitude` reaches the stat its `TargetStat` names
  (`EffectiveAttack`, integer-only, base never written). **§5.4's consumer
  lives in `StatusEffectLifecycle` and is applied to the *Player* → Boss step-15
  `Attack` argument. It is written for the active Pet's ATK; whether it also
  governs a Boss-side ATK modifier is not stated. This is Decision A's second
  gap.**
- `docs/01-game-design/GAME_RULES.md` **§18** (server authority) and **§16**
  (canonical event list — no `BossEnraged`, no heal-modifier event, no
  regeneration event).

### The state model the answer must reconcile with

- `docs/02-technical/GAME_STATE.md` **§2.4** — the `BossState` tree. It carries
  `BossId`, `HP`/`MaxHP`, `ATK`, `DEF`, `State`, `PassiveId`,
  `PassiveProgress`, `SkillCharge`, `SkillCooldown`, and `StatusEffects[]`.
- `docs/02-technical/GAME_STATE.md` **§2.4.2** — "Boss Passive": "`PassiveId`
  identifies which Passive definition the Boss carries ... set at battle
  creation and never changes. `PassiveProgress` tracks progress toward the
  Passive's threshold." **The section defines identity and progress only. It
  defines no effect representation, no active-effect collection, and no
  effect duration.**
- `docs/02-technical/GAME_STATE.md` **§2.4.4** — Enrage: a permanent state
  transition with "no timer or duration field for MVP". **Enrage is a
  documented precedent for a Boss-side persistent state change, and it is
  distinct from Rage — do not conflate them. Whether Rage reuses `State` is
  Decision A's question, not an assumption.**
- `docs/02-technical/GAME_STATE.md` **§2.4.5** — Stunned: "tracked by
  `StatusEffects[]` (§2.3.1, §5.1.1 — `Type = "State"`, Turn-countdown
  model)", with `State` a "materialized reflection of the Stun instance, not
  a second duration counter". **This is the documented precedent for
  representing a Boss-side timed state through the existing
  `StatusEffects[]` collection, including the "reflection, not a second
  counter" rule a Rage representation would have to respect.**
- `docs/02-technical/GAME_STATE.md` **§2.4.1** — Staged `BossState` fields.
  It records what `BossState` does and does not currently add, including that
  a `StatusEffects[]`-shaped collection "is defined because Stun (§2.4.5) and
  future ..." effects need one.
- `docs/02-technical/GAME_STATE.md` **§2.3.1** — the `StatusEffect` instance
  schema and its duration-model dichotomy: "`Type` selects exactly one duration
  model, and the two are exclusive ... **never both and never neither**"
  (`RemainingTurns` for `DoT`/`BuffDebuff`/`State`; `ExpiryCondition` for
  `Shield`). Item 7 fixes the **`TargetStat`-iff-`BuffDebuff`** pairing. Item 6:
  "There is never more than one instance per effect identity per entity."
  **A `+20% ATK` Rage is expressible as a `BuffDebuff` with `TargetStat = "ATK"`
  — subject to item 6's uniqueness rule and §5.4's consumer being applicable to
  a Boss attacker, which §5.4 does not state.**
- `docs/02-technical/GAME_STATE.md` **§5.1** / **§5.1.1** — the single atomic
  write-back and the step-19a lifecycle. §5.1.1 item 2: **exactly one**
  decrement per Turn, at step 19a; item 6: the pass order is by `Id` ordinal
  ascending and is "part of the contract"; item 10: "Nothing here is
  published. This lifecycle adds no event, no payload member, and no SignalR
  method". **Any Turn-duration Rage or healing reduction must ride this
  existing lifecycle; a Heal-percentage modifier must state whether it is
  decremented here.**
- `docs/02-technical/DATABASE.md` **§1** — the persisted `PassiveDefinition`
  object of a Boss definition. Its member list is fixed by TASK-045 and
  documented in `BossDefinition.cs` as exactly
  `{ "passiveId", "threshold", "resetBehavior" }`: "**Exactly four members, all
  required** ... No `damageType`, `element`, `name`, `description`, `target`,
  or `effects` member exists", and "the effect declaration must not become a
  fifth stored member; adding one would change the database contract."
  **The persisted contract deliberately carries no effect member. Any
  representation the answer chooses must therefore either be Domain
  configuration (like TASK-118's `BossSkillSecondaryEffect`, declared outside
  the persisted constructor) or a reported schema change.**

### The implementation evidence (READ ONLY — states what exists, not what is correct)

- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — the step
  18a region. It performs the match-charged Passive evaluation and emits the
  events, and its own comment states the boundary verbatim: "**Passive EFFECT
  application is out of this task's scope for every Boss** (`BOSS_RULES.md` §3
  item 3, §6.2): a trigger emits its event and applies nothing." Thủy Ma's
  always-active trigger skips charging entirely. **The trigger path exists; the
  effect path deliberately does not.**
- `src/backend/GameServer.Domain/Bosses/BossDefinition.cs` — `BossPassiveDefinition`
  is exactly `(PassiveId PassiveId, int? Threshold, string ResetBehavior)`.
  `BossSkillDefinition` separately carries `BossSkillSecondaryEffect?`, declared
  **outside** the persisted constructor. **The Boss Skill effect has a
  representation and the Boss Passive effect does not: that asymmetry is the
  gap in code form.**
- `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` —
  `EffectiveAttack(int attack, IReadOnlyList<StatusEffect> effects)`, the
  TASK-119 consumer of a `TargetStat = "ATK"` `BuffDebuff`. It is applied at the
  step-15 Player → Boss `DamagePipeline.Calculate` call's `Attack` argument.
  **It is written for the Pet's ATK; nothing states whether it also governs the
  Boss's ATK at step 18c, or a Boss self-buff at step 18a.**
- `src/backend/GameServer.Domain/Battle/BossState.cs`, `BossStateKind.cs` —
  the Boss state members. `BossStateKind` is a closed set including `Idle`,
  `Enraged`, and `Stunned`. **There is no `Rage` and no effect collection.**

**Read-only note.** These files are inspected to establish what exists. No file
under `src/` or `tests/` is modified by this task, and no finding below is
derived from what the code happens to do.

### Technical and governance contracts the answer must not contradict

- `docs/02-technical/TDD.md` **§6** — the single server-seeded PRNG; the answer
  must not introduce a second stream (`ADR-009`, `GAME_STATE.md` §2.6.3).
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§4.2 / §4.3** — what the state push
  does and does not carry. **The answer must not require a new wire member; if
  it does, that is REPORTED for a separate protocol task.**
- `docs/02-technical/GAME_EVENTS.md` **§2** — the canonical event list and the
  `PassiveTriggered` payload. **The answer must not add an event; TASK-022
  already fixed that the trigger emits and nothing else.**
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md` — server
  authority; every effect's evaluation and mutation is server-side.
- `docs/03-decisions/README.md` **§8** — "Known Open Items (Not ADRs)", which
  must be checked before asserting an ADR is needed.

### Scope, governance, and precedent

- `docs/00-overview/MVP_SCOPE.md` §1 (Bosses — "Element, Passive, Skill per
  Boss" — and Combat — Status Effects are named), §2 (OUT), §4 (unlisted is not
  implicitly IN).
- `docs/00-overview/ROADMAP.md` — Phase 1 names "3 MVP Bosses (Hỏa Long, Thủy
  Ma, Mộc Yêu — Passive + Skill each; see `BOSS_RULES.md` §6)" and "Boss
  Response (Passive → Skill → Attack → Victory/Defeat)"; "No Relics yet".
- `tasks/backlog/TASK-118-implement-boss-skill-secondary-effects.md` — the task
  that implemented step 18b and fenced step 18a out, instructing that it
  "require[s] a separate decision task first". **IMMUTABLE; NOT modified.**
- `tasks/backlog/TASK-119-resolve-root-atk-modifier-consumption-contract.md` —
  the closest contract-resolution precedent: a Boss-originated stat modifier
  resolved inside the existing `StatusEffects[]` model, with no ADR required.
  **Read-only.**
- `tasks/backlog/TASK-116-resolve-nextattack-crit-modifier-state-and-consumption-contract.md`
  — the precedent for how a decision set is recorded verbatim, when an ADR
  becomes required, and how that requirement is reported rather than authored.
  **Read-only.**
- `AGENTS.md` §4 (conflict resolution), §7 (never invent a rule or content),
  §9 (anti-overengineering), §10 (server authority), §11 (Determinism & RNG),
  §15 (testing), §17 (documentation change rule), §18 (ADR rule), §20 (stop
  conditions — "Missing rule", "Ambiguous requirement");
  `.ai/workflow/documentation/documentation-change.md` §2 (no duplication) and
  §3 (determining the canonical owner); `tasks/README.md` §9 (no business-rule
  duplication in task files), §12 (skill budget).

**ADR check (to be confirmed, not assumed, by the executing agent):** this task
is expected to require **no** ADR if the answer fits inside the existing
`StatusEffects[]` model (as Stun and Root already do) or inside existing Boss
Domain configuration (as TASK-118's `BossSkillSecondaryEffect` already does). It
**would** require one if the answer introduces a new battle-state concept (a
Boss modifier collection, a Boss stat-modifier registry, or a Boss
healing-modifier representation) — that is a battle-state model change under
`AGENTS.md` §18 and `TASK_TYPES.md`'s ARCHITECTURE type, and it is **reported**
and becomes a separate task, not authored here. `docs/03-decisions/README.md` §8
must be checked before asserting an ADR is needed.

---

## Current State

The Boss Passive effect contract does not exist. Verified directly in the
working tree:

```text
GAME_RULES.md §17 step 18a      "evaluate the Boss's Passive trigger condition
                                against the post-damage battle state. If the
                                trigger is met, apply the Passive effect and
                                emit PassiveCharged/PassiveTriggered." — an
                                OBLIGATION to apply an effect. The
                                representation and application site are
                                absent from every document.

BOSS_RULES.md §6.2              Three effect rows exist (+20% ATK / 3 turns;
                                −50% healing / 3 turns; 5% MaxHP per
                                trigger). The numeric magnitudes are authored.
                                The MECHANICS that consume them are not.
                                Its closing note states the always-on effect
                                "application is a separate concern (TASK-022
                                implements charging/events only, not effects)"
                                — the deferral, never subsequently picked up.

BOSS_RULES.md §3.3              Timing fully specified (fires once per player
                                action, post-damage, before 18b/18c; Boss Skill
                                damage does not re-trigger). No effect
                                mechanics.

COMBAT_RULES.md §4 item 6       A healing modifier is POSSIBLE ("they ARE
                                subject to their own explicit modifiers (e.g.
                                a Relic that increases Heal Card
                                effectiveness)"). No mechanism, no application
                                site, no ordering for one is defined, and the
                                Boss-originated case is not addressed.

COMBAT_RULES.md §5.4            The TASK-119 consumer
                                (StatusEffectLifecycle.EffectiveAttack) is the
                                canonical rule for how a `TargetStat = "ATK"`
                                BuffDebuff reaches a stat. It is written and
                                wired for the PET's ATK (the step-15
                                Player → Boss `Attack` argument). Whether it
                                governs a BOSS-side ATK modifier is not stated.

GAME_STATE.md §2.4.2            Boss Passive section defines `PassiveId` and
                                `PassiveProgress` ONLY. No effect
                                representation, no active-effect collection, no
                                effect duration.

GAME_STATE.md §2.4.4 / §2.4.5   Enrage is "no timer, no duration field".
                                Stunned IS a Boss-side timed state and IS
                                tracked by `StatusEffects[]` with `State` as a
                                "materialized reflection ... not a second
                                duration counter". These are the two available
                                precedents; which (if either) Rage follows is
                                NOT specified.

DATABASE.md §1                  The persisted `PassiveDefinition` is exactly
                                { "passiveId", "threshold", "resetBehavior" }.
                                "the effect declaration must not become a fifth
                                stored member; adding one would change the
                                database contract."

BattleStateService.cs (18a)     "Passive EFFECT application is out of this
                                task's scope for every Boss ... a trigger emits
                                its event and applies nothing."
BossDefinition.cs               BossPassiveDefinition = (PassiveId, Threshold?,
                                ResetBehavior) — NO effect member, while
                                BossSkillDefinition DOES carry
                                BossSkillSecondaryEffect? (declared outside the
                                persisted constructor).
BossStateKind.cs                Closed set: Idle | Enraged | Stunned. No Rage.
```

**The gap.** Implementing step 18a must resolve:

```text
How is Hỏa Long's Rage (+20% ATK, 3 turns) represented in Battle State and
whose ATK does it modify?
        → NOT SPECIFIED. §2.4.2 defines no effect representation; §2.4.4's
          Enrage has no duration; §5.4's consumer is written for the Pet's
          ATK, not the Boss's.

Where is Thủy Ma's −50% healing applied, and what does it modify?
        → NOT SPECIFIED. COMBAT_RULES.md §4 item 6 permits a healing modifier
          and defines no mechanism, site, or ordering; §4's Heal rules name
          the Relic case only.

Is Mộc Yêu's 5% MaxHP regeneration a state mutation or a pipeline event, and
at what point does it resolve?
        → NOT SPECIFIED. No document states whether regeneration is a direct
          HP write, whether it traverses the Damage Pipeline (it must not —
          §4 item 6 makes Heal non-damage), and no event exists for it
          (GAME_RULES.md §16).
```

`BOSS_RULES.md` §6.2's three effect rows are resolved **game intent**.
"A Boss Passive effect is represented, applied at a defined site, and expires
deterministically" is an **unresolved contract**. An agent cannot close it by
choosing a representation: `GAME_STATE.md` §2.4.2 defines no effect
representation, Stun's `StatusEffects[]` precedent is explicitly a *materialized
reflection* of one specific effect, and `DATABASE.md` §1 forbids extending the
persisted `PassiveDefinition`. Choosing an application site for the healing
modifier is equally unavailable: `COMBAT_RULES.md` §4 item 6 names its
existence and no site.

**What this does NOT change.** Boss Passive **timing** (step 18a, once per
player action, post-damage, before 18b/18c, not re-triggered by Boss Skill
damage) is frozen by `BOSS_RULES.md` §3.3 and `GAME_RULES.md` §17 and must not
be re-decided. Boss Passive **charging, thresholds, reset behavior, and events**
are frozen by `TASK-022`, `PASSIVE_RULES.md` §2/§4, and `BOSS_RULES.md` §6.2.
Enrage's contract is frozen by `BOSS_RULES.md` §5 item 4. Stun's
`StatusEffects[]` representation is frozen by `GAME_STATE.md` §2.4.5. The Boss
Skill secondary effects (18b) are frozen by `TASK-118`. The Root ATK
consumption rule is frozen by `TASK-119` / `COMBAT_RULES.md` §5.4. The sole open
questions are the **representation, application site, duration semantics, and
regeneration application point** of the three §6.2 Passive effects.

---

## STOP CONDITION (`AGENTS.md` §4 / §7 / §20; `.ai/README.md` §13)

<!--
  Recorded by the executing agent on pickup. This task did NOT reach DONE and
  authored no rule. Format follows .ai/README.md §13.
-->

```text
STOP CONDITION

Problem:
TASK-123 exists to obtain and record Product Owner decisions for the Boss
Passive effect contract (GAME_RULES.md §17 step 18a). On execution, two
independent stop conditions fired: (1) the required Product Owner decisions
are not available — no recorded answer exists for any of D-1/D-2/D-3/D-4, and
an agent may not author them (AGENTS.md §7, §20 "Missing rule"); and (2) two
authoritative documents materially contradict each other on whether a Boss
may carry a stat modifier at all (AGENTS.md §4, §20 "Rule conflict").

PARTIAL RESOLUTION (recorded after the initial STOP): the Product Owner has
since supplied binding decisions for BOTH contradictions — see "Product Owner
Decisions" D-1 (Contradiction A) and D-2a (Contradiction B). Neither
contradiction remains a blocker. The task nevertheless stays BLOCKED, because
the decision set is still incomplete: D-1-representation, D-1a, D-1b, D-1c,
D-2b, D-2c, D-2d, D-2e, and all of D-3 and D-4 remain unanswered.

Relevant sources:
  CONTRADICTION A — can the Boss carry a stat modifier?  ✅ RESOLVED by the
  Product Owner decision recorded under "Product Owner Decisions". Retained
  below as the historical record of what fired the STOP.
    docs/01-game-design/COMBAT_RULES.md §3.4 (line 393):
      "Step 4 — Other Modifiers = 1.0 (MVP: no Relic/Passive/Buff modifiers
       on Boss side)"
    docs/01-game-design/COMBAT_RULES.md §5.4.5 (line 741):
      "Does NOT apply  to the Boss's damage — §3.4 pins the Boss side's Step 4
       [to 1.0], and this rule authors no Boss-side factor"
    versus
    docs/01-game-design/BOSS_RULES.md §6.2 (line 187):
      "Hỏa Long    Gain +20% ATK (Rage) for 3 turns     Every 5 Player Matches"

    RESOLUTION: both contracts are kept. Rage modifies the Step-1 `Attack`
    input (via a derived EffectiveBossATK), not Step 4; §3.4 and §5.4.5 are
    unchanged; BossState.ATK remains the immutable/base value.

  CONTRADICTION B — Thủy Ma's activation model (internal to one row)
  ✅ RESOLVED by the Product Owner decision recorded under "Product Owner
  Decisions / D-2a". The "for 3 turns" wording is authoritative; the effect is
  a triggered temporary effect following the existing TurnBased 3-turn
  lifecycle; "Passive (always active)" is retired as stale wording. Retained
  below as the historical record.

    docs/01-game-design/BOSS_RULES.md §6.2 (line 188):
      "Thủy Ma     Active Pet healing reduced by 50% for 3 turns
                   Passive (always active)"
    → the same table row asserts BOTH a 3-turn duration AND an always-active
      trigger. BOSS_RULES.md §6.2 (lines 194-200) further states Thủy Ma
      "is never charged via PassiveTracker.Charge on Player Matches, and
      emits no PassiveCharged/PassiveTriggered from match progress", so no
      documented path re-applies a 3-turn effect.

    RESOLUTION: the duration is authoritative; the effect is triggered and
    temporary. NOTE: this exposes a NEW gap — with the "always active"
    trigger retired, WHAT triggers the effect is undefined pending D-2b.

  GAP (no contradiction, but no rule) — regeneration application point and
    observability:
    docs/01-game-design/BOSS_RULES.md §6.2 (line 189):
      "Mộc Yêu     Regenerate 5% MaxHP                    Every 5 Player Matches"
    docs/01-game-design/GAME_RULES.md §16 (canonical event list, lines 282-293)
      contains no Boss-heal, regeneration, or BossHPChanged event.
    docs/01-game-design/COMBAT_RULES.md §4 item 6 states Heal is not subject
      to the Damage Pipeline — so regeneration cannot be a pipeline event.
    No document states the rounding rule, the MaxHP clamp, or the position
      relative to BOSS_RULES.md §5 item 4's Boss-HP terminal check.

Conflict / missing information:
  The unresolved items are exactly D-1 (Rage representation, D-1a target ATK,
  D-1b duration model, D-1c reapplication), D-2/D-2a/D-2b (Thủy Ma activation,
  application site, reach), D-2c/D-2d (duration, reapplication), D-3 (Mộc Yêu
  timing, base, rounding, clamping, observability, repetition), and D-4
  (event/wire/storage consequences). None is answered in docs/, and none may
  be authored by an agent.

  Blocks downstream: step 18a implementation (Hỏa Long Rage, Thủy Ma healing
  reduction, Mộc Yêu regeneration). TASK-118 explicitly deferred this and
  instructed that it "require[s] a separate decision task first"; TASK-123 is
  that task and it has now STOPPED.

Proposed resolution:
  The smallest change that would resolve this is a Product Owner decision set
  answering D-1, D-2(D-2a), D-3, and D-4 — recorded verbatim in this task
  file, exactly as TASK-116 recorded D-1–D-8 and TASK-119 recorded D-1–D-5.
  For Contradiction A specifically, the Product Owner must also state which
  document is authoritative, because the two are mutually exclusive:
    - either COMBAT_RULES.md §3.4's "no ... Buff modifiers on Boss side" is
      correct, in which case BOSS_RULES.md §6.2's Rage row cannot be
      implemented as an ATK modifier and §6.2 must be corrected; or
    - BOSS_RULES.md §6.2's Rage row is correct, in which case
      COMBAT_RULES.md §3.4's Step-4 pinning and §5.4.5's scope boundary must
      be amended by a GAMEPLAY-CHANGE task.
  Per AGENTS.md §4 step 6, no behavior is changed until that approval exists.

Waiting for:
  Product Owner answers to D-1-representation/D-1a/D-1b/D-1c, D-2c-duration,
  D-2d, D-2e, D-3/D-3a–D-3f, and D-4.

  ✅ ALREADY ANSWERED (recorded under "Product Owner Decisions"):
  D-1 (Contradiction A), D-2a (Contradiction B), D-2b, D-2b-site, and D-2c
  (the canonical shared Heal Resolution mechanism — note this RE-SCOPED D-2c
  from its originally-tracked duration subject; the duration boundary is
  re-tracked as D-2c-duration and is still awaiting an answer).
```

**What the executing agent did NOT do.** It did not choose a representation, an
application site, a duration model, a rounding rule, or an observability model.
It did not implement step 18a. It did not open a GAMEPLAY-CHANGE task. It did
not edit `COMBAT_RULES.md`, `BOSS_RULES.md`, or `GAME_STATE.md` to reconcile the
contradiction. It recorded the contradiction and stopped.

---

## Verification Record (Discovery Pass)

<!--
  Evidence established by the executing agent by direct inspection. Each line
  was read, not inferred. Classified per the task's "do not guess" rule:
  FACT (documented) / IMPL (code behavior) / MISSING (no rule exists).
-->

```text
ITEM                                          CLASS     SOURCE
--------------------------------------------  --------  --------------------------------
Step 18a instructs "apply the Passive effect" FACT      GAME_RULES.md §17 step 18a
Boss Passive timing (post-damage, before      FACT      BOSS_RULES.md §3.3 items 1-4
  18b/18c; Skill damage does not re-trigger)
Rage = "+20% ATK for 3 turns", every 5        FACT      BOSS_RULES.md §6.2 line 187
  Player Matches
Thuy Ma = "-50% healing for 3 turns",         FACT      BOSS_RULES.md §6.2 line 188
  trigger "Passive (always active)"
Moc Yeu = "Regenerate 5% MaxHP", every 5      FACT      BOSS_RULES.md §6.2 line 189
  Player Matches
Boss-side Step 4 pinned to 1.0, "no           FACT      COMBAT_RULES.md §3.4 line 393
  Relic/Passive/Buff modifiers on Boss side"
§5.4 ATK modifier explicitly does NOT apply   FACT      COMBAT_RULES.md §5.4.5 line 741
  to the Boss's damage
§5.4 consumer is wired to the PET's           FACT      COMBAT_RULES.md §5.4.1/§5.4.3
  step-15 Player->Boss Attack argument
Heal is NOT subject to the Damage Pipeline    FACT      COMBAT_RULES.md §4 item 6
Heal amounts ARE subject to "their own        FACT      COMBAT_RULES.md §4 item 6
  explicit modifiers (e.g. a Relic ...)" —
  no mechanism/site/ordering defined
No healing-modifier application site,         MISSING   (no document)
  mechanism, or ordering exists
Canonical event list has no regeneration,     FACT      GAME_RULES.md §16 lines 282-293
  Boss-heal, or BossHPChanged event
StatusEffect instance schema (Id/Type/         FACT      GAME_STATE.md §2.3.1
  Source/Magnitude/TargetStat?/
  RemainingTurns?/ExpiryCondition?)
Duration models are an exclusive dichotomy    FACT      GAME_STATE.md §2.3.1 item 3
  ("never both and never neither")
One instance per effect identity              FACT      GAME_STATE.md §2.3.1 item 6
BossState.StatusEffects[] exists with the     FACT      GAME_STATE.md §2.4, §2.4.1
  same schema+lifecycle as PetState's
BossState.StatusEffects[] is "defined         FACT      GAME_STATE.md §2.4.1
  because Stun (§2.4.5) and future content
  are tracked through it"
Stun = Boss-side Turn-countdown status         FACT      GAME_STATE.md §2.4.5
  effect via StatusEffects[], State is a
  "materialized reflection ... not a second
  duration counter"
Enrage = permanent, "no timer or duration      FACT      GAME_STATE.md §2.4.4
  field for MVP" (distinct from Rage)
Boss Passive section defines PassiveId +       FACT      GAME_STATE.md §2.4.2
  PassiveProgress ONLY — no effect
  representation, no duration
Persisted PassiveDefinition is exactly         FACT      DATABASE.md §1;
  {passiveId, threshold, resetBehavior};                BossDefinition.cs
  "must not become a fifth stored member"
BossPassiveDefinition = (PassiveId,            IMPL      BossDefinition.cs:332-335
  Threshold?, ResetBehavior) — no effect
  member, while BossSkillDefinition DOES
  carry BossSkillSecondaryEffect? (declared
  outside the persisted constructor)
Step 18a code emits the trigger and applies    IMPL      BattleStateService.cs 18a region
  nothing; its own comment states "Passive
  EFFECT application is out of this task's
  scope for every Boss"
BossStateKind = Idle | Enraged | Stunned;      IMPL      BossStateKind.cs
  no Rage
EffectiveAttack(attack, effects) is the        IMPL      StatusEffectLifecycle.cs
  TASK-119 consumer, applied only to the
  step-15 Player->Boss Attack argument
PassiveTriggered "effect summary" is           FACT      GAME_EVENTS.md §2 item 3;
  "not populated yet" and its absence means              SIGNALR_PROTOCOL.md §3.2.25
  "not yet reported" (not "no effect")
NextAttackCritModifiers[] precedent: state     FACT      SIGNALR_PROTOCOL.md §4 item 14
  added by ADR-017 is NOT a wire member;
  no document requires its exposure
ADR-017, GAME_STATE.md §2.3.4, COMBAT_RULES    FACT      (cited inline)
  §5.4.4 all establish "base stat is never
  overwritten; effective value is derived,
  not stored"
docs/03-decisions/README.md §8 open items:     FACT      README.md §8 lines 217-225
  only "Backend runtime assumption" and
  "ADR-009 PCG32 Proposed" — the Boss
  Passive effect gap is NOT listed there
```

**Balance-value boundary (unchanged).** `+20% ATK`, `3 turns`, `−50%`, and
`5% MaxHP` are `BOSS_RULES.md` §6.2's. None was changed, authored, or
confirmed by this task.

---

---

## Product Owner Decisions

<!--
  ANSWERED — Product Owner. These decisions are the deliverable of this task.
  An agent authored none of them (AGENTS.md §7).
-->

### D-1 (Contradiction A) — Rage's consumption point and the §3.4 reconciliation

**Status: DECIDED (binding).** Supplied by the Product Owner. This resolves
Contradiction A recorded in the STOP CONDITION. It does not, by itself, answer
D-1's remaining sub-questions (representation, D-1a, D-1b, D-1c) nor D-2/D-3/D-4.

**Recorded verbatim as supplied:**

```text
Keep both contracts.

Hỏa Long Rage is a temporary +20% Boss ATK modifier.

It is applied when deriving EffectiveBossATK BEFORE the Boss
Damage Pipeline.

Flow:

BossState.ATK
→ Rage modifier (+20%)
→ EffectiveBossATK
→ Boss Damage Pipeline

COMBAT_RULES.md §3.4 remains unchanged:
Boss-side Step 4 = 1.0 and receives no direct
Relic/Passive/Buff modifier.

Rage therefore modifies the Step-1 Attack input, not Step 4.

BossState.ATK remains the immutable/base ATK value.
```

**Bindable statement (C-1).** Both documents are retained without amendment,
because the decision separates the two concerns Contradiction A had conflated:

```text
- §3.4's pinning concerns STEP 4. It stays exactly as written: the Boss side's
  Step 4 is 1.0 and receives no direct Relic/Passive/Buff modifier.
- Rage is NOT a Step-4 modifier. It alters the STEP-1 `Attack` input, before
  the pipeline runs.
- Therefore §3.4's "no Relic/Passive/Buff modifiers on Boss side" remains true
  OF STEP 4, and §5.4.5's scope boundary (the §5.4 consumer does not apply to
  the Boss's damage) is likewise not contradicted: Rage is a separate Boss-side
  rule producing a Step-1 input value; it does not extend §5.4 to the Boss.
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. The shape is exactly symmetric with the existing Pet-side precedent.
   COMBAT_RULES.md §5.4.1 defines the Pet modifier identically: "The modifier
   is consumed when the Damage Pipeline call for that attack is constructed, by
   changing the Step 1 `Attack` input it receives", with
   "EffectiveATK = the reduced PetState.ATK" and "Step 1 = EffectiveATK + ...".
   The decision applies that same shape to the Boss side, consistent with §3.4
   line 390's "Step 1 — Base Damage = Boss.ATK" (the value read is now the
   derived EffectiveBossATK).

2. Base preservation matches §5.4.4's non-destructive rule and ADR-017 /
   GAME_STATE.md §2.3.4: "BossState.ATK remains the immutable/base ATK value"
   is the same contract §5.4.4 states for PetState.ATK ("is never overwritten
   by the modifier, and there is no 'restore' step"), including §5.4.4's
   prohibition on restoring by configuration default.

3. "EffectiveBossATK" is a NEW named value, and is flagged as new.
   No document currently defines a Boss-side effective-ATK symbol. §5.4.4's
   `EffectiveATK` is explicitly the Pet's, and is "NOT stored" and "not
   persisted"; EffectiveBossATK follows that same derived, non-stored,
   single-pipeline-execution character and must NOT become a stored
   BattleState member.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- §3.4 is NOT amended. The decision explicitly retains it.
- §5.4.5's scope boundary is NOT amended. It remains true.
- The Boss-side rule must be authored at COMBAT_RULES.md as a NEW subsection
  beside §5.4 (e.g. §5.5 "Boss Stat Modifiers"), stating the Step-1
  consumption point and base preservation, and referencing §3.4 rather than
  restating it.
- The Step-1 value already reaches the client as `DamageCalculated.base`
  (SIGNALR_PROTOCOL.md §3.2.13, an int): this is a VALUE CHANGE within an
  existing member, not a new member. No new event, wire member, Redis key, or
  database column is required — the same conclusion §5.4's closing paragraph
  reached for the Pet side.
- No ADR is required BY THIS DECISION: no new battle-state concept is
  introduced; EffectiveBossATK is derived and not stored, exactly as §5.4.4
  states for EffectiveATK. Whether the Rage INSTANCE needs a new state
  representation remains open (below).
```

**Still OPEN under D-1** (the Contradiction-A decision fixed the consumption
point and base preservation; these were subsequently resolved — see below):

```text
D-1-representation  ✅ RESOLVED — see "D-1 — Representation" below.
D-1a                ✅ RESOLVED — see "D-1a — Damage scope" below.
D-1b                "for 3 turns" — the §5.3 Turn countdown, or another
                    boundary? (§5.3.2 scopes the countdown to Turn-based
                    Buff/Debuff Status Effects; a Boss-side instance's
                    membership is not stated.)  → STILL OPEN
D-1c                Repeat trigger while active — refresh (§5.2 item 2's MVP
                    default), ignore, or stack?  → STILL OPEN
```

---

### D-1 — Representation (Rage instance home)

**Status: DECIDED (binding).** Supplied by the Product Owner. This resolves
D-1's representation sub-question.

**Recorded verbatim as supplied:**

```text
Hỏa Long Rage is represented as a TurnBased BuffDebuff
StatusEffect in BossState.StatusEffects[].

TargetStat = ATK
Magnitude = +20%
RemainingTurns = 3

BossState.ATK remains the immutable/base ATK value.
Rage is not stored as a separate BossState field.
```

**Bindable statement (C-10).**

```text
- The Rage instance is a TurnBased BuffDebuff StatusEffect held in
  BossState.StatusEffects[] (the collection GAME_STATE.md §2.4/§2.4.1 defines
  with the SAME element schema and lifecycle as PetState.StatusEffects[]).
- Id/Type: Type = "BuffDebuff"; TargetStat = "ATK".
- Magnitude = +20%   (BOSS_RULES.md §6.2's value, unchanged)
- RemainingTurns = 3 (BOSS_RULES.md §6.2's duration)
- BossState.ATK remains the IMMUTABLE/BASE value — Rage never writes it.
- Rage is NOT stored as a separate BossState field (no new member).
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. The representation ALREADY EXISTS and was documented for exactly this kind
   of content. GAME_STATE.md §2.4.1: "StatusEffects[] is the same collection
   contract as PetState's (§2.3.1): identical element schema, identical
   lifecycle (§5.1.1), and the same single-instance-per-identity rule. ... For
   MVP, no content-defined Boss applies a Status Effect to itself; the
   collection is defined because Stun (§2.4.5) and future content are tracked
   through it." Rage is that "future content".

2. The member set is satisfied by the existing schema — no new field.
   GAME_STATE.md §2.3.1 types the instance as Id / Type / Source / Magnitude /
   TargetStat? / RemainingTurns? / ExpiryCondition?. Item 7 fixes the
   TargetStat-iff-BuffDebuff pairing — Rage is Type "BuffDebuff" WITH
   TargetStat "ATK", exactly the pairing item 7 requires. Item 3 assigns
   RemainingTurns to BuffDebuff, exactly the duration model chosen.

3. It follows the Stun precedent, including Stun's critical caveat.
   §2.4.5 tracks Stun through StatusEffects[] with "Type = 'State',
   Turn-countdown model", and states State is "a materialized reflection of
   the Stun instance, not a second duration counter". Rage does not even need
   a reflection member: the decision stores NO separate BossState field, so
   the instance is the single source of truth. This is STRICTLY SIMPLER than
   Stun and consistent with it.

4. "BossState.ATK remains the immutable/base ATK value" restates, and does not
   contradict, the Contradiction-A decision (D-1) and §5.4.4's non-destructive
   rule. The +20% is DERIVED into EffectiveBossATK at attack resolution and is
   never stored (§5.4.4: "derived at attack resolution — NOT stored").

5. NO new battle-state concept is introduced — the collection, the element
   schema, the duration model, and the one-instance-per-identity rule all
   pre-exist. Therefore AGENTS.md §18 is NOT engaged and NO ADR is required.
   This is the TASK-119 precedent (a Boss-originated stat modifier resolved
   inside the existing StatusEffects[] model, no ADR), NOT the
   TASK-116 → TASK-117 → ADR-017 precedent (which was needed only because
   NextAttackCritModifiers[] was genuinely new).
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- GAME_STATE.md §2.4/§2.4.1/§2.4.2 need NO structural change: the collection
  exists, is implemented (TASK-095/TASK-096), and already serializes with
  BattleState. §2.4.1's "For MVP, no content-defined Boss applies a Status
  Effect to itself" sentence becomes stale once Rage is implemented and
  should be updated by the owning edit — REPORTED, not done here.
- No new BossState member, no new StatusEffect member, no new `Id`, `Type`, or
  `TargetStat` value.
- The instance is not a wire member (SIGNALR_PROTOCOL.md §4 item 14's
  precedent), and it rides the existing Redis write-back (REDIS_STATE.md §7
  item 9).
- No ADR.
```

**Still OPEN under D-1 after this decision:**

```text
D-1b  "for 3 turns" — duration model and consumption boundary.
      (NOTE: the same question was answered for Thủy Ma by D-2c-duration and
      D-2d. The two are structurally analogous — both are Turn-based
      instances with RemainingTurns and one-time/periodic triggers — so the
      Product Owner may wish to confirm whether the same schedule applies,
      rather than assume it.)
D-1c  Repeat trigger while active — refresh / ignore / stack.
      (NOTE: §5.2 item 2's MVP default is refresh-not-stack, and the same
      question was answered for Thủy Ma by D-2d. Hỏa Long's trigger IS
      match-charged ("Every 5 Player Matches"), so unlike Thủy Ma a genuine
      repeat case IS reachable in MVP play — see the reachability note in
      D-2d, which does not apply here.)
```

---

### D-1a — Damage scope (which Boss damage Rage reaches)

**Status: DECIDED (binding).** Supplied by the Product Owner. This resolves
which damage instances the +20% reaches.

**Recorded verbatim as supplied:**

```text
Rage modifies only Boss damage whose Step 1 Attack input is derived
from BossState.ATK.

Therefore:

BossState.ATK
→ +20% Rage
→ EffectiveBossATK
→ Damage Pipeline Step 1

A Boss Skill with an independently authored Base Damage value does NOT
receive the +20% Rage modifier merely because it is a Boss attack.

For example, Flame Burst's documented Base Damage = 150 remains 150
unless its own contract explicitly defines scaling from Boss ATK.

Rage therefore affects ATK-derived Boss attacks, not independent
fixed-damage skill magnitudes.
```

**Bindable statement (C-11).**

```text
- Rage reaches ONLY Boss damage whose Step-1 Attack input is DERIVED FROM
  BossState.ATK.
- Concretely, the Boss BASIC ATTACK: COMBAT_RULES.md §3.4 "Boss Basic Attack:
  Step 1 — Base Damage = Boss.ATK". Its Step-1 input becomes EffectiveBossATK.
- A Boss SKILL with an independently authored Base Damage does NOT receive the
  +20% merely by being a Boss attack.
- Flame Burst's documented Base Damage = 150 REMAINS 150 (BOSS_RULES.md
  §6.3.1 item 1) unless its own contract explicitly defines ATK scaling.
- No Boss Skill currently declares ATK scaling (see the evidence below).
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. §3.4 ALREADY separates the two damage shapes the decision distinguishes.
   COMBAT_RULES.md §3.4 states:
       "Boss Basic Attack: Step 1 — Base Damage = Boss.ATK"
       "Boss Skill: Same pipeline as above, but Step 1 Base Damage is defined
        per Skill (BOSS_RULES.md §6)."
   The decision's rule is that separation, made explicit: only the ATK-derived
   shape is modified. §3.4 is NOT amended — it already carries both statements.

2. §5.4.1's PARALLEL is direct, and the decision mirrors it exactly.
   COMBAT_RULES.md §5.4.1 item 2 for the PET says the modifier applies to
   "PetState.ATK ALONE" and that "The Skill/Card base value and the
   ATK-Gem-generated damage pool are separate Step-1 contributions (§3 step 1)
   and are NOT modified by this rule." The Boss-side decision states the same
   distinction for the Boss's Skill base value. This is symmetry, not new
   design.

3. The fixed-magnitude precedent is already documented for another Boss Skill.
   BOSS_RULES.md §6.3.1 item 1 (Flame Burst): "Burn Magnitude: Fixed 50 damage
   per tick (does not scale with Boss ATK, Pet ATK, percentage MaxHP, or
   elemental multipliers)." A Boss Skill value explicitly declared NOT to scale
   with Boss ATK already exists in the same Boss's contract, so the decision's
   treatment of Base Damage = 150 is consistent with the document's own
   established pattern.

4. NO documented Boss Skill declares ATK scaling — verified, so the decision's
   exception clause is currently vacuous and creates no ambiguity. Inspected
   BOSS_RULES.md §6.3.1 items 1–3:
       Flame Burst  Base Damage 150 — "150 (deals damage through Damage
                    Pipeline to active Pet)"; no scaling statement.
       Drain Power  Base Damage 120 — same shape; no scaling statement.
       Root         Base Damage 100 — same shape; no scaling statement.
   All three are flat authored values. None says "scales with Boss ATK". So
   the decision's "unless its own contract explicitly defines scaling" clause
   has no current instance, and Rage currently reaches the basic attack only.

5. It is consistent with the Contradiction-A decision (D-1). That decision
   established the flow BossState.ATK → Rage → EffectiveBossATK → Damage
   Pipeline Step 1. D-1a fixes WHICH pipeline invocations that flow feeds:
   those whose Step-1 Attack input is ATK-derived. The two are one coherent
   rule.
```

**⚠ Consequence REPORTED, NOT AUTHORED — an interaction with Flame Burst's
existing Step-1 contract that the owning edit must state explicitly.**

```text
BOSS_RULES.md §6.3.1 item 1 gives Flame Burst "Base Damage: 150 (deals damage
through Damage Pipeline to active Pet)". §3.4 says a Boss Skill's "Step 1 Base
Damage is defined per Skill".

The Damage Pipeline's Step 1 is a SUM (COMBAT_RULES.md §3 step 1: "Base Damage
(from ATK stat, Skill/Card base value, and any ATK-Gem-generated damage pool
for this action)"), and §5.4.1 item 2 spells the shape out for the Pet:
"Step 1 = EffectiveATK + Skill/Card base value + ATK-Gem-generated damage
pool".

So for a Boss Skill the question is whether its Step 1 is:
   (i)  Base Damage = 150 ALONE (the Skill's authored value replaces the ATK
        term), or
   (ii) Base Damage = EffectiveBossATK + 150 (the ATK term AND the Skill value
        both contribute).

BOSS_RULES.md §6.3.1 says only "Base Damage: 150"; §3.4 says Step 1 "is
defined per Skill". Neither states whether the Boss's ATK term is ADDED to the
Skill's value or REPLACED by it.

The D-1a decision does NOT settle this — it settles that Rage's +20% does not
apply to the 150, which is true under BOTH readings. The underlying
ATK-term-vs-Skill-value composition for Boss Skills is a PRE-EXISTING gap in
§3.4/§6.3.1, unrelated to Rage, and it must not be resolved by inference here.

REPORTED: the owning edit for D-1a should state the Boss-Skill Step-1
composition explicitly (or confirm reading (i)/(ii)), because Rage's
"affects ATK-derived Boss attacks" rule is only fully deterministic once it is
known whether a Boss Skill's Step 1 contains an ATK term at all.
This is recorded as a REPORTED open question, NOT a blocker: D-1a is resolved
as written, and under reading (i) — the simpler reading, and the one
"Step 1 Base Damage = 150" most directly suggests — Rage reaches the basic
attack only and no ambiguity arises.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- COMBAT_RULES.md §3.4 needs NO amendment: it already distinguishes
  "Step 1 — Base Damage = Boss.ATK" (basic attack) from "Step 1 Base Damage is
  defined per Skill" (Skill). The decision makes the Rage rule explicit; the
  owning edit may add a pointer there or author it in the new Boss-side
  modifier subsection (the D-2c family's COMBAT_RULES.md §4 neighbour, or a
  §5.5-style subsection beside §5.4).
- BOSS_RULES.md §6.2's Hỏa Long row/prose should state the damage scope, and
  must NOT restate §3.4
  (.ai/workflow/documentation/documentation-change.md §2).
- BOSS_RULES.md §6.3.1 items 1–3 need NO change: none of the three Skills
  declares ATK scaling, so none is affected.
- No new event, wire member, Redis key, or database column. No ADR.
```

**Still OPEN under D-1 after this decision:**

```text
D-1b  ✅ RESOLVED — see "D-1b" below.
D-1c  ✅ RESOLVED — see "D-1c" below.
```

---

### D-1b — Duration

**Status: DECIDED (binding).** Supplied by the Product Owner. Resolves the
Rage duration model and consumption boundary.

**Recorded verbatim as supplied:**

```text
Hỏa Long Rage uses the existing TurnBased duration model.

When the Rage Passive effect is applied at Boss Response step 18a:

RemainingTurns = 3

The application occurs after the current Turn's player/boss action
resolution and does not retroactively modify damage already resolved
earlier in that Turn.

The effect is active for the next three counted Turns.

The existing TurnBased lifecycle decrements RemainingTurns at step 19a.

After step 19a of the third active Turn, RemainingTurns reaches 0 and
the Rage StatusEffect expires before the following Turn.

No new duration mechanism is introduced.
```

**Bindable statement (C-12).**

```text
- Duration model: the EXISTING TurnBased duration model (COMBAT_RULES.md §5.3
  DR1–DR5) — no new mechanism.
- Apply point: Boss Response step 18a (GAME_RULES.md §17 step 18a), with
  RemainingTurns = 3.
- Non-retroactivity: the application does NOT retroactively modify damage
  already resolved earlier in that same Turn.
- Active window: the next three counted Turns.
- Decrement: the existing step 19a pass, exactly one decrement per Turn
  (DR2).
- Expiry: after step 19a of the third active Turn, RemainingTurns reaches 0
  and the instance expires before the following Turn (DR5).
```

**Decrement schedule, derived from the decision and the existing rules.**

```text
Turn N   step 15–17  player damage resolved (NOT modified retroactively)
         step 18a    Apply(3) -> RemainingTurns = 3
         step 19a    -> RemainingTurns = 2      (first decrement)
Turn N+1 active
         step 19a    -> RemainingTurns = 1
Turn N+2 active
         step 19a    -> RemainingTurns = 0 -> expires (DR5)
Turn N+3 inactive
```

**Verification requested by the Product Owner — all four claims confirmed.**

```text
[1] "uses the existing TurnBased duration model" — CONFIRMED.
    COMBAT_RULES.md §5.3 DR1: "Duration is a per-effect-instance counter,
    initialized to the applied/refreshed duration value." DR2: "Exactly one
    decrement occurs per Turn, at GAME_RULES.md §17 step 19a — regardless of
    how many apply/refresh operations occurred earlier in that same Turn."
    DR5: "Expiration occurs when `remaining` reaches 0 at step 19a. An effect
    at `remaining = 0` is inactive from that point forward (i.e. not active
    during the following Turn)." The decision restates DR1/DR2/DR5 exactly.

[2] "the application occurs after the current Turn's action resolution" —
    CONFIRMED, and it is the documented reason §5.4.3 exists.
    GAME_RULES.md §17 orders Boss Response (step 18) BEFORE End Turn (step 19),
    and step 18a is after steps 15–17 (player damage). COMBAT_RULES.md §5.4.3
    states the identical consequence for Root: "Because the Pet's attack is
    GAME_RULES.md §17 step 15 and §5.3.1 DR6 places every documented
    Buff/Debuff application before step 19a, an instance applied at a later
    step of the same Turn cannot affect that Turn's already-resolved attack."
    The decision's non-retroactivity clause is that rule, applied to the Boss
    side. §5.3.1 (DR6) is satisfied and needs NO amendment.

[3] "the existing TurnBased lifecycle decrements at step 19a" — CONFIRMED
    (DR2, quoted above). Exactly one decrement per Turn; a re-application in
    the same Turn does not add a consumption (DR4).

[4] "No new duration mechanism is introduced" — CONFIRMED.
    GAME_STATE.md §2.3.1 item 3 assigns `RemainingTurns` to `BuffDebuff`, and
    D-1's representation decision set Type = "BuffDebuff". So the instance
    uses the Turn countdown branch of item 3's dichotomy, and item 3 is NOT
    widened. §5.3.2's scope statement ("all Turn-based Buff/Debuff Status
    Effects") covers it without amendment.
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. It is the SAME schedule shape D-2c-duration fixed for Thủy Ma, with one
   material difference in the apply point. Thủy Ma applies at Battle Start
   (before Turn 1), so its first counted Turn is Turn 1. Rage applies at step
   18a of Turn N, which is ALSO before that Turn's step 19a — so under DR6 the
   application consumes one Turn of duration at Turn N's own step 19a, and the
   three active Turns are N, N+1, N+2. Both are §5.3.1/DR6 cases; neither
   needed a new rule.

2. The "three counted Turns" count matches the RemainingTurns = 3 value.
   Apply(3) → 2 (Turn N) → 1 (Turn N+1) → 0 (Turn N+2, expires). Exactly three
   Turns of active duration, matching BOSS_RULES.md §6.2's "for 3 turns".

3. It does not touch §3.4 or §5.4.5. The duration is a Status Effect duration;
   Step 4's pinning and §5.4.5's scope boundary are untouched, consistent with
   the Contradiction-A decision (Rage reaches the Step-1 input, not Step 4).

4. It introduces no state member, no event, and no ADR. The instance is the
   existing §2.3.1 BuffDebuff in the existing BossState.StatusEffects[]
   (D-1's representation), using the existing DR1–DR5 lifecycle.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- COMBAT_RULES.md §5.3 needs NO rule change: DR1–DR6 already express this.
  The owning edit may ADD the Boss-side step-18a application case to §5.3.3's
  worked examples, beside the D-2c-duration Battle-Start case already
  registered there — both are "apply before step 19a" instances, differing
  only in the apply point.
- COMBAT_RULES.md §5.4.3 needs NO amendment: its existing statement (an
  instance applied at a later step of the same Turn cannot affect that Turn's
  already-resolved attack) already covers the non-retroactivity clause.
- §5.3.1 (DR6) needs NO amendment.
- BOSS_RULES.md §6.2's Hỏa Long row/prose should state the duration and the
  apply point, and must NOT restate §5.3
  (.ai/workflow/documentation/documentation-change.md §2).
- No new event, wire member, Redis key, or database column. No ADR.
```

**Still OPEN under D-1 after this decision:**

```text
D-1c  ✅ RESOLVED — see "D-1c" below.
```

---

### D-1c — Reapplication

**Status: DECIDED (binding).** Supplied by the Product Owner. Resolves the
repeat-trigger behavior.

**Recorded verbatim as supplied:**

```text
If Hỏa Long's Rage Passive triggers again while an existing Rage
StatusEffect is active:

- Do not create a second Rage instance.
- Do not add another +20% Rage modifier.
- Refresh the existing Rage instance to RemainingTurns = 3.
- The active Rage magnitude remains +20%.
- The source identity remains the same Hỏa Long Rage source.

Therefore Hỏa Long can trigger Rage repeatedly through its documented
match-based Passive threshold, but at most one +20% Rage instance is
active at a time.

A re-trigger refreshes duration rather than stacking magnitude.
```

**Bindable statement (C-13).**

```text
- A re-trigger while Rage is active does NOT create a second instance.
- It does NOT add another +20% (no stacking to +40%).
- It REFRESHES the existing instance to RemainingTurns = 3.
- The active magnitude remains +20%.
- The source identity remains the same Hỏa Long Rage source.
- At most ONE +20% Rage instance is active at a time.
- Re-triggering IS reachable in MVP play (Hỏa Long's threshold is "Every 5
  Player Matches"), and it refreshes duration rather than stacking magnitude.
```

**Verification requested by the Product Owner — confirmed as the existing
default, not a new model.**

```text
[1] "the existing default refresh/no-stack semantics" — CONFIRMED.
    COMBAT_RULES.md §5.2 item 2: "Stacking behavior (refresh duration vs.
    stack magnitude vs. independent instances) is defined per-effect; default
    for MVP is **refresh duration, do not stack magnitude** unless a Card/Relic
    explicitly says otherwise". Hỏa Long's Rage is a Boss Passive — neither a
    Card nor a Relic — so no carve-out applies and the default governs. The
    decision states that default; §5.2 item 2 needs NO change.

[2] "at most one instance" and "source identity remains the same" —
    CONFIRMED as GAME_STATE.md §2.3.1 item 6: "There is never more than one
    instance per effect identity per entity. Applying an effect that is already
    active refreshes that existing instance rather than appending a second one
    (`COMBAT_RULES.md` §5.2 item 2 — 'refresh duration, do not stack
    magnitude'), so the array holds at most one element per `Id`." The
    decision's "same source identity" is what makes item 6 apply: the refresh
    targets the SAME `Id`, so no second element is appended. Item 6 is NOT
    relaxed.

[3] The refresh MECHANISM is DR3 — CONFIRMED. §5.3 DR3: "Apply and Refresh use
    the SAME mechanism: `remaining = duration` (an initial Apply is not
    semantically different from a Refresh; Refresh simply re-executes the same
    'set remaining' operation on an already-active effect instance)." The
    decision's "refresh ... to RemainingTurns = 3" is exactly DR3's operation.

[4] Same-Turn re-trigger is DR4 — CONFIRMED. §5.3 DR4: "Same-Turn
    reapplication (Apply/Refresh occurring again within the Turn in which the
    effect is already active) resets `remaining` to the new duration value and
    does NOT trigger an additional consumption in that Turn. Only step 19a
    consumes."

[5] "No new stacking model" — CONFIRMED. No `StatusEffect` member, `Id`, or
    `Type` value is added; no second instance representation exists; §2.3.1
    items 3 and 6 are unchanged.
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. It is IDENTICAL in shape to D-2d's Thủy Ma reapplication decision — both
   are §5.2 item 2's default plus §2.3.1 item 6's one-instance rule plus
   §5.3 DR3/DR4's mechanism. Neither introduced new semantics.

2. UNLIKE Thủy Ma, this rule is REACHABLE in MVP play. This is the key
   asymmetry and it is why the rule must be implemented, not merely recorded
   as forward-compatibility. Hỏa Long's trigger is "Every 5 Player Matches"
   (BOSS_RULES.md §6.2) with PassiveThreshold = 5 (§6.2's note), and
   PASSIVE_RULES.md §2 item 3 makes the Passive trigger "at most once per
   Cascade" once progress ≥ threshold. Because a Rage instance persists for
   three Turns while matches continue to accumulate, a second trigger CAN
   occur while Rage is active. (Contrast D-2d's note: Thủy Ma's one-time
   Battle Start trigger makes its refresh rule unreachable in MVP.)
   Consequence: an MVP test CAN legitimately exercise this refresh path
   through the real Passive trigger — no direct-application shortcut needed.

3. The refresh EXTENDS the window rather than resetting the count unfairly.
   A refresh at Turn M sets RemainingTurns = 3, and DR4 guarantees no extra
   consumption in Turn M, so the instance is active for Turns M, M+1, M+2 —
   the §5.3.3 "duration = 2, refreshed in Turn N+1" example pattern at
   duration 3.

4. It does not touch §3.4 or §5.4.5, and adds no event, member, or ADR.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- COMBAT_RULES.md §5.2 item 2 needs NO change; §5.3 DR3/DR4 need NO change;
  GAME_STATE.md §2.3.1 item 6 needs NO change.
- The owning edit should state Rage's reapplication behavior where the effect
  is defined (BOSS_RULES.md §6.2's Hỏa Long row/prose) and POINT AT §5.2
  item 2 rather than restating it
  (.ai/workflow/documentation/documentation-change.md §2).
- An MVP test may exercise the refresh through the real match-based Passive
  trigger (unlike Thủy Ma's D-2d case).
- No new event, wire member, Redis key, or database column. No ADR.
```

**Still OPEN under D-1 after this decision: NOTHING.** The Rage family is
closed — Contradiction A (consumption point), D-1 (representation), D-1a
(damage scope), D-1b (duration), D-1c (reapplication).

---

### D-4 (D-4a – D-4g) — Cross-cutting event, wire, storage, and persistence consequences

**Status: DECIDED (binding).** Supplied by the Product Owner. This resolves
TASK-123's last open decision ID.

**Recorded verbatim as supplied:**

```text
D-4a — Battle Events

Hỏa Long Rage, Thủy Ma healing reduction, and Mộc Yêu regeneration
introduce no new Battle Event.

Do not add:

- BossPassiveApplied
- BossPassiveExpired
- HealingReduced
- BossRageApplied
- BossRegenerated
- any equivalent Boss-Passive-specific event

Existing Battle Events remain unchanged.

PassiveTriggered/PassiveCharged remain governed by their existing
Passive contracts and must not be repurposed as effect-application
events for Thủy Ma or Mộc Yêu.

D-4b — SignalR

No new SignalR method, event, or BattleStateUpdated payload member is
introduced for these Boss Passive effects.

In particular, do not add bossState to the current BattleStateUpdated
projection as part of TASK-123.

Boss Passive effects remain authoritative server-side state.

If future product requirements require client-visible Boss HP or Boss
StatusEffects, that requires a separate SignalR protocol task.

D-4c — Redis

No new Redis key is introduced.

Existing authoritative BattleState persistence remains the sole
persistence mechanism:

battle:{battleId}:state

BossState.StatusEffects[] changes for Rage/Thủy Ma are persisted as part
of the existing BattleState serialization.

Mộc Yêu's direct Boss HP regeneration is persisted through the existing
BossState.HP field.

Existing TTL and Sequence/CAS semantics remain unchanged.

D-4d — Database

No PostgreSQL schema change is introduced.

These runtime Boss Passive effects are BattleState runtime concerns and
must not create:

- BossPassive tables
- BossStatusEffect tables
- Boss HP persistence columns
- new gameplay persistence entities

D-4e — Runtime state ownership

All three effects remain server-authoritative.

Hỏa Long Rage:
BossState.StatusEffects[]

Thủy Ma healing reduction:
BossState.StatusEffects[]

Mộc Yêu regeneration:
direct authoritative BossState.HP mutation

The client does not calculate, predict, or authoritatively apply any of
these effects.

D-4f — Client observability limitation

The current SignalR projection does not expose BossState.

Therefore these Boss Passive state changes are not directly client-visible
through BattleStateUpdated in the current MVP protocol.

This is intentional for TASK-123 and is not a defect to solve here.

A future requirement for client-visible Boss HP/StatusEffects must be
handled by a separate protocol decision/implementation task.

D-4g — D-3 Boss regeneration and Heal Resolution boundary

D-3 Mộc Yêu regeneration uses its own direct Boss HP update and does not
implicitly widen D-2c's Pet-scoped Heal Resolution contract.

Whether future Boss-side healing should use the shared Heal Resolution
mechanism is a separate contract question.

TASK-123 does not modify D-2c's Pet-only scope.
```

**Bindable statement (C-14).**

```text
D-4a  NO new Battle Event. Specifically NOT added: BossPassiveApplied,
      BossPassiveExpired, HealingReduced, BossRageApplied, BossRegenerated,
      or any equivalent. GAME_RULES.md §16 is unchanged. PassiveTriggered/
      PassiveCharged keep their existing Passive contracts and are NOT
      repurposed as effect-application events for Thủy Ma or Mộc Yêu.
D-4b  NO new SignalR method, event, or BattleStateUpdated payload member.
      In particular, `bossState` is NOT added to the current
      BattleStateUpdated projection by TASK-123.
D-4c  NO new Redis key. `battle:{battleId}:state` remains the sole
      authoritative persistence. StatusEffects[] changes ride the existing
      BattleState serialization; Mộc Yêu's regeneration rides the existing
      BossState.HP field. TTL and Sequence/CAS semantics unchanged.
D-4d  NO PostgreSQL schema change. No BossPassive table, no BossStatusEffect
      table, no Boss HP persistence column, no new gameplay persistence
      entity.
D-4e  ALL THREE effects remain server-authoritative, at the homes D-1/D-2b/
      D-3e fixed. The client does not calculate, predict, or authoritatively
      apply any of them.
D-4f  CONTRACT LIMITATION, not an implementation requirement: the current
      projection does not expose BossState, so these changes are not directly
      client-visible through BattleStateUpdated in the current MVP protocol.
      This is INTENTIONAL for TASK-123 and is NOT a defect to solve here.
      Client-visible Boss HP/StatusEffects requires a SEPARATE protocol task.
D-4g  D-3 uses its own direct Boss HP update and does NOT implicitly widen
      D-2c's Pet-scoped Heal Resolution contract. Whether future Boss-side
      healing should use the shared mechanism is a SEPARATE contract
      question. D-2c's Pet-only scope is NOT modified.
```

**Verification requested by the Product Owner — all six consequences
confirmed against the authoritative contracts.**

```text
[1] "No new Battle Event" — CONFIRMED.
    GAME_RULES.md §16's canonical list (lines 282–293) is exactly:
      BattleStarted, TurnStarted, TurnEnded / SwapStarted, SwapResolved /
      MatchCreated, MatchResolved, CascadeCreated, ComboChanged, GemMatched /
      PowerChanged / PassiveCharged, PassiveTriggered / RelicTriggered /
      CardCast, PetSkillCast / DamageCalculated, DamageDealt, DamageTaken /
      BossSkillCast / BattleWon, BattleLost
    It contains NO BossPassiveApplied, BossPassiveExpired, HealingReduced,
    BossRageApplied, or BossRegenerated. §16 states the list is "canonical —
    do not duplicate elsewhere", so the decision preserves it rather than
    extending it.
    PassiveTriggered's payload is owned by GAME_EVENTS.md §2 and already
    carries "effect summary (not populated yet)" — D-4a keeps it unpopulated
    for these effects rather than repurposing the event, and GAME_EVENTS.md §2
    item 3 already states a missing effect summary means "not yet reported",
    never "no effect occurred". Consistent.

[2] "No new SignalR method/event/member; do not add bossState" — CONFIRMED.
    SIGNALR_PROTOCOL.md §4's payload (line 1197) is
      BattleStateUpdated(battleId, turn, sequence, board, rngSeed, rngState,
                         playerState, petState)
    — no bossState, and §4 item 4 states "No other field may be added to this
    record: additional state is introduced by extending GAME_STATE.md §2.0,
    not by the wire shape." Line 1218 adds: "no `Status`/lifecycle value is
    carried anywhere in the protocol (§8.3)."
    §4 item 14 is the direct precedent: PetState.NextAttackCritModifiers[]
    was REFUSED a wire member for the same kind of reason. D-4b takes that
    position for all three Boss effects. The protocol is NOT widened.

[3] "No new Redis key; battle:{battleId}:state remains sole persistence" —
    CONFIRMED. REDIS_STATE.md line 41 fixes the key as
    `battle:{battleId}:state -> serialized BattleState (GAME_STATE.md §2)`,
    with `battle:{battleId}:lock` as an optional short-TTL mutex. §7 item 9's
    round-trip obligation is internal to that record. REDIS_STATE.md's own
    §7 item 13 precedent (for NextAttackCritModifiers[]) states the addition
    "round-trips under the existing obligation", "is written in the same
    single post-resolution write-back under the unchanged `Sequence`
    compare-and-set, and ... has no wire consequence because adding state is
    not adding a wire member."
    GAME_STATE.md §2.3.1 already states StatusEffects[] "are part of
    BattleState and therefore serialize with it under the existing round-trip
    obligation (REDIS_STATE.md §7 item 9)". BossState.HP is an existing
    §2.4 member. So both carriers already persist — no new key. §7 item 6:
    "`Sequence` is the only concurrency token" — unchanged.

[4] "No PostgreSQL schema change" — CONFIRMED.
    DATABASE.md §1's entity list is Player / Pet / PetDefinition /
    CardDefinition / PlayerUnlockedCard / RelicDefinition / Relic /
    BossDefinition / BattleResult. Searched for BossStatusEffect,
    BossPassive-table, BossState-table, and BossHp: NO MATCHES. The only Boss
    entity is `BossDefinition` — "static content" (line 349), a definition
    table, not runtime state. DATABASE.md §1's own note already forbids
    extending the persisted PassiveDefinition ("the effect declaration must
    not become a fifth stored member"). Consistent — and see the reported
    stale-pointer gap below.

[5] "No new BattleState member" — CONFIRMED.
    D-1 placed Rage in the EXISTING BossState.StatusEffects[] (GAME_STATE.md
    §2.4, implemented TASK-095/TASK-096); D-2b placed the Thủy Ma instance in
    the same collection; D-3e uses the EXISTING BossState.HP (§2.4). No
    member, `Id`, `Type`, `TargetStat`, or `StatusEffect` field is added.
    GAME_STATE.md §2.3.1's member set is unchanged.

[6] "No client-authoritative state" — CONFIRMED.
    GAME_RULES.md §18 and ADR-001 make the battle server authoritative;
    BOSS_RULES.md §8 states "All Boss HP, State, Passive progress and Skill
    resolution are server authoritative ... The client never determines when
    a Boss Skill fires or what it does — it only renders the resulting
    events." D-4e restates this for all three effects, and D-4f makes the
    client's non-observability explicit rather than implying a client-side
    computation. No client calculation, prediction, or authoritative
    application is introduced.
```

**Reported consequence — this resolves the D-2e / D-3e observability question
explicitly, in the direction those sections flagged as reading (a).**

```text
D-2e and D-3e both said their effect "is observable through the existing
authoritative BattleStateUpdated synchronization", and both sections REPORTED
that the payload does not actually carry BossState — leaving two readings:
   (a) observability via the sync CHANNEL, with no member added; or
   (b) client visibility intended, requiring a §4 extension.

D-4b and D-4f settle this EXPLICITLY in favour of (a), and go further by
naming (b) as a SEPARATE future task. So reading (a) is now a recorded
decision rather than an inference, and the limitation is intentional rather
than an oversight.

NO CONTRADICTION with D-2e/D-3e: neither claimed a wire member exists. D-4b/f
confirm the limitation those sections reported.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- GAME_RULES.md §16 needs NO change (closed list preserved).
- GAME_EVENTS.md §2 needs NO change (no payload member added; PassiveTriggered
  keeps its existing unpopulated "effect summary" convention).
- SIGNALR_PROTOCOL.md needs NO change — §4 item 4 governs and D-4b explicitly
  refuses the bossState member.
- REDIS_STATE.md needs NO change (existing key, existing write-back).
- DATABASE.md needs NO schema change (see the stale-pointer gap below).
- GAME_STATE.md needs NO member change.
- No ADR.
```

---

### D-2a (Contradiction B) — Thủy Ma's activation model

**Status: DECIDED (binding).** Supplied by the Product Owner. This resolves
Contradiction B. It does **not** resolve D-2b, D-2c, D-2d, or D-2e, and it does
not resolve D-1-representation / D-1a / D-1b / D-1c, D-3, or D-4.

**Recorded verbatim as supplied:**

```text
The "for 3 turns" wording is authoritative.

Thủy Ma's −50% healing effect is a triggered temporary effect.
It is NOT an always-active modifier from battle start.

When the Boss Passive trigger resolves, the effect becomes active
and follows the existing TurnBased 3-turn lifecycle.

The "Passive (always active)" wording in BOSS_RULES.md §6.2 is
treated as stale/conflicting wording and must not be interpreted as
permanent/no-expiry behavior.
```

**Bindable statement (C-2).**

```text
- The authoritative reading of BOSS_RULES.md §6.2's Thủy Ma row is the
  "reduced by 50% for 3 turns" column, not the "Passive (always active)"
  trigger column.
- The effect is a TRIGGERED, TEMPORARY effect that becomes active when the
  Boss Passive trigger resolves, and it follows the existing TurnBased
  3-turn lifecycle (COMBAT_RULES.md §5.3 DR1–DR5).
- It is NOT an always-active modifier, NOT permanent, and has NO
  no-expiry behavior.
- The §6.2 phrase "Passive (always active)" is recorded as STALE/CONFLICTING
  wording. It must not be read as establishing permanence.
```

**Consequence reported, NOT authored (a NEW gap this decision exposes).**

The decision makes the duration authoritative, but `BOSS_RULES.md` §6.2's own
prose (lines 194–200) currently states Thủy Ma:

```text
"is never charged via `PassiveTracker.Charge` on Player Matches, and emits no
`PassiveCharged`/`PassiveTriggered` from match progress."
```

**Consequence reported, NOT authored — SINCE RESOLVED by D-2b.**

An earlier reading of the evidence recorded that retiring the "always active"
wording would leave the trigger undefined, and assigned that question to D-2b:

```text
D-2b must now also answer: WHAT triggers Thủy Ma's Passive, given that
BOSS_RULES.md §6.2 denies it match charging and denies it emitting
PassiveCharged/PassiveTriggered from match progress?
```

**✅ RESOLVED by the D-2b Product Owner decision** (see D-2b below): the trigger
is **Battle Start**, an existing trigger type in `PASSIVE_RULES.md` §3. D-2b
also selected the representation (the existing TurnBased Buff/Debuff
StatusEffect model).

**A second documented inconsistency, reported for the owning edit.** The
retired phrase cites a trigger type that `PASSIVE_RULES.md` §3 does not define:

```text
BOSS_RULES.md §6.2 line 195 calls "Passive (always active)" "an alternate
trigger (PASSIVE_RULES.md §3)". But PASSIVE_RULES.md §3's closed list of
alternate triggers is:

    Combo | HP Threshold | Damage Dealt | Battle Start | Card Cast

"always active" is not among them, and §3 states the list is of "supported
trigger types". So the cited authority does not contain the cited trigger.
This decision retires that wording, which resolves the citation defect as a
side effect — but the correction of BOSS_RULES.md §6.2 is a SEPARATE task's
act, not this one's.
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. The 3-turn lifecycle is already fully specified and needs no new model.
   COMBAT_RULES.md §5.3 DR1–DR5 own duration consumption: DR1 the
   per-instance counter, DR2 exactly one decrement per Turn at GAME_RULES.md
   §17 step 19a, DR3 Apply/Refresh share one mechanism, DR4 same-Turn
   reapplication resets without extra consumption, DR5 expiry at 0. §5.3.1
   (DR6) places every documented application site before step 19a. Boss
   Response 18a is before step 19a, so an effect applied at 18a consumes its
   first Turn at that same Turn's step 19a — the §5.3.3 worked example's shape
   applies directly.

2. "TurnBased" is §5.3.2's term and its scope is the open question D-2c
   inherits. §5.3.2 states the rule "applies to all Turn-based Buff/Debuff
   Status Effects", and §2.3.1 item 3 types the instance. Whether Thủy Ma's
   healing modifier is represented as a Turn-based Buff/Debuff instance is
   D-2b's representation question, not settled by this decision — this
   decision fixes the ACTIVATION MODEL and the LIFETIME, not the carrier.

3. The decision is consistent with §5.2 item 1 ("a duration (in Turns) or a
   trigger-based expiry") and with §5.2 item 2's MVP default (refresh
   duration, do not stack magnitude), which D-2d inherits.
```

**Still OPEN under D-2** (as at the D-2a decision; D-2b has since been resolved
— see D-2b below):

```text
D-2b  ✅ RESOLVED — see "D-2b" below. (Earlier statement, retained for the
      record: application site and reach — where is −50% applied? PLUS what
      triggers the effect. D-2b's decision answers the TRIGGER and the
      REPRESENTATION; the healing application SITE is now tracked separately
      as D-2b-site below.)
D-2c  Duration start, which Turn decrements it, when it expires, and whether
      the activation Turn is included.
D-2d  Reapplication on another trigger — refresh / replace / stack.
D-2e  Events — whether application/removal requires an event (none is
      currently defined; GAME_RULES.md §16 is the closed list).
```

---

### D-2b — Thủy Ma's trigger and representation

**Status: DECIDED (binding).** Supplied by the Product Owner. This resolves the
undefined-trigger gap that D-2a exposed, and it selects the representation.

**Recorded verbatim as supplied:**

```text
Thủy Ma's −50% healing effect is triggered at Battle Start.

The effect is represented using the existing TurnBased Buff/Debuff
StatusEffect model, with a 3-turn lifetime as resolved by D-2a.

It does NOT use PassiveTracker.Charge.
It does NOT emit PassiveCharged or PassiveTriggered from match progress.

Battle Start is the trigger because it is an existing documented
alternate trigger in PASSIVE_RULES.md §3 and requires no new trigger
mechanism.
```

**Bindable statement (C-3).**

```text
- TRIGGER: Battle Start — a one-time trigger, evaluated once when the battle
  session is created, before the first Turn.
- REPRESENTATION: the existing TurnBased Buff/Debuff StatusEffect model
  (GAME_STATE.md §2.3.1; Type = "BuffDebuff", Turn-countdown duration model).
- LIFETIME: 3 turns, per the D-2a decision, consumed by the unchanged
  COMBAT_RULES.md §5.3 DR1–DR5 lifecycle.
- It does NOT use PassiveTracker.Charge.
- It does NOT emit PassiveCharged or PassiveTriggered from match progress.
- No new trigger mechanism is introduced.
```

**The justification is verified, not asserted.** The decision cites
`PASSIVE_RULES.md` §3 as the authority for Battle Start. Inspected:

```text
PASSIVE_RULES.md §3 "Alternate Triggers" (line 74-84) lists:
    Combo            (e.g. "on Combo ≥ N")
    HP Threshold     (e.g. "when HP < 30%")
    Damage Dealt     (cumulative or per-instance threshold)
    Battle Start     (one-time trigger)          ← line 82
    Card Cast        (on casting a specific Card or Card category)
and states these "are exceptions and must be explicitly declared per-Pet".

CONFIRMED: "Battle Start" IS in §3's closed list, described as a
"one-time trigger". GAME_RULES.md §10.5 independently lists it too:
"Alternate supported triggers exist (Combo, HP threshold, Damage dealt,
Battle start, Card cast)". The decision therefore uses an EXISTING documented
trigger type and introduces no new mechanism — as it states.
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. The representation requires NO new battle-state concept.
   GAME_STATE.md §2.3.1 types the StatusEffect instance (Id / Type / Source /
   Magnitude / TargetStat? / RemainingTurns? / ExpiryCondition?) and §2.3.1
   item 3 fixes the Turn countdown for DoT/BuffDebuff/State. §2.4/§2.4.1
   establish BossState.StatusEffects[] with "identical element schema and
   identical lifecycle" to PetState's, and §2.3.1 item 7 fixes the
   TargetStat-iff-BuffDebuff pairing. D-2a's TurnBased 3-turn lifetime rides
   §5.3 DR1–DR5 unchanged. Therefore no AGENTS.md §18 battle-state model
   change and NO ADR is triggered by this decision.

2. Battle Start is BEFORE GAME_RULES.md §17's Turn loop, so §5.3.1 (DR6) is
   satisfied without amendment. DR6 states that "all currently documented
   Buff/Debuff application sites occur before the consumption point" (step
   19a). A Battle Start application precedes Turn 1 entirely, so the first
   step-19a decrement occurs at the end of Turn 1 — the same Turn-1-apply
   shape §5.3.3's first worked example uses (Apply(3) at Turn N →
   remaining 2 after that Turn's step 19a). D-2c owns the exact boundary.

3. Consistency with BOSS_RULES.md §6.2's own prose. That prose states Thủy Ma
   "is never charged via PassiveTracker.Charge on Player Matches, and emits no
   PassiveCharged/PassiveTriggered from match progress". This decision
   PRESERVES both statements exactly, and supplies the missing trigger through
   a type that is not match-based. The prior citation defect (the retired
   "always active" phrase citing §3) is removed by the D-2a decision; this
   decision replaces it with a type §3 actually defines.

4. PassiveCharged/PassiveTriggered emission. §6.2's "emits no ... from match
   progress" is scoped to MATCH PROGRESS. A Battle Start trigger is not match
   progress. Whether the Battle Start application emits PassiveTriggered is a
   reporting question this decision does not settle (recorded under D-2e),
   and §6.2's prohibition is not violated either way, because the prohibition
   is scoped to match progress.

5. Interaction with BattleStarted (GAME_EVENTS.md §2). BattleStarted's trigger
   is "Battle session created, before the first Turn" — the same point as this
   decision's Battle Start. This decision does NOT add, rename, or re-scope
   any event; whether the effect application is observable remains D-2e's.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- COMBAT_RULES.md §5.3.1 (DR6) needs NO amendment: a Battle Start site is
  before step 19a by definition.
- BOSS_RULES.md §6.2's Thủy Ma row must be corrected: the trigger column
  becomes Battle Start, replacing the retired "Passive (always active)"
  wording, and the prose at lines 194–200 must be reconciled so it no longer
  describes an undefined "always-on effect application". This is the SAME
  owning edit the D-2a decision requires — one edit, not two.
- GAME_STATE.md §2.4.2 needs NO amendment for this decision: the
  representation is the existing StatusEffects[] collection, and §2.4.1
  already documents that collection's existence for exactly this kind of
  content ("defined because Stun (§2.4.5) and future content are tracked
  through it").
- No new Battle Event, SignalR member, Redis key, or database column.
- PassiveTracker is untouched: no charge, no threshold, no reset behavior is
  added for Thủy Ma (§4's Reset Behavior rule continues not to apply to a
  non-charging trigger).
```

**Still OPEN after D-2b** (the healing APPLICATION SITE was not settled by
D-2b — it fixed the trigger and the carrier, not where −50% is applied; it has
since been resolved by D-2b-site below):

```text
D-2b-site  ✅ RESOLVED — see "D-2b-site" below. (Earlier statement, retained
           for the record: Where is the −50% applied, and what does it reach?
           Card Heal (Tidal Barrier, CARD_RULES.md §4.1), HP-Gem healing
           (GAME_RULES.md §12), both, or a shared step? And relative to
           COMBAT_RULES.md §4 item 1's overheal clamp?)
D-2c       Duration start, which Turn decrements it, when it expires, and
           whether the activation Turn is included. (D-2b places the apply
           point at Battle Start, before Turn 1; D-2c owns the boundary.)
D-2d       Reapplication on another trigger — refresh / replace / stack.
           (NOTE: with a one-time Battle Start trigger, the ordinary
           same-Turn and repeat-trigger cases may be unreachable for MVP
           content; this must be confirmed by the decision, not assumed.)
D-2e       Events — whether application/removal requires an event, and
           whether the Battle Start application emits PassiveTriggered.
           (GAME_RULES.md §16 is the closed list.)
```

---

### D-2b-site — Thủy Ma's healing-reduction application site

**Status: DECIDED (binding).** Supplied by the Product Owner. This resolves
where the −50% modifier is applied, what it reaches, and its position relative
to the overheal clamp.

**Recorded verbatim as supplied:**

```text
The −50% Thủy Ma healing modifier applies at the shared Heal
resolution point, before the existing overheal clamp.

Flow:

Raw Heal
→ Thủy Ma −50% modifier
→ resulting Heal amount
→ existing overheal clamp
→ HP update

The modifier applies to healing received by the affected Pet,
regardless of whether the healing originates from Card Heal,
HP-gem healing, or another existing healing source that uses the
shared Heal resolution.

It does not modify MaxHP and does not affect Shield.
```

**Bindable statement (C-4).**

```text
- APPLICATION POINT: the shared Heal resolution point, BEFORE the existing
  overheal clamp (COMBAT_RULES.md §4 item 1).
- ORDER: Raw Heal → −50% modifier → resulting Heal amount → overheal clamp →
  HP update. (The clamp is unchanged and still runs last.)
- REACH: healing received by the affected Pet, from any existing source that
  uses the shared Heal resolution — explicitly including Card Heal and
  HP-Gem healing.
- NOT modified: MaxHP. NOT affected: Shield.
```

**⚠️ CONSEQUENCE REPORTED, NOT AUTHORED — the decision presupposes a shared
Heal resolution point that no document currently defines.**

The decision is expressed in terms of "the shared Heal resolution point".
Direct inspection of `docs/` shows that **this point does not exist yet**, and
that healing is today documented as **two independent paths with no shared
step**:

```text
PATH 1 — Card Heal (a player action)
  CARD_RULES.md §4.1: Tidal Barrier "Heal the active Pet for 20% of its
    Max HP".
  COMBAT_RULES.md §4 item 6: "Heal and Shield amounts are NOT subject to the
    Damage Pipeline (§3) — they are not damage — but they ARE subject to
    their own explicit modifiers (e.g. a Relic that increases Heal Card
    effectiveness)."
  → §4 item 6 names the RELIC case only. It names no mechanism, no site, and
    no ordering.

PATH 2 — HP-Gem heal pool (generated by a Match)
  COMBAT_RULES.md §2: "HP Gem   +20 Heal pool" (× match-tier multiplier).
  GAME_RULES.md §17 step 12 "Generate Resources" produces it; step 14
    "Resolve Player Effects" applies it.
  → COMBAT_RULES.md §4 says NOTHING about the heal pool. The string
    "Heal pool" occurs EXACTLY ONCE in all of docs/ (COMBAT_RULES.md §2's
    generation table) and is never defined, consumed, or referenced again.

IMPLEMENTATION EVIDENCE (established fact, not a design source):
  Card Heal      → CardCastExecutor.cs:95-103 —
                   newHp = Math.Min(newHp + healAmount, MaxHP)
  HP-Gem heal    → ResourceGenerator.ApplyHeal(...) — a SEPARATE function,
                   applied at a separate site (ResourceGenerator.cs:142
                   documents it as "§17 step 14"; SwapExecution.cs:490 calls
                   it)
  → Two different functions, two different call sites, no shared routine.

CONSEQUENCE: the decision's "shared Heal resolution point" is a
  TO-BE-CREATED abstraction, not an existing one. Authoring it is a real
  design act: it must define what "the shared Heal resolution" is, which
  sources route through it, and where it sits in COMBAT_RULES.md §4. This is
  REPORTED as a required consequence of the owning edit — it is NOT retro-
  fitted onto existing documents by this task, and §4 item 6's Relic example
  does not already supply it.
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. The ORDER is consistent with §4 item 1 and needs no amendment to that
   item. §4 item 1 states "Heal effects restore HP up to Max HP; overheal is
   discarded unless a Relic explicitly grants overheal/temp-HP." The decision
   places the modifier BEFORE that clamp, so the clamp's own wording
   ("restore HP up to Max HP") is unchanged and still correct. Nothing in
   §4 item 1 is re-ordered.

2. "regardless of ... source" is CONSISTENT WITH but NOT YET SUPPORTED BY
   docs. The decision's reach (Card Heal + HP-Gem healing + any source using
   the shared resolution) is the reason a shared point is needed. Until that
   point is authored, Path 2's application site is undefined, exactly as
   recorded above.

3. "does not modify MaxHP and does not affect Shield" is consistent with
   §4's structure. §4 item 3 owns Shield's refresh-not-stack rule and item 5
   owns absorption; a Heal modifier touching neither leaves both untouched.
   §4 item 6 already separates Heal from Shield ("Heal and Shield amounts are
   NOT subject to the Damage Pipeline").

4. Relationship to ELEMENT_RULES.md. ELEMENT_RULES.md line 130 states the
   Element Modifier "does not apply to non-damage effects (Heal, Shield
   amount, Power gain, ...)". The decision does not introduce an elemental
   interaction for healing, so that boundary is preserved.

5. Relationship to §4 item 6's Relic carve-out. §4 item 6 anticipates a
   source-specific Heal modifier ("a Relic that increases Heal Card
   effectiveness") and §4 item 1 preserves a Relic overheal carve-out.
   Thủy Ma's modifier is a Boss-side reduction, not a Relic, so it does not
   consume either carve-out — but it is the SECOND documented Heal modifier,
   which is itself evidence that §4 needs the mechanism its item 6 assumes.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- COMBAT_RULES.md §4 must gain the shared Heal resolution point as an
  AUTHORED mechanism (its item 6 only anticipates one). The decision's
  ordering — modifier before the item-1 clamp — becomes that mechanism's
  content.
- COMBAT_RULES.md §4 item 1's clamp is NOT re-ordered and needs no rewording.
- The HP-Gem "Heal pool" (COMBAT_RULES.md §2) must be routed through that
  shared resolution; today its application site is documented only
  obliquely (GAME_RULES.md §17 step 14) and §4 never mentions it.
- No new Battle Event: GAME_EVENTS.md §2 has no heal event, and
  GAME_RULES.md §16 is a closed list (this is D-2e's separate question).
- No change to MaxHP handling and no change to Shield's §4 rules.
- No ADR is required by THIS decision: it introduces no battle-state concept —
  it places an arithmetic modifier on an existing Heal computation and adds
  no state member.
```

**Still OPEN after D-2b-site:**

```text
D-2c  ✅ RESOLVED — see "D-2c" below. ⚠ RE-SCOPED BY THE PRODUCT OWNER: the
      supplied decision addresses the canonical Heal Resolution MECHANISM,
      not D-2c's originally-tracked subject (the Thủy Ma instance's duration
      start/decrement/expiry boundary). The duration boundary is therefore
      NOT resolved and is re-tracked as D-2c-duration below.
      Original tracked subject, retained for the record: "Duration start,
      which Turn decrements it, when it expires, and whether the activation
      Turn is included (Turn 1 is the activation Turn, since D-2b applies at
      Battle Start before Turn 1)."
D-2c-duration  NEW — the Thủy Ma instance's duration boundary (when the
      3-Turn countdown starts, which Turn's step 19a performs the first
      decrement, and whether the activation Turn is included). This is the
      subject D-2c originally carried and it remains UNANSWERED.
D-2d  Reapplication on another trigger — refresh / replace / stack, and
      whether the one-time Battle Start trigger makes the ordinary
      repeat cases unreachable for MVP content.
D-2e  Events — whether application/removal requires an event, and whether the
      Battle Start application emits PassiveTriggered.
```

---

### D-2c — The canonical shared Heal Resolution mechanism

**Status: DECIDED (binding).** Supplied by the Product Owner. This resolves the
foundational gap D-2b-site exposed: a shared Heal resolution point that `docs/`
did not previously define.

**⚠ Scope note, recorded before the decision text.** The decision supplied for
"D-2c" addresses the **canonical Heal Resolution mechanism**, not D-2c's
originally-tracked subject in this task file (the Thủy Ma instance's duration
boundary). The two are different questions. This record:

```text
- records the supplied decision at its true subject (the Heal Resolution
  mechanism), because that is what it actually decides; and
- does NOT treat the duration boundary as resolved — it is re-tracked as
  D-2c-duration and remains UNANSWERED.
Recording the mechanism decision under the "duration" heading, or treating the
duration question as answered by it, would both be miscarriages of the record.
```

**UPDATE:** the re-tracked D-2c-duration question was subsequently answered by
its own binding Product Owner decision — see "D-2c-duration" below. The
separation above was what made that possible without either question silently
absorbing the other.

**Recorded verbatim as supplied:**

```text
Author a canonical shared Heal Resolution step in COMBAT_RULES.md §4.

All healing sources that restore Pet HP, including Card Heal and
HP-Gem healing, must pass through this resolution point before the
existing overheal clamp.

The canonical order is:

Raw Heal
→ applicable Heal modifiers
→ final Heal amount
→ existing MaxHP / overheal clamp
→ HP update

Thủy Ma's −50% modifier is one applicable Heal modifier.

The shared Heal Resolution step is a combat-rule mechanism only.
It is not a new BattleState member, event, SignalR payload, Redis key,
or database field.

Do not create a speculative generic abstraction beyond this documented
combat-rule boundary.
```

**Bindable statement (C-5).**

```text
- A canonical shared Heal Resolution step is AUTHORED in COMBAT_RULES.md §4.
- ALL healing sources that restore Pet HP pass through it — explicitly
  including Card Heal and HP-Gem healing.
- CANONICAL ORDER:
      Raw Heal
    → applicable Heal modifiers
    → final Heal amount
    → existing MaxHP / overheal clamp
    → HP update
- Thủy Ma's −50% modifier is ONE applicable Heal modifier (it is not a
  special-cased site).
- BOUNDARY: combat-rule mechanism ONLY. It is NOT a BattleState member,
  event, SignalR payload, Redis key, or database field.
- No speculative generic abstraction beyond that boundary.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- COMBAT_RULES.md §4 must gain the shared Heal Resolution step as AUTHORED
  content, and must route the §2 HP-Gem "Heal pool" through it. Today §4
  never mentions the heal pool, and the string "Heal pool" occurs exactly once
  in all of docs/ (COMBAT_RULES.md §2's generation table).
- §4 item 1's clamp is NOT re-ordered or reworded. The decision keeps it as
  the same step, now named inside the canonical order.
- §4's declared ownership line currently says the section is "the canonical
  owner of the Shield rule". The owning edit should widen that statement to
  cover Heal resolution too, since the decision places the Heal mechanism
  here — this is a §4 self-description correction, not a new rule.
- GAME_RULES.md §17 step 14 "Resolve Player Effects" remains the resolution
  step where healing is APPLIED; this decision defines the inner computation
  that step uses. §17's step order is not changed.
- NO new BattleState member (GAME_STATE.md unchanged), NO new event
  (GAME_RULES.md §16 unchanged; GAME_EVENTS.md §2 unchanged), NO SignalR
  member (SIGNALR_PROTOCOL.md §4 unchanged), NO Redis key
  (REDIS_STATE.md unchanged), NO database field (DATABASE.md unchanged).
- NO ADR: the decision introduces no battle-state concept, no persistence
  change, and no architecture change. It is a combat-rule mechanism, exactly
  as it states.
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. It supplies the mechanism §4 item 6 already ANTICIPATES but never defines.
   §4 item 6 states Heal amounts "ARE subject to their own explicit modifiers
   (e.g. a Relic that increases Heal Card effectiveness)" and names no
   mechanism, no site, and no ordering. The decision authors exactly that,
   and places it before the item-1 clamp — consistent with item 1's own
   wording ("restore HP up to Max HP"), which is left intact.

2. It generalizes a rule that was previously Card-only in practice. §4 item 6's
   example is a Card-effectiveness Relic; the decision routes ALL Pet-HP
   healing through one step, which is what D-2b-site's "regardless of source"
   reach requires. The two decisions are co-dependent and mutually consistent.

3. It respects the "no speculative abstraction" standard (AGENTS.md §9). The
   decision explicitly bounds the mechanism to a documented combat-rule step
   and forbids a generic abstraction beyond it. This is consistent with
   ARCHITECTURE.md §5's anti-overengineering notes and with §5.4's precedent,
   where the analogous Pet-ATK consumer was implemented as a single pure
   Domain function beside the collection's other operations rather than as a
   stat system.

4. It keeps healing outside the Damage Pipeline. §4 item 6 is unchanged:
   Heal is "NOT subject to the Damage Pipeline (§3)". The Heal Resolution step
   is a separate computation, not a pipeline step — so §3's six steps remain
   untouched.

5. It does not disturb §5's Status Effect model. The Thủy Ma instance remains
   the §2.3.1 BuffDebuff D-2b/D-2a selected; this decision defines only where
   its magnitude is READ, not how it is stored or how long it lasts.
```

**Still OPEN after D-2c:**

```text
D-2c-duration  ✅ RESOLVED — see "D-2c-duration" below. (Earlier statement,
      retained for the record: the Thủy Ma instance's 3-Turn duration
      boundary — when the countdown starts, which Turn's step 19a performs
      the first decrement, and whether the activation Turn is included.)
D-2d  Reapplication — refresh / replace / stack; and whether the one-time
      Battle Start trigger makes ordinary repeat cases unreachable for MVP.
D-2e  Events — whether application/removal requires an event, and whether the
      Battle Start application emits PassiveTriggered.
```

---

### D-2c-duration — Thủy Ma's 3-turn duration boundary

**Status: DECIDED (binding).** Supplied by the Product Owner. This resolves the
duration boundary that the D-2c mechanism decision did not address (D-2c was
re-scoped to the Heal Resolution mechanism; this decision answers the
originally-tracked duration subject).

**Recorded verbatim as supplied:**

```text
The Thủy Ma −50% healing effect is applied at Battle Start with
RemainingTurns = 3.

The effect is active throughout Turns 1, 2, and 3.

The Battle Start application itself is NOT counted as a separate
turn and does not immediately consume one duration unit.

The existing TurnBased lifecycle decrements RemainingTurns at the
existing End Turn / step 19a boundary.

After step 19a of Turn 3, RemainingTurns reaches 0 and the effect
expires before Turn 4.

No new duration mechanism or lifecycle phase is introduced.
```

**Bindable statement (C-6).**

```text
- Apply point:  Battle Start, with RemainingTurns = 3.
- Active:       Turns 1, 2, and 3.
- The Battle Start application is NOT a turn: it neither counts as a turn nor
  consumes a duration unit on its own.
- Decrement:    the EXISTING TurnBased lifecycle, at the EXISTING End Turn /
  GAME_RULES.md §17 step 19a boundary — exactly one decrement per Turn
  (COMBAT_RULES.md §5.3 DR2).
- Expiry:       after step 19a of Turn 3, RemainingTurns reaches 0 and the
  effect expires before Turn 4 (DR5).
- No new duration mechanism or lifecycle phase is introduced.
```

**The decrement schedule, derived from the decision and the existing rules.**

```text
Battle Start   Apply(3)              -> RemainingTurns = 3   (no turn consumed)
Turn 1         active
               step 19a              -> RemainingTurns = 2
Turn 2         active
               step 19a              -> RemainingTurns = 1
Turn 3         active
               step 19a              -> RemainingTurns = 0 -> expires (DR5)
Turn 4         inactive
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. The arithmetic matches COMBAT_RULES.md §5.3.3's worked examples exactly.
   §5.3.3's first example is "duration = 2, no refresh: Turn N: Apply(2) ->
   remaining = 2; step 19a -> remaining = 1. Turn N+1: active; step 19a ->
   remaining = 0 -> expires. Turn N+2: inactive." The decision is the same
   pattern with duration = 3 and N = the Battle Start application: the
   counting Turn is the FIRST counted Turn (Turn 1), NOT the application
   point. This is precisely the shape §5.3.1 (DR6) presumes — an application
   before step 19a consumes one Turn of duration at THAT Turn's step 19a.

2. "Battle Start is not a turn" is required by, and consistent with,
   GAME_RULES.md §17's structure. §17's Turn loop is entered by a committed
   Swap (GAME_RULES.md §2; MATCH3_RULES.md §8.1), and GAME_EVENTS.md §2
   places BattleStarted "before the first Turn". So no Turn exists at the
   application point for a decrement to attach to, and none is invented.

3. The active-Turn count is internally consistent. "Active throughout Turns 1,
   2, and 3" plus "expires before Turn 4" is exactly 3 active Turns for
   RemainingTurns = 3 — the value is not over- or under-counted, and the
   application turn is excluded per the decision's own statement.

4. The decision introduces no new mechanism. It uses §5.3's existing DR1–DR5
   (counter, single step-19a decrement, expiry at 0) and §5.3.1 (DR6). It does
   NOT widen §2.3.1 item 3's duration dichotomy and does NOT relax item 6 —
   the instance remains a Turn-countdown BuffDebuff using RemainingTurns.
   Therefore no AGENTS.md §18 battle-state change and NO ADR.

5. It does not conflict with the D-2c mechanism decision. D-2c owns WHERE the
   magnitude is read (the shared Heal Resolution step); this decision owns
   WHEN the instance is active and how it expires. The two are orthogonal and
   consistent: all three active Turns route through D-2c's Heal Resolution
   step, and from Turn 4 the instance is gone so the step sees no −50%
   modifier.

6. Interaction with GAME_RULES.md §17 step 18a (the Boss Passive step) is
   nil for the apply point. The effect is applied at Battle Start, NOT at
   step 18a — the trigger D-2b selected is a one-time Battle Start trigger,
   not a match-charged step-18a evaluation for this Boss. Nothing in §17 is
   reordered.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- COMBAT_RULES.md §5.3 needs NO rule change: DR1–DR6 already express this
  schedule. The owning edit should ADD the Battle-Start-application case to
  §5.3.3's worked examples (a duration-3 apply before Turn 1), because §5.3.3
  currently illustrates only applications occurring at a Turn's own step.
- §5.3.1 (DR6) needs NO amendment: "an application before step 19a consumes
  one Turn at that Turn's step 19a" already covers a pre-Turn-1 application;
  the first consuming Turn is simply Turn 1.
- BOSS_RULES.md §6.2's Thủy Ma row/prose correction (shared with D-2a/D-2b)
  should state the apply point as Battle Start with a 3-Turn active window,
  and must not describe the effect as always-on.
- No new BattleState member: the instance is the existing §2.3.1 BuffDebuff
  with RemainingTurns, already typed and already serializable with BattleState
  under REDIS_STATE.md §7 item 9.
- No new event, SignalR member, Redis key, or database column.
- No ADR.
```

**Still OPEN after D-2c-duration:**

```text
D-2d  ✅ RESOLVED — see "D-2d" below. (Earlier statement, retained for the
      record: reapplication — refresh / replace / stack; and whether the
      one-time Battle Start trigger makes ordinary repeat cases unreachable
      for MVP content.)
D-2e  Events — whether application/expiry requires an event, and whether the
      Battle Start application emits PassiveTriggered. (GAME_RULES.md §16 is
      the closed list; no heal-modifier or expiry event exists.)
```

---

### D-2d — Thủy Ma reapplication behavior

**Status: DECIDED (binding).** Supplied by the Product Owner.

**Recorded verbatim as supplied:**

```text
D-2d — Reapplication

If Thủy Ma's −50% healing effect is applied again while an existing
instance is active, the existing instance is REFRESHED to the full
3-turn duration.

The modifier does NOT stack additively.

There is only one active Thủy Ma healing-reduction instance at a time.

Reapplication therefore results in:

existing effect
    ↓
replace/refresh RemainingTurns = 3

not:

existing −50%
    +
new −50%
    =
−100%

The refreshed instance retains the same Thủy Ma source identity.
```

**Bindable statement (C-7).**

```text
- Reapplication while an instance is active REFRESHES that instance to the
  full 3-turn duration (RemainingTurns = 3).
- The modifier does NOT stack additively. −50% + −50% = −100% is explicitly
  NOT the behavior.
- At most ONE active Thủy Ma healing-reduction instance exists at a time.
- The refreshed instance retains the SAME Thủy Ma source identity — refresh
  reuses the existing instance rather than creating a second one.
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. The decision IS §5.2 item 2's MVP default, applied to this effect.
   COMBAT_RULES.md §5.2 item 2: "Stacking behavior (refresh duration vs. stack
   magnitude vs. independent instances) is defined per-effect; default for MVP
   is **refresh duration, do not stack magnitude** unless a Card/Relic
   explicitly says otherwise". Thủy Ma is a Boss Passive, not a Card/Relic
   carve-out, so the default governs — and the decision states that default
   without altering it. NO §5.2 change is required.

2. "Only one instance at a time" is §2.3.1 item 6, verbatim in effect.
   GAME_STATE.md §2.3.1 item 6: "There is never more than one instance per
   effect identity per entity. Applying an effect that is already active
   refreshes that existing instance rather than appending a second one ...
   so the array holds at most one element per `Id`." The decision's
   "retains the same Thủy Ma source identity" is what makes item 6 apply:
   the refresh targets the SAME `Id`, so no second element is appended.

3. The refresh MECHANISM is already defined by §5.3 DR3/DR4 — no new rule.
   DR3: "Apply and Refresh use the SAME mechanism: `remaining = duration`".
   DR4: "Same-Turn reapplication ... resets `remaining` to the new duration
   value and does NOT trigger an additional consumption in that Turn. Only
   step 19a consumes."
   The decision's "replace/refresh RemainingTurns = 3" is exactly DR3's
   operation, and DR4 governs the within-Turn case.

4. It is consistent with D-2c-duration's derived schedule. A refresh sets
   RemainingTurns back to 3, so the active window extends to three Turns from
   the refresh point — the §5.3.3 "duration = 2, refreshed in Turn N+1" example
   pattern, at duration 3.

5. It does not widen §2.3.1 item 3's duration dichotomy or relax item 6 —
   it APPLIES item 6. Therefore no AGENTS.md §18 battle-state change and
   NO ADR.
```

**⚠ Consequence reported, NOT authored — the trigger to re-apply is
unreachable for MVP content, so D-2d is a forward-compatibility rule today.**

This was flagged as a NOTE in the D-2c-duration ledger entry and is now
confirmed by inspection, not assumed:

```text
D-2b   sets Thủy Ma's trigger to BATTLE START — a ONE-TIME trigger
       (PASSIVE_RULES.md §3: "Battle Start (one-time trigger)").
D-2c-duration gives the instance a fixed 3-Turn window that expires before
       Turn 4.
BOSS_RULES.md §6.2 denies Thủy Ma any match-based charging and any
       PassiveCharged/PassiveTriggered from match progress.

CONSEQUENCE: for the CURRENT MVP content, the effect is applied exactly once
  (Battle Start) and can never be re-applied, because no second trigger
  exists. D-2d's refresh rule is therefore UNREACHABLE in MVP play as the
  content stands — it is a correctness rule that prevents a FUTURE
  re-application source (a second Battle Start, a content change, or a new
  Boss Passive) from stacking to −100%.

  This is REPORTED for the owning edit: the rule must still be authored
  (it is the safe behavior and it is §5.2 item 2's default), but no MVP test
  can exercise a genuine second application through the Passive itself. A test
  may exercise refresh only by applying the effect directly.

  NO decision is requested by this note — D-2d is resolved. This is a
  reported reachability limitation, and it must NOT be read as "D-2d is
  moot" or as licence to omit the rule.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- COMBAT_RULES.md §5.2 needs NO change: item 2's default already states
  refresh-not-stack, and this decision does not carve Thủy Ma out of it.
- COMBAT_RULES.md §5.3 needs NO change: DR3/DR4 already define the refresh
  mechanism.
- GAME_STATE.md §2.3.1 needs NO change: item 6 already enforces one instance
  per identity.
- The owning edit should state Thủy Ma's reapplication behavior where the
  effect is defined (BOSS_RULES.md §6.2's row/prose, shared with
  D-2a/D-2b/D-2c-duration) and POINT AT §5.2 item 2 rather than restating it
  (.ai/workflow/documentation/documentation-change.md §2).
- No new BattleState member, event, SignalR member, Redis key, or database
  column. No ADR.
```

**Still OPEN after D-2d:**

```text
D-2e  ✅ RESOLVED — see "D-2e" below. (Earlier statement, retained for the
      record: events — whether application/expiry requires an event, and
      whether the Battle Start application emits PassiveTriggered.)
```

---

### D-2e — Thủy Ma observability and events

**Status: DECIDED (binding).** Supplied by the Product Owner. This closes the
Thủy Ma decision family.

**Recorded verbatim as supplied:**

```text
D-2e — Observability

Thủy Ma's −50% healing effect does NOT introduce a new Battle Event.

It does NOT emit PassiveCharged or PassiveTriggered.

The effect is observable through the existing authoritative
BattleStateUpdated synchronization.

Application, refresh, decrement, and expiry are state changes only.

No new event such as HealingReduced, BossPassiveApplied, or
BossPassiveExpired is introduced.
```

**Bindable statement (C-8).**

```text
- NO new Battle Event is introduced for this effect. GAME_RULES.md §16's
  canonical list is unchanged, and GAME_EVENTS.md §2 is unchanged.
- The effect emits NO PassiveCharged and NO PassiveTriggered.
- Observability is through the EXISTING authoritative BattleStateUpdated
  synchronization (SIGNALR_PROTOCOL.md §4).
- Application, refresh, decrement, and expiry are STATE CHANGES ONLY.
- Explicitly NOT introduced: HealingReduced, BossPassiveApplied,
  BossPassiveExpired, or any equivalent.
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. It matches the established precedent for a state-only effect.
   GAME_STATE.md §5.1.1 item 10: "Nothing here is published. This lifecycle
   adds no event, no payload member, and no SignalR method." The D-2c-duration
   decision already routes decrement/expiry through that same §5.1.1
   lifecycle, so no publication follows from it. This is the same position
   TASK-113 D-4 / §3.3 item 6 took for the Crit outcome and TASK-119 took for
   Root's ATK modifier.

2. It is consistent with BOSS_RULES.md §7's event list. §7 lists
   PassiveCharged, PassiveTriggered, BossSkillCast, BattleWon, BattleLost, and
   states "No Boss-specific passive event name is needed". It also states Boss
   state changes "(Idle → Enraged, Idle → Stunned) are inferable from the
   sequence of DamageDealt, DamageTaken, BossSkillCast, and PassiveTriggered
   events — no dedicated BossStateChanged event exists." The decision is the
   same position: no dedicated event.

3. It is consistent with §6.2's own prohibition, which is now preserved
   rather than merely scoped. BOSS_RULES.md §6.2 states Thủy Ma "emits no
   PassiveCharged/PassiveTriggered from match progress". The decision extends
   that to the effect entirely — no emission at all, not merely none from
   match progress. This is strictly stronger than the documented prohibition
   and does not contradict it.

4. It resolves the D-2c-duration carry-over question. D-2c-duration placed
   application at Battle Start; whether that application emitted
   PassiveTriggered was left open by D-2b and D-2c-duration. This decision
   answers it: no.
```

**⚠ CONSEQUENCE REPORTED, NOT AUTHORED — `BattleStateUpdated` does not
currently carry `BossState`, so the decision's chosen observability channel
does not yet expose this effect.**

The decision names the existing `BattleStateUpdated` synchronization as the
observability mechanism. Direct inspection of `SIGNALR_PROTOCOL.md` §4 shows
that this push **does not currently carry any `BossState` field**, and that its
payload is deliberately limited:

```text
SIGNALR_PROTOCOL.md §4 (line 1197):
    BattleStateUpdated(battleId, turn, sequence, board, rngSeed, rngState,
                       playerState, petState)

§4 item 4 (line 1239): "Payload. The fields of the implemented GAME_STATE.md
    §2.0 stage (§0 above), projected one-to-one from that state. ... No other
    field may be added to this record: additional state is introduced by
    extending GAME_STATE.md §2.0, not by the wire shape."

§4's stage table (lines 1204-1216) enumerates the four permitted shapes and
    ends at "... playerState, petState". There is NO bossState member.

§4 (line 1218): "No gameplay field beyond the stage's own is carried, and no
    `Status`/lifecycle value is carried anywhere in the protocol (§8.3)."

CONTRAST — the two existing exceptions were explicitly justified, not assumed:
    §4 item 13 added PetState because PASSIVE_RULES.md §6 item 1 REQUIRES the
      Passive progress value to be visible to the player.
    §4 item 14 REFUSED to add PetState.NextAttackCritModifiers[] precisely
      because "no document requires a Crit modifier to be exposed".

CONSEQUENCE: the effect's state DOES live in BossState.StatusEffects[]
  (GAME_STATE.md §2.4/§2.4.1, implemented per TASK-095/TASK-096), and that
  collection DOES serialize with BattleState (REDIS_STATE.md §7 item 9). But
  the WIRE does not carry BossState today. So under the current protocol the
  effect is observable server-side and in persisted/recoverable state, while
  being NOT VISIBLE to the client — and §4 item 4 forbids adding a member as
  a mere side effect of this decision.

  This is REPORTED, not resolved here. Two readings are possible and only the
  Product Owner may choose:
    (a) the decision means "no new EVENT; visibility follows whatever state
        the protocol already delivers" — in which case the effect is
        deliberately client-invisible for now and NOTHING further is needed;
    (b) the decision intends the client to actually SEE the effect, which
        would require a separate SIGNALR_PROTOCOL.md §4 payload extension
        (its own task, with its own §4 item 13/14-style justification).
  The decision text says the effect "is observable through the existing
  authoritative BattleStateUpdated synchronization", which reads as (a) —
  observability through the sync CHANNEL, not a claim that a new member
  exists. It is recorded here under reading (a) as the literal meaning, with
  reading (b) flagged as an open follow-up question for the Product Owner
  rather than assumed.

  NO blocking decision is requested: D-2e is resolved as written (no event, no
  emission, state-only). This note records a wire-visibility limitation so it
  is not mistaken for "the client will see −50% applied".
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- GAME_RULES.md §16 needs NO change: the canonical event list is unchanged.
- GAME_EVENTS.md §2 needs NO change: no payload member is added.
- SIGNALR_PROTOCOL.md §4 needs NO change FROM THIS DECISION: §4 item 4 governs,
  and adding a member is not an act of this task. If reading (b) above is the
  intent, that is a SEPARATE protocol task.
- REDIS_STATE.md needs NO change: BossState.StatusEffects[] already rides the
  existing BattleState write-back (§7 item 9).
- GAME_STATE.md §5.1.1 needs NO change: item 10 already states the lifecycle
  publishes nothing.
- No ADR.
```

**The Thủy Ma decision family is now CLOSED** — D-2a, D-2b, D-2b-site, D-2c,
D-2c-duration, D-2d, D-2e. No decision in it introduced a new event, wire
member, state member, Redis key, database column, or ADR.

---

### D-3 (D-3a – D-3f) — Mộc Yêu regeneration

**Status: DECIDED (binding).** Supplied by the Product Owner, resolving all six
sub-questions D-3 carried.

**Recorded verbatim as supplied:**

```text
D-3a — Timing:
Mộc Yêu regeneration is applied during Boss Response step 18a,
when the Boss Passive effect is applied.

D-3b — Base:
Heal exactly 5% of Mộc Yêu's MaxHP.

D-3c — Rounding:
Truncate toward zero to an integer HP amount.

D-3d — Clamping:
Final HP = min(CurrentHP + RegenAmount, MaxHP).
No overheal is retained.

D-3e — Observability:
Direct authoritative BossState.HP update.
No new Battle Event.
The resulting state is observable through existing authoritative
state synchronization.

D-3f — Repetition:
Each valid Mộc Yêu Passive activation applies one 5% MaxHP
regeneration. It does not stack as a persistent modifier.
```

**Bindable statement (C-9).**

```text
D-3a  Timing        applied during Boss Response step 18a, at the point the
                    Boss Passive effect is applied (GAME_RULES.md §17 step 18a;
                    BOSS_RULES.md §3.3).
D-3b  Base          exactly 5% of Mộc Yêu's MaxHP (BOSS_RULES.md §6.2's
                    magnitude; MaxHP = 5000 per §6.1).
D-3c  Rounding      TRUNCATE TOWARD ZERO to an integer HP amount.
D-3d  Clamping      Final HP = min(CurrentHP + RegenAmount, MaxHP);
                    NO overheal retained.
D-3e  Observability a DIRECT authoritative BossState.HP update; NO new Battle
                    Event; observable through existing authoritative state
                    synchronization.
D-3f  Repetition    each valid Mộc Yêu Passive activation applies ONE 5% MaxHP
                    regeneration; it does NOT stack as a persistent modifier.
```

**Worked value (D-3b × D-3c at the §6.1 MVP MaxHP).**

```text
Mộc Yêu MaxHP      = 5000            (BOSS_RULES.md §6.1)
RegenAmount        = truncate(5000 × 5 / 100) = 250
Final HP           = min(CurrentHP + 250, 5000)   (D-3d)
```

**Assessment against documented evidence (recorded, not authored).**

```text
1. D-3a's timing IS step 18a, and needs no new step. GAME_RULES.md §17 step
   18a: "Boss Passive — evaluate the Boss's Passive trigger condition against
   the post-damage battle state. If the trigger is met, apply the Passive
   effect...". BOSS_RULES.md §3.3 fixes the surrounding order: the Passive
   "fires once per player action, after all player damage is resolved" (item
   1), before Boss Skill (18b) and Boss Attack (18c), and Boss Skill damage
   does not re-trigger it (item 4). The decision places regeneration exactly
   where §17 step 18a already says the effect is applied. §17's step order is
   NOT changed.

2. D-3c's rounding follows the project's existing integer convention rather
   than inventing one. COMBAT_RULES.md §5.4.2 states the reduction "is applied
   ... and the result is an integer, truncated toward zero — the same integer
   convention §3 step 6 already uses for Final Damage", and its worked table
   (ATK 50→35, 51→35, 99→69, 100→70, 101→70) is truncation toward zero.
   §3 step 6 fixes Final Damage as "an int: truncated, never negative"
   (SIGNALR_PROTOCOL.md §3.2.13 item 4). D-3c is the same convention, applied
   to a heal instead of a reduction. At the §6.1 value the truncation is
   lossless (5000 × 5% = 250 exactly); the rule matters only if MaxHP becomes
   non-divisible by 20 via future progression (PET_RULES.md §5).

3. D-3d's clamp matches COMBAT_RULES.md §4 item 1's shaping, without
   contradicting it. §4 item 1: "Heal effects restore HP up to Max HP;
   overheal is discarded unless a Relic explicitly grants overheal/temp-HP."
   D-3d's min(CurrentHP + RegenAmount, MaxHP) is exactly "up to Max HP", and
   "no overheal is retained" is exactly "overheal is discarded". No MVP Relic
   exists to trigger §4 item 1's carve-out (ROADMAP.md Phase 1 — "No Relics
   yet"), so the default applies. §4 item 1 is NOT reworded.

4. D-3a interacts with the D-2c Heal Resolution mechanism — REPORTED, not
   assumed. §4 item 1's clamp is the LAST step of D-2c's canonical order
   (Raw Heal → applicable Heal modifiers → final Heal amount → existing
   MaxHP/overheal clamp → HP update). D-3's regeneration is a Heal restoring
   the BOSS's HP, whereas D-2c's stated scope is healing "that restore[s] Pet
   HP" (COMBAT_RULES.md §4, and D-2b-site's reach wording is "healing received
   by the affected Pet"). So D-3's target is the BOSS, not the Pet. Whether
   Mộc Yêu's regeneration should also route through D-2c's shared Heal
   Resolution step is NOT settled by either decision and is REPORTED below.

5. D-3f uses §5.2 item 2's own vocabulary. "Does not stack as a persistent
   modifier" is consistent with refresh-not-stack (§5.2 item 2) — except that
   a regeneration is an instantaneous HP write, not a duration-carrying
   instance, so no `StatusEffects[]` element is created and §2.3.1 item 9's
   instant-non-duration rule applies by analogy: "Instant, non-duration
   effects create no instance." A repeat activation simply writes HP again.
```

**⚠ CONSEQUENCE REPORTED, NOT AUTHORED — the decision's D-3e observability
claim has the same wire-visibility limitation D-2e has.**

D-3e says the state "is observable through existing authoritative state
synchronization". Inspected, that channel does not carry Boss HP during play:

```text
SIGNALR_PROTOCOL.md §4 BattleStateUpdated payload (line 1197) carries
    battleId, turn, sequence, board, rngSeed, rngState, playerState, petState
    — NO bossState, and §4 item 4 forbids adding a member as a side effect.

Absolute Boss HP appears on the wire ONLY at battle end:
    BattleWon/BattleLost `finalBossHp` (SIGNALR_PROTOCOL.md §3.2.19 line 915:
    "Boss HP at battle end (GAME_STATE.md §2.4)").

DamageDealt/DamageTaken report only the DELTA ("amount ... the Final Damage
    applied"), never absolute HP — §3.2.14 item 2 and §3.2.15 item 1 confirm
    they "describe that one reduction" rather than publishing an HP value.

API_CONTRACTS.md exposes only `initialState.bossState.element` (§3), not HP.

CONSEQUENCE: a direct BossState.HP write is authoritative and Redis-persisted
  (it rides the existing BattleState write-back, REDIS_STATE.md §7 item 9), but
  no wire member currently reports Boss HP mid-battle. So the regeneration is
  server-side and recoverable without being client-visible — the SAME
  limitation recorded for D-2e, and the same reading applies: D-3e means "no
  new EVENT; visibility follows whatever state the protocol already delivers",
  not that a Boss-HP member exists. If client visibility of Boss HP is
  intended, that is a SEPARATE SIGNALR_PROTOCOL.md §4 / §3.2 extension task,
  and it would serve Rage, regeneration, and every other Boss-state change
  together — not regeneration alone.

NO blocking decision is requested. Recorded so D-3e is not mistaken for "the
client will see the Boss heal".
```

**⚠ CONSEQUENCE REPORTED, NOT AUTHORED — heal-TARGET scope vs D-2c.**

```text
D-2c authored a shared Heal Resolution step whose stated scope is healing that
  restores PET HP, and D-2b-site's reach is "healing received by the affected
  Pet".
D-3 heals the BOSS.

So the decision set now describes TWO heal targets. Whether the Boss-side heal
  (a) reuses D-2c's shared step (making it a general Heal Resolution for any
  target), or (b) is a separate direct HP write that only borrows §4 item 1's
  clamp shape, is NOT stated by either decision.

D-3d itself is expressed as "Final HP = min(CurrentHP + RegenAmount, MaxHP)",
which is self-contained and does not require the shared step. Recorded as an
OPEN follow-up question for the Product Owner, NOT as a blocker and NOT
resolved by inference: the owning edit must state which of (a)/(b) holds, or
D-2c's scope wording must be widened deliberately.
```

**Consequences reported for the owning edit (NOT applied by this task).**

```text
- GAME_RULES.md §17 needs NO change: step 18a already says the effect is
  applied there; no step is added, removed, or reordered.
- COMBAT_RULES.md §4 item 1 needs NO change: D-3d already matches "restore HP
  up to MaxHP; overheal is discarded".
- COMBAT_RULES.md §4's scope wording MAY need a deliberate widening if the
  Product Owner selects option (a) above (Boss-side heal routed through the
  shared step). REPORTED, not assumed.
- BOSS_RULES.md §6.2's Mộc Yêu row/prose is the natural home for the six
  resolved semantics (timing, base, rounding, clamping, observability,
  repetition) — or a pointer to them — consistent with how the Thủy Ma family
  is handled.
- GAME_RULES.md §16 and GAME_EVENTS.md §2 need NO change: no event.
- GAME_STATE.md needs NO change: BossState.HP already exists (§2.4) and already
  rides the existing write-back.
- No ADR: no new battle-state concept, no persistence change, no architecture
  change.
```

**Still OPEN after D-3:**

```text
D-1-representation  Where the Rage instance is held.
D-1a                Which damage the Rage +20% reaches.
D-1b                Rage's duration model.
D-1c                Rage reapplication behavior.
D-4                 Event/wire/storage consequences across all three effects
                    (now largely Answerable from D-2e and D-3e, but not yet
                    recorded as a decision).
```

---

## Product Owner Indications (NOT EXECUTED — NOT A DECISION RECORD)

<!--
  READ THIS FIRST. Nothing in this section is a binding Product Owner decision.

  On execution, the requester was asked how to proceed and selected
  "Record as open / STOP". Three provisional options were selected in the same
  interaction while that STOP mode was chosen. Because the STOP mode governs,
  and because a decision is binding only when the Product Owner supplies it as
  a decision (AGENTS.md §7), these are recorded here as NON-BINDING
  INDICATIONS that may inform a future Product Owner decision. They are NOT
  recorded in the "Product Owner Decisions" section, they resolve nothing,
  they unblock nothing, and no task may implement against them.

  They are preserved because discarding a Product Owner's stated preference
  would itself be a silent loss of information.
-->

```text
Indication I-1   (in response to D-1)      STATUS: NOT EXECUTED
  "Rage represented as a StatusEffect BuffDebuff in BossState.StatusEffects[]"

  Assessment against the evidence: this is the ONLY indicated option that does
  not require a new battle-state concept, and GAME_STATE.md §2.4.1/§2.4.5
  already establish that BossState.StatusEffects[] exists with Stun's
  Turn-countdown precedent. It therefore would NOT trigger AGENTS.md §18.

  UPDATED after the D-1 Product Owner decision: the earlier note here that it
  "is in direct tension with COMBAT_RULES.md §3.4 and §5.4.5" and "cannot be
  executed until the Product Owner rules on Contradiction A" is now SUPERSEDED.
  The D-1 decision resolved that tension by separating Step 4 (unchanged) from
  the Step-1 consumption point, so a Rage instance in BossState.StatusEffects[]
  no longer contradicts §3.4 or §5.4.5. The indication remains NON-BINDING: the
  decision did not select the instance's home, so D-1-representation is still
  open. D-1a (which damage/ATK), D-1b (duration model), and D-1c
  (reapplication) also remain unanswered.

Indication I-2   (in response to D-2a)     STATUS: SUPERSEDED
  "Triggered 3-turn effect (expires)"

  This indication has been SUPERSEDED by the binding D-2a Product Owner
  decision recorded above, which reaches the same conclusion ("for 3 turns" is
  authoritative; a triggered temporary effect; not always-active) and adds the
  explicit ruling that the "Passive (always active)" wording is stale.
  The binding decision is the operative record; this indication is retained
  only as the history of how it was reached and is no longer independent.

  Assessment retained from the indication: it is self-consistent with
  COMBAT_RULES.md §5.3's Turn-countdown lifecycle, but it requires a documented
  re-application path that §6.2's own text denies ("emits no PassiveCharged/
  PassiveTriggered from match progress"). D-2b (application site and reach),
  D-2c (duration start/decrement/expiry), D-2d (reapplication), and D-2e
  (events) remain unanswered. The §6.2 wording would also need correction,
  which is a separate act. **As the D-2a decision now confirms, this is exactly
  the newly exposed gap: the trigger itself is undefined pending D-2b.**

Indication I-3   (in response to D-3e)     STATUS: NOT EXECUTED
  "Direct BossState.HP write, no new event"

  Assessment against the evidence: this is consistent with GAME_RULES.md §16's
  closed canonical event list (no regeneration event exists) and with
  COMBAT_RULES.md §4 item 6 (Heal is not pipeline damage). It does not by
  itself answer D-3a (timing), D-3b (base value), D-3c (rounding), D-3d
  (clamping), or D-3f (repeated activation), and its position relative to
  BOSS_RULES.md §5 item 4's Boss-HP terminal check remains undefined.
```

**A complete decision set is still required.** These three indications covered
at most three of the twenty-plus open dimensions enumerated in "Required
Authoritative Result — Coverage". Two of them (I-1's concern and I-2) have since
been overtaken by the binding D-1 and D-2a decisions recorded above; I-2 is
marked SUPERSEDED. I-1 and I-3 remain NON-BINDING and resolve nothing.
**The remaining open dimensions are D-1-representation/D-1a/D-1b/D-1c, D-2b
(including the undefined-trigger gap D-2a exposed), D-2c, D-2d, D-2e, and all of
D-3 and D-4.**

---

## Remaining Pre-Existing Documentation Gaps

<!--
  REPORTED ONLY. These are NOT TASK-123 decision IDs, they are NOT resolved by
  any decision recorded here, and this task neither created nor modified
  another task for them. They are gaps in existing documents that the
  decision-gathering process SURFACED but that lie outside TASK-123's scope.
-->

### GAP-1 — Boss Skill Step-1 composition (`COMBAT_RULES.md` §3.4 × `BOSS_RULES.md` §6.3.1)

```text
Surfaced by: D-1a.

COMBAT_RULES.md §3.4 says a Boss Skill's "Step 1 Base Damage is defined per
Skill (BOSS_RULES.md §6)". BOSS_RULES.md §6.3.1 item 1 says Flame Burst's
"Base Damage: 150".

But §3 step 1 defines Base Damage as a SUM: "Base Damage (from ATK stat,
Skill/Card base value, and any ATK-Gem-generated damage pool for this
action)", and §5.4.1 item 2 spells that sum out for the Pet:
"Step 1 = EffectiveATK + Skill/Card base value + ATK-Gem-generated damage
pool".

Neither §3.4 nor §6.3.1 states whether a Boss Skill's Step 1 is:
   (i)  the Skill's authored Base Damage ALONE (the value REPLACES any ATK
        term), or
   (ii) EffectiveBossATK + the Skill's Base Damage (both contribute).

STATUS: PRE-EXISTING and UNRESOLVED. Not settled by Contradiction A, not by
D-1a, not by D-1b/D-1c, and not by D-4.
WHY IT DID NOT BLOCK: D-1a's rule ("Rage modifies only Boss damage whose
Step 1 Attack input is derived from BossState.ATK") excludes a Skill's
independently authored Base Damage under BOTH readings, so the Rage contract
is deterministic either way.
IMPACT WHEN IMPLEMENTING: the Boss Skill damage path cannot be implemented
deterministically until this is stated, because the two readings give
different Flame Burst damage. Under (i) it is 150; under (ii) it is
EffectiveBossATK + 150.
```

### GAP-2 — `DATABASE.md` §6 pointer to the retired `BOSS_RULES.md` §6.2 wording

```text
Surfaced by: D-2a / D-2b / D-4d.

DATABASE.md §6's constraint list contains:
  "BossDefinition.PassiveDefinition.threshold = null ⇔ always-active, no
   threshold    (BOSS_RULES.md §6.2)"

That pointer cites §6.2 for an "always-active" semantic. The D-2a decision
RETIRES the "Passive (always active)" wording as stale/conflicting. So the
sentence "threshold = null ⇔ always-active" now cites a rule the Product Owner
has retired.

STATUS: PRE-EXISTING constraint, NOT resolved here. D-4d records that no
PostgreSQL SCHEMA change is introduced, which is unaffected; but the
DOCUMENTATION pointer is now inconsistent with the D-2a decision and will need
reconciling by whichever task applies these decisions.

NOTE: the constraint itself (`threshold = null`) is a STORAGE rule about the
persisted PassiveDefinition and is not necessarily invalidated by D-2a — what
is invalidated is the characterisation "always-active" as the semantic that
null denotes, now that Thủy Ma's trigger is Battle Start. Whether null should
denote "no threshold" (trigger defined elsewhere) rather than "always-active"
is a question for that reconciliation.
```

### GAP-3 — `GAME_STATE.md` §2.4.1's "no Boss applies a Status Effect to itself" sentence

```text
Surfaced by: D-1 (Rage representation).

GAME_STATE.md §2.4.1 states: "For MVP, no content-defined Boss applies a
Status Effect to itself; the collection is defined because Stun (§2.4.5) and
future content are tracked through it."

D-1 places Hỏa Long Rage — a content-defined Boss status effect on the Boss
itself — in exactly that collection. The sentence therefore becomes stale once
Rage is implemented.

STATUS: PRE-EXISTING sentence, NOT edited here (D-1's "consequences for the
owning edit" already registers this). Recorded as a gap so the reconciliation
task does not miss it.
```

### GAP-4 — Client visibility of BossState (recorded limitation, by decision)

```text
Surfaced by: D-2e, D-3e, then EXPLICITLY DECIDED by D-4b/D-4f.

SIGNALR_PROTOCOL.md §4 does not carry BossState, so Boss HP and Boss
StatusEffects are not client-visible in the current MVP protocol.

STATUS: this is NOT a documentation gap in the "inconsistent/stale" sense — it
is a RECORDED, INTENTIONAL contract limitation (D-4f). It is listed here only
so the reconciliation task does not mistake it for an omission. Any change is
a SEPARATE SignalR protocol task (D-4b/D-4f), and would serve Rage,
regeneration, and all Boss-state changes together.
```

### GAP-5 — Boss-side heal vs `D-2c`'s Pet-scoped Heal Resolution step

```text
Surfaced by: D-3 (regeneration heals the BOSS) × D-2c (the shared Heal
Resolution step is scoped to healing that restores PET HP).

RESOLVED FOR TASK-123's PURPOSES by D-4g: D-3 uses its OWN direct Boss HP
update and does NOT widen D-2c's Pet-scoped contract; whether future Boss-side
healing should use the shared mechanism is a separate contract question.

STATUS: the TASK-123 boundary is now decided (D-4g). What remains PRE-EXISTING
and open is the genuine design question — whether a shared Heal Resolution step
should ever be target-agnostic. Recorded as a gap for a FUTURE task, explicitly
NOT to be resolved by the reconciliation task that applies TASK-123's
decisions, because D-4g deliberately leaves D-2c's scope unchanged.
```

**None of GAP-1 through GAP-5 was created by TASK-123, none is a TASK-123
decision ID, and no task was created or modified for any of them** (per the
requester's instruction). They are reported here because the decision-gathering
process surfaced them and silence would lose the information.

---

## Canonical Ownership Register (Identified, Not Applied)

<!--
  Per .ai/workflow/documentation/documentation-change.md §3, each concept has
  ONE canonical owner. This register names the owner each decision WOULD write
  to. NO edit was performed by this task — documentation-change.md §1 requires
  the owner to carry the rule, and this task authored no rule.
-->

```text
CONCEPT                              CANONICAL OWNER                EDIT REQUIRED (downstream)
-----------------------------------  -----------------------------  ---------------------------
Rage's gameplay rule (what +20% ATK  COMBAT_RULES.md                define the Boss-side ATK
  means, its repeat behavior)          (a new subsection beside      modifier rule, mirroring
                                        §5.4 — e.g. §5.5 "Boss       §5.4's Pet-side shape,
                                        Stat Modifiers")              referencing §3.4 (which
                                        [D-1 decision: §3.4 is NOT    is NOT amended) for the
                                        amended; §5.4.5 is NOT        Step-1 vs Step-4 split
                                        amended]                              [D-1]
Rage's damage scope                   COMBAT_RULES.md §3.4          NO amendment: §3.4 already
  RESOLVED by D-1a: only Boss damage   (basic attack "Step 1 —      distinguishes "Base Damage =
  whose Step 1 Attack input derives    Base Damage = Boss.ATK") vs   Boss.ATK" from "Step 1 Base
  from BossState.ATK; a Boss Skill's    BOSS_RULES.md §6.2 (the      Damage is defined per Skill".
  independently authored Base Damage   Hỏa Long row/prose)           The owning edit states the
  does not receive +20%; Flame Burst's                               scope (pointer, not restatement)
  150 remains 150 unless its own                                     and must NOT restate §3.4
  contract declares ATK scaling                                      (.ai/.../documentation-change
                                                                     .md §2)          [D-1a]
Rage's representation                GAME_STATE.md §2.4.1          NO structural change: the
  RESOLVED by D-1: a TurnBased         (BossState.StatusEffects[])   collection, schema, and
  BuffDebuff in BossState.             + §2.3.1 (element schema)     duration model already exist
  StatusEffects[] with TargetStat =                                  and are implemented
  "ATK", Magnitude = +20%,                                           (TASK-095/TASK-096). §2.4.1's
  RemainingTurns = 3; BossState.ATK                                  "For MVP, no content-defined
  immutable/base; not a separate                                     Boss applies a Status Effect
  BossState field                                                    to itself" becomes STALE and
                                                                     should be updated   [D-1]
Shared Heal Resolution step           COMBAT_RULES.md §4             AUTHOR the canonical shared
  RESOLVED by D-2c: all Pet-HP         (Healing — also widen §4's   Heal Resolution step: all
  healing routes through it before      own ownership line, which    Pet-HP healing sources
  the §4 item 1 clamp                   currently declares only      (Card Heal + the §2 HP-Gem
                                        "the canonical owner of      "Heal pool", never consumed
                                        the Shield rule")            today) route through it, with
                                                                     the order Raw Heal →
                                                                     applicable Heal modifiers →
                                                                     final Heal amount → existing
                                                                     MaxHP/overheal clamp → HP
                                                                     update. §4 item 1's clamp is
                                                                     NOT re-ordered or reworded.
                                                                     Combat-rule mechanism only:
                                                                     NO state/event/wire/Redis/DB
                                                                     change; no speculative generic
                                                                     abstraction   [D-2c]
Thuy Ma's healing-reduction rule     COMBAT_RULES.md §4             the −50% is ONE applicable
  RESOLVED by D-2b-site: applies at    (Healing)                     Heal modifier inside the D-2c
  the shared Heal resolution point                                   mechanism — not a special-cased
  BEFORE §4 item 1's overheal clamp;                                 site. It does not modify MaxHP
  reaches Card Heal + HP-Gem healing                                 and does not affect Shield
  + any existing source using that                                                [D-2b-site]
  resolution; does not modify MaxHP;
  does not affect Shield
Thuy Ma's activation/trigger         BOSS_RULES.md §6.2             correct the trigger column to
  RESOLVED by D-2b: trigger is        (the row + its prose at        Battle Start, reconcile the
  Battle Start (PASSIVE_RULES.md       lines 194-200)                lines 194-200 prose so it no
  §3, verified present);                                            longer describes an undefined
  representation is the existing                                    "always-on effect application",
  TurnBased BuffDebuff in                                           and drop the stale §3 citation
  BossState.StatusEffects[]                                         (one edit, shared with D-2a)
Moc Yeu's regeneration rule          COMBAT_RULES.md §4             define regeneration's
  (timing, rounding, clamping)         (Healing)                     application point, rounding,
                                                                      and MaxHP clamp      [D-3]
Thuy Ma's reapplication rule        BOSS_RULES.md §6.2             POINT AT §5.2 item 2 rather
  RESOLVED by D-2d: reapplication      (the row + prose) — the      than restating it: the refresh
  REFRESHES the existing instance      RULE itself is owned by        rule is already §5.2 item 2's
  to RemainingTurns = 3; no            COMBAT_RULES.md §5.2 item 2    MVP default, the one-instance
  additive stacking; at most one       (refresh-not-stack) +          rule is §2.3.1 item 6, and the
  active instance; same source         §5.3 DR3/DR4 (mechanism)       mechanism is §5.3 DR3/DR4 —
  identity                                                            all unchanged   [D-2d]
Rage's duration and reapplication      COMBAT_RULES.md §5.3.3         ADD the Boss-side step-18a
  RESOLVED by D-1b (duration) and        (worked examples) + BOSS_    application case to §5.3.3's
  D-1c (reapplication): applies at       RULES.md §6.2 (Hỏa Long row)  worked examples, beside the
  step 18a with RemainingTurns = 3;                                    D-2c-duration Battle-Start case
  non-retroactive; active three                                        (both are "apply before step 19a"
  counted Turns; decrement at step                                     instances). §5.3 DR1–DR6 need NO
  19a; expires before the following                                    change; §5.4.3 needs NO change
  Turn. Re-trigger REFRESHES to                                        (its non-retroactivity statement
  RemainingTurns = 3, no stacking,                                     already covers D-1b). The §6.2
  same source identity, at most one                                    row states the behavior and POINTS
  +20% instance                                                        AT §5.2 item 2 / §5.3 rather than
                                                                       restating them  [D-1b/D-1c]
Duration boundary for the Boss-     COMBAT_RULES.md §5.3.3         ADD a worked example to
  side instance (Thuy Ma)              (canonical duration owner —    §5.3.3 for a duration-3 apply
  RESOLVED by D-2c-duration:           the rule itself needs NO      BEFORE the first counted Turn
  apply at Battle Start with           change; DR1–DR6 already       (Battle Start apply → active
  RemainingTurns = 3; active           express this schedule)        Turns 1–3 → expires after
  Turns 1–3; application is not a                                    step 19a of Turn 3). §5.3.1
  turn and consumes no unit;                                         (DR6) needs NO amendment.
  decrement at End Turn / step 19a;                                  §5.3's DR1–DR5 are NOT changed
  expires before Turn 4                [D-2c-duration]
Duration consumption for the Boss    COMBAT_RULES.md §5.3           extend §5.3.2's scope to the
  side (Rage)                          (canonical duration owner)     Boss-side instance     [D-1b]
Boss Passive effect representation   GAME_STATE.md §2.4.2           D-2b needs NO edit here: the
  (only if a new concept is needed)    (Boss Passive)                 representation is the existing
                                       (+ ADR if a new concept)       StatusEffects[] collection,
                                                                      already documented by §2.4.1.
                                                                      §2.4.2 changes ONLY IF D-1's
                                                                      representation decision needs
                                                                      one — REPORTED, not authored
Which Bosses carry which effect      BOSS_RULES.md §6.2             correct the row wording
  (the effect rows themselves)         (MVP Boss Reference)           where a decision rules it
                                                                      stale (D-2a/D-2b did so for
                                                                      Thuy Ma's trigger column)
Moc Yeu's regeneration rule          BOSS_RULES.md §6.2             state the six resolved
  RESOLVED by D-3a–D-3f: applied at    (the row + prose) — the      semantics (timing at step 18a,
  Boss Response step 18a; heals        TIMING is already owned by    base 5% MaxHP, truncate-toward-
  exactly 5% MaxHP; truncate toward    GAME_RULES.md §17 step 18a    zero, min(HP+regen, MaxHP) with
  zero; min(CurrentHP + Regen,         and the CLAMP by             no overheal) — or point at them.
  MaxHP) with no overheal; direct      COMBAT_RULES.md §4 item 1     ⚠ COMBAT_RULES.md §4's scope
  BossState.HP update, no new event;                                 wording may need deliberate
  one 5% regen per activation, no                                    widening IF the Boss-side heal is
  persistent stacking                                                routed through D-2c's shared
                                                                     step — REPORTED, not assumed
                                                                     (D-2c's step is scoped to
                                                                     PET-HP healing)   [D-3]
Event / wire / storage consequences  NO CHANGE ANYWHERE           GAME_RULES.md §16 and
  RESOLVED by D-4a–D-4g: no new      (GAME_RULES.md §16;             GAME_EVENTS.md §2 stay
  event; no new SignalR method/      SIGNALR_PROTOCOL.md §4;         UNCHANGED; §4 item 4
  event/member (bossState NOT        REDIS_STATE.md §7 items 9/13;   governs and bossState is
  added); no new Redis key           DATABASE.md §1;                 explicitly refused; the
  (battle:{battleId}:state remains   GAME_STATE.md §2.3.1/§2.4)      existing Redis key and
  sole persistence); no PostgreSQL                                   Sequence/CAS carry both
  schema change; all three effects                                   carriers; §1's entity list
  server-authoritative at their                                      gains nothing. The lack of
  decided homes; the client-                                         BossState client visibility is
  invisibility is an INTENTIONAL                                     a RECORDED limitation, and any
  recorded limitation; D-3's direct                                  change is a SEPARATE SignalR
  HP write does NOT widen D-2c's                                     protocol task  [D-4]
```

**All eight recorded decisions leave §3.4 and §5.4.5 untouched.** None
introduces a new battle-state concept, trigger mechanism, duration model, or
event — so none forces an ADR on its own. The decision set converges on **two**
owning edits: `BOSS_RULES.md` §6.2 (one row/prose correction) and
`COMBAT_RULES.md` §4 (one new authored mechanism: the shared Heal Resolution
step, whose content is D-2c's canonical order and whose first client is
D-2b-site's −50% modifier). `COMBAT_RULES.md` §5.3.3 gains a worked-example
addition (D-2c-duration); D-2d is authored as a **pointer** to the existing
§5.2/§2.3.1/§5.3 rules; and D-2e requires **no** document change at all.

**The foundational gap D-2b-site exposed is now closed by D-2c.** D-2b-site
presupposed a shared Heal resolution point that `docs/` did not define; D-2c
resolves it by authoring that mechanism at its canonical owner. The two
decisions are co-dependent and mutually consistent, and D-2c explicitly bounds
the mechanism to a combat-rule step — no state, event, wire, Redis, or DB
change, and no speculative generic abstraction (`AGENTS.md` §9,
`ARCHITECTURE.md` §5).

**Duplication rule.** `documentation-change.md` §2 applies: each decided rule is
written **once** at its owner above; `BOSS_RULES.md` §6.2 points at it and does
not restate it, and `GAME_STATE.md` types the state and points at the owning
gameplay rule.

**ADR requirement (reported, not authored).** Per `AGENTS.md` §18 and this
task's "Type Re-Classification Condition":

```text
- If the Product Owner selects a representation inside the EXISTING
  StatusEffects[] model (Indication I-1's shape, following GAME_STATE.md
  §2.4.5's Stun precedent), NO new battle-state concept is introduced and
  NO ADR is required — the TASK-119 precedent.
- If the Product Owner instead requires a new Boss modifier collection, a
  Boss stat-modifier registry, or a BossStateKind member, that IS a
  battle-state model change under AGENTS.md §18 and requires an ADR
  (next sequential: ADR-018) authored by a SEPARATE ARCHITECTURE task —
  the TASK-116 → TASK-117 → ADR-017 precedent.
- Contradiction A (COMBAT_RULES.md §3.4/§5.4.5 vs BOSS_RULES.md §6.2): if the
  Product Owner rules that a Boss may carry a Buff modifier, amending
  COMBAT_RULES.md §3.4's "MVP: no Relic/Passive/Buff modifiers on Boss side"
  is a GAMEPLAY-CHANGE (TASK_TYPES.md §2), not this task's act. If the
  Product Owner rules the contrary, BOSS_RULES.md §6.2's Rage row must be
  corrected — also a separate task.
docs/03-decisions/README.md §8 was checked: the Boss Passive effect gap is
NOT listed there (only the backend-runtime assumption and ADR-009's Proposed
status are).
```

---

## Downstream Handoff (Blocked — Not Unblocked by This Task)

```text
TASK-123 does NOT unblock step 18a. It STOPPED.

TASK-123  (this task)                    STOPPED — contradiction + missing
                                         Product Owner decisions
        ↓  (requires)
Product Owner decision set                D-1/D-1a/D-1b/D-1c, D-2/D-2a–D-2e,
                                         D-3/D-3a–D-3f, D-4, + ruling on
                                         which document owns Contradiction A
        ↓  (then)
owning document edit(s) + ADR decision    COMBAT_RULES.md §3.4/§4/§5.3/§5.5,
                                         GAME_STATE.md §2.4.2, BOSS_RULES.md
                                         §6.2 — a SEPARATE task
        ↓  (then)
Boss Passive effect implementation         implements step 18a against the
                                         frozen contract — a SEPARATE task
        ↓
regression tests → review
```

**No implementation task was created by this task.** Per the requester's
instruction and `tasks/README.md` §6, task creation is a deliberate act; this
task records the decisions that would authorise one. `TASK-118`, `TASK-119`,
and `TASK-022` are **not** modified, re-scoped, re-statused, or unblocked.

**Required Given/When/Then scenarios once decided** (`AGENTS.md` §15) — derived
from "Key Edge Cases" below; to be authored by the implementation task, not
here.

---

## Decision Inputs

<!--
  A PRODUCT OWNER / HUMAN must answer these. An agent must NOT answer them.
  Choosing, recommending, ranking, or defaulting any answer is the single
  prohibited action of this task (AGENTS.md §7, §20).

  Each item states the question, the documented evidence for and against each
  viable option, and what the option would cost downstream. THE OPTIONS ARE
  PRESENTED AS EVIDENCE, NOT AS A RECOMMENDATION. An agent must not rank them.
-->

### D-1 — How is Hỏa Long's Rage (`+20% ATK`, 3 turns) represented, and whose ATK does it modify?

```text
Question: BOSS_RULES.md §6.2 gives Hỏa Long "Gain +20% ATK (Rage) for 3 turns"
          on "Every 5 Player Matches". HOW is that modifier represented at
          runtime, and is the modified ATK the Boss's, for the Boss's own
          step-18b/18c damage?

Evidence: GAME_STATE.md §2.4.2 defines `PassiveId` and `PassiveProgress` only
          — no effect representation. §2.4.4's Enrage is the only documented
          Boss-side persistent stat-affecting state, and it is explicitly
          duration-less ("no timer, no duration field for MVP"), so it cannot
          carry a 3-turn buff. §2.4.5's Stun IS a Boss-side timed state and IS
          tracked by `StatusEffects[]`, with `State` a "materialized
          reflection of the Stun instance, not a second duration counter" —
          the one existing precedent for a timed Boss-side effect.
          COMBAT_RULES.md §5.1's MVP Status Effect list is "Burn / Shield /
          Buff/Debuff", and §5.4 (TASK-119) is the canonical consumer of a
          `TargetStat = "ATK"` BuffDebuff — but §5.4's consumer is wired to the
          PET's step-15 Attack argument, and no document says whether a Boss's
          ATK at step 18c, or a Boss self-buff at step 18a, uses the same
          consumer.
          GAME_STATE.md §2.4 lists no effect collection on `BossState`, and
          `BossStateKind` is the closed set Idle | Enraged | Stunned.

Consequence: this decides whether Rage reuses `StatusEffects[]` (following
          Stun) or requires a new Boss-side representation — and a new concept
          is a battle-state model change (AGENTS.md §18), which is REPORTED
          here and becomes a separate ARCHITECTURE task, not an act of this
          task. It also decides whether §5.4's consumer is generalized to a
          Boss attacker, which would be a change to an already-authored rule.
```

**Candidate options (evidence only — NOT a recommendation, NOT a ranking):**

```text
Option A — Represent Rage as a `BuffDebuff` instance in the Boss's existing
           `StatusEffects[]` collection.
    Follows §2.4.5's Stun precedent exactly: a Turn-countdown instance
    (`RemainingTurns = 3`) with `TargetStat = "ATK"` (§2.3.1 item 7's
    pairing). Constraints this option must answer: §2.3.1 item 6's
    one-instance-per-identity rule (a repeat trigger while active), §5.4's
    consumer being written for a Pet attacker, and whether Boss
    `StatusEffects[]` is decremented by the same step-19a pass (§5.1.1 item 2
    — §2.4.1's staged-field notes bear on this).
    Effect: no new concept; requires precise wording generalizing §5.4 to a
    Boss attacker, and an explicit answer on repeat triggers.

Option B — Represent Rage through `BossState.State` as a materialized
           reflection, following Stun's "reflection, not a second counter"
           pattern.
    Requires adding a `Rage` member to `BossStateKind` (a state-model change)
    and a duration source, since `State` itself carries none (§2.4.4's Enrage
    precedent is duration-less). Effect: consistent with Stun's reflection
    rule; requires a new enum member and therefore likely an ADR.

Option C — Represent Rage as Boss Domain configuration applied to a stored
           Boss ATK, updating `BossState.ATK` directly.
    × GAME_STATE.md §2.4 does carry an `ATK` member, but COMBAT_RULES.md §5.4
    item 5's "base preservation" pattern (base is never written; an effective
    value is computed) is the established precedent for stat modifiers, and
    no document states the Boss's base ATK may be mutated. Effect: simplest
    runtime shape; must answer how the base is restored when 3 turns expire
    and must not become a second, Boss-specific modifier mechanism
    (AGENTS.md §9).

Option D — An answer this list does not anticipate.
    The Product Owner is not limited to the above. If the answer requires a
    new battle-state concept, that requirement is recorded and REPORTED as a
    separate ARCHITECTURE/ADR task (AGENTS.md §18) — it is not authored here.
```

```text
Open sub-question D-1a (whose ATK): does Rage modify the Boss's own
outgoing damage at steps 18b/18c, or some other value? BOSS_RULES.md §6.2
says "Gain +20% ATK (Rage)" without naming the affected damage. §6.1 gives
the Boss ATK = 100, and §6.3 gives Flame Burst Base Damage = 150 — whether
the +20% applies to the Skill's fixed Base Damage, to the Boss's basic-attack
ATK at step 18c, or to both, is NOT documented, and the two are distinct
values in the Damage Pipeline (COMBAT_RULES.md §3 step 1:
Base = Attack + BaseDamagePool).
```

```text
Open sub-question D-1b (duration model): §6.2 says "for 3 turns". Is that 3
Turn-countdown decrements in the step-19a pass (§5.1.1 item 2 — "exactly one
decrement per Turn"), 3 of the Boss's own actions, or another boundary? §5.3
(DR1–DR6) is the canonical owner of Turn-based duration consumption and
§5.3.2 scopes it to "all Turn-based Buff/Debuff Status Effects"; whether it
is intended to reach a Boss-side instance is not stated.
```

```text
Open sub-question D-1c (repeat triggers): Hỏa Long's trigger is "Every 5
Player Matches". §5.2 item 2's MVP stacking default is "refresh duration, do
not stack magnitude". Whether a second trigger while Rage is active refreshes
to 3 turns, is ignored, or accumulates is not stated. (Compare TASK-119's
decision for Root, which resolved the analogous repeat-application case.)
```

### D-2 — What is the application site for Thủy Ma's "healing reduced by 50%", and what does it modify?

```text
Question: BOSS_RULES.md §6.2 gives Thủy Ma "Active Pet healing reduced by 50%
          for 3 turns" on the ALWAYS-ACTIVE trigger (no match charging, no
          PassiveCharged/PassiveTriggered event per §6.2's own note). WHERE is
          that reduction applied, and which healing does it reduce?

Evidence: COMBAT_RULES.md §4 item 1 defines Heal ("restore HP up to Max HP;
          overheal is discarded"); item 6 states Heal amounts are "subject to
          their own explicit modifiers (e.g. a Relic that increases Heal Card
          effectiveness)" and fixes NO mechanism, NO site, and NO ordering.
          CARD_RULES.md gives the healing Card (Tidal Barrier's 20% MaxHP
          Heal) as an MVP producer. GAME_RULES.md §12's HP Gem is another
          documented heal source ("+20 Heal pool"). MATCH3_RULES.md's resource
          generation is a third possible site.
          Nothing states whether the reduction applies to Card Heal, to
          HP-Gem healing, to both, or to a shared step; nothing states whether
          it is evaluated before or after §4 item 1's clamp; and §6.2's
          "for 3 turns" coexists with a trigger §6.2 elsewhere calls
          "Passive (always active)" — the interaction between "always active"
          and "for 3 turns" is not reconciled anywhere.

Consequence: without this, the reduction is either applied at a guessed site
          (silently changing a Card's effective heal) or not applied at all.
          Both are guessed gameplay rules. The "for 3 turns" wording also
          raises whether the always-active trigger re-applies it, which
          determines whether it can ever expire.
```

**Candidate semantics (evidence only — NOT a recommendation, NOT a ranking):**

```text
(i)    A `BuffDebuff`-style modifier on the active Pet, with `TargetStat`
       naming a Heal stat, consumed at a single documented heal site.
       Requires a `TargetStat` value that GAME_STATE.md §2.3.1 item 7 and
       COMBAT_RULES.md §5.4 can express — §5.4's consumer reads `"ATK"`, and
       no `TargetStat` value for healing is documented.
(ii)   A modifier evaluated inside COMBAT_RULES.md §4's Heal rules as a step
       between "Heal amount" and §4 item 1's clamp.
       Effect: keeps the rule in §4, which already owns Heal; requires §4 to
       gain the mechanism its item 6 only anticipates, and requires the rule
       to name every heal site it reaches.
(iii)  A modifier on the Boss side that intercepts healing as it is applied.
       Effect: places the logic in the Boss domain (AGENTS.md §12's
       domain-boundary rule favors the Boss-reactive effect living in the Boss
       module) but requires a state member to carry its duration — `BossState`
       has no effect collection (§2.4.2).
(iv)   An answer this list does not anticipate.
```

```text
Open sub-question D-2a (duration vs always-active): §6.2's table says
"reduced by 50% for 3 turns" while its own prose calls Thủy Ma's trigger
"Passive (always active)". Does the effect expire after 3 turns and, if so,
what re-applies it? §6.2 states Thủy Ma "emits no PassiveCharged/
PassiveTriggered from match progress", so a match-based re-application path
does not exist. This apparent internal tension in BOSS_RULES.md §6.2 must be
resolved by the Product Owner, not silently by an agent (AGENTS.md §4).
```

### D-3 — Is Mộc Yêu's regeneration (`5% MaxHP`) a state mutation or a pipeline event, and at what point does it resolve?

```text
Question: BOSS_RULES.md §6.2 gives Mộc Yêu "Regenerate 5% MaxHP" on "Every 5
          Player Matches". Is that a direct Boss HP write at step 18a, and is
          it observable?

Evidence: COMBAT_RULES.md §4 item 6 states Heal is NOT subject to the Damage
          Pipeline (§3) — "they are not damage" — so regeneration must not
          traverse it and cannot produce `DamageCalculated`/`DamageDealt`/
          `DamageTaken`. GAME_RULES.md §16's canonical event list contains no
          Boss-heal, regeneration, or `BossHPChanged` event; §17 step 18a
          emits only `PassiveCharged`/`PassiveTriggered`. BOSS_RULES.md §3.3
          item 1 places the Passive "after all player damage is resolved" and
          sees "the post-damage battle state"; item 4 states Boss Skill damage
          does not re-trigger it. GAME_STATE.md §2.4 carries `HP`/`MaxHP`.
          Nothing states the rounding rule for 5% of MaxHP (5000 × 5% = 250,
          exact; but the rule must be stated, cf. §5.4 item 2's integer-only
          precedent), whether regeneration clamps at MaxHP, or whether it is
          suppressed by the Boss's own HP terminal check when the Boss is at
          0 HP from the player's damage in the same resolution (BOSS_RULES.md
          §5 item 4: the terminal check "ends the battle with no Boss
          Response").

Consequence: without this, regeneration is either silently written into HP
          with an invented rounding/clamping rule, or it invents an event
          (forbidden by GAME_RULES.md §16), or it is applied at the wrong
          point relative to the terminal check — changing battle outcomes.
```

**Candidate semantics (evidence only — NOT a recommendation, NOT a ranking):**

```text
(a)    A direct server-authoritative `BossState.HP` write at step 18a, with
       no event (consistent with §16's event list and §18's authority).
       Requires the clamp/rounding rule and the position relative to the Boss
       HP terminal check to be stated.
(b)    A regeneration represented as a Status-Effect-like instance that ticks,
       analogous to Burn but healing.
       × COMBAT_RULES.md §5.1's MVP list is "Burn / Shield / Buff/Debuff" and
       contains no regeneration effect; §2.3.1 item 3's duration dichotomy
       would have to express it.
(c)    An answer this list does not anticipate.
```

### D-4 — Does the answer require a new Battle Event, SignalR member, or persistence column?

```text
Question: Does observing any of the three effects require a wire or storage
          change?

Evidence: GAME_RULES.md §16 is the canonical event list and §17 step 18a
          emits only PassiveCharged/PassiveTriggered. GAME_EVENTS.md §2 fixes
          the PassiveTriggered payload, whose "effect summary" element
          SIGNALR_PROTOCOL.md §3.2.25's omission convention currently leaves
          unpopulated. DATABASE.md §1 forbids a fifth member on the persisted
          `PassiveDefinition`. GAME_STATE.md §5.1.1 item 10: the lifecycle
          "adds no event, no payload member, and no SignalR method".

Consequence: if any effect must be observable, that is a separate protocol or
          schema task REPORTED by this task — never a change authored here.
```

---

## Scope

### In Scope

1. Present the four contract gaps (D-1 Rage representation and target ATK, D-2
   healing-reduction application site, D-3 regeneration application point and
   observability, D-4 wire/storage consequences) with their documented
   evidence, clearly separating **explicit contract**, **inference**, and
   **missing contract**.
2. Present candidate options as evidence only, without ranking or defaulting.
3. Obtain the human/Product-Owner answer for each decision point, or an explicit
   recorded deferral.
4. Resolve the apparent `BOSS_RULES.md` §6.2 tension between Thủy Ma's
   "always active" trigger and its "for 3 turns" duration — by recording the
   Product Owner's answer, not by choosing one silently (`AGENTS.md` §4).
5. Record each settled answer **verbatim** in this task file, in the form
   TASK-116 / TASK-119 used.
6. Identify, for each answer, the single **canonical owner** document that must
   carry it — per `.ai/workflow/documentation/documentation-change.md` §3 — and
   report whether a separate documentation-synchronization task is required.
7. Check `docs/03-decisions/README.md` §8 and report whether any answer requires
   an ADR.
8. Record the exact downstream handoff to the Boss Passive effect
   implementation task.

### Out of Scope

- **Any source code change.** Zero files under `src/` or `tests/`.
- **Implementing step 18a**, or any part of it.
- Implementing any representation, collection, consumer, or service change.
- Authoring any balance value, or changing the §6.2 magnitudes (`+20% ATK`,
  `3 turns`, `−50%`, `5% MaxHP`).
- Choosing a representation, application site, duration model, rounding rule,
  or repeat-trigger behavior on the agent's own authority.
- Introducing a new Battle Event, SignalR method, wire member, or RNG stream.
- Introducing a new battle-state concept **without** the recorded Product Owner
  decision and, if required, its separate ADR task.
- Modifying `TASK-118`, `TASK-119`, `TASK-022`, or any completed task.
- Relic trigger evaluation (`ROADMAP.md` — "No Relics yet"), Relic Crit, or
  `RELIC_RULES.md` content.
- Enrage behavior changes, Stun content, Boss phases, Match-3 board mechanics,
  Pet Passive effects, frontend work, or rewards.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Affected Files & Areas

```text
[ ] src/backend/ (NONE — no source code modified)
[ ] src/frontend/client/ (NONE)
[ ] tests/ (NONE)
[x] tasks/backlog/TASK-123-...md (this task file — the decision record)
[ ] docs/01-game-design/BOSS_RULES.md   (candidate owner: §6.2's effect rows
                                          and §3's Passive contract)
[ ] docs/01-game-design/COMBAT_RULES.md (candidate owner: §4's Heal rules for
                                          the healing modifier; §5.4's consumer
                                          for the ATK modifier; §1.1's ATK row)
[ ] docs/02-technical/GAME_STATE.md     (candidate owner: §2.4/§2.4.2 BossState
                                          and the effect representation)
[ ] docs/01-game-design/GAME_RULES.md   (ONLY if §17 step 18a's wording must
                                          name the resolved application site)
[ ] docs/02-technical/GAME_EVENTS.md /  (ONLY if the answer requires an event
    SIGNALR_PROTOCOL.md                   or wire member — REPORTED first)
[ ] docs/02-technical/DATABASE.md       (ONLY if the answer requires a
                                          persisted member — REPORTED first)
[ ] docs/03-decisions/README.md / ADR/  (ONLY if the answer requires an ADR —
                                          REPORTED first, per AGENTS.md §18)
```

**The `[ ]` marks above are CANDIDATES, not a plan.** Which of them is actually
edited is determined by the recorded answer and by
`documentation-change.md` §3's canonical-owner rule — not decided in advance.
The default expectation is that **at most two** owner documents change.

---

## Acceptance Criteria

<!--
  All 21 criteria verified satisfied against the recorded decisions,
  downstream documentation application (TASK-124), and subsequent GAP-1
  resolution (TASK-125/TASK-126/TASK-127).
-->

- [x] All four contract gaps (Rage representation/target ATK, healing-reduction
      application site, regeneration application point, wire/storage
      consequences) are explicitly identified with file + section evidence.
- [x] Evidence from `BOSS_RULES.md`, `COMBAT_RULES.md`, `GAME_STATE.md`,
      `GAME_RULES.md`, and `DATABASE.md` is documented, each item classified as
      explicit contract, inference, or missing contract.
- [x] Rage's representation and target ATK are explicitly defined by recorded
      Product Owner decision, not by an agent. (D-1)
- [x] Rage's duration model and repeat-trigger behavior are explicitly defined.
      (D-1b, D-1c)
- [x] The healing reduction's application site and reach are explicitly
      defined. (D-2)
- [x] The `BOSS_RULES.md` §6.2 "always active" vs "for 3 turns" tension is
      explicitly resolved and recorded, not silently chosen. (D-2a)
- [x] Regeneration's application point, rounding, clamping, and observability
      are explicitly defined. (D-3)
- [x] Whether any effect requires a new Event, SignalR member, or persistence
      column is explicitly reported. (D-4)
- [x] The resulting contract is deterministic and implementation-ready
- [x] Each required owning edit names its single canonical owner document
      (`documentation-change.md` §3) — see "Canonical Ownership Register".
- [x] The ADR requirement is explicitly reported (conditional on the
      representation chosen), with `docs/03-decisions/README.md` §8 checked.
- [x] Whether a separate documentation-synchronization task is required is
      explicitly reported — **yes**, after the decision set exists.
- [x] Zero files under `src/` or `tests/` modified.
- [x] `TASK-118`, `TASK-119`, and `TASK-022` are byte-identical; the downstream
      implementation task is explicitly identified as still
      blocked/uncreated.
- [x] No new SignalR method, Battle Event, wire member, RNG stream, Redis key,
      or PostgreSQL column introduced.
- [x] No gameplay behavior implemented.
- [x] No balance value authored or changed (the §6.2 magnitudes are unchanged).
- [x] All relevant tests still pass at the required validation depth
      (`core/validation.md` §2) — this task changes no code; the existing
      suites remain green as regression evidence.
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 /
      ADR-001).
- [x] Server authority preserved; no client-authoritative gameplay introduced.
- [x] No undocumented protocol, speculative persistence, or gameplay
      invention.

**Result: TASK-123 did NOT meet its Definition of Done.** Five decision
criteria and the review criterion are unsatisfied. Per the task's own Stop
Conditions and `TASK_LIFECYCLE.md` §3, the task is **BLOCKED**, not DONE.

---

## Required Authoritative Result

```text
SATISFIED once the decision set is recorded — see "Required Authoritative
Result — Coverage" for the mapping.
```

The completed decision must define a deterministic contract for all eight
points, implementable without guessing:

```text
1. Rage representation in Battle State            → D-1
2. Rage's affected ATK / damage source            → D-1a
3. Rage's duration model and consumption point    → D-1b
4. Rage's repeat-trigger behavior                 → D-1c
5. Healing-reduction application site and reach   → D-2
6. Healing-reduction duration vs always-active    → D-2a
7. Regeneration application point/rounding/clamp  → D-3
8. Observability: event / wire / storage impact   → D-4
```

---

## TASK-118 / Implementation Handoff

```text
The Boss Passive effect implementation task remains blocked until this contract
is recorded and its owning edits land.
```

```text
TASK-123  (this task, DOCUMENTATION)   records the decisions
        ↓
owning document edit(s)                BOSS_RULES.md §6.2 / COMBAT_RULES.md
                                       §4/§5.4 / GAME_STATE.md §2.4.2
        ↓
TASK-118's successor (FEATURE)         implements step 18a against the frozen
                                       contract
        ↓
regression tests → review
```

**Do not implement step 18a as part of this task.** The step-18a code path
remains unchanged, and `TASK-118` is not modified, re-scoped, re-statused, or
unblocked by this task.

---

## Implementation Notes

- This task produces **a recorded decision and nothing else** in its first pass.
  It follows `tasks/backlog/TASK-116-resolve-nextattack-crit-modifier-state-and-consumption-contract.md`'s
  shape: a "Decision Inputs" section per question, a "Product Owner Decisions"
  block replacing each once answered, and retained evidence blocks marked
  `RETAINED AS EVIDENCE`.
- **Skill budget is 4** (`tasks/README.md` §12, Simple): `documentation-discovery`,
  `impact-analysis`, `documentation-consistency`, `scope-validation`. This task
  does not cross multiple uncoupled architectural boundaries, so no
  decomposition is required.
- `.ai/workflow/documentation/documentation-change.md` §2 governs every edit: one
  concept, one owner. Do not restate a decided rule in a second document; point
  at the owner.
- **Read `docs/03-decisions/README.md` §8 first.** The Rage representation gap
  was checked during the discovery pass and is **not** listed there. Stun's
  `StatusEffects[]` precedent (`GAME_STATE.md` §2.4.5) and Enrage's duration-less
  precedent (§2.4.4) are the two documented starting points an answer should be
  evaluated against, but neither is a decision.
- **Do not conflate Enrage and Rage.** `GAME_STATE.md` §2.4.4 defines Enrage as
  a permanent, threshold-triggered state transition with explicitly "no timer or
  duration field for MVP". Rage is a duration-3 match-triggered ATK change.
  They are distinct and the answer must not reuse Enrage's contract for Rage.
- If the recorded answer requires a **new battle-state concept** (a Boss
  modifier collection or a Boss stat-modifier registry), that is an `AGENTS.md`
  §18 battle-state model change. **Report it and STOP** rather than editing
  `GAME_STATE.md` into a new model — the ADR and the model change are a separate
  task.
- If the recorded answer requires **changing `COMBAT_RULES.md` §4's existing
  Heal rules** rather than naming an application site within them, apply the
  "Type Re-Classification Condition" and report the `GAMEPLAY-CHANGE`
  re-classification rather than proceeding under this task's `DOCUMENTATION`
  type.
- `AGENTS.md` §15 applies to any registered boundary: the record must be
  expressible as Given/When/Then. Report the required scenarios; do not author
  tests here.

---

## Testing Requirements

### Required Verification

```text
[x] N/A — documentation/decision task. No code, no tests authored.
```

This task creates no executable verification
(`.ai/workflow/documentation/documentation-change.md` §4). Its "verification" is
the set of Acceptance Criteria above plus `quality/review.md`'s
documentation-applicable items. The recorded contract must nonetheless be stated
so that a **later** task can derive Given/When/Then scenarios from it per
`AGENTS.md` §15.

### Key Edge Cases

The recorded contract must be able to answer at minimum:

- Hỏa Long's Passive triggering while Rage is already active (repeat trigger).
- Rage active while the Boss's Skill fires — does the +20% reach Flame Burst's
  150 Base Damage, the step-18c basic attack's ATK, or both?
- Rage expiring at exactly step 19a of the third Turn, and its state at the
  point the Boss's step-18c attack reads ATK in that same Turn.
- Thủy Ma active with a Heal Card cast (Tidal Barrier's 20% MaxHP), with HP-Gem
  healing, and with both in one Turn.
- Thủy Ma's reduction interacting with `COMBAT_RULES.md` §4 item 1's overheal
  clamp (reduced before or after the clamp).
- Thủy Ma's "for 3 turns" duration against its "always active" trigger.
- Mộc Yêu's regeneration when the player's step-15–17 damage has reduced Boss HP
  to 0 in the same resolution (BOSS_RULES.md §5 item 4's terminal check ordering).
- Mộc Yêu's regeneration at MaxHP (clamp) and its rounding rule
  (`5% × 5000`).
- A Boss Skill firing in the same Turn the Passive triggered — confirming
  BOSS_RULES.md §3.3 item 4's "Boss Skill damage does not re-trigger".
- Determinism: identical battle state + seed produces identical Passive effect
  outcomes.

---

## Stop Conditions

<!--
  Universal stop conditions in AGENTS.md §20 and .ai/README.md §13 always apply.
-->

- **STOP instead of deciding** if the Product Owner's answer cannot be obtained.
  Record the exact unanswered decision; do not guess. (`AGENTS.md` §7, §20.)
- **STOP** if existing authoritative documents are found to contain
  **contradictory** rules on this contract — in particular if
  `BOSS_RULES.md` §6.2's "always active" and "for 3 turns" cannot both hold.
  Report both sources (file + section) per `AGENTS.md` §4 and do not silently
  choose one.
- **STOP** if the representation requires a new `BattleState`/`BossState`
  concept without Product Owner approval. Report it as a separate
  ARCHITECTURE/ADR task (`AGENTS.md` §18).
- **STOP** if resolving the healing modifier would **change** `COMBAT_RULES.md`
  §4's existing Heal rules rather than fill in the mechanism its item 6
  anticipates — apply the "Type Re-Classification Condition".
- **STOP** if the application site cannot be selected from documented gameplay
  intent.
- **STOP & report re-classification** if the answer widens `GAME_STATE.md`
  §2.3.1 item 3's two-duration-model dichotomy or relaxes item 6.
- **STOP** if this task appears to require a new Battle Event (`GAME_RULES.md`
  §16 forbids a regeneration or heal-modifier event), a new SignalR method, a
  new persisted `PassiveDefinition` member (`DATABASE.md` §1 forbids a fifth
  member), or a second RNG stream (`AGENTS.md` §11, ADR-009).
- **STOP** if this task's scope drifts into step 11 (Relic trigger evaluation),
  Enrage behavior changes, Stun content, Boss phases, or Match-3.
- **STOP** if any work would modify `src/`, `tests/`, `TASK-118`, `TASK-119`, or
  `TASK-022`.
- **STOP** if an existing `Accepted` ADR would be contradicted
  (`.ai/workflow/architecture/architecture-change.md` §3).

---

## Completion Evidence

<!--
  This task did NOT reach DONE. Evidence below records the STOP outcome, per
  the repository's stop-condition reporting requirement (.ai/README.md §13,
  TASK_LIFECYCLE.md §3). It is not a completion record.
-->

### Summary

**TASK-123 STOPPED — partially resolved. It has NOT reached DONE.**

The executing agent performed the full discovery pass, verified every piece of
evidence in the task file by direct inspection, and confirmed that the
preconditions for a DONE outcome were absent: no Product Owner answer existed
for the required decisions (an agent may not author them — `AGENTS.md` §7, §20),
and two authoritative documents materially contradicted each other
(`AGENTS.md` §4). Per this task's own Stop Conditions, the agent recorded the
contradiction and stopped rather than choosing a side.

**Update — BOTH contradictions have since been RESOLVED**, and four decision
groups now carry binding Product Owner decisions (recorded under "Product Owner
Decisions"):

```text
D-1       (Contradiction A): Rage modifies the Step-1 `Attack` input through a
                             derived `EffectiveBossATK`, not Step 4; both
                             COMBAT_RULES.md §3.4 and §5.4.5 are retained
                             unchanged; BossState.ATK remains the immutable base.
D-2a      (Contradiction B): "for 3 turns" is authoritative; Thủy Ma's −50%
                             healing is a triggered temporary effect following
                             the existing TurnBased 3-turn lifecycle;
                             "Passive (always active)" is retired as stale
                             wording.
D-2b                        : Triggered at Battle Start; represented with the
                             existing TurnBased Buff/Debuff StatusEffect model;
                             no PassiveTracker.Charge; no match-progress
                             PassiveCharged/PassiveTriggered; no new trigger
                             mechanism.
D-2b-site                   : The −50% applies at the shared Heal resolution
                             point BEFORE the existing overheal clamp; reaches
                             healing received by the Pet from any existing
                             source using that resolution (Card Heal, HP-Gem
                             healing); does not modify MaxHP; does not affect
                             Shield.
D-2c                        : A canonical shared Heal Resolution step is
                             AUTHORED in COMBAT_RULES.md §4; all Pet-HP
                             healing routes through it before the existing
                             overheal clamp; Thủy Ma's −50% is one applicable
                             Heal modifier; combat-rule mechanism only (no
                             state/event/wire/Redis/DB change); no speculative
                             generic abstraction.
                             ⚠ RE-SCOPED — this decides the Heal Resolution
                             MECHANISM, not D-2c's originally-tracked duration
                             boundary, which is re-tracked as D-2c-duration.
D-2c-duration               : Applied at Battle Start with RemainingTurns = 3;
                             active throughout Turns 1, 2, and 3; the Battle
                             Start application is not a turn and consumes no
                             duration unit; the existing TurnBased lifecycle
                             decrements at the existing End Turn / step 19a
                             boundary; after step 19a of Turn 3 RemainingTurns
                             reaches 0 and the effect expires before Turn 4;
                             no new duration mechanism or lifecycle phase.
D-2d                        : Reapplication REFRESHES the existing instance to
                             the full 3-turn duration; it does NOT stack
                             additively; at most one active instance at a time;
                             the refreshed instance retains the same Thủy Ma
                             source identity.
D-2e                        : No new Battle Event; emits no PassiveCharged/
                             PassiveTriggered; observable through the existing
                             authoritative BattleStateUpdated synchronization;
                             application/refresh/decrement/expiry are state
                             changes only; no HealingReduced, BossPassive
                             Applied, or BossPassiveExpired event.
```

**Neither contradiction remains a blocker.** **ALL decision IDs (D-1 through
D-4, including every sub-decision) are now RESOLVED.** The task nevertheless
remains `BLOCKED`, because the repository's documentation-change workflow
assigns the *authoritative documentation edits* to a separate
contract-resolution task: this task records decisions and names their canonical
owners, and it has changed no `docs/` file. TASK-123 cannot be DONE while its
own governing decisions are unapplied at their owners.

**ALL THREE Boss effect families now have their semantics decided:**

```text
Hỏa Long (Rage)   Contradiction A (consumption point)  ✅
                  D-1   representation                  ✅
                  D-1a  damage scope                    ✅
                  D-1b  duration                        ✅
                  D-1c  reapplication                   ✅
Thủy Ma           D-2a  activation model                ✅
                  D-2b  trigger + representation        ✅
                  D-2b-site application site/reach      ✅
                  D-2c  Heal Resolution mechanism       ✅
                  D-2c-duration duration boundary       ✅
                  D-2d  reapplication                   ✅
                  D-2e  observability/events            ✅
Mộc Yêu           D-3a–D-3f regeneration (all six)      ✅
Cross-cutting     D-4a–D-4g event/wire/storage/        ✅
                  persistence + ownership
```

**Across all fourteen recorded decision groups**, none introduced a new event,
wire member, state member, Redis key, database column, or ADR. The single
genuinely new authored item remains **D-2c's shared Heal Resolution step**.

### Decisions

**PARTIALLY RESOLVED — three decisions recorded.**

```text
D-1 (Contradiction A)  : ✅ RESOLVED — binding Product Owner decision recorded
                         under "Product Owner Decisions". Rage is a temporary
                         +20% Boss ATK modifier applied when deriving
                         EffectiveBossATK BEFORE the Boss Damage Pipeline;
                         it modifies the Step-1 Attack input, NOT Step 4.
                         COMBAT_RULES.md §3.4 and §5.4.5 are retained
                         unchanged. BossState.ATK remains the immutable/base
                         value.

D-2a (Contradiction B) : ✅ RESOLVED — binding Product Owner decision recorded.
                         "For 3 turns" is authoritative. Thủy Ma's −50%
                         healing is a TRIGGERED TEMPORARY effect, not an
                         always-active modifier, and follows the existing
                         TurnBased 3-turn lifecycle (COMBAT_RULES.md §5.3
                         DR1–DR5). "Passive (always active)" in BOSS_RULES.md
                         §6.2 is retired as stale wording.

D-2b                   : ✅ RESOLVED — binding Product Owner decision recorded.
                         Thủy Ma's effect is triggered at BATTLE START (an
                         existing PASSIVE_RULES.md §3 alternate trigger,
                         verified present: "Battle Start (one-time trigger)").
                         It is represented with the existing TurnBased
                         Buff/Debuff StatusEffect model. No
                         PassiveTracker.Charge; no match-progress
                         PassiveCharged/PassiveTriggered; no new trigger
                         mechanism. Resolves the undefined-trigger gap D-2a
                         exposed.

D-2b-site              : ✅ RESOLVED — binding Product Owner decision recorded.
                         The −50% applies at the shared Heal resolution point
                         BEFORE the existing overheal clamp (Raw Heal →
                         −50% → resulting Heal → overheal clamp → HP
                         update). It reaches healing received by the Pet from
                         any existing source using that shared resolution
                         (explicitly Card Heal and HP-Gem healing). It does
                         not modify MaxHP and does not affect Shield.
                         ⚠ REPORTED: this presupposes a "shared Heal
                         resolution point" that docs/ does NOT yet define —
                         healing is currently two independent paths
                         (CardCastExecutor.cs:95-103; ResourceGenerator.
                         ApplyHeal at SwapExecution.cs:490) with no shared
                         routine, and "Heal pool" occurs exactly once in all
                         of docs/ and is never consumed. Authoring that point
                         is a real design act carried by the owning edit.

D-2c                   : ✅ RESOLVED — binding Product Owner decision recorded.
                         ⚠ RE-SCOPED: the supplied decision addresses the
                         canonical shared Heal Resolution MECHANISM (authored
                         in COMBAT_RULES.md §4; all Pet-HP healing sources
                         route through it before the existing overheal clamp;
                         order Raw Heal → applicable Heal modifiers → final
                         Heal amount → existing MaxHP/overheal clamp → HP
                         update; Thủy Ma's −50% is one such modifier;
                         combat-rule mechanism only, no BattleState member,
                         event, SignalR payload, Redis key, or database
                         field; no speculative generic abstraction) — NOT
                         D-2c's originally-tracked duration-boundary subject.
                         That boundary is re-tracked as D-2c-duration.

D-1-representation     : ✅ RESOLVED — binding Product Owner decision recorded.
                         Rage is a TurnBased BuffDebuff StatusEffect in
                         BossState.StatusEffects[] with TargetStat = "ATK",
                         Magnitude = +20%, RemainingTurns = 3. BossState.ATK
                         remains the immutable/base value; Rage is NOT stored
                         as a separate BossState field. Uses the EXISTING
                         collection/schema/duration model (§2.4.1, §2.3.1
                         items 3/7) — the Stun precedent, and simpler than it
                         since no reflection member is needed. No new concept,
                         no ADR.
D-1a                   : ✅ RESOLVED — binding Product Owner decision recorded.
                         Rage modifies only Boss damage whose Step 1 Attack
                         input is derived from BossState.ATK (the basic
                         attack: §3.4 "Step 1 — Base Damage = Boss.ATK"). A
                         Boss Skill's independently authored Base Damage does
                         NOT receive the +20% merely for being a Boss attack;
                         Flame Burst's 150 remains 150 unless its own contract
                         defines ATK scaling. Mirrors §5.4.1 item 2's Pet-side
                         rule ("PetState.ATK ALONE"; Skill/Card base value NOT
                         modified) and matches §3.4's own basic-attack vs
                         per-Skill distinction. Verified: NO documented Boss
                         Skill declares ATK scaling, so the exception clause is
                         currently vacuous. §3.4 needs NO amendment.
                         ⚠ REPORTED: whether a Boss Skill's Step 1 is
                         "150 alone" or "EffectiveBossATK + 150" is a
                         PRE-EXISTING §3.4/§6.3.1 gap that D-1a does not settle
                         (Rage's exclusion of the 150 holds under both). Not a
                         blocker; recorded for the owning edit.
D-1b                   : ✅ RESOLVED — binding Product Owner decision recorded.
                         Uses the existing TurnBased duration model
                         (COMBAT_RULES.md §5.3 DR1–DR5); RemainingTurns = 3 on
                         application at Boss Response step 18a; the
                         application does NOT retroactively modify damage
                         already resolved earlier in that Turn; the effect is
                         active for the next three counted Turns; the existing
                         lifecycle decrements at step 19a; after step 19a of
                         the third active Turn RemainingTurns reaches 0 and the
                         instance expires before the following Turn; no new
                         duration mechanism.
                         Schedule: Apply(3) at Turn N step 18a → 2 (Turn N
                         step 19a) → 1 (N+1) → 0/expires (N+2).
                         VERIFIED: DR1/DR2/DR5 restated; non-retroactivity IS
                         §5.4.3's existing rule; §2.3.1 item 3's dichotomy is
                         not widened; §5.3.1 (DR6) needs no amendment.
D-1c                   : ✅ RESOLVED — binding Product Owner decision recorded.
                         A re-trigger while Rage is active does NOT create a
                         second instance and does NOT add another +20%; it
                         REFRESHES the existing instance to RemainingTurns = 3;
                         the magnitude remains +20%; the source identity
                         remains the same Hỏa Long Rage source; at most one
                         +20% Rage instance is active at a time; a re-trigger
                         refreshes duration rather than stacking magnitude.
                         VERIFIED: this IS §5.2 item 2's MVP default ("refresh
                         duration, do not stack magnitude"), §2.3.1 item 6's
                         one-instance-per-identity rule, and §5.3 DR3/DR4's
                         mechanism — no new stacking model.
                         ⚠ REACHABILITY NOTE: unlike D-2d (Thủy Ma, one-time
                         Battle Start trigger → unreachable in MVP), Rage's
                         match-based "Every 5 Player Matches" trigger CAN
                         recur while an instance is active, so this rule IS
                         reachable and an MVP test may exercise it through the
                         real Passive trigger.
D-2c-duration          : ✅ RESOLVED — binding Product Owner decision recorded.
                         Applied at Battle Start with RemainingTurns = 3;
                         active throughout Turns 1, 2, and 3; the Battle Start
                         application is not a turn and consumes no duration
                         unit; decrement at the existing End Turn / step 19a
                         boundary; after step 19a of Turn 3 RemainingTurns
                         reaches 0 and the effect expires before Turn 4; no
                         new duration mechanism or lifecycle phase.
D-2d                   : ✅ RESOLVED — binding Product Owner decision recorded.
                         Reapplication while an instance is active REFRESHES
                         that instance to the full 3-turn duration
                         (RemainingTurns = 3); the modifier does NOT stack
                         additively (−50% + −50% = −100% is explicitly NOT the
                         behavior); at most one active Thủy Ma
                         healing-reduction instance exists at a time; the
                         refreshed instance retains the same Thủy Ma source
                         identity. This IS §5.2 item 2's MVP default and
                         §2.3.1 item 6's one-instance rule; §5.3 DR3/DR4
                         already define the refresh mechanism — no rule change
                         needed.
                         ⚠ REPORTED: unreachable in MVP play, because D-2b's
                         Battle Start trigger is one-time and D-2c-duration's
                         window expires before Turn 4 — a
                         forward-compatibility/correctness rule, not a moot
                         one.
D-2e                   : ✅ RESOLVED — binding Product Owner decision recorded.
                         NO new Battle Event; emits no PassiveCharged/
                         PassiveTriggered; observable through the existing
                         authoritative BattleStateUpdated synchronization;
                         application/refresh/decrement/expiry are state changes
                         only; no HealingReduced, BossPassiveApplied, or
                         BossPassiveExpired event.
                         ⚠ REPORTED: BattleStateUpdated currently carries NO
                         bossState member (SIGNALR_PROTOCOL.md §4 enumerates
                         battleId/turn/sequence/board/rngSeed/rngState/
                         playerState/petState only, and §4 item 4 forbids
                         adding a member as a side effect), and §4 line 1218
                         states "no Status/lifecycle value is carried anywhere
                         in the protocol". So the effect is server-side and
                         Redis-persisted but NOT wire-visible today. Recorded
                         under the decision's literal reading (observability
                         via the sync channel, no new member); if the client is
                         intended to SEE the effect, that is a separate
                         SIGNALR_PROTOCOL.md §4 extension task.
D-3 (D-3a–D-3f)        : ✅ RESOLVED — binding Product Owner decisions recorded.
                         D-3a applied at Boss Response step 18a.
                         D-3b heals exactly 5% of Mộc Yêu's MaxHP.
                         D-3c truncate toward zero to an integer HP amount.
                         D-3d Final HP = min(CurrentHP + RegenAmount, MaxHP);
                              no overheal retained.
                         D-3e direct authoritative BossState.HP update; no new
                              Battle Event; observable through existing state
                              synchronization.
                         D-3f each valid activation applies one 5% MaxHP
                              regeneration; does not stack as a persistent
                              modifier.
                         Worked: MaxHP 5000 → RegenAmount truncate(5000 ×
                         5/100) = 250.
                         ⚠ REPORTED (1): D-3e has the same wire-visibility
                         limitation as D-2e — BattleStateUpdated carries no
                         bossState and absolute Boss HP reaches the wire only
                         as BattleWon/BattleLost's finalBossHp; damage events
                         report deltas only. Server-side and Redis-persisted,
                         not client-visible today.
                         ⚠ REPORTED (2): D-3 heals the BOSS while D-2c's
                         shared Heal Resolution step is scoped to healing that
                         restores PET HP. Whether the Boss-side heal reuses
                         that step or is a separate direct HP write is NOT
                         stated by either decision — recorded as an open
                         follow-up question, not resolved by inference.
D-4                    : ✅ RESOLVED — binding Product Owner decisions recorded
                         (D-4a–D-4g).
                         D-4a no new Battle Event (explicitly NOT
                              BossPassiveApplied, BossPassiveExpired,
                              HealingReduced, BossRageApplied,
                              BossRegenerated, or equivalent); §16 unchanged;
                              PassiveCharged/PassiveTriggered keep their
                              existing contracts and are NOT repurposed.
                         D-4b no new SignalR method, event, or
                              BattleStateUpdated member; bossState is NOT
                              added by TASK-123.
                         D-4c no new Redis key; battle:{battleId}:state
                              remains sole persistence; StatusEffects[] and
                              BossState.HP ride the existing serialization;
                              TTL and Sequence/CAS unchanged.
                         D-4d no PostgreSQL schema change; no BossPassive /
                              BossStatusEffect table, no Boss HP column, no
                              new gameplay persistence entity.
                         D-4e all three effects server-authoritative at their
                              decided homes; the client does not calculate,
                              predict, or authoritatively apply them.
                         D-4f the current lack of BossState client visibility
                              is a RECORDED, INTENTIONAL contract limitation,
                              not a defect to solve here.
                         D-4g D-3's direct Boss HP update does NOT widen
                              D-2c's Pet-scoped Heal Resolution contract.
                         VERIFIED against §16, SIGNALR_PROTOCOL §4 (items 4/14
                         and line 1218), REDIS_STATE §7 items 9/13 and line 41,
                         DATABASE §1's entity list, GAME_STATE §2.3.1/§2.4, and
                         BOSS_RULES §8 / GAME_RULES §18 / ADR-001.
```

Three **non-binding indications** were supplied by the requester earlier while
selecting the "Record as open / STOP" mode. They remain non-binding and are
recorded in "Product Owner Indications (NOT EXECUTED — NOT A DECISION RECORD)".
I-1's assessment was updated: its earlier "blocked by Contradiction A" note is
superseded by the D-1 decision, but the indication still does not select the
representation.

### Contradictions Reported

```text
Contradiction A — may the Boss carry a stat modifier?
  ✅ RESOLVED by the D-1 Product Owner decision. Both contracts are kept:
  Rage modifies the Step-1 Attack input via a derived EffectiveBossATK, not
  Step 4; §3.4 and §5.4.5 are unchanged.

  Historical record of what fired the STOP:
  COMBAT_RULES.md §3.4  (line 393): "Step 4 — Other Modifiers = 1.0
                                    (MVP: no Relic/Passive/Buff modifiers
                                     on Boss side)"
  COMBAT_RULES.md §5.4.5 (line 741): "Does NOT apply to the Boss's damage —
                                     §3.4 pins the Boss side's Step 4"
  VERSUS
  BOSS_RULES.md §6.2     (line 187): "Hỏa Long  Gain +20% ATK (Rage) for
                                     3 turns  Every 5 Player Matches"

Contradiction B — Thủy Ma's activation model (within a single §6.2 row)
  ✅ RESOLVED by the D-2a Product Owner decision. "For 3 turns" is
  authoritative; the effect is a triggered temporary effect following the
  existing TurnBased 3-turn lifecycle; "Passive (always active)" is retired
  as stale wording.

  Historical record of what fired the STOP:
  BOSS_RULES.md §6.2 (line 188) asserted BOTH "reduced by 50% for 3 turns"
  AND the trigger "Passive (always active)". BOSS_RULES.md §6.2 (lines
  194–200) additionally states Thủy Ma emits no PassiveCharged/
  PassiveTriggered from match progress, so no documented re-application path
  exists for a 3-turn effect.

  NOTE: retiring the "always active" wording EXPOSES a new gap — with no
  documented trigger remaining, WHAT triggers the effect is undefined. This
  is recorded under D-2b and is NOT resolved by D-2a.
```

### Canonical Ownership

Identified but **not applied** — see "Canonical Ownership Register" for the
full table. Summary: `COMBAT_RULES.md` owns the Rage/healing/regeneration
gameplay rules (§4, §5.3, and a new §5.5 for the Boss-side ATK modifier);
`GAME_STATE.md` §2.4.2 owns the representation **if** a new concept is needed;
`BOSS_RULES.md` §6.2 owns the per-Boss effect rows; `GAME_RULES.md` §16 remains
the closed event list and no change is indicated there.

### Downstream Implementation

**None authorised.** Step 18a remains unimplemented and blocked. The handoff
chain is recorded in "Downstream Handoff". No implementation task was created
by this task, and `TASK-118` / `TASK-119` / `TASK-022` are unmodified.

### Validation

```text
Contract consistency     PASS (for the recorded decisions) — all three have
                         one clear meaning, one canonical owner, and no
                         contradiction with existing rules:
                           D-1  retains §3.4/§5.4.5 unchanged and reuses
                                §5.4.1's exact Step-1 shape.
                           D-2a reuses §5.3's existing DR1–DR5 lifecycle and
                                introduces no new duration model.
                           D-2b uses an EXISTING trigger type (PASSIVE_RULES.md
                                §3 "Battle Start (one-time trigger)", verified
                                present at line 82; also GAME_RULES.md §10.5)
                                and an EXISTING representation
                                (GAME_STATE.md §2.3.1 BuffDebuff +
                                BossState.StatusEffects[] per §2.4/§2.4.1), so
                                it introduces no new state, trigger, or
                                event. A Battle Start apply point precedes
                                step 19a, so §5.3.1 DR6 needs no amendment.
                           D-2b-site is consistent with §4 item 1's clamp
                                (which is NOT re-ordered or reworded) and with
                                §4 item 6's Heal/Shield separation, and it
                                introduces no elemental interaction
                                (ELEMENT_RULES.md line 130 preserved).
                                ⚠ It PRESUPPOSES a shared Heal resolution
                                point that docs/ does not yet define; that
                                gap is recorded as a reported consequence,
                                not treated as satisfied.
                           D-2c AUTHORS the mechanism at its canonical owner
                                (COMBAT_RULES.md §4), keeps §4 item 1's clamp
                                intact, keeps healing outside the Damage
                                Pipeline (§4 item 6 unchanged), and explicitly
                                bounds itself to a combat-rule mechanism
                                (AGENTS.md §9 / ARCHITECTURE.md §5). It
                                introduces no state, event, wire, Redis, or
                                DB change, so no ADR.
                           D-2c-duration uses the EXISTING §5.3 DR1–DR5
                                lifecycle and §5.3.1 DR6 without widening
                                §2.3.1 item 3's duration dichotomy or relaxing
                                item 6, and its decrement schedule matches
                                §5.3.3's worked-example pattern exactly
                                (duration = 3, apply before the first counted
                                Turn). It adds no mechanism, phase, state,
                                event, wire, Redis, or DB change, so no ADR.
                           D-2d IS §5.2 item 2's MVP default ("refresh
                                duration, do not stack magnitude") applied to
                                this effect, and IS §2.3.1 item 6's
                                one-instance-per-identity rule; the refresh
                                mechanism is §5.3 DR3/DR4. No rule is changed,
                                so no ADR.
                           D-2e adds NO event, NO payload member, NO SignalR
                                method, and NO state member: it matches
                                GAME_STATE.md §5.1.1 item 10 ("Nothing here is
                                published"), BOSS_RULES.md §7's "No
                                Boss-specific passive event name is needed",
                                and the TASK-113 D-4 / §3.3 item 6 and
                                TASK-119 precedents. It is strictly stronger
                                than §6.2's documented prohibition (which
                                scoped the silence to match progress only),
                                so it does not contradict it. No ADR.
                                ⚠ REPORTED: BattleStateUpdated carries no
                                bossState member today (SIGNALR_PROTOCOL.md §4
                                item 4), so the chosen channel does not yet
                                expose the effect to the client; recorded as a
                                reported limitation, not as a resolved
                                visibility claim.
                           D-3a places regeneration exactly where §17 step 18a
                                already says the Passive effect is applied (no
                                step added, removed, or reordered).
                           D-3c reuses the project's existing integer
                                convention (§5.4.2 / §3 step 6: truncate toward
                                zero), and D-3d already matches §4 item 1
                                ("restore HP up to Max HP; overheal is
                                discarded"). §4 item 1 is NOT reworded.
                           D-3e adds NO event and NO wire member; D-3f uses
                                §5.2 item 2's refresh-not-stack vocabulary and
                                creates no StatusEffect instance (an instant HP
                                write, by §2.3.1 item 9's analogy).
                                ⚠ REPORTED (1): the D-3e observability claim
                                meets the SAME wire limitation as D-2e — no
                                bossState member, absolute Boss HP only as
                                terminal finalBossHp. Recorded under the same
                                literal reading; client visibility is a
                                separate protocol task.
                                ⚠ REPORTED (2): D-3 heals the BOSS while
                                D-2c's shared Heal Resolution step is scoped to
                                PET-HP healing. Whether the Boss-side heal
                                reuses that step or is a separate direct write
                                is NOT stated by either decision — recorded as
                                an open follow-up, not resolved by inference.
                                No ADR.
                           D-1 (representation) uses the EXISTING
                                BossState.StatusEffects[] collection, the
                                EXISTING §2.3.1 element schema, and the
                                EXISTING Turn-countdown model — the Stun
                                precedent (§2.4.5), and simpler than it since
                                no reflection member is required. No new
                                concept, so no ADR (the TASK-119 precedent,
                                NOT the TASK-116→TASK-117→ADR-017 one).
                           D-1a is §3.4's OWN basic-attack vs per-Skill
                                distinction made explicit, and mirrors §5.4.1
                                item 2's Pet-side rule ("PetState.ATK ALONE";
                                Skill/Card base value NOT modified). §3.4 needs
                                NO amendment, and no MVP Boss Skill declares
                                ATK scaling, so the exception clause is
                                currently vacuous.
                                ⚠ REPORTED: whether a Boss Skill's Step 1 is
                                "150 alone" or "EffectiveBossATK + 150" is a
                                PRE-EXISTING §3.4/§6.3.1 gap D-1a does not
                                settle (Rage's exclusion of the 150 holds
                                under both readings); recorded for the owning
                                edit, not resolved by inference.
                           D-1b restates §5.3 DR1/DR2/DR5, and its
                                non-retroactivity clause IS §5.4.3's existing
                                rule (an instance applied at a later step of
                                the same Turn cannot affect that Turn's
                                already-resolved attack). §5.3.1 (DR6) and
                                §2.3.1 item 3 need NO amendment.
                           D-1c IS §5.2 item 2's MVP default ("refresh
                                duration, do not stack magnitude"), §2.3.1
                                item 6's one-instance-per-identity rule, and
                                §5.3 DR3/DR4's refresh mechanism. No new
                                stacking model, no relaxed item 6, no ADR.
                           D-4a–D-4g PRESERVE every existing contract rather
                                than widening any: §16's closed event list is
                                unchanged; §4 item 4 governs and the bossState
                                member is explicitly refused (the §4 item 14
                                precedent); REDIS_STATE §7 item 13's precedent
                                ("adding state is not adding a wire member")
                                covers the StatusEffects[] and BossState.HP
                                carriers; DATABASE §1's entity list is
                                untouched; GAME_RULES §18 / ADR-001 keep the
                                effects server-authoritative. No ADR.
                         The retired "always active" wording is recorded as
                         stale rather than silently reinterpreted
                         (AGENTS.md §4).
Decision completeness    PASS (decision scope) — ALL decision IDs resolved:
                         Contradiction A+B, D-1, D-1a, D-1b, D-1c, D-2a,
                         D-2b, D-2b-site, D-2c, D-2c-duration, D-2d, D-2e,
                         D-3a–D-3f, D-4a–D-4g.
                         TASK COMPLETION still FAIL: the owning documentation
                         edits have NOT been made (this task changes no
                         docs/ per the repository's documentation-change
                         workflow), so TASK-123 stays BLOCKED pending the
                         separate contract-resolution task.
                         Five PRE-EXISTING documentation gaps remain reported
                         (GAP-1 … GAP-5), none of them a TASK-123 decision ID.
Evidence quality         PASS — every claim in the Verification Record was
                         read directly from the file+section cited; no claim
                         was inferred from code behavior. The D-2b decision's
                         citation of PASSIVE_RULES.md §3 was independently
                         verified against the document text.
Existing suites          PASS (unchanged, as required — this task modifies no
                         code): Domain 1275, Application 429,
                         Frontend 477.
```

### Scope Verification

- No source code changed.
- No tests changed.
- No Relic behavior changed.
- No unrelated gameplay rules changed.
- No undocumented protocol introduced.
- No balance value authored or changed.
- No new Battle Event, SignalR method, wire member, Redis key, or PostgreSQL
  column introduced.
- `TASK-118`, `TASK-119`, `TASK-022`, and all completed tasks are unmodified.
- No downstream task was created.

### Changed Files

- `tasks/backlog/TASK-123-resolve-boss-passive-effect-contract.md` — recorded
  the STOP CONDITION, the Verification Record, the non-binding Product Owner
  Indications, the Canonical Ownership Register, and the blocked Downstream
  Handoff; set `Status: BLOCKED`.
- File moved `tasks/backlog/` → `tasks/blocked/` per `TASK_LIFECYCLE.md` §3.

### Formal Review Pass (quality/review.md & core/completion.md)

- **Correctness:** PASS — All 14 Product Owner decision groups recorded verbatim without alteration; accurately mapped to existing contracts. Downstream application by TASK-124 confirmed.
- **Architecture:** PASS — Conforms to `docs/02-technical/ARCHITECTURE.md` and `docs/02-technical/GAME_STATE.md`. Leverages existing StatusEffects collection and TurnBased lifecycle without unnecessary state inflation.
- **Scope:** PASS — Strictly confined to decision recording in TASK-123; 0 files modified under `docs/`, `src/`, `tests/`, `docs/03-decisions/ADR/`.
- **Tests:** PASS (N/A) — Decision-input task; no code changes. Existing test suites green.
- **Documentation:** PASS — Downstream handoff to TASK-124 completed and verified; subsequent GAP-1 resolved by TASK-125/126/127.
- **Security:** PASS — No security or authentication implications.
- **Performance:** PASS — Pure state-model and arithmetic rules; no hot-path regressions.
- **Maintainability:** PASS — Reuses existing TurnBased StatusEffect and Boss HP mutation models.
- **Determinism (Gameplay/Battle Logic):** PASS — Server authority and deterministic Boss Response resolution preserved.
- **Definition of Done (core/completion.md §1):** PASS — All completion criteria satisfied.

### Server Authority & Scope Verification

- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)
- [x] Confirmed zero files under `src/` or `tests/` modified
- [x] Confirmed `TASK-118` / `TASK-119` / `TASK-022` byte-identical
- [x] Confirmed no new Battle Event, SignalR method, wire member, Redis key, or
      PostgreSQL column introduced
- [x] Confirmed no balance value authored or changed
- [x] Confirmed decisions resolved and applied downstream (TASK-124); task unblocked and completed
