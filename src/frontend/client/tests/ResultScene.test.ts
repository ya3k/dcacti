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
  const texts: Array<{ text: string; color?: string }> = [];
  const sceneStarted: Array<{ key: string; data?: unknown }> = [];
  const battleResultRequests: string[] = [];

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
    battleResultRequests,
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

  it('leaves the outcome presentation intact when the result read is rejected (API_CONTRACTS.md §4 notes 6–7)', async () => {
    const harness = await renderWithResult(
      outcomeData,
      new Error('BATTLE_NOT_FOUND')
    );

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toMatch(/VICTORY/i);
    expect(rendered).toContain('Boss HP: 0');
    expect(rendered).toContain('Player HP: 320');
    // No reward value is fabricated for an unavailable result.
    expect(rendered).not.toContain('REWARDS');
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

  it('cleans up reward presentation references on shutdown', () => {
    const harness = createSceneHarness({ battleResult: deliveredResult(deliveredRewards()) });
    const scene = new ResultScene();
    const ctx = harness.context(scene, 'ResultScene');

    runScene(scene, ctx, 'create', outcomeData);

    expect(() => runScene(scene, ctx, 'shutdown')).not.toThrow();
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
