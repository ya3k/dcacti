import * as Phaser from 'phaser';
import { SAFE_AREA, GAME_WIDTH } from '../GameViewport';
import { readRuntime } from '../runtime/RuntimeRegistry';
import type { GameRuntimePort } from '../runtime/GameRuntimeEvents';
import { formatDeliveredValue, formatLeveledUp, formatRewards } from '../presentation/RewardSummaryFormat';
import type { BattleHistoryItemResponse } from '../../services/api/BattleModels';

/** The background fill inside the safe area (Slate 900). */
const BACKGROUND_COLOR = 0x0f172a;
/** The safe area frame (Slate 700). */
const FRAME_COLOR = 0x334155;
/** The panel fill (Slate 800) and its border (Slate 700). */
const PANEL_FILL_COLOR = 0x1e293b;
const PANEL_BORDER_COLOR = 0x334155;
/** The primary action fill (Blue 700) and its border (Blue 400). */
const ACTIVE_FILL_COLOR = 0x1d4ed8;
const ACTIVE_BORDER_COLOR = 0x60a5fa;
/** The secondary action fill (Slate 800) and its border (Slate 600). */
const INACTIVE_FILL_COLOR = 0x1e293b;
const INACTIVE_BORDER_COLOR = 0x475569;

/** Header/back-button geometry, in logical game pixels. */
const BACK_BUTTON_WIDTH = 140;
const BACK_BUTTON_HEIGHT = 36;

/** The account-progression panel below the header. */
const PROGRESSION_TOP = SAFE_AREA.y + 70;
const PROGRESSION_HEIGHT = 92;

/** The delivered-history list below the progression panel. */
const LIST_TOP = PROGRESSION_TOP + PROGRESSION_HEIGHT + 22;
const LIST_BOTTOM = SAFE_AREA.y + SAFE_AREA.height - 30;
const LIST_HEIGHT = LIST_BOTTOM - LIST_TOP;

/**
 * One history entry's block: the heading, the three labelled members, and the
 * three lines of the shared `RewardSummary` rendering.
 *
 * It is a fixed pitch rather than a measured layout: the entry's line count is
 * fixed by the contract's member set (§4.5 note 12) and the reward rendering's
 * own shape, so the vertical pitch is a known constant.
 */
const ENTRY_HEIGHT = 150;

/**
 * How many complete entries the list area can present without overflowing the
 * safe area. §4.5 note 5 makes the array the Player's **complete** history with
 * no pagination, so a longer history is never truncated silently: the scene
 * states how many of the delivered entries it is showing.
 */
const MAX_VISIBLE_ENTRIES = Math.max(1, Math.floor(LIST_HEIGHT / ENTRY_HEIGHT));

/** The empty-history notice (§4.5 note 9: `200 []` is not an error). */
const EMPTY_MESSAGE = 'No battles yet';

/**
 * BattleHistoryScene — the read-only Battle History & account-progression
 * surface.
 *
 * It presents the authenticated Player's completed battles and the latest
 * delivered Player Level / XP, which are exactly what
 * `GET /api/battle/history` returns (`API_CONTRACTS.md` §4.5). It is a
 * **viewer**: it owns no battle, no battle state, no progression rule, and no
 * loadout.
 *
 * ```text
 * MainMenuScene
 *     │  BATTLE HISTORY
 *     ↓
 * BattleHistoryScene
 *     ├── Player Level / XP        the newest delivered element's own values
 *     ├── Battle #1 …              one block per delivered element, in order
 *     └── < BACK ───────────────► MainMenuScene
 * ```
 *
 * **The read goes through the runtime port.** The scene imports neither
 * `@microsoft/signalr`, nor `fetch`, nor `ApiService`:
 * `services/api/` and `services/realtime/` are transport layers, and a scene
 * reaching either would bypass the boundary (`ARCHITECTURE.md` §2.2.1 rule 1,
 * §2.2.3 rule 3). It opens no `BattleHub` connection and subscribes to no
 * battle push — a history read is a request/response and §4.5 introduces no
 * message of any kind (its note 13).
 *
 * **Every presented value is the delivered one.** The five element members
 * `battleId`, `outcome`, `rewards`, `durationTurns`, and `completedAt`
 * (note 12) are printed from the response: no XP is summed, no Level is derived
 * from XP, no `leveledUp` flag is recomputed, no `completedAt` is parsed or
 * re-formatted, no `durationTurns` is re-counted, and no victory/defeat
 * semantics is decided here (note 2, `GAME_RULES.md` §18, ADR-001,
 * `AGENTS.md` §10). The rewards use the presentation layer's existing
 * `RewardSummary` rendering (`game/presentation/RewardSummaryFormat.ts`), so
 * the eight delivered members are not represented twice.
 *
 * **The delivered order is preserved exactly.** §4.5 note 4 fixes the order —
 * `CompletedAt` descending, tie-broken by `BattleResultId` descending — and
 * states that clients MAY rely on it, so the array is rendered as received:
 * the scene does not sort, filter, reverse, re-number by timestamp, or infer an
 * order from `battleId`. The `Battle #n` caption is a positional label for the
 * delivered array (the task's own sketch), not a derived ordering.
 *
 * **The newest element's own progression values are the account's display.**
 * The progression panel prints `history[0].rewards`' Player-track members —
 * `newPlayerLevel`, `newPlayerXp`, and `playerLeveledUp` — because the newest
 * completed battle is the one whose delivered result carries the Player's
 * current values. A `null` there is the contract's "no row" value and is shown
 * as unavailable, never as a fabricated Level or XP.
 *
 * **The history is ephemeral presentation state.** It is a plain scene field,
 * replaced wholesale by a reload and dropped by the scene's teardown, which
 * `create()` attaches to Phaser's actual scene lifecycle events
 * (`Phaser.Scenes.Events.SHUTDOWN` / `DESTROY`). It is not added to
 * `GameRuntimeState`, and no store, singleton, cache, or module-level slot is
 * introduced (`ARCHITECTURE.md` §2.2.3 rules 1–2, §5 item 5, `AGENTS.md` §9,
 * ADR-022 D3).
 *
 * **The read is asynchronous, and this scene owns that.** A per-presentation run
 * counter (`historyLoadRun`) is taken by `create()`, advanced by both `create()`
 * and the teardown, and re-checked before any write, so a response that settles
 * after the scene has been shut down — or after the instance has been reused
 * for a later presentation — is discarded instead of mutating UI that is no
 * longer the presentation it was read for.
 *
 * It obtains the port with `readRuntime(this)`. When no runtime was supplied the
 * scene still runs and reports that the history cannot be loaded, so scene
 * lifecycle never depends on runtime availability.
 */
export class BattleHistoryScene extends Phaser.Scene {
  private runtime: GameRuntimePort | null = null;

  // --- Presentation state -----------------------------------------------------

  /** The delivered history, exactly as the read returned it. */
  private history: BattleHistoryItemResponse[] = [];
  /** The history read is in flight. */
  private loading = false;
  /** The failure feedback from the last rejected read, or `null`. */
  private loadError: string | null = null;

  /**
   * Identifies the presentation run an in-flight history read belongs to.
   *
   * `create()` takes the next value and hands it to `loadHistory()`, and both
   * `create()` and the scene's teardown advance it. The read therefore writes
   * only while the run that started it is still the current one: a
   * `getBattleHistory` promise that settles after the scene was shut down (its
   * `Text` objects destroyed by Phaser's `DisplayList#shutdown`), or after the
   * instance was reused, is discarded instead of mutating UI that is no longer
   * its own — the `ResultScene` `rewardLoadRun` solution (TASK-205).
   *
   * It is plain scene-local state: no cancellation registry, controller, or
   * global state is introduced (`AGENTS.md` §9).
   */
  private historyLoadRun = 0;

  // --- Scene-owned display objects -------------------------------------------
  //
  // The shell is built once in `drawShell()` and kept; the dynamic content area
  // (list blocks, notices, the retry control) is rebuilt on every render pass.
  // The teardown `create()` attaches to Phaser's lifecycle events clears and
  // detaches both, so a shut-down scene leaves no interactive object and no
  // loaded history behind.

  /** The persistent shell text objects, created once by `drawShell()`. */
  private shellObjects: Phaser.GameObjects.GameObject[] = [];
  /** The persistent shell's text objects, so a re-render can update them. */
  private titleText: Phaser.GameObjects.Text | null = null;
  private progressionTitleText: Phaser.GameObjects.Text | null = null;
  private progressionText: Phaser.GameObjects.Text | null = null;
  private statusText: Phaser.GameObjects.Text | null = null;
  /** Rebuilt on every render pass; each entry is one interactive hit area. */
  private interactiveObjects: Phaser.GameObjects.GameObject[] = [];
  private renderedTexts: Phaser.GameObjects.Text[] = [];

  /**
   * Guards the Back navigation so it fires at most once per scene instance,
   * mirroring `MainMenuScene`'s and `CollectionViewerScene`'s own guard: a rapid
   * double tap must not produce a duplicate scene transition (`AGENTS.md` §16 —
   * same pattern, not a new one).
   */
  private hasTransitioned = false;

  constructor() {
    super('BattleHistoryScene');
  }

  create(): void {
    this.runtime = readRuntime(this);

    // Attach this scene's teardown to Phaser's own scene lifecycle events,
    // before any of the objects it releases is created.
    //
    // A scene method that merely *exists* is never invoked: Phaser 4.2.1 calls
    // `init`/`preload`/`create`/`update` by name and nothing else, and
    // `Phaser.Scene` declares no `shutdown` method at all. The engine's teardown
    // signal is `Phaser.Scenes.Events.SHUTDOWN` (from `Systems#shutdown`) and
    // `DESTROY` (from `Systems#destroy`); that is the documented mechanism
    // (`.ai/skills/client/phaser-architecture` "Resource Lifecycle & Cleanup";
    // `.ai/skills/phaser/scenes` gotcha 14).
    //
    // The registration is idempotent — the pair is detached first, then attached
    // with `once` — so a scene that is shut down and later reopened holds
    // exactly one teardown handler per event instead of stacking another one on
    // every run (`once` alone would leave the run's unfired `DESTROY` handler
    // behind). This is the same "detach before attach" idempotency
    // `BattleScene.registerBoardInput` uses (TASK-205).
    this.events.off(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.off(Phaser.Scenes.Events.DESTROY, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, this.shutdown, this);
    this.events.once(Phaser.Scenes.Events.DESTROY, this.shutdown, this);

    // A reopened scene starts from a clean slate: Phaser reuses the instance, so
    // state left by a previous run must not survive into this one.
    this.resetHistoryState();
    this.drawShell();

    if (this.runtime === null) {
      this.loadError = 'No runtime is available: the battle history cannot be loaded.';
      this.render();
      return;
    }

    // This presentation's history read run. `loadHistory` captures the value and
    // checks it again once the read settles, so the asynchronous continuation can
    // only write to the presentation that started it (see the field's own note).
    const run = ++this.historyLoadRun;

    this.render();
    void this.loadHistory(run);
  }

  /**
   * Scene teardown — the handler subscribed to Phaser's own
   * `Phaser.Scenes.Events.SHUTDOWN` / `DESTROY` events by `create()`.
   *
   * It is a plain method invoked *by the engine's event*, not an engine hook:
   * Phaser never calls a scene method named `shutdown` on its own, so before
   * this registration existed the body below never ran in the browser.
   *
   * It destroys every object the scene created, detaches every interactive
   * handler, drops the loaded history, and **invalidates any history read still
   * in flight** by advancing {@link historyLoadRun}. Nothing outside the scene's
   * own presentation is touched: the runtime holds no history on this scene's
   * behalf, the preserved pre-battle loadout is not this scene's (ADR-022), and
   * no battle state is read, cleared, or written here.
   *
   * Phaser's own `DisplayList#shutdown` handles the same event and destroys
   * every child before this handler runs, so the `off` / `destroy` calls below
   * are made against already-destroyed game objects; `GameObject#destroy` is a
   * documented no-op once `scene` is released, and the `off` calls are
   * harmless. Every step is idempotent and safe on both events.
   */
  shutdown(): void {
    this.historyLoadRun++;

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
    this.progressionTitleText = null;
    this.progressionText = null;
    this.statusText = null;

    this.resetHistoryState();
  }

  /** Discards everything this presentation loaded or derived. */
  private resetHistoryState(): void {
    this.history = [];
    this.loading = false;
    this.loadError = null;
    this.hasTransitioned = false;
  }

  // ---------------------------------------------------------------------------
  // History read (through the runtime port)
  // ---------------------------------------------------------------------------

  /**
   * Reads the completed-battle history through the runtime port
   * (`API_CONTRACTS.md` §4.5).
   *
   * The delivered array is stored exactly as received — same elements, same
   * order, no transformation. On rejection the scene reports the failure and
   * offers `RETRY`; nothing is substituted for the missing history, and no
   * partially loaded list is kept (a failed read leaves `history` empty, so no
   * rows can be rendered from a previous attempt alongside the error).
   *
   * `run` is the presentation run `create()` started this read for. The read is
   * asynchronous and the scene can be shut down or reused while it is in flight,
   * so the delivery is applied only when that run is still the current one
   * ({@link historyLoadRun}).
   */
  private async loadHistory(run: number): Promise<void> {
    const runtime = this.runtime;

    if (runtime === null) {
      return;
    }

    this.loading = true;
    this.loadError = null;
    this.render();

    let history: BattleHistoryItemResponse[];

    try {
      history = await runtime.getBattleHistory();
    } catch (error) {
      // The presentation that asked for this read may be over, or may have been
      // replaced: a stale failure must not paint an error over a later run.
      if (run !== this.historyLoadRun) {
        return;
      }

      this.loading = false;
      this.loadError = `Battle history load failed: ${describeError(error)}`;
      // A failed read renders no list: the stale array (if any) is dropped rather
      // than shown beside the error.
      this.history = [];
      this.render();
      return;
    }

    if (run !== this.historyLoadRun) {
      // The run this read belonged to is over — the scene was shut down, or the
      // instance was reused for a later presentation. Neither may be written by
      // this continuation.
      return;
    }

    this.history = history;
    this.loading = false;
    this.loadError = null;
    this.render();
  }

  /** Re-issues the history read — the `RETRY` action behind a failed read. */
  private retryLoad(): void {
    void this.loadHistory(++this.historyLoadRun);
  }

  // ---------------------------------------------------------------------------
  // Interaction
  // ---------------------------------------------------------------------------

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
   * Draws the persistent shell: the background frame, the title, the
   * account-progression panel, and the feedback line.
   *
   * The frames are static; only the text drawn into them changes between render
   * passes.
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
      .text(GAME_WIDTH / 2, SAFE_AREA.y + 28, 'BATTLE HISTORY', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '22px',
        color: '#e2e8f0',
        fontStyle: 'bold',
      })
      .setOrigin(0.5);
    this.shellObjects.push(this.titleText);

    // The account-progression panel. Its heading is static; its values are the
    // newest delivered element's own members, written by `renderProgression()`.
    const panel = this.add.rectangle(
      GAME_WIDTH / 2,
      PROGRESSION_TOP + PROGRESSION_HEIGHT / 2,
      SAFE_AREA.width - 52,
      PROGRESSION_HEIGHT,
      PANEL_FILL_COLOR
    );
    panel.setStrokeStyle(1, PANEL_BORDER_COLOR);
    this.shellObjects.push(panel);

    this.progressionTitleText = this.add
      .text(SAFE_AREA.x + 26, PROGRESSION_TOP + 14, 'Player Level / XP', {
        fontFamily: 'system-ui, sans-serif',
        fontSize: '16px',
        color: '#94a3b8',
        fontStyle: 'bold',
      })
      .setOrigin(0, 0);
    this.shellObjects.push(this.progressionTitleText);

    this.progressionText = this.add
      .text(SAFE_AREA.x + 26, PROGRESSION_TOP + 42, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '20px',
        color: '#f8fafc',
      })
      .setOrigin(0, 0);
    this.shellObjects.push(this.progressionText);

    // The feedback line under the progression panel: loading state, empty
    // notice, or error.
    this.statusText = this.add
      .text(SAFE_AREA.x + 26, PROGRESSION_TOP + PROGRESSION_HEIGHT + 2, '', {
        fontFamily: 'ui-monospace, monospace',
        fontSize: '15px',
        color: '#94a3b8',
      })
      .setOrigin(0, 0)
      .setWordWrapWidth(SAFE_AREA.width - 52);
    this.shellObjects.push(this.statusText);
  }

  /**
   * Renders the current scene state.
   *
   * Every pass rebuilds the dynamic content area from the loaded history and
   * re-creates its hit areas, so what is presented is always exactly what the
   * last read returned — a reload never leaves stale rows or listeners behind.
   */
  private render(): void {
    this.clearDynamicObjects();

    this.drawBackButton();
    this.renderProgression();
    this.drawStatusLine();

    if (this.loadError === null && !this.loading) {
      this.drawHistoryList();
    }
  }

  /** Draws the persistent `< BACK` button returning to `MainMenuScene`. */
  private drawBackButton(): void {
    const x = SAFE_AREA.x + 20 + BACK_BUTTON_WIDTH / 2;
    const y = SAFE_AREA.y + 20 + BACK_BUTTON_HEIGHT / 2;

    const button = this.add
      .rectangle(x, y, BACK_BUTTON_WIDTH, BACK_BUTTON_HEIGHT, INACTIVE_FILL_COLOR)
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
   * Writes the account-progression line from the **newest delivered element's
   * own Player-track members** (`API_CONTRACTS.md` §4.5 notes 2–4; the member
   * list is `DATABASE.md` §1's).
   *
   * ```text
   * Level <newPlayerLevel>   ·   XP <newPlayerXp>   [·  LEVEL UP]
   * ```
   *
   * The three values — `newPlayerLevel`, `newPlayerXp`, and `playerLeveledUp` —
   * are printed as delivered. No Level is derived from XP, no XP is summed, and
   * the level-up flag is never re-derived by comparing values; a `null` member
   * is printed as unavailable rather than promoted to a number
   * (`DATABASE.md` §1 items 4–5, `AGENTS.md` §10).
   *
   * With no delivered history there is nothing authoritative to show, so the
   * panel states that rather than inventing a starting Level or XP (§4.5 note 9,
   * `AGENTS.md` §7).
   */
  private renderProgression(): void {
    if (this.progressionText === null) {
      return;
    }

    // While the read is in flight, or after it failed, no delivered values exist
    // yet — the panel says so and fabricates nothing.
    if (this.loading) {
      this.progressionText.setText('—');
      this.progressionText.setColor('#64748b');
      return;
    }

    if (this.loadError !== null) {
      this.progressionText.setText('—');
      this.progressionText.setColor('#64748b');
      return;
    }

    const newest = this.history[0];

    if (newest === undefined) {
      // No battles: no delivered progression exists. §4.5 note 9's empty history
      // is not a `Level 1` / `0 XP` statement, so the panel shows "unavailable"
      // and the empty notice below states the fact.
      this.progressionText.setText('—');
      this.progressionText.setColor('#64748b');
      return;
    }

    const rewards = newest.rewards;
    this.progressionText.setText(
      `Level ${formatDeliveredValue(rewards.newPlayerLevel)}   ·   XP ${formatDeliveredValue(rewards.newPlayerXp)}${formatLeveledUp(rewards.playerLeveledUp)}`
    );
    this.progressionText.setColor('#f8fafc');
  }

  /**
   * Draws the feedback line under the progression panel.
   *
   * It reports exactly one of the three documented states — the read is in
   * flight, the read failed, or the Player has no completed battles — and
   * otherwise says nothing, so a populated history carries no notice beyond the
   * truncation statement below.
   */
  private drawStatusLine(): void {
    if (this.loading) {
      this.statusText?.setText('Loading battle history…');
      this.statusText?.setColor('#93c5fd');
      return;
    }

    if (this.loadError !== null) {
      // The error banner. `RETRY` is drawn beside it as a real action, and no
      // partial list is rendered from a failed read.
      this.statusText?.setText(this.loadError);
      this.statusText?.setColor('#f87171');
      this.drawRetryButton();
      return;
    }

    if (this.history.length === 0) {
      this.statusText?.setText(EMPTY_MESSAGE);
      this.statusText?.setColor('#64748b');
      return;
    }

    if (this.history.length > MAX_VISIBLE_ENTRIES) {
      // The array is the complete history (§4.5 note 5) and no pagination exists,
      // so a history longer than the safe area can hold is reported rather than
      // silently clipped. The counts are the delivered array's own length — a
      // measurement, not a derived gameplay value.
      this.statusText?.setText(
        `Showing the ${MAX_VISIBLE_ENTRIES} newest of ${this.history.length} battles.`
      );
      this.statusText?.setColor('#64748b');
      return;
    }

    this.statusText?.setText('');
  }

  /** Draws the `RETRY` action for a failed read. */
  private drawRetryButton(): void {
    const width = 120;
    const height = 32;
    const x = SAFE_AREA.x + 20 + width / 2;
    // Sits under the wrapped error banner, still inside the safe area: the banner
    // line starts at `PROGRESSION_TOP + PROGRESSION_HEIGHT + 2` and the message
    // occupies at most two lines at this width.
    const y = PROGRESSION_TOP + PROGRESSION_HEIGHT + 52;

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
   * Draws one block per delivered history element, in the delivered order.
   *
   * Each block prints the element's own five members (note 12): its heading
   * carries the positional `Battle #n` caption and the element's `battleId`, and
   * the lines carry `outcome`, `durationTurns`, `completedAt`, and the rewards
   * through the shared `RewardSummary` rendering. Nothing is re-ordered,
   * re-numbered by time, or re-formatted, and no entry is created that the
   * response did not contain.
   *
   * The block is a text object rather than a button: this surface is read-only
   * and has no per-entry action (`AGENTS.md` §9).
   */
  private drawHistoryList(): void {
    if (this.history.length === 0) {
      // The empty notice is the status line's job; no placeholder entry is
      // created to fill the space.
      return;
    }

    this.history.slice(0, MAX_VISIBLE_ENTRIES).forEach((entry, index) => {
      const y = LIST_TOP + 6 + index * ENTRY_HEIGHT;

      const label = this.add
        .text(SAFE_AREA.x + 26, y, entryLines(entry, index), {
          fontFamily: 'ui-monospace, monospace',
          fontSize: '14px',
          color: '#cbd5e1',
          lineSpacing: 6,
        })
        .setOrigin(0, 0)
        .setWordWrapWidth(SAFE_AREA.width - 52);

      this.renderedTexts.push(label);
    });
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
 * The lines of one delivered history element's block.
 *
 * Every line is the element's own delivered member (`API_CONTRACTS.md` §4.5
 * note 12):
 *
 * ```text
 * Battle #<n>  <battleId>
 * Outcome: <outcome>
 * Duration: <durationTurns> turns
 * Completed At: <completedAt>
 * <the shared RewardSummary rendering: REWARDS, Player:, Pet:>
 * ```
 *
 * `outcome` is printed as the wire value it is (`victory` | `defeat`) — this
 * surface carries no outcome semantics of its own and decides nothing from HP.
 * `durationTurns` and `completedAt` are printed as delivered: the turn count is
 * not re-counted and the completion reading is not parsed, localised, or
 * re-derived (§4.5 notes 2–3, `DATABASE.md` §1).
 *
 * `n` is the element's position in the delivered array (1-based), which is the
 * contract's own order (note 4) — the array is never re-ordered to produce it.
 */
function entryLines(entry: BattleHistoryItemResponse, index: number): string {
  return [
    `Battle #${index + 1}  ${entry.battleId}`,
    `Outcome: ${entry.outcome}`,
    `Duration: ${entry.durationTurns} turns`,
    `Completed At: ${entry.completedAt}`,
    formatRewards(entry.rewards),
  ].join('\n');
}

/** Presentation-safe rendering of a rejection's own message. */
function describeError(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
