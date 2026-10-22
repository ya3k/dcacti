import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { GameRuntime } from '../src/game/runtime/GameRuntime';
import { RuntimeActionNotImplementedError } from '../src/game/runtime/GameRuntimeEvents';
import { RuntimeActionOutcomeUnknownError } from '../src/game/runtime/GameRuntimeEvents';
import { DEFAULT_RESOLUTION_WATCHDOG_MS } from '../src/game/runtime/GameRuntimeEvents';
import type { BattleEventsEnvelope } from '../src/game/runtime/GameRuntimeEvents';
import { ApiRequestError } from '../src/services/api/ApiService';
import { ApplicationSession } from '../src/services/api/ApplicationSession';
import type { SignalRConnectionHandlers } from '../src/services/realtime/SignalRService';

/**
 * Test double for the SignalR transport.
 *
 * Mirrors only the surface `GameRuntime` uses, so the runtime's coordination
 * behaviour can be tested without a live hub. The runtime must be testable
 * through the transport port alone — it never imports SignalR types.
 */
class FakeSignalR {
  public handlers: SignalRConnectionHandlers = {};
  public connectCalls: string[] = [];
  public disconnectCalls = 0;
  public subscriptions = new Map<string, (...args: unknown[]) => void>();
  public connectBehaviour: () => Promise<void> = async () => {};
  public connectId: string | null = 'conn-1';
  /** Mirrors the real service: `on()` requires a live connection. */
  public connected = false;
  /**
   * Every hub method name invoked, in order — so a test can assert the start
   * sequence calls no method beyond the documented `JoinBattle`
   * (SIGNALR_PROTOCOL.md §2).
   */
  public invokedMethods: string[] = [];
  /**
   * `on()` registrations per method name. Counting registrations rather than
   * only the surviving handler is what detects a duplicate subscription: the
   * real `HubConnection` would also hold both handlers.
   */
  private registrationCounts = new Map<string, number>();
  /** Every `swap()` call the runtime made, in order (SIGNALR_PROTOCOL.md §2.1, ADR-025). */
  public swapCalls: Array<[string, number, number, number | null, string | null]> = [];
  /** The acknowledgement `swap()` resolves with; overridden per case. */
  public swapResult: { accepted: boolean; reason?: string | null } = { accepted: true };
  /** When set, `swap()` rejects with it — a transport failure (§8.3). */
  public swapBehaviour: (() => Promise<never>) | null = null;
  /** Every `cardCast()` call the runtime made, in order (SIGNALR_PROTOCOL.md §2). */
  public cardCastCalls: Array<[string, string, string | undefined]> = [];
  /** The acknowledgement `cardCast()` resolves with; overridden per case. */
  public cardCastResult: { accepted: boolean; reason?: string | null } = { accepted: true };
  /** When set, `cardCast()` rejects with it — a transport failure (§8.3). */
  public cardCastBehaviour: (() => Promise<never>) | null = null;
  /** Every `petSkillCast()` call the runtime made, in order (SIGNALR_PROTOCOL.md §2). */
  public petSkillCastCalls: Array<[string, string | undefined]> = [];
  /** The acknowledgement `petSkillCast()` resolves with; overridden per case. */
  public petSkillCastResult: { accepted: boolean; reason?: string | null } = { accepted: true };
  /** When set, `petSkillCast()` rejects with it — a transport failure (§8.3). */
  public petSkillCastBehaviour: (() => Promise<never>) | null = null;
  /**
   * Every `getBattleState()` call the runtime made, in order
   * (`SIGNALR_PROTOCOL.md` §7.1) — the reconnect snapshot request.
   */
  public getBattleStateCalls: string[] = [];
  /**
   * The envelope `getBattleState()` resolves with; overridden per case. The
   * default is an accepted snapshot so a recovery test only has to supply the
   * state it wants recovered.
   */
  public getBattleStateResult: {
    accepted: boolean;
    serverSequence?: number | null;
    state?: unknown;
    reason?: string | null;
  } = { accepted: true };
  /** When set, `getBattleState()` rejects with it — a transport failure (§8.3). */
  public getBattleStateBehaviour: (() => Promise<never>) | null = null;
  /**
   * Every `joinBattle()` call the runtime made, in order
   * (SIGNALR_PROTOCOL.md §1.2, §2 `JoinBattle`).
   */
  public joinCalls: string[] = [];
  /** When set, `joinBattle()` rejects with it — a join failure. */
  public joinBehaviour: (() => Promise<never>) | null = null;
  /**
   * Counts physical connections built, the way the real service's single
   * `HubConnection` field does. `connect()` returns early while connected, so a
   * second call must not raise this (ARCHITECTURE.md §2.2.1 rule 6).
   */
  public physicalConnections = 0;

  setHandlers(handlers: SignalRConnectionHandlers): void {
    this.handlers = handlers;
  }

  async connect(hubUrl: string): Promise<void> {
    this.connectCalls.push(hubUrl);

    // Mirrors the real service: an already-Connected connection returns
    // immediately and builds nothing, and an in-flight start is shared rather
    // than duplicated.
    if (this.connected) {
      return;
    }

    await this.connectBehaviour();
    this.connected = true;
    this.physicalConnections += 1;
    this.handlers.onConnected?.(this.connectId);
  }

  disconnect(): Promise<void> {
    this.disconnectCalls += 1;
    this.connected = false;
    return Promise.resolve();
  }

  /**
   * Mirrors `SignalRService.joinBattle` (§1.2): it adds the connection to the
   * battle's group and returns nothing.
   *
   * It fails when not connected, exactly as the real service does — the fake
   * must not be more permissive than production, or the runtime's ordering
   * (connect before join) would go unverified.
   */
  async joinBattle(battleId: string): Promise<void> {
    if (this.joinBehaviour) {
      return await this.joinBehaviour();
    }

    if (!this.connected) {
      throw new Error('SignalR connection is not established.');
    }

    this.invokedMethods.push('JoinBattle');
    this.joinCalls.push(battleId);
  }

  isConnected(): boolean {
    return this.connected;
  }

  /** How many times `on()` was called for a method name. */
  subscriptionRegistrations(methodName: string): number {
    return this.registrationCounts.get(methodName) ?? 0;
  }

  async swap(
    battleId: string,
    fromCell: number,
    toCell: number,
    expectedSequence: number | null,
    actionId: string | null
  ): Promise<{ accepted: boolean; reason?: string | null }> {
    // Recorded before the behaviour runs, so an attempted request that fails at
    // the transport still shows which arguments the runtime submitted — the fact a
    // retry's identity is asserted on.
    this.invokedMethods.push('Swap');
    this.swapCalls.push([battleId, fromCell, toCell, expectedSequence, actionId]);

    if (this.swapBehaviour) {
      return await this.swapBehaviour();
    }

    return this.swapResult;
  }

  async cardCast(
    battleId: string,
    cardId: string,
    clientSequence?: string
  ): Promise<{ accepted: boolean; reason?: string | null }> {
    if (this.cardCastBehaviour) {
      return await this.cardCastBehaviour();
    }

    this.invokedMethods.push('CardCast');
    this.cardCastCalls.push([battleId, cardId, clientSequence]);
    return this.cardCastResult;
  }

  async petSkillCast(
    battleId: string,
    clientSequence?: string
  ): Promise<{ accepted: boolean; reason?: string | null }> {
    if (this.petSkillCastBehaviour) {
      return await this.petSkillCastBehaviour();
    }

    this.invokedMethods.push('PetSkillCast');
    this.petSkillCastCalls.push([battleId, clientSequence]);
    return this.petSkillCastResult;
  }

  /**
   * Mirrors `SignalRService.getBattleState` (`SIGNALR_PROTOCOL.md` §7.1): the
   * reconnect snapshot request, addressed by the battle id the caller already
   * holds.
   */
  async getBattleState(battleId: string): Promise<{
    accepted: boolean;
    serverSequence?: number | null;
    state?: unknown;
    reason?: string | null;
  }> {
    // Recorded before the behaviour runs, exactly as `swap()` records its call, so
    // a gated or failing request still shows that the runtime issued it — the fact
    // "one request in flight per battle" is asserted on.
    this.invokedMethods.push('GetBattleState');
    this.getBattleStateCalls.push(battleId);

    if (this.getBattleStateBehaviour) {
      return await this.getBattleStateBehaviour();
    }

    if (!this.connected) {
      throw new Error('SignalR connection is not established.');
    }

    return this.getBattleStateResult;
  }

  on<TArgs extends unknown[]>(
    methodName: string,
    handler: (...args: TArgs) => void
  ): () => void {
    // The real SignalRService throws unless the connection is established;
    // reproducing that here keeps the runtime's ordering honest.
    if (!this.connected) {
      throw new Error('SignalR connection is not established.');
    }

    this.registrationCounts.set(methodName, this.subscriptionRegistrations(methodName) + 1);
    this.subscriptions.set(methodName, handler as (...args: unknown[]) => void);
    return () => {
      this.subscriptions.delete(methodName);
    };
  }

  /** Simulates a server → client broadcast. */
  emit(methodName: string, ...args: unknown[]): void {
    this.subscriptions.get(methodName)?.(...args);
  }
}

function createRuntime() {
  const transport = new FakeSignalR();
  const runtime = new GameRuntime(transport as never);
  return { runtime, transport };
}

/** The four documented Gem contract names (MATCH3_RULES.md §1.1). */
const GEM_NAMES = ['ATK', 'DEF', 'HP', 'POWER'];

/**
 * A server-shaped board: exactly 64 delivered cells.
 *
 * Each entry is the protocol's `CellPayload` — the cell's Gem type and nothing
 * else (`GAME_STATE.md` §2.1.3 items 1–4, `SIGNALR_PROTOCOL.md` §4.1 item 5) —
 * so a cell is an object carrying one member, never a bare Gem-name string.
 */
function serverCells(): Array<{ gemType: string }> {
  return Array.from({ length: 64 }, (_, index) => ({
    gemType: GEM_NAMES[index % GEM_NAMES.length],
  }));
}

/**
 * A well-formed `BattleStateUpdated` payload — the implemented
 * `GAME_STATE.md` §0 stage's fields: the §2.0.5 Board Foundation State fields
 * plus §2.2's `playerState`, §2.3's `petState`, and §2.4's `bossState`
 * (SIGNALR_PROTOCOL.md §4, §4.2, §4.3, §4.4).
 */
function payload(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    battleId: 'battle-1',
    turn: 0,
    sequence: 0,
    rngSeed: 42,
    rngState: { state: 123456789, increment: 1 },
    board: { cells: serverCells() },
    // GAME_STATE.md §2.2: both values exist from battle creation and are always
    // delivered — including at 0, which is a value, not an absence.
    playerState: { combo: 0, matchCount: 0 },
    // SIGNALR_PROTOCOL.md §4.3: the enumerated delivered members. The
    // `current / threshold` pair is always present; `passiveResetOverride` is
    // omitted here because a default reset is spelled by its absence (§4.3
    // item 7); and `statusEffects` is always present — an active Pet with no
    // active effect is an empty array, never an omission (§4.3 item 14).
    petState: {
      hp: 1000,
      maxHp: 1000,
      power: 0,
      passiveId: 'xich-lang',
      passiveProgress: { threshold: 5, current: 0 },
      equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
      statusEffects: [],
    },
    // SIGNALR_PROTOCOL.md §4.4: the four-member Boss projection. All members
    // are always present and none is nullable (§4.4 items 4, 12, ADR-029).
    bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000, power: 0 },
    ...overrides,
  };
}

describe('GameRuntime', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  describe('initialization', () => {
    it('starts in a disconnected, non-gameplay state', () => {
      const { runtime } = createRuntime();
      const state = runtime.getState();

      expect(state.connection).toBe('disconnected');
      expect(state.runtime).toBe('initializing');
      expect(state.sync).toBe('unsynchronized');
      expect(state.connectionId).toBeNull();
      expect(state.lastError).toBeNull();
    });

    it('carries no gameplay state', () => {
      const { runtime } = createRuntime();
      const keys = Object.keys(runtime.getState());

      // Guard against gameplay state leaking into the runtime contract.
      for (const forbidden of ['hp', 'board', 'gems', 'power', 'combo', 'damage', 'boss', 'pet']) {
        expect(keys).not.toContain(forbidden);
      }
    });

    it('connects through the transport port on initialize', async () => {
      const { runtime, transport } = createRuntime();

      await runtime.initialize('/hubs/battle');

      expect(transport.connectCalls).toEqual(['/hubs/battle']);
      expect(runtime.getState().connection).toBe('connected');
      expect(runtime.getState().connectionId).toBe('conn-1');
      expect(runtime.getState().runtime).toBe('ready');
    });

    it('reports awaiting_battle rather than synchronized when connected', async () => {
      const { runtime } = createRuntime();

      await runtime.initialize();

      // Battle synchronization requires battle resolution (SIGNALR_PROTOCOL.md
      // §5–§6, ADR-008), which does not exist yet.
      expect(runtime.getState().sync).toBe('awaiting_battle');
    });

    it('is idempotent and never opens a second connection', async () => {
      const { runtime, transport } = createRuntime();

      await runtime.initialize();
      await runtime.initialize();
      await runtime.initialize();

      expect(transport.connectCalls).toHaveLength(1);
    });

    it('subscribes to the documented server broadcasts once each', async () => {
      const { runtime, transport } = createRuntime();

      await runtime.initialize();
      await runtime.initialize();

      // ReceiveEvents (§3) and the initial-state push (§4).
      expect(transport.subscriptions.has('ReceiveEvents')).toBe(true);
      expect(transport.subscriptions.has('BattleStateUpdated')).toBe(true);
      expect(transport.subscriptions.size).toBe(2);
    });

    it('subscribes only after the connection exists', async () => {
      // Regression: subscribing before connecting throws, because
      // SignalRService.on() requires a live connection.
      const { runtime, transport } = createRuntime();
      const order: string[] = [];
      const originalOn = transport.on.bind(transport);
      transport.on = ((method: string, handler: (...a: unknown[]) => void) => {
        order.push('subscribe');
        return originalOn(method, handler as never);
      }) as typeof transport.on;

      const originalConnect = transport.connect.bind(transport);
      transport.connect = async (url: string) => {
        await originalConnect(url);
        order.push('connect');
      };

      await runtime.initialize();

      // Both documented subscriptions are registered only after the connection
      // exists — never before it.
      expect(order).toEqual(['connect', 'subscribe', 'subscribe']);
    });

    it('does not subscribe when the connection fails', async () => {
      const { runtime, transport } = createRuntime();
      transport.connectBehaviour = async () => {
        throw new Error('backend unreachable');
      };

      await runtime.initialize();

      expect(transport.subscriptions.size).toBe(0);
      expect(runtime.getState().connection).toBe('error');
    });
  });

  describe('runtime lifecycle events', () => {
    it('emits connected on a successful connection', async () => {
      const { runtime } = createRuntime();
      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      await runtime.initialize();

      expect(events).toContain('connected');
    });

    it('emits disconnected when the transport closes', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      transport.handlers.onClosed?.(new Error('network down'));

      expect(events).toContain('disconnected');
      expect(runtime.getState().connection).toBe('disconnected');
      expect(runtime.getState().runtime).toBe('suspended');
      expect(runtime.getState().lastError).toBe('network down');
    });

    it('emits reconnecting then reconnected across a reconnect cycle', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      transport.handlers.onReconnecting?.(new Error('lost'));
      expect(runtime.getState().connection).toBe('reconnecting');
      expect(runtime.getState().runtime).toBe('suspended');

      transport.handlers.onReconnected?.('conn-2');
      expect(runtime.getState().connection).toBe('connected');
      expect(runtime.getState().runtime).toBe('ready');
      expect(runtime.getState().connectionId).toBe('conn-2');

      expect(events).toEqual(
        expect.arrayContaining(['reconnecting', 'reconnected'])
      );
    });

    it('handles a connection failure gracefully without throwing', async () => {
      const { runtime, transport } = createRuntime();
      transport.connectBehaviour = async () => {
        throw new Error('backend unreachable');
      };

      await expect(runtime.initialize()).resolves.toBeUndefined();

      const state = runtime.getState();
      expect(state.connection).toBe('error');
      expect(state.runtime).toBe('error');
      expect(state.lastError).toBe('backend unreachable');
    });
  });

  describe('battle event forwarding', () => {
    it('forwards server-authoritative event batches unchanged', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const received: unknown[] = [];
      runtime.onBattleEvents((envelope) => received.push(envelope));

      // The documented delivery order (`SIGNALR_PROTOCOL.md` §3.1, §6 item 4):
      // the resolution's state is pushed before its batch, so the batch is at the
      // sequence the synchronized copy already carries. That is the path this
      // test asserts passes through unchanged.
      const serverState = payload({ battleId: 'battle-1', sequence: 7 });
      transport.emit('BattleStateUpdated', serverState);

      const payloadEnvelope = {
        battleId: 'battle-1',
        serverSequence: 7,
        events: [{ some: 'server-owned-event' }, { another: true }],
      };
      transport.emit('ReceiveEvents', payloadEnvelope);

      expect(received).toHaveLength(1);
      expect(received[0]).toEqual(payloadEnvelope);
    });

    it('does not reorder events', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const received: unknown[][] = [];
      runtime.onBattleEvents((envelope) => received.push([...envelope.events]));

      // As above: the state push that precedes the batch (`§6 item 4`).
      transport.emit('BattleStateUpdated', payload({ battleId: 'b', sequence: 1 }));

      transport.emit('ReceiveEvents', {
        battleId: 'b',
        serverSequence: 1,
        events: ['first', 'second', 'third'],
      });

      expect(received[0]).toEqual(['first', 'second', 'third']);
    });

    it('reports a malformed payload as a runtime error instead of inventing data', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const battles: unknown[] = [];
      const events: string[] = [];
      runtime.onBattleEvents((e) => battles.push(e));
      runtime.onRuntimeEvent((e) => events.push(e.type));

      transport.emit('ReceiveEvents', { battleId: 'b' });

      expect(battles).toHaveLength(0);
      expect(events).toContain('runtime_error');
    });

    it('stops forwarding after disposal', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const received: unknown[] = [];
      runtime.onBattleEvents((e) => received.push(e));

      await runtime.dispose();
      transport.emit('ReceiveEvents', { battleId: 'b', serverSequence: 1, events: [] });

      expect(received).toHaveLength(0);
    });

    describe('intermediatePetHp envelope parsing and forwarding', () => {
      it('forwards a valid finite numeric intermediatePetHp on ReceiveEvents batches', async () => {
        const { runtime, transport } = createRuntime();
        await runtime.initialize();

        const received: BattleEventsEnvelope[] = [];
        runtime.onBattleEvents((envelope) => received.push(envelope));

        transport.emit('BattleStateUpdated', payload({ battleId: 'b', sequence: 1 }));

        transport.emit('ReceiveEvents', {
          battleId: 'b',
          serverSequence: 1,
          events: [{ type: 'SwapStarted' }],
          intermediatePetHp: 560,
        });

        expect(received).toHaveLength(1);
        expect(received[0].intermediatePetHp).toBe(560);
        expect(received[0].battleId).toBe('b');
        expect(received[0].serverSequence).toBe(1);

        // Also covers zero as a valid finite numeric value
        transport.emit('ReceiveEvents', {
          battleId: 'b',
          serverSequence: 1,
          events: [{ type: 'BattleLost' }],
          intermediatePetHp: 0,
        });

        expect(received).toHaveLength(2);
        expect(received[1].intermediatePetHp).toBe(0);
      });

      it('remains backward-compatible when intermediatePetHp is absent or null', async () => {
        const { runtime, transport } = createRuntime();
        await runtime.initialize();

        const received: BattleEventsEnvelope[] = [];
        runtime.onBattleEvents((envelope) => received.push(envelope));

        transport.emit('BattleStateUpdated', payload({ battleId: 'b', sequence: 1 }));

        // Older server payload: field is absent
        transport.emit('ReceiveEvents', {
          battleId: 'b',
          serverSequence: 1,
          events: [{ type: 'SwapStarted' }],
        });

        expect(received).toHaveLength(1);
        expect('intermediatePetHp' in received[0]).toBe(false);
        expect(received[0].intermediatePetHp).toBeUndefined();

        // Older or nullable server payload: field is explicitly null
        transport.emit('ReceiveEvents', {
          battleId: 'b',
          serverSequence: 1,
          events: [{ type: 'CardCast' }],
          intermediatePetHp: null,
        });

        expect(received).toHaveLength(2);
        expect('intermediatePetHp' in received[1]).toBe(false);
        expect(received[1].intermediatePetHp).toBeUndefined();
      });

      it('sanitizes string values of intermediatePetHp according to the parser contract', async () => {
        const { runtime, transport } = createRuntime();
        await runtime.initialize();

        const received: BattleEventsEnvelope[] = [];
        runtime.onBattleEvents((envelope) => received.push(envelope));

        transport.emit('BattleStateUpdated', payload({ battleId: 'b', sequence: 1 }));

        // A string value must not be forwarded as intermediatePetHp; the parser omits it
        transport.emit('ReceiveEvents', {
          battleId: 'b',
          serverSequence: 1,
          events: [{ type: 'SwapStarted' }],
          intermediatePetHp: '560',
        });

        expect(received).toHaveLength(1);
        expect('intermediatePetHp' in received[0]).toBe(false);
        expect(received[0].intermediatePetHp).toBeUndefined();
      });

      it('rejects non-finite values (NaN, positive infinity, negative infinity)', async () => {
        const { runtime, transport } = createRuntime();
        await runtime.initialize();

        const received: BattleEventsEnvelope[] = [];
        runtime.onBattleEvents((envelope) => received.push(envelope));

        transport.emit('BattleStateUpdated', payload({ battleId: 'b', sequence: 1 }));

        // NaN
        transport.emit('ReceiveEvents', {
          battleId: 'b',
          serverSequence: 1,
          events: [{ type: 'SwapStarted' }],
          intermediatePetHp: Number.NaN,
        });

        // Positive Infinity
        transport.emit('ReceiveEvents', {
          battleId: 'b',
          serverSequence: 1,
          events: [{ type: 'SwapStarted' }],
          intermediatePetHp: Number.POSITIVE_INFINITY,
        });

        // Negative Infinity
        transport.emit('ReceiveEvents', {
          battleId: 'b',
          serverSequence: 1,
          events: [{ type: 'SwapStarted' }],
          intermediatePetHp: Number.NEGATIVE_INFINITY,
        });

        expect(received).toHaveLength(3);
        expect('intermediatePetHp' in received[0]).toBe(false);
        expect('intermediatePetHp' in received[1]).toBe(false);
        expect('intermediatePetHp' in received[2]).toBe(false);
      });

      it('maintains distinct parser responsibilities from BattleScene domain validation', async () => {
        const { runtime, transport } = createRuntime();
        await runtime.initialize();

        const received: BattleEventsEnvelope[] = [];
        runtime.onBattleEvents((envelope) => received.push(envelope));

        transport.emit('BattleStateUpdated', payload({ battleId: 'b', sequence: 1 }));

        // The parser validates only type and finiteness (SIGNALR_PROTOCOL.md §3).
        // It does not enforce domain constraints such as non-negativity (intermediatePetHp >= 0),
        // MaxHP clamping, or phase correlation, which are owned by BattleScene.
        transport.emit('ReceiveEvents', {
          battleId: 'b',
          serverSequence: 1,
          events: [{ type: 'SwapStarted' }],
          intermediatePetHp: -10,
        });

        expect(received).toHaveLength(1);
        expect(received[0].intermediatePetHp).toBe(-10);

        // Direct parser invocation confirms the contract on readBattleEventsEnvelope
        const parse = (p: unknown): BattleEventsEnvelope | null =>
          (runtime as unknown as { readBattleEventsEnvelope: (payload: unknown) => BattleEventsEnvelope | null }).readBattleEventsEnvelope(p);

        expect(parse({ battleId: 'b', serverSequence: 1, events: [], intermediatePetHp: 560 })).toEqual({
          battleId: 'b',
          serverSequence: 1,
          events: [],
          intermediatePetHp: 560,
        });
        expect(parse({ battleId: 'b', serverSequence: 1, events: [], intermediatePetHp: -10 })).toEqual({
          battleId: 'b',
          serverSequence: 1,
          events: [],
          intermediatePetHp: -10,
        });
        expect(parse({ battleId: 'b', serverSequence: 1, events: [] })).toEqual({
          battleId: 'b',
          serverSequence: 1,
          events: [],
        });
        expect(parse({ battleId: 'b', serverSequence: 1, events: [], intermediatePetHp: null })).toEqual({
          battleId: 'b',
          serverSequence: 1,
          events: [],
        });
        expect(parse({ battleId: 'b', serverSequence: 1, events: [], intermediatePetHp: 'invalid' })).toEqual({
          battleId: 'b',
          serverSequence: 1,
          events: [],
        });
        expect(parse({ battleId: 'b', serverSequence: 1, events: [], intermediatePetHp: Number.NaN })).toEqual({
          battleId: 'b',
          serverSequence: 1,
          events: [],
        });
        expect(parse({ battleId: 'b', serverSequence: 1, events: [], intermediatePetHp: Number.POSITIVE_INFINITY })).toEqual({
          battleId: 'b',
          serverSequence: 1,
          events: [],
        });
        expect(parse({ battleId: 'b', serverSequence: 1, events: [], intermediatePetHp: Number.NEGATIVE_INFINITY })).toEqual({
          battleId: 'b',
          serverSequence: 1,
          events: [],
        });
        expect(parse(null)).toBeNull();
        expect(parse('not-an-object')).toBeNull();
        expect(parse({ battleId: 123 })).toBeNull();
      });
    });
  });

  describe('subscriptions', () => {
    it('unsubscribes runtime listeners', async () => {
      const { runtime } = createRuntime();
      const listener = vi.fn();

      const unsubscribe = runtime.onRuntimeEvent(listener);
      await runtime.initialize();
      const callsAfterInit = listener.mock.calls.length;

      unsubscribe();
      runtime.setEngineStatus('running');

      expect(listener.mock.calls.length).toBe(callsAfterInit);
    });

    it('unsubscribes battle listeners', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const listener = vi.fn();
      const unsubscribe = runtime.onBattleEvents(listener);
      unsubscribe();

      transport.emit('ReceiveEvents', { battleId: 'b', serverSequence: 1, events: [] });

      expect(listener).not.toHaveBeenCalled();
    });
  });

  describe('cleanup / disposal', () => {
    it('disconnects the transport on dispose', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      await runtime.dispose();

      expect(transport.disconnectCalls).toBe(1);
      expect(runtime.isDisposed()).toBe(true);
    });

    it('detaches transport handlers on dispose', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      await runtime.dispose();

      expect(transport.handlers.onConnected).toBeUndefined();
      expect(transport.handlers.onClosed).toBeUndefined();
    });

    it('clears all listeners on dispose', async () => {
      const { runtime } = createRuntime();
      await runtime.initialize();

      const runtimeListener = vi.fn();
      const battleListener = vi.fn();
      runtime.onRuntimeEvent(runtimeListener);
      runtime.onBattleEvents(battleListener);

      await runtime.dispose();
      runtime.setEngineStatus('running');

      expect(runtimeListener).not.toHaveBeenCalled();
      expect(battleListener).not.toHaveBeenCalled();
    });

    it('is safe to dispose repeatedly', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      await runtime.dispose();
      await runtime.dispose();

      expect(transport.disconnectCalls).toBe(1);
    });

    it('refuses to re-initialize after disposal', async () => {
      const { runtime } = createRuntime();
      await runtime.initialize();
      await runtime.dispose();

      await expect(runtime.initialize()).rejects.toThrow(/disposed/i);
    });
  });

  describe('battle state synchronization (SIGNALR_PROTOCOL.md §4)', () => {
    it('exposes no battle state before the server pushes it', async () => {
      const { runtime } = createRuntime();
      await runtime.initialize();

      expect(runtime.getBattleState()).toBeNull();
    });

    it('subscribes to the documented BattleStateUpdated push', async () => {
      const { runtime, transport } = createRuntime();

      await runtime.initialize();

      expect(transport.subscriptions.has('BattleStateUpdated')).toBe(true);
      // ReceiveEvents (§3) and BattleStateUpdated (§4) only — no parallel or
      // invented state-sync method exists (§4, §8).
      expect(transport.subscriptions.size).toBe(2);
    });

    it('stores the incoming state as the runtime synchronized copy', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      transport.emit('BattleStateUpdated', payload());

      expect(runtime.getBattleState()).toEqual(payload());
    });

    it('carries the authoritative board exactly as the server sent it', async () => {
      // SIGNALR_PROTOCOL.md §4 item 10 / §4.1 item 3: the board is delivered —
      // the client stores all 64 cells verbatim and derives nothing.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const cells = serverCells();
      cells[0] = { gemType: 'POWER' };
      cells[63] = { gemType: 'ATK' };

      transport.emit('BattleStateUpdated', payload({ board: { cells } }));

      expect(runtime.getBattleState()!.board.cells).toEqual(cells);
      expect(runtime.getBattleState()!.board.cells).toHaveLength(64);
    });

    it('does not mutate the board it received', async () => {
      // The runtime holds a synchronized copy; it never repairs or rewrites it.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const sent = payload();
      transport.emit('BattleStateUpdated', sent);

      const received = runtime.getBattleState()!;
      received.board.cells.forEach((cell, index) => {
        expect(cell.gemType).toBe(
          (sent.board as { cells: Array<{ gemType: string }> }).cells[index].gemType
        );
      });
    });

    it('notifies battle-state listeners with the server value unchanged', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const received: unknown[] = [];
      runtime.onBattleState((state) => received.push(state));

      transport.emit('BattleStateUpdated', payload());

      expect(received).toEqual([payload()]);
    });

    it('does not author, adjust, or recompute the state it receives', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      // Whatever the server says is what the runtime holds — the client owns
      // none of these values (§4.9, ADR-001).
      const sent = payload({ battleId: 'server-owned', turn: 4, sequence: 11 });

      transport.emit('BattleStateUpdated', sent);

      expect(runtime.getBattleState()).toEqual(sent);
    });

    it('reports synchronization once the authoritative state arrives', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      expect(runtime.getState().sync).toBe('awaiting_battle');

      transport.emit('BattleStateUpdated', payload());

      expect(runtime.getState().sync).toBe('synchronized');
    });

    it('emits a battle_state_changed runtime event', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      transport.emit('BattleStateUpdated', payload());

      expect(events).toContain('battle_state_changed');
    });

    it('reports a malformed payload instead of inventing battle state', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      // `sequence` missing — no default is substituted.
      transport.emit('BattleStateUpdated', { battleId: 'battle-1', turn: 0 });

      expect(runtime.getBattleState()).toBeNull();
      expect(events).toContain('runtime_error');
    });

    it('rejects a partial board rather than completing it', async () => {
      // SIGNALR_PROTOCOL.md §4.1 item 3: "The client does not receive a partial
      // board." A short board must not be padded with generated Gems — that
      // would make the client authoritative (GAME_RULES.md §18).
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      transport.emit('BattleStateUpdated', payload({ board: { cells: serverCells().slice(0, 63) } }));

      expect(runtime.getBattleState()).toBeNull();
      expect(events).toContain('runtime_error');
    });

    it('accepts the delivered cell shape, where each cell carries its Gem type', async () => {
      // SIGNALR_PROTOCOL.md §4.1 item 5: the board carries pure `GemType` cell
      // entries. A reader that required bare Gem-name strings would reject every
      // real server payload, which is exactly how the board failed to render
      // before TASK-182.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      transport.emit('BattleStateUpdated', payload());

      const board = runtime.getBattleState()!.board;
      expect(board.cells).toHaveLength(64);
      expect(board.cells.every((cell) => typeof cell.gemType === 'string')).toBe(true);
      // The four documented contract names (MATCH3_RULES.md §1.1) are what the
      // server sent; nothing was substituted by the client.
      expect(new Set(board.cells.map((c) => c.gemType))).toEqual(
        new Set(['ATK', 'DEF', 'HP', 'POWER'])
      );
    });

    it('keeps a delivered cell to its Gem type and models no retired member', async () => {
      // GAME_STATE.md §2.1.3 items 1–4: a cell's whole stored content is its Gem
      // type, so the synchronized copy carries exactly one member. ADR-028 §4
      // makes the reader tolerant of a retired per-cell member — it is ignored
      // rather than modelled, and no placeholder is written in its place.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const cells = serverCells().map((cell, index) =>
        index === 5 ? { gemType: cell.gemType, retiredCellMember: { type: 'Retired' } } : cell
      );

      transport.emit('BattleStateUpdated', payload({ board: { cells } }));

      const board = runtime.getBattleState()!.board;
      expect(board.cells[5]).toEqual({ gemType: 'DEF' });
      expect(Object.keys(board.cells[5])).toEqual(['gemType']);
      expect(board.cells.every((cell) => Object.keys(cell).length === 1)).toBe(true);
    });

    it('rejects a cell that carries no Gem type rather than defaulting one', async () => {
      // `gemType` is always present on the wire (GAME_STATE.md §2.1.3 items 1–2),
      // so its absence is malformed — reading a default would let the client
      // decide a board value (GAME_RULES.md §18).
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      const cells = serverCells();
      cells[10] = {} as { gemType: string };

      transport.emit('BattleStateUpdated', payload({ board: { cells } }));

      expect(runtime.getBattleState()).toBeNull();
      expect(events).toContain('runtime_error');
    });

    it('rejects a payload carrying only one half of the RngState pair', async () => {
      // GAME_STATE.md §2.6.2 item 1: the two components are one logical field
      // and are never split.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      transport.emit('BattleStateUpdated', payload({ rngState: { state: 123456789 } }));

      expect(runtime.getBattleState()).toBeNull();
      expect(events).toContain('runtime_error');
    });

    it('rejects a payload missing a PlayerState value rather than defaulting it', async () => {
      // GAME_STATE.md §2.2 / MATCH3_RULES.md §6.5 item 4: both values are
      // non-nullable and always present, and `Combo = 0` is a real value rather
      // than an absence. A payload that omits one is therefore malformed — the
      // client must not substitute a zero of its own, which would be a second,
      // non-authoritative Match/Combo source (GAME_RULES.md §18).
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      transport.emit('BattleStateUpdated', payload({ playerState: { combo: 0 } }));

      expect(runtime.getBattleState()).toBeNull();
      expect(events).toContain('runtime_error');
    });

    it('carries the delivered MatchCount and Combo unchanged', async () => {
      // SIGNALR_PROTOCOL.md §4.2 / GAME_STATE.md §2.2: `playerState` is
      // authoritative server state, carried because it is a BattleState field.
      // The client renders it and derives nothing (MATCH3_RULES.md §6.6 item 3).
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const sent = payload({ playerState: { combo: 3, matchCount: 7 } });
      transport.emit('BattleStateUpdated', sent);

      expect(runtime.getBattleState()!.playerState).toEqual({ combo: 3, matchCount: 7 });

      // Exactly what was sent — not recomputed from `turn`, `sequence`, or the
      // board, none of which carry a Match or Combo value
      // (GAME_STATE.md §5.2 item 2).
      expect(runtime.getBattleState()!.playerState).toEqual(
        (sent.playerState as { combo: number; matchCount: number })
      );
    });

    it('carries the delivered Passive identity and progress pair unchanged', async () => {
      // SIGNALR_PROTOCOL.md §4.3 / GAME_STATE.md §2.3: `petState` is
      // authoritative server state, carried because it is a BattleState field of
      // the implemented stage and because PASSIVE_RULES.md §6 item 1 requires the
      // pair to be exposed as a UI-facing value. The client renders it and
      // derives nothing.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const sent = payload({
        petState: {
          hp: 1000,
          maxHp: 1000,
          power: 0,
          passiveId: 'thanh-xa-poison',
          passiveProgress: { threshold: 7, current: 3 },
          equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          statusEffects: [],
        },
      });

      transport.emit('BattleStateUpdated', sent);

      expect(runtime.getBattleState()!.petState).toEqual({
        passiveId: 'thanh-xa-poison',
        passiveProgress: { threshold: 7, current: 3 },
        equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
        statusEffects: [],
        hp: 1000,
        maxHp: 1000,
        power: 0,
      });

      // Exactly what was sent — not derived from `board`, `turn`, `sequence`,
      // `combo`, or `matchCount`, none of which carries a Passive value.
      expect(runtime.getBattleState()!.petState).toEqual(sent.petState);
    });

    it('carries a delivered non-default reset override as the documented contract name', async () => {
      // §4.3 item 6: when the Passive declares a non-default behavior the member
      // is present, carrying `"Partial"` or `"NoReset"` — the contract name of
      // PASSIVE_RULES.md §4 item 2, never a numeric enum ordinal (§3.2.4).
      for (const contractName of ['Partial', 'NoReset']) {
        const { runtime, transport } = createRuntime();
        await runtime.initialize();

        transport.emit(
          'BattleStateUpdated',
          payload({
            petState: {
              hp: 1000,
              maxHp: 1000,
              power: 0,
              passiveId: 'xich-lang',
              passiveProgress: { threshold: 5, current: 4 },
              passiveResetOverride: contractName,
              equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
              statusEffects: [],
            },
          })
        );

        expect(runtime.getBattleState()!.petState.passiveResetOverride).toBe(contractName);
      }
    });

    it('tolerates an omitted reset override without inventing one', async () => {
      // §4.3 item 7: a default reset OMITS the member, and the absence *is* the
      // statement "default". The runtime stores the absence as absence — it must
      // not substitute `null`, `"Default"`, or any other value, because writing
      // one would be a second spelling of one fact GAME_STATE.md §0 item 5
      // forbids.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      transport.emit('BattleStateUpdated', payload());

      const petState = runtime.getBattleState()!.petState;

      expect('passiveResetOverride' in petState).toBe(false);
      expect(petState.passiveResetOverride).toBeUndefined();
      expect(Object.keys(petState).sort()).toEqual(
        ['equippedCards', 'hp', 'maxHp', 'passiveId', 'passiveProgress', 'power', 'statusEffects'].sort()
      );
    });

    it('rejects a payload missing a PetState member rather than defaulting it', async () => {
      // §4.3 items 3–4, 13 / GAME_STATE.md §2.3 item 3: `passiveId`, both members
      // of `passiveProgress`, and the 4-entry `equippedCards` are non-nullable and
      // always present, and `current = 0` is a real value rather than an absence.
      // A payload that omits one is therefore malformed — the client must not substitute a
      // value of its own, which would be a second, non-authoritative Passive
      // source (GAME_RULES.md §18).
      const malformed = [
        // No `petState` at all.
        { petState: undefined },
        // No `passiveId`.
        {
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          },
        },
        // No `passiveProgress`.
        {
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          },
        },
        // Half of the progress pair.
        {
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5 },
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          },
        },
        // Ill-typed members.
        {
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: '5', current: 0 },
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          },
        },
        {
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 7,
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          },
        },
        // Missing equippedCards.
        { petState: { passiveId: 'xich-lang', passiveProgress: { threshold: 5, current: 0 } } },
        // equippedCards not an array.
        {
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: 'invalid',
          },
        },
        // equippedCards wrong length.
        {
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge'],
          },
        },
        // equippedCards empty string.
        {
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: ['card-heal', '', 'card-power-charge', 'card-inferno'],
          },
        },
      ];

      for (const override of malformed) {
        const { runtime, transport } = createRuntime();
        await runtime.initialize();

        const events: string[] = [];
        runtime.onRuntimeEvent((e) => events.push(e.type));

        transport.emit('BattleStateUpdated', payload(override));

        expect(runtime.getBattleState()).toBeNull();
        expect(events).toContain('runtime_error');
      }
    });

    it('models no undocumented PetState member', async () => {
      // §4.3 item 2: `petState` carries the enumerated members — the Passive
      // identity, its progress pair, the conditional reset override,
      // `equippedCards`, the active Pet's `statusEffects`, and (TASK-208
      // D-208-01/D-208-02, §4.3 item 15) the live combat values hp/maxHp/power.
      // The rest of GAME_STATE.md §2.3 — identity, progression, the remaining
      // combat stats, the sibling modifier collections, and the Relic loadout —
      // belongs to other stages and is not delivered.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      transport.emit(
        'BattleStateUpdated',
        payload({
          petState: {
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
            statusEffects: [],
            hp: 1000,
            maxHp: 1000,
            power: 0,
            petId: 'pet-1',
            element: 'Hoa',
            tier: 1,
            star: 3,
            level: 12,
            atk: 50,
            def: 25,
            crit: 5,
            equippedRelics: ['relic-a'],
            nextAttackCritModifiers: [],
            cardCostModifiers: [],
            atkModifiers: [],
          },
          bossState: {
            hp: 4200,
            maxHp: 5000,
            bossId: 'boss-hoa-long',
            element: 'Fire',
            atk: 60,
            def: 30,
            state: 'Idle',
            passiveId: 'boss-hoa-long-rage',
            passiveProgress: { threshold: 3, current: 1 },
            skillCooldown: 0,
            power: 0,
            statusEffects: [],
          },
        })
      );

      const state = runtime.getBattleState()!;
      const petState = state.petState;

      // Only the documented members are modelled; the rest are dropped rather
      // than carried as an invented shape. TASK-208's D-208-01/D-208-02 decisions
      // (§4.3 item 15) added `hp`, `maxHp` and `power` to that documented set, so
      // they are modelled now and the remaining Domain-only members are not.
      expect(Object.keys(petState).sort()).toEqual(
        ['equippedCards', 'hp', 'maxHp', 'passiveId', 'passiveProgress', 'power', 'statusEffects'].sort()
      );

      for (const undocumented of [
        'petId',
        'element',
        'tier',
        'star',
        'level',
        'atk',
        'def',
        'crit',
        'equippedRelics',
        'nextAttackCritModifiers',
        'cardCostModifiers',
        'atkModifiers',
      ]) {
        expect(petState).not.toHaveProperty(undocumented);
      }

      // §4.4 items 2–3, 10, 12: `bossState` is a four-member projection, not
      // `BossState`. Every other §2.4 member — including the Boss's own
      // `StatusEffects[]` — is dropped rather than modelled.
      expect(Object.keys(state.bossState).sort()).toEqual(['bossId', 'hp', 'maxHp', 'power'].sort());

      for (const hiddenBossMember of [
        'element',
        'atk',
        'def',
        'state',
        'passiveId',
        'passiveProgress',
        'skillCooldown',
        'statusEffects',
      ]) {
        expect(state.bossState).not.toHaveProperty(hiddenBossMember);
      }
    });

    it('carries the delivered equippedCards loadout verbatim', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const loadout = ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'];
      transport.emit(
        'BattleStateUpdated',
        payload({
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: loadout,
            statusEffects: [],
          },
        })
      );

      expect(runtime.getBattleState()!.petState.equippedCards).toEqual(loadout);
    });

    it('carries the delivered statusEffects array verbatim', async () => {
      // SIGNALR_PROTOCOL.md §4.3 item 14 / GAME_STATE.md §2.3.1: the active
      // Pet's active Status Effect instances are delivered through the same push
      // and are read as sent. The client renders them and derives nothing: it
      // does not apply, refresh, decrement, expire, or remove an instance, and
      // does not evaluate a duration or an expiry condition (GAME_RULES.md §18).
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const effects = [
        // A Turn-based instance with no TargetStat: `targetStat` and
        // `expiryCondition` do not apply and are absent (§2.3.1 item 7).
        { id: 'Burn', type: 'DoT', source: 'boss', magnitude: 25, remainingTurns: 2 },
        // A trigger-based instance: `remainingTurns` does not apply and is absent
        // — the two duration models are mutually exclusive (§2.3.1 item 3).
        { id: 'Shield', type: 'Shield', source: 'player', magnitude: 100, expiryCondition: 'ShieldDepleted' },
        // A stat-modifying BuffDebuff: `targetStat` applies and is present.
        { id: 'Root', type: 'BuffDebuff', source: 'boss', magnitude: -30, targetStat: 'ATK', remainingTurns: 3 },
      ];

      transport.emit(
        'BattleStateUpdated',
        payload({
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
            statusEffects: effects,
          },
        })
      );

      const delivered = runtime.getBattleState()!.petState.statusEffects;

      // Exactly what was sent, element for element and order for order: element
      // order is not semantic (§2.3.1 item 10) but is preserved, not reordered.
      expect(delivered).toEqual(effects);

      // A member that does not apply stays ABSENT rather than becoming `null` or
      // an invented value (§2.3.1 item 7, §3.2.5).
      expect('targetStat' in delivered[0]).toBe(false);
      expect('expiryCondition' in delivered[0]).toBe(false);
      expect('remainingTurns' in delivered[1]).toBe(false);
    });

    it('carries an empty statusEffects collection as an empty array, not an absence', async () => {
      // §4.3 item 14: the member is always present and an active Pet with no
      // active effect is sent an EMPTY ARRAY. This is the opposite of §3.2.5's
      // omitted-when-not-applicable convention, and a consumer must not read an
      // absent `statusEffects` as "no effect is active".
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      transport.emit('BattleStateUpdated', payload());

      const statusEffects = runtime.getBattleState()!.petState.statusEffects;

      expect(statusEffects).toEqual([]);
      expect('statusEffects' in runtime.getBattleState()!.petState).toBe(true);
    });

    it('rejects a payload that omits statusEffects rather than reading it as empty', async () => {
      // §4.3 item 14 / GAME_STATE.md §2.3.2 item 1: the collection "always
      // exists ... it is never omitted and never null", so an omission is outside
      // the contract. Reading it as "no effect is active" would accept a second
      // spelling of that fact (GAME_STATE.md §0 item 5), so the payload is
      // malformed and the runtime must not fabricate the member.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      transport.emit(
        'BattleStateUpdated',
        payload({
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5, current: 0 },
            equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          },
        })
      );

      expect(runtime.getBattleState()).toBeNull();
      expect(events).toContain('runtime_error');
    });

    it('rejects a malformed statusEffects element rather than defaulting it', async () => {
      // §2.3.1 items 1–2: `id`, `type`, `source`, and `magnitude` are required on
      // every instance, so an element missing one is malformed rather than
      // defaultable. The client must not invent the missing member.
      const malformedElements: unknown[][] = [
        // No `id`.
        [{ type: 'DoT', source: 'boss', magnitude: 25, remainingTurns: 2 }],
        // No `type`.
        [{ id: 'Burn', source: 'boss', magnitude: 25, remainingTurns: 2 }],
        // No `source`.
        [{ id: 'Burn', type: 'DoT', magnitude: 25, remainingTurns: 2 }],
        // No `magnitude`.
        [{ id: 'Burn', type: 'DoT', source: 'boss', remainingTurns: 2 }],
        // Ill-typed members.
        [{ id: 7, type: 'DoT', source: 'boss', magnitude: 25, remainingTurns: 2 }],
        [{ id: 'Burn', type: 'DoT', source: 'boss', magnitude: '25', remainingTurns: 2 }],
        [{ id: 'Burn', type: 'DoT', source: 'boss', magnitude: 25, remainingTurns: '2' }],
        // Not an object.
        ['Burn'],
      ];

      for (const statusEffects of malformedElements) {
        const { runtime, transport } = createRuntime();
        await runtime.initialize();

        const events: string[] = [];
        runtime.onRuntimeEvent((e) => events.push(e.type));

        transport.emit(
          'BattleStateUpdated',
          payload({
            petState: {
              hp: 1000,
              maxHp: 1000,
              power: 0,
              passiveId: 'xich-lang',
              passiveProgress: { threshold: 5, current: 0 },
              equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
              statusEffects,
            },
          })
        );

        expect(runtime.getBattleState()).toBeNull();
        expect(events).toContain('runtime_error');
      }
    });

    it('carries the delivered Boss identity and hp and maxHp unchanged', async () => {
      // SIGNALR_PROTOCOL.md §4.4 / GAME_STATE.md §2.4: the Boss arrives as the
      // three-member `bossState` projection — the canonical technical Identity plus
      // the live health — and every member is read as sent. The client does not
      // damage the Boss, clamp `hp` to `maxHp`, infer one from the other, re-derive
      // either from `finalBossHp`, or turn the identity into anything but the
      // identity (§4.4 items 5, 7 and 10).
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      transport.emit(
        'BattleStateUpdated',
        payload({ bossState: { bossId: 'boss-kim-loi-vuong', hp: 4200, maxHp: 5000, power: 30 } })
      );

      expect(runtime.getBattleState()!.bossState).toEqual({
        bossId: 'boss-kim-loi-vuong',
        hp: 4200,
        maxHp: 5000,
        power: 30,
      });
    });

    it('carries an hp of 0 as a real published value', async () => {
      // §4.4 item 4: `hp = 0` is the terminal value of a won battle and is a real
      // published value sent as `0` — never an omission and never null.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      transport.emit(
        'BattleStateUpdated',
        payload({ bossState: { bossId: 'boss-hoa-long', hp: 0, maxHp: 5000, power: 10 } })
      );

      const state = runtime.getBattleState()!;
      expect(state.bossState.hp).toBe(0);
      // The other members are unaffected by a zero HP: nothing is dropped and
      // nothing is inferred from it (§4.4 items 4–5, 10, 12).
      expect(state.bossState.maxHp).toBe(5000);
      expect(state.bossState.bossId).toBe('boss-hoa-long');
      expect(state.bossState.power).toBe(10);
    });

    it('rejects a payload missing a bossState member rather than defaulting it', async () => {
      // §4.4 items 4, 12: all four members are always present and none is optional —
      // the Boss exists from battle creation at full health with its identity and
      // power fixed, so there is no absent case, a client must not read an absent `hp`
      // or `power` as zero, and it must not supply an identity it was not sent (§4.4 item 10).
      const malformed = [
        // No `bossState` at all.
        { bossState: undefined },
        // No `hp`.
        { bossState: { bossId: 'boss-hoa-long', maxHp: 5000, power: 0 } },
        // No `maxHp`.
        { bossState: { bossId: 'boss-hoa-long', hp: 4200, power: 0 } },
        // No `power`.
        { bossState: { bossId: 'boss-hoa-long', hp: 4200, maxHp: 5000 } },
        // No `bossId` — the identity is never reconstructed client-side.
        { bossState: { hp: 4200, maxHp: 5000, power: 0 } },
        // An empty identity is not an identity.
        { bossState: { bossId: '', hp: 4200, maxHp: 5000, power: 0 } },
        // Ill-typed members.
        { bossState: { bossId: 'boss-hoa-long', hp: '4200', maxHp: 5000, power: 0 } },
        { bossState: { bossId: 'boss-hoa-long', hp: 4200, maxHp: null, power: 0 } },
        { bossState: { bossId: 'boss-hoa-long', hp: 4200, maxHp: 5000, power: '10' } },
        // The empty array is not the absent case, but a non-number is still not a
        // Boss HP value, and no identity can be read from it.
        { bossState: [] },
      ];

      for (const override of malformed) {
        const { runtime, transport } = createRuntime();
        await runtime.initialize();

        const events: string[] = [];
        runtime.onRuntimeEvent((e) => events.push(e.type));

        transport.emit('BattleStateUpdated', payload(override));

        expect(runtime.getBattleState()).toBeNull();
        expect(events).toContain('runtime_error');
      }
    });

    it('rejects an explicit null reset override rather than reading it as a default', async () => {
      // §4.3 item 7 / §3.2.5: the omission is the documented spelling of a
      // default reset, and no member of this contract is ever sent as JSON
      // `null`. A producer writing `null` is therefore outside the contract and
      // the payload is malformed — the runtime must not quietly reinterpret it
      // as "default", which would accept a second spelling of one fact.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const events: string[] = [];
      runtime.onRuntimeEvent((e) => events.push(e.type));

      transport.emit(
        'BattleStateUpdated',
        payload({
          petState: {
            hp: 1000,
            maxHp: 1000,
            power: 0,
            passiveId: 'xich-lang',
            passiveProgress: { threshold: 5, current: 0 },
            passiveResetOverride: null,
          },
        })
      );

      expect(runtime.getBattleState()).toBeNull();
      expect(events).toContain('runtime_error');
    });

    it('derives no Passive value on the client', async () => {
      // §4.3 item 9 / GAME_RULES.md §18: the client does not charge a Passive,
      // evaluate a Threshold, reset progress, or apply an overflow. The stored
      // pair is the delivered pair, whatever the surrounding state says.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const sent = payload({
        // A Combo, a Match total, a Turn, and a Sequence that carry no Passive
        // meaning — none of them may influence the stored progress.
        turn: 9,
        sequence: 21,
        playerState: { combo: 4, matchCount: 137 },
        petState: {
          hp: 1000,
          maxHp: 1000,
          power: 0,
          passiveId: 'xich-lang',
          passiveProgress: { threshold: 5, current: 3 },
          equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          statusEffects: [],
        },
      });

      transport.emit('BattleStateUpdated', sent);

      expect(runtime.getBattleState()!.petState.passiveProgress).toEqual({
        threshold: 5,
        current: 3,
      });
    });

    it('ignores a payload carrying an undocumented Status value', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      // No Status/lifecycle value exists in the protocol (§8.3); an unexpected
      // field is not modelled and not carried into the runtime's copy. The
      // board-foundation stage exposes exactly the enumerated §4 members.
      transport.emit('BattleStateUpdated', payload({ status: 'READY' }));

      expect(runtime.getBattleState()).toEqual(payload());
      expect(Object.keys(runtime.getBattleState()!)).toEqual([
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
    });

    it('advances no RNG and derives no Gem on the client', async () => {
      // SIGNALR_PROTOCOL.md §4.1 item 2: the RNG values are delivered because
      // they are BattleState fields, not because the client uses them.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const sent = payload();
      transport.emit('BattleStateUpdated', sent);

      // The rngState the client holds is exactly what the server sent — it was
      // not advanced, re-seeded, or recomputed.
      expect(runtime.getBattleState()!.rngState).toEqual(sent.rngState);

      // And the board is exactly the delivered board, not a client-derived one.
      expect(runtime.getBattleState()!.board.cells).toEqual(
        (sent.board as { cells: Array<{ gemType: string }> }).cells
      );
    });

    it('holds no battle state fields in the technical runtime state', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();
      transport.emit('BattleStateUpdated', payload());

      // The authoritative copy is exposed separately; the technical runtime
      // state contract stays technical (ARCHITECTURE.md §2.2.1 rule 5).
      for (const key of [
        'battleId',
        'turn',
        'sequence',
        'board',
        'rngSeed',
        'rngState',
        'playerState',
        'petState',
      ]) {
        expect(Object.keys(runtime.getState())).not.toContain(key);
      }
    });

    it('unsubscribes battle-state listeners', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const listener = vi.fn();
      const unsubscribe = runtime.onBattleState(listener);
      unsubscribe();

      transport.emit('BattleStateUpdated', payload());

      expect(listener).not.toHaveBeenCalled();
    });

    it('stops receiving state after disposal', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();
      await runtime.dispose();

      transport.emit('BattleStateUpdated', payload());

      expect(runtime.getBattleState()).toBeNull();
    });
  });

  describe('reconnect & resync recovery (SIGNALR_PROTOCOL.md §7, ADR-008)', () => {
    /** A `GET /api/battle/{battleId}/result` body (`API_CONTRACTS.md` §4). */
    function resultResponse() {
      return {
        battleId: 'battle-1',
        outcome: 'victory',
        rewards: {},
        durationTurns: 5,
      };
    }

    function createRecoveryRuntime() {
      const transport = new FakeSignalR();
      const api = { getBattleResult: vi.fn(async () => resultResponse()) };
      const runtime = new GameRuntime(transport as never, api as never);
      return { runtime, transport, api };
    }

    /** A joined, synchronized battle: what the §4 push established. */
    async function joinedRuntime(overrides: Record<string, unknown> = {}) {
      const context = createRecoveryRuntime();
      await context.runtime.initialize();
      context.transport.emit('BattleStateUpdated', payload(overrides));
      return context;
    }

    /** The documented §7.1 reconnect cycle. */
    function reconnect(transport: FakeSignalR): void {
      transport.handlers.onReconnecting?.(new Error('lost'));
      transport.handlers.onReconnected?.('conn-2');
    }

    it('requests the snapshot after a reconnect when a battle is known', async () => {
      const { transport } = await joinedRuntime({ battleId: 'battle-7' });

      reconnect(transport);

      // §7.1: on reconnect the client calls `GetBattleState(battleId)` — for the
      // battle the runtime already holds from the server's own state, never for
      // a caller-supplied or locally invented id.
      await vi.waitFor(() => {
        expect(transport.getBattleStateCalls).toEqual(['battle-7']);
      });
    });

    it('re-joins the battle group before requesting the snapshot', async () => {
      // SIGNALR_PROTOCOL.md §7 item 4 / §1.2 / §2: the connection a reconnect
      // produces is a member of no group, so the reconnected client must
      // re-invoke `JoinBattle(battleId)` before any post-reconnect delivery is
      // expected. `invokedMethods` records the hub calls in the order the runtime
      // made them, so the order of the two steps is asserted and not merely that
      // both happened.
      const { transport } = await joinedRuntime({ battleId: 'battle-7' });

      reconnect(transport);

      await vi.waitFor(() => {
        expect(transport.invokedMethods).toEqual(['JoinBattle', 'GetBattleState']);
      });

      // Both steps address the battle the runtime already holds from the
      // server's own push — never a caller-supplied or locally invented id.
      expect(transport.joinCalls).toEqual(['battle-7']);
      expect(transport.getBattleStateCalls).toEqual(['battle-7']);
    });

    it('awaits the re-join before recovery proceeds', async () => {
      // §7 item 4's ordering is an await, not a fire-and-forget: recovery must
      // not issue its snapshot request while the re-join is still in flight, or
      // the client would consider itself recovered on a connection that is not
      // yet in the group.
      const { transport } = await joinedRuntime({ battleId: 'battle-7' });

      const gate: { release: () => void } = { release: () => {} };
      const joinBattle = transport.joinBattle.bind(transport);
      const joinSpy = vi.spyOn(transport, 'joinBattle').mockImplementation(
        (battleId: string) =>
          new Promise<void>((resolve, reject) => {
            gate.release = () => {
              joinBattle(battleId).then(resolve, reject);
            };
          })
      );

      reconnect(transport);

      await vi.waitFor(() => {
        expect(joinSpy).toHaveBeenCalledWith('battle-7');
      });

      // The re-join is still unresolved, so recovery has not started.
      expect(transport.getBattleStateCalls).toEqual([]);

      gate.release();

      await vi.waitFor(() => {
        expect(transport.getBattleStateCalls).toEqual(['battle-7']);
      });
    });

    it('continues through the existing recovery path after the re-join', async () => {
      // The re-join is a step of the reconnect flow, not its end: §7.1's snapshot
      // request still follows it, and the recovered snapshot is ingested by the
      // same §4 path the push uses.
      const { runtime, transport } = await joinedRuntime({ battleId: 'battle-7' });
      const recovered = payload({ battleId: 'battle-7', turn: 3, sequence: 4 });
      transport.getBattleStateResult = { accepted: true, serverSequence: 4, state: recovered };

      reconnect(transport);

      await vi.waitFor(() => {
        expect(runtime.getBattleState()).toEqual(recovered);
      });

      expect(transport.invokedMethods).toEqual(['JoinBattle', 'GetBattleState']);
      expect(runtime.getState().sync).toBe('synchronized');
    });

    it('re-joins no group when no battle is known', async () => {
      const { runtime, transport } = createRecoveryRuntime();
      await runtime.initialize();

      reconnect(transport);

      await vi.waitFor(() => {
        expect(transport.invokedMethods).toEqual([]);
      });

      // There is no current battle, so there is no group to re-join: no
      // `JoinBattle` is issued, no snapshot is requested, and no battle is
      // fabricated to give either step a target.
      expect(transport.joinCalls).toEqual([]);
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getBattleState()).toBeNull();
    });

    it('reports a failed re-join and still recovers the snapshot', async () => {
      // A failed `JoinBattle` is a technical failure, not battle state: it is
      // reported through the existing error channel, and §7.1's snapshot is still
      // requested — it is a direct request/response and does not depend on group
      // membership, so a failed re-join must not additionally suppress the
      // authoritative resync.
      const { runtime, transport } = await joinedRuntime({ battleId: 'battle-7' });
      transport.joinBehaviour = async () => {
        throw new Error('join failed');
      };
      const recovered = payload({ battleId: 'battle-7', turn: 3, sequence: 4 });
      transport.getBattleStateResult = { accepted: true, serverSequence: 4, state: recovered };

      const reportedErrors: Array<string | null> = [];
      runtime.onRuntimeEvent((event) => {
        if (event.type === 'runtime_error') {
          reportedErrors.push(event.detail ?? null);
        }
      });

      reconnect(transport);

      await vi.waitFor(() => {
        expect(runtime.getBattleState()).toEqual(recovered);
      });

      // The failure is reported with its own technical detail — and the resync
      // that follows it clears the diagnostic state, because the client is
      // synchronized again and the reported failure was not battle state.
      expect(reportedErrors).toContain('join failed');
      expect(runtime.getState().lastError).toBeNull();
      expect(transport.getBattleStateCalls).toEqual(['battle-7']);
    });

    it('requests no snapshot when no battle is known', async () => {
      const { runtime, transport } = createRecoveryRuntime();
      await runtime.initialize();

      reconnect(transport);
      await Promise.resolve();

      // Nothing to recover: no request is issued and no battle is fabricated.
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getBattleState()).toBeNull();
      expect(runtime.getState().sync).toBe('awaiting_battle');
    });

    it('ingests the recovered snapshot through the existing §4 path', async () => {
      const { runtime, transport } = await joinedRuntime();
      const recovered = payload({
        turn: 5,
        sequence: 6,
        playerState: { combo: 2, matchCount: 11 },
      });
      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 6,
        state: recovered,
        reason: null,
      };

      const runtimeEvents: string[] = [];
      const recoveredStates: unknown[] = [];
      runtime.onRuntimeEvent((event) => runtimeEvents.push(event.type));
      runtime.onBattleState((state) => recoveredStates.push(state));

      reconnect(transport);

      await vi.waitFor(() => {
        expect(runtime.getBattleState()).toEqual(recovered);
      });

      // The same observable result the §4 push produces: `sync` reaches
      // 'synchronized', `battle_state_changed` is emitted, and the existing
      // battle-state listeners are dispatched — with no second ingestion path.
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtimeEvents).toContain('battle_state_changed');
      expect(recoveredStates).toEqual([recovered]);
    });

    it('recovers the same projection the §4 push carries, statusEffects and bossState included', async () => {
      // SIGNALR_PROTOCOL.md §7.1: the reconnect snapshot "is the same projection
      // the §4 push carries, member for member" — the record's own members,
      // `playerState`, `petState` including `statusEffects[]`, and `bossState`.
      // §7 therefore defines no member set of its own, and a recovered client must
      // be able to re-render the active Pet's Status Effects and the Boss's live
      // health from either path with one model (ADR-008).
      const { runtime, transport } = await joinedRuntime();

      const recovered = payload({
        turn: 7,
        sequence: 8,
        bossState: { bossId: 'boss-hoa-long', hp: 3100, maxHp: 5000, power: 40 },
        petState: {
          hp: 1000,
          maxHp: 1000,
          power: 0,
          passiveId: 'xich-lang',
          passiveProgress: { threshold: 5, current: 2 },
          equippedCards: ['card-heal', 'card-shield', 'card-power-charge', 'card-inferno'],
          statusEffects: [
            { id: 'Burn', type: 'DoT', source: 'boss', magnitude: 25, remainingTurns: 2 },
          ],
        },
      });

      transport.getBattleStateResult = { accepted: true, serverSequence: 8, state: recovered };

      reconnect(transport);

      await vi.waitFor(() => {
        expect(runtime.getBattleState()).toEqual(recovered);
      });

      const state = runtime.getBattleState()!;

      // The recovered Pet collection is the delivered one, element for element.
      expect(state.petState.statusEffects).toEqual([
        { id: 'Burn', type: 'DoT', source: 'boss', magnitude: 25, remainingTurns: 2 },
      ]);

      // The recovered Boss projection is the delivered one — the canonical
      // Identity, HP pair, and live Power — not re-derived from the stale copy
      // the runtime held before the reconnect, and not read off any event (§4.4 items 10, 12).
      expect(state.bossState).toEqual({ bossId: 'boss-hoa-long', hp: 3100, maxHp: 5000, power: 40 });

      // The three live Pet combat values are the delivered ones, so a resynchronizing
      // client re-renders the current HP and Power with no event replay
      // (§4.3 item 15, §7 item 2).
      expect(state.petState.hp).toBe(1000);
      expect(state.petState.maxHp).toBe(1000);
      expect(state.petState.power).toBe(0);

      // And the recovered state carries exactly the §4 member set: recovery adds
      // no member of its own, and the widened projections are present on it.
      expect(Object.keys(state)).toEqual([
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
      expect(Object.keys(state.petState).sort()).toEqual(
        ['equippedCards', 'hp', 'maxHp', 'passiveId', 'passiveProgress', 'power', 'statusEffects'].sort()
      );
    });

    it('replaces the stale runtime copy instead of merging into it', async () => {
      const { runtime, transport } = await joinedRuntime({
        turn: 0,
        sequence: 0,
        playerState: { combo: 0, matchCount: 0 },
      });

      const recovered = payload({
        turn: 4,
        sequence: 9,
        playerState: { combo: 4, matchCount: 12 },
        board: { cells: serverCells().fill({ gemType: 'POWER' }) },
      });
      transport.getBattleStateResult = { accepted: true, serverSequence: 9, state: recovered };

      reconnect(transport);

      await vi.waitFor(() => {
        expect(runtime.getBattleState()).toEqual(recovered);
      });

      // Exact equality is the assertion: a merge would leave a value from the
      // stale copy behind, and no local prediction survives §7.2.
      expect(runtime.getBattleState()!.turn).toBe(4);
      expect(runtime.getBattleState()!.playerState).toEqual({ combo: 4, matchCount: 12 });
    });

    it('refuses a snapshot that is behind the sequence already delivered', async () => {
      // `GAME_STATE.md` §5 (Sequence is the basis for the client's detection of
      // missed/out-of-order delivery) and §5.1 item 3 (`Sequence = n` is the
      // complete result of the n-th resolution): a snapshot behind the copy the
      // same battle has already delivered is older than what the client holds, so
      // recovery must not roll the synchronized state backward.
      //
      // This test replaces the previous assertion that a lower-sequence snapshot
      // was ingested verbatim. The ingestion guard now refuses it; the refusal
      // stores nothing, dispatches no listener, and is reported through the
      // existing technical channel, exactly as a malformed payload is.
      //
      // **It covers that refusal only.** It issues a client-requested recovery on a
      // reconnect and never withholds a batch, so it asserts nothing about
      // `GAME_STATE.md` §5's missed/out-of-order detection: a snapshot at or ahead
      // of the copy would be ingested here. The batch-gap detection and the
      // snapshot it requests are covered by this file's P0.4 suite.
      const { runtime, transport } = await joinedRuntime({ turn: 4, sequence: 9 });

      const recovered = payload({ turn: 2, sequence: 3 });
      transport.getBattleStateResult = { accepted: true, serverSequence: 3, state: recovered };

      const delivered: unknown[] = [];
      runtime.onBattleState((state) => delivered.push(state));

      reconnect(transport);

      await vi.waitFor(() => {
        expect(runtime.getState().lastError).toContain('behind the synchronized sequence 9');
      });

      // The newer copy the runtime holds survives the stale snapshot untouched.
      expect(runtime.getBattleState()!.sequence).toBe(9);
      expect(runtime.getBattleState()!.turn).toBe(4);
      expect(runtime.getBattleState()).toEqual(payload({ turn: 4, sequence: 9 }));

      // Nothing was re-rendered from a payload that was not stored, and no
      // synchronization is claimed for a resync that produced no usable snapshot —
      // the same outcome the malformed-snapshot case already has.
      expect(delivered).toEqual([]);
      expect(runtime.getState().sync).toBe('unsynchronized');
    });

    it('treats the battle as ended and falls back to the result route on BATTLE_NOT_FOUND', async () => {
      const { runtime, transport, api } = await joinedRuntime({ battleId: 'battle-9' });
      transport.getBattleStateResult = {
        accepted: false,
        serverSequence: null,
        state: null,
        reason: 'BATTLE_NOT_FOUND',
      };

      reconnect(transport);

      // §7.3: the documented fallback is the existing battle-result route
      // (API_CONTRACTS.md §4), addressed by the same battle.
      await vi.waitFor(() => {
        expect(api.getBattleResult).toHaveBeenCalledWith('battle-9');
      });

      // Treated as ended: no live battle remains, no state is fabricated to
      // stand in for the snapshot, and the runtime does not stay 'synchronized'
      // with a battle that no longer exists.
      expect(runtime.getBattleState()).toBeNull();
      expect(runtime.getState().sync).toBe('awaiting_battle');

      // The ended battle can no longer be addressed by an action.
      await expect(
        runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 })
      ).rejects.toThrow('No battle state is known');
    });

    it('exposes no store or ownership detail on BATTLE_NOT_FOUND', async () => {
      const { runtime, transport, api } = await joinedRuntime({ battleId: 'battle-9' });
      transport.getBattleStateResult = {
        accepted: false,
        serverSequence: null,
        state: null,
        reason: 'BATTLE_NOT_FOUND',
      };

      reconnect(transport);
      await vi.waitFor(() => {
        expect(api.getBattleResult).toHaveBeenCalled();
      });

      // REDIS_STATE.md §3 / API_CONTRACTS.md §4 notes 6–7: unknown, expired, and
      // foreign are one indistinguishable answer, so the client must not infer
      // (or report) which of them occurred.
      const reported = JSON.stringify(runtime.getState()).toLowerCase();
      for (const detail of ['redis', 'expired', 'foreign', 'ownership', 'store']) {
        expect(reported).not.toContain(detail);
      }
    });

    it('reports a malformed recovered snapshot instead of fabricating state', async () => {
      const { runtime, transport } = await joinedRuntime();

      // `sequence` missing — no default is substituted, exactly as the §4
      // reader already handles a malformed push.
      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 4,
        state: { battleId: 'battle-1', turn: 2 },
      };

      const runtimeEvents: string[] = [];
      runtime.onRuntimeEvent((event) => runtimeEvents.push(event.type));

      reconnect(transport);
      await vi.waitFor(() => {
        expect(runtimeEvents).toContain('runtime_error');
      });

      // The stale copy is neither replaced nor completed, and `sync` never
      // claims synchronization with a payload that was not read.
      expect(runtime.getBattleState()).toEqual(payload());
      expect(runtime.getState().sync).not.toBe('synchronized');
    });

    it('reports a failed recovery request without fabricating state', async () => {
      const { runtime, transport } = await joinedRuntime();
      transport.getBattleStateBehaviour = async () => {
        throw new Error('connection lost');
      };

      const runtimeEvents: string[] = [];
      runtime.onRuntimeEvent((event) => runtimeEvents.push(event.type));

      reconnect(transport);

      await vi.waitFor(() => {
        expect(runtime.getState().lastError).toBe('connection lost');
      });

      expect(runtimeEvents).toContain('runtime_error');
      expect(runtime.getBattleState()).toEqual(payload());
    });

    it('replays no missed events during recovery', async () => {
      const { runtime, transport } = await joinedRuntime();
      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 4,
        state: payload({ sequence: 4 }),
      };

      const batches: unknown[] = [];
      runtime.onBattleEvents((envelope) => batches.push(envelope));

      reconnect(transport);
      await vi.waitFor(() => {
        expect(runtime.getState().sync).toBe('synchronized');
      });

      // §7.2 / §8 item 8 / ADR-008: a desynchronized client resynchronizes from
      // a snapshot only — no missed events are requested, replayed, or
      // synthesized, and there is no event log to replay from.
      expect(batches).toEqual([]);
    });

    it('calculates no authoritative state and increments no server sequence', () => {
      // AGENTS.md §10 / ADR-001 / GAME_STATE.md §5.2 item 3: the recovered
      // values are the server's. The runtime holds them and derives nothing.
      const source = readFileSync(resolve(__dirname, '../src/game/runtime/GameRuntime.ts'), 'utf8')
        .replace(/\/\*[\s\S]*?\*\//g, '')
        .replace(/(^|[^:])\/\/.*$/gm, '$1');

      for (const forbidden of [
        'sequence++',
        'sequence +=',
        'serverSequence++',
        'serverSequence +=',
        'Math.random',
        'damage',
        'crit',
        'burn',
      ]) {
        expect(source, `GameRuntime must not contain "${forbidden}"`).not.toContain(forbidden);
      }
    });
  });

  describe('monotonic state ingestion guard (GAME_STATE.md §5, SIGNALR_PROTOCOL.md §4, §7)', () => {
    /**
     * P0.1. `receiveBattleState` is the single ingestion path for both the §4 push
     * and the §7.1 recovery snapshot, so these tests drive the guard through the
     * real runtime and the transport port: a well-formed payload is delivered
     * exactly as the server delivers it, and every assertion is on what the runtime
     * then holds, reports, and dispatches. No private field is mutated to
     * manufacture an outcome.
     *
     * The recovery-path case — a stale snapshot received during the documented §7
     * reconnect cycle — is covered by "refuses a snapshot that is behind the
     * sequence already delivered" in the reconnect & resync block above, which
     * exercises the same guard through the same `recoverBattleState` entry point.
     */

    /** A synchronized battle, established by the server's own §4 push. */
    async function synchronizedBattle(overrides: Record<string, unknown> = {}) {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();
      transport.emit(
        'BattleStateUpdated',
        payload({ battleId: 'battle-1', turn: 11, sequence: 11, ...overrides })
      );
      return { runtime, transport };
    }

    /** A `POST /api/battle/start` 200 body (API_CONTRACTS.md §3). */
    function startResponse(battleId: string) {
      return {
        battleId,
        signalrHub: '/hubs/battle',
        // Defined by the contract and deliberately never promoted into runtime
        // state: the §4 push is the only synchronization path (TASK-077).
        initialState: { battleId, turn: 0, sequence: 0 },
      };
    }

    /** The documented request selection (API_CONTRACTS.md §3). */
    const startRequest = {
      petId: 'pet-001',
      bossId: 'boss-hoa-long',
      cardLoadout: ['heal', 'shield', 'power_charge'],
      relicLoadout: ['relic-1', 'relic-2', 'relic-3'],
    };

    /** A runtime over the fake transport and a start-request double. */
    async function startedRuntime(battleId: string) {
      const transport = new FakeSignalR();
      const api = { startBattle: vi.fn(async () => startResponse(battleId)) };
      const runtime = new GameRuntime(transport as never, api as never);
      await runtime.initialize();
      return { runtime, transport, api };
    }

    it('refuses a payload whose sequence is behind the one already delivered', async () => {
      // Sequence 11 followed by sequence 10: the same battle, one complete
      // resolution behind the copy already delivered (GAME_STATE.md §5.1 item 3).
      const { runtime, transport } = await synchronizedBattle({
        playerState: { combo: 4, matchCount: 21 },
      });
      expect(runtime.getBattleState()!.sequence).toBe(11);

      const delivered: unknown[] = [];
      const errors: Array<string | null> = [];
      runtime.onBattleState((state) => delivered.push(state));
      runtime.onRuntimeEvent((event) => {
        if (event.type === 'runtime_error') {
          errors.push(event.detail ?? null);
        }
      });

      transport.emit(
        'BattleStateUpdated',
        payload({
          battleId: 'battle-1',
          turn: 10,
          sequence: 10,
          playerState: { combo: 3, matchCount: 17 },
        })
      );

      // Nothing rolled back: the copy is still the server's sequence 11.
      expect(runtime.getBattleState()!.sequence).toBe(11);
      expect(runtime.getBattleState()!.turn).toBe(11);
      expect(runtime.getBattleState()!.playerState).toEqual({ combo: 4, matchCount: 21 });

      // The refused payload is neither stored nor rendered from, and the refusal
      // is reported through the technical channel a malformed payload already
      // uses — the runtime holds the server's most recent state either way.
      expect(delivered).toEqual([]);
      expect(errors).toHaveLength(1);
      expect(errors[0]).toContain('sequence 10');
      expect(errors[0]).toContain('behind the synchronized sequence 11');
      expect(runtime.getState().lastError).toBe(errors[0]);
      expect(runtime.getState().sync).toBe('synchronized');
    });

    it('stores a payload that advances the sequence', async () => {
      // The guard is monotonic, not a freeze: the battle's next resolution is the
      // ordinary delivery and is stored as sent.
      const { runtime, transport } = await synchronizedBattle();

      transport.emit(
        'BattleStateUpdated',
        payload({
          battleId: 'battle-1',
          turn: 12,
          sequence: 12,
          playerState: { combo: 1, matchCount: 1 },
        })
      );

      expect(runtime.getBattleState()!.sequence).toBe(12);
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getState().lastError).toBeNull();
    });

    it('ingests a duplicate sequence idempotently and reports no error', async () => {
      const { runtime, transport } = await synchronizedBattle({
        playerState: { combo: 4, matchCount: 21 },
      });

      const delivered: unknown[] = [];
      const events: string[] = [];
      runtime.onBattleState((state) => delivered.push(state));
      runtime.onRuntimeEvent((event) => events.push(event.type));

      const duplicate = payload({
        battleId: 'battle-1',
        turn: 11,
        sequence: 11,
        playerState: { combo: 4, matchCount: 21 },
      });
      transport.emit('BattleStateUpdated', duplicate);

      // Applying the same complete resolution again is a re-application of the
      // same value rather than a state change (GAME_STATE.md §5.1 item 3).
      expect(runtime.getBattleState()).toEqual(duplicate);
      expect(runtime.getState().sync).toBe('synchronized');

      // An expected duplicate is not an anomaly: no error is reported for it and
      // no noisy log is introduced.
      expect(events).not.toContain('runtime_error');
      expect(runtime.getState().lastError).toBeNull();

      // Delivery is not suppressed. The record carries battle state only — its
      // exact member set (SIGNALR_PROTOCOL.md §4 item 4) holds no non-gameplay
      // metadata, and no `Status`/lifecycle value exists anywhere in the protocol
      // (§8.3) — so suppressing it would preserve nothing, while a recovering
      // client must still reach `synchronized` and re-render from its snapshot
      // even when that snapshot is level with the copy it holds (§7.2).
      expect(delivered).toEqual([duplicate]);
    });

    it('refuses state for a different battle while one is active', async () => {
      const { runtime, transport } = await synchronizedBattle({
        battleId: 'battle-1',
        turn: 3,
        sequence: 3,
      });

      const delivered: unknown[] = [];
      const errors: Array<string | null> = [];
      runtime.onBattleState((state) => delivered.push(state));
      runtime.onRuntimeEvent((event) => {
        if (event.type === 'runtime_error') {
          errors.push(event.detail ?? null);
        }
      });

      transport.emit(
        'BattleStateUpdated',
        payload({ battleId: 'battle-other', turn: 0, sequence: 0 })
      );

      // The active battle is untouched: a payload for another battle, at a lower
      // sequence, cannot replace it — and no sequence is assumed between the two
      // battles (GAME_STATE.md §5.2 item 3), so the refusal is the identity's, not
      // the version's.
      expect(runtime.getBattleState()!.battleId).toBe('battle-1');
      expect(runtime.getBattleState()!.sequence).toBe(3);
      expect(delivered).toEqual([]);
      expect(errors).toHaveLength(1);
      expect(errors[0]).toContain('battle-other');
      expect(errors[0]).toContain('no lifecycle transition established it');

      // The action boundary still addresses the battle that is actually active, so
      // a foreign payload cannot redirect a request either.
      await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
      expect(transport.swapCalls[0][0]).toBe('battle-1');
      expect(transport.swapCalls[0][3]).toBe(3);
    });

    it('accepts the new battle a lifecycle transition established, whatever its sequence', async () => {
      const { runtime, transport } = await startedRuntime('battle-b');

      // Battle A is active and synchronized at a sequence battle B cannot share:
      // B is a different battle and starts at 0 (GAME_STATE.md §2.0.2, §5).
      transport.emit(
        'BattleStateUpdated',
        payload({ battleId: 'battle-a', turn: 5, sequence: 5 })
      );

      const delivered: unknown[] = [];
      runtime.onBattleState((state) => delivered.push(state));

      // The documented transition: the REST step names the battle, then the group
      // join triggers that battle's own §4 push (SIGNALR_PROTOCOL.md §1 items 1–2).
      await runtime.startBattle(startRequest);

      transport.emit(
        'BattleStateUpdated',
        payload({ battleId: 'battle-b', turn: 0, sequence: 0 })
      );

      // The transition's own push replaced the battle that was held, even though
      // its sequence is lower than the one A had reached.
      expect(runtime.getBattleState()!.battleId).toBe('battle-b');
      expect(runtime.getBattleState()!.sequence).toBe(0);
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getState().lastError).toBeNull();
      expect(delivered).toHaveLength(1);

      // The action boundary follows the transition to the new battle.
      await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
      expect(transport.swapCalls[0][0]).toBe('battle-b');
    });

    it('keeps a still-valid active battle when a new battle cannot be started', async () => {
      const { runtime, transport } = await startedRuntime('battle-b');

      transport.emit(
        'BattleStateUpdated',
        payload({ battleId: 'battle-a', turn: 5, sequence: 5 })
      );
      transport.joinBehaviour = async () => {
        throw new Error('JoinBattle failed');
      };

      await expect(runtime.startBattle(startRequest)).rejects.toThrow('JoinBattle failed');

      // The still-valid active battle survives the failed transition: it was not
      // reset in advance of the new battle becoming enterable, and nothing was
      // fabricated to stand in for the battle that was never entered.
      expect(runtime.getBattleState()!.battleId).toBe('battle-a');
      expect(runtime.getBattleState()!.sequence).toBe(5);
      expect(runtime.getBattleState()!.turn).toBe(5);
      expect(runtime.getState().sync).toBe('synchronized');

      // The failed start established no identity, so a payload for the battle it
      // had named is still a cross-battle payload and cannot replace A.
      const errors: Array<string | null> = [];
      runtime.onRuntimeEvent((event) => {
        if (event.type === 'runtime_error') {
          errors.push(event.detail ?? null);
        }
      });

      transport.emit(
        'BattleStateUpdated',
        payload({ battleId: 'battle-b', turn: 0, sequence: 0 })
      );

      expect(runtime.getBattleState()!.battleId).toBe('battle-a');
      expect(runtime.getBattleState()!.sequence).toBe(5);
      expect(errors[0]).toContain('no lifecycle transition established it');
    });
  });

  describe('client → server request boundary', () => {
    /**
     * TASK-069 stage advance. This block previously asserted only that
     * `requestAction({ kind: 'Swap' })` rejects with
     * `RuntimeActionNotImplementedError`, because no gameplay action was
     * implemented at all. `SIGNALR_PROTOCOL.md` §2.1's Swap contract existed in
     * docs and on the server (`BattleHub.Swap`) with no client half; TASK-069
     * implements it, so the Swap assertions are replaced by the documented path.
     *
     * Every property that still holds is preserved below: an unknown action
     * kind, `CardCast`, `PetSkillCast`, and `GetBattleState` all still reject,
     * and no client-side gameplay is introduced.
     */

    /**
     * A joined battle: the runtime holds the state the server pushed
     * (SIGNALR_PROTOCOL.md §4), which is where it sources `battleId`.
     */
    async function joinedRuntime() {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-42' }));
      return { runtime, transport };
    }

    it('routes the documented Swap action to the transport', async () => {
      const { runtime, transport } = await joinedRuntime();

      const acknowledgement = await runtime.requestAction({
        kind: 'Swap',
        fromCell: 12,
        toCell: 13,
      });

      // MATCH3_RULES.md §2.1.1 / SIGNALR_PROTOCOL.md §2.1 / ADR-025: the documented
      // arguments, in order — the battle, the two §1.0 cell indices, the state
      // precondition the runtime observed, and the identity of this logical action.
      expect(transport.swapCalls).toHaveLength(1);
      const [battleId, fromCell, toCell, expectedSequence, actionId] = transport.swapCalls[0];

      expect(battleId).toBe('battle-42');
      expect(fromCell).toBe(12);
      expect(toCell).toBe(13);
      expect(expectedSequence).toBe(0);
      expect(typeof actionId).toBe('string');
      expect(actionId!.length).toBeGreaterThan(0);

      // §5: the acknowledgement is returned to the caller unchanged.
      expect(acknowledgement).toEqual({ accepted: true });
    });

    it('sources battleId from the state the server pushed, not the caller', async () => {
      const { runtime, transport } = await joinedRuntime();

      // The request shape carries no battle id at all (SIGNALR_PROTOCOL.md
      // §2.1: the transport identifies the battle), so the runtime can only use
      // the one it received.
      await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });

      expect(transport.swapCalls[0][0]).toBe('battle-42');
    });

    it('keeps the action identity separate from the state precondition', async () => {
      // ADR-025 §1–§2: the two values are different things and are supplied
      // separately. The precondition is the state version the runtime holds; the
      // action identity is not a server number at all, so it is never equal to the
      // sequence it accompanies — and the server never requires it to be.
      const { runtime, transport } = await joinedRuntime();

      const state = runtime.getBattleState()!;
      expect(state.sequence).toBe(0);

      await runtime.requestAction({ kind: 'Swap', fromCell: 4, toCell: 5 });

      const [battleId, , , expectedSequence, actionId] = transport.swapCalls[0];

      expect(battleId).toBe(state.battleId);
      expect(expectedSequence).toBe(state.sequence);
      expect(actionId).not.toBe(String(state.sequence));
      expect(actionId).not.toContain(':');
      expect(actionId).not.toMatch(/^[0-9]+$/);
    });

    it('generates a distinct action identity per logical action', async () => {
      const { runtime, transport } = await joinedRuntime();

      await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
      await runtime.requestAction({ kind: 'Swap', fromCell: 2, toCell: 3 });

      const first = transport.swapCalls[0][4];
      const second = transport.swapCalls[1][4];

      expect(first).not.toBe(second);
    });

    it('preserves one logical action’s identity for a retry after a transport failure', async () => {
      // ADR-025 §2: the identity is what the server deduplicates on, so a retry of
      // one logical action must carry the identity its first attempt carried. A
      // transport failure is not an answer (SIGNALR_PROTOCOL.md §8.3), so the
      // action stays unresolved and its identity is retained — and the retry that
      // names the same two cells in the other order is the same action
      // (MATCH3_RULES.md §2.1.1 item 2).
      const { runtime, transport } = await joinedRuntime();
      transport.swapBehaviour = async () => {
        throw new Error('SignalR connection is not established.');
      };

      await expect(
        runtime.requestAction({ kind: 'Swap', fromCell: 12, toCell: 13 })
      ).rejects.toThrow('SignalR connection is not established.');

      transport.swapBehaviour = null;

      await runtime.requestAction({ kind: 'Swap', fromCell: 13, toCell: 12 });

      expect(transport.swapCalls).toHaveLength(2);
      expect(transport.swapCalls[1][4]).toBe(transport.swapCalls[0][4]);
    });

    it('gives a new logical action a new identity once the previous one is settled', async () => {
      // The first submission was acknowledged, so the same pair submitted again is
      // a new logical action — not a retry of the first — and must not reuse its
      // identity, which the server would answer as a replay instead of executing
      // the player's new intent (ADR-025 §2).
      const { runtime, transport } = await joinedRuntime();

      await runtime.requestAction({ kind: 'Swap', fromCell: 12, toCell: 13 });
      await runtime.requestAction({ kind: 'Swap', fromCell: 12, toCell: 13 });

      expect(transport.swapCalls[1][4]).not.toBe(transport.swapCalls[0][4]);
    });

    it('never reuses an action identity from an earlier client runtime', async () => {
      // A page reload is a fresh module evaluation and a fresh runtime. A counter
      // initialised at module scope would restart and give the first swap of the
      // same pair the identity the earlier runtime used, which the server reads as
      // a retry of that action — the swap would be answered as a replay and never
      // executed (ADR-025 §2). The identity must therefore not be a counter's.
      const { runtime, transport } = await joinedRuntime();

      await runtime.requestAction({ kind: 'Swap', fromCell: 12, toCell: 13 });
      const beforeReload = transport.swapCalls[0][4];
      expect(beforeReload).toBeTruthy();

      vi.resetModules();
      const reloadedModule = await import('../src/game/runtime/GameRuntime');
      const reloadedTransport = new FakeSignalR();
      const reloadedRuntime = new reloadedModule.GameRuntime(reloadedTransport as never);

      await reloadedRuntime.initialize();
      reloadedTransport.emit('BattleStateUpdated', payload({ battleId: 'battle-42' }));

      await reloadedRuntime.requestAction({ kind: 'Swap', fromCell: 12, toCell: 13 });

      expect(reloadedTransport.swapCalls).toHaveLength(1);
      expect(reloadedTransport.swapCalls[0][4]).not.toBe(beforeReload);
    });

    it('resolves with a rejection without mutating any state', async () => {
      // SIGNALR_PROTOCOL.md §5 item 2 / MATCH3_RULES.md §2.1.5: a rejected swap
      // is a gameplay no-op. The client mutates nothing, and the reason is
      // surfaced as received.
      const { runtime, transport } = await joinedRuntime();
      transport.swapResult = { accepted: false, reason: 'NO_MATCH_FROM_SWAP' };

      const before = runtime.getBattleState();

      const acknowledgement = await runtime.requestAction({
        kind: 'Swap',
        fromCell: 12,
        toCell: 13,
      });

      expect(acknowledgement).toEqual({ accepted: false, reason: 'NO_MATCH_FROM_SWAP' });
      // Board, turn, and sequence are exactly what the server pushed: the
      // runtime recomputed nothing and derived nothing.
      expect(runtime.getBattleState()).toEqual(before);
      expect(runtime.getBattleState()!.board.cells).toEqual(before!.board.cells);
    });

    it('reports the unknown-battle rejection the hub returns', async () => {
      // BattleHub.Swap's unknown-battle path returns accepted:false with
      // BATTLE_NOT_FOUND. The runtime passes it through uninterpreted.
      const { runtime, transport } = await joinedRuntime();
      transport.swapResult = { accepted: false, reason: 'BATTLE_NOT_FOUND' };

      const acknowledgement = await runtime.requestAction({
        kind: 'Swap',
        fromCell: 0,
        toCell: 8,
      });

      expect(acknowledgement.reason).toBe('BATTLE_NOT_FOUND');
    });

    it('sends no request when no battle state is known', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      await expect(
        runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 })
      ).rejects.toThrow(/No battle state is known/);

      expect(transport.swapCalls).toEqual([]);
    });

    it('propagates a transport failure without mutating state', async () => {
      const { runtime, transport } = await joinedRuntime();
      transport.swapBehaviour = async () => {
        throw new Error('SignalR connection is not established.');
      };

      await expect(
        runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 })
      ).rejects.toThrow('SignalR connection is not established.');

      // A failed request is a transport failure — presentation state, not game
      // state (SIGNALR_PROTOCOL.md §8.3).
      expect(runtime.getBattleState()!.battleId).toBe('battle-42');
    });

    it('routes the documented CardCast action to the transport', async () => {
      const { runtime, transport } = await joinedRuntime();

      const acknowledgement = await runtime.requestAction({
        kind: 'CardCast',
        cardId: 'card-heal',
      });

      expect(transport.cardCastCalls).toHaveLength(1);
      const [battleId, cardId, clientSequence] = transport.cardCastCalls[0];
      expect(battleId).toBe('battle-42');
      expect(cardId).toBe('card-heal');
      expect(typeof clientSequence).toBe('string');
      expect(clientSequence!.length).toBeGreaterThan(0);
      expect(acknowledgement).toEqual({ accepted: true });
    });

    it('rejects CardCast when cardId is missing or empty', async () => {
      const { runtime } = await joinedRuntime();

      await expect(
        runtime.requestAction({ kind: 'CardCast', cardId: '' })
      ).rejects.toThrow('CardCast action requires a non-empty cardId.');
    });

    it('routes the documented PetSkillCast action to the transport', async () => {
      const { runtime, transport } = await joinedRuntime();

      const acknowledgement = await runtime.requestAction({
        kind: 'PetSkillCast',
      });

      expect(transport.petSkillCastCalls).toHaveLength(1);
      const [battleId, clientSequence] = transport.petSkillCastCalls[0];
      expect(battleId).toBe('battle-42');
      expect(typeof clientSequence).toBe('string');
      expect(clientSequence!.length).toBeGreaterThan(0);
      expect(acknowledgement).toEqual({ accepted: true });
    });

    it('still rejects every action kind the client does not implement', async () => {
      const { runtime, transport } = await joinedRuntime();

      // `GetBattleState` (SIGNALR_PROTOCOL.md §7) and unmodelled actions remain
      // unimplemented and must stay unavailable on the client.
      for (const kind of ['GetBattleState', 'Unknown', 'Surrender']) {
        await expect(runtime.requestAction({ kind } as never)).rejects.toBeInstanceOf(
          RuntimeActionNotImplementedError
        );
      }

      expect(transport.swapCalls).toEqual([]);
      expect(transport.cardCastCalls).toEqual([]);
      expect(transport.petSkillCastCalls).toEqual([]);
    });
  });

  describe('battle start orchestration (SIGNALR_PROTOCOL.md §1 items 1–2, §2)', () => {
    /**
     * TASK-077. The documented start sequence is exactly:
     *
     * ```text
     * ApiService.startBattle(request)     POST /api/battle/start
     *         ↓
     * SignalRService.connect(signalrHub)  §1 item 2
     *         ↓
     * SignalRService.joinBattle(battleId) §1.2, §2 JoinBattle
     * ```
     *
     * The REST response's `initialState` is deliberately never read as battle
     * state: the only synchronization path is the §4 push, handled by the
     * existing `receiveBattleState`.
     */

    /** A `POST /api/battle/start` 200 body (API_CONTRACTS.md §3). */
    function startResponse(overrides: Record<string, unknown> = {}) {
      return {
        battleId: 'server-battle-7',
        signalrHub: '/hubs/battle',
        // Present because the contract defines it, so these tests can assert it
        // is never promoted into runtime state. Its values are deliberately
        // distinguishable from anything the push later delivers.
        initialState: {
          battleId: 'server-battle-7',
          turn: 0,
          sequence: 0,
          rngSeed: 999,
          rngState: { state: 5, increment: 1 },
          board: { cells: serverCells() },
          combo: 0,
          matchCount: 0,
          petState: { passiveId: 'from-rest-initial-state' },
          bossState: { bossId: 'boss-hoa-long' },
        },
        ...overrides,
      };
    }

    /** A documented request selection (API_CONTRACTS.md §3). */
    const request = {
      petId: 'pet-001',
      bossId: 'boss-hoa-long',
      cardLoadout: ['heal', 'shield', 'power_charge'],
      relicLoadout: ['relic-1', 'relic-2', 'relic-3'],
    };

    /**
     * A runtime with a REST double. `ApiService` is injected the same way the
     * transport is, so no live server is involved.
     */
    function createOrchestratedRuntime() {
      const transport = new FakeSignalR();
      const api = {
        startBattle: vi.fn(async () => startResponse()),
      };
      const runtime = new GameRuntime(transport as never, api as never);
      return { runtime, transport, api };
    }

    describe('the documented sequence', () => {
      it('starts a battle through REST, connect, then join — in that order', async () => {
        const { runtime, transport, api } = createOrchestratedRuntime();

        const order: string[] = [];
        api.startBattle.mockImplementation(async () => {
          order.push('rest');
          return startResponse();
        });
        const originalConnect = transport.connect.bind(transport);
        transport.connect = async (url: string) => {
          order.push('connect');
          return await originalConnect(url);
        };
        const originalJoin = transport.joinBattle.bind(transport);
        transport.joinBattle = async (battleId: string) => {
          order.push('join');
          return await originalJoin(battleId);
        };

        await runtime.startBattle(request);

        expect(order).toEqual(['rest', 'connect', 'join']);
      });

      it('submits the request to the documented endpoint through ApiService', async () => {
        const { runtime, api } = createOrchestratedRuntime();

        await runtime.startBattle(request);

        // The selection is transported unchanged — the client validates no
        // gameplay rule and decides no accept/reject (API_CONTRACTS.md §3).
        expect(api.startBattle).toHaveBeenCalledTimes(1);
        expect(api.startBattle).toHaveBeenCalledWith(request);
      });

      it("connects using the response's signalrHub", async () => {
        const { runtime, transport, api } = createOrchestratedRuntime();
        api.startBattle.mockResolvedValue(startResponse({ signalrHub: '/hubs/custom-battle' }));

        await runtime.startBattle(request);

        expect(transport.connectCalls).toEqual(['/hubs/custom-battle']);
      });

      it("joins using the server's battleId, not a client-derived one", async () => {
        const { runtime, transport, api } = createOrchestratedRuntime();
        api.startBattle.mockResolvedValue(startResponse({ battleId: 'server-owned-id' }));

        await runtime.startBattle(request);

        // SIGNALR_PROTOCOL.md §1 item 1 / GAME_RULES.md §18: the server owns
        // BattleId. It is never derived from the request, from `initialState`,
        // or from a locally generated identifier.
        expect(transport.joinCalls).toEqual(['server-owned-id']);
        expect(transport.joinCalls[0]).not.toBe(request.petId);
      });

      it('resolves after the join, without awaiting the state push', async () => {
        const { runtime, transport } = createOrchestratedRuntime();
        await runtime.initialize();

        // Nothing has pushed state, yet the start has resolved.
        await expect(runtime.startBattle(request)).resolves.toBeUndefined();

        expect(transport.joinCalls).toHaveLength(1);
        expect(runtime.getState().sync).not.toBe('synchronized');
        expect(runtime.getBattleState()).toBeNull();
      });

      it('invokes no hub method other than the documented group join', async () => {
        const { runtime, transport } = createOrchestratedRuntime();

        await runtime.startBattle(request);

        // `JoinBattle` is the one non-gameplay client → server call (§2). The
        // gameplay methods and the reconnect snapshot (`GetBattleState`) are not
        // part of starting a battle.
        expect(transport.joinCalls).toHaveLength(1);
        expect(transport.swapCalls).toEqual([]);
        expect(transport.invokedMethods).toEqual(['JoinBattle']);
      });
    });

    describe('the single synchronization path', () => {
      it('is unsynchronized until the server pushes, even after a successful start', async () => {
        const { runtime, transport } = createOrchestratedRuntime();

        await runtime.startBattle(request);

        // SIGNALR_PROTOCOL.md §4: the state arrives as an unsolicited push on
        // join. The REST `initialState` is not that push.
        expect(runtime.getState().sync).not.toBe('synchronized');
        expect(runtime.getBattleState()).toBeNull();

        transport.emit('BattleStateUpdated', payload({ battleId: 'server-battle-7' }));

        expect(runtime.getState().sync).toBe('synchronized');
        expect(runtime.getBattleState()).not.toBeNull();
      });

      it('never promotes the REST initialState into runtime state', async () => {
        // TASK-077's critical requirement: `response.initialState` must not be
        // stored, merged, rendered, or used to set `sync`. The runtime state is
        // established only by the §4 push.
        const { runtime, transport } = createOrchestratedRuntime();
        await runtime.initialize();

        await runtime.startBattle(request);

        expect(runtime.getBattleState()).toBeNull();
        // A connected runtime that has not yet received the push: `awaiting_battle`
        // is the documented state, and it is set by the connection lifecycle — not
        // by the REST response (GameRuntimeState.ts SyncStatus).
        expect(runtime.getState().sync).toBe('awaiting_battle');

        // The pushed state is what the runtime holds — the REST summary's
        // distinguishing value never appears.
        transport.emit('BattleStateUpdated', payload({ battleId: 'server-battle-7' }));

        const state = runtime.getBattleState()!;
        expect(state.petState.passiveId).toBe('xich-lang');
        expect(JSON.stringify(state)).not.toContain('from-rest-initial-state');
        expect(state.rngSeed).toBe(42);
      });

      it('notifies battle-state listeners only when the push arrives', async () => {
        const { runtime, transport } = createOrchestratedRuntime();
        const listener = vi.fn();
        runtime.onBattleState(listener);

        await runtime.startBattle(request);
        expect(listener).not.toHaveBeenCalled();

        transport.emit('BattleStateUpdated', payload());
        expect(listener).toHaveBeenCalledTimes(1);
      });

      it('leaves the TASK-069 swap path working against the pushed battleId', async () => {
        // A successful start must not disturb the existing request boundary:
        // `requestAction` sources `battleId` from the pushed state.
        const { runtime, transport } = createOrchestratedRuntime();

        await runtime.startBattle(request);
        transport.emit('BattleStateUpdated', payload({ battleId: 'pushed-battle' }));

        const acknowledgement = await runtime.requestAction({
          kind: 'Swap',
          fromCell: 12,
          toCell: 13,
        });

        expect(acknowledgement).toEqual({ accepted: true });
        expect(transport.swapCalls[0][0]).toBe('pushed-battle');
      });
    });

    describe('subscription safety', () => {
      it('registers both documented subscriptions exactly once when initialize succeeded', async () => {
        const { runtime, transport } = createOrchestratedRuntime();
        await runtime.initialize();

        await runtime.startBattle(request);

        // Case A: `connect` is idempotent and no second registration happens.
        expect(transport.subscriptionRegistrations('ReceiveEvents')).toBe(1);
        expect(transport.subscriptionRegistrations('BattleStateUpdated')).toBe(1);
        expect(transport.subscriptions.size).toBe(2);
      });

      it('registers the subscriptions when initialize failed before subscribing', async () => {
        // Case B — the critical regression: `initialize()` subscribes only after a
        // successful connect, so a previously failed initialization leaves a later
        // connection without handlers. Without this the join-triggered push would
        // be silently missed.
        const { runtime, transport } = createOrchestratedRuntime();
        transport.connectBehaviour = async () => {
          throw new Error('backend unreachable');
        };

        await runtime.initialize();
        expect(transport.subscriptions.size).toBe(0);

        transport.connectBehaviour = async () => {};

        await runtime.startBattle(request);

        expect(transport.subscriptionRegistrations('ReceiveEvents')).toBe(1);
        expect(transport.subscriptionRegistrations('BattleStateUpdated')).toBe(1);
        expect(transport.joinCalls).toEqual(['server-battle-7']);

        // The push that the join triggered is observed.
        transport.emit('BattleStateUpdated', payload());
        expect(runtime.getBattleState()).toEqual(payload());
        expect(runtime.getState().sync).toBe('synchronized');
      });

      it('does not re-register when a start follows an already-subscribed runtime', async () => {
        // Case C: no duplicates, however the connection was obtained.
        const { runtime, transport } = createOrchestratedRuntime();

        await runtime.initialize();
        await runtime.startBattle(request);

        expect(transport.subscriptionRegistrations('ReceiveEvents')).toBe(1);
        expect(transport.subscriptionRegistrations('BattleStateUpdated')).toBe(1);
      });

      it('opens no second physical connection when already connected', async () => {
        // ARCHITECTURE.md §2.2.1 rule 6 — one runtime, one connection. The runtime
        // calls `connect(response.signalrHub)` unconditionally and the service
        // remains the single idempotency point.
        const { runtime, transport } = createOrchestratedRuntime();
        await runtime.initialize();
        expect(transport.physicalConnections).toBe(1);

        await runtime.startBattle(request);

        expect(transport.connectCalls).toHaveLength(2);
        expect(transport.physicalConnections).toBe(1);
      });
    });

    describe('failure semantics', () => {
      /**
       * The documented REST rejections (API_CONTRACTS.md §2.3, §3) plus transport
       * errors. Each must propagate with no connect, no join, and untouched state.
       */
      const restFailures: Array<[string, string]> = [
        ['401', 'Request to /api/battle/start failed with status 401'],
        ['400 INVALID_LOADOUT', 'Request to /api/battle/start failed with status 400'],
        ['400 PET_NOT_OWNED', 'Request to /api/battle/start failed with status 400'],
        ['400 BOSS_NOT_FOUND', 'Request to /api/battle/start failed with status 400'],
        ['network error', 'Failed to fetch'],
      ];

      it.each(restFailures)('propagates a %s from the start request', async (_case, message) => {
        const { runtime, transport, api } = createOrchestratedRuntime();
        await runtime.initialize();
        api.startBattle.mockRejectedValue(new Error(message));

        await expect(runtime.startBattle(request)).rejects.toThrow(message);

        // No battle exists after a rejection, so nothing is opened or joined and
        // no state is fabricated.
        expect(transport.connectCalls).toHaveLength(1);
        expect(transport.joinCalls).toEqual([]);
        expect(runtime.getBattleState()).toBeNull();
        expect(runtime.getState().sync).not.toBe('synchronized');
      });

      it('propagates a connect failure without joining or fabricating state', async () => {
        const { runtime, transport } = createOrchestratedRuntime();
        transport.connectBehaviour = async () => {
          throw new Error('hub unreachable');
        };

        await expect(runtime.startBattle(request)).rejects.toThrow('hub unreachable');

        expect(transport.joinCalls).toEqual([]);
        expect(runtime.getBattleState()).toBeNull();
        expect(runtime.getState().sync).not.toBe('synchronized');
      });

      it('propagates a join failure and fabricates no battle state', async () => {
        const { runtime, transport } = createOrchestratedRuntime();
        transport.joinBehaviour = async () => {
          throw new Error('JoinBattle failed');
        };

        await expect(runtime.startBattle(request)).rejects.toThrow('JoinBattle failed');

        // The join is the last documented step, so a failure there means no
        // battle was entered: `sync` and `battleState` stay untouched.
        expect(runtime.getBattleState()).toBeNull();
        expect(runtime.getState().sync).not.toBe('synchronized');
      });

      it('rejects a start after disposal without connecting or joining', async () => {
        const { runtime, transport } = createOrchestratedRuntime();
        await runtime.initialize();
        await runtime.dispose();

        await expect(runtime.startBattle(request)).rejects.toThrow(/disposed/i);

        // 'connect' is called only once — by the initialize above — and the join
        // never happens.
        expect(transport.connectCalls).toHaveLength(1);
        expect(transport.joinCalls).toEqual([]);
        expect(runtime.getBattleState()).toBeNull();
        expect(runtime.getState().sync).not.toBe('synchronized');
      });

      it('rejects when the join is attempted without a connection', async () => {
        // The fake preserves production behaviour: `joinBattle` fails when
        // disconnected, exactly as `SignalRService.joinBattle` does.
        const { runtime, transport } = createOrchestratedRuntime();
        transport.connectBehaviour = async () => {
          throw new Error('SignalR connection is not established.');
        };

        await expect(runtime.startBattle(request)).rejects.toThrow(
          'SignalR connection is not established.'
        );

        expect(transport.joinCalls).toEqual([]);
      });
    });
  });

  describe('pre-battle collection reads (ARCHITECTURE.md §2.2.3 rules 3–4, 6)', () => {
    /**
     * TASK-078. The pre-battle selection flow needs the owned collection, and
     * `ARCHITECTURE.md` §2.2.3 rule 3 routes it through the runtime port rather
     * than letting `LobbyScene` reach `services/api/`. These tests assert the
     * port capability is pure delegation: no orchestration, no caching, no
     * reordering, and no state held on the runtime.
     */

    /** A `GET /api/pets` element (API_CONTRACTS.md §5.1). */
    const petElement = {
      petId: 'pet-instance-1',
      identity: 'Xích Lang',
      element: 'Fire',
      tier: 'Common',
      star: 1,
      level: 1,
    };

    /** A `GET /api/cards` element (API_CONTRACTS.md §5.3). */
    const cardElement = { cardId: 'card-heal', name: 'Heal', category: 'Basic' };

    /** A `GET /api/relics` element (API_CONTRACTS.md §5.4). */
    const relicElement = { relicId: 'relic-instance-1', name: 'Berserker Core' };

    /** A runtime with collection-read doubles on the injected `ApiService`. */
    function createCollectionRuntime() {
      const transport = new FakeSignalR();
      const api = {
        getPets: vi.fn(async () => [petElement]),
        getPet: vi.fn(async () => petElement),
        getCards: vi.fn(async () => [cardElement]),
        getRelics: vi.fn(async () => [relicElement]),
      };
      const runtime = new GameRuntime(transport as never, api as never);
      return { runtime, transport, api };
    }

    it('delegates getPets to the existing ApiService method', async () => {
      const { runtime, api } = createCollectionRuntime();

      await expect(runtime.getPets()).resolves.toEqual([petElement]);
      expect(api.getPets).toHaveBeenCalledTimes(1);
    });

    it('delegates getPet with the instance id it was given', async () => {
      const { runtime, api } = createCollectionRuntime();

      // §5.2 addresses the detail route by the owned instance identity; the
      // runtime passes it on unchanged and adds no lookup of its own.
      await expect(runtime.getPet('pet-instance-7')).resolves.toEqual(petElement);
      expect(api.getPet).toHaveBeenCalledWith('pet-instance-7');
    });

    it('delegates getCards to the existing ApiService method', async () => {
      const { runtime, api } = createCollectionRuntime();

      await expect(runtime.getCards()).resolves.toEqual([cardElement]);
      expect(api.getCards).toHaveBeenCalledTimes(1);
    });

    it('delegates getRelics to the existing ApiService method', async () => {
      const { runtime, api } = createCollectionRuntime();

      await expect(runtime.getRelics()).resolves.toEqual([relicElement]);
      expect(api.getRelics).toHaveBeenCalledTimes(1);
    });

    it('returns the collection in the order the server sent it', async () => {
      // §5.5 defines no ordering and forbids relying on one, so the runtime must
      // not impose one either — a sort here would become loadout ordering the
      // moment a scene submitted it (RELIC_RULES.md §2.3).
      const { runtime, api } = createCollectionRuntime();
      const sent = [
        { relicId: 'relic-z', name: 'Z' },
        { relicId: 'relic-a', name: 'A' },
        { relicId: 'relic-m', name: 'M' },
      ];
      api.getRelics.mockResolvedValue(sent as never);

      const received = await runtime.getRelics();

      expect(received.map((r) => r.relicId)).toEqual(['relic-z', 'relic-a', 'relic-m']);
      // And the array reaches the caller without being re-sorted into the
      // server's own response order or any alphabetic order.
      expect(received.map((r) => r.relicId)).not.toEqual([...sent.map((r) => r.relicId)].sort());
    });

    it('propagates a collection-read rejection unchanged', async () => {
      // A `401 UNAUTHENTICATED` (API_CONTRACTS.md §2.3) reaches the caller as the
      // transport raised it; the runtime records nothing and fabricates nothing.
      const { runtime, api } = createCollectionRuntime();
      api.getPets.mockRejectedValue(
        new Error('Request to /api/pets failed with status 401')
      );

      await expect(runtime.getPets()).rejects.toThrow('status 401');

      expect(runtime.getBattleState()).toBeNull();
    });

    it('holds no collection state on the runtime', async () => {
      const { runtime } = createCollectionRuntime();

      await runtime.getPets();
      await runtime.getCards();
      await runtime.getRelics();

      // The read is a selection SOURCE, not a selection (ARCHITECTURE.md §2.2.3
      // rule 4): the selected items belong to the scene. Nothing about the
      // collection is kept here, so a scene cannot recover a selection from the
      // runtime — not even in the technical state contract.
      for (const key of ['pets', 'cards', 'relics', 'collection', 'loadout', 'selection']) {
        expect(Object.keys(runtime.getState())).not.toContain(key);
      }
      expect(Object.keys(runtime.getState()).sort()).toEqual([
        'connection',
        'connectionId',
        'engine',
        'lastError',
        'runtime',
        'session',
        'sync',
      ]);
    });
  });

  describe('battle history read (API_CONTRACTS.md §4.5, TASK-206)', () => {
    /**
     * TASK-206. The Battle History surface reads the Player's completed battles
     * through the runtime port (`ARCHITECTURE.md` §2.2.1 rule 1, §2.2.3 rule 3).
     * These tests assert the capability is pure delegation: no orchestration, no
     * caching, no reordering, no state held, and no computation of any member.
     */

    /** Two `GET /api/battle/history` elements, newest first (§4.5 note 4). */
    const newest = {
      battleId: 'battle-zzz',
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
      durationTurns: 12,
      completedAt: '2026-10-05T09:15:00Z',
    };
    const older = {
      battleId: 'battle-aaa',
      outcome: 'defeat' as const,
      rewards: {
        playerXpGained: 0,
        newPlayerXp: 300,
        playerLeveledUp: false,
        newPlayerLevel: 4,
        petXpGained: 0,
        newPetXp: 900,
        petLeveledUp: false,
        newPetLevel: 10,
      },
      durationTurns: 3,
      completedAt: '2026-10-04T21:00:00Z',
    };

    /** A runtime with a history-read double on the injected `ApiService`. */
    function createHistoryRuntime() {
      const transport = new FakeSignalR();
      const api = {
        getBattleHistory: vi.fn(async () => [newest, older]),
      };
      const runtime = new GameRuntime(transport as never, api as never);
      return { runtime, transport, api };
    }

    it('delegates getBattleHistory to the existing ApiService method', async () => {
      const { runtime, api } = createHistoryRuntime();

      await expect(runtime.getBattleHistory()).resolves.toEqual([newest, older]);
      expect(api.getBattleHistory).toHaveBeenCalledTimes(1);
    });

    it('sends no argument: the scope is the session, and there is no query parameter', async () => {
      const { runtime, api } = createHistoryRuntime();

      await runtime.getBattleHistory();

      // §4.5 notes 5–6 and 8: no pagination, filter, sort, or search parameter
      // exists, and no client input selects the Player.
      expect(api.getBattleHistory).toHaveBeenCalledWith();
    });

    it('preserves the delivered order exactly', async () => {
      const { runtime, api } = createHistoryRuntime();
      // Deliberately not ordered by id or by a locally parseable timestamp: the
      // contract's order is the server's (§4.5 note 4) and the runtime re-sorts
      // nothing.
      const sent = [newest, older];
      api.getBattleHistory.mockResolvedValue(sent as never);

      const received = await runtime.getBattleHistory();

      expect(received.map((entry) => entry.battleId)).toEqual(['battle-zzz', 'battle-aaa']);
      expect(received.map((entry) => entry.battleId)).not.toEqual(
        [...sent.map((entry) => entry.battleId)].sort()
      );
    });

    it('computes nothing: every member arrives exactly as the server sent it', async () => {
      const { runtime } = createHistoryRuntime();

      const [entry] = await runtime.getBattleHistory();

      expect(entry).toEqual(newest);
      // XP is not summed, Level is not derived, the level-up flag is not
      // re-derived, and `completedAt` is not parsed or reformatted.
      expect(entry.rewards.newPlayerXp).toBe(400);
      expect(entry.rewards.newPlayerLevel).toBe(5);
      expect(entry.rewards.playerLeveledUp).toBe(true);
      expect(entry.completedAt).toBe('2026-10-05T09:15:00Z');
    });

    it('returns a 200 [] history as the empty array, not as an absence', async () => {
      const { runtime, api } = createHistoryRuntime();
      api.getBattleHistory.mockResolvedValue([] as never);

      // §4.5 note 9: a Player with no completed battles receives 200 and `[]`,
      // which is a delivered answer rather than a failure or a `null`.
      await expect(runtime.getBattleHistory()).resolves.toEqual([]);
    });

    it('propagates a history-read rejection unchanged', async () => {
      const { runtime, api } = createHistoryRuntime();
      api.getBattleHistory.mockRejectedValue(
        new Error('Request to /api/battle/history failed with status 401')
      );

      // The §2.3/§6 `401 UNAUTHENTICATED` reaches the caller as the transport
      // raised it; the runtime records nothing, caches nothing, and fabricates no
      // history.
      await expect(runtime.getBattleHistory()).rejects.toThrow('status 401');

      expect(runtime.getBattleState()).toBeNull();
    });

    it('holds no history state on the runtime', async () => {
      const { runtime } = createHistoryRuntime();

      await runtime.getBattleHistory();

      // §4.5's response is ephemeral presentation data (GAME_STATE.md §4,
      // ARCHITECTURE.md §2.2.3 rules 1–2): the calling scene owns it, so nothing
      // about it is kept here — not on the instance and not in the technical
      // state contract.
      for (const key of ['history', 'battles', 'battleHistory', 'results', 'progression']) {
        expect(Object.keys(runtime.getState())).not.toContain(key);
        expect(Object.keys(runtime)).not.toContain(key);
      }
    });

    it('performs no transport operation for the read', async () => {
      const { runtime, transport } = createHistoryRuntime();

      await runtime.getBattleHistory();

      // The history read is REST-only (§4.5 note 13): it introduces no hub
      // method, no event, and no subscription, so the SignalR port is untouched.
      expect(transport.invokedMethods).toEqual([]);
      expect(transport.subscriptions.size).toBe(0);
    });
  });

  describe('post-result active battle cleanup (TASK-203, D-202-04 = A, ADR-022)', () => {
    /**
     * TASK-203. When the player leaves a completed battle, `ResultScene` asks the
     * runtime to drop its synchronized battle copy through
     * `clearActiveBattleState()` (`ARCHITECTURE.md` §2.2.1, §2.2.3). The cleanup
     * is client-local state behavior: it clears the copy, retains the
     * connection, reads no result, introduces no wire message, and cannot affect
     * the preserved pre-battle loadout — which this runtime does not hold at all
     * (`GameRuntimePort.clearActiveBattleState`).
     */

    /** A joined, synchronized battle — the §4 push a completed battle leaves behind. */
    async function completedBattle(overrides: Record<string, unknown> = {}) {
      const context = createRuntime();
      await context.runtime.initialize();
      context.transport.emit('BattleStateUpdated', payload(overrides));
      return context;
    }

    it('drops the synchronized battle copy and returns to connected-with-no-battle', async () => {
      const { runtime } = await completedBattle({ battleId: 'battle-ended' });
      expect(runtime.getBattleState()!.battleId).toBe('battle-ended');
      expect(runtime.getState().sync).toBe('synchronized');

      runtime.clearActiveBattleState();

      expect(runtime.getBattleState()).toBeNull();
      // `awaiting_battle` is the documented "connected, no current battle" value
      // (state/GameRuntimeState.ts, SyncStatus).
      expect(runtime.getState().sync).toBe('awaiting_battle');
      expect(runtime.getState().connection).toBe('connected');
      expect(runtime.getState().lastError).toBeNull();
    });

    it('does not disconnect or disturb the transport', async () => {
      const { runtime, transport } = await completedBattle();
      const connectCalls = [...transport.connectCalls];
      const invokedMethods = [...transport.invokedMethods];

      runtime.clearActiveBattleState();

      // D-202-04 = A is not C/D: clearing the SignalR battle connection was not
      // approved, so the process-wide connection survives untouched
      // (ARCHITECTURE.md §2.2.1 rule 6).
      expect(transport.disconnectCalls).toBe(0);
      expect(transport.isConnected()).toBe(true);
      expect(transport.connectCalls).toEqual(connectCalls);
      expect(transport.invokedMethods).toEqual(invokedMethods);
      expect(runtime.getState().connection).toBe('connected');
    });

    it('takes no §7.3 result fallback read', async () => {
      // `handleBattleNotRecoverable` is the BATTLE_NOT_FOUND path and it reads
      // the result route; a normal post-result exit is not that path and must
      // read no result (SIGNALR_PROTOCOL.md §7.3, API_CONTRACTS.md §4).
      const transport = new FakeSignalR();
      const api = { getBattleResult: vi.fn(async () => ({ battleId: 'battle-1' })) };
      const runtime = new GameRuntime(transport as never, api as never);
      await runtime.initialize();
      transport.emit('BattleStateUpdated', payload());

      runtime.clearActiveBattleState();

      expect(api.getBattleResult).not.toHaveBeenCalled();
      expect(runtime.getBattleState()).toBeNull();
    });

    it('is idempotent, never throws, and reports the value the connection warrants', () => {
      const { runtime } = createRuntime();

      expect(() => {
        runtime.clearActiveBattleState();
        runtime.clearActiveBattleState();
      }).not.toThrow();

      expect(runtime.getBattleState()).toBeNull();
      // With no connection there is no "connected, no current battle" claim to
      // make, so the status stays `unsynchronized` rather than inventing one.
      expect(runtime.getState().connection).toBe('disconnected');
      expect(runtime.getState().sync).toBe('unsynchronized');
    });

    it('leaves the completed battle unaddressable', async () => {
      const { runtime, transport } = await completedBattle({ battleId: 'battle-ended' });

      runtime.clearActiveBattleState();

      // No stale battle id can reach the next battle's action path.
      await expect(
        runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 })
      ).rejects.toThrow(/No battle state is known/);
      expect(transport.swapCalls).toEqual([]);
    });

    it('accepts the next battle as a fresh push of its own id', async () => {
      const { runtime, transport } = await completedBattle({ battleId: 'battle-ended' });

      runtime.clearActiveBattleState();
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-next', turn: 0, sequence: 0 }));

      // The next battle's state is the server's new push through the existing
      // §4 path — never a revision of the copy that was dropped.
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getBattleState()!.battleId).toBe('battle-next');

      await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
      expect(transport.swapCalls).toHaveLength(1);
      expect(transport.swapCalls[0][0]).toBe('battle-next');
    });

    it('holds no result data as battle state', async () => {
      // The completed battle's persisted result is presentation data read on
      // demand (GAME_STATE.md §4, API_CONTRACTS.md §4); it is never promoted into
      // the synchronized battle copy, and after the cleanup there is no copy.
      const { runtime } = await completedBattle();
      runtime.clearActiveBattleState();

      expect(runtime.getBattleState()).toBeNull();
      for (const key of ['outcome', 'rewards', 'result', 'battleId']) {
        expect(Object.keys(runtime.getState())).not.toContain(key);
      }
    });
  });

  describe('authenticated session invalidation (API_CONTRACTS.md §2.3, ADR-020 D4 item 2)', () => {
    /**
     * TASK-234. Two approved paths end an authenticated session, and both run
     * exactly one cleanup:
     *
     * ```text
     * explicit sign-out control (ADR-020 D4 item 2)   ─┐
     *                                                    ├→ invalidateSession()
     * a `401` on the authenticated transport (§2.3)   ─┘
     * ```
     *
     * The cleanup is the canonical credential clear (`ApplicationSession`), the
     * runtime's existing session publication, the synchronized battle copy and
     * the authenticated connection. It issues no HTTP request of its own and
     * introduces no endpoint, wire message, or server-side revocation: §2.3
     * "Lifecycle (MVP)" and ADR-015 D5 fix revocation as absent in MVP, so the
     * signed-out token keeps its documented 24-hour server-side validity.
     */

    const SESSION_KEYS = ['dcacti_session_token', 'dcacti_player_id', 'dcacti_username'];

    /** The session a successful §2 exchange establishes (ADR-020 D4 item 1). */
    function authenticateSession(): ApplicationSession {
      const session = ApplicationSession.getInstance();
      session.establish({
        sessionToken: 'jwt.header.payload',
        playerId: 'player_1',
        username: 'hero',
      });
      return session;
    }

    /** §2.3 "Failure behavior": the one public `401` body a covered endpoint sends. */
    function unauthenticated(): ApiRequestError {
      return new ApiRequestError(
        401,
        'UNAUTHENTICATED',
        'Your session is no longer valid. Please sign in again.'
      );
    }

    /**
     * A runtime over the fake transport and a REST double whose covered reads all
     * reject with `error`. The `api` methods double as the record that the
     * cleanup issues no request of its own.
     */
    function sessionRuntime(error: unknown) {
      const transport = new FakeSignalR();
      const reject = async (): Promise<never> => {
        throw error;
      };
      const api = {
        getPets: vi.fn(reject),
        getPet: vi.fn(reject),
        getCards: vi.fn(reject),
        getRelics: vi.fn(reject),
        getBattleHistory: vi.fn(reject),
        getBattleResult: vi.fn(reject),
        startBattle: vi.fn(reject),
      };
      const runtime = new GameRuntime(transport as never, api as never);
      return { runtime, transport, api };
    }

    /** An authenticated, connected runtime — the state a `401` arrives in. */
    async function authenticatedRuntime(error: unknown = unauthenticated()) {
      const session = authenticateSession();
      const context = sessionRuntime(error);
      context.runtime.setSessionStatus('authenticated');
      await context.runtime.initialize();
      return { ...context, session };
    }

    /** Settles a promise into its value or rejection reason, for identity checks. */
    async function settle(promise: Promise<unknown>): Promise<unknown> {
      return await promise.then(
        (value) => value,
        (error: unknown) => error
      );
    }

    beforeEach(() => {
      ApplicationSession.getInstance().clear();
      window.localStorage.clear();
    });

    afterEach(() => {
      ApplicationSession.getInstance().clear();
      window.localStorage.clear();
    });

    it('a `401` clears the session, publishes unauthenticated and releases the transport', async () => {
      // Given an authenticated session and a runtime holding synchronized battle
      // state, When an authenticated read rejects with `401 UNAUTHENTICATED`,
      // Then the stored session is cleared, `session` is `unauthenticated`,
      // `getBattleState()` is null, and the transport is disconnected — once.
      const failure = unauthenticated();
      const { runtime, transport } = await authenticatedRuntime(failure);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-live' }));
      expect(runtime.getBattleState()!.battleId).toBe('battle-live');
      expect(runtime.getState().sync).toBe('synchronized');

      const caught = await settle(runtime.getPets());

      // The documented rejection still reaches the caller unchanged.
      expect(caught).toBe(failure);

      // 1. `ApplicationSession.clear()` — the only credential store (ADR-015 D2,
      //    ADR-020 D4): no runtime-owned copy of the session exists.
      const session = ApplicationSession.getInstance();
      expect(session.isAuthenticated()).toBe(false);
      expect(session.getSessionToken()).toBeNull();
      expect(session.getPlayerId()).toBeNull();
      expect(session.getUsername()).toBeNull();
      expect(session.getAuthorizationHeader()).toEqual({});
      for (const key of SESSION_KEYS) {
        expect(window.localStorage.getItem(key)).toBeNull();
      }

      // 2. The existing `SessionStatus` value — no new status is introduced —
      //    which is what makes `App` render `AuthScreen` in the same document.
      expect(runtime.getState().session).toBe('unauthenticated');

      // 3. No synchronized copy and no authenticated connection outlive it.
      expect(runtime.getBattleState()).toBeNull();
      expect(runtime.getState().connection).toBe('disconnected');
      expect(runtime.getState().connectionId).toBeNull();
      expect(runtime.getState().sync).toBe('unsynchronized');
      expect(transport.disconnectCalls).toBe(1);
      expect(transport.isConnected()).toBe(false);

      // The transport subscriptions are detached too, so a stale push from the
      // ended session cannot repopulate the cleared copy.
      expect(transport.subscriptions.size).toBe(0);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-stale' }));
      expect(runtime.getBattleState()).toBeNull();
    });

    it('is idempotent: a second `401` clears nothing and disconnects nothing again', async () => {
      // Given a session already invalidated, When a second `401` arrives (the
      // one public §2.3 answer again), Then no second clear and no second
      // disconnect occur, and the state stays `unauthenticated`.
      const failure = unauthenticated();
      const { runtime, transport, session } = await authenticatedRuntime(failure);
      const clearSpy = vi.spyOn(session, 'clear');

      expect(await settle(runtime.getPets())).toBe(failure);
      expect(clearSpy).toHaveBeenCalledTimes(1);
      expect(transport.disconnectCalls).toBe(1);

      // A read issued under the same (now ended) session answers 401 as well.
      expect(await settle(runtime.getBattleHistory())).toBe(failure);

      expect(clearSpy).toHaveBeenCalledTimes(1);
      expect(transport.disconnectCalls).toBe(1);
      expect(runtime.getState().session).toBe('unauthenticated');
      expect(runtime.getState().connection).toBe('disconnected');
    });

    it('collapses two overlapping `401`s into a single cleanup', async () => {
      // §2.3: missing, invalid/tampered and expired sessions share one public
      // response, so two in-flight covered reads that both answer 401 are
      // indistinguishable and must not clean up twice.
      const session = authenticateSession();
      const transport = new FakeSignalR();
      const pending: Array<(error: unknown) => void> = [];
      const inFlight = (): Promise<never> =>
        new Promise<never>((_, reject) => {
          pending.push(reject);
        });
      const api = { getPets: vi.fn(inFlight), getCards: vi.fn(inFlight) };
      const runtime = new GameRuntime(transport as never, api as never);
      runtime.setSessionStatus('authenticated');
      await runtime.initialize();

      const failure = unauthenticated();
      const readPets = settle(runtime.getPets());
      const readCards = settle(runtime.getCards());
      expect(pending).toHaveLength(2);

      pending[0](failure);
      pending[1](failure);

      expect(await readPets).toBe(failure);
      expect(await readCards).toBe(failure);
      expect(transport.disconnectCalls).toBe(1);
      expect(session.isAuthenticated()).toBe(false);
      expect(runtime.getState().session).toBe('unauthenticated');
    });

    it('leaves the session and runtime state untouched for any non-`401` failure', async () => {
      // Given an authenticated session, When a read rejects with
      // `400 INVALID_LOADOUT` / `404` / a transport error, Then the rejection
      // propagates unchanged and no session state is touched (§3, §6).
      const failures = [
        new ApiRequestError(400, 'INVALID_LOADOUT', 'The requested loadout is not valid.'),
        new ApiRequestError(404, 'BATTLE_NOT_FOUND', 'That information is no longer available.'),
        new Error('Request to /api/pets failed with status 503'),
      ];

      for (const failure of failures) {
        ApplicationSession.getInstance().clear();
        window.localStorage.clear();
        const { runtime, transport, session } = await authenticatedRuntime(failure);
        const clearSpy = vi.spyOn(session, 'clear');

        expect(await settle(runtime.getPets())).toBe(failure);

        expect(clearSpy).not.toHaveBeenCalled();
        expect(session.isAuthenticated()).toBe(true);
        expect(transport.disconnectCalls).toBe(0);
        expect(transport.isConnected()).toBe(true);
        expect(runtime.getState().session).toBe('authenticated');
        expect(runtime.getState().connection).toBe('connected');
      }
    });

    it('classifies the rejection at the transport, not per endpoint', async () => {
      // Every covered capability reaches the same boundary, so the outcome does
      // not depend on which one was called: the battle-start POST and the §7.3
      // result fallback read end the session exactly as a collection read does.
      const startFailure = unauthenticated();
      const start = await authenticatedRuntime(startFailure);
      expect(await settle(start.runtime.startBattle({} as never))).toBe(startFailure);
      expect(start.session.isAuthenticated()).toBe(false);
      expect(start.runtime.getState().session).toBe('unauthenticated');
      expect(start.transport.disconnectCalls).toBe(1);

      ApplicationSession.getInstance().clear();
      window.localStorage.clear();

      const fallbackFailure = unauthenticated();
      const session = authenticateSession();
      const transport = new FakeSignalR();
      const api = {
        getBattleResult: vi.fn(async (): Promise<never> => {
          throw fallbackFailure;
        }),
      };
      const runtime = new GameRuntime(transport as never, api as never);
      runtime.setSessionStatus('authenticated');
      await runtime.initialize();
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-gone' }));
      transport.getBattleStateResult = { accepted: false, reason: 'BATTLE_NOT_FOUND' };

      // The documented §7 reconnect cycle: the snapshot cannot be recovered, so
      // §7.3 takes the result fallback read — which answers 401 here.
      transport.handlers.onReconnecting?.(new Error('lost'));
      transport.handlers.onReconnected?.('conn-2');

      await vi.waitFor(() => {
        expect(api.getBattleResult).toHaveBeenCalledWith('battle-gone');
      });
      await vi.waitFor(() => {
        expect(runtime.getState().session).toBe('unauthenticated');
      });

      expect(session.isAuthenticated()).toBe(false);
      expect(transport.disconnectCalls).toBe(1);
    });

    it('re-initializes after invalidation: reconnects and re-registers the subscriptions', async () => {
      // Given a session invalidated by `401`, When the player authenticates
      // again, Then `initialize()` reconnects the transport and re-registers
      // `ReceiveEvents` and `BattleStateUpdated` (SIGNALR_PROTOCOL.md §3–§4).
      const failure = unauthenticated();
      const { runtime, transport } = await authenticatedRuntime(failure);
      expect(transport.subscriptions.size).toBe(2);

      expect(await settle(runtime.getPets())).toBe(failure);
      expect(runtime.isInitialized()).toBe(false);
      expect(transport.subscriptions.size).toBe(0);

      // A second successful authentication in the same document (ADR-020): a new
      // §2 exchange stores a new session, and the app initializes again.
      authenticateSession();
      runtime.setSessionStatus('authenticated');
      await runtime.initialize();

      expect(runtime.isInitialized()).toBe(true);
      expect(transport.connectCalls).toEqual(['/hubs/battle', '/hubs/battle']);
      // A new physical connection — the hub authenticates with the token that
      // built it (SignalRService.ts `accessTokenFactory`), so the second account
      // never inherits the first session's authenticated connection.
      expect(transport.physicalConnections).toBe(2);
      expect(runtime.getState().connection).toBe('connected');
      expect(runtime.getState().session).toBe('authenticated');

      // The documented subscriptions are live again on the new connection, not
      // merely once registered.
      expect(transport.subscriptions.has('ReceiveEvents')).toBe(true);
      expect(transport.subscriptions.has('BattleStateUpdated')).toBe(true);
      expect(transport.subscriptionRegistrations('ReceiveEvents')).toBe(2);
      expect(transport.subscriptionRegistrations('BattleStateUpdated')).toBe(2);

      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-second' }));
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getBattleState()!.battleId).toBe('battle-second');
    });

    it('runs the identical cleanup for the explicit sign-out path, with no request of its own', async () => {
      // The control in App.tsx calls this exact method, so both triggers share
      // one routine (ADR-020 D4 item 2).
      const { runtime, transport, api, session } = await authenticatedRuntime();
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-live' }));

      await runtime.invalidateSession();

      expect(session.isAuthenticated()).toBe(false);
      for (const key of SESSION_KEYS) {
        expect(window.localStorage.getItem(key)).toBeNull();
      }
      expect(runtime.getState().session).toBe('unauthenticated');
      expect(runtime.getState().connection).toBe('disconnected');
      expect(runtime.getBattleState()).toBeNull();
      expect(transport.disconnectCalls).toBe(1);

      // No HTTP request, no hub invocation, no logout endpoint, no revocation:
      // the signed-out token keeps its documented server-side validity
      // (API_CONTRACTS.md §2.3 "Lifecycle (MVP)", ADR-015 D5).
      for (const method of Object.values(api)) {
        expect(method).not.toHaveBeenCalled();
      }
      expect(transport.invokedMethods).toEqual([]);
    });

    it('is safe and silent when no session is held', async () => {
      // A never-authenticated or already-invalidated client holds nothing to
      // release, so the cleanup is a silent no-op rather than an error.
      const session = ApplicationSession.getInstance();
      session.clear();
      const clearSpy = vi.spyOn(session, 'clear');
      const { runtime, transport, api } = sessionRuntime(unauthenticated());

      await expect(runtime.invalidateSession()).resolves.toBeUndefined();
      await expect(runtime.invalidateSession()).resolves.toBeUndefined();

      expect(clearSpy).not.toHaveBeenCalled();
      expect(transport.disconnectCalls).toBe(0);
      expect(runtime.getState().session).toBe('unauthenticated');
      for (const method of Object.values(api)) {
        expect(method).not.toHaveBeenCalled();
      }
    });

    it('clears the three session keys only', async () => {
      // An unrelated application preference is not part of the session, so the
      // cleanup must not touch it.
      const { runtime } = await authenticatedRuntime(unauthenticated());
      window.localStorage.setItem('dcacti_unrelated_preference', 'keep-me');

      expect(await settle(runtime.getPets())).toBeInstanceOf(ApiRequestError);

      for (const key of SESSION_KEYS) {
        expect(window.localStorage.getItem(key)).toBeNull();
      }
      expect(window.localStorage.getItem('dcacti_unrelated_preference')).toBe('keep-me');
    });

    it('adds no session-invalidation state to the technical runtime contract', async () => {
      // ARCHITECTURE.md §2.2.1 rule 5: the state contract carries connection,
      // session, runtime, synchronization, engine and diagnostics only. The
      // cleanup republishes the existing `session` member rather than adding one.
      const { runtime } = await authenticatedRuntime(unauthenticated());

      await runtime.invalidateSession();

      expect(Object.keys(runtime.getState()).sort()).toEqual([
        'connection',
        'connectionId',
        'engine',
        'lastError',
        'runtime',
        'session',
        'sync',
      ]);
    });
  });

  describe('P0.2: Action Idempotency, Submission & Recovery Coordination', () => {
    function createP02Runtime() {
      const transport = new FakeSignalR();
      const api = {
        getBattleResult: vi.fn(async () => ({
          battleId: 'battle-1',
          outcome: 'victory',
          rewards: {},
          durationTurns: 5,
        })),
        startBattle: vi.fn(async () => ({
          battleId: 'battle-2',
          signalrHub: 'http://localhost/hubs/battle',
          initialState: payload({ battleId: 'battle-2' }),
        })),
      };
      const runtime = new GameRuntime(transport as never, api as never);
      return { runtime, transport, api };
    }

    async function joinedP02Runtime(overrides: Record<string, unknown> = {}) {
      const ctx = createP02Runtime();
      await ctx.runtime.initialize();
      ctx.transport.emit('BattleStateUpdated', payload(overrides));
      return ctx;
    }

    it('1. preserves action identity when transport throws during submission', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', sequence: 0 });

      transport.swapBehaviour = async () => {
        throw new Error('Transport dropped');
      };

      await expect(
        runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 })
      ).rejects.toThrow('Transport dropped');

      expect(transport.swapCalls).toHaveLength(1);
      const originalActionId = transport.swapCalls[0][4];
      expect(originalActionId).toBeTruthy();

      // Reset transport to succeed and retry the same logical swap
      transport.swapBehaviour = null;
      transport.swapResult = { accepted: true };

      const ack = await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
      expect(ack.accepted).toBe(true);
      expect(transport.swapCalls).toHaveLength(2);
      expect(transport.swapCalls[1][4]).toBe(originalActionId);
    });

    it('2. retry reuses the same logical action identity for reversed cell pair', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', sequence: 0 });

      transport.swapBehaviour = async () => {
        throw new Error('Timeout');
      };

      await expect(
        runtime.requestAction({ kind: 'Swap', fromCell: 2, toCell: 3 })
      ).rejects.toThrow('Timeout');

      const originalActionId = transport.swapCalls[0][4];

      transport.swapBehaviour = null;
      transport.swapResult = { accepted: true };

      // Reverse order (3, 2 instead of 2, 3) is the same logical action
      const ack = await runtime.requestAction({ kind: 'Swap', fromCell: 3, toCell: 2 });
      expect(ack.accepted).toBe(true);
      expect(transport.swapCalls[1][4]).toBe(originalActionId);
    });

    it('3. a definitive server rejection settles the submission without treating it as committed', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', sequence: 0 });

      transport.swapResult = { accepted: false, reason: 'INVALID_SWAP' };

      const ack = await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
      expect(ack.accepted).toBe(false);
      expect(ack.reason).toBe('INVALID_SWAP');

      const firstActionId = transport.swapCalls[0][4];

      // A subsequent submission of the same cells receives a NEW logical action identity
      transport.swapResult = { accepted: true };
      const secondAck = await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
      expect(secondAck.accepted).toBe(true);
      expect(transport.swapCalls[1][4]).not.toBe(firstActionId);
    });

    it('4. a newer authoritative state arriving before a delayed action response is preserved', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', sequence: 0 });

      let resolveSwap!: (value: { accepted: boolean }) => void;
      transport.swapBehaviour = () =>
        new Promise<{ accepted: boolean }>((resolve) => {
          resolveSwap = resolve;
        }) as unknown as Promise<never>;

      const swapPromise = runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });

      // While swap is in flight, the server pushes sequence 1
      transport.emit(
        'BattleStateUpdated',
        payload({ battleId: 'battle-1', turn: 1, sequence: 1 })
      );
      expect(runtime.getBattleState()!.sequence).toBe(1);

      // Now the delayed swap response arrives
      resolveSwap({ accepted: true });
      const ack = await swapPromise;
      expect(ack.accepted).toBe(true);

      // Newer authoritative state remains preserved at sequence 1
      expect(runtime.getBattleState()!.sequence).toBe(1);
    });

    it('5. a reconnect-requested recovery ingests the snapshot and restores synchronization', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', sequence: 0 });

      // Simulate connection reconnected and unsynchronized
      transport.handlers.onReconnecting?.(new Error('lost'));
      transport.handlers.onReconnected?.('conn-2');
      expect(runtime.getState().sync).toBe('unsynchronized');

      // The snapshot reports a sequence ahead of the copy the runtime holds. This
      // test issues the recovery itself on the reconnect path: it never delivers a
      // batch whose state is missing, so it is **not** coverage for a
      // gap-triggered recovery — that is this file's P0.4 suite, where a withheld
      // batch opens the request.
      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 1,
        state: payload({ battleId: 'battle-1', turn: 1, sequence: 1 }),
      };

      await runtime.recoverBattleState();

      expect(runtime.getBattleState()!.sequence).toBe(1);
      expect(runtime.getState().sync).toBe('synchronized');
    });

    it('6. a recovery with no batch at all re-renders from the snapshot and fabricates no event', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', sequence: 0 });

      const events: unknown[] = [];
      runtime.onBattleEvents((env) => events.push(env));

      // Recovery snapshot arrives without any ReceiveEvents batch. The batch is not
      // withheld here — none was ever delivered, so this is not coverage for a
      // missed batch either (P0.4 covers both directions of that).
      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 1,
        state: payload({ battleId: 'battle-1', turn: 1, sequence: 1 }),
      };

      await runtime.recoverBattleState();

      expect(runtime.getBattleState()!.sequence).toBe(1);
      expect(runtime.getState().sync).toBe('synchronized');
      // No combat events were fabricated
      expect(events).toHaveLength(0);
    });

    it('7. a valid newer snapshot restores synchronization after a stale snapshot was rejected', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', turn: 4, sequence: 9 });

      // Put into unsynchronized state (e.g. disconnect)
      transport.handlers.onReconnecting?.(new Error('lost'));
      expect(runtime.getState().sync).toBe('unsynchronized');

      // Stale snapshot at sequence 3
      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 3,
        state: payload({ battleId: 'battle-1', turn: 2, sequence: 3 }),
      };

      await runtime.recoverBattleState();
      expect(runtime.getState().sync).toBe('unsynchronized');
      expect(runtime.getState().lastError).toContain('behind the synchronized sequence 9');
      expect(runtime.getBattleState()!.sequence).toBe(9);

      // Now a valid newer snapshot at sequence 10 arrives
      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 10,
        state: payload({ battleId: 'battle-1', turn: 5, sequence: 10 }),
      };

      await runtime.recoverBattleState();
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getState().lastError).toBeNull();
      expect(runtime.getBattleState()!.sequence).toBe(10);
    });

    it('8. equal-sequence recovery still notifies consumers and finishes synchronization', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', turn: 2, sequence: 5 });

      transport.handlers.onReconnecting?.(new Error('lost'));
      expect(runtime.getState().sync).toBe('unsynchronized');

      const notified: Array<{ sequence: number; source?: string }> = [];
      runtime.onBattleState((st, meta) =>
        notified.push({ sequence: st?.sequence ?? -1, source: meta?.source })
      );

      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 5,
        state: payload({ battleId: 'battle-1', turn: 2, sequence: 5 }),
      };

      await runtime.recoverBattleState();

      expect(runtime.getState().sync).toBe('synchronized');
      expect(notified).toHaveLength(1);
      expect(notified[0].sequence).toBe(5);
      expect(notified[0].source).toBe('recovery');
    });

    it('9. cross-battle and stale-sequence guards from P0.1 remain effective', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', turn: 2, sequence: 4 });

      // Unsolicited foreign battle push
      transport.emit('BattleStateUpdated', payload({ battleId: 'foreign-battle', sequence: 0 }));
      expect(runtime.getBattleState()!.battleId).toBe('battle-1');
      expect(runtime.getState().lastError).toContain('foreign-battle');

      // Stale sequence push
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 2 }));
      expect(runtime.getBattleState()!.sequence).toBe(4);
      expect(runtime.getState().lastError).toContain('behind the synchronized sequence 4');
    });

    it('10. recovery-source metadata is propagated correctly for push vs recovery', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', sequence: 0 });

      const sources: string[] = [];
      runtime.onBattleState((_, meta) => {
        if (meta?.source) sources.push(meta.source);
      });

      // Push delivery
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', sequence: 1 }));
      expect(sources[0]).toBe('push');

      // Recovery delivery
      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 2,
        state: payload({ battleId: 'battle-1', sequence: 2 }),
      };
      await runtime.recoverBattleState();
      expect(sources[1]).toBe('recovery');
    });

    it('11. recovery retries are bounded and do not spin indefinitely on stale snapshots', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', sequence: 9 });

      transport.handlers.onReconnecting?.(new Error('lost'));
      expect(runtime.getState().sync).toBe('unsynchronized');

      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 3,
        state: payload({ battleId: 'battle-1', sequence: 3 }),
      };

      await runtime.recoverBattleState();

      // Bounded retries: 3 attempts total, then terminates
      expect(transport.getBattleStateCalls).toHaveLength(3);
      expect(runtime.getState().sync).toBe('unsynchronized');
    });

    it('12. a delayed response from a previous battle cannot settle the current battle action', async () => {
      const { runtime, transport } = await joinedP02Runtime({ battleId: 'battle-1', sequence: 0 });

      let resolveSwap!: (value: { accepted: boolean }) => void;
      transport.swapBehaviour = () =>
        new Promise<{ accepted: boolean }>((resolve) => {
          resolveSwap = resolve;
        }) as unknown as Promise<never>;

      // Submit swap for battle-1
      const swapPromise = runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });

      // Start new battle battle-2
      await runtime.startBattle({
        petId: 'pet-1',
        bossId: 'boss-1',
        cardLoadout: ['c1', 'c2', 'c3'],
        relicLoadout: [],
      });
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-2', sequence: 0 }));
      expect(runtime.getBattleState()!.battleId).toBe('battle-2');

      // Now delayed swap for battle-1 arrives
      resolveSwap({ accepted: true });

      await expect(swapPromise).rejects.toThrow(
        'Action response for battle "battle-1" arrived after active battle changed to "battle-2"'
      );

      // Active battle-2 is unaffected
      expect(runtime.getBattleState()!.battleId).toBe('battle-2');
    });
  });

  describe('P0.3: transport-uncertain action outcomes and recovery coordination', () => {
    function createP03Runtime() {
      const transport = new FakeSignalR();
      const runtime = new GameRuntime(transport as never);
      return { runtime, transport };
    }

    async function joinedP03Runtime(overrides: Record<string, unknown> = {}) {
      const ctx = createP03Runtime();
      await ctx.runtime.initialize();
      ctx.transport.emit('BattleStateUpdated', payload(overrides));
      return ctx;
    }

    it('reports a submitted-but-unanswered action as an unknown outcome, and asks the server instead of assuming', async () => {
      const { runtime, transport } = await joinedP03Runtime({ battleId: 'battle-1', sequence: 2 });

      transport.swapBehaviour = async () => {
        throw new Error('Transport timeout');
      };

      const failure = await runtime
        .requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 })
        .catch((error: unknown) => error);

      // The distinction the caller cannot make for itself: no acknowledgement is
      // neither an acceptance nor a rejection (SIGNALR_PROTOCOL.md §5, §8.3).
      expect(failure).toBeInstanceOf(RuntimeActionOutcomeUnknownError);
      expect((failure as RuntimeActionOutcomeUnknownError).kind).toBe('Swap');
      expect((failure as Error).message).toContain('Transport timeout');

      // The copy the runtime holds can no longer be vouched for, and it says so —
      // which is the state the synchronization indicator presents.
      expect(runtime.getState().sync).toBe('unsynchronized');

      // It asks for the authoritative snapshot rather than concluding that the
      // action did not happen, and it fabricates no state while doing so.
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
      expect(runtime.getBattleState()!.sequence).toBe(2);

      // The logical action's identity is still unresolved: a retry of the same
      // pair carries the identical action token (ADR-025 §2 / P0.2).
      const originalActionId = transport.swapCalls[0][4];
      expect(originalActionId).toBeTruthy();

      transport.swapBehaviour = null;
      transport.swapResult = { accepted: true };
      await runtime.requestAction({ kind: 'Swap', fromCell: 1, toCell: 0 });

      expect(transport.swapCalls).toHaveLength(2);
      expect(transport.swapCalls[1][4]).toBe(originalActionId);
    });

    it('lets the snapshot decide what the unanswered action did — committed or not', async () => {
      // The action committed: the authoritative state is a step further on.
      const committed = await joinedP03Runtime({ battleId: 'battle-1', sequence: 2 });
      committed.transport.swapBehaviour = async () => {
        throw new Error('Transport timeout');
      };
      await committed.runtime
        .requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 })
        .catch(() => undefined);

      committed.transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 3,
        state: payload({ battleId: 'battle-1', turn: 2, sequence: 3 }),
      };
      await committed.runtime.recoverBattleState();

      expect(committed.runtime.getBattleState()!.sequence).toBe(3);
      expect(committed.runtime.getState().sync).toBe('synchronized');

      // The action did not commit: the snapshot is level with the copy already
      // held — accepted and re-applied, never read as a rollback the client
      // performed (GameRuntime.receiveBattleState's idempotent duplicate).
      const refused = await joinedP03Runtime({ battleId: 'battle-1', sequence: 2 });
      refused.transport.swapBehaviour = async () => {
        throw new Error('Transport timeout');
      };
      await refused.runtime
        .requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 })
        .catch(() => undefined);

      refused.transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 2,
        state: payload({ battleId: 'battle-1', sequence: 2 }),
      };
      await refused.runtime.recoverBattleState();

      expect(refused.runtime.getBattleState()!.sequence).toBe(2);
      expect(refused.runtime.getState().sync).toBe('synchronized');
    });

    it('does not desynchronize or recover on a definitive rejection', async () => {
      const { runtime, transport } = await joinedP03Runtime({ battleId: 'battle-1', sequence: 2 });

      transport.swapResult = { accepted: false, reason: 'INVALID_SWAP' };

      const acknowledgement = await runtime.requestAction({
        kind: 'Swap',
        fromCell: 0,
        toCell: 1,
      });

      // A rejection is the server's own answer: the action is settled as a
      // gameplay no-op, the client is still synchronized, and there is nothing to
      // reconcile.
      expect(acknowledgement).toEqual({ accepted: false, reason: 'INVALID_SWAP' });
      expect(runtime.getState().sync).toBe('synchronized');
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getBattleState()!.sequence).toBe(2);
    });

    it('treats an unanswered cast as an unknown outcome too, and a refusal as settled', async () => {
      const uncertain = await joinedP03Runtime({ battleId: 'battle-1', sequence: 2 });

      uncertain.transport.cardCastBehaviour = async () => {
        throw new Error('Connection lost');
      };

      const failure = await uncertain.runtime
        .requestAction({ kind: 'CardCast', cardId: 'card-heal' })
        .catch((error: unknown) => error);

      expect(failure).toBeInstanceOf(RuntimeActionOutcomeUnknownError);
      expect((failure as RuntimeActionOutcomeUnknownError).kind).toBe('CardCast');
      expect(uncertain.runtime.getState().sync).toBe('unsynchronized');
      expect(uncertain.transport.getBattleStateCalls).toEqual(['battle-1']);

      const refused = await joinedP03Runtime({ battleId: 'battle-1', sequence: 2 });
      refused.transport.petSkillCastResult = {
        accepted: false,
        reason: 'PET_SKILL_NOT_OWNED',
      };

      const acknowledgement = await refused.runtime.requestAction({ kind: 'PetSkillCast' });

      expect(acknowledgement).toEqual({ accepted: false, reason: 'PET_SKILL_NOT_OWNED' });
      expect(refused.runtime.getState().sync).toBe('synchronized');
      expect(refused.transport.getBattleStateCalls).toEqual([]);
    });
  });

  /**
   * TASK-273 / P0.4 — client sequence desync detection and recovery convergence at
   * the runtime boundary.
   *
   * The contract under test (`SIGNALR_PROTOCOL.md` §3.1, §6 items 3–4, §7.1–§7.3,
   * `GAME_STATE.md` §5):
   *
   * ```text
   * push-then-batch   BattleStateUpdated(S) precedes ReceiveEvents(S), so the batch's
   *                   serverSequence equals the sequence the synchronized copy holds
   * gap               a batch ahead of the held sequence means the state for a
   *                   committed resolution never arrived: withhold it, publish
   *                   unsynchronized, request the §7.1 snapshot once
   * at or below       the held state already represents that resolution: forward, and
   *                   never fetch a snapshot for it
   * cross-battle      a batch for another battle never reaches a listener
   * ended             §7.3's BATTLE_NOT_FOUND is published as `null` + `source:
   *                   'ended'`, so the scene can drop what it was presenting
   * ```
   */
  describe('P0.4: client sequence desync detection and recovery convergence', () => {
    function createP04Runtime() {
      const transport = new FakeSignalR();
      const api = {
        getBattleResult: vi.fn(async () => ({
          battleId: 'battle-1',
          outcome: 'victory',
          rewards: {},
          durationTurns: 5,
        })),
        startBattle: vi.fn(async () => ({
          battleId: 'battle-2',
          signalrHub: 'http://localhost/hubs/battle',
          initialState: payload({ battleId: 'battle-2' }),
        })),
      };
      const runtime = new GameRuntime(transport as never, api as never);
      return { runtime, transport, api };
    }

    /** A joined runtime with the documented §4 push already delivered. */
    async function joinedP04Runtime(overrides: Record<string, unknown> = {}) {
      const ctx = createP04Runtime();
      await ctx.runtime.initialize();
      ctx.transport.emit('BattleStateUpdated', payload(overrides));
      return ctx;
    }

    /** The batches a listener observed, in dispatch order. */
    function observeBatches(runtime: GameRuntime): BattleEventsEnvelope[] {
      const seen: BattleEventsEnvelope[] = [];
      runtime.onBattleEvents((envelope) => seen.push(envelope));
      return seen;
    }

    function batch(battleId: string, serverSequence: number, events: unknown[] = []) {
      return { battleId, serverSequence, events };
    }

    it('1. lets the documented push-then-batch path pass through unchanged', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 3,
      });
      const seen = observeBatches(runtime);
      const syncBefore = runtime.getState().sync;

      const delivered = batch('battle-1', 3, [{ type: 'GemMatched' }]);
      transport.emit('ReceiveEvents', delivered);

      // Forwarded exactly as delivered, with no state change and no snapshot
      // request: the sequence equals the one the synchronized copy holds, which is
      // what §6 item 4's push-then-batch order guarantees.
      expect(seen).toEqual([delivered]);
      expect(runtime.getState().sync).toBe(syncBefore);
      expect(runtime.getState().sync).toBe('synchronized');
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getBattleState()!.sequence).toBe(3);
    });

    it('2. withholds a batch ahead of the held state and requests the snapshot', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 3,
      });
      const seen = observeBatches(runtime);

      // The state for the resolution this batch belongs to never arrived: the
      // batch is at 4 while the synchronized copy is still at 3 (§6 item 3). The
      // snapshot the gap is recovered with is the resolution's own state.
      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 4,
        state: payload({ battleId: 'battle-1', turn: 4, sequence: 4 }),
      };

      transport.emit('ReceiveEvents', batch('battle-1', 4, [{ type: 'GemMatched' }]));

      // Withheld: nothing the client holds can represent that resolution, so it is
      // not presented, and the authoritative snapshot is requested instead
      // (§7.1 — a snapshot, never a replay).
      expect(seen).toEqual([]);
      expect(runtime.getState().sync).toBe('unsynchronized');
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);

      // The snapshot converges it: the state is ingested by the ordinary §4 path
      // and synchronization is claimed again.
      await vi.waitFor(() => {
        expect(runtime.getBattleState()!.sequence).toBe(4);
      });

      expect(runtime.getState().sync).toBe('synchronized');
    });

    it('3. detects a skipped sequence, not only a single-step gap', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 4,
      });
      const seen = observeBatches(runtime);

      // Sequences 5 and 6 were both committed and neither state arrived.
      transport.emit('ReceiveEvents', batch('battle-1', 6, [{ type: 'GemMatched' }]));

      expect(seen).toEqual([]);
      expect(runtime.getState().sync).toBe('unsynchronized');
      expect(runtime.getState().lastError).toContain('serverSequence 6');
      expect(runtime.getState().lastError).toContain('synchronized sequence 4');
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
    });

    it('4. drops a batch from another battle and leaves the active state untouched', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 3,
      });
      const seen = observeBatches(runtime);

      transport.emit('ReceiveEvents', batch('battle-other', 3, [{ type: 'BattleWon' }]));

      // Cross-battle contamination: the batch belongs to no battle this runtime
      // synchronizes, so it reaches no listener and no recovery is requested for
      // the active battle — a foreign batch says nothing about it.
      expect(seen).toEqual([]);
      expect(runtime.getBattleState()!.battleId).toBe('battle-1');
      expect(runtime.getBattleState()!.sequence).toBe(3);
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getState().lastError).toContain('battle-other');
    });

    it('5. forwards a duplicate or older batch without an unnecessary recovery', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 4,
      });
      const seen = observeBatches(runtime);

      // A re-delivery of the resolution the copy already represents, and a batch
      // for an earlier one: both are forwarded (the scene's own accepted-resolution
      // memory decides presentation) and neither is a desynchronization.
      transport.emit('ReceiveEvents', batch('battle-1', 4, [{ type: 'GemMatched' }]));
      transport.emit('ReceiveEvents', batch('battle-1', 3, [{ type: 'GemMatched' }]));

      expect(seen).toHaveLength(2);
      expect(runtime.getState().sync).toBe('synchronized');
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getBattleState()!.sequence).toBe(4);
    });

    it('6. preserves unsynchronized status and retries a bounded number of times after a transport failure', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 1,
      });

      transport.getBattleStateBehaviour = async () => {
        throw new Error('connection lost');
      };

      transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));

      // The recovery request failed, so the client is honestly still
      // desynchronized: nothing is fabricated and no synchronization is claimed.
      await vi.waitFor(() => {
        expect(runtime.getState().lastError).toBe('connection lost');
      });

      expect(runtime.getState().sync).toBe('unsynchronized');
      expect(runtime.getBattleState()!.sequence).toBe(1);

      // Bounded: the retry policy issues a fixed number of attempts and stops,
      // rather than spinning on a persistent outage.
      await vi.waitFor(() => {
        expect(transport.getBattleStateCalls.length).toBe(3);
      });
      await new Promise((resolve) => setTimeout(resolve, 120));
      expect(transport.getBattleStateCalls.length).toBe(3);
      expect(runtime.getState().sync).toBe('unsynchronized');
    });

    it('7. keeps one recovery request in flight for one unresolved gap', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 1,
      });

      let release!: (value: { accepted: boolean; state?: unknown; serverSequence?: number }) => void;
      transport.getBattleStateBehaviour = () =>
        new Promise<never>((resolve) => {
          release = resolve as unknown as (value: {
            accepted: boolean;
            state?: unknown;
            serverSequence?: number;
          }) => void;
        });

      // Several observations of the same unresolved desynchronization: the gap
      // itself, a later gap, and the action path that also asks for recovery.
      transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));
      transport.emit('ReceiveEvents', batch('battle-1', 3, [{ type: 'GemMatched' }]));
      transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));
      void runtime.recoverBattleState();

      expect(transport.getBattleStateCalls).toEqual(['battle-1']);

      release({
        accepted: true,
        serverSequence: 3,
        state: payload({ battleId: 'battle-1', turn: 3, sequence: 3 }),
      });

      await vi.waitFor(() => {
        expect(runtime.getState().sync).toBe('synchronized');
      });
      expect(runtime.getBattleState()!.sequence).toBe(3);
    });

    it('8. requests no recovery for gap-free consecutive batches', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 1,
      });
      const seen = observeBatches(runtime);

      for (const sequence of [1, 2, 3]) {
        transport.emit(
          'BattleStateUpdated',
          payload({ battleId: 'battle-1', turn: sequence, sequence })
        );
        transport.emit('ReceiveEvents', batch('battle-1', sequence, [{ type: 'GemMatched' }]));
      }

      expect(seen.map((envelope) => envelope.serverSequence)).toEqual([1, 2, 3]);
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getState().sync).toBe('synchronized');
    });

    it('9. refuses a recovery response whose envelope and snapshot sequences disagree', async () => {
      // Equal: the documented response, ingested by the ordinary §4 path.
      const equal = await joinedP04Runtime({ battleId: 'battle-1', sequence: 1 });
      equal.transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 2,
        state: payload({ battleId: 'battle-1', turn: 2, sequence: 2 }),
      };
      await equal.runtime.recoverBattleState();
      expect(equal.runtime.getBattleState()!.sequence).toBe(2);
      expect(equal.runtime.getState().sync).toBe('synchronized');

      // The envelope ahead of the snapshot it carries: two spellings of one value
      // that disagree, so neither is chosen — nothing is stored, no synchronization
      // is claimed, and the contradiction is reported.
      const ahead = await joinedP04Runtime({ battleId: 'battle-1', sequence: 1 });
      ahead.transport.handlers.onReconnecting?.(new Error('lost'));
      ahead.transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 5,
        state: payload({ battleId: 'battle-1', turn: 2, sequence: 2 }),
      };
      await ahead.runtime.recoverBattleState();

      expect(ahead.runtime.getBattleState()!.sequence).toBe(1);
      expect(ahead.runtime.getState().sync).toBe('unsynchronized');
      expect(ahead.runtime.getState().lastError).toContain('inconsistent recovery envelope');
      expect(ahead.runtime.getState().lastError).toContain('serverSequence 5');
      expect(ahead.runtime.getState().lastError).toContain('sequence 2');

      // The envelope behind the snapshot: the same refusal, in the other direction.
      const behind = await joinedP04Runtime({ battleId: 'battle-1', sequence: 1 });
      behind.transport.handlers.onReconnecting?.(new Error('lost'));
      behind.transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 1,
        state: payload({ battleId: 'battle-1', turn: 4, sequence: 4 }),
      };
      await behind.runtime.recoverBattleState();

      expect(behind.runtime.getBattleState()!.sequence).toBe(1);
      expect(behind.runtime.getState().sync).toBe('unsynchronized');
      expect(behind.runtime.getState().lastError).toContain('serverSequence 1');
      expect(behind.runtime.getState().lastError).toContain('sequence 4');
    });

    it('10. refuses a recovery response for another battle, so it cannot replace the active state', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 3,
      });

      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 7,
        state: payload({ battleId: 'battle-other', turn: 7, sequence: 7 }),
      };

      await runtime.recoverBattleState();

      // No lifecycle transition established the other battle, so the shared
      // ingestion guard refuses it: the active copy is untouched and no
      // synchronization is claimed for a battle this runtime does not hold.
      expect(runtime.getBattleState()!.battleId).toBe('battle-1');
      expect(runtime.getBattleState()!.sequence).toBe(3);
      expect(runtime.getState().lastError).toContain('battle-other');
    });

    it('11. publishes BATTLE_NOT_FOUND as an ended battle through the scene boundary', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-9',
        sequence: 2,
      });

      const publications: Array<{ battleId: string | null; source?: string }> = [];
      runtime.onBattleState((state, metadata) =>
        publications.push({
          battleId: state?.battleId ?? null,
          source: metadata?.source,
        })
      );

      transport.getBattleStateResult = {
        accepted: false,
        serverSequence: null,
        state: null,
        reason: 'BATTLE_NOT_FOUND',
      };

      await runtime.recoverBattleState();

      // §7.3: the battle cannot be recovered, so the runtime holds none and says
      // so. It publishes the loss — `null` with the `'ended'` source — rather than a
      // state, and it invents neither a state nor an outcome.
      expect(publications).toEqual([{ battleId: null, source: 'ended' }]);
      expect(runtime.getBattleState()).toBeNull();
      expect(runtime.getState().sync).toBe('awaiting_battle');
    });

    it('12. lets no recovery continuation from a previous battle or session mutate the current one', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 1,
      });

      let release!: (value: { accepted: boolean; state?: unknown; serverSequence?: number }) => void;
      transport.getBattleStateBehaviour = () =>
        new Promise<never>((resolve) => {
          release = resolve as unknown as (value: {
            accepted: boolean;
            state?: unknown;
            serverSequence?: number;
          }) => void;
        });

      // Battle 1's gap opens a recovery that stays in flight.
      transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);

      // A new battle is established while that answer is still on its way: the
      // lifecycle transition's own push is what makes battle 2 the active one.
      await runtime.startBattle({
        petId: 'pet-1',
        bossId: 'boss-1',
        cardLoadout: ['c1', 'c2', 'c3'],
        relicLoadout: [],
      });
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-2', sequence: 0 }));
      expect(runtime.getBattleState()!.battleId).toBe('battle-2');

      // The old battle's snapshot finally arrives. It is not the active battle and
      // no transition established it, so the shared guard refuses it: it cannot
      // replace, settle, or roll back the battle now on screen.
      release({
        accepted: true,
        serverSequence: 9,
        state: payload({ battleId: 'battle-1', turn: 9, sequence: 9 }),
      });
      await new Promise((resolve) => setTimeout(resolve, 20));

      expect(runtime.getBattleState()!.battleId).toBe('battle-2');
      expect(runtime.getBattleState()!.sequence).toBe(0);
      expect(runtime.getState().sync).toBe('synchronized');
    });

    it('13. cannot detect a batch lost after its own state push advanced the sequence (TASK-280)', async () => {
      const { runtime, transport } = await joinedP04Runtime({
        battleId: 'battle-1',
        sequence: 0,
      });
      const seen = observeBatches(runtime);

      // Resolution 1: its §4 state push is delivered — §3.1 item 1 and §6 item 4
      // place the push before any batch for that battle — and its ReceiveEvents
      // batch is lost in transit (§3.1 item 6). The synchronized copy therefore
      // advances to 1 while no batch for 1 ever arrives.
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      // Resolution 2: its push advances the copy to 2 before its own batch, so that
      // batch is compared against a held sequence it already equals — §6 item 3's
      // documented push-then-batch delivery, not the `>` case that opens a gap.
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 2, sequence: 2 }));
      transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));

      // The lost batch is applied by nobody and recorded by nothing: the runtime
      // keeps no set of received sequences and no expected-sequence map, so batch 1
      // is indistinguishable from a batch that was never committed. No snapshot is
      // requested, because no gap is ever observed — the state push, not the batch,
      // is what the copy's version comes from (GAME_STATE.md §5.0).
      expect(seen.map((envelope) => envelope.serverSequence)).toEqual([2]);
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getState().lastError).toBeNull();
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getBattleState()!.sequence).toBe(2);
    });

    describe('TASK-274 — final recovery boundary & runtime invariant verification', () => {
      // --- Review A ---
      it('Review A.1: drops a batch for a battle with no held state before reaching listeners', async () => {
        const { runtime, transport } = createP04Runtime();
        await runtime.initialize();
        // Runtime has initialized but no battle has been joined or synchronized (battleState is null).
        expect(runtime.getBattleState()).toBeNull();

        const seen = observeBatches(runtime);
        const errors: string[] = [];
        runtime.onRuntimeEvent((e) => {
          if (e.type === 'runtime_error' && e.detail) {
            errors.push(e.detail);
          }
        });

        // Transport emits a batch for battle-1.
        transport.emit('ReceiveEvents', batch('battle-1', 1, [{ type: 'GemMatched' }]));

        // Dropped: no listeners receive the unattached batch.
        expect(seen).toHaveLength(0);
        expect(runtime.getBattleState()).toBeNull();
        expect(transport.getBattleStateCalls).toHaveLength(0);
        expect(errors).toHaveLength(1);
        expect(errors[0]).toContain('while no battle is synchronized');
      });

      it('Review A.2: drops a batch for battle A when battle B is active', async () => {
        const { runtime, transport } = await joinedP04Runtime({
          battleId: 'battle-B',
          sequence: 2,
        });

        const seen = observeBatches(runtime);
        const errors: string[] = [];
        runtime.onRuntimeEvent((e) => {
          if (e.type === 'runtime_error' && e.detail) {
            errors.push(e.detail);
          }
        });

        transport.emit('ReceiveEvents', batch('battle-A', 2, [{ type: 'GemMatched' }]));

        // Dropped: battle-A batch cannot reach listeners while battle-B is active.
        expect(seen).toHaveLength(0);
        expect(runtime.getBattleState()!.battleId).toBe('battle-B');
        expect(runtime.getBattleState()!.sequence).toBe(2);
        expect(runtime.getState().sync).toBe('synchronized');
        expect(errors).toHaveLength(1);
        expect(errors[0]).toContain('while "battle-B" is synchronized');
      });

      it('Review A.3: allows state push followed by matching batch to work normally through runtime', async () => {
        const { runtime, transport } = await joinedP04Runtime({
          battleId: 'battle-B',
          sequence: 1,
        });

        const seen = observeBatches(runtime);
        // Push state for sequence 2
        transport.emit('BattleStateUpdated', payload({ battleId: 'battle-B', sequence: 2 }));
        expect(runtime.getBattleState()!.sequence).toBe(2);

        // Batch for sequence 2 arrives
        const b = batch('battle-B', 2, [{ type: 'GemMatched' }]);
        transport.emit('ReceiveEvents', b);

        expect(seen).toEqual([b]);
        expect(runtime.getState().sync).toBe('synchronized');
        expect(transport.getBattleStateCalls).toEqual([]);
      });

      it('Review A.4: drops late batch arriving after battle-ended publication and cannot restart old presentation', async () => {
        const { runtime, transport } = await joinedP04Runtime({
          battleId: 'battle-1',
          sequence: 2,
        });

        const statePublications: Array<{ battleId: string | null; source?: string }> = [];
        runtime.onBattleState((s, m) =>
          statePublications.push({ battleId: s?.battleId ?? null, source: m?.source })
        );

        // Recovery returns BATTLE_NOT_FOUND, causing runtime to end the battle.
        transport.getBattleStateResult = {
          accepted: false,
          serverSequence: null,
          state: null,
          reason: 'BATTLE_NOT_FOUND',
        };
        await runtime.recoverBattleState();

        expect(statePublications).toContainEqual({ battleId: null, source: 'ended' });
        expect(runtime.getBattleState()).toBeNull();
        expect(runtime.getState().sync).toBe('awaiting_battle');

        // Late batch arrives for the ended battle.
        const seen = observeBatches(runtime);
        transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));

        // Dropped: cannot restart or resurrect the ended battle.
        expect(seen).toHaveLength(0);
        expect(runtime.getBattleState()).toBeNull();
        expect(runtime.getState().sync).toBe('awaiting_battle');
      });

      it('Review A.5: drops late batch from previous battle after new battle is active without affecting new battle', async () => {
        const { runtime, transport } = await joinedP04Runtime({
          battleId: 'battle-1',
          sequence: 4,
        });

        // Switch to battle-2
        await runtime.startBattle({
          petId: 'pet-1',
          bossId: 'boss-1',
          cardLoadout: ['c1', 'c2', 'c3'],
          relicLoadout: [],
        });
        transport.emit('BattleStateUpdated', payload({ battleId: 'battle-2', sequence: 0 }));
        expect(runtime.getBattleState()!.battleId).toBe('battle-2');
        expect(runtime.getBattleState()!.sequence).toBe(0);

        const seen = observeBatches(runtime);
        // Late batch for battle-1 arrives
        transport.emit('ReceiveEvents', batch('battle-1', 4, [{ type: 'GemMatched' }]));

        expect(seen).toHaveLength(0);
        expect(runtime.getBattleState()!.battleId).toBe('battle-2');
        expect(runtime.getBattleState()!.sequence).toBe(0);
        expect(runtime.getState().sync).toBe('synchronized');
      });

      // --- Review B ---
      it('Review B.1-B.5: rejects mismatched recovery envelope, remains unsynchronized, clears in-flight state without infinite retry loop', async () => {
        const { runtime, transport } = await joinedP04Runtime({
          battleId: 'battle-1',
          sequence: 1,
        });

        // Trigger gap recovery: batch ahead of state (sequence 2)
        transport.getBattleStateResult = {
          accepted: true,
          serverSequence: 99, // Mismatched envelope
          state: payload({ battleId: 'battle-1', turn: 2, sequence: 2 }),
        };

        transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));

        await vi.waitFor(() => {
          expect(runtime.getState().lastError).toContain('inconsistent recovery envelope');
        });

        // 1. Authoritative held state is not replaced
        expect(runtime.getBattleState()!.sequence).toBe(1);
        // 2. Runtime remains unsynchronized
        expect(runtime.getState().sync).toBe('unsynchronized');
        // 4. In-flight flag is cleared (allows future requests)
        // 5. No infinite automatic retry loop: only 1 call made
        expect(transport.getBattleStateCalls).toEqual(['battle-1']);
        await new Promise((resolve) => setTimeout(resolve, 80));
        expect(transport.getBattleStateCalls).toEqual(['battle-1']);
      });

      it('Review B.6: restores synchronization when a subsequent valid recovery response arrives', async () => {
        const { runtime, transport } = await joinedP04Runtime({
          battleId: 'battle-1',
          sequence: 1,
        });

        // Trigger gap recovery: batch ahead of state (sequence 2) withholds and marks unsynchronized
        transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));
        expect(runtime.getState().sync).toBe('unsynchronized');

        // First attempt returns malformed response
        transport.getBattleStateResult = {
          accepted: true,
          serverSequence: 99,
          state: payload({ battleId: 'battle-1', turn: 2, sequence: 2 }),
        };
        await runtime.recoverBattleState();

        expect(runtime.getState().sync).toBe('unsynchronized');
        expect(runtime.getBattleState()!.sequence).toBe(1);

        // Second attempt returns valid consistent response
        transport.getBattleStateResult = {
          accepted: true,
          serverSequence: 3,
          state: payload({ battleId: 'battle-1', turn: 3, sequence: 3 }),
        };
        await runtime.recoverBattleState();

        expect(runtime.getState().sync).toBe('synchronized');
        expect(runtime.getBattleState()!.sequence).toBe(3);
        expect(runtime.getState().lastError).toBeNull();
      });

      it('Review B.7: rejects malformed recovery response for old battle A and prevents it from mutating active battle B', async () => {
        const { runtime, transport } = await joinedP04Runtime({
          battleId: 'battle-A',
          sequence: 1,
        });

        // Start battle-2 (the id returned by the api mock)
        await runtime.startBattle({
          petId: 'pet-1',
          bossId: 'boss-1',
          cardLoadout: ['c1', 'c2', 'c3'],
          relicLoadout: [],
        });
        transport.emit('BattleStateUpdated', payload({ battleId: 'battle-2', sequence: 0 }));

        // Malformed recovery for old battle-A arrives
        transport.getBattleStateResult = {
          accepted: true,
          serverSequence: 50,
          state: payload({ battleId: 'battle-A', turn: 10, sequence: 10 }),
        };
        await runtime.recoverBattleState();

        expect(runtime.getBattleState()!.battleId).toBe('battle-2');
        expect(runtime.getBattleState()!.sequence).toBe(0);
        expect(runtime.getState().sync).toBe('synchronized');
      });

      // --- Review C ---
      it('Review C.2: late state push or batch cannot resurrect an ended battle', async () => {
        const { runtime, transport } = await joinedP04Runtime({
          battleId: 'battle-1',
          sequence: 1,
        });

        // Battle ends via BATTLE_NOT_FOUND
        transport.getBattleStateResult = {
          accepted: false,
          serverSequence: null,
          state: null,
          reason: 'BATTLE_NOT_FOUND',
        };
        await runtime.recoverBattleState();
        expect(runtime.getBattleState()).toBeNull();
        expect(runtime.getState().sync).toBe('awaiting_battle');

        // Late ReceiveEvents arrives for battle-1
        const seen = observeBatches(runtime);
        transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));
        expect(seen).toHaveLength(0);
        expect(runtime.getBattleState()).toBeNull();
        expect(runtime.getState().sync).toBe('awaiting_battle');

        // Now start battle-2 as a new battle
        await runtime.startBattle({
          petId: 'pet-1',
          bossId: 'boss-1',
          cardLoadout: ['c1', 'c2', 'c3'],
          relicLoadout: [],
        });
        transport.emit('BattleStateUpdated', payload({ battleId: 'battle-2', sequence: 0 }));
        expect(runtime.getBattleState()!.battleId).toBe('battle-2');

        // A late BattleStateUpdated for old battle-1 cannot resurrect or overwrite battle-2
        transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', sequence: 5 }));
        expect(runtime.getBattleState()!.battleId).toBe('battle-2');
        expect(runtime.getBattleState()!.sequence).toBe(0);
      });

      it('Review C.3: delayed getBattleResult response for old battle cannot replace active battle', async () => {
        const { runtime, api, transport } = await joinedP04Runtime({
          battleId: 'battle-1',
          sequence: 1,
        });

        let releaseResult!: (val: {
          battleId: string;
          outcome: 'victory' | 'defeat';
          rewards: Record<string, unknown>;
          durationTurns: number;
        }) => void;
        api.getBattleResult = vi.fn(
          () =>
            new Promise((res) => {
              releaseResult = res as typeof releaseResult;
            })
        );

        // Fetch result for battle-1 in background
        const resultPromise = runtime.getBattleResult('battle-1');

        // Battle-2 starts while result is still in flight
        await runtime.startBattle({
          petId: 'pet-1',
          bossId: 'boss-1',
          cardLoadout: ['c1', 'c2', 'c3'],
          relicLoadout: [],
        });
        transport.emit('BattleStateUpdated', payload({ battleId: 'battle-2', sequence: 0 }));
        expect(runtime.getBattleState()!.battleId).toBe('battle-2');

        // Delayed result for battle-1 arrives
        releaseResult({
          battleId: 'battle-1',
          outcome: 'victory',
          rewards: {},
          durationTurns: 5,
        });
        const r = await resultPromise;
        expect(r.battleId).toBe('battle-1');

        // Active battle remains battle-2, unchanged
        expect(runtime.getBattleState()!.battleId).toBe('battle-2');
        expect(runtime.getBattleState()!.sequence).toBe(0);
        expect(runtime.getState().sync).toBe('synchronized');
      });
    });
  });

  /**
   * TASK-284 — the bounded, client-local expectation that a delivered
   * authoritative state push is followed by its `ReceiveEvents` batch
   * (`SIGNALR_PROTOCOL.md` §7).
   *
   * Every case below is deterministic: fake timers drive the watchdog and
   * controlled promises drive the transport, so no case sleeps and no case
   * depends on wall-clock ordering. The scenarios are the nine the task fixes,
   * plus the exclusions the approved design requires (the §3.1 item 6
   * no-broadcast acceptance, a definitive rejection, and a push with no
   * submitted action) and the configurability of the duration.
   */
  describe('TASK-284 — resolution-batch watchdog (SIGNALR_PROTOCOL.md §7)', () => {
    /** The documented initial duration, which the default runtime must use. */
    const WATCHDOG_MS = DEFAULT_RESOLUTION_WATCHDOG_MS;

    beforeEach(() => {
      vi.useFakeTimers();
    });

    afterEach(() => {
      vi.useRealTimers();
    });

    function createWatchdogRuntime(options?: { resolutionWatchdogMs?: number }) {
      const transport = new FakeSignalR();
      const api = {
        getBattleResult: vi.fn(async () => ({
          battleId: 'battle-1',
          outcome: 'victory',
          rewards: {},
          durationTurns: 5,
        })),
        startBattle: vi.fn(async () => ({
          battleId: 'battle-2',
          signalrHub: 'http://localhost/hubs/battle',
          initialState: payload({ battleId: 'battle-2' }),
        })),
      };
      const runtime = new GameRuntime(transport as never, api as never, options);
      return { runtime, transport, api };
    }

    /** A joined runtime whose documented §4 push has already been delivered. */
    async function joinedWatchdogRuntime(
      overrides: Record<string, unknown> = {},
      options?: { resolutionWatchdogMs?: number }
    ) {
      const ctx = createWatchdogRuntime(options);
      await ctx.runtime.initialize();
      ctx.transport.emit('BattleStateUpdated', payload(overrides));
      return ctx;
    }

    function batch(battleId: string, serverSequence: number, events: unknown[] = []) {
      return { battleId, serverSequence, events };
    }

    function observeBatches(runtime: GameRuntime): BattleEventsEnvelope[] {
      const seen: BattleEventsEnvelope[] = [];
      runtime.onBattleEvents((envelope) => seen.push(envelope));
      return seen;
    }

    function observePublications(runtime: GameRuntime) {
      const publications: Array<{ state: unknown; source: string | undefined }> = [];
      runtime.onBattleState((state, metadata) => {
        publications.push({ state, source: metadata?.source });
      });
      return publications;
    }

    function observeErrors(runtime: GameRuntime): string[] {
      const details: string[] = [];
      runtime.onRuntimeEvent((event) => {
        if (event.type === 'runtime_error' && event.detail) {
          details.push(event.detail);
        }
      });
      return details;
    }

    /** A snapshot request that never answers, so the expiry's own publication stays observable. */
    function holdRecovery(): () => Promise<never> {
      return () => new Promise<never>(() => {});
    }

    /** The one documented action every scenario starts from: an accepted Swap. */
    async function submitSwap(runtime: GameRuntime): Promise<void> {
      await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
    }

    it('1. a matching batch inside the window settles the expectation: no recovery at all', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });
      const seen = observeBatches(runtime);

      await submitSwap(runtime);

      // §3.1 item 1: the resolution's state is published before the hub call
      // returns, so the push advances the held copy to the resolution's version.
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));
      expect(runtime.getBattleState()!.sequence).toBe(1);

      // Its batch arrives inside the window: forwarded whole, exactly once.
      const delivered = batch('battle-1', 1, [{ type: 'GemMatched' }]);
      transport.emit('ReceiveEvents', delivered);

      expect(seen).toEqual([delivered]);
      expect(runtime.getState().sync).toBe('synchronized');

      // Whatever the runtime is left waiting for afterwards, the settled
      // expectation fires nothing: §7 asks for the snapshot only for a real gap.
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getState().lastError).toBeNull();
    });

    it('2. a push delivered before the acknowledgement arms it, at the DELIVERED sequence', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });

      let resolveSwap!: (value: { accepted: boolean }) => void;
      transport.swapBehaviour = () =>
        new Promise<{ accepted: boolean }>((resolve) => {
          resolveSwap = resolve;
        }) as unknown as Promise<never>;

      const swapPromise = runtime.requestAction({ kind: 'Swap', fromCell: 4, toCell: 5 });
      const submittedActionId = transport.swapCalls[0][4];

      // The push carries sequence 3: two further resolutions committed before the
      // client was told anything, so `expectedSequence + 1` (1) is NOT the
      // resolution this action's batch belongs to (GAME_STATE.md §5.0).
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 3, sequence: 3 }));
      expect(runtime.getBattleState()!.sequence).toBe(3);

      // The delayed acknowledgement arrives afterwards: it neither re-arms nor
      // re-keys the expectation the push armed, and it requests nothing.
      resolveSwap({ accepted: true });
      expect((await swapPromise).accepted).toBe(true);
      expect(transport.getBattleStateCalls).toEqual([]);

      // A batch at 1 is an older, superseded resolution: forwarded, and it
      // settles nothing — the expectation is still waiting for the batch at 3.
      const older = batch('battle-1', 1, [{ type: 'GemMatched' }]);
      transport.emit('ReceiveEvents', older);

      transport.getBattleStateBehaviour = holdRecovery();
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS);

      // The expiry names the delivered sequence and the action identity the
      // runtime submitted under.
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
      expect(runtime.getState().sync).toBe('unsynchronized');
      expect(runtime.getState().lastError).toContain('sequence 3');
      expect(runtime.getState().lastError).toContain(submittedActionId!);
    });

    it('3. a lost batch expires at the 2,500 ms default and requests the existing snapshot', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });
      const seen = observeBatches(runtime);
      const details = observeErrors(runtime);

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      // One millisecond before the documented window the client still claims to
      // be synchronized and has asked for nothing.
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS - 1);
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getState().sync).toBe('synchronized');

      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 1,
        state: payload({ battleId: 'battle-1', turn: 1, sequence: 1 }),
      };
      await vi.advanceTimersByTimeAsync(1);

      // §7: it marks itself unsynchronized, records the delivered sequence and the
      // action identity, and asks for the item 1 snapshot — nothing else.
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
      expect(details.some((detail) => detail.includes('sequence 1'))).toBe(true);
      expect(details.some((detail) => detail.includes('action'))).toBe(true);

      // The snapshot converges the runtime through the existing §4 path, and no
      // event is fabricated or replayed to stand in for the lost batch.
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getBattleState()!.sequence).toBe(1);
      expect(seen).toEqual([]);

      // One expectation, one request: the watchdog does not fire again.
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
    });

    it('4. a batch arriving while the requested snapshot is in flight opens no second request', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });
      const seen = observeBatches(runtime);
      const publications = observePublications(runtime);

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      let releaseRecovery!: (value: {
        accepted: boolean;
        serverSequence?: number;
        state?: unknown;
      }) => void;
      transport.getBattleStateBehaviour = () =>
        new Promise<never>((resolve) => {
          releaseRecovery = resolve as unknown as typeof releaseRecovery;
        });

      await vi.advanceTimersByTimeAsync(WATCHDOG_MS);
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);

      // The lost batch arrives while the snapshot is in flight. It equals the held
      // sequence, so it is admitted and forwarded whole exactly once — and it
      // opens no second request: §7.1 keeps one in flight per battle.
      const delivered = batch('battle-1', 1, [{ type: 'GemMatched' }]);
      transport.emit('ReceiveEvents', delivered);
      expect(seen).toEqual([delivered]);

      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);

      // The snapshot finally answers: the existing recovery path converges once.
      releaseRecovery({
        accepted: true,
        serverSequence: 1,
        state: payload({ battleId: 'battle-1', turn: 1, sequence: 1 }),
      });
      await vi.advanceTimersByTimeAsync(0);

      expect(runtime.getState().sync).toBe('synchronized');
      expect(publications.filter((entry) => entry.source === 'recovery')).toHaveLength(1);
      expect(seen).toEqual([delivered]);
    });

    it('5. the snapshot supersedes the pending presentation: a late batch replays nothing', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });
      const seen = observeBatches(runtime);

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 1,
        state: payload({ battleId: 'battle-1', turn: 1, sequence: 1 }),
      };
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS);
      expect(runtime.getState().sync).toBe('synchronized');

      // The batch the expectation was waiting for arrives after the snapshot that
      // covers it. The runtime still delivers it whole and exactly once by the
      // ordinary §3 path — it neither replays it nor rolls the state back
      // (SIGNALR_PROTOCOL.md §7 precedence, ADR-008) — and the scene's own
      // accepted-resolution memory is what refuses a second presentation
      // (SceneLifecycle.test.ts).
      const late = batch('battle-1', 1, [{ type: 'GemMatched' }]);
      transport.emit('ReceiveEvents', late);

      expect(seen).toEqual([late]);
      expect(runtime.getBattleState()!.sequence).toBe(1);
      expect(runtime.getState().sync).toBe('synchronized');
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);

      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
      expect(runtime.getState().lastError).toBeNull();
    });

    it('6. a failed recovery stays unsynchronized, publishes no state, and stays bounded', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });
      const publications = observePublications(runtime);

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      transport.getBattleStateBehaviour = () => Promise.reject(new Error('transport down'));

      await vi.advanceTimersByTimeAsync(WATCHDOG_MS);
      expect(transport.getBattleStateCalls.length).toBeGreaterThan(0);

      // The expiry's request inherits the existing bound: MAX_RECOVERY_ATTEMPTS.
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);
      expect(transport.getBattleStateCalls).toHaveLength(3);

      // Honest and locked: unsynchronized, holding exactly the state it was
      // delivered, with no snapshot, no fabricated state, and no recovery
      // publication for a scene to converge on.
      expect(runtime.getState().sync).toBe('unsynchronized');
      expect(runtime.getState().lastError).toContain('transport down');
      expect(runtime.getBattleState()!.battleId).toBe('battle-1');
      expect(runtime.getBattleState()!.sequence).toBe(1);
      expect(publications.filter((entry) => entry.source === 'recovery')).toHaveLength(0);
    });

    it('7. BATTLE_NOT_FOUND keeps its terminal path and leaves no watchdog behind', async () => {
      const { runtime, transport, api } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });
      const publications = observePublications(runtime);

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      transport.getBattleStateResult = { accepted: false, reason: 'BATTLE_NOT_FOUND' };
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS);

      // §7.3 unchanged: the copy is dropped, the loss is published as `ended`, and
      // the documented result fallback is taken.
      expect(runtime.getBattleState()).toBeNull();
      expect(runtime.getState().sync).toBe('awaiting_battle');
      expect(publications.at(-1)).toEqual({ state: null, source: 'ended' });
      expect(api.getBattleResult).toHaveBeenCalledWith('battle-1');

      // The battle is gone, so nothing retries and nothing recovers it again.
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
    });

    it('8a. dispose leaves no orphaned watchdog timer behind', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      await runtime.dispose();
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);

      expect(transport.getBattleStateCalls).toEqual([]);
    });

    it('8b. the post-result battle clear leaves no orphaned watchdog timer behind', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      runtime.clearActiveBattleState();
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);

      expect(runtime.getBattleState()).toBeNull();
      expect(transport.getBattleStateCalls).toEqual([]);
    });

    it('8c. a reconnect recovery supersedes the watchdog instead of racing it', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      transport.getBattleStateResult = {
        accepted: true,
        serverSequence: 1,
        state: payload({ battleId: 'battle-1', turn: 1, sequence: 1 }),
      };
      transport.handlers.onReconnecting?.(new Error('lost'));
      transport.handlers.onReconnected?.('conn-2');
      await vi.advanceTimersByTimeAsync(1);

      // Exactly the reconnect's own recovery: the snapshot it delivered supersedes
      // the expectation, so the armed timer never becomes a second request.
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
      expect(runtime.getState().sync).toBe('synchronized');

      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
    });

    it('9. a watchdog armed for one battle cannot recover the battle that replaced it', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));

      // The battle is replaced: the transition's own push establishes battle-2.
      await runtime.startBattle({
        petId: 'pet-1',
        bossId: 'boss-1',
        cardLoadout: ['c1', 'c2', 'c3'],
        relicLoadout: [],
      });
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-2', sequence: 0 }));
      expect(runtime.getBattleState()!.battleId).toBe('battle-2');

      // The obsolete watchdog is inert: it may not address battle-2, and battle-1
      // is no longer synchronized.
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getState().sync).toBe('synchronized');
    });

    it('9b. a newer advance re-arms the ONE expectation, so only the newest resolution governs', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });

      await submitSwap(runtime);

      // Two resolutions commit, each publishing its state first: the first
      // expectation is replaced by the second, not left running alongside it.
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 2, sequence: 2 }));
      expect(runtime.getBattleState()!.sequence).toBe(2);

      transport.getBattleStateBehaviour = holdRecovery();
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS);

      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
      expect(runtime.getState().lastError).toContain('sequence 2');
      expect(runtime.getState().lastError).not.toContain('sequence 1');
    });

    it('excludes a documented no-broadcast acceptance (the §3.1 item 6 idempotent retry)', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 4,
      });

      // The retry of an already-committed action answers `Accepted: true` and
      // re-commits and re-broadcasts nothing: no state push, no `sequence`
      // advance. It is not a desync and must not be reported as one.
      const acknowledgement = await runtime.requestAction({ kind: 'Swap', fromCell: 6, toCell: 7 });
      expect(acknowledgement.accepted).toBe(true);
      expect(transport.swapCalls[0][3]).toBe(4);
      expect(runtime.getBattleState()!.sequence).toBe(4);

      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);

      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getState().lastError).toBeNull();
    });

    it('arms nothing for a push that advances with no action outstanding (TASK-280 push-only loss)', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });
      const seen = observeBatches(runtime);

      // The TASK-280 shape with no submitted action: the push advances the copy
      // and its batch is lost. Nothing the client sent is waiting on a
      // resolution, so nothing is expected and nothing is recovered — the client
      // holds the state those resolutions produced.
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 2, sequence: 2 }));
      transport.emit('ReceiveEvents', batch('battle-1', 2, [{ type: 'GemMatched' }]));

      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);

      expect(seen.map((envelope) => envelope.serverSequence)).toEqual([2]);
      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getState().sync).toBe('synchronized');
      expect(runtime.getState().lastError).toBeNull();
    });

    it('arms nothing for a definitively rejected action', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });

      transport.swapResult = { accepted: false, reason: 'STALE_ACTION' };
      const acknowledgement = await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
      expect(acknowledgement.accepted).toBe(false);

      // A rejected action produces no state push and no batch (§3.1 item 4), so
      // nothing of its own is outstanding — and it says nothing about a later
      // resolution's delivery either.
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS * 4);

      expect(transport.getBattleStateCalls).toEqual([]);
      expect(runtime.getState().sync).toBe('synchronized');
    });

    it('honours the configured duration instead of the 2,500 ms default', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime(
        { battleId: 'battle-1', sequence: 0 },
        { resolutionWatchdogMs: 100 }
      );

      await submitSwap(runtime);
      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));
      transport.getBattleStateBehaviour = holdRecovery();

      await vi.advanceTimersByTimeAsync(99);
      expect(transport.getBattleStateCalls).toEqual([]);

      await vi.advanceTimersByTimeAsync(1);
      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
    });

    it('names a cast request’s own correlation id, which no Swap identity stands in for', async () => {
      const { runtime, transport } = await joinedWatchdogRuntime({
        battleId: 'battle-1',
        sequence: 0,
      });

      await runtime.requestAction({ kind: 'CardCast', cardId: 'card-inferno' });
      const clientSequence = transport.cardCastCalls[0][2];
      expect(clientSequence).toBeTruthy();

      transport.emit('BattleStateUpdated', payload({ battleId: 'battle-1', turn: 1, sequence: 1 }));
      transport.getBattleStateBehaviour = holdRecovery();
      await vi.advanceTimersByTimeAsync(WATCHDOG_MS);

      expect(transport.getBattleStateCalls).toEqual(['battle-1']);
      expect(runtime.getState().lastError).toContain(clientSequence!);
    });
  });
});
