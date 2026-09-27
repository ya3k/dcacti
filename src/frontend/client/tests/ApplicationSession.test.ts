import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ApplicationSession } from '../src/services/api/ApplicationSession';
import { ApiService } from '../src/services/api/ApiService';

/**
 * Application-session propagation on the client
 * (`API_CONTRACTS.md` §2.8 "Transport"; `ADR-015` D4).
 *
 * ```text
 * Discord identity exchange
 *         ↓
 * sessionToken (self-contained signed JWT)
 *         ↓
 * Authorization: Bearer on REST + SignalR access-token mechanism
 * ```
 */
describe('ApplicationSession', () => {
  let session: ApplicationSession;

  beforeEach(() => {
    session = ApplicationSession.getInstance();
    session.clear();
  });

  it('should return singleton instance', () => {
    expect(ApplicationSession.getInstance()).toBe(session);
  });

  it('should hold no session before one is established', () => {
    expect(session.isAuthenticated()).toBe(false);
    expect(session.getSessionToken()).toBeNull();
    expect(session.getPlayerId()).toBeNull();

    // §2.8 "Transport": with no session there is no Authorization header to
    // send — an empty header, not a placeholder credential.
    expect(session.getAuthorizationHeader()).toEqual({});
  });

  it('should hold the session established by a successful exchange', () => {
    session.establish({ sessionToken: 'header.payload.signature', playerId: 'player_1' });

    expect(session.isAuthenticated()).toBe(true);
    expect(session.getSessionToken()).toBe('header.payload.signature');
    expect(session.getPlayerId()).toBe('player_1');
  });

  it('should present the session as a Bearer header', () => {
    session.establish({ sessionToken: 'header.payload.signature', playerId: 'player_1' });

    // §2.8 "Transport": REST uses `Authorization: Bearer <sessionToken>`.
    expect(session.getAuthorizationHeader()).toEqual({
      Authorization: 'Bearer header.payload.signature',
    });
  });

  it('should forget the session when cleared', () => {
    session.establish({ sessionToken: 'header.payload.signature', playerId: 'player_1' });
    session.clear();

    expect(session.isAuthenticated()).toBe(false);
    expect(session.getAuthorizationHeader()).toEqual({});
  });

  it('should hold no session in browser storage', () => {
    // ADR-015 D5 has no revocation and no refresh, so a persisted token could
    // outlive its usefulness with no way to withdraw it. The session is held in
    // memory only: the holder exposes no persistence surface, and the token it
    // holds appears in neither storage area.
    session.establish({ sessionToken: 'header.payload.signature', playerId: 'player_1' });

    const surface = Object.getOwnPropertyNames(ApplicationSession.prototype);

    for (const forbidden of ['persist', 'save', 'store', 'load', 'restore']) {
      expect(surface).not.toContain(forbidden);
    }

    const stored = JSON.stringify([
      Object.entries(window.localStorage ?? {}),
      Object.entries(window.sessionStorage ?? {}),
    ]);

    expect(stored).not.toContain('header.payload.signature');
    expect(stored).not.toContain('sessionToken');
  });
});

describe('ApiService credential propagation', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    ApplicationSession.getInstance().clear();
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  it('should send no Authorization header to the session-establishing endpoint', async () => {
    // §2.1 / §2.8 "Coverage": `POST /api/auth/discord` is the one endpoint that
    // does not require an authenticated session — it establishes one.
    ApplicationSession.getInstance().establish({
      sessionToken: 'existing.token.value',
      playerId: 'player_1',
    });

    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ sessionToken: 'new.token.value', playerId: 'player_1' }),
    });

    await ApiService.getInstance().authenticateDiscord('authorization-code');

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers).not.toHaveProperty('Authorization');
  });

  it('should record the session returned by the exchange', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({ sessionToken: 'issued.token.value', playerId: 'player_9' }),
    });

    await ApiService.getInstance().authenticateDiscord('authorization-code');

    // The token the exchange issued is the one subsequent REST and hub traffic
    // presents (§2.5, §2.8 "Transport").
    expect(ApplicationSession.getInstance().getSessionToken()).toBe('issued.token.value');
    expect(ApplicationSession.getInstance().getPlayerId()).toBe('player_9');
  });

  it('should send the session as Bearer on authenticated REST requests', async () => {
    ApplicationSession.getInstance().establish({
      sessionToken: 'issued.token.value',
      playerId: 'player_9',
    });

    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ battleId: 'battle_1' }) });

    await ApiService.getInstance().get('/api/battle/history');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/battle/history');
    expect(init.headers.Authorization).toBe('Bearer issued.token.value');
  });

  it('should send the session as Bearer on authenticated POST requests', async () => {
    ApplicationSession.getInstance().establish({
      sessionToken: 'issued.token.value',
      playerId: 'player_9',
    });

    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ battleId: 'battle_1' }) });

    await ApiService.getInstance().post('/api/battle/start', { petId: 'pet_1' });

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers.Authorization).toBe('Bearer issued.token.value');
  });

  it('should never send a PlayerId as authentication material', async () => {
    // API_CONTRACTS.md §4 note 7 / ADR-015 D3: ownership is never established
    // from client-supplied input. The server derives the identity from the
    // session's `player_id` claim, so the client has no PlayerId to send.
    ApplicationSession.getInstance().establish({
      sessionToken: 'issued.token.value',
      playerId: 'player_9',
    });

    fetchMock.mockResolvedValue({ ok: true, json: async () => ({}) });

    await ApiService.getInstance().get('/api/pets');

    const serialized = JSON.stringify(fetchMock.mock.calls[0]);
    expect(serialized).not.toContain('X-Player-Id');
    expect(serialized).not.toContain('playerId');
  });
});
