import { describe, it, expect, beforeEach } from 'vitest';
import { SignalRService } from '../src/services/realtime/SignalRService';
import * as signalR from '@microsoft/signalr';

describe('SignalRService', () => {
  let service: SignalRService;

  beforeEach(() => {
    service = SignalRService.getInstance();
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
});
