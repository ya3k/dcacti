import React, { useEffect, useState, useCallback } from 'react';
import './App.css';
import { ApiService } from '../services/api/ApiService';
import { SignalRService } from '../services/realtime/SignalRService';
import { DiscordService } from '../services/discord/DiscordService';
import { GameShell } from './GameShell';
import { StatusOverlay, IntegrationStatus } from '../ui/components/StatusOverlay';
import { ViewportDebugOverlay } from '../ui/components/ViewportDebugOverlay';

export const App: React.FC = () => {
  const [status, setStatus] = useState<IntegrationStatus>({
    backendApi: 'checking',
    signalR: 'connecting',
    signalRPing: null,
    discord: 'checking',
    discordMessage: 'Initializing Discord SDK boundary...',
    phaser: 'initializing',
  });

  const checkBackend = useCallback(async () => {
    const apiService = ApiService.getInstance();
    const isHealthy = await apiService.checkHealth();
    setStatus((prev) => ({
      ...prev,
      backendApi: isHealthy ? 'ok' : 'error',
    }));
  }, []);

  const connectSignalR = useCallback(async () => {
    const signalR = SignalRService.getInstance();
    try {
      setStatus((prev) => ({ ...prev, signalR: 'connecting' }));
      await signalR.connect('/hubs/battle');
      setStatus((prev) => ({ ...prev, signalR: 'connected' }));

      // Run initial smoke-test ping
      const startTime = performance.now();
      const pingRes = await signalR.ping('bootstrap_smoke_ping');
      const durationMs = Math.round(performance.now() - startTime);

      setStatus((prev) => ({
        ...prev,
        signalRPing: `Ping Ack: ${pingRes.accepted ? 'OK' : 'Rejected'} (${durationMs}ms)`,
      }));
    } catch {
      setStatus((prev) => ({
        ...prev,
        signalR: 'error',
        signalRPing: 'SignalR connection failed',
      }));
    }
  }, []);

  const handlePing = async () => {
    const signalR = SignalRService.getInstance();
    try {
      const startTime = performance.now();
      const pingRes = await signalR.ping(`manual_${Date.now()}`);
      const durationMs = Math.round(performance.now() - startTime);
      setStatus((prev) => ({
        ...prev,
        signalRPing: `Ping Ack: ${pingRes.accepted ? 'OK' : 'Rejected'} (${durationMs}ms)`,
      }));
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      setStatus((prev) => ({
        ...prev,
        signalRPing: `Ping Error: ${message}`,
      }));
    }
  };

  const initDiscord = useCallback(async () => {
    const discord = DiscordService.getInstance();
    const ctx = await discord.initialize();
    if (ctx.isAvailable) {
      setStatus((prev) => ({
        ...prev,
        discord: 'inside_discord',
        discordMessage: 'Embedded App SDK connected inside Discord Activity iframe.',
      }));
    } else {
      setStatus((prev) => ({
        ...prev,
        discord: 'outside_discord',
        discordMessage: ctx.error || 'Running in normal browser outside Discord Activity.',
      }));
    }
  }, []);

  useEffect(() => {
    checkBackend();
    connectSignalR();
    initDiscord();

    return () => {
      SignalRService.getInstance().disconnect().catch(() => {});
    };
  }, [checkBackend, connectSignalR, initDiscord]);

  const handlePhaserInit = useCallback(() => {
    setStatus((prev) => ({ ...prev, phaser: 'running' }));
  }, []);

  return (
    <GameShell onGameInitialized={handlePhaserInit}>
      <StatusOverlay status={status} onRetrySignalR={connectSignalR} onPing={handlePing} />
      {import.meta.env.DEV ? <ViewportDebugOverlay /> : null}
    </GameShell>
  );
};

export default App;