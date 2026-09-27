namespace GameServer.Domain.Battle;

/// <summary>
/// A battle's outcome — the closed two-value set <c>DATABASE.md</c> §1 and
/// <c>GAME_EVENTS.md</c> §2 fix.
///
/// <code>
/// BattleWon   →  victory
/// BattleLost  →  defeat
/// </code>
///
/// <b>The values are <c>victory</c> and <c>defeat</c>, and nothing else.</b>
/// <c>GAME_EVENTS.md</c> §2 owns the value set; <c>DATABASE.md</c> §1's
/// <c>BattleResult.Outcome</c>, <c>API_CONTRACTS.md</c> §4's REST
/// <c>outcome</c>, and <c>SIGNALR_PROTOCOL.md</c> §3.2.19's wire
/// <c>outcome</c> all reference the same two values for the same battle
/// (<c>API_CONTRACTS.md</c> §4 notes 1 and 4). TASK-050's human Decision C
/// fixed that single vocabulary repository-wide, so there is no second
/// spelling: not <c>Won</c>/<c>Lost</c>, not <c>win</c>/<c>loss</c>, not
/// <c>success</c>/<c>failure</c>, and no <c>draw</c>.
///
/// <b>Which value applies is decided by the resolution, not here.</b>
/// <c>GAME_RULES.md</c> §1.4 ends the battle when either side reaches
/// <c>0</c> HP, and the resolution reports it as <c>BattleWon</c> or
/// <c>BattleLost</c> (<c>GAME_EVENTS.md</c> §2); this type is the closed set
/// those reports map onto, and it defines no terminal condition of its own.
/// </summary>
public enum BattleOutcome
{
    /// <summary>
    /// The battle ended with the Boss defeated — <c>BattleWon</c>
    /// (<c>GAME_EVENTS.md</c> §2, <c>GAME_RULES.md</c> §1.4), stored as
    /// <c>"victory"</c> (<c>DATABASE.md</c> §1).
    /// </summary>
    Victory,

    /// <summary>
    /// The battle ended with the player's side defeated — <c>BattleLost</c>
    /// (<c>GAME_EVENTS.md</c> §2, <c>GAME_RULES.md</c> §1.4), stored as
    /// <c>"defeat"</c> (<c>DATABASE.md</c> §1).
    /// </summary>
    Defeat,
}

/// <summary>
/// The persisted and wire spellings of <see cref="BattleOutcome"/>
/// (<c>DATABASE.md</c> §1, <c>GAME_EVENTS.md</c> §2, <c>API_CONTRACTS.md</c> §4).
///
/// <b>Why a mapping rather than <c>ToString()</c>.</b> The stored values are
/// lowercase <c>"victory"</c>/<c>"defeat"</c>, while the C# enum member names
/// are the conventional PascalCase. Writing the enum's identifier would persist
/// <c>"Victory"</c> — a value no document defines — so the contract's own
/// spelling is stated once here and used by both persistence and the REST
/// projection, exactly as <c>GemTypes.ToContractName</c> states the Gem type
/// vocabulary for the wire (<c>SIGNALR_PROTOCOL.md</c> §3.2.4).
/// </summary>
public static class BattleOutcomes
{
    /// <summary>
    /// The stored/REST value for <see cref="BattleOutcome.Victory"/>
    /// (<c>GAME_EVENTS.md</c> §2, <c>DATABASE.md</c> §1).
    /// </summary>
    public const string Victory = "victory";

    /// <summary>
    /// The stored/REST value for <see cref="BattleOutcome.Defeat"/>
    /// (<c>GAME_EVENTS.md</c> §2, <c>DATABASE.md</c> §1).
    /// </summary>
    public const string Defeat = "defeat";

    /// <summary>
    /// The contract value of an outcome (<c>DATABASE.md</c> §1,
    /// <c>API_CONTRACTS.md</c> §4 note 4) — never the enum's identifier.
    /// </summary>
    /// <param name="outcome">The outcome to spell.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not one of the two documented outcomes. The set is closed,
    /// so an undefined value is refused rather than spelled as something the
    /// contract does not define.
    /// </exception>
    public static string ToContractValue(BattleOutcome outcome) => outcome switch
    {
        BattleOutcome.Victory => Victory,
        BattleOutcome.Defeat => Defeat,
        _ => throw new ArgumentOutOfRangeException(
            nameof(outcome),
            outcome,
            "DATABASE.md §1 defines exactly two battle outcomes — \"victory\" and \"defeat\"."),
    };

    /// <summary>
    /// The outcome a stored contract value denotes
    /// (<c>DATABASE.md</c> §1) — the inverse of
    /// <see cref="ToContractValue"/>, used when a row is read back.
    /// </summary>
    /// <param name="value">The stored <c>Outcome</c> value.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The stored value is not one of the two the contract defines. It is
    /// refused rather than mapped onto a nearest outcome: a row holding an
    /// undefined outcome is not evidence that one of the two happened.
    /// </exception>
    public static BattleOutcome FromContractValue(string value) => value switch
    {
        Victory => BattleOutcome.Victory,
        Defeat => BattleOutcome.Defeat,
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            "DATABASE.md §1 stores exactly two battle outcomes — \"victory\" and \"defeat\"."),
    };
}
