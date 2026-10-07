import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import { preserveLoadout, readPreservedLoadout } from '../state/PreservedLoadout';
import { formatCardSummary, formatRelicSummary } from '../presentation/ContentEffectFormat';
import type { GameRuntimePort } from '../runtime/GameRuntimeEvents';
import type { BattleStartRequest } from '../../services/api/BattleModels';
import type { CardResponse, PetResponse, RelicResponse } from '../../services/api/CollectionModels';

/**
 * The scene start data `LobbyScene` accepts.
 *
 * `ResultScene`'s approved `PLAY AGAIN` continuation passes
 * `{ restorePreservedLoadout: true }` (`D-202-01 = C`, `D-202-03 = D`; the
 * documented `scene.start(key, data)` mechanism `BattleScene → ResultScene`
 * already uses). That flag is the **only** thing that makes the scene consult
 * the preserved-loadout carrier: `MainMenuScene`'s `START BATTLE` starts this
 * scene without it, so a main-menu entry and a first-ever entry keep exactly
 * their existing behavior. Preservation is approved for the `PLAY AGAIN`
 * return only, and nothing here invents behavior for the other entries
 * (`ARCHITECTURE.md` §2.2.3, ADR-022).
 */
export interface LobbySceneData {
  /**
   * Restore the loadout preserved by the battle that just ended
   * (`ResultScene`'s `PLAY AGAIN`). Absent or `false` on every other entry.
   */
  readonly restorePreservedLoadout?: boolean;
}

/**
 * One selectable entry of the Lobby's static Boss catalog.
 *
 * It carries **selection/presentation metadata only** (`BOSS_RULES.md` §6): the
 * identity the request submits and the two content values the UI shows. It holds
 * no Boss stat, threshold, Passive, Skill, effect, or balance value, and none is
 * needed here — the server is authoritative for every one of them
 * (`GAME_RULES.md` §18, ADR-001).
 */
export interface MvpBossOption {
  /**
   * The Boss's canonical technical Identity (`BOSS_RULES.md` §6.4) — the value
   * this scene submits as the request's `bossId` (never the display name, and
   * never a `BossDefinitionId`, which is the persistence key of `DATABASE.md`
   * §1).
   */
  readonly bossId: string;
  /**
   * The human-readable content name (`BOSS_RULES.md` §6). Presentation only: it
   * is never submitted, and never used as a technical identifier.
   */
  readonly displayName: string;
  /**
   * The Boss's Element as the design documents name it (`BOSS_RULES.md` §6.1 —
   * `Hỏa`/`Thủy`/`Mộc`/`Thổ`/`Kim`). Display only, for the player's own
   * comparison; the Element that reaches the wire is the server's
   * `bossState.element` (`API_CONTRACTS.md` §3), not this label.
   */
  readonly element: string;
}

/**
 * The five canonical MVP Bosses the player may choose between.
 *
 * `BOSS_RULES.md` §6 defines exactly five content-defined MVP Bosses and §6.4
 * fixes each canonical technical Identity, so this list is a **transcription of
 * that contract, not a second definition of it**: every `bossId` below is the
 * §6.4 value verbatim, and each `displayName`/`element` is §6's own
 * presentation content. The client has no Boss data source — there is no Boss
 * collection read and no Boss endpoint (`API_CONTRACTS.md` §1) — and Bosses are
 * global static content with no ownership, so the five identities are held here
 * rather than fetched (`ARCHITECTURE.md` §5, `AGENTS.md` §9).
 *
 * The list is closed: it is not a registry, it is not extensible at runtime, and
 * adding a sixth entry would be inventing Boss content that `BOSS_RULES.md` §6
 * does not define. It contains no gameplay: the server resolves the submitted
 * identity through its own `BossDefinitions.All` and answers `400
 * BOSS_NOT_FOUND` for anything else (`API_CONTRACTS.md` §3).
 */
export const MVP_BOSSES: readonly MvpBossOption[] = [
  { bossId: 'boss-hoa-long', displayName: 'Hỏa Long', element: 'Hỏa' },
  { bossId: 'boss-thuy-ma', displayName: 'Thủy Ma', element: 'Thủy' },
  { bossId: 'boss-moc-yeu', displayName: 'Mộc Yêu', element: 'Mộc' },
  { bossId: 'boss-son-thach-ve', displayName: 'Sơn Thạch Vệ', element: 'Thổ' },
  { bossId: 'boss-kim-loi-vuong', displayName: 'Kim Lôi Vương', element: 'Kim' },
];

/**
 * The documented loadout cardinalities the interaction is designed around:
 * exactly one active Pet (`PET_RULES.md` §2), exactly three Basic Cards
 * (`CARD_RULES.md` §1), and three to five Relics (`RELIC_RULES.md` §2.1).
 *
 * These shape the *interaction* — how many slots the player may fill. They are
 * not a validity judgment: ownership, count, category, copy limit, and
 * distinctness are validated by the server on `POST /api/battle/start`
 * (`API_CONTRACTS.md` §3, `ARCHITECTURE.md` §2.2.3 rule 5).
 */
const CARD_LOADOUT_SIZE = 3;
const MIN_RELIC_LOADOUT_SIZE = 3;
const MAX_RELIC_LOADOUT_SIZE = 5;

/** The one column pitch the three collection lists are laid out with, in game pixels. */
const COLUMN_PITCH = 372;
const COLUMN_ORIGIN_X = SAFE_AREA.x + 26;
const LIST_TOP = SAFE_AREA.y + 84;
const ROW_HEIGHT = 30;

/**
 * How many collection rows one column can show before the Boss band begins.
 *
 * It is the column grid's own capacity, not a content limit: the three columns
 * are laid out between the header band and the Boss band below them
 * ({@link BOSS_HEADER_TOP}), which the bottom text band then follows. Five Pets
 * and three Basic Cards fit inside it — MVP's ten Relics do not
 * (`MVP_SCOPE.md` §1), so the Relic column reaches the rest of its owned
 * instances through its own pager ({@link LOBBY_RELIC_PREV_BUTTON}); the other
 * two lists need none and have none.
 */
const MAX_LIST_ROWS = 8;

/** The x the Relic column starts at, and the safe area's own right edge. */
const RELIC_COLUMN_X = COLUMN_ORIGIN_X + COLUMN_PITCH * 2;
const SAFE_AREA_RIGHT = SAFE_AREA.x + SAFE_AREA.width;

/**
 * The Relic column's pager geometry, in logical game pixels.
 *
 * <b>Why a pager exists.</b> MVP grants ten Relics (`MVP_SCOPE.md` §1) against a
 * column grid with {@link MAX_LIST_ROWS} rows before the Boss band, so without
 * one the bottom two owned instances would be unreachable — a live product
 * defect (TASK-213 §3.2 C-2). Rewriting the grid to fit ten rows is not
 * available: the columns, the Boss band, and the bottom text band already
 * occupy the 672 px safe area exactly, and ten rows at the 30 px pitch would run
 * into the Boss band. Paging is therefore the smallest bounded mechanism the
 * existing scene architecture supports.
 *
 * <b>Where it sits.</b> The controls live in the Relic column's own **header
 * line**: the header literal is left-aligned at that column's origin and the
 * collection rows start 26 px lower, so the pager cannot overlap a row, the
 * Boss band, the bottom text band, the START BATTLE trigger, or the `< BACK` /
 * `RETRY` corner — whatever the selection contains. It is drawn only when the
 * column actually has more owned instances than one page holds, so a collection
 * that fits renders exactly as it did before.
 *
 * They are exported so the layout can be asserted against the same numbers the
 * scene draws with, rather than against literals in a test.
 */
const RELIC_PAGE_CONTROL_Y = SAFE_AREA.y + 58 + 11;
const RELIC_PAGE_CONTROL_WIDTH = 74;
const RELIC_PAGE_CONTROL_HEIGHT = 22;
const RELIC_PAGE_CONTROL_GAP = 10;

export const LOBBY_RELIC_NEXT_BUTTON = {
  x: SAFE_AREA_RIGHT - 26 - RELIC_PAGE_CONTROL_WIDTH / 2,
  y: RELIC_PAGE_CONTROL_Y,
  width: RELIC_PAGE_CONTROL_WIDTH,
  height: RELIC_PAGE_CONTROL_HEIGHT,
} as const;

export const LOBBY_RELIC_PREV_BUTTON = {
  x:
    LOBBY_RELIC_NEXT_BUTTON.x -
    RELIC_PAGE_CONTROL_WIDTH / 2 -
    RELIC_PAGE_CONTROL_GAP -
    RELIC_PAGE_CONTROL_WIDTH / 2,
  y: RELIC_PAGE_CONTROL_Y,
  width: RELIC_PAGE_CONTROL_WIDTH,
  height: RELIC_PAGE_CONTROL_HEIGHT,
} as const;

/**
 * The pager's page indicator, in logical game pixels.
 *
 * It is right-aligned to end just left of `PREV`, inside the Relic column and
 * clear of the column's own header literal. It states which owned instances the
 * page is showing (`first-last of total`) so the player can tell that more
 * instances exist — a control with no such statement would leave "is that all of
 * them?" unanswerable.
 */
const RELIC_PAGE_INDICATOR_RIGHT =
  LOBBY_RELIC_PREV_BUTTON.x - RELIC_PAGE_CONTROL_WIDTH / 2 - RELIC_PAGE_CONTROL_GAP;

/**
 * The content line under a collection row's identity line, in game pixels.
 *
 * A Card and a Relic each state **what they change**, and neither statement fits
 * beside the row's name within one column: the widest provisioned Card rule and
 * the widest provisioned Relic rule both exceed the column pitch. The content
 * therefore takes the row's own second line rather than a tooltip, a hover
 * surface, or a second panel — `LobbyScene` has no tooltip framework to reuse,
 * and adding one would be a new interaction system rather than a presentation
 * change (`ARCHITECTURE.md` §5, `AGENTS.md` §9).
 *
 * The geometry is deliberately inside the existing row pitch: the identity line
 * keeps its 14 px monospace at the row's own `y`, and the content line is a
 * 12 px monospace at `y + 15`, so the pair occupies 15 + 13 = 28 px of the 30 px
 * pitch and cannot touch the row below it or the Boss band beneath the lists.
 * The content text is the `API_CONTRACTS.md` §5.3/§5.4 values rendered by
 * `ContentEffectFormat`, and no rule, magnitude, or threshold is computed here.
 */
const ROW_CONTENT_OFFSET = 15;
const ROW_CONTENT_FONT_SIZE = '12px';
/** The dimmer tone a row's content line uses when the row is not selected. */
const ROW_CONTENT_COLOR = '#94a3b8';

/**
 * How wide a row's content line may be before it wraps, per column (game
 * pixels).
 *
 * It is the horizontal room the column actually has: the Cards column stops
 * where the Relics column begins, and the Relics column stops at the safe
 * area's right edge. Wrapping is the fail-safe for a rule longer than the
 * provisioned content, so an over-long statement can never run under the
 * neighbouring column (`TASK-186`'s non-overlap contract).
 */
const CARD_CONTENT_WIDTH = COLUMN_PITCH - 12;
const RELIC_CONTENT_WIDTH = SAFE_AREA_RIGHT - RELIC_COLUMN_X;

/**
 * The Boss-selection band, in game pixels.
 *
 * Step 4 sits **below** the three collection columns rather than beside them:
 * those three present owned collections read from the server, while the Boss
 * choice is the five global static options this scene holds itself. The band
 * starts below the tallest possible collection list (the columns reach
 * `LIST_TOP + MAX_LIST_ROWS * ROW_HEIGHT`) and lists one option per canonical
 * Boss, at the same row pitch the collection lists use, so all five are fully
 * legible and clear of the Runtime Status overlay's corner.
 */
const BOSS_HEADER_TOP = LIST_TOP + MAX_LIST_ROWS * ROW_HEIGHT + 22;
const BOSS_LIST_TOP = BOSS_HEADER_TOP + 26;

/**
 * Vertical offsets from the safe area bottom for the bottom-left text band.
 *
 * Derived from the measured line height (15 px for 14px monospace) and the line
 * counts of the four blocks:
 *   selectionText (3 lines = 45 px)
 *   reviewText    (4 lines = 60 px)
 *   messageText   (1 line  = 15 px)
 *   errorText     (1 line  = 15 px)
 * in the 165 px band between the 5th Boss row (bottom y = 531) and the safe
 * area bottom (y = 696), leaving >= 4 px positive gap between all blocks:
 *   selectionText: bottom - 161 (y = 535..580, gap to boss row = 4 px)
 *   reviewText:    bottom - 111 (y = 585..645, gap to selection = 5 px)
 *   messageText:   bottom -  46 (y = 650..665, gap to review = 5 px)
 *   errorText:     bottom -  26 (y = 670..685, gap to message = 5 px, inset = 11 px)
 */
export const SELECTION_TEXT_BOTTOM_OFFSET = 161;
export const REVIEW_TEXT_BOTTOM_OFFSET = 111;
export const MESSAGE_TEXT_BOTTOM_OFFSET = 46;
export const ERROR_TEXT_BOTTOM_OFFSET = 26;

/**
 * The Lobby's two outbound controls, in logical game pixels.
 *
 * `< BACK` is drawn on every render pass; `RETRY` is drawn only while the
 * current error belongs to an operation that can be repeated. They share one
 * row in the top-right corner, which is the one band of this layout that is
 * structurally free: the header is a fixed, left-aligned literal, and the
 * collection columns' title band starts 27 px lower. So neither control can
 * overlap the three collection lists, the Boss band, the bottom text band, the
 * START BATTLE trigger, or each other, whatever the selection contains.
 *
 * They are exported so the layout can be asserted against the same numbers the
 * scene draws with, rather than against literals in a test.
 */
export const LOBBY_BACK_BUTTON = {
  x: SAFE_AREA.x + SAFE_AREA.width - 20 - 140 / 2,
  y: SAFE_AREA.y + 31,
  width: 140,
  height: 36,
} as const;

export const LOBBY_RETRY_BUTTON = {
  x: LOBBY_BACK_BUTTON.x - 140 / 2 - 12 - 120 / 2,
  y: SAFE_AREA.y + 31,
  width: 120,
  height: 32,
} as const;

/** The two operation names `RETRY` can repeat. */
type LobbyOperation = 'collection' | 'start';

/**
 * The pre-battle presentation colours. Display only: a Card's `category` is a
 * documented wire member (`API_CONTRACTS.md` §5.3) and is coloured per value so
 * the two categories are distinguishable; an unrecognised value falls back to a
 * neutral tone rather than to a guessed category.
 */
const CATEGORY_COLORS: Readonly<Record<string, string>> = {
  Basic: '#34d399',
  PetSkill: '#94a3b8',
};

/**
 * LobbyScene — battle preparation, loadout review, and the match-start trigger
 * (`TDD.md` §2.1).
 *
 * It owns the MVP pre-battle selection flow of `GDD.md` §2 — Choose Pet →
 * Equip Cards → Equip Relics → Choose Boss → Start Battle — as presentation and
 * interaction only:
 *
 * ```text
 * getPets() / getCards() / getRelics()      (selection source, ARCHITECTURE.md §2.2.3 rule 4)
 *         ↓
 * in-progress selection                     (ephemeral, scene-local — rules 1–2)
 *         ↓
 * BattleStartRequest                        (API_CONTRACTS.md §3, exactly four members)
 *         ↓
 * runtime.startBattle(request)              (rule 5 — the runtime owns REST → connect → join)
 * ```
 *
 * **Each Card and Relic row states what that option changes.** The Lobby is the
 * loadout decision point, so a row carrying only a name would leave "what does
 * this do?" unanswerable before `START BATTLE` (`GDD.md` §17, §8/§10). The Card
 * and Relic rows therefore draw a second line read from the response's own
 * `API_CONTRACTS.md` §5.3 `effectDefinition` / §5.4
 * `trigger`/`condition`/`effectDefinition`, rendered by
 * `presentation/ContentEffectFormat` and by nothing else. The scene computes no
 * magnitude, threshold, cost, or legality; keeps no content catalog; and has no
 * per-Card or per-Relic lookup table — a row's text is a function of the
 * response alone, and a member the response omits renders nothing rather than a
 * guessed value.
 *
 * **The Relic column pages, so every owned instance is reachable.** MVP grants
 * ten Relics (`MVP_SCOPE.md` §1) against a column grid with eight rows before the
 * Boss band, so the column shows one page of {@link MAX_LIST_ROWS} owned
 * instances at a time and offers `PREV` / `NEXT` beside its own header when there
 * is more than one page. It is a bounded presentation mechanism, not a new
 * subsystem: the page is one scene field, the selection is untouched by it, and a
 * collection that fits on one page renders exactly as it did before. Nothing here
 * decides ownership, count, category, copy limit, or distinctness — the server
 * validates the submitted request (`API_CONTRACTS.md` §3,
 * `ARCHITECTURE.md` §2.2.3 rule 5).
 *
 * **It reads the collection through the runtime port and nothing else.** It
 * imports neither `@microsoft/signalr`, nor `fetch`, nor `ApiService`, nor the
 * Discord SDK: `services/api/` and `services/realtime/` are transport layers and
 * a scene reaching either would bypass the boundary
 * (`ARCHITECTURE.md` §2.2.1 rule 1, §2.2.3 rule 3). The one content list it does
 * hold — the five canonical MVP Bosses of {@link MVP_BOSSES} — is a static
 * transcription of `BOSS_RULES.md` §6.4, not a collection read: Bosses are
 * global content with no ownership and no endpoint serves them
 * (`API_CONTRACTS.md` §1), so there is nothing to fetch and no port capability
 * to add (`ARCHITECTURE.md` §2.2.3 rule 6).
 *
 * **The in-progress selection is scene state and lives nowhere else.** It is
 * created with the scene, held in the scene's own fields, and discarded by the
 * scene's own teardown — which is attached to Phaser's actual scene lifecycle
 * events (`Phaser.Scenes.Events.SHUTDOWN` / `DESTROY`) in `create()`, because a
 * method named `shutdown()` is not itself an engine hook. It is not in
 * `state/GameRuntimeState.ts` (rule 5), not on
 * `GameRuntime` (rule 2), and has no store, manager, or module of its own
 * (rule 1, `AGENTS.md` §9).
 *
 * **The one documented exception is the preserved loadout** (ADR-022,
 * `ARCHITECTURE.md` §2.2.3). When this scene successfully starts a battle it
 * publishes the submitted request to the client game-presentation layer's
 * preserved-loadout carrier, and when it is entered through the approved
 * `ResultScene → PLAY AGAIN` continuation it restores that selection before the
 * first render. Preservation changes the selection's lifetime, not its nature:
 * the restored selection is the scene's own editable state, never a lock and
 * never a client-side legality decision, and the scene's teardown still discards
 * the scene's copy while leaving the carrier alone — the carrier is not battle
 * state and is not cleared by the post-result cleanup (`D-202-04 = A`).
 *
 * **The scene decides no legality.** The UI limits how many items can be picked;
 * it does not compute whether the selection is valid. Count, ownership,
 * category, copy limit, and distinctness are the server's answer
 * (`API_CONTRACTS.md` §3), and that answer is what this scene reports.
 *
 * **The scene is not a dead end and not a trap.** `< BACK` returns to
 * `MainMenuScene` from every state, and a failed operation — the collection
 * read, or the battle start — reports itself with a working `RETRY` beside it.
 * `RETRY` repeats only the operation that actually failed and never creates a
 * second concurrent one. Leaving the Lobby **invalidates the run in flight**
 * (`asyncRun`), so a battle start that settles after the player pressed
 * `< BACK` can neither preserve a loadout nor open a battle. Collection and
 * Battle History are deliberately *not* reached from here: they are main-menu
 * destinations, and `< BACK` is the one documented exit (`D-202-01 = C`).
 *
 * It obtains the port with `readRuntime(this)`. When no runtime was supplied the
 * scene still runs and reports that the flow is unavailable, so scene lifecycle
 * never depends on runtime availability.
 */
export class LobbyScene extends Phaser.Scene {
  private runtime: GameRuntimePort | null = null;

  /**
   * Whether this instance was entered through the approved `ResultScene` →
   * `PLAY AGAIN` continuation and must therefore restore the preserved loadout.
   *
   * Set from the scene start data (`LobbySceneData`) and reset by the scene's
   * teardown, so a normal entry — `MainMenuScene`'s `START BATTLE`, or a restart
   * — never restores anything.
   */
  private restorePreservedLoadout = false;

  // --- In-progress selection (ephemeral scene state; cleared on shutdown) ---

  /**
   * The selected owned Pet's **instance** identity (`Pet.PetInstanceId`,
   * `API_CONTRACTS.md` §5.1) — never a Pet definition id, never hardcoded, and
   * `null` until the player picks one from the loaded collection.
   */
  private selectedPetId: string | null = null;
  /**
   * The selected Basic Card `CardDefinitionId` values, in the order chosen.
   * Bounded by {@link CARD_LOADOUT_SIZE} slots.
   */
  private selectedCardIds: string[] = [];
  /**
   * The selected owned Relic **instance** ids, in the order chosen.
   *
   * The order **is** the equip slot order — position *i* is slot *i + 1*
   * (`RELIC_RULES.md` §2.3) — so it is submitted exactly as selected and is
   * never sorted, de-duplicated, or re-derived (`ARCHITECTURE.md` §2.2.3
   * rule 4).
   */
  private selectedRelicIds: string[] = [];

  /**
   * Which page of the Relic column is shown, 0-based.
   *
   * The column shows {@link MAX_LIST_ROWS} owned instances at a time, and MVP
   * grants ten (`MVP_SCOPE.md` §1), so this is how the player reaches the
   * instances that do not fit. It is plain scene state like the selection
   * itself: no scroll container, no viewport object, and no store is introduced
   * (`AGENTS.md` §9). It is deliberately **not** part of the selection — paging
   * shows a different slice of the same owned set and can never add, remove, or
   * reorder a selected instance, and it is carried through the request only
   * through {@link selectedRelicIds} (`RELIC_RULES.md` §2.3: the selection's own
   * order is the equip order).
   */
  private relicPage = 0;

  /**
   * The selected Boss's canonical technical Identity (`BOSS_RULES.md` §6.4) —
   * one of {@link MVP_BOSSES}'s `bossId` values, and the single source of the
   * request's `bossId`.
   *
   * It starts `null` and stays `null` until the player explicitly picks a Boss:
   * the MVP has **no default Boss**, so no Boss is pre-selected and nothing
   * substitutes one if the player never chooses (`requestStart` reports the
   * unfilled slot instead of submitting). Selecting a different Boss replaces
   * this value; there is no second, parallel record of the choice
   * (`ARCHITECTURE.md` §2.2.3 rule 1).
   */
  private selectedBossId: string | null = null;

  // --- Loaded collection (mirrors exactly what the reads returned) ---

  private ownedPets: PetResponse[] = [];
  private ownedCards: CardResponse[] = [];
  private ownedRelics: RelicResponse[] = [];

  // --- Presentation state ---

  /** The collections are being read through the port. */
  private loading = false;
  /**
   * A `startBattle` request is outstanding.
   *
   * The scene-local in-flight guard: while it is set the start trigger is
   * ignored, so repeated activation issues exactly one request. No request is
   * queued. It is released by the attempt's own failure, which is what lets
   * `RETRY` submit again (`ARCHITECTURE.md` §2.2.3 rule 5).
   */
  private startPending = false;
  /** A selection-driven message (e.g. "select a Pet first") shown to the player. */
  private selectionMessage = '';
  /** The player-facing feedback from the last failed operation, or `null`. */
  private startError: string | null = null;
  /**
   * Which operation {@link startError} belongs to, so `RETRY` repeats that one
   * and nothing else. `null` when there is no error, or when the failure has no
   * repeatable operation behind it (no runtime was published).
   */
  private failedOperation: LobbyOperation | null = null;

  /**
   * Identifies this scene instance's current asynchronous run.
   *
   * `create()` takes the next value and hands it to every operation it starts,
   * and both `create()` and the scene's teardown advance it. An operation
   * therefore writes — or navigates — only while the run that started it is
   * still the current one: a collection read that settles after the scene was
   * shut down (its objects destroyed by Phaser's `DisplayList#shutdown`), and a
   * `startBattle` that settles after the player pressed `< BACK`, are both
   * discarded instead of mutating a presentation that is no longer theirs.
   *
   * `< BACK` advances it explicitly as well: leaving the Lobby must invalidate
   * the attempt in flight, not merely happen to be followed by a teardown.
   *
   * It is plain scene-local state, like the other per-run guards here: no
   * cancellation registry, controller, or global state is introduced
   * (`AGENTS.md` §9). It is the same pattern `ResultScene`'s `rewardLoadRun`
   * uses for the reward read.
   */
  private asyncRun = 0;

  /**
   * Guards the one scene transition this run may make — to `MainMenuScene`
   * (`< BACK`) or to `BattleScene` (a successful start).
   *
   * Both share it: a second activation of either cannot start a second scene,
   * and a repeated `START`/`RETRY` cannot produce a duplicate transition
   * (`MainMenuScene.transitionTo`'s documented precedent).
   */
  private hasTransitioned = false;

  /** Rebuilt on every render pass; each entry is one interactive hit area. */
  private interactiveObjects: Phaser.GameObjects.GameObject[] = [];
  private renderedTexts: Phaser.GameObjects.Text[] = [];

  private headerText: Phaser.GameObjects.Text | null = null;
  private petsHeaderText: Phaser.GameObjects.Text | null = null;
  private cardsHeaderText: Phaser.GameObjects.Text | null = null;
  private relicsHeaderText: Phaser.GameObjects.Text | null = null;
  private bossHeaderText: Phaser.GameObjects.Text | null = null;
  private selectionText: Phaser.GameObjects.Text | null = null;
  private reviewText: Phaser.GameObjects.Text | null = null;
  private messageText: Phaser.GameObjects.Text | null = null;
  private errorText: Phaser.GameObjects.Text | null = null;

  constructor() {
    super('LobbyScene');
  }

  /**
   * Scene start — reads the entry's start data before `create()` renders.
   *
   * Phaser calls this on every start, with `undefined` when the scene was
   * started without data, so the flag never survives from a previous run.
   */
  init(data?: LobbySceneData): void {
    this.restorePreservedLoadout = data?.restorePreservedLoadout === true;
  }

  create(): void {
    this.runtime = readRuntime(this);

    // Attach this scene's teardown to Phaser's own scene lifecycle events,
    // before any of the resources it releases is created.
    //
    // A scene method that merely *exists* is never invoked: Phaser 4.2.1 calls
    // `init`/`preload`/`create`/`update` by name (`SceneManager.bootScene` /
    // `SceneManager.create`) and nothing else, and `Phaser.Scene` declares no
    // `shutdown` method at all. What the engine actually emits when it takes a
    // scene down is `Phaser.Scenes.Events.SHUTDOWN` — from
    // `Phaser.Scenes.Systems#shutdown`, which `SceneManager` calls for a queued
    // `stop` and when restarting a running scene — and `DESTROY` from
    // `Systems#destroy`. That is the documented scene-event mechanism
    // (`.ai/skills/client/phaser-architecture` "Resource Lifecycle & Cleanup";
    // `.ai/skills/phaser/scenes` gotcha 14); without it the teardown below never
    // runs in the browser and this instance's in-progress selection survives
    // into the next run, against `ARCHITECTURE.md` §2.2.3 rule 1.
    //
    // The registration is idempotent — the pair is detached first, then attached
    // with `once` — so a scene that is shut down and later restarted holds
    // exactly one teardown handler per event instead of stacking another one on
    // every run (`once` alone would leave the run's unfired `DESTROY` handler
    // behind). This is the same "detach before attach" idempotency
    // `BattleScene.registerBoardInput` already uses, and `once` means the
    // handler is consumed by the event that fired it.
    this.events.off(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.off(Phaser.Scenes.Events.DESTROY, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.DESTROY, this.shutdown, this);

    // A scene instance is reusable, so this run's own state is established here
    // rather than inherited from the previous one: the asynchronous run
    // identity advances, and the previous run's transport guards and feedback
    // are cleared. The run identity is what makes a continuation belonging to
    // the ended run harmless (see the field's own note).
    const run = ++this.asyncRun;
    this.loading = false;
    this.startPending = false;
    this.selectionMessage = '';
    this.startError = null;
    this.failedOperation = null;
    this.hasTransitioned = false;
    this.relicPage = 0;

    // Restore before the first render, so the preserved selection is what the
    // player sees immediately — including while the collection read is still in
    // flight. Only the approved PLAY AGAIN entry reads the carrier.
    this.applyPreservedLoadout();

    this.drawShell();
    this.render();

    if (this.runtime === null) {
      // No runtime: the flow cannot read a collection or start a battle. The
      // scene stays alive and says so rather than throwing (task §4). There is
      // no operation to repeat, so no RETRY is offered.
      this.startError = 'No runtime is available: cannot load the collection or start a battle.';
      this.render();
      return;
    }

    void this.loadCollection(run);
  }

  /**
   * Scene teardown — the handler subscribed to Phaser's own
   * `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY` events by `create()`.
   *
   * It is a plain method invoked *by the engine's event*, not an engine hook:
   * Phaser never calls a scene method named `shutdown` on its own, so before
   * this registration existed the body below never ran in the browser.
   *
   * Everything the scene owned is dropped here, including the in-progress
   * selection: a shut-down lobby has no selection, and the runtime keeps none on
   * its behalf (`ARCHITECTURE.md` §2.2.3 rules 1–2). Detaching the hit areas
   * prevents pointer handlers from surviving a scene restart — Phaser has
   * already destroyed those game objects by the time its `DisplayList#shutdown`
   * has handled this same event, so only the scene's own handler registrations
   * and references are left to release.
   *
   * The **preserved-loadout carrier is deliberately not cleared here**: it is
   * not this scene's copy of the selection but the game instance's
   * presentation state, and it exists precisely so it can outlive this scene
   * (`ARCHITECTURE.md` §2.2.3, ADR-022). This is also why the post-result
   * active-battle cleanup leaves it untouched.
   *
   * The body is idempotent and safe on both events: every assignment is a plain
   * reset, detaching an already-detached handler is harmless, and running it
   * twice simply repeats the reset.
   */
  shutdown(): void {
    // Everything this run's asynchronous work asked for is stale from here on:
    // the run identity advances before any reference is dropped, so a promise
    // that settles later — a collection read, a `startBattle` — writes nothing
    // and navigates nowhere.
    this.asyncRun += 1;

    for (const object of this.interactiveObjects) {
      object.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);
    }
    this.interactiveObjects = [];
    this.renderedTexts = [];

    this.selectedPetId = null;
    this.selectedCardIds = [];
    this.selectedRelicIds = [];
    this.selectedBossId = null;
    this.restorePreservedLoadout = false;
    this.relicPage = 0;

    this.ownedPets = [];
    this.ownedCards = [];
    this.ownedRelics = [];

    this.loading = false;
    this.startPending = false;
    this.selectionMessage = '';
    this.startError = null;
    this.failedOperation = null;
    this.hasTransitioned = false;

    this.headerText = null;
    this.petsHeaderText = null;
    this.cardsHeaderText = null;
    this.relicsHeaderText = null;
    this.bossHeaderText = null;
    this.selectionText = null;
    this.reviewText = null;
    this.messageText = null;
    this.errorText = null;
  }

  // ---------------------------------------------------------------------------
  // Collection read (through the runtime port)
  // ---------------------------------------------------------------------------

  /**
   * Reads the owned Pet, Card, and Relic collections through the runtime port.
   *
   * The reads are a **selection source** (`ARCHITECTURE.md` §2.2.3 rule 4). The
   * responses are shown as received: nothing is filtered, sorted, defaulted, or
   * repaired, and when a read fails or returns an empty set the UI shows exactly
   * that — no fallback loadout is invented (`AGENTS.md` §7).
   *
   * `run` is the async run `create()` (or `RETRY`) started this read for. The
   * read is asynchronous and the scene can be shut down or reused while it is in
   * flight, so the delivery is written only while that run is still the current
   * one ({@link asyncRun}); a stale read renders nothing and leaves the state
   * the run that replaced it owns untouched.
   *
   * A failure is recoverable: it names the operation in
   * {@link failedOperation}, which is what puts a working `RETRY` on screen.
   */
  private async loadCollection(run: number): Promise<void> {
    const runtime = this.runtime;
    if (runtime === null) {
      return;
    }

    this.loading = true;
    this.startError = null;
    this.failedOperation = null;
    this.render();

    try {
      const [pets, cards, relics] = await Promise.all([
        runtime.getPets(),
        runtime.getCards(),
        runtime.getRelics(),
      ]);

      if (run !== this.asyncRun) {
        return;
      }

      this.ownedPets = pets;
      this.ownedCards = cards;
      this.ownedRelics = relics;
    } catch (error) {
      if (run !== this.asyncRun) {
        return;
      }

      // A failed read is presentation feedback, not game state: the selection
      // stays empty and nothing is substituted for the missing data.
      this.startError = `Collection load failed: ${describeError(error)}`;
      this.failedOperation = 'collection';
    } finally {
      if (run === this.asyncRun) {
        this.loading = false;
        this.render();
      }
    }
  }

  /**
   * The `RETRY` action behind a failed operation.
   *
   * It repeats the operation that actually failed — the collection read, or the
   * battle start — and nothing else. An activation while something is already
   * in flight is ignored, so a repeated `RETRY` cannot issue two requests or
   * two start attempts (`ARCHITECTURE.md` §2.2.3 rule 5).
   */
  private retryFailedOperation(): void {
    if (this.loading || this.startPending) {
      return;
    }

    if (this.failedOperation === 'collection') {
      void this.loadCollection(this.asyncRun);
      return;
    }

    if (this.failedOperation === 'start') {
      this.requestStart();
    }
  }

  /**
   * `< BACK` — the Lobby's exit to `MainMenuScene` (`GDD.md` §2.1,
   * `TDD.md` §2.1).
   *
   * It is available in every Lobby state, including while a read or a start is
   * in flight, so the screen is never a dead end. Leaving **invalidates the
   * current run's asynchronous work before the transition is requested**: a
   * battle start that settles afterwards compares the run it captured against
   * {@link asyncRun} and is discarded, so it can neither preserve a loadout nor
   * open a battle from a scene the player has left.
   *
   * It deliberately reaches `MainMenuScene` and nothing else: Collection and
   * Battle History stay reachable from the main menu, exactly as before this
   * scene existed (`D-202-01 = C`).
   */
  private goBack(): void {
    if (this.hasTransitioned) {
      return;
    }

    this.hasTransitioned = true;
    this.asyncRun += 1;

    this.scene.start('MainMenuScene');
  }

  // ---------------------------------------------------------------------------
  // Selection interaction
  // ---------------------------------------------------------------------------

  /**
   * Applies the preserved loadout when — and only when — this instance was
   * entered through the approved `ResultScene` → `PLAY AGAIN` continuation
   * (`D-202-03 = D`, `ARCHITECTURE.md` §2.2.3, ADR-022).
   *
   * The restored ids become this scene's own selection, so they are presented
   * through the existing Lobby UI and remain fully editable: the player may
   * deselect or replace any of them, and `buildStartRequest` carries whatever
   * the selection is at submit time. Nothing is validated here — restoring is a
   * starting point, not a legality decision, and the server validates the
   * submitted request (rule 5).
   *
   * The ids are restored verbatim, including an id the collection read does not
   * return: filtering them would be a client-side ownership/legality decision
   * the client must not make. With no preserved loadout — or on any entry that
   * did not ask for one — the scene keeps its empty selection, so a normal
   * entry behaves exactly as before.
   */
  private applyPreservedLoadout(): void {
    if (!this.restorePreservedLoadout) {
      return;
    }

    const preserved = readPreservedLoadout(this);

    if (preserved === null) {
      return;
    }

    this.selectedPetId = preserved.petId;
    this.selectedCardIds = [...preserved.cardLoadout];
    this.selectedRelicIds = [...preserved.relicLoadout];
    this.selectedBossId = preserved.bossId;
  }

  /**
   * Chooses the active Pet (`PET_RULES.md` §2 — exactly one per battle).
   *
   * Selecting another owned Pet replaces the current choice; selecting the
   * current one clears it. The submitted value is the collection entry's
   * `petId`, which `API_CONTRACTS.md` §5.1 defines as the owned instance.
   */
  private selectPet(petId: string): void {
    this.selectionMessage = '';
    this.selectedPetId = this.selectedPetId === petId ? null : petId;
    this.render();
  }

  /**
   * Toggles one Basic Card into the loadout (`CARD_RULES.md` §1 — exactly three
   * Basic Cards).
   *
   * Only the `cardId` is selected, and it is taken from the collection read. The
   * three slots are an interaction limit, not a validity decision
   * (`ARCHITECTURE.md` §2.2.3 rule 5).
   */
  private toggleCard(cardId: string): void {
    const index = this.selectedCardIds.indexOf(cardId);

    if (index >= 0) {
      this.selectedCardIds.splice(index, 1);
      this.selectionMessage = '';
      this.render();
      return;
    }

    if (this.selectedCardIds.length >= CARD_LOADOUT_SIZE) {
      this.selectionMessage = `Exactly ${CARD_LOADOUT_SIZE} Basic Cards: deselect one first.`;
      this.render();
      return;
    }

    this.selectedCardIds.push(cardId);
    this.selectionMessage = '';
    this.render();
  }

  /**
   * Toggles one owned Relic **instance** into the loadout
   * (`RELIC_RULES.md` §2.1 — 3–5 owned instances, each at most once).
   *
   * Selection order is preserved: the array reaches the request in the order the
   * player picked, because that order **is** the equip slot order
   * (`RELIC_RULES.md` §2.3).
   */
  private toggleRelic(relicId: string): void {
    const index = this.selectedRelicIds.indexOf(relicId);

    if (index >= 0) {
      this.selectedRelicIds.splice(index, 1);
      this.selectionMessage = '';
      this.render();
      return;
    }

    if (this.selectedRelicIds.length >= MAX_RELIC_LOADOUT_SIZE) {
      this.selectionMessage = `At most ${MAX_RELIC_LOADOUT_SIZE} Relics: deselect one first.`;
      this.render();
      return;
    }

    this.selectedRelicIds.push(relicId);
    this.selectionMessage = '';
    this.render();
  }

  /**
   * How many pages the owned Relic column occupies.
   *
   * It is a function of the read alone — `Math.ceil(owned / page size)`, floored
   * at one so an empty or partial column still has a page — and it is never a
   * content decision: the scene holds no expectation of how many Relics a Player
   * owns (`AGENTS.md` §7, §10).
   */
  private relicPageCount(): number {
    return Math.max(1, Math.ceil(this.ownedRelics.length / MAX_LIST_ROWS));
  }

  /**
   * Moves the Relic column by one page and re-renders.
   *
   * The target is clamped, so activating `PREV` on the first page or `NEXT` on
   * the last is a no-op rather than a wrap-around — a wrap would present an
   * instance the player is already looking past as if it were on the far side.
   *
   * Paging changes **what is shown**, never **what is selected**: the selection
   * lives in {@link selectedRelicIds} and is untouched here, so a selected Relic
   * stays selected when its page scrolls out of view and keeps its equip slot
   * (`RELIC_RULES.md` §2.3 — slot order is selection order, not display order).
   */
  private changeRelicPage(delta: number): void {
    const target = Math.min(
      Math.max(this.relicPage + delta, 0),
      this.relicPageCount() - 1
    );

    if (target === this.relicPage) {
      return;
    }

    this.relicPage = target;
    this.selectionMessage = '';
    this.render();
  }

  /**
   * Chooses the Boss the battle is fought against (`BOSS_RULES.md` §1 — a battle
   * has exactly one Boss) from the five canonical MVP Bosses.
   *
   * Selecting another Boss replaces the current choice; selecting the current
   * one clears it — the same single-selection behaviour `selectPet` has. The
   * stored value is the catalog entry's canonical technical Identity (§6.4), and
   * it is taken from the catalog rather than derived from the display name, so
   * `boss-son-thach-ve` and `boss-kim-loi-vuong` are submitted with the exact
   * spellings §6.4 fixes.
   *
   * The scene decides no Boss legality here: it does not check that the choice
   * is owned, unlocked, or otherwise fightable — the server resolves the
   * submitted identity and answers `400 BOSS_NOT_FOUND` for an unknown one,
   * through the same `startError` path as any other rejection
   * (`API_CONTRACTS.md` §3, `ARCHITECTURE.md` §2.2.3 rule 5).
   */
  private selectBoss(bossId: string): void {
    this.selectionMessage = '';
    this.selectedBossId = this.selectedBossId === bossId ? null : bossId;
    this.render();
  }

  /**
   * The Start Battle trigger.
   *
   * It requires the slot counts to be filled (1 / 3 / 3–5) and a Boss to be
   * chosen before it submits, because an incomplete selection has no documented
   * request shape at all — `cardLoadout` is exactly three elements,
   * `relicLoadout` is three to five, and `bossId` names the Boss the battle is
   * created against (`API_CONTRACTS.md` §3), which this scene does not default.
   * That check is about *completeness*, not legality:
   * the scene never decides whether the chosen items are owned, distinct, or
   * within a copy limit — the server does (§3, `ARCHITECTURE.md` §2.2.3 rule 5).
   *
   * It is inert while a collection read is in flight or a start attempt is
   * outstanding, and after this run has already claimed its transition. The
   * first condition is what keeps the presented "disabled" state real: on the
   * `PLAY AGAIN` entry the preserved loadout is applied before the read
   * finishes, so a click during the read could otherwise submit a request built
   * from a view that is still being populated.
   */
  private requestStart(): void {
    // The in-flight guards: repeated activation while a read or a request is
    // outstanding results in exactly one `startBattle` call. Nothing is queued.
    if (this.startPending || this.loading || this.hasTransitioned) {
      return;
    }

    if (this.runtime === null) {
      this.startError = 'No runtime is available: the battle cannot be started.';
      this.render();
      return;
    }

    const incomplete = this.describeIncompleteSelection();
    if (incomplete !== null) {
      this.selectionMessage = incomplete;
      this.render();
      return;
    }

    this.selectionMessage = '';
    void this.startBattle(this.asyncRun);
  }

  /**
   * Submits the selection through the runtime port and routes on the result.
   *
   * On success the scene transitions to `BattleScene`. On rejection it stays
   * active, shows the error, and keeps the selection intact so the player can
   * correct and retry — the in-flight guard is released, which is what makes
   * `RETRY` submit again (`ARCHITECTURE.md` §2.2.3 rule 5).
   *
   * A successful start also **preserves the submitted loadout** (`D-202-03 = D`,
   * ADR-022): the request the server accepted is the loadout the battle is
   * fought with, so it is what `ResultScene` → `PLAY AGAIN` must restore. A
   * rejected start creates no battle and therefore preserves nothing.
   *
   * `run` is the async run that issued this attempt. A settlement belonging to a
   * run the scene has already left — `< BACK` was pressed, or the instance was
   * reused — is discarded before it can preserve anything or navigate.
   *
   * The scene does not duplicate the runtime's REST → connect → join
   * orchestration, and it computes no gameplay value: it hands over a request
   * and reports the outcome (`TASK-077`).
   */
  private async startBattle(run: number): Promise<void> {
    const runtime = this.runtime;
    if (runtime === null) {
      return;
    }

    this.startPending = true;
    this.startError = null;
    this.failedOperation = null;
    this.render();

    // Exactly the request that is submitted, kept so the preserved loadout is
    // the battle's own loadout rather than a second, later reading of the
    // scene's selection.
    const request = this.buildStartRequest();

    try {
      await runtime.startBattle(request);
    } catch (error) {
      if (run !== this.asyncRun) {
        return;
      }

      // The documented rejections (401 UNAUTHENTICATED, 400 INVALID_LOADOUT /
      // PET_NOT_OWNED / BOSS_NOT_FOUND) and transport failures all arrive here.
      // No battle exists after any of them, so the scene stays put, nothing is
      // preserved, and the operation is named so RETRY can repeat it. The
      // message is the transport's own player-facing one (`API_CONTRACTS.md`
      // §6's `message` when the server sent an envelope) — never a status code.
      this.startError = `Battle start failed: ${describeError(error)}`;
      this.failedOperation = 'start';
      this.startPending = false;
      this.render();
      return;
    }

    if (run !== this.asyncRun) {
      // The Lobby was left (or reused) while the start was in flight: the battle
      // the server created is not this presentation's to enter, so no loadout is
      // preserved and no scene is started.
      return;
    }

    this.startPending = false;
    this.enterBattle(request);
  }

  /**
   * Claims this run's transition and enters the battle the request started.
   *
   * The claim is taken *before* the loadout is preserved and the scene is
   * started, so a repeated success path cannot preserve twice or start a second
   * `BattleScene` (`MainMenuScene.transitionTo`'s documented shape).
   */
  private enterBattle(request: BattleStartRequest): void {
    if (this.hasTransitioned) {
      return;
    }

    this.hasTransitioned = true;
    preserveLoadout(this, request);
    this.scene.start('BattleScene');
  }

  /**
   * Builds the `BattleStartRequest` (`API_CONTRACTS.md` §3) from the
   * in-progress selection — exactly its four documented members and nothing
   * else.
   *
   * ```text
   * petId         the selected owned Pet instance id from the collection read
   * bossId        the selected Boss's canonical technical Identity
   *               (BOSS_RULES.md §6.4), taken from the scene's own selection
   * cardLoadout   the three selected Basic Card ids, as chosen
   * relicLoadout  the three-to-five selected owned Relic instance ids, in the
   *               order chosen — position i is equip slot i + 1 (§2.3)
   * ```
   *
   * The Boss member is the **player's choice**: no Boss literal, no default, and
   * no fallback is substituted for it, so whichever of the five the player
   * selected is exactly what the server resolves.
   *
   * No `playerId` (the caller is resolved server-side from the session,
   * §2.8), no Signature/PetSkill card (derived by the server, §3), and no
   * `battleId`, `turn`, `sequence`, HP, Power, or any other server-owned value
   * (`GAME_RULES.md` §18, ADR-001).
   *
   * The arrays are copies of the scene's own selection, sent unsorted and
   * un-deduplicated: transporting the selection is the client's whole part in
   * the loadout contract (`RELIC_RULES.md` §2.1 item 3).
   */
  private buildStartRequest(): BattleStartRequest {
    return {
      // Slot completeness is enforced by `requestStart` before this runs, so the
      // selected ids are present here. No fallback value is substituted.
      petId: this.selectedPetId as string,
      bossId: this.selectedBossId as string,
      cardLoadout: [...this.selectedCardIds],
      relicLoadout: [...this.selectedRelicIds],
    };
  }

  /** The message naming an unfilled slot requirement, or `null` when complete. */
  private describeIncompleteSelection(): string | null {
    if (this.selectedPetId === null) {
      return 'Choose a Pet first.';
    }
    if (this.selectedCardIds.length !== CARD_LOADOUT_SIZE) {
      return `Choose exactly ${CARD_LOADOUT_SIZE} Basic Cards.`;
    }
    if (this.selectedRelicIds.length < MIN_RELIC_LOADOUT_SIZE) {
      return `Choose at least ${MIN_RELIC_LOADOUT_SIZE} Relics.`;
    }
    // There is no default Boss, so the choice is a required slot like the others
    // (step 4 of the flow). Choosing *which* Boss is the player's to make; the
    // server remains the one that decides whether the identity is a real Boss.
    if (this.selectedBossId === null) {
      return 'Choose a Boss.';
    }
    return null;
  }

  // ---------------------------------------------------------------------------
  // Presentation
  // ---------------------------------------------------------------------------

  /** Draws the static frame and creates the text objects the render pass fills. */
  private drawShell(): void {
    this.add
      .rectangle(
        SAFE_AREA.x + SAFE_AREA.width / 2,
        SAFE_AREA.y + SAFE_AREA.height / 2,
        SAFE_AREA.width,
        SAFE_AREA.height,
        0x0f172a
      )
      .setStrokeStyle(2, 0x334155);

    const title = (x: number, y: number, text: string) =>
      this.add.text(x, y, text, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '18px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      });

    const small = (x: number, y: number, color: string) =>
      this.add.text(x, y, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '14px',
        color,
      });

    this.headerText = this.add
      .text(SAFE_AREA.x + 26, SAFE_AREA.y + 18, '', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '22px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0, 0);

    // Step 1 — Choose Pet, step 2 — Equip Cards, step 3 — Equip Relics
    // (GDD.md §2).
    this.petsHeaderText = title(COLUMN_ORIGIN_X, SAFE_AREA.y + 58, '');
    this.cardsHeaderText = title(
      COLUMN_ORIGIN_X + COLUMN_PITCH,
      SAFE_AREA.y + 58,
      ''
    );
    this.relicsHeaderText = title(
      COLUMN_ORIGIN_X + COLUMN_PITCH * 2,
      SAFE_AREA.y + 58,
      ''
    );

    // Step 4 — Choose Boss (GDD.md §2; BOSS_RULES.md §6). Its own band below the
    // three collection columns: those read server-owned collections, this is the
    // five canonical MVP Bosses the scene holds itself.
    this.bossHeaderText = title(COLUMN_ORIGIN_X, BOSS_HEADER_TOP, '');

    // Slot counters — the selection affordance.
    this.selectionText = small(
      SAFE_AREA.x + 26,
      SAFE_AREA.y + SAFE_AREA.height - SELECTION_TEXT_BOTTOM_OFFSET,
      '#93c5fd'
    );

    // Step 5 — loadout Review. Presentational only: it restates the selection
    // that would be submitted, and asserts nothing about its validity.
    this.reviewText = small(
      SAFE_AREA.x + 26,
      SAFE_AREA.y + SAFE_AREA.height - REVIEW_TEXT_BOTTOM_OFFSET,
      '#94a3b8'
    );

    this.messageText = small(
      SAFE_AREA.x + 26,
      SAFE_AREA.y + SAFE_AREA.height - MESSAGE_TEXT_BOTTOM_OFFSET,
      '#fbbf24'
    );
    this.messageText.setWordWrapWidth(SAFE_AREA.width - 100);

    this.errorText = small(
      SAFE_AREA.x + 26,
      SAFE_AREA.y + SAFE_AREA.height - ERROR_TEXT_BOTTOM_OFFSET,
      '#f87171'
    );
    this.errorText.setWordWrapWidth(SAFE_AREA.width - 100);
  }

  /**
   * Renders the current scene state.
   *
   * Every render rebuilds the collection rows from the loaded responses and
   * re-creates their hit areas, so the presented content is always exactly what
   * the collection read returned — a re-read never leaves stale entries behind.
   */
  private render(): void {
    this.clearDynamicObjects();

    this.headerText?.setText('LOBBY — BATTLE PREPARATION');
    this.petsHeaderText?.setText('1. CHOOSE PET');
    this.cardsHeaderText?.setText('2. EQUIP CARDS');
    this.relicsHeaderText?.setText('3. EQUIP RELICS');
    this.bossHeaderText?.setText('4. CHOOSE BOSS');

    this.renderPets();
    this.renderCards();
    this.renderRelics();
    this.drawRelicPager();
    this.renderBosses();

    this.selectionText?.setText(
      [
        `Pet:   ${this.selectedPetId ?? '—'}`,
        `Cards: ${this.selectedCardIds.length}/${CARD_LOADOUT_SIZE}`,
        `Relics: ${this.selectedRelicIds.length}/${MAX_RELIC_LOADOUT_SIZE}` +
          ` (min ${MIN_RELIC_LOADOUT_SIZE})`,
      ].join('\n')
    );

    // Step 5 — Review. The Boss line states the selected Boss — `(none chosen)`
    // until the player picks one, since no Boss is defaulted — and the loadout
    // lines restate the selection. This is a summary of a request the player is
    // about to submit, not a validation of it.
    this.reviewText?.setText(
      [
        `5. REVIEW — Boss: ${this.selectedBossId ?? '(none chosen)'}`,
        `Pet:   ${this.selectedPetId ?? '(none chosen)'}`,
        `Cards: ${this.selectedCardIds.length > 0 ? this.selectedCardIds.join(', ') : '(none chosen)'}`,
        `Relics: ${this.selectedRelicIds.length > 0 ? this.selectedRelicIds.join(', ') : '(none chosen)'}`,
      ].join('\n')
    );

    this.messageText?.setText(this.describeStartTriggerMessage());
    this.errorText?.setText(this.startError ?? '');

    // The exit is drawn on every pass, so it is usable in every state — while
    // the collection is loading, after a failure, and when the loadout is ready.
    this.drawBackControl();
    this.drawStartTrigger();

    // `RETRY` is offered only for a failure that has an operation behind it to
    // repeat: a rejected start or a failed read. A failure with nothing to
    // repeat (no runtime was published) reports itself and offers no control.
    if (this.startError !== null && this.failedOperation !== null) {
      this.drawRetryControl();
    }
  }

  /**
   * The Start Battle trigger's own status line — the step-4 affordance and its
   * progress. It reports transport/presentation state only: no match, damage,
   * boss, or any other gameplay value exists here (`GAME_RULES.md` §18).
   *
   * On the error path it keeps saying what the next attempt needs instead of
   * going blank: the error line above it carries *what happened*, and this line
   * carries *what to do*, so neither has to do both jobs.
   */
  private describeStartTriggerMessage(): string {
    if (this.loading) {
      return 'Loading the collection…';
    }
    if (this.startPending) {
      return 'START BATTLE — request in flight…';
    }
    if (this.selectionMessage !== '') {
      return this.selectionMessage;
    }
    if (this.describeIncompleteSelection() !== null) {
      return 'START BATTLE — fill the selection first.';
    }
    if (this.startError !== null) {
      return 'START BATTLE — ready to retry.';
    }
    return 'START BATTLE — ready to submit.';
  }

  /**
   * Draws the `< BACK` control that returns to `MainMenuScene`.
   *
   * It is a real control in every Lobby state, matching the convention the
   * read-only viewer and history scenes already use (a filled rectangle with
   * its caption centred on it). It is re-created by each render pass, so a
   * re-render destroys the previous pass's object and its pointer handler with
   * it — repeated renders and a reused scene instance cannot accumulate
   * listeners.
   */
  private drawBackControl(): void {
    this.drawControl(LOBBY_BACK_BUTTON, '< BACK', () => this.goBack());
  }

  /** Draws the `RETRY` control behind a failed, repeatable operation. */
  private drawRetryControl(): void {
    this.drawControl(LOBBY_RETRY_BUTTON, 'RETRY', () => this.retryFailedOperation());
  }

  /**
   * Draws one outbound control and registers its hit area.
   *
   * It follows this scene's existing visual and interaction convention (fill,
   * stroke, centred bold label, `setInteractive` + `GAMEOBJECT_POINTER_DOWN`)
   * rather than introducing a button abstraction (`ARCHITECTURE.md` §5,
   * `AGENTS.md` §9).
   */
  private drawControl(
    layout: { readonly x: number; readonly y: number; readonly width: number; readonly height: number },
    label: string,
    onActivate: () => void
  ): void {
    const button = this.add
      .rectangle(layout.x, layout.y, layout.width, layout.height, 0x1d4ed8)
      .setStrokeStyle(1, 0x60a5fa);

    const caption = this.add
      .text(layout.x, layout.y, label, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '16px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    button.setInteractive({ useHandCursor: true });
    button.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, () => onActivate());

    this.renderedTexts.push(caption);
    this.interactiveObjects.push(button);
  }

  /** Draws the interactive Start Battle trigger and registers its hit area. */
  private drawStartTrigger(): void {
    const width = 300;
    const height = 44;
    const x = GAME_WIDTH - SAFE_AREA.x - 26 - width / 2;
    const y = SAFE_AREA.y + SAFE_AREA.height - 96;

    const enabled = !this.startPending && !this.loading;

    const button = this.add
      .rectangle(x, y, width, height, enabled ? 0x1d4ed8 : 0x334155)
      .setStrokeStyle(1, enabled ? 0x60a5fa : 0x475569);

    const label = this.add
      .text(x, y, 'START BATTLE', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '18px',
        color: enabled ? '#e2e8f0' : '#94a3b8',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    button.setInteractive({ useHandCursor: enabled });
    button.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, () => this.requestStart());

    this.renderedTexts.push(label);
    // The button is interactive; the render pass owns its teardown, so the
    // shutdown path can detach the handler.
    this.interactiveObjects.push(button);
  }

  /** One Pet row per owned Pet returned by the read; none is invented. */
  private renderPets(): void {
    if (this.loading) {
      this.renderNote(COLUMN_ORIGIN_X, LIST_TOP, 'Loading pets…');
      return;
    }
    if (this.ownedPets.length === 0) {
      // Empty collection: the UI shows exactly that. No starter Pet is assumed.
      this.renderNote(COLUMN_ORIGIN_X, LIST_TOP, 'No owned Pets returned.');
      return;
    }

    this.ownedPets.slice(0, MAX_LIST_ROWS).forEach((pet, index) => {
      const selected = this.selectedPetId === pet.petId;

      this.renderOption(
        COLUMN_ORIGIN_X,
        LIST_TOP + index * ROW_HEIGHT,
        `${selected ? '●' : '○'} ${pet.identity}  ${pet.element}  Lv ${pet.level}`,
        selected,
        () => this.selectPet(pet.petId)
      );
    });
  }

  /** One Card row per unlocked Card returned by the read. */
  private renderCards(): void {
    if (this.loading) {
      this.renderNote(COLUMN_ORIGIN_X + COLUMN_PITCH, LIST_TOP, 'Loading cards…');
      return;
    }
    if (this.ownedCards.length === 0) {
      this.renderNote(COLUMN_ORIGIN_X + COLUMN_PITCH, LIST_TOP, 'No unlocked Cards returned.');
      return;
    }

    this.ownedCards.slice(0, MAX_LIST_ROWS).forEach((card, index) => {
      const slot = this.selectedCardIds.indexOf(card.cardId);

      this.renderOption(
        COLUMN_ORIGIN_X + COLUMN_PITCH,
        LIST_TOP + index * ROW_HEIGHT,
        `${slot >= 0 ? '●' : '○'} ${card.name}  [${card.category}]` +
          (slot >= 0 ? `  slot ${slot + 1}` : ''),
        slot >= 0,
        () => this.toggleCard(card.cardId),
        CATEGORY_COLORS[card.category],
        // What the Card changes (API_CONTRACTS.md §5.3's effectDefinition),
        // rendered from the response the read returned. Nothing is priced,
        // computed, or looked up per Card id.
        formatCardSummary(card),
        CARD_CONTENT_WIDTH
      );
    });
  }

  /** One Relic row per owned Relic instance the current page shows. */
  private renderRelics(): void {
    if (this.loading) {
      this.renderNote(RELIC_COLUMN_X, LIST_TOP, 'Loading relics…');
      return;
    }
    if (this.ownedRelics.length === 0) {
      this.renderNote(RELIC_COLUMN_X, LIST_TOP, 'No owned Relics returned.');
      return;
    }

    // The column shows one page of owned instances at a time. The page is
    // clamped against the read, so a shorter collection — a re-read, or a read
    // that returned fewer instances — can never leave the player looking at an
    // empty page. Rows keep the column's own pitch, so a page of rows occupies
    // exactly the list block the other two columns use.
    const page = Math.min(this.relicPage, this.relicPageCount() - 1);

    this.ownedRelics
      .slice(page * MAX_LIST_ROWS, page * MAX_LIST_ROWS + MAX_LIST_ROWS)
      .forEach((relic, index) => {
        const slot = this.selectedRelicIds.indexOf(relic.relicId);

        this.renderOption(
          RELIC_COLUMN_X,
          LIST_TOP + index * ROW_HEIGHT,
          `${slot >= 0 ? '●' : '○'} ${relic.name}` +
            // Slot numbering is display of the documented selection order:
            // position i is equip slot i + 1 (RELIC_RULES.md §2.3).
            (slot >= 0 ? `  slot ${slot + 1}` : ''),
          slot >= 0,
          () => this.toggleRelic(relic.relicId),
          '#cbd5e1',
          // When the Relic reacts, what it waits for, and what it changes
          // (API_CONTRACTS.md §5.4's trigger/condition/effectDefinition),
          // rendered from the response the read returned.
          formatRelicSummary(relic),
          RELIC_CONTENT_WIDTH
        );
      });
  }

  /**
   * Draws the Relic column's pager, when — and only when — the column holds more
   * owned instances than one page shows.
   *
   * It is the "reach every owned instance" affordance TASK-213 §3.2 C-2 requires
   * once all ten MVP Relics are owned: without it the tenth and ninth Relic rows
   * would be unreachable, and the player could not inspect or select them. The
   * controls follow the scene's existing control convention (a filled,
   * interactive rectangle with its caption centred on it, re-created by each
   * render pass) rather than introducing a scrolling container, a wheel handler,
   * or a second interaction system (`ARCHITECTURE.md` §5, `AGENTS.md` §9).
   *
   * The controls decide no legality and page no selection: they only change which
   * slice of the owned column is presented (`describeIncompleteSelection` still
   * answers what the request needs).
   */
  private drawRelicPager(): void {
    const pageCount = this.relicPageCount();

    if (pageCount <= 1) {
      return;
    }

    const page = Math.min(this.relicPage, pageCount - 1);
    const first = page * MAX_LIST_ROWS + 1;
    const last = Math.min(this.ownedRelics.length, page * MAX_LIST_ROWS + MAX_LIST_ROWS);

    this.drawControl(LOBBY_RELIC_PREV_BUTTON, 'PREV', () => this.changeRelicPage(-1));
    this.drawControl(LOBBY_RELIC_NEXT_BUTTON, 'NEXT', () => this.changeRelicPage(1));

    const indicator = this.add
      .text(
        RELIC_PAGE_INDICATOR_RIGHT,
        RELIC_PAGE_CONTROL_Y,
        `${first}-${last} of ${this.ownedRelics.length}`,
        {
          fontFamily: 'ui-monospace, monospace',
          fontSize: '13px',
          color: '#94a3b8',
        }
      )
      .setOrigin(1, 0.5);

    this.renderedTexts.push(indicator);
  }

  /**
   * One Boss option per canonical MVP Boss, always all five
   * (`BOSS_RULES.md` §6 — the catalog is closed).
   *
   * Unlike the three collection lists there is no read to wait for and no empty
   * case: the options are this scene's own static content, so they render on the
   * first pass and survive a collection re-render unchanged. The selected Boss is
   * marked the same way the other single-choice list is (`selectPet`): a filled
   * marker and the selected tone.
   */
  private renderBosses(): void {
    MVP_BOSSES.forEach((boss, index) => {
      const selected = this.selectedBossId === boss.bossId;

      this.renderOption(
        COLUMN_ORIGIN_X,
        BOSS_LIST_TOP + index * ROW_HEIGHT,
        `${selected ? '●' : '○'} ${boss.displayName}  ${boss.element}`,
        selected,
        () => this.selectBoss(boss.bossId)
      );
    });
  }

  /**
   * Draws one clickable option row and registers its hit areas.
   *
   * A row has one line — its identity and selection state — and, when the
   * caller supplies one, a second line carrying that option's own shipped
   * content (`detail`). Both lines are the same hit area: activating either
   * runs `onSelect`, so the content line is part of the option rather than a
   * separate control, and a Row's selection behaviour is unchanged by the
   * second line.
   *
   * `detail` is empty for the Pet and Boss rows, whose contracts carry no such
   * member, and for a Card or Relic whose structured content produced no text —
   * in which case no second line is drawn at all rather than an empty one.
   */
  private renderOption(
    x: number,
    y: number,
    text: string,
    selected: boolean,
    onSelect: () => void,
    color: string = '#cbd5e1',
    detail: string = '',
    detailWidth: number = 0
  ): void {
    const label = this.add
      .text(x, y, text, {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '14px',
        color: selected ? '#fbbf24' : color,
      })
      .setOrigin(0, 0);

    label.setInteractive({ useHandCursor: true });
    label.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, onSelect);

    this.renderedTexts.push(label);
    this.interactiveObjects.push(label);

    if (detail === '') {
      return;
    }

    const content = this.add
      .text(x, y + ROW_CONTENT_OFFSET, detail, {
        fontFamily: 'ui-monospace, monospace',
        fontSize: ROW_CONTENT_FONT_SIZE,
        color: selected ? '#fbbf24' : ROW_CONTENT_COLOR,
      })
      .setOrigin(0, 0);

    if (detailWidth > 0) {
      content.setWordWrapWidth(detailWidth);
    }

    content.setInteractive({ useHandCursor: true });
    content.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, onSelect);

    this.renderedTexts.push(content);
    this.interactiveObjects.push(content);
  }

  /** Draws a non-interactive informational row. */
  private renderNote(x: number, y: number, text: string): void {
    this.renderedTexts.push(
      this.add
        .text(x, y, text, {
          fontFamily: 'ui-monospace, monospace',
          fontSize: '14px',
          color: '#64748b',
        })
        .setOrigin(0, 0)
    );
  }

  /** Destroys the previous render pass's dynamic objects and hit areas. */
  private clearDynamicObjects(): void {
    for (const object of this.interactiveObjects) {
      object.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);
      object.destroy();
    }
    this.interactiveObjects = [];

    for (const text of this.renderedTexts) {
      text.destroy();
    }
    this.renderedTexts = [];
  }
}

/**
 * Presentation-safe rendering of a failed operation's own message.
 *
 * The shared transport (`ApiService`) has already reduced a rejected response to
 * the server's `API_CONTRACTS.md` §6 `message` — its human-readable detail —
 * and the runtime's own failures carry their own descriptions, so an `Error`'s
 * message is what there is to say. What is deliberately never rendered is
 * anything that is not a message: a rejecting non-`Error` is reported by its
 * effect, so no raw payload, stack, status line, or host object's string form
 * can reach the screen through this line.
 */
function describeError(error: unknown): string {
  return error instanceof Error && error.message !== '' ? error.message : 'Unknown error.';
}
