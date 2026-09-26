import * as signalR from '@microsoft/signalr';

export interface PingResult {
  accepted: boolean;
  clientSequence?: string;
  serverTime: string;
}

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
 * In-battle hub methods (`Swap`, `CardCast`, `PetSkillCast`, `GetBattleState`)
 * are NOT implemented: they are gameplay (SIGNALR_PROTOCOL.md §2, §7).
 * `ReceiveEvents` (§3) is subscribed generically so the runtime can forward
 * server-authoritative event batches without modelling any event shape.
 *
 * `JoinBattle` (§1.2) is the one client → server method implemented here, and it
 * is not gameplay: it adds the connection to the battle's group, which is what
 * triggers the server's initial-state push (§4.1). The service exposes it and
 * subscribes to `BattleStateUpdated` (§4) without interpreting the payload.
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
      .withUrl(hubUrl)
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
}