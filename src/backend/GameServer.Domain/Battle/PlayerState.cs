namespace GameServer.Domain.Battle;

/// <summary>
/// The player's battle progression state (<c>GAME_STATE.md</c> §2.2).
///
/// <code>
/// BattleState
/// └── PlayerState
///     ├── Combo        (current Combo for the Swap that just resolved;
///     │                 GAME_RULES.md §5, MATCH3_RULES.md §6)
///     └── MatchCount   (cumulative Matches this battle; GAME_RULES.md §3)
/// </code>
///
/// <b>This is the documented owner, not a new decision.</b> <c>GAME_STATE.md</c>
/// §2.2 places <c>Combo</c> and <c>MatchCount</c> here, and §2 nests
/// <c>PlayerState</c> inside <c>BattleState</c>. Both values are therefore
/// ordinary <b>Active Battle State</b>: authoritative, server-produced, and
/// written in the same single post-resolution write-back as <c>Turn</c>,
/// <c>Sequence</c>, <c>BoardState</c>, and <c>RngState</c> (§5.1). There is no
/// second representation of either value — not on <c>BattleState</c>, not on
/// <c>BoardState</c>, and not on the transient <c>ResolutionContext</c> (§0
/// item 5).
///
/// <b>Only the two fields this stage requires exist.</b> §2.2 also lists
/// <c>HP</c>/<c>MaxHP</c>, <c>ATK</c>/<c>DEF</c>/<c>Crit</c>, <c>Power</c>,
/// <c>StatusEffects[]</c>, <c>EquippedRelics[]</c>, and <c>EquippedCards[]</c>.
/// Those belong to the Combat, Passive, Relic, and Card stages and are
/// <b>not yet implemented</b>, not <b>not required</b> (§0 item 4, §2.0.5.3):
/// each is added by its own owning task, exactly as this stage adds these two.
///
/// <b>No player identifier.</b> Neither §2 nor §2.2 declares one, and both are
/// closed field lists. MVP is exactly one player per battle, identified by the
/// battle (<c>BattleId</c>); a future multiplayer record is reached by
/// extending this container, not by inventing a field today
/// (<c>MVP_SCOPE.md</c> §3 lists multiplayer as FUTURE, not designed).
///
/// <b>No derived value is stored.</b> Nothing here is computed from
/// <c>Turn</c>, <c>Sequence</c>, the board, or the resolution: both fields are
/// state, and neither can be re-derived after the fact
/// (<c>GAME_STATE.md</c> §5.2 item 2).
/// </summary>
/// <param name="Combo">
/// The Combo of the most recently <b>committed</b> Swap
/// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §5, <c>MATCH3_RULES.md</c>
/// §6).
///
/// Its rule-level value starts at <see cref="InitialCombo"/> and reads <c>0</c>
/// only before the battle's first committed Swap
/// (<c>MATCH3_RULES.md</c> §6.5 item 4). A committed Swap resets it to <c>0</c>
/// before its first Match is counted and then increments it by exactly <b>1 per
/// Match</b>, in the detection order of <c>MATCH3_RULES.md</c> §3.2 and §4.2,
/// across every pass of the cascade loop (§6.2, §6.3). It is therefore equal to
/// the number of Matches that Swap produced, and never <c>0</c> for a committed
/// Swap (§6.5 item 3).
///
/// It is <b>not</b> turn-cumulative: it does not carry across a Turn boundary,
/// and it does not accumulate over a battle (§6.1 item 4). Its next reset is the
/// next committed Swap — nothing else resets it, and the terminating pass that
/// detects no Match is not a Match and does not reset it (§6.4). A rejected Swap
/// neither resets nor changes it (§6.1 item 3, §6.5 item 2), Special Gem
/// activation, chaining, and creation never change it (§6.3.1), and no cleared
/// cell and no cascade depth is ever converted into it (§6.2 item 2).
/// </param>
/// <param name="MatchCount">
/// The cumulative number of Matches this battle has produced
/// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §3).
///
/// Its value starts at <see cref="InitialMatchCount"/> and is
/// <b>battle-cumulative</b>: it increments by exactly <b>1 per Match</b> and
/// never resets — not per Turn, not per Swap, and not per Cascade. Matches
/// produced by cascades count, and several Matches produced by one Swap each
/// count separately, because each distinct detected shape is one Match however
/// many cells it spans (<c>MATCH3_RULES.md</c> §3 item 5, §6.2 item 3).
///
/// It counts <b>Matches only</b>: a Special Gem activation, chain, or the N
/// cells one clears are not Matches and never increment it
/// (<c>MATCH3_RULES.md</c> §5.5.5 item 8, §6.3.1), and it is not a cleared-cell
/// total. It is independent of <c>Turn</c> and of <c>Sequence</c>: one committed
/// Swap advances <c>Turn</c> and <c>Sequence</c> by exactly 1 each whatever its
/// Match count is (<c>MATCH3_RULES.md</c> §8.1, §8.2). A rejected Swap does not
/// change it (§2.1.5 item 5).
///
/// No Combo bonus, damage modifier, Passive charge, or Relic trigger is
/// implemented from it here: those are gameplay consumers owned by
/// <c>COMBAT_RULES.md</c> and <c>RELIC_RULES.md</c> and remain unimplemented.
/// </param>
public readonly record struct PlayerState(int Combo, int MatchCount)
{
    /// <summary>
    /// The initial <c>MatchCount</c> for a battle that has produced no Match
    /// (<c>GAME_STATE.md</c> §2.2, <c>GAME_RULES.md</c> §3).
    /// </summary>
    public const int InitialMatchCount = 0;

    /// <summary>
    /// The initial <c>Combo</c> for a battle whose first Swap has not been
    /// committed (<c>GAME_STATE.md</c> §2.2, <c>MATCH3_RULES.md</c> §6.1 item 1).
    ///
    /// <c>0</c> is a real, publishable value here — it is the value the state
    /// reads before the first committed Swap — not an "absent" convention and
    /// not an omitted field (<c>MATCH3_RULES.md</c> §6.5 item 4,
    /// <c>GAME_STATE.md</c> §2.1.7 item 5).
    /// </summary>
    public const int InitialCombo = 0;

    /// <summary>
    /// The documented initial progression state of a newly created battle:
    /// <c>MatchCount = 0</c> and <c>Combo = 0</c> (<c>GAME_STATE.md</c> §2.2).
    ///
    /// Board generation is not a player Swap/Action and starts no Turn
    /// (<c>MATCH3_RULES.md</c> §8.1 item 4), so the state a battle begins with
    /// carries both values at their documented starting point — not <c>null</c>,
    /// not a sentinel, and not a value created lazily on the first Swap.
    ///
    /// It is a static property rather than a constant, so it is a value of this
    /// type and not a third state-bearing member: the two fields above remain
    /// the whole of the representation.
    /// </summary>
    public static PlayerState Initial => new(InitialCombo, InitialMatchCount);
}