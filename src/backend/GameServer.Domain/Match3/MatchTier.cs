namespace GameServer.Domain.Match3;

/// <summary>
/// The match tier of the Gems a step consumed — the tier whose multiplier
/// <c>COMBAT_RULES.md</c> §2 applies to that step's resource output.
///
/// The four tiers are exactly the four rows of `COMBAT_RULES.md` §2's
/// multiplier table (Match 3 / Match 4 / Match 5 / L/T). There is no sixth tier:
/// a straight run of 6 or more is a Match 5 and uses <see cref="Match5"/>
/// (<c>MATCH3_RULES.md</c> §5.3 item 3, <c>COMBAT_RULES.md</c> §2 item 4).
///
/// <see cref="Base"/> is the tier a Special Gem activation's cleared cells
/// generate at. It is <b>not</b> a fifth row of the table: an activation is not
/// a Match and has no match tier, so its cells generate at the Match-3 base rate
/// (<c>COMBAT_RULES.md</c> §2 item 1, <c>MATCH3_RULES.md</c> §5.7 item 6). This
/// member exists so that "no tier" is a nameable value rather than a magic
/// multiplier, and its rate is intentionally the same as <see cref="Match3"/>.
/// </summary>
public enum MatchTier
{
    /// <summary>
    /// No match tier — a Special Gem activation's cleared cells
    /// (<c>COMBAT_RULES.md</c> §2 item 1, <c>MATCH3_RULES.md</c> §5.7 item 6).
    /// Generates at the Match-3 base rate (<c>1.0×</c>).
    /// </summary>
    Base = 0,

    /// <summary>Match 3 — <c>1.0×</c> (<c>COMBAT_RULES.md</c> §2).</summary>
    Match3 = 1,

    /// <summary>Match 4 — <c>1.5×</c> (<c>COMBAT_RULES.md</c> §2).</summary>
    Match4 = 2,

    /// <summary>
    /// Match 5 — <c>2.0×</c> (<c>COMBAT_RULES.md</c> §2). A run of 6 or more is
    /// also this tier (<c>MATCH3_RULES.md</c> §5.3 item 3).
    /// </summary>
    Match5 = 3,

    /// <summary>L/T — <c>1.25×</c> (<c>COMBAT_RULES.md</c> §2).</summary>
    Lt = 4,
}

/// <summary>
/// The match-tier multipliers of <c>COMBAT_RULES.md</c> §2.
///
/// §2 states of its own table that it "is the sole source of truth for
/// match-tier output, and it is the rate every other document references", and
/// that its values "are configuration, not hardcoded constants". They are held
/// here as exact rational numerators over a fixed denominator so that the
/// documented values — including the two that are not whole numbers — are
/// applied without floating-point drift.
///
/// <code>
/// Match 3   → 1.0×
/// Match 4   → 1.5×
/// Match 5   → 2.0×
/// L/T       → 1.25×
/// </code>
///
/// The L/T rate is <c>1.25×</c> and is deliberately <b>lower</b> than the Match-4
/// rate: <c>MATCH3_RULES.md</c> §5.4 item 3 rejects a larger L/T clear partly
/// because it would contradict "its <c>1.25×</c> multiplier position". A tier
/// ladder that made L/T the highest tier would contradict the owning document.
/// </summary>
public static class MatchTierMultipliers
{
    /// <summary>
    /// The denominator every documented multiplier is expressed over. Each
    /// multiplier below is an exact multiple of <c>0.25</c>, so a denominator of
    /// <c>4</c> represents all of them without rounding.
    /// </summary>
    public const int Denominator = 4;

    /// <summary>
    /// The numerator of a tier's multiplier over <see cref="Denominator"/>
    /// (<c>COMBAT_RULES.md</c> §2).
    /// </summary>
    public static int NumeratorFor(MatchTier tier) => tier switch
    {
        MatchTier.Base or MatchTier.Match3 => 4,    // 1.0×
        MatchTier.Match4 => 6,                      // 1.5×
        MatchTier.Match5 => 8,                      // 2.0×
        MatchTier.Lt => 5,                          // 1.25×
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Not a documented match tier."),
    };

    /// <summary>
    /// The documented ladder in ascending tier order, for the tier a straight
    /// run of the given length forms (<c>MATCH3_RULES.md</c> §5.5.2, §5.3
    /// item 3).
    /// </summary>
    /// <param name="length">The run's number of cells, which is at least 3.</param>
    public static MatchTier ForStraightRun(int length) => length switch
    {
        < 3 => throw new ArgumentOutOfRangeException(
            nameof(length), length, "A Match has a minimum length of 3 (MATCH3_RULES.md §3 item 2)."),
        3 => MatchTier.Match3,
        4 => MatchTier.Match4,
        // §5.3 item 3: a run of 6 or more is a Match 5; no separate tier exists.
        _ => MatchTier.Match5,
    };
}
