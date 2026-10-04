import { ApiService } from '../../services/api/ApiService';
import type { BattleResultResponse, BattleStartRequest } from '../../services/api/ApiService';
import type { CardResponse, PetResponse, RelicResponse } from '../../services/api/CollectionModels';
import { SignalRService } from '../../services/realtime/SignalRService';
import type { BattleStateSnapshotResponse } from '../../services/realtime/SignalRService';
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
  type RuntimeBossState,
  type RuntimeEvent,
  type RuntimeEventListener,
  type RuntimePassiveProgress,
  type RuntimePetState,
  type RuntimePlayerState,
  type RuntimeRngState,
  type RuntimeStatusEffect,
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
 * The rejection code `GetBattleState` returns when the snapshot cannot be
 * recovered (`SIGNALR_PROTOCOL.md` §7.3).
 *
 * The code is the server contract's own value, not a client-invented state: it
 * covers an unknown battle, an expired active-state record, and a battle owned
 * by another Player as one indistinguishable answer (`REDIS_STATE.md` §3,
 * `GAME_STATE.md` §2.8). The client therefore never tries to tell those cases
 * apart, and exposes no Redis or ownership detail.
 */
const BATTLE_NOT_FOUND_REASON = 'BATTLE_NOT_FOUND';

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
 *     items 1–2, §2 `JoinBattle`) — coordination only,
 *   - reconnect/resync recovery (`recoverBattleState`, SIGNALR_PROTOCOL.md §7,
 *     ADR-008) — the documented `GetBattleState` snapshot request, ingested by
 *     the same `receiveBattleState` path the §4 push uses.
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
        // A runtime that holds a battle is not "awaiting" one: the §7 snapshot
        // request below is what restores synchronization, and until it lands
        // the client is synchronized with nothing (`unsynchronized`). A runtime
        // that never had a battle is simply awaiting one, and no recovery is
        // requested for it (see `recoverBattleState`).
        const hasBattle = this.battleState !== null;

        this.updateState({
          connection: 'connected',
          connectionId: connectionId ?? this.state.connectionId,
          sync: hasBattle ? 'unsynchronized' : 'awaiting_battle',
          runtime: 'ready',
          lastError: null,
        });
        this.emit({ type: 'reconnected', state: this.state });

        // SIGNALR_PROTOCOL.md §7.1: on reconnect the client requests the
        // authoritative snapshot. Requested only when a current battle is
        // known; the request is asynchronous, so the transition above is
        // reported first and the recovered state follows through the existing
        // §4 ingestion path.
        void this.recoverBattleState();
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
   * Every other action kind — including the `GetBattleState` snapshot request —
   * still rejects with `RuntimeActionNotImplementedError`. §7's snapshot is not
   * an action a caller submits: it is requested by the runtime's own reconnect
   * handling (`recoverBattleState`), from the battle identity the runtime
   * already holds.
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
  // Reconnect / resync recovery (SIGNALR_PROTOCOL.md §7, ADR-008)
  // ---------------------------------------------------------------------------

  /**
   * Requests the authoritative snapshot after a reconnect and synchronizes the
   * runtime from it (`SIGNALR_PROTOCOL.md` §7.1–§7.3, `ADR-008`).
   *
   * ```text
   * SignalR reconnect
   *         ↓
   * SignalRService.getBattleState(battleId)
   *         ↓
   * BattleHub.GetBattleState
   *         ↓
   * authoritative snapshot
   *         ↓
   * receiveBattleState()          ← the existing §4 ingestion path
   * ```
   *
   * **Only a known battle is recovered.** The identity is the one the runtime
   * already holds from the server's own state (`§4.9`) — the same source
   * `requestAction` resolves an action's battle from, and the same source
   * `BattleScene` reads through `getBattleState()`. There is no second battle-id
   * store, no URL/localStorage/React/scene-sourced id, and no caller-supplied
   * one. With no battle known there is nothing to recover, so no request is
   * issued and no state is invented.
   *
   * **The snapshot is ingested by the existing path.** An accepted response is
   * routed through `receiveBattleState` (`§4`), so recovery produces exactly
   * what a push produces and no more: the recovered snapshot replaces the
   * runtime's copy, `sync` reaches `'synchronized'`, and the existing
   * `battle_state_changed` notification and `battleStateListeners` dispatch
   * fire. No second ingestion path, no second state store, and no
   * recovery-specific state model exists.
   *
   * **Local prediction is discarded, not merged.** §7.2 makes the snapshot
   * authoritative: nothing the client previously held is merged into it, and a
   * snapshot is never reconciled against or rejected in favour of a stale local
   * copy. Missed events are not replayed and none is requested — a
   * desynchronized client resynchronizes from a snapshot only (§8 item 8,
   * `ARCHITECTURE.md` §5.2).
   *
   * **No sequence logic is invented.** The response's `serverSequence` is the
   * server's `BattleState.Sequence` and is documented state metadata, not a
   * client correlation id (`§5.2` item 3): the runtime does not seed
   * `clientSequence` from it, does not compare the snapshot against it, does not
   * increment it, and does not reject a snapshot on it. The value the runtime
   * stores is the one the snapshot itself carries, exactly as sent.
   */
  private async recoverBattleState(): Promise<void> {
    // §7.1's precondition: a current battle is known. `battleId` comes from the
    // runtime's own synchronized state and nowhere else.
    const battleId = this.battleState?.battleId;

    if (battleId === undefined) {
      return;
    }

    let response: BattleStateSnapshotResponse;

    try {
      response = await this.signalR.getBattleState(battleId);
    } catch (error) {
      // A transport failure is a technical failure, not battle state: nothing
      // is fabricated and the runtime keeps whatever it held. The §4 push
      // remains the only other way state arrives.
      const detail = this.describeError(error);
      this.updateState({ lastError: detail });
      this.emit({ type: 'runtime_error', state: this.state, detail });
      return;
    }

    if (response.accepted) {
      // The documented §4 shape, validated and stored by the same reader the
      // push uses — no recovery-only validator and no recovery-only model.
      this.receiveBattleState(response.state);
      return;
    }

    if (response.reason === BATTLE_NOT_FOUND_REASON) {
      await this.handleBattleNotRecoverable(battleId);
      return;
    }

    // Any other rejection: reported as a technical failure, with no state
    // fabricated to fill the gap. No second error state is introduced.
    const detail =
      `GetBattleState was rejected (${response.reason ?? 'no reason given'}); ` +
      'no snapshot was received.';
    this.updateState({ lastError: detail });
    this.emit({ type: 'runtime_error', state: this.state, detail });
  }

  /**
   * `SIGNALR_PROTOCOL.md` §7.3's `BATTLE_NOT_FOUND` behavior.
   *
   * The snapshot cannot be recovered — the active-state record expired or was
   * cleared, or the battle is not the caller's, which the contract makes one
   * indistinguishable answer (`REDIS_STATE.md` §3, `API_CONTRACTS.md` §4 notes
   * 6–7) — so the client **treats the battle as ended**.
   *
   * 1. **The stale copy is discarded, and nothing stands in for it.** The
   *    runtime no longer holds a live battle, so `battleState` is cleared rather
   *    than kept or merged: `getBattleState()` reports `null`, and
   *    `requestAction` can no longer address a battle that has ended. No
   *    gameplay value is derived, and the client does not try to work out
   *    *which* of the indistinguishable cases occurred — no Redis detail and no
   *    ownership information is exposed.
   * 2. **The documented fallback is the existing battle-result route.**
   *    `GET /api/battle/{battleId}/result` (`API_CONTRACTS.md` §4) is already
   *    implemented client-side as `ApiService.getBattleResult`, so §7.3's
   *    fallback reuses that path: no second recovery mechanism, no new SignalR
   *    method or event, and no change to the server response contract. A battle
   *    whose record expired before completion has no persisted result and
   *    answers `404 BATTLE_NOT_FOUND` (`REDIS_STATE.md` §3, "no partial result
   *    is written to PostgreSQL") — that is the documented outcome of the
   *    fallback, not a client defect.
   *
   * The runtime neither stores nor interprets a completed battle's result: the
   * persisted result is presentation data (`GAME_STATE.md` §4 — client
   * presentation state, owned by the client), not part of the synchronized
   * battle copy this runtime holds and versions. §7.3 requires the fallback to
   * be taken, and it is.
   */
  private async handleBattleNotRecoverable(battleId: string): Promise<void> {
    // Treated as ended: the synchronized copy is dropped, so no stale client
    // state survives and no state is reconstructed locally. `sync` returns to
    // the documented "connected, no current battle" value rather than adding a
    // recovery-specific one.
    this.battleState = null;
    this.updateState({ sync: 'awaiting_battle', lastError: null });

    try {
      await this.api.getBattleResult(battleId);
    } catch (error) {
      // The fallback is best-effort by contract: an expired battle legitimately
      // has no result row. A failure is reported through the existing technical
      // error channel and fabricates no battle state.
      const detail = this.describeError(error);
      this.updateState({ lastError: detail });
      this.emit({ type: 'runtime_error', state: this.state, detail });
    }
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

  /**
   * A completed battle's persisted result (`API_CONTRACTS.md` §4) —
   * `GET /api/battle/{battleId}/result`.
   *
   * This is what `ResultScene` reads the persisted `rewards`
   * (`RewardSummary`) from. §4 returns data only for a battle that has
   * **already ended**, which is the only state in which a terminal outcome has
   * reached the presentation layer — so the read is well-formed by
   * construction and this method invents no "not yet ended" behavior.
   *
   * The runtime delegates to the existing `ApiService` method and nothing else:
   * it adds no orchestration, does not cache the response, and does not hold it
   * as state. The persisted result is client presentation data
   * (`GAME_STATE.md` §4), not part of the synchronized battle copy this runtime
   * versions, so it is deliberately not stored here.
   *
   * It computes no reward: no XP is summed, no Level is derived from XP, and no
   * `null` member is promoted to a number. Reward amounts and the resulting
   * progression are server-authored (`GAME_RULES.md` §18, `AGENTS.md` §10,
   * ADR-001).
   *
   * This reuses the same route §7.3's fallback read already takes; it
   * introduces no second endpoint, no query parameter, and no second retrieval
   * mechanism.
   */
  public async getBattleResult(battleId: string): Promise<BattleResultResponse> {
    return await this.api.getBattleResult(battleId);
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
   * `rngSeed`, `rngState`, `board`, `playerState`, `petState`, `bossState`
   * (`GAME_STATE.md` §2.0.5, §2.2, §2.3, §2.4).
   * Nothing is derived from it, and no gameplay meaning is inferred: the server
   * owns these values (§4.9, ADR-001). In particular the board is stored as
   * received and is never generated, filled, repaired, or re-derived by the
   * client (§4 item 10), `playerState`'s Match/Combo values are rendered, never
   * counted or recomputed (`MATCH3_RULES.md` §6.6 item 3), `petState`'s
   * delivered members are stored verbatim — the runtime never charges a
   * Passive, evaluates a Threshold, or resets progress (§4.3 item 9), and never
   * applies, refreshes, decrements, expires, or removes a Status Effect
   * (§4.3 item 14) — and `bossState`'s two values are rendered, never damaged,
   * clamped, or re-derived (§4.4 item 7).
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
   * Validates only the documented §4 shape — the `GAME_STATE.md` §2.2/§2.3/§2.4/
   * §2.0.5 fields `battleId`, `turn`, `sequence`, `rngSeed`, `rngState`, `board`,
   * `playerState`, `petState`, `bossState`.
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
   * same applies to `playerState`, `petState`, and `bossState`: their values are
   * read as sent and are never derived, clamped, or recomputed
   * (`GAME_RULES.md` §18).
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

    const bossState = this.readBossState(candidate.bossState);
    if (bossState === null) {
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
      bossState,
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
   * `passiveResetOverride` is one optional member: it is present iff the
   * Passive's reset behavior is non-default, and its absence *is* the statement
   * "default" (§4.3 item 7). Absence is therefore tolerated and stored as
   * absence — the runtime never substitutes `"Default"`, `null`, or any other
   * value for it, because writing one would be a second spelling of one fact.
   *
   * `statusEffects` is the other always-present member, and its empty case is
   * spelled by an **empty array**, not by absence (§4.3 item 14): it is required
   * here for exactly that reason — a client must not read a missing
   * `statusEffects` as "no effect is active". Each element is read through
   * `readStatusEffect`, which keeps the element's three optional members absent
   * when they do not apply rather than materializing them as `null`
   * (`GAME_STATE.md` §2.3.1 item 7).
   *
   * Every value is read as sent. The runtime does not charge a Passive, evaluate
   * a Threshold, reset progress, or apply an overflow (§4.3 item 9); it does not
   * apply, refresh, decrement, expire, or remove a Status Effect and does not
   * re-derive the collection from the board, the counters, or an event
   * (§4.3 item 14); and it does not widen the object to the rest of §2.3 —
   * identity, progression, combat stats, the sibling modifier collections, and
   * the Relic loadout snapshot are not delivered (§4.3 item 2), while
   * `equippedCards` is delivered per §4.3 item 13.
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

    // §4.3 item 14: the collection is always present and an active Pet with no
    // active effect is sent an empty array — never an omission and never `null`.
    // A payload that omits it is therefore outside the contract, and the runtime
    // must not read the absence as "no effect is active".
    if (!Array.isArray(candidate.statusEffects)) {
      return null;
    }

    const statusEffects: RuntimeStatusEffect[] = [];
    for (const raw of candidate.statusEffects) {
      const effect = this.readStatusEffect(raw);
      if (effect === null) {
        return null;
      }
      statusEffects.push(effect);
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
          statusEffects,
        }
      : {
          passiveId: candidate.passiveId,
          passiveProgress,
          equippedCards: [...candidate.equippedCards],
          statusEffects,
          passiveResetOverride: candidate.passiveResetOverride,
        };
  }

  /**
   * Reads one delivered Status Effect instance (`GAME_STATE.md` §2.3.1, §2.3.2
   * item 3; `SIGNALR_PROTOCOL.md` §4.3 item 14).
   *
   * The four required members are checked for type, because they are what makes
   * the element an instance at all (§2.3.1 items 1–2): `id` names the effect,
   * `type` and `source` select from their documented closed sets, and
   * `magnitude` carries the applied value. An element missing one is malformed
   * rather than defaultable.
   *
   * The three optional members are present iff they apply and are **absent**
   * otherwise — never `null`, never a sentinel string (§2.3.1 item 7, §3.2.5).
   * The reader therefore stores each of them only when it actually arrived, so
   * "does not apply" stays distinguishable from a value the runtime invented.
   * Nothing here decrements `remainingTurns`, evaluates `expiryCondition`, or
   * interprets `magnitude` — those belong to `GAME_STATE.md` §5.1.1 /
   * `COMBAT_RULES.md` §5 and to the server (`GAME_RULES.md` §18).
   */
  private readStatusEffect(value: unknown): RuntimeStatusEffect | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as Partial<RuntimeStatusEffect>;

    if (typeof candidate.id !== 'string' || candidate.id.length === 0) {
      return null;
    }
    if (typeof candidate.type !== 'string' || candidate.type.length === 0) {
      return null;
    }
    if (typeof candidate.source !== 'string' || candidate.source.length === 0) {
      return null;
    }
    if (typeof candidate.magnitude !== 'number') {
      return null;
    }

    // `remainingTurns` and `expiryCondition` are mutually exclusive duration
    // models (§2.3.1 item 3), so neither may be read as a substitute for the
    // other: each is either the delivered value or absent.
    if (candidate.remainingTurns !== undefined && typeof candidate.remainingTurns !== 'number') {
      return null;
    }
    if (candidate.targetStat !== undefined && typeof candidate.targetStat !== 'string') {
      return null;
    }
    if (
      candidate.expiryCondition !== undefined &&
      typeof candidate.expiryCondition !== 'string'
    ) {
      return null;
    }

    const effect: {
      id: string;
      type: string;
      source: string;
      magnitude: number;
      targetStat?: string;
      remainingTurns?: number;
      expiryCondition?: string;
    } = {
      id: candidate.id,
      type: candidate.type,
      source: candidate.source,
      magnitude: candidate.magnitude,
    };

    if (candidate.targetStat !== undefined) {
      effect.targetStat = candidate.targetStat;
    }
    if (candidate.remainingTurns !== undefined) {
      effect.remainingTurns = candidate.remainingTurns;
    }
    if (candidate.expiryCondition !== undefined) {
      effect.expiryCondition = candidate.expiryCondition;
    }

    return effect;
  }

  /**
   * Reads the Boss HP projection (`GAME_STATE.md` §2.4,
   * `SIGNALR_PROTOCOL.md` §4.4).
   *
   * Both values are required and neither is nullable: the Boss exists from battle
   * creation at full health, so there is no absent or "Boss not yet available"
   * case for either member and a payload missing one is malformed rather than
   * implicitly zero (§4.4 item 4). In particular the runtime must not read an
   * absent `hp` as `0`.
   *
   * The two are read independently: neither is derived from the other, `hp` is
   * never clamped to `maxHp`, and no unit, scale, or rounding is applied
   * (§4.4 item 5). The object is a projection, not `BossState` — the runtime
   * models no other Boss member, and it does not re-derive these values from the
   * events or from `finalBossHp` (§4.4 items 3 and 7, `GAME_RULES.md` §18).
   */
  private readBossState(value: unknown): RuntimeBossState | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as Partial<RuntimeBossState>;

    if (typeof candidate.hp !== 'number' || typeof candidate.maxHp !== 'number') {
      return null;
    }

    return { hp: candidate.hp, maxHp: candidate.maxHp };
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