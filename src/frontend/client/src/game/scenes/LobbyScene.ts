import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import type { GameRuntimePort } from '../runtime/GameRuntimeEvents';
import type { BattleStartRequest } from '../../services/api/BattleModels';
import type { CardResponse, PetResponse, RelicResponse } from '../../services/api/CollectionModels';

/**
 * The MVP Boss identity the battle-start request carries
 * (`BOSS_RULES.md` §6.4 — the canonical technical Identity, never the display
 * name "Hỏa Long" and never a `BossDefinitionId`).
 *
 * It is one fixed value: the MVP flow has **no Boss-selection step**
 * (`TDD.md` §2.1 — "There is no MVP Boss-selection step"), so the scene holds no
 * Boss list, performs no Boss fetch, and has no Boss selection state. The server
 * validates it and answers `400 BOSS_NOT_FOUND` for anything else
 * (`API_CONTRACTS.md` §3), which this scene reports like any other rejection.
 */
export const MVP_BOSS_ID = 'boss-hoa-long';

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
 * Equip Cards → Equip Relics → Start Battle — as presentation and interaction
 * only:
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
 * (`ARCHITECTURE.md` §2.2.1 rule 1, §2.2.3 rule 3).
 *
 * **The in-progress selection is scene state and lives nowhere else.** It is
 * created with the scene, held in the scene's own fields, and discarded on
 * `shutdown()`. It is not in `state/GameRuntimeState.ts` (rule 5), not on
 * `GameRuntime` (rule 2), and has no store, manager, or module of its own
 * (rule 1, `AGENTS.md` §9).
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
  private selectionText: Phaser.GameObjects.Text | null = null;
  private reviewText: Phaser.GameObjects.Text | null = null;
  private messageText: Phaser.GameObjects.Text | null = null;
  private errorText: Phaser.GameObjects.Text | null = null;

  constructor() {
    super('LobbyScene');
  }

  create(): void {
    this.runtime = readRuntime(this);

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
   * Scene shutdown — reached when this scene stops or the game is destroyed.
   *
   * Everything the scene owned is dropped here, including the in-progress
   * selection: a shut-down lobby has no selection, and the runtime keeps none on
   * its behalf (`ARCHITECTURE.md` §2.2.3 rules 1–2). Detaching the hit areas
   * prevents pointer handlers from surviving a scene restart.
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
   * The Start Battle trigger.
   *
   * It requires the slot counts to be filled (1 / 3 / 3–5) before it submits,
   * because an incomplete selection has no documented request shape at all —
   * `cardLoadout` is exactly three elements and `relicLoadout` is three to five
   * (`API_CONTRACTS.md` §3). That check is about *completeness*, not legality:
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

    try {
      await runtime.startBattle(this.buildStartRequest());
    } catch (error) {
      // The documented rejections (401 UNAUTHENTICATED, 400 INVALID_LOADOUT /
      // PET_NOT_OWNED / BOSS_NOT_FOUND) and transport failures all arrive here.
      // No battle exists after any of them, so the scene stays put.
      this.startError = `Battle start failed: ${describeError(error)}`;
      this.startPending = false;
      this.render();
      return;
    }

    this.startPending = false;
    this.scene.start('BattleScene');
  }

  /**
   * Builds the `BattleStartRequest` (`API_CONTRACTS.md` §3) from the
   * in-progress selection — exactly its four documented members and nothing
   * else.
   *
   * ```text
   * petId         the selected owned Pet instance id from the collection read
   * bossId        the fixed MVP Boss identity (BOSS_RULES.md §6.4)
   * cardLoadout   the three selected Basic Card ids, as chosen
   * relicLoadout  the three-to-five selected owned Relic instance ids, in the
   *               order chosen — position i is equip slot i + 1 (§2.3)
   * ```
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
      bossId: MVP_BOSS_ID,
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
    // (GDD.md §2). The fixed Boss target is stated here because the MVP has no
    // Boss-selection step (TDD.md §2.1).
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

    // Slot counters — the selection affordance.
    this.selectionText = small(SAFE_AREA.x + 26, SAFE_AREA.y + SAFE_AREA.height - 132, '#93c5fd');

    // Step 4 — loadout Review. Presentational only: it restates the selection
    // that would be submitted, and asserts nothing about its validity.
    this.reviewText = small(SAFE_AREA.x + 26, SAFE_AREA.y + SAFE_AREA.height - 96, '#94a3b8');

    this.messageText = small(SAFE_AREA.x + 26, SAFE_AREA.y + SAFE_AREA.height - 58, '#fbbf24');
    this.messageText.setWordWrapWidth(SAFE_AREA.width - 100);

    this.errorText = small(SAFE_AREA.x + 26, SAFE_AREA.y + SAFE_AREA.height - 26, '#f87171');
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

    this.renderPets();
    this.renderCards();
    this.renderRelics();

    this.selectionText?.setText(
      [
        `Pet:   ${this.selectedPetId ?? '—'}`,
        `Cards: ${this.selectedCardIds.length}/${CARD_LOADOUT_SIZE}`,
        `Relics: ${this.selectedRelicIds.length}/${MAX_RELIC_LOADOUT_SIZE}` +
          ` (min ${MIN_RELIC_LOADOUT_SIZE})`,
      ].join('\n')
    );

    // Step 4 — Review. The Boss line states the fixed target; the loadout lines
    // restate the selection. This is a summary of a request the player is about
    // to submit, not a validation of it.
    this.reviewText?.setText(
      [
        `4. REVIEW — Boss: ${MVP_BOSS_ID}`,
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
