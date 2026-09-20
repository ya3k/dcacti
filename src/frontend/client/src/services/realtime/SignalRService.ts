import * as signalR from '@microsoft/signalr';

export interface PingResult {
  accepted: boolean;
  clientSequence?: string;
  serverTime: string;
}

/**
 * The authoritative initial battle state pushed on group join
 * (SIGNALR_PROTOCOL.md §4).
 *
 * Exactly the `GAME_STATE.md` §2.0 fields — `battleId`, `turn`, `sequence`.
 * No gameplay field is carried, and no `Status`/lifecycle value exists anywhere
 * in the protocol (§4.4, §8.3). The values are server-authored
 * (`GAME_RULES.md` §18, ADR-001).
 *
 * This is a transport-level shape only. The service does not interpret it,
 * derive from it, or recompute it; it hands it to the runtime unchanged.
 */
export interface BattleStateUpdatedPayload {
  readonly battleId: string;
  readonly turn: number;
  readonly sequence: number;
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