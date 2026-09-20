import * as signalR from '@microsoft/signalr';

export interface PingResult {
  accepted: boolean;
  clientSequence?: string;
  serverTime: string;
}

export class SignalRService {
  private static instance: SignalRService | null = null;
  private connection: signalR.HubConnection | null = null;

  private constructor() {}

  public static getInstance(): SignalRService {
    if (!SignalRService.instance) {
      SignalRService.instance = new SignalRService();
    }
    return SignalRService.instance;
  }

  public async connect(hubUrl: string = '/hubs/battle'): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    await this.connection.start();
  }

  public async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  public getConnectionState(): signalR.HubConnectionState {
    return this.connection ? this.connection.state : signalR.HubConnectionState.Disconnected;
  }

  public isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }

  public async ping(clientSequence: string = `ping_${Date.now()}`): Promise<PingResult> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR connection is not established.');
    }

    return await this.connection.invoke<PingResult>('Ping', clientSequence);
  }
}
