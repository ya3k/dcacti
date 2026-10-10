import { describe, it, expect, vi, beforeEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { BattleScene } from '../src/game/scenes/BattleScene';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { GameRuntimeState } from '../src/state/GameRuntimeState';
import type { BattleEventsEnvelope, RuntimeBattleState } from '../src/game/runtime/GameRuntimeEvents';
import { SceneEventEmitter } from './support/SceneEventEmitter';
import {
  parseInBattleEvent,
  formatInBattleEvent,
  describeEventCallout,
  selectBatchCallout,
  BOSS_SKILL_NAMES,
  resolveBossSkillDisplayName,
  BOSS_ENRAGE_THRESHOLDS,
  isBossEnraged,
  resolveDamagePresentationPhase,
  SWAP_REJECTION_MESSAGES,
  formatSwapRejection,
  CARD_CAST_REJECTION_MESSAGES,
  formatCardCastRejection,
  SPECIAL_GEM_PRESENTATIONS,
  resolveSpecialGemPresentation,
  formatGemCellLabel,
  parseGemBaseLabel,
  parseSpecialGemBadge,
  CALLOUT_BOSS_COLOR,
  INTERACTION_BOSS_ATTACK,
  INTERACTION_RESOLVING,
  INTERACTION_YOUR_TURN,
  resolveOutcomeStatement,
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
  PresentedRelicTriggered,
  PresentedPowerChanged,
} from '../src/game/scenes/BattleEventPresenter';

vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({ destroy: vi.fn() })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
  Loader: { Events: { COMPLETE: 'complete' } },
  Input: { Events: { GAMEOBJECT_POINTER_DOWN: 'gameobjectdown' } },
  // Phaser's scene lifecycle events (TASK-204): the engine emits SHUTDOWN /
  // DESTROY on `scene.events` and never calls a scene method by name.
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
  /** The owned Relic instances the scene reads (`GET /api/relics`, §5.4). */
  relics?: Array<{ relicId: string; name: string }>;
  /** When set, the Relic collection read rejects with it. */
  relicsFailure?: Error;
}

function createSceneHarness(options: SceneHarnessOptions = {}) {
  const {
    state = INITIAL_RUNTIME_STATE,
    withRuntime = true,
    battleState = null,
    relics = [],
    relicsFailure,
  } = options;
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
    /**
     * The owned Relic collection read (`GET /api/relics`, API_CONTRACTS.md §5.4),
     * which is where the battle scene's Relic display names come from.
     */
    getRelics: vi.fn(async () => {
      if (relicsFailure) {
        throw relicsFailure;
      }
      return relics;
    }),
    setEngineStatus: vi.fn(),
  };

  function context(scene: object, sceneKey: string): object {
    const makeText = (value: string) => {
      const entry = { text: value, color: undefined as string | undefined };
      texts.push(entry);
      let alpha = 1;

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
        // Phaser's `GameObject#setAlpha` — the combat callout resets it when a new
        // message replaces the one on screen (TASK-210 §7).
        setAlpha: (next: number) => {
          alpha = next;
          return obj;
        },
        get alpha() {
          return alpha;
        },
        destroy: () => {},
      };
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
      // Phaser's injected scene event emitter (`this.events` / `sys.events`).
      events: new SceneEventEmitter(),
      scene: {
        start: (key: string, data?: unknown) => sceneStarted.push({ key, data }),
      },
      add: {
        rectangle: (_x: number, _y: number, _w: number, _h: number, fill?: number) => {
          const rect = {
            kind: 'tile',
            fill,
            width: _w,
            height: _h,
            /**
             * Phaser's `Rectangle#setSize` / `GameObject#setPosition` /
             * `GameObject#setVisible` — the three calls the HUD gauges make to
             * resize the existing fill rather than allocating one per state push
             * (TASK-210 §15).
             */
            setSize: (nextWidth: number, nextHeight: number) => {
              rect.width = nextWidth;
              rect.height = nextHeight;
              return rect;
            },
            setPosition: () => rect,
            setVisible: () => rect,
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

  it('parses and formats RelicTriggered with verbatim relicId (SIGNALR_PROTOCOL.md §3.2.23)', () => {
    const raw: PresentedRelicTriggered = {
      type: 'RelicTriggered',
      relicId: 'relic-instance-berserker-core',
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe(
      'RelicTriggered: relic-instance-berserker-core'
    );
  });

  it('does not surface an effect summary on RelicTriggered (§3.2.23 item 2, §3.2.25)', () => {
    const parsed = parseInBattleEvent({
      type: 'RelicTriggered',
      relicId: 'relic-instance-mana-crystal',
      effectSummary: '+10 Power',
    });

    expect(parsed).toEqual({
      type: 'RelicTriggered',
      relicId: 'relic-instance-mana-crystal',
    });
    expect(formatInBattleEvent(parsed!)).not.toContain('+10 Power');
  });

  it('parses and formats PowerChanged with all documented members verbatim (SIGNALR_PROTOCOL.md §3.2.24)', () => {
    const raw: PresentedPowerChanged = {
      type: 'PowerChanged',
      delta: 25,
      power: 45,
      source: 'card',
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('PowerChanged: +25 -> 45 (card)');
  });

  it('presents a negative PowerChanged delta as delivered without absolute-value conversion (§3.2.24 item 1)', () => {
    const parsed = parseInBattleEvent({
      type: 'PowerChanged',
      delta: -30,
      power: 12,
      source: 'card',
    });

    expect(parsed).toEqual({
      type: 'PowerChanged',
      delta: -30,
      power: 12,
      source: 'card',
    });
    expect(formatInBattleEvent(parsed!)).toBe('PowerChanged: -30 -> 12 (card)');
  });

  it('accepts each documented PowerChanged source value without rejecting any (§3.2.24 item 3)', () => {
    for (const source of ['match', 'card', 'relic']) {
      const parsed = parseInBattleEvent({
        type: 'PowerChanged',
        delta: 5,
        power: 5,
        source,
      });
      expect(parsed).toEqual({ type: 'PowerChanged', delta: 5, power: 5, source });
    }
  });

  it('carries PowerChanged source as the delivered string, never a number or ordinal (§3.2.4 item 3)', () => {
    const parsed = parseInBattleEvent({
      type: 'PowerChanged',
      delta: 1,
      power: 1,
      source: 'relic',
    });

    expect(typeof (parsed as PresentedPowerChanged).source).toBe('string');
    expect(parseInBattleEvent({ type: 'PowerChanged', delta: 1, power: 1, source: 0 })).toBeNull();
  });

  it('returns null for malformed RelicTriggered and PowerChanged payloads', () => {
    expect(parseInBattleEvent({ type: 'RelicTriggered' })).toBeNull();
    expect(parseInBattleEvent({ type: 'RelicTriggered', relicId: '' })).toBeNull();
    expect(parseInBattleEvent({ type: 'RelicTriggered', relicId: 123 })).toBeNull();

    expect(parseInBattleEvent({ type: 'PowerChanged' })).toBeNull();
    expect(parseInBattleEvent({ type: 'PowerChanged', delta: 5, power: 5 })).toBeNull();
    expect(parseInBattleEvent({ type: 'PowerChanged', power: 5, source: 'card' })).toBeNull();
    expect(parseInBattleEvent({ type: 'PowerChanged', delta: 5, source: 'card' })).toBeNull();
    expect(parseInBattleEvent({ type: 'PowerChanged', delta: '5', power: 5, source: 'card' })).toBeNull();
    expect(parseInBattleEvent({ type: 'PowerChanged', delta: 5, power: '5', source: 'card' })).toBeNull();
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

  // ---------------------------------------------------------------------------
  // TASK-245 — the combat phase classifier (GAME_RULES.md §17 steps 15–18c)
  // ---------------------------------------------------------------------------

  it('classifies one delivered damage instance into its ordered presentation phase', () => {
    // The two MVP damage directions are distinguished from the delivered party
    // strings alone (`SIGNALR_PROTOCOL.md` §3.2.14 item 3): the player's hit on
    // the Boss and the Boss's response are two ordered phases.
    expect(resolveDamagePresentationPhase('player', 'boss')).toBe('damage');
    expect(resolveDamagePresentationPhase('boss', 'player')).toBe('retaliation');

    // A direction the contract does not define is assigned to no phase: the
    // classifier never guesses which side was hurt.
    expect(resolveDamagePresentationPhase('player', 'player')).toBeNull();
    expect(resolveDamagePresentationPhase('boss', 'boss')).toBeNull();
    expect(resolveDamagePresentationPhase('third-party', 'boss')).toBeNull();
    expect(resolveDamagePresentationPhase('boss', 'third-party')).toBeNull();
    expect(resolveDamagePresentationPhase('', '')).toBeNull();
  });

  it('needs no member beyond the delivered source and target to classify a hit', () => {
    // It is a presentation classification over exactly the two documented
    // members, and it applies no damage: the same source/target pair always
    // yields the same phase whatever the amount is.
    const dealt = parseInBattleEvent({
      type: 'DamageDealt',
      source: 'player',
      target: 'boss',
      amount: 150,
    });
    expect(dealt).not.toBeNull();
    expect(
      resolveDamagePresentationPhase(
        (dealt as PresentedDamageDealt).source,
        (dealt as PresentedDamageDealt).target
      )
    ).toBe('damage');
  });
});

describe('TASK-245 — the SignalR wire contract is unchanged (PD-3, PD-4)', () => {
  /** The `src/`-relative socket types the frontend projection is built from. */
  const runtimeEventsSource = readFileSync(
    resolve(__dirname, '../src/game/runtime/GameRuntimeEvents.ts'),
    'utf8'
  );

  it('keeps the ReceiveEvents envelope at exactly its three documented members', () => {
    // SIGNALR_PROTOCOL.md §3: `ReceiveEvents(battleId, serverSequence, events[])`.
    // The presentation timeline reads those three and nothing else, and it adds
    // no member to the envelope.
    const body =
      /export interface BattleEventsEnvelope \{([\s\S]*?)\n\}/.exec(runtimeEventsSource)?.[1] ?? '';
    // Member declarations are the interface's own two-space-indented lines; the
    // `readonly unknown[]` in the `events` member's type is not one of them.
    const members = [...body.matchAll(/^ {2}readonly (\w+)/gm)].map((match) => match[1]);

    expect(members).toEqual(['battleId', 'serverSequence', 'events']);
  });

  it('keeps CascadeCreated at exactly { type, cascadeDepth }', () => {
    // SIGNALR_PROTOCOL.md §3.2.7 item 4: the event carries no other member. The
    // timeline keys its cascade phases on this value alone (PD-4).
    const parsed = parseInBattleEvent({ type: 'CascadeCreated', cascadeDepth: 3 });
    expect(parsed).toEqual({ type: 'CascadeCreated', cascadeDepth: 3 });
    expect(Object.keys(parsed as object).sort()).toEqual(['cascadeDepth', 'type']);
  });

  it('introduces no per-pass board snapshot on any delivered event', () => {
    // PD-4 / SIGNALR_PROTOCOL.md §3.1 item 2: intermediate board states are never
    // sent, and no member may carry one. Extra members on the wire are not read.
    expect(
      parseInBattleEvent({
        type: 'CascadeCreated',
        cascadeDepth: 1,
        board: [{ gemType: 'ATK' }],
        cells: [1, 2, 3],
      })
    ).toEqual({ type: 'CascadeCreated', cascadeDepth: 1 });

    expect(
      parseInBattleEvent({
        type: 'MatchCreated',
        shape: 'Straight',
        cells: [8, 9, 10],
        gemType: 'ATK',
        cascadeDepth: 1,
        board: [{ gemType: 'ATK' }],
      })
    ).toEqual({
      type: 'MatchCreated',
      shape: 'Straight',
      cells: [8, 9, 10],
      gemType: 'ATK',
      cascadeDepth: 1,
    });
  });

  it('accepts no board-resolution message the discriminator does not define', () => {
    // SIGNALR_PROTOCOL.md §3.2.2 item 2 / §8 item 7: no `BoardResolved`,
    // `BoardUpdated`, `CascadeUpdated`, or per-pass message exists, so none is
    // parsed — the set stays closed against events GAME_EVENTS.md §2 does not
    // define.
    for (const type of [
      'BoardResolved',
      'BoardUpdated',
      'BoardCreated',
      'CascadeUpdated',
      'SpecialGemActivated',
      'StatusEffectUpdated',
      'BossHpChanged',
    ]) {
      expect(parseInBattleEvent({ type, board: [] }), `${type} must stay ignored`).toBeNull();
    }
  });
});

describe('TASK-210 — the player-facing combat callout (SIGNALR_PROTOCOL.md §3.2.16–§3.2.24)', () => {
  /** The callout resolver a scene supplies: loaded Card definitions, else the id. */
  const cardNames = new Map([['card-heal', 'Heal']]);
  const resolveCardName = (cardId: string) => cardNames.get(cardId) ?? cardId;

  /**
   * The Relic resolver a scene supplies: the owned-instance read
   * (`GET /api/relics`, `API_CONTRACTS.md` §5.4) keyed by the identity
   * `RelicTriggered.relicId` carries, or `null` when no definition was loaded.
   *
   * It is deliberately not the Card resolver's shape: an unresolved Relic
   * instance identity is not shown (it is an internal handle), so this seam
   * fails closed.
   */
  const relicNames = new Map([
    ['relicinst_a1', 'Berserker Core'],
    ['relicinst_b2', 'Mana Crystal'],
    ['relicinst_c3', 'Emergency Core'],
  ]);
  const resolveRelicName = (relicId: string) => relicNames.get(relicId) ?? null;

  it('has nothing to say about the events that are already shown another way', () => {
    // The per-cell board highlights carry `MatchCreated`/`GemMatched`/`CascadeCreated`
    // position; the damage and Power floaters carry the numbers; and
    // `DamageCalculated` is the six-factor pipeline detail. Each therefore
    // produces no callout.
    for (const event of [
      { type: 'GemMatched', cellIndex: 8, gemType: 'ATK' },
      { type: 'CascadeCreated', cascadeDepth: 2 },
      { type: 'DamageCalculated', base: 137, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 137 },
      { type: 'DamageDealt', source: 'player', target: 'boss', amount: 120 },
      { type: 'DamageTaken', source: 'boss', target: 'player', amount: 80 },
      { type: 'PowerChanged', delta: 25, power: 25, source: 'match' },
    ]) {
      const parsed = parseInBattleEvent(event);
      expect(parsed, `${event.type} must parse`).not.toBeNull();
      expect(
        describeEventCallout(parsed!, resolveCardName, resolveRelicName),
        `${event.type} must produce no callout`
      ).toBeNull();
    }
  });

  it('names the triggered Relic from the delivered definition and never shows its instance id', () => {
    // SIGNALR_PROTOCOL.md §3.2.23: the event carries only the owned instance
    // identity. The player-facing name comes from the caller's definition lookup
    // (API_CONTRACTS.md §5.4's `name`), which is the same delivered-read pattern
    // the Card callout already uses.
    const triggered = describeEventCallout(
      parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_a1' })!,
      resolveCardName,
      resolveRelicName
    );

    expect(triggered).toEqual({ message: 'RELIC: Berserker Core', color: '#7dd3fc', priority: 2 });

    // The raw instance identity is technical (RELIC_RULES.md §2.2 item 3) and is
    // never part of what the player reads.
    expect(triggered?.message).not.toContain('relicinst_a1');
    expect(triggered?.message).not.toContain('relic-');

    // A second Relic resolves to its own name — the lookup is keyed by the
    // delivered identity, not by a position or a fixed label.
    expect(
      describeEventCallout(
        parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_b2' })!,
        resolveCardName,
        resolveRelicName
      )?.message
    ).toBe('RELIC: Mana Crystal');
  });

  it('produces no callout for a RelicTriggered whose definition is not loaded', () => {
    // A missing definition is not a licence to print an internal handle, and not
    // an error: the event simply has nothing player-facing to say, and the rest of
    // the batch's presentation is unaffected.
    const unknown = describeEventCallout(
      parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_unknown' })!,
      resolveCardName,
      resolveRelicName
    );

    expect(unknown).toBeNull();

    // Even a resolver that has loaded nothing at all yields no callout — never a
    // fallback to the identity.
    const empty = describeEventCallout(
      parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_a1' })!,
      resolveCardName,
      () => null
    );
    expect(empty).toBeNull();

    // ...and no name is derived from the Relic's own content members: none of them
    // is even part of the parsed event.
    const parsed = parseInBattleEvent({
      type: 'RelicTriggered',
      relicId: 'relicinst_unknown',
      trigger: 'OnMatchCount',
      condition: { conditionType: 'MatchCountAtLeast', threshold: 3 },
      effectDefinition: [{ effectType: 'ATK', valueType: 'Percentage', value: 5 }],
    })!;
    expect(parsed).toEqual({ type: 'RelicTriggered', relicId: 'relicinst_unknown' });
  });

  it('shows at most one callout when several Relics trigger in one batch, naming the last', () => {
    // A 3–5 Relic loadout resolves every eligible Relic for one event in equip-slot
    // order (RELIC_RULES.md §4.2), so a single batch can carry more than one
    // `RelicTriggered` — including Emergency Core's per-Swap re-emission while
    // armed. The single-callout mechanism names the one the batch finished on and
    // is deliberately not widened into a queue.
    const selected = selectBatchCallout(
      [
        parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_a1' })!,
        parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_b2' })!,
        parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_c3' })!,
      ],
      resolveCardName,
      resolveRelicName
    );

    expect(selected?.message).toBe('RELIC: Emergency Core');
    expect(selected?.message).not.toContain('relicinst');
  });

  it('calls out a meaningful Combo and stays silent about an ordinary single-Match Swap', () => {
    // MATCH3_RULES.md §6.2 item 1: the first Match of a committed Swap makes Combo
    // 1, which is the ordinary case, not a combo.
    const single = parseInBattleEvent({ type: 'ComboChanged', combo: 1 })!;
    expect(describeEventCallout(single, resolveCardName, resolveRelicName)).toBeNull();

    const cascade = parseInBattleEvent({ type: 'ComboChanged', combo: 4 })!;
    expect(describeEventCallout(cascade, resolveCardName, resolveRelicName)).toEqual({
      message: 'COMBO ×4',
      color: '#fbbf24',
      priority: 1,
    });
  });

  it('names a cast from the loaded definition and never leaks a Boss skill identity', () => {
    expect(
      describeEventCallout(
        parseInBattleEvent({ type: 'CardCast', cardId: 'card-heal' })!,
        resolveCardName,
        resolveRelicName
      )
    ).toEqual({ message: 'CARD: Heal', color: '#a5b4fc', priority: 2 });

    // An identity with no loaded definition is shown as the identity itself.
    expect(
      describeEventCallout(
        parseInBattleEvent({ type: 'CardCast', cardId: 'card-unknown' })!,
        resolveCardName,
        resolveRelicName
      )?.message
    ).toBe('CARD: card-unknown');

    expect(
      describeEventCallout(
        parseInBattleEvent({ type: 'PetSkillCast', cardId: 'card-inferno' })!,
        resolveCardName,
        resolveRelicName
      )?.message
    ).toBe('PET SKILL: card-inferno');

    // `skillId` is a technical identity with no client-visible definition: the
    // event's own existence is what the player is told.
    const bossSkill = describeEventCallout(
      parseInBattleEvent({ type: 'BossSkillCast', skillId: 'flame-burst-mega', sourceId: 'boss-hoa-long' })!,
      resolveCardName,
      resolveRelicName
    );
    expect(bossSkill).toEqual({ message: 'BOSS SKILL', color: '#f87171', priority: 3 });
    expect(bossSkill?.message).not.toContain('flame-burst-mega');
  });

  it('attributes a Passive callout to the entity the delivered source names', () => {
    const petCharged = describeEventCallout(
      parseInBattleEvent({
        type: 'PassiveCharged',
        passiveId: 'thanh-xa-poison',
        source: 'pet',
        sourceId: 'pet-instance-1',
        progress: 3,
        threshold: 7,
      })!,
      resolveCardName,
      resolveRelicName
    );
    expect(petCharged).toEqual({ message: 'PASSIVE 3/7', color: '#c4b5fd', priority: 5 });

    const bossCharged = describeEventCallout(
      parseInBattleEvent({
        type: 'PassiveCharged',
        passiveId: 'boss-hoa-long-rage',
        source: 'boss',
        sourceId: 'boss-hoa-long',
        progress: 5,
        threshold: 5,
      })!,
      resolveCardName,
      resolveRelicName
    );
    expect(bossCharged?.message).toBe('BOSS PASSIVE 5/5');

    expect(
      describeEventCallout(
        parseInBattleEvent({
          type: 'PassiveTriggered',
          passiveId: 'pet-phoenix-rebirth',
          source: 'pet',
          sourceId: 'pet-instance-1',
          progress: 5,
          threshold: 5,
        })!,
        resolveCardName,
        resolveRelicName
      )?.message
    ).toBe('PASSIVE TRIGGERED');

    // Neither the Passive identity nor either pair of numbers is guessed at, and
    // the identity itself is not shown: it is technical, and the Pet panel already
    // carries the delivered `passiveId`.
    expect(bossCharged?.message).not.toContain('boss-hoa-long-rage');
  });

  it('calls out an ordinary Match', () => {
    const match = parseInBattleEvent({
      type: 'MatchCreated',
      shape: 'Straight',
      cells: [8, 9, 10],
      gemType: 'ATK',
      cascadeDepth: 0,
    })!;

    expect(describeEventCallout(match, resolveCardName, resolveRelicName)).toEqual({
      message: 'MATCH',
      color: '#e2e8f0',
      priority: 6,
    });
  });

  it('selects exactly one callout per batch, by importance then by delivery order', () => {
    const events = [
      parseInBattleEvent({
        type: 'MatchCreated',
        shape: 'Straight',
        cells: [8, 9, 10],
        gemType: 'ATK',
        cascadeDepth: 0,
      })!,
      parseInBattleEvent({ type: 'ComboChanged', combo: 4 })!,
      parseInBattleEvent({ type: 'PowerChanged', delta: 10, power: 10, source: 'match' })!,
      parseInBattleEvent({ type: 'DamageDealt', source: 'player', target: 'boss', amount: 120 })!,
    ];

    expect(selectBatchCallout(events, resolveCardName, resolveRelicName)?.message).toBe('COMBO ×4');

    // Among equally important events the last one wins: it is the one the
    // resolution finished on.
    const twoCasts = [
      parseInBattleEvent({ type: 'CardCast', cardId: 'card-heal' })!,
      parseInBattleEvent({ type: 'PetSkillCast', cardId: 'card-inferno' })!,
    ];
    expect(selectBatchCallout(twoCasts, resolveCardName, resolveRelicName)?.message).toBe(
      'PET SKILL: card-inferno'
    );

    // A batch that produces no events a player needs told produces no callout.
    expect(
      selectBatchCallout(
        [
          parseInBattleEvent({ type: 'GemMatched', cellIndex: 8, gemType: 'ATK' })!,
          parseInBattleEvent({ type: 'CascadeCreated', cascadeDepth: 3 })!,
        ],
        resolveCardName,
        resolveRelicName
      )
    ).toBeNull();

    expect(selectBatchCallout([], resolveCardName, resolveRelicName)).toBeNull();
  });

  it('still presents exactly one callout when a Relic trigger shares a batch with Combo, cast and Match', () => {
    // The ladder is unchanged for every existing event: a Combo outranks a Relic
    // trigger, a Relic trigger outranks a Boss action, a Passive and a Match, and
    // it shares the cast's slot (the later-delivered one wins that tie).
    const comboBatch = [
      parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_a1' })!,
      parseInBattleEvent({ type: 'ComboChanged', combo: 3 })!,
    ];
    expect(selectBatchCallout(comboBatch, resolveCardName, resolveRelicName)?.message).toBe('COMBO ×3');

    const bossBatch = [
      parseInBattleEvent({ type: 'BossSkillCast', skillId: 'flame-burst', sourceId: 'boss-hoa-long' })!,
      parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_a1' })!,
      parseInBattleEvent({ type: 'MatchCreated', shape: 'Straight', cells: [0, 1, 2], gemType: 'ATK', cascadeDepth: 0 })!,
    ];
    expect(selectBatchCallout(bossBatch, resolveCardName, resolveRelicName)?.message).toBe(
      'RELIC: Berserker Core'
    );

    // Sharing a priority number with the cast slot changes no existing event's
    // standing, and the tie is the pre-existing "last delivered wins" rule.
    const castBatch = [
      parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_b2' })!,
      parseInBattleEvent({ type: 'CardCast', cardId: 'card-heal' })!,
    ];
    expect(selectBatchCallout(castBatch, resolveCardName, resolveRelicName)?.message).toBe('CARD: Heal');

    // An unresolvable Relic is simply absent from the batch's selection, and the
    // sibling events still get their callout.
    const unresolvedBatch = [
      parseInBattleEvent({ type: 'RelicTriggered', relicId: 'relicinst_unknown' })!,
      parseInBattleEvent({ type: 'ComboChanged', combo: 2 })!,
    ];
    expect(selectBatchCallout(unresolvedBatch, resolveCardName, resolveRelicName)?.message).toBe('COMBO ×2');
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
    {
      name: 'RelicTriggered',
      event: {
        type: 'RelicTriggered',
        relicId: 'relic-instance-berserker-core',
      },
      expectedTokens: ['RelicTriggered', 'relic-instance-berserker-core'],
    },
    {
      name: 'PowerChanged',
      event: {
        type: 'PowerChanged',
        delta: 25,
        power: 45,
        source: 'card',
      },
      expectedTokens: ['PowerChanged', '+25', '45', 'card'],
    },
  ])('presents $name verbatim when delivered in ReceiveEvents', ({ event, expectedTokens }) => {
    // TASK-209 §5: the raw event feed is no longer *rendered* — `Match: … depth: 0`,
    // `DamageCalculated: base …` and the rest are developer console material, not the
    // player's battle HUD. The scene still parses every batch and records each
    // formatted line in delivery order for development and tests, which is where the
    // delivered members are still asserted verbatim.
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-test',
      serverSequence: 3,
      events: [event],
    });

    const presented = (scene as unknown as { getPresentedEvents(): readonly string[] })
      .getPresentedEvents();

    expect(presented).toHaveLength(1);
    for (const token of expectedTokens) {
      expect(presented[0], `Expected presented output to contain "${token}"`).toContain(token);
    }

    // And none of that raw line reaches the player-facing HUD.
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain(presented[0]);
  });

  it('presents RelicTriggered and PowerChanged in delivered order alongside existing types (GAME_EVENTS.md §1)', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-relic',
      serverSequence: 9,
      events: [
        { type: 'ComboChanged', combo: 2 },
        { type: 'PassiveCharged', passiveId: 'pet-passive', source: 'pet', sourceId: 'pet-inst-1', progress: 1, threshold: 3 },
        { type: 'RelicTriggered', relicId: 'relic-instance-mana-crystal' },
        { type: 'PowerChanged', delta: 10, power: 30, source: 'relic' },
        { type: 'CardCast', cardId: 'card-shield' },
      ],
    });

    const presented = (scene as unknown as { getPresentedEvents(): readonly string[] }).getPresentedEvents();
    expect(presented).toEqual([
      'Combo: 2',
      'PassiveCharged: pet-passive (pet:pet-inst-1) 1/3',
      'RelicTriggered: relic-instance-mana-crystal',
      'PowerChanged: +10 -> 30 (relic)',
      'CardCast: card-shield',
    ]);
  });

  it('no longer drops RelicTriggered or PowerChanged at the parser default branch', () => {
    for (const event of [
      { type: 'RelicTriggered', relicId: 'relic-instance-berserker-core' },
      { type: 'PowerChanged', delta: 25, power: 45, source: 'card' },
    ]) {
      const parsed = parseInBattleEvent(event);
      expect(parsed, `${event.type} must be recognized`).not.toBeNull();
      expect(formatInBattleEvent(parsed!).length).toBeGreaterThan(0);
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

    const presented = (scene as unknown as { getPresentedEvents(): readonly string[] })
      .getPresentedEvents();

    // The delivered order, as recorded (TASK-209 §5: recorded rather than rendered).
    const idxMatch = presented.findIndex((line) => line.startsWith('Match: Straight ATK [8, 9, 10]'));
    const idxGem = presented.findIndex((line) => line.startsWith('GemMatched: cell 8 (ATK)'));
    const idxCascade = presented.findIndex((line) => line.startsWith('Cascade: depth 2'));
    const idxCombo = presented.findIndex((line) => line.startsWith('Combo: 3'));
    const idxDamage = presented.findIndex((line) => line.startsWith('DamageCalculated: base 100'));

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

  it('releases input guard on scene teardown', () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');

    harness.emitBattleEvents({
      battleId: 'b-lock',
      serverSequence: 5,
      events: [{ type: 'ComboChanged', combo: 5 }],
    });

    harness.shutdownScene(ctx);
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
        // TASK-149: the handoff also carries the batch's battleId so
        // ResultScene can read the persisted reward summary
        // (API_CONTRACTS.md §4). The outcome members are unchanged.
        battleId: 'b-final',
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
        // TASK-149: the handoff also carries the batch's battleId.
        battleId: 'b-defeat',
      },
    });
  });

  it('cleans up all subscriptions and presentation state across create/teardown cycles', () => {
    const { harness, scene, ctx } = createBattle();

    runScene(scene, ctx, 'create');
    expect(harness.battleEventListeners.size).toBe(1);

    harness.emitBattleEvents({
      battleId: 'b-1',
      serverSequence: 2,
      events: [{ type: 'ComboChanged', combo: 4 }],
    });

    harness.shutdownScene(ctx);
    expect(harness.battleEventListeners.size).toBe(0);

    // Recreate
    runScene(scene, ctx, 'create');
    expect(harness.battleEventListeners.size).toBe(1);

    harness.shutdownScene(ctx);
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

describe('TASK-218B — the Relic trigger callout reaches the player (SIGNALR_PROTOCOL.md §3.2.23)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  /** The owned Relics the scene reads (`GET /api/relics`, API_CONTRACTS.md §5.4). */
  const OWNED_RELICS = [
    { relicId: 'relicinst_berserker', name: 'Berserker Core' },
    { relicId: 'relicinst_mana', name: 'Mana Crystal' },
    { relicId: 'relicinst_emergency', name: 'Emergency Core' },
  ];

  function createBattle(relics = OWNED_RELICS, relicsFailure?: Error) {
    const harness = createSceneHarness({ relics, ...(relicsFailure ? { relicsFailure } : {}) });
    const scene = new BattleScene();
    const ctx = harness.context(scene, 'BattleScene');
    return { harness, scene, ctx };
  }

  /** The callout line the player actually reads, as rendered. */
  function renderedCallout(harness: ReturnType<typeof createSceneHarness>): string {
    const text = (harness as unknown as { texts: Array<{ text: string }> }).texts;
    // The callout is the centered line on the row above the board; the scene's
    // harness records every Text with its value, so the last non-empty centered
    // one carrying a callout vocabulary word is the callout.
    const callouts = text
      .map((entry) => entry.text)
      .filter((value) => /^(MATCH|COMBO ×\d+|CARD: |PET SKILL: |BOSS SKILL|RELIC: |PASSIVE)/.test(value));
    return callouts[callouts.length - 1] ?? '';
  }

  it('names the triggered Relic in the player-facing callout, never its instance id', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    harness.emitBattleEvents({
      battleId: 'b-relic',
      serverSequence: 4,
      events: [{ type: 'RelicTriggered', relicId: 'relicinst_berserker' }],
    });

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('RELIC: Berserker Core');
    expect(rendered).not.toContain('relicinst_berserker');
    expect(rendered).not.toContain('RelicTriggered');
  });

  it('names two different Relics from the delivered read, one callout at a time', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    harness.emitBattleEvents({
      battleId: 'b-relic',
      serverSequence: 4,
      events: [{ type: 'RelicTriggered', relicId: 'relicinst_mana' }],
    });
    expect(harness.texts.map((t) => t.text).join('\n')).toContain('RELIC: Mana Crystal');

    harness.emitBattleEvents({
      battleId: 'b-relic',
      serverSequence: 5,
      events: [{ type: 'RelicTriggered', relicId: 'relicinst_emergency' }],
    });
    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).toContain('RELIC: Emergency Core');
    expect(rendered).not.toContain('relicinst_');
  });

  it('shows no callout at all for a Relic the loaded definitions do not contain', async () => {
    const { harness, scene, ctx } = createBattle([
      { relicId: 'relicinst_berserker', name: 'Berserker Core' },
    ]);
    runScene(scene, ctx, 'create');
    await flush();

    // The whole event is delivered; nothing about it is player-facing.
    harness.emitBattleEvents({
      battleId: 'b-relic',
      serverSequence: 4,
      events: [
        { type: 'RelicTriggered', relicId: 'relicinst_unowned' },
        { type: 'PowerChanged', delta: 5, power: 5, source: 'relic' },
      ],
    });

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('relicinst_unowned');
    expect(rendered).not.toContain('RelicTriggered');
    expect(rendered).not.toContain('RELIC:');
    // The sibling event's own feedback is unaffected: the Power movement is still
    // drawn, so an unresolvable Relic does not stop the rest of the presentation.
    expect(harness.texts.map((t) => t.text)).toContain('+5');
  });

  it('shows no callout when the Relic collection read fails — never the raw id', async () => {
    const { harness, scene, ctx } = createBattle([], new Error('GET /api/relics unavailable'));
    runScene(scene, ctx, 'create');
    await flush();

    harness.emitBattleEvents({
      battleId: 'b-relic',
      serverSequence: 4,
      events: [{ type: 'RelicTriggered', relicId: 'relicinst_berserker' }],
    });

    const rendered = harness.texts.map((t) => t.text).join('\n');
    expect(rendered).not.toContain('RELIC:');
    expect(rendered).not.toContain('relicinst_berserker');
  });

  it('keeps every existing callout unchanged and still presents exactly one per batch', async () => {
    const { harness, scene, ctx } = createBattle();
    runScene(scene, ctx, 'create');
    await flush();

    // A Combo still outranks a Relic trigger in the same batch.
    harness.emitBattleEvents({
      battleId: 'b-relic',
      serverSequence: 4,
      events: [
        { type: 'MatchCreated', shape: 'Straight', cells: [0, 1, 2], gemType: 'ATK', cascadeDepth: 0 },
        { type: 'ComboChanged', combo: 3 },
        { type: 'RelicTriggered', relicId: 'relicinst_berserker' },
      ],
    });
    expect(renderedCallout(harness)).toBe('COMBO ×3');

    // An ordinary single-Match Swap is still just `MATCH`.
    harness.emitBattleEvents({
      battleId: 'b-relic',
      serverSequence: 5,
      events: [
        { type: 'MatchCreated', shape: 'Straight', cells: [3, 4, 5], gemType: 'ATK', cascadeDepth: 0 },
      ],
    });
    expect(renderedCallout(harness)).toBe('MATCH');

    // Several Relic triggers in one batch still produce one callout, naming the
    // last one the resolution finished on (RELIC_RULES.md §4.2's equip-slot order).
    harness.emitBattleEvents({
      battleId: 'b-relic',
      serverSequence: 6,
      events: [
        { type: 'RelicTriggered', relicId: 'relicinst_berserker' },
        { type: 'RelicTriggered', relicId: 'relicinst_mana' },
        { type: 'RelicTriggered', relicId: 'relicinst_emergency' },
      ],
    });

    // Three triggers in one batch still produce exactly ONE callout line: the
    // scene owns a single callout Text, and the batch's selected callout replaces
    // whatever was on it. Nothing is queued and no second line is created.
    const lastBatchRelicCallouts = harness.texts
      .map((t) => t.text)
      .filter((value) => value.startsWith('RELIC: '));
    expect(lastBatchRelicCallouts).toEqual(['RELIC: Emergency Core']);
    expect(renderedCallout(harness)).toBe('RELIC: Emergency Core');
    // The batch named one Relic, and it named the right one — the last the
    // resolution finished on (RELIC_RULES.md §4.2's equip-slot order).
    expect(lastBatchRelicCallouts[0]).not.toContain('relicinst_');
  });

  describe('TASK-231 Boss Skills and Combat Readability', () => {
    const resolveCardName = (id: string) => id;
    const resolveRelicName = (id: string) => id;

    it('maps all five canonical BossSkillCast.skillId values to display names from BOSS_RULES.md §6.3/§6.4', () => {
      const skills = [
        { skillId: 'flame-burst', expectedName: 'Flame Burst' },
        { skillId: 'drain-power', expectedName: 'Drain Power' },
        { skillId: 'root', expectedName: 'Root' },
        { skillId: 'earthquake', expectedName: 'Earthquake' },
        { skillId: 'thunder-strike', expectedName: 'Thunder Strike' },
      ];

      for (const { skillId, expectedName } of skills) {
        expect(BOSS_SKILL_NAMES[skillId]).toBe(expectedName);
        expect(resolveBossSkillDisplayName(skillId)).toBe(expectedName);

        const callout = describeEventCallout(
          parseInBattleEvent({ type: 'BossSkillCast', skillId, sourceId: 'boss-test' })!,
          resolveCardName,
          resolveRelicName
        );

        expect(callout).toEqual({
          message: `BOSS SKILL: ${expectedName}`,
          color: '#f87171',
          priority: 3,
        });
      }
    });

    it('falls back safely to BOSS SKILL when skillId is unknown', () => {
      expect(resolveBossSkillDisplayName('unknown-skill')).toBeNull();

      const callout = describeEventCallout(
        parseInBattleEvent({ type: 'BossSkillCast', skillId: 'unknown-skill', sourceId: 'boss-test' })!,
        resolveCardName,
        resolveRelicName
      );

      expect(callout).toEqual({
        message: 'BOSS SKILL',
        color: '#f87171',
        priority: 3,
      });
    });

    it('evaluates isBossEnraged according to BOSS_RULES.md §5 item 4 & §6.1 strict boundary', () => {
      // Hỏa Long, Thủy Ma, Mộc Yêu: threshold 1500 (30% of 5000)
      expect(BOSS_ENRAGE_THRESHOLDS['boss-hoa-long']).toBe(1500);
      expect(isBossEnraged('boss-hoa-long', 5000, 5000)).toBe(false);
      expect(isBossEnraged('boss-hoa-long', 1501, 5000)).toBe(false);
      expect(isBossEnraged('boss-hoa-long', 1500, 5000)).toBe(false); // strict <
      expect(isBossEnraged('boss-hoa-long', 1499, 5000)).toBe(true);
      expect(isBossEnraged('boss-hoa-long', 1, 5000)).toBe(true);
      expect(isBossEnraged('boss-hoa-long', 0, 5000)).toBe(false); // defeated

      expect(isBossEnraged('boss-thuy-ma', 1500, 5000)).toBe(false);
      expect(isBossEnraged('boss-thuy-ma', 1499, 5000)).toBe(true);

      expect(isBossEnraged('boss-moc-yeu', 1500, 5000)).toBe(false);
      expect(isBossEnraged('boss-moc-yeu', 1499, 5000)).toBe(true);

      // Sơn Thạch Vệ: threshold 1500 (50% of 3000)
      expect(BOSS_ENRAGE_THRESHOLDS['boss-son-thach-ve']).toBe(1500);
      expect(isBossEnraged('boss-son-thach-ve', 1500, 3000)).toBe(false);
      expect(isBossEnraged('boss-son-thach-ve', 1499, 3000)).toBe(true);

      // Kim Lôi Vương: threshold 2100 (75% of 2800)
      expect(BOSS_ENRAGE_THRESHOLDS['boss-kim-loi-vuong']).toBe(2100);
      expect(isBossEnraged('boss-kim-loi-vuong', 2100, 2800)).toBe(false);
      expect(isBossEnraged('boss-kim-loi-vuong', 2099, 2800)).toBe(true);

      // Unknown boss: falls back to 30% of maxHp
      expect(isBossEnraged('unknown-boss', 300, 1000)).toBe(false);
      expect(isBossEnraged('unknown-boss', 299, 1000)).toBe(true);
    });

    it('maps all canonical Swap rejection codes and provides safe fallback', () => {
      expect(SWAP_REJECTION_MESSAGES['NO_MATCH_FROM_SWAP']).toBe('Swap does not create a match.');
      expect(formatSwapRejection('NO_MATCH_FROM_SWAP')).toBe('Swap does not create a match.');
      expect(formatSwapRejection('INVALID_SWAP')).toBe('Invalid swap. Gems must be adjacent.');
      expect(formatSwapRejection('INVALID_CELL_INDEX')).toBe('Invalid board position.');
      expect(formatSwapRejection('STALE_ACTION')).toBe('Board has changed. Please try again.');
      expect(formatSwapRejection('BATTLE_NOT_FOUND')).toBe('Battle session not found.');

      // Fallbacks
      expect(formatSwapRejection('CUSTOM_UNKNOWN')).toBe('Swap rejected (CUSTOM_UNKNOWN).');
      expect(formatSwapRejection(null)).toBe('Swap rejected.');
      expect(formatSwapRejection(undefined)).toBe('Swap rejected.');
      expect(formatSwapRejection('')).toBe('Swap rejected.');
    });

    it('maps all canonical CardCast rejection codes and provides safe fallback', () => {
      expect(CARD_CAST_REJECTION_MESSAGES['INSUFFICIENT_POWER']).toBe('Not enough Power.');
      expect(formatCardCastRejection('INSUFFICIENT_POWER')).toBe('Not enough Power.');
      expect(formatCardCastRejection('CARD_CAST_ALREADY_USED_THIS_TURN')).toBe(
        'Only one card can be cast per turn.'
      );
      expect(formatCardCastRejection('CARD_NOT_IN_LOADOUT')).toBe(
        'Card is not in your current loadout.'
      );
      expect(formatCardCastRejection('INVALID_CARD')).toBe('Invalid card.');
      expect(formatCardCastRejection('PET_SKILL_NOT_OWNED')).toBe('Pet skill is not available.');
      expect(formatCardCastRejection('BATTLE_NOT_FOUND')).toBe('Battle session not found.');

      // Fallbacks
      expect(formatCardCastRejection('CUSTOM_UNKNOWN')).toBe('Cast rejected (CUSTOM_UNKNOWN).');
      expect(formatCardCastRejection(null)).toBe('Cast rejected.');
      expect(formatCardCastRejection(undefined)).toBe('Cast rejected.');
      expect(formatCardCastRejection('')).toBe('Cast rejected.');
    });

    it('evaluates isBossEnraged latching behavior across healing and defeat', () => {
      // Unlatched behavior (currentlyEnraged = false)
      expect(isBossEnraged('boss-hoa-long', 5000, 5000, false)).toBe(false);
      expect(isBossEnraged('boss-hoa-long', 1499, 5000, false)).toBe(true);
      expect(isBossEnraged('boss-hoa-long', 0, 5000, false)).toBe(false);

      // Latched behavior (currentlyEnraged = true) stays enraged on healing and defeat
      expect(isBossEnraged('boss-hoa-long', 1499, 5000, true)).toBe(true);
      expect(isBossEnraged('boss-hoa-long', 3000, 5000, true)).toBe(true); // healed
      expect(isBossEnraged('boss-hoa-long', 5000, 5000, true)).toBe(true); // full heal
      expect(isBossEnraged('boss-hoa-long', 0, 5000, true)).toBe(true); // defeated
      expect(isBossEnraged('boss-thuy-ma', 0, 5000, true)).toBe(true);
      expect(isBossEnraged('boss-kim-loi-vuong', 2800, 2800, true)).toBe(true);
    });

    it('resolves visual presentations for all four authoritative specialGem variants', () => {
      // LineClear Horizontal
      const lcH = resolveSpecialGemPresentation({ type: 'LineClear', orientation: 'Horizontal' });
      expect(lcH).toEqual(SPECIAL_GEM_PRESENTATIONS.LineClearHorizontal);
      expect(lcH?.badge).toBe('[H]');
      expect(lcH?.strokeWidth).toBe(3);
      expect(lcH?.strokeColor).toBe(0x38bdf8);

      // LineClear Vertical
      const lcV = resolveSpecialGemPresentation({ type: 'LineClear', orientation: 'Vertical' });
      expect(lcV).toEqual(SPECIAL_GEM_PRESENTATIONS.LineClearVertical);
      expect(lcV?.badge).toBe('[V]');
      expect(lcV?.strokeWidth).toBe(3);
      expect(lcV?.strokeColor).toBe(0x38bdf8);

      // LineClear default (orientation missing/omitted) falls back safely to Horizontal
      const lcDef = resolveSpecialGemPresentation({ type: 'LineClear' });
      expect(lcDef).toEqual(SPECIAL_GEM_PRESENTATIONS.LineClearHorizontal);

      // Burst (3x3 area)
      const burst = resolveSpecialGemPresentation({ type: 'Burst' });
      expect(burst).toEqual(SPECIAL_GEM_PRESENTATIONS.Burst);
      expect(burst?.badge).toBe('[BURST]');
      expect(burst?.strokeWidth).toBe(3);
      expect(burst?.strokeColor).toBe(0xfacc15);

      // Area (cross/plus)
      const area = resolveSpecialGemPresentation({ type: 'Area' });
      expect(area).toEqual(SPECIAL_GEM_PRESENTATIONS.Area);
      expect(area?.badge).toBe('[AREA]');
      expect(area?.strokeWidth).toBe(3);
      expect(area?.strokeColor).toBe(0xf472b6);

      // Ordinary gems & unrecognized
      expect(resolveSpecialGemPresentation(null)).toBeNull();
      expect(resolveSpecialGemPresentation(undefined)).toBeNull();
      expect(resolveSpecialGemPresentation({ type: 'Unknown' })).toBeNull();
    });

    it('formats cell labels preserving base types and parsing badges', () => {
      // Normal gems: unchanged
      expect(formatGemCellLabel('ATK', null)).toBe('ATK');
      expect(formatGemCellLabel('DEF', undefined)).toBe('DEF');
      expect(formatGemCellLabel('HP')).toBe('HP');
      expect(formatGemCellLabel('PWR', null)).toBe('PWR');

      // Special gem variants
      expect(formatGemCellLabel('ATK', { type: 'LineClear', orientation: 'Horizontal' })).toBe('ATK [H]');
      expect(formatGemCellLabel('DEF', { type: 'LineClear', orientation: 'Vertical' })).toBe('DEF [V]');
      expect(formatGemCellLabel('HP', { type: 'Burst' })).toBe('HP [BURST]');
      expect(formatGemCellLabel('PWR', { type: 'Area' })).toBe('PWR [AREA]');

      // Base label parsing
      expect(parseGemBaseLabel('ATK')).toBe('ATK');
      expect(parseGemBaseLabel('ATK [H]')).toBe('ATK');
      expect(parseGemBaseLabel('DEF [V]')).toBe('DEF');
      expect(parseGemBaseLabel('HP [BURST]')).toBe('HP');
      expect(parseGemBaseLabel('PWR [AREA]')).toBe('PWR');
      expect(parseGemBaseLabel('')).toBe('');
      expect(parseGemBaseLabel(null)).toBe('');

      // Badge parsing
      expect(parseSpecialGemBadge('ATK')).toBeNull();
      expect(parseSpecialGemBadge('ATK [H]')).toBe('[H]');
      expect(parseSpecialGemBadge('DEF [V]')).toBe('[V]');
      expect(parseSpecialGemBadge('HP [BURST]')).toBe('[BURST]');
      expect(parseSpecialGemBadge('PWR [AREA]')).toBe('[AREA]');
      expect(parseSpecialGemBadge('')).toBeNull();
      expect(parseSpecialGemBadge(null)).toBeNull();
    });
  });
});

describe('TASK-246 — the interaction indicator is client state, never wire state', () => {
  /** The `src/`-relative socket types the frontend projection is built from. */
  const runtimeEventsSource = readFileSync(
    resolve(__dirname, '../src/game/runtime/GameRuntimeEvents.ts'),
    'utf8'
  );

  /** The presenter's own source, with its prose removed. */
  const presenterSource = readFileSync(
    resolve(__dirname, '../src/game/scenes/BattleEventPresenter.ts'),
    'utf8'
  )
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:])\/\/.*$/gm, '$1');

  it('names exactly the three client-side interaction states, each with a display colour', () => {
    // The vocabulary is the client's own. `YOUR TURN` states that this client
    // permits input (nothing on the wire says whose turn it is), `RESOLVING` that
    // its local timeline is playing, and `BOSS ATTACK` that the delivered batch
    // named the Boss as the damage source (TASK-246 §4).
    expect(INTERACTION_YOUR_TURN).toEqual({ label: 'YOUR TURN', color: '#4ade80' });
    expect(INTERACTION_RESOLVING).toEqual({ label: 'RESOLVING', color: '#94a3b8' });
    expect(INTERACTION_BOSS_ATTACK).toEqual({ label: 'BOSS ATTACK', color: CALLOUT_BOSS_COLOR });

    // Frozen presentation constants: they cannot be mutated into state carriers.
    expect(Object.isFrozen(INTERACTION_YOUR_TURN)).toBe(true);
    expect(Object.isFrozen(INTERACTION_RESOLVING)).toBe(true);
    expect(Object.isFrozen(INTERACTION_BOSS_ATTACK)).toBe(true);
  });

  it('states the delivered outcome and never substitutes one for an unrecognised value', () => {
    // SIGNALR_PROTOCOL.md §3.2.19 item 1 fixes the delivered value set as
    // "victory" | "defeat"; the battle's end is the server's own fact and this
    // only names it.
    expect(resolveOutcomeStatement('victory')).toEqual({ label: 'VICTORY', color: '#34d399' });
    expect(resolveOutcomeStatement('defeat')).toEqual({ label: 'DEFEAT', color: '#f87171' });

    // A value outside that pair is presented as itself rather than being mapped
    // onto one of the two (AGENTS.md §7 — no invented value).
    expect(resolveOutcomeStatement('abandoned').label).toBe('ABANDONED');
  });

  it('reads no turn, phase, or status member anywhere in the presenter', () => {
    // F-3 / C-1: the contract delivers no turn ownership and no battle phase, so
    // there is no member for the indicator to read. The presenter therefore
    // contains no such reference, and the vocabulary above is presentation only.
    for (const forbidden of [
      'activeTurn',
      'whoseTurn',
      'turnOwner',
      'isMyTurn',
      'currentPhase',
      'battleStatus',
      'phaseIndex',
    ]) {
      expect(
        presenterSource,
        `BattleEventPresenter must not read "${forbidden}"`
      ).not.toContain(forbidden);
    }

    // The phase classifier the indicator reuses reads exactly the two delivered
    // party strings and nothing else.
    expect(resolveDamagePresentationPhase('boss', 'player')).toBe('retaliation');
    expect(resolveDamagePresentationPhase('player', 'boss')).toBe('damage');
  });

  it('keeps the state push at exactly its nine documented members, with no phase or status field', () => {
    // SIGNALR_PROTOCOL.md §4: `BattleStateUpdated` carries the battle projection.
    // GAME_STATE.md §2.0.3 states §2.0 contains no `Status` field, and a lifecycle
    // field "must be introduced by its own design/ADR task, not added here" — so
    // neither the indicator nor anything else this task added may widen it.
    const body =
      /export interface RuntimeBattleState \{([\s\S]*?)\n\}/.exec(runtimeEventsSource)?.[1] ?? '';
    const members = [...body.matchAll(/^ {2}readonly (\w+)/gm)].map((match) => match[1]);

    expect(members).toEqual([
      'battleId',
      'turn',
      'sequence',
      'rngSeed',
      'rngState',
      'board',
      'playerState',
      'petState',
      'bossState',
    ]);

    for (const forbidden of ['phase', 'status', 'activeTurn', 'whoseTurn', 'turnOwner']) {
      expect(members, `BattleState must carry no "${forbidden}" member`).not.toContain(forbidden);
    }
  });

  it('accepts no turn or phase event the discriminator set does not define', () => {
    // GAME_EVENTS.md §1/§2 define `BattleStarted`, `TurnStarted`, `SwapStarted`,
    // `SwapResolved`, `MatchResolved` and `TurnEnded`, but SIGNALR_PROTOCOL.md
    // §3.2.2's closed set does not admit them and the implementation emits none
    // (TASK-246's C-1 — reported, not resolved here). The indicator therefore
    // cannot be sourced from a turn or phase event, and none is accepted.
    for (const type of [
      'BattleStarted',
      'TurnStarted',
      'TurnEnded',
      'SwapStarted',
      'SwapResolved',
      'MatchResolved',
      'PhaseChanged',
      'BossPhaseChanged',
      'BossTurnStarted',
    ]) {
      expect(parseInBattleEvent({ type, phase: 'boss' }), `${type} must stay ignored`).toBeNull();
    }
  });
});
