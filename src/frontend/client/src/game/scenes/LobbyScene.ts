import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import { preserveLoadout, readPreservedLoadout } from '../state/PreservedLoadout';
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
const MAX_LIST_ROWS = 8;

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
   * queued.
   */
  private startPending = false;
  /** A selection-driven message (e.g. "select a Pet first") shown to the player. */
  private selectionMessage = '';
  /** The error feedback from the last rejected start, or `null`. */
  private startError: string | null = null;

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

    // Restore before the first render, so the preserved selection is what the
    // player sees immediately — including while the collection read is still in
    // flight. Only the approved PLAY AGAIN entry reads the carrier.
    this.applyPreservedLoadout();

    this.drawShell();
    this.render();

    if (this.runtime === null) {
      // No runtime: the flow cannot read a collection or start a battle. The
      // scene stays alive and says so rather than throwing (task §4).
      this.startError = 'No runtime is available: cannot load the collection or start a battle.';
      this.render();
      return;
    }

    void this.loadCollection();
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

    this.ownedPets = [];
    this.ownedCards = [];
    this.ownedRelics = [];

    this.loading = false;
    this.startPending = false;
    this.selectionMessage = '';
    this.startError = null;

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
   */
  private async loadCollection(): Promise<void> {
    const runtime = this.runtime;
    if (runtime === null) {
      return;
    }

    this.loading = true;
    this.render();

    try {
      const [pets, cards, relics] = await Promise.all([
        runtime.getPets(),
        runtime.getCards(),
        runtime.getRelics(),
      ]);

      this.ownedPets = pets;
      this.ownedCards = cards;
      this.ownedRelics = relics;
    } catch (error) {
      // A failed read is presentation feedback, not game state: the selection
      // stays empty and nothing is substituted for the missing data.
      this.startError = `Collection load failed: ${describeError(error)}`;
    } finally {
      this.loading = false;
      this.render();
    }
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
   */
  private requestStart(): void {
    // The in-flight guard: repeated activation while a request is outstanding
    // results in exactly one `startBattle` call. Nothing is queued.
    if (this.startPending) {
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
    void this.startBattle();
  }

  /**
   * Submits the selection through the runtime port and routes on the result.
   *
   * On success the scene transitions to `BattleScene`. On rejection it stays
   * active, shows the error, and keeps the selection intact so the player can
   * correct and retry (`ARCHITECTURE.md` §2.2.3 rule 5).
   *
   * A successful start also **preserves the submitted loadout** (`D-202-03 = D`,
   * ADR-022): the request the server accepted is the loadout the battle is
   * fought with, so it is what `ResultScene` → `PLAY AGAIN` must restore. A
   * rejected start creates no battle and therefore preserves nothing.
   *
   * The scene does not duplicate the runtime's REST → connect → join
   * orchestration, and it computes no gameplay value: it hands over a request
   * and reports the outcome (`TASK-077`).
   */
  private async startBattle(): Promise<void> {
    const runtime = this.runtime;
    if (runtime === null) {
      return;
    }

    this.startPending = true;
    this.startError = null;
    this.render();

    // Exactly the request that is submitted, kept so the preserved loadout is
    // the battle's own loadout rather than a second, later reading of the
    // scene's selection.
    const request = this.buildStartRequest();

    try {
      await runtime.startBattle(request);
    } catch (error) {
      // The documented rejections (401 UNAUTHENTICATED, 400 INVALID_LOADOUT /
      // PET_NOT_OWNED / BOSS_NOT_FOUND) and transport failures all arrive here.
      // No battle exists after any of them, so the scene stays put and nothing
      // is preserved.
      this.startError = `Battle start failed: ${describeError(error)}`;
      this.startPending = false;
      this.render();
      return;
    }

    this.startPending = false;
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

    this.drawStartTrigger();
  }

  /**
   * The Start Battle trigger's own status line — the step-4 affordance and its
   * progress. It reports transport/presentation state only: no match, damage,
   * boss, or any other gameplay value exists here (`GAME_RULES.md` §18).
   */
  private describeStartTriggerMessage(): string {
    if (this.loading) {
      return 'Loading the collection…';
    }
    if (this.startError !== null) {
      return this.selectionMessage;
    }
    if (this.selectionMessage !== '') {
      return this.selectionMessage;
    }
    if (this.startPending) {
      return 'START BATTLE — request in flight…';
    }
    return this.describeIncompleteSelection() === null
      ? 'START BATTLE — ready to submit.'
      : 'START BATTLE — fill the selection first.';
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
        CATEGORY_COLORS[card.category]
      );
    });
  }

  /** One Relic row per owned Relic instance returned by the read. */
  private renderRelics(): void {
    if (this.loading) {
      this.renderNote(COLUMN_ORIGIN_X + COLUMN_PITCH * 2, LIST_TOP, 'Loading relics…');
      return;
    }
    if (this.ownedRelics.length === 0) {
      this.renderNote(COLUMN_ORIGIN_X + COLUMN_PITCH * 2, LIST_TOP, 'No owned Relics returned.');
      return;
    }

    this.ownedRelics.slice(0, MAX_LIST_ROWS).forEach((relic, index) => {
      const slot = this.selectedRelicIds.indexOf(relic.relicId);

      this.renderOption(
        COLUMN_ORIGIN_X + COLUMN_PITCH * 2,
        LIST_TOP + index * ROW_HEIGHT,
        `${slot >= 0 ? '●' : '○'} ${relic.name}` +
          // Slot numbering is display of the documented selection order:
          // position i is equip slot i + 1 (RELIC_RULES.md §2.3).
          (slot >= 0 ? `  slot ${slot + 1}` : ''),
        slot >= 0,
        () => this.toggleRelic(relic.relicId)
      );
    });
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

  /** Draws one clickable option row and registers its hit area. */
  private renderOption(
    x: number,
    y: number,
    text: string,
    selected: boolean,
    onSelect: () => void,
    color: string = '#cbd5e1'
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

/** Presentation-safe rendering of a rejection's own message. */
function describeError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
