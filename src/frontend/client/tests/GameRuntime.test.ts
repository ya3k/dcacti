import { describe, it, expect, vi, beforeEach } from 'vitest';
import { GameRuntime } from '../src/game/runtime/GameRuntime';
import { RuntimeActionNotImplementedError } from '../src/game/runtime/GameRuntimeEvents';
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

  setHandlers(handlers: SignalRConnectionHandlers): void {
    this.handlers = handlers;
  }

  async connect(hubUrl: string): Promise<void> {
    this.connectCalls.push(hubUrl);
    await this.connectBehaviour();
    this.connected = true;
    this.handlers.onConnected?.(this.connectId);
  }

  async disconnect(): Promise<void> {
    this.disconnectCalls += 1;
    this.connected = false;
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

/** A server-shaped board: exactly 64 cells (GAME_STATE.md §2.1.1). */
function serverCells(): string[] {
  return Array.from({ length: 64 }, (_, index) => GEM_NAMES[index % GEM_NAMES.length]);
}

/**
 * A well-formed `BattleStateUpdated` payload — the implemented
 * `GAME_STATE.md` §0 stage's fields: the §2.0.5 Board Foundation State fields
 * plus §2.2's `playerState` (SIGNALR_PROTOCOL.md §4, §4.2).
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
      cells[0] = 'POWER';
      cells[63] = 'ATK';

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
        expect(cell).toBe((sent.board as { cells: string[] }).cells[index]);
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

    it('ignores a payload carrying an undocumented Status value', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();

      // No Status/lifecycle value exists in the protocol (§8.3); an unexpected
      // field is not modelled and not carried into the runtime's copy.
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
        (sent.board as { cells: string[] }).cells
      );
    });

    it('holds no battle state fields in the technical runtime state', async () => {
      const { runtime, transport } = createRuntime();
      await runtime.initialize();
      transport.emit('BattleStateUpdated', payload());

      // The authoritative copy is exposed separately; the technical runtime
      // state contract stays technical (ARCHITECTURE.md §2.2.1 rule 5).
      for (const key of ['battleId', 'turn', 'sequence', 'board', 'rngSeed', 'rngState', 'playerState']) {
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

  describe('client → server request boundary', () => {
    it('exposes the boundary without implementing gameplay actions', async () => {
      const { runtime } = createRuntime();

      await expect(runtime.requestAction({ kind: 'Swap' })).rejects.toBeInstanceOf(
        RuntimeActionNotImplementedError
      );
    });
  });
});