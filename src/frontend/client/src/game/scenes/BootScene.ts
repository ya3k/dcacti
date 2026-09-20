import * as Phaser from 'phaser';
import { readRuntime } from '../runtime/RuntimeRegistry';

/**
 * BootScene — technical initialization only (TDD.md §2.1).
 *
 * Responsibilities are limited to the runtime configuration and technical
 * service availability needed before anything else runs, then handing off to
 * `PreloaderScene`. It initializes no gameplay.
 *
 * It records the engine lifecycle on the runtime so the runtime's status
 * reflects the Phaser engine being operational, and performs no gameplay work.
 */
export class BootScene extends Phaser.Scene {
  constructor() {
    super('BootScene');
  }

  preload(): void {
    // Technical initialization only — no asset loading here.
  }

  create(): void {
    // Technical service availability: the Phaser engine is up and running.
    readRuntime(this)?.setEngineStatus('running');

    this.scene.start('PreloaderScene');
  }
}