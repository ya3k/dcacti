import { describe, it, expect } from 'vitest';
import {
  GAME_WIDTH,
  GAME_HEIGHT,
  GAME_ASPECT_RATIO,
  SAFE_AREA,
  SAFE_AREA_MARGIN,
  fitWithinViewport,
} from '../src/game/GameViewport';
import { createGameConfig } from '../src/game/GameConfig';

describe('Logical game resolution', () => {
  it('is 1280 x 720 (16:9)', () => {
    expect(GAME_WIDTH).toBe(1280);
    expect(GAME_HEIGHT).toBe(720);
    expect(GAME_ASPECT_RATIO).toBeCloseTo(16 / 9, 10);
  });

  it('defines a safe area inset from every edge', () => {
    expect(SAFE_AREA.x).toBe(SAFE_AREA_MARGIN);
    expect(SAFE_AREA.y).toBe(SAFE_AREA_MARGIN);
    expect(SAFE_AREA.width).toBe(GAME_WIDTH - SAFE_AREA_MARGIN * 2);
    expect(SAFE_AREA.height).toBe(GAME_HEIGHT - SAFE_AREA_MARGIN * 2);

    // The safe area stays strictly inside the logical viewport.
    expect(SAFE_AREA.x).toBeGreaterThan(0);
    expect(SAFE_AREA.y).toBeGreaterThan(0);
    expect(SAFE_AREA.x + SAFE_AREA.width).toBeLessThan(GAME_WIDTH);
    expect(SAFE_AREA.y + SAFE_AREA.height).toBeLessThan(GAME_HEIGHT);
  });
});

describe('fitWithinViewport', () => {
  const viewports: Array<[number, number]> = [
    [1920, 1080],
    [1600, 900],
    [1366, 768],
    [1280, 720],
    [1024, 768],
    [900, 700],
    [800, 600],
    [600, 900],
  ];

  it.each(viewports)('fits %ix%i inside the viewport without distortion', (w, h) => {
    const fit = fitWithinViewport(w, h);

    // Never larger than the viewport — this is what prevents scrollbars.
    expect(fit.width).toBeLessThanOrEqual(w + 1e-9);
    expect(fit.height).toBeLessThanOrEqual(h + 1e-9);

    // Aspect ratio is preserved (no stretching).
    expect(fit.width / fit.height).toBeCloseTo(GAME_ASPECT_RATIO, 10);
  });

  it('never upscales beyond the viewport on an undersized window', () => {
    const fit = fitWithinViewport(100, 100);
    expect(fit.width).toBeLessThanOrEqual(100);
    expect(fit.height).toBeLessThanOrEqual(100);
  });

  it('is safe for degenerate viewport sizes', () => {
    expect(fitWithinViewport(0, 0)).toEqual({ width: 0, height: 0, scale: 0 });
    expect(fitWithinViewport(-10, 100).scale).toBe(0);
  });
});

describe('Phaser game configuration', () => {
  it('uses the logical game resolution', () => {
    const config = createGameConfig(document.createElement('div'));
    expect(config.width).toBe(GAME_WIDTH);
    expect(config.height).toBe(GAME_HEIGHT);
  });

  it('uses the Scale Manager with FIT and CENTER_BOTH', () => {
    const config = createGameConfig(document.createElement('div'));

    expect(config.scale).toBeDefined();
    expect(config.scale?.mode).toBe('FIT');
    expect(config.scale?.autoCenter).toBe('CENTER_BOTH');
  });

  it('does not let Phaser resize the parent or document body', () => {
    const config = createGameConfig(document.createElement('div'));
    expect(config.scale?.expandParent).toBe(false);
  });

  it('mounts into the provided parent element', () => {
    const parent = document.createElement('div');
    const config = createGameConfig(parent);
    expect(config.parent).toBe(parent);
  });

  it('rounds canvas sizes to whole pixels to avoid fractional-pixel overflow', () => {
    const config = createGameConfig(document.createElement('div'));
    expect(config.scale?.autoRound).toBe(true);
  });

  it('does not set an unsupported resolution option (Phaser 4 has none)', () => {
    const config = createGameConfig(document.createElement('div')) as Record<string, unknown>;
    expect(config.resolution).toBeUndefined();
    expect((config.scale as Record<string, unknown>).resolution).toBeUndefined();
  });
});