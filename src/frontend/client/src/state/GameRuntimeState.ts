/**
 * Runtime state contract for the client game runtime.
 *
 * Scope (deliberate): this describes only the technical/session states required
 * by the runtime foundation — connection, session, runtime and server
 * synchronization status. It is NOT gameplay state.
 *
 * Explicitly absent, because they are authoritative server-owned values that
 * GAME_STATE.md §2 defines as `BattleState` (Persistent/Active Battle State,
 * ADR-001, ADR-005) and are not implemented yet:
 *
 *   player HP, boss HP, board/gem state, pet stats, power, combo, damage,
 *   combat state, turn number, RNG state.
 *
 * The client must never compute or hold those authoritatively (`GAME_RULES.md`
 * §18, ADR-001, `.ai/README.md` §15). When battle resolution is implemented,
 * authoritative `BattleState` arrives from the server and is rendered — it does
 * not become client-owned runtime state defined here.
 */

/** SignalR transport connection status (SIGNALR_PROTOCOL.md §1). */
export type ConnectionStatus =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'error';

/**
 * Session establishment status. Sessions are established by the documented
 * Discord → backend authentication boundary (`POST /api/auth/discord`,
 * API_CONTRACTS.md §2, ADR-007), which is not implemented by this task.
 */
export type SessionStatus =
  | 'unauthenticated'
  | 'authenticating'
  | 'authenticated'
  | 'error';

/** Lifecycle status of the client game runtime itself. */
export type RuntimeStatus = 'initializing' | 'ready' | 'suspended' | 'error';

/**
 * Server synchronization status.
 *
 * `synchronized` means the authoritative battle state has been delivered by the
 * server's initial-state push (SIGNALR_PROTOCOL.md §4) and the runtime now
 * holds a synchronized copy of it. The runtime itself holds no battle state in
 * this contract — that copy is exposed separately as `RuntimeBattleState`
 * (GAME_STATE.md §2.0). `awaiting_battle` is the correct state for a connected
 * runtime that has not yet joined a battle group.
 */
export type SyncStatus = 'unsynchronized' | 'awaiting_battle' | 'synchronized';

/** Phaser engine lifecycle status, mirrored for the runtime status display. */
export type EngineStatus = 'initializing' | 'running' | 'stopped' | 'error';

/** Technical runtime state snapshot. Contains no gameplay state. */
export interface GameRuntimeState {
  /** SignalR transport connection state. */
  readonly connection: ConnectionStatus;
  /** Authenticated application session state. */
  readonly session: SessionStatus;
  /** Client runtime lifecycle state. */
  readonly runtime: RuntimeStatus;
  /** Server synchronization state. */
  readonly sync: SyncStatus;
  /** Phaser engine lifecycle state. */
  readonly engine: EngineStatus;
  /**
   * Hub connection id assigned by the server on connect
   * (SIGNALR_PROTOCOL.md §1; ADR-008 depends on a stable connection identity).
   * `null` until connected.
   */
  readonly connectionId: string | null;
  /**
   * Last technical runtime error, for diagnostics only. Never a gameplay error.
   */
  readonly lastError: string | null;
}

/** Initial runtime state before any connection is attempted. */
export const INITIAL_RUNTIME_STATE: GameRuntimeState = {
  connection: 'disconnected',
  session: 'unauthenticated',
  runtime: 'initializing',
  sync: 'unsynchronized',
  engine: 'initializing',
  connectionId: null,
  lastError: null,
};