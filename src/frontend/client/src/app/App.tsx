import React, { useEffect, useState, useCallback } from 'react';
import './App.css';
import { GameShell } from './GameShell';
import { StatusOverlay } from '../ui/components/StatusOverlay';
import { ViewportDebugOverlay } from '../ui/components/ViewportDebugOverlay';
import { GameRuntime } from '../game/runtime/GameRuntime';
import { DiscordService } from '../services/discord/DiscordService';
import { ApiService } from '../services/api/ApiService';

/** Default hub path (SIGNALR_PROTOCOL.md §1; mapped in vite.config.ts). */
const BATTLE_HUB_URL = '/hubs/battle';

/**
 * The process-wide runtime.
 *
 * `SignalRService` is itself a singleton, so there is exactly one connection per
 * document regardless. Keeping the runtime at module scope makes the ownership
 * explicit: React attaches to and detaches from one long-lived runtime rather
 * than creating a runtime per effect run, which is what produces the StrictMode
 * mount → cleanup → mount race against the shared transport.
 */
let sharedRuntime: GameRuntime | null = null;
let bootstrapPromise: Promise<void> | null = null;

function getSharedRuntime(): GameRuntime {
  if (sharedRuntime === null) {
    sharedRuntime = new GameRuntime();
  }
  return sharedRuntime;
}

/**
 * Establishes the authenticated application session before connecting SignalR
 * (TDD.md §2.1 item 4, API_CONTRACTS.md §2, SIGNALR_PROTOCOL.md §1).
 *
 * Sequence:
 *   DiscordService.initialize()
 *           ↓
 *   DiscordService.getAuthorizationCode()
 *           ↓
 *   ApiService.authenticateDiscord(code)
 *           ↓
 *   ApplicationSession.establish(...) (called by ApiService.authenticateDiscord)
 *           ↓
 *   GameRuntime.setSessionStatus('authenticated')
 *           ↓
 *   GameRuntime.initialize(BATTLE_HUB_URL)
 *           ↓
 *   SignalR connection
 */
async function bootstrapApplication(runtime: GameRuntime): Promise<void> {
  runtime.setSessionStatus('authenticating');

  try {
    const discordContext = await DiscordService.getInstance().initialize();
    if (!discordContext.isAvailable) {
      runtime.setSessionStatus('error');
      return;
    }

    const code = await DiscordService.getInstance().getAuthorizationCode();
    if (!code) {
      runtime.setSessionStatus('error');
      return;
    }

    await ApiService.getInstance().authenticateDiscord(code);
    runtime.setSessionStatus('authenticated');

    await runtime.initialize(BATTLE_HUB_URL);
  } catch {
    runtime.setSessionStatus('error');
  }
}

/** Test-only: releases the module-scoped runtime and bootstrap promise between test cases. */
export function resetSharedRuntime(): void {
  sharedRuntime = null;
  bootstrapPromise = null;
}

/**
 * Application shell.
 *
 * Owns the `GameRuntime` lifecycle and the Discord Activity SDK integration
 * boundary. React owns the shell, overlays and platform integration; Phaser owns
 * the game surface (TDD.md §2.1).
 *
 * React does not connect to SignalR itself and does not read gameplay state:
 * the runtime is the single authority for connection state and forwards
 * server-authoritative events to the presentation layer (task §15–§17).
 *
 * StrictMode (task §20). Effects run mount → cleanup → mount in development.
 * The bootstrap sequence is executed once and `initialize()` is idempotent,
 * so the second mount attaches to the already-authenticating/connecting runtime
 * and the app never opens a duplicate connection or duplicates authentication.
 */
export const App: React.FC = () => {
  const [runtime, setRuntime] = useState<GameRuntime | null>(null);

  useEffect(() => {
    const activeRuntime = getSharedRuntime();

    setRuntime(activeRuntime);

    if (!bootstrapPromise) {
      bootstrapPromise = bootstrapApplication(activeRuntime);
    }

    return () => {
      // The runtime outlives an individual effect run: `SignalRService` is a
      // singleton and the runtime is module-scoped, so disposal here would tear
      // down the connection the next mount reuses. It is disposed on page
      // teardown instead (see disposeSharedRuntime).
      setRuntime((current) => (current === activeRuntime ? null : current));
    };
  }, []);

  // Release the connection when the document goes away.
  useEffect(() => {
    const onPageHide = () => {
      void sharedRuntime?.dispose();
    };

    window.addEventListener('pagehide', onPageHide);
    return () => window.removeEventListener('pagehide', onPageHide);
  }, []);

  const handlePhaserInit = useCallback(() => {
    // The Phaser engine is running; reflect it on the shared runtime.
    runtime?.setEngineStatus('running');
  }, [runtime]);

  // The shell and the Phaser canvas only mount once a runtime exists, so the
  // game is never created without the runtime it coordinates through.
  if (runtime === null) {
    return <div className="game-shell" data-testid="game-shell-bootstrapping" />;
  }

  return (
    <GameShell runtime={runtime} onGameInitialized={handlePhaserInit}>
      <StatusOverlay />
      {import.meta.env.DEV ? <ViewportDebugOverlay /> : null}
    </GameShell>
  );
};

export default App;