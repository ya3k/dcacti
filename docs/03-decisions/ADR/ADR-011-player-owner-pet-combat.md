# ADR-011: Player as Account Owner, Pet as Combat Character, PetState as Battle Runtime Combat State

**Status:** Accepted
**Date:** 2026-09-24

## Context

Earlier drafts and the current Domain implementation used a nested
`PlayerState` under `BattleState` to hold both Match/Combo accounting
(`Combo`, `MatchCount`) **and** the battle's combat stats (`HP`, `MaxHP`,
`ATK`, `DEF`, `Crit`, `Power`) plus status/loadout collections
(`StatusEffects[]`, `EquippedRelics[]`, `EquippedCards[]`).

That shape conflicted with the game design's role model:

- `GAME_RULES.md` §1 and §14 treat the **Player** as the account/owner and
  the **Pet** as the combat character whose stats participate in the Damage
  Pipeline.
- `PET_RULES.md` §2 and §5 define Pet identity, progression, and the
  Pet Level formula against the Player as owner, not against a Player
  combat pool.
- `RELIC_RULES.md` §2 and `CARD_RULES.md` §1/§3 equip Relics and Cards for
  the **active Pet** for a battle, not onto a global Player loadout.
- `COMBAT_RULES.md` §1.1 documents combat stats as the **active Pet's**
  stats (or the Boss's), with no separate Player HP/ATK/DEF/Power pool.

Keeping combat fields under `PlayerState` made the technical contract and
the Domain type name imply a Player combat pool that no rule document
defines. Meanwhile the wire already used `playerState` only as a payload
label for `combo` + `matchCount` (`SIGNALR_PROTOCOL.md` §4.2) and
`finalPlayerHp` / `target="player"` as fixed protocol labels — renaming
those would be an API/SignalR contract change outside a documentation-only
redesign.

## Decision

Authoritative role model and state ownership:

```text
Player   → account / owner (persistent collection: Pets, Cards, Relics;
           no battle-time combat stats)
Pet      → combat character (equips Relic/Card loadout for battle;
             Level = clamp(floor(Player Level × Pet Level Multiplier), 1, 50)
             — config `decimal > 0`, floor before clamp; see ADR-012 and
             PET_RULES.md §5)
PetState → authoritative battle-time combat runtime state under BattleState
           (HP/MaxHP, ATK/DEF/Crit, Power, StatusEffects,
            EquippedRelics, EquippedCards, Passive*)
BattleState
├── BattleId, Sequence, RngSeed/RngState, BoardState, Turn
├── Combo, MatchCount          (Match/Combo accounting at the root)
├── PetState
└── BossState
```

1. **`PlayerState` is removed from the `BattleState` contract**
   (`GAME_STATE.md` §2). There is no nested Player battle-state node.
2. **Match/Combo accounting (`Combo`, `MatchCount`) lives at the
   `BattleState` root** (`GAME_STATE.md` §2.2) — not under Player and not
   under BoardState (`Cells[64]` only, ADR-010).
3. **Combat stats, status, and battle loadout live under `PetState`**
   (`GAME_STATE.md` §2.3). The active Pet is the damage/HP subject on the
   player side; `BossState.HP` is the Boss side.
4. **Ownership vs. equip is split:** Player FKs in PostgreSQL remain
   collection ownership (`DATABASE.md` §2); equipped Relic/Card sets are
   battle-scoped loadouts selected at `POST /api/battle/start` for the one
   active Pet (`API_CONTRACTS.md` §3). No global Player equip slots.
5. **No duplicate authoritative combat pools:** Player has no HP/ATK/DEF/
   Power columns or state fields. One combat-stat home: `PetState` (plus
   `BossState` for the Boss).
6. **Wire names are not renamed by this decision.** `playerState`,
   `finalPlayerHp`, and `target="player"` remain fixed protocol labels; doc
   descriptions correct them to active-Pet / BattleState-root semantics.
   Renaming any of them is a future protocol-breaking task, not this ADR.
7. **Pet Level formula** (design, not state): `Pet.Level = clamp(floor(Player
   Level × Pet Level Multiplier), 1, 50)` (config `decimal > 0`, not
   hard-coded; floor before clamp) — owned by `PET_RULES.md` §5. OPEN
   conflicts previously reported there (Player
   Level undefined; interaction with the 1–50 clamp) are **resolved by
   ADR-012**: Player Level is an MVP persistent account attribute
   (1–50, no combat stats), and the clamp bounds the floored formula
   result — see `PET_RULES.md` §5.1. Multiplier type, range, rounding,
   and clamp order are owned solely by `PET_RULES.md` §5. There is **no
   Pet XP system** and **no Evolution system**.

## Consequences

### Positive
- Technical state matches game-design roles; no implied Player combat pool.
- Relic/Card ownership (DB) and equip (battle loadout) are unambiguous.
- Single combat-stat home (`PetState`) avoids dual authority.

### Negative
- **Implementation mismatch:** Domain still nests combat stats and
  accounting under a type named `PlayerState`. Aligning code, Redis
  serialization projections, and tests is a follow-up implementation task —
  out of scope for this documentation-only change.
- Wire member names remain semantically misleading (`playerState` for
  Combo/MatchCount; `finalPlayerHp` for Pet HP) until a deliberate protocol
  version bump.
- ~~`MVP_SCOPE.md` does not define Player Level~~ — **resolved by
  ADR-012**: Player Level is listed IN in `MVP_SCOPE.md` §1 and fully
  specified in `PET_RULES.md` §5.

### Trade-offs
- Keeping wire names preserves client compatibility at the cost of
  temporary doc/code name drift until Future Work renames them.

## Alternatives Considered

### Option A — Keep `PlayerState` as combat home, rename only in prose
Rejected: perpetuates a Player combat pool no rule document defines and
keeps combat/loadout authority in the wrong role object.

### Option B — Rename wire fields (`playerState`, `finalPlayerHp`, `target`) now
Rejected for this task: API/SignalR contract change; requires protocol
versioning and client coordination — recorded as Future Work instead.

### Option C — Duplicate combat stats on both Player and Pet
Rejected: dual authoritative pools violate the single-source rule
(`GAME_STATE.md` §0 item 5 parallel representation ban; server authority
ADR-001).

## Related Documents

- `docs/01-game-design/GAME_RULES.md` (§1, §9, §12, §14, §17)
- `docs/01-game-design/PET_RULES.md` (§2, §5, §5.1 — former OPEN conflicts
  resolved per ADR-012)
- `docs/01-game-design/COMBAT_RULES.md` (§1.1, §3)
- `docs/01-game-design/RELIC_RULES.md` (§2, §3)
- `docs/01-game-design/CARD_RULES.md` (§1, §3)
- `docs/02-technical/GAME_STATE.md` (§2, §2.2, §2.3)
- `docs/02-technical/SIGNALR_PROTOCOL.md` (§3.2, §4.2, §4.3)
- `docs/02-technical/GAME_EVENTS.md` (§2 BattleWon/BattleLost)
- `docs/02-technical/API_CONTRACTS.md` (§3)
- `docs/02-technical/DATABASE.md` (§2, §3)
- `docs/02-technical/REDIS_STATE.md` (§7)
- `docs/00-overview/GDD.md` (§3, §9–§11)
- `docs/03-decisions/ADR/ADR-001-server-authoritative-battle.md`
- `docs/03-decisions/ADR/ADR-010-committed-swap-state.md`
