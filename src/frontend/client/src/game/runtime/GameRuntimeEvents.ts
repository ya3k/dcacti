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
 * The client's synchronized copy of the authoritative battle state, as delivered
 * by `BattleStateUpdated` (`SIGNALR_PROTOCOL.md` §4, §4.2, §4.3).
 *
 * The implemented `GAME_STATE.md` §0 stage's fields — `battleId`, `turn`,
 * `sequence`, `rngSeed`, `rngState`, `board`, `playerState`, `petState`. The
 * client stores this and renders it; it never authors, adjusts, or recomputes it
 * (§4.9, `GAME_RULES.md` §18, ADR-001). No gameplay field beyond the stage's own
 * is modelled here and no `Status`/lifecycle value exists in the protocol (§8.3).
 *
 * `board` is the server-generated `Cells[64]`. The client must not generate,
 * fill, repair, validate, or re-derive it, and no client-side RNG participates
 * in any part of it (§4 item 10, `GAME_STATE.md` §2.0.5.4.1).
 *
 * `rngSeed`/`rngState` are part of the authoritative state (`GAME_STATE.md`
 * §2.6) and are carried because they are `BattleState` fields — not because the
 * client uses them. The client never advances, re-seeds, or draws from the RNG
 * and never uses it to produce a Gem value; a client that needs a board reads
 * `board` (§4.1 item 2).
 *
 * `playerState` carries `MatchCount` and `Combo` (`GAME_STATE.md` §2.2). It is
 * carried for the same reason: it is a `BattleState` field. The client renders
 * both values and never computes them — it does not count Matches, advance a
 * Combo, or reset one (`GAME_RULES.md` §18, `MATCH3_RULES.md` §6.6 item 3).
 *
 * `petState` carries the active Pet's Passive trio (`GAME_STATE.md` §2.3,
 * §4.3). It is carried because it is a `BattleState` field of the implemented
 * stage and because `PASSIVE_RULES.md` §6 item 1 requires the progress to be
 * exposed as a UI-facing value. The client renders the pair it was sent and
 * derives nothing: it does not charge a Passive, evaluate a Threshold, reset
 * progress, or apply an overflow, and it re-derives none of it from `board`,
 * `turn`, `sequence`, `combo`, or `matchCount` (§4.3 item 9, `GAME_RULES.md`
 * §18, ADR-001).
 */
export interface RuntimeBattleState {
  readonly battleId: string;
  readonly turn: number;
  readonly sequence: number;
  readonly rngSeed: number;
  readonly rngState: RuntimeRngState;
  readonly board: RuntimeBoard;
  readonly playerState: RuntimePlayerState;
  readonly petState: RuntimePetState;
}

/**
 * The client's synchronized copy of the delivered `PetState` members
 * (`GAME_STATE.md` §2.3, `SIGNALR_PROTOCOL.md` §4.3).
 *
 * Exactly the three members §4.3 item 2 fixes, mirroring the wire shape:
 * `passiveId`, `passiveProgress`, and the conditional `passiveResetOverride`.
 * The rest of §2.3 — identity, progression, the combat stats, and both loadout
 * snapshots — is not delivered and is deliberately not modelled here
 * (`GAME_STATE.md` §2.3: "Domain state implemented is not the same as client
 * wire delivery").
 *
 * This is a synchronized presentation copy. Every value is read as sent and
 * rendered; none is derived, advanced, or recomputed by the client
 * (`PASSIVE_RULES.md` §2–§5 own the charging, threshold, trigger, and reset
 * rules, and the server evaluates them — §4.3 item 9, ADR-001).
 */
export interface RuntimePetState {
  /**
   * The active Pet's Passive identity (`GAME_STATE.md` §2.3) — the same value
   * the `PassiveCharged`/`PassiveTriggered` events report (`GAME_EVENTS.md`
   * §2 item 1). Always present; it is the identity, not the definition
   * (§4.3 item 3).
   */
  readonly passiveId: string;
  /**
   * The `current / threshold` pair (`GAME_STATE.md` §2.5). Both members are
   * always present — `current = 0` is a real value, not an absence (§4.3
   * item 4).
   */
  readonly passiveProgress: RuntimePassiveProgress;
  /**
   * The Passive's non-default Reset Behavior — `"Partial"` or `"NoReset"`
   * (`PASSIVE_RULES.md` §4 item 2) — or absent for the default. Absence is the
   * documented representation of `Default`; it is never `null`, never
   * `"Default"`, and the client must not invent a value in its place (§4.3
   * item 7).
   */
  readonly passiveResetOverride?: string;
}

/**
 * The client's synchronized copy of `GAME_STATE.md` §2.5's `PassiveProgress`
 * pair (`SIGNALR_PROTOCOL.md` §4.3 item 4).
 *
 * One logical field with two members, read together to render the documented
 * `current / threshold` pair (`PASSIVE_RULES.md` §6 item 1). Neither is
 * nullable and neither is omitted.
 */
export interface RuntimePassiveProgress {
  /** The Passive's own Threshold (`PASSIVE_RULES.md` §1). */
  readonly threshold: number;
  /** The settled progress reached toward it (`GAME_STATE.md` §2.5). */
  readonly current: number;
}

/**
 * The client's synchronized copy of `PlayerState`'s implemented fields
 * (`GAME_STATE.md` §2.2).
 *
 * Both are always present, including at `0`: `combo = 0` is the value the state
 * reads before the battle's first committed Swap, and it is a value, not a gap
 * (`MATCH3_RULES.md` §6.5 item 4). The client renders them and derives nothing
 * from the board, the counters, or the resolution.
 */
export interface RuntimePlayerState {
  /** The most recently committed Swap's Match total (`MATCH3_RULES.md` §6.3). */
  readonly combo: number;
  /** The battle's cumulative Match total (`GAME_RULES.md` §3). */
  readonly matchCount: number;
}

/**
 * The client's synchronized copy of `RngState` (`GAME_STATE.md` §2.6.2).
 *
 * One logical field with two components (§2.6.2 item 1). Opaque to the client:
 * it is transported, never advanced or drawn from.
 */
export interface RuntimeRngState {
  readonly state: number;
  readonly increment: number;
}

/**
 * The client's synchronized copy of the authoritative board
 * (`GAME_STATE.md` §2.1.1).
 *
 * `cells` is exactly 64 Gem type names in row-major order —
 * `index = row * 8 + column` (`MATCH3_RULES.md` §1.0). At the Board Foundation
 * stage `Cells[64]` is the only part of `BoardState` that exists; rendering it at
 * `row = floor(index / 8)`, `column = index % 8` is a presentation concern
 * (`ARCHITECTURE.md` §2.2.2) and introduces no competing coordinate system.
 */
export interface RuntimeBoard {
  readonly cells: readonly string[];
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
   * The client's synchronized copy of the authoritative battle state
   * (`GAME_STATE.md` §2.2, §2.0.5), or `null` before the server has pushed it.
   *
   * This is a synchronized presentation copy, not client-owned state: the client
   * never authors any of its fields, and never generates or re-derives
   * `board` (SIGNALR_PROTOCOL.md §4.9–§4.10).
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