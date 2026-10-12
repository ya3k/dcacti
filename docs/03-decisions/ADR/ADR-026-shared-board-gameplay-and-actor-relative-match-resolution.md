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
   - If a cascade resolution leaves zero valid swaps (`BoardGenerationValidator.HasValidSwap == false`), the board is deterministically reshuffled using the server PRNG stream (`Pcg32`), guaranteeing a valid swap and emitting `BoardReshuffled`.

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

## Related Documents

- `docs/00-overview/MVP_SCOPE.md` §1
- `docs/01-game-design/GAME_RULES.md` §1.3, §2, §14, §15, §17
- `docs/01-game-design/MATCH3_RULES.md` §1.4, §2, §8
- `docs/01-game-design/COMBAT_RULES.md` §2, §3, §4
- `docs/01-game-design/BOSS_RULES.md` §3, §4, §6
- `docs/02-technical/GAME_STATE.md` §2.1, §2.4, §5
- `docs/02-technical/SIGNALR_PROTOCOL.md` §3, §3.1, §3.2
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-008-battle-recovery.md`
- `docs/03-decisions/ADR/ADR-025-swap-action-preconditions-and-idempotency.md`
- `tasks/active/TASK-258-shared-board-gameplay-contract-audit-and-design.md`
