import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
// The presentation layer's one rendering of a delivered `RewardSummary`
// (`DATABASE.md` §1), shared with `BattleHistoryScene` so the eight delivered
// members are never represented twice (docs/AGENTS.md §2).
import { formatRewards } from '../presentation/RewardSummaryFormat';
import type { BattleResultResponse } from '../../services/api/BattleModels';
import type { LobbySceneData } from './LobbyScene';

/**
 * The two approved post-result navigation controls (`D-202-01 = C`).
 *
 * They are the labels the scene renders and the only destinations it reaches;
 * the strings are the Product Owner's own wording.
 */
const PLAY_AGAIN_LABEL = 'PLAY AGAIN';
const MAIN_MENU_LABEL = 'MAIN MENU';

/**
 * The reward block's third control (`TASK-211 §6`).
 *
 * It is the same word the read-only viewer and history scenes already use for
 * their failed loads, so `RETRY` means one thing in this application: repeat
 * the read that failed.
 */
const RETRY_LABEL = 'RETRY';

/** The navigation controls' geometry, in logical game pixels. */
const BUTTON_WIDTH = 280;
const BUTTON_HEIGHT = 50;
const BUTTON_ROW_Y = SAFE_AREA.y + 520;
const BUTTON_OFFSET_X = 160;

/**
 * The reward block's own states (`TASK-211 §5`).
 *
 * `loading` and `failure` are visibly different from each other and from a
 * delivered zero reward, which renders as `REWARDS` + `+0 XP`: the whole defect
 * being fixed was that a failed read and a read still in flight both looked
 * like an ordinary empty box.
 */
type RewardLoadState = 'loading' | 'success' | 'failure';

/** Shown while the result read is in flight. */
const REWARD_LOADING_TEXT = 'Loading rewards…';
/**
 * Shown when an addressable reward read failed, or delivered something that is
 * not the documented shape. It states the outcome and offers `RETRY`; it
 * deliberately does not say "no rewards", which is a different, delivered fact.
 */
const REWARD_FAILURE_TEXT = 'Rewards could not be loaded.\nPlease try again.';
/**
 * Shown when there was no reward read to make at all — the handoff carried no
 * battle to address, or no runtime was published. There is nothing to repeat,
 * so no `RETRY` is offered and the wording does not promise one.
 */
const REWARD_UNAVAILABLE_TEXT = 'Rewards are unavailable for this battle.';

/** The `RETRY` control's geometry — between the reward block and the nav row. */
const RETRY_BUTTON_WIDTH = 160;
const RETRY_BUTTON_HEIGHT = 34;
const RETRY_BUTTON_ROW_Y = SAFE_AREA.y + 470;

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
 * **The read has a state, and the player can see it.** The block is either
 * loading, showing the delivered members, or reporting that they could not be
 * loaded — and a failure offers `RETRY`, which repeats **only** the read under a
 * fresh run identity. A failed read is never rendered as a zero reward
 * (`REWARDS` + `+0 XP` is a delivered fact, not an empty box), nothing is
 * fabricated to fill the gap, and the battle is never restarted from here. The
 * same read delivers the battle's `durationTurns` (§4 note 5), which this scene
 * prints as delivered: no turn is counted, no timestamp is read, and no length
 * is derived from combat events.
 *
 * **Transport stays behind the runtime port.** The scene reads the result
 * through the `GameRuntime` port and imports no transport implementation — the
 * only `services/api/` import here is the type-only model file, exactly as
 * ARCHITECTURE.md §2.2.1 rule 1 requires.
 *
 * **The terminal Pet HP line says Pet.** `finalPlayerHp` is a fixed protocol
 * label for the active **Pet's** HP at battle end (SIGNALR_PROTOCOL.md §3.2.19,
 * GAME_STATE.md §2.3, ADR-011): there is no Player HP pool, so the label a
 * player reads names the Pet and the wire member is untouched.
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
  /**
   * The delivered terminal Pet HP line.
   *
   * The field keeps its historical name — several browser harnesses read it —
   * but the value it prints is the one the contract delivers: the battle's
   * `finalPlayerHp`, which `SIGNALR_PROTOCOL.md` §3.2.19 defines as the active
   * **Pet's** HP at battle end (`GAME_STATE.md` §2.3 `PetState.HP`). The wire
   * name is a fixed protocol label; there is no Player HP pool (ADR-011).
   */
  private playerHpText: Phaser.GameObjects.Text | null = null;
  /** The delivered `durationTurns`, once the result read has delivered it. */
  private durationText: Phaser.GameObjects.Text | null = null;
  private rewardText: Phaser.GameObjects.Text | null = null;

  /** The reward block's current state ({@link RewardLoadState}). */
  private rewardState: RewardLoadState = 'loading';
  /**
   * Whether the current failure has a reward read behind it to repeat.
   *
   * A read that was issued and failed — including one that answered with a
   * payload that is not the documented shape — is repeatable, so `RETRY` is
   * offered. A failure with no addressable read at all is not, and no control
   * is drawn for it.
   */
  private rewardRetryable = false;
  /** The rendered `RewardSummary` block of the current successful read. */
  private rewardSummary = '';
  /** The delivered `durationTurns` of the current successful read. */
  private rewardDurationTurns: number | null = null;
  /** The `RETRY` control's objects, destroyed and re-created by each render pass. */
  private rewardRetryButton: Phaser.GameObjects.Rectangle | null = null;
  private rewardRetryLabel: Phaser.GameObjects.Text | null = null;

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

    // A scene instance is reusable, so the reward block starts this run in its
    // own state rather than in the ended run's.
    this.rewardState = 'loading';
    this.rewardRetryable = false;
    this.rewardSummary = '';
    this.rewardDurationTurns = null;
    this.rewardRetryButton = null;
    this.rewardRetryLabel = null;

    this.drawShell();
    this.renderResult();
    this.renderRewards();
    this.drawNavigation();

    // The reward summary is read but never awaited into the scene lifecycle: the
    // outcome and terminal HP values above are rendered immediately and do not
    // depend on it. A failed or unavailable read leaves that presentation intact
    // and reports itself in the reward block.
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
    this.durationText = null;
    this.rewardText = null;
    this.rewardState = 'loading';
    this.rewardRetryable = false;
    this.rewardSummary = '';
    this.rewardDurationTurns = null;
    // Phaser's `DisplayList#shutdown` has already destroyed these objects by the
    // time this handler runs; the references are dropped so no continuation can
    // reach them.
    this.rewardRetryButton = null;
    this.rewardRetryLabel = null;
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

    // The battle's delivered length (`BattleResult.DurationTurns`,
    // `API_CONTRACTS.md` §4 note 5). It is written only from the result read's
    // own `durationTurns`: nothing here counts turns, reads a timestamp, or
    // derives a length from combat events.
    this.durationText = this.add
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 356, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '18px',
        color: '#94a3b8',
      })
      .setOrigin(0.5);

    // The reward summary area. Its state is set by `renderRewards`: it reports
    // the read as loading, as failed (with `RETRY`), as unavailable, or as the
    // delivered members. It never shows an invented value.
    this.rewardText = this.add
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 396, '', {
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
      this.playerHpText?.setText('Final Pet HP: —');
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
    //
    // The second line names the **Pet's** HP because that is what the delivered
    // member is: `finalPlayerHp` is a fixed protocol label for
    // `PetState.HP` at battle end (`SIGNALR_PROTOCOL.md` §3.2.19 note,
    // `GAME_STATE.md` §2.3, ADR-011 — there is no Player HP pool). The member
    // name is unchanged; only the label a player reads was wrong.
    this.bossHpText?.setText(`Final Boss HP: ${finalBossHp}`);
    this.playerHpText?.setText(`Final Pet HP: ${finalPlayerHp}`);
  }

  /**
   * Renders the reward block in its current state.
   *
   * It writes to the one `rewardText` this scene created and re-creates the
   * `RETRY` control from scratch on every pass, so no state — loaded, loading or
   * failed — can leave a second block or a second handler behind however often
   * `RETRY` is pressed.
   */
  private renderRewards(): void {
    this.clearRewardRetryControl();

    if (this.rewardState === 'success') {
      this.rewardText?.setText(this.rewardSummary);
      this.durationText?.setText(
        this.rewardDurationTurns === null ? '' : `Duration: ${this.rewardDurationTurns} turns`
      );
      return;
    }

    // Neither a failed nor an in-flight read delivered a length, and neither is
    // rendered as a zero.
    this.durationText?.setText('');

    this.rewardText?.setText(
      this.rewardState === 'loading'
        ? REWARD_LOADING_TEXT
        : this.rewardRetryable
          ? REWARD_FAILURE_TEXT
          : REWARD_UNAVAILABLE_TEXT
    );

    if (this.rewardState === 'failure' && this.rewardRetryable) {
      this.drawRewardRetryControl();
    }
  }

  /**
   * Draws the `RETRY` control the failure state offers (`TASK-211 §6`).
   *
   * Its handler is the only thing it does: it repeats the reward read and
   * nothing else. It never re-enters the battle, never re-reads the outcome, and
   * never navigates.
   */
  private drawRewardRetryControl(): void {
    const x = GAME_WIDTH / 2;
    const y = RETRY_BUTTON_ROW_Y;

    const button = this.add
      .rectangle(x, y, RETRY_BUTTON_WIDTH, RETRY_BUTTON_HEIGHT, 0x1d4ed8)
      .setStrokeStyle(1, 0x60a5fa);

    const label = this.add
      .text(x, y, RETRY_LABEL, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '16px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    button.setInteractive({ useHandCursor: true });
    button.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, () => this.retryRewards());

    this.rewardRetryButton = button;
    this.rewardRetryLabel = label;
  }

  /**
   * Releases the `RETRY` control's objects and their pointer handlers.
   *
   * It is called by every reward render pass, so a retry cannot accumulate a
   * second control or a second listener.
   */
  private clearRewardRetryControl(): void {
    this.rewardRetryButton?.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);
    this.rewardRetryButton?.destroy();
    this.rewardRetryLabel?.destroy();
    this.rewardRetryButton = null;
    this.rewardRetryLabel = null;
  }

  /**
   * `RETRY` — repeats the reward read, and only that (`TASK-211 §6`).
   *
   * The run identity advances first, so the read that failed (or any read still
   * in flight) can no longer write: only this repeat owns the presentation. An
   * activation while a read is already in flight is ignored, so repeated
   * pressing can never issue two concurrent reads.
   */
  private retryRewards(): void {
    if (this.rewardState === 'loading') {
      return;
    }

    const run = ++this.rewardLoadRun;
    this.rewardState = 'loading';
    this.rewardRetryable = false;
    this.renderRewards();

    void this.loadRewards(run);
  }

  /**
   * Reads the battle's persisted result through the runtime port and renders its
   * `rewards` and its `durationTurns` (`API_CONTRACTS.md` §4).
   *
   * The read is issued only when the handoff supplied a `battleId` **and** a
   * runtime is published; with either missing there is nothing to address, and
   * the block reports the summary as unavailable rather than as a failure to
   * retry. A read that is issued and rejects —
   * `404 BATTLE_NOT_FOUND` or `401 UNAUTHENTICATED` (§4 notes 6–7) — leaves the
   * outcome presentation untouched and puts the block in its failure state with
   * a working `RETRY`.
   *
   * **It fails closed on a payload that is not the documented shape.** An absent
   * or malformed `rewards` (or a non-numeric `durationTurns`) is treated as the
   * failure state, never as data: a `{}`-shaped response must not render literal
   * `undefined` under a `REWARDS` heading, and a zero reward must stay
   * distinguishable from a payload that never arrived.
   *
   * `run` is the presentation run that started this read. The read is
   * asynchronous and the scene can be shut down or reused while it is in flight,
   * so the delivery is written only when that run is still the current one
   * ({@link rewardLoadRun}).
   */
  private async loadRewards(run: number): Promise<void> {
    const battleId = this.resultData?.battleId;

    // The documented scene → runtime accessor (ARCHITECTURE.md §2.2.1 rule 2).
    // Returns null in an isolated scene test, so the outcome presentation never
    // depends on runtime availability.
    const runtime = readRuntime(this);

    if (battleId === undefined || battleId === '' || !runtime) {
      if (run === this.rewardLoadRun) {
        this.rewardState = 'failure';
        this.rewardRetryable = false;
        this.renderRewards();
      }
      return;
    }

    let result: BattleResultResponse;

    try {
      result = await runtime.getBattleResult(battleId);
    } catch {
      // The result is authoritative and simply unavailable: nothing is
      // fabricated to fill the gap, and the outcome presentation stands.
      if (run !== this.rewardLoadRun) {
        return;
      }

      this.rewardState = 'failure';
      this.rewardRetryable = true;
      this.renderRewards();
      return;
    }

    // The presentation that asked for this read may be over: Phaser destroys a
    // stopped scene's game objects and this scene's teardown drops its
    // references, and a later `create()` on the reused instance owns a different
    // reward area. Neither may be written by a stale continuation.
    if (run !== this.rewardLoadRun) {
      return;
    }

    if (!isDeliveredResult(result)) {
      this.rewardState = 'failure';
      this.rewardRetryable = true;
      this.renderRewards();
      return;
    }

    this.rewardSummary = formatRewards(result.rewards);
    this.rewardDurationTurns = result.durationTurns;
    this.rewardState = 'success';
    this.renderRewards();
  }
}

/**
 * Whether a delivered result carries the members this scene renders, in the
 * documented shape (`API_CONTRACTS.md` §4, `DATABASE.md` §1).
 *
 * It checks only what the reward block actually reads: the eight `rewards`
 * members and `durationTurns`. The four "resulting value" members of the Player
 * and Pet tracks are nullable **by contract** (`DATABASE.md` §1 item 4), so
 * `null` is a valid delivered value here and is never treated as malformed — the
 * presentation renders it as unavailable (`formatRewards`). Anything else is the
 * failure state rather than data.
 */
function isDeliveredResult(result: BattleResultResponse): boolean {
  if (result === null || typeof result !== 'object') {
    return false;
  }

  if (typeof result.durationTurns !== 'number' || !Number.isFinite(result.durationTurns)) {
    return false;
  }

  const rewards: unknown = result.rewards;

  if (rewards === null || typeof rewards !== 'object') {
    return false;
  }

  const summary = rewards as unknown as Record<string, unknown>;

  const isCount = (value: unknown): boolean =>
    typeof value === 'number' && Number.isFinite(value);
  const isNullableCount = (value: unknown): boolean => value === null || isCount(value);
  const isNullableFlag = (value: unknown): boolean =>
    value === null || typeof value === 'boolean';

  return (
    isCount(summary.playerXpGained) &&
    isNullableCount(summary.newPlayerXp) &&
    isNullableFlag(summary.playerLeveledUp) &&
    isNullableCount(summary.newPlayerLevel) &&
    isCount(summary.petXpGained) &&
    isNullableCount(summary.newPetXp) &&
    isNullableFlag(summary.petLeveledUp) &&
    isNullableCount(summary.newPetLevel)
  );
}
