import React, { useEffect, useRef, useState, useCallback } from 'react';
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
 * The URL the battle hub is connected to: `BATTLE_HUB_URL` prefixed with the
 * configured SignalR base (`src/frontend/client/.env.example`'s
 * `VITE_SIGNALR_URL`) when one is set.
 *
 * The variable exists so this client can reach a remote backend tunnel instead
 * of the origin it was served from; it is the same setting the template tells a
 * developer to paste the tunnel URL into. When it is unset — or set but empty,
 * which the template's empty default makes the ordinary local case — the
 * documented relative path `BATTLE_HUB_URL` is returned unchanged, so the hub
 * is reached through the document origin and the Vite development server's
 * `/hubs` proxy (`vite.config.ts`).
 *
 * Trailing slashes are removed so a configured base with or without one cannot
 * produce a doubled separator (`https://host//hubs/battle`).
 */
function resolveBattleHubUrl(): string {
  const configured = import.meta.env.VITE_SIGNALR_URL;

  if (typeof configured !== 'string') {
    return BATTLE_HUB_URL;
  }

  const baseUrl = configured.replace(/\/+$/, '');

  return baseUrl === '' ? BATTLE_HUB_URL : `${baseUrl}${BATTLE_HUB_URL}`;
}

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
      await runtime.initialize(resolveBattleHubUrl());
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

  // Guards the sign-out control against a concurrent second activation. A
  // re-render is not fast enough to be that guard, and React state read from a
  // handler is stale within the same tick, so the in-flight fact lives in a ref.
  const signOutInFlight = useRef(false);

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
      await activeRuntime.initialize(resolveBattleHubUrl());
    } catch {
      activeRuntime.setSessionStatus('error');
    }
  }, []);

  const handlePhaserInit = useCallback(() => {
    runtime?.setEngineStatus('running');
  }, [runtime]);

  /**
   * The explicit sign-out control's action (`ADR-020` D4 item 2).
   *
   * It runs the runtime's single session cleanup — the same routine a `401` on
   * the authenticated transport runs — so sign-out and session invalidation
   * cannot diverge: credentials cleared through `ApplicationSession.clear()`,
   * `session: 'unauthenticated'` published through the runtime state, and the
   * authenticated transport released. The render condition below then replaces
   * `GameShell` with `AuthScreen` in this document, and the runtime stays able
   * to authenticate again.
   *
   * The control performs no request of its own (`API_CONTRACTS.md` §2.3
   * "Lifecycle (MVP)", `ADR-015` D5: no logout endpoint, no server-side
   * revocation), and a second activation while the first cleanup is in flight is
   * ignored rather than dispatched.
   */
  const handleSignOut = useCallback(async () => {
    if (signOutInFlight.current) {
      return;
    }

    signOutInFlight.current = true;

    try {
      await getSharedRuntime().invalidateSession();
    } finally {
      signOutInFlight.current = false;
    }
  }, []);

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
        The first two overlay children are DEVELOPMENT-ONLY diagnostics, gated
        by the same existing project convention (`import.meta.env.DEV`): the
        runtime status panel reports transport/infrastructure state and the
        viewport overlay reports presentation geometry. Neither is a gameplay
        HUD — and the status panel is an opaque card anchored over the game
        surface's bottom-right corner — so neither is mounted in a normal player
        presentation (TASK-209 §6; ui/components/StatusOverlay.tsx "It is NOT
        the game HUD"). The player-facing battle information is rendered by
        BattleScene itself.
      */}
      {import.meta.env.DEV ? <StatusOverlay /> : null}
      {import.meta.env.DEV ? <ViewportDebugOverlay /> : null}

      {/*
        The authenticated shell's one player-facing platform control: the
        sign-out option `ADR-020` D4 item 2 requires. It is an overlay child, so
        it opts back into pointer events (App.css, ARCHITECTURE.md §2.2.2 rule 4)
        and is anchored top-right, clear of the two DEV-only diagnostics (the
        viewport overlay is top-left, the status card bottom-right). Styling is
        inline, following PhaserGame.tsx, so no stylesheet outside this task's
        declared files is touched. It is rendered only for an authenticated
        session, so it can never appear on `AuthScreen`.
      */}
      <button
        type="button"
        data-testid="sign-out-button"
        onClick={handleSignOut}
        style={{
          position: 'absolute',
          top: 12,
          right: 12,
          zIndex: 10,
          padding: '8px 14px',
          borderRadius: 8,
          border: '1px solid var(--card-border)',
          background: 'var(--card-bg)',
          color: 'var(--text-primary)',
          cursor: 'pointer',
        }}
      >
        Đăng xuất
      </button>
    </GameShell>
  );
};

export default App;
