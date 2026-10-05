import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ApplicationSession } from '../src/services/api/ApplicationSession';
import { ApiService } from '../src/services/api/ApiService';

/**
 * Application-session propagation on the client (ADR-015, ADR-020).
 *
 * ```text
 * Web authentication (Register / Login)
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
    expect(session.getUsername()).toBeNull();

    // With no session there is no Authorization header to send
    expect(session.getAuthorizationHeader()).toEqual({});
  });

  it('should hold the session established by successful authentication', () => {
    session.establish({
      sessionToken: 'header.payload.signature',
      playerId: 'player_1',
      username: 'testuser',
    });

    expect(session.isAuthenticated()).toBe(true);
    expect(session.getSessionToken()).toBe('header.payload.signature');
    expect(session.getPlayerId()).toBe('player_1');
    expect(session.getUsername()).toBe('testuser');
  });

  it('should persist session to localStorage and restore successfully', () => {
    session.establish({
      sessionToken: 'header.payload.signature',
      playerId: 'player_1',
      username: 'testuser',
    });

    expect(window.localStorage.getItem('dcacti_session_token')).toBe('header.payload.signature');
    expect(window.localStorage.getItem('dcacti_player_id')).toBe('player_1');
    expect(window.localStorage.getItem('dcacti_username')).toBe('testuser');

    // Create fresh instance or clear memory without clearing storage
    (session as any).sessionToken = null;
    (session as any).playerId = null;
    (session as any).username = null;
    expect(session.isAuthenticated()).toBe(false);

    const restored = session.restoreFromStorage();
    expect(restored).toBe(true);
    expect(session.isAuthenticated()).toBe(true);
    expect(session.getSessionToken()).toBe('header.payload.signature');
    expect(session.getUsername()).toBe('testuser');
  });

  it('should present the session as a Bearer header', () => {
    session.establish({
      sessionToken: 'header.payload.signature',
      playerId: 'player_1',
      username: 'testuser',
    });

    expect(session.getAuthorizationHeader()).toEqual({
      Authorization: 'Bearer header.payload.signature',
    });
  });

  it('should forget the session and clear storage when cleared', () => {
    session.establish({
      sessionToken: 'header.payload.signature',
      playerId: 'player_1',
      username: 'testuser',
    });
    session.clear();

    expect(session.isAuthenticated()).toBe(false);
    expect(session.getAuthorizationHeader()).toEqual({});
    expect(window.localStorage.getItem('dcacti_session_token')).toBeNull();
  });
});

describe('ApiService credential propagation', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    ApplicationSession.getInstance().clear();
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  it('should register a new account without Authorization header and establish session', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        sessionToken: 'registered.token.value',
        playerId: 'player_new',
        username: 'alice',
      }),
    });

    const result = await ApiService.getInstance().register({
      username: 'alice',
      password: 'password123',
    });

    expect(result.username).toBe('alice');
    expect(result.sessionToken).toBe('registered.token.value');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/auth/register');
    expect(init.headers).not.toHaveProperty('Authorization');
    expect(JSON.parse(init.body)).toEqual({
      username: 'alice',
      password: 'password123',
    });

    expect(ApplicationSession.getInstance().isAuthenticated()).toBe(true);
    expect(ApplicationSession.getInstance().getUsername()).toBe('alice');
  });

  it('should login an existing account without Authorization header and establish session', async () => {
    fetchMock.mockResolvedValue({
      ok: true,
      json: async () => ({
        sessionToken: 'logged_in.token.value',
        playerId: 'player_existing',
        username: 'bob',
      }),
    });

    const result = await ApiService.getInstance().login({
      username: 'bob',
      password: 'password123',
    });

    expect(result.username).toBe('bob');
    expect(result.sessionToken).toBe('logged_in.token.value');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/auth/login');
    expect(init.headers).not.toHaveProperty('Authorization');
    expect(JSON.parse(init.body)).toEqual({
      username: 'bob',
      password: 'password123',
    });

    expect(ApplicationSession.getInstance().isAuthenticated()).toBe(true);
    expect(ApplicationSession.getInstance().getUsername()).toBe('bob');
  });

  it('should throw an error on failed login', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 401,
      json: async () => ({ error: 'INVALID_CREDENTIALS' }),
    });

    await expect(
      ApiService.getInstance().login({ username: 'bob', password: 'bad' })
    ).rejects.toThrow('INVALID_CREDENTIALS');
  });

  it('should send the session as Bearer on authenticated REST requests', async () => {
    ApplicationSession.getInstance().establish({
      sessionToken: 'issued.token.value',
      playerId: 'player_9',
      username: 'charlie',
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
      username: 'charlie',
    });

    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ battleId: 'battle_1' }) });

    await ApiService.getInstance().post('/api/battle/start', { petId: 'pet_1' });

    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers.Authorization).toBe('Bearer issued.token.value');
  });

  it('should never send a PlayerId as authentication material in request body or headers', async () => {
    ApplicationSession.getInstance().establish({
      sessionToken: 'issued.token.value',
      playerId: 'player_9',
      username: 'charlie',
    });

    fetchMock.mockResolvedValue({ ok: true, json: async () => ({}) });

    await ApiService.getInstance().get('/api/pets');

    const serialized = JSON.stringify(fetchMock.mock.calls[0]);
    expect(serialized).not.toContain('X-Player-Id');
    expect(serialized).not.toContain('player_9');
  });
});
