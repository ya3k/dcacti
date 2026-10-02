import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { BattleScene } from '../src/game/scenes/BattleScene';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { GameRuntimeState } from '../src/state/GameRuntimeState';
import type { BattleEventsEnvelope, RuntimeBattleState } from '../src/game/runtime/GameRuntimeEvents';
import {
  parseInBattleEvent,
  formatInBattleEvent,
} from '../src/game/scenes/BattleEventPresenter';
import type {
  PresentedMatchCreated,
  PresentedCascadeCreated,
  PresentedComboChanged,
  PresentedGemMatched,
  PresentedDamageCalculated,
  PresentedDamageDealt,
  PresentedDamageTaken,
  PresentedPassiveCharged,
  PresentedPassiveTriggered,
  PresentedBossSkillCast,
} from '../src/game/scenes/BattleEventPresenter';

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
  const boardInputHandlers = new Map<string, (pointer: { x: number; y: number }) => void>();
  const requestedActions: Array<Record<string, unknown>> = [];
  const sceneStarted: Array<{ key: string; data?: unknown }> = [];
  const createdRectangles: Array<{ kind: string; fill?: number }> = [];

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
    requestAction: (action: Record<string, unknown>) => {
      requestedActions.push(action);
      return Promise.resolve({ accepted: true });
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
        destroy: () => {},
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
        on: (event: string, handler: (pointer: { x: number; y: number }) => void) => {
          boardInputHandlers.set(event, handler);
          return obj;
        },
        off: (event: string) => {
          boardInputHandlers.delete(event);
        },
      };
      return obj;
    };

    return Object.assign(Object.create(scene), {
      scene: {
        start: (key: string, data?: unknown) => sceneStarted.push({ key, data }),
      },
      add: {
        rectangle: (_x: number, _y: number, _w: number, _h: number, fill?: number) => {
          const rect = {
            kind: 'tile',
            fill,
            setStrokeStyle: () => rect,
            destroy: () => {},
          };
          createdRectangles.push(rect);
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
    createdRectangles,
    boardInputHandlers,
    requestedActions,
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

const CELL_SIZE = 56;
const CELL_GAP = 6;
const BOARD_PITCH = CELL_SIZE + CELL_GAP;
const BOARD_COLUMNS = 8;
const BOARD_WIDTH = BOARD_COLUMNS * CELL_SIZE + (BOARD_COLUMNS - 1) * CELL_GAP;
const BOARD_ORIGIN_X = (1280 - BOARD_WIDTH) / 2;
const BOARD_ORIGIN_Y = 24 + 96;

function cellCentre(index: number): { x: number; y: number } {
  const row = Math.floor(index / BOARD_COLUMNS);
  const column = index % BOARD_COLUMNS;
  return {
    x: BOARD_ORIGIN_X + column * BOARD_PITCH + CELL_SIZE / 2,
    y: BOARD_ORIGIN_Y + row * BOARD_PITCH + CELL_SIZE / 2,
  };
}

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

async function flush(): Promise<void> {
  await Promise.resolve();
  await Promise.resolve();
}

describe('TASK-088 — BattleEventPresenter parser & formatter', () => {
  it('parses and formats MatchCreated with verbatim members', () => {
    const raw: PresentedMatchCreated = {
      type: 'MatchCreated',
      shape: 'Lt',
      cells: [7, 15, 23, 24],
      gemType: 'POWER',
      cascadeDepth: 4,
      createdSpecialGems: [{ cellIndex: 23, specialGem: { type: 'Burst' } }],
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);

    const formatted = formatInBattleEvent(parsed!);
    expect(formatted).toContain('Lt');
    expect(formatted).toContain('POWER');
    expect(formatted).toContain('7, 15, 23, 24');
    expect(formatted).toContain('depth: 4');
    expect(formatted).toContain('Burst@23');
  });

  it('parses and formats CascadeCreated with verbatim cascadeDepth', () => {
    const raw: PresentedCascadeCreated = {
      type: 'CascadeCreated',
      cascadeDepth: 7,
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('Cascade: depth 7');
  });

  it('parses and formats ComboChanged with verbatim combo', () => {
    const raw: PresentedComboChanged = {
      type: 'ComboChanged',
      combo: 19,
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('Combo: 19');
  });

  it('parses and formats GemMatched without specialGem', () => {
    const raw: PresentedGemMatched = {
      type: 'GemMatched',
      cellIndex: 42,
      gemType: 'DEF',
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('GemMatched: cell 42 (DEF)');
  });

  it('parses and formats GemMatched with LineClear specialGem and orientation', () => {
    const raw: PresentedGemMatched = {
      type: 'GemMatched',
      cellIndex: 19,
      gemType: 'ATK',
      specialGem: {
        type: 'LineClear',
        orientation: 'Vertical',
      },
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('GemMatched: cell 19 (ATK) [Special: LineClear Vertical]');
  });

  it('parses and formats DamageCalculated with all 6 pipeline members verbatim', () => {
    const raw: PresentedDamageCalculated = {
      type: 'DamageCalculated',
      base: 137,
      comboModifier: 2.5,
      elementModifier: 1.25,
      otherModifiers: 1.1,
      defense: 43.21,
      finalDamage: 99,
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);

    const formatted = formatInBattleEvent(parsed!);
    expect(formatted).toContain('base 137');
    expect(formatted).toContain('combo 2.5x');
    expect(formatted).toContain('elem 1.25x');
    expect(formatted).toContain('other 1.1x');
    expect(formatted).toContain('def 43.21');
    expect(formatted).toContain('final 99');
  });

  it('parses and formats DamageDealt with verbatim members', () => {
    const raw: PresentedDamageDealt = {
      type: 'DamageDealt',
      source: 'player',
      target: 'boss',
      amount: 99,
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('DamageDealt: 99 (player -> boss)');
  });

  it('parses and formats DamageTaken with verbatim members', () => {
    const raw: PresentedDamageTaken = {
      type: 'DamageTaken',
      source: 'boss',
      target: 'player',
      amount: 77,
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('DamageTaken: 77 on player (from boss)');
  });

  it('parses and formats PassiveCharged with non-derivable progress and threshold', () => {
    const raw: PresentedPassiveCharged = {
      type: 'PassiveCharged',
      passiveId: 'boss-hoa-long-rage',
      source: 'boss',
      sourceId: 'boss-hoa-long',
      progress: 11,
      threshold: 17,
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe(
      'PassiveCharged: boss-hoa-long-rage (boss:boss-hoa-long) 11/17'
    );
  });

  it('parses and formats PassiveTriggered with verbatim members', () => {
    const raw: PresentedPassiveTriggered = {
      type: 'PassiveTriggered',
      passiveId: 'pet-phoenix-rebirth',
      source: 'pet',
      sourceId: 'pet-inst-99',
      progress: 10,
      threshold: 10,
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe(
      'PassiveTriggered: pet-phoenix-rebirth (pet:pet-inst-99) 10/10'
    );
  });

  it('parses and formats BossSkillCast with verbatim skillId and sourceId', () => {
    const raw: PresentedBossSkillCast = {
      type: 'BossSkillCast',
      skillId: 'flame-burst-mega',
      sourceId: 'boss-hoa-long',
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('BossSkillCast: flame-burst-mega by boss-hoa-long');
  });

  it('parses and formats CardCast with verbatim cardId (SIGNALR_PROTOCOL.md §3.2.20)', () => {
    const raw = {
      type: 'CardCast',
      cardId: 'card-shield',
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('CardCast: card-shield');
  });

  it('parses and formats PetSkillCast with verbatim cardId (SIGNALR_PROTOCOL.md §3.2.21)', () => {
    const raw = {
      type: 'PetSkillCast',
      cardId: 'card-inferno',
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('PetSkillCast: card-inferno');
  });

  it('returns null for unknown event types or malformed events', () => {
    expect(parseInBattleEvent({ type: 'UnknownType', amount: 10 })).toBeNull();
    expect(parseInBattleEvent({ type: 'BattleStarted', battleId: 'b1' })).toBeNull();
    expect(parseInBattleEvent(null)).toBeNull();
    expect(parseInBattleEvent('invalid')).toBeNull();
    expect(parseInBattleEvent({ type: 'MatchCreated', shape: 123 })).toBeNull();
    expect(parseInBattleEvent({ type: 'DamageDealt', amount: 'notANumber' })).toBeNull();
    expect(parseInBattleEvent({ type: 'CardCast' })).toBeNull();
    expect(parseInBattleEvent({ type: 'CardCast', cardId: '' })).toBeNull();
    expect(parseInBattleEvent({ type: 'CardCast', cardId: 123 })).toBeNull();
    expect(parseInBattleEvent({ type: 'PetSkillCast' })).toBeNull();
    expect(parseInBattleEvent({ type: 'PetSkillCast', cardId: '' })).toBeNull();
    expect(parseInBattleEvent({ type: 'PetSkillCast', cardId: 123 })).toBeNull();
  });
});

describe('TASK-088 — BattleScene In-Battle Event Presentation (SIGNALR_PROTOCOL.md §3.2.6–§3.2.18)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  function createBattle() {
    const harness = createSceneHarness();
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  it.each([
    {
      name: 'MatchCreated',
      event: {
        type: 'MatchCreated',
        shape: 'Lt',
        cells: [7, 15, 23, 24],
        gemType: 'POWER',
        cascadeDepth: 4,
        createdSpecialGems: [{ cellIndex: 23, specialGem: { type: 'Burst' } }],
      },
      expectedTokens: ['Lt', 'POWER', '7, 15, 23, 24', 'depth: 4', 'Burst@23'],
    },
    {
      name: 'GemMatched without specialGem',
      event: {
        type: 'GemMatched',
        cellIndex: 42,
        gemType: 'DEF',
      },
      expectedTokens: ['cell 42', 'DEF'],
    },
    {
      name: 'GemMatched with specialGem',
      event: {
        type: 'GemMatched',
        cellIndex: 19,
        gemType: 'ATK',
        specialGem: { type: 'LineClear', orientation: 'Vertical' },
      },
      expectedTokens: ['cell 19', 'ATK', 'LineClear', 'Vertical'],
    },
    {
      name: 'CascadeCreated',
      event: {
        type: 'CascadeCreated',
        cascadeDepth: 7,
      },
      expectedTokens: ['depth 7'],
    },
    {
      name: 'ComboChanged',
      event: {
        type: 'ComboChanged',
        combo: 19,
      },
      expectedTokens: ['Combo: 19'],
    },
    {
      name: 'DamageCalculated',
      event: {
        type: 'DamageCalculated',
        base: 137,
        comboModifier: 2.5,
        elementModifier: 1.25,
        otherModifiers: 1.1,
        defense: 43.21,
        finalDamage: 99,
      },
      expectedTokens: ['137', '2.5', '1.25', '1.1', '43.21', '99'],
    },
    {
      name: 'DamageDealt',
      event: {
        type: 'DamageDealt',
        source: 'player',
        target: 'boss',
        amount: 99,
      },
      expectedTokens: ['DamageDealt: 99', 'player -> boss'],
    },
    {
      name: 'DamageTaken',
      event: {
        type: 'DamageTaken',
        source: 'boss',
        target: 'player',
        amount: 77,
      },
      expectedTokens: ['DamageTaken: 77', 'boss', 'player'],
    },
    {
      name: 'PassiveCharged',
      event: {
        type: 'PassiveCharged',
        passiveId: 'boss-hoa-long-rage',
        source: 'boss',
        sourceId: 'boss-hoa-long',
        progress: 11,
        threshold: 17,
      },
      expectedTokens: ['boss-hoa-long-rage', 'boss:boss-hoa-long', '11/17'],
    },
    {
      name: 'PassiveTriggered',
      event: {
        type: 'PassiveTriggered',
        passiveId: 'pet-phoenix-rebirth',
        source: 'pet',
        sourceId: 'pet-inst-99',
        progress: 10,
        threshold: 10,
      },
      expectedTokens: ['pet-phoenix-rebirth', 'pet:pet-inst-99', '10/10'],
    },
    {
      name: 'BossSkillCast',
      event: {
        type: 'BossSkillCast',
        skillId: 'flame-burst-mega',
        sourceId: 'boss-hoa-long',
      },
      expectedTokens: ['flame-burst-mega', 'boss-hoa-long'],
    },
    {
      name: 'CardCast',
      event: {
        type: 'CardCast',
        cardId: 'card-shield',
      },
      expectedTokens: ['CardCast', 'card-shield'],
    },
    {
      name: 'PetSkillCast',
      event: {
        type: 'PetSkillCast',
        cardId: 'card-inferno',
      },
      expectedTokens: ['PetSkillCast', 'card-inferno'],
    },
  ])('presents $name verbatim when delivered in ReceiveEvents', ({ event, expectedTokens }) => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-test',
      serverSequence: 3,
      events: [event],
    });

    const rendered = harness.texts.map((t) => t.text).join('\n');
    for (const token of expectedTokens) {
      expect(rendered, `Expected rendered output to contain "${token}"`).toContain(token);
    }
  });

  it('presents both CardCast and PetSkillCast in order for Pet Skill Card cast (SIGNALR_PROTOCOL.md §3.2.22)', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-test',
      serverSequence: 5,
      events: [
        { type: 'CardCast', cardId: 'card-inferno' },
        { type: 'PetSkillCast', cardId: 'card-inferno' },
      ],
    });

    const presented = (scene as unknown as { getPresentedEvents(): readonly string[] }).getPresentedEvents();
    expect(presented).toEqual([
      'CardCast: card-inferno',
      'PetSkillCast: card-inferno',
    ]);
  });

  it('preserves the exact received event order during presentation (GAME_EVENTS.md §1.1)', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    const events = [
      {
        type: 'MatchCreated',
        shape: 'Straight',
        cells: [8, 9, 10],
        gemType: 'ATK',
        cascadeDepth: 1,
      },
      {
        type: 'GemMatched',
        cellIndex: 8,
        gemType: 'ATK',
      },
      {
        type: 'CascadeCreated',
        cascadeDepth: 2,
      },
      {
        type: 'ComboChanged',
        combo: 3,
      },
      {
        type: 'DamageCalculated',
        base: 100,
        comboModifier: 1.5,
        elementModifier: 1.5,
        otherModifiers: 1.0,
        defense: 68.57,
        finalDamage: 68,
      },
    ];

    harness.emitBattleEvents({
      battleId: 'b-seq',
      serverSequence: 4,
      events,
    });

    const rendered = harness.texts.map((t) => t.text).join('\n');

    // Index of each event presentation in the text
    const idxMatch = rendered.indexOf('Match: Straight ATK [8, 9, 10]');
    const idxGem = rendered.indexOf('GemMatched: cell 8 (ATK)');
    const idxCascade = rendered.indexOf('Cascade: depth 2');
    const idxCombo = rendered.indexOf('Combo: 3');
    const idxDamage = rendered.indexOf('DamageCalculated: base 100');

    expect(idxMatch).toBeGreaterThanOrEqual(0);
    expect(idxGem).toBeGreaterThan(idxMatch);
    expect(idxCascade).toBeGreaterThan(idxGem);
    expect(idxCombo).toBeGreaterThan(idxCascade);
    expect(idxDamage).toBeGreaterThan(idxCombo);
  });

  it('safely ignores unknown event types without throwing or fabricating presentation', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    expect(() => {
      harness.emitBattleEvents({
        battleId: 'b-unknown',
        serverSequence: 2,
        events: [
          { type: 'TurnStarted', turn: 1 },
          { type: 'UnknownFabricatedEvent', randomData: 123 },
          { notAnEvent: true },
        ],
      });
    }).not.toThrow();

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('TurnStarted');
    expect(rendered).not.toContain('UnknownFabricatedEvent');
  });

  it('locks player input during presentation sequence and unlocks on completion', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    // Deliver events
    harness.emitBattleEvents({
      battleId: 'b-lock',
      serverSequence: 5,
      events: [
        {
          type: 'DamageDealt',
          source: 'player',
          target: 'boss',
          amount: 50,
        },
      ],
    });

    // After presentation sequence completes, input is unlocked and swaps can be submitted
    tapCell(harness, 10);
    tapCell(harness, 11);
    await flush();

    expect(harness.requestedActions).toHaveLength(1);
    expect(harness.requestedActions[0]).toEqual({
      kind: 'Swap',
      fromCell: 10,
      toCell: 11,
    });
  });

  it('releases input guard on scene shutdown', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-lock',
      serverSequence: 5,
      events: [{ type: 'ComboChanged', combo: 5 }],
    });

    runScene(scene, ctx, 'shutdown');
    // Calling isInputLocked on scene prototype
    expect((scene as BattleScene).isInputLocked()).toBe(false);
  });

  it('releases input guard even if presentation encounters an error', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    // Corrupted event batch with circular reference or unexpected behavior
    const badBatch = {
      battleId: 'b-err',
      serverSequence: 6,
      events: [
        {
          type: 'MatchCreated',
          get shape() {
            throw new Error('Exploding getter in presentation');
          },
        },
      ],
    };

    expect(() => harness.emitBattleEvents(badBatch as unknown as BattleEventsEnvelope)).not.toThrow();

    // Input must still be unlocked
    tapCell(harness, 20);
    tapCell(harness, 21);
    await flush();

    expect(harness.requestedActions).toHaveLength(1);
  });

  it('preserves outcome transition (BattleWon) alongside sibling resolution events', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-final',
      serverSequence: 10,
      events: [
        {
          type: 'DamageTaken',
          source: 'player',
          target: 'boss',
          amount: 150,
        },
        {
          type: 'PassiveTriggered',
          passiveId: 'boss-hoa-long-rage',
          source: 'boss',
          sourceId: 'boss-hoa-long',
          progress: 5,
          threshold: 5,
        },
        {
          type: 'BattleWon',
          outcome: 'victory',
          finalBossHp: 0,
          finalPlayerHp: 100,
        },
      ],
    });

    expect(harness.sceneStarted).toHaveLength(1);
    expect(harness.sceneStarted[0]).toEqual({
      key: 'ResultScene',
      data: {
        outcome: 'victory',
        finalBossHp: 0,
        finalPlayerHp: 100,
      },
    });
  });

  it('preserves outcome transition (BattleLost) alongside sibling resolution events', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-defeat',
      serverSequence: 10,
      events: [
        {
          type: 'BossSkillCast',
          skillId: 'flame-burst',
          sourceId: 'boss-hoa-long',
        },
        {
          type: 'DamageDealt',
          source: 'boss',
          target: 'player',
          amount: 250,
        },
        {
          type: 'BattleLost',
          outcome: 'defeat',
          finalBossHp: 400,
          finalPlayerHp: 0,
        },
      ],
    });

    expect(harness.sceneStarted).toHaveLength(1);
    expect(harness.sceneStarted[0]).toEqual({
      key: 'ResultScene',
      data: {
        outcome: 'defeat',
        finalBossHp: 400,
        finalPlayerHp: 0,
      },
    });
  });

  it('cleans up all subscriptions, tweens, and presentation state across create/shutdown cycles', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.battleEventListeners.size).toBe(1);

    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 2,
      events: [{ type: 'ComboChanged', combo: 4 }],
    });

    runScene(scene, ctx, 'shutdown');
    expect(harness.battleEventListeners.size).toBe(0);

    // Recreate
    runScene(scene, ctx, 'create');
    expect(harness.battleEventListeners.size).toBe(1);

    runScene(scene, ctx, 'shutdown');
    expect(harness.battleEventListeners.size).toBe(0);
  });

  it('does not calculate gameplay or author state (Server Authority)', () => {
    const source = readFileSync(
      resolve(__dirname, '../src/game/scenes/BattleScene.ts'),
      'utf8'
    )
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/(^|[^:])\/\/.*$/gm, '$1');

    for (const forbidden of [
      'hp -',
      'hp +',
      'bossHp -',
      'playerHp -',
      'accumulateDamage',
      'calculateDamage',
      'detectMatch',
      'findMatches',
      'evaluatePassive',
    ]) {
      expect(source, `BattleScene must not contain "${forbidden}"`).not.toContain(forbidden);
    }
  });
});
