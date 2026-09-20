import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { GameShell } from '../src/app/GameShell';

describe('GameShell', () => {
  it('renders the game shell, canvas mount and overlay layers', () => {
    render(
      <GameShell>
        <div data-testid="hud">HUD</div>
      </GameShell>
    );

    expect(screen.getByTestId('game-shell')).toBeInTheDocument();
    expect(screen.getByTestId('game-shell-canvas')).toBeInTheDocument();
    expect(screen.getByTestId('game-shell-overlay')).toBeInTheDocument();
    expect(screen.getByTestId('hud')).toBeInTheDocument();
  });

  it('keeps the Phaser container inside the shell', () => {
    const { container } = render(<GameShell />);

    const shell = container.querySelector('.game-shell');
    const phaser = container.querySelector('#phaser-container');

    expect(shell).not.toBeNull();
    expect(phaser).not.toBeNull();
    expect(shell?.contains(phaser)).toBe(true);
  });

  it('places the overlay inside the shell, not beside it', () => {
    const { container } = render(
      <GameShell>
        <div data-testid="hud">HUD</div>
      </GameShell>
    );

    const shell = container.querySelector('.game-shell');
    const overlay = container.querySelector('.game-shell__overlay');

    expect(shell?.contains(overlay)).toBe(true);
  });

  it('renders no overlay layer when there are no children', () => {
    const { container } = render(<GameShell />);
    expect(container.querySelector('.game-shell__overlay')).toBeNull();
  });

  it('notifies once the game instance is created', () => {
    const onInit = vi.fn();
    render(<GameShell onGameInitialized={onInit} />);
    expect(onInit).toHaveBeenCalledTimes(1);
  });

  it('uses the shell class that owns full-viewport layout', () => {
    const { container } = render(<GameShell />);
    expect(container.querySelector('.game-shell')).not.toBeNull();
  });
});