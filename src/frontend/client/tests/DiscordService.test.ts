import { describe, it, expect, beforeEach } from 'vitest';
import { DiscordService } from '../src/services/discord/DiscordService';

describe('DiscordService', () => {
  let service: DiscordService;

  beforeEach(() => {
    service = DiscordService.getInstance();
  });

  it('should return singleton instance', () => {
    const another = DiscordService.getInstance();
    expect(service).toBe(another);
  });

  it('should safely detect non-iframe environment and fallback gracefully', async () => {
    const context = await service.initialize('test-client-id');
    expect(context.isAvailable).toBe(false);
    expect(context.error).toContain('Not running inside Discord Activity iframe');
  });

  it('should return null authorization code when outside Discord', async () => {
    const code = await service.getAuthorizationCode();
    expect(code).toBeNull();
  });
});
