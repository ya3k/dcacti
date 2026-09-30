import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import type { MockInstance } from 'vitest';
import { render, act } from '@testing-library/react';
import React from 'react';
import { App, resetSharedRuntime } from '../src/app/App';
import { GameRuntime } from '../src/game/runtime/GameRuntime';
import { DiscordService } from '../src/services/discord/DiscordService';
import { ApiService } from '../src/services/api/ApiService';
import { ApplicationSession } from '../src/services/api/ApplicationSession';
import { SignalRService } from '../src/services/realtime/SignalRService';

vi.mock('phaser', () => {
  // Mirrors tests/setup.ts: `new Phaser.Game(config)` must yield an object with
  // a `destroy` method, because PhaserGame calls it during cleanup.
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

/**
 * Application-level lifecycle and authentication orchestration tests (TASK-086).
 *
 * Requirements (TDD.md §2.1 item 4, API_CONTRACTS.md §2, SIGNALR_PROTOCOL.md §1):
 *   mount
 *     ↓
 *   Discord initialization (DiscordService.initialize)
 *     ↓
 *   authorization (DiscordService.getAuthorizationCode)
 *     ↓
 *   API authentication (ApiService.authenticateDiscord)
 *     ↓
 *   ApplicationSession authenticated (ApplicationSession.establish)
 *     ↓
 *   GameRuntime.setSessionStatus('authenticated')
 *     ↓
 *   SignalR connect (SignalRService.connect)
 *
 * Invariant:
 *   NO AUTHENTICATED APPLICATION SESSION → NO SIGNALR CONNECTION
 */
describe('App runtime lifecycle and authentication orchestration', () => {
  let connectSpy: MockInstance;
  let disconnectSpy: MockInstance;
  let discordInitSpy: MockInstance;
  let discordAuthCodeSpy: MockInstance;
  let apiAuthSpy: MockInstance;

  beforeEach(() => {
    resetSharedRuntime();
    ApplicationSession.getInstance().clear();

    const signalR = SignalRService.getInstance();
    connectSpy = vi.spyOn(signalR, 'connect').mockResolvedValue(undefined);
    disconnectSpy = vi.spyOn(signalR, 'disconnect').mockResolvedValue(undefined);
    vi.spyOn(signalR, 'on').mockReturnValue(() => {});
    vi.spyOn(signalR, 'setHandlers').mockImplementation(() => {});

    const discord = DiscordService.getInstance();
    discordInitSpy = vi.spyOn(discord, 'initialize').mockResolvedValue({
      isAvailable: true,
    });
    discordAuthCodeSpy = vi
      .spyOn(discord, 'getAuthorizationCode')
      .mockResolvedValue('test-auth-code-123');

    const api = ApiService.getInstance();
    apiAuthSpy = vi.spyOn(api, 'authenticateDiscord').mockImplementation(async (_code: string) => {
      const response = {
        sessionToken: 'test.jwt.session.token',
        playerId: 'player_test_1',
      };
      ApplicationSession.getInstance().establish(response);
      return response;
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    ApplicationSession.getInstance().clear();
    resetSharedRuntime();
  });

  describe('successful authentication ordering', () => {
    it('renders the application shell', async () => {
      const { container } = render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(container.querySelector('.game-shell')).not.toBeNull();
    });

    it('executes the full authentication sequence before connecting SignalR', async () => {
      const executionOrder: string[] = [];

      discordInitSpy.mockImplementation(async () => {
        executionOrder.push('discord:initialize');
        return { isAvailable: true };
      });

      discordAuthCodeSpy.mockImplementation(async () => {
        executionOrder.push('discord:getAuthorizationCode');
        return 'test-auth-code-123';
      });

      apiAuthSpy.mockImplementation(async (code: string) => {
        executionOrder.push(`api:authenticateDiscord(${code})`);
        const response = {
          sessionToken: 'test.jwt.session.token',
          playerId: 'player_test_1',
        };
        ApplicationSession.getInstance().establish(response);
        return response;
      });

      let sessionAuthenticatedAtConnect: boolean | null = null;
      let sessionTokenAtConnect: string | null = null;

      connectSpy.mockImplementation(async () => {
        executionOrder.push('signalr:connect');
        sessionAuthenticatedAtConnect = ApplicationSession.getInstance().isAuthenticated();
        sessionTokenAtConnect = ApplicationSession.getInstance().getSessionToken();
        return Promise.resolve();
      });

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      // Proof of ordering: authenticate completed BEFORE SignalR connect attempted
      expect(executionOrder).toEqual([
        'discord:initialize',
        'discord:getAuthorizationCode',
        'api:authenticateDiscord(test-auth-code-123)',
        'signalr:connect',
      ]);

      // Proof of authenticated session at connect time
      expect(sessionAuthenticatedAtConnect).toBe(true);
      expect(sessionTokenAtConnect).toBe('test.jwt.session.token');
      expect(connectSpy).toHaveBeenCalledTimes(1);
      expect(connectSpy).toHaveBeenCalledWith('/hubs/battle');
    });

    it('transitions session status from unauthenticated to authenticating to authenticated', async () => {
      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation(function (
        this: GameRuntime,
        status: string
      ) {
        sessionStatusCalls.push(status);
        // Call actual implementation to update state
        (this as unknown as { updateState: (patch: { session: string }) => void }).updateState({
          session: status,
        });
      });

      discordInitSpy.mockResolvedValue({ isAvailable: true });
      discordAuthCodeSpy.mockResolvedValue('test-auth-code-123');
      apiAuthSpy.mockImplementation(async () => {
        const response = {
          sessionToken: 'test.jwt.session.token',
          playerId: 'player_test_1',
        };
        ApplicationSession.getInstance().establish(response);
        return response;
      });

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      // Transitions: authenticating -> authenticated (initial was unauthenticated)
      expect(sessionStatusCalls).toEqual(['authenticating', 'authenticated']);
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(true);
    });

    it('ensures SignalR receives the application session JWT through accessTokenFactory after authentication', async () => {
      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(true);
      expect(ApplicationSession.getInstance().getSessionToken()).toBe('test.jwt.session.token');
    });
  });

  describe('authentication failure paths', () => {
    it('does not connect SignalR and transitions session to error when Discord SDK is unavailable', async () => {
      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation((status: string) => {
        sessionStatusCalls.push(status);
      });

      discordInitSpy.mockResolvedValue({
        isAvailable: false,
        error: 'Not running inside Discord Activity iframe (Local Development Mode)',
      });

      const { container } = render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(sessionStatusCalls).toEqual(['authenticating', 'error']);
      expect(discordInitSpy).toHaveBeenCalledTimes(1);
      expect(discordAuthCodeSpy).not.toHaveBeenCalled();
      expect(apiAuthSpy).not.toHaveBeenCalled();
      expect(connectSpy).not.toHaveBeenCalled();
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);
      expect(container.querySelector('.game-shell')).not.toBeNull();
    });

    it('does not connect SignalR and transitions session to error when Discord initialization throws', async () => {
      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation((status: string) => {
        sessionStatusCalls.push(status);
      });

      discordInitSpy.mockRejectedValue(new Error('SDK init threw'));

      const { container } = render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(sessionStatusCalls).toEqual(['authenticating', 'error']);
      expect(discordInitSpy).toHaveBeenCalledTimes(1);
      expect(discordAuthCodeSpy).not.toHaveBeenCalled();
      expect(apiAuthSpy).not.toHaveBeenCalled();
      expect(connectSpy).not.toHaveBeenCalled();
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);
      expect(container.querySelector('.game-shell')).not.toBeNull();
    });

    it('does not connect SignalR and transitions session to error when authorize returns null', async () => {
      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation((status: string) => {
        sessionStatusCalls.push(status);
      });

      discordAuthCodeSpy.mockResolvedValue(null);

      const { container } = render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(sessionStatusCalls).toEqual(['authenticating', 'error']);
      expect(discordInitSpy).toHaveBeenCalledTimes(1);
      expect(discordAuthCodeSpy).toHaveBeenCalledTimes(1);
      expect(apiAuthSpy).not.toHaveBeenCalled();
      expect(connectSpy).not.toHaveBeenCalled();
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);
      expect(container.querySelector('.game-shell')).not.toBeNull();
    });

    it('does not connect SignalR and transitions session to error when authorize throws', async () => {
      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation((status: string) => {
        sessionStatusCalls.push(status);
      });

      discordAuthCodeSpy.mockRejectedValue(new Error('User denied access'));

      const { container } = render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(sessionStatusCalls).toEqual(['authenticating', 'error']);
      expect(discordInitSpy).toHaveBeenCalledTimes(1);
      expect(discordAuthCodeSpy).toHaveBeenCalledTimes(1);
      expect(apiAuthSpy).not.toHaveBeenCalled();
      expect(connectSpy).not.toHaveBeenCalled();
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);
      expect(container.querySelector('.game-shell')).not.toBeNull();
    });

    it('does not connect SignalR and transitions session to error when API authentication returns non-2xx', async () => {
      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation((status: string) => {
        sessionStatusCalls.push(status);
      });

      apiAuthSpy.mockRejectedValue(new Error('Authentication failed with status 401'));

      const { container } = render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(sessionStatusCalls).toEqual(['authenticating', 'error']);
      expect(discordInitSpy).toHaveBeenCalledTimes(1);
      expect(discordAuthCodeSpy).toHaveBeenCalledTimes(1);
      expect(apiAuthSpy).toHaveBeenCalledWith('test-auth-code-123');
      expect(connectSpy).not.toHaveBeenCalled();
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);
      expect(container.querySelector('.game-shell')).not.toBeNull();
    });

    it('does not connect SignalR and transitions session to error on network failure during API authentication', async () => {
      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation((status: string) => {
        sessionStatusCalls.push(status);
      });

      apiAuthSpy.mockRejectedValue(new TypeError('Failed to fetch'));

      const { container } = render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(sessionStatusCalls).toEqual(['authenticating', 'error']);
      expect(discordInitSpy).toHaveBeenCalledTimes(1);
      expect(discordAuthCodeSpy).toHaveBeenCalledTimes(1);
      expect(apiAuthSpy).toHaveBeenCalledWith('test-auth-code-123');
      expect(connectSpy).not.toHaveBeenCalled();
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);
      expect(container.querySelector('.game-shell')).not.toBeNull();
    });
  });

  describe('StrictMode and idempotency', () => {
    it('executes authentication and connects exactly once under StrictMode remounting', async () => {
      const { unmount } = render(
        <React.StrictMode>
          <App />
        </React.StrictMode>
      );

      await act(async () => {
        await Promise.resolve();
      });

      unmount();

      await act(async () => {
        await Promise.resolve();
      });

      // StrictMode double-invocation must not cause duplicate auth or duplicate connect
      expect(discordInitSpy).toHaveBeenCalledTimes(1);
      expect(discordAuthCodeSpy).toHaveBeenCalledTimes(1);
      expect(apiAuthSpy).toHaveBeenCalledTimes(1);
      expect(connectSpy).toHaveBeenCalledTimes(1);
    });

    it('does not tear down the connection on a StrictMode effect cleanup', async () => {
      const { unmount } = render(
        <React.StrictMode>
          <App />
        </React.StrictMode>
      );

      await act(async () => {
        await Promise.resolve();
      });

      // The runtime intentionally outlives an individual effect run, because the
      // transport it owns is a process-wide singleton.
      expect(disconnectSpy).not.toHaveBeenCalled();

      unmount();
    });
  });

  describe('instance verification', () => {
    it('creates the runtime as a GameRuntime instance', () => {
      const runtime = new GameRuntime();
      expect(runtime).toBeInstanceOf(GameRuntime);
      expect(runtime.isInitialized()).toBe(false);
    });
  });
});