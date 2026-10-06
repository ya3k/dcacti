import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { BattleScene } from '../src/game/scenes/BattleScene';
import { BootScene } from '../src/game/scenes/BootScene';
import { CollectionViewerScene } from '../src/game/scenes/CollectionViewerScene';
import { BattleHistoryScene } from '../src/game/scenes/BattleHistoryScene';
import { LobbyScene } from '../src/game/scenes/LobbyScene';
import { MainMenuScene } from '../src/game/scenes/MainMenuScene';
import { PreloaderScene } from '../src/game/scenes/PreloaderScene';
import { ResultScene } from '../src/game/scenes/ResultScene';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { readPreservedLoadout, preserveLoadout } from '../src/game/state/PreservedLoadout';
import { createGameConfig } from '../src/game/GameConfig';
import { GAME_WIDTH, SAFE_AREA } from '../src/game/GameViewport';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { GameRuntimeState } from '../src/state/GameRuntimeState';
import type {
  BattleEventsEnvelope,
  RuntimeBattleState,
  RuntimeEventListener,
} from '../src/game/runtime/GameRuntimeEvents';
import { SceneEventEmitter } from './support/SceneEventEmitter';

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
  // Phaser's scene lifecycle events. `Phaser.Scenes.Systems#shutdown` emits
  // SHUTDOWN (from `SceneManager.stop`, and when a running scene is restarted)
  // and `#destroy` emits DESTROY. `Phaser.Scene` declares no `shutdown` method,
  // and the engine never calls a scene method merely because one exists — so a
  // scene attaches its teardown to these events, and so does this suite.
  Scenes: {
    Events: {
      SHUTDOWN: 'shutdown',
      DESTROY: 'destroy',
    },
  },
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
 *
 * The scene's own event emitter is modelled by `SceneEventEmitter`
 * (`tests/support/SceneEventEmitter.ts`), so a test can take a scene down the
 * way Phaser does instead of calling a method by name.
 */
interface SceneHarnessOptions {
  state?: GameRuntimeState;
  withRuntime?: boolean;
  battleState?: RuntimeBattleState | null;
  /** The owned collection the pre-battle flow reads (`GET /api/pets`). */
  pets?: Array<{ petId: string; identity: string; element: string; tier: string; star: number; level: number }>;
  /** The owned Relic instances the pre-battle flow reads (`GET /api/relics`). */
  relics?: Array<{ relicId: string; name: string }>;
  /**
   * Whether writing to a destroyed `Text` throws (the browser evidence of the
   * stale-write defect) or is merely recorded as a refused write.
   *
   * It defaults to throwing, which is the faithful behaviour and what the
   * TASK-204 tests rely on. A test that observes an *asynchronous* write — where
   * a thrown `TypeError` inside a `void`ed promise would surface as an unhandled
   * rejection rather than at the assertion — sets it to `false` and asserts on
   * {@link createSceneHarness}'s `destroyedWriteAttempts` instead.
   */
  textsThrowWhenDestroyed?: boolean;
}

function createSceneHarness(options: SceneHarnessOptions = {}) {
  const {
    state = INITIAL_RUNTIME_STATE,
    withRuntime = true,
    battleState = null,
    pets = [],
    relics = [],
    textsThrowWhenDestroyed = true,
  } = options;
  const listeners = new Set<RuntimeEventListener>();
  const battleStateListeners = new Set<(state: RuntimeBattleState) => void>();
  const battleEventListeners = new Set<(envelope: BattleEventsEnvelope) => void>();
  const texts: Array<{ text: string; color?: string }> = [];
  /**
   * Every write a scene attempted against a `Text` object the engine has already
   * destroyed (`DisplayList#shutdown`), in order — including the ones that also
   * threw. Empty means no continuation reached a dead object.
   */
  const destroyedWriteAttempts: string[] = [];
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
  /**
   * The Phaser game-wide registry's contents — one store per game instance,
   * shared by every scene context this harness builds, exactly as
   * `game.registry` is in Phaser. That is what makes the preserved-loadout
   * carrier (ADR-022) survive a scene transition.
   */
  const registryValues = new Map<string, unknown>();
  /** The acknowledgement `requestAction` resolves with. */
  let actionResult: { accepted: boolean; reason?: string | null } = { accepted: true };
  /** When set, `requestAction` rejects with it. */
  let actionBehaviour: (() => Promise<never>) | null = null;
  let loading = false;
  let currentBattleState = battleState;
  /**
   * Every object the scenes created with a registered pointer handler, with its
   * geometry and the label it draws. It models Phaser's real display list
   * closely enough for a test to activate a control the way a player does —
   * `setInteractive` on the hit area, the label drawn on top at the same centre.
   */
  const clickables: Array<{
    kind: string;
    text: string;
    x: number;
    y: number;
    readonly interactive: boolean;
    click: () => void;
  }> = [];
  /** Every post-result cleanup the runtime was asked for (TASK-203). */
  const clearActiveBattleStateCalls: string[] = [];
  /** Every `startBattle` request a scene submitted, in order. */
  const startRequests: Array<Record<string, unknown>> = [];
  /**
   * The scene emitters and display lists this harness built, by scene context,
   * so a test can take a scene down the way Phaser does instead of calling a
   * method directly.
   */
  const sceneLifecycles = new WeakMap<
    object,
    { events: SceneEventEmitter; destroyDisplayList: () => void }
  >();
  /** The state this harness's runtime currently reports. */
  let currentState = state;

  /**
   * Reports a technical runtime state transition to the subscribers the way
   * `GameRuntime` does (`updateState` → `emit`). Every runtime state change goes
   * through here, so a subscription that outlived its scene is reached exactly
   * as it is in the browser.
   */
  const dispatchRuntimeState = (next: GameRuntimeState) => {
    currentState = next;
    for (const listener of [...listeners]) {
      listener({ type: 'runtime_state_changed', state: next });
    }
  };

  const runtime = {
    getState: () => currentState,
    onRuntimeEvent: (listener: RuntimeEventListener) => {
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
    /**
     * The documented battle-start capability (ARCHITECTURE.md §2.2.3 rule 5).
     * It records the request the scene submitted; the REST → connect → join
     * sequence itself is the real runtime's and is covered in
     * `GameRuntime.test.ts`.
     */
    startBattle: (request: Record<string, unknown>) => {
      startRequests.push(request);
      return Promise.resolve();
    },
    /**
     * The documented client-local post-result cleanup (TASK-203,
     * `GameRuntimePort.clearActiveBattleState`): it drops the runtime's
     * synchronized battle copy and nothing else — no disconnect, no result read,
     * and no effect on the preserved loadout (`GameRuntime.test.ts` covers the
     * real implementation's transport behavior).
     *
     * Like the real implementation it is a state transition, and the runtime
     * notifies its subscribers of one (`updateState` → `emit`). Modelling that
     * notification is what lets this suite fail on the stale-listener defect:
     * the cleanup is precisely the moment the abandoned BattleScene listener was
     * reached in the browser.
     */
    clearActiveBattleState: () => {
      clearActiveBattleStateCalls.push('clearActiveBattleState');
      currentBattleState = null;
      // The real capability's own state transition (GameRuntime.ts): the
      // synchronized copy is dropped and `sync` returns to the documented value
      // for the connection the runtime actually has.
      dispatchRuntimeState({
        ...currentState,
        sync: currentState.connection === 'connected' ? 'awaiting_battle' : 'unsynchronized',
        lastError: null,
      });
    },
    getCards: vi.fn(async () => [
      { cardId: 'card-heal', name: 'Heal', category: 'Basic' as const },
      { cardId: 'card-shield', name: 'Shield', category: 'Basic' as const },
      { cardId: 'card-power-charge', name: 'Power Charge', category: 'Basic' as const },
      { cardId: 'card-inferno', name: 'Inferno', category: 'PetSkill' as const },
    ]),
    getPets: vi.fn(async () => pets),
    getPet: vi.fn(async () => pets[0] ?? ({} as never)),
    getRelics: vi.fn(async () => relics),
    /** The completed battle's persisted result the result route returns. */
    getBattleResult: vi.fn(async (battleId: string) => ({
      battleId,
      outcome: 'victory' as const,
      rewards: {
        playerXpGained: 100,
        newPlayerXp: 400,
        playerLeveledUp: true,
        newPlayerLevel: 5,
        petXpGained: 100,
        newPetXp: 900,
        petLeveledUp: false,
        newPetLevel: 10,
      },
      durationTurns: 4,
    })),
    /**
     * The authenticated Player's completed-battle history
     * (`API_CONTRACTS.md` §4.5). The default is the documented empty history
     * (note 9), so a scenario that cares about the loaded list sets `history`
     * itself.
     */
    getBattleHistory: vi.fn(async () => [] as Array<Record<string, unknown>>),
    setEngineStatus,
  };

  /** Builds the `this` context for a scene instance. */
  function context(scene: object, sceneKey: string): object {
    let containerIndex = 0;

    /**
     * The scene's display list — every game object `add.*` produced for this
     * scene run. Phaser's `DisplayList` destroys all of them when the scene
     * shuts down, which is what makes a scene reference kept past teardown
     * dangerous.
     */
    const displayList: Array<{ destroy: () => void }> = [];

    /** Models Phaser's `DisplayList#shutdown`: every child is destroyed. */
    const destroyDisplayList = () => {
      for (const child of [...displayList]) {
        child.destroy();
      }
      displayList.length = 0;
    };

    /**
     * The scene's own event emitter (Phaser's `scene.events`). The engine
     * raises its lifecycle events on this emitter and never calls a scene
     * method because one exists, so the suite raises them here too.
     */
    const events = new SceneEventEmitter();

    /** A text object. When `into` is given, the object is a board child. */
    const makeText = (x: number, y: number, value: string, into?: { kind: string; label: string }[]) => {
      const entry = { text: value, color: undefined as string | undefined };
      texts.push(entry);

      const handlers: Array<() => void> = [];
      let interactive = false;
      /**
       * True once Phaser's display list has destroyed this object. A destroyed
       * Game Object has released its render state, so writing to it is a real
       * failure rather than a no-op — the browser evidence for the stale
       * listener defect is a `TypeError` raised from exactly this call.
       */
      let destroyed = false;

      /**
       * Refuses a write to a destroyed object.
       *
       * The attempt is recorded either way, so a test can assert that no write
       * was even attempted; whether it also throws depends on
       * `textsThrowWhenDestroyed`.
       */
      const writable = (next: string): boolean => {
        if (!destroyed) {
          return true;
        }

        destroyedWriteAttempts.push(next);

        if (textsThrowWhenDestroyed) {
          throw new TypeError(
            "Cannot read properties of null (reading 'drawImage')"
          );
        }

        return false;
      };

      const obj = {
        kind: 'label' as const,
        label: value,
        x,
        y,
        get text() {
          return entry.text;
        },
        get interactive() {
          return interactive;
        },
        setOrigin: () => obj,
        setText: (next: string) => {
          if (!writable(next)) {
            return obj;
          }
          entry.text = next;
          return obj;
        },
        setColor: (next: string) => {
          if (!writable(next)) {
            return obj;
          }
          entry.color = next;
          return obj;
        },
        setWordWrapWidth: () => obj,
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
        destroy: () => {
          destroyed = true;
          handlers.length = 0;
        },
        click: () => {
          for (const h of [...handlers]) h();
        },
      };

      displayList.push({ destroy: () => obj.destroy() });
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

      displayList.push({ destroy: () => obj.destroy() });

      return obj;
    };

    const ctx = Object.assign(Object.create(scene), {
      // Phaser's injected scene event emitter (`this.events` / `sys.events`).
      events,
      scene: {
        start: (key: string, data?: unknown) => sceneStarted.push({ key, data }),
      },
      add: {
        rectangle: (x = 0, y = 0, width = 0, height = 0) => {
          const handlers: Array<() => void> = [];
          let interactive = false;
          const rect = {
            kind: 'tile',
            text: '',
            x,
            y,
            width,
            height,
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
            destroy: () => {
              handlers.length = 0;
            },
            click: () => {
              for (const h of [...handlers]) h();
            },
          };
          clickables.push(rect);
          displayList.push({
            destroy: () => {
              rect.destroy();
            },
          });
          return rect;
        },
        text: (x: number, y: number, value: string) => makeText(x, y, value),
        container: () => makeContainer(),
      },
      load: {
        once: (event: string, handler: () => void) => {
          loadHandlers.set(event, handler);
        },
        isLoading: () => loading,
      },
      registry: {
        get: (key: string) => {
          if (withRuntime && key === RUNTIME_REGISTRY_KEY) {
            return runtime;
          }
          return registryValues.get(key);
        },
        set: (key: string, value: unknown) => {
          registryValues.set(key, value);
        },
        remove: (key: string) => {
          registryValues.delete(key);
        },
      },
      sys: { settings: { key: sceneKey } },
    });

    sceneLifecycles.set(ctx, { events, destroyDisplayList });

    return ctx;
  }

  return {
    runtime,
    setEngineStatus,
    texts,
    destroyedWriteAttempts,
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
    clearActiveBattleStateCalls,
    startRequests,
    /**
     * Activates the control a player reads as `label`: the interactive hit area
     * drawn at that label's own centre. It is how Phaser's input actually
     * resolves a click — the label is display, the hit area is the control.
     */
    clickControl: (label: string) => {
      const labels = clickables.filter((c) => c.kind === 'label' && c.text === label);
      const text = labels[labels.length - 1];
      if (!text) {
        throw new Error(`No control labelled "${label}" was rendered.`);
      }
      const targets = clickables.filter(
        (c) => c.interactive && c.kind === 'tile' && c.x === text.x && c.y === text.y
      );
      const target = targets[targets.length - 1];
      if (!target) {
        throw new Error(`The control labelled "${label}" is not an interactive control.`);
      }
      target.click();
    },
    clickOption: (pattern: string | RegExp) => {
      // A render pass destroys the previous pass's option objects and creates
      // new ones, so the *newest* match is the live control; an older match's
      // handlers have already been detached.
      const matches = clickables.filter((c) =>
        typeof pattern === 'string' ? c.text.includes(pattern) : pattern.test(c.text)
      );
      const match = matches[matches.length - 1];
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
    /** Reports a technical runtime state transition to the runtime's subscribers. */
    emitRuntimeState: (next: GameRuntimeState) => {
      dispatchRuntimeState(next);
    },
    /**
     * Takes a scene down exactly as Phaser does.
     *
     * `SceneManager` calls `Systems#shutdown`, which emits
     * `Phaser.Scenes.Events.SHUTDOWN` on the scene's emitter; the engine's own
     * `DisplayList` handles that event by destroying every child. Nothing here
     * calls a scene method by name — if the scene's teardown is not attached to
     * the engine's event, it does not run.
     */
    shutdownScene: (ctx: object) => {
      const lifecycle = sceneLifecycles.get(ctx);
      if (!lifecycle) {
        return;
      }
      lifecycle.destroyDisplayList();
      lifecycle.events.emit('shutdown');
    },
    /** Phaser's `Systems#destroy` → `Phaser.Scenes.Events.DESTROY`. */
    destroyScene: (ctx: object) => {
      const lifecycle = sceneLifecycles.get(ctx);
      if (!lifecycle) {
        return;
      }
      lifecycle.destroyDisplayList();
      lifecycle.events.emit('destroy');
    },
    /** How many listeners the scene's own emitter holds for one lifecycle event. */
    sceneListenerCount: (ctx: object, event: string) =>
      sceneLifecycles.get(ctx)?.events.listenerCount(event) ?? 0,
    /**
     * The engine's own half of taking a scene down, on its own.
     *
     * Phaser's `DisplayList` handles the shutdown itself and destroys every
     * child whether or not the scene registered anything, so this is what a
     * scene's game objects look like after teardown even when no teardown of the
     * scene's own ran. It exists so a test can show what the defect looks like:
     * a scene that attaches no teardown keeps its subscriptions pointed at
     * destroyed objects.
     */
    destroySceneDisplayList: (ctx: object) => {
      sceneLifecycles.get(ctx)?.destroyDisplayList();
    },
    setLoading: (value: boolean) => {
      loading = value;
    },
    context,
  };
}

/**
 * Invokes one of the scene methods Phaser itself calls by name — `init`,
 * `preload`, `create` — with the harness context.
 *
 * It is deliberately **not** the way this suite takes a scene down: Phaser
 * invokes no other scene method by name, so teardown is driven through the
 * engine's own events by `harness.shutdownScene` / `harness.destroyScene`
 * (TASK-204).
 */
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
    expect(new LobbyScene()).toBeInstanceOf(LobbyScene);
    expect(new CollectionViewerScene()).toBeInstanceOf(CollectionViewerScene);
    expect(new BattleHistoryScene()).toBeInstanceOf(BattleHistoryScene);
    expect(new BattleScene()).toBeInstanceOf(BattleScene);
    expect(new ResultScene()).toBeInstanceOf(ResultScene);
  });

  it('registers every scene, with the two read-only branches alongside the battle lifecycle', () => {
    // TASK-190: `CollectionViewerScene` joins the registered scene list. It is
    // registered by class, so Phaser keys it by the scene's own configured key.
    // TASK-206: `BattleHistoryScene` joins it as the second read-only branch off
    // the main menu.
    const config = createGameConfig(document.createElement('div'));

    expect(config.scene).toEqual([
      BootScene,
      PreloaderScene,
      MainMenuScene,
      LobbyScene,
      CollectionViewerScene,
      BattleHistoryScene,
      BattleScene,
      ResultScene,
    ]);
  });
});

describe('MainMenuScene navigation', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('places START BATTLE at (640, 324), COLLECTION at (640, 394), and BATTLE HISTORY at (640, 464)', () => {
    // The coordinate contract TASK-190 fixes: the battle entry keeps the centre
    // `standalone-web-smoke.mjs` clicks, and the collection entry sits one clear
    // button-height below it, so the two hit areas never overlap. TASK-206 adds
    // the Battle History entry one further pitch below, so the two existing
    // entries keep their own coordinates and no hit area overlaps another.
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const positions: Array<{ x: number; y: number; width: number; height: number }> = [];
    const ctx = Object.assign(harness.context(scene, 'MainMenuScene'), {
      add: {
        rectangle: (x = 0, y = 0, width = 0, height = 0) => {
          positions.push({ x, y, width, height });
          const obj = {
            setStrokeStyle: () => obj,
            setInteractive: () => obj,
            on: () => obj,
          };
          return obj;
        },
        text: () => ({ setOrigin: () => undefined }),
      },
    });

    runScene(scene, ctx, 'create');

    // The first rectangle is the safe-area background; then the three buttons.
    expect(positions).toHaveLength(4);
    expect(positions[1]).toEqual({ x: 640, y: 324, width: 280, height: 50 });
    expect(positions[2]).toEqual({ x: 640, y: 394, width: 280, height: 50 });
    expect(positions[3]).toEqual({ x: 640, y: 464, width: 280, height: 50 });

    // No vertical overlap between any two adjacent targets: each gap is 70 - 50.
    for (let i = 1; i < positions.length - 1; i++) {
      const bottom = positions[i].y + positions[i].height / 2;
      const top = positions[i + 1].y - positions[i + 1].height / 2;
      expect(top).toBeGreaterThan(bottom);
    }

    // The new entry is inside the safe area, so it cannot be clipped.
    const lastBottom = positions[3].y + positions[3].height / 2;
    expect(lastBottom).toBeLessThanOrEqual(SAFE_AREA.y + SAFE_AREA.height);
  });

  it('opens CollectionViewerScene from the COLLECTION navigation action', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'openCollection' as never);

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['CollectionViewerScene']);
  });

  it('opens BattleHistoryScene from the BATTLE HISTORY navigation action', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'openBattleHistory' as never);

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleHistoryScene']);
  });

  it('cannot open both scenes from repeated activation of either button', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'openCollection' as never);
    runScene(scene, ctx, 'openCollection' as never);
    runScene(scene, ctx, 'startBattle' as never);

    // The single-use guard is shared: whichever action fires first wins, and the
    // other button cannot start a second scene from the same instance.
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['CollectionViewerScene']);
  });

  it('shares the single-use guard with the BATTLE HISTORY entry', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'openBattleHistory' as never);
    runScene(scene, ctx, 'openBattleHistory' as never);
    runScene(scene, ctx, 'openCollection' as never);
    runScene(scene, ctx, 'startBattle' as never);

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleHistoryScene']);
  });

  it('offers a COLLECTION and a BATTLE HISTORY entry point alongside START BATTLE', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text);
    expect(rendered).toContain('START BATTLE');
    expect(rendered).toContain('COLLECTION');
    expect(rendered).toContain('BATTLE HISTORY');
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
      listener({ type: 'runtime_state_changed', state: connected });
    }

    expect(harness.texts.map((t) => t.text)).toContain('Runtime Connected');
  });

  it('subscribes to the runtime exactly once', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.listeners.size).toBe(1);
  });

  it('attaches its teardown to the engine lifecycle events, not to a method name', () => {
    // TASK-204. `Phaser.Scene` declares no `shutdown` method and the engine
    // never calls one: `Systems#shutdown` / `#destroy` emit SHUTDOWN / DESTROY,
    // and that is the only teardown signal a scene gets. The scene must
    // therefore be *listening* for them — a teardown that only exists as a
    // method would never run in the browser.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('detaches from the runtime when Phaser shuts the scene down', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.listeners.size).toBe(1);

    // The engine's own teardown path — not a call to the scene's method.
    harness.shutdownScene(ctx);

    expect(harness.listeners.size).toBe(0);
  });

  it('detaches from the runtime when Phaser destroys the scene', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.listeners.size).toBe(1);

    harness.destroyScene(ctx);

    expect(harness.listeners.size).toBe(0);
  });

  it('holds no subscription on the runtime after a real teardown', () => {
    // The mandatory invariant: after the Phaser scene lifecycle ends, none of
    // this scene's listeners is left on the runtime — so the completed battle's
    // `clearActiveBattleState()` cannot reach a BattleScene whose game objects
    // the engine has already destroyed.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.listeners.size).toBe(1);
    expect(harness.battleStateListeners.size).toBe(1);
    expect(harness.battleEventListeners.size).toBe(1);

    harness.shutdownScene(ctx);

    expect(harness.listeners.size).toBe(0);
    expect(harness.battleStateListeners.size).toBe(0);
    expect(harness.battleEventListeners.size).toBe(0);
  });

  it('is safe to shut down when the scene never created its objects', () => {
    const { harness, ctx } = createBattle();
    expect(() => harness.shutdownScene(ctx)).not.toThrow();
  });

  it('does not leave a duplicate subscription when the scene is reused', () => {
    // A scene instance is reused: `start` after a `shutdown` runs `create()`
    // again. One teardown per run, one subscription per run.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');

    expect(harness.listeners.size).toBe(1);
    expect(harness.battleStateListeners.size).toBe(1);
    expect(harness.battleEventListeners.size).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    // TASK-205: the unfired DESTROY handler of the first run must not be left
    // behind for the second one to stack on top of.
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
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

  it('detaches from the battle-state push when Phaser shuts the scene down', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.battleStateListeners.size).toBe(1);

    harness.shutdownScene(ctx);

    expect(harness.battleStateListeners.size).toBe(0);
  });

  it('teardown clears the rendered board', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');
    expect(harness.boardCells.length).toBeGreaterThan(0);

    harness.shutdownScene(ctx);

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

  it('detaches the board pointer handler when the scene is shut down', () => {
    // The board layer is disposed by the scene's teardown, which the engine
    // raises as SHUTDOWN, so no live handler is left behind for a restarted
    // scene.
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.boardInputHandlerCounts.get('gameobjectdown')).toBe(1);

    harness.shutdownScene(ctx);

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

  it('subscribes to onBattleEvents on create and detaches on the engine teardown', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.battleEventListeners.size).toBe(1);

    harness.shutdownScene(ctx);
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

  it('attaches its teardown to the engine lifecycle events', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');

    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('keeps exactly one teardown per event when the scene is reused', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');
    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');

    // `once` alone would leave the first run's unfired DESTROY handler behind,
    // so the detach-then-attach registration is what keeps this at one.
    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('shutdown before navigation is safe and starts nothing', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    runScene(scene, ctx, 'create');
    harness.shutdownScene(ctx);

    expect(harness.sceneStarted).toHaveLength(0);
  });

  it('is safe to be taken down by the engine before create() ever ran', () => {
    const harness = createSceneHarness();
    const scene = new MainMenuScene();
    const ctx = harness.context(scene, 'MainMenuScene');

    expect(() => harness.shutdownScene(ctx)).not.toThrow();
    expect(() => harness.destroyScene(ctx)).not.toThrow();
  });
});

describe('CollectionViewerScene', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates its presentation through the shared harness without throwing', () => {
    const harness = createSceneHarness();
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene, 'CollectionViewerScene');

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
    expect(harness.texts.map((t) => t.text)).toContain('COLLECTION VIEWER');
  });

  it('subscribes to no runtime stream and opens no battle state', () => {
    // TASK-190: unlike `BattleScene`, the viewer is a request/response screen.
    // §5 defines no push for a collection read, so there is nothing to subscribe
    // to and no battle to synchronize (`SIGNALR_PROTOCOL.md` §2, §4).
    const harness = createSceneHarness();
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene, 'CollectionViewerScene');

    runScene(scene, ctx, 'create');

    expect(harness.listeners.size).toBe(0);
    expect(harness.battleStateListeners.size).toBe(0);
    expect(harness.battleEventListeners.size).toBe(0);
  });

  it('reads the collection through the runtime port and no other capability', () => {
    const harness = createSceneHarness();
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene, 'CollectionViewerScene');

    runScene(scene, ctx, 'create');

    expect(harness.runtime.getPets).toHaveBeenCalled();
    expect(harness.runtime.getCards).toHaveBeenCalled();
    expect(harness.runtime.getRelics).toHaveBeenCalled();
    // The read-only viewer starts no battle and submits no action.
    expect(harness.requestedActions).toHaveLength(0);
    expect(harness.actionInvocations).toHaveLength(0);
  });

  it('transitions back to MainMenuScene', () => {
    const harness = createSceneHarness();
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene, 'CollectionViewerScene');

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'goBack' as never);

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('teardown is safe and leaves nothing subscribed or transitioning', () => {
    const harness = createSceneHarness();
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene, 'CollectionViewerScene');

    runScene(scene, ctx, 'create');
    harness.shutdownScene(ctx);

    expect(harness.sceneStarted).toHaveLength(0);
    expect(harness.listeners.size).toBe(0);
    expect(harness.battleStateListeners.size).toBe(0);
  });

  it('attaches its teardown to the engine lifecycle events', () => {
    const harness = createSceneHarness();
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene, 'CollectionViewerScene');

    runScene(scene, ctx, 'create');

    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('drops the loaded collection when Phaser destroys the scene', () => {
    const harness = createSceneHarness({
      pets: [
        {
          petId: 'pet-instance-1',
          identity: 'Xích Lang',
          element: 'Fire',
          tier: 'Common',
          star: 1,
          level: 1,
        },
      ],
    });
    const scene = new CollectionViewerScene();
    const ctx = harness.context(scene, 'CollectionViewerScene');

    runScene(scene, ctx, 'create');
    harness.destroyScene(ctx);

    expect((ctx as { ownedPets: unknown[] }).ownedPets).toHaveLength(0);
    expect((ctx as { selectedItemId: string | null }).selectedItemId).toBeNull();
  });

  it('is a side branch: no other scene starts it and it starts nothing but the menu', () => {
    // The MainMenu is its only entry point, and MainMenuScene its only exit, so
    // the viewer cannot be reached from — or lead into — the battle lifecycle.
    const viewerSource = readFileSync(
      resolve(__dirname, '../src/game/scenes/CollectionViewerScene.ts'),
      'utf8'
    ).replace(/\/\*[\s\S]*?\*\//g, '');

    const startedByViewer = [...viewerSource.matchAll(/scene\.start\('([^']+)'\)/g)].map(
      (match) => match[1]
    );
    expect(startedByViewer).toEqual(['MainMenuScene']);

    const menuSource = readFileSync(
      resolve(__dirname, '../src/game/scenes/MainMenuScene.ts'),
      'utf8'
    ).replace(/\/\*[\s\S]*?\*\//g, '');
    expect(menuSource).toContain("'CollectionViewerScene'");
    expect(menuSource).toContain("'LobbyScene'");
  });
});

describe('BattleHistoryScene (TASK-206, API_CONTRACTS.md §4.5)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('creates its presentation through the shared harness without throwing', () => {
    const harness = createSceneHarness();
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene, 'BattleHistoryScene');

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
    const rendered = harness.texts.map((t) => t.text);
    expect(rendered).toContain('BATTLE HISTORY');
    expect(rendered).toContain('< BACK');
  });

  it('subscribes to no runtime stream and opens no battle state', () => {
    // Like the collection viewer, this is a request/response screen: §4.5
    // introduces no push, hub method, or event, so there is nothing to subscribe
    // to and no battle to synchronize (API_CONTRACTS.md §4.5 note 13).
    const harness = createSceneHarness();
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene, 'BattleHistoryScene');

    runScene(scene, ctx, 'create');

    expect(harness.listeners.size).toBe(0);
    expect(harness.battleStateListeners.size).toBe(0);
    expect(harness.battleEventListeners.size).toBe(0);
  });

  it('reads the history through the runtime port and no other capability', () => {
    const harness = createSceneHarness();
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene, 'BattleHistoryScene');

    runScene(scene, ctx, 'create');

    expect(harness.runtime.getBattleHistory).toHaveBeenCalledTimes(1);
    // The read-only surface starts no battle, submits no action, reads no other
    // collection, reads no battle result, and clears no battle state.
    expect(harness.runtime.getBattleResult).not.toHaveBeenCalled();
    expect(harness.runtime.getPets).not.toHaveBeenCalled();
    expect(harness.requestedActions).toHaveLength(0);
    expect(harness.actionInvocations).toHaveLength(0);
    expect(harness.startRequests).toHaveLength(0);
    expect(harness.clearActiveBattleStateCalls).toHaveLength(0);
  });

  it('renders the delivered history the runtime returned', async () => {
    const harness = createSceneHarness();
    harness.runtime.getBattleHistory.mockResolvedValue([
      {
        battleId: 'battle-zzz',
        outcome: 'victory',
        rewards: {
          playerXpGained: 100,
          newPlayerXp: 400,
          playerLeveledUp: true,
          newPlayerLevel: 5,
          petXpGained: 100,
          newPetXp: 900,
          petLeveledUp: false,
          newPetLevel: 10,
        },
        durationTurns: 12,
        completedAt: '2026-10-05T09:15:00Z',
      },
    ] as never);
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene, 'BattleHistoryScene');

    runScene(scene, ctx, 'create');
    // The read is asynchronous: the delivery lands after the promise settles.
    await flush();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('battle-zzz');
    expect(rendered).toContain('Outcome: victory');
    expect(rendered).toContain('Duration: 12 turns');
    expect(rendered).toContain('Completed At: 2026-10-05T09:15:00Z');
  });

  it('transitions back to MainMenuScene', () => {
    const harness = createSceneHarness();
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene, 'BattleHistoryScene');

    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'goBack' as never);

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('teardown is safe and leaves nothing subscribed or transitioning', () => {
    const harness = createSceneHarness();
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene, 'BattleHistoryScene');

    runScene(scene, ctx, 'create');
    harness.shutdownScene(ctx);

    expect(harness.sceneStarted).toHaveLength(0);
    expect(harness.listeners.size).toBe(0);
    expect(harness.battleStateListeners.size).toBe(0);
  });

  it('attaches its teardown to the engine lifecycle events', () => {
    const harness = createSceneHarness();
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene, 'BattleHistoryScene');

    runScene(scene, ctx, 'create');

    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('drops the loaded history when Phaser destroys the scene', () => {
    const harness = createSceneHarness();
    harness.runtime.getBattleHistory.mockResolvedValue([
      {
        battleId: 'battle-zzz',
        outcome: 'victory',
        rewards: {
          playerXpGained: 100,
          newPlayerXp: 400,
          playerLeveledUp: true,
          newPlayerLevel: 5,
          petXpGained: 100,
          newPetXp: 900,
          petLeveledUp: false,
          newPetLevel: 10,
        },
        durationTurns: 12,
        completedAt: '2026-10-05T09:15:00Z',
      },
    ] as never);
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene, 'BattleHistoryScene');

    runScene(scene, ctx, 'create');
    harness.destroyScene(ctx);

    expect((ctx as { history: unknown[] }).history).toHaveLength(0);
    expect((ctx as { loadError: string | null }).loadError).toBeNull();
  });

  it('is a side branch: only the main menu starts it and it starts only the menu', () => {
    const historySource = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleHistoryScene.ts'),
      'utf8'
    ).replace(/\/\*[\s\S]*?\*\//g, '');

    const startedByHistory = [...historySource.matchAll(/scene\.start\('([^']+)'\)/g)].map(
      (match) => match[1]
    );
    expect(startedByHistory).toEqual(['MainMenuScene']);

    const menuSource = readFileSync(
      resolve(__dirname, '../src/game/scenes/MainMenuScene.ts'),
      'utf8'
    ).replace(/\/\*[\s\S]*?\*\//g, '');
    expect(menuSource).toContain("'BattleHistoryScene'");
    // The two existing entries keep their own destinations.
    expect(menuSource).toContain("'CollectionViewerScene'");
    expect(menuSource).toContain("'LobbyScene'");
  });
});

describe('Post-result lifecycle — Result → Lobby / MainMenu (TASK-203, ADR-022)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** The owned Pet the pre-battle flow reads (`API_CONTRACTS.md` §5.1). */
  const OWNED_PET = {
    petId: 'pet-instance-1',
    identity: 'Xích Lang',
    element: 'Fire',
    tier: 'Common',
    star: 1,
    level: 1,
  };

  /** The owned Relic instances the pre-battle flow reads (`API_CONTRACTS.md` §5.4). */
  const OWNED_RELICS = [
    { relicId: 'relic-instance-1', name: 'Berserker Core' },
    { relicId: 'relic-instance-2', name: 'Mana Crystal' },
    { relicId: 'relic-instance-3', name: 'Assassin Eye' },
  ];

  /** The loadout the completed battle was fought with (`API_CONTRACTS.md` §3). */
  const COMPLETED_LOADOUT = {
    petId: 'pet-instance-1',
    bossId: 'boss-hoa-long',
    cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
    relicLoadout: ['relic-instance-1', 'relic-instance-2', 'relic-instance-3'],
  };

  /** The runtime's synchronized copy of the battle that has just ended. */
  function synchronizedBattle(battleId = 'battle-ended'): RuntimeBattleState {
    return {
      battleId,
      turn: 6,
      sequence: 9,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: {
        cells: Array.from({ length: 64 }, (_, index) => ({
          gemType: ['ATK', 'DEF', 'HP', 'POWER'][index % 4],
        })),
      },
      playerState: { combo: 0, matchCount: 3 },
      petState: {
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 2 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      // The battle is over: the Boss is at 0 HP and the outcome has been
      // delivered (SIGNALR_PROTOCOL.md §3.2.19, §4.4).
      bossState: { hp: 0, maxHp: 5000 },
    };
  }

  function postBattleHarness() {
    return createSceneHarness({
      pets: [OWNED_PET],
      relics: OWNED_RELICS,
      battleState: synchronizedBattle(),
    });
  }

  /** Selects the full documented loadout through the scene's own option rows. */
  function selectLoadout(harness: ReturnType<typeof createSceneHarness>): void {
    harness.clickOption('Xích Lang');
    harness.clickOption('Heal');
    harness.clickOption('Shield');
    harness.clickOption('Power Charge');
    harness.clickOption('Berserker Core');
    harness.clickOption('Mana Crystal');
    harness.clickOption('Assassin Eye');
    harness.clickOption('Hỏa Long');
  }

  it('PLAY AGAIN clears the completed battle, preserves the loadout, and opens a Lobby that restores it', async () => {
    const harness = postBattleHarness();

    // 1. The player prepares and starts a battle from the Lobby. That Lobby is
    //    gone by the time the result is presented — which is exactly why the
    //    loadout needs the documented carrier (ARCHITECTURE.md §2.2.3, ADR-022).
    const lobby = new LobbyScene();
    const lobbyCtx = harness.context(lobby, 'LobbyScene');
    runScene(lobby, lobbyCtx, 'init', undefined);
    runScene(lobby, lobbyCtx, 'create');
    await flush();

    selectLoadout(harness);
    harness.clickControl('START BATTLE');
    await flush();

    expect(harness.startRequests).toEqual([COMPLETED_LOADOUT]);
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
    // Phaser stops the Lobby as the queued scene start is processed (TASK-205).
    harness.shutdownScene(lobbyCtx);

    // 2. The battle reaches its terminal outcome: BattleScene hands the outcome
    //    and the battle's id to ResultScene.
    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');
    runScene(result, resultCtx, 'init', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 850,
      battleId: 'battle-ended',
    });
    runScene(result, resultCtx, 'create');

    // 3. PLAY AGAIN — the approved continuation (D-202-01 = C).
    harness.clickControl('PLAY AGAIN');
    await flush();

    // D-202-04 = A: the completed battle's active battle state is cleared...
    expect(harness.clearActiveBattleStateCalls).toHaveLength(1);
    expect(harness.runtime.getBattleState()).toBeNull();
    // ...without disconnecting the transport (`clearActiveBattleState` performs
    // no transport operation: GameRuntime.test.ts pins the real runtime's half).
    // ...and the preserved loadout survives that cleanup.
    expect(readPreservedLoadout(lobbyCtx as never)).toEqual(COMPLETED_LOADOUT);

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene', 'LobbyScene']);
    expect(harness.sceneStarted[1].data).toEqual({ restorePreservedLoadout: true });

    // 4. The Lobby opened by PLAY AGAIN shows the loadout the battle used, and
    //    the next request carries it with no stale battle state attached.
    const nextLobby = new LobbyScene();
    const nextCtx = harness.context(nextLobby, 'LobbyScene');
    runScene(nextLobby, nextCtx, 'init', harness.sceneStarted[1].data);
    runScene(nextLobby, nextCtx, 'create');
    await flush();

    const review = (nextCtx as { reviewText: { text: string } }).reviewText.text;
    expect(review).toContain('Boss: boss-hoa-long');
    expect(review).toContain('Pet:   pet-instance-1');
    expect(review).toContain('relic-instance-3');

    harness.clickControl('START BATTLE');
    await flush();

    const nextRequest = harness.startRequests[1];
    expect(nextRequest).toEqual(COMPLETED_LOADOUT);
    // A battle-start request carries the four documented members and nothing
    // else: no previous battle's id, turn, or sequence can travel with it
    // (API_CONTRACTS.md §3, D-202-04 = A).
    expect(Object.keys(nextRequest).sort()).toEqual([
      'bossId',
      'cardLoadout',
      'petId',
      'relicLoadout',
    ]);
  });

  it('MAIN MENU clears the completed battle, opens the menu, and consumes no loadout', () => {
    const harness = postBattleHarness();
    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');

    // The battle that just ended had preserved its loadout.
    preserveLoadout(resultCtx as never, COMPLETED_LOADOUT);

    runScene(result, resultCtx, 'init', {
      outcome: 'defeat',
      finalBossHp: 4200,
      finalPlayerHp: 0,
      battleId: 'battle-ended',
    });
    runScene(result, resultCtx, 'create');

    harness.clickControl('MAIN MENU');

    expect(harness.clearActiveBattleStateCalls).toHaveLength(1);
    expect(harness.runtime.getBattleState()).toBeNull();
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
    // The preserved pre-battle loadout is not battle state: the exit neither
    // clears it nor consumes it (ADR-022).
    expect(readPreservedLoadout(resultCtx as never)).toEqual(COMPLETED_LOADOUT);
  });

  it('leaves no stale battle state behind for the next battle', async () => {
    const harness = postBattleHarness();
    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');

    preserveLoadout(resultCtx as never, COMPLETED_LOADOUT);
    runScene(result, resultCtx, 'create', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 850,
      battleId: 'battle-ended',
    });

    harness.clickControl('PLAY AGAIN');
    await flush();

    // The runtime holds no battle at all, so no previous battle id, turn,
    // sequence, board, or outcome can be rendered or addressed by the next
    // battle (D-202-04 = A).
    expect(harness.runtime.getBattleState()).toBeNull();

    const nextLobby = new LobbyScene();
    const nextCtx = harness.context(nextLobby, 'LobbyScene');
    runScene(nextLobby, nextCtx, 'init', harness.sceneStarted[0].data);
    runScene(nextLobby, nextCtx, 'create');
    await flush();

    // Nothing on the new Lobby carries the completed battle's identity.
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('battle-ended');
  });
});

describe('Phaser scene teardown — the TASK-203 regression (TASK-204)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** The runtime's synchronized copy of the battle in progress. */
  function synchronizedBattle(battleId: string, bossHp = 0): RuntimeBattleState {
    return {
      battleId,
      turn: 6,
      sequence: 9,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: {
        cells: Array.from({ length: 64 }, (_, index) => ({
          gemType: ['ATK', 'DEF', 'HP', 'POWER'][index % 4],
        })),
      },
      playerState: { combo: 2, matchCount: 5 },
      petState: {
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 2 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { hp: bossHp, maxHp: 5000 },
    };
  }

  /** The terminal outcome batch the server delivers (SIGNALR_PROTOCOL.md §3.2.19). */
  function outcomeBatch(battleId: string, finalPlayerHp = 850): BattleEventsEnvelope {
    return {
      battleId,
      serverSequence: 10,
      events: [{ type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp }],
    };
  }

  function battleHarness(battleId = 'battle-1') {
    return createSceneHarness({ battleState: synchronizedBattle(battleId) });
  }

  it('runs the completed battle exit without a stale listener reaching dead UI', () => {
    // The whole defect, in the order the browser runs it.
    const harness = battleHarness('battle-1');
    const battle = new BattleScene();
    const battleCtx = harness.context(battle, 'BattleScene');
    runScene(battle, battleCtx, 'create');

    // Battle 1 reaches its terminal outcome: the scene hands off to ResultScene.
    harness.emitBattleEvents(outcomeBatch('battle-1'));
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['ResultScene']);

    // Phaser stops BattleScene as the queued scene start is processed, and that
    // removes the scene's subscriptions with it.
    harness.shutdownScene(battleCtx);
    expect(harness.listeners.size).toBe(0);
    expect(harness.battleStateListeners.size).toBe(0);
    expect(harness.battleEventListeners.size).toBe(0);

    // ResultScene is presented and the player activates PLAY AGAIN.
    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');
    runScene(result, resultCtx, 'init', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 850,
      battleId: 'battle-1',
    });
    runScene(result, resultCtx, 'create');

    // `clearActiveBattleState()` reports a runtime state transition. With the
    // stale BattleScene still subscribed, that report reached a scene whose game
    // objects Phaser had already destroyed and threw, aborting the navigation
    // before `scene.start(...)`.
    expect(() => harness.clickControl('PLAY AGAIN')).not.toThrow();

    expect(harness.clearActiveBattleStateCalls).toHaveLength(1);
    expect(harness.runtime.getBattleState()).toBeNull();
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['ResultScene', 'LobbyScene']);
  });

  it('runs the MAIN MENU exit without a stale listener reaching dead UI', () => {
    const harness = battleHarness('battle-1');
    const battle = new BattleScene();
    const battleCtx = harness.context(battle, 'BattleScene');
    runScene(battle, battleCtx, 'create');

    harness.emitBattleEvents(outcomeBatch('battle-1'));
    harness.shutdownScene(battleCtx);

    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');
    runScene(result, resultCtx, 'init', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 850,
      battleId: 'battle-1',
    });
    runScene(result, resultCtx, 'create');

    expect(() => harness.clickControl('MAIN MENU')).not.toThrow();

    expect(harness.clearActiveBattleStateCalls).toHaveLength(1);
    expect(harness.runtime.getBattleState()).toBeNull();
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['ResultScene', 'MainMenuScene']);
  });

  it('can fail on that defect: a scene with no attached teardown keeps its listener', () => {
    // The harness's own guard, in the spirit of TASK-182's board-input model. If
    // a scene's teardown is not attached to the engine's event, the engine still
    // destroys the scene's game objects, and the stale listener then throws out
    // of the cleanup — which is exactly the browser evidence. A suite that
    // cannot represent the defect cannot prove the fix, so this test pins the
    // defect's shape using the engine's half of teardown alone.
    const harness = battleHarness('battle-1');
    const battle = new BattleScene();
    const battleCtx = harness.context(battle, 'BattleScene');
    runScene(battle, battleCtx, 'create');

    harness.emitBattleEvents(outcomeBatch('battle-1'));

    // Phaser's DisplayList destroys the stopped scene's children whether or not
    // the scene registered a teardown of its own.
    harness.destroySceneDisplayList(battleCtx);

    // Nothing released the subscription, so the completed battle's cleanup still
    // reaches the scene — whose text objects are now destroyed.
    expect(harness.listeners.size).toBe(1);

    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');
    runScene(result, resultCtx, 'create', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 850,
      battleId: 'battle-1',
    });

    expect(() => harness.clickControl('PLAY AGAIN')).toThrow(TypeError);
    // Navigation never happened: `scene.start(...)` is never reached.
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['ResultScene']);
  });

  it('lets a reused scene start Battle 2 with no state carried over', () => {
    // Battle reuse is the second half of the regression: `outcomeHandled` is
    // scene-instance state, so it only resets if the teardown actually runs.
    const harness = battleHarness('battle-1');
    const battle = new BattleScene();
    const battleCtx = harness.context(battle, 'BattleScene');
    runScene(battle, battleCtx, 'create');

    // Battle 1 ends and the engine stops the scene.
    harness.emitBattleEvents(outcomeBatch('battle-1', 850));
    harness.shutdownScene(battleCtx);

    expect(harness.sceneStarted).toEqual([
      {
        key: 'ResultScene',
        data: { outcome: 'victory', finalBossHp: 0, finalPlayerHp: 850, battleId: 'battle-1' },
      },
    ]);

    // The player leaves the completed battle, then starts Battle 2 from the
    // Lobby. Phaser reuses the same scene instance: `start` after `shutdown`
    // runs `create()` again.
    harness.runtime.clearActiveBattleState();
    expect(harness.runtime.getBattleState()).toBeNull();
    runScene(battle, battleCtx, 'create');

    // Exactly one subscription per stream — never the previous battle's plus a
    // new one — and the readout holds no battle state until the new push.
    expect(harness.listeners.size).toBe(1);
    expect(harness.battleStateListeners.size).toBe(1);
    expect(harness.battleEventListeners.size).toBe(1);
    expect((battleCtx as { battleText: { text: string } | null }).battleText?.text).toBe('');
    expect((battleCtx as { outcomeHandled: boolean }).outcomeHandled).toBe(false);

    // Battle 2's own authoritative push arrives.
    harness.setBattleState(synchronizedBattle('battle-2', 5000));
    expect((battleCtx as { battleText: { text: string } | null }).battleText?.text).toContain(
      'BattleId: battle-2'
    );

    // Battle 2 reaches its own outcome and hands off to ResultScene again.
    harness.emitBattleEvents(outcomeBatch('battle-2', 512));

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['ResultScene', 'ResultScene']);
    expect(harness.sceneStarted[1].data).toEqual({
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 512,
      battleId: 'battle-2',
    });
  });

  it('accepts one outcome per battle, so a reused scene is not stuck', () => {
    // The `outcomeHandled` guard still does its documented job *within* one
    // battle: a duplicate terminal batch cannot start ResultScene twice.
    const harness = battleHarness('battle-2');
    const battle = new BattleScene();
    const battleCtx = harness.context(battle, 'BattleScene');
    runScene(battle, battleCtx, 'create');

    harness.emitBattleEvents(outcomeBatch('battle-2'));
    harness.emitBattleEvents(outcomeBatch('battle-2'));

    expect(harness.sceneStarted).toHaveLength(1);
  });

  it('clears active battle state without touching the preserved loadout', () => {
    // The TASK-203 cleanup boundary, re-asserted on the real path: the exit
    // clears battle state and leaves the preserved pre-battle selection alone
    // (ADR-022 D5).
    const harness = battleHarness('battle-1');
    const loadout = {
      petId: 'pet-instance-1',
      bossId: 'boss-hoa-long',
      cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
      relicLoadout: ['relic-instance-1', 'relic-instance-2', 'relic-instance-3'],
    };

    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');
    preserveLoadout(resultCtx as never, loadout);

    runScene(result, resultCtx, 'create', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 850,
      battleId: 'battle-1',
    });

    harness.clickControl('MAIN MENU');

    expect(harness.runtime.getBattleState()).toBeNull();
    expect(readPreservedLoadout(resultCtx as never)).toEqual(loadout);
  });

  it('pins the mechanism in the source: Phaser\u2019s own lifecycle event is registered', () => {
    // A pin on the mechanism itself: the cleanup is subscribed to Phaser's own
    // lifecycle events rather than relying on the method's name. `Phaser.Scene`
    // declares no `shutdown` method, so nothing else would ever call it.
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
      'utf8'
    ).replace(/\/\*[\s\S]*?\*\//g, '');

    expect(source).toMatch(/events\.once\(\s*Phaser\.Scenes\.Events\.SHUTDOWN/);
    expect(source).toMatch(/events\.once\(\s*Phaser\.Scenes\.Events\.DESTROY/);
  });
});

describe('Phaser scene lifecycle consistency — every scene with a teardown (TASK-205)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /**
   * The scenes that own cleanup, with the loader that builds each one and the
   * scene-local state that proves its teardown ran. `BattleScene` is covered by
   * the TASK-204 suite above; `BootScene` / `PreloaderScene` declare no teardown
   * at all.
   */
  interface LifecycleScene {
    readonly name: string;
    readonly key: string;
    readonly build: () => object;
    /** Puts the scene's owned state into a non-initial value. */
    readonly dirty: (scene: object, ctx: object) => void;
    /** Asserts that state was released. */
    readonly assertReleased: (ctx: object) => void;
  }

  const SCENES: readonly LifecycleScene[] = [
    {
      name: 'MainMenuScene',
      key: 'MainMenuScene',
      build: () => new MainMenuScene(),
      /** The scene's only owned state, claimed by navigating. */
      dirty: (scene: object, ctx: object) => runScene(scene, ctx, 'startBattle'),
      assertReleased: (ctx: object) => {
        expect((ctx as { hasTransitioned: boolean }).hasTransitioned).toBe(false);
      },
    },
    {
      name: 'LobbyScene',
      key: 'LobbyScene',
      build: () => new LobbyScene(),
      dirty: (_scene: object, ctx: object) => {
        (ctx as { selectedPetId: string | null }).selectedPetId = 'pet-instance-1';
      },
      assertReleased: (ctx: object) => {
        expect((ctx as { selectedPetId: string | null }).selectedPetId).toBeNull();
      },
    },
    {
      name: 'CollectionViewerScene',
      key: 'CollectionViewerScene',
      build: () => new CollectionViewerScene(),
      dirty: (_scene: object, ctx: object) => {
        (ctx as { selectedItemId: string | null }).selectedItemId = 'pet-instance-1';
      },
      assertReleased: (ctx: object) => {
        expect((ctx as { selectedItemId: string | null }).selectedItemId).toBeNull();
      },
    },
    {
      name: 'BattleHistoryScene',
      key: 'BattleHistoryScene',
      build: () => new BattleHistoryScene(),
      dirty: (_scene: object, ctx: object) => {
        (ctx as { history: unknown[] }).history = [{ battleId: 'battle-test' }];
      },
      assertReleased: (ctx: object) => {
        expect((ctx as { history: unknown[] }).history).toHaveLength(0);
        expect((ctx as { loadError: string | null }).loadError).toBeNull();
      },
    },
    {
      name: 'ResultScene',
      key: 'ResultScene',
      build: () => new ResultScene(),
      dirty: (_scene: object, ctx: object) => {
        (ctx as { hasTransitioned: boolean }).hasTransitioned = true;
      },
      assertReleased: (ctx: object) => {
        expect((ctx as { resultData: unknown }).resultData).toBeNull();
        expect((ctx as { hasTransitioned: boolean }).hasTransitioned).toBe(false);
      },
    },
  ];

  /** Creates every scene once and hands each to `body` with its harness. */
  function forEachScene(
    body: (
      scene: object,
      ctx: object,
      harness: ReturnType<typeof createSceneHarness>,
      entry: (typeof SCENES)[number]
    ) => void
  ): void {
    for (const entry of SCENES) {
      const harness = createSceneHarness();
      const scene = entry.build();
      const ctx = harness.context(scene, entry.key);
      runScene(scene, ctx, 'create');
      body(scene, ctx, harness, entry);
    }
  }

  it('registers its teardown against the engine lifecycle events, not a method name', () => {
    forEachScene((_scene, ctx, harness, entry) => {
      // `Phaser.Scene` declares no `shutdown` method and `SceneManager` invokes a
      // scene only for `init` / `preload` / `create` / `update`. A teardown that
      // exists only as a method is therefore never called in the browser.
      expect(harness.sceneListenerCount(ctx, 'shutdown'), entry.name).toBe(1);
      expect(harness.sceneListenerCount(ctx, 'destroy'), entry.name).toBe(1);
    });
  });

  it('runs its teardown on SHUTDOWN', () => {
    for (const entry of SCENES) {
      const harness = createSceneHarness();
      const scene = entry.build();
      const ctx = harness.context(scene, entry.key);
      runScene(scene, ctx, 'create');
      entry.dirty(scene, ctx);

      harness.shutdownScene(ctx);

      entry.assertReleased(ctx);
    }
  });

  it('runs its teardown on DESTROY', () => {
    for (const entry of SCENES) {
      const harness = createSceneHarness();
      const scene = entry.build();
      const ctx = harness.context(scene, entry.key);
      runScene(scene, ctx, 'create');
      entry.dirty(scene, ctx);

      harness.destroyScene(ctx);

      entry.assertReleased(ctx);
    }
  });

  it('is safe and idempotent when both events fire, or neither ran a create()', () => {
    for (const entry of SCENES) {
      const harness = createSceneHarness();
      const scene = entry.build();
      const ctx = harness.context(scene, entry.key);

      // No `create()` ran: there is nothing registered and nothing to release.
      expect(() => harness.shutdownScene(ctx), entry.name).not.toThrow();
      expect(() => harness.destroyScene(ctx), entry.name).not.toThrow();

      runScene(scene, ctx, 'create');
      expect(() => harness.shutdownScene(ctx), entry.name).not.toThrow();
      // The second event fires with the scene's state already released.
      expect(() => harness.destroyScene(ctx), entry.name).not.toThrow();
      entry.assertReleased(ctx);
    }
  });

  it('does not accumulate lifecycle listeners when the scene is reused', () => {
    for (const entry of SCENES) {
      const harness = createSceneHarness();
      const scene = entry.build();
      const ctx = harness.context(scene, entry.key);

      runScene(scene, ctx, 'create');
      harness.shutdownScene(ctx);
      runScene(scene, ctx, 'create');
      harness.shutdownScene(ctx);
      runScene(scene, ctx, 'create');

      // Three runs, one teardown per event: `once` alone would leave every
      // earlier run's unfired DESTROY handler on the emitter.
      expect(harness.sceneListenerCount(ctx, 'shutdown'), entry.name).toBe(1);
      expect(harness.sceneListenerCount(ctx, 'destroy'), entry.name).toBe(1);

      // The registration left behind is the live one.
      entry.dirty(scene, ctx);
      harness.shutdownScene(ctx);
      entry.assertReleased(ctx);
    }
  });

  it('keeps every scene free of a method-name lifecycle assumption in the source', () => {
    // A pin on the mechanism for every scene in the repository that declares a
    // teardown, so a new scene cannot be added with the defect this task fixes.
    for (const file of [
      'BattleScene.ts',
      'MainMenuScene.ts',
      'LobbyScene.ts',
      'CollectionViewerScene.ts',
      'BattleHistoryScene.ts',
      'ResultScene.ts',
    ]) {
      const source = readFileSync(
        resolve(__dirname, `../src/game/scenes/${file}`),
        'utf8'
      ).replace(/\/\*[\s\S]*?\*\//g, '');

      expect(source, file).toMatch(/events\.once\(\s*Phaser\.Scenes\.Events\.SHUTDOWN/);
      expect(source, file).toMatch(/events\.once\(\s*Phaser\.Scenes\.Events\.DESTROY/);
      expect(source, file).toMatch(/shutdown\(\): void \{/);
    }
  });
});

describe('ResultScene — asynchronous reward loading (TASK-205)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** A deferred `getBattleResult` the test resolves by hand. */
  function deferredResult(): {
    readonly promise: Promise<unknown>;
    readonly release: (value: unknown) => void;
  } {
    let release: (value: unknown) => void = () => {};
    const promise = new Promise<unknown>((resolve) => {
      release = resolve;
    });
    return { promise, release };
  }

  /** One delivered `RewardSummary` (`DATABASE.md` §1). */
  const REWARDS = {
    playerXpGained: 100,
    newPlayerXp: 400,
    playerLeveledUp: true,
    newPlayerLevel: 5,
    petXpGained: 100,
    newPetXp: 900,
    petLeveledUp: false,
    newPetLevel: 10,
  };

  const OUTCOME = {
    outcome: 'victory',
    finalBossHp: 0,
    finalPlayerHp: 850,
    battleId: 'battle-1',
  };

  it('does not write to the presentation when the reward read settles after SHUTDOWN', async () => {
    // The E2E report's concrete risk: `loadRewards()` is in flight while the
    // player leaves, Phaser destroys the scene's Text objects, and the promise
    // then resolves. The delivered members must be dropped, not written.
    const harness = createSceneHarness({ textsThrowWhenDestroyed: false });
    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');

    const deferred = deferredResult();
    harness.runtime.getBattleResult = vi.fn(() => deferred.promise) as never;

    runScene(result, resultCtx, 'create', OUTCOME);
    expect(harness.runtime.getBattleResult).toHaveBeenCalledWith('battle-1');

    // Phaser stops the scene: the DisplayList destroys every game object and the
    // scene's own teardown runs on the same event.
    harness.shutdownScene(resultCtx);

    deferred.release({ battleId: 'battle-1', outcome: 'victory', rewards: REWARDS, durationTurns: 4 });
    await flush();

    // No write was even attempted against the run's destroyed reward Text, and
    // nothing about the ended presentation was re-created.
    expect(harness.destroyedWriteAttempts).toEqual([]);
    expect((resultCtx as { rewardText: unknown }).rewardText).toBeNull();
    expect(harness.texts.map((t) => t.text).join('\n')).not.toContain('REWARDS');
  });

  it('does not write the ended run’s rewards into a later run’s presentation', async () => {
    // Scene reuse is the other half: the instance is started again for the next
    // battle, and the previous run's read settles afterwards. `rewardText` now
    // points at a live Text, so the null-guard alone would let the stale
    // delivery overwrite the new presentation — only the run identity stops it.
    const harness = createSceneHarness({ textsThrowWhenDestroyed: false });
    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');

    const deferred = deferredResult();
    harness.runtime.getBattleResult = vi.fn(() => deferred.promise) as never;

    runScene(result, resultCtx, 'create', OUTCOME);
    harness.shutdownScene(resultCtx);

    // The player goes on to another result that needs no reward read at all.
    runScene(result, resultCtx, 'create', {
      outcome: 'defeat',
      finalBossHp: 412,
      finalPlayerHp: 0,
    });

    deferred.release({ battleId: 'battle-1', outcome: 'victory', rewards: REWARDS, durationTurns: 4 });
    await flush();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/DEFEAT/i);
    // The ended run's reward summary did not land in this presentation.
    expect(rendered).not.toContain('REWARDS');
    expect(harness.destroyedWriteAttempts).toEqual([]);
  });

  it('still renders the delivered rewards when the read settles inside the same run', async () => {
    // The guard must not break the documented behaviour it protects.
    const harness = createSceneHarness({ textsThrowWhenDestroyed: false });
    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');

    const deferred = deferredResult();
    harness.runtime.getBattleResult = vi.fn(() => deferred.promise) as never;

    runScene(result, resultCtx, 'create', OUTCOME);
    deferred.release({ battleId: 'battle-1', outcome: 'victory', rewards: REWARDS, durationTurns: 4 });
    await flush();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('REWARDS');
    expect(rendered).toContain('+100 XP');
    expect(rendered).toContain('900');
  });

  it('registers its teardown against the engine lifecycle events', () => {
    const harness = createSceneHarness();
    const result = new ResultScene();
    const resultCtx = harness.context(result, 'ResultScene');

    runScene(result, resultCtx, 'create', OUTCOME);

    expect(harness.sceneListenerCount(resultCtx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(resultCtx, 'destroy')).toBe(1);
  });
});
