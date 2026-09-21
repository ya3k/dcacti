import { SignalRService } from '../../services/realtime/SignalRService';
import {
  INITIAL_RUNTIME_STATE,
  type GameRuntimeState,
} from '../../state/GameRuntimeState';
import {
  RuntimeActionNotImplementedError,
  type BattleEventsEnvelope,
  type BattleEventsListener,
  type BattleStateListener,
  type GameRuntimePort,
  type RuntimeActionRequest,
  type RuntimeBattleState,
  type RuntimeBoard,
  type RuntimeEvent,
  type RuntimeEventListener,
  type RuntimePlayerState,
  type RuntimeRngState,
} from './GameRuntimeEvents';

/**
 * The board's cell count — exactly 64 (`MATCH3_RULES.md` §1.0,
 * `GAME_STATE.md` §2.1.1).
 *
 * Used only to reject a partial board payload (`SIGNALR_PROTOCOL.md` §4.1
 * item 3). It is a shape check on received data, never a source of board
 * geometry: the client does not lay the board out from this number and does not
 * derive any game rule from it.
 */
const BOARD_CELL_COUNT = 64;

/**
 * `GameRuntime` — the client runtime coordination boundary.
 *
 * Responsibility (task §9): coordinate Phaser, SignalR, runtime state and
 * runtime events.
 *
 *   BattleScene → GameRuntime → SignalRService → SignalR
 *
 * It owns ONLY:
 *   - connection state (delegated from `SignalRService` lifecycle callbacks),
 *   - runtime lifecycle (initialize / dispose),
 *   - server event subscription (forwarding `ReceiveEvents` batches),
 *   - the synchronized copy of the server's authoritative battle state
 *     (`BattleStateUpdated`, SIGNALR_PROTOCOL.md §4),
 *   - scene lifecycle coordination (scenes publish/receive through the port).
 *
 * It deliberately does NOT (task §9, AGENTS.md §10, ADR-001):
 *   - calculate damage, match, combo, cascade, passive, or power,
 *   - interpret, filter, reorder, or synthesise Battle Events,
 *   - author, adjust, or recompute any battle state,
 *   - generate, fill, repair, validate, or re-derive the board
 *     (SIGNALR_PROTOCOL.md §4 item 10),
 *   - hold authoritative state of any kind.
 *
 * The battle state it exposes is a synchronized presentation copy of what the
 * server pushed (`GAME_STATE.md` §2.0.5, `SIGNALR_PROTOCOL.md` §4.9). The
 * runtime stores it and hands it to the scenes unchanged; it is not a second
 * authoritative game engine.
 *
 * Battle events are forwarded to subscribers exactly as the server sent them.
 * Reordering would break the resolution order guaranteed by
 * SIGNALR_PROTOCOL.md §3; interpreting would make the client authoritative.
 *
 * It also does not import Phaser: it is engine-agnostic so the Phaser scenes
 * depend on this runtime (and never on SignalR), which is the boundary that
 * keeps the game presentation independent of transport.
 */
export class GameRuntime implements GameRuntimePort {
  private state: GameRuntimeState = INITIAL_RUNTIME_STATE;
  private battleState: RuntimeBattleState | null = null;
  private runtimeListeners = new Set<RuntimeEventListener>();
  private battleListeners = new Set<BattleEventsListener>();
  private battleStateListeners = new Set<BattleStateListener>();
  private transportUnsubscribers: Array<() => void> = [];
  private disposeTransportHandlers: (() => void) | null = null;
  private initialized = false;
  private disposed = false;

  constructor(private readonly signalR: SignalRService = SignalRService.getInstance()) {}

  // ---------------------------------------------------------------------------
  // Lifecycle
  // ---------------------------------------------------------------------------

  /**
   * Initializes the runtime and connects the transport.
   *
   * Failure is graceful (task §19): a failed connection leaves the runtime in a
   * diagnostic state (`connection: 'error'`, `runtime: 'error'`) rather than
   * throwing into the caller, so the Phaser scenes keep running and the status
   * overlay can report the problem.
   */
  public async initialize(hubUrl: string = '/hubs/battle'): Promise<void> {
    if (this.disposed) {
      throw new Error('GameRuntime has been disposed and cannot be re-initialized.');
    }

    // Idempotent: repeated initialization must not open a second connection or
    // accumulate duplicate subscription callbacks (task §20).
    if (this.initialized) {
      return;
    }
    this.initialized = true;

    this.signalR.setHandlers({
      onConnecting: () => {
        this.updateState({ connection: 'connecting', lastError: null });
      },
      onConnected: (connectionId) => {
        this.updateState({
          connection: 'connected',
          connectionId,
          // No active battle exists yet: synchronization is not established
          // until battle resolution (SIGNALR_PROTOCOL.md §5–§6, ADR-008).
          sync: 'awaiting_battle',
          runtime: 'ready',
          lastError: null,
        });
        this.emit({ type: 'connected', state: this.state });
      },
      onReconnecting: (error) => {
        this.updateState({
          connection: 'reconnecting',
          sync: 'unsynchronized',
          runtime: 'suspended',
          lastError: this.describeError(error),
        });
        this.emit({
          type: 'reconnecting',
          state: this.state,
          detail: this.describeError(error),
        });
      },
      onReconnected: (connectionId) => {
        this.updateState({
          connection: 'connected',
          connectionId: connectionId ?? this.state.connectionId,
          sync: 'awaiting_battle',
          runtime: 'ready',
          lastError: null,
        });
        this.emit({ type: 'reconnected', state: this.state });
      },
      onClosed: (error) => {
        this.updateState({
          connection: 'disconnected',
          connectionId: null,
          sync: 'unsynchronized',
          runtime: 'suspended',
          lastError: this.describeError(error),
        });
        this.emit({
          type: 'disconnected',
          state: this.state,
          detail: this.describeError(error),
        });
      },
    });

    this.disposeTransportHandlers = () => this.signalR.setHandlers({});

    try {
      await this.signalR.connect(hubUrl);
    } catch (error) {
      const detail = this.describeError(error);
      this.updateState({
        connection: 'error',
        connectionId: null,
        sync: 'unsynchronized',
        runtime: 'error',
        lastError: detail,
      });
      this.emit({ type: 'connection_error', state: this.state, detail });
      // No connection exists, so there is nothing to subscribe to. Returning
      // here keeps the failure graceful: the runtime stays in a diagnostic
      // state instead of throwing out of initialization (task §19).
      return;
    }

    // Subscribed only once the connection exists — `SignalRService.on` requires
    // a live connection to register a handler on.
    this.transportUnsubscribers = [
      this.signalR.on<[unknown]>('ReceiveEvents', (payload) => {
        this.forwardBattleEvents(payload);
      }),
      // The documented initial state push (SIGNALR_PROTOCOL.md §4). Server-
      // initiated, delivered on group join — never requested by the client.
      this.signalR.on<[unknown]>('BattleStateUpdated', (payload) => {
        this.receiveBattleState(payload);
      }),
    ];
  }

  /**
   * Disposes the runtime: detaches transport handlers, clears all listeners and
   * releases the transport connection.
   *
   * Safe to call repeatedly and safe to call when initialization failed.
   */
  public async dispose(): Promise<void> {
    if (this.disposed) {
      return;
    }
    this.disposed = true;

    for (const unsubscribe of this.transportUnsubscribers) {
      unsubscribe();
    }
    this.transportUnsubscribers = [];

    this.disposeTransportHandlers?.();
    this.disposeTransportHandlers = null;

    this.runtimeListeners.clear();
    this.battleListeners.clear();
    this.battleStateListeners.clear();

    await this.signalR.disconnect();

    this.updateState({
      connection: 'disconnected',
      connectionId: null,
      sync: 'unsynchronized',
      runtime: 'initializing',
    });
  }

  /** True once `dispose()` has run. */
  public isDisposed(): boolean {
    return this.disposed;
  }

  /** True once `initialize()` has been called on a live runtime. */
  public isInitialized(): boolean {
    return this.initialized && !this.disposed;
  }

  // ---------------------------------------------------------------------------
  // Runtime status
  // ---------------------------------------------------------------------------

  /** Called by the Phaser bridge so the runtime reflects engine lifecycle. */
  public setEngineStatus(engine: GameRuntimeState['engine']): void {
    this.updateState({ engine });
  }

  /**
   * Records that the authenticated application session was established
   * (API_CONTRACTS.md §2, ADR-007). Not wired to real auth in this task: the
   * Discord → backend session exchange is outside the runtime foundation.
   */
  public setSessionStatus(session: GameRuntimeState['session']): void {
    this.updateState({ session });
  }

  public getState(): GameRuntimeState {
    return this.state;
  }

  /**
   * The client's synchronized copy of the authoritative Board Foundation State
   * (`GAME_STATE.md` §2.0.5), or `null` until the server pushes it
   * (SIGNALR_PROTOCOL.md §4).
   *
   * The runtime stores what the server sent and exposes it unchanged. It does
   * not derive, extend, or validate gameplay meaning from it (§4.9), and it never
   * generates or repairs the board it carries (§4 item 10).
   */
  public getBattleState(): RuntimeBattleState | null {
    return this.battleState;
  }

  // ---------------------------------------------------------------------------
  // Subscriptions
  // ---------------------------------------------------------------------------

  public onRuntimeEvent(listener: RuntimeEventListener): () => void {
    this.runtimeListeners.add(listener);
    return () => {
      this.runtimeListeners.delete(listener);
    };
  }

  public onBattleEvents(listener: BattleEventsListener): () => void {
    this.battleListeners.add(listener);
    return () => {
      this.battleListeners.delete(listener);
    };
  }

  public onBattleState(listener: BattleStateListener): () => void {
    this.battleStateListeners.add(listener);
    return () => {
      this.battleStateListeners.delete(listener);
    };
  }

  // ---------------------------------------------------------------------------
  // Client → server request boundary
  // ---------------------------------------------------------------------------

  /**
   * The client → server request boundary (task §15, reverse direction).
   *
   * No gameplay action is implemented. Establishing the boundary without
   * inventing request shapes is the requirement; sent requests would be
   * `Swap`/`CardCast`/`PetSkillCast` (SIGNALR_PROTOCOL.md §2), all of which are
   * gameplay.
   */
  public async requestAction(action: RuntimeActionRequest): Promise<never> {
    throw new RuntimeActionNotImplementedError(action.kind);
  }

  // ---------------------------------------------------------------------------
  // Internals
  // ---------------------------------------------------------------------------

  private updateState(patch: Partial<GameRuntimeState>): void {
    this.state = { ...this.state, ...patch };
    this.emit({ type: 'runtime_state_changed', state: this.state });
  }

  private emit(event: RuntimeEvent): void {
    for (const listener of this.runtimeListeners) {
      listener(event);
    }
  }

  /**
   * Forwards a server-authoritative `ReceiveEvents` batch
   * (SIGNALR_PROTOCOL.md §3) to battle-event subscribers unchanged.
   *
   * The payload is not reshaped, filtered, or interpreted. A malformed payload
   * is reported as a technical runtime error and otherwise ignored — the
   * runtime never fabricates gameplay data to fill a gap.
   */
  private forwardBattleEvents(payload: unknown): void {
    const envelope = this.readBattleEventsEnvelope(payload);

    if (envelope === null) {
      const detail = 'Received a malformed ReceiveEvents payload; ignored.';
      this.updateState({ lastError: detail });
      this.emit({ type: 'runtime_error', state: this.state, detail });
      return;
    }

    for (const listener of this.battleListeners) {
      listener(envelope);
    }
  }

  /**
   * Validates only the transport envelope shape the protocol defines
   * (SIGNALR_PROTOCOL.md §3). Individual event payloads remain opaque —
   * `GAME_EVENTS.md` owns their content.
   */
  private readBattleEventsEnvelope(payload: unknown): BattleEventsEnvelope | null {
    if (typeof payload !== 'object' || payload === null) {
      return null;
    }

    const candidate = payload as Partial<BattleEventsEnvelope>;

    if (typeof candidate.battleId !== 'string') {
      return null;
    }
    if (typeof candidate.serverSequence !== 'number') {
      return null;
    }
    if (!Array.isArray(candidate.events)) {
      return null;
    }

    return {
      battleId: candidate.battleId,
      serverSequence: candidate.serverSequence,
      events: candidate.events,
    };
  }

  /**
   * Receives the server's authoritative battle state push
   * (SIGNALR_PROTOCOL.md §4) and stores it as the runtime's synchronized copy.
   *
   * The payload is stored exactly as sent — `battleId`, `turn`, `sequence`,
   * `rngSeed`, `rngState`, `board`, `playerState` (`GAME_STATE.md` §2.2, §2.0.5).
   * Nothing is derived from it, and no gameplay meaning is inferred: the server
   * owns these values (§4.9, ADR-001). In particular the board is stored as
   * received and is never generated, filled, repaired, or re-derived by the
   * client (§4 item 10), and `playerState`'s Match/Combo values are rendered, never
   * counted or recomputed (`MATCH3_RULES.md` §6.6 item 3).
   *
   * A malformed payload is reported as a technical runtime error and ignored —
   * the runtime never fabricates battle state to fill a gap.
   */
  private receiveBattleState(payload: unknown): void {
    const state = this.readBattleState(payload);

    if (state === null) {
      const detail = 'Received a malformed BattleStateUpdated payload; ignored.';
      this.updateState({ lastError: detail });
      this.emit({ type: 'runtime_error', state: this.state, detail });
      return;
    }

    this.battleState = state;
    this.updateState({ sync: 'synchronized', lastError: null });
    this.emit({ type: 'battle_state_changed', state: this.state });

    for (const listener of this.battleStateListeners) {
      listener(state);
    }
  }

  /**
   * Validates only the documented §4 shape — the `GAME_STATE.md` §2.2/§2.0.5
   * fields `battleId`, `turn`, `sequence`, `rngSeed`, `rngState`, `board`,
   * `playerState`.
   *
   * The record carries no other field and no `Status`/lifecycle value
   * (SIGNALR_PROTOCOL.md §4.4, §8.3), so nothing else is read or defaulted.
   *
   * This checks *shape*, not gameplay meaning: it does not know what a Gem is,
   * does not validate the board against any game rule, and cannot repair one.
   * Validating the board is the server's job (`MATCH3_RULES.md` §1.3–§1.4); a
   * client-side check would be a second, non-authoritative implementation. The
   * same applies to `playerState`: its values are read as sent and are never
   * derived, clamped, or recomputed (`GAME_RULES.md` §18).
   */
  private readBattleState(payload: unknown): RuntimeBattleState | null {
    if (typeof payload !== 'object' || payload === null) {
      return null;
    }

    const candidate = payload as Partial<RuntimeBattleState>;

    if (typeof candidate.battleId !== 'string' || candidate.battleId.length === 0) {
      return null;
    }
    if (typeof candidate.turn !== 'number') {
      return null;
    }
    if (typeof candidate.sequence !== 'number') {
      return null;
    }
    if (typeof candidate.rngSeed !== 'number') {
      return null;
    }

    const rngState = this.readRngState(candidate.rngState);
    if (rngState === null) {
      return null;
    }

    const board = this.readBoard(candidate.board);
    if (board === null) {
      return null;
    }

    const playerState = this.readPlayerState(candidate.playerState);
    if (playerState === null) {
      return null;
    }

    return {
      battleId: candidate.battleId,
      turn: candidate.turn,
      sequence: candidate.sequence,
      rngSeed: candidate.rngSeed,
      rngState,
      board,
      playerState,
    };
  }

  /**
   * Reads `PlayerState`'s implemented fields (`GAME_STATE.md` §2.2).
   *
   * Both values are required: the state carries them from battle creation, both
   * are non-nullable, and neither is omitted when it is `0` — zero is a value
   * here, not an absence (`MATCH3_RULES.md` §6.5 item 4). A payload missing one is
   * therefore malformed rather than implicitly zero.
   *
   * The values are read as sent. The runtime does not count Matches, advance a
   * Combo, or reset one; it derives neither value from the board, the counters,
   * or the resolution (`GAME_RULES.md` §18, `MATCH3_RULES.md` §6.6 item 3).
   */
  private readPlayerState(value: unknown): RuntimePlayerState | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as Partial<RuntimePlayerState>;

    if (typeof candidate.combo !== 'number' || typeof candidate.matchCount !== 'number') {
      return null;
    }

    return { combo: candidate.combo, matchCount: candidate.matchCount };
  }

  /**
   * Reads the `RngState` pair (`GAME_STATE.md` §2.6.2). Both components are
   * required: the two are one logical field and are never split
   * (§2.6.2 item 1), so a payload carrying only one is malformed.
   */
  private readRngState(value: unknown): RuntimeRngState | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as Partial<RuntimeRngState>;

    if (typeof candidate.state !== 'number' || typeof candidate.increment !== 'number') {
      return null;
    }

    return { state: candidate.state, increment: candidate.increment };
  }

  /**
   * Reads the board's cells (`GAME_STATE.md` §2.1.1).
   *
   * The runtime checks only that 64 cell entries arrived — the shape
   * `SIGNALR_PROTOCOL.md` §4.1 item 3 guarantees ("the client does not receive a
   * partial board"). It deliberately does not inspect the Gem types or evaluate
   * any board rule: the board is server-authored and this is not a second
   * generator or validator.
   */
  private readBoard(value: unknown): RuntimeBoard | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as { cells?: unknown };

    if (!Array.isArray(candidate.cells) || candidate.cells.length !== BOARD_CELL_COUNT) {
      return null;
    }

    if (!candidate.cells.every((cell): cell is string => typeof cell === 'string')) {
      return null;
    }

    return { cells: candidate.cells };
  }

  private describeError(error: unknown): string | null {
    if (error === undefined || error === null) {
      return null;
    }
    return error instanceof Error ? error.message : String(error);
  }
}