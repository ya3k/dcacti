import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import * as Phaser from 'phaser';
import { PhaserGame } from '../src/game/PhaserGame';
import { GAME_WIDTH, GAME_HEIGHT } from '../src/game/GameViewport';
import { RUNTIME_REGISTRY_KEY } from '../src/game/runtime/RuntimeRegistry';

vi.mock('phaser', () => ({
  AUTO: 'AUTO',
  Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
  Game: vi.fn().mockImplementation(() => ({
    destroy: vi.fn(),
  })),
  Scene: class MockScene {},
  Structs: { Size: class MockSize {} },
}));

/** Minimal stand-in for the GameRuntime port; the component only passes it on. */
const fakeRuntime = { name: 'runtime' } as never;

describe('PhaserGame Component', () => {
  beforeEach(() => {
    vi.mocked(Phaser.Game).mockClear();
  });

  it('should render phaser container div', () => {
    const onInit = vi.fn();
    const { container } = render(<PhaserGame runtime={fakeRuntime} onInitialized={onInit} />);

    const phaserDiv = container.querySelector('#phaser-container');
    expect(phaserDiv).not.toBeNull();
    expect(onInit).toHaveBeenCalled();
  });

  it('creates the game with the logical resolution and scale manager', () => {
    render(<PhaserGame runtime={fakeRuntime} />);

    expect(Phaser.Game).toHaveBeenCalledTimes(1);
    const config = vi.mocked(Phaser.Game).mock.calls[0][0] as Record<string, any>;

    expect(config.width).toBe(GAME_WIDTH);
    expect(config.height).toBe(GAME_HEIGHT);
    expect(config.scale.mode).toBe('FIT');
    expect(config.scale.autoCenter).toBe('CENTER_BOTH');
    expect(config.scale.expandParent).toBe(false);
  });

  it('registers the documented scene lifecycle', () => {
    render(<PhaserGame runtime={fakeRuntime} />);

    const config = vi.mocked(Phaser.Game).mock.calls[0][0] as Record<string, any>;
    const sceneKeys = (config.scene as Array<{ name?: string }>).map((s) => s.name);

    expect(sceneKeys).toEqual(['BootScene', 'PreloaderScene', 'BattleScene']);
  });

  it('publishes the runtime to the Phaser registry for scenes', () => {
    render(<PhaserGame runtime={fakeRuntime} />);

    const config = vi.mocked(Phaser.Game).mock.calls[0][0] as Record<string, any>;
    const set = vi.fn();
    config.callbacks.postBoot({ registry: { set } });

    expect(set).toHaveBeenCalledWith(RUNTIME_REGISTRY_KEY, fakeRuntime);
  });

  it('mounts the game into the container element', () => {
    const { container } = render(<PhaserGame runtime={fakeRuntime} />);

    const config = vi.mocked(Phaser.Game).mock.calls[0][0] as Record<string, any>;
    expect(config.parent).toBe(container.querySelector('#phaser-container'));
  });

  it('does not create a second game instance when re-rendered', () => {
    const { rerender } = render(<PhaserGame runtime={fakeRuntime} />);
    rerender(<PhaserGame runtime={fakeRuntime} />);

    expect(Phaser.Game).toHaveBeenCalledTimes(1);
  });

  it('does not recreate the game when only the init callback changes identity', () => {
    const { rerender } = render(<PhaserGame runtime={fakeRuntime} onInitialized={() => {}} />);
    rerender(<PhaserGame runtime={fakeRuntime} onInitialized={() => {}} />);

    expect(Phaser.Game).toHaveBeenCalledTimes(1);
  });

  it('destroys the game instance on unmount', () => {
    const { unmount } = render(<PhaserGame runtime={fakeRuntime} />);

    const instance = vi.mocked(Phaser.Game).mock.results[0].value;
    unmount();

    expect(instance.destroy).toHaveBeenCalledWith(true);
  });

  it('creates a fresh game after a StrictMode-style unmount/remount cycle', () => {
    const first = render(<PhaserGame runtime={fakeRuntime} />);
    const firstInstance = vi.mocked(Phaser.Game).mock.results[0].value;
    first.unmount();

    render(<PhaserGame runtime={fakeRuntime} />);

    // Exactly one live instance at a time: the first was destroyed.
    expect(firstInstance.destroy).toHaveBeenCalledWith(true);
    expect(Phaser.Game).toHaveBeenCalledTimes(2);
  });
});