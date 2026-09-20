import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { GameShell } from '../src/app/GameShell';
import { GameRuntime } from '../src/game/runtime/GameRuntime';

vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({
    destroy: vi.fn(),
  })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
}));

/** Real runtime instance; its transport is never connected in these tests. */
function createRuntime(): GameRuntime {
  return new GameRuntime();
}

describe('GameShell', () => {
  let runtime: GameRuntime;

  beforeEach(() => {
    runtime = createRuntime();
  });

  it('renders the game shell, canvas mount and overlay layers', () => {
    render(
      <GameShell runtime={runtime}>
        <div data-testid="hud">HUD</div>
      </GameShell>
    );

    expect(screen.getByTestId('game-shell')).toBeInTheDocument();
    expect(screen.getByTestId('game-shell-canvas')).toBeInTheDocument();
    expect(screen.getByTestId('game-shell-overlay')).toBeInTheDocument();
    expect(screen.getByTestId('hud')).toBeInTheDocument();
  });

  it('keeps the Phaser container inside the shell', () => {
    const { container } = render(<GameShell runtime={runtime} />);

    const shell = container.querySelector('.game-shell');
    const phaser = container.querySelector('#phaser-container');

    expect(shell).not.toBeNull();
    expect(phaser).not.toBeNull();
    expect(shell?.contains(phaser)).toBe(true);
  });

  it('places the overlay inside the shell, not beside it', () => {
    const { container } = render(
      <GameShell runtime={runtime}>
        <div data-testid="hud">HUD</div>
      </GameShell>
    );

    const shell = container.querySelector('.game-shell');
    const overlay = container.querySelector('.game-shell__overlay');

    expect(shell?.contains(overlay)).toBe(true);
  });

  it('renders no overlay layer when there are no children', () => {
    const { container } = render(<GameShell runtime={runtime} />);
    expect(container.querySelector('.game-shell__overlay')).toBeNull();
  });

  it('notifies once the game instance is created', () => {
    const onInit = vi.fn();
    render(<GameShell runtime={runtime} onGameInitialized={onInit} />);
    expect(onInit).toHaveBeenCalledTimes(1);
  });

  it('uses the shell class that owns full-viewport layout', () => {
    const { container } = render(<GameShell runtime={runtime} />);
    expect(container.querySelector('.game-shell')).not.toBeNull();
  });
});