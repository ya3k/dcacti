namespace GameServer.Domain.Elements;

/// <summary>
/// The element damage modifier configuration — the three factors
/// <c>COMBAT_RULES.md</c> §3 step 3 multiplies into Base Damage after the Combo
/// Modifier.
///
/// <code>
/// Advantage     = 1.50  (ELEMENT_RULES.md §2.2)
/// Neutral       = 1.00  (ELEMENT_RULES.md §2.2)
/// Disadvantage  = 0.75  (ELEMENT_RULES.md §2.2)
/// </code>
///
/// <b>Why these are a value and not <c>const</c> fields.</b> §2.2 states the
/// modifiers "are configuration values (see COMBAT_RULES.md for where they apply
/// in the damage pipeline), not hardcoded constants. Changing them is a balance
/// change and does not require a rule-conflict process unless it changes the
/// *relationship* structure itself (e.g. adding Tương Sinh)." <c>COMBAT_RULES.md</c>
/// §2 and §3.3 apply the same wording to their own balance numbers ("These are
/// default balance values owned by this document. They are configuration, not
/// hardcoded constants"). A <c>const</c> is compiled into every call site and can
/// only be changed by editing and recompiling the consuming code, which is
/// precisely the "hardcoded constant" §2.2 rejects. This record is the single
/// place the three default factors are stated, so a balance change is a change
/// here and nowhere else.
///
/// <b>What is deliberately not configurable.</b> The Tương Khắc cycle itself is
/// a rule, not a balance value: §2.2 keeps a change to the <i>relationship
/// structure</i> inside the Rule Change Policy (<c>GAME_RULES.md</c> §20). So
/// this type carries only the three factors and never a matchup table, and the
/// cycle stays hardcoded in <see cref="ElementMatchups"/> as the documented rule
/// it is.
///
/// <b>Supplied, never ambient.</b> There is no static mutable "current" instance
/// and no service locator: a caller passes the configuration it means to use, so
/// a resolution can never be affected by another resolution's balance edit and
/// nothing global has to be reset between tests. The default is reached explicitly
/// through <see cref="Default"/>. The resolution itself
/// (<see cref="ElementMatchups"/>) deliberately takes no configuration at all —
/// a matchup is a rule.
/// </summary>
public readonly record struct ElementModifiers
{
    /// <summary>
    /// Creates a modifier set from the three factors
    /// (<c>ELEMENT_RULES.md</c> §2.2).
    /// </summary>
    /// <param name="advantage">
    /// The factor for <see cref="ElementMatchup.Advantage"/> — default
    /// <c>1.50</c> (§2.2).
    /// </param>
    /// <param name="neutral">
    /// The factor for <see cref="ElementMatchup.Neutral"/> — default
    /// <c>1.00</c> (§2.2).
    /// </param>
    /// <param name="disadvantage">
    /// The factor for <see cref="ElementMatchup.Disadvantage"/> — default
    /// <c>0.75</c> (§2.2).
    /// </param>
    public ElementModifiers(double advantage, double neutral, double disadvantage)
    {
        Advantage = advantage;
        Neutral = neutral;
        Disadvantage = disadvantage;
    }

    /// <summary>
    /// The modifier applied when the attacker counters the defender
    /// (<c>ELEMENT_RULES.md</c> §2.2).
    ///
    /// Default <c>1.50</c> — §2.2 gives the value as <c>1.50×</c>, so the factor
    /// is the multiplier itself and not a percentage or a delta.
    /// </summary>
    public double Advantage { get; init; }

    /// <summary>
    /// The modifier applied when neither side counters the other — equal
    /// Elements, or either side elementless (<c>ELEMENT_RULES.md</c> §2.1, §3).
    ///
    /// Default <c>1.00</c> (§2.2). <c>1.00</c> is the identity factor, so a
    /// Neutral matchup leaves Base Damage × Combo Modifier unchanged into
    /// <c>COMBAT_RULES.md</c> §3 step 4.
    /// </summary>
    public double Neutral { get; init; }

    /// <summary>
    /// The modifier applied when the defender counters the attacker
    /// (<c>ELEMENT_RULES.md</c> §2.2).
    ///
    /// Default <c>0.75</c> (§2.2). §2.2 sets no floor for a damage instance, so
    /// this type applies none: <c>COMBAT_RULES.md</c> §3 step 6 owns the
    /// "minimum 0" of <i>Final Damage</i> and this step 3 factor is only one
    /// input to it.
    /// </summary>
    public double Disadvantage { get; init; }

    /// <summary>
    /// The default modifier set of <c>ELEMENT_RULES.md</c> §2.2:
    /// Advantage <c>1.50</c>, Neutral <c>1.00</c>, Disadvantage <c>0.75</c>.
    ///
    /// It is a static property rather than a constant so that a caller can
    /// replace it by passing its own <see cref="ElementModifiers"/> — the
    /// balance-change path §2.2 describes — without this type becoming mutable
    /// shared state.
    /// </summary>
    public static ElementModifiers Default => new(1.50, 1.00, 0.75);

    /// <summary>
    /// The documented default factor for an <see cref="ElementMatchup"/> — the
    /// lookup <c>COMBAT_RULES.md</c> §3 step 3 performs.
    ///
    /// <b>The lookup is total over the three outcome kinds.</b> §2.1 states a
    /// matchup is exactly one of the three, so there is no "unmatched" case to
    /// fall back to and no default branch returning a plausible factor. A value
    /// outside the enum is not a matchup §2.1 defines, and returning a factor for
    /// it would silently price an undefined outcome, so it throws.
    /// </summary>
    /// <param name="matchup">The resolved matchup of one damage instance.</param>
    /// <returns>
    /// The configured factor for that matchup: <see cref="Advantage"/>,
    /// <see cref="Neutral"/>, or <see cref="Disadvantage"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="matchup"/> is not one of the three kinds
    /// <c>ELEMENT_RULES.md</c> §2.1 defines.
    /// </exception>
    public double For(ElementMatchup matchup) => matchup switch
    {
        ElementMatchup.Advantage => Advantage,
        ElementMatchup.Neutral => Neutral,
        ElementMatchup.Disadvantage => Disadvantage,
        _ => throw new ArgumentOutOfRangeException(
            nameof(matchup),
            matchup,
            "A matchup is exactly one of Advantage / Neutral / Disadvantage "
            + "(ELEMENT_RULES.md §2.1); no other outcome has a modifier."),
    };
}
