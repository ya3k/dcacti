import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH, GAME_HEIGHT } from '../GameViewport';

/**
 * PreloaderScene — asset-loading infrastructure and lifecycle only
 * (TDD.md §2.1).
 *
 * It owns the loading lifecycle boundary and the transition to `BattleScene`.
 * No real gameplay assets exist yet, and the scene is written so it works
 * correctly with zero assets to load: `preload()` registers nothing, the
 * `complete` handler still fires, and the scene still transitions.
 *
 * Coordinates are expressed in the logical game space (1280 x 720). The Scale
 * Manager maps this space onto the real viewport, so nothing here reads
 * `window.innerWidth` / `window.innerHeight` or reacts to resize directly.
 */
export class PreloaderScene extends Phaser.Scene {
  /** Guards against starting the next scene more than once. */
  private hasTransitioned = false;

  constructor() {
    super('PreloaderScene');
  }

  preload(): void {
    // Asset-loading lifecycle boundary. No gameplay assets are defined yet;
    // an empty load list completes immediately and still emits `complete`.
    this.load.once(Phaser.Loader.Events.COMPLETE, () => {
      this.onLoadingComplete();
    });
  }

  create(): void {
    // Technical placeholder proving the Phaser engine runtime is operational.
    // Drawn inside the safe area so it never sits against the viewport edge.
    const bg = this.add.rectangle(
      SAFE_AREA.x + SAFE_AREA.width / 2,
      SAFE_AREA.y + SAFE_AREA.height / 2,
      SAFE_AREA.width,
      SAFE_AREA.height,
      0x1a1d24
    );
    bg.setStrokeStyle(2, 0x3b82f6);

    this.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT / 2 - 20, 'Phaser Engine Running', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '22px',
        color: '#60a5fa',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    this.add
      .text(GAME_WIDTH / 2, GAME_HEIGHT / 2 + 20, 'Loading runtime assets…', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '14px',
        color: '#9ca3af',
      })
      .setOrigin(0.5);

    // Nothing to load: hand off to the battle runtime shell immediately.
    // When real assets are added, the `complete` handler performs this
    // transition instead.
    if (!this.load.isLoading()) {
      this.onLoadingComplete();
    }
  }

  /**
   * Loading lifecycle boundary → transition to the battle runtime shell.
   *
   * Guarded so the transition happens at most once: with an empty load queue
   * `create()` completes loading directly while the `complete` handler is also
   * registered, and both paths would otherwise start `BattleScene` twice.
   */
  private onLoadingComplete(): void {
    if (this.hasTransitioned) {
      return;
    }
    this.hasTransitioned = true;

    this.scene.start('BattleScene');
  }
}