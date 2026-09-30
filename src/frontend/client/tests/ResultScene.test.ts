import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { ResultScene } from '../src/game/scenes/ResultScene';
import type { ResultSceneData } from '../src/game/scenes/ResultScene';
import { BattleScene } from '../src/game/scenes/BattleScene';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
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
  Input: { Events: { GAMEOBJECT_POINTER_DOWN: 'gameobjectdown' } },
}));

interface SceneHarnessOptions {
  state?: GameRuntimeState;
  withRuntime?: boolean;
  battleState?: RuntimeBattleState | null;
}

function createSceneHarness(options: SceneHarnessOptions = {}) {
  const { state = INITIAL_RUNTIME_STATE, withRuntime = true, battleState = null } = options;
  const runtimeListeners = new Set<(event: { state: GameRuntimeState }) => void>();
  const battleStateListeners = new Set<(state: RuntimeBattleState) => void>();
  const battleEventListeners = new Set<(envelope: BattleEventsEnvelope) => void>();
  const texts: Array<{ text: string; color?: string }> = [];
  const sceneStarted: Array<{ key: string; data?: unknown }> = [];

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
    setEngineStatus: vi.fn(),
  };

  function context(scene: object, sceneKey: string): object {
    const makeText = (value: string) => {
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
      return obj;
    };

    const makeContainer = () => {
      const children: unknown[] = [];
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
        on: () => obj,
        off: () => {},
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
      registry: {
        get: (key: string) => (withRuntime && key === RUNTIME_REGISTRY_KEY ? runtime : undefined),
      },
      sys: { settings: { key: sceneKey } },
    });
  }

  return {
    runtime,
    texts,
    runtimeListeners,
    battleStateListeners,
    battleEventListeners,
    emitBattleEvents: (envelope: BattleEventsEnvelope) => {
      for (const listener of battleEventListeners) {
        listener(envelope);
      }
    },
    sceneStarted,
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
    expect(rendered).toContain('Player HP: 812');
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
    expect(rendered).toContain('Player HP: 0');
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
    expect(rendered).toContain('Player HP: 100');
  });

  it('renders fallback when no result data is provided', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create');

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('NO RESULT');
  });

  it('cleans up references on shutdown', () => {
    const harness = createSceneHarness();
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', {
      outcome: 'victory',
      finalBossHp: 0,
      finalPlayerHp: 50,
    });

    expect(() => runScene(scene, ctx, 'shutdown')).not.toThrow();
  });

  it('contains no client-side outcome derivation or reward logic', () => {
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/ResultScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    for (const forbidden of [
      'finalBossHp <= 0',
      'finalBossHp === 0',
      'finalPlayerHp <= 0',
      'finalPlayerHp === 0',
      'RewardSummary',
      '/api/battle/',
      'getBattleResult',
      'ApiService',
    ]) {
      expect(source, `ResultScene must not contain "${forbidden}"`).not.toContain(forbidden);
    }
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

  it('unsubscribes from onBattleEvents on shutdown()', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.battleEventListeners.size).toBe(1);

    runScene(scene, ctx, 'shutdown');
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

  it('does NOT transition if BattleScene shuts down before outcome arrives', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    runScene(scene, ctx, 'shutdown');

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
