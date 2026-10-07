import React, { useEffect, useState, useCallback } from 'react';
import './App.css';
import { GameShell } from './GameShell';
import { StatusOverlay } from '../ui/components/StatusOverlay';
import { ViewportDebugOverlay } from '../ui/components/ViewportDebugOverlay';
import { AuthScreen } from '../ui/components/AuthScreen';
import { GameRuntime } from '../game/runtime/GameRuntime';
import { ApplicationSession } from '../services/api/ApplicationSession';
import type { SessionStatus } from '../state/GameRuntimeState';

/** Default hub path (SIGNALR_PROTOCOL.md §1; mapped in vite.config.ts). */
export const BATTLE_HUB_URL = '/hubs/battle';

/**
 * The process-wide runtime.
 *
 * `SignalRService` is itself a singleton, so there is exactly one connection per
 * document regardless. Keeping the runtime at module scope makes the ownership
 * explicit: React attaches to and detaches from one long-lived runtime rather
 * than creating a runtime per effect run.
 */
let sharedRuntime: GameRuntime | null = null;
let bootstrapPromise: Promise<void> | null = null;

export function getSharedRuntime(): GameRuntime {
  if (sharedRuntime === null) {
    sharedRuntime = new GameRuntime();
  }
  return sharedRuntime;
}

/**
 * Checks for an existing session on startup. If found, connects SignalR.
 * Otherwise transitions session status to unauthenticated so the login/register
 * screen is displayed (ADR-020).
 */
export async function bootstrapApplication(runtime: GameRuntime): Promise<void> {
  const session = ApplicationSession.getInstance();
  const restored = session.restoreFromStorage();

  if (restored) {
    runtime.setSessionStatus('authenticated');
    try {
      await runtime.initialize(BATTLE_HUB_URL);
    } catch {
      // Hub connect error or expired token: clear session and ask to re-login
      session.clear();
      runtime.setSessionStatus('unauthenticated');
    }
  } else {
    runtime.setSessionStatus('unauthenticated');
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
 * Owns the `GameRuntime` lifecycle and the authentication state.
 * When unauthenticated, displays the AuthScreen.
 * Once authenticated, displays the GameShell and starts Phaser.
 */
export const App: React.FC = () => {
  const [runtime, setRuntime] = useState<GameRuntime | null>(null);
  const [sessionStatus, setSessionStatus] = useState<SessionStatus>('authenticating');

  useEffect(() => {
    const activeRuntime = getSharedRuntime();
    setRuntime(activeRuntime);
    setSessionStatus(activeRuntime.getState().session);

    const unsubscribe = activeRuntime.onRuntimeEvent((event) => {
      setSessionStatus(event.state.session);
    });

    if (!bootstrapPromise) {
      bootstrapPromise = bootstrapApplication(activeRuntime);
    }

    return () => {
      unsubscribe();
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

  const handleAuthenticated = useCallback(async () => {
    const activeRuntime = getSharedRuntime();
    activeRuntime.setSessionStatus('authenticated');
    try {
      await activeRuntime.initialize(BATTLE_HUB_URL);
    } catch {
      activeRuntime.setSessionStatus('error');
    }
  }, []);

  const handlePhaserInit = useCallback(() => {
    runtime?.setEngineStatus('running');
  }, [runtime]);

  if (runtime === null) {
    return <div className="game-shell" data-testid="game-shell-bootstrapping" />;
  }

  if (sessionStatus !== 'authenticated') {
    return (
      <div className="game-shell">
        <AuthScreen onAuthenticated={handleAuthenticated} />
      </div>
    );
  }

  return (
    <GameShell runtime={runtime} onGameInitialized={handlePhaserInit}>
      {/*
        Both overlay children are DEVELOPMENT-ONLY diagnostics, gated by the same
        existing project convention (`import.meta.env.DEV`): the runtime status
        panel reports transport/infrastructure state and the viewport overlay
        reports presentation geometry. Neither is a gameplay HUD — and the status
        panel is an opaque card anchored over the game surface's bottom-right
        corner — so neither is mounted in a normal player presentation
        (TASK-209 §6; ui/components/StatusOverlay.tsx "It is NOT the game HUD").
        The player-facing battle information is rendered by BattleScene itself.
      */}
      {import.meta.env.DEV ? <StatusOverlay /> : null}
      {import.meta.env.DEV ? <ViewportDebugOverlay /> : null}
    </GameShell>
  );
};

export default App;
