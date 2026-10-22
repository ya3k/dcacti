import { ApiRequestError, ApiService } from '../../services/api/ApiService';
import type {
  BattleHistoryItemResponse,
  BattleResultResponse,
  BattleStartRequest,
} from '../../services/api/ApiService';
import type { CardResponse, PetResponse, RelicResponse } from '../../services/api/CollectionModels';
import { ApplicationSession } from '../../services/api/ApplicationSession';
import { SignalRService } from '../../services/realtime/SignalRService';
import type { BattleStateSnapshotResponse } from '../../services/realtime/SignalRService';
import { createActionId } from './ActionIdentity';
import {
  INITIAL_RUNTIME_STATE,
  type GameRuntimeState,
} from '../../state/GameRuntimeState';
import {
  DEFAULT_RESOLUTION_WATCHDOG_MS,
  RUNTIME_ACTION_SWAP,
  RUNTIME_ACTION_CARD_CAST,
  RUNTIME_ACTION_PET_SKILL_CAST,
  RuntimeActionNotImplementedError,
  RuntimeActionOutcomeUnknownError,
  readCombatTrace,
  type BattleEventsEnvelope,
  type BattleEventsListener,
  type BattleStateListener,
  type BattleStateMetadata,
  type BattleStateSource,
  type GameRuntimeOptions,
  type GameRuntimePort,
  type RuntimeActionAcknowledgement,
  type RuntimeActionRequest,
  type RuntimeBattleState,
  type RuntimeBoard,
  type RuntimeBossState,
  type RuntimeCell,
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
 * Counter behind the opaque correlation id the non-Swap actions carry
 * (`SIGNALR_PROTOCOL.md` §2 item 1).
 *
 * It is a per-process request counter and nothing else: `CardCast` and
 * `PetSkillCast` carry this value as an opaque correlation id, and nothing on
 * either path compares it with any server value. It is **not** the Swap action
 * identity — that is `createActionId`, whose value must stay unique across a
 * runtime reset, and which the Swap path uses instead of this counter.
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
 * One armed resolution-batch expectation (`SIGNALR_PROTOCOL.md` §7, TASK-284).
 *
 * The three association keys are exactly the ones the approved design fixes:
 * the battle the advance was delivered for, the identity of the action the
 * runtime submitted against it, and **the version the delivered state push
 * carries**. The sequence is read from the delivery and is never derived from
 * `expectedSequence`, `held.sequence`, or any increment of either
 * (`GAME_STATE.md` §5.0 — the client does not author `Sequence`).
 */
interface ResolutionBatchExpectation {
  readonly battleId: string;
  readonly actionIdentity: string;
  readonly sequence: number;
  readonly timer: ReturnType<typeof setTimeout>;
}

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
 *   - server event subscription (forwarding `ReceiveEvents` batches) and the
 *     **delivery-integrity check** that decides whether one may be forwarded: a
 *     batch for another battle, or one whose `serverSequence` is ahead of the
 *     synchronized state's `sequence`, is withheld and the authoritative snapshot
 *     is requested instead (`SIGNALR_PROTOCOL.md` §6 item 3, §7.1; the narrow
 *     exception `ARCHITECTURE.md` §2.2.1 rule 4 records),
 *   - the synchronized copy of the server's authoritative battle state
 *     (`BattleStateUpdated`, SIGNALR_PROTOCOL.md §4, and the §7.1 snapshot),
 *   - the monotonic guard on ingesting that copy (`receiveBattleState`): a
 *     payload for the active battle that is behind the sequence already
 *     delivered, and a payload for a battle no lifecycle transition has
 *     established, are refused rather than stored, so the synchronized copy
 *     cannot be overwritten by stale or cross-battle state (`GAME_STATE.md` §5),
 *   - scene lifecycle coordination (scenes publish/receive through the port),
 *   - the client → server request boundary for the documented Swap action
 *     (`requestAction`, SIGNALR_PROTOCOL.md §2.1) — coordination only,
 *   - battle-start orchestration (`startBattle`, SIGNALR_PROTOCOL.md §1
 *     items 1–2, §2 `JoinBattle`) — coordination only,
 *   - reconnect/resync recovery (`SIGNALR_PROTOCOL.md` §7, ADR-008) — the
 *     documented group re-join of §7 item 4 (`JoinBattle`, §1.2, §2) followed
 *     by the `GetBattleState` snapshot request (`recoverBattleState`, §7.1),
 *     ingested by the same `receiveBattleState` path the §4 push uses. The same
 *     snapshot path serves a **detected desynchronization**, not only a
 *     reconnect: §6 item 3's skipped `serverSequence`, an action whose
 *     acknowledgement never arrived (`markActionOutcomeUnknown`), and an accepted
 *     action whose delivered state push was never followed by its
 *     `ReceiveEvents` batch (the one bounded resolution-batch expectation below,
 *     TASK-284) request the same §7.1 snapshot, deduplicated to one request in
 *     flight per battle, and the bounded retry of `MAX_RECOVERY_ATTEMPTS` is what
 *     keeps a persistent failure from spinning,
 *   - the client-local post-result cleanup (`clearActiveBattleState`) — dropping
 *     the synchronized copy when the player leaves a completed battle
 *     (`D-202-04 = A`, ARCHITECTURE.md §2.2.3, ADR-022): state only, no
 *     transport operation, no result read, and no effect on the preserved
 *     pre-battle loadout, which this runtime does not hold,
 *   - the authenticated-session cleanup (`invalidateSession`) — the one routine
 *     both approved session-ending paths run (ADR-020 D4 item 2, the explicit
 *     sign-out; API_CONTRACTS.md §2.3, the authenticated transport's `401
 *     UNAUTHENTICATED`): the canonical credential clear, the runtime's own
 *     `session` publication, the synchronized battle copy and the authenticated
 *     transport connection. It issues no HTTP request and introduces no
 *     endpoint, wire message, or server-side revocation.
 *
 * It deliberately does NOT (task §9, AGENTS.md §10, ADR-001):
 *   - calculate damage, match, combo, cascade, passive, or power,
 *   - validate a Swap, decide whether one is legal, or commit an exchange,
 *   - interpret, reorder, or synthesise Battle Events, or drop an event from a
 *     batch it forwards: the delivery-integrity check withholds or dispatches a
 *     batch **whole**, reads no event payload, and decides nothing an event
 *     means (`ARCHITECTURE.md` §2.2.1 rule 4),
 *   - author, adjust, or recompute any battle state — refusing to store a
 *     payload is not authoring it: the guard above compares the delivered
 *     version and either stores what arrived or keeps what it holds, and no
 *     value is ever adjusted, derived, or invented,
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
  /**
   * The battle identity a lifecycle transition has explicitly established for
   * the *next* active battle, or `null` when no transition is outstanding.
   *
   * `startBattle` sets it from the server's own `BattleStartResponse.battleId`
   * (SIGNALR_PROTOCOL.md §1 items 1–2, `GAME_RULES.md` §18, ADR-001) — that is
   * the documented lifecycle transition, and it is what authorises the §4 push
   * for a battle other than the synchronized one to replace it. It is consumed
   * when that battle's own state is ingested, and dropped by every path that
   * stops holding a battle (`clearActiveBattleState`,
   * `handleBattleNotRecoverable`, `invalidateSession`) and by a start that
   * fails, which establishes nothing.
   *
   * It holds no state: it is an identity, not a battle copy, so it is never
   * exposed through `getBattleState()` and never read as gameplay data.
   */
  private establishedBattleId: string | null = null;
  private runtimeListeners = new Set<RuntimeEventListener>();
  private battleListeners = new Set<BattleEventsListener>();
  private battleStateListeners = new Set<BattleStateListener>();
  private transportUnsubscribers: Array<() => void> = [];
  private disposeTransportHandlers: (() => void) | null = null;
  private initialized = false;
  private disposed = false;

  /**
   * The action identities of logical actions this runtime has submitted and not
   * yet received an acknowledgement for, keyed by
   * {@link logicalActionKey} (`ADR-025` §2).
   *
   * It is what makes a retry of one logical action carry the identity its first
   * attempt carried, while a new logical action never reuses one. Entries are
   * removed on acknowledgement and dropped with the battle
   * (`clearActiveBattleState`), so it holds only genuinely unresolved actions.
   */
  private readonly unresolvedActionIds = new Map<string, string>();

  /**
   * Maximum consecutive snapshot recovery retry attempts before terminating
   * the recovery retry loop (P0.2 §4.E). Bounded to prevent tight or infinite
   * spinning when server snapshots are persistently unavailable or stale.
   */
  private static readonly MAX_RECOVERY_ATTEMPTS = 3;

  /**
   * Pending retry timer handle for snapshot recovery backoff. Cleared when
   * recovery succeeds, the battle ends/clears, or the runtime is disposed.
   */
  private recoveryRetryTimer: ReturnType<typeof setTimeout> | null = null;

  /**
   * The battle one recovery request is currently in flight for, or `null` when
   * none is (`SIGNALR_PROTOCOL.md` §7.1).
   *
   * **At most one recovery request is in flight per battle.** A second
   * observation of the same unresolved desynchronization — another batch of the
   * same gap, an unanswered action, a repeated client request — joins the
   * request already in flight instead of opening a second one, which is what
   * keeps a desynchronized client from issuing a storm of identical
   * `GetBattleState` calls. It is cleared on every path that ends, supersedes, or
   * abandons the attempt (`receiveBattleState`'s convergence, the bounded retry,
   * `handleBattleNotRecoverable`, `clearActiveBattleState`, `invalidateSession`,
   * `dispose`).
   */
  private recoveryInFlightBattleId: string | null = null;

  /**
   * The newest `ReceiveEvents.serverSequence` withheld as a delivery-integrity
   * gap (`forwardBattleEvents`), or `null` when no gap is outstanding.
   *
   * It is the one piece of bookkeeping the gap path adds: a repeated
   * observation of the **same** gap does not re-request a snapshot that is
   * already in flight for it, and a recovered state at or past it clears the
   * entry. It is a delivered version used for coordination only — never a
   * gameplay value, and never compared across two different battles.
   */
  private withheldGapSequence: number | null = null;

  /**
   * The identity of the action the runtime submitted against the synchronized
   * battle and has not yet seen settled by a delivered resolution batch, or
   * `null` when no such action is outstanding (`TASK-284`).
   *
   * It is what lets an armed expectation name **the action it belongs to**
   * rather than a battle alone. It deliberately survives an accepted
   * acknowledgement: `SIGNALR_PROTOCOL.md` §3.1 item 1 publishes the state before
   * the hub call returns, so the resolution's push may legitimately arrive before
   * the caller is told the action was accepted. It is dropped by a definitive
   * rejection (a rejected action has no resolution and no batch — §3.1 item 4),
   * by the admission of the batch that settles it, by a recovery snapshot, and by
   * every battle boundary.
   *
   * It is a request identity, never battle state and never a gameplay value: it
   * is the value the action was submitted with (`ADR-025` §2's retry-stable
   * `actionId` for a Swap, the opaque per-request correlation id for a cast).
   */
  private pendingResolutionAction: { battleId: string; identity: string } | null = null;

  /**
   * How long the runtime waits for the `ReceiveEvents` batch of a delivered
   * authoritative state advance before it treats that batch as lost
   * (`SIGNALR_PROTOCOL.md` §7, TASK-284). Defaults to
   * {@link DEFAULT_RESOLUTION_WATCHDOG_MS}.
   *
   * The duration is client policy (`GameRuntimeOptions`); the behaviour is the
   * documented one, and nothing here is a second recovery mechanism.
   */
  private readonly resolutionWatchdogMs: number;

  /**
   * The one armed, bounded resolution-batch expectation
   * (`SIGNALR_PROTOCOL.md` §7, TASK-284), or `null` when none is armed.
   *
   * ```text
   * armed     a §4 state push that ADVANCES the held sequence, for the battle an
   *           action was submitted against (`pendingResolutionAction`), and the
   *           sequence it carries is the expectation's own
   * cleared   the matching batch is admitted; a recovery snapshot covers it; the
   *           battle is replaced, cleared, ended or disposed (clearRecoveryState)
   * expires   publish `sync = 'unsynchronized'` with a technical detail, then
   *           request the EXISTING §7 item 1 snapshot (recoverBattleState)
   * ```
   *
   * It is at most one, it fires at most once, and every retry after it belongs to
   * the existing bounded recovery: no second timer family, retry loop, or
   * recovery path exists. Expiry releases no input lock, publishes no state, and
   * determines no outcome — the scene converges on the snapshot the request
   * delivers (`ADR-008`).
   */
  private resolutionExpectation: ResolutionBatchExpectation | null = null;

  private clearRecoveryRetry(): void {
    if (this.recoveryRetryTimer !== null) {
      clearTimeout(this.recoveryRetryTimer);
      this.recoveryRetryTimer = null;
    }
  }

  /**
   * Drops every piece of recovery bookkeeping: the retry timer, the in-flight
   * marker, the withheld gap, and the missing-batch expectation with the action
   * identity it named.
   *
   * Every boundary that stops the runtime holding a battle — or that has just
   * converged one — releases all of them together, so no timer, no in-flight
   * marker, no stale gap, and no watchdog for a battle the runtime no longer
   * holds can outlive the battle they belong to.
   */
  private clearRecoveryState(): void {
    this.clearRecoveryRetry();
    this.recoveryInFlightBattleId = null;
    this.withheldGapSequence = null;
    this.clearResolutionExpectation();
    this.pendingResolutionAction = null;
  }

  /**
   * Cancels the armed expectation and its timer, if any (`TASK-284`).
   *
   * Both halves go together: a surviving timer for a cleared or replaced
   * expectation is exactly the orphaned watchdog this must not leave behind, and
   * {@link expireResolutionExpectation} refuses to act for an expectation that is
   * no longer the armed one.
   */
  private clearResolutionExpectation(): void {
    if (this.resolutionExpectation !== null) {
      clearTimeout(this.resolutionExpectation.timer);
      this.resolutionExpectation = null;
    }
  }

  /**
   * Arms (or re-arms) the one expectation that the delivered advance at
   * `sequence` will be followed by its `ReceiveEvents` batch (`TASK-284`,
   * `SIGNALR_PROTOCOL.md` §7).
   *
   * `battleId` and `sequence` are the delivered push's own values and
   * `actionIdentity` is the submitted action's — none is computed here. Re-arming
   * cancels the previous expectation, so a newer advance supersedes an older
   * missing batch: the newer recovery snapshot covers it, and an older
   * expectation may not open a second concurrent request.
   */
  private armResolutionExpectation(
    battleId: string,
    sequence: number,
    actionIdentity: string
  ): void {
    this.clearResolutionExpectation();

    const expectation: ResolutionBatchExpectation = {
      battleId,
      actionIdentity,
      sequence,
      timer: setTimeout(() => {
        this.expireResolutionExpectation(expectation);
      }, this.resolutionWatchdogMs),
    };

    this.resolutionExpectation = expectation;
  }

  /**
   * The expectation's one expiry path (`TASK-284`, `SIGNALR_PROTOCOL.md` §7).
   *
   * It publishes the desynchronization it observed — the delivered `sequence`
   * whose batch never arrived, and the action identity it belongs to — through
   * the existing technical channel (`sync` plus `lastError` and the
   * `runtime_error` event), then invokes the **existing** `recoverBattleState()`,
   * whose one-request-in-flight rule and `MAX_RECOVERY_ATTEMPTS` bound it
   * inherits. It releases no input lock, publishes no battle state, presents
   * nothing, and fabricates no outcome: the snapshot the request delivers is what
   * the scene converges on (`ADR-008`).
   *
   * ```text
   * not the armed expectation            → inert: it was replaced or cleared
   * disposed                             → inert: nothing outlives disposal
   * no battle, or another battle         → inert: an obsolete watchdog may not
   *                                        recover the battle that replaced it
   * the held sequence moved past it      → inert: the newer delivered state already
   *                                        covers that resolution (§7 precedence)
   * otherwise                            → publish, then request the snapshot once
   * ```
   */
  private expireResolutionExpectation(expectation: ResolutionBatchExpectation): void {
    // Identity, not equality: only the expectation still armed may act, so a
    // replaced or cancelled one can never recover anything.
    if (this.resolutionExpectation !== expectation) {
      return;
    }

    this.resolutionExpectation = null;

    if (this.disposed) {
      return;
    }

    const held = this.battleState;

    if (held === null || held.battleId !== expectation.battleId) {
      return;
    }

    // The expectation answers one delivered advance. A held copy that has moved
    // past it is the newer state that already represents that resolution
    // (`GAME_STATE.md` §5.1 item 3), so the missing batch is superseded and there
    // is nothing to resynchronize.
    if (held.sequence !== expectation.sequence) {
      return;
    }

    const detail =
      `No ReceiveEvents batch arrived for the resolution delivered as ` +
      `${expectation.battleId} sequence ${expectation.sequence} (action ` +
      `"${expectation.actionIdentity}") within ${this.resolutionWatchdogMs} ms: the ` +
      'batch is treated as lost, the client is unsynchronized, and the authoritative ' +
      'snapshot is requested instead of replaying events (SIGNALR_PROTOCOL.md §7).';

    this.updateState({ sync: 'unsynchronized', lastError: detail });
    this.emit({ type: 'runtime_error', state: this.state, detail });

    // Best-effort and self-contained, exactly as `markActionOutcomeUnknown` and
    // `withholdBatchAheadOfState` are: `recoverBattleState` reports its own
    // failure and bounds its own retry, so this never throws into a timer and
    // never retries on its own (the expectation fires once).
    void this.recoverBattleState();
  }

  /**
   * The one ingestion point's reconciliation of the missing-batch expectation
   * (`TASK-284`, `SIGNALR_PROTOCOL.md` §7).
   *
   * ```text
   * a recovery snapshot        → supersede: the expectation it answers is over
   * a different battle         → supersede: no watchdog may outlive its battle
   * a §4 push that advances    → arm for the DELIVERED sequence, when an action
   *   the held sequence          was submitted against that battle and is unsettled
   * a §4 push at the same      → leave as it is: nothing new was delivered
   *   sequence (a duplicate)
   * ```
   *
   * It stores nothing, derives no sequence, and interprets no gameplay value: it
   * only decides whether one delivery-timing expectation is armed, superseded, or
   * left alone.
   */
  private reconcileResolutionExpectation(
    state: RuntimeBattleState,
    source: BattleStateSource,
    previousBattle: RuntimeBattleState | null
  ): void {
    const battleChanged =
      previousBattle !== null && previousBattle.battleId !== state.battleId;

    if (source === 'recovery' || battleChanged) {
      this.clearResolutionExpectation();
      this.pendingResolutionAction = null;
      return;
    }

    if (source !== 'push' || previousBattle === null) {
      return;
    }

    const pending = this.pendingResolutionAction;

    if (pending === null || pending.battleId !== state.battleId) {
      return;
    }

    if (state.sequence > previousBattle.sequence) {
      this.armResolutionExpectation(state.battleId, state.sequence, pending.identity);
    }
  }

  /**
   * Settles the expectation when the batch it was waiting for is admitted, and
   * forgets the submitted action it named (`TASK-284`).
   *
   * An admitted batch at or beyond the expectation's own sequence is the
   * resolution that expectation was about (`GAME_STATE.md` §5.1 item 3), and an
   * admitted batch at the held sequence is the matching batch of the newest
   * delivered advance — the resolution the submitted action is waiting on. A
   * batch below the expectation's sequence is a re-delivery or a superseded
   * resolution and settles nothing.
   */
  private settleResolutionExpectation(envelope: BattleEventsEnvelope, heldSequence: number): void {
    const expectation = this.resolutionExpectation;

    if (
      expectation !== null &&
      expectation.battleId === envelope.battleId &&
      envelope.serverSequence >= expectation.sequence
    ) {
      this.clearResolutionExpectation();
    }

    const pending = this.pendingResolutionAction;

    if (
      pending !== null &&
      pending.battleId === envelope.battleId &&
      envelope.serverSequence === heldSequence
    ) {
      this.pendingResolutionAction = null;
    }
  }

  /**
   * Forgets the submitted action an expectation may be armed with, for one
   * battle (`TASK-284`).
   *
   * A definitive rejection settles the action as a gameplay no-op
   * (`SIGNALR_PROTOCOL.md` §5 item 2, `MATCH3_RULES.md` §2.1.5): nothing was
   * committed for it, so no resolution of its own is outstanding and no
   * expectation may be armed in its name. An already-armed expectation survives,
   * because it belongs to an advance that *was* delivered and whose batch is
   * still expected.
   */
  private forgetSubmittedAction(battleId: string): void {
    if (this.pendingResolutionAction?.battleId === battleId) {
      this.pendingResolutionAction = null;
    }
  }

  constructor(
    private readonly signalR: SignalRService = SignalRService.getInstance(),
    private readonly api: ApiService = ApiService.getInstance(),
    options: GameRuntimeOptions = {}
  ) {
    this.resolutionWatchdogMs =
      options.resolutionWatchdogMs ?? DEFAULT_RESOLUTION_WATCHDOG_MS;
  }

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
        // A runtime that holds a battle is not "awaiting" one: the §7 flow below
        // is what restores synchronization, and until it lands the client is
        // synchronized with nothing (`unsynchronized`). A runtime that never had
        // a battle is simply awaiting one, and no reconnect work is requested
        // for it (see `rejoinAndRecoverAfterReconnect`).
        const hasBattle = this.battleState !== null;

        this.updateState({
          connection: 'connected',
          connectionId: connectionId ?? this.state.connectionId,
          sync: hasBattle ? 'unsynchronized' : 'awaiting_battle',
          runtime: 'ready',
          lastError: null,
        });
        this.emit({ type: 'reconnected', state: this.state });

        // SIGNALR_PROTOCOL.md §7 items 4 and 1: the new connection first
        // re-joins the battle's group, and only then is the authoritative
        // snapshot requested. Both steps are asynchronous, so the transition
        // above is reported first; the recovered state follows through the
        // existing §4 ingestion path.
        void this.rejoinAndRecoverAfterReconnect();
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
    this.clearRecoveryState();

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

  /**
   * Ends the current authenticated session — the single cleanup both approved
   * session-ending paths run.
   *
   * ```text
   * explicit sign-out control (ADR-020 D4 item 2)   ─┐
   *                                                    ├─→ invalidateSession()
   * a `401` on the authenticated transport (§2.3)   ─┘
   *      ├── ApplicationSession.clear()          the canonical credential clear
   *      ├── battleState = null                  no copy outlives the session
   *      ├── transport unsubscribed + disconnected  no authenticated connection
   *      ├── initialized = false                 the runtime is re-armable
   *      └── session = 'unauthenticated'         the state `App` already renders
   *                                              `AuthScreen` for (ADR-020 D5)
   * ```
   *
   * **One cleanup, two triggers, no second session state.** The session is
   * cleared through the one credential store `ADR-015`/`ADR-020` define
   * (`ApplicationSession`), and the session status is published through the
   * runtime state this class already owns (`setSessionStatus`,
   * `state/GameRuntimeState.ts`). No event bus, store, status value, or
   * `localStorage` key is added (`AGENTS.md` §9, `ARCHITECTURE.md` §2.2.1
   * rules 3 and 5), and `App`'s existing `session !== 'authenticated'` render
   * condition is the only route back to `AuthScreen` (`ADR-020` D5).
   *
   * **Idempotent, and safe when nothing is held.** The canonical store is the
   * only "is a session held" fact, so running this twice — including two
   * overlapping `401`s from in-flight reads, which `API_CONTRACTS.md` §2.3 makes
   * indistinguishable — clears once and disconnects once: the credential clear
   * is synchronous, so the second caller finds no session and returns before
   * touching the transport. A client that holds no session (never
   * authenticated, or already invalidated) is a silent no-op.
   *
   * **The signed-out token keeps its documented server-side validity.** No
   * request is issued and no revocation is attempted: `API_CONTRACTS.md` §2.3
   * "Lifecycle (MVP)" and `ADR-015` D5 fix revocation as absent, so this is a
   * client-side release only — the token stays valid until its 24-hour absolute
   * expiry.
   *
   * **The runtime is not left disposed.** `dispose()` is terminal and
   * `initialize()` returns early once initialized, so the cleanup releases the
   * transport and re-arms initialization instead: the next authenticated
   * session reconnects and re-registers its `ReceiveEvents` /
   * `BattleStateUpdated` subscriptions through the existing paths
   * (`SIGNALR_PROTOCOL.md` §1, §3–§4), and `SignalRService.connect()` builds a
   * connection from the current session token
   * (`SignalRService.ts`'s `accessTokenFactory`) rather than reusing the one
   * that belonged to the ended session.
   *
   * Safe to call repeatedly, safe before any connection exists, and it never
   * throws.
   */
  public async invalidateSession(): Promise<void> {
    const session = ApplicationSession.getInstance();

    // The one credential store is the only session-held fact (ADR-015 D2,
    // ADR-020 D4). A runtime-owned duplicate of it would be exactly the second
    // session-state value ARCHITECTURE.md §2.2.1 rules 3/5 forbid.
    if (!session.isAuthenticated()) {
      return;
    }

    session.clear();

    // The synchronized copy belongs to the session that received it: the next
    // battle arrives as a fresh server push (SIGNALR_PROTOCOL.md §4), never as
    // a revision of this one. The unresolved action identities go with it — they
    // identify actions of a battle this runtime no longer holds (ADR-025 §2) —
    // and so does any identity a start in flight had established, which belongs
    // to the ended session.
    this.battleState = null;
    this.unresolvedActionIds.clear();
    this.establishedBattleId = null;
    this.clearRecoveryState();

    // Detach before stopping: a stop must not report itself through this runtime
    // and overwrite the unauthenticated publication below.
    for (const unsubscribe of this.transportUnsubscribers) {
      unsubscribe();
    }
    this.transportUnsubscribers = [];

    this.disposeTransportHandlers?.();
    this.disposeTransportHandlers = null;

    await this.signalR.disconnect();

    // Re-arm the runtime: `initialize()` is idempotent against a live session
    // and `dispose()` is terminal, so without this a second authentication in
    // the same document could neither reconnect nor re-subscribe.
    this.initialized = false;

    this.updateState({
      session: 'unauthenticated',
      connection: 'disconnected',
      connectionId: null,
      sync: 'unsynchronized',
      runtime: 'initializing',
      lastError: null,
    });
  }

  // ---------------------------------------------------------------------------
  // Runtime status
  // ---------------------------------------------------------------------------

  /** Called by the Phaser bridge so the runtime reflects engine lifecycle. */
  public setEngineStatus(engine: GameRuntimeState['engine']): void {
    this.updateState({ engine });
  }

  /**
   * Records the authenticated application session's status
   * (`API_CONTRACTS.md` §2.3, `ADR-020`; the session contract itself is
   * `ADR-015`). It is the status `App` renders `AuthScreen` or `GameShell`
   * from, and the value `invalidateSession()` resets to `'unauthenticated'`
   * when the session ends.
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

  /**
   * Drops the synchronized battle copy when the player leaves a completed
   * battle (`D-202-04 = A`, `ARCHITECTURE.md` §2.2.1, §2.2.3, ADR-022,
   * `GameRuntimePort.clearActiveBattleState`).
   *
   * ```text
   * ResultScene PLAY AGAIN / MAIN MENU
   *         ↓
   * clearActiveBattleState()
   *         ├── battleState = null            → getBattleState() reports null
   *         ├── sync = awaiting_battle        → when the connection is 'connected'
   *         │        unsynchronized           → otherwise
   *         └── no transport call, no result read, no preserved-loadout change
   * ```
   *
   * **Nothing is recomputed or fabricated.** The copy is dropped, not adjusted:
   * the next battle arrives as a fresh server push (`SIGNALR_PROTOCOL.md` §4)
   * through the existing `receiveBattleState` path, and the completed battle's
   * persisted result is not consulted or stored
   * (`API_CONTRACTS.md` §4, `GAME_STATE.md` §4).
   *
   * **The transport is untouched.** No connect, disconnect, or hub invocation
   * happens here — `D-202-04 = C/D` (clearing the SignalR battle connection)
   * was not approved — and the process-wide connection rule is unaffected
   * (§2.2.1 rule 6). No wire message is introduced either: §8 item 3 records that no
   * battle lifecycle/status message exists.
   *
   * **This is not §7.3's `BATTLE_NOT_FOUND` path.** That path is a failed
   * recovery and additionally takes the result fallback read
   * (`handleBattleNotRecoverable`); this is a normal lifecycle step and takes
   * no fallback.
   *
   * **The preserved pre-battle loadout is not touched.** It is not battle
   * state: it lives in the client game-presentation layer's carrier, not on
   * this runtime (ADR-022), and clearing it would make `D-202-03 = D`
   * impossible.
   *
   * Safe to call repeatedly, safe when no battle is held, and never throws.
   */
  public clearActiveBattleState(): void {
    this.battleState = null;

    // The identity a transition established belongs to a battle the runtime no
    // longer holds, so it is dropped with it: the next battle is established by
    // its own transition or by its own first push, never by a leftover identity.
    this.establishedBattleId = null;

    // No battle is held, so nothing remains unresolved in it: the identities of
    // its logical actions are dropped with it, and no later action can be
    // submitted under one (`ADR-025` §2).
    this.unresolvedActionIds.clear();
    this.clearRecoveryState();

    // The documented no-current-battle value is defined relative to the
    // connection the runtime actually has (`state/GameRuntimeState.ts`,
    // `SyncStatus`): a connected runtime with no battle is `awaiting_battle`,
    // while a runtime that is not connected is simply `unsynchronized`.
    this.updateState({
      sync: this.state.connection === 'connected' ? 'awaiting_battle' : 'unsynchronized',
      lastError: null,
    });
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
   * failures, not game state (`SIGNALR_PROTOCOL.md` §8 item 3).
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
      //
      // The two contract values are supplied separately (`ADR-025` §1–§2): the
      // state version the runtime holds as `expectedSequence`, and the identity of
      // this logical action as `actionId`.
      const fromCell = candidate.fromCell as number;
      const toCell = candidate.toCell as number;
      const submittedBattleId = battleState.battleId;
      // The pair is unordered (`MATCH3_RULES.md` §2.1.1 item 2), so the two
      // spellings of one swap are one logical action and share one identity.
      const logicalActionKey = this.logicalActionKey(
        submittedBattleId,
        RUNTIME_ACTION_SWAP,
        Math.min(fromCell, toCell),
        Math.max(fromCell, toCell)
      );
      const actionId = this.actionIdFor(logicalActionKey, 'swap');

      // The action is recorded before it is submitted, not after it is
      // acknowledged: §3.1 item 1 publishes the state (and from there the
      // resolution's batch) before the hub call returns, so the delivery an
      // expectation is armed for can arrive while this call is still in flight
      // (`TASK-284`, SIGNALR_PROTOCOL.md §7).
      this.pendingResolutionAction = { battleId: submittedBattleId, identity: actionId };

      let acknowledgement: RuntimeActionAcknowledgement;

      try {
        acknowledgement = await this.signalR.swap(
          submittedBattleId,
          fromCell,
          toCell,
          battleState.sequence,
          actionId
        );
      } catch (error) {
        // A transport or RPC failure is not a definitive answer from the server
        // (SIGNALR_PROTOCOL.md §8 item 3, P0.2 §4.A, §4.D): no acknowledgement was
        // received, so whether the swap committed is **unknown**. The action
        // identity stays in `unresolvedActionIds` so an idempotent retry reuses
        // the identical actionId, the runtime publishes the desynchronization it
        // now has, and it asks for the authoritative snapshot rather than
        // assuming the swap did not happen (P0.3 §5).
        this.markActionOutcomeUnknown(error);
        throw new RuntimeActionOutcomeUnknownError(RUNTIME_ACTION_SWAP, error);
      }

      // Cross-battle guard (P0.2 §4.D item 5, item 12):
      // A delayed action response belonging to an older battle must not settle the
      // current battle's action or overwrite state if the active battle changed or ended.
      if (this.battleState?.battleId !== submittedBattleId) {
        throw new Error(
          `Action response for battle "${submittedBattleId}" arrived after active battle changed to "${this.battleState?.battleId ?? 'none'}".`
        );
      }

      // The action is settled by its acknowledgement, so the next submission of
      // the same pair is a new logical action and takes a new identity. An action
      // that throws instead keeps its identity: a transport failure is not an
      // answer (`SIGNALR_PROTOCOL.md` §8 item 3), and a retry of it must carry the
      // identity the first attempt carried or the server cannot recognise it as
      // the same logical action (`ADR-025` §2).
      this.unresolvedActionIds.delete(logicalActionKey);

      // A definitive rejection is a settled no-op, not a resolution: it produces
      // no state push and no batch (§3.1 item 4), so no expectation may be armed
      // in its name. An accepted action keeps its identity recorded until the
      // batch that settles it arrives (TASK-284).
      if (!acknowledgement.accepted) {
        this.forgetSubmittedAction(submittedBattleId);
      }

      return acknowledgement;
    }

    if (candidate.kind === RUNTIME_ACTION_CARD_CAST) {
      if (typeof candidate.cardId !== 'string' || candidate.cardId.length === 0) {
        throw new Error('CardCast action requires a non-empty cardId.');
      }
      const submittedBattleId = battleState.battleId;
      // The cast has no `ADR-025` action identity, so the opaque per-request
      // correlation id it already carries is what an expectation names
      // (TASK-284, SIGNALR_PROTOCOL.md §2 item 1, §7). It is recorded before the
      // request is submitted, for the same §3.1 item 1 ordering reason a Swap's
      // identity is.
      const clientSequence = this.nextClientSequence('card_cast');
      this.pendingResolutionAction = { battleId: submittedBattleId, identity: clientSequence };

      let acknowledgement: RuntimeActionAcknowledgement;

      try {
        acknowledgement = await this.signalR.cardCast(
          submittedBattleId,
          candidate.cardId,
          clientSequence
        );
      } catch (error) {
        // As for a Swap: an unanswered request is not a refusal, so nothing is
        // assumed about whether the cast committed (P0.3 §5).
        this.markActionOutcomeUnknown(error);
        throw new RuntimeActionOutcomeUnknownError(RUNTIME_ACTION_CARD_CAST, error);
      }

      if (this.battleState?.battleId !== submittedBattleId) {
        throw new Error(
          `Action response for battle "${submittedBattleId}" arrived after active battle changed to "${this.battleState?.battleId ?? 'none'}".`
        );
      }

      if (!acknowledgement.accepted) {
        this.forgetSubmittedAction(submittedBattleId);
      }

      return acknowledgement;
    }

    // candidate.kind === RUNTIME_ACTION_PET_SKILL_CAST
    const submittedBattleId = battleState.battleId;
    const clientSequence = this.nextClientSequence('pet_skill_cast');
    this.pendingResolutionAction = { battleId: submittedBattleId, identity: clientSequence };

    let acknowledgement: RuntimeActionAcknowledgement;

    try {
      acknowledgement = await this.signalR.petSkillCast(submittedBattleId, clientSequence);
    } catch (error) {
      // Same contract as the two actions above (P0.3 §5).
      this.markActionOutcomeUnknown(error);
      throw new RuntimeActionOutcomeUnknownError(RUNTIME_ACTION_PET_SKILL_CAST, error);
    }

    if (this.battleState?.battleId !== submittedBattleId) {
      throw new Error(
        `Action response for battle "${submittedBattleId}" arrived after active battle changed to "${this.battleState?.battleId ?? 'none'}".`
      );
    }

    if (!acknowledgement.accepted) {
      this.forgetSubmittedAction(submittedBattleId);
    }

    return acknowledgement;
  }

  /**
   * Records the one thing a client can know when a submitted action is never
   * acknowledged: **it is no longer synchronized with the server**, and the
   * authoritative snapshot is the only way to find out what happened.
   *
   * ```text
   * action submitted, no acknowledgement received
   *         ↓
   * sync = 'unsynchronized'      the runtime holds a copy it can no longer vouch
   *                              for — a resolution the server may already have
   *                              committed is missing from it
   *         ↓
   * recoverBattleState() §7.1    the authoritative snapshot, ingested by the
   *                              existing §4 path and published to the
   *                              presentation layer with source 'recovery'
   * ```
   *
   * It invents no state and settles nothing: the action's identity is left
   * unresolved (a retry must carry it — `ADR-025` §2), no withdrawal of the
   * request is attempted (no such message exists, `SIGNALR_PROTOCOL.md` §8 item 3),
   * and a failed recovery leaves the runtime honestly `unsynchronized` rather
   * than guessing an outcome. Nothing here is gameplay state.
   */
  private markActionOutcomeUnknown(error: unknown): void {
    this.updateState({ sync: 'unsynchronized', lastError: this.describeError(error) });

    // Best-effort and self-contained: `recoverBattleState` reports its own
    // transport failure through the existing technical channel and returns, so
    // this never throws into the action's rejection path.
    void this.recoverBattleState();
  }

  /**
   * Generates the opaque per-request correlation id the **non-Swap** actions
   * carry (`SIGNALR_PROTOCOL.md` §2 item 1).
   *
   * It identifies a request, not a position in the battle's history: it is not
   * `BattleState.Sequence` and is not derived from the synchronized `sequence`.
   * `CardCast` and `PetSkillCast` have no precondition and no server-side
   * deduplication (`ADR-025` is the Swap action's contract), so a monotonic
   * counter is sufficient there, and no randomness is involved. The Swap action
   * does **not** use this value: it carries the state precondition and the action
   * identity `ADR-025` defines, and its identity must stay unique across runtime
   * resets — see {@link createActionId}.
   */
  private nextClientSequence(prefix = 'action'): string {
    clientSequenceCounter += 1;
    return `${prefix}_${clientSequenceCounter}`;
  }

  /**
   * The key one logical action is identified by, for as long as it is unresolved:
   * the battle it addresses, its kind, and the action's own identity payload (the
   * unordered cell pair for a Swap).
   */
  private logicalActionKey(
    battleId: string,
    kind: string,
    first: number,
    second: number
  ): string {
    return `${battleId}|${kind}|${first}|${second}`;
  }

  /**
   * The action identity to submit with a logical action (`ADR-025` §2).
   *
   * A logical action the runtime has already submitted and not yet heard back
   * about keeps the identity it was submitted with, so a retry of it is the same
   * action to the server and cannot execute twice; a different logical action
   * always takes a fresh, unique identity. The entry is dropped when the action's
   * acknowledgement arrives (`requestAction`) and when the runtime stops holding
   * the battle (`clearActiveBattleState`), so identities are retained only for
   * genuinely unresolved actions.
   */
  private actionIdFor(logicalActionKey: string, prefix: string): string {
    const existing = this.unresolvedActionIds.get(logicalActionKey);

    if (existing !== undefined) {
      return existing;
    }

    const actionId = createActionId(prefix);
    this.unresolvedActionIds.set(logicalActionKey, actionId);

    return actionId;
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
   * UNAUTHENTICATED`, `400` loadout error — API_CONTRACTS.md §2.3, §3), a connect
   * failure, or a join failure propagates to the caller with `sync`,
   * `battleState`, and `connection` unchanged. No battle state is invented, no
   * `battleId` is manufactured, and no partial battle is initialized
   * (`SIGNALR_PROTOCOL.md` §8 item 3). The runtime adds no duplicate-start guard: no
   * document defines the behavior of overlapping calls, so none is invented here.
   *
   * **The transition establishes the new battle's identity, and only after the
   * REST step has answered with one.** The server-owned `battleId` the start
   * response carries is recorded as the identity this transition has established,
   * which is what lets the join's §4 push for that battle replace a synchronized
   * copy of a *different* battle (`receiveBattleState`). It is dropped if either
   * transport step then fails: a start that does not proceed establishes nothing.
   *
   * **The held copy is never reset here.** A battle the runtime already holds is
   * left exactly as it is — it is replaced only when the new battle's own §4 push
   * arrives — so a failed start leaves a still-valid active battle synchronized
   * instead of silently erasing it. Nothing is cleared in advance of the
   * transition succeeding.
   *
   * @throws Error when the runtime is disposed, or when any of the three
   * documented steps fails.
   */
  public async startBattle(request: BattleStartRequest): Promise<void> {
    if (this.disposed) {
      throw new Error('GameRuntime has been disposed and cannot start a battle.');
    }

    const response = await this.throughAuthenticatedTransport(() =>
      this.api.startBattle(request)
    );

    // The documented lifecycle transition (`SIGNALR_PROTOCOL.md` §1 items 1–2):
    // the server created this battle and named it, so this identity is what the
    // §4 push that follows the join may replace the currently synchronized
    // battle with.
    this.establishedBattleId = response.battleId;

    try {
      await this.signalR.connect(response.signalrHub);

      // The subscriptions the join's push arrives on. Registered before the join
      // so the push is never silently unobserved, and registered exactly once
      // (case A/B/C, task §9).
      this.registerTransportSubscriptions();

      await this.signalR.joinBattle(response.battleId);
    } catch (error) {
      // The transition did not proceed, so it established no identity: the
      // runtime is left exactly as it was, still holding whatever battle it held.
      this.establishedBattleId = null;
      throw error;
    }
  }

  // ---------------------------------------------------------------------------
  // Reconnect / resync recovery (SIGNALR_PROTOCOL.md §7, ADR-008)
  // ---------------------------------------------------------------------------

  /**
   * The reconnect flow's transport step and its state step, in the documented
   * order (`SIGNALR_PROTOCOL.md` §7 items 4 and 1, ADR-008):
   *
   * ```text
   * SignalR reconnect
   *         ↓
   * SignalRService.joinBattle(battleId)    §7 item 4, §1.2, §2 `JoinBattle` —
   *         │                              the new connection re-joins the group
   *         ↓
   * recoverBattleState()                   §7.1–§7.3 — the snapshot request
   *         ↓
   * receiveBattleState()                   the existing §4 ingestion path
   * ```
   *
   * **Group membership is connection-scoped** (§7 item 4): the connection a
   * reconnect produces is a member of no group, so the §4 push and the §3
   * batches that follow a resolution would not reach it. The re-join is the
   * documented way back in, and it is the same authoritative `JoinBattle`
   * operation `startBattle` already performs — no new hub method, event, wire
   * member, or state model is introduced, and the server restores nothing on its
   * own (`BattleHub` re-adds no group on connect).
   *
   * **The re-join is awaited before recovery proceeds.** `joinBattle` resolves
   * only once its hub invocation has completed, so §7 item 1's snapshot request
   * is never issued on a connection that has not yet been added to the group.
   * Normal post-reconnect event delivery is only expected after the two steps
   * below have run, in this order.
   *
   * **`battleId` is the battle the runtime already holds.** It is the
   * server-pushed identity (`§4.9`) — the same source `requestAction` and
   * `recoverBattleState` resolve their battle from, and never a caller-supplied,
   * URL-, storage-, or scene-sourced one. With no current battle there is no
   * group to re-join, so no `JoinBattle` is issued and no battle is invented.
   *
   * **A failed re-join is a technical failure, not state.** It is reported on
   * the existing `runtime_error` channel and recovery still runs: §7's snapshot
   * is a direct request/response and does not depend on group membership, so a
   * failed re-join must not additionally suppress the authoritative resync, and
   * no battle state is fabricated to cover either failure.
   */
  private async rejoinAndRecoverAfterReconnect(): Promise<void> {
    const battleId = this.battleState?.battleId;

    if (battleId !== undefined) {
      try {
        await this.signalR.joinBattle(battleId);
      } catch (error) {
        const detail = this.describeError(error);
        this.updateState({ lastError: detail });
        this.emit({ type: 'runtime_error', state: this.state, detail });
      }
    }

    await this.recoverBattleState();
  }

  /**
   * Requests the authoritative snapshot after a reconnect **or** when the client
   * has detected that it is desynchronized — a withheld batch ahead of the
   * synchronized state (`forwardBattleEvents`), an action whose acknowledgement
   * never arrived (`markActionOutcomeUnknown`), or an accepted action whose
   * delivered state push was never followed by its batch
   * (`expireResolutionExpectation`, TASK-284) — and synchronizes the runtime from
   * it (`SIGNALR_PROTOCOL.md` §6 item 3, §7.1–§7.3, `ADR-008`).
   *
   * ```text
   * SignalR reconnect, a withheld batch, an unanswered action, or an expired
   * resolution-batch expectation
   *         ↓
   * SignalRService.getBattleState(battleId)          §7.1 — the request/response
   *         ↓                                        snapshot; no broadcast
   * BattleHub.GetBattleState
   *         ↓
   * authoritative snapshot
   *         ↓
   * ingestBattleState()          ← the existing §4 ingestion path
   * ```
   *
   * **The same snapshot answers every desynchronization.** `ADR-008` makes the
   * snapshot the only resynchronization path, so a detected gap uses the request
   * the reconnect already uses rather than adding a mechanism: no second
   * ingestion path, no second state store, and no recovery-specific state model
   * exists.
   *
   * **Only a known battle is recovered.** The identity is the one the runtime
   * already holds from the server's own state (`SIGNALR_PROTOCOL.md` §4.9) — the
   * same source `requestAction` resolves an action's battle from, and the same
   * source `BattleScene` reads through `getBattleState()`. There is no second
   * battle-id store, no URL/localStorage/React/scene-sourced id, and no
   * caller-supplied one. With no battle known there is nothing to recover and no
   * state is invented to give the request a target.
   *
   * **The response's two sequence spellings must agree.** An accepted response
   * carries the version as its own `serverSequence` and again as
   * `state.sequence`, both projected from the same committed
   * `BattleState.Sequence` (`SIGNALR_PROTOCOL.md` §7 item 5). A response whose two
   * spellings disagree is an invalid recovery response: nothing is stored, no
   * synchronization is claimed, and the contradiction is reported — neither value
   * is silently preferred, because choosing would be the client authoring the
   * version it is supposed to read (`GAME_RULES.md` §18, `ADR-001`). When they
   * agree, the version ingested is the state's own `sequence`, the field the
   * ingestion guard compares; the envelope copy is an integrity cross-check.
   *
   * **Local prediction is discarded, not merged.** `SIGNALR_PROTOCOL.md` §7.2
   * makes the snapshot authoritative: nothing the client previously held is merged
   * into it, and no local prediction survives it. Missed events are not replayed
   * and none is requested — a desynchronized client resynchronizes from a snapshot
   * only (`SIGNALR_PROTOCOL.md` §8 item 8, `ARCHITECTURE.md` §5 item 2).
   *
   * **A snapshot of the active battle is ingested by the shared guard, and the
   * guard's one refusal is versioning rather than reconciliation.** Because this
   * routes through `ingestBattleState`, a snapshot that is *behind* the copy the
   * same battle has already delivered is refused there, so recovery can never
   * roll the synchronized state backward; a snapshot that is current or ahead —
   * the ordinary recovery answer — is ingested verbatim and remains authoritative,
   * and a snapshot that is level with the copy is the idempotent re-application
   * described on that method. No sequence comparison is made between two
   * different battles, and a rejected payload leaves the held copy untouched
   * rather than being replaced, merged, or completed.
   *
   * **No sequence logic is invented from `serverSequence`.** The response's
   * `serverSequence` is the server's `BattleState.Sequence` and is documented
   * state metadata, not a client correlation id (`GAME_STATE.md` §5.2 item 3):
   * the runtime does not seed `clientSequence` from it and does not increment it.
   * The version the guard reads is the one the snapshot's own state carries,
   * exactly as sent; the value the runtime stores is that of the payload it
   * accepted.
   *
   * **One request in flight per battle.** A caller that arrives while a request
   * for the same battle is already in flight joins that attempt: it issues
   * nothing and returns, because the answer already on its way is the answer to
   * the same question. That is what keeps repeated observations of one unresolved
   * gap — several withheld batches, or a withheld batch plus an unanswered
   * action — from becoming a request storm, and it is why §7.1's snapshot is
   * never requested twice concurrently for one battle.
   *
   * A refused snapshot that is stale (sequence behind the held state), or a
   * transport failure that answered nothing, schedules a bounded retry up to
   * {@link MAX_RECOVERY_ATTEMPTS} attempts, so a transient failure does not leave
   * the client permanently desynchronized and a persistent one does not spin.
   */
  public async recoverBattleState(): Promise<void> {
    // §7.1's precondition: a current battle is known. `battleId` comes from the
    // runtime's own synchronized state and nowhere else.
    const battleId = this.battleState?.battleId;

    if (battleId === undefined) {
      return;
    }

    if (this.recoveryInFlightBattleId === battleId) {
      // The same question is already being asked for this battle. Joining it
      // means issuing nothing: the in-flight response settles both callers.
      return;
    }

    await this.runRecovery(battleId, 1);
  }

  /**
   * One snapshot request attempt, and the bounded retry it may schedule.
   *
   * `battleId` is passed in rather than re-read, so a battle that changes while
   * the request is in flight cannot make this attempt's bookkeeping or its retry
   * apply to the new one: every continuation is checked against the battle it was
   * issued for.
   */
  private async runRecovery(battleId: string, attempt: number): Promise<void> {
    // The battle this attempt is for must still be the synchronized one, and no
    // other attempt may already be in flight for it.
    if (
      this.disposed ||
      this.battleState?.battleId !== battleId ||
      this.recoveryInFlightBattleId === battleId
    ) {
      return;
    }

    this.recoveryInFlightBattleId = battleId;

    let response: BattleStateSnapshotResponse;

    try {
      response = await this.signalR.getBattleState(battleId);
    } catch (error) {
      this.recoveryInFlightBattleId = null;

      // A transport failure is a technical failure, not battle state: nothing
      // is fabricated and the runtime keeps whatever it held. The §4 push
      // remains the only other way state arrives.
      const detail = this.describeError(error);
      this.updateState({ lastError: detail });
      this.emit({ type: 'runtime_error', state: this.state, detail });

      // The recovery is still needed — the client is still desynchronized and
      // still holds this battle — so the bounded retry is what gives the
      // request another chance without turning a persistent outage into a loop.
      await this.retryRecoveryIfStillNeeded(battleId, attempt);
      return;
    }

    this.recoveryInFlightBattleId = null;

    if (response.accepted) {
      // §7.1's envelope and the snapshot it carries are two spellings of one
      // value: the server builds both from the same committed `BattleState`
      // (`BattleHub.GetBattleState` projects `state.Sequence` as the envelope's
      // `serverSequence` and the §4 payload's `sequence` alike). An accepted
      // response whose two spellings disagree is therefore not a snapshot this
      // client may converge from, and neither value is preferred: nothing is
      // stored, synchronization is not claimed, and the contradiction is
      // reported through the existing technical channel.
      const state = this.readBattleState(response.state);

      if (state === null) {
        const detail = 'Received a malformed GetBattleState recovery payload; ignored.';
        this.updateState({ lastError: detail });
        this.emit({ type: 'runtime_error', state: this.state, detail });
        return;
      }

      if (response.serverSequence !== state.sequence) {
        const detail =
          `GetBattleState returned an inconsistent recovery envelope: serverSequence ` +
          `${this.describeDeliveredValue(response.serverSequence)} for a snapshot at sequence ` +
          `${state.sequence}; ignored so no version is silently chosen ` +
          '(SIGNALR_PROTOCOL.md §7.1).';
        this.updateState({ lastError: detail });
        this.emit({ type: 'runtime_error', state: this.state, detail });
        return;
      }

      // The documented §4 shape, validated and stored by the same reader the
      // push uses — no recovery-only validator and no recovery-only model.
      const result = this.ingestBattleState(state, 'recovery');

      if (result.isStale) {
        await this.retryRecoveryIfStillNeeded(battleId, attempt);
      }
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
   * Schedules the next bounded recovery attempt when recovery is still needed.
   *
   * ```text
   * still needed  the battle is still the synchronized one, the client is still
   *               `unsynchronized`, and attempts remain
   * otherwise     nothing is scheduled: a converged, replaced, cleared, disposed
   *               or exhausted runtime has nothing left to retry
   * ```
   *
   * The attempt counter is what bounds the retry: once
   * {@link MAX_RECOVERY_ATTEMPTS} have run, the client stays honestly
   * `unsynchronized` with its last reported failure rather than retrying forever.
   */
  private async retryRecoveryIfStillNeeded(battleId: string, attempt: number): Promise<void> {
    if (attempt >= GameRuntime.MAX_RECOVERY_ATTEMPTS) {
      return;
    }

    await this.scheduleRecoveryRetry(battleId, attempt + 1);
  }

  private async scheduleRecoveryRetry(battleId: string, nextAttempt: number): Promise<void> {
    this.clearRecoveryRetry();

    if (this.disposed || this.battleState?.battleId !== battleId || this.state.sync === 'synchronized') {
      return;
    }

    const delayMs = 25 * Math.pow(2, nextAttempt - 2);

    await new Promise<void>((resolve) => {
      this.recoveryRetryTimer = setTimeout(async () => {
        this.recoveryRetryTimer = null;

        if (this.disposed || this.battleState?.battleId !== battleId || this.state.sync === 'synchronized') {
          resolve();
          return;
        }

        await this.runRecovery(battleId, nextAttempt);
        resolve();
      }, delayMs);
    });
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
    // recovery-specific one. Any identity a transition had established goes with
    // it: the battle it named is the one that could not be recovered. Every piece
    // of recovery bookkeeping goes too — there is no battle left to recover, so no
    // retry timer, in-flight marker or withheld gap may outlive it.
    this.battleState = null;
    this.unresolvedActionIds.clear();
    this.establishedBattleId = null;
    this.clearRecoveryState();
    this.updateState({ sync: 'awaiting_battle', lastError: null });

    // **The loss is published, so the presentation layer can answer it.** The
    // battle the scene was rendering is gone; leaving the scene to discover that
    // from `getBattleState() === null` would leave whatever it was presenting —
    // a board, a timeline, a lock — in place until it happened to look. The
    // publication carries no state (`null`) and the `'ended'` source, because
    // there is none to carry and none is invented: §7.3's answer is that the
    // battle cannot be recovered, not a substitute state.
    //
    // The publication precedes the fallback read below, so a scene answering it
    // drops its stale presentation at the moment the battle is known to be over
    // rather than after a network round trip.
    const endedMetadata: BattleStateMetadata = { source: 'ended' };
    for (const listener of this.battleStateListeners) {
      listener(null, endedMetadata);
    }

    try {
      await this.throughAuthenticatedTransport(() => this.api.getBattleResult(battleId));
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
    return await this.throughAuthenticatedTransport(() => this.api.getPets());
  }

  /**
   * One owned Pet's detail (`API_CONTRACTS.md` §5.2) —
   * `GET /api/pets/{petId}`.
   *
   * `petId` is the owned instance identity and is passed on as supplied; the
   * runtime adds no lookup, index, or cached collection of its own.
   */
  public async getPet(petId: string): Promise<PetResponse> {
    return await this.throughAuthenticatedTransport(() => this.api.getPet(petId));
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
    return await this.throughAuthenticatedTransport(() => this.api.getCards());
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
    return await this.throughAuthenticatedTransport(() => this.api.getRelics());
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
    return await this.throughAuthenticatedTransport(() => this.api.getBattleResult(battleId));
  }

  /**
   * The authenticated Player's completed-battle history
   * (`API_CONTRACTS.md` §4.5) — `GET /api/battle/history`.
   *
   * This is the read-only account-progression source the Battle History surface
   * presents. The runtime delegates to the existing `ApiService` method and
   * nothing else: no orchestration, no second retrieval mechanism, and no query
   * parameter — §4.5 notes 5–6 make the bare route the entire request surface
   * (no pagination, filter, sort, or search), and the path carries no
   * `playerId` because the scope is the session-derived identity (note 8).
   *
   * **The order is the server's.** §4.5 note 4 fixes it — `CompletedAt`
   * descending, tie-broken by `BattleResultId` descending — as a documented
   * contract clients MAY rely on, so the array is handed back exactly as it
   * arrived: it is not sorted, reversed, filtered, re-numbered, or trimmed, and
   * no order is inferred from `battleId` or from `completedAt`.
   *
   * **Nothing is computed and nothing is held.** No XP is summed, no Level is
   * derived, no `leveledUp` flag is recomputed, no `durationTurns` or
   * `completedAt` value is re-derived, and `null` is never promoted to a number
   * (`DATABASE.md` §1, `GAME_RULES.md` §18, AGENTS.md §10). The response is not
   * cached and is not stored as runtime state — it is ephemeral presentation
   * data (`GAME_STATE.md` §4, `ARCHITECTURE.md` §2.2.3 rules 1–2) that the
   * calling scene owns and releases (`ADR-022`'s carrier is a different
   * concept and is untouched here).
   *
   * An empty history arrives as the empty array (§4.5 note 9) and is returned
   * as such; a rejection — including §2.3/§6's `401 UNAUTHENTICATED` — propagates
   * unchanged, with no fabricated or partial history.
   */
  public async getBattleHistory(): Promise<BattleHistoryItemResponse[]> {
    return await this.throughAuthenticatedTransport(() => this.api.getBattleHistory());
  }

  // ---------------------------------------------------------------------------
  // Internals
  // ---------------------------------------------------------------------------

  /**
   * Runs one REST capability of this runtime through the single authenticated
   * transport boundary, applying `API_CONTRACTS.md` §2.3's session-invalidation
   * outcome to a `401` before the rejection reaches the caller.
   *
   * **The classification is the transport's, not a per-call-site decision.**
   * `ApiService.get` / `ApiService.post` serve only the endpoints §2.3
   * "Coverage" places behind the application session, so a `401` on this
   * transport *is* §2.3's `UNAUTHENTICATED` answer — the one public response
   * missing, invalid/tampered, and expired sessions share. Every covered
   * capability therefore reaches the boundary through here rather than
   * classifying `401` for itself, which is what keeps the cleanup single
   * (`ARCHITECTURE.md` §2.2.1 rules 1, 3 and 5).
   *
   * **`INVALID_CREDENTIALS` can never end a session.** §2.2 rule 2's `401`
   * belongs to `POST /api/auth/login`, a separate `ApiService` method pair
   * (`login` / `register`) that this runtime never calls: they are not covered
   * endpoints, and no credential rejection is routed through this boundary.
   *
   * **No other status is a session signal.** Every other rejection — `400
   * INVALID_LOADOUT` / `PET_NOT_OWNED` / `BOSS_NOT_FOUND`, `404`, a transport
   * failure — propagates to the caller exactly as the transport raised it, with
   * the session and the runtime state untouched.
   */
  private async throughAuthenticatedTransport<T>(request: () => Promise<T>): Promise<T> {
    try {
      return await request();
    } catch (error) {
      if (error instanceof ApiRequestError && error.status === 401) {
        // Awaited, so the caller's rejection is observed after the session has
        // actually ended: no caller can react to a `401` while the stale
        // credentials or the authenticated connection are still live.
        await this.invalidateSession();
      }
      throw error;
    }
  }

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
        this.receiveBattleState(payload, 'push');
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
   * Forwards one server-authoritative `ReceiveEvents` batch
   * (SIGNALR_PROTOCOL.md §3) to battle-event subscribers unchanged — after the
   * delivery-integrity check below has admitted it.
   *
   * The payload is never reshaped, re-sorted, filtered member-by-member, or
   * interpreted: an admitted batch is dispatched exactly as delivered, and the
   * runtime neither adds an event nor drops one. A malformed payload is reported
   * as a technical runtime error and otherwise ignored — the runtime never
   * fabricates gameplay data to fill a gap.
   *
   * **The one check, and why it is a delivery-integrity check.** The server
   * commits a resolution's `BattleState` and pushes it *before* it sends that
   * resolution's batch (`SIGNALR_PROTOCOL.md` §3.1, §6 item 4), so the batch's
   * `serverSequence` should be exactly the sequence the synchronized copy
   * already carries. Two delivered versions are therefore compared — never a
   * gameplay value, never a board, never an event payload:
   *
   * ```text
   * no battle held                          → withhold  the batch's battle is not
   *                                                      the synchronized one, and
   *                                                      there is no state it could
   *                                                      be presented against
   * different battle than the held one      → withhold  it belongs to no battle this
   *                                                      runtime synchronizes
   * serverSequence equal to held sequence   → forward   the documented §6 item 4
   *                                                      push-then-batch delivery
   * serverSequence below the held sequence  → forward   the synchronized state
   *                                                      already represents that
   *                                                      resolution; the scene's
   *                                                      own accepted-resolution
   *                                                      memory decides whether it
   *                                                      is re-presented
   * serverSequence above the held sequence  → withhold  a delivery-integrity gap:
   *                                                      the state for a committed
   *                                                      resolution never arrived,
   *                                                      so nothing the client
   *                                                      holds can represent it
   * ```
   *
   * **Only the gap is a desynchronization.** §6 item 3 makes a skipped
   * `serverSequence` state desync, and §7.1's snapshot is the documented
   * reconciliation (`ADR-008`: a snapshot, never an event replay) — so the
   * withheld batch is *not* forwarded, `sync` becomes `unsynchronized`, and the
   * authoritative snapshot is requested once for the gap. A batch **at or below**
   * the held sequence is the ordinary path and the superseded case respectively:
   * the held state already contains that resolution's result
   * (`GAME_STATE.md` §5.1 item 3), so no snapshot is fetched for it — a
   * re-delivery must not turn into an unnecessary recovery, and it is not a
   * desynchronization at all.
   *
   * **It is deliberately not an event filter.** Nothing here reads an event
   * payload, counts events, drops an event from an admitted batch, or decides
   * what an event means; a batch the runtime cannot place against its
   * synchronized state is withheld whole, and a batch it can place is forwarded
   * whole and in order (`ARCHITECTURE.md` §2.2.1 rule 4, `GAME_RULES.md` §18).
   */
  private forwardBattleEvents(payload: unknown): void {
    const envelope = this.readBattleEventsEnvelope(payload);

    if (envelope === null) {
      const detail = 'Received a malformed ReceiveEvents payload; ignored.';
      this.updateState({ lastError: detail });
      this.emit({ type: 'runtime_error', state: this.state, detail });
      return;
    }

    const held = this.battleState;

    if (held === null) {
      // No synchronized battle: there is no state this resolution could be
      // presented against. `SIGNALR_PROTOCOL.md` §6 item 4 orders the state push
      // before any batch for that battle, and §7.1's recovery is addressed by a
      // battle id — with no battle held there is nothing to recover and nothing
      // is invented to give one a target.
      const detail =
        `Received a ReceiveEvents batch for "${envelope.battleId}" while no battle ` +
        'is synchronized; ignored (SIGNALR_PROTOCOL.md §4, §6 item 4).';
      this.updateState({ lastError: detail });
      this.emit({ type: 'runtime_error', state: this.state, detail });
      return;
    }

    if (envelope.battleId !== held.battleId) {
      // Cross-battle contamination: the batch belongs to a battle this runtime
      // does not synchronize, so it must not reach a listener that is presenting
      // `held`. No recovery is requested — a foreign batch says nothing about the
      // active battle.
      const detail =
        `Received a ReceiveEvents batch for "${envelope.battleId}" while ` +
        `"${held.battleId}" is synchronized; ignored (SIGNALR_PROTOCOL.md §3, §4).`;
      this.updateState({ lastError: detail });
      this.emit({ type: 'runtime_error', state: this.state, detail });
      return;
    }

    if (envelope.serverSequence > held.sequence) {
      this.withholdBatchAheadOfState(envelope, held.sequence);
      return;
    }

    // Equal to, or below, the synchronized sequence: exactly what §6 item 4
    // delivers, or a re-delivery the synchronized state already represents. Both
    // are forwarded unchanged and neither changes synchronization. An admitted
    // batch is also what settles an armed expectation for this battle
    // (`TASK-284`, SIGNALR_PROTOCOL.md §7).
    this.settleResolutionExpectation(envelope, held.sequence);

    for (const listener of this.battleListeners) {
      listener(envelope);
    }
  }

  /**
   * Withholds one `ReceiveEvents` batch that is ahead of the synchronized state
   * and asks for the authoritative snapshot (`SIGNALR_PROTOCOL.md` §6 item 3,
   * §7.1, `ADR-008`).
   *
   * This is the one client-observable meaning of "sequence desync": a
   * `serverSequence` that steps past the sequence the synchronized copy carries
   * means the state written back for that resolution was never delivered, so the
   * client holds no board its events could be presented against. The resolution
   * is therefore not animated — there is nothing to animate it over — and the
   * §7.1 snapshot is requested instead of the missing events, because §8 item 8
   * and `ADR-008` make a snapshot the only resynchronization path and no event is
   * ever replayed.
   *
   * The request is deduplicated: the newest withheld gap is remembered, and a
   * second observation while a request for it (or a retry of it) is still in
   * flight joins that attempt rather than opening another. The bounded retry of
   * `recoverBattleState` is what keeps a persistently unrecoverable gap from
   * spinning.
   */
  private withholdBatchAheadOfState(
    envelope: BattleEventsEnvelope,
    heldSequence: number
  ): void {
    // The gap observed before this one, so a repeat of the *same* unresolved gap
    // joins the attempt already outstanding for it instead of asking again
    // (`recoverBattleState` issues at most one request per battle in flight).
    const previousGap = this.withheldGapSequence;
    this.withheldGapSequence = envelope.serverSequence;

    const detail =
      `Withheld a ReceiveEvents batch for "${envelope.battleId}" at serverSequence ` +
      `${envelope.serverSequence}, ahead of the synchronized sequence ${heldSequence}: ` +
      'the state for that resolution was not delivered, so the authoritative ' +
      'snapshot is requested instead of replaying its events ' +
      '(SIGNALR_PROTOCOL.md §6 item 3, §7.1).';

    this.updateState({ sync: 'unsynchronized', lastError: detail });
    this.emit({ type: 'runtime_error', state: this.state, detail });

    if (previousGap === envelope.serverSequence) {
      return;
    }

    // Best-effort and self-contained: `recoverBattleState` reports its own
    // failures through the existing technical channel, so this never throws into
    // the transport callback.
    void this.recoverBattleState();
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

    const intermediatePetHp =
      typeof candidate.intermediatePetHp === 'number' && Number.isFinite(candidate.intermediatePetHp)
        ? candidate.intermediatePetHp
        : null;

    // The optional `combatTrace` is parsed here because this method rebuilds the
    // envelope: an unlisted member would otherwise be dropped and never reach the
    // Developer Move Log (`SIGNALR_PROTOCOL.md` §3 item 5, §3.3). A trace that is
    // malformed or whose own `battleId`/`serverSequence` do not repeat the
    // envelope's is treated as absent for this delivery — only the trace is
    // dropped, never the batch itself (§3.3.2 item 2).
    const combatTrace =
      candidate.combatTrace === undefined || candidate.combatTrace === null
        ? null
        : readCombatTrace(candidate.combatTrace, candidate.battleId, candidate.serverSequence);

    return {
      battleId: candidate.battleId,
      serverSequence: candidate.serverSequence,
      events: candidate.events,
      ...(intermediatePetHp !== null ? { intermediatePetHp } : {}),
      ...(combatTrace !== null ? { combatTrace } : {}),
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
   * (§4.3 item 14) and stores the active Pet's three live combat values verbatim
   * (§4.3 item 15) — and `bossState`'s identity and two HP values are rendered,
   * never damaged, clamped, inferred, or re-derived (§4.4 items 5, 7 and 10).
   *
   * A malformed payload is reported as a technical runtime error and ignored —
   * the runtime never fabricates battle state to fill a gap.
   *
   * **A well-formed payload is then guarded before it is stored.** This is the
   * one ingestion path, shared by the §4 push and the §7.1 recovery snapshot, so
   * the guard is what keeps a stale or cross-battle payload from overwriting the
   * active battle (`GAME_STATE.md` §5 — `Sequence` is the version the client
   * detects missed or out-of-order delivery with, and §5.1 item 3 — a state at
   * `Sequence = n` is the complete result of the n-th resolution). It compares
   * versions only inside one battle; nothing is assumed between two different
   * battles (`GAME_STATE.md` §5.2 item 3, `SIGNALR_PROTOCOL.md` §2 item 1 — this is not the
   * client's `clientSequence` and is not the action token's `expectedSequence`):
   *
   * ```text
   * no battle held                 → store   the §4 delivery that establishes it
   * same battle, sequence lower    → refuse  it is behind what was delivered
   * same battle, sequence level    → store   the idempotent duplicate below
   * same battle, sequence higher   → store   the ordinary next delivery
   * other battle, established      → store   the lifecycle transition's push
   * other battle, not established  → refuse  no transition named it
   * ```
   *
   * **A refusal stores nothing and is not a second error state.** The held copy
   * is left exactly as it is — not merged, not completed, not partially
   * overwritten — no battle-state listener is dispatched, and the refusal is
   * reported through the existing technical channel (`lastError` plus the
   * `runtime_error` event) that a malformed payload already uses. Nothing is
   * derived from the refused payload, and the runtime still holds the server's
   * own most recent state for the battle.
   *
   * **A duplicate sequence is idempotent and is not reported.** Two payloads of
   * the same battle at the same `Sequence` describe the same complete resolution
   * (`§5.1` item 3), and this projection carries battle state only: the record's
   * exact member set (`§4` item 4) holds no non-gameplay metadata — no `Status`
   * or lifecycle value exists anywhere in the protocol (`§8 item 3`) — so there is
   * nothing a duplicate could update that a suppressed delivery would preserve.
   * Storing it is therefore a plain re-application of the same value rather than
   * a state change, and it stays an accepted delivery: recovery must still reach
   * `'synchronized'` and re-render from its snapshot even when the snapshot is
   * level with the copy already held (§7.2). Suppressing the ingestion would
   * leave such a client permanently `'unsynchronized'`, which is why a duplicate
   * is not treated as an anomaly and produces no `lastError` or `runtime_error`.
   */
  private receiveBattleState(
    payload: unknown,
    source: BattleStateSource = 'push'
  ): { accepted: boolean; isStale: boolean } {
    const state = this.readBattleState(payload);

    if (state === null) {
      const detail =
        source === 'recovery'
          ? 'Received a malformed GetBattleState recovery payload; ignored.'
          : 'Received a malformed BattleStateUpdated payload; ignored.';
      this.updateState({ lastError: detail });
      this.emit({ type: 'runtime_error', state: this.state, detail });
      return { accepted: false, isStale: false };
    }

    return this.ingestBattleState(state, source);
  }

  /**
   * Stores one already-read authoritative state and publishes it to the scenes —
   * the shared half of the §4 push and the §7.1 recovery snapshot, so both
   * deliveries produce exactly the same observables and no recovery-only
   * ingestion path or recovery-only model exists.
   *
   * It is the single decision point of the ingestion guard
   * (`refuseStateDelivery`), so an accepted push and an accepted snapshot are
   * stored, versioned, and dispatched identically.
   */
  private ingestBattleState(
    state: RuntimeBattleState,
    source: BattleStateSource
  ): { accepted: boolean; isStale: boolean } {
    const refusal = this.refuseStateDelivery(state);

    if (refusal !== null) {
      this.updateState({ lastError: refusal });
      this.emit({ type: 'runtime_error', state: this.state, detail: refusal });
      const isStale =
        this.battleState !== null &&
        state.battleId === this.battleState.battleId &&
        state.sequence < this.battleState.sequence;
      return { accepted: false, isStale };
    }

    // The copy this delivery replaces decides one thing only: whether the
    // delivered sequence is an advance of the same battle (`TASK-284`).
    const previousBattle = this.battleState;

    this.battleState = state;

    // The transition's identity is consumed by the battle it named: once that
    // battle's own state is stored, the copy it replaced is no longer current and
    // no later payload may be admitted on that transition's authority.
    if (state.battleId === this.establishedBattleId) {
      this.establishedBattleId = null;
      this.unresolvedActionIds.clear();
    }

    // The client is synchronized again: the state it now holds represents the
    // resolution any withheld gap was about, so the scheduled retry ends and the
    // outstanding-gap bookkeeping is released with it (`SIGNALR_PROTOCOL.md`
    // §7.1).
    this.clearRecoveryRetry();
    this.withheldGapSequence = null;

    // And the missing-batch expectation is reconciled against what was actually
    // delivered: a §4 push that advances the held sequence arms it, a recovery
    // snapshot or a different battle supersedes it (`TASK-284`,
    // SIGNALR_PROTOCOL.md §7).
    this.reconcileResolutionExpectation(state, source, previousBattle);

    this.updateState({ sync: 'synchronized', lastError: null });
    this.emit({ type: 'battle_state_changed', state: this.state });

    const metadata: BattleStateMetadata = { source };
    for (const listener of this.battleStateListeners) {
      listener(state, metadata);
    }

    return { accepted: true, isStale: false };
  }

  /**
   * The monotonic and cross-battle guard on state ingestion — see
   * `receiveBattleState` for the decision table and its documentation basis.
   *
   * Returns the technical detail to report when the delivered state must not
   * replace the synchronized copy, or `null` when it may be stored. It reads only
   * the two versions it compares (`battleId`, `sequence`) and the identity a
   * lifecycle transition established; it interprets no gameplay value, adjusts
   * nothing, and never invents state to fill a gap.
   */
  private refuseStateDelivery(state: RuntimeBattleState): string | null {
    const held = this.battleState;

    // The documented §4 delivery: joining the battle's group is what triggers it,
    // and with no battle held there is no copy for it to overwrite — it is the
    // delivery that establishes the active battle.
    if (held === null) {
      return null;
    }

    if (state.battleId === held.battleId) {
      if (state.sequence < held.sequence) {
        return (
          `Received battle state for "${state.battleId}" at sequence ` +
          `${state.sequence}, behind the synchronized sequence ${held.sequence}; ` +
          'ignored so synchronized state cannot roll backward (GAME_STATE.md §5).'
        );
      }

      // Level with, or ahead of, what was delivered: a duplicate of one complete
      // resolution or the battle's next one. Both are stored (see above).
      return null;
    }

    if (state.battleId === this.establishedBattleId) {
      // A lifecycle transition (`startBattle`) established this identity from the
      // server's own start response, so this is the push for the battle that
      // transition created — the one case in which state for another battle
      // legitimately replaces the held copy.
      return null;
    }

    return (
      `Received battle state for "${state.battleId}" while "${held.battleId}" is ` +
      'synchronized and no lifecycle transition established it; ignored ' +
      '(SIGNALR_PROTOCOL.md §4).'
    );
  }

  /**
   * Validates only the documented §4 shape — the `GAME_STATE.md` §2.2/§2.3/§2.4/
   * §2.0.5 fields `battleId`, `turn`, `sequence`, `rngSeed`, `rngState`, `board`,
   * `playerState`, `petState`, `bossState`.
   *
   * The record carries no other field and no `Status`/lifecycle value
   * (SIGNALR_PROTOCOL.md §4 item 4 — the payload carries the implemented stage's
   * own fields and no others, with the resulting member set part of the contract
   * per §8 item 1 — and §8 item 3), so nothing else is read or defaulted.
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
   * `hp`, `maxHp` and `power` are the last three required members (§4.3 item 15,
   * the `TASK-208` <b>D-208-01</b> and <b>D-208-02</b> decisions): all three are
   * defined from battle
   * creation, all three are non-nullable, and an initial `power = 0` or a terminal
   * `hp = 0` is delivered as `0` — so a payload missing one is malformed rather
   * than implicitly zero, and the runtime must not read an absent member as `0`.
   * They are stored as sent, one-to-one: the runtime does not damage or heal the
   * Pet, clamp `hp` to `maxHp`, derive one of the three from another, reconstruct
   * HP from the damage events, or reconstruct Power from `PowerChanged`.
   *
   * Every value is read as sent. The runtime does not charge a Passive, evaluate
   * a Threshold, reset progress, or apply an overflow (§4.3 item 9); it does not
   * apply, refresh, decrement, expire, or remove a Status Effect and does not
   * re-derive the collection from the board, the counters, or an event
   * (§4.3 item 14); and it does not widen the object to the rest of §2.3 —
   * identity, progression, the remaining combat stats, the sibling modifier
   * collections, and the Relic loadout snapshot are not delivered (§4.3 item 2),
   * while `equippedCards` is delivered per §4.3 item 13.
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

    // §4.3 item 15: the three live combat values are always present and
    // non-nullable, so a missing or mistyped member is a malformed payload rather
    // than an implicit zero.
    if (
      typeof candidate.hp !== 'number' ||
      typeof candidate.maxHp !== 'number' ||
      typeof candidate.power !== 'number'
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
          statusEffects,
          hp: candidate.hp,
          maxHp: candidate.maxHp,
          power: candidate.power,
        }
      : {
          passiveId: candidate.passiveId,
          passiveProgress,
          equippedCards: [...candidate.equippedCards],
          statusEffects,
          hp: candidate.hp,
          maxHp: candidate.maxHp,
          power: candidate.power,
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
   * Reads the Boss projection (`GAME_STATE.md` §2.4,
   * `SIGNALR_PROTOCOL.md` §4.4).
   *
   * All three members are required and none is nullable: the Boss exists from
   * battle creation at full health with its identity fixed, so there is no absent
   * or "Boss not yet available" case for any member and a payload missing one is
   * malformed rather than implicitly zero (§4.4 item 4). In particular the runtime
   * must not read an absent `hp` as `0` and must not supply an identity it was not
   * sent (§4.4 item 10).
   *
   * The three are read independently: neither HP value is derived from the other,
   * `hp` is never clamped to `maxHp`, no unit, scale, or rounding is applied
   * (§4.4 item 5), and the identity is carried across verbatim — the runtime does
   * not resolve a display name, does not consult a catalog, and does not
   * reconstruct it from an event's `sourceId` or from the battle the client asked
   * for. The object is a projection, not `BossState` — the runtime models no other
   * Boss member, and it does not re-derive these values from the events or from
   * `finalBossHp` (§4.4 items 3, 7 and 10, `GAME_RULES.md` §18).
   */
  private readBossState(value: unknown): RuntimeBossState | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as Partial<RuntimeBossState>;

    if (typeof candidate.bossId !== 'string' || candidate.bossId.length === 0) {
      return null;
    }

    if (
      typeof candidate.hp !== 'number' ||
      typeof candidate.maxHp !== 'number' ||
      typeof candidate.power !== 'number'
    ) {
      return null;
    }

    return {
      bossId: candidate.bossId,
      hp: candidate.hp,
      maxHp: candidate.maxHp,
      power: candidate.power,
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
   * Reads the board's cells (`GAME_STATE.md` §2.1.1, `SIGNALR_PROTOCOL.md`
   * §4.1 item 5).
   *
   * The runtime checks only that 64 well-formed cell entries arrived — the shape
   * `SIGNALR_PROTOCOL.md` §4.1 item 3 guarantees ("the client does not receive a
   * partial board"). It deliberately does not inspect the Gem *values* or
   * evaluate any board rule: the board is server-authored and this is not a
   * second generator or validator. Reading `gemType` is a shape check on a
   * delivered member, not a board rule.
   *
   * A cell is an object because that is what the protocol delivers: §4.1 item 5
   * states the board carries pure `GemType` cell entries. Nothing else is read
   * from a cell, because nothing else is stored per cell (`GAME_STATE.md` §2.1.3
   * item 4, §2.1.2 items 1–2).
   */
  private readBoard(value: unknown): RuntimeBoard | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as { cells?: unknown };

    if (!Array.isArray(candidate.cells) || candidate.cells.length !== BOARD_CELL_COUNT) {
      return null;
    }

    const cells: RuntimeCell[] = [];

    for (const entry of candidate.cells) {
      const cell = this.readCell(entry);
      if (cell === null) {
        return null;
      }
      cells.push(cell);
    }

    return { cells };
  }

  /**
   * Reads one delivered board cell (`SIGNALR_PROTOCOL.md` §4.1 item 5).
   *
   * `gemType` is required and must be a non-empty string — it is the cell's whole
   * delivered content and is always present (`GAME_STATE.md` §2.1.3 items 1–4).
   * No other member is read, and none is repaired: a cell that carries no Gem
   * type is malformed rather than defaulted.
   */
  private readCell(value: unknown): RuntimeCell | null {
    if (typeof value !== 'object' || value === null) {
      return null;
    }

    const candidate = value as { gemType?: unknown };

    if (typeof candidate.gemType !== 'string' || candidate.gemType.length === 0) {
      return null;
    }

    return { gemType: candidate.gemType };
  }

  private describeError(error: unknown): string | null {
    if (error === undefined || error === null) {
      return null;
    }
    return error instanceof Error ? error.message : String(error);
  }

  /**
   * Renders one delivered protocol value for a technical diagnostic message.
   *
   * It is presentation of a received value, never a substitution for one: an
   * absent optional member is reported as `absent`, so a diagnostic can state
   * that the envelope carried no version at all instead of printing an empty
   * string that would read like a version of `0`.
   */
  private describeDeliveredValue(value: number | null | undefined): string {
    return typeof value === 'number' ? String(value) : 'absent';
  }
}