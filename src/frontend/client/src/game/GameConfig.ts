import * as Phaser from 'phaser';
import { BootScene } from './scenes/BootScene';
import { PreloaderScene } from './scenes/PreloaderScene';
import { BattleScene } from './scenes/BattleScene';
import { GAME_WIDTH, GAME_HEIGHT } from './GameViewport';
import type { GameRuntime } from './runtime/GameRuntime';
import { RUNTIME_REGISTRY_KEY } from './runtime/RuntimeRegistry';

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
 *
 * Scene lifecycle (TDD.md §2.1, task §5):
 *
 *   BootScene → PreloaderScene → BattleScene
 *
 * `MainMenuScene`, `LobbyScene`, and `ResultScene` are documented in
 * `TDD.md` §2.1 / `ARCHITECTURE.md` §1 as part of the eventual full lifecycle,
 * but they are menu/navigation presentation owned by later tasks. Only the
 * scenes the runtime foundation requires are registered here.
 *
 * The `runtime` is injected into Phaser's game-wide registry (`game.registry`,
 * a `Phaser.Data.DataManager`) from the `postBoot` hook — the point at which
 * the game has booted and `registry` is available. Scenes read it back through
 * `this.registry`, which is the same game-wide DataManager, so scenes depend on
 * the `GameRuntime` port and never on SignalR (task §16).
 */
export function createGameConfig(
  parent: HTMLElement,
  runtime: GameRuntime | null = null
): Phaser.Types.Core.GameConfig {
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
    scene: [BootScene, PreloaderScene, BattleScene],
    callbacks: {
      // Runs at the end of the boot sequence: all game systems (including the
      // registry and Scene Manager) are live, and the first scene has not yet
      // created its game objects.
      postBoot: (game) => {
        game.registry.set(RUNTIME_REGISTRY_KEY, runtime);
      },
    },
  };
}