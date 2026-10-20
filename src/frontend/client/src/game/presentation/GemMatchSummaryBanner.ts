/**
 * GemMatchSummaryBanner — the one presentation of a delivered
 * `MatchedGemSummary` (`SIGNALR_PROTOCOL.md` §3.2.29, `GAME_EVENTS.md` §2).
 *
 * It answers one question for the player: **how many Gems did this actor's board
 * cycle match**, per Gem type. It is deliberately one reusable component used by
 * both actors' cycles — the Player's summary and the Boss Response phase's are the
 * same widget, differing only in the title line it is handed — so neither side can
 * drift into a second rendering of the same contract (`docs/AGENTS.md` §2).
 *
 * **It renders delivered counts; it computes nothing.** Every chip comes from one
 * delivered `gems` entry: the type, its label and colour are the game's existing
 * board palette (passed in — this module keeps no second palette), and the count
 * is the delivered integer. Nothing is summed, scaled, converted into damage,
 * healing, DEF buff magnitude, or Power, and a Gem type the item
 * did not name simply has no chip (`§3.2.29` items 3, 8; `AGENTS.md` §10).
 *
 * **It is a transient presentation effect, not a HUD region and not a second
 * notification system.** It creates its own objects for its own phase and destroys
 * itself when its beats finish; the caller keeps the returned reference only so an
 * aborted timeline can cancel it (the same contract `playTurnBanner` follows).
 */

import type * as Phaser from 'phaser';

/** One Gem type's presentation: the board's own fill colour and short label. */
export interface GemPaletteEntry {
  readonly color: number;
  readonly label: string;
}

/**
 * The board's Gem-type presentation, keyed by the §3.2.4 contract name a delivered
 * `gemType` carries.
 *
 * It is supplied by the caller rather than declared here: the board already owns
 * this mapping, and a second copy would be a second representation of one fact
 * (`docs/AGENTS.md` §2). A type outside the map is drawn as the inert placeholder
 * the board itself uses and is never guessed at.
 */
export type GemPalette = Readonly<Record<string, GemPaletteEntry>>;

/**
 * The banner's own beats, in one place, so its total lifetime is derived from
 * them rather than restated by the caller.
 *
 * These are display timings and nothing else: no gameplay value, order, or
 * outcome depends on them, they are never sent or read from the wire, and without
 * a tween manager (a unit harness) the banner is created and left visible — which
 * is what keeps the phase observable without a clock.
 */
export const GEM_MATCH_SUMMARY_TIMING = Object.freeze({
  /** How long the panel takes to settle in from `enterScale`. */
  enterMs: 200,
  /** How long the settled panel is held, readable, before it fades. */
  holdMs: 900,
  /** The fade-out beat. */
  exitMs: 240,
  /** The scale the panel enters from; it settles at 1 over `enterMs`. */
  enterScale: 0.92,
  titleFontSize: '15px',
  countFontSize: '15px',
  chipLabelFontSize: '11px',
  /** The panel's height, in logical game pixels. */
  panelHeight: 66,
  /** Horizontal padding inside the panel, each side. */
  panelPaddingX: 18,
  /** The width one Gem chip (icon + label + count) occupies. */
  chipSlotWidth: 62,
  /** The gap between two chips. */
  chipGap: 6,
  /** The panel's minimum width, so a one-chip summary is still readable. */
  panelMinWidth: 264,
});

/**
 * The banner's total lifetime: its three beats, which is exactly what a phase
 * presenting it must be held for.
 */
export function gemMatchSummaryBannerMs(): number {
  return (
    GEM_MATCH_SUMMARY_TIMING.enterMs +
    GEM_MATCH_SUMMARY_TIMING.holdMs +
    GEM_MATCH_SUMMARY_TIMING.exitMs
  );
}

/**
 * The title line the delivered cycle's side is presented with, or `null` when the
 * side is not one this contract defines.
 *
 * The two titles are display text, not wire values: the side itself is the
 * delivered `SwapStarted.actor` that opened the cycle (`SIGNALR_PROTOCOL.md`
 * §3.2.26 item 2), and a cycle that named no side gets no banner rather than a
 * guessed one (`AGENTS.md` §10).
 */
export function gemMatchSummaryTitle(actor: 'player' | 'boss' | null): string | null {
  if (actor === 'player') {
    return 'PLAYER GEMS MATCHED';
  }

  if (actor === 'boss') {
    return 'BOSS GEMS MATCHED';
  }

  return null;
}

/**
 * One drawn chip: a Gem type with the board's presentation and the delivered
 * count.
 */
export interface GemMatchSummaryChip {
  readonly gemType: string;
  readonly label: string;
  readonly color: number;
  readonly count: number;
}

/**
 * The chips a delivered `gems` array produces, in the order the item delivered
 * them.
 *
 * The order is the item's own (`§3.2.29` item 4 fixes ATK, DEF, HP, POWER) and is
 * never re-sorted; a count that is not a positive integer is not drawable and
 * contributes no chip, so the banner never states a count the item did not
 * deliver. An empty result means there is nothing to show.
 */
export function gemMatchSummaryChips(
  gems: readonly { readonly gemType: string; readonly count: number }[],
  palette: GemPalette
): readonly GemMatchSummaryChip[] {
  const chips: GemMatchSummaryChip[] = [];

  for (const gem of gems) {
    if (!Number.isInteger(gem.count) || gem.count < 1) {
      continue;
    }

    // The board's own palette entry, or the inert placeholder the board uses for
    // a type it does not know — never a substitution and never a guess.
    const presentation = palette[gem.gemType] ?? { color: 0x475569, label: '?' };

    chips.push({
      gemType: gem.gemType,
      label: presentation.label,
      color: presentation.color,
      count: gem.count,
    });
  }

  return chips;
}

/** What one banner is told to present. */
export interface GemMatchSummaryBannerOptions {
  /** The scene the objects belong to. */
  readonly scene: Phaser.Scene;
  /** The layer the objects are added to (the scene's transient feedback layer). */
  readonly layer: Phaser.GameObjects.Container;
  /** The panel's centre, in the layer's coordinates. */
  readonly x: number;
  readonly y: number;
  /** The side whose cycle this is, or `null` when the batch named none. */
  readonly actor: 'player' | 'boss' | null;
  /** The delivered counts, in the item's own order. */
  readonly gems: readonly { readonly gemType: string; readonly count: number }[];
  /** The board's Gem palette. */
  readonly palette: GemPalette;
  /** The title/accent colour, as the scene's own side colour (`#rrggbb`). */
  readonly accentColor: string;
  /** The tween manager, or `undefined` in a harness with no clock. */
  readonly tweens: Phaser.Tweens.TweenManager | undefined;
}

/**
 * One presented banner: the container it created, its lifetime, and the only
 * operation a caller needs — destroying it when a timeline is abandoned.
 */
export class GemMatchSummaryBanner {
  private readonly container: Phaser.GameObjects.Container;
  private readonly tweens: Phaser.Tweens.TweenManager | undefined;
  private destroyed = false;

  private constructor(options: GemMatchSummaryBannerOptions, container: Phaser.GameObjects.Container) {
    this.container = container;
    this.tweens = options.tweens;
    this.playBeats();
  }

  /**
   * Presents one banner, or returns `null` when there is nothing to present.
   *
   * `null` is the honest answer for a cycle whose side this contract does not
   * define and for an item that delivered no drawable count: no title is invented
   * and no chip is fabricated.
   */
  static present(options: GemMatchSummaryBannerOptions): GemMatchSummaryBanner | null {
    const title = gemMatchSummaryTitle(options.actor);
    const chips = gemMatchSummaryChips(options.gems, options.palette);

    if (title === null || chips.length === 0) {
      return null;
    }

    const timing = GEM_MATCH_SUMMARY_TIMING;
    const chipsWidth = chips.length * timing.chipSlotWidth + (chips.length - 1) * timing.chipGap;
    const panelWidth = Math.max(timing.panelMinWidth, chipsWidth + timing.panelPaddingX * 2);
    const halfHeight = timing.panelHeight / 2;
    const accent = hexColor(options.accentColor);

    const container = options.scene.add.container(options.x, options.y);

    const panel = options.scene.add
      .rectangle(0, 0, panelWidth, timing.panelHeight, 0x0b1220, 0.9)
      .setStrokeStyle(2, accent, 0.9);
    container.add(panel);

    const titleText = options.scene.add
      .text(0, -halfHeight + 15, title, {
        fontFamily: 'system-ui, sans-serif',
        fontSize: timing.titleFontSize,
        color: options.accentColor,
        fontStyle: 'bold',
      })
      .setOrigin(0.5);
    container.add(titleText);

    // The chips run left to right inside the panel, centred as one row: the first
    // slot's left edge is placed so the whole row is horizontally centred.
    const rowLeft = -chipsWidth / 2;
    const rowY = 12;

    for (let index = 0; index < chips.length; index++) {
      const chip = chips[index];
      const slotLeft = rowLeft + index * (timing.chipSlotWidth + timing.chipGap);
      const centerX = slotLeft + timing.chipSlotWidth / 2;

      // The icon is the board's own treatment: a filled square in the Gem type's
      // colour with its short label on it, so a chip reads as the same Gem the
      // board draws.
      const icon = options.scene.add
        .rectangle(centerX - 18, rowY, 20, 20, chip.color)
        .setStrokeStyle(1, 0x0b0f19);
      container.add(icon);

      const iconLabel = options.scene.add
        .text(centerX - 18, rowY, chip.label, {
          fontFamily: 'system-ui, sans-serif',
          fontSize: timing.chipLabelFontSize,
          color: '#0b0f19',
          fontStyle: 'bold',
        })
        .setOrigin(0.5);
      container.add(iconLabel);

      const countText = options.scene.add
        .text(centerX - 4, rowY, `×${chip.count}`, {
          fontFamily: 'system-ui, sans-serif',
          fontSize: timing.countFontSize,
          color: '#e2e8f0',
          fontStyle: 'bold',
        })
        .setOrigin(0, 0.5);
      container.add(countText);
    }

    options.layer.add(container);

    return new GemMatchSummaryBanner(options, container);
  }

  /** The banner's total lifetime, which its phase must be held for. */
  get totalMs(): number {
    return gemMatchSummaryBannerMs();
  }

  /**
   * Destroys the panel and every object on it.
   *
   * It is idempotent: an aborted timeline destroys the banner, and the exit beat's
   * own completion may then run against an already-destroyed panel. A caller that
   * drops the reference first (an aborted timeline) leaves nothing behind either.
   */
  destroy(): void {
    if (this.destroyed) {
      return;
    }

    this.destroyed = true;

    if (this.tweens) {
      this.tweens.killTweensOf(this.container);
    }

    // `container.destroy()` releases the children it owns, so no object survives
    // the panel.
    this.container.destroy();
  }

  /**
   * The three beats: enter, hold, exit.
   *
   * Without a tween manager (a unit harness) the panel is created and left
   * visible, exactly as the board's floaters and highlight behave.
   */
  private playBeats(): void {
    if (!this.tweens) {
      return;
    }

    const timing = GEM_MATCH_SUMMARY_TIMING;

    this.container.setAlpha(1);
    this.container.setScale(timing.enterScale);

    this.tweens.add({
      targets: this.container,
      scale: 1,
      duration: timing.enterMs,
    });

    this.tweens.add({
      targets: this.container,
      alpha: 0,
      delay: timing.enterMs + timing.holdMs,
      duration: timing.exitMs,
      onComplete: () => {
        this.destroy();
      },
    });
  }
}

/** Converts a `#rrggbb` display colour to the numeric form a fill/stroke takes. */
function hexColor(color: string): number {
  const parsed = Number.parseInt(color.replace('#', ''), 16);

  return Number.isFinite(parsed) ? parsed : 0xffffff;
}
