# Match-3 Rules

**Version:** 1.5 (§6.1/§8 citations updated — `PlayerState.Combo`/
`PlayerState.MatchCount` replaced with `BattleState.Combo`/
`BattleState.MatchCount` per Player-as-owner / Pet-as-combat redesign;
no gameplay rule changed — committed-swap state owner named for §2.1.4's
already-applied check — see `GAME_STATE.md` §2.1.10)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

This document defines the exact Match-3 board behavior. It expands GAME_RULES.md
sections 3, 4 and 7 ("Match Rules", "Cascade Rules", "Special Gem Rules"). If anything
here conflicts with GAME_RULES.md, GAME_RULES.md wins and the conflict must be reported
(see GAME_RULES.md §20).

> **§5.2–§5.4, §5.8 and §5.9 contain NEW DESIGN DECISIONs.** The original
> Special Gem rules were destroyed and are not recoverable. The rules in those
> sections were **authored** to resolve §5.1.1 under `GAME_RULES.md` §20; they
> are not recovered historical content and must not be described as such. Every
> new ruling is labelled `NEW DESIGN DECISION` at its decision point, and
> §5.9.1 indexes all of them.

---

# 1. Board

Board size, cell indexing, Gem types, and initial board generation are defined
in §1.0–§1.5 below.

The board holds exactly one Gem per cell at all times except during the
Remove → Gravity → Spawn sub-steps of resolution.

## 1.0 Cell Indexing and Board Size

```text
Size:      8 × 8 (64 cells)
Indexing:  row-major, 0–63, (0,0) = top-left
```

This is the board's only coordinate convention. No second convention exists.

```text
index  = row * 8 + column
row    = floor(index / 8)
column = index % 8

row     0..7, 0 = top
column  0..7, 0 = left
index   0..63
```

`(0,0)` is the top-left cell and is `index 0`; `index 63` is the bottom-right
cell. Rows and columns are 0-based. The 64 cells are exactly the 8 rows × 8
columns above — a board is never partially filled at rest.

Server state and Phaser presentation both use this convention. A conversion
to screen coordinates is a presentation concern only
(`ARCHITECTURE.md` §2.2.2) and must not introduce a competing board
coordinate system.

## 1.A The Resolution Lifecycle

This document owns the full board-resolution contract. The sections below are
its stages:

```text
Player Swap Action            §2.1 — the action, its validation, rejection,
                                     idempotency, and the accepted lifecycle
      →
Action Validation             §2.1.2
      →
Swap Resolution               §2.1.6
      →
Match Detection               §3 — one pass, ordered, set semantics
      →
Match Resolution              §4.1 step 1
      →
Special Gem Activation        §4.1 step 2, §5.5.5, §5.8.3
      →
Special Gem Creation          §4.1 step 2, §5.5.1–§5.5.4, §5.8.4
      →
Effect Clipping / Overlap     §5.8.1, §5.8.2
      →
Gravity                       §4.4
      →
Spawn                         §4.5 — the only gameplay RNG consumer
      →
Repeat Resolution             §4.2, §4.3 — until a pass finds no Match
      →
Combo Calculation             §6
      →
Turn / Sequence update        §8 — counters, and the order they are written
      →
Resulting BattleState         GAME_STATE.md §2 (state), §5.1 (write-back)
      →
BattleStateUpdated            SIGNALR_PROTOCOL.md §3, §4
```

Match-3 mechanics are owned here (`GAME_RULES.md` §2–§7 expands into this
document). Where a stage is a rule of the game rather than an implementation
obligation, the owning section says so explicitly.

## 1.1 Gem Types (MVP)

```text
ATK
DEF
HP
POWER
```

Gems are functional only. Gems carry no Element (see ELEMENT_RULES.md §1).
A Gem type is not an Element, does not map to one, and confers no elemental
advantage or disadvantage: Element belongs to the Pet/Boss combat system,
not to the board (`GAME_RULES.md` §6, §8). These four types are the complete
MVP set — no other Gem type exists.

## 1.2 Board Generation

1. Initial board is produced by a **deterministic row-major constrained
   random fill** (§1.2.1). It is still randomly generated — the draw is
   random, the candidate set it draws from is constrained.
2. Initial board must not contain a pre-existing Match.
3. Initial board must contain at least one valid Swap (no dead boards).
4. Gem type distribution weights are configuration, default uniform
   (25% each) over the **valid candidate set** for each cell (§1.2.1), not
   over all four types unconditionally.

"Randomly filled" in this document never means independent unrestricted
uniform selection per cell. The initial fill is constrained (§1.2.1).

### 1.2.1 Generation Contract — Deterministic Row-Major Constrained Random Fill

```text
1. Server obtains the battle's RNG state (GAME_STATE.md §2.6).
2. Fill the 8×8 board in deterministic row-major order (§1.2.1.1).
3. Validate the completed candidate against §1.3 and §1.4.
4. If invalid, retry per §1.5.
5. Accept the valid board as the initial board.
6. Retain the resulting RNG state (GAME_STATE.md §2.6.2).
```

Generation is deterministic: the same RNG state always yields the same
candidate board and the same sequence of retries, so the same seed always
produces the same initial board (§7).

The PRNG is the server-seeded generator defined by `ADR-009`
(`GAME_STATE.md` §2.6). Generation must not use any other randomness source.
The generator is a **consumer** of that PRNG — this section changes which
values are requested and in what order, and changes nothing about the
generator itself.

#### 1.2.1.1 Fill Order

Cells are filled one at a time in deterministic row-major order, using the
§1.0 indexing:

```text
index = row * 8 + column
```

Filling proceeds `index 0 → index 63`: row 0 left-to-right, then row 1
left-to-right, and so on. No other fill order exists. The order is fixed and
does not depend on RNG output.

#### 1.2.1.2 Candidate Exclusion (the Constraint)

For the current cell, before any draw:

```text
1. Determine the Gem types that would create an immediate horizontal run of
   3 with already-filled cells (§3's Match shape).
2. Determine the Gem types that would create an immediate vertical run of
   3 with already-filled cells.
3. Exclude those Gem types from the candidate set.
4. Draw uniformly from the remaining valid Gem types using the
   authoritative PCG32 RNG (ADR-009, GAME_STATE.md §2.6).
5. Write the selected Gem into the current cell.
6. Continue to the next cell.
```

Only **already-filled** cells are consulted, so the check is exactly the
completed portion of the run that the current cell would extend:

```text
Horizontal: the current cell is at (row, column). If column ≥ 2 and
            cells (row, column-1) and (row, column-2) are both filled with
            the same type T, then T would create a horizontal run of 3 and
            T is excluded.

Vertical:   if row ≥ 2 and cells (row-1, column) and (row-2, column) are
            both filled with the same type T, then T would create a vertical
            run of 3 and T is excluded.
```

Fewer than two already-filled neighbours in a line cannot create a run of 3,
so that line excludes nothing. Excluded types are the **union** of the
horizontal and vertical exclusions, so at most two types can be excluded:

```text
normal cell, nothing excluded     → candidate set = { ATK, DEF, HP, POWER }
one line excludes one type        → candidate set = remaining 3 types
both lines exclude a type         → candidate set = remaining 2 types
```

The candidate set is built by iterating the four §1.1 types in their
canonical order (`ATK`, `DEF`, `HP`, `POWER`) and omitting the excluded
types. That order is part of the deterministic result, because the bounded
selection is reduced by index into this list.

Selection among the remaining candidates is uniform (equal weight,
default 25% each before exclusion and `1 / |candidate set|` after).

**The candidate set is never empty.** A horizontal and a vertical exclusion
can never remove all four types: they can remove at most one type each, and
two lines cannot jointly exclude more than two distinct types — leaving at
least two valid candidates for every cell of the initial 8×8 board under the
documented rules. This is a property of the constraint as defined, not a
fallback: no rule for an empty candidate set is defined, because the
algorithm cannot produce one.

#### 1.2.1.3 Constraint Is Not a Match Resolution

The exclusion in §1.2.1.2 consults §3's Match shape but is not Match
Detection and produces no match results, tiers, shapes, or events. It
prevents the generator from **creating** an existing match; it detects
nothing during gameplay. The generator never intentionally creates a match
during initial board construction.

#### 1.2.1.4 RNG Consumption

Each cell performs exactly **one bounded RNG selection**. The terminology
below is precise and the three terms are not interchangeable:

```text
cell selection          one Gem choice for one cell, in §1.2.1.1 order
bounded RNG selection   one call to the authoritative bounded sampling
                        operation over the candidate set
PRNG output             one 32-bit value produced by one PCG32 step
RNG state               the PCG32 state pair carried in BattleState
```

The sequence for one cell is fixed:

```text
candidate set
    ↓
bounded RNG selection over the candidate set
    ↓
one Gem type
    ↓
written into the cell
```

1. **One cell selection per cell.** A cell performs exactly one bounded RNG
   selection, regardless of how many candidates remain — a 4-candidate cell
   and a 2-candidate cell each perform one.
2. **A bounded RNG selection may consume more than one PRNG output.** The
   authoritative bounded sampling operation is `pcg32_boundedrand_r`'s
   rejection method: it computes a rejection threshold from the bound and
   repeats "draw one PRNG output; accept it if it is at or above the
   threshold" until a value is accepted, then reduces the accepted value
   modulo the bound to index the candidate set. The reduction is therefore
   **modulo after acceptance**, not a bare modulo of the first draw — an
   unqualified "selection mod candidate count" description is not accurate.
3. **The number of PRNG outputs is deterministic, but not universally
   fixed.** For a given RNG state and candidate sequence the number of
   outputs consumed is fully determined and reproducible; it is **not**
   always one output per cell. Rejection occurs only when a drawn output
   falls below the threshold, which depends on the bound. In practice the
   threshold is `0` for bounds `2` and `4` (which never reject) and `1` for
   bound `3`, so rejection is vanishingly rare yet not impossible. A full
   board therefore performs **exactly 64 cell selections**, but the number
   of underlying PRNG outputs those selections consume is **not** universally
   fixed at 64 — it is deterministic per candidate sequence, and may exceed
   64.
4. **Retry consumes a fresh full pass.** A rejected candidate's next attempt
   is another 64 cell selections continuing from the RNG state the previous
   attempt produced (§1.5 item 1); the generator is never re-seeded or
   restarted, and the stream is never rewound.
5. **Validation consumes no RNG.** The §1.3/§1.4 validation of a completed
   candidate reads the finished board only. It performs no draw, no bounded
   selection, and no PRNG step, so validating a board leaves the RNG state
   exactly where it was.
6. **No other draw exists in generation.** Nothing else in this section
   draws: not the exclusion check, not the fill order, not the indexing, not
   the retry decision, and not validation.
7. **"Randomly choose" is never used unqualified.** Every draw in generation
   is "a bounded RNG selection over the candidate set of the current cell",
   as specified above. Selection is deterministic: the same initial RNG
   state always reproduces the exact same 64-Gem sequence, cell by cell, and
   the same resulting RNG state.

#### 1.2.1.5 Scope — Initial Board Only

This constrained fill applies to **initial board generation only**.

Cascade spawns into empty cells during resolution are **not** generated by
this algorithm: they are unconstrained independent draws from the four §1.1
types using the same distribution (§4 item 2). A cascade spawn has no
constraint — spawned Gems are allowed to form a match, which is precisely
what makes cascades possible. §1.2.1's exclusion is an initialization-time
rule and does not apply to gameplay resolution.

## 1.3 Initial Board Validity — No Pre-existing Match

The initial board must contain **no Match**. A Match here is the §3 shape
applied to the initial board: 3+ Gems of the same type in an unbroken
horizontal or vertical line.

This is an initial-board validity constraint only. It does not create a new
Match definition — it reuses §3's — and it is enforced by the generation-time
validator bounded in §1.4.1, not by the gameplay Match Detector. Checking
this constraint does not make the Board Generation Validator the Gameplay
Match Detector (§1.4.2).

The §1.2.1 constrained fill already prevents the generator from creating a
match during construction, so a board that reaches the validator normally
satisfies §1.3 already. §1.3 remains a separately validated constraint
because the validator checks the **completed board**, not the generator's
intent: it is the guarantee, and the constraint is the mechanism. The
mechanism does not replace the check.

## 1.4 Initial Board Validity — At Least One Valid Swap

The initial board must contain **at least one valid Swap**. A valid Swap is
one that produces at least one Match under §2 and §3: swapping the two Gems
of an orthogonally adjacent pair yields a board containing a §3 Match.

"At least one" is the whole requirement: a board is accepted as soon as one
qualifying pair exists. No minimum count, no distribution requirement, and no
quality measure beyond this is defined.

### 1.4.1 Board Generation Validator

The validator exists **only** to enforce §1.3 and §1.4 during initial board
generation. It is bounded by exactly that purpose:

```text
It may:     evaluate a completed candidate board for a §3 Match shape
            evaluate whether an adjacent pair swap would produce a §3 Match
It may not: emit Battle Events (GAME_EVENTS.md §2)
            calculate or modify Combo
            modify BattleState after initialization
            resolve player actions
            perform cascades, gravity, or spawning (§4)
            create Special Gems (§5)
            generate resources, deal damage, or affect combat
```

It runs once, at initialization, and is not reachable during gameplay. It is
stateless and pure: it reads a candidate board and answers valid/invalid,
mutating nothing and drawing nothing (§1.2.1.4 item 5).

The validator checks **both** initial-board constraints on the completed
candidate board, and both are required:

```text
Board Generation Validator   → final board validation
                               ├── no existing matches    (§1.3)
                               └── at least one valid swap (§1.4)
```

Passing one is not sufficient: a board with no match but no valid Swap is
still rejected and retried (§1.5). The validator is the guarantee that the
accepted board satisfies §1.3 and §1.4; the §1.2.1 constrained fill is the
construction mechanism that keeps §1.3 satisfied while the board is built.

### 1.4.2 Distinct From Gameplay Match Detection and Swap Validation

```text
Board Generation Validator      → enforces §1.3/§1.4 at initialization
Gameplay Match Detection (§3)   → detects matches during resolution
Gameplay Swap Validation (§2)   → validates a player's Swap action
```

These are **different responsibilities with different rules and different
lifecycles**, and the Board Generation Validator must not become either of
them:

1. It is not the Gameplay Match Detector. It never runs as part of a Swap,
   cascade, or resolution pass, and it produces no match results, tiers,
   shapes, or events (§3, §4).
2. It is not the Gameplay Swap Validator. It never processes a player Swap
   request, never commits or reverts a Swap, and never starts a Turn (§2).
3. Its answer is used only to accept or reject a **candidate board during
   generation**. It is not consulted for any gameplay decision.

The gameplay match/swap pipeline (§2–§5, `GAME_RULES.md` §17) is owned by
the resolution contract in those sections. Reproducing a partial gameplay
engine to satisfy §1.4 is not authorized: the validator's scope is the two
constraints above, and nothing else.

### 1.4.3 Implementation Note

A generation-time evaluation that checks whether any adjacent swap creates a
§3 Match is the minimum needed to satisfy §1.4. It is not required to be the
gameplay algorithm, not required to enumerate all matches, and not required
to be reusable by the gameplay resolver — the resolver implements §2–§5 in
full on its own terms.

## 1.5 Retry on Invalid Candidate

1. **Retry is deterministic.** A rejected candidate is discarded and the next
   candidate is drawn by continuing from the RNG state the rejected attempt
   produced (§1.2.1.4 item 4, §7). The generator is never re-seeded,
   restarted, or rewound per attempt.
2. **Retry is bounded, not unlimited.** Generation makes at most **64**
   attempts. Each attempt performs a fresh pass of 64 cell selections
   (§1.2.1.1), continuing the same stream.
3. **If no candidate passes within 64 attempts**, generation fails and the
   battle creation is rejected as an error. No fallback construction is
   defined: there is no partial fill, no forced pattern, no repair pass, and
   no "best effort" board. A battle is never created with an initial board
   that violates §1.3 or §1.4.
4. **Termination.** The bound in (2) makes termination unconditional — the
   loop cannot run forever. It is an implementation safeguard expressed as a
   rule, not a gameplay mechanic. The bound is an engineering safety bound on
   the **whole** candidate pipeline, not an estimate of how often a candidate
   is rejected. The §1.2.1 constrained fill guarantees §1.3 during
   construction, so the §1.3 check is not expected to reject a candidate; the
   §1.4 check is the one that can. A candidate is rejected only if the
   completed board happens to contain no valid Swap, in which case a fresh
   candidate is drawn from the same stream. No specific success probability is
   guaranteed, and none is required: the retry mechanism remains deterministic
   and bounded at 64 attempts, and a battle is never created with a board that
   violates §1.3 or §1.4 (item 3).
5. **Validation itself consumes no RNG.** The §1.4.1 validator performs no
   draw (§1.2.1.4 item 5), so rejecting a candidate consumes nothing beyond
   the cell selections of the attempt that produced it.
6. **No combat or gameplay effect.** A failed generation is a creation-time
   error. It is not a Match, cascade, or gameplay outcome, and it does not
   involve Turn, Combo, Power, or damage.

---

# 2. Swap

1. The player selects two orthogonally adjacent Gems (up/down/left/right). Diagonal
   swaps are invalid.
2. The server simulates the swap on a copy of the board.
3. If the simulated swap produces at least one valid Match, the swap is committed.
4. If the simulated swap produces no Match, the board is reverted and no Turn,
   Match, Combo, Power, Passive or Relic progression occurs.
5. A committed Swap always begins exactly one new Turn (GAME_RULES.md §2) and
   resets Combo to 0 before the first Match of that Swap is counted.

The items below make the server-side implementation contract of that rule
explicit. They constrain the *implementation*, not the game rule: where an item
restates a rule from item 1—5 it is that rule read operationally, not a second
rule, and this section remains the single owner of Swap validation.

## 2.1 Swap Action Contract

### 2.1.1 Identity and Parameters

```text
from   0..63   cell index of the Gem the player is moving (MATCH3_RULES.md §1.0)
to     0..63   cell index of the adjacent cell it is exchanged with
```

1. **Indexing.** Both values are §1.0 row-major cell indices `0..63`. This is
   the board's only coordinate convention: a Swap action carries no row/column,
   screen, or pixel coordinate, and the server defines no second convention for
   it (§1.0, ARCHITECTURE.md §2.2.2).
2. **No direction field.** The action carries both cells and no direction, and
   the *order* of `from` and `to` has no gameplay meaning: swapping `(12, 13)`
   and swapping `(13, 12)` are the same Swap, produce the same board, and the
   second is therefore rejected as already applied (§2.1.4) rather than applied
   again. The unordered pair `{from, to}` is the swap's identity.
3. **No client-supplied gameplay field.** The action carries no Gem type, no
   match result, no Combo value, no Turn, and no Sequence. Every such value is
   server-determined (GAME_RULES.md §18, ADR-001); the client sends the two
   cells it is exchanging and nothing that could be authoritative.
4. **Per-battle identity.** The battle the action applies to is identified by
   the transport, not by the payload (`SIGNALR_PROTOCOL.md` §2) – the action
   does not carry a second battle identifier of its own.
5. **Wire shape.** The transport parameters for this action – including the
   client correlation value that accompanies it – are owned by
   `SIGNALR_PROTOCOL.md` §2. This section owns *what a Swap is*, not how it is
   framed on the wire.

### 2.1.2 Validation Order

Every Swap action is validated by the following checks, and **all checks pass**
before the swap is committed (§2 item 2—3):

```text
1. Index range and distinctness
2. Adjacency (item 1)
3. Already applied / idempotency (§2.1.4)
4. Match-producing (§2 item 3, applying §3)
```

1. **Index range (§1.0).** `from` and `to` must each be a cell index in
   `0..63`. A value outside that range is not a cell and the action is
   rejected (`INVALID_CELL_INDEX`).
2. **Distinct cells.** `from` must differ from `to`; a swap of a cell with
   itself is not a Swap and is rejected (`INVALID_CELL_INDEX`).
3. **Adjacency (§2 item 1).** The two cells must be orthogonally adjacent:
   `|row(from) − row(to)| + |column(from) − column(to)| = 1`, using the §1.0
   mapping. A diagonal pair, a distant pair, and a same-cell pair are all
   rejected (`INVALID_SWAP`). Diagonal swaps are invalid and no diagonal
   variant of the action exists.
4. **Already applied / idempotency (§2.1.4).** The action's unordered pair
   `{from, to}` must not be the pair most recently committed to the board.
   The check reads `BattleState.LastCommittedSwapPair` (`GAME_STATE.md`
   §2.1.10); when that record is present and its canonical pair equals the
   action's, the action is rejected (`STALE_ACTION`). No RNG and no board
   simulation is involved: the check compares two cell-index pairs.
5. **Match-producing (§2 item 3).** The swap is simulated on a copy of the
   board (§2 item 2) and must produce **at least one** §3 Match.
   "At least one" is the whole requirement, exactly as §1.4 defines it for
   initial-board validation: no minimum number of matches, no tier
   requirement, and no quality measure beyond this. A swap that produces no
   match is rejected (`NO_MATCH_FROM_SWAP`) and the board is reverted
   (§2 item 4).

Checks are evaluated in the order above so that two implementations reject the
same action with the same reason when more than one check could fail.

A Swap that produces no Match is **not** a resolved Swap with a zero-Match
result: it is a rejected action and the board reverts (§2 item 4). A committed
Swap therefore always contains at least one Match, which is what §6.5 relies on.

### 2.1.3 Coordinate and Boundary Rules

1. Adjacency is evaluated on the §1.0 row-major mapping, never on raw index
   arithmetic alone: `index 7` and `index 8` differ by 1 but are **not**
   adjacent (row 0 column 7 vs. row 1 column 0), while `index 7` and `index 6`
   are. No wraparound exists across a row edge.
2. Cells on the board edge have fewer neighbours; a swap naming a non-existent
   neighbour is rejected by the adjacency check, not clamped, folded, or
   reinterpreted.
3. `from` and `to` are symmetric inputs: neither is a "primary" cell for
   *validation*. Where the two cells subsequently differ – special-gem
   placement, §5.5.3 – the distinction is drawn from which cell's Gem
   participated in the resulting Match, which is a property of the validated
   board state and not of the order the player supplied.

### 2.1.4 Staleness and Idempotency

Idempotency is defined against the **action's identity within its Turn**, not
against an opaque request identifier:

1. Each Swap action carries the client's correlation value, which correlates a
   request with its result and is **not** the authoritative
   `BattleState.Sequence` (`SIGNALR_PROTOCOL.md` §2 item 1). It is not used to
   detect staleness: it identifies the request, not the position of the action
   in the battle's history.
2. **Already applied.** If the action's unordered pair `{from, to}` is exactly
   the pair most recently committed to the board, the action is **rejected as
   already applied** (`STALE_ACTION`). It is not applied a second time: the
   previous commit already exchanged those two cells, so applying it again
   would revert it – which is never a Swap (§2 item 3 requires a produced
   Match). The two cells are exchanged once per Turn at most.

   **Where that pair lives.** The comparison reads
   `BattleState.LastCommittedSwapPair`, the authoritative record of the most
   recently committed Swap, owned by `GAME_STATE.md` §2.1.10 — including its
   canonical `(min, max)` ordering, its absent-before-first-commit initial
   value, and when it is written. The rule is stated here; the state it reads
   is stated there, and this section does not restate that state's shape.
   Because the stored pair is canonical and the action's pair is `{from, to}`,
   the comparison is between unordered pairs: `Swap(13, 12)` after a committed
   `Swap(12, 13)` is the already-applied case, exactly as `Swap(12, 13)` is
   (§2.1.1 item 2).
3. **Superseded (out of order).** A Swap is a stateless exchange against the
   *current* board: a Swap naming a pair that is not the pair most recently
   committed is validated only as a swap of the board's current contents and
   against the current Turn. There is no queue per Turn and no requirement
   that a Turn's action exceed the value in force when it was issued: the
   action either satisfies §2.1.2 against the current board, or it does not.
   If it does not, it is rejected and no rejected action is ever replayed
   later (§2.1.5).
4. **No in-flight window is observable.** The server resolves one action at a
   time per battle (`SIGNALR_PROTOCOL.md` §6 item 1); an action arriving before a
   prior resolution has finished is resolved strictly after it, in arrival
   order (`SIGNALR_PROTOCOL.md` §6 item 2), never concurrently with it.
5. `STALE_ACTION` therefore has exactly the meaning in item 2. It is not a
   version-mismatch code: nothing in this contract compares a client-supplied
   number against `BattleState.Sequence`.
6. The rejection is deterministic: the same board and the same action always
   produce the same outcome.

### 2.1.5 Rejection Is a Gameplay No-Op

A rejected Swap (§2.1.2) is **not** an action resolution:

1. **The board is unchanged**, state-for-state, not merely equivalent: no
   exchange is committed, the board reverts to its pre-action state, and the
   64 cells hold exactly the values they held before the action arrived
   (§2 item 4).
2. **`Turn` is unchanged.** A rejected Swap is not a player Swap that
   successfully resolved, so it begins no Turn (§8.1; GAME_RULES.md §2 item 1).
   A later accepted Swap commits against the unchanged `Turn`, exactly as if
   the rejected action had never been sent.
3. **`Sequence` is unchanged.** `Sequence` increments only for a successfully
   resolved action (§8.2); a rejection resolves nothing (`GAME_STATE.md` §5).
4. **`RngState` is unchanged.** Validation performs the whole match evaluation
   on the copy (§2 item 2) and draws **nothing**: no RNG selection is consumed
   by index validation, adjacency, staleness, simulation, or match detection
   (§7.2). A rejected action therefore advances the PRNG by exactly zero
   selections, and a client that resends a rejected action until it is
   accepted leaves the RNG stream in the same place as one that sends only the
   accepted action.
5. **Nothing else changes.** No Match is counted, no Combo is set or reset, no
   Power, Passive, or Relic progression occurs, no Special Gem is created, and
   no battle state field is written (§2 item 4). This includes
   `BattleState.LastCommittedSwapPair` (`GAME_STATE.md` §2.1.10 item 6): a
   rejected action commits nothing, so it neither records a pair nor clears the
   previously committed one — a rejection can never make an earlier commit
   forgettable.
6. **No Battle Event.** A rejected Swap emits no Battle Event, including
   `SwapStarted`/`SwapResolved` (`GAME_EVENTS.md` §2). The rejection is
   reported to the caller only (`SIGNALR_PROTOCOL.md` §5).
7. Rejection is deterministic: the same board and the same action always
   produce the same rejection and the same reason.

### 2.1.6 Accepted Swap Lifecycle

An accepted Swap is a **resolution** and is the unit `Turn` and `Sequence`
count (§8):

```text
1. Receive the Swap action
2. Validate it (§2.1.2) – draws nothing (§7.2)
3. Emit SwapStarted (GAME_EVENTS.md §2)
4. Commit the exchange to the board (§2 item 2, item 3) and record the
   committed pair as `BattleState.LastCommittedSwapPair`
   (GAME_STATE.md §2.1.10 item 5)
5. Begin exactly one new Turn (§2 item 5, §8.1)
6. Reset Combo to 0 (§2 item 5, §6.1)
7. SwapResolved   (GAME_EVENTS.md §2)
8. Detect matches (§3) and resolve the board until it is stable (§4)
9. Update Turn and Sequence (§8.3)
10. Retain the resulting RngState (GAME_STATE.md §2.6.2)
11. Publish the authoritative state (SIGNALR_PROTOCOL.md §3, §4)
```

1. Steps 3—8 are **one resolution**: they complete entirely before step 9
   writes `Turn`/`Sequence` and before step 11 publishes anything
   (`GAME_STATE.md` §5.1). No reader – no other action, no snapshot request,
   no delivery – can observe the battle between step 2 and step 9.
2. Step 2 rejects the action and stops there when any check fails; steps 3—11
   then do not run at all (§2.1.5).
3. Within the resolution the board passes through the transient intermediate
   states of §4 (Match Remove → Gravity → Spawn), where the board is
   temporarily not full. Those states are **Transient Resolution State** and
   are never the published board (§1: a board holds exactly one Gem per cell
   at rest; `GAME_STATE.md` §3).
4. A Swap that is a **player** Swap is the only action this section validates.
   The Board Generation Validator is not this validator and never processes a
   player Swap request (§1.4.2 item 2).

---

# 3. Match Detection

1. A Match is 3+ Gems of the same type in an unbroken horizontal or vertical line.
2. Minimum match length is 3. There is no maximum; a run of 6+ identical Gems in
   one line is still evaluated as a single match shape but yields the highest
   applicable special-gem tier for that line (see §5.3).
3. L-shapes and T-shapes are two intersecting lines (one horizontal + one vertical,
   sharing exactly one Gem) each of length ≥ 3, evaluated as defined in §5.4.
4. All simultaneous matches formed by a single board state (post-swap, or post-
   cascade) are detected together in one detection pass, not sequentially.
5. Every distinct match shape detected in a pass counts as one Match for the
   purposes of GAME_RULES.md §3 (Match Rules) and §5 (Combo Rules).

## 3.1 Detection Contract

1. **One pass per board state.** Detection runs on a single, fixed board state
   – the board immediately after the committed Swap, or the board immediately
   after a Gravity+Spawn step (§4). It never reads a partially updated board.
   Match Detection and Match Resolution are not interleaved within one pass
   (item 4).
2. **Simultaneous, then ordered reporting.** All matches present in that board
   state are detected *together* – one pass yields the complete set – and only
   then are they removed. "Together" is about *what forms a match*, not about
   processing order: the resulting match set is enumerated in the fixed
   deterministic order of §3.2, because Passives, Relics, resource generation,
   and the event stream all depend on a stable order (PASSIVE_RULES.md §5).
3. **Shapes are detected as lines first, then grouped.** A maximal horizontal
   run of 3+ and a maximal vertical run of 3+ of one type are the primitive
   shapes. Two primitives of the same type that share exactly one cell are one
   L/T shape (§5.4); primitives that share no cell are independent matches.
4. **A detection pass produces a match set, and a match set is counted as one
   Match per distinct shape** (item 5, §6.2).

## 3.2 Deterministic Match Order

Within one detection pass, matches are ordered and processed by:

```text
1. Orientation:  horizontal shapes before vertical shapes
2. Start cell:   ascending row-major index of the shape's first cell
```

1. A shape's **first cell** is the lowest §1.0 index among its own cells – the
   leftmost cell for a horizontal shape (same row, lowest column) and the
   topmost cell for a vertical shape (same column, lowest row).
2. A horizontal shape and a vertical shape that share a cell are not ordered
   against each other as two entries: they are the single L/T shape of §5.4
   and occupy the position of their intersection cell (§5.5.3 item 4).
3. This order is deterministic and depends only on the §1.0 indexing – never on
   dictionary iteration order, insertion order, allocation order, or any
   unordered collection.
4. The order in this section is **within** a detection pass. The order of
   passes themselves is owned by §4.2 (cascade depth), which is the outer
   order.

## 3.3 Duplicate Cells and Shared Cells

1. **Duplicate entries are removed by construction, not by de-duplication
   afterwards.** Primitives are maximal runs: each cell belongs to at most one
   horizontal primitive and at most one vertical primitive. The match set is
   therefore a set of shapes with **no duplicate cells** and **no duplicate
   shapes** within a pass – the same cell can never appear in two horizontal
   shapes, or in the same shape twice.
2. **A shared cell makes an L/T shape, not two overlapping matches.** Two
   primitives of the same Gem type sharing exactly one cell are one L/T match
   (§5.4), counted once (§5.5.2 item 2), and the shared cell is removed once.
   Two primitives of *different* Gem types cannot share a cell at all, because
   a cell holds exactly one Gem type.
3. **Removal happens once per cell.** Match Resolution (§4.1) removes the union
   of the cells in the pass's shapes; a cell named by more than one shape is a
   single cell for removal, resource generation, and `GemMatched`
   (GAME_EVENTS.md §2). Removal and accounting are over the union as a set;
   the order in which its `GemMatched` events are emitted is the event
   contract of `GAME_EVENTS.md` §1.3 (ascending §1.0 cell index).
4. **Removal is simultaneous for the pass.** All matched cells are removed from
   the board state as one step – the board does not exist in a state where
   only some of a pass's matched cells are gone.

## 3.4 Match, Cascade, and Combo Are Distinct

```text
Match      one distinct shape detected in one detection pass (§3 item 5)
Cascade    one further detection pass produced by Gravity+Spawn (§4.2, §4.3)
Combo      the running count of Matches within one Swap (§6)
Turn       one player Swap/Action (GAME_RULES.md §2)
```

1. A Match is counted when it is detected in a pass, not when its Gems are
   removed, and not when its resources are generated: one detection pass that
   finds three distinct shapes counts three Matches (GAME_RULES.md §3 item 2).
2. Gems cleared by a Special Gem's effect (§5.5.5) are **not** a Match
   (PASSIVE_RULES.md §2.2) and are never added to the match set of a pass.
3. A Cascade is not a Combo increment by itself: the Cascade is the extra pass,
   and the Match contained in it is what advances the Combo (§6.3).
4. The per-swap totals are owned by §6.5 item 4 / §6.7 and GAME_RULES.md §2
   item 2 / §5.
---

# 4. Cascade

Resolution loop after a committed Swap or after any Match resolution:

```text
1. Remove all Gems belonging to matched shapes from this detection pass
2. Apply gravity (each column's remaining Gems fall to fill empty cells below them)
3. Spawn new random Gems into now-empty cells at the top of each column
4. Run Match Detection (§3) on the resulting board
5. If new matches exist → this is a Cascade → go to step 1
6. If no new matches exist → Cascade loop ends
```

Rules:

1. Gravity only moves Gems downward within their own column; Gems never move
   between columns.
2. Spawned Gems use the same distribution as §1.2 (default uniform, 25% each)
   unless a Relic/effect overrides it. A cascade spawn is **not** a §1.2.1
   constrained fill: it draws independently from all four §1.1 types with no
   candidate exclusion, because a spawn has no build-up order and is allowed
   to form a match (§1.2.1.5). The positions, order, and RNG consumption of
   that draw are owned by §4.5.
3. Special Gems (see §5) activated during a Cascade resolve their effect
   immediately when removed, before the next Gravity/Spawn step. Which Special
   Gems a pass activates, which it creates, where, and in what order is owned
   by §4.1 step 2 and §5.5.1—§5.5.5.
4. Every Match found during a Cascade iteration increases Combo (GAME_RULES.md §5)
   and total Match count (GAME_RULES.md §3), identically to the first Match of
   the Swap.
5. There is no hard cap on Cascade depth in MVP; the loop ends naturally when the
   board stabilizes (§4.3). Implementations must guard against non-terminating boards
   (e.g. a spawn table that can regenerate infinite matches) but this is an
   implementation safeguard, not a gameplay rule.

## 4.1 Resolution Steps Within One Detection Pass

The shape of steps 1—3 of the loop above, made explicit and fixed:

```text
1. Match Resolution   remove the union of the pass's matched cells (§3.2, §3.3)
2. Special Gems       activate the Special Gems this pass consumed, then create
                      the Special Gems its shapes require, at their §5.5.3
                      positions – before Gravity runs
3. Gravity            each column's remaining Gems fall to fill empty cells
                      below them
4. Spawn              draw new Gems into the now-empty cells at the top of
                      each column
5. Re-detect          run Match Detection (§3) on the completed board
```

1. **Step 2 is here, not in §5.** §5 owns *which* Special Gems exist and what
   they do; this step is the point at which Match Resolution activates the ones
   it consumed and creates the ones the pass's shapes require.
2. **Creation is not a delayed effect.** A Special Gem required by a shape is
   on the board before step 3 runs, so it is subject to the same Gravity and
   Spawn as every other Gem and cannot be skipped or displaced by them.
3. **Activation and creation both resolve in step 2, in a fixed order.** Step 2
   activates the Gems the pass consumed and *then* creates the Gems its shapes
   require; both happen before Gravity. The exact order and the reason are
   §5.5.5 item 3, and the rule that keeps the two from colliding is §5.5.4
   item 2.
4. **A Special Gem created in this pass is not itself matched in this pass.**
   It is created after Match Detection has produced the pass's match set and
   after that set's cells are removed, so it cannot be part of a match that has
   already been detected, and it is not re-examined until step 5.
5. The step order is fixed and may not be reordered. `GAME_RULES.md` §17's
   logical order ("Resolve Board" → "Detect Match" → "Remove Matched Gems" →
   "Apply Gravity" → "Spawn Gems" → "Detect Cascade") is this loop, expressed
   once per iteration.

## 4.2 The Cascade Loop

```text
Detection pass 1   (board after the committed Swap)     → depth 1
        ↓  remove / activate+create / gravity / spawn
Detection pass 2   (board after pass 1's Gravity+Spawn)  → depth 2, a Cascade
        ↓  remove / activate+create / gravity / spawn
Detection pass 3   (board after pass 2's Gravity+Spawn)  → depth 3, a Cascade
        ↓
      … until a pass detects no Match
```

1. **Depth 1 is the first pass** – the one run on the board produced by the
   committed Swap (§2.1.6 step 8). It is **not** a Cascade.
2. **Depth ≥ 2 is a Cascade.** A pass at depth `d ≥ 2` is a Cascade, and the
   Cascade's depth index within the Swap is `d − 1` (the second pass is
   `CascadeCreated` depth 1 – GAME_EVENTS.md §2). A pass that detects **no**
   match produces no Cascade: the Cascade loop ends at the last pass that did
   detect one (§4.3).
3. **Passes are strictly sequential.** Pass `d + 1` runs only after pass `d`'s
   Gravity and Spawn have completed. Two passes never run against the same
   board state, and Gravity and Spawn never interleave with detection.
4. **The pass count per Swap is bounded only by the board stabilising** (§4
   item 5). No cascade limit is introduced here.
5. Every Match in every pass – depth 1 and every Cascade – is processed by the
   same rules (§3, §4.1, §5) and counted identically (§6.2, §3.4 item 1).

## 4.3 When the Cascade Ends

1. The loop ends when a detection pass produces **no matches** on the fully
   resolved board – that is, after that pass's board (post-Gravity, post-Spawn,
   fully populated, and with every Special Gem effect that pass produced
   already resolved) contains no §3 shape anywhere.
2. "No matches remain" is evaluated on the **completed, full** board: a board
   that is momentarily empty in some cells during §4.1 steps 1—4 is not
   evaluated for termination.
3. When the loop ends, the board holds exactly one Gem per cell (§1) and that
   board is the state published by the Swap's resolution (§2.1.6 step 11).
4. The terminating pass is still a **detection pass** and still emits no Match
   events, because it detected no Match (GAME_EVENTS.md §1).
5. A Swap whose first pass (depth 1) contains no Match is not reached: §2.1.2
   item 4 rejects it before it is committed. The loop therefore always begins
   with at least one match.

## 4.4 Gravity

Gravity is a server-side state transition, not an animation. It moves the Gems
that remain on the board after §4.1 step 1 downward within their own column:

```text
for column = 0 → 7:                     (fixes the columns' new contents)
    for row = 7 → 0:                    (bottom to top)
        if cell(row, column) is empty:
            pull the nearest non-empty cell above it down into it
```

1. **Columns are processed in ascending §1.0 column order** (`0 → 7`).
   **Within a column, cells are resolved from the bottom up** (`row 7 → 0`).
   Both orders are fixed and are not an implementation choice.
2. **Each column is independent.** A Gem never leaves its own column, so the
   result of one column cannot depend on another column's contents. The
   column order above therefore exists for determinism and reporting, not
   because columns interact – and it must still be fixed, so that two
   implementations agree on the order of any per-column effect.
3. **Gems never change relative order.** Within a column, the surviving Gems
   keep their original top-to-bottom sequence and compact into the lowest
   cells; a Gem never passes another Gem. Gravity is order-preserving, and is
   not a sort, a shuffle, or a re-draw.
4. **Gravity is sequential within a column and has one deterministic result.**
   The result is uniquely defined by item 3 – "the survivors of the column,
   in their original order, occupying the lowest cells" – so simultaneous and
   sequential formulations give the same board. Where a mechanism must be
   chosen, columns are resolved in ascending order (item 1).
5. **Empty-cell representation.** Gravity operates on cells that were vacated
   by §4.1 step 1; an empty cell is a cell with **no Gem**, a state that exists
   only inside one resolution's §4.1 steps 1—4 and never in the published board
   (§1, `GAME_STATE.md` §3). No marker Gem type, sentinel value, or negative
   index is introduced into the four-type value domain (§1.1): an empty cell is
   the absence of a Gem, not a fifth Gem.
6. **Gravity draws no RNG.** It is a pure rearrangement (§7.2).
7. **Gravity produces no Match, no Combo, and no events.** It only relocates
   Gems; the board is re-examined by the next detection pass (§4.1 step 5).
8. **Interaction with Special Gems.** A Special Gem is a Gem occupying a cell
   and falls under exactly these rules – it is pulled down like any other Gem,
   keeps its place in the column's order, and never moves between columns. Its
   effect is not re-triggered by falling; activation happens only as §5.5.5
   defines.

## 4.5 Spawn

Spawn fills the cells that Gravity left empty at the **top of each column**
(§4 step 3). It is a gameplay draw, not initial board generation:

```text
for column = 0 → 7:
    for each still-empty cell in that column, top to bottom:
        draw one Gem and place it
```

1. **Spawned positions.** Spawn fills exactly the cells that are empty after
   Gravity, and those cells are always the topmost cells of their column. A
   column with `k` survivors spawns exactly `k` Gems into its top `k` cells
   (rows `0 … k−1`); a column with no survivors spawns 8. No other cell is ever
   filled by Spawn, and no cell is filled twice.
2. **Column order is ascending §1.0 column order** (`0 → 7`). **Within a
   column, the empty cells are filled top to bottom** (`row 0 → row k−1`).
   Both orders are fixed, and the draw order within a column therefore runs
   from the highest empty cell downward.
3. **Gem distribution.** Each spawned Gem is an independent uniform draw from
   the four §1.1 types, default 25% each – the same distribution as §1.2 item 4
   and §4 item 2, and **not** the §1.2.1 constrained fill. §1.2.1's candidate
   exclusion is an initialization-time rule and does not apply here; a spawn is
   allowed to create a match, which is precisely what makes cascades possible
   (§1.2.1.5). If a Relic or effect overrides the distribution (§4 item 2), the
   override supplies the weights and this section supplies the order and the
   draw count.
4. **RNG consumption is exactly one selection per spawned cell.** A spawn of
   `s` cells consumes exactly `s` selections – one per cell, in the order of
   item 2, with no draw for the fill order, the position, the weights, the
   cell's emptiness, or anything else (§7.2). A pass that spawns nothing draws
   nothing.
5. **Conversion.** The selection is reduced onto the four-type value domain in
   the fixed §1.1 order, as §7.3 specifies. This is the general four-type case
   of the same reduction §1.2.1.4 item 2 documents for the constrained fill,
   and is not a second mechanism.
6. **Spawn is simultaneous in effect, sequential in the stream.** The Gems a
   spawn step places are all placed before the next detection pass (§4.1
   step 5), and the board is never detected while a spawn step is incomplete.
   The order in item 2 is the order of consumption, and it is part of the
   deterministic result.
7. **Interaction with Special Gems.** A spawned Gem is an ordinary Gem of one
   of the four §1.1 types: **Spawn never creates a Special Gem.** Special Gems
   come only from §4.1 step 2 / §5. A spawned Gem placed where a Special Gem
   was removed is an ordinary Gem, and the Special Gem's effect has already
   resolved (§5.5.5).
8. **Determinism of the whole step.** For a given board-after-Gravity and a
   given `RngState`, the spawn step produces exactly one board and one
   resulting `RngState` (§7.1).

## 4.6 The Loop Is Deterministic End to End

1. Given the same starting board, the same committed Swap, and the same
   `RngState`, the whole §4 loop – every pass, every removal, every created
   Special Gem, every fall, every spawn, and the final board and `RngState` –
   is fully determined (§7.1).
2. Nothing in the loop depends on iteration order of an unordered collection,
   on timing, on allocation, or on any second source of randomness (§7.2).
3. The only operation in the loop that consumes randomness is Spawn
   (§4.5 item 4). Everything else in §3—§5 is a pure function of the board
   state.

# 5. Special Gems

> **Recovery note — §5.1–§5.4 were incomplete; they are now RESOLVED by
> design decision.**
>
> **Historical recovery:** only the following was ever confirmed by surviving
> artifacts — a Special Gem is an area-clear board object
> (`GAME_RULES.md` §7), it exists per Match 4 / Match 5 / L-T
> (`MVP_SCOPE.md` §1, `COMBAT_RULES.md` §2), its type names are
> `Line Clear Gem` / `Burst Gem` / `Area Gem` (`GAME_STATE.md` §2.1.2), and it
> is a tracked board object (named `BoardState.PendingSpecialGems[]` in the
> state contract at the time of the recovery).
> No surviving artifact ever defined creation position, effect geometry, or
> how many Special Gems a shape produces.
>
> *(State-representation note, added after this recovery.)* The
> `PendingSpecialGems[]` name above is the wording of an earlier revision of
> `GAME_STATE.md`. That field no longer exists: `GAME_STATE.md` §2.1 now holds
> a Special Gem inside its cell in `BoardState.Cells[64]`. This is a change to
> the *state representation only* — no rule in this document depends on it.
>
> **NEW DESIGN DECISION:** the rules in §5.2, §5.3, §5.4, §5.8, and §5.9 below
> are **newly authored**, not recovered. They were written to resolve §5.1.1
> under `GAME_RULES.md` §20 and are labelled at each decision point. They are
> not historical content and must never be described as "restored".

`GAME_RULES.md` §7 owns the Special Gem rule statement and defers the exact
types and behavior to this document. A Special Gem is a board object that
occupies a cell and carries a clear effect (§5.5); it is not one of the four
§1.1 Gem types as a type of its own (§5.5.4 item 4 in the resolution
contract) — the cell it occupies still holds one of those four types, which is
the type the Special Gem replaced (`GAME_STATE.md` §2.1.3 items 1–2).

## 5.1 Match 3

1. A Match 3 produces **no Special Gem** in MVP.
2. It triggers base resource generation at the Match-3 tier multiplier owned
   by `COMBAT_RULES.md` §2 (see §6.1 below for the trigger, not the value).

## 5.1.1 Resolution Record — Previously Unresolved Rules

This section previously listed the Special Gem rules that **no surviving
document defined**. They are now resolved by explicit design decision
(`GAME_RULES.md` §20). The table records where each answer lives; it is a
pointer, not a second statement of the rules.

```text
Question                                  Now owned by
----------------------------------------  ------------------------------------
1. Creation position
   Match 4 / Match 5, from a Swap           §5.2 item 1, §5.3 item 1, §5.5.3
   Match 4 / Match 5, from a Cascade        §5.5.3 item 3
   L/T shape                                §5.4 item 2, §5.5.3 item 4
2. Effect geometry
   which cells each type clears             §5.2 item 2, §5.3 item 2, §5.4 item 3
   orientation from the creating Match      §5.2 item 3
   whether the area includes its own cell   §5.2 item 2, §5.3 item 2, §5.4 item 3
   which parts are configuration            §5.2 item 4, §5.3 item 4, §5.4 item 5
3. Special Gem count per shape
   exactly one gem per qualifying shape     §5.5.2 item 1
   a shape qualifying at more than one tier §5.4 item 4, §5.5.2 item 2
   more than one gem per resolution         §5.5.1 item 2, §5.5.4 item 3
4. Match 6+                                §5.3 item 3 (no separate tier)
```

**NEW DESIGN DECISION — the four numbered points above are new rulings, not
recovered ones.** The one exception is item 4's *conclusion* (that
`MVP_SCOPE.md` §1 lists only Match 3 / Match 4 / Match 5 and therefore defines
no Match-6 tier): that observation was already recorded, and §5.3 item 3 now
supplies the actual behavioral ruling that the observation left open.

Everything below this point is normative. There are **no remaining unresolved
Special Gem rules required to implement MVP Special Gems**, and there is no
longer any Special-Gem-adjacent open item anywhere in this document set. The
`BoardState.PendingSpecialGems[]` **field shape** that was the last open item
(the serialization-representation gap previously recorded in `GAME_STATE.md`
§2.1.2 item 5) has been **resolved and closed**: `GAME_STATE.md` §2.1 defines
`BoardState` as `Cells[64]` alone, with each cell entry carrying its Gem type
plus an optional Special Gem, and defines that representation's ownership,
ordering, and serialization. It was never a gameplay rule, and this document's
rules are unaffected by it (§5.5.4 item 4, §5.9.3 item 3).

## 5.2 Match 4 — Line Clear Gem

1. **Creation, and creation position.** A Match 4 creates exactly one
   **Line Clear Gem**, at the **swap origin** (§5.5.3 item 1) when the shape
   was produced by the accepted Swap, and at the **line centre**
   (§5.5.3 item 3) when it was produced by a Cascade. The position rule is
   owned by §5.5.3; this item records only that Match 4 allocates exactly one
   Line Clear Gem and does not restate the coordinates.

2. **Effect — the whole line through its own cell, including its own cell.**
   **NEW DESIGN DECISION.** When it activates (§5.5.5), a Line Clear Gem
   clears **every cell of its own row or its own column** — all 8 cells of
   that line on the 8 × 8 board — **including the cell the Line Clear Gem
   itself occupies**. Exactly one of the two lines is cleared, never both:
   the Gem's orientation (§5.2 item 3) selects which.

   ```text
   Line Clear Gem at index i, row r = floor(i / 8), column c = i % 8

   horizontal orientation → { (r, 0), (r, 1), … (r, 7) }    8 cells
   vertical   orientation → { (0, c), (1, c), … (7, c) }    8 cells
   ```

   The affected set is computed against the §1.0 board, is clipped as §5.8
   requires, and is reduced by the reserved creation cells of §5.5.4 item 2.
   A full row and a full column are both exactly 8 cells on this board, so a
   Line Clear Gem never overflows the board and never needs wrapping or
   folding — clipping (§5.8) is reachable for this effect only in the sense
   that the set is *defined* on the board, not in the sense that cells are
   ever discarded. No radius, length, or per-side cap exists; the line is the
   whole line, which is the largest deterministic line the board has.

   **Wording note — "whole line", not "remainder of the line".** An earlier
   revision of this section described the effect as clearing "the remainder of
   its row or column". That wording is ambiguous: it can be read as a
   *directional* remainder ("the part of the line beyond the Gem, in one
   direction"), which would make the effect depend on a direction the Swap
   action does not carry (`§2.1.1 item 2`) and would leave half the line
   uncleared for no gameplay reason. This resolution fixes the reading
   explicitly: the effect is the **entire line**, in both directions from the
   Gem, **including the Gem's own cell**. There is no "remainder" concept in
   the MVP rule, no direction-dependent variant, and no partially-cleared
   line. A later implementation must clear all 8 cells of the line.

3. **Orientation follows the matched line.** **NEW DESIGN DECISION.** The
   orientation is fixed at **creation** time from the orientation of the
   straight line that created the Gem, and it never changes afterwards:

   ```text
   the creating line was a horizontal primitive  → horizontal Line Clear
   the creating line was a vertical primitive    → vertical Line Clear
   ```

   A horizontal Match 4 therefore yields a horizontal Line Clear Gem and a
   vertical Match 4 a vertical one. This is the only orientation source:
   orientation does **not** depend on the Swap direction, on which cell was
   the origin, on the Gem's own type, on gravity, on where the Gem later
   falls, or on the orientation of any later Match that consumes it. The
   orientation is a property of the **created Special Gem**, not of its cell
   — a Line Clear Gem that falls keeps its orientation (§4.4 item 8).

   A Line Clear Gem created by an L/T shape's qualifying arm (§5.4 item 4)
   takes the orientation of **that arm**.

4. **Configuration boundary.** The line's *presence*, its *inclusiveness of
   the Gem's own cell*, and its *orientation rule* are rules and are **not**
   configuration. The only configurable surface a future balance pass may
   touch is a **per-orientation length cap** (e.g. "clear at most N cells
   from the Gem outward"); the MVP value is "no cap — the whole line". No
   implementation may introduce a cap without a rule change
   (`GAME_RULES.md` §20).

5. **Resource generation for the consumed Gems** uses the Match-4 tier
   multiplier owned by `COMBAT_RULES.md` §2 — this document does not restate
   the value, because that document is the sole source of truth for
   match-tier resource output. Generation for the *activation* is owned by
   §5.7.

## 5.3 Match 5 — Burst Gem

1. **Creation, and creation position.** A Match 5 creates exactly one
   **Burst Gem**, at the **swap origin** (§5.5.3 item 1) when the shape was
   produced by the accepted Swap, and at the **line centre** (§5.5.3 item 3)
   when it was produced by a Cascade. As with §5.2 item 1, the position rule
   is owned by §5.5.3.

2. **Effect — the 3 × 3 square centred on its own cell, including its own
   cell.** **NEW DESIGN DECISION.** When it activates (§5.5.5), a Burst Gem
   clears every cell in the **3 × 3 square of which its own cell is the
   centre** — the eight orthogonally and diagonally adjacent cells plus the
   Burst Gem's own cell. The shape is a square, **not** a plus and **not** a
   diamond: it includes the four diagonal neighbours.

   ```text
   Burst Gem at index i, row r = floor(i / 8), column c = i % 8

   affected = all (r + dr, c + dc)
              for dr in { −1, 0, +1 }, dc in { −1, 0, +1 }
              clipped to 0 ≤ row ≤ 7 and 0 ≤ column ≤ 7   (§5.8)
   ```

   The affected set is computed against the §1.0 board, is clipped as §5.8
   requires, and is reduced by the reserved creation cells of §5.5.4 item 2.
   Because it is centred on a single cell of an 8 × 8 board, a Burst Gem
   clears **9 cells** in the interior, **6 cells** when it sits on an edge
   (non-corner), and **4 cells** at a corner. These counts are consequences of
   §5.8's clipping, not separate special cases, and no rule enumerates them
   at activation time.

   **Alternatives rejected — why 3 × 3 and not 5 × 5, a fixed radius, or a
   cross.** A 5 × 5 square clears 25 cells (39% of the board) and, from the
   interior, leaves the board's remaining Gems unable to avoid cascading into
   further clears; it also makes the Match-5 tier strictly dominate the L/T
   tier's Area Gem by a wide margin, which would flatten the tier ladder more
   than the documented multipliers (`COMBAT_RULES.md` §2: Match 5 = 2.0×,
   L/T = 1.25×) intend. A fixed radius discards the square's simplicity and
   gains nothing, because the radius is a constant on an 8 × 8 board. A cross
   is already the Area Gem's shape (§5.4 item 3), and reusing it would make
   two Special Gem types geometrically identical — the three MVP types must be
   distinguishable by effect, not only by creation tier (`GAME_RULES.md` §7:
   "a distinct Special Gem with an area-clear effect"). The 3 × 3 square is
   the smallest area that is meaningfully *area*-like, is legible to a player
   as "the cell and everything touching it", and is one comparison per
   affected cell to test — the lowest-complexity option that still reads as a
   burst.

3. **Match 6+ is Match 5 — there is no separate tier.** **NEW DESIGN
   DECISION (Option A).** A straight line of **6 or more** creates exactly
   one **Burst Gem** and is otherwise resolved exactly as a Match 5: same
   single Burst Gem, same §5.3 item 2 geometry, same tier multiplier as
   owned by `COMBAT_RULES.md` §2. No Match-6+, Match-7+, or "super" tier
   exists, no additional Special Gem type is introduced, and a long run never
   creates more than one Special Gem from being long.

   ```text
   straight line, length 3     → no Special Gem          (§5.1)
   straight line, length 4     → 1 Line Clear Gem        (§5.2)
   straight line, length 5     → 1 Burst Gem             (§5.3)
   straight line, length 6     → 1 Burst Gem             (§5.3 item 3)
   straight line, length 7     → 1 Burst Gem             (§5.3 item 3)
   straight line, length 8     → 1 Burst Gem             (§5.3 item 3)
   ```

   **Alternatives rejected.** *Option B (a separate Match 6+ behavior)*
   requires a fourth Special Gem type or a second Burst variant, which
   `MVP_SCOPE.md` §1 does not list and which `GAME_RULES.md` §7 does not
   authorize; it also adds a tier the player must learn for a case the 8 × 8
   board reaches only occasionally. *Option C (Match 6+ creates multiple
   effects)* multiplies the creation count and therefore the collision,
   reservation, and chain surface of §5.5.4/§5.5.5 for no player-facing
   clarity gain, and it conflicts with §5.5.2's per-shape ladder, which
   assigns one Special Gem per qualifying shape. *Option D* is not needed: no
   surviving artifact and no design goal demands a distinct long-run rule, and
   the tier ladder in `COMBAT_RULES.md` §2 already stops at Match 5. Option A
   is also the reading §3 item 2 and §5.3 item 3 already assumed, so it
   introduces no change to §3's Match definition — a run of 6+ remains **one**
   Match shape (§3 item 2) and is counted as exactly one Match (§3 item 5),
   incrementing Combo once (§6.2 item 3).

4. **Configuration boundary.** The 3 × 3 extent and the inclusion of the
   diagonals are rules and are **not** configuration in MVP. The configurable
   surface reserved for a future balance pass is the **side length** of the
   square (odd values only, so that "centred on its own cell" stays defined);
   the MVP value is `3`. Changing it is a rule change (`GAME_RULES.md` §20).

5. **Resource generation for the consumed Gems** uses the Match-5 tier
   multiplier owned by `COMBAT_RULES.md` §2 (see §5.2 item 5). A run of 6+
   uses the same Match-5 multiplier (`COMBAT_RULES.md` §2 owns the value);
   this document defines no sixth multiplier.

## 5.4 L / T Matches — Area Gem

1. **Shape definition — what counts as an L/T.** An L or T shape is **two
   intersecting maximal primitives, one horizontal and one vertical, that
   share exactly one cell and are each of length ≥ 3** — as §3 item 3 defines
   it. This item adds no second definition; it fixes the detection properties
   the tier needs:

   ```text
   minimum dimensions   both arms ≥ 3       (the §3 item 2 minimum)
   required cells       exactly the arms' union; the shared cell counted once
   orientation          one arm horizontal, one arm vertical — always
   rotation / mirror    every rotation and mirror is the same pattern:
                        there is no distinguished "L" versus "T" shape beyond
                        the arm lengths, and no orientation-dependent rule
   ```

   1. **No maximum arm length.** An arm may be 3, 4, 5, … ; a long arm is
      still one arm of the same pattern (§5.5.2 item 1).
   2. **A straight line is never an L/T.** The two primitives must be
      perpendicular, so two collinear runs are one straight primitive under
      §3's maximal-run rule, not an L/T (§3.3 item 1).
   3. **`T` and `L` are not distinguished.** Whether the intersection is at an
      arm's end or in its interior changes the shape's *cell count* but not
      its tier: both are `L/T` and both create one Area Gem. No separate `T`
      tier and no separate `L` tier exists.
   4. **Rotations and mirrors are equivalent.** Because the pattern is defined
      by "one horizontal + one vertical primitive sharing exactly one cell",
      all four rotations and all four mirrors satisfy it identically. No rule
      may depend on which rotation was matched.

2. **Creation position — the intersection cell.** An L/T shape creates exactly
   one **Area Gem**, at the **single cell shared by its two arms** (§5.5.3
   item 4). The intersection is not displaced by the swap origin: for an L/T
   shape the intersection is what identifies the shape, and it is the position
   the Area Gem's effect is centred on.

3. **Effect — a plus/cross, including its own cell.** **NEW DESIGN
   DECISION.** When it activates (§5.5.5), an Area Gem clears its own cell
   plus the **four orthogonally adjacent cells** — the plus/cross of its
   position:

   ```text
   Area Gem at index i, row r = floor(i / 8), column c = i % 8

   affected = { (r, c) } ∪ { (r−1, c), (r+1, c), (r, c−1), (r, c+1) }
              clipped to 0 ≤ row ≤ 7 and 0 ≤ column ≤ 7   (§5.8)
   ```

   The affected set is computed against the §1.0 board, is clipped as §5.8
   requires, and is reduced by the reserved creation cells of §5.5.4 item 2.
   An Area Gem therefore clears **5 cells** in the interior, **4 cells** on an
   edge (non-corner), and **3 cells** at a corner — consequences of §5.8's
   clipping, not special cases.

   **Why a plus, and not a different shape.** The plus is chosen because it is
   the **orthogonal complement** of the Burst Gem's 3 × 3 square: the Burst
   Gem takes a shape's diagonals, the Area Gem takes its orthogonals, so the
   two are distinguishable at a glance, neither dominates the other in area
   (9 versus 5 interior cells, against the tier ladder's own ordering
   Match 5 > L/T in `COMBAT_RULES.md` §2), and a player can predict both from
   the cell alone. It is also the lowest-complexity non-trivial area: five
   cells, no diagonals to reason about, and the same four-neighbour test §2's
   adjacency check already uses. **Alternatives rejected:** a 5 × 5 square
   duplicates the Burst Gem's shape and would make the L/T tier the largest
   clear in the game, contradicting its `1.25×` multiplier position; a
   same-type-only clear (clearing only Gems matching the creating Gem's type)
   is not an area-clear effect as `GAME_RULES.md` §7 requires, and would make
   the Gem's value depend on a board distribution the player cannot read;
   a diagonal-only (×) shape would be a strict subset of the Burst Gem's
   square and therefore redundant.

   **This section explicitly does not assume a cross by default any longer:**
   the cross/plus is the *selected* geometry, and this item is its owning
   rule. A future change to it is a rule change (`GAME_RULES.md` §20), and
   the shape is not a configuration value in MVP (§5.4 item 5).

4. **Arms that also qualify as Match 4 or Match 5 — both Special Gems are
   created.** **NEW DESIGN DECISION.** The tier ladder is evaluated **per
   arm** as well as for the whole pattern:

   ```text
   the L/T pattern itself                  → 1 Area Gem       (always)
   plus, for each arm independently:
       arm length 4                        → 1 Line Clear Gem
       arm length ≥ 5                      → 1 Burst Gem
       arm length 3                        → nothing
   ```

   1. **Precedence within an arm.** An arm is classified **once**, at its
      **highest** qualifying tier: an arm of length ≥ 5 yields a Burst Gem
      (not also a Line Clear Gem), and an arm of length 4 yields a Line Clear
      Gem. An arm never yields two Special Gems from itself.
   2. **Both arms can qualify.** If both arms are length ≥ 4, the shape
      produces **three** Special Gems: one Area Gem at the intersection plus
      one from each arm. There is no rule that suppresses an arm's gem
      because the other arm also qualified.
   3. **The L/T pattern is never suppressed.** An L/T always creates its Area
      Gem. An arm qualifying at a higher tier does **not** replace the Area
      Gem; the higher-tier rule takes precedence **for the arm**, and both
      Special Gems are created. This is the precedence §5.5.2 item 2 states.
   4. **The L/T pattern counts as one Match.** However many Special Gems it
      creates, it is a single Match shape (§3 item 5, §3.3 item 2), counted
      once, incrementing Combo once (§6.2 item 3) and generating resources
      once per consumed cell (§5.7 item 2). The Special Gems created are a
      consequence of the Match, never additional Matches.
   5. **Multiple creations in one cell are resolved by §5.5.4 item 3.** The
      Area Gem's intersection cell and an arm's gem position can coincide
      (for example a `T` whose long arm's centre is the junction); when they
      do, the winner is the earlier source in the §5.5.1 item 4 intra-shape
      order — the **Area Gem** first, then the horizontal arm's gem, then the
      vertical arm's gem — and every later claim on that cell is discarded. The
      shape still counts as the Match it was, and its Gems are still removed
      and still generate resources.

   6. **An arm's gem position is the arm's own line centre (§5.5.3 item 7).**
      It is **not** the intersection and **not** a swap origin. Both of those
      alternatives are excluded: the intersection is the Area Gem's own
      position (item 2), and no L/T creation has a swap origin (§5.5.3 item 7
      item 6). The arm's own primitive supplies the start `s` and the actual
      length `L` used by §5.5.3 item 7's formula, so an arm's gem is placed
      independently of the other arm and of the pattern.

5. **Configuration boundary.** The plus shape and the "both gems are created"
   rule are rules, not configuration. The configurable surface reserved for a
   future balance pass is the plus's **arm length** (how far from the centre
   each of the four directions reaches, `1` = the MVP plus); the MVP value is
   `1`. Allowing or suppressing the arm's own Special Gem is likewise a rule
   change, not configuration.

6. **Resource generation for the consumed Gems** uses the L/T tier multiplier
   owned by `COMBAT_RULES.md` §2 (see §5.2 item 5). The tier multiplier is
   applied **once per consumed cell over the union of the shape's cells**
   (§5.7 item 2) — an arm's higher classification does not apply a second,
   higher multiplier to the cells it shares with the L/T pattern, because the
   shape is one Match at one tier.

---

## 5.5 Special Gem Activation

1. A Special Gem's clear effect triggers when it is consumed as part of any
   Match (its own color matched normally) or when an explicit effect
   (Card/Relic/Boss) detonates it directly.
2. Gems cleared by a Special Gem's effect are NOT counted as a "Match" for
   Combo/Match-count purposes – they are an explosion, not a match – but they DO
   count toward resource generation and DO trigger Cascade re-evaluation
   (gravity + spawn + re-detect), per §4.
3. Special Gem chain reactions (a cleared Special Gem detonating another Special
   Gem) are allowed and resolve sequentially within the same Cascade step.

### 5.5.1 Creation Order Within a Detection Pass

1. Special Gems required by a pass's shapes are created **after** that pass's
   match set is known and its matched cells are removed, and **before** Gravity
   runs (§4.1 step 2).
2. When a pass requires more than one Special Gem, they are created in the
   fixed order of the pass's match set (§3.2) – horizontal shapes first, then
   vertical shapes, each by ascending start-cell index; an L/T shape has the
   position of its intersection cell (§5.5.3 item 4).
3. The order in item 2 is the order of creation and therefore of any
   deterministic reporting of created Special Gems. Creation cells that
   collide are resolved by §5.5.4 item 3, so the order decides which claim
   wins rather than changing the board in any other way.
4. **Within one L/T shape – the intra-shape creation order.** **NEW DESIGN
   DECISION.** Item 2 orders **shapes**, and an L/T is a **single** match-set
   entry (§3.3 item 2, §3.2 item 2), so item 2 alone cannot order the up-to-three
   creations one L/T produces (§5.4 item 4 item 2). Those creations are ordered
   by the following fixed sequence, which is the intra-shape continuation of the
   same ordering principle item 2 already uses:

   ```text
   1. the Area Gem                      (the L/T pattern's own creation)
   2. the gem of the HORIZONTAL arm     (horizontal before vertical, §3.2 item 1)
   3. the gem of the VERTICAL arm
   ```

   1. **It is an ordering of sources, not of types.** The sequence orders the
      three **creation sources** of one L/T — the pattern, then its horizontal
      arm, then its vertical arm. It is **not** a ranking of Special Gem types.
      §5.8.4 item 4 remains in force: no Special Gem type outranks another, and
      a global ranking such as `Area > Line > Burst` does **not** exist and may
      not be introduced. The order this item defines happens to yield
      Area Gem → then an arm gem, but that is a consequence of the pattern
      preceding its arms, not of the types involved.
   2. **Why the pattern precedes its arms.** The Area Gem is the L/T pattern's
      own creation (§5.4 item 2) — the creation that *identifies* the shape —
      while an arm gem is a consequence of an arm independently qualifying
      (§5.4 item 4 item 2). Ordering the identifying creation first keeps the
      L/T's own tier intact when two of its creations claim one cell; §5.4
      item 4 item 3 states that the L/T pattern "is never suppressed".
   3. **Why the horizontal arm precedes the vertical arm.** §3.2 item 1 already
      fixes horizontal before vertical as the pass's primary ordering key. This
      item applies that same documented key one level down, inside the shape,
      instead of inventing a second principle.
   4. **All five intra-L/T cases are covered, with no open tie.**

      ```text
      Case 1  Area only (both arms length 3)
              1 creation, in position 1. No collision possible.
      Case 2  Area + horizontal arm gem (H arm >= 4, V arm = 3)
              order: Area, then H arm gem. Collision -> the Area Gem wins.
      Case 3  Area + vertical arm gem (V arm >= 4, H arm = 3)
              order: Area, then V arm gem. Collision -> the Area Gem wins.
      Case 4  Area + both arm gems (both arms >= 4)
              order: Area, H arm gem, V arm gem.
              Area vs H  -> Area wins.
              Area vs V  -> Area wins.
              H vs V     -> the horizontal arm's gem wins.
      Case 5  any collision between two or more of the three
              resolved by the order above: the earliest source in the
              sequence keeps the cell, and every later claim on that same
              cell is discarded per §5.5.4 item 3.
      ```

   5. **A collision inside one L/T is reachable, and is resolved by this
      order.** The task's canonical case (also recorded in §5.8.5):

      ```text
      horizontal arm: row 2, columns 0..4  (length 5, so >= 5 → Burst Gem)
      vertical arm:   column 2, rows 2..4  (length 3, so no arm gem)
      intersection:   (2,2), index 18

      Area Gem    → (2,2)   [§5.4 item 2]
      H arm gem   → arm start s = 16, L = 5 → index 16 + 2 = 18 = (2,2)
                             [§5.5.3 item 7]
      ⇒ both claim (2,2); order positions the Area Gem first
      ⇒ the AREA GEM is created at (2,2) and the H arm's Burst Gem is
        discarded (§5.5.4 item 3). The L/T is still one Match and still
        generates resources once per consumed cell.
      ```

   6. **The order is total and deterministic.** Every intra-L/T creation is
      placed in exactly one of the three positions above, and no two positions
      are equal (the pattern is not an arm, and the horizontal arm is not the
      vertical arm), so the sequence never leaves a tie open. It depends only on
      the shape's own structure and the §1.0 indexing — never on a dictionary,
      hash, allocation, insertion, or enumeration order (§5.8.3, §7.2 item 4).
   7. **The order also governs reporting.** As item 3 states for item 2's order,
      this sequence is the order in which the L/T's creations are created and
      therefore the order in which created Special Gems are reported
      (`GAME_EVENTS.md` §2).
   8. **Discarded claims have no further effect.** A discarded claim is not
      created, not stored, not activated, and not reported as created
      (§5.5.4 item 3).

### 5.5.2 Which Match Shapes Create Which Special Gem

```text
two intersecting lines, sharing exactly one cell,
    each of length >= 3                             → Area Gem   (§5.4)
otherwise, a straight line of exactly 5+            → Burst Gem  (§5.3)
otherwise, a straight line of exactly 4             → Line Clear Gem (§5.2)
otherwise, a straight line of exactly 3             → no Special Gem (§5.1)
```

1. The ladder is evaluated **per line** for a straight shape and **per
   intersection** for an L/T shape, exactly as §5.1—§5.4 define; this table
   restates their precedence, it does not add a tier.
2. Precedence when a shape qualifies at more than one tier is owned by §5.4
   item 4: the higher-tier rule takes precedence for the arm, and the L/T
   pattern still produces its one Area Gem, so both Special Gems are created
   and placed independently — the Area Gem at the intersection (§5.5.3 item 4)
   and the arm's gem at that arm's own line centre (§5.5.3 item 7) — subject
   to the collision rule of §5.5.4 item 3 and the intra-L/T order of §5.5.1
   item 4.
3. A straight line of 6 or more creates a **Burst Gem**, not a new tier –
   there is no Match-6+ tier in MVP (§5.3 item 3).
4. No Match-3 shape creates a Special Gem (§5.1). A pass made entirely of
   Match-3 shapes creates none, and the pass still resolves normally.

### 5.5.3 Creation Location

1. **Match 4 / Match 5 – swap origin.** The Special Gem is created at the
   position of the **swapped Gem** (§5.2 item 1, §5.3 item 1): the cell that was
   one of the two cells named by the accepted Swap and whose Gem is a member of
   the matched line. "Swap origin" means that cell – the §1.0 index the
   player's action moved a Gem into – not a direction, and not the cell the Gem
   came from.
2. **Identifying the swap origin.** Of the two cells named by the accepted Swap
   (§2.1.1 item 1), the origin is the one whose Gem is a member of the matched
   line. Exactly one of the two cells is the origin when the shape was created
   by the Swap. If **neither** cell is a member of the matched line, the line is
   a Match that existed independently of this Swap – a Cascade match – and
   item 3 applies. If **both** cells are members of the matched line, the shape
   is an L/T pattern and item 4 applies (the intersection is the location, and
   no origin is needed). A case that fits none of these three is a
   documentation/implementation conflict and must be reported, not resolved by
   inventing a rule.
3. **Match 4 / Match 5 in a Cascade – line centre.** For a Match with no swap
   origin (a Cascade match), the Special Gem is created at the **geometric
   centre** of the matched line: the middle cell of the line when its length is
   odd, and the **lower-indexed of the two middle cells** when its length is
   even – that is, for a line of length `L` starting at index `s` and stepping
   by `1` (horizontal) or `8` (vertical), the cell at `s + floor((L − 1) / 2)`.
   The tie-break is fixed here because "geometric centre" alone is ambiguous
   for an even-length line and the choice must be identical in every
   implementation.
4. **L/T – the intersection cell.** The Area Gem is created at the single cell
   shared by the two lines (§5.4 item 1). The intersection is not displaced by
   the swap origin: for an L/T shape the intersection is what identifies the
   shape.
5. **The location is a cell, always.** Every Special Gem is created in a cell
   that was part of the shape that created it (for Match 4/5 the origin cell is
   a member of the matched line; for L/T it is the shared cell). No Special Gem
   is created outside the shape, at an arbitrary empty cell, or at a newly
   spawned cell.
6. **Creation does not depend on Swap direction.** The player's action carries
   an unordered pair of cells and no direction (§2.1.1 item 2), so no rule can
   depend on a direction. Two Swaps of the same pair produce the same origin
   identification and therefore the same creation location.
7. **L/T arm creations – the arm's own line centre.** **NEW DESIGN DECISION.**
   A Special Gem created by an L/T shape's **arm** (§5.4 item 4) is placed at
   the **line centre of that arm alone**, computed by the §5.5.3 item 3 rule
   applied to the arm's own primitive:

   ```text
   arm centre index = s + floor((L − 1) / 2) × step
       s     = the arm's own first cell (§3.2 item 1) — its lowest §1.0 index
       L     = the arm's ACTUAL length, i.e. its number of cells
       step  = 1 for a horizontal arm, 8 for a vertical arm
   ```

   1. **Primitive scope — the arm's own primitive only.** The centre is
      computed from the arm's cells, **never** from the whole L/T shape, never
      from the other arm, and never from the union of the arms. A horizontal
      arm and a vertical arm therefore have different starts and different
      lengths, and each is centred independently.
   2. **Vertical arms step by 8, not by 1.** §5.5.3 item 3 states the step
      ("stepping by `1` (horizontal) or `8` (vertical)"); the multiplication by
      `step` is what that sentence means, and it is stated explicitly here so
      the formula cannot be read as a horizontal-only rule.
   3. **Even-length arms — the lower-indexed of the two middle cells.** A
      length-4 arm has two central cells; the selected one is the
      **lower-indexed** one, exactly as §5.5.3 item 3 fixes for an even-length
      line. `floor((4 − 1) / 2) = 1`, so the selected cell is **one cell along**
      from the arm's start, not two.
   4. **Odd-length arms — the unique centre.** A length-5 arm selects the cell
      **two** along (`floor(4 / 2) = 2`); a length-7 arm selects the cell
      **three** along (`floor(6 / 2) = 3`). There is no tie for an odd length.
   5. **The actual arm length is used — never a truncated Match-5 length.**
      §5.3 item 3 treats a run of 6+ as a Match 5 **for tier classification
      only**. It does **not** shorten the arm. A length-6 arm centres on the
      cell **two** along (`floor(5 / 2) = 2`); a length-7 arm on the cell
      **three** along. Placement always uses the arm's real cell count `L`.
   6. **The swap-origin rule does not apply to any L/T creation.** For an L/T
      shape, neither the Area Gem nor an arm's gem is placed at a swap origin.
      §5.5.3 item 1 scopes the origin rule to a straight Match 4 / Match 5, and
      §5.5.3 item 2 states that for an L/T "no origin is needed". This item
      resolves the cross-reference in §5.5.4 item 3 accordingly: the phrase
      "the arm's swap origin or line centre" there means **the arm's line
      centre**, because the origin alternative is not reachable for an L/T.
      An L/T arm gem therefore has exactly **one** possible position, not two.
   7. **The arm's own cells are the §5.5.3 item 5 guarantee here.** The centre
      computed by this item is a cell of the arm (§1.0 indexing), so it is
      always a cell of the shape that created the gem, as item 5 requires.

   **The four required arm cases, resolved.** Using the §1.0 indexing, for an
   arm starting at its own lowest index and `step` as above:

   ```text
   horizontal arm, length 4   → the cell 1 along   (lower-indexed middle)
   horizontal arm, length 5+  → the cell floor((L−1)/2) along
   vertical   arm, length 4   → the cell 1 row down (lower-indexed middle)
   vertical   arm, length 5+  → the cell floor((L−1)/2) rows down
   ```

   **Why the arm's own line centre, and not the intersection or an origin.**
   The intersection is already the Area Gem's position (§5.4 item 2), and
   assigning it to arm gems too would make every arm gem collide with the Area
   Gem and be discarded by §5.5.4 item 3 — which would make §5.4 item 4 item 2's
   "three Special Gems" unreachable, contradicting an explicit requirement.
   A swap origin is not identifiable for an L/T (§5.5.3 item 2: both swapped
   cells lie on the shape, one per arm, and the rule declines to pick), and the
   §2.1.1 item 2 no-direction invariant forbids deriving one from the action.
   The line centre is the only rule that (a) is already named for arm gems by
   §5.5.4 item 3, (b) reuses the already-fixed §5.5.3 item 3 formula and its
   even-length tie-break rather than inventing a new one, and (c) is a pure
   function of the arm's own cells, so it needs no direction, no RNG, and no
   tie-break beyond the documented one.

### 5.5.4 Replacement, Reservation and Collision

1. **Creation replaces the removal, not the Gem.** The cell is emptied by §4.1
   step 1 like every other matched cell and then receives the Special Gem in
   §4.1 step 2. The Special Gem is a **new** board object at that cell; it is
   never "the same Gem, upgraded", and no Gem survives Match Resolution.
   Result: the cell holds exactly one Gem before Gravity, as §1 requires at
   rest.
2. **Creation cells are reserved before any activation runs, and an activation
   never clears a reserved cell.** Step 2 of §4.1 computes the pass's required
   Special Gem positions *first*; those cells are **reserved**. Every affected
   set of that step's activations is then reduced by the reserved cells, unless
   the reserved cell is the activated Special Gem's own cell. The order is
   fixed:

```text
step 2 of §4.1
   1. compute the pass's required Special Gem positions   → reserved cells
   2. gather the consumed Special Gems                    (§5.5.5 item 1)
   3. resolve their effects over (affected set − reserved cells), including
      chains                                             (§5.5.5 item 7)
   4. create the Special Gems in the reserved cells       (§5.5.1 order)
   5. the board is stable for Gravity                     (§4.1 step 3)
```

   The reservation is what makes activation and creation compatible in one
   step: an activation always clears everything in its affected set that is not
   a reserved creation cell, and a Special Gem the pass creates always survives
   step 2 and reaches Gravity.
3. **Two creations in one cell – fixed tie-break.** §5.4 item 4 can require more
   than one Special Gem for one L/T shape (an arm qualifying as Match 4/5 *and*
   the L/T pattern's Area Gem). §5.5.3 item 4 places the Area Gem at the
   intersection, and §5.5.3 item 7 places an arm's gem at **that arm's own line
   centre** — the arm's swap origin is **not** a reachable position for any L/T
   creation, because §5.5.3 item 1 scopes the origin rule to a straight
   Match 4 / Match 5 and §5.5.3 item 2 states that an L/T needs no origin. Those
   two locations can be the same cell – for example a T whose long arm's centre
   is the junction (§5.5.1 item 4 item 5). When one pass gives two or more
   Special Gems the same cell, **only the first in the order of §5.5.1 is
   created** – that is, first by shape in the §5.5.1 item 2 order, and within
   one L/T shape by the §5.5.1 item 4 sequence (Area Gem, then the horizontal
   arm's gem, then the vertical arm's gem). The later claim on that cell is
   discarded, and the shape that produced it still counts as the Match it was
   (its Gems are still removed and still generate resources). The cell holds one
   Special Gem, never two, so §1's one-Gem-per-cell invariant holds at every
   point. A discarded Special Gem claim has **no** further effect: it is not
   created, not stored, not activated, and not reported as created by §5.5.1
   item 3.
4. **The created Special Gem carries the Gem type of the Gem it replaced.**
   It is not one of the four §1.1 types as a *Special Gem type* — there is no
   fifth Gem type and creating one is not a Match-4/5/L-T tier — and it is
   never a matchable colour of its own: §5.5.5 item 1 consumes it only through
   an explicit detonation or by being named in an effect's affected set, and
   §5.5.5 item 2 states how a Special Gem enters a match set. The **cell** it
   occupies still holds one of the four §1.1 Gem types, which is the type the
   Gem removed from that cell in this pass had (item 1). It occupies one cell,
   it participates in Gravity and Spawn like any other Gem (§4.4 item 8), and
   it is consumed only as §5.5.5 defines.
   **Its board representation is owned by `GAME_STATE.md` §2.1**, which holds
   it inside `BoardState.Cells[64]`: each cell entry carries its Gem type plus
   an optional Special Gem (type, and orientation for a Line Clear Gem). The
   `PendingSpecialGems[]` **field-shape gap** this item previously recorded is
   **closed** by that section — the collection is not part of `BoardState` at
   all, so there is no second board representation and nothing left to decide
   before a board carrying Special Gems can be stored or delivered. The
   Special Gem *rules* above are unchanged by that resolution and remain owned
   here.
5. **Resources for the removed Gems are still generated.** The Gems forming a
   Match 4 / Match 5 / L-T shape are consumed and generated against normally –
   at the tier multiplier of the shape they formed (COMBAT_RULES.md §2). The
   created Special Gem adds no generation of its own; generation happens when
   it is later consumed (§5.5.5 item 8).

### 5.5.5 Activation Rules

1. **Trigger.** A Special Gem's clear effect activates when the Special Gem is
   **consumed**:
   * it is part of a detection pass's match set – that is, its own colour is
     matched normally, exactly like any other Gem (§5.5 item 1); or
   * an explicit effect (Card / Relic / Boss) detonates it directly
     (§5.5 item 1). The detonation effects themselves are owned by those
     systems; this section owns only what a detonation does to the board.
   A Special Gem that is merely present on the board, that falls, or that is
   created does **not** activate: creation, gravity, and spawn never trigger it
   (§4.4 item 8).
2. **Consumption is a match-set member.** A Special Gem consumed as part of the
   match set is removed with that set (§4.1 step 1) and its effect resolves in
   §4.1 step 2, before Gravity. An explicit detonation that happens outside a
   Swap's resolution (a Card or Boss effect) resolves immediately when that
   effect applies, through the same procedure.
3. **Activation and creation both resolve in step 2, in this fixed order.**
   §5.5.4 item 2 owns the reservation that keeps them from colliding; the order
   itself is the five-point sequence shown there. Activation is resolved first
   so that each effect's affected set is computed against the board the pass
   actually produced.
4. **Affected cells.** Each effect clears a fixed set of cells around its own
   position, and the three MVP effects are exactly these — each geometry is
   **owned by the tier section named**, and is restated here only as a
   pointer, never as a competing definition:

   ```text
   Line Clear Gem → the whole row or column through its own cell,
                    including its own cell; orientation fixed at creation
                    from the creating line's orientation      §5.2 items 2, 3
   Burst Gem      → the 3×3 square centred on its own cell,
                    including its own cell and the diagonals   §5.3 item 2
   Area Gem       → the plus/cross of its own cell: itself plus the
                    four orthogonally adjacent cells            §5.4 item 3
   ```

   All three include the activated Special Gem's own cell, so consuming a
   Special Gem always clears the cell it occupied and never leaves it behind.
   The affected set is computed against the §1.0 board and never leaves the
   board: cells outside `0..63` do not exist and are not wrapped, folded, or
   clamped to another row or column — §5.8 owns that clipping. The set is then
   reduced by the reserved cells of §5.5.4 item 2.
5. **Simultaneous or sequential.** A Special Gem effect clears its whole
   affected set as **one step** – the set is computed before any of it is
   removed, so the effect never expands or shrinks because part of its own area
   was already removed. Two Special Gems activated by the **same** match set
   are resolved **sequentially**, in the match-set order of §3.2, each with its
   own complete affected set (§5.5 item 3: "resolve sequentially within the
   same Cascade step").
6. **Duplicate removal.** A cell is removed **once**, regardless of how many
   effects name it. Removal, resource generation per cleared Gem, and the
   `GemMatched` report are computed over the **union** of the cells the effects
   effectively clear, after all effects of the step are known – never per
   effect.
7. **Chain reactions.** A Special Gem inside another Special Gem's affected set
   is consumed by it and activates in turn: chains are allowed
   (§5.5 item 3, which requires them to "resolve sequentially within the same
   Cascade step"). A chain is resolved **breadth-first in activation order**:
   all effects discovered at one level are resolved before any effect
   discovered by them. This mirrors the breadth-first queue `RELIC_RULES.md` §4
   item 3 defines for Relic-triggered chains, applied here to Special Gem
   chains so the two chain mechanisms order identically.

   **Termination.** A Special Gem that has already activated in this step does
   not activate again in it, so a chain is finite: each activation either
   consumes a Special Gem that has not yet activated (progress) or changes
   nothing. A Special Gem is never consumed twice in one step (§5.5.5 item 6's
   once-per-cell rule applied to the Special Gem's own cell). This is the
   board-level expression of the anti-infinite-chain requirement
   `GAME_RULES.md` §13 item 4 states for chains and `RELIC_RULES.md` §5 item 1
   implements for Relics; it is the same requirement, not a second one. A
   board that still could not terminate would be a defect in the pass's
   affected-set computation and must be reported, not capped by an invented
   limit.
8. **Counting.** Gems cleared by a Special Gem effect are **not** a Match: they
   are not added to the pass's match set, they do not create a Match, they do
   not advance Combo, and they do not increase the Match count (§5.5 item 2,
   `PASSIVE_RULES.md` §2.2). They **do** generate resources per cleared Gem and
   **do** feed the next Gravity + Spawn + re-detect pass (§4.1, §5.5 item 2).
   The generation rate per cleared Gem is owned by `COMBAT_RULES.md` §2 – its
   table is the sole source of truth for match-tier resource output – and §5.7
   owns only *when* generation is triggered.
9. **Interaction with other Special Gems.** One Special Gem may consume
   another (item 7); the consumed one activates its own effect in its own turn.
   No MVP effect is amplified, disabled, or transformed by another Special
   Gem's presence – the three types do not combine into a fourth effect, and no
   such combination is defined.
10. **Effect activation consumes no RNG.** A Special Gem effect is a
    deterministic function of the board and the activated Gem's type and
    position (§7.2). It also creates no further Special Gem: creation is a
    property of match shapes (§5.5.2), not of explosions.

## 5.6 Deterministic Ordering Summary

This is the **index** of the ordering rules. §5.8.3 is the owning section and
states the same orders in more detail, including the levels inside a chain; the
table below is a pointer, not a second contract.

```text
pass        cascade depth order                       §4.2
match set   orientation, then start-cell index        §3.2
reservation required creation cells, computed first   §5.5.4 item 2
activation  match-set order, then BFS across chains   §5.5.5 items 5, 7
                                                      §5.8.3
creation    match-set order, after activation,        §5.5.1 items 1–3
            before Gravity; collisions resolved by
            §5.5.4 item 3
creation    within one L/T: Area Gem, then the        §5.5.1 item 4
(one L/T)   HORIZONTAL arm's gem, then the
            VERTICAL arm's gem; collisions resolved
            by §5.5.4 item 3
clipping    affected sets reduced to existing cells   §5.8.1
overlap     union; cleared, generated and activated   §5.8.2
            once per cell
gravity     column 0 → 7, cells bottom → top          §4.4 item 1
spawn       column 0 → 7, cells top → bottom          §4.5 item 2
```

Every order above is fixed by this document; none of them may be left to an
unordered collection or to an implementation's convenience (`§7.2 item 4`).

**Not in this table — `GemMatched` enumeration.** The table above orders the
resolution: which shapes and effects contribute cells, and in what sequence.
The order of the individual `GemMatched` events inside one resolved cleared
union is the **event contract**, not a board rule, and is owned by
`GAME_EVENTS.md` §1.3: once per cell of the union, in ascending §1.0 cell
index, within each of §4.1's sub-steps. That order is an event-enumeration
order only — it never changes which cells are cleared, which Special Gem a
collision creates, or anything else in this table (§5.8.3 item 5).

## 5.7 Resource Generation Hooks

Exact numeric formulas for Power/HP/ATK/DEF resource output per Gem type and per
match tier are defined in `COMBAT_RULES.md` §2 and `GAME_RULES.md` §12 (Power
Rules). This document defines only *when* generation is triggered:

```text
Match 3   → base resource generation            (COMBAT_RULES.md §2)
Match 4   → increased resource generation + Line Clear Gem
Match 5   → higher resource generation + Burst Gem
L/T       → its own tier multiplier + Area Gem  (COMBAT_RULES.md §2)
Special Gem detonation → resource generation per Gem cleared (base rate)
```

1. The multipliers themselves are owned by `COMBAT_RULES.md` §2, which states it
   is the sole source of truth for match-tier resource output; this section does
   not restate them and does not define a competing "base rate".
2. Generation is triggered by **consumed Gems**, once per cell, over the union
   of the cells a step removes (§3.3 item 3, §5.5.5 item 6) – a cell removed by
   more than one shape or effect generates once.
3. A Special Gem that is **created** generates nothing by itself (§5.5.4
   item 5); it generates when it is later consumed.
4. Generation is not a Match and is not counted as one (`GAME_RULES.md` §3,
   `PASSIVE_RULES.md` §2.2).
5. Generation is a **downstream consumer** of board resolution: it reads the
   pass's cleared cells and the per-Gem tier, and it is owned – like Power,
   Passive charge, and Relics – by its own domain document
   (`COMBAT_RULES.md`, `GAME_RULES.md` §17 steps 10—13). This section owns only
   *when* it is triggered by board events; it defines no rate, multiplier, or
   formula.
6. **Which tier multiplier applies to a Special Gem's cleared cells.**
   **NEW DESIGN DECISION.** The cells a Special Gem *activation* clears are not
   a Match (§5.5.5 item 8) and therefore have **no match tier**: they generate
   at the **base per-Gem rate** owned by `COMBAT_RULES.md` §2 — the Match-3
   tier — and receive **no** Match-4, Match-5, or L/T multiplier. The tier
   multiplier belongs to the *shape that created the Special Gem*, and it has
   already been applied when that shape's own Gems were consumed (§5.5.4
   item 5). A Special Gem activation therefore never applies a tier multiplier,
   never applies a second multiplier to a cell, and never multiplies by the
   tier of the Gem that created it. `COMBAT_RULES.md` §2 owns the numeric rate;
   this item owns only that the rate is the base one.
7. **Cascade-generated Matches are counted and generated independently.** Every
   Match detected in a Cascade pass generates resources at its own shape's tier
   under the same rules as the Swap's first pass (§4 item 4, §4.2 item 5). A
   Cascade is not a multiplier and does not aggregate passes: each pass's
   consumed cells generate once, at that pass's tiers, and the Swap's total is
   the sum. No "cascade bonus" multiplier exists in MVP.

## 5.8 Boundaries, Overlap and Determinism

### 5.8.1 Board Boundaries — Clipping

**NEW DESIGN DECISION.** Every effect's affected set is computed as a set of
`(row, column)` pairs, then **clipped to the cells that exist**:

```text
a cell (row, column) is in the affected set only if
    0 ≤ row ≤ 7   and   0 ≤ column ≤ 7
```

1. **Clipping is by discard, never by wrap.** A cell outside the board is
   **dropped**. It is never wrapped to the other side of the board, never
   folded onto another row or column, never clamped onto the nearest existing
   cell, and never counted as an affected cell. `index 7` and `index 8` are not
   neighbours (§2.1.3 item 1), so no effect may cross a row edge.
2. **Clipping is not an error and emits nothing.** An effect that reaches the
   edge is normal and produces no warning, no rejection, no event, and no
   substitution. It simply has fewer affected cells.
3. **No out-of-board cell exists.** There is no cell `-1`, no cell `64`, and no
   phantom cell outside `0..63` (§1.0). Nothing in the resolution may allocate,
   address, or report one.
4. **The effect still includes its own cell.** The activated Special Gem's own
   cell is always on the board, so clipping never removes it, and every
   activation clears the cell the Gem occupied (§5.5.5 item 4).
5. **The documented edge cases, stated explicitly.**

```text
Line Clear Gem at row 0          → clears row 0, all 8 cells        (§5.2 item 2)
Line Clear Gem at row 7          → clears row 7, all 8 cells
Line Clear Gem at column 0       → clears column 0, all 8 cells
Line Clear Gem at column 7       → clears column 7, all 8 cells
Burst Gem at a corner (0,0)      → 4 cells  { (0,0), (0,1), (1,0), (1,1) }
Burst Gem on an edge, non-corner → 6 cells
Burst Gem in the interior        → 9 cells
Area Gem at a corner (0,0)       → 3 cells  { (0,0), (0,1), (1,0) }
Area Gem on an edge, non-corner  → 4 cells
Area Gem in the interior         → 5 cells
```

A Line Clear Gem never needs clipping in the sense of discarding cells — a full
row and a full column are each exactly 8 cells on this board — but it is
subject to §5.8.1 exactly like the others, and its set is defined the same way.
The counts above are **consequences** of the rules, not a second table an
implementation must special-case; an implementation that hard-codes nine
"corner/edge/interior" branches instead of clipping one set is not
implementing this section.

### 5.8.2 Overlapping Effects — Union Semantics

**NEW DESIGN DECISION.** When two or more effects affect the same cell, the
cell is treated **once**, and the pass operates on the **union** of all
effective affected sets:

```text
effective set of one activation
    = (its clipped affected set) − (the reserved creation cells of
                                    §5.5.4 item 2, unless the reserved cell is
                                    the activated Gem's own cell)
```

1. **A cell is cleared once.** Removal is over the union of the effective sets
   of the whole step, never per effect (§5.5.5 item 6). An implementation that
   removes a cell when effect A names it and removes it *again* when effect B
   names it has implemented the wrong rule even if the final board looks the
   same.
2. **A cell generates resources once.** Resource generation, the per-Gem
   report, and the `GemMatched` event are computed over the same union, once
   per cell (§5.7 item 2, `GAME_EVENTS.md` §2). Overlapping effects therefore
   produce **no** double resources, **no** double `GemMatched`, and no
   stacking bonus of any kind. The union is a **set**: this section fixes
   *which* cells it holds and that accounting is by unique cell, never in what
   order its members are reported. The report order is the event contract of
   `GAME_EVENTS.md` §1.3 (ascending §1.0 cell index), which depends only on the
   §1.0 indexing and not on the order the effects were enumerated in
   (item 5).
3. **A Special Gem activates at most once per step.** If two effects both name
   the cell of the same Special Gem, that Special Gem is consumed once and its
   effect resolves **once** (§5.5.5 item 7's termination rule). Its own
   affected set is computed once, when it activates.
4. **No effect is amplified, reduced, or transformed by overlap.** Two effects
   overlapping do not combine into a larger effect, do not cancel, and do not
   produce a fourth effect (§5.5.5 item 9). Each resolves on its own geometry;
   only the *result* (the union of cleared cells) is shared.
5. **Resource accounting is by unique cleared cells.** The number of cleared
   cells a step reports for generation is the **cardinality of the union**, not
   the sum of the effects' sizes. This is the property that makes overlap
   deterministic and easy to test: the same board and the same set of
   activations always yield the same cleared-cell count, whatever order the
   effects are enumerated in.
6. **Chain activations do not re-clear already-cleared cells.** A cell cleared
   earlier in the same step is no longer on the board, so a later effect naming
   it has nothing to clear there and generates nothing for it.

### 5.8.3 Deterministic Resolution Ordering

**NEW DESIGN DECISION (the ordering contract).** Resolution order is fully
determined by the board state and the documented indices. It may never depend
on dictionary or hash iteration order, insertion order, allocation order,
database order, network arrival order, client animation order, timing, or any
other unordered or external source (§7.2 item 4, `AGENTS.md` §11).

```text
outer    cascade pass          depth order                     §4.2
    ├── 1. match set           orientation (horizontal first),
    │                          then ascending start-cell index   §3.2
    ├── 2. reserved cells      required creation positions,
    │                          computed before any activation     §5.5.4 item 2
    ├── 3. activation          the pass's consumed Special Gems, in the
    │                          match-set order of §3.2, each resolved as
    │                          one step over its effective set       §5.5.5 items 5, 6
    │      └── 3a. chains      breadth-first: all effects discovered at
    │                          one level before any effect discovered
    │                          by them; within a level, in the order the
    │                          consuming effects were resolved, and within
    │                          one effect by ascending §1.0 index of the
    │                          consumed Special Gems                  §5.5.5 item 7
    ├── 4. creation            the pass's required Special Gems, in the
    │                          match-set order of §3.2; within one L/T
    │                          shape by the §5.5.1 item 4 sequence (Area
    │                          Gem, then H arm's gem, then V arm's gem);
    │                          collisions resolved by first-in-order wins  §5.5.1
    ├── 5. gravity             column 0 → 7, cells bottom → top        §4.4 item 1
    └── 6. spawn               column 0 → 7, cells top → bottom        §4.5 item 2
```

1. **Every level above is a total order.** For any two effects, activations, or
   creations in the same pass, the order between them is decided by these
   rules, and there is no tie these rules leave open:
   * two effects from **different** match-set entries → match-set order (§3.2);
   * two effects from the **same** entry → that entry is one shape, so it
     appears once in the match set, and the case does not arise;
   * two Special Gems consumed by **one** effect's affected set → ascending
     §1.0 cell index of the consumed Special Gem's own cell;
   * two creations claiming **one** cell, from **different** shapes → first in
     §5.5.1 item 2 order wins (§5.5.4 item 3);
   * two creations claiming **one** cell, from **one L/T shape** → first in the
     §5.5.1 item 4 intra-shape sequence wins (Area Gem, then the horizontal
     arm's gem, then the vertical arm's gem — §5.5.4 item 3);
   * two creations from one L/T claiming **different** cells → both are
     created, in the §5.5.1 item 4 sequence order.
2. **Chains are breadth-first, not depth-first.** A Special Gem consumed by
   effect A does not resolve before effect B of the same level has resolved.
   This is the same convention `RELIC_RULES.md` §4 item 3 defines for
   Relic-triggered chains, applied here so both chain mechanisms in the game
   order identically.
3. **A step's effects see the board the pass produced, not each other's
   removals.** Each activation's affected set in level 4 (creation) is computed
   against the board the pass produced, reduced by the reserved cells — it is
   never computed against a board partially mutated by a sibling effect
   (§5.5.5 item 5). Overlap is then handled by §5.8.2's union at the *result*
   level.
4. **Chains are immediate within the step.** A chain activation resolves inside
   the same §4.1 step 2 as the effect that caused it — it does not wait for
   Gravity, and it does not become a new detection pass. It is therefore
   *not* a new Match, does not advance Combo, and does not increment the Match
   count (§5.5.5 item 8).
5. **Order affects reporting, not the cleared set.** Because removal, resource
   generation, and reporting are union-based (§5.8.2), the *set* of cleared
   cells does not depend on the order. The order is still mandatory, because
   the activation sequence, the creation-collision winner, the chain
   breadth-first levels, and the event stream (`GAME_EVENTS.md` §1) all depend
   on it, and replay/debugging must reproduce it exactly (§7.1).
6. **Newly created Special Gems do not activate in the pass that creates
   them.** A Special Gem created in step 4 of a pass is created *after* that
   pass's activations, is not part of the pass's match set, and is not
   re-examined until the next detection pass (§4.1 item 4). It can therefore
   never activate in its creating pass — not through a chain, and not through
   the pass's own effect geometry. "Newly spawned Special Gems can activate
   during the same resolution" is therefore answered precisely: **they can
   activate in a later detection pass of the same Swap (for example after
   gravity and spawn produce a match that consumes them), but never in the pass
   that created them.**
7. **Recursion is bounded by consumption, not by a limit.** A chain is finite
   because each activation consumes a Special Gem that had not yet activated in
   this step (§5.5.5 item 7). No depth limit, recursion cap, or iteration
   budget is introduced, and none may be added as a safeguard: a chain that
   could not terminate would be a defect in the affected-set computation and
   must be reported (`AGENTS.md` §7), not capped.

### 5.8.4 Multiple and Simultaneous Special Gem Creation

**NEW DESIGN DECISION.** The question "can one swap create multiple Special
Gems?" is answered **yes, and the count is fully determined**:

1. **One resolution can create more than one Special Gem.** The pass's shapes
   are evaluated one by one, in match-set order (§3.2), and each qualifying
   shape contributes its own creation(s) (§5.5.2). There is no global cap of
   one Special Gem per Swap, per pass, or per board.
2. **How many, per shape.** Exactly as §5.5.2 item 1 and §5.4 item 4 define: a
   straight line of 4 creates 1; a straight line of ≥ 5 creates 1; an L/T
   pattern creates 1 Area Gem plus **one per arm that independently qualifies**
   at Match 4 or Match 5. A straight line never creates more than one, however
   long it is (§5.3 item 3).
3. **Positions are selected per shape, not globally.** Each creation's position
   is decided by §5.5.3 for the shape that produced it (swap origin, line
   centre, or L/T intersection) — and for a creation sourced from an L/T
   **arm**, by §5.5.3 item 7 (the arm's own line centre). There is no
   board-wide allocation pass that could move a creation to a free cell: a
   creation always lands in a cell that was part of its own shape (§5.5.3
   item 5), so a creation never displaces or re-seats another shape's creation.
4. **The deterministic priority is the match-set order, extended inside an
   L/T.** When two creations want the same cell, the one whose shape appears
   **first** in the §5.5.1 item 2 order — which is the §3.2 order, horizontal
   shapes before vertical shapes and then ascending start-cell index — is
   created, and the later claim is discarded (§5.5.4 item 3). When both claims
   come from the **same** L/T shape, the §5.5.1 item 2 order cannot separate
   them, and the **§5.5.1 item 4** intra-shape sequence decides instead (Area
   Gem, then the horizontal arm's gem, then the vertical arm's gem).
   "Priority" is therefore not a separate ranking of Special Gem *types*: no
   type outranks another, and the winner is decided by the shape's position in
   the pass's order and, within one L/T, by the source's position in the
   §5.5.1 item 4 sequence.
5. **A single cell can never satisfy two creation rules as a *source*.** The
   question "can one Gem qualify for more than one special creation rule?" has
   a precise answer: a **cell** belongs to at most one horizontal primitive and
   at most one vertical primitive (§3.3 item 1), so it can be part of at most
   one straight shape and at most one L/T shape in a pass. What *can* happen is
   that one **L/T shape** qualifies at more than one tier — its arms and the
   pattern — and that is resolved by §5.4 item 4 (all of them are created,
   subject to the collision rule), **not** by a precedence rule that suppresses
   one.
6. **Classification overlap is resolved by the ladder, evaluated per shape and
   per arm.** If a shape could be classified at more than one tier, the
   **highest** applicable tier for that line wins for that line (§5.5.2 item 1,
   §5.4 item 4 item 1), and for an L/T the pattern's Area Gem is **always**
   created in addition (§5.4 item 4 item 3). The overlap therefore never
   produces ambiguity: a line is classified once, and an L/T adds exactly one
   Area Gem no matter how its arms classified.
7. **Simultaneous creation is sequential in effect.** All creations of a pass
   happen in the single §4.1 step 2, after that pass's activations and before
   Gravity (§4.1 item 3). The board is never observed between two creations of
   the same step, and Gravity never runs between them.

### 5.8.5 The Worked Multi-Creation Examples

The cases the design must answer, resolved:

```text
Match 4 + Match 4 (one swap, two separate straight lines)
    → 2 shapes in the match set, each length 4
    → 2 Line Clear Gems, each at its own shape's §5.5.3 position
    → if the two positions are the same cell: only the first in
      §5.5.1 item 2 order is created                        (§5.5.4 item 3)

Match 4 + L/T
    → the L/T contributes 1 Area Gem (+1 per qualifying arm)
    → the straight Match 4 contributes 1 Line Clear Gem
    → 2 or more creations in total, in match-set order

Match 5 + Match 4
    → 1 Burst Gem + 1 Line Clear Gem, in match-set order

Match 5 + L/T
    → the L/T contributes 1 Area Gem (+1 per qualifying arm ≥ 4)
    → the straight Match 5 contributes 1 Burst Gem

multiple L/T
    → one Area Gem per L/T shape (+1 per qualifying arm each)

one L/T with both arms ≥ 4
    → 3 creations, in the §5.5.1 item 4 order:
          1. Area Gem      at the intersection
          2. horizontal arm gem at that arm's own line centre (§5.5.3 item 7)
          3. vertical arm gem   at that arm's own line centre (§5.5.3 item 7)
    → if any two of those positions coincide, the earlier source in the
      §5.5.1 item 4 sequence keeps the cell and the later claim is discarded
      (§5.5.4 item 3)

one L/T, both arms of length 3
    → 1 creation: the Area Gem only

one L/T, Area Gem and an arm gem claim one cell (the canonical collision)
    horizontal arm row 2, columns 0..4   (length 5 → Burst Gem)
    vertical   arm column 2, rows 2..4   (length 3 → no arm gem)
    intersection (2,2) = index 18
      Area Gem  → (2,2)                                  (§5.4 item 2)
      H arm gem → arm start 16, L = 5 → 16 + floor(4/2) = 18 = (2,2)
                                                         (§5.5.3 item 7)
    → both claim (2,2); the Area Gem is first in §5.5.1 item 4
    → (2,2) holds the AREA GEM; the arm's Burst Gem is discarded
      (§5.5.4 item 3). The L/T is still one Match and still generates
      resources once per consumed cell.

one L/T, both arm gems claim one cell
    → only possible when both arms' own centres are the same cell, which
      requires them to meet; the HORIZONTAL arm's gem is first in the
      §5.5.1 item 4 order and keeps the cell

one L/T, all three positions distinct
    → all three are created, in the §5.5.1 item 4 order
      (Area, horizontal arm gem, vertical arm gem)
```

These are consequences of §5.4 item 4, §5.5.1, §5.5.2 and §5.5.4 — not a
second set of rules. They are recorded here because they are the cases the
design had to answer, and an implementation must produce exactly these
outcomes.

### 5.8.6 Special Gem + Special Gem — The MVP Interaction Matrix

**NEW DESIGN DECISION.** There is **one** interaction rule, and it is uniform
across every combination: **a Special Gem consumed by another Special Gem's
affected set activates normally, once, with its own unmodified geometry.** No
combination is disabled, amplified, merged, transformed, or given a special
case.

| Combination | Allowed? | Effect when both are consumed in the same step |
| --- | --- | --- |
| Line + Line | Yes | Each clears its own full row/column. Two lines → the union of the two lines. |
| Burst + Burst | Yes | Each clears its own 3×3. Two squares → the union of the two squares. |
| Area + Area | Yes | Each clears its own plus. Two pluses → the union of the two pluses. |
| Line + Burst | Yes | Line clears its full row/column; Burst clears its 3×3. Union of the two. |
| Line + Area | Yes | Line clears its full row/column; Area clears its plus. Union of the two. |
| Burst + Area | Yes | Burst clears its 3×3; Area clears its plus. Union of the two. |

1. **Allowed.** Every combination is allowed. No pair is forbidden, and no
   pair requires a different Swap, a different activation trigger, or a
   different input.
2. **Effect.** Each Special Gem resolves its **own** type's geometry from
   §5.2–§5.4, at its own position, with its own orientation (for a Line Clear
   Gem). The combined result is the **union** (§5.8.2) — never a merged shape,
   never an enlarged shape, never a new shape.
3. **Affected cells.** The union of the effective sets, each clipped by §5.8.1
   and each reduced by the reserved creation cells of §5.5.4 item 2.
4. **Number of activations.** **One activation per Special Gem**, exactly. A
   Special Gem activates once per step even if both partners name its cell
   (§5.8.2 item 3). There is no "combo activation" that activates a gem twice.
5. **Chain behavior.** If the two are consumed **by the same match set** (both
   matched normally in one pass), they are resolved sequentially in §3.2
   match-set order, as siblings at the same chain level — neither is a chain of
   the other. If one is consumed by the other's affected set, it is a **chain**
   and resolves at the next breadth-first level (§5.5.5 item 7, §5.8.3 item 2).
   Both are allowed; the distinction is only *how* the second was consumed.
6. **Resource generation.** Union-based: each cleared cell generates **once**
   at the base rate (§5.7 item 6), regardless of how many effects named it, and
   a Special Gem's own cell generates when it is consumed like any other cell.
   There is **no** combination bonus, **no** multiplier for matching two
   Special Gems, and **no** additional resource for the second activator.
7. **No combo system is introduced.** **NEW DESIGN DECISION.** MVP defines **no**
   Special Gem combination table, no "line + line = cross", no "burst + burst =
   5×5", and no tier of combined effects. Such a system is a separate feature
   with its own design surface; it is not required by `MVP_SCOPE.md` §1 and is
   not authorized by `GAME_RULES.md` §7. If it is ever wanted, it is a rule
   change (`GAME_RULES.md` §20) and an addition to this section — not an
   implementation-time improvisation.
8. **"Special Gem + Special Gem interaction" via a Swap.** A Swap is validated
   only on whether it produces a Match (§2.1.2 item 4); no Swap directly on two
   Special Gems does anything by itself. Two Special Gems interact only by one
   being consumed — matched normally as part of a shape, named in the other's
   affected set, or detonated by an explicit Card/Relic/Boss effect
   (§5.5.5 item 1). **No "swap two Special Gems to fuse them" rule exists in
   MVP**, and the client cannot request one (`SIGNALR_PROTOCOL.md` §2 carries
   only `Swap(battleId, fromCell, toCell, clientSequence)`).

### 5.8.7 What This Section Does Not Change

1. **§5.5 and §5.5.1–§5.5.5 remain authoritative** for creation order,
   location, reservation, collision, and activation triggers. This section
   supplies the geometry, boundary, overlap, and ordering detail those sections
   reference, and does not replace them.
2. **No existing rule is re-opened.** §3's Match definition, §4's cascade loop,
   §6's Combo, §7's RNG contract, and §8's counter behaviour are unchanged.
3. **No new Special Gem type, event name, or wire message is introduced**
   (§5.9 item 6).

## 5.9 Summary of New Design Decisions and Their Consequences

### 5.9.1 The Decisions

```text
#   Decision                                          Owned by
--  ------------------------------------------------  ---------------------
1   Line Clear Gem clears its whole row or column,    §5.2 item 2
    including its own cell
2   Line Clear orientation follows the creating        §5.2 item 3
    line's orientation, fixed at creation
3   Burst Gem clears the 3x3 square centred on its     §5.3 item 2
    own cell, including diagonals and its own cell
4   Match 6+ is Match 5: one Burst Gem, no new tier    §5.3 item 3
5   L/T = two perpendicular primitives >= 3 sharing    §5.4 item 1
    exactly one cell; rotations/mirrors equivalent
6   L/T creates 1 Area Gem at the intersection         §5.4 item 2
7   Area Gem clears the plus/cross of its own cell     §5.4 item 3
    (itself + 4 orthogonal neighbours)
8   An L/T arm of length 4 creates a Line Clear Gem,   §5.4 item 4
    length >= 5 creates a Burst Gem, in addition to
    the pattern's Area Gem; both arms can qualify
9   Out-of-board cells are discarded, never wrapped    §5.8.1
    or clamped
10  Overlapping effects: cleared once, generate once,  §5.8.2
    activate once; accounting is by unique cleared cell
11  Chain activations are breadth-first, immediate,    §5.8.3
    and finite by consumption — no depth cap
12  A newly created Special Gem never activates in     §5.8.3 item 6
    the pass that created it
13  One pass may create multiple Special Gems; the     §5.8.4
    count is determined per shape, positions per
    shape, collisions by match-set priority
14  Special + Special: no combination system; each     §5.8.6
    activates normally, union semantics, no bonus
15  Activation clears generate at the base rate,       §5.7 item 6
    with no tier multiplier
16  Special Gem activation does not change Combo       §6.3 item 2 (unchanged)
17  An L/T arm's Special Gem is placed at THAT ARM's   §5.5.3 item 7
    own line centre, computed from the arm's own
    primitive and its ACTUAL length; the swap-origin
    rule does not apply to any L/T creation
18  Even-length arm (length 4) uses the lower-indexed  §5.5.3 item 7 item 3
    of the two middle cells, per §5.5.3 item 3
19  Intra-L/T creation order and collision priority:   §5.5.1 item 4
    Area Gem, then the HORIZONTAL arm's gem, then the
    VERTICAL arm's gem — an ordering of creation
    SOURCES, never a ranking of Gem types
```

Decisions 17–19 were added to resolve the two ambiguities recorded after the
first Special Gem design pass: the position rule for an L/T arm's Special Gem,
and the ordering/collision rule for the up-to-three creations of one L/T shape.
They are **new design decisions**, not recovered content, and are labelled at
their owning sections.

### 5.9.1.1 Why Decisions 17–19 Were Required

```text
Ambiguity: L/T arm creation position
    §5.5.4 item 3 named "the arm's swap origin or line centre", but §5.5.3
    item 1 scopes the origin rule to a straight Match 4 / Match 5 and §5.5.3
    item 2 states an L/T needs no origin. Two readings were consistent with
    the text and produced different boards.
    Resolved by: §5.5.3 item 7 (one position: the arm's own line centre).

Ambiguity: ordering/collision among one L/T's own creations
    §5.5.1 item 2 orders SHAPES, and an L/T is one shape (§3.3 item 2), so
    the up-to-three creations from one L/T had no order and no collision
    winner. Reachable (a T whose long arm's centre is the junction).
    Resolved by: §5.5.1 item 4 (Area Gem, then H arm's gem, then V arm's gem).
```

Both resolutions preserve the existing principle that collision priority is
**not** a ranking of Special Gem types (§5.8.4 item 4): the arm rule is a
position rule derived from the arm's own cells, and the order rule sequences
creation *sources* using the pass's own horizontal-before-vertical key
(§3.2 item 1).

### 5.9.2 Implementation Impact

1. **The three geometries are pure functions of a cell index and the board
   size.** Each is a small set of `(row, column)` offsets plus one clipping
   test — no path-finding, no flood fill, no search.
2. **The resolution needs one union accumulator per step**, not one removal per
   effect. That is the single structural addition §5.8.2 requires.
3. **The activation sequence needs one FIFO queue for chains** (breadth-first,
   §5.8.3 item 2). This is the same shape `RELIC_RULES.md` §4 item 3 already
   needs for Relics, so it is not a new mechanism.
4. **Nothing new is needed in state, transport, or protocol.** The rules act on
   `BoardState.Cells[64]`, which is now also where a Special Gem is held
   (`GAME_STATE.md` §2.1). This resolution did not decide that shape and does
   not depend on it — it is recorded here as the resolution of the field-shape
   gap this item previously left open, and it changes none of the rules above.
5. **No RNG is added.** All three geometries, clipping, union, ordering, and
   chains are pure functions of the board (§7.2 items 1, 2).

### 5.9.3 Documentation Impact

1. **`MATCH3_RULES.md` §5** is the owning section for every decision above and
   is the only document that needed the new rules.
2. **`COMBAT_RULES.md` §2** is referenced for every multiplier and rate and is
   **not** changed by this resolution: its match-tier table already covers
   Match 3 / 4 / 5 / L-T, and §5.7 item 6 places activation clears at the base
   rate it already owns. A clarification is recorded there (§5.9.4).
3. **`GAME_STATE.md` §2.1.2** recorded the `PendingSpecialGems[]` field-shape
   gap. That resolution did not close it and did not require it to close: the
   rules are expressible on `Cells[64]` (§5.5.4 item 4, `GAME_STATE.md`
   §2.1.2 item 5). It has since been **closed separately, by its owning
   document**: `GAME_STATE.md` §2.1 now defines `BoardState` as `Cells[64]`
   alone, holds a Special Gem inside its cell entry, and removes the
   placeholder field. §5.5.4 item 4 is updated to point at it. No rule in this
   document changed as a result, and no section of this document needed a
   substantive edit for it.
4. **`GAME_EVENTS.md`** already defines `GemMatched` as once per cell over the
   union (§2) and `MatchCreated` with the Special Gem created (§2). The new
   rules change neither, so no change was required by this resolution for those
   definitions. A later event-contract clarification added §1.3 there, which
   fixes the enumeration order of the `GemMatched` events inside one cleared
   union (ascending §1.0 cell index). It is an event-enumeration order only and
   changes nothing in §3.2, §5.5.1, §5.5.5, or §5.8 — see §5.6. That document
   was also updated when the state representation was resolved, to carry the
   consumed Special Gem's type and orientation in the `GemMatched` payload
   (`GAME_STATE.md` §2.1.4); this document's rules are unaffected by it.
5. **`GAME_RULES.md` §7** already delegates Special Gem behavior to this
   document and needs no change.

### 5.9.4 Conflict Check

The new rules were compared against every document §19 of the task lists, and
against `ELEMENT_RULES.md`, `MVP_SCOPE.md`, and `ARCHITECTURE.md`. No conflict
with an authoritative rule was found. Two items are recorded as **clarifications
applied**, and one as a **non-conflict noted but deliberately not changed**:

1. **`COMBAT_RULES.md` §2 vs §5.7 item 6 — clarified, not conflicting.**
   `COMBAT_RULES.md` §2 defines multipliers for *Match 3 / Match 4 / Match 5 /
   L/T*, i.e. for **matched** Gems. §5.7 item 6 places a Special Gem
   activation's cleared cells at the base rate. These do not disagree — a
   Special Gem activation is not a Match (§5.5.5 item 8, `PASSIVE_RULES.md`
   §2.2) and so has no tier — but §5.7's original line said only "base rate"
   without stating *why* no tier applies. A one-line clarification is added to
   `COMBAT_RULES.md` §2 making the scope explicit ("applies to matched Gems;
   Special Gem activation clears generate at the Match-3 base rate"). That is
   the smallest correction, and `COMBAT_RULES.md` remains the sole owner of the
   numbers.
2. **`MATCH3_RULES.md` §5.2/§5.3/§5.4 vs §5.5.1–§5.5.5 — resolved, not
   conflicting.** §5.5.3, §5.5.4, and §5.5.5 already cited `§5.2 item 1/2`,
   `§5.3 item 1/2/3`, and `§5.4 item 1/2/3`, but §5.2–§5.4 did not define
   those items. That was a **dangling internal reference**, not a rule
   conflict between documents. It is resolved by authoring the missing items in
   §5.2–§5.4, which is exactly what those sections now contain. No §5.5.x rule
   was changed except where it restated a geometry that §5.2–§5.4 now own
   (§5.5.5 item 4, §5.5.4 item 4), and those edits only replace a restatement
   with a pointer.
3. **Provenance note — the committed `v1.0` text, and why it is not treated as
   authority.** The repository's committed version of this document (`v1.0`, at
   `HEAD`) contained a **short** Special Gem ruleset in its §5.2–§5.4. The
   preceding documentation-recovery session classified the detailed rules as
   unrecoverable and replaced them with "unresolved" stubs; this task then
   authored the rules above. **The committed `v1.0` text is recorded here for
   transparency, but it is not the authority for the rules in §5.2–§5.4**, for
   three reasons:

   1. It is **not** a surviving authoritative artifact in the sense the recovery
      used: it is a four-item sketch that never defined creation order,
      reservation, collision, chains, boundaries, overlap, ordering, multiple
      creation, or the interaction matrix — i.e. most of what §5.5.x and this
      resolution had to decide. Calling the new rules "recovered" from it would
      overstate it.
   2. Where it did speak, `v1.0` was **ambiguous or incomplete** on exactly the
      point this resolution had to fix: its Line Clear wording ("the remainder
      of its row or column") admits a directional reading that the Swap action
      cannot supply, and its Area Gem shape was left as "configuration".
   3. Treating it as authority would mean the new decisions are **presented as
      recovered historical content**, which the task explicitly forbids.

   This resolution therefore stands on its own reasoning, and the label
   `NEW DESIGN DECISION` on each ruling remains correct. The author notes, as a
   matter of record, that the independently-authored decisions **converge with
   the `v1.0` sketch on most points** (Match 4 / Match 5 / L-T type assignment,
   swap-origin and line-centre placement, Burst = 3×3 centred, Match 6+ = Match
   5, L/T = intersection, Area = cross, both-gems-for-a-qualifying-arm, chains
   allowed and sequential). Convergence is not adopted as evidence of recovery;
   it is reported so a human reviewer can see that the new rules are consistent
   with the project's last committed intent, and can overrule any point they
   disagree with (`GAME_RULES.md` §20).

   **The single behavioral difference from the `v1.0` sketch** is the Line Clear
   extent: `v1.0` said "remainder of its row or column", this resolution says
   the **entire line including the Gem's own cell** (§5.2 item 2's wording
   note). The new ruling is the one in force. If a human reviewer prefers a
   directional or partial clear, that is a rule change to §5.2 item 2 and must
   be made explicitly, not inferred from the older text.

   **Historical evidence on the L/T arm placement (§5.5.3 item 7, decision 17).**
   The `v1.0` sketch's §5.4 item 3 said that when an L/T arm separately
   qualifies as Match 4 or Match 5, "the higher-tier Special Gem rule
   (§5.2/§5.3) takes precedence **over the L/T rule for that arm**". That is
   **historical evidence** that an arm's gem was intended to follow the
   straight-line placement rules (swap origin or line centre) rather than the
   L/T intersection — it corroborates decision 17's rejection of the
   intersection for arm gems. It is **not**, however, the source of decision
   17: `v1.0` never stated which of "swap origin" or "line centre" applies to
   an arm, never defined an arm's origin for an L/T, and never mentioned the
   arm's own length or the even-length tie-break. Decision 17 is therefore a
   **new design decision** that is *consistent with* this historical evidence,
   not a recovery of it. `v1.0` is superseded by the current text on every
   point it left open.
4. **`MVP_SCOPE.md` §1 vs the new rules — consistent, no change.**
   `MVP_SCOPE.md` §1 lists `Match 3 / Match 4 / Match 5`, `L/T matches`,
   `Cascade`, and `Combo`. Decision 4 (§5.3 item 3: Match 6+ is Match 5) keeps
   the tier list exactly as `MVP_SCOPE.md` states it and introduces no new
   system. The multiple-creation and interaction rules are properties of
   Match-4/5/L-T resolution, not new scope items. **No `MVP_SCOPE.md` change is
   required or made.**
5. **`GAME_RULES.md` §7 vs decision 14 (§5.8.6 item 7) — consistent.**
   `GAME_RULES.md` §7 says each of Match 4 / Match 5 / L/T produces "a distinct
   Special Gem with an area-clear effect". It does **not** define a combination
   system, so declining to add one is the conforming reading, not a
   contradiction. **No `GAME_RULES.md` change is required or made.**
6. **`GAME_STATE.md` §2.1.2 item 5 and `REDIS_STATE.md` §7 item 9 — since
   resolved by their owning document.** Both recorded the
   `PendingSpecialGems[]` **field-shape** gap. This resolution defined the
   gameplay rules and deliberately did **not** define the representation (what
   a Special Gem's `Cells[64]` entry holds, the collection's element shape or
   ordering): that gap was owned by the Special Gem implementation stage, and
   closing it here would have been inventing a contract this task did not own.
   It was subsequently closed **there**, by the state-representation decision:
   `GAME_STATE.md` §2.1 now holds a Special Gem inside its cell entry in
   `Cells[64]` and defines ownership, occupancy, ordering, serialization, and
   Redis/SignalR consequences; `REDIS_STATE.md` §7 item 10 records the Redis
   consequence. Those are state-contract changes, and they change no rule in
   this document.
7. **`SIGNALR_PROTOCOL.md` §8 item 7 — consistent.** That section forbids new
   board-resolution messages including a `SpecialGemActivated` message. The new
   rules add no message, no method, and no subscription; activation is reported
   through the existing `GemMatched` event and the existing state push. **No
   protocol change is required or made** by this resolution. The later
   state-representation decision touched that document (§4.1 item 5) only to
   state that the board carries its Special Gem state in the existing push — it
   adds no message either, so this item still holds unchanged.

### 5.9.5 Remaining Open Items

**There are zero unresolved Special Gem gameplay rules required to implement
MVP Special Gems.** Every item the design was asked to resolve now has an
explicit owning rule (§5.9.1), and the one Special-Gem-adjacent item that was
open here has since been closed by the document that owned it:

```text
BoardState.PendingSpecialGems[] field shape
    - what a Special Gem's entry in Cells[64] holds
    - the collection's element shape and ordering
    Owner: GAME_STATE.md §2.1 (the state contract's canonical owner)
    Status: RESOLVED — BoardState is Cells[64] alone; each cell entry
            carries its Gem type plus an optional Special Gem
            (type, and orientation for a Line Clear Gem). The
            PendingSpecialGems[] field no longer exists.
    Redis consequence: REDIS_STATE.md §7 item 10
```

It was a **serialization-representation** decision, not a gameplay rule: the
gameplay contract was already complete on `Cells[64]` alone
(`MATCH3_RULES.md` §5.5.4 item 4). It blocked *storing or delivering* a board
that carries Special Gems, and it has been resolved by its owning document —
not by an inference from this one. **No open Special-Gem item remains anywhere
in this document set.**

---

# 6. Combo

Combo counts consecutive Matches caused by one Swap (`GAME_RULES.md` §5). This
section owns the Combo value's lifecycle as board mechanics; `GAME_RULES.md` §5
owns the rule and its damage multipliers.

## 6.1 Starting Value and Reset

1. `Combo` is a `BattleState` field (`GAME_STATE.md` §2.2, `BattleState.Combo`)
   whose rule-level value starts at **0**.
2. A **committed** Swap resets Combo to 0 before the first Match of that Swap is
   counted (§2 item 5). The reset is part of the committed Swap's resolution
   (§2.1.6 step 6), after validation, after `SwapStarted`, and before any Match
   of the Swap.
3. A **rejected** Swap neither resets nor changes Combo (§2.1.5 item 5): it is
   not a Swap that began (§2 item 5).
4. Combo does not persist between Swaps (`GAME_RULES.md` §5 item 3), and nothing
   in this contract carries it across a Turn boundary.

## 6.2 Increment

1. Combo is a **count of Matches within the current Swap**, so it advances by
   exactly **1 per Match**, in detection order, and the first Match of a Swap
   therefore makes Combo **1** – never 0 and never 2 (`GAME_RULES.md` §5 item 2).
2. The value must not be settable to anything other than `previous + 1` by a
   Match: there is no double increment for a big shape, no bonus for an L/T,
   and no increment per cleared cell.
3. A shape counted as one Match (§3 item 5) increments Combo once, whatever its
   size or tier.
4. Gems cleared by a Special Gem effect increment nothing (§5.5.5 item 8).

## 6.3 Cascade Increment

1. Every Match found in a Cascade pass increments Combo exactly as a Match of
   the Swap's first pass does (§4 item 4). Combo is therefore equal to the
   running total of Matches detected in the Swap so far, across all passes.
2. **A Special Gem activation by itself does not increment Combo** (it is not a
   Match, §5.5.5 item 8). A Special Gem activation that leads to a further
   detection pass contributes to Combo only through the Matches that pass
   detects.
3. A Special Gem **creation** never increments Combo: creation is a consequence
   of a Match that has already been counted.

### 6.3.1 Special Gem Activation and Combo — The Explicit Ruling

**NEW DESIGN DECISION (confirmation of the existing distinction).** Does a
Special Gem activation change Combo, delay it, or reset it? **None of the
three.** The existing distinction is preserved, and this item rules the
question directly rather than leaving it to be inferred:

```text
Special Gem activation         → does NOT change Combo
Special Gem chain activation   → does NOT change Combo
Special Gem creation           → does NOT change Combo
Special Gem effect clearing N  → does NOT change Combo by N
    cells (it is not N Matches)
```

1. **It does not change Combo.** An activation is not a Match (§5.5.5 item 8,
   `PASSIVE_RULES.md` §2.2, `GAME_RULES.md` §5 item 1), so it neither
   increments Combo nor sets it. No Special Gem type has a Combo bonus, and no
   number of cleared cells is converted into Combo.
2. **It does not delay Combo resolution.** Combo is advanced by Matches in
   detection order as the resolution proceeds (§6.6 item 1). A step 2
   activation sits *inside* a pass, between the pass's Match counting and its
   Gravity (§4.1, §8.3), and it neither suspends nor defers the counting of the
   Matches that follow it. When a pass's match set contains both Matches and
   Special Gem activations, the Matches are counted in the pass's §3.2 order
   and the activations are simply not counted.
3. **It does not reset Combo.** The only reset is §6.1 item 2, a committed
   Swap. An activation is not a Swap and cannot reset Combo, however many cells
   it clears and however long its chain runs.
4. **Ordinarily this is a non-question, because of the reservation rule.** A
   Special Gem is consumed either as a member of the pass's match set — in
   which case the Match it belongs to is counted once, as that shape, and the
   activation adds nothing — or as an explicit detonation outside a Swap's
   resolution, in which case there is no Swap Combo to affect (`CARD_RULES.md`
   §3 item 5: a Card cast does not consume a Turn and does not interact with
   Combo). The ruling in items 1–3 therefore covers the same ground from the
   other direction, not a third case.
5. **Why no change is warranted.** `GAME_RULES.md` §5 item 1 defines Combo as
   "consecutive Matches caused by one Swap", and §5 item 4 makes Combo a damage
   modifier. Counting an explosion as a Match would make Combo a measure of
   board activity rather than of matching skill, would let a single detonation
   spike the damage multiplier far beyond what the §5 table (`1.00×`–`1.50×`)
   is calibrated for, and would break the identity §6.5 item 3 relies on
   (Combo = the Swap's Match count, §6.3 item 1). No gameplay reason requires
   it, so the distinction stands.

## 6.4 Reset Condition

The only reset in this contract is §6.1 item 2, on a committed Swap. A pass
that finds no match does not reset Combo – it ends the Cascade (§4.3), and the
Swap's Combo remains the total of the Matches it produced. Combo returns to 0
when the **next** Swap commits.

## 6.5 Swap With No Match, and the Combo Floor

1. A Swap producing no Match is **rejected**, not resolved (§2.1.2 item 4), so
   the "Swap with no Match" state is not reachable as a committed Swap: the
   board reverts and Combo keeps the value it held before the action (§2.1.5).
2. After a rejected Swap, Combo still holds the previous Swap's final value – it
   is not zeroed by a rejection. The zeroing happens when a Swap **begins**
   (§6.1 item 2), which a rejection is not.
3. **No committed Swap can end with Combo = 0.** A committed Swap always
   contains at least one Match (§2.1.2 item 4 and the note following it), and
   each Match increments Combo, so the value it leaves behind is at least 1.
   The transient `Combo = 0` between the reset of §6.1 item 2 and the Swap's
   first Match is internal to the resolution and is not an observable value: it
   is never written to the state store and never published
   (`GAME_STATE.md` §5.1 item 2). An implementation that can publish a committed
   Swap with `Combo = 0` has applied §2.1.2 item 4 after committing rather than
   before, which contradicts §2.1.6.
4. Consequently `Combo` as a *published* value reads 0 only before the battle's
   first committed Swap (`GAME_STATE.md` §2.2); from the first committed Swap
   onward it is at least 1.

## 6.6 Timing of State and Events

1. In one Swap's resolution, Matches are processed in the order of §3.2 and
   §4.2, and Combo reaches its final value for that Swap **before** the
   resolution completes. A Match that ends a Cascade pass still increments
   Combo before that pass's Gravity/Spawn runs.
2. `ComboChanged` (`GAME_EVENTS.md` §2) is emitted whenever the Combo value
   changes – on each increment during the resolution, and on the reset of
   §6.1 item 2 when that reset changes the value. Its exact position in the
   event stream, and whether the values are reported per Match or once per
   Swap, are owned by `GAME_EVENTS.md` §1—§2; this section owns only *when the
   value changes*.
3. Combo is authoritative server state (`GAME_RULES.md` §18): it is computed
   here, not received from the client, and the client renders it from the events
   and from the published state.

## 6.7 The Worked Example

The canonical example of `GAME_RULES.md` §2 and §5, expressed in this
document's terms:

```text
Turn 21
Swap                                  (§2.1.6) – Combo reset to 0
 └── pass 1, depth 1                  → Match #1 detected
       Match #1 counted               → Match count 1, Combo 1
       └── gravity + spawn
             └── pass 2, Cascade 1    → Match #2 detected
                   Match #2 counted   → Match count 2, Combo 2
                   └── gravity + spawn
                         └── pass 3, Cascade 2  → Match #3 detected
                               Match #3 counted → Match count 3, Combo 3
                               ── gravity + spawn
                                     └── pass 4, Cascade 3 → no match
                                           Cascade loop ends

Turn    = 21        (this Swap is Turn 21 – GAME_RULES.md §2)
Matches = 3         (this Swap's Match count; the battle total is
                     BattleState.MatchCount, GAME_STATE.md §2.2)
Combo   = 3         (consecutive Matches of this Swap, §6.3 item 1)
```

1. The **Turn number** is the current Turn for the Swap, labelled "Turn 21" as
   `GAME_RULES.md` §2's example does; how the counter is stored and written is
   owned by `GAME_STATE.md` §2.0.2 / §5.1 and §8 below.
2. **Matches** is the number of Matches produced by this Swap (3). The
   cumulative battle total is `BattleState.MatchCount` (`GAME_STATE.md` §2.2),
   owned by `GAME_RULES.md` §3.
3. **Combo** is 3 – the number of consecutive Matches in this one Swap, which
   equals the Match count of this Swap by §6.3 item 1.
4. The example contains **three Cascades** (passes 2, 3, and the terminating
   pass 4) and **three Matches**. Pass 4 produces no Match and no
   `CascadeCreated` (`GAME_EVENTS.md` §2).

---

# 7. Determinism and RNG During Resolution

1. Given the same board state and the same Swap input, match detection, cascade
   resolution and special gem creation must be fully deterministic **except** for
   the random Gem values spawned into empty cells, which use a server-seeded RNG.
2. The server is authoritative for the RNG seed and all resulting board states,
   per GAME_RULES.md §18.
3. The RNG is the deterministic server-seeded generator defined by `ADR-009`.
   Its seed, state representation, and advancement semantics are owned by
   `GAME_STATE.md` §2.6; this document owns which board operations consume it
   (§1.2.1.4 for the initial fill, §4.5 item 4 for gameplay spawn, §7) and the
   value distribution (§1.2 item 4). The generator is a **consumer** of that
   PRNG, not a replacement for it: §1.2.1 changes which values are drawn and in
   what order, and changes nothing about the PRNG itself.
4. Initial board generation (§1.2) is deterministic in the same sense: the same
   seed produces the same initial board, including the same 64-selection
   sequence per attempt (§1.2.1.4) and the same §1.5 retry sequence. A retry
   continues the same RNG stream rather than re-seeding.
5. No other source of randomness may be used for any board operation. Client-side
   randomness is never authoritative.

## 7.1 The Reproducibility Guarantee

```text
same initial state
  + same ordered sequence of accepted actions
=
same final board
  + same RngState
  + same Match count
  + same Combo
```

1. The guarantee holds for the whole battle: state and RNG state advance only
   through the accepted actions of §2.1.6, and every operation between them is
   a pure function of the state.
2. A rejected action (§2.1.5) is not part of the sequence: it changes neither
   the board nor `RngState`, so a rejected action may be removed from a
   battle's input history without changing anything downstream.
3. Recovery is covered by the same guarantee: `RngState` is part of
   `BattleState` (`GAME_STATE.md` §2.6.2 item 4), so a battle restored from a
   snapshot continues the stream exactly as the unrecovered battle would
   (`ADR-008`).

## 7.2 What Consumes RNG, and What Must Not

```text
Consumes RNG during gameplay
  cascade spawn into empty cells (§4.5 item 4)     exactly 1 selection per
                                                    spawned cell
  initial board generation (§1.2.1.4)              exactly 1 selection per
                                                    cell, initialization only
```

1. These are the only board operations that consume randomness. Anything else
   that consumes randomness on the board would be a rule change and must go
   through `GAME_RULES.md` §20 before implementation.
2. The following **must not** consume RNG. Each is a pure function of the board
   state and the resolution's inputs:

```text
Swap validation, including the simulated swap and its match check  §2.1.2
match detection and the match-set order                            §3
match resolution and cell removal                                  §4.1
Special Gem creation, its type, order, location and collision      §5.5
Special Gem activation and its affected set                        §5.5.5
gravity                                                            §4.4
Combo calculation and Match counting                               §6
cascade termination and pass ordering                              §4.2, §4.3
event creation, ordering and serialization                         GAME_EVENTS.md
SignalR delivery                                                   SIGNALR_PROTOCOL.md
client rendering and presentation                                  ARCHITECTURE.md §2.2
```

3. A Relic or effect that overrides the spawn distribution (§4 item 2) changes
   *which* values the documented draw produces, not how many selections are
   consumed or where they are consumed. It introduces no second generator
   (`ADR-009` §1).
4. `System.Random`, `Random.Shared`, `Guid`-derived values, timestamps,
   hash-order-dependent iteration, and client-side sources must never produce a
   gameplay-relevant value (`ADR-009` §2, `TDD.md` §6 item 3).

## 7.3 Value Conversion During Gameplay

1. A spawned Gem's type is produced by reducing the PRNG selection onto the
   four §1.1 types:

```text
one PRNG selection
        ↓
reduce onto the four §1.1 types in their fixed order (ATK, DEF, HP, POWER)
        ↓
one Gem type
```

2. The reduction is part of the rule, not an implementation detail: two
   implementations given the same `RngState` must produce the same Gem, so the
   reduction must be specified by the PRNG's own documented semantics
   (`ADR-009`, `GAME_STATE.md` §2.6.3) rather than chosen per implementation.
   The reduction over a *candidate set* is documented in §1.2.1.4 item 2; a
   gameplay spawn has all four types as its candidate set, so it is that same
   reduction applied to the full set.
3. The conversion consumes exactly one selection per spawned cell
   (§4.5 item 4) and leaves the stream positioned for the next spawned cell in
   the §4.5 item 2 order.

---

# 8. Turn and Sequence During Board Resolution

`GAME_RULES.md` §2 owns what a Turn **is**, and `GAME_STATE.md` §5 owns what
`Sequence` **is** and the concurrency rule built on it. This section owns what
board resolution **does** to both counters, and is the single place the
resolution stage's counter behaviour is stated. `GAME_STATE.md` §5.1 references
this section rather than restating it.

## 8.1 When Turn Changes

1. **A Turn is a successfully resolved player Swap/Action** (`GAME_RULES.md` §2
   item 1). It begins when a Swap is **committed** (§2.1.6 step 5) – after
   validation has passed, never before it.
2. **One committed Swap begins exactly one Turn** (§2 item 5). The Turn's whole
   resolution – every Match, every Cascade, every Special Gem, and the entire
   §4 loop – belongs to that one Turn and begins no additional Turn.
3. **A rejected Swap begins no Turn** (§2.1.5 item 2). Out-of-range, non-
   adjacent, already-applied, and no-match-producing actions all leave `Turn`
   unchanged.
4. **Nothing else on the board begins a Turn.** Board generation is not a
   Swap/Action (`GAME_STATE.md` §2.0.5.2 item 1); Match Detection, Match
   Resolution, Special Gem creation and activation, Gravity, Spawn, and Combo
   calculation are steps *within* a Turn and never begin one.
5. **A Card cast begins no Turn** (`CARD_RULES.md` §3 item 5). The Turn rule
   applies to the Swap/Action that starts it; Card casts are independent of the
   Match-3 Turn/Combo system and are not board resolution.
6. This document fixes the *order* in which the counter changes relative to the
   resolution (§8.3). `GAME_RULES.md` §2 owns what a Turn is,
   `GAME_STATE.md` §2.0.2 owns the counter's stored representation and initial
   value, and `GAME_STATE.md` §5.1 owns the state write-back that persists it.

## 8.2 When Sequence Changes

1. **One successfully resolved action increments `Sequence` by exactly 1** – a
   committed Swap is one such action (`GAME_STATE.md` §5). It is not one
   increment per Match, per pass, per Cascade, per Special Gem, per removed
   cell, or per increment of anything else.
2. **Board resolution substeps do not increment `Sequence`.** The §4 loop's
   Gravity, Spawn, detection passes, Special Gem creation and activation, and
   Combo changes are *inside* one resolution and increment nothing. `Sequence`
   is updated once, at the end of the whole resolution (§8.3).
3. **A rejected Swap increments nothing** (§2.1.5 item 3).
4. **A card cast increments nothing on the board.** Non-Swap actions are not
   board resolution; their own `Sequence` treatment is owned by the task that
   implements them and by `GAME_STATE.md` §5, which states the resolution
   counter rule they must satisfy.
5. **Board generation increments nothing.** `Sequence = 0` remains the only
   valid value until the first action resolution succeeds (`GAME_STATE.md`
   §2.0.2, §2.0.5.2 item 1).
6. **Reading or publishing state increments nothing.** Neither the state push
   (`SIGNALR_PROTOCOL.md` §4), the event batch (§3), nor a snapshot request
   (§7) is an action, so none of them changes `Sequence`. `RngState` advancing
   and `Sequence` advancing are independent (`GAME_STATE.md` §5.2 item 4).

## 8.3 Update Order Within a Resolution

A committed Swap's resolution updates the counters in this fixed order:

```text
1. Validate the action                      (§2.1.2)   – counters unchanged
2. SwapStarted                              (GAME_EVENTS.md §2)
3. Commit the exchange to the board         (§2.1.6 step 4)
4. Begin the Turn                           (§8.1)     – Turn takes effect here
5. Reset Combo to 0                         (§6.1 item 2)
6. SwapResolved                             (GAME_EVENTS.md §2)
7. Resolve the board until stable           (§4 loop)  – Turn and Sequence are
                                                          not written here
8. Increment Sequence by exactly 1          (§8.2)     – after the board is
                                                          stable
9. Retain the resulting RngState            (GAME_STATE.md §2.6.2)
10. Publish the authoritative state          (SIGNALR_PROTOCOL.md §3, §4)
```

Steps 3–4 also record the committed pair (`GAME_STATE.md` §2.1.10 item 5); it
travels in the same single write-back as `Turn` and `Sequence` below and is
never written mid-resolution (§8.3 item 1, `GAME_STATE.md` §5.1).

1. `Sequence` reaches its new value **only after** the resolution is complete –
   the Swap's whole board outcome is included in the change the new value
   describes. A reader that observes the new `Sequence` therefore observes the
   finished board of that Turn.
2. `Sequence` is the **single** published state version: board, `RngState`,
   Match counts and Combo all become visible together under it, and no per-step
   incremental write of the state exists (`GAME_STATE.md` §5.1).
3. The write-back to the state store, the compare-and-set it performs, and the
   retry it may take when the store's `Sequence` has moved are owned by
   `REDIS_STATE.md` §4 and do not change the order above: a retry re-runs the
   same deterministic resolution, so the result is identical.
4. `Turn` is not a version and is never used for concurrency control
   (`GAME_STATE.md` §5 item 3).

## 8.4 Counters During the Resolution

| During the resolution | Turn | Sequence | RngState |
| --- | --- | --- | --- |
| Swap validation, including the simulated swap and its match check | unchanged | unchanged | unchanged |
| Committing the exchange | begins | unchanged | unchanged |
| Match Detection / Match Resolution (each pass) | in effect | unchanged | unchanged |
| Special Gem activation and creation | in effect | unchanged | unchanged |
| Gravity | in effect | unchanged | unchanged |
| Spawn | in effect | unchanged | **advances** (one selection per spawned cell) |
| Combo calculation | in effect | unchanged | unchanged |
| Cascade termination | in effect | unchanged | unchanged |
| End of the whole resolution | in effect (still this Turn) | `+1`, once | retained as produced |

"In effect" means the Turn the committed Swap began is the Turn the step belongs
to; the step neither starts a Turn nor ends one.