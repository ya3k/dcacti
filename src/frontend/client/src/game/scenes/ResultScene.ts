import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import type { RewardSummaryResponse } from '../../services/api/BattleModels';

/**
 * The terminal battle outcome data handed off from BattleScene.
 *
 * Contains the server-delivered outcome wire members (SIGNALR_PROTOCOL.md
 * §3.2.19) plus the identity of the battle they belong to. It introduces no
 * derived fields and no client-side calculation (GAME_STATE.md §4,
 * AGENTS.md §10).
 */
export interface ResultSceneData {
  readonly outcome: string;
  readonly finalBossHp: number;
  readonly finalPlayerHp: number;
  /**
   * The battle's id (SIGNALR_PROTOCOL.md §3.2, `GAME_STATE.md` §2.0.1) — the
   * identity `GET /api/battle/{battleId}/result` is addressed by
   * (API_CONTRACTS.md §4).
   *
   * It is carried so the scene can read the battle's persisted result through
   * the runtime port. It is optional because the outcome handoff is not
   * required to supply it: without it the outcome presentation still renders
   * and only the reward summary is unavailable.
   */
  readonly battleId?: string;
}

/**
 * ResultScene — battle outcome and reward presentation (TDD.md §2.1).
 *
 * It presents the server-delivered battle outcome (Victory / Defeat), the
 * terminal HP values, and the persisted `RewardSummary` — every value verbatim:
 *
 *   BattleWon / BattleLost → GameRuntime.onBattleEvents → BattleScene → ResultScene
 *
 *   GET /api/battle/{battleId}/result
 *     → GameRuntime.getBattleResult → ResultScene → rewards
 *
 * The scene performs no result calculation, winner determination, or HP
 * evaluation: `outcome` is authoritative (SIGNALR_PROTOCOL.md §3.2.19,
 * GAME_RULES.md §18, ADR-001).
 *
 * **The reward summary is read, not computed.** It is delivered by the
 * documented result endpoint (API_CONTRACTS.md §4), whose `rewards` member is
 * the persisted `BattleResult.RewardSummary` — a member list owned by
 * DATABASE.md §1 ("Reward semantics for `RewardSummary`") and returned for both
 * outcomes. The scene renders exactly the eight delivered members (four Player
 * track, four Pet track) and derives nothing: no XP is summed, no Level is
 * derived from XP, no `leveledUp` flag is recomputed, and a `null` member is
 * never promoted to `0` (DATABASE.md §1 items 4–5, ADR-001).
 *
 * **Transport stays behind the runtime port.** The scene reads the result
 * through the `GameRuntime` port and imports no transport implementation — the
 * only `services/api/` import here is the type-only model file, exactly as
 * ARCHITECTURE.md §2.2.1 rule 1 requires.
 *
 * Navigation after ResultScene is not in scope (TDD.md §2.1 lifecycle ends at
 * ResultScene).
 */
export class ResultScene extends Phaser.Scene {
  private resultData: ResultSceneData | null = null;
  private outcomeText: Phaser.GameObjects.Text | null = null;
  private bossHpText: Phaser.GameObjects.Text | null = null;
  private playerHpText: Phaser.GameObjects.Text | null = null;
  private rewardText: Phaser.GameObjects.Text | null = null;

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

    // The reward summary is read but never awaited into the scene lifecycle: the
    // outcome and terminal HP values above are rendered immediately and do not
    // depend on it. A failed or unavailable read leaves that presentation intact.
    void this.loadRewards();
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
    this.rewardText = null;
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

    // The reward summary area. Left empty until the delivered members arrive, so
    // an unavailable read shows nothing rather than an invented value.
    this.rewardText = this.add
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 352, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '16px',
        color: '#cbd5e1',
        align: 'center',
        lineSpacing: 6,
      })
      .setOrigin(0.5, 0);
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

  /**
   * Reads the battle's persisted result through the runtime port and renders its
   * `rewards` (`API_CONTRACTS.md` §4 note 1).
   *
   * The read happens only when the handoff supplied a `battleId`; with none
   * there is nothing to address and no request is issued. A failed read —
   * `404 BATTLE_NOT_FOUND` or `401 UNAUTHENTICATED` (§4 notes 6–7) — leaves the
   * reward area empty and the outcome presentation untouched: the reward
   * summary is unavailable, which is not the same statement as a zero reward.
   */
  private async loadRewards(): Promise<void> {
    const battleId = this.resultData?.battleId;

    if (battleId === undefined || battleId === '') {
      return;
    }

    // The documented scene → runtime accessor (ARCHITECTURE.md §2.2.1 rule 2).
    // Returns null in an isolated scene test, so the outcome presentation never
    // depends on runtime availability.
    const runtime = readRuntime(this);

    if (!runtime) {
      return;
    }

    let rewards: RewardSummaryResponse;

    try {
      const result = await runtime.getBattleResult(battleId);
      rewards = result.rewards;
    } catch {
      // The result is authoritative and simply unavailable: nothing is
      // fabricated to fill the gap, and the outcome presentation stands.
      return;
    }

    this.rewardText?.setText(formatRewards(rewards));
  }
}

/**
 * Formats the delivered `RewardSummary` members (`DATABASE.md` §1).
 *
 * Every member is printed exactly as delivered. The four Player-track members
 * and the four Pet-track members are grouped under their own heading so the
 * account progression and the Pet progression are distinguishable.
 *
 * Nothing here is computed or coerced: `null` is the documented "this track had
 * no row" value (§1 item 4) and is printed as such rather than as `0`, and a
 * `0` grant on a defeat is printed as `0` because it is a real delivered value
 * (§1 item 5).
 */
function formatRewards(rewards: RewardSummaryResponse): string {
  return [
    'REWARDS',
    `Player: +${rewards.playerXpGained} XP  ·  XP ${formatDelivered(rewards.newPlayerXp)}  ·  Level ${formatDelivered(rewards.newPlayerLevel)}${formatLeveledUp(rewards.playerLeveledUp)}`,
    `Pet: +${rewards.petXpGained} XP  ·  XP ${formatDelivered(rewards.newPetXp)}  ·  Level ${formatDelivered(rewards.newPetLevel)}${formatLeveledUp(rewards.petLeveledUp)}`,
  ].join('\n');
}

/**
 * Renders one delivered "resulting value" member.
 *
 * A `null` member is the contract's "no row" value, not an absent field and not
 * a zero (`DATABASE.md` §1 item 4), so it is shown as unavailable rather than
 * converted.
 */
function formatDelivered(value: number | null): string {
  return value === null ? '—' : `${value}`;
}

/**
 * Renders a delivered `leveledUp` flag.
 *
 * The flag is printed from the delivered boolean and is never re-derived by
 * comparing XP or Level values (`DATABASE.md` §1 item 1).
 */
function formatLeveledUp(leveledUp: boolean | null): string {
  if (leveledUp === null) {
    return '  ·  —';
  }

  return leveledUp ? '  ·  LEVEL UP' : '';
}
