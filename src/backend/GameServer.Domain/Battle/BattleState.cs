namespace GameServer.Domain.Battle;

/// <summary>
/// Battle State Foundation — the minimal authoritative battle state
/// (<c>GAME_STATE.md</c> §2.0).
///
/// It is the staged subset of the eventual full <c>BattleState</c>
/// (<c>GAME_STATE.md</c> §2), which is not replaced, reduced, or redefined by
/// this type: the remaining §2 fields (<c>RngSeed</c>/<c>RngState</c>,
/// <c>BoardState</c>, <c>PlayerState</c>, <c>PetState</c>, <c>BossState</c>)
/// are added by their owning systems as those systems are implemented.
///
/// This type is deliberately minimal and framework-independent
/// (<c>ARCHITECTURE.md</c> §2.1): it references no ASP.NET Core, SignalR,
/// EF Core, Redis, HTTP, Phaser, or Discord concern.
///
/// It contains no <c>Status</c> field and no lifecycle enum
/// (<c>GAME_STATE.md</c> §2.0.3): a battle has no lifecycle state machine, and
/// battle outcome is expressed as the <c>BattleWon</c>/<c>BattleLost</c> events
/// (<c>GAME_EVENTS.md</c> §2), not as state.
/// </summary>
/// <param name="BattleId">
/// Identity of the battle session (<c>GAME_STATE.md</c> §2.0.1). It scopes the
/// client's SignalR group membership (<c>SIGNALR_PROTOCOL.md</c> §1.2) and
/// carries no gameplay content — it selects no Pet, Boss, or loadout.
/// </param>
/// <param name="Turn">
/// Current Turn number (<c>GAME_STATE.md</c> §2.0.1, §2, <c>GAME_RULES.md</c>
/// §2). <c>0</c> means no player Swap/Action has yet been successfully
/// resolved (§2.0.2). This type defines no Turn increment rule: §2.0.2 leaves
/// that to the task that implements action resolution.
/// </param>
/// <param name="Sequence">
/// Monotonic resolution counter (<c>GAME_STATE.md</c> §2.0.1, §2, §5).
/// <c>0</c> means no authoritative action resolution has yet occurred, and is
/// therefore the only valid value until the first resolution succeeds
/// (§2.0.2). Not to be confused with
/// <c>GameServer.Application.Runtime.RuntimeStatus.Sequence</c>, which is a
/// technical connection counter and not battle state.
/// </param>
public sealed record BattleState(string BattleId, int Turn, int Sequence)
{
    /// <summary>
    /// Initial <c>Turn</c> for a battle with no resolved action
    /// (<c>GAME_STATE.md</c> §2.0.2).
    /// </summary>
    public const int InitialTurn = 0;

    /// <summary>
    /// Initial <c>Sequence</c> for a battle with no resolved action
    /// (<c>GAME_STATE.md</c> §2.0.2, §5).
    /// </summary>
    public const int InitialSequence = 0;

    /// <summary>
    /// Creates the foundation state for a newly created battle session:
    /// <c>Turn = 0</c>, <c>Sequence = 0</c> (<c>GAME_STATE.md</c> §2.0.2).
    ///
    /// Authoritative state is server-produced (<c>GAME_RULES.md</c> §18,
    /// <c>ADR-001</c>); this factory is the only place a foundation battle
    /// begins.
    /// </summary>
    public static BattleState Create(string battleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        return new BattleState(battleId, InitialTurn, InitialSequence);
    }
}