import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
// The presentation layer's one rendering of a delivered `RewardSummary`
// (`DATABASE.md` §1), shared with `BattleHistoryScene` so the eight delivered
// members are never represented twice (docs/AGENTS.md §2).
import { formatRewards } from '../presentation/RewardSummaryFormat';
import type { RewardSummaryResponse } from '../../services/api/BattleModels';
import type { LobbySceneData } from './LobbyScene';

/**
 * The two approved post-result navigation controls (`D-202-01 = C`).
 *
 * They are the labels the scene renders and the only destinations it reaches;
 * the strings are the Product Owner's own wording.
 */
const PLAY_AGAIN_LABEL = 'PLAY AGAIN';
const MAIN_MENU_LABEL = 'MAIN MENU';

/** The navigation controls' geometry, in logical game pixels. */
const BUTTON_WIDTH = 280;
const BUTTON_HEIGHT = 50;
const BUTTON_ROW_Y = SAFE_AREA.y + 520;
const BUTTON_OFFSET_X = 160;

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
 * **Navigation is the two approved explicit controls** (`D-202-01 = C`,
 * `D-202-02 = A`; GDD.md §2.1, TDD.md §2.1):
 *
 * ```text
 * PLAY AGAIN   → clear the completed battle's active battle state
 *                → LobbyScene, carrying { restorePreservedLoadout: true } so the
 *                  loadout the battle just ended with is restored and stays
 *                  editable (D-202-03 = D, ADR-022)
 *
 * MAIN MENU    → clear the completed battle's active battle state
 *                → MainMenuScene
 * ```
 *
 * Each fires at most once per scene instance, and it is the only way off this
 * scene: there is no automatic timed transition, no full-screen tap target, and
 * no any-key confirmation. The cleanup is client-local — it drops the runtime's
 * synchronized battle copy through the port and never disconnects the transport
 * (`D-202-04 = A`; ARCHITECTURE.md §2.2.1, §2.2.3) — and it deliberately does
 * not touch the preserved pre-battle loadout, which is not battle state.
 */
export class ResultScene extends Phaser.Scene {
  private resultData: ResultSceneData | null = null;
  private outcomeText: Phaser.GameObjects.Text | null = null;
  private bossHpText: Phaser.GameObjects.Text | null = null;
  private playerHpText: Phaser.GameObjects.Text | null = null;
  private rewardText: Phaser.GameObjects.Text | null = null;

  /**
   * Guards the post-result navigation so exactly one transition leaves this
   * scene instance.
   *
   * Both controls share it: a second activation of the same control, a double
   * press, or an activation of the other control cannot start a second scene
   * (`MainMenuScene.transitionTo`'s documented precedent).
   */
  private hasTransitioned = false;

  /**
   * Identifies the presentation run an in-flight reward read belongs to.
   *
   * `create()` takes the next value and hands it to `loadRewards()`, and both
   * `create()` and the scene's teardown advance it. The read therefore writes to
   * the reward area only while the run that started it is still the current one:
   * a `getBattleResult` promise that settles after the scene has been shut down
   * (its `Text` objects destroyed by Phaser's `DisplayList#shutdown`), or after
   * the instance has been reused for a later battle, is discarded instead of
   * mutating UI that is no longer the presentation it was read for.
   *
   * It is plain scene-local state, like the other per-run guards here: no
   * cancellation registry, controller, or global state is introduced
   * (`AGENTS.md` §9).
   */
  private rewardLoadRun = 0;

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

    // A scene instance is reusable, so the guard is reset for this presentation.
    this.hasTransitioned = false;

    // Attach this scene's teardown to Phaser's own scene lifecycle events,
    // before any of the resources it releases is created.
    //
    // A scene method that merely *exists* is never invoked: Phaser 4.2.1 calls
    // `init`/`preload`/`create`/`update` by name (`SceneManager.bootScene` /
    // `SceneManager.create`) and nothing else, and `Phaser.Scene` declares no
    // `shutdown` method at all. What the engine actually emits when it takes a
    // scene down is `Phaser.Scenes.Events.SHUTDOWN` — from
    // `Phaser.Scenes.Systems#shutdown`, which `SceneManager` calls for a queued
    // `stop` and when restarting a running scene — and `DESTROY` from
    // `Systems#destroy`. That is the documented scene-event mechanism
    // (`.ai/skills/client/phaser-architecture` "Resource Lifecycle & Cleanup";
    // `.ai/skills/phaser/scenes` gotcha 14); without it the teardown below never
    // runs in the browser, so this scene's references (and its in-flight reward
    // read) would outlive the presentation they belong to.
    //
    // The registration is idempotent — the pair is detached first, then attached
    // with `once` — so a scene that is shut down and later reused holds exactly
    // one teardown handler per event instead of stacking another one on every
    // run (`once` alone would leave the run's unfired `DESTROY` handler behind).
    // This is the same "detach before attach" idempotency
    // `BattleScene.registerBoardInput` already uses, and `once` means the
    // handler is consumed by the event that fired it.
    this.events.off(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.off(Phaser.Scenes.Events.DESTROY, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.DESTROY, this.shutdown, this);

    // This presentation's reward read run. `loadRewards` captures the value and
    // checks it again once the read settles, so the asynchronous continuation
    // can only write to the presentation that started it (see the field's own
    // note).
    const rewardRun = ++this.rewardLoadRun;

    this.drawShell();
    this.renderResult();
    this.drawNavigation();

    // The reward summary is read but never awaited into the scene lifecycle: the
    // outcome and terminal HP values above are rendered immediately and do not
    // depend on it. A failed or unavailable read leaves that presentation intact.
    void this.loadRewards(rewardRun);
  }

  /**
   * Scene teardown — the handler subscribed to Phaser's own
   * `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY` events by `create()`.
   *
   * It is a plain method invoked *by the engine's event*, not an engine hook:
   * Phaser never calls a scene method named `shutdown` on its own, so before
   * this registration existed the body below never ran in the browser.
   *
   * It drops this scene's references and its per-presentation navigation guard,
   * and it **invalidates any reward read still in flight**: `loadRewards`
   * compares the run it captured against {@link rewardLoadRun} before it
   * writes, so a `getBattleResult` promise that settles after teardown — or
   * after this instance has been reused for a later battle — cannot touch the
   * destroyed `Text` objects of the ended run, nor the live ones of the next
   * (`Phaser`'s `DisplayList#shutdown` has already destroyed this run's game
   * objects by the time this handler runs).
   *
   * Nothing outside the scene's own presentation is touched: the completed
   * battle's active battle state is the runtime's to drop and is cleared by the
   * runtime port on the way out, and the preserved pre-battle loadout is not
   * this scene's (`D-202-04 = A`, `ARCHITECTURE.md` §2.2.3, ADR-022).
   *
   * Every step is idempotent and safe on both events: the assignments are plain
   * resets, and the run counter simply advances again.
   */
  shutdown(): void {
    this.rewardLoadRun++;

    this.resultData = null;
    this.outcomeText = null;
    this.bossHpText = null;
    this.playerHpText = null;
    this.rewardText = null;
    this.hasTransitioned = false;
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

  /**
   * Draws the two approved post-result controls (`D-202-01 = C`,
   * `D-202-02 = A`).
   *
   * They are explicit interactive buttons and the scene's only navigation
   * mechanism: nothing here registers a keyboard handler, a full-screen tap
   * target, or a timer. Their labels are the Product Owner's own wording, and
   * their geometry only has to keep them clear of the reward area above.
   */
  private drawNavigation(): void {
    this.drawButton(GAME_WIDTH / 2 - BUTTON_OFFSET_X, BUTTON_ROW_Y, PLAY_AGAIN_LABEL, () =>
      this.playAgain()
    );

    this.drawButton(GAME_WIDTH / 2 + BUTTON_OFFSET_X, BUTTON_ROW_Y, MAIN_MENU_LABEL, () =>
      this.returnToMainMenu()
    );
  }

  /**
   * Draws one interactive navigation control and registers its hit area.
   *
   * It follows `MainMenuScene`'s existing visual and interaction convention
   * (fill, stroke, centred label, `setInteractive` + `GAMEOBJECT_POINTER_DOWN`)
   * rather than introducing a navigation or button abstraction
   * (`ARCHITECTURE.md` §5, `AGENTS.md` §9).
   */
  private drawButton(x: number, y: number, label: string, onActivate: () => void): void {
    const button = this.add
      .rectangle(x, y, BUTTON_WIDTH, BUTTON_HEIGHT, 0x1d4ed8)
      .setStrokeStyle(1, 0x60a5fa);
    button.setInteractive({ useHandCursor: true });
    button.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, onActivate);

    this.add
      .text(x, y, label, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '20px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);
  }

  /**
   * `PLAY AGAIN` — the approved continuation to `LobbyScene` (`D-202-01 = C`).
   *
   * The completed battle's active battle state is cleared first, so nothing
   * about it can reach the next battle (`D-202-04 = A`), and it is cleared
   * without disconnecting the transport. The preserved pre-battle loadout is
   * **not** affected: it is not battle state, and `LobbyScene` is told to
   * restore it through the documented scene-start data
   * (`D-202-03 = D`, ADR-022).
   */
  private playAgain(): void {
    if (!this.claimTransition()) {
      return;
    }

    this.clearActiveBattleState();

    const data: LobbySceneData = { restorePreservedLoadout: true };
    this.scene.start('LobbyScene', data);
  }

  /**
   * `MAIN MENU` — the approved continuation to `MainMenuScene`
   * (`D-202-01 = C`).
   *
   * It clears the completed battle's active battle state exactly as
   * `PLAY AGAIN` does, for the same reason, and navigates nowhere else.
   */
  private returnToMainMenu(): void {
    if (!this.claimTransition()) {
      return;
    }

    this.clearActiveBattleState();

    this.scene.start('MainMenuScene');
  }

  /**
   * Claims this scene instance's single permitted transition.
   *
   * @returns `true` for the first caller, `false` for every later one, so
   * activation cannot start a second scene.
   */
  private claimTransition(): boolean {
    if (this.hasTransitioned) {
      return false;
    }

    this.hasTransitioned = true;
    return true;
  }

  /**
   * Clears the completed battle's active battle state through the runtime port
   * (`D-202-04 = A`, `ARCHITECTURE.md` §2.2.1, §2.2.3).
   *
   * The runtime drops its synchronized battle copy and keeps the connection; it
   * computes nothing, reads no result route, and does not touch the preserved
   * pre-battle loadout.
   *
   * `readRuntime` returns `null` in an isolated scene test, and navigation must
   * not depend on runtime availability: with no runtime there is no
   * synchronized copy to clear, and the scene still navigates.
   */
  private clearActiveBattleState(): void {
    readRuntime(this)?.clearActiveBattleState();
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
   *
   * `run` is the presentation run `create()` started this read for. The read is
   * asynchronous and the scene can be shut down or reused while it is in flight,
   * so the delivery is written only when that run is still the current one
   * ({@link rewardLoadRun}).
   */
  private async loadRewards(run: number): Promise<void> {
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

    // The presentation that asked for this read may be over: Phaser destroys a
    // stopped scene's game objects and this scene's teardown drops its
    // references, and a later `create()` on the reused instance owns a different
    // reward area. Neither may be written by a stale continuation.
    if (run !== this.rewardLoadRun) {
      return;
    }

    this.rewardText?.setText(formatRewards(rewards));
  }
}
