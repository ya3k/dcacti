export interface HealthStatus {
  status: string;
}

export interface DiscordAuthResponse {
  sessionToken: string;
  playerId: string;
}

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

    return await response.json();
  }
}
