/**
 * Runtime event contract for the client game runtime.
 *
 * Two distinct categories live here, and the distinction is the whole point of
 * the architecture:
 *
 * 1. RUNTIME (technical) events — connection/lifecycle transitions produced by
 *    `GameRuntime`. These are infrastructure, not gameplay.
 *
 * 2. BATTLE events (server → client) — forwarded verbatim from SignalR. Per
 *    SIGNALR_PROTOCOL.md §3 these arrive together in one `ReceiveEvents` call,
 *    already ordered by the server (GAME_RULES.md §17), with the authoritative
 *    `BattleState.Sequence` (GAME_STATE.md §5).
 *
 * `GameRuntime` is a transport boundary: it forwards battle events to the
 * presentation layer unchanged. It never produces, reorders, filters, or
 * interprets them — reordering would corrupt the resolution order the protocol
 * guarantees, and interpreting them would make the client authoritative
 * (GAME_RULES.md §18, ADR-001, AGENTS.md §10).
 *
 * No battle event *names* are invented here. The `events` array is opaque
 * payload: GAME_EVENTS.md §2 owns event content and §3.1 leaves the wire schema
 * to the protocol document. Nothing in this file enumerates gameplay events.
 */

import type { GameRuntimeState } from '../../state/GameRuntimeState';

/** Technical runtime lifecycle events emitted by `GameRuntime`. */
export type RuntimeEventType =
  | 'runtime_state_changed'
  | 'connected'
  | 'disconnected'
  | 'reconnecting'
  | 'reconnected'
  | 'connection_error'
  | 'runtime_error'
  | 'battle_state_changed';

export interface RuntimeEvent {
  readonly type: RuntimeEventType;
  /** State after the transition. */
  readonly state: GameRuntimeState;
  /**
   * Optional technical detail (e.g. an error message). Never gameplay data.
   * `null` when the transition carried no detail.
   */
  readonly detail?: string | null;
}

/**
 * A transport-agnostic battle event envelope forwarded from the server.
 *
 * Mirrors the `ReceiveEvents(battleId, serverSequence, events[])` delivery
 * defined in SIGNALR_PROTOCOL.md §3. `events` contents are owned by
 * GAME_EVENTS.md and are not interpreted by the runtime.
 */
export interface BattleEventsEnvelope {
  readonly battleId: string;
  /**
   * `BattleState.Sequence` after the resolution (GAME_STATE.md §5) —
   * strictly increasing, no gaps. Used only for gap detection per
   * SIGNALR_PROTOCOL.md §5.3; the runtime does not act on it beyond reporting.
   */
  readonly serverSequence: number;
  /** Ordered events produced by resolving one action. Opaque to the runtime. */
  readonly events: readonly unknown[];
}

/** Listener signature for runtime (technical) lifecycle events. */
export type RuntimeEventListener = (event: RuntimeEvent) => void;

/** Listener signature for server-authoritative battle event batches. */
export type BattleEventsListener = (envelope: BattleEventsEnvelope) => void;

/**
 * The client's synchronized copy of the authoritative Battle State Foundation
 * (`GAME_STATE.md` §2.0), as delivered by `BattleStateUpdated`
 * (SIGNALR_PROTOCOL.md §4).
 *
 * Exactly the §2.0 fields — `battleId`, `turn`, `sequence`. The client stores
 * this and renders it; it never authors, adjusts, or recomputes it (§4.9,
 * `GAME_RULES.md` §18, ADR-001). No gameplay field is modelled here and no
 * `Status`/lifecycle value exists in the protocol (§8.3).
 */
export interface RuntimeBattleState {
  readonly battleId: string;
  readonly turn: number;
  readonly sequence: number;
}

/** Listener signature for authoritative battle-state pushes. */
export type BattleStateListener = (state: RuntimeBattleState) => void;

/**
 * The runtime surface the presentation layer (Phaser scenes / React) depends on.
 *
 * Scenes depend on this interface, never on SignalR. That is the boundary that
 * keeps Phaser independent of the transport implementation
 * (ARCHITECTURE.md §2.2 rule 3, AGENTS.md §13).
 */
export interface GameRuntimePort {
  /** Current technical runtime state. */
  getState(): GameRuntimeState;
  /**
   * The client's synchronized copy of the authoritative foundation battle state
   * (`GAME_STATE.md` §2.0), or `null` before the server has pushed it.
   *
   * This is a synchronized presentation copy, not client-owned state: the
   * client never authors `battleId`, `turn`, or `sequence`
   * (SIGNALR_PROTOCOL.md §4.9).
   */
  getBattleState(): RuntimeBattleState | null;
  /** Subscribes to technical runtime lifecycle events. Returns an unsubscribe. */
  onRuntimeEvent(listener: RuntimeEventListener): () => void;
  /** Subscribes to server-authoritative battle event batches. */
  onBattleEvents(listener: BattleEventsListener): () => void;
  /**
   * Subscribes to authoritative battle-state pushes
   * (SIGNALR_PROTOCOL.md §4). The state is delivered unchanged; the runtime
   * derives nothing from it. Returns an unsubscribe.
   */
  onBattleState(listener: BattleStateListener): () => void;
  /**
   * Request boundary for client → server actions (SIGNALR_PROTOCOL.md §2).
   *
   * Deliberately unimplemented: no gameplay action input exists in this task.
   * It exists so the architectural boundary is established (task §15) without
   * inventing gameplay request shapes.
   */
  requestAction(action: RuntimeActionRequest): Promise<never>;
}

/**
 * Placeholder for a client → server gameplay action request.
 *
 * No concrete action (`Swap` / `CardCast` / `PetSkillCast`) is modelled: those
 * shapes are owned by SIGNALR_PROTOCOL.md §2 and implementing them is gameplay.
 */
export interface RuntimeActionRequest {
  readonly kind: string;
  readonly [key: string]: unknown;
}

/** Raised when a caller attempts a gameplay action the runtime does not implement. */
export class RuntimeActionNotImplementedError extends Error {
  constructor(kind: string) {
    super(
      `Runtime action "${kind}" is not implemented: gameplay action requests ` +
        `(SIGNALR_PROTOCOL.md §2) are outside the runtime foundation scope.`
    );
    this.name = 'RuntimeActionNotImplementedError';
  }
}