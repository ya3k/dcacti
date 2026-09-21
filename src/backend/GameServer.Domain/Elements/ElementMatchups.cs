namespace GameServer.Domain.Elements;

/// <summary>
/// Tương Khắc (overcoming cycle) resolution — <c>ELEMENT_RULES.md</c> §2 and §3,
/// applied where <c>COMBAT_RULES.md</c> §3 step 3 places the Element Modifier in
/// the damage pipeline.
///
/// <code>
/// (attacking Element, defending Element)  →  ElementMatchup
/// </code>
///
/// Both Elements are <b>nullable</b>, because elementless is the absence of an
/// Element and not a sixth one: <c>ELEMENT_RULES.md</c> §3 covers "some damage
/// sources may have no Element (e.g. a Basic Card with no elemental tag, or a
/// pure 'true damage' effect)" and "an elementless target (if any such target
/// exists)". A <c>null</c> on either side is that case, and there is no
/// <c>Element.None</c> member to pass instead.
///
/// <b>The cycle, exactly as documented.</b> §2 states the MVP implements only
/// Tương Khắc and gives the five relationships:
///
/// <code>
/// Mộc  →  Thổ
/// Thổ  →  Thủy
/// Thủy →  Hỏa
/// Hỏa  →  Kim
/// Kim  →  Mộc
/// </code>
///
/// "the element on the left counters the element on the right".
/// <c>GAME_RULES.md</c> §8 states the same cycle as
/// <c>Mộc → Thổ → Thủy → Hỏa → Kim → Mộc</c>. The two agree; there is no conflict
/// to report. No relationship beyond these five exists in MVP — Tương Sinh is
/// explicitly out of scope (§2, §7; <c>GAME_RULES.md</c> §8.7;
/// <c>MVP_SCOPE.md</c> §2) and is not implemented here, and this class
/// deliberately has no hook for it.
///
/// <b>Pure and stateless.</b> The operation is a total function of its two
/// arguments. It reads no state, holds no table that can be mutated, draws no
/// RNG, and touches no framework type — Domain has zero dependencies outside
/// itself (<c>ARCHITECTURE.md</c> §1, §2.1). Nothing here mutates an entity,
/// assigns an Element to anything, or consumes damage: <c>COMBAT_RULES.md</c> §3
/// owns the pipeline that multiplies the modifier in, and Pet/Boss/Skill/Effect
/// Element ownership is a separate concern (§1.1).
///
/// The type is named in the plural after the existing <c>GemTypes</c> helper, so
/// that the <see cref="ElementMatchup"/> outcome enum and this resolver do not
/// share a name.
/// </summary>
public static class ElementMatchups
{
    /// <summary>
    /// Resolves the matchup between an attacking Element and a defending Element
    /// (<c>ELEMENT_RULES.md</c> §2.1).
    ///
    /// <code>
    /// 1. A counters D  (A → D)           → Advantage
    /// 2. D counters A  (D → A)           → Disadvantage
    /// 3. otherwise (A == D, or A/D null) → Neutral
    /// </code>
    ///
    /// <b>The checks are ordered and mutually exclusive.</b> In a five-element
    /// cycle where each Element counters exactly one other, <c>A → D</c> and
    /// <c>D → A</c> cannot both hold (§2), so the order of checks 1 and 2 cannot
    /// change the outcome; it is written in the documented order rather than as a
    /// materialized lookup table, so the cycle is stated once, in
    /// <see cref="Counters"/>.
    ///
    /// <b>Equal Elements are Neutral, not Advantage</b> — §2.1's <c>A == D</c>
    /// case. This falls out of checks 1 and 2 rather than needing its own branch:
    /// §2 lists no self-relationship, so an Element never counters itself.
    ///
    /// <b>Elementless is Neutral on either side</b> (§3 items 1–2), and also falls
    /// out of the same two checks: <c>null</c> is not equal to any Element and
    /// counters nothing, so neither the attacker-counters-defender check nor its
    /// reverse can hold. §3 item 1 fixes the elementless <i>attacker</i> case at
    /// Neutral against any defending Element; item 2 fixes the elementless
    /// <i>defender</i> case at Neutral; with both sides absent the result is
    /// Neutral as well, which is the same reading of "no elemental relationship
    /// exists" and introduces no relationship §2 does not define.
    ///
    /// The result is a matchup only. Turning it into a damage factor is
    /// <see cref="ElementModifiers.For"/>, and applying that factor is
    /// <c>COMBAT_RULES.md</c> §3 — neither is done here.
    /// </summary>
    /// <param name="attacker">
    /// The Element of the damage source — Pet, Boss, Skill, or Effect
    /// (<c>ELEMENT_RULES.md</c> §5), or <c>null</c> for an elementless source
    /// (§3). This type does not read the entity; the caller supplies its Element.
    /// </param>
    /// <param name="defender">
    /// The Element of the entity receiving the damage — Pet or Boss
    /// (<c>ELEMENT_RULES.md</c> §5), or <c>null</c> for an elementless target
    /// (§3 item 2).
    /// </param>
    /// <returns>
    /// <see cref="ElementMatchup.Advantage"/> when the attacker counters the
    /// defender, <see cref="ElementMatchup.Disadvantage"/> when the defender
    /// counters the attacker, and <see cref="ElementMatchup.Neutral"/> in every
    /// remaining case (§2.1, §3).
    /// </returns>
    public static ElementMatchup Resolve(Element? attacker, Element? defender)
    {
        if (attacker is null || defender is null)
        {
            return ElementMatchup.Neutral;
        }

        if (Counters(attacker.Value, defender.Value))
        {
            return ElementMatchup.Advantage;
        }

        if (Counters(defender.Value, attacker.Value))
        {
            return ElementMatchup.Disadvantage;
        }

        return ElementMatchup.Neutral;
    }

    /// <summary>
    /// True when <paramref name="attacker"/> counters <paramref name="defender"/>
    /// — <c>attacker → defender</c> in the Tương Khắc cycle
    /// (<c>ELEMENT_RULES.md</c> §2).
    ///
    /// This is the single expression of the cycle's five relationships in the
    /// codebase. Each Element counters exactly one other (§2), so the mapping is a
    /// function and not a relation, and the cycle closes:
    /// <c>Mộc → Thổ → Thủy → Hỏa → Kim → Mộc</c>.
    ///
    /// It is a directional predicate, not a symmetric comparison: a matchup is
    /// resolved by asking it twice, in both directions (<see cref="Resolve"/>).
    /// </summary>
    /// <param name="attacker">The Element on the left of the cycle.</param>
    /// <param name="defender">The Element on the right of the cycle.</param>
    /// <returns>
    /// True when <paramref name="attacker"/> counters <paramref name="defender"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attacker"/> is not one of the five Elements
    /// <c>ELEMENT_RULES.md</c> §1 defines.
    /// </exception>
    public static bool Counters(Element attacker, Element defender) => attacker switch
    {
        // Mộc → Thổ  (ELEMENT_RULES.md §2)
        Element.Moc => defender == Element.Tho,

        // Thổ → Thủy  (ELEMENT_RULES.md §2)
        Element.Tho => defender == Element.Thuy,

        // Thủy → Hỏa  (ELEMENT_RULES.md §2)
        Element.Thuy => defender == Element.Hoa,

        // Hỏa → Kim   (ELEMENT_RULES.md §2)
        Element.Hoa => defender == Element.Kim,

        // Kim → Mộc   (ELEMENT_RULES.md §2)
        Element.Kim => defender == Element.Moc,

        _ => throw new ArgumentOutOfRangeException(
            nameof(attacker),
            attacker,
            "MVP has exactly five Elements (ELEMENT_RULES.md §1); "
            + "no other value has a Tương Khắc relationship."),
    };
}
