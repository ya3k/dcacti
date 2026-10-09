import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { GameRuntime } from '../src/game/runtime/GameRuntime';
import { RuntimeActionNotImplementedError } from '../src/game/runtime/GameRuntimeEvents';
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
  /** Every `swap()` call the runtime made, in order (SIGNALR_PROTOCOL.md §2.1). */
  public swapCalls: Array<[string, number, number, string]> = [];
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
    clientSequence: string
  ): Promise<{ accepted: boolean; reason?: string | null }> {
    if (this.swapBehaviour) {
      return await this.swapBehaviour();
    }

    this.invokedMethods.push('Swap');
    this.swapCalls.push([battleId, fromCell, toCell, clientSequence]);
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
    if (this.getBattleStateBehaviour) {
      return await this.getBattleStateBehaviour();
    }

    if (!this.connected) {
      throw new Error('SignalR connection is not established.');
    }

    this.invokedMethods.push('GetBattleState');
    this.getBattleStateCalls.push(battleId);
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
 * Each entry is the protocol's `CellPayload` — the cell's Gem type plus the
 * optional Special Gem at that cell (`GAME_STATE.md` §2.1.1,
 * `SIGNALR_PROTOCOL.md` §4.1 item 5), so a fixture is a bare Gem-name string
 * only if the wire shape were, which it is not.
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
    // SIGNALR_PROTOCOL.md §4.4: the two-member Boss HP projection. Both members
    // are always present and neither is nullable (§4.4 item 4).
    bossState: { bossId: 'boss-hoa-long', hp: 5000, maxHp: 5000 },
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

      const payload = {
        battleId: 'battle-1',
        serverSequence: 7,
        events: [{ some: 'server-owned-event' }, { another: true }],
      };
      transport.emit('ReceiveEvents', payload);

      expect(received).toHaveLength(1);
      expect(received[0]).toEqual(payload);
    });

    it('does not reorder events', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const received: unknown[][] = [];
      runtime.onBattleEvents((envelope) => received.push([...envelope.events]));

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
      // SIGNALR_PROTOCOL.md §4.1 item 5: the board "carries its Special Gem
      // state, and needs nothing else" — each cell entry holds its Gem type and,
      // optionally, the Special Gem at that cell. A reader that required bare
      // Gem-name strings would reject every real server payload, which is exactly
      // how the board failed to render before TASK-182.
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

    it('keeps an absent Special Gem absent instead of materializing one', async () => {
      // GAME_STATE.md §2.1.7 item 3: an omitted Special Gem member is the
      // documented statement that the cell holds an ordinary Gem. It is not a
      // null sentinel and not an invitation to predict one, so the reader must
      // not write a placeholder value in its place.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      const cells = serverCells().map((cell, index) =>
        index === 5 ? { gemType: cell.gemType, specialGem: { type: 'Burst' } } : cell
      );

      transport.emit('BattleStateUpdated', payload({ board: { cells } }));

      const board = runtime.getBattleState()!.board;
      expect(board.cells[5].specialGem).toEqual({ type: 'Burst' });
      expect('specialGem' in board.cells[5]).toBe(true);

      // Every other cell keeps the member absent rather than set to undefined,
      // null, or a placeholder type.
      const others = board.cells.filter((_, index) => index !== 5);
      expect(others.every((cell) => !('specialGem' in cell) || cell.specialGem === undefined)).toBe(
        true
      );
      expect(others.some((cell) => (cell.specialGem as unknown) === null)).toBe(false);
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
            skillCharge: 2,
            skillCooldown: 0,
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

      // §4.4 items 2–3 and item 10: `bossState` is a three-member projection, not
      // `BossState`. Every other §2.4 member — including the Boss's own
      // `StatusEffects[]` — is dropped rather than modelled.
      expect(Object.keys(state.bossState).sort()).toEqual(['bossId', 'hp', 'maxHp'].sort());

      for (const hiddenBossMember of [
        'element',
        'atk',
        'def',
        'state',
        'passiveId',
        'passiveProgress',
        'skillCharge',
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
        payload({ bossState: { bossId: 'boss-kim-loi-vuong', hp: 4200, maxHp: 5000 } })
      );

      expect(runtime.getBattleState()!.bossState).toEqual({
        bossId: 'boss-kim-loi-vuong',
        hp: 4200,
        maxHp: 5000,
      });
    });

    it('carries an hp of 0 as a real published value', async () => {
      // §4.4 item 4: `hp = 0` is the terminal value of a won battle and is a real
      // published value sent as `0` — never an omission and never null.
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      transport.emit(
        'BattleStateUpdated',
        payload({ bossState: { bossId: 'boss-hoa-long', hp: 0, maxHp: 5000 } })
      );

      const state = runtime.getBattleState()!;
      expect(state.bossState.hp).toBe(0);
      // The other two members are unaffected by a zero HP: nothing is dropped and
      // nothing is inferred from it (§4.4 items 4–5, 10).
      expect(state.bossState.maxHp).toBe(5000);
      expect(state.bossState.bossId).toBe('boss-hoa-long');
    });

    it('rejects a payload missing a bossState member rather than defaulting it', async () => {
      // §4.4 item 4: all three members are always present and none is optional —
      // the Boss exists from battle creation at full health with its identity
      // fixed, so there is no absent case, a client must not read an absent `hp` as
      // zero, and it must not supply an identity it was not sent (§4.4 item 10).
      const malformed = [
        // No `bossState` at all.
        { bossState: undefined },
        // No `hp`.
        { bossState: { bossId: 'boss-hoa-long', maxHp: 5000 } },
        // No `maxHp`.
        { bossState: { bossId: 'boss-hoa-long', hp: 4200 } },
        // No `bossId` — the identity is never reconstructed client-side.
        { bossState: { hp: 4200, maxHp: 5000 } },
        // An empty identity is not an identity.
        { bossState: { bossId: '', hp: 4200, maxHp: 5000 } },
        // Ill-typed members.
        { bossState: { bossId: 'boss-hoa-long', hp: '4200', maxHp: 5000 } },
        { bossState: { bossId: 'boss-hoa-long', hp: 4200, maxHp: null } },
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
        bossState: { bossId: 'boss-hoa-long', hp: 3100, maxHp: 5000 },
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
      // Identity plus the HP pair — not re-derived from the stale copy the runtime
      // held before the reconnect, and not read off any event (§4.4 item 10).
      expect(state.bossState).toEqual({ bossId: 'boss-hoa-long', hp: 3100, maxHp: 5000 });

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

    it('does not compare the snapshot against a locally held sequence', async () => {
      // GAME_STATE.md §5.2 item 3 / SIGNALR_PROTOCOL.md §2 item 1: `Sequence`
      // is not the client's correlation id, and no document defines a
      // "snapshot is stale, discard it" rule. A snapshot carrying a lower value
      // than the copy the runtime holds is still the authoritative one and is
      // ingested verbatim — no comparison, no rejection, no increment.
      const { runtime, transport } = await joinedRuntime({ turn: 4, sequence: 9 });

      const recovered = payload({ turn: 2, sequence: 3 });
      transport.getBattleStateResult = { accepted: true, serverSequence: 3, state: recovered };

      reconnect(transport);

      await vi.waitFor(() => {
        expect(runtime.getBattleState()).toEqual(recovered);
      });

      expect(runtime.getBattleState()!.sequence).toBe(3);
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

      // MATCH3_RULES.md §2.1.1 / SIGNALR_PROTOCOL.md §2.1: exactly the four
      // documented arguments, in order — the battle, the two §1.0 cell indices,
      // and the opaque client correlation id.
      expect(transport.swapCalls).toHaveLength(1);
      const [battleId, fromCell, toCell, clientSequence] = transport.swapCalls[0];

      expect(battleId).toBe('battle-42');
      expect(fromCell).toBe(12);
      expect(toCell).toBe(13);
      expect(typeof clientSequence).toBe('string');
      expect(clientSequence.length).toBeGreaterThan(0);

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

    it('does not require the correlation id to equal the server sequence', async () => {
      // SIGNALR_PROTOCOL.md §2 item 1 / MATCH3_RULES.md §2.1.4 item 1:
      // `clientSequence` is opaque, is NOT `BattleState.Sequence`, and is never
      // used to reject a stale action. The client is never required to track a
      // server number for its Swap to be accepted.
      const { runtime, transport } = await joinedRuntime();

      const state = runtime.getBattleState()!;
      expect(state.sequence).toBe(0);

      await runtime.requestAction({ kind: 'Swap', fromCell: 4, toCell: 5 });

      expect(transport.swapCalls[0][3]).not.toBe(String(state.sequence));
    });

    it('generates a distinct opaque correlation id per request', async () => {
      const { runtime, transport } = await joinedRuntime();

      await runtime.requestAction({ kind: 'Swap', fromCell: 0, toCell: 1 });
      await runtime.requestAction({ kind: 'Swap', fromCell: 2, toCell: 3 });

      const first = transport.swapCalls[0][3];
      const second = transport.swapCalls[1][3];

      expect(first).not.toBe(second);
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
});
