import '@testing-library/jest-dom';
import { vi } from 'vitest';

/**
 * The development-only authentication opt-in (TASK-181) is **off** for every test
 * unless the test turns it on itself.
 *
 * The default has to be pinned rather than inherited: the switch is read from
 * `import.meta.env.VITE_DEV_AUTH`, which a developer's untracked `.env.local` may
 * legitimately set to `'true'` while playing locally. Pinning it here keeps the
 * suite deterministic — the production behaviour a test asserts is the behaviour
 * it runs — and a development-path test opts in explicitly with
 * `vi.stubEnv('VITE_DEV_AUTH', 'true')`.
 */
vi.stubEnv('VITE_DEV_AUTH', 'false');

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