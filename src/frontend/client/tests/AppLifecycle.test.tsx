import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import type { MockInstance } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { App, resetSharedRuntime, getSharedRuntime } from '../src/app/App';
import { ApiRequestError, ApiService } from '../src/services/api/ApiService';
import { ApplicationSession } from '../src/services/api/ApplicationSession';
import { SignalRService } from '../src/services/realtime/SignalRService';

vi.mock('phaser', () => {
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

describe('App runtime lifecycle and Web Account authentication orchestration', () => {
  let connectSpy: MockInstance;
  let disconnectSpy: MockInstance;
  let apiLoginSpy: MockInstance;
  let apiRegisterSpy: MockInstance;

  beforeEach(() => {
    resetSharedRuntime();
    ApplicationSession.getInstance().clear();
    window.localStorage.clear();

    const signalR = SignalRService.getInstance();
    connectSpy = vi.spyOn(signalR, 'connect').mockResolvedValue(undefined);
    disconnectSpy = vi.spyOn(signalR, 'disconnect').mockResolvedValue(undefined);
    vi.spyOn(signalR, 'on').mockReturnValue(() => {});
    vi.spyOn(signalR, 'setHandlers').mockImplementation(() => {});

    const api = ApiService.getInstance();
    apiLoginSpy = vi.spyOn(api, 'login').mockImplementation(async (req) => {
      const response = {
        sessionToken: 'test.jwt.session.token',
        playerId: 'player_test_1',
        username: req.username,
      };
      ApplicationSession.getInstance().establish(response);
      return response;
    });

    apiRegisterSpy = vi.spyOn(api, 'register').mockImplementation(async (req) => {
      const response = {
        sessionToken: 'test.jwt.session.token',
        playerId: 'player_test_new',
        username: req.username,
      };
      ApplicationSession.getInstance().establish(response);
      return response;
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    ApplicationSession.getInstance().clear();
    window.localStorage.clear();
    resetSharedRuntime();
  });

  describe('unauthenticated initial state', () => {
    it('renders the AuthScreen when no session exists in localStorage', async () => {
      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(screen.getByTestId('auth-screen')).not.toBeNull();
      expect(screen.getByTestId('input-username')).not.toBeNull();
      expect(screen.getByTestId('input-password')).not.toBeNull();
    });

    it('INVARIANT: does NOT connect to SignalR while unauthenticated', async () => {
      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(connectSpy).not.toHaveBeenCalled();
    });
  });

  describe('restoring session from localStorage', () => {
    it('automatically transitions to authenticated and connects SignalR if session exists', async () => {
      ApplicationSession.getInstance().establish({
        sessionToken: 'persisted.jwt.token',
        playerId: 'player_persisted',
        username: 'saved_hero',
      });

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(connectSpy).toHaveBeenCalledTimes(1);
      expect(screen.queryByTestId('auth-screen')).toBeNull();
      expect(screen.getByTestId('game-shell')).not.toBeNull();
    });
  });

  describe('interactive login and registration flow', () => {
    it('authenticates through login form and mounts GameShell', async () => {
      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      const userInput = screen.getByTestId('input-username');
      const passInput = screen.getByTestId('input-password');
      const submitBtn = screen.getByTestId('button-submit');

      fireEvent.change(userInput, { target: { value: 'hero123' } });
      fireEvent.change(passInput, { target: { value: 'secretpass' } });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      expect(apiLoginSpy).toHaveBeenCalledWith({
        username: 'hero123',
        password: 'secretpass',
      });

      expect(connectSpy).toHaveBeenCalledTimes(1);
      expect(screen.queryByTestId('auth-screen')).toBeNull();
      expect(screen.getByTestId('game-shell')).not.toBeNull();
    });

    it('authenticates through register form and mounts GameShell', async () => {
      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      const registerTab = screen.getByTestId('tab-register');
      fireEvent.click(registerTab);

      const userInput = screen.getByTestId('input-username');
      const passInput = screen.getByTestId('input-password');
      const submitBtn = screen.getByTestId('button-submit');

      fireEvent.change(userInput, { target: { value: 'newhero' } });
      fireEvent.change(passInput, { target: { value: 'secretpass' } });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      expect(apiRegisterSpy).toHaveBeenCalledWith({
        username: 'newhero',
        password: 'secretpass',
      });

      expect(connectSpy).toHaveBeenCalledTimes(1);
      expect(screen.queryByTestId('auth-screen')).toBeNull();
      expect(screen.getByTestId('game-shell')).not.toBeNull();
    });

    it('shows error banner when credentials are rejected and never connects SignalR', async () => {
      apiLoginSpy.mockRejectedValue(new Error('INVALID_CREDENTIALS'));

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      const userInput = screen.getByTestId('input-username');
      const passInput = screen.getByTestId('input-password');
      const submitBtn = screen.getByTestId('button-submit');

      fireEvent.change(userInput, { target: { value: 'wronguser' } });
      fireEvent.change(passInput, { target: { value: 'wrongpass' } });

      await act(async () => {
        fireEvent.click(submitBtn);
      });

      expect(screen.getByTestId('auth-error-banner')).not.toBeNull();
      expect(connectSpy).not.toHaveBeenCalled();
      expect(screen.getByTestId('auth-screen')).not.toBeNull();
    });
  });

  describe('document unload', () => {
    it('disposes the shared runtime on pagehide', async () => {
      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      const runtime = getSharedRuntime();
      const disposeSpy = vi.spyOn(runtime, 'dispose');

      window.dispatchEvent(new Event('pagehide'));

      expect(disposeSpy).toHaveBeenCalledTimes(1);
    });
  });

  /**
   * TASK-234. The two approved session-ending paths (ADR-020 D4 item 2's
   * explicit sign-out, and API_CONTRACTS.md §2.3's `401 UNAUTHENTICATED` on the
   * authenticated transport) must both return the application to `AuthScreen`
   * in the same document, and both must leave it able to authenticate again.
   */
  describe('explicit sign-out (ADR-020 D4 item 2)', () => {
    const SESSION_KEYS = ['dcacti_session_token', 'dcacti_player_id', 'dcacti_username'];

    /** Starts from a persisted session — the authenticated shell (ADR-020 D4). */
    async function renderAuthenticatedApp() {
      ApplicationSession.getInstance().establish({
        sessionToken: 'persisted.jwt.token',
        playerId: 'player_persisted',
        username: 'saved_hero',
      });

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });
    }

    it('is absent from the unauthenticated presentation', async () => {
      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      // The control belongs to the authenticated shell only: it is rendered as a
      // `game-shell-overlay` child, so `AuthScreen` can never show it.
      expect(screen.getByTestId('auth-screen')).not.toBeNull();
      expect(screen.queryByTestId('sign-out-button')).toBeNull();
    });

    it('returns to AuthScreen, clears the three session keys and releases the transport', async () => {
      await renderAuthenticatedApp();
      expect(screen.getByTestId('game-shell')).not.toBeNull();
      const runtime = getSharedRuntime();

      // The control lives in the authenticated shell's overlay layer
      // (App.css `.game-shell__overlay`, ARCHITECTURE.md §2.2.2 rule 4).
      expect(
        screen.getByTestId('game-shell-overlay').contains(screen.getByTestId('sign-out-button'))
      ).toBe(true);

      await act(async () => {
        fireEvent.click(screen.getByTestId('sign-out-button'));
      });

      // Back to the unauthenticated presentation in the same document, and the
      // control disappears with the shell it belongs to.
      expect(screen.queryByTestId('game-shell')).toBeNull();
      expect(screen.getByTestId('auth-screen')).not.toBeNull();
      expect(screen.queryByTestId('sign-out-button')).toBeNull();

      // The canonical credential clear (ADR-015 D2, ADR-020 D4 item 2).
      for (const key of SESSION_KEYS) {
        expect(window.localStorage.getItem(key)).toBeNull();
      }
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);

      // One release of the authenticated transport — and the runtime is still
      // usable, because this cleanup is not `dispose()`.
      expect(disconnectSpy).toHaveBeenCalledTimes(1);
      expect(runtime.getState().session).toBe('unauthenticated');
      expect(runtime.getBattleState()).toBeNull();
      expect(runtime.isDisposed()).toBe(false);
      expect(runtime.isInitialized()).toBe(false);

      // No request of its own: no logout endpoint exists in MVP
      // (API_CONTRACTS.md §2.3 "Lifecycle (MVP)", ADR-015 D5).
      expect(apiLoginSpy).not.toHaveBeenCalled();
      expect(apiRegisterSpy).not.toHaveBeenCalled();
    });

    it('cannot be fired concurrently: a second activation performs no second cleanup', async () => {
      await renderAuthenticatedApp();

      const invalidateSpy = vi.spyOn(getSharedRuntime(), 'invalidateSession');
      const signOut = screen.getByTestId('sign-out-button');

      await act(async () => {
        fireEvent.click(signOut);
        fireEvent.click(signOut);
      });

      // The control dispatched the cleanup once: the second activation, while
      // the first cleanup was still in flight, was ignored rather than passed
      // on — and the cleanup itself is idempotent (one disconnect, not two).
      expect(invalidateSpy).toHaveBeenCalledTimes(1);
      expect(disconnectSpy).toHaveBeenCalledTimes(1);
      expect(screen.getByTestId('auth-screen')).not.toBeNull();
    });

    it('signs out and logs in again in the same document', async () => {
      await renderAuthenticatedApp();
      expect(connectSpy).toHaveBeenCalledTimes(1);

      await act(async () => {
        fireEvent.click(screen.getByTestId('sign-out-button'));
      });
      expect(screen.getByTestId('auth-screen')).not.toBeNull();

      fireEvent.change(screen.getByTestId('input-username'), { target: { value: 'hero123' } });
      fireEvent.change(screen.getByTestId('input-password'), { target: { value: 'secretpass' } });

      await act(async () => {
        fireEvent.click(screen.getByTestId('button-submit'));
      });

      expect(apiLoginSpy).toHaveBeenCalledWith({
        username: 'hero123',
        password: 'secretpass',
      });
      // The second authentication reconnects the runtime and mounts the shell
      // again — no reload and no stale `initialized` / subscription state.
      expect(connectSpy).toHaveBeenCalledTimes(2);
      expect(screen.queryByTestId('auth-screen')).toBeNull();
      expect(screen.getByTestId('game-shell')).not.toBeNull();
      expect(getSharedRuntime().getState().session).toBe('authenticated');
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(true);
    });
  });

  describe('authenticated HTTP 401 invalidation (API_CONTRACTS.md §2.3)', () => {
    const SESSION_KEYS = ['dcacti_session_token', 'dcacti_player_id', 'dcacti_username'];

    async function renderAuthenticatedApp() {
      ApplicationSession.getInstance().establish({
        sessionToken: 'persisted.jwt.token',
        playerId: 'player_persisted',
        username: 'saved_hero',
      });

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });
    }

    it('returns to AuthScreen without a reload, and authenticates again in the same document', async () => {
      // §2.3 "Failure behavior": missing, invalid/tampered and expired sessions
      // are one public answer, surfaced by the shared authenticated transport as
      // an `ApiRequestError` (§6 envelope).
      vi.spyOn(ApiService.getInstance(), 'getPets').mockRejectedValue(
        new ApiRequestError(
          401,
          'UNAUTHENTICATED',
          'Your session is no longer valid. Please sign in again.'
        )
      );

      await renderAuthenticatedApp();
      expect(screen.getByTestId('game-shell')).not.toBeNull();

      const runtime = getSharedRuntime();

      await act(async () => {
        await expect(runtime.getPets()).rejects.toThrow(/session is no longer valid/);
      });

      // The application is back on `AuthScreen` in the same document: the same
      // runtime instance, no re-bootstrap, and no reload.
      expect(screen.queryByTestId('game-shell')).toBeNull();
      expect(screen.getByTestId('auth-screen')).not.toBeNull();
      expect(getSharedRuntime()).toBe(runtime);
      expect(runtime.getState().session).toBe('unauthenticated');

      for (const key of SESSION_KEYS) {
        expect(window.localStorage.getItem(key)).toBeNull();
      }
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);
      expect(disconnectSpy).toHaveBeenCalledTimes(1);
      expect(connectSpy).toHaveBeenCalledTimes(1);

      // And the player can authenticate again in this document.
      fireEvent.change(screen.getByTestId('input-username'), { target: { value: 'hero123' } });
      fireEvent.change(screen.getByTestId('input-password'), { target: { value: 'secretpass' } });

      await act(async () => {
        fireEvent.click(screen.getByTestId('button-submit'));
      });

      expect(connectSpy).toHaveBeenCalledTimes(2);
      expect(screen.queryByTestId('auth-screen')).toBeNull();
      expect(screen.getByTestId('game-shell')).not.toBeNull();
      expect(screen.getByTestId('sign-out-button')).not.toBeNull();
      expect(runtime.getState().session).toBe('authenticated');
    });

    it('does not invalidate the session for a non-401 failure', async () => {
      // A `400 INVALID_LOADOUT` is a loadout rejection, not a session signal: the
      // shell must survive it (§3, §6).
      vi.spyOn(ApiService.getInstance(), 'getPets').mockRejectedValue(
        new ApiRequestError(400, 'INVALID_LOADOUT', 'The requested loadout is not valid.')
      );

      await renderAuthenticatedApp();
      const runtime = getSharedRuntime();

      await act(async () => {
        await expect(runtime.getPets()).rejects.toThrow(/loadout is not valid/);
      });

      expect(screen.getByTestId('game-shell')).not.toBeNull();
      expect(screen.queryByTestId('auth-screen')).toBeNull();
      expect(runtime.getState().session).toBe('authenticated');
      expect(disconnectSpy).not.toHaveBeenCalled();
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(true);
    });
  });
});
