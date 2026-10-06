import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { BattleHistoryScene } from '../src/game/scenes/BattleHistoryScene';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { BattleHistoryItemResponse } from '../src/services/api/BattleModels';
import { SceneEventEmitter } from './support/SceneEventEmitter';

/**
 * TASK-206 — BattleHistoryScene.
 *
 * Phaser's real `Scene` cannot run headlessly in jsdom, and its `add` / `scene` /
 * `registry` members are prototype getters that cannot be assigned onto an
 * instance. The harness therefore builds a `this` context whose prototype is the
 * real scene instance, so the scene's own private helpers resolve, and overrides
 * only the Phaser collaborators the scene calls — the same technique
 * `CollectionViewerScene.test.ts` uses.
 *
 * The runtime double is strict about the boundary: the only capability the scene
 * may read is `getBattleHistory`, and the double records every call, so a scene
 * that reached any other capability, issued a second read, or bypassed the port
 * would be visible here.
 */
vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({ destroy: vi.fn() })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
  Loader: { Events: { COMPLETE: 'complete' } },
  Input: { Events: { GAMEOBJECT_POINTER_DOWN: 'gameobjectdown' } },
  // Phaser's scene lifecycle events (TASK-205): the engine emits SHUTDOWN /
  // DESTROY on `scene.events` and never calls a scene method merely because one
  // exists, so the scene attaches its teardown to these events and so does this
  // suite.
  Scenes: {
    Events: {
      SHUTDOWN: 'shutdown',
      DESTROY: 'destroy',
    },
  },
}));

/** One delivered `RewardSummary` (`DATABASE.md` §1) — the eight members. */
function rewards(
  overrides: Partial<BattleHistoryItemResponse['rewards']> = {}
): BattleHistoryItemResponse['rewards'] {
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

/** One `GET /api/battle/history` element (`API_CONTRACTS.md` §4.5). */
function historyItem(
  overrides: Partial<BattleHistoryItemResponse> = {}
): BattleHistoryItemResponse {
  return {
    battleId: 'battle-001',
    outcome: 'victory',
    rewards: rewards(),
    durationTurns: 12,
    completedAt: '2026-10-05T09:15:00Z',
    ...overrides,
  };
}

/**
 * Two delivered battles in the contract's own order — newest first
 * (`API_CONTRACTS.md` §4.5 note 4). The ids and completion readings are
 * deliberately not ordered by `battleId`, so a client that sorted or inferred an
 * order would be visible.
 */
const DELIVERED_HISTORY: BattleHistoryItemResponse[] = [
  historyItem({
    battleId: 'battle-zzz',
    outcome: 'victory',
    durationTurns: 12,
    completedAt: '2026-10-05T09:15:00Z',
    rewards: rewards({ newPlayerLevel: 5, newPlayerXp: 400, playerLeveledUp: true }),
  }),
  historyItem({
    battleId: 'battle-aaa',
    outcome: 'defeat',
    durationTurns: 3,
    completedAt: '2026-10-04T21:00:00Z',
    rewards: rewards({
      playerXpGained: 0,
      newPlayerLevel: 4,
      newPlayerXp: 300,
      playerLeveledUp: false,
      petXpGained: 0,
    }),
  }),
];

interface Clickable {
  readonly text: string;
  readonly interactive: boolean;
  readonly x?: number;
  readonly y?: number;
  readonly width?: number;
  readonly height?: number;
  click(): void;
}

interface SceneHarnessOptions {
  withRuntime?: boolean;
  history?: BattleHistoryItemResponse[];
  /** When set, every history read rejects with it. */
  historyFailure?: Error | null;
  /**
   * When set, the first read rejects and every later read succeeds — used to
   * prove `RETRY` really re-reads.
   */
  failFirstLoadOnly?: boolean;
  /** Resolves the pending history read, so the loading state is observable. */
  deferLoad?: boolean;
  /**
   * Whether writing to a destroyed `Text` throws (the browser evidence of a
   * stale-write defect) or is merely recorded. Defaults to throwing; a test that
   * observes an *asynchronous* write sets it to `false` and asserts on
   * `destroyedWriteAttempts`.
   */
  textsThrowWhenDestroyed?: boolean;
}

function createHistoryHarness(options: SceneHarnessOptions = {}) {
  const {
    withRuntime = true,
    history = DELIVERED_HISTORY,
    historyFailure = null,
    failFirstLoadOnly = false,
    deferLoad = false,
    textsThrowWhenDestroyed = true,
  } = options;

  const texts: string[] = [];
  const clickables: Clickable[] = [];
  const liveTexts = new Set<{ readonly text: string }>();
  const liveRectangles = new Set<{ readonly text: string }>();
  /** Every history read the scene performed, in order. */
  const historyReads: string[] = [];
  /** Every other runtime capability the scene reached for, if any. */
  const otherCapabilityCalls: string[] = [];
  const sceneStarted: Array<{ key: string }> = [];
  /** Every write a scene attempted against an engine-destroyed `Text`. */
  const destroyedWriteAttempts: string[] = [];

  let loadAttempts = 0;
  let releaseLoad: () => void = () => {};
  const loadGate = deferLoad
    ? new Promise<void>((resolve) => {
        releaseLoad = resolve;
      })
    : Promise.resolve();

  const shouldFail = (): boolean => {
    if (historyFailure === null) {
      return false;
    }
    if (failFirstLoadOnly) {
      return loadAttempts === 1;
    }
    return true;
  };

  const runtime = {
    getState: () => INITIAL_RUNTIME_STATE,
    getBattleState: () => {
      otherCapabilityCalls.push('getBattleState');
      return null;
    },
    onRuntimeEvent: () => {
      otherCapabilityCalls.push('onRuntimeEvent');
      return () => {};
    },
    onBattleEvents: () => {
      otherCapabilityCalls.push('onBattleEvents');
      return () => {};
    },
    onBattleState: () => {
      otherCapabilityCalls.push('onBattleState');
      return () => {};
    },
    requestAction: () => {
      otherCapabilityCalls.push('requestAction');
      return Promise.reject(new Error('not used by BattleHistoryScene'));
    },
    startBattle: () => {
      otherCapabilityCalls.push('startBattle');
      return Promise.reject(new Error('not used by BattleHistoryScene'));
    },
    clearActiveBattleState: () => {
      otherCapabilityCalls.push('clearActiveBattleState');
    },
    getPets: () => {
      otherCapabilityCalls.push('getPets');
      return Promise.resolve([]);
    },
    getPet: () => {
      otherCapabilityCalls.push('getPet');
      return Promise.resolve({} as never);
    },
    getCards: () => {
      otherCapabilityCalls.push('getCards');
      return Promise.resolve([]);
    },
    getRelics: () => {
      otherCapabilityCalls.push('getRelics');
      return Promise.resolve([]);
    },
    getBattleResult: () => {
      otherCapabilityCalls.push('getBattleResult');
      return Promise.reject(new Error('not used by BattleHistoryScene'));
    },
    getBattleHistory: () => {
      loadAttempts += 1;
      historyReads.push('getBattleHistory');
      return loadGate.then(() => {
        if (shouldFail()) {
          throw historyFailure;
        }
        return history;
      });
    },
  };

  /** A text object with a registered pointer handler. */
  const makeText = (x: number, y: number, label: string): Clickable => {
    const handlers: Array<() => void> = [];
    let current = label;
    let interactive = false;
    let destroyed = false;

    texts.push(label);

    const obj = {
      x,
      y,
      get text() {
        return current;
      },
      get interactive() {
        return interactive;
      },
      setText: (next: string) => {
        if (destroyed) {
          destroyedWriteAttempts.push(next);
          if (textsThrowWhenDestroyed) {
            throw new TypeError('Cannot set property text of #<Text> which has only a getter');
          }
          return obj;
        }
        current = next;
        texts.push(next);
        return obj;
      },
      setColor: () => obj,
      setOrigin: () => obj,
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
        liveTexts.delete(obj);
      },
      click: () => {
        for (const handler of [...handlers]) {
          handler();
        }
      },
    };

    liveTexts.add(obj);
    clickables.push(obj as unknown as Clickable);
    return obj as unknown as Clickable;
  };

  const makeRectangle = (x = 0, y = 0, width = 0, height = 0): Clickable => {
    const handlers: Array<() => void> = [];
    let interactive = false;

    const obj = {
      x,
      y,
      width,
      height,
      label: null as { readonly text: string } | null,
      get text() {
        return obj.label === null ? '' : obj.label.text;
      },
      get interactive() {
        return interactive;
      },
      setStrokeStyle: () => obj,
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
        handlers.length = 0;
        liveRectangles.delete(obj);
      },
      click: () => {
        for (const handler of [...handlers]) {
          handler();
        }
      },
    };

    liveRectangles.add(obj);
    clickables.push(obj as unknown as Clickable);
    return obj as unknown as Clickable;
  };

  /** The rectangle awaiting a caption, so `makeText` can pair the two. */
  let uncaptionedRectangle: { label: { readonly text: string } | null } | null = null;

  function context(scene: object): object {
    const events = new SceneEventEmitter();

    return Object.assign(Object.create(scene), {
      events,
      scene: { start: (key: string) => sceneStarted.push({ key }) },
      add: {
        rectangle: (x: number, y: number, width: number, height: number) => {
          const rect = makeRectangle(x, y, width, height);
          uncaptionedRectangle = rect as unknown as { label: { readonly text: string } | null };
          return rect;
        },
        text: (x: number, y: number, value: string) => {
          const text = makeText(x, y, value);
          if (uncaptionedRectangle !== null) {
            uncaptionedRectangle.label = text as unknown as { readonly text: string };
            uncaptionedRectangle = null;
          }
          return text;
        },
      },
      registry: {
        get: (key: string) => (withRuntime && key === RUNTIME_REGISTRY_KEY ? runtime : undefined),
      },
      sys: { settings: { key: 'BattleHistoryScene' } },
    });
  }

  const isLive = (object: object): boolean =>
    liveTexts.has(object as { readonly text: string }) ||
    liveRectangles.has(object as { readonly text: string });

  return {
    runtime,
    texts,
    clickables,
    historyReads,
    otherCapabilityCalls,
    sceneStarted,
    destroyedWriteAttempts,
    context,
    releaseLoad,
    /** The live render pass's text, as the player would read it. */
    rendered: () => [...liveTexts].map((t) => t.text).join('\n'),
    /** Clicks the live interactive object whose caption is `label` exactly. */
    clickLabel: (label: string): void => {
      const found = clickables.filter((c) => c.interactive && c.text === label && isLive(c));
      if (found.length === 0) {
        throw new Error(`No interactive object is labelled "${label}".`);
      }
      found[found.length - 1].click();
    },
    /** The interactive objects that are still live, i.e. clickable right now. */
    liveInteractive: (): Clickable[] => clickables.filter((c) => c.interactive && isLive(c)),
    /**
     * Takes a scene down exactly as Phaser does (TASK-205):
     * `SceneManager` → `Systems#shutdown` → `scene.events.emit(SHUTDOWN)`, with
     * the engine's own `DisplayList` having destroyed every child first.
     */
    shutdownScene: (ctx: object) => {
      for (const object of [...liveTexts, ...liveRectangles]) {
        (object as { destroy?: () => void }).destroy?.();
      }
      (ctx as { events?: SceneEventEmitter }).events?.emit('shutdown');
    },
    /** `Phaser.Scenes.Systems#destroy` → `Phaser.Scenes.Events.DESTROY`. */
    destroyScene: (ctx: object) => {
      (ctx as { events?: SceneEventEmitter }).events?.emit('destroy');
    },
    /** How many listeners the scene's own emitter holds for one lifecycle event. */
    sceneListenerCount: (ctx: object, event: string) =>
      (ctx as { events?: SceneEventEmitter }).events?.listenerCount(event) ?? 0,
  };
}

/** Invokes a scene method with the harness context, as Phaser itself would. */
function runScene(scene: object, ctx: object, method: string): void {
  const fn = Object.getPrototypeOf(scene)[method] as (this: object) => void;
  fn.call(ctx);
}

async function flush(): Promise<void> {
  for (let i = 0; i < 6; i++) {
    await Promise.resolve();
  }
}

/** Runs `create()` and lets the history read settle. */
async function createHistoryScene(options: SceneHarnessOptions = {}) {
  const harness = createHistoryHarness(options);
  const scene = new BattleHistoryScene();
  const ctx = harness.context(scene);
  runScene(scene, ctx, 'create');
  await flush();
  return { harness, scene, ctx };
}

describe('BattleHistoryScene — the read-only history surface', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('exposes the documented scene class and create()s without throwing', async () => {
    const { harness, scene } = await createHistoryScene();

    expect(scene).toBeInstanceOf(BattleHistoryScene);
    const rendered = harness.rendered();
    expect(rendered).toContain('BATTLE HISTORY');
    expect(rendered).toContain('< BACK');
    expect(rendered).toContain('Player Level / XP');
  });

  it('reads the history through the runtime port, exactly once', async () => {
    const { harness } = await createHistoryScene();

    expect(harness.historyReads).toEqual(['getBattleHistory']);
  });

  it('reaches no runtime capability other than the history read', async () => {
    const { harness } = await createHistoryScene();

    // The scene opens no battle, subscribes to no stream, requests no action,
    // reads no other route, and clears no battle state
    // (ARCHITECTURE.md §2.2.1 rule 1, §2.2.3 rule 3).
    expect(harness.otherCapabilityCalls).toEqual([]);
  });

  it('renders every delivered element member verbatim', async () => {
    const { harness } = await createHistoryScene();

    const rendered = harness.rendered();
    // battleId
    expect(rendered).toContain('battle-zzz');
    expect(rendered).toContain('battle-aaa');
    // outcome
    expect(rendered).toContain('Outcome: victory');
    expect(rendered).toContain('Outcome: defeat');
    // durationTurns
    expect(rendered).toContain('Duration: 12 turns');
    expect(rendered).toContain('Duration: 3 turns');
    // completedAt
    expect(rendered).toContain('Completed At: 2026-10-05T09:15:00Z');
    expect(rendered).toContain('Completed At: 2026-10-04T21:00:00Z');
  });

  it('renders the delivered rewards through the shared RewardSummary representation', async () => {
    const { harness } = await createHistoryScene();

    const rendered = harness.rendered();
    // The same rendering ResultScene uses for the same eight members
    // (DATABASE.md §1): one Player track and one Pet track per battle.
    expect(rendered).toContain('REWARDS');
    expect(rendered.match(/Player: \+100 XP/g)?.length).toBeGreaterThanOrEqual(1);
    expect(rendered).toContain('Level 5');
    expect(rendered).toContain('XP 400');
    expect(rendered).toMatch(/LEVEL UP/);
    // The defeat's zero grants are printed as 0, not omitted.
    expect(rendered).toContain('Player: +0 XP');
    expect(rendered).toContain('Pet: +0 XP');
  });

  it('preserves the delivered order and re-sorts nothing', async () => {
    const { harness } = await createHistoryScene();

    const rendered = harness.rendered();
    // The array's own order (newest first, API_CONTRACTS.md §4.5 note 4) is the
    // rendered order: the ids are deliberately not in id or timestamp order, so
    // a client-side sort would fail here.
    expect(rendered.indexOf('battle-zzz')).toBeLessThan(rendered.indexOf('battle-aaa'));
    expect(rendered).toContain('Battle #1');
    expect(rendered).toContain('Battle #2');
    expect(rendered.indexOf('Battle #1')).toBeLessThan(rendered.indexOf('Battle #2'));
  });

  it('presents the newest delivered element’s own Player Level / XP', async () => {
    const { harness } = await createHistoryScene();

    const rendered = harness.rendered();
    // The newest element (index 0) carries Player Level 5 / XP 400 / leveled up.
    expect(rendered).toContain('Level 5');
    expect(rendered).toContain('XP 400');
    // And not the older element's values as the account display, and never a
    // fabricated starting Level.
    expect(rendered).toContain('Level 4');
    expect(rendered).not.toMatch(/Level 1(?!\d)/);
  });

  it('renders a null progression member as unavailable, never as 0', async () => {
    const { harness } = await createHistoryScene({
      history: [
        historyItem({
          rewards: rewards({
            newPlayerXp: null,
            newPlayerLevel: null,
            playerLeveledUp: null,
          }),
        }),
      ],
    });

    const rendered = harness.rendered();
    // DATABASE.md §1 item 4: `null` is "this track had no row", not a zero.
    expect(rendered).toContain('—');
    expect(rendered).not.toContain('Level 0');
    expect(rendered).not.toContain('XP 0 ');
  });

  it('prints the delivered leveledUp flag without re-deriving it', async () => {
    // Contrary on purpose: the Level is unchanged numerically, but the server
    // reports the level-up. The scene prints what was delivered.
    const { harness } = await createHistoryScene({
      history: [
        historyItem({
          rewards: rewards({ newPlayerLevel: 4, newPlayerXp: 300, playerLeveledUp: true }),
        }),
      ],
    });

    expect(harness.rendered()).toMatch(/LEVEL UP/);
  });
});

describe('BattleHistoryScene — empty history (API_CONTRACTS.md §4.5 note 9)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows an explicit empty state for 200 []', async () => {
    const { harness } = await createHistoryScene({ history: [] });

    const rendered = harness.rendered();
    expect(rendered).toContain('No battles yet');
    expect(rendered).not.toContain('Battle #1');
    expect(rendered).not.toContain('REWARDS');
  });

  it('fabricates no Player Level or XP for an account with no battles', async () => {
    const { harness, ctx } = await createHistoryScene({ history: [] });

    const rendered = harness.rendered();
    // No `Level 1` / `0 XP` is invented: §4.5 note 9 makes the empty history a
    // `200 []`, not a starting progression.
    expect(rendered).not.toMatch(/Level \d/);
    expect(rendered).not.toMatch(/XP \d/);
    expect((ctx as { history: unknown[] }).history).toEqual([]);
  });

  it('is not an error state: no RETRY is offered and the BACK navigation remains', async () => {
    const { harness } = await createHistoryScene({ history: [] });

    expect(harness.liveInteractive().map((c) => c.text)).toEqual(['< BACK']);
  });
});

describe('BattleHistoryScene — error state and RETRY', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('shows an error state with RETRY and BACK when the read fails', async () => {
    const { harness } = await createHistoryScene({
      historyFailure: new Error('Request to /api/battle/history failed with status 401'),
    });

    const rendered = harness.rendered();
    expect(rendered).toContain('Battle history load failed');
    expect(rendered).toContain('401');
    expect(harness.liveInteractive().map((c) => c.text).sort()).toEqual(['< BACK', 'RETRY']);
  });

  it('renders no partial history list after a failed request', async () => {
    const { harness, ctx } = await createHistoryScene({
      historyFailure: new Error('network error'),
    });

    expect(harness.rendered()).not.toContain('Battle #1');
    expect(harness.rendered()).not.toContain('REWARDS');
    expect((ctx as { history: unknown[] }).history).toEqual([]);
  });

  it('re-reads on RETRY and renders the delivered history when it succeeds', async () => {
    const { harness } = await createHistoryScene({
      historyFailure: new Error('Request to /api/battle/history failed with status 500'),
      failFirstLoadOnly: true,
    });

    expect(harness.historyReads).toEqual(['getBattleHistory']);

    harness.clickLabel('RETRY');
    await flush();

    // A fresh request, not a replay of the failed one.
    expect(harness.historyReads).toEqual(['getBattleHistory', 'getBattleHistory']);
    const rendered = harness.rendered();
    expect(rendered).toContain('battle-zzz');
    expect(rendered).toContain('Battle #1');
    expect(rendered).not.toContain('Battle history load failed');
  });

  it('does not accumulate retry handlers across repeated failures and retries', async () => {
    const { harness } = await createHistoryScene({
      historyFailure: new Error('Request to /api/battle/history failed with status 503'),
    });

    harness.clickLabel('RETRY');
    await flush();
    harness.clickLabel('RETRY');
    await flush();

    // Each retry re-renders, which destroys the previous RETRY hit area and
    // detaches its listener, so exactly one live RETRY exists.
    expect(harness.liveInteractive().filter((c) => c.text === 'RETRY')).toHaveLength(1);
    expect(harness.historyReads.length).toBe(3);
  });

  it('treats a missing runtime as an error state that BACK can still leave', async () => {
    const { harness } = await createHistoryScene({ withRuntime: false });

    expect(harness.rendered()).toContain('No runtime is available');
    expect(harness.historyReads).toEqual([]);

    harness.clickLabel('< BACK');
    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });
});

describe('BattleHistoryScene — navigation', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('returns to MainMenuScene from < BACK', async () => {
    const { harness } = await createHistoryScene();

    harness.clickLabel('< BACK');

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('fires at most once per scene instance', async () => {
    const { harness } = await createHistoryScene();

    harness.clickLabel('< BACK');
    harness.clickLabel('< BACK');

    expect(harness.sceneStarted.map((s) => s.key)).toEqual(['MainMenuScene']);
  });

  it('starts no other scene', async () => {
    const { harness } = await createHistoryScene();
    await flush();

    expect(harness.sceneStarted).toEqual([]);
  });
});

describe('BattleHistoryScene — Phaser lifecycle (TASK-205 pattern)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('attaches its teardown to the engine lifecycle events, not to a method name', async () => {
    const { harness, ctx } = await createHistoryScene();

    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('releases its presentation and loaded history on SHUTDOWN', async () => {
    const { harness, ctx } = await createHistoryScene();

    harness.shutdownScene(ctx);

    expect((ctx as { history: unknown[] }).history).toEqual([]);
    expect((ctx as { shellObjects: unknown[] }).shellObjects).toEqual([]);
    expect((ctx as { interactiveObjects: unknown[] }).interactiveObjects).toEqual([]);
    expect((ctx as { renderedTexts: unknown[] }).renderedTexts).toEqual([]);
    expect((ctx as { progressionText: unknown }).progressionText).toBeNull();
    expect((ctx as { statusText: unknown }).statusText).toBeNull();
    expect((ctx as { hasTransitioned: boolean }).hasTransitioned).toBe(false);
  });

  it('releases its presentation and loaded history on DESTROY', async () => {
    const { harness, ctx } = await createHistoryScene();

    harness.destroyScene(ctx);

    expect((ctx as { history: unknown[] }).history).toEqual([]);
    expect((ctx as { shellObjects: unknown[] }).shellObjects).toEqual([]);
    expect((ctx as { renderedTexts: unknown[] }).renderedTexts).toEqual([]);
  });

  it('is idempotent: both events, and neither after a create(), are safe', async () => {
    const harness = createHistoryHarness();
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene);

    // No create() ran: nothing is registered and nothing is owned.
    expect(() => harness.shutdownScene(ctx)).not.toThrow();
    expect(() => harness.destroyScene(ctx)).not.toThrow();

    runScene(scene, ctx, 'create');
    await flush();

    expect(() => harness.shutdownScene(ctx)).not.toThrow();
    expect(() => harness.destroyScene(ctx)).not.toThrow();
    expect((ctx as { history: unknown[] }).history).toEqual([]);
  });

  it('does not accumulate lifecycle listeners when the scene is reused', async () => {
    const harness = createHistoryHarness();
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene);

    runScene(scene, ctx, 'create');
    await flush();
    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');
    await flush();
    harness.shutdownScene(ctx);
    runScene(scene, ctx, 'create');
    await flush();

    // Three runs, one teardown per event: `once` alone would leave every
    // earlier run's unfired DESTROY handler on the emitter.
    expect(harness.sceneListenerCount(ctx, 'shutdown')).toBe(1);
    expect(harness.sceneListenerCount(ctx, 'destroy')).toBe(1);
  });

  it('does not accumulate display objects when the scene is reused', async () => {
    const harness = createHistoryHarness();
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene);

    runScene(scene, ctx, 'create');
    await flush();
    const firstRunRendered = harness.rendered();
    harness.shutdownScene(ctx);

    runScene(scene, ctx, 'create');
    await flush();
    const secondRunRendered = harness.rendered();

    // One shell and one entry block per battle — identical between runs, not
    // doubled by the reuse.
    expect(secondRunRendered).toBe(firstRunRendered);
  });
});

describe('BattleHistoryScene — asynchronous read safety (TASK-205 pattern)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** A deferred `getBattleHistory` the test resolves by hand. */
  function deferredHistory(): {
    readonly promise: Promise<BattleHistoryItemResponse[]>;
    readonly release: (value: BattleHistoryItemResponse[]) => void;
    readonly reject: (error: Error) => void;
  } {
    let release: (value: BattleHistoryItemResponse[]) => void = () => {};
    let reject: (error: Error) => void = () => {};
    const promise = new Promise<BattleHistoryItemResponse[]>((resolve, fail) => {
      release = resolve;
      reject = fail;
    });
    return { promise, release, reject };
  }

  it('does not write to the presentation when the read settles after SHUTDOWN', async () => {
    const harness = createHistoryHarness({ textsThrowWhenDestroyed: false });
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene);

    const deferred = deferredHistory();
    harness.runtime.getBattleHistory = vi.fn(() => deferred.promise) as never;

    runScene(scene, ctx, 'create');
    expect(harness.runtime.getBattleHistory).toHaveBeenCalledTimes(1);

    // The player leaves: Phaser's DisplayList destroys the scene's Text objects
    // and the scene's own teardown runs on the same event.
    harness.shutdownScene(ctx);

    deferred.release(DELIVERED_HISTORY);
    await flush();

    // The delivered history was dropped, not written: no history was loaded and
    // no write was attempted against a destroyed object.
    expect((ctx as { history: unknown[] }).history).toEqual([]);
    expect(harness.destroyedWriteAttempts).toEqual([]);
    expect(harness.rendered()).not.toContain('battle-zzz');
  });

  it('does not write the ended run’s history into a later run’s presentation', async () => {
    // Scene reuse is the other half: the instance is restarted and the previous
    // run's read settles afterwards, when the scene's references point at the
    // NEW presentation. Only the run identity stops the stale delivery.
    const harness = createHistoryHarness({ textsThrowWhenDestroyed: false });
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene);

    const firstRun = deferredHistory();
    harness.runtime.getBattleHistory = vi.fn(() => firstRun.promise) as never;

    runScene(scene, ctx, 'create');
    harness.shutdownScene(ctx);

    // A second run whose own read is still pending.
    const secondRun = deferredHistory();
    harness.runtime.getBattleHistory = vi.fn(() => secondRun.promise) as never;
    runScene(scene, ctx, 'create');

    // Run A settles after run B started.
    firstRun.release([historyItem({ battleId: 'battle-from-run-a' })]);
    await flush();

    expect(harness.rendered()).not.toContain('battle-from-run-a');
    expect(harness.destroyedWriteAttempts).toEqual([]);

    // Run B's own delivery still lands.
    secondRun.release([historyItem({ battleId: 'battle-from-run-b' })]);
    await flush();

    expect(harness.rendered()).toContain('battle-from-run-b');
    expect(harness.rendered()).not.toContain('battle-from-run-a');
  });

  it('does not paint a stale failure over a later run', async () => {
    const harness = createHistoryHarness({ textsThrowWhenDestroyed: false });
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene);

    const firstRun = deferredHistory();
    harness.runtime.getBattleHistory = vi.fn(() => firstRun.promise) as never;
    runScene(scene, ctx, 'create');
    harness.shutdownScene(ctx);

    const secondRun = deferredHistory();
    harness.runtime.getBattleHistory = vi.fn(() => secondRun.promise) as never;
    runScene(scene, ctx, 'create');

    firstRun.reject(new Error('Request to /api/battle/history failed with status 401'));
    await flush();

    // The ended run's failure did not become this presentation's error state.
    expect((ctx as { loadError: string | null }).loadError).toBeNull();
    expect(harness.rendered()).not.toContain('Battle history load failed');

    secondRun.release([historyItem({ battleId: 'battle-from-run-b' })]);
    await flush();
    expect(harness.rendered()).toContain('battle-from-run-b');
  });

  it('shows the loading state while the read is pending and clears it on delivery', async () => {
    const harness = createHistoryHarness({ deferLoad: true });
    const scene = new BattleHistoryScene();
    const ctx = harness.context(scene);

    runScene(scene, ctx, 'create');
    await flush();

    expect((ctx as { loading: boolean }).loading).toBe(true);
    expect(harness.rendered()).toContain('Loading battle history…');
    expect(harness.rendered()).not.toContain('battle-zzz');

    harness.releaseLoad();
    await flush();

    expect((ctx as { loading: boolean }).loading).toBe(false);
    expect(harness.rendered()).not.toContain('Loading battle history…');
    expect(harness.rendered()).toContain('battle-zzz');
  });
});

describe('BattleHistoryScene — boundary, authority, and no global state', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  const source = (): string =>
    readFileSync(resolve(__dirname, '../src/game/scenes/BattleHistoryScene.ts'), 'utf8').replace(
      /\/\*[\s\S]*?\*\//g,
      ''
    );

  it('imports no transport client and no HTTP client', () => {
    const code = source();

    // ARCHITECTURE.md §2.2.1 rule 1 / §2.2.3 rule 3: the scene depends on the
    // runtime port, never on the transport.
    expect(code).not.toMatch(/@microsoft\/signalr/);
    expect(code).not.toMatch(/HubConnection/);
    expect(code).not.toMatch(/services\/realtime/);
    expect(code).not.toMatch(/ApiService/);
    expect(code).not.toMatch(/\bfetch\s*\(/);

    // The only `services/api/` import is the type-only wire model.
    const apiImports = [...code.matchAll(/from '[^']*services\/api\/([^']+)'/g)].map(
      (match) => match[1]
    );
    expect(apiImports).toEqual(['BattleModels']);
    expect(code).toMatch(/import type \{[^}]*\} from '\.\.\/\.\.\/services\/api\/BattleModels'/);
  });

  it('computes no progression, ordering, or duration, and holds no global state', () => {
    const code = source();

    // AGENTS.md §10, ADR-001, API_CONTRACTS.md §4.5: every presented value is
    // the delivered one.
    expect(code).not.toMatch(/\.sort\(|\.reverse\(|\.filter\(/);
    expect(code).not.toMatch(/Date\.parse|new Date\(|toLocaleString|toISOString/);
    expect(code).not.toMatch(/levelForXp|xpForLevel|xpToLevel|Math\.floor\([^)]*XP/);

    // ARCHITECTURE.md §2.2.3 rules 1–2, §5 item 5, ADR-022: ephemeral,
    // scene-local presentation state — no store, singleton, cache, or browser
    // storage.
    expect(code).not.toMatch(/localStorage|sessionStorage|indexedDB/);
    expect(code).not.toMatch(/\bwindow\.|\bdocument\./);
  });

  it('registers its teardown against the engine lifecycle events in the source', () => {
    const code = source();

    expect(code).toMatch(/events\.off\(\s*Phaser\.Scenes\.Events\.SHUTDOWN/);
    expect(code).toMatch(/events\.off\(\s*Phaser\.Scenes\.Events\.DESTROY/);
    expect(code).toMatch(/events\.once\(\s*Phaser\.Scenes\.Events\.SHUTDOWN/);
    expect(code).toMatch(/events\.once\(\s*Phaser\.Scenes\.Events\.DESTROY/);
    expect(code).toMatch(/shutdown\(\): void \{/);
  });

  it('owns the asynchronous read with a scene-local run guard, not a global one', () => {
    const code = source();

    expect(code).toMatch(/historyLoadRun/);
    expect(code).toMatch(/run !== this\.historyLoadRun/);
    // No cancellation manager, registry, or module-level promise store.
    expect(code).not.toMatch(/AbortController|abortController|cancelToken/);
    expect(code).not.toMatch(/^let /m);
  });
});
