import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH, GAME_HEIGHT } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import type { GameRuntime } from '../runtime/GameRuntime';
import type { GameRuntimeState } from '../../state/GameRuntimeState';
import type { RuntimeBattleState } from '../runtime/GameRuntimeEvents';

/**
 * BattleScene — runtime shell only.
 *
 * This is NOT a game screen and contains no gameplay. It exists to prove the
 * battle runtime can exist and to establish the presentation boundary:
 *
 *   BattleScene → GameRuntime → SignalRService → SignalR
 *
 * The scene reads the runtime through the `GameRuntime` port and never imports
 * SignalR, `HubConnection`, or any transport type (task §16,
 * ARCHITECTURE.md §2.2 rule 3). No board, gem, match, damage, HP, or any other
 * gameplay concept appears here.
 *
 * It presents the Battle State Foundation the server pushed — `BattleId`,
 * `Turn`, `Sequence` (GAME_STATE.md §2.0, SIGNALR_PROTOCOL.md §4) — as a
 * development readout. Those values are server-owned: the scene only displays
 * what the runtime received and never computes, adjusts, or decides them
 * (SIGNALR_PROTOCOL.md §4.9, ADR-001).
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
   * Detaching here prevents listener leaks across scene restarts (task §20).
   */
  shutdown(): void {
    this.runtimeUnsubscribe?.();
    this.runtimeUnsubscribe = null;
    this.battleStateUnsubscribe?.();
    this.battleStateUnsubscribe = null;
    this.statusText = null;
    this.detailText = null;
    this.battleText = null;
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
      .text(GAME_WIDTH / 2, GAME_HEIGHT / 2 - 60, 'BATTLE SCENE', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '28px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    this.statusText = this.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT / 2 + 10, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '18px',
        color: '#60a5fa',
      })
      .setOrigin(0.5);

    this.detailText = this.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT / 2 + 50, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '13px',
        color: '#94a3b8',
        align: 'center',
      })
      .setOrigin(0.5);

    this.battleText = this.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT / 2 + 90, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '13px',
        color: '#64748b',
        align: 'center',
      })
      .setOrigin(0.5);

    this.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT / 2 + 110, `${GAME_WIDTH} x ${GAME_HEIGHT} logical`, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '12px',
        color: '#475569',
      })
      .setOrigin(0.5);
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
   * Renders the synchronized authoritative foundation state the server pushed
   * (`BattleStateUpdated`, SIGNALR_PROTOCOL.md §4) — a development readout of
   * `BattleId`, `Turn`, and `Sequence`.
   *
   * It presents the values verbatim. The scene computes nothing, adjusts
   * nothing, and owns nothing: these are server-authoritative fields
   * (GAME_STATE.md §2.0.4, ADR-001).
   */
  private renderBattleState(state: RuntimeBattleState | null): void {
    if (!this.battleText) {
      return;
    }

    if (state === null) {
      this.battleText.setText('');
      return;
    }

    this.battleText.setText(
      [`BattleId: ${state.battleId}`, `Turn: ${state.turn}`, `Sequence: ${state.sequence}`].join(
        '\n'
      )
    );
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