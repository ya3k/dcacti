import React, { useEffect, useState, useCallback } from 'react';
import './App.css';
import { GameShell } from './GameShell';
import { StatusOverlay } from '../ui/components/StatusOverlay';
import { ViewportDebugOverlay } from '../ui/components/ViewportDebugOverlay';
import { GameRuntime } from '../game/runtime/GameRuntime';

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

function getSharedRuntime(): GameRuntime {
  if (sharedRuntime === null) {
    sharedRuntime = new GameRuntime();
  }
  return sharedRuntime;
}

/** Test-only: releases the module-scoped runtime between test cases. */
export function resetSharedRuntime(): void {
  sharedRuntime = null;
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
 * `initialize()` is idempotent and `dispose()` is only called when the runtime
 * was never initialized, so the second mount attaches to the already-connecting
 * runtime and the app never opens a second connection or tears down a live one.
 */
export const App: React.FC = () => {
  const [runtime, setRuntime] = useState<GameRuntime | null>(null);

  useEffect(() => {
    const activeRuntime = getSharedRuntime();

    setRuntime(activeRuntime);

    // Initialize the runtime: connects the transport and binds lifecycle
    // handlers. Idempotent, so the StrictMode remount is a no-op here. Failures
    // are handled inside the runtime and surface as runtime state, so a backend
    // that is down leaves the app usable (task §19).
    void activeRuntime.initialize(BATTLE_HUB_URL);

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