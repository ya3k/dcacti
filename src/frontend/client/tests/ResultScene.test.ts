import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { ResultScene } from '../src/game/scenes/ResultScene';
import type { ResultSceneData } from '../src/game/scenes/ResultScene';
import { BattleScene } from '../src/game/scenes/BattleScene';
import { GameRuntime } from '../src/game/runtime/GameRuntime';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { GameRuntimeState } from '../src/state/GameRuntimeState';
import type { BattleEventsEnvelope, RuntimeBattleState } from '../src/game/runtime/GameRuntimeEvents';
import type {
  BattleResultResponse,
  RewardSummaryResponse,
} from '../src/services/api/BattleModels';
import { SceneEventEmitter } from './support/SceneEventEmitter';

vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({ destroy: vi.fn() })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
  Loader: { Events: { COMPLETE: 'complete' } },
  Input: { Events: { GAMEOBJECT_POINTER_DOWN: 'gameobjectdown' } },
  // Phaser's scene lifecycle events (TASK-204): the engine emits SHUTDOWN /
  // DESTROY on `scene.events` and never calls a scene method by name, so a
  // scene's teardown is attached to these events.
  Scenes: {
    Events: {
      SHUTDOWN: 'shutdown',
      DESTROY: 'destroy',
    },
  },
  // The board's hit area is a real Phaser.Geom.Rectangle; BattleScene's shell
  // constructs one, so the mock must provide the constructor.
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
      static Contains(): boolean {
        return true;
      }
    },
  },
}));

interface SceneHarnessOptions {
  state?: GameRuntimeState;
  withRuntime?: boolean;
  battleState?: RuntimeBattleState | null;
  /**
   * The result the mock runtime's `getBattleResult` resolves with (TASK-149).
   * A rejected promise models the documented `404 BATTLE_NOT_FOUND` /
   * `401 UNAUTHENTICATED` outcomes (API_CONTRACTS.md §4 notes 6–7).
   */
  battleResult?: BattleResultResponse | Error;
}

function createSceneHarness(options: SceneHarnessOptions = {}) {
  const {
    state = INITIAL_RUNTIME_STATE,
    withRuntime = true,
    battleState = null,
    battleResult,
  } = options;
  const runtimeListeners = new Set<(event: { state: GameRuntimeState }) => void>();
  const battleStateListeners = new Set<(state: RuntimeBattleState) => void>();
  const battleEventListeners = new Set<(envelope: BattleEventsEnvelope) => void>();
  const texts: Array<{ text: string; color?: string; destroyed: boolean }> = [];
  const sceneStarted: Array<{ key: string; data?: unknown }> = [];
  const battleResultRequests: string[] = [];
  /**
   * Every object the scene created that can receive a pointer activation, with
   * its geometry — so a test can activate a control by the label a player reads
   * (Phaser draws a control as an interactive rectangle with its label on top at
   * the same centre).
   *
   * `destroyed` is modelled because a scene that re-renders a control must
   * release the previous one: the reward block's `RETRY` is re-created by every
   * render pass, so "exactly one live control" is an assertion about what Phaser
   * still holds, not about how many objects were ever built.
   */
  const clickables: Array<{
    kind: 'tile' | 'label';
    text: string;
    x: number;
    y: number;
    readonly interactive: boolean;
    readonly destroyed: boolean;
    click: () => void;
  }> = [];

  const runtime = {
    getState: () => state,
    onRuntimeEvent: (listener: (event: { state: GameRuntimeState }) => void) => {
      runtimeListeners.add(listener);
      return () => {
        runtimeListeners.delete(listener);
      };
    },
    getBattleState: () => battleState,
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
    requestAction: () => Promise.resolve({ accepted: true }),
    /**
     * The documented client-local post-result cleanup (TASK-203,
     * `GameRuntimePort.clearActiveBattleState`). It is a spy here: the scene's
     * obligation is to ask for the cleanup exactly once per exit, and the
     * runtime's own behavior is covered in `GameRuntime.test.ts`.
     */
    clearActiveBattleState: vi.fn(),
    getBattleResult: (battleId: string) => {
      battleResultRequests.push(battleId);
      if (battleResult instanceof Error) {
        return Promise.reject(battleResult);
      }
      if (battleResult === undefined) {
        return Promise.reject(new Error('BATTLE_NOT_FOUND'));
      }
      return Promise.resolve(battleResult);
    },
    setEngineStatus: vi.fn(),
  };

  function context(scene: object, sceneKey: string): object {
    /** The scene's own emitter (Phaser's `scene.events`) — see TASK-204. */
    const events = new SceneEventEmitter();

    const makeText = (x: number, y: number, value: string) => {
      const entry = { text: value, color: undefined as string | undefined, destroyed: false };
      texts.push(entry);

      const handlers: Array<() => void> = [];
      let interactive = false;

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
        get destroyed() {
          return entry.destroyed;
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
        // Phaser's `GameObject#setAlpha` — the HUD's combat callout resets it when a
        // new message replaces the one on screen (TASK-210 §7).
        setAlpha: () => obj,
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
          entry.destroyed = true;
          handlers.length = 0;
        },
        click: () => {
          for (const handler of [...handlers]) handler();
        },
      };

      clickables.push(obj);
      return obj;
    };

    const makeContainer = () => {
      const children: unknown[] = [];
      let size: { width: number; height: number } | null = null;
      const obj = {
        add: (added: unknown) => {
          const list = Array.isArray(added) ? added : [added];
          children.push(...list);
          return obj;
        },
        removeAll: () => {
          children.length = 0;
          return obj;
        },
        destroy: () => {
          children.length = 0;
        },
        // Phaser's Container has no implicit size and no texture, so it is only a
        // valid input target once `setSize` declares an extent and
        // `setInteractive` derives a hit area from it
        // (.ai/skills/phaser/input-keyboard-mouse-touch).
        setSize: (width: number, height: number) => {
          size = { width, height };
          return obj;
        },
        get width() {
          return size ? size.width : 0;
        },
        get height() {
          return size ? size.height : 0;
        },
        setInteractive: () => {
          // Phaser warns and skips enabling input when a Container has no size.
          return obj;
        },
        on: () => obj,
        off: () => {},
      };
      return obj;
    };

    return Object.assign(Object.create(scene), {
      // Phaser's injected scene event emitter (`this.events` / `sys.events`).
      events,
      scene: {
        start: (key: string, data?: unknown) => sceneStarted.push({ key, data }),
      },
      add: {
        rectangle: (x = 0, y = 0, width = 0, height = 0) => {
          const handlers: Array<() => void> = [];
          let interactive = false;
          let destroyed = false;

          const rect = {
            kind: 'tile' as const,
            text: '',
            x,
            y,
            width,
            height,
            get interactive() {
              return interactive;
            },
            get destroyed() {
              return destroyed;
            },
            // Phaser's `Rectangle#setSize` / `GameObject#setPosition` /
            // `GameObject#setVisible` — the three calls the battle HUD's gauges make
            // when a state push resizes an existing fill instead of allocating one
            // (TASK-210 §15).
            setSize: (nextWidth: number, nextHeight: number) => {
              rect.width = nextWidth;
              rect.height = nextHeight;
              return rect;
            },
            setPosition: (nextX: number, nextY: number) => {
              rect.x = nextX;
              rect.y = nextY;
              return rect;
            },
            setVisible: () => rect,
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
              destroyed = true;
              handlers.length = 0;
            },
            click: () => {
              for (const handler of [...handlers]) handler();
            },
          };

          clickables.push(rect);
          return rect;
        },
        text: (x: number, y: number, value: string) => makeText(x, y, value),
        container: () => makeContainer(),
      },
      registry: {
        get: (key: string) => (withRuntime && key === RUNTIME_REGISTRY_KEY ? runtime : undefined),
      },
      sys: { settings: { key: sceneKey } },
    });
  }

  return {
    runtime,
    texts,
    clickables,
    /**
     * Activates the control a player reads as `label`: the interactive hit area
     * drawn at that label's own centre. Only live objects are considered — a
     * re-rendered control's released predecessor keeps its text but no handler.
     */
    clickControl: (label: string) => {
      const labels = clickables.filter((c) => c.kind === 'label' && c.text === label && !c.destroyed);
      const text = labels[labels.length - 1];
      if (!text) {
        throw new Error(`No control labelled "${label}" was rendered.`);
      }
      const targets = clickables.filter(
        (c) => c.interactive && !c.destroyed && c.kind === 'tile' && c.x === text.x && c.y === text.y
      );
      const target = targets[targets.length - 1];
      if (!target) {
        throw new Error(`The control labelled "${label}" is not an interactive control.`);
      }
      target.click();
    },
    /**
     * The captions of the text objects the scene still holds, in creation order
     * — what a player could read right now, and what `setText` currently says.
     */
    liveCaptions: () =>
      clickables
        .filter((c) => c.kind === 'label' && !c.destroyed)
        .map((c) => c.text),
    runtimeListeners,
    battleStateListeners,
    battleEventListeners,
    battleResultRequests,
    emitBattleEvents: (envelope: BattleEventsEnvelope) => {
      for (const listener of battleEventListeners) {
        listener(envelope);
      }
    },
    sceneStarted,
    /**
     * Takes a scene down exactly as Phaser does: `SceneManager` →
     * `Systems#shutdown` → `scene.events.emit(Phaser.Scenes.Events.SHUTDOWN)`.
     * Nothing here calls a scene method by name (TASK-204).
     */
    shutdownScene: (ctx: object) => {
      (ctx as { events?: SceneEventEmitter }).events?.emit('shutdown');
    },
    context,
  };
}

function runScene(scene: object, ctx: object, method: string, ...args: unknown[]): void {
  const fn = Object.getPrototypeOf(scene)[method] as ((this: object, ...args: unknown[]) => void) | undefined;
  fn?.call(ctx, ...args);
}

describe('ResultScene presentation (TDD.md §2.1, SIGNALR_PROTOCOL.md §3.2.19)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('exposes the documented ResultScene class', () => {
    expect(new ResultScene()).toBeInstanceOf(ResultScene);
  });

  it('displays Victory presentation for outcome = "victory" with non-derived HP values', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    const resultData: ResultSceneData = {
      outcome: 'victory',
      finalBossHp: 37,
      finalPlayerHp: 812,
    };

    runScene(scene, ctx, 'create', resultData);

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/VICTORY/i);
    expect(rendered).toContain('Boss HP: 37');
    expect(rendered).toContain('Pet HP: 812');
  });

  it('displays Defeat presentation for outcome = "defeat" with delivered HP values verbatim', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    const resultData: ResultSceneData = {
      outcome: 'defeat',
      finalBossHp: 412,
      finalPlayerHp: 0,
    };

    runScene(scene, ctx, 'create', resultData);

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/DEFEAT/i);
    expect(rendered).toContain('Boss HP: 412');
    expect(rendered).toContain('Pet HP: 0');
  });

  it('does NOT determine victory/defeat from HP values (outcome is authoritative)', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    // Intentionally contrary to naive HP derivation: outcome is defeat even though boss HP is 0
    const resultData: ResultSceneData = {
      outcome: 'defeat',
      finalBossHp: 0,
      finalPlayerHp: 100,
    };

    runScene(scene, ctx, 'create', resultData);

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/DEFEAT/i);
    expect(rendered).not.toMatch(/VICTORY/i);
    expect(rendered).toContain('Boss HP: 0');
    expect(rendered).toContain('Pet HP: 100');
  });

  it('renders fallback when no result data is provided', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('NO RESULT');
  });

  it('cleans up references when Phaser takes the scene down', () => {
    // The engine's own teardown path (TASK-205) — not a call to the scene's
    // method: `Systems#shutdown` emits SHUTDOWN and only a handler registered
    // against that event runs.
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 50,
    });

    expect(() => harness.shutdownScene(ctx)).not.toThrow();
    expect((ctx as { resultData: unknown }).resultData).toBeNull();

    // The stage's own assertions are unchanged: the scene still reaches no
    // transport and derives no outcome from HP.
    expect(harness.sceneStarted).toEqual([]);
  });

  it('attaches its teardown to the engine lifecycle events, not to a method name', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 50,
    });

    expect((ctx as { events: SceneEventEmitter }).events.listenerCount('shutdown')).toBe(1);
    expect((ctx as { events: SceneEventEmitter }).events.listenerCount('destroy')).toBe(1);

    // A shut-down and restarted scene keeps exactly one teardown per event.
    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create', {
      outcome: 'defeat',
      finalBossHp: 10,
      finalPlayerHp: 0,
    });

    expect((ctx as { events: SceneEventEmitter }).events.listenerCount('shutdown')).toBe(1);
    expect((ctx as { events: SceneEventEmitter }).events.listenerCount('destroy')).toBe(1);
  });

  it('contains no client-side outcome derivation or direct transport access', () => {
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/ResultScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    // TASK-149 stage advance. This assertion previously also listed
    // 'RewardSummary', '/api/battle/', and 'getBattleResult' as forbidden,
    // because no reward member was rendered and the scene reached no result
    // route. TASK-149 implements exactly that: `ResultScene` now reads the
    // persisted result through the GameRuntime port and renders its `rewards`
    // (API_CONTRACTS.md §4 note 1, DATABASE.md §1). The historical *assertion*
    // is superseded by the current authoritative contract, not TASK-087 itself
    // (completed tasks are immutable, TASK_LIFECYCLE.md §3).
    //
    // Every property that still holds is preserved: the scene derives no
    // outcome from HP, and it still reaches no transport directly — the
    // remaining forbidden tokens are the transport implementations, and
    // RuntimeBoundaries.test.ts independently enforces the same boundary for
    // every registered scene.
    for (const forbidden of [
      'finalBossHp <= 0',
      'finalBossHp === 0',
      'finalPlayerHp <= 0',
      'finalPlayerHp === 0',
      'ApiService',
      'services/api/ApiService',
      'services/realtime',
      '@microsoft/signalr',
      'HubConnection',
    ]) {
      expect(source, `ResultScene must not contain "${forbidden}"`).not.toContain(forbidden);
    }

    // The result route is reached through the runtime port, never spelled as a
    // URL or fetched by the scene (ARCHITECTURE.md §2.2.1 rule 1).
    expect(source).not.toMatch(/\bfetch\s*\(/);
    expect(source).toContain('readRuntime');
  });
});

describe('ResultScene post-result navigation (TASK-203, D-202-01 = C, D-202-02 = A, D-202-04 = A)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** A completed battle's outcome handoff, exactly as BattleScene delivers it. */
  const completedBattle: ResultSceneData = {
    outcome: 'victory',
    finalBossHp: 0,
    finalPlayerHp: 850,
    battleId: 'battle-1',
  };

  function present(data: ResultSceneData = completedBattle) {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', data);

    return { harness, scene, ctx };
  }

  it('renders exactly the two approved explicit controls', () => {
    const { harness } = present();

    const rendered = harness.texts.map((t) => t.text);
    expect(rendered).toContain('PLAY AGAIN');
    expect(rendered).toContain('MAIN MENU');

    // Two interactive hit areas, and the shell background is not one of them:
    // the controls are the scene's only input surface (D-202-02 = A).
    expect(harness.clickables.filter((c) => c.interactive)).toHaveLength(2);
  });

  it('does not navigate on its own — no automatic transition', async () => {
    const { harness } = present();

    await Promise.resolve();
    await Promise.resolve();

    expect(harness.sceneStarted).toEqual([]);
  });

  it('PLAY AGAIN clears the completed battle and opens LobbyScene with the preserved loadout', () => {
    const { harness } = present();

    harness.clickControl('PLAY AGAIN');

    // D-202-04 = A: the completed battle's active battle state is cleared before
    // the scene is left, and D-202-03 = D: the Lobby is told to restore the
    // loadout the battle just ended with (ADR-022).
    expect(harness.runtime.clearActiveBattleState).toHaveBeenCalledTimes(1);
    expect(harness.sceneStarted).toEqual([
      { key: 'LobbyScene', data: { restorePreservedLoadout: true } },
    ]);
  });

  it('MAIN MENU clears the completed battle and opens MainMenuScene only', () => {
    const { harness } = present();

    harness.clickControl('MAIN MENU');

    expect(harness.runtime.clearActiveBattleState).toHaveBeenCalledTimes(1);
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
    // Nothing carries the preserve flag on this destination.
    expect(harness.sceneStarted[0].data).toBeUndefined();
  });

  it('fires at most once per scene instance, whichever control is activated', () => {
    const { harness } = present();

    harness.clickControl('PLAY AGAIN');
    harness.clickControl('PLAY AGAIN');
    harness.clickControl('MAIN MENU');

    expect(harness.sceneStarted).toHaveLength(1);
    expect(harness.sceneStarted[0].key).toBe('LobbyScene');
    // The guard is claimed before the cleanup, so a repeat activation does not
    // even ask for a second cleanup.
    expect(harness.runtime.clearActiveBattleState).toHaveBeenCalledTimes(1);
  });

  it('still navigates when no runtime is published', () => {
    const harness = createSceneHarness({ withRuntime: false });
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');
    runScene(scene, ctx, 'create', completedBattle);

    expect(() => harness.clickControl('PLAY AGAIN')).not.toThrow();
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['LobbyScene']);
  });

  it('keeps the outcome presentation while offering the controls', () => {
    const { harness } = present();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/VICTORY/i);
    expect(rendered).toContain('Boss HP: 0');
    expect(rendered).toContain('Pet HP: 850');

    harness.clickControl('PLAY AGAIN');

    // Leaving does not rewrite what the completed battle delivered.
    const afterLeave = harness.texts.map((t) => t.text).join('\n');
    expect(afterLeave).toContain('Boss HP: 0');
  });

  it('navigates through the runtime port only, with no other input mechanism', () => {
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/ResultScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    // The approved destinations, by their documented scene keys.
    expect(source).toContain("'LobbyScene'");
    expect(source).toContain("'MainMenuScene'");
    expect(source).toContain('PLAY AGAIN');
    expect(source).toContain('MAIN MENU');

    // D-202-04 = A + ARCHITECTURE.md §2.2.1: the cleanup is a port capability,
    // never a transport call.
    expect(source).toContain('clearActiveBattleState');
    expect(source).toContain('readRuntime');

    // D-202-02 = A: no keyboard-only path, no full-screen tap, no automatic
    // timed transition is the way forward.
    expect(source).not.toMatch(/keyboard/i);
    expect(source).not.toMatch(/delayedCall|setTimeout|setInterval/);

    // The stale pre-TASK-202 lifecycle comment must not survive.
    expect(source).not.toContain('Navigation after ResultScene is not in scope');
  });
});

describe('ResultScene reward presentation (TASK-149, API_CONTRACTS.md §4 note 1, DATABASE.md §1)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** One delivered `RewardSummary` — the 8-member contract in force (`DATABASE.md` §1). */
  function deliveredRewards(overrides: Partial<RewardSummaryResponse> = {}): RewardSummaryResponse {
    return {
      playerXpGained: 100,
      newPlayerXp: 400,
      playerLeveledUp: true,
      newPlayerLevel: 5,
      petXpGained: 100,
      newPetXp: 900,
      petLeveledUp: false,
      newPetLevel: 10,
      ...overrides,
    };
  }

  function deliveredResult(rewards: RewardSummaryResponse): BattleResultResponse {
    return {
      battleId: 'b-1',
      outcome: 'victory',
      rewards,
      durationTurns: 4,
    };
  }

  async function renderWithResult(
    data: ResultSceneData,
    battleResult: BattleResultResponse | Error
  ) {
    const harness = createSceneHarness({ battleResult });
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', data);

    // The reward read is asynchronous; the outcome render is not, so flushing
    // the microtask queue is what lets the delivered members land.
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();

    return harness;
  }

  const outcomeData: ResultSceneData = {
    outcome: 'victory',
    finalBossHp: 0,
    finalPlayerHp: 320,
    battleId: 'b-1',
  };

  it('reads the persisted result through the runtime port, once, by battleId', async () => {
    const harness = await renderWithResult(outcomeData, deliveredResult(deliveredRewards()));

    expect(harness.battleResultRequests).toEqual(['b-1']);
  });

  it('renders every documented Player-track member verbatim (DATABASE.md §1 item 1)', async () => {
    const harness = await renderWithResult(
      outcomeData,
      deliveredResult(
        deliveredRewards({
          playerXpGained: 100,
          newPlayerXp: 400,
          playerLeveledUp: true,
          newPlayerLevel: 5,
        })
      )
    );

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('+100 XP');
    expect(rendered).toContain('400');
    expect(rendered).toContain('5');
    expect(rendered).toMatch(/LEVEL UP/);
  });

  it('renders every documented Pet-track member verbatim (DATABASE.md §1 item 2)', async () => {
    const harness = await renderWithResult(
      outcomeData,
      deliveredResult(
        deliveredRewards({
          petXpGained: 100,
          newPetXp: 900,
          petLeveledUp: false,
          newPetLevel: 10,
        })
      )
    );

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('900');
    expect(rendered).toContain('10');
  });

  it('renders both tracks distinguishably', async () => {
    const harness = await renderWithResult(outcomeData, deliveredResult(deliveredRewards()));

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Player:');
    expect(rendered).toContain('Pet:');
    expect(rendered).toContain('REWARDS');
  });

  it('renders a defeat’s zero grants as 0, never as an omission (DATABASE.md §1 item 5)', async () => {
    const harness = await renderWithResult(
      { ...outcomeData, outcome: 'defeat', finalPlayerHp: 0 },
      deliveredResult(
        deliveredRewards({
          playerXpGained: 0,
          petXpGained: 0,
          newPlayerXp: 300,
          newPetXp: 800,
          playerLeveledUp: false,
          petLeveledUp: false,
        })
      )
    );

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('+0 XP');
    expect(rendered).not.toMatch(/LEVEL UP/);
  });

  it('renders a null resulting value as unavailable, never as 0 (DATABASE.md §1 item 4)', async () => {
    const harness = await renderWithResult(
      outcomeData,
      deliveredResult(
        deliveredRewards({
          newPlayerXp: null,
          newPlayerLevel: null,
          playerLeveledUp: null,
          newPetXp: null,
          newPetLevel: null,
          petLeveledUp: null,
        })
      )
    );

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('—');
    // A null member must not become a number.
    expect(rendered).not.toContain('XP 0');
    expect(rendered).not.toContain('Level 0');
  });

  it('renders the delivered leveledUp flags without re-deriving them', async () => {
    // Contrary on purpose: the Level is unchanged numerically, but the server
    // reports the level-up. The scene prints what was delivered.
    const harness = await renderWithResult(
      outcomeData,
      deliveredResult(
        deliveredRewards({
          newPlayerLevel: 4,
          playerLeveledUp: true,
          newPetLevel: 10,
          petLeveledUp: false,
        })
      )
    );

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/LEVEL UP/);
  });

  it('reports a rejected result read as a readable, recoverable failure (TASK-211 §5)', async () => {
    const harness = await renderWithResult(
      outcomeData,
      new Error('BATTLE_NOT_FOUND')
    );

    const texts = harness.texts.map((t) => t.text);
    const rendered = texts.join('\n');
    expect(rendered).toMatch(/VICTORY/i);
    expect(rendered).toContain('Boss HP: 0');
    expect(rendered).toContain('Pet HP: 320');

    // The failure is stated in the reward block instead of leaving it blank. A
    // failed read is NOT a zero reward: `REWARDS` + `+0 XP` is a delivered fact
    // (DATABASE.md §1 item 5) and must stay distinguishable from it.
    expect(rendered).toContain('Rewards could not be loaded.');
    expect(rendered).toContain('Please try again.');
    expect(rendered).not.toContain('REWARDS');
    expect(rendered).not.toContain('+0 XP');

    // The failure is recoverable, and both approved exits stay usable.
    expect(texts).toContain('RETRY');
    expect(texts).toContain('PLAY AGAIN');
    expect(texts).toContain('MAIN MENU');
  });

  it('issues no result request when the handoff carries no battleId', async () => {
    const harness = createSceneHarness({ battleResult: deliveredResult(deliveredRewards()) });
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 320,
    });

    await Promise.resolve();

    expect(harness.battleResultRequests).toEqual([]);
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/VICTORY/i);
  });

  it('issues no result request when no runtime is published', async () => {
    const harness = createSceneHarness({ withRuntime: false });
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', outcomeData);

    await Promise.resolve();

    expect(harness.battleResultRequests).toEqual([]);
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/VICTORY/i);
  });

  it('renders the outcome before the reward read resolves', () => {
    const harness = createSceneHarness({ battleResult: deliveredResult(deliveredRewards()) });
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', outcomeData);

    // Synchronously after create(), the outcome is already rendered while the
    // reward read is still pending.
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/VICTORY/i);
    expect(rendered).toContain('Boss HP: 0');
  });

  it('cleans up reward presentation references when Phaser takes the scene down', () => {
    const harness = createSceneHarness({ battleResult: deliveredResult(deliveredRewards()) });
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', outcomeData);

    expect(() => harness.shutdownScene(ctx)).not.toThrow();
    expect((ctx as { rewardText: unknown }).rewardText).toBeNull();
  });
});

describe('ResultScene reward loading states and RETRY (TASK-211 §5–§6)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** One delivered `RewardSummary` — the 8-member contract in force (`DATABASE.md` §1). */
  function rewards(overrides: Partial<RewardSummaryResponse> = {}): RewardSummaryResponse {
    return {
      playerXpGained: 100,
      newPlayerXp: 400,
      playerLeveledUp: true,
      newPlayerLevel: 5,
      petXpGained: 100,
      newPetXp: 900,
      petLeveledUp: false,
      newPetLevel: 10,
      ...overrides,
    };
  }

  function deliveredResult(
    overrides: Partial<BattleResultResponse> = {}
  ): BattleResultResponse {
    return {
      battleId: 'b-1',
      outcome: 'victory',
      rewards: rewards(),
      durationTurns: 7,
      ...overrides,
    };
  }

  const OUTCOME: ResultSceneData = {
    outcome: 'victory',
    finalBossHp: 0,
    finalPlayerHp: 320,
    battleId: 'b-1',
  };

  /** A deferred `getBattleResult` the test resolves by hand. */
  function deferred(): { readonly promise: Promise<BattleResultResponse>; readonly release: (value: BattleResultResponse) => void } {
    let release: (value: BattleResultResponse) => void = () => {};
    const promise = new Promise<BattleResultResponse>((resolve) => {
      release = resolve;
    });
    return { promise, release };
  }

  /** Renders the scene and returns the harness plus the live captions. */
  async function present(battleResult: BattleResultResponse | Error) {
    const harness = createSceneHarness({ battleResult });
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', OUTCOME);
    await flush();

    return { harness, scene, ctx };
  }

  async function flush(): Promise<void> {
    for (let i = 0; i < 5; i++) {
      await Promise.resolve();
    }
  }

  /** The captions of the text objects the current presentation still holds. */
  function captions(harness: ReturnType<typeof createSceneHarness>): string[] {
    return harness.liveCaptions();
  }

  it('shows an explicit loading state before the read settles', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    const pending = deferred();
    harness.runtime.getBattleResult = vi.fn(() => pending.promise) as never;

    runScene(scene, ctx, 'create', OUTCOME);

    // Synchronously after create() the block says what it is doing, so an
    // in-flight read is distinguishable from one that failed (the documented
    // defect: both looked like an empty box).
    expect(captions(harness)).toContain('Loading rewards…');
    expect(captions(harness)).not.toContain('RETRY');
    expect(harness.texts.map((t) => t.text).join('\n')).not.toContain('REWARDS');
  });

  it('shows the delivered members on success, with the delivered duration', async () => {
    const { harness } = await present(deliveredResult({ durationTurns: 7 }));

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('REWARDS');
    expect(rendered).toContain('Player: +100 XP');
    expect(rendered).toContain('Pet: +100 XP');

    // The battle's length is the delivered `durationTurns` — not a locally
    // counted turn, a timestamp difference, or a combat-event count.
    expect(rendered).toContain('Duration: 7 turns');

    // A success is not a failure state: no RETRY is offered, and the two
    // approved exits remain.
    expect(captions(harness)).not.toContain('RETRY');
    expect(captions(harness)).toContain('PLAY AGAIN');
    expect(captions(harness)).toContain('MAIN MENU');
  });

  it('renders a delivered zero-turn battle as 0, never as an absence', async () => {
    const { harness } = await present(deliveredResult({ durationTurns: 0 }));

    // `DATABASE.md` §1: a battle reaching a terminal state before any committed
    // Swap records 0, which is a value rather than an absent member.
    expect(harness.texts.map((t) => t.text).join('\n')).toContain('Duration: 0 turns');
  });

  it('offers RETRY only on failure, and keeps both exits usable', async () => {
    const { harness } = await present(new Error('BATTLE_NOT_FOUND'));

    expect(captions(harness)).toContain('RETRY');
    expect(captions(harness)).toContain('PLAY AGAIN');
    expect(captions(harness)).toContain('MAIN MENU');

    // Leaving is still possible from the failure state, and the completed
    // battle's active battle state is still cleared on the way out.
    harness.clickControl('MAIN MENU');
    expect(harness.runtime.clearActiveBattleState).toHaveBeenCalledTimes(1);
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('PLAY AGAIN remains usable after a reward failure, and restarts no battle', async () => {
    const { harness } = await present(new Error('BATTLE_NOT_FOUND'));

    harness.clickControl('PLAY AGAIN');

    expect(harness.runtime.clearActiveBattleState).toHaveBeenCalledTimes(1);
    expect(harness.sceneStarted).toEqual([
      { key: 'LobbyScene', data: { restorePreservedLoadout: true } },
    ]);
    // Nothing about the completed battle was re-run: the failing read is not
    // repeated by leaving, and no battle route was called again.
    expect(harness.battleResultRequests).toEqual(['b-1']);
  });

  it('RETRY repeats only the reward read and renders the delivered members', async () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    /** Every battle the scene addressed, in order. */
    const reads: string[] = [];

    // First read fails, the retry succeeds — the same scene instance throughout.
    let attempt = 0;
    harness.runtime.getBattleResult = vi.fn(async (battleId: string) => {
      reads.push(battleId);
      attempt += 1;
      if (attempt === 1) {
        throw new Error('BATTLE_NOT_FOUND');
      }
      return deliveredResult({ durationTurns: 3 });
    }) as never;

    runScene(scene, ctx, 'create', OUTCOME);
    await flush();

    expect(captions(harness)).toContain('RETRY');

    harness.clickControl('RETRY');
    await flush();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('REWARDS');
    expect(rendered).toContain('Duration: 3 turns');
    // The retry replaced the failure state; it did not add to it.
    expect(rendered).not.toContain('Rewards could not be loaded.');
    expect(rendered.match(/REWARDS/g)).toHaveLength(1);
    expect(reads).toEqual(['b-1', 'b-1']);
  });

  it('repeated RETRY never duplicates the reward block, the control, or a read', async () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    /** Every battle the scene addressed, in order. */
    const reads: string[] = [];

    harness.runtime.getBattleResult = vi.fn(async (battleId: string) => {
      reads.push(battleId);
      throw new Error('BATTLE_NOT_FOUND');
    }) as never;

    runScene(scene, ctx, 'create', OUTCOME);
    await flush();

    for (let press = 0; press < 4; press++) {
      harness.clickControl('RETRY');
      await flush();
    }

    const rendered = harness.texts.map((t) => t.text).join('\n');
    // Four presses issued four sequential reads — never two at once — and the
    // presentation still holds exactly one failure statement.
    expect(reads).toHaveLength(5);
    expect(rendered.match(/Rewards could not be loaded\./g)).toHaveLength(1);
    // The live control set is unchanged: exactly one RETRY, and the two exits.
    const live = captions(harness).filter((t) => t === 'RETRY');
    expect(live).toHaveLength(1);
    expect((ctx as { rewardState: string }).rewardState).toBe('failure');
  });

  it('a stale reward read cannot overwrite a newer retry', async () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    const first = deferred();
    const second = deferred();
    let call = 0;
    harness.runtime.getBattleResult = vi.fn(() => {
      call += 1;
      return call === 1 ? first.promise : second.promise;
    }) as never;

    runScene(scene, ctx, 'create', OUTCOME);
    expect((ctx as { rewardState: string }).rewardState).toBe('loading');

    // The retry can only be reached from a settled failure, so the first read is
    // failed by hand, and the retry then starts a second read.
    first.release(deliveredResult({ rewards: undefined as never }));
    await flush();
    expect((ctx as { rewardState: string }).rewardState).toBe('failure');

    harness.clickControl('RETRY');
    await flush();
    expect((ctx as { rewardState: string }).rewardState).toBe('loading');

    // The retry settles with the delivered members...
    second.release(deliveredResult({ durationTurns: 11 }));
    await flush();
    expect(harness.texts.map((t) => t.text).join('\n')).toContain('Duration: 11 turns');
  });

  it('treats an absent or malformed rewards payload as the failure state, not as data', async () => {
    // A contract drift the shipped backend does not produce: the shape the
    // reward block reads is missing. It must degrade to the failure state rather
    // than render `undefined` under a `REWARDS` heading.
    const malformed = [
      { ...deliveredResult(), rewards: undefined },
      { ...deliveredResult(), rewards: null },
      { ...deliveredResult(), rewards: {} },
      { ...deliveredResult(), rewards: { ...rewards(), newPlayerXp: 'many' } },
      { ...deliveredResult(), durationTurns: undefined },
    ];

    for (const payload of malformed) {
      const harness = createSceneHarness();
      const scene = new ResultScene();
      const ctx = harness.context(scene, 'ResultScene');
      harness.runtime.getBattleResult = vi.fn(async () => payload as never) as never;

      runScene(scene, ctx, 'create', OUTCOME);
      await flush();

      const rendered = harness.texts.map((t) => t.text).join('\n');
      expect(rendered).toContain('Rewards could not be loaded.');
      expect(rendered).not.toContain('REWARDS');
      expect(rendered).not.toContain('undefined');
      expect((ctx as { rewardState: string }).rewardState).toBe('failure');
    }
  });

  it('reports an unaddressable read as unavailable and offers nothing to retry', async () => {
    const harness = createSceneHarness({ withRuntime: false });
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', OUTCOME);
    await flush();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('Rewards are unavailable for this battle.');
    // Nothing to repeat, so no inert control is offered.
    expect(captions(harness)).not.toContain('RETRY');
    expect(harness.battleResultRequests).toEqual([]);
  });

  it('clears the RETRY control and its handler when Phaser takes the scene down', async () => {
    const { harness, ctx } = await present(new Error('BATTLE_NOT_FOUND'));

    expect(captions(harness)).toContain('RETRY');

    expect(() => harness.shutdownScene(ctx)).not.toThrow();
    expect((ctx as { rewardRetryButton: unknown }).rewardRetryButton).toBeNull();
    expect((ctx as { rewardRetryLabel: unknown }).rewardRetryLabel).toBeNull();
  });

  it('renders the delivered terminal Pet HP and never a Player HP value', async () => {
    const { harness } = await present(deliveredResult());

    const texts = captions(harness);
    expect(texts).toContain('Final Pet HP: 320');
    expect(texts.some((t) => t.includes('Player HP'))).toBe(false);
  });
});

describe('GameRuntime result port (TASK-149, API_CONTRACTS.md §4)', () => {
  it('delegates getBattleResult to the ApiService method with the supplied battleId', async () => {
    const delivered = {
      battleId: 'b-9',
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
    };

    const api = {
      getBattleResult: vi.fn().mockResolvedValue(delivered),
    };
    const signalR = {} as never;

    const runtime = new GameRuntime(signalR, api as never);
    const result = await runtime.getBattleResult('b-9');

    expect(api.getBattleResult).toHaveBeenCalledTimes(1);
    expect(api.getBattleResult).toHaveBeenCalledWith('b-9');
    // The response is transported unchanged — no member is rewritten.
    expect(result).toEqual(delivered);
  });

  it('propagates a rejection without fabricating a result', async () => {
    const api = {
      getBattleResult: vi.fn().mockRejectedValue(new Error('BATTLE_NOT_FOUND')),
    };

    const runtime = new GameRuntime({} as never, api as never);

    await expect(runtime.getBattleResult('b-9')).rejects.toThrow('BATTLE_NOT_FOUND');
  });
});

describe('BattleScene outcome handoff (TASK-087, SIGNALR_PROTOCOL.md §3.2.19)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  function createBattle() {
    const harness = createSceneHarness();
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  it('subscribes to onBattleEvents in create()', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');

    expect(harness.battleEventListeners.size).toBe(1);
  });

  it('unsubscribes from onBattleEvents on the engine teardown', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.battleEventListeners.size).toBe(1);

    harness.shutdownScene(ctx);
    expect(harness.battleEventListeners.size).toBe(0);
  });

  it('transitions to ResultScene on BattleWon with verbatim values', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 5,
      events: [
        {
          type: 'BattleWon',
          outcome: 'victory',
          finalBossHp: 37,
          finalPlayerHp: 812,
        },
      ],
    });

    expect(harness.sceneStarted).toHaveLength(1);
    expect(harness.sceneStarted[0]).toEqual({
      key: 'ResultScene',
      data: {
        outcome: 'victory',
        finalBossHp: 37,
        finalPlayerHp: 812,
        // TASK-149 adds the batch's battleId so ResultScene can address the
        // documented result endpoint (API_CONTRACTS.md §4). The outcome members
        // are unchanged.
        battleId: 'b-1',
      },
    });
  });

  it('transitions to ResultScene on BattleLost with verbatim values', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-2',
      serverSequence: 9,
      events: [
        {
          type: 'BattleLost',
          outcome: 'defeat',
          finalBossHp: 412,
          finalPlayerHp: 0,
        },
      ],
    });

    expect(harness.sceneStarted).toHaveLength(1);
    expect(harness.sceneStarted[0]).toEqual({
      key: 'ResultScene',
      data: {
        outcome: 'defeat',
        finalBossHp: 412,
        finalPlayerHp: 0,
        // TASK-149: the batch's battleId travels with the outcome handoff.
        battleId: 'b-2',
      },
    });
  });

  it('does NOT transition when envelope contains no BattleWon / BattleLost', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 2,
      events: [
        { type: 'TurnStarted', turn: 1 },
        { type: 'DamageDealt', damage: 50 },
        { type: 'TurnEnded', turn: 1 },
      ],
    });

    expect(harness.sceneStarted).toHaveLength(0);
  });

  it('transitions when BattleWon is surrounded by other resolution events in the batch', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 6,
      events: [
        { type: 'DamageDealt', damage: 100 },
        {
          type: 'BattleWon',
          outcome: 'victory',
          finalBossHp: 0,
          finalPlayerHp: 250,
        },
        { type: 'TurnEnded', turn: 3 },
      ],
    });

    expect(harness.sceneStarted).toEqual([
      {
        key: 'ResultScene',
        data: {
          outcome: 'victory',
          finalBossHp: 0,
          finalPlayerHp: 250,
          battleId: 'b-1',
        },
      },
    ]);
  });

  it('transitions only once on duplicate BattleWon events', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    const wonPayload = {
      type: 'BattleWon',
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 100,
    };

    // Duplicate in same envelope
    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 5,
      events: [wonPayload, wonPayload],
    });

    // Duplicate in subsequent envelope
    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 6,
      events: [wonPayload],
    });

    expect(harness.sceneStarted).toHaveLength(1);
  });

  it('transitions only once on duplicate BattleLost events', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    const lostPayload = {
      type: 'BattleLost',
      outcome: 'defeat',
      finalBossHp: 200,
      finalPlayerHp: 0,
    };

    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 5,
      events: [lostPayload],
    });

    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 6,
      events: [lostPayload],
    });

    expect(harness.sceneStarted).toHaveLength(1);
  });

  it('does NOT transition if BattleScene is shut down before the outcome arrives', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    harness.shutdownScene(ctx);

    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 5,
      events: [
        {
          type: 'BattleWon',
          outcome: 'victory',
          finalBossHp: 0,
          finalPlayerHp: 100,
        },
      ],
    });

    expect(harness.sceneStarted).toHaveLength(0);
  });
});
