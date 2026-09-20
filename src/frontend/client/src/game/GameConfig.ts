import * as Phaser from 'phaser';
import { BootScene } from './scenes/BootScene';
import { PreloaderScene } from './scenes/PreloaderScene';
import { GAME_WIDTH, GAME_HEIGHT } from './GameViewport';

/**
 * Creates the Phaser 4 game configuration for the Discord Activity viewport.
 *
 * Scaling is delegated entirely to Phaser's Scale Manager (verified against the
 * installed phaser 4.2.1 typings):
 *
 * - `mode: Phaser.Scale.FIT`           scale the logical resolution to the
 *                                      largest size that fits the parent while
 *                                      preserving aspect ratio (no distortion).
 * - `autoCenter: CENTER_BOTH`          center the canvas within the parent.
 * - `autoRound: true`                  floor display/style sizes, avoiding
 *                                      fractional-pixel blur and scrollbars.
 * - `expandParent: false`              never let Phaser resize the parent or
 *                                      document body; the GameShell owns layout.
 *
 * Phaser 4 has no `resolution` / `devicePixelRatio` game-config option (the
 * ScaleConfig typings expose width, height, zoom, parent, expandParent, mode,
 * min, max, snap, autoRound, autoCenter, resizeInterval, fullscreenTarget).
 * The canvas backing store therefore tracks `baseSize` (see
 * `WebGLRenderer.resize`) rather than a multiplied device pixel ratio. Sharpness
 * is preserved by scaling the canvas with CSS while rendering at the logical
 * resolution; see the high-DPI note in the task documentation.
 */
export function createGameConfig(parent: HTMLElement): Phaser.Types.Core.GameConfig {
  return {
    type: Phaser.AUTO,
    parent,
    width: GAME_WIDTH,
    height: GAME_HEIGHT,
    backgroundColor: '#0b0f19',
    scale: {
      mode: Phaser.Scale.FIT,
      autoCenter: Phaser.Scale.CENTER_BOTH,
      autoRound: true,
      expandParent: false,
    },
    scene: [BootScene, PreloaderScene],
  };
}