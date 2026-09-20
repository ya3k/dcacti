import * as Phaser from 'phaser';

export class BootScene extends Phaser.Scene {
  constructor() {
    super('BootScene');
  }

  preload(): void {
    // Technical initialization only — no asset loading here
  }

  create(): void {
    this.scene.start('PreloaderScene');
  }
}
