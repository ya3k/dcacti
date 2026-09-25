import { describe, it, expect, beforeEach, vi } from 'vitest';
import { SignalRService } from '../src/services/realtime/SignalRService';
import type {
  BattleStateUpdatedPayload,
  CellPayload,
} from '../src/services/realtime/SignalRService';
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

  describe('Board Foundation State transport (SIGNALR_PROTOCOL.md §4, §4.1)', () => {
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

      // The transport-level shape of the implemented stage: battleId, turn,
      // sequence, board, rngSeed, rngState, playerState, petState — and no other
      // member (`SIGNALR_PROTOCOL.md` §4 item 4, §4.2, §4.3). No Status or
      // lifecycle value exists in the protocol (§8.3).
      //
      // Each board cell is an entry carrying its Gem type plus an optional
      // Special Gem (`GAME_STATE.md` §2.1.1, §4.1 item 5). A generated board
      // holds no Special Gem, so the member is null (§2.1.7 item 8).
      //
      // Typed against the contract so the test also proves the declared payload
      // shape accepts every documented member — including the delivered
      // `petState` trio (§4.3) with its conditional reset override omitted for a
      // default reset (§4.3 item 7).
      const payload: BattleStateUpdatedPayload = {
        battleId: 'battle-1',
        turn: 0,
        sequence: 0,
        rngSeed: 42,
        rngState: { state: 123456789, increment: 1 },
        board: {
          cells: Array.from({ length: 64 }, (): CellPayload => ({ gemType: 'ATK', specialGem: null })),
        },
        playerState: { combo: 0, matchCount: 0 },
        petState: {
          passiveId: 'xich-lang',
          passiveProgress: { threshold: 5, current: 0 },
        },
      };
      hub.handlers.get('BattleStateUpdated')?.(payload);

      expect(received).toEqual([payload]);
    });

    it('accepts a delivered petState carrying a non-default reset override', async () => {
      // §4.3 item 6: when the Passive declares a non-default Reset Behavior the
      // member is present and carries the contract name — `"Partial"` or
      // `"NoReset"` (PASSIVE_RULES.md §4 item 2) — never a numeric enum ordinal.
      const hub = installFakeHub();
      await service.connect('/hubs/battle');

      const received: BattleStateUpdatedPayload[] = [];
      service.on<[BattleStateUpdatedPayload]>('BattleStateUpdated', (payload) =>
        received.push(payload)
      );

      const payload: BattleStateUpdatedPayload = {
        battleId: 'battle-1',
        turn: 2,
        sequence: 3,
        rngSeed: 42,
        rngState: { state: 123456789, increment: 1 },
        board: {
          cells: Array.from({ length: 64 }, (): CellPayload => ({ gemType: 'ATK', specialGem: null })),
        },
        playerState: { combo: 3, matchCount: 7 },
        petState: {
          passiveId: 'thanh-xa-poison',
          passiveProgress: { threshold: 7, current: 5 },
          passiveResetOverride: 'Partial',
        },
      };
      hub.handlers.get('BattleStateUpdated')?.(payload);

      // The service stores nothing and rewrites nothing: the payload it forwards
      // is the payload it received, `petState` included.
      expect(received[0]).toBe(payload);
      expect(received[0].petState.passiveResetOverride).toBe('Partial');
    });

    it('carries no gameplay calculation for the delivered petState', () => {
      // §4.3 item 9 / GAME_RULES.md §18: the service transports and forwards the
      // Passive members. It does not charge a Passive, evaluate a Threshold,
      // reset progress, or apply an overflow — that would be a second, client-side
      // Passive system, and the service is a transport boundary only.
      const surface = Object.getOwnPropertyNames(SignalRService.prototype);

      for (const forbidden of [
        'chargePassive',
        'resetPassive',
        'evaluateThreshold',
        'applyPassiveEffect',
        'applyOverflow',
        'resolvePassive',
      ]) {
        expect(surface).not.toContain(forbidden);
      }
    });

    it('delivers cell entries carrying a Special Gem unchanged', async () => {
      const hub = installFakeHub();
      await service.connect('/hubs/battle');

      const received: unknown[] = [];
      service.on('BattleStateUpdated', (payload) => received.push(payload));

      // A Line Clear Gem carries an orientation; a Burst and an Area Gem carry
      // none (`GAME_STATE.md` §2.1.4 item 2). The service treats all of it as
      // opaque transport data — it renders nothing and derives nothing
      // (`SIGNALR_PROTOCOL.md` §4.1 item 6).
      //
      // Typed against the contract so the test also proves the declared shape
      // accepts every documented cell entry.
      const cells: CellPayload[] = Array.from(
        { length: 64 },
        (): CellPayload => ({ gemType: 'ATK', specialGem: null })
      );
      cells[5] = { gemType: 'ATK', specialGem: { type: 'LineClear', orientation: 'Horizontal' } };
      cells[12] = { gemType: 'DEF', specialGem: { type: 'LineClear', orientation: 'Vertical' } };
      cells[27] = { gemType: 'HP', specialGem: { type: 'Burst', orientation: null } };
      cells[40] = { gemType: 'POWER', specialGem: { type: 'Area', orientation: null } };

      const payload = {
        battleId: 'battle-1',
        turn: 1,
        sequence: 1,
        rngSeed: 42,
        rngState: { state: 123456789, increment: 1 },
        board: { cells },
      };
      hub.handlers.get('BattleStateUpdated')?.(payload);

      expect(received[0]).toBe(payload);
    });

    it('does not derive or transform the payload', async () => {
      const hub = installFakeHub();
      await service.connect('/hubs/battle');

      const received: unknown[] = [];
      service.on('BattleStateUpdated', (payload) => received.push(payload));

      const payload = {
        battleId: 'battle-1',
        turn: 3,
        sequence: 9,
        rngSeed: 7,
        rngState: { state: 1, increment: 1 },
        board: {
          cells: Array.from({ length: 64 }, (_, i) => ({
            gemType: i % 2 ? 'ATK' : 'DEF',
            specialGem: null,
          })),
        },
      };
      hub.handlers.get('BattleStateUpdated')?.(payload);

      // The service stores no state and computes nothing: the object it hands
      // on is the object it received — board cells included.
      expect(received[0]).toBe(payload);
    });

    it('exposes no board request or re-roll method', async () => {
      // SIGNALR_PROTOCOL.md §8.6: the client does not ask for a board, a cell,
      // or a re-roll. It joins the group and receives the board with the rest
      // of the state.
      const surface = Object.getOwnPropertyNames(SignalRService.prototype);

      for (const forbidden of ['getBoard', 'requestBoard', 'rerollBoard', 'rollBoard', 'getCell']) {
        expect(surface).not.toContain(forbidden);
      }
    });

    it('exposes no Special Gem gameplay method', async () => {
      // SIGNALR_PROTOCOL.md §4.1 item 6 / GAME_RULES.md §18: the client renders
      // the board it receives, including its Special Gems, and never computes
      // one — it does not create, place, move, match, activate, chain, or clear
      // a Special Gem, and never infers a type or orientation.
      const surface = Object.getOwnPropertyNames(SignalRService.prototype);

      for (const forbidden of [
        'activateSpecialGem',
        'detonateSpecialGem',
        'createSpecialGem',
        'resolveBoard',
        'resolveSpecialGems',
        'matchGems',
        'applyGravity',
        'spawnGems',
      ]) {
        expect(surface).not.toContain(forbidden);
      }

      // No gameplay message is registered or invoked by name either.
      const source = SignalRService.prototype.joinBattle.toString();
      expect(source).toContain('JoinBattle');
      expect(source).not.toContain('SpecialGem');
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
