import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import type { MockInstance } from 'vitest';
import { render, act } from '@testing-library/react';
import { App, resetSharedRuntime } from '../src/app/App';
import { GameRuntime } from '../src/game/runtime/GameRuntime';
import { DiscordService } from '../src/services/discord/DiscordService';
import { ApiService } from '../src/services/api/ApiService';
import { ApplicationSession } from '../src/services/api/ApplicationSession';
import { SignalRService } from '../src/services/realtime/SignalRService';
import {
  DEVELOPMENT_AUTHORIZATION_CODE,
  isDevelopmentAuthenticationEnabled,
} from '../src/services/api/DevelopmentAuthentication';

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

/**
 * The development-only authentication bootstrap (TASK-181).
 *
 * <code>
 * normal browser tab (window.self === window.top)
 *         ↓
 * DiscordService.initialize()            → isAvailable: false
 *         ↓
 * isDevelopmentAuthenticationEnabled()   → the explicit opt-in
 *         ↓
 * ApiService.authenticateDiscord('development')
 *         ↓
 * ApplicationSession established
 *         ↓
 * GameRuntime.setSessionStatus('authenticated')
 *         ↓
 * SignalR connect
 * </code>
 *
 * These exercise the real `App` bootstrap and the real session holder. The Discord
 * SDK, the API call, and the SignalR transport are substituted exactly as the
 * existing lifecycle suite substitutes them — nothing injects an identity into the
 * client, because the client never holds one (ADR-015 D3): the session comes from
 * `ApiService`, which is the same call the Discord path makes.
 */
describe('Development authentication bootstrap (TASK-181)', () => {
  let connectSpy: MockInstance;
  let discordInitSpy: MockInstance;
  let discordAuthCodeSpy: MockInstance;
  let apiAuthSpy: MockInstance;

  const enabledResponse = {
    sessionToken: 'development.jwt.session.token',
    playerId: 'player_development',
  };

  beforeEach(() => {
    resetSharedRuntime();
    ApplicationSession.getInstance().clear();
    // The suite's default is pinned off (tests/setup.ts); each test states the
    // value it runs with rather than inheriting one.
    vi.stubEnv('VITE_DEV_AUTH', 'false');

    const signalR = SignalRService.getInstance();
    connectSpy = vi.spyOn(signalR, 'connect').mockResolvedValue(undefined);
    vi.spyOn(signalR, 'disconnect').mockResolvedValue(undefined);
    vi.spyOn(signalR, 'on').mockReturnValue(() => {});
    vi.spyOn(signalR, 'setHandlers').mockImplementation(() => {});

    const discord = DiscordService.getInstance();
    // Exactly what DiscordService reports in a normal browser tab: the iframe gate
    // fails, so no Discord SDK session and no authorization code exist.
    discordInitSpy = vi.spyOn(discord, 'initialize').mockResolvedValue({
      isAvailable: false,
      error: 'Not running inside Discord Activity iframe (Local Development Mode)',
    });
    discordAuthCodeSpy = vi.spyOn(discord, 'getAuthorizationCode').mockResolvedValue(null);

    const api = ApiService.getInstance();
    apiAuthSpy = vi.spyOn(api, 'authenticateDiscord').mockImplementation(async (_code: string) => {
      ApplicationSession.getInstance().establish(enabledResponse);
      return enabledResponse;
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    ApplicationSession.getInstance().clear();
    resetSharedRuntime();
    // Restore this file's own default rather than unstubbing to an ambient value
    // a developer's `.env.local` may carry.
    vi.stubEnv('VITE_DEV_AUTH', 'false');
  });

  // ---------------------------------------------------------------------
  // The condition itself
  // ---------------------------------------------------------------------

  describe('the development switch', () => {
    it('is off unless it is explicitly opted into', () => {
      expect(isDevelopmentAuthenticationEnabled()).toBe(false);

      for (const value of ['', '1', 'TRUE', 'yes']) {
        vi.stubEnv('VITE_DEV_AUTH', value);
        expect(isDevelopmentAuthenticationEnabled()).toBe(false);
      }

      vi.stubEnv('VITE_DEV_AUTH', 'true');
      expect(isDevelopmentAuthenticationEnabled()).toBe(true);
    });
  });

  // ---------------------------------------------------------------------
  // Boot in a normal browser tab
  // ---------------------------------------------------------------------

  describe('a normal browser tab (not framed by Discord Activity)', () => {
    it('runs outside an iframe, which is the condition the development path exists for', () => {
      // The precondition the development path removes: jsdom is a normal
      // browsing context, so `window.self !== window.top` is false — the same
      // evaluation DiscordService.initialize uses to report the SDK unavailable.
      expect(window.self).toBe(window.top);
    });

    it('authenticates through the development path and connects SignalR', async () => {
      vi.stubEnv('VITE_DEV_AUTH', 'true');

      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation(function (
        this: GameRuntime,
        status: string
      ) {
        sessionStatusCalls.push(status);
        (this as unknown as { updateState: (patch: { session: string }) => void }).updateState({
          session: status,
        });
      });

      let authenticatedAtConnect: boolean | null = null;
      connectSpy.mockImplementation(async () => {
        authenticatedAtConnect = ApplicationSession.getInstance().isAuthenticated();
      });

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      // The development identity request went to the one endpoint that
      // establishes a session (API_CONTRACTS.md §2.1, §2.8 "Coverage")…
      expect(apiAuthSpy).toHaveBeenCalledTimes(1);
      expect(apiAuthSpy).toHaveBeenCalledWith(DEVELOPMENT_AUTHORIZATION_CODE);

      // …no Discord authorization code was needed…
      expect(discordInitSpy).toHaveBeenCalledTimes(1);
      expect(discordAuthCodeSpy).not.toHaveBeenCalled();

      // …a real session is held, and the runtime was told it is authenticated…
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(true);
      expect(ApplicationSession.getInstance().getSessionToken()).toBe(enabledResponse.sessionToken);
      expect(sessionStatusCalls).toEqual(['authenticating', 'authenticated']);

      // …and SignalR connected only once that session existed.
      expect(connectSpy).toHaveBeenCalledTimes(1);
      expect(connectSpy).toHaveBeenCalledWith('/hubs/battle');
      expect(authenticatedAtConnect).toBe(true);
    });

    it('reports an error and does not connect when the development switch is off', async () => {
      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation((status: string) => {
        sessionStatusCalls.push(status);
      });

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      // The pre-existing behaviour: no iframe and no opt-in means no session.
      expect(sessionStatusCalls).toEqual(['authenticating', 'error']);
      expect(discordAuthCodeSpy).not.toHaveBeenCalled();
      expect(apiAuthSpy).not.toHaveBeenCalled();
      expect(connectSpy).not.toHaveBeenCalled();
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);
    });

    it('reports an error and does not connect when the development session is refused', async () => {
      vi.stubEnv('VITE_DEV_AUTH', 'true');

      // The server refusing the path (unknown environment, no opt-in, or the
      // development source not registered) is the failure the client must not
      // paper over: no session, no connection.
      apiAuthSpy.mockRejectedValue(new Error('Authentication failed with status 503'));

      const sessionStatusCalls: string[] = [];
      vi.spyOn(GameRuntime.prototype, 'setSessionStatus').mockImplementation((status: string) => {
        sessionStatusCalls.push(status);
      });

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(apiAuthSpy).toHaveBeenCalledWith(DEVELOPMENT_AUTHORIZATION_CODE);
      expect(sessionStatusCalls).toEqual(['authenticating', 'error']);
      expect(connectSpy).not.toHaveBeenCalled();
      expect(ApplicationSession.getInstance().isAuthenticated()).toBe(false);
    });
  });

  // ---------------------------------------------------------------------
  // The Discord Activity path is untouched
  // ---------------------------------------------------------------------

  describe('the Discord Activity path', () => {
    it('still runs the Discord flow when framed, even with the development switch on', async () => {
      vi.stubEnv('VITE_DEV_AUTH', 'true');

      discordInitSpy.mockResolvedValue({ isAvailable: true });
      discordAuthCodeSpy.mockResolvedValue('discord-authorization-code');

      render(<App />);

      await act(async () => {
        await Promise.resolve();
      });

      expect(discordAuthCodeSpy).toHaveBeenCalledTimes(1);
      // The Discord authorization code is what was exchanged — not the
      // development trigger.
      expect(apiAuthSpy).toHaveBeenCalledWith('discord-authorization-code');
      expect(apiAuthSpy).not.toHaveBeenCalledWith(DEVELOPMENT_AUTHORIZATION_CODE);
      expect(connectSpy).toHaveBeenCalledTimes(1);
    });
  });
});
