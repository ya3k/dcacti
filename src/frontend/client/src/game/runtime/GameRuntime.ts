import { ApiService } from '../../services/api/ApiService';
import type { BattleStartRequest } from '../../services/api/ApiService';
import type { CardResponse, PetResponse, RelicResponse } from '../../services/api/CollectionModels';
import { SignalRService } from '../../services/realtime/SignalRService';
import {
  INITIAL_RUNTIME_STATE,
  type GameRuntimeState,
} from '../../state/GameRuntimeState';
import {
  RUNTIME_ACTION_SWAP,
  RUNTIME_ACTION_CARD_CAST,
  RUNTIME_ACTION_PET_SKILL_CAST,
  RuntimeActionNotImplementedError,
  type BattleEventsEnvelope,
  type BattleEventsListener,
  type BattleStateListener,
  type GameRuntimePort,
  type RuntimeActionAcknowledgement,
  type RuntimeActionRequest,
  type RuntimeBattleState,
  type RuntimeBoard,
  type RuntimeEvent,
  type RuntimeEventListener,
  type RuntimePassiveProgress,
  type RuntimePetState,
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
 * Counter behind the opaque `clientSequence` correlation id
 * (`SIGNALR_PROTOCOL.md` §2 item 1).
 *
 * It is a per-process request counter and nothing else: the value identifies a
 * request so the client can correlate it with its result. It is deliberately
 * **not** `BattleState.Sequence` — it is never seeded from, compared with, or
 * required to equal any server number, and no action is rejected on its value
 * (`SIGNALR_PROTOCOL.md` §2 item 1, `MATCH3_RULES.md` §2.1.4 item 1). Staleness
 * is the server's already-applied check, decided from its own committed record.
 */
let clientSequenceCounter = 0;

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
 *   - scene lifecycle coordination (scenes publish/receive through the port),
 *   - the client → server request boundary for the documented Swap action
 *     (`requestAction`, SIGNALR_PROTOCOL.md §2.1) — coordination only,
 *   - battle-start orchestration (`startBattle`, SIGNALR_PROTOCOL.md §1
 *     items 1–2, §2 `JoinBattle`) — coordination only.
 *
 * It deliberately does NOT (task §9, AGENTS.md §10, ADR-001):
 *   - calculate damage, match, combo, cascade, passive, or power,
 *   - validate a Swap, decide whether one is legal, or commit an exchange,
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

  constructor(
    private readonly signalR: SignalRService = SignalRService.getInstance(),
    private readonly api: ApiService = ApiService.getInstance()
  ) {}

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
    this.registerTransportSubscriptions();
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
   * The client's synchronized copy of the authoritative battle state
   * (`GAME_STATE.md` §2.0.5, §2.2, §2.3), or `null` until the server pushes it
   * (SIGNALR_PROTOCOL.md §4).
   *
   * The runtime stores what the server sent and exposes it unchanged. It does
   * not derive, extend, or validate gameplay meaning from it (§4.9) — in
   * particular it never generates or repairs the board it carries (§4 item 10)
   * and never charges a Passive, evaluates a Threshold, or resets progress from
   * the `petState` it carries (§4.3 item 9).
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
   * Coordinates the documented `Swap`, `CardCast`, and `PetSkillCast` requests
   * (`SIGNALR_PROTOCOL.md` §2, §2.1, `MATCH3_RULES.md` §2.1.1):
   *
   * ```text
   * BattleScene → GameRuntime.requestAction → SignalRService.swap → BattleHub.Swap
   * BattleScene → GameRuntime.requestAction → SignalRService.cardCast → BattleHub.CardCast
   * BattleScene → GameRuntime.requestAction → SignalRService.petSkillCast → BattleHub.PetSkillCast
   * ```
   *
   * The runtime coordinates and nothing more. It resolves the battle the action
   * applies to from the state the server already pushed (never from the caller),
   * generates an opaque per-request correlation id, submits through the
   * transport, and resolves with the §5 acknowledgement.
   *
   * It decides no gameplay: it does not validate cards, costs, cooldowns, or
   * effects, does not simulate or commit a swap, does not resolve a cascade, and
   * does not mutate the board or combat state. Whether the action succeeds is
   * the server's answer (`MATCH3_RULES.md` §2.1.2, `CARD_RULES.md` §3,
   * `GAME_RULES.md` §18, ADR-001).
   *
   * The `clientSequence` it generates is opaque and is **not**
   * `BattleState.Sequence`: it is never derived from, compared with, or required
   * to equal the runtime's synchronized `sequence`, and it is never used to
   * decide staleness (`SIGNALR_PROTOCOL.md` §2 item 1, `MATCH3_RULES.md` §2.1.4
   * item 1).
   *
   * Every other action kind — including `GetBattleState` — still rejects with
   * `RuntimeActionNotImplementedError`.
   *
   * @throws RuntimeActionNotImplementedError for any action other than `Swap`, `CardCast`, or `PetSkillCast`.
   * @throws Error when no battle state is known, or the transport rejects the
   * request (no connection / connection lost). Both are presentation-level
   * failures, not game state (`SIGNALR_PROTOCOL.md` §8.3).
   */
  public async requestAction(
    action: RuntimeActionRequest
  ): Promise<RuntimeActionAcknowledgement> {
    const candidate = action as {
      kind?: unknown;
      fromCell?: unknown;
      toCell?: unknown;
      cardId?: unknown;
    };

    if (
      candidate.kind !== RUNTIME_ACTION_SWAP &&
      candidate.kind !== RUNTIME_ACTION_CARD_CAST &&
      candidate.kind !== RUNTIME_ACTION_PET_SKILL_CAST
    ) {
      throw new RuntimeActionNotImplementedError(String(candidate.kind));
    }

    // The battle id is the one the runtime holds from the server's own push
    // (SIGNALR_PROTOCOL.md §4.9): the caller cannot supply one the runtime never
    // received, so no action can be addressed to an invented battle.
    const battleState = this.battleState;

    if (battleState === null) {
      throw new Error(
        'No battle state is known: the server has not pushed BattleStateUpdated ' +
          'for a joined battle (SIGNALR_PROTOCOL.md §4). No action was sent.'
      );
    }

    if (candidate.kind === RUNTIME_ACTION_SWAP) {
      // The cells are passed on as supplied. The runtime does not range-check,
      // adjacency-check, or otherwise pre-validate them: the only pointer-side
      // check is the scene's own interaction feedback (a clearly non-board or
      // same-cell tap is never submitted), and the authoritative validation is
      // `MATCH3_RULES.md` §2.1.2's, which the server performs.
      return await this.signalR.swap(
        battleState.battleId,
        candidate.fromCell as number,
        candidate.toCell as number,
        this.nextClientSequence('swap')
      );
    }

    if (candidate.kind === RUNTIME_ACTION_CARD_CAST) {
      if (typeof candidate.cardId !== 'string' || candidate.cardId.length === 0) {
        throw new Error('CardCast action requires a non-empty cardId.');
      }
      return await this.signalR.cardCast(
        battleState.battleId,
        candidate.cardId,
        this.nextClientSequence('card_cast')
      );
    }

    // candidate.kind === RUNTIME_ACTION_PET_SKILL_CAST
    return await this.signalR.petSkillCast(
      battleState.battleId,
      this.nextClientSequence('pet_skill_cast')
    );
  }

  /**
   * Generates the opaque per-request correlation id
   * (`SIGNALR_PROTOCOL.md` §2 item 1).
   *
   * It identifies a request, not a position in the battle's history: it is not
   * `BattleState.Sequence`, is not derived from the synchronized `sequence`, and
   * is never used to reject a stale action (`MATCH3_RULES.md` §2.1.4 item 1). No
   * client-side randomness is involved — a monotonic counter is enough for
   * correlation and introduces none of the non-determinism `AGENTS.md` §11
   * forbids.
   */
  private nextClientSequence(prefix = 'action'): string {
    clientSequenceCounter += 1;
    return `${prefix}_${clientSequenceCounter}`;
  }

  // ---------------------------------------------------------------------------
  // Battle start (REST → connect → join)
  // ---------------------------------------------------------------------------

  /**
   * Battle-start orchestration — the client half of the documented sequence.
   *
   * ```text
   * ApiService.startBattle(request)        POST /api/battle/start  (API_CONTRACTS.md §3)
   *         ↓
   * SignalRService.connect(signalrHub)     connect the hub          (SIGNALR_PROTOCOL.md §1 item 2)
   *         ↓
   * SignalRService.joinBattle(battleId)    join the battle group    (§1.2, §2 JoinBattle)
   * ```
   *
   * That is the whole of the documented sequence (`SIGNALR_PROTOCOL.md` §1
   * items 1–2, §2) and the whole of what this method does. It invokes no other
   * hub method — `Swap` is `requestAction`'s, and `Ping`, `CardCast`,
   * `PetSkillCast`, and `GetBattleState` are not part of starting a battle. It
   * therefore does not call any state-fetch method after joining: the group join
   * is a transport step whose only observable effect is the server-initiated
   * state push (§2 `JoinBattle` item 2).
   *
   * **The synchronized copy is established only by that push.** This method does
   * not read, store, merge, or render the response's `initialState` membership:
   * `sync` and `getBattleState()` change only when a valid `BattleStateUpdated`
   * arrives through `receiveBattleState` (§4). The REST response supplies the
   * transport addresses and nothing else — no second synchronization path
   * exists (§4 item 11, §8).
   *
   * **The method resolves after the join, not after the state arrives.** The
   * push is asynchronous (§2 `JoinBattle` item 2), so this method deliberately
   * does not await `sync`, and no timeout, poll, or wait constant is introduced:
   * no such value is documented.
   *
   * **`battleId` is the server's.** It is taken from the start response, never
   * derived from the request, from `initialState`, or from a locally generated
   * identifier — the server owns BattleId (`GAME_RULES.md` §18, ADR-001).
   *
   * **Subscriptions are guaranteed present.** `connect` is called
   * unconditionally and the transport service owns connection idempotency
   * (`ARCHITECTURE.md` §2.2.1 rule 6 — one runtime, one connection), so no second
   * connection policy exists here. The documented `ReceiveEvents` and
   * `BattleStateUpdated` subscriptions are registered exactly once before the
   * join, which is what makes the join-triggered push observable even when
   * `initialize()` recorded a connection failure before reaching its own
   * subscription step.
   *
   * **Failure rejects and nothing is fabricated.** A REST failure (`401
   * UNAUTHENTICATED`, `400` loadout error — API_CONTRACTS.md §2.8, §3), a connect
   * failure, or a join failure propagates to the caller with `sync`,
   * `battleState`, and `connection` unchanged. No battle state is invented, no
   * `battleId` is manufactured, and no partial battle is initialized
   * (`SIGNALR_PROTOCOL.md` §8.3). The runtime adds no duplicate-start guard: no
   * document defines the behavior of overlapping calls, so none is invented here.
   *
   * @throws Error when the runtime is disposed, or when any of the three
   * documented steps fails.
   */
  public async startBattle(request: BattleStartRequest): Promise<void> {
    if (this.disposed) {
      throw new Error('GameRuntime has been disposed and cannot start a battle.');
    }

    const response = await this.api.startBattle(request);

    await this.signalR.connect(response.signalrHub);

    // The subscriptions the join's push arrives on. Registered before the join
    // so the push is never silently unobserved, and registered exactly once
    // (case A/B/C, task §9).
    this.registerTransportSubscriptions();

    await this.signalR.joinBattle(response.battleId);
  }

  // ---------------------------------------------------------------------------
  // Pre-battle collection reads (selection source, not a selection)
  // ---------------------------------------------------------------------------

  /**
   * The owned Pet collection (`API_CONTRACTS.md` §5.1) —
   * `GET /api/pets`.
   *
   * This is the pre-battle selection source `LobbyScene` builds its in-progress
   * selection from (`ARCHITECTURE.md` §2.2.3 rules 3–4). The runtime delegates
   * to the existing `ApiService` method and nothing else: it adds no
   * orchestration, does not cache the response, and does not hold it as state —
   * the selection belongs to the scene (rule 2), and the read belongs to the
   * transport. The array is returned in the order the server sent it; §5.5
   * defines no ordering, so none is imposed here.
   */
  public async getPets(): Promise<PetResponse[]> {
    return await this.api.getPets();
  }

  /**
   * One owned Pet's detail (`API_CONTRACTS.md` §5.2) —
   * `GET /api/pets/{petId}`.
   *
   * `petId` is the owned instance identity and is passed on as supplied; the
   * runtime adds no lookup, index, or cached collection of its own.
   */
  public async getPet(petId: string): Promise<PetResponse> {
    return await this.api.getPet(petId);
  }

  /**
   * The Player's unlocked Card definitions (`API_CONTRACTS.md` §5.3) —
   * `GET /api/cards`.
   *
   * Membership of the returned array **is** the unlocked state (ADR-012). The
   * runtime reads it as sent: it does not evaluate a category, a count, or a
   * copy limit, because those are the server's validation at battle start
   * (`ARCHITECTURE.md` §2.2.3 rule 5).
   */
  public async getCards(): Promise<CardResponse[]> {
    return await this.api.getCards();
  }

  /**
   * The owned Relic instances (`API_CONTRACTS.md` §5.4) — `GET /api/relics`.
   *
   * Each element is an owned instance identity. The runtime transports the read
   * unchanged — in particular it does not order or filter it, so no collection
   * ordering can leak into the equip-slot order the submitted selection defines
   * (`RELIC_RULES.md` §2.3, `ARCHITECTURE.md` §2.2.3 rule 4).
   */
  public async getRelics(): Promise<RelicResponse[]> {
    return await this.api.getRelics();
  }

  // ---------------------------------------------------------------------------
  // Internals
  // ---------------------------------------------------------------------------

  /**
   * Registers the documented server → client subscriptions exactly once.
   *
   * ```text
   * ReceiveEvents       §3 — one batch per resolved action
   * BattleStateUpdated  §4 — the server-initiated state push on group join
   * ```
   *
   * Both are registered here and nowhere else, so a successful battle start
   * always has them present regardless of how the connection was obtained:
   *
   * ```text
   * Case A  initialize() connected and subscribed → this is a no-op
   * Case B  initialize() failed at connect        → registers now
   * Case C  already registered                    → this is a no-op
   * ```
   *
   * Case B is the reason this is a shared helper rather than an inlined block:
   * `initialize()` subscribes only after a successful connect, so a runtime whose
   * initialization failed would otherwise reach `joinBattle` with no handler for
   * the push that join triggers — and that push would be silently missed.
   *
   * `SignalRService.on` requires a live connection, so this is only called after
   * a successful `connect`. It is idempotent by the same mechanism `initialize()`
   * already uses for repeated initialization: the registered unsubscribers are
   * the record that the subscriptions exist.
   */
  private registerTransportSubscriptions(): void {
    if (this.transportUnsubscribers.length > 0) {
      return;
    }

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
   * `rngSeed`, `rngState`, `board`, `playerState`, `petState` (`GAME_STATE.md`
   * §2.2, §2.3, §2.0.5).
   * Nothing is derived from it, and no gameplay meaning is inferred: the server
   * owns these values (§4.9, ADR-001). In particular the board is stored as
   * received and is never generated, filled, repaired, or re-derived by the
   * client (§4 item 10), `playerState`'s Match/Combo values are rendered, never
   * counted or recomputed (`MATCH3_RULES.md` §6.6 item 3), and `petState`'s
   * delivered members are stored verbatim — the runtime never charges a
   * Passive, evaluates a Threshold, or resets progress (§4.3 item 9).
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
   * Validates only the documented §4 shape — the `GAME_STATE.md` §2.2/§2.3/§2.0.5
   * fields `battleId`, `turn`, `sequence`, `rngSeed`, `rngState`, `board`,
   * `playerState`, `petState`.
   *
   * The record carries no other field and no `Status`/lifecycle value
   * (SIGNALR_PROTOCOL.md §4 item 4 — the payload carries the implemented stage's
   * own fields and no others, with the resulting member set part of the contract
   * per §8.1 — and §8.3), so nothing else is read or defaulted.
   *
   * This checks *shape*, not gameplay meaning: it does not know what a Gem is,
   * does not validate the board against any game rule, and cannot repair one.
   * Validating the board is the server's job (`MATCH3_RULES.md` §1.3–§1.4); a
   * client-side check would be a second, non-authoritative implementation. The
   * same applies to `playerState` and `petState`: their values are read as sent
   * and are never derived, clamped, or recomputed (`GAME_RULES.md` §18).
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

    const petState = this.readPetState(candidate.petState);
    if (petState === null) {
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
      petState,
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
   * Reads `PetState`'s delivered members (`GAME_STATE.md` §2.3,
   * `SIGNALR_PROTOCOL.md` §4.3).
   *
   * `passiveId` and `passiveProgress` are required, in the same way
   * `playerState`'s two values are: both are defined from battle creation, both
   * are non-nullable, and neither is omitted — so a payload missing one is
   * malformed rather than implicitly empty, and the runtime must not invent a
   * value to fill the gap (`GAME_RULES.md` §18). `current = 0` is a real value
   * and is delivered as `0`, never by absence (§4.3 item 4).
   *
   * `passiveResetOverride` is the one optional member: it is present iff the
   * Passive's reset behavior is non-default, and its absence *is* the statement
   * "default" (§4.3 item 7). Absence is therefore tolerated and stored as
   * absence — the runtime never substitutes `"Default"`, `null`, or any other
   * value for it, because writing one would be a second spelling of one fact.
   *
   * Every value is read as sent. The runtime does not charge a Passive, evaluate
   * a Threshold, reset progress, or apply an overflow (§4.3 item 9), and it does
   * not widen the object to the rest of §2.3 — identity, progression, combat
   * stats, and relics are not delivered (§4.3 item 2), while `equippedCards`
   * is delivered per §4.3 item 13.
   */
  private readPetState(value: unknown): RuntimePetState | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as Partial<RuntimePetState>;

    if (typeof candidate.passiveId !== 'string' || candidate.passiveId.length === 0) {
      return null;
    }

    const passiveProgress = this.readPassiveProgress(candidate.passiveProgress);
    if (passiveProgress === null) {
      return null;
    }

    if (
      !Array.isArray(candidate.equippedCards) ||
      candidate.equippedCards.length !== 4 ||
      !candidate.equippedCards.every((id) => typeof id === 'string' && id.length > 0)
    ) {
      return null;
    }

    // The conditional member: absent means the default reset, and only the two
    // documented contract names are a non-default statement (§4.3 items 6–7).
    if (
      candidate.passiveResetOverride !== undefined &&
      typeof candidate.passiveResetOverride !== 'string'
    ) {
      return null;
    }

    return candidate.passiveResetOverride === undefined
      ? {
          passiveId: candidate.passiveId,
          passiveProgress,
          equippedCards: [...candidate.equippedCards],
        }
      : {
          passiveId: candidate.passiveId,
          passiveProgress,
          equippedCards: [...candidate.equippedCards],
          passiveResetOverride: candidate.passiveResetOverride,
        };
  }

  /**
   * Reads the `PassiveProgress` pair (`GAME_STATE.md` §2.3).
   *
   * Both members are required: the two are one logical field read together to
   * render the documented `current / threshold` pair (`PASSIVE_RULES.md` §6
   * item 1), and `current = 0` is a real publishable value rather than an
   * absence, so a payload carrying only one is malformed (§4.3 item 4).
   */
  private readPassiveProgress(value: unknown): RuntimePassiveProgress | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as Partial<RuntimePassiveProgress>;

    if (typeof candidate.threshold !== 'number' || typeof candidate.current !== 'number') {
      return null;
    }

    return { threshold: candidate.threshold, current: candidate.current };
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