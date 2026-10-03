# TASK-152 — Implement the Authoritative `PowerChanged` Emission Contract

<!--
  GEN-TASK EXECUTION MANIFEST
  Principle: TASK = EXECUTION MANIFEST, NOT DOCUMENTATION DUMP.
  References docs/ and tasks/ by path and section.

  THIS IS THE IMPLEMENTATION TASK TASK-150 AND TASK-151 UNBLOCKED.

    TASK-150  decision record D-1 … D-8          (record)
            ↓
    TASK-151  applied to the two contract owners (documentation)
            ↓
    TASK-152  implemented in the resolution       (this task)

  TASK-150's consequence R-1 recorded that CardCastExecutor accumulates every
  Power change into one `newPower` and writes it once, which D-8 makes
  non-conforming. This task implements D-8 and the four-source value set. It
  does NOT re-open or reinterpret D-1 … D-8, and it modifies no documentation.
-->

---

## Metadata

```text
Task ID:           TASK-152
Type:              FEATURE (TASK_TYPES.md §2 — "Implement a documented mechanic,
                   capability, or system that already has a home in docs/ but has
                   not yet been built." The PowerChanged emission contract is now
                   fully documented — GAME_EVENTS.md §2 item 4 and
                   SIGNALR_PROTOCOL.md §3.2.24 items 1, 3, 5, 6 — and every
                   Power mutation site except the Relic stage's was emitting
                   nothing. This is building the documented contract.)
Status:            DONE (all five mutation sites emit; all four source values are
                   covered; the D-8 per-mutation composition is implemented and
                   tested; every backend and client suite passes — see
                   Completion Evidence.)
Risk:              HIGH (TASK_TYPES.md §4 — FEATURE baseline MEDIUM, "can be HIGH
                   if it touches combat / battle state." This touches the Power
                   write sites of four resolution stages, the resolution's event
                   sequence, and the GameServer.Domain.Cards module.)
Priority:          HIGH (the last blocker between the resolved TASK-150 record and
                   a contract-conforming realtime Power report.)
Primary Agent:     gameplay (TASK_TYPES.md §5 — Card/Relic/Boss Power semantics →
                   Gameplay. Supporting: backend for the Application-layer
                   resolution wiring, realtime for the wire projection.)
Supporting Agents: backend (BattleStateService step 13 and step 18b emission,
                   the Application-layer resolution sequence),
                   realtime (BattleEventWireProjection and the
                   SIGNALR_PROTOCOL.md §3.2.24 wire value),
                   testing (rule-derived scenarios for all five sources),
                   review
Workflow:          development/feature.md
Skills:            gameplay/gameplay-behavior-derivation,
                   gameplay/authority-determinism-audit,
                   realtime/realtime-protocol-validation,
                   testing/test-scenario-generation,
                   quality/implementation-review
                   (5 skills — Normal budget, tasks/README.md §12)
Dependencies:      TASK-150 (DONE — the decision record D-1 … D-8 this task
                   implements),
                   TASK-151 (DONE — applied D-6/D-7/D-8 to GAME_EVENTS.md §2 and
                   SIGNALR_PROTOCOL.md §3.2.24; AGENTS.md §17/§18 place the
                   contract change before the implementation),
                   TASK-133 (DONE — the Relic stage and the existing `"relic"`
                   emitter, whose behavior this task must not regress),
                   TASK-148 (DONE — client presentation of PowerChanged; NOT
                   modified by this task)
Blocks:            A contract-conforming realtime Power report for the Match,
                   Card, and Boss stages
```

---

## Objective

Implement the `PowerChanged` emission contract TASK-150 decided and TASK-151 documented: every authoritative gameplay mutation of `PetState.Power` emits exactly one `PowerChanged` from the stage that owns the mutation, carrying that mutation's signed `delta`, the resulting authoritative `power`, and the owning `source` — with the four-value vocabulary `"match"`, `"card"`, `"relic"`, `"boss"`.

---

## Current State

Before this task, only the Relic stage emitted. The other four mutation sites
wrote `PetState.Power` silently:

```text
#  Mutation site                              Stage (§17)     source    before
-  -----------------------------------------  --------------  --------  --------
1  Match resource generation                  Power, step 13  "match"   silent
     BattleStateService.cs (step 13)
2  Card / Pet Skill cost deduction            Card, step 14   "card"    silent
     CardCastExecutor.cs
3  Card Power effect (Power Charge +25)       Card, step 14   "card"    silent
     CardCastExecutor.cs
4  Relic Power effect                         Relic, step 11  "relic"   EMITTED
     RelicResolver.cs
5  Boss Skill secondary effect: Drain Power   Boss, step 18b  "boss"    silent
     BattleStateService.cs
```

`PowerChangeSource` carried only `Match`, `Card`, and `Relic`, and
`CardCastExecutor` accumulated every effect into one `newPower` and wrote it
once — the single-net composition TASK-150's consequence R-1 recorded as
non-conforming under D-8.

---

## Authoritative References

- `docs/02-technical/GAME_EVENTS.md` **§2 `PowerChanged`** — the payload source set (four values), item 2 (each value defined by the stage that owns the mutation; `"card"` is **not** cost-only), item 3 (a Card cast's Power change is reported as the mutations it owns), and **item 4** (owning-stage emission, "one mutation, one event", authoritative ordering, "a mutation that does not occur emits nothing")
- `docs/02-technical/SIGNALR_PROTOCOL.md` **§3.2.24** — the member table (`type`, `delta`, `power`, `source`), item 1 (`delta` carries the mutation's own sign; the sign is not inferred from `source`), item 2 (`power` is the resulting value), item 3 (the per-event four-value set), item 5 (owning-stage emission; `"boss"` = a Boss-owned Power mutation, Boss Drain Power the provisioned case), item 6 (one mutation, one event; authoritative order); **§3.2.2 item 5**; **§3.2.4** (string enum, contract name on the wire); **§3.2.5** (omission, never null)
- `docs/01-game-design/GAME_RULES.md` **§12** — Power's 0–100 range and its generation sources (the invariant this task must not re-derive); **§17** — the fixed resolution order: step 11 (Trigger Relics), step 13 (Update Power), step 14 (Resolve Player Effects), step 18b (Boss Skill); **§16** — the canonical event-name list containing `PowerChanged`; **§18** — server authority
- `docs/01-game-design/CARD_RULES.md` **§2** — Power Charge: cost 0, "Active Pet gains 25 Power"; **§3 item 4** — the cost deduction; **§3.6** — `EffectiveCardCost` composed once and used by validation, deduction, and reporting
- `docs/01-game-design/BOSS_RULES.md` **§6.3.1 item 2** — Drain Power's −20 flat Pet Power, instant, no duration, `max(0, Power − 20)`; **§7** — the Boss event list
- `docs/01-game-design/RELIC_RULES.md` **§8.3 item 3** — an Immediate Power grant is applied once through the Power write site; **§8.5** — Mana Crystal's `+10 Power`
- `docs/02-technical/GAME_STATE.md` **§2.3** (`PetState.Power`), **§2.3.1 item 9** (Drain Power is an immediate `PetState.Power` mutation, not a Status Effect), **§5.1** (the single write-back)
- `docs/02-technical/REDIS_STATE.md` **§4** (the single write-back under the `Sequence` compare-and-set; items 2 and 5)
- `docs/02-technical/ARCHITECTURE.md` **§2.1** (Application sequences; Domain owns rules), **§5** (anti-overengineering)
- `docs/00-overview/MVP_SCOPE.md` §1 — Power is IN scope
- `AGENTS.md` §7 (invent no rule), §9 (anti-overengineering), §10 (server authority), §11 (determinism), §12 (domain boundaries), §14, §15 (testing), §16 (task discipline), §17 (documentation), §20
- `tasks/backlog/TASK-150-resolve-powerchanged-source-semantics-for-boss-drain-and-card-power-charge.md` — the decision record D-1 … D-8 and consequence R-1
- `tasks/completed/TASK-151-apply-powerchanged-source-decisions-to-authoritative-documentation.md` — the contract application
- `src/backend/GameServer.Domain/Battle/PowerEvents.cs`, `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs`, `src/backend/GameServer.Domain/Relics/RelicResolver.cs`, `src/backend/GameServer.Domain/Match3/ResourceGenerator.cs`, `src/backend/GameServer.Application/Battle/BattleStateService.cs`, `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs`

---

## Scope

### In Scope

1. **`PowerChangeSource` gains the fourth documented value** — `Boss`, projected to the wire name `"boss"`.
2. **Match emission (step 13):** the Power stage writes the generated Power through `ResourceGenerator.ApplyPower` and emits `PowerChanged { source: "match" }` with the delta the write actually applied and the resulting `Power`.
3. **Card cost emission (step 14):** the Card stage's cost deduction emits `PowerChanged { source: "card" }` with the negative delta and the resulting `Power`.
4. **Card Power-effect emission (step 14):** a Card's Power-granting effect emits its **own** `PowerChanged { source: "card" }` with the positive delta and the resulting `Power`.
5. **D-8 composition:** a Card cast performing several Power mutations emits one event per mutation, in authoritative execution order — never one collapsed net event.
6. **Boss Drain emission (step 18b):** the Boss Response stage emits `PowerChanged { source: "boss" }` carrying the **actual** mutation the floor allowed (a 20 drain against Power 10 is `delta -10` at `power 0`).
7. **Relic regression preservation:** the existing step-11 `"relic"` emission is unchanged and not duplicated.
8. **No-op silence:** a Power mutation that does not occur emits nothing.
9. **Tests** for all five sources, the D-8 ordering, the clamping, and the no-op case, plus the wire value set.

### Out of Scope

- **Any documentation change.** `GAME_EVENTS.md`, `SIGNALR_PROTOCOL.md`, `GAME_STATE.md`, `GAME_RULES.md`, `BOSS_RULES.md`, `CARD_RULES.md`, and `RELIC_RULES.md` are unmodified: TASK-151 already applied the contract.
- **Modifying TASK-150, TASK-151, or TASK-148**, or any completed task (`TASK_LIFECYCLE.md` §3).
- **Any new event, wire member, SignalR method, or subscription.** The payload stays exactly `{ type, delta, power, source }`.
- **Any new Redis key, state representation, or persistence path.** `PetState.Power` remains the one authoritative Power value and travels in the existing single write-back.
- **Any new gameplay rule, Power formula, or clamp.** `ResourceGenerator.ApplyPower` remains the single 0–100 clamp site for the Match, Card, and Relic writers; the Boss Drain keeps `BOSS_RULES.md` §6.3.1 item 2's own `max(0, …)` floor.
- **Any client change.** TASK-148 already presents `PowerChanged` and forwards `source` without a client-side enum restriction; the frontend is unmodified and its suites are a regression check only.
- Any item listed as OUT in `docs/00-overview/MVP_SCOPE.md` §2.

---

## Implementation Requirements

```text
PowerChangeSource (GameServer.Domain/Battle/PowerEvents.cs)
  → add `Boss = 3` as the fourth documented value
  → document all four values as stage owners, not directions

CardCastExecutor (GameServer.Domain/Cards/CardCastExecutor.cs)
  → the cost deduction routes through ResourceGenerator.ApplyPower (the one clamp
    site) and emits one PowerChanged with source "card" when it moved the value
  → the Power effect arm emits its OWN PowerChanged with source "card"
  → the two are separate events, emitted in execution order (D-8)
  → neither is emitted when its mutation did not occur
  → PetState.Power is still written once, from the accumulated newPower

BattleStateService (GameServer.Application/Battle/BattleStateService.cs)
  → step 13: read the Power write site's own before/after and emit one
    PowerChanged with source "match" when the generation moved the value
  → step 18b: compute the drained value once, write it, and emit one
    PowerChanged with source "boss" carrying the ACTUAL delta
  → step 11 (Relic) emission is left exactly as it was

BattleEventWireProjection (GameServer.Api/Hubs/BattleEventWireProjection.cs)
  → doc only: the projected value set is the four documented names; the
    projection itself already carries the Domain value through unchanged
```

---

## Acceptance Criteria

- [x] `PowerChangeSource` carries exactly the four documented values, and `Boss` projects to the wire name `"boss"`
- [x] The Match resource generation at step 13 emits one `PowerChanged` with `source = "match"`, carrying the actual delta and the resulting `Power`
- [x] The Card cost deduction emits one `PowerChanged` with `source = "card"`, a negative delta, and the resulting `Power`
- [x] A Card Power effect emits its own `PowerChanged` with `source = "card"`, a positive delta, and the resulting `Power`
- [x] The Relic stage's step-11 emission still carries `source = "relic"` with the values it always did, and is neither duplicated nor regressed
- [x] Boss Drain Power at step 18b emits one `PowerChanged` with `source = "boss"` and is never mapped to `"relic"`
- [x] Boss Drain's event reports the **actual** clamped mutation, not the requested magnitude
- [x] A Card cast performing two Power mutations emits two events in authoritative execution order, never one collapsed net event
- [x] A Power mutation that does not occur emits no `PowerChanged`
- [x] Every emitted `delta` is the change the mutation actually applied, and every `power` is the resulting authoritative `PetState.Power`
- [x] The wire payload remains exactly `{ type, delta, power, source }` — no member added or removed
- [x] No new event type, SignalR method, subscription, or Redis key is introduced
- [x] `PetState.Power` remains the one authoritative Power value; no parallel Power state exists
- [x] PowerChanged emission stays inside the existing authoritative mutation flow: one load, one resolve, one CAS write-back, one published result
- [x] A failed CAS attempt publishes no accepted events
- [x] Zero files under `docs/` are modified
- [x] Zero files under `src/frontend/` are modified
- [x] TASK-150, TASK-151, and TASK-148 are unmodified
- [x] No gameplay rule, Power formula, clamp, or `source` value is invented

---

## Affected Files & Areas

```text
[x] src/backend/GameServer.Domain/Battle/PowerEvents.cs
[x] src/backend/GameServer.Domain/Cards/CardCastExecutor.cs
[x] src/backend/GameServer.Domain/Match3/BattleEvent.cs            (doc only)
[x] src/backend/GameServer.Application/Battle/BattleStateService.cs
[x] src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs   (doc only)
[x] tests/backend/GameServer.Domain.Tests/Cards/CardCastExecutorTests.cs
[x] tests/backend/GameServer.Application.Tests/Battle/PowerChangedEmissionTests.cs (new)
[x] tests/backend/GameServer.Api.Tests/RelicWireProjectionTests.cs
[x] tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs
[x] tests/backend/GameServer.Api.Tests/Hubs/BattleHubCardCastTests.cs
[x] tests/backend/GameServer.Api.Tests/Hubs/BattleHubPetSkillCastTests.cs
[ ] src/frontend/client/ — NONE
[ ] docs/ — NONE
```

---

## Testing Requirements

### Required Verification

```text
[x] Unit tests         — PowerChangeSource set; each stage's emission shape
[x] Integration tests  — the full Swap/CardCast pipeline, the wire batch, the CAS path
[x] Gameplay scenarios — Given/When/Then per GAME_RULES.md §17 stage order
```

Scenarios covered:

```text
Match      Power 0 + POWER match  → PowerChanged { delta +pool, power, "match" }
Match      Power 95 + POWER match → PowerChanged { delta +5,   power 100, "match" }
Card cost  Power 50, cost 10      → PowerChanged { delta -10,  power 40,  "card" }
Card gain  Power 10, Power Charge → PowerChanged { delta +25,  power 35,  "card" }
Card gain  Power 90, Power Charge → PowerChanged { delta +10,  power 100, "card" }
Card both  Power 50, cost 10 +25  → two events, -10@40 then +25@65, in order
Card none  cost 0, no Power effect → no PowerChanged
Relic      Power 0, Mana Crystal  → PowerChanged { delta +10,  power 10,  "relic" }
Boss drain Power 50, drain 20     → PowerChanged { delta -20,  power 30,  "boss" }
Boss drain Power 10, drain 20     → PowerChanged { delta -10,  power 0,   "boss" }
Boss drain Power 0,  drain 20     → no PowerChanged
Ordering   Relic → Match → Boss   → three events in step 11 → 13 → 18b order
Wire       each source value      → "match" | "card" | "relic" | "boss"
```

---

## Stop Conditions

Universal stop conditions in `AGENTS.md` §20 and `.ai/README.md` §13 apply.

- If TASK-150's decisions are found to conflict with another authoritative document: STOP per `AGENTS.md` §4.
- If a **sixth** unresolved `PetState.Power` mutation site is discovered: STOP and report it.
- If `PowerChanged` requires a **fifth** `source` value: STOP — the value set is a Product Owner decision.
- If the existing event infrastructure cannot represent the required ordering: STOP.
- If D-8 cannot be implemented without changing an authoritative contract: STOP.
- If CAS/retry would duplicate accepted `PowerChanged` events: STOP.
- If Boss Drain's semantics conflict with the existing authoritative Power rules: STOP.
- If the work requires a new gameplay decision, a new SignalR event/method, or a new Redis contract: STOP.

**None fired.** The mutation inventory remains exactly the five TASK-150 recorded, and no additional source value was needed.

---

## Completion Evidence

### Changed Files

- `src/backend/GameServer.Domain/Battle/PowerEvents.cs` — `PowerChangeSource` widened from three documented values to the four `GAME_EVENTS.md` §2 item 2 / `SIGNALR_PROTOCOL.md` §3.2.24 item 3 own: `Boss = 3` added, and each value's doc restated as the stage that owns the mutation rather than the direction of the change (`Match` step 13, `Card` step 14 covering both cost and gain, `Relic` step 11, `Boss` step 18b). `PowerChangedEvent`'s doc gained the four-value set, the clamped-delta reading (a drain the floor absorbs), and the one-mutation-one-event rule.
- `src/backend/GameServer.Domain/Cards/CardCastExecutor.cs` — the cost deduction now routes through `ResourceGenerator.ApplyPower` (the single 0–100 clamp site `GAME_RULES.md` §12 / `COMBAT_RULES.md` §1.1 fix) instead of raw subtraction, and emits its own `PowerChanged { source: "card" }`; the Power-effect arm replaced its local `Math.Clamp(…, 0, 100)` with the same shared write site and emits its **own** `PowerChanged`. Two mutations therefore produce two events in execution order (D-8), each suppressed when its mutation did not occur. `PetState.Power` is still written once, from the accumulated value.
- `src/backend/GameServer.Application/Battle/BattleStateService.cs` — step 13 (Update Power) reads the write site's own before/after and emits one `PowerChanged { source: "match" }` when the generation moved the value; step 18b (Boss Skill secondary effect) computes the drained value once, writes it, and emits one `PowerChanged { source: "boss" }` carrying the **actual** mutation. The step-11 Relic emission is untouched.
- `src/backend/GameServer.Domain/Match3/BattleEvent.cs` — doc only: `PowerChanged`'s summary and `ForPowerChanged`'s contract now state the four owning stages and the one-mutation-one-event rule.
- `src/backend/GameServer.Api/Hubs/BattleEventWireProjection.cs` — doc only: the projected `source` value set is the four documented names. No projection logic changed; the Domain value was already carried through unchanged.
- `tests/backend/GameServer.Domain.Tests/Cards/CardCastExecutorTests.cs` — existing event-sequence expectations updated for the new cost emission, plus four new tests: the Power Charge grant (`+25`, source `card`), the clamped grant (`+10` at the cap, not `+25`), the D-8 two-mutation ordering (`-10@40` then `+25@65`, asserted as two ordered events and never the collapsed `+15@65`), and the no-mutation case (no `PowerChanged`).
- `tests/backend/GameServer.Application.Tests/Battle/PowerChangedEmissionTests.cs` — **new**. Nine scenarios through the real Application pipeline: match generation, the match clamp, the Card cost, the Card grant, the Relic regression guard (including that step 11 precedes step 13), the Boss drain, the Boss drain's clamped floor, the Boss no-op, and a three-source ordering test asserting the delivered deltas reproduce the committed `Power`.
- `tests/backend/GameServer.Api.Tests/RelicWireProjectionTests.cs` — the source theory extended with `("boss", "boss")`; new tests for the exact four-member payload, the Boss drain's clamped delta, and a positive `"card"` delta; two new batch-projection tests (two card mutations preserved in order; a match/relic/boss batch).
- `tests/backend/GameServer.Api.Tests/ApiIntegrationTests.cs` — the `PowerChanged` item added to the wire member allow-list (`{ type, delta, power, source }`); the executor-order expectation now inserts the step-13 match emission at its documented position between the Passive/Relic reports and the damage instances.
- `tests/backend/GameServer.Api.Tests/Hubs/BattleHubCardCastTests.cs` — the accepted-cast batch now asserts the CardCast plus the cost `PowerChanged` (`-20` @ `30`, source `card`).
- `tests/backend/GameServer.Api.Tests/Hubs/BattleHubPetSkillCastTests.cs` — the Pet Skill batch now asserts CardCast, the cost `PowerChanged` (`-40` @ `10`, source `card`), then PetSkillCast, in that order.

### Validation Results

Full regression, every suite run in this session:

```text
GameServer.Domain.Tests          1467 passed, 0 failed, 0 skipped
GameServer.Application.Tests      498 passed, 0 failed, 0 skipped
GameServer.Infrastructure.Tests   363 passed, 0 failed, 0 skipped
GameServer.Api.Tests              297 passed, 0 failed, 0 skipped
frontend/client (vitest)          520 passed, 0 failed (18 files)
                                 ─────
Total                            3145 passed, 0 failed, 0 skipped
```

No pre-existing unrelated failure was present: every suite was green after the change, and the only tests that required updating were the ones whose event-sequence expectations the new emissions legitimately changed.

### Server Authority & Scope Verification

- [x] Confirmed zero files under `docs/` modified — TASK-151's revisions (`GAME_EVENTS.md` v2.11, `SIGNALR_PROTOCOL.md` v2.14) are byte-unchanged
- [x] Confirmed zero files under `src/frontend/` modified — the vitest suite was run as a regression check only, and it passes unmodified
- [x] Confirmed TASK-150, TASK-151, and TASK-148 are unmodified
- [x] Confirmed no new event type, SignalR method, subscription, or payload member
- [x] Confirmed no new Redis key, state member, or persistence path — `PetState.Power` is still the one authoritative value and rides the existing single write-back
- [x] Confirmed no parallel Power state was introduced (`PowerState`, `PendingPower`, `PowerChanges`, `PowerDeltaQueue` — none exist)
- [x] Confirmed `ResourceGenerator.ApplyPower` remains the one 0–100 clamp site for the Match, Card, and Relic writers, and the Boss Drain keeps its own documented `max(0, …)` floor
- [x] Confirmed the client remains non-authoritative — no Power arithmetic was added anywhere under `src/frontend/`
- [x] Confirmed emission sits inside the existing CAS flow: a single load, one resolution, one `Sequence`-guarded write-back, and the accepted result returned; a failed attempt emits nothing outward because events travel only on the returned result
- [x] Confirmed no gameplay rule, formula, clamp, or `source` value was invented
- [x] Confirmed adherence to MVP scope (`MVP_SCOPE.md` §1 — Power is IN)
