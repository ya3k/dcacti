import { ApplicationSession } from './ApplicationSession';
import type { DiscordAuthResponse } from './ApplicationSession';

export interface HealthStatus {
  status: string;
}

/**
 * The `POST /api/auth/discord` response (API_CONTRACTS.md §2.5).
 *
 * Re-exported from {@link ApplicationSession} so the wire shape has one
 * definition — the session holder is what consumes it.
 */
export type { DiscordAuthResponse };

export class ApiService {
  private static instance: ApiService | null = null;
  private baseUrl: string;

  private constructor(baseUrl: string = '') {
    this.baseUrl = baseUrl;
  }

  public static getInstance(): ApiService {
    if (!ApiService.instance) {
      ApiService.instance = new ApiService();
    }
    return ApiService.instance;
  }

  public async checkHealth(): Promise<boolean> {
    try {
      const response = await fetch(`${this.baseUrl}/health`);
      if (!response.ok) return false;
      const text = await response.text();
      return text.includes('Healthy');
    } catch {
      return false;
    }
  }

  /**
   * Establishes the application session from a Discord authorization code
   * (API_CONTRACTS.md §2).
   *
   * This is the one endpoint that does not require an authenticated session
   * (§2.1, §2.8 "Coverage"), so it deliberately sends no `Authorization` header
   * even when a session already exists. On success the returned session is
   * recorded, which is what makes every later request authenticated.
   */
  public async authenticateDiscord(code: string): Promise<DiscordAuthResponse> {
    const response = await fetch(`${this.baseUrl}/api/auth/discord`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ code }),
    });

    if (!response.ok) {
      throw new Error(`Authentication failed with status ${response.status}`);
    }

    const body = (await response.json()) as DiscordAuthResponse;

    ApplicationSession.getInstance().establish(body);

    return body;
  }

  /**
   * A `GET` to an application endpoint, carrying the application session as
   * `Authorization: Bearer <sessionToken>` (API_CONTRACTS.md §2.8 "Transport";
   * ADR-015 D4).
   *
   * Every REST endpoint except `POST /api/auth/discord` requires that session,
   * and an unauthenticated response is `401 { "error": "UNAUTHENTICATED" }`
   * (§2.8 "Failure behavior").
   */
  public async get<T>(path: string): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'GET',
      headers: {
        ...ApplicationSession.getInstance().getAuthorizationHeader(),
      },
    });

    if (!response.ok) {
      throw new Error(`Request to ${path} failed with status ${response.status}`);
    }

    return (await response.json()) as T;
  }

  /**
   * A `POST` to an application endpoint, carrying the same session header.
   */
  public async post<T>(path: string, body: unknown): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...ApplicationSession.getInstance().getAuthorizationHeader(),
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      throw new Error(`Request to ${path} failed with status ${response.status}`);
    }

    return (await response.json()) as T;
  }
}
