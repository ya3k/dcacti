import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusOverlay, IntegrationStatus } from '../src/ui/components/StatusOverlay';

describe('StatusOverlay Component', () => {
  it('should render all diagnostic indicators with correct state', () => {
    const status: IntegrationStatus = {
      backendApi: 'ok',
      signalR: 'connected',
      signalRPing: 'Ping Ack: OK (12ms)',
      discord: 'outside_discord',
      discordMessage: 'Running in normal browser outside Discord Activity.',
      phaser: 'running',
    };

    render(<StatusOverlay status={status} />);

    expect(screen.getByText('Integration Smoke Test Diagnostics')).toBeInTheDocument();
    expect(screen.getByText('OK (Healthy)')).toBeInTheDocument();
    expect(screen.getByText('CONNECTED')).toBeInTheDocument();
    expect(screen.getByText('Ping Ack: OK (12ms)')).toBeInTheDocument();
    expect(screen.getByText('RUNNING')).toBeInTheDocument();
    expect(screen.getByText('LOCAL DEV (Outside Discord)')).toBeInTheDocument();
    expect(screen.getByText('Running in normal browser outside Discord Activity.')).toBeInTheDocument();
  });
});
