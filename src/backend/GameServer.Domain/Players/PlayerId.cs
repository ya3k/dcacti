namespace GameServer.Domain.Players;

/// <summary>
/// The identity of the Player who owns this battle (<c>GAME_STATE.md</c> §2.8
/// <c>BattleState.PlayerId</c>; <c>ADR-014</c> decision 1).
///
/// <b>It is the account/owner identity</b> — the same value as
/// <see cref="Player.PlayerId"/> (<c>DATABASE.md</c> §1) — recorded from the
/// authenticated battle-start request at battle creation
/// (<c>API_CONTRACTS.md</c> §1, §3, the "requesting Player") and carried
/// unchanged in the state record for the battle's lifetime (§2.8 item 2).
/// It is the value the battle-end persistence path writes into
/// <c>BattleResult.PlayerId</c> (<c>DATABASE.md</c> §1), and it is never
/// re-derived from a session or from client input at battle end (§2.8 item 4).
///
/// <b>Identity only, with no combat meaning.</b> The Player is the
/// account/owner with no battle-time combat pool (<c>ADR-011</c>): this value
/// carries no stats, no resource pool, and no gameplay value of any kind
/// (§2.8 item 1). It introduces no <c>PlayerState</c> node and no lifecycle
/// <c>Status</c> field (<c>GAME_STATE.md</c> §2.0.3) — a battle's outcome
/// stays expressed as the <c>BattleWon</c>/<c>BattleLost</c> events
/// (<c>GAME_EVENTS.md</c> §2).
///
/// <b>It is not a wire member.</b> <c>GAME_STATE.md</c> §2.8 item 3 excludes
/// it from every client-facing projection — the <c>BattleStateUpdated</c>
/// stage lists, all Battle Event payloads, and the <c>GetBattleState</c>
/// snapshot (<c>SIGNALR_PROTOCOL.md</c> §4 item 4, §7.1). State added is not
/// wire exposure added; the client never receives it.
///
/// <b>Why a value type and not a bare <c>string</c>.</b> The value is
/// server-authoritative and travels into the persisted state record, so it is
/// named so a caller cannot pass a Pet instance id, a Boss id, or any other
/// string where the owning Player's identity belongs. This follows the
/// existing <see cref="GameServer.Domain.Bosses.BossId"/> and
/// <see cref="GameServer.Domain.Passives.PassiveId"/> pattern, which are the
/// same shape for the same reason.
///
/// There is no id format, scheme, or validation rule in any document — the
/// contract defines the field's meaning, not its spelling — so this type
/// imposes none and holds the identifier verbatim.
/// </summary>
/// <param name="Value">
/// The Player's identifier, exactly as <see cref="Player.PlayerId"/>
/// (<c>DATABASE.md</c> §1) and <c>API_CONTRACTS.md</c> §2.5's response
/// <c>playerId</c> hold it. It is recorded from the authenticated battle
/// creation context and is never re-derived, re-numbered, or invented by a
/// reader.
/// </param>
public readonly record struct PlayerId(string Value)
{
    /// <summary>
    /// The documented identity, for diagnostics and for a caller that has one.
    /// </summary>
    public override string ToString() => Value;
}
