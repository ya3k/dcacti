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
import type { BattleResultResponse, BattleStartRequest } from '../../services/api/BattleModels';
import type { CardResponse, PetResponse, RelicResponse } from '../../services/api/CollectionModels';

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
   * The `current / threshold` pair (`GAME_STATE.md` §2.3). Both members are
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
  /**
   * The active Pet's equipped cards (`SIGNALR_PROTOCOL.md` §4.3 item 13,
   * `GAME_STATE.md` §2.3). Exactly the 4-entry loadout (3 Basic Cards +
   * 1 Pet Skill Card). Always present, non-empty, and non-nullable.
   */
  readonly equippedCards: readonly string[];
}

/**
 * The client's synchronized copy of `GAME_STATE.md` §2.3's `PassiveProgress`
 * pair (`SIGNALR_PROTOCOL.md` §4.3 item 4).
 *
 * One logical field with two members, read together to render the documented
 * `current / threshold` pair (`PASSIVE_RULES.md` §6 item 1). Neither is
 * nullable and neither is omitted.
 */
export interface RuntimePassiveProgress {
  /** The Passive's own Threshold (`PASSIVE_RULES.md` §1). */
  readonly threshold: number;
  /** The settled progress reached toward it (`GAME_STATE.md` §2.3). */
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
 *
 * The rule is transport-general (`ARCHITECTURE.md` §2.2.1 rule 1, §2.2.3 rule
 * 3): a scene must not import `fetch`, `ApiService`, or any other HTTP/REST
 * client either, so the pre-battle selection flow's collection read is a
 * capability of this port rather than a direct `services/api/` call.
 *
 * **Capabilities, not models** (`ARCHITECTURE.md` §2.2.3 rule 6). The port
 * exposes what a scene may *ask the runtime to do*; it defines, re-exports, and
 * owns no collection read model, loadout, or selection type. The wire shapes
 * referenced below are owned by `services/api/` and are only imported as
 * types, so this file declares none of them.
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
   * The documented **swap**, **cardCast**, and **petSkillCast** actions are
   * implemented: they submit through the transport and resolve with the §5
   * acknowledgement. No other action kind is accepted — those still reject with
   * `RuntimeActionNotImplementedError`.
   *
   * §7's `GetBattleState` snapshot is **not** an action on this boundary: it is
   * requested by the runtime itself when the connection is re-established
   * (`SIGNALR_PROTOCOL.md` §7.1), from the battle identity the runtime already
   * holds, so no caller submits it and no caller supplies a `battleId` for it.
   *
   * The runtime coordinates the request and nothing else: it does not decide
   * whether an action is legal, does not compute a match, cascade, combo, or
   * damage, and does not mutate the board or combat state. The server resolves
   * the action and the next authoritative push re-renders the client
   * (`GAME_RULES.md` §18, ADR-001).
   */
  requestAction(action: RuntimeActionRequest): Promise<RuntimeActionAcknowledgement>;
  /**
   * The battle-start capability of the pre-battle selection flow
   * (`ARCHITECTURE.md` §2.2.3 rule 5, TASK-077).
   *
   * `LobbyScene` submits the documented `BattleStartRequest`
   * (`API_CONTRACTS.md` §3) built from its own in-progress selection and the
   * runtime performs the documented start sequence
   * (`SIGNALR_PROTOCOL.md` §1 items 1–2, §2):
   *
   * ```text
   * ApiService.startBattle(request)   →  POST /api/battle/start
   *         ↓
   * SignalRService.connect(signalrHub)
   *         ↓
   * SignalRService.joinBattle(battleId)
   * ```
   *
   * The scene supplies the selection and nothing else. It does not validate the
   * loadout, does not decide whether the selection is legal, and does not handle
   * the battle: the server is authoritative for the resulting `BattleState`
   * (`GAME_RULES.md` §18, ADR-001).
   *
   * The promise settles once the documented sequence has run — a rejection
   * (`401 UNAUTHENTICATED`, `400 INVALID_LOADOUT` / `PET_NOT_OWNED` /
   * `BOSS_NOT_FOUND`, or a connect/join failure) leaves no battle created and
   * nothing fabricated, and the caller keeps its selection to retry
   * (`ARCHITECTURE.md` §2.2.3 rule 5).
   */
  startBattle(request: BattleStartRequest): Promise<void>;
  /**
   * The owned Pet collection (`API_CONTRACTS.md` §5.1) as the server returns it.
   *
   * This is a **selection source, never a selection**
   * (`ARCHITECTURE.md` §2.2.3 rule 4): the response carries no equip state and
   * no defined ordering (§5.5, §5.6), so the caller builds its own selection
   * from it and must not read a loadout out of it. The runtime transports the
   * read unchanged — it does not sort, filter, default, or cache it.
   */
  getPets(): Promise<PetResponse[]>;
  /**
   * One owned Pet's detail (`API_CONTRACTS.md` §5.2), addressed by the owned
   * **instance** identity (`Pet.PetInstanceId`, §5.1). The 200 body is the same
   * object as one `getPets` array element.
   */
  getPet(petId: string): Promise<PetResponse>;
  /**
   * The Card definitions the Player has unlocked (`API_CONTRACTS.md` §5.3).
   * Membership of the array **is** the unlocked state (ADR-012), so there is no
   * equip or unlock member to read from it (§5.5, §5.6).
   */
  getCards(): Promise<CardResponse[]>;
  /**
   * The Relic **instances** the Player owns (`API_CONTRACTS.md` §5.4), each
   * carrying its owned instance identity and no equip state (§5.6). The
   * instance identity is what a `relicLoadout` selection submits (§3).
   */
  getRelics(): Promise<RelicResponse[]>;
  /**
   * A completed battle's persisted result (`API_CONTRACTS.md` §4) —
   * `GET /api/battle/{battleId}/result`.
   *
   * This is the documented result read for a battle that has **already ended**;
   * §4 states the endpoint returns data only in that case. It is what
   * `ResultScene` renders the persisted `rewards` (`RewardSummary`) from, and it
   * is **not** an action on the `requestAction` boundary — it resolves no
   * action, changes no state, and emits no event.
   *
   * The runtime transports the read unchanged: it does not compute a reward,
   * apply a grant, update Player or Pet progression, promote `null` to a
   * number, or cache the response. Reward amounts and the resulting progression
   * are server-authored (`GAME_RULES.md` §18, `AGENTS.md` §10, ADR-001).
   *
   * This is a second caller of the same route the runtime already reads on
   * `SIGNALR_PROTOCOL.md` §7.3's fallback; it adds no endpoint, no query
   * parameter, and no second retrieval mechanism.
   */
  getBattleResult(battleId: string): Promise<BattleResultResponse>;
}

/**
 * The action kind the runtime implements (`SIGNALR_PROTOCOL.md` §2.1).
 *
 * The string is the documented hub method name, so the request carries the
 * protocol's own action vocabulary rather than a second one invented here.
 */
export const RUNTIME_ACTION_SWAP = 'Swap';
export const RUNTIME_ACTION_CARD_CAST = 'CardCast';
export const RUNTIME_ACTION_PET_SKILL_CAST = 'PetSkillCast';

/**
 * A client → server gameplay action request (SIGNALR_PROTOCOL.md §2).
 *
 * Swap submits the two §1.0 cell indices of the pair the player selected
 * (`MATCH3_RULES.md` §2.1.1) and nothing else. No Gem type, match result,
 * Combo, Turn, or `Sequence` value is carried.
 *
 * CardCast submits the card definition identity (`cardId`, §3.2.20) and
 * nothing else: no Power cost, damage, effect, or result.
 *
 * PetSkillCast submits no card or skill identifier (§2): the active Pet's
 * Signature Skill is implied.
 *
 * `battleId` is deliberately **not** a member on any request: the runtime
 * sources it from the battle state the server already pushed, so the scene
 * cannot supply an identifier the runtime never received (SIGNALR_PROTOCOL.md
 * §2.1: the transport identifies the battle).
 *
 * These are requests, not state. The client never treats a submitted action as
 * authoritative, and an accepted request changes nothing locally — the next
 * `BattleStateUpdated` push does (`SIGNALR_PROTOCOL.md` §3.1, §4).
 */
export interface RuntimeSwapActionRequest {
  readonly kind: typeof RUNTIME_ACTION_SWAP;
  /**
   * The §1.0 index of the cell the player is moving (`MATCH3_RULES.md` §2.1.1).
   */
  readonly fromCell: number;
  /**
   * The §1.0 index it is exchanged with (`MATCH3_RULES.md` §2.1.1). The pair is
   * unordered: the two indices together are the swap's identity, and neither is
   * a "primary" cell for validation (§2.1.1 item 2, §2.1.3 item 3).
   */
  readonly toCell: number;
}

export interface RuntimeCardCastActionRequest {
  readonly kind: typeof RUNTIME_ACTION_CARD_CAST;
  /**
   * The cast Card's `CardDefinitionId` (`SIGNALR_PROTOCOL.md` §2, §3.2.20).
   */
  readonly cardId: string;
}

export interface RuntimePetSkillCastActionRequest {
  readonly kind: typeof RUNTIME_ACTION_PET_SKILL_CAST;
}

/**
 * Any client → server action request the runtime boundary accepts.
 *
 * An unknown or unimplemented action reaches the boundary as an unmodelled value
 * and is rejected with `RuntimeActionNotImplementedError`. §7's `GetBattleState`
 * snapshot is not an action a caller submits — the runtime requests it itself
 * on reconnect — so it is not a member of this union either.
 */
export type RuntimeActionRequest =
  | RuntimeSwapActionRequest
  | RuntimeCardCastActionRequest
  | RuntimePetSkillCastActionRequest;

/**
 * The transport-level result of an action request
 * (`SIGNALR_PROTOCOL.md` §5).
 *
 * It is feedback about the **request**, not authoritative state and not a Battle
 * Event: it is not broadcast, is not sequenced, and changes nothing by itself
 * (§5 item 1). `accepted: true` means the action was resolved and its events
 * arrive on the §3 batch; `accepted: false` means the action was rejected under
 * its owning domain rule and nothing happened (`MATCH3_RULES.md` §2.1.5).
 *
 * The runtime hands this to the caller unchanged: it does not interpret
 * `reason`, does not retry, and does not mutate state on either outcome
 * (§5 item 2). `reason` is presentation data only — the client derives no
 * gameplay meaning from it.
 */
export interface RuntimeActionAcknowledgement {
  /** True when the action was resolved; false when it was rejected (§5 item 2). */
  readonly accepted: boolean;
  /**
   * The machine-readable rejection code (§5 item 3), or absent/`null` when
   * accepted. Empty strings are normalised to absence: a rejection always
   * carries a real code, and the client must not treat an empty one as a reason.
   */
  readonly reason?: string | null;
}

/** Raised when a caller attempts a gameplay action the runtime does not implement. */
export class RuntimeActionNotImplementedError extends Error {
  constructor(kind: string) {
    super(
      `Runtime action "${kind}" is not implemented: the client implements ` +
        `Swap, CardCast, and PetSkillCast requests (SIGNALR_PROTOCOL.md §2). ` +
        `GetBattleState (§7) is not a caller-submitted action.`
    );
    this.name = 'RuntimeActionNotImplementedError';
  }
}