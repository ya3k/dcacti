import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import type { MockInstance } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { App, resetSharedRuntime, getSharedRuntime } from '../src/app/App';
import { ApiService } from '../src/services/api/ApiService';
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
  let apiLoginSpy: MockInstance;
  let apiRegisterSpy: MockInstance;

  beforeEach(() => {
    resetSharedRuntime();
    ApplicationSession.getInstance().clear();
    window.localStorage.clear();

    const signalR = SignalRService.getInstance();
    connectSpy = vi.spyOn(signalR, 'connect').mockResolvedValue(undefined);
    vi.spyOn(signalR, 'disconnect').mockResolvedValue(undefined);
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
});
