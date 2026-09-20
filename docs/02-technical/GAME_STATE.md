# Game State

**Version:** 1.0
**Status:** Draft

> This document answers: **"What state exists during a running battle?"**
> It does not define what each value *means* (see `GAME_RULES.md` and
> domain rules) or how it is serialized/stored (see `REDIS_STATE.md`,
> `DATABASE.md`).

---

# 1. State Categories

```text
Persistent State           Survives across battles — PostgreSQL
                             (DATABASE.md)
Active Battle State          Exists only while a battle is running —
                             Redis (REDIS_STATE.md); this document's focus
Transient Resolution State    Exists only within one Swap/Card resolution;
                             never stored, discarded after the resolution
                             completes
Client Presentation State     Exists only in the client; animation/UI
                             state with no gameplay authority
```

Only **Active Battle State** and **Transient Resolution State** are defined
in this document. Persistent State fields live in `DATABASE.md`.

---

# 2. Active Battle State — `BattleState`

```text
BattleState
├── BattleId
├── Sequence               (monotonic counter, incremented per resolved
│                            action — used for ordering/idempotency,
│                            see SIGNALR_PROTOCOL.md)
├── RngSeed / RngState      (server-seeded, see MATCH3_RULES.md §7,
│                            TDD.md §6)
├── BoardState
├── PlayerState
├── PetState                (the one active Pet for this battle)
├── BossState
└── Turn                    (current Turn number, GAME_RULES.md §2)
```

## 2.1 BoardState

```text
BoardState
├── Cells[64]                (Gem type per cell, MATCH3_RULES.md §1)
└── PendingSpecialGems[]      (Special Gems present on the board, with type
                               and position, MATCH3_RULES.md §5)
```

## 2.2 PlayerState

```text
PlayerState
├── HP / MaxHP
├── ATK / DEF / Crit          (base + active modifiers)
├── Power                      (0–100, GAME_RULES.md §12)
├── Combo                       (current Combo for the in-progress Swap,
│                                resets to 0 between Swaps, GAME_RULES.md §5)
├── StatusEffects[]              (Burn/Shield/Buff-Debuff instances,
│                                COMBAT_RULES.md §5)
├── EquippedRelics[]              (3–5, slot order fixed at battle start,
│                                RELIC_RULES.md §4)
├── EquippedCards[]                (3 Basic Cards + 1 Pet Skill Card)
└── MatchCount                      (cumulative Matches this battle,
                                    GAME_RULES.md §3)
```

## 2.3 PetState

```text
PetState
├── PetId / Identity
├── Element
├── Tier / Star / Level
├── PassiveProgress             (current count vs. threshold,
│                                PASSIVE_RULES.md §2)
└── PassiveResetOverride          (only present if this Pet's Passive uses
                                  non-default reset behavior,
                                  PASSIVE_RULES.md §4)
```

## 2.4 BossState

```text
BossState
├── BossId / Identity
├── Element
├── HP / MaxHP / ATK / DEF
├── State                        (internal enum, e.g. Idle/Charging/
│                                Enraged — BOSS_RULES.md §5)
├── PassiveProgress                (shape depends on the Boss's declared
│                                  trigger category, BOSS_RULES.md §3)
└── StatusEffects[]                 (effects applied to the Boss)
```

---

# 3. Transient Resolution State

Exists only for the duration of resolving one Swap or one Card Cast; never
persisted, never sent to the client as a single blob (only the resulting
Battle Events are sent — see `GAME_EVENTS.md`).

```text
ResolutionContext
├── MatchesThisResolution[]     (each with shape, tier, Cascade depth)
├── ComboThisResolution
├── RelicsTriggeredThisResolution[]
├── DamageInstancesThisResolution[]
└── EventsEmitted[]             (ordered, per GAME_RULES.md §17)
```

`ResolutionContext` is built and discarded entirely within
`BattleResolutionService` (`ARCHITECTURE.md` §4) for a single action.

---

# 4. Client Presentation State

Not authoritative and not defined exhaustively here — owned entirely by the
client (`ARCHITECTURE.md` §1, `client/src/game/`, `client/src/ui/`). Examples: which
animation is currently playing, camera position, UI panel open/closed. None
of this is reconstructable from or required by the server.

---

# 5. Versioning & Concurrency

`BattleState.Sequence` increments by exactly 1 per successfully resolved
action and is the basis for:

1. Optimistic concurrency when writing to Redis (`REDIS_STATE.md` §4).
2. Client-side detection of missed/out-of-order events
   (`SIGNALR_PROTOCOL.md`).

No other field is used for concurrency control.
