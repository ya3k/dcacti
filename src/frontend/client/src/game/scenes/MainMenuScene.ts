import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';

/**
 * MainMenuScene — main game menu presentation and navigation (TDD.md §2.1).
 *
 * Presentation only: renders a minimal menu and provides the two documented
 * navigation actions — `START BATTLE`, which starts `LobbyScene` and the battle
 * lifecycle, and `COLLECTION`, which opens the read-only
 * `CollectionViewerScene`. It owns no state, no authority, and performs no API
 * calls, SignalR interaction, or gameplay computation.
 */
export class MainMenuScene extends Phaser.Scene {
  /** Guards against starting the next scene more than once. */
  private hasTransitioned = false;

  constructor() {
    super('MainMenuScene');
  }

  create(): void {
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

  /** Starts the given scene, at most once per scene instance. */
  private transitionTo(sceneKey: string): void {
    if (this.hasTransitioned) {
      return;
    }
    this.hasTransitioned = true;
    this.scene.start(sceneKey);
  }

  shutdown(): void {
    // No subscriptions to clean up — this scene is purely presentational.
  }
}
