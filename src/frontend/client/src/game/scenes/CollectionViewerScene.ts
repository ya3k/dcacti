import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import type { GameRuntimePort } from '../runtime/GameRuntimeEvents';
import type { CardResponse, PetResponse, RelicResponse } from '../../services/api/CollectionModels';

/**
 * The three collection categories the viewer presents.
 *
 * These are the client's own presentation labels for the three existing
 * collection read endpoints (`API_CONTRACTS.md` §5.1, §5.3, §5.4). They are
 * deliberately **not** any wire value: no §5 response carries a category member
 * for a Pet or a Relic, and a Card's wire `category` (`"Basic" | "PetSkill"`) is
 * a different concept that is rendered as-is rather than being folded in here.
 */
export type CollectionTab = 'pets' | 'cards' | 'relics';

/**
 * The tab bar, in presentation order, with the read each tab presents.
 *
 * ```text
 * PETS    GET /api/pets    owned Pet instances      (§5.1)
 * CARDS   GET /api/cards   unlocked Card definitions (§5.3)
 * RELICS  GET /api/relics  owned Relic instances     (§5.4)
 * ```
 */
export const COLLECTION_TABS: readonly { readonly key: CollectionTab; readonly label: string }[] = [
  { key: 'pets', label: 'PETS' },
  { key: 'cards', label: 'CARDS' },
  { key: 'relics', label: 'RELICS' },
];

/** The background fill inside the safe area (Slate 900). */
const BACKGROUND_COLOR = 0x0f172a;
/** The safe area frame (Slate 700). */
const FRAME_COLOR = 0x334155;
/** The active tab / primary action fill (Blue 700) and its border (Blue 400). */
const ACTIVE_FILL_COLOR = 0x1d4ed8;
const ACTIVE_BORDER_COLOR = 0x60a5fa;
/** The inactive tab / secondary action fill (Slate 800) and its border (Slate 600). */
const INACTIVE_FILL_COLOR = 0x1e293b;
const INACTIVE_BORDER_COLOR = 0x475569;
/** The detail panel fill (Slate 800) and its border (Slate 700). */
const PANEL_FILL_COLOR = 0x1e293b;
const PANEL_BORDER_COLOR = 0x334155;

/** Header/back/tab bar geometry, in logical game pixels. */
const HEADER_BOTTOM = SAFE_AREA.y + 56;
const TAB_TOP = SAFE_AREA.y + 70;
const TAB_HEIGHT = 40;
const TAB_WIDTH = 160;
const TAB_GAP = 16;
const CONTENT_TOP = SAFE_AREA.y + 130;
const CONTENT_BOTTOM = SAFE_AREA.y + SAFE_AREA.height - 40;
const CONTENT_HEIGHT = CONTENT_BOTTOM - CONTENT_TOP;
const PANEL_WIDTH = 480;

/** Item row geometry: one clickable row per item, at a fixed pitch. */
const ROW_HEIGHT = 34;
/** How many rows the list column can show without overflowing the content area. */
const MAX_LIST_ROWS = Math.floor(CONTENT_HEIGHT / ROW_HEIGHT);

/**
 * The Element wire value set of `API_CONTRACTS.md` §5.1, mapped to the tonal
 * presentation the Lobby and Battle scenes already use.
 *
 * Presentation only: the scene looks a Pet's `element` up in this table and
 * renders it verbatim. An unrecognised value falls back to the neutral tone
 * rather than being translated, guessed, or re-classified — the wire value is
 * never reinterpreted (`ELEMENT_RULES.md` §1 owns the vocabulary, and §5.1 owns
 * which spelling reaches the wire).
 */
const ELEMENT_COLORS: Readonly<Record<string, string>> = {
  Fire: '#ef4444',
  Water: '#3b82f6',
  Earth: '#d97706',
  Wood: '#22c55e',
  Metal: '#e2e8f0',
};

/** The neutral tone used for a value outside a documented presentation table. */
const NEUTRAL_VALUE_COLOR = '#94a3b8';

/**
 * The Card `category` wire values of `API_CONTRACTS.md` §5.3 (`CARD_RULES.md`
 * §1, `DATABASE.md` §3) mapped for display. As with {@link ELEMENT_COLORS}, an
 * unlisted value is shown in the neutral tone rather than being classified.
 */
const CARD_CATEGORY_COLORS: Readonly<Record<string, string>> = {
  Basic: '#93c5fd',
  PetSkill: '#c084fc',
};

/**
 * CollectionViewerScene — the read-only meta-progression collection viewer.
 *
 * It presents the authenticated Player's owned Pets, unlocked Cards, and owned
 * Relics, which are exactly the three collections the existing §5 read endpoints
 * return. It is a **viewer**: it owns no ownership rule, no progression, no
 * inventory mutation, no equip/loadout state, and no battle state.
 *
 * ```text
 * MainMenuScene
 *     │  COLLECTION
 *     ↓
 * CollectionViewerScene
 *     ├── PETS / CARDS / RELICS   (category tabs over one load)
 *     ├── item row                (one per returned entry)
 *     ├── detail panel             (the selected entry's own fields)
 *     └── < BACK ───────────────► MainMenuScene
 * ```
 *
 * **Every read goes through the runtime port.** The scene imports neither
 * `@microsoft/signalr`, nor `fetch`, nor `ApiService`: `services/api/` and
 * `services/realtime/` are transport layers, and a scene reaching either would
 * bypass the boundary (`ARCHITECTURE.md` §2.2.1 rule 1, §2.2.3 rule 3). It opens
 * no `BattleHub` connection and subscribes to no battle push — unlike
 * `BattleScene` it listens to nothing at all, because a collection read is a
 * request/response and §5 defines no push for it (`SIGNALR_PROTOCOL.md` §2, §4).
 *
 * **It renders the responses verbatim.** The three arrays are stored exactly as
 * received and every presented value is read out of them: nothing is sorted,
 * filtered, defaulted, counted up, or derived. In particular the scene computes
 * no level, tier, star, rarity, combat power, or progression — those are server
 * values that arrive in the response (`API_CONTRACTS.md` §5.1) and are printed
 * as sent (`GAME_RULES.md` §18, ADR-001, `AGENTS.md` §10).
 *
 * **All three reads happen once, together.** `create()` issues `getPets()`,
 * `getCards()`, and `getRelics()` in one `Promise.all` and holds the result for
 * the scene's lifetime, so switching tabs re-renders from what is already in
 * hand and issues no further request. There is no cache: the arrays are plain
 * scene fields, replaced wholesale by a reload and dropped by the scene's own
 * teardown, which `create()` attaches to Phaser's actual scene lifecycle events
 * (`Phaser.Scenes.Events.SHUTDOWN` / `DESTROY`) — the same ephemeral scene state
 * `LobbyScene` keeps its selection in
 * (`ARCHITECTURE.md` §2.2.3 rules 1–2, `AGENTS.md` §9).
 *
 * **A failed read is one error state, not a partial view.** §5 defines three
 * independent endpoints and no aggregate, so a load either produced all three
 * collections or it did not; `Promise.all` rejects on the first failure and the
 * scene says so and offers `RETRY` rather than showing two categories and an
 * unexplained empty third (`AGENTS.md` §7 — nothing is invented to fill a gap).
 *
 * It obtains the port with `readRuntime(this)`. When no runtime was supplied the
 * scene still runs and reports that the collection cannot be loaded, so scene
 * lifecycle never depends on runtime availability.
 */
export class CollectionViewerScene extends Phaser.Scene {
  private runtime: GameRuntimePort | null = null;

  // --- Presentation state -----------------------------------------------------

  /** The active category tab. `PETS` is the documented default. */
  private tab: CollectionTab = 'pets';
  /**
   * The selected entry's own identity — a Pet/Relic **instance** id or a Card
   * definition id (`API_CONTRACTS.md` §5.1, §5.3, §5.4) — or `null` when nothing
   * is selected. Cleared whenever the category changes, because an id is only
   * meaningful within the category it came from.
   */
  private selectedItemId: string | null = null;
  /** The three collection reads are in flight. */
  private loading = false;
  /** The failure feedback from the last rejected load, or `null`. */
  private loadError: string | null = null;

  // --- Loaded collection (mirrors exactly what the reads returned) ------------

  private ownedPets: PetResponse[] = [];
  private ownedCards: CardResponse[] = [];
  private ownedRelics: RelicResponse[] = [];

  // --- Scene-owned display objects -------------------------------------------
  //
  // The shell is built once in `drawShell()` and kept; the content area is
  // rebuilt on every render pass. Both lists are cleared and detached by the
  // teardown `create()` attaches to Phaser's scene lifecycle events, so a
  // shut-down viewer leaves no interactive object behind and a reopened viewer
  // starts from an empty pair of lists.

  /** The persistent shell text objects, created once by `drawShell()`. */
  private shellObjects: Phaser.GameObjects.GameObject[] = [];
  /** The persistent shell's text objects, so a re-render can update them. */
  private titleText: Phaser.GameObjects.Text | null = null;
  private statusText: Phaser.GameObjects.Text | null = null;
  private detailText: Phaser.GameObjects.Text | null = null;
  /** Rebuilt on every render pass; each entry is one interactive hit area. */
  private interactiveObjects: Phaser.GameObjects.GameObject[] = [];
  private renderedTexts: Phaser.GameObjects.Text[] = [];

  /**
   * Guards the Back navigation so it fires at most once per scene instance,
   * mirroring `MainMenuScene`'s own guard: a rapid double tap must not produce a
   * duplicate scene transition (`AGENTS.md` §16 — same pattern, not a new one).
   */
  private hasTransitioned = false;

  constructor() {
    super('CollectionViewerScene');
  }

  create(): void {
    this.runtime = readRuntime(this);

    // Attach this scene's teardown to Phaser's own scene lifecycle events,
    // before any of the objects it releases is created.
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
    // runs in the browser, so a shut-down viewer keeps the ended run's shell
    // objects and loaded collections in its own fields and a reopened viewer
    // accumulates them.
    //
    // The registration is idempotent — the pair is detached first, then attached
    // with `once` — so a scene that is shut down and later reopened holds
    // exactly one teardown handler per event instead of stacking another one on
    // every run (`once` alone would leave the run's unfired `DESTROY` handler
    // behind). This is the same "detach before attach" idempotency
    // `BattleScene.registerBoardInput` already uses, and `once` means the
    // handler is consumed by the event that fired it.
    this.events.off(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.off(Phaser.Scenes.Events.DESTROY, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.DESTROY, this.shutdown, this);

    // A reopened scene starts from a clean slate: Phaser reuses the instance, so
    // state left by a previous run must not survive into this one.
    this.resetViewerState();
    this.drawShell();
    this.render();

    if (this.runtime === null) {
      this.loadError = 'No runtime is available: the collection cannot be loaded.';
      this.render();
      return;
    }

    void this.loadCollections();
  }

  /**
   * Scene teardown — the handler subscribed to Phaser's own
   * `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY` events by `create()`.
   *
   * It is a plain method invoked *by the engine's event*, not an engine hook:
   * Phaser never calls a scene method named `shutdown` on its own, so before
   * this registration existed the body below never ran in the browser.
   *
   * Every object the scene created is destroyed and every interactive handler is
   * detached, so reopening the viewer cannot accumulate UI, listeners, or
   * callbacks. The loaded collections and the selection are dropped with it: a
   * shut-down viewer holds nothing, and the runtime keeps none on its behalf.
   *
   * Phaser's own `DisplayList#shutdown` handles the same event and destroys
   * every child before this handler runs, so the `off` / `destroy` calls below
   * are made against already-destroyed game objects; `GameObject#destroy` is a
   * documented no-op once `scene` is released, and the `off` calls are
   * harmless — what remains to release here is the scene's own references and
   * handler registrations. The body is idempotent and safe on both events.
   */
  shutdown(): void {
    for (const object of this.shellObjects) {
      object.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);
      object.destroy();
    }
    this.shellObjects = [];

    for (const object of this.interactiveObjects) {
      object.off(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN);
      object.destroy();
    }
    this.interactiveObjects = [];

    for (const text of this.renderedTexts) {
      text.destroy();
    }
    this.renderedTexts = [];

    this.titleText = null;
    this.statusText = null;
    this.detailText = null;

    this.resetViewerState();
  }

  /** Discards every value the scene derived from the reads. */
  private resetViewerState(): void {
    this.tab = 'pets';
    this.selectedItemId = null;
    this.loading = false;
    this.loadError = null;

    this.ownedPets = [];
    this.ownedCards = [];
    this.ownedRelics = [];

    this.hasTransitioned = false;
  }

  // ---------------------------------------------------------------------------
  // Collection read (through the runtime port)
  // ---------------------------------------------------------------------------

  /**
   * Reads all three collections through the runtime port.
   *
   * The three reads are issued together, because they are one load: the viewer
   * cannot present its tab bar meaningfully while part of it is unknown. On
   * rejection the error is shown with a real `RETRY`, and nothing is substituted
   * for the missing data.
   */
  private async loadCollections(): Promise<void> {
    const runtime = this.runtime;
    if (runtime === null) {
      return;
    }

    this.loading = true;
    this.loadError = null;
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
      this.loadError = `Collection load failed: ${describeError(error)}`;
    } finally {
      this.loading = false;
      this.render();
    }
  }

  // ---------------------------------------------------------------------------
  // Interaction
  // ---------------------------------------------------------------------------

  /**
   * Switches the active category.
   *
   * The list is re-rendered from the already-loaded arrays — no request is
   * issued, because the single load in `create()` retrieved all three
   * collections. The selection is cleared: an identity selected in one category
   * means nothing in another, and carrying it over would present a detail panel
   * for an entry the player is no longer looking at.
   */
  private selectTab(tab: CollectionTab): void {
    if (this.tab === tab) {
      return;
    }
    this.tab = tab;
    this.selectedItemId = null;
    this.render();
  }

  /**
   * Selects an entry for the detail panel, or clears it when the same entry is
   * tapped again — the toggling single-selection behaviour `LobbyScene` uses.
   *
   * Nothing about the entry is modified: this records an id and re-renders.
   */
  private selectItem(itemId: string): void {
    this.selectedItemId = this.selectedItemId === itemId ? null : itemId;
    this.render();
  }

  /** Re-issues the collection load — the `RETRY` action behind a failed load. */
  private retryLoad(): void {
    void this.loadCollections();
  }

  /**
   * Returns to `MainMenuScene` through the documented scene transition.
   *
   * Guarded so the transition fires at most once per scene instance.
   */
  private goBack(): void {
    if (this.hasTransitioned) {
      return;
    }
    this.hasTransitioned = true;
    this.scene.start('MainMenuScene');
  }

  // ---------------------------------------------------------------------------
  // Presentation
  // ---------------------------------------------------------------------------

  /**
   * Draws the persistent shell: background, header, and the detail panel frame.
   *
   * The frame is static; only the text drawn into the content area changes
   * between render passes.
   */
  private drawShell(): void {
    const background = this.add.rectangle(
      SAFE_AREA.x + SAFE_AREA.width / 2,
      SAFE_AREA.y + SAFE_AREA.height / 2,
      SAFE_AREA.width,
      SAFE_AREA.height,
      BACKGROUND_COLOR
    );
    background.setStrokeStyle(2, FRAME_COLOR);
    this.shellObjects.push(background);

    this.titleText = this.add
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 28, 'COLLECTION VIEWER', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '22px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);
    this.shellObjects.push(this.titleText);

    // The detail panel's frame. Its content is dynamic, so only the frame is
    // persistent.
    const panelX = SAFE_AREA.x + SAFE_AREA.width - 26 - PANEL_WIDTH / 2;
    const panel = this.add.rectangle(
      panelX,
      CONTENT_TOP + CONTENT_HEIGHT / 2,
      PANEL_WIDTH,
      CONTENT_HEIGHT,
      PANEL_FILL_COLOR
    );
    panel.setStrokeStyle(1, PANEL_BORDER_COLOR);
    this.shellObjects.push(panel);

    this.detailText = this.add
      .text(panelX - PANEL_WIDTH / 2 + 20, CONTENT_TOP + 20, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '15px',
        color: '#f8fafc',
      })
      .setOrigin(0, 0)
      .setWordWrapWidth(PANEL_WIDTH - 40);
    this.shellObjects.push(this.detailText);

    // The feedback line under the tab bar: loading state, empty notice, or error.
    this.statusText = this.add
      .text(SAFE_AREA.x + 26, HEADER_BOTTOM + 6, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '14px',
        color: '#94a3b8',
      })
      .setOrigin(0, 0)
      .setWordWrapWidth(SAFE_AREA.width - 52);
    this.shellObjects.push(this.statusText);
  }

  /**
   * Renders the current scene state.
   *
   * Every pass rebuilds the dynamic content area from the loaded arrays and
   * re-creates its hit areas, so what is presented is always exactly what the
   * last read returned — a reload never leaves stale rows or listeners behind.
   */
  private render(): void {
    this.clearDynamicObjects();

    this.drawBackButton();
    this.drawTabs();
    this.drawStatusLine();

    if (this.loadError === null && !this.loading) {
      this.drawItemList();
    }
    this.drawDetailPanel();
  }

  /** Draws the persistent `< BACK` button returning to `MainMenuScene`. */
  private drawBackButton(): void {
    const width = 140;
    const height = 36;
    const x = SAFE_AREA.x + 20 + width / 2;
    const y = SAFE_AREA.y + 20 + height / 2;

    const button = this.add
      .rectangle(x, y, width, height, INACTIVE_FILL_COLOR)
      .setStrokeStyle(1, INACTIVE_BORDER_COLOR);
    const label = this.add
      .text(x, y, '< BACK', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '16px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    button.setInteractive({ useHandCursor: true });
    button.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, () => this.goBack());

    this.renderedTexts.push(label);
    this.interactiveObjects.push(button);
  }

  /**
   * Draws the three category tabs with their counts.
   *
   * A count is the length of the array the read returned — it is measured, not
   * derived, and it is the collection's own size rather than any judgment about
   * it. The count is withheld while loading, because the arrays are not yet the
   * server's answer.
   */
  private drawTabs(): void {
    const startX = SAFE_AREA.x + 20;

    COLLECTION_TABS.forEach((entry, index) => {
      const active = this.tab === entry.key;
      const x = startX + index * (TAB_WIDTH + TAB_GAP) + TAB_WIDTH / 2;
      const y = TAB_TOP + TAB_HEIGHT / 2;

      const button = this.add
        .rectangle(
          x,
          y,
          TAB_WIDTH,
          TAB_HEIGHT,
          active ? ACTIVE_FILL_COLOR : INACTIVE_FILL_COLOR
        )
        .setStrokeStyle(1, active ? ACTIVE_BORDER_COLOR : INACTIVE_BORDER_COLOR);

      const label = this.add
        .text(x, y, this.tabLabel(entry), {
          fontFamily: 'system-ui, sans-serif',
          fontSize: '16px',
          color: active ? '#f8fafc' : '#94a3b8',
          fontStyle: active ? 'bold' : 'normal',
        })
        .setOrigin(0.5);

      button.setInteractive({ useHandCursor: true });
      button.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, () => this.selectTab(entry.key));

      this.renderedTexts.push(label);
      this.interactiveObjects.push(button);
    });
  }

  /** The tab's own label, with its measured count once the load has settled. */
  private tabLabel(entry: { readonly key: CollectionTab; readonly label: string }): string {
    if (this.loading || this.loadError !== null) {
      return entry.label;
    }
    return `${entry.label} (${this.itemsFor(entry.key).length})`;
  }

  /** The loaded entries of a category, exactly as the read returned them. */
  private itemsFor(tab: CollectionTab): readonly { readonly id: string }[] {
    switch (tab) {
      case 'pets':
        return this.ownedPets.map((pet) => ({ id: pet.petId }));
      case 'cards':
        return this.ownedCards.map((card) => ({ id: card.cardId }));
      case 'relics':
        return this.ownedRelics.map((relic) => ({ id: relic.relicId }));
    }
  }

  /**
   * Draws the feedback line under the header.
   *
   * It reports exactly one of the three documented states — the load is in
   * flight, the load failed, or the active category is empty — and otherwise
   * says nothing, so a populated category carries no notice at all.
   */
  private drawStatusLine(): void {
    if (this.loading) {
      this.statusText?.setText('Loading collection items…');
      this.statusText?.setColor('#93c5fd');
      return;
    }

    if (this.loadError !== null) {
      // The error banner. `RETRY` is drawn beside it as a real action.
      this.statusText?.setText(this.loadError);
      this.statusText?.setColor('#f87171');
      this.drawRetryButton();
      return;
    }

    const count = this.itemsFor(this.tab).length;
    if (count === 0) {
      this.statusText?.setText(EMPTY_MESSAGES[this.tab]);
      this.statusText?.setColor('#64748b');
      return;
    }

    this.statusText?.setText('');
  }

  /** Draws the `RETRY` action for a failed load. */
  private drawRetryButton(): void {
    const width = 120;
    const height = 32;
    const x = SAFE_AREA.x + 20 + width / 2;
    // Sits under the wrapped error banner, still inside the safe area: the
    // banner line starts at `HEADER_BOTTOM + 6` and the message occupies at most
    // two lines at this width.
    const y = HEADER_BOTTOM + 52;

    const button = this.add
      .rectangle(x, y, width, height, ACTIVE_FILL_COLOR)
      .setStrokeStyle(1, ACTIVE_BORDER_COLOR);
    const label = this.add
      .text(x, y, 'RETRY', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '15px',
        color: '#f8fafc',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);

    button.setInteractive({ useHandCursor: true });
    // A fresh handler on a freshly created object: a retry re-renders through
    // `clearDynamicObjects()`, which destroys this object and detaches this
    // listener, so repeated retries cannot accumulate handlers.
    button.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, () => this.retryLoad());

    this.renderedTexts.push(label);
    this.interactiveObjects.push(button);
  }

  /**
   * Draws one row per entry of the active category.
   *
   * Rows are drawn from the loaded array in the order it arrived: §5.5 defines
   * no ordering, so the scene neither sorts nor re-numbers them. When the
   * category is empty this draws nothing — the empty notice is the status line's
   * job — and no placeholder entry is created.
   */
  private drawItemList(): void {
    const startX = SAFE_AREA.x + 20;

    if (this.tab === 'pets') {
      this.ownedPets.slice(0, MAX_LIST_ROWS).forEach((pet, index) => {
        this.drawItemRow(
          startX,
          index,
          `${pet.identity}   ${pet.element}   ${pet.tier}   ★${pet.star}   Lv ${pet.level}`,
          pet.petId,
          () => this.selectItem(pet.petId),
          ELEMENT_COLORS[pet.element] ?? NEUTRAL_VALUE_COLOR
        );
      });
      return;
    }

    if (this.tab === 'cards') {
      this.ownedCards.slice(0, MAX_LIST_ROWS).forEach((card, index) => {
        this.drawItemRow(
          startX,
          index,
          `${card.name}   [${card.category}]`,
          card.cardId,
          () => this.selectItem(card.cardId),
          CARD_CATEGORY_COLORS[card.category] ?? NEUTRAL_VALUE_COLOR
        );
      });
      return;
    }

    this.ownedRelics.slice(0, MAX_LIST_ROWS).forEach((relic, index) => {
      this.drawItemRow(
        startX,
        index,
        relic.name,
        relic.relicId,
        () => this.selectItem(relic.relicId),
        NEUTRAL_VALUE_COLOR
      );
    });
  }

  /** Draws one clickable item row and registers its hit area. */
  private drawItemRow(
    x: number,
    index: number,
    text: string,
    itemId: string,
    onSelect: () => void,
    color: string
  ): void {
    const selected = this.selectedItemId === itemId;
    const y = CONTENT_TOP + 62 + index * ROW_HEIGHT;

    const label = this.add
      .text(x, y, `${selected ? '●' : '○'} ${text}`, {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '15px',
        color: selected ? '#fbbf24' : color,
      })
      .setOrigin(0, 0);

    label.setInteractive({ useHandCursor: true });
    label.on(Phaser.Input.Events.GAMEOBJECT_POINTER_DOWN, onSelect);

    this.renderedTexts.push(label);
    this.interactiveObjects.push(label);
  }

  /**
   * Draws the detail panel for the selected entry.
   *
   * The rows are the selected entry's **own wire members**, printed verbatim:
   * a Pet shows the six `§5.1` members, a Card the three `§5.3` members, and a
   * Relic the two `§5.4` members. Nothing is added to them — no description,
   * lore, stat, rarity, bonus, or upgrade level exists in these responses and
   * none is invented here (`AGENTS.md` §7, §10).
   *
   * The panel is presentation only: it reads the loaded arrays and writes text.
   * It does not modify the entry it is showing.
   */
  private drawDetailPanel(): void {
    if (this.detailText === null) {
      return;
    }

    if (this.selectedItemId === null) {
      this.detailText.setText('Select an item to view details');
      this.detailText.setColor('#64748b');
      return;
    }

    const rows = this.detailRows(this.selectedItemId);
    this.detailText.setText(rows.join('\n'));
    this.detailText.setColor('#f8fafc');
  }

  /**
   * The `Label: value` lines of the selected entry, taken from the loaded
   * response that carries the id. Every value is the member's own value; none is
   * formatted into a different meaning, and an id that is somehow no longer in
   * the loaded arrays yields the placeholder rather than a fabricated row.
   */
  private detailRows(itemId: string): string[] {
    const pet = this.ownedPets.find((entry) => entry.petId === itemId);
    if (pet !== undefined) {
      return [
        'ITEM DETAIL',
        '',
        `Pet: ${pet.identity}`,
        `Element: ${pet.element}`,
        `Tier: ${pet.tier}`,
        `Star: ${pet.star}`,
        `Level: ${pet.level}`,
        `Instance ID: ${pet.petId}`,
      ];
    }

    const card = this.ownedCards.find((entry) => entry.cardId === itemId);
    if (card !== undefined) {
      return [
        'ITEM DETAIL',
        '',
        `Card: ${card.name}`,
        `Category: ${card.category}`,
        `Card ID: ${card.cardId}`,
      ];
    }

    const relic = this.ownedRelics.find((entry) => entry.relicId === itemId);
    if (relic !== undefined) {
      return ['ITEM DETAIL', '', `Relic: ${relic.name}`, `Relic ID: ${relic.relicId}`];
    }

    return ['Select an item to view details'];
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
 * The empty notice per category.
 *
 * It states what the read returned — an empty collection, which §5.5 defines as
 * `200 []` — and nothing more: no unowned or locked entry is listed, because §5
 * serves owned items only and the client has no catalog to draw one from.
 */
const EMPTY_MESSAGES: Readonly<Record<CollectionTab, string>> = {
  pets: 'No owned Pets returned.',
  cards: 'No unlocked Cards returned.',
  relics: 'No owned Relics returned.',
};

/** Presentation-safe rendering of a rejection's own message. */
function describeError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
