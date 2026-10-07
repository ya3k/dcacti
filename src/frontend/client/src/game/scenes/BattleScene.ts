import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import { readPreservedLoadout } from '../state/PreservedLoadout';
import { MVP_BOSSES } from './LobbyScene';
import type { GameRuntime } from '../runtime/GameRuntime';
import type { GameRuntimeState } from '../../state/GameRuntimeState';
import type { BattleEventsEnvelope, RuntimeBattleState, RuntimeBoard } from '../runtime/GameRuntimeEvents';
import {
  RUNTIME_ACTION_SWAP,
  RUNTIME_ACTION_CARD_CAST,
  RUNTIME_ACTION_PET_SKILL_CAST,
} from '../runtime/GameRuntimeEvents';
import type {
  CardResponse,
  PetResponse,
  PetSignatureSkillResponse,
} from '../../services/api/CollectionModels';
import type { InBattleServerEvent } from './BattleEventPresenter';
import { parseInBattleEvent, formatInBattleEvent, selectBatchCallout } from './BattleEventPresenter';

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
 *      │ BOSS  Kim Lôi Vương              <connection>             │ ← boss band
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
 * safe area. Only the centered lines — the connection status, the board message
 * and the combat callout — use a centered origin.
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
 * The transient combat callout — the row directly above the board.
 *
 * It is the one line that reports *what just happened*: a Match, a Combo, a
 * cast, a Boss action or a Passive trigger. It is set from a delivered event and
 * fades; it holds no state, blocks no input, and nothing about the battle
 * depends on it. `boardMessageText` shares this slot, because "no state has
 * arrived yet" and "combat is resolving" are mutually exclusive.
 */
const CALLOUT_Y = BOARD_ORIGIN_Y - 16;
const CALLOUT_WIDTH = 460;
const CALLOUT_HOLD_MS = 700;
const CALLOUT_FADE_MS = 350;

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

/** Floating combat numbers: how far one rises, and for how long it lives. */
const FLOATER_RISE = 30;
const FLOATER_LIFE_MS = 600;

/** Colours for the transient combat feedback (floaters and the callout). */
const DAMAGE_TO_BOSS_COLOR = '#f87171';
const DAMAGE_TO_PET_COLOR = '#fb923c';
const POWER_GAIN_COLOR = '#a78bfa';
const POWER_LOSS_COLOR = '#fb7185';

/**
 * The MVP damage party identifiers (`SIGNALR_PROTOCOL.md` §3.2.14 item 3).
 *
 * They are read as delivered to decide *which panel* a damage floater is drawn
 * over. The mapping is presentation only: the scene applies no damage, derives
 * no HP from `amount`, and treats an unrecognized party as "no anchor here"
 * rather than guessing a side.
 */
const PLAYER_PARTY = 'player';
const BOSS_PARTY = 'boss';

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
   * is connected, reconnecting, or unavailable. It reports no transport detail
   * (no hub name, no connection id, no sync flag): those are diagnostics and are
   * deliberately not the player's HUD.
   */
  private connectionText: Phaser.GameObjects.Text | null = null;
  /** The Boss's display name, resolved from the client catalog by `bossState.bossId`. */
  private bossNameText: Phaser.GameObjects.Text | null = null;
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
  /** The drawn board cells, cleared and redrawn on each state push. */
  private boardLayer: Phaser.GameObjects.Container | null = null;
  /** Interactive cast controls container for equipped cards and signature skill. */
  private castControlsLayer: Phaser.GameObjects.Container | null = null;
  /**
   * The first selected cell's §1.0 index, or `null` when nothing is selected.
   *
   * Presentation-local input state only — it is not board state and is never
   * sent as anything but the two cells of a Swap request.
   */
  private selectedCell: number | null = null;
  /** True while a Swap request is outstanding, so further taps are ignored. */
  private swapPending = false;
  /** True while any action request (swap or cast) is in flight. */
  private actionInFlight = false;
  /** True while an in-battle event presentation sequence is playing, locking player input. */
  private presentationLocked = false;
  /** Guard ensuring only the first terminal outcome event transitions to ResultScene. */
  private outcomeHandled = false;
  /**
   * Developer/testing log of the events presented so far, in delivered order.
   *
   * It is **not rendered**: the raw event feed (`Match: … depth: 0`,
   * `DamageCalculated: base …`) is developer console material, not a player HUD.
   * It is kept as a readable diagnostic for development and tests, which is why
   * it is still accumulated while `feedbackText` no longer exists.
   */
  private presentedEventsLog: string[] = [];
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

  constructor() {
    super('BattleScene');
  }

  create(): void {
    this.runtime = readRuntime(this);
    this.drawRuntimeShell();
    this.registerBoardInput();

    void this.loadCardDefinitions();
    void this.loadPetCatalog();

    // Reflect current runtime state immediately, then follow transitions.
    this.renderRuntimeState(this.runtime?.getState() ?? null);
    this.renderBattleState(this.runtime?.getBattleState() ?? null);

    if (this.runtime) {
      this.runtimeUnsubscribe = this.runtime.onRuntimeEvent((event) => {
        this.renderRuntimeState(event.state);
      });
      this.battleStateUnsubscribe = this.runtime.onBattleState((state) => {
        this.renderBattleState(state);
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
   * tween-driven feedback  the combat callout's hold/fade and every floating
   *                        number — `tweens.killAll()` releases them with the
   *                        scene, so no presentation animation outlives it
   * per-battle guards      selectedCell, swapPending, actionInFlight,
   *                        presentationLocked, outcomeHandled — each describes the
   *                        battle that just ended, so the NEXT battle must start
   *                        from its initial value (`outcomeHandled` in
   *                        particular: left true, it discards the next outcome)
   * per-battle caches      presentedEventsLog, cardDefinitions, petCatalog and
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
    this.bossNameText = null;
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
    this.boardLayer?.destroy(true);
    this.boardLayer = null;
    this.castControlsLayer?.destroy(true);
    this.castControlsLayer = null;
    this.feedbackLayer?.destroy(true);
    this.feedbackLayer = null;
    this.tweens?.killAll();
    this.selectedCell = null;
    this.swapPending = false;
    this.actionInFlight = false;
    this.presentationLocked = false;
    this.outcomeHandled = false;
    this.presentedEventsLog = [];
    this.cardDefinitions.clear();
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

    return { fill, x, y, width, height };
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
   */
  private updateGauge(gauge: HudGauge | null, value: number, max: number): void {
    if (!gauge) {
      return;
    }

    const ratio = max > 0 ? value / max : 0;
    const proportion = Number.isFinite(ratio) ? Math.min(1, Math.max(0, ratio)) : 0;
    const fillWidth = Math.round(gauge.width * proportion);

    gauge.fill.setVisible(fillWidth > 0);
    gauge.fill.setSize(fillWidth, gauge.height);
    gauge.fill.setPosition(gauge.x + fillWidth / 2, gauge.y + gauge.height / 2);
  }

  /**
   * Presents the runtime's connection status in player-facing terms.
   *
   * This is the one line that reports infrastructure state, and it says only what
   * a player needs: whether the battle is connected, still connecting,
   * reconnecting, or unavailable. It reports no transport detail — no SignalR
   * label, no sync flag, no connection id — because that readout is developer
   * console material and not the player's battle HUD (TASK-209 §5).
   */
  private renderRuntimeState(state: GameRuntimeState | null): void {
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
   * Presents the synchronized authoritative state the server pushed
   * (`BattleStateUpdated`, SIGNALR_PROTOCOL.md §4) as the player-facing battle
   * HUD plus the 8 x 8 board.
   *
   * It presents the values verbatim. The scene computes nothing, adjusts
   * nothing, and owns nothing: these are server-authoritative fields
   * (GAME_STATE.md §2.0.5.4, ADR-001). No HP is derived from a damage event and
   * no Power is reconstructed from `PowerChanged` (§4.3 item 15, §4.4 item 7).
   */
  private renderBattleState(state: RuntimeBattleState | null): void {
    this.currentBattleState = state;

    if (state === null) {
      this.renderBossHud(null);
      this.renderPetHud(null, null);
      this.renderPlayerHud(null);
      this.boardMessage = 'Preparing the board…';
      this.boardMessageText?.setText(this.boardMessage);
      this.renderBoard(null);
      this.renderCastControls(null);
      return;
    }

    this.renderBossHud(state);
    this.renderPetHud(state, this.activePetId());
    this.renderPlayerHud(state);

    this.boardMessage = '';
    this.boardMessageText?.setText(this.boardMessage);

    this.renderBoard(state.board);
    this.renderCastControls(state);
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
   */
  private renderBossHud(state: RuntimeBattleState | null): void {
    if (!this.bossNameText || !this.bossHpText) {
      return;
    }

    if (state === null) {
      this.bossNameText.setText('');
      this.bossHpText.setText('');
      this.updateGauge(this.bossHpGauge, 0, 0);
      return;
    }

    const bossId = state.bossState.bossId;

    this.bossNameText.setText(resolveBossDisplayName(bossId));
    this.bossHpText.setText(`${state.bossState.hp} / ${state.bossState.maxHp}`);
    this.updateGauge(this.bossHpGauge, state.bossState.hp, state.bossState.maxHp);
  }

  /**
   * Presents the Pet panel: the Pet's name, its authoritative `hp / maxHp` and
   * Power, and the already-delivered Passive progress and Status Effect
   * instances.
   *
   * The HP and Power values are the delivered `petState` values and nothing else
   * (§4.3 item 15). The Pet's name is presentation content: `PetState` carries no
   * client-visible identity (ADR-014), so it is resolved from the client's own
   * loadout/catalog — the preserved pre-battle selection's `petId` plus the owned
   * Pet collection. When that identity is unknown the panel states what it has
   * (the identity it was given) or a neutral placeholder; it never invents a name.
   *
   * The Passive and Status Effect lines were already presented before TASK-209 and
   * are carried over unchanged: they are values the projection already delivers
   * (`petState.passiveId`, `passiveProgress`, `statusEffects[]`), so surfacing them
   * expands no contract. Nothing is derived: no Passive is charged, no Threshold is
   * evaluated, no progress is reset (§4.3 item 9), and no Status Effect is applied,
   * refreshed, decremented, expired, or removed (§4.3 item 14).
   */
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
   */
  private renderPetHud(state: RuntimeBattleState | null, petId: string | null): void {
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
    this.petHpText.setText(`${state.petState.hp} / ${state.petState.maxHp}`);
    this.updateGauge(this.petHpGauge, state.petState.hp, state.petState.maxHp);

    // The `0–100` range is the documented invariant of GAME_RULES.md §12, shown as
    // a static scale around the delivered value. No `maxPower` exists on the wire
    // and none is invented; no cost, affordability, or cast legality is derived
    // from the value (§4.3 item 15, CARD_RULES.md §3.6).
    this.petPowerText.setText(`${state.petState.power} / ${POWER_RANGE_MAX}`);
    this.updateGauge(this.powerGauge, state.petState.power, POWER_RANGE_MAX);

    const { current, threshold } = state.petState.passiveProgress;

    this.petPassiveText?.setText(`Passive: ${state.petState.passiveId}`);
    this.petPassiveProgressText?.setText(
      `${current} / ${threshold} Matches${describePassiveReset(state.petState)}`
    );
    // Presentation only: the delivered pair drawn as a proportion. Comparing the
    // pair to the Threshold — and every charge, trigger, overflow and reset rule —
    // is the server's (`PASSIVE_RULES.md` §2–§5, §4.3 item 9).
    this.updateGauge(this.passiveGauge, current, threshold);

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
   */
  private renderPlayerHud(state: RuntimeBattleState | null): void {
    if (!this.comboText || !this.matchesText) {
      return;
    }

    if (state === null) {
      this.comboText.setText('');
      this.matchesText.setText('');
      return;
    }

    const { combo, matchCount } = state.playerState;

    this.comboText.setText(combo >= 2 ? `COMBO ×${combo}` : '');
    this.matchesText.setText(`MATCHES ${matchCount}`);
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
      if (this.currentBattleState) {
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
        this.renderPetHud(this.currentBattleState, this.activePetId());
        this.renderCastControls(this.currentBattleState);
      }
    } catch {
      // Failed collection read is presentation feedback; the panel renders with the
      // identity it has.
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
   */
  private renderCastControls(state: RuntimeBattleState | null): void {
    if (!this.castControlsLayer) {
      return;
    }

    this.castControlsLayer.removeAll(true);

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

      const tile = this.add
        .rectangle(x, y, buttonWidth, buttonHeight, isPetSkill ? 0x4f46e5 : 0x1e293b)
        .setStrokeStyle(1, isPetSkill ? 0x818cf8 : 0x475569)
        .setInteractive({ useHandCursor: true });

      const label = this.add
        .text(x, y, labelText, {
          fontFamily: 'system-ui, sans-serif',
          fontSize: '12px',
          color: '#e2e8f0',
          fontStyle: 'bold',
        })
        .setOrigin(0.5)
        .setInteractive({ useHandCursor: true });

      const onTrigger = () => {
        if (isPetSkill) {
          void this.submitPetSkillCast();
        } else {
          void this.submitCardCast(cardId);
        }
      };

      tile.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, onTrigger);
      label.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, onTrigger);

      this.castControlsLayer.add([tile, label]);
    }
  }

  /**
   * Draws the authoritative board as 8 rows x 8 columns of 64 cells.
   *
   * The cells are rendered in the server's row-major order
   * (`MATCH3_RULES.md` §1.0): cell at index `i` is drawn at
   * `row = floor(i / 8)`, `column = i % 8`. The scene performs no other
   * transformation and produces no cell value of its own — every rendered Gem
   * comes from the payload (SIGNALR_PROTOCOL.md §4 item 10).
   */
  private renderBoard(board: RuntimeBoard | null): void {
    if (!this.boardLayer) {
      return;
    }

    // Redraw from scratch: the layer only ever shows the latest server state.
    this.boardLayer.removeAll(true);

    if (board === null) {
      this.boardMessageText?.setText(this.boardMessage);
      return;
    }

    const cells = board.cells;

    this.boardMessageText?.setText(this.boardMessage);

    for (let index = 0; index < cells.length; index++) {
      // The board's only coordinate convention (MATCH3_RULES.md §1.0). This is
      // the presentation mapping from that convention to screen space.
      const row = Math.floor(index / BOARD_COLUMNS);
      const column = index % BOARD_COLUMNS;

      if (row >= BOARD_ROWS || column >= BOARD_COLUMNS) {
        // Defensive: a payload with more than 64 cells would otherwise draw
        // outside the board. Such a payload is rejected by the runtime before it
        // reaches the scene, so this is unreachable in practice.
        break;
      }

      // Each delivered cell carries its Gem type and, optionally, the Special Gem
      // at that cell (SIGNALR_PROTOCOL.md §4.1 item 5). Only the Gem type is
      // presented: the scene paints no Special Gem state it was not asked to show
      // and infers nothing from the cell (GAME_RULES.md §18).
      this.drawCell(row, column, cells[index].gemType);
    }
  }

  /** Draws one board cell at its documented (row, column) position. */
  private drawCell(row: number, column: number, gemName: string): void {
    if (!this.boardLayer) {
      return;
    }

    const x = BOARD_ORIGIN_X + column * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2;
    const y = BOARD_ORIGIN_Y + row * (CELL_SIZE + CELL_GAP) + CELL_SIZE / 2;

    // An unrecognised Gem name is drawn as an inert placeholder. The scene never
    // substitutes a valid-looking Gem: doing so would fabricate authoritative
    // board content (SIGNALR_PROTOCOL.md §4 item 10).
    const presentation = GEM_PRESENTATION[gemName] ?? { color: 0x475569, label: '?' };

    const tile = this.add
      .rectangle(x, y, CELL_SIZE, CELL_SIZE, presentation.color)
      .setStrokeStyle(1, 0x0b0f19);

    const label = this.add
      .text(x, y, presentation.label, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '14px',
        color: '#0b0f19',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    this.boardLayer.add([tile, label]);
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
   * The handler is registered **at most once**. The board is redrawn on every
   * authoritative push (`renderBoard` → `removeAll(true)`), and the scene can be
   * re-entered, so a registration that assumed "one call per draw" would
   * accumulate listeners and submit one gesture's Swap several times. Detaching
   * the event's listeners before attaching makes the call idempotent without
   * changing the lifecycle pattern — no new abstraction is introduced, and the
   * board layer is the only object that listens here, so removing this event's
   * listeners cannot detach anyone else's handler.
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
   * event's local coordinates are already the same space `drawCell` draws in —
   * the mapping below is the exact inverse of `drawCell`'s
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
   * - first cell — selected, shown as selection feedback;
   * - second tap on the selected cell — selection cleared, nothing sent;
   * - second cell — the pair is submitted through the runtime port.
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
      this.renderSwapStatus();
      return;
    }

    if (this.selectedCell === cellIndex) {
      this.selectedCell = null;
      this.renderSwapStatus();
      return;
    }

    const fromCell = this.selectedCell;
    this.selectedCell = null;

    void this.submitSwap(fromCell, cellIndex);
  }

  /**
   * Submits one Swap request through the runtime port and presents the result.
   *
   * The scene calls the runtime and nothing else: it does not touch
   * `SignalRService`, the hub, or the board (`ARCHITECTURE.md` §2.2 rule 3).
   *
   * On `accepted: true` the scene changes **no** board, turn, or sequence value:
   * the next authoritative `BattleStateUpdated` push re-renders the board
   * (`SIGNALR_PROTOCOL.md` §3.1, §4). On `accepted: false` nothing is mutated
   * locally and nothing is retried automatically — the machine-readable `reason`
   * is shown as feedback only (`MATCH3_RULES.md` §2.1.5, §5 item 2).
   */
  private async submitSwap(fromCell: number, toCell: number): Promise<void> {
    if (this.isInputLocked()) {
      return;
    }

    if (!this.runtime) {
      this.renderSwapStatus('Swap unavailable: no runtime is connected.');
      return;
    }

    this.swapPending = true;
    this.actionInFlight = true;
    this.renderSwapStatus();

    try {
      // The pair is sent as selected, and both cells are §1.0 indices
      // (`MATCH3_RULES.md` §2.1.1). The runtime sources the battle id and the
      // correlation id; the scene supplies only the two cells the player chose.
      const acknowledgement = await this.runtime.requestAction({
        kind: RUNTIME_ACTION_SWAP,
        fromCell,
        toCell,
      });

      this.renderSwapStatus(undefined, fromCell, toCell, acknowledgement);
    } catch (error) {
      // A failed request is a transport/runtime failure — presentation state,
      // not game state (`SIGNALR_PROTOCOL.md` §8.3). No board value changes and
      // nothing is retried.
      this.renderSwapStatus(
        `Swap not sent: ${error instanceof Error ? error.message : String(error)}`
      );
    } finally {
      this.swapPending = false;
      this.actionInFlight = false;
    }
  }

  /**
   * Presents the Swap interaction's own feedback: the current selection, or the
   * outcome of the last submitted request.
   *
   * This is transport feedback (`SIGNALR_PROTOCOL.md` §2.1, §5) — not resolution
   * presentation. It reports no match, cascade, combo, damage, or boss value,
   * because the client computes none of those.
   */
  private renderSwapStatus(
    message?: string,
    fromCell?: number,
    toCell?: number,
    acknowledgement?: { readonly accepted: boolean; readonly reason?: string | null }
  ): void {
    if (!this.swapText) {
      return;
    }

    if (message !== undefined) {
      this.swapText.setText(message);
      this.swapText.setColor('#f87171');
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
        this.swapText.setText(
          `Swap ${fromCell} -> ${toCell}: rejected (${acknowledgement.reason ?? 'unknown'}).`
        );
        this.swapText.setColor('#fbbf24');
      }
      return;
    }

    if (this.swapPending) {
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
    if (this.isInputLocked()) {
      return;
    }

    if (!this.runtime) {
      this.renderCastStatus('CardCast unavailable: no runtime is connected.');
      return;
    }

    this.actionInFlight = true;
    this.renderCastStatus(`CardCast ${cardId} in flight…`);

    try {
      const acknowledgement = await this.runtime.requestAction({
        kind: RUNTIME_ACTION_CARD_CAST,
        cardId,
      });

      this.renderCastStatus(undefined, cardId, false, acknowledgement);
    } catch (error) {
      this.renderCastStatus(
        `CardCast ${cardId} not sent: ${error instanceof Error ? error.message : String(error)}`
      );
    } finally {
      this.actionInFlight = false;
    }
  }

  /**
   * Submits one PetSkillCast request through the runtime port and presents the result.
   */
  private async submitPetSkillCast(): Promise<void> {
    if (this.isInputLocked()) {
      return;
    }

    if (!this.runtime) {
      this.renderCastStatus('PetSkillCast unavailable: no runtime is connected.');
      return;
    }

    this.actionInFlight = true;
    this.renderCastStatus('PetSkillCast in flight…');

    try {
      const acknowledgement = await this.runtime.requestAction({
        kind: RUNTIME_ACTION_PET_SKILL_CAST,
      });

      this.renderCastStatus(undefined, undefined, true, acknowledgement);
    } catch (error) {
      this.renderCastStatus(
        `PetSkillCast not sent: ${error instanceof Error ? error.message : String(error)}`
      );
    } finally {
      this.actionInFlight = false;
    }
  }

  /**
   * Presents cast interaction transport feedback: in flight, accepted, or rejected
   * with server machine-readable reason (SIGNALR_PROTOCOL.md §2, §5).
   */
  private renderCastStatus(
    message?: string,
    cardId?: string,
    isSkill?: boolean,
    acknowledgement?: { readonly accepted: boolean; readonly reason?: string | null }
  ): void {
    if (!this.castText) {
      return;
    }

    if (message !== undefined) {
      this.castText.setText(message);
      this.castText.setColor(message.includes('in flight') ? '#94a3b8' : '#f87171');
      return;
    }

    if (acknowledgement) {
      const actionName = isSkill ? 'PetSkillCast' : `CardCast ${cardId}`;
      if (acknowledgement.accepted) {
        this.castText.setText(`${actionName}: accepted. Awaiting the server's state push.`);
        this.castText.setColor('#34d399');
      } else {
        this.castText.setText(
          `${actionName}: rejected (${acknowledgement.reason ?? 'unknown'}).`
        );
        this.castText.setColor('#fbbf24');
      }
      return;
    }

    this.castText.setText('');
  }

  /**
   * Handles server-authoritative battle events delivered via the runtime port.
   *
   * 1. Outcome branch first (TASK-087): when the envelope contains BattleWon or BattleLost,
   *    transitions immediately to ResultScene without delay.
   * 2. In-battle presentation: presents the non-outcome event types in exact array order
   *    while holding the scene-local input guard.
   *
   * It performs no result calculation: the server is authoritative for the
   * outcome and terminal HP values (GAME_RULES.md §18, ADR-001, AGENTS.md §10).
   *
   * The handoff also carries the batch's `battleId` (SIGNALR_PROTOCOL.md §3),
   * which is what `ResultScene` addresses the documented result endpoint with to
   * read the persisted reward summary (API_CONTRACTS.md §4). The outcome members
   * themselves are unchanged; the id is transport metadata the envelope already
   * carries, not a gameplay value.
   */
  private handleBattleEvents(envelope: BattleEventsEnvelope): void {
    if (this.outcomeHandled) {
      return;
    }

    const outcome = BattleScene.findOutcomeEvent(envelope.events);
    if (outcome) {
      this.outcomeHandled = true;
      this.scene.start('ResultScene', { ...outcome, battleId: envelope.battleId });
      return;
    }

    this.presentationLocked = true;
    try {
      this.presentEventBatch(envelope.events);
    } catch (error) {
      this.tweens?.killAll();
      this.presentationLocked = false;
      return;
    }
  }

  /**
   * Presents a batch of in-battle server events in the exact array order received.
   *
   * Three things happen, and all three are **presentation only** (`GAME_RULES.md`
   * §18, ADR-001):
   *
   * ```text
   * 1. every event is parsed and its developer-diagnostic line recorded in
   *    `presentedEventsLog` — for development and tests, never rendered
   *    (TASK-209 §5);
   * 2. the batch's damage and Power movement is drawn as transient floating
   *    numbers, anchored on the side the delivered event names (`damageAnchor`);
   * 3. at most ONE player-facing callout is shown for the whole batch — the most
   *    important one selectBatchCallout picks from the delivered events — so
   *    feedback stays selective rather than becoming a second event feed
   *    (TASK-210 §7).
   * ```
   *
   * Nothing here mutates state, holds a queue, or blocks the player: the callout
   * and the floaters fade on their own, and the input guard is released as soon
   * as this synchronous pass finishes (TASK-210 §8).
   */
  private presentEventBatch(events: readonly unknown[]): void {
    const presented: InBattleServerEvent[] = [];

    for (const raw of events) {
      const parsed = parseInBattleEvent(raw);
      if (!parsed) {
        continue;
      }

      presented.push(parsed);
      this.presentedEventsLog.push(formatInBattleEvent(parsed));
    }

    this.presentCombatFeedback(presented);

    const callout = selectBatchCallout(presented, (cardId) => this.cardDisplayName(cardId));
    if (callout) {
      this.showCallout(callout.message, callout.color);
    }

    if (this.tweens && presented.length > 0) {
      this.tweens.add({
        targets: this.feedbackLayer ?? {},
        alpha: { from: 0.6, to: 1 },
        duration: 150,
        onComplete: () => {
          this.presentationLocked = false;
        },
      });
    } else {
      this.presentationLocked = false;
    }
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
            this.spawnCellHighlight(cellIndex, 0xffffff, 0.4, 0xfacc15, 400);
          }
          previousDamage = null;
          break;
        }
        case 'GemMatched': {
          this.spawnCellHighlight(event.cellIndex, 0x60a5fa, 0.5, 0x38bdf8, 350);
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
   * The object is created, animated, destroyed on completion, and never stored:
   * nothing accumulates per frame, and the layer is destroyed with the scene
   * (TASK-210 §15). Without a tween manager (a unit harness) the number is left
   * in place, which is what makes it observable without a running clock.
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

  /** Returns whether player input is currently locked. */
  isInputLocked(): boolean {
    return this.presentationLocked || this.swapPending || this.actionInFlight;
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
