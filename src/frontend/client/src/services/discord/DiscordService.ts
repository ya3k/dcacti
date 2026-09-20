import { DiscordSDK } from '@discord/embedded-app-sdk';

export interface DiscordContext {
  isAvailable: boolean;
  user?: {
    id: string;
    username: string;
  };
  error?: string;
}

export class DiscordService {
  private static instance: DiscordService | null = null;
  private discordSdk: DiscordSDK | null = null;
  private context: DiscordContext = { isAvailable: false };

  private constructor() {}

  public static getInstance(): DiscordService {
    if (!DiscordService.instance) {
      DiscordService.instance = new DiscordService();
    }
    return DiscordService.instance;
  }

  public async initialize(clientId?: string): Promise<DiscordContext> {
    const isIframe = typeof window !== 'undefined' && window.self !== window.top;
    const targetClientId = clientId || import.meta.env.VITE_DISCORD_CLIENT_ID || '123456789012345678';

    if (!isIframe) {
      this.context = {
        isAvailable: false,
        error: 'Not running inside Discord Activity iframe (Local Development Mode)',
      };
      return this.context;
    }

    try {
      this.discordSdk = new DiscordSDK(targetClientId);
      await this.discordSdk.ready();

      this.context = {
        isAvailable: true,
      };
      return this.context;
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      this.context = {
        isAvailable: false,
        error: `Discord SDK initialization failed: ${message}`,
      };
      return this.context;
    }
  }

  public async getAuthorizationCode(): Promise<string | null> {
    if (!this.discordSdk || !this.context.isAvailable) {
      return null;
    }

    try {
      const { code } = await this.discordSdk.commands.authorize({
        client_id: this.discordSdk.clientId,
        response_type: 'code',
        state: '',
        prompt: 'none',
        scope: ['identify', 'guilds'],
      });
      return code;
    } catch {
      return null;
    }
  }

  public getContext(): DiscordContext {
    return this.context;
  }
}
