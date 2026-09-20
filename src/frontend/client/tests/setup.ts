import '@testing-library/jest-dom';
import { vi } from 'vitest';

/**
 * Minimal Phaser 4 test double.
 *
 * Mirrors only the API surface the client actually uses, verified against the
 * installed phaser 4.2.1 typings:
 *   - Phaser.AUTO
 *   - Phaser.Scale.FIT / Phaser.Scale.CENTER_BOTH
 *   - Phaser.Game (constructed with a GameConfig)
 *   - Phaser.Scene (base class for scenes)
 *   - Phaser.Structs.Size (exposed by the Scale Manager)
 */
vi.mock('phaser', () => {
  class MockScene {
    constructor() {}
  }

  const Game = vi.fn().mockImplementation(() => ({
    destroy: vi.fn(),
  }));

  return {
    AUTO: 'AUTO',
    Scale: {
      FIT: 'FIT',
      CENTER_BOTH: 'CENTER_BOTH',
    },
    Game,
    Scene: MockScene,
    Structs: {
      Size: class MockSize {},
    },
  };
});