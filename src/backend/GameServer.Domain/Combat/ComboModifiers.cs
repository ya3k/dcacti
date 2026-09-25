namespace GameServer.Domain.Combat;

/// <summary>
/// The Combo damage multipliers — <c>GAME_RULES.md</c> §5's table, which that
/// document declares canonical: "This table is canonical here. Other documents
/// must reference it, not copy it."
///
/// <code>
/// Combo 1   = 1.00×
/// Combo 2   = 1.10×
/// Combo 3   = 1.20×
/// Combo 4   = 1.35×
/// Combo 5+  = 1.50×
/// </code>
///
/// <b>This is the Combo Modifier</b> — step 2 of <c>COMBAT_RULES.md</c> §3's
/// Damage Pipeline, which multiplies Base Damage by the factor the Combo value
/// selects. It is the same table <c>GAME_RULES.md</c> §5 intends when it says
/// "Combo may modify damage".
///
/// <b>Why the factors are stored as rationals and not as <c>double</c>.</b> §5
/// states the table is "**configurable**", and <c>ELEMENT_RULES.md</c> §2.2 and
/// <c>COMBAT_RULES.md</c> §3.2 use the same wording for their own balance
/// numbers ("These are default balance values owned by this document. They are
/// configuration, not hardcoded constants"). <see cref="ElementModifiers"/>
/// resolves the identical question for the Element Modifier by storing the
/// factors as a value a caller may replace. This type follows that precedent
/// and stores each factor as an integer numerator over
/// <see cref="Denominator"/> = <c>100</c>, so the table is one editable
/// declaration rather than a literal repeated at each arithmetic site. Every
/// documented factor is an exact multiple of <c>1/100</c> (<c>1.00</c>,
/// <c>1.10</c>, <c>1.20</c>, <c>1.35</c>, <c>1.50</c>), so the representation is
/// lossless for the documented values and the pipeline's arithmetic stays
/// exact integer arithmetic: no floating-point rounding is introduced anywhere
/// between Base Damage and Final Damage, and the documented truncation of §3
/// step 6 is therefore the <i>only</i> place a fractional value is discarded.
///
/// <b>The lookup is total, and there is no upper bound to fall back on.</b> §5
/// gives <c>Combo 5+</c> as one row, so every Combo at or above
/// <see cref="HighestEnumeratedCombo"/> selects the same factor. A Combo below
/// <see cref="LowestCombo"/> is not a Combo §5 defines — a committed Swap always
/// leaves <c>Combo ≥ 1</c> (<c>MATCH3_RULES.md</c> §6.5 item 3) and <c>0</c> is
/// the pre-resolution value that never reaches the pipeline — so it has no
/// factor and this type throws rather than pricing an undefined row.
/// </summary>
public readonly record struct ComboModifiers
{
    /// <summary>
    /// The denominator every factor is expressed over (<c>GAME_RULES.md</c> §5).
    ///
    /// <c>100</c> is the smallest denominator that represents all five
    /// documented factors exactly (<c>1.00</c>, <c>1.10</c>, <c>1.20</c>,
    /// <c>1.35</c>, <c>1.50</c> are <c>100</c>, <c>110</c>, <c>120</c>,
    /// <c>135</c>, <c>150</c> hundredths).
    /// </summary>
    public const int Denominator = 100;

    /// <summary>
    /// The lowest Combo the table has a row for (<c>GAME_RULES.md</c> §5:
    /// "Combo starts at 1 on the first Match").
    /// </summary>
    public const int LowestCombo = 1;

    /// <summary>
    /// The highest Combo with a row of its own (<c>GAME_RULES.md</c> §5:
    /// <c>Combo 5+</c>). Every higher Combo reads the same row.
    /// </summary>
    public const int HighestEnumeratedCombo = 5;

    /// <summary>
    /// The default table of <c>GAME_RULES.md</c> §5 — <c>1.00</c> / <c>1.10</c> /
    /// <c>1.20</c> / <c>1.35</c> / <c>1.50</c> for Combo 1–4 and Combo 5+
    /// respectively.
    ///
    /// It is a static property rather than a constant, for the same reason
    /// <see cref="Elements.ElementModifiers.Default"/> is: §5 makes the table
    /// configurable, so a balance change is a value a caller passes rather than a
    /// recompilation of every call site. The default is reached explicitly, so no
    /// resolution can be affected by another resolution's balance edit.
    /// </summary>
    public static ComboModifiers Default => new(100, 110, 120, 135, 150);

    /// <summary>
    /// Creates a table from the five documented factors, each as a numerator over
    /// <see cref="Denominator"/> (<c>GAME_RULES.md</c> §5).
    /// </summary>
    /// <param name="combo1">The factor for Combo 1 — §5's <c>1.00×</c>.</param>
    /// <param name="combo2">The factor for Combo 2 — §5's <c>1.10×</c>.</param>
    /// <param name="combo3">The factor for Combo 3 — §5's <c>1.20×</c>.</param>
    /// <param name="combo4">The factor for Combo 4 — §5's <c>1.35×</c>.</param>
    /// <param name="combo5Plus">
    /// The factor for Combo 5 and above — §5's <c>1.50×</c>.
    /// </param>
    public ComboModifiers(int combo1, int combo2, int combo3, int combo4, int combo5Plus)
    {
        Combo1 = combo1;
        Combo2 = combo2;
        Combo3 = combo3;
        Combo4 = combo4;
        Combo5Plus = combo5Plus;
    }

    /// <summary>The factor for Combo 1, over <see cref="Denominator"/> (default <c>100</c>).</summary>
    public int Combo1 { get; init; }

    /// <summary>The factor for Combo 2, over <see cref="Denominator"/> (default <c>110</c>).</summary>
    public int Combo2 { get; init; }

    /// <summary>The factor for Combo 3, over <see cref="Denominator"/> (default <c>120</c>).</summary>
    public int Combo3 { get; init; }

    /// <summary>The factor for Combo 4, over <see cref="Denominator"/> (default <c>135</c>).</summary>
    public int Combo4 { get; init; }

    /// <summary>
    /// The factor for Combo 5 and every higher Combo, over
    /// <see cref="Denominator"/> (default <c>150</c>).
    /// </summary>
    public int Combo5Plus { get; init; }

    /// <summary>
    /// The numerator of the documented factor for a Combo value — the table
    /// lookup <c>COMBAT_RULES.md</c> §3 step 2 performs.
    ///
    /// It is returned as the numerator over <see cref="Denominator"/> rather than
    /// as a <c>double</c> so that the caller multiplies exactly
    /// (<c>damage × Numerator / Denominator</c>) and introduces no rounding of
    /// its own.
    /// </summary>
    /// <param name="combo">
    /// The Combo of the Swap whose damage instance is being priced — the
    /// <c>BattleState.Combo</c> this committed Swap produced
    /// (<c>GAME_STATE.md</c> §2.2, <c>MATCH3_RULES.md</c> §6).
    /// </param>
    /// <returns>
    /// The numerator of the factor: <see cref="Combo1"/> … <see cref="Combo4"/>
    /// for Combo 1–4, and <see cref="Combo5Plus"/> for Combo 5 and above.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="combo"/> is below <see cref="LowestCombo"/>. <c>GAME_RULES.md</c>
    /// §5 defines no row below Combo 1, and no factor may be invented for it.
    /// </exception>
    public int NumeratorFor(int combo) => combo switch
    {
        // GAME_RULES.md §5: Combo 1 — the first Match of a Swap, no bonus.
        1 => Combo1,

        // GAME_RULES.md §5: Combo 2.
        2 => Combo2,

        // GAME_RULES.md §5: Combo 3.
        3 => Combo3,

        // GAME_RULES.md §5: Combo 4.
        4 => Combo4,

        // GAME_RULES.md §5: "Combo 5+" — one row for every higher Combo, which is
        // why the guard below is a floor and not a range.
        _ when combo >= HighestEnumeratedCombo => Combo5Plus,

        _ => throw new ArgumentOutOfRangeException(
            nameof(combo),
            combo,
            "GAME_RULES.md §5's table begins at Combo 1; no factor is defined "
            + "below it, and a committed Swap always leaves Combo >= 1 "
            + "(MATCH3_RULES.md §6.5 item 3)."),
    };
}
