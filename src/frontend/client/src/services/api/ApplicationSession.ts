export interface HealthStatus {
  status: string;
}

/**
 * The `POST /api/auth/discord` response (API_CONTRACTS.md §2.5).
 *
 * The shape is unchanged by the session contract. `sessionToken` is the
 * application session defined in §2.8 (ADR-015): a self-contained signed JWT
 * issued after the §2 identity exchange succeeds. It is **not** the Discord
 * access token — that is used only for the §2.3 identity request server-side and
 * is never issued to the client (§2.7 item 4).
 *
 * The token is application authentication material, so it is held in memory for
 * the session's lifetime and never persisted: ADR-015 D5 defines no revocation
 * and no refresh, so a stored token would outlive its usefulness with no way to
 * withdraw it, and D10's secret boundary is about never placing sessions in
 * artifacts the client controls.
 */
export interface DiscordAuthResponse {
  sessionToken: string;
  playerId: string;
}

/**
 * The application session's client-side holder (ADR-015 D3/D4).
 *
 * <code>
 * Discord identity exchange
 *         ↓
 * sessionToken (self-contained signed JWT)
 *         ↓
 * Authorization: Bearer on REST + SignalR access-token mechanism
 * </code>
 *
 * `playerId` is kept alongside the token only so the UI can label the
 * authenticated player. It is **not** an authority: every ownership decision is
 * made by the server from the token's `player_id` claim, and the client never
 * sends a PlayerId to establish identity (API_CONTRACTS.md §2.8 "Identity", §4
 * note 7).
 */
export class ApplicationSession {
  private static instance: ApplicationSession | null = null;

  private sessionToken: string | null = null;
  private playerId: string | null = null;

  private constructor() {}

  public static getInstance(): ApplicationSession {
    if (!ApplicationSession.instance) {
      ApplicationSession.instance = new ApplicationSession();
    }
    return ApplicationSession.instance;
  }

  /**
   * Records the session established by a successful §2 exchange.
   */
  public establish(response: DiscordAuthResponse): void {
    this.sessionToken = response.sessionToken;
    this.playerId = response.playerId;
  }

  /** Forgets the session. There is no server-side logout in MVP (D5). */
  public clear(): void {
    this.sessionToken = null;
    this.playerId = null;
  }

  public getSessionToken(): string | null {
    return this.sessionToken;
  }

  public getPlayerId(): string | null {
    return this.playerId;
  }

  public isAuthenticated(): boolean {
    return this.sessionToken !== null;
  }

  /**
   * The `Authorization` header value for REST requests (§2.8 "Transport"), or
   * `null` when no session has been established.
   */
  public getAuthorizationHeader(): Record<string, string> {
    return this.sessionToken === null
      ? {}
      : { Authorization: `Bearer ${this.sessionToken}` };
  }
}
