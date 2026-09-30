import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';

/**
 * MainMenuScene — main game menu presentation and navigation (TDD.md §2.1).
 *
 * Presentation only: renders a minimal menu and provides the documented
 * navigation action that starts `LobbyScene`. It owns no state, no authority,
 * and performs no API calls, SignalR interaction, or gameplay computation.
 */
export class MainMenuScene extends Phaser.Scene {
  /** Guards against starting LobbyScene more than once. */
  private hasTransitioned = false;

  constructor() {
    super('MainMenuScene');
  }

  create(): void {
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

    // Navigation button label.
    const buttonX = GAME_WIDTH / 2;
    const buttonY = SAFE_AREA.y + 300;
    const buttonWidth = 280;
    const buttonHeight = 50;

    const bg = this.add.rectangle(buttonX, buttonY, buttonWidth, buttonHeight, 0x1d4ed8)
      .setStrokeStyle(1, 0x60a5fa);
    bg.setInteractive({ useHandCursor: true });

    bg.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, () => {
      this.startBattle();
    });

    this.add.text(buttonX, buttonY, 'START BATTLE', {
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
    if (this.hasTransitioned) {
      return;
    }
    this.hasTransitioned = true;
    this.scene.start('LobbyScene');
  }

  shutdown(): void {
    // No subscriptions to clean up — this scene is purely presentational.
  }
}
