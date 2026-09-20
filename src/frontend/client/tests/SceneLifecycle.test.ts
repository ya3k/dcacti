import { describe, it, expect, vi, beforeEach } from 'vitest';
import { BattleScene } from '../src/game/scenes/BattleScene';
import { BootScene } from '../src/game/scenes/BootScene';
import { PreloaderScene } from '../src/game/scenes/PreloaderScene';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { GameRuntimeState } from '../src/state/GameRuntimeState';
import type { RuntimeBattleState } from '../src/game/runtime/GameRuntimeEvents';

vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({ destroy: vi.fn() })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
  Loader: { Events: { COMPLETE: 'complete' } },
}));

/**
 * Scene harness.
 *
 * Phaser's real `Scene` cannot run headlessly in jsdom, and its `add` / `load` /
 * `scene` / `registry` members are prototype getters that cannot be assigned
 * onto an instance. The harness therefore builds a `this` context whose
 * prototype is the real scene instance, so the scene's own private helpers
 * resolve, and overrides only the Phaser collaborators the scenes call. This is
 * how Phaser itself drives a scene (via the Scene Systems).
 *
 * It verifies scene lifecycle coordination and the Phaser to GameRuntime
 * boundary without asserting on rendering internals.
 */
interface SceneHarnessOptions {
  state?: GameRuntimeState;
  withRuntime?: boolean;
  battleState?: RuntimeBattleState | null;
}

function createSceneHarness(options: SceneHarnessOptions = {}) {
  const { state = INITIAL_RUNTIME_STATE, withRuntime = true, battleState = null } = options;
  const listeners = new Set<(event: { state: GameRuntimeState }) => void>();
  const battleStateListeners = new Set<(state: RuntimeBattleState) => void>();
  const texts: Array<{ text: string; color?: string }> = [];
  const sceneStarted: Array<{ key: string; data?: unknown }> = [];
  const loadHandlers = new Map<string, () => void>();
  const setEngineStatus = vi.fn();
  let loading = false;
  let currentBattleState = battleState;

  const runtime = {
    getState: () => state,
    onRuntimeEvent: (listener: (event: { state: GameRuntimeState }) => void) => {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getBattleState: () => currentBattleState,
    onBattleState: (listener: (state: RuntimeBattleState) => void) => {
      battleStateListeners.add(listener);
      return () => {
        battleStateListeners.delete(listener);
      };
    },
    setEngineStatus,
  };

  /** Builds the `this` context for a scene instance. */
  function context(scene: object, sceneKey: string): object {
    const makeText = () => {
      const record: { text: string; color?: string } = { text: '' };
      texts.push(record);
      const obj = {
        setOrigin: () => obj,
        setText: (value: string) => {
          record.text = value;
          return obj;
        },
        setColor: (value: string) => {
          record.color = value;
          return obj;
        },
      };
      return obj;
    };

    return Object.assign(Object.create(scene), {
      scene: {
        start: (key: string, data?: unknown) => sceneStarted.push({ key, data }),
      },
      add: {
        rectangle: () => ({ setStrokeStyle: () => undefined }),
        text: () => makeText(),
      },
      load: {
        once: (event: string, handler: () => void) => {
          loadHandlers.set(event, handler);
        },
        isLoading: () => loading,
      },
      registry: {
        get: (key: string) =>
          withRuntime && key === RUNTIME_REGISTRY_KEY ? runtime : undefined,
      },
      sys: { settings: { key: sceneKey } },
    });
  }

  return {
    runtime,
    setEngineStatus,
    texts,
    listeners,
    battleStateListeners,
    sceneStarted,
    loadHandlers,
    setBattleState: (next: RuntimeBattleState) => {
      currentBattleState = next;
      for (const listener of battleStateListeners) {
        listener(next);
      }
    },
    setLoading: (value: boolean) => {
      loading = value;
    },
    context,
  };
}

/** Invokes a scene method with the harness context, as Phaser itself would. */
function runScene(scene: object, ctx: object, method: string): void {
  const fn = Object.getPrototypeOf(scene)[method] as (this: object) => void;
  fn.call(ctx);
}

describe('Phaser scene lifecycle', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('exposes the documented scene classes', () => {
    expect(new BootScene()).toBeInstanceOf(BootScene);
    expect(new PreloaderScene()).toBeInstanceOf(PreloaderScene);
    expect(new BattleScene()).toBeInstanceOf(BattleScene);
  });
});

describe('BootScene', () => {
  it('performs technical init and transitions to PreloaderScene', () => {
    const harness = createSceneHarness();
    const boot = new BootScene();

    runScene(boot, harness.context(boot, 'BootScene'), 'create');

    expect(harness.setEngineStatus).toHaveBeenCalledWith('running');
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['PreloaderScene']);
  });

  it('loads no assets in the boot phase', () => {
    const harness = createSceneHarness();
    const boot = new BootScene();

    runScene(boot, harness.context(boot, 'BootScene'), 'preload');

    expect(harness.loadHandlers.size).toBe(0);
  });
});

describe('PreloaderScene', () => {
  it('registers the loading lifecycle boundary and transitions with zero assets', () => {
    const harness = createSceneHarness();
    const preloader = new PreloaderScene();
    const ctx = harness.context(preloader, 'PreloaderScene');

    runScene(preloader, ctx, 'preload');
    runScene(preloader, ctx, 'create');

    // The loading lifecycle boundary is registered...
    expect(harness.loadHandlers.has('complete')).toBe(true);
    // ...and the scene still advances correctly with nothing to load.
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
  });

  it('advances via the load-complete handler while assets are loading', () => {
    const harness = createSceneHarness();
    const preloader = new PreloaderScene();
    const ctx = harness.context(preloader, 'PreloaderScene');
    harness.setLoading(true);

    runScene(preloader, ctx, 'preload');
    runScene(preloader, ctx, 'create');

    // Nothing transitions until loading completes.
    expect(harness.sceneStarted).toHaveLength(0);

    harness.loadHandlers.get('complete')?.();

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
  });

  it('transitions to BattleScene exactly once', () => {
    const harness = createSceneHarness();
    const preloader = new PreloaderScene();
    const ctx = harness.context(preloader, 'PreloaderScene');

    runScene(preloader, ctx, 'preload');
    runScene(preloader, ctx, 'create');
    harness.loadHandlers.get('complete')?.();

    expect(harness.sceneStarted).toHaveLength(1);
  });
});

describe('BattleScene', () => {
  function createBattle(
    state: GameRuntimeState = INITIAL_RUNTIME_STATE,
    withRuntime = true
  ) {
    const harness = createSceneHarness({ state, withRuntime });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  it('creates the runtime shell without throwing', () => {
    const { harness, scene, ctx } = createBattle();

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
    expect(harness.texts.length).toBeGreaterThan(0);
  });

  it('renders a connected runtime as ready', () => {
    const { harness, scene, ctx } = createBattle({
      ...INITIAL_RUNTIME_STATE,
      connection: 'connected',
      connectionId: 'abc',
      runtime: 'ready',
      sync: 'awaiting_battle',
      engine: 'running',
    });

    runScene(scene, ctx, 'create');

    expect(harness.texts.map((t) => t.text)).toContain('Runtime Connected');
  });

  it('renders an unconnected runtime as waiting, not as ready', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.texts.map((t) => t.text)).toContain('Runtime Waiting for Connection');
    expect(harness.texts.map((t) => t.text)).not.toContain('Runtime Connected');
  });

  it('renders a failed connection as unavailable', () => {
    const { harness, scene, ctx } = createBattle({
      ...INITIAL_RUNTIME_STATE,
      connection: 'error',
      runtime: 'error',
    });

    runScene(scene, ctx, 'create');

    expect(harness.texts.map((t) => t.text)).toContain('Runtime Unavailable');
  });

  it('renders the reconnecting state distinctly', () => {
    const { harness, scene, ctx } = createBattle({
      ...INITIAL_RUNTIME_STATE,
      connection: 'reconnecting',
      runtime: 'suspended',
    });

    runScene(scene, ctx, 'create');

    expect(harness.texts.map((t) => t.text)).toContain('Runtime Reconnecting…');
  });

  it('survives a scene with no runtime supplied', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, false);

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
    expect(harness.texts.map((t) => t.text)).toContain('Runtime Unavailable');
  });

  it('updates its readout when the runtime state changes', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.texts.map((t) => t.text)).toContain('Runtime Waiting for Connection');

    const connected: GameRuntimeState = {
      ...INITIAL_RUNTIME_STATE,
      connection: 'connected',
      connectionId: 'c1',
      runtime: 'ready',
      sync: 'awaiting_battle',
    };
    for (const listener of harness.listeners) {
      listener({ state: connected });
    }

    expect(harness.texts.map((t) => t.text)).toContain('Runtime Connected');
  });

  it('subscribes to the runtime exactly once', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.listeners.size).toBe(1);
  });

  it('detaches from the runtime on shutdown', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.listeners.size).toBe(1);

    runScene(scene, ctx, 'shutdown');

    expect(harness.listeners.size).toBe(0);
  });

  it('shutdown is safe when the scene never created its objects', () => {
    const { scene, ctx } = createBattle();
    expect(() => runScene(scene, ctx, 'shutdown')).not.toThrow();
  });

  it('contains no gameplay presentation', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    // Runtime shell only: no board, gems, HP, Power, damage, or boss readout.
    const rendered = harness.texts.map((t) => t.text).join(' ');
    for (const forbidden of ['HP', 'Boss', 'Power', 'Combo', 'Damage', 'Gem', 'Board']) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }
  });
});

describe('BattleScene — Battle State Foundation presentation (GAME_STATE.md §2.0)', () => {
  function createBattle(
    state: GameRuntimeState = INITIAL_RUNTIME_STATE,
    withRuntime = true,
    battleState: RuntimeBattleState | null = null
  ) {
    const harness = createSceneHarness({ state, withRuntime, battleState });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  it('renders the foundation state the server delivered', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, {
      battleId: 'battle-1',
      turn: 0,
      sequence: 0,
    });

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('BattleId: battle-1');
    expect(rendered).toContain('Turn: 0');
    expect(rendered).toContain('Sequence: 0');
  });

  it('renders the documented initial values verbatim', () => {
    // GAME_STATE.md §2.0.2: Turn = 0, Sequence = 0 for a battle with no
    // resolved action.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, {
      battleId: 'battle-1',
      turn: 0,
      sequence: 0,
    });

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/Turn: 0\b/);
    expect(rendered).toMatch(/Sequence: 0\b/);
  });

  it('renders nothing until the server pushes state', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('BattleId:');
    expect(rendered).not.toContain('Turn:');
    expect(rendered).not.toContain('Sequence:');
  });

  it('updates the readout when the runtime receives new state', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    harness.setBattleState({ battleId: 'battle-9', turn: 0, sequence: 0 });

    // The scene displays what the runtime received — it decides nothing.
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('BattleId: battle-9');
  });

  it('does not own the state: it renders an unchanged server value', () => {
    // The scene never computes, adjusts, or recomputes these values
    // (SIGNALR_PROTOCOL.md §4.9, ADR-001). A non-initial value is displayed
    // exactly as received.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, {
      battleId: 'battle-owned-by-server',
      turn: 7,
      sequence: 12,
    });

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Turn: 7');
    expect(rendered).toContain('Sequence: 12');
  });

  it('subscribes to the battle-state push exactly once', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.battleStateListeners.size).toBe(1);
  });

  it('detaches from the battle-state push on shutdown', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.battleStateListeners.size).toBe(1);

    runScene(scene, ctx, 'shutdown');

    expect(harness.battleStateListeners.size).toBe(0);
  });

  it('survives a scene with no runtime supplied', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, false);

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('BattleId:');
  });

  it('presents no lifecycle or status value', () => {
    // GAME_STATE.md §2.0.3 / SIGNALR_PROTOCOL.md §8.3: no Status and no
    // READY/STARTING/ACTIVE/PAUSED/FINISHED/WON/LOST exists in this contract.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, {
      battleId: 'battle-1',
      turn: 0,
      sequence: 0,
    });

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join(' ');
    expect(rendered).not.toMatch(/READY/i);
    for (const forbidden of ['Status', 'STARTING', 'ACTIVE', 'PAUSED', 'FINISHED', 'WON', 'LOST']) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }
  });

  it('presents no gameplay state alongside the foundation readout', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, {
      battleId: 'battle-1',
      turn: 0,
      sequence: 0,
    });

    runScene(scene, ctx, 'create');

    // Foundation readout only: no board, gems, HP, Power, combo, or boss.
    const rendered = harness.texts.map((t) => t.text).join(' ');
    for (const forbidden of ['HP', 'Boss', 'Power', 'Combo', 'Damage', 'Gem', 'Board', 'Pet', 'Card', 'Relic']) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }
  });
});