/**
 * Logical game resolution and viewport layout constants.
 *
 * The game is authored against a fixed logical space (1280 x 720, 16:9) and
 * Phaser's Scale Manager maps that logical space onto the real Discord
 * Activity / browser viewport. All game coordinates are expressed in logical
 * units, never in `window.innerWidth` / `window.innerHeight`.
 *
 * This keeps presentation independent from viewport size: the viewport scales
 * the presentation, it never changes the game's logical board or rules.
 */

/** Logical game width in game pixels. */
export const GAME_WIDTH = 1280;

/** Logical game height in game pixels. */
export const GAME_HEIGHT = 720;

/** Logical aspect ratio (16 / 9). */
export const GAME_ASPECT_RATIO = GAME_WIDTH / GAME_HEIGHT;

/**
 * Safe-area margin, in logical game pixels, reserved inside the logical
 * viewport. Important presentation content should stay inside it so it never
 * sits against the physical viewport edge. No gameplay HUD is implemented yet;
 * this only establishes the boundary for future UI.
 */
export const SAFE_AREA_MARGIN = 24;

/**
 * Smallest viewport, in CSS pixels, the layout is designed to remain usable
 * at. Below this the layout still fits without scrolling — the game simply
 * continues to scale down (see the task's "very small viewports" requirement).
 */
export const MIN_SUPPORTED_VIEWPORT_WIDTH = 320;
export const MIN_SUPPORTED_VIEWPORT_HEIGHT = 240;

/** Computed safe-area rectangle inside the logical game space. */
export const SAFE_AREA = {
  x: SAFE_AREA_MARGIN,
  y: SAFE_AREA_MARGIN,
  width: GAME_WIDTH - SAFE_AREA_MARGIN * 2,
  height: GAME_HEIGHT - SAFE_AREA_MARGIN * 2,
} as const;

/**
 * Resolves the largest size, in CSS pixels, that preserves the logical aspect
 * ratio inside the given viewport. Used for reporting/debug; Phaser's FIT
 * scale mode performs the authoritative calculation at render time.
 */
export function fitWithinViewport(
  viewportWidth: number,
  viewportHeight: number
): { width: number; height: number; scale: number } {
  if (viewportWidth <= 0 || viewportHeight <= 0) {
    return { width: 0, height: 0, scale: 0 };
  }

  const scale = Math.min(viewportWidth / GAME_WIDTH, viewportHeight / GAME_HEIGHT);

  return {
    width: GAME_WIDTH * scale,
    height: GAME_HEIGHT * scale,
    scale,
  };
}