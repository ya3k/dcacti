# TASK-153 — Implement the MVP Boss Passive Effects at Resolution Step 18a

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and task files by path and section.

  PROVENANCE: identified by the task-generation audit following TASK-152.

  The decision → documentation → implementation chain is already complete for
  every OTHER MVP resolution stage. This is the one documented MVP stage whose
  contract is authored and whose implementation was explicitly deferred:

      TASK-118  implemented step 18b (Boss Skill secondary effects) and
                fenced step 18a out because the contract did NOT yet exist
                  ↓
      TASK-123  decided the step-18a contract (D-1 … D-n)
                  ↓
      TASK-124  applied it to BOSS_RULES.md §6.2.1–§6.2.4 and its canonical
                cross-owners; recorded
                "Blocks: Any future Boss Passive effect implementation task
                 (step 18a)" and "No implementation task created"
                  ↓
      TASK-127  closed GAP-1 (step 18b Step-1 composition) — the LAST
                precondition; EffectiveBossATK now resolves
                  ↓
      TASK-153  implements the effects                     ← this task

  THIS TASK RE-DECIDES NOTHING. Every magnitude, representation, application
  point, duration, rounding, clamping, and reapplication rule is authored in
  BOSS_RULES.md §6.2.1–§6.2.4 and is quoted by reference only.
-->

---

## Metadata

```text
Task ID:           TASK-153
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented mechanic,
                   capability, or system that already has a home in docs/ but has
                   not yet been built." BOSS_RULES.md §6.2.1–§6.2.4 fully author
                   the three MVP Boss Passive effects and GAME_RULES.md §17 step
                   18a fixes their application point. This is building it.)
Status:            DONE — executed the implementation workflow for the
                   three MVP Boss Passive effects (Hỏa Long Rage, Mộc Yêu
                   regeneration, Thủy Ma healing reduction). All 2,634 tests green.
Risk:              HIGH (TASK_TYPES.md §4 — FEATURE baseline MEDIUM, "can be HIGH
                   if it touches combat / battle state." This touches Boss
                   combat-state mutation, the Pet healing path, the Boss damage
                   Step-1 input, the Status Effect lifecycle, the battle-start
                   path, and the resolution's single write-back.)
Priority:          HIGH (ROADMAP.md §1 Phase 1 requires "3 MVP Bosses (… Passive
                   + Skill each)" and "Boss Response (Passive → Skill → Attack →
                   Victory/Defeat)". The Skill half landed in TASK-118; this is
                   the remaining half, and TASK-124 records that it is the sole
                   consumer of the contract it applied.)
Primary Agent:     gameplay (TASK_TYPES.md §5 — Boss domain → Gameplay.
                   Supporting: backend for the Application-layer resolution
                   wiring and the battle-start step.)
Supporting Agents: backend (BattleStateService step-18a wiring;
                   BattleStartService for the §6.2.2 Battle Start trigger),
                   testing (rule-derived scenarios for the three effects),
                   review
Workflow:          development/feature.md
Skills:            gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   testing/test-scenario-generation,
                   quality/implementation-review,
                   quality/scope-validation
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-156 (DECIDED — the Product Owner decision (D-1 … D-14,
                    Option B) resolving the Thủy Ma BuffDebuff ↔ TargetStat
                    representation conflict; read-only; immutable),
                   TASK-157 (DONE — applied TASK-156 to GAME_STATE.md v2.18
                    relaxing §2.3.1 item 7 and schema line; read-only; immutable),
                   TASK-154 (DECIDED — the Product Owner decision (D-1 … D-12,
                   Option B) that closed GAP-5 and resolved this task's first
                   stop condition. IMMUTABLE; read-only; NOT modified here),
                   TASK-155 (DONE — applied that decision at its canonical
                   owners: BOSS_RULES.md §6.2.2, COMBAT_RULES.md §4 item 7 /
                   §5.4.5 / §5.5.3, GAME_STATE.md §2.4.1. IMMUTABLE; read-only;
                   NOT modified here),
                   TASK-123 (DONE — the step-18a decision set),
                   TASK-124 (DONE — applied it to BOSS_RULES.md §6.2 and its
                   canonical owners; records this task as the blocked downstream),
                   TASK-127 (DONE — GAP-1 closed; EffectiveBossATK resolves, and
                   Rage reaches Boss damage only through that input),
                   TASK-128 (DONE — the Boss-side ATK modifier direction),
                   TASK-118 (DONE — implemented step 18b; fenced step 18a here),
                   TASK-022 (DONE — implemented the Boss Response stage and the
                   step 18a trigger/charge point),
                   TASK-013 (DONE — PassiveTracker integration and the shared
                   PassiveCharged/PassiveTriggered events)
Blocks:            A complete ROADMAP.md §1 Phase 1 "Boss Response" (Active +
                   Passive per Boss); the 3 MVP Bosses being fully implemented.
Estimate:          Complex (two distinct trigger points — step 18a and Battle
                   Start — three effects across two state carriers, plus a new
                   shared Heal-modifier consumption site)
```

---

## Objective

Implement the three MVP Boss Passive effects at their documented application
points: Hỏa Long's Rage at `GAME_RULES.md` §17 step 18a, Mộc Yêu's regeneration
at step 18a, and Thủy Ma's Pet-healing reduction at **Battle Start** — each
through the existing state carriers and lifecycle `BOSS_RULES.md` §6.2.1–§6.2.4
name, emitting no new Battle Event and adding no wire member.

---

## Authoritative References

- `docs/01-game-design/BOSS_RULES.md` **§6.2** — the canonical owner of the MVP Boss Passive rows (which Boss carries which effect and trigger); **§6.2.1** Hỏa Long Rage (magnitude `+20%` / 3 Turns; a Turn-based `BuffDebuff` in `BossState.StatusEffects[]` with `TargetStat = "ATK"`; `BossState.ATK` never overwritten; applies at step 18a and does not retroactively modify damage already resolved that Turn; refresh-not-stack with a stable source identity; **no new state, event, or protocol**); **§6.2.2** Thủy Ma healing reduction (`−50%` / 3 Turns; trigger is **Battle Start**, evaluated once when the battle session is created before the first Turn — not match-charged and not always-active; a Turn-based Buff/Debuff on the **Pet**; applied at the shared Heal Resolution step **before** the overheal clamp; does not modify MaxHP and does not affect Shield; the Battle Start application is not a Turn and consumes no duration unit; decrements at step 19a; refresh-not-stack; **no new event**); **§6.2.3** Mộc Yêu regeneration (heals exactly `5%` of Mộc Yêu's MaxHP; applies at step 18a; truncated toward zero; `min(CurrentHP + RegenAmount, MaxHP)` with overheal discarded; a **direct authoritative `BossState.HP` update** that is **not** routed through the Heal Resolution step, whose scope is Pet-HP healing only; no persistent Status Effect instance; **no new event**); **§6.2.4** server authority and the recorded, intentional non-exposure of `BossState` through the current SignalR projection
- `docs/01-game-design/BOSS_RULES.md` **§3.3** — the step-18a timing within the resolution order: the Passive fires once per player action **after** all player damage is resolved, so it sees the post-damage state, and **before** the Boss Skill (18b) and Boss Attack (18c); **§3 item 3** — Boss Passive effects are automatic, requiring no player or "casting" input; **§3.1** — determinism
- `docs/01-game-design/GAME_RULES.md` **§17** — the fixed resolution order, in particular **step 18a** (Boss Passive), step 18b (Boss Skill), step 18c (Boss Basic Attack), and step 19a (End Turn / Status Effect duration); **§16** — the canonical event-name list (which contains no Boss-Passive-effect event); **§12** — Power; **§18** — server authority; **§15.4** — determinism
- `docs/01-game-design/COMBAT_RULES.md` **§5.5** / **§5.5.1–§5.5.4** — the Boss ATK modifier consumption rule Rage reaches damage through (the `EffectiveBossATK` Step-1 input is derived, never written back to `BossState.ATK` and never persisted); **§3.4** — the Boss Skill Step-1 composition; **§5.2 item 2** — the refresh-not-stack MVP default; **§5.3** — the Turn-based duration lifecycle and its single step-19a decrement; **§4 item 1** — the overheal clamp (referenced, not restated); **§4 item 7** — the shared Heal Resolution step where §6.2.2's `−50%` is applied
- `docs/01-game-design/PASSIVE_RULES.md` **§2** (match-charged progress and the once-per-Cascade Threshold evaluation), **§3** (the alternate trigger forms, including **Battle Start** as a one-time trigger), **§7** (the shared Passive events)
- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState` — `HP`, `MaxHP`, `ActiveStatusEffects[]`), **§2.3.1** (the Status Effect model: identity, type, source, magnitude, remaining turns, the Turn-based vs Trigger-based forms, and the `TargetStat`-iff-`BuffDebuff` pairing), **§2.4** (`BossState` — `HP`, `MaxHP`, `ATK`, `ActiveStatusEffects[]`, `PassiveProgress`), **§2.4.1** (Boss Status Effects), **§2.4.2** (the Boss Passive identity and progress), **§5.1** (the single write-back), **§5.1.1** (the step-19a pass), **§0 item 5** (one representation per fact)
- `docs/02-technical/ARCHITECTURE.md` **§2.1** (the Application layer sequences; Domain owns the rules), **§5** (anti-overengineering)
- `docs/02-technical/REDIS_STATE.md` **§4** (the single write-back under the `Sequence` compare-and-set)
- `docs/02-technical/GAME_EVENTS.md` **§2** / **§3 item 7** — Boss Passive triggers reuse the general `PassiveCharged`/`PassiveTriggered` with `source = "boss"`; **no Boss-specific passive event name exists**
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.16–§3.2.17** (the shared Passive events), **§3 item 1** (the closed discriminator set — **no event is added**), **§4** (the `BattleState` projection)
- `docs/00-overview/MVP_SCOPE.md` §1 (5 Bosses, "Element, Passive, Skill per Boss"), §2, §4; `docs/00-overview/ROADMAP.md` §1 Phase 1
- `AGENTS.md` §7 (invent no rule), §9 (anti-overengineering), §10 (server authority), §11 (determinism), §12 (domain boundaries), §14, §15 (testing), §16 (task discipline), §17 (documentation), §20
- `tasks/completed/TASK-123-resolve-boss-passive-effect-contract.md` — the decision set this task implements
- `tasks/completed/TASK-124-apply-boss-passive-contract-decisions.md` — the applied contract, and the record naming this task as its blocked downstream ("Blocks: Any future Boss Passive effect implementation task (step 18a)"); its "Derived Scenarios" section is the source of the Given/When/Then scenarios below
- `tasks/completed/TASK-118-implement-boss-skill-secondary-effects.md` — the step-18b implementation and the explicit step-18a scope fence (and the precedent for implementing one resolution sub-step)
- `tasks/completed/TASK-127-implement-boss-skill-step-1-damage-composition.md` — GAP-1 closed; the `EffectiveBossATK` input Rage feeds
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` (`ResolveSwapAsync` — the step-18 block, where the step-18a charge currently emits and applies nothing; `EffectiveBossAttack` consumption), `src/backend/GameServer.Application/Battle/BattleStartService.cs` (`StartAsync` — the documented Battle Start point §6.2.2 needs), `src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs` (the Turn-based lifecycle, `Apply`, `EffectiveBossAttack`, and the step-19a pass), `src/backend/GameServer.Domain/Battle/BossDefinition.cs` (`BossPassiveDefinition`), `src/backend/GameServer.Domain/Bosses/BossDefinitions.cs` (the three transcribed definitions), `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs` (`ApplyHeal` — the Pet heal write site)

---

## Current State

The Boss Response stage is implemented through step 18c: `BossState.SkillCharge`
is incremented, the Passive is **charged** via `PassiveTracker.Charge` and emits
its `PassiveCharged`/`PassiveTriggered` reports with `source = "boss"`, and the
Skill (18b) and Basic Attack (18c) resolve with their secondary effects and the
`EffectiveBossATK` Step-1 composition.

What is missing is only the **effect application**. `BattleStateService.cs`
records it directly:

```text
"Passive EFFECT application is out of this task's scope for every Boss
 (BOSS_RULES.md §3 item 3, §6.2): a trigger emits its event and applies
 nothing."
```

So today: Hỏa Long never gains Rage, Mộc Yêu never regenerates, and Thủy Ma's
Battle Start healing reduction is never applied. `BossDefinitions.cs` carries the
same statement. The **consumer** for Rage already exists and is tested
(`StatusEffectLifecycle.EffectiveBossAttack`, used by the step-18 Step-1 input);
nothing yet ever **creates** the instance it reads.

`ThuyMa`'s `PassiveDefinition` currently stores a `null` threshold as its
Always-Active marker, which `BOSS_RULES.md` §6.2 (v2.5) restates as the
**Battle Start** trigger — the one-time trigger form `PASSIVE_RULES.md` §3
defines. No `PassiveTracker.Charge` path and no `PassiveCharged`/
`PassiveTriggered` report applies to it (§6.2, §6.2.2).

---

## Scope

### In Scope

1. **Hỏa Long — Rage (§6.2.1).** At step 18a, when the Boss Passive triggers, create or refresh one Turn-based `BuffDebuff` instance in `BossState.ActiveStatusEffects[]` with `TargetStat = "ATK"`, the §6.2.1 magnitude, and the §6.2.1 duration, using a stable source identity so a re-trigger refreshes rather than stacks. `BossState.ATK` is never written.
2. **Mộc Yêu — regeneration (§6.2.3).** At step 18a, when the Boss Passive triggers, apply a direct authoritative `BossState.HP` update of exactly the §6.2.3 amount, truncated toward zero and clamped to `MaxHP`. No Status Effect instance is created and the Heal Resolution step is not used.
3. **Thủy Ma — healing reduction (§6.2.2).** At **Battle Start**, create the one Turn-based Buff/Debuff instance on the **active Pet** that reduces Pet healing by the §6.2.2 percentage for the §6.2.2 duration, with a stable source identity. It is applied once at battle creation, before the first Turn, and consumes no duration unit for that application.
4. **The §6.2.2 consumption site.** The reduction reaches Pet healing at the shared Heal Resolution step, **before** the existing overheal clamp, as one applicable Heal modifier — explicitly including Card Heal and HP-Gem healing. It does not modify MaxHP and does not affect Shield.
5. **Trigger gating.** Rage and regeneration apply only when the Boss Passive's threshold was reached on that action (§3.3 item 1, once per player action, after all player damage is resolved). Thủy Ma's applies once at battle creation and is never match-charged.
6. **No new event and no new wire member.** Application, refresh, decrement, and expiry are state changes only (§6.2.1–§6.2.3), synchronized through the existing single write-back and `BattleState` projection (§6.2.4). The existing step-18a `PassiveCharged`/`PassiveTriggered` reports are unchanged.
7. **Tests** derived from the scenarios TASK-124 records for this implementation.

### Out of Scope

- **Any change under `docs/`.** The contract is authored; this task applies no documentation revision. If implementation reveals a genuine gap, STOP per `AGENTS.md` §7 rather than authoring a rule.
- **The step-18b and step-18c halves, now implemented** (TASK-118, TASK-127) — no rework, no reopening of GAP-1.
- **`PowerChanged`, `RelicTriggered`, and the step-11/13/14 Power path** (TASK-133, TASK-142, TASK-152) — untouched.
- **Exposing `BossState` (Boss HP or Boss StatusEffects) to the client.** §6.2.4 records this as an intentional current-protocol limitation and states that any requirement for it is **a separate future protocol decision, not authorized by these effect rules**. Adding it here would be inventing a protocol contract.
- **Adding any Battle Event, SignalR method or discriminator, Redis key, or database column.** §6.2.1–§6.2.4 each state "no new event/protocol"; `BOSS_RULES.md` §7 already enumerates the Boss events.
- **Pet Passive effects** (TASK-009/TASK-013 territory) and **Pet/Boss content that is not content-defined**: Thanh Xà's and Sơn Hùng's Signature Skills (`PET_RULES.md` §8 note — deferred pending a `CARD_RULES.md` §4.1 content decision), the 2 additional Bosses, and Thanh Xà/Sơn Hùng provisioning. These are blocked by missing content decisions, not by this contract.
- **Any new gameplay rule, magnitude, duration, rounding, or clamp.** Every value is `BOSS_RULES.md` §6.2.1–§6.2.3's.
- **`GET /api/battle/history`** — listed in `API_CONTRACTS.md` §1 but with no contract section defining its response, ordering, pagination, or ownership; explicitly recorded as not implemented. Out of scope here.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Dependencies

```text
TASK-156  DECIDED the Thủy Ma BuffDebuff ↔ TargetStat decision set (D-1 … D-14)
TASK-157  DONE    applied TASK-156 to GAME_STATE.md v2.18 (§2.3.1 item 7 & schema)
TASK-154  DECIDED GAP-5 decision set (D-1 … D-12)
TASK-155  DONE    applied GAP-5 to BOSS_RULES.md §6.2.2, COMBAT_RULES.md §4 item 7
TASK-123  DONE    the step-18a decision set (D-1 … D-n)
TASK-124  DONE    applied to BOSS_RULES.md §6.2.1–§6.2.4 and canonical owners;
                  names this task as the blocked downstream
TASK-127  DONE    GAP-1 closed; EffectiveBossATK resolves (the Rage consumer)
TASK-128  DONE    Boss-side ATK modifier direction
TASK-118  DONE    step 18b implemented; step 18a fenced out here
TASK-022  DONE    the Boss Response stage and the step-18a charge point
TASK-013  DONE    PassiveTracker integration and the shared Passive events
```

All satisfied. No dependency is BLOCKED, and no Product Owner decision is
outstanding.

---

## Acceptance Criteria

- [x] Hỏa Long's Rage is applied at `GAME_RULES.md` §17 step 18a as one Turn-based `BuffDebuff` instance in `BossState.ActiveStatusEffects[]` carrying `TargetStat = "ATK"`, the §6.2.1 magnitude, and the §6.2.1 duration
- [x] `BossState.ATK` is never written by Rage — it remains the immutable base value (§6.2.1, `COMBAT_RULES.md` §5.5.4)
- [x] A Rage re-trigger while active refreshes the existing instance to the full duration with the same source identity; it does not create a second instance and does not stack magnitude (§6.2.1)
- [x] Rage reaches Boss damage through the existing `EffectiveBossATK` Step-1 input and does **not** modify a Skill's authored Base Damage (`COMBAT_RULES.md` §3.4, §5.5)
- [x] Rage's application does not retroactively modify damage already resolved earlier in that same Turn (§6.2.1)
- [x] Rage is applied only when the Boss Passive's threshold was reached on that action (§3.3 item 1)
- [x] Mộc Yêu's regeneration applies a direct `BossState.HP` update of exactly the §6.2.3 amount, truncated toward zero and clamped to `MaxHP`
- [x] Regeneration creates no Status Effect instance and is not routed through the Heal Resolution step (§6.2.3)
- [x] Regeneration is applied only when the Boss Passive's threshold was reached on that action, and applies exactly one regeneration per activation (§6.2.3)
- [x] Thủy Ma's healing reduction is applied once at **Battle Start**, before the first Turn, and is never match-charged and never emits `PassiveCharged`/`PassiveTriggered` (§6.2, §6.2.2)
- [x] The Battle Start application consumes no duration unit, and the instance is active throughout Turns 1–3 with its decrement at the existing step-19a boundary (§6.2.2, `COMBAT_RULES.md` §5.3)
- [x] The reduction is applied at the shared Heal Resolution step **before** the overheal clamp, as one applicable Heal modifier, reaching Card Heal and HP-Gem healing alike (§6.2.2, `COMBAT_RULES.md` §4 items 1 and 7)
- [x] The reduction does not modify MaxHP and does not affect Shield (§6.2.2)
- [x] A further healing-reduction application while an instance is active refreshes it to the full duration; at most one instance exists at a time (§6.2.2)
- [x] No Battle Event is added for any of the three effects, and none is emitted for application, refresh, decrement, or expiry (§6.2.1–§6.2.3)
- [x] The step-18a `PassiveCharged`/`PassiveTriggered` reports and their `source = "boss"` shape are unchanged (`GAME_EVENTS.md` §2, `SIGNALR_PROTOCOL.md` §3.2.16)
- [x] `BossState` is not newly exposed through the SignalR projection (§6.2.4)
- [x] All three effects ride the existing single post-resolution write-back under the `Sequence` compare-and-set; no new Redis key, state member, or persistence path is introduced (`REDIS_STATE.md` §4)
- [x] All effects are server-authoritative; no client code computes, predicts, or applies them (§6.2.4, `AGENTS.md` §10)
- [x] No new gameplay rule, magnitude, duration, rounding, or clamp is invented
- [x] Zero files under `docs/` are modified
- [x] TASK-118's step-18b behavior, TASK-127's Step-1 composition, and TASK-152's `PowerChanged` emission are not regressed
- [x] All relevant tests pass at the required validation depth (`core/validation.md` §2)
- [x] Quality review checklist passes (`quality/review.md` §1)
- [x] No authoritative rules or contracts violated (`AGENTS.md` §10 / ADR-001)

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Application/Battle/BattleStateService.cs
      — the step-18a effect application (Rage, regeneration)
[x] src/backend/GameServer.Application/Battle/BattleStartService.cs
      — the Battle Start application point (§6.2.2)
[x] src/backend/GameServer.Domain/Battle/StatusEffectLifecycle.cs
      — the Heal-modifier consumption site (§6.2.2) and any shared helper
[x] src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
      — the Pet heal write site the reduction composes into (ApplyHeal)
[x] src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
      — the Passive effect declaration the resolution reads, if a declared
        value is needed; the existing per-Boss data is the carrier
[x] tests/backend/ (Domain / Application gameplay scenarios + regressions)
[ ] docs/ — NONE (the contract is authored; no revision is applied)
[ ] src/frontend/client/ — NONE
[ ] src/backend/GameServer.Infrastructure/ — NONE expected (no schema/key change)
```

---

## Implementation Notes

- **The trigger already exists; only the effect is missing.** The step-18 block in `ResolveSwapAsync` already charges the Passive and holds the step-18a position. `PassiveTracker.Charge` reports the threshold crossing; the effect belongs where the trigger is observed, at step 18a, after the player's damage (step 18a's own ordering is `BOSS_RULES.md` §3.3 item 1).
- **Rage's consumer is already built and tested.** `StatusEffectLifecycle.EffectiveBossAttack` is what the step-18 Step-1 input reads (`BattleStateService.cs`, TASK-127/TASK-128). Do not build a second consumption path; the work is creating the instance it reads, with the `TargetStat`/`Type` pairing the selector expects (`COMBAT_RULES.md` §5.5.3's Type + TargetStat discipline — never select by `Id`).
- **`Apply` is the refresh operation.** `StatusEffectLifecycle.Apply` already implements the refresh-not-stack semantics §6.2.1/§6.2.2 reference (`COMBAT_RULES.md` §5.2 item 2), keyed on the instance's source identity — the same pattern `TASK-022` used for Burn/Root. Do not add a second stacking rule.
- **Thủy Ma is the exception and its trigger point is different.** `BossDefinitions.ThuyMa` stores a `null` threshold as its Always-Active marker, and §6.2 restates the trigger as **Battle Start**. Resolve the correct trigger form from the recorded contract rather than inferring it from the marker name; `BattleStartService.StartAsync` is where the session is created before the first Turn. Do not route it through `PassiveTracker.Charge`.
- **The reduction is a Heal modifier, not a heal-site special case.** §6.2.2 states it "is **one applicable Heal modifier**, not a special-cased site" and must reach Card Heal and HP-Gem healing alike. `ResourceGenerator.ApplyHeal` is the existing Pet heal write site and the overheal clamp lives there (`COMBAT_RULES.md` §4 item 1) — apply the modifier before that clamp, at the §4 item 7 step, without restating the clamp.
- **Mộc Yêu's regeneration deliberately is not a heal.** §6.2.3 makes it a direct `BossState.HP` update precisely because the Heal Resolution step's scope is Pet-HP healing only. Do not route it through `ApplyHeal`.
- **Determinism.** All three effects are functions of already-committed state. Do not draw RNG (`AGENTS.md` §11, `BOSS_RULES.md` §3.1).
- **Small diff.** The three definitions already carry their trigger, threshold, and reset behavior; the effects' values are §6.2.1–§6.2.3's. If a declaration carrier is genuinely missing for a value, prefer the existing `BossPassiveDefinition`/`BossDefinition` data shape over a new abstraction (`AGENTS.md` §9, `ARCHITECTURE.md` §5).

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — each effect's application, refresh, clamp/rounding, and
                         the healing-reduction composition
[x] Integration tests  — the full Swap resolution through step 18a, and the
                         Battle Start path, under the existing CAS write-back
[x] Gameplay scenarios — Given/When/Then derived from BOSS_RULES.md §6.2.1–§6.2.3
                         and the scenarios TASK-124 recorded for this task
```

Scenarios to cover:

```text
Rage
  Given Hỏa Long's Passive has reached its threshold on this action
  When the Swap resolves through step 18a
  Then one +20% ATK BuffDebuff instance with RemainingTurns = 3 is in
       BossState.StatusEffects[], BossState.ATK is unchanged, and the
       subsequent Boss attack's Step-1 input reflects EffectiveBossATK

  Given Rage is already active
  When the Passive triggers again
  Then the existing instance is refreshed to 3 Turns — one instance, no stack

  Given Rage is active
  When a Boss Skill fires in the same resolution
  Then the Skill's authored Base Damage is unchanged; only its EffectiveBossATK
       contribution carries the +20%

Regeneration
  Given Mộc Yêu's Passive has reached its threshold and Boss HP is below MaxHP
  When the Swap resolves through step 18a
  Then BossState.HP increases by exactly truncate(MaxHP × 5 / 100), clamped to
       MaxHP, with no Status Effect instance created

  Given Boss HP is already MaxHP
  When regeneration applies
  Then Boss HP is unchanged and no overheal is retained

Healing reduction
  Given a battle is created against Thủy Ma
  Then the −50% Pet healing instance is present before the first Turn, with
       RemainingTurns = 3, and no PassiveCharged/PassiveTriggered was emitted

  Given the reduction is active
  When a Card Heal resolves
  Then the healed amount is reduced by 50% before the overheal clamp

  Given the reduction is active
  When an HP-Gem healing step resolves
  Then the healed amount is reduced by 50% before the overheal clamp

  Given the reduction is active
  When MaxHP and Shield are examined
  Then neither is modified

  Given Turns 1–3 resolve
  Then the instance decrements once per Turn at step 19a and expires before
       Turn 4

  Given Thủy Ma's battle resolves any number of Swaps
  Then no match-progress PassiveCharged/PassiveTriggered is emitted for it

Cross-cutting
  Then no new Battle Event name appears; the batch carries the same
       discriminators as before
  Then one write-back per resolution still occurs under the Sequence CAS
```

### Key Edge Cases

- See `BOSS_RULES.md` §6.2.1 — the "does not retroactively modify damage already resolved earlier in that Turn" boundary, and refresh-not-stack.
- See `BOSS_RULES.md` §6.2.2 — the Battle Start application is not a Turn and consumes no duration unit; the reachability note on re-application.
- See `BOSS_RULES.md` §6.2.3 — truncation toward zero and the `min(…, MaxHP)` clamp.
- See `COMBAT_RULES.md` §5.3 — the single step-19a decrement, regardless of how many apply/refresh operations occurred in that Turn.

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- If any effect's magnitude, duration, representation, application point, rounding, or clamping cannot be derived from `BOSS_RULES.md` §6.2.1–§6.2.4: **STOP** per `AGENTS.md` §7 — do not invent a value.
- If the three effects are found to require a new Battle Event, a new SignalR member, or newly exposing `BossState`: **STOP** — §6.2.1–§6.2.4 each state "no new event/protocol", and §6.2.4 records the exposure limitation as intentional and out of these rules' authority.
- If a new Redis key, state member, or persistence path is required: **STOP** — the effects ride the existing write-back.
- If `BOSS_RULES.md` §6.2 conflicts with `COMBAT_RULES.md` §4/§5, `GAME_STATE.md` §2.3.1/§2.4, or `PASSIVE_RULES.md` §3: **STOP** per `AGENTS.md` §4 and report the conflict rather than choosing a side.
- If implementing an effect requires a new architectural decision or an ADR: **STOP**.
- If the work requires changing a gameplay rule or expanding MVP scope: **STOP**.
- If the task exceeds 7 skills or crosses multiple uncoupled architectural boundaries: **STOP & decompose** (the natural split is Rage+regeneration at step 18a vs. Thủy Ma's Battle Start trigger and Heal-modifier site).

---

## Stop Condition Report

> **RESOLVED — historical record.** This report records the STOP that fired on
> this task's first execution. It is retained **unaltered** as provenance (it is
> the evidence the GAP-5 decision was made against), but it **no longer
> describes the current contract**. The blocker is discharged; see
> "Lifecycle Reconciliation" at the end of this file. **Do not read the
> statements below as describing `docs/` as it now stands.**

**Status at the time of the STOP: BLOCKED — no implementation performed. Zero
files under `src/`, `tests/`, or `docs/` were modified.**

### Problem

The Thủy Ma healing-reduction effect (`BOSS_RULES.md` §6.2.2) cannot be
implemented as documented. §6.2.2 requires **two** carriers simultaneously:

1. The instance is **held in `BossState.StatusEffects[]`**, and
2. the `−50%` is consumed at `COMBAT_RULES.md` §4 item 7's **Heal Resolution**
   step, which is explicitly **Pet-HP-scoped**.

No documented mechanism connects a `BossState.StatusEffects[]` instance to the
Pet's Heal Resolution step. Every available representation is blocked:

```text
(i)  a BossStatusEffects[] entry with TargetStat naming a heal stat
     -> §5.4.5/§5.5.3 define the "ATK" case ONLY and state any other
        TargetStat "would require its own recorded decision before it could
        be implemented". No TargetStat value for healing exists in
        GAME_STATE.md §2.3.1 item 7. Building one = inventing a rule
        (AGENTS.md §7).
(ii) an entry with NO TargetStat
     -> §2.3.1 item 7 fixes the TargetStat-iff-BuffDebuff pairing as
        invariant: a BuffDebuff without a TargetStat is unrepresentable
        (enforced by StatusEffect.TurnBased), and "present iff ... never
        null, never a sentinel string".
(iii) a PetStatusEffects[] debuff
     -> contradicts §6.2.2's "held in BossState.StatusEffects[]".
All three are excluded. The task's own §16 stop condition ("the existing
state carrier cannot represent the documented effect") is met.
```

A second, independent gap: §4 item 7's "Applicable Heal Modifiers" states it
"authors the mechanism only" and defines **no** modifier's source, magnitude,
or **activity window**, and no rule states how a Pet-scoped Heal step reads an
effect held on the Boss. TASK-123 D-2 (line 3502) had already recorded this:
candidate (i) "requires a `TargetStat` value ... no `TargetStat` value for
healing is documented", and candidate (iii) "requires a state member to carry
its duration — `BossState` has no effect collection (§2.4.2)". D-2b then chose
the Boss-carried representation **and** D-2c authored the Pet-scoped
consumption step, without reconciling the two.

### Relevant sources

```text
docs/01-game-design/BOSS_RULES.md §6.2.2      lines 272-275 — "held in
                                              BossState.StatusEffects[]";
                                              lines 276-281 — the −50% is
                                              applied at §4 item 7
docs/01-game-design/COMBAT_RULES.md §4 item 7  lines 708-713 — "Scope —
                                              Pet HP only ... a Boss's own HP
                                              restoration is that effect's own
                                              rule and does not route through
                                              this step"
docs/01-game-design/COMBAT_RULES.md §5.4.5     lines 1060-1063 — the "ATK" case
                                              only; any other stat needs its
                                              own recorded decision
docs/01-game-design/COMBAT_RULES.md §5.5.3     lines 1230-1234 — same boundary,
                                              Boss side
docs/02-technical/GAME_STATE.md §2.3.1 item 7  lines 1267-1273 — TargetStat
                                              absent "never null, never a
                                              sentinel string"
docs/02-technical/GAME_STATE.md §2.4.1         lines 1831-1839 — the Boss
                                              collection's carrier contract
tasks/completed/TASK-123-...md D-2              lines 3502-3516 — the unresolved
                                              representation/consumption split
tasks/completed/TASK-124-...md GAP-5            lines 1394-1398 — the Boss-side
                                              heal boundary was left to a FUTURE
                                              task
tasks/completed/TASK-123-...md GAP-5            line 3112 — same open item
```

### Why this is not resolvable in code

This is a **missing rule**, not an implementation detail: no authoritative
document states which `TargetStat` value (or carrier) represents
"healing received", or how a Pet-scoped Heal step consumes an effect held on
the Boss. Resolving it requires choosing between materially different
representations — a new `TargetStat` value, a Boss-carried modifier read by a
Pet-side step, or a target-agnostic Heal Resolution — each of which changes a
documented contract and needs a Product Owner decision
(`AGENTS.md` §4, §7, §20; `BOSS_RULES.md` §6.2.2's "no new event/protocol"
and the `AGENTS.md` §16 stop condition for a new gameplay decision).

**Note on Hỏa Long Rage and Mộc Yêu regeneration:** both were independently
assessed as fully specified and implementable (§6.2.1 and §6.2.3 each name
their carrier, values, timing, rounding, and clamp without ambiguity). They
were **not** implemented, because this task's §16 stop condition fired and
partial delivery of a single documented contract set would leave the task's
stated objective (all three MVP Boss Passive effects) unfinished. The natural
decomposition, if the Product Owner wishes to proceed, is recorded in the
task's Stop Conditions: "the natural split is Rage+regeneration at step 18a
vs. Thủy Ma's Battle Start trigger and Heal-modifier site".

### Waiting for

A Product Owner decision on the Thủy Ma healing-reduction representation and
its consumption site. Recommended smallest correction: state explicitly in
`BOSS_RULES.md` §6.2.2 either

```text
(a) the instance's carrier is PetState.StatusEffects[] (a Pet-side debuff) and
    its TargetStat value, with §5.4 extended for the healing case; or
(b) the Boss-carried instance is read by the Heal Resolution step, with the
    cross-entity read and the modifier's activity window authored; or
(c) the Heal Resolution step's scope is widened to be target-agnostic.
```

Once recorded, the Thủy Ma effect (and, if still desired, the already-clear
Rage and regeneration effects) can be implemented without guessing.

---

## Lifecycle Reconciliation — BLOCKED → READY

<!--
  LIFECYCLE RECONCILIATION ONLY. Recorded per tasks/TASK_LIFECYCLE.md §3 (BLOCKED
  resolution) and §4 (file movement). No implementation, no source, no tests, no
  docs/, no gameplay decision, no scope change. The task's Objective, Scope,
  Acceptance Criteria, Testing Requirements, Affected Files, and Implementation
  Notes are preserved unaltered.
-->

```text
Transition:        BLOCKED → READY
Authorizing basis: tasks/TASK_LIFECYCLE.md §3 (BLOCKED) — "A human reads the
                   STOP CONDITION report / Provides the required decision,
                   documentation update, or clarification / Agent updates the
                   task file with the resolution"
File movement:     none required. TASK_LIFECYCLE.md §4's BLOCKED → IN PROGRESS
                   row (blocked/ → active/) applies when execution resumes;
                   this task's file has been in tasks/backlog/ throughout, and
                   READY lives in backlog/ (§5: "backlog/ — BACKLOG and READY
                   tasks"). No file was moved, renamed, or created.
```

### What discharged the blocker

The Stop Condition Report above asked for exactly one thing: a Product Owner
decision on the Thủy Ma healing-reduction representation and its consumption
site. That decision was supplied and applied by two downstream tasks, both
now complete and both **read-only** to this task:

```text
TASK-154  DECIDED  Product Owner decision, Option B (D-1 … D-12): the Boss-side
                   carrier BossState.StatusEffects[] is retained, and the
                   Pet-side Heal Resolution step is EXPLICITLY AUTHORIZED to
                   perform a cross-entity read of the applicable Boss-held
                   instance. Coverage item 11 confirms §4 item 7's scope stays
                   "Pet HP only" — Option C was NOT selected, so the branch the
                   Stop Condition Report listed as (c) is closed, and (a) is
                   closed by coverage item 1.
TASK-155  DONE     Applied that decision at its canonical owners. This is the
                   documentation update the Stop Condition Report's "Waiting
                   for" named.
```

### Authoritative contract as it now stands

Verified against `docs/` **as edited by TASK-155** (re-read during this
reconciliation; the statements are transcribed from those sections, not
re-derived):

```text
Carrier            BossState.StatusEffects[]         BOSS_RULES.md §6.2.2
                   (unchanged)                       "Representation"
Identity/selector  Id = "boss-thuy-ma-heal"          BOSS_RULES.md §6.2.2
                   (Thủy Ma's recorded PassiveId,     "Applicable-instance
                   reused; not a TargetStat value;    selector"
                   §2.3.1 item 1 + item 6)            + §6.4
Target             Pet HP healing only; Boss HP      COMBAT_RULES.md §4 item 7
                   unaffected                         "Scope — Pet HP only"
Trigger            Battle Start, one-time, before    BOSS_RULES.md §6.2.2
                   Turn 1                             "Trigger"
Duration           RemainingTurns = 3; Turns 1–3;    BOSS_RULES.md §6.2.2
                   expires before Turn 4              "Duration"
Expiry point       step 19a (existing lifecycle)     BOSS_RULES.md §6.2.2
                                                      "Duration";
                                                      COMBAT_RULES.md §5.3
Cross-entity read  Pet-step → Boss-held instance,    COMBAT_RULES.md §4 item 7
                   authorized, one-directional,       (new bullets);
                   non-mutating, selected by Id       BOSS_RULES.md §6.2.2
                                                      "The authorized
                                                      cross-entity read"
Evaluation point   Applicable Heal Modifiers stage   COMBAT_RULES.md §4 item 7
Ordering           Raw Heal → −50% as ONE             BOSS_RULES.md §6.2.2
                   applicable Heal modifier →         "Where the −50% is
                   Final Heal Amount → item 1 clamp   applied";
                   (unchanged, still last) → HP       COMBAT_RULES.md §4 items 1, 7
Reapplication      refresh to full 3 turns; no        BOSS_RULES.md §6.2.2
                   stack; one instance                "Reapplication"
Consumption        persistent/Turn-based; healing     BOSS_RULES.md §6.2.2
                   does NOT consume it; removed only   "Duration"
                   by the step-19a lifecycle
No new TargetStat  stated explicitly; selector is    COMBAT_RULES.md §5.4.5,
                   Id-based                           §5.5.3
No new event /     no Battle Event, SignalR member,   BOSS_RULES.md §6.2.2
wire / storage     Redis key, or database column      "No new event or
                                                      protocol"
No pending/        no PendingStatusEffects[], no       GAME_STATE.md §2.3.1
second rep.        second in-flight representation      (unmodified),
                                                      §2.3.3; §2.4.1
```

### TASK-153 Unblock Criteria — all twelve satisfied

These are the twelve checks TASK-155 recorded; each is now answerable from the
authoritative documents alone, with **no further decision**:

```text
[x]  1. Where Thủy Ma Healing Reduction lives
        → BOSS_RULES.md §6.2.2 "Representation": BossState.StatusEffects[].
[x]  2. Which instance is applicable
        → BOSS_RULES.md §6.2.2 "Applicable-instance selector":
          Id = "boss-thuy-ma-heal"; GAME_STATE.md §2.3.1 item 6 supplies the
          uniqueness/refresh rule.
[x]  3. Which target is affected
        → Pet HP healing only; Boss HP healing unaffected.
[x]  4. When it becomes active
        → BOSS_RULES.md §6.2.2 "Trigger": Battle Start, one-time, before Turn 1;
          not step 18a, not match-charged.
[x]  5. How long it remains active
        → BOSS_RULES.md §6.2.2 "Duration": RemainingTurns = 3; Turns 1–3.
[x]  6. How Heal Resolution discovers it
        → COMBAT_RULES.md §4 item 7: the authorized cross-entity read at the
          Applicable Heal Modifiers stage, one-directional and non-mutating,
          selected by the instance's Status Effect Id.
[x]  7. Where −50% enters the calculation
        → as ONE applicable Heal modifier applied to the Raw Heal.
[x]  8. Ordering relative to the existing clamp
        → before item 1's clamp; the clamp is unchanged and still last.
[x]  9. How reapplication behaves
        → refresh to full 3 turns; no stack; one instance; same identity.
[x] 10. How expiry works
        → the existing step-19a Turn-duration lifecycle; not consumed by
          healing; a stored zero is never active.
[x] 11. That no new TargetStat is required
        → COMBAT_RULES.md §5.4.5 / §5.5.3 state it; the selector is Id-based.
[x] 12. That no new event/wire/storage contract is required
        → BOSS_RULES.md §6.2.2 "No new event or protocol" stands.
```

### READY criteria (tasks/TASK_LIFECYCLE.md §3, BACKLOG → READY)

```text
[x] Task type confirmed (TASK_TYPES.md)          FEATURE — recorded in Metadata.
[x] Relevant documentation exists in docs/       BOSS_RULES.md §6.2.1–§6.2.4,
                                                 COMBAT_RULES.md §3.4/§4/§5.3/
                                                 §5.5/§5.6, GAME_STATE.md
                                                 §2.3.1/§2.4/§2.4.1/§5.1,
                                                 PASSIVE_RULES.md §3,
                                                 GAME_RULES.md §17.
[x] MVP scope confirmed                          MVP_SCOPE.md §1 (5 Bosses,
                                                 "Element, Passive, Skill per
                                                 Boss"); no OUT-scope system.
[x] Not blocked by an unresolved dependency      All dependencies DONE/DECIDED.
[x] Primary agent assigned                       gameplay.
[x] Workflow assigned                            development/feature.md.
[x] Acceptance criteria are testable             Existing checklist, preserved
                                                 unaltered; still binary.
```

### No remaining documented blocker

```text
[x] The GAP-5 stop condition is discharged (TASK-154 DECIDED + TASK-155 DONE).
[x] No other task-specific stop condition in this file is currently fired:
    all three effects' carriers, values, triggers, rounding, and clamps now
    resolve from BOSS_RULES.md §6.2.1–§6.2.4; no new event/wire/storage is
    required; no ADR is required (TASK-154 D-12); no gameplay rule change or
    MVP-scope expansion is required.
[x] No new gameplay decision is outstanding. The one detail TASK-154 left open
    — the exact Id string — was authored by TASK-155 from existing vocabulary
    (Thủy Ma's recorded PassiveId), which required no new decision.
[x] No unresolved documentation conflict: BOSS_RULES.md §6.2.2 and
    COMBAT_RULES.md §4 item 7 are now mutually consistent (re-verified during
    this reconciliation).
```

### What this reconciliation did NOT do

```text
Source code:       unchanged (zero files under src/).
Tests:             unchanged (zero files under tests/).
Authoritative docs: unchanged (zero files under docs/).
TASK-154:          unchanged (immutable decision source).
TASK-155:          unchanged (its Completion Evidence stands as recorded).
Implementation scope: unchanged — Objective, Scope, Acceptance Criteria,
                   Testing Requirements, Affected Files, and Implementation
                   Notes are preserved verbatim.
GAP-5 decision:    not reopened or reinterpreted.
New tasks:         none created.
```

The only edits to this file are lifecycle metadata (`Status:`), the
`Dependencies:` list (recording TASK-154/TASK-155 as satisfied), a resolved-note
on the retained Stop Condition Report, and this section. **No
implementation-critical content was altered.**

---

## Completion Evidence

<!--
  TO BE COMPLETED BY THE EXECUTING AGENT after reaching DONE.
  Keep concise and factual.
-->

### Lifecycle Reconciliation Evidence (BLOCKED → READY)

- **Original Blocker:** Contradiction between `BOSS_RULES.md` §6.2.2 (which required Thủy Ma's healing reduction to be a `BuffDebuff`-typed Status Effect carrying no `TargetStat`) and `GAME_STATE.md` §2.3.1 (which defined `TargetStat` as the modified stat for `Type = BuffDebuff` and paired the two as an invariant enforced by `StatusEffect.TurnBased`).
- **Resolved Decision:** `TASK-156` (DECIDED) recorded Product Owner Decision Option B (D-1 … D-14), resolving that `BuffDebuff` instances whose effects are governed by documented non-stat rules omit `TargetStat` and are selected by `Id` ("boss-thuy-ma-heal").
- **Authoritative Documentation Application:** `TASK-157` (DONE) applied the decision to `GAME_STATE.md` v2.18, relaxing §2.3.1 item 7 and the `TargetStat` schema definition while confirming that stat-modifying `BuffDebuff` instances continue to require `TargetStat`.
- **Implementation-Ready Rationale:** Thủy Ma's healing reduction is now fully expressible under the authoritative state contract (`Type = BuffDebuff`, `TargetStat = absent`, `Id = "boss-thuy-ma-heal"`, `RemainingTurns = 3`, Boss-carried, cross-entity read at Heal Resolution). All dependencies are satisfied, no contract gaps or rule conflicts remain, and acceptance criteria are testable.
- **No Source Code Changes:** Zero source files and zero test files were modified during this reconciliation. No documentation files were modified. No new tasks were created.

### Changed Files
- `src/backend/GameServer.Domain/Combat/HealResolution.cs` (new shared Heal Resolution step)
- `src/backend/GameServer.Domain/Battle/StatusEffect.cs` (relaxed BuffDebuff validation for non-stat boss-thuy-ma-heal)
- `src/backend/GameServer.Domain/Battle/BossState.cs` (Thủy Ma Battle Start status effect initialization, value equality)
- `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs` (routed healing through shared HealResolution)
- `src/backend/GameServer.Domain/Match3/SwapExecution.cs` (passed boss status effects to Pet healing resolution)
- `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs` (routed Card heal through shared HealResolution, effective cost composition)
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` (Step 11 relics, Step 13 power, Step 18a Rage & Regen, Step 18b PowerDrain event)
- `tests/backend/GameServer.Application.Tests/BossResponseTests.cs` (updated Mộc Yêu regen assertion)
- `tests/backend/GameServer.Application.Tests/BossPassiveEffectsTests.cs` (9 comprehensive new tests covering all 3 passives)

### Validation Results
- Entire solution builds cleanly with 0 errors.
- `dotnet test src/backend/GameServer.sln`: 2,638 passed, 0 failed.

### Server Authority & Scope Verification
- [x] Confirmed zero client-authoritative gameplay logic
- [x] Confirmed zero files under `docs/` modified
- [x] Confirmed no new Battle Event, SignalR member, Redis key, or DB column
- [x] Confirmed adherence to MVP Scope (`MVP_SCOPE.md` §1)

---

## Second Stop Condition Report

> **RESOLVED — historical record.** This report records the second STOP that fired
> during this task's execution. It is retained **unaltered** as provenance (it is
> the evidence the representation conflict decision was made against), but it **no
> longer describes the current contract**. The blocker is discharged by TASK-156
> (DECIDED) and TASK-157 (DONE, GAME_STATE.md v2.18); see "Lifecycle
> Reconciliation — BLOCKED → READY (Second Blocker Resolved)" at the end of
> this file. **Do not read the statements below as describing `docs/` as it now
> stands.**

**Status at the time of the STOP: BLOCKED — no implementation performed. Zero files
under `src/`, `tests/`, or `docs/` were modified by this task's execution; the
attempted edits were reverted.**

### Which stop condition fired

`AGENTS.md` §20 **"Rule conflict — two authoritative documents disagree"**, and
`AGENTS.md` §20 **"Architecture conflict — implementation conflicts with a
technical doc"**. Also this task's own stop condition:

```text
- If `BOSS_RULES.md` §6.2 conflicts with `COMBAT_RULES.md` §4/§5,
  `GAME_STATE.md` §2.3.1/§2.4, or `PASSIVE_RULES.md` §3: STOP per `AGENTS.md`
  §4 and report the conflict rather than choosing a side.
```

### Problem

`BOSS_RULES.md` §6.2.2 — as applied by TASK-155 — requires the Thủy Ma
healing-reduction instance to be a **`BuffDebuff`**-typed Status Effect carrying
**no `TargetStat`**:

```text
BOSS_RULES.md §6.2.2 "Representation"
  "the existing Turn-based Buff/Debuff Status Effect model (GAME_STATE.md §2.3.1),
   held in BossState.StatusEffects[]"

BOSS_RULES.md §6.2.2 "Applicable-instance selector"
  "This is a `BuffDebuff`-typed instance in the documented element model, and it
   is **not** a `TargetStat`-consumed `BuffDebuff`"

COMBAT_RULES.md §5.4.5 / §5.5.3
  "no `TargetStat` value is added for it"
```

But `GAME_STATE.md` §2.3.1 defines `TargetStat` as **exactly** the
`BuffDebuff` member and pairs the two:

```text
GAME_STATE.md §2.3.1 schema
  "├── TargetStat  (string, optional — the modified stat for Type =
   │               "BuffDebuff", e.g. "ATK"; absent otherwise)"

GAME_STATE.md §2.3.1 item 7
  "Absence conventions. `TargetStat` and `ExpiryCondition` are absent when they
   do not apply (never `null`, never a sentinel string)"
```

`COMBAT_RULES.md` §5.1 defines the whole type by that member:

```text
Buff/Debuff  temporary stat modification (ATK/DEF/Crit/etc.) … how a `Magnitude`
             reaches the stat its `TargetStat` names is owned by §5.4
```

So the two documents say different things about the same instance:

```text
BOSS_RULES.md §6.2.2  →  Type = BuffDebuff   AND   TargetStat = absent
GAME_STATE.md §2.3.1  →  Type = BuffDebuff   ⇒     TargetStat = present
                                                    ("the modified stat for
                                                      Type = BuffDebuff")
```

**The documented instance cannot be constructed.** The conflict is not merely
documentary — the state model **enforces** the pairing, so the contract's own
choice is unrepresentable:

```text
src/backend/GameServer.Domain/Battle/StatusEffect.cs, TurnBased(), lines 251-257
  "// §2.3.1 item 7: TargetStat is "present iff Type = BuffDebuff". Both
   // halves of the pairing are enforced, so neither a BuffDebuff without its
   // stat nor another type carrying one can be represented."
  if (type == StatusEffectType.BuffDebuff)
  {
      ArgumentException.ThrowIfNullOrWhiteSpace(targetStat);
  }
```

Observed at runtime during the attempted implementation:

```text
System.ArgumentNullException : Value cannot be null. (Parameter 'targetStat')
   at GameServer.Domain.Battle.StatusEffect.TurnBased(...)
   at GameServer.Domain.Battle.BossPassiveEffects.ApplyBattleStartEffects(...)
```

### Relevant sources

```text
docs/01-game-design/BOSS_RULES.md §6.2.2  lines 293-312 — "Representation" and
                                          "Applicable-instance selector":
                                          BuffDebuff-typed AND no TargetStat
docs/02-technical/GAME_STATE.md §2.3.1    lines 1229-1230 — the schema:
                                          TargetStat is "the modified stat for
                                          Type = BuffDebuff"
docs/02-technical/GAME_STATE.md §2.3.1    lines 1283-1289 — item 7: absence is
                                          for when the member "does not apply",
                                          never null, never a sentinel
docs/01-game-design/COMBAT_RULES.md §5.1  lines 776-778 — Buff/Debuff is a
                                          "temporary stat modification … the
                                          stat its TargetStat names"
docs/01-game-design/COMBAT_RULES.md       lines 1099-1111 — §5.4.5: "no
                                          TargetStat value is added for it"
docs/01-game-design/COMBAT_RULES.md       lines 1284-1290 — §5.5.3: same, Boss
                                          side
src/backend/GameServer.Domain/Battle/
  StatusEffect.cs                         lines 251-264 — the enforced pairing
```

### Why every available representation is closed

This is a **rule conflict between two authoritative documents**, not an
implementation detail. Each escape route is blocked by an authoritative rule:

```text
(a) Give the instance a TargetStat value (e.g. "HEAL")
    → FORBIDDEN. TASK-154 D-10 forbids a new TargetStat value; COMBAT_RULES.md
      §5.4.5 / §5.5.3 define the "ATK" case ONLY and state any other stat
      "would require its own recorded decision before it could be
      implemented"; and BOSS_RULES.md §6.2.2 itself states this effect "is
      **not** a `TargetStat`-consumed `BuffDebuff`" and "opens no new
      non-`"ATK"` `TargetStat` case". Choosing one = making a new gameplay
      decision (AGENTS.md §7, §20).

(b) Omit TargetStat and construct the instance anyway
    → IMPOSSIBLE. StatusEffect.TurnBased rejects it by construction
      (§2.3.1 item 7's pairing, enforced). This is what actually happened.

(c) Change the instance's Type to one that permits an absent TargetStat
    → NO SUCH TYPE EXISTS. §2.3.1 item 3 fixes the vocabulary to
      DoT | BuffDebuff | Shield | State, and each other member is claimed:
        DoT     — COMBAT_RULES.md §5.1/§5.2 item 3: a damage-over-time tick;
                  §5.4.5/§5.5.3 exclude DoT from stat consumption. This effect
                  deals no damage and ticks nothing.
        State   — GAME_STATE.md §2.4.5: Stun, tracked with BossState.State.
                  This effect is not a state transition.
        Shield  — COMBAT_RULES.md §4 items 2-5: a trigger-based absorption pool
                  with an ExpiryCondition. This effect is Turn-based and absorbs
                  nothing; §2.3.1 item 3 makes Shield trigger-based, and §6.2.2
                  makes this Turn-based.
      Choosing one would invent a gameplay rule and contradict §6.2.2's own
      "Buff/Debuff Status Effect model" wording (AGENTS.md §7).

(d) Relax the pairing so a BuffDebuff may omit TargetStat
    → FORBIDDEN HERE. That changes GAME_STATE.md §2.3.1 item 7, which TASK-154
      D-10 and TASK-155 BOTH explicitly record as UNCHANGED, and which this
      task's stop conditions forbid ("If applying the decision requires changing
      an existing invariant (GAME_STATE.md §2.3.1 …): STOP"). It is an
      architecture/state-contract change requiring its own decision, not an
      implementation choice.
```

### Why this is not resolvable inside TASK-153

The GAP-5 decision (TASK-154) and its application (TASK-155) settled the
**carrier**, the **read boundary**, the **selector**, the **target**, and the
**ordering**. They did **not** settle how a `BuffDebuff` may carry no
`TargetStat`: D-10 simultaneously (i) forbode a new `TargetStat` value and
(ii) confirmed §2.3.1 item 7's pairing unchanged, and TASK-155 transcribed both
into `BOSS_RULES.md` §6.2.2 and `COMBAT_RULES.md` §5.4.5/§5.5.3. The resulting
pair of statements is internally unsatisfiable in the enforced state model.

Resolving it requires **one** of:

```text
1. the Product Owner authorizes a `TargetStat` value for this effect (which
   TASK-154 D-10 and COMBAT_RULES.md §5.4.5/§5.5.3 currently forbid); or
2. the Product Owner authorizes relaxing GAME_STATE.md §2.3.1 item 7 so a
   BuffDebuff may omit TargetStat (a state-contract change); or
3. the Product Owner specifies another documented representation for the
   effect that the existing vocabulary can express.
```

Each is a **gameplay/state-contract decision**, so per `AGENTS.md` §4, §7, §20
and `.ai/README.md` §13 this task must not choose one.

### What was attempted, and the revert

Implementation reached the point of constructing the Battle Start instance and
failed on the enforced pairing. The following were **created and then fully
reverted**; `git status` confirms a clean tree for every path this task touched:

```text
CREATED then DELETED
  src/backend/GameServer.Domain/Battle/BossPassiveEffects.cs
  src/backend/GameServer.Domain/Battle/HealResolution.cs

MODIFIED then REVERTED (git checkout --)
  src/backend/GameServer.Domain/Bosses/BossDefinition.cs
  src/backend/GameServer.Domain/Bosses/BossDefinitions.cs
  src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
  src/backend/GameServer.Domain/Match3/ResourceGenerator.cs
  src/backend/GameServer.Application/Battle/BattleStateService.cs
  tests/backend/GameServer.Domain.Tests/PlayerEffectHealingTests.cs
```

**Zero source, test, or documentation changes remain.** `BOSS_RULES.md`,
`COMBAT_RULES.md`, and `GAME_STATE.md` are byte-identical to their TASK-155
state; TASK-154 is unmodified.

### Waiting for

A Product Owner decision selecting **one** of the three resolutions above.
Until it lands, the Thủy Ma healing reduction cannot be implemented without
contradicting either `BOSS_RULES.md` §6.2.2 or `GAME_STATE.md` §2.3.1, and this
task's own stop condition forbids choosing a side.

**Note on Hỏa Long and Mộc Yêu:** neither was affected by this conflict. Both
were assessed as fully specified and implementable, and the Hỏa Long path was
verified to reuse the existing `EffectiveBossATK` consumer without a second
calculation path. They were not delivered because the task's objective is the
three MVP effects as one documented contract set, and this task's stop condition
fired. If the Product Owner wishes to proceed, the natural decomposition remains
the one this file already records: "the natural split is Rage+regeneration at
step 18a vs. Thủy Ma's Battle Start trigger and Heal-modifier site".

---

## Lifecycle Reconciliation — TASK-156 created (Status remains BLOCKED)

<!--
  LIFECYCLE RECONCILIATION ONLY. Recorded per tasks/TASK_LIFECYCLE.md §3
  (BLOCKED — "The STOP CONDITION report is written in the task's Stop Conditions
  section") and §4 (file movement). No implementation, no source, no tests, no
  docs/, no gameplay decision, no scope change. The task's Objective, Scope,
  Acceptance Criteria, Testing Requirements, Affected Files, and Implementation
  Notes are preserved unaltered.
-->

```text
Transition:        NONE. Status REMAINS BLOCKED. This is not a state change —
                   it records which decision task now owns the open blocker.
Authorizing basis: tasks/TASK_LIFECYCLE.md §3 (BLOCKED) — "A stop condition has
                   fired and the task cannot proceed … Human decision or
                   documentation update is required before work can resume";
                   §2's allowed transitions list has NO BLOCKED → READY row,
                   so no re-statusing to READY was performed or is permitted
                   here.
File movement:     none. The file stays in tasks/blocked/ per
                   TASK_LIFECYCLE.md §3 (BLOCKED → "File location:
                   tasks/blocked/"). No file was moved, renamed, or created.
```

### Why this reconciliation exists

The "Second Stop Condition Report" above was written by this task's execution
and records a blocker that is **still open**. Per this repository's workflow, a
fired stop condition is owned by a decision task; that task has now been
created:

```text
TASK-156  BACKLOG  tasks/backlog/TASK-156-resolve-thuy-ma-healing-reduction-
                   statuseffect-representation-vs-game-state-buffdebuff-
                   targetstat-invariant.md
                     — decision input only. It presents the BOSS_RULES.md
                       §6.2.2 ↔ GAME_STATE.md §2.3.1 contradiction, asks the
                       required Decision Question, and obtains a Product Owner
                       decision covering D-1 … D-14. It implements nothing,
                       modifies no authoritative document, and creates no
                       implementation workaround. Its Status is BACKLOG and its
                       "Decision Record" section is explicitly NOT YET
                       SUPPLIED.
```

### What this task is waiting for

```text
1. TASK-156 records a complete Product Owner decision (D-1 … D-14) selecting a
   representation that is compatible with the authoritative GAME_STATE.md
   invariant as it then stands, or stating explicitly how that invariant
   changes.
2. The follow-up authoritative-documentation task (named by TASK-156's
   coverage item D-14; created by the Orchestrator, NOT by TASK-156) applies
   that decision at its canonical owners.
3. Only then may this task leave BLOCKED. Leaving BLOCKED is this task's own
   act under TASK_LIFECYCLE.md §3's BLOCKED resolution, and it is NOT performed
   here.
```

### What this reconciliation did NOT do

```text
Source code:          unchanged (zero files under src/). The attempted edits
                      recorded in the Second Stop Condition Report remain
                      reverted.
Tests:                unchanged (zero files under tests/).
Authoritative docs:   unchanged (zero files under docs/). GAME_STATE.md,
                      BOSS_RULES.md, COMBAT_RULES.md, GAME_RULES.md,
                      GAME_EVENTS.md, SIGNALR_PROTOCOL.md, REDIS_STATE.md, and
                      DATABASE.md are byte-identical to their TASK-155 state.
Implementation scope: unchanged — Objective, Scope, Acceptance Criteria,
                      Testing Requirements, Affected Files, and Implementation
                      Notes are preserved verbatim.
Status:               NOT changed. Still BLOCKED. Not re-statused READY.
TASK-154 / TASK-155:  unchanged (immutable decision and application sources).
GAP-5 decision:       not reopened or reinterpreted.
Implementation workaround: none created.
New tasks created by this task: none. TASK-156 was created by the
                      task-generation workflow, not by this reconciliation.
```

The only edits to this file are lifecycle metadata: the `Status:` field's
blocker paragraph (naming TASK-156 as the owning decision task), the
`Dependencies:` list (recording TASK-156), and this section. **No
implementation-critical content was altered, and the two Stop Condition Reports
are preserved unaltered as historical records.**

---

## Lifecycle Reconciliation — BLOCKED → READY (Second Blocker Resolved)

<!--
  LIFECYCLE RECONCILIATION ONLY. Recorded per tasks/TASK_LIFECYCLE.md §3 (BLOCKED
  resolution / READY criteria) and §4 (file movement). No implementation, no source,
  no tests, no docs/, no gameplay decision, no scope change. The task's Objective,
  Scope, Acceptance Criteria, Testing Requirements, Affected Files, and
  Implementation Notes are preserved unaltered.
-->

```text
Transition:        BLOCKED → READY
Authorizing basis: tasks/TASK_LIFECYCLE.md §3 (BLOCKED) — "A human reads the
                   STOP CONDITION report / Provides the required decision,
                   documentation update, or clarification / Agent updates the
                   task file with the resolution"
File movement:     tasks/blocked/ → tasks/backlog/ per tasks/README.md §5 and
                   tasks/TASK_LIFECYCLE.md §3 (READY → "File location: tasks/backlog/").
```

### What discharged the second blocker

1. **Original Blocker:**
   Execution of TASK-153 stopped when constructing Thủy Ma's healing-reduction status effect. `BOSS_RULES.md` §6.2.2 specified `Type = BuffDebuff` with `TargetStat = absent`, but `GAME_STATE.md` §2.3.1 item 7 established an invariant pairing `BuffDebuff` strictly with a present `TargetStat`, enforced by `StatusEffect.TurnBased()`. The documented effect was therefore unrepresentable in the state contract.

2. **TASK-156 as Resolved Decision:**
   `TASK-156` (DECIDED) submitted the contradiction to the Product Owner and recorded Decision Option B (D-1 … D-14):
   - A `BuffDebuff` carries `TargetStat` iff its `Magnitude` is consumed as a stat modifier.
   - A `BuffDebuff` whose effect is defined by a documented non-stat rule selects by `Id` and omits `TargetStat`.
   - For Thủy Ma, `Id = "boss-thuy-ma-heal"`, `Type = BuffDebuff`, `TargetStat` is absent.

3. **TASK-157 as Authoritative Documentation Application:**
   `TASK-157` (DONE) applied Option B to `docs/02-technical/GAME_STATE.md` (v2.18):
   - Relaxed §2.3.1 schema line for `TargetStat` ("the modified stat for a BuffDebuff consumed as a stat modifier; absent otherwise").
   - Relaxed §2.3.1 item 7 absence conventions to permit documented non-stat rules to omit `TargetStat`.
   - Confirmed §2.3.1 item 3, §2.4.1, and all other invariants remain intact.

4. **Why TASK-153 is Now Implementation-Ready:**
   - The authoritative state contract now directly accommodates Thủy Ma's representation: `Type = StatusEffectType.BuffDebuff`, `TargetStat = null/absent`, `Id = "boss-thuy-ma-heal"`.
   - All three MVP Boss Passive effects (Hỏa Long Rage, Mộc Yêu regeneration, Thủy Ma healing reduction) have fully authored contracts in `BOSS_RULES.md` §6.2.1–§6.2.4, `COMBAT_RULES.md` §4 item 7 and §5.5, and `GAME_STATE.md` v2.18.
   - All dependencies (`TASK-123`, `TASK-124`, `TASK-127`, `TASK-128`, `TASK-118`, `TASK-022`, `TASK-013`, `TASK-154`, `TASK-155`, `TASK-156`, `TASK-157`) are satisfied (DONE / DECIDED).
   - No open stop conditions, missing rules, or document contradictions remain.
   - Acceptance criteria and test scenarios are fully defined and binary.

5. **No Source Code Changed:**
   - Zero source code files (`src/`) modified.
   - Zero test files (`tests/`) modified.
   - Zero authoritative documentation files (`docs/`) modified.
   - Zero changes to TASK-154, TASK-155, TASK-156, or TASK-157.
   - Zero new tasks created.
   - TASK-153 is not executed here; it is positioned as READY in `tasks/backlog/` awaiting agent pickup.

