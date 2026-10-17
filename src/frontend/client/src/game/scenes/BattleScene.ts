import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import { readPreservedLoadout } from '../state/PreservedLoadout';
import { MVP_BOSSES } from './LobbyScene';
import type { GameRuntime } from '../runtime/GameRuntime';
import type { GameRuntimeState } from '../../state/GameRuntimeState';
import type {
  BattleEventsEnvelope,
  BattleStateMetadata,
  RuntimeBattleState,
  RuntimeBoard,
  RuntimeCell,
} from '../runtime/GameRuntimeEvents';
import {
  RUNTIME_ACTION_SWAP,
  RUNTIME_ACTION_CARD_CAST,
  RUNTIME_ACTION_PET_SKILL_CAST,
  RuntimeActionOutcomeUnknownError,
} from '../runtime/GameRuntimeEvents';
import type {
  CardResponse,
  PetResponse,
  PetSignatureSkillResponse,
  RelicResponse,
} from '../../services/api/CollectionModels';
import type {
  EventCallout,
  InBattleServerEvent,
  InteractionStatement,
  MoveLogAbortReason,
  MoveLogLifecycleState,
  MoveLogLifecycleScope,
  PresentedDropMove,
  PresentedSpawnedGem,
  PresentationActor,
  PresentationAction,
  PresentationPhaseKind,
} from './BattleEventPresenter';
import {
  CALLOUT_BOSS_COLOR,
  DAMAGE_PARTY_BOSS,
  DAMAGE_PARTY_PLAYER,
  INTERACTION_BOSS_ATTACK,
  INTERACTION_RESOLVING,
  INTERACTION_YOUR_TURN,
  MOVE_LOG_TITLE,
  adaptPresentedEvent,
  deliveredSwapOf,
  describeEventCallout,
  formatCardCastRejection,
  formatInBattleEvent,
  formatMoveLogRow,
  formatSwapRejection,
  isBossEnraged,
  isPetPassiveEvent,
  parseInBattleEvent,
  resolveOutcomeStatement,
  selectBatchCalloutEvent,
} from './BattleEventPresenter';

/**
 * Board presentation geometry, in logical game pixels.
 *
 * This is the *presentation* conversion of the authoritative row-major index to
 * screen coordinates — the concern `ARCHITECTURE.md` §2.2.2 reserves for the
 * client. It introduces no competing board coordinate system: the board's only
 * coordinate convention remains `index = row * 8 + column`
 * (`MATCH3_RULES.md` §1.0), and these constants only decide how large a cell is
 * drawn.
 *
 * The board's logical size is fixed at 8 x 8 (`MATCH3_RULES.md` §1.0) and is
 * never derived from the viewport: responsive behavior scales the presentation
 * (`GameViewport.ts`, `ARCHITECTURE.md` §2.2.2 rule 6) and must never change the
 * cell count.
 */
const BOARD_COLUMNS = 8;
const BOARD_ROWS = 8;
const CELL_SIZE = 56;
const CELL_GAP = 6;
const BOARD_WIDTH = BOARD_COLUMNS * CELL_SIZE + (BOARD_COLUMNS - 1) * CELL_GAP;
const BOARD_ORIGIN_X = (GAME_WIDTH - BOARD_WIDTH) / 2;
const BOARD_ORIGIN_Y = SAFE_AREA.y + 96;

/**
 * Battle HUD geometry, in logical game pixels.
 *
 * The HUD fills the bands the fixed board leaves free — the strip above it and
 * the column beside it — so nothing it draws overlaps the 8 x 8 board, a
 * matchable cell, a cast control, or the 24 px safe area:
 *
 * ```text
 *   24 ┌───────────────────────────────────────────────────────────┐
 *      │ BOSS  Kim Lôi Vương     <connection>          YOUR TURN    │ ← boss band
 *      │                    <sync, only while unsynchronized>       │
 *      │ [██████████░░░░░░░░]  2680 / 2800                         │
 *  104 │                    MATCH / COMBO ×4                       │ ← callout
 *  120 │ PET  Xich Lang   ┌───────────────┐                        │
 *      │ [██████████░░░░] │               │                        │
 *      │ 916 / 1000       │  8 x 8 board  │                        │
 *      │ POWER [███░░░░░] │               │                        │
 *      │ 42 / 100         │               │                        │
 *      │ COMBO ×4         └───────────────┘                        │
 *      │ MATCHES 12                                                │
 *      │ Passive  …                                                │
 *      │ [███░░░░] 2 / 5 Matches (reset: Default)                  │
 *      │ Effects: Burn (25, 2 turns)                               │
 *  696 └───────────────────────────────────────────────────────────┘
 * ```
 *
 * Every coordinate here is presentation geometry over the fixed 1280 x 720
 * logical space (`GameViewport.ts`): no HUD line depends on the viewport, wraps
 * out of its column, scrolls, or scales with the browser. The whole HUD lives in
 * the boss band and the left column, leaving the area below the board for the
 * cast controls and the transport status lines.
 *
 * **Origin.** Every banded HUD line is drawn with origin `(0, 0)`, so its
 * constant is the line's *top* and a longer value (a second wrapped line, a
 * third Status Effect) grows **downwards** into free space instead of out of the
 * safe area. The centered lines — the connection status, the board message and
 * the combat callout — use a centered origin, and the interaction indicator is
 * right-aligned on the safe area's own inset so its own growth moves leftwards,
 * across the empty half of the boss band and never over the Boss panel.
 */
const HUD_INSET = 12;
const HUD_LEFT = SAFE_AREA.x + HUD_INSET;
const HUD_TOP = SAFE_AREA.y + HUD_INSET;

/** The widest HUD line that stays inside the left column beside the board. */
const HUD_COLUMN_WIDTH = BOARD_ORIGIN_X - HUD_LEFT - HUD_INSET;

/** The gap between a gauge's track and the authoritative readout beside it. */
const HUD_VALUE_GAP = 12;

// --- Boss band: the strip above the board ---------------------------------

/** The Boss section caption and the Boss's display name, on one row. */
const BOSS_CAPTION_Y = HUD_TOP + 6;
const BOSS_NAME_X = HUD_LEFT + 48;
const BOSS_NAME_Y = HUD_TOP + 4;
const BOSS_NAME_WIDTH = 240;

/** The Boss HP gauge and its authoritative `hp / maxHp` readout. */
const BOSS_BAR_X = HUD_LEFT;
const BOSS_BAR_Y = HUD_TOP + 38;
const BOSS_BAR_WIDTH = 250;
const BOSS_BAR_HEIGHT = 16;
const BOSS_HP_TEXT_X = BOSS_BAR_X + BOSS_BAR_WIDTH + HUD_VALUE_GAP;
const BOSS_HP_TEXT_Y = BOSS_BAR_Y;

/** The connection line: centered in the boss band, clear of the Boss panel. */
const CONNECTION_Y = HUD_TOP + 12;

/**
 * The synchronization indicator: the centered line directly beneath the
 * connection line, on the boss band's second row.
 *
 * It is the player-facing half of `GameRuntimeState.sync` — the one technical
 * value this scene renders besides the connection status, and the one
 * `GameRuntimeState.ts` defines for it (`unsynchronized` / `awaiting_battle` /
 * `synchronized`). It is deliberately **not** a battle fact: no turn, phase,
 * status, HP, or board value reaches it, it is not interactive, and it never
 * blocks the board or a cast control.
 *
 * **Why this position.** It shares the boss band's own row with nothing: the
 * connection line sits at `CONNECTION_Y` centered, the interaction indicator is
 * right-aligned on the safe-area inset, and the board starts at
 * `BOARD_ORIGIN_Y`. A short centered line therefore reaches neither the Boss
 * panel (x 36–286), the board, nor the safe area, and it is drawn with a
 * centered origin so it grows symmetrically rather than towards a control.
 *
 * **What it says.** `unsynchronized` while connected is a client that is
 * re-establishing its copy of the server's state — `SYNCHRONIZING…`, in the
 * warning colour the scene uses for pending feedback. `unsynchronized` on a
 * connection that is not up is stated as a state rather than as progress —
 * `NOT SYNCHRONIZED` — because no recovery is in flight to promise. It is
 * hidden for every other value, including `synchronized`, so the line can never
 * claim the copy is restored before the runtime has actually published that.
 */
const SYNC_INDICATOR_Y = CONNECTION_Y + 16;
const SYNC_INDICATOR_WIDTH = 260;
const SYNC_RECOVERING_MESSAGE = 'SYNCHRONIZING…';
const SYNC_UNSYNCHRONIZED_MESSAGE = 'NOT SYNCHRONIZED';
const SYNC_RECOVERING_COLOR = '#fbbf24';
const SYNC_UNSYNCHRONIZED_COLOR = '#f87171';

/**
 * What the board area states when the runtime publishes that the battle can no
 * longer be recovered (`SIGNALR_PROTOCOL.md` §7.3, `BattleStateSource` of
 * `'ended'`).
 *
 * It is a statement about **this client's** hold on the battle, not about the
 * battle: the scene knows only that the authoritative snapshot could not be
 * retrieved, so it says exactly that and presents no board, no HP, no outcome, and
 * no fabricated victory or defeat. The documented result fallback is the runtime's
 * own `GET /api/battle/{battleId}/result` read, which this boundary does not
 * carry.
 */
const BATTLE_ENDED_MESSAGE = 'Battle no longer available';

/**
 * The persistent interaction indicator, at the right-hand end of the boss band.
 *
 * It is the one line that answers "is it the player's move, or is the client
 * busy showing a resolution?" — and it is the **client's own** statement about
 * its own input state, never a battle value. Nothing on the wire says whose turn
 * it is (`GAME_STATE.md` §2.0.3: §2.0 contains no `Status` field, and the closed
 * `type` discriminator set of `SIGNALR_PROTOCOL.md` §3.2.2 admits no turn or
 * phase event — see `TASK-246` C-1), so this line is derived from
 * `isInputLocked()`, the phase of the local presentation timeline that is
 * playing, and the delivered `BattleWon` / `BattleLost` outcome — and from
 * nothing else (`AGENTS.md` §10).
 *
 * **Why this position.** The boss band's right half is the one HUD area no other
 * line uses: the Boss panel and its HP readout occupy the left, the connection
 * status is centered, and the board starts at `BOARD_ORIGIN_Y`. The line is
 * right-aligned on the safe area's own inset, so it can never grow across the
 * centered connection line, reach a board cell, leave the 24 px safe area, or
 * overlap the callout row above the board.
 */
const PHASE_TEXT_X = SAFE_AREA.x + SAFE_AREA.width - HUD_INSET;
const PHASE_TEXT_Y = BOSS_NAME_Y;
const PHASE_TEXT_WIDTH = 300;

/**
 * The transient combat callout — the row directly above the board.
 *
 * It is the one line that reports *what just happened*: a Match, a Combo, a
 * cast, a Boss action or a Passive trigger. It is set from a delivered event and
 * fades; it holds no state, blocks no input, and nothing about the battle
 * depends on it. `boardMessageText` shares this slot, because "no state has
 * arrived yet" and "combat is resolving" are mutually exclusive.
 *
 * Its lifetime is the `CALLOUT_HOLD_MS` + `CALLOUT_FADE_MS` pair of the pacing
 * block below. One delivered batch is one resolution, so the callout belongs to
 * the whole timeline rather than to a phase — but it is bounded to well inside
 * the resolution that raised it, so it can neither outlive its own resolution
 * nor still be on screen when the next one's callout arrives (TASK-246 §5).
 */
const CALLOUT_Y = BOARD_ORIGIN_Y - 16;
const CALLOUT_WIDTH = 460;

// --- Pet panel: the column beside the board -------------------------------

/** The Pet section caption and the Pet's display name, on one row. */
const PET_CAPTION_Y = BOARD_ORIGIN_Y + 32;
const PET_NAME_X = HUD_LEFT + 48;
const PET_NAME_Y = BOARD_ORIGIN_Y + 29;
const PET_NAME_WIDTH = HUD_COLUMN_WIDTH - 48;

/** The Pet HP gauge and its authoritative `hp / maxHp` readout. */
const PET_BAR_X = HUD_LEFT;
const PET_BAR_Y = BOARD_ORIGIN_Y + 66;
const PET_BAR_WIDTH = 200;
const PET_BAR_HEIGHT = 16;
const PET_HP_TEXT_X = PET_BAR_X + PET_BAR_WIDTH + HUD_VALUE_GAP;
const PET_HP_TEXT_Y = PET_BAR_Y;

/** The Power gauge and its readout against the documented range (SECONDARY). */
const POWER_LABEL_Y = BOARD_ORIGIN_Y + 94;
const POWER_BAR_X = HUD_LEFT + 60;
const POWER_BAR_Y = BOARD_ORIGIN_Y + 96;
const POWER_BAR_WIDTH = 140;
const POWER_BAR_HEIGHT = 12;
const POWER_TEXT_X = POWER_BAR_X + POWER_BAR_WIDTH + HUD_VALUE_GAP;
const POWER_TEXT_Y = BOARD_ORIGIN_Y + 93;

/** The delivered Match feedback (SECONDARY): the Combo and the cumulative count. */
const COMBO_Y = BOARD_ORIGIN_Y + 124;
const MATCHES_Y = BOARD_ORIGIN_Y + 154;

/** The delivered Passive identity and its `current / threshold` progress. */
const PET_PASSIVE_Y = BOARD_ORIGIN_Y + 182;
const PASSIVE_BAR_X = HUD_LEFT;
const PASSIVE_BAR_Y = BOARD_ORIGIN_Y + 208;
const PASSIVE_BAR_WIDTH = 120;
const PASSIVE_BAR_HEIGHT = 8;
const PASSIVE_TEXT_X = PASSIVE_BAR_X + PASSIVE_BAR_WIDTH + HUD_VALUE_GAP;
const PASSIVE_TEXT_Y = BOARD_ORIGIN_Y + 206;

/** The delivered Status Effect instances (TERTIARY). Grows downwards when long. */
const PET_STATUS_Y = BOARD_ORIGIN_Y + 236;

/** The Swap interaction's own guidance / acknowledgement line (TERTIARY). */
const SWAP_TEXT_Y = BOARD_ORIGIN_Y + 300;

/** The cast interaction's own acknowledgement line, below the board. */
const CAST_TEXT_Y = BOARD_ORIGIN_Y + BOARD_WIDTH + 18;

// --- Developer Move Log: the free band to the right of the board ------------
//
// `TASK-253` / operator decision D-1 authorizes exactly one developer surface
// inside the battle view: this panel, drawn only when `import.meta.env.DEV` is
// true. It is the one place in this scene where a delivered `turn`, a delivered
// `serverSequence` and the raw event log are *rendered*, which is the bounded
// supersede of `TASK-210` §9 that D-1 records. Nothing else about the diagnostic
// boundary changes: transport state, the SignalR status, the battle id and the
// RNG seed/state are still rendered nowhere, by this panel or by any other line.
//
// **Why this band.** The board's fixed logical extent is x 395–885, y 120–610
// (`BOARD_ORIGIN_X` / `BOARD_ORIGIN_Y` / `BOARD_WIDTH`). Everything else in the
// HUD lives in the boss band above the board, in the column to its left, or in
// the interaction strip below it, so the band to the *right* of the board is the
// only free rectangle large enough for a readable panel. The panel is inset by
// `HUD_INSET` from the safe area's right edge, exactly as the left column is,
// and it stops at the board's own top and bottom edges — so it can never reach a
// board cell, a cast control, another HUD line, or the 24 px safe-area margin.

/** The panel's left edge: `HUD_INSET` clear of the board's right edge (885). */
const MOVE_LOG_LEFT = BOARD_ORIGIN_X + BOARD_WIDTH + HUD_INSET;
/** The panel's right edge, inset from the safe area exactly as the HUD column is. */
const MOVE_LOG_RIGHT = SAFE_AREA.x + SAFE_AREA.width - HUD_INSET;
const MOVE_LOG_WIDTH = MOVE_LOG_RIGHT - MOVE_LOG_LEFT;
/** The panel spans exactly the board's own rows, so it reaches neither band. */
const MOVE_LOG_TOP = BOARD_ORIGIN_Y;
const MOVE_LOG_HEIGHT = BOARD_WIDTH;
/** The caption row, and the first row line beneath it. */
const MOVE_LOG_BODY_TOP = MOVE_LOG_TOP + 20;
/** The widest a wrapped row may draw, inside the panel's own border. */
const MOVE_LOG_WRAP_WIDTH = MOVE_LOG_WIDTH - 8;
const MOVE_LOG_FONT_SIZE = '10px';
/**
 * The vertical pitch of one row, and the wrapped lines it may occupy.
 *
 * A row is `formatMoveLogRow`'s output: a `Match` or a `PowerChanged` line can
 * exceed the panel's width, so each row is given a fixed two-line box. A fixed
 * pitch is what keeps every row's position a property of the layout rather than
 * of the rendered text metrics, so the panel's geometry is observable without a
 * font measurement and no row can ever draw over the next one.
 */
const MOVE_LOG_ROW_LINES = 2;
const MOVE_LOG_ROW_HEIGHT = 26;

/**
 * How many rows the panel shows, and how many it retains.
 *
 * The visible window is always the **newest** rows (`scrollMoveLogToNewest`),
 * which is the panel's auto-scroll; the retained buffer is larger, so a resolved
 * resolution can still be read after the next one pushed its rows, and the
 * oldest rows are evicted once the cap is reached.
 */
const MOVE_LOG_VISIBLE_ROWS = 18;
const MOVE_LOG_CAP = 200;

/**
 * The documented Power range's upper bound (`GAME_RULES.md` §12,
 * `COMBAT_RULES.md` §1.1).
 *
 * It is a **presentation scale for that documented invariant, not state**: no
 * `maxPower` member exists on the wire and none is invented here
 * (`SIGNALR_PROTOCOL.md` §4.3 item 15). The delivered `petState.power` is what is
 * displayed; this constant only decides the "of what" it is shown against.
 */
const POWER_RANGE_MAX = 100;

/** Gauge presentation: a dark bordered track with a semantic fill. */
const GAUGE_TRACK_COLOR = 0x1e293b;
const GAUGE_TRACK_BORDER = 0x475569;
const BOSS_HP_GAUGE_COLOR = 0xef4444;
const PET_HP_GAUGE_COLOR = 0x22c55e;
const POWER_GAUGE_COLOR = 0xa855f7;
const PASSIVE_GAUGE_COLOR = 0x38bdf8;

/**
 * Floating combat numbers: how far one rises. Its lifetime is `FLOATER_LIFE_MS`
 * in the pacing block below, which is also what keeps it inside the phase that
 * spawned it.
 */
const FLOATER_RISE = 30;

/** Colours for the transient combat feedback (floaters and the callout). */
const DAMAGE_TO_BOSS_COLOR = '#f87171';
const DAMAGE_TO_PET_COLOR = '#fb923c';
const POWER_GAIN_COLOR = '#a78bfa';
const POWER_LOSS_COLOR = '#fb7185';

/**
 * The cast controls' two presentations: the caption colour an enabled control
 * draws, and the dimmed one a control held by the guard draws.
 *
 * Presentation only — neither colour carries a gameplay meaning, and the control's
 * own responsiveness is its handler's, not its colour's.
 */
const CAST_CONTROL_ENABLED_TEXT = '#e2e8f0';
const CAST_CONTROL_DISABLED_TEXT = '#475569';

/**
 * One drawn cast control: the tile, its caption, the styling its enabled
 * presentation restores, and the handler it carries while input is permitted.
 *
 * The set is kept so the guard's changes toggle the controls **in place** instead
 * of destroying and recreating them (`presentCastControlsEnabled`).
 */
interface CastControl {
  readonly tile: Phaser.GameObjects.Rectangle;
  readonly label: Phaser.GameObjects.Text;
  readonly enabledFill: number;
  readonly enabledStroke: number;
  readonly onTrigger: () => void;
}

/**
 * The MVP damage party identifiers (`SIGNALR_PROTOCOL.md` §3.2.14 item 3).
 *
 * They are read as delivered to decide *which panel* a damage floater is drawn
 * over. The mapping is presentation only: the scene applies no damage, derives
 * no HP from `amount`, and treats an unrecognized party as "no anchor here"
 * rather than guessing a side. The pair itself is owned by
 * `BattleEventPresenter`, so the party vocabulary has one spelling.
 */
const PLAYER_PARTY = DAMAGE_PARTY_PLAYER;
const BOSS_PARTY = DAMAGE_PARTY_BOSS;

/**
 * The battle presentation's **timing policy** — every display duration the
 * timeline uses, in milliseconds, in one frozen block.
 *
 * **One rule governs it: a phase's hold covers the animations that phase itself
 * triggers** (TASK-246 §5). The policy declares *effect* lifetimes (how long one
 * transient animation lives) and one shared phase beat; every *phase hold* is
 * then **derived** from them as the longest effect the phase can start, plus
 * `beatMs` (`phaseHoldMs`/`phaseEffectMs`). A hold can therefore never be shorter
 * than the feedback it starts, and no effect outlives the phase that started it —
 * and because `advancePresentation` also waits on the animations a phase actually
 * started (`trackPhaseAnimation`), the relationship is enforced structurally, not
 * only by this arithmetic.
 *
 * ```text
 * group        field              what it is                             phase
 * swap         exchangeMs         the committed exchange tween           swap
 *              markMs             a delivered coordinate marker           swap
 * match        highlightMs        a MatchCreated cell flash               match/cascade
 * gem          highlightMs        a GemMatched cell flash                 match/cascade
 *              fallMs             one delivered Fall travel               gravity-drop
 *              refillMs           one re-label / spawn entrance           refill-spawn/settle
 * gauge        travelMs           one HUD gauge travel                    damage/retaliation
 * floater      lifeMs             one floating number                     damage/feedback
 * callout      holdMs + fadeMs    the one batch callout                   boss-skill
 * banner       enter+hold+exit    the turn banner's three beats            banner
 * beatMs       —                  the beat every hold ends with            every phase
 * ```
 *
 * **The readability floors are deliberate.** Boss-turn actions and damage
 * feedback each get a hold long enough to be *read* rather than merely noticed:
 * a Fall/Refill that lands faster than the eye can follow is indistinguishable
 * from the board jumping. A typical ordinary resolution still stays well inside
 * 1.5× the holds TASK-246 §5 calibrated (the machine's own regression test pins
 * that bound), so this is a corrected pace, not a blanket slowdown.
 *
 * These are display timings and nothing else: no gameplay value, order, or
 * outcome depends on them, they are never sent or read from the wire, and a
 * scene without a tween manager (a unit harness) plays every phase immediately in
 * the delivered order — which is what keeps every ordering assertion observable
 * without a clock. They exist so a resolution reads as a sequence rather than as
 * one frame.
 */
const PRESENTATION_TIMING = Object.freeze({
  /** The beat a phase is held *after* the animation it triggered has finished. */
  beatMs: 80,
  swap: Object.freeze({ exchangeMs: 300, markMs: 300 }),
  match: Object.freeze({ highlightMs: 400 }),
  gem: Object.freeze({
    highlightMs: 340,
    fallMs: 260,
    refillMs: 280,
    /** How far above its destination cell a re-labelled view enters from. */
    refillEnterPx: 12,
    /** How far above its delivered cell a spawned Gem enters from. */
    spawnEnterPx: 34,
  }),
  gauge: Object.freeze({ travelMs: 260 }),
  floater: Object.freeze({ lifeMs: 460 }),
  callout: Object.freeze({ holdMs: 460, fadeMs: 260 }),
  banner: Object.freeze({
    enterMs: 180,
    holdMs: 420,
    exitMs: 200,
    /** The scale the label enters from; it settles at 1 over `enterMs`. */
    enterScale: 0.85,
    fontSize: '26px',
  }),
});

/**
 * The policy's own fields, projected to the names the call sites read.
 *
 * They are projections, not a second declaration: the value exists exactly once
 * (`PRESENTATION_TIMING`), and every hold is derived from it by
 * `phaseHoldMs`/`phaseEffectMs` rather than restated per event handler
 * (`TASK-275` §6.6: durations centralized, not scattered).
 */
const SWAP_TWEEN_MS: number = PRESENTATION_TIMING.swap.exchangeMs;
const MATCH_HIGHLIGHT_MS: number = PRESENTATION_TIMING.match.highlightMs;
const GEM_HIGHLIGHT_MS: number = PRESENTATION_TIMING.gem.highlightMs;
const GEM_REFILL_MS: number = PRESENTATION_TIMING.gem.refillMs;
const GAUGE_TWEEN_MS: number = PRESENTATION_TIMING.gauge.travelMs;
const FLOATER_LIFE_MS: number = PRESENTATION_TIMING.floater.lifeMs;
const CALLOUT_HOLD_MS: number = PRESENTATION_TIMING.callout.holdMs;
const CALLOUT_FADE_MS: number = PRESENTATION_TIMING.callout.fadeMs;
const PHASE_SETTLE_MS: number = PRESENTATION_TIMING.beatMs;
const GEM_REFILL_ENTER: number = PRESENTATION_TIMING.gem.refillEnterPx;
const GEM_FALL_MS: number = PRESENTATION_TIMING.gem.fallMs;
const GEM_SPAWN_ENTER: number = PRESENTATION_TIMING.gem.spawnEnterPx;

/**
 * The swap step's **coordinate marker** (TASK-264): the colour and translucency
 * one of the two §1.0 cells a delivered board action named is marked with, when
 * the step cannot present the exchange itself — when the contents at those cells
 * on screen are not the Gems the action exchanged.
 *
 * Its lifetime is `swap.markMs`, deliberately below the swap phase's own hold
 * (`SWAP_TWEEN_MS` + `PHASE_SETTLE_MS`), so the phase that starts it still
 * outlasts it (TASK-246 §5), and it is drawn as a translucent fill with the same
 * colour's border so it is distinct from a Match's white flash and a Gem's blue
 * one (`spawnCellHighlight`'s other two call sites).
 */
const SWAP_MARK_MS = PRESENTATION_TIMING.swap.markMs;
const SWAP_MARK_COLOR = 0xf87171;
const SWAP_MARK_FILL_ALPHA = 0.2;

/**
 * The turn banner (TASK-275): one presentation-only `PLAYER TURN` / `BOSS TURN`
 * marker, played as its own phase at each board cycle's own delivered boundary.
 *
 * ```text
 * source          the `actor` member of a delivered `SwapStarted`
 *                 (`SIGNALR_PROTOCOL.md` §3.2.26 item 2) — `"player"` or `"boss"`
 * enter → hold → exit
 *                 three beats: the label fades and scales in over
 *                 `TURN_BANNER_ENTER_MS`, is held for `TURN_BANNER_HOLD_MS`, and
 *                 fades out over `TURN_BANNER_EXIT_MS`
 * ```
 *
 * **It is a display of a delivered member and nothing else.** The server sends no
 * turn, phase, or ownership signal of its own: `TurnStarted`/`TurnEnded` are
 * defined in `GAME_EVENTS.md` §2 but are **not** in the closed wire discriminator
 * set and are never emitted (`SIGNALR_PROTOCOL.md` §3.2.2 item 2), so the banner
 * never claims a turn number, never names a phase the batch did not deliver, and
 * never adds a gameplay event or a sequence increment. It is drawn only where a
 * delivered `SwapStarted` opens a board cycle, and only for the side that member
 * names; a batch whose boundary names no recognized side (or delivers none at
 * all) plays no banner (`AGENTS.md` §10).
 *
 * It is a transient overlay in the feedback layer — **not** a new HUD region and
 * not a second notification system: it is created for its phase and destroyed
 * with it, exactly like a Match highlight (TASK-218A's "no new HUD region, no
 * second notification system" boundary is kept).
 *
 * The three beats are declared in the timing policy, together, so a phase's total
 * hold is derived from them by one expression (`phaseEffectMs`) rather than
 * scattered through the event handlers (`TASK-275` §6.6: durations centralized,
 * not scattered).
 */
const TURN_BANNER_ENTER_MS = PRESENTATION_TIMING.banner.enterMs;
const TURN_BANNER_HOLD_MS = PRESENTATION_TIMING.banner.holdMs;
const TURN_BANNER_EXIT_MS = PRESENTATION_TIMING.banner.exitMs;
const TURN_BANNER_TOTAL_MS = TURN_BANNER_ENTER_MS + TURN_BANNER_HOLD_MS + TURN_BANNER_EXIT_MS;
const TURN_BANNER_ENTER_SCALE = PRESENTATION_TIMING.banner.enterScale;
const TURN_BANNER_FONT_SIZE = PRESENTATION_TIMING.banner.fontSize;
/** The label each delivered side is presented with (display text, not a wire value). */
const TURN_BANNER_TEXT: Readonly<Record<'player' | 'boss', string>> = Object.freeze({
  player: 'PLAYER TURN',
  boss: 'BOSS TURN',
});
const TURN_BANNER_COLOR: Readonly<Record<'player' | 'boss', string>> = Object.freeze({
  player: '#38bdf8',
  boss: CALLOUT_BOSS_COLOR,
});

/**
 * The selected-cell treatment: one persistent ring, drawn around the selected
 * cell for as long as it stays selected.
 *
 * It is **one** display object reused by every selection — created once per
 * scene run, moved to the selected cell, and hidden when nothing is selected —
 * so selecting a cell never allocates an object, never destroys one, and never
 * redraws the board (`renderSelection`, `TASK-246` §3).
 *
 * Its form is deliberately unlike every other board effect. It is an opaque,
 * hollow, 4 px white ring, and it is **never faded, never tweened, and never
 * destroyed** while the scene lives — the opposite of `spawnCellHighlight`'s
 * match/cascade overlays, which are translucent filled squares that fade to
 * alpha 0 and destroy themselves. It sits outside a cell's tile and label, which
 * it never touches, so a selected cell still reads as the Gem it holds; damage
 * feedback is a red or orange number over a HUD panel, off the board entirely.
 */
const SELECTION_RING_COLOR = 0xffffff;
const SELECTION_RING_STROKE = 4;
const SELECTION_RING_SIZE = CELL_SIZE + 4;

/**
 * One HUD gauge: a bordered track plus the fill whose width is the *visual*
 * proportion of `value / max`.
 *
 * The fill is presentation only. Neither rectangle is state, nothing reads them
 * back, and the authoritative numbers are drawn as text beside the track.
 */
interface HudGauge {
  readonly fill: Phaser.GameObjects.Rectangle;
  readonly x: number;
  readonly y: number;
  readonly width: number;
  readonly height: number;
  /**
   * The fill width currently on screen, as the gauge tween drives it.
   *
   * It is the animated *proportion* `value / max` — the same presentation
   * ratio the fill is resized from — and never a delivered number: it holds no
   * HP, no Power, and no value any rule is evaluated against. It exists so a
   * gauge change is a visible travel to the delivered value rather than an
   * instant jump, and so a newer delivered value can retarget the same travel.
   */
  readonly fillState: { width: number };
}

/**
 * One rendered board cell: the persistent tile + label pair a Gem is drawn
 * with, plus the delivered content it currently shows.
 *
 * A view belongs to the cell it occupies — the PD-8 pairing's destination for
 * it — and it survives every authoritative push, so a resolution moves and
 * relabels the existing views instead of destroying and recreating 64 cells.
 * `content` is a presentation key of the delivered `gemType` currently rendered;
 * it is compared only to decide whether a cell's content changed, and is never
 * read as a gameplay value.
 *
 * `cleared` records that a **delivered** clear has removed this cell's content
 * during the timeline that is playing (TASK-275): the view is hidden and its
 * `content` emptied, and the phase that converges the board re-labels it from
 * the delivered cell — with the refill entrance — because a cleared view counts
 * as a content change. It is a presentation flag only: it is written by the
 * cells `MatchCreated.cells` / `GemMatched.cellIndex` name, never derived from a
 * board difference or a match rule, and it holds no Gem identity (the wire
 * carries none — `SIGNALR_PROTOCOL.md` §4.1).
 */
interface GemView {
  readonly tile: Phaser.GameObjects.Rectangle;
  readonly label: Phaser.GameObjects.Text;
  content: string;
  cleared: boolean;
}

/**
 * One ordered phase of a resolution's presentation timeline.
 *
 * The phase list is built from one delivered `ReceiveEvents` batch
 * (`SIGNALR_PROTOCOL.md` §3) and played in the batch's own order — the client
 * never reorders, filters, or re-derives it. Each phase carries the delivered
 * events it presents, so a phase is a *grouping* of what the server sent and
 * never a second event stream.
 *
 * `actor` and `action` come from the single delivered-event adapter
 * (`BattleEventPresenter.adaptPresentedEvent`), so a Swap, a Boss Skill, a Boss
 * Passive and a Card / Pet-Skill cast carry the same two members and reach their
 * phases through the same call (`AGENTS.md` §9). `actor` is `null` when the
 * delivered members name no actor this contract defines; it is never guessed
 * (`AGENTS.md` §10).
 */
interface PresentationPhase {
  readonly kind: PresentationPhaseKind;
  readonly actor: PresentationActor | null;
  readonly action: PresentationAction;
  /** The developer-diagnostic line this phase records when it plays. */
  readonly detail: string;
  /** How long the phase is held before the next one begins. */
  readonly holdMs: number;
  /** The delivered events this phase presents, in delivery order. */
  readonly events: readonly InBattleServerEvent[];
  /**
   * Whether this phase's **board-mutating** steps may be applied to the board the
   * client renders.
   *
   * It is `true` exactly when the board cycle this phase belongs to started from
   * the board the scene is showing, which is one question with three answers
   * (`AGENTS.md` §10 — the client claims an exchange only where it can establish
   * that the action was validated against the board on screen):
   *
   * ```text
   * delivered `SwapStarted.actor === "player"`
   *                        the round's first board action, validated against the
   *                        committed board the state push delivered (ADR-026 §2)
   * no delivered boundary at all
   *                        the exchange is this client's own committed pair, on the
   *                        board on screen
   * a delivered Boss boundary whose cycle this batch's own delivered deltas replay
   * onto the round's committed board
   *                        the Boss's own step-18a action is resolved by the same
   *                        Match-3 pipeline as the Player's, on the board the
   *                        Player's cycle left (ADR-026 §2) — and once the Player's
   *                        own cycle has been applied that *is* the board on screen,
   *                        so the cycle is placeable (`replayBoardDeltas`)
   * ```
   *
   * It is `false` for every other boundary: the Boss boundary whose cycle does not
   * replay onto the round's committed board, which is the one case the contract
   * cannot place — the unreported deadlock reshuffle replaced the board with one
   * the client has never held (`ADR-026` §8, `GAME_EVENTS.md` §3 item 8) — and an
   * `actor` word outside the contract's two values, which names no side.
   *
   * A cycle whose starting board the client does not hold may **not** have that
   * cycle's clears, Fall deltas or Refill placements applied to the rendered
   * board: those deltas are true of the server's pass board, not of the board on
   * screen, so applying them would remove Gems the server confirmed at other
   * cells, move views between coordinates the report never described, and leave a
   * cell showing an asset no delivered event ever placed there.
   *
   * The cycle is still **reported**: its banner, its coordinate marker, its cell
   * highlights, its callout and its combat stages all play as delivered. What is
   * withheld is the local board mutation, and the round's single `settle` phase
   * converges the board on the round's committed
   * authoritative board (`SIGNALR_PROTOCOL.md` §3.1 items 1–3, §3.2.28 item 8:
   * a client may use the delivered deltas only to present the pass they report —
   * never to author a board).
   */
  readonly boardBacked: boolean;
  /**
   * The batch's single player-facing callout, when the event that callout was
   * selected from is one of this phase's own delivered events.
   *
   * The callout is a presentation of a *delivered* event, so it is raised by the
   * phase that presents that event rather than at batch arrival: a callout can
   * therefore never be on screen before the step it names has been presented
   * (TASK-254 AF-2). It is the same one line the batch always had — one callout
   * per resolution, selected by the same importance ladder — only with a causal
   * place in the timeline (`selectBatchCalloutEvent`).
   */
  readonly callout?: EventCallout;
}

/**
 * The scene's presentation-orchestration machine (TASK-252).
 *
 * It replaces the three independent booleans that used to describe the guard
 * (`swapPending`, `actionInFlight`, `presentationTimeline`) with one named
 * state, so "locked ⟺ not accepting player input" is a single derived fact
 * (`isInputLocked`).
 *
 * ```text
 * idle                    no action is outstanding and no timeline is playing: the
 *                         scene accepts player input
 * submitted               an action this scene submitted has not finished being
 *                         presented, and no timeline is playing — either its
 *                         acknowledgement is still unanswered, or the
 *                         acknowledgement has settled and its resolution's batch
 *                         is still to arrive (`SIGNALR_PROTOCOL.md` §3.1: the two
 *                         travel on separate paths, so either order is legal)
 * presenting              a resolution's timeline is playing and no request is unanswered
 * presenting-submitted    both: the timeline plays while its request is still unanswered
 *                         (`SIGNALR_PROTOCOL.md` §3.1 — the state is written back and
 *                         the batch is sent before the invocation result returns, so
 *                         the batch normally arrives first)
 * terminated              the battle's terminal outcome has been handed to
 *                         `ResultScene`; the scene presents no further resolution
 * ```
 *
 * `submitted` deliberately spans both the unanswered request and the
 * acknowledgement-settled-but-resolution-pending interval (TASK-254 AF-5): that
 * is what keeps the guard engaged from submission through presentation and
 * reconciliation, in either delivery order.
 */
export type BattleOrchestrationState =
  | 'idle'
  | 'submitted'
  | 'presenting'
  | 'presenting-submitted'
  | 'terminated';

/**
 * The machine's legal transitions, and the only ones the scene performs.
 *
 * ```text
 * idle                 → submitted             the player submitted an action
 * idle                 → presenting            a delivered batch started a timeline
 * idle                 → terminated            a delivered batch handed the outcome off
 * submitted            → idle                  the request settled (or was rejected) with no timeline
 * submitted            → presenting            the request settled, then its batch started a timeline
 * submitted            → presenting-submitted  its batch started a timeline while the request was unanswered
 * submitted            → terminated            its batch carried the terminal outcome
 * presenting           → idle                  the timeline completed, aborted, or was superseded
 * presenting           → terminated            the terminal outcome was handed off
 * presenting-submitted → submitted             the timeline ended; the request is still unanswered
 * presenting-submitted → presenting            the request settled; the timeline plays on
 * presenting-submitted → terminated            the terminal outcome was handed off
 * ```
 *
 * A superseding batch leaves `presenting` and re-enters it through the aborted
 * timeline's own end, so no state lists itself: a call naming the current state
 * is not a transition at all.
 *
 * `terminated` has no outgoing transition: the battle is over and the scene is
 * on its way to `ResultScene`. The scene's *lifecycle* resets the machine to
 * `idle` (`create()` / `shutdown()`), which is the machine's initial value for a
 * scene run rather than a transition between orchestration states.
 */
export const BATTLE_ORCHESTRATION_LEGAL_TRANSITIONS: Readonly<
  Record<BattleOrchestrationState, readonly BattleOrchestrationState[]>
> = Object.freeze({
  idle: Object.freeze(['submitted', 'presenting', 'terminated'] as const),
  submitted: Object.freeze(['idle', 'presenting', 'presenting-submitted', 'terminated'] as const),
  presenting: Object.freeze(['idle', 'terminated'] as const),
  'presenting-submitted': Object.freeze(['submitted', 'presenting', 'terminated'] as const),
  terminated: Object.freeze([] as const),
});

/** The states in which the scene holds player input. */
const ORCHESTRATION_LOCKED_STATES: readonly BattleOrchestrationState[] = Object.freeze([
  'submitted',
  'presenting',
  'presenting-submitted',
]);

/**
 * The documented cap on each diagnostic buffer (`presentedEventsLog`,
 * `presentedPhases`).
 *
 * Both are developer/testing records and are never rendered (TASK-209 §5); they
 * grow for the life of the scene, so they are bounded and evict oldest-first.
 * They are also cleared when the delivered battle changes and on teardown.
 */
const PRESENTATION_LOG_CAP = 256;

/**
 * The phase kinds that start no animation at all, and are therefore held for no
 * time at all.
 *
 * The terminal outcome is the only one (§3.2.19 item 1; TASK-246 finding L-1): it
 * hands the battle off and is padded with nothing.
 *
 * The delivered-action kinds (`cast` / `relic` / `boss-skill`) are deliberately
 * **not** here (TASK-254 AF-1/AF-4). A phase's hold is derived from the events
 * that phase actually carries (`phaseHoldMs` → `phaseEffectMs`), and a
 * delivered-action step can carry — and start — the readouts and floaters of
 * events delivered alongside it, and the `boss-skill` step presents its own
 * delivered `BossSkillCast`. Holding such a phase for zero time would let its
 * own effect still be animating when the next, conflicting phase begins, which is
 * exactly the overlap this rule forbids: a hold is never shorter than the longest
 * animation its phase starts.
 */
const ZERO_HOLD_PHASE_KINDS: readonly PresentationPhaseKind[] = Object.freeze(['outcome']);

/**
 * The presentation key of one delivered cell: its Gem type
 * (`SIGNALR_PROTOCOL.md` §4.1 item 5, `GAME_STATE.md` §2.1.3).
 *
 * It answers "is what this cell renders the same as what the server now
 * delivers" and nothing else — no rule, matchup, or gameplay meaning is read
 * from it.
 *
 * The **one** spelling of that key: `gemContentKey` projects a delivered board
 * cell, and `gemContentKeyFor` projects the `gemType` an event names. A delivered
 * clear is compared against the same key, so "the cell this event confirms was
 * cleared" and "the content this view renders" are stated in one vocabulary and
 * can never drift apart.
 */
function gemContentKeyFor(gemType: string): string {
  return gemType;
}

function gemContentKey(cell: RuntimeCell): string {
  return gemContentKeyFor(cell.gemType);
}

/**
 * Placeholder presentation of the four Gem types (`MATCH3_RULES.md` §1.1:
 * `ATK`, `DEF`, `HP`, `POWER`).
 *
 * Keys are the documented contract names the server sends; the value is a
 * placeholder fill colour and short label. This is display only — it assigns no
 * gameplay meaning, and an unrecognised name is rendered as an inert
 * placeholder rather than being guessed at.
 */
const GEM_PRESENTATION: Readonly<Record<string, { readonly color: number; readonly label: string }>> = {
  ATK: { color: 0xef4444, label: 'ATK' },
  DEF: { color: 0x3b82f6, label: 'DEF' },
  HP: { color: 0x22c55e, label: 'HP' },
  POWER: { color: 0xa855f7, label: 'PWR' },
};

/**
 * BattleScene — the battle presentation runtime.
 *
 * It presents the Board Foundation State the server pushed
 * (`BattleStateUpdated`, SIGNALR_PROTOCOL.md §4) as an 8 x 8 board of 64 cells:
 *
 *   BattleStateUpdated → SignalRService → GameRuntime → BattleScene → 8x8 board
 *
 * The scene reads the runtime through the `GameRuntime` port and never imports
 * SignalR, `HubConnection`, or any transport type (ARCHITECTURE.md §2.2 rule 3).
 *
 * The board it draws is the server's authoritative `Cells[64]`, presented
 * verbatim. The scene computes no board content of its own: it does not
 * generate, fill, repair, validate, or re-derive the board, and it contains no
 * client-side randomness (SIGNALR_PROTOCOL.md §4 item 10, `GAME_RULES.md` §18,
 * ADR-001). Its only transformation is the documented index → screen mapping.
 *
 * **One presentation lifecycle for every action (TASK-254).** One delivered
 * `ReceiveEvents` batch is one resolved action (`SIGNALR_PROTOCOL.md` §3.1), and
 * the scene presents it as one ordered timeline, in the batch's own order,
 * through one lifecycle whatever the actor is:
 *
 * ```text
 * Action            the delivered action's own step: the committed Swap's
 *                   exchange, a Card / Pet-Skill cast, a Relic trigger, a Boss
 *                   Skill cast
 * Execution         the board cycle the delivered Match / Gem events report
 * Board Resolution  the gravity/refill interpolation onto the authoritative board
 *                   (PD-1/PD-8) — one step, where the board cycle ends, and only
 *                   when this resolution delivered a board cycle at all
 * Cascades          one step per delivered `CascadeCreated.cascadeDepth`, with
 *                   that pass's own delivered Match / Gem feedback
 * Combat Effects    the delivered damage instances, the Boss's own steps, and the
 *                   readouts / floaters their delivered events own
 * Recovery          the board and every timeline-owned readout converged on the
 *                   delivered state, once the last phase has played (an abort or
 *                   a supersession converges the same way)
 * Next Actor        the guard released, and the scene accepting player input again
 * ```
 *
 * Every one of those steps is a *grouping* of delivered events (`buildPresentationPhases`),
 * every actor/action on them comes from the one delivered-event interpreter
 * (`adaptPresentedEvent`), a stage with no delivered event is skipped rather than
 * fabricated, and each phase is held for at least the longest animation its own
 * events start (`phaseHoldMs`). The player's input is locked for the whole of a
 * resolution — from submission through presentation and Recovery (PD-2). Nothing
 * in the timeline computes a gameplay value, and every value it presents — the
 * board's content, the HP pairs, the damage numbers — is read from what the
 * server delivered.
 *
 * **Swap input.** The scene implements the documented Match-3 interaction
 * (`MATCH3_RULES.md` §2 item 1): a tap on a cell selects it, and a tap on a
 * second cell submits the pair through the runtime port —
 *
 *   select cell A → select cell B → GameRuntime.requestAction(Swap)
 *     → SignalRService.swap → BattleHub.Swap
 *
 * The selection is presentation-local state. The scene does not decide whether
 * the swap is legal, whether it produces a match, or what it resolves to: that
 * is `MATCH3_RULES.md` §2.1.2's server-side validation. Its only local checks
 * are interaction feedback — a tap outside the board is ignored, and a second
 * tap on the same cell clears the selection instead of submitting — which
 * neither alters the request shape nor substitutes for the server's answer
 * (`SIGNALR_PROTOCOL.md` §2.1 item 1).
 *
 * It is created with the `GameRuntime` as scene data by `GameConfig`. When no
 * runtime is supplied it still runs, reporting that the runtime is unavailable,
 * so scene lifecycle never depends on runtime availability.
 */
export class BattleScene extends Phaser.Scene {
  private runtime: GameRuntime | null = null;
  private runtimeUnsubscribe: (() => void) | null = null;
  private battleStateUnsubscribe: (() => void) | null = null;
  private battleEventsUnsubscribe: (() => void) | null = null;
  /**
   * The player-facing connection line — a short statement of whether the battle
   * is connected, reconnecting, or unavailable. It reports the connection only:
   * no hub name, no connection id, no battle id, no sequence, and no RNG value.
   * The runtime's synchronization status is a player-facing state of its own and
   * has its own line (`syncText`, `SYNC_INDICATOR_*`), not a diagnostic flag on
   * this one.
   */
  private connectionText: Phaser.GameObjects.Text | null = null;
  /**
   * The player-facing synchronization indicator (`SYNC_INDICATOR_*`), or `null`
   * before the shell is drawn and after teardown.
   *
   * One line, derived from `GameRuntimeState.sync` alone (`renderSyncIndicator`):
   * it is visible exactly while the runtime reports `unsynchronized`, and hidden
   * for `synchronized` and `awaiting_battle`. It holds no battle value, takes no
   * input, and is never on the board's own input path.
   */
  private syncText: Phaser.GameObjects.Text | null = null;
  /** The Boss's display name, resolved from the client catalog by `bossState.bossId`. */
  private bossNameText: Phaser.GameObjects.Text | null = null;
  /** Whether the Boss is currently in Enraged state according to authoritative HP and threshold. */
  private bossEnraged: boolean = false;
  /** Authoritative battle ID of the current battle for Enrage latch lifecycle tracking. */
  private currentBattleId: string | null = null;
  /** The Boss's authoritative `hp / maxHp` (SIGNALR_PROTOCOL.md §4.4). */
  private bossHpText: Phaser.GameObjects.Text | null = null;
  /**
   * The Boss's HP gauge — the *visual* proportion of the same two delivered
   * numbers. It is presentation only: it holds no HP, is never read back, and
   * the authoritative values are also drawn as text beside it.
   */
  private bossHpGauge: HudGauge | null = null;
  /**
   * The active Pet's name, resolved from the client loadout/catalog.
   *
   * `PetState` carries no client-visible identity (ADR-014 excludes the Pet
   * instance identity from the wire), so the Pet's identity comes from the
   * existing preserved pre-battle loadout (ADR-022) plus the owned-Pet collection
   * read (`GameRuntimePort.getPets()`), exactly as the Lobby presents it. It is
   * presentation content only — the Pet's HP and Power are the authoritative
   * `petState` values, never this name's.
   */
  private petNameText: Phaser.GameObjects.Text | null = null;
  /** The active Pet's authoritative `hp / maxHp` (SIGNALR_PROTOCOL.md §4.3 item 15). */
  private petHpText: Phaser.GameObjects.Text | null = null;
  /** The active Pet's HP gauge — the visual proportion of the delivered pair. */
  private petHpGauge: HudGauge | null = null;
  /** The active Pet's authoritative Power (SIGNALR_PROTOCOL.md §4.3 item 15). */
  private petPowerText: Phaser.GameObjects.Text | null = null;
  /** The Power gauge — the delivered value drawn against the documented range. */
  private powerGauge: HudGauge | null = null;
  /**
   * The active Pet's delivered Passive identity and its `current / threshold`
   * progress pair (SIGNALR_PROTOCOL.md §4.3 items 3–5) — presented verbatim.
   */
  private petPassiveText: Phaser.GameObjects.Text | null = null;
  /** The Passive's `current / threshold` readout, beside its progress gauge. */
  private petPassiveProgressText: Phaser.GameObjects.Text | null = null;
  /** The Passive's progress gauge — how far the delivered pair has got. */
  private passiveGauge: HudGauge | null = null;
  /**
   * The active Pet's delivered Status Effect instances (SIGNALR_PROTOCOL.md §4.3
   * item 14) — presented verbatim, with the empty collection shown as the
   * documented no-effect case.
   */
  private petStatusText: Phaser.GameObjects.Text | null = null;
  /** The delivered Combo, shown when it is meaningful (MATCH3_RULES.md §6). */
  private comboText: Phaser.GameObjects.Text | null = null;
  /** The delivered cumulative Match count (GAME_RULES.md §3). */
  private matchesText: Phaser.GameObjects.Text | null = null;
  /**
   * The transient combat callout: the one player-facing statement of what just
   * happened (a Match, a Combo, a cast, a Boss action, a Passive trigger).
   *
   * It is set from a delivered event, holds no battle state, and fades on its own;
   * it never blocks input and never becomes a second event log.
   */
  private calloutText: Phaser.GameObjects.Text | null = null;
  /** The board area's message — the state-pending case only. */
  private boardMessageText: Phaser.GameObjects.Text | null = null;
  /** Board-area message shown before the server has pushed any battle state. */
  private boardMessage = '';
  /** Swap input / acknowledgement feedback. */
  private swapText: Phaser.GameObjects.Text | null = null;
  /** Feedback text area presenting card/skill cast transport feedback. */
  private castText: Phaser.GameObjects.Text | null = null;
  /** Visual layer for transient event highlights and floating combat text. */
  private feedbackLayer: Phaser.GameObjects.Container | null = null;
  /**
   * The persistent board layer — the container the 64 gem views live in.
   *
   * It is created once per scene run and is the board's input target; the views
   * inside it are persistent too, so an authoritative push moves and relabels
   * them rather than emptying and refilling the container.
   */
  private boardLayer: Phaser.GameObjects.Container | null = null;
  /** Interactive cast controls container for equipped cards and signature skill. */
  private castControlsLayer: Phaser.GameObjects.Container | null = null;
  /**
   * The cast controls currently drawn, so the guard's own changes are presented
   * by toggling them rather than by rebuilding the set (D-2).
   */
  private castControls: CastControl[] = [];
  /**
   * The first selected cell's §1.0 index, or `null` when nothing is selected.
   *
   * Presentation-local input state only — it is not board state and is never
   * sent as anything but the two cells of a Swap request.
   */
  private selectedCell: number | null = null;
  /**
   * The persistent selected-cell ring (`SELECTION_RING_*`), or `null` before the
   * shell has been drawn and after teardown.
   *
   * It is the board's own visible statement that a cell is selected, and
   * `renderSelection` drives it from `selectedCell` alone. There is no object
   * per cell: one ring is moved onto the selected cell, and hidden whenever
   * nothing is selected — a board the delivered state no longer carries, a cell
   * a resolution cleared, or a scene that has ended.
   */
  private selectionRing: Phaser.GameObjects.Rectangle | null = null;
  /**
   * The presentation-orchestration machine's current state (TASK-252).
   *
   * It is the scene's **single** statement about whether it is accepting player
   * input: `isInputLocked()` is exactly "this state holds input", so the guard
   * and the interaction indicator can never disagree. It is moved only by
   * `transitionOrchestration`, which applies the documented legal transitions
   * (`BATTLE_ORCHESTRATION_LEGAL_TRANSITIONS`).
   */
  private orchestration: BattleOrchestrationState = 'idle';
  /**
   * The in-flight presentation timeline, or `null` when nothing is playing.
   *
   * The machine's `presenting` / `presenting-submitted` states describe *when*
   * this is set; the phase list itself stays here so an aborted timeline can be
   * dropped whole.
   */
  private presentationTimeline: PresentationPhase[] | null = null;
  /** The next phase to play in the in-flight timeline. */
  private presentationStep = 0;
  /**
   * The longest animation the phase **currently playing** has started, in
   * milliseconds — the structural half of the pacing rule.
   *
   * Every animation helper the phases use reports its own duration here
   * (`trackPhaseAnimation`), and `advancePresentation` holds the phase for
   * `max(holdMs, this)` before advancing. That is what makes "a phase cannot
   * complete before the animations it started" a property of the machine rather
   * than a coincidence of two constants agreeing: a Match presentation can never
   * begin while the exchange that produced it is still on screen, whatever a
   * future timing edit does to the declared holds.
   *
   * It is reset at every phase boundary and read immediately after that phase has
   * played, so it never carries a value across phases, and it is pure
   * presentation bookkeeping — no gameplay value is stored in it.
   */
  private phaseAnimationMs = 0;
  /**
   * The tween target the timeline's phase pacing runs on.
   *
   * It holds a presentation-only progress value — nothing reads it back, no
   * gameplay value is in it — and killing its tween is how an aborted timeline
   * stops advancing without disturbing any other animation.
   */
  private readonly phaseClock = { progress: 0 };
  /**
   * The persistent interaction indicator (`PHASE_TEXT_*`), or `null` before the
   * shell has been drawn and after teardown.
   *
   * One line, one value: `renderPhaseIndicator` writes the single statement
   * `phaseIndicator()` resolves, so two contradictory interaction states can
   * never be on screen at the same time.
   */
  private phaseText: Phaser.GameObjects.Text | null = null;
  /**
   * The kind of the timeline phase currently playing, or `null` when no phase is
   * playing.
   *
   * It is the local presentation timeline's own position — not a server phase.
   * `buildPresentationPhases` derives a `retaliation` phase from delivered
   * `DamageDealt` / `DamageTaken` parties alone (`resolveDamagePresentationPhase`),
   * so the indicator's `BOSS ATTACK` statement is backed by a delivered
   * `source="boss"` / `target="player"` event and by nothing else (TASK-246 §4).
   */
  private presentationPhaseKind: PresentationPhaseKind | null = null;
  /**
   * The terminal statement this resolution handed to `ResultScene`, or `null`.
   *
   * It is set immediately before the handoff — by `handOffOutcome`, so every
   * path that hands off (the timeline's last phase, its completion, an abort, a
   * supersession) sets it — which is what lets `VICTORY` / `DEFEAT` be presented
   * *before* the battle scene is replaced. The delivered
   * `BattleWon` / `BattleLost` outcome is the only thing it is derived from; it
   * is cleared by `create()` and by teardown, so a reused scene never shows the
   * previous battle's result.
   */
  private terminalStatement: InteractionStatement | null = null;
  /** The `serverSequence` of the batch the in-flight timeline is presenting. */
  private presentationSequence: number | null = null;
  /** The battle the in-flight timeline belongs to. */
  private presentationBattleId: string | null = null;
  /**
   * The newest `serverSequence` this scene has accepted a resolution for, or `null`.
   *
   * `serverSequence` is the post-resolution counter and is strictly increasing with no
   * gaps (`SIGNALR_PROTOCOL.md` §3 item 2; `GAME_STATE.md` §5), so a batch at or below
   * this value is a re-delivery or an older one and is a resolution the presentation
   * has already played. It is remembered **after** its timeline ends as well, which is
   * what stops an old round's animations from being replayed once a newer resolution
   * has been accepted (ADR-008: recovery is a snapshot, never an event replay). It is
   * dropped when the delivered battle changes, so a new battle starts at its own first
   * sequence — the same bookkeeping the developer Move Log keeps for its own rows.
   */
  private acceptedResolutionSequence: number | null = null;
  /** The battle {@link acceptedResolutionSequence} belongs to. */
  private acceptedResolutionBattleId: string | null = null;
  /**
   * The terminal outcome this resolution is handing off, or `null`.
   *
   * It is recorded as soon as the batch names one, so the handoff cannot be lost
   * by a phase boundary: the timeline's last phase performs it, and a timeline
   * that completes, errors, or is superseded performs it too. It is deliberately
   * cleared by scene teardown — a scene that is going away navigates nowhere.
   */
  private pendingOutcomeHandoff: {
    readonly outcome: string;
    readonly finalBossHp: number;
    readonly finalPlayerHp: number;
    readonly battleId: string;
  } | null = null;
  /**
   * The persistent gem views, indexed by the §1.0 cell each currently occupies.
   *
   * A resolution moves and relabels these; it never destroys and recreates them
   * (the previous redraw did, which is exactly what left nothing to animate).
   */
  private gemViews: Array<GemView | null> = [];

  /**
   * The gem views a delivered Fall step displaced and its Refill step has not yet
   * re-placed (`SIGNALR_PROTOCOL.md` §3.2.28).
   *
   * A fall lands a view on a cell whose own view the same step vacates, so exactly
   * one view is displaced per spawned Gem. Those views are pooled here and spent by
   * the refill step that closes the same pass, which is what keeps the board's 64
   * cells rendered by the same persistent display objects for the whole battle —
   * a resolution re-indexes and re-labels the views instead of destroying and
   * recreating them.
   *
   * The pool is transient presentation state and never a board: a view is only ever
   * held here between the two steps of one delivered transition, and
   * `renderBoard` drains it (reusing what a cell still needs) so an abort,
   * a recovery, or a teardown can leave nothing behind.
   */
  private refillableViews: GemView[] = [];
  /**
   * The authoritative sequence the rendered board was last converged from, or
   * `null` when no board has been rendered yet.
   *
   * It is the reference point for supersession and for deciding whether an
   * arriving push belongs to a resolution whose batch is still to come.
   */
  private settledBoardSequence: number | null = null;
  /** The battle the rendered board belongs to, so a new battle never pairs with it. */
  private settledBattleId: string | null = null;
  /**
   * The authoritative sequence whose delivered values are being held back for
   * its presentation timeline, or `null` when nothing is held.
   *
   * A resolution's state push is delivered *before* its event batch
   * (`SIGNALR_PROTOCOL.md` §3.1: the write-back happens first, then the batch is
   * sent), and the batch is what sequences the resolution. So while the client
   * is awaiting the resolution of its own action, the board's content and the two
   * HP panels' delivered values are held at their previous presentation and
   * released by the timeline — by the settle phase for the board, by the damage
   * and retaliation phases for the panels, and by the timeline's own end, an
   * abort, or a newer push for anything left over. Nothing is ever computed: the
   * held values are the delivered ones, released later rather than derived.
   */
  private heldSequence: number | null = null;
  /**
   * Whether an action request this client submitted has not yet been answered.
   *
   * The acknowledgement travels back on its own path, independently of the
   * resolution's state push and event batch (`SIGNALR_PROTOCOL.md` §3.1), so it
   * can arrive before them, after them, or between them. It is therefore its own
   * fact and **never** the guard's: a settled request says nothing about whether
   * its resolution has been presented yet (TASK-254 AF-5).
   */
  private submissionOutstanding = false;
  /**
   * Whether a resolution this client is presenting — or is still awaiting the
   * batch of — has yet to be presented **and** reconciled.
   *
   * It is the one fact the guard's *release* and the board hold both key on, in
   * place of "a request is still in flight" (TASK-254 AF-5/AF-6):
   *
   * ```text
   * set      when an accepted action is submitted, and when a delivered batch
   *          starts a timeline (a resolution this client presents)
   * held     while the timeline plays, and while an accepted action's batch is
   *          still to arrive — the acknowledgement-settled-first ordering
   * cleared  when the timeline completes, aborts or is superseded (all of which
   *          converge first), when the action is rejected or fails (which emits
   *          nothing at all: GAME_EVENTS.md §1.2), when a delivered state cannot
   *          be this resolution's (a recovery snapshot or a newer resolution,
   *          ADR-008), and on teardown
   * ```
   *
   * Only delivery facts set and clear it: no timer, no animation and no clock
   * takes part, so an authoritative gameplay outcome can never depend on it
   * (`AGENTS.md` §10).
   */
  private resolutionPending = false;
  /**
   * The presentation generation: the token that bounds in-flight presentation
   * work to the state it was started for.
   *
   * A recovery snapshot is authoritative and may arrive at any point of a
   * resolution — mid-cascade, mid-combat, or while a phase's pacing tween is
   * still holding. Everything the interrupted timeline had queued is cancelled
   * (its pacing tween, its gem-view travels, its transient effects), and this
   * counter is what makes the cancellation safe even for a callback that is
   * already in flight: `advancePresentation`'s pacing callback captures the
   * generation it was scheduled under and does nothing when it no longer
   * matches, so a stale phase can neither advance an abandoned timeline nor
   * release a newer action's lock.
   *
   * It is bumped by every path that replaces the state a timeline was presenting
   * — a recovery convergence, `create()` and `shutdown()` — and by the rejection
   * rollback, which restores the authoritative board. It is scene-local
   * bookkeeping and holds no battle value.
   */
  private presentationGeneration = 0;
  /**
   * The submission serial: the token that bounds one action request's
   * continuation to the submission it belongs to.
   *
   * An acknowledgement, a rejection, or a transport outcome can arrive after the
   * scene has moved on — after a recovery converged the state, after the battle
   * was replaced, or after the scene was torn down and re-created. The serial is
   * incremented when a submission is opened and by every path that invalidates
   * the outstanding ones (a recovery convergence, `create()`, `shutdown()`,
   * `clearActiveBattleState`-driven state replacement), and a continuation whose
   * serial — or whose battle id — no longer matches does nothing at all: it
   * cannot write feedback, cannot clear a newer gesture's committed pair, and
   * cannot release a lock that belongs to a different submission (`P0.3` §5,
   * `SIGNALR_PROTOCOL.md` §3.1).
   */
  private submissionSerial = 0;
  /**
   * The two §1.0 cells of the committed Swap this client has still to present,
   * or `null` when no committed Swap is outstanding.
   *
   * It is the interaction record of the player's own gesture — the same two
   * cells the request carried (`MATCH3_RULES.md` §2.1.1) — and it drives exactly
   * one presentation: the swap movement at the head of the resolution's
   * timeline.
   *
   * **It is dropped when that Swap's own phase plays, not when the request's
   * acknowledgement settles.** The acknowledgement and the resolution's batch
   * travel on different paths (`SIGNALR_PROTOCOL.md` §3.1: the state is written
   * back and the batch is sent before the invocation result returns), so a
   * timeline built from a batch that arrives after the acknowledgement has
   * settled would otherwise lose the swap movement entirely. A rejected Swap
   * emits no events at all (`GAME_EVENTS.md` §1.2), so the rejection and failure
   * paths drop it explicitly rather than relying on a timeline that never comes.
   */
  private committedSwapCells: { readonly from: number; readonly to: number } | null = null;
  /**
   * Developer/testing log of the presentation phases played, in order.
   *
   * Like `getPresentedEvents()`, it is **not rendered**: it is the readable
   * record that lets development and the test suite inspect the timeline's
   * order. It holds no gameplay value and nothing reads it back, and it is
   * bounded by `PRESENTATION_LOG_CAP`.
   */
  private presentedPhases: string[] = [];
  /**
   * Developer/testing log of the events presented so far, in delivered order.
   *
   * It is **not rendered**: the raw event feed (`Match: … depth: 0`,
   * `DamageCalculated: base …`) is developer console material, not a player HUD.
   * It is kept as a readable diagnostic for development and tests, which is why
   * it is still accumulated while `feedbackText` no longer exists. It is bounded
   * by `PRESENTATION_LOG_CAP`.
   */
  private presentedEventsLog: string[] = [];
  /**
   * The battle the two diagnostic buffers above belong to.
   *
   * A delivered state for another battle (or a cleared state) ends the battle
   * whose record they hold, so both are dropped then — a new battle starts with
   * an empty record rather than the previous one's.
   */
  private presentationLogBattleId: string | null = null;
  /**
   * The turn of the last battle state for which the player turn banner was presented.
   *
   * It bounds the banner to its turn: entering the player turn triggers the banner
   * before player input is required, and repeated state notifications or selections
   * during that turn do not duplicate it.
   */
  private lastPlayerTurnBannerTurn: number | null = null;
  /**
   * The battle identity the turn above belongs to.
   */
  private lastPlayerTurnBannerBattleId: string | null = null;
  /**
   * Currently active turn banner label, tracked to prevent overlapping banner tweens.
   */
  private activeTurnBanner: Phaser.GameObjects.Text | null = null;
  /**
   * Every transient effect the in-flight timeline has started and not yet had
   * released: board highlights and floating numbers.
   *
   * A phase starts them and `abortPresentation` cancels them, so an abandoned
   * resolution cannot animate over the next one. On a completed timeline they
   * have already destroyed themselves inside their own phase's hold, and the
   * references are simply dropped.
   */
  private timelineEffects: Phaser.GameObjects.GameObject[] = [];
  /**
   * Whether the callout currently on screen was raised by a presentation
   * timeline: the batch's own single callout, or the Boss Enrage callout a phase
   * presents.
   *
   * It is what bounds the callout to its own timeline: a callout raised outside a
   * timeline (a transport rejection) is left to fade on its own, while one a
   * timeline raised is cancelled with that timeline.
   */
  private timelineCalloutShown = false;
  /**
   * Card definition lookup populated via GameRuntimePort.getCards().
   *
   * It is the delivered `GET /api/cards` read — the Player's **unlocked** Card
   * definitions (`API_CONTRACTS.md` §5.3) — used for one presentation lookup:
   * a Basic Card's player-facing name. A `PetSkill` CardDefinition is never an
   * unlock row (`CARD_RULES.md` §1 item 4, ADR-012 item 9), so the derived
   * Signature Skill is never a key here and this map is deliberately **not** an
   * identification source for it.
   */
  private cardDefinitions = new Map<string, CardResponse>();
  /**
   * Relic definition lookup populated via GameRuntimePort.getRelics().
   *
   * It is the delivered `GET /api/relics` read — the Player's **owned** Relic
   * instances (`API_CONTRACTS.md` §5.4) — used for one presentation lookup: the
   * player-facing `name` of the Relic a delivered `RelicTriggered` event names.
   *
   * It is keyed by the **owned instance identity** (`relicId`), which is exactly
   * the value `RelicTriggered.relicId` and `PetState.EquippedRelics[]` carry
   * (`RELIC_RULES.md` §2.2 item 3, `SIGNALR_PROTOCOL.md` §3.2.23 item 1) — not by
   * a definition id. The map resolves a name and nothing else: it is not a Relic
   * content catalog, no trigger, condition, or effect definition is read from it,
   * and no gameplay value is derived from it (`GAME_RULES.md` §18, ADR-001).
   */
  private relicDefinitions = new Map<string, RelicResponse>();
  /** The owned Pet collection, keyed by the owned instance identity. */
  private petCatalog = new Map<string, PetResponse>();
  /**
   * The derived Signature Skill reference of each owned Pet, keyed by the
   * `CardDefinitionId` it names.
   *
   * It is populated only from the delivered Pet read
   * (`GameRuntimePort.getPets()`, `API_CONTRACTS.md` §5.1's `signatureSkill`),
   * so the Signature Skill is identified from the Pet that derives it — never
   * from a `Category` read out of the unlocked Card collection, which can never
   * contain it (`SIGNALR_PROTOCOL.md` §4.3 item 13, `CARD_RULES.md` §1 item 4).
   * Every entry is a delivered value, copied verbatim: the client derives no
   * Signature Skill, keeps no content catalog of its own, and validates no Card
   * (`AGENTS.md` §10).
   */
  private signatureSkills = new Map<string, PetSignatureSkillResponse>();
  /** Current battle state for redrawing cast controls when card definitions load. */
  private currentBattleState: RuntimeBattleState | null = null;
  /**
   * The developer Move Log, or `null` when the surface does not exist.
   *
   * It is built by `createDeveloperMoveLog` and only when the compile-time
   * `import.meta.env.DEV` flag is true — the flag's **one** read in this scene, at
   * shell construction (`App.tsx:203–204`'s convention, cited to `TASK-209` §6 by
   * operator decision D-1). It is never read per row: the panel's existence is this
   * one reference, so a production build has neither the surface nor the code that
   * would build it, and every call site below is a `null`-safe no-op there.
   *
   * Its `layer` is the **single declared container** every text object the panel
   * renders is a child of, which is what lets the smoke harness (and this suite)
   * narrow a scene-wide diagnostic assertion to "everything except the Move Log's
   * own declared text objects" by container membership rather than by a pattern
   * that could also hide a regression (TASK-253 D-1).
   */
  private developerMoveLog: DeveloperMoveLog | null = null;
  /**
   * The developer Move Log's retained rows, oldest first — a bounded ring.
   *
   * It is the record `getMoveLogRows()` reads, and the panel writes it **in
   * place**, so a reader holding the array keeps seeing the live buffer exactly as
   * `getPresentedEvents()` does. Every entry is one delivered event or one lifecycle
   * step of the client's own presentation: it holds no gameplay value, nothing in
   * the scene reads it back, and it is empty whenever the surface does not exist.
   */
  private readonly moveLogRows: string[] = [];

  constructor() {
    super('BattleScene');
  }

  create(): void {
    this.bossEnraged = false;
    this.currentBattleId = null;
    // The interaction objects of a previous run were destroyed with that run's
    // display list by Phaser, so their references are dropped *before* the reset
    // below can write to them: `releaseTimeline` re-derives the indicator and the
    // selection treatment, and neither may target an object the engine has
    // already taken down. The shell this run draws replaces them.
    this.selectionRing = null;
    this.phaseText = null;
    this.syncText = null;
    // A reused scene instance must start its next battle from the initial
    // presentation state: no timeline, no held values, no views carried over
    // (`shutdown` already dropped them; this is the same reset made explicit for
    // a scene that was never shut down). The orchestration machine is reset to
    // its initial value *before* the timeline is released, so the release is not
    // a transition out of a state the new run never entered.
    this.orchestration = 'idle';
    this.releaseTimeline();
    this.pendingOutcomeHandoff = null;
    this.gemViews = [];
    // A pooled view belongs to the board that is being dropped: the next run draws
    // its own, so the pool starts empty with the occupant map.
    this.refillableViews = [];
    this.settledBoardSequence = null;
    this.settledBattleId = null;
    this.heldSequence = null;
    // No resolution of a previous run has been accepted for this one: the memory is
    // keyed by the delivered battle, and this run's battle has not delivered a batch
    // yet (`shutdown` drops the same pair).
    this.acceptedResolutionSequence = null;
    this.acceptedResolutionBattleId = null;
    // No submission and no resolution of a previous run can be outstanding for
    // this one (`shutdown` drops the same pair).
    this.submissionOutstanding = false;
    this.resolutionPending = false;
    this.committedSwapCells = null;
    // No continuation of a previous run may write into this one, and no
    // presentation callback scheduled by that run may advance a phase here: both
    // tokens are moved forward rather than reset, so an in-flight request or a
    // pacing tween of the previous run can never match this run's values
    // (`shutdown` does the same).
    this.presentationGeneration += 1;
    this.submissionSerial += 1;
    // No selection and no terminal statement survive into the next battle: both
    // belong to the battle that ended (`shutdown` drops them for the same reason).
    this.clearSelection();
    this.terminalStatement = null;
    // The buffers are emptied *in place*: they are the scene's own diagnostic
    // record, and a reset must not swap the array a reader already holds.
    this.presentedPhases.length = 0;
    this.presentedEventsLog.length = 0;
    this.presentationLogBattleId = null;
    this.lastPlayerTurnBannerTurn = null;
    this.lastPlayerTurnBannerBattleId = null;
    if (this.activeTurnBanner) {
      this.tweens?.killTweensOf(this.activeTurnBanner);
      this.activeTurnBanner.destroy();
      this.activeTurnBanner = null;
    }
    // The developer Move Log belongs to the battle that just ended: its rows, its
    // row counter and its recorded sequence start empty for this run. The panel's
    // own objects are drawn by `drawRuntimeShell` below.
    this.developerMoveLog = null;
    this.timelineEffects = [];
    this.timelineCalloutShown = false;
    this.runtime = readRuntime(this);
    this.drawRuntimeShell();
    this.registerBoardInput();

    void this.loadCardDefinitions();
    void this.loadPetCatalog();
    void this.loadRelicDefinitions();

    // Reflect current runtime state immediately, then follow transitions.
    this.renderRuntimeState(this.runtime?.getState() ?? null);
    this.renderBattleState(this.runtime?.getBattleState() ?? null);

    if (this.runtime) {
      this.runtimeUnsubscribe = this.runtime.onRuntimeEvent((event) => {
        this.renderRuntimeState(event.state);
      });
      this.battleStateUnsubscribe = this.runtime.onBattleState((state, metadata) => {
        this.renderBattleState(state, metadata);
      });
      this.battleEventsUnsubscribe = this.runtime.onBattleEvents((envelope) => {
        this.handleBattleEvents(envelope);
      });
    }

    // The cleanup below is attached to Phaser's own scene lifecycle events.
    //
    // A scene method that merely *exists* is never invoked: Phaser 4.2.1 calls
    // `init`/`preload`/`create`/`update` by name (`SceneManager.bootScene` /
    // `SceneManager.create`) and nothing else. What the engine actually emits
    // when it takes a scene down is `Phaser.Scenes.Events.SHUTDOWN` — from
    // `Phaser.Scenes.Systems#shutdown`, which `SceneManager` calls for a queued
    // `stop` and when restarting a running scene — and `DESTROY` from
    // `Systems#destroy`. `Phaser.Scene` declares no `shutdown` method at all.
    //
    // So the subscription handles created just above are released by listening
    // for those events, the documented scene-event mechanism
    // (`.ai/skills/client/phaser-architecture` "Resource Lifecycle & Cleanup";
    // `.ai/skills/phaser/scenes` gotcha 14). Without this, a scene that is shut
    // down stays subscribed: the next `GameRuntime` state push or battle event
    // would reach a listener whose game objects Phaser has already destroyed
    // (`DisplayList#shutdown` destroys every child), and this scene's
    // per-instance state — `outcomeHandled` included — would survive into the
    // next time the same instance is started.
    //
    // `once` per event: a scene that is shut down and later restarted registers
    // exactly one handler per run, and the handler is consumed by the event
    // that fired it. Releasing twice is harmless — every handle is null-safe
    // and re-nulling an already-null reference is idempotent.
    //
    // The pair is detached before it is attached, so the registration itself is
    // idempotent: `once` alone would leave the run's *unfired* `DESTROY` handler
    // on the emitter, and a scene that is stopped and started again would stack
    // one more on every run (TASK-205's repo-wide audit; the same "detach before
    // attach" idempotency `registerBoardInput` below already uses).
    this.events.off(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.off(Phaser.Scenes.Events.DESTROY, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.DESTROY, this.shutdown, this);
  }

  /**
   * Scene teardown — the handler subscribed to Phaser's own
   * `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY` events by `create()`.
   *
   * It detaches every runtime subscription, so no stale listener survives the
   * scene (the runtime's `runtimeListeners` / `battleStateListeners` /
   * `battleListeners` must not contain this scene after teardown), and it drops
   * the scene's own references and per-instance state so a later `create()` on
   * the same instance starts a battle from a clean slate.
   *
   * It is a plain method invoked *by the engine's event*, not an engine hook:
   * Phaser never calls a scene method named `shutdown` on its own.
   *
   * What is released, and why (`ARCHITECTURE.md` §2.2.1 rule 3 — the scene owns
   * presentation, the runtime owns coordination, the server owns the battle):
   *
   * ```text
   * subscriptions          scene-owned handles on shared streams — released, so a
   *                        shut-down scene is not an observer of the next battle
   * game objects / layers  scene-owned presentation (the HUD text and gauges, the
   *                        board, the cast controls, the feedback layer) —
   *                        Phaser's DisplayList has already destroyed them, so
   *                        re-destroying is a no-op; dropping the references is
   *                        what stops a stale write, and the gauges are dropped
   *                        with them because a gauge is a pair of rectangles and
   *                        holds no value of its own
   * tween-driven feedback  the combat callout's hold/fade, every floating number,
   *                        the phase pacing of an in-flight timeline and any
   *                        refill travel — `tweens.killAll()` releases them with
   *                        the scene, so no presentation animation outlives it
   * per-battle guards      selectedCell, the orchestration machine (with the
   *                        outcomeHandled fact its `terminated` state carries),
   *                        and the in-flight presentation timeline — each
   *                        describes the battle that just ended, so the NEXT
   *                        battle must start from its initial value
   *                        (`terminated` in particular: left latched, it discards
   *                        the next outcome; a timeline left in place would lock
   *                        the next battle's input)
   * interaction feedback   the selected-cell ring's and the interaction
   *                        indicator's references — the engine has already
   *                        destroyed both objects with the display list, so
   *                        dropping the references is what stops a stale write;
   *                        each describes only this battle's own input state and
   *                        the resolution that just played, so the next battle
   *                        starts with no selection and no indicator statement
   * held presentation      heldSequence and committedSwapCells — the delivered
   *                        values a timeline had not released yet and the swap
   *                        pair it was going to present. Both are dropped without
   *                        being applied: Phaser's DisplayList has already
   *                        destroyed the objects they would be written to, and a
   *                        scene that is going away presents nothing
   * per-battle caches      presentedEventsLog, presentedPhases, gemViews,
   *                        cardDefinitions, relicDefinitions, petCatalog and
   *                        signatureSkills — presentation data of the ended
   *                        battle; no authoritative value is in them, so nothing
   *                        is lost by clearing them
   * synchronized copy      currentBattleState — a snapshot of what the server last
   *                        pushed; `create()` re-reads it from the runtime
   * ```
   *
   * It deliberately touches nothing it does not own: the runtime's synchronized
   * copy is dropped by `GameRuntime.clearActiveBattleState()` and not here, and
   * the preserved pre-battle loadout (ADR-022) is not battle state, so neither
   * this teardown nor that cleanup clears it.
   */
  shutdown(): void {
    this.runtimeUnsubscribe?.();
    this.runtimeUnsubscribe = null;
    this.battleStateUnsubscribe?.();
    this.battleStateUnsubscribe = null;
    this.battleEventsUnsubscribe?.();
    this.battleEventsUnsubscribe = null;
    this.connectionText = null;
    this.syncText = null;
    this.bossNameText = null;
    this.bossEnraged = false;
    this.currentBattleId = null;
    this.bossHpText = null;
    this.bossHpGauge = null;
    this.petNameText = null;
    this.petHpText = null;
    this.petHpGauge = null;
    this.petPowerText = null;
    this.powerGauge = null;
    this.petPassiveText = null;
    this.petPassiveProgressText = null;
    this.passiveGauge = null;
    this.petStatusText = null;
    this.comboText = null;
    this.matchesText = null;
    this.calloutText = null;
    this.boardMessageText = null;
    this.boardMessage = '';
    this.swapText = null;
    this.castText = null;
    this.selectionRing = null;
    this.phaseText = null;
    this.boardLayer?.destroy(true);
    this.boardLayer = null;
    this.castControlsLayer?.destroy(true);
    this.castControlsLayer = null;
    this.feedbackLayer?.destroy(true);
    this.feedbackLayer = null;
    // The developer Move Log panel is released with the shell it was drawn into,
    // so no text object of a shut-down scene survives into the next run.
    this.developerMoveLog?.release();
    this.developerMoveLog = null;
    this.tweens?.killAll();
    this.selectedCell = null;
    // The orchestration machine returns to its initial value for the next
    // battle, and the in-flight timeline and everything it was holding are
    // dropped, not applied: the engine has already destroyed the game objects
    // they would be written to. Releasing the guard here is what keeps a scene
    // that is stopped mid-resolution from coming back locked.
    this.orchestration = 'idle';
    this.releaseTimeline();
    this.pendingOutcomeHandoff = null;
    this.terminalStatement = null;
    this.gemViews = [];
    // Teardown drops the pool with the layer that held its objects: a view a
    // resolution left pooled is destroyed by the engine, never carried into the
    // next scene run.
    this.refillableViews = [];
    this.settledBoardSequence = null;
    this.settledBattleId = null;
    this.heldSequence = null;
    // The accepted-resolution memory belongs to the battle that just ended: the next
    // battle delivers its own first `serverSequence`, which must not be mistaken for a
    // re-delivery of this one's (`create()` drops the same pair).
    this.acceptedResolutionSequence = null;
    this.acceptedResolutionBattleId = null;
    // A submission or a resolution of the battle that just ended is not the next
    // battle's: the machine is reset to `idle` above, so neither fact may keep
    // the next run locked.
    this.submissionOutstanding = false;
    this.resolutionPending = false;
    this.committedSwapCells = null;
    // A continuation of the request this scene had in flight, and any pacing
    // callback the timeline it was playing had queued, must not write into the
    // scene that comes next: both tokens move forward, so neither can match
    // (`create()` does the same).
    this.presentationGeneration += 1;
    this.submissionSerial += 1;
    this.presentedEventsLog.length = 0;
    this.presentedPhases.length = 0;
    this.presentationLogBattleId = null;
    this.lastPlayerTurnBannerTurn = null;
    this.lastPlayerTurnBannerBattleId = null;
    if (this.activeTurnBanner) {
      this.tweens?.killTweensOf(this.activeTurnBanner);
      this.activeTurnBanner.destroy();
      this.activeTurnBanner = null;
    }
    // The developer Move Log: the engine has already destroyed the panel's
    // container and every text object in it with the display list, so the
    // references are dropped and the buffer is cleared — a scene that is shut down
    // mid-resolution presents nothing, and a reader sees an empty record rather
    // than a resolution that was abandoned.
    this.developerMoveLog = null;
    this.timelineEffects = [];
    this.timelineCalloutShown = false;
    this.castControls = [];
    this.cardDefinitions.clear();
    this.relicDefinitions.clear();
    this.petCatalog.clear();
    this.signatureSkills.clear();
    this.currentBattleState = null;
  }

  /**
   * Draws the battle HUD's static shell, its gauges, and its text lines.
   *
   * The hierarchy the layout establishes (`TASK-210`'s visual hierarchy):
   *
   * ```text
   * PRIMARY    the Boss band (name + HP gauge + numbers) and the Pet panel's
   *            name and HP gauge — the two values that end the battle
   * SECONDARY  the Pet's Power gauge, the delivered Combo/Match counters and
   *            the cast controls below the board
   * TERTIARY   the Passive identity and progress, the Status Effect instances,
   *            the interaction status lines and the connection status
   * ```
   *
   * Every line is player-facing battle information drawn from the authoritative
   * state (the Boss identity and HP, the Pet's name, HP and Power, the
   * Combo/Match counters, the Passive pair, the Status Effect instances) or a
   * short connection status. No developer diagnostic line exists in this scene:
   * the transport state, the battle id, the turn/sequence counters, the RNG seed
   * and state, and the raw event log are not the player's battle HUD (TASK-209
   * §5). The event log is still accumulated for development and tests
   * (`getPresentedEvents`) — it is simply not rendered.
   *
   * A line that can grow (a wrapped Status Effect list, a long swap message) is
   * created with `wordWrap` + `maxLines`, and with advanced wrapping so even a
   * single long token is broken at the column edge: a wrapped line therefore
   * cannot reach the board (`ARCHITECTURE.md` §2.2.2 rule 6 — the viewport scales
   * the presentation, it never changes the game's layout).
   */
  private drawRuntimeShell(): void {
    this.add
      .rectangle(
        SAFE_AREA.x + SAFE_AREA.width / 2,
        SAFE_AREA.y + SAFE_AREA.height / 2,
        SAFE_AREA.width,
        SAFE_AREA.height,
        0x0f172a
      )
      .setStrokeStyle(2, 0x334155);

    // --- Boss band (above the board): identity, HP gauge, hit points ---------
    // The section caption is static decoration: it is created once, never updated,
    // and needs no field — Phaser's own display list owns and destroys it.
    this.add
      .text(HUD_LEFT, BOSS_CAPTION_Y, 'BOSS', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '11px',
        color: '#64748b',
      })
      .setOrigin(0, 0);

    this.bossNameText = this.add
      .text(BOSS_NAME_X, BOSS_NAME_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '20px',
        color: '#f8fafc',
        fontStyle: 'bold',
        wordWrap: { width: BOSS_NAME_WIDTH, useAdvancedWrap: true },
        maxLines: 1,
      })
      .setOrigin(0, 0);

    // The Boss's HP gauge. It is drawn from the two delivered numbers and from
    // nothing else: no HP is accumulated from a damage event, no second HP value
    // is stored, and the fill is a *visual proportion* that never feeds back into
    // state (`SIGNALR_PROTOCOL.md` §4.4 items 5 and 7; `GAME_RULES.md` §18).
    this.bossHpGauge = this.createGauge(
      BOSS_BAR_X,
      BOSS_BAR_Y,
      BOSS_BAR_WIDTH,
      BOSS_BAR_HEIGHT,
      BOSS_HP_GAUGE_COLOR
    );

    this.bossHpText = this.add
      .text(BOSS_HP_TEXT_X, BOSS_HP_TEXT_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '16px',
        color: '#fca5a5',
      })
      .setOrigin(0, 0);

    // --- Connection status (top-centre) --------------------------------------
    this.connectionText = this.add
      .text(GAME_WIDTH / 2, CONNECTION_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '13px',
        color: '#94a3b8',
      })
      .setOrigin(0.5);

    // The synchronization indicator (`SYNC_INDICATOR_*`): the second centered
    // line of the boss band, hidden until the runtime reports that it is
    // unsynchronized. It is created hidden rather than conditionally, so its
    // lifetime is the shell's and no push allocates an object — and it is a
    // plain text line, so it is never an input target and can never intercept a
    // board tap or cover a cast control.
    this.syncText = this.add
      .text(GAME_WIDTH / 2, SYNC_INDICATOR_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '12px',
        color: SYNC_RECOVERING_COLOR,
        wordWrap: { width: SYNC_INDICATOR_WIDTH, useAdvancedWrap: true },
        maxLines: 1,
      })
      .setOrigin(0.5)
      .setVisible(false);

    // The interaction indicator: the client's own statement of what it permits
    // right now (`PHASE_TEXT_*`). It is the one line that answers "is it my
    // move?", and it answers that from `isInputLocked()`, the phase of the local
    // presentation timeline, and the delivered outcome — never from a turn,
    // phase, or status member, because the wire carries none
    // (`GAME_STATE.md` §2.0.3, `SIGNALR_PROTOCOL.md` §3.2.2; TASK-246's F-3).
    this.phaseText = this.add
      .text(PHASE_TEXT_X, PHASE_TEXT_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '15px',
        color: '#94a3b8',
        fontStyle: 'bold',
        wordWrap: { width: PHASE_TEXT_WIDTH, useAdvancedWrap: true },
        maxLines: 1,
      })
      .setOrigin(1, 0);

    // --- Pet panel (the column beside the board) -----------------------------
    this.add
      .text(HUD_LEFT, PET_CAPTION_Y, 'PET', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '11px',
        color: '#64748b',
      })
      .setOrigin(0, 0);

    this.petNameText = this.add
      .text(PET_NAME_X, PET_NAME_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '18px',
        color: '#f8fafc',
        fontStyle: 'bold',
        wordWrap: { width: PET_NAME_WIDTH, useAdvancedWrap: true },
        maxLines: 1,
      })
      .setOrigin(0, 0);

    this.petHpGauge = this.createGauge(
      PET_BAR_X,
      PET_BAR_Y,
      PET_BAR_WIDTH,
      PET_BAR_HEIGHT,
      PET_HP_GAUGE_COLOR
    );

    this.petHpText = this.add
      .text(PET_HP_TEXT_X, PET_HP_TEXT_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '15px',
        color: '#86efac',
      })
      .setOrigin(0, 0);

    // The Power gauge. `POWER_RANGE_MAX` is the documented `0–100` invariant of
    // `GAME_RULES.md` §12 shown as a static scale — it is not a wire member and
    // no `maxPower` is invented (`SIGNALR_PROTOCOL.md` §4.3 item 15). The
    // delivered value is what decides the fill, and no cost, affordability, or
    // cast legality is read from it.
    this.add
      .text(HUD_LEFT, POWER_LABEL_Y, 'POWER', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '12px',
        color: '#94a3b8',
      })
      .setOrigin(0, 0);

    this.powerGauge = this.createGauge(
      POWER_BAR_X,
      POWER_BAR_Y,
      POWER_BAR_WIDTH,
      POWER_BAR_HEIGHT,
      POWER_GAUGE_COLOR
    );

    this.petPowerText = this.add
      .text(POWER_TEXT_X, POWER_TEXT_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '15px',
        color: '#c4b5fd',
      })
      .setOrigin(0, 0);

    // The delivered Match feedback: the Combo (only when it is meaningful) and
    // the battle's cumulative Match count. Both are printed as received
    // (`MATCH3_RULES.md` §6.6 item 3).
    this.comboText = this.add
      .text(HUD_LEFT, COMBO_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '18px',
        color: '#fbbf24',
        fontStyle: 'bold',
      })
      .setOrigin(0, 0);

    this.matchesText = this.add
      .text(HUD_LEFT, MATCHES_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '13px',
        color: '#94a3b8',
      })
      .setOrigin(0, 0);

    // The Passive's delivered identity (SIGNALR_PROTOCOL.md §4.3 item 3),
    // presented verbatim: the scene does not charge a Passive, evaluate a
    // Threshold, or reset progress (§4.3 item 9), and the client holds no
    // Passive definition to name it with — so it prints the delivered identity
    // rather than a guessed label.
    this.petPassiveText = this.add
      .text(HUD_LEFT, PET_PASSIVE_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '13px',
        color: '#94a3b8',
      })
      .setOrigin(0, 0);

    // The Passive's progress gauge and its delivered `current / threshold`
    // readout. The gauge shows whether any progress exists at a glance; it is a
    // visual proportion of the delivered pair and evaluates neither the threshold
    // nor the trigger (§4.3 item 9).
    this.passiveGauge = this.createGauge(
      PASSIVE_BAR_X,
      PASSIVE_BAR_Y,
      PASSIVE_BAR_WIDTH,
      PASSIVE_BAR_HEIGHT,
      PASSIVE_GAUGE_COLOR
    );

    this.petPassiveProgressText = this.add
      .text(PASSIVE_TEXT_X, PASSIVE_TEXT_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '13px',
        color: '#94a3b8',
      })
      .setOrigin(0, 0);

    // The active Pet's delivered Status Effect instances (§4.3 item 14), presented
    // verbatim and never mutated. The line wraps inside the HUD column rather than
    // running under the board, so no cell or cast control is obscured, and it is
    // capped at three lines so a long list can never reach the board's row.
    this.petStatusText = this.add
      .text(HUD_LEFT, PET_STATUS_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '13px',
        color: '#94a3b8',
        wordWrap: { width: HUD_COLUMN_WIDTH, useAdvancedWrap: true },
        maxLines: 3,
      })
      .setOrigin(0, 0);

    // The transient combat callout: the player-facing statement of the most
    // important thing that happened in the resolution that just arrived. It sits
    // on the row directly above the board, outside the board's cells, and it
    // never blocks input (§TASK-210 §7–§8).
    this.calloutText = this.add
      .text(GAME_WIDTH / 2, CALLOUT_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '18px',
        color: '#e2e8f0',
        fontStyle: 'bold',
        wordWrap: { width: CALLOUT_WIDTH, useAdvancedWrap: true },
        maxLines: 1,
      })
      .setOrigin(0.5);

    // Board-area message, shown only while no authoritative state has arrived. It
    // shares the callout's row because the two are mutually exclusive: the scene
    // clears this message as soon as the first state push is rendered
    // (`renderBattleState`).
    this.boardMessageText = this.add
      .text(GAME_WIDTH / 2, CALLOUT_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '13px',
        color: '#64748b',
        wordWrap: { width: CALLOUT_WIDTH, useAdvancedWrap: true },
        maxLines: 1,
      })
      .setOrigin(0.5);

    // Swap input / acknowledgement feedback. This is transport feedback about
    // the request — not resolution presentation: it reports which cells were
    // selected and whether the server accepted the request
    // (SIGNALR_PROTOCOL.md §2.1, §5). It sits in the Pet column, beside the
    // board's lower half, and wraps inside it.
    this.swapText = this.add
      .text(HUD_LEFT, SWAP_TEXT_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '13px',
        color: '#94a3b8',
        wordWrap: { width: HUD_COLUMN_WIDTH, useAdvancedWrap: true },
        maxLines: 3,
      })
      .setOrigin(0, 0);

    // Cast input / acknowledgement feedback. Transport feedback about CardCast
    // and PetSkillCast requests (SIGNALR_PROTOCOL.md §2, §5). It is centered in
    // the strip between the board's bottom edge and the cast controls, so it
    // describes the controls directly below it.
    this.castText = this.add
      .text(GAME_WIDTH / 2, CAST_TEXT_Y, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '13px',
        color: '#94a3b8',
        wordWrap: { width: CALLOUT_WIDTH, useAdvancedWrap: true },
        maxLines: 1,
      })
      .setOrigin(0.5);

    // The board's cells are drawn into this container, and the container is the
    // board's input target (see `registerBoardInput`). A Container has no
    // implicit size and no texture, so it is only a valid input target once it is
    // given a hit area: `setSize(BOARD_WIDTH, BOARD_WIDTH)` declares the board
    // extent and `setInteractive(...)` supplies the shape Phaser hit-tests
    // (`.ai/skills/phaser/input-keyboard-mouse-touch`, "Containers must specify a
    // shape or call setSize first"; Phaser's `setHitAreaFromTexture` warns
    // "Container.setInteractive must specify a Shape or call setSize() first").
    //
    // The hit area is an explicit rectangle rather than the one `setInteractive()`
    // derives from `setSize`, because a Container reports `originX/originY = 0.5`
    // unconditionally (`Phaser.GameObjects.Container#originX`) and Phaser
    // normalizes that origin into the tested point
    // (`InputManager.pointWithinHitArea` adds `displayOriginX/Y`). Phaser's
    // derived rectangle `(0, 0, w, h)` would therefore be compared against local
    // points shifted by half the board, leaving the real board area unpickable
    // while a phantom region half a board away tested as inside.
    //
    // The rectangle below is positioned to cancel that shift, so the hit area
    // covers exactly the drawn board: the cells are drawn between
    // `BOARD_ORIGIN_X` and `BOARD_ORIGIN_X + BOARD_WIDTH` in the container's local
    // space, and the `+ BOARD_WIDTH / 2` accounts for the origin Phaser adds.
    const boardHitArea = new Phaser.Geom.Rectangle(
      BOARD_ORIGIN_X + BOARD_WIDTH / 2,
      BOARD_ORIGIN_Y + BOARD_WIDTH / 2,
      BOARD_WIDTH,
      BOARD_WIDTH
    );

    this.boardLayer = this.add
      .container(0, 0)
      .setSize(BOARD_WIDTH, BOARD_WIDTH)
      .setInteractive(boardHitArea, Phaser.Geom.Rectangle.Contains);
    this.castControlsLayer = this.add.container(0, 0);
    this.feedbackLayer = this.add.container(0, 0);

    // The selected-cell ring: one persistent, hollow, opaque ring that is moved
    // onto the selected cell and hidden when nothing is selected
    // (`renderSelection`).
    //
    // It is created outside every container — like the HUD gauges — so it is
    // drawn above the board layer and above the transient feedback layer while
    // leaving the board layer's own child count at exactly "64 tiles + 64
    // labels" (TASK-245's persistent-view invariant). Nothing here is per cell:
    // selecting allocates nothing, destroys nothing, and redraws nothing.
    this.selectionRing = this.add
      .rectangle(0, 0, SELECTION_RING_SIZE, SELECTION_RING_SIZE, 0x000000, 0)
      .setStrokeStyle(SELECTION_RING_STROKE, SELECTION_RING_COLOR)
      .setVisible(false);

    // The developer Move Log (TASK-253 D-1). It is drawn last so it is the top of
    // the shell, and — exactly like the two DEVELOPMENT-ONLY React diagnostics
    // (`App.tsx:203–204`) — only when the compile-time DEV flag is true. This is
    // the flag's **one** read: with it false the panel's construction, its rows and
    // every literal of its vocabulary are unreachable, so a production build drops
    // them whole and the surface does not exist at all.
    if (import.meta.env.DEV) {
      this.developerMoveLog = createDeveloperMoveLog(
        this,
        () => this.currentBattleState?.turn ?? null,
        this.moveLogRows
      );
    }
  }

  /**
   * Creates one HUD gauge: a bordered track and the fill drawn on top of it.
   *
   * Both rectangles are presentation objects. They are never read back, they hold
   * no gameplay value, and they are constructed once per scene run — updating one
   * resizes the existing fill rather than creating an object per state push
   * (TASK-210 §15).
   */
  private createGauge(
    x: number,
    y: number,
    width: number,
    height: number,
    fillColor: number
  ): HudGauge {
    this.add
      .rectangle(x + width / 2, y + height / 2, width, height, GAUGE_TRACK_COLOR)
      .setStrokeStyle(1, GAUGE_TRACK_BORDER);

    const fill = this.add.rectangle(x + width / 2, y + height / 2, width, height, fillColor);

    return { fill, x, y, width, height, fillState: { width } };
  }

  /**
   * Sets one gauge's fill to the **visual** proportion `value / max`.
   *
   * This is a presentation calculation and nothing else — the `barWidth = current
   * / max` shape `ARCHITECTURE.md` §2.2.2 permits. It therefore:
   *
   * - clamps only the *ratio*, never a displayed number and never state: `hp` is
   *   never rewritten, and the authoritative values are drawn as text beside the
   *   track exactly as delivered (`SIGNALR_PROTOCOL.md` §4.3 item 15, §4.4 item 7);
   * - treats a non-positive or non-finite `max` as "no proportion to draw"
   *   (an empty track) instead of dividing by it, so a defensive/terminal payload
   *   cannot produce `NaN` geometry or a phantom full bar;
   * - renders `value` above `max` as a full track, which is a display choice about
   *   the ratio only — no value is clamped in the payload or the HUD text.
   *
   * With `animate` and a tween manager present, the fill **travels** to the new
   * proportion (`GAUGE_TWEEN_MS`, `Sine.easeOut`) instead of jumping, so a
   * delivered HP change is a visible decrease rather than a discontinuity. The
   * travel is retargeted — not stacked — when a newer value arrives mid-flight,
   * and it lands exactly on `Math.round(width * proportion)`, the same width the
   * direct write would have produced. A direct write also cancels any travel
   * still in flight, so an interpolation can never overwrite the delivered
   * proportion after the fact.
   */
  private updateGauge(gauge: HudGauge | null, value: number, max: number, animate = false): void {
    if (!gauge) {
      return;
    }

    const ratio = max > 0 ? value / max : 0;
    const proportion = Number.isFinite(ratio) ? Math.min(1, Math.max(0, ratio)) : 0;
    const fillWidth = Math.round(gauge.width * proportion);

    if (!animate || !this.tweens) {
      this.tweens?.killTweensOf(gauge.fillState);
      gauge.fillState.width = fillWidth;
      BattleScene.applyGaugeFill(gauge, fillWidth);
      return;
    }

    if (fillWidth === gauge.fillState.width) {
      return;
    }

    this.trackPhaseAnimation(GAUGE_TWEEN_MS);
    this.tweens.killTweensOf(gauge.fillState);
    this.tweens.add({
      targets: gauge.fillState,
      width: fillWidth,
      duration: GAUGE_TWEEN_MS,
      ease: 'Sine.easeOut',
      onUpdate: () => {
        BattleScene.applyGaugeFill(gauge, Math.round(gauge.fillState.width));
      },
      onComplete: () => {
        gauge.fillState.width = fillWidth;
        BattleScene.applyGaugeFill(gauge, fillWidth);
      },
    });
  }

  /**
   * Resizes one gauge's existing fill to a pixel width — the whole visual write.
   *
   * Nothing is stored, read back, or derived here: the width is the presentation
   * proportion the caller already computed, and the delivered numbers are drawn
   * as text beside the track regardless of what the fill shows mid-travel.
   */
  private static applyGaugeFill(gauge: HudGauge, fillWidth: number): void {
    gauge.fill.setVisible(fillWidth > 0);
    gauge.fill.setSize(fillWidth, gauge.height);
    gauge.fill.setPosition(gauge.x + fillWidth / 2, gauge.y + gauge.height / 2);
  }

  /**
   * Presents the runtime's connection status in player-facing terms.
   *
   * This is the one line that reports the transport connection, and it says only
   * what a player needs: whether the battle is connected, still connecting,
   * reconnecting, or unavailable. It reports no transport detail — no SignalR
   * label, no connection id, no battle id — because that readout is developer
   * console material and not the player's battle HUD (TASK-209 §5). The
   * runtime's synchronization status is presented by its own line
   * (`renderSyncIndicator`), which is a state rather than a diagnostic.
   */
  private renderRuntimeState(state: GameRuntimeState | null): void {
    this.renderSyncIndicator(state);

    if (!this.connectionText) {
      return;
    }

    if (state === null) {
      this.connectionText.setText('Battle unavailable');
      this.connectionText.setColor('#f87171');
      return;
    }

    const { message, color } = describeConnection(state.connection);
    this.connectionText.setText(message);
    this.connectionText.setColor(color);
  }

  /**
   * Presents `GameRuntimeState.sync` as the one non-blocking line that says
   * whether the client's copy of the authoritative state is restored.
   *
   * ```text
   * unsynchronized, connected       SYNCHRONIZING…      a recovery is in flight
   *                                                     (reconnect, or a
   *                                                     submission whose outcome
   *                                                     was never answered)
   * unsynchronized, not connected   NOT SYNCHRONIZED    a state, not a promise:
   *                                                     no connection is up to
   *                                                     recover over
   * awaiting_battle, synchronized   hidden              `awaiting_battle` is
   *                                                     "connected, no battle"
   *                                                     and needs no line, and
   *                                                     `synchronized` is the
   *                                                     state the line is the
   *                                                     absence of
   * no runtime at all               hidden              the connection line
   *                                                     already reports it
   * ```
   *
   * It is driven by the runtime's published value and by nothing else: the scene
   * neither predicts nor reports synchronization the runtime has not confirmed,
   * and it derives no battle meaning from the value — `sync` is technical state
   * (`state/GameRuntimeState.ts`), never a turn, phase, or gameplay status
   * (`GAME_RULES.md` §18, `AGENTS.md` §10).
   *
   * The line is a plain, non-interactive text object: it never registers
   * pointer input, it is not on the board's input path, and it sits in the boss
   * band's own free row, clear of the board and of every cast control.
   */
  private renderSyncIndicator(state: GameRuntimeState | null): void {
    const text = this.syncText;
    if (!text) {
      return;
    }

    if (state === null || state.sync !== 'unsynchronized') {
      // Hidden rather than left showing the last statement, so the line cannot
      // claim an outage the runtime no longer reports — and the delivered
      // `synchronized` is what hides it, never the scene's own assumption.
      text.setText('');
      text.setVisible(false);
      return;
    }

    const recovering = state.connection === 'connected';
    text.setText(recovering ? SYNC_RECOVERING_MESSAGE : SYNC_UNSYNCHRONIZED_MESSAGE);
    text.setColor(recovering ? SYNC_RECOVERING_COLOR : SYNC_UNSYNCHRONIZED_COLOR);
    text.setVisible(true);
  }

  /**
   * Presents the synchronized authoritative state the server pushed
   * (`BattleStateUpdated`, SIGNALR_PROTOCOL.md §4) as the player-facing battle
   * HUD plus the 8 x 8 board.
   *
   * It presents the values verbatim. The scene computes nothing, adjusts
   * nothing, and owns nothing: these are server-authoritative fields
   * (GAME_STATE.md §2.0.5.4, ADR-001). No HP is derived from a damage event and
   * no Power is reconstructed from `PowerChanged` (§4.3 item 15, §4.4 item 7).
   *
   * **What is presented now, and what is held for the timeline.** The server
   * writes the resolution's state back and pushes it *before* it sends that
   * resolution's event batch (`SIGNALR_PROTOCOL.md` §3.1). So while the client is
   * awaiting the resolution of its own action — or while a resolution's timeline
   * is already playing — every readout the timeline owns is held at its previous
   * presentation and released by the timeline (`heldSequence`; operator decision
   * D-2):
   *
   * ```text
   * board                  the settle phase converges it (PD-1/PD-8)
   * Boss hp / maxHp        the damage phase presents the pair, and the Boss
   *                        Enraged label and callout with it
   * Pet hp / maxHp         the retaliation phase presents the pair
   * Combo / Match count    the combo phase presents them (ComboChanged)
   * Pet Power value/gauge  the power phase presents them (PowerChanged)
   * Passive progress/gauge the Pet's own passive phase presents them
   * Status Effect instances  converged at the timeline's end
   * cast controls          converged at the timeline's end, and presented
   *                        disabled for as long as input is held
   * ```
   *
   * Everything else — the Boss and Pet identities, the connection status, the
   * board message and the interaction indicator — is presented immediately, and a
   * push that is not an awaited resolution's and not superseding anything — a
   * join snapshot, a resync, a repeated push — is presented immediately too.
   *
   * Holding is *timing*, never computation: the values released are exactly the
   * delivered ones.
   *
   * **A recovery snapshot is never held (`P0.3` §4).** `metadata.source` is the
   * runtime's own statement of where an authoritative state came from
   * (`BattleStateMetadata`): `push` is the §4 delivery, `recovery` is the §7.1
   * snapshot the runtime requested because it could no longer vouch for its copy.
   * A recovery is applied **directly and immediately** — board, HUD, health,
   * turn-owned readouts and Status Effects, with no animation and with no
   * matching `ReceiveEvents` batch required, because a reconciliation snapshot is
   * exactly the case in which the batch may never arrive (`ADR-008`: recovery is
   * a snapshot, never an event replay). It also converges every in-flight
   * presentation first (`invalidatePendingPresentation`), so no lock, pacing
   * tween, transient effect, or submission continuation can survive it — an
   * authoritative recovery always leaves the scene idle and rendered from the
   * delivered values.
   *
   * An **equal-sequence** recovery takes the same path: the runtime accepts a
   * snapshot level with the copy it holds as an idempotent re-application
   * (`GameRuntime.receiveBattleState`), and the scene treats it as authoritative
   * too — the board and readouts it writes are the delivered ones, so a recovery
   * that confirms the state already presented costs no visible change, while one
   * that arrives for a resolution still being presented ends that presentation
   * rather than leaving it (or its lock) pending on a batch that may never come.
   *
   * Ordinary `push` updates keep the event-driven animation described above:
   * their timeline is built from the batch and presented in the batch's own
   * order, and only a superseding push aborts it.
   */
  private renderBattleState(
    state: RuntimeBattleState | null,
    metadata?: BattleStateMetadata
  ): void {
    this.currentBattleState = state;

    // The delivered battle identity this push replaces, read before it is
    // overwritten: a change of battle — or the loss of one — is the boundary at
    // which a request addressed to the old battle stops being this scene's.
    const previousBattleId = this.presentationLogBattleId;

    // The delivered battle changed (or ended): the previous battle's developer
    // record is not this battle's, so both diagnostic buffers start empty.
    if (state === null || (this.presentationLogBattleId !== null && this.presentationLogBattleId !== state.battleId)) {
      this.resetPresentationLogs();
    }
    this.presentationLogBattleId = state?.battleId ?? null;
    // The developer Move Log follows the same boundary: the delivered battle
    // identity is what its rows belong to, and a different one clears it.
    this.developerMoveLog?.syncBattle(state?.battleId ?? null);

    const recovery = metadata?.source === 'recovery';
    const ended = metadata?.source === 'ended';
    const battleGone =
      state === null || (previousBattleId !== null && state.battleId !== previousBattleId);
    let superseded = false;

    if (recovery) {
      // The authoritative reconciliation: everything a timeline was holding or
      // still playing is converged and released before the snapshot is
      // presented, and the snapshot itself is then presented immediately.
      this.invalidatePendingPresentation();

      // The snapshot's own sequence is the newest resolution this scene has seen,
      // so a later batch for a resolution it already contains is never replayed.
      if (state !== null) {
        this.seedAcceptedResolution(state);
      }
    } else {
      // A newer authoritative state supersedes playback: the timeline is dropped,
      // its held values are converged, and the newer state is then presented
      // immediately rather than being held for an older resolution's batch.
      superseded = this.isSupersededBy(state);
      if (superseded) {
        this.abortPresentation('superseded-state');
      }

      if (battleGone) {
        // The battle a submission was addressed to is gone — replaced by another
        // battle, or dropped (`clearActiveBattleState`) — so that submission can
        // never become the active scene's, and the lock it held is released with
        // it rather than waiting for an answer for a battle that is over
        // (P0.3 §5).
        this.invalidatePendingPresentation();
      }

      // A resolution this client is still awaiting whose delivered state this is
      // not: a resync, a jump in the post-resolution counter, or another battle's
      // state (`GAME_STATE.md` §5, `ADR-008`). The server has already moved past
      // it, so nothing will ever present it — the guard is released and the
      // delivered state is presented immediately, rather than leaving the client
      // locked behind a resolution that can no longer arrive.
      if (
        this.resolutionPending &&
        !this.isPresenting() &&
        (state === null || !this.isExpectedResolutionState(state))
      ) {
        this.releasePendingResolution();
      }
    }

    if (state === null) {
      this.heldSequence = null;
      // Nothing is delivered at all: there is no board for a selection to belong
      // to, and no interaction the indicator could honestly permit.
      this.clearSelection();
      this.renderBossHud(null, false);
      this.renderPetHud(null, null, false);
      this.renderPlayerHud(null, false);
      // `metadata.source === 'ended'` is the runtime publishing §7.3's
      // `BATTLE_NOT_FOUND`: the battle cannot be recovered, so the stale board and
      // every lock it held have already been released above (`invalidatePendingPresentation`)
      // and the board layer is emptied by `renderBoard(null)` below. The scene
      // presents the loss and **fabricates no result**: the documented fallback is
      // the runtime's own read of `GET /api/battle/{battleId}/result`
      // (`API_CONTRACTS.md` §4, `SIGNALR_PROTOCOL.md` §7.3), which this boundary
      // does not carry, so there is nothing here to navigate to and no outcome to
      // invent. A battle that never delivered an outcome delivers none now.
      this.boardMessage = ended ? BATTLE_ENDED_MESSAGE : 'Preparing the board…';
      this.boardMessageText?.setText(this.boardMessage);
      this.renderBoard(null);
      this.renderCastControls(null);
      this.renderPhaseIndicator();
      return;
    }

    const hold = !recovery && !superseded && (this.shouldHoldResolution(state) || this.isPresenting());
    this.heldSequence = hold ? state.sequence : null;

    // A push that presents a board replaces the board the player's gesture was
    // made on — a first push, a new battle, a resync, or a committed action's
    // result — so the selection ends with it. That is what stops a cell chosen
    // against one board from pairing with a later tap against another: the
    // interaction state is dropped, not re-pointed (`TASK-246` §3). A push
    // *held* for an awaited resolution presents no board at all, and its action
    // already cleared the selection when it was submitted.
    if (!hold) {
      this.clearSelection();
    }

    this.renderBossHud(state, hold);
    this.renderPetHud(state, this.activePetId(), hold);
    this.renderPlayerHud(state, hold);

    this.boardMessage = '';
    this.boardMessageText?.setText(this.boardMessage);

    if (!hold) {
      this.renderBoard(state.board);
    }

    // The cast controls are a timeline-owned readout (D-2): a held push presents
    // nothing of them, and a push that presents the board re-presents the set from
    // the delivered loadout with the guard's own state applied to it.
    if (!hold) {
      this.renderCastControls(state);
    }
    this.renderPhaseIndicator();

    if (
      !recovery &&
      !hold &&
      !this.isInputLocked() &&
      !this.outcomeHandled &&
      this.pendingOutcomeHandoff === null
    ) {
      this.presentPlayerTurnBannerIfNeeded();
    }
  }

  /**
   * Drops both bounded diagnostic buffers.
   *
   * It runs when the delivered battle changes and on teardown, so the developer
   * record always belongs to the battle on screen. Both are emptied **in place**:
   * they are the scene's own diagnostic record, and a reader holding the array
   * keeps seeing the live buffer.
   */
  private resetPresentationLogs(): void {
    this.presentedEventsLog.length = 0;
    this.presentedPhases.length = 0;
    this.lastPlayerTurnBannerTurn = null;
    this.lastPlayerTurnBannerBattleId = null;
    if (this.activeTurnBanner) {
      this.tweens?.killTweensOf(this.activeTurnBanner);
      this.activeTurnBanner.destroy();
      this.activeTurnBanner = null;
    }
  }

  /**
   * Presents the player turn banner if entering a new player turn.
   *
   * It ensures the banner appears when the turn begins without requiring player
   * input, while preventing duplicate presentation on repeated lifecycle pushes,
   * user selection events, or recovery of the same turn.
   */
  private presentPlayerTurnBannerIfNeeded(): void {
    const state = this.currentBattleState;
    if (
      !state ||
      this.outcomeHandled ||
      this.pendingOutcomeHandoff !== null ||
      this.isInputLocked() ||
      this.isPresenting() ||
      this.heldSequence !== null
    ) {
      return;
    }

    if (
      this.lastPlayerTurnBannerBattleId === state.battleId &&
      this.lastPlayerTurnBannerTurn === state.turn
    ) {
      return;
    }

    this.lastPlayerTurnBannerBattleId = state.battleId;
    this.lastPlayerTurnBannerTurn = state.turn;
    this.playTurnBanner('player');
  }

  /**
   * Appends one line to a bounded diagnostic buffer, evicting oldest-first.
   *
   * Both buffers are developer/testing records that are never rendered
   * (TASK-209 §5); without a cap they would grow for the life of the scene
   * (TASK-252 audit RC-5).
   */
  private static appendBounded(log: string[], entry: string): void {
    log.push(entry);

    if (log.length > PRESENTATION_LOG_CAP) {
      log.splice(0, log.length - PRESENTATION_LOG_CAP);
    }
  }

  /**
   * Whether an arriving authoritative state is newer than the timeline playing.
   *
   * Supersession is keyed on the delivered values alone: a state for a different
   * battle, or one whose `sequence` has moved past the batch being presented
   * (`GAME_STATE.md` §5 — the sequence is the post-resolution counter), is newer
   * than what is on screen, so playback is abandoned in favour of it rather than
   * continuing to present a resolution the server has already left behind.
   */
  private isSupersededBy(state: RuntimeBattleState | null): boolean {
    if (this.presentationTimeline === null || state === null) {
      return false;
    }

    if (state.battleId !== this.presentationBattleId) {
      return true;
    }

    return this.presentationSequence !== null && state.sequence > this.presentationSequence;
  }

  /**
   * Whether this push belongs to a resolution whose presentation has not finished
   * yet, so its board and HP panels should wait for the timeline.
   *
   * Two delivered/client-local facts must line up, and each rules out a case that
   * must be presented immediately:
   *
   * ```text
   * a resolution is pending  this client is presenting a resolution, or awaiting
   *                          the batch of an accepted action it submitted — the
   *                          only situation in which a batch for this state is
   *                          known to be coming (SIGNALR_PROTOCOL.md §2, §3.1).
   *                          It is `resolutionPending`, **not** "a request is
   *                          still in flight": an acknowledgement that settles
   *                          before its resolution's batch does not release the
   *                          hold, which is what stops the resolution's own final
   *                          board from being revealed before the timeline reaches
   *                          the settle phase that presents it (TASK-254 AF-6). A
   *                          join push, a resync snapshot (§7) and any unsolicited
   *                          push are therefore never held
   * same battle + the next   a new battle's first state is a fresh board, and
   *   sequence               `sequence` is the post-resolution counter, stepping
   *                          by one per resolved action (GAME_STATE.md §5) — so
   *                          `settled + 1` is this resolution and anything else is
   *                          a jump (recovery, a missed batch) that must
   *                          resynchronize by converging, never by replaying
   *                          (ADR-008)
   * ```
   */
  private shouldHoldResolution(state: RuntimeBattleState): boolean {
    return this.resolutionPending && this.isExpectedResolutionState(state);
  }

  /**
   * Whether the delivered state is the one this client's pending resolution
   * resolves to: the same battle, exactly one post-resolution sequence step past
   * the board on screen.
   *
   * It reads delivered counters only; it decides nothing about the battle and
   * evaluates no rule.
   */
  private isExpectedResolutionState(state: RuntimeBattleState): boolean {
    if (this.settledBoardSequence === null || this.settledBattleId !== state.battleId) {
      return false;
    }

    return state.sequence === this.settledBoardSequence + 1;
  }

  /**
   * Drops a resolution that can no longer be presented and releases the guard it
   * was holding.
   *
   * It is used when a delivered state proves the server has moved past the
   * resolution this client was awaiting — a recovery snapshot, a resync, or a
   * newer resolution resolved before the older batch arrived (`ADR-008`). The
   * submission, if any, is untouched: a request still unanswered keeps the guard
   * until its own acknowledgement settles (`onActionSettled`).
   */
  private releasePendingResolution(): void {
    this.resolutionPending = false;

    if (this.orchestration === 'submitted' && !this.submissionOutstanding) {
      this.transitionOrchestration('idle');
    }
  }

  /**
   * Accepts a resolution that cannot be presented and releases everything it
   * engaged, so no lock survives a batch that will never build a timeline
   * (`TASK-273` §4.3).
   *
   * It is the empty-phase counterpart of `invalidatePendingPresentation`: that
   * lifecycle releases the locks a *superseded* presentation held, and this one
   * releases the locks a presentation that was never built would have held. Both
   * end with the same machine state — no pending resolution, no unanswered
   * submission bookkeeping, and the readouts converged on the delivered state.
   *
   * ```text
   * settle      the values a hold deferred are applied from the delivered state,
   *             so the board and the timeline-owned readouts leave the mid-hold
   *             values they would otherwise keep forever
   * release     `resolutionPending` and `committedSwapCells` are dropped and the
   *             machine's own transitions release input; the request fact, if one
   *             is still unanswered, is kept — an acknowledgement that has not
   *             arrived is still outstanding, and releasing it here would let a
   *             second action be submitted against a board the answered resolution
   *             has not presented
   * re-derive   the transport feedback lines are re-derived from the released
   *             state, so a submission that is no longer pending cannot still be
   *             reported as one
   * ```
   *
   * **Nothing is invented.** Every value applied is a delivered one; no HP,
   * board, combo, or power is computed (`GAME_RULES.md` §18, `AGENTS.md` §10). The
   * batch carries no terminal outcome in this path — a `BattleWon`/`BattleLost`
   * always produces the timeline's `outcome` phase, so a batch that produced no
   * phase produced no handoff either — and none is fabricated.
   *
   * **An older `expectedSequence` cannot be resubmitted.** Releasing the guard
   * does not change the state version a later action is submitted with: the
   * runtime reads `expectedSequence` from the state it holds, which this path
   * leaves exactly as delivered.
   */
  private releaseUnpresentableResolution(): void {
    this.settleHeldPresentation();
    this.resolutionPending = false;
    this.committedSwapCells = null;
    this.onActionSettled();
    this.renderSwapStatus();
    this.renderCastStatus();
  }

  /**
   * Invalidates everything the scene had in flight and releases every
   * presentation lock — **exactly once**, and only through the lifecycle the
   * scene already has (`P0.3` §4, §5).
   *
   * It is the one lifecycle for the two moments a presentation stops belonging to
   * the scene's current state: an **authoritative recovery** converged the state
   * a timeline was presenting (`metadata.source === 'recovery'`), and the
   * **delivered battle is gone** — replaced by another battle's state, or dropped
   * entirely.
   *
   * ```text
   * 1. invalidate   the presentation generation and the submission serial move
   *                 forward, so a pacing callback or a request continuation that
   *                 is already in flight can no longer write — it belongs to the
   *                 state this call replaced
   * 2. cancel       the in-flight timeline is abandoned with everything it
   *                 started: pacing, gem-view travels, transient highlights and
   *                 floaters, and the callout it raised. With no timeline there is
   *                 no pacing left to cancel — the generation above is what makes
   *                 any callback already in flight a no-op — and what can still be
   *                 live (a gem travel, a transient effect, a raised callout, a
   *                 value a hold deferred) is released or converged here, without
   *                 animation, on the state this scene already adopted
   * 3. release      the resolution and the unanswered submission are settled
   *                 through the machine's own transitions, so input is released
   *                 and no lock survives the call, and the transport feedback lines
   *                 are re-derived from the released state
   * ```
   *
   * **Nothing is derived and nothing is invented.** Every value applied is a
   * delivered one; no HP, board, combo, or power is computed, and a delivered
   * state is never merged with a local prediction (`GAME_RULES.md` §18,
   * `AGENTS.md` §10).
   *
   * **Idempotent.** Each step is a no-op when its subject is already absent: a
   * second call finds no timeline, no held values, no outstanding lock, and no
   * request continuation that is still current, so it re-renders the delivered
   * state and changes nothing else. That is what lets a repeated reconciliation
   * snapshot be applied without duplicating an abort, a convergence, or a lock
   * release — and what stops the release from being scattered through the paths
   * that only want its effect.
   */
  private invalidatePendingPresentation(): void {
    // 1. Everything already queued belongs to the state this call replaced.
    this.presentationGeneration += 1;
    this.submissionSerial += 1;

    // 2. Cancel the interrupted presentation — and, with no timeline, anything an
    //    earlier abort left behind.
    if (this.presentationTimeline !== null) {
      // A newer authoritative state superseding playback is the abandonment the
      // scene already has a name for. The abort cancels the pacing, the gem
      // travels and the timeline's transient effects, converges the held values,
      // and releases the timeline and its callout.
      this.abortPresentation('superseded-state');
    } else {
      // No timeline is playing, so there is no pacing to cancel: the generation
      // above is what makes any callback that is already in flight a no-op. What
      // can still be live is a gem-view travel, a transient effect, a callout a
      // timeline raised, and a value a hold deferred — each released or converged
      // here.
      this.killGemViewTweens();
      this.cancelTimelineEffects();
      this.cancelTimelineCallout();
      this.timelineCalloutShown = false;
      this.settleHeldPresentation();
    }

    // 3. Release every presentation lock. The resolution this client was awaiting
    //    can no longer be presented — its batch may never arrive — and no request
    //    is left outstanding: a continuation of one is invalidated above, so it
    //    cannot re-lock the scene when it settles. The transport feedback lines are
    //    re-derived from the released state, so a request that is no longer
    //    outstanding cannot still be reported as one.
    this.resolutionPending = false;
    this.committedSwapCells = null;
    this.onActionSettled();
    this.renderSwapStatus();
    this.renderCastStatus();
  }

  /**
   * Adopts a recovered snapshot's `sequence` as the newest resolution this scene
   * has accepted.
   *
   * A recovery is the runtime's reconciliation, not a delivery of events
   * (`ADR-008`), so a later batch at or below the recovered sequence describes a
   * resolution the snapshot already contains: it is recorded in the developer
   * log and presented no second time — which is the scene's own guard
   * (`acceptedResolutionSequence`) and not a second one. A batch past it is a
   * genuinely newer resolution and plays normally.
   *
   * It advances a counter and compares two delivered numbers; it derives no
   * gameplay value and decides nothing about the battle.
   */
  private seedAcceptedResolution(state: RuntimeBattleState): void {
    if (this.acceptedResolutionBattleId !== state.battleId) {
      this.acceptedResolutionBattleId = state.battleId;
      this.acceptedResolutionSequence = state.sequence;
      return;
    }

    if (
      this.acceptedResolutionSequence === null ||
      state.sequence > this.acceptedResolutionSequence
    ) {
      this.acceptedResolutionSequence = state.sequence;
    }
  }

  /**
   * Presents the Boss's delivered `hp / maxHp` — the readout and its gauge
   * together.
   *
   * Both are the same two delivered numbers (`SIGNALR_PROTOCOL.md` §4.4), so they
   * move as one: the text is written verbatim and the gauge travels to the
   * delivered proportion. Nothing is derived — the pair is not clamped, and
   * neither number is computed from a damage event.
   */
  private presentBossHp(state: RuntimeBattleState, animate = true): void {
    this.bossHpText?.setText(`${state.bossState.hp} / ${state.bossState.maxHp}`);
    this.updateGauge(this.bossHpGauge, state.bossState.hp, state.bossState.maxHp, animate);
  }

  /** Presents the active Pet's delivered `hp / maxHp` — the readout and its gauge. */
  private presentPetHp(state: RuntimeBattleState, animate = true): void {
    this.petHpText?.setText(`${state.petState.hp} / ${state.petState.maxHp}`);
    this.updateGauge(this.petHpGauge, state.petState.hp, state.petState.maxHp, animate);
  }

  /**
   * Presents the Boss band: its display name, resolved from the existing client
   * catalog by the authoritative `bossState.bossId`, the delivered `hp / maxHp`
   * pair as text, and the HP gauge drawn from those same two numbers.
   *
   * The name is **presentation only and never a wire value** (§4.4 item 10): the
   * projection carries the canonical technical Identity and the client resolves
   * the rest from the one transcription of `BOSS_RULES.md` §6.4 it already holds
   * (`MVP_BOSSES`). No second catalog is created and none is fetched, the resolved
   * Element label takes no part in any matchup or damage decision, and an
   * unrecognized identity is rendered as the identity itself — never a guessed
   * name.
   *
   * Both numbers are rendered exactly as delivered: neither is clamped to the
   * other and no percentage is predicted (§4.4 items 5 and 7). The gauge is the
   * only thing the pair is turned into, and it is a ratio of presentation — the
   * full-HP, zero-HP and out-of-range cases all keep the delivered numbers on
   * screen unchanged (`updateGauge`).
   *
   * `hold` decides *when* the delivered pair and the Enrage label are presented
   * (see `renderBattleState`). Both are timeline-owned readouts under operator
   * decision D-2: the Boss's HP moves in the damage phase this resolution deals,
   * and the Enrage transition is a function of that same delivered HP pair, so
   * both are presented by the phase that owns them. The identity is presented
   * immediately because it is not one of them, and while a hold is in place the
   * label keeps the *previously presented* latch rather than reading ahead
   * (`presentBossEnrage`).
   */
  private renderBossHud(state: RuntimeBattleState | null, hold: boolean): void {
    if (!this.bossNameText || !this.bossHpText) {
      return;
    }

    if (state === null) {
      this.bossEnraged = false;
      this.currentBattleId = null;
      this.bossNameText.setText('');
      this.bossHpText.setText('');
      this.updateGauge(this.bossHpGauge, 0, 0);
      return;
    }

    if (this.currentBattleId !== null && this.currentBattleId !== state.battleId) {
      this.bossEnraged = false;
    }
    this.currentBattleId = state.battleId;

    const bossId = state.bossState.bossId;

    if (hold) {
      // The Enrage latch and its callout are held for the phase that owns the
      // damage: the label keeps whatever the last presented resolution latched.
      this.bossNameText.setText(this.bossDisplayLabel(bossId));
      return;
    }

    this.presentBossEnrage(state);
    this.presentBossHp(state);
  }

  /** The Boss's display name with the currently presented Enrage label. */
  private bossDisplayLabel(bossId: string): string {
    const displayName = resolveBossDisplayName(bossId);
    return this.bossEnraged ? `${displayName} [ENRAGED]` : displayName;
  }

  /**
   * Presents the Boss's derived Enrage state — the latch, the `[ENRAGED]` label
   * and, on the initial transition only, the `BOSS ENRAGED` callout.
   *
   * The pair is read from the authoritative `hp` / `maxHp` (`BOSS_RULES.md` §5
   * item 4, §6.1) and the latch persists for the rest of the battle, so a later
   * resolution — including one that heals the Boss past the threshold — presents
   * no second transition and no second callout.
   *
   * It is a timeline-owned readout under D-2: `renderBossHud` presents it
   * immediately only when nothing is held, and the damage phase (or convergence)
   * presents it otherwise.
   */
  private presentBossEnrage(state: RuntimeBattleState): void {
    if (!this.bossNameText) {
      return;
    }

    const bossId = state.bossState.bossId;
    const initialTransition =
      !this.bossEnraged && isBossEnraged(bossId, state.bossState.hp, state.bossState.maxHp, false);

    if (initialTransition) {
      this.bossEnraged = true;
      this.showCallout('BOSS ENRAGED', CALLOUT_BOSS_COLOR);
      this.timelineCalloutShown = this.isPresenting();
    }

    this.bossNameText.setText(this.bossDisplayLabel(bossId));
  }

  /**
   * Presents the Pet panel: the Pet's name, its authoritative `hp / maxHp` as
   * text over its HP gauge, its Power over the Power gauge, and the
   * already-delivered Passive progress and Status Effect instances.
   *
   * The HP and Power values are the delivered `petState` values and nothing else
   * (§4.3 item 15), rendered as the same two numbers the gauge's fill is a ratio
   * of. The Pet's name is presentation content: `PetState` carries no
   * client-visible identity (ADR-014), so it is resolved from the client's own
   * loadout/catalog — the preserved pre-battle selection's `petId` plus the owned
   * Pet collection. When that identity is unknown the panel states what it has
   * (the identity it was given) or a neutral placeholder; it never invents a name.
   *
   * The Passive and Status Effect lines present values the projection already
   * delivers (`petState.passiveId`, `passiveProgress`, `statusEffects[]`), so they
   * expand no contract. Nothing is derived: no Passive is charged, no Threshold is
   * evaluated, no progress is reset (§4.3 item 9), and no Status Effect is applied,
   * refreshed, decremented, expired, or removed (§4.3 item 14).
   *
   * `hold` decides *when* the timeline-owned readouts are presented (operator
   * decision D-2): the delivered `hp / maxHp` pair is presented by the retaliation
   * phase, the Power value and gauge by the power phase, the Passive progress and
   * gauge by the Pet's own passive phase, and the Status Effect instances by
   * convergence at the timeline's end. The Pet's identity is presented
   * immediately, because it is not one of them.
   */
  private renderPetHud(
    state: RuntimeBattleState | null,
    petId: string | null,
    hold: boolean
  ): void {
    if (!this.petNameText || !this.petHpText || !this.petPowerText) {
      return;
    }

    if (state === null) {
      this.petNameText.setText('');
      this.petHpText.setText('');
      this.petPowerText.setText('');
      this.petPassiveText?.setText('');
      this.petPassiveProgressText?.setText('');
      this.petStatusText?.setText('');
      this.updateGauge(this.petHpGauge, 0, 0);
      this.updateGauge(this.powerGauge, 0, POWER_RANGE_MAX);
      this.updateGauge(this.passiveGauge, 0, 0);
      return;
    }

    this.petNameText.setText(resolvePetDisplayName(petId, this.petCatalog));

    if (hold) {
      return;
    }

    this.presentPetHp(state);
    this.presentPetPower(state);
    this.presentPetPassive(state);
    this.presentPetStatus(state);
  }

  /**
   * Presents the active Pet's delivered Power as text and as its gauge.
   *
   * The `0–100` range is the documented invariant of `GAME_RULES.md` §12, shown
   * as a static scale around the delivered value. No `maxPower` exists on the
   * wire and none is invented; no cost, affordability, or cast legality is derived
   * from the value (§4.3 item 15, `CARD_RULES.md` §3.6).
   *
   * It is a timeline-owned readout under D-2, presented by the power phase (the
   * delivered `PowerChanged`) or by convergence.
   */
  private presentPetPower(state: RuntimeBattleState, animate = false): void {
    this.petPowerText?.setText(`${state.petState.power} / ${POWER_RANGE_MAX}`);
    this.updateGauge(this.powerGauge, state.petState.power, POWER_RANGE_MAX, animate);
  }

  /**
   * Presents the active Pet's delivered Passive identity and its
   * `current / threshold` progress pair, over its progress gauge.
   *
   * The gauge is the delivered pair drawn as a proportion. Comparing the pair to
   * the Threshold — and every charge, trigger, overflow and reset rule — is the
   * server's (`PASSIVE_RULES.md` §2–§5, §4.3 item 9).
   *
   * It is a timeline-owned readout under D-2, presented by a `passive` phase whose
   * delivered actor is the Pet itself — the panel's pair is `PetState`'s, so a
   * Boss-owned Passive charge (whose owner is the Boss) presents nothing here —
   * or by convergence.
   */
  private presentPetPassive(state: RuntimeBattleState, animate = false): void {
    const { current, threshold } = state.petState.passiveProgress;

    this.petPassiveText?.setText(`Passive: ${state.petState.passiveId}`);
    this.petPassiveProgressText?.setText(
      `${current} / ${threshold} Matches${describePassiveReset(state.petState)}`
    );
    this.updateGauge(this.passiveGauge, current, threshold, animate);
  }

  /**
   * Presents the active Pet's delivered Status Effect instances.
   *
   * It is a timeline-owned readout under D-2 with no owning delivered event: the
   * closed discriminator set contains no Status Effect event
   * (`SIGNALR_PROTOCOL.md` §3.2.2), so the collection reaches the client through
   * the state push alone and is converged at the timeline's end.
   */
  private presentPetStatus(state: RuntimeBattleState): void {
    this.petStatusText?.setText(
      `Effects: ${describeStatusEffects(state.petState.statusEffects)}`
    );
  }

  /**
   * Presents the player's delivered Match accounting: the cumulative Match count
   * and, when it is meaningful, the Combo.
   *
   * Both values are read as sent and neither is computed, counted, advanced, or
   * reset here (`GAME_RULES.md` §18, `MATCH3_RULES.md` §6.6 item 3). Combo counts
   * the Matches of one committed Swap, so a published `combo` of `1` is an
   * ordinary single-Match Swap and a value of `2` or more is the string of
   * Matches the player cares about (`MATCH3_RULES.md` §6.2–§6.3, §6.5 item 4);
   * the line is therefore shown only from `2` upward, and no new combo rule or
   * multiplier is introduced with it.
   *
   * **Idle versus active.** The line follows the delivered value rather than a
   * client-held one, so the idle state is an empty line and any combo the server
   * has since replaced below `2` clears it on the same push. It is deliberately
   * *not* aged out on a timer: `MATCH3_RULES.md` §6.5 items 1–2 make a rejected
   * Swap leave the previous Swap's Combo standing, so a client-side expiry would
   * disagree with the authoritative value. The moment a Combo is *earned* is
   * instead marked by the transient `COMBO ×N` callout, which is set from the
   * delivered `ComboChanged` event (`renderEventCallout`).
   *
   * **The active line is the timeline's own point, not the round's end.** While a
   * timeline plays, the Combo the player reads is the value the step being played
   * delivered — `combo` below, the phase's own `ComboChanged` payload
   * (`SIGNALR_PROTOCOL.md` §3.2.8: the **new** Combo value that Match produced) —
   * so a four-Match round reads `×2` at the Match that earned it and `×4` only
   * when the round reaches it. Presenting `currentBattleState.playerState.combo`
   * instead would show the round's *final* value at the first Matches, which is a
   * value the resolution had not reached yet (`MATCH3_RULES.md` §6.6 item 1: Combo
   * reaches its final value for the Swap as the Swap resolves). Nothing is
   * computed to do this: the number shown is the delivered one, and the committed
   * value is still what convergence presents.
   *
   * Both lines are timeline-owned readouts under operator decision D-2, presented
   * by the phase holding the delivered event that owns them or by convergence at
   * the timeline's end. The Combo line's own event is a `ComboChanged`; the
   * cumulative Match count has no owned event in the closed discriminator set, so
   * it is read from the committed state whenever the line is written during the
   * timeline and is converged on that same committed value at its end. `hold` is
   * what keeps a raw push from writing either of them early.
   *
   * @param state the delivered authoritative state the committed values come from.
   * @param hold `true` when a raw push must present neither line.
   * @param combo the delivered Combo value the step being played reports, or
   *   `null` when the caller presents the committed state's own value — which is
   *   what convergence, a rollback and a push that presents the board all do.
   */
  private renderPlayerHud(
    state: RuntimeBattleState | null,
    hold = false,
    combo: number | null = null
  ): void {
    if (!this.comboText || !this.matchesText) {
      return;
    }

    if (state === null) {
      this.comboText.setText('');
      this.matchesText.setText('');
      return;
    }

    if (hold) {
      return;
    }

    const presentedCombo = combo ?? state.playerState.combo;

    this.comboText.setText(presentedCombo >= 2 ? `COMBO ×${presentedCombo}` : '');
    this.matchesText.setText(`MATCHES ${state.playerState.matchCount}`);
  }

  /**
   * The active Pet's owned-instance identity, read from the preserved pre-battle
   * loadout (ADR-022) — the only client-side record of the loadout this battle was
   * started with, because `PetState` carries no client-visible identity
   * (ADR-014).
   *
   * It is **not** used as authoritative battle state: it selects a display name
   * only. HP and Power come from `petState` regardless of whether an identity was
   * found, so a missing or unknown loadout can never change a rendered combat
   * value — it can only leave the name as the identity itself or a neutral
   * placeholder.
   */
  private activePetId(): string | null {
    return readPreservedLoadout(this)?.petId ?? null;
  }

  /**
   * Loads card definition metadata via the runtime port (`GameRuntimePort.getCards()`).
   *
   * The client uses definition metadata only to present a Basic Card's player-facing
   * name. It performs no gameplay validation and computes no effect or cost
   * (CARD_RULES.md §3, ADR-001).
   *
   * **It is not a Signature Skill identification source.** `getCards()` is the
   * unlocked Card collection (`API_CONTRACTS.md` §5.3), and a `PetSkill`
   * CardDefinition is never an unlock row (`CARD_RULES.md` §1 item 4, ADR-012
   * item 9), so the derived Signature Skill's identity can never be a key in this
   * map. The Signature Skill is identified from the Pet read instead
   * (`loadPetCatalog`, `SIGNALR_PROTOCOL.md` §4.3 item 13).
   */
  private async loadCardDefinitions(): Promise<void> {
    if (!this.runtime || typeof this.runtime.getCards !== 'function') {
      return;
    }

    try {
      const cards = await this.runtime.getCards();
      this.cardDefinitions.clear();
      for (const card of cards) {
        this.cardDefinitions.set(card.cardId, card);
      }
      if (this.currentBattleState && !this.isInputLocked()) {
        this.renderCastControls(this.currentBattleState);
      }
    } catch {
      // Failed collection read is presentation feedback; controls render with available data.
    }
  }

  /**
   * Loads the owned Pet collection via the runtime port
   * (`GameRuntimePort.getPets()`, `API_CONTRACTS.md` §5.1), keyed by the owned
   * instance identity — and, from the same delivered read, each Pet's derived
   * Signature Skill reference, keyed by the card identity it names.
   *
   * ```text
   * petCatalog       petId    → the owned Pet        (one display lookup)
   * signatureSkills  cardId   → the Pet's derived    (the Signature Skill
   *                             Signature Skill       identification source)
   * ```
   *
   * The Pet catalog serves **one display lookup**: turning the preserved
   * loadout's `petId` into the Pet's `identity` string the Lobby already shows. It
   * supplies no combat value — HP, Max HP and Power are the authoritative
   * `petState` values and are rendered whether or not this read succeeds. A failed
   * read leaves the lookup empty and the panel falls back to the identity itself,
   * exactly as an unknown identity does; nothing is fabricated (AGENTS.md §7).
   *
   * The Signature Skill map serves the **identification** `SIGNALR_PROTOCOL.md`
   * §4.3 item 13 fixes: the equipped entry whose `cardId` is the Pet's delivered
   * `signatureSkill.cardId` is the derived Signature Skill, so the client never
   * has to read a `Category` out of the Card collection it cannot populate for a
   * `PetSkill` row. A failed read leaves the map empty, and the presentation fails
   * closed to the delivered card identity on the ordinary `CardCast` path — which
   * `CardCast(<signature cardId>)` keeps protocol-conformant (§2, §3.2.20).
   */
  private async loadPetCatalog(): Promise<void> {
    if (!this.runtime || typeof this.runtime.getPets !== 'function') {
      return;
    }

    try {
      const pets = await this.runtime.getPets();
      this.petCatalog.clear();
      this.signatureSkills.clear();
      for (const pet of pets) {
        this.petCatalog.set(pet.petId, pet);

        // API_CONTRACTS.md §5.1's derived Signature Skill reference, copied as
        // delivered. One Pet's reference is one CardDefinitionId, so this keying
        // is a value copy rather than a derivation, and the entry carries the
        // Skill's own delivered name.
        this.signatureSkills.set(pet.signatureSkill.cardId, pet.signatureSkill);
      }
      if (this.currentBattleState) {
        // The same hold `renderBattleState` applies: while a resolution's timeline
        // owns the Pet panel's readouts, a settled collection read refreshes the
        // Pet's *name* (presentation content, not a delivered combat value) and
        // nothing else — it never presents a held readout early (D-2).
        const hold = this.heldSequence !== null || this.isPresenting();
        this.renderPetHud(this.currentBattleState, this.activePetId(), hold);
        if (!hold) {
          this.renderCastControls(this.currentBattleState);
        }
      }
    } catch {
      // Failed collection read is presentation feedback; the panel renders with the
      // identity it has.
    }
  }

  /**
   * Loads the owned Relic instances via the runtime port
   * (`GameRuntimePort.getRelics()`, `API_CONTRACTS.md` §5.4), keyed by the owned
   * instance identity.
   *
   * The Relic read serves **one display lookup**: turning a delivered
   * `RelicTriggered.relicId` into the Relic's own `name` for the combat callout.
   * It is the same collection read the Lobby and the Collection viewer already
   * make, through the same port — no second API service, no direct HTTP access
   * (`ARCHITECTURE.md` §2.2 rule 3), and no client-side Relic catalog.
   *
   * Only `name` takes part. `trigger`, `condition`, and `effectDefinition` are the
   * delivered content the Lobby renders, and reading a *result* out of them would
   * be client-side derivation (`RELIC_RULES.md` §8.2 item 1, `GAME_RULES.md` §18):
   * whether a Relic triggered is decided by the server and arrives as
   * `RelicTriggered`, and what it changed arrives through the `BattleState`
   * projection or `PowerChanged`. A failed read leaves the lookup empty, and the
   * callout then declines to name the Relic rather than showing its raw identity.
   */
  private async loadRelicDefinitions(): Promise<void> {
    if (!this.runtime || typeof this.runtime.getRelics !== 'function') {
      return;
    }

    try {
      const relics = await this.runtime.getRelics();
      this.relicDefinitions.clear();
      for (const relic of relics) {
        this.relicDefinitions.set(relic.relicId, relic);
      }
    } catch {
      // Failed collection read is presentation feedback; a Relic whose name is
      // unknown simply produces no callout.
    }
  }

  /**
   * Renders interactive cast triggers for the 3 submitted Basic Cards and the
   * active Pet's derived Signature Skill from authoritative
   * `RuntimeBattleState.petState.equippedCards`.
   *
   * **Identification is Pet-derived, and it uses no position.** An equipped entry
   * is the Signature Skill if and only if its `cardId` is a Signature Skill
   * reference the delivered Pet read gave for an owned Pet
   * (`this.signatureSkills`, `API_CONTRACTS.md` §5.1) — the reference the derived
   * entry was resolved from (`SIGNALR_PROTOCOL.md` §4.3 item 13, `CARD_RULES.md`
   * §4 item 1). It is deliberately **not** identified by an entry's array index,
   * by a `Category` read out of `getCards()`, or by any per-Pet hard-coding: the
   * map is built from the delivered read alone, so every owned Pet's own Signature
   * Skill resolves and no individual card id appears here.
   *
   * **Dispatch follows the identification.** The Signature Skill dispatches
   * `PetSkillCast` (§2, canonical) and every other entry dispatches
   * `CardCast(cardId)`. An entry that matches no delivered reference fails closed
   * to the ordinary Card path — never to an invented skill — which keeps
   * `CardCast(<signature cardId>)`'s protocol compatibility (§2, §3.2.20).
   *
   * **Presentation distinguishes the two through the existing model**: the
   * `Skill: <name>` caption and tile style versus the `Card: <name>` caption and
   * tile style. No new HUD, terminology, or vocabulary is introduced, and the
   * Skill's name is the delivered `signatureSkill.name` rather than the raw
   * `cardId` (`API_CONTRACTS.md` §5.1). No cost, affordability, or legality is
   * read or computed here (`CARD_RULES.md` §3.6, ADR-001).
   *
   * **`disabled` is the guard made visible (D-2).** The control set is a
   * timeline-owned readout: while input is held the controls are drawn dimmed and
   * carry **no** pointer handler, so a click during the lock submits nothing. The
   * set itself is rebuilt only from a push that is not held, so it is never
   * destroyed and recreated while a timeline is playing; the guard's own changes
   * toggle the existing controls' presentation (`presentCastControlsEnabled`).
   * `submitCardCast` / `submitPetSkillCast` keep their own guard check regardless,
   * so the disabled presentation is feedback rather than the guard itself.
   */
  private renderCastControls(state: RuntimeBattleState | null): void {
    if (!this.castControlsLayer) {
      return;
    }

    this.castControlsLayer.removeAll(true);
    this.castControls = [];

    if (state === null || !state.petState.equippedCards) {
      return;
    }

    const equipped = state.petState.equippedCards;
    const buttonWidth = 115;
    const buttonHeight = 36;
    const buttonGap = 10;
    const originY = BOARD_ORIGIN_Y + BOARD_WIDTH + 34;

    for (let index = 0; index < equipped.length; index++) {
      const cardId = equipped[index];
      const signatureSkill = this.signatureSkills.get(cardId);
      const isPetSkill = signatureSkill !== undefined;
      const displayName = signatureSkill?.name ?? this.cardDefinitions.get(cardId)?.name ?? cardId;
      const labelText = isPetSkill ? `Skill: ${displayName}` : `Card: ${displayName}`;

      const x = BOARD_ORIGIN_X + index * (buttonWidth + buttonGap) + buttonWidth / 2;
      const y = originY + buttonHeight / 2;

      const enabledFill = isPetSkill ? 0x4f46e5 : 0x1e293b;
      const enabledStroke = isPetSkill ? 0x818cf8 : 0x475569;

      const tile = this.add
        .rectangle(x, y, buttonWidth, buttonHeight, enabledFill)
        .setStrokeStyle(1, enabledStroke)
        .setInteractive({ useHandCursor: true });

      const label = this.add
        .text(x, y, labelText, {
          fontFamily: 'system-ui, sans-serif',
          fontSize: '12px',
          color: CAST_CONTROL_ENABLED_TEXT,
          fontStyle: 'bold',
        })
        .setOrigin(0.5)
        .setInteractive({ useHandCursor: true });

      this.castControlsLayer.add([tile, label]);
      this.castControls.push({
        tile,
        label,
        enabledFill,
        enabledStroke,
        onTrigger: () => {
          if (isPetSkill) {
            void this.submitPetSkillCast();
          } else {
            void this.submitCardCast(cardId);
          }
        },
      });
    }

    this.presentCastControlsEnabled(!this.isInputLocked());
  }

  /**
   * Presents the cast controls as enabled or disabled **in place**.
   *
   * It is what makes the guard visible without rebuilding the set: an enabled
   * control carries its own trigger handler and its own tile/label styling, and a
   * disabled one carries neither — so a click during the lock submits nothing, and
   * nothing about the control set is destroyed or recreated while a timeline
   * plays.
   */
  private presentCastControlsEnabled(enabled: boolean): void {
    for (const control of this.castControls) {
      if (enabled) {
        control.tile.setFillStyle(control.enabledFill);
        control.tile.setStrokeStyle(1, control.enabledStroke);
        control.label.setColor(CAST_CONTROL_ENABLED_TEXT);
        control.tile.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);
        control.label.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);
        control.tile.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, control.onTrigger);
        control.label.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, control.onTrigger);
        continue;
      }

      control.tile.setFillStyle(0x0f172a);
      control.tile.setStrokeStyle(1, 0x334155);
      control.label.setColor(CAST_CONTROL_DISABLED_TEXT);
      control.tile.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);
      control.label.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);
    }
  }

  /**
   * Applies the authoritative board to the persistent gem views — the **PD-8
   * interpolation** from the previously rendered board to the authoritative
   * board.
   *
   * The views are paired with the destination cells by **within-column relative
   * order**: for each column, top to bottom, the view occupying the i-th
   * destination cell pairs with the i-th destination cell of that column, and
   * each is placed there showing that cell's delivered content. The cells are
   * addressed in the board's only coordinate convention (`MATCH3_RULES.md` §1.0):
   * cell at index `i` is `row = floor(i / 8)`, `column = i % 8`.
   *
   * **What the pairing is, and what it is not.** It is a *visual heuristic* for
   * deciding which already-rendered sprite travels to which cell, and explicitly
   * **not** a server-guaranteed Gem identity mapping — the wire carries no Gem
   * instance identity, so none exists to honour. What it does guarantee is what
   * the client can honestly guarantee: every destination cell ends up showing
   * its own delivered content (`GAME_STATE.md` §2.1, `SIGNALR_PROTOCOL.md` §4.1
   * item 5), so the board converges on the authoritative board exactly.
   *
   * **What this never does** (`PD-1`, `PD-8`, `AGENTS.md` §10):
   *
   * ```text
   * no gravity destination   `MATCH3_RULES.md` §4.4's compaction rule is not
   *                          applied to compute which cell a Gem falls to — in
   *                          particular survivors are never anchored to the
   *                          lowest cells of a column, which would be exactly
   *                          that rule
   * no spawned identity      no Gem identity is inferred or generated; every
   *                          written value is `board.cells[index]` as delivered
   * no match / cascade       nothing is detected, cleared, or evaluated
   * no intermediate board    no per-pass frame is constructed; the only board
   *                          this can converge on is the delivered one
   * ```
   *
   * `animate` plays the transition — a Gem whose delivered content differs from
   * what its cell rendered enters from just above that same cell, whose
   * destination is therefore still read from the board rather than computed —
   * while `false` writes in place, which is what a fresh board, a recovery
   * snapshot, and a convergence after an abort use.
   */
  private renderBoard(board: RuntimeBoard | null, animate = false): void {
    if (!this.boardLayer) {
      return;
    }

    if (board === null) {
      // No authoritative board at all: there is nothing to converge on, so the
      // views are released rather than left showing a battle that is over. The
      // layer holds nothing but the views.
      this.gemViews = [];
      this.refillableViews = [];
      this.boardLayer.removeAll(true);
      this.settledBoardSequence = null;
      this.settledBattleId = null;
      this.boardMessageText?.setText(this.boardMessage);
      // With no board there is no cell to mark, so the ring cannot be left
      // showing a selection on a board that is gone.
      this.renderSelection();
      return;
    }

    const cells = board.cells;

    // No view may be left animating towards a position this write overrides.
    this.killGemViewTweens();

    // The views are created in §1.0 index order, so the layer's child order stays
    // "cell 0's pair, cell 1's pair, …" — the order a renderer draws in, and the
    // order the board's own presentation is read in.
    for (let index = 0; index < BOARD_ROWS * BOARD_COLUMNS; index++) {
      const cell = cells[index];

      if (cell === undefined || this.gemViews[index]) {
        // A payload carrying fewer cells than the documented 8 x 8 board cannot
        // place the cells it does not carry; such a payload is rejected by the
        // runtime before it reaches the scene.
        continue;
      }

      // A cell a delivered Fall emptied is given back the view that step pooled, so
      // an abort, a recovery or a superseding push between a pass's two steps
      // restores the board on the same persistent views instead of allocating a new
      // pair (`SIGNALR_PROTOCOL.md` §3.2.28 item 3). Only a view no cell still owns
      // is handed out (`takePooledView`), so this transition can never map two
      // coordinates onto one sprite.
      const pooled = this.takePooledView();

      if (pooled) {
        this.drawGemContent(pooled, cell);
        this.gemViews[index] = pooled;
        continue;
      }

      this.gemViews[index] = this.createGemView(cell);
    }

    // A pooled view the board did not need holds nothing the delivered board
    // reports: it is released with the layer rather than left invisible in it, so a
    // battle's board layer never accumulates objects a resolution abandoned. A view
    // a cell still occupies is left alone — the layer's views are the board, and the
    // pool is not allowed to outrank the occupant map.
    for (const leftover of this.refillableViews) {
      if (this.gemViews.includes(leftover)) {
        continue;
      }

      leftover.tile.destroy();
      leftover.label.destroy();
    }
    this.refillableViews = [];

    // The PD-8 pairing: within each column, top to bottom, each view is paired
    // with the destination cell it occupies.
    for (let column = 0; column < BOARD_COLUMNS; column++) {
      for (let row = 0; row < BOARD_ROWS; row++) {
        const index = row * BOARD_COLUMNS + column;
        const cell = cells[index];
        const view = this.gemViews[index];

        if (cell === undefined || !view) {
          continue;
        }

        // A view a delivered clear emptied during this resolution counts as a
        // content change whatever the delivered cell holds: the board this
        // transition applies is the resolution's committed board, and every cell
        // it reports is re-labelled from it — the cleared ones entering from
        // above (`placeGemView`'s refill entrance). `cleared` is written only by
        // `clearConfirmedCell`, from an event the contract delivered.
        const changed = view.cleared || view.content !== gemContentKey(cell);
        if (changed) {
          this.drawGemContent(view, cell);
        }

        this.placeGemView(view, index, animate && changed);
      }
    }

    this.settledBoardSequence = this.currentBattleState?.sequence ?? null;
    this.settledBattleId = this.currentBattleState?.battleId ?? null;
    this.boardMessageText?.setText(this.boardMessage);

    // The board on screen has just changed, so the selection treatment is
    // re-derived from `selectedCell` against the views that now exist: it marks
    // the selected cell's own cell, and marks nothing at all when the delivered
    // board no longer carries it (`TASK-246` §3).
    this.renderSelection();
  }

  /**
   * Creates one cell's persistent view: the tile and its label, drawn at the
   * cell's own position and added to the board layer in §1.0 index order, so the
   * layer's child order stays "cell 0's pair, cell 1's pair, …".
   */
  private createGemView(cell: RuntimeCell): GemView {
    const tile = this.add.rectangle(0, 0, CELL_SIZE, CELL_SIZE, GEM_PRESENTATION[cell.gemType]?.color ?? 0x475569);
    const label = this.add
      .text(0, 0, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '14px',
        color: '#0b0f19',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    this.boardLayer?.add([tile, label]);

    const view: GemView = { tile, label, content: '', cleared: false };
    this.drawGemContent(view, cell);

    return view;
  }

  /**
   * Writes one delivered cell's content onto a view: its Gem colour and its
   * label.
   *
   * Every value comes from the delivered cell. An unrecognised Gem name is drawn
   * as an inert placeholder — the scene never substitutes a valid-looking Gem,
   * because that would fabricate authoritative board content
   * (`SIGNALR_PROTOCOL.md` §4 item 10).
   *
   * It also **restores** a view a delivered clear had emptied (TASK-275): the
   * tiles a `cleared` flag hid are shown again, and the flag is dropped, so a
   * cell's appearance is always the delivered cell's and a cleared cell can
   * never stay empty after the board has been converged. The gem that arrives is
   * `board.cells[index]` as delivered — the scene chooses nothing.
   */
  private drawGemContent(view: GemView, cell: RuntimeCell): void {
    const presentation = GEM_PRESENTATION[cell.gemType] ?? { color: 0x475569, label: '?' };

    view.tile.setFillStyle(presentation.color);
    view.tile.setStrokeStyle(1, 0x0b0f19);
    view.label.setFontSize(14);
    view.label.setText(presentation.label);

    view.tile.setVisible(true);
    view.label.setVisible(true);
    view.cleared = false;
    view.content = gemContentKey(cell);
  }

  /**
   * Places one view on its paired destination cell — the cell it occupies in the
   * rendered board, which is the cell whose delivered content it now shows.
   *
   * With `animate`, the view enters from `enterFrom` pixels above *that same cell*:
   * the destination is read from the board, never computed, so this is a settle into
   * a cell rather than a gravity fall between cells. The distance is the caller's
   * because it names the same delivered cell for two different entrances — a
   * re-labelled cell (`GEM_REFILL_ENTER`, the settle convergence) and a Gem a pass's
   * `spawnedGems` placed (`GEM_SPAWN_ENTER`, the Refill step,
   * `SIGNALR_PROTOCOL.md` §3.2.28 item 4).
   */
  private placeGemView(
    view: GemView,
    index: number,
    animate: boolean,
    enterFrom = GEM_REFILL_ENTER
  ): void {
    const { x, y } = BattleScene.cellCoordinates(index);

    if (!animate || !this.tweens) {
      view.tile.setPosition(x, y);
      view.label.setPosition(x, y);
      return;
    }

    this.trackPhaseAnimation(GEM_REFILL_MS);
    view.tile.setPosition(x, y - enterFrom);
    view.label.setPosition(x, y - enterFrom);

    this.tweens.add({
      targets: [view.tile, view.label],
      y,
      duration: GEM_REFILL_MS,
      ease: 'Sine.easeOut',
    });
  }

  /** Releases every view tween, so a position write is never fought by one. */
  private killGemViewTweens(): void {
    if (!this.tweens) {
      return;
    }

    for (const view of this.gemViews) {
      if (!view) {
        continue;
      }

      this.tweens.killTweensOf(view.tile);
      this.tweens.killTweensOf(view.label);
    }
  }

  // ---------------------------------------------------------------------------
  // Swap input (MATCH3_RULES.md §2 item 1; SIGNALR_PROTOCOL.md §2.1)
  // ---------------------------------------------------------------------------

  /**
   * Registers the two-tap Swap interaction on the existing board layer.
   *
   * Phaser delivers a pointer event with the container's local coordinates, so
   * the tapped cell is resolved with the inverse of the same mapping
   * `renderBoard` uses — one coordinate convention, used in both directions
   * (`MATCH3_RULES.md` §1.0). A tap that falls in a gap between cells, or
   * outside the board, resolves to no cell and is ignored.
   *
   * The handler is registered **at most once**. The board layer's own views are
   * updated on every authoritative push (`renderBoard`) but the layer is created
   * once and never emptied while a board exists, and the scene can be re-entered,
   * so a registration that assumed "one call per draw" would accumulate listeners
   * and submit one gesture's Swap several times. Detaching the event's listeners
   * before attaching makes the call idempotent without changing the lifecycle
   * pattern — no new abstraction is introduced, and the board layer is the only
   * object that listens here, so removing this event's listeners cannot detach
   * anyone else's handler.
   */
  private registerBoardInput(): void {
    if (!this.boardLayer) {
      return;
    }

    // Idempotent: any previously registered board pointer listener is removed
    // first, so a repeated call leaves exactly one — never handler x 2.
    this.boardLayer.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);

    this.boardLayer.on(
      Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN,
      (pointer: Phaser.Input.Pointer) => {
        this.onCellTapped(BattleScene.cellIndexAt(pointer.x, pointer.y));
      }
    );
  }

  /**
   * Maps a board-layer-local point to a §1.0 cell index, or `null` when the
   * point is not inside a cell.
   *
   * The board layer is a container positioned at the scene origin, so a pointer
   * event's local coordinates are already the same space the gem views are drawn
   * in — the mapping below is the exact inverse of `cellCoordinates`'
   * `x = BOARD_ORIGIN_X + column * pitch + CELL_SIZE / 2`
   * (`MATCH3_RULES.md` §1.0 provides the index; this is only its presentation
   * inverse).
   *
   * This is presentation geometry only. It decides no gameplay fact: it answers
   * "which drawn cell did the player touch", and an out-of-cell point yields no
   * index rather than a clamped or guessed one
   * (`MATCH3_RULES.md` §2.1.3 item 2 — nothing is clamped or reinterpreted).
   */
  private static cellIndexAt(x: number, y: number): number | null {
    const pitch = CELL_SIZE + CELL_GAP;
    const localX = x - BOARD_ORIGIN_X;
    const localY = y - BOARD_ORIGIN_Y;

    if (localX < 0 || localY < 0) {
      return null;
    }

    const column = Math.floor(localX / pitch);
    const row = Math.floor(localY / pitch);

    if (row >= BOARD_ROWS || column >= BOARD_COLUMNS) {
      return null;
    }

    // Inside the cell's own square, not in the gap that follows it.
    if (localX - column * pitch > CELL_SIZE || localY - row * pitch > CELL_SIZE) {
      return null;
    }

    return row * BOARD_COLUMNS + column;
  }

  /**
   * Handles one tap on a board cell.
   *
   * Local interaction behavior only (`SIGNALR_PROTOCOL.md` §2.1 item 1):
   *
   * - no cell (tap outside the board) — ignored;
   * - first cell — selected, and rendered as selected on the board itself
   *   (`renderSelection`);
   * - second tap on the selected cell — selection cleared, nothing sent;
   * - second cell — the pair is submitted through the runtime port.
   *
   * A tap that arrives while the guard is engaged does nothing at all — it
   * neither selects the tapped cell nor keeps a selection alive, because a lock
   * only ever engages after `clearSelection()` has already dropped one (see the
   * selection lifecycle in `clearSelection`'s callers).
   *
   * The scene performs no gameplay validation: it never checks adjacency, never
   * looks for a match, and never decides whether the swap is legal. Adjacency is
   * `MATCH3_RULES.md` §2.1.2's server-side check, and the server's answer is what
   * this scene renders.
   */
  private onCellTapped(cellIndex: number | null): void {
    if (cellIndex === null || this.isInputLocked()) {
      return;
    }

    if (this.selectedCell === null) {
      this.selectedCell = cellIndex;
      this.renderSelection();
      this.renderSwapStatus();
      return;
    }

    if (this.selectedCell === cellIndex) {
      this.clearSelection();
      this.renderSwapStatus();
      return;
    }

    const fromCell = this.selectedCell;
    this.clearSelection();

    void this.submitSwap(fromCell, cellIndex);
  }

  /**
   * Draws the persistent selected-cell treatment from `selectedCell` alone.
   *
   * It is the board's own statement that a cell is selected, and it reads
   * nothing else: no Gem, no match, no delivered value, no gameplay meaning
   * (`TASK-246` §3 — selection presentation is UI-only). Three cases:
   *
   * ```text
   * nothing selected            the ring is hidden
   * a selected cell the         the ring is hidden — the treatment follows the
   *   rendered board no           authoritative board, so it never marks a cell
   *   longer carries              the delivered board does not contain
   * a selected cell of the      the ring is moved onto that cell and shown
   *   rendered board
   * ```
   *
   * It writes visibility and position on **one** persistent object; it creates,
   * destroys, and redraws nothing, so a selection can never disturb the board or
   * a match/cascade overlay (`SELECTION_RING_*`).
   */
  private renderSelection(): void {
    const ring = this.selectionRing;
    if (!ring) {
      return;
    }

    const index = this.selectedCell;
    if (index === null || !this.gemViews[index]) {
      ring.setVisible(false);
      return;
    }

    const { x, y } = BattleScene.cellCoordinates(index);
    ring.setPosition(x, y);
    ring.setVisible(true);
  }

  /**
   * Ends the current selection and its treatment.
   *
   * It is the **single** way a selection ends, and every path that ends one
   * calls it: a same-cell re-tap, a pair submission, a swap rejection or
   * transport failure, the input guard engaging (a submitted action, or a
   * resolution's timeline starting), timeline supersession or completion, a
   * delivered board for a different battle or a null state, and scene teardown
   * (`shutdown` drops the ring with it).
   *
   * That is what makes a stale selection impossible: `selectedCell` is never
   * left holding a cell across a gesture, a lock, or a board change, so no
   * later tap can pair with one and submit a Swap the player did not intend
   * (`TASK-246` §3).
   */
  private clearSelection(): void {
    this.selectedCell = null;
    this.renderSelection();
  }

  /**
   * Submits one Swap request through the runtime port and presents the result.
   *
   * The scene calls the runtime and nothing else: it does not touch
   * `SignalRService`, the hub, or the board (`ARCHITECTURE.md` §2.2 rule 3).
   *
   * **The three outcomes are three different things (`P0.3` §5).**
   *
   * ```text
   * accepted: true          the request was resolved; the scene changes no board,
   *                         turn, or sequence value and the next authoritative
   *                         push plus that resolution's batch present it
   *                         (`SIGNALR_PROTOCOL.md` §3.1, §4)
   * accepted: false         a definitive server rejection: a gameplay no-op that
   *                         emits no events (`MATCH3_RULES.md` §2.1.5,
   *                         `GAME_EVENTS.md` §1.2). Nothing is retried, the
   *                         machine-readable reason is feedback only, and the
   *                         board and every timeline-owned readout are rolled
   *                         back onto the newest authoritative state the runtime
   *                         holds (`SIGNALR_PROTOCOL.md` §5 item 2)
   * RuntimeActionOutcomeUnknownError
   *                         the request was submitted and never answered, so
   *                         whether it committed is **unknown**. The scene
   *                         assumes nothing: the swap's own pair stays the
   *                         committed gesture and the resolution stays pending
   *                         while the runtime re-establishes synchronization
   *                         (§7), and the recovery snapshot is what converges
   *                         the scene and releases the guard
   * any other failure       the runtime refused the request before submitting it
   *                         (no battle known, an unimplemented action, an answer
   *                         for a battle that is no longer active): nothing was
   *                         sent, so nothing is outstanding
   * ```
   *
   * The two requested cells are kept as the committed Swap
   * (`committedSwapCells`). They are the player's own gesture, recorded so the
   * resolution's timeline can present the Swap itself as its first phase, and they
   * are dropped when **that Swap's own phase plays** — not when this request's
   * acknowledgement settles, because the two travel on different paths
   * (`SIGNALR_PROTOCOL.md` §3.1). A rejection, which emits no events at all
   * (`GAME_EVENTS.md` §1.2), drops them here so no later resolution can present a
   * swap movement the server refused; an unconfirmed outcome keeps them, because
   * that resolution may still arrive.
   *
   * **A continuation that no longer belongs to the active scene does nothing.**
   * The answer can arrive after a recovery converged the state, after the battle
   * was replaced, or after the scene was torn down, so every write below is
   * gated on {@link isCurrentSubmission}: a stale answer cannot report feedback
   * for the current battle, cannot clear a newer gesture's committed pair, and
   * cannot release a lock another submission owns (`P0.3` §5).
   */
  private async submitSwap(fromCell: number, toCell: number): Promise<void> {
    if (!this.acceptsPlayerAction()) {
      return;
    }

    if (!this.runtime) {
      this.renderSwapStatus('Swap unavailable: no runtime is connected.');
      return;
    }

    const submission = this.beginSubmission();
    this.transitionOrchestration('submitted');
    // Two facts, not one (TASK-254 AF-5): the request is unanswered, and the
    // resolution it was accepted for has yet to be presented and reconciled. The
    // first is cleared by the acknowledgement; the second only by the timeline
    // (or by a rejection, which emits no events at all).
    this.submissionOutstanding = true;
    this.resolutionPending = true;
    this.committedSwapCells = { from: fromCell, to: toCell };
    // The gesture is over the instant its pair is submitted, and the guard is
    // engaged for as long as the request and its resolution take — so the
    // selection must not survive either (TASK-246 §3).
    this.clearSelection();
    this.renderSwapStatus();
    this.renderPhaseIndicator();

    try {
      // The pair is sent as selected, and both cells are §1.0 indices
      // (`MATCH3_RULES.md` §2.1.1). The runtime sources the battle id and the
      // correlation id; the scene supplies only the two cells the player chose.
      const acknowledgement = await this.runtime.requestAction({
        kind: RUNTIME_ACTION_SWAP,
        fromCell,
        toCell,
      });

      if (!this.isCurrentSubmission(submission)) {
        return;
      }

      this.renderSwapStatus(undefined, fromCell, toCell, acknowledgement);

      if (!acknowledgement.accepted) {
        // A rejected Swap is a gameplay no-op that emits no events
        // (`MATCH3_RULES.md` §2.1.5 item 6, `GAME_EVENTS.md` §1.2): nothing may
        // be left selected for the player's next tap to pair with, no timeline
        // will ever come to present the pair, and no resolution is pending.
        this.clearSelection();
        this.committedSwapCells = null;
        this.resolutionPending = false;
        // The rollback: the board and every timeline-owned readout are restored
        // to the authoritative state, so a refused gesture leaves nothing behind
        // on screen.
        this.rollbackToAuthoritativeState();
      }
    } catch (error) {
      if (!this.isCurrentSubmission(submission)) {
        return;
      }

      this.clearSelection();

      if (error instanceof RuntimeActionOutcomeUnknownError) {
        // Submitted, never answered: the swap may have committed, so nothing is
        // rolled back and the guard is kept until the runtime's authoritative
        // recovery converges the scene (`markActionOutcomeUnknown` already put
        // the runtime in `unsynchronized`, which is what the synchronization
        // indicator is presenting).
        this.renderSwapStatus(
          `Swap ${fromCell} -> ${toCell} unconfirmed: the server's answer was not ` +
            'received; synchronizing with the server.',
          undefined,
          undefined,
          undefined,
          '#fbbf24'
        );
      } else {
        // A failed request is a transport/runtime failure — presentation state,
        // not game state (`SIGNALR_PROTOCOL.md` §8.3) — and it produced no
        // acknowledgement because the runtime never submitted it. No board value
        // changes, nothing is retried, and the request's own batch never arrives.
        this.committedSwapCells = null;
        this.resolutionPending = false;
        this.renderSwapStatus(
          `Swap not sent: ${error instanceof Error ? error.message : String(error)}`
        );
      }
    } finally {
      // Another submission may have replaced this one — a newer gesture, or a
      // recovery that already released the guard. A superseded continuation does
      // not settle the current one.
      if (this.isCurrentSubmission(submission)) {
        this.onActionSettled();
        this.renderPhaseIndicator();
      }
    }
  }

  /**
   * Opens one action submission and returns the token its continuation is bound
   * to: the submission's serial, and the battle the request was addressed to.
   *
   * A newer submission, a recovery convergence, a new battle, or scene teardown
   * moves one half of the pair, and {@link isCurrentSubmission} then answers
   * `false` for the older one — so the older answer cannot write into the
   * active scene (`P0.3` §5).
   */
  private beginSubmission(): { readonly serial: number; readonly battleId: string | null } {
    this.submissionSerial += 1;

    return {
      serial: this.submissionSerial,
      battleId: this.currentBattleState?.battleId ?? null,
    };
  }

  /**
   * Whether one submission's continuation is still the active scene's: its serial
   * is the newest, and the battle it addressed is the one on screen.
   *
   * Both halves matter. The serial rules out an answer that a recovery, a newer
   * gesture, or a scene run has already superseded; the battle id rules out an
   * answer for a battle that is no longer active, which is the one cross-battle
   * case the runtime's own guard cannot report as anything but a rejection.
   */
  private isCurrentSubmission(submission: {
    readonly serial: number;
    readonly battleId: string | null;
  }): boolean {
    return (
      submission.serial === this.submissionSerial &&
      (this.currentBattleState?.battleId ?? null) === submission.battleId
    );
  }

  /**
   * Presents the Swap interaction's own feedback: the current selection, or the
   * outcome of the last submitted request.
   *
   * This is transport feedback (`SIGNALR_PROTOCOL.md` §2.1, §5) — not resolution
   * presentation. It reports no match, cascade, combo, damage, or boss value,
   * because the client computes none of those.
   *
   * A caller supplying `message` may also supply its colour: a failure the client
   * knows about (the red default) and an outcome it does **not** know (the
   * warning colour an unconfirmed submission uses) are different statements, and
   * the line must not present them alike (`P0.3` §5).
   */
  private renderSwapStatus(
    message?: string,
    fromCell?: number,
    toCell?: number,
    acknowledgement?: { readonly accepted: boolean; readonly reason?: string | null },
    messageColor = '#f87171'
  ): void {
    if (!this.swapText) {
      return;
    }

    if (message !== undefined) {
      this.swapText.setText(message);
      this.swapText.setColor(messageColor);
      return;
    }

    if (acknowledgement) {
      if (acknowledgement.accepted) {
        // The request was accepted. The board is NOT changed here: the next
        // authoritative push re-renders it (SIGNALR_PROTOCOL.md §3.1, §4).
        this.swapText.setText(
          `Swap ${fromCell} -> ${toCell}: accepted. Awaiting the server's state push.`
        );
        this.swapText.setColor('#34d399');
      } else {
        // Rejected: nothing happened, and the reason is the server's own
        // machine-readable code, shown as received (§5 items 2–3).
        const friendlyReason = formatSwapRejection(acknowledgement.reason);
        this.swapText.setText(
          `Swap ${fromCell} -> ${toCell}: rejected (${acknowledgement.reason ?? 'unknown'}).`
        );
        this.swapText.setColor('#fbbf24');
        this.showCallout(friendlyReason, '#fbbf24');
      }
      return;
    }

    if (this.submissionOutstanding) {
      this.swapText.setText('Swap request in flight…');
      this.swapText.setColor('#94a3b8');
      return;
    }

    this.swapText.setText(
      this.selectedCell === null
        ? 'Select a cell to swap.'
        : `Cell ${this.selectedCell} selected — select a neighbour.`
    );
    this.swapText.setColor('#94a3b8');
  }

  /**
   * Submits one CardCast request through the runtime port and presents the result.
   */
  private async submitCardCast(cardId: string): Promise<void> {
    if (!this.acceptsPlayerAction()) {
      return;
    }

    if (!this.runtime) {
      this.renderCastStatus('CardCast unavailable: no runtime is connected.');
      return;
    }

    const submission = this.beginSubmission();
    this.transitionOrchestration('submitted');
    this.submissionOutstanding = true;
    this.resolutionPending = true;
    // A cast engages the same guard a Swap does, so a board selection cannot be
    // left holding a cell across it — a later tap must start a new gesture
    // rather than complete the one the guard interrupted (TASK-246 §3).
    this.clearSelection();
    this.renderCastStatus(`CardCast ${cardId} in flight…`);
    this.renderPhaseIndicator();

    try {
      const acknowledgement = await this.runtime.requestAction({
        kind: RUNTIME_ACTION_CARD_CAST,
        cardId,
      });

      if (!this.isCurrentSubmission(submission)) {
        return;
      }

      if (!acknowledgement.accepted) {
        // A rejected cast is a gameplay no-op that emits no events
        // (`GAME_EVENTS.md` §1.2, `SIGNALR_PROTOCOL.md` §2): no resolution is
        // coming, so the guard is not held waiting for one (TASK-254 AF-5), and
        // the board and every timeline-owned readout are rolled back onto the
        // authoritative state (P0.3 §5).
        this.resolutionPending = false;
        this.rollbackToAuthoritativeState();
      }

      this.renderCastStatus(undefined, cardId, false, acknowledgement);
    } catch (error) {
      if (!this.isCurrentSubmission(submission)) {
        return;
      }

      if (error instanceof RuntimeActionOutcomeUnknownError) {
        // Submitted, never answered: the cast may have committed, so nothing is
        // rolled back and the resolution stays pending until the runtime's
        // authoritative recovery converges the scene.
        this.renderCastStatus(
          `CardCast ${cardId} unconfirmed: the server's answer was not received; ` +
            'synchronizing with the server.',
          cardId,
          false,
          undefined,
          '#fbbf24'
        );
      } else {
        this.resolutionPending = false;
        this.renderCastStatus(
          `CardCast ${cardId} not sent: ${error instanceof Error ? error.message : String(error)}`
        );
      }
    } finally {
      if (this.isCurrentSubmission(submission)) {
        this.onActionSettled();
        this.renderPhaseIndicator();
      }
    }
  }

  /**
   * Submits one PetSkillCast request through the runtime port and presents the result.
   *
   * Its three outcomes are the Swap's (`submitSwap`): an accepted cast awaits the
   * authoritative push, a rejected one is a definitive no-op that converges the
   * board on the authoritative state, and an **unanswered** one is neither — the
   * cast may have committed, so the resolution stays pending and the runtime's
   * recovery is what settles the scene (P0.3 §5).
   */
  private async submitPetSkillCast(): Promise<void> {
    if (!this.acceptsPlayerAction()) {
      return;
    }

    if (!this.runtime) {
      this.renderCastStatus('PetSkillCast unavailable: no runtime is connected.');
      return;
    }

    const submission = this.beginSubmission();
    this.transitionOrchestration('submitted');
    this.submissionOutstanding = true;
    this.resolutionPending = true;
    // Same guard, same consequence as a CardCast: no selection survives it.
    this.clearSelection();
    this.renderCastStatus('PetSkillCast in flight…');
    this.renderPhaseIndicator();

    try {
      const acknowledgement = await this.runtime.requestAction({
        kind: RUNTIME_ACTION_PET_SKILL_CAST,
      });

      if (!this.isCurrentSubmission(submission)) {
        return;
      }

      if (!acknowledgement.accepted) {
        // As for a CardCast: a rejection resolves nothing and emits nothing.
        this.resolutionPending = false;
        this.rollbackToAuthoritativeState();
      }

      this.renderCastStatus(undefined, undefined, true, acknowledgement);
    } catch (error) {
      if (!this.isCurrentSubmission(submission)) {
        return;
      }

      if (error instanceof RuntimeActionOutcomeUnknownError) {
        this.renderCastStatus(
          'PetSkillCast unconfirmed: the server\'s answer was not received; ' +
            'synchronizing with the server.',
          undefined,
          true,
          undefined,
          '#fbbf24'
        );
      } else {
        this.resolutionPending = false;
        this.renderCastStatus(
          `PetSkillCast not sent: ${error instanceof Error ? error.message : String(error)}`
        );
      }
    } finally {
      if (this.isCurrentSubmission(submission)) {
        this.onActionSettled();
        this.renderPhaseIndicator();
      }
    }
  }

  /**
   * Presents cast interaction transport feedback: in flight, accepted, or rejected
   * with server machine-readable reason (SIGNALR_PROTOCOL.md §2, §5).
   *
   * `messageColor` overrides the message caption's colour for a caller that has
   * something other than a failure to report — the unconfirmed-submission line
   * uses the warning colour, because an outcome the client does not know is not
   * the same statement as one it does (`P0.3` §5).
   */
  private renderCastStatus(
    message?: string,
    cardId?: string,
    isSkill?: boolean,
    acknowledgement?: { readonly accepted: boolean; readonly reason?: string | null },
    messageColor?: string
  ): void {
    if (!this.castText) {
      return;
    }

    if (message !== undefined) {
      this.castText.setText(message);
      this.castText.setColor(
        messageColor ?? (message.includes('in flight') ? '#94a3b8' : '#f87171')
      );
      return;
    }

    if (acknowledgement) {
      const actionName = isSkill ? 'PetSkillCast' : `CardCast ${cardId}`;
      if (acknowledgement.accepted) {
        this.castText.setText(`${actionName}: accepted. Awaiting the server's state push.`);
        this.castText.setColor('#34d399');
      } else {
        const friendlyReason = formatCardCastRejection(acknowledgement.reason);
        this.castText.setText(
          `${actionName}: rejected (${acknowledgement.reason ?? 'unknown'}).`
        );
        this.castText.setColor('#fbbf24');
        this.showCallout(friendlyReason, '#fbbf24');
      }
      return;
    }

    this.castText.setText('');
  }

  /**
   * Handles server-authoritative battle events delivered via the runtime port.
   *
   * One delivered batch is one resolved action, and it is presented as one
   * ordered timeline (`playEventBatch`). The steps here are all synchronous, so
   * what this scene has *received* never lags behind the batch:
   *
   * ```text
   * parse + log     every event is parsed and its developer-diagnostic line
   *                 appended to `presentedEventsLog` in delivered order, before
   *                 anything is animated (TASK-209 §5)
   * outcome         a terminal BattleWon / BattleLost is recorded as the
   *                 timeline's handoff — it is the timeline's last phase and it
   *                 is also performed by any path that ends the timeline, so
   *                 the handoff can never be lost behind a phase boundary
   * hold released   values a previous push held for *this* batch, if it is not
   *                 this batch's resolution, are converged first
   * timeline        the phases are built from the batch as delivered and played
   * ```
   *
   * It performs no result calculation: the server is authoritative for the
   * outcome and terminal HP values (GAME_RULES.md §18, ADR-001, AGENTS.md §10).
   *
   * The handoff also carries the batch's `battleId` (SIGNALR_PROTOCOL.md §3),
   * which is what `ResultScene` addresses the documented result endpoint with to
   * read the persisted reward summary (API_CONTRACTS.md §4). The outcome members
   * themselves are unchanged; the id is transport metadata the envelope already
   * carries, not a gameplay value.
   *
   * **Defense in depth: a batch is admitted before anything is built.** The
   * runtime already withholds a batch it cannot place against its synchronized
   * state (its delivery-integrity check), and this is the scene's own half of the
   * same invariant — a resolution may only be animated against the authoritative
   * state that represents it. A batch the held state contradicts — a foreign
   * battle's, or one ahead of (or behind) the delivered `sequence` — is refused
   * whole: no timeline, no callout, no outcome handoff, no accepted resolution,
   * no settled-board stamp (`admitsBatch`).
   */
  private handleBattleEvents(envelope: BattleEventsEnvelope): void {
    if (this.outcomeHandled) {
      return;
    }

    if (!this.admitsBatch(envelope)) {
      // A batch the held authoritative state does not represent is refused whole
      // (`admitsBatch`). Nothing is built and nothing is booked:
      //
      //   no timeline          no resolution is animated over a board that does
      //                        not represent it
      //   no callout           no combat feedback and no Boss Enrage line is
      //                        raised for a resolution the scene cannot place
      //   no outcome handoff   `pendingOutcomeHandoff` is not written, so an
      //                        unrepresentable batch can never end the battle on
      //                        screen or hand `ResultScene` an outcome it did not
      //                        produce
      //   no accepted sequence `acceptedResolutionSequence` is not advanced
      //   no settled stamp     `settledBoardSequence` is not written
      //   no state rendered    the held state is not re-rendered from it
      //
      // It is not a release path either: a refused batch means the client is
      // desynchronized, and the runtime — which owns recovery orchestration — has
      // already marked `sync` unsynchronized and requested the §7.1 snapshot for
      // that gap (`SIGNALR_PROTOCOL.md` §6 item 3). The scene's locks are
      // released when that authoritative state arrives (`renderBattleState`), not
      // by inventing a local answer here. The scene never calls the transport or
      // asks for a snapshot of its own (`ARCHITECTURE.md` §2.2.1 rule 1).
      return;
    }

    const outcome = BattleScene.findOutcomeEvent(envelope.events);
    if (outcome) {
      this.pendingOutcomeHandoff = { ...outcome, battleId: envelope.battleId };
    }

    try {
      this.playEventBatch(envelope);
    } catch {
      // A malformed batch is presentation feedback, not a battle fact: the
      // timeline is abandoned, the guard is released, and the values it was
      // holding are converged on the authoritative state
      // (`SIGNALR_PROTOCOL.md` §8.3, `AGENTS.md` §10).
      this.abortPresentation('malformed-batch');
    }
  }

  /**
   * Whether one delivered batch may be presented at all — the scene's admission
   * gate before any timeline, callout or handoff is built (`TASK-273` §4.2).
   *
   * ```text
   * batch's battle ≠ the held battle   → refuse  cross-battle contamination
   * batch is ahead of the held state   → refuse  the state for that resolution
   *                                               never arrived
   * batch is at the held state         → admit   the documented push-then-batch
   *                                               delivery (SIGNALR_PROTOCOL.md
   *                                               §3.1, §6 item 4)
   * batch is behind the held state     → refuse  the held state already contains
   *                                               that resolution's result
   *                                               (GAME_STATE.md §5.1 item 3)
   * no authoritative state held        → refuse  no state to represent resolution
   *                                               (TASK-274 Review A)
   * ```
   *
   * **It compares two delivered versions and nothing else.** The held state is
   * the one the runtime pushed through the port; `sequence` is the
   * post-resolution counter (`GAME_STATE.md` §5), so `held.sequence` names exactly
   * the resolution the state represents. No gameplay value, board cell, HP,
   * combo, or event payload is read, and no rule is evaluated: the answer is
   * "does the authoritative state on screen represent this batch's resolution",
   * which is the only question a presentation may depend on
   * (`GAME_RULES.md` §18, `AGENTS.md` §10).
   *
   * **A batch behind the held state is refused rather than replayed.** The
   * delivered state at `sequence = n` is the complete result of the n-th
   * resolution (§5.1 item 3), so a batch for an earlier resolution describes a
   * resolution the state already contains — the snapshot-over-replay rule
   * (`ADR-008`) and the reason a recovery may never be followed by the animation
   * of the resolutions it superseded.
   *
   * **With no state held at all the batch is refused.** Under the documented
   * delivery order, the state push precedes any batch for that battle (§6 item 4),
   * and the runtime's own delivery-integrity check drops a batch for a battle it
   * does not hold (`ARCHITECTURE.md` §2.2.1 rules 1 and 4). The scene refuses an
   * unversioned batch when it holds no authoritative state, ensuring that unheld
   * batches cannot reach presentation (TASK-274 Review A).
   *
   * The second, independent memory (`acceptedResolutionSequence`) still governs
   * the repeat case inside an admitted batch: this gate answers "may this batch be
   * presented at all", and `playEventBatch` answers "has this exact resolution
   * already been presented".
   */
  private admitsBatch(envelope: BattleEventsEnvelope): boolean {
    const held = this.currentBattleState;

    if (held === null) {
      return false;
    }

    if (held.battleId !== envelope.battleId) {
      return false;
    }

    return envelope.serverSequence === held.sequence;
  }

  /**
   * Builds and plays one batch's presentation timeline.
   *
   * Everything that must be true *immediately* happens here, synchronously, so
   * that "what the scene received" is never waiting on an animation:
   *
   * ```text
   * 1. every event is parsed and its developer-diagnostic line recorded in
   *    `presentedEventsLog` — for development and tests, never rendered
   *    (TASK-209 §5);
   * 2. the ordered phase list is built from the events exactly as delivered, and
   *    the batch's single player-facing callout is bound to the phase that owns
   *    the event it was selected from — the most important event
   *    `selectBatchCalloutEvent` picks from the delivered events, so feedback stays
   *    selective rather than becoming a second event feed (TASK-210 §7), and it is
   *    raised with that phase rather than at batch arrival (TASK-254 AF-2);
   * 3. the timeline starts and plays it, holding the scene's input guard until its
   *    last phase has played (PD-2).
   * ```
   *
   * A batch that produces no phases — an unknown or malformed event set, or a
   * rejection (which emits no events at all, `GAME_EVENTS.md` §1.2) — produces
   * no timeline, raises no callout, and locks nothing. A batch already accepted, an
   * older one, and one superseded by a newer one present nothing and are recorded
   * only (`acceptedResolutionSequence`).
   */
  private playEventBatch(envelope: BattleEventsEnvelope): void {
    const presented: InBattleServerEvent[] = [];
    // Whether this batch's resolution is one the developer Move Log has not
    // recorded yet. A repeated or older `serverSequence` is still parsed and still
    // recorded in the scene's own diagnostic log below — the Move Log simply does
    // not present it a second time (TASK-253).
    const moveLog = this.developerMoveLog;
    const recordForMoveLog = moveLog !== null && moveLog.beginBatch(envelope);

    // The batch's board-action actor, tracked in the delivered order for the
    // panel's own rows. It is the same delivered member the phase builder reads
    // (`deliveredSwapOf`), so the Move Log's `actor/action` column reports the
    // same attribution the timeline presents instead of a second, side-free one.
    let boardActionActor: 'player' | 'boss' | null = null;

    for (const raw of envelope.events) {
      const parsed = parseInBattleEvent(raw);
      if (!parsed) {
        continue;
      }

      boardActionActor = deliveredSwapOf(parsed)?.actor ?? boardActionActor;

      presented.push(parsed);
      BattleScene.appendBounded(this.presentedEventsLog, formatInBattleEvent(parsed));

      if (recordForMoveLog) {
        moveLog?.recordEvent(parsed, envelope.serverSequence, boardActionActor);
      }
    }

    if (recordForMoveLog) {
      moveLog?.commitSequence(envelope.serverSequence);
    }

    // A resolution this scene has already accepted is presented no second time.
    // `serverSequence` is the post-resolution counter and is strictly increasing with
    // no gaps (`SIGNALR_PROTOCOL.md` §3 item 2, `GAME_STATE.md` §5), so a batch at or
    // below the newest one already accepted is a re-delivery or an older one — whether
    // or not its own timeline is still playing. The memory outlives the timeline, which
    // is what stops an old round's animations from being replayed after a newer
    // resolution has been accepted, and it is keyed by battle so a new battle's first
    // sequence is never mistaken for a repeat of the previous battle's (ADR-008:
    // recovery is a snapshot, never an event replay).
    if (this.acceptedResolutionBattleId !== envelope.battleId) {
      this.acceptedResolutionBattleId = envelope.battleId;
      this.acceptedResolutionSequence = null;
    }

    if (
      this.acceptedResolutionSequence !== null &&
      envelope.serverSequence <= this.acceptedResolutionSequence
    ) {
      // Already accepted, or older than what has been accepted: recorded above, and
      // presented no second time.
      return;
    }

    // A batch that arrives while an earlier one is still playing. The server's
    // own sequencing (`serverSequence` is the post-resolution counter,
    // `GAME_STATE.md` §5) makes the newer resolution the one that matters, and the
    // guard above has already established that this one is strictly newer:
    if (this.presentationTimeline !== null) {
      // Newer: the playing timeline is superseded, exactly as a newer
      // authoritative state supersedes it. Its pacing is cancelled and its guard
      // released before this batch's timeline takes over.
      this.abortPresentation('superseded-batch');

      if (this.outcomeHandled) {
        // The superseded batch carried the terminal outcome, which the abort has
        // already handed off. The battle is over; nothing further is presented.
        return;
      }
    }

    // A hold that belongs to a different resolution is not this batch's to
    // release: it is converged now rather than left waiting for a batch that has
    // already been superseded (`renderBattleState`).
    if (this.heldSequence !== null && this.heldSequence !== envelope.serverSequence) {
      this.settleHeldPresentation();
    }

    const phases = this.buildPresentationPhases(presented);

    // **Presentability is decided before the resolution is committed.** The
    // batch has been recorded and it is strictly newer than the newest accepted
    // one, but whether it can be *presented* is a separate question — and a batch
    // that produces no phase must not leave the scene waiting for a timeline that
    // will never exist (`TASK-273` §4.3).
    if (phases.length === 0) {
      // An empty or unrecognized phase set is accepted and deliberately
      // superseded: the delivered state already represents this resolution, so
      // the resolution is marked accepted — a re-delivery presents nothing a
      // second time — and **every lock the batch engaged is released**, because
      // nothing will ever come to release them. The held readouts are converged
      // on the authoritative state first, so the board on screen is the delivered
      // one rather than a mid-resolution one this batch could never finish.
      this.acceptedResolutionSequence = envelope.serverSequence;
      this.releaseUnpresentableResolution();
      return;
    }

    this.acceptedResolutionSequence = envelope.serverSequence;

    this.presentationTimeline = phases;
    this.presentationStep = 0;
    this.presentationSequence = envelope.serverSequence;
    this.presentationBattleId = envelope.battleId;
    // The client's own lifecycle: a timeline was built from this batch, before any
    // of it plays. It is recorded from the phase list the batch produced — one
    // delivered resolution, one row — and never from a duration.
    moveLog?.recordQueued(
      envelope.serverSequence,
      phases.map((phase) => phase.kind)
    );
    // A timeline is the guard engaged for a whole resolution: a cell selected
    // before it started must not survive it, or the player's next tap would
    // complete a gesture from before the resolution (TASK-246 §3).
    this.clearSelection();
    this.onTimelineStarted();
    this.advancePresentation();
  }

  /**
   * The content key of every §1.0 cell as the scene currently renders it, or `null`
   * for a cell whose view holds no content.
   *
   * It is the board on screen stated in the one vocabulary the board, a delivered
   * clear and a delivered board cell all share (`gemContentKey`), so "what this
   * cell renders" and "what the server says this cell holds" are the same kind of
   * value and can be compared without reading a rule or a gameplay value.
   */
  private renderedBoardContents(): Array<string | null> {
    return Array.from({ length: BOARD_ROWS * BOARD_COLUMNS }, (_, index) => {
      const view = this.gemViews[index];

      return view && !view.cleared && view.content !== '' ? view.content : null;
    });
  }

  /**
   * The content key of every §1.0 cell one **delivered board** carries, or `null`
   * when the payload does not carry the documented 8 × 8 board.
   *
   * The comparison target of `replayBoardDeltas`: the committed board a round's
   * state push delivered is the authoritative statement of where that round's
   * board work ended (`SIGNALR_PROTOCOL.md` §3.1 items 1–3, §4; `GAME_STATE.md`
   * §5.0 — the push precedes the batch and the batch's `serverSequence` and the
   * pushed `sequence` are two readings of one committed value).
   */
  private static deliveredBoardContents(
    board: RuntimeBoard | null
  ): Array<string | null> | null {
    if (!board || !Array.isArray(board.cells)) {
      return null;
    }

    const contents: Array<string | null> = [];

    for (let index = 0; index < BOARD_ROWS * BOARD_COLUMNS; index++) {
      const cell = board.cells[index];

      if (cell === undefined) {
        return null;
      }

      contents.push(gemContentKey(cell));
    }

    return contents;
  }

  /** Whether two content boards state the same content at every §1.0 cell. */
  private static sameBoardContents(
    left: readonly (string | null)[],
    right: readonly (string | null)[]
  ): boolean {
    return left.length === right.length && left.every((content, index) => content === right[index]);
  }

  /**
   * Replays one delivered batch's board deltas onto the board the scene renders,
   * in the delivered order, and reports the board they produce — or `null` when a
   * delivered delta cannot be placed on that board.
   *
   * **It is a verification, not a board of its own.** Every value it writes is a
   * member of a delivered event: the pair a `SwapStarted` names, the cells a
   * `MatchCreated`/`GemMatched` confirms cleared and their delivered `gemType`,
   * the `fromCell`/`toCell` pairs a `CascadeStepResolved` reports as its pass's
   * Gravity, and the `cellIndex`/`gemType` it reports as its pass's Spawn. It
   * computes no match, no cascade, no gravity destination, no spawned identity and
   * no board of its own; it reads no RNG and no gameplay value (`AGENTS.md`
   * §10/§11, `SIGNALR_PROTOCOL.md` §3.1 item 5, §3.2.28 item 8, `ADR-027`).
   *
   * It exists to answer one question a board action's own boundary cannot:
   * **is the board this cycle starts from the board on screen?** The contract
   * delivers no statement of the board an action was validated on
   * (`SIGNALR_PROTOCOL.md` §3.2.26 item 6), and the Boss's step-18a action is
   * validated on the board the Player's cycle left — which the scene is showing
   * once it has applied the Player's own cycle — *unless* the Boss AI's deadlock
   * recovery replaced that board with a generated one, an operation that emits
   * nothing at all (`ADR-026` §8, `GAME_EVENTS.md` §3 item 8). Replaying the
   * batch's own deltas onto the rendered board and asking whether they land on the
   * round's committed board decides exactly that, from delivered data alone: they
   * do when the scene holds the cycle's starting board, and they cannot when an
   * unreported reshuffle replaced it.
   *
   * ```text
   * returns null   a delivered clear names a cell whose rendered content is not the
   *                content the event confirms, a `dropMoves` origin holds no
   *                content, or a pass's Spawn does not fill every cell its Gravity
   *                left empty (MATCH3_RULES.md §4.5 item 1: the board a pass ends on
   *                is full)
   * returns board  the content every board delta leaves behind, in §1.0 order
   * ```
   *
   * **The order is the presentation's own order** (delivered order, and within a
   * pass: clears, then Gravity, then Spawn — `MATCH3_RULES.md` §5.8.3,
   * `SIGNALR_PROTOCOL.md` §3.2.28 item 5), so a verified replay is the order
   * `presentMatchDestruction` / `playGravityDrop` / `playRefillSpawn` apply, and
   * the clearing rule is the one `clearConfirmedCell` applies: a delivered clear
   * confirms a cell by its Gem type, and a cell its own pass already emptied is
   * confirmed rather than refused.
   */
  private static replayBoardDeltas(
    start: readonly (string | null)[],
    events: readonly InBattleServerEvent[]
  ): Array<string | null> | null {
    const contents = start.slice();
    const inRange = (index: number): boolean =>
      Number.isInteger(index) && index >= 0 && index < contents.length;

    /** Confirms and empties one cell, mirroring `clearConfirmedCell`. */
    const clear = (cellIndex: number, gemType: string): boolean => {
      if (!inRange(cellIndex)) {
        return false;
      }

      const current = contents[cellIndex];

      // A cell this pass's own clear already emptied (a Match's `cells` and that
      // cell's own `GemMatched` both name it) is confirmed.
      if (current === null) {
        return true;
      }

      if (current !== gemContentKeyFor(gemType)) {
        return false;
      }

      contents[cellIndex] = null;

      return true;
    };

    for (const event of events) {
      switch (event.type) {
        case 'SwapStarted': {
          const swap = deliveredSwapOf(event);

          if (!swap || !inRange(swap.fromCell) || !inRange(swap.toCell)) {
            return null;
          }

          const from = contents[swap.fromCell] ?? null;
          contents[swap.fromCell] = contents[swap.toCell] ?? null;
          contents[swap.toCell] = from;
          break;
        }

        case 'MatchCreated': {
          for (const cellIndex of event.cells) {
            if (!clear(cellIndex, event.gemType)) {
              return null;
            }
          }
          break;
        }

        case 'GemMatched': {
          if (!clear(event.cellIndex, event.gemType)) {
            return null;
          }
          break;
        }

        case 'CascadeStepResolved': {
          // Gravity moves the survivors the report names and nothing else: every
          // origin is read from the snapshot taken before the step and vacated
          // first, then every destination is filled, so a chain of moves applies as
          // the one step Gravity applied (`playGravityDrop`).
          const before = contents.slice();

          for (const move of event.dropMoves) {
            if (
              !inRange(move.fromCell) ||
              !inRange(move.toCell) ||
              (before[move.fromCell] ?? null) === null
            ) {
              return null;
            }
          }

          for (const move of event.dropMoves) {
            contents[move.fromCell] = null;
          }

          for (const move of event.dropMoves) {
            contents[move.toCell] = before[move.fromCell] ?? null;
          }

          // Spawn fills exactly the cells Gravity left empty
          // (`MATCH3_RULES.md` §4.5 item 1), so the pass ends on a full board.
          for (const gem of event.spawnedGems) {
            if (!inRange(gem.cellIndex)) {
              return null;
            }

            contents[gem.cellIndex] = gemContentKeyFor(gem.gemType);
          }

          if (contents.includes(null)) {
            return null;
          }

          break;
        }

        default:
          // Every other delivered event presents feedback, a readout or a combat
          // stage; none of them states a board delta.
          break;
      }
    }

    return contents;
  }

  /**
   * Builds the ordered phases of one delivered batch, in the batch's own order.
   *
   * The grouping is a single forward pass: every event is mapped by the one
   * delivered-event adapter (`adaptPresentedEvent`) and is placed on the phase it
   * belongs to, either extending the phase already open or opening the next one,
   * so the delivered order is preserved exactly and nothing is re-sorted,
   * filtered, or re-derived (`ARCHITECTURE.md` §2.2.1 rule 4,
   * `SIGNALR_PROTOCOL.md` §3.1 item 5). A committed board action, a Boss Skill, a
   * Boss Passive, a Relic trigger and a Card / Pet-Skill cast all reach their
   * phases through that same adapter call — there is no per-actor branch,
   * pipeline, registry, interface or event bus (`AGENTS.md` §9).
   *
   * The pass also carries the batch's **delivered board-action actor** forward —
   * the side the newest `SwapStarted` names (`deliveredSwapOf`) — and hands it to
   * that same adapter call, because no Match/Gem/Cascade/Combo event carries a
   * party member of its own (`SIGNALR_PROTOCOL.md` §3.2.6–§3.2.12). It is read
   * from the delivered member and never from the cycle's position in the batch, a
   * client-held turn, or a board difference, so a two-cycle round attributes each
   * cycle to the side that actually acted (`GAME_EVENTS.md` §1, ADR-026 §2).
   *
   * ```text
   * swap          one committed board action's exchange, opened by its own
   *               delivered `SwapStarted` (actor + the two §1.0 cells it named,
   *               `SIGNALR_PROTOCOL.md` §3.2.26) and closed by its `SwapResolved`.
   *               A batch that delivers no boundary keeps the pre-extension
   *               fallback: the pair this client submitted, side-free
   * match / cascade
   *               that action's own board cycle, in the delivered
   *               order of `GAME_EVENTS.md` §1.1: one phase per delivered
   *               `CascadeCreated.cascadeDepth` (MATCH3_RULES.md §4.2 item 2), with
   *               that pass's own Match / Gem feedback, each attributed to the
   *               actor the delivered boundary named
   * damage        the player's hit, classified from the delivered
   *               `source`/`target` (`SIGNALR_PROTOCOL.md` §3.2.14); it also owns
   *               the Boss `hp / maxHp` pair and the Enrage label/callout the pair
   *               drives
   * retaliation  the Boss's response, classified the same way; it owns the Pet
   *               `hp / maxHp` pair
   * cast / relic / boss-skill
   *               the delivered action's own step, attributed to the actor the
   *               adapter derived; a `boss-skill` step presents its own delivered
   *               `BossSkillCast`, so it is held for that presentation
   * settle        the gravity/refill interpolation onto the authoritative board
   *               (PD-1/PD-8), played where the board cycle ends — and only when
   *               this resolution delivered a board cycle at all
   * feedback      a readout / floater step — the delivered Combo, Power and
   *               Passive changes, and a damage direction the contract does not
   *               define — which joins the phase already open (or opens one when
   *               nothing is open) and is presented by it
   * outcome       the terminal BattleWon / BattleLost handoff, last
   * ```
   *
   * `DamageCalculated` and `SwapResolved` present nothing of their own: they stay
   * in the batch's developer log and in the batch's single callout, and add no
   * step to the timeline — the exchange `SwapResolved` closes is already the step
   * its `SwapStarted` opened.
   *
   * `MatchCreated.cascadeDepth` and `CascadeCreated.cascadeDepth` are **not the
   * same value** for one pass and are never equated (`SIGNALR_PROTOCOL.md`
   * §3.2.7 item 2): each is recorded under its own event's name and neither is
   * compared with the other.
   */
  private buildPresentationPhases(
    presented: readonly InBattleServerEvent[]
  ): PresentationPhase[] {
    const phases: PresentationPhase[] = [];

    // The batch's single callout, and the delivered event it was selected from.
    // The line is raised by the *phase* that presents that event, so the binding
    // is resolved here, in the same forward pass that places the events: a
    // callout can then never be presented before the step it names (AF-2).
    const selection = selectBatchCalloutEvent(
      presented,
      (cardId) => this.cardDisplayName(cardId),
      (relicId) => this.relicDisplayName(relicId)
    );

    let open: {
      kind: PresentationPhaseKind;
      actor: PresentationActor | null;
      action: PresentationAction;
      detail: string;
      events: InBattleServerEvent[];
      boardBacked: boolean;
      callout?: EventCallout;
    } | null = null;

    const flush = () => {
      if (open === null) {
        return;
      }

      phases.push({
        kind: open.kind,
        actor: open.actor,
        action: open.action,
        detail: open.detail,
        holdMs: BattleScene.phaseHoldMs(open.kind, open.events),
        events: open.events,
        boardBacked: open.boardBacked,
        ...(open.callout ? { callout: open.callout } : {}),
      });
      open = null;
    };

    // The session's board-action actor: the side the newest delivered
    // `SwapStarted` names. Each board cycle's Match/Gem/Cascade/Combo events
    // carry no party member of their own (`SIGNALR_PROTOCOL.md`
    // §3.2.6–§3.2.12), so the delivered boundary is what attributes them — never
    // the cycle's position in the batch and never a client-held turn. It starts
    // `null`, which is the side-free answer for a batch that delivers no boundary
    // at all.
    let boardActionActor: 'player' | 'boss' | null = null;

    // The side the newest presented turn banner named, or `null` when this batch
    // has played none. It exists so the banner marks a **boundary** rather than a
    // repeated label: one banner per delivered board cycle, and only when the
    // boundary opens a cycle for a side the previous banner did not already mark
    // (`TASK-275` §6.6).
    let bannerSide: 'player' | 'boss' | null = null;

    // The delivered board-action boundaries of this batch, in delivered order: a
    // `SwapStarted` opens one board cycle and the batch's next one closes it
    // (`SIGNALR_PROTOCOL.md` §3.2.26 item 3). One round carries the Player's and the
    // Boss Response Phase's (`ADR-026` §2), and the last of them is the only cycle
    // this client cannot place from the contract alone.
    let lastBoundaryIndex = -1;

    for (let index = 0; index < presented.length; index++) {
      if (deliveredSwapOf(presented[index]) !== null) {
        lastBoundaryIndex = index;
      }
    }

    // Whether the last delivered boundary's cycle starts from the board on screen.
    //
    // The Player's own action is validated against the committed board the state
    // push delivered (`ADR-026` §2), so it is placeable from the contract alone and
    // is never questioned here — the Player Turn's presentation is unchanged. Every
    // other boundary is placeable only when this batch's own delivered board deltas
    // replay onto the round's committed board: the Boss's step-18a action is
    // resolved by the same Match-3 pipeline on the board the Player's cycle left,
    // and that is the board on screen once the Player's own cycle has been applied,
    // so its deltas land on the committed board — unless the Boss AI's deadlock
    // recovery replaced that board with a generated one, which emits nothing at all
    // (`ADR-026` §8, `GAME_EVENTS.md` §3 item 8). Replaying them answers the
    // question from delivered data alone (`replayBoardDeltas`); where they do not
    // land, the cycle's starting board is not one this client holds.
    const lastBoundaryActor =
      lastBoundaryIndex >= 0
        ? deliveredSwapOf(presented[lastBoundaryIndex])?.actor ?? null
        : null;

    let lastCyclePlaceable = false;

    if (lastBoundaryActor === 'boss') {
      const rendered = this.renderedBoardContents();
      const committed = BattleScene.deliveredBoardContents(
        this.currentBattleState?.board ?? null
      );

      if (committed !== null) {
        const replayed = BattleScene.replayBoardDeltas(rendered, presented);

        lastCyclePlaceable =
          replayed !== null && BattleScene.sameBoardContents(replayed, committed);
      }
    }

    // Whether the board cycle currently being built started from the board the
    // scene renders (`PresentationPhase.boardBacked`). It is decided by the
    // delivered boundary that opens the cycle and by nothing else, and it is
    // re-decided at every boundary — one round carries the Player's cycle *and*
    // the Boss Response Phase's (`ADR-026` §2), and the two are not the same
    // board. A batch that delivers no boundary at all keeps the initial `true`:
    // its exchange is this client's own committed pair, on the board on screen.
    let cycleBoardBacked = true;

    for (let index = 0; index < presented.length; index++) {
      const event = presented[index];

      boardActionActor = deliveredSwapOf(event)?.actor ?? boardActionActor;

      const step = adaptPresentedEvent(event, boardActionActor);

      if (step === null) {
        // The event presents nothing of its own: it is already recorded in
        // `presentedEventsLog`, and whatever a player needs told about it is the
        // batch's single callout. It therefore opens and extends no phase.
        continue;
      }

      // A delivered board-action boundary opens the cycle's turn banner before
      // the exchange it introduces (TASK-275 §6.6). The banner is a
      // **presentation-only marker of a delivered member**: the side comes from
      // the `SwapStarted`'s own `actor` (`SIGNALR_PROTOCOL.md` §3.2.26 item 2),
      // never from a client-held turn, the batch's position, or a board
      // difference — and a boundary whose `actor` is outside the contract's two
      // values plays no banner at all, because it names no side
      // (`AGENTS.md` §10). It adds no gameplay event, no sequence increment, and
      // no server phase; `TurnStarted`/`TurnEnded` exist in `GAME_EVENTS.md` §2
      // but are outside the closed wire discriminator set and are never emitted
      // (`SIGNALR_PROTOCOL.md` §3.2.2 item 2), so nothing here claims a turn.
      if (step.kind === 'swap') {
        const side = step.actor === 'player' || step.actor === 'boss' ? step.actor : null;

        // The boundary also decides whether this cycle's board work may be applied
        // to the rendered board (`PresentationPhase.boardBacked`). The Player's own
        // committed action always may: it is validated against the committed board
        // this scene renders, exactly as it always has been. Every other boundary
        // may only when this batch's own delivered deltas were verified to replay
        // onto the round's committed board (the pre-pass above) — which is what
        // makes the Boss's ordinary cycle placeable, and what leaves a cycle whose
        // starting board the client has never held (an unreported deadlock
        // reshuffle) on the reported-without-board-work path.
        cycleBoardBacked =
          side === 'player' ||
          (side === 'boss' && index === lastBoundaryIndex && lastCyclePlaceable);

        if (side === 'boss' && side !== bannerSide) {
          flush();
          bannerSide = side;
          phases.push({
            kind: 'banner',
            actor: side,
            action: 'turn-banner',
            detail: 'banner BOSS TURN',
            holdMs: BattleScene.phaseHoldMs('banner', []),
            events: [],
            boardBacked: cycleBoardBacked,
          });
        }
      }

      // A cycle the client **cannot** place presents no movement at all, so it
      // presents no movement step either: the delivered transition report joins the
      // phase that presents the pass it closes. A `gravity-drop` / `refill-spawn`
      // step states that the pass's Gravity and Spawn played on the board on screen
      // — and for such a cycle they did not (`PresentationPhase.boardBacked`).
      const withheldMovement = step.kind === 'gravity-drop' && !cycleBoardBacked;

      // A CascadeCreated always opens its own phase: it *is* the pass boundary,
      // and one phase per delivered `cascadeDepth` is the contract
      // (`MATCH3_RULES.md` §4.2 item 2). Match and Gem feedback extends whichever
      // phase is showing this pass. A floater-only event joins the open phase
      // rather than splitting it. A step of the same kind extends the phase
      // already open — and so does a movement step whose movement is withheld.
      const extendsOpen =
        open !== null &&
        step.kind !== 'cascade' &&
        (withheldMovement ||
          step.kind === 'feedback' ||
          open.kind === step.kind ||
          (step.kind === 'match' && open.kind === 'cascade'));

      if (!extendsOpen) {
        flush();
      }

      if (open === null) {
        open = {
          kind: withheldMovement ? 'feedback' : step.kind,
          actor: step.actor,
          action: step.action,
          detail: step.detail,
          events: [],
          boardBacked: cycleBoardBacked,
        };
      }

      open.events.push(event);

      // The batch's callout belongs to the phase holding the event it was
      // selected from — the one place in the timeline where that event is
      // presented.
      if (selection !== null && event === selection.event) {
        open.callout = selection.callout;
      }

      // A delivered pass transition reports two steps of the one pass — the Fall its
      // `dropMoves` describe and the Refill its `spawnedGems` describe — so, for a
      // cycle whose board work is presented, it opens two phases, in that order, and
      // the pass is closed only when the refill has played
      // (`SIGNALR_PROTOCOL.md` §3.2.28 item 5, `GAME_EVENTS.md` §1.1 item 8).
      // The adapter still maps one delivered event to one step
      // (`adaptPresentedEvent`); the timeline is what plays the transition's two
      // halves, and the next cascade pass — whose own matches are detected on the
      // board this refill produced — can only begin after them.
      //
      // For a cycle the client cannot place there is no Refill step to play: the
      // transition's report already joined the pass's own phase above, and neither
      // half is presented as movement.
      if (event.type === 'CascadeStepResolved' && !withheldMovement) {
        flush();

        phases.push({
          kind: 'refill-spawn',
          actor: step.actor,
          action: 'refill-spawn',
          detail: `refill depth=${event.passDepth}`,
          holdMs: BattleScene.phaseHoldMs('refill-spawn', [event]),
          events: [event],
          boardBacked: cycleBoardBacked,
        });
      }
    }

    flush();

    // The fallback swap step: the pair this client submitted, presented when the
    // batch delivered **no** swap boundary of its own. It is the pre-extension
    // behaviour preserved for a batch that carries no `SwapStarted`, and it is
    // suppressed as soon as the delivered boundary opened a swap step — the
    // delivered, committed pair is what the timeline presents whenever there is
    // one, so no second step can present the same exchange
    // (SIGNALR_PROTOCOL.md §3.2.26).
    //
    // The dependency is the committed pair alone — never "the request happened to
    // still be in flight when the batch arrived" — so a batch that arrives after
    // its acknowledgement settled still presents the exchange
    // (SIGNALR_PROTOCOL.md §3.1).
    const swapCells = this.committedSwapCells;
    const deliveredSwapStep = phases.some((phase) => phase.kind === 'swap');

    if (swapCells && !deliveredSwapStep) {
      phases.unshift({
        kind: 'swap',
        // The side-free board actor: this step is the client's own submitted pair,
        // and no delivered member of this batch names the side that exchanged it.
        // **No turn banner is played for it** (TASK-275): the banner's whole
        // content is the delivered `SwapStarted.actor`, and a fallback step has no
        // delivered actor to show — presenting `PLAYER TURN` here would state a
        // side the batch did not name.
        actor: 'swap',
        action: 'swap',
        detail: `swap ${swapCells.from}->${swapCells.to}`,
        holdMs: BattleScene.phaseHoldMs('swap', []),
        events: [],
        boardBacked: true,
      });
    }

    // Where the board cycle ends: the round's single committed board, applied after
    // all of the round's board work (`SIGNALR_PROTOCOL.md` §3.1 item 2 — no per-pass
    // board is ever delivered). It is placed at the first damage phase, i.e. at the
    // boundary between the board's own events and the resolution's combat stages
    // (`GAME_EVENTS.md` §1), or after the round's last board-cycle phase when the
    // round delivers one (`lastBoardCyclePhaseIndex`).
    //
    // It is part of the timeline exactly when this resolution delivered a board
    // cycle — a Swap moved Gems, a Match/Cascade was reported, or a pass reported its
    // own completed Gravity + Spawn transition. That is what makes the resolution's
    // **final** displayed board the authoritative one: whatever the timeline moved on
    // the way, the destination cells are written from the delivered state before the
    // combat phases play. A resolution that moved no board (a Card cast, a Boss-only
    // response) has no board-resolution stage to present, and its held board is
    // converged by Recovery instead (TASK-254: a stage with no delivered event is
    // skipped, never fabricated).
    const boardPresented = phases.some(
      (phase) =>
        phase.kind === 'swap' ||
        phase.kind === 'match' ||
        phase.kind === 'cascade' ||
        phase.kind === 'gravity-drop' ||
        phase.kind === 'refill-spawn'
    );

    if (boardPresented) {
      // TASK-254's placement: the one supported board transition sits at the first
      // combat stage the resolution delivers, or at the resolution's end when it
      // delivers none (`SIGNALR_PROTOCOL.md` §3.1 item 2: no per-pass board is ever
      // delivered, so there is exactly one board to converge on).
      const firstCombat = phases.findIndex(
        (phase) => phase.kind === 'damage' || phase.kind === 'retaliation'
      );

      // ADR-026 §2 makes *one round's* board cycle span both actors: the Player's
      // committed Swap **and** the Boss's own board action at `GAME_RULES.md` §17
      // step 18a. The Boss's board cycle is delivered after the Player's damage
      // reports (`GAME_EVENTS.md` §1), so the round's board work does not end at the
      // first combat stage. The committed board is therefore applied where that work
      // actually ends — after the round's last board-cycle phase — and TASK-254's
      // placement still governs every resolution whose board cycle ends at or before
      // it. It is still exactly one transition, and the displayed board is still the
      // delivered one: nothing is derived and nothing is invented.
      const lastBoardCycle = BattleScene.lastBoardCyclePhaseIndex(phases);
      const at = firstCombat === -1 ? phases.length : Math.max(firstCombat, lastBoardCycle + 1);

      phases.splice(at, 0, {
        kind: 'settle',
        // The settle step converges the resolution's board, and the committed
        // Swap is its actor: this step exists only when a board cycle was
        // delivered, which is the board actor's own work.
        actor: 'swap',
        action: 'settle',
        detail: 'settle',
        holdMs: BattleScene.phaseHoldMs('settle', []),
        events: [],
        // The settle is *the* authoritative board write of the round
        // (`SIGNALR_PROTOCOL.md` §3.1 items 1–3), so it is never withheld — what
        // a cycle the client cannot place withholds is the local application of
        // that cycle's deltas (`boardBacked`), and this phase is where the round's
        // committed board replaces whatever the timeline left on screen.
        boardBacked: true,
      });
    }

    if (this.pendingOutcomeHandoff) {
      phases.push({
        kind: 'outcome',
        actor: 'battle',
        action: 'outcome',
        detail: `outcome ${this.pendingOutcomeHandoff.outcome}`,
        holdMs: BattleScene.phaseHoldMs('outcome', []),
        events: [],
        boardBacked: true,
      });
    }

    return phases;
  }

  /**
   * How long one phase is held before the next begins.
   *
   * It is **derived from the phase's own delivered events**, so the hold covers
   * exactly the animations this phase starts and nothing more: a Match's hold is
   * its highlight's lifetime, a phase that also carries a floating number or a
   * gauge travel is held for that, and a phase that starts no animation at all
   * (the terminal outcome handoff) is held for no time at all. That is what keeps
   * the relationship true for every batch shape rather than only for the common
   * one — a `feedback` event joins the phase already open, so the phase that
   * receives it must grow to cover it (TASK-246 §5), and a `boss-skill` step is
   * held for the skill presentation it raises rather than for nothing at all
   * (TASK-254 AF-1/AF-4).
   */
  private static phaseHoldMs(
    kind: PresentationPhaseKind,
    events: readonly InBattleServerEvent[]
  ): number {
    if (ZERO_HOLD_PHASE_KINDS.includes(kind)) {
      return 0;
    }

    return BattleScene.phaseEffectMs(kind, events) + PHASE_SETTLE_MS;
  }

  /**
   * The longest animation one phase's own events trigger, in milliseconds.
   *
   * It reads the *delivered* events, and only to decide how long to look at what
   * they present — never what they mean: `MatchCreated`/`GemMatched` flash the
   * cells they name, a damage instance draws a floater and moves an HP panel,
   * `PowerChanged` draws a signed floater over a travelling Power gauge, the
   * active Pet's own Passive change travels the Passive gauge, the swap tweens two
   * views, the settle phase plays the refill, and a `BossSkillCast` raises the
   * skill callout the step is presented with. No value is computed from any of
   * them.
   */
  private static phaseEffectMs(
    kind: PresentationPhaseKind,
    events: readonly InBattleServerEvent[]
  ): number {
    switch (kind) {
      case 'banner':
        // The banner's own three beats: its enter and exit tweens run inside its
        // hold, so the next phase cannot begin over a banner still on screen
        // (TASK-275 §6.6).
        return TURN_BANNER_TOTAL_MS;
      case 'swap':
        return SWAP_TWEEN_MS;
      case 'gravity-drop':
        // The pass's delivered Fall travel (`GEM_FALL_MS`); the Refill step that
        // closes the same pass is its own phase and its own hold.
        return GEM_FALL_MS;
      case 'refill-spawn':
        return GEM_REFILL_MS;
      case 'settle':
        return GEM_REFILL_MS;
      case 'damage':
      case 'retaliation':
        // Either combat phase moves the panel of the side the delivered events
        // name, so its gauge travel counts even before its events are read.
        return BattleScene.phasePresentationEffectMs(events, GAUGE_TWEEN_MS);
      default:
        return BattleScene.phasePresentationEffectMs(events, 0);
    }
  }

  /** The longest board/combat/floater animation the given delivered events start. */
  private static phasePresentationEffectMs(
    events: readonly InBattleServerEvent[],
    floor: number
  ): number {
    let longest = floor;

    for (const event of events) {
      switch (event.type) {
        case 'MatchCreated':
          longest = Math.max(longest, MATCH_HIGHLIGHT_MS);
          break;
        case 'GemMatched':
          longest = Math.max(longest, GEM_HIGHLIGHT_MS);
          break;
        case 'DamageDealt':
        case 'DamageTaken':
          // One floater per damage instance, plus the panel travel it names.
          longest = Math.max(longest, FLOATER_LIFE_MS, GAUGE_TWEEN_MS);
          break;
        case 'PowerChanged':
          // The signed Power floater of §3.2.24 and the Power gauge it is drawn
          // over.
          longest = Math.max(longest, FLOATER_LIFE_MS, GAUGE_TWEEN_MS);
          break;
        case 'PassiveCharged':
        case 'PassiveTriggered':
          // Only the active Pet's own Passive pair has a gauge on this panel, so
          // only the Pet's own charge/trigger travels it.
          if (isPetPassiveEvent(event)) {
            longest = Math.max(longest, GAUGE_TWEEN_MS);
          }
          break;
        case 'BossSkillCast':
          // The Boss Skill step presents its own delivered cast on the Boss band's
          // callout row, whose lifetime is the callout's hold plus its fade: the
          // hold must cover it, or the next phase would replace the printout
          // before it has been read (TASK-254 AF-1).
          longest = Math.max(longest, CALLOUT_HOLD_MS + CALLOUT_FADE_MS);
          break;
        default:
          // Every other event presents through the callout alone, or not at all.
          break;
      }
    }

    return longest;
  }

  /**
   * The position of the last phase that belongs to the round's board cycle, or `-1`
   * when the round delivered none.
   *
   * The board cycle is the committed Swap and everything the board itself reported —
   * TASK-254's `swap` / `match` / `cascade` phases, each pass's delivered
   * `gravity-drop` and `refill-spawn` steps (`SIGNALR_PROTOCOL.md` §3.2.28), plus the
   * floater step a delivered `ComboChanged` joins (`GAME_EVENTS.md` §1.1). It is a
   * **presentation** grouping read from the phases' own kinds: no actor is inferred,
   * nothing is re-sorted, and no gameplay value is derived from it.
   *
   * It exists for one fact: `settle` converges the round's single committed board, and
   * under ADR-026 §2 the round's board cycle spans the Player's Swap *and* the Boss's
   * step 18a board action, whose events are delivered after the Player's combat stages
   * (`GAME_EVENTS.md` §1). A pass's own Fall and Refill steps belong to that cycle
   * too: the committed board is applied after the round's last of them, never in the
   * middle of a pass.
   */
  private static lastBoardCyclePhaseIndex(phases: readonly PresentationPhase[]): number {
    let last = -1;

    for (let index = 0; index < phases.length; index++) {
      const kind = phases[index].kind;

      if (
        kind === 'swap' ||
        kind === 'match' ||
        kind === 'cascade' ||
        kind === 'gravity-drop' ||
        kind === 'refill-spawn'
      ) {
        last = index;
      }
    }

    return last;
  }

  /**
   * Plays the timeline's phases in order, then ends it.
   *
   * A phase plays synchronously: it presents what the server delivered for that
   * step (and, for the settle phase, converges the board). The next phase is
   * then scheduled after the phase's own hold, so the resolution reads as a
   * sequence. **Without a tween manager — a unit harness — every phase plays
   * immediately and in order**, which is what makes the ordering observable
   * without a running clock.
   *
   * The phase that is playing is what the interaction indicator reports, so a
   * `retaliation` phase — and only a `retaliation` phase, which
   * `buildPresentationPhases` derives from a delivered
   * `source="boss"`/`target="player"` damage instance — is when `BOSS ATTACK` is
   * shown (`TASK-246` §4).
   */
  private advancePresentation(): void {
    const phases = this.presentationTimeline;
    if (phases === null) {
      return;
    }

    while (this.presentationStep < phases.length) {
      const phase = phases[this.presentationStep];
      const position = this.presentationStep + 1;
      this.presentationStep++;

      // The phase the machine was showing has finished: it is completed by the
      // machine moving on, not by a measured duration.
      this.developerMoveLog?.recordPhaseStarted(
        position,
        phases.length,
        phase.kind,
        phase.detail,
        this.presentationSequence
      );

      this.presentationPhaseKind = phase.kind;
      // The phase starts its own animations; each one reports its duration as it
      // starts (`trackPhaseAnimation`), and the phase is then held for the longer
      // of its declared hold and what it actually started. That is the structural
      // half of the pacing rule: a phase can never complete — and the next phase,
      // which presents the step that depends on it, can never begin — while an
      // animation it started is still running.
      this.phaseAnimationMs = 0;
      this.playPresentationPhase(phase);
      const holdMs = Math.max(phase.holdMs, this.phaseAnimationMs);
      BattleScene.appendBounded(this.presentedPhases, phase.detail);
      this.renderPhaseIndicator();

      if (holdMs > 0 && this.tweens) {
        // The pacing runs on the phase clock, so an aborted timeline can cancel
        // exactly its own pacing without disturbing any feedback animation. The
        // generation it was scheduled under is captured with it: a completion
        // that lands after the timeline was abandoned by an authoritative
        // recovery does nothing, so no stale callback can advance an abandoned
        // timeline or unlock a newer action (`P0.3` §4).
        const generation = this.presentationGeneration;
        this.phaseClock.progress = 0;
        this.tweens.add({
          targets: this.phaseClock,
          progress: 1,
          duration: holdMs,
          onComplete: () => {
            if (generation !== this.presentationGeneration) {
              return;
            }
            this.advancePresentation();
          },
        });
        return;
      }
    }

    this.finishPresentation();
  }

  /**
   * Presents one phase's step.
   *
   * Every value presented is a delivered one: the phase's own events go through
   * the same batch feedback path the scene already had (`presentCombatFeedback`),
   * the readouts the phase's events own are presented by it
   * (`presentPhaseReadouts`), the settle phase applies the authoritative board,
   * the two combat phases move the side the delivered events name — and the Boss's
   * Enrage label with the Boss's HP — a `boss-skill` step presents its own
   * delivered cast, and the outcome phase hands off. No step computes a gameplay
   * value, and none is conditioned on one.
   *
   * **Order inside the phase.** The batch's single callout is raised first, then
   * the step's own presentation. That keeps one statement on the callout line —
   * the step's own presentation replaces the batch headline when the two differ,
   * exactly as a `BOSS ENRAGED` transition has always replaced the batch's
   * headline — and it is why the callout is raised *here*, with the phase that
   * owns its event, rather than when the batch arrived (TASK-254 AF-2).
   */
  private playPresentationPhase(phase: PresentationPhase): void {
    // The batch's callout belongs to the phase holding the event it reports, so
    // it is raised with that phase and never before it.
    if (phase.callout) {
      this.showCallout(phase.callout.message, phase.callout.color);
      // A callout a timeline raised belongs to the resolution it reports, so the
      // timeline owns its lifetime (D-2/RC-3).
      this.timelineCalloutShown = true;
    }

    switch (phase.kind) {
      case 'banner':
        this.playTurnBanner(phase.actor);
        break;
      case 'swap':
        this.playSwapPhase(phase);
        break;
      case 'gravity-drop':
        // The pass's delivered Gravity: the survivors the report named travel from
        // the cell they left to the cell they arrived at.
        //
        // A `gravity-drop` phase exists only for a cycle whose board work is
        // presented (`PresentationPhase.boardBacked`): a cycle the client cannot
        // place states no movement step at all, and its delivered pass report joins
        // the phase presenting that pass (`buildPresentationPhases`). The round's
        // single `settle` still converges the delivered board.
        this.playGravityDrop(phase.events);
        break;
      case 'refill-spawn':
        // The same pass's delivered Spawn: the Gems the report named enter the cells
        // it named, so the pass ends on a full board and the next pass's matches are
        // detected on the board this step produced.
        //
        // Like the Fall, this phase exists only where the report describes the board
        // on screen: placing a spawned Gem at a coordinate whose surrounding Gems the
        // client never held would draw a board neither delivered nor derived.
        this.playRefillSpawn(phase.events);
        break;
      case 'settle':
        if (this.currentBattleState) {
          this.renderBoard(this.currentBattleState.board, true);
        }
        break;
      case 'damage':
        if (this.currentBattleState) {
          // The Boss's HP moves here, so the Enrage transition that same delivered
          // pair drives is presented here too — label and callout together.
          this.presentBossEnrage(this.currentBattleState);
          this.presentBossHp(this.currentBattleState);
        }
        break;
      case 'retaliation':
        if (this.currentBattleState) {
          this.presentPetHp(this.currentBattleState);
        }
        break;
      case 'boss-skill':
        // The step presents its own delivered cast: without this arm a step that
        // carries only its `BossSkillCast` would render nothing at all, whatever
        // its hold (TASK-254 AF-1).
        this.presentBossSkillStep(phase);
        break;
      case 'outcome':
        this.handOffOutcome();
        break;
      default:
        // `match`, `cascade`, `feedback`, `cast` and `relic` present through the
        // readouts and floaters their own events carry, below.
        break;
    }

    this.presentPhaseReadouts(phase);

    if (phase.events.length > 0) {
      this.presentCombatFeedback(phase.events);
    }

    // The match/cascade step's own destruction, **after** its highlight: the
    // cells this pass's delivered events confirm were cleared lose their content
    // here (TASK-275 §6.2). It is the same step's events and nothing else, so a
    // phase with no Match/Gem event removes nothing, and a duplicate batch never
    // re-runs it (`acceptedResolutionSequence`).
    //
    // It is applied only where the pass's cells are cells of the board on screen
    // (`boardBacked`). A cycle the client cannot place is the one case where the
    // delivered cells name positions on a board it does not hold: the batch's own
    // deltas were not verified to replay onto the round's committed board, so the
    // pass is reported without a local removal and the committed board arrives at
    // `settle`. (Removing the content found there anyway would delete the wrong Gem;
    // refusing individual clears per cell, because the rendered content disagrees
    // with the delivered `gemType`, is the stale-asset defect — the cell would keep
    // an asset no delivered event placed there until that transition replaced it.)
    if ((phase.kind === 'match' || phase.kind === 'cascade') && phase.boardBacked) {
      this.presentMatchDestruction(phase.events);
    }
  }

  /**
   * Presents a delivered `BossSkillCast` step on the Boss band's own callout row.
   *
   * It is the Boss acting, so it is presented where every Boss-originated message
   * already is: the callout row directly beneath the Boss band, in the Boss band's
   * own colour (`CALLOUT_BOSS_COLOR` — the same line and colour `BOSS ENRAGED`
   * uses) (`BOSS_RULES.md` §6.3/§6.4; `SIGNALR_PROTOCOL.md` §3.2.18).
   *
   * **Everything shown is delivered or already transcribed.** The line comes from
   * the one existing formatter (`describeEventCallout`), which resolves the
   * delivered `skillId` through `resolveBossSkillDisplayName`'s transcription of
   * `BOSS_RULES.md` §6.3/§6.4. No catalog, asset, per-Boss animation or guessed
   * name is introduced, and an unrecognised `skillId` keeps that formatter's
   * existing fallback line rather than an invented one (TASK-254 DR-2). `sourceId`
   * is what the formatter reports the caster from; nothing is inferred from it.
   */
  private presentBossSkillStep(phase: PresentationPhase): void {
    const cast = phase.events.find((event) => event.type === 'BossSkillCast');
    if (!cast) {
      return;
    }

    const callout = describeEventCallout(
      cast,
      (cardId) => this.cardDisplayName(cardId),
      (relicId) => this.relicDisplayName(relicId)
    );

    if (!callout) {
      return;
    }

    this.showCallout(callout.message, callout.color);
    this.timelineCalloutShown = true;
  }

  /**
   * Presents the timeline-owned readouts the phase's own delivered events own
   * (operator decision D-2).
   *
   * A Combo change owns the Combo line and the cumulative Match line, a Power
   * change owns the Pet's Power value and gauge, and the active Pet's own Passive
   * change owns the Passive pair and its gauge. Each is presented by the phase
   * holding the event that reports it — the board phase between whose Matches the
   * server emitted it, or the phase a later delivered event opened — so no readout
   * is written by a raw push while a timeline plays, and none is presented before
   * the step that owns it.
   *
   * **The Combo line is the delivered event's own value.** A `ComboChanged` is
   * presented from the value it carries (`SIGNALR_PROTOCOL.md` §3.2.8: the **new**
   * Combo value that Match produced), which is the authoritative Combo at that
   * point of the timeline — not the round's committed final value the state push
   * already holds (`renderPlayerHud`). The phase's own events are walked in
   * delivered order, so a phase carrying several Matches' Combo reports presents
   * the last of them and a later phase presents a later value: the line progresses
   * with the resolution (regression: a duplicate or superseded batch never reaches
   * here, and convergence still presents the committed value).
   *
   * A Boss-owned Passive change owns no readout on this panel: the panel carries
   * `PetState`'s pair, so nothing is presented for it (`SIGNALR_PROTOCOL.md`
   * §3.2.16 item 1). Nothing is derived: no Passive is charged, no Threshold is
   * evaluated, and no Combo or Match count is computed — the Combo value written is
   * the delivered one.
   */
  private presentPhaseReadouts(phase: PresentationPhase): void {
    const state = this.currentBattleState;
    if (!state) {
      return;
    }

    for (const event of phase.events) {
      switch (event.type) {
        case 'ComboChanged':
          this.renderPlayerHud(state, false, event.combo);
          break;
        case 'PowerChanged':
          this.presentPetPower(state, true);
          break;
        case 'PassiveCharged':
        case 'PassiveTriggered':
          if (isPetPassiveEvent(event)) {
            this.presentPetPassive(state, true);
          }
          break;
        default:
          break;
      }
    }
  }

  /**
   * Plays one committed board action's first step: the two Gems it exchanged
   * change places (`MATCH3_RULES.md` §2.1.6 steps 3→7) — or, for an action whose
   * two cells are not cells of the board on screen, the two §1.0 cells it named
   * are marked instead (TASK-264).
   *
   * **The pair is the delivered one whenever the step carries it.** A step opened
   * by a `SwapStarted` holds that event, and the two cells it presents are the
   * cells the accepted action named — committed, validated data the server
   * delivered (`SIGNALR_PROTOCOL.md` §3.2.26), which is what makes the Boss's own
   * board action presentable at all: the client never sees the Boss's move except
   * through this member, and it derives no swap from a board difference. Only a
   * step for a batch that delivered no boundary falls back to the pair this
   * client submitted (`committedSwapCells`).
   *
   * **A pair is only *exchanged* when the cycle it opens started from the board on
   * screen** (`PresentationPhase.boardBacked`) — that is, when the coordinates name
   * an exchange the displayed contents really made. The boundary decides that, once,
   * for the whole cycle it opens (`buildPresentationPhases`), and this step reads
   * the decision rather than re-deriving it:
   *
   * ```text
   * Player boundary / no boundary   exchanges — the round's first board action is
   *                                 validated against the committed board this
   *                                 scene renders (`ADR-026` §2), and the
   *                                 submitted-pair fallback is that same pair
   * Boss boundary, cycle placed     exchanges — the Boss's action is resolved on the
   *                                 board the Player's cycle left, and that is the
   *                                 board on screen once the Player's own cycle has
   *                                 been applied; the batch's own deltas were
   *                                 verified to replay onto the committed board
   * anything else                   marks only — the cycle starts from a board the
   *                                 client has never held (the unreported deadlock
   *                                 reshuffle, `ADR-026` §8), or from an `actor`
   *                                 word that names no side
   * ```
   *
   * For the marked case the exchange is not claimed: the Boss's action is validated
   * against the board its own resolution continues from, and after a deadlock
   * reshuffle that is a board the AI generated and the client has never held
   * (`BossAiEngine.SelectMove`, `ADR-026` §8). `SIGNALR_PROTOCOL.md` §3.1 item 2
   * delivers no per-pass board, so without the reconciliation check the client could
   * not know those contents and moving the displayed cells would present an exchange
   * no authoritative state contains.
   *
   * The cell **indices** are board-independent (`MATCH3_RULES.md` §1.0), so the
   * delivered pair is still presented: the two cells are marked, and the round's
   * committed board follows at its one transition. Nothing is validated, no
   * adjacency is checked, no match is looked for, and no board is reconstructed —
   * a Swap the server rejected emits no events at all (`GAME_EVENTS.md` §1.2), so
   * no timeline, and therefore no swap step, is ever played for one.
   *
   * The fallback pair is consumed here rather than by the request's `finally`:
   * this phase is the one presentation it drives, and it plays whether or not the
   * request's acknowledgement has already settled.
   */
  private playSwapPhase(phase: PresentationPhase): void {
    const delivered =
      phase.events.map((event) => deliveredSwapOf(event)).find((swap) => swap !== null) ?? null;

    // Both sources are read as one shape: the delivered pair's own member names and
    // the submitted pair's, so the step below reads one pair whatever opened it.
    const pair = delivered
      ? { from: delivered.fromCell, to: delivered.toCell }
      : this.committedSwapCells;

    if (!pair) {
      return;
    }

    this.committedSwapCells = null;

    // The step exchanges the views it names exactly when its cycle started from the
    // board on screen (`PresentationPhase.boardBacked`): the Player's own committed
    // action and the submitted-pair fallback always did and still do, and the Boss's
    // action does whenever this batch's own deltas were verified to replay onto the
    // round's committed board. Any other step — a cycle whose starting board the
    // client has never held, or an `actor` word outside the contract's two values,
    // which names no side (`SIGNALR_PROTOCOL.md` §3.2.26 item 2) — is presented as
    // marked cells only.
    const exchangedOnHeldBoard = phase.boardBacked;

    if (!exchangedOnHeldBoard) {
      this.markSwapCells(pair.from, pair.to);
      return;
    }

    const fromView = this.gemViews[pair.from];
    const toView = this.gemViews[pair.to];

    if (!fromView || !toView) {
      return;
    }

    if (!this.tweens) {
      // Without a tween manager the exchange is applied directly, so the
      // presented order is still observable without a running clock.
      this.exchangeGemViews(pair.from, pair.to);
      return;
    }

    const from = BattleScene.cellCoordinates(pair.from);
    const to = BattleScene.cellCoordinates(pair.to);

    // The exchange is an animation of this phase: the phase cannot complete — and
    // the Match/Cascade step detected on the exchanged board cannot begin — until
    // it has finished (`trackPhaseAnimation`, `advancePresentation`). This is the
    // ordering the timeline is required to have: a Match presentation is never
    // played over a swap that is still visibly in flight, whatever the declared
    // hold happens to be.
    this.trackPhaseAnimation(SWAP_TWEEN_MS);

    this.tweens.add({
      targets: [fromView.tile, fromView.label],
      x: to.x,
      y: to.y,
      duration: SWAP_TWEEN_MS,
      ease: 'Sine.easeInOut',
    });
    this.tweens.add({
      targets: [toView.tile, toView.label],
      x: from.x,
      y: from.y,
      duration: SWAP_TWEEN_MS,
      ease: 'Sine.easeInOut',
    });

    // The Gems changed places; the cells did not. The occupant map follows the
    // movement immediately, so every later phase — and any abort — reads the
    // board as the Swap left it.
    this.gemViews[pair.from] = toView;
    this.gemViews[pair.to] = fromView;
  }

  /**
   * Marks the two §1.0 cells a delivered board action named, moving nothing.
   *
   * It is the swap step's presentation for an action whose exchange the scene
   * cannot show as a movement of its views (see `playSwapPhase`): the coordinates
   * are delivered and true of any board, while the contents at those cells are
   * not — so the step states *where* the action acted and claims nothing about
   * what it exchanged. The round's committed board is what the round's single
   * transition then applies (`settle`).
   *
   * Both marks are the scene's existing transient board highlights, so each is
   * registered as a timeline-owned effect: a supersession, an abort and a teardown
   * cancel them with the timeline that started them, exactly as a Match's own
   * flash is cancelled, and no mark can outlive the resolution that raised it.
   * Their lifetime is `SWAP_MARK_MS`, inside the swap phase's own hold.
   */
  private markSwapCells(from: number, to: number): void {
    this.spawnCellHighlight(
      from,
      SWAP_MARK_COLOR,
      SWAP_MARK_FILL_ALPHA,
      SWAP_MARK_COLOR,
      SWAP_MARK_MS
    );
    this.spawnCellHighlight(
      to,
      SWAP_MARK_COLOR,
      SWAP_MARK_FILL_ALPHA,
      SWAP_MARK_COLOR,
      SWAP_MARK_MS
    );
  }

  /** Exchanges two cells' occupants, placing each view on the other's cell. */
  private exchangeGemViews(from: number, to: number): void {
    const fromView = this.gemViews[from] ?? null;
    const toView = this.gemViews[to] ?? null;

    this.gemViews[from] = toView;
    this.gemViews[to] = fromView;

    if (fromView) {
      this.placeGemView(fromView, to, false);
    }

    if (toView) {
      this.placeGemView(toView, from, false);
    }
  }

  /**
   * Plays one pass's delivered **Fall** step: every survivor the event's
   * `dropMoves` named travels from the §1.0 cell it came from to the §1.0 cell it
   * arrived at (`SIGNALR_PROTOCOL.md` §3.2.28 item 3, `MATCH3_RULES.md` §4.4).
   *
   * **The client computes no gravity.** Both endpoints of every movement are
   * delivered members — no column is scanned, no survivor list is rebuilt, no cell
   * is anchored to the bottom of its column, and no distance is derived
   * (`AGENTS.md` §10). The step presents the movement the server executed.
   *
   * **The occupant map is rebuilt from a snapshot taken before the step.** Every
   * origin is vacated first and every destination is then filled, so two moves that
   * share a cell — one move's destination is another's origin — are applied the way
   * Gravity applied them and no view can be lost by ordering. A cell no move
   * reached keeps the view it had.
   *
   * **A view the step displaces is pooled, not destroyed.** Gravity only ever falls a
   * survivor into a cell this pass emptied, so the view that cell had is exactly the
   * view the same pass's Refill step must re-place: it is hidden (so no cell is ever
   * drawn twice, even if a clear was refused) and kept for `playRefillSpawn`. That is
   * what keeps the board drawn by the same persistent views for a whole battle.
   */
  private playGravityDrop(events: readonly InBattleServerEvent[]): void {
    const moves = BattleScene.deliveredDropMoves(events);

    if (moves.length === 0 || !this.boardLayer) {
      return;
    }

    const before = this.gemViews.slice();

    // The views this step's own report **claims**: each move's subject is the view
    // its origin holds in the snapshot taken before the step. A claimed view travels
    // with its move; every *other* view standing on a destination cell is displaced,
    // and pooling it is what keeps the occupant map one-to-one — a displaced view
    // left where it was would still be drawn at a coordinate no delivered event
    // placed it at, and the arriving view would be drawn on top of it.
    //
    // Deciding ownership by "does a move claim this exact view" rather than by "is
    // this cell some move's origin" is what makes the rule hold when the snapshot is
    // already incomplete: an origin whose slot is empty claims nothing, so the view
    // its destination still holds is pooled instead of being silently overwritten.
    const claimed = new Set<GemView>();
    for (const move of moves) {
      const view = before[move.fromCell];
      if (view) {
        claimed.add(view);
      }
    }

    for (const move of moves) {
      const displaced = before[move.toCell];

      if (!displaced || claimed.has(displaced)) {
        continue;
      }

      this.poolGemView(displaced);
    }

    // Every origin is vacated *before* any destination is filled, so a chain of
    // moves (0→8, 8→16, 16→24) applies as the one step Gravity applied and no view
    // is lost to the order the report happens to list them in.
    for (const move of moves) {
      if (this.gemViews[move.fromCell] === before[move.fromCell]) {
        this.gemViews[move.fromCell] = null;
      }
    }

    for (const move of moves) {
      const view = before[move.fromCell];
      if (!view) {
        // The cell the report names as an origin holds no view on screen — the
        // occupant map is already incomplete — so there is nothing to move. Nothing
        // is invented for it: the cell converges from the delivered board at the
        // round's single transition (`settle`).
        continue;
      }

      this.gemViews[move.toCell] = view;
      this.travelGemView(view, move.toCell, GEM_FALL_MS);
    }
  }

  /**
   * Empties one gem view and returns it to the displacement pool.
   *
   * A pooled view is hidden, holds no content, and is claimed by no cell: it exists
   * only so the pass's own Refill step (or the round's single board transition) can
   * re-label it and place it at the delivered cell it belongs to, which is what keeps
   * a battle's board on the same 64 persistent views (TASK-245) while never leaving a
   * sprite attached to a coordinate no delivered event named.
   */
  private poolGemView(view: GemView): void {
    view.cleared = true;
    view.content = '';
    view.tile.setVisible(false);
    view.label.setVisible(false);
    this.refillableViews.push(view);
  }

  /**
   * Takes one pooled view that no cell currently owns, or `null` when the pool holds
   * none.
   *
   * The ownership check is the pool's contract, enforced rather than assumed: a view
   * still referenced by a `gemViews` slot belongs to that cell, so handing it to
   * another cell would map two coordinates onto one sprite — the exact defect that
   * shows up as a cell drawn twice and another drawn not at all. An entry that fails
   * the check is dropped from the pool (it is the occupied slot's view, so it is not
   * orphaned); with the map one-to-one it is never reached.
   */
  private takePooledView(): GemView | null {
    while (this.refillableViews.length > 0) {
      const candidate = this.refillableViews.pop() as GemView;

      if (!this.gemViews.includes(candidate)) {
        return candidate;
      }
    }

    return null;
  }

  /**
   * Plays one pass's delivered **Refill** step: every Gem the event's `spawnedGems`
   * named enters the §1.0 cell it named, showing the Gem type the server drew for it
   * (`SIGNALR_PROTOCOL.md` §3.2.28 item 4, `MATCH3_RULES.md` §4.5).
   *
   * The cell and the type are both delivered: the client chooses neither, draws
   * nothing from the RNG, and never predicts a spawn (`AGENTS.md` §10,
   * `SIGNALR_PROTOCOL.md` §3.1 item 5). Each Gem enters from above its own cell, so
   * the pass ends on a full board and the next pass — whose matches the server
   * detected on exactly this board — presents against the same board the client now
   * shows.
   */
  private playRefillSpawn(events: readonly InBattleServerEvent[]): void {
    const spawned = BattleScene.deliveredSpawnedGems(events);

    if (spawned.length === 0 || !this.boardLayer) {
      return;
    }

    for (const gem of spawned) {
      const cell: RuntimeCell = { gemType: gem.gemType };
      // Two already-drawn views can stand for this cell, and both are re-used rather
      // than abandoned:
      //
      //     the view still on this cell   the pass's own clear emptied it and no move
      //                                   took it, because nothing could fall into it —
      //                                   a cleared cell at the top of its column. It is
      //                                   hidden and holds no content, exactly like a
      //                                   pooled one.
      //     a pooled view                 the pass's Fall displaced it from a cell a
      //                                   survivor fell into, so it is the view this
      //                                   Refill step must re-place.
      //
      // Either way the cell keeps a persistent pair and the board stays on the same 64
      // views for the whole battle (TASK-245). Only a cell the scene has neither for —
      // a payload this board cannot account for — allocates one, and an unconsumed
      // pooled view is released by `renderBoard`.
      const onCell = this.gemViews[gem.cellIndex] ?? null;
      const pooled = onCell ?? this.takePooledView();
      const view = pooled ?? this.createGemView(cell);

      if (pooled) {
        // The delivered cell's content replaces whatever the view held — a cleared
        // cell's emptied tile, or a pooled view's — and it restores the view's
        // visibility and drops its `cleared` flag, so a cell the pass emptied can
        // never keep an asset no delivered event placed there (`drawGemContent`).
        this.drawGemContent(view, cell);
      }

      this.gemViews[gem.cellIndex] = view;
      this.placeGemView(view, gem.cellIndex, true, GEM_SPAWN_ENTER);
    }
  }

  /**
   * Travels one view to a §1.0 cell over `durationMs` — the Fall step's own movement.
   *
   * Both endpoints are delivered: the view is already drawn on the cell the report
   * named as the origin, and the destination is the report's own `toCell`. Nothing is
   * derived from the board. Without a tween manager (a unit harness) the view is
   * placed immediately, so the delivered order stays observable with no clock.
   */
  private travelGemView(view: GemView, index: number, durationMs: number): void {
    const { x, y } = BattleScene.cellCoordinates(index);

    if (!this.tweens) {
      view.tile.setPosition(x, y);
      view.label.setPosition(x, y);
      return;
    }

    this.trackPhaseAnimation(durationMs);
    this.tweens.add({
      targets: [view.tile, view.label],
      x,
      y,
      duration: durationMs,
      ease: 'Sine.easeIn',
    });
  }

  /** Every `dropMoves` entry the given phase's delivered events carry, in order. */
  private static deliveredDropMoves(events: readonly InBattleServerEvent[]): PresentedDropMove[] {
    const moves: PresentedDropMove[] = [];

    for (const event of events) {
      if (event.type === 'CascadeStepResolved') {
        moves.push(...event.dropMoves);
      }
    }

    return moves;
  }

  /** Every `spawnedGems` entry the given phase's delivered events carry, in order. */
  private static deliveredSpawnedGems(
    events: readonly InBattleServerEvent[]
  ): PresentedSpawnedGem[] {
    const gems: PresentedSpawnedGem[] = [];

    for (const event of events) {
      if (event.type === 'CascadeStepResolved') {
        gems.push(...event.spawnedGems);
      }
    }

    return gems;
  }

  /**
   * Ends the timeline normally: the resolution is converged on the authoritative
   * state, the guard is released, and a terminal outcome the batch named is handed
   * off.
   *
   * **Recovery converges before the guard is released** (TASK-254): the machine
   * reaches the next actor only once the board and every timeline-owned readout
   * match the delivered state, so input can never be accepted against a board the
   * resolution has not finished presenting. The timeline's own callout is cancelled
   * first, so a callout Recovery raises (the delivered `BOSS ENRAGED`) is the one
   * left on screen, and a callout Recovery raised is not cancelled with the
   * timeline that has just ended.
   */
  private finishPresentation(): void {
    if (this.presentationTimeline === null) {
      return;
    }

    if (
      this.presentationBattleId !== null &&
      this.currentBattleState !== null &&
      this.presentationBattleId !== this.currentBattleState.battleId
    ) {
      this.releaseTimeline();
      return;
    }

    // The timeline's own end: the last phase is completed by the same step advance
    // a phase boundary uses, and the resolution's timeline row is closed before the
    // guard is released, so the panel's order is the order the machine played.
    this.developerMoveLog?.recordCompleted(
      this.presentationSequence,
      this.presentationTimeline.length
    );
    this.cancelTimelineCallout();
    this.settleHeldPresentation();
    // A callout Recovery raised reports the delivered state, not the timeline that
    // has just ended: it is left to fade on its own (RC-3 scopes the *timeline's*
    // callout, and this line is no longer the timeline's).
    this.timelineCalloutShown = false;
    this.releaseTimeline();
    this.handOffOutcome();
    if (
      !this.outcomeHandled &&
      this.pendingOutcomeHandoff === null &&
      !this.isInputLocked()
    ) {
      this.presentPlayerTurnBannerIfNeeded();
    }
  }

  /**
   * Abandons the in-flight timeline: its pacing and every effect it started are
   * cancelled, the values it was holding are converged on the authoritative state,
   * and only then is the guard released — so an aborted resolution leaves the board
   * and the HUD on the server's values rather than mid-animation, and the next actor
   * is never reached mid-abort.
   *
   * It is used by supersession (`renderBattleState`) and by a presentation error
   * (`handleBattleEvents`). It cancels everything the abandoned timeline started —
   * its pacing, the gem-view travels, and every transient effect a phase spawned
   * (`timelineEffects`) — so no abandoned resolution animates over the next one
   * (TASK-252 audit RC-2).
   */
  private abortPresentation(reason: MoveLogAbortReason = 'superseded-state'): void {
    // The aborted timeline's own facts, read before `releaseTimeline` drops them:
    // the resolution it was presenting and the battle it belonged to.
    const abortedSequence = this.presentationSequence;
    const abortedBattleId = this.presentationBattleId;
    const aborted = this.presentationTimeline !== null;

    this.tweens?.killTweensOf(this.phaseClock);
    // A swap or refill travel still in flight belongs to the abandoned
    // presentation: cancelling it is what leaves the board converged rather than
    // drifting to a position the newer state has already replaced.
    this.killGemViewTweens();
    this.cancelTimelineEffects();
    // The pair the abandoned timeline was going to present is dropped with it: a
    // newer state has already replaced the board it would have moved.
    this.committedSwapCells = null;
    // The developer Move Log records the abandonment against the resolution it
    // actually belonged to: a timeline that was playing when a newer delivered
    // value arrived is `aborted`, one whose batch could not be presented at all is
    // `failed`, and a timeline that belonged to a battle the log has already
    // re-keyed away from is not recorded against the new battle.
    if (aborted) {
      this.developerMoveLog?.recordAbandoned(abortedBattleId, abortedSequence, reason);
    }
    // The abandoned timeline's own callout does not survive it; anything Recovery
    // raises for the delivered state afterwards does.
    this.cancelTimelineCallout();
    this.settleHeldPresentation();
    this.timelineCalloutShown = false;
    this.releaseTimeline();
    this.handOffOutcome();
  }

  /**
   * Cancels and releases every transient effect the in-flight timeline started.
   *
   * A phase's highlights and floaters are created, animated and destroyed inside
   * the phase's own hold, so on a completed timeline they have already released
   * themselves and this only drops the references. On an abort their tweens are
   * still running, so both the tween and the object are released here — that is
   * what keeps an abandoned resolution's effects from overlapping the next one.
   */
  private cancelTimelineEffects(): void {
    const effects = this.timelineEffects;
    this.timelineEffects = [];

    for (const effect of effects) {
      this.tweens?.killTweensOf(effect);
      effect.destroy();
    }
    if (this.activeTurnBanner) {
      this.tweens?.killTweensOf(this.activeTurnBanner);
      this.activeTurnBanner.destroy();
      this.activeTurnBanner = null;
    }
  }

  /**
   * Releases the timeline-aware input guard and drops the in-flight timeline.
   *
   * It touches no game object beyond the interaction indicator it re-derives: it
   * is the bookkeeping half of ending a presentation, and every caller that must
   * also *apply* something does so explicitly (`settleHeldPresentation`).
   *
   * Ending a timeline therefore also ends the selection and the phase statement
   * it was showing: completion, supersession, a presentation error, and scene
   * teardown all arrive here, so no phase can leak into the next interaction and
   * no stale selection can outlive the resolution it was made in
   * (`TASK-246` §3–§4).
   *
   * It also bounds the batch callout to its own timeline: a callout a timeline
   * raised is cancelled and hidden here, so it can never still be on screen when
   * the next resolution's callout arrives (TASK-252 audit RC-3).
   */
  private releaseTimeline(): void {
    this.presentationTimeline = null;
    this.presentationStep = 0;
    this.presentationSequence = null;
    this.presentationBattleId = null;
    this.presentationPhaseKind = null;
    this.onTimelineEnded();
    this.cancelTimelineCallout();
    this.clearSelection();
    this.renderPhaseIndicator();
  }

  /**
   * Cancels a callout the timeline raised, so it does not outlive that timeline.
   *
   * A callout raised outside a timeline — a transport rejection's own feedback —
   * is left to fade on its own; only the batch's callout and the Boss Enrage
   * callout a phase presents belong to a resolution.
   */
  private cancelTimelineCallout(): void {
    if (!this.timelineCalloutShown) {
      return;
    }

    this.timelineCalloutShown = false;

    const text = this.calloutText;
    if (!text) {
      return;
    }

    this.tweens?.killTweensOf(text);
    // The line's own text is left as the record of what was said; hiding it is
    // what ends its life with the timeline that raised it.
    text.setAlpha(0);
  }

  /**
   * Applies everything the timeline was still holding — the board's content and
   * every readout the timeline owns (operator decision D-2) — immediately and
   * without animation.
   *
   * It is the guarantee behind "the displayed state is the authoritative state":
   * whatever a timeline did not reach, an abort, the timeline's own end, and the
   * next push all converge here, so a held value can never outlive the
   * presentation that held it. Timeline completion, an abort and state
   * supersession therefore each converge the board **and** the Combo / Match
   * counters, the Pet Power value and gauge, the Passive progress and gauge, the
   * Status Effect instances, the Boss Enraged label and the cast controls onto the
   * delivered authoritative state.
   *
   * Nothing is derived: the values written are the delivered ones already in
   * `currentBattleState`.
   */
  private settleHeldPresentation(): void {
    const held = this.heldSequence !== null;
    this.heldSequence = null;

    const state = this.currentBattleState;
    if (!state) {
      return;
    }

    this.renderBoard(state.board, false);
    this.presentBossEnrage(state);
    this.presentBossHp(state, false);
    this.presentPetHp(state, false);
    this.renderPlayerHud(state);
    this.presentPetPower(state, false);
    this.presentPetPassive(state, false);
    this.presentPetStatus(state);

    // The cast controls are rebuilt from the delivered loadout only when a hold
    // actually deferred them, and never while a timeline plays: convergence runs
    // after the machine has left `presenting`, so the set it draws is the one the
    // guard actually permits. A timeline that held nothing leaves the set it
    // already presented in place rather than churning it.
    if (held) {
      this.renderCastControls(state);
    }
  }

  /**
   * Restores the rendered board and every timeline-owned readout to the newest
   * authoritative state the runtime holds, without animation — the rollback half
   * of a **definitive** server rejection (`P0.3` §5,
   * `SIGNALR_PROTOCOL.md` §5 item 2).
   *
   * A rejection is the server's own answer that the action was a gameplay no-op
   * and emitted no events (`GAME_EVENTS.md` §1.2), so nothing of the refused
   * gesture may remain presented. The rollback converges on
   * `currentBattleState` — **the state the runtime currently holds**, never a
   * snapshot captured when the request was submitted — so a state that arrived
   * while the request was in flight is never overwritten by an older rendering,
   * and a rollback is never applied from an obsolete snapshot.
   *
   * It writes delivered values only, and it is idempotent: with nothing held and
   * the board already converged, it re-renders the same delivered state.
   */
  private rollbackToAuthoritativeState(): void {
    this.settleHeldPresentation();
  }

  /**
   * Hands the resolution's terminal outcome to `ResultScene`, once.
   *
   * The handoff is the timeline's last phase and is also performed by every path
   * that ends the timeline, so it is never lost behind a phase boundary and never
   * delayed by a phase that has yet to run. It is deliberately *not* performed by
   * scene teardown (`shutdown` drops the pending handoff instead): a scene that is
   * going away navigates nowhere.
   *
   * The delivered outcome is stated on the interaction indicator **before** the
   * scene is replaced, so the player reads `VICTORY` / `DEFEAT` rather than being
   * left on a `RESOLVING` line while the battle scene goes away. The statement is
   * taken from the delivered `outcome` member of `BattleWon` / `BattleLost`
   * (`SIGNALR_PROTOCOL.md` §3.2.19 item 1) — the battle's end is the server's own
   * fact, and this only names it (`TASK-246` §4).
   *
   * The handoff moves the orchestration machine to its terminal state, which both
   * latches "this battle already handed its outcome off" and releases the input
   * guard.
   */
  private handOffOutcome(): void {
    const outcome = this.pendingOutcomeHandoff;
    if (!outcome || this.outcomeHandled) {
      return;
    }

    // The terminal result is recorded **before** the handoff, so the panel's last
    // row is the outcome the battle actually ended with and it is readable on the
    // scene that is about to be replaced (`ResultScene` takes the display over).
    this.developerMoveLog?.recordOutcome(outcome);

    this.transitionOrchestration('terminated');
    this.pendingOutcomeHandoff = null;
    this.terminalStatement = resolveOutcomeStatement(outcome.outcome);
    this.renderPhaseIndicator();
    this.scene.start('ResultScene', outcome);
  }

  /**
   * Plays the turn banner for the side a delivered board-action boundary named
   * (`TASK-275` §6.6).
   *
   * ```text
   * enter   the label appears: alpha 1 and scale settling to 1 over
   *         TURN_BANNER_ENTER_MS
   * hold    TURN_BANNER_HOLD_MS, while the phase's own hold runs
   * exit    alpha to 0 over TURN_BANNER_EXIT_MS, then the object destroys itself
   * ```
   *
   * **Everything it shows is a delivered member**: `actor` came from the
   * `SwapStarted` that opened the cycle, and the label is the display text for
   * that one value (`TURN_BANNER_TEXT`). No turn number, no phase name, no status
   * word, and no server event is claimed — `SIGNALR_PROTOCOL.md` §3.2.2 item 2
   * admits no turn or phase discriminator, so the banner states only which side's
   * board cycle the delivered batch has reached.
   *
   * It is a transient overlay created for its phase, registered as a
   * timeline-owned effect so an abort or a teardown destroys it with the
   * resolution that started it, and destroyed by its own exit tween on a
   * completed timeline. It is **not** a HUD region and **not** a second
   * notification system: one text object, one phase, no queue (TASK-218A's
   * boundary; §6.6's "do not use banners to hide missing animation phases" — the
   * banner presents a delivered boundary and stands in for nothing).
   *
   * Without a tween manager (a unit harness) the label is created and left
   * visible, which is what makes the phase observable without a running clock —
   * exactly as a floater behaves.
   */
  private playTurnBanner(actor: PresentationActor | null): void {
    if (actor !== 'player' && actor !== 'boss') {
      // No side: no banner. A banner for a boundary that named none would state
      // whose turn it is from nothing (`AGENTS.md` §10).
      return;
    }

    if (!this.feedbackLayer) {
      return;
    }

    if (this.activeTurnBanner) {
      this.tweens?.killTweensOf(this.activeTurnBanner);
      this.activeTurnBanner.destroy();
      this.activeTurnBanner = null;
    }

    // The board's centre: the middle two cells' midpoint, derived from the same
    // cell geometry every other board effect uses. It is a display position and
    // nothing else.
    const upper = BattleScene.cellCoordinates(27);
    const lower = BattleScene.cellCoordinates(28);
    const x = (upper.x + lower.x) / 2;
    const y = (upper.y + lower.y) / 2;

    const label = this.add
      .text(x, y, TURN_BANNER_TEXT[actor], {
        fontFamily: 'system-ui, sans-serif',
        fontSize: TURN_BANNER_FONT_SIZE,
        color: TURN_BANNER_COLOR[actor],
        fontStyle: 'bold',
      })
      .setOrigin(0.5);
    this.feedbackLayer.add(label);
    this.timelineEffects.push(label);
    this.activeTurnBanner = label;

    // The banner's three beats run inside its own hold, so the phase cannot
    // complete — and the exchange it introduces cannot begin — over a banner still
    // on screen (TASK-275 §6.6).
    this.trackPhaseAnimation(TURN_BANNER_TOTAL_MS);

    if (!this.tweens) {
      return;
    }

    label.setAlpha(1);
    label.setScale(TURN_BANNER_ENTER_SCALE);

    // The enter beat, on its own property so it cannot fight the exit beat's
    // `alpha` on the same target.
    this.tweens.add({
      targets: label,
      scale: 1,
      duration: TURN_BANNER_ENTER_MS,
    });

    // The hold and the exit beat. `delay` is the hold, so the three beats are one
    // schedule derived from the three constants — the label is gone by
    // `TURN_BANNER_TOTAL_MS`, which is exactly the phase's hold, so the next
    // phase can never begin over a banner still on screen.
    this.tweens.add({
      targets: label,
      alpha: 0,
      delay: TURN_BANNER_ENTER_MS + TURN_BANNER_HOLD_MS,
      duration: TURN_BANNER_EXIT_MS,
      onComplete: () => {
        label.destroy();
        if (this.activeTurnBanner === label) {
          this.activeTurnBanner = null;
        }
      },
    });
  }

  /**
   * Records one animation the **phase currently playing** has started, so the
   * phase cannot complete before it has finished.
   *
   * Every animation helper reports its own duration here as it starts — the swap
   * exchange and its coordinate marks, a Match/Gem highlight, a Fall travel, a
   * Refill/spawn entrance, a gauge travel, a floater and the turn banner — and
   * `advancePresentation` holds the phase for the longer of its declared hold and
   * the longest animation actually started.
   *
   * It is the structural form of the pacing rule the timing policy states in
   * numbers: "a Match presentation must not begin before the swap that produced it
   * has reached its required presentation state" is then a property of the machine,
   * not a coincidence of two constants agreeing — a future edit to one duration
   * cannot silently cut the next phase short, because the phase waits on what it
   * started rather than on what it declared.
   *
   * A non-finite or non-positive duration records nothing, and only the longest
   * value is kept: this is a high-water mark for one phase, reset at every phase
   * boundary.
   */
  private trackPhaseAnimation(durationMs: number): void {
    if (Number.isFinite(durationMs) && durationMs > this.phaseAnimationMs) {
      this.phaseAnimationMs = durationMs;
    }
  }

  /**
   * Removes the content of the cells one match/cascade step's delivered events
   * confirm were cleared (`TASK-275` §6.2).
   *
   * ```text
   * MatchCreated   its `cells`, each confirmed by the event's own `gemType`
   * GemMatched     its `cellIndex`, confirmed by the event's own `gemType`
   * ```
   *
   * The clears are the **pre-removal** facts the contract defines
   * (`SIGNALR_PROTOCOL.md` §3.2.6, §3.2.9): the cell and the Gem type it held.
   * Nothing else is read, no board difference is diffed, and no match is
   * detected. Every other event type removes nothing.
   *
   * **It is gated on this step's own confirmed clear.** A cell whose rendered
   * content the delivered events did not confirm is not removed: a pass whose
   * cells disagree with the board on screen changes nothing (`TASK-275` AC-5), and
   * the round's single `settle` convergence is what presents the delivered board.
   */
  private presentMatchDestruction(events: readonly InBattleServerEvent[]): void {
    for (const event of events) {
      if (event.type === 'MatchCreated') {
        for (const cellIndex of event.cells) {
          // The Match's `cells` are the shape's cells and share its Gem type
          // (§3.2.6 item 5), so the type is the whole confirmation available
          // here.
          this.clearConfirmedCell(cellIndex, event.gemType);
        }
        continue;
      }

      if (event.type === 'GemMatched') {
        this.clearConfirmedCell(event.cellIndex, event.gemType);
      }
    }
  }

  /**
   * Empties one board cell's view **only when the cell's rendered content is the
   * content the delivered event confirms** (`TASK-275` §6.2).
   *
   * ```text
   * view missing, or already empty   → nothing (idempotent: a replayed event and
   *                                    the second event naming one cell both no-op)
   * rendered content ≠ delivered     → nothing (the scene will not remove a Gem
   *                                    the server did not confirm at that cell)
   * rendered content = delivered     → hide the tile and label, drop the content
   * ```
   *
   * **The gate is what keeps the destruction honest under the delivered
   * contract.** Pass 1 of a round is detected on the board the client renders —
   * the authoritative board the push delivered, with the accepted exchange
   * applied from its own `SwapStarted` — so its cells agree and are removed. A
   * later pass is detected on an intermediate board the contract never sends
   * (`SIGNALR_PROTOCOL.md` §3.1 item 2), so the client cannot know which Gem sat
   * at a given cell then; when the delivered `gemType` does not match what the
   * cell renders, the clear is **not** invented and the cell is left as it is.
   * Either way the board converges on the accepted authoritative board at the
   * round's single transition, and a cleared cell is re-labelled from it with
   * the refill entrance (`drawGemContent`, `placeGemView`).
   *
   * The confirmation is the cell's whole content key: a delivered clear names the
   * Gem type the cell held, and that type is the cell's whole state
   * (`GAME_STATE.md` §2.1.3 items 1–4).
   */
  private clearConfirmedCell(cellIndex: number, gemType: string): boolean {
    const view = this.gemViews[cellIndex];

    if (!view || view.cleared) {
      return false;
    }

    if (view.content !== gemContentKeyFor(gemType)) {
      return false;
    }

    view.cleared = true;
    view.content = '';
    view.tile.setVisible(false);
    view.label.setVisible(false);

    return true;
  }

  /**
   * Draws the batch's board highlights and floating combat numbers.
   *
   * The board highlights are keyed on the matched cells the delivered events name
   * (`MatchCreated.cells`, `GemMatched.cellIndex`); the floaters are keyed on the
   * **delivered party**, in this order:
   *
   * ```text
   * DamageDealt / DamageTaken   one floater per damage instance, drawn over the
   *                             panel of the party the event names as the
   *                             receiver
   * PowerChanged                one floater over the Power gauge, signed
   * ```
   *
   * `DamageDealt` and `DamageTaken` carry **identical payloads** for one instance
   * (`SIGNALR_PROTOCOL.md` §3.2.15 item 1), so the pair is collapsed into a single
   * floater instead of drawing the same hit twice. The scene applies no damage and
   * derives no HP from `amount` — `amount` is a number to show, nothing more.
   */
  private presentCombatFeedback(events: readonly InBattleServerEvent[]): void {
    let previousDamage: { source: string; target: string; amount: number } | null = null;

    for (const event of events) {
      switch (event.type) {
        case 'MatchCreated': {
          for (const cellIndex of event.cells) {
            this.spawnCellHighlight(cellIndex, 0xffffff, 0.4, 0xfacc15, MATCH_HIGHLIGHT_MS);
          }
          previousDamage = null;
          break;
        }
        case 'GemMatched': {
          this.spawnCellHighlight(event.cellIndex, 0x60a5fa, 0.5, 0x38bdf8, GEM_HIGHLIGHT_MS);
          previousDamage = null;
          break;
        }
        case 'DamageDealt': {
          this.spawnDamageFloater(event.target, event.amount);
          previousDamage = { source: event.source, target: event.target, amount: event.amount };
          break;
        }
        case 'DamageTaken': {
          // §3.2.15 item 1: the same instance, reported from the receiver's side.
          // Only a `DamageTaken` with no matching `DamageDealt` before it is drawn.
          const paired =
            previousDamage !== null &&
            previousDamage.source === event.source &&
            previousDamage.target === event.target &&
            previousDamage.amount === event.amount;

          if (!paired) {
            this.spawnDamageFloater(event.target, event.amount);
          }

          previousDamage = null;
          break;
        }
        case 'PowerChanged': {
          this.spawnPowerFloater(event.delta);
          previousDamage = null;
          break;
        }
        default: {
          previousDamage = null;
          break;
        }
      }
    }
  }

  /**
   * Flashes one board cell, when the delivered index is a cell of this board.
   *
   * An index outside the 8 x 8 board is ignored rather than clamped: the scene
   * presents the event's own cells and never invents a position for one it cannot
   * place (`MATCH3_RULES.md` §1.0).
   *
   * The highlight is registered as a timeline-owned effect, so an aborted
   * timeline cancels it instead of letting it fade over the next resolution.
   */
  private spawnCellHighlight(
    cellIndex: number,
    fill: number,
    fillAlpha: number,
    stroke: number,
    duration: number
  ): void {
    if (!this.feedbackLayer) {
      return;
    }

    if (cellIndex < 0 || cellIndex >= BOARD_ROWS * BOARD_COLUMNS) {
      return;
    }

    const { x, y } = BattleScene.cellCoordinates(cellIndex);
    const highlight = this.add
      .rectangle(x, y, CELL_SIZE, CELL_SIZE, fill, fillAlpha)
      .setStrokeStyle(2, stroke);
    this.feedbackLayer.add(highlight);
    this.timelineEffects.push(highlight);

    this.trackPhaseAnimation(duration);

    if (this.tweens) {
      this.tweens.add({
        targets: highlight,
        alpha: 0,
        duration,
        onComplete: () => {
          highlight.destroy();
        },
      });
    }
  }

  /**
   * Draws one floating damage number over the panel of the party that **took**
   * the damage (`SIGNALR_PROTOCOL.md` §3.2.14 item 3).
   *
   * The side is decided by the delivered `target`, never by the event's own
   * discriminator: the two damage reports of one instance carry the same
   * source and target, so a discriminator-keyed placement would draw every hit
   * on one side and mislabel who was hurt. An unrecognized party draws no
   * floater — the callout and the authoritative HP readout still carry the
   * resolution, and no side is guessed for a party the contract does not define.
   */
  private spawnDamageFloater(target: string, amount: number): void {
    const anchor = BattleScene.damageAnchor(target);
    if (!anchor) {
      return;
    }

    const color = target === BOSS_PARTY ? DAMAGE_TO_BOSS_COLOR : DAMAGE_TO_PET_COLOR;
    this.spawnFloater(anchor.x, anchor.y, `-${amount}`, color);
  }

  /**
   * Draws the signed Power movement over the Power gauge (`SIGNALR_PROTOCOL.md`
   * §3.2.24).
   *
   * `delta` is the delivered signed change and is shown as delivered; the
   * resulting `power` is never recomputed from it, and nothing is inferred about
   * a cost or an affordability state from either number (CARD_RULES.md §3.6).
   */
  private spawnPowerFloater(delta: number): void {
    const sign = delta >= 0 ? '+' : '';
    const color = delta >= 0 ? POWER_GAIN_COLOR : POWER_LOSS_COLOR;

    this.spawnFloater(
      POWER_BAR_X + POWER_BAR_WIDTH / 2,
      POWER_TEXT_Y,
      `${sign}${delta}`,
      color
    );
  }

  /**
   * Spawns one transient floating number in the feedback layer.
   *
   * The object is created, animated, and destroyed on completion, and it is
   * registered as a timeline-owned effect for as long as the timeline that
   * started it is alive: an abort cancels it with the rest of that timeline's
   * effects, and a completed timeline drops the reference because the object
   * destroyed itself inside its own phase's hold. Nothing accumulates per frame,
   * and the layer is destroyed with the scene (TASK-210 §15). Without a tween
   * manager (a unit harness) the number is left in place, which is what makes it
   * observable without a running clock.
   */
  private spawnFloater(x: number, y: number, message: string, color: string): void {
    if (!this.feedbackLayer) {
      return;
    }

    const label = this.add
      .text(x, y, message, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '18px',
        color,
        fontStyle: 'bold',
      })
      .setOrigin(0.5);
    this.feedbackLayer.add(label);
    this.timelineEffects.push(label);

    this.trackPhaseAnimation(FLOATER_LIFE_MS);

    if (!this.tweens) {
      return;
    }

    this.tweens.add({
      targets: label,
      y: y - FLOATER_RISE,
      alpha: 0,
      duration: FLOATER_LIFE_MS,
      onComplete: () => {
        label.destroy();
      },
    });
  }

  /**
   * Shows the batch's single player-facing callout, replacing any callout still
   * on screen.
   *
   * It holds briefly and fades — no queue, no blocking, and no dependence on the
   * previous message having finished, so a fast player is never waiting on a
   * presentation. The fade is a tween like every other transient effect, so
   * `shutdown()`'s `tweens.killAll()` releases it with the scene
   * (TASK-205's lifecycle contract).
   *
   * A callout a **timeline** raises — the batch's own single callout, and the
   * `BOSS ENRAGED` callout a phase presents — is owned by that timeline and
   * cancelled with it (`cancelTimelineCallout`), so it can never outlive the
   * resolution that raised it. A callout raised outside a timeline (a transport
   * rejection's own feedback) is left to fade on its own.
   */
  private showCallout(message: string, color: string): void {
    const text = this.calloutText;
    if (!text) {
      return;
    }

    text.setText(message);
    text.setColor(color);
    text.setAlpha(1);

    if (!this.tweens) {
      return;
    }

    this.tweens.killTweensOf(text);
    this.tweens.add({
      targets: text,
      alpha: 0,
      delay: CALLOUT_HOLD_MS,
      duration: CALLOUT_FADE_MS,
    });
  }

  /**
   * The board-side anchor one damage floater is drawn from, keyed on the party the
   * delivered event names as the receiver (`SIGNALR_PROTOCOL.md` §3.2.14 item 3).
   *
   * Both anchors sit outside the 8 x 8 board — over the Boss band for the Boss and
   * over the Pet panel for the active Pet — so a floating number can never cover a
   * matchable cell, and neither depends on the viewport.
   */
  private static damageAnchor(target: string): { x: number; y: number } | null {
    if (target === BOSS_PARTY) {
      return { x: BOSS_BAR_X + BOSS_BAR_WIDTH / 2, y: BOSS_BAR_Y + 34 };
    }

    if (target === PLAYER_PARTY) {
      return { x: PET_BAR_X + PET_BAR_WIDTH / 2, y: PET_BAR_Y + 22 };
    }

    return null;
  }

  /**
   * The player-facing name of the Relic one delivered `RelicTriggered` names, or
   * `null` when this scene holds no definition for it.
   *
   * `relicId` is the Relic's owned **instance** identity — an internal handle, not
   * a label (`RELIC_RULES.md` §2.2 item 3, `SIGNALR_PROTOCOL.md` §3.2.23 item 1) —
   * so an unresolvable instance yields `null` and the caller shows nothing, rather
   * than falling back to the identity as the Card path does. `BossSkillCast`'s
   * precedent already declines a technical identity rather than rendering it
   * (TASK-210 §7), and no name is ever derived from the Relic's `trigger`,
   * `condition`, or `effectDefinition` (`RELIC_RULES.md` §8.2 item 1).
   */
  private relicDisplayName(relicId: string): string | null {
    return this.relicDefinitions.get(relicId)?.name ?? null;
  }

  /**
   * The player-facing name of one Card definition identity, or the delivered
   * identity itself.
   *
   * It is the same lookup the cast controls use and it is presentation only: the
   * derived Signature Skill's name comes from the delivered Pet read
   * (`API_CONTRACTS.md` §5.1) and every other Card's from the delivered unlocked
   * collection (§5.3). An identity with no delivered name is shown as the identity
   * rather than as a guessed name, and no cost, effect, or legality is read from
   * either source (`CARD_RULES.md` §3, ADR-001).
   */
  private cardDisplayName(cardId: string): string {
    return (
      this.signatureSkills.get(cardId)?.name ??
      this.cardDefinitions.get(cardId)?.name ??
      cardId
    );
  }

  /**
   * Computes center coordinates for a board cell index in presentation pixels.
   */
  private static cellCoordinates(index: number): { x: number; y: number } {
    const row = Math.floor(index / BOARD_COLUMNS);
    const column = index % BOARD_COLUMNS;
    return {
      x: BOARD_ORIGIN_X + column * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2,
      y: BOARD_ORIGIN_Y + row * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2,
    };
  }

  /**
   * Read-only snapshot of the presented event lines.
   *
   * **Diagnostics only.** It is the developer/testing record of what the scene
   * presented, and it is no longer rendered anywhere: the raw event feed is not
   * the player's battle HUD (TASK-209 §5). Keeping it is what lets development and
   * the test suite inspect presentation order without a console readout.
   */
  getPresentedEvents(): readonly string[] {
    return this.presentedEventsLog;
  }

  /**
   * Read-only snapshot of the presentation phases played, in order.
   *
   * **Diagnostics only**, exactly like `getPresentedEvents()`: it is the
   * developer/testing record of the timeline the scene played for each delivered
   * resolution (`swap 8->9`, `match depth=1`, `cascade depth=1`, `settle`,
   * `damage player->boss`, …), it is never rendered, and nothing in the scene
   * reads it back.
   */
  getPresentedPhases(): readonly string[] {
    return this.presentedPhases;
  }

  /**
   * Read-only snapshot of the developer Move Log's retained rows, oldest first.
   *
   * It is the panel's own buffer — the rows the panel is showing, plus the older
   * ones still inside `MOVE_LOG_CAP` — and every entry is `formatMoveLogRow`'s
   * output: a delivered wire value, or the client's own presentation lifecycle.
   *
   * **Developer diagnostics only**, and only in a development build: with
   * `import.meta.env.DEV` false the surface does not exist and this is always
   * empty. Nothing in the scene reads it back, it holds no gameplay value, and it
   * is the accessible form of the panel for development and the test suite.
   */
  getMoveLogRows(): readonly string[] {
    return this.moveLogRows;
  }

  /**
   * Returns whether player input is currently locked.
   *
   * The guard covers the **whole** presentation of a resolution: from the moment
   * an action is submitted, through the timeline that presents what the server
   * resolved, and until Recovery has converged the board and the readouts on the
   * delivered state. A player can therefore never act against a board that is
   * still being shown mid-resolution, and a settled acknowledgement does not
   * release it while the resolution it was accepted for is still pending
   * (TASK-254 AF-5). It is released on the timeline's completion, on an error, on
   * supersession, on a rejected or failed action, and on scene teardown.
   *
   * It is one derived fact over the orchestration machine
   * (`BATTLE_ORCHESTRATION_LEGAL_TRANSITIONS`'s state set): every state except
   * `idle` — the state that accepts player input — and `terminated` holds input.
   */
  isInputLocked(): boolean {
    return ORCHESTRATION_LOCKED_STATES.includes(this.orchestration);
  }

  /** Whether a resolution's presentation timeline is playing. */
  private isPresenting(): boolean {
    return this.orchestration === 'presenting' || this.orchestration === 'presenting-submitted';
  }

  /**
   * Whether this battle can still accept a player action request.
   *
   * It is the input guard **and** the terminal latch: a round that handed its outcome
   * to `ResultScene` accepts no further Swap, Card cast, or Pet Skill cast. `terminated`
   * is deliberately not one of the input-locked states (the machine's own doc: the
   * battle is over and the scene is on its way out), so the guard alone would let a tap
   * in that window send a request the server has no battle for; the latch is what makes
   * "a terminal round stops gameplay input" true of the request itself. It reads two
   * client-local facts and answers a question about this client's own interaction — it
   * is not a server turn, phase, or status (`GAME_STATE.md` §2.0.3; TASK-246 §4).
   */
  private acceptsPlayerAction(): boolean {
    return !this.isInputLocked() && !this.outcomeHandled;
  }

  /**
   * Whether this scene has handed its battle's terminal outcome to `ResultScene`.
   *
   * It is the machine's `terminated` state read as a fact, so the terminal latch
   * and the orchestration state cannot disagree. It is read by
   * `handleBattleEvents` (one battle accepts one outcome) and reset by `create()`
   * / `shutdown()` with the rest of the machine.
   */
  private get outcomeHandled(): boolean {
    return this.orchestration === 'terminated';
  }

  /**
   * Applies one transition of the orchestration machine.
   *
   * It is the **only** writer of `orchestration`, and it refuses a transition the
   * documented table does not allow: presentation never throws
   * (`handleBattleEvents` turns a malformed batch into an abort), so an illegal
   * transition leaves the machine where it is rather than corrupting the guard.
   * A call naming the current state is not a transition and does nothing.
   */
  private transitionOrchestration(to: BattleOrchestrationState): void {
    if (to === this.orchestration) {
      return;
    }

    if (!BATTLE_ORCHESTRATION_LEGAL_TRANSITIONS[this.orchestration]?.includes(to)) {
      return;
    }

    this.orchestration = to;
    // The guard is presented the moment it changes: the cast controls are dimmed
    // and made non-responsive while input is held, and restored when it is
    // permitted again. This toggles the existing controls' presentation; it never
    // rebuilds the set, so no delivered push can churn them mid-resolution (D-2).
    this.presentCastControlsEnabled(!this.isInputLocked());
    this.renderPhaseIndicator();
  }

  /**
   * Records that a resolution's timeline has started.
   *
   * A batch can arrive before its own request's acknowledgement or after it
   * (`SIGNALR_PROTOCOL.md` §3.1), and both are legal: the machine keeps the
   * unanswered-request fact when it is present, so the guard is never released
   * while a request is still unanswered — and it takes the *request's* fact, not
   * the machine's state, because the machine is also `submitted` while an
   * acknowledgement has settled and its resolution's batch is still to arrive.
   *
   * A timeline is also a resolution this client is presenting, so `resolutionPending`
   * is asserted here: the board hold and the guard's release both key on it
   * (TASK-254 AF-5/AF-6).
   */
  private onTimelineStarted(): void {
    this.resolutionPending = true;
    this.transitionOrchestration(
      this.submissionOutstanding ? 'presenting-submitted' : 'presenting'
    );
    // The presentation machine's own state after the transition: the Move Log's
    // timeline row records the machine's fact, not an animation's start.
    this.developerMoveLog?.recordTimelineAnimating(this.presentationSequence, this.orchestration);
  }

  /**
   * Records that a resolution's timeline has ended — completed, aborted, or been
   * superseded.
   *
   * The unanswered-request fact, if there is one, is kept: a scene whose timeline
   * ended before its request was answered stays locked until that answer arrives.
   * `resolutionPending` is cleared here, because a timeline ends only through
   * completion, abort or supersession — and every one of those converges the board
   * and the readouts on the delivered state *before* it reaches this point
   * (`finishPresentation` / `abortPresentation`); what is left is the guard.
   */
  private onTimelineEnded(): void {
    this.resolutionPending = false;

    if (this.orchestration === 'presenting') {
      this.transitionOrchestration('idle');
      return;
    }

    if (this.orchestration === 'presenting-submitted') {
      // The request is necessarily still unanswered here: an acknowledgement that
      // settles while its timeline plays moves the machine to `presenting`, which
      // `onActionSettled` below is the only writer of.
      this.transitionOrchestration('submitted');
    }
  }

  /**
   * Records that an action request's acknowledgement has settled.
   *
   * **A settled request does not release the guard on its own** (TASK-254 AF-5).
   * The acknowledgement and the resolution's state push / event batch travel on
   * separate paths (`SIGNALR_PROTOCOL.md` §3.1), so an answer can arrive first;
   * releasing input there would let a second gesture be submitted against a board
   * the resolution has not presented yet — and would reveal the resolution's final
   * board before its own timeline did (AF-6). The machine therefore stays
   * `submitted` while `resolutionPending` holds, and the timeline's end (or a
   * rejection, which clears the pending fact) releases it.
   *
   * The cast controls need no re-presentation of their own: the machine's
   * transition presents the guard's new state, and no control set is rebuilt.
   */
  private onActionSettled(): void {
    this.submissionOutstanding = false;

    if (this.orchestration === 'submitted') {
      if (!this.resolutionPending) {
        this.transitionOrchestration('idle');
      }
      return;
    }

    if (this.orchestration === 'presenting-submitted') {
      this.transitionOrchestration('presenting');
    }
  }

  /**
   * The one statement the interaction indicator is currently making, or `null`
   * when it has nothing honest to say.
   *
   * **This is the client's own interaction state, not a server fact.** The
   * contract delivers no turn ownership, no phase, and no status
   * (`GAME_STATE.md` §2.0.3; the `type` discriminator set of
   * `SIGNALR_PROTOCOL.md` §3.2.2 is closed and contains no turn or phase event —
   * the `GAME_EVENTS.md` §1/§2 turn events that would be needed for one are not
   * on the wire, TASK-246's C-1), so the scene states only what it can actually
   * know:
   *
   * ```text
   * terminal statement    the delivered BattleWon / BattleLost outcome, set by
   *                       handOffOutcome immediately before the ResultScene
   *                       handoff — `VICTORY` / `DEFEAT`
   * retaliation phase     the local timeline is playing the phase the delivered
   *                       `source="boss"`/`target="player"` damage instance
   *                       opened — `BOSS ATTACK`
   * input locked          the same `isInputLocked()` condition the guard itself
   *                       uses, so the line and the guard can never disagree —
   *                       `RESOLVING`
   * no battle delivered   nothing: with no authoritative board there is no
   *                       interaction to permit, so the line stays empty rather
   *                       than claiming a move
   * otherwise             the client permits player input — `YOUR TURN`
   * ```
   *
   * Each is a presentation status. `RESOLVING` in particular is *not* a
   * server-defined phase: the server resolves a Swap completely, writes the state
   * back, and only then sends the batch (`SIGNALR_PROTOCOL.md` §3.1 items 1–2),
   * so there is no remote phase to observe — the honest meaning is exactly this
   * local timeline playing. Nothing here is inferred from animation timing, and
   * no missing server phase is fabricated.
   */
  private phaseIndicator(): InteractionStatement | null {
    if (this.terminalStatement) {
      return this.terminalStatement;
    }

    if (this.presentationPhaseKind === 'retaliation') {
      return INTERACTION_BOSS_ATTACK;
    }

    if (this.isInputLocked()) {
      return INTERACTION_RESOLVING;
    }

    if (this.currentBattleState === null) {
      return null;
    }

    return INTERACTION_YOUR_TURN;
  }

  /**
   * Writes the interaction indicator's single statement.
   *
   * One line, one value: the label is replaced rather than layered, so two
   * contradictory interaction states can never be readable at once. An empty
   * statement clears the line — on a scene with nothing delivered, and after
   * teardown, which clears it before the engine destroys it.
   */
  private renderPhaseIndicator(): void {
    const text = this.phaseText;
    if (!text) {
      return;
    }

    const statement = this.phaseIndicator();
    if (statement === null) {
      text.setText('');
      return;
    }

    text.setText(statement.label);
    text.setColor(statement.color);
  }

  /**
   * Finds the first terminal battle outcome event in an event batch.
   *
   * The batch may contain other resolution events (GAME_EVENTS.md §1); the
   * outcome is identified by the documented `BattleWon` / `BattleLost` wire
   * discriminators (SIGNALR_PROTOCOL.md §3.2.19).
   */
  private static findOutcomeEvent(
    events: readonly unknown[]
  ): { readonly outcome: string; readonly finalBossHp: number; readonly finalPlayerHp: number } | null {
    for (const event of events) {
      if (
        typeof event === 'object' &&
        event !== null &&
        'type' in event &&
        (event.type === 'BattleWon' || event.type === 'BattleLost') &&
        'outcome' in event &&
        typeof (event as { outcome?: unknown }).outcome === 'string' &&
        'finalBossHp' in event &&
        typeof (event as { finalBossHp?: unknown }).finalBossHp === 'number' &&
        'finalPlayerHp' in event &&
        typeof (event as { finalPlayerHp?: unknown }).finalPlayerHp === 'number'
      ) {
        const payload = event as {
          outcome: string;
          finalBossHp: number;
          finalPlayerHp: number;
        };
        return {
          outcome: payload.outcome,
          finalBossHp: payload.finalBossHp,
          finalPlayerHp: payload.finalPlayerHp,
        };
      }
    }
    return null;
  }
}

/**
 * The developer Move Log, as the battle scene holds it (TASK-253, operator
 * decision D-1). Built by `createDeveloperMoveLog`, and only in a DEV build.
 */
type DeveloperMoveLog = ReturnType<typeof createDeveloperMoveLog>;

/**
 * The phase the panel is currently presenting, for its per-phase lifecycle rows.
 *
 * It is the machine's own step position (`presentationStep`), not a measurement of
 * animation: a phase's row is recorded when the step is entered and closed when the
 * machine moves on, so the panel reports the presentation's real transitions
 * rather than a duration.
 */
interface PresentedPhaseStep {
  readonly index: number;
  readonly total: number;
  readonly detail: string;
}

/**
 * Builds the developer Move Log panel and returns the record it keeps.
 *
 * It is a **module-level factory rather than scene methods** so that a production
 * build drops the whole surface — the panel's layout, its text objects and every
 * literal of its row vocabulary. Its only call site is guarded by the compile-time
 * `import.meta.env.DEV` constant (`drawRuntimeShell`), so with the flag false the
 * call and the factory are both unreachable and the production bundle carries
 * neither (the same convention `App.tsx:203–204` uses for the two React
 * diagnostics).
 *
 * The panel is a development surface and nothing else. It is the one line of the
 * battle view that reports the delivered `turn`/`serverSequence` and the raw event
 * feed, which `TASK-210` §9 forbids everywhere else — the bounded supersede
 * recorded by operator decision D-1. Every row is built by `formatMoveLogRow` from
 * a delivered wire value or from the client's own presentation lifecycle; it
 * computes no gameplay value, evaluates no rule, and infers no missing state
 * (`AGENTS.md` §10, ADR-001).
 *
 * ```text
 * geometry      one container at the scene's origin, holding a background
 *               rectangle, the caption, and MOVE_LOG_VISIBLE_ROWS row text
 *               objects — all in the free band to the right of the board
 *               (MOVE_LOG_LEFT), inside the 24 px safe area and between the
 *               board's own top and bottom edges
 * rows          the row objects are created once and written in place
 *               (`scrollToNewest`), so recording a row allocates nothing and the
 *               panel's object count is fixed for the life of the scene run
 * retention     MOVE_LOG_CAP rows, oldest evicted first; the visible window is
 *               always the newest MOVE_LOG_VISIBLE_ROWS rows, which is the
 *               panel's auto-scroll
 * lifecycle     cleared on `create()` (a new panel), on `release()` (teardown) and
 *               whenever the delivered battle identity changes (`syncBattle`)
 * ```
 *
 * @param scene the scene whose display list the panel is drawn into.
 * @param readTurn the delivered `BattleStateUpdated.turn` at the moment a row is
 *   recorded, or `null` when no state push has been delivered — a read of the
 *   scene's synchronized copy, never a derived turn.
 * @param rows the scene's own retained-row buffer, written in place so
 *   `getMoveLogRows()` reads the live record whether or not the surface exists.
 */
function createDeveloperMoveLog(
  scene: BattleScene,
  readTurn: () => number | null,
  rows: string[]
) {
  const layer = scene.add.container(0, 0);

  const background = scene.add
    .rectangle(
      MOVE_LOG_LEFT + MOVE_LOG_WIDTH / 2,
      MOVE_LOG_TOP + MOVE_LOG_HEIGHT / 2,
      MOVE_LOG_WIDTH,
      MOVE_LOG_HEIGHT,
      0x0b1220,
      0.9
    )
    .setStrokeStyle(1, 0x334155);

  const caption = scene.add
    .text(MOVE_LOG_LEFT + 4, MOVE_LOG_TOP + 4, MOVE_LOG_TITLE, {
      fontFamily: 'system-ui, sans-serif',
      fontSize: MOVE_LOG_FONT_SIZE,
      color: '#64748b',
    })
    .setOrigin(0, 0);

  const rowTexts: Phaser.GameObjects.Text[] = [];
  for (let index = 0; index < MOVE_LOG_VISIBLE_ROWS; index++) {
    rowTexts.push(
      scene.add
        .text(MOVE_LOG_LEFT + 4, MOVE_LOG_BODY_TOP + index * MOVE_LOG_ROW_HEIGHT, '', {
          fontFamily: 'system-ui, sans-serif',
          fontSize: MOVE_LOG_FONT_SIZE,
          color: '#94a3b8',
          wordWrap: { width: MOVE_LOG_WRAP_WIDTH, useAdvancedWrap: true },
          maxLines: MOVE_LOG_ROW_LINES,
        })
        .setOrigin(0, 0)
    );
  }

  layer.add([background, caption, ...rowTexts]);

  /** The panel's own row counter, so the recorded order is readable. */
  let ordinal = 0;
  /** The battle the retained rows belong to. */
  let battleId: string | null = null;
  /** The newest `serverSequence` already recorded. */
  let sequence: number | null = null;
  /** The phase the machine is presenting, or `null` when none is in flight. */
  let phaseInFlight: PresentedPhaseStep | null = null;

  /**
   * Writes the newest visible rows into the panel, oldest first — its auto-scroll.
   *
   * The window is pinned to the newest row, so the row just recorded is always on
   * screen and the oldest visible rows scroll out of the top as newer ones arrive.
   * Rows that leave the window are simply overwritten; the retained buffer still
   * holds them for a reader of `BattleScene.getMoveLogRows()`.
   */
  const scrollToNewest = (): void => {
    const visible = rows.slice(-rowTexts.length);

    for (let index = 0; index < rowTexts.length; index++) {
      rowTexts[index].setText(visible[index] ?? '');
    }
  };

  /**
   * Records one row, evicting the oldest once `MOVE_LOG_CAP` is reached.
   *
   * It is the only writer of the buffer: every row is a delivered value or a client
   * presentation fact, and there is no path that fabricates one — an unknown or
   * malformed event never reaches it, and a rejected action emits no events at all
   * (`GAME_EVENTS.md` §1.2), so no row exists for either.
   */
  const append = (row: string): void => {
    rows.push(row);

    if (rows.length > MOVE_LOG_CAP) {
      rows.splice(0, rows.length - MOVE_LOG_CAP);
    }

    scrollToNewest();
  };

  /** Records one lifecycle row of the client's own presentation. */
  const lifecycle = (
    scope: MoveLogLifecycleScope,
    state: MoveLogLifecycleState,
    at: number | null,
    detail = ''
  ): void => {
    ordinal += 1;
    append(formatMoveLogRow({ kind: 'lifecycle', ordinal, scope, state, sequence: at, detail }));
  };

  /**
   * Closes the phase the machine was showing.
   *
   * The completion is the machine's own step advance — the next phase, or the
   * timeline's end (`recordCompleted`) — and never a measured animation time.
   */
  const completePhase = (): void => {
    const phase = phaseInFlight;
    if (phase === null) {
      return;
    }

    phaseInFlight = null;
    lifecycle('phase', 'completed', sequence, `${phase.index}/${phase.total} ${phase.detail}`);
  };

  /**
   * Re-keys the panel to a delivered battle identity, clearing it when it changed.
   *
   * The identity is the battle the rows belong to: a delivered state push and the
   * event batch that follows it both carry it (`SIGNALR_PROTOCOL.md` §3, §4), and
   * a different one means the battle whose resolution was on screen has ended.
   */
  const syncBattle = (nextBattleId: string | null): void => {
    if (battleId === nextBattleId) {
      return;
    }

    rows.length = 0;
    ordinal = 0;
    sequence = null;
    phaseInFlight = null;
    battleId = nextBattleId;
    scrollToNewest();
  };

  scrollToNewest();

  return {
    /** The panel's single declared container, holding every text object of it. */
    layer,
    /**
     * Records one delivered event's row, with the actor/action the one adapter
     * names for it.
     *
     * `boardActionActor` is the side the batch's newest delivered `SwapStarted`
     * named, supplied by the caller that walked the batch in order — the same
     * value the timeline's phase builder threads through the adapter — so the
     * panel's `actor/action` column cannot contradict the phase it describes.
     */
    recordEvent(
      event: InBattleServerEvent,
      at: number,
      boardActionActor: 'player' | 'boss' | null = null
    ): void {
      ordinal += 1;
      append(
        formatMoveLogRow({
          kind: 'event',
          ordinal,
          sequence: at,
          turn: readTurn(),
          step: adaptPresentedEvent(event, boardActionActor),
          event,
        })
      );
    },
    /** Records that a timeline was built from a delivered batch, before it plays. */
    recordQueued(at: number, phaseKinds: readonly PresentationPhaseKind[]): void {
      lifecycle('timeline', 'queued', at, `phases=${phaseKinds.length} ${phaseKinds.join(',')}`);
    },
    /** Records the presentation machine's own state after a timeline started. */
    recordTimelineAnimating(at: number | null, state: BattleOrchestrationState): void {
      lifecycle('timeline', 'animating', at, `state=${state}`);
    },
    /** Records the timeline's own end, closing its last phase first. */
    recordCompleted(at: number | null, phaseCount: number): void {
      completePhase();
      lifecycle('timeline', 'completed', at, `phases=${phaseCount}`);
    },
    /**
     * Records that the machine entered one phase.
     *
     * It closes the phase the machine was showing first, so the panel's
     * `animating` / `completed` pairs follow the machine's real step advance.
     */
    recordPhaseStarted(
      position: number,
      phaseCount: number,
      kind: PresentationPhaseKind,
      detail: string,
      at: number | null
    ): void {
      completePhase();
      phaseInFlight = { index: position, total: phaseCount, detail: `${kind} ${detail}` };
      lifecycle('phase', 'animating', at, `${position}/${phaseCount} ${kind} ${detail}`);
    },
    /**
     * Records the abandonment of the in-flight timeline, against the resolution it
     * actually belonged to.
     *
     * The playing phase is recorded as `aborted`; the timeline is `failed` when its
     * batch could not be presented at all and `aborted` when a newer delivered value
     * superseded it. A timeline that belonged to a battle the panel has already been
     * re-keyed away from is not recorded against the new battle.
     */
    recordAbandoned(
      abortedBattleId: string | null,
      at: number | null,
      reason: MoveLogAbortReason
    ): void {
      if (battleId !== abortedBattleId) {
        phaseInFlight = null;
        return;
      }

      const phase = phaseInFlight;
      phaseInFlight = null;

      if (phase !== null) {
        lifecycle('phase', 'aborted', at, `${phase.index}/${phase.total} ${phase.detail}`);
      }

      lifecycle('timeline', reason === 'malformed-batch' ? 'failed' : 'aborted', at, reason);
    },
    /** Records the terminal result, before the scene hands the battle off. */
    recordOutcome(outcome: {
      readonly outcome: string;
      readonly finalBossHp: number;
      readonly finalPlayerHp: number;
    }): void {
      ordinal += 1;
      append(
        formatMoveLogRow({
          kind: 'outcome',
          ordinal,
          outcome: outcome.outcome,
          finalBossHp: outcome.finalBossHp,
          finalPlayerHp: outcome.finalPlayerHp,
        })
      );
    },
    /**
     * Re-keys the panel to a delivered battle identity, clearing it when it changed.
     *
     * The identity is the battle the rows belong to: a delivered state push and the
     * event batch that follows it both carry it (`SIGNALR_PROTOCOL.md` §3, §4), and
     * a different one means the battle whose resolution was on screen has ended.
     */
    syncBattle,
    /**
     * Whether a delivered batch is a resolution the panel has not already recorded.
     *
     * Each resolution is recorded once: `serverSequence` is strictly increasing
     * (`SIGNALR_PROTOCOL.md` §3 item 2), so a batch at or below the newest one
     * already recorded is a repeat or an older delivery and adds no rows. This is
     * the panel's own bookkeeping — it changes no presentation behaviour, and the
     * diagnostic buffers the rest of the scene keeps are untouched by it.
     */
    beginBatch(envelope: BattleEventsEnvelope): boolean {
      syncBattle(envelope.battleId);

      return sequence === null || envelope.serverSequence > sequence;
    },
    /** Remembers the newest `serverSequence` recorded. */
    commitSequence(at: number): void {
      sequence = at;
    },
    /** Releases the panel's references: the engine owns the objects themselves. */
    release(): void {
      rows.length = 0;
      phaseInFlight = null;
    },
  };
}

/**
 * The Boss's display name for the authoritative `bossState.bossId`
 * (`BOSS_RULES.md` §6.4, `SIGNALR_PROTOCOL.md` §4.4 item 10).
 *
 * This is the **presentation lookup only**. The wire carries the canonical
 * technical Identity; the client resolves the human-readable name from the one
 * transcription of `BOSS_RULES.md` §6.4 that the Lobby already holds
 * (`MVP_BOSSES`) — no second catalog is created and none is fetched, because no
 * Boss read endpoint exists (`API_CONTRACTS.md` §1).
 *
 * An identity the catalog does not recognize is rendered **as the identity
 * itself**, never as a guessed name: a fabricated label would be client-authored
 * content standing in for authoritative state. The lookup resolves no Element
 * matchup, damage, or any other gameplay result.
 */
function resolveBossDisplayName(bossId: string): string {
  const boss = MVP_BOSSES.find((candidate) => candidate.bossId === bossId);

  return boss?.displayName ?? bossId;
}

/**
 * The Pet's display name for the owned instance identity the preserved pre-battle
 * loadout carries, resolved from the owned-Pet collection read.
 *
 * The name it renders is the Pet's `identity` member (`API_CONTRACTS.md` §5.1) —
 * the same string the Lobby's selection list shows — and nothing is derived from
 * it. When the identity is unknown (no preserved loadout, or a collection that does
 * not contain it) the panel shows the identity it was given, and with no identity
 * at all a neutral `Pet` placeholder: no name is ever invented, and no combat value
 * depends on the outcome (AGENTS.md §7).
 */
function resolvePetDisplayName(
  petId: string | null,
  catalog: ReadonlyMap<string, PetResponse>
): string {
  if (petId === null || petId.length === 0) {
    return 'Pet';
  }

  return catalog.get(petId)?.identity ?? petId;
}

/**
 * Maps the runtime's connection status to a short player-facing line and colour.
 *
 * It reports no transport detail — no hub/method name, no sync flag, no connection
 * id — because that readout is developer console material and not the player's
 * battle HUD (TASK-209 §5). A connected battle says nothing, so the line is empty
 * in the normal case and appears only when something needs the player's attention.
 */
function describeConnection(connection: GameRuntimeState['connection']): {
  readonly message: string;
  readonly color: string;
} {
  switch (connection) {
    case 'connected':
      return { message: '', color: '#94a3b8' };
    case 'reconnecting':
      return { message: 'Reconnecting…', color: '#fbbf24' };
    case 'error':
      return { message: 'Battle unavailable', color: '#f87171' };
    case 'connecting':
    default:
      return { message: 'Connecting…', color: '#94a3b8' };
  }
}

/**
 * Renders the delivered Passive's Reset Behavior as the display suffix of its
 * `current / threshold` readout (`PASSIVE_RULES.md` §4 item 2).
 *
 * Presentation only: the absence of `passiveResetOverride` means the default
 * reset behavior (`SIGNALR_PROTOCOL.md` §4.3 item 7) and is shown as such without
 * substituting a value for it, while a delivered override is printed under its own
 * contract name (`"Partial"` / `"NoReset"`) and never translated into a rule.
 */
function describePassiveReset(state: RuntimeBattleState['petState']): string {
  return ` (reset: ${state.passiveResetOverride ?? 'Default'})`;
}

/**
 * Renders the active Pet's delivered Status Effect instances
 * (`SIGNALR_PROTOCOL.md` §4.3 item 14, `GAME_STATE.md` §2.3.1).
 *
 * Presentation only: every value is printed as received. The scene does not
 * apply, refresh, decrement, expire, or remove an instance, does not evaluate
 * `remainingTurns` or `expiryCondition`, and interprets no `magnitude` — that
 * lifecycle is `GAME_STATE.md` §5.1.1's and the server's (§4.3 item 14,
 * `GAME_RULES.md` §18).
 *
 * The two duration models are mutually exclusive (`GAME_STATE.md` §2.3.1
 * item 3), so whichever one the element carries is shown and no value is
 * invented for the other. An empty collection is the documented spelling of
 * "no effect is active" — it is displayed as such rather than as a missing
 * member (§4.3 item 14).
 *
 * Each instance is rendered compactly from the members that identify it to a
 * player: its delivered `Id` (the effect's own identity, e.g. `Burn`, `Shield` —
 * `§2.3.1` item 1), its applied `Magnitude` and its duration. `Id` is printed
 * verbatim, so an identity this client has no definition for still reads as
 * itself rather than as a guessed name; there is no client-side Status Effect
 * catalog to look one up in, and inventing one would be inventing content
 * (`AGENTS.md` §7). `Type` and `Source` are deliberately not shown: they are the
 * instance's internal classification (`"DoT"` / `"BuffDebuff"` / `"Shield"` /
 * `"State"`, `"player"` / `"boss"`) and reading them adds no player-facing fact.
 * Nothing is counted or stacked: `§2.3.1` item 6 allows at most one instance per
 * identity, so no multiplicity is displayed.
 */
function describeStatusEffects(effects: RuntimeBattleState['petState']['statusEffects']): string {
  if (effects.length === 0) {
    return 'none';
  }

  return effects
    .map((effect) => `${effect.id} (${effect.magnitude}, ${describeStatusDuration(effect)})`)
    .join('; ');
}

/**
 * One delivered Status Effect instance's duration, as the single model its
 * element carries (`GAME_STATE.md` §2.3.1 item 3).
 *
 * An element carrying neither member is not a case the contract produces; it is
 * still rendered as a neutral "no duration" rather than as an invented one.
 */
function describeStatusDuration(effect: RuntimeBattleState['petState']['statusEffects'][number]): string {
  if (effect.remainingTurns !== undefined) {
    return `${effect.remainingTurns} turns`;
  }

  if (effect.expiryCondition !== undefined) {
    return effect.expiryCondition;
  }

  return 'no duration';
}
