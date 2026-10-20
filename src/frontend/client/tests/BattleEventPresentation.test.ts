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
  adaptPresentedEvent,
  deliveredSwapOf,
  describeEventCallout,
  selectBatchCallout,
  selectBatchCalloutEvent,
  BOSS_SKILL_NAMES,
  resolveBossSkillDisplayName,
  BOSS_ENRAGE_THRESHOLDS,
  isBossEnraged,
  resolveDamagePresentationPhase,
  SWAP_REJECTION_MESSAGES,
  formatSwapRejection,
  CARD_CAST_REJECTION_MESSAGES,
  formatCardCastRejection,
  CALLOUT_BOSS_COLOR,
  INTERACTION_BOSS_ATTACK,
  INTERACTION_RESOLVING,
  INTERACTION_YOUR_TURN,
  resolveOutcomeStatement,
  MOVE_LOG_TITLE,
  formatMoveLogRow,
} from '../src/game/scenes/BattleEventPresenter';
import {
  GEM_MATCH_SUMMARY_TIMING,
  gemMatchSummaryBannerMs,
  gemMatchSummaryChips,
  gemMatchSummaryTitle,
} from '../src/game/presentation/GemMatchSummaryBanner';
import type {
  PresentedSwapStarted,
  PresentedSwapResolved,
  PresentedMatchCreated,
  PresentedCascadeCreated,
  PresentedComboChanged,
  PresentedGemMatched,
  PresentedDamageCalculated,
  PresentedDamageDealt,
  PresentedDamageTaken,
  PresentedPassiveCharged,
  PresentedPassiveTriggered,
  PresentedBossCardCast,
  PresentedRelicTriggered,
  PresentedPowerChanged,
} from '../src/game/scenes/BattleEventPresenter';

/**
 * Every line the scene renders **except** the developer Move Log's own declared
 * text objects.
 *
 * The Move Log is the one surface operator decision D-1 authorizes to render the
 * delivered turn/sequence and the raw event feed (TASK-253); the diagnostic
 * boundary that governs every other line is unchanged, so a scene-wide assertion
 * about what is *not* rendered subtracts exactly the panel's own objects — by the
 * single container D-1 declares them in, never by a pattern that could also hide a
 * regression.
 */
function playerFacingText(
  harness: { readonly texts: ReadonlyArray<{ readonly text: string; readonly containerIndex: number | null }> },
  ctx: object
): string {
  const logIndex = moveLogContainerIndex(ctx);

  return harness.texts
    .filter((entry) => logIndex === null || entry.containerIndex !== logIndex)
    .map((entry) => entry.text)
    .join('\n');
}

/**
 * The index of the developer Move Log's single declared container in a scene
 * context, or `null` when the surface does not exist.
 *
 * Every text object the panel draws is a child of that one container, which is how
 * a scene-wide assertion tells the one authorized developer surface from every
 * other line the scene renders (TASK-253 D-1).
 */
function moveLogContainerIndex(ctx: object): number | null {
  const log = (ctx as { developerMoveLog?: { layer?: { containerIndex?: number } } | null })
    .developerMoveLog;
  const index = log?.layer?.containerIndex;

  return typeof index === 'number' ? index : null;
}

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
  const texts: Array<{
    text: string;
    color?: string;
    /**
     * The container this object is a child of, as the harness numbered it, or
     * `null` while it belongs to none.
     *
     * Every text object the developer Move Log draws is a child of the panel's
     * single container, so this is what lets a scene-wide diagnostic assertion
     * subtract exactly that one authorized surface (TASK-253 D-1).
     */
    readonly containerIndex: number | null;
  }> = [];
  const boardInputHandlers = new Map<string, (pointer: { x: number; y: number }) => void>();
  const requestedActions: Array<Record<string, unknown>> = [];
  const sceneStarted: Array<{ key: string; data?: unknown }> = [];
  const createdRectangles: Array<{ kind: string; fill?: number }> = [];

  let currentBattleState = battleState;

  const runtime = {
    getState: () => state,
    onRuntimeEvent: (listener: (event: { state: GameRuntimeState }) => void) => {
      runtimeListeners.add(listener);
      return () => {
        runtimeListeners.delete(listener);
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
    /**
     * How many containers this scene run has created, so each one can be numbered
     * and the objects it owns identified by container membership.
     */
    let containerIndex = 0;

    const makeText = (value: string) => {
      const entry = {
        text: value,
        color: undefined as string | undefined,
        get containerIndex(): number | null {
          return containerIndex;
        },
      };
      texts.push(entry);
      let alpha = 1;
      /** Whether the object is drawn — the synchronization indicator is hidden in place. */
      let visible = true;
      /** The container this object was added to, as the harness numbered it. */
      let containerIndex: number | null = null;

      const obj = {
        kind: 'label' as const,
        label: value,
        setOrigin: () => obj,
        setPosition: () => obj,
        setFontSize: () => obj,
        setText: (next: string) => {
          entry.text = next;
          return obj;
        },
        setColor: (next: string) => {
          entry.color = next;
          return obj;
        },
        /** The container this object is a child of, or `null` while it is in none. */
        get containerIndex() {
          return containerIndex;
        },
        /** Set by the container that takes this object as a child. */
        set containerIndex(next: number | null) {
          containerIndex = next;
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
        /** Phaser's `GameObject#setVisible` — the sync indicator is shown/hidden in place. */
        setVisible: (next: boolean) => {
          visible = next;
          return obj;
        },
        get visible() {
          return visible;
        },
        destroy: () => {},
      };
      return obj;
    };

    const makeContainer = () => {
      const myIndex = ++containerIndex;
      const children: unknown[] = [];
      let size: { width: number; height: number } | null = null;
      const obj = {
        /** This container's own number, so a panel can be named by its container. */
        containerIndex: myIndex,
        add: (added: unknown) => {
          const list = Array.isArray(added) ? added : [added];
          children.push(...list);
          // Phaser reparents each child into the container; the mock records the
          // same fact, which is how a test tells a panel's own text objects from
          // every other line the scene draws (TASK-253 D-1).
          for (const child of list) {
            if (typeof child === 'object' && child !== null && 'containerIndex' in child) {
              (child as { containerIndex: number | null }).containerIndex = myIndex;
            }
          }
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
            setFillStyle: (nextFill: number) => {
              rect.fill = nextFill;
              return rect;
            },
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
      // Under SIGNALR_PROTOCOL.md §3.1 / §6 item 4, the state push precedes its batch.
      // Deliver the matching state first so the scene holds valid authoritative state for this resolution.
      if (
        envelope &&
        typeof envelope.battleId === 'string' &&
        typeof envelope.serverSequence === 'number' &&
        (currentBattleState === null ||
          currentBattleState.battleId !== envelope.battleId ||
          currentBattleState.sequence !== envelope.serverSequence)
      ) {
        currentBattleState = {
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
        } as unknown as RuntimeBattleState;

        for (const listener of battleStateListeners) {
          listener(currentBattleState);
        }
      }
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
  it('parses and formats SwapStarted with verbatim actor and cells', () => {
    // SIGNALR_PROTOCOL.md §3.2.26: the acting side and the two §1.0 cells the
    // accepted action named, all four members verbatim.
    const raw: PresentedSwapStarted = {
      type: 'SwapStarted',
      actor: 'boss',
      fromCell: 27,
      toCell: 28,
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('SwapStarted: boss 27->28');

    // Cell 0 is a real §1.0 index and is parsed, not treated as absent.
    expect(
      parseInBattleEvent({ type: 'SwapStarted', actor: 'player', fromCell: 0, toCell: 1 })
    ).toEqual({ type: 'SwapStarted', actor: 'player', fromCell: 0, toCell: 1 });
  });

  it('parses and formats SwapResolved with the delivered outcome verbatim', () => {
    // SIGNALR_PROTOCOL.md §3.2.27: the outcome word is read as sent and is not
    // validated against a client-side vocabulary — an outcome outside the
    // contract's two values is still reported verbatim rather than reinterpreted.
    const raw: PresentedSwapResolved = {
      type: 'SwapResolved',
      outcome: 'committed',
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('SwapResolved: committed');

    expect(
      parseInBattleEvent({ type: 'SwapResolved', outcome: 'reverted' })
    ).toEqual({ type: 'SwapResolved', outcome: 'reverted' });
  });

  it('parses and formats MatchCreated with verbatim members', () => {
    const raw: PresentedMatchCreated = {
      type: 'MatchCreated',
      shape: 'Lt',
      cells: [7, 15, 23, 24],
      gemType: 'POWER',
      cascadeDepth: 4,
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);

    const formatted = formatInBattleEvent(parsed!);
    expect(formatted).toContain('Lt');
    expect(formatted).toContain('POWER');
    expect(formatted).toContain('7, 15, 23, 24');
    expect(formatted).toContain('depth: 4');
    expect(formatted).toBe('Match: Lt POWER [7, 15, 23, 24] depth: 4');
  });

  it('parses a MatchCreated carrying a retired per-cell member as the ordinary shape', () => {
    // SIGNALR_PROTOCOL.md §3.2.6 item 6 / §3.2.11 / ADR-028: `createdSpecialGems`
    // is retired from the wire, so a Match event neither requires it nor projects
    // it. A tolerant reader accepts the delivered ordinary members and ignores the
    // retired one — a match resolves and is presented as the confirmed match it
    // is, with no creation presentation derived from it.
    const parsed = parseInBattleEvent({
      type: 'MatchCreated',
      shape: 'Straight',
      cells: [7, 15, 23, 31],
      gemType: 'ATK',
      cascadeDepth: 1,
      retiredCreationMember: [{ cellIndex: 15 }],
    });

    expect(parsed).toEqual({
      type: 'MatchCreated',
      shape: 'Straight',
      cells: [7, 15, 23, 31],
      gemType: 'ATK',
      cascadeDepth: 1,
    });
    expect(Object.keys(parsed!)).toEqual(['type', 'shape', 'cells', 'gemType', 'cascadeDepth']);
    expect(formatInBattleEvent(parsed!)).toBe('Match: Straight ATK [7, 15, 23, 31] depth: 1');
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

  it('parses and formats GemMatched with its two delivered members', () => {
    const raw: PresentedGemMatched = {
      type: 'GemMatched',
      cellIndex: 42,
      gemType: 'DEF',
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe('GemMatched: cell 42 (DEF)');
  });

  it('parses a GemMatched carrying a retired per-cell member as the ordinary shape', () => {
    // SIGNALR_PROTOCOL.md §3.2.9 item 3 / §3.2.10 / ADR-028: `specialGem` is
    // retired from the wire, so an ordinary matched-cell report is the whole
    // event. A tolerant reader ignores the retired member and the presenter
    // therefore raises no activation/consumption presentation for it.
    const parsed = parseInBattleEvent({
      type: 'GemMatched',
      cellIndex: 19,
      gemType: 'ATK',
      retiredCellMember: { type: 'Retired' },
    });

    expect(parsed).toEqual({ type: 'GemMatched', cellIndex: 19, gemType: 'ATK' });
    expect(Object.keys(parsed!)).toEqual(['type', 'cellIndex', 'gemType']);
    expect(formatInBattleEvent(parsed!)).toBe('GemMatched: cell 19 (ATK)');
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

  it('parses and formats BossCardCast with verbatim cardId and sourceId (SIGNALR_PROTOCOL.md §3.2.30)', () => {
    // The Boss's one selected action for the response phase: the Boss Card's
    // definition identity (the ability SkillId) and the Boss's canonical Identity,
    // both verbatim.
    const raw: PresentedBossCardCast = {
      type: 'BossCardCast',
      cardId: 'earthquake',
      sourceId: 'boss-son-thach-ve',
    };

    const parsed = parseInBattleEvent(raw);
    expect(parsed).toEqual(raw);
    expect(formatInBattleEvent(parsed!)).toBe(
      'BossCardCast: earthquake by boss-son-thach-ve'
    );

    // The payload is exactly `{ type, cardId, sourceId }`: no cost, no Power value
    // and no cooldown member exists on it (§3.2.30 item 4), and a hypothetical one
    // delivered anyway is not read.
    expect(Object.keys(parsed as object).sort()).toEqual(['cardId', 'sourceId', 'type']);
    expect(
      parseInBattleEvent({
        type: 'BossCardCast',
        cardId: 'earthquake',
        sourceId: 'boss-son-thach-ve',
        cost: 30,
        power: 70,
        cooldown: 0,
      })
    ).toEqual(raw);
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

    // §3.2.26/§3.2.27: a boundary event whose required members are missing or
    // mistyped is malformed, so it presents nothing rather than opening a swap
    // step with a guessed actor or cell.
    expect(parseInBattleEvent({ type: 'SwapStarted' })).toBeNull();
    expect(parseInBattleEvent({ type: 'SwapStarted', actor: 'boss', fromCell: 3 })).toBeNull();
    expect(parseInBattleEvent({ type: 'SwapStarted', actor: '', fromCell: 3, toCell: 4 })).toBeNull();
    expect(parseInBattleEvent({ type: 'SwapStarted', actor: 1, fromCell: 3, toCell: 4 })).toBeNull();
    expect(
      parseInBattleEvent({ type: 'SwapStarted', actor: 'boss', fromCell: '3', toCell: 4 })
    ).toBeNull();
    expect(parseInBattleEvent({ type: 'SwapResolved' })).toBeNull();
    expect(parseInBattleEvent({ type: 'SwapResolved', outcome: '' })).toBeNull();
    expect(parseInBattleEvent({ type: 'SwapResolved', outcome: 1 })).toBeNull();
  });

  it('returns null for a malformed BossCardCast rather than guessing a member (§3.2.30 items 2–3)', () => {
    // Both members are required and must be non-empty strings: a cast that names no
    // Boss Card identity, or no Boss, is malformed and presents nothing.
    expect(parseInBattleEvent({ type: 'BossCardCast' })).toBeNull();
    expect(parseInBattleEvent({ type: 'BossCardCast', cardId: 'earthquake' })).toBeNull();
    expect(
      parseInBattleEvent({ type: 'BossCardCast', sourceId: 'boss-son-thach-ve' })
    ).toBeNull();
    expect(
      parseInBattleEvent({ type: 'BossCardCast', cardId: '', sourceId: 'boss-son-thach-ve' })
    ).toBeNull();
    expect(
      parseInBattleEvent({ type: 'BossCardCast', cardId: 'earthquake', sourceId: '' })
    ).toBeNull();
    expect(
      parseInBattleEvent({ type: 'BossCardCast', cardId: 'earthquake', sourceId: 7 })
    ).toBeNull();
    expect(
      parseInBattleEvent({ type: 'BossCardCast', cardId: 1, sourceId: 'boss-son-thach-ve' })
    ).toBeNull();

    // The tolerant-reader rule is untouched: admitting `BossCardCast` admits no
    // other name, and an unrecognised one is still ignored rather than coerced.
    expect(
      parseInBattleEvent({
        type: 'BossCardCastPending',
        cardId: 'earthquake',
        sourceId: 'boss-son-thach-ve',
      })
    ).toBeNull();
    expect(
      parseInBattleEvent({ type: 'CardCast', cardId: 'earthquake', sourceId: 'boss-son-thach-ve' })
    ).toEqual({ type: 'CardCast', cardId: 'earthquake' });
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
      'GemActivated',
      'StatusEffectUpdated',
      'BossHpChanged',
    ]) {
      expect(parseInBattleEvent({ type, board: [] }), `${type} must stay ignored`).toBeNull();
    }
  });
});

describe('ADR-027 — the delivered per-pass Gravity + Spawn report (SIGNALR_PROTOCOL.md §3.2.28)', () => {
  /** One well-formed item: a chained fall in one column and one refilled top cell. */
  const WELL_FORMED = {
    type: 'CascadeStepResolved',
    passDepth: 2,
    dropMoves: [
      { fromCell: 16, toCell: 24 },
      { fromCell: 8, toCell: 16 },
      { fromCell: 0, toCell: 8 },
    ],
    spawnedGems: [{ cellIndex: 0, gemType: 'DEF' }],
  };

  it('parses the pass depth, the movements and the spawns exactly as delivered', () => {
    expect(parseInBattleEvent(WELL_FORMED)).toEqual(WELL_FORMED);
  });

  it('reads an omitted array as empty, and an explicitly empty one the same way', () => {
    // §3.2.28 item 2: each array is omitted when it would be empty; both spellings
    // decode to the one statement "this step reported nothing of that kind".
    expect(parseInBattleEvent({ type: 'CascadeStepResolved', passDepth: 1 })).toEqual({
      type: 'CascadeStepResolved',
      passDepth: 1,
      dropMoves: [],
      spawnedGems: [],
    });

    expect(
      parseInBattleEvent({
        type: 'CascadeStepResolved',
        passDepth: 1,
        dropMoves: [],
        spawnedGems: [],
      })
    ).toEqual({
      type: 'CascadeStepResolved',
      passDepth: 1,
      dropMoves: [],
      spawnedGems: [],
    });
  });

  it('refuses a malformed item whole, so no part of it is ever applied', () => {
    const rejected: unknown[] = [
      { type: 'CascadeStepResolved' },
      { type: 'CascadeStepResolved', passDepth: 0 },
      { type: 'CascadeStepResolved', passDepth: 1.5 },
      { type: 'CascadeStepResolved', passDepth: '1' },
      // §3.2.28 item 3: both cells are integers inside the board, differ, and share a
      // column; one source and one destination each.
      { type: 'CascadeStepResolved', passDepth: 1, dropMoves: [{ fromCell: 64, toCell: 8 }] },
      { type: 'CascadeStepResolved', passDepth: 1, dropMoves: [{ fromCell: 0, toCell: 0 }] },
      { type: 'CascadeStepResolved', passDepth: 1, dropMoves: [{ fromCell: 0, toCell: 9 }] },
      {
        type: 'CascadeStepResolved',
        passDepth: 1,
        dropMoves: [
          { fromCell: 0, toCell: 8 },
          { fromCell: 1, toCell: 8 },
        ],
      },
      {
        type: 'CascadeStepResolved',
        passDepth: 1,
        dropMoves: [
          { fromCell: 0, toCell: 8 },
          { fromCell: 0, toCell: 16 },
        ],
      },
      { type: 'CascadeStepResolved', passDepth: 1, dropMoves: 'nope' },
      // §3.2.28 item 4: a §1.0 destination cell each, once, and one of the four
      // documented Gem types — never a Special Gem, never a type the contract omits.
      { type: 'CascadeStepResolved', passDepth: 1, spawnedGems: [{ cellIndex: 0, gemType: 'FIRE' }] },
      { type: 'CascadeStepResolved', passDepth: 1, spawnedGems: [{ cellIndex: -1, gemType: 'ATK' }] },
      {
        type: 'CascadeStepResolved',
        passDepth: 1,
        spawnedGems: [
          { cellIndex: 0, gemType: 'ATK' },
          { cellIndex: 0, gemType: 'DEF' },
        ],
      },
      { type: 'CascadeStepResolved', passDepth: 1, spawnedGems: [{ cellIndex: 8 }] },
      // §4.5 item 1: Spawn fills only what Gravity left empty, so a spawned Gem never
      // lands where a survivor fell.
      {
        type: 'CascadeStepResolved',
        passDepth: 1,
        dropMoves: [{ fromCell: 0, toCell: 8 }],
        spawnedGems: [{ cellIndex: 8, gemType: 'ATK' }],
      },
    ];

    for (const raw of rejected) {
      expect(parseInBattleEvent(raw), JSON.stringify(raw)).toBeNull();
    }
  });

  it('maps the report to the pass’s Fall step, attributed to its cycle’s actor', () => {
    const parsed = parseInBattleEvent(WELL_FORMED);
    expect(parsed).not.toBeNull();

    expect(adaptPresentedEvent(parsed!)).toEqual({
      actor: 'swap',
      action: 'gravity-drop',
      kind: 'gravity-drop',
      detail: 'gravity depth=2',
    });

    // The side is the delivered boundary's, exactly as every other board-cycle step's
    // is (SIGNALR_PROTOCOL.md §3.2.28 item 6: the item carries no `actor` of its own).
    expect(adaptPresentedEvent(parsed!, 'boss')?.actor).toBe('boss');

    // The developer record names the delivered members verbatim and computes nothing.
    expect(formatInBattleEvent(parsed!)).toBe(
      'CascadeStepResolved: pass 2 drops [16->24, 8->16, 0->8] spawns [0:DEF]'
    );
  });
});

describe('TASK-252 — the delivered event → (actor, action, kind) adapter', () => {
  /** The presenter's own source, with its prose removed. */
  const presenterSource = readFileSync(
    resolve(__dirname, '../src/game/scenes/BattleEventPresenter.ts'),
    'utf8'
  )
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:])\/\/.*$/gm, '$1');

  /**
   * One delivered payload per name of `SIGNALR_PROTOCOL.md` §3.2.2's closed
   * twenty-name discriminator set, in the order that section's table lists
   * them: eighteen in-battle names plus the two outcome names. The two outcome
   * names are delivered the same way but are not `InBattleServerEvent`s, so they
   * are asserted as "no in-battle presentation step of their own".
   */
  const DELIVERED_PAYLOADS: ReadonlyArray<{ readonly name: string; readonly event: unknown }> = [
    { name: 'SwapStarted', event: { type: 'SwapStarted', actor: 'boss', fromCell: 27, toCell: 28 } },
    { name: 'SwapResolved', event: { type: 'SwapResolved', outcome: 'committed' } },
    { name: 'MatchCreated', event: { type: 'MatchCreated', shape: 'Lt', cells: [7, 15, 23, 24], gemType: 'POWER', cascadeDepth: 4 } },
    { name: 'CascadeCreated', event: { type: 'CascadeCreated', cascadeDepth: 2 } },
    { name: 'ComboChanged', event: { type: 'ComboChanged', combo: 4 } },
    { name: 'GemMatched', event: { type: 'GemMatched', cellIndex: 42, gemType: 'DEF' } },
    {
      name: 'CascadeStepResolved',
      event: {
        type: 'CascadeStepResolved',
        passDepth: 1,
        dropMoves: [{ fromCell: 16, toCell: 24 }],
        spawnedGems: [{ cellIndex: 0, gemType: 'DEF' }],
      },
    },
    {
      name: 'MatchedGemSummary',
      event: {
        type: 'MatchedGemSummary',
        gems: [
          { gemType: 'ATK', count: 5 },
          { gemType: 'POWER', count: 2 },
        ],
      },
    },
    { name: 'DamageCalculated', event: { type: 'DamageCalculated', base: 137, comboModifier: 2.5, elementModifier: 1.25, otherModifiers: 1.1, defense: 43, finalDamage: 99 } },
    { name: 'DamageDealt', event: { type: 'DamageDealt', source: 'player', target: 'boss', amount: 99 } },
    { name: 'DamageTaken', event: { type: 'DamageTaken', source: 'boss', target: 'player', amount: 77 } },
    { name: 'PassiveCharged', event: { type: 'PassiveCharged', passiveId: 'pet-passive', source: 'pet', sourceId: 'pet-inst-1', progress: 1, threshold: 3 } },
    { name: 'PassiveTriggered', event: { type: 'PassiveTriggered', passiveId: 'pet-passive', source: 'pet', sourceId: 'pet-inst-1', progress: 3, threshold: 3 } },
    { name: 'BossCardCast', event: { type: 'BossCardCast', cardId: 'earthquake', sourceId: 'boss-son-thach-ve' } },
    { name: 'CardCast', event: { type: 'CardCast', cardId: 'card-shield' } },
    { name: 'PetSkillCast', event: { type: 'PetSkillCast', cardId: 'card-inferno' } },
    { name: 'RelicTriggered', event: { type: 'RelicTriggered', relicId: 'relic-instance-berserker-core' } },
    { name: 'PowerChanged', event: { type: 'PowerChanged', delta: 25, power: 45, source: 'card' } },
    { name: 'BattleWon', event: { type: 'BattleWon', outcome: 'victory', finalBossHp: 0, finalPlayerHp: 85 } },
    { name: 'BattleLost', event: { type: 'BattleLost', outcome: 'defeat', finalBossHp: 120, finalPlayerHp: 0 } },
  ];

  /** The closed discriminator set §3.2.2 fixes. */
  const DISCRIMINATOR_SET = DELIVERED_PAYLOADS.map((payload) => payload.name);

  it('keeps the closed discriminator set at exactly the twenty documented names', () => {
    expect(DISCRIMINATOR_SET).toEqual([
      'SwapStarted',
      'SwapResolved',
      'MatchCreated',
      'CascadeCreated',
      'ComboChanged',
      'GemMatched',
      'CascadeStepResolved',
      'MatchedGemSummary',
      'DamageCalculated',
      'DamageDealt',
      'DamageTaken',
      'PassiveCharged',
      'PassiveTriggered',
      'BossCardCast',
      'CardCast',
      'PetSkillCast',
      'RelicTriggered',
      'PowerChanged',
      'BattleWon',
      'BattleLost',
    ]);

    // The parser's own switch admits exactly the eighteen in-battle names — no
    // more (a phase, status or turn name would be a wire change) and no fewer.
    // `SwapStarted` and `SwapResolved` are the two ADR-026 / TASK-259 proposal
    // formalized by SIGNALR_PROTOCOL.md §3.2.26–§3.2.27, which GAME_EVENTS.md §1/§2
    // had already defined, `CascadeStepResolved` is the one the Operator Decision
    // approving TASK-278 A-1(i)/A-4 added (§3.2.28, ADR-027),
    // `MatchedGemSummary` is the per-board-cycle matched-Gem report §3.2.29 defines,
    // and `BossCardCast` is the Boss action event §3.2.30 tabulates.
    const parsedNames = DELIVERED_PAYLOADS.filter(
      (payload) => parseInBattleEvent(payload.event) !== null
    ).map((payload) => payload.name);

    expect(parsedNames).toEqual([
      'SwapStarted',
      'SwapResolved',
      'MatchCreated',
      'CascadeCreated',
      'ComboChanged',
      'GemMatched',
      'CascadeStepResolved',
      'MatchedGemSummary',
      'DamageCalculated',
      'DamageDealt',
      'DamageTaken',
      'PassiveCharged',
      'PassiveTriggered',
      'BossCardCast',
      'CardCast',
      'PetSkillCast',
      'RelicTriggered',
      'PowerChanged',
    ]);
  });

  it('maps every delivered in-battle event to its actor, action and phase kind', () => {
    const steps = new Map<string, { actor: string | null; action: string; kind: string }>();

    for (const payload of DELIVERED_PAYLOADS) {
      const parsed = parseInBattleEvent(payload.event);
      if (parsed === null) {
        continue;
      }

      // The board-cycle actor is threaded from the batch, so a board-cycle event
      // read on its own keeps the side-free `swap` answer — asserted here, and
      // against a delivered boundary in the batch-attribution tests.
      const step = adaptPresentedEvent(parsed);
      // `DamageCalculated` and `SwapResolved` are the two delivered in-battle
      // events with no presentation step of their own; they are asserted
      // separately below.
      if (payload.name === 'DamageCalculated' || payload.name === 'SwapResolved') {
        expect(step).toBeNull();
        continue;
      }

      expect(step, `${payload.name} must adapt`).not.toBeNull();
      steps.set(payload.name, {
        actor: step!.actor,
        action: step!.action,
        kind: step!.kind,
      });
    }

    expect(Object.fromEntries(steps)).toEqual({
      SwapStarted: { actor: 'boss', action: 'swap', kind: 'swap' },
      MatchCreated: { actor: 'swap', action: 'match', kind: 'match' },
      CascadeCreated: { actor: 'swap', action: 'cascade', kind: 'cascade' },
      ComboChanged: { actor: 'swap', action: 'combo', kind: 'feedback' },
      GemMatched: { actor: 'swap', action: 'match', kind: 'match' },
      CascadeStepResolved: { actor: 'swap', action: 'gravity-drop', kind: 'gravity-drop' },
      MatchedGemSummary: { actor: 'swap', action: 'gem-summary', kind: 'gem-summary' },
      DamageDealt: { actor: 'player', action: 'damage', kind: 'damage' },
      DamageTaken: { actor: 'boss', action: 'retaliation', kind: 'retaliation' },
      PassiveCharged: { actor: 'pet', action: 'passive-charge', kind: 'feedback' },
      PassiveTriggered: { actor: 'pet', action: 'passive-trigger', kind: 'feedback' },
      BossCardCast: { actor: 'boss', action: 'boss-card-cast', kind: 'boss-card' },
      CardCast: { actor: 'player', action: 'card-cast', kind: 'cast' },
      PetSkillCast: { actor: 'player', action: 'pet-skill-cast', kind: 'cast' },
      RelicTriggered: { actor: 'player', action: 'relic', kind: 'relic' },
      PowerChanged: { actor: 'power', action: 'power', kind: 'feedback' },
    });

    // Sixteen of the eighteen in-battle names adapt; the other two are
    // `DamageCalculated` and `SwapResolved`, which present nothing of their own.
    expect(steps.size).toBe(16);
  });

  it('attributes a board cycle to the actor of the delivered SwapStarted that opened it', () => {
    // SIGNALR_PROTOCOL.md §3.2.26 item 6 / GAME_EVENTS.md §1: the Match, Gem,
    // Cascade and Combo events of a cycle carry no party member, so the actor is
    // the one the boundary delivered — not the cycle's position in the batch and not
    // a client-held turn. Both cycles of one round are attributed this way, and the
    // side-free `swap` answer survives only for a cycle whose boundary was not
    // delivered.
    const playerCycle = { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 } as const;
    const bossCycle = playerCycle;

    expect(adaptPresentedEvent(playerCycle, 'player')?.actor).toBe('player');
    expect(adaptPresentedEvent(bossCycle, 'boss')?.actor).toBe('boss');

    // No boundary context: the honest side-free answer, unchanged from TASK-252.
    expect(adaptPresentedEvent(playerCycle)?.actor).toBe('swap');

    expect(adaptPresentedEvent({ type: 'CascadeCreated', cascadeDepth: 1 }, 'boss')?.actor).toBe('boss');
    expect(adaptPresentedEvent({ type: 'GemMatched', cellIndex: 3, gemType: 'HP' }, 'boss')?.actor).toBe('boss');
    expect(adaptPresentedEvent({ type: 'ComboChanged', combo: 2 }, 'boss')?.actor).toBe('boss');
  });

  it('reads one delivered board-action boundary through one projection', () => {
    // `deliveredSwapOf` is the single place the delivered boundary is projected, so
    // the swap step's actor and the swap animation's two cells cannot disagree. A
    // delivered actor word outside the contract's two values names no side.
    expect(deliveredSwapOf({ type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 })).toEqual({
      actor: 'player',
      fromCell: 20,
      toCell: 21,
    });

    expect(deliveredSwapOf({ type: 'SwapStarted', actor: 'boss', fromCell: 3, toCell: 4 })).toEqual({
      actor: 'boss',
      fromCell: 3,
      toCell: 4,
    });

    expect(
      deliveredSwapOf({ type: 'SwapStarted', actor: 'third-party', fromCell: 3, toCell: 4 })
    ).toEqual({ actor: null, fromCell: 3, toCell: 4 });

    // Not a boundary at all: no swap step, no cells, no actor.
    expect(deliveredSwapOf({ type: 'ComboChanged', combo: 2 })).toBeNull();
    expect(deliveredSwapOf({ type: 'SwapResolved', outcome: 'committed' })).toBeNull();
  });

  it('presents nothing of its own for DamageCalculated and SwapResolved, and no in-battle step for either outcome', () => {
    // The six pipeline factors are developer detail: the resulting number reaches
    // the player as the damage floater and as the HP readout.
    expect(adaptPresentedEvent(parseInBattleEvent({ type: 'DamageCalculated', base: 1, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 1 })!)).toBeNull();

    // `SwapResolved` closes the exchange its own `SwapStarted` already opened
    // (MATCH3_RULES.md §2.1.6 steps 3 and 7), so it opens no step of its own
    // whatever context it is read with — its delivered outcome is not a second
    // swap to present.
    const resolved = parseInBattleEvent({ type: 'SwapResolved', outcome: 'committed' });
    expect(resolved).not.toBeNull();
    expect(adaptPresentedEvent(resolved!)).toBeNull();
    expect(adaptPresentedEvent(resolved!, 'boss')).toBeNull();

    // The two terminal outcomes are not in-battle events; the scene finds them on
    // the envelope instead (§3.2.19) and hands the battle off.
    for (const name of ['BattleWon', 'BattleLost']) {
      expect(parseInBattleEvent(DELIVERED_PAYLOADS.find((p) => p.name === name)!.event)).toBeNull();
    }
  });

  it('attributes each Passive to the entity its delivered source names', () => {
    for (const [source, actor] of [
      ['pet', 'pet'],
      ['boss', 'boss'],
    ] as const) {
      const charged = adaptPresentedEvent({
        type: 'PassiveCharged',
        passiveId: 'p',
        source,
        sourceId: 'owner-1',
        progress: 1,
        threshold: 2,
      });
      const triggered = adaptPresentedEvent({
        type: 'PassiveTriggered',
        passiveId: 'p',
        source,
        sourceId: 'owner-1',
        progress: 2,
        threshold: 2,
      });

      expect(charged?.actor).toBe(actor);
      expect(triggered?.actor).toBe(actor);
    }

    // A source outside the documented pair names no actor this contract defines,
    // so none is claimed (AGENTS.md §7/§10).
    expect(
      adaptPresentedEvent({
        type: 'PassiveCharged',
        passiveId: 'p',
        source: 'third-party',
        sourceId: 'x',
        progress: 1,
        threshold: 2,
      })?.actor
    ).toBeNull();
  });

  it('claims no actor for a damage direction the contract does not define', () => {
    for (const [source, target] of [
      ['player', 'player'],
      ['boss', 'boss'],
      ['third-party', 'boss'],
      ['boss', 'third-party'],
    ] as const) {
      const step = adaptPresentedEvent({ type: 'DamageDealt', source, target, amount: 10 });
      expect(step?.actor, `${source}->${target}`).toBeNull();
      expect(step?.kind).toBe('feedback');
    }
  });

  it('reads no member outside the delivered payload, whatever extra members arrive', () => {
    // A hypothetical `phase` / `status` / `role` / `turn` member changes nothing:
    // the adapter reads only the documented members and derives the actor from
    // them, so no wire widening can leak into the presentation model.
    const widened = adaptPresentedEvent({
      type: 'CardCast',
      cardId: 'card-shield',
      phase: 'boss',
      status: 'active',
      role: 'boss',
      turn: 7,
    } as unknown as Parameters<typeof adaptPresentedEvent>[0]);
    const plain = adaptPresentedEvent({ type: 'CardCast', cardId: 'card-shield' });

    expect(widened).toEqual(plain);
    expect(widened?.actor).toBe('player');

    // And the same for a PowerChanged whose extra members name a side.
    expect(
      adaptPresentedEvent({
        type: 'PowerChanged',
        delta: -5,
        power: 10,
        source: 'boss',
        target: 'player',
        party: 'boss',
      } as unknown as Parameters<typeof adaptPresentedEvent>[0])?.actor
    ).toBe('power');
  });

  it('applies one adapter and no per-actor branch anywhere in the presenter', () => {
    const source = presenterSource;

    // Exactly one adapter, and one damage classifier that the adapter reuses
    // rather than a second one of its own.
    expect(source.match(/export function adaptPresentedEvent\(/g) ?? []).toHaveLength(1);
    expect(source.match(/export function resolveDamagePresentationPhase\(/g) ?? []).toHaveLength(1);
    expect(source).toContain('resolveDamagePresentationPhase(event.source, event.target)');

    // No second classifier, pipeline, registry or per-actor switch.
    for (const forbidden of [
      "actor === 'boss'",
      'actor === "boss"',
      "actor === 'player'",
      'switch (actor',
      'switch (step.actor',
      'ActorPipeline',
      'actorRegistry',
      'actorHandlers',
    ]) {
      expect(source, `BattleEventPresenter must not contain "${forbidden}"`).not.toContain(forbidden);
    }
  });
});

describe('TASK-275 — the turn banner is scene presentation, never a delivered step', () => {
  /** The presenter's own source, with its prose removed (TASK-252's reader). */
  const presenterSource = readFileSync(
    resolve(__dirname, '../src/game/scenes/BattleEventPresenter.ts'),
    'utf8'
  )
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:])\/\/.*$/gm, '$1');

  /** One delivered payload per in-battle name of the closed discriminator set. */
  const IN_BATTLE_EVENTS: readonly unknown[] = [
    { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
    { type: 'SwapResolved', outcome: 'committed' },
    { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
    { type: 'CascadeCreated', cascadeDepth: 1 },
    { type: 'ComboChanged', combo: 2 },
    { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
    { type: 'DamageCalculated', base: 100, comboModifier: 1, elementModifier: 1, otherModifiers: 1, defense: 0, finalDamage: 100 },
    { type: 'DamageDealt', source: 'player', target: 'boss', amount: 100 },
    { type: 'DamageTaken', source: 'player', target: 'boss', amount: 100 },
    { type: 'PassiveCharged', passiveId: 'pet-passive', source: 'pet', sourceId: 'pet-inst-1', progress: 1, threshold: 3 },
    { type: 'PassiveTriggered', passiveId: 'pet-passive', source: 'pet', sourceId: 'pet-inst-1', progress: 3, threshold: 3 },
    { type: 'BossCardCast', cardId: 'earthquake', sourceId: 'boss-son-thach-ve' },
    { type: 'CardCast', cardId: 'card-shield' },
    { type: 'PetSkillCast', cardId: 'card-inferno' },
    { type: 'RelicTriggered', relicId: 'relic-instance-berserker-core' },
    { type: 'PowerChanged', delta: 25, power: 45, source: 'card' },
  ];

  it('maps no delivered event to the banner phase or the turn-banner action', () => {
    // The banner is a grouping the **scene** opens from a delivered board-action
    // boundary; no event adapts into it. If one did, a banner would appear for an
    // event the contract never made a turn boundary (TASK-275 §6.6).
    for (const event of IN_BATTLE_EVENTS) {
      const parsed = parseInBattleEvent(event);
      expect(parsed, JSON.stringify(event)).not.toBeNull();

      const step = adaptPresentedEvent(parsed as Parameters<typeof adaptPresentedEvent>[0], 'player');
      expect(step?.kind, JSON.stringify(event)).not.toBe('banner');
      expect(step?.action, JSON.stringify(event)).not.toBe('turn-banner');
    }
  });

  it('only names the banner vocabulary: the presenter derives it from no delivered member', () => {
    // One phase name and one action name, and neither is produced by the adapter —
    // the side a banner labels is read from `SwapStarted.actor` by the scene
    // (`deliveredSwapOf`, the contract's own two values `DELIVERED_SWAP_ACTORS`).
    expect(presenterSource.match(/'banner'/g) ?? []).toHaveLength(1);
    expect(presenterSource.match(/'turn-banner'/g) ?? []).toHaveLength(1);

    // No turn or phase member is introduced or read: the wire carries none
    // (`SIGNALR_PROTOCOL.md` §3.2.2 item 2, TASK-246 C-1).
    for (const forbidden of ['turnStarted', 'turnEnded', 'turnChanged', 'whoseTurn', 'activeTurn']) {
      expect(presenterSource, `BattleEventPresenter must not read "${forbidden}"`).not.toContain(
        forbidden
      );
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

  it('names a cast from the loaded definition', () => {
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

  it('names the delivered event the batch callout was selected from (TASK-254)', () => {
    // The callout is presented with the phase that owns the event it reports, so
    // the selection has to carry that event. The rule itself is unchanged — the
    // same importance ladder and the same delivery-order tie-break as
    // `selectBatchCallout`, which is its callout-only projection.
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
      parseInBattleEvent({ type: 'BossCardCast', cardId: 'flame-burst', sourceId: 'boss-hoa-long' })!,
      parseInBattleEvent({ type: 'DamageDealt', source: 'player', target: 'boss', amount: 120 })!,
    ];

    const selection = selectBatchCalloutEvent(events, resolveCardName, resolveRelicName);
    expect(selection?.callout.message).toBe('COMBO ×4');
    // The event it names is the delivered event that produced that line, and the
    // callout-only projection selects exactly the same line.
    expect(selection?.event).toBe(events[1]);
    expect(selectBatchCallout(events, resolveCardName, resolveRelicName)).toEqual(
      selection?.callout
    );

    // Among equally important events the last delivered one wins, and it is that
    // event that owns the line.
    const twoCasts = [
      parseInBattleEvent({ type: 'CardCast', cardId: 'card-heal' })!,
      parseInBattleEvent({ type: 'PetSkillCast', cardId: 'card-inferno' })!,
    ];
    const last = selectBatchCalloutEvent(twoCasts, resolveCardName, resolveRelicName);
    expect(last?.callout.message).toBe('PET SKILL: card-inferno');
    expect(last?.event).toBe(twoCasts[1]);

    // Nothing a player needs told: no callout and therefore no owning event — and
    // no event is invented for the line that is not presented.
    expect(
      selectBatchCalloutEvent(
        [
          parseInBattleEvent({ type: 'GemMatched', cellIndex: 8, gemType: 'ATK' })!,
          parseInBattleEvent({ type: 'CascadeCreated', cascadeDepth: 3 })!,
        ],
        resolveCardName,
        resolveRelicName
      )
    ).toBeNull();
    expect(selectBatchCalloutEvent([], resolveCardName, resolveRelicName)).toBeNull();
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
      parseInBattleEvent({ type: 'BossCardCast', cardId: 'flame-burst', sourceId: 'boss-hoa-long' })!,
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
      },
      expectedTokens: ['Lt', 'POWER', '7, 15, 23, 24', 'depth: 4'],
    },
    {
      name: 'GemMatched',
      event: {
        type: 'GemMatched',
        cellIndex: 42,
        gemType: 'DEF',
      },
      expectedTokens: ['cell 42', 'DEF'],
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
      name: 'BossCardCast',
      event: {
        type: 'BossCardCast',
        cardId: 'earthquake',
        sourceId: 'boss-son-thach-ve',
      },
      expectedTokens: ['earthquake', 'boss-son-thach-ve'],
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

    // And none of that raw line reaches the player-facing HUD. The developer Move
    // Log is the one surface D-1 authorizes to render the raw feed (TASK-253), so
    // it is excluded by its own declared object name and every other line is not.
    const rendered = playerFacingText(harness, ctx);
    expect(rendered).not.toContain(presented[0]);
    expect(harness.texts.map((entry) => entry.text).join('\n')).toContain(presented[0]);
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
          type: 'BossCardCast',
          cardId: 'flame-burst',
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
      .filter((value) => /^(MATCH|COMBO ×\d+|CARD: |PET SKILL: |BOSS CARD|RELIC: |PASSIVE)/.test(value));
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

    const rendered = playerFacingText(harness, ctx);
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
    const rendered = playerFacingText(harness, ctx);
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

    const rendered = playerFacingText(harness, ctx);
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

    const rendered = playerFacingText(harness, ctx);
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

    it('maps a BossCardCast to BOSS CARD through the same canonical display-name resolver (§3.2.30)', () => {
      // The Boss Card's definition identity IS the ability `SkillId` (§3.2.30 item
      // 2), so the one transcription BOSS_RULES.md §6.3/§6.4 owns names it. This
      // asserts the reuse: no second name table exists for the Boss Card form.
      for (const { cardId, expectedName } of [
        { cardId: 'flame-burst', expectedName: 'Flame Burst' },
        { cardId: 'drain-power', expectedName: 'Drain Power' },
        { cardId: 'root', expectedName: 'Root' },
        { cardId: 'earthquake', expectedName: 'Earthquake' },
        { cardId: 'thunder-strike', expectedName: 'Thunder Strike' },
      ]) {
        expect(BOSS_SKILL_NAMES[cardId]).toBe(expectedName);
        expect(resolveBossSkillDisplayName(cardId)).toBe(expectedName);

        const callout = describeEventCallout(
          parseInBattleEvent({ type: 'BossCardCast', cardId, sourceId: 'boss-boss-id' })!,
          resolveCardName,
          resolveRelicName
        );

        expect(callout).toEqual({
          message: `BOSS CARD: ${expectedName}`,
          color: '#f87171',
          priority: 3,
        });
      }
    });

    it('falls back safely to BOSS CARD when the Boss Card identity is unknown', () => {
      expect(resolveBossSkillDisplayName('unknown-card')).toBeNull();

      const callout = describeEventCallout(
        parseInBattleEvent({ type: 'BossCardCast', cardId: 'unknown-card', sourceId: 'boss-test' })!,
        resolveCardName,
        resolveRelicName
      );

      expect(callout).toEqual({
        message: 'BOSS CARD',
        color: '#f87171',
        priority: 3,
      });
      // The technical identity is never printed: the callout says what happened.
      expect(callout?.message).not.toContain('unknown-card');
    });

    it('presents the delivered BossCardCast on the Boss band callout row, from its own event', async () => {
      // The Boss's own delivered step: the scene presents the cast its own event
      // reports — the canonical name, never the technical `sourceId` — and reads no
      // cost, Power or cooldown from it (SIGNALR_PROTOCOL.md §3.2.30 item 4).
      const { harness, scene, ctx } = createBattle();
      runScene(scene, ctx, 'create');
      await flush();

      harness.emitBattleEvents({
        battleId: 'b-boss-card',
        serverSequence: 6,
        events: [{ type: 'BossCardCast', cardId: 'earthquake', sourceId: 'boss-son-thach-ve' }],
      });

      const rendered = playerFacingText(harness, ctx);
      expect(rendered).toContain('BOSS CARD: Earthquake');
      expect(rendered).not.toContain('boss-son-thach-ve');

      // The developer Move Log still records the delivered members verbatim.
      expect(harness.texts.map((entry) => entry.text).join('\n')).toContain(
        'BossCardCast: earthquake by boss-son-thach-ve'
      );
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
      expect(formatSwapRejection('INVALID_ACTION_PAYLOAD')).toBe('Action payload does not match previous submission.');
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
    // `GAME_EVENTS.md` §1/§2 define `BattleStarted`, `TurnStarted`, `TurnEnded`,
    // `MatchResolved` and `PhaseChanged`-style names, but `SIGNALR_PROTOCOL.md`
    // §3.2.2's closed set does not admit them and the implementation emits none
    // (TASK-246's C-1 — reported, not resolved here). The indicator therefore
    // cannot be sourced from a turn or phase event, and none is accepted.
    //
    // `SwapStarted` and `SwapResolved` are **not** in this list any more: this task
    // formalized them into §3.2.26–§3.2.27, so they are admitted — and they are
    // still not a turn or phase signal: neither carries a `turn`, a `phase`, or
    // any "whose turn it is" member (`§3.2.26` item 7, `§3.2.27` item 5).
    for (const type of [
      'BattleStarted',
      'TurnStarted',
      'TurnEnded',
      'MatchResolved',
      'PhaseChanged',
      'BossPhaseChanged',
      'BossTurnStarted',
    ]) {
      expect(parseInBattleEvent({ type, phase: 'boss' }), `${type} must stay ignored`).toBeNull();
    }
  });
});

describe('TASK-253 — the developer Move Log row formatter (operator decision D-1)', () => {
  /** The presenter's own source, with its prose removed. */
  const presenterSource = readFileSync(
    resolve(__dirname, '../src/game/scenes/BattleEventPresenter.ts'),
    'utf8'
  )
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:])\/\/.*$/gm, '$1');

  /** The eighteen non-outcome names of the closed discriminator set (§3.2.2). */
  const IN_BATTLE_TYPES = [
    'SwapStarted',
    'SwapResolved',
    'MatchCreated',
    'CascadeCreated',
    'ComboChanged',
    'GemMatched',
    'CascadeStepResolved',
    'MatchedGemSummary',
    'DamageCalculated',
    'DamageDealt',
    'DamageTaken',
    'PassiveCharged',
    'PassiveTriggered',
    'BossCardCast',
    'CardCast',
    'PetSkillCast',
    'RelicTriggered',
    'PowerChanged',
  ];

  /** One delivered payload per in-battle discriminator. */
  const DELIVERED: ReadonlyArray<{ readonly name: string; readonly event: unknown }> = [
    { name: 'SwapStarted', event: { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 } },
    { name: 'SwapResolved', event: { type: 'SwapResolved', outcome: 'committed' } },
    { name: 'MatchCreated', event: { type: 'MatchCreated', shape: 'Lt', cells: [7, 15, 23], gemType: 'POWER', cascadeDepth: 4 } },
    { name: 'CascadeCreated', event: { type: 'CascadeCreated', cascadeDepth: 2 } },
    { name: 'ComboChanged', event: { type: 'ComboChanged', combo: 4 } },
    { name: 'GemMatched', event: { type: 'GemMatched', cellIndex: 42, gemType: 'DEF' } },
    {
      name: 'CascadeStepResolved',
      event: {
        type: 'CascadeStepResolved',
        passDepth: 1,
        dropMoves: [{ fromCell: 16, toCell: 24 }],
        spawnedGems: [{ cellIndex: 0, gemType: 'DEF' }],
      },
    },
    {
      name: 'MatchedGemSummary',
      event: {
        type: 'MatchedGemSummary',
        gems: [
          { gemType: 'DEF', count: 4 },
          { gemType: 'HP', count: 1 },
        ],
      },
    },
    { name: 'DamageCalculated', event: { type: 'DamageCalculated', base: 137, comboModifier: 2.5, elementModifier: 1.25, otherModifiers: 1.1, defense: 43, finalDamage: 99 } },
    { name: 'DamageDealt', event: { type: 'DamageDealt', source: 'player', target: 'boss', amount: 99 } },
    { name: 'DamageTaken', event: { type: 'DamageTaken', source: 'boss', target: 'player', amount: 77 } },
    { name: 'PassiveCharged', event: { type: 'PassiveCharged', passiveId: 'pet-passive', source: 'pet', sourceId: 'pet-inst-1', progress: 1, threshold: 3 } },
    { name: 'PassiveTriggered', event: { type: 'PassiveTriggered', passiveId: 'boss-rage', source: 'boss', sourceId: 'boss-1', progress: 3, threshold: 3 } },
    { name: 'BossCardCast', event: { type: 'BossCardCast', cardId: 'earthquake', sourceId: 'boss-son-thach-ve' } },
    { name: 'CardCast', event: { type: 'CardCast', cardId: 'card-shield' } },
    { name: 'PetSkillCast', event: { type: 'PetSkillCast', cardId: 'card-inferno' } },
    { name: 'RelicTriggered', event: { type: 'RelicTriggered', relicId: 'relic-instance-berserker-core' } },
    { name: 'PowerChanged', event: { type: 'PowerChanged', delta: 25, power: 45, source: 'card' } },
  ];

  /** One event row, built the way the scene builds it. */
  function eventRow(name: string, turn: number | null = 4, ordinal = 1, sequence = 9): string {
    const event = parseInBattleEvent(DELIVERED.find((payload) => payload.name === name)?.event);
    expect(event, `${name} must parse`).not.toBeNull();

    return formatMoveLogRow({
      kind: 'event',
      ordinal,
      sequence,
      turn,
      step: adaptPresentedEvent(event as never),
      event: event as never,
    });
  }

  it('names the delivered discriminator and the adapter actor/action, then the delivered members', () => {
    expect(eventRow('SwapStarted')).toBe(
      '#1 seq=9 turn=4 type=SwapStarted player/swap | SwapStarted: player 20->21'
    );
    // `SwapResolved` opens no step of its own (its `SwapStarted` already opened the
    // exchange), so the row claims neither an actor nor an action — the same
    // `-/-` the other step-less events carry.
    expect(eventRow('SwapResolved')).toBe(
      '#1 seq=9 turn=4 type=SwapResolved -/- | SwapResolved: committed'
    );
    expect(eventRow('MatchCreated')).toBe(
      '#1 seq=9 turn=4 type=MatchCreated swap/match | Match: Lt POWER [7, 15, 23] depth: 4'
    );
    expect(eventRow('BossCardCast')).toBe(
      '#1 seq=9 turn=4 type=BossCardCast boss/boss-card-cast | BossCardCast: earthquake by boss-son-thach-ve'
    );
    expect(eventRow('CardCast')).toBe(
      '#1 seq=9 turn=4 type=CardCast player/card-cast | CardCast: card-shield'
    );
    expect(eventRow('PowerChanged')).toBe(
      '#1 seq=9 turn=4 type=PowerChanged power/power | PowerChanged: +25 -> 45 (card)'
    );
    expect(eventRow('CascadeStepResolved')).toBe(
      '#1 seq=9 turn=4 type=CascadeStepResolved swap/gravity-drop | CascadeStepResolved: pass 1 drops [16->24] spawns [0:DEF]'
    );
    expect(eventRow('MatchedGemSummary')).toBe(
      '#1 seq=9 turn=4 type=MatchedGemSummary swap/gem-summary | MatchedGemSummary: DEF x4, HP x1'
    );
    expect(eventRow('DamageTaken')).toBe(
      '#1 seq=9 turn=4 type=DamageTaken boss/retaliation | DamageTaken: 77 on player (from boss)'
    );
  });

  it('names only a discriminator of the closed set, and claims no actor for an event that names none', () => {
    const rows = DELIVERED.map((payload) => eventRow(payload.name));

    const named = rows.map((row) => /type=([A-Za-z]+)/.exec(row)?.[1] ?? '');
    expect(named).toEqual(IN_BATTLE_TYPES);
    expect(named.every((name) => IN_BATTLE_TYPES.includes(name))).toBe(true);

    // `DamageCalculated` presents nothing of its own, so the adapter names no
    // actor/action for it — and the row claims neither rather than guessing.
    expect(eventRow('DamageCalculated')).toContain('type=DamageCalculated -/-');

    // The `turn` is `-` when no `BattleStateUpdated` has been delivered: the row
    // never invents one.
    expect(eventRow('ComboChanged', null)).toContain(' turn=- type=ComboChanged');
  });

  it('records the client lifecycle verbs without inventing a duration or a value', () => {
    expect(
      formatMoveLogRow({
        kind: 'lifecycle',
        ordinal: 3,
        scope: 'timeline',
        state: 'queued',
        sequence: 12,
        detail: 'phases=2 match,settle',
      })
    ).toBe('#3 timeline queued phases=2 match,settle seq=12');

    expect(
      formatMoveLogRow({
        kind: 'lifecycle',
        ordinal: 4,
        scope: 'timeline',
        state: 'animating',
        sequence: 12,
        detail: 'state=presenting',
      })
    ).toBe('#4 timeline animating state=presenting seq=12');

    expect(
      formatMoveLogRow({
        kind: 'lifecycle',
        ordinal: 5,
        scope: 'phase',
        state: 'animating',
        sequence: 12,
        detail: '1/2 match match depth=1',
      })
    ).toBe('#5 phase animating 1/2 match match depth=1 seq=12');

    expect(
      formatMoveLogRow({
        kind: 'lifecycle',
        ordinal: 6,
        scope: 'phase',
        state: 'completed',
        sequence: 12,
        detail: '1/2 match match depth=1',
      })
    ).toBe('#6 phase completed 1/2 match match depth=1 seq=12');

    expect(
      formatMoveLogRow({
        kind: 'lifecycle',
        ordinal: 7,
        scope: 'timeline',
        state: 'aborted',
        sequence: 12,
        detail: 'superseded-state',
      })
    ).toBe('#7 timeline aborted superseded-state seq=12');

    expect(
      formatMoveLogRow({
        kind: 'lifecycle',
        ordinal: 8,
        scope: 'timeline',
        state: 'failed',
        sequence: 12,
        detail: 'malformed-batch',
      })
    ).toBe('#8 timeline failed malformed-batch seq=12');

    // A lifecycle row with no resolution to name says so rather than printing one.
    expect(
      formatMoveLogRow({
        kind: 'lifecycle',
        ordinal: 9,
        scope: 'timeline',
        state: 'completed',
        sequence: null,
        detail: '',
      })
    ).toBe('#9 timeline completed');
  });

  it('records the terminal result from the delivered outcome members', () => {
    expect(
      formatMoveLogRow({
        kind: 'outcome',
        ordinal: 11,
        outcome: 'victory',
        finalBossHp: 0,
        finalPlayerHp: 85,
      })
    ).toBe('#11 outcome victory bossHp=0 playerHp=85');
  });

  it('reuses the one event formatter and adds no second event vocabulary', () => {
    // The row's event text is `formatInBattleEvent`'s output, so the delivered
    // members are spelled exactly once in the codebase.
    expect(presenterSource).toContain('formatInBattleEvent(row.event)');
    // One row formatter, and no arithmetic on a delivered value.
    expect(presenterSource.match(/export function formatMoveLogRow\(/g)).toHaveLength(1);
    expect(presenterSource).not.toMatch(/finalDamage\s*[-+*/]/);
  });

  it('declares the panel\'s caption, which names no wire member', () => {
    expect(MOVE_LOG_TITLE).toBe('MOVE LOG');
  });
});

describe('MatchedGemSummary — the delivered per-cycle gem counts (SIGNALR_PROTOCOL.md §3.2.29)', () => {
  /** One delivered item, as the projection writes it (§3.2.29's example shape). */
  const DELIVERED_SUMMARY = {
    type: 'MatchedGemSummary',
    gems: [
      { gemType: 'ATK', count: 5 },
      { gemType: 'HP', count: 2 },
    ],
  };

  /** The board's Gem palette, as the scene supplies it to the component. */
  const PALETTE = {
    ATK: { color: 0xef4444, label: 'ATK' },
    DEF: { color: 0x3b82f6, label: 'DEF' },
    HP: { color: 0x22c55e, label: 'HP' },
    POWER: { color: 0xa855f7, label: 'PWR' },
  };

  // =======================================================================
  // Parser
  // =======================================================================

  it('parses the delivered entries as sent, in delivered order', () => {
    const parsed = parseInBattleEvent(DELIVERED_SUMMARY);

    expect(parsed).toEqual({
      type: 'MatchedGemSummary',
      gems: [
        { gemType: 'ATK', count: 5 },
        { gemType: 'HP', count: 2 },
      ],
    });
  });

  it('reads an omitted `gems` member as "reported, no type to show"', () => {
    // §3.2.29 item 2: the member is omitted only when the cycle matched no Gem
    // type, and an empty array is never written. The parser accepts the omission
    // without inventing a count.
    expect(parseInBattleEvent({ type: 'MatchedGemSummary' })).toEqual({
      type: 'MatchedGemSummary',
      gems: [],
    });
  });

  it('rejects a malformed item whole rather than repairing or partially applying it', () => {
    // §3.2.29 items 3–4: an entry is one of the four types with an integer count of
    // at least one, once per type, in the fixed order. Every violation below is a
    // malformed item, and a malformed item presents nothing (§3.2.29 item 8).
    const malformed: readonly unknown[] = [
      { type: 'MatchedGemSummary', gems: 'ATK' },
      { type: 'MatchedGemSummary', gems: null },
      { type: 'MatchedGemSummary', gems: [{ gemType: 'ATK', count: 0 }] },
      { type: 'MatchedGemSummary', gems: [{ gemType: 'ATK', count: -1 }] },
      { type: 'MatchedGemSummary', gems: [{ gemType: 'ATK', count: 1.5 }] },
      { type: 'MatchedGemSummary', gems: [{ gemType: 'ATK' }] },
      { type: 'MatchedGemSummary', gems: [{ gemType: 'GOLD', count: 2 }] },
      { type: 'MatchedGemSummary', gems: [{ count: 2 }] },
      {
        type: 'MatchedGemSummary',
        gems: [
          { gemType: 'ATK', count: 1 },
          { gemType: 'ATK', count: 2 },
        ],
      },
      {
        type: 'MatchedGemSummary',
        gems: [
          { gemType: 'HP', count: 1 },
          { gemType: 'ATK', count: 2 },
        ],
      },
      { type: 'MatchedGemSummary', gems: [null] },
    ];

    for (const item of malformed) {
      expect(parseInBattleEvent(item), JSON.stringify(item)).toBeNull();
    }
  });

  // =======================================================================
  // Adapter
  // =======================================================================

  it('opens one step of its own, attributed to the cycle’s delivered actor', () => {
    const parsed = parseInBattleEvent(DELIVERED_SUMMARY);

    expect(adaptPresentedEvent(parsed!, 'player')).toEqual({
      actor: 'player',
      action: 'gem-summary',
      kind: 'gem-summary',
      detail: 'gem summary',
    });

    expect(adaptPresentedEvent(parsed!, 'boss')?.actor).toBe('boss');

    // No boundary context: the side-free answer, exactly as every other board-cycle
    // event gives it. The side is the delivered `SwapStarted.actor`, never a member
    // on this item (§3.2.29 item 6).
    expect(adaptPresentedEvent(parsed!)?.actor).toBe('swap');
  });

  it('presents nothing for an item that names no Gem type', () => {
    const parsed = parseInBattleEvent({ type: 'MatchedGemSummary' });

    expect(parsed).not.toBeNull();
    expect(adaptPresentedEvent(parsed!)).toBeNull();
    expect(adaptPresentedEvent(parsed!, 'player')).toBeNull();
  });

  it('maps no other delivered event into the summary step', () => {
    // The step is the scene's presentation of THIS item and of nothing else: a
    // summary can never be opened by a Match, a pass report, or a turn boundary.
    for (const event of [
      { type: 'SwapStarted', actor: 'player', fromCell: 20, toCell: 21 },
      { type: 'MatchCreated', shape: 'Straight', cells: [24, 25, 26], gemType: 'ATK', cascadeDepth: 1 },
      { type: 'GemMatched', cellIndex: 24, gemType: 'ATK' },
      {
        type: 'CascadeStepResolved',
        passDepth: 1,
        dropMoves: [{ fromCell: 16, toCell: 24 }],
      },
      { type: 'ComboChanged', combo: 2 },
      { type: 'PowerChanged', delta: 25, power: 45, source: 'match' },
    ]) {
      const parsed = parseInBattleEvent(event);
      expect(parsed, JSON.stringify(event)).not.toBeNull();
      expect(adaptPresentedEvent(parsed!)?.kind, JSON.stringify(event)).not.toBe('gem-summary');
    }
  });

  // =======================================================================
  // Diagnostics
  // =======================================================================

  it('formats the delivered counts verbatim, adding and deriving nothing', () => {
    const parsed = parseInBattleEvent(DELIVERED_SUMMARY);

    expect(formatInBattleEvent(parsed!)).toBe('MatchedGemSummary: ATK x5, HP x2');
  });

  // =======================================================================
  // The reusable banner component's pure helpers
  // =======================================================================

  it('draws one chip per delivered type, using the board’s own palette', () => {
    const chips = gemMatchSummaryChips(DELIVERED_SUMMARY.gems, PALETTE);

    expect(chips).toEqual([
      { gemType: 'ATK', label: 'ATK', color: 0xef4444, count: 5 },
      { gemType: 'HP', label: 'HP', color: 0x22c55e, count: 2 },
    ]);
  });

  it('draws no chip for a count that is not positive, and never fabricates a type', () => {
    const chips = gemMatchSummaryChips(
      [
        { gemType: 'ATK', count: 0 },
        { gemType: 'DEF', count: -2 },
        { gemType: 'HP', count: 1.5 },
        { gemType: 'POWER', count: 3 },
      ],
      PALETTE
    );

    expect(chips).toEqual([{ gemType: 'POWER', label: 'PWR', color: 0xa855f7, count: 3 }]);

    // A type the palette does not carry is drawn as the board's own inert
    // placeholder — never guessed at, never substituted.
    expect(gemMatchSummaryChips([{ gemType: 'GOLD', count: 2 }], PALETTE)).toEqual([
      { gemType: 'GOLD', label: '?', color: 0x475569, count: 2 },
    ]);
  });

  it('labels the panel with the actor’s own title, and none for a side the contract does not define', () => {
    expect(gemMatchSummaryTitle('player')).toBe('PLAYER GEMS MATCHED');
    expect(gemMatchSummaryTitle('boss')).toBe('BOSS GEMS MATCHED');
    expect(gemMatchSummaryTitle(null)).toBeNull();
  });

  it('derives one banner lifetime from its three beats', () => {
    expect(gemMatchSummaryBannerMs()).toBe(
      GEM_MATCH_SUMMARY_TIMING.enterMs +
        GEM_MATCH_SUMMARY_TIMING.holdMs +
        GEM_MATCH_SUMMARY_TIMING.exitMs
    );
    expect(gemMatchSummaryBannerMs()).toBeGreaterThan(0);
  });
});

