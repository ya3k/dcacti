# Game State

**Version:** 1.1
**Status:** Draft

> This document answers: **"What state exists during a running battle?"**
> It does not define what each value *means* (see `GAME_RULES.md` and
> domain rules) or how it is serialized/stored (see `REDIS_STATE.md`,
> `DATABASE.md`).

---

# 0. Staged Implementation

The battle system is implemented in stages. This document therefore defines
two state contracts, not one:

```text
Battle State Foundation     §2.0 — the minimal technical state that can
                              exist before gameplay systems exist
        ↓
   (progressively implemented: Match-3, Combat, Pet, Boss, RNG —
    each added by its own owning task)
        ↓
Full BattleState            §2 — the complete authoritative runtime state
                              required by the finished battle system
```

1. **§2 is the eventual gameplay contract.** It is not replaced, reduced, or
   redefined by §2.0. Every field in §2 remains required; none is removed.
2. **§2.0 is a staged implementation contract**, not a second design of the
   gameplay state. It exists so the runtime foundation (server → SignalR →
   `GameRuntime` → `BattleScene`) can be built and verified before Match-3,
   combat, Pets, Bosses, Cards, or Relics exist.
3. **§2.0 is a strict subset of §2.** Every field in §2.0 is a field of §2,
   with the same meaning and the same rules. §2.0 introduces no field that
   §2 does not have, and no field that §2 defines differently.
4. A field absent from §2.0 is **not yet implemented**, not **not required**.
   §2.0's field list grows toward §2 as each owning system is implemented,
   until the two lists are identical.

---

# 1. State Categories

```text
Persistent State           Survives across battles — PostgreSQL
                             (DATABASE.md)
Active Battle State          Exists only while a battle is running —
                             Redis (REDIS_STATE.md); this document's focus
Battle State Foundation       The staged subset of Active Battle State that
                             exists before gameplay systems do (§0, §2.0);
                             server-authoritative, not persisted
Transient Resolution State    Exists only within one Swap/Card resolution;
                             never stored, discarded after the resolution
                             completes
Client Presentation State     Exists only in the client; animation/UI
                             state with no gameplay authority
```

Only **Active Battle State** (including its **Battle State Foundation**
subset) and **Transient Resolution State** are defined in this document.
Persistent State fields live in `DATABASE.md`.

---

# 2. Active Battle State — `BattleState`

> This is the **full** contract — the eventual gameplay state. While the
> battle system is being built in stages, the subset defined in §2.0 is what
> exists. The two are not alternatives: §2.0 grows into §2 (§0).

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

`BattleId`, `Sequence`, and `Turn` are present from the start (§2.0). The
remainder are added as their owning systems are implemented.

## 2.0 Battle State Foundation

The minimal technical state that exists at this implementation stage, before
any gameplay system exists (§0).

```text
Battle State Foundation
├── BattleId
├── Turn          = 0
└── Sequence      = 0
```

### 2.0.1 Fields

```text
BattleId        Identity of the battle session. Identifies the battle and
                scopes the client's SignalR group membership
                (SIGNALR_PROTOCOL.md §1.2). No gameplay content — it does
                not select a Pet, Boss, or loadout.

Turn            Current Turn number. Same field and same meaning as
                BattleState.Turn (§2), GAME_RULES.md §2.

Sequence        Monotonic resolution counter. Same field and same meaning as
                BattleState.Sequence (§2, §5).
```

### 2.0.2 Initial Values

```text
Turn      = 0
Sequence  = 0
```

**`Turn = 0`** means that no player Swap/Action has yet been successfully
resolved. This introduces no new Turn rule: `GAME_RULES.md` §2.1 ("a Turn
represents one player Swap/Action") remains authoritative and unchanged, and
§2's example (`One Swap → Match 1 → Cascade → …` → 1 Turn) is consistent with
a pre-resolution value of `0`.

When action resolution is implemented, the owning task must state precisely
when the counter increments if the current documentation does not already do
so. **This document does not define that increment rule**, and §2.0 does not
imply one.

**`Sequence = 0`** means that no authoritative action resolution has yet
occurred. The rule in §5 is unchanged: `BattleState.Sequence` increments by
exactly 1 per successfully resolved action. `0` is therefore the only valid
value until the first resolution succeeds.

### 2.0.3 Fields Explicitly Not Present

§2.0 contains **no `Status` field**, and this document introduces none.

```text
Status, READY, STARTING, ACTIVE, PAUSED, FINISHED, WON, LOST — and any
similar lifecycle enum — are NOT part of Battle State Foundation.
```

1. A battle has no lifecycle state machine. It is started, and it ends.
2. Battle outcome is expressed as **events** — `BattleWon` / `BattleLost`
   (`GAME_EVENTS.md` §2, `GAME_RULES.md` §16) — not as a state field. §2.0
   does not convert them into one, and §2 has no such field either.
3. If a battle lifecycle state machine is later required, it must be
   introduced by its own design/ADR task, not added here.

The remaining §2 fields (`RngSeed`/`RngState`, `BoardState`, `PlayerState`,
`PetState`, `BossState`) are likewise not present in §2.0. They are gameplay
systems that do not exist yet; that is not a scope reduction of §2.

### 2.0.4 Persistence and Ownership

1. **Server-authoritative.** §2.0 is authoritative server state, exactly as
   §2 is (`GAME_RULES.md` §18, ADR-001). The client holds a synchronized
   presentation copy only and may never author it.
2. **Not persisted to Redis.** Foundation State is not written to
   `battle:{battleId}:state`. Redis stores the full active battle state
   (§2, `REDIS_STATE.md` §1–§2); a partial state must not be written there
   and presented as that contract. The boundary is documented in
   `REDIS_STATE.md` §7.
3. **Not persisted to PostgreSQL.** No foundation field is durable data
   (`DATABASE.md`).

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

`Sequence` starts at `0` (§2.0.2) and is `0` for as long as no action has
been successfully resolved. This rule is unchanged by §2.0 — the foundation
stage simply has no resolutions yet.
