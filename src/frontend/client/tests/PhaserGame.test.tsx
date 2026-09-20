import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import * as Phaser from 'phaser';
import { PhaserGame } from '../src/game/PhaserGame';
import { GAME_WIDTH, GAME_HEIGHT } from '../src/game/GameViewport';

vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({
    destroy: vi.fn(),
  })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
}));

describe('PhaserGame Component', () => {
  beforeEach(() => {
    vi.mocked(Phaser.Game).mockClear();
  });

  it('should render phaser container div', () => {
    const onInit = vi.fn();
    const { container } = render(<PhaserGame onInitialized={onInit} />);

    const phaserDiv = container.querySelector('#phaser-container');
    expect(phaserDiv).not.toBeNull();
    expect(onInit).toHaveBeenCalled();
  });

  it('creates the game with the logical resolution and scale manager', () => {
    render(<PhaserGame />);

    expect(Phaser.Game).toHaveBeenCalledTimes(1);
    const config = vi.mocked(Phaser.Game).mock.calls[0][0] as Record<string, any>;

    expect(config.width).toBe(GAME_WIDTH);
    expect(config.height).toBe(GAME_HEIGHT);
    expect(config.scale.mode).toBe('FIT');
    expect(config.scale.autoCenter).toBe('CENTER_BOTH');
    expect(config.scale.expandParent).toBe(false);
  });

  it('mounts the game into the container element', () => {
    const { container } = render(<PhaserGame />);

    const config = vi.mocked(Phaser.Game).mock.calls[0][0] as Record<string, any>;
    expect(config.parent).toBe(container.querySelector('#phaser-container'));
  });

  it('does not create a second game instance when re-rendered', () => {
    const { rerender } = render(<PhaserGame />);
    rerender(<PhaserGame />);

    expect(Phaser.Game).toHaveBeenCalledTimes(1);
  });

  it('destroys the game instance on unmount', () => {
    const { unmount } = render(<PhaserGame />);

    const instance = vi.mocked(Phaser.Game).mock.results[0].value;
    unmount();

    expect(instance.destroy).toHaveBeenCalledWith(true);
  });
});