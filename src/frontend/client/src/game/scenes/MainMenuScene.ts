import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';

/**
 * MainMenuScene — main game menu presentation and navigation (TDD.md §2.1).
 *
 * Presentation only: renders a minimal menu and provides the three documented
 * navigation actions — `START BATTLE`, which starts `LobbyScene` and the battle
 * lifecycle, `COLLECTION`, which opens the read-only `CollectionViewerScene`,
 * and `BATTLE HISTORY`, which opens the read-only `BattleHistoryScene`. It owns
 * no state, no authority, and performs no API calls, SignalR interaction, or
 * gameplay computation.
 */
export class MainMenuScene extends Phaser.Scene {
  /** Guards against starting the next scene more than once. */
  private hasTransitioned = false;

  constructor() {
    super('MainMenuScene');
  }

  create(): void {
    // Attach this scene's teardown to Phaser's own scene lifecycle events.
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
    // `.ai/skills/phaser/scenes` gotcha 14); without it the teardown below is
    // dead code in the browser.
    //
    // The registration is idempotent — the pair is detached first, then attached
    // with `once` — so a scene that is shut down and later restarted holds
    // exactly one teardown handler per event instead of stacking another one on
    // every run (`once` alone would leave the run's unfired `DESTROY` handler
    // behind). This is the same "detach before attach" idempotency
    // `BattleScene.registerBoardInput` already uses, and `once` means the
    // handler is consumed by the event that fired it.
    this.events.off(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.off(Phaser.Scenes.Events.DESTROY, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.DESTROY, this.shutdown, this);

    this.hasTransitioned = false;
    this.drawMenu();
  }

  /** Draws the main menu UI elements. */
  private drawMenu(): void {
    // Background fill inside the safe area.
    this.add.rectangle(
      SAFE_AREA.x + SAFE_AREA.width / 2,
      SAFE_AREA.y + SAFE_AREA.height / 2,
      SAFE_AREA.width,
      SAFE_AREA.height,
      0x0f172a
    ).setStrokeStyle(2, 0x334155);

    // Title.
    this.add.text(
      GAME_WIDTH / 2,
      SAFE_AREA.y + 180,
      'DCACTI',
      {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '48px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      }
    ).setOrigin(0.5);

    const buttonX = GAME_WIDTH / 2;
    const buttonWidth = 280;
    const buttonHeight = 50;

    // START BATTLE — the battle lifecycle's entry point. Its coordinates are
    // unchanged: `standalone-web-smoke.mjs` clicks this exact centre.
    this.drawButton(buttonX, SAFE_AREA.y + 300, buttonWidth, buttonHeight, 'START BATTLE', () =>
      this.startBattle()
    );

    // COLLECTION — the read-only meta-progression viewer. Placed below START
    // BATTLE with a clear gap, so the two click targets never overlap and the
    // battle entry stays where the automated smoke test expects it.
    this.drawButton(
      buttonX,
      SAFE_AREA.y + 370,
      buttonWidth,
      buttonHeight,
      'COLLECTION',
      () => this.openCollection()
    );

    // BATTLE HISTORY — the read-only completed-battle and account-progression
    // surface. It sits one button pitch below COLLECTION, so no hit area
    // overlaps another and the two existing entries keep their own coordinates:
    // `START BATTLE` stays at (640, 324), which `standalone-web-smoke.mjs`
    // clicks, and `COLLECTION` stays at (640, 394).
    this.drawButton(
      buttonX,
      SAFE_AREA.y + 440,
      buttonWidth,
      buttonHeight,
      'BATTLE HISTORY',
      () => this.openBattleHistory()
    );
  }

  /** Draws one interactive menu button using the menu's own visual conventions. */
  private drawButton(
    x: number,
    y: number,
    width: number,
    height: number,
    label: string,
    onActivate: () => void
  ): void {
    const background = this.add
      .rectangle(x, y, width, height, 0x1d4ed8)
      .setStrokeStyle(1, 0x60a5fa);
    background.setInteractive({ useHandCursor: true });
    background.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, onActivate);

    this.add.text(x, y, label, {
      fontFamily: 'system-ui, sans-serif',
      fontSize: '20px',
      color: '#e2e8f0',
      fontStyle: 'bold',
    }).setOrigin(0.5);
  }

  /**
   * Documented navigation action — starts `LobbyScene`.
   *
   * Guarded so it fires at most once per scene instance, mirroring the
   * pattern used in `PreloaderScene`.
   */
  private startBattle(): void {
    this.transitionTo('LobbyScene');
  }

  /** Navigation action — opens the read-only `CollectionViewerScene`. */
  private openCollection(): void {
    this.transitionTo('CollectionViewerScene');
  }

  /**
   * Navigation action — opens the read-only `BattleHistoryScene`.
   *
   * It uses the same single-use transition as the other two entries, so
   * whichever navigation action fires first claims this scene instance and no
   * second scene can be started from the same presentation.
   */
  private openBattleHistory(): void {
    this.transitionTo('BattleHistoryScene');
  }

  /** Starts the given scene, at most once per scene instance. */
  private transitionTo(sceneKey: string): void {
    if (this.hasTransitioned) {
      return;
    }
    this.hasTransitioned = true;
    this.scene.start(sceneKey);
  }

  /**
   * Scene teardown — the handler subscribed to Phaser's own
   * `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY` events by `create()`.
   *
   * It is a plain method invoked *by the engine's event*, not an engine hook:
   * Phaser never calls a scene method named `shutdown` on its own.
   *
   * Ownership audit: the only thing this scene owns is its per-run navigation
   * guard. It subscribes to no `GameRuntime` stream, registers no timer or
   * tween, holds no runtime, battle, or preserved-loadout value, and its game
   * objects belong to the scene's `DisplayList`, which Phaser destroys by
   * itself (`DisplayList#shutdown` handles the same event). Resetting
   * `hasTransitioned` here is the same per-run reset `create()` performs, so a
   * reused instance carries no claim on a transition into the next
   * presentation; the reset is idempotent, so running it on both events or
   * twice on one event is harmless.
   */
  shutdown(): void {
    this.hasTransitioned = false;
  }
}
