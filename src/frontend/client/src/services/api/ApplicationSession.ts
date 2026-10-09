export interface HealthStatus {
  status: string;
}

/**
 * Authoritative web session payload returned by POST /api/auth/register
 * and POST /api/auth/login (ADR-020).
 */
export interface AuthResponse {
  sessionToken: string;
  playerId: string;
  username: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

const STORAGE_KEYS = {
  SESSION_TOKEN: 'dcacti_session_token',
  PLAYER_ID: 'dcacti_player_id',
  USERNAME: 'dcacti_username',
} as const;

/**
 * The application session's client-side holder (ADR-015, ADR-020).
 *
 * <code>
 * Web account authentication (Register / Login)
 *         ↓
 * sessionToken (self-contained signed JWT) + playerId + username
 *         ↓
 * Authorization: Bearer on REST + SignalR access-token mechanism
 * </code>
 *
 * `playerId` and `username` are kept alongside the token for UI presentation.
 * Ownership decisions are made server-side from the token's `player_id` claim.
 */
export class ApplicationSession {
  private static instance: ApplicationSession | null = null;

  private sessionToken: string | null = null;
  private playerId: string | null = null;
  private username: string | null = null;

  private constructor() {}

  public static getInstance(): ApplicationSession {
    if (!ApplicationSession.instance) {
      ApplicationSession.instance = new ApplicationSession();
    }
    return ApplicationSession.instance;
  }

  /**
   * Records the session established by successful registration or login.
   */
  public establish(response: { sessionToken: string; playerId: string; username?: string }): void {
    this.sessionToken = response.sessionToken;
    this.playerId = response.playerId;
    this.username = response.username ?? 'player';

    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        window.localStorage.setItem(STORAGE_KEYS.SESSION_TOKEN, response.sessionToken);
        window.localStorage.setItem(STORAGE_KEYS.PLAYER_ID, response.playerId);
        window.localStorage.setItem(STORAGE_KEYS.USERNAME, this.username);
      } catch {
        // Storage might fail in sandboxed iframes or private browsing; ignore.
      }
    }
  }

  /**
   * Attempts to restore an existing session from localStorage.
   * Returns true if a valid session was restored.
   */
  public restoreFromStorage(): boolean {
    if (typeof window === 'undefined' || !window.localStorage) {
      return false;
    }

    try {
      const token = window.localStorage.getItem(STORAGE_KEYS.SESSION_TOKEN);
      const playerId = window.localStorage.getItem(STORAGE_KEYS.PLAYER_ID);
      const username = window.localStorage.getItem(STORAGE_KEYS.USERNAME);

      if (token && playerId && username) {
        this.sessionToken = token;
        this.playerId = playerId;
        this.username = username;
        return true;
      }
    } catch {
      // Storage access failure.
    }

    return false;
  }

  /** Forgets the session and clears localStorage. */
  public clear(): void {
    this.sessionToken = null;
    this.playerId = null;
    this.username = null;

    if (typeof window !== 'undefined' && window.localStorage) {
      try {
        window.localStorage.removeItem(STORAGE_KEYS.SESSION_TOKEN);
        window.localStorage.removeItem(STORAGE_KEYS.PLAYER_ID);
        window.localStorage.removeItem(STORAGE_KEYS.USERNAME);
      } catch {
        // Storage access failure.
      }
    }
  }

  public getSessionToken(): string | null {
    return this.sessionToken;
  }

  public getPlayerId(): string | null {
    return this.playerId;
  }

  public getUsername(): string | null {
    return this.username;
  }

  public isAuthenticated(): boolean {
    return this.sessionToken !== null;
  }

  /**
   * The `Authorization` header value for REST requests (§2.3 "Transport"), or
   * empty object when no session has been established.
   */
  public getAuthorizationHeader(): Record<string, string> {
    return this.sessionToken === null
      ? {}
      : { Authorization: `Bearer ${this.sessionToken}` };
  }
}
