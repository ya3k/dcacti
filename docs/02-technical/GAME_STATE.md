# Game State

**Version:** 2.7 (§2.4 `BossId` denotation fixed per TASK-046 — the tree's
`BossId / Identity` entry is the canonical technical Boss Identity
(`BOSS_RULES.md` §6.4, e.g. `boss-hoa-long`), not a display name and not
`BossDefinitionId`; no second identity field and no display-name member is
added to `BossState`; prior 2.6: §2.8 Battle Identity — `BattleState.PlayerId` added to the
full §2 contract as the battle-creation owner identity that sources
`BattleResult.PlayerId` (`DATABASE.md` §1); server-authoritative, excluded
from every wire projection (`SIGNALR_PROTOCOL.md` §4, §7.1), ADR-014;
§2.3 `PetId` denotation fixed to the owned Pet instance
(`Pet.PetInstanceId`); §2.0.3 staging list updated; prior 2.5: §2.3
`PetState.EquippedCards[]` element representation
stated as `CardDefinitionId` — definition-based, repeated entries repeat
the same definition per the CARD_RULES.md §1 loadout copy limit, order
non-semantic; card-side "underlying instances" wording corrected — Cards
are unlocks, not instances; prior 2.4: `PetState.EquippedRelics[]` element representation stated as
owned Relic instance identity, owned by `RELIC_RULES.md` §2.2, with the
resolved slot-index source and duplicate-selection policy referenced from
§2.3–§2.5; prior 2.3: two blocking OPEN records cited — equip slot index
source and duplicate-selection policy; prior 2.2: Player/Pet role model per
ADR-011 — `PlayerState`
removed from §2; Combo/MatchCount at BattleState root; combat stats +
StatusEffects + EquippedRelics/EquippedCards moved to `PetState`; wire
member name `playerState` retained as a fixed protocol label; prior 2.1:
§2.4.3 Turn-increment citation corrected to MATCH3_RULES §8.1 / §5.1)
**Status:** Draft

> This document answers: **"What state exists during a running battle?"**
> It does not define what each value *means* (see `GAME_RULES.md` and
> domain rules) or how it is serialized/stored (see `REDIS_STATE.md`,
> `DATABASE.md`).

---

# 0. Staged Implementation

The battle system is implemented in stages. This document therefore defines
the staged contracts below, plus the full contract they grow into:

```text
Battle State Foundation     §2.0 — the minimal technical state that can
                              exist before gameplay systems exist
        ↓
Board Foundation State      §2.0.5 — §2.0 plus the authoritative Match-3
                              board and the RNG state that generates it
        ↓
   (progressively implemented: Match-3 resolution, Special Gems, Combat,
    Pet, Boss — each added by its own owning task)
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
5. **Each stage is a strict extension of the previous one.** §2.0.5 is §2.0
   plus fields §2 already defines; it removes nothing, renames nothing, and
   redefines nothing. A field added by a later stage is likewise a field §2
   already has. No stage introduces a parallel representation of a concept
   another stage already owns.

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
Board Foundation State        The next staged subset: Battle State
                             Foundation plus the authoritative board and
                             its RNG state (§0, §2.0.5);
                             server-authoritative, not persisted
Transient Resolution State    Exists only within one Swap/Card resolution;
                             never stored, discarded after the resolution
                             completes; carries the intermediate states of
                             MATCH3_RULES.md §4.1 (see §3, §5.1)
Client Presentation State     Exists only in the client; animation/UI
                             state with no gameplay authority
```

Only **Active Battle State** (including its staged subsets — **Battle State
Foundation** and **Board Foundation State**) and **Transient Resolution
State** are defined in this document. Persistent State fields live in
`DATABASE.md`.

---

# 2. Active Battle State — `BattleState`

> This is the **full** contract — the eventual gameplay state. While the
> battle system is being built in stages, the staged subsets defined in §2.0
> and §2.0.5 are what exist. They are not alternatives: the stages grow into
> §2 (§0).

```text
BattleState
├── BattleId
├── PlayerId                (the Player who created this battle — owner
│                            identity only, §2.8; not a wire member)
├── Sequence               (monotonic counter, incremented per resolved
│                            action — used for ordering/idempotency,
│                            see SIGNALR_PROTOCOL.md)
├── RngSeed / RngState      (server-seeded, see §2.6, MATCH3_RULES.md §7,
│                            TDD.md §6, ADR-009)
├── BoardState
├── LastCommittedSwapPair?  (the unordered pair most recently committed to
│                            the board; absent before the first commit —
│                            §2.1.10, MATCH3_RULES.md §2.1.4)
├── Combo                   (Match/Combo accounting — §2.2)
├── MatchCount              (Match/Combo accounting — §2.2)
├── PetState                (the one active Pet for this battle — combat
│                            character; combat stats, loadout, Passive —
│                            §2.3; ADR-011)
├── BossState
└── Turn                    (current Turn number, GAME_RULES.md §2)
```

`BattleId`, `Sequence`, and `Turn` are present from the start (§2.0). The
remainder — including `PlayerId` (§2.8) — are added as their owning systems
are implemented.

There is no `PlayerState` member. The Player is the account/owner and has
no authoritative battle-time combat pool; combat stats and the battle
loadout live under `PetState` (§2.3), and Match/Combo accounting lives at
the `BattleState` root (§2.2). The wire member name `playerState` used by
`SIGNALR_PROTOCOL.md` §4.2 is a **fixed protocol label** for the Combo/
MatchCount projection and does not reintroduce a state path of that name
(ADR-011). The only Player identity in state is the root member `PlayerId`
(§2.8) — owner identity metadata for persistence sourcing, with no combat
pool and no gameplay value.

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

The increment rule is **defined**, by board resolution:
`MATCH3_RULES.md` §8.1 owns when a Turn begins (a **committed** Swap, after
validation passes; a rejected Swap begins no Turn) and §8.3 owns the order in
which the counter takes effect during a resolution. §2.0.5 adds one further
case to the same rule: board generation is not a Swap/Action and begins no
Turn (`MATCH3_RULES.md` §8.1 item 4).

**`Sequence = 0`** means that no authoritative action resolution has yet
occurred. The rule in §5 is unchanged: `BattleState.Sequence` increments by
exactly 1 per successfully resolved action. `0` is therefore the only valid
value until the first resolution succeeds — which includes the whole Board
Foundation stage (§2.0.5.2): generating the board resolves no action.

The increment rule is likewise **defined**, by board resolution:
`MATCH3_RULES.md` §8.2 owns what a `Sequence` increment counts (one committed
Swap; not its Matches, passes, Cascades, Special Gems, or removed cells), and
§8.3 owns the order — `Sequence` is incremented once, after the board is
stable and before the resulting state is written or published.

### 2.0.3 Fields Explicitly Not Present

§2.0 contains **no `Status` field**, and this document introduces none.

```text
Status, READY, STARTING, ACTIVE, PAUSED, FINISHED, WON, LOST — and any
similar lifecycle enum — are NOT part of Battle State Foundation.
```

This holds for every later stage too: the Board Foundation State (§2.0.5)
adds no `Status` field and no lifecycle value, and
`SIGNALR_PROTOCOL.md` §8 item 3 carries none anywhere in the protocol.

1. A battle has no lifecycle state machine. It is started, and it ends.
2. Battle outcome is expressed as **events** — `BattleWon` / `BattleLost`
   (`GAME_EVENTS.md` §2, `GAME_RULES.md` §16) — not as a state field. §2.0
   does not convert them into one, and §2 has no such field either.
3. If a battle lifecycle state machine is later required, it must be
   introduced by its own design/ADR task, not added here.

The remaining §2 fields (`PlayerId` §2.8, `Combo`/`MatchCount` §2.2,
`PetState` §2.3, and `BossState` §2.4) are likewise
not present in §2.0. They are gameplay systems that do not exist yet; that is
not a scope reduction of §2.

`BoardState` and `RngSeed`/`RngState` are also not present in §2.0 — they are
added by the **next** stage, the Board Foundation State (§2.0.5), which is the
implementation step immediately following §2.0. Their absence here is a
staging position, not a statement that they are out of scope.

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

## 2.0.5 Board Foundation State

The next implementation stage (§0 item 5). It is §2.0 plus the fields §2
already defines for the Match-3 board and the randomness that generates it —
nothing else is added, and nothing already in §2.0 changes.

```text
Board Foundation State
├── BattleId
├── Turn          = 0
├── Sequence      = 0
├── RngSeed                       (§2.6)
├── RngState                      (§2.6)
├── BoardState                    (§2.1)
│   └── Cells[64]
│       └── entry  { GemType, SpecialGem? }   (§2.1.1; at this stage the
│                                              Special Gem is absent in
│                                              every entry)
└── LastCommittedSwapPair         (§2.1.10; absent at this stage — no Swap
                                   has been committed)
```

Each field has the same name, meaning, and rules as its §2 counterpart; this
stage is where those §2 fields first come into existence (§0 item 5).

### 2.0.5.1 Fields

```text
BattleId        Unchanged from §2.0.1.

Turn            Unchanged from §2.0.1. Still 0: board generation is not a
                player Swap/Action and starts no Turn (GAME_RULES.md §2.1).

Sequence        Unchanged from §2.0.1, §5. Still 0: board generation is not
                an action resolution and increments no counter.

RngSeed         The battle's server-chosen PRNG seed (§2.6.1). Part of §2's
                RngSeed/RngState pair (TDD.md §6 item 2, ADR-009).

RngState        The PRNG state after generating the initial board (§2.6.2).
                Part of the same §2 pair. It is a state + increment pair, not
                a single word (§2.6.2.1).

BoardState      The authoritative board (§2.1). At this stage its Cells[64]
                hold the initial board generated by §2.7, and every entry's
                Special Gem metadata is absent (§2.1.7 item 8) — generation
                creates no Special Gem (MATCH3_RULES.md §1.4.1, §4.5 item 7).

LastCommittedSwapPair
                The unordered pair most recently committed to the board
                (§2.1.10), which is the state MATCH3_RULES.md §2.1.4's
                already-applied check reads. It is absent at this stage:
                board generation commits no Swap (§2.0.5.2 item 1), so no
                pair exists yet. Absence is the representation of "no Swap
                has been committed", not a default or sentinel pair
                (§2.1.10 item 3).
```

### 2.0.5.2 Board Generation Is Not a Resolution

1. Generating the initial board does **not** increment `Turn` or `Sequence`.
   Those fields are defined by `GAME_RULES.md` §2.1 (a Turn is one player
   Swap/Action) and §5 (Sequence increments per resolved action); board
   generation is neither. Both therefore remain `0` at this stage.
2. No Battle Event is emitted by board generation
   (`GAME_EVENTS.md` §2 — no event describes initial board creation), and
   `BattleStarted` remains a gameplay event requiring a created battle with a
   Pet and a Boss (`GAME_EVENTS.md` §2).
3. `RngSeed`/`RngState` are consumed by generation but are not themselves a
   resolution: they advance the PRNG, not `Sequence`.

### 2.0.5.3 What This Stage Does Not Add

Owned by later stages: `PetState`'s combat and collection members (§2.3 —
combat stats, StatusEffects, EquippedRelics, EquippedCards) and
`BossState` (§2.4). `BattleState`'s `Combo` and `MatchCount` (§2.2) are now
implemented and are no longer absent — see §2.2 — and the combat stats
`HP`/`MaxHP`, `ATK`/`DEF`/`Crit`, and `Power` are now implemented as well
(they belong to `PetState` §2.3 in this contract; the Domain type that
currently holds them is an implementation mismatch recorded under ADR-011
Implementation Impact, not a second state path).
Special Gem *state* arrives with
the Special Gem implementation stage (`MATCH3_RULES.md` §5); nothing about
Special Gems is implemented here.

`BoardState` is **not** deferred, and has no deferred second field:
`BoardState` is `Cells[64]` (§2.1), and the Special Gem metadata a later stage
puts in it lives inside those cell entries rather than in a new field
(§2.1.2). At this stage every entry's Special Gem metadata is simply absent,
which is the representation's own statement that the board holds no Special
Gem (§2.1.7 item 3) — no empty collection stands in for it.

### 2.0.5.4 Persistence and Ownership

1. **Server-authoritative.** Identical to §2.0.4 item 1. The generated board and
   the RNG values that produced it are authoritative server state
   (`GAME_RULES.md` §18, ADR-001). The client renders the board it receives
   and may never generate, mutate, or re-derive it.
2. **Not persisted to Redis.** The Board Foundation stage remains a staged
   subset of §2 (§0), so `REDIS_STATE.md` §7 applies unchanged: it is not
   written to `battle:{battleId}:state`, and no partial schema is created for
   it. That boundary is neither weakened nor extended by this stage.
3. **Not persisted to PostgreSQL.** No field here is durable data
   (`DATABASE.md`); no board or RNG table is introduced.

## 2.1 BoardState

```text
BoardState has exactly one field:

    Cells[64]    exactly 64 cell entries; each entry carries the cell's
                 Gem type plus an optional Special Gem (§2.1.1)

There is no second field.
```

`BoardState` is one concept with one representation, built in stages (§0).
`Cells[64]` is the whole of it: the single ordered, index-addressed collection
that holds every cell's occupant together with that occupant's Special Gem
state. There is no second collection of board contents, and no field of
`BoardState` duplicates a cell.

### 2.1.1 `Cells[64]` — the Board's Only Cell Collection

```text
Cells[64]       Exactly 64 entries, row-major, one entry per cell
                (board size, layout, and indexing: MATCH3_RULES.md §1.0)

entry at index i
├── GemType                 ATK | DEF | HP | POWER   (MATCH3_RULES.md §1.1)
└── SpecialGem?             absent → an ordinary Gem
                            present → a Special Gem (type; plus orientation
                            for a Line Clear Gem — §2.1.4 item 2)
```

1. `Cells` is a fixed-length, row-major array of exactly **64** entries. The
   board's size, its indexing convention, its `index = row * 8 + column`
   mapping, and its coordinate convention are owned by `MATCH3_RULES.md`
   §1.0 — this document does not define a second coordinate system and does
   not restate the mapping.
2. Each entry holds one Gem type drawn from the four functional types
   (`MATCH3_RULES.md` §1.1, `GAME_RULES.md` §6). Gems are functional, not
   elemental; a Gem type is not an Element. The four types are the value
   domain; **how** the initial fill selects among them is
   `MATCH3_RULES.md` §1.2.1, and is not restated here.
3. **Present from the Board Foundation stage** (§2.0.5): `Cells[64]` is the
   only part of `BoardState` that stage implements, and is the part the
   client receives and renders.
4. `Cells[64]` is server-authored (`GAME_RULES.md` §18, ADR-001). The client
   never generates, fills, or repairs it.
5. **Every entry is an occupant, and a Special Gem is an occupant.** A cell
   holds exactly one occupant (`MATCH3_RULES.md` §1) and that occupant is
   always one of the four Gem types (§2.1.3 item 1, §2.1.8 item 6): a Special
   Gem adds metadata to the occupant of its cell, it does not replace the
   occupant with a non-Gem value. `MATCH3_RULES.md` §5.5.4 item 4 owns that
   rule.
6. **The array is the board's ordering, and it is never re-enumerated.** The
   64 entries are stored in ascending §1.0 index order, in both the runtime
   state and every serialization of it (§2.1.7). No implementation may
   serialize, enumerate, or deliver the cells in another order, and no lookup
   derived from `Cells` may depend on dictionary, hash, insertion, or
   allocation order (`MATCH3_RULES.md` §7.2 item 4).

### 2.1.2 Special Gems Are Held in `Cells[64]`, Not in `PendingSpecialGems[]`

**Resolution of the former contract gap.** Earlier revisions of this document
carried `BoardState.PendingSpecialGems[]` as a second field of `BoardState`,
described as "the field that records *which* of the 64 cells hold a Special
Gem and of which type", and recorded its shape as an unresolved gap (§2.1.2
item 5 of revision 1.2). That gap is now **closed by the decision below**: the
collection is **removed**, and Special Gem state is carried by `Cells[64]`
itself (§2.1.1, §2.1.3–§2.1.9).

```text
BoardState
└── Cells[64]
    └── entry
        ├── GemType
        └── SpecialGem?         (type + orientation; §2.1.4)
```

1. **No `PendingSpecialGems[]` field exists.** `BoardState` has exactly one
   field, `Cells[64]`. Nothing in this document, in `REDIS_STATE.md`, in
   `SIGNALR_PROTOCOL.md`, in `GAME_EVENTS.md`, or in `MATCH3_RULES.md` defines
   a `BoardState` field named `PendingSpecialGems`, `SpecialGems`, or any other
   collection parallel to `Cells[64]`.
2. **Why the old field is removed rather than filled in.** A second collection
   keyed by cell index would be a redundant index of a subset of `Cells[64]`:
   it would need the cell's Gem type to be kept in step with `Cells[64]` under
   every gravity and swap, and it would make two writers of one fact
   (`MATCH3_RULES.md` §5.5.4 item 2's activation/creation step would write
   both). One collection cannot disagree with itself, so it is the smaller and
   the safer representation — and it is the representation the rest of the
   design already assumed: `MATCH3_RULES.md` §5.9.2 item 4 places the Special
   Gem rules on `BoardState.Cells[64]`, and §5.5.4 item 4 states that a
   created Special Gem "occupies one cell" and "participates in Gravity and
   Spawn like any other Gem".
3. **The word "pending" was never a state.** It described the field, not a
   lifecycle. There is no "pending Special Gem" entity in the authoritative
   state: between the moment a Special Gem is created and the moment it
   activates, the state is simply a board on which that cell holds a Special
   Gem. The word may still be used as a verb for the §4.1 step 2 **pass-local
   creation bookkeeping** (§2.1.6 item 4) — that bookkeeping is Transient
   Resolution State (§3), is not `BattleState`, and is never stored or
   delivered.
4. **The board-level rules were never blocked by this.** Creation, position,
   movement, activation, consumption, ordering, and collision are owned by
   `MATCH3_RULES.md` §4–§5 in full and are unchanged by this section. This
   section defines only how the resulting board is *represented* — which is
   what was missing.

### 2.1.3 Cell Occupancy

```text
an entry of Cells[64] at rest
├── GemType      always present, one of the four types (MATCH3_RULES.md §1.1)
└── SpecialGem?  present or absent, independent of the Gem type
```

1. **A Special Gem is metadata on a cell's Gem, not a substitute for it.** The
   cell's `GemType` is always one of the four §1.1 types, whether or not the
   cell holds a Special Gem. The Gem type is what `MATCH3_RULES.md` §5.5.5
   item 1 consumes when "its own colour is matched normally", and it is what
   `GemMatched` reports (`GAME_EVENTS.md` §2) — so it must exist for a Special
   Gem's cell exactly as for any other.
2. **The Gem type of a created Special Gem is the Gem type of the Gem it
   replaces.** `MATCH3_RULES.md` §5.5.4 item 1: the cell's Gem is removed by
   §4.1 step 1 and the Special Gem is created in that same cell in §4.1
   step 2. The cell never holds a non-Gem value: the created Special Gem
   carries the Gem type of the Gem that was removed from that cell in this
   pass — the Gem type that made the cell a member of the matched shape.
3. **One Special Gem per cell, at most.** A cell holds either no Special Gem
   or exactly one. `MATCH3_RULES.md` §5.5.4 item 3 owns this: two creations in
   one cell resolve to the first in the §5.5.1 order, and "the cell holds one
   Special Gem, never two", so §1's one-occupant-per-cell invariant holds at
   every point. The representation cannot express two Special Gems in one
   cell, which is the documented rule, not a limitation.
4. **Nothing else is stored per cell.** No Special Gem identity, id, sequence
   number, creation pass, cascade depth, age, armed/ready flag, remaining
   charge, or creation order is part of the state. None of them is required by
   any rule in `MATCH3_RULES.md` §5: a Special Gem's behavior is a pure
   function of its type, its orientation, and the cell it currently occupies
   (§2.1.4, `MATCH3_RULES.md` §5.5.5 item 10).
5. **A cell index identifies the Special Gem's position, including after
   movement.** There is no `CellIndex` field inside the Special Gem metadata:
   the index *is* the position in `Cells[64]` (§2.1.7), so a Special Gem's
   recorded position cannot disagree with the array it lives in — the
   failure mode a separate `CellIndex` field would reintroduce.

### 2.1.4 Special Gem Metadata and Orientation

```text
SpecialGem
├── Type            LineClear | Burst | Area      (MATCH3_RULES.md §5.2–§5.4)
└── Orientation?    Horizontal | Vertical          (Line Clear only)
```

1. **`Type`** is one of the three MVP types — `Line Clear Gem`
   (`MATCH3_RULES.md` §5.2), `Burst Gem` (§5.3), `Area Gem` (§5.4). These are
   the complete set: §5.3 item 3 defines no Match-6+ tier and introduces no
   fourth type, and `MVP_SCOPE.md` §1 lists no other.
2. **`Orientation` is present if and only if `Type` is `LineClear`**, and its
   value is `Horizontal` or `Vertical` (`MATCH3_RULES.md` §5.2 item 3). This
   is the only conditional field in the model: `Burst` and `Area` effects are
   orientation-free (`MATCH3_RULES.md` §5.3 item 2, §5.4 item 3), so an
   orientation on those types would be a value no rule reads. It appears in
   the serialization only for a Line Clear Gem (§2.1.7 item 4).
3. **Orientation belongs to the Special Gem, not to its cell.** It is fixed at
   creation from the orientation of the straight line that created the Gem and
   never changes (`MATCH3_RULES.md` §5.2 item 3) — including when the Gem later
   falls, is swapped, or is displaced by gravity (§4.4 item 8). Because the
   metadata lives in the cell entry, it travels with the Gem's `GemType` under
   any movement: the two never move separately.
4. **No other field.** There is no orientation value for a non-Line-Clear
   type, no "both orientations" value, no direction, no size/radius/length
   field, and no tier field. The three geometries are fixed by
   `MATCH3_RULES.md` §5.2–§5.4 and are not configuration in MVP.

### 2.1.5 Gravity, Spawn, and Cell Movement

1. **Gravity moves the whole entry.** `MATCH3_RULES.md` §4.4 moves the Gems
   that survive a Match Resolution downward within their column. In this
   representation a "Gem" is a cell entry: gravity moves the `GemType` **and**
   its `SpecialGem` metadata together, as one unit. A Special Gem is pulled
   down like any other Gem, keeps its place in the column's order, never
   crosses a column boundary, and is not re-triggered by falling
   (`MATCH3_RULES.md` §4.4 item 8). Gravity reads and writes only `Cells[64]`;
   it has no second collection to keep in step, and it draws no RNG
   (`MATCH3_RULES.md` §4.4 item 6).
2. **A swap exchanges two whole entries for the same reason.**
   `MATCH3_RULES.md` §2 item 2 exchanges two cells; the exchange carries each
   cell's `SpecialGem` metadata with its `GemType`, so a swapped Special Gem
   keeps its type and orientation at its new cell.
3. **Spawn produces an entry with no Special Gem, always.**
   `MATCH3_RULES.md` §4.5 item 7: "Spawn never creates a Special Gem." A
   spawned entry therefore has `SpecialGem` absent, whatever the cell held
   before — including a cell whose Special Gem was just consumed. Spawn draws
   exactly one RNG selection per spawned cell and writes only the `GemType`
   (§4.5 item 4).
4. **Empty cells do not exist in this representation.** An empty cell is "the
   absence of a Gem, not a fifth Gem" (`MATCH3_RULES.md` §4.4 item 5), and it
   exists only inside one resolution's §4.1 steps 1–4, never in the published
   board (§3, §5.1 item 2). It is therefore **Transient Resolution State**
   (§3) and is not represented in `BattleState` at all — no sentinel Gem type,
   no null cell entry, no negative index, and no "empty" variant of the
   `Cells[64]` entry. Authoritative state holds exactly 64 Gem entries at all
   times (§2.1.3 item 1).
5. **Creation during a resolution is transient first, board state second.**
   The positions a pass must fill, the reservations that protect them, and the
   Gems removed to make room all live in the pass's local state (§2.1.6
   item 4). The Special Gems the pass creates become part of `Cells[64]`
   before Gravity runs (`MATCH3_RULES.md` §4.1 item 2), and are then written,
   moved, and published as ordinary board state — there is no separate
   promotion step, no second store, and no field to keep in step.

### 2.1.6 Deterministic Ordering

1. **There is no ordered Special Gem collection to order.** `BoardState`
   holds no Special Gem list, so this document defines no `CellIndex`
   ascending order and no creation-order sort for one. Any implementation that
   materializes such a list is building a derived projection, not a
   representation (§2.1.9 item 3).
2. **If a list of the board's Special Gems is ever produced, its order is
   ascending §1.0 cell index.** The order is the cell's own index in
   `Cells[64]` — lowest first, `0 → 63` — with no tie to break, because a cell
   index is unique and a cell holds at most one Special Gem (§2.1.3 item 3).
   This reuses the indexing convention every other board order already uses
   (`MATCH3_RULES.md` §3.2 item 1, §4.4 item 1, §4.5 item 2, `GAME_EVENTS.md`
   §1.3) and introduces no new one.
3. **Cell-index order is not, and must never become, a gameplay resolution
   order.** Creation order, activation order, chain order, and collision
   winners are owned by `MATCH3_RULES.md` §3.2, §5.5.1 item 4, §5.5.4 item 3,
   §5.5.5 items 5 and 7, and §5.8.3, and none of them is changed by this
   section. In particular, a discarded claim is not represented in state at
   all (`MATCH3_RULES.md` §5.5.4 item 3: it is "not created, not stored").
4. **The pass-local creation order is transient.** The §5.5.1 / §5.5.1 item 4
   creation and collision order is the order in which the pass's creations are
   *computed and committed*, and it exists only inside the resolution that
   computes it — exactly like the reservation set and the removed cells
   (§3). It is never a field of `BattleState` and is never serialized, because
   it has no effect on the resulting board: only the winner of a collision
   reaches the board, and the winner is decided by that order, not recorded in
   it.

### 2.1.7 Serialization and Persistence

1. **`BattleState` is serialized from `Cells[64]` with no side channel.**
   Special Gem state is part of `BattleState` because it is part of the cells
   (§2). A serializer that writes `BattleState` therefore writes it with no
   additional field, no parallel array, and no second key: `REDIS_STATE.md` §2
   item 1 ("serialized as JSON, matching the shape in this document exactly —
   no additional Redis-only fields") is satisfied without exception.
2. **Cells are serialized as a 64-element array in ascending index order**
   (§2.1.1 item 6). The array position **is** the cell index
   (`MATCH3_RULES.md` §1.0): element `i` is the entry for cell `i`. No
   `CellIndex` field is written per element, because the position already
   states it and a redundant index field is one more thing that can disagree
   with the array.
3. **A cell with no Special Gem is serialized as a cell with no Special Gem
   member.** The representation is **absent**, not a null element, not a
   sentinel type, and not a default value. Omitting the member and writing an
   explicit `null` are the same statement — "this cell holds no Special Gem" —
   and either is decodable, but no third spelling exists: no `"None"` type
   value, no empty-string orientation, no slot marker. What must never happen
   is that an absent Special Gem is spelled as one of the three real types.
4. **A cell with a Special Gem serializes its type, and its orientation only
   when its type requires it** (§2.1.4 item 2). A `Burst` or `Area` entry
   carries no orientation member; a `LineClear` entry always carries one of
   `Horizontal` / `Vertical`. The exact JSON member names and casing are an
   implementation detail of the serializer (`SIGNALR_PROTOCOL.md` §8 item 1,
   `GAME_EVENTS.md` §3 item 1); their *existence and meaning* are owned here.
5. **Round-trip is lossless and is the serializer's contract.** Serializing a
   `BattleState` and deserializing it must return a `BattleState` whose
   `Cells[64]` are identical — same 64 Gem types, in the same index order, with
   the same Special Gem type and orientation at the same cells — and whose
   `Turn`, `Sequence`, `RngSeed`, and `RngState` are unchanged
   (`REDIS_STATE.md` §7 items 9–10, §2.6.2 item 4). A round trip that drops a
   `SpecialGem`, reorders cells, or renumbers an index is a defect, and it is
   the failure this section exists to make detectable.
6. **A snapshot taken between resolutions is complete.** §5.3 defines what a
   snapshot contains; Special Gem state is inside `Cells[64]`, so it is carried
   with the board, and no separate snapshot step, field, or compatibility path
   is introduced by it (`ADR-008`).
7. **Redis holds this and nothing else for it.** The active battle record is
   `BattleState` under `battle:{battleId}:state` (`REDIS_STATE.md` §1–§2). No
   separate Special Gem key, hash, set, or index exists in Redis, and the field
   adds nothing to the record's lifecycle (`REDIS_STATE.md` §3–§4).
8. **The initial board carries no Special Gem state at all.** Board generation
   never creates a Special Gem (`MATCH3_RULES.md` §1.4.1, §4.5 item 7): every
   entry of a generated board has `SpecialGem` absent, and generation is
   therefore unaffected by this model in every respect, including its RNG
   consumption and its determinism (§2.7).

### 2.1.8 Activation and Removal

1. **Activation is not a state transition of the Special Gem entry.** A
   Special Gem activates when it is **consumed** (`MATCH3_RULES.md` §5.5.5
   item 1), and consumption has already removed it: the cell holding it is
   cleared by `MATCH3_RULES.md` §4.1 step 1 (match-set consumption) or by
   another effect's affected set (§5.5.5 item 2, item 7). There is no
   "activated" flag, no arming step, no state in which a Special Gem is
   present-and-spent, and no state in which it has activated but is still on
   the board. Activation reads the entry, computes its affected set, and the
   entry is gone.
2. **Removal is exactly the removal of the cell's occupant.** Clearing a
   Special Gem's cell removes the whole entry — its Gem type and its Special
   Gem metadata together, as one act. There is no separate "remove the Special
   Gem but keep the Gem" operation and no "remove the Gem but keep the Special
   Gem" operation: the cell is emptied (`MATCH3_RULES.md` §4.1 step 1) and is
   refilled by Spawn with an ordinary Gem (§2.1.5 item 3). A Special Gem's own
   cell is always in its own affected set, so consuming it always clears it and
   never leaves it behind (`MATCH3_RULES.md` §5.5.5 item 4).
3. **A cell is removed once, and its Special Gem is therefore consumed once.**
   Removal, resource generation, and the `GemMatched` report are computed over
   the **union** of a step's cleared cells (`MATCH3_RULES.md` §5.5.5 item 6,
   §5.8.2 item 1), and a Special Gem "activates at most once per step" even if
   several effects name its cell (`MATCH3_RULES.md` §5.8.2 item 3). In this
   representation that is not a special case: the cell has one occupant with at
   most one Special Gem, so the once-per-cell rule and the
   once-per-Special-Gem rule are the same rule.
4. **A chain is 'cell was consumed', not a state flag on the entry.** A
   Special Gem consumed by another effect's affected set activates in turn
   (`MATCH3_RULES.md` §5.5.5 item 7, breadth-first, finite by consumption).
   The chain is tracked by the resolution, not by the board: an entry that has
   been consumed is no longer in `Cells[64]`, which is what makes a second
   activation impossible and what makes the chain terminate.
   `MATCH3_RULES.md` §5.8.3 item 7 forbids a depth cap; no cap, counter, or
   chain-generation number is stored per Special Gem.
5. **A collision loser never reaches the state.** When two creations claim one
   cell, the later claim is discarded and "is not created, not stored, not
   activated, and not reported as created" (`MATCH3_RULES.md` §5.5.4 item 3).
   The representation contains only the winner, so no discarded claim is ever
   serialized, delivered, or recoverable — and a serializer cannot accidentally
   preserve one.
6. **A Special Gem never enters a match set as a colour of its own.** §2.1.3
   item 1: the cell's `GemType` is still one of the four §1.1 types, and that
   is the value Match Detection compares. The Special Gem metadata is not a
   fifth type and is never compared by detection (`MATCH3_RULES.md` §5.5.4
   item 4).

### 2.1.9 What This Model Does Not Add

1. **No gameplay behavior.** Every rule referenced above is owned by
   `MATCH3_RULES.md`; this section states only where the resulting values live.
   No rule of §5.2–§5.9 is changed, narrowed, or extended, and no new Special
   Gem behavior is introduced.
2. **No new `BattleState` field for Special Gems.** `BoardState` is
   `Cells[64]` (§2.1.2 item 1). The Special Gem metadata is inside the cell
   entries, so no field was added for it and the §0 staging rule ("a field
   added by a later stage is a field §2 already has") is satisfied by the cell
   shape rather than by an added field. (The §2 field list does gain one member
   in this revision — `LastCommittedSwapPair`, §2.1.10 — but from the Swap
   stage, not from this one, and it is not Special Gem state.)
3. **No transport DTO architecture.** `SIGNALR_PROTOCOL.md` §4 item 4 projects
   the state one-to-one, so the board field carries the same 64 entries the
   state does; no Special-Gem-specific message, method, or subscription is
   introduced (`SIGNALR_PROTOCOL.md` §3.1 item 3, §8 item 7).
4. **No client authority.** The board, including its Special Gem metadata, is
   server-authored and server-resolved (`GAME_RULES.md` §18, `ADR-001`). The
   client renders what it receives; it never creates, moves, matches, or
   activates a Special Gem (§2.0.5.4 item 1, `SIGNALR_PROTOCOL.md` §4.1
   item 2).

### 2.1.10 `LastCommittedSwapPair` — the Commit Record `STALE_ACTION` Reads

**Resolution of the former contract gap.** `MATCH3_RULES.md` §2.1.2 requires
the already-applied / idempotency check (§2.1.4) as check 3 of the Swap
validation order, and §2.1.4 item 2 defines that check's input as "the pair
most recently committed to the board". No field of `BattleState` held that
value: the §2 field list had no such member, §2.0.5's stage had none, and
`BoardState` cannot hold it (§2.1.2 item 1 fixes `BoardState` at one field,
`Cells[64]` — a swap leaves no trace in the board that distinguishes
"exchanged once" from "exchanged back"). This section closes that gap: the
value is a **`BattleState` field**, because §2 is the authoritative runtime
state and §0 item 5 requires a field added by a later stage to be a field §2
already declares.

```text
BattleState
└── LastCommittedSwapPair?
    └── (MinCellIndex, MaxCellIndex)   both §1.0 indices, MinCellIndex < MaxCellIndex
```

1. **What it records.** The unordered pair `{from, to}` of the most recently
   **committed** Swap (`MATCH3_RULES.md` §2.1.4 item 2) — the pair whose
   exchange the board currently reflects, and nothing else. It is the
   authoritative input to that check and has no other reader. It is not a
   version, not a request id, and not a Turn number.
2. **Canonical ordering, because the pair is unordered.** `MATCH3_RULES.md`
   §2.1.1 item 2 defines the swap's identity as the unordered pair `{from, to}`
   and states the order of `from`/`to` has no gameplay meaning; §2.1.3 item 3
   makes the two inputs symmetric. The field is therefore stored **canonically**
   as `(min(from, to), max(from, to))` with `MinCellIndex < MaxCellIndex`, so a
   request's argument order can never make the stored value ambiguous or make
   two spellings of one pair compare unequal. `(13, 12)` and `(12, 13)` both
   record `(12, 13)`, and the two are the same commit.
3. **Absence is the initial value — there is no sentinel pair.** Before the
   first committed Swap the field is **absent**, exactly as a cell's
   `SpecialGem` is absent when the cell holds none (§2.1.7 item 3). It is never
   `(0, 0)`, `(-1, -1)`, or a defaulted `(0, 1)`: `(0, 0)` is not even a
   representable pair (item 2 requires two distinct indices), and any real pair
   used as a stand-in would be a commit the battle never made. Battle creation
   leaves it absent: board generation commits no Swap and is not an action
   resolution (§2.0.5.2 item 1), so a newly created battle has no committed
   pair.
4. **Absent never rejects.** An action is rejected as already applied only when
   the field is *present* and equals the action's canonical pair
   (`MATCH3_RULES.md` §2.1.4 item 2). While the field is absent, check 3 can
   never fail — no swap has been committed, so no swap is already applied.
5. **Set only by a successful commit, to the committed pair.** Committing a
   Swap (`MATCH3_RULES.md` §2.1.6 step 4, §8.3 step 3) writes the action's
   canonical pair. It is written once per resolution, in the same single
   post-resolution write-back as `Turn` and `Sequence` (§5.1): it is state, not
   a mid-resolution flag, and a reader never observes it changed by a
   resolution still in progress (§5.1 item 2).
6. **A rejected action does not change it — by any rejection reason.** For
   `INVALID_CELL_INDEX`, `INVALID_SWAP`, `NO_MATCH_FROM_SWAP`, and
   `STALE_ACTION` alike, the field keeps the value it had
   (`MATCH3_RULES.md` §2.1.5: a rejected action writes nothing). A rejection is
   not a commit, so it neither records a pair nor clears one.
7. **A rejection never clears the previous commit either.** Applying a *new*
   pair replaces the stored value; a rejected request leaves the old pair in
   force, so the previously committed Swap is still recognised as already
   applied after an unrelated rejected action arrives. Only a later *committed*
   Swap replaces it.
8. **The client never supplies or reads it.** The field is derived by the
   server from its own commit history; `clientSequence` is **not** its source
   and is not consulted for staleness (`MATCH3_RULES.md` §2.1.4 items 1 and 5,
   `SIGNALR_PROTOCOL.md` §2 item 1). No Swap request carries it
   (`MATCH3_RULES.md` §2.1.1 item 3), so a client cannot declare an action
   already committed.
9. **It is authoritative state, and it is not delivered to the client.** It is
   part of `BattleState` (§2) and is therefore serialized with it (item 10),
   but it is **not** a member of the `BattleStateUpdated` payload: that record
   carries exactly the implemented stage's fields and admits no other
   (`SIGNALR_PROTOCOL.md` §4 item 4, §4 items 10–11). This is deliberate and
   has no client-side consequence: §2.1.4 makes the client unable to
   participate in staleness detection at all, so there is nothing for a client
   to do with the value. It is server-side bookkeeping inside authoritative
   state, not a new client-facing field, and the client must never author,
   adjust, or recompute it (§2.1.9 item 4, `GAME_RULES.md` §18, `ADR-001`).
10. **Serialization.** The field is written with the rest of `BattleState`
    (§2.1.7 item 1). When present it carries the canonical pair, with
    `MinCellIndex < MaxCellIndex` so the serialized order is itself canonical
    and a round trip cannot reorder or re-spell it (§2.1.7 item 5). When absent
    it is **omitted**, matching the board's absent-Special-Gem rule
    (§2.1.7 item 3): absence is the statement "no Swap has been committed", and
    no `null` pair, sentinel index, or zero pair stands in for it. Member names
    and casing are the serializer's implementation detail (§2.1.7 item 4,
    `SIGNALR_PROTOCOL.md` §8 item 1).
11. **No other field is added, and no lifecycle value.** There is no action id,
    request id, correlation value, timestamp, retry count, generation counter,
    Turn number, or Queue of pending or superseded actions stored here or
    beside it — `MATCH3_RULES.md` §2.1.4 item 3 states a Swap is otherwise
    stateless against the current board and defines no per-Turn queue, and §2.1
    defines no `Status` field (§2.0.3). The one pair above is the whole of it.
12. **This section defines no new Swap rule.** Which actions are rejected, in
    what order, and with what reason remain owned by `MATCH3_RULES.md` §2.1.2
    and §2.1.4; this section states only where the value that rule reads lives,
    what shape it has, and when it is written.
13. **No gameplay behavior.** Recording a commit records no Match, Combo, Power,
    Passive, or Relic progression, creates no Special Gem, and consumes no
    randomness. The field is written from the action the server already
    committed, and reads nothing else.

## 2.2 Match / Combo Accounting (`BattleState.Combo`, `BattleState.MatchCount`)

Match/Combo accounting is two fields at the **`BattleState` root**. There
is no nested `PlayerState` container: the Player is the account/owner and
is not a battle-time combat state (§2 tree, ADR-011).

```text
BattleState
├── Combo                       (current Combo for the Swap that just
│                                committed and resolved, resets to 0 when
│                                a new Swap begins, GAME_RULES.md §5 —
│                                implemented)
└── MatchCount                  (cumulative Matches this battle,
                                GAME_RULES.md §3 — implemented)
```

**Both fields are implemented.** They are `int`, both `0` at battle
creation. They are written in the single post-resolution write-back of
§5.1, for a committed Swap only; their rule-level lifecycle is owned by
`MATCH3_RULES.md` §6 and their cumulative/cumulative-vs-scoped distinction
by `GAME_RULES.md` §3 and §5.

**Wire label.** `SIGNALR_PROTOCOL.md` §4.2 delivers these two values under
the fixed payload member name `playerState` (exactly `combo` and
`matchCount`). That member name is a **protocol label** — it is not a
state path, and this contract has no `PlayerState` node (§2 tree,
ADR-011). See §2.2.1.

### 2.2.1 Delivery and Consequences

**The two fields are state, and they are the only Match/Combo members
delivered on the wire.** `SIGNALR_PROTOCOL.md` §4.2 fixes the `playerState`
payload member to exactly `combo` and `matchCount` — so of the §2 members
in this section, only these two reach the client under that label. The
combat stats and collection members live under `PetState` (§2.3) and are
not part of `playerState` (§4 item 4's rule that a payload carries only
the implemented stage's own fields). Adding state is not adding a wire
member: as with `LastCommittedSwapPair` (§2.1.10 item 9,
`SIGNALR_PROTOCOL.md` §4 item 12), delivering more is a protocol change
owned by its own task.

Two consequences follow, and both are deliberate rather than gaps:

1. **Absence conventions do not apply to these members.** Unlike
   `LastCommittedSwapPair` (§2.1.10 item 3) and the optional cell
   `SpecialGem` (§2.1.7 item 3), `MatchCount` and `Combo` are defined
   from battle creation, and `Combo = 0` is a real publishable value —
   the value read before the battle's first committed Swap
   (`MATCH3_RULES.md` §6.5 item 4). Neither is nullable, neither is
   omitted, and zero is never spelled by omission.
2. **These fields exist, and Redis persistence remains deferred.**
   `PetState`'s combat members (§2.3) and `BossState` (§2.4) are also
   implemented, and `POST /api/battle/start` (TASK-030) creates a battle
   carrying §2's shape — but that is a storage decision of its own
   (`REDIS_STATE.md` §7), and no Redis record is written by this stage.
   `REDIS_STATE.md` §7 items 4 and 8 and its item 12 apply unchanged:
   these fields neither require nor authorize persistence, and
   add no key and no Redis-only field.

**Implementation note (not a contract path).** The Domain model previously
nested these two values under a type named `PlayerState`. That type name
does not appear in this contract; it was an implementation-side mismatch
recorded under ADR-011 Implementation Impact / `tasks/completed` impact,
not a second authoritative path and not a wire rename.

## 2.3 PetState

The one active Pet for this battle. The Pet is the **combat character**;
the Player is the account/owner and has no authoritative battle-time
combat pool (ADR-011). Combat stats, status, and the battle loadout live
here — not under a Player state.

```text
PetState
├── PetId / Identity
├── Element
├── Tier / Star / Level
├── HP / MaxHP                 (combat stats — implemented in the Domain
│                               model; COMBAT_RULES.md §1.1)
├── ATK / DEF / Crit           (base + active modifiers — implemented in
│                               the Domain model)
├── Power                      (0–100, GAME_RULES.md §12 — implemented in
│                               the Domain model)
├── StatusEffects[]              (Burn/Shield/Buff-Debuff instances,
│                                COMBAT_RULES.md §5 — not yet implemented)
├── EquippedRelics[]              (3–5 owned Relic instance identities,
│                                slot order fixed at battle start — the
│                                slot index is the submitted `relicLoadout`
│                                position + 1, and the array preserves that
│                                order, RELIC_RULES.md §2.2–§2.5, §4 —
│                                not yet implemented)
├── EquippedCards[]                (3 Basic Cards + 1 Pet Skill Card —
│                                 not yet implemented)
├── PassiveId                    (the Pet's one Passive — PASSIVE_RULES.md §1;
│                                 the identity PassiveCharged/PassiveTriggered
│                                 report, GAME_EVENTS.md §2)
├── PassiveProgress             (current count vs. threshold,
│                                PASSIVE_RULES.md §2)
└── PassiveResetOverride          (only present if this Pet's Passive uses
                                  non-default reset behavior,
                                  PASSIVE_RULES.md §4)
```

**`PetId` denotes the owned Pet instance.** The tree's `PetId / Identity`
entry is the **instance** identity: the same value as `Pet.PetInstanceId`
(`DATABASE.md` §1), the `petId` submitted to `POST /api/battle/start`
(`API_CONTRACTS.md` §3), and `BattleResult.PetInstanceId` at battle end
(`DATABASE.md` §1). It is not a Pet definition id and not the display
`Identity` name (`PET_RULES.md` §2, `PetDefinition.Identity`) — those are
persistent definition-side values, not battle state. The `PetId` payload
member of `BattleStarted` (`GAME_EVENTS.md` §2) denotes this same instance
value. ADR-014 evaluated whether a separate `PetState.PetInstanceId` member
was required and recorded that it is not: `PetId` already is the instance.

**Implemented so far: `HP`, `MaxHP`, `ATK`, `DEF`, `Power`, `Crit`, plus
`PassiveId`/`PassiveProgress`/`PassiveResetOverride` and identity fields**
as staged, together with the two battle-loadout collections
`EquippedRelics[]` and `EquippedCards[]`, which TASK-027/TASK-028
implemented and `POST /api/battle/start` (TASK-030) populates at battle
creation. **The remaining collection member `StatusEffects[]` is not yet
implemented.** (The Domain model no longer nests the combat stats under a
type named `PlayerState`; that implementation mismatch was corrected —
see §2.2 Implementation note and ADR-011.)

**Domain state implemented is not the same as client wire delivery.**
Not all members here are part of any wire payload — see
`SIGNALR_PROTOCOL.md` §4.2 (only `combo`/`matchCount` under the
`playerState` label) and §4.3 (only the Passive trio under `petState`).

The combat-stats stage owns `HP`, `MaxHP`, `ATK`, `DEF`, `Power`, and
`Crit` as `int` members, each initialized at battle creation to its
`COMBAT_RULES.md` §1.1 MVP default (`HP` = `MaxHP` = 1000, `ATK` = 50,
`DEF` = 25, `Power` = 0, `Crit` = 5). §1.1 owns those values and states that
they are not permanent invariants — changing them is a configuration change,
not a redesign of this field list. `Crit` is the percent §1.1 defines
("critical hit chance (%)"), so the value is `5` and not `0.05`. `Power`'s
0–100 range is a documented invariant of §1.1 and `GAME_RULES.md` §12 and is
not enforced by the state. They are carried through the §5.1 write-back
unchanged: this stage adds the fields and their initial values only, and the
Damage Pipeline (`COMBAT_RULES.md` §3) and Resource Generation (§2) that will
read and write them are not implemented.

These combat stats are the **active Pet's** stats. There is no separate
Player HP/ATK/DEF/Power pool in §2; damage and healing apply to
`PetState.HP` (or `BossState.HP`), per `COMBAT_RULES.md` §1.1 and
`GAME_RULES.md` §14.

The remaining collection member is **not yet implemented** — not **not
required** (§0 item 4): `StatusEffects[]` arrives with its owning Combat
system, exactly as the Relic and Card stages arrived and added
`EquippedRelics[]` and `EquippedCards[]`. It is not stubbed, defaulted, or
represented by a placeholder collection, because a placeholder for a field
no rule yet reads would be a representation of its own (§0 item 5).
`EquippedRelics[]` and `EquippedCards[]` are the **battle-scoped loadout**
fixed at battle start (3–5 Relics, exactly 3 Basic + 1 Pet Skill Card), and
`POST /api/battle/start` (`API_CONTRACTS.md` §3) is the point at which they
are snapshot from the submitted `relicLoadout` and `cardLoadout`.
For Relics, ownership of the underlying owned instances remains the
Player's (`DATABASE.md` §2); for Cards there are no instances — the
Player's unlock rows (`PlayerUnlockedCard`) remain the Player's — while
the equipped set is per-Pet (`RELIC_RULES.md` §2, `CARD_RULES.md` §1,
ADR-011).

**`EquippedRelics[]`'s element is an identity, not a definition.** Each
element is one owned Relic **instance identity** — the same "identity, not
a definition" member shape this section records for `PassiveId` and §2.4
records for `BossId`, and the same identity `GAME_EVENTS.md` §2 reports as
`RelicTriggered`'s `RelicId`. A Relic's `Trigger`/`Condition`/`Effect`/
`Reset` are its definition on `RelicDefinition` (`DATABASE.md` §1) and are
**not** copied into the element (§0 item 5). Which identity it is, why it
is the instance and not `RelicDefinitionId`, and the evidence for that are
owned by `RELIC_RULES.md` §2.2 and are not restated here.

**The member's order is the submitted loadout order, and it is fixed.**
`relicLoadout[0]` becomes slot 1 and therefore `EquippedRelics[0]`; the array
is snapshotted once at battle start and does not change for that battle. The
slot-index source, the 3–5 count and ownership validation, the
duplicate-instance rule, and the resulting valid/invalid behavior are owned
by `RELIC_RULES.md` §2.1–§2.5 and `API_CONTRACTS.md` §3, and are not
restated here — this section states only where the values live and that
their order is preserved as submitted.

**`EquippedCards[]`'s element is a definition identity, not an instance.**
Each element is one `CardDefinitionId` (`DATABASE.md` §1) — a definition
identity, the same member shape this section records for `PassiveId` and
§2.4 records for `BossId`, and the contrast of `EquippedRelics[]` above
(instance identity). There are no Card instances (ADR-012 item 9): if
the same `CardDefinitionId` appears more than once, the repeated
elements are that same definition repeated — permitted only up to its
per-CardDefinition loadout copy limit (`CARD_RULES.md` §1) — and never
separate owned or persistent entities. The array holds exactly the four
battle-scoped cards (the 3 submitted Basic CardDefinitionIds plus the
active Pet's derived Signature Skill CardDefinitionId), is snapshotted
once at battle start, and does not change for that battle. Element
order carries no gameplay significance — no rule reads card array
positions, unlike `EquippedRelics[]` above, whose order is the equip
slot order.

**The combat stats are state, and they are not yet delivered on the wire.**
`SIGNALR_PROTOCOL.md` §4.2 fixes the `playerState` payload member to exactly
`combo` and `matchCount`, and §4.3 fixes `petState` to exactly the Passive
trio — so no combat member of this section reaches the client today, per
§4 item 4's rule that a payload carries only the implemented stage's own
fields. The combat stats do not change that by themselves: as with
`LastCommittedSwapPair` (§2.1.10 item 9, `SIGNALR_PROTOCOL.md` §4 item 12),
adding state is not adding a wire member. Delivering them is a protocol
change owned by its own task.

Absence conventions do not apply to the combat stats: they are defined
from battle creation, and `Power = 0` is a real publishable value —
the value read before any Match generates Power (`COMBAT_RULES.md` §2).
None is nullable, none is omitted, and zero is never spelled by omission.

`PetState` and `BossState` (§2.4) both now exist, and `POST /api/battle/start`
(TASK-030) creates an authoritative battle from the resolved Pet, Boss, and
both loadout snapshots — so §2's shape is produced for a created battle. That
does not itself authorize Redis persistence: `REDIS_STATE.md` §7's deferral
gate is a storage decision with its own task, and this stage writes no Redis
key. `REDIS_STATE.md` §7 items 4, 8 and 12 apply unchanged in the meantime:
these fields add no key and no Redis-only field.

**`PassiveId` is the Passive's identity, and it is a value, not a new
concept.** A Pet has **exactly one** Passive (`PASSIVE_RULES.md` §1,
`GAME_RULES.md` §9.2 item 2), so this field names which Passive definition the
active Pet carries — it does not select among several, and there is no
collection, slot, or ordering of Passives in `PetState`. It is what
`GAME_EVENTS.md` §2's `PassiveCharged` and `PassiveTriggered` report as their
`PassiveId`, and it is the same member shape the sibling identity fields
elsewhere in state use (`BossState.BossId`, `PetState.EquippedRelics[]`,
`PetState.EquippedCards[]`).

1. **It is an identity, not a definition.** The field carries the identifier
   only. The Passive's `Threshold`, `Trigger Type`, `Effect`, and
   `Reset Behavior` (`PASSIVE_RULES.md` §1) are the definition those values are
   read from; none of them is stored here, and no second copy of the definition
   is introduced by this field.
2. **It is set at battle creation and never changes.** Selecting a Pet locks in
   that Pet's Passive for the duration of the battle
   (`PET_RULES.md` §2 item 3; mid-battle Pet swapping is out of MVP scope), so
   no resolution writes it and no event changes it.
3. **It is present from battle creation** — a battle always has its one active
   Pet and therefore its one Passive (`GAME_EVENTS.md` §2 `BattleStarted`
   requires a created battle with a Pet and a Boss). It is **not** an
   absent-when-unset convention and not written by the first charge: unlike
   `LastCommittedSwapPair` (§2.1.10 item 3) and a cell's optional `SpecialGem`
   (§2.1.7 item 3), there is no "no Passive yet" state for it to spell.
4. **No gameplay behavior is introduced here.** This field records which
   Passive the tracker charges; the charge, threshold, trigger, and reset rules
   are owned by `PASSIVE_RULES.md` §2–§5 and are not restated, narrowed, or
   extended by it. It carries no progress value (that is `PassiveProgress`)
   and no reset policy (that is `PassiveResetOverride`).

## 2.4 BossState

```text
BossState
├── BossId / Identity
├── Element
├── HP / MaxHP / ATK / DEF
├── State                        (Idle/Charging/Enraged/Stunned — BOSS_RULES.md §1)
├── PassiveId                    (the Boss's one Passive — BOSS_RULES.md §3;
│                                 identity for PassiveCharged/PassiveTriggered
│                                 events, GAME_EVENTS.md §2)
├── PassiveProgress              (current count vs. threshold,
│                                 BOSS_RULES.md §3, PASSIVE_RULES.md §2)
├── SkillCharge                  (current charge vs. skill charge requirement,
│                                 BOSS_RULES.md §4)
├── SkillCooldown                (turns remaining before Skill can fire,
│                                 BOSS_RULES.md §4)
└── StatusEffects[]              (not yet implemented — owned by Status
                                  Effects system)
```

**`BossId` denotes the canonical technical Boss Identity.** The tree's
`BossId / Identity` entry is the stable machine-readable game-level Boss ID
(`BOSS_RULES.md` §6.4, e.g. `boss-hoa-long`) — the same value
`BattleStarted.BossId` reports (`GAME_EVENTS.md` §2), event `sourceId`
carries when `source = "boss"` (`SIGNALR_PROTOCOL.md` §3.2.16–§3.2.18),
`POST /api/battle/start` receives as `bossId` (`API_CONTRACTS.md` §3), and
`BossDefinition.Identity` persists (`DATABASE.md` §1). It is **not** the
Boss's display name (display names are presentation-only content —
`BOSS_RULES.md` §6.4) and **not** `BossDefinitionId` (the persistence
primary key — `DATABASE.md` §1); the three are never collapsed (TASK-046).
It is set at battle creation and never changes; there is no boss instance
record, so it is a definition-side identity (contrast `PetState.PetId`,
which is an instance identity — §2.3, ADR-014). No second identity field
and no display-name member is added to `BossState`.

### 2.4.1 Staged BossState Fields

```text
Implement now:
  BossId, Element, HP, MaxHP, ATK, DEF, State,
  PassiveId, PassiveProgress, SkillCharge, SkillCooldown

Deferred:
  StatusEffects[] — owned by Status Effects system
```

### 2.4.2 Boss Passive

Boss Passive is reactive and match-based, mirroring the Pet Passive structure
(PASSIVE_RULES.md §1–§2) but triggered by the *player's* actions or battle
state rather than the Boss's own Matches (Bosses do not match Gems).

`PassiveId` identifies which Passive definition the Boss carries — the same
identity pattern as `PetState.PassiveId` (§2.3). It is set at battle creation
and never changes.

`PassiveProgress` tracks progress toward the Passive's threshold, owned by
BOSS_RULES.md §3 and PASSIVE_RULES.md §2.

### 2.4.3 Boss Skill Charge and Cooldown

`SkillCharge` tracks progress toward the Boss Skill's charge requirement.
It increments per player match (same trigger as Passive charging, but
independent counter). When `SkillCharge ≥ SkillChargeRequirement` (defined
per Boss in BOSS_RULES.md §6), the Skill is eligible to fire.

`SkillCooldown` tracks turns remaining before the Skill can fire again.
It starts at the Boss's cooldown value after each Skill use, decrements by 1
at each Turn increment (`MATCH3_RULES.md` §8.1 — one committed Swap begins
exactly one Turn; the stored Turn advances once in that resolution's single
write-back, `GAME_STATE.md` §5.1), and blocks Skill use
while `> 0`.

The Skill fires when BOTH conditions are met:
1. `SkillCharge ≥ SkillChargeRequirement`
2. `SkillCooldown = 0`

After the Skill fires: `SkillCharge` resets to 0, `SkillCooldown` resets to
the Boss's cooldown value.

### 2.4.4 Enrage

Enrage is a permanent state transition triggered when `BossHP < EnrageThreshold`
(defined per Boss in BOSS_RULES.md §6). Once Enraged, the Boss remains Enraged
for the rest of the battle — there is no timer or duration field for MVP.
The Enrage threshold and any Enrage-specific behavior changes are owned by
BOSS_RULES.md §5.

### 2.4.5 Stunned

Stunned is a temporary state that prevents the Boss from acting (no Skill,
no basic attack). Stun duration is measured in Turns and tracked by
`StatusEffects[]` (not yet implemented). When Stun is applied, `State` becomes
`Stunned`; when the duration expires, `State` reverts to `Idle`. For MVP,
no content-defined Boss applies Stun — the state exists for future content.
Full state machines are explicitly deferred to Future Expansion
(BOSS_RULES.md §5 item 3).

## 2.6 RNG (`RngSeed` / `RngState`)

`RngSeed` and `RngState` are the §2 fields that make gameplay randomness
reproducible (`TDD.md` §6, `MATCH3_RULES.md` §7). The algorithm is selected by
`ADR-009`; this section defines the state contract that travels in
`BattleState`.

### 2.6.1 `RngSeed` — Seed Representation and Source

```text
Type:    unsigned 64-bit integer
Source:  server-generated at battle creation
```

1. **Server-authoritative.** The seed is chosen by the server
   (`GAME_RULES.md` §18, ADR-001). It is never supplied, influenced, or
   derived from client input — not from client `Math.random()`, client
   timestamps, client-generated values, or any client-provided field
   (`TDD.md` §6 item 3).
2. **Source.** The seed comes from the server's own entropy at battle
   creation. Nothing in the documentation requires a specific entropy source,
   so this is an implementation detail — but it must be a server-side source,
   and it must produce a `System.UInt64` (`ADR-009`).
3. **Distinct from `RngState`.** `RngSeed` records the battle's origin point
   and is never rewritten after creation. `RngState` is what advances. They
   are not interchangeable representations of the same value (§2.6.2).

### 2.6.2 `RngState` — State Representation and Advancement

```text
Type:    two unsigned 64-bit integers, carried as a pair
         ├── State        — the PRNG's current internal state
         └── Increment    — the PRNG's stream selector
Meaning: where the stream resumes
```

1. **`RngState` is a pair, not a single word.** The generator selected by
   `ADR-009` keeps a 64-bit internal state **plus** a 64-bit stream selector.
   A single 64-bit integer cannot represent it, so `RngState` carries both.
   This is one logical field with two components; the two components are never
   split across separate `BattleState` fields, and no third RNG value exists.
2. `RngState` is the single advancement point of the battle's randomness. Any
   operation that consumes randomness reads `RngState`, produces its values,
   and stores the resulting `RngState` back into `BattleState` — there is no
   second, hidden generator (`AGENTS.md` §11).
3. **Advanced by consumption.** `RngState` changes only when a value is
   drawn; drawing nothing leaves it unchanged. It is not advanced on a timer,
   on a Turn boundary, or as a side effect of unrelated state changes.
4. **Snapshot semantics.** `RngState` is part of `BattleState`, so
   recovering a battle from a snapshot (`ADR-008`, `SIGNALR_PROTOCOL.md` §7)
   restores the stream exactly: subsequent draws continue identically to the
   unrecovered battle (`TDD.md` §6). This is why the state is stored rather
   than recomputed from `RngSeed`.

### 2.6.3 PRNG Identity

The algorithm is fixed, and it is selected and justified in `ADR-009` —
which owns the algorithm, its state and output widths, its advancement step,
and its next-value semantics. This document owns only how that state is
represented inside `BattleState` (§2.6.1–§2.6.2).

The concrete algorithm, its reference sequence, and its state/output widths
are stated in `ADR-009` and are not restated here, so there is exactly one
place to change if the generator is ever revisited.

Whatever the algorithm, it must be a deterministic, integer-only generator
whose complete state is serializable into `BattleState`. `Random.Shared`,
`System.Random`, `Guid`-derived randomness, and timestamps must never produce
a gameplay-relevant value (`AGENTS.md` §11, `TDD.md` §6).

### 2.6.4 Value Conversion

Converting a PRNG output into a Gem type is owned by `MATCH3_RULES.md` §1.1
(Gem types) and §1.2 (their distribution), because that is a game rule, not a
state shape. The state contract guarantees only that the conversion is a
**deterministic function of `RngState`** — the same `RngState` always yields
the same Gem type sequence.

Initial board generation reduces the PRNG output onto a per-cell **candidate
set** rather than onto all four types directly (`MATCH3_RULES.md` §1.2.1.2,
§1.2.1.4). That reduction is a game rule owned entirely by
`MATCH3_RULES.md`; this document records only that it consumes the same
`RngState` one selection per cell and adds no second RNG value, no second
generator, and no additional state field.

## 2.7 Initial Board Generation

The contract for producing `BoardState.Cells[64]` at battle creation. The
generation algorithm — including the constrained fill order and candidate
exclusion of `MATCH3_RULES.md` §1.2.1 — and its validation rules
(`MATCH3_RULES.md` §1.3–§1.4) are owned by `MATCH3_RULES.md`; this section
defines only the staged state obligations and the ownership boundary that
keeps generation separate from gameplay.

### 2.7.1 Staged Flow

```text
1. Server obtains the battle's RNG state (§2.6).
2. Generate a candidate board by deterministic row-major constrained random
   fill (MATCH3_RULES.md §1.2.1).
3. Validate the completed candidate against both initial-board constraints
   (MATCH3_RULES.md §1.3–§1.4).
4. If invalid, retry deterministically (MATCH3_RULES.md §1.5).
5. Accept the valid board into BoardState.Cells[64].
6. Retain the resulting RngState in BattleState (§2.6.2).
```

Steps 2–4 are `MATCH3_RULES.md`'s rules; steps 1, 5, and 6 are this
document's state obligations. Neither section restates the other.

The fill in step 2 consumes one PRNG selection per cell, 64 per attempt
(`MATCH3_RULES.md` §1.2.1.4). This is a consumption pattern of the single
`RngState` pair (§2.6.2), not a change to the RNG contract: no additional
state, seed, or generator is introduced by the constrained fill, and
`RngState` remains the only advancement point.

### 2.7.2 Ownership Boundary

Board generation is an **initialization-time** concern, not gameplay
resolution. It therefore:

1. runs once, at battle creation, before any player action — it is not part
   of the `GAME_RULES.md` §17 resolution pipeline and adds no step to it;
2. emits no Battle Events (`GAME_EVENTS.md` §2);
3. consumes no Turn and no `Sequence` (§2.0.5.2);
4. does not run again after initialization — nothing in this stage re-rolls
   or repairs a board once accepted.

The **Board Generation Validator** it uses to satisfy steps 3–4 — including
the valid-swap check — is bounded precisely in `MATCH3_RULES.md` §1.4.1, and
its separation from gameplay Match Detection and gameplay Swap Validation is
owned by `MATCH3_RULES.md` §1.4.2. This document does not re-assert that
boundary; it records only that generation must not implement, borrow, or
expose a gameplay match/swap engine to satisfy its own constraints.

### 2.7.3 Not Persisted

The generated board and RNG state are held per §2.0.5.4 items 2–3: no
Redis record and no PostgreSQL row is created for them at this stage.

## 2.8 Battle Identity (`BattleState.PlayerId`)

`PlayerId` is the identity of the Player who created this battle: the
account/owner identity (`Player.PlayerId`, `DATABASE.md` §1), recorded from
the authenticated battle-start request (`API_CONTRACTS.md` §1, §3 — the
"requesting Player") at battle creation and carried unchanged in the state
record for the battle's lifetime (ADR-014).

1. **Denotation.** Identity only. The Player is the account/owner with no
   battle-time combat pool (ADR-011): `PlayerId` carries no stats, no
   resource pool, and no gameplay value of any kind — it exists so the
   battle-end persistence path can source `BattleResult.PlayerId`
   (`DATABASE.md` §1) from authoritative state. It is not a lifecycle
   field; §2.0.3's no-`Status` rule is unaffected.
2. **Staging.** Not a Battle State Foundation field (§2.0, §2.0.3) and not
   a Board Foundation field (§2.0.5): the full §2 contract includes it, and
   the implementation stage adds it with its owning system (§0 item 4).
3. **Not a wire member.** `PlayerId` is server state only. It is delivered
   by no `BattleStateUpdated` stage projection (`SIGNALR_PROTOCOL.md` §4 —
   only the members each stage list enumerates are ever projected) and by
   no Battle Event (`GAME_EVENTS.md` §2), and it is excluded from the
   `GetBattleState` snapshot projection (`SIGNALR_PROTOCOL.md` §7.1). State
   added is not wire exposure added (`SIGNALR_PROTOCOL.md` §4 item 4); the
   client never receives it.
4. **Battle-end sourcing.** The battle-end path writes this member, in
   fixed order, into `BattleResult.PlayerId` (`DATABASE.md` §1) before the
   active state record is cleared (`REDIS_STATE.md` §3,
   `ARCHITECTURE.md` §4 item 4). The value is never re-derived from a
   session or from client input at battle end (`GAME_RULES.md` §18,
   ADR-001, `AGENTS.md` §10).

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

pass-local board bookkeeping (never a field of BattleState)
├── ReservedCreationCells       (MATCH3_RULES.md §5.5.4 item 2)
├── RequiredCreations[]         (the pass's Special Gem creations, in the
│                                §5.5.1 order, before they are committed)
├── ClearedCells                (the pass's union accumulator, §5.8.2)
└── ConsumedSpecialGems[]       (the pass's consumed Special Gems, in the
                                 activation and chain order of §5.8.3)
```

`ResolutionContext` is built and discarded entirely within
`BattleResolutionService` (`ARCHITECTURE.md` §4) for a single action.

1. It is the only place the intermediate states of a board resolution exist:
   the board mid-pass, the cells emptied by Match Resolution before Spawn
   refills them, the Special Gems a pass creates
   (`MATCH3_RULES.md` §4.1), the match set of the pass in progress, the
   reserved creation cells and the pass's creation, removal, and activation
   order that goes with them, and the running Combo. None of those is
   `BattleState`, and none is written to the store or published (§5.1,
   §2.1.5 item 4, §2.1.6 item 4).
2. `MatchesThisResolution[]` and the Cascade depth it records are the board
   resolution's own bookkeeping, ordered as `MATCH3_RULES.md` §3.2 and §4.2
   define. They do not create a second Match-count field: the authoritative
   cumulative total is `BattleState.MatchCount` (§2.2).
3. It is not serialized, not delivered, and not recoverable — a snapshot is
   taken between resolutions, never inside one (§5.1 item 2, §5.3 item 3).
4. **The pass-local board bookkeeping is a working set, not a representation.**
   It lists what the pass is in the middle of doing to `Cells[64]`; it is not a
   second copy of the board, and none of it survives the pass that built it. In
   particular, nothing in it is a Special Gem field: a Special Gem exists in
   authoritative state only once it is committed into its cell entry (§2.1.8
   item 1), and a discarded collision claim exists in neither (§2.1.8 item 5).

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
been successfully resolved. This rule is unchanged by §2.0 or §2.0.5 — the
staged subsets simply have no resolutions yet.

## 5.1 When `Sequence` and `Turn` Change (Board Resolution)

This section owns the **state write-back** of the two counters. The board rules
that determine *what counts* are owned by `MATCH3_RULES.md` §8 and are not
restated here.

```text
one attempt to resolve one action
    ↓
validate                                  (MATCH3_RULES.md §2.1.2)
    ├── rejected  → no state change at all: no board, no Turn, no Sequence,
    │                no RngState, no LastCommittedSwapPair, no event
    │                (MATCH3_RULES.md §2.1.5, §2.1.10 item 6)
    └── accepted  → SwapStarted, exchange committed, Turn begins,
                    Combo reset, SwapResolved, board resolved to stability
    ↓
write Turn and Sequence                     (MATCH3_RULES.md §8.3)
    ↓
write LastCommittedSwapPair                 (§2.1.10 item 5, §5.1 item 7)
    ↓
write resulting RngState                    (§2.6.2)
    ↓
one atomic write-back of the whole BattleState, gated on the pre-resolution
Sequence (§4)
    ↓
publish (SIGNALR_PROTOCOL.md §3, §4)
```

1. **One action, one resolution, one `Sequence` value.** A committed Swap
   produces exactly one new `Sequence` value, whatever number of Matches,
   Cascades, or Special Gems it contains (`MATCH3_RULES.md` §8.2).
2. **Nothing is written mid-resolution.** The board after the exchange, the
   intermediate states of `MATCH3_RULES.md` §4.1 (remove / create / gravity /
   spawn), intermediate Combo values, intermediate Match counts, the pass's
   reserved creation cells and its creation order, and every cell the board
   has momentarily emptied are **Transient Resolution State** (§3,
   §2.1.5 item 4, §2.1.6 item 4): they exist only inside
   `BattleResolutionService` (`ARCHITECTURE.md` §4) and are never written to
   the store or published. A reader therefore never observes a Turn in
   progress, and never observes a board that is not full (§2.1.1).
3. **`Sequence` describes the finished resolution.** It is written after the
   board is stable, so the board, `RngState`, Combo, and Match counts that
   accompany it are all the post-resolution values. A reader that sees
   `Sequence = n` sees the complete result of the n-th resolution.
4. **`Turn` is not a version.** It is a game value (`GAME_RULES.md` §2), it is
   written in the same write-back as `Sequence` rather than as a separate
   concurrency step, and it is never compared for optimistic locking (§5,
   `REDIS_STATE.md` §4).
5. **Order within the write.** `MATCH3_RULES.md` §8.3 owns the order of the
   counter updates relative to the resolution; this section owns only that the
   write-back is one operation after all of them, under the compare-and-set in
   §4.
6. **A rejected action writes nothing.** Not the board, not the counters, not
   `RngState` (`MATCH3_RULES.md` §2.1.5). It is not a resolution that failed —
   it is an action that was never a resolution. This includes
   `LastCommittedSwapPair` (§2.1.10 item 6): a rejection records no commit, so
   it neither writes a pair nor clears the one already in force.
7. **The commit record is written in the same write-back, and only for a
   committed Swap.** `LastCommittedSwapPair` (§2.1.10) is set when — and only
   when — a Swap is committed; being part of one `BattleState`, it is written
   in the single post-resolution write-back above rather than as a separate
   step. It is not a resolution counter and is never used for concurrency
   control: `Sequence` remains the only concurrency token (§5, §5.1 item 4,
   `REDIS_STATE.md` §4 item 6).

## 5.2 What `Sequence` Is Not

1. `Sequence` is not a Turn number and is not derived from one: several
   resolutions that begin a Turn are still one increment each, and a non-Swap
   action is not a board resolution at all.
2. `Sequence` is not a Match, Cascade, Combo, or Match-count value, and none of
   those is derived from it.
3. `Sequence` is not the client's correlation id
   (`SIGNALR_PROTOCOL.md` §2 item 1) and is never supplied or influenced by the
   client.
4. `Sequence` counts **actions**, not RNG draws: `RngState` advances per spawned
   cell (`MATCH3_RULES.md` §4.5 item 4) and is entirely independent of it.

## 5.3 Reconnect and Snapshot Compatibility

1. Everything a committed Swap produces is `BattleState`:
   `BoardState.Cells[64]` — including every Special Gem it created and every
   Special Gem its activations consumed (§2.1.7) — `RngSeed`, `RngState`,
   `Turn`, `Sequence`, and the resolution's values in `BattleState`
   (`Combo`, `MatchCount`, §2.2). No resolve result lives outside those
   fields, so no gameplay contract change is needed to snapshot a battle
   after a resolution.
2. Because `RngState` is stored rather than recomputed (§2.6.2 item 4), a
   snapshot taken after any resolution resumes the stream exactly
   (`MATCH3_RULES.md` §7.1 item 3, ADR-008).
3. **What a snapshot does not include:** the transient values of §3
   (`ResolutionContext`). A snapshot is taken between resolutions, never
   inside one (`SIGNALR_PROTOCOL.md` §6 item 1, `REDIS_STATE.md` §4), so there is
   never a partially resolved board to represent. Nothing in the resolution
   contract requires a field the snapshot contract does not already have.
4. **A snapshot carries the board and not the cascade history.** The client
   re-renders from the snapshot and does not replay events (ADR-008), which is
   consistent with the resolution contract because the resolution contract
   exports its result as state, not as a replayable log.
