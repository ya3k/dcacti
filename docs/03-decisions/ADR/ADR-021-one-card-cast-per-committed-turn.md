# ADR-021: One Card Cast Per Committed Match-3 Turn

**Status:** Accepted
**Date:** 2026-10-04

## Context

`CARD_RULES.md` §3 item 5 and `MATCH3_RULES.md` §8.1 item 5 establish that a
Card cast does not consume a Turn, does not interact with the Combo system, and
is not board resolution. Those rules are intentional and remain in force: a
Card is an **auxiliary** action taken within the Match-3 Turn, not a Turn of its
own.

`TASK-191` (balance-pass analysis, B-02) observed a consequence those rules did
not bound. The cast path validated only three conditions — the Card is in the
active Pet's loadout, its category is Basic or PetSkill, and current Power is at
least the effective cost — and placed **no limit on how many casts a player
could resolve between two committed Swaps**. A `0`-cost Card could therefore be
cast repeatedly, and because `CardCastExecutor` emits `BattleWon` the moment
Boss HP reaches zero, a damage Card could end a battle with no committed Swap
and no Boss response at all:

```text
Power Charge ×4   (cost 0 each, no cast limit)
    ↓
Damage Card
    ↓
Boss defeated — 0 Turns, 0 Boss responses
```

Boss mechanics — the documented primary source of difficulty — never fired,
because the Boss acts only inside Swap resolution.

This was a **balance defect, not an implementation defect**: the code faithfully
implemented the rules as written. The rules themselves left the number of casts
per Turn undefined. Resolving it is a gameplay rule change, so it required an
explicit Product Owner decision (`GAME_RULES.md` §20, `AGENTS.md` §7/§17).

## Decision

`TASK-191` §6 **Q-4** was answered by the Product Owner:

```text
Q-4 → OPTION B — ONE CARD CAST PER TURN
```

**A player may successfully cast at most one Card during each committed
Match-3 Turn.**

1. **The restriction is a cast-count constraint, not Turn consumption.** The
   Match-3 Turn remains the authoritative unit of combat progression. A Card
   cast does **not** increment `Turn`, does **not** resolve the Match-3 board,
   and does **not** independently trigger the normal Boss response.
2. **The limit is per committed Match-3 Turn.** After one successful cast,
   further casts are **rejected** until the next committed Match-3 Turn. A
   rejected cast changes no state and spends no Power
   (`CARD_RULES.md` §3 item 3).
3. **The Match-3 Swap still advances the Turn and resolves the Boss response.**
   The cycle is:

   ```text
   Committed Match-3 Turn
       ├── at most 1 successful Card cast   (auxiliary action)
       └── Match-3 swap / turn resolution
               ↓
            Turn++
               ↓
            Boss Response
               ↓
            next Turn — Card cast available again
   ```

4. **The existing Boss-response mechanism is the only one.** Because a cast can
   no longer be chained between Swaps, Boss retaliation arrives through the
   existing step-18 pipeline on the committed Swap. No second Boss-damage,
   cooldown, passive, or status path is introduced.
5. **The rule is server-authoritative.** Enforcement belongs in the cast
   validation path (`GAME_RULES.md` §18, ADR-001); the client only reflects the
   server's rejection. The rejection is a new cast-validation failure reason
   alongside the existing ones and uses the existing rejection/error
   conventions — it does not silently mutate state.

This ADR records the decision only. The implementing task owns the code.

## Rejected Alternatives

### Option A — Card cast consumes a Turn and triggers the normal Boss response

Rejected. It would **fundamentally couple Cards to the Match-3 Turn system**,
contradicting `CARD_RULES.md` §3 item 5 and `MATCH3_RULES.md` §8.1 item 5, both
of which remain in force. It would also collapse the documented "spend now vs.
save for the Skill" tension (`GDD.md` §10) by making every Card cast cost a full
board resolution and a Boss attack — turning auxiliary actions into the primary
turn loop. Option B removes the degenerate loop without that coupling.

### Option C — Keep unlimited free casts and only change Power Charge's cost

Rejected. It addresses the *fuel* (B-01) but not the *mechanism* (B-02): the
underlying unlimited zero-risk card chain survives, because any `0`-cost or
cheap Card can still be chained arbitrarily between Swaps and the Boss still
never responds. It would leave the battle winnable with no committed Swap.

## Consequences

- The `Power Charge ×4 → Damage Card` zero-risk kill loop is no longer
  expressible: at most one of those five casts can resolve per committed Turn.
- Boss passives and Skills — the documented difficulty source (`GDD.md` §12,
  `BOSS_RULES.md` §7) — become reachable again, because progression now requires
  committed Swaps.
- Card casts keep their existing independence: no `Turn` increment, no board
  mutation, no Combo interaction. Only the **number** of casts per Turn is
  bounded.
- A new cast-rejection reason is required in the cast-validation vocabulary. It
  is a validation failure, not a rejected Swap: it writes nothing, spends no
  Power, applies no effect, and emits only the rejection to the client
  (`CARD_RULES.md` §3 item 3).
- Nothing about Card costs, effect magnitudes, Power Charge's `0` cost, Boss
  values, Pet/Relic/Element balance, or the Match-3 board changes. B-01 and the
  other TASK-191 balance items remain separate, undecided decisions.

## Related Documents

- `CARD_RULES.md` §3 — the cast rule and the one-cast-per-Turn constraint
- `MATCH3_RULES.md` §8.1, §8.2 — what does and does not begin a Turn
- `GAME_RULES.md` §2, §11, §17, §18 — Turn definition, Card rules, resolution
  order, server authority
- `GAME_RULES.md` §20 — rule change policy
- `TASK-191` §3 B-02, §6 Q-4 — the analysis and the approved decision
- ADR-001 — server-authoritative battle resolution
