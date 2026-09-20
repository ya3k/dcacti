using GameServer.Application.Battle;
using GameServer.Application.Runtime;
using GameServer.Domain.Battle;
using Microsoft.AspNetCore.SignalR;

namespace GameServer.Api.Hubs;

public record PingResponse(bool Accepted, string? ClientSequence, DateTimeOffset ServerTime);

/// <summary>
/// The initial-state push payload (<c>SIGNALR_PROTOCOL.md</c> §4).
///
/// It carries exactly the <c>GAME_STATE.md</c> §2.0 fields — <c>battleId</c>,
/// <c>turn</c>, <c>sequence</c> — and nothing else. No gameplay field may be
/// added to this record (§4.4): additional state is introduced by extending
/// <c>GAME_STATE.md</c> §2.0, not by the wire shape. No <c>Status</c>/lifecycle
/// value is carried anywhere in the protocol (§8.3).
/// </summary>
public record BattleStateUpdated(string BattleId, int Turn, int Sequence);

/// <summary>
/// Thin realtime communication boundary (SIGNALR_PROTOCOL.md, ARCHITECTURE.md §1/§2.1).
///
/// The hub translates wire messages into Application-layer calls and back. It
/// must never contain Match-3 logic, combat logic, domain calculations, Redis
/// business logic, or any gameplay rule: those belong to Domain/Application
/// (ARCHITECTURE.md §2.1, GAME_RULES.md §18, ADR-001).
///
/// In-battle hub methods (<c>Swap</c>, <c>CardCast</c>, <c>PetSkillCast</c>,
/// <c>ReceiveEvents</c>, <c>GetBattleState</c> — SIGNALR_PROTOCOL.md §2, §3, §7)
/// are intentionally NOT implemented in this task: they require battle
/// resolution, which is gameplay and out of scope.
/// </summary>
public class BattleHub : Hub
{
    private readonly RuntimeService _runtime;
    private readonly BattleStateService _battles;

    public BattleHub(RuntimeService runtime, BattleStateService battles)
    {
        _runtime = runtime;
        _battles = battles;
    }

    /// <summary>
    /// Technical connection acknowledgement.
    ///
    /// Established by the framework lifecycle below rather than a bespoke
    /// client-invoked method: <c>OnConnectedAsync</c> records the connection
    /// and returns the status to the caller, which is the smallest technical
    /// connection verification the documentation supports
    /// (SIGNALR_PROTOCOL.md §1; ADR-008 relies on the connection having a
    /// stable identity for reconnect/resync).
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var status = _runtime.OnConnected(Context.ConnectionId);

        await Clients.Caller.SendAsync("RuntimeStatusChanged", status);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var status = _runtime.OnDisconnected(Context.ConnectionId, faulted: exception is not null);

        // The client may already be gone; a failed notification is not an error
        // condition for a disconnected connection.
        if (status is not null)
        {
            try
            {
                await Clients.Caller.SendAsync("RuntimeStatusChanged", status);
            }
            catch (Exception)
            {
                // Connection already closed — nothing to report.
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Joins the caller to the battle's group and delivers the authoritative
    /// initial battle state (SIGNALR_PROTOCOL.md §1.2, §4).
    ///
    /// Trigger (§4.1): joining the group is what triggers delivery. The server
    /// pushes the state to the joining caller before any <c>ReceiveEvents</c>
    /// for that battle; there is no separate client request, and no
    /// <c>GetBattleState</c>-style invocation on this path (§4.2).
    ///
    /// Scope (§4.3): the joining caller only — this is not a group broadcast.
    ///
    /// The hub only delegates: the state comes from the Application layer, and
    /// no value is computed, derived, or adjusted here (§4.9, ARCHITECTURE.md
    /// §2.1).
    /// </summary>
    public async Task JoinBattle(string battleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        await Groups.AddToGroupAsync(Context.ConnectionId, battleId);

        var state = _battles.GetInitialStateForGroup(battleId);

        // An unknown battle yields no push. The hub defines no error contract
        // for this path: SIGNALR_PROTOCOL.md §5 defines rejection shapes for
        // in-battle action methods only.
        if (state is null)
        {
            return;
        }

        await Clients.Caller.SendAsync("BattleStateUpdated", ToPayload(state));
    }

    /// <summary>
    /// Projects the domain foundation state onto the §4 wire payload.
    ///
    /// A pure field mapping — no calculation is performed.
    /// </summary>
    private static BattleStateUpdated ToPayload(BattleState state) =>
        new(state.BattleId, state.Turn, state.Sequence);

    /// <summary>
    /// Bootstrap-era connectivity probe retained from the integration smoke
    /// test. It is technical only and carries no gameplay meaning.
    /// </summary>
    public Task<PingResponse> Ping(string? clientSequence = null)
    {
        return Task.FromResult(new PingResponse(
            Accepted: true,
            ClientSequence: clientSequence,
            ServerTime: DateTimeOffset.UtcNow));
    }
}