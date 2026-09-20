import type * as Phaser from 'phaser';
import type { GameRuntime } from './GameRuntime';

/**
 * Registry key under which the `GameRuntime` is published to Phaser scenes.
 *
 * Lives in its own module (rather than in `GameConfig.ts`) so scenes can read
 * the runtime without importing `GameConfig`, which itself imports the scenes.
 * That would be a circular import.
 */
export const RUNTIME_REGISTRY_KEY = 'gameRuntime';

/**
 * Reads the `GameRuntime` published by `createGameConfig`.
 *
 * Scenes depend on the `GameRuntime` port through this accessor and never
 * import SignalR or construct a runtime of their own — the boundary that keeps
 * Phaser independent of the transport implementation (task §16,
 * ARCHITECTURE.md §2.2 rule 3).
 *
 * Returns `null` when no runtime was supplied (e.g. isolated scene tests), so
 * scene lifecycle never depends on runtime availability.
 */
export function readRuntime(scene: Phaser.Scene): GameRuntime | null {
  return (scene.registry.get(RUNTIME_REGISTRY_KEY) as GameRuntime | undefined) ?? null;
}