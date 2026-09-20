import { describe, it, expect, beforeEach, vi } from 'vitest';
import { SignalRService } from '../src/services/realtime/SignalRService';
import * as signalR from '@microsoft/signalr';

/**
 * Transport double for the hub connection.
 *
 * `SignalRService` is the only place in the client that may touch
 * `HubConnection`, so the service is exercised with a fake builder rather than
 * a live hub.
 */
function installFakeHub() {
  const handlers = new Map<string, (...args: unknown[]) => void>();
  const invokes: Array<{ method: string; args: unknown[] }> = [];

  const connection = {
    state: signalR.HubConnectionState.Connected,
    connectionId: 'conn-under-test',
    start: vi.fn().mockResolvedValue(undefined),
    stop: vi.fn().mockResolvedValue(undefined),
    on: vi.fn((method: string, handler: (...args: unknown[]) => void) => {
      handlers.set(method, handler);
    }),
    off: vi.fn((method: string) => {
      handlers.delete(method);
    }),
    invoke: vi.fn(async (method: string, ...args: unknown[]) => {
      invokes.push({ method, args });
      return undefined;
    }),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
  };

  vi.spyOn(signalR, 'HubConnectionBuilder').mockImplementation(
    () =>
      ({
        withUrl: () => builder,
        withAutomaticReconnect: () => builder,
        configureLogging: () => builder,
        build: () => connection,
      }) as unknown as signalR.HubConnectionBuilder
  );

  const builder = {
    withUrl: () => builder,
    withAutomaticReconnect: () => builder,
    configureLogging: () => builder,
    build: () => connection,
  };

  return { connection, handlers, invokes };
}

describe('SignalRService', () => {
  let service: SignalRService;

  beforeEach(async () => {
    vi.restoreAllMocks();
    service = SignalRService.getInstance();
    // The service is a process-wide singleton, so a connection left open by an
    // earlier case would (correctly) make the next one a no-op. Reset it.
    await service.disconnect();
  });

  it('should return singleton instance', () => {
    const another = SignalRService.getInstance();
    expect(service).toBe(another);
  });

  it('should report disconnected when not started', () => {
    expect(service.getConnectionState()).toBe(signalR.HubConnectionState.Disconnected);
    expect(service.isConnected()).toBe(false);
  });

  it('should throw error on ping when disconnected', async () => {
    await expect(service.ping()).rejects.toThrow('SignalR connection is not established.');
  });

  describe('Battle State Foundation transport (SIGNALR_PROTOCOL.md §4)', () => {
    it('subscribes to the documented BattleStateUpdated push', async () => {
      const hub = installFakeHub();
      await service.connect('/hubs/battle');

      const received: unknown[] = [];
      service.on('BattleStateUpdated', (payload) => received.push(payload));

      expect(hub.handlers.has('BattleStateUpdated')).toBe(true);
    });

    it('delivers the server payload to the subscriber unchanged', async () => {
      const hub = installFakeHub();
      await service.connect('/hubs/battle');

      const received: unknown[] = [];
      service.on('BattleStateUpdated', (payload) => received.push(payload));

      // The transport-level shape only: battleId, turn, sequence. No Status or
      // lifecycle value exists in the protocol (§4.4, §8.3).
      const payload = { battleId: 'battle-1', turn: 0, sequence: 0 };
      hub.handlers.get('BattleStateUpdated')?.(payload);

      expect(received).toEqual([payload]);
    });

    it('does not derive or transform the payload', async () => {
      const hub = installFakeHub();
      await service.connect('/hubs/battle');

      const received: unknown[] = [];
      service.on('BattleStateUpdated', (payload) => received.push(payload));

      const payload = { battleId: 'battle-1', turn: 3, sequence: 9 };
      hub.handlers.get('BattleStateUpdated')?.(payload);

      // The service stores no state and computes nothing: the object it hands
      // on is the object it received.
      expect(received[0]).toBe(payload);
    });

    it('unsubscribes cleanly', async () => {
      const hub = installFakeHub();
      await service.connect('/hubs/battle');

      const handler = vi.fn();
      const unsubscribe = service.on('BattleStateUpdated', handler);
      unsubscribe();

      expect(hub.handlers.has('BattleStateUpdated')).toBe(false);

      // A late push after unsubscribe must not reach the handler.
      hub.handlers.get('BattleStateUpdated')?.({ battleId: 'b', turn: 0, sequence: 0 });
      expect(handler).not.toHaveBeenCalled();
    });

    it('joins the battle group via the documented JoinBattle method', async () => {
      const hub = installFakeHub();
      await service.connect('/hubs/battle');

      await service.joinBattle('battle-1');

      // Joining the group is what triggers the server push (§4.1).
      expect(hub.invokes).toEqual([{ method: 'JoinBattle', args: ['battle-1'] }]);
    });

    it('throws on joinBattle when no connection is established', async () => {
      await expect(service.joinBattle('battle-1')).rejects.toThrow(
        'SignalR connection is not established.'
      );
    });
  });
});
