import React from 'react';
import { useGameRuntimeState } from '../../game/runtime/GameRuntimeContext';
import { ApplicationSession } from '../../services/api/ApplicationSession';
import type { ConnectionStatus, GameRuntimeState } from '../../state/GameRuntimeState';

/**
 * Minimal technical runtime status overlay.
 *
 * This is debugging/infrastructure UI: it reports whether the
 * backend, SignalR transport, Phaser engine and runtime are
 * available. It is NOT the game HUD and shows no gameplay state.
 *
 * It is absolutely positioned inside the shell overlay and contributes nothing
 * to document size, so it can never change the game viewport dimensions or
 * introduce scrollbars.
 */
export const StatusOverlay: React.FC = () => {
  const runtime = useGameRuntimeState();

  return (
    <div className="status-overlay-card" data-testid="runtime-status-overlay">
      <div className="status-header">
        <h2>Runtime Status</h2>
        <span className="platform-tag">Web Standalone</span>
      </div>

      <div className="status-body">
        <div className="status-grid">
          <StatusItem label="Account" value={describeAccount()} state="neutral" />
          <StatusItem
            label="Backend"
            value={runtime.connection === 'connected' ? 'Connected' : 'Unknown'}
            state={runtime.connection === 'connected' ? 'ok' : 'warning'}
          />
          <StatusItem
            label="SignalR"
            value={describeConnection(runtime.connection)}
            state={connectionTone(runtime.connection)}
          />
          <StatusItem
            label="Phaser"
            value={describeEngine(runtime.engine)}
            state={runtime.engine === 'running' ? 'ok' : 'warning'}
          />
          <StatusItem
            label="Runtime"
            value={describeRuntime(runtime)}
            state={runtime.runtime === 'ready' ? 'ok' : 'warning'}
          />
        </div>

        {/*
          Synchronization is reported separately from connection: a connected
          runtime has no synchronized battle state until battle resolution
          exists (SIGNALR_PROTOCOL.md §5–§6, ADR-008).
        */}
        <div className="status-item">
          <div className="status-label">Server Sync</div>
          <div className="status-subtext">{describeSync(runtime)}</div>
        </div>

        {runtime.connectionId ? (
          <div className="status-item">
            <div className="status-label">Connection Id</div>
            <div className="status-subtext">{runtime.connectionId}</div>
          </div>
        ) : null}

        {runtime.lastError ? (
          <div className="status-item">
            <div className="status-label">Last Error</div>
            <div className="status-subtext">{runtime.lastError}</div>
          </div>
        ) : null}
      </div>
    </div>
  );
};

type Tone = 'ok' | 'warning' | 'error' | 'neutral';

interface StatusItemProps {
  label: string;
  value: string;
  state: Tone;
}

const StatusItem: React.FC<StatusItemProps> = ({ label, value, state }) => (
  <div className="status-item">
    <div className="status-label">{label}</div>
    <div className={`status-badge badge-${state === 'ok' ? 'success' : state === 'warning' ? 'warning' : state === 'error' ? 'error' : 'neutral'}`}>
      {value}
    </div>
  </div>
);

function describeAccount(): string {
  const username = ApplicationSession.getInstance().getUsername();
  return username ? username : 'Unauthenticated';
}

function describeConnection(connection: ConnectionStatus): string {
  switch (connection) {
    case 'connected':
      return 'Connected';
    case 'connecting':
      return 'Connecting';
    case 'reconnecting':
      return 'Reconnecting';
    case 'error':
      return 'Error';
    default:
      return 'Disconnected';
  }
}

function connectionTone(connection: ConnectionStatus): Tone {
  switch (connection) {
    case 'connected':
      return 'ok';
    case 'connecting':
    case 'reconnecting':
      return 'warning';
    case 'error':
      return 'error';
    default:
      return 'warning';
  }
}

function describeEngine(engine: GameRuntimeState['engine']): string {
  switch (engine) {
    case 'running':
      return 'Running';
    case 'stopped':
      return 'Stopped';
    case 'error':
      return 'Error';
    default:
      return 'Initializing';
  }
}

function describeRuntime(runtime: GameRuntimeState): string {
  switch (runtime.runtime) {
    case 'ready':
      return 'Ready';
    case 'suspended':
      return 'Waiting for connection';
    case 'error':
      return 'Unavailable';
    default:
      return 'Initializing';
  }
}

function describeSync(runtime: GameRuntimeState): string {
  switch (runtime.sync) {
    case 'synchronized':
      return 'Synchronized with server';
    case 'awaiting_battle':
      return 'Connected — no active battle';
    default:
      return 'Not synchronized';
  }
}

export default StatusOverlay;