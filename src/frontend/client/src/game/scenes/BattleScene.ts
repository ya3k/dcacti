import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import type { GameRuntime } from '../runtime/GameRuntime';
import type { GameRuntimeState } from '../../state/GameRuntimeState';
import type { BattleEventsEnvelope, RuntimeBattleState, RuntimeBoard } from '../runtime/GameRuntimeEvents';
import {
  RUNTIME_ACTION_SWAP,
  RUNTIME_ACTION_CARD_CAST,
  RUNTIME_ACTION_PET_SKILL_CAST,
} from '../runtime/GameRuntimeEvents';
import type { CardResponse } from '../../services/api/CollectionModels';
import type { InBattleServerEvent } from './BattleEventPresenter';
import { parseInBattleEvent, formatInBattleEvent } from './BattleEventPresenter';

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
 * **Swap input.** The scene implements the documented Match-3 interaction
 * (`MATCH3_RULES.md` §2 item 1): a tap on a cell selects it, and a tap on a
 * second cell submits the pair through the runtime port —
 *
 *   select cell A → select cell B → GameRuntime.requestAction(Swap)
 *     → SignalRService.swap → BattleHub.Swap
 *
 * The selection is presentation-local state. The scene does not decide whether
 * the swap is legal, whether it produces a match, or what it resolves to: that
 * is `MATCH3_RULES.md` §2.1.2's server-side validation. Its only local checks
 * are interaction feedback — a tap outside the board is ignored, and a second
 * tap on the same cell clears the selection instead of submitting — which
 * neither alters the request shape nor substitutes for the server's answer
 * (`SIGNALR_PROTOCOL.md` §2.1 item 1).
 *
 * It is created with the `GameRuntime` as scene data by `GameConfig`. When no
 * runtime is supplied it still runs, reporting that the runtime is unavailable,
 * so scene lifecycle never depends on runtime availability.
 */
export class BattleScene extends Phaser.Scene {
  private runtime: GameRuntime | null = null;
  private runtimeUnsubscribe: (() => void) | null = null;
  private battleStateUnsubscribe: (() => void) | null = null;
  private battleEventsUnsubscribe: (() => void) | null = null;
  private statusText: Phaser.GameObjects.Text | null = null;
  private detailText: Phaser.GameObjects.Text | null = null;
  private battleText: Phaser.GameObjects.Text | null = null;
  private boardText: Phaser.GameObjects.Text | null = null;
  private swapText: Phaser.GameObjects.Text | null = null;
  /** Feedback text area presenting card/skill cast transport feedback. */
  private castText: Phaser.GameObjects.Text | null = null;
  /** Feedback text area presenting in-battle server events in delivered order. */
  private feedbackText: Phaser.GameObjects.Text | null = null;
  /** Visual layer for transient event highlights and floating combat text. */
  private feedbackLayer: Phaser.GameObjects.Container | null = null;
  /** The drawn board cells, cleared and redrawn on each state push. */
  private boardLayer: Phaser.GameObjects.Container | null = null;
  /** Interactive cast controls container for equipped cards and signature skill. */
  private castControlsLayer: Phaser.GameObjects.Container | null = null;
  /**
   * The first selected cell's §1.0 index, or `null` when nothing is selected.
   *
   * Presentation-local input state only — it is not board state and is never
   * sent as anything but the two cells of a Swap request.
   */
  private selectedCell: number | null = null;
  /** True while a Swap request is outstanding, so further taps are ignored. */
  private swapPending = false;
  /** True while any action request (swap or cast) is in flight. */
  private actionInFlight = false;
  /** True while an in-battle event presentation sequence is playing, locking player input. */
  private presentationLocked = false;
  /** Guard ensuring only the first terminal outcome event transitions to ResultScene. */
  private outcomeHandled = false;
  /** Log of presented events for display and verification. */
  private presentedEventsLog: string[] = [];
  /** Card definition lookup populated via GameRuntimePort.getCards(). */
  private cardDefinitions = new Map<string, CardResponse>();
  /** Current battle state for redrawing cast controls when card definitions load. */
  private currentBattleState: RuntimeBattleState | null = null;

  constructor() {
    super('BattleScene');
  }

  create(): void {
    this.runtime = readRuntime(this);
    this.drawRuntimeShell();
    this.registerBoardInput();

    void this.loadCardDefinitions();

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
      this.battleEventsUnsubscribe = this.runtime.onBattleEvents((envelope) => {
        this.handleBattleEvents(envelope);
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
    this.battleEventsUnsubscribe?.();
    this.battleEventsUnsubscribe = null;
    this.statusText = null;
    this.detailText = null;
    this.battleText = null;
    this.boardText = null;
    this.swapText = null;
    this.castText = null;
    this.feedbackText = null;
    this.boardLayer?.destroy(true);
    this.boardLayer = null;
    this.castControlsLayer?.destroy(true);
    this.castControlsLayer = null;
    this.feedbackLayer?.destroy(true);
    this.feedbackLayer = null;
    this.tweens?.killAll();
    this.selectedCell = null;
    this.swapPending = false;
    this.actionInFlight = false;
    this.presentationLocked = false;
    this.outcomeHandled = false;
    this.presentedEventsLog = [];
    this.cardDefinitions.clear();
    this.currentBattleState = null;
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

    // Swap input / acknowledgement feedback. This is transport feedback about
    // the request — not resolution presentation: it reports which cells were
    // selected and whether the server accepted the request
    // (SIGNALR_PROTOCOL.md §2.1, §5).
    this.swapText = this.add
      .text(BOARD_ORIGIN_X, BOARD_ORIGIN_Y - 42, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '13px',
        color: '#94a3b8',
      })
      .setOrigin(0, 0.5);

    // Cast input / acknowledgement feedback. Transport feedback about CardCast
    // and PetSkillCast requests (SIGNALR_PROTOCOL.md §2, §5).
    this.castText = this.add
      .text(BOARD_ORIGIN_X, BOARD_ORIGIN_Y + BOARD_WIDTH + 14, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '13px',
        color: '#94a3b8',
      })
      .setOrigin(0, 0.5);

    // Event presentation feedback readout. Starts empty on create so no forbidden terms exist before events arrive.
    this.feedbackText = this.add
      .text(BOARD_ORIGIN_X + BOARD_WIDTH + 24, BOARD_ORIGIN_Y, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '13px',
        color: '#94a3b8',
        wordWrap: { width: 340 },
      })
      .setOrigin(0, 0);

    this.boardLayer = this.add.container(0, 0);
    this.castControlsLayer = this.add.container(0, 0);
    this.feedbackLayer = this.add.container(0, 0);
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
    this.currentBattleState = state;

    if (!this.battleText) {
      return;
    }

    if (state === null) {
      this.battleText.setText('');
      this.renderBoard(null);
      this.renderCastControls(null);
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
        // The Passive's delivered `current / threshold` pair and its identity
        // (SIGNALR_PROTOCOL.md §4.3). Rendered verbatim: the scene does not
        // charge a Passive, evaluate a Threshold, or reset progress (§4.3
        // item 9, PASSIVE_RULES.md §6 item 1).
        `Passive: ${state.petState.passiveId} ` +
          `${describePassiveProgress(state.petState)}`,
      ].join('\n')
    );

    this.renderBoard(state.board);
    this.renderCastControls(state);
  }

  /**
   * Loads card definition metadata via the runtime port (`GameRuntimePort.getCards()`).
   *
   * The client uses definition metadata only to present names and identify the
   * Signature Skill (`category === 'PetSkill'`). It performs no gameplay validation
   * and computes no effect or cost (CARD_RULES.md §3, ADR-001).
   */
  private async loadCardDefinitions(): Promise<void> {
    if (!this.runtime || typeof this.runtime.getCards !== 'function') {
      return;
    }

    try {
      const cards = await this.runtime.getCards();
      this.cardDefinitions.clear();
      for (const card of cards) {
        this.cardDefinitions.set(card.cardId, card);
      }
      if (this.currentBattleState) {
        this.renderCastControls(this.currentBattleState);
      }
    } catch {
      // Failed collection read is presentation feedback; controls render with available data.
    }
  }

  /**
   * Renders interactive cast triggers for equipped cards and the active Pet's
   * Signature Skill from authoritative `RuntimeBattleState.petState.equippedCards`.
   */
  private renderCastControls(state: RuntimeBattleState | null): void {
    if (!this.castControlsLayer) {
      return;
    }

    this.castControlsLayer.removeAll(true);

    if (state === null || !state.petState.equippedCards) {
      return;
    }

    const equipped = state.petState.equippedCards;
    const buttonWidth = 115;
    const buttonHeight = 36;
    const buttonGap = 10;
    const originY = BOARD_ORIGIN_Y + BOARD_WIDTH + 34;

    for (let index = 0; index < equipped.length; index++) {
      const cardId = equipped[index];
      const def = this.cardDefinitions.get(cardId);
      const isPetSkill = def?.category === 'PetSkill';
      const displayName = def?.name ?? cardId;
      const labelText = isPetSkill ? `Skill: ${displayName}` : `Card: ${displayName}`;

      const x = BOARD_ORIGIN_X + index * (buttonWidth + buttonGap) + buttonWidth / 2;
      const y = originY + buttonHeight / 2;

      const tile = this.add
        .rectangle(x, y, buttonWidth, buttonHeight, isPetSkill ? 0x4f46e5 : 0x1e293b)
        .setStrokeStyle(1, isPetSkill ? 0x818cf8 : 0x475569)
        .setInteractive({ useHandCursor: true });

      const label = this.add
        .text(x, y, labelText, {
          fontFamily: 'system-ui, sans-serif',
          fontSize: '12px',
          color: '#e2e8f0',
          fontStyle: 'bold',
        })
        .setOrigin(0.5)
        .setInteractive({ useHandCursor: true });

      const onTrigger = () => {
        if (isPetSkill) {
          void this.submitPetSkillCast();
        } else {
          void this.submitCardCast(cardId);
        }
      };

      tile.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, onTrigger);
      label.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, onTrigger);

      this.castControlsLayer.add([tile, label]);
    }
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

  // ---------------------------------------------------------------------------
  // Swap input (MATCH3_RULES.md §2 item 1; SIGNALR_PROTOCOL.md §2.1)
  // ---------------------------------------------------------------------------

  /**
   * Registers the two-tap Swap interaction on the existing board layer.
   *
   * Phaser delivers a pointer event with the container's local coordinates, so
   * the tapped cell is resolved with the inverse of the same mapping
   * `renderBoard` uses — one coordinate convention, used in both directions
   * (`MATCH3_RULES.md` §1.0). A tap that falls in a gap between cells, or
   * outside the board, resolves to no cell and is ignored.
   */
  private registerBoardInput(): void {
    if (!this.boardLayer) {
      return;
    }

    this.boardLayer.on(
      Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN,
      (pointer: Phaser.Input.Pointer) => {
        this.onCellTapped(BattleScene.cellIndexAt(pointer.x, pointer.y));
      }
    );
  }

  /**
   * Maps a board-layer-local point to a §1.0 cell index, or `null` when the
   * point is not inside a cell.
   *
   * The board layer is a container positioned at the scene origin, so a pointer
   * event's local coordinates are already the same space `drawCell` draws in —
   * the mapping below is the exact inverse of `drawCell`'s
   * `x = BOARD_ORIGIN_X + column * pitch + CELL_SIZE / 2`
   * (`MATCH3_RULES.md` §1.0 provides the index; this is only its presentation
   * inverse).
   *
   * This is presentation geometry only. It decides no gameplay fact: it answers
   * "which drawn cell did the player touch", and an out-of-cell point yields no
   * index rather than a clamped or guessed one
   * (`MATCH3_RULES.md` §2.1.3 item 2 — nothing is clamped or reinterpreted).
   */
  private static cellIndexAt(x: number, y: number): number | null {
    const pitch = CELL_SIZE + CELL_GAP;
    const localX = x - BOARD_ORIGIN_X;
    const localY = y - BOARD_ORIGIN_Y;

    if (localX < 0 || localY < 0) {
      return null;
    }

    const column = Math.floor(localX / pitch);
    const row = Math.floor(localY / pitch);

    if (row >= BOARD_ROWS || column >= BOARD_COLUMNS) {
      return null;
    }

    // Inside the cell's own square, not in the gap that follows it.
    if (localX - column * pitch > CELL_SIZE || localY - row * pitch > CELL_SIZE) {
      return null;
    }

    return row * BOARD_COLUMNS + column;
  }

  /**
   * Handles one tap on a board cell.
   *
   * Local interaction behavior only (`SIGNALR_PROTOCOL.md` §2.1 item 1):
   *
   * - no cell (tap outside the board) — ignored;
   * - first cell — selected, shown as selection feedback;
   * - second tap on the selected cell — selection cleared, nothing sent;
   * - second cell — the pair is submitted through the runtime port.
   *
   * The scene performs no gameplay validation: it never checks adjacency, never
   * looks for a match, and never decides whether the swap is legal. Adjacency is
   * `MATCH3_RULES.md` §2.1.2's server-side check, and the server's answer is what
   * this scene renders.
   */
  private onCellTapped(cellIndex: number | null): void {
    if (cellIndex === null || this.isInputLocked()) {
      return;
    }

    if (this.selectedCell === null) {
      this.selectedCell = cellIndex;
      this.renderSwapStatus();
      return;
    }

    if (this.selectedCell === cellIndex) {
      this.selectedCell = null;
      this.renderSwapStatus();
      return;
    }

    const fromCell = this.selectedCell;
    this.selectedCell = null;

    void this.submitSwap(fromCell, cellIndex);
  }

  /**
   * Submits one Swap request through the runtime port and presents the result.
   *
   * The scene calls the runtime and nothing else: it does not touch
   * `SignalRService`, the hub, or the board (`ARCHITECTURE.md` §2.2 rule 3).
   *
   * On `accepted: true` the scene changes **no** board, turn, or sequence value:
   * the next authoritative `BattleStateUpdated` push re-renders the board
   * (`SIGNALR_PROTOCOL.md` §3.1, §4). On `accepted: false` nothing is mutated
   * locally and nothing is retried automatically — the machine-readable `reason`
   * is shown as feedback only (`MATCH3_RULES.md` §2.1.5, §5 item 2).
   */
  private async submitSwap(fromCell: number, toCell: number): Promise<void> {
    if (this.isInputLocked()) {
      return;
    }

    if (!this.runtime) {
      this.renderSwapStatus('Swap unavailable: no runtime is connected.');
      return;
    }

    this.swapPending = true;
    this.actionInFlight = true;
    this.renderSwapStatus();

    try {
      // The pair is sent as selected, and both cells are §1.0 indices
      // (`MATCH3_RULES.md` §2.1.1). The runtime sources the battle id and the
      // correlation id; the scene supplies only the two cells the player chose.
      const acknowledgement = await this.runtime.requestAction({
        kind: RUNTIME_ACTION_SWAP,
        fromCell,
        toCell,
      });

      this.renderSwapStatus(undefined, fromCell, toCell, acknowledgement);
    } catch (error) {
      // A failed request is a transport/runtime failure — presentation state,
      // not game state (`SIGNALR_PROTOCOL.md` §8.3). No board value changes and
      // nothing is retried.
      this.renderSwapStatus(
        `Swap not sent: ${error instanceof Error ? error.message : String(error)}`
      );
    } finally {
      this.swapPending = false;
      this.actionInFlight = false;
    }
  }

  /**
   * Presents the Swap interaction's own feedback: the current selection, or the
   * outcome of the last submitted request.
   *
   * This is transport feedback (`SIGNALR_PROTOCOL.md` §2.1, §5) — not resolution
   * presentation. It reports no match, cascade, combo, damage, or boss value,
   * because the client computes none of those.
   */
  private renderSwapStatus(
    message?: string,
    fromCell?: number,
    toCell?: number,
    acknowledgement?: { readonly accepted: boolean; readonly reason?: string | null }
  ): void {
    if (!this.swapText) {
      return;
    }

    if (message !== undefined) {
      this.swapText.setText(message);
      this.swapText.setColor('#f87171');
      return;
    }

    if (acknowledgement) {
      if (acknowledgement.accepted) {
        // The request was accepted. The board is NOT changed here: the next
        // authoritative push re-renders it (SIGNALR_PROTOCOL.md §3.1, §4).
        this.swapText.setText(
          `Swap ${fromCell} -> ${toCell}: accepted. Awaiting the server's state push.`
        );
        this.swapText.setColor('#34d399');
      } else {
        // Rejected: nothing happened, and the reason is the server's own
        // machine-readable code, shown as received (§5 items 2–3).
        this.swapText.setText(
          `Swap ${fromCell} -> ${toCell}: rejected (${acknowledgement.reason ?? 'unknown'}).`
        );
        this.swapText.setColor('#fbbf24');
      }
      return;
    }

    if (this.swapPending) {
      this.swapText.setText('Swap request in flight…');
      this.swapText.setColor('#94a3b8');
      return;
    }

    this.swapText.setText(
      this.selectedCell === null
        ? 'Select a cell to swap.'
        : `Cell ${this.selectedCell} selected — select a neighbour.`
    );
    this.swapText.setColor('#94a3b8');
  }

  /**
   * Submits one CardCast request through the runtime port and presents the result.
   */
  private async submitCardCast(cardId: string): Promise<void> {
    if (this.isInputLocked()) {
      return;
    }

    if (!this.runtime) {
      this.renderCastStatus('CardCast unavailable: no runtime is connected.');
      return;
    }

    this.actionInFlight = true;
    this.renderCastStatus(`CardCast ${cardId} in flight…`);

    try {
      const acknowledgement = await this.runtime.requestAction({
        kind: RUNTIME_ACTION_CARD_CAST,
        cardId,
      });

      this.renderCastStatus(undefined, cardId, false, acknowledgement);
    } catch (error) {
      this.renderCastStatus(
        `CardCast ${cardId} not sent: ${error instanceof Error ? error.message : String(error)}`
      );
    } finally {
      this.actionInFlight = false;
    }
  }

  /**
   * Submits one PetSkillCast request through the runtime port and presents the result.
   */
  private async submitPetSkillCast(): Promise<void> {
    if (this.isInputLocked()) {
      return;
    }

    if (!this.runtime) {
      this.renderCastStatus('PetSkillCast unavailable: no runtime is connected.');
      return;
    }

    this.actionInFlight = true;
    this.renderCastStatus('PetSkillCast in flight…');

    try {
      const acknowledgement = await this.runtime.requestAction({
        kind: RUNTIME_ACTION_PET_SKILL_CAST,
      });

      this.renderCastStatus(undefined, undefined, true, acknowledgement);
    } catch (error) {
      this.renderCastStatus(
        `PetSkillCast not sent: ${error instanceof Error ? error.message : String(error)}`
      );
    } finally {
      this.actionInFlight = false;
    }
  }

  /**
   * Presents cast interaction transport feedback: in flight, accepted, or rejected
   * with server machine-readable reason (SIGNALR_PROTOCOL.md §2, §5).
   */
  private renderCastStatus(
    message?: string,
    cardId?: string,
    isSkill?: boolean,
    acknowledgement?: { readonly accepted: boolean; readonly reason?: string | null }
  ): void {
    if (!this.castText) {
      return;
    }

    if (message !== undefined) {
      this.castText.setText(message);
      this.castText.setColor(message.includes('in flight') ? '#94a3b8' : '#f87171');
      return;
    }

    if (acknowledgement) {
      const actionName = isSkill ? 'PetSkillCast' : `CardCast ${cardId}`;
      if (acknowledgement.accepted) {
        this.castText.setText(`${actionName}: accepted. Awaiting the server's state push.`);
        this.castText.setColor('#34d399');
      } else {
        this.castText.setText(
          `${actionName}: rejected (${acknowledgement.reason ?? 'unknown'}).`
        );
        this.castText.setColor('#fbbf24');
      }
      return;
    }

    this.castText.setText('');
  }

  /**
   * Handles server-authoritative battle events delivered via the runtime port.
   *
   * 1. Outcome branch first (TASK-087): when the envelope contains BattleWon or BattleLost,
   *    transitions immediately to ResultScene without delay.
   * 2. In-battle presentation: presents the non-outcome event types in exact array order
   *    while holding the scene-local input guard.
   *
   * It performs no result calculation: the server is authoritative for the
   * outcome and terminal HP values (GAME_RULES.md §18, ADR-001, AGENTS.md §10).
   *
   * The handoff also carries the batch's `battleId` (SIGNALR_PROTOCOL.md §3),
   * which is what `ResultScene` addresses the documented result endpoint with to
   * read the persisted reward summary (API_CONTRACTS.md §4). The outcome members
   * themselves are unchanged; the id is transport metadata the envelope already
   * carries, not a gameplay value.
   */
  private handleBattleEvents(envelope: BattleEventsEnvelope): void {
    if (this.outcomeHandled) {
      return;
    }

    const outcome = BattleScene.findOutcomeEvent(envelope.events);
    if (outcome) {
      this.outcomeHandled = true;
      this.scene.start('ResultScene', { ...outcome, battleId: envelope.battleId });
      return;
    }

    this.presentationLocked = true;
    try {
      this.presentEventBatch(envelope.events);
    } catch (error) {
      this.tweens?.killAll();
      this.presentationLocked = false;
      return;
    }
  }

  /**
   * Presents a batch of in-battle server events in the exact array order received.
   */
  private presentEventBatch(events: readonly unknown[]): void {
    let presentedCount = 0;

    for (const raw of events) {
      const parsed = parseInBattleEvent(raw);
      if (!parsed) {
        continue;
      }

      presentedCount++;
      const formatted = formatInBattleEvent(parsed);
      this.presentedEventsLog.push(formatted);
      this.spawnEventVisualFeedback(parsed);
    }

    if (presentedCount > 0 && this.feedbackText) {
      this.feedbackText.setText(this.presentedEventsLog.slice(-14).join('\n'));
    }

    if (this.tweens && presentedCount > 0) {
      this.tweens.add({
        targets: this.feedbackText ?? {},
        alpha: { from: 0.6, to: 1 },
        duration: 150,
        onComplete: () => {
          this.presentationLocked = false;
        },
      });
    } else {
      this.presentationLocked = false;
    }
  }

  /**
   * Spawns transient visual feedback objects for a presented event.
   */
  private spawnEventVisualFeedback(event: InBattleServerEvent): void {
    if (!this.feedbackLayer) {
      return;
    }

    switch (event.type) {
      case 'MatchCreated': {
        for (const cellIndex of event.cells) {
          if (cellIndex >= 0 && cellIndex < BOARD_ROWS * BOARD_COLUMNS) {
            const { x, y } = BattleScene.cellCoordinates(cellIndex);
            const highlight = this.add
              .rectangle(x, y, CELL_SIZE, CELL_SIZE, 0xffffff, 0.4)
              .setStrokeStyle(2, 0xfacc15);
            this.feedbackLayer.add(highlight);

            if (this.tweens) {
              this.tweens.add({
                targets: highlight,
                alpha: 0,
                duration: 400,
                onComplete: () => {
                  highlight.destroy();
                },
              });
            }
          }
        }
        break;
      }
      case 'GemMatched': {
        if (event.cellIndex >= 0 && event.cellIndex < BOARD_ROWS * BOARD_COLUMNS) {
          const { x, y } = BattleScene.cellCoordinates(event.cellIndex);
          const flash = this.add
            .rectangle(x, y, CELL_SIZE, CELL_SIZE, 0x60a5fa, 0.5)
            .setStrokeStyle(2, 0x38bdf8);
          this.feedbackLayer.add(flash);

          if (this.tweens) {
            this.tweens.add({
              targets: flash,
              alpha: 0,
              scale: 1.2,
              duration: 350,
              onComplete: () => {
                flash.destroy();
              },
            });
          }
        }
        break;
      }
      case 'DamageDealt':
      case 'DamageTaken': {
        const x = BOARD_ORIGIN_X + BOARD_WIDTH / 2;
        const y =
          event.type === 'DamageDealt'
            ? BOARD_ORIGIN_Y - 20
            : BOARD_ORIGIN_Y + BOARD_WIDTH + 20;
        const color = event.type === 'DamageDealt' ? '#f87171' : '#fb923c';
        const label = this.add
          .text(x, y, `-${event.amount}`, {
            fontFamily: 'system-ui, sans-serif',
            fontSize: '18px',
            color,
            fontStyle: 'bold',
          })
          .setOrigin(0.5);
        this.feedbackLayer.add(label);

        if (this.tweens) {
          this.tweens.add({
            targets: label,
            y: y - 30,
            alpha: 0,
            duration: 600,
            onComplete: () => {
              label.destroy();
            },
          });
        }
        break;
      }
      default:
        // Other events update feedbackText log
        break;
    }
  }

  /**
   * Computes center coordinates for a board cell index in presentation pixels.
   */
  private static cellCoordinates(index: number): { x: number; y: number } {
    const row = Math.floor(index / BOARD_COLUMNS);
    const column = index % BOARD_COLUMNS;
    return {
      x: BOARD_ORIGIN_X + column * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2,
      y: BOARD_ORIGIN_Y + row * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2,
    };
  }

  /** Read-only snapshot of presented event lines for diagnostics/testing. */
  getPresentedEvents(): readonly string[] {
    return this.presentedEventsLog;
  }

  /** Returns whether player input is currently locked. */
  isInputLocked(): boolean {
    return this.presentationLocked || this.swapPending || this.actionInFlight;
  }

  /**
   * Finds the first terminal battle outcome event in an event batch.
   *
   * The batch may contain other resolution events (GAME_EVENTS.md §1); the
   * outcome is identified by the documented `BattleWon` / `BattleLost` wire
   * discriminators (SIGNALR_PROTOCOL.md §3.2.19).
   */
  private static findOutcomeEvent(
    events: readonly unknown[]
  ): { readonly outcome: string; readonly finalBossHp: number; readonly finalPlayerHp: number } | null {
    for (const event of events) {
      if (
        typeof event === 'object' &&
        event !== null &&
        'type' in event &&
        (event.type === 'BattleWon' || event.type === 'BattleLost') &&
        'outcome' in event &&
        typeof (event as { outcome?: unknown }).outcome === 'string' &&
        'finalBossHp' in event &&
        typeof (event as { finalBossHp?: unknown }).finalBossHp === 'number' &&
        'finalPlayerHp' in event &&
        typeof (event as { finalPlayerHp?: unknown }).finalPlayerHp === 'number'
      ) {
        const payload = event as {
          outcome: string;
          finalBossHp: number;
          finalPlayerHp: number;
        };
        return {
          outcome: payload.outcome,
          finalBossHp: payload.finalBossHp,
          finalPlayerHp: payload.finalPlayerHp,
        };
      }
    }
    return null;
  }
}

/**
 * Renders the delivered Passive progress pair as the documented
 * `current / threshold` value (`PASSIVE_RULES.md` §6 item 1).
 *
 * Presentation only: both numbers come from the synchronized `petState` and are
 * printed as received. The absence of `passiveResetOverride` means the default
 * reset behavior (`SIGNALR_PROTOCOL.md` §4.3 item 7) and is shown as such
 * without substituting a value for it.
 */
function describePassiveProgress(state: RuntimeBattleState['petState']): string {
  const { current, threshold } = state.passiveProgress;

  const reset = state.passiveResetOverride ?? 'Default';

  return `(${current} / ${threshold} Matches, reset: ${reset})`;
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