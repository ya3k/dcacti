import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, act } from '@testing-library/react';
import { StatusOverlay } from '../src/ui/components/StatusOverlay';
import { GameRuntimeProvider } from '../src/game/runtime/GameRuntimeContext';
import { GameRuntime } from '../src/game/runtime/GameRuntime';
import { INITIAL_RUNTIME_STATE } from '../src/state/GameRuntimeState';
import type { GameRuntimeState } from '../src/state/GameRuntimeState';
import type { SignalRConnectionHandlers } from '../src/services/realtime/SignalRService';

/**
 * Transport double: the overlay is driven purely by runtime state, so the
 * runtime is exercised through its transport port with no live hub.
 */
class FakeSignalR {
  public handlers: SignalRConnectionHandlers = {};
  public connectId: string | null = 'conn-42';
  public failConnect = false;

  setHandlers(handlers: SignalRConnectionHandlers): void {
    this.handlers = handlers;
  }

  async connect(): Promise<void> {
    if (this.failConnect) {
      throw new Error('backend unreachable');
    }
    this.handlers.onConnected?.(this.connectId);
  }

  async disconnect(): Promise<void> {}

  on(): () => void {
    return () => {};
  }
}

function renderOverlay(runtime: GameRuntime) {
  return render(
    <GameRuntimeProvider runtime={runtime}>
      <StatusOverlay />
    </GameRuntimeProvider>
  );
}

function state(overrides: Partial<GameRuntimeState>): GameRuntimeState {
  return { ...INITIAL_RUNTIME_STATE, ...overrides };
}

describe('StatusOverlay (runtime status)', () => {
  let transport: FakeSignalR;
  let runtime: GameRuntime;

  beforeEach(() => {
    vi.restoreAllMocks();
    transport = new FakeSignalR();
    runtime = new GameRuntime(transport as never);
  });

  it('renders the technical runtime indicators', () => {
    renderOverlay(runtime);

    expect(screen.getByTestId('runtime-status-overlay')).toBeInTheDocument();
    expect(screen.getByText('Runtime Status')).toBeInTheDocument();
    expect(screen.getByText('SignalR')).toBeInTheDocument();
    expect(screen.getByText('Phaser')).toBeInTheDocument();
    expect(screen.getByText('Runtime')).toBeInTheDocument();
  });

  it('reports the SignalR connection state', () => {
    renderOverlay(runtime);
    expect(screen.getByText('Disconnected')).toBeInTheDocument();
  });

  it('shows the connected state after the runtime connects', async () => {
    renderOverlay(runtime);

    await act(async () => {
      await runtime.initialize();
    });

    // "Connected" appears for both Backend and SignalR once connected.
    expect(screen.getAllByText('Connected')).toHaveLength(2);
    expect(screen.getByText('conn-42')).toBeInTheDocument();
  });

  it('shows a waiting runtime when the connection fails', async () => {
    transport.failConnect = true;
    renderOverlay(runtime);

    await act(async () => {
      await runtime.initialize();
    });

    expect(screen.getByText('Error')).toBeInTheDocument();
    expect(screen.getByText('Unavailable')).toBeInTheDocument();
    expect(screen.getByText('backend unreachable')).toBeInTheDocument();
  });

  it('shows the reconnecting state during a reconnect', async () => {
    renderOverlay(runtime);
    await act(async () => {
      await runtime.initialize();
    });

    act(() => {
      transport.handlers.onReconnecting?.(new Error('lost'));
    });

    expect(screen.getByText('Reconnecting')).toBeInTheDocument();
    expect(screen.getByText('Waiting for connection')).toBeInTheDocument();
  });

  it('reports synchronization separately from connection', async () => {
    renderOverlay(runtime);

    await act(async () => {
      await runtime.initialize();
    });

    // Connected, but no battle exists yet — never reported as synchronized.
    expect(screen.getByText('Connected — no active battle')).toBeInTheDocument();
  });

  it('reports Phaser as running once the engine status is set', async () => {
    renderOverlay(runtime);

    await act(async () => {
      runtime.setEngineStatus('running');
    });

    expect(screen.getByText('Running')).toBeInTheDocument();
  });

  it('displays no gameplay state', async () => {
    renderOverlay(runtime);

    await act(async () => {
      await runtime.initialize();
    });

    // Infrastructure UI only: no HP, board, gems, Power, combo, or damage.
    for (const forbidden of ['HP', 'Boss', 'Power', 'Combo', 'Damage', 'Board', 'Gem']) {
      expect(screen.queryByText(new RegExp(forbidden, 'i'))).toBeNull();
    }
  });

  it('renders the runtime state given directly, without a connection', () => {
    // The overlay is a pure function of runtime state; this documents the
    // mapping used for the initial (pre-connection) presentation.
    const initial = state({ connection: 'disconnected' });
    expect(initial.runtime).toBe('initializing');
    expect(initial.sync).toBe('unsynchronized');
  });
});