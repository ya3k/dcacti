import { describe, it, expect, afterEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { ViewportDebugOverlay } from '../src/ui/components/ViewportDebugOverlay';
import { GAME_WIDTH, GAME_HEIGHT } from '../src/game/GameViewport';

/** jsdom defaults to 1024x768; override and notify listeners. */
function setViewport(width: number, height: number) {
  window.innerWidth = width;
  window.innerHeight = height;
  act(() => {
    window.dispatchEvent(new Event('resize'));
  });
}

describe('ViewportDebugOverlay (development diagnostics)', () => {
  const originalWidth = window.innerWidth;
  const originalHeight = window.innerHeight;

  afterEach(() => {
    setViewport(originalWidth, originalHeight);
  });

  it('reports the viewport and the logical game resolution', () => {
    setViewport(1280, 720);
    render(<ViewportDebugOverlay />);

    const overlay = screen.getByTestId('viewport-debug');
    expect(overlay.textContent).toContain('Viewport: 1280 x 720');
    expect(overlay.textContent).toContain(`Game:     ${GAME_WIDTH} x ${GAME_HEIGHT}`);
  });

  it('updates when the viewport changes', () => {
    setViewport(1920, 1080);
    render(<ViewportDebugOverlay />);

    expect(screen.getByTestId('viewport-debug').textContent).toContain('Viewport: 1920 x 1080');

    setViewport(800, 600);
    expect(screen.getByTestId('viewport-debug').textContent).toContain('Viewport: 800 x 600');
    // 800x600 is taller than 16:9, so width is the limiting axis: 800/1280 = 0.63.
    expect(screen.getByTestId('viewport-debug').textContent).toContain('Scale:    0.63');
  });

  it('reports a scale that keeps the game inside the viewport', () => {
    setViewport(1920, 1080);
    render(<ViewportDebugOverlay />);

    // 1920x1080 is exactly 16:9, so the game fills it at scale 1.50.
    expect(screen.getByTestId('viewport-debug').textContent).toContain('Scale:    1.50');
    expect(screen.getByTestId('viewport-debug').textContent).toContain('Canvas:   1920 x 1080');
  });
});