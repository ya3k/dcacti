import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH, GAME_HEIGHT } from '../GameViewport';

/**
 * Bootstrap asset-loading scene.
 *
 * Coordinates are expressed in the logical game space (1280 x 720). The Scale
 * Manager maps this space onto the real viewport, so nothing here reads
 * `window.innerWidth` / `window.innerHeight` or reacts to resize directly.
 */
export class PreloaderScene extends Phaser.Scene {
  constructor() {
    super('PreloaderScene');
  }

  preload(): void {
    // Technical asset preloading lifecycle boundary
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
      .text(GAME_WIDTH / 2, GAME_HEIGHT / 2 + 20, `${GAME_WIDTH} x ${GAME_HEIGHT} Logical Viewport`, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '14px',
        color: '#9ca3af',
      })
      .setOrigin(0.5);
  }
}