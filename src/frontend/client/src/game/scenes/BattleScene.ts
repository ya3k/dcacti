import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import type { GameRuntime } from '../runtime/GameRuntime';
import type { GameRuntimeState } from '../../state/GameRuntimeState';
import type { RuntimeBattleState, RuntimeBoard } from '../runtime/GameRuntimeEvents';

/**
 * Board presentation geometry, in logical game pixels.
 *
 * This is the *presentation* conversion of the authoritative row-major index to
 * screen coordinates — the concern `ARCHITECTURE.md` §2.2.2 reserves for the
 * client. It introduces no competing board coordinate system: the board's only
 * coordinate convention remains `index = row * 8 + column`
 * (`MATCH3_RULES.md` §1.0), and these constants only decide how large a cell is
 * drawn.
 *
 * The board's logical size is fixed at 8 x 8 (`MATCH3_RULES.md` §1.0) and is
 * never derived from the viewport: responsive behavior scales the presentation
 * (`GameViewport.ts`, `ARCHITECTURE.md` §2.2.2 rule 6) and must never change the
 * cell count.
 */
const BOARD_COLUMNS = 8;
const BOARD_ROWS = 8;
const CELL_SIZE = 56;
const CELL_GAP = 6;
const BOARD_WIDTH = BOARD_COLUMNS * CELL_SIZE + (BOARD_COLUMNS - 1) * CELL_GAP;
const BOARD_ORIGIN_X = (GAME_WIDTH - BOARD_WIDTH) / 2;
const BOARD_ORIGIN_Y = SAFE_AREA.y + 96;

/**
 * Placeholder presentation of the four Gem types (`MATCH3_RULES.md` §1.1:
 * `ATK`, `DEF`, `HP`, `POWER`).
 *
 * Keys are the documented contract names the server sends; the value is a
 * placeholder fill colour and short label. This is display only — it assigns no
 * gameplay meaning, and an unrecognised name is rendered as an inert
 * placeholder rather than being guessed at.
 */
const GEM_PRESENTATION: Readonly<Record<string, { readonly color: number; readonly label: string }>> = {
  ATK: { color: 0xef4444, label: 'ATK' },
  DEF: { color: 0x3b82f6, label: 'DEF' },
  HP: { color: 0x22c55e, label: 'HP' },
  POWER: { color: 0xa855f7, label: 'PWR' },
};

/**
 * BattleScene — the battle presentation runtime.
 *
 * It presents the Board Foundation State the server pushed
 * (`BattleStateUpdated`, SIGNALR_PROTOCOL.md §4) as an 8 x 8 board of 64 cells:
 *
 *   BattleStateUpdated → SignalRService → GameRuntime → BattleScene → 8x8 board
 *
 * The scene reads the runtime through the `GameRuntime` port and never imports
 * SignalR, `HubConnection`, or any transport type (ARCHITECTURE.md §2.2 rule 3).
 *
 * The board it draws is the server's authoritative `Cells[64]`, presented
 * verbatim. The scene computes no board content of its own: it does not
 * generate, fill, repair, validate, or re-derive the board, and it contains no
 * client-side randomness (SIGNALR_PROTOCOL.md §4 item 10, `GAME_RULES.md` §18,
 * ADR-001). Its only transformation is the documented index → screen mapping.
 *
 * This is not gameplay: there is no swap interaction, match detection, cascade,
 * gravity, combo, or combat presentation here. Those are owned by the future
 * resolution task.
 *
 * It is created with the `GameRuntime` as scene data by `GameConfig`. When no
 * runtime is supplied it still runs, reporting that the runtime is unavailable,
 * so scene lifecycle never depends on runtime availability.
 */
export class BattleScene extends Phaser.Scene {
  private runtime: GameRuntime | null = null;
  private runtimeUnsubscribe: (() => void) | null = null;
  private battleStateUnsubscribe: (() => void) | null = null;
  private statusText: Phaser.GameObjects.Text | null = null;
  private detailText: Phaser.GameObjects.Text | null = null;
  private battleText: Phaser.GameObjects.Text | null = null;
  private boardText: Phaser.GameObjects.Text | null = null;
  /** The drawn board cells, cleared and redrawn on each state push. */
  private boardLayer: Phaser.GameObjects.Container | null = null;

  constructor() {
    super('BattleScene');
  }

  create(): void {
    this.runtime = readRuntime(this);
    this.drawRuntimeShell();

    // Reflect current runtime state immediately, then follow transitions.
    this.renderRuntimeState(this.runtime?.getState() ?? null);
    this.renderBattleState(this.runtime?.getBattleState() ?? null);

    if (this.runtime) {
      this.runtimeUnsubscribe = this.runtime.onRuntimeEvent((event) => {
        this.renderRuntimeState(event.state);
      });
      this.battleStateUnsubscribe = this.runtime.onBattleState((state) => {
        this.renderBattleState(state);
      });
    }
  }

  /**
   * Scene shutdown — reached when this scene stops or the game is destroyed.
   * Detaching here prevents listener leaks across scene restarts.
   */
  shutdown(): void {
    this.runtimeUnsubscribe?.();
    this.runtimeUnsubscribe = null;
    this.battleStateUnsubscribe?.();
    this.battleStateUnsubscribe = null;
    this.statusText = null;
    this.detailText = null;
    this.battleText = null;
    this.boardText = null;
    this.boardLayer?.destroy(true);
    this.boardLayer = null;
  }

  private drawRuntimeShell(): void {
    this.add
      .rectangle(
        SAFE_AREA.x + SAFE_AREA.width / 2,
        SAFE_AREA.y + SAFE_AREA.height / 2,
        SAFE_AREA.width,
        SAFE_AREA.height,
        0x0f172a
      )
      .setStrokeStyle(2, 0x334155);

    this.add
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 28, 'BATTLE SCENE', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '24px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    this.statusText = this.add
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 60, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '16px',
        color: '#60a5fa',
      })
      .setOrigin(0.5);

    this.detailText = this.add
      .text(SAFE_AREA.x + 12, SAFE_AREA.y + SAFE_AREA.height - 58, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '13px',
        color: '#94a3b8',
      })
      .setOrigin(0, 0.5);

    this.battleText = this.add
      .text(SAFE_AREA.x + 12, SAFE_AREA.y + SAFE_AREA.height - 24, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '13px',
        color: '#64748b',
      })
      .setOrigin(0, 0.5);

    // Board caption and the layer the 64 cells are drawn into.
    this.boardText = this.add
      .text(BOARD_ORIGIN_X, BOARD_ORIGIN_Y - 22, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '13px',
        color: '#64748b',
      })
      .setOrigin(0, 0.5);

    this.boardLayer = this.add.container(0, 0);
  }

  /**
   * Renders technical runtime status. This is a diagnostic readout proving the
   * runtime exists — it is not a gameplay HUD.
   */
  private renderRuntimeState(state: GameRuntimeState | null): void {
    if (!this.statusText || !this.detailText) {
      return;
    }

    if (state === null) {
      this.statusText.setText('Runtime Unavailable');
      this.statusText.setColor('#f87171');
      this.detailText.setText('No GameRuntime was supplied to the scene.');
      return;
    }

    const { ready, label } = describeRuntime(state);

    this.statusText.setText(label);
    this.statusText.setColor(ready ? '#34d399' : '#fbbf24');
    this.detailText.setText(
      [
        `SignalR: ${state.connection}`,
        `Sync:    ${state.sync}`,
        state.connectionId ? `Conn:    ${state.connectionId}` : null,
      ]
        .filter((line): line is string => line !== null)
        .join('\n')
    );
  }

  /**
   * Presents the synchronized authoritative state the server pushed
   * (`BattleStateUpdated`, SIGNALR_PROTOCOL.md §4) — a development readout of
   * the Board Foundation State plus the 8 x 8 board.
   *
   * It presents the values verbatim. The scene computes nothing, adjusts
   * nothing, and owns nothing: these are server-authoritative fields
   * (GAME_STATE.md §2.0.5.4, ADR-001).
   */
  private renderBattleState(state: RuntimeBattleState | null): void {
    if (!this.battleText) {
      return;
    }

    if (state === null) {
      this.battleText.setText('');
      this.renderBoard(null);
      return;
    }

    this.battleText.setText(
      [
        `BattleId: ${state.battleId}`,
        `Turn: ${state.turn}  Sequence: ${state.sequence}`,
        // Part of the authoritative state; displayed for diagnostics only. The
        // client never advances, re-seeds, or draws from the RNG
        // (SIGNALR_PROTOCOL.md §4.1 item 2) and never derives a Gem from it.
        `RngSeed: ${state.rngSeed}  RngState: ${state.rngState.state}/${state.rngState.increment}`,
      ].join('\n')
    );

    this.renderBoard(state.board);
  }

  /**
   * Draws the authoritative board as 8 rows x 8 columns of 64 cells.
   *
   * The cells are rendered in the server's row-major order
   * (`MATCH3_RULES.md` §1.0): cell at index `i` is drawn at
   * `row = floor(i / 8)`, `column = i % 8`. The scene performs no other
   * transformation and produces no cell value of its own — every rendered Gem
   * comes from the payload (SIGNALR_PROTOCOL.md §4 item 10).
   */
  private renderBoard(board: RuntimeBoard | null): void {
    if (!this.boardLayer) {
      return;
    }

    // Redraw from scratch: the layer only ever shows the latest server state.
    this.boardLayer.removeAll(true);

    if (board === null) {
      this.boardText?.setText('Board: awaiting authoritative state');
      return;
    }

    const cells = board.cells;

    this.boardText?.setText(
      `Board: ${BOARD_ROWS}x${BOARD_COLUMNS} (${cells.length} cells, server-authoritative)`
    );

    for (let index = 0; index < cells.length; index++) {
      // The board's only coordinate convention (MATCH3_RULES.md §1.0). This is
      // the presentation mapping from that convention to screen space.
      const row = Math.floor(index / BOARD_COLUMNS);
      const column = index % BOARD_COLUMNS;

      if (row >= BOARD_ROWS || column >= BOARD_COLUMNS) {
        // Defensive: a payload with more than 64 cells would otherwise draw
        // outside the board. Such a payload is rejected by the runtime before it
        // reaches the scene, so this is unreachable in practice.
        break;
      }

      this.drawCell(row, column, cells[index]);
    }
  }

  /** Draws one board cell at its documented (row, column) position. */
  private drawCell(row: number, column: number, gemName: string): void {
    if (!this.boardLayer) {
      return;
    }

    const x = BOARD_ORIGIN_X + column * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2;
    const y = BOARD_ORIGIN_Y + row * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2;

    // An unrecognised Gem name is drawn as an inert placeholder. The scene never
    // substitutes a valid-looking Gem: doing so would fabricate authoritative
    // board content (SIGNALR_PROTOCOL.md §4 item 10).
    const presentation = GEM_PRESENTATION[gemName] ?? { color: 0x475569, label: '?' };

    const tile = this.add
      .rectangle(x, y, CELL_SIZE, CELL_SIZE, presentation.color)
      .setStrokeStyle(1, 0x0b0f19);

    const label = this.add
      .text(x, y, presentation.label, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '14px',
        color: '#0b0f19',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    this.boardLayer.add([tile, label]);
  }
}

/**
 * Maps technical runtime state to a short presentation label.
 *
 * This is presentation of infrastructure status only — it computes no gameplay
 * value of any kind.
 */
function describeRuntime(state: GameRuntimeState): { ready: boolean; label: string } {
  if (state.connection === 'connected') {
    return { ready: true, label: 'Runtime Connected' };
  }

  switch (state.connection) {
    case 'connecting':
      return { ready: false, label: 'Runtime Connecting…' };
    case 'reconnecting':
      return { ready: false, label: 'Runtime Reconnecting…' };
    case 'error':
      return { ready: false, label: 'Runtime Unavailable' };
    default:
      return { ready: false, label: 'Runtime Waiting for Connection' };
  }
}