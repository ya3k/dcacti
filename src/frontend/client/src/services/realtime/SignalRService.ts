import * as signalR from '@microsoft/signalr';
import { ApplicationSession } from '../api/ApplicationSession';

export interface PingResult {
  accepted: boolean;
  clientSequence?: string;
  serverTime: string;
}

/**
 * The direct invocation result of a `Swap` request
 * (`SIGNALR_PROTOCOL.md` §5).
 *
 * It is transport-level feedback about the **request** — not authoritative state
 * and not a Battle Event. It is delivered to the caller only, is not broadcast,
 * is not sequenced, and changes nothing by itself (§5 items 1 and 4). The
 * service returns it verbatim: it interprets nothing, defaults nothing, and
 * mutates no state.
 *
 * `accepted: true` means the action was resolved and its events arrive on the §3
 * batch (and its state on the §4 push). `accepted: false` means the action was
 * rejected under its owning domain rule and **nothing happened**
 * (`MATCH3_RULES.md` §2.1.5). The client must not mutate the board on a
 * rejection and must not blindly retry one (§5 item 2).
 */
export interface SwapResult {
  readonly accepted: boolean;
  /**
   * The machine-readable rejection code, or absent when accepted
   * (`SIGNALR_PROTOCOL.md` §5 item 3). The codes are owned per action by its
   * domain document — for `Swap`, the failing checks of `MATCH3_RULES.md`
   * §2.1.2 together with the staleness rejection of §2.1.4. This contract keeps
   * no parallel list, so the client treats the value as opaque presentation
   * data and derives no gameplay meaning from it.
   */
  readonly reason?: string;
}

/**
 * The direct invocation result of a `CardCast` request
 * (`SIGNALR_PROTOCOL.md` §2, §5).
 */
export interface CardCastAcknowledgement {
  readonly accepted: boolean;
  readonly reason?: string | null;
}
export type CardCastResult = CardCastAcknowledgement;

/**
 * The direct invocation result of a `PetSkillCast` request
 * (`SIGNALR_PROTOCOL.md` §2, §5).
 */
export interface PetSkillCastAcknowledgement {
  readonly accepted: boolean;
  readonly reason?: string | null;
}
export type PetSkillCastResult = PetSkillCastAcknowledgement;

/**
 * The authoritative battle state pushed on group join
 * (SIGNALR_PROTOCOL.md §4, §4.2, §4.3).
 *
 * Exactly the currently implemented `GAME_STATE.md` §0 stage's fields —
 * `battleId`, `turn`, `sequence`, `board`, `rngSeed`, `rngState`,
 * `playerState`, `petState` — and no others (§4 item 4: the record carries the
 * implemented stage's own fields and nothing else; §8.1 leaves only the
 * serializer's mechanics to the implementation, never the resulting member set).
 * No gameplay field beyond the stage's own is carried, and no `Status`/lifecycle
 * value exists anywhere in the protocol (§8.3). The values are server-authored
 * (`GAME_RULES.md` §18, ADR-001).
 *
 * `board` carries the server-generated `Cells[64]` (`GAME_STATE.md` §2.1.1,
 * `MATCH3_RULES.md` §1.2). The client renders the received board and must not
 * generate, fill, repair, validate, or re-derive it (§4 item 10).
 *
 * `playerState` carries the resolution's `MatchCount` and `Combo`
 * (`GAME_STATE.md` §2.2). Both are delivered because they are `BattleState`
 * fields, not because the client computes them: the client renders them and
 * never authors, adjusts, or recomputes either (§4.9, `GAME_RULES.md` §18).
 *
 * `petState` carries the active Pet's Passive (`GAME_STATE.md` §2.3, §4.3). It
 * is delivered because it is a `BattleState` field and because
 * `PASSIVE_RULES.md` §6 item 1 requires the Passive's progress to be exposed as
 * a UI-facing value. The client renders the `current / threshold` pair and the
 * Passive identity it was sent; it does not charge a Passive, evaluate a
 * Threshold, or reset progress (§4.3 item 9).
 *
 * This is a transport-level shape only. The service does not interpret it,
 * derive from it, or recompute it; it hands it to the runtime unchanged.
 */
export interface BattleStateUpdatedPayload {
  readonly battleId: string;
  readonly turn: number;
  readonly sequence: number;
  /**
   * The battle's server-chosen PRNG seed (`GAME_STATE.md` §2.6.1). Delivered
   * because it is a `BattleState` field, not because the client uses it — the
   * client never advances, re-seeds, or draws from the RNG (§4.1 item 2).
   */
  readonly rngSeed: number;
  /**
   * The PRNG state after generating the initial board (`GAME_STATE.md` §2.6.2)
   * — a state + increment *pair*, not a single word (§2.6.2 item 1). Opaque to
   * the client for the same reason as `rngSeed`.
   */
  readonly rngState: RngStatePayload;
  /** The authoritative board, exactly 64 cells (`SIGNALR_PROTOCOL.md` §4.1). */
  readonly board: BoardPayload;
  /**
   * The authoritative `playerState` projection (`GAME_STATE.md` §2.2,
   * `SIGNALR_PROTOCOL.md` §4.2). A fixed protocol label for the two
   * `BattleState` root values, not a state path (ADR-011 item 6).
   */
  readonly playerState: PlayerStatePayload;
  /**
   * The authoritative `petState` projection (`GAME_STATE.md` §2.3,
   * `SIGNALR_PROTOCOL.md` §4.3) — the active Pet's Passive trio and nothing
   * else. The rest of §2.3 belongs to later stages and is not delivered.
   */
  readonly petState: PetStatePayload;
}

/**
 * The direct invocation result of a `GetBattleState(battleId)` request — §7's
 * reconnect/resync snapshot (`SIGNALR_PROTOCOL.md` §5, §7.1, §7.3).
 *
 * It is the documented §5 envelope carrying the §7 snapshot, and it is modelled
 * at the transport level only: the service returns it verbatim, interprets
 * nothing, defaults nothing, and mutates no state. The sibling
 * `SwapResult`/`CardCastAcknowledgement`/`PetSkillCastAcknowledgement` types are
 * the same treatment for the §2 gameplay requests.
 *
 * `accepted: true` carries:
 *
 * ```text
 * serverSequence   the snapshot's authoritative BattleState.Sequence
 *                  (GAME_STATE.md §5) — the server's value, never a client
 *                  correlation id (§5.2 item 3), never seeded, compared, or
 *                  incremented by the client
 * state            the authoritative snapshot, projected exactly as the §4
 *                  BattleStateUpdated push projects it (§7.1) — the same
 *                  wire shape, so `BattleStateUpdatedPayload` is reused rather
 *                  than duplicated. `BattleState.PlayerId` is server-only and
 *                  absent from it (GAME_STATE.md §2.8, ADR-014)
 * ```
 *
 * `accepted: false` carries the documented `reason` — `BATTLE_NOT_FOUND` on this
 * path — and no state (§7.3). The reason is opaque presentation data: the
 * client derives no gameplay meaning from it and cannot tell an expired record
 * from a foreign battle, because the server contract makes them one answer.
 */
export interface BattleStateSnapshotResponse {
  readonly accepted: boolean;
  /**
   * The snapshot's authoritative `BattleState.Sequence` (`GAME_STATE.md` §5),
   * or `null`/absent when the request was rejected.
   */
  readonly serverSequence?: number | null;
  /**
   * The authoritative snapshot — the §4 projection — or `null`/absent when the
   * request was rejected.
   */
  readonly state?: BattleStateUpdatedPayload | null;
  /**
   * The machine-readable rejection code (§5 item 3), or `null`/absent when
   * accepted. `BATTLE_NOT_FOUND` is the one value on this path (§7.3).
   */
  readonly reason?: string | null;
}

/**
 * The wire projection of `PetState`'s delivered members (`GAME_STATE.md` §2.3,
 * `SIGNALR_PROTOCOL.md` §4.3).
 *
 * `SIGNALR_PROTOCOL.md` §4.3 item 2 fixes this object to exactly three members —
 * `passiveId`, `passiveProgress`, and the conditional `passiveResetOverride`:
 *
 * ```text
 * petState
 * ├── passiveId                 the active Pet's Passive identity   always present
 * ├── passiveProgress            { threshold, current }             always present
 * └── passiveResetOverride       "Partial" | "NoReset"              present only when
 *                                                                   non-default
 * ```
 *
 * The rest of `GAME_STATE.md` §2.3 — `PetId`/Identity, `Element`,
 * `Tier`/`Star`/`Level`, the combat stats (`HP`/`MaxHP`/`ATK`/`DEF`/`Crit`/
 * `Power`), and the `EquippedRelics[]`/`EquippedCards[]` loadout snapshots —
 * belongs to other stages and is **not** delivered (§4.3 item 2: referring to
 * `petState` as a whole does not widen §4 item 4's rule). Modelling those
 * members here would be a second, undocumented wire shape.
 */
export interface PetStatePayload {
  /**
   * The active Pet's Passive identity (`GAME_STATE.md` §2.3) — the same value
   * `GAME_EVENTS.md` §2's `PassiveCharged`/`PassiveTriggered` report. It is
   * always present and has no absent or null form: a battle always has its one
   * active Pet and therefore its one Passive (§4.3 item 3). It is the identity,
   * not the definition — no Threshold, Trigger Type, Effect, or Reset Behavior
   * is nested beside it.
   */
  readonly passiveId: string;
  /**
   * The Passive's `current / threshold` pair (`GAME_STATE.md` §2.3). Both
   * members are always present: `current = 0` is a real publishable value — it
   * is what a battle begins with — so absence is never used for it and a client
   * must not read an absent member as zero (§4.3 item 4).
   */
  readonly passiveProgress: PassiveProgressPayload;
  /**
   * The Passive's non-default Reset Behavior, as its contract name — `"Partial"`
   * or `"NoReset"` (`PASSIVE_RULES.md` §4 item 2), never a numeric enum ordinal
   * (§3.2.4).
   *
   * It is **omitted, never `null`, when the reset is the default** (§4.3 item 7):
   * the absence *is* the statement "this Passive uses the default reset", and no
   * `null`, `"Default"` string, or empty value stands in for it. A consumer
   * tolerates absence and reads it as `Default` — it must not invent a value for
   * it, and `"Default"` is deliberately not a third permitted value (§4.3
   * item 7).
   */
  readonly passiveResetOverride?: string;
  /**
   * The active Pet's equipped cards (`SIGNALR_PROTOCOL.md` §4.3 item 13,
   * `GAME_STATE.md` §2.3). Exactly the 4-entry loadout (3 Basic Cards +
   * 1 Pet Skill Card). Always present, non-empty, and non-nullable.
   */
  readonly equippedCards: readonly string[];
}

/**
 * The wire projection of `GAME_STATE.md` §2.3's `PassiveProgress`
 * `(Threshold, Current)` pair (`SIGNALR_PROTOCOL.md` §4.3 item 4).
 *
 * Both members are always present, and neither is nullable or omitted. The pair
 * travels as one nested object because the two are read together — a reader
 * renders the documented `current / threshold` pair without supplying either
 * from elsewhere (`PASSIVE_RULES.md` §6 item 1's `7 / 10 Matches`).
 */
export interface PassiveProgressPayload {
  /** The Passive's own Threshold (`PASSIVE_RULES.md` §1). */
  readonly threshold: number;
  /**
   * The progress reached toward it (`GAME_STATE.md` §2.3) — the settled value,
   * never derived or advanced by the client (§4.3 item 9).
   */
  readonly current: number;
}

/**
 * The wire projection of `PlayerState` (`GAME_STATE.md` §2.2).
 *
 * The two members are the two `PlayerState` fields the implemented stage
 * carries — `combo` and `matchCount` — and nothing else: the rest of §2.2
 * belongs to later stages.
 *
 * Both are always present, including at `0`. `combo = 0` is the value the state
 * reads before the battle's first committed Swap; it is a value, not an absence,
 * and is never omitted (`MATCH3_RULES.md` §6.5 item 4).
 */
export interface PlayerStatePayload {
  /**
   * The `Combo` of the most recently committed Swap — the number of Matches
   * that Swap produced (`MATCH3_RULES.md` §6.2–§6.3) — or `0` before the
   * battle's first committed Swap. Rendering it is a presentation concern; the
   * client never computes or adjusts it (`GAME_RULES.md` §18).
   */
  readonly combo: number;
  /**
   * The cumulative number of Matches this battle has produced
   * (`GAME_RULES.md` §3). Battle-cumulative: a later Swap never resets it.
   */
  readonly matchCount: number;
}

/**
 * The wire projection of `RngState` (`GAME_STATE.md` §2.6.2).
 *
 * The two components stay together as one logical field (§2.6.2 item 1), which
 * is why they travel as one nested object rather than as two flat properties.
 */
export interface RngStatePayload {
  readonly state: number;
  readonly increment: number;
}

/**
 * The wire projection of `BoardState` (`GAME_STATE.md` §2.1.1).
 *
 * `cells` is exactly 64 entries in row-major order —
 * `index = row * 8 + column` (`MATCH3_RULES.md` §1.0). The client does not
 * receive a partial board and does not request cells individually (§4.1 item 3).
 *
 * Each entry carries the cell's Gem type plus, optionally, the Special Gem at
 * that cell — the same shape the state holds, delivered entry for entry with no
 * additional payload member (`SIGNALR_PROTOCOL.md` §4.1 item 5,
 * `GAME_STATE.md` §2.1.7 items 1–4). There is no `PendingSpecialGems[]` and no
 * second collection (§2.1.2 item 1).
 *
 * The client renders this and derives nothing: it never creates, places, moves,
 * matches, activates, chains, or clears a Special Gem, and it never infers a
 * Special Gem's type or orientation from anything but the state it was sent
 * (`SIGNALR_PROTOCOL.md` §4.1 item 6, `GAME_RULES.md` §18, ADR-001).
 */
export interface BoardPayload {
  /**
   * One cell entry per cell, in ascending index order. The array position *is*
   * the cell index (`GAME_STATE.md` §2.1.7 item 2), so no index is carried per
   * element.
   */
  readonly cells: readonly CellPayload[];
}

/**
 * The wire projection of one `Cells[64]` entry (`GAME_STATE.md` §2.1.1).
 */
export interface CellPayload {
  /**
   * The cell's Gem type, as the documented contract name (`ATK`, `DEF`, `HP`,
   * `POWER` — `MATCH3_RULES.md` §1.1). Always present, including for a cell that
   * holds a Special Gem: a Special Gem adds metadata to a cell's occupant and
   * does not replace its Gem type (`GAME_STATE.md` §2.1.3 items 1–2).
   */
  readonly gemType: string;
  /**
   * The Special Gem at this cell, or `null`/absent for an ordinary Gem. Absence
   * is the documented representation of "this cell holds no Special Gem" — it is
   * not an invitation to predict one (`GAME_STATE.md` §2.1.7 item 3,
   * `SIGNALR_PROTOCOL.md` §4.1 item 6).
   */
  readonly specialGem?: SpecialGemPayload | null;
}

/**
 * The wire projection of `SpecialGem` metadata (`GAME_STATE.md` §2.1.4).
 */
export interface SpecialGemPayload {
  /**
   * `LineClear`, `Burst`, or `Area` — the three MVP types
   * (`GAME_STATE.md` §2.1.4 item 1, `MATCH3_RULES.md` §5.2–§5.4). The client
   * treats this as an opaque label for rendering.
   */
  readonly type: string;
  /**
   * `Horizontal` or `Vertical`, present if and only if `type` is `LineClear`
   * (`GAME_STATE.md` §2.1.4 item 2). A `Burst` or `Area` entry carries none.
   */
  readonly orientation?: string | null;
}

/**
 * Transport-agnostic connection lifecycle callbacks.
 *
 * These mirror the underlying SignalR client lifecycle so the runtime can react
 * to reconnection (task §19) without importing SignalR types itself.
 */
export interface SignalRConnectionHandlers {
  onConnecting?: () => void;
  onConnected?: (connectionId: string | null) => void;
  onReconnecting?: (error?: Error) => void;
  onReconnected?: (connectionId?: string) => void;
  onClosed?: (error?: Error) => void;
}

/**
 * SignalR Hub client wrapper (ARCHITECTURE.md §1 `services/realtime/`).
 *
 * Isolates SignalR transport details. `GameRuntime` depends on this service,
 * never on `HubConnection` directly — that keeps Phaser independent of the
 * transport implementation (ARCHITECTURE.md §2.2 rule 3, task §16).
 *
 * In-battle hub methods `CardCast` and `PetSkillCast` (SIGNALR_PROTOCOL.md §2)
 * are implemented. So is reconnect recovery: `getBattleState` (§7.1) issues the
 * documented `GetBattleState(battleId)` request and returns the §5 envelope
 * carrying the authoritative snapshot. `ReceiveEvents` (§3) is subscribed
 * generically so the runtime can forward server-authoritative event batches
 * without modelling any event shape.
 *
 * Three client → server gameplay actions are implemented here: `swap` (§2.1),
 * `cardCast` (§2), and `petSkillCast` (§2). None is client-authoritative: the
 * service sends requests and returns §5 acknowledgements verbatim.
 * `getBattleState` (§7.1) is not a gameplay action — it requests the snapshot a
 * reconnected client resynchronizes from and returns it uninterpreted.
 * `joinBattle` (§1.2) adds the connection to the battle's group, which triggers
 * the server's initial-state push (§4.1). The service decides nothing: it does
 * not detect matches, validate board state, resolve a cascade, or modify any
 * battle state — the server remains authoritative (`GAME_RULES.md` §18, ADR-001).
 * The service exposes them and subscribes to `BattleStateUpdated` (§4) without
 * interpreting the payload.
 */
export class SignalRService {
  private static instance: SignalRService | null = null;
  private connection: signalR.HubConnection | null = null;
  private handlers: SignalRConnectionHandlers = {};
  /** Guards against concurrent connect() calls creating two connections. */
  private startPromise: Promise<void> | null = null;

  private constructor() {}

  public static getInstance(): SignalRService {
    if (!SignalRService.instance) {
      SignalRService.instance = new SignalRService();
    }
    return SignalRService.instance;
  }

  /**
   * Registers connection lifecycle callbacks. Replaces any previously
   * registered handlers so a remounting consumer cannot accumulate listeners.
   */
  public setHandlers(handlers: SignalRConnectionHandlers): void {
    this.handlers = handlers;
  }

  public async connect(hubUrl: string = '/hubs/battle'): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    // Idempotent: a second concurrent connect() must not build a second
    // connection (task §20 — "do not create duplicate SignalR connections").
    if (this.startPromise) {
      return this.startPromise;
    }

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        // SIGNALR_PROTOCOL.md §1 item 3 / ADR-015 D4: the `BattleHub` connection
        // is authenticated with the application session — the same JWT REST
        // carries — supplied through SignalR's standard access-token mechanism.
        //
        // It is a factory rather than a value so a reconnect after a new §2
        // exchange presents the current session. The Discord access token is
        // never used here: it is not the application session and is never
        // accepted as a `BattleHub` credential (§1 item 4).
        accessTokenFactory: () => ApplicationSession.getInstance().getSessionToken() ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection = connection;

    connection.onreconnecting((error) => {
      this.handlers.onReconnecting?.(error);
    });

    connection.onreconnected((connectionId) => {
      this.handlers.onReconnected?.(connectionId);
    });

    connection.onclose((error) => {
      this.handlers.onClosed?.(error);
    });

    this.handlers.onConnecting?.();

    const startPromise = connection
      .start()
      .then(() => {
        // A disconnect may have superseded this connection while it was
        // negotiating; only report a connection that is still the active one.
        if (this.connection !== connection) {
          return;
        }
        this.handlers.onConnected?.(connection.connectionId ?? null);
      })
      .finally(() => {
        if (this.startPromise === startPromise) {
          this.startPromise = null;
        }
      });

    this.startPromise = startPromise;

    return startPromise;
  }

  /**
   * Stops the current connection.
   *
   * Any in-flight `connect()` is awaited first so the stop cannot race a
   * connection that is still negotiating — that race is exactly what produces
   * "The connection was stopped during negotiation" under React StrictMode's
   * mount → cleanup → mount cycle.
   */
  public async disconnect(): Promise<void> {
    const inFlight = this.startPromise;

    const connection = this.connection;
    this.connection = null;
    this.startPromise = null;

    if (inFlight) {
      // Settle the negotiation attempt, whatever its outcome.
      await inFlight.catch(() => undefined);
    }

    if (connection) {
      await connection.stop();
    }
  }

  public getConnectionState(): signalR.HubConnectionState {
    return this.connection ? this.connection.state : signalR.HubConnectionState.Disconnected;
  }

  public isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  /** The server-assigned connection id, or null when not connected. */
  public getConnectionId(): string | null {
    return this.connection?.connectionId ?? null;
  }

  /**
   * Subscribes to a server → client broadcast. Returns an unsubscribe function.
   *
   * Used for `ReceiveEvents` (SIGNALR_PROTOCOL.md §3),
   * `BattleStateUpdated` (§4) and `RuntimeStatusChanged` (the technical
   * connection acknowledgement).
   */
  public on<TArgs extends unknown[]>(
    methodName: string,
    handler: (...args: TArgs) => void
  ): () => void {
    if (!this.connection) {
      throw new Error('SignalR connection is not established.');
    }

    this.connection.on(methodName, handler);
    return () => {
      this.connection?.off(methodName, handler);
    };
  }

  /**
   * Joins the battle's group (SIGNALR_PROTOCOL.md §1.2).
   *
   * Joining is what triggers the server's authoritative initial-state push
   * (§4.1) — this method itself returns nothing and derives nothing. Adding the
   * connection to the group is transport work; the state arrives asynchronously
   * on `BattleStateUpdated`.
   */
  public async joinBattle(battleId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established.');
    }

    await this.connection.invoke('JoinBattle', battleId);
  }

  public async ping(clientSequence: string = `ping_${Date.now()}`): Promise<PingResult> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established.');
    }

    return await this.connection.invoke<PingResult>('Ping', clientSequence);
  }

  /**
   * Submits one Swap request (`SIGNALR_PROTOCOL.md` §2, §2.1) and returns the
   * §5 acknowledgement verbatim.
   *
   * The four arguments are exactly the documented request, in the documented
   * order: `battleId`, `fromCell`, `toCell`, `clientSequence`. Both cells are
   * §1.0 row-major indices `0..63` and nothing else — no row/column pair, no
   * screen coordinate, and no direction (`MATCH3_RULES.md` §2.1.1, §2.1.3) — and
   * the pair is unordered, so `(12, 13)` and `(13, 12)` name the same swap
   * (§2.1.1 item 2). No gameplay field is sent: the request carries no Gem type,
   * match result, Combo, Turn, or `Sequence` value (§2.1 item 4,
   * `GAME_RULES.md` §18).
   *
   * `clientSequence` is the opaque client-generated correlation id (§2 item 1).
   * It is **not** `BattleState.Sequence`, is never compared with or derived from
   * it, and is never used to reject a stale action — staleness is
   * `MATCH3_RULES.md` §2.1.4 item 2's already-applied check, decided by the
   * server. The client is never required to track a server number for its Swap
   * to be accepted.
   *
   * This is transport only. Whether the swap is legal, whether it produces a
   * match, and what it resolves to are the server's decisions
   * (`MATCH3_RULES.md` §2.1.2, §2.1.6). The service sends the request, returns
   * the result, and mutates nothing — an accepted result does not change any
   * board value here; the next authoritative `BattleStateUpdated` push does
   * that (§3.1, §4).
   */
  public async swap(
    battleId: string,
    fromCell: number,
    toCell: number,
    clientSequence: string
  ): Promise<SwapResult> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established.');
    }

    return await this.connection.invoke<SwapResult>(
      'Swap',
      battleId,
      fromCell,
      toCell,
      clientSequence
    );
  }

  /**
   * Submits one CardCast request (`SIGNALR_PROTOCOL.md` §2) and returns the
   * §5 acknowledgement verbatim.
   *
   * The three arguments are exactly the documented request, in the documented
   * order: `battleId`, `cardId`, `clientSequence`. `cardId` is the cast Card's
   * `CardDefinitionId` (§3.2.20).
   *
   * `clientSequence` is the opaque client-generated correlation id (§2 item 1).
   */
  public async cardCast(
    battleId: string,
    cardId: string,
    clientSequence?: string
  ): Promise<CardCastAcknowledgement> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established.');
    }

    return await this.connection.invoke<CardCastAcknowledgement>(
      'CardCast',
      battleId,
      cardId,
      clientSequence
    );
  }

  /**
   * Submits one PetSkillCast request (`SIGNALR_PROTOCOL.md` §2) and returns the
   * §5 acknowledgement verbatim.
   *
   * The two arguments are exactly the documented request, in the documented
   * order: `battleId`, `clientSequence`. The active Pet's Signature Skill is
   * implied (§2).
   *
   * `clientSequence` is the opaque client-generated correlation id (§2 item 1).
   */
  public async petSkillCast(
    battleId: string,
    clientSequence?: string
  ): Promise<PetSkillCastAcknowledgement> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established.');
    }

    return await this.connection.invoke<PetSkillCastAcknowledgement>(
      'PetSkillCast',
      battleId,
      clientSequence
    );
  }

  /**
   * Requests the current authoritative battle state snapshot for a reconnected
   * client (`SIGNALR_PROTOCOL.md` §7.1) and returns the documented §5 envelope
   * verbatim.
   *
   * The one argument is exactly the documented request —
   * `GetBattleState(battleId)` — and the **existing** connection is reused: this
   * invokes on the connection `connect` already built and opens no second
   * connection path. `battleId` is the battle the runtime already holds from the
   * server's own state; the caller never invents one.
   *
   * This is §7's reconnect/resync read, not §4's join push. §4 is an unsolicited
   * server push on group join carrying the implemented stage's fields; §7 is a
   * client-requested full snapshot used when the client has lost
   * synchronization, and the two are not interchangeable (§7, ADR-008).
   *
   * It is transport only, and it is a **read**: it resolves no action, changes
   * no battle state, and emits no Battle Event — the server's method increments
   * no `Sequence` and reports the committed record as it stands. The service
   * therefore performs no gameplay work of any kind. It does not calculate
   * damage, Crit, Burn, or any other value, does not merge or reconcile state,
   * does not seed, compare, increment, or reject on `serverSequence`
   * (`GAME_STATE.md` §5.2 item 3), and replays nothing (§7.2, `ARCHITECTURE.md`
   * §5.2). The snapshot is handed on as received; ingestion is the runtime's
   * (`GameRuntime.receiveBattleState`, §4's existing path).
   *
   * `accepted: false` carries the documented reason and no state. This method
   * does not decide what `BATTLE_NOT_FOUND` means — §7.3's behavior (the battle
   * is treated as ended and the client falls back to the existing battle-result
   * route) is the runtime's, not the transport's.
   */
  public async getBattleState(battleId: string): Promise<BattleStateSnapshotResponse> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established.');
    }

    return await this.connection.invoke<BattleStateSnapshotResponse>('GetBattleState', battleId);
  }
}