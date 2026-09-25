using GameServer.Domain.Players;

namespace GameServer.Domain.Pets;

/// <summary>
/// The canonical Pet Level derivation — the single formula every
/// representation of Pet Level uses (<c>PET_RULES.md</c> §5 items 1–2,
/// ADR-012 items 3–4, <c>DATABASE.md</c> §1, §3).
///
/// <code>
/// Pet.Level = clamp(
///     floor(Player.Level × PetDefinition.PetLevelMultiplier),
///     1,
///     50
/// )
///
/// 1. multiply
/// 2. floor
/// 3. clamp to [1, 50]
/// </code>
///
/// <b>One rule for stored and battle Level.</b> §5 item 2 requires this
/// operation order to apply identically to the persistent
/// <c>Pet.Level</c> column and to <c>BattleState.PetState.Level</c>
/// (<c>GAME_STATE.md</c> §2.3): the battle-time value is a snapshot of this
/// same derivation, never a second formula, rounding rule, or clamp order.
/// There is no round-after-clamp step — clamp is always last (§5 item 1).
///
/// <b>The multiplier is a caller-supplied configuration value.</b>
/// <c>PetDefinition.PetLevelMultiplier</c> is owned by configuration
/// (<c>DATABASE.md</c> §1, ADR-012 item 3) and is never hard-coded here;
/// concrete MVP multiplier numbers are deferred to balance
/// (<c>PET_RULES.md</c> §5 item 3, <c>MVP_SCOPE.md</c> §1) and are not
/// defined by this type.
///
/// <b>No Pet XP path exists.</b> Pets have no independent XP progression
/// (<c>PET_RULES.md</c> §5 item 4, ADR-012 item 6): this method is the only
/// way a Pet Level is produced. There is no XP parameter, no level-up step,
/// and no rate of increase.
/// </summary>
public static class PetLevelDerivation
{
    /// <summary>
    /// Derives Pet Level from Player Level and the Pet's configuration
    /// multiplier, using exactly <c>PET_RULES.md</c> §5's operation order:
    /// multiply → <c>decimal.Floor</c> → clamp to [1, 50].
    /// </summary>
    /// <param name="playerLevel">
    /// The owner's <c>Player.Level</c> — the persistent account attribute in
    /// the documented 1–50 range (<c>PET_RULES.md</c> §5 item 1,
    /// <c>DATABASE.md</c> §3, ADR-012 item 1).
    /// </param>
    /// <param name="petLevelMultiplier">
    /// <c>PetDefinition.PetLevelMultiplier</c> — decimal configuration,
    /// <c>&gt; 0</c> (<c>PET_RULES.md</c> §5 item 1,
    /// <c>DATABASE.md</c> §3). Values below 1 are legal (a Pet may lag its
    /// owner); zero and negative values are not.
    /// </param>
    /// <returns>
    /// The derived Pet Level, always within <c>[Player.MinLevel,
    /// Player.MaxLevel]</c> = <c>[1, 50]</c> — both ranges are the
    /// documented ones and neither is redefined here (ADR-012 item 4).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="petLevelMultiplier"/> is zero or negative —
    /// illegal per <c>PET_RULES.md</c> §5 item 1 and
    /// <c>DATABASE.md</c> §3.
    /// </exception>
    public static int Derive(int playerLevel, decimal petLevelMultiplier)
    {
        if (petLevelMultiplier <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(petLevelMultiplier),
                petLevelMultiplier,
                "PetLevelMultiplier must be greater than zero (PET_RULES.md §5, DATABASE.md §3).");
        }

        // Step 1: multiply, then step 2: floor the product (never round,
        // never ceil, never clamp before flooring — PET_RULES.md §5 item 1).
        var floored = decimal.Floor(playerLevel * petLevelMultiplier);

        // Step 3: clamp the floored result to the documented 1–50 range.
        // The bounds are Player.MinLevel/MaxLevel, which are the same
        // documented 1–50 range as Pet Level (PET_RULES.md §5 item 1).
        if (floored < Player.MinLevel)
        {
            return Player.MinLevel;
        }

        if (floored > Player.MaxLevel)
        {
            return Player.MaxLevel;
        }

        return (int)floored;
    }
}
