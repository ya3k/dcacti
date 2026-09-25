import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
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
  /**
   * The board container's current children, in draw order. It models the real
   * container: `removeAll` clears it, so a redraw reflects the latest push.
   */
  const boardCells: Array<{ kind: string; label?: string }> = [];
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
    /** A text object. When `into` is given, the object is a board child. */
    const makeText = (value: string, into?: { kind: string; label: string }[]) => {
      const entry = { text: value, color: undefined as string | undefined };
      texts.push(entry);

      const obj = {
        kind: 'label' as const,
        label: value,
        setOrigin: () => obj,
        setText: (next: string) => {
          entry.text = next;
          return obj;
        },
        setColor: (next: string) => {
          entry.color = next;
          return obj;
        },
      };

      // Board labels are created through the same `add.text` factory as scene
      // readouts, so the distinction is made at the call site in the scene: the
      // scene adds board children to its container explicitly.
      void into;
      return obj;
    };

    /** A container that models child ownership, including `removeAll`. */
    const makeContainer = () => {
      let children: Array<{ kind: string; label?: string }> = [];

      const obj = {
        add: (added: unknown) => {
          const list = Array.isArray(added) ? added : [added];
          children = children.concat(list as Array<{ kind: string; label?: string }>);
          syncBoard();
          return obj;
        },
        removeAll: () => {
          children = [];
          syncBoard();
          return obj;
        },
        destroy: () => {
          children = [];
          syncBoard();
        },
      };

      const syncBoard = () => {
        boardCells.length = 0;
        boardCells.push(...children);
      };

      return obj;
    };

    return Object.assign(Object.create(scene), {
      scene: {
        start: (key: string, data?: unknown) => sceneStarted.push({ key, data }),
      },
      add: {
        rectangle: () => {
          const rect = { kind: 'tile', setStrokeStyle: () => rect };
          return rect;
        },
        text: (_x: number, _y: number, value: string) => makeText(value),
        container: () => makeContainer(),
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
    boardCells,
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

  it('contains no gameplay interaction or resolution presentation', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    // No gameplay: no swap interaction, match, cascade, combo, damage, or boss
    // readout. (A board is presented once the server pushes one — see the Board
    // Foundation block below — but the scene resolves nothing.)
    const rendered = harness.texts.map((t) => t.text).join(' ');
    for (const forbidden of ['Boss', 'Combo', 'Damage', 'Cascade', 'Match ', 'Swap']) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }
  });
});

describe('BattleScene — Board Foundation presentation (GAME_STATE.md §2.0.5)', () => {
  /** The four documented Gem contract names (MATCH3_RULES.md §1.1). */
  const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];

  /** A server-shaped board: exactly 64 cells in the documented order. */
  function serverBoard(): string[] {
    return Array.from({ length: 64 }, (_, index) => GEM_NAMES[index % GEM_NAMES.length]);
  }

  function serverState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return {
      battleId: 'battle-1',
      turn: 0,
      sequence: 0,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: { cells: serverBoard() },
      // GAME_STATE.md §2.2: both values exist from battle creation and are always
      // delivered — including at 0, which is a value, not an absence.
      playerState: { combo: 0, matchCount: 0 },
      // SIGNALR_PROTOCOL.md §4.3: exactly the Passive trio. The delivered
      // `current / threshold` pair is always present; `passiveResetOverride` is
      // omitted for the default reset, which is the documented representation
      // (§4.3 items 4, 7).
      petState: {
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
      },
      ...overrides,
    };
  }

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

  it('renders the board foundation state the server delivered', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('BattleId: battle-1');
    expect(rendered).toMatch(/Turn: 0\b/);
    expect(rendered).toMatch(/Sequence: 0\b/);
  });

  it('renders the documented initial values verbatim', () => {
    // GAME_STATE.md §2.0.5.2 item 1: board generation is not an action
    // resolution, so Turn and Sequence stay 0.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/Turn: 0\b/);
    expect(rendered).toMatch(/Sequence: 0\b/);
  });

  it('renders the delivered Passive progress pair verbatim', () => {
    // SIGNALR_PROTOCOL.md §4.3 / PASSIVE_RULES.md §6 item 1: the delivered
    // `current / threshold` pair is presented as the UI-facing value. The scene
    // prints both numbers as received — it charges nothing, evaluates no
    // Threshold, and resets nothing (§4.3 item 9).
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      petState: {
        passiveId: 'thanh-xa-poison',
        passiveProgress: { threshold: 7, current: 3 },
      },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('thanh-xa-poison');
    expect(rendered).toContain('3 / 7');
    // The member's absence is the documented spelling of the default reset
    // (§4.3 item 7) — stated as such, with no value invented for it.
    expect(rendered).toContain('reset: Default');
  });

  it('renders the delivered non-default reset override contract name', () => {
    // §4.3 item 6: when the Passive declares a non-default behavior the wire
    // member carries its contract name — `"Partial"` or `"NoReset"`.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      petState: {
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 4 },
        passiveResetOverride: 'Partial',
      },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('4 / 5');
    expect(rendered).toContain('reset: Partial');
  });

  it('renders the 8x8 board as exactly 64 cells', () => {
    // GAME_STATE.md §2.1.1 / MATCH3_RULES.md §1.0: 8 x 8 = 64 cells.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const labels = harness.boardCells.filter((c) => c.kind === 'label');
    expect(labels).toHaveLength(64);
  });

  it('presents only the four documented Gem types', () => {
    // MATCH3_RULES.md §1.1: ATK, DEF, HP, POWER. The scene maps each server Gem
    // name to a placeholder — it never invents a type.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const labels = harness.boardCells.filter((c) => c.kind === 'label').map((c) => c.label);

    // All four documented types are drawn (PWR is the placeholder abbreviation
    // for POWER), and nothing outside the documented set appears.
    expect(new Set(labels)).toEqual(new Set(['ATK', 'DEF', 'HP', 'PWR']));
    expect(labels.every((label) => ['ATK', 'DEF', 'HP', 'PWR'].includes(label!))).toBe(true);
  });

  it('renders the gems the server sent, in the server order', () => {
    // The scene presents `Cells[64]` verbatim: cell i is the i-th value of the
    // payload. It reorders, substitutes, and generates nothing.
    const cells = serverBoard();
    cells[0] = 'POWER';
    cells[63] = 'ATK';

    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      board: { cells },
    }));

    runScene(scene, ctx, 'create');

    const labels = harness.boardCells.filter((c) => c.kind === 'label').map((c) => c.label);
    expect(labels[0]).toBe('PWR'); // POWER
    expect(labels[63]).toBe('ATK');
  });

  it('draws one tile per cell', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const tiles = harness.boardCells.filter((c) => c.kind === 'tile');
    expect(tiles).toHaveLength(64);
  });

  it('renders nothing until the server pushes state', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('BattleId:');
    expect(rendered).not.toMatch(/Turn:/);
    expect(rendered).not.toMatch(/Sequence:/);

    // And no board is drawn: the client never generates one to fill the gap
    // (SIGNALR_PROTOCOL.md §4 item 10).
    expect(harness.boardCells).toHaveLength(0);
  });

  it('updates the readout and board when the runtime receives new state', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    harness.setBattleState(serverState({ battleId: 'battle-9' }));

    // The scene displays what the runtime received — it decides nothing.
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('BattleId: battle-9');
    expect(harness.boardCells.filter((c) => c.kind === 'label')).toHaveLength(64);
  });

  it('does not own the state: it renders unchanged server values', () => {
    // The scene never computes, adjusts, or recomputes these values
    // (SIGNALR_PROTOCOL.md §4.9, ADR-001).
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      battleId: 'battle-owned-by-server',
      turn: 7,
      sequence: 12,
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Turn: 7');
    expect(rendered).toContain('Sequence: 12');
  });

  it('does not mutate the board it was given', () => {
    // The board is server-authored and the client never repairs it
    // (GAME_STATE.md §2.0.5.4.1).
    const state = serverState();
    const before = [...state.board.cells];

    const { scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, state);
    runScene(scene, ctx, 'create');

    expect(state.board.cells).toEqual(before);
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

  it('shutdown clears the rendered board', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');
    expect(harness.boardCells.length).toBeGreaterThan(0);

    runScene(scene, ctx, 'shutdown');

    expect(harness.boardCells).toHaveLength(0);
  });

  it('survives a scene with no runtime supplied', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, false);

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('BattleId:');
    expect(harness.boardCells).toHaveLength(0);
  });

  it('presents no lifecycle or status value', () => {
    // GAME_STATE.md §2.0.3 / SIGNALR_PROTOCOL.md §8.3: no Status and no
    // READY/STARTING/ACTIVE/PAUSED/FINISHED/WON/LOST exists in this contract.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join(' ');
    expect(rendered).not.toMatch(/READY/i);
    for (const forbidden of ['Status', 'STARTING', 'ACTIVE', 'PAUSED', 'FINISHED', 'WON', 'LOST']) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }
  });

  it('presents no gameplay system state alongside the board', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    // The board and its four Gem types are part of this stage; the later-stage
    // gameplay systems are not (GAME_STATE.md §2.0.5.3).
    const rendered = harness.texts.map((t) => t.text).join(' ');
    for (const forbidden of ['Boss', 'Combo', 'Damage', 'Pet', 'Card', 'Relic', 'PendingSpecial']) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }
  });

  it('contains no client-side randomness', () => {
    // SIGNALR_PROTOCOL.md §4 item 10 / GAME_RULES.md §18: no client-side RNG
    // participates in any part of the board.
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
      'utf8'
    );

    expect(source).not.toMatch(/Math\.random/);
    expect(source).not.toMatch(/crypto\./);
  });
});