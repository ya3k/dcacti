---
name: phaser-match3
description: "Use this skill when implementing the 8x8 Match-3 board presentation, gem visual lifecycle, coordinate mapping, selection feedback, and visual cascade animations in Phaser 4 for DCacti. Does NOT calculate matches or damage."
---

# skills/client/phaser-match3.md — Skill: DCacti Phaser Match-3 Presentation

**Version:** 1.0  
**Status:** Binding  
**Scope:** Visual presentation and user interaction for the 8x8 Match-3 gameplay board in Phaser 4.

> Governs the presentation layer of DCacti's 8x8 Match-3 board, including row-major cell mapping, gem sprites, drag/selection interaction feedback, animation locks, and visual cascade falls.

---

## Purpose

Provide precise visual presentation standards for the Match-3 board in Phaser 4:
1. Render an 8x8 grid (64 logical cells, row-major index 0..63).
2. Convert between grid coordinates `(row, col)` and Phaser screen positions `(x, y)`.
3. Handle player pointer input (tap, drag, swap intent) with optimistic visual cues.
4. Animate gem matches, special gem spawns, and cascade drops driven exclusively by server events.
5. Strictly exclude authoritative match detection, scoring, cascade logic, or damage calculation.

---

## When to Use

- Building or updating `BoardView.ts` or gem sprite presentation components.
- Implementing drag/tap swap interactions on the Phaser canvas.
- Calculating screen pixel positions for grid cells within the 1280x720 viewport.
- Playing gem destruction, special gem transformation, or drop/spawn animations from server `ReceiveEvents`.
- Managing visual animation locks while board animations are playing.

---

## When Not to Use

- Detecting authoritative 3-in-a-row matches or cascade loops (server Domain logic).
- Computing elemental damage, combo multipliers, or mana gains (server Combat logic).
- Generating random gem spawns (server RNG only).
- Generic Phaser tweening syntax (use upstream `phaser/tweens` or `phaser/input-keyboard-mouse-touch`).

---

## Authoritative Sources

- `docs/01-game-design/GAME_RULES.md` §1–§8 — Board specifications and match rules
- `docs/01-game-design/MATCH3_RULES.md` §1–§7 — Match-3 mechanics and special gem concepts
- `docs/02-technical/GAME_STATE.md` §2.2 — BoardState contract and cell indexing
- `docs/02-technical/GAME_EVENTS.md` §2 — Board events (`GemMatched`, `GemsDropped`, `BoardUpdated`, `SpecialGemCreated`)
- `docs/03-decisions/ADR/ADR-001*` — Server authority

---

## Board Specifications & Coordinate Math

```text
Dimensions:        8 columns x 8 rows = 64 cells
Cell Indexing:     Row-Major: index = (row * 8) + col  (range 0..63)
                   row = Math.floor(index / 8), col = index % 8
Logical Board Box: 1280x720 canvas layout
Origin (0,0):      Top-Left of the board is cell (row: 0, col: 0, index: 0)
```

### Coordinate Transforms

```ts
// Example: Converting cell (row, col) to screen (x, y)
const BOARD_ORIGIN_X = 240; // logical X offset in 1280x720 viewport
const BOARD_ORIGIN_Y = 120; // logical Y offset
const CELL_SIZE = 64;       // pixel width/height per cell

function cellToScreen(row: number, col: number): { x: number; y: number } {
    return {
        x: BOARD_ORIGIN_X + col * CELL_SIZE + CELL_SIZE / 2,
        y: BOARD_ORIGIN_Y + row * CELL_SIZE + CELL_SIZE / 2
    };
}

function screenToCell(x: number, y: number): { row: number; col: number } | null {
    const col = Math.floor((x - BOARD_ORIGIN_X) / CELL_SIZE);
    const row = Math.floor((y - BOARD_ORIGIN_Y) / CELL_SIZE);
    if (row >= 0 && row < 8 && col >= 0 && col < 8) {
        return { row, col };
    }
    return null;
}
```

---

## Input & Swap Presentation Flow

```text
1. Player Pointer Down on Gem A
   └── Highlight Gem A (scale up 1.1x, glow or outline)
2. Player Drags or Taps adjacent Gem B
   └── Validate adjacency: Math.abs(r1-r2) + Math.abs(c1-c2) === 1
3. Visual Optimistic Swap:
   ├── Tween Gem A ↔ Gem B positions (e.g. 150ms)
   ├── Lock user input on the board
   └── Send SwapAction request to server via GameRuntimePort
4. Server Response / Events:
   ├── IF Invalid Swap: Tween Gem A ↔ Gem B back (revert animation) and unlock input
   └── IF Valid Swap: Play match animations, cascade falls, special gem spawns
```

---

## Event-Driven Visual Lifecycle

The board state renders sequentially based on incoming server event batches:

1. **Gem Swapped Event (`GemSwapped`):** Animate swap between two cells.
2. **Gem Matched Event (`GemMatched`):** Animate destruction/fade/scale-down of matched cells, emit particle burst (`phaser/particles`).
3. **Special Gem Created Event (`SpecialGemCreated`):** Transform cell sprite into special gem graphic (Bomb, Line, Rainbow).
4. **Gems Dropped Event (`GemsDropped` / `BoardUpdated`):** Animate falling gems to new positions with bouncy ease (`Phaser.Math.Easing.Bounce.Out` or `Back.Out`), spawn new gems from top of board.
5. **Turn / Cascade Complete:** Unlock player input once all tweens finish.

---

## Do / Don't

| Do | Don't |
|---|---|
| Map cells using 0..63 row-major indexing. | Use 1-indexed or column-major cell indices. |
| Lock player board input while cascade animations are actively playing. | Allow players to spam swaps during visual cascade playback. |
| Revert swap visually if the server rejects the swap request. | Assume local swap is valid and destroy gems before server confirmation. |
| Animate gem falls based on server `ReceiveEvents` payload. | Calculate match combinations, cascade gravity, or RNG gems on the client. |

---

## Stop Conditions

- Task asks to implement server match detection or cascade resolution inside Phaser.
- Board coordinate indexing diverges from `GAME_STATE.md` (8x8, 0..63 row-major).
- Server event definitions for board actions are missing or ambiguous.

---

## Traceability

- **Reads:** `docs/01-game-design/MATCH3_RULES.md`, `docs/02-technical/GAME_STATE.md`, `docs/02-technical/GAME_EVENTS.md`
- **Used by:** `client` agent, `orchestrator` agent
- **Related Skills:** `client/phaser-battle-presentation`, `client/client-event-projection`, `client/client-state-authority`, `phaser/tweens`, `phaser/input-keyboard-mouse-touch`
