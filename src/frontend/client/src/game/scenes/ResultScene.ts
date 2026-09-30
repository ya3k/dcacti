import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';

/**
 * The terminal battle outcome data handed off from BattleScene.
 *
 * Contains only the server-delivered wire members (SIGNALR_PROTOCOL.md §3.2.19).
 * It introduces no derived fields, no client-side calculation, and no reward
 * summary (GAME_STATE.md §4, AGENTS.md §10).
 */
export interface ResultSceneData {
  readonly outcome: string;
  readonly finalBossHp: number;
  readonly finalPlayerHp: number;
}

/**
 * ResultScene — battle outcome presentation (TDD.md §2.1).
 *
 * It presents the server-delivered battle outcome (Victory / Defeat) and the
 * terminal HP values verbatim:
 *
 *   BattleWon / BattleLost → GameRuntime.onBattleEvents → BattleScene → ResultScene
 *
 * The scene performs no result calculation, winner determination, or HP
 * evaluation: `outcome` is authoritative (SIGNALR_PROTOCOL.md §3.2.19,
 * GAME_RULES.md §18, ADR-001).
 *
 * Navigation after ResultScene is not in scope (TDD.md §2.1 lifecycle ends at
 * ResultScene).
 */
export class ResultScene extends Phaser.Scene {
  private resultData: ResultSceneData | null = null;
  private outcomeText: Phaser.GameObjects.Text | null = null;
  private bossHpText: Phaser.GameObjects.Text | null = null;
  private playerHpText: Phaser.GameObjects.Text | null = null;

  constructor() {
    super('ResultScene');
  }

  init(data?: ResultSceneData): void {
    this.resultData = data ?? null;
  }

  create(data?: ResultSceneData): void {
    if (data) {
      this.resultData = data;
    }

    this.drawShell();
    this.renderResult();
  }

  /**
   * Scene shutdown — reached when this scene stops or the game is destroyed.
   * Cleans up scene-local references.
   */
  shutdown(): void {
    this.resultData = null;
    this.outcomeText = null;
    this.bossHpText = null;
    this.playerHpText = null;
  }

  private drawShell(): void {
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
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 48, 'BATTLE RESULT', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '24px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    this.outcomeText = this.add
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 160, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '48px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    this.bossHpText = this.add
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 260, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '18px',
        color: '#94a3b8',
      })
      .setOrigin(0.5);

    this.playerHpText = this.add
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 300, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '18px',
        color: '#94a3b8',
      })
      .setOrigin(0.5);
  }

  private renderResult(): void {
    if (!this.resultData) {
      this.outcomeText?.setText('NO RESULT');
      this.outcomeText?.setColor('#94a3b8');
      this.bossHpText?.setText('Final Boss HP: —');
      this.playerHpText?.setText('Final Player HP: —');
      return;
    }

    const { outcome, finalBossHp, finalPlayerHp } = this.resultData;

    // The outcome field is authoritative (SIGNALR_PROTOCOL.md §3.2.19, GAME_EVENTS.md §2).
    // The scene does NOT determine victory/defeat from HP values.
    if (outcome === 'victory') {
      this.outcomeText?.setText('VICTORY');
      this.outcomeText?.setColor('#22c55e');
    } else if (outcome === 'defeat') {
      this.outcomeText?.setText('DEFEAT');
      this.outcomeText?.setColor('#ef4444');
    } else {
      this.outcomeText?.setText(outcome.toUpperCase());
      this.outcomeText?.setColor('#94a3b8');
    }

    // Terminal HP values are presented exactly as delivered.
    // The scene does not clamp, recompute, normalize, or infer HP.
    this.bossHpText?.setText(`Final Boss HP: ${finalBossHp}`);
    this.playerHpText?.setText(`Final Player HP: ${finalPlayerHp}`);
  }
}
