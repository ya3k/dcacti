namespace GameServer.Domain.Match3;

/// <summary>
/// One entry of <c>BoardState.Cells[64]</c> — the cell's occupant together with
/// that occupant's Special Gem state (<c>GAME_STATE.md</c> §2.1.1).
///
/// <code>
/// entry at index i
/// ├── GemType                 ATK | DEF | HP | POWER   (MATCH3_RULES.md §1.1)
/// ── SpecialGem?             absent → an ordinary Gem
///                             present → a Special Gem (type; plus orientation
///                             for a Line Clear Gem — §2.1.4 item 2)
/// </code>
///
/// A Special Gem is <b>metadata on a cell's Gem, not a substitute for it</b>
/// (<c>GAME_STATE.md</c> §2.1.3 item 1). The cell's Gem type is always one of
/// the four documented types, whether or not the cell holds a Special Gem: the
/// Gem type is what Match Detection compares when "its own colour is matched
/// normally" and what <c>GemMatched</c> reports, so it exists for a Special
/// Gem's cell exactly as for any other.
///
/// One Special Gem per cell, at most (<c>GAME_STATE.md</c> §2.1.3 item 3,
/// <c>MATCH3_RULES.md</c> §5.5.4 item 3). The type cannot express two, which is
/// the documented rule, not a limitation.
/// </summary>
/// <param name="GemType">
/// The cell's Gem type — always one of the four (<c>MATCH3_RULES.md</c> §1.1).
/// For a cell holding a Special Gem this is the type of the Gem the Special Gem
/// replaced (<c>MATCH3_RULES.md</c> §5.5.4 item 4,
/// <c>GAME_STATE.md</c> §2.1.3 item 2).
/// </param>
/// <param name="SpecialGem">
/// The cell's Special Gem, or <c>null</c> for an ordinary Gem. This is the
/// documented "absent" representation (<c>GAME_STATE.md</c> §2.1.7 item 3): the
/// member is omitted, never a sentinel type and never a slot marker.
/// </param>
public readonly record struct Cell(GemType GemType, SpecialGem? SpecialGem)
{
    /// <summary>An ordinary Gem with no Special Gem (<c>GAME_STATE.md</c> §2.1.7 item 3).</summary>
    public static Cell Ordinary(GemType gemType) => new(gemType, null);

    /// <summary>A cell carrying a Special Gem alongside its Gem type.</summary>
    public static Cell WithSpecial(GemType gemType, SpecialGem specialGem) => new(gemType, specialGem);

    /// <summary>True when this cell holds a Special Gem.</summary>
    public bool HasSpecialGem => SpecialGem is not null;

    /// <summary>
    /// The Special Gem's type, or <c>null</c> when this cell holds an ordinary
    /// Gem. Provided so activation and reporting do not each re-implement the
    /// nullable unwrap.
    /// </summary>
    public SpecialGemType? SpecialGemTypeOrNull => SpecialGem?.Type;
}