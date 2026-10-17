# Match-3 Rules

**Version:** 1.12 (§1.A, §1.6.3, §4.1, §4.4, §4.5, §4.6, §5, §5.7 items 1–7, §5.8, §5.9, §6.2, §6.3, §6.3.1, §7.2, §8.1, §8.2, §8.4 reconciled per TASK-287 under operator decisions OD-1–OD-7: Special Gems removed in full (creation, placement, activation, chain reactions, AoE clearing, interaction matrices retired); match-tier multipliers retired; §4.1 pass simplified to Match Resolution → Gravity → Spawn → Re-detect; §5.7 items 1–7 reshaped to matches only; §5.7 item 8 preserved per TASK-286. **Interim join notice retained:** This document is shared with TASK-286 and finalized under the joint group commit protocol (`TASK_LIFECYCLE.md` §6.1 item 5); neither slice may be committed alone. Prior 1.11: (§5.7 item 8 **REWRITTEN** per TASK-286 under the final operator decisions OD-1–OD-7: Special Gems are removed entirely from gameplay — no creation, activation, chain activation, or area-of-effect clearing — and all match-tier resource multipliers are removed; resource generation is triggered only by Matches, at the approved per-Gem `BaseOutput`, counted once per unique matched cell and accumulated per cascade pass. **This item's slice alone does not make §5 or §5.7 self-consistent:** the retirement of §5.1–§5.9, §5.5.5, §6.3.1, the §5.7 tier items, and §4.1's Special Gem stage is owned by TASK-287 in the **same join group commit** (`TASK_LIFECYCLE.md` §6.1 item 5); neither slice may be committed alone. Prior 1.10: §5.7 item 8 updated per TASK-286 cross-referencing `COMBAT_RULES.md` §2 item 6's reconciled Special Gem Combat Contribution Contract (operator-confirmed match-based model D1–D4; Decision A calculation order; obsolete detonation clear mechanics and former Gate A decision A2 retired; attack qualification gated strictly on confirmed valid ATK match D2; former Gate B detonation-overlap retired with calculation order formalized per Decision A); no board, swap, cascade, or gem rules changed. Prior 1.9: §1.6.4 item 4 now names `CARD_RULES.md` §3 item 8 as the owner of the
post-terminal Card/Pet Skill cast question it previously left unattributed — that rule
refuses such a cast under the same `STALE_ACTION` contract §2.1.4 item 7 uses for the
Swap. No Match-3 rule, guard, step, code, event, or RNG behaviour changes; §2.1.4 item 7
and §1.6's recovery rule are untouched. Prior 1.8: (§1.6 "Runtime Board Deadlock Recovery" added and §2.1.4 item 7 added per ADR-026 / TASK-267: every committed board that is non-terminal and permits a Player action has at least one legal match-producing adjacent swap, evaluated and recovered at the Round commit boundary on the battle's retained Pcg32 stream with §1.5 item 2's unchanged 64-attempt bound, with terminal boards exempt, the Boss's pre-move recovery retained, and no Battle Event emitted; a Swap against an already-terminal battle is rejected under §2.1.4's existing state-precondition code before any resolution or RNG. §7.2's RNG-consumer list now names that recovery, §1.6.4's citations and §2.1.2's ordering pointer are corrected, and §1.6.3 records that a regenerated board preserves no Special Gem (TASK-268). Prior 1.7 (§2, §8 updated per ADR-026 / TASK-259: Player and Boss share the one Match-3 board; both act via valid swaps; cell type determines effect; acting side determines ownership and target; Turn represents an atomic combat Round; Sequence increments once per committed round write-back. Prior 1.6: §2.1.4 item 2 **completed, not changed in its rules**: the
action-identity recognition window is stated as what the rule and
`GAME_STATE.md` §2.1.10 item 5 already define — the identity is compared with the
most recently committed Swap, so a retry arriving after a later action committed
is rejected by item 1 as `STALE_ACTION` — and the no-precondition request form's
limitation is stated: such an action is resolved only against the state read for
it and is rejected as `STALE_ACTION` if a concurrent writer moves that state
first, never re-resolved against the newer board. `ADR-025`'s items 1–6 are
unchanged, no rejection reason is added or removed, and no gameplay rule changes.
Prior 1.5: (§6.1/§8 citations updated — `PlayerState.Combo`/
`PlayerState.MatchCount` replaced with `BattleState.Combo`/
`BattleState.MatchCount` per Player-as-owner / Pet-as-combat redesign;
no gameplay rule changed — committed-swap state owner named for §2.1.4's
already-applied check — see `GAME_STATE.md` §2.1.10)
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

This document defines the exact Match-3 board behavior. It expands GAME_RULES.md
sections 3, 4 and 7 ("Match Rules", "Cascade Rules", "Special Gem Rules (Retired)"). If anything
here conflicts with GAME_RULES.md, GAME_RULES.md wins and the conflict must be reported
(see GAME_RULES.md §20).

> **§5 Special Gem Rules are RETIRED per OD-1–OD-7 / TASK-287.** Special Gems
> do not exist in gameplay; former §5.2–§5.4, §5.8, and §5.9 are retired in full.
> See §5 for retirement and authoring history.

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
Match Resolution              §4.1 — cell consumption, set union
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

## 1.6 Runtime Board Deadlock Recovery

§1.3–§1.5 govern the **initial** board, at battle creation. This section
governs the board of a battle already in progress: the board the Player is
about to act on.

### 1.6.1 The playable-board invariant

> **Every committed board that is non-terminal and permits a Player action has
> at least one legal match-producing adjacent swap.**

1. **"Legal match-producing adjacent swap" is §2.1.2's rule, not a second
   definition.** It is a pair of orthogonally adjacent cells whose exchange
   produces at least one §3 Match — exactly the pair §2.1.2 item 5 accepts and
   §1.4 calls a valid Swap. A merely adjacent pair that produces no Match is
   not a legal move (§2 item 4), so the invariant is not "the board has at
   least one adjacent pair".
2. **A deadlocked board** is a **stable** board — §4.3's terminating state, with
   no Match anywhere and every cell filled (§1) — that contains **zero** legal
   match-producing adjacent swaps. A stable board on which the Player has no
   legal move at all is the condition this section removes.
3. **The invariant's scope is non-terminal boards only.** A board is *terminal*
   for this section when the battle has ended under `GAME_RULES.md` §1.4 —
   the Boss's HP or the active Pet's HP has reached 0 — whether that happened at
   the Boss terminal check (`GAME_RULES.md` §17 step 18, `BOSS_RULES.md` §5
   item 4), at the Player terminal check, or in a step 19a damage-over-time tick
   (`GAME_RULES.md` §17 step 19a). A terminal board is **exempt** (§1.6.4).
4. **Stability is an input, not the check.** The invariant is evaluated on the
   finished board of a Round, after every cascade of that Round has terminated
   (§4.3). A board in a transient intermediate state (§4.1) is never evaluated
   and is never committed (`GAME_STATE.md` §3).

### 1.6.2 The recovery boundary

1. **The boundary is the Round commit.** Recovery is evaluated at the end of a
   Round's resolution — after the Player action, after the Boss response phase
   where it runs (`GAME_RULES.md` §17 steps 18a–18d), after combat effects, and
   after the step 19a Status Effect pass — and **before** the Round's single
   write-back. The recovered board and the RNG state it consumed are part of the
   **same** state that write-back commits. Recovery therefore introduces no
   second write, no second Round, no intermediate persisted state, and no
   separate persistence mechanism (`GAME_STATE.md` §5.1, `REDIS_STATE.md` §4).
2. **Terminal first.** The terminal determination is made before the invariant
   is evaluated, and a terminal Round performs no recovery (§1.6.4).
3. **No unnecessary reshuffle.** When the finished board already contains at
   least one legal match-producing adjacent swap it is preserved exactly, and
   neither the board nor the RNG state moves. A playable board is never
   regenerated.
4. **Deadlock recovery.** When the finished board contains none, the board is
   regenerated (§1.6.3), and the regenerated board and the advanced RNG state
   replace the deadlocked pair in the Round's committed state.
5. **Recovery is not deferred to the Player's action.** The board is never left
   deadlocked for the Player to discover by attempting a Swap, and no Swap
   action is ever validated against a board that violates this section.

### 1.6.3 The recovery mechanism

1. **The mechanism is §1.2.1's generation, unchanged.** Recovery draws a
   replacement board with the documented deterministic row-major constrained
   random fill (§1.2.1) and accepts it only when it satisfies §1.3 and §1.4 — so
   the accepted board contains at least one legal match-producing adjacent swap
   by construction (§1.4). No separate repair pass, forced pattern, or partial
   fill exists: §1.5 item 3's "no fallback construction" applies to recovery
   exactly as it applies to initial generation.
2. **The mechanism is not Match Detection, Swap Validation, or Cascade
   Resolution.** It creates no Match, resolves no Swap, runs no pass of §4,
   and awards no Combo, Match, Power, Passive, or Relic
   progression. It writes the board and the RNG state and nothing else.
3. **One stream, bounded retries, deterministic outcome.** Recovery consumes the
   battle's existing server-seeded RNG stream (`GAME_STATE.md` §2.6.2,
   `ADR-009`, `ADR-026` §8) — never a system, unseeded, or second generator, and
   never a re-seed — and continues that same stream across rejected candidates
   as §1.5 item 1 requires. It is bounded by the **64** attempts of §1.5 item 2:
   the bound is not raised for recovery, and no unbounded retry loop is
   introduced. Because both the stream and the bound are fixed, an identical
   input board and RNG state always produce the identical replacement board and
   the identical resulting RNG state (§7.1).
4. **Exhaustion fails closed.** If no candidate satisfies §1.3 and §1.4 within
   the bound, recovery fails as §1.5 item 3 fails generation: the Round is not
   committed, and no partially recovered board and no advanced RNG state reach
   the store. §1.5 item 3's construction rules apply unchanged — there is no
   fallback board, no forced pattern, and no partial fill, and a board that
   violates §1.3 or §1.4 is never committed as the playable board. What differs
   is only *where* the failure lands: §1.5 item 3 fails a battle's creation,
   while recovery fails an already-created battle's Round, so the Round is
   refused rather than the battle. Recovery failure is a server failure, not a
   gameplay outcome: it emits no Battle Event and states no outcome.
5. **Nothing else is redistributed.**
   The fill draws from §1.1's four Gem types under §1.2.1.2's constraint. No
   Gem-type probability or distribution (§7.3) and no generator algorithm
   changes. Recovery replaces the board entirely rather than rebuilding around
   its contents, it applies to the Boss's pre-move recovery on the same terms
   (§1.6.5 item 4), and no compensation or carry-over is defined.

### 1.6.4 Terminal boards are exempt

1. Recovery is **never** performed on the board of a battle that has ended
   (§1.6.1 item 3). The outcome is already authoritative (`GAME_RULES.md` §1.4,
   `GAME_EVENTS.md` §2) and no Swap action is admitted against it (§2.1.4 item 7),
   so the invariant has no subject there.
2. A deadlocked terminal board is therefore committed as it is, and a
   generation failure can never invalidate an otherwise valid terminal result:
   recovery is not attempted for a terminal Round, so it cannot fail.
3. This is not an exception to the invariant. The invariant's own scope is
   "committed, non-terminal, and permits a Player action" (§1.6.1 item 3), and
   the exemption is that scope read literally.
4. **What the exemption does not decide.** It says nothing about a Card cast or a
   Pet Skill cast after the battle has ended: those are not board actions, they
   resolve no Swap and reach no cascade, and whether they are refused once the
   battle has ended is a gameplay rule this section does not own — it is owned by
   `CARD_RULES.md` §3 item 8, which refuses such a cast under the same established
   `STALE_ACTION` contract this section's §2.1.4 item 7 uses for the Swap. The one
   action whose post-terminal admission §2.1.4 item 7 fixes is the Swap.

### 1.6.5 This recovery and the Boss's pre-move recovery

1. **The Boss keeps its own pre-move recovery, unchanged.** Before the Boss
   selects its board action, a deadlocked board must be recovered for the Boss to
   have a legal move at all (`ADR-026` §8; `GAME_RULES.md` §17 step 18a is the
   step it runs inside, and that step's own text states the Boss's move selection
   and execution — the recovery is the operation `ADR-026` §8 owns). That is
   a separate, earlier evaluation of the same deadlock question and is retained.
2. **They cover different boards.** The Boss's pre-move recovery covers the board
   the **Player's** action and cascades left. This section's recovery covers the
   board the **Boss's** action and cascades left, immediately before the commit —
   a board the Boss's pre-move check has already passed. Neither alone covers
   both cases, which is why both exist.
3. **They do not double-recover.** The commit boundary evaluates the invariant on
   the finished board and regenerates only if that board is deadlocked, so a
   board the Boss's recovery already made playable is preserved exactly
   (§1.6.2 item 3). At most one recovery happens per board.
4. **Both consume the same single stream.** Neither introduces a second
   randomization mechanism (`ADR-009`).

### 1.6.6 Visibility — server-internal, no event

1. Recovery is a **server-internal** operation of the Round. It emits **no**
   Battle Event: there is no `BoardReshuffled` event, no reshuffle flag, and no
   payload member for it (`GAME_EVENTS.md` §3 item 8; TASK-265's approved
   Option A).
2. The recovered board reaches the client only as the Round's committed board —
   in the Round's single existing state push and in the existing
   `GetBattleState` snapshot (`SIGNALR_PROTOCOL.md` §4, §7; `ADR-008`) — which is
   the path every other committed board takes.
3. No hub method, argument, subscription, delivery path, or wire discriminator
   is added by this section, and §2.1.2's rejection code set is unchanged by it.

---

# 2. Swap

Player and Boss share one Match-3 board (ADR-026). Both sides act by executing
valid swaps on that board:

1. The acting actor selects two orthogonally adjacent Gems (up/down/left/right). Diagonal
   swaps are invalid.
2. The server simulates the swap on a copy of the board.
3. If the simulated swap produces at least one valid Match, the swap is committed.
4. If the simulated swap produces no Match, the board is reverted and no Turn,
   Match, Combo, Power, Passive or Relic progression occurs.
5. A committed Swap begins an action phase within the combat Round (GAME_RULES.md §2, ADR-026) and
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
4. **State precondition / staleness (§2.1.4, ADR-025).** The action carries the
   server-issued state version `expectedSequence` that the client observed. If
   `expectedSequence` does not equal `BattleState.Sequence`, the action was
   derived from an obsolete board/battle state and is rejected (`STALE_ACTION`).
   If the request is a duplicate delivery or retry of the already-committed
   action with matching action identity and payload coordinates, it returns
   the committed result idempotently without executing again. Coordinate
   history is not used to block legitimate swaps on subsequent turns.
5. **Match-producing (§2 item 3).** The swap is simulated on a copy of the
   board (§2 item 2) and must produce **at least one** §3 Match.
   "At least one" is the whole requirement, exactly as §1.4 defines it for
   initial-board validation: no minimum number of matches, no tier
   requirement, and no quality measure beyond this. A swap that produces no
   match is rejected (`NO_MATCH_FROM_SWAP`) and the board is reverted
   (§2 item 4).

Checks are evaluated in the order above so that two implementations reject the
same action with the same reason when more than one check could fail. That order
governs an action against a battle **still in progress**: §2.1.4 item 7 places one
precondition ahead of all five checks for a battle that has already ended, and
names the reason it produces, so a terminal battle's rejection does not depend on
which of these checks the action would also have failed.

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

Staleness and idempotency are governed by the dual state-precondition and
action-identity contract (`ADR-025`):

1. **State Version Precondition (`expectedSequence`).** Each Swap action carries
   the server-issued `expectedSequence` observed by the client. The server compares
   this against `BattleState.Sequence`. If `expectedSequence != state.Sequence`,
   the board has moved on since the client observed it; the action is rejected as
   `STALE_ACTION`.
2. **Action Identity and Idempotency (`actionId`).** Each Swap action carries a
   unique client-generated action identifier (`actionId`). When a request's
   `actionId` matches the battle's `LastCommittedSwap.ActionId`:
   - If the coordinate payload `{from, to}` matches the recorded swap, the
     server recognizes an idempotent retry (e.g. after a lost SignalR
     acknowledgement) and returns the committed result without re-executing or
     duplicating events.
   - If the payload differs, the request is rejected (`INVALID_ACTION_PAYLOAD`).

   The identity is compared with the **most recently committed Swap** and nothing
   else — the one record `GAME_STATE.md` §2.1.10 item 5 keeps — so the window in
   which a retry is recognized as a replay is one committed Swap wide. A retry
   that arrives after a later action has committed matches no recorded identity
   and is rejected by item 1 as `STALE_ACTION`: it is never re-executed and emits
   no event. That window is this rule's behavior, and widening it would be a
   state-shape decision (retaining an action history), not an interpretation of
   it.

   An action whose request states no `expectedSequence` — the legacy token form
   (`SIGNALR_PROTOCOL.md` §2 item 1) — makes no claim about the state it observed,
   so it is resolved only against the state read for it. If a concurrent writer
   moves that state before its write-back, the action is rejected as
   `STALE_ACTION` rather than re-resolved against the newer state
   (`REDIS_STATE.md` §4 item 2). Not stating the precondition is a limitation of
   that form and never a way around item 1.
3. **Legitimate Consecutive-Turn Coordinate Reuse.** Swapping the same unordered
   pair `{from, to}` on consecutive turns (e.g. Turn N and Turn N+1) is fully
   valid whenever the new board state produced by cascades and spawns permits a
   valid match at those coordinates. No coordinate-pair lock is retained across
   turns.
4. **No in-flight window is observable.** The server resolves actions sequentially
   per battle (`SIGNALR_PROTOCOL.md` §6 item 1). Concurrent requests based on the
   same observed state cannot both commit: the first committed action advances
   `Sequence`, causing any subsequent concurrent action with the older
   `expectedSequence` to be rejected as `STALE_ACTION`.
5. `STALE_ACTION` indicates that the action's state precondition was violated
   because the authoritative state advanced before the action committed.
6. The rejection is deterministic: the same board and the same action always
   produce the same outcome.
7. **A terminal battle admits no Swap action.** A battle that has ended
   (`GAME_RULES.md` §1.4: the Boss's HP or the active Pet's HP has reached 0)
   permits no further Swap. An action that reaches the server against such a
   battle is rejected **at the action boundary**, before any Swap is resolved
   and before any RNG is consumed, under this section's existing
   state-precondition rejection, `STALE_ACTION` (item 5): the action's
   precondition is that the battle is still in progress, and a terminal state
   satisfies no precondition of a resolution. No additional rejection code is
   introduced for it and §2.1.2's check set is unchanged.
   - The guard is evaluated **after** the idempotency recognition of item 2 and
     before any resolution, so a retry of the very action that *produced* the
     terminal state is still answered as an idempotent replay rather than
     rejected, and a retry is never re-executed (item 2).
   - Because the guard precedes §2.1.2's checks, an action against a terminal
     battle is rejected as `STALE_ACTION` even when it would also have failed an
     earlier §2.1.2 check. That is this section's order for a battle that is no
     longer in progress; it does not reorder §2.1.2 for a live battle.

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
   Power, Passive, or Relic progression occurs, and
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
2. Only detected shapes are Matches; non-match board clears do not exist in gameplay (OD-1, OD-2).
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
3. Special Gems do not exist in gameplay (OD-1, OD-2; see §5). No board objects
   are activated or created during a Cascade.
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
2. Gravity            each column's remaining Gems fall to fill empty cells
                      below them
3. Spawn              draw new Gems into the now-empty cells at the top of
                      each column
4. Re-detect          run Match Detection (§3) on the completed board
```

1. **Match Resolution consumes every cell in the pass's match set** (the set
   union of cells from detected shapes). Overlapping or multi-shape cells are
   removed and counted once per pass (§3.3 item 3).
2. **Gravity runs immediately on the board after matched cells are removed;**
   Gems fall downward within their column (§4.4).
3. **Spawn fills now-empty cells at the top of each column** (§4.5).
4. **Re-detect runs Match Detection (§3) on the resulting board.** If new
   matches are detected, the cascade loop continues (§4.2).
5. The step order is fixed and may not be reordered. `GAME_RULES.md` §17's
   logical order ("Resolve Board" → "Detect Match" → "Remove Matched Gems" →
   "Apply Gravity" → "Spawn Gems" → "Detect Cascade") is this loop, expressed
   once per iteration.

## 4.2 The Cascade Loop

```text
Detection pass 1   (board after the committed Swap)     → depth 1
        ↓  remove / gravity / spawn
Detection pass 2   (board after pass 1's Gravity+Spawn)  → depth 2, a Cascade
        ↓  remove / gravity / spawn
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
   same rules (§3, §4.1) and counted identically (§6.2, §3.4 item 1).

## 4.3 When the Cascade Ends

1. The loop ends when a detection pass produces **no matches** on the fully
   resolved board – that is, after that pass's board (post-Gravity, post-Spawn,
   fully populated) contains no §3 shape anywhere.
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
7. **Gravity produces no Match, no Combo, and emits no event of its own.** It only
   relocates Gems; the board is re-examined by the next detection pass (§4.1 step
   5). What a pass's Gravity and Spawn did is **reported after both have
   completed**, as that pass's own closing report in the round's existing single
   event batch (`GAME_EVENTS.md` §1.1 item 8, §2 `CascadeStepResolved`;
   `SIGNALR_PROTOCOL.md` §3.2.28) — Gravity itself still produces and emits
   nothing, and no event carries an intermediate board (`GAME_STATE.md` §3).
8. **No Special Gems (OD-1, OD-2).** Gravity moves only ordinary Gems of the
   four §1.1 types. Special Gems do not exist on the board.

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
7. **Only basic gems spawn.** Every spawned Gem is an ordinary Gem of one
   of the four §1.1 types. Special Gems do not exist in gameplay (OD-1, OD-2).
8. **Determinism of the whole step.** For a given board-after-Gravity and a
   given `RngState`, the spawn step produces exactly one board and one
   resulting `RngState` (§7.1).

## 4.6 The Loop Is Deterministic End to End

1. Given the same starting board, the same committed Swap, and the same
   `RngState`, the whole §4 loop – every pass, every removal, every fall,
   every spawn, and the final board and `RngState` – is fully determined
   (§7.1).
2. Nothing in the loop depends on iteration order of an unordered collection,
   on timing, on allocation, or on any second source of randomness (§7.2).
3. The only operation in the loop that consumes randomness is Spawn
   (§4.5 item 4). Everything else in §3—§4 is a pure function of the board
   state.

# 5. Special Gems (Retired)

> **Retirement Notice (OD-1–OD-7 / TASK-287):** Special Gems are removed entirely
> from gameplay. No Match creates a board object; no creation, activation,
> chain activation, area-of-effect clearing, explosion, or detonation mechanic
> exists; and no cell is ever cleared except as a member of a detected Match.
>
> **Historical Record:** This section formerly contained §5.1–§5.6, §5.8, and
> §5.9 (including §5.1.1, §5.2 Line Clear Gem, §5.3 Burst Gem, §5.4 Area Gem,
> §5.5 activation/creation/placement/collision, §5.6 summary table, §5.8
> clipping/overlap/activation order/chains/interaction matrix, and §5.9
> resolution audit). Those rules were newly authored under `GAME_RULES.md` §20 to
> resolve the recovery gap left by surviving artifacts. Under operator
> decisions OD-1…OD-7, all Special Gem mechanics are retired in full. Match 4,
> Match 5, and L/T patterns remain valid matches, detected and resolved under
> §3 and §4, consuming only their matched cells with no special board objects
> or tier multipliers. Section numbering is preserved without renumbering;
> resource generation hooks are retained at §5.7 below.

## 5.7 Resource Generation Hooks

Exact numeric formulas for Power/HP/ATK/DEF resource output per Gem type are
defined in `COMBAT_RULES.md` §2 and `GAME_RULES.md` §12 (Power Rules). This
document defines only *when* generation is triggered:

```text
Match 3   → base resource generation (BaseOutput per matched cell, COMBAT_RULES.md §2)
Match 4   → base resource generation (BaseOutput per matched cell, COMBAT_RULES.md §2)
Match 5   → base resource generation (BaseOutput per matched cell, COMBAT_RULES.md §2)
L/T       → base resource generation (BaseOutput per matched cell, COMBAT_RULES.md §2)
```

1. **Generation rates are owned by `COMBAT_RULES.md` §2.** Match-tier multipliers
   are retired (OD-3). Every valid match contributes `BaseOutput(t)` per unique
   matched cell, without tier multipliers or special detonation rates.
2. **Generation is triggered by matched Gems**, once per cell, over the union of
   cells removed in that detection pass (§3.3 item 3) — a cell shared by more
   than one shape generates once.
3. **No Special Gems exist (OD-1, OD-2).** Former rules governing Special Gem
   creation and activation generation are retired.
4. **Resource generation is a downstream consequence of Match resolution.**
   Generation is not a Match and is not counted as one (`GAME_RULES.md` §3).
5. **Generation reads the pass's unique matched cells.** It is owned — like
   Power, Passive charge, and Relics — by its own domain document
   (`COMBAT_RULES.md`, `GAME_RULES.md` §17 steps 10—13). This section owns only
   *when* board events trigger it; it defines no rates or damage formulas.
6. **No tier multiplier is applied (OD-3).** All detected shapes (Match 3,
   Match 4, Match 5, L/T) contribute at the documented per-Gem `BaseOutput`
   without tier multipliers. Larger matches yield more resources solely because
   they consume more unique cells.
7. **Cascade-generated Matches are counted and generated independently.** Every
   Match detected in a Cascade pass contributes resources under the same rules
   as the Swap's first pass (§4 item 4, §4.2 item 5). Cascade is not a
   multiplier and does not aggregate passes: each pass's unique matched cells
   generate once at `BaseOutput`, and the action's total is the sum across
   passes. No "cascade bonus" multiplier exists.
8. **Downstream Combat Contribution Contract.** How generated resources feed damage, shields,
   healing, Power, and `ADR-026` actor gating is owned by `COMBAT_RULES.md` §2 item 6. This item
   owns only the board-side facts that trigger generation, and states them as follows.
   - **Generation is triggered by Matches only.** A cell enters an actor's generation set only as a
     member of a Match that the actor's own resolution detected. No Special Gem, activation, chain,
     area-of-effect, explosion, or detonation clear exists, so there is no second trigger and no
     non-Match source of cleared cells.
   - **Each unique matched cell is counted once**, over the union of a pass's matched cells — a cell
     shared by two shapes of the same pass is one cell and is counted once (§3.3 item 3, §5.8.2),
     and it is never counted again by a later pass.
   - **Every cascade Match contributes again**, under the same rule: each detection pass's matched
     cells are counted once in that pass, and the action's total is the sum over its passes (§5.7
     item 7). No "cascade bonus" and no cross-pass multiplier exists.
   - **No match tier is applied.** There is no Match 3 / Match 4 / Match 5 / L-T generation
     multiplier; a larger Match earns more only because it consumes more cells. The former
     `MatchTier.Base` notion and the "activation cells generate at the base rate" rule are retired
     with the activation mechanic that needed them.
   - **Attack qualification is not this section's rule.** Whether the generated resources produce an
     attack is decided on Match evidence — a confirmed valid ATK-gem match — and is owned by
     `COMBAT_RULES.md` §2 item 6 and `ADR-026` D1. A cleared-cell count is a reporting quantity and
     never the eligibility predicate.
   - **Disclosed inconsistency pending the join commit.** §5.1–§5.9, §5.5.5, §5.7 items 3 and 6, and
     §6.3.1 still describe the retired Special Gem model. Their retirement is owned by TASK-287 in
     the same join group commit as this item (`TASK_LIFECYCLE.md` §6.1 item 5). Until that slice
     lands, this document is not self-consistent and must not be committed alone.


## 5.8 Boundaries, Overlap and Determinism (Retired)

*(Retired per OD-1, OD-2 / TASK-287: Area-of-effect clipping, Special Gem overlap, activation ordering, and interaction matrices are retired with the Special Gem mechanics they governed. Cell overlap for ordinary matches is governed by §3.3 item 3 and §5.7 item 2.)*

## 5.9 Resolution Audit (Retired)

*(Retired per OD-1, OD-2 / TASK-287: The Special Gem resolution audit and recovery history are retired with the underlying Special Gem mechanics.)*

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
4. Non-match events increment nothing. (Special Gem effects are retired per OD-1/OD-2; only confirmed valid Matches increment Combo.)

## 6.3 Cascade Increment

1. Every Match found in a Cascade pass increments Combo exactly as a Match of
   the Swap's first pass does (§4 item 4). Combo is therefore equal to the
   running total of Matches detected in the Swap so far, across all passes.
2. Only confirmed valid Matches increment Combo. Secondary clears and Special
   Gem activations/creations do not exist in gameplay (OD-1, OD-2).

### 6.3.1 Special Gem Activation and Combo (Retired)

*(Retired per OD-1, OD-2 / TASK-287: Special Gem activation, chain activation, and creation do not exist in gameplay. Combo is incremented strictly by detected valid Matches.)*

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
   resolution must be fully deterministic **except** for
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
  runtime deadlock recovery (§1.6.3 item 3)        §1.2.1.4's selections per
                                                    attempt, up to §1.5 item 2's
                                                    bound, on the retained stream
```

1. These are the only board operations that consume randomness — the list above is
   exhaustive, and each entry names the section that owns its consumption. Anything
   else that consumes randomness on the board would be a rule change and must go
   through `GAME_RULES.md` §20 before implementation. The third entry is
   `TASK-267`'s approved authorization (§1.6, decisions D1–D4) of the operation
   `ADR-026` §8 had already introduced for the Boss's pre-move recovery: both are
   the same mechanism — `BoardGenerator.Generate` over the battle's retained
   stream, bounded by §1.5 item 2 — evaluated at two different points (`§1.6.5`).
2. The following **must not** consume RNG. Each is a pure function of the board
   state and the resolution's inputs:

```text
Swap validation, including the simulated swap and its match check  §2.1.2
match detection and the match-set order                            §3
match resolution and cell removal                                  §4.1
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

1. **A Turn is an atomic combat Round** (`GAME_RULES.md` §2 item 1, `ADR-026`),
   encompassing the Player action phase and the Boss response phase. It advances
   by exactly 1 upon the commit of the atomic round (§2.1.6 step 9, `GAME_STATE.md` §5.1) —
   after both phases complete, never before.
2. **One committed Round advances Turn by exactly 1** (§2 item 5, `ADR-026`). The Round's whole
   resolution – Player resolution, Boss resolution, every Cascade,
   and the entire §4 loop for both actors – belongs to that one atomic Round.
3. **A rejected Swap begins no Turn** (§2.1.5 item 2). Out-of-range, non-
   adjacent, already-applied, and no-match-producing actions all leave `Turn`
   unchanged.
4. **Nothing else on the board begins a Turn.** Board generation is not a
   Swap/Action (`GAME_STATE.md` §2.0.5.2 item 1); Match Detection, Match
   Resolution, Gravity, Spawn, and Combo
   calculation are steps *within* a Turn and never begin one.
5. **A Card cast begins no Turn** (`CARD_RULES.md` §3 item 5). The Turn rule
   applies to the Swap/Action that starts it; Card casts are independent of the
   Match-3 Turn/Combo system and are not board resolution.
6. **A Card cast is nonetheless bounded to one per committed Match-3 Turn.**
   `CARD_RULES.md` §3 item 6 owns the rule: at most one successful Card cast may
   occur during a committed Match-3 Turn. It is a **cast-count constraint, not
   Turn consumption** — it does not contradict item 5, because the cast still
   begins no Turn. The lifecycle is:

   ```text
   New committed Match-3 Turn      (§8.1 item 1 — a committed Swap)
        ↓
   Card cast available
        ↓
   Card successfully cast          (auxiliary action — no Turn, no board change)
        ↓
   Card cast unavailable for the remainder of that Turn
        ↓
   Match-3 Turn commits, Turn++    (§8.3), boss response
        ↓
   Next Turn — Card cast available again
   ```

   A rejected Swap begins no Turn (§2.1.5 item 2), so it neither consumes nor
   restores the allowance. The allowance is per **committed** Turn: it is not
   restored by a rejection and not consumed by one.
7. This document fixes the *order* in which the counter changes relative to the
   resolution (§8.3). `GAME_RULES.md` §2 owns what a Turn is,
   `GAME_STATE.md` §2.0.2 owns the counter's stored representation and initial
   value, and `GAME_STATE.md` §5.1 owns the state write-back that persists it.

## 8.2 When Sequence Changes

1. **One successfully resolved atomic round increments `Sequence` by exactly 1** – a
   committed round write-back is one such action (`GAME_STATE.md` §5, `ADR-026`). It is not one
   increment per Match, per pass, per Cascade, per removed
   cell, or per increment of anything else. Monotonic `Sequence` advancement provides
   authoritative state versioning in Redis; it does not guarantee gap-free client
   event delivery, which is governed by `ADR-008` snapshot recovery.
2. **Board resolution substeps do not increment `Sequence`.** The §4 loop's
   Gravity, Spawn, detection passes, and
   Combo changes are *inside* one resolution and increment nothing. `Sequence`
   is updated once, at the end of the whole resolution (§8.3).
3. **A rejected Swap increments nothing** (§2.1.5 item 3).
4. **A card cast increments nothing on the board.** Non-Swap actions are not
   board resolution; their own `Sequence` treatment is owned by the task that
   implements them and by `GAME_STATE.md` §5, which states the resolution
   counter rule they must satisfy. A card cast is additionally bounded to one
   per committed Match-3 Turn (§8.1 item 6, `CARD_RULES.md` §3 item 6) — a
   cast-count constraint that changes nothing about `Sequence`, `Turn`, or the
   board.
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
| Gravity | in effect | unchanged | unchanged |
| Spawn | in effect | unchanged | **advances** (one selection per spawned cell) |
| Combo calculation | in effect | unchanged | unchanged |
| Cascade termination | in effect | unchanged | unchanged |
| End of the whole resolution | in effect (still this Turn) | `+1`, once | retained as produced |

"In effect" means the Turn the committed Swap began is the Turn the step belongs
to; the step neither starts a Turn nor ends one.
