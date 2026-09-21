namespace GameServer.Domain.Match3;

/// <summary>
/// The three MVP Special Gem types (<c>GAME_STATE.md</c> §2.1.4 item 1,
/// <c>MATCH3_RULES.md</c> §5.2–§5.4).
///
/// These are the complete set: <c>MATCH3_RULES.md</c> §5.3 item 3 defines no
/// Match-6+ tier and introduces no fourth type, and <c>MVP_SCOPE.md</c> §1 lists
/// no other.
///
/// A Special Gem type is <b>not</b> a Gem type. It does not extend
/// <see cref="GemType"/>, it is never a fifth member of the four-type value
/// domain (<c>MATCH3_RULES.md</c> §1.1), and Match Detection never compares it:
/// the cell a Special Gem occupies still holds one of the four Gem types, and
/// that is the value detection reads (<c>GAME_STATE.md</c> §2.1.8 item 6,
/// <c>MATCH3_RULES.md</c> §5.5.4 item 4).
/// </summary>
public enum SpecialGemType
{
    /// <summary>
    /// Line Clear Gem (<c>MATCH3_RULES.md</c> §5.2) — created by a straight
    /// Match of exactly 4, or by an L/T arm of length exactly 4
    /// (§5.4 item 4). Clears the whole row or column through its own cell.
    /// </summary>
    LineClear = 0,

    /// <summary>
    /// Burst Gem (<c>MATCH3_RULES.md</c> §5.3) — created by a straight Match of
    /// length 5 or more, or by an L/T arm of length 5 or more (§5.4 item 4).
    /// Clears the 3×3 square centred on its own cell.
    /// </summary>
    Burst = 1,

    /// <summary>
    /// Area Gem (<c>MATCH3_RULES.md</c> §5.4) — created by an L/T shape, always
    /// at the intersection. Clears its own cell plus the four orthogonally
    /// adjacent cells.
    /// </summary>
    Area = 2,
}

/// <summary>
/// The orientation of a Line Clear Gem (<c>GAME_STATE.md</c> §2.1.4 item 2,
/// <c>MATCH3_RULES.md</c> §5.2 item 3).
///
/// It is fixed at creation from the orientation of the straight line that
/// created the Gem and never changes afterwards — including when the Gem later
/// falls or is displaced by gravity (<c>MATCH3_RULES.md</c> §5.2 item 3,
/// §4.4 item 8).
/// </summary>
public enum SpecialGemOrientation
{
    /// <summary>The creating line was a horizontal primitive — clears a row.</summary>
    Horizontal = 0,

    /// <summary>The creating line was a vertical primitive — clears a column.</summary>
    Vertical = 1,
}

/// <summary>
/// A Special Gem — the optional occupant metadata of one <c>Cells[64]</c> entry
/// (<c>GAME_STATE.md</c> §2.1.4).
///
/// <code>
/// SpecialGem
/// ├── Type            LineClear | Burst | Area
/// └── Orientation?    Horizontal | Vertical   (Line Clear only)
/// </code>
///
/// The model carries nothing else. There is no identity, id, creation pass,
/// cascade depth, age, armed/ready flag, remaining charge, creation order, or
/// cell index: a Special Gem's behavior is a pure function of its type, its
/// orientation, and the cell it currently occupies
/// (<c>GAME_STATE.md</c> §2.1.3 item 4, §2.1.4 item 4). The index <i>is</i> the
/// position in <c>Cells[64]</c>, so no stored index can disagree with the array
/// it lives in (§2.1.3 item 5).
/// </summary>
/// <param name="Type">
/// One of the three MVP types (<c>GAME_STATE.md</c> §2.1.4 item 1).
/// </param>
/// <param name="Orientation">
/// Present <b>if and only if</b> <see cref="Type"/> is
/// <see cref="SpecialGemType.LineClear"/> (<c>GAME_STATE.md</c> §2.1.4 item 2).
/// It is the only conditional value in the model: <see cref="SpecialGemType.Burst"/>
/// and <see cref="SpecialGemType.Area"/> are orientation-free, so an orientation
/// on those types would be a value no rule reads.
/// </param>
public readonly record struct SpecialGem(SpecialGemType Type, SpecialGemOrientation? Orientation)
{
    /// <summary>
    /// A horizontal Line Clear Gem (<c>MATCH3_RULES.md</c> §5.2 items 2, 3).
    /// </summary>
    public static SpecialGem LineClearHorizontal() =>
        new(SpecialGemType.LineClear, SpecialGemOrientation.Horizontal);

    /// <summary>
    /// A vertical Line Clear Gem (<c>MATCH3_RULES.md</c> §5.2 items 2, 3).
    /// </summary>
    public static SpecialGem LineClearVertical() =>
        new(SpecialGemType.LineClear, SpecialGemOrientation.Vertical);

    /// <summary>
    /// A Line Clear Gem oriented from the creating line's own orientation
    /// (<c>MATCH3_RULES.md</c> §5.2 item 3).
    /// </summary>
    /// <param name="orientation">
    /// The creating primitive's orientation — horizontal for a row run, vertical
    /// for a column run. This is the only orientation source.
    /// </param>
    public static SpecialGem LineClear(SpecialGemOrientation orientation) =>
        new(SpecialGemType.LineClear, orientation);

    /// <summary>A Burst Gem (<c>MATCH3_RULES.md</c> §5.3 item 2).</summary>
    public static SpecialGem Burst() => new(SpecialGemType.Burst, null);

    /// <summary>An Area Gem (<c>MATCH3_RULES.md</c> §5.4 item 3).</summary>
    public static SpecialGem Area() => new(SpecialGemType.Area, null);

    /// <summary>
    /// True when this metadata is well formed: orientation present exactly for a
    /// Line Clear Gem (<c>GAME_STATE.md</c> §2.1.4 item 2).
    /// </summary>
    public bool IsWellFormed =>
        Type == SpecialGemType.LineClear ? Orientation is not null : Orientation is null;
}