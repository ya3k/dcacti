using Microsoft.AspNetCore.SignalR;

namespace GameServer.Api.Hubs;

public record PingResponse(bool Accepted, string? ClientSequence, DateTimeOffset ServerTime);

public class BattleHub : Hub
{
    public Task<PingResponse> Ping(string? clientSequence = null)
    {
        return Task.FromResult(new PingResponse(
            Accepted: true,
            ClientSequence: clientSequence,
            ServerTime: DateTimeOffset.UtcNow));
    }
}
