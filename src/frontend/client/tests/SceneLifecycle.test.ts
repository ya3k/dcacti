import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { BattleScene } from '../src/game/scenes/BattleScene';
import { BootScene } from '../src/game/scenes/BootScene';
import { MainMenuScene } from '../src/game/scenes/MainMenuScene';
import { PreloaderScene } from '../src/game/scenes/PreloaderScene';
import { ResultScene } from '../src/game/scenes/ResultScene';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { GAME_WIDTH, SAFE_AREA } from '../src/game/GameViewport';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { GameRuntimeState } from '../src/state/GameRuntimeState';
import type { BattleEventsEnvelope, RuntimeBattleState } from '../src/game/runtime/GameRuntimeEvents';

vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({ destroy: vi.fn() })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
  Loader: { Events: { COMPLETE: 'complete' } },
  // The board layer registers its pointer input through this event constant
  // (Phaser's own `gameobjectdown` name).
  Input: { Events: { GAMEOBJECT_POINTER_DOWN: 'gameobjectdown' } },
  // The board's hit area is a real `Phaser.Geom.Rectangle`. The mock keeps the
  // constructor and its `Contains` predicate faithful, because the hit area's
  // geometry is exactly what the board input-target tests assert on.
  Geom: {
    Rectangle: class MockRectangle {
      constructor(x: number, y: number, width: number, height: number) {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
      }
      readonly x: number;
      readonly y: number;
      readonly width: number;
      readonly height: number;
      static Contains(
        rect: { x: number; y: number; width: number; height: number },
        x: number,
        y: number
      ): boolean {
        return (
          rect.width > 0 &&
          rect.height > 0 &&
          rect.x <= x &&
          x <= rect.x + rect.width &&
          rect.y <= y &&
          y <= rect.y + rect.height
        );
      }
    },
  },
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
  const battleEventListeners = new Set<(envelope: BattleEventsEnvelope) => void>();
  const texts: Array<{ text: string; color?: string }> = [];
  /**
   * The board container's current children, in draw order. It models the real
   * container: `removeAll` clears it, so a redraw reflects the latest push.
   */
  const boardCells: Array<{ kind: string; label?: string }> = [];
  const sceneStarted: Array<{ key: string; data?: unknown }> = [];
  const loadHandlers = new Map<string, () => void>();
  const setEngineStatus = vi.fn();
  /**
   * The board layer's registered gameobject input handlers, keyed by event name
   * (`Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN`). Phaser drives the scene's
   * input through these; the harness does the same.
   *
   * The stored function dispatches to **every** listener the scene registered,
   * so a duplicate registration is observable as a duplicate effect rather than
   * being silently collapsed by a single-entry map.
   */
  const boardInputHandlers = new Map<string, (pointer: { x: number; y: number }) => void>();
  /** How many listeners the board layer holds per event name. */
  const boardInputHandlerCounts = new Map<string, number>();
  /** The board container itself, so the test can assert its input contract. */
  const boardLayers: Array<{
    readonly width: number;
    readonly height: number;
    readonly inputEnabled: boolean;
    readonly hitArea: { x: number; y: number; width: number; height: number } | null;
    /** The rectangle handed to `setInteractive`, i.e. the shape Phaser tests. */
    readonly hitAreaForTest: {
      x: number;
      y: number;
      width: number;
      height: number;
    } | null;
  }> = [];
  /** Every action the scene submitted through the runtime port. */
  const requestedActions: Array<Record<string, unknown>> = [];
  /** Every action invocation sent to the runtime port (including in-flight or failed). */
  const actionInvocations: Array<Record<string, unknown>> = [];
  /** The acknowledgement `requestAction` resolves with. */
  let actionResult: { accepted: boolean; reason?: string | null } = { accepted: true };
  /** When set, `requestAction` rejects with it. */
  let actionBehaviour: (() => Promise<never>) | null = null;
  let loading = false;
  let currentBattleState = battleState;
  const clickables: Array<{ text: string; interactive: boolean; click: () => void }> = [];

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
    onBattleEvents: (listener: (envelope: BattleEventsEnvelope) => void) => {
      battleEventListeners.add(listener);
      return () => {
        battleEventListeners.delete(listener);
      };
    },
    requestAction: (action: Record<string, unknown>) => {
      actionInvocations.push(action);
      if (actionBehaviour) {
        return actionBehaviour();
      }
      requestedActions.push(action);
      return Promise.resolve(actionResult);
    },
    getCards: vi.fn(async () => [
      { cardId: 'card-heal', name: 'Heal', category: 'Basic' as const },
      { cardId: 'card-shield', name: 'Shield', category: 'Basic' as const },
      { cardId: 'card-power-charge', name: 'Power Charge', category: 'Basic' as const },
      { cardId: 'card-inferno', name: 'Inferno', category: 'PetSkill' as const },
    ]),
    getPets: vi.fn(async () => []),
    getPet: vi.fn(async () => ({} as never)),
    getRelics: vi.fn(async () => []),
    setEngineStatus,
  };

  /** Builds the `this` context for a scene instance. */
  function context(scene: object, sceneKey: string): object {
    let containerIndex = 0;

    /** A text object. When `into` is given, the object is a board child. */
    const makeText = (value: string, into?: { kind: string; label: string }[]) => {
      const entry = { text: value, color: undefined as string | undefined };
      texts.push(entry);

      const handlers: Array<() => void> = [];
      let interactive = false;

      const obj = {
        kind: 'label' as const,
        label: value,
        get text() {
          return entry.text;
        },
        get interactive() {
          return interactive;
        },
        setOrigin: () => obj,
        setText: (next: string) => {
          entry.text = next;
          return obj;
        },
        setColor: (next: string) => {
          entry.color = next;
          return obj;
        },
        setInteractive: () => {
          interactive = true;
          return obj;
        },
        on: (_event: string, handler: () => void) => {
          handlers.push(handler);
          return obj;
        },
        off: () => {
          handlers.length = 0;
          return obj;
        },
        click: () => {
          for (const h of [...handlers]) h();
        },
      };

      clickables.push(obj);
      // Board labels are created through the same `add.text` factory as scene
      // readouts, so the distinction is made at the call site in the scene: the
      // scene adds board children to its container explicitly.
      void into;
      return obj;
    };

    /**
     * A container that models child ownership, `removeAll`, and Phaser's real
     * input-enabling contract.
     *
     * The input members are modelled because that is precisely where the board
     * defect lived: a Phaser `Container` has no implicit size and no texture, so
     * it only becomes a pickable input target once `setSize(width, height)` has
     * declared an extent and `setInteractive()` has derived a hit area from it.
     * A mock that accepts `.on('gameobjectdown', …)` without those two calls
     * cannot represent "this container is not an input target" and therefore
     * cannot fail on the real defect.
     */
    const makeContainer = () => {
      containerIndex++;
      const isBoard = containerIndex === 1;
      let children: Array<{ kind: string; label?: string }> = [];
      let size: { width: number; height: number } | null = null;
      let interactive = false;
      let explicitHitArea: { x: number; y: number; width: number; height: number } | null = null;

      /** Handlers per Phaser event name; Phaser emits to every listener. */
      const inputHandlers = new Map<
        string,
        Array<(pointer: { x: number; y: number }) => void>
      >();

      const obj = {
        add: (added: unknown) => {
          const list = Array.isArray(added) ? added : [added];
          children = children.concat(list as Array<{ kind: string; label?: string }>);
          if (isBoard) syncBoard();
          return obj;
        },
        removeAll: () => {
          children = [];
          if (isBoard) syncBoard();
          return obj;
        },
        destroy: () => {
          children = [];
          inputHandlers.clear();
          if (isBoard) {
            syncBoard();
            syncBoardInputHandlers();
          }
        },
        /** Phaser's `ComputedSize.setSize` — required before input on a Container. */
        setSize: (width: number, height: number) => {
          size = { width, height };
          return obj;
        },
        /** The declared extent, or `null` when `setSize` was never called. */
        get width() {
          return size ? size.width : 0;
        },
        get height() {
          return size ? size.height : 0;
        },
        /**
         * Phaser's `setInteractive`. On a Container with no explicit shape it
         * derives the hit area from the object's size and warns (then skips
         * enabling) when no size was set — the exact behaviour the defect hit.
         * An explicit shape is recorded as the tested hit area.
         */
        setInteractive: (hitArea?: { x: number; y: number; width: number; height: number }) => {
          if (size === null) {
            // Phaser: "Container.setInteractive must specify a Shape or call
            // setSize() first" — input is NOT enabled and the object is never
            // added to the input list.
            return obj;
          }
          interactive = true;
          explicitHitArea = hitArea ?? null;
          return obj;
        },
        /** Whether Phaser actually enabled this object for input. */
        get inputEnabled() {
          return interactive;
        },
        /** The hit area Phaser derived, `null` when the object is not enabled. */
        get hitArea() {
          return interactive && size
            ? { x: 0, y: 0, width: size.width, height: size.height }
            : null;
        },
        /** The explicit shape passed to `setInteractive`, when one was given. */
        get hitAreaForTest() {
          return explicitHitArea;
        },
        /** The board layer's input registration (Phaser's gameobject events). */
        on: (event: string, handler: (pointer: { x: number; y: number }) => void) => {
          const existing = inputHandlers.get(event) ?? [];
          existing.push(handler);
          inputHandlers.set(event, existing);
          if (isBoard) syncBoardInputHandlers();
          return obj;
        },
        off: (event: string, handler?: (pointer: { x: number; y: number }) => void) => {
          if (handler === undefined) {
            inputHandlers.delete(event);
          } else {
            const existing = (inputHandlers.get(event) ?? []).filter((h) => h !== handler);
            inputHandlers.set(event, existing);
          }
          if (isBoard) syncBoardInputHandlers();
          return obj;
        },
      };

      const syncBoard = () => {
        boardCells.length = 0;
        boardCells.push(...children);
      };

      const syncBoardInputHandlers = () => {
        boardInputHandlerCounts.clear();
        for (const [event, handlers] of inputHandlers) {
          boardInputHandlerCounts.set(event, handlers.length);
        }
        // Phaser emits to every registered listener; the harness does the same so
        // a duplicated registration is observable as a duplicated effect.
        boardInputHandlers.set('gameobjectdown', (pointer) => {
          for (const handler of [...(inputHandlers.get('gameobjectdown') ?? [])]) {
            handler(pointer);
          }
        });
      };

      if (isBoard) boardLayers.push(obj);

      return obj;
    };

    return Object.assign(Object.create(scene), {
      scene: {
        start: (key: string, data?: unknown) => sceneStarted.push({ key, data }),
      },
      add: {
        rectangle: () => {
          const handlers: Array<() => void> = [];
          let interactive = false;
          const rect = {
            kind: 'tile',
            text: '',
            get interactive() {
              return interactive;
            },
            setStrokeStyle: () => rect,
            setInteractive: () => {
              interactive = true;
              return rect;
            },
            on: (_event: string, handler: () => void) => {
              handlers.push(handler);
              return rect;
            },
            off: () => {
              handlers.length = 0;
              return rect;
            },
            click: () => {
              for (const h of [...handlers]) h();
            },
          };
          clickables.push(rect);
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
    battleEventListeners,
    emitBattleEvents: (envelope: BattleEventsEnvelope) => {
      for (const listener of battleEventListeners) {
        listener(envelope);
      }
    },
    boardCells,
    boardInputHandlers,
    boardInputHandlerCounts,
    boardLayers,
    clickables,
    clickOption: (pattern: string | RegExp) => {
      const match = clickables.find((c) =>
        typeof pattern === 'string' ? c.text.includes(pattern) : pattern.test(c.text)
      );
      if (!match) {
        throw new Error(`Clickable matching "${pattern}" was not found.`);
      }
      match.click();
    },
    requestedActions,
    actionInvocations,
    setActionResult: (next: { accepted: boolean; reason?: string | null }) => {
      actionResult = next;
    },
    setActionBehaviour: (next: (() => Promise<never>) | null) => {
      actionBehaviour = next;
    },
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
function runScene(scene: object, ctx: object, method: string, ...args: unknown[]): void {
  const fn = Object.getPrototypeOf(scene)[method] as ((this: object, ...args: unknown[]) => void) | undefined;
  fn?.call(ctx, ...args);
}

/**
 * The board's presentation geometry, mirroring `BattleScene`'s own constants.
 *
 * The scene's index → screen mapping is a presentation concern
 * (`ARCHITECTURE.md` §2.2.2), so the tests resolve a cell the same way the scene
 * does: `index = row * 8 + column` (`MATCH3_RULES.md` §1.0) drawn at a fixed
 * pitch from the board origin. Nothing gameplay is derived here.
 *
 * The board layer is a container at the scene origin, so a pointer event's local
 * coordinates are the space the cells are drawn in — the same space these helpers
 * produce.
 */
const CELL_SIZE = 56;
const CELL_GAP = 6;
const BOARD_PITCH = CELL_SIZE + CELL_GAP;
const BOARD_COLUMNS = 8;
const BOARD_WIDTH = BOARD_COLUMNS * CELL_SIZE + (BOARD_COLUMNS - 1) * CELL_GAP;
const BOARD_ORIGIN_X = (GAME_WIDTH - BOARD_WIDTH) / 2;
const BOARD_ORIGIN_Y = SAFE_AREA.y + 96;

/** Board-layer-local centre of the cell at a §1.0 index. */
function cellCentre(index: number): { x: number; y: number } {
  const row = Math.floor(index / BOARD_COLUMNS);
  const column = index % BOARD_COLUMNS;
  return {
    x: BOARD_ORIGIN_X + column * BOARD_PITCH + CELL_SIZE / 2,
    y: BOARD_ORIGIN_Y + row * BOARD_PITCH + CELL_SIZE / 2,
  };
}

/** The midpoint of the gap between two horizontally adjacent cells. */
function gapCentre(row: number): { x: number; y: number } {
  return {
    x: BOARD_ORIGIN_X + CELL_SIZE + CELL_GAP / 2,
    y: BOARD_ORIGIN_Y + row * BOARD_PITCH + CELL_SIZE / 2,
  };
}

/** Taps the cell at a §1.0 index, through the scene's registered board input. */
function tapCell(
  harness: { boardInputHandlers: Map<string, (p: { x: number; y: number }) => void> },
  index: number
): void {
  const handler = harness.boardInputHandlers.get('gameobjectdown');
  if (!handler) {
    throw new Error('BattleScene registered no board pointer input.');
  }
  handler(cellCentre(index));
}

/** Taps a board-layer-local point. */
function tapPoint(
  harness: { boardInputHandlers: Map<string, (p: { x: number; y: number }) => void> },
  x: number,
  y: number
): void {
  harness.boardInputHandlers.get('gameobjectdown')?.({ x, y });
}

/** Lets the pending swap promise chain settle. */
async function flush(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
  await Promise.resolve();
}

describe('Phaser scene lifecycle', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('exposes the documented scene classes', () => {
    expect(new BootScene()).toBeInstanceOf(BootScene);
    expect(new PreloaderScene()).toBeInstanceOf(PreloaderScene);
    expect(new MainMenuScene()).toBeInstanceOf(MainMenuScene);
    expect(new BattleScene()).toBeInstanceOf(BattleScene);
    expect(new ResultScene()).toBeInstanceOf(ResultScene);
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
    // ...and the scene still advances correctly with nothing to load. TASK-090:
    // the transition target is now MainMenuScene (TDD.md §2.1 full lifecycle).
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
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

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('transitions to MainMenuScene exactly once', () => {
    const harness = createSceneHarness();
    const preloader = new PreloaderScene();
    const ctx = harness.context(preloader, 'PreloaderScene');

    runScene(preloader, ctx, 'preload');
    runScene(preloader, ctx, 'create');
    harness.loadHandlers.get('complete')?.();

    expect(harness.sceneStarted).toHaveLength(1);
  });

  it('never starts LobbyScene directly from PreloaderScene', () => {
    const harness = createSceneHarness();
    const preloader = new PreloaderScene();
    const ctx = harness.context(preloader, 'PreloaderScene');

    runScene(preloader, ctx, 'preload');
    runScene(preloader, ctx, 'create');
    harness.loadHandlers.get('complete')?.();

    const startedKeys = harness.sceneStarted.map((s) => s.key);
    expect(startedKeys).not.toContain('LobbyScene');
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

  it('contains no resolution presentation', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    // TASK-069 stage advance. This assertion previously read "contains no
    // gameplay interaction or resolution presentation" and forbade `Swap`
    // outright, because the scene had no input at all. The documented Match-3
    // interaction (MATCH3_RULES.md §2 item 1) is now implemented, so `Swap` as a
    // *request* is no longer a violation.
    //
    // What is still forbidden, and all this assertion now forbids, is
    // **resolution presentation**: a match, cascade, or combo readout, a damage
    // number, or a boss readout. None of those exists on the client, because the
    // client computes none of them (GAME_RULES.md §18, ADR-001). The scene's swap
    // line reports only which cells were selected and whether the server accepted
    // the request — transport feedback (SIGNALR_PROTOCOL.md §2.1, §5), not a
    // resolution result.
    const rendered = harness.texts.map((t) => t.text).join(' ');
    for (const forbidden of ['Boss', 'Combo', 'Damage', 'Cascade', 'Match ']) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }
  });
});

describe('BattleScene — Board Foundation presentation (GAME_STATE.md §2.0.5)', () => {
  /** The four documented Gem contract names (MATCH3_RULES.md §1.1). */
  const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];

  /**
   * A server-shaped board: exactly 64 cells in the documented order.
   *
   * Each cell is the delivered `CellPayload` shape — its Gem type plus the
   * optional Special Gem at that cell (`SIGNALR_PROTOCOL.md` §4.1 item 5), i.e.
   * `{ gemType, specialGem }`. A bare Gem-name string is **not** what the
   * protocol delivers, and modelling it as one is what previously let a
   * mismatched client reader pass this suite.
   */
  function serverBoard(): Array<{ gemType: string; specialGem?: { type: string } }> {
    return Array.from({ length: 64 }, (_, index) => ({
      gemType: GEM_NAMES[index % GEM_NAMES.length],
    }));
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
      // SIGNALR_PROTOCOL.md §4.3: the enumerated `petState` members. The
      // delivered `current / threshold` pair is always present;
      // `passiveResetOverride` is omitted for the default reset, which is the
      // documented representation (§4.3 items 4, 7); and `statusEffects` is
      // always present — an active Pet with no active effect is an empty array,
      // never an omission (§4.3 item 14).
      petState: {
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      // SIGNALR_PROTOCOL.md §4.4: the two-member Boss HP projection. Both members
      // are always present and neither is nullable (§4.4 item 4).
      bossState: { hp: 5000, maxHp: 5000 },
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
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
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
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('4 / 5');
    expect(rendered).toContain('reset: Partial');
  });

  it('renders the delivered Boss hp and maxHp verbatim', () => {
    // SIGNALR_PROTOCOL.md §4.4: the Boss's live health reaches the client as the
    // two-member `bossState` projection. The scene prints both numbers as
    // received — it damages nothing, clamps nothing, and infers neither value
    // from the other (§4.4 item 7).
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      bossState: { hp: 4200, maxHp: 5000 },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('Boss HP: 4200 / 5000');
  });

  it('renders the delivered Status Effects verbatim, and none as an empty collection', () => {
    // SIGNALR_PROTOCOL.md §4.3 item 14 / GAME_STATE.md §2.3.1: the active Pet's
    // active instances are presented as delivered. The scene applies, refreshes,
    // decrements, expires, and removes nothing, and evaluates no duration or
    // expiry condition (§4.3 item 14, GAME_STATE.md §5.1.1).
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      petState: {
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [
          // A Turn-based instance: the countdown model is shown, because that is
          // the one the element carries (§2.3.1 item 3). `targetStat` does not
          // apply here and stays absent.
          { id: 'Burn', type: 'DoT', source: 'boss', magnitude: 25, remainingTurns: 2 },
          // A trigger-based instance: the expiry condition is shown instead, and
          // no countdown is invented for it.
          { id: 'Shield', type: 'Shield', source: 'player', magnitude: 100, expiryCondition: 'ShieldDepleted' },
        ],
      },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('Burn');
    expect(rendered).toContain('DoT');
    expect(rendered).toContain('2 turns');
    expect(rendered).toContain('Shield');
    expect(rendered).toContain('ShieldDepleted');
    // The empty collection is not this payload's case, so "none" is not shown.
    expect(rendered).not.toContain('Status Effects: none');
  });

  it('renders the empty Status Effect collection as the documented no-effect case', () => {
    // §4.3 item 14: an active Pet with no active effect is sent an EMPTY ARRAY —
    // never an omission. The scene states that as the documented empty case
    // rather than leaving the line blank.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('Status Effects: none');
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
    cells[0] = { gemType: 'POWER' };
    cells[63] = { gemType: 'ATK' };

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

  it('presents no lifecycle value', () => {
    // GAME_STATE.md §2.0.3 / SIGNALR_PROTOCOL.md §8.3: no
    // READY/STARTING/ACTIVE/PAUSED/FINISHED/WON/LOST exists in this contract.
    //
    // `Status` is no longer excluded as a substring: §4.3 item 14 delivers the
    // active Pet's `statusEffects[]`, so "Status Effects" is a documented,
    // rendered member rather than a lifecycle value. What the protocol has no
    // member for is a battle `status`, and the assertion below pins that.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join(' ');
    expect(rendered).not.toMatch(/READY/i);
    for (const forbidden of ['STARTING', 'ACTIVE', 'PAUSED', 'FINISHED', 'WON', 'LOST']) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }

    // The rendered line is the documented Status Effect collection, not a
    // battle-lifecycle value.
    expect(rendered).toContain('Status Effects:');
    expect(rendered).not.toMatch(/\bStatus:\s/);
  });

  it('presents no gameplay system state alongside the board', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    // The board and its four Gem types are part of this stage, as are the
    // delivered `petState` members (the Passive, §4.3) and the two-member Boss HP
    // projection (§4.4). Card casts are implemented in TASK-120. The gameplay
    // systems this stage does not implement remain unmodelled and unrendered:
    // resolution, Combo accounting, Damage, and Relics (GAME_STATE.md §2.0.5.3).
    const rendered = harness.texts.map((t) => t.text).join(' ');
    for (const forbidden of ['Combo', 'Damage', 'Relic', 'PendingSpecial']) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }

    // The Boss line is the delivered `hp`/`maxHp` pair and nothing else: no
    // Boss ATK/DEF, no Element, no State, no Passive progress, no Skill
    // charge/cooldown, and no Boss Status Effects (§4.4 item 3).
    expect(rendered).toContain('Boss HP:');
    for (const hiddenBossMember of [
      'Boss ATK',
      'Boss DEF',
      'Boss Element',
      'Boss State',
      'Boss Passive',
      'SkillCharge',
      'SkillCooldown',
    ]) {
      expect(rendered).not.toContain(hiddenBossMember);
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

describe('BattleScene — Swap input (MATCH3_RULES.md §2, SIGNALR_PROTOCOL.md §2.1)', () => {
  /** The four documented Gem contract names (MATCH3_RULES.md §1.1). */
  const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];

  function serverState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return {
      battleId: 'battle-1',
      turn: 0,
      sequence: 0,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: {
        // The delivered `CellPayload` shape: each cell is its Gem type plus the
        // optional Special Gem at that cell (SIGNALR_PROTOCOL.md §4.1 item 5).
        cells: Array.from({ length: 64 }, (_, index) => ({
          gemType: GEM_NAMES[index % GEM_NAMES.length],
        })),
      },
      playerState: { combo: 0, matchCount: 0 },
      petState: {
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { hp: 5000, maxHp: 5000 },
      ...overrides,
    };
  }

  function createBattle(battleState: RuntimeBattleState | null = serverState()) {
    const harness = createSceneHarness({ battleState });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  /**
   * The board-layer-local centre of the cell at a §1.0 index, resolved the same
   * way `drawCell` places it (`MATCH3_RULES.md` §1.0 provides the index; this is
   * its presentation inverse, mirroring the scene's own `cellCoordinates`).
   */
  function cellCentre(index: number): { x: number; y: number } {
    const row = Math.floor(index / BOARD_COLUMNS);
    const column = index % BOARD_COLUMNS;
    return {
      x: BOARD_ORIGIN_X + column * BOARD_PITCH + CELL_SIZE / 2,
      y: BOARD_ORIGIN_Y + row * BOARD_PITCH + CELL_SIZE / 2,
    };
  }

  it('registers board pointer input', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.boardInputHandlers.has('gameobjectdown')).toBe(true);
  });

  // ---------------------------------------------------------------------------
  // TASK-182 — the board input-target contract.
  //
  // Registering a handler with `.on('gameobjectdown', …)` is not the same as
  // having a board Phaser can pick. A Container has no implicit size and no
  // texture, so Phaser's input system only selects it once `setSize` declares an
  // extent and `setInteractive` derives a hit area from it. The assertions below
  // are on *that* contract, because it is the one the suite previously could not
  // represent (and therefore could not fail on).
  // ---------------------------------------------------------------------------

  it('enables the board layer as an input target with a hit area covering the board', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.boardLayers).toHaveLength(1);

    const board = harness.boardLayers[0];

    // AC-01: the layer is an input-enabled Game Object, not merely an emitter a
    // handler was attached to.
    expect(board.inputEnabled).toBe(true);

    // AC-02: its hit area is exactly the drawn board extent — 8 cells at the
    // existing cell pitch (`MATCH3_RULES.md` §1.0's 8 x 8 contract, presented by
    // `drawCell`). The logical board size and the coordinate system are unchanged.
    expect(board.width).toBe(BOARD_WIDTH);
    expect(board.height).toBe(BOARD_WIDTH);
    expect(board.hitArea?.width).toBe(BOARD_WIDTH);
    expect(board.hitArea?.height).toBe(BOARD_WIDTH);

    // The hit area must also be *positioned* so Phaser picks the drawn board.
    // Phaser normalizes a Container's origin before testing the shape
    // (`InputManager.pointWithinHitArea` adds `displayOriginX/Y`, and a Container's
    // origin is always 0.5), so the rectangle is offset by half the board to
    // cancel that shift. A hit area left at the origin would test a phantom region
    // half a board away and leave every real cell unpickable.
    expect(board.hitAreaForTest).toEqual({
      x: BOARD_ORIGIN_X + BOARD_WIDTH / 2,
      y: BOARD_ORIGIN_Y + BOARD_WIDTH / 2,
      width: BOARD_WIDTH,
      height: BOARD_WIDTH,
    });

    // Every drawn cell centre must fall inside that rectangle once Phaser's origin
    // normalization is applied — i.e. the hit area really does cover the board.
    const originOffset = BOARD_WIDTH / 2;
    for (const index of [0, 7, 8, 27, 56, 63]) {
      const centre = cellCentre(index);
      const normalizedX = centre.x + originOffset;
      const normalizedY = centre.y + originOffset;
      const area = board.hitAreaForTest!;
      const inside =
        normalizedX >= area.x &&
        normalizedX <= area.x + area.width &&
        normalizedY >= area.y &&
        normalizedY <= area.y + area.height;
      expect(inside, `cell ${index} at (${centre.x}, ${centre.y}) must be pickable`).toBe(true);
    }
  });

  it('sizes the board layer before attaching its pointer handler', () => {
    // AC-01 requires the hit area to be established *before* any pointer handler
    // is attached, because that ordering is what makes the target pickable. The
    // scene draws the shell (which enables the layer) and only then registers the
    // handler, so a layer that was never enabled cannot be masked by a handler
    // that happens to be attached.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    const board = harness.boardLayers[0];
    const handlerCount = harness.boardInputHandlerCounts.get('gameobjectdown') ?? 0;

    expect(board.inputEnabled).toBe(true);
    expect(handlerCount).toBe(1);
  });

  it('registers exactly one board pointer handler', () => {
    // AC-08: registration is at most one handler, so one gesture cannot produce
    // several Swap requests.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.boardInputHandlerCounts.get('gameobjectdown')).toBe(1);
  });

  it('adds no second board handler when the scene draws its shell again', async () => {
    // AC-08: a redraw (or a scene re-entry through `create`) must not accumulate
    // handlers. `drawRuntimeShell` is the only creator of the board layer, so the
    // redraw path here is the one the scene actually has: a repeated `create`.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'create');

    // The second create replaces the board layer, so there is one layer and one
    // handler on it — never handler x 2.
    const totalHandlers = harness.boardInputHandlerCounts.get('gameobjectdown') ?? 0;
    expect(totalHandlers).toBe(1);

    // And one gesture still submits exactly one request, which is the behaviour
    // the count exists to protect.
    tapCell(harness, 12);
    tapCell(harness, 13);
    await flush();

    expect(harness.requestedActions).toEqual([
      { kind: 'Swap', fromCell: 12, toCell: 13 },
    ]);
  });

  it('leaves exactly one board handler when input is registered twice on one layer', () => {
    // AC-08's guard, driven directly: `registerBoardInput` is called twice against
    // the *same* board layer, which is the shape a repeated shell draw would take
    // if the layer were reused. The second registration must replace the first
    // rather than stack on it, so one gesture still submits one request.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'registerBoardInput');
    runScene(scene, ctx, 'registerBoardInput');

    expect(harness.boardInputHandlerCounts.get('gameobjectdown')).toBe(1);
  });

  it('submits one Swap for one interaction, not one per registered handler', async () => {
    // AC-08's observable consequence: with a duplicated registration the real
    // Phaser emitter would fire the handler twice, so the guard is verified
    // through the request count as well as the listener list.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'registerBoardInput');

    tapCell(harness, 0);
    tapCell(harness, 1);
    await flush();

    expect(harness.requestedActions).toHaveLength(1);
    expect(harness.requestedActions).toEqual([
      { kind: 'Swap', fromCell: 0, toCell: 1 },
    ]);
  });

  it('keeps the board input target across an authoritative board redraw', async () => {
    // AC-01: `renderBoard` calls `removeAll(true)` on each push. That clears the
    // layer's *children*; the container, its hit area, and its handler survive, so
    // the board remains interactive after the board is re-rendered.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    harness.setBattleState(serverState({ sequence: 1 }));
    await flush();

    const board = harness.boardLayers[0];
    expect(board.inputEnabled).toBe(true);
    expect(board.hitArea).toEqual({
      x: 0,
      y: 0,
      width: BOARD_WIDTH,
      height: BOARD_WIDTH,
    });
    expect(harness.boardInputHandlerCounts.get('gameobjectdown')).toBe(1);

    // The redraw still presents all 64 cells, and a gesture still reaches the
    // existing swap path.
    expect(harness.boardCells.filter((c) => c.kind === 'label')).toHaveLength(64);

    tapCell(harness, 20);
    tapCell(harness, 21);
    await flush();

    expect(harness.requestedActions).toEqual([
      { kind: 'Swap', fromCell: 20, toCell: 21 },
    ]);
  });

  it('detaches the board pointer handler on shutdown', () => {
    // The existing lifecycle pattern is unchanged: `shutdown()` disposes the
    // layer, so no live handler is left behind for a restarted scene.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.boardInputHandlerCounts.get('gameobjectdown')).toBe(1);

    runScene(scene, ctx, 'shutdown');

    expect(harness.boardInputHandlerCounts.get('gameobjectdown') ?? 0).toBe(0);
  });

  it('submits the documented Swap request for two selected cells', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    // MATCH3_RULES.md §2 item 1 / §2.1.1: select the first cell, then the second.
    tapCell(harness, 12);
    expect(harness.requestedActions).toHaveLength(0);

    tapCell(harness, 13);
    await flush();

    // Exactly one request, carrying the two §1.0 indices and nothing else: no
    // Gem type, match result, Combo, Turn, or Sequence value (§2.1.1 item 3).
    expect(harness.requestedActions).toEqual([
      { kind: 'Swap', fromCell: 12, toCell: 13 },
    ]);
  });

  it('names the pair in the order the cells were selected', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    tapCell(harness, 27);
    tapCell(harness, 35);
    await flush();

    // The pair is unordered as a *swap* (MATCH3_RULES.md §2.1.1 item 2); the
    // selection order is what the scene reports, and no direction field exists.
    expect(harness.requestedActions).toEqual([
      { kind: 'Swap', fromCell: 27, toCell: 35 },
    ]);
  });

  it('clears the selection instead of submitting when the same cell is tapped twice', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    tapCell(harness, 20);
    tapCell(harness, 20);
    await flush();

    // Local interaction feedback only: a cell swapped with itself is not a Swap
    // (MATCH3_RULES.md §2.1.2 item 2), so nothing is sent.
    expect(harness.requestedActions).toHaveLength(0);

    // The selection is cleared, so the next tap starts a new pair.
    tapCell(harness, 21);
    tapCell(harness, 22);
    await flush();

    expect(harness.requestedActions).toEqual([
      { kind: 'Swap', fromCell: 21, toCell: 22 },
    ]);
  });

  it('sends the pair as selected even when it is not adjacent', async () => {
    // The scene performs no gameplay validation: adjacency is
    // MATCH3_RULES.md §2.1.2 item 3's server-side check, and the server's
    // rejection is the answer the client renders
    // (SIGNALR_PROTOCOL.md §2.1 item 1).
    const { harness, scene, ctx } = createBattle();
    harness.setActionResult({ accepted: false, reason: 'INVALID_SWAP' });

    runScene(scene, ctx, 'create');
    tapCell(harness, 0);
    tapCell(harness, 63);
    await flush();

    expect(harness.requestedActions).toEqual([
      { kind: 'Swap', fromCell: 0, toCell: 63 },
    ]);
  });

  it('presents the acknowledgement without mutating the board', async () => {
    const state = serverState();
    const cellsBefore = [...state.board.cells];

    const { harness, scene, ctx } = createBattle(state);
    runScene(scene, ctx, 'create');

    tapCell(harness, 8);
    tapCell(harness, 9);
    await flush();

    // §5 item 2 + §3.1: acceptance changes nothing locally — the next
    // authoritative state push re-renders the board. No cell value was rewritten,
    // and the scene computed no board, match, cascade, combo, or Sequence value.
    expect(state.board.cells).toEqual(cellsBefore);
    expect(harness.boardCells.filter((c) => c.kind === 'label')).toHaveLength(64);
  });

  it('surfaces a rejection as feedback and changes nothing', async () => {
    const state = serverState();
    const cellsBefore = [...state.board.cells];

    const { harness, scene, ctx } = createBattle(state);
    harness.setActionResult({ accepted: false, reason: 'NO_MATCH_FROM_SWAP' });

    runScene(scene, ctx, 'create');
    tapCell(harness, 12);
    tapCell(harness, 13);
    await flush();

    // SIGNALR_PROTOCOL.md §5 item 2 / MATCH3_RULES.md §2.1.5: a rejected swap is
    // a gameplay no-op. The machine-readable reason is shown as received, nothing
    // is mutated, and nothing is retried automatically.
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('NO_MATCH_FROM_SWAP');
    expect(harness.requestedActions).toHaveLength(1);
    expect(state.board.cells).toEqual(cellsBefore);
  });

  it('ignores taps outside the board', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    // Far outside the drawn board, and in the gap between two cells: neither
    // resolves to a cell, so nothing is selected and nothing is sent. The point
    // is never clamped or rounded into a neighbouring cell
    // (MATCH3_RULES.md §2.1.3 item 2).
    tapPoint(harness, -50, -50);
    tapCell(harness, 9);

    // A gap tap after the first cell: the selection survives, and the next real
    // cell tap completes the pair.
    const gap = gapCentre(1);
    tapPoint(harness, gap.x, gap.y);
    tapCell(harness, 10);
    await flush();

    expect(harness.requestedActions).toEqual([
      { kind: 'Swap', fromCell: 9, toCell: 10 },
    ]);
  });

  it('reports a transport failure without sending a second request', async () => {
    const { harness, scene, ctx } = createBattle();
    harness.setActionBehaviour(async () => {
      throw new Error('SignalR connection is not established.');
    });

    runScene(scene, ctx, 'create');
    tapCell(harness, 0);
    tapCell(harness, 1);
    await flush();

    // A failed request is presentation state, not game state
    // (SIGNALR_PROTOCOL.md §8.3). No board value changed and nothing was retried.
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Swap not sent');
    expect(harness.requestedActions).toHaveLength(0);
  });

  it('reports swap-unavailable when no runtime was supplied', async () => {
    const harness = createSceneHarness({ battleState: serverState(), withRuntime: false });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');

    runScene(scene, ctx, 'create');
    tapCell(harness, 0);
    tapCell(harness, 1);
    await flush();

    expect(harness.requestedActions).toHaveLength(0);
    expect(harness.texts.map((t) => t.text).join('\n')).toContain('Swap unavailable');
  });

  it('adds no competing coordinate system and computes no gameplay', () => {
    // MATCH3_RULES.md §1.0 / §2.1.1: the board's only coordinate convention is
    // `index = row * 8 + column`. The scene maps that index to screen space for
    // presentation (`ARCHITECTURE.md` §2.2.2) and must not introduce a second
    // convention, implement Match-3 resolution, or duplicate board state.
    //
    // Comments are stripped first, so the prose that *names* these forbidden
    // systems while forbidding them is not a violation — only real code is.
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    for (const forbidden of [
      'SignalRService',
      'HubConnection',
      '@microsoft/signalr',
      'findMatch',
      'hasMatch',
      'cascade =',
      'applyGravity',
      'damage',
      'combo++',
    ]) {
      expect(source, `BattleScene must not reference "${forbidden}"`).not.toContain(forbidden);
    }

    // It submits through the runtime port and never invents an action name.
    expect(source).toContain('requestAction');
    expect(source).toContain('RUNTIME_ACTION_SWAP');
    expect(source).toContain('RUNTIME_ACTION_CARD_CAST');
    expect(source).toContain('RUNTIME_ACTION_PET_SKILL_CAST');
    expect(source).not.toContain("'GetBattleState'");
  });
});

describe('BattleScene — CardCast and PetSkillCast input (SIGNALR_PROTOCOL.md §2, TASK-120)', () => {
  const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];

  function serverState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return {
      battleId: 'battle-1',
      turn: 0,
      sequence: 0,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: {
        // The delivered `CellPayload` shape: each cell is its Gem type plus the
        // optional Special Gem at that cell (SIGNALR_PROTOCOL.md §4.1 item 5).
        cells: Array.from({ length: 64 }, (_, index) => ({
          gemType: GEM_NAMES[index % GEM_NAMES.length],
        })),
      },
      playerState: { combo: 0, matchCount: 0 },
      petState: {
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { hp: 5000, maxHp: 5000 },
      ...overrides,
    };
  }

  function createBattle(battleState: RuntimeBattleState | null = serverState()) {
    const harness = createSceneHarness({ battleState });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  it('renders cast controls for all synchronized equipped cards', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    const rendered = harness.texts.map((t) => t.text).join(' ');
    expect(rendered).toContain('Card: Heal');
    expect(rendered).toContain('Card: Shield');
    expect(rendered).toContain('Card: Power Charge');
    expect(rendered).toContain('Skill: Inferno');
  });

  it('derives the signature skill dynamically from category === PetSkill metadata', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    // The button for Inferno is categorized as PetSkill, so it presents as Skill
    const skillOption = harness.clickables.find((c) => c.text.includes('Skill: Inferno'));
    expect(skillOption).toBeDefined();

    // The basic cards are presented as Card
    const cardOption = harness.clickables.find((c) => c.text.includes('Card: Heal'));
    expect(cardOption).toBeDefined();
  });

  it('submits CardCast through runtime.requestAction on basic card click', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    harness.clickOption('Card: Heal');
    await flush();

    expect(harness.requestedActions).toEqual([
      { kind: 'CardCast', cardId: 'card-heal' },
    ]);
  });

  it('submits PetSkillCast through runtime.requestAction on skill click without sending cardId', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    harness.clickOption('Skill: Inferno');
    await flush();

    expect(harness.requestedActions).toEqual([
      { kind: 'PetSkillCast' },
    ]);
  });

  it('displays transport feedback when cast is accepted', async () => {
    const { harness, scene, ctx } = createBattle();
    harness.setActionResult({ accepted: true });
    runScene(scene, ctx, 'create');
    await flush();

    harness.clickOption('Card: Shield');
    await flush();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('CardCast card-shield: accepted. Awaiting the server\'s state push.');
  });

  it('displays rejection reason code when cast is rejected', async () => {
    const { harness, scene, ctx } = createBattle();
    harness.setActionResult({ accepted: false, reason: 'INSUFFICIENT_POWER' });
    runScene(scene, ctx, 'create');
    await flush();

    harness.clickOption('Skill: Inferno');
    await flush();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('PetSkillCast: rejected (INSUFFICIENT_POWER).');
  });

  it('locks cast input while an action is in flight to prevent concurrent submissions', async () => {
    const { harness, scene, ctx } = createBattle();
    let resolveAction!: (val: { accepted: boolean; reason?: string | null }) => void;
    harness.setActionBehaviour(
      () =>
        new Promise<{ accepted: boolean; reason?: string | null }>((resolve) => {
          resolveAction = resolve;
        }) as unknown as Promise<never>
    );
    runScene(scene, ctx, 'create');
    await flush();

    // Trigger first cast
    harness.clickOption('Card: Heal');

    // Attempt second cast while first is in flight
    harness.clickOption('Card: Shield');

    // Attempt skill cast while in flight
    harness.clickOption('Skill: Inferno');

    // Attempt board swap while in flight
    tapCell(harness, 0);
    tapCell(harness, 1);

    expect(harness.actionInvocations).toHaveLength(1);

    resolveAction({ accepted: true });
    await flush();

    // Only the first action was submitted
    expect(harness.actionInvocations).toHaveLength(1);
    expect(harness.actionInvocations[0]).toEqual({ kind: 'CardCast', cardId: 'card-heal' });
  });

  it('locks card and skill cast while swap is in flight', async () => {
    const { harness, scene, ctx } = createBattle();
    let resolveSwap!: (val: { accepted: boolean; reason?: string | null }) => void;
    harness.setActionBehaviour(
      () =>
        new Promise<{ accepted: boolean; reason?: string | null }>((resolve) => {
          resolveSwap = resolveSwap ?? resolve;
        }) as unknown as Promise<never>
    );
    runScene(scene, ctx, 'create');
    await flush();

    // Start swap
    tapCell(harness, 12);
    tapCell(harness, 13);

    // Attempt card cast while swap is in flight
    harness.clickOption('Card: Heal');
    harness.clickOption('Skill: Inferno');

    resolveSwap({ accepted: true });
    await flush();

    // Only swap was submitted
    expect(harness.actionInvocations).toEqual([
      { kind: 'Swap', fromCell: 12, toCell: 13 },
    ]);
  });

  it('has zero direct SignalRService access from BattleScene', () => {
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
      'utf8'
    );

    expect(source).not.toMatch(/from\s+['"][^'"]*SignalRService/);
    expect(source).not.toContain('hubConnection');
    expect(source).not.toMatch(/\.invoke\s*</);
  });
});

describe('BattleScene — Outcome handoff (TASK-087, SIGNALR_PROTOCOL.md §3.2.19)', () => {
  function createBattle() {
    const harness = createSceneHarness();
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  it('subscribes to onBattleEvents on create and detaches on shutdown', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.battleEventListeners.size).toBe(1);

    runScene(scene, ctx, 'shutdown');
    expect(harness.battleEventListeners.size).toBe(0);
  });

  it('transitions to ResultScene when BattleWon is received', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'battle-1',
      serverSequence: 10,
      events: [
        {
          type: 'BattleWon',
          outcome: 'victory',
          finalBossHp: 37,
          finalPlayerHp: 812,
        },
      ],
    });

    expect(harness.sceneStarted).toEqual([
      {
        key: 'ResultScene',
        data: {
          outcome: 'victory',
          finalBossHp: 37,
          finalPlayerHp: 812,
          // TASK-149: the handoff also carries the batch's battleId so
          // ResultScene can read the persisted reward summary
          // (API_CONTRACTS.md §4). The outcome members are unchanged.
          battleId: 'battle-1',
        },
      },
    ]);
  });

  it('transitions to ResultScene when BattleLost is received', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'battle-1',
      serverSequence: 12,
      events: [
        {
          type: 'BattleLost',
          outcome: 'defeat',
          finalBossHp: 412,
          finalPlayerHp: 0,
        },
      ],
    });

    expect(harness.sceneStarted).toEqual([
      {
        key: 'ResultScene',
        data: {
          outcome: 'defeat',
          finalBossHp: 412,
          finalPlayerHp: 0,
          // TASK-149: the handoff also carries the batch's battleId.
          battleId: 'battle-1',
        },
      },
    ]);
  });

  it('does not transition when envelope contains no outcome event', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'battle-1',
      serverSequence: 3,
      events: [{ type: 'TurnEnded', turn: 1 }],
    });

    expect(harness.sceneStarted).toHaveLength(0);
  });

  it('guards against duplicate transitions on multiple outcome events', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    const won = {
      type: 'BattleWon',
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 100,
    };

    harness.emitBattleEvents({
      battleId: 'battle-1',
      serverSequence: 10,
      events: [won, won],
    });

    harness.emitBattleEvents({
      battleId: 'battle-1',
      serverSequence: 11,
      events: [won],
    });

    expect(harness.sceneStarted).toHaveLength(1);
  });
});

describe('ResultScene', () => {
  it('renders victory outcome and verbatim HP values', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', {
      outcome: 'victory',
      finalBossHp: 37,
      finalPlayerHp: 812,
    });

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/VICTORY/i);
    expect(rendered).toContain('Boss HP: 37');
    expect(rendered).toContain('Player HP: 812');
  });

  it('renders defeat outcome and verbatim HP values', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', {
      outcome: 'defeat',
      finalBossHp: 412,
      finalPlayerHp: 0,
    });

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/DEFEAT/i);
    expect(rendered).toContain('Boss HP: 412');
    expect(rendered).toContain('Player HP: 0');
  });
});

describe('MainMenuScene', () => {
  it('can be created with the mocked Phaser/runtime harness', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
  });

  it('creates main menu presentation on create', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');

    // The menu draws a title text.
    const rendered = harness.texts.map((t) => t.text);
    expect(rendered).toContain('DCACTI');
  });

  it('does not navigate before the navigation action', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');

    expect(harness.sceneStarted).toHaveLength(0);
  });

  it('navigation action starts LobbyScene', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');

    // Simulate the button click by invoking the private startBattle method
    // through the proper harness context (the same `this` the scene was created with).
    runScene(scene, ctx, 'startBattle' as never);

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['LobbyScene']);
  });

  it('repeated navigation action cannot start LobbyScene multiple times', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');

    runScene(scene, ctx, 'startBattle' as never);
    runScene(scene, ctx, 'startBattle' as never);
    runScene(scene, ctx, 'startBattle' as never);

    expect(harness.sceneStarted).toHaveLength(1);
    expect(harness.sceneStarted[0].key).toBe('LobbyScene');
  });

  it('shutdown before navigation is safe and starts nothing', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'shutdown');

    expect(harness.sceneStarted).toHaveLength(0);
  });
});