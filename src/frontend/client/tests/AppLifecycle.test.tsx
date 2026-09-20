import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, act } from '@testing-library/react';
import React from 'react';
import { App, resetSharedRuntime } from '../src/app/App';
import { GameRuntime } from '../src/game/runtime/GameRuntime';
import { SignalRService } from '../src/services/realtime/SignalRService';

vi.mock('phaser', () => {
  // Mirrors tests/setup.ts: `new Phaser.Game(config)` must yield an object with
  // a `destroy` method, because PhaserGame calls it during cleanup.
  const Game = vi.fn(function MockGame(this: { destroy: () => void }) {
    this.destroy = vi.fn();
  });

  return {
    AUTO: 'AUTO',
    Scale: { FIT: 'FIT', CENTER_BOTH: 'CENTER_BOTH' },
    Game,
    Scene: class MockScene {},
    Structs: { Size: class MockSize {} },
    Loader: { Events: { COMPLETE: 'complete' } },
  };
});

/**
 * Application-level lifecycle tests.
 *
 * These guard the task's cleanup requirements (task §20): React must not create
 * duplicate SignalR connections when StrictMode double-invokes effects, and must
 * not create duplicate Phaser instances.
 */
describe('App runtime lifecycle', () => {
  let connectSpy: ReturnType<typeof vi.fn>;
  let disconnectSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    resetSharedRuntime();

    // The transport is replaced at the service port so the application
    // lifecycle can be observed without a live hub. `on()` succeeds because the
    // runtime only subscribes after a successful connect.
    const service = SignalRService.getInstance();
    connectSpy = vi
      .spyOn(service, 'connect')
      .mockResolvedValue(undefined) as unknown as ReturnType<typeof vi.fn>;
    disconnectSpy = vi
      .spyOn(service, 'disconnect')
      .mockResolvedValue(undefined) as unknown as ReturnType<typeof vi.fn>;
    vi.spyOn(service, 'on').mockReturnValue(() => {});
    vi.spyOn(service, 'setHandlers').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
    resetSharedRuntime();
  });

  it('renders the application shell', async () => {
    const { container } = render(<App />);

    await act(async () => {
      await Promise.resolve();
    });

    expect(container.querySelector('.game-shell')).not.toBeNull();
  });

  it('initializes the runtime exactly once per document', async () => {
    render(<App />);

    await act(async () => {
      await Promise.resolve();
    });

    expect(connectSpy).toHaveBeenCalledTimes(1);
  });

  it('does not open a duplicate connection under StrictMode remounting', async () => {
    const { unmount } = render(
      <React.StrictMode>
        <App />
      </React.StrictMode>
    );

    await act(async () => {
      await Promise.resolve();
    });

    unmount();

    await act(async () => {
      await Promise.resolve();
    });

    // A module-scoped runtime plus an idempotent initialize() means the
    // StrictMode double-invoke cannot produce a second connection.
    expect(connectSpy).toHaveBeenCalledTimes(1);
  });

  it('does not tear down the connection on a StrictMode effect cleanup', async () => {
    const { unmount } = render(
      <React.StrictMode>
        <App />
      </React.StrictMode>
    );

    await act(async () => {
      await Promise.resolve();
    });

    // The runtime intentionally outlives an individual effect run, because the
    // transport it owns is a process-wide singleton.
    expect(disconnectSpy).not.toHaveBeenCalled();

    unmount();
  });

  it('creates the runtime as a GameRuntime instance', () => {
    const runtime = new GameRuntime();
    expect(runtime).toBeInstanceOf(GameRuntime);
    expect(runtime.isInitialized()).toBe(false);
  });
});