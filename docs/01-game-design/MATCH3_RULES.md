# Match-3 Rules

**Version:** 1.0
**Status:** MVP Domain Rule
**Parent:** GAME_RULES.md

This document defines the exact Match-3 board behavior. It expands GAME_RULES.md
sections 3, 4 and 7 ("Match Rules", "Cascade Rules", "Special Gem Rules"). If anything
here conflicts with GAME_RULES.md, GAME_RULES.md wins and the conflict must be reported
(see GAME_RULES.md §20).

---

# 1. Board

```text
Size:      8 × 8 (64 cells)
Indexing:  row-major, 0–63, (0,0) = top-left
```

The board holds exactly one Gem per cell at all times except during the
Remove → Gravity → Spawn sub-steps of resolution.

## 1.1 Gem Types (MVP)

```text
ATK
DEF
HP
POWER
```

Gems are functional only. Gems carry no Element (see ELEMENT_RULES.md §1).

## 1.2 Board Generation

1. Initial board is filled randomly from the 4 Gem types.
2. Initial board must not contain a pre-existing Match.
3. Initial board must contain at least one valid Swap (no dead boards).
4. Random distribution weights are configuration, default uniform (25% each).

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
2. Spawned Gems use the same distribution as §1.2 unless a Relic/effect overrides it.
3. Special Gems (see §5) triggered during a Cascade resolve their effect
   immediately when removed, before the next Gravity/Spawn step.
4. Every Match found during a Cascade iteration increases Combo (GAME_RULES.md §5)
   and total Match count (GAME_RULES.md §3), identically to the first Match of
   the Swap.
5. There is no hard cap on Cascade depth in MVP; the loop ends naturally when the
   board stabilizes. Implementations must guard against non-terminating boards
   (e.g. a spawn table that can regenerate infinite matches) but this is an
   implementation safeguard, not a gameplay rule.

---

# 5. Special Gems

## 5.1 Match 3

No Special Gem is created. Standard resource generation only
(see COMBAT_RULES.md / GAME_RULES.md §12 for Power, and PASSIVE_RULES.md for
Passive charge).

## 5.2 Match 4

1. Creates one Special Gem at the position of the swapped Gem (or, for a Cascade
   match with no swap origin, the geometric center of the matched line).
2. Match 4 Special Gem type: **Line Clear Gem** — when later matched or detonated,
   clears the remainder of its row or column (orientation fixed by the matched
   line's orientation).
3. Resource generation for the consumed Gems is increased relative to Match 3
   (exact multiplier is configuration; see COMBAT_RULES.md).

## 5.3 Match 5

1. Creates one Special Gem at the swap origin (or line center for Cascade matches).
2. Match 5 Special Gem type: **Burst Gem** — when later matched or detonated,
   clears all Gems in a 3×3 area centered on its position.
3. A straight run of 6+ identical Gems in one line is treated as a Match 5 for
   Special Gem purposes (Burst Gem), not a stronger separate tier — there is no
   Match-6+ tier in MVP.
4. Resource generation is higher than Match 4 (configuration).

## 5.4 L / T Matches

1. An L or T shape (two intersecting lines of 3+, sharing one Gem) creates one
   Special Gem at the intersection cell.
2. L/T Special Gem type: **Area Gem** — when later matched or detonated, clears a
   fixed cross/plus-shaped area around its position (exact shape: configuration,
   default = the 4 orthogonally adjacent cells plus itself).
3. If an L/T shape's arms are individually long enough to separately qualify as
   Match 4 or Match 5, the higher-tier Special Gem rule (§5.2/§5.3) takes
   precedence over the L/T rule for that arm; the intersection still produces
   one Area Gem for the L/T pattern itself. Both Special Gems are created.

## 5.5 Special Gem Activation

1. A Special Gem's clear effect triggers when it is consumed as part of any
   Match (its own color matched normally) or when an explicit effect
   (Card/Relic/Boss) detonates it directly.
2. Gems cleared by a Special Gem's effect are NOT counted as a "Match" for
   Combo/Match-count purposes — they are an explosion, not a match — but they DO
   count toward resource generation and DO trigger Cascade re-evaluation
   (gravity + spawn + re-detect), per §4.
3. Special Gem chain reactions (a cleared Special Gem detonating another Special
   Gem) are allowed and resolve sequentially within the same Cascade step.

---

# 6. Resource Generation Hooks

Exact numeric formulas for Power/HP/ATK/DEF resource output per Gem type and per
match tier are defined in COMBAT_RULES.md and GAME_RULES.md §12 (Power Rules).
This document defines only *when* generation is triggered:

```text
Match 3   → base resource generation
Match 4   → increased resource generation + Line Clear Gem
Match 5   → higher resource generation + Burst Gem
L/T       → base+ resource generation + Area Gem
Special Gem detonation → resource generation per Gem cleared (base rate)
```

---

# 7. Determinism

1. Given the same board state and the same Swap input, match detection, cascade
   resolution and special gem creation must be fully deterministic **except** for
   the random Gem values spawned into empty cells, which use a server-seeded RNG.
2. The server is authoritative for the RNG seed and all resulting board states,
   per GAME_RULES.md §18.
