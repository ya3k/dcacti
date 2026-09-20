using System.Collections.Concurrent;
using GameServer.Domain.Battle;

namespace GameServer.Application.Battle;

/// <summary>
/// Application-layer boundary for the Battle State Foundation lifecycle
/// (<c>GAME_STATE.md</c> §2.0).
///
/// Responsibility: create and hold the authoritative foundation state of a
/// battle session, and hand it to the realtime boundary when a client joins
/// that battle's group (<c>SIGNALR_PROTOCOL.md</c> §1.2, §4.1). It performs
/// sequencing and coordination only — no game rule logic
/// (<c>ARCHITECTURE.md</c> §2.1).
///
/// It must never:
/// <list type="bullet">
/// <item>implement Match-3, combat, or any domain rule,</item>
/// <item>compute an authoritative gameplay value (<c>GAME_RULES.md</c> §18,
/// <c>ADR-001</c>),</item>
/// <item>persist foundation state (<c>REDIS_STATE.md</c> §7.1: Foundation State
/// is NOT written to Redis; <c>GAME_STATE.md</c> §2.0.4),
/// <item>emit Battle Events (<c>GAME_EVENTS.md</c> — owned by battle
/// resolution).</item>
/// </list>
///
/// The session registry here is deliberately not a battle-state store in the
/// <c>REDIS_STATE.md</c> sense, and not an alternative to it. It holds only the
/// §2.0 subset — no full <c>BattleState</c> (§2) can be expressed here — and it
/// is process-local and safe to lose, exactly as §2.0.4 describes: Foundation
/// State is not persisted to Redis or PostgreSQL.
/// </summary>
public sealed class BattleStateService
{
    private readonly ConcurrentDictionary<string, BattleState> _battles = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates the authoritative foundation state for a new battle session
    /// (<c>GAME_STATE.md</c> §2.0.2): <c>Turn = 0</c>, <c>Sequence = 0</c>.
    ///
    /// This is not a battle-creation endpoint or hub method. Battle creation
    /// remains <c>POST /api/battle/start</c> (<c>API_CONTRACTS.md</c> §3), which
    /// is unchanged by Battle State Foundation and which no foundation-stage
    /// battle can satisfy, because the loadout systems it validates do not exist
    /// yet (<c>REDIS_STATE.md</c> §7.3, <c>ROADMAP.md</c> §1 Phase 2).
    /// </summary>
    public BattleState CreateBattle(string battleId)
    {
        var state = BattleState.Create(battleId);

        _battles[battleId] = state;
        return state;
    }

    /// <summary>
    /// Returns the authoritative foundation state for a battle, or <c>null</c>
    /// when no session with that id exists.
    /// </summary>
    public BattleState? GetBattle(string battleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(battleId);

        return _battles.TryGetValue(battleId, out var state) ? state : null;
    }

    /// <summary>
    /// Returns the state to deliver to a client that has just joined a battle's
    /// group, or <c>null</c> when the group names no known battle.
    ///
    /// This is the server-side half of the initial state push
    /// (<c>SIGNALR_PROTOCOL.md</c> §4): joining the group is what triggers
    /// delivery, so the caller asks for the state rather than invoking a
    /// separate request. Foundation State has no resolutions
    /// (<c>GAME_STATE.md</c> §2.0.2), so the returned state is always the
    /// initial one — no value is derived, adjusted, or recomputed here.
    /// </summary>
    public BattleState? GetInitialStateForGroup(string battleId)
    {
        return GetBattle(battleId);
    }

    /// <summary>Number of battle sessions currently held.</summary>
    public int ActiveBattleCount => _battles.Count;
}