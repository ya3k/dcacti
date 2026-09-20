import React from 'react';

export interface IntegrationStatus {
  backendApi: 'checking' | 'ok' | 'error';
  signalR: 'disconnected' | 'connecting' | 'connected' | 'error';
  signalRPing: string | null;
  discord: 'checking' | 'inside_discord' | 'outside_discord' | 'error';
  discordMessage: string;
  phaser: 'initializing' | 'running' | 'error';
}

interface StatusOverlayProps {
  status: IntegrationStatus;
  onRetrySignalR?: () => void;
  onPing?: () => void;
}

export const StatusOverlay: React.FC<StatusOverlayProps> = ({
  status,
  onRetrySignalR,
  onPing,
}) => {
  const getBadgeClass = (state: string) => {
    switch (state) {
      case 'ok':
      case 'connected':
      case 'running':
      case 'inside_discord':
        return 'status-badge badge-success';
      case 'outside_discord':
        return 'status-badge badge-neutral';
      case 'checking':
      case 'connecting':
      case 'initializing':
        return 'status-badge badge-warning';
      default:
        return 'status-badge badge-error';
    }
  };

  return (
    <div className="status-overlay-card">
      <div className="status-header">
        <h2>Integration Smoke Test Diagnostics</h2>
        <span className="platform-tag">Discord Activity / Local Dev</span>
      </div>

      <div className="status-body">
        <div className="status-grid">
          <div className="status-item">
            <div className="status-label">Backend API (/health)</div>
            <div className={getBadgeClass(status.backendApi)}>
              {status.backendApi === 'ok' ? 'OK (Healthy)' : status.backendApi.toUpperCase()}
            </div>
          </div>

          <div className="status-item">
            <div className="status-label">SignalR Hub (/hubs/battle)</div>
            <div className="status-row">
              <div className={getBadgeClass(status.signalR)}>
                {status.signalR.toUpperCase()}
              </div>
              {status.signalRPing && (
                <span className="ping-info">{status.signalRPing}</span>
              )}
            </div>
          </div>

          <div className="status-item">
            <div className="status-label">Phaser 4 Engine</div>
            <div className={getBadgeClass(status.phaser)}>
              {status.phaser === 'running' ? 'RUNNING' : status.phaser.toUpperCase()}
            </div>
          </div>

          <div className="status-item">
            <div className="status-label">Discord Activity SDK</div>
            <div className={getBadgeClass(status.discord)}>
              {status.discord === 'inside_discord'
                ? 'CONNECTED (Activity)'
                : status.discord === 'outside_discord'
                ? 'LOCAL DEV (Outside Discord)'
                : status.discord.toUpperCase()}
            </div>
            <div className="status-subtext">{status.discordMessage}</div>
          </div>
        </div>

        <div className="action-row">
          <button
            className="btn btn-primary"
            onClick={onPing}
            disabled={status.signalR !== 'connected'}
          >
            SignalR Ping Test
          </button>
          {status.signalR === 'error' || status.signalR === 'disconnected' ? (
            <button className="btn btn-secondary" onClick={onRetrySignalR}>
              Reconnect SignalR
            </button>
          ) : null}
        </div>
      </div>
    </div>
  );
};
