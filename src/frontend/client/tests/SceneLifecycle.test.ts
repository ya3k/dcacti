import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { BattleScene, BATTLE_ORCHESTRATION_LEGAL_TRANSITIONS } from '../src/game/scenes/BattleScene';
import {
  adaptPresentedEvent,
  resolveBossSkillDisplayName,
  type InBattleServerEvent,
} from '../src/game/scenes/BattleEventPresenter';
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
  BattleStateMetadata,
  RuntimeBattleState,
  RuntimeBoard,
  RuntimeEventListener,
} from '../src/game/runtime/GameRuntimeEvents';
import { RuntimeActionOutcomeUnknownError } from '../src/game/runtime/GameRuntimeEvents';
import type { CardResponse, PetResponse } from '../src/services/api/CollectionModels';
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
  pets?: PetResponse[];
  /** The owned Relic instances the pre-battle flow reads (`GET /api/relics`). */
  relics?: Array<{ relicId: string; name: string }>;
  /** When set, the Relic collection read rejects with it. */
  relicsFailure?: Error;
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
  /**
   * Whether the scene is given a tween manager.
   *
   * It defaults to `false`, which is the harness every existing suite was written
   * against: with no `this.tweens` the scene writes every presentation value
   * immediately and in delivered order, so a synchronous assertion sees the
   * finished pass. A test that has to observe a *deferred*, sequential
   * presentation — a held phase, a travelling gauge, a settling gem view —
   * sets this to `true` and drives the queue with `flushTweens()`.
   */
  withTweens?: boolean;
}

function createSceneHarness(options: SceneHarnessOptions = {}) {
  const {
    state = INITIAL_RUNTIME_STATE,
    withRuntime = true,
    battleState = null,
    pets = [],
    relics = [],
    relicsFailure,
    textsThrowWhenDestroyed = true,
    withTweens = false,
  } = options;
  const listeners = new Set<RuntimeEventListener>();
  const battleStateListeners = new Set<
    (state: RuntimeBattleState | null, metadata?: BattleStateMetadata) => void
  >();
  const battleEventListeners = new Set<(envelope: BattleEventsEnvelope) => void>();
  /**
   * Every `Text` object the scene created, in creation order.
   *
   * The position and the style are recorded too, because TASK-210's HUD is a
   * layout: a test has to be able to see that a line was placed where the layout
   * says and that a line which can grow was given a word-wrap width and a
   * `maxLines` cap (`BattleScene`'s origin-`(0, 0)` banded lines).
   */
  const texts: Array<{
    text: string;
    color?: string;
    x: number;
    y: number;
    style?: Record<string, unknown>;
    /**
     * The index of the container this object is a child of, or `null` while it
     * belongs to none.
     *
     * The developer Move Log's panel is the one surface in this scene whose text
     * objects are *declared* — every one of them is a child of the panel's single
     * container (`BattleScene.developerMoveLog.layer`) — which is what lets a test
     * narrow a scene-wide diagnostic assertion to "everything except the panel's
     * own objects" by container membership rather than by a pattern that could also
     * hide a real regression (TASK-253 D-1).
     */
    readonly containerIndex: number | null;
  }> = [];
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
  const boardCells: Array<{
    kind: string;
    label?: string;
    fill?: number;
    strokeWidth?: number;
    strokeColor?: number;
  }> = [];
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
   * Every tween the scene has created and not yet had completed.
   *
   * Phaser's own `TweenManager` cannot run headlessly here, so the scene gets a
   * recorder instead: `add` queues a tween and `flushTweens` completes the queue
   * in order. That is what makes a *deferred* presentation observable — a phase
   * held until its pacing tween completes, a gauge travelling to its delivered
   * value, a gem view settling into its cell — without a running clock. The
   * recorder is only attached when `withTweens` is set, so every suite that
   * predates the timeline keeps the immediate, tween-less behaviour it asserts.
   */
  interface RecordedTween {
    readonly targets: ReadonlyArray<object>;
    readonly props: Record<string, unknown>;
    readonly duration: number;
    readonly onUpdate?: () => void;
    readonly onComplete?: () => void;
  }

  let pendingTweens: RecordedTween[] = [];
  /** Every target a `killTweensOf` has been asked for, in order. */
  const killedTweenTargets: object[] = [];

  const asTargets = (targets: unknown): object[] => {
    if (Array.isArray(targets)) {
      return targets.filter((target): target is object => typeof target === 'object' && target !== null);
    }

    return typeof targets === 'object' && targets !== null ? [targets] : [];
  };

  const tweens = {
    add: (config: Record<string, unknown>) => {
      const { targets, duration, onUpdate, onComplete, ease, ...props } = config;
      void ease;

      pendingTweens.push({
        targets: asTargets(targets),
        props,
        duration: typeof duration === 'number' ? duration : 0,
        ...(typeof onUpdate === 'function' ? { onUpdate: onUpdate as () => void } : {}),
        ...(typeof onComplete === 'function' ? { onComplete: onComplete as () => void } : {}),
      });

      return {};
    },
    killTweensOf: (target: unknown) => {
      const list = asTargets(target);
      for (const entry of list) {
        killedTweenTargets.push(entry);
      }

      pendingTweens = pendingTweens.filter(
        (tween) => !tween.targets.some((candidate) => list.includes(candidate))
      );
    },
    killAll: () => {
      pendingTweens = [];
    },
  };

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
    onBattleState: (listener: (state: RuntimeBattleState | null) => void) => {
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
    getCards: vi.fn(
      async (): Promise<CardResponse[]> => [
        // API_CONTRACTS.md §5.3: membership of this array IS the unlocked state.
        // The starter grant creates exactly these three Basic unlock rows and no
        // Pet Skill row (`PlayerStarterGrantFactory.cs`; `CARD_RULES.md` §1 item 4,
        // ADR-012 item 9), so a fixture that put a `PetSkill` definition here would
        // certify behaviour production cannot reach. The derived Signature Skill is
        // delivered by `GET /api/pets` instead (§5.1) — which is exactly what the
        // Signature Skill identification tests below rely on. Each `effectDefinition`
        // is the Card's own stored content (`CARD_RULES.md` §2).
        {
          cardId: 'card-heal',
          name: 'Heal',
          category: 'Basic',
          effectDefinition: [{ effectType: 'Heal', valueType: 'PercentMaxHp', value: 20 }],
        },
        {
          cardId: 'card-shield',
          name: 'Shield',
          category: 'Basic',
          effectDefinition: [{ effectType: 'Shield', valueType: 'PercentMaxHp', value: 20 }],
        },
        {
          cardId: 'card-power-charge',
          name: 'Power Charge',
          category: 'Basic',
          effectDefinition: [{ effectType: 'Power', valueType: 'Flat', value: 25 }],
        },
      ]
    ),
    getPets: vi.fn(async () => pets),
    getPet: vi.fn(async () => pets[0] ?? ({} as never)),
    getRelics: vi.fn(async () => {
      if (relicsFailure) {
        throw relicsFailure;
      }
      return relics;
    }),
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
    const makeText = (
      x: number,
      y: number,
      value: string,
      into?: { kind: string; label: string }[],
      style?: Record<string, unknown>
    ) => {
      const entry = {
        text: value,
        color: undefined as string | undefined,
        x,
        y,
        style,
        get containerIndex(): number | null {
          return containerIndex;
        },
      };
      texts.push(entry);
      let alpha = 1;
      let fontSize: number | undefined;
      /**
       * Whether the object is drawn.
       *
       * The synchronization indicator is created hidden and shown only while the
       * runtime reports `unsynchronized` (`GameRuntimeState.sync`), so a test has
       * to be able to read the line's visibility as the scene actually set it.
       */
      let visible = true;
      /** The container this object was added to, as the harness numbered it. */
      let containerIndex: number | null = null;
      /**
       * The object's live position.
       *
       * A persistent gem view is *moved* rather than recreated (TASK-245), so the
       * position has to be readable after the fact — the board container holds the
       * object itself, and `entry` above keeps the creation-time snapshot the
       * existing layout tests assert on.
       */
      let liveX = x;
      let liveY = y;
      let scale = 1;

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
        /**
         * The label currently drawn.
         *
         * It is a *live* read of the object's text, not the creation-time value: a
         * persistent gem view is relabelled in place when the authoritative board
         * changes (TASK-245), and the board container holds the object itself.
         */
        get label() {
          return entry.text;
        },
        get x() {
          return liveX;
        },
        set x(next: number) {
          liveX = next;
        },
        get y() {
          return liveY;
        },
        set y(next: number) {
          liveY = next;
        },
        get text() {
          return entry.text;
        },
        /** The colour the scene last set, as `setColor` received it. */
        get color() {
          return entry.color;
        },
        /** The style config the scene passed to `add.text`, as Phaser stores it. */
        get style() {
          return style;
        },
        /** The container this object is a child of, or `null` while it is in none. */
        get containerIndex() {
          return containerIndex;
        },
        /** Set by the container that takes this object as a child. */
        set containerIndex(next: number | null) {
          containerIndex = next;
        },
        /** The font size the scene last set, in pixels. */
        get fontSize() {
          return fontSize;
        },
        get interactive() {
          return interactive;
        },
        setOrigin: () => obj,
        /** Phaser's `GameObject#setPosition` — a persistent view is moved, not recreated. */
        setPosition: (nextX: number, nextY: number) => {
          liveX = nextX;
          liveY = nextY;
          return obj;
        },
        /** Phaser's `Text#setFontSize` — a cell label's font size is drawn from it. */
        setFontSize: (next: number | string) => {
          fontSize = typeof next === 'number' ? next : Number.parseInt(next, 10);
          return obj;
        },
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
        /**
         * Phaser's `GameObject#setAlpha`. The combat callout sets its alpha back to
         * 1 when a new message replaces an older one, so the mock records it.
         */
        setAlpha: (next: number) => {
          alpha = next;
          return obj;
        },
        get alpha() {
          return alpha;
        },
        set alpha(next: number) {
          alpha = next;
        },
        setScale: (next: number) => {
          scale = next;
          return obj;
        },
        get scale() {
          return scale;
        },
        set scale(next: number) {
          scale = next;
        },
        /** Phaser's `GameObject#setVisible` — the sync indicator is shown/hidden in place. */
        setVisible: (next: boolean) => {
          visible = next;
          return obj;
        },
        get visible() {
          return visible;
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
      const myIndex = ++containerIndex;
      const isBoard = myIndex === 1;
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
        /**
         * This container's own number, so a test can name a panel by the container
         * that owns its objects rather than by a property of each object.
         */
        containerIndex: myIndex,
        add: (added: unknown) => {
          const list = Array.isArray(added) ? added : [added];
          children = children.concat(
            list as Array<{
              kind: string;
              label?: string;
              strokeWidth?: number;
              strokeColor?: number;
            }>
          );
          // Phaser reparents each child into the container; the mock records the
          // same fact, which is how a test tells a panel's own text objects from
          // every other line the scene draws (TASK-253 D-1).
          for (const child of list) {
            if (typeof child === 'object' && child !== null && 'containerIndex' in child) {
              (child as { containerIndex: number | null }).containerIndex = myIndex;
            }
          }
          if (isBoard) syncBoard();
          return obj;
        },
        removeAll: () => {
          children = [];
          if (isBoard) syncBoard();
          return obj;
        },
        /**
         * Phaser's `Container#list`: the children the container currently owns.
         *
         * A test reads a panel's own objects through it — the developer Move Log
         * registers its row text objects as container children, so "the panel's own
         * declared objects" is a property of the scene rather than of the mock.
         */
        get list() {
          return children;
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
      // Phaser's `scene.tweens` (`Systems#tweens` → `TweenManager`), attached
      // only when the test asked for the recorded, test-driven queue.
      ...(withTweens ? { tweens } : {}),
      scene: {
        start: (key: string, data?: unknown) => sceneStarted.push({ key, data }),
      },
      add: {
        rectangle: (x = 0, y = 0, width = 0, height = 0, fill = 0) => {
          const handlers: Array<() => void> = [];
          let interactive = false;
          let visible = true;
          let strokeWidth = 0;
          let strokeColor = 0;
          let fillColor = fill;
          let alpha = 1;
          let scale = 1;
          const rect = {
            kind: 'tile',
            text: '',
            x,
            y,
            width,
            height,
            get strokeWidth() {
              return strokeWidth;
            },
            get strokeColor() {
              return strokeColor;
            },
            /**
             * The colour currently drawn.
             *
             * A persistent gem view is *relabelled* in place when the
             * authoritative board changes (TASK-245) rather than being destroyed
             * and recreated, so the fill has to be readable after the fact.
             */
            get fill() {
              return fillColor;
            },
            get interactive() {
              return interactive;
            },
            /**
             * Phaser's `Rectangle#setSize`: it resizes the underlying geometry and
             * re-derives the display origin, so the shape stays centred on its
             * position. The HUD gauges resize their existing fill with it rather
             * than creating a rectangle per state push (TASK-210 §15), so the mock
             * has to model it for a fill width to be observable at all.
             */
            setSize: (nextWidth: number, nextHeight: number) => {
              rect.width = nextWidth;
              rect.height = nextHeight;
              return rect;
            },
            /** Phaser's `Rectangle#setFillStyle` — the gem view's delivered Gem colour. */
            setFillStyle: (next: number) => {
              fillColor = next;
              return rect;
            },
            /** Phaser's `GameObject#setPosition`. */
            setPosition: (nextX: number, nextY: number) => {
              rect.x = nextX;
              rect.y = nextY;
              return rect;
            },
            /** Phaser's `GameObject#setVisible` — a zero-width fill is hidden. */
            setVisible: (next: boolean) => {
              visible = next;
              return rect;
            },
            get visible() {
              return visible;
            },
            setAlpha: (next: number) => {
              alpha = next;
              return rect;
            },
            get alpha() {
              return alpha;
            },
            set alpha(next: number) {
              alpha = next;
            },
            setScale: (next: number) => {
              scale = next;
              return rect;
            },
            get scale() {
              return scale;
            },
            set scale(next: number) {
              scale = next;
            },
            setStrokeStyle: (w = 1, c = 0x0b0f19) => {
              strokeWidth = w;
              strokeColor = c;
              return rect;
            },
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
        text: (x: number, y: number, value: string, style?: Record<string, unknown>) =>
          makeText(x, y, value, undefined, style),
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
      // TASK-273: deliver the batch in the documented order. The server pushes the
      // resolution's state *before* its batch (`SIGNALR_PROTOCOL.md` §3.1, §6
      // item 4), and the scene admits a batch only when the authoritative state it
      // holds represents it. A harness that already delivered a state that does
      // not yet carry the batch's own battle and `serverSequence` (the values the
      // server would have written back) re-keys that delivered state first — every
      // other delivered member is left exactly as the test configured it, and no
      // fixture value is invented here. A test that already delivered the matching
      // state itself is left alone, so its own delivery is the one exercised.
      const baseState = currentBattleState ?? {
        battleId: envelope.battleId,
        sequence: envelope.serverSequence,
        turn: 1,
        board: {
          cells: Array.from({ length: 64 }, (_, idx) => ({
            cellIndex: idx,
            gemType: 'FIRE',
          })),
        },
        playerState: { hp: 1000, maxHp: 1000, power: 0, statusEffects: [] },
        petState: {
          hp: 1000,
          maxHp: 1000,
          power: 0,
          statusEffects: [],
          equippedCards: [],
          passiveId: null,
          passiveProgress: { current: 0, threshold: 0 },
        },
        bossState: {
          bossId: 'boss-test',
          hp: 3000,
          maxHp: 3000,
          power: 0,
          statusEffects: [],
          enrageThreshold: 0,
          isEnraged: false,
        },
      };

      const alreadyDelivered =
        currentBattleState !== null &&
        currentBattleState.battleId === envelope.battleId &&
        currentBattleState.sequence === envelope.serverSequence;

      if (!alreadyDelivered) {
        currentBattleState = {
          ...baseState,
          battleId: envelope.battleId,
          sequence: envelope.serverSequence,
        } as unknown as RuntimeBattleState;

        for (const listener of battleStateListeners) {
          listener(currentBattleState, { source: 'push' });
        }
      }

      for (const listener of battleEventListeners) {
        listener(envelope);
      }
    },
    /**
     * Delivers a batch **without** the state push that precedes it
     * (`SIGNALR_PROTOCOL.md` §3.1, §6 item 4).
     *
     * It exists for the one test family that must exercise the scene's own
     * delivery-integrity gate (`TASK-273` §4.2): a batch the authoritative state
     * the scene holds does not represent — a foreign battle's, one ahead of the
     * delivered `sequence`, or one behind it. `emitBattleEvents` deliberately
     * cannot express that, because it delivers the documented order.
     */
    emitUnmatchedBattleEvents: (envelope: BattleEventsEnvelope) => {
      for (const listener of battleEventListeners) {
        listener(envelope);
      }
    },
    /**
     * How many tweens the scene has created and not yet completed.
     *
     * It is the observable behind "no leaked tween": a presentation that
     * finished, aborted, was superseded, or was torn down must leave none of its
     * own pacing behind.
     */
    pendingTweenCount: () => pendingTweens.length,
    /**
     * The pending tweens' own durations, targets, and completion callback, in
     * creation order.
     *
     * It is what makes "a phase's hold covers the animations that phase starts"
     * (TASK-246 §5) an assertion about the scene's real tween configuration
     * rather than about a constant copied into this suite: a test can find the
     * pacing tween by its target (the scene's own `phaseClock`) and compare its
     * duration with every animation tween that same phase queued.
     *
     * `completion` is the callback Phaser would fire when the tween finishes. A
     * test can hold it and invoke it *after* the tween has been killed, which is
     * the one shape a killed tween cannot reproduce: a callback that was already
     * in flight when an authoritative recovery abandoned the timeline it belonged
     * to (P0.3 §4).
     */
    pendingTweenRecords: () =>
      pendingTweens.map((tween) => ({
        duration: tween.duration,
        targets: tween.targets,
        props: tween.props,
        completion: tween.onComplete,
      })),
    /** The targets `killTweensOf` has been asked for, in order. */
    killedTweenTargets,
    /**
     * Completes pending tweens in creation order, applying each tween's final
     * property values and firing its `onUpdate` / `onComplete`.
     *
     * A completion may enqueue the next phase's tween, so the queue legitimately
     * grows while it drains. `limit` caps how many tweens this call completes: a
     * limit of `1` advances exactly one step of a presentation, which is how a
     * test observes a *sequential* timeline phase by phase.
     */
    flushTweens: (limit = Number.POSITIVE_INFINITY) => {
      let completed = 0;

      while (pendingTweens.length > 0 && completed < limit) {
        const tween = pendingTweens.shift() as RecordedTween;
        completed++;

        for (const target of tween.targets) {
          for (const [key, value] of Object.entries(tween.props)) {
            if (typeof value === 'number') {
              (target as Record<string, unknown>)[key] = value;
            } else if (typeof value === 'object' && value !== null && 'to' in value) {
              (target as Record<string, unknown>)[key] = (value as { to: unknown }).to;
            }
          }
        }

        tween.onUpdate?.();
        tween.onComplete?.();
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
    setBattleState: (next: RuntimeBattleState | null, metadata?: BattleStateMetadata) => {
      currentBattleState = next;
      for (const listener of battleStateListeners) {
        listener(next, metadata);
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
 * The five **provisioned** MVP Pets as `GET /api/pets` elements
 * (`API_CONTRACTS.md` §5.1, `PET_RULES.md` §8, `CARD_RULES.md` §4.1).
 *
 * ```text
 * Pet        element   SignatureSkillCardId      Skill
 * Xích Lang  Fire      card-inferno              Inferno
 * Bạch Hổ    Metal     card-iron-fang            Iron Fang
 * Huyền Quy  Water     card-tidal-barrier        Tidal Barrier
 * Thanh Xà   Wood      card-venomous-bloom       Venomous Bloom
 * Sơn Hùng   Earth     card-earthshaker          Earthshaker
 * ```
 *
 * These are the responses' own members — the Pet's own
 * `PetDefinition.SignatureSkillCardId` reference resolved through its
 * `CardDefinition` — transcribed so an identification can be asserted value by
 * value across **all five** Pets. They are not a client-side content source: the
 * scene reads only the response it is handed, and every one of these references
 * is deliberately absent from the Card collection (`§5.3`), which is what makes
 * a Category-based identification impossible.
 */
const PROVISIONED_PET_SIGNATURE_SKILLS: ReadonlyArray<{
  readonly petId: string;
  readonly identity: string;
  readonly element: PetResponse['element'];
  readonly cardId: string;
  readonly name: string;
}> = [
  { petId: 'pet-instance-1', identity: 'Xích Lang', element: 'Fire', cardId: 'card-inferno', name: 'Inferno' },
  { petId: 'pet-instance-2', identity: 'Bạch Hổ', element: 'Metal', cardId: 'card-iron-fang', name: 'Iron Fang' },
  { petId: 'pet-instance-3', identity: 'Huyền Quy', element: 'Water', cardId: 'card-tidal-barrier', name: 'Tidal Barrier' },
  { petId: 'pet-instance-4', identity: 'Thanh Xà', element: 'Wood', cardId: 'card-venomous-bloom', name: 'Venomous Bloom' },
  { petId: 'pet-instance-5', identity: 'Sơn Hùng', element: 'Earth', cardId: 'card-earthshaker', name: 'Earthshaker' },
];

/** All five provisioned Pets, each carrying its own derived Signature Skill. */
function provisionedPets(): PetResponse[] {
  return PROVISIONED_PET_SIGNATURE_SKILLS.map((pet) => ({
    petId: pet.petId,
    identity: pet.identity,
    element: pet.element,
    tier: 'Common',
    star: 1,
    level: 1,
    signatureSkill: { cardId: pet.cardId, name: pet.name, category: 'PetSkill' },
  }));
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

/**
 * The index of the developer Move Log's single declared container in a scene
 * context, or `null` when the surface does not exist.
 *
 * TASK-253's panel is the one development surface authorized to render the
 * delivered turn/sequence and the raw event feed, and every text object it draws
 * is a child of that one container (operator decision D-1). A scene-wide assertion
 * about what is *not* rendered therefore subtracts exactly those objects — by
 * container membership, never by a pattern that could also excuse a regression.
 */
function moveLogContainerIndex(ctx: object): number | null {
  const log = (ctx as { developerMoveLog?: { layer?: { containerIndex?: number } } | null })
    .developerMoveLog;
  const index = log?.layer?.containerIndex;

  return typeof index === 'number' ? index : null;
}

/** Every line the scene renders **except** the developer Move Log's own text objects. */
function playerFacingText(
  harness: ReturnType<typeof createSceneHarness>,
  ctx: object
): string {
  const logIndex = moveLogContainerIndex(ctx);

  return harness.texts
    .filter((entry) => logIndex === null || entry.containerIndex !== logIndex)
    .map((entry) => entry.text)
    .join('\n');
}

/**
 * The developer Move Log's own declared text objects, in creation order — the
 * caption followed by the visible rows.
 */
function panelText(harness: ReturnType<typeof createSceneHarness>, ctx: object): string {
  const logIndex = moveLogContainerIndex(ctx);

  return harness.texts
    .filter((entry) => logIndex !== null && entry.containerIndex === logIndex)
    .map((entry) => entry.text)
    .join('\n');
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

  it('says nothing about the connection while the battle is connected', () => {
    // TASK-209 §5: the connection line is the HUD's only infrastructure statement
    // and it is player-facing. A connected battle needs no statement at all, and
    // the developer diagnostics it used to show — a "Runtime …" label, `SignalR:`,
    // `Sync:`, `Conn:` — are not the player's battle HUD.
    const { harness, scene, ctx } = createBattle({
      ...INITIAL_RUNTIME_STATE,
      connection: 'connected',
      connectionId: 'abc',
      runtime: 'ready',
      sync: 'awaiting_battle',
      engine: 'running',
    });

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text);
    expect(rendered).not.toContain('Connecting…');
    expect(rendered).not.toContain('Reconnecting…');
    for (const diagnostic of ['Runtime Connected', 'SignalR:', 'Sync:', 'Conn:']) {
      expect(rendered.join('\n')).not.toContain(diagnostic);
    }
  });

  it('renders an unconnected runtime as connecting, not as ready', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.texts.map((t) => t.text)).toContain('Connecting…');
    expect(harness.texts.map((t) => t.text)).not.toContain('Runtime Connected');
  });

  it('renders a failed connection as unavailable', () => {
    const { harness, scene, ctx } = createBattle({
      ...INITIAL_RUNTIME_STATE,
      connection: 'error',
      runtime: 'error',
    });

    runScene(scene, ctx, 'create');

    expect(harness.texts.map((t) => t.text)).toContain('Battle unavailable');
  });

  it('renders the reconnecting state distinctly', () => {
    const { harness, scene, ctx } = createBattle({
      ...INITIAL_RUNTIME_STATE,
      connection: 'reconnecting',
      runtime: 'suspended',
    });

    runScene(scene, ctx, 'create');

    expect(harness.texts.map((t) => t.text)).toContain('Reconnecting…');
  });

  it('survives a scene with no runtime supplied', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, false);

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
    expect(harness.texts.map((t) => t.text)).toContain('Battle unavailable');
  });

  it('updates its readout when the runtime state changes', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.texts.map((t) => t.text)).toContain('Connecting…');

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

    // A connected battle has nothing to announce, so the line it rendered while
    // connecting is cleared (TASK-209 §5).
    expect(harness.texts.map((t) => t.text)).not.toContain('Connecting…');
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
    // TASK-209 stage advance. The scene now presents the player-facing battle HUD,
    // so "a boss readout" and "a combo readout" are no longer violations by name:
    // §4.4's Boss identity and live health and §4.2/§6's delivered Combo and Match
    // count are authoritative values the player is meant to see. What remains
    // forbidden is **client-computed resolution presentation**: a match, cascade,
    // or damage *result* the client would have had to calculate
    // (GAME_RULES.md §18, ADR-001). This harness supplies no battle state at all,
    // so nothing resolution-shaped can be rendered here — and the forbidden terms
    // pin the raw event feed and any computed result.
    const rendered = harness.texts.map((t) => t.text).join(' ');
    for (const forbidden of ['Damage', 'Cascade', 'Match:', 'depth:', 'calculated']) {
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
   * Each cell is the delivered `CellPayload` shape — its Gem type and nothing else
   * (`GAME_STATE.md` §2.1.3 items 1–4, `SIGNALR_PROTOCOL.md` §4.1 item 5), i.e.
   * `{ gemType }`. A bare Gem-name string is **not** what the protocol delivers,
   * and modelling it as one is what previously let a mismatched client reader
   * pass this suite.
   */
  function serverBoard(): Array<{ gemType: string }> {
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
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      // SIGNALR_PROTOCOL.md §4.4: the two-member Boss HP projection. Both members
      // are always present and neither is nullable (§4.4 item 4).
      bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
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

  it('renders the battle HUD the server delivered', () => {
    // TASK-209 §4 / TASK-210 §1: the player-facing HUD presents the Boss's identity
    // and live health, the active Pet's name, HP, Max HP and Power, and the
    // delivered Match counter — every value read straight from the authoritative
    // push. TASK-210 widened HP and Power into gauges drawn *beside* those same
    // numbers, so the numbers themselves are still what this asserts. The
    // developer readout the HUD replaced (battle id, turn, sequence, RNG, transport
    // state) is deliberately gone (§5).
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Hỏa Long');
    expect(rendered).toContain('5000 / 5000');
    expect(rendered).toContain('1000 / 1000');
    expect(rendered).toContain('0 / 100');
    expect(rendered).toContain('MATCHES 0');

    for (const diagnostic of ['BattleId:', 'Turn:', 'Sequence:', 'RngSeed:', 'SignalR:', 'Sync:']) {
      expect(rendered).not.toContain(diagnostic);
    }
  });

  it('renders the delivered initial values verbatim', () => {
    // §4.3 item 15 / §4.4 item 4: a battle begins with both sides at full health
    // and no Power generated yet. Those are the delivered values and they are
    // printed as sent — zero is a value here, not an absence — while the derived
    // readouts (turn, sequence, RNG) are no longer rendered at all.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('1000 / 1000');
    expect(rendered).toContain('0 / 100');
    expect(rendered).not.toMatch(/Turn: 0\b/);
    expect(rendered).not.toMatch(/Sequence: 0\b/);
  });

  it('renders the delivered Pet live combat values verbatim', () => {
    // §4.3 item 15 / GAME_STATE.md §2.3: the active Pet's current HP, Max HP and
    // Power reach the client as three authoritative `petState` members and are
    // printed as received. `hp`, `maxHp` and `power` are independent — the scene
    // damages, heals, clamps, and reconstructs none of them.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      petState: {
        hp: 640,
        maxHp: 1000,
        power: 45,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('640 / 1000');
    expect(rendered).toContain('45 / 100');
    // The scene exposes no derived value: no remaining-HP percentage, no clamped
    // HP, and no Power reconstructed from an event.
    expect(rendered).not.toContain('%');
  });

  it('renders the active Pet name from the client loadout and catalog', async () => {
    // §4.3 item 2 / ADR-014: `PetState` carries no client-visible identity, so the
    // HUD resolves the Pet's display name from the client's own preserved
    // pre-battle loadout (ADR-022) plus the owned-Pet collection read — never from
    // the wire, and never as a guessed name.
    const harness = createSceneHarness({
      state: INITIAL_RUNTIME_STATE,
      battleState: serverState(),
      pets: [
        {
          petId: 'pet-instance-1',
          identity: 'Thanh Xà',
          element: 'Fire',
          tier: 'Common',
          star: 1,
          level: 1,
          signatureSkill: {
            cardId: 'card-venomous-bloom',
            name: 'Venomous Bloom',
            category: 'PetSkill',
          },
        },
      ],
    });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    preserveLoadout(ctx as never, {
      petId: 'pet-instance-1',
      bossId: 'boss-hoa-long',
      cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
      relicLoadout: ['relic-1', 'relic-2', 'relic-3'],
    });

    runScene(scene, ctx, 'create');

    await flush();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Thanh Xà');
  });

  it('falls back to the identity, never a guessed name, when the Pet is unknown', () => {
    // A loadout identity the collection does not contain (or no loadout at all)
    // renders what the client actually has: the identity it was given, or a
    // neutral placeholder. Fabricating a name would be client-authored content
    // standing in for state (AGENTS.md §7).
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Pet');
    // The Pet's HP and Power are still the authoritative delivered values.
    expect(rendered).toContain('1000 / 1000');
    expect(rendered).toContain('0 / 100');
  });

  it('renders the delivered Passive progress pair verbatim', () => {
    // SIGNALR_PROTOCOL.md §4.3 / PASSIVE_RULES.md §6 item 1: the delivered
    // `current / threshold` pair is presented as the UI-facing value. The scene
    // prints both numbers as received — it charges nothing, evaluates no
    // Threshold, and resets nothing (§4.3 item 9).
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
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
        hp: 1000,
        maxHp: 1000,
        power: 0,
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

  it('renders the delivered Boss identity, hp and maxHp verbatim', () => {
    // SIGNALR_PROTOCOL.md §4.4: the Boss reaches the client as the three-member
    // `bossState` projection — the canonical technical Identity plus its live
    // health. The scene prints the numbers as received (it damages nothing, clamps
    // nothing, and infers neither value from the other, §4.4 item 7) and resolves
    // the display name from the client's own single Boss catalog by that identity
    // (§4.4 item 10) — no display string travels on the wire.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      bossState: { bossId: 'boss-kim-loi-vuong', hp: 4200, maxHp: 5000 },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('Kim Lôi Vương');
    expect(rendered).toContain('4200 / 5000');
  });

  it('renders an unrecognized Boss identity as the identity, never a guessed name', () => {
    // §4.4 item 10: an identity the client catalog does not recognize is rendered
    // as the identity itself. Fabricating a display name would be client-authored
    // content standing in for authoritative state.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      bossState: { bossId: 'boss-not-in-catalog', hp: 100, maxHp: 200 },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('boss-not-in-catalog');
    expect(rendered).toContain('100 / 200');
  });

  it('renders the delivered Status Effects compactly, and none as an empty collection', () => {
    // SIGNALR_PROTOCOL.md §4.3 item 14 / GAME_STATE.md §2.3.1: the active Pet's
    // active instances are presented as delivered. The scene applies, refreshes,
    // decrements, expires, and removes nothing, and evaluates no duration or
    // expiry condition (§4.3 item 14, GAME_STATE.md §5.1.1).
    //
    // TASK-210 §6 narrows this line to the facts that identify an effect to a
    // player — its delivered `Id`, its applied `Magnitude` and the one duration
    // model its element carries — so the line is asserted exactly, verbatim
    // values and all. The instance's `Type` (`"DoT"`) and `Source` (`"boss"`) are
    // its internal classification and are deliberately no longer printed.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
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

    expect(rendered).toContain('Effects: Burn (25, 2 turns); Shield (100, ShieldDepleted)');
    // Neither an effect's identity, its magnitude, nor either duration model is
    // rewritten: the two mutually exclusive models are printed as carried.
    expect(rendered).toContain('25');
    expect(rendered).toContain('2 turns');
    expect(rendered).toContain('ShieldDepleted');
    expect(rendered).toContain('100');
    // The empty collection is not this payload's case, so "none" is not shown.
    expect(rendered).not.toContain('Effects: none');
    // The internal classification is not a player-facing fact and is not shown.
    expect(rendered).not.toContain('DoT');
  });

  it('renders an unknown Status Effect identity as the identity it was sent', () => {
    // TASK-210 §6: an effect identity this client has no definition for is printed
    // as the delivered identity. The client holds no Status Effect catalog, so
    // inventing a name, an icon, or a duration would be inventing content
    // (AGENTS.md §7, GAME_STATE.md §2.3.1 item 1: `Id` is an identity, not a
    // definition).
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        // A defensive element carrying neither duration member: the contract does
        // not produce one (§2.3.1 item 3), and it is rendered neutrally rather
        // than with an invented duration.
        statusEffects: [{ id: 'internal-effect-7', type: 'BuffDebuff', source: 'boss', magnitude: 3 }],
      },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('Effects: internal-effect-7 (3, no duration)');
    expect(rendered).not.toContain('BuffDebuff');
  });

  it('renders the empty Status Effect collection as the documented no-effect case', () => {
    // §4.3 item 14: an active Pet with no active effect is sent an EMPTY ARRAY —
    // never an omission. The scene states that as the documented empty case
    // rather than leaving the line blank.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');

    expect(rendered).toContain('Effects: none');
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

  it('renders no battle values until the server pushes state', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('BattleId:');
    expect(rendered).not.toMatch(/Turn:/);
    expect(rendered).not.toMatch(/Sequence:/);
    // No HP, Power, or identity is invented to fill the gap either: the panels stay
    // empty and the board area states that it is still waiting.
    expect(rendered).not.toMatch(/HP\s+\d/);
    expect(rendered).not.toMatch(/POWER\s+\d/);
    expect(rendered).toContain('Preparing the board…');

    // And no board is drawn: the client never generates one to fill the gap
    // (SIGNALR_PROTOCOL.md §4 item 10).
    expect(harness.boardCells).toHaveLength(0);
  });

  it('updates the HUD and board when the runtime receives new state', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    harness.setBattleState(serverState({
      bossState: { bossId: 'boss-thuy-ma', hp: 3100, maxHp: 5000 },
      petState: {
        hp: 750,
        maxHp: 1000,
        power: 40,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
    }));

    // The scene displays what the runtime received — it decides nothing.
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Thủy Ma');
    expect(rendered).toContain('3100 / 5000');
    expect(rendered).toContain('750 / 1000');
    expect(rendered).toContain('40 / 100');
    expect(harness.boardCells.filter((c) => c.kind === 'label')).toHaveLength(64);
  });

  it('does not own the state: it renders unchanged server values', () => {
    // The scene never computes, adjusts, or recomputes these values
    // (SIGNALR_PROTOCOL.md §4.9, ADR-001). The delivered combat values are the ones
    // on screen, untouched — no clamp, no heal, no reconstruction.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      battleId: 'battle-owned-by-server',
      turn: 7,
      sequence: 12,
      petState: {
        hp: 999,
        maxHp: 1000,
        power: 100,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { bossId: 'boss-moc-yeu', hp: 1, maxHp: 5000 },
    }));

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('999 / 1000');
    expect(rendered).toContain('100 / 100');
    expect(rendered).toContain('1 / 5000');
    expect(rendered).toContain('Mộc Yêu');
    // Turn and sequence are not rendered any more (TASK-209 §5) — the delivered
    // values are still the ones the board and HUD were drawn from.
    expect(rendered).not.toContain('Turn: 7');
    expect(rendered).not.toContain('Sequence: 12');
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
    expect(rendered).toContain('Effects:');
    expect(rendered).not.toMatch(/\bStatus:\s/);
  });

  it('presents no gameplay system state alongside the board', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    // The board and its four Gem types are part of this stage, as are the
    // delivered `petState` members (the Passive and the live combat values, §4.3)
    // and the three-member Boss projection (§4.4). Card casts are implemented in
    // TASK-120. The gameplay systems this stage does not implement remain
    // unmodelled and unrendered: resolution, Damage, and Relics
    // (GAME_STATE.md §2.0.5.3). `Combo` is no longer in that list: MATCH3_RULES.md
    // §6 makes the delivered `playerState.combo` a player-facing value, and
    // TASK-209 §4 requires it to be presented when it is meaningful.
    const rendered = harness.texts.map((t) => t.text).join(' ');
    for (const forbidden of ['Damage', 'Relic', ['Pending', 'Special'].join('')]) {
      expect(rendered).not.toMatch(new RegExp(forbidden, 'i'));
    }

    // The Boss line is the delivered identity and `hp`/`maxHp` pair and nothing
    // else: no Boss ATK/DEF, no Element, no State, no Passive progress, no Skill
    // charge/cooldown, and no Boss Status Effects (§4.4 items 3 and 10).
    expect(rendered).toContain('5000 / 5000');
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

  it('presents the delivered Combo when it is meaningful, and the Match count always', () => {
    // TASK-209 §4: MATCH3_RULES.md §6 makes `playerState.combo` a delivered,
    // player-facing value. A published `combo` of 1 is an ordinary single-Match
    // Swap (§6.2 item 1), so the line appears from 2 upward, and the cumulative
    // Match count is always shown. Neither value is counted, advanced, or reset by
    // the scene (§6.6 item 3).
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      playerState: { combo: 0, matchCount: 0 },
    }));

    runScene(scene, ctx, 'create');
    expect(harness.texts.map((t) => t.text).join('\n')).not.toContain('COMBO');
    expect(harness.texts.map((t) => t.text).join('\n')).toContain('MATCHES 0');

    // A single-Match Swap is still not a combo.
    harness.setBattleState(serverState({ playerState: { combo: 1, matchCount: 3 } }));
    let rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('COMBO');
    expect(rendered).toContain('MATCHES 3');

    // Two or more Matches in one committed Swap is the value the player cares
    // about, printed exactly as delivered.
    harness.setBattleState(serverState({ playerState: { combo: 4, matchCount: 9 } }));
    rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('COMBO ×4');
    expect(rendered).toContain('MATCHES 9');
  });

  // ---------------------------------------------------------------------------
  // TASK-210 — Battle feedback & presentation
  // ---------------------------------------------------------------------------

  /**
   * One HUD gauge as the scene's own `HudGauge` value exposes it: the fill's
   * current geometry and visibility, plus the track's declared geometry.
   */
  interface GaugeView {
    readonly x: number;
    readonly y: number;
    readonly width: number;
    readonly height: number;
    readonly fill: {
      readonly x: number;
      readonly y: number;
      readonly width: number;
      readonly height: number;
      readonly visible: boolean;
    };
  }

  /** A scene-owned presentation field, read the way the scene wrote it. */
  function field<T>(ctx: object, name: string): T {
    return (ctx as Record<string, T>)[name];
  }

  /** The delivered `petState` the HUD tests vary, with the rest at its defaults. */
  function petPresentation(overrides: {
    hp?: number;
    maxHp?: number;
    power?: number;
    passiveProgress?: { threshold: number; current: number };
    passiveId?: string;
    passiveResetOverride?: string;
  }): RuntimeBattleState {
    return serverState({
      petState: {
        hp: overrides.hp ?? 1000,
        maxHp: overrides.maxHp ?? 1000,
        power: overrides.power ?? 0,
        passiveId: overrides.passiveId ?? 'xich-lang',
        passiveProgress: overrides.passiveProgress ?? { threshold: 5, current: 0 },
        ...(overrides.passiveResetOverride !== undefined
          ? { passiveResetOverride: overrides.passiveResetOverride }
          : {}),
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
    });
  }

  it('draws the Boss and Pet HP gauges full when both sides are at full health', () => {
    // TASK-210 §1: the HP presentation is a gauge of the authoritative
    // `hp / maxHp` pair, and the numbers stay on screen beside it. A full bar is
    // the whole track, exactly as delivered — nothing is inferred.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('5000 / 5000');
    expect(rendered).toContain('1000 / 1000');

    const boss = field<GaugeView>(ctx, 'bossHpGauge');
    const pet = field<GaugeView>(ctx, 'petHpGauge');
    expect(boss.fill.width).toBe(boss.width);
    expect(pet.fill.width).toBe(pet.width);
    expect(boss.fill.visible).toBe(true);
    expect(pet.fill.visible).toBe(true);
  });

  it('sizes each HP gauge to the delivered current / max pair', () => {
    // The bar is only a proportion (`ARCHITECTURE.md` §2.2.2's `barWidth =
    // current / max`): the authoritative numbers are rendered as sent beside it,
    // and the fill is never read back into them.
    const { harness, scene, ctx } = createBattle(
      INITIAL_RUNTIME_STATE,
      true,
      petPresentation({ hp: 640, maxHp: 1000 })
    );

    runScene(scene, ctx, 'create');
    harness.setBattleState(
      serverState({
        bossState: { bossId: 'boss-hoa-long', hp: 1250, maxHp: 5000 },
        petState: {
          hp: 640,
          maxHp: 1000,
          power: 0,
          passiveId: 'xich-lang',
          passiveProgress: { threshold: 5, current: 0 },
          equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          statusEffects: [],
        },
      })
    );

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('1250 / 5000');
    expect(rendered).toContain('640 / 1000');

    // 1250 / 5000 of a 250 px track = 62.5 → 63; 640 / 1000 of a 200 px track = 128.
    const boss = field<GaugeView>(ctx, 'bossHpGauge');
    const pet = field<GaugeView>(ctx, 'petHpGauge');
    expect(boss.fill.width).toBe(63);
    expect(pet.fill.width).toBe(128);
    // The fill is anchored at the track's own left edge, so it grows rightwards.
    expect(boss.fill.x).toBe(boss.x + boss.fill.width / 2);
    expect(pet.fill.x).toBe(pet.x + pet.fill.width / 2);
  });

  it('renders a zero-HP gauge as an empty track, with the delivered zero still on screen', () => {
    // TASK-210 §1's zero-HP case: the gauge draws nothing, and the authoritative
    // `0` is still rendered as the number the server sent. A lost battle's
    // terminal HP is a value, not an absence (`SIGNALR_PROTOCOL.md` §4.4 item 4).
    const { harness, scene, ctx } = createBattle(
      INITIAL_RUNTIME_STATE,
      true,
      serverState({
        bossState: { bossId: 'boss-hoa-long', hp: 0, maxHp: 5000 },
        petState: {
          hp: 0,
          maxHp: 1000,
          power: 0,
          passiveId: 'xich-lang',
          passiveProgress: { threshold: 5, current: 0 },
          equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          statusEffects: [],
        },
      })
    );

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('0 / 5000');
    expect(rendered).toContain('0 / 1000');

    const boss = field<GaugeView>(ctx, 'bossHpGauge');
    const pet = field<GaugeView>(ctx, 'petHpGauge');
    expect(boss.fill.width).toBe(0);
    expect(pet.fill.width).toBe(0);
    expect(boss.fill.visible).toBe(false);
    expect(pet.fill.visible).toBe(false);
  });

  it('draws no proportion and mutates no value for a non-positive or unexpected delivered max', () => {
    // Defensive case (`TASK-210` §1 "handle unexpected values defensively without
    // mutating authoritative state"): `max = 0` has no proportion to draw, so the
    // track is left empty instead of producing `Infinity`/`NaN` geometry, and the
    // numbers are still printed verbatim. A `hp` above `maxHp` fills the track
    // without the payload or the readout being clamped.
    const { harness, scene, ctx } = createBattle(
      INITIAL_RUNTIME_STATE,
      true,
      serverState({ bossState: { bossId: 'boss-hoa-long', hp: 25, maxHp: 0 } })
    );

    runScene(scene, ctx, 'create');

    expect(harness.texts.map((t) => t.text).join('\n')).toContain('25 / 0');
    expect(field<GaugeView>(ctx, 'bossHpGauge').fill.width).toBe(0);
    expect(field<GaugeView>(ctx, 'bossHpGauge').fill.visible).toBe(false);

    harness.setBattleState(
      serverState({ bossState: { bossId: 'boss-hoa-long', hp: 6000, maxHp: 5000 } })
    );

    // The readout keeps both delivered numbers exactly as sent; only the track's
    // visual proportion is capped at full.
    expect(harness.texts.map((t) => t.text).join('\n')).toContain('6000 / 5000');
    const boss = field<GaugeView>(ctx, 'bossHpGauge');
    expect(boss.fill.width).toBe(boss.width);
  });

  it('holds no second HP value: the gauge is the only thing derived from the pair', () => {
    // TASK-210 §12: presentation calculations are allowed only when they are purely
    // visual. The scene keeps no `bossHp` / `petHp` field of its own, so no second
    // authoritative HP value exists to drift from the delivered one.
    const { scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    for (const invented of ['bossHp', 'petHp', 'currentHp', 'playerHp', 'remainingHp']) {
      expect(Object.keys(ctx), `the scene must not hold a "${invented}" field`).not.toContain(
        invented
      );
    }
  });

  it('draws the Power gauge at 0, mid-range and full against the documented range', () => {
    // TASK-210 §2: Power is presented from the authoritative `petState.power`
    // against the documented `0–100` invariant of `GAME_RULES.md` §12. No
    // `maxPower` member is read or invented, and no cost or affordability is
    // derived from the value.
    const cases = [
      { power: 0, expected: 0, visible: false },
      { power: 45, expected: 63, visible: true },
      { power: 100, expected: 140, visible: true },
    ];

    for (const scenario of cases) {
      const { harness, scene, ctx } = createBattle(
        INITIAL_RUNTIME_STATE,
        true,
        petPresentation({ power: scenario.power })
      );

      runScene(scene, ctx, 'create');

      expect(harness.texts.map((t) => t.text).join('\n')).toContain(
        `${scenario.power} / 100`
      );

      const gauge = field<GaugeView>(ctx, 'powerGauge');
      expect(gauge.fill.width, `power ${scenario.power}`).toBe(scenario.expected);
      expect(gauge.fill.visible, `power ${scenario.power}`).toBe(scenario.visible);
    }
  });

  it('invents no maxPower: the Power scale is the documented invariant, not a member', () => {
    // TASK-210 §2's explicit boundary: `0–100` is the documented range of
    // `GAME_RULES.md` §12 / `COMBAT_RULES.md` §1.1, not state. No `maxPower` member
    // exists on the wire and none is introduced here (`SIGNALR_PROTOCOL.md` §4.3
    // item 15). Comments are stripped, so the prose that *names* the absent member
    // while forbidding it is not the violation — only real code is.
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    expect(source).not.toContain('maxPower');
    expect(source).not.toContain('MaxPower');
  });

  it('clears and updates the delivered Combo on the push that replaces it', () => {
    // TASK-210 §4: the Combo line follows the delivered value, so a later push that
    // publishes an ordinary single-Match Swap clears the previous Swap's `COMBO ×N`
    // rather than leaving a stale one on screen. Nothing is aged out on a timer:
    // `MATCH3_RULES.md` §6.5 items 1–2 make a rejected Swap leave the previous
    // value standing, so a client-side expiry would disagree with the state.
    const { harness, scene, ctx } = createBattle(
      INITIAL_RUNTIME_STATE,
      true,
      serverState({ playerState: { combo: 3, matchCount: 4 } })
    );

    runScene(scene, ctx, 'create');
    expect(harness.texts.map((t) => t.text).join('\n')).toContain('COMBO ×3');

    harness.setBattleState(serverState({ playerState: { combo: 1, matchCount: 7 } }));
    let rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('COMBO');
    expect(rendered).toContain('MATCHES 7');

    harness.setBattleState(serverState({ playerState: { combo: 7, matchCount: 11 } }));
    rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('COMBO ×7');
    expect(rendered).not.toContain('COMBO ×3');
  });

  it('shows whether the delivered Passive has progress, without evaluating it', () => {
    // TASK-210 §5: the player can see which Passive is active and whether any
    // progress exists. The gauge is the delivered pair drawn as a proportion; the
    // charge, threshold, trigger, overflow and reset rules stay the server's, and
    // the client holds no Passive definition to name it with, so the delivered
    // identity is printed as itself.
    const idle = createBattle(
      INITIAL_RUNTIME_STATE,
      true,
      petPresentation({ passiveProgress: { threshold: 5, current: 0 } })
    );
    runScene(idle.scene, idle.ctx, 'create');

    expect(idle.harness.texts.map((t) => t.text).join('\n')).toContain('Passive: xich-lang');
    expect(idle.harness.texts.map((t) => t.text).join('\n')).toContain(
      '0 / 5 Matches (reset: Default)'
    );
    expect(field<GaugeView>(idle.ctx, 'passiveGauge').fill.width).toBe(0);
    expect(field<GaugeView>(idle.ctx, 'passiveGauge').fill.visible).toBe(false);

    const inProgress = createBattle(
      INITIAL_RUNTIME_STATE,
      true,
      petPresentation({
        passiveId: 'thanh-xa-poison',
        passiveProgress: { threshold: 7, current: 3 },
        passiveResetOverride: 'Partial',
      })
    );
    runScene(inProgress.scene, inProgress.ctx, 'create');

    const rendered = inProgress.harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Passive: thanh-xa-poison');
    expect(rendered).toContain('3 / 7 Matches (reset: Partial)');
    // 3 / 7 of a 120 px track = 51.43 → 51.
    expect(field<GaugeView>(inProgress.ctx, 'passiveGauge').fill.width).toBe(51);
    expect(field<GaugeView>(inProgress.ctx, 'passiveGauge').fill.visible).toBe(true);
  });

  it('draws one damage floater per damage instance, over the party that took it', () => {
    // TASK-210 §3. `DamageDealt` and `DamageTaken` carry identical payloads for one
    // instance (`SIGNALR_PROTOCOL.md` §3.2.15 item 1), so the pair must produce ONE
    // floater, and the side is the delivered `target` — never the event's own
    // discriminator, which would draw every hit on the same side.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());
    runScene(scene, ctx, 'create');

    // TASK-273: the delivered state the batch belongs to, pushed in the
    // documented order (`SIGNALR_PROTOCOL.md` §3.1, §6 item 4) before the
    // baseline below is read, so the baseline covers the HUD the push presents
    // and the assertion covers only what the batch adds.
    harness.setBattleState(serverState({ battleId: 'b-210', sequence: 2 }));

    const beforePlayerHit = harness.texts.length;
    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 2,
      events: [
        { type: 'DamageCalculated', base: 137, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 120 },
        { type: 'DamageDealt', source: 'player', target: 'boss', amount: 120 },
        { type: 'DamageTaken', source: 'player', target: 'boss', amount: 120 },
      ],
    });

    const bossHit = harness.texts.slice(beforePlayerHit);
    expect(bossHit.map((t) => t.text)).toEqual(['-120']);
    // The Boss anchor: the centre of the Boss HP gauge, on the row below it —
    // above the board's own top edge (BOARD_ORIGIN_Y = 120).
    expect(bossHit[0].x).toBe(161);
    expect(bossHit[0].y).toBe(108);

    harness.setBattleState(serverState({ battleId: 'b-210', sequence: 3 }));
    const beforePetHit = harness.texts.length;
    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 3,
      events: [
        { type: 'DamageCalculated', base: 80, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 80 },
        { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
        { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
      ],
    });

    const petHit = harness.texts.slice(beforePetHit);
    expect(petHit.map((t) => t.text)).toEqual(['-80']);
    // The Pet anchor: the centre of the Pet HP gauge, beside the board.
    expect(petHit[0].x).toBe(136);
    expect(petHit[0].y).toBe(208);
  });

  it('draws a DamageTaken with no matching DamageDealt of its own', () => {
    // The pair-collapse must not swallow a report that arrives on its own: a
    // `DamageTaken` with no preceding identical `DamageDealt` is its own instance
    // and is drawn.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());
    runScene(scene, ctx, 'create');

    harness.setBattleState(serverState({ battleId: 'b-210', sequence: 4 }));

    const before = harness.texts.length;
    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 4,
      events: [{ type: 'DamageTaken', source: 'player', target: 'boss', amount: 42 }],
    });

    expect(harness.texts.slice(before).map((t) => t.text)).toEqual(['-42']);
  });

  it('draws no floater for a damage party the contract does not define', () => {
    // The two MVP party identifiers are `"player"` and `"boss"`
    // (`SIGNALR_PROTOCOL.md` §3.2.14 item 3). An unrecognized one gets no anchor:
    // the scene does not guess which panel someone was hurt on.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());
    runScene(scene, ctx, 'create');

    harness.setBattleState(serverState({ battleId: 'b-210', sequence: 5 }));

    const before = harness.texts.length;
    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 5,
      events: [{ type: 'DamageDealt', source: 'player', target: 'third-party', amount: 10 }],
    });

    expect(harness.texts.slice(before)).toHaveLength(0);
  });

  it('draws the delivered Power movement as a signed floater over the Power gauge', () => {
    // TASK-210 §3's resource change: `PowerChanged.delta` is signed and is shown as
    // delivered; the resulting `power` is never recomputed from it.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());
    runScene(scene, ctx, 'create');

    harness.setBattleState(serverState({ battleId: 'b-210', sequence: 6 }));

    const beforeGain = harness.texts.length;
    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 6,
      events: [{ type: 'PowerChanged', delta: 25, power: 25, source: 'match' }],
    });

    const gain = harness.texts.slice(beforeGain);
    expect(gain.map((t) => t.text)).toEqual(['+25']);
    expect(gain[0].x).toBe(166);
    expect(gain[0].y).toBe(213);

    harness.setBattleState(serverState({ battleId: 'b-210', sequence: 7 }));
    const beforeLoss = harness.texts.length;
    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 7,
      events: [{ type: 'PowerChanged', delta: -30, power: 12, source: 'boss' }],
    });

    const loss = harness.texts.slice(beforeLoss);
    expect(loss.map((t) => t.text)).toEqual(['-30']);
    expect(loss[0].x).toBe(166);
  });

  it('presents one selective callout per delivered batch, not one per event', () => {
    // TASK-210 §7: the player is told the most important thing the resolution
    // produced. A Combo outranks the Match that carried it, a Boss Skill outranks
    // the Power the same boss drained, and an event with nothing to say to a player
    // (`GemMatched`, `CascadeCreated`, `DamageCalculated`) produces no line at all.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());
    runScene(scene, ctx, 'create');

    const callout = () => field<{ text: string; color?: string }>(ctx, 'calloutText');

    expect(callout().text, 'no events yet').toBe('');

    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 8,
      events: [
        { type: 'GemMatched', cellIndex: 8, gemType: 'ATK' },
        { type: 'CascadeCreated', cascadeDepth: 1 },
      ],
    });
    expect(callout().text, 'nothing a player needs told').toBe('');

    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 9,
      events: [
        { type: 'MatchCreated', shape: 'Straight', cells: [8, 9, 10], gemType: 'ATK', cascadeDepth: 0 },
        { type: 'ComboChanged', combo: 1 },
      ],
    });
    expect(callout().text, 'an ordinary single-Match Swap').toBe('MATCH');

    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 10,
      events: [
        { type: 'MatchCreated', shape: 'Straight', cells: [8, 9, 10], gemType: 'ATK', cascadeDepth: 0 },
        { type: 'ComboChanged', combo: 4 },
        { type: 'PowerChanged', delta: 10, power: 10, source: 'match' },
      ],
    });
    expect(callout().text, 'the Combo outranks the Match and the Power').toBe('COMBO ×4');
    expect(callout().color).toBe('#fbbf24');

    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 11,
      events: [
        { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
        { type: 'PowerChanged', delta: -20, power: 5, source: 'boss' },
      ],
    });
    expect(callout().text, 'the Boss acting outranks the Power it drained').toBe('BOSS SKILL: Flame Burst');
    expect(callout().color).toBe('#f87171');
  });

  it('names a cast callout from the delivered reads, or the delivered identity', async () => {
    // TASK-210 §7's `CARD CAST` / `PET SKILL`: the callout uses the same
    // presentation lookup the cast controls use. A Basic Card is named from the
    // delivered unlocked Card collection (`API_CONTRACTS.md` §5.3); the derived
    // Signature Skill is named from the delivered Pet read (§5.1), because it is
    // not an unlock row and can never be in that collection
    // (`SIGNALR_PROTOCOL.md` §4.3 item 13). With no delivered name the identity
    // itself is shown — never a guessed name (`CARD_RULES.md` §3).
    const harness = createSceneHarness({
      battleState: serverState(),
      pets: provisionedPets(),
    });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');

    runScene(scene, ctx, 'create');
    // The collection reads are real async port calls; one flush settles them, the
    // same way the cast-control tests wait for them.
    await flush();

    const callout = () => field<{ text: string }>(ctx, 'calloutText');

    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 12,
      events: [{ type: 'CardCast', cardId: 'card-heal' }],
    });
    expect(callout().text).toBe('CARD: Heal');

    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 13,
      events: [{ type: 'PetSkillCast', cardId: 'card-inferno' }],
    });
    expect(callout().text).toBe('PET SKILL: Inferno');

    // An identity with no delivered name is shown as the identity itself.
    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 14,
      events: [{ type: 'CardCast', cardId: 'card-not-in-collection' }],
    });
    expect(callout().text).toBe('CARD: card-not-in-collection');
  });

  it('draws no heal feedback, because the contract delivers no heal amount', () => {
    // TASK-210 §3 / §14 item 8: healing emits NO event at all
    // (`ResourceGenerator.ApplyHeal`: "No event. Healing is a state-only change"),
    // and no event carries a resulting HP, so there is nothing to show and nothing
    // to derive. This pins that the scene does not turn a Passive trigger — the
    // shape a real heal would arrive in — into an invented `+N`.
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());
    runScene(scene, ctx, 'create');

    harness.setBattleState(serverState({ battleId: 'b-210', sequence: 14 }));

    const before = harness.texts.length;
    harness.emitBattleEvents({
      battleId: 'b-210',
      serverSequence: 14,
      events: [
        {
          type: 'PassiveTriggered',
          passiveId: 'pet-phoenix-rebirth',
          source: 'pet',
          sourceId: 'pet-instance-1',
          progress: 5,
          threshold: 5,
        },
      ],
    });

    // Only the callout statement is added; no floating number claims a healing
    // amount the server never sent.
    expect(harness.texts.slice(before)).toHaveLength(0);
    expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('PASSIVE TRIGGERED');
  });

  it('keeps every growing HUD line inside its own column and band', () => {
    // TASK-210 §10: the layout is deterministic. Every HUD line that can grow is
    // created with a word-wrap width and a `maxLines` cap, and its declared left
    // edge plus that width stays clear of the board's own left edge (395). The
    // callout sits on the row above the board and the cast line on the row below
    // it, so neither can cover a cell. (The browser E2E measures the same property
    // on the rendered bounds.)
    const { scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());

    runScene(scene, ctx, 'create');

    const BOARD_LEFT = 395;
    const BOARD_TOP = 120;
    const BOARD_BOTTOM = 610;

    interface StyledLine {
      readonly x: number;
      readonly y: number;
      readonly style?: { wordWrap?: { width: number }; maxLines?: number };
    }

    for (const name of ['bossNameText', 'petNameText', 'petStatusText', 'swapText']) {
      const line = field<StyledLine>(ctx, name);
      const wrap = line.style?.wordWrap?.width ?? 0;

      expect(wrap, `${name} must declare a word-wrap width`).toBeGreaterThan(0);
      expect(line.x + wrap, `${name} must stay inside the HUD column`).toBeLessThanOrEqual(
        BOARD_LEFT
      );
      expect(line.style?.maxLines ?? 0, `${name} must be line-capped`).toBeGreaterThan(0);
    }

    expect(field<StyledLine>(ctx, 'calloutText').y).toBeLessThan(BOARD_TOP);
    expect(field<StyledLine>(ctx, 'boardMessageText').y).toBeLessThan(BOARD_TOP);
    expect(field<StyledLine>(ctx, 'castText').y).toBeGreaterThan(BOARD_BOTTOM);
  });

  it('presents canonical display names for all five MVP Boss Skills in callout feed (TASK-231)', () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());
    runScene(scene, ctx, 'create');

    const callout = () => field<{ text: string; color?: string }>(ctx, 'calloutText');

    const canonicalSkills = [
      { skillId: 'flame-burst', bossId: 'boss-hoa-long', expected: 'BOSS SKILL: Flame Burst' },
      { skillId: 'drain-power', bossId: 'boss-thuy-ma', expected: 'BOSS SKILL: Drain Power' },
      { skillId: 'root', bossId: 'boss-moc-yeu', expected: 'BOSS SKILL: Root' },
      { skillId: 'earthquake', bossId: 'boss-son-thach-ve', expected: 'BOSS SKILL: Earthquake' },
      { skillId: 'thunder-strike', bossId: 'boss-kim-loi-vuong', expected: 'BOSS SKILL: Thunder Strike' },
    ];

    for (let i = 0; i < canonicalSkills.length; i++) {
      const { skillId, bossId, expected } = canonicalSkills[i];
      harness.emitBattleEvents({
        battleId: 'b-231',
        serverSequence: 20 + i,
        events: [{ type: 'BossSkillCast', skillId, sourceId: bossId }],
      });
      expect(callout().text).toBe(expected);
      expect(callout().color).toBe('#f87171');
    }

    // Unrecognized skill falls back safely to 'BOSS SKILL'
    harness.emitBattleEvents({
      battleId: 'b-231',
      serverSequence: 30,
      events: [{ type: 'BossSkillCast', skillId: 'unknown-cataclysm', sourceId: 'boss-hoa-long' }],
    });
    expect(callout().text).toBe('BOSS SKILL');
    expect(callout().color).toBe('#f87171');
  });

  it('presents Boss Enrage transition in HUD and callout feed without duplicate callouts on subsequent updates (TASK-231)', () => {
    // Initial state: Hỏa Long at full HP 5000/5000 (Enrage threshold: 1500)
    const { harness, scene, ctx } = createBattle(
      INITIAL_RUNTIME_STATE,
      true,
      serverState({ bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 } })
    );
    runScene(scene, ctx, 'create');

    const bossName = () => field<{ text: string }>(ctx, 'bossNameText');
    const callout = () => field<{ text: string; color?: string }>(ctx, 'calloutText');

    expect(bossName().text).toBe('Hỏa Long');
    expect(callout().text).toBe('');

    // State update 1: HP drops to 1500 (boundary test: strict <, not enraged yet)
    harness.setBattleState(
      serverState({ bossState: { bossId: 'boss-hoa-long', hp: 1500, maxHp: 5000 } })
    );
    expect(bossName().text).toBe('Hỏa Long');
    expect(callout().text).toBe('');

    // State update 2: HP drops to 1499 (< 1500, Enrage transition triggers!)
    harness.setBattleState(
      serverState({ bossState: { bossId: 'boss-hoa-long', hp: 1499, maxHp: 5000 } })
    );
    expect(bossName().text).toBe('Hỏa Long [ENRAGED]');
    expect(callout().text).toBe('BOSS ENRAGED');
    expect(callout().color).toBe('#f87171');

    // Emit a different event to change callout text. It is delivered at the
    // state the scene already holds — the push that precedes a batch
    // (`SIGNALR_PROTOCOL.md` §3.1, §6 item 4) is the one above, and a further
    // battle-identity change here would be a new battle rather than an update.
    harness.emitBattleEvents({
      battleId: 'battle-1',
      serverSequence: 0,
      events: [
        { type: 'MatchCreated', shape: 'Straight', cells: [0, 1, 2], gemType: 'ATK', cascadeDepth: 0 },
      ],
    });
    expect(callout().text).toBe('MATCH');

    // State update 3: HP drops further to 1200 while already enraged
    harness.setBattleState(
      serverState({ bossState: { bossId: 'boss-hoa-long', hp: 1200, maxHp: 5000 } })
    );
    expect(bossName().text).toBe('Hỏa Long [ENRAGED]');
    // Callout remains 'MATCH', NOT overwritten with redundant 'BOSS ENRAGED'
    expect(callout().text).toBe('MATCH');
  });

  it('presents player-friendly callout messages for Swap and CardCast rejections (TASK-231)', async () => {
    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState());
    runScene(scene, ctx, 'create');
    await flush();

    const callout = () => field<{ text: string; color?: string }>(ctx, 'calloutText');
    const swapText = () => field<{ text: string }>(ctx, 'swapText');
    const castText = () => field<{ text: string }>(ctx, 'castText');

    // 1. Swap rejection: NO_MATCH_FROM_SWAP
    harness.setActionResult({ accepted: false, reason: 'NO_MATCH_FROM_SWAP' });
    tapCell(harness, 0);
    tapCell(harness, 1);
    await flush();

    expect(callout().text).toBe('Swap does not create a match.');
    expect(callout().color).toBe('#fbbf24');
    expect(swapText().text).toContain('rejected (NO_MATCH_FROM_SWAP).');

    // 2. CardCast rejection: INSUFFICIENT_POWER
    harness.setActionResult({ accepted: false, reason: 'INSUFFICIENT_POWER' });
    harness.clickOption('Card: Shield');
    await flush();

    expect(callout().text).toBe('Not enough Power.');
    expect(callout().color).toBe('#fbbf24');
    expect(castText().text).toContain('rejected (INSUFFICIENT_POWER).');

    // 3. CardCast rejection: CARD_CAST_ALREADY_USED_THIS_TURN
    harness.setActionResult({ accepted: false, reason: 'CARD_CAST_ALREADY_USED_THIS_TURN' });
    harness.clickOption('Card: Shield');
    await flush();

    expect(callout().text).toBe('Only one card can be cast per turn.');
    expect(callout().color).toBe('#fbbf24');
    expect(castText().text).toContain('rejected (CARD_CAST_ALREADY_USED_THIS_TURN).');

    // 4. Unknown rejection code fallback
    harness.setActionResult({ accepted: false, reason: 'MYSTERIOUS_ERROR' });
    tapCell(harness, 2);
    tapCell(harness, 3);
    await flush();

    expect(callout().text).toBe('Swap rejected (MYSTERIOUS_ERROR).');
    expect(callout().color).toBe('#fbbf24');
  });

  it('renders every cell as an ordinary Gem type with no specialised cell state (TASK-293)', () => {
    // MATCH3_RULES.md §1.1 / GAME_STATE.md §2.1.2–§2.1.4: a cell holds one
    // functional Gem type and nothing else, so every cell is drawn with the same
    // tile treatment and its own type label. The board is exactly 128 display
    // objects (64 tiles + 64 labels) — no per-cell overlay, badge, or second
    // styling exists to draw.
    const cells = serverBoard();
    cells[0] = { gemType: 'ATK' };
    cells[1] = { gemType: 'DEF' };
    cells[2] = { gemType: 'HP' };
    cells[3] = { gemType: 'POWER' };

    const { harness, scene, ctx } = createBattle(INITIAL_RUNTIME_STATE, true, serverState({
      board: { cells },
    }));

    runScene(scene, ctx, 'create');

    // Child-count invariant: exactly 128 children (64 tiles + 64 labels).
    expect(harness.boardCells).toHaveLength(128);

    const tiles = harness.boardCells.filter((c) => c.kind === 'tile');
    const labels = harness.boardCells.filter((c) => c.kind === 'label');
    expect(tiles).toHaveLength(64);
    expect(labels).toHaveLength(64);

    // One uniform tile treatment for every cell.
    expect(tiles.every((tile) => tile.strokeWidth === 1)).toBe(true);
    expect(tiles.every((tile) => tile.strokeColor === 0x0b0f19)).toBe(true);

    // Each label is exactly its own Gem type, undecorated.
    expect(labels[0].label).toBe('ATK');
    expect(labels[1].label).toBe('DEF');
    expect(labels[2].label).toBe('HP');
    expect(labels[3].label).toBe('PWR');

    // The whole board's labels are the four documented types and nothing else:
    // no label carries a suffix another vocabulary appended.
    const width = new Set(labels.map((l) => l.label!.split(' ').length));
    expect(width).toEqual(new Set([1]));
    expect(new Set(labels.map((l) => l.label))).toEqual(new Set(['ATK', 'DEF', 'HP', 'PWR']));
  });

  it('latches boss Enrage state across healing and defeat, resetting only on new battle or state clear (TASK-232)', async () => {
    const { harness, scene, ctx } = createBattle(
      INITIAL_RUNTIME_STATE,
      true,
      serverState({ battleId: 'battle-alpha', bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 } })
    );

    runScene(scene, ctx, 'create');

    const bossName = () => field<{ text: string }>(ctx, 'bossNameText');
    const callout = () => field<{ text: string; color?: string }>(ctx, 'calloutText');

    expect(bossName().text).toBe('Hỏa Long');
    expect(callout().text).toBe('');

    // Boss HP drops below threshold: 1400 < 1500 -> Enrage latches and callout fires
    harness.setBattleState(
      serverState({
        battleId: 'battle-alpha',
        bossState: { bossId: 'boss-hoa-long', hp: 1400, maxHp: 5000 },
      })
    );
    expect(bossName().text).toBe('Hỏa Long [ENRAGED]');
    expect(callout().text).toBe('BOSS ENRAGED');

    // Emit another event so callout changes away from 'BOSS ENRAGED'
    harness.emitBattleEvents({
      battleId: 'battle-alpha',
      serverSequence: 50,
      events: [
        { type: 'MatchCreated', shape: 'Straight', cells: [0, 1, 2], gemType: 'DEF', cascadeDepth: 0 },
      ],
    });
    expect(callout().text).toBe('MATCH');

    // Boss heals: HP rises above threshold (2500 > 1500) -> stays latched!
    harness.setBattleState(
      serverState({
        battleId: 'battle-alpha',
        bossState: { bossId: 'boss-hoa-long', hp: 2500, maxHp: 5000 },
      })
    );
    expect(bossName().text).toBe('Hỏa Long [ENRAGED]');
    // Callout does NOT re-emit 'BOSS ENRAGED'
    expect(callout().text).toBe('MATCH');

    // Boss is defeated: HP drops to 0 -> stays latched!
    harness.setBattleState(
      serverState({
        battleId: 'battle-alpha',
        bossState: { bossId: 'boss-hoa-long', hp: 0, maxHp: 5000 },
      })
    );
    expect(bossName().text).toBe('Hỏa Long [ENRAGED]');

    // Clear battle state -> unlatches
    harness.setBattleState(null);
    expect(bossName().text).toBe('');

    // Transition to a genuinely new battle (different battleId) -> unlatched
    harness.setBattleState(
      serverState({
        battleId: 'battle-beta',
        bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
      })
    );
    expect(bossName().text).toBe('Hỏa Long');
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
        // The delivered `CellPayload` shape: each cell is its Gem type and nothing
        // else (`GAME_STATE.md` §2.1.3 items 1–4, `SIGNALR_PROTOCOL.md` §4.1 item 5).
        cells: Array.from({ length: 64 }, (_, index) => ({
          gemType: GEM_NAMES[index % GEM_NAMES.length],
        })),
      },
      playerState: { combo: 0, matchCount: 0 },
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
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

    // TASK-210 stage advance. `'damage'` was in this list while the client had no
    // damage presentation at all. TASK-210 §3 requires the delivered
    // `DamageDealt`/`DamageTaken` events to be *presented* — a floating number over
    // the panel of the party that took the hit — so the lowercase word is now part
    // of legitimate presentation code (`spawnDamageFloater`, `damageAnchor`,
    // `previousDamage`, the floater colours). What stays forbidden is what always
    // was: resolving damage. The dedicated assertion below pins that property
    // directly — no arithmetic may touch a delivered HP value or the delivered
    // `amount`, so no HP can be damaged, healed, or reconstructed here
    // (`SIGNALR_PROTOCOL.md` §4.3 item 15, §4.4 item 7, `GAME_RULES.md` §18).
    for (const forbidden of [
      'SignalRService',
      'HubConnection',
      '@microsoft/signalr',
      'findMatch',
      'hasMatch',
      'cascade =',
      'applyGravity',
      'DamagePipeline',
      'ApplyDamage',
      'applyDamage',
      'CalculateDamage',
      'calculateDamage',
      'damageResult',
      'DamageResult',
      'combo++',
    ]) {
      expect(source, `BattleScene must not reference "${forbidden}"`).not.toContain(forbidden);
    }

    // The scene never computes with a delivered combat value: `hp`, `maxHp` and
    // `power` reach the HUD as text, and a damage `amount` is formatted into a
    // floater. None of them is an operand of anything.
    expect(source, 'BattleScene must not compute with the delivered HP').not.toMatch(
      /\.(hp|maxHp|power)\s*(\+\+|--|\+=|-=|\*=|\/=)/
    );
    expect(source, 'BattleScene must not compute with the delivered damage amount').not.toMatch(
      /\.amount\s*(\+\+|--|\+=|-=|\*=|\/=)/
    );

    // It submits through the runtime port and never invents an action name.
    expect(source).toContain('requestAction');
    expect(source).toContain('RUNTIME_ACTION_SWAP');
    expect(source).toContain('RUNTIME_ACTION_CARD_CAST');
    expect(source).toContain('RUNTIME_ACTION_PET_SKILL_CAST');
    expect(source).not.toContain("'GetBattleState'");
  });
});

describe('BattleScene — cascade presentation timeline (TASK-245, GAME_EVENTS.md §1)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];
  const GEM_LABELS: Readonly<Record<string, string>> = {
    ATK: 'ATK',
    DEF: 'DEF',
    HP: 'HP',
    POWER: 'PWR',
  };

  /** One HUD gauge, read the way the scene's own `HudGauge` exposes it. */
  interface GaugeView {
    readonly width: number;
    readonly height: number;
    readonly fill: {
      readonly x: number;
      readonly y: number;
      readonly width: number;
      readonly height: number;
      readonly visible: boolean;
    };
  }

  /** A scene-owned presentation field, read the way the scene wrote it. */
  function field<T>(ctx: object, name: string): T {
    return (ctx as Record<string, T>)[name];
  }

  /**
   * The scene's own input guard, evaluated against the context that owns its
   * state.
   *
   * The harness runs a scene through a `this` context (the way Phaser's own
   * Scene Systems do), so a scene method has to be invoked the same way to see
   * what the scene actually wrote.
   */
  function inputLocked(scene: BattleScene, ctx: object): boolean {
    return (
      Object.getPrototypeOf(scene) as { isInputLocked(this: object): boolean }
    ).isInputLocked.call(ctx);
  }

  /** The presentation phases the scene has played, in order. */
  function playedPhases(ctx: object): string[] {
    return [...field<string[]>(ctx, 'presentedPhases')];
  }

  /**
   * A delivered 8 x 8 board, rotated by `shift` cells so a resolution's board is
   * provably different from the one already on screen.
   */
  function deliveredBoard(shift = 0): RuntimeBoard {
    return {
      cells: Array.from({ length: 64 }, (_, index) => ({
        gemType: GEM_NAMES[(index + shift) % GEM_NAMES.length],
      })),
    };
  }

  /** The labels the delivered board must be rendered as, in §1.0 index order. */
  function expectedLabels(shift = 0): string[] {
    return Array.from({ length: 64 }, (_, index) =>
      GEM_LABELS[GEM_NAMES[(index + shift) % GEM_NAMES.length]]
    );
  }

  /** The §1.0 cell index a board-local point sits on, or `null` outside the board. */
  function cellIndexOf(x: number, y: number): number | null {
    const column = Math.round((x - BOARD_ORIGIN_X - CELL_SIZE / 2) / BOARD_PITCH);
    const row = Math.round((y - BOARD_ORIGIN_Y - CELL_SIZE / 2) / BOARD_PITCH);

    if (row < 0 || row >= BOARD_COLUMNS || column < 0 || column >= BOARD_COLUMNS) {
      return null;
    }

    return row * BOARD_COLUMNS + column;
  }

  /**
   * The label rendered at each §1.0 cell, read from the view's own position.
   *
   * The board container's child order is the order the views were created in and
   * never changes, while a committed Swap deliberately exchanges two views'
   * cells — so the cell a label *shows* is the cell it sits on, not its slot in
   * the container.
   */
  function labelsByCell(harness: ReturnType<typeof createSceneHarness>): string[] {
    const labels: string[] = new Array(BOARD_COLUMNS * 8).fill('');

    for (const cell of harness.boardCells) {
      if (cell.kind !== 'label') {
        continue;
      }

      const view = cell as unknown as { x: number; y: number; label?: string };
      const index = cellIndexOf(view.x, view.y);

      if (index !== null) {
        labels[index] = view.label ?? '';
      }
    }

    return labels;
  }

  function battleState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return {
      battleId: 'battle-245',
      turn: 1,
      sequence: 0,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: deliveredBoard(),
      playerState: { combo: 0, matchCount: 0 },
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
      ...overrides,
    };
  }

  /**
   * A battle scene with a recorded, test-driven tween queue.
   *
   * The queue is what makes the timeline observable as a *sequence*: a phase's
   * pacing tween is the only thing that advances it, so `flushTweens(1)` plays
   * exactly one step.
   */
  function createTimelineBattle(state: RuntimeBattleState | null = battleState()) {
    const harness = createSceneHarness({ battleState: state, withTweens: true });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    runScene(scene, ctx, 'create');
    if (harness.pendingTweenCount() > 0) {
      harness.flushTweens(harness.pendingTweenCount());
    }
    return { harness, scene, ctx };
  }

  /**
   * Submits one Swap whose acknowledgement is held open, which is the state a
   * real request is in while its resolution's state push and event batch arrive
   * (`SIGNALR_PROTOCOL.md` §3.1). The returned function settles it.
   */
  function submitHeldSwap(
    harness: ReturnType<typeof createSceneHarness>,
    from = 20,
    to = 21
  ): () => void {
    let settle: (() => void) | null = null;

    harness.setActionBehaviour(
      () =>
        new Promise<never>((resolve) => {
          settle = () => resolve({ accepted: true, reason: null } as never);
        })
    );

    tapCell(harness, from);
    tapCell(harness, to);

    return () => {
      // The request's own acknowledgement, and then an ordinary runtime again —
      // so a later gesture is recorded the way a real one is.
      harness.setActionBehaviour(null);
      settle?.();
    };
  }

  /**
   * Delivers one resolution exactly as the server does: the state push first,
   * then the atomic event batch keyed by the post-resolution `serverSequence`
   * (`SIGNALR_PROTOCOL.md` §3.1, `BattleHub.Swap`).
   */
  function deliverResolution(
    harness: ReturnType<typeof createSceneHarness>,
    next: RuntimeBattleState,
    events: unknown[]
  ): void {
    harness.setBattleState(next);
    harness.emitBattleEvents({
      battleId: next.battleId,
      serverSequence: next.sequence,
      events,
    });
  }

  /** A Swap that resolves through one Match and two Cascade passes. */
  const CASCADE_BATCH: unknown[] = [
    { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
    { type: 'GemMatched', cellIndex: 25, gemType: 'ATK' },
    { type: 'CascadeCreated', cascadeDepth: 1 },
    { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'DEF', cascadeDepth: 2 },
    { type: 'GemMatched', cellIndex: 32, gemType: 'DEF' },
    { type: 'CascadeCreated', cascadeDepth: 2 },
    { type: 'MatchCreated', shape: 'Lt', cells: [40, 41, 42, 48], gemType: 'HP', cascadeDepth: 3 },
    { type: 'GemMatched', cellIndex: 48, gemType: 'HP' },
    { type: 'ComboChanged', combo: 3 },
    { type: 'PowerChanged', delta: 10, power: 10, source: 'match' },
    { type: 'DamageCalculated', base: 100, comboModifier: 1.5, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 150 },
    { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
    { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
    { type: 'PassiveCharged', passiveId: 'boss-hoa-long-rage', source: 'boss', sourceId: 'boss-hoa-long', progress: 1, threshold: 5 },
    { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
    { type: 'DamageCalculated', base: 80, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 80 },
    { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
    { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
  ];

  const CASCADE_PHASES = [
    'swap 20->21',
    'match depth=1',
    'cascade depth=1',
    'cascade depth=2',
    'settle',
    'damage player->boss',
    // TASK-252: the Boss Response's own delivered events reach their phases
    // through the same adapter — the Boss Skill cast (GAME_EVENTS.md §1,
    // GAME_RULES.md §17 step 18b) is its own step, and the Boss Passive charge
    // that precedes it is carried by the damage phase it was delivered in.
    'boss-skill flame-burst',
    'retaliation boss->player',
  ];

  it('plays one ordered phase per delivered pass, in GAME_EVENTS.md §1 order', () => {
    const { harness, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    deliverResolution(harness, battleState({ sequence: 1 }), CASCADE_BATCH);

    // Played one phase at a time: every intermediate state is a prefix of the
    // delivered order, so no phase is ever played early, late, or twice.
    const snapshots: string[][] = [];
    for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
      harness.flushTweens(1);
      snapshots.push([...playedPhases(ctx)]);
    }
    settle();

    for (const snapshot of snapshots) {
      expect(snapshot).toEqual(CASCADE_PHASES.slice(0, snapshot.length));
    }

    const phases = [...playedPhases(ctx)];
    expect(phases).toEqual(CASCADE_PHASES);
    expect(snapshots.length).toBeGreaterThan(0);

    // One phase per delivered `CascadeCreated`, carrying that event's own depth
    // (`MATCH3_RULES.md` §4.2 item 2), in delivered order.
    const deliveredDepths = CASCADE_BATCH.filter(
      (event) => (event as { type: string }).type === 'CascadeCreated'
    ).map((event) => (event as { cascadeDepth: number }).cascadeDepth);
    expect(phases.filter((phase) => phase.startsWith('cascade '))).toEqual(
      deliveredDepths.map((depth) => `cascade depth=${depth}`)
    );

    // `MatchCreated.cascadeDepth` and `CascadeCreated.cascadeDepth` are two
    // different documented values for one pass and are never equated
    // (`SIGNALR_PROTOCOL.md` §3.2.7 item 2): pass 2 delivers `MatchCreated`
    // depth 2 next to `CascadeCreated` depth 1, and each phase reports its own
    // event's value under its own name.
    expect(phases.filter((phase) => phase === 'match depth=1')).toHaveLength(1);
    expect(phases.filter((phase) => phase === 'cascade depth=1')).toHaveLength(1);
    expect(phases.filter((phase) => phase === 'cascade depth=2')).toHaveLength(1);
  });

  it('renders the board from persistent gem views that a resolution never destroys', () => {
    const { harness } = createTimelineBattle();
    const tilesBefore = harness.boardCells.filter((cell) => cell.kind === 'tile');
    const labelsBefore = harness.boardCells.filter((cell) => cell.kind === 'label');
    expect(tilesBefore).toHaveLength(64);
    expect(labelsBefore).toHaveLength(64);

    const settle = submitHeldSwap(harness);
    deliverResolution(
      harness,
      battleState({ sequence: 1, board: deliveredBoard(1) }),
      CASCADE_BATCH.slice(11)
    );
    settle();
    harness.flushTweens();

    // The same 128 display objects, index for index: the resolution moved and
    // relabelled the board's views instead of recreating all 64 cells.
    const tilesAfter = harness.boardCells.filter((cell) => cell.kind === 'tile');
    const labelsAfter = harness.boardCells.filter((cell) => cell.kind === 'label');
    expect(harness.boardCells).toHaveLength(128);
    tilesAfter.forEach((tile, index) => expect(tile).toBe(tilesBefore[index]));
    labelsAfter.forEach((label, index) => expect(label).toBe(labelsBefore[index]));

    // And every destination cell converged on the authoritative board.
    expect(labelsByCell(harness)).toEqual(expectedLabels(1));
  });

  it('holds the awaited resolution’s board until its timeline presents it', () => {
    const { harness, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    harness.setBattleState(battleState({ sequence: 1, board: deliveredBoard(1) }));

    // The push arrived before its batch, so the board the timeline is about to
    // present is not on screen yet.
    expect(labelsByCell(harness)).toEqual(expectedLabels(0));

    deliverResolution(harness, battleState({ sequence: 1, board: deliveredBoard(1) }), [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    ]);
    settle();
    harness.flushTweens();

    expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1', 'settle']);
    expect(labelsByCell(harness)).toEqual(expectedLabels(1));
  });

  it('converges a hold whose batch never matches it, rather than leaving it on screen', () => {
    const { harness, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    harness.setBattleState(battleState({ sequence: 1, board: deliveredBoard(1) }));
    expect(labelsByCell(harness)).toEqual(expectedLabels(0));

    // A batch for a different resolution is not the hold's to release: the held
    // board converges immediately, and no settle phase is built for it.
    harness.emitBattleEvents({
      battleId: 'battle-245',
      serverSequence: 2,
      events: [{ type: 'PowerChanged', delta: 5, power: 5, source: 'match' }],
    });
    settle();
    harness.flushTweens();

    expect(labelsByCell(harness)).toEqual(expectedLabels(1));
    // The delivered Power change is its own step and the board cycle still
    // settles: TASK-252 gives the Power readout a phase of its own (D-2).
    expect(playedPhases(ctx)).toEqual(['swap 20->21', 'power 5', 'settle']);
  });

  it('presents player damage and boss retaliation as two distinct ordered phases', async () => {
    const { harness, ctx } = createTimelineBattle();

    // The collection reads settle first, so the only text this resolution adds is
    // its own combat feedback.
    await flush();
    const before = harness.texts.length;

    const settle = submitHeldSwap(harness);
    deliverResolution(
      harness,
      battleState({ sequence: 1 }),
      CASCADE_BATCH.slice(11).filter((event) => {
        const type = (event as { type: string }).type;
        return type.startsWith('Damage') || type === 'BossSkillCast';
      })
    );
    settle();
    harness.flushTweens();

    const phases = [...playedPhases(ctx)];
    expect(phases.indexOf('damage player->boss')).toBeGreaterThan(-1);
    expect(phases.indexOf('retaliation boss->player')).toBeGreaterThan(
      phases.indexOf('damage player->boss')
    );

    // One floater per damage instance, over the panel of the party the delivered
    // events name as the receiver (`SIGNALR_PROTOCOL.md` §3.2.14 item 3). The cast
    // controls are re-rendered when the collection reads settle, so their
    // captions are excluded.
    const added = harness.texts
      .slice(before)
      .map((text) => text.text)
      .filter((text) => !text.startsWith('Card: ') && !text.startsWith('Skill: '));
    expect(added).toEqual(['-150', '-80']);
  });

  it('presents a Boss-only resolution as retaliation alone', () => {
    const { harness, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    deliverResolution(harness, battleState({ sequence: 1 }), [
      { type: 'DamageCalculated', base: 80, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 80 },
      { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
      { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
    ]);
    settle();
    harness.flushTweens();

    const combat = playedPhases(ctx).filter(
      (phase) => phase.startsWith('damage ') || phase.startsWith('retaliation ')
    );
    expect(combat).toEqual(['retaliation boss->player']);
  });

  it('locks player input for the whole timeline and releases it on completion', async () => {
    const { harness, scene, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    expect(harness.requestedActions).toHaveLength(0);

    deliverResolution(harness, battleState({ sequence: 1 }), CASCADE_BATCH);
    settle();
    await flush();

    // Locked while the timeline is still playing: a gesture changes nothing and
    // submits nothing, and the acknowledgement has already settled.
    expect(inputLocked(scene, ctx)).toBe(true);
    tapCell(harness, 30);
    tapCell(harness, 31);
    await flush();
    expect(harness.requestedActions).toHaveLength(0);

    harness.flushTweens();

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(harness.pendingTweenCount()).toBe(0);
    tapCell(harness, 30);
    tapCell(harness, 31);
    await flush();
    expect(harness.requestedActions).toEqual([{ kind: 'Swap', fromCell: 30, toCell: 31 }]);
  });

  it('releases the guard and converges the board when a newer state supersedes playback', async () => {
    const { harness, scene, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    deliverResolution(harness, battleState({ sequence: 1 }), CASCADE_BATCH);
    settle();
    await flush();

    // Play only the first phase, then let a newer authoritative state arrive.
    harness.flushTweens(1);
    const played = [...playedPhases(ctx)];
    expect(played.length).toBeLessThan(CASCADE_PHASES.length);
    expect(inputLocked(scene, ctx)).toBe(true);

    harness.setBattleState(battleState({ sequence: 2, board: deliveredBoard(2) }));

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(labelsByCell(harness)).toEqual(expectedLabels(2));
    expect(harness.boardCells).toHaveLength(128);

    // The abandoned timeline's pacing was cancelled with it — not left running
    // towards a phase the newer state has already replaced.
    expect(harness.killedTweenTargets).toContain(field<object>(ctx, 'phaseClock'));

    // Playback is abandoned: flushing the queue cannot advance a timeline that
    // no longer exists.
    harness.flushTweens();
    expect([...playedPhases(ctx)]).toEqual(played);
    expect(harness.pendingTweenCount()).toBe(0);
  });

  it('supersedes an in-flight timeline with a newer batch, and ignores a duplicate one', async () => {
    const { harness, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    deliverResolution(harness, battleState({ sequence: 1 }), CASCADE_BATCH);
    settle();
    await flush();

    expect(playedPhases(ctx)).toEqual(['swap 20->21']);
    harness.flushTweens(1);

    // A newer batch supersedes the one playing: the abandoned timeline's
    // remaining cascade phases are never played.
    const newer = [
      { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
    ];
    harness.emitBattleEvents({
      battleId: 'battle-245',
      serverSequence: 2,
      events: newer,
    });

    // A duplicate of the batch now playing is recorded and presented no second
    // time — one resolution is presented once.
    const loggedBefore = field<string[]>(ctx, 'presentedEventsLog').length;
    harness.emitBattleEvents({
      battleId: 'battle-245',
      serverSequence: 2,
      events: newer,
    });
    expect(field<string[]>(ctx, 'presentedEventsLog').length).toBeGreaterThan(loggedBefore);

    harness.flushTweens();

    const phases = playedPhases(ctx);
    expect(phases).not.toContain('cascade depth=1');
    expect(phases).not.toContain('cascade depth=2');
    expect(phases.filter((phase) => phase === 'retaliation boss->player')).toHaveLength(1);
  });

  it('releases the guard on a presentation error without throwing', async () => {    const { harness, scene, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    harness.setBattleState(battleState({ sequence: 1 }));
    expect(() =>
      harness.emitBattleEvents({
        battleId: 'battle-245',
        serverSequence: 1,
        events: [
          {
            type: 'MatchCreated',
            get shape() {
              throw new Error('Exploding getter in presentation');
            },
          },
        ],
      } as unknown as BattleEventsEnvelope)
    ).not.toThrow();

    settle();
    await flush();
    harness.flushTweens();

    expect(inputLocked(scene, ctx)).toBe(false);
    // A malformed batch presents nothing, and the awaited resolution's board is
    // still converged on the authoritative state.
    expect(labelsByCell(harness)).toEqual(expectedLabels(0));
  });

  it('releases the guard and every tween on scene teardown mid-timeline', () => {
    const { harness, scene, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    deliverResolution(harness, battleState({ sequence: 1 }), CASCADE_BATCH);
    settle();

    expect(inputLocked(scene, ctx)).toBe(true);

    harness.shutdownScene(ctx);

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(harness.pendingTweenCount()).toBe(0);
    expect(harness.boardCells).toHaveLength(0);
  });

  it('releases the guard and every tween on DESTROY too (TASK-204 lifecycle)', () => {
    // Phaser takes a scene down through either event — `Systems#shutdown` emits
    // SHUTDOWN and `#destroy` emits DESTROY — so a timeline in flight must be
    // released by both, not just by the one a test happens to raise.
    const { harness, scene, ctx } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    deliverResolution(harness, battleState({ sequence: 1 }), CASCADE_BATCH);
    settle();

    expect(inputLocked(scene, ctx)).toBe(true);

    harness.destroyScene(ctx);

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(harness.pendingTweenCount()).toBe(0);
    expect(harness.boardCells).toHaveLength(0);
  });

  it('animates the HP gauges to their delivered values instead of jumping', () => {
    const { harness, ctx } = createTimelineBattle();

    const boss = () => field<GaugeView>(ctx, 'bossHpGauge');
    const pet = () => field<GaugeView>(ctx, 'petHpGauge');
    expect(boss().fill.width).toBe(boss().width);
    expect(pet().fill.width).toBe(pet().width);

    const next = battleState({
      sequence: 1,
      bossState: { bossId: 'boss-hoa-long', hp: 1250, maxHp: 5000 },
      petState: { ...battleState().petState, hp: 640 },
    });

    const settle = submitHeldSwap(harness);
    deliverResolution(harness, next, CASCADE_BATCH.slice(11));
    settle();

    // The awaited resolution's HP pair is held with its board: the panels still
    // show the previous delivered values while the timeline has not reached them.
    const rendered = () => harness.texts.map((text) => text.text).join('\n');
    expect(rendered()).toContain('5000 / 5000');
    expect(rendered()).not.toContain('1250 / 5000');

    // Drive the timeline to the damage phase: the delivered pair is now on the
    // readout and the gauge is travelling, not jumped.
    let guard = 0;
    while (!rendered().includes('1250 / 5000') && guard < 500) {
      harness.flushTweens(1);
      guard++;
    }

    expect(rendered()).toContain('1250 / 5000');
    expect(boss().fill.width).toBe(boss().width);

    harness.flushTweens();

    // It lands exactly on the delivered proportion — the same width a direct
    // write would have produced.
    expect(boss().fill.width).toBe(Math.round(boss().width * (1250 / 5000)));
    expect(pet().fill.width).toBe(Math.round(pet().width * (640 / 1000)));
    expect(rendered()).toContain('640 / 1000');
  });

  it('hands the outcome off after the preceding damage phases, and never loses it', () => {
    const { harness } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    deliverResolution(harness, battleState({ sequence: 1 }), [
      { type: 'DamageCalculated', base: 100, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 150 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
      { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 850 },
    ]);
    settle();

    // The damage phases come first: nothing has been handed off yet.
    expect(harness.sceneStarted).toHaveLength(0);

    harness.flushTweens();

    expect(harness.sceneStarted).toEqual([
      {
        key: 'ResultScene',
        data: {
          outcome: 'victory',
          finalBossHp: 0,
          finalPlayerHp: 850,
          battleId: 'battle-245',
        },
      },
    ]);
  });

  it('still hands the outcome off when the timeline is superseded before it', () => {
    const { harness } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    deliverResolution(harness, battleState({ sequence: 1 }), [
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 850 },
    ]);
    settle();

    harness.flushTweens(1);
    expect(harness.sceneStarted).toHaveLength(0);

    // A newer authoritative state supersedes the playback, and the terminal
    // outcome is still handed off — it can never be blocked by the timeline.
    harness.setBattleState(battleState({ sequence: 2 }));

    expect(harness.sceneStarted.map((started) => started.key)).toEqual(['ResultScene']);
  });

  it('plays no timeline and locks nothing for a rejected Swap', async () => {
    const harness = createSceneHarness({ battleState: battleState(), withTweens: true });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    runScene(scene, ctx, 'create');

    harness.setActionResult({ accepted: false, reason: 'NO_MATCH_FROM_SWAP' });
    tapCell(harness, 20);
    tapCell(harness, 21);
    await flush();

    // A rejected Swap emits no events (`GAME_EVENTS.md` §1.2), so no batch — and
    // therefore no timeline, no swap movement, and no lock.
    expect(playedPhases(ctx)).toEqual([]);
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(labelsByCell(harness)).toEqual(expectedLabels(0));
  });

  it('ignores unknown events inside a batch without fabricating a phase', () => {
    const { harness, scene, ctx } = createTimelineBattle();

    harness.emitBattleEvents({
      battleId: 'battle-245',
      serverSequence: 1,
      events: [
        { type: 'TurnStarted', turn: 2 },
        { type: 'UnknownFabricatedEvent', randomData: 1 },
        { notAnEvent: true },
      ],
    });

    expect(playedPhases(ctx)).toEqual([]);
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(harness.pendingTweenCount()).toBe(0);
  });

  it('converges every destination cell on the authoritative board, and computes nothing', () => {
    // PD-1 / PD-8 / AGENTS.md §10: the interpolation is presentation only. Every
    // destination cell shows the cell's own delivered content, and the pairing's
    // result is never read back — the board the server delivered is the board on
    // screen, cell for cell, with the four documented Gem types and nothing else.
    const { harness } = createTimelineBattle();

    const settle = submitHeldSwap(harness);
    deliverResolution(
      harness,
      battleState({ sequence: 1, board: deliveredBoard(3) }),
      CASCADE_BATCH
    );
    settle();
    harness.flushTweens();

    const labels = labelsByCell(harness);
    expect(labels).toEqual(expectedLabels(3));
    expect(new Set(labels)).toEqual(new Set(['ATK', 'DEF', 'HP', 'PWR']));

    // The view count is invariant across a resolution: nothing was cleared,
    // moved, or spawned by a client-side rule.
    expect(harness.boardCells).toHaveLength(128);

    // And the source holds no gravity, spawn, or match-resolution step.
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    for (const forbidden of [
      'Math.random',
      'compaction',
      'survivors',
      'lowestCells',
      'spawnGem',
      'gravityDestination',
      'collapseColumn',
      'Math.abs(row',
    ]) {
      expect(source, `BattleScene must not reference "${forbidden}"`).not.toContain(forbidden);
    }

    // The pairing is within-column relative order and nothing else: the board is
    // written column by column, top to bottom, with no cross-column pairing.
    expect(source).toContain('for (let column = 0; column < BOARD_COLUMNS; column++)');
    expect(source).toContain('for (let row = 0; row < BOARD_ROWS; row++)');
  });

  // ---------------------------------------------------------------------------
  // TASK-254 — Recovery, the board hold, and the stages a resolution skips
  // ---------------------------------------------------------------------------

  describe('TASK-254 — Recovery, the board hold, and the stages a resolution skips', () => {
    /** The phase kinds of the in-flight timeline, in order. */
    function timelineKinds(ctx: object): string[] {
      return (field<Array<{ kind: string }> | null>(ctx, 'presentationTimeline') ?? []).map(
        (phase) => phase.kind
      );
    }

    /** Plays the in-flight timeline to its end, one step at a time. */
    function playToEnd(harness: ReturnType<typeof createSceneHarness>): void {
      for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
        harness.flushTweens(1);
      }
    }

    it('does not reveal the resolution’s final board when the acknowledgement settles first', async () => {
      const { harness, scene, ctx } = createTimelineBattle();

      const settle = submitHeldSwap(harness, 20, 21);

      // The acknowledgement settles first — one of the two legal delivery orders
      // (SIGNALR_PROTOCOL.md §3.1) — with no timeline playing yet. The guard covers
      // the interval regardless: the resolution it was accepted for has not been
      // presented, let alone reconciled (TASK-254 AF-5).
      settle();
      await flush();
      expect((ctx as { orchestration: string }).orchestration).toBe('submitted');
      expect(inputLocked(scene, ctx)).toBe(true);

      // The resolution's own authoritative state arrives before its batch. It is
      // exactly the next delivered sequence, so it is held: the board it carries is
      // *not* the displayed board (AF-6).
      harness.setBattleState(battleState({ sequence: 1, board: deliveredBoard(1) }));
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));
      expect(inputLocked(scene, ctx)).toBe(true);

      // Its batch presents it: the committed Swap plays first and the board changes
      // at the settle phase — the resolution's own supported transition, and not
      // before it.
      harness.emitBattleEvents({
        battleId: 'battle-245',
        serverSequence: 1,
        events: CASCADE_BATCH,
      });
      expect(playedPhases(ctx)).toEqual(['swap 20->21']);
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));
      expect(inputLocked(scene, ctx)).toBe(true);

      playToEnd(harness);

      expect(playedPhases(ctx)).toEqual(CASCADE_PHASES);
      expect(labelsByCell(harness)).toEqual(expectedLabels(1));
      expect((ctx as { orchestration: string }).orchestration).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(harness.pendingTweenCount()).toBe(0);
    });

    it('does not reveal the resolution’s final board when the batch arrives first', async () => {
      const { harness, scene, ctx } = createTimelineBattle();

      const settle = submitHeldSwap(harness, 20, 21);

      // The batch arrives first, with the state push still to come: the timeline
      // plays its delivered steps and the board on screen stays the one the gesture
      // was made on.
      harness.emitBattleEvents({
        battleId: 'battle-245',
        serverSequence: 1,
        events: CASCADE_BATCH,
      });
      settle();
      await flush();

      expect(playedPhases(ctx).length).toBeGreaterThan(0);
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));

      // The state push arrives mid-timeline: it is held for the timeline that is
      // already presenting this resolution.
      harness.setBattleState(battleState({ sequence: 1, board: deliveredBoard(1) }));
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));

      playToEnd(harness);

      expect(labelsByCell(harness)).toEqual(expectedLabels(1));
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('presents no board state the server did not deliver', () => {
      const { harness, ctx } = createTimelineBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(
        harness,
        battleState({ sequence: 1, board: deliveredBoard(1) }),
        CASCADE_BATCH
      );
      settle();

      // Every board the presentation shows is one of exactly three delivered-shaped
      // states: the board the gesture was made on, that same board with the
      // player's *own* two committed cells exchanged (the Swap step's presentation
      // of the request the server accepted), and the resolution's single delivered
      // final board. No per-cascade board is invented, because the wire delivers
      // none (SIGNALR_PROTOCOL.md §3.1 item 2, GAME_STATE.md §5.3 item 4).
      const before = expectedLabels(0);
      const exchanged = [...before];
      [exchanged[20], exchanged[21]] = [exchanged[21], exchanged[20]];
      const final = expectedLabels(1);
      const allowed = [before, exchanged, final].map((labels) => labels.join('|'));

      const samples: string[] = [labelsByCell(harness).join('|')];
      let phasesSeen = playedPhases(ctx).length;
      for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
        harness.flushTweens(1);
        // The board is sampled at each **phase boundary** — the moment a delivered
        // step has finished presenting — rather than mid-animation.
        const played = playedPhases(ctx).length;
        if (played !== phasesSeen) {
          phasesSeen = played;
          samples.push(labelsByCell(harness).join('|'));
        }
      }

      expect(samples.length).toBeGreaterThan(2);
      for (const sample of samples) {
        expect(allowed, `a presented board must be a delivered state: ${sample}`).toContain(sample);
      }
      // The player's own two committed cells were seen to change places, and the
      // resolution's one delivered final board was reached — so the samples above
      // really did cover the whole presentation rather than a single frame.
      expect(samples).toContain(exchanged.join('|'));
      expect(samples).toContain(final.join('|'));

      // One board transition and no more: exactly one settle phase, placed after
      // the last cascade pass and before the resolution's combat stages.
      const phases = playedPhases(ctx);
      expect(phases.filter((phase) => phase === 'settle')).toHaveLength(1);
      expect(phases.lastIndexOf('cascade depth=2')).toBeLessThan(phases.indexOf('settle'));
      expect(phases.indexOf('settle')).toBeLessThan(phases.indexOf('damage player->boss'));
    });

    it('skips the Board Resolution stage for a cast that moves no board', async () => {
      const { harness, scene, ctx } = createTimelineBattle();

      // A Card cast is an auxiliary action that does not resolve the board
      // (GAME_RULES.md §17): the client submits it exactly as it submits a Swap.
      harness.clickOption(/^Card: card-heal$/);
      await flush();

      // Its state push is held exactly as a Swap's is: it is this resolution's own
      // next delivered sequence.
      harness.setBattleState(battleState({ sequence: 1 }));
      expect((ctx as { orchestration: string }).orchestration).toBe('submitted');

      harness.emitBattleEvents({
        battleId: 'battle-245',
        serverSequence: 1,
        events: [{ type: 'CardCast', cardId: 'card-heal' }],
      });

      // The cast's own step is the whole timeline: no Match, no Cascade, and no
      // Board Resolution stage exists, because this resolution delivered no board
      // cycle at all — a stage with no delivered event is skipped, never fabricated.
      expect(timelineKinds(ctx)).toEqual(['cast']);
      expect(playedPhases(ctx)).toEqual(['cast card-heal']);
      expect(field<{ text: string }>(ctx, 'calloutText').text).toMatch(/^CARD: /);

      playToEnd(harness);

      // The board never moved, and the guard is released once Recovery has
      // converged the delivered state.
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('skips the Board Resolution stage for a Boss-only resolution that moved no board', () => {
      const { harness, ctx } = createTimelineBattle();

      harness.emitBattleEvents({
        battleId: 'battle-245',
        serverSequence: 1,
        events: [
          { type: 'DamageCalculated', base: 80, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 80 },
          { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
          { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
        ],
      });

      // The Boss's response is one combat step: nothing about the board was
      // delivered, so nothing about the board is presented.
      expect(timelineKinds(ctx)).toEqual(['retaliation']);
      expect(playedPhases(ctx)).toEqual(['retaliation boss->player']);
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));

      playToEnd(harness);
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));
    });
  });
});

describe('BattleScene — selected-cell treatment, interaction indicator & pacing (TASK-246)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** The four documented Gem contract names (MATCH3_RULES.md §1.1). */
  const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];

  /** A scene-owned presentation field, read the way the scene wrote it. */
  function field<T>(ctx: object, name: string): T {
    return (ctx as Record<string, T>)[name];
  }

  /**
   * The scene's own input guard, evaluated against the context that owns its
   * state (the harness runs a scene through a `this` context, the way Phaser's
   * own Scene Systems do).
   */
  function inputLocked(scene: BattleScene, ctx: object): boolean {
    return (
      Object.getPrototypeOf(scene) as { isInputLocked(this: object): boolean }
    ).isInputLocked.call(ctx);
  }

  /** One rendered rectangle: a board tile, an overlay, a gauge fill, or the ring. */
  interface RenderedRectangle {
    readonly x: number;
    readonly y: number;
    readonly width: number;
    readonly height: number;
    readonly strokeWidth?: number;
    readonly strokeColor?: number;
    readonly visible?: boolean;
    readonly alpha?: number;
  }

  /** The persistent selection treatment, as the scene owns it. */
  const ring = (ctx: object) => field<RenderedRectangle | null>(ctx, 'selectionRing');

  /** The persistent interaction indicator, as the scene owns it. */
  const indicator = (ctx: object) =>
    field<{ readonly text: string; readonly color?: string } | null>(ctx, 'phaseText');
  const indicatorLabel = (ctx: object): string | null => indicator(ctx)?.text ?? null;

  /** The phases the timeline has played, in order. */
  const playedPhases = (ctx: object): string[] => [...field<string[]>(ctx, 'presentedPhases')];

  /**
   * A delivered 8 x 8 board, rotated by `shift` cells so a resolution's board is
   * provably different from the one already on screen.
   */
  function deliveredBoard(shift = 0): RuntimeBoard {
    return {
      cells: Array.from({ length: 64 }, (_, index) => ({
        gemType: GEM_NAMES[(index + shift) % GEM_NAMES.length],
      })),
    };
  }

  function battleState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return {
      battleId: 'battle-246',
      turn: 1,
      sequence: 0,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: deliveredBoard(),
      playerState: { combo: 0, matchCount: 0 },
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
      ...overrides,
    };
  }

  /** A battle scene with a recorded, test-driven tween queue. */
  function createBattle(state: RuntimeBattleState | null = battleState()) {
    const harness = createSceneHarness({ battleState: state, withTweens: true });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    runScene(scene, ctx, 'create');
    if (harness.pendingTweenCount() > 0) {
      harness.flushTweens(harness.pendingTweenCount());
    }
    return { harness, scene, ctx };
  }

  /**
   * Submits one Swap whose acknowledgement is held open — the state a real
   * request is in while its resolution's state push and event batch arrive
   * (`SIGNALR_PROTOCOL.md` §3.1). The returned function settles it.
   */
  function submitHeldSwap(
    harness: ReturnType<typeof createSceneHarness>,
    from = 20,
    to = 21
  ): () => void {
    let settle: (() => void) | null = null;

    harness.setActionBehaviour(
      () =>
        new Promise<never>((resolve) => {
          settle = () => resolve({ accepted: true, reason: null } as never);
        })
    );

    tapCell(harness, from);
    tapCell(harness, to);

    return () => {
      harness.setActionBehaviour(null);
      settle?.();
    };
  }

  /** Delivers one resolution the way the server does: state push, then batch. */
  function deliverResolution(
    harness: ReturnType<typeof createSceneHarness>,
    next: RuntimeBattleState,
    events: unknown[]
  ): void {
    harness.setBattleState(next);
    harness.emitBattleEvents({
      battleId: next.battleId,
      serverSequence: next.sequence,
      events,
    });
  }

  /**
   * One resolved Swap covering every phase kind the timeline can play: a Match,
   * two delivered Cascade passes, a delivered Power movement, the player's hit on
   * the Boss, and the Boss's retaliation.
   */
  const RESOLUTION_BATCH: unknown[] = [
    { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
    { type: 'CascadeCreated', cascadeDepth: 1 },
    { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'DEF', cascadeDepth: 2 },
    { type: 'GemMatched', cellIndex: 32, gemType: 'DEF' },
    { type: 'CascadeCreated', cascadeDepth: 2 },
    { type: 'MatchCreated', shape: 'Lt', cells: [40, 41, 42, 48], gemType: 'HP', cascadeDepth: 3 },
    { type: 'ComboChanged', combo: 3 },
    { type: 'PowerChanged', delta: 10, power: 10, source: 'match' },
    { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
    { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
    { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
    { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
  ];

  /** The delivered state that resolution commits: a moved board and two moved HP pairs. */
  function resolvedState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return battleState({
      sequence: 1,
      board: deliveredBoard(1),
      bossState: { bossId: 'boss-hoa-long', hp: 1250, maxHp: 5000 },
      petState: { ...battleState().petState, hp: 640 },
      ...overrides,
    });
  }

  // ---------------------------------------------------------------------------
  // Selection feedback (TASK-246 §3)
  // ---------------------------------------------------------------------------

  it('renders the selected cell with a persistent board treatment of its own', () => {
    const { harness, ctx } = createBattle();

    // Nothing is selected yet: the treatment exists but marks no cell.
    expect(ring(ctx)).not.toBeNull();
    expect(ring(ctx)?.visible).toBe(false);

    tapCell(harness, 12);

    const marking = ring(ctx);
    const centre = cellCentre(12);

    // The cell itself is marked, at the cell's own position...
    expect(marking?.visible).toBe(true);
    expect(marking?.x).toBe(centre.x);
    expect(marking?.y).toBe(centre.y);

    // ...with a treatment that is only the selection's: an opaque 4 px white
    // ring, never faded (alpha stays 1), sized to the cell, and distinct from the
    // match/cascade overlays (`spawnCellHighlight`'s translucent fills) and from
    // a cell tile's own 1 px stroke.
    expect(marking?.strokeWidth).toBe(4);
    expect(marking?.strokeColor).toBe(0xffffff);
    expect(marking?.width).toBe(CELL_SIZE + 4);
    expect(marking?.alpha).toBe(1);

    // The text line still reports the selection: the treatment is board
    // feedback in addition to it, not a replacement for it.
    expect(field<{ text: string }>(ctx, 'swapText').text).toContain('Cell 12 selected');

    // And nothing animates it: no tween targets the ring, so it cannot fade or
    // destroy itself the way a transient highlight does while the cell stays
    // selected.
    expect(harness.pendingTweenCount()).toBe(0);
    expect(harness.pendingTweenRecords().some((t) => t.targets.includes(marking as object))).toBe(
      false
    );
  });

  it('deselects on a second tap on the same cell and hides the treatment', () => {
    const { harness, ctx } = createBattle();

    tapCell(harness, 12);
    expect(ring(ctx)?.visible).toBe(true);

    tapCell(harness, 12);

    expect(ring(ctx)?.visible).toBe(false);
    expect(harness.requestedActions).toHaveLength(0);
    expect(field<{ text: string }>(ctx, 'swapText').text).toBe('Select a cell to swap.');
  });

  it('clears the treatment when the pair is submitted', async () => {
    const { harness, ctx } = createBattle();

    tapCell(harness, 12);
    expect(ring(ctx)?.visible).toBe(true);

    tapCell(harness, 13);
    await flush();

    expect(ring(ctx)?.visible).toBe(false);
    expect(harness.requestedActions).toEqual([{ kind: 'Swap', fromCell: 12, toCell: 13 }]);
  });

  it('composes the selection with the cell’s own styling instead of clobbering it', () => {
    // R-1: selecting a cell must not disturb what that cell renders. The cell's
    // own styling lives on its tile (fill and stroke) and its label; the
    // selection is a separate persistent ring, so the two compose
    // (GAME_STATE.md §2.1.3: the cell's Gem type is its whole content).
    const cells: Array<{ gemType: string }> = Array.from({ length: 64 }, (_, index) => ({
      gemType: GEM_NAMES[index % GEM_NAMES.length],
    }));
    cells[12] = { gemType: 'HP' };

    const { harness, ctx } = createBattle(battleState({ board: { cells } }));

    const tiles = harness.boardCells.filter((cell) => cell.kind === 'tile');
    const labels = harness.boardCells.filter((cell) => cell.kind === 'label');

    const fillBefore = tiles[12].fill;
    const strokeBefore = tiles[12].strokeColor;
    const strokeWidthBefore = tiles[12].strokeWidth;
    expect(labels[12].label).toBe('HP');

    tapCell(harness, 12);

    // Both are still visible: the delivered cell styling is untouched and the
    // selection ring marks the same cell.
    expect(tiles[12].fill).toBe(fillBefore);
    expect(tiles[12].strokeColor).toBe(strokeBefore);
    expect(tiles[12].strokeWidth).toBe(strokeWidthBefore);
    expect(labels[12].label).toBe('HP');
    expect(ring(ctx)?.visible).toBe(true);
    expect(ring(ctx)?.x).toBe(cellCentre(12).x);
    expect(ring(ctx)?.y).toBe(cellCentre(12).y);
  });

  it('never lets a stale selection pair with a later tap to submit an unintended Swap', async () => {
    const { harness, scene, ctx } = createBattle();

    // The player selects a cell and submits the pair: the selection ends with
    // the gesture, before the resolution even starts.
    const settle = submitHeldSwap(harness, 20, 21);
    expect(ring(ctx)?.visible).toBe(false);
    expect(field<number | null>(ctx, 'selectedCell')).toBeNull();

    deliverResolution(harness, resolvedState(), RESOLUTION_BATCH);

    // A tap during the resolution's own timeline changes nothing and is not
    // remembered: it neither selects its cell nor completes a pair.
    expect(inputLocked(scene, ctx)).toBe(true);
    tapCell(harness, 30);
    await flush();

    expect(field<number | null>(ctx, 'selectedCell')).toBeNull();
    expect(ring(ctx)?.visible).toBe(false);
    // The only request the scene ever made is the pair the player submitted; the
    // tap that landed while the guard was engaged was refused, not queued.
    expect(harness.actionInvocations).toEqual([{ kind: 'Swap', fromCell: 20, toCell: 21 }]);

    harness.flushTweens();
    settle();
    await flush();
    expect(inputLocked(scene, ctx)).toBe(false);

    // The next tap therefore *starts* a gesture rather than finishing the
    // interrupted one, and only the player's own second tap submits.
    tapCell(harness, 30);
    expect(ring(ctx)?.visible).toBe(true);
    await flush();
    expect(harness.actionInvocations).toEqual([{ kind: 'Swap', fromCell: 20, toCell: 21 }]);

    tapCell(harness, 31);
    await flush();

    expect(harness.actionInvocations).toEqual([
      { kind: 'Swap', fromCell: 20, toCell: 21 },
      { kind: 'Swap', fromCell: 30, toCell: 31 },
    ]);
    expect(ring(ctx)?.visible).toBe(false);
  });

  it('clears the selection when a cast engages the same guard', async () => {
    // A CardCast is not a Swap gesture, but it engages the same input guard — so
    // a board selection must not survive it and pair with a later tap.
    const { harness, ctx } = createBattle();
    await flush();

    tapCell(harness, 12);
    expect(ring(ctx)?.visible).toBe(true);

    harness.clickOption('Card: Heal');
    await flush();

    expect(harness.actionInvocations.map((action) => action.kind)).toEqual(['CardCast']);
    expect(ring(ctx)?.visible).toBe(false);
    expect(field<number | null>(ctx, 'selectedCell')).toBeNull();
  });

  it('ends the selection when an authoritative push presents another board', () => {
    const { harness, ctx } = createBattle();

    tapCell(harness, 12);
    expect(ring(ctx)?.visible).toBe(true);

    // A push for a different battle (or a resync) replaces the board the gesture
    // was made on, so the gesture ends with it.
    harness.setBattleState(
      battleState({ battleId: 'battle-246-next', sequence: 1, board: deliveredBoard(2) })
    );

    expect(ring(ctx)?.visible).toBe(false);
    expect(field<number | null>(ctx, 'selectedCell')).toBeNull();

    // And a cleared state (battle over, session change) drops it too, because the
    // board it marked is gone.
    tapCell(harness, 12);
    expect(ring(ctx)?.visible).toBe(true);

    harness.setBattleState(null);

    expect(ring(ctx)?.visible).toBe(false);
    expect(harness.boardCells).toHaveLength(0);
  });

  it('drops a live selection on SHUTDOWN and on DESTROY', () => {
    const shutDown = createBattle();
    tapCell(shutDown.harness, 12);
    expect(ring(shutDown.ctx)?.visible).toBe(true);

    shutDown.harness.shutdownScene(shutDown.ctx);

    expect(field(shutDown.ctx, 'selectionRing')).toBeNull();
    expect(field<number | null>(shutDown.ctx, 'selectedCell')).toBeNull();
    expect(shutDown.harness.boardCells).toHaveLength(0);

    const destroyed = createBattle();
    tapCell(destroyed.harness, 12);
    expect(ring(destroyed.ctx)?.visible).toBe(true);

    destroyed.harness.destroyScene(destroyed.ctx);

    expect(field(destroyed.ctx, 'selectionRing')).toBeNull();
    expect(field<number | null>(destroyed.ctx, 'selectedCell')).toBeNull();
    expect(destroyed.harness.boardCells).toHaveLength(0);
  });

  // ---------------------------------------------------------------------------
  // Interaction / phase messaging (TASK-246 §4)
  // ---------------------------------------------------------------------------

  it('states the client’s own interaction state, and claims no server turn', () => {
    const { ctx } = createBattle();

    // The client permits input, so it says so — as its own interaction state
    // (`isInputLocked()` is false), never as a server-asserted turn.
    expect(indicatorLabel(ctx)).toBe('YOUR TURN');
    expect(indicator(ctx)?.color).toBe('#4ade80');

    // Nothing about a turn's ownership or number is presented: the wire carries
    // no such member, and the raw `turn` counter stays unrendered (TASK-210 §9).
    expect(indicatorLabel(ctx)).not.toMatch(/TURN\s*\d/);
    expect(indicatorLabel(ctx)).not.toMatch(/BOSS TURN/);
  });

  it('claims nothing before an authoritative board has arrived', () => {
    const { ctx } = createBattle(null);

    // With no delivered battle there is no interaction to permit, so the
    // indicator makes no statement at all rather than claiming a move.
    expect(indicatorLabel(ctx)).toBe('');
    expect(field<{ text: string }>(ctx, 'boardMessageText').text).toBe('Preparing the board…');
  });

  it('reads RESOLVING through the timeline and BOSS ATTACK only in the delivered retaliation phase', async () => {
    const { harness, scene, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), RESOLUTION_BATCH);

    const samples: Array<{ phase: string; label: string | null }> = [];
    const sample = () => {
      const phases = playedPhases(ctx);
      samples.push({ phase: phases[phases.length - 1] ?? '', label: indicatorLabel(ctx) });
    };

    sample();
    for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
      harness.flushTweens(1);
      sample();
    }
    settle();
    await flush();
    harness.flushTweens();
    sample();

    const labelsFor = (phase: string) =>
      samples.filter((entry) => entry.phase === phase).map((entry) => entry.label);

    // While the timeline plays, the indicator says the client is busy — for the
    // swap, the match, the cascades, the settle, and the player's own damage.
    expect(labelsFor('swap 20->21').every((label) => label === 'RESOLVING')).toBe(true);
    expect(labelsFor('damage player->boss').length).toBeGreaterThan(0);
    expect(labelsFor('damage player->boss').every((label) => label === 'RESOLVING')).toBe(true);

    // The Boss's response is the one phase that reads BOSS ATTACK, and it is the
    // phase a delivered `source="boss"`/`target="player"` instance opened. (The
    // label is sampled while each of that phase's own animations drains, so the
    // phase names are compared as a set.)
    const bossAttack = samples.filter((entry) => entry.label === 'BOSS ATTACK');
    expect(bossAttack.length).toBeGreaterThan(0);
    expect(new Set(bossAttack.map((entry) => entry.phase))).toEqual(
      new Set(['retaliation boss->player'])
    );

    // The timeline's end releases the guard and returns to the statement that the
    // client permits input.
    expect(samples[samples.length - 1].label).toBe('YOUR TURN');
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(harness.pendingTweenCount()).toBe(0);
  });

  it('never fabricates BOSS ATTACK without a delivered boss-to-player damage instance', async () => {
    const { harness, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
    ]);

    const labels: Array<string | null> = [indicatorLabel(ctx)];
    for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
      harness.flushTweens(1);
      labels.push(indicatorLabel(ctx));
    }
    settle();
    await flush();
    harness.flushTweens();
    labels.push(indicatorLabel(ctx));

    expect(labels).not.toContain('BOSS ATTACK');
    expect(playedPhases(ctx)).not.toContain('retaliation boss->player');
    // Nothing about a Boss turn is inferred from the board events either: the
    // batch named no boss damage, so no boss phase exists to present.
    expect(labels.every((label) => label === 'RESOLVING' || label === 'YOUR TURN')).toBe(true);
  });

  it('presents the delivered terminal outcome before handing the battle off', async () => {
    const { harness, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), [
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 850 },
    ]);

    // The damage phase is still a local presentation status, and nothing has been
    // handed off yet.
    expect(indicatorLabel(ctx)).toBe('RESOLVING');
    expect(harness.sceneStarted).toHaveLength(0);

    let guard = 0;
    while (harness.pendingTweenCount() > 0 && guard < 500) {
      harness.flushTweens(1);
      guard++;
    }
    settle();
    await flush();
    harness.flushTweens();

    // The delivered outcome is named on the indicator as the battle is handed
    // off — the player reads the result, not a stuck RESOLVING line.
    expect(harness.sceneStarted.map((started) => started.key)).toEqual(['ResultScene']);
    expect(indicatorLabel(ctx)).toBe('VICTORY');
    expect(indicator(ctx)?.color).toBe('#34d399');
  });

  it('presents a delivered defeat as its own terminal statement', () => {
    const { harness, ctx } = createBattle();

    harness.emitBattleEvents({
      battleId: 'battle-246',
      serverSequence: 7,
      events: [
        { type: 'DamageDealt', source: 'boss', target: 'player', amount: 90 },
        { type: 'BattleLost', outcome: 'defeat', finalBossHp: 320, finalPlayerHp: 0 },
      ],
    });

    // The Boss's delivered hit is presented as the Boss acting...
    expect(indicatorLabel(ctx)).toBe('BOSS ATTACK');
    expect(harness.sceneStarted).toHaveLength(0);

    harness.flushTweens();

    // ...and the delivered defeat is stated before the handoff.
    expect(indicatorLabel(ctx)).toBe('DEFEAT');
    expect(indicator(ctx)?.color).toBe('#f87171');
    expect(harness.sceneStarted.map((started) => started.key)).toEqual(['ResultScene']);
  });

  it('drops the indicator with the scene, mid-timeline', async () => {
    const { harness, scene, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), RESOLUTION_BATCH);

    expect(indicatorLabel(ctx)).toBe('RESOLVING');
    expect(inputLocked(scene, ctx)).toBe(true);

    harness.shutdownScene(ctx);
    settle();
    await flush();

    expect(field(ctx, 'phaseText')).toBeNull();
    expect(field(ctx, 'selectionRing')).toBeNull();
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(harness.pendingTweenCount()).toBe(0);
    expect(harness.boardCells).toHaveLength(0);
  });

  // ---------------------------------------------------------------------------
  // Presentation pacing (TASK-246 §5)
  // ---------------------------------------------------------------------------

  it('holds each phase for longer than the animations that phase starts', async () => {
    const { harness, ctx } = createBattle();
    const phaseClock = field<object>(ctx, 'phaseClock');
    const callout = field<object>(ctx, 'calloutText');

    /** The phase's own pacing tween — the hold the timeline runs on. */
    const pendingHold = () =>
      harness.pendingTweenRecords().find((tween) => tween.targets.includes(phaseClock));
    /**
     * This phase's own animation tweens: everything its hold must cover.
     *
     * The callout's hold/fade is excluded deliberately: one delivered batch is one
     * resolution, so the callout belongs to the whole timeline rather than to the
     * phase that happens to be playing when it was set — its bounded lifetime is
     * asserted separately below.
     */
    const pendingEffects = () =>
      harness.pendingTweenRecords().filter(
        (tween) => !tween.targets.includes(phaseClock) && !tween.targets.includes(callout)
      );

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), RESOLUTION_BATCH);

    const observed: Array<{ phase: string; hold: number; longestEffect: number }> = [];
    /** The callout's own hold+fade tween, captured while it is still pending. */
    let calloutTween: { readonly duration: number; readonly props: Record<string, unknown> } | undefined;
    const observe = () => {
      const phases = playedPhases(ctx);
      const hold = pendingHold();
      if (!hold) {
        return;
      }

      calloutTween =
        calloutTween ?? harness.pendingTweenRecords().find((tween) => tween.targets.includes(callout));

      observed.push({
        phase: phases[phases.length - 1] ?? '',
        hold: hold.duration,
        longestEffect: pendingEffects().reduce((longest, tween) => Math.max(longest, tween.duration), 0),
      });
    };

    observe();
    for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
      harness.flushTweens(1);
      observe();
    }
    settle();
    await flush();
    harness.flushTweens();

    expect(observed.length).toBeGreaterThan(0);

    // The first sample taken while each phase was playing is the one whose queue
    // holds that phase's whole effect set (the recorder completes tweens in
    // creation order, so later samples of the same phase have already drained
    // some of it).
    const firstPerPhase = new Map<
      string,
      { phase: string; hold: number; longestEffect: number }
    >();
    for (const entry of observed) {
      if (!firstPerPhase.has(entry.phase)) {
        firstPerPhase.set(entry.phase, entry);
      }
    }

    // Every phase outlasts the longest animation it starts, so its feedback is
    // settled before the next phase begins rather than being cut off by it.
    for (const entry of firstPerPhase.values()) {
      expect(
        entry.hold,
        `${entry.phase}'s hold must exceed its own ${entry.longestEffect} ms animation`
      ).toBeGreaterThan(entry.longestEffect);
    }

    // The Swap in particular is held longer than its own exchange tween, so the
    // exchange is seen to settle (the defect this task fixes was a hold equal to
    // its tween). BOSS-TURN PRESENTATION §4 raised the exchange's own duration
    // (220 → 300 ms) and the shared beat (60 → 80 ms), so the relationship — not
    // just the number — is what this pins: the hold is the tween plus the beat.
    const swap = firstPerPhase.get('swap 20->21');
    expect(swap?.longestEffect).toBe(300);
    expect(swap?.hold).toBeGreaterThan(300);

    // A phase that floats a number — and moves the HP panel of the side that took
    // it — is held longer than the floater lives, so a damage number cannot still
    // be on screen when the retaliation's own feedback appears. The floater's own
    // lifetime was raised with the rest of the policy (400 → 460 ms).
    const damage = firstPerPhase.get('damage player->boss');
    const retaliation = firstPerPhase.get('retaliation boss->player');
    expect(damage?.longestEffect).toBe(460);
    expect(damage?.hold).toBeGreaterThan(460);
    expect(retaliation?.longestEffect).toBe(460);
    expect(retaliation?.hold).toBeGreaterThan(460);

    // The longest effects still fit inside the phase that starts them on the
    // refill/settle path too.
    const settlePhase = firstPerPhase.get('settle');
    expect(settlePhase?.hold).toBeGreaterThan(settlePhase?.longestEffect ?? 0);

    // The callout is the one effect that belongs to the whole batch rather than to
    // a phase, so it is bounded by its own resolution instead: its hold plus fade
    // is shorter than the timeline that raised it, so it can never still be on
    // screen when the next resolution's callout arrives.
    const timelineHold = [...firstPerPhase.values()].reduce((sum, entry) => sum + entry.hold, 0);

    expect(calloutTween).toBeDefined();
    expect(Number(calloutTween?.props.delay ?? 0) + (calloutTween?.duration ?? 0)).toBeLessThan(
      timelineHold
    );
  });

  it('keeps an ordinary resolution bounded rather than applying a blanket slowdown', async () => {
    // TASK-246 §5 / R-4: the relationship is corrected, not everything slowed
    // down. The holds this task changed were 220/320/340/420 (a typical Swap with
    // one Match, the settle, and the player's damage = 1300 ms of holds); the
    // derived holds must stay under a fifth above that. The delivered batch below
    // is exactly that path.
    const { harness, ctx } = createBattle();
    const phaseClock = field<object>(ctx, 'phaseClock');

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
      { type: 'ComboChanged', combo: 2 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
    ]);

    const holds: Array<{ phase: string; hold: number }> = [];
    const record = () => {
      const phases = playedPhases(ctx);
      const phase = phases[phases.length - 1] ?? '';
      const hold = harness
        .pendingTweenRecords()
        .find((tween) => tween.targets.includes(phaseClock));

      if (hold && !holds.some((entry) => entry.phase === phase)) {
        holds.push({ phase, hold: hold.duration });
      }
    };

    record();
    for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
      harness.flushTweens(1);
      record();
    }
    settle();
    await flush();
    harness.flushTweens();

    // One hold per phase, and the whole ordinary path stays well inside 1.5x the
    // holds it replaced (1300 ms -> ~1380 ms here: swap 280, match 360, settle
    // 280, damage 460). TASK-252's delivered Combo change is carried by the Match
    // phase it belongs to rather than opening a step of its own, so the
    // correction this task inherited (TASK-246 §5 / R-4) is preserved exactly.
    expect(holds.map((entry) => entry.phase)).toEqual([
      'swap 20->21',
      'match depth=1',
      'settle',
      'damage player->boss',
    ]);

    const total = holds.reduce((sum, entry) => sum + entry.hold, 0);
    expect(total).toBeGreaterThan(1300);
    expect(total).toBeLessThan(1300 * 1.5);
  });

  it('derives a zero-duration hold for the terminal outcome phase (finding L-1)', () => {
    // Finding L-1 / TASK-246: the outcome phase hands off immediately to
    // ResultScene with no animation and no hold (0 ms). It must not be padded
    // with PHASE_SETTLE_MS (60 ms).
    const directHold = (
      BattleScene as unknown as {
        phaseHoldMs(kind: string, events: readonly unknown[]): number;
      }
    ).phaseHoldMs('outcome', []);
    expect(directHold).toBe(0);

    const { harness, ctx } = createBattle();
    const phaseClock = field<object>(ctx, 'phaseClock');

    harness.emitBattleEvents({
      battleId: 'battle-246',
      serverSequence: 99,
      events: [
        { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 850 },
      ],
    });

    // The outcome phase schedules no pacing tween on the phase clock.
    const holdTweens = harness
      .pendingTweenRecords()
      .filter((tween) => tween.targets.includes(phaseClock));
    expect(holdTweens).toHaveLength(0);
    expect(harness.sceneStarted.map((started) => started.key)).toEqual(['ResultScene']);
  });
});
describe('BattleScene — actor/action orchestration & timeline-owned readouts (TASK-252)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** The four documented Gem contract names (MATCH3_RULES.md §1.1). */
  const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];

  /** The documented orchestration states. */
  const ORCHESTRATION_STATES = [
    'idle',
    'submitted',
    'presenting',
    'presenting-submitted',
    'terminated',
  ] as const;

  /** The states in which the scene holds player input. */
  const LOCKED_STATES: ReadonlyArray<string> = [
    'submitted',
    'presenting',
    'presenting-submitted',
  ];

  /** A scene-owned presentation field, read the way the scene wrote it. */
  function field<T>(ctx: object, name: string): T {
    return (ctx as Record<string, T>)[name];
  }

  /** The scene's own input guard, evaluated against its context. */
  function inputLocked(scene: BattleScene, ctx: object): boolean {
    return (
      Object.getPrototypeOf(scene) as { isInputLocked(this: object): boolean }
    ).isInputLocked.call(ctx);
  }

  /** The phases the timeline has played, in order. */
  const playedPhases = (ctx: object): string[] => [...field<string[]>(ctx, 'presentedPhases')];

  /** One phase of the in-flight timeline, as the scene holds it. */
  interface TimelinePhaseView {
    readonly kind: string;
    readonly actor: string | null;
    readonly action: string;
    readonly detail: string;
    readonly holdMs: number;
    readonly events: ReadonlyArray<InBattleServerEvent>;
  }

  /** The phases the in-flight timeline is built from, as the scene holds them. */
  const timelinePhases = (ctx: object): TimelinePhaseView[] => [
    ...(field<TimelinePhaseView[] | null>(ctx, 'presentationTimeline') ?? []),
  ];

  /** A delivered 8 x 8 board, rotated by `shift` cells. */
  function deliveredBoard(shift = 0): RuntimeBoard {
    return {
      cells: Array.from({ length: 64 }, (_, index) => ({
        gemType: GEM_NAMES[(index + shift) % GEM_NAMES.length],
      })),
    };
  }

  function battleState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return {
      battleId: 'battle-252',
      turn: 1,
      sequence: 0,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: deliveredBoard(),
      playerState: { combo: 0, matchCount: 0 },
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
      ...overrides,
    };
  }

  /** A battle scene with a recorded, test-driven tween queue. */
  function createBattle(
    state: RuntimeBattleState | null = battleState(),
    withTweens = true
  ) {
    const harness = createSceneHarness({ battleState: state, withTweens });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    runScene(scene, ctx, 'create');
    if (withTweens && harness.pendingTweenCount() > 0) {
      harness.flushTweens(harness.pendingTweenCount());
    }
    return { harness, scene, ctx };
  }

  /** Submits one Swap whose acknowledgement is held open. */
  function submitHeldSwap(
    harness: ReturnType<typeof createSceneHarness>,
    from = 20,
    to = 21
  ): () => void {
    let settle: (() => void) | null = null;

    harness.setActionBehaviour(
      () =>
        new Promise<never>((resolve) => {
          settle = () => resolve({ accepted: true, reason: null } as never);
        })
    );

    tapCell(harness, from);
    tapCell(harness, to);

    return () => {
      harness.setActionBehaviour(null);
      settle?.();
    };
  }

  /** Holds the next submitted action's acknowledgement open until released. */
  function holdNextAction(harness: ReturnType<typeof createSceneHarness>): () => void {
    let settle: (() => void) | null = null;

    harness.setActionBehaviour(
      () =>
        new Promise<never>((resolve) => {
          settle = () => resolve({ accepted: true, reason: null } as never);
        })
    );

    return () => {
      harness.setActionBehaviour(null);
      settle?.();
    };
  }

  /** Delivers one resolution the way the server does: state push, then batch. */
  function deliverResolution(
    harness: ReturnType<typeof createSceneHarness>,
    next: RuntimeBattleState,
    events: unknown[]
  ): void {
    harness.setBattleState(next);
    harness.emitBattleEvents({
      battleId: next.battleId,
      serverSequence: next.sequence,
      events,
    });
  }

  /**
   * The delivered state one resolution commits: a moved board, both HP pairs
   * moved, the Combo and Match counters advanced, Power and the Passive pair
   * changed, a Status Effect applied, and the Boss below its Enrage threshold
   * (BOSS_RULES.md §6.1) — so every timeline-owned readout (D-2) has a new value
   * to present.
   */
  function resolvedState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return battleState({
      sequence: 1,
      board: deliveredBoard(1),
      playerState: { combo: 3, matchCount: 12 },
      petState: {
        ...battleState().petState,
        hp: 640,
        power: 40,
        passiveProgress: { threshold: 5, current: 4 },
        statusEffects: [
          { id: 'Burn', type: 'DoT', source: 'boss', magnitude: 25, remainingTurns: 2 },
        ],
      },
      bossState: { bossId: 'boss-hoa-long', hp: 1250, maxHp: 5000 },
      ...overrides,
    });
  }

  /**
   * A committed Swap that produces a cascade and a Boss response, carrying the
   * delivered events behind every readout D-2 names.
   */
  const TASK_252_BATCH: unknown[] = [
    { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
    { type: 'CascadeCreated', cascadeDepth: 1 },
    { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'DEF', cascadeDepth: 2 },
    { type: 'GemMatched', cellIndex: 32, gemType: 'DEF' },
    { type: 'CascadeCreated', cascadeDepth: 2 },
    { type: 'MatchCreated', shape: 'Lt', cells: [40, 41, 42, 48], gemType: 'HP', cascadeDepth: 3 },
    { type: 'ComboChanged', combo: 3 },
    { type: 'PowerChanged', delta: 40, power: 40, source: 'match' },
    { type: 'PassiveCharged', passiveId: 'xich-lang', source: 'pet', sourceId: 'pet-instance-1', progress: 4, threshold: 5 },
    { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
    { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
    { type: 'PassiveCharged', passiveId: 'boss-hoa-long-rage', source: 'boss', sourceId: 'boss-hoa-long', progress: 1, threshold: 5 },
    { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
    { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
    { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
  ];

  /** The whole timeline one `TASK_252_BATCH` resolution plays, in order. */
  const TASK_252_PHASES = [
    'swap 20->21',
    'match depth=1',
    'cascade depth=1',
    'cascade depth=2',
    'settle',
    'damage player->boss',
    'boss-skill flame-burst',
    'retaliation boss->player',
  ];

  /** Each readout D-2 puts under timeline ownership, as it is on screen. */
  function readouts(harness: ReturnType<typeof createSceneHarness>, ctx: object) {
    return {
      combo: field<{ text: string } | null>(ctx, 'comboText')?.text ?? null,
      matches: field<{ text: string } | null>(ctx, 'matchesText')?.text ?? null,
      power: field<{ text: string } | null>(ctx, 'petPowerText')?.text ?? null,
      powerGauge: field<{ fill: { width: number } } | null>(ctx, 'powerGauge')?.fill.width ?? null,
      passive: field<{ text: string } | null>(ctx, 'petPassiveProgressText')?.text ?? null,
      passiveGauge:
        field<{ fill: { width: number } } | null>(ctx, 'passiveGauge')?.fill.width ?? null,
      status: field<{ text: string } | null>(ctx, 'petStatusText')?.text ?? null,
      bossName: field<{ text: string } | null>(ctx, 'bossNameText')?.text ?? null,
      bossHp: field<{ text: string } | null>(ctx, 'bossHpText')?.text ?? null,
      petHp: field<{ text: string } | null>(ctx, 'petHpText')?.text ?? null,
      callout: field<{ text: string } | null>(ctx, 'calloutText')?.text ?? null,
      castControls: liveCastCaptions(harness),
    };
  }

  /** The pre-resolution presentation every held readout is still showing. */
  const HELD_READOUTS = {
    combo: '',
    matches: 'MATCHES 0',
    power: '0 / 100',
    powerGauge: 0,
    passive: '0 / 5 Matches (reset: Default)',
    passiveGauge: 0,
    status: 'Effects: none',
    bossName: 'Hỏa Long',
    bossHp: '5000 / 5000',
    petHp: '1000 / 1000',
    callout: '',
  };

  /** The live cast-control captions, in render order. */
  function castCaptions(harness: ReturnType<typeof createSceneHarness>): string[] {
    return harness.texts
      .map((text) => text.text)
      .filter((text) => text.startsWith('Card: ') || text.startsWith('Skill: '));
  }

  /**
   * The cast-control captions a context is currently presenting, ignoring the
   * harness's creation log's already-detached earlier passes.
   */
  function liveCastCaptions(harness: ReturnType<typeof createSceneHarness>): string[] {
    return castCaptions(harness).slice(-4);
  }

  /** Plays the whole timeline to its end. */
  function playToEnd(harness: ReturnType<typeof createSceneHarness>): void {
    for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
      harness.flushTweens(1);
    }
  }

  /**
   * The pending tweens that target one of the given objects.
   *
   * It is how "no tween the aborted timeline started is still running" is
   * asserted about *that timeline's own* targets: convergence can legitimately
   * raise a callout of its own (the Boss Enrage callout the delivered state now
   * shows), and that is not a tween the abandoned timeline started.
   */
  function pendingTweensFor(
    harness: ReturnType<typeof createSceneHarness>,
    targets: object[]
  ): Array<{ readonly duration: number; readonly targets: readonly object[] }> {
    return harness
      .pendingTweenRecords()
      .filter((tween) => tween.targets.some((target) => targets.includes(target)));
  }

  // ---------------------------------------------------------------------------
  // The delivered actor/action model
  // ---------------------------------------------------------------------------

  it('carries a delivered actor and action on every phase of a resolution', () => {
    const { harness, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), TASK_252_BATCH);

    const phases = timelinePhases(ctx);
    expect(phases.map((phase) => phase.detail)).toEqual(TASK_252_PHASES);

    // Every phase carries both members, and each actor is one of the documented
    // values — never an undefined or invented one.
    const documentedActors = ['swap', 'player', 'pet', 'boss', 'power', 'battle'];
    for (const phase of phases) {
      expect(phase.actor, `${phase.detail} must carry an actor`).not.toBeUndefined();
      if (phase.actor !== null) {
        expect(documentedActors).toContain(phase.actor);
      }
      expect(typeof phase.action).toBe('string');
      expect(phase.action.length).toBeGreaterThan(0);
    }

    // Every phase that carries delivered events reports the adapter's own step for
    // the event that opened it: the timeline's attribution and the adapter's
    // cannot disagree.
    for (const phase of phases) {
      if (phase.events.length === 0) {
        continue;
      }
      const step = adaptPresentedEvent(phase.events[0]);
      expect(step, `${phase.detail} must adapt`).not.toBeNull();
      expect(phase.actor, `${phase.detail} actor`).toBe(step!.actor);
      expect(phase.action, `${phase.detail} action`).toBe(step!.action);
      expect(phase.kind, `${phase.detail} kind`).toBe(step!.kind);
    }

    // The attribution each step reaches is the one the delivered members name: the
    // board cycle to the committed Swap, the player's hit to the player, the Boss's
    // Skill and retaliation to the Boss, and the Power mutation to its stage.
    const phaseOf = (detail: string) => phases.filter((phase) => phase.detail === detail)[0];

    expect([phaseOf('swap 20->21').actor, phaseOf('swap 20->21').action]).toEqual(['swap', 'swap']);
    expect([phaseOf('match depth=1').actor, phaseOf('match depth=1').action]).toEqual([
      'swap',
      'match',
    ]);
    expect([phaseOf('cascade depth=2').actor, phaseOf('cascade depth=2').action]).toEqual([
      'swap',
      'cascade',
    ]);
    expect([phaseOf('settle').actor, phaseOf('settle').action]).toEqual(['swap', 'settle']);
    expect(phaseOf('damage player->boss').actor).toBe('player');
    expect(phaseOf('boss-skill flame-burst').actor).toBe('boss');
    expect(phaseOf('retaliation boss->player').actor).toBe('boss');

    // The steps the board phase carries are attributed individually, so the Combo,
    // the Power mutation and the Pet's own Passive are attributed to the Swap, the
    // Power stage and the Pet respectively — even though they are presented by the
    // phase they were delivered in.
    const carried = phaseOf('cascade depth=2').events.map((event) => {
      const step = adaptPresentedEvent(event);
      return [event.type, step?.actor, step?.action];
    });
    expect(carried).toContainEqual(['ComboChanged', 'swap', 'combo']);
    expect(carried).toContainEqual(['PowerChanged', 'power', 'power']);
    expect(carried).toContainEqual(['PassiveCharged', 'pet', 'passive-charge']);

    settle();
  });

  it('never reads PowerChanged.source as a side, for any of its four stage values', () => {
    // SIGNALR_PROTOCOL.md §3.2.24 item 3: `source` answers "which stage owns the
    // Power mutation" and is NOT a `DamageParty` — `"boss"` here is not the Boss
    // party and `"card"` is not the player party.
    for (const source of ['match', 'card', 'relic', 'boss']) {
      const { harness, ctx } = createBattle();
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: [{ type: 'PowerChanged', delta: 10, power: 10, source }],
      });

      const phases = timelinePhases(ctx);
      expect(phases.map((phase) => phase.kind)).toEqual(['feedback']);
      expect(phases[0].actor, `PowerChanged(source="${source}")`).toBe('power');
      expect(phases[0].actor).not.toBe('boss');
      expect(phases[0].actor).not.toBe('player');
      harness.flushTweens();
    }
  });

  it('produces a Boss action\u2019s phases and a cast\u2019s phases through the same adapter as a Swap', () => {
    // A Boss-only resolution: its Passive trigger, its Skill cast and its
    // retaliation each reach a phase of their own.
    const boss = createBattle();
    boss.harness.emitBattleEvents({
      battleId: 'battle-252',
      serverSequence: 4,
      events: [
        { type: 'PassiveTriggered', passiveId: 'boss-hoa-long-rage', source: 'boss', sourceId: 'boss-hoa-long', progress: 5, threshold: 5 },
        { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
        { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
        { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
      ],
    });

    expect(
      timelinePhases(boss.ctx).map((phase) => [phase.kind, phase.actor, phase.action])
    ).toEqual([
      // The Boss Passive charge is delivered inside the phase that was open (a
      // readout step), and the Boss Skill cast is its own step.
      ['feedback', 'boss', 'passive-trigger'],
      ['boss-skill', 'boss', 'boss-skill'],
      ['retaliation', 'boss', 'retaliation'],
    ]);
    boss.harness.flushTweens();
    expect(playedPhases(boss.ctx)).toEqual([
      'passive boss-hoa-long-rage',
      'boss-skill flame-burst',
      'retaliation boss->player',
    ]);

    // A cast resolution: a Card cast and its Signature Skill refinement share one
    // cast phase; the player's Relic is its own step. No Swap phase exists,
    // because no Swap was committed. (Both steps hold for no time, so the batch
    // carries its own player damage to keep the timeline observable.)
    const cast = createBattle();
    cast.harness.emitBattleEvents({
      battleId: 'battle-252',
      serverSequence: 5,
      events: [
        { type: 'CardCast', cardId: 'card-inferno' },
        { type: 'PetSkillCast', cardId: 'card-inferno' },
        { type: 'RelicTriggered', relicId: 'relicinst_berserker_core' },
        { type: 'DamageDealt', source: 'player', target: 'boss', amount: 30 },
        { type: 'DamageTaken', source: 'player', target: 'boss', amount: 30 },
      ],
    });

    expect(
      timelinePhases(cast.ctx)
        .slice(0, 2)
        .map((phase) => [phase.kind, phase.actor, phase.action])
    ).toEqual([
      ['cast', 'player', 'card-cast'],
      ['relic', 'player', 'relic'],
    ]);
    cast.harness.flushTweens();
    expect(playedPhases(cast.ctx)).toEqual([
      'cast card-inferno',
      'relic relicinst_berserker_core',
      'damage player->boss',
    ]);
  });

  it('presents the committed Swap even when its acknowledgement settled before the batch arrived', async () => {
    // The acknowledgement and the resolution's batch travel on different paths
    // (SIGNALR_PROTOCOL.md §3.1). The swap phase's dependency is the committed
    // pair, not "the request happened to be in flight when the batch arrived".
    const { harness, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    settle();
    await flush();
    expect(playedPhases(ctx)).toEqual([]);

    deliverResolution(harness, resolvedState(), [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    ]);
    playToEnd(harness);

    expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1', 'settle']);
  });

  it('presents no swap movement for a rejected Swap, and none later either', async () => {
    const { harness, ctx } = createBattle();
    harness.setActionResult({ accepted: false, reason: 'NO_MATCH_FROM_SWAP' });

    tapCell(harness, 20);
    tapCell(harness, 21);
    await flush();

    // A rejected Swap emits no events (GAME_EVENTS.md §1.2), and a later
    // resolution must not present a movement the server refused.
    deliverResolution(harness, resolvedState(), [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    ]);
    playToEnd(harness);

    expect(playedPhases(ctx)).toEqual(['match depth=1', 'settle']);
  });

  // ---------------------------------------------------------------------------
  // The orchestration state machine and the lock invariants
  // ---------------------------------------------------------------------------

  it('locks input in every state except the accepting and the terminated state', () => {
    const { scene, ctx } = createBattle();

    for (const state of ORCHESTRATION_STATES) {
      (ctx as { orchestration: string }).orchestration = state;
      expect(inputLocked(scene, ctx), `isInputLocked() in "${state}"`).toBe(
        LOCKED_STATES.includes(state)
      );
    }
  });

  it('documents one closed transition table, with no transition out of the terminal state', () => {
    const table = BATTLE_ORCHESTRATION_LEGAL_TRANSITIONS;

    expect(Object.keys(table).sort()).toEqual([...ORCHESTRATION_STATES].sort());
    expect(table.terminated).toEqual([]);

    // Every listed target is a real state, and no state lists itself: a
    // same-state call is not a transition.
    for (const state of ORCHESTRATION_STATES) {
      for (const target of table[state]) {
        expect(ORCHESTRATION_STATES).toContain(target);
        expect(target).not.toBe(state);
      }
    }
  });

  it('applies no phase and no state for an older or duplicate batch', async () => {
    const { harness, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), TASK_252_BATCH);
    settle();
    await flush();
    expect(playedPhases(ctx)).toEqual(['swap 20->21']);

    const played = playedPhases(ctx);
    const held = readouts(harness, ctx);

    // An older batch — an earlier `serverSequence` than the one this scene has
    // already accepted — is recorded and presented no second time, and a duplicate
    // of the batch playing is treated the same way. Only the two sequences below
    // are delivered here: this test covers the duplicate/older rule, **not** a gap
    // in the sequence. Gap detection is the runtime's
    // (`GameRuntime.test.ts`, P0.4) and the scene's admission gate is covered by
    // the P0.4 suite's `own` tests.
    for (const serverSequence of [0, 1]) {
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence,
        events: [{ type: 'DamageDealt', source: 'boss', target: 'player', amount: 999 }],
      });

      expect(playedPhases(ctx)).toEqual(played);
      expect(readouts(harness, ctx)).toEqual(held);
    }

    // The record keeps what was received; the presentation keeps what was already
    // playing (TASK-209 §5's diagnostic boundary).
    expect((ctx as { presentedEventsLog: string[] }).presentedEventsLog).toContain(
      'DamageDealt: 999 (boss -> player)'
    );

    playToEnd(harness);
  });

  it('performs only documented transitions while driving a full resolution', async () => {
    const { harness, scene, ctx } = createBattle();
    const observed: string[] = [];
    const sample = () => observed.push((ctx as { orchestration: string }).orchestration);

    sample();
    const settle = submitHeldSwap(harness, 20, 21);
    sample();

    deliverResolution(harness, resolvedState(), TASK_252_BATCH);
    sample();
    playToEnd(harness);
    sample();

    settle();
    await flush();
    sample();

    for (let index = 1; index < observed.length; index++) {
      const from = observed[index - 1] as (typeof ORCHESTRATION_STATES)[number];
      const to = observed[index] as (typeof ORCHESTRATION_STATES)[number];
      if (from === to) {
        continue;
      }
      expect(
        BATTLE_ORCHESTRATION_LEGAL_TRANSITIONS[from],
        `${from} → ${to} must be documented`
      ).toContain(to);
    }

    // The whole journey was walked: the submission, the timeline, and back to
    // accepting input.
    expect(observed).toContain('submitted');
    expect(observed).toContain('presenting-submitted');
    expect(observed[observed.length - 1]).toBe('idle');
    expect(inputLocked(scene, ctx)).toBe(false);
  });

  it('keeps input locked for the timeline even after the acknowledgement settles', async () => {
    const { harness, scene, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), TASK_252_BATCH);
    settle();
    await flush();

    expect((ctx as { orchestration: string }).orchestration).toBe('presenting');
    expect(inputLocked(scene, ctx)).toBe(true);

    playToEnd(harness);
    expect(inputLocked(scene, ctx)).toBe(false);
    expect((ctx as { orchestration: string }).orchestration).toBe('idle');
  });

  it('keeps input locked while a request is still in flight after its timeline ended', async () => {
    const { harness, scene, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), TASK_252_BATCH);

    // The whole timeline plays before the acknowledgement settles.
    playToEnd(harness);
    expect((ctx as { orchestration: string }).orchestration).toBe('submitted');
    expect(inputLocked(scene, ctx)).toBe(true);

    settle();
    await flush();
    expect((ctx as { orchestration: string }).orchestration).toBe('idle');
    expect(inputLocked(scene, ctx)).toBe(false);
  });

  // ---------------------------------------------------------------------------
  // Timeline-owned readouts (operator decision D-2)
  // ---------------------------------------------------------------------------

  it('writes no timeline-owned readout from a raw push while a timeline plays', () => {
    const { harness, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    // The push arrives before its batch: the board, both HP pairs, the Combo and
    // Match counters, Power, the Passive pair, the Status Effect list, the cast
    // controls and the Boss pair with its Enrage label all wait for the timeline.
    harness.setBattleState(resolvedState());

    expect(readouts(harness, ctx)).toEqual({
      ...HELD_READOUTS,
      castControls: liveCastCaptions(harness),
    });

    deliverResolution(harness, resolvedState(), TASK_252_BATCH);
    expect(timelinePhases(ctx).length).toBeGreaterThan(0);

    // A repeated push for the already-delivered sequence, arriving while the
    // timeline plays, writes none of them either.
    harness.setBattleState(resolvedState());
    const held = readouts(harness, ctx);
    expect(held.combo).toBe('');
    expect(held.matches).toBe('MATCHES 0');
    expect(held.power).toBe('0 / 100');
    expect(held.powerGauge).toBe(0);
    expect(held.passive).toBe('0 / 5 Matches (reset: Default)');
    expect(held.passiveGauge).toBe(0);
    expect(held.status).toBe('Effects: none');
    expect(held.bossName).toBe('Hỏa Long');
    expect(held.bossHp).toBe('5000 / 5000');
    expect(held.petHp).toBe('1000 / 1000');
    expect(held.callout).not.toBe('BOSS ENRAGED');

    // And the cast-control set is not rebuilt while the timeline plays.
    expect(held.castControls).toEqual(liveCastCaptions(harness));

    settle();
    playToEnd(harness);
  });

  it('presents each readout from the phase that owns it, and never before', () => {
    const { harness, ctx } = createBattle();

    const before = readouts(harness, ctx);

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), TASK_252_BATCH);
    settle();

    const observed: Array<{ phase: string; readout: ReturnType<typeof readouts> }> = [];
    const observe = () =>
      observed.push({
        phase: playedPhases(ctx).slice(-1)[0] ?? '',
        readout: readouts(harness, ctx),
      });

    observe();
    for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
      harness.flushTweens(1);
      observe();
    }

    /** The first phase (in order) at which a readout stopped reading `before`. */
    const changedAt = (pick: (readout: ReturnType<typeof readouts>) => unknown): string =>
      observed.find((entry) => pick(entry.readout) !== pick(before))?.phase ?? '';

    const phases = playedPhases(ctx);
    const powerAt = changedAt((readout) => readout.power);
    const passiveAt = changedAt((readout) => readout.passive);
    const bossHpAt = changedAt((readout) => readout.bossHp);
    const bossNameAt = changedAt((readout) => readout.bossName);
    const petHpAt = changedAt((readout) => readout.petHp);
    const comboAt = changedAt((readout) => readout.combo);
    const matchesAt = changedAt((readout) => readout.matches);
    const statusAt = changedAt((readout) => readout.status);

    // Nothing is presented before the phase that owns it. The Combo, the Power
    // value and the Pet's own Passive pair are owned by the board phase the server
    // delivered them in; the Boss pair and its Enrage label by the damage phase;
    // the Pet pair by the retaliation phase.
    expect(powerAt).toBe('cascade depth=2');
    expect(passiveAt).toBe('cascade depth=2');
    expect(comboAt).toBe('cascade depth=2');
    expect(matchesAt).toBe('cascade depth=2');
    expect(phases.indexOf(bossHpAt)).toBeGreaterThanOrEqual(phases.indexOf('damage player->boss'));
    expect(phases.indexOf(petHpAt)).toBeGreaterThanOrEqual(
      phases.indexOf('retaliation boss->player')
    );
    expect(bossNameAt).toBe('damage player->boss');

    // The Boss-owned Passive charge delivered later in the same batch presents
    // nothing to this panel: the panel carries `PetState`'s own pair, so the Pet's
    // own phase is the only one that owns it.
    expect(passiveAt).not.toBe('damage player->boss');

    // The Status Effect instances have no owning delivered event — the closed
    // discriminator set contains none — so convergence at the timeline's end is
    // their presentation point, and nothing presents them early.
    expect(phases.indexOf(statusAt)).toBeGreaterThanOrEqual(phases.indexOf('settle'));

    // And every readout converges on the delivered values.
    const end = readouts(harness, ctx);
    expect(end.combo).toBe('COMBO ×3');
    expect(end.matches).toBe('MATCHES 12');
    expect(end.power).toBe('40 / 100');
    expect(end.passive).toBe('4 / 5 Matches (reset: Default)');
    expect(end.status).toContain('Burn (25, 2 turns)');
    expect(end.bossName).toBe('Hỏa Long [ENRAGED]');
    expect(end.bossHp).toBe('1250 / 5000');
    expect(end.petHp).toBe('640 / 1000');
  });

  it('presents the Boss Enrage label and callout from the damage phase, never from the push', () => {
    const { harness, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), [
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
    ]);
    settle();

    // The push named a Boss below its threshold, but the label and its callout are
    // the damage phase's to present.
    expect(field<{ text: string }>(ctx, 'bossNameText').text).toBe('Hỏa Long');
    expect(field<{ text: string }>(ctx, 'calloutText').text).not.toBe('BOSS ENRAGED');

    playToEnd(harness);

    expect(field<{ text: string }>(ctx, 'bossNameText').text).toBe('Hỏa Long [ENRAGED]');
    expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('BOSS ENRAGED');
    expect(field<{ text: string; color?: string }>(ctx, 'calloutText').color).toBe('#f87171');
  });

  it('converges every readout on the authoritative state when the timeline completes', () => {
    const { harness, ctx } = createBattle();

    // A resolution whose batch carries no event for most of the readouts: only
    // convergence can present them.
    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(
      harness,
      resolvedState({ playerState: { combo: 5, matchCount: 21 } }),
      [
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]
    );
    settle();
    playToEnd(harness);

    const end = readouts(harness, ctx);
    expect(end.combo).toBe('COMBO ×5');
    expect(end.matches).toBe('MATCHES 21');
    expect(end.power).toBe('40 / 100');
    expect(end.passive).toBe('4 / 5 Matches (reset: Default)');
    expect(end.status).toContain('Burn');
    expect(end.bossName).toBe('Hỏa Long [ENRAGED]');
    expect(end.bossHp).toBe('1250 / 5000');
    expect(end.petHp).toBe('640 / 1000');
    expect(harness.pendingTweenCount()).toBe(0);
  });

  it('converges every readout on the authoritative state when a newer state supersedes playback', async () => {
    const { harness, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), TASK_252_BATCH);
    settle();
    await flush();

    harness.flushTweens(1);
    expect(readouts(harness, ctx).bossHp).toBe('5000 / 5000');

    const newer = resolvedState({
      sequence: 2,
      playerState: { combo: 1, matchCount: 30 },
      petState: {
        ...battleState().petState,
        hp: 300,
        power: 12,
        passiveProgress: { threshold: 5, current: 2 },
        statusEffects: [],
      },
      bossState: { bossId: 'boss-hoa-long', hp: 900, maxHp: 5000 },
    });
    harness.setBattleState(newer);

    const end = readouts(harness, ctx);
    expect(end.combo).toBe('');
    expect(end.matches).toBe('MATCHES 30');
    expect(end.power).toBe('12 / 100');
    expect(end.passive).toBe('2 / 5 Matches (reset: Default)');
    expect(end.status).toBe('Effects: none');
    expect(end.bossHp).toBe('900 / 5000');
    expect(end.petHp).toBe('300 / 1000');

    // The abandoned timeline's own pacing is cancelled with it.
    expect(pendingTweensFor(harness, [field<object>(ctx, 'phaseClock')])).toEqual([]);
  });

  it('converges every readout on the authoritative state when the timeline aborts', async () => {
    const { harness, scene, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    harness.setBattleState(resolvedState());

    // A malformed batch aborts the presentation, and the delivered values
    // converge rather than being left mid-presentation.
    expect(() =>
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: [
          {
            type: 'MatchCreated',
            get shape() {
              throw new Error('Exploding getter in presentation');
            },
          },
        ],
      } as unknown as BattleEventsEnvelope)
    ).not.toThrow();

    const end = readouts(harness, ctx);
    expect(end.matches).toBe('MATCHES 12');
    expect(end.combo).toBe('COMBO ×3');
    expect(end.power).toBe('40 / 100');
    expect(end.passive).toBe('4 / 5 Matches (reset: Default)');
    expect(end.status).toContain('Burn');
    expect(end.bossName).toBe('Hỏa Long [ENRAGED]');
    expect(end.bossHp).toBe('1250 / 5000');
    expect(end.petHp).toBe('640 / 1000');

    // The request is still unanswered, so the guard stays engaged until it is.
    expect(inputLocked(scene, ctx)).toBe(true);
    settle();
    await flush();
    expect(inputLocked(scene, ctx)).toBe(false);
  });

  // ---------------------------------------------------------------------------
  // Timeline-owned transient effects and the bounded callout
  // ---------------------------------------------------------------------------

  it('cancels every effect the aborted timeline started, so none outlives it', async () => {
    const { harness, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), TASK_252_BATCH);
    settle();
    await flush();

    // Play until a phase has spawned its own transient effects (the Match's cell
    // highlights).
    for (let step = 0; step < 50 && field<object[]>(ctx, 'timelineEffects').length === 0; step++) {
      harness.flushTweens(1);
    }

    const tracked = [...field<object[]>(ctx, 'timelineEffects')];
    expect(tracked.length).toBeGreaterThan(0);
    expect(harness.pendingTweenCount()).toBeGreaterThan(0);
    const killedBefore = [...harness.killedTweenTargets];

    harness.setBattleState(resolvedState({ sequence: 2 }));

    // Every tween the abandoned timeline started — its pacing, its gem travel and
    // every transient effect a phase spawned — is cancelled with it.
    const phaseClock = field<object>(ctx, 'phaseClock');
    expect(pendingTweensFor(harness, [phaseClock, ...tracked])).toEqual([]);
    expect(harness.killedTweenTargets).toContain(phaseClock);
    for (const effect of tracked) {
      expect(harness.killedTweenTargets).toContain(effect);
    }
    expect(harness.killedTweenTargets.length).toBeGreaterThan(killedBefore.length);
    expect(field<object[]>(ctx, 'timelineEffects')).toEqual([]);
  });

  it('raises the batch callout with its own phase, and bounds it to its own timeline', async () => {
    const { harness, ctx } = createBattle();

    // A resolution whose callout lives longer than its own first phase holds.
    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    ]);
    settle();

    const callout = field<object>(ctx, 'calloutText');

    // The committed Swap's step plays first, and the batch's callout is not on
    // screen yet: it belongs to the phase that presents the delivered event it was
    // selected from, which is the Match phase (TASK-254 AF-2).
    expect(playedPhases(ctx)).toEqual(['swap 20->21']);
    expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('');

    // Play until that phase begins — the `swap` step's own animations and pacing
    // drain first.
    for (let step = 0; step < 20 && playedPhases(ctx).length < 2; step++) {
      harness.flushTweens(1);
    }
    expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1']);
    expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('MATCH');

    const calloutTween = harness
      .pendingTweenRecords()
      .find((tween) => tween.targets.includes(callout));
    expect(calloutTween).toBeDefined();
    expect(Number(calloutTween?.props.delay ?? 0) + (calloutTween?.duration ?? 0)).toBe(720);

    // Abandon the timeline while the callout is still on screen: it is cancelled
    // with the resolution that raised it rather than outliving it. (The newer
    // state leaves the Boss above its Enrage threshold, so convergence raises no
    // callout of its own on the same line.)
    harness.setBattleState(
      resolvedState({ sequence: 2, bossState: { bossId: 'boss-hoa-long', hp: 4800, maxHp: 5000 } })
    );

    expect(pendingTweensFor(harness, [callout])).toEqual([]);
    expect(harness.killedTweenTargets).toContain(callout);
    expect(field<{ alpha: number }>(ctx, 'calloutText').alpha).toBe(0);
    expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('MATCH');
    await flush();
  });

  // ---------------------------------------------------------------------------
  // Cast controls under the guard
  // ---------------------------------------------------------------------------

  it('presents the cast controls disabled while locked, and rebuilds them only outside a timeline', async () => {
    const { harness, scene, ctx } = createBattle();
    await flush();

    /** The four live cast controls' captions, with the colour each is drawn in. */
    const controls = () =>
      harness.texts
        .filter((text) => text.text.startsWith('Card: ') || text.text.startsWith('Skill: '))
        .slice(-4);

    const enabledCaptions = castCaptions(harness).length;
    expect(enabledCaptions).toBeGreaterThan(0);
    expect(controls().map((control) => control.color)).toEqual([
      '#e2e8f0',
      '#e2e8f0',
      '#e2e8f0',
      '#e2e8f0',
    ]);

    // The guard engages with a request whose answer is still outstanding, so the
    // controls are presented disabled **in place** — same captions, dimmed, and a
    // click on one submits nothing.
    const release = holdNextAction(harness);
    harness.clickOption('Card: Heal');
    expect(controls().every((control) => control.color === '#475569')).toBe(true);
    expect(castCaptions(harness).length).toBe(enabledCaptions);
    harness.clickOption('Card: Shield');
    expect(harness.actionInvocations.map((action) => action.kind)).toEqual(['CardCast']);

    // The resolution's push and batch arrive while the request is in flight.
    harness.setBattleState(resolvedState());
    harness.emitBattleEvents({
      battleId: 'battle-252',
      serverSequence: 1,
      events: TASK_252_BATCH,
    });

    // While the timeline plays, the control set is neither rebuilt nor enabled.
    expect(castCaptions(harness).length).toBe(enabledCaptions);
    expect(controls().every((control) => control.color === '#475569')).toBe(true);

    playToEnd(harness);

    // The timeline's end re-presents the set from the delivered loadout — but the
    // request is still unanswered, so it is still the disabled presentation.
    expect(castCaptions(harness).length).toBeGreaterThan(enabledCaptions);
    expect(inputLocked(scene, ctx)).toBe(true);
    expect(controls().every((control) => control.color === '#475569')).toBe(true);

    release();
    await flush();

    // The request is answered: the guard releases and the controls are presented
    // enabled again, without the set being rebuilt a second time.
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(controls().every((control) => control.color === '#e2e8f0')).toBe(true);

    harness.clickOption('Card: Shield');
    await flush();
    expect(harness.actionInvocations.map((action) => action.kind)).toEqual([
      'CardCast',
      'CardCast',
    ]);
  });

  // ---------------------------------------------------------------------------
  // Bounded diagnostics
  // ---------------------------------------------------------------------------

  it('bounds both diagnostic buffers and clears them when the battle changes', () => {
    const { harness, scene } = createBattle(battleState(), false);

    for (let sequence = 1; sequence <= 200; sequence++) {
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: sequence,
        events: [
          { type: 'ComboChanged', combo: sequence },
          { type: 'PowerChanged', delta: 1, power: 1, source: 'match' },
          { type: 'DamageDealt', source: 'player', target: 'boss', amount: 10 },
        ],
      });
    }

    expect((scene as BattleScene).getPresentedEvents().length).toBe(256);
    expect((scene as BattleScene).getPresentedPhases().length).toBe(256);

    // A push for another battle ends this one's developer record.
    harness.setBattleState(battleState({ battleId: 'battle-252-next', sequence: 400 }));
    expect((scene as BattleScene).getPresentedEvents()).toHaveLength(0);
    expect((scene as BattleScene).getPresentedPhases()).toHaveLength(0);
  });

  // ---------------------------------------------------------------------------
  // Pacing: the derived hold invariant, for every phase kind
  // ---------------------------------------------------------------------------

  it('derives a hold that covers the longest animation every phase kind starts', () => {
    const statics = BattleScene as unknown as {
      phaseHoldMs(kind: string, events: readonly unknown[]): number;
    };

    /** One representative delivered event per phase kind, with the effect it starts. */
    const cases: ReadonlyArray<{ kind: string; events: unknown[]; longestEffect: number }> = [
      { kind: 'swap', events: [], longestEffect: 220 },
      { kind: 'match', events: [{ type: 'MatchCreated', shape: 'Straight', cells: [0, 1, 2], gemType: 'ATK', cascadeDepth: 1 }], longestEffect: 300 },
      { kind: 'match', events: [{ type: 'GemMatched', cellIndex: 0, gemType: 'ATK' }], longestEffect: 260 },
      { kind: 'cascade', events: [{ type: 'CascadeCreated', cascadeDepth: 1 }], longestEffect: 0 },
      { kind: 'settle', events: [], longestEffect: 220 },
      { kind: 'damage', events: [{ type: 'DamageDealt', source: 'player', target: 'boss', amount: 1 }], longestEffect: 400 },
      { kind: 'retaliation', events: [{ type: 'DamageTaken', source: 'boss', target: 'player', amount: 1 }], longestEffect: 400 },
      // The readout / floater steps join the phase they were delivered in, so the
      // hold that receives them must grow to cover what they start.
      { kind: 'feedback', events: [{ type: 'ComboChanged', combo: 3 }], longestEffect: 0 },
      { kind: 'feedback', events: [{ type: 'PowerChanged', delta: 10, power: 10, source: 'match' }], longestEffect: 400 },
      { kind: 'feedback', events: [{ type: 'PassiveCharged', passiveId: 'p', source: 'pet', sourceId: 'i', progress: 1, threshold: 2 }], longestEffect: 250 },
      { kind: 'feedback', events: [{ type: 'PassiveCharged', passiveId: 'p', source: 'boss', sourceId: 'b', progress: 1, threshold: 2 }], longestEffect: 0 },
      { kind: 'feedback', events: [{ type: 'DamageDealt', source: 'player', target: 'third-party', amount: 1 }], longestEffect: 400 },
      { kind: 'cast', events: [{ type: 'CardCast', cardId: 'card-heal' }], longestEffect: 0 },
      { kind: 'relic', events: [{ type: 'RelicTriggered', relicId: 'r' }], longestEffect: 0 },
      // The Boss Skill step presents its own delivered cast, so its hold covers
      // that callout's lifetime (`CALLOUT_HOLD_MS` + `CALLOUT_FADE_MS`) rather than
      // being held for nothing (TASK-254 AF-1).
      { kind: 'boss-skill', events: [{ type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' }], longestEffect: 660 },
      // ...including when it carries effect-starting events of its own: the hold is
      // the longest of them, never the shortest.
      {
        kind: 'boss-skill',
        events: [
          { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
          { type: 'PowerChanged', delta: -20, power: 5, source: 'boss' },
        ],
        longestEffect: 660,
      },
      { kind: 'outcome', events: [], longestEffect: 0 },
    ];

    for (const entry of cases) {
      const hold = statics.phaseHoldMs(entry.kind, entry.events);
      expect(
        hold,
        `${entry.kind}'s hold must cover its own ${entry.longestEffect} ms effect`
      ).toBeGreaterThanOrEqual(entry.longestEffect);

      if (entry.longestEffect > 0) {
        // A phase that starts an animation is held *past* it, so the feedback is
        // settled before the next phase replaces it (TASK-246 §5).
        expect(hold, `${entry.kind} must be held past its own effect`).toBeGreaterThan(
          entry.longestEffect
        );
      }
    }

    // Every delivered-action kind is now held for a non-zero time, so no effect it
    // starts can still be animating when the next, conflicting phase begins: the
    // Boss Skill step presents its own cast and is held for it, and a cast / Relic
    // step is held for its own settle beat (TASK-254 AF-1/AF-4).
    for (const kind of ['cast', 'relic', 'boss-skill']) {
      expect(statics.phaseHoldMs(kind, []), `${kind} must not be a zero-hold step`).toBeGreaterThan(
        0
      );
    }

    // A Boss Skill step that carries its own delivered cast is held past that
    // callout's whole lifetime, and non-zero whether or not it carries one.
    expect(
      statics.phaseHoldMs('boss-skill', [
        { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
      ])
    ).toBeGreaterThan(660);

    // The one step that starts nothing at all is held for no time at all: the
    // terminal hand-off hands the battle off and is padded with nothing
    // (finding L-1).
    expect(statics.phaseHoldMs('outcome', [])).toBe(0);
  });

  it('holds the delivered cast and Relic steps for their own effects, and no longer', () => {
    // The one-delivery-shape regression the browser smoke harness's own timing
    // budgets assume: a delivered Combo, Power and Passive change is presented by
    // the phase it was delivered in, and a cast / Relic step opens its own step
    // without lengthening the resolution beyond that step's own hold.
    //
    // TASK-254 updated this expectation deliberately. The delivered-action steps
    // were previously pinned at a zero-length hold, which is exactly the overrun
    // AF-4 describes: a `cast` / `relic` / `boss-skill` phase that receives a
    // delivered `PowerChanged` or damage instance starts a floater and a gauge
    // travel and was still held for 0 ms. The hold is now derived from the phase's
    // own events for **every** kind, so a step that starts no animation is held for
    // its settle beat alone (`PRESENTATION_TIMING.beatMs` = 80 ms) and one that does
    // start an animation is held past it.
    //
    // BOSS-TURN PRESENTATION §4 raised the policy's values (floater 400 → 460,
    // gauge 250 → 260, refill 220 → 280, beat 60 → 80 ms); the relationship this
    // test pins is unchanged — each phase's hold is its own longest effect plus the
    // beat, and the Relic step opens its own beat rather than lengthening another
    // step. The four board/combat holds are now match 540 + settle 360 + damage 540
    // + retaliation 540, plus the Relic step's own 80 ms beat = 2060 ms.
    const probeBatch: unknown[] = [
      { type: 'MatchCreated', shape: 'Straight', cells: [0, 1, 2], gemType: 'ATK', cascadeDepth: 0 },
      { type: 'ComboChanged', combo: 4 },
      { type: 'PowerChanged', delta: 12, power: 12, source: 'match' },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 88 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 88 },
      { type: 'DamageDealt', source: 'boss', target: 'player', amount: 40 },
      { type: 'DamageTaken', source: 'boss', target: 'player', amount: 40 },
      { type: 'RelicTriggered', relicId: 'relicinst_berserker_core' },
      { type: 'RelicTriggered', relicId: 'relicinst_mana_crystal' },
    ];

    const { harness, ctx } = createBattle();
    harness.emitBattleEvents({ battleId: 'battle-252', serverSequence: 1, events: probeBatch });

    const phases = timelinePhases(ctx);
    expect(phases.map((phase) => [phase.detail, phase.holdMs])).toEqual([
      ['match depth=0', 540],
      ['settle', 360],
      ['damage player->boss', 540],
      ['retaliation boss->player', 540],
      ['relic relicinst_berserker_core', 80],
    ]);
    expect(phases.reduce((total, phase) => total + phase.holdMs, 0)).toBe(2060);

    harness.flushTweens();
  });

  // ---------------------------------------------------------------------------
  // Gameplay scenario (GAME_EVENTS.md §1 order)
  // ---------------------------------------------------------------------------

  it('presents a committed Swap that produces a cascade and a Boss response in delivered order', async () => {
    // Given a committed Swap that produces a cascade and a Boss response
    const { harness, scene, ctx } = createBattle();
    const settle = submitHeldSwap(harness, 20, 21);

    // When the server resolves it and delivers one batch
    deliverResolution(harness, resolvedState(), TASK_252_BATCH);

    // Then the phases and their actor/action attribution follow GAME_EVENTS.md §1
    expect(playedPhases(ctx)).toEqual(['swap 20->21']);
    expect(timelinePhases(ctx).map((phase) => phase.kind)).toEqual([
      'swap',
      'match',
      'cascade',
      'cascade',
      'settle',
      'damage',
      'boss-skill',
      'retaliation',
    ]);

    // And no readout shows the resolution's end value before its phase
    expect(readouts(harness, ctx).bossHp).toBe('5000 / 5000');
    expect(readouts(harness, ctx).bossName).toBe('Hỏa Long');
    expect(readouts(harness, ctx).combo).toBe('');
    expect(readouts(harness, ctx).power).toBe('0 / 100');
    expect(readouts(harness, ctx).matches).toBe('MATCHES 0');

    // And input stays locked until the last phase completes
    settle();
    await flush();
    expect(inputLocked(scene, ctx)).toBe(true);
    tapCell(harness, 30);
    tapCell(harness, 31);
    await flush();
    expect(harness.actionInvocations).toHaveLength(1);

    playToEnd(harness);
    expect(playedPhases(ctx)).toEqual(TASK_252_PHASES);
    expect(inputLocked(scene, ctx)).toBe(false);

    // And an abort mid-timeline cancels that timeline's own effects
    const aborted = createBattle();
    const abortSettle = submitHeldSwap(aborted.harness, 20, 21);
    deliverResolution(aborted.harness, resolvedState(), TASK_252_BATCH);
    abortSettle();
    aborted.harness.flushTweens(1);
    aborted.harness.setBattleState(resolvedState({ sequence: 2 }));
    expect(pendingTweensFor(aborted.harness, [field<object>(aborted.ctx, 'phaseClock')])).toEqual(
      []
    );
    expect(field<object[]>(aborted.ctx, 'timelineEffects')).toEqual([]);

    // And a terminal outcome still reaches ResultScene
    const terminal = createBattle();
    const terminalSettle = submitHeldSwap(terminal.harness, 20, 21);
    deliverResolution(terminal.harness, resolvedState(), [
      ...TASK_252_BATCH,
      { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 640 },
    ]);
    terminalSettle();
    expect(terminal.harness.sceneStarted).toHaveLength(0);

    playToEnd(terminal.harness);
    expect(terminal.harness.sceneStarted.map((started) => started.key)).toEqual(['ResultScene']);
  });

  it('releases the guard, the effects and the timeline on scene teardown mid-resolution', async () => {
    const { harness, scene, ctx } = createBattle();

    const settle = submitHeldSwap(harness, 20, 21);
    deliverResolution(harness, resolvedState(), TASK_252_BATCH);
    settle();
    await flush();
    harness.flushTweens(1);
    expect(inputLocked(scene, ctx)).toBe(true);

    harness.shutdownScene(ctx);

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(harness.pendingTweenCount()).toBe(0);
    expect(field<object[]>(ctx, 'timelineEffects')).toEqual([]);
    expect(harness.boardCells).toHaveLength(0);
  });

  // ---------------------------------------------------------------------------
  // TASK-254 — one action lifecycle for both actors
  // ---------------------------------------------------------------------------

  describe('TASK-254 — one action lifecycle for both actors', () => {
    /** The phase kinds of the in-flight timeline, in order. */
    const timelineKinds = (ctx: object): string[] =>
      timelinePhases(ctx).map((phase) => phase.kind);

    /** Plays the in-flight timeline until `count` phases have started. */
    function playUntil(
      harness: ReturnType<typeof createSceneHarness>,
      ctx: object,
      count: number
    ): void {
      for (let step = 0; step < 500; step++) {
        if (playedPhases(ctx).length >= count || harness.pendingTweenCount() === 0) {
          return;
        }
        harness.flushTweens(1);
      }
    }

    it('presents Player and Boss actions through one lifecycle, in the delivered order', async () => {
      const { harness, scene, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), TASK_252_BATCH);
      settle();
      await flush();

      const phases = timelinePhases(ctx);
      const kinds = phases.map((phase) => phase.kind);

      // The one lifecycle, stage by stage: the committed Swap (Action), the board
      // cycle (Execution), one step per delivered cascade pass (Cascades), the one
      // supported board transition (Board Resolution) — after the last cascade and
      // before the combat stages — then the player's hit and the Boss's own steps
      // (Combat Effects), with Recovery and Next Actor at the timeline's end.
      expect(kinds).toEqual([
        'swap',
        'match',
        'cascade',
        'cascade',
        'settle',
        'damage',
        'boss-skill',
        'retaliation',
      ]);
      expect(kinds.lastIndexOf('cascade')).toBeLessThan(kinds.indexOf('settle'));
      expect(kinds.indexOf('settle')).toBeLessThan(kinds.indexOf('damage'));

      // Every phase carried its actor and action from the **same** adapter call:
      // the Swap's steps, the player's, and the Boss's reach their phases through
      // the one delivered-event interpreter, so no phase names a party that was not
      // derived from delivered members.
      for (const phase of phases) {
        if (phase.events.length === 0) {
          continue;
        }
        const step = adaptPresentedEvent(phase.events[0]);
        expect(step?.actor, `${phase.detail} actor`).toBe(phase.actor);
        expect(step?.action, `${phase.detail} action`).toBe(phase.action);
      }

      // The Boss's response is its own delivered steps, in the delivered order
      // (GAME_RULES.md §17 steps 18a–18c): its Passive's step, its Skill, then the
      // retaliation the two lead to.
      expect(
        phases.filter((phase) => phase.actor === 'boss').map((phase) => phase.kind)
      ).toEqual(['boss-skill', 'retaliation']);

      playToEnd(harness);

      // Recovery: every readout converges on the delivered state, and only then is
      // the next actor reached.
      const end = readouts(harness, ctx);
      expect(end.bossHp).toBe('1250 / 5000');
      expect(end.bossName).toBe('Hỏa Long [ENRAGED]');
      expect(end.petHp).toBe('640 / 1000');
      expect(end.combo).toBe('COMBO ×3');
      expect(end.power).toBe('40 / 100');
      expect((ctx as { orchestration: string }).orchestration).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(harness.pendingTweenCount()).toBe(0);
    });

    it('reaches every actor through one adapter call and no per-actor branch', () => {
      const source = readFileSync(
        resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
        'utf8'
      )
        .replace(/\/\*[\s\S]*?\*\//g, '')
        .replace(/(^|[^:])\/\/.*$/gm, '$1');

      // Every call site that interprets a delivered event into an actor/action
      // uses that one adapter — the phase builder, and the DEV-only Move Log's row
      // factory (TASK-253) — so there is no second pipeline, registry or by-actor
      // branch anywhere (AGENTS.md §9).
      expect(source.match(/adaptPresentedEvent\(/g) ?? []).toHaveLength(2);

      for (const forbidden of ['byActor', 'perActor', 'actorQueue', 'actionQueue']) {
        expect(source, `BattleScene must not branch by actor via "${forbidden}"`).not.toContain(
          forbidden
        );
      }
    });

    it('raises a batch callout with the phase that owns its event, never at arrival', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      settle();
      await flush();

      harness.setBattleState(resolvedState());
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: [
          { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
          { type: 'ComboChanged', combo: 3 },
          { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
          { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
        ],
      });

      // The batch has arrived and the committed Swap's own step is playing: the
      // batch's single callout is not on screen, because the event it was selected
      // from is presented by a phase that has not started yet (TASK-254 AF-2).
      expect(playedPhases(ctx)).toEqual(['swap 20->21']);
      expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('');

      // It is raised with that phase — the Match/Cascade step that carries the
      // delivered Combo — and before the damage phase it precedes.
      playUntil(harness, ctx, 2);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1']);
      expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('COMBO ×3');
      expect(field<{ text: string; color?: string }>(ctx, 'calloutText').color).toBe('#fbbf24');

      playToEnd(harness);
      expect(playedPhases(ctx)).toContain('damage player->boss');
    });

    it('presents a Boss Skill step from its own delivered cast, before the retaliation', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      settle();
      await flush();

      harness.setBattleState(resolvedState());
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: [
          { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
          { type: 'DamageCalculated', base: 80, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 80 },
          { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
          { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
        ],
      });

      // The callout-bearing event is the Boss Skill step, and it is delivered
      // before the damage phase: the line is not raised at batch arrival.
      expect(playedPhases(ctx)).toEqual(['swap 20->21']);
      expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('');

      const phases = timelinePhases(ctx);
      const skillIndex = phases.findIndex((phase) => phase.kind === 'boss-skill');
      const retaliationIndex = phases.findIndex((phase) => phase.kind === 'retaliation');
      expect(skillIndex).toBeGreaterThan(-1);
      expect(skillIndex).toBeLessThan(retaliationIndex);

      // The step's hold is non-zero and covers the presentation it raises: the
      // callout's own hold plus its fade, and the phase's settle beat.
      expect(phases[skillIndex].holdMs).toBe(800);
      expect(phases[skillIndex].holdMs).toBeGreaterThan(0);

      // It presents its own delivered `skillId`, resolved through the one existing
      // transcription of BOSS_RULES.md §6.3/§6.4, on the Boss band's own colour.
      playUntil(harness, ctx, 2);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'boss-skill flame-burst']);
      const callout = field<{ text: string; color?: string }>(ctx, 'calloutText');
      expect(callout.text).toBe(`BOSS SKILL: ${resolveBossSkillDisplayName('flame-burst')}`);
      expect(callout.color).toBe('#f87171');
      expect(callout.text).not.toContain('flame-burst');

      playToEnd(harness);
      expect(playedPhases(ctx)).toContain('retaliation boss->player');
    });

    it('presents a Boss phase batch in delivered order, all attributed to the Boss', () => {
      const { harness, ctx } = createBattle();

      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: [
          { type: 'PassiveCharged', passiveId: 'boss-hoa-long-rage', source: 'boss', sourceId: 'boss-hoa-long', progress: 1, threshold: 5 },
          { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
          { type: 'DamageCalculated', base: 80, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 80 },
          { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
          { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
        ],
      });

      const phases = timelinePhases(ctx);

      // GAME_RULES.md §17 steps 18a–18c, as delivered: the Boss Passive's step, the
      // Boss Skill step, then the Boss's damage — all three the boss actor, through
      // the same adapter a Swap and a cast use.
      expect(phases.map((phase) => phase.detail)).toEqual([
        'passive boss-hoa-long-rage',
        'boss-skill flame-burst',
        'retaliation boss->player',
      ]);
      expect(timelineKinds(ctx)).toEqual(['feedback', 'boss-skill', 'retaliation']);
      expect(phases.map((phase) => phase.actor)).toEqual(['boss', 'boss', 'boss']);

      // The Boss Skill step's own presentation is non-zero, and the Boss-owned
      // Passive change starts no animation on this panel (its pair is the Pet's) —
      // so that step is held for the shared settle beat alone.
      expect(phases[1].holdMs).toBeGreaterThan(0);
      expect(phases[0].holdMs).toBe(80);

      playToEnd(harness);

      expect(playedPhases(ctx)).toEqual([
        'passive boss-hoa-long-rage',
        'boss-skill flame-burst',
        'retaliation boss->player',
      ]);
      // The step's own presentation is what remains on the callout line when the
      // timeline ends (the line is hidden with the timeline, and its text is the
      // record of what was said).
      expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('BOSS SKILL: Flame Burst');
      expect(harness.pendingTweenCount()).toBe(0);
    });

    it('completes a delivered-action step’s own effects inside its hold', () => {
      const { harness, scene, ctx } = createBattle();

      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: [
          { type: 'CardCast', cardId: 'card-heal' },
          { type: 'PowerChanged', delta: 25, power: 25, source: 'card' },
        ],
      });

      // The cast's own step carries the delivered Power change, so it starts the
      // Power floater and the Power gauge travel — and is held past both rather
      // than for 0 ms (AF-4).
      const cast = timelinePhases(ctx).find((phase) => phase.kind === 'cast');
      expect(cast?.holdMs).toBe(540);

      const effects = [...field<object[]>(ctx, 'timelineEffects')];
      expect(effects.length).toBeGreaterThan(0);
      expect(pendingTweensFor(harness, effects).length).toBeGreaterThan(0);

      // Drain everything the phase started except its own pacing tween: every one
      // of its effects has completed while the phase is still being held, so none
      // of them can still be animating when the next, conflicting phase begins.
      for (let step = 0; step < 20 && harness.pendingTweenCount() > 1; step++) {
        harness.flushTweens(1);
      }
      expect(pendingTweensFor(harness, effects)).toEqual([]);
      expect(pendingTweensFor(harness, [field<object>(ctx, 'phaseClock')])).toHaveLength(1);
      expect(playedPhases(ctx)).toEqual(['cast card-heal']);

      // The timeline advances only once that hold has elapsed.
      harness.flushTweens(1);
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(harness.pendingTweenCount()).toBe(0);
    });

    it('keeps input locked from submission through presentation and reconciliation', async () => {
      const { harness, scene, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      expect(inputLocked(scene, ctx)).toBe(true);
      expect(harness.actionInvocations).toHaveLength(1);

      // The acknowledgement settles first and the guard does **not** release: the
      // resolution it was accepted for has not arrived, let alone been presented
      // (TASK-254 AF-5).
      settle();
      await flush();
      expect((ctx as { orchestration: string }).orchestration).toBe('submitted');
      expect(inputLocked(scene, ctx)).toBe(true);

      // A second gesture in that window submits nothing at all — so no duplicate
      // request, and therefore no STALE_ACTION "Board has changed" rejection.
      tapCell(harness, 30);
      tapCell(harness, 31);
      await flush();
      expect(harness.actionInvocations).toHaveLength(1);
      expect(field<{ text: string }>(ctx, 'swapText').text).not.toContain('Board has changed');
      expect((ctx as { selectedCell: number | null }).selectedCell).toBeNull();

      // The resolution's own state push is held for its timeline, and the lock is
      // continuous across it.
      harness.setBattleState(resolvedState());
      expect(inputLocked(scene, ctx)).toBe(true);
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: TASK_252_BATCH,
      });
      expect(inputLocked(scene, ctx)).toBe(true);

      playToEnd(harness);

      // Only Recovery's convergence releases it — board and readouts on the
      // delivered state.
      expect((ctx as { orchestration: string }).orchestration).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(readouts(harness, ctx).bossHp).toBe('1250 / 5000');

      // And the guard really is released: the next gesture is submitted.
      tapCell(harness, 30);
      tapCell(harness, 31);
      await flush();
      expect(harness.actionInvocations).toHaveLength(2);
    });

    it('prevents a duplicate submission when the batch arrives before the acknowledgement', async () => {
      const { harness, scene, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), TASK_252_BATCH);
      settle();
      await flush();

      expect((ctx as { orchestration: string }).orchestration).toBe('presenting');
      expect(inputLocked(scene, ctx)).toBe(true);

      tapCell(harness, 30);
      tapCell(harness, 31);
      await flush();
      expect(harness.actionInvocations).toHaveLength(1);

      playToEnd(harness);
      expect(inputLocked(scene, ctx)).toBe(false);
      expect((ctx as { orchestration: string }).orchestration).toBe('idle');
    });

    it('releases a pending resolution from delivered facts, never from elapsed time', async () => {
      const { harness, scene, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      settle();
      await flush();

      // The request is answered and nothing is playing, yet the guard holds: no
      // timer, tween or clock takes part in that fact.
      expect((ctx as { orchestration: string }).orchestration).toBe('submitted');
      expect(inputLocked(scene, ctx)).toBe(true);
      expect(harness.pendingTweenCount()).toBe(0);

      // A repeat of the state this resolution is expected to resolve to keeps it
      // held...
      harness.setBattleState(resolvedState());
      expect(inputLocked(scene, ctx)).toBe(true);
      expect(readouts(harness, ctx).bossHp).toBe('5000 / 5000');

      // ...and a delivered state this resolution cannot be — a recovery snapshot or
      // any delivered version that is not `settled + 1` (ADR-008) — releases it,
      // because that is a delivery fact and not a duration.
      //
      // This is a **state-push** hold-release, not coverage for a missing event
      // batch: the batch never arrives here, and nothing rebroadcasts it. The
      // missing-batch paths (a withheld batch ahead of the delivered state, and a
      // resolution with no presentable phase) are covered by the P0.4 suite below
      // and by `GameRuntime.test.ts`'s P0.4 suite.
      harness.setBattleState(resolvedState({ sequence: 4 }));
      expect((ctx as { orchestration: string }).orchestration).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(readouts(harness, ctx).bossHp).toBe('1250 / 5000');
    });

    it('converges the delivered state before the guard advances to the next actor', async () => {
      const { harness, scene, ctx } = createBattle();

      // The order of the two facts is what "Recovery" means: the board is converged
      // while the guard is still engaged, and the machine moves to the next actor
      // only afterwards. Both are observed through the scene's own calls.
      const prototype = Object.getPrototypeOf(scene) as Record<string, unknown>;
      const realRenderBoard = prototype.renderBoard as (
        this: object,
        board: unknown,
        animate?: boolean
      ) => void;
      const observed: Array<{ animate: boolean; orchestration: string }> = [];

      prototype.renderBoard = function (this: object, board: unknown, animate = false) {
        observed.push({
          animate,
          orchestration: (this as { orchestration: string }).orchestration,
        });
        return realRenderBoard.call(this, board, animate);
      };

      try {
        const settle = submitHeldSwap(harness, 20, 21);
        deliverResolution(harness, resolvedState(), TASK_252_BATCH);
        settle();
        await flush();
        playToEnd(harness);
      } finally {
        prototype.renderBoard = realRenderBoard;
      }

      // The resolution's board was presented by its own settle phase, and the
      // timeline was playing at that moment...
      expect(observed.some((entry) => entry.animate && entry.orchestration === 'presenting')).toBe(
        true
      );

      // ...and Recovery converged it while the guard was still engaged: no
      // convergence ran with the machine already back to the accepting state.
      const convergences = observed.filter((entry) => !entry.animate);
      expect(convergences.length).toBeGreaterThan(0);
      expect(convergences.every((entry) => entry.orchestration !== 'idle')).toBe(true);

      expect((ctx as { orchestration: string }).orchestration).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('cancels a Boss Skill step’s own presentation with the timeline it belongs to', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      settle();
      await flush();

      harness.setBattleState(resolvedState());
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: [
          { type: 'BossSkillCast', skillId: 'earthquake', sourceId: 'boss-son-thach-ve' },
          { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
          { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
        ],
      });

      // Play to the Boss Skill step: its own presentation is on the callout line.
      playUntil(harness, ctx, 2);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'boss-skill earthquake']);
      const callout = field<object>(ctx, 'calloutText');
      expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('BOSS SKILL: Earthquake');
      expect(pendingTweensFor(harness, [callout])).toHaveLength(1);

      // A newer delivered state supersedes the timeline: the step's own
      // presentation is cancelled with it, exactly as the batch callout always was
      // (TASK-252 RC-3), and nothing of the abandoned timeline is left behind.
      harness.setBattleState(
        resolvedState({ sequence: 2, bossState: { bossId: 'boss-hoa-long', hp: 4800, maxHp: 5000 } })
      );

      expect(pendingTweensFor(harness, [callout])).toEqual([]);
      expect(harness.killedTweenTargets).toContain(callout);
      expect(field<{ alpha: number }>(ctx, 'calloutText').alpha).toBe(0);
      expect(field<object[]>(ctx, 'timelineEffects')).toEqual([]);
      expect(harness.pendingTweenCount()).toBe(0);
      await flush();
    });

    it('fabricates no callout, phase or selection for a rejected action', async () => {
      const { harness, scene, ctx } = createBattle();
      await flush();

      harness.setActionResult({ accepted: false, reason: 'NO_MATCH_FROM_SWAP' });
      tapCell(harness, 20);
      tapCell(harness, 21);
      await flush();

      // A rejected action emits nothing at all (GAME_EVENTS.md §1.2): no timeline,
      // no phase, and no delivery-shaped callout. The only line is the transport
      // rejection's own feedback, which is raised outside a timeline and keeps its
      // existing behaviour.
      expect(playedPhases(ctx)).toEqual([]);
      expect(timelinePhases(ctx)).toEqual([]);
      expect(field<{ text: string }>(ctx, 'calloutText').text).toBe(
        'Swap does not create a match.'
      );
      expect((scene as BattleScene).getPresentedEvents()).toEqual([]);
      expect((ctx as { selectedCell: number | null }).selectedCell).toBeNull();
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('renders no turn, sequence, transport or RNG diagnostic for a full resolution', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), TASK_252_BATCH);
      settle();
      await flush();
      playToEnd(harness);

      // Every player-facing line is a delivered battle value or the delivered
      // action's own presentation (TASK-209 §5 / TASK-210 §9 hold; the DEV-only
      // Move Log is the one authorized surface and is not part of these lines).
      const rendered = playerFacingText(harness, ctx);
      expect(rendered).not.toMatch(/seq=|turn=|serverSequence|SignalR|RNG/i);
      expect(rendered).toContain('BOSS SKILL');
    });
  });

  // ---------------------------------------------------------------------------
  // TASK-262 — the shared-board round: two board cycles, one committed board
  // ---------------------------------------------------------------------------

  describe('TASK-262 — shared-board round presentation and event handling', () => {
    /**
     * One committed round of the shared board (ADR-026 §2), exactly as the server
     * delivers it (`GAME_EVENTS.md` §1):
     *
     * ```text
     * Player phase   its board cycle, its Passive and Power, its ATK-gated instance
     * Boss phase     the Boss's own board cycle on the same board, its Passive, its
     *                non-damaging Skill, its own ATK-gated instance
     * ```
     *
     * Both board cycles carry the same event shapes, and **none** of them names the
     * actor of a `MatchCreated`, `GemMatched`, `ComboChanged` or `CascadeCreated`
     * (`SIGNALR_PROTOCOL.md` §3.2.6–§3.2.12): that is why the timeline claims no
     * party for the board cycle and only the delivered damage parties, Passives and
     * `BossSkillCast` are attributed to a side.
     *
     * **Only the Player's cycle reports a Combo.** `ComboChanged`'s payload is the
     * new value of `BattleState.Combo` (`GAME_EVENTS.md` §2,
     * `SIGNALR_PROTOCOL.md` §3.2.8 item 1), and that counter is the Player action's
     * committed Swap's (`GAME_STATE.md` §2.2 — written in the round's single
     * write-back, delivered as `playerState`; `BOSS_RULES.md` §6.2/§6.2.5 read
     * "Player Matches" / "Player Combo"), so the Boss Response phase's own cycle —
     * resolved by the same pipeline without writing either counter (step 18a) —
     * reports its `MatchCreated`/`GemMatched` items and no Combo at all.
     */
    const SHARED_ROUND: unknown[] = [
      // Player phase — §17 steps 2–9, the board cycle the committed Swap resolved.
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
      { type: 'ComboChanged', combo: 1 },
      // §17 step 10, step 11 and step 13: the Player's Passive, and the Power its
      // POWER Gems generated.
      { type: 'PassiveCharged', passiveId: 'xich-lang', source: 'pet', sourceId: 'pet-instance-1', progress: 4, threshold: 5 },
      { type: 'PowerChanged', delta: 30, power: 30, source: 'match' },
      // §17 steps 15–17: the Player's ATK-gated instance (ADR-026 D1).
      { type: 'DamageCalculated', base: 150, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 150 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
      // Boss Response Phase, §17 step 18a: the Boss's own board cycle on the same
      // board — one pass with a Match and a Cascade, and no Combo of its own.
      { type: 'MatchCreated', shape: 'Straight', cells: [3, 4, 5], gemType: 'POWER', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 3, gemType: 'POWER' },
      { type: 'CascadeCreated', cascadeDepth: 1 },
      { type: 'MatchCreated', shape: 'Straight', cells: [11, 12, 13], gemType: 'DEF', cascadeDepth: 2 },
      { type: 'GemMatched', cellIndex: 11, gemType: 'DEF' },
      // §17 steps 18b–18d: the Boss's Passive, its Skill, and its own instance.
      { type: 'PassiveCharged', passiveId: 'boss-hoa-long-rage', source: 'boss', sourceId: 'boss-hoa-long', progress: 1, threshold: 5 },
      { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
      { type: 'DamageCalculated', base: 80, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 80 },
      { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
      { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
    ];

    /**
     * The whole timeline that round plays, in the delivered order: the Player's board
     * cycle, the Player's damage step, the Boss's own board cycle, the one supported
     * board transition **at the end of the round's board cycle**, then the Boss's own
     * Passive/Skill/retaliation steps.
     */
    const SHARED_ROUND_PHASES = [
      'swap 20->21',
      'match depth=1',
      'damage player->boss',
      'match depth=1',
      'cascade depth=1',
      'settle',
      'boss-skill flame-burst',
      'retaliation boss->player',
    ];

    /**
     * The Player's Swap kills the Boss: the round is terminal inside its Player phase,
     * so no Boss Response Phase is delivered at all (`GAME_RULES.md` §17 step 18a's
     * terminal check).
     */
    const KILLING_ROUND: unknown[] = [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'DamageCalculated', base: 5000, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 5000 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 5000 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 5000 },
      { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 640 },
    ];

    /**
     * The Boss's own board cycle, its Skill and its ATK-gated instance kill the active
     * Pet: the round is terminal inside its Boss phase, and its outcome is last.
     */
    const DEFEAT_ROUND: unknown[] = [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
      { type: 'MatchCreated', shape: 'Straight', cells: [3, 4, 5], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 3, gemType: 'ATK' },
      { type: 'BossSkillCast', skillId: 'drain-power', sourceId: 'boss-thuy-ma' },
      { type: 'PowerChanged', delta: -20, power: 20, source: 'boss' },
      { type: 'DamageCalculated', base: 640, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 640 },
      { type: 'DamageDealt', source: 'boss', target: 'player', amount: 640 },
      { type: 'DamageTaken', source: 'boss', target: 'player', amount: 640 },
      { type: 'BattleLost', outcome: 'defeat', finalBossHp: 1250, finalPlayerHp: 0 },
    ];

    /** The labels one delivered board renders as, in §1.0 index order. */
    function expectedBoardLabels(shift: number): string[] {
      const labels: Readonly<Record<string, string>> = {
        ATK: 'ATK',
        DEF: 'DEF',
        HP: 'HP',
        POWER: 'PWR',
      };

      return Array.from({ length: 64 }, (_, index) =>
        labels[GEM_NAMES[(index + shift) % GEM_NAMES.length]]
      );
    }

    /**
     * The label rendered at each §1.0 cell, read from the view's own position.
     *
     * It is TASK-245's reading: a committed Swap exchanges two views' cells without
     * relabelling them, so the cell a label *shows* is the cell it sits on.
     */
    function boardLabels(harness: ReturnType<typeof createSceneHarness>): string[] {
      const labels: string[] = new Array(BOARD_COLUMNS * 8).fill('');

      for (const cell of harness.boardCells) {
        if (cell.kind !== 'label') {
          continue;
        }

        const view = cell as unknown as { x: number; y: number; label?: string };
        const column = Math.round((view.x - BOARD_ORIGIN_X - CELL_SIZE / 2) / BOARD_PITCH);
        const row = Math.round((view.y - BOARD_ORIGIN_Y - CELL_SIZE / 2) / BOARD_PITCH);

        if (row < 0 || row >= BOARD_COLUMNS || column < 0 || column >= BOARD_COLUMNS) {
          continue;
        }

        labels[row * BOARD_COLUMNS + column] = view.label ?? '';
      }

      return labels;
    }

    /** Plays the in-flight timeline until `count` phases have started. */
    function playUntil(
      harness: ReturnType<typeof createSceneHarness>,
      ctx: object,
      count: number
    ): void {
      for (let step = 0; step < 500; step++) {
        if (playedPhases(ctx).length >= count || harness.pendingTweenCount() === 0) {
          return;
        }
        harness.flushTweens(1);
      }
    }

    /** The Combo readout on screen. */
    function comboReadout(ctx: object): string {
      return field<{ text: string } | null>(ctx, 'comboText')?.text ?? '';
    }

    it('presents the round’s Player phase and Boss phase in the delivered order on one board', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), SHARED_ROUND);
      settle();
      await flush();

      const phases = timelinePhases(ctx);
      const details = phases.map((phase) => phase.detail);

      // GAME_EVENTS.md §1's own order, and nothing re-sorted, filtered or duplicated:
      // the Player's board cycle, the Player's damage step, the Boss's own board cycle
      // on the same board, then the Boss's Passive/Skill/retaliation steps.
      expect(details).toEqual(SHARED_ROUND_PHASES);

      // The round's board work does not end at the Player's combat stage: the Boss's
      // board cycle is delivered after it, so the one supported board transition is
      // placed at the end of that cycle and before the Boss's own steps.
      expect(details.indexOf('damage player->boss')).toBeLessThan(
        details.lastIndexOf('cascade depth=1')
      );
      expect(details.indexOf('settle')).toBeGreaterThan(details.lastIndexOf('cascade depth=1'));
      expect(details.indexOf('settle')).toBeLessThan(details.indexOf('boss-skill flame-burst'));

      // One timeline, played in the order it was built.
      playToEnd(harness);
      expect(playedPhases(ctx)).toEqual(SHARED_ROUND_PHASES);
    });

    it('applies the round’s committed board once, at the end of the round’s board cycle', async () => {
      const { harness, ctx } = createBattle();

      const preRound = expectedBoardLabels(0);
      const exchanged = [...preRound];
      [exchanged[20], exchanged[21]] = [exchanged[21], exchanged[20]];
      const committed = expectedBoardLabels(1);

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), SHARED_ROUND);
      settle();
      await flush();

      // Through the Player's board cycle and its damage step the displayed board is the
      // one the Player acted on — the committed round board is not on screen yet
      // (SIGNALR_PROTOCOL.md §3.1 item 2: no per-pass board is delivered, so exactly
      // one transition exists, and it has not happened).
      playUntil(harness, ctx, 3);
      const duringPlayerPhase = boardLabels(harness).join('|');
      expect([preRound.join('|'), exchanged.join('|')]).toContain(duringPlayerPhase);
      expect(duringPlayerPhase).not.toBe(committed.join('|'));

      // Exactly one board transition, and it lands after the Boss's board cycle.
      const phases = timelinePhases(ctx).map((phase) => phase.detail);
      expect(phases.filter((phase) => phase === 'settle')).toHaveLength(1);
      expect(phases.indexOf('settle')).toBeGreaterThan(phases.lastIndexOf('cascade depth=1'));

      // The round ends on the delivered committed board.
      playToEnd(harness);
      expect(boardLabels(harness)).toEqual(committed);
    });

    it('keeps the actor attribution the delivered members support and claims no other', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), SHARED_ROUND);
      settle();
      await flush();

      const phases = timelinePhases(ctx);

      // Every phase's attribution is the one adapter's answer for the event it presents
      // (TASK-252's invariant), held again for a two-cycle round.
      for (const phase of phases) {
        if (phase.events.length === 0) {
          continue;
        }
        const step = adaptPresentedEvent(phase.events[0]);
        expect(step?.actor, `${phase.detail} actor`).toBe(phase.actor);
        expect(step?.action, `${phase.detail} action`).toBe(phase.action);
      }

      const actorOf = (detail: string) =>
        phases.find((phase) => phase.detail === detail)?.actor;

      // The Boss's own delivered steps are the Boss's: its Skill cast and the
      // retaliation its ATK-gated instance opens.
      expect(actorOf('boss-skill flame-burst')).toBe('boss');
      expect(actorOf('retaliation boss->player')).toBe('boss');
      expect(actorOf('damage player->boss')).toBe('player');

      // The round's board cycle names no side in any delivered member, so neither of
      // its two cycles is attributed to one: both keep the adapter's side-free board
      // actor. Claiming the Boss for its board cycle here would invent a party no
      // delivered member names (AGENTS.md §10) — the contract decision that would make
      // it attributable is reported, not assumed.
      expect(actorOf('match depth=1')).toBe('swap');
      expect(actorOf('cascade depth=1')).toBe('swap');
      expect(actorOf('settle')).toBe('swap');

      // The same is true of a delivered board-cycle event read on its own, including
      // the Boss cycle's own Combo report.
      expect(
        adaptPresentedEvent({ type: 'ComboChanged', combo: 2 })?.actor
      ).toBe('swap');
      expect(
        adaptPresentedEvent({ type: 'MatchCreated', shape: 'Straight', cells: [3, 4, 5], gemType: 'POWER', cascadeDepth: 1 })?.actor
      ).toBe('swap');
    });

    it('presents the delivered Combo as the timeline reaches it, never the round’s committed value', async () => {
      const { harness, ctx } = createBattle();

      // The Player's own board cycle reports one Combo per Match, and the committed
      // state delivered with the batch is the round's final `Combo` — the same value
      // the last report carries (GAME_STATE.md §2.2, MATCH3_RULES.md §6.3 item 1).
      const PROGRESSING_ROUND: unknown[] = [
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
        { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
        { type: 'ComboChanged', combo: 1 },
        { type: 'CascadeCreated', cascadeDepth: 1 },
        { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'DEF', cascadeDepth: 2 },
        { type: 'GemMatched', cellIndex: 32, gemType: 'DEF' },
        { type: 'ComboChanged', combo: 2 },
        { type: 'CascadeCreated', cascadeDepth: 2 },
        { type: 'MatchCreated', shape: 'Lt', cells: [40, 41, 42, 48], gemType: 'HP', cascadeDepth: 3 },
        { type: 'GemMatched', cellIndex: 40, gemType: 'HP' },
        { type: 'ComboChanged', combo: 3 },
      ];

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), PROGRESSING_ROUND);
      settle();
      await flush();

      // The committed state the batch delivered is the round's end: Combo 3.
      expect(field<RuntimeBattleState | null>(ctx, 'currentBattleState')?.playerState).toEqual({
        combo: 3,
        matchCount: 12,
      });

      // The delivered step each phase presents, so the samples below are tied to
      // the phase that owns the value rather than to a count of phases.
      const details = timelinePhases(ctx).map((phase) => phase.detail);
      expect(details.slice(0, 4)).toEqual([
        'swap 20->21',
        'match depth=1',
        'cascade depth=1',
        'cascade depth=2',
      ]);

      // The swap step reports no Combo, so the line still holds the pre-round value.
      playUntil(harness, ctx, 1);
      expect(comboReadout(ctx)).toBe('');

      // The Match that reported Combo 1: below the line's own threshold, so the line
      // is empty — and, above all, **not** the round's committed 3. Presenting
      // `currentBattleState` here is exactly the premature read this guards
      // (MATCH3_RULES.md §6.6 item 1: the Swap's Combo reaches its final value as the
      // Swap resolves, not at its first Match).
      playUntil(harness, ctx, 2);
      expect(comboReadout(ctx)).toBe('');

      // The Match that reported Combo 2 — the value at this point of the timeline.
      playUntil(harness, ctx, 3);
      expect(comboReadout(ctx)).toBe('COMBO ×2');

      // The round's last Match, the value the committed state ends at.
      playUntil(harness, ctx, 4);
      expect(comboReadout(ctx)).toBe('COMBO ×3');

      // Convergence presents the committed authoritative state, so the final HUD is
      // the round's own result and not a leftover of the timeline.
      playToEnd(harness);
      expect(readouts(harness, ctx).combo).toBe('COMBO ×3');
      expect(readouts(harness, ctx).matches).toBe('MATCHES 12');
    });

    it('presents no Combo for a batch that reports none, and converges on the committed one', async () => {
      const { harness, ctx } = createBattle();

      // A cycle that delivers its Match and its consumed Gem but no `ComboChanged`:
      // the scene presents the reports it was sent and derives nothing from the ones
      // it was not (AGENTS.md §10, `MATCH3_RULES.md` §6.6 item 3).
      const NO_COMBO_ROUND: unknown[] = [
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
        { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
      ];

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), NO_COMBO_ROUND);
      settle();
      await flush();

      playUntil(harness, ctx, 2);

      // The committed state is `Combo` 3, but the timeline was never told 3. The line
      // is therefore not written with it: no value is inferred from the Match, from
      // the board, or from the delivered state.
      expect(comboReadout(ctx)).toBe('');

      // Convergence still presents the authoritative value, so a batch that reports
      // no Combo can never leave the HUD stale.
      playToEnd(harness);
      expect(readouts(harness, ctx).combo).toBe('COMBO ×3');
      expect(readouts(harness, ctx).matches).toBe('MATCHES 12');
    });

    it('never doubles a Combo report delivered twice inside one phase', async () => {
      const { harness, ctx } = createBattle();

      // The same report twice in the pass that earned it: the phase presents the
      // delivered value, and nothing counts, sums, or advances it — so the line reads
      // what was sent, never twice it.
      const DUPLICATED_COMBO_ROUND: unknown[] = [
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
        { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
        { type: 'ComboChanged', combo: 2 },
        { type: 'ComboChanged', combo: 2 },
        { type: 'CascadeCreated', cascadeDepth: 1 },
        { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'DEF', cascadeDepth: 2 },
        { type: 'GemMatched', cellIndex: 32, gemType: 'DEF' },
        { type: 'ComboChanged', combo: 3 },
      ];

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), DUPLICATED_COMBO_ROUND);
      settle();
      await flush();

      playUntil(harness, ctx, 2);
      expect(comboReadout(ctx)).toBe('COMBO ×2');

      playUntil(harness, ctx, 3);
      expect(comboReadout(ctx)).toBe('COMBO ×3');

      playToEnd(harness);
      expect(readouts(harness, ctx).combo).toBe('COMBO ×3');
    });

    it('presents only the Player cycle’s Combo reports and never writes one into the authoritative state', async () => {
      const { harness, scene, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), SHARED_ROUND);
      settle();
      await flush();

      // GAME_STATE.md §2.2: `BattleState.Combo` is the Player action's swap-scoped
      // counter, and the round's own delivered batch reports it once — from the
      // Player's cycle. The Boss Response phase's cycle resolved Matches of its own
      // (the fixture has two) and reports no Combo for them.
      expect((scene as BattleScene).getPresentedEvents()).toContain('Combo: 1');
      expect((scene as BattleScene).getPresentedEvents()).not.toContain('Combo: 2');

      const samples: string[] = [];
      let seen = 0;
      const record = () => {
        const played = playedPhases(ctx);
        if (played.length === seen) {
          return;
        }
        seen = played.length;
        samples.push(comboReadout(ctx));
      };

      record();
      for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
        harness.flushTweens(1);
        record();
      }

      // The Boss cycle contributes no value to the line at any point of the round:
      // the only Combo the timeline ever presented is the Player's own reported run
      // (1 — below the threshold — and then the committed 3 at convergence).
      expect(samples.every((sample) => sample === '' || sample === 'COMBO ×3')).toBe(true);
      expect(readouts(harness, ctx).combo).toBe('COMBO ×3');
      expect(readouts(harness, ctx).matches).toBe('MATCHES 12');

      // AGENTS.md §10 / GAME_STATE.md §4: the presentation writes text, never state.
      // The counters the scene would write back are the delivered ones, unchanged by
      // every Combo the timeline presented.
      expect(field<RuntimeBattleState | null>(ctx, 'currentBattleState')?.playerState).toEqual({
        combo: 3,
        matchCount: 12,
      });
    });

    it('presents an accepted resolution once and never replays an older round', async () => {
      const { harness, scene, ctx } = createBattle();

      // Round 1, played to its end.
      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), TASK_252_BATCH);
      settle();
      await flush();
      playToEnd(harness);

      const played = playedPhases(ctx);
      const recorded = (scene as BattleScene).getPresentedEvents().length;

      // A re-delivery of the resolution that just played: recorded, and presented no
      // second time — its own timeline has ended, which is exactly the case the
      // in-flight comparison alone cannot catch.
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: TASK_252_BATCH,
      });

      expect(playedPhases(ctx)).toEqual(played);
      expect(timelinePhases(ctx)).toEqual([]);
      expect((scene as BattleScene).getPresentedEvents().length).toBeGreaterThan(recorded);

      // An older resolution than one already accepted: presented no second time either
      // (SIGNALR_PROTOCOL.md §3 item 2 — `serverSequence` is strictly increasing).
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 0,
        events: [{ type: 'DamageDealt', source: 'boss', target: 'player', amount: 999 }],
      });

      expect(playedPhases(ctx)).toEqual(played);

      // A resolution the scene has not accepted is presented: the guard is a
      // duplicate/older rule, not a latch.
      deliverResolution(harness, resolvedState({ sequence: 2 }), [
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]);

      expect(playedPhases(ctx).length).toBeGreaterThan(played.length);
    });

    it('converges on a recovery snapshot and does not replay the interrupted round', async () => {
      const { harness, scene, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), SHARED_ROUND);
      settle();
      await flush();

      // Mid-presentation: the Player's board cycle has played and the round is still
      // being presented.
      playUntil(harness, ctx, 2);
      expect(timelinePhases(ctx).length).toBeGreaterThan(0);
      expect(inputLocked(scene, ctx)).toBe(true);

      // The client missed a resolution: the snapshot the server delivers on recovery
      // is two sequences ahead (ADR-008 — recovery is a snapshot, not an event replay).
      const recovered = battleState({
        sequence: 3,
        board: deliveredBoard(3),
        playerState: { combo: 5, matchCount: 20 },
      });
      harness.setBattleState(recovered);

      // The interrupted timeline is abandoned rather than left playing, the guard is
      // released, and the board and the readouts are the recovered snapshot's.
      expect(timelinePhases(ctx)).toEqual([]);
      expect((ctx as { orchestration: string }).orchestration).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(boardLabels(harness)).toEqual(expectedBoardLabels(3));
      expect(readouts(harness, ctx).combo).toBe('COMBO ×5');
      expect(readouts(harness, ctx).matches).toBe('MATCHES 20');

      // The abandoned round is not replayed afterwards: it is older than the
      // resolution the snapshot proved the server had already committed. It is still
      // recorded in the diagnostic log — that is the boundary TASK-209 §5 draws — but
      // no phase of it plays again.
      const played = playedPhases(ctx);
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: SHARED_ROUND,
      });

      expect(playedPhases(ctx)).toEqual(played);
      expect(timelinePhases(ctx)).toEqual([]);
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('locks input for the whole round and restores it when the presentation ends', async () => {
      const { harness, scene, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);

      // Submission through the round's first phase: locked.
      expect(inputLocked(scene, ctx)).toBe(true);

      deliverResolution(harness, resolvedState(), SHARED_ROUND);
      settle();
      await flush();

      // The Player phase, its combat step, the Boss phase and the Boss's own steps are
      // one committed round: input stays held across all of them (TASK-254 AF-5).
      playUntil(harness, ctx, 3);
      expect(inputLocked(scene, ctx)).toBe(true);

      // A tap during the Boss phase selects nothing and submits nothing.
      tapCell(harness, 30);
      tapCell(harness, 31);
      expect(harness.actionInvocations).toHaveLength(1);
      expect((ctx as { selectedCell: number | null }).selectedCell).toBeNull();

      playToEnd(harness);
      expect(playedPhases(ctx)).toEqual(SHARED_ROUND_PHASES);
      expect((ctx as { orchestration: string }).orchestration).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);

      // Interaction is restored: the next gesture is submitted.
      tapCell(harness, 30);
      tapCell(harness, 31);
      await flush();
      expect(harness.actionInvocations).toHaveLength(2);
    });

    it('stops presentation and gameplay input after a terminal round', async () => {
      const { harness, scene, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(
        harness,
        resolvedState({ bossState: { bossId: 'boss-hoa-long', hp: 0, maxHp: 5000 } }),
        KILLING_ROUND
      );
      settle();
      await flush();
      playToEnd(harness);

      // The outcome is the timeline's last phase and the handoff happens once: nothing
      // is presented after it, and the battle reaches `ResultScene` (GAME_EVENTS.md §1).
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'settle',
        'damage player->boss',
        'outcome victory',
      ]);
      expect(harness.sceneStarted.map((started) => started.key)).toEqual(['ResultScene']);
      expect((ctx as { orchestration: string }).orchestration).toBe('terminated');
      // `terminated` is deliberately not an input-locked state (the machine's own doc:
      // the battle is over and the scene is on its way out), so the terminal latch —
      // not the guard — is what refuses the request asserted below.
      expect(inputLocked(scene, ctx)).toBe(false);

      const played = playedPhases(ctx);
      const invocations = harness.actionInvocations.length;

      // A further delivered batch presents nothing at all, and a gesture submits
      // nothing: the battle is over (GAME_RULES.md §1.4; the `terminated` state is not
      // an input-locked state, so the terminal latch is what refuses the request).
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 2,
        events: [{ type: 'DamageDealt', source: 'boss', target: 'player', amount: 999 }],
      });
      tapCell(harness, 30);
      tapCell(harness, 31);
      await flush();

      expect(playedPhases(ctx)).toEqual(played);
      expect(timelinePhases(ctx)).toEqual([]);
      expect(harness.actionInvocations).toHaveLength(invocations);
      expect(harness.sceneStarted.map((started) => started.key)).toEqual(['ResultScene']);
    });

    it('presents a defeat round’s board update before its combat stages and its outcome last', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(
        harness,
        resolvedState({ petState: { ...resolvedState().petState, hp: 0 } }),
        DEFEAT_ROUND
      );
      settle();
      await flush();

      // The Boss's own board cycle is the round's last board work, so the board
      // converges after it; the Boss's Skill step and its retaliation follow, and the
      // delivered outcome is last.
      expect(timelinePhases(ctx).map((phase) => phase.detail)).toEqual([
        'swap 20->21',
        'match depth=1',
        'damage player->boss',
        'match depth=1',
        'settle',
        'boss-skill drain-power',
        'retaliation boss->player',
        'outcome defeat',
      ]);

      playToEnd(harness);

      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'damage player->boss',
        'match depth=1',
        'settle',
        'boss-skill drain-power',
        'retaliation boss->player',
        'outcome defeat',
      ]);
      expect(harness.sceneStarted.map((started) => started.key)).toEqual(['ResultScene']);
      expect(harness.sceneStarted[0].data).toMatchObject({ outcome: 'defeat' });
    });

    it('presents nothing for a name the discriminator set does not admit', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), [
        { type: 'TurnStarted', turn: 1 },
        { type: 'BossPhaseChanged', phase: 'boss' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
        { type: 'MatchResolved' },
        { type: 'TurnEnded' },
      ]);
      settle();
      await flush();

      // `GAME_EVENTS.md` §1 defines names `SIGNALR_PROTOCOL.md` §3.2.2's closed set does
      // not admit — a turn boundary, a Boss phase boundary and a resolved-Match report
      // among them. None is a delivered event this protocol carries, so none presents a
      // phase, claims a party, or advances the timeline: the delivered Match presents,
      // and the rest are ignored. `SwapStarted`/`SwapResolved` were on this list until
      // TASK-263 formalized them into §3.2.26–§3.2.27; they are covered by the
      // TASK-263 block below.
      expect(timelinePhases(ctx).map((phase) => phase.detail)).toEqual([
        'swap 20->21',
        'match depth=1',
        'settle',
      ]);

      playToEnd(harness);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1', 'settle']);
    });

    it('keys the accepted-resolution memory to the battle, not the counter', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState({ sequence: 5 }), TASK_252_BATCH);
      settle();
      await flush();
      playToEnd(harness);

      // A new battle starts its own `serverSequence` run: its first resolution is
      // presented even though it is numerically below the previous battle's.
      const next = battleState({ battleId: 'battle-262-next', sequence: 1, board: deliveredBoard(2) });
      harness.setBattleState(next);
      harness.emitBattleEvents({
        battleId: 'battle-262-next',
        serverSequence: 1,
        events: [
          { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
        ],
      });

      expect(timelinePhases(ctx).map((phase) => phase.detail)).not.toEqual([]);
      playToEnd(harness);
      expect(playedPhases(ctx)).toContain('match depth=1');
    });
  });

  // =========================================================================
  // TASK-263 — the delivered board-action boundary (SIGNALR_PROTOCOL.md
  // §3.2.26–§3.2.27, GAME_EVENTS.md §1/§2): the actor of each committed board
  // action comes from the wire, never from the batch's order.
  // =========================================================================

  describe('TASK-263 — actor-aware board actions on the shared board', () => {
    it('opens each board action from its delivered boundary and attributes its cycle', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
        { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
        { type: 'ComboChanged', combo: 1 },
        { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
        { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
        { type: 'SwapStarted', actor: 'boss', fromCell: 27, toCell: 28 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'DEF', cascadeDepth: 1 },
        { type: 'GemMatched', cellIndex: 33, gemType: 'DEF' },
        { type: 'ComboChanged', combo: 1 },
        { type: 'DamageTaken', source: 'boss', target: 'player', amount: 90 },
      ]);
      settle();
      await flush();

      const phases = timelinePhases(ctx);

      // Each delivered `SwapStarted` opens its own swap step, in the delivered
      // order, carrying its own delivered cells; each following Match/Gem/Combo
      // names no party of its own, so it is attributed to the boundary that opened
      // its cycle. The one board transition still sits where the round's board work
      // ends (TASK-254/262), and the terminal steps keep their own parties.
      // The player turn banner was presented when the player turn began; the round's
      // presentation timeline opens with the player swap and presents the boss banner
      // before the boss's board cycle.
      expect(phases.map((phase) => `${phase.detail}:${phase.actor}`)).toEqual([
        'swap 20->21:player',
        'match depth=1:player',
        'damage player->boss:player',
        'banner BOSS TURN:boss',
        'swap 27->28:boss',
        'match depth=1:boss',
        'settle:swap',
        'retaliation boss->player:boss',
      ]);

      // The delivered pair opened each step — not the submitted pair and not a
      // board difference: the Boss's two cells exist on the wire only through this
      // member, and the client derives no swap from a board it has already moved.
      expect(phases.filter((phase) => phase.kind === 'swap')).toHaveLength(2);
      expect(phases.filter((phase) => phase.kind === 'settle')).toHaveLength(1);
      // The timeline presents the Boss turn banner before its board cycle.
      expect(phases.filter((phase) => phase.kind === 'banner').map((phase) => phase.actor)).toEqual([
        'boss',
      ]);

      playToEnd(harness);
      expect(playedPhases(ctx)).toContain('swap 27->28');
    });

    it('takes the swap step from the delivered boundary, not from the submitted pair', async () => {
      const { harness, ctx } = createBattle();

      // The client submitted (20, 21); the committed action the server delivered
      // named (12, 13). The committed, validated pair is the one presented: the
      // delivered event is authoritative for the exchange, and no second step is
      // opened for the local pair (SIGNALR_PROTOCOL.md §3.2.26 item 8).
      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), [
        { type: 'SwapStarted', actor: 'player', fromCell: 12, toCell: 13 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]);
      settle();
      await flush();

      const details = timelinePhases(ctx).map((phase) => phase.detail);

      expect(details).toEqual(['swap 12->13', 'match depth=1', 'settle']);
      expect(details).not.toContain('swap 20->21');
    });

    it('keys the swap step to the submitted pair when the batch delivers no boundary', async () => {
      const { harness, ctx } = createBattle();

      // The pre-extension batch shape (TASK-254's fixtures): no delivered boundary,
      // so the pair this client submitted is what the step presents, and the board
      // cycle keeps the side-free actor TASK-252 gave it.
      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), TASK_252_BATCH);
      settle();
      await flush();

      const phases = timelinePhases(ctx);

      expect(phases.map((phase) => phase.detail)).toContain('swap 20->21');
      expect(phases.filter((phase) => phase.kind === 'swap').map((phase) => phase.actor)).toEqual([
        'swap',
      ]);
      expect(phases.find((phase) => phase.detail === 'match depth=1')?.actor).toBe('swap');
    });

    it('claims no side for an actor word the contract does not define', async () => {
      const { harness, ctx } = createBattle();

      // ADR-001 / AGENTS.md §10: the delivered word names the side, so a word
      // outside the contract's two values names no side — the step still presents
      // the delivered cells, and neither the step nor the cycle it opens claims a
      // party it was not told.
      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), [
        { type: 'SwapStarted', actor: 'third-party', fromCell: 3, toCell: 4 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]);
      settle();
      await flush();

      const phases = timelinePhases(ctx);

      expect(phases.find((phase) => phase.detail === 'swap 3->4')?.actor).toBeNull();
      expect(phases.find((phase) => phase.detail === 'match depth=1')?.actor).toBe('swap');
    });

    it('ignores a malformed boundary event and keeps the submitted-pair fallback', async () => {
      const { harness, ctx } = createBattle();

      // A boundary event whose required members are missing is malformed, so it
      // presents nothing: no swap step is opened from a guessed cell or actor, and
      // the batch falls back to the pair this client submitted — the same behaviour
      // a batch with no boundary at all has.
      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), [
        { type: 'SwapStarted', actor: 'boss' },
        { type: 'SwapResolved' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]);
      settle();
      await flush();

      const phases = timelinePhases(ctx);

      expect(phases.map((phase) => phase.detail)).toEqual(['swap 20->21', 'match depth=1', 'settle']);
      expect(phases.find((phase) => phase.detail === 'match depth=1')?.actor).toBe('swap');
    });
  });

  // =========================================================================
  // TASK-264 — the Boss's board action on a board the client does not hold
  // (SIGNALR_PROTOCOL.md §3.1 items 1–3, ADR-026 §2/§8): the delivered pair is
  // presented as the two §1.0 cells it names, never as a movement of the
  // contents on screen, because the board the Boss's action was validated
  // against — post-Player-cycle, and reshuffled after a deadlock — is never
  // delivered.
  // =========================================================================

  describe('TASK-264 — Boss swap choreography on the board the client holds', () => {
    /**
     * One committed shared-board round in which both sides act, each board action
     * opened by its own delivered boundary (`GAME_EVENTS.md` §1,
     * `SIGNALR_PROTOCOL.md` §3.2.26): the Player's committed Swap on the board the
     * client is showing, then the Boss's step 18a board action — whose coordinates
     * index the board its own resolution continued from, which no delivered member
     * describes (`SIGNALR_PROTOCOL.md` §3.1 item 2).
     */
    const BOTH_ACTORS_ROUND: unknown[] = [
      { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
      { type: 'SwapResolved', outcome: 'committed' },
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
      { type: 'ComboChanged', combo: 1 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
      { type: 'SwapStarted', actor: 'boss', fromCell: 27, toCell: 28 },
      { type: 'SwapResolved', outcome: 'committed' },
      { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'DEF', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 33, gemType: 'DEF' },
      { type: 'ComboChanged', combo: 1 },
      { type: 'DamageTaken', source: 'boss', target: 'player', amount: 90 },
    ];

    /**
     * The one timeline that round plays, in the delivered order: both board actions
     * in the order the wire delivered them, and the round's single committed board
     * transition where the round's board work ends (TASK-254/262).
     */
    const BOTH_ACTORS_PHASES = [
      'swap 20->21',
      'match depth=1',
      'damage player->boss',
      'banner BOSS TURN',
      'swap 27->28',
      'match depth=1',
      'settle',
      'retaliation boss->player',
    ];

    /** A round whose Boss phase ends the battle, with the Boss's own boundary. */
    const TERMINAL_BOSS_ROUND: unknown[] = [
      { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
      { type: 'SwapResolved', outcome: 'committed' },
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
      { type: 'SwapStarted', actor: 'boss', fromCell: 27, toCell: 28 },
      { type: 'SwapResolved', outcome: 'committed' },
      { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'DamageDealt', source: 'boss', target: 'player', amount: 9999 },
      { type: 'DamageTaken', source: 'boss', target: 'player', amount: 9999 },
      { type: 'BattleLost', outcome: 'defeat', finalBossHp: 1250, finalPlayerHp: 0 },
    ];

    const GEM_LABELS_BY_TYPE: Readonly<Record<string, string>> = {
      ATK: 'ATK',
      DEF: 'DEF',
      HP: 'HP',
      POWER: 'PWR',
    };

    /** The labels one delivered board renders as, in §1.0 index order. */
    function expectedBoardLabels(shift: number): string[] {
      return Array.from({ length: 64 }, (_, index) =>
        GEM_LABELS_BY_TYPE[GEM_NAMES[(index + shift) % GEM_NAMES.length]]
      );
    }

    /**
     * The label rendered at each §1.0 cell, read from the view's own position.
     *
     * It is TASK-245's reading: a committed Swap exchanges two views' cells without
     * relabelling them, so the cell a label *shows* is the cell it sits on.
     */
    function boardLabels(harness: ReturnType<typeof createSceneHarness>): string[] {
      const labels: string[] = new Array(64).fill('');

      for (const cell of harness.boardCells) {
        if (cell.kind !== 'label') {
          continue;
        }

        const view = cell as unknown as { x: number; y: number; label?: string };
        const column = Math.round((view.x - BOARD_ORIGIN_X - CELL_SIZE / 2) / BOARD_PITCH);
        const row = Math.round((view.y - BOARD_ORIGIN_Y - CELL_SIZE / 2) / BOARD_PITCH);

        if (row < 0 || row >= 8 || column < 0 || column >= 8) {
          continue;
        }

        labels[row * 8 + column] = view.label ?? '';
      }

      return labels;
    }

    /** The delivered board's own label at one §1.0 cell. */
    function deliveredLabelAt(shift: number, index: number): string {
      return expectedBoardLabels(shift)[index];
    }

    /** The board-local centre of one §1.0 cell. */
    function cellCentre(index: number): { x: number; y: number } {
      const column = index % 8;
      const row = Math.floor(index / 8);

      return {
        x: BOARD_ORIGIN_X + column * BOARD_PITCH + CELL_SIZE / 2,
        y: BOARD_ORIGIN_Y + row * BOARD_PITCH + CELL_SIZE / 2,
      };
    }

    /** The Gem views the board is rendered with: a tile and a label per cell. */
    function gemViews(harness: ReturnType<typeof createSceneHarness>): object[] {
      return harness.boardCells.filter((cell) => cell.kind === 'tile' || cell.kind === 'label');
    }

    /** Plays the timeline one tween at a time until `detail` has started. */
    function playUntilPhase(
      harness: ReturnType<typeof createSceneHarness>,
      ctx: object,
      detail: string
    ): void {
      for (let step = 0; step < 500; step++) {
        if (playedPhases(ctx).includes(detail) || harness.pendingTweenCount() === 0) {
          return;
        }
        harness.flushTweens(1);
      }
    }

    /**
     * The animations one phase started, its own pacing excluded.
     *
     * The callout's own hold/fade is excluded for the same reason the pacing test
     * excludes it: one delivered batch is one resolution, so the callout belongs to
     * the whole timeline rather than to the phase playing when it was raised.
     */
    function phaseEffects(
      harness: ReturnType<typeof createSceneHarness>,
      ctx: object
    ): Array<{ readonly duration: number; readonly targets: ReadonlyArray<object> }> {
      const phaseClock = field<object>(ctx, 'phaseClock');
      const callout = field<object>(ctx, 'calloutText');

      return harness
        .pendingTweenRecords()
        .filter(
          (tween) => !tween.targets.includes(phaseClock) && !tween.targets.includes(callout)
        );
    }

    /** Whether any animation the phase playing started moves a Gem view. */
    function movesGemViews(
      harness: ReturnType<typeof createSceneHarness>,
      ctx: object
    ): boolean {
      const views = gemViews(harness);

      return phaseEffects(harness, ctx).some((tween) =>
        tween.targets.some((target) => views.includes(target))
      );
    }

    it('presents both delivered board actions in the delivered order, with one board transition', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), BOTH_ACTORS_ROUND);
      settle();
      await flush();

      const phases = timelinePhases(ctx);

      // GAME_EVENTS.md §1's order, unchanged by this task: `SwapStarted` opens its
      // own step, its board cycle follows it, and the round's single committed board
      // transition sits where the round's board work ends. TASK-275's presentation-only
      // banner precedes each delivered boundary and stands in for no gameplay step.
      expect(phases.map((phase) => `${phase.detail}:${phase.actor}`)).toEqual([
        'swap 20->21:player',
        'match depth=1:player',
        'damage player->boss:player',
        'banner BOSS TURN:boss',
        'swap 27->28:boss',
        'match depth=1:boss',
        'settle:swap',
        'retaliation boss->player:boss',
      ]);
      expect(phases.filter((phase) => phase.kind === 'swap')).toHaveLength(2);
      expect(phases.filter((phase) => phase.kind === 'settle')).toHaveLength(1);

      playToEnd(harness);
      expect(playedPhases(ctx)).toEqual(BOTH_ACTORS_PHASES);

      // The round ends on the committed board the server delivered — the board the
      // reshuffle produced and the Boss's cycle resolved, not a locally reconstructed
      // one (SIGNALR_PROTOCOL.md §3.1 items 1–3).
      expect(boardLabels(harness)).toEqual(expectedBoardLabels(1));
    });

    it('exchanges the Player’s own pair and marks the Boss’s instead of moving stale contents', async () => {
      const { harness, ctx } = createBattle();

      const preRound = expectedBoardLabels(0);
      const exchanged = [...preRound];
      [exchanged[20], exchanged[21]] = [exchanged[21], exchanged[20]];

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), BOTH_ACTORS_ROUND);
      settle();
      await flush();

      // The Player's step: the Player's action was validated against the committed
      // board on screen, so its two cells' contents really did change places.
      playUntilPhase(harness, ctx, 'swap 20->21');
      harness.flushTweens(2);
      expect(boardLabels(harness)).toEqual(exchanged);

      // The Boss's step: the two cells it names are not cells of a board this client
      // holds, so nothing on the board moves for it.
      const beforeBossStep = boardLabels(harness).join('|');
      playUntilPhase(harness, ctx, 'swap 27->28');

      const effects = phaseEffects(harness, ctx);

      // It presents the two delivered §1.0 cells — and nothing else: no animation of
      // this step targets a Gem view, so no content is exchanged.
      expect(effects).toHaveLength(2);
      expect(movesGemViews(harness, ctx)).toBe(false);
      expect(
        effects.map((tween) => {
          const target = tween.targets[0] as { x: number; y: number };
          return { x: target.x, y: target.y };
        })
      ).toEqual([cellCentre(27), cellCentre(28)]);

      // Its own mark is shorter than the hold of the phase that started it
      // (TASK-246 §5), so the step still outlasts its presentation.
      const hold = harness
        .pendingTweenRecords()
        .find((tween) => tween.targets.includes(field<object>(ctx, 'phaseClock')));
      expect(hold).toBeDefined();
      for (const effect of effects) {
        expect(effect.duration).toBeGreaterThan(0);
        expect(effect.duration).toBeLessThan(hold?.duration ?? 0);
      }

      // Completing the step's own animations moves nothing: the board is still where
      // the Player's own exchange left it.
      harness.flushTweens(effects.length);
      expect(boardLabels(harness).join('|')).toBe(beforeBossStep);
      playToEnd(harness);
      expect(boardLabels(harness)).toEqual(expectedBoardLabels(1));
    });

    it('moves nothing for the Boss’s step when a reshuffle changed the cells’ contents', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), BOTH_ACTORS_ROUND);
      settle();
      await flush();

      // The fixture is the deadlock case the presentation has to survive: the Boss's
      // action was selected on a board whose contents at those two cells differ from
      // the contents on screen (ADR-026 §8's reshuffle; the delivered board is the
      // reshuffled one's own resolution). Nothing delivered says so, which is exactly
      // why the client may not move the contents it holds.
      const onScreen = boardLabels(harness);

      playUntilPhase(harness, ctx, 'swap 20->21');
      harness.flushTweens(2);

      const displayed = boardLabels(harness);
      expect(displayed[27]).not.toBe(deliveredLabelAt(1, 27));
      expect(displayed[28]).not.toBe(deliveredLabelAt(1, 28));

      playUntilPhase(harness, ctx, 'swap 27->28');
      const effects = phaseEffects(harness, ctx);
      expect(movesGemViews(harness, ctx)).toBe(false);

      // The delivered pair is still presented — as the two cells it names, and as no
      // Gem view's movement.
      const marked = effects.map((tween) => {
        const target = tween.targets[0] as { x: number; y: number };
        return `${target.x},${target.y}`;
      });
      expect(marked).toEqual([
        `${cellCentre(27).x},${cellCentre(27).y}`,
        `${cellCentre(28).x},${cellCentre(28).y}`,
      ]);

      // Completing them leaves the displayed contents untouched.
      harness.flushTweens(effects.length);
      expect(boardLabels(harness)).toEqual(displayed);

      // The round still converges on the delivered committed board — the reshuffled
      // one's own resolution — and on nothing the client derived.
      playToEnd(harness);
      expect(boardLabels(harness)).toEqual(expectedBoardLabels(1));

      // The board the client held is never patched towards the delivered one by this
      // step: the only write is the round's single transition.
      expect(onScreen).toEqual(expectedBoardLabels(0));
    });

    it('keeps the Player’s exchange from the delivered boundary and from the submitted-pair fallback', async () => {
      // The delivered boundary names cells other than the submitted pair: the
      // committed pair is the one presented, and it is presented as the exchange the
      // Player's own action made on the board on screen.
      const committedBoundary = createBattle();
      const settleCommitted = submitHeldSwap(committedBoundary.harness, 20, 21);
      deliverResolution(committedBoundary.harness, resolvedState(), [
        { type: 'SwapStarted', actor: 'player', fromCell: 12, toCell: 13 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]);
      settleCommitted();
      await flush();

      playUntilPhase(committedBoundary.harness, committedBoundary.ctx, 'swap 12->13');
      committedBoundary.harness.flushTweens(2);

      const committed = expectedBoardLabels(0);
      [committed[12], committed[13]] = [committed[13], committed[12]];
      expect(boardLabels(committedBoundary.harness)).toEqual(committed);

      // A batch that delivers no boundary at all (the pre-extension shape) keeps the
      // submitted pair's own exchange, exactly as before this task.
      const fallback = createBattle();
      const settleFallback = submitHeldSwap(fallback.harness, 20, 21);
      deliverResolution(fallback.harness, resolvedState(), TASK_252_BATCH);
      settleFallback();
      await flush();

      playUntilPhase(fallback.harness, fallback.ctx, 'swap 20->21');
      fallback.harness.flushTweens(2);

      const submitted = expectedBoardLabels(0);
      [submitted[20], submitted[21]] = [submitted[21], submitted[20]];
      expect(boardLabels(fallback.harness)).toEqual(submitted);
    });

    it('marks the delivered cells and claims no side for an actor word the contract does not define', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), [
        { type: 'SwapStarted', actor: 'third-party', fromCell: 3, toCell: 4 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]);
      settle();
      await flush();

      const phase = timelinePhases(ctx).find((entry) => entry.detail === 'swap 3->4');
      expect(phase?.actor).toBeNull();

      const before = boardLabels(harness).join('|');
      playUntilPhase(harness, ctx, 'swap 3->4');

      const effects = phaseEffects(harness, ctx);

      // No side is claimed, so no exchange is presented either: the two delivered
      // cells are marked, and no animation moves a Gem view.
      expect(movesGemViews(harness, ctx)).toBe(false);
      expect(effects.map((tween) => tween.targets[0])).toEqual([
        expect.objectContaining(cellCentre(3)),
        expect.objectContaining(cellCentre(4)),
      ]);
      harness.flushTweens(effects.length);
      expect(boardLabels(harness).join('|')).toBe(before);
    });

    it('cancels the Boss swap mark with the timeline it belongs to, and leaves no lock', async () => {
      const { harness, scene, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), BOTH_ACTORS_ROUND);
      settle();
      await flush();

      playUntilPhase(harness, ctx, 'swap 27->28');
      const marks = phaseEffects(harness, ctx).flatMap((tween) => [...tween.targets]);
      expect(marks).toHaveLength(2);
      expect(inputLocked(scene, ctx)).toBe(true);

      // A newer authoritative state supersedes the playing round.
      harness.setBattleState(resolvedState({ sequence: 2, board: deliveredBoard(2) }));

      // Every mark is cancelled and released with the timeline that started it, so no
      // mark can outlive the resolution it belonged to.
      for (const mark of marks) {
        expect(harness.killedTweenTargets).toContain(mark);
      }
      expect(field<object[]>(ctx, 'timelineEffects')).toEqual([]);

      // The abandoned round converges on the delivered board and releases the guard.
      expect(boardLabels(harness)).toEqual(expectedBoardLabels(2));
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(harness.pendingTweenCount()).toBe(0);
    });

    it('presents and marks nothing for a duplicate or older batch', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(harness, resolvedState(), BOTH_ACTORS_ROUND);
      settle();
      await flush();
      playToEnd(harness);

      const played = playedPhases(ctx);
      const board = boardLabels(harness);
      const logged = field<string[]>(ctx, 'presentedEventsLog').length;

      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 1,
        events: BOTH_ACTORS_ROUND,
      });
      harness.emitBattleEvents({
        battleId: 'battle-252',
        serverSequence: 0,
        events: BOTH_ACTORS_ROUND,
      });

      // Recorded, and presented no second time: no timeline, no board movement, and
      // no mark left behind.
      expect(field<string[]>(ctx, 'presentedEventsLog').length).toBeGreaterThan(logged);
      expect(playedPhases(ctx)).toEqual(played);
      expect(timelinePhases(ctx)).toEqual([]);
      expect(boardLabels(harness)).toEqual(board);
      expect(harness.pendingTweenCount()).toBe(0);
    });

    it('marks the Boss’s terminal board action and hands the outcome off last', async () => {
      const { harness, ctx } = createBattle();

      const settle = submitHeldSwap(harness, 20, 21);
      deliverResolution(
        harness,
        resolvedState({ petState: { ...resolvedState().petState, hp: 0 } }),
        TERMINAL_BOSS_ROUND
      );
      settle();
      await flush();

      expect(timelinePhases(ctx).map((phase) => phase.detail)).toEqual([
        'swap 20->21',
        'match depth=1',
        'damage player->boss',
        'banner BOSS TURN',
        'swap 27->28',
        'match depth=1',
        'settle',
        'retaliation boss->player',
        'outcome defeat',
      ]);

      // Through the Player's own step the board shows the exchange the Player's
      // action made; the Boss's step must leave exactly that board alone.
      playUntilPhase(harness, ctx, 'damage player->boss');
      const beforeBossStep = boardLabels(harness).join('|');

      playUntilPhase(harness, ctx, 'swap 27->28');
      const effects = phaseEffects(harness, ctx);
      expect(effects).toHaveLength(2);
      expect(movesGemViews(harness, ctx)).toBe(false);
      harness.flushTweens(effects.length);
      expect(boardLabels(harness).join('|')).toBe(beforeBossStep);

      playToEnd(harness);
      expect(playedPhases(ctx)[playedPhases(ctx).length - 1]).toBe('outcome defeat');
      expect(harness.sceneStarted.map((started) => started.key)).toEqual(['ResultScene']);
      expect(harness.sceneStarted[0].data).toMatchObject({ outcome: 'defeat' });
    });
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
        // The delivered `CellPayload` shape: each cell is its Gem type and nothing
        // else (`GAME_STATE.md` §2.1.3 items 1–4, `SIGNALR_PROTOCOL.md` §4.1 item 5).
        cells: Array.from({ length: 64 }, (_, index) => ({
          gemType: GEM_NAMES[index % GEM_NAMES.length],
        })),
      },
      playerState: { combo: 0, matchCount: 0 },
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
      ...overrides,
    };
  }

  function createBattle(
    battleState: RuntimeBattleState | null = serverState(),
    pets: PetResponse[] = provisionedPets()
  ) {
    const harness = createSceneHarness({ battleState, pets });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  /**
   * The four live cast-control captions, in render order.
   *
   * `renderCastControls` empties its layer and redraws on every pass (the Card
   * and Pet reads each trigger one), so the harness's `texts` log accumulates
   * every pass. The newest four are the live controls; earlier ones are already
   * detached. The uppercase callout (`CARD: …`) never collides with these
   * captions, which use the `Card: ` / `Skill: ` presentation spelling.
   */
  function castControlCaptions(harness: ReturnType<typeof createSceneHarness>): string[] {
    const captions = harness.texts
      .map((text) => text.text)
      .filter((text) => text.startsWith('Card: ') || text.startsWith('Skill: '));

    return captions.slice(-4);
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

  it('identifies the Signature Skill from the delivered Pet read, not from a Card category', async () => {
    // SIGNALR_PROTOCOL.md §4.3 item 13 / API_CONTRACTS.md §5.1: the derived entry
    // is identified from the Pet that derives it. The delivered Card collection
    // holds only the three unlocked Basic Cards, so a `Category === 'PetSkill'`
    // test over it can never succeed — and the Signature Skill is still
    // identified, with its delivered name.
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    // The Skill is presented as the Skill, with the delivered name.
    const skillOption = harness.clickables.find((c) => c.text.includes('Skill: Inferno'));
    expect(skillOption).toBeDefined();

    // The Basic cards are presented as Cards.
    const cardOption = harness.clickables.find((c) => c.text.includes('Card: Heal'));
    expect(cardOption).toBeDefined();

    // Exactly one entry is the Skill, and the raw developer id is never shown.
    const captions = castControlCaptions(harness);
    expect(captions.filter((caption) => caption.startsWith('Skill: '))).toEqual(['Skill: Inferno']);
    expect(captions).toHaveLength(4);

    // The Card collection this battle read contains no PetSkill row at all — the
    // identification cannot have come from it.
    const cards: readonly CardResponse[] = await harness.runtime.getCards();
    expect(cards.map((card) => card.cardId)).toEqual([
      'card-heal',
      'card-shield',
      'card-power-charge',
    ]);
    expect(cards.some((card) => card.category === 'PetSkill')).toBe(false);

    // And the Signature Skill's own identity is not in it: `§5.3`'s membership is
    // the unlocked set, which a derived Skill never joins (`CARD_RULES.md` §1
    // item 4, ADR-012 item 9).
    expect(cards.some((card) => card.cardId === 'card-inferno')).toBe(false);
  });

  it('resolves every one of the five provisioned Pets to its own Signature Skill', async () => {
    // API_CONTRACTS.md §5.1 + PET_RULES.md §8: all five provisioned MVP Pets
    // derive a Signature Skill, and each must identify from its own delivered
    // reference. There is no privileged Pet and no single-Pet assumption: the
    // same loop covers all five, and each case moves the Player to that Pet's
    // loadout by changing only the delivered reference.
    for (const pet of PROVISIONED_PET_SIGNATURE_SKILLS) {
      const harness = createSceneHarness({
        battleState: serverState({
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'passive-under-test',
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: [
              'card-heal',
              'card-shield',
              'card-power-charge',
              pet.cardId,
            ],
            statusEffects: [],
          },
        }),
        // Only this Pet is owned, so its reference is the only one delivered —
        // the identification has to use the datum, not a fixed card id.
        pets: [
          {
            petId: pet.petId,
            identity: pet.identity,
            element: pet.element,
            tier: 'Common',
            star: 1,
            level: 1,
            signatureSkill: { cardId: pet.cardId, name: pet.name, category: 'PetSkill' },
          },
        ],
      });
      const scene = new BattleScene();
      const ctx = harness.context(scene, 'BattleScene');
      runScene(scene, ctx, 'create');
      await flush();

      expect(castControlCaptions(harness), `Pet ${pet.identity}`).toEqual([
        'Card: Heal',
        'Card: Shield',
        'Card: Power Charge',
        `Skill: ${pet.name}`,
      ]);

      harness.clickOption(`Skill: ${pet.name}`);
      await flush();

      expect(harness.requestedActions, `Pet ${pet.identity}`).toEqual([{ kind: 'PetSkillCast' }]);
    }
  });

  it('identifies the Signature Skill with no Card collection read at all', async () => {
    // API_CONTRACTS.md §5.1 / CARD_RULES.md §1 item 4: the Signature Skill is
    // DERIVED, so it is not a member of the unlocked Card collection and the
    // client must not need that collection to identify it. Here the Card read
    // fails outright and the Skill is still identified and still dispatches
    // `PetSkillCast` — the identification is the Pet read's alone.
    const { harness, scene, ctx } = createBattle();
    harness.runtime.getCards = vi.fn(
      async () => Promise.reject(new Error('GET /api/cards unavailable'))
    ) as never;

    runScene(scene, ctx, 'create');
    await flush();

    expect(castControlCaptions(harness)).toEqual([
      // A Basic Card with no delivered name falls back to its identity — never a
      // guessed name (`CARD_RULES.md` §3).
      'Card: card-heal',
      'Card: card-shield',
      'Card: card-power-charge',
      'Skill: Inferno',
    ]);

    harness.clickOption('Skill: Inferno');
    await flush();

    expect(harness.requestedActions).toEqual([{ kind: 'PetSkillCast' }]);
  });

  it('keeps the Signature Skill identified across a reconnect/resync state push', async () => {
    // SIGNALR_PROTOCOL.md §4 / §7: the reconnect snapshot is the same projection
    // as the push and arrives through the same `onBattleState` path, so a resync
    // re-renders the cast controls from the state it was sent. The Pet-derived
    // identification is collection-sourced rather than event- or state-replayed,
    // so it survives a resync untouched by the discarded event history (§7 item
    // 2).
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    expect(castControlCaptions(harness)).toContain('Skill: Inferno');

    // The authoritative snapshot after a reconnect: a later sequence, a new board
    // and different live values, with the same loadout.
    harness.setBattleState(
      serverState({
        sequence: 41,
        turn: 9,
        petState: {
          hp: 640,
          maxHp: 1000,
          power: 100,
          passiveId: 'xich-lang',
          passiveProgress: { threshold: 5, current: 4 },
          equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          statusEffects: [],
        },
      })
    );
    await flush();

    expect(castControlCaptions(harness)).toEqual([
      'Card: Heal',
      'Card: Shield',
      'Card: Power Charge',
      'Skill: Inferno',
    ]);

    harness.clickOption('Skill: Inferno');
    await flush();

    expect(harness.requestedActions).toEqual([{ kind: 'PetSkillCast' }]);
  });

  it('hard-codes no individual Pet or Signature Skill card mapping', () => {
    // API_CONTRACTS.md §5.1 / SIGNALR_PROTOCOL.md §4.3 item 13: the identification
    // is a comparison against delivered data, so no per-Pet or per-card identity
    // may appear in the scene, and no `Category` test over the Card collection may
    // remain. Comments are stripped first, so prose that names these systems while
    // explaining the rule is not a violation — only real code is.
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    for (const hardCoded of [
      'card-inferno',
      'card-iron-fang',
      'card-tidal-barrier',
      'card-venomous-bloom',
      'card-earthshaker',
      'pet-xich-lang',
      'pet-bach-ho',
      'pet-huyen-quy',
      'pet-thanh-xa',
      'pet-son-hung',
    ]) {
      expect(source, `BattleScene must not hard-code "${hardCoded}"`).not.toContain(hardCoded);
    }

    // The Signature Skill is not identified from the Card collection's category:
    // `category` is a `CardDefinition` member the scene never reads at all, so no
    // `PetSkill`-category test over the owned Card collection can exist here. The
    // identification compares delivered identities instead.
    expect(source).not.toContain('.category');
    expect(source).not.toContain('category ===');
    expect(source).toContain('signatureSkills');
    expect(source).toMatch(/signatureSkills\.get\(/);
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

/**
 * The ten canonical MVP Relic definitions as `GET /api/relics` elements
 * (`API_CONTRACTS.md` §5.4).
 *
 * ```text
 * relicId                     name
 * relicinst_berserker_core    Berserker Core
 * relicinst_mana_crystal      Mana Crystal
 * relicinst_assassin_eye      Assassin Eye
 * relicinst_emergency_core    Emergency Core
 * relicinst_burning_curse     Burning Curse
 * relicinst_combo_fang        Combo Fang
 * relicinst_arcane_battery    Arcane Battery
 * relicinst_execution_mark    Execution Mark
 * relicinst_cascade_core      Cascade Core
 * relicinst_battle_instinct   Battle Instinct
 * ```
 *
 * The names are `RELIC_RULES.md` §6's ten provisioned Relics, and `relicId` is the
 * owned **instance** identity (§2.2 item 3) — the value `RelicTriggered.relicId`
 * and `PetState.EquippedRelics[]` carry, which is what the scene's lookup is keyed
 * by. These are the response's own members, transcribed so the resolution can be
 * asserted value by value across **all ten**; they are not a client-side content
 * source, and the scene reads only the response it is handed.
 */
const PROVISIONED_RELICS: ReadonlyArray<{ readonly relicId: string; readonly name: string }> = [
  { relicId: 'relicinst_berserker_core', name: 'Berserker Core' },
  { relicId: 'relicinst_mana_crystal', name: 'Mana Crystal' },
  { relicId: 'relicinst_assassin_eye', name: 'Assassin Eye' },
  { relicId: 'relicinst_emergency_core', name: 'Emergency Core' },
  { relicId: 'relicinst_burning_curse', name: 'Burning Curse' },
  { relicId: 'relicinst_combo_fang', name: 'Combo Fang' },
  { relicId: 'relicinst_arcane_battery', name: 'Arcane Battery' },
  { relicId: 'relicinst_execution_mark', name: 'Execution Mark' },
  { relicId: 'relicinst_cascade_core', name: 'Cascade Core' },
  { relicId: 'relicinst_battle_instinct', name: 'Battle Instinct' },
];

describe('BattleScene — Relic definition lookup (TASK-218B, API_CONTRACTS.md §5.4)', () => {
  function createBattle(
    relics: Array<{ relicId: string; name: string }> = [...PROVISIONED_RELICS],
    relicsFailure?: Error
  ) {
    const harness = createSceneHarness({ relics, ...(relicsFailure ? { relicsFailure } : {}) });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  /** The scene's own resolved name for one delivered Relic identity. */
  function resolveInScene(ctx: object, relicId: string): string | null {
    return (ctx as unknown as { relicDisplayName(id: string): string | null }).relicDisplayName(
      relicId
    );
  }

  it('resolves all ten provisioned Relics to their own delivered names', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    // The collection read goes through the runtime port (`getRelics()`), which is
    // the only Relic source this scene has — no second API service, no direct HTTP.
    expect(harness.runtime.getRelics).toHaveBeenCalledTimes(1);

    for (const relic of PROVISIONED_RELICS) {
      expect(resolveInScene(ctx, relic.relicId), relic.name).toBe(relic.name);
    }

    // The lookup is keyed by the owned instance identity and resolves nothing it
    // was not delivered: an unknown identity has no name at all (the caller shows
    // nothing rather than the raw id), and the *definition*-looking spelling is not
    // a key, which is the drift SIGNALR_PROTOCOL.md §3.2.23 item 1 guards against.
    expect(resolveInScene(ctx, 'relicinst_not_owned')).toBeNull();
    expect(resolveInScene(ctx, 'relic-berserker-core')).toBeNull();
  });

  it('loads and clears the Relic lookup across the Phaser lifecycle', async () => {
    const { harness, scene, ctx } = createBattle([
      { relicId: 'relicinst_berserker_core', name: 'Berserker Core' },
    ]);
    runScene(scene, ctx, 'create');
    await flush();

    expect(resolveInScene(ctx, 'relicinst_berserker_core')).toBe('Berserker Core');

    // The engine's own teardown path (TASK-205): a later `create()` on the same
    // instance must start from a clean slate, so the map is emptied with the other
    // per-battle caches.
    harness.shutdownScene(ctx);
    expect(resolveInScene(ctx, 'relicinst_berserker_core')).toBeNull();

    // A reused scene re-reads the collection on its next run.
    runScene(scene, ctx, 'create');
    await flush();
    expect(resolveInScene(ctx, 'relicinst_berserker_core')).toBe('Berserker Core');

    // DESTROY is the other teardown signal and clears it just the same.
    harness.destroyScene(ctx);
    expect(resolveInScene(ctx, 'relicinst_berserker_core')).toBeNull();
  });

  it('clears the Relic lookup rather than keeping the ended battle\'s definitions', async () => {
    // Reloading a battle whose delivered collection no longer contains a Relic must
    // not leave the previous run's name resolvable: the map is rebuilt, not merged.
    const { harness, scene, ctx } = createBattle([
      { relicId: 'relicinst_berserker_core', name: 'Berserker Core' },
    ]);
    runScene(scene, ctx, 'create');
    await flush();
    expect(resolveInScene(ctx, 'relicinst_berserker_core')).toBe('Berserker Core');

    harness.shutdownScene(ctx);
    harness.runtime.getRelics = vi.fn(async () => [
      { relicId: 'relicinst_mana_crystal', name: 'Mana Crystal' },
    ]) as never;
    runScene(scene, ctx, 'create');
    await flush();

    expect(resolveInScene(ctx, 'relicinst_mana_crystal')).toBe('Mana Crystal');
    expect(resolveInScene(ctx, 'relicinst_berserker_core')).toBeNull();
  });

  it('holds no Relic definition when the collection read fails, and never throws', async () => {
    const failure = new Error('GET /api/relics failed 500');
    const { harness, scene, ctx } = createBattle(
      [{ relicId: 'relicinst_berserker_core', name: 'Berserker Core' }],
      failure
    );

    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
    await flush();

    expect(harness.runtime.getRelics).toHaveBeenCalledTimes(1);
    await expect(harness.runtime.getRelics.mock.results[0]?.value).rejects.toThrow(failure);
    expect(resolveInScene(ctx, 'relicinst_berserker_core')).toBeNull();

    // The battle presentation still runs: a delivered event the scene cannot name
    // is simply not called out, and no error reaches the player.
    expect(() =>
      harness.emitBattleEvents({
        battleId: 'b-relic',
        serverSequence: 3,
        events: [{ type: 'RelicTriggered', relicId: 'relicinst_berserker_core' }],
      })
    ).not.toThrow();

    // The player-facing surface names no Relic identity it could not resolve. The
    // developer Move Log is excluded by its single declared container (TASK-253
    // D-1): the delivered `RelicTriggered` row quotes the delivered `relicId`
    // verbatim, which is exactly the developer record D-1 authorizes, and it is the
    // ONLY surface that does.
    const rendered = playerFacingText(harness, ctx);
    expect(rendered).not.toContain('RELIC:');
    expect(rendered).not.toContain('relicinst_berserker_core');
    // ...and the row that quotes it really is the developer panel's, so the
    // exclusion above is a narrowing and not a way of losing the observation.
    expect(panelText(harness, ctx)).toContain('RelicTriggered: relicinst_berserker_core');

    // A scene that previously held Relic definitions must clear them across shutdown
    // and not retain stale definitions if the subsequent battle's read rejects.
    harness.shutdownScene(ctx);
    harness.runtime.getRelics = vi.fn(async () => [
      { relicId: 'relicinst_berserker_core', name: 'Berserker Core' },
    ]) as never;
    runScene(scene, ctx, 'create');
    await flush();
    expect(resolveInScene(ctx, 'relicinst_berserker_core')).toBe('Berserker Core');

    harness.shutdownScene(ctx);
    harness.runtime.getRelics = vi.fn(async () => {
      throw failure;
    }) as never;
    expect(() => runScene(scene, ctx, 'create')).not.toThrow();
    await flush();
    expect(resolveInScene(ctx, 'relicinst_berserker_core')).toBeNull();
  });

  it('hard-codes no individual Relic identity or name', () => {
    // API_CONTRACTS.md §5.4 / RELIC_RULES.md §8.2 item 1: the name is the delivered
    // response's own member, so no per-Relic identity, no Relic name, and no
    // derivation from `trigger` / `condition` / `effectDefinition` may appear in the
    // scene. Comments are stripped first, so prose that names these systems while
    // explaining the rule is not a violation — only real code is.
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    for (const hardCoded of [
      ...PROVISIONED_RELICS.map((relic) => relic.relicId),
      ...PROVISIONED_RELICS.map((relic) => relic.name),
      'relic-berserker-core',
      'relicinst_',
      // The content members must never be read to name or explain a trigger.
      '.trigger',
      '.effectDefinition',
      'conditionType',
    ]) {
      expect(source, `BattleScene must not hard-code "${hardCoded}"`).not.toContain(hardCoded);
    }

    // It does read the delivered name through the delivered port, and it reads
    // nothing else from the response it stored.
    expect(source).toMatch(/relicDefinitions\.get\(/);
    expect(source).toMatch(/runtime\.getRelics\(/);
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
    // The delivered `finalPlayerHp` is `PetState.HP` at battle end
    // (SIGNALR_PROTOCOL.md §3.2.19, GAME_STATE.md §2.3, ADR-011): the label
    // names the Pet, and no Player HP value exists to print (TASK-211 §8).
    expect(rendered).toContain('Pet HP: 812');
    expect(rendered).not.toMatch(/Player HP/);
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
    expect(rendered).toContain('Pet HP: 0');
    expect(rendered).not.toMatch(/Player HP/);
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
          signatureSkill: {
            cardId: 'card-inferno',
            name: 'Inferno',
            category: 'PetSkill',
          },
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
  const OWNED_PET: PetResponse = {
    petId: 'pet-instance-1',
    identity: 'Xích Lang',
    element: 'Fire',
    tier: 'Common',
    star: 1,
    level: 1,
    signatureSkill: {
      cardId: 'card-inferno',
      name: 'Inferno',
      category: 'PetSkill',
    },
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
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 2 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      // The battle is over: the Boss is at 0 HP and the outcome has been
      // delivered (SIGNALR_PROTOCOL.md §3.2.19, §4.4).
      bossState: { bossId: 'boss-hoa-long', hp: 0, maxHp: 5000 },
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
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 2 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { bossId: 'boss-hoa-long', hp: bossHp, maxHp: 5000 },
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
    // new one — and the HUD holds no battle state until the new push.
    expect(harness.listeners.size).toBe(1);
    expect(harness.battleStateListeners.size).toBe(1);
    expect(harness.battleEventListeners.size).toBe(1);
    expect(
      (battleCtx as { bossHpText: { text: string } | null }).bossHpText?.text
    ).toBe('');
    expect((battleCtx as { outcomeHandled: boolean }).outcomeHandled).toBe(false);

    // Battle 2's own authoritative push arrives and repaints the HUD from it.
    harness.setBattleState(synchronizedBattle('battle-2', 5000));
    expect(
      (battleCtx as { bossHpText: { text: string } | null }).bossHpText?.text
    ).toContain('5000 / 5000');

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
        // TASK-211's per-run transition guard: it is per-run state like the
        // selection, so a stopped Lobby must not keep a claim on a transition.
        (ctx as { hasTransitioned: boolean }).hasTransitioned = true;
      },
      assertReleased: (ctx: object) => {
        expect((ctx as { selectedPetId: string | null }).selectedPetId).toBeNull();
        expect((ctx as { hasTransitioned: boolean }).hasTransitioned).toBe(false);
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
        // TASK-211's reward-block state, which only this scene's teardown
        // releases: a stopped result keeps neither a retryable failure nor a
        // rendered summary.
        (ctx as { rewardState: string }).rewardState = 'failure';
        (ctx as { rewardRetryable: boolean }).rewardRetryable = true;
        (ctx as { rewardSummary: string }).rewardSummary = 'REWARDS';
      },
      assertReleased: (ctx: object) => {
        expect((ctx as { resultData: unknown }).resultData).toBeNull();
        expect((ctx as { hasTransitioned: boolean }).hasTransitioned).toBe(false);
        expect((ctx as { rewardState: string }).rewardState).toBe('loading');
        expect((ctx as { rewardRetryable: boolean }).rewardRetryable).toBe(false);
        expect((ctx as { rewardSummary: string }).rewardSummary).toBe('');
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

describe('LobbyScene — asynchronous start attempt (TASK-211 §2)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** A deferred `startBattle` the test resolves by hand. */
  function deferredStart(): {
    readonly promise: Promise<void>;
    readonly release: () => void;
  } {
    let release: () => void = () => {};
    const promise = new Promise<void>((resolve) => {
      release = resolve;
    });
    return { promise, release };
  }

  /** The starter ownership the pre-battle flow reads (DATABASE.md §2). */
  function lobbyHarness() {
    return createSceneHarness({
      textsThrowWhenDestroyed: false,
      pets: [
        {
          petId: 'pet-instance-1',
          identity: 'Xích Lang',
          element: 'Fire',
          tier: 'Common',
          star: 1,
          level: 1,
          signatureSkill: {
            cardId: 'card-inferno',
            name: 'Inferno',
            category: 'PetSkill',
          },
        },
      ],
      relics: [
        { relicId: 'relic-instance-1', name: 'Berserker Core' },
        { relicId: 'relic-instance-2', name: 'Mana Crystal' },
        { relicId: 'relic-instance-3', name: 'Assassin Eye' },
      ],
    });
  }

  /**
   * Selects the complete documented loadout (1 Pet / 3 Basic Cards / 3 Relics /
   * 1 Boss) through the scene's own controls, so the start is reachable without
   * touching the scene's state directly.
   */
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

  it('navigates nowhere and preserves nothing when the attempt settles after SHUTDOWN', async () => {
    // The concrete risk `< BACK` makes reachable: the player leaves the Lobby
    // while `startBattle` is in flight, Phaser destroys the scene's game
    // objects, and the promise then resolves.
    const harness = lobbyHarness();
    const lobby = new LobbyScene();
    const ctx = harness.context(lobby, 'LobbyScene');

    const deferred = deferredStart();
    harness.runtime.startBattle = vi.fn(() => deferred.promise) as never;

    runScene(lobby, ctx, 'create');
    await flush();
    selectLoadout(harness);

    harness.clickControl('START BATTLE');
    expect(harness.runtime.startBattle).toHaveBeenCalledTimes(1);

    harness.shutdownScene(ctx);

    deferred.release();
    await flush();

    // No battle was entered from a scene that is over...
    expect(harness.sceneStarted).toEqual([]);
    // ...no loadout was preserved for a battle this presentation never claimed...
    expect(readPreservedLoadout(ctx as never)).toBeNull();
    // ...and no write was even attempted against the destroyed presentation.
    expect(harness.destroyedWriteAttempts).toEqual([]);
  });

  it('does not let the ended run’s attempt navigate a reused scene', async () => {
    // Scene reuse is the other half: the instance is started again, and the
    // previous run's attempt settles afterwards. The scene is live, so only the
    // run identity can stop the abandoned attempt from taking it to a battle.
    const harness = lobbyHarness();
    const lobby = new LobbyScene();
    const ctx = harness.context(lobby, 'LobbyScene');

    const deferred = deferredStart();
    harness.runtime.startBattle = vi.fn(() => deferred.promise) as never;

    runScene(lobby, ctx, 'create');
    await flush();
    selectLoadout(harness);
    harness.clickControl('START BATTLE');

    harness.shutdownScene(ctx);
    runScene(lobby, ctx, 'create');
    await flush();

    deferred.release();
    await flush();

    expect(harness.sceneStarted).toEqual([]);
    expect(readPreservedLoadout(ctx as never)).toBeNull();
    expect(harness.destroyedWriteAttempts).toEqual([]);
  });

  it('still enters the battle when the attempt settles inside the same run', async () => {
    // The guard must not break the documented behaviour it protects.
    const harness = lobbyHarness();
    const lobby = new LobbyScene();
    const ctx = harness.context(lobby, 'LobbyScene');

    const deferred = deferredStart();
    harness.runtime.startBattle = vi.fn(() => deferred.promise) as never;

    runScene(lobby, ctx, 'create');
    await flush();
    selectLoadout(harness);
    harness.clickControl('START BATTLE');

    deferred.release();
    await flush();

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
    // `D-202-03 = D`: the loadout the accepted start submitted is preserved for
    // `ResultScene → PLAY AGAIN` (ADR-022).
    expect(readPreservedLoadout(ctx as never)).toEqual({
      petId: 'pet-instance-1',
      bossId: 'boss-hoa-long',
      cardLoadout: ['card-heal', 'card-shield', 'card-power-charge'],
      relicLoadout: ['relic-instance-1', 'relic-instance-2', 'relic-instance-3'],
    });
  });

  it('BACK invalidates the attempt and returns to the main menu', async () => {
    const harness = lobbyHarness();
    const lobby = new LobbyScene();
    const ctx = harness.context(lobby, 'LobbyScene');

    const deferred = deferredStart();
    harness.runtime.startBattle = vi.fn(() => deferred.promise) as never;

    runScene(lobby, ctx, 'create');
    await flush();
    selectLoadout(harness);
    harness.clickControl('START BATTLE');

    // Leaving is reachable while the request is outstanding, and it invalidates
    // that request before asking for the transition.
    harness.clickControl('< BACK');
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);

    deferred.release();
    await flush();

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
    expect(readPreservedLoadout(ctx as never)).toBeNull();
  });

  it('issues exactly one start request however often the control is activated', async () => {
    const harness = lobbyHarness();
    const lobby = new LobbyScene();
    const ctx = harness.context(lobby, 'LobbyScene');

    const deferred = deferredStart();
    harness.runtime.startBattle = vi.fn(() => deferred.promise) as never;

    runScene(lobby, ctx, 'create');
    await flush();
    selectLoadout(harness);

    harness.clickControl('START BATTLE');
    harness.clickControl('START BATTLE');
    harness.clickControl('START BATTLE');
    await flush();

    expect(harness.runtime.startBattle).toHaveBeenCalledTimes(1);

    deferred.release();
    await flush();

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['BattleScene']);
  });
});

describe('BattleScene — developer Move Log (TASK-253, operator decision D-1)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    // The DEV gate is read through `import.meta.env.DEV`; a test that stubs it
    // must not leak the stub into the next one.
    vi.unstubAllEnvs();
  });

  const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];
  const CELL_SIZE = 56;
  const CELL_GAP = 6;
  const BOARD_WIDTH = 8 * CELL_SIZE + 7 * CELL_GAP;
  const BOARD_ORIGIN_X = (GAME_WIDTH - BOARD_WIDTH) / 2;
  const BOARD_ORIGIN_Y = SAFE_AREA.y + 96;
  /** The free band the panel declares: right of the board, inside the safe area. */
  const PANEL_LEFT = BOARD_ORIGIN_X + BOARD_WIDTH + 12;
  const PANEL_RIGHT = SAFE_AREA.x + SAFE_AREA.width - 12;
  /** The panel's documented visible-row window and retained-row cap. */
  const VISIBLE_ROWS = 18;
  const RETAINED_ROWS = 200;

  /** The sixteen non-outcome discriminators of the closed wire set (§3.2.2). */
  const CLOSED_EVENT_TYPES = [
    'SwapStarted',
    'SwapResolved',
    'MatchCreated',
    'CascadeCreated',
    'ComboChanged',
    'GemMatched',
    'DamageCalculated',
    'DamageDealt',
    'DamageTaken',
    'PassiveCharged',
    'PassiveTriggered',
    'BossSkillCast',
    'CardCast',
    'PetSkillCast',
    'RelicTriggered',
    'PowerChanged',
  ];

  function deliveredBoard(shift = 0): RuntimeBoard {
    return {
      cells: Array.from({ length: 64 }, (_, index) => ({
        gemType: GEM_NAMES[(index + shift) % GEM_NAMES.length],
      })),
    };
  }

  function battleState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return {
      battleId: 'battle-253',
      turn: 4,
      sequence: 0,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: deliveredBoard(),
      playerState: { combo: 0, matchCount: 0 },
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal'],
        statusEffects: [],
      },
      bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
      ...overrides,
    };
  }

  /** A battle scene with or without the recorded, test-driven tween queue. */
  function createBattle(withTweens = false, state: RuntimeBattleState | null = battleState()) {
    const harness = createSceneHarness({ battleState: state, withTweens });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    runScene(scene, ctx, 'create');
    return { harness, scene, ctx };
  }

  /** A scene-owned private field, read the way the scene wrote it. */
  function field<T>(ctx: object, name: string): T {
    return (ctx as Record<string, T>)[name];
  }

  /**
   * The panel's own declared text objects, in creation order.
   *
   * They are the children of the panel's **single declared container** — the
   * identity operator decision D-1 authorizes — so this is a statement about the
   * scene rather than about a naming convention this suite keeps in step by hand.
   */
  function panelTexts(harness: ReturnType<typeof createSceneHarness>, ctx: object) {
    const logIndex = moveLogContainerIndex(ctx);

    return harness.texts.filter(
      (text) => logIndex !== null && text.containerIndex === logIndex
    );
  }

  /** The panel's declared text objects' current content, in creation order. */
  function panelLines(harness: ReturnType<typeof createSceneHarness>, ctx: object): string[] {
    return panelTexts(harness, ctx).map((text) => text.text);
  }

  /** One delivered resolution carrying every non-outcome discriminator. */
  const EVERY_TYPE_BATCH: unknown[] = [
    // The committed board action's own boundary (§3.2.26–§3.2.27), which is what
    // attributes the board cycle that follows it.
    { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
    { type: 'SwapResolved', outcome: 'committed' },
    {
      type: 'MatchCreated',
      shape: 'Straight',
      cells: [24, 25, 26],
      gemType: 'ATK',
      cascadeDepth: 1,
    },
    { type: 'GemMatched', cellIndex: 25, gemType: 'ATK' },
    { type: 'CascadeCreated', cascadeDepth: 2 },
    { type: 'ComboChanged', combo: 3 },
    {
      type: 'DamageCalculated',
      base: 100,
      comboModifier: 1.2,
      elementModifier: 1,
      otherModifiers: 1,
      defense: 10,
      finalDamage: 110,
    },
    { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
    { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
    {
      type: 'PassiveCharged',
      passiveId: 'xich-lang',
      source: 'pet',
      sourceId: 'pet-instance-1',
      progress: 4,
      threshold: 5,
    },
    {
      type: 'PassiveTriggered',
      passiveId: 'boss-hoa-long-rage',
      source: 'boss',
      sourceId: 'boss-hoa-long',
      progress: 5,
      threshold: 5,
    },
    { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
    { type: 'CardCast', cardId: 'card-heal' },
    { type: 'PetSkillCast', cardId: 'card-inferno' },
    { type: 'RelicTriggered', relicId: 'relicinst_berserker_core' },
    { type: 'PowerChanged', delta: 40, power: 40, source: 'match' },
  ];

  /** Delivers one resolution the way the server does: state push, then batch. */
  function deliver(
    harness: ReturnType<typeof createSceneHarness>,
    sequence: number,
    events: unknown[],
    battleId = 'battle-253'
  ): void {
    harness.setBattleState(battleState({ battleId, sequence }));
    harness.emitBattleEvents({ battleId, serverSequence: sequence, events } as BattleEventsEnvelope);
  }

  // ---------------------------------------------------------------------------
  // Placement and DEV gating
  // ---------------------------------------------------------------------------

  it('draws the panel in the free band to the right of the board, inside the safe area', () => {
    const { harness, ctx } = createBattle();

    const panel = panelTexts(harness, ctx);
    // The caption plus one text object per visible row.
    expect(panel.length).toBe(VISIBLE_ROWS + 1);

    for (const text of panel) {
      const wrapWidth = (text.style?.wordWrap as { width?: number } | undefined)?.width ?? 0;
      // Right of the 8 x 8 board: the panel can never cover a matchable cell.
      expect(text.x).toBeGreaterThanOrEqual(PANEL_LEFT);
      expect(text.x).toBeGreaterThanOrEqual(BOARD_ORIGIN_X + BOARD_WIDTH);
      // Inside the safe area, its declared wrap reaching no further than the
      // panel's own right edge.
      expect(text.x + wrapWidth).toBeLessThanOrEqual(PANEL_RIGHT);
      expect(text.x + wrapWidth).toBeLessThanOrEqual(SAFE_AREA.x + SAFE_AREA.width);
      // Within the board's own rows, so it reaches neither HUD band.
      expect(text.y).toBeGreaterThanOrEqual(BOARD_ORIGIN_Y);
      expect(text.y).toBeLessThan(BOARD_ORIGIN_Y + BOARD_WIDTH);
    }

    // No other line is placed in the band, so no HUD line and no cast control is
    // drawn in it.
    const logIndex = moveLogContainerIndex(ctx);
    const inPanelBand = (x: number, y: number) =>
      x >= PANEL_LEFT && x < PANEL_RIGHT && y >= BOARD_ORIGIN_Y && y < BOARD_ORIGIN_Y + BOARD_WIDTH;
    const intruders = harness.texts
      .filter((text) => text.containerIndex !== logIndex)
      .filter((text) => inPanelBand(text.x, text.y));
    expect(intruders.map((text) => text.text)).toEqual([]);
  });

  it('exists only when the DEV flag is true', () => {
    vi.stubEnv('DEV', false);
    const disabled = createBattle();

    // The surface and every text object of it are absent, and no row is recorded.
    expect(field<object | null>(disabled.ctx, 'developerMoveLog')).toBeNull();
    expect(panelTexts(disabled.harness, disabled.ctx)).toEqual([]);
    expect(disabled.scene.getMoveLogRows()).toEqual([]);
    deliver(disabled.harness, 1, [{ type: 'ComboChanged', combo: 1 }]);
    expect(disabled.scene.getMoveLogRows()).toEqual([]);

    vi.unstubAllEnvs();

    const enabled = createBattle();
    expect(field<object | null>(enabled.ctx, 'developerMoveLog')).not.toBeNull();
    deliver(enabled.harness, 1, [{ type: 'ComboChanged', combo: 1 }]);
    expect(enabled.scene.getMoveLogRows().length).toBeGreaterThan(0);
    expect(panelTexts(enabled.harness, enabled.ctx).length).toBe(VISIBLE_ROWS + 1);
  });

  // ---------------------------------------------------------------------------
  // Row construction: delivered members only
  // ---------------------------------------------------------------------------

  it('records one row per delivered event, with the delivered type and the adapter actor/action', () => {
    const { harness, scene } = createBattle();
    deliver(harness, 7, EVERY_TYPE_BATCH);

    const rows = scene.getMoveLogRows();
    const eventRows = rows.filter((row) => row.includes(' type='));
    expect(eventRows).toHaveLength(EVERY_TYPE_BATCH.length);

    // Every event row names a delivered discriminator, and only from the closed set.
    const namedTypes = eventRows.map((row) => / type=([A-Za-z]+)/.exec(row)?.[1] ?? '');
    expect(namedTypes).toEqual(EVERY_TYPE_BATCH.map((event) => (event as { type: string }).type));
    expect(namedTypes.every((name) => CLOSED_EVENT_TYPES.includes(name))).toBe(true);

    // The delivered `serverSequence` and `turn`, and nothing derived.
    expect(eventRows.every((row) => row.includes(' seq=7 turn=4 '))).toBe(true);

    // The actor/action comes from the one adapter, with the board-cycle actor the
    // delivered boundary named: the swap step is the delivered action's, the board
    // cycle that follows it is the actor that opened it, a cast is the player's, a
    // Boss skill is the boss's, and `PowerChanged` is the mutation stage the
    // delivered `source` names — never a side (SIGNALR_PROTOCOL.md §3.2.24 item 3).
    const actorOf = (type: string) => {
      const row = eventRows.find((entry) => entry.includes(`type=${type} `));
      return /type=\S+ (\S+)/.exec(row ?? '')?.[1] ?? '';
    };
    expect(actorOf('SwapStarted')).toBe('player/swap');
    // `SwapResolved` opens no step of its own: the exchange it closes is already the
    // step its `SwapStarted` opened, so the row claims neither actor nor action.
    expect(actorOf('SwapResolved')).toBe('-/-');
    expect(actorOf('MatchCreated')).toBe('player/match');
    expect(actorOf('GemMatched')).toBe('player/match');
    expect(actorOf('CascadeCreated')).toBe('player/cascade');
    expect(actorOf('ComboChanged')).toBe('player/combo');
    expect(actorOf('PowerChanged')).toBe('power/power');
    expect(actorOf('PassiveCharged')).toBe('pet/passive-charge');
    expect(actorOf('PassiveTriggered')).toBe('boss/passive-trigger');
    expect(actorOf('BossSkillCast')).toBe('boss/boss-skill');
    expect(actorOf('CardCast')).toBe('player/card-cast');
    expect(actorOf('PetSkillCast')).toBe('player/pet-skill-cast');
    expect(actorOf('RelicTriggered')).toBe('player/relic');
    expect(actorOf('DamageDealt')).toBe('player/damage');
    expect(actorOf('DamageTaken')).toBe('player/damage');
    // The adapter presents nothing of its own for the damage pipeline detail, so
    // no actor is claimed for it.
    expect(actorOf('DamageCalculated')).toBe('-/-');

    // The delivered members reach the row through the one event formatter.
    const joined = eventRows.join('\n');
    expect(joined).toContain('Match: Straight ATK [24, 25, 26] depth: 1');
    expect(joined).toContain('GemMatched: cell 25 (ATK)');
    expect(joined).toContain('Cascade: depth 2');
    expect(joined).toContain('Combo: 3');
    expect(joined).toContain('DamageCalculated: base 100');
    expect(joined).toContain('DamageDealt: 150 (player -> boss)');
    expect(joined).toContain('DamageTaken: 150 on boss (from player)');
    expect(joined).toContain('PassiveCharged: xich-lang (pet:pet-instance-1) 4/5');
    expect(joined).toContain('PassiveTriggered: boss-hoa-long-rage (boss:boss-hoa-long) 5/5');
    expect(joined).toContain('BossSkillCast: flame-burst by boss-hoa-long');
    expect(joined).toContain('CardCast: card-heal');
    expect(joined).toContain('PetSkillCast: card-inferno');
    expect(joined).toContain('RelicTriggered: relicinst_berserker_core');
    expect(joined).toContain('PowerChanged: +40 -> 40 (match)');
  });

  it('fabricates no row: an unknown or malformed event, and a rejected action, record nothing', async () => {
    const { harness, scene } = createBattle();

    // A batch whose events the contract does not define is ignored with no row at
    // all — not guessed at, and not padded with a placeholder.
    deliver(harness, 1, [
      { type: 'NotADocumentedEvent', whatever: 1 },
      { nope: true },
      { type: 'MatchCreated', shape: 42 },
    ]);
    expect(scene.getMoveLogRows()).toEqual([]);

    // A rejected Swap emits nothing at all (GAME_EVENTS.md §1.2): no batch, no row.
    harness.setActionResult({ accepted: false, reason: 'NO_MATCH_FROM_SWAP' });
    tapCell(harness, 20);
    tapCell(harness, 21);
    await flush();
    expect(scene.getMoveLogRows()).toEqual([]);
  });

  // ---------------------------------------------------------------------------
  // Lifecycle rows, sourced from the orchestration machine
  // ---------------------------------------------------------------------------

  it('records queued, animating and completed for the timeline and for each phase', () => {
    const { harness, scene } = createBattle();
    deliver(harness, 1, [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'CascadeCreated', cascadeDepth: 2 },
    ]);

    const rows = scene.getMoveLogRows();
    const index = (needle: string) => rows.findIndex((row) => row.includes(needle));

    expect(rows.some((row) => row.includes('timeline queued phases=3 match,cascade,settle seq=1'))).toBe(true);
    // The machine's own state, not an animation's start.
    expect(rows.some((row) => row.includes('timeline animating state=presenting seq=1'))).toBe(true);
    // Two delivered board phases (the Match and the Cascade) plus the settle step
    // the board cycle ends with, each with its own animating/completed pair.
    expect(rows.some((row) => row.includes('phase animating 1/3 match match depth=1 seq=1'))).toBe(true);
    expect(rows.some((row) => row.includes('phase completed 1/3 match match depth=1 seq=1'))).toBe(true);
    expect(rows.some((row) => row.includes('phase animating 2/3 cascade cascade depth=2 seq=1'))).toBe(true);
    expect(rows.some((row) => row.includes('phase completed 2/3 cascade cascade depth=2 seq=1'))).toBe(true);
    expect(rows.some((row) => row.includes('phase animating 3/3 settle settle seq=1'))).toBe(true);
    expect(rows.some((row) => row.includes('phase completed 3/3 settle settle seq=1'))).toBe(true);
    expect(rows.some((row) => row.includes('timeline completed phases=3 seq=1'))).toBe(true);

    // ...and in the order the machine performed them.
    expect(index('timeline queued')).toBeLessThan(index('timeline animating'));
    expect(index('timeline animating')).toBeLessThan(index('phase animating 1/3'));
    expect(index('phase animating 1/3')).toBeLessThan(index('phase completed 1/3'));
    expect(index('phase completed 1/3')).toBeLessThan(index('phase animating 2/3'));
    expect(index('phase completed 2/3')).toBeLessThan(index('phase animating 3/3'));
    expect(index('phase completed 3/3')).toBeLessThan(index('timeline completed'));
  });

  it("records a committed Swap's coordinates and the terminal result before the handoff", async () => {
    const { harness, scene, ctx } = createBattle(true);

    const atHandoff: Array<readonly string[]> = [];
    const sceneProxy = field<{ start: (key: string, data?: unknown) => void }>(ctx, 'scene');
    const originalStart = sceneProxy.start;
    sceneProxy.start = (key: string, data?: unknown) => {
      // What the panel was showing at the instant the handoff happened.
      atHandoff.push([...scene.getMoveLogRows()]);
      originalStart(key, data);
    };

    // A committed Swap, whose own phase presents the two cells the player chose.
    tapCell(harness, 20);
    tapCell(harness, 21);
    harness.setBattleState(battleState({ sequence: 1 }));
    harness.emitBattleEvents({
      battleId: 'battle-253',
      serverSequence: 1,
      events: [
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
        { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 420 },
      ],
    } as BattleEventsEnvelope);

    for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
      harness.flushTweens(1);
    }
    await flush();

    const rows = scene.getMoveLogRows();
    expect(rows.some((row) => row.includes('swap 20->21'))).toBe(true);

    // The delivered outcome, with the delivered final HP pair, is the last row the
    // panel held when ResultScene was started — so it was recorded before the
    // handoff rather than after the scene was replaced.
    expect(atHandoff.length).toBeGreaterThan(0);
    expect(atHandoff[0][atHandoff[0].length - 1]).toContain('outcome victory bossHp=0 playerHp=420');
    // ...and the outcome phase that recorded it is the timeline's last phase, so
    // the row precedes the timeline's own completion.
    expect(rows.some((row) => row.includes('outcome victory bossHp=0 playerHp=420'))).toBe(true);
    expect(rows.indexOf(rows.filter((row) => row.includes('outcome victory bossHp=0'))[0])).toBeLessThan(
      rows.indexOf(rows.filter((row) => row.includes('timeline completed'))[0])
    );
  });

  it('records the abandonment of an in-flight timeline with its reason, and never duplicates its rows', () => {
    const { harness, scene } = createBattle(true);

    deliver(harness, 1, [
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    ]);
    const beforeAbort = scene.getMoveLogRows().filter((row) => row.includes(' type=')).length;

    // A newer delivered state supersedes the playing timeline.
    harness.setBattleState(battleState({ sequence: 2 }));

    const rows = scene.getMoveLogRows();
    // The playing phase is recorded as aborted, and the timeline with its reason.
    expect(rows.some((row) => row.includes('phase aborted 1/2 match match depth=1 seq=1'))).toBe(true);
    expect(rows.some((row) => row.includes('timeline aborted superseded-state seq=1'))).toBe(true);
    // The superseded resolution's own event rows were not recorded a second time,
    // and it did not also report itself completed.
    expect(rows.filter((row) => row.includes(' type=')).length).toBe(beforeAbort);
    expect(rows.some((row) => row.includes('timeline completed'))).toBe(false);
  });

  it('records a malformed batch as a failed timeline only when a timeline was there to fail', () => {
    const { harness, scene } = createBattle(true);

    // Nothing was playing, so there is no timeline to fail: no row is fabricated.
    expect(() =>
      harness.emitBattleEvents({
        battleId: 'battle-253',
        serverSequence: 1,
        events: [
          {
            type: 'MatchCreated',
            get shape() {
              throw new Error('Exploding getter in presentation');
            },
          },
        ],
      } as unknown as BattleEventsEnvelope)
    ).not.toThrow();
    expect(scene.getMoveLogRows()).toEqual([]);

    // A timeline that *is* playing when the malformed batch arrives is failed.
    deliver(harness, 2, [
      { type: 'MatchCreated', shape: 'Straight', cells: [3, 4, 5], gemType: 'ATK', cascadeDepth: 1 },
    ]);
    const before = scene.getMoveLogRows().length;

    expect(() =>
      harness.emitBattleEvents({
        battleId: 'battle-253',
        // Re-delivered at the sequence the synchronized state already carries, so
        // the malformed payload — not a superseding state — is what the playing
        // timeline meets (`TASK-273` §4.3: the parse runs before any
        // presentability decision).
        serverSequence: 2,
        events: [
          {
            type: 'MatchCreated',
            get shape() {
              throw new Error('Exploding getter in presentation');
            },
          },
        ],
      } as unknown as BattleEventsEnvelope)
    ).not.toThrow();

    const rows = scene.getMoveLogRows();
    expect(rows.length).toBeGreaterThan(before);
    expect(rows.some((row) => row.includes('timeline failed malformed-batch seq=2'))).toBe(true);
  });

  // ---------------------------------------------------------------------------
  // Bounded retention, auto-scroll and reset
  // ---------------------------------------------------------------------------

  it('evicts the oldest rows at the documented cap and keeps the newest on screen', () => {
    const { harness, scene, ctx } = createBattle();

    for (let index = 0; index < 100; index++) {
      deliver(harness, index + 1, [
        { type: 'ComboChanged', combo: index },
        { type: 'PowerChanged', delta: 1, power: index, source: 'match' },
        { type: 'DamageDealt', source: 'player', target: 'boss', amount: index },
      ]);
    }

    const rows = scene.getMoveLogRows();
    // The panel's documented cap, asserted the way the scene's own buffer test
    // asserts the diagnostic buffers' 256.
    expect(rows.length).toBe(RETAINED_ROWS);
    // The oldest rows are evicted...
    expect(rows.some((row) => row.includes('seq=1 '))).toBe(false);
    // ...and the newest are retained and readable.
    expect(rows[rows.length - 1]).toMatch(/seq=100$/);

    // Auto-scroll: the panel's own row objects show the newest rows, oldest first,
    // with the newest row last — so the row just recorded is always on screen.
    const visible = panelLines(harness, ctx).filter((line) => line.length > 0);
    const newestWindow = rows.slice(-VISIBLE_ROWS);
    expect(visible.slice(-newestWindow.length)).toEqual([...newestWindow]);
    expect(visible[visible.length - 1]).toBe(rows[rows.length - 1]);
  });

  it('clears the panel after create(), on shutdown, and when the battle identity changes', () => {
    const { harness, scene, ctx } = createBattle();
    deliver(harness, 1, [{ type: 'ComboChanged', combo: 1 }]);
    expect(scene.getMoveLogRows().length).toBeGreaterThan(0);

    // A delivered state for another battle ends this one's record, and the panel's
    // own lines go with it.
    harness.setBattleState(battleState({ battleId: 'battle-253-next', sequence: 9 }));
    expect(scene.getMoveLogRows()).toEqual([]);
    expect(panelLines(harness, ctx).filter((line) => line.length > 0)).toEqual(['MOVE LOG']);

    deliver(harness, 10, [{ type: 'ComboChanged', combo: 2 }], 'battle-253-next');
    expect(scene.getMoveLogRows().length).toBeGreaterThan(0);

    // Teardown releases the panel and clears its buffer.
    harness.shutdownScene(ctx);
    expect(field<object | null>(ctx, 'developerMoveLog')).toBeNull();
    expect(scene.getMoveLogRows()).toEqual([]);

    // A reused scene starts its next battle from an empty panel.
    runScene(scene, ctx, 'create');
    expect(scene.getMoveLogRows()).toEqual([]);
    expect(field<object | null>(ctx, 'developerMoveLog')).not.toBeNull();
  });

  it('records a repeated or older serverSequence once, and never twice', () => {
    const { harness, scene } = createBattle();
    const batch = [
      { type: 'ComboChanged', combo: 1 },
      { type: 'PowerChanged', delta: 1, power: 1, source: 'match' },
    ];

    deliver(harness, 5, batch);
    const afterFirst = scene.getMoveLogRows().filter((row) => row.includes(' type=')).length;
    expect(afterFirst).toBe(batch.length);

    deliver(harness, 5, batch);
    expect(scene.getMoveLogRows().filter((row) => row.includes(' type=')).length).toBe(afterFirst);

    deliver(harness, 4, batch);
    expect(scene.getMoveLogRows().filter((row) => row.includes(' type=')).length).toBe(afterFirst);
  });

  it('renders the delivered turn and sequence inside the panel, and nowhere the diagnostic boundary governs', () => {
    const { harness, ctx } = createBattle();
    deliver(harness, 12, [{ type: 'ComboChanged', combo: 3 }]);

    const panel = panelLines(harness, ctx).join('\n');
    expect(panel).toContain('seq=12 turn=4');

    // The boundary TASK-210 §9 draws still holds everywhere else: the panel is the
    // one authorized surface (D-1), so every other rendered line is free of the
    // transport/RNG/turn diagnostics and of the raw event log.
    const outside = playerFacingText(harness, ctx);
    for (const diagnostic of ['SignalR:', 'Sync:', 'BattleId:', 'RngSeed:', 'Turn:', 'Sequence:']) {
      expect(outside).not.toContain(diagnostic);
    }
    expect(outside).not.toContain('seq=12');
    expect(outside).not.toContain('Combo: 3');
  });
});

/**
 * P0.3 — authoritative recovery, action outcomes, and the synchronization
 * indicator.
 *
 * The three gaps this suite covers are all *scene-level* consequences of the
 * runtime contract:
 *
 * ```text
 * recovery snapshot   `onBattleState(state, { source: 'recovery' })` is the
 *                     §7.1 reconciliation. It is applied directly — board, HUD,
 *                     health, turn-owned readouts and Status Effects — with no
 *                     matching `ReceiveEvents` batch required, and it converges
 *                     and releases everything the interrupted presentation was
 *                     holding (`ADR-008`: recovery is a snapshot, never an event
 *                     replay)
 * action outcomes     a definitive rejection (`accepted: false`) is a gameplay
 *                     no-op that is rolled back onto the newest authoritative
 *                     state; a transport failure with no acknowledgement is
 *                     **not** a rejection, so nothing is assumed about whether
 *                     the action committed and the runtime's recovery is what
 *                     settles the scene (`SIGNALR_PROTOCOL.md` §5, §7)
 * sync indicator      `GameRuntimeState.sync` drives one non-blocking line:
 *                     visible while `unsynchronized`, hidden once the runtime
 *                     confirms `synchronized`
 * ```
 *
 * The fixtures below are the delivered values only: a board is rotated by cell
 * count so "the board on screen" is provably the delivered one, and no test
 * asserts on a value the client computes — it computes none.
 */
describe('BattleScene — authoritative recovery, action outcomes & synchronization (P0.3)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];
  const GEM_LABELS: Readonly<Record<string, string>> = {
    ATK: 'ATK',
    DEF: 'DEF',
    HP: 'HP',
    POWER: 'PWR',
  };
  const BATTLE_ID = 'battle-p03';

  /** A scene-owned presentation field, read the way the scene wrote it. */
  function field<T>(ctx: object, name: string): T {
    return (ctx as Record<string, T>)[name];
  }

  /**
   * A delivered 8 x 8 board, rotated by `shift` cells — so the board a recovery
   * snapshot carries is provably different from the one already on screen.
   */
  function deliveredBoard(shift = 0): RuntimeBoard {
    return {
      cells: Array.from({ length: 64 }, (_, index) => ({
        gemType: GEM_NAMES[(index + shift) % GEM_NAMES.length],
      })),
    };
  }

  /** The labels the delivered board must be rendered as, in §1.0 index order. */
  function expectedLabels(shift = 0): string[] {
    return Array.from({ length: 64 }, (_, index) =>
      GEM_LABELS[GEM_NAMES[(index + shift) % GEM_NAMES.length]]
    );
  }

  /** The §1.0 cell index a board-local point sits on, or `null` outside the board. */
  function cellIndexOf(x: number, y: number): number | null {
    const column = Math.round((x - BOARD_ORIGIN_X - CELL_SIZE / 2) / BOARD_PITCH);
    const row = Math.round((y - BOARD_ORIGIN_Y - CELL_SIZE / 2) / BOARD_PITCH);

    if (row < 0 || row >= BOARD_COLUMNS || column < 0 || column >= BOARD_COLUMNS) {
      return null;
    }

    return row * BOARD_COLUMNS + column;
  }

  /**
   * The label rendered at each §1.0 cell, read from the view's own position.
   *
   * A committed Swap deliberately exchanges two views' cells, so the cell a
   * label *shows* is the cell it sits on, not its slot in the container.
   */
  function labelsByCell(harness: ReturnType<typeof createSceneHarness>): string[] {
    const labels: string[] = new Array(BOARD_COLUMNS * BOARD_COLUMNS).fill('');

    for (const cell of harness.boardCells) {
      if (cell.kind !== 'label') {
        continue;
      }

      const view = cell as unknown as { x: number; y: number; label?: string };
      const index = cellIndexOf(view.x, view.y);

      if (index !== null) {
        labels[index] = view.label ?? '';
      }
    }

    return labels;
  }

  /** One delivered authoritative state, shaped exactly as the §4 push is. */
  function battleState(overrides: Partial<RuntimeBattleState> = {}): RuntimeBattleState {
    return {
      battleId: BATTLE_ID,
      turn: 1,
      sequence: 0,
      rngSeed: 42,
      rngState: { state: 123456789, increment: 1 },
      board: deliveredBoard(),
      playerState: { combo: 0, matchCount: 0 },
      petState: {
        hp: 1000,
        maxHp: 1000,
        power: 0,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 0 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
      bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
      ...overrides,
    };
  }

  /** One technical runtime state, as `GameRuntimeState` publishes it. */
  function runtimeState(overrides: Partial<GameRuntimeState> = {}): GameRuntimeState {
    return {
      ...INITIAL_RUNTIME_STATE,
      session: 'authenticated',
      connection: 'connected',
      runtime: 'ready',
      sync: 'synchronized',
      ...overrides,
    };
  }

  /** A battle scene with the recorded, test-driven tween queue. */
  function createBattle(
    state: RuntimeBattleState | null = battleState(),
    runtimeStateValue: GameRuntimeState = runtimeState()
  ) {
    const harness = createSceneHarness({
      battleState: state,
      withTweens: true,
      state: runtimeStateValue,
    });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    runScene(scene, ctx, 'create');
    if (harness.pendingTweenCount() > 0) {
      harness.flushTweens(harness.pendingTweenCount());
    }
    return { harness, scene, ctx };
  }

  function inputLocked(scene: BattleScene, ctx: object): boolean {
    return (
      Object.getPrototypeOf(scene) as { isInputLocked(this: object): boolean }
    ).isInputLocked.call(ctx);
  }

  /** The presentation phases the scene has played, in order. */
  function playedPhases(ctx: object): string[] {
    return [...field<string[]>(ctx, 'presentedPhases')];
  }

  /** Every line the scene renders, joined — the player-facing text. */
  function renderedText(harness: ReturnType<typeof createSceneHarness>): string {
    return harness.texts.map((entry) => entry.text).join('\n');
  }

  /** The delivered §4 push, with the metadata the runtime publishes for it. */
  function push(
    harness: ReturnType<typeof createSceneHarness>,
    next: RuntimeBattleState
  ): void {
    harness.setBattleState(next, { source: 'push' });
  }

  /** The §7.1 authoritative reconciliation snapshot. */
  function recovery(
    harness: ReturnType<typeof createSceneHarness>,
    next: RuntimeBattleState
  ): void {
    harness.setBattleState(next, { source: 'recovery' });
  }

  function emitBatch(
    harness: ReturnType<typeof createSceneHarness>,
    serverSequence: number,
    events: unknown[],
    battleId = BATTLE_ID
  ): void {
    harness.emitBattleEvents({ battleId, serverSequence, events });
  }

  /**
   * Submits one action whose acknowledgement is held open, which is the state a
   * real request is in while its resolution's push and batch arrive
   * (`SIGNALR_PROTOCOL.md` §3.1). The returned handles settle it.
   */
  function holdAction(harness: ReturnType<typeof createSceneHarness>): {
    resolve: (acknowledgement: { accepted: boolean; reason?: string | null }) => void;
    reject: (error: unknown) => void;
  } {
    let resolve!: (acknowledgement: { accepted: boolean; reason?: string | null }) => void;
    let reject!: (error: unknown) => void;

    harness.setActionBehaviour(
      () =>
        new Promise<never>((res, rej) => {
          resolve = res as unknown as (acknowledgement: {
            accepted: boolean;
            reason?: string | null;
          }) => void;
          reject = rej;
        })
    );

    return {
      // Read through the binding, not a snapshot of it: the behaviour is invoked
      // when the scene actually submits, which is after this call returns.
      resolve: (acknowledgement) => resolve(acknowledgement),
      reject: (error) => reject(error),
    };
  }

  /** A Swap that resolves through two cascade passes and both combat stages. */
  const CASCADE_BATCH: unknown[] = [
    { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
    { type: 'CascadeCreated', cascadeDepth: 1 },
    { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'DEF', cascadeDepth: 2 },
    { type: 'GemMatched', cellIndex: 32, gemType: 'DEF' },
    { type: 'CascadeCreated', cascadeDepth: 2 },
    { type: 'DamageCalculated', base: 100, comboModifier: 1.5, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 150 },
    { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
    { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
    { type: 'DamageCalculated', base: 80, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 80 },
    { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
    { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
  ];

  /** The same resolution's combat stages only — no board cycle at all. */
  const COMBAT_BATCH: unknown[] = [
    { type: 'DamageCalculated', base: 100, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 100 },
    { type: 'DamageDealt', source: 'player', target: 'boss', amount: 100 },
    { type: 'DamageTaken', source: 'player', target: 'boss', amount: 100 },
    { type: 'DamageCalculated', base: 80, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 80 },
    { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
    { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
  ];

  /** Plays the timeline forward until `count` phases have played, step by step. */
  function playUntil(
    harness: ReturnType<typeof createSceneHarness>,
    ctx: object,
    count: number
  ): void {
    for (let step = 0; step < 500 && playedPhases(ctx).length < count; step++) {
      if (harness.pendingTweenCount() === 0) {
        return;
      }
      harness.flushTweens(1);
    }
  }

  it('1. converges and releases the lock when a recovery snapshot arrives during a cascade', () => {
    const { harness, scene, ctx } = createBattle();
    const held = holdAction(harness);

    tapCell(harness, 20);
    tapCell(harness, 21);

    // The resolution's own state push arrives first — held, because its batch is
    // what sequences it — and then that batch: the cascade timeline plays while
    // the request is still unanswered.
    push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));
    expect(labelsByCell(harness)).toEqual(expectedLabels(0));

    emitBatch(harness, 1, CASCADE_BATCH);
    playUntil(harness, ctx, 3);

    expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1', 'cascade depth=1']);
    expect(harness.pendingTweenCount()).toBeGreaterThan(0);
    expect(inputLocked(scene, ctx)).toBe(true);

    // The recovery snapshot: the authoritative state at sequence 2, with a
    // different board, and no `ReceiveEvents` batch at all.
    recovery(harness, battleState({ sequence: 2, turn: 3, board: deliveredBoard(2) }));

    // The cascade presentation is abandoned exactly where it stood — the settle
    // step that would converge the resolution's board is never played — the lock
    // is released, and the scene renders the snapshot's own board.
    expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1', 'cascade depth=1']);
    expect(playedPhases(ctx)).not.toContain('settle');
    expect(harness.pendingTweenCount()).toBe(0);
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(field<string>(ctx, 'orchestration')).toBe('idle');
    expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
    expect(labelsByCell(harness)).toEqual(expectedLabels(2));

    // The answer to the request that was in flight cannot re-lock the scene.
    held.resolve({ accepted: true });
    expect(inputLocked(scene, ctx)).toBe(false);
  });

  it('2. converges and releases the lock when a recovery snapshot arrives during combat', () => {
    const { harness, scene, ctx } = createBattle();

    // The resolution's own state push, then its combat batch: the damage phase
    // presents the pair the push carried.
    push(
      harness,
      battleState({
        sequence: 1,
        turn: 2,
        board: deliveredBoard(1),
        bossState: { bossId: 'boss-hoa-long', hp: 4500, maxHp: 5000 },
      })
    );
    emitBatch(harness, 1, COMBAT_BATCH);

    expect(playedPhases(ctx)).toEqual(['damage player->boss']);
    expect(field<{ text: string }>(ctx, 'bossHpText').text).toBe('4500 / 5000');
    expect(inputLocked(scene, ctx)).toBe(true);

    // The recovery snapshot carries the authoritative values — including an HP
    // pair no damage event in the batch derives.
    recovery(
      harness,
      battleState({
        sequence: 2,
        turn: 3,
        board: deliveredBoard(2),
        bossState: { bossId: 'boss-hoa-long', hp: 4000, maxHp: 5000 },
        petState: {
          hp: 700,
          maxHp: 1000,
          power: 35,
          passiveId: 'xich-lang',
          passiveProgress: { threshold: 5, current: 3 },
          equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          statusEffects: [],
        },
      })
    );

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(harness.pendingTweenCount()).toBe(0);
    expect(playedPhases(ctx)).toEqual(['damage player->boss']);
    expect(field<{ text: string }>(ctx, 'bossHpText').text).toBe('4000 / 5000');
    expect(field<{ text: string }>(ctx, 'petHpText').text).toBe('700 / 1000');
    expect(field<{ text: string }>(ctx, 'petPowerText').text).toBe('35 / 100');
    expect(labelsByCell(harness)).toEqual(expectedLabels(2));
  });

  it('3. invalidates pending callbacks, so none can advance a newer timeline or restore a stale board', () => {
    const { harness, scene, ctx } = createBattle();
    const phaseClock = field<object>(ctx, 'phaseClock');

    emitBatch(harness, 1, CASCADE_BATCH);

    // The abandoned timeline's pacing callback, held exactly as Phaser would
    // have it queued when the recovery lands.
    const abandoned = harness
      .pendingTweenRecords()
      .find((tween) => tween.targets.includes(phaseClock));
    expect(abandoned?.completion).toBeTypeOf('function');

    recovery(harness, battleState({ sequence: 2, board: deliveredBoard(2) }));
    expect(labelsByCell(harness)).toEqual(expectedLabels(2));

    const phasesAfterRecovery = playedPhases(ctx);
    expect(phasesAfterRecovery).toEqual(['match depth=1']);

    // A newer resolution arrives and starts its own timeline.
    emitBatch(harness, 3, CASCADE_BATCH);
    expect(playedPhases(ctx)).toEqual([...phasesAfterRecovery, 'match depth=1']);

    const pendingBefore = harness.pendingTweenCount();
    expect(pendingBefore).toBeGreaterThan(0);

    // The abandoned timeline's callback lands late. It belongs to a presentation
    // that no longer exists, so it advances nothing and writes nothing — not even
    // into the timeline that replaced it.
    abandoned?.completion?.();

    expect(playedPhases(ctx)).toEqual([...phasesAfterRecovery, 'match depth=1']);
    expect(harness.pendingTweenCount()).toBe(pendingBefore);
    expect(field<number>(ctx, 'presentationSequence')).toBe(3);
    expect(inputLocked(scene, ctx)).toBe(true);
    expect(labelsByCell(harness)).toEqual(expectedLabels(2));
  });

  it('4. releases the pending presentation exactly once, including an equal-sequence recovery', async () => {
    const { harness, scene, ctx } = createBattle();
    const phaseClock = field<object>(ctx, 'phaseClock');
    const held = holdAction(harness);

    tapCell(harness, 20);
    tapCell(harness, 21);

    // The resolution this client is awaiting is playing, with its state held.
    const resolved = battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) });
    push(harness, resolved);
    emitBatch(harness, 1, CASCADE_BATCH);

    expect(field<object | null>(ctx, 'presentationTimeline')).not.toBeNull();
    expect(field<number | null>(ctx, 'heldSequence')).toBe(1);
    expect(inputLocked(scene, ctx)).toBe(true);

    // An equal-sequence recovery — the runtime's idempotent re-application of the
    // very resolution this scene is presenting — still ends that presentation and
    // releases the lock it was holding. Nothing is left waiting for a batch.
    recovery(harness, resolved);

    // The pacing waiter was released once, by the abort of the timeline it
    // belonged to: the phase clock is killed exactly here.
    expect(harness.killedTweenTargets.filter((target) => target === phaseClock)).toHaveLength(1);
    expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
    expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
    expect(harness.pendingTweenCount()).toBe(0);
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(labelsByCell(harness)).toEqual(expectedLabels(1));

    // A second, identical recovery releases nothing a second time.
    recovery(harness, resolved);

    expect(harness.killedTweenTargets.filter((target) => target === phaseClock)).toHaveLength(1);
    expect(harness.pendingTweenCount()).toBe(0);
    expect(field<string>(ctx, 'orchestration')).toBe('idle');
    expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(labelsByCell(harness)).toEqual(expectedLabels(1));

    // And the answer to the request that was in flight when the first recovery
    // landed cannot settle, report, or re-lock the released scene.
    held.resolve({ accepted: true });
    await flush();

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(renderedText(harness)).not.toContain('20 -> 21');
  });

  it('5. requires no ReceiveEvents batch: a recovery applies the state a push would still be holding', () => {
    const { harness, scene, ctx } = createBattle();
    const held = holdAction(harness);

    tapCell(harness, 20);
    tapCell(harness, 21);

    const resolved = battleState({
      sequence: 1,
      turn: 2,
      board: deliveredBoard(3),
      playerState: { combo: 3, matchCount: 7 },
      bossState: { bossId: 'boss-hoa-long', hp: 3000, maxHp: 5000 },
      petState: {
        hp: 800,
        maxHp: 1000,
        power: 40,
        passiveId: 'xich-lang',
        passiveProgress: { threshold: 5, current: 4 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
      },
    });

    // As a push this is the awaited resolution's state: it is held at the
    // presentation the gesture was made on until its batch arrives.
    push(harness, resolved);

    expect(labelsByCell(harness)).toEqual(expectedLabels(0));
    expect(field<number | null>(ctx, 'heldSequence')).toBe(1);
    expect(inputLocked(scene, ctx)).toBe(true);

    // Delivered as the authoritative reconciliation instead, the same state is
    // applied directly — with no batch at all, and with no event fabricated for
    // it — and the lock is released.
    recovery(harness, resolved);

    expect(labelsByCell(harness)).toEqual(expectedLabels(3));
    expect(field<{ text: string }>(ctx, 'bossHpText').text).toBe('3000 / 5000');
    expect(field<{ text: string }>(ctx, 'petHpText').text).toBe('800 / 1000');
    expect(field<{ text: string }>(ctx, 'petPowerText').text).toBe('40 / 100');
    expect(field<{ text: string }>(ctx, 'comboText').text).toBe('COMBO ×3');
    expect(field<{ text: string }>(ctx, 'matchesText').text).toBe('MATCHES 7');
    expect(field<{ text: string }>(ctx, 'petPassiveProgressText').text).toContain('4 / 5');
    expect(scene.getPresentedEvents()).toEqual([]);
    expect(playedPhases(ctx)).toEqual([]);
    expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
    expect(inputLocked(scene, ctx)).toBe(false);

    held.resolve({ accepted: true });
  });

  it('6. keeps the ordinary push animation: the batch still drives the timeline', () => {
    const { harness, scene, ctx } = createBattle();

    push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));

    // An unsolicited push presents immediately: nothing is pending for it.
    expect(labelsByCell(harness)).toEqual(expectedLabels(1));
    expect(inputLocked(scene, ctx)).toBe(false);

    emitBatch(harness, 1, CASCADE_BATCH);

    // The batch's own order is the timeline's order, phase by phase.
    expect(inputLocked(scene, ctx)).toBe(true);
    expect(playedPhases(ctx)).toEqual(['match depth=1']);
    harness.flushTweens();
    expect(playedPhases(ctx)).toEqual([
      'match depth=1',
      'cascade depth=1',
      'cascade depth=2',
      'settle',
      'damage player->boss',
      'retaliation boss->player',
    ]);

    // The timeline's own end releases the guard and leaves the delivered board and
    // readouts on screen: the push path is unchanged by the recovery lifecycle.
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(harness.pendingTweenCount()).toBe(0);
    expect(labelsByCell(harness)).toEqual(expectedLabels(1));
    expect(field<{ text: string }>(ctx, 'bossHpText').text).toBe('5000 / 5000');
  });

  it('7. rolls the board back onto the newest authoritative state on a definitive rejection', async () => {
    const { harness, scene, ctx } = createBattle();
    const held = holdAction(harness);

    tapCell(harness, 20);
    tapCell(harness, 21);
    expect(inputLocked(scene, ctx)).toBe(true);

    // A newer authoritative state arrives while the request is unanswered: it is
    // the awaited resolution's own state, so the board is held at the state the
    // gesture was made on.
    push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));
    expect(labelsByCell(harness)).toEqual(expectedLabels(0));
    expect(field<number | null>(ctx, 'heldSequence')).toBe(1);

    // The server's definitive answer: the swap was a gameplay no-op.
    held.resolve({ accepted: false, reason: 'INVALID_SWAP' });
    await flush();

    // The rollback converges on the authoritative state the runtime now holds —
    // the newer one, never the snapshot the request was submitted against — and
    // the guard is released.
    expect(labelsByCell(harness)).toEqual(expectedLabels(1));
    expect(inputLocked(scene, ctx)).toBe(false);
    expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
    expect(field<object | null>(ctx, 'committedSwapCells')).toBeNull();
    expect(renderedText(harness)).toContain('INVALID_SWAP');
    expect(scene.getPresentedEvents()).toEqual([]);

    // And the refused pair can never be presented by a later resolution.
    push(harness, battleState({ sequence: 2, turn: 3, board: deliveredBoard(2) }));
    emitBatch(harness, 2, [{ type: 'ComboChanged', combo: 1 }]);

    expect(playedPhases(ctx).some((phase) => phase.startsWith('swap'))).toBe(false);
    expect(labelsByCell(harness)).toEqual(expectedLabels(2));
  });

  it('8. keeps an unanswered action unresolved and reconciles it through the recovery snapshot', async () => {
    const { harness, scene, ctx } = createBattle();

    harness.setActionBehaviour(async () => {
      throw new RuntimeActionOutcomeUnknownError('Swap', new Error('Transport timeout'));
    });

    tapCell(harness, 20);
    tapCell(harness, 21);
    await flush();

    // The request was submitted and never answered, so the scene claims nothing:
    // the gesture stays the committed pair, the resolution stays pending, and the
    // lock is held until authoritative state arrives.
    expect(inputLocked(scene, ctx)).toBe(true);
    expect(field<object | null>(ctx, 'committedSwapCells')).toEqual({ from: 20, to: 21 });
    expect(field<boolean>(ctx, 'resolutionPending')).toBe(true);
    expect(renderedText(harness)).toContain('unconfirmed');
    expect(renderedText(harness)).not.toContain('not sent');

    // The runtime's recovery answers what happened: the swap did commit, so the
    // authoritative state is one sequence step on and carries its board.
    recovery(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(field<boolean>(ctx, 'resolutionPending')).toBe(false);
    expect(field<object | null>(ctx, 'committedSwapCells')).toBeNull();
    expect(harness.pendingTweenCount()).toBe(0);
    expect(labelsByCell(harness)).toEqual(expectedLabels(1));
  });

  it('9. ignores a late response for a battle that is no longer active', async () => {
    const { harness, scene, ctx } = createBattle(
      battleState({ battleId: 'battle-a', sequence: 0 })
    );
    const held = holdAction(harness);

    tapCell(harness, 20);
    tapCell(harness, 21);
    expect(inputLocked(scene, ctx)).toBe(true);
    expect(field<object | null>(ctx, 'committedSwapCells')).toEqual({ from: 20, to: 21 });

    // Battle B replaces battle A: the lifecycle transition's own push. A's
    // submission belongs to A, so it is invalidated with it and the lock it held
    // is released.
    push(
      harness,
      battleState({ battleId: 'battle-b', sequence: 0, turn: 1, board: deliveredBoard(3) })
    );

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(field<object | null>(ctx, 'committedSwapCells')).toBeNull();
    expect(labelsByCell(harness)).toEqual(expectedLabels(3));

    // Battle A's answer finally arrives. It cannot settle, report, or lock the
    // active scene.
    held.resolve({ accepted: true });
    await flush();

    expect(inputLocked(scene, ctx)).toBe(false);
    expect(field<string>(ctx, 'orchestration')).toBe('idle');
    expect(renderedText(harness)).not.toContain('20 -> 21');
    expect(harness.requestedActions).toHaveLength(0);
    expect(labelsByCell(harness)).toEqual(expectedLabels(3));
    expect(field<RuntimeBattleState | null>(ctx, 'currentBattleState')?.battleId).toBe('battle-b');
  });

  it('10. shows the synchronization indicator while unsynchronized and hides it once synchronized', () => {
    const { harness, ctx } = createBattle(
      battleState({ sequence: 1 }),
      runtimeState({ sync: 'unsynchronized', connection: 'connected' })
    );

    const indicator = field<{
      text: string;
      color?: string;
      visible: boolean;
      interactive: boolean;
      y: number;
    }>(ctx, 'syncText');

    // Recovering: the runtime has published that its copy is not synchronized and
    // a connection is up to re-establish it.
    expect(indicator.visible).toBe(true);
    expect(indicator.text).toBe('SYNCHRONIZING…');
    expect(indicator.color).toBe('#fbbf24');

    // Non-blocking: a plain, non-interactive line placed in the band above the
    // board, never over a cell or a control.
    expect(indicator.interactive).toBe(false);
    expect(indicator.y).toBeLessThan(BOARD_ORIGIN_Y);

    // The runtime confirms synchronization: the line is hidden, and it is the
    // runtime's own value that hides it — never a client assumption.
    harness.emitRuntimeState(runtimeState({ sync: 'synchronized' }));
    expect(indicator.visible).toBe(false);
    expect(indicator.text).toBe('');

    // Connected with no battle is not an outage.
    harness.emitRuntimeState(runtimeState({ sync: 'awaiting_battle' }));
    expect(indicator.visible).toBe(false);

    // A connection that is not up cannot promise recovery: the line states the
    // state instead.
    harness.emitRuntimeState(runtimeState({ sync: 'unsynchronized', connection: 'reconnecting' }));
    expect(indicator.visible).toBe(true);
    expect(indicator.text).toBe('NOT SYNCHRONIZED');
    expect(indicator.color).toBe('#f87171');

    // A scene with no runtime at all reports nothing here: its connection line
    // already states that the battle is unavailable.
    const withoutRuntime = createSceneHarness({ withRuntime: false, withTweens: true });
    const orphan = new BattleScene();
    const orphanCtx = withoutRuntime.context(orphan, 'BattleScene');
    runScene(orphan, orphanCtx, 'create');

    expect(field<{ visible: boolean }>(orphanCtx, 'syncText').visible).toBe(false);
  });

  it('11. leaks no timers, tweens, listeners, or Phaser objects across repeated recoveries and shutdown', () => {
    const { harness, scene, ctx } = createBattle();

    expect(harness.battleStateListeners.size).toBe(1);
    expect(harness.battleEventListeners.size).toBe(1);
    expect(harness.boardCells).toHaveLength(128);

    // Three recoveries, each interrupting a real timeline: nothing accumulates.
    let sequence = 1;

    for (let round = 0; round < 3; round++) {
      push(harness, battleState({ sequence, board: deliveredBoard(sequence) }));
      emitBatch(harness, sequence, CASCADE_BATCH);
      expect(harness.pendingTweenCount()).toBeGreaterThan(0);

      sequence += 1;
      recovery(harness, battleState({ sequence, board: deliveredBoard(sequence) }));

      expect(harness.pendingTweenCount()).toBe(0);
      expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
      expect(inputLocked(scene, ctx)).toBe(false);
      // The board is still exactly one view pair per cell: a recovery converges
      // the views it has rather than creating or destroying them.
      expect(harness.boardCells).toHaveLength(128);
      expect(harness.boardInputHandlerCounts.get('gameobjectdown')).toBe(1);

      sequence += 1;
    }

    expect(labelsByCell(harness)).toEqual(expectedLabels(sequence - 1));

    // Teardown releases the subscriptions, the deferred work, the game objects,
    // and the indicator's own reference.
    harness.shutdownScene(ctx);

    expect(harness.battleStateListeners.size).toBe(0);
    expect(harness.battleEventListeners.size).toBe(0);
    expect(harness.listeners.size).toBe(0);
    expect(harness.pendingTweenCount()).toBe(0);
    expect(harness.boardCells).toHaveLength(0);
    expect(field<object | null>(ctx, 'syncText')).toBeNull();
    expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
  });

  /**
   * TASK-273 / P0.4 — the scene's own delivery-integrity gate, the release of a
   * resolution that cannot be presented, and the battle-ended publication.
   *
   * The runtime withholds a batch it cannot place against its synchronized state
   * (its own delivery-integrity check); these tests pin the scene's **defense in
   * depth** — a batch the authoritative state on screen does not represent is
   * refused whole (`SIGNALR_PROTOCOL.md` §3.1, §6 item 4, `GAME_STATE.md` §5.1
   * item 3) — plus the two deadlock paths `TASK-273` §4.3/§4.4 close:
   *
   * ```text
   * unrepresentable batch   a foreign battle's, one ahead of the delivered
   *                         `sequence`, or one behind it: no timeline, no callout,
   *                         no outcome handoff, no accepted sequence, no stamp
   * empty phase set         a resolution that is accepted but cannot be presented
   *                         releases every lock it engaged rather than holding
   *                         `resolutionPending` for a timeline that cannot exist
   * ended battle            §7.3's loss arrives as `null` + `source: 'ended'`: the
   *                         stale presentation is released and no result is invented
   * ```
   */
  describe('TASK-273 — unrepresentable batches, unpresentable resolutions & ended battles (P0.4)', () => {
    /** One batch that produces a timeline, keyed to `sequence` for `battleId`. */
    function cascadeAt(battleId: string, sequence: number): BattleEventsEnvelope {
      return { battleId, serverSequence: sequence, events: CASCADE_BATCH } as BattleEventsEnvelope;
    }

    it('13. never builds a timeline over a board the held state does not represent', () => {
      const { harness, scene, ctx } = createBattle(
        battleState({ sequence: 1, board: deliveredBoard(1) })
      );

      // The resolution's state never arrived: the batch is one sequence step past
      // the delivered state, so nothing on screen represents it. The batch is
      // delivered without a matching push on purpose, which is exactly what the
      // scene's own gate must survive.
      harness.emitUnmatchedBattleEvents(cascadeAt('battle-p03', 2));

      expect(playedPhases(ctx)).toEqual([]);
      expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
      expect(harness.pendingTweenCount()).toBe(0);
      expect(inputLocked(scene, ctx)).toBe(false);

      // The board on screen is still the delivered one, and no feedback claims a
      // resolution the scene could not place.
      expect(labelsByCell(harness)).toEqual(expectedLabels(1));
      expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('');

      // Nothing was booked for it: no resolution was accepted and no board was
      // stamped from it.
      expect(field<number | null>(ctx, 'acceptedResolutionSequence')).toBeNull();
      expect(field<number | null>(ctx, 'settledBoardSequence')).toBe(1);
    });

    it('14. cannot let a cross-battle batch reach presentation, the board, a callout or ResultScene', () => {
      const { harness, scene, ctx } = createBattle(
        battleState({ sequence: 3, board: deliveredBoard(2) })
      );

      // A terminal batch belonging to another battle: `BattleWon` would normally
      // end the battle and hand its outcome to `ResultScene`.
      harness.emitUnmatchedBattleEvents({
        battleId: 'battle-elsewhere',
        serverSequence: 3,
        events: [
          { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 640 },
        ],
      } as BattleEventsEnvelope);

      expect(harness.sceneStarted).toEqual([]);
      expect(playedPhases(ctx)).toEqual([]);
      expect(scene.getPresentedEvents()).toEqual([]);
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));
      expect(field<{ text: string }>(ctx, 'calloutText').text).toBe('');
      expect(field<{ text: string }>(ctx, 'orchestration')).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('15. accepts an empty phase set and releases every lock it engaged', async () => {
      const { harness, scene, ctx } = createBattle();
      const held = holdAction(harness);

      // A cast engages the same presentation lock a Swap does, and it commits no
      // swap pair — so a batch that produces no presentable phase at all leaves the
      // guard with nothing to release it.
      await flush();
      harness.clickOption(/^Card: card-heal$/);
      await flush();

      expect(inputLocked(scene, ctx)).toBe(true);
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(true);

      // The awaiting cast's own resolution state, then a batch that produces no
      // presentable phase at all.
      push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));
      expect(field<number | null>(ctx, 'heldSequence')).toBe(1);

      emitBatch(harness, 1, []);

      // No timeline is built, and the resolution cannot retain the presentation:
      // the held readouts are converged on the delivered state and every lock the
      // batch engaged is released, so the scene cannot wait forever for a timeline
      // that will never exist.
      expect(playedPhases(ctx)).toEqual([]);
      expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(false);
      expect(field<object | null>(ctx, 'committedSwapCells')).toBeNull();
      expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
      expect(field<string>(ctx, 'orchestration')).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);

      // Accepted (a repeat presents nothing a second time) and converged on the
      // delivered board — never on a fabricated one.
      expect(field<number | null>(ctx, 'acceptedResolutionSequence')).toBe(1);
      expect(labelsByCell(harness)).toEqual(expectedLabels(1));
      expect(scene.getPresentedEvents()).toEqual([]);

      held.resolve({ accepted: true });
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('16. converges a replay-style state push without a batch and releases the appropriate locks', async () => {
      const { harness, scene, ctx } = createBattle();
      const held = holdAction(harness);

      tapCell(harness, 20);
      tapCell(harness, 21);

      // The acknowledgement settles, but the resolution's batch never arrives —
      // the server does not rebroadcast events (`SIGNALR_PROTOCOL.md` §8 item 8,
      // `ADR-008`).
      held.resolve({ accepted: true });
      await flush();

      push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));
      expect(field<number | null>(ctx, 'heldSequence')).toBe(1);
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));
      expect(inputLocked(scene, ctx)).toBe(true);

      // The next resolution's state arrives instead. It proves the client has
      // moved past the resolution the batch would have described, so the guard is
      // released and the delivered board is presented — no fabricated combat
      // state, no invented timeline.
      push(harness, battleState({ sequence: 2, turn: 3, board: deliveredBoard(2) }));

      expect(inputLocked(scene, ctx)).toBe(false);
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(false);
      expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
      expect(field<string>(ctx, 'orchestration')).toBe('idle');
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));
      expect(scene.getPresentedEvents()).toEqual([]);
      expect(playedPhases(ctx)).toEqual([]);
    });

    it('17. cancels a boss animation in flight and converges on the recovery snapshot', () => {
      const { harness, scene, ctx } = createBattle();

      push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));
      emitBatch(harness, 1, [
        { type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' },
      ]);

      expect(playedPhases(ctx)).toEqual(['boss-skill flame-burst']);
      expect(harness.pendingTweenCount()).toBeGreaterThan(0);
      expect(inputLocked(scene, ctx)).toBe(true);

      recovery(
        harness,
        battleState({
          sequence: 2,
          turn: 3,
          board: deliveredBoard(2),
          petState: {
            hp: 700,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
            statusEffects: [],
          },
        })
      );

      // The interrupted animation's tweens are released rather than left to land,
      // and the scene converges on the snapshot's own values.
      expect(harness.pendingTweenCount()).toBe(0);
      expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
      expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));
      expect(field<{ text: string }>(ctx, 'petHpText').text).toBe('700 / 1000');
    });

    it('18. never replays a batch at or below the recovered snapshot and refuses one behind it', () => {
      const { harness, scene, ctx } = createBattle(battleState({ sequence: 3 }));

      // The authoritative reconciliation: `sequence = 5`, a board the client has
      // never presented.
      recovery(harness, battleState({ sequence: 5, turn: 4, board: deliveredBoard(2) }));
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));

      // A late batch for a resolution the snapshot already contains: recorded, and
      // presented no second time (the snapshot is never followed by the animation
      // of what it superseded).
      emitBatch(harness, 5, CASCADE_BATCH);
      expect(playedPhases(ctx)).toEqual([]);
      expect(harness.pendingTweenCount()).toBe(0);

      // A batch for an *older* resolution, delivered without a matching push: the
      // held state already contains that resolution's result, so it is refused
      // whole and the recovered board is untouched.
      harness.emitUnmatchedBattleEvents(cascadeAt('battle-p03', 4));
      expect(playedPhases(ctx)).toEqual([]);
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('19. lets no delayed callback from an invalidated timeline alter the recovered board', () => {
      const { harness, scene, ctx } = createBattle();

      emitBatch(harness, 1, CASCADE_BATCH);
      expect(harness.pendingTweenCount()).toBeGreaterThan(0);

      // Every callback the abandoned timeline had queued, captured before the
      // recovery invalidates it.
      const stale = harness
        .pendingTweenRecords()
        .map((tween) => tween.completion)
        .filter((completion): completion is () => void => typeof completion === 'function');
      expect(stale.length).toBeGreaterThan(0);

      recovery(harness, battleState({ sequence: 4, board: deliveredBoard(2) }));
      const phasesAfterRecovery = playedPhases(ctx);
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));
      expect(harness.pendingTweenCount()).toBe(0);

      // They land late. Not one of them advances a phase, re-locks the scene, or
      // restores a board the snapshot replaced.
      for (const completion of stale) {
        completion();
      }

      expect(playedPhases(ctx)).toEqual(phasesAfterRecovery);
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));
      expect(harness.pendingTweenCount()).toBe(0);
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(field<number | null>(ctx, 'presentationSequence')).toBeNull();
    });

    it('20. keeps the presentation lock on the valid push-then-batch path until the batch is handled', async () => {
      const { harness, scene, ctx } = createBattle();
      const held = holdAction(harness);

      tapCell(harness, 20);
      tapCell(harness, 21);
      expect(inputLocked(scene, ctx)).toBe(true);

      // The resolution's own state push — held for the batch that sequences it, so
      // the board the gesture was made on stays on screen.
      push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));
      expect(field<number | null>(ctx, 'heldSequence')).toBe(1);
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));
      expect(inputLocked(scene, ctx)).toBe(true);

      // The matching batch: the timeline starts under the same lock, opening with
      // the exchange this client submitted and then the delivered board cycle.
      emitBatch(harness, 1, CASCADE_BATCH);
      expect(playedPhases(ctx)).toEqual(['swap 20->21']);
      expect(field<number | null>(ctx, 'presentationSequence')).toBe(1);
      expect(inputLocked(scene, ctx)).toBe(true);

      // The acknowledgement settles while the resolution plays: the lock is kept
      // for the timeline that has not finished presenting it (AF-5/AF-6).
      held.resolve({ accepted: true });
      await flush();
      expect(inputLocked(scene, ctx)).toBe(true);

      // The timeline's own end releases it, having converged the board on the
      // delivered state.
      harness.flushTweens();

      expect(inputLocked(scene, ctx)).toBe(false);
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(false);
      expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
      expect(labelsByCell(harness)).toEqual(expectedLabels(1));
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'cascade depth=1',
        'cascade depth=2',
        'settle',
        'damage player->boss',
        'retaliation boss->player',
      ]);
    });

    it('21. releases stale presentation on the battle-ended publication and fabricates no result', () => {
      const { harness, scene, ctx } = createBattle(
        battleState({ sequence: 1, board: deliveredBoard(1) })
      );
      const held = holdAction(harness);

      tapCell(harness, 20);
      tapCell(harness, 21);
      push(harness, battleState({ sequence: 2, turn: 3, board: deliveredBoard(2) }));
      emitBatch(harness, 2, CASCADE_BATCH);

      expect(field<object | null>(ctx, 'presentationTimeline')).not.toBeNull();
      expect(inputLocked(scene, ctx)).toBe(true);

      // §7.3: the authoritative snapshot cannot be recovered, so the runtime
      // publishes the loss — `null` with the `'ended'` source.
      harness.setBattleState(null, { source: 'ended' });

      // Every lock the ended battle held is released and the stale board is gone:
      // nothing is left hidden behind the empty presentation.
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(false);
      expect(field<object | null>(ctx, 'committedSwapCells')).toBeNull();
      expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
      expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
      expect(field<string>(ctx, 'orchestration')).toBe('idle');
      expect(harness.pendingTweenCount()).toBe(0);
      expect(harness.boardCells).toHaveLength(0);
      expect(field<RuntimeBattleState | null>(ctx, 'currentBattleState')).toBeNull();

      // No result is invented: the outcome of a battle that could not be recovered
      // is not fabricated into a `ResultScene` handoff, and no terminal statement
      // is claimed. The documented fallback is the runtime's own result read.
      expect(harness.sceneStarted).toEqual([]);
      expect(scene.getPresentedEvents()).toEqual([]);
      expect(field<object | null>(ctx, 'pendingOutcomeHandoff')).toBeNull();
      expect(renderedText(harness)).toContain('Battle no longer available');

      // A late answer for the ended battle cannot settle or re-lock the scene.
      held.resolve({ accepted: true });
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('22. treats a rejected action as a gameplay no-op: no recovery, no sequence bookkeeping', async () => {
      const { harness, scene, ctx } = createBattle();

      harness.setActionResult({ accepted: false, reason: 'INVALID_SWAP' });

      tapCell(harness, 20);
      tapCell(harness, 21);
      await flush();

      // The one request the scene made was the action itself: a rejection is the
      // server's own answer, so no snapshot is asked for, no resolution is
      // accepted, and no sequence is advanced (`SIGNALR_PROTOCOL.md` §5 item 2,
      // `MATCH3_RULES.md` §2.1.5).
      expect(harness.requestedActions).toHaveLength(1);
      expect(harness.requestedActions[0]).toMatchObject({ kind: 'Swap', fromCell: 20, toCell: 21 });
      expect(field<number | null>(ctx, 'acceptedResolutionSequence')).toBeNull();
      expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(false);
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(playedPhases(ctx)).toEqual([]);
      expect(scene.getPresentedEvents()).toEqual([]);
      expect(harness.sceneStarted).toEqual([]);

      // The refusal is reported as lightweight feedback, and the board on screen is
      // still the delivered one.
      expect(renderedText(harness)).toContain('INVALID_SWAP');
      expect(labelsByCell(harness)).toEqual(expectedLabels(0));
    });
  });

  describe('TASK-274 — final recovery boundary review & scene admission gate verification', () => {
    function cascadeAt(battleId: string, sequence: number): BattleEventsEnvelope {
      return { battleId, serverSequence: sequence, events: CASCADE_BATCH } as BattleEventsEnvelope;
    }

    it('Review A.1: refuses an unversioned batch when BattleScene holds no authoritative state', () => {
      const { harness, scene, ctx } = createBattle(null);
      expect(field<RuntimeBattleState | null>(ctx, 'currentBattleState')).toBeNull();

      // Emit batch without prior state
      harness.emitUnmatchedBattleEvents(cascadeAt('battle-test', 1));

      // Invariant verified: admitsBatch returns false when held === null.
      // No timeline built, no phases played, no events recorded as presented.
      expect(playedPhases(ctx)).toEqual([]);
      expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
      expect(scene.getPresentedEvents()).toEqual([]);
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('Review A.4: late batch after battle-ended publication cannot restart presentation or re-lock scene', () => {
      const { harness, scene, ctx } = createBattle(
        battleState({ sequence: 1, board: deliveredBoard(1) })
      );

      // Battle ends per BATTLE_NOT_FOUND (published as null with source: 'ended')
      harness.setBattleState(null, { source: 'ended' });
      expect(field<RuntimeBattleState | null>(ctx, 'currentBattleState')).toBeNull();
      expect(renderedText(harness)).toContain('Battle no longer available');
      expect(inputLocked(scene, ctx)).toBe(false);

      // Late batch arrives for the ended battle
      harness.emitUnmatchedBattleEvents(cascadeAt('battle-p03', 1));

      // Batch is refused: old presentation cannot restart, message remains, locks stay unlocked
      expect(playedPhases(ctx)).toEqual([]);
      expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
      expect(scene.getPresentedEvents()).toEqual([]);
      expect(renderedText(harness)).toContain('Battle no longer available');
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('Review A.5: late batch from old battle cannot affect newly active battle on scene', () => {
      const { harness, ctx } = createBattle(
        battleState({ battleId: 'battle-new', sequence: 0, board: deliveredBoard(0) })
      );

      // Late batch for old battle arrives
      harness.emitUnmatchedBattleEvents(cascadeAt('battle-old', 0));

      // Refused: cross-battle batch rejected
      expect(playedPhases(ctx)).toEqual([]);
      expect(field<object | null>(ctx, 'presentationTimeline')).toBeNull();
      expect(field<RuntimeBattleState | null>(ctx, 'currentBattleState')?.battleId).toBe('battle-new');
    });

    it('Review B.8: presentation lock is safely retained until valid recovery convergence without indefinite deadlock', async () => {
      const { harness, scene, ctx } = createBattle(
        battleState({ sequence: 1, board: deliveredBoard(1) })
      );

      let pendingAction!: { resolve: (val: { accepted: boolean }) => void };
      harness.setActionBehaviour(
        () =>
          new Promise<never>((res) => {
            pendingAction = { resolve: res as unknown as (val: { accepted: boolean }) => void };
          })
      );

      // Player swaps cells: locks input and sets resolutionPending
      tapCell(harness, 20);
      tapCell(harness, 21);

      expect(inputLocked(scene, ctx)).toBe(true);
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(true);

      // Suppose runtime receives a malformed recovery response (inconsistent envelope).
      // Runtime rejects it and publishes NO state to the scene.
      // Scene lock remains safely held waiting for valid recovery snapshot.
      expect(inputLocked(scene, ctx)).toBe(true);
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(true);

      // When subsequent valid recovery snapshot arrives:
      const recoveredState = battleState({
        sequence: 2,
        board: deliveredBoard(2),
      });
      harness.setBattleState(recoveredState, { source: 'recovery' });

      // InvalidatePendingPresentation converges presentation and releases the locks safely
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(false);
      expect(field<RuntimeBattleState | null>(ctx, 'currentBattleState')?.sequence).toBe(2);

      // Clean up in-flight promise
      pendingAction.resolve({ accepted: true });
    });

    it('Review C.1 & C.5: BATTLE_NOT_FOUND clears board and locks, fabricates no result, and provides stable fallback', () => {
      const { harness, scene, ctx } = createBattle(
        battleState({ sequence: 1, board: deliveredBoard(1) })
      );

      // Engage a lock
      (ctx as { inputLocked: boolean }).inputLocked = true;
      (ctx as { resolutionPending: boolean }).resolutionPending = true;

      // Deliver ended battle
      harness.setBattleState(null, { source: 'ended' });

      expect(inputLocked(scene, ctx)).toBe(false);
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(false);
      expect(harness.boardCells).toHaveLength(0);
      expect(renderedText(harness)).toContain('Battle no longer available');
      expect(harness.sceneStarted).toEqual([]); // No ResultScene fabricated
    });

    it('Review C.4: authoritative outcome event in admitted batch navigates to ResultScene', () => {
      const { harness } = createBattle(
        battleState({ battleId: 'battle-p03', sequence: 5, board: deliveredBoard(1) })
      );

      // Deliver matching state push, then batch with BattleWon
      harness.emitBattleEvents({
        battleId: 'battle-p03',
        serverSequence: 5,
        events: [
          {
            type: 'BattleWon',
            outcome: 'victory',
            finalBossHp: 0,
            finalPlayerHp: 800,
          },
        ],
      });

      expect(harness.sceneStarted).toHaveLength(1);
      expect(harness.sceneStarted[0]).toEqual({
        key: 'ResultScene',
        data: {
          outcome: 'victory',
          finalBossHp: 0,
          finalPlayerHp: 800,
          battleId: 'battle-p03',
        },
      });
    });
  });

  /**
   * TASK-275 — the turn banner, the confirmed-gem destruction, and the single
   * supported board transition.
   *
   * ```text
   * turn banner     one presentation-only `PLAYER TURN` / `BOSS TURN` marker per
   *                 delivered board-action boundary, labelled from that
   *                 boundary's own `actor` (`SIGNALR_PROTOCOL.md` §3.2.26 item 2)
   *                 and completed before the next phase begins (`§6.6`)
   * destruction     the cells a `MatchCreated` / `GemMatched` confirms are
   *                 cleared, and **only** those: a cell whose rendered content is
   *                 not the content the delivered event names is left alone,
   *                 because the contract sends no per-pass board for a cascade
   *                 pass to be checked against (`SIGNALR_PROTOCOL.md` §3.1
   *                 item 2) — `§6.2`, `§6.4`
   * board           exactly one transition per round, onto the accepted
   *                 authoritative board, where a cleared cell is re-labelled from
   *                 the delivered cell with the refill entrance — `§6.3`'s
   *                 supported subset: no gem view is ever moved between cells
   *                 except by a delivered `SwapStarted` (`§7`)
   * recovery        a snapshot during any of those phases converges the board,
   *                 restores every cleared cell, and cancels the phase's own
   *                 resources (`§8`)
   * ```
   *
   * **The fixtures state delivered values only.** The board below is written out
   * cell by cell so one delivered `gemType` agrees with the rendered content (pass
   * 1, whose board the client holds) and another disagrees with it (a later pass,
   * whose board the contract never sends). Nothing here asserts a value the client
   * computes, because it computes none.
   */
  describe('TASK-275 — turn banners, confirmed-gem destruction & the single board transition', () => {
    /** The label each Gem type renders as (`GEM_LABELS`, spelled once). */
    const BOARD_LABELS: Readonly<Record<string, string>> = Object.freeze({
      ATK: 'ATK',
      DEF: 'DEF',
      HP: 'HP',
      POWER: 'PWR',
    });

    /**
     * The fixture board: all cells `POWER`, except a `Straight` ATK match at
     * 24–26 and an HP cell at 32.
     *
     * It is written out rather than rotated so one delivered event can be made to
     * **agree** with the rendered content (24–26) and another to **disagree** with
     * it (32) — which is exactly the difference between pass 1, whose board the
     * client holds, and a later pass, whose board the contract never sends.
     */
    function board(): RuntimeBoard {
      const cells: Array<{ gemType: string }> = Array.from(
        { length: BOARD_COLUMNS * BOARD_COLUMNS },
        () => ({ gemType: 'POWER' })
      );

      for (const index of [24, 25, 26]) {
        cells[index] = { gemType: 'ATK' };
      }
      cells[32] = { gemType: 'HP' };

      return { cells };
    }

    /** What the fixture board must render as, in §1.0 index order. */
    function boardLabels(): string[] {
      return board().cells.map((cell) => BOARD_LABELS[cell.gemType] ?? cell.gemType);
    }

    /** One board cell's view, as the scene holds it. */
    interface ViewLike {
      content: string;
      cleared: boolean;
      tile: { x: number; y: number; visible: boolean };
      label: { visible: boolean };
    }

    function views(ctx: object): Array<ViewLike | null> {
      return field<Array<ViewLike | null>>(ctx, 'gemViews');
    }

    /** The indices whose view a delivered clear has emptied. */
    function clearedCells(ctx: object): number[] {
      return views(ctx)
        .map((view, index) => (view?.cleared ? index : -1))
        .filter((index) => index >= 0);
    }

    /**
     * Every view that is still showing content sits on its own §1.0 cell.
     *
     * The delivered exchange is the one movement the contract authorises, so a
     * view a Swap moved is checked against the cell it now *occupies*; no other
     * view may leave its cell, which is what makes "the client derived no gravity
     * destination" observable (`TASK-275` §6.3, §7).
     */
    function everyViewOnItsOwnCell(ctx: object): boolean {
      return views(ctx).every((view, index) => {
        if (!view || view.cleared) {
          return true;
        }

        return cellIndexOf(view.tile.x, view.tile.y) === index;
      });
    }

    /** Plays the timeline to its end, phase by phase. */
    function playAllTweens(harness: ReturnType<typeof createSceneHarness>): void {
      for (let step = 0; step < 500 && harness.pendingTweenCount() > 0; step++) {
        harness.flushTweens(1);
      }
    }

    /** The timeline the scene built for one batch, read before it is released. */
    interface TimelinePhaseLike {
      kind: string;
      detail: string;
      actor: string | null;
      holdMs: number;
      events: unknown[];
    }

    function builtTimeline(ctx: object): TimelinePhaseLike[] {
      return [
        ...(field<TimelinePhaseLike[] | null>(ctx, 'presentationTimeline') ?? []),
      ];
    }

    /**
     * A round's board cycle: one committed Player Swap whose first pass clears
     * 24–26, and a second delivered pass that names 32 as a DEF clear — a cell the
     * board on screen renders as HP.
     */
    const MATCH_CLEAR_BATCH: unknown[] = [
      { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
      { type: 'SwapResolved', outcome: 'committed' },
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
      { type: 'GemMatched', cellIndex: 25, gemType: 'ATK' },
      { type: 'GemMatched', cellIndex: 26, gemType: 'ATK' },
      { type: 'CascadeCreated', cascadeDepth: 1 },
      { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'DEF', cascadeDepth: 2 },
      { type: 'GemMatched', cellIndex: 32, gemType: 'DEF' },
    ];

    /** The same round with both actors' board cycles, in the delivered order. */
    const BOTH_TURNS_BATCH: unknown[] = [
      { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
      { type: 'SwapResolved', outcome: 'committed' },
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
      { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
      { type: 'SwapStarted', actor: 'boss', fromCell: 27, toCell: 28 },
      { type: 'SwapResolved', outcome: 'committed' },
      { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'HP', cascadeDepth: 1 },
      { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
      { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
    ];

    /** A battle already holding the fixture board, with one action in flight. */
    function battleWithHeldAction(): {
      harness: ReturnType<typeof createSceneHarness>;
      scene: BattleScene;
      ctx: object;
      resolveAction: () => void;
    } {
      const { harness, scene, ctx } = createBattle(battleState({ sequence: 0, board: board() }));
      const held = holdAction(harness);

      tapCell(harness, 20);
      tapCell(harness, 21);

      return { harness, scene, ctx, resolveAction: () => held.resolve({ accepted: true }) };
    }

    it('7. plays one banner per delivered board-action boundary, labelled from that boundary’s own actor', () => {
      const { harness, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, BOTH_TURNS_BATCH);

      playAllTweens(harness);

      // The boss banner precedes the cycle it introduces and stands in for no gameplay
      // step: the delivered order of the round is otherwise untouched.
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'damage player->boss',
        'banner BOSS TURN',
        'swap 27->28',
        'match depth=1',
        'settle',
        'retaliation boss->player',
      ]);

      // The label the player reads names the side the wire delivered, and the board
      // is the accepted authoritative board once the timeline is done.
      expect(renderedText(harness)).toContain('BOSS TURN');
      expect(labelsByCell(harness)).toEqual(boardLabels());

      resolveAction();
    });

    it('7b. starts the next phase only after the banner phase has completed', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, BOTH_TURNS_BATCH);

      // Play until the boss banner phase starts.
      playUntil(harness, ctx, 4);
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'damage player->boss',
        'banner BOSS TURN',
      ]);
      expect(inputLocked(scene, ctx)).toBe(true);

      // Its own two beats (the enter and the exit) complete without advancing the
      // timeline: the exchange the banner introduces has not begun.
      harness.flushTweens(2);
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'damage player->boss',
        'banner BOSS TURN',
      ]);

      // The banner's pacing tween is what completes the phase, and only then does
      // the boss swap step play.
      harness.flushTweens(1);
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'damage player->boss',
        'banner BOSS TURN',
        'swap 27->28',
      ]);

      resolveAction();
    });

    it('7c. plays no banner for a boundary that names no side, and none for a batch that delivers none', () => {
      const unnamed = battleWithHeldAction();

      push(unnamed.harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(unnamed.harness, 1, [
        { type: 'SwapStarted', actor: 'nobody', fromCell: 20, toCell: 21 },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]);

      playAllTweens(unnamed.harness);

      // An `actor` outside the contract's two values names no side, so no banner is
      // shown for it — the step itself still presents the delivered cells.
      expect(playedPhases(unnamed.ctx).some((detail) => detail.startsWith('banner'))).toBe(false);
      expect(playedPhases(unnamed.ctx)).toContain('swap 20->21');
      unnamed.resolveAction();

      const boundaryless = battleWithHeldAction();

      push(boundaryless.harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(boundaryless.harness, 1, [
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]);

      playAllTweens(boundaryless.harness);

      // No delivered boundary at all: the submitted pair is presented side-free and
      // no turn is claimed (`TASK-275` §6.6: no invented turn/phase signal).
      expect(playedPhases(boundaryless.ctx).some((detail) => detail.startsWith('banner'))).toBe(false);
      expect(playedPhases(boundaryless.ctx)).toContain('swap 20->21');
      boundaryless.resolveAction();
    });

    it('7d. adds no gameplay event, no sequence increment and no state for the banner', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, BOTH_TURNS_BATCH);

      // The banner is a phase of its own carrying no delivered event, and the
      // diagnostic event record holds exactly the batch's own events.
      const banner = builtTimeline(ctx).find((phase) => phase.kind === 'banner');
      expect(banner).toBeDefined();
      expect(banner?.events).toEqual([]);
      expect(banner?.actor).toBe('boss');
      expect(scene.getPresentedEvents()).toHaveLength(BOTH_TURNS_BATCH.length);
      expect(scene.getPresentedEvents().some((line) => line.includes('banner'))).toBe(false);

      // No second request is made, and the delivered turn is the state push's own
      // value — the banner writes no state and advances no sequence.
      expect(harness.actionInvocations).toHaveLength(1);
      expect(harness.actionInvocations[0]).toMatchObject({ fromCell: 20, toCell: 21 });
      expect(field<RuntimeBattleState | null>(ctx, 'currentBattleState')?.turn).toBe(2);

      playAllTweens(harness);
      resolveAction();
    });

    it('3. clears exactly the cells the delivered events confirm, in the order the phases play', () => {
      const { harness, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, MATCH_CLEAR_BATCH);

      // Nothing is removed before the match step plays.
      expect(clearedCells(ctx)).toEqual([]);

      // The exchange and the first pass have played: the pass-1 cells
      // are gone, with their tile and label hidden, and the board has not yet been
      // converged onto the round's board.
      playUntil(harness, ctx, 2);
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
      ]);
      expect(clearedCells(ctx)).toEqual([24, 25, 26]);
      for (const index of [24, 25, 26]) {
        expect(views(ctx)[index]?.tile.visible).toBe(false);
        expect(views(ctx)[index]?.label.visible).toBe(false);
      }

      // The second delivered pass: its `MatchCreated` / `GemMatched` name cell 32 as
      // a DEF clear, and the cell renders HP. No per-pass board is delivered for the
      // pass to be checked against, so the clear is refused and the cell is left
      // alone — the client fabricates no cascade state (`TASK-275` §3, §6.4).
      playUntil(harness, ctx, 3);
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'cascade depth=1',
      ]);
      expect(clearedCells(ctx)).toEqual([24, 25, 26]);
      expect(views(ctx)[32]?.tile.visible).toBe(true);
      expect(views(ctx)[32]?.cleared).toBe(false);

      // The round's single board transition is what puts every cell back, from the
      // accepted authoritative board.
      playUntil(harness, ctx, 4);
      playAllTweens(harness);
      expect(clearedCells(ctx)).toEqual([]);
      expect(labelsByCell(harness)).toEqual(boardLabels());

      resolveAction();
    });

    it('4. converges the board in one transition after the clear phases, and never moves a view between cells', () => {
      const { harness, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, MATCH_CLEAR_BATCH);

      // Once the exchange has played, every surviving view sits on its own cell: no
      // view has left it for a gravity destination the client would have had to
      // compute. The invariant is asserted again after the clear phases and after
      // the transition — only a delivered `SwapStarted` may move a view.
      playUntil(harness, ctx, 2);
      expect(playedPhases(ctx)).toContain('swap 20->21');
      expect(everyViewOnItsOwnCell(ctx)).toBe(true);

      playUntil(harness, ctx, 3);
      expect(everyViewOnItsOwnCell(ctx)).toBe(true);

      // Every clear phase has played and the transition has not: the board beneath
      // the cleared cells is still the pre-round one, so a pass could not have been
      // resolved against a board the client invented.
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'cascade depth=1',
      ]);
      expect(clearedCells(ctx)).toEqual([24, 25, 26]);

      // The transition is exactly one phase, and it is played after the round's
      // board cycle.
      playUntil(harness, ctx, 4);
      playAllTweens(harness);
      const phases = playedPhases(ctx);
      expect(phases.filter((detail) => detail === 'settle')).toHaveLength(1);
      expect(phases.indexOf('settle')).toBeGreaterThan(phases.indexOf('cascade depth=1'));

      expect(everyViewOnItsOwnCell(ctx)).toBe(true);
      expect(field<number | null>(ctx, 'settledBoardSequence')).toBe(1);
      expect(field<string | null>(ctx, 'settledBattleId')).toBe(BATTLE_ID);
      expect(labelsByCell(harness)).toEqual(boardLabels());

      resolveAction();
    });

    it('10. converges and releases the lock when a recovery snapshot arrives during the turn banner', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, BOTH_TURNS_BATCH);
      playUntil(harness, ctx, 4);

      expect(playedPhases(ctx)).toContain('banner BOSS TURN');
      expect(inputLocked(scene, ctx)).toBe(true);

      // The banner's own text object is among the timeline's owned effects while it plays.
      const bannerEffects = [...field<object[]>(ctx, 'timelineEffects')];
      const bannerEffect = bannerEffects.find(
        (effect) => (effect as { text?: string }).text === 'BOSS TURN'
      );
      expect(bannerEffect).toBeDefined();

      recovery(harness, battleState({ sequence: 2, turn: 3, board: deliveredBoard(2) }));

      // The banner's timeline is abandoned where it stood: its phase never advanced,
      // its effect was cancelled with it, and the snapshot's board is on screen.
      expect(playedPhases(ctx)).toContain('banner BOSS TURN');
      expect(harness.killedTweenTargets).toContain(bannerEffect);
      expect(field<object[]>(ctx, 'timelineEffects')).toEqual([]);
      expect(harness.pendingTweenCount()).toBe(0);
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(field<string>(ctx, 'orchestration')).toBe('idle');
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));

      // The answer to the action that was in flight cannot re-lock the scene.
      resolveAction();
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('11. converges and releases the lock when a recovery snapshot arrives during the swap phase', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, MATCH_CLEAR_BATCH);
      playUntil(harness, ctx, 1);

      expect(playedPhases(ctx)).toEqual(['swap 20->21']);
      expect(clearedCells(ctx)).toEqual([]);

      recovery(harness, battleState({ sequence: 2, turn: 3, board: deliveredBoard(2) }));

      expect(playedPhases(ctx)).not.toContain('match depth=1');
      expect(harness.pendingTweenCount()).toBe(0);
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));

      resolveAction();
    });

    it('12. restores every cleared cell when a recovery snapshot arrives during the destruction', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, MATCH_CLEAR_BATCH);
      playUntil(harness, ctx, 2);

      expect(clearedCells(ctx)).toEqual([24, 25, 26]);

      recovery(harness, battleState({ sequence: 2, turn: 3, board: deliveredBoard(2) }));

      // A recovered snapshot supersedes the obsolete presentation: no cell may stay
      // empty over it, and the board on screen is the snapshot's.
      expect(clearedCells(ctx)).toEqual([]);
      for (const view of views(ctx)) {
        if (view) {
          expect(view.tile.visible).toBe(true);
          expect(view.label.visible).toBe(true);
        }
      }
      expect(labelsByCell(harness)).toEqual(expectedLabels(2));
      expect(field<number | null>(ctx, 'settledBoardSequence')).toBe(2);
      expect(inputLocked(scene, ctx)).toBe(false);

      resolveAction();
    });

    it('13. converges and releases the lock when a recovery snapshot arrives during the refill transition', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, MATCH_CLEAR_BATCH);
      playUntil(harness, ctx, 4);

      // The transition has played: the board is converged and its refill tweens are
      // in flight.
      expect(playedPhases(ctx)).toContain('settle');
      expect(harness.pendingTweenCount()).toBeGreaterThan(0);

      recovery(harness, battleState({ sequence: 2, turn: 3, board: deliveredBoard(3) }));

      expect(harness.pendingTweenCount()).toBe(0);
      expect(field<string>(ctx, 'orchestration')).toBe('idle');
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(labelsByCell(harness)).toEqual(expectedLabels(3));

      resolveAction();
    });

    it('20. keeps input locked through the banner, the destruction and the transition, and releases it at the end', async () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, MATCH_CLEAR_BATCH);

      // The action's acknowledgement settles while its resolution plays — the
      // server's own order (`SIGNALR_PROTOCOL.md` §3.1) — so the guard that is
      // observed below is the presentation's, not the request's.
      resolveAction();
      await flush();

      for (const phaseCount of [1, 2, 3, 4]) {
        playUntil(harness, ctx, phaseCount);
        expect(inputLocked(scene, ctx), `after ${phaseCount} phase(s)`).toBe(true);
      }

      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'cascade depth=1',
        'settle',
      ]);

      playAllTweens(harness);

      expect(inputLocked(scene, ctx)).toBe(false);
      expect(field<string>(ctx, 'orchestration')).toBe('idle');
      expect(harness.pendingTweenCount()).toBe(0);
      expect(labelsByCell(harness)).toEqual(boardLabels());
    });

    it('17. causes no duplicate banner, clear or effect when the same batch is delivered twice', () => {
      const { harness, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, BOTH_TURNS_BATCH);
      playAllTweens(harness);

      const phasesAfterFirstDelivery = [...playedPhases(ctx)];
      const bannersAfterFirstDelivery = harness.texts.filter(
        (entry) => entry.text === 'BOSS TURN'
      ).length;
      expect(bannersAfterFirstDelivery).toBe(1);
      expect(clearedCells(ctx)).toEqual([]);

      // The same resolution, delivered again: recorded, and presented no second time
      // (SIGNALR_PROTOCOL.md §3 item 2, GAME_STATE.md §5.1 item 3).
      emitBatch(harness, 1, BOTH_TURNS_BATCH);

      expect(playedPhases(ctx)).toEqual(phasesAfterFirstDelivery);
      expect(harness.pendingTweenCount()).toBe(0);
      expect(harness.texts.filter((entry) => entry.text === 'BOSS TURN').length).toBe(
        bannersAfterFirstDelivery
      );
      expect(labelsByCell(harness)).toEqual(boardLabels());

      resolveAction();
    });

    it('22. destroys the banner’s own resource when the scene goes down mid-banner', () => {
      const { harness, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, BOTH_TURNS_BATCH);
      playUntil(harness, ctx, 4);

      const bannerEffects = [...field<object[]>(ctx, 'timelineEffects')];
      const bannerEffect = bannerEffects.find(
        (effect) => (effect as { text?: string }).text === 'BOSS TURN'
      );
      expect(bannerEffect).toBeDefined();

      harness.shutdownScene(ctx);

      expect(harness.pendingTweenCount()).toBe(0);
      expect(field<object[]>(ctx, 'timelineEffects')).toEqual([]);
      expect(field<boolean>(ctx, 'resolutionPending')).toBe(false);
      expect(field<number | null>(ctx, 'heldSequence')).toBeNull();
      expect(field<string>(ctx, 'orchestration')).toBe('idle');

      resolveAction();
    });

    // =====================================================================
    // ADR-027 — the delivered per-pass Gravity + Spawn report
    // (GAME_EVENTS.md §1.1 item 8, §2; SIGNALR_PROTOCOL.md §3.2.28)
    // =====================================================================

    /**
     * A round whose first pass reports its own completed Gravity + Spawn transition:
     * the ATK row at 24–26 is cleared, three survivors fall in each of the three
     * columns (with every falling Gem's destination also being another Gem's origin),
     * and the three vacated column tops are refilled.
     *
     * The delivered batch is the fixture, not a simulated resolution: the client's
     * obligation is to present the transition the batch reports, and every value it
     * applies is a member of this batch (`AGENTS.md` §10).
     */
    const PER_PASS_BATCH: unknown[] = [
      { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
      { type: 'SwapResolved', outcome: 'committed' },
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
      { type: 'GemMatched', cellIndex: 25, gemType: 'ATK' },
      { type: 'GemMatched', cellIndex: 26, gemType: 'ATK' },
      {
        type: 'CascadeStepResolved',
        passDepth: 1,
        dropMoves: [
          { fromCell: 16, toCell: 24 },
          { fromCell: 8, toCell: 16 },
          { fromCell: 0, toCell: 8 },
          { fromCell: 17, toCell: 25 },
          { fromCell: 9, toCell: 17 },
          { fromCell: 1, toCell: 9 },
          { fromCell: 18, toCell: 26 },
          { fromCell: 10, toCell: 18 },
          { fromCell: 2, toCell: 10 },
        ],
        spawnedGems: [
          { cellIndex: 0, gemType: 'DEF' },
          { cellIndex: 1, gemType: 'HP' },
          { cellIndex: 2, gemType: 'ATK' },
        ],
      },
    ];

    /** The board that pass produces: 24–26 refilled by the fallers, 0–2 by the spawns. */
    function afterPassBoard(): RuntimeBoard {
      const cells = board().cells.map((cell) => ({ ...cell }));

      for (const index of [24, 25, 26]) {
        // The Gem that fell into each cleared cell came from the cell three rows
        // above it, which the fixture board renders as POWER.
        cells[index] = { gemType: 'POWER' };
      }

      cells[0] = { gemType: 'DEF' };
      cells[1] = { gemType: 'HP' };
      cells[2] = { gemType: 'ATK' };

      return { cells };
    }

    /** The delivered board's own labels, in §1.0 index order. */
    function labelsForCells(next: RuntimeBoard): string[] {
      return next.cells.map((cell) => BOARD_LABELS[cell.gemType] ?? cell.gemType);
    }

    it('23. plays a delivered pass’s Fall and Refill in order, and moves only the views it named', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: afterPassBoard() }));
      emitBatch(harness, 1, PER_PASS_BATCH);

      const before = views(ctx).map((view) => view);

      // The exchange, the match, the pass's Fall and its Refill are four separate
      // phases in the delivered order, and the round's single board transition closes
      // the cycle (SIGNALR_PROTOCOL.md §3.2.28 item 5).
      playUntil(harness, ctx, 3);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1', 'gravity depth=1']);
      expect(inputLocked(scene, ctx)).toBe(true);

      // The Fall moved exactly the views the report named, through the chain each
      // column's movement describes: a destination that is also another move's origin
      // takes the arriving view (0→8, 8→16, 16→24).
      expect(views(ctx)[24]).toBe(before[16]);
      expect(views(ctx)[16]).toBe(before[8]);
      expect(views(ctx)[8]).toBe(before[0]);
      expect(views(ctx)[25]).toBe(before[17]);
      expect(views(ctx)[26]).toBe(before[18]);

      // The vacated tops hold nothing until the Refill step provides their Gem.
      expect(views(ctx)[0]).toBeNull();
      expect(views(ctx)[1]).toBeNull();
      expect(views(ctx)[2]).toBeNull();

      playUntil(harness, ctx, 4);
      expect(playedPhases(ctx)).toContain('refill depth=1');

      // The Refill filled every vacated cell with the delivered Gem type and left no
      // cell without a view.
      expect(views(ctx).every((view) => view !== null)).toBe(true);
      expect(views(ctx)[0]?.content).toBe('DEF');
      expect(views(ctx)[1]?.content).toBe('HP');
      expect(views(ctx)[2]?.content).toBe('ATK');

      playAllTweens(harness);

      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'gravity depth=1',
        'refill depth=1',
        'settle',
      ]);

      // The board is the delivered one, drawn on the same 64 persistent views: three
      // falls displaced three views and the three spawns re-used them, so no cell's
      // pair was destroyed or recreated (TASK-245's persistent-view invariant).
      expect(labelsByCell(harness)).toEqual(labelsForCells(afterPassBoard()));
      expect(harness.boardCells).toHaveLength(128);
      expect(everyViewOnItsOwnCell(ctx)).toBe(true);

      // The guard stays engaged while the action's own answer is still outstanding
      // (TASK-252), so the timeline's end is not itself an unlock.
      expect(inputLocked(scene, ctx)).toBe(true);
      resolveAction();
    });

    it('24. presents a later pass only after the earlier pass’s Refill has completed', () => {
      const { harness, ctx, resolveAction } = battleWithHeldAction();

      const twoPasses = [
        ...PER_PASS_BATCH,
        { type: 'CascadeCreated', cascadeDepth: 1 },
        { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'HP', cascadeDepth: 2 },
        { type: 'GemMatched', cellIndex: 32, gemType: 'HP' },
        {
          type: 'CascadeStepResolved',
          passDepth: 2,
          dropMoves: [
            { fromCell: 24, toCell: 32 },
            { fromCell: 16, toCell: 24 },
            { fromCell: 8, toCell: 16 },
            { fromCell: 0, toCell: 8 },
          ],
          spawnedGems: [{ cellIndex: 0, gemType: 'ATK' }],
        },
      ];

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, twoPasses);

      // The first pass's Fall has played; its Refill has not.
      playUntil(harness, ctx, 3);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1', 'gravity depth=1']);

      // With the first pass's Refill complete, the second pass — detected on exactly
      // the board the first pass's own transition produced — is what plays next.
      playUntil(harness, ctx, 4);
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'gravity depth=1',
        'refill depth=1',
      ]);

      playUntil(harness, ctx, 5);
      expect(playedPhases(ctx)[4]).toBe('cascade depth=1');

      playUntil(harness, ctx, 6);
      expect(playedPhases(ctx)[5]).toBe('gravity depth=2');

      playAllTweens(harness);
      expect(playedPhases(ctx)).toContain('refill depth=2');

      // One transition per pass, in the delivered order, whichever depths the batch
      // reports (SIGNALR_PROTOCOL.md §3.2.28 item 5).
      const phases = playedPhases(ctx);
      expect(phases.indexOf('gravity depth=2')).toBeGreaterThan(phases.indexOf('refill depth=1'));
      expect(phases.filter((detail) => detail === 'settle')).toHaveLength(1);

      resolveAction();
    });

    it('25. never lets the boss act on an incompletely resolved board', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      // One round: the Player's board action — whose pass reports its own transition —
      // then the Boss Response Phase's own board action (ADR-026 §2, GAME_RULES.md §17
      // step 18a), each opening with its own delivered boundary.
      const round = [
        ...PER_PASS_BATCH,
        { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
        { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
        { type: 'SwapStarted', actor: 'boss', fromCell: 27, toCell: 28 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [33, 34, 35], gemType: 'HP', cascadeDepth: 1 },
        { type: 'GemMatched', cellIndex: 33, gemType: 'HP' },
        {
          type: 'CascadeStepResolved',
          passDepth: 1,
          dropMoves: [{ fromCell: 25, toCell: 33 }],
          spawnedGems: [{ cellIndex: 25, gemType: 'POWER' }],
        },
        { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
        { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
      ];

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, round);

      playAllTweens(harness);

      const phases = playedPhases(ctx);

      // The Player's own board work — including the Fall and Refill its pass reports —
      // is complete before the Boss's turn is announced or acted on: the boss banner
      // and the boss's own exchange both follow the player's Refill.
      expect(phases.indexOf('refill depth=1')).toBeGreaterThan(-1);
      expect(phases.indexOf('banner BOSS TURN')).toBeGreaterThan(phases.indexOf('refill depth=1'));
      expect(phases.indexOf('swap 27->28')).toBeGreaterThan(phases.indexOf('refill depth=1'));

      // And no combat stage is presented over an unresolved player board: the player's
      // damage follows the player's own pass report.
      expect(phases.indexOf('damage player->boss')).toBeGreaterThan(phases.indexOf('refill depth=1'));

      expect(labelsByCell(harness)).not.toEqual([]);

      expect(inputLocked(scene, ctx)).toBe(true);
      resolveAction();
    });

    it('26. replays no Fall and no Refill for a duplicate batch', () => {
      const { harness, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: afterPassBoard() }));
      emitBatch(harness, 1, PER_PASS_BATCH);
      playAllTweens(harness);

      const phasesAfterFirstDelivery = [...playedPhases(ctx)];
      const positionsAfterFirstDelivery = views(ctx).map((view) => view);

      // The identical resolution, delivered again: recorded, and presented no second
      // time — no second Fall, no second Refill, and no view moves twice.
      emitBatch(harness, 1, PER_PASS_BATCH);

      expect(playedPhases(ctx)).toEqual(phasesAfterFirstDelivery);
      expect(views(ctx).map((view) => view)).toEqual(positionsAfterFirstDelivery);
      expect(harness.pendingTweenCount()).toBe(0);
      expect(labelsByCell(harness)).toEqual(labelsForCells(afterPassBoard()));

      resolveAction();
    });

    it('27. refuses a malformed pass report, keeps the fall out of it, and converges on the board', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      const malformed = [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
        { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
        // A destination that is not a §1.0 cell: nothing about this item may be
        // applied, and nothing may be repaired by inference (SIGNALR_PROTOCOL.md
        // §3.2.28 item 8).
        {
          type: 'CascadeStepResolved',
          passDepth: 1,
          dropMoves: [{ fromCell: 16, toCell: 99 }],
          spawnedGems: [{ cellIndex: 0, gemType: 'DEF' }],
        },
      ];

      push(harness, battleState({ sequence: 1, turn: 2, board: afterPassBoard() }));
      emitBatch(harness, 1, malformed);

      playAllTweens(harness);

      // The item presented nothing at all: no Fall and no Refill phase exists, and no
      // interaction lock is left behind once the action's own answer settles.
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1', 'settle']);
      expect(harness.pendingTweenCount()).toBe(0);

      // The board on screen is still the delivered one, converged by the single
      // transition the delivery always provides.
      expect(labelsByCell(harness)).toEqual(labelsForCells(afterPassBoard()));
      expect(views(ctx).every((view) => view !== null)).toBe(true);

      // A refused item leaves the resolution's own lock lifecycle untouched: the guard
      // is still the action's, and the item neither held nor released it.
      expect(inputLocked(scene, ctx)).toBe(true);
      resolveAction();
    });

    it('28. converges on a recovery snapshot that arrives between a pass’s Fall and its Refill', () => {
      const { harness, scene, ctx, resolveAction } = battleWithHeldAction();

      push(harness, battleState({ sequence: 1, turn: 2, board: afterPassBoard() }));
      emitBatch(harness, 1, PER_PASS_BATCH);

      // The Fall has played: three cells are vacated and the views have moved, and the
      // pass's Refill has not run.
      playUntil(harness, ctx, 3);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1', 'gravity depth=1']);
      expect(views(ctx)[0]).toBeNull();

      // The authoritative snapshot supersedes the presentation: it converges the board
      // immediately, leaves no cell empty, cancels the pass's pacing, and releases the
      // lock exactly once (ADR-008, SIGNALR_PROTOCOL.md §7).
      recovery(harness, battleState({ sequence: 2, turn: 3, board: board() }));

      expect(playedPhases(ctx)).not.toContain('refill depth=1');
      expect(views(ctx).every((view) => view !== null)).toBe(true);
      expect(everyViewOnItsOwnCell(ctx)).toBe(true);
      expect(labelsByCell(harness)).toEqual(boardLabels());
      expect(harness.pendingTweenCount()).toBe(0);
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(field<string>(ctx, 'orchestration')).toBe('idle');
      expect(field<object[]>(ctx, 'refillableViews')).toEqual([]);

      // No board-layer object was leaked or recreated by the abandoned pass.
      expect(harness.boardCells).toHaveLength(128);

      resolveAction();
    });

    it('29. re-places the very view a top-row clear emptied when the pass’s Refill names it', () => {
      const { harness, ctx, resolveAction } = battleWithHeldAction();

      // A pass whose cleared cells are the **tops** of columns 0–2: nothing can fall
      // into them, so the delivered report carries no `dropMoves` for those columns
      // and one `spawnedGems` entry per cell. Each cleared cell already had a view,
      // hidden by the pass's own Match step, because no move took it — the Refill must
      // re-place that same view rather than abandon it and allocate a new pair
      // (TASK-245's persistent-view invariant, asserted as the 128-object board).
      const topRowPass = [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [0, 1, 2], gemType: 'POWER', cascadeDepth: 1 },
        { type: 'GemMatched', cellIndex: 0, gemType: 'POWER' },
        { type: 'GemMatched', cellIndex: 1, gemType: 'POWER' },
        { type: 'GemMatched', cellIndex: 2, gemType: 'POWER' },
        {
          type: 'CascadeStepResolved',
          passDepth: 1,
          spawnedGems: [
            { cellIndex: 0, gemType: 'POWER' },
            { cellIndex: 1, gemType: 'POWER' },
            { cellIndex: 2, gemType: 'POWER' },
          ],
        },
      ];

      push(harness, battleState({ sequence: 1, turn: 2, board: board() }));
      emitBatch(harness, 1, topRowPass);

      const before = views(ctx).map((view) => view);

      playAllTweens(harness);

      // The pass still presents its Refill: the cells it named hold the Gem it named.
      expect(playedPhases(ctx)).toContain('refill depth=1');
      expect(views(ctx).every((view) => view !== null)).toBe(true);
      expect(labelsByCell(harness)).toEqual(boardLabels());

      // And it did so on the views the clear emptied: the three column tops kept their
      // own pair, no view was abandoned in the layer, and the board is still drawn by
      // the same 128 persistent objects.
      expect(views(ctx)[0]).toBe(before[0]);
      expect(views(ctx)[1]).toBe(before[1]);
      expect(views(ctx)[2]).toBe(before[2]);
      expect(harness.boardCells).toHaveLength(128);

      resolveAction();
    });

    // =====================================================================
    // Match tiers resolve as ordinary confirmed matches (TASK-293 / ADR-028)
    //
    // GAME_STATE.md §2.1.2 item 4 / MATCH3_RULES.md §1.A: a Match-4, Match-5 or
    // L/T match resolves as the confirmed match it is. No tier creates, places,
    // activates, detonates, chains, or area-clears anything, so no presentation
    // path for such an effect exists — the Match step only flashes and clears the
    // cells the delivered events name.
    // =====================================================================

    /**
     * Every way the scene's 64 cell views disagree with themselves at one instant.
     *
     * ```text
     * one view at two cells      a view the occupant map bound twice — the state a
     *                            destroyed-but-referenced sprite would leave
     * hidden but holding content a view the board cannot account for
     * hidden but not `cleared`   a half state no presentation step may produce
     * ```
     */
    function viewIncoherences(ctx: object): string[] {
      const list = views(ctx);
      const problems: string[] = [];

      if (list.length !== BOARD_COLUMNS * BOARD_COLUMNS) {
        problems.push(`the occupant map holds ${list.length} entries`);
      }

      const seen = new Map<object, number>();

      list.forEach((view, index) => {
        if (!view) {
          return;
        }

        const first = seen.get(view);
        if (first !== undefined) {
          problems.push(`one view occupies both cell ${first} and cell ${index}`);
        } else {
          seen.set(view, index);
        }

        if (view.cleared !== (view.content === '')) {
          problems.push(`cell ${index} cleared=${view.cleared} holding "${view.content}"`);
        }

        if (view.cleared !== !view.tile.visible) {
          problems.push(`cell ${index} cleared=${view.cleared} tile.visible=${view.tile.visible}`);
        }

        if (view.label.visible !== view.tile.visible) {
          problems.push(`cell ${index} label/tile visibility disagree`);
        }
      });

      return problems;
    }

    /** Every cell index whose view the occupant map does not carry. */
    function cellsWithoutView(ctx: object): number[] {
      return views(ctx)
        .map((view, index) => (view === null ? index : -1))
        .filter((index) => index >= 0);
    }

    /** One ordinary board: POWER everywhere, ATK on the given matched cells. */
    function atkMatchBoard(matched: readonly number[]): RuntimeBoard {
      const cells: Array<{ gemType: string }> = Array.from(
        { length: BOARD_COLUMNS * BOARD_COLUMNS },
        () => ({ gemType: 'POWER' })
      );

      for (const index of matched) {
        cells[index] = { gemType: 'ATK' };
      }

      return { cells };
    }

    /** Battle holding the given board, with one action in flight. */
    function battleOnBoardWithHeldAction(initialBoard: RuntimeBoard): {
      harness: ReturnType<typeof createSceneHarness>;
      scene: BattleScene;
      ctx: object;
      resolveAction: () => void;
    } {
      const { harness, scene, ctx } = createBattle(battleState({ sequence: 0, board: initialBoard }));
      const held = holdAction(harness);

      tapCell(harness, 20);
      tapCell(harness, 21);

      return { harness, scene, ctx, resolveAction: () => held.resolve({ accepted: true }) };
    }

    /** The delivered Gravity of one column that lost the rows the Match cleared. */
    function columnDropMoves(column: number, clearedRows: readonly number[]): unknown[] {
      const contents = Array.from({ length: BOARD_COLUMNS }, (_, row) => row);
      const survivors = contents.filter((row) => !clearedRows.includes(row));
      const moves: Array<{ fromCell: number; toCell: number }> = [];

      survivors.forEach((row, position) => {
        const destinationRow = position + clearedRows.length;

        if (destinationRow !== row) {
          moves.push({
            fromCell: row * BOARD_COLUMNS + column,
            toCell: destinationRow * BOARD_COLUMNS + column,
          });
        }
      });

      return moves;
    }

    /** One `CascadeStepResolved` for a pass that emptied the given rows of three columns. */
    function passReport(
      passDepth: number,
      clearedByColumn: ReadonlyArray<readonly number[]>,
      spawned: ReadonlyArray<{ cellIndex: number; gemType: string }>
    ): unknown {
      return {
        type: 'CascadeStepResolved',
        passDepth,
        dropMoves: clearedByColumn.flatMap((rows, column) => columnDropMoves(column, rows)),
        spawnedGems: spawned,
      };
    }

    /**
     * The board a single-pass round leaves: POWER everywhere except the three
     * spawned column heads, which are the only cells whose content the pass wrote.
     */
    function boardAfterOnePass(
      spawned: ReadonlyArray<{ cellIndex: number; gemType: string }>
    ): RuntimeBoard {
      const cells: Array<{ gemType: string }> = Array.from(
        { length: BOARD_COLUMNS * BOARD_COLUMNS },
        () => ({ gemType: 'POWER' })
      );

      for (const gem of spawned) {
        cells[gem.cellIndex] = { gemType: gem.gemType };
      }

      return { cells };
    }

    /** The cell indices a Match step has emptied, in §1.0 order. */
    function hiddenCells(ctx: object): number[] {
      return views(ctx)
        .map((view, index) => (view !== null && !view.tile.visible ? index : -1))
        .filter((index) => index >= 0);
    }

    /**
     * Every overlay the feedback layer holds, so a step's own extra visual can be
     * counted rather than assumed absent.
     */
    function feedbackOverlays(ctx: object): Array<{ kind: string; strokeWidth?: number }> {
      const layer = field<{ list: Array<{ kind: string; strokeWidth?: number }> } | null>(
        ctx,
        'feedbackLayer'
      );
      return layer ? [...layer.list] : [];
    }

    /**
     * Asserts the delivered round presented no retired board step: the timeline is
     * exactly the delivered swap/match/gravity/refill/settle sequence, and no phase
     * carries a special-cell activation or a per-turn tally line.
     */
    function expectNoRetiredBoardStep(ctx: object): void {
      expect(playedPhases(ctx)).toEqual([
        'swap 20->21',
        'match depth=1',
        'gravity depth=1',
        'refill depth=1',
        'settle',
      ]);
      expect(playedPhases(ctx).some((detail) => detail.startsWith('activate'))).toBe(false);
      expect(playedPhases(ctx).some((detail) => detail.startsWith('summary'))).toBe(false);
    }

    it('30. resolves a Match-4 as one confirmed match and creates nothing', () => {
      const matched = [24, 25, 26, 27];
      const spawned = [
        { cellIndex: 0, gemType: 'HP' },
        { cellIndex: 1, gemType: 'ATK' },
        { cellIndex: 2, gemType: 'DEF' },
      ];

      const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(atkMatchBoard(matched));

      push(harness, battleState({ sequence: 1, turn: 2, board: boardAfterOnePass(spawned) }));
      emitBatch(harness, 1, [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: matched, gemType: 'ATK', cascadeDepth: 1 },
        ...matched.map((cellIndex) => ({ type: 'GemMatched', cellIndex, gemType: 'ATK' })),
        { type: 'ComboChanged', combo: 1 },
        passReport(1, [[3], [3], [3]], spawned),
      ]);

      // The Match step presents the confirmed match itself: exactly its four cells
      // lose their content, and no fifth cell is redrawn with a created artefact —
      // every cell that still holds something holds one of the four documented Gem
      // types and nothing else.
      playUntil(harness, ctx, 2);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1']);
      expect(hiddenCells(ctx)).toEqual(matched);
      expect(
        views(ctx).every(
          (view) =>
            view === null || view.cleared || ['ATK', 'DEF', 'HP', 'POWER'].includes(view.content)
        )
      ).toBe(true);

      // Its own flashes are the only overlays: no activation ring, no badge, and no
      // object whose stroke is a retired presentation table's 3 px ring.
      expect(feedbackOverlays(ctx).every((overlay) => overlay.strokeWidth !== 3)).toBe(true);

      playAllTweens(harness);

      // The round is the ordinary swap/match/gravity/refill/settle sequence, and
      // the board converges on the delivered one on the same 128 objects.
      expectNoRetiredBoardStep(ctx);
      expect(labelsByCell(harness)).toEqual(labelsForCells(boardAfterOnePass(spawned)));
      expect(cellsWithoutView(ctx)).toEqual([]);
      expect(viewIncoherences(ctx)).toEqual([]);
      expect(harness.boardCells).toHaveLength(128);
      expect(harness.pendingTweenCount()).toBe(0);

      resolveAction();
    });

    it('31. keeps every cell drawn and every view coherent across three consecutive cascades', () => {
      // Pass 1 empties row 3 of columns 0–3; pass 2 empties row 4 of columns 0–2;
      // pass 3 empties row 5 of columns 0–2. Each pass is detected on the board the
      // previous pass's own delivered Fall and Refill produced.
      const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(
        atkMatchBoard([24, 25, 26, 27])
      );

      push(
        harness,
        battleState({ sequence: 1, turn: 2, board: boardAfterThreePasses() })
      );
      emitBatch(harness, 1, [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26, 27], gemType: 'ATK', cascadeDepth: 1 },
        ...[24, 25, 26, 27].map((cellIndex) => ({ type: 'GemMatched', cellIndex, gemType: 'ATK' })),
        { type: 'ComboChanged', combo: 1 },
        passReport(1, [[3], [3], [3], [3]], [
          { cellIndex: 0, gemType: 'HP' },
          { cellIndex: 1, gemType: 'ATK' },
          { cellIndex: 2, gemType: 'DEF' },
          { cellIndex: 3, gemType: 'POWER' },
        ]),
        { type: 'CascadeCreated', cascadeDepth: 1 },
        { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'POWER', cascadeDepth: 2 },
        ...[32, 33, 34].map((cellIndex) => ({ type: 'GemMatched', cellIndex, gemType: 'POWER' })),
        { type: 'ComboChanged', combo: 2 },
        passReport(2, [[4], [4], [4]], [
          { cellIndex: 0, gemType: 'HP' },
          { cellIndex: 1, gemType: 'HP' },
          { cellIndex: 2, gemType: 'HP' },
        ]),
        { type: 'CascadeCreated', cascadeDepth: 2 },
        { type: 'MatchCreated', shape: 'Straight', cells: [40, 41, 42], gemType: 'POWER', cascadeDepth: 3 },
        ...[40, 41, 42].map((cellIndex) => ({ type: 'GemMatched', cellIndex, gemType: 'POWER' })),
        { type: 'ComboChanged', combo: 3 },
        passReport(3, [[5], [5], [5]], [
          { cellIndex: 0, gemType: 'ATK' },
          { cellIndex: 1, gemType: 'HP' },
          { cellIndex: 2, gemType: 'DEF' },
        ]),
      ]);

      const expectedPhases = [
        'swap 20->21',
        'match depth=1',
        'gravity depth=1',
        'refill depth=1',
        'cascade depth=1',
        'gravity depth=2',
        'refill depth=2',
        'cascade depth=2',
        'gravity depth=3',
        'refill depth=3',
        'settle',
      ];

      for (let played = 1; played <= expectedPhases.length; played++) {
        playUntil(harness, ctx, played);

        expect(playedPhases(ctx)).toEqual(expectedPhases.slice(0, played));

        // After every delivered step: no view is bound twice, none is left in a
        // half state, and no view is hidden while it still holds content.
        expect(viewIncoherences(ctx)).toEqual([]);

        // A step that starts no travel of its own (a Match or Cascade step) can only
        // begin once the previous step's travel has landed: its own pacing tween is
        // queued behind that travel, so a view that is showing content is on its own
        // cell here — the Fall and Refill a pass reports cannot be overtaken by the
        // next pass (SIGNALR_PROTOCOL.md §3.2.28 item 5).
        if (
          expectedPhases[played - 1].startsWith('match') ||
          expectedPhases[played - 1].startsWith('cascade')
        ) {
          expect(everyViewOnItsOwnCell(ctx)).toBe(true);
        }

        // A Refill step ends a pass on a full board, and the round's transition ends
        // it on the delivered one; a Fall step legitimately leaves its own vacated
        // cells empty until that Refill (SIGNALR_PROTOCOL.md §3.2.28 item 5). A pass
        // that reported its own transition leaves **no** cell hidden behind it.
        if (expectedPhases[played - 1].startsWith('refill') || expectedPhases[played - 1] === 'settle') {
          expect(cellsWithoutView(ctx)).toEqual([]);
          expect(views(ctx).some((view) => view?.cleared)).toBe(false);
        }
      }

      playAllTweens(harness);

      // The converged board is the delivered one, on the same 64 persistent pairs.
      expect(labelsByCell(harness)).toEqual(labelsForCells(boardAfterThreePasses()));
      expect(cellsWithoutView(ctx)).toEqual([]);
      expect(viewIncoherences(ctx)).toEqual([]);
      expect(everyViewOnItsOwnCell(ctx)).toBe(true);

      // No cell's view was re-allocated: the board is still drawn by 64 pairs.
      expect(harness.boardCells).toHaveLength(128);
      expect(harness.pendingTweenCount()).toBe(0);

      resolveAction();
    });

    it('32. resolves a Match-5 as one confirmed match and creates nothing', () => {
      const matched = [24, 25, 26, 27, 28];
      const spawned = [
        { cellIndex: 0, gemType: 'HP' },
        { cellIndex: 1, gemType: 'ATK' },
        { cellIndex: 2, gemType: 'DEF' },
        { cellIndex: 3, gemType: 'POWER' },
        { cellIndex: 4, gemType: 'HP' },
      ];

      const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(atkMatchBoard(matched));

      push(harness, battleState({ sequence: 1, turn: 2, board: boardAfterOnePass(spawned) }));
      emitBatch(harness, 1, [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: matched, gemType: 'ATK', cascadeDepth: 1 },
        ...matched.map((cellIndex) => ({ type: 'GemMatched', cellIndex, gemType: 'ATK' })),
        { type: 'ComboChanged', combo: 1 },
        passReport(1, [[3], [3], [3], [3], [3]], spawned),
      ]);

      // A five-cell run resolves exactly like a three-cell one: the confirmed
      // match's five cells are cleared and nothing is created at a sixth cell.
      playUntil(harness, ctx, 2);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1']);
      expect(hiddenCells(ctx)).toEqual(matched);
      expect(
        views(ctx).every(
          (view) =>
            view === null || view.cleared || ['ATK', 'DEF', 'HP', 'POWER'].includes(view.content)
        )
      ).toBe(true);
      expect(feedbackOverlays(ctx).every((overlay) => overlay.strokeWidth !== 3)).toBe(true);

      playAllTweens(harness);

      expectNoRetiredBoardStep(ctx);
      expect(labelsByCell(harness)).toEqual(labelsForCells(boardAfterOnePass(spawned)));
      expect(labelsByCell(harness).every((label) => !label.includes(' '))).toBe(true);
      expect(cellsWithoutView(ctx)).toEqual([]);
      expect(viewIncoherences(ctx)).toEqual([]);
      expect(harness.boardCells).toHaveLength(128);

      resolveAction();
    });

    it('33. replays no clear when the same batch is delivered twice', () => {
      const matched = [24, 25, 26, 27];
      const spawned = [
        { cellIndex: 0, gemType: 'HP' },
        { cellIndex: 1, gemType: 'ATK' },
        { cellIndex: 2, gemType: 'DEF' },
      ];
      const batch = [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: matched, gemType: 'ATK', cascadeDepth: 1 },
        ...matched.map((cellIndex) => ({ type: 'GemMatched', cellIndex, gemType: 'ATK' })),
        { type: 'ComboChanged', combo: 1 },
        passReport(1, [[3], [3], [3]], spawned),
      ];

      const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(atkMatchBoard(matched));

      push(harness, battleState({ sequence: 1, turn: 2, board: boardAfterOnePass(spawned) }));
      emitBatch(harness, 1, batch);
      playAllTweens(harness);

      const phasesAfterFirstDelivery = [...playedPhases(ctx)];
      const viewsAfterFirstDelivery = views(ctx).map((view) => view);
      const labelsAfterFirstDelivery = labelsByCell(harness);

      emitBatch(harness, 1, batch);

      // The same resolution is presented once: no phase replays, no clear re-runs,
      // and no view or label is disturbed by the duplicate delivery.
      expect(playedPhases(ctx)).toEqual(phasesAfterFirstDelivery);
      expect(views(ctx).map((view) => view)).toEqual(viewsAfterFirstDelivery);
      expect(labelsByCell(harness)).toEqual(labelsAfterFirstDelivery);
      expect(harness.boardCells).toHaveLength(128);
      expect(harness.pendingTweenCount()).toBe(0);

      resolveAction();
    });

    it('34. resolves and animates an L/T match without any creation or detonation step', () => {
      // An L: the horizontal primitive on row 3 (24, 25, 26) and the vertical
      // primitive on column 0 (24, 33, 42) share cell 24
      // (SIGNALR_PROTOCOL.md §3.2.6 item 1 — `Lt` is the one L/T shape identity).
      const matched = [24, 25, 26, 33, 42];
      const spawned = [
        { cellIndex: 0, gemType: 'HP' },
        { cellIndex: 1, gemType: 'ATK' },
        { cellIndex: 2, gemType: 'DEF' },
      ];

      const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(atkMatchBoard(matched));

      push(harness, battleState({ sequence: 1, turn: 2, board: boardAfterLtPass(spawned) }));
      emitBatch(harness, 1, [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Lt', cells: matched, gemType: 'ATK', cascadeDepth: 1 },
        ...matched.map((cellIndex) => ({ type: 'GemMatched', cellIndex, gemType: 'ATK' })),
        { type: 'ComboChanged', combo: 1 },
        passReport(1, [[3, 4, 5], [3], [3]], spawned),
      ]);

      // The shared cell is cleared once, with the rest of the shape, and the Match
      // step presents the one confirmed match — there is no second pass of effect
      // and no cell is cleared twice.
      playUntil(harness, ctx, 2);
      expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1']);
      expect(hiddenCells(ctx)).toEqual(matched);
      expect(feedbackOverlays(ctx).every((overlay) => overlay.strokeWidth !== 3)).toBe(true);

      playAllTweens(harness);

      expectNoRetiredBoardStep(ctx);
      expect(labelsByCell(harness)).toEqual(labelsForCells(boardAfterLtPass(spawned)));
      expect(cellsWithoutView(ctx)).toEqual([]);
      expect(viewIncoherences(ctx)).toEqual([]);
      expect(harness.boardCells).toHaveLength(128);
      expect(harness.pendingTweenCount()).toBe(0);

      resolveAction();
    });

    /** The board an L/T single-pass round leaves: POWER plus the three spawned heads. */
    function boardAfterLtPass(
      spawned: ReadonlyArray<{ cellIndex: number; gemType: string }>
    ): RuntimeBoard {
      return boardAfterOnePass(spawned);
    }

    /**
     * The three-pass round's committed board: the column heads the third pass
     * spawned, and the survivors Gravity carried down.
     */
    function boardAfterThreePasses(): RuntimeBoard {
      const cells: Array<{ gemType: string }> = Array.from(
        { length: BOARD_COLUMNS * BOARD_COLUMNS },
        () => ({ gemType: 'POWER' })
      );

      cells[0] = { gemType: 'ATK' };
      cells[8] = { gemType: 'HP' };
      cells[16] = { gemType: 'HP' };
      cells[1] = { gemType: 'HP' };
      cells[9] = { gemType: 'HP' };
      cells[17] = { gemType: 'ATK' };
      cells[2] = { gemType: 'DEF' };
      cells[10] = { gemType: 'HP' };
      cells[18] = { gemType: 'DEF' };

      return { cells };
    }

    /**
     * The two-pass round's committed board: the second pass's spawned column heads,
     * the survivors Gravity carried down, and POWER elsewhere.
     */
    function boardAfterTwoPasses(): RuntimeBoard {
      const cells: Array<{ gemType: string }> = Array.from(
        { length: BOARD_COLUMNS * BOARD_COLUMNS },
        () => ({ gemType: 'POWER' })
      );

      cells[0] = { gemType: 'DEF' };
      cells[1] = { gemType: 'HP' };
      cells[2] = { gemType: 'ATK' };
      cells[8] = { gemType: 'DEF' };
      cells[9] = { gemType: 'HP' };
      cells[10] = { gemType: 'ATK' };

      return { cells };
    }

    // -------------------------------------------------------------------------
    // BOSS-TURN PRESENTATION — swap/match ordering, pacing and asset consistency
    // (MATCH3_RULES.md §4, SIGNALR_PROTOCOL.md §3.2.28)
    //
    // One board cycle is presented as the delivered swap → match → Fall → Refill
    // sequence and nothing else. No tier, activation, chain or area-clear step
    // exists to present (ADR-028, GAME_STATE.md §2.1.2).
    // -------------------------------------------------------------------------
    describe('BOSS-TURN PRESENTATION — ordering, pacing & asset consistency', () => {
      /** One object the scene has drawn into the feedback layer. */
      interface OverlayLike {
        kind: string;
        x: number;
        y: number;
        strokeWidth?: number;
        strokeColor?: number;
        alpha?: number;
        text?: string;
      }

      /** Everything the feedback layer currently holds. */
      function feedbackChildren(ctx: object): OverlayLike[] {
        const layer = field<{ list: OverlayLike[] } | null>(ctx, 'feedbackLayer');
        return layer ? [...layer.list] : [];
      }

      /** How many objects in the feedback layer are ordinary cell flashes. */
      function highlightCount(ctx: object): number {
        return feedbackChildren(ctx).filter(
          (child) => child.kind === 'tile' && child.strokeWidth === 2
        ).length;
      }

      // -----------------------------------------------------------------------
      // §3 — swap/match ordering
      // -----------------------------------------------------------------------

      it('1. never begins the Match step while the swap that produced it is still animating', () => {
        const { harness, ctx, resolveAction } = battleWithHeldAction();

        push(harness, battleState({ sequence: 1, turn: 2, board: afterPassBoard() }));
        emitBatch(harness, 1, PER_PASS_BATCH);

        // The exchange step plays, and its exchange tween is in flight.
        playUntil(harness, ctx, 1);
        expect(playedPhases(ctx)).toEqual(['swap 20->21']);

        const hold = harness
          .pendingTweenRecords()
          .find((tween) => tween.targets.includes(field<object>(ctx, 'phaseClock')));
        const exchange = harness
          .pendingTweenRecords()
          .filter((tween) => tween.targets.length === 2 && 'x' in tween.props);

        expect(exchange).toHaveLength(2);
        // The phase's own hold outlasts the exchange it started, and the machine
        // holds on the animations the phase actually started (`trackPhaseAnimation`)
        // rather than on a declared number alone.
        for (const tween of exchange) {
          expect(hold?.duration ?? 0).toBeGreaterThan(tween.duration);
        }

        // Completing the exchange — and only the exchange — does not start the
        // Match step: the swap's required presentation state is the whole phase
        // (its hold included), so the Match cannot be presented over a swap still
        // being paced.
        harness.flushTweens(exchange.length);
        expect(playedPhases(ctx)).toEqual(['swap 20->21']);

        // The Match step begins only once the swap phase has completed.
        harness.flushTweens(1);
        expect(playedPhases(ctx)).toEqual(['swap 20->21', 'match depth=1']);

        playAllTweens(harness);
        expect(harness.pendingTweenCount()).toBe(0);
        resolveAction();
      });

      it('2. presents no dependent step for a rejected swap, and rolls the board back', async () => {
        const { harness, scene, ctx } = createBattle(battleState({ sequence: 0, board: board() }));
        const held = holdAction(harness);

        tapCell(harness, 20);
        tapCell(harness, 21);
        const beforeRejection = labelsByCell(harness);

        held.resolve({ accepted: false, reason: 'INVALID_SWAP' });
        await flush();

        // No timeline, no phase, and no board effect of any kind: a rejected Swap
        // emits no Battle Event at all (`GAME_EVENTS.md` §1.2), so there is no
        // dependent presentation to process. The only object the rejection raises is
        // the transport feedback line, never a board overlay.
        expect(playedPhases(ctx)).toEqual([]);
        expect(field<object[] | null>(ctx, 'presentationTimeline')).toBeNull();
        expect(feedbackChildren(ctx).filter((child) => child.kind === 'tile')).toEqual([]);

        // The rollback converged the board and the guard is released, so no stale
        // pair survives to pair with the player's next tap. No timeline pacing is
        // left running either — the only tween here is the rejection line's own fade,
        // which is transport feedback rather than a phase.
        expect(labelsByCell(harness)).toEqual(beforeRejection);
        expect(field<object | null>(ctx, 'committedSwapCells')).toBeNull();
        expect(inputLocked(scene, ctx)).toBe(false);
        expect(
          harness
            .pendingTweenRecords()
            .some((tween) => tween.targets.includes(field<object>(ctx, 'phaseClock')))
        ).toBe(false);
      });

      it('3. presents one match step and one set of views for a duplicate batch', () => {
        const { harness, ctx, resolveAction } = battleWithHeldAction();

        push(harness, battleState({ sequence: 1, turn: 2, board: afterPassBoard() }));
        emitBatch(harness, 1, PER_PASS_BATCH);
        playAllTweens(harness);

        const phasesAfterFirst = [...playedPhases(ctx)];
        const boardAfterFirst = labelsByCell(harness);
        const viewsAfterFirst = views(ctx).map((view) => view);

        // The identical resolution, delivered again: recorded, presented no second
        // time — one match step, no second exchange, and no view replaced.
        emitBatch(harness, 1, PER_PASS_BATCH);

        expect(playedPhases(ctx)).toEqual(phasesAfterFirst);
        expect(labelsByCell(harness)).toEqual(boardAfterFirst);
        expect(views(ctx).map((view) => view)).toEqual(viewsAfterFirst);
        expect(playedPhases(ctx).filter((detail) => detail === 'match depth=1')).toHaveLength(1);
        expect(new Set(views(ctx).filter((view) => view !== null)).size).toBe(64);
        expect(harness.boardCells).toHaveLength(128);

        resolveAction();
      });

      it('4. presents an ordinary board cycle with no retired activation or summary step', () => {
        const { harness, ctx, resolveAction } = battleWithHeldAction();

        push(harness, battleState({ sequence: 1, turn: 2, board: afterPassBoard() }));
        emitBatch(harness, 1, PER_PASS_BATCH);

        // The timeline is exactly the delivered board cycle and the round's single
        // convergence: no activation phase, no per-turn tally phase, and no other
        // step the contract does not deliver (`SIGNALR_PROTOCOL.md` §3.2.12).
        expect(builtTimeline(ctx).map((phase) => phase.kind)).toEqual([
          'swap',
          'match',
          'gravity-drop',
          'refill-spawn',
          'settle',
        ]);
        expect(playedPhases(ctx).some((detail) => detail.startsWith('activate'))).toBe(false);
        expect(playedPhases(ctx).some((detail) => detail.startsWith('summary'))).toBe(false);

        // The Match step's presentation is the delivered cell flashes alone — one
        // per cell a `MatchCreated` or `GemMatched` names (3 + 3 here) — and nothing
        // else: an activation ring would be a stroke-3 overlay no delivered member
        // asks for.
        playUntil(harness, ctx, 2);
        expect(highlightCount(ctx)).toBe(6);
        expect(feedbackChildren(ctx).every((overlay) => overlay.strokeWidth !== 3)).toBe(true);

        playAllTweens(harness);
        expect(playedPhases(ctx)).not.toContain('summary');
        expect(harness.pendingTweenCount()).toBe(0);

        resolveAction();
      });

      it('12. applies no clear, Fall or Refill from a cycle whose starting board the client does not hold', () => {
        const { harness, ctx, resolveAction } = battleWithHeldAction();

        // The Player's cycle is the client's own board. The Boss's cycle is not one
        // this batch's own deltas can place: the Boss's action was selected on the
        // post-Player-cycle board and, after an unreported deadlock reshuffle, on a
        // board the client has never held (ADR-026 §8, TASK-264) — so the delivered
        // deltas do not replay onto the round's committed board, and the cycle is
        // reported without local board work. Its delivered cells here even *agree*
        // with the rendered board, which is exactly the case a per-cell gate cannot
        // catch.
        const round = [
          ...PER_PASS_BATCH,
          { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
          { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
          { type: 'SwapStarted', actor: 'boss', fromCell: 27, toCell: 28 },
          { type: 'SwapResolved', outcome: 'committed' },
          {
            type: 'MatchCreated',
            shape: 'Straight',
            cells: [16, 17, 18],
            gemType: 'POWER',
            cascadeDepth: 1,
          },
          { type: 'GemMatched', cellIndex: 16, gemType: 'POWER' },
          {
            type: 'CascadeStepResolved',
            passDepth: 1,
            dropMoves: [{ fromCell: 16, toCell: 24 }],
            spawnedGems: [{ cellIndex: 16, gemType: 'DEF' }],
          },
          { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
          { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
        ];

        /** The round's committed board — the Boss's cycle resolved on the reshuffled one. */
        const bossRoundBoard: RuntimeBoard = {
          cells: Array.from({ length: BOARD_COLUMNS * BOARD_COLUMNS }, () => ({ gemType: 'DEF' })),
        };

        push(harness, battleState({ sequence: 1, turn: 2, board: bossRoundBoard }));
        emitBatch(harness, 1, round);

        // The Player's own cycle is applied: the client's board is the pass board
        // the next boundary starts from.
        playUntil(harness, ctx, 5);
        expect(playedPhases(ctx)).toEqual([
          'swap 20->21',
          'match depth=1',
          'gravity depth=1',
          'refill depth=1',
          'damage player->boss',
        ]);
        expect(labelsByCell(harness)).toEqual(labelsForCells(afterPassBoard()));
        const trackedBoard = labelsByCell(harness);
        const viewAt24 = views(ctx)[24];

        // The Boss's cycle is reported — its banner, its marker, its match highlight
        // and its own delivered pass report all play — but nothing on the rendered
        // board is mutated by deltas that describe a board the client does not hold,
        // and the pass's transition is presented as no movement step at all: a
        // `gravity-drop` / `refill-spawn` step would state that its Gravity and Spawn
        // played on the board on screen (`PresentationPhase.boardBacked`).
        playUntil(harness, ctx, 8);
        expect(playedPhases(ctx)).toEqual([
          'swap 20->21',
          'match depth=1',
          'gravity depth=1',
          'refill depth=1',
          'damage player->boss',
          'banner BOSS TURN',
          'swap 27->28',
          'match depth=1',
        ]);
        // The Boss's own cycle opens no movement step at all: every `gravity-drop` /
        // `refill-spawn` phase of the timeline belongs to the Player's cycle.
        expect(
          builtTimeline(ctx)
            .filter(
              (phase) => phase.kind === 'gravity-drop' || phase.kind === 'refill-spawn'
            )
            .map((phase) => phase.detail)
        ).toEqual(['gravity depth=1', 'refill depth=1']);

        expect(labelsByCell(harness)).toEqual(trackedBoard);
        expect(views(ctx)[24]).toBe(viewAt24);
        expect(clearedCells(ctx)).toEqual([]);
        expect(viewIncoherences(ctx)).toEqual([]);

        // The round's single board transition then converges the committed board,
        // and every cell is drawn on exactly one view: no duplicate sprite, no
        // invisible occupied cell, no view left on a coordinate the report named.
        playAllTweens(harness);

        expect(labelsByCell(harness)).toEqual(labelsForCells(bossRoundBoard));
        expect(labelsByCell(harness)[24]).toBe('DEF');
        expect(cellsWithoutView(ctx)).toEqual([]);
        expect(viewIncoherences(ctx)).toEqual([]);
        expect(everyViewOnItsOwnCell(ctx)).toBe(true);
        expect(new Set(views(ctx).filter((view) => view !== null)).size).toBe(64);
        expect(harness.boardCells).toHaveLength(128);

        resolveAction();
      });

      it('15. converges every view on the newer authoritative state when a travel is in flight', () => {
        const { harness, ctx, resolveAction } = battleWithHeldAction();

        push(harness, battleState({ sequence: 1, turn: 2, board: afterPassBoard() }));
        emitBatch(harness, 1, PER_PASS_BATCH);

        // The Fall step has played: a view is travelling toward its delivered
        // destination cell, and the phase's pacing is still pending.
        playUntil(harness, ctx, 3);
        const travelling = harness
          .pendingTweenRecords()
          .filter((tween) => tween.targets.length === 2 && 'x' in tween.props && 'y' in tween.props);
        expect(travelling.length).toBeGreaterThan(0);

        // A newer authoritative state arrives: the travel is cancelled with the
        // resolution that started it, and every view converges on its authoritative
        // cell — so a stale animation cannot leave a sprite at a coordinate the
        // newer board never placed it at.
        recovery(harness, battleState({ sequence: 3, turn: 4, board: deliveredBoard(2) }));

        expect(harness.pendingTweenCount()).toBe(0);
        expect(labelsByCell(harness)).toEqual(expectedLabels(2));
        expect(everyViewOnItsOwnCell(ctx)).toBe(true);
        expect(viewIncoherences(ctx)).toEqual([]);
        expect(cellsWithoutView(ctx)).toEqual([]);

        // A delayed frame from the cancelled pacing completes nothing: the board is
        // still the recovered one.
        const stale = travelling[0].completion;
        stale?.();
        expect(everyViewOnItsOwnCell(ctx)).toBe(true);
        expect(labelsByCell(harness)).toEqual(expectedLabels(2));

        resolveAction();
      });

      it('16. leaves no cell without a view and no duplicated view across a two-pass cascade round', () => {
        const spawned = [
          { cellIndex: 0, gemType: 'DEF' },
          { cellIndex: 1, gemType: 'HP' },
          { cellIndex: 2, gemType: 'ATK' },
        ];

        const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(
          atkMatchBoard([24, 25, 26])
        );

        push(harness, battleState({ sequence: 1, turn: 2, board: boardAfterTwoPasses() }));
        emitBatch(harness, 1, [
          { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
          { type: 'SwapResolved', outcome: 'committed' },
          { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
          ...[24, 25, 26].map((cellIndex) => ({ type: 'GemMatched', cellIndex, gemType: 'ATK' })),
          { type: 'ComboChanged', combo: 1 },
          passReport(1, [[3], [3], [3]], spawned),
          { type: 'CascadeCreated', cascadeDepth: 1 },
          { type: 'MatchCreated', shape: 'Straight', cells: [32, 33, 34], gemType: 'POWER', cascadeDepth: 2 },
          ...[32, 33, 34].map((cellIndex) => ({ type: 'GemMatched', cellIndex, gemType: 'POWER' })),
          { type: 'ComboChanged', combo: 2 },
          passReport(2, [[4], [4], [4]], spawned),
        ]);

        playAllTweens(harness);

        const drawn = views(ctx).filter((view) => view !== null);

        expect(drawn).toHaveLength(64);
        expect(new Set(drawn).size).toBe(64);
        expect(cellsWithoutView(ctx)).toEqual([]);
        expect(viewIncoherences(ctx)).toEqual([]);
        expect(everyViewOnItsOwnCell(ctx)).toBe(true);
        expect(harness.boardCells).toHaveLength(128);
        expect(labelsByCell(harness)).toEqual(labelsForCells(boardAfterTwoPasses()));

        resolveAction();
      });

      // -----------------------------------------------------------------------
      // §8 — one board pipeline for the Player Turn and the Boss Turn (TASK-285)
      // -----------------------------------------------------------------------
      //
      // The reported defect: the Boss Turn's board operations were presented as no
      // board work at all — its swap was a coordinate marker, its clears, Fall and
      // Refill were withheld, and the sprites only changed when the round's
      // committed board was applied. These tests drive a **complete** delivered round
      // — the Player's board cycle and the Boss Response Phase's, each closed by its
      // own pass report — and assert that the Boss's cycle now executes the same
      // shared board mechanisms the Player's does, that the decision to do so is the
      // reconciliation of the batch's own delivered deltas against the round's
      // committed board, and that a cycle which does not reconcile still fabricates
      // nothing.

      describe('TASK-285 — one board pipeline for the Player Turn and the Boss Turn', () => {
        /** Every cell's rendered content key, or `null` where the cell holds nothing. */
        function contentsByCell(ctx: object): Array<string | null> {
          return views(ctx).map((view) =>
            view && !view.cleared && view.content !== '' ? view.content : null
          );
        }

        /** One delivered board's content keys, in §1.0 order (`gemContentKey`'s form). */
        function contentsOf(next: RuntimeBoard): string[] {
          return next.cells.map((cell) => cell.gemType);
        }

        /** The pending Gem-view travels: a two-target tween moving a tile and its label. */
        function travellingViews(harness: ReturnType<typeof createSceneHarness>): number {
          return harness
            .pendingTweenRecords()
            .filter(
              (tween) => tween.targets.length === 2 && 'x' in tween.props && 'y' in tween.props
            ).length;
        }

        /** The cells the pending single-object feedback effects sit on. */
        function effectCells(harness: ReturnType<typeof createSceneHarness>): number[] {
          const cells: number[] = [];

          for (const tween of harness.pendingTweenRecords()) {
            if (tween.targets.length !== 1 || !('alpha' in tween.props)) {
              continue;
            }

            const target = tween.targets[0] as { x: number; y: number };
            const index = cellIndexOf(target.x, target.y);

            if (index !== null) {
              cells.push(index);
            }
          }

          return cells;
        }

        /** A board whose only differences from `POWER` are the named cells. */
        function cellsOf(overrides: Readonly<Record<number, string>>): RuntimeBoard {
          const cells: Array<{ gemType: string }> = Array.from(
            { length: BOARD_COLUMNS * BOARD_COLUMNS },
            () => ({ gemType: 'POWER' })
          );

          for (const [index, gemType] of Object.entries(overrides)) {
            cells[Number(index)] = { gemType };
          }

          return { cells };
        }

        /**
         * The board one round's two board actions start from.
         *
         * The Player's swap (18⇄19) closes an ATK line at 16–18, in columns 0–2; the
         * Boss's (38⇄39) closes one at 36–38, in columns 4–6. The two cycles touch
         * disjoint columns, so each cycle's Gravity and Spawn is stated entirely by
         * its own delivered report and neither depends on the other's intermediate
         * board — which is what makes the round's committed board the board the
         * batch's own deltas produce.
         */
        function roundStartBoard(): RuntimeBoard {
          return cellsOf({
            16: 'ATK',
            17: 'ATK',
            18: 'HP',
            19: 'ATK',
            36: 'ATK',
            37: 'ATK',
            38: 'DEF',
            39: 'ATK',
          });
        }

        /** The board the Player's own cycle leaves — the Boss's step-18a starting board. */
        function roundMidBoard(): RuntimeBoard {
          return cellsOf({ 19: 'HP', 36: 'ATK', 37: 'ATK', 38: 'DEF', 39: 'ATK' });
        }

        /** The board the round commits: both cycles' board work, and nothing else. */
        function roundCommittedBoard(): RuntimeBoard {
          return cellsOf({ 19: 'HP', 39: 'DEF' });
        }

        /** The Player's committed board action, its pass, and its damage on the Boss. */
        const PLAYER_CYCLE: unknown[] = [
          { type: 'SwapStarted', actor: 'player', fromCell: 18, toCell: 19 },
          { type: 'SwapResolved', outcome: 'committed' },
          { type: 'MatchCreated', shape: 'Straight', cells: [16, 17, 18], gemType: 'ATK', cascadeDepth: 1 },
          { type: 'GemMatched', cellIndex: 16, gemType: 'ATK' },
          { type: 'GemMatched', cellIndex: 17, gemType: 'ATK' },
          { type: 'GemMatched', cellIndex: 18, gemType: 'ATK' },
          {
            type: 'CascadeStepResolved',
            passDepth: 1,
            dropMoves: [
              { fromCell: 8, toCell: 16 },
              { fromCell: 0, toCell: 8 },
              { fromCell: 9, toCell: 17 },
              { fromCell: 1, toCell: 9 },
              { fromCell: 10, toCell: 18 },
              { fromCell: 2, toCell: 10 },
            ],
            spawnedGems: [
              { cellIndex: 0, gemType: 'POWER' },
              { cellIndex: 1, gemType: 'POWER' },
              { cellIndex: 2, gemType: 'POWER' },
            ],
          },
          { type: 'DamageDealt', source: 'player', target: 'boss', amount: 150 },
          { type: 'DamageTaken', source: 'player', target: 'boss', amount: 150 },
        ];

        /**
         * The Boss Response Phase's own board action and its pass.
         *
         * Its shape is the Player's cycle's shape exactly — one `SwapStarted`, one
         * `MatchCreated`, that pass's `GemMatched` run and its `CascadeStepResolved`.
         * With `cascade`, the pass's delivered spawns form a new ATK line, so a
         * second delivered pass follows: its spawns are the `POWER` the round commits.
         */
        function bossCycle(cascade = false): unknown[] {
          const events: unknown[] = [
            { type: 'SwapStarted', actor: 'boss', fromCell: 38, toCell: 39 },
            { type: 'SwapResolved', outcome: 'committed' },
            { type: 'MatchCreated', shape: 'Straight', cells: [36, 37, 38], gemType: 'ATK', cascadeDepth: 1 },
            { type: 'GemMatched', cellIndex: 36, gemType: 'ATK' },
            { type: 'GemMatched', cellIndex: 37, gemType: 'ATK' },
            { type: 'GemMatched', cellIndex: 38, gemType: 'ATK' },
            {
              type: 'CascadeStepResolved',
              passDepth: 1,
              dropMoves: [
                { fromCell: 28, toCell: 36 },
                { fromCell: 20, toCell: 28 },
                { fromCell: 12, toCell: 20 },
                { fromCell: 4, toCell: 12 },
                { fromCell: 29, toCell: 37 },
                { fromCell: 21, toCell: 29 },
                { fromCell: 13, toCell: 21 },
                { fromCell: 5, toCell: 13 },
                { fromCell: 30, toCell: 38 },
                { fromCell: 22, toCell: 30 },
                { fromCell: 14, toCell: 22 },
                { fromCell: 6, toCell: 14 },
              ],
              spawnedGems: [
                { cellIndex: 4, gemType: cascade ? 'ATK' : 'POWER' },
                { cellIndex: 5, gemType: cascade ? 'ATK' : 'POWER' },
                { cellIndex: 6, gemType: cascade ? 'ATK' : 'POWER' },
              ],
            },
          ];

          if (cascade) {
            events.push(
              { type: 'CascadeCreated', cascadeDepth: 1 },
              { type: 'MatchCreated', shape: 'Straight', cells: [4, 5, 6], gemType: 'ATK', cascadeDepth: 2 },
              { type: 'GemMatched', cellIndex: 4, gemType: 'ATK' },
              { type: 'GemMatched', cellIndex: 5, gemType: 'ATK' },
              { type: 'GemMatched', cellIndex: 6, gemType: 'ATK' },
              {
                type: 'CascadeStepResolved',
                passDepth: 2,
                dropMoves: [
                  { fromCell: 60, toCell: 52 },
                  { fromCell: 52, toCell: 44 },
                  { fromCell: 44, toCell: 36 },
                  { fromCell: 36, toCell: 28 },
                  { fromCell: 28, toCell: 20 },
                  { fromCell: 20, toCell: 12 },
                  { fromCell: 12, toCell: 4 },
                  { fromCell: 61, toCell: 53 },
                  { fromCell: 53, toCell: 45 },
                  { fromCell: 45, toCell: 37 },
                  { fromCell: 37, toCell: 29 },
                  { fromCell: 29, toCell: 21 },
                  { fromCell: 21, toCell: 13 },
                  { fromCell: 13, toCell: 5 },
                  { fromCell: 62, toCell: 54 },
                  { fromCell: 54, toCell: 46 },
                  { fromCell: 46, toCell: 38 },
                  { fromCell: 38, toCell: 30 },
                  { fromCell: 30, toCell: 22 },
                  { fromCell: 22, toCell: 14 },
                  { fromCell: 14, toCell: 6 },
                ],
                spawnedGems: [
                  { cellIndex: 60, gemType: 'POWER' },
                  { cellIndex: 61, gemType: 'POWER' },
                  { cellIndex: 62, gemType: 'POWER' },
                ],
              }
            );
          }

          events.push(
            { type: 'DamageDealt', source: 'boss', target: 'player', amount: 80 },
            { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 }
          );

          return events;
        }

        /** The round's delivered batch: both cycles, in the delivered order. */
        function roundBatch(cascade = false): unknown[] {
          return [...PLAYER_CYCLE, ...bossCycle(cascade)];
        }

        it('17. animates the Boss’s authorised swap, clear, Fall and Refill through the shared pipeline', () => {
          const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(roundStartBoard());

          // The committed board is the one this batch's own delivered deltas produce:
          // the ordinary Boss Turn, where the Boss's step-18a action was resolved on
          // the board the Player's cycle left and that is the board on screen.
          push(
            harness,
            battleState({ sequence: 1, turn: 2, board: roundCommittedBoard() })
          );
          emitBatch(harness, 1, roundBatch());

          // **One pipeline for both turns.** The Boss's board cycle opens the same
          // step kinds, holds them for the same time, and is preceded only by its own
          // delivered banner — the Player's cycle in the same round is the reference.
          const timeline = builtTimeline(ctx);
          expect(timeline.map((phase) => phase.kind)).toEqual([
            'swap',
            'match',
            'gravity-drop',
            'refill-spawn',
            'damage',
            'banner',
            'swap',
            'match',
            'gravity-drop',
            'refill-spawn',
            'settle',
            'retaliation',
          ]);
          const playerCycle = timeline.slice(0, 4);
          const bossCyclePhases = timeline.slice(6, 10);
          expect(bossCyclePhases.map((phase) => phase.kind)).toEqual(
            playerCycle.map((phase) => phase.kind)
          );
          expect(bossCyclePhases.map((phase) => phase.holdMs)).toEqual(
            playerCycle.map((phase) => phase.holdMs)
          );

          // The Player's own cycle is unchanged: its swap, its clear, its Fall and its
          // Refill, in the delivered order, leaving the board the Boss acted on.
          playUntil(harness, ctx, 5);
          expect(playedPhases(ctx)).toEqual([
            'swap 18->19',
            'match depth=1',
            'gravity depth=1',
            'refill depth=1',
            'damage player->boss',
          ]);
          expect(contentsByCell(ctx)).toEqual(contentsOf(roundMidBoard()));

          const viewAt38 = views(ctx)[38];
          const viewAt39 = views(ctx)[39];
          const cellOf38 = cellIndexOf(viewAt38?.tile.x ?? 0, viewAt38?.tile.y ?? 0);
          const cellOf39 = cellIndexOf(viewAt39?.tile.x ?? 0, viewAt39?.tile.y ?? 0);

          // The Boss's exchange. The two views the delivered `SwapStarted` names have
          // changed cells in the occupant map and their travel is in flight — the
          // sprites visibly transition between the two cells rather than appearing
          // already moved.
          playUntil(harness, ctx, 7);
          expect(playedPhases(ctx)[6]).toBe('swap 38->39');
          expect(views(ctx)[38]).toBe(viewAt39);
          expect(views(ctx)[39]).toBe(viewAt38);
          expect(travellingViews(harness)).toBe(2);

          // The Match's own destruction, once that travel has completed: the three
          // cells the delivered events confirm lose their asset.
          playUntil(harness, ctx, 8);
          expect(clearedCells(ctx)).toEqual([36, 37, 38]);
          expect(cellIndexOf(views(ctx)[39]?.tile.x ?? 0, views(ctx)[39]?.tile.y ?? 0)).toBe(39);
          expect(cellIndexOf(views(ctx)[38]?.tile.x ?? 0, views(ctx)[38]?.tile.y ?? 0)).toBe(38);
          expect(cellOf38).toBe(38);
          expect(cellOf39).toBe(39);

          // The Fall moves the survivors the report named and the Refill enters the
          // spawned Gems — and the board they leave is already the round's committed
          // board, **before** the reconciliation step has started at all.
          playUntil(harness, ctx, 10);
          expect(playedPhases(ctx)[9]).toBe('refill depth=1');
          expect(playedPhases(ctx)).not.toContain('settle');
          expect(contentsByCell(ctx)).toEqual(contentsOf(roundCommittedBoard()));
          expect(clearedCells(ctx)).toEqual([]);
          expect(cellsWithoutView(ctx)).toEqual([]);
          expect(viewIncoherences(ctx)).toEqual([]);
          // The Refill's own entrance animations are what the phase is waiting on.
          expect(harness.pendingTweenCount()).toBeGreaterThan(0);

          // Reconciliation then converges the same board: no relabel, no stale asset,
          // one view per cell, and the round's lock released.
          playAllTweens(harness);

          expect(playedPhases(ctx)).toContain('settle');
          expect(contentsByCell(ctx)).toEqual(contentsOf(roundCommittedBoard()));
          expect(labelsByCell(harness)).toEqual(labelsForCells(roundCommittedBoard()));
          expect(cellsWithoutView(ctx)).toEqual([]);
          expect(viewIncoherences(ctx)).toEqual([]);
          expect(everyViewOnItsOwnCell(ctx)).toBe(true);
          expect(new Set(views(ctx).filter((view) => view !== null)).size).toBe(64);
          expect(harness.boardCells).toHaveLength(128);
          expect(harness.pendingTweenCount()).toBe(0);

          resolveAction();
        });

        it('18. fabricates no board movement for a Boss cycle that does not reconcile', () => {
          const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(roundStartBoard());

          // A committed board this batch's own deltas do **not** produce: the board
          // the Boss acted on is not the one the client holds — the unreported
          // deadlock reshuffle (`ADR-026` §8, `GAME_EVENTS.md` §3 item 8). The cycle
          // is still reported, and its board work is withheld.
          push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(3) }));
          emitBatch(harness, 1, roundBatch());

          expect(builtTimeline(ctx).map((phase) => phase.kind)).toEqual([
            'swap',
            'match',
            'gravity-drop',
            'refill-spawn',
            'damage',
            'banner',
            'swap',
            'match',
            'settle',
            'retaliation',
          ]);

          // The Player's cycle still runs exactly as it always has.
          playUntil(harness, ctx, 5);
          expect(contentsByCell(ctx)).toEqual(contentsOf(roundMidBoard()));

          const viewAt38 = views(ctx)[38];
          const viewAt39 = views(ctx)[39];
          const clearedBefore = clearedCells(ctx);

          // The Boss's step is a coordinate marker, not an exchange: no view leaves
          // its cell, and the two delivered cells are what is marked.
          playUntil(harness, ctx, 7);
          expect(playedPhases(ctx)[6]).toBe('swap 38->39');
          expect(views(ctx)[38]).toBe(viewAt38);
          expect(views(ctx)[39]).toBe(viewAt39);
          expect(cellIndexOf(viewAt38?.tile.x ?? 0, viewAt38?.tile.y ?? 0)).toBe(38);
          expect(cellIndexOf(viewAt39?.tile.x ?? 0, viewAt39?.tile.y ?? 0)).toBe(39);
          expect(travellingViews(harness)).toBe(0);
          expect(effectCells(harness)).toEqual(expect.arrayContaining([38, 39]));

          // And its pass clears nothing on the rendered board: the delivered cells
          // name a board the client does not hold.
          playUntil(harness, ctx, 8);
          expect(clearedCells(ctx)).toEqual(clearedBefore);

          // The round's single transition is what converges the delivered board, and
          // every cell is drawn on exactly one view.
          playAllTweens(harness);

          expect(labelsByCell(harness)).toEqual(expectedLabels(3));
          expect(cellsWithoutView(ctx)).toEqual([]);
          expect(viewIncoherences(ctx)).toEqual([]);
          expect(everyViewOnItsOwnCell(ctx)).toBe(true);
          expect(new Set(views(ctx).filter((view) => view !== null)).size).toBe(64);
          expect(harness.pendingTweenCount()).toBe(0);

          resolveAction();
        });

        it('19. plays a chained Boss cascade in order and converges on the committed board', () => {
          const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(roundStartBoard());

          push(
            harness,
            battleState({ sequence: 1, turn: 2, board: roundCommittedBoard() })
          );
          emitBatch(harness, 1, roundBatch(true));

          // The second pass opens after the first pass's Refill has played, on the
          // board that Refill produced — the same order the Player's own cascades keep.
          expect(builtTimeline(ctx).map((phase) => phase.detail)).toEqual([
            'swap 18->19',
            'match depth=1',
            'gravity depth=1',
            'refill depth=1',
            'damage player->boss',
            'banner BOSS TURN',
            'swap 38->39',
            'match depth=1',
            'gravity depth=1',
            'refill depth=1',
            'cascade depth=1',
            'gravity depth=2',
            'refill depth=2',
            'settle',
            'retaliation boss->player',
          ]);

          playAllTweens(harness);

          const phases = playedPhases(ctx);

          expect(phases.indexOf('cascade depth=1')).toBeGreaterThan(phases.indexOf('refill depth=1'));
          expect(phases.indexOf('gravity depth=2')).toBeGreaterThan(
            phases.indexOf('cascade depth=1')
          );
          expect(phases.indexOf('refill depth=2')).toBeGreaterThan(phases.indexOf('gravity depth=2'));
          expect(phases).toContain('settle');

          expect(contentsByCell(ctx)).toEqual(contentsOf(roundCommittedBoard()));
          expect(labelsByCell(harness)).toEqual(labelsForCells(roundCommittedBoard()));
          expect(clearedCells(ctx)).toEqual([]);
          expect(cellsWithoutView(ctx)).toEqual([]);
          expect(viewIncoherences(ctx)).toEqual([]);
          expect(everyViewOnItsOwnCell(ctx)).toBe(true);
          expect(new Set(views(ctx).filter((view) => view !== null)).size).toBe(64);
          expect(harness.boardCells).toHaveLength(128);
          expect(harness.pendingTweenCount()).toBe(0);

          resolveAction();
        });

        it('20. cancels a Boss cycle’s animation with the timeline that started it', () => {
          const { harness, ctx, resolveAction } = battleOnBoardWithHeldAction(roundStartBoard());

          push(
            harness,
            battleState({ sequence: 1, turn: 2, board: roundCommittedBoard() })
          );
          emitBatch(harness, 1, roundBatch());

          // The Boss's Fall is in flight — a view is travelling toward the destination
          // cell its delivered report named.
          playUntil(harness, ctx, 9);
          expect(playedPhases(ctx)[8]).toBe('gravity depth=1');
          expect(travellingViews(harness)).toBeGreaterThan(0);

          // A newer authoritative state supersedes the resolution: the travel is
          // cancelled with the timeline that started it and every view converges on
          // the newer board, so a superseded Boss cycle cannot leave the board
          // partially animated.
          recovery(harness, battleState({ sequence: 3, turn: 4, board: deliveredBoard(2) }));

          expect(harness.pendingTweenCount()).toBe(0);
          expect(labelsByCell(harness)).toEqual(expectedLabels(2));
          expect(clearedCells(ctx)).toEqual([]);
          expect(cellsWithoutView(ctx)).toEqual([]);
          expect(viewIncoherences(ctx)).toEqual([]);
          expect(everyViewOnItsOwnCell(ctx)).toBe(true);
          expect(new Set(views(ctx).filter((view) => view !== null)).size).toBe(64);

          resolveAction();
        });
      });
    });
  });

  describe('Player-turn banner lifecycle & deduplication (Defect 1)', () => {
    function playAllTweens(harness: ReturnType<typeof createSceneHarness>): void {
      for (let i = 0; i < 200 && harness.pendingTweenCount() > 0; i++) {
        harness.flushTweens(harness.pendingTweenCount());
      }
    }

    it('1. triggers the player-turn banner at initial player-turn entry without player input', () => {
      const state = battleState({ sequence: 0, turn: 1 });
      const harness = createSceneHarness({ battleState: state, withTweens: true });
      const scene = new BattleScene();
      const ctx = harness.context(scene, 'BattleScene');

      // Before create(), no banner exists.
      expect(harness.texts.some((t) => t.text === 'PLAYER TURN')).toBe(false);

      // Entering the scene starts the player turn.
      runScene(scene, ctx, 'create');

      // Exactly one banner exists immediately without any cell selection or pointer input.
      const banners = harness.texts.filter((t) => t.text === 'PLAYER TURN');
      expect(banners).toHaveLength(1);

      // Two tweens are scheduled: enter scale and exit alpha/destroy.
      expect(harness.pendingTweenCount()).toBe(2);

      // Flushing the tweens finishes the banner and destroys the text object.
      harness.flushTweens(2);
      expect(harness.pendingTweenCount()).toBe(0);
    });

    it('2. emits exactly one banner per player turn despite repeated pushes and input events', async () => {
      const state = battleState({ sequence: 0, turn: 1 });
      const harness = createSceneHarness({ battleState: state, withTweens: true });
      const scene = new BattleScene();
      const ctx = harness.context(scene, 'BattleScene');
      runScene(scene, ctx, 'create');

      // Flush startup banner tweens.
      harness.flushTweens(harness.pendingTweenCount());
      const initialBannerCount = harness.texts.filter((t) => t.text === 'PLAYER TURN').length;
      expect(initialBannerCount).toBe(1);

      // Selecting a cell does not trigger a duplicate banner.
      tapCell(harness, 12);
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      expect(harness.pendingTweenCount()).toBe(0);

      // Deselecting the cell does not trigger a duplicate banner.
      tapCell(harness, 12);
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      expect(harness.pendingTweenCount()).toBe(0);

      // Repeated state push for the same turn does not trigger a duplicate banner.
      harness.setBattleState(battleState({ sequence: 0, turn: 1 }));
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      expect(harness.pendingTweenCount()).toBe(0);

      // Submitting a swap does not emit a duplicate banner.
      const held = holdAction(harness);
      tapCell(harness, 20);
      tapCell(harness, 21);
      held.resolve({ accepted: true });
      await flush();

      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
    });

    it('3. triggers a subsequent player-turn banner after a complete resolution', async () => {
      const state = battleState({ sequence: 0, turn: 1 });
      const harness = createSceneHarness({ battleState: state, withTweens: true });
      const scene = new BattleScene();
      const ctx = harness.context(scene, 'BattleScene');
      runScene(scene, ctx, 'create');

      // Flush initial banner for turn 1.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      harness.flushTweens(harness.pendingTweenCount());

      // Submit an action and deliver round resolution that advances to turn 2.
      const held = holdAction(harness);
      tapCell(harness, 20);
      tapCell(harness, 21);
      held.resolve({ accepted: true });
      await flush();

      // State push arrives with turn: 2, but held during timeline playback.
      push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));
      emitBatch(harness, 1, [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      ]);

      // During resolution playback, turn 2 banner is NOT yet triggered.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);

      // Play all tweens of the round presentation to reach finishPresentation.
      playAllTweens(harness);

      // The new player turn (turn 2) has begun: exactly one new 'PLAYER TURN' banner was triggered and played.
      const turnBanners = harness.texts.filter((t) => t.text === 'PLAYER TURN');
      expect(turnBanners).toHaveLength(2);
      expect(harness.pendingTweenCount()).toBe(0);
    });

    it('4. does not duplicate player-turn banner upon recovery of the same turn', () => {
      const state = battleState({ sequence: 0, turn: 1 });
      const harness = createSceneHarness({ battleState: state, withTweens: true });
      const scene = new BattleScene();
      const ctx = harness.context(scene, 'BattleScene');
      runScene(scene, ctx, 'create');

      // Flush initial banner for turn 1.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      harness.flushTweens(harness.pendingTweenCount());
      expect(harness.pendingTweenCount()).toBe(0);

      // Authoritative recovery snapshot arrives for the same turn (turn 1).
      recovery(harness, battleState({ sequence: 0, turn: 1, board: deliveredBoard(1) }));

      // Exactly one banner total; no duplicate triggered.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      expect(harness.pendingTweenCount()).toBe(0);
      expect(inputLocked(scene, ctx)).toBe(false);
    });

    it('5. resets deduplication on battle replacement and ignores stale callbacks from old battle', async () => {
      const state1 = battleState({ battleId: 'battle-1', sequence: 0, turn: 1 });
      const harness = createSceneHarness({ battleState: state1, withTweens: true });
      const scene = new BattleScene();
      const ctx = harness.context(scene, 'BattleScene');
      runScene(scene, ctx, 'create');

      // Initial banner for battle-1, turn 1.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      harness.flushTweens(harness.pendingTweenCount());
      expect(harness.pendingTweenCount()).toBe(0);

      // Battle replacement: a new battle arrives with battleId: 'battle-2', turn 1.
      const state2 = battleState({ battleId: 'battle-2', sequence: 0, turn: 1 });
      harness.setBattleState(state2);

      // A fresh banner is presented for battle-2's turn 1.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(2);
      harness.flushTweens(harness.pendingTweenCount());
      expect(harness.pendingTweenCount()).toBe(0);

      // A stale event batch from the old battle-1 arrives without matching state.
      harness.emitUnmatchedBattleEvents({
        battleId: 'battle-1',
        serverSequence: 1,
        events: [
          { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
          { type: 'SwapResolved', outcome: 'committed' },
        ],
      });
      await flush();

      // The stale batch is refused; no banner is triggered for battle-1 or duplicated for battle-2.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(2);
      expect(harness.pendingTweenCount()).toBe(0);
    });

    it('6. does not trigger a player-turn banner for terminal battle state', async () => {
      const state = battleState({ sequence: 0, turn: 1 });
      const harness = createSceneHarness({ battleState: state, withTweens: true });
      const scene = new BattleScene();
      const ctx = harness.context(scene, 'BattleScene');
      runScene(scene, ctx, 'create');

      // Flush turn 1 startup banner.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      harness.flushTweens(harness.pendingTweenCount());

      // Submit action and resolve with terminal BattleWon event.
      const held = holdAction(harness);
      tapCell(harness, 20);
      tapCell(harness, 21);
      held.resolve({ accepted: true });
      await flush();

      push(harness, battleState({ sequence: 1, turn: 2, board: deliveredBoard(1) }));
      emitBatch(harness, 1, [
        { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
        { type: 'SwapResolved', outcome: 'committed' },
        { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 850 },
      ]);

      // Play all tweens of the terminal timeline.
      playAllTweens(harness);

      // Battle was won and handed off to ResultScene; NO new player turn banner was triggered.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      expect(harness.sceneStarted.some((s) => s.key === 'ResultScene')).toBe(true);
    });

    it('7. is not triggered merely by unlocking input or ending an arbitrary presentation timeline', async () => {
      const state = battleState({ sequence: 0, turn: 1 });
      const harness = createSceneHarness({ battleState: state, withTweens: true });
      const scene = new BattleScene();
      const ctx = harness.context(scene, 'BattleScene');
      runScene(scene, ctx, 'create');

      // Flush startup banner for turn 1.
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      harness.flushTweens(harness.pendingTweenCount());

      // 1. Action rejection: input locks and unlocks without turn change.
      const held = holdAction(harness);
      tapCell(harness, 20);
      tapCell(harness, 21);
      held.resolve({ accepted: false, reason: 'InvalidSwap' });
      await flush();

      // Input is unlocked, but turn has not changed: banner count remains 1.
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      // Flush any callout fade tween.
      harness.flushTweens(harness.pendingTweenCount());
      expect(harness.pendingTweenCount()).toBe(0);

      // 2. An arbitrary non-turn-advancing event batch (e.g. CardCast where turn remains 1).
      harness.setBattleState(battleState({ sequence: 1, turn: 1, board: deliveredBoard(1) }));
      emitBatch(harness, 1, [
        { type: 'CardCast', cardId: 'test-card' },
      ]);

      // Timeline plays and completes finishPresentation().
      playAllTweens(harness);

      // Ending the arbitrary presentation timeline does NOT infer a new player turn.
      expect(inputLocked(scene, ctx)).toBe(false);
      expect(harness.texts.filter((t) => t.text === 'PLAYER TURN')).toHaveLength(1);
      expect(harness.pendingTweenCount()).toBe(0);
    });
  });
});
