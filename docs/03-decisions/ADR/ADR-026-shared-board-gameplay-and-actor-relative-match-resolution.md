# ADR-026: Shared-Board Gameplay and Actor-Relative Match Resolution

**Status:** Accepted
**Date:** 2026-10-11
**Deciders:** Product Owner, Systems Designer, Backend Architecture, Client Architecture
**Consulted:** AGENTS.md, GDD.md, GAME_RULES.md, MATCH3_RULES.md, COMBAT_RULES.md, BOSS_RULES.md, GAME_STATE.md, SIGNALR_PROTOCOL.md

## Context

In the initial MVP design (`GAME_RULES.md` §1.3, `BOSS_RULES.md` §3.1), the Match-3 board was exclusively manipulated by the player to generate combat resources for their Pet. The Boss did not interact with the board, instead reacting automatically after player damage by charging skills via player match counts and falling back to a fixed-damage basic attack (`Base Damage = Boss.ATK` at `GAME_RULES.md` §17 step 18c) independently of the board.

This asymmetric model led to several gameplay and design deficiencies:
1. **Passive Boss Combat:** The Boss functioned as an inert target with automatic retaliation rather than an active combatant on the board.
2. **Resource Dead-Ends:** Defensive gems (`DEF`) generated a `DefensePool` that was discarded in `BattleStateService.cs` without having any effect in combat.
3. **Decoupled Combat:** Board resolution was partially decoupled from damage: both sides dealt baseline attack damage regardless of whether `ATK` gems were matched.

To resolve these deficiencies, the Product Owner approved the following authoritative gameplay direction (**D1–D7**):
> "Player and Boss share one Match-3 board. Both sides act by making valid swaps on that board. The type of cells matched determines the effect; the acting side determines ownership and target. Neither side performs a default fixed-damage attack independently of the board."

## Decision

1. **Shared Authoritative Board:**
   - Player and Boss share a single 8×8 grid stored in `BattleState.BoardState` (`Cells[64]`).
   - The board is a shared mutable state throughout the battle. Both sides read from and write to this identical board.

2. **Turn Alternation & Atomic Round Transaction (D4):**
   - A single battle Round encapsulates two sequential phases:
     $$\text{Round } N = \text{Player Action Phase} \rightarrow \text{Boss Response Phase} \rightarrow \text{Step 19a End-Round}$$
   - `BattleState.Turn` represents the Round counter and advances by exactly 1 upon the commit of the atomic round: `Turn = state.Turn + 1`.
   - `BattleState.Sequence` increments by exactly 1 per committed round write-back: `Sequence = state.Sequence + 1`.
   - All mutations across Player phase, Boss phase, status effects, and counters are persisted to Redis in a single atomic Compare-And-Set write (`TryUpdateAsync`). If CAS fails or an exception occurs prior to commit, zero state is persisted.

3. **ATK Damage Gating (D1):**
   - Damage may be calculated and applied **only when the acting side produces a valid ATK-gem match** (`ClearedGemCounts.Atk > 0`).
   - The acting side's ATK stat (`PetState.ATK` or `BossState.ATK`) contributes to damage **only after** an ATK match is confirmed.
   - If zero ATK gems are matched, no attack occurs, the Damage Pipeline does not run, and zero attack damage is dealt.
   - Applicable attack modifiers (e.g. `NextAttack` modifiers) are preserved on non-ATK turns and are not consumed without a qualifying ATK match (`COMBAT_RULES.md` §3.3 item 8).

4. **Actor-Relative Cell Effects:**
   - **`ATK` Gems:** Generates attack damage targeting the opponent, gated strictly on a confirmed ATK match (D1).
   - **`DEF` Gems:** Generates temporary Shield absorption pool on the acting actor (`PetState.ActiveStatusEffects` for Player; `BossState.ActiveStatusEffects` for Boss) using `StatusEffectType.Shield`.
   - **`HP` Gems:** Generates Heal pool restoring HP to the acting actor (Pet HP for Player; Boss HP for Boss), clamped to `MaxHP`.
   - **`POWER` Gems (D2):** Generates Power for the active Pet (range 0–100) during Player turn, or increments `BossState.SkillCharge` during Boss turn. Player POWER matches never charge the Boss.

5. **Elimination of Default Fixed-Damage Attacks:**
   - Both sides deal attack damage only through confirmed ATK-gem matches.
   - The legacy fallback basic attack (`Base Damage = Boss.ATK` at `GAME_RULES.md` §17 step 18c) is **RETIRED**.

6. **Boss Skills Non-Damaging Structural Contract (D3, D5, D6, D7):**
   - Direct damage from Boss skills is prohibited by default (`BaseDamage = 0` on all five MVP skills).
   - Un-gated damage-over-time (such as Burn DoT) is removed.
   - Skills modify the board, resources, or combat modifiers:
     - **Hỏa Long (`flame-burst`, D5 — Option 5B):** Direct damage and Burn DoT removed; applies a temporary **Boss ATK buff** to `BossState.ActiveStatusEffects`.
     - **Thủy Ma (`drain-power`):** Direct damage removed; preserves existing -20 flat Pet Power drain (`BOSS_RULES.md` §6.3.1 item 2).
     - **Mộc Yêu (`root`):** Direct damage removed; preserves existing -30% Pet ATK debuff for 2 Turns (`BOSS_RULES.md` §6.3.1 item 3).
     - **Sơn Thạch Vệ (`earthquake`, D6 — Option 2A):** Direct damage removed; applies a temporary **Boss Shield** to `BossState.ActiveStatusEffects`.
     - **Kim Lôi Vương (`thunder-strike`, D7 — Option 3A):** Direct damage removed; applies a trigger-based modifier buffing the Boss's next qualifying ATK match (`ExpiryCondition = "NextQualifyingAttack"`).

7. **Persistence vs. Event Delivery Boundaries (D4):**
   - Single-key Redis CAS (`TryUpdateAsync`) guarantees single-record state persistence atomicity.
   - Redis CAS does **not** provide distributed two-phase commit with SignalR or guarantee exactly-once event delivery across network drops.
   - Reconnection and recovery remain strictly governed by `ADR-008` (state snapshot resynchronization via `GetBattleState`); there is no event replay log.
   - An idempotent retry matching `LastCommittedSwapPair.ActionId` returns an acceptance response (`SwapResponse(Accepted: true)`) without re-executing or re-broadcasting events; full state recovery requires client snapshot fetch (`GetBattleState`).

8. **Deterministic Boss AI & Deadlock Reshuffling:**
   - The Boss selects moves via a pure, deterministic scoring function over immediate first-pass legal swaps (from the 112 orthogonal candidate swaps), tie-breaking by lowest cell index, consuming zero unseeded PRNG.
   - Deadlock recovery is a **server-internal operation of that selection**: when the Boss's move selection finds no legal match-producing swap on the board the Player's action and cascades left, the board is replaced with a deterministically generated valid one (`BoardGenerator.Generate`, bounded by `BoardGenerator.MaxAttempts`) on the same server PRNG stream (`Pcg32`), and the Boss then selects its move on the reshuffled board (`BossAiEngine.SelectMove`).
   - **The authoritative rule is `MATCH3_RULES.md` §1.6, not this item.** §1.6 owns what a deadlocked stable board is, the playable-board invariant and its non-terminal scope, the commit-boundary evaluation point, the deterministic mechanism and its bounded retry, the terminal-board exemption, and the relationship between the Boss's pre-move recovery and the commit-boundary one. This item records only *why* the operation is server-internal, deterministic, and stream-preserving; it neither restates nor extends §1.6, and a change to the recovery rule is a change to §1.6.
   - **The runtime predicate is the legal-move enumeration, in both recoveries.** The deadlock test is the Boss move selection's own enumeration of legal match-producing swaps (`BossAiEngine.EnumerateLegalMoves` / `BossAiEngine.SelectMove`), which is also the predicate `MATCH3_RULES.md` §1.6 evaluates at the commit boundary; `BoardGenerationValidator.HasValidSwap` is a generation-time helper (§1.4.1) that the runtime path does not call.
   - **The reshuffle emits no Battle Event, and none is promised.** No `BoardReshuffled` event, discriminator, or payload member exists — the canonical event-name list (`GAME_RULES.md` §16), the event semantics (`GAME_EVENTS.md` §2) and the closed wire discriminator set (`SIGNALR_PROTOCOL.md` §3.2.2) contain none, and no code path emits one. The reshuffle becomes authoritative only through the Round's single `BattleState` write-back — the committed board is the board that follows from the reshuffled one — and it reaches the client as the Round's committed board in the state push (`GAME_STATE.md` §5.1, `SIGNALR_PROTOCOL.md` §3.1 items 1–3 and §4). The client therefore cannot identify the reshuffle operation from the delivered stream — an accepted limitation of the contract, recorded by the event list's owner at `GAME_EVENTS.md` §3 item 8, not a missing event.

## Unresolved Balance Choices (Explicitly Preserved)

The following parameters are non-blocking balance tuning decisions and are explicitly preserved as unresolved pending implementation tuning:
1. **`SkillCharge` Unit Metric:** Whether `SkillCharge` counts *POWER matches* (+1 per match, preserving thresholds 4–6) or *POWER gem units* (+10 per gem, requiring threshold re-scaling to 30–60).
2. **`SkillCharge` Overflow / Clamp:** Whether accumulation clamps at `ChargeRequirement` or continues uncapped during cooldown.
3. **Hỏa Long ATK Buff Compounding:** Whether `flame-burst` ATK buff and `boss-hoa-long-rage` (+20% passive) compound multiplicatively (`StatusEffectLifecycle.cs:752`), additively, or refresh/override each other.
4. **Numeric Magnitudes:** Specific Shield points for Earthquake, buff percentages and durations for Flame Burst and Thunder Strike.

## Alternatives Considered

- **Separate Authoritative Commits (Two Distinct Redis Writes):** Committing Player turn, then running an asynchronous worker/timer for Boss turn. Rejected: Introduces complex server-side timer state, race conditions upon disconnect/reconnect, and doubles Redis CAS overhead.
- **Full Cascade Lookahead Simulation in Boss AI:** Having Boss AI simulate refills and multi-pass cascades before choosing a move. Rejected: Requires branching PRNG state draws or burns server entropy, creating non-deterministic lookahead and test fragility. First-pass scoring is clean, fast, and fully deterministic.

## Consequences

### Positive
- High tactical depth: Player and Boss interact on a dynamic, shared board where every move changes the options available to the other side.
- Complete mechanical symmetry: All four cell types (`ATK`, `DEF`, `HP`, `POWER`) have meaningful, actor-relative effects.
- Zero un-gated damage: Eliminates unfair, unavoidable damage ticks outside active board matches.
- Full server authority and offline determinism are preserved.

### Negative
- Requires synchronized updates across game rules (`GAME_RULES.md`, `MATCH3_RULES.md`, `COMBAT_RULES.md`, `BOSS_RULES.md`) and technical contracts (`GAME_STATE.md`, `SIGNALR_PROTOCOL.md`).
- Requires extending SignalR protocol to transmit Boss swap coordinates (`SwapStarted`) for client presentation.

## Amendment — Deadlock-Reshuffle Reporting (TASK-265)

The first revision of Decision item 8 stated that the deadlock reshuffle was performed
"…guaranteeing a valid swap and **emitting `BoardReshuffled`**", and named
`BoardGenerationValidator.HasValidSwap` as the runtime predicate. Neither statement was
accurate:

- **No owning document ever defined that event.** `GAME_RULES.md` §16's canonical
  event-name list does not list it, `GAME_EVENTS.md` §2 gives it no payload,
  `SIGNALR_PROTOCOL.md` §3.2.2's discriminator set is closed against events
  `GAME_EVENTS.md` §2 does not define, and the Domain `BattleEventType` has no such
  member. No code path emits it. The name entered the repository as an *engineering
  proposal* in the `TASK-258` audit (row C-TRX-03) and was carried into item 8 by the
  `TASK-259` documentation synchronization without ever being added to the event list's
  owner. An ADR may not create a contract: `docs/03-decisions/README.md` §3 makes the
  technical document the owner of the *what*, and precedence places `GAME_EVENTS.md`
  above the ADR (`AGENTS.md` §2, `GAME_RULES.md` §21).
- **The runtime predicate is not `HasValidSwap`.** The deadlock test is the Boss move
  selection's own enumeration of legal match-producing swaps
  (`BossAiEngine.SelectMove`); `BoardGenerationValidator.HasValidSwap` is a
  generation-time helper (`MATCH3_RULES.md` §1.4.1) that the runtime path does not call.

**Corrected rule (Decision item 8, as amended).** Item 8's decision is unchanged —
deadlock recovery is deterministic, server-side, and consumes only the server PRNG
stream — but it is **server-internal and reported by state, not by an event**: the
reshuffle becomes authoritative only in the Round's single write-back, whose committed
board is the board that follows from it, and that board is delivered once per Round by
`SIGNALR_PROTOCOL.md` §4's state push. No event is added for the operation
(`GAME_EVENTS.md` §3 item 8). The client cannot identify the reshuffle from the delivered
stream; that limitation is deliberate and does not prevent presenting the Boss's board
action honestly (TASK-264).

**Why the record is amended in place and no ADR is created or superseded.** No decision
is reversed: the recovery mechanism, its determinism, the Boss AI's scoring, and the
Round's atomicity are all unchanged, so the superseding path of
`.ai/workflow/architecture/adr-change.md` §3 does not apply. The amendment instead makes
this record stop contradicting the event owner, which is what `docs/03-decisions/README.md`
§3 requires of an ADR — the same in-place-amendment treatment `ADR-018` received for its
Decision 12. The option of defining the event was considered and rejected because no
consumer requires it and an event would not deliver the board the client would need to
animate it (`TASK-265` §Decision).

## Amendment — Rule Ownership of Deadlock Recovery (TASK-267)

Decision item 8 described the recovery operation without naming the document that owns
the rule. `TASK-266` established that the rule had no authoritative game-design or
technical owner outside this ADR, and the Product Owner decisions recorded for it
(D1–D4) place the rule in the domain document per `AGENTS.md` §2's precedence — a
specific domain rule governs an ADR.

**Ownership recorded, no decision reversed.** `MATCH3_RULES.md` §1.6 now owns what a
deadlocked stable board is, the playable-board invariant and its non-terminal scope, the
commit-boundary evaluation point, the deterministic mechanism and its 64-attempt bound,
the terminal-board exemption, and the relationship between the Boss's pre-move recovery
and the commit-boundary one. Item 8's own decision is unchanged: recovery is
deterministic, server-internal, consumes only the server PRNG stream, and is reported by
the state it commits rather than by an event. Two clarifications were also folded into
item 8 — that the runtime predicate is the legal-move enumeration in both recoveries
(the `TASK-265` amendment's finding), and that a *second* recovery now exists at the
Round commit boundary while the Boss's pre-move recovery is retained. This is the same
in-place-amendment treatment the `TASK-265` amendment received, for the same reason: an
ADR may explain a decision, but it may not own a rule the domain document owns.

## Amendment — DEF Gems Grant a DEF Stat Buff, Not a Shield (TASK-299)

Decision item 4's **`DEF` Gems** bullet said a DEF match "Generates temporary
Shield absorption pool on the acting actor … using `StatusEffectType.Shield`."
That conversion is **retired** by the operator's approved gameplay decision for
`TASK-299`: a DEF match now grants a temporary **DEF stat buff** instead. The
actorship, the actor-relative ownership, the `+5` per-Gem base output, and the
"cell type determines the effect, acting side determines ownership and target"
principle are unchanged; only the effect's *kind* changes.

**Corrected rule (Decision item 4, DEF bullet, as amended).** A confirmed DEF-Gem
match grants the **acting actor** (the Pet for a Player action, the Boss for a
Boss action) a Turn-based `BuffDebuff` instance with `TargetStat = "DEF"` — the
`"DefenseBuff"` identity — whose `Magnitude` is that action's generated
`DefensePool` and which raises that actor's **effective DEF** for the opponent's
next attack. It creates **no** Shield, no absorption pool, and no
`StatusEffectType.Shield` instance. The buff is refresh-not-stack, its base stat
is never overwritten, and it expires at the documented Turn boundary (one
`RemainingTurns` for the Pet's buff, two for the Boss's, because the Player acts
before the Boss inside one atomic Round — the same single "opponent's next attack
phase" window expressed for each side).

**Rule ownership.** The rule itself is owned by `COMBAT_RULES.md` — §2's DEF-Gem
output row and actor-relative bullet, §3.2's effective-DEF statement, and the new
§5.4.6 (Pet side, including the application/stacking/Turn-window statements) and
§5.5.6 (Boss side). This ADR records only *why* the representation is the
existing `StatusEffects[]` stat-modifier path rather than a Shield: the state
model already carries `TargetStat`-consumed `BuffDebuff` instances with a Turn
countdown and a refresh-not-stack default (`GAME_STATE.md` §2.3.1), so the change
introduces no new state member, no new Status Effect `Type`, no new collection,
and no new protocol member. `GAME_RULES.md` §17 step 18a's resource list is
reconciled to match.

**Why the record is amended in place and no ADR is created or superseded.** No
architectural decision is reversed: shared board, actor-relative resolution, the
atomic Round, the `StatusEffects[]` representation, and the authoritative model
are all unchanged, so `.ai/workflow/architecture/adr-change.md` §3's superseding
path does not apply. The amendment instead makes this record stop contradicting
the owning domain rule, which is what `docs/03-decisions/README.md` §3 requires of
an ADR — the same in-place-amendment treatment the `TASK-265` and `TASK-267`
amendments to this same record, and `ADR-018`'s Decision 12 amendment, received.
The unchanged items remain item 1 (shared board), item 2 (Turn/atomic Round),
item 3 (ATK gating), item 5 (no default fixed-damage attacks), item 6 (Boss skill
contract), item 7 (persistence/event boundaries), and item 8 (deterministic Boss
AI); the DEF bullet of item 4 is the only corrected text, and the HP, POWER, and
ATK bullets of item 4 are untouched.

## Related Documents

- `docs/00-overview/MVP_SCOPE.md` §1
- `docs/01-game-design/GAME_RULES.md` §1.3, §2, §14, §15, §17
- `docs/01-game-design/MATCH3_RULES.md` §1.4, §1.6, §2, §8
- `docs/01-game-design/COMBAT_RULES.md` §2, §3, §4, §5
- `docs/01-game-design/BOSS_RULES.md` §3, §4, §6
- `docs/02-technical/GAME_STATE.md` §2.1, §2.4, §5
- `docs/02-technical/GAME_EVENTS.md` §1, §2, §3 item 8
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3, §3.1, §3.2
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md`
- `docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md`
- `tasks/completed/TASK-258-shared-board-gameplay-contract-audit-and-design.md`
