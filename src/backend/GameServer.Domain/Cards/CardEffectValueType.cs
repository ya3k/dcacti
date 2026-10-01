namespace GameServer.Domain.Cards;

/// <summary>
/// How a <see cref="CardEffectDefinition"/>'s
/// <see cref="CardEffectDefinition.Value"/> is interpreted — the
/// <c>valueType</c> member of <c>DATABASE.md</c> §1's stored contract. It is
/// <b>the type of <c>value</c></b>: additional per-effect parameters travel as
/// extra members on the same element, never as new interpretations here
/// (TASK-111 decision D-3).
///
/// <code>
/// Flat              the value is the amount itself
/// PercentMaxHp      the value is a proportion of the target's Max HP
/// PercentagePoints  the value is a percentage-point adjustment
/// Undetermined      the owning document states no magnitude yet
/// </code>
///
/// <b>The three interpreting members are exactly what the owning document
/// distinguishes.</b> <c>CARD_RULES.md</c> §2 states its two Basic Card
/// proportions as a share of the active Pet's Max HP ("20% of its Max HP") and
/// its Power Charge as a plain amount ("25 Power") — <see cref="Flat"/> and
/// <see cref="PercentMaxHp"/>. §4.1 states Iron Fang's Crit increase in
/// percentage <i>points</i> ("by 10 percentage points"), which is
/// <see cref="PercentagePoints"/>: a percentage-point adjustment is not a
/// proportion of anything, so reusing <see cref="PercentMaxHp"/> would
/// reinterpret the magnitude, and expressing it in <see cref="Flat"/> would lose
/// the unit. These three are the whole interpretation set (D-3).
///
/// <b><see cref="Undetermined"/> is not an interpretation — it records the
/// absence of one.</b> It marks an effect whose owning document states an
/// identity but no magnitude, so the element is representable and readable while
/// no number is authored (<c>DATABASE.md</c> §1 item 9; TASK-111 D-3 confirms it
/// "remains valid for effects whose authored magnitude is not yet determined").
/// Such an element carries <b>no</b> <c>value</c> member at all — never
/// <c>0</c>, never <c>null</c>, never a placeholder, never a borrowed value — so
/// an unauthored magnitude can never be read as a number.
///
/// An effect carrying it is <b>not resolvable</b>: a resolver must treat it as
/// an open content gap and must never substitute a value (<c>AGENTS.md</c> §7).
/// After the TASK-112 encoding, no provisioned content row remains in this
/// state, because <c>CARD_RULES.md</c> §4.1 now authors every Pet Skill
/// magnitude; the member stays in the contract because the vocabulary is closed
/// and an unauthored magnitude must remain representable rather than invented.
///
/// <b>The three interpreting members each require a positive value</b>, and
/// <see cref="Undetermined"/> must carry none (<c>DATABASE.md</c> §3:
/// "int, &gt; 0, present iff valueType interprets one").
///
/// <b>Member order carries no documented meaning.</b> The numeric values are
/// storage identifiers and no rule derives from them; the persisted contract
/// stores the member <b>names</b>, never these ordinals (<c>DATABASE.md</c> §1
/// item 2). The order below is a stable reading aid.
/// </summary>
public enum CardEffectValueType
{
    /// <summary>
    /// The value is the effect's amount as stated — <c>CARD_RULES.md</c> §2's
    /// Power Charge ("Active Pet gains 25 Power") and §4.1's two damage
    /// magnitudes ("Deal 100 Fire (Hỏa) damage"; "Deal 120 damage") and Burn
    /// rate ("50 damage per tick"). No proportion or per-target scaling is
    /// applied by the interpretation.
    /// </summary>
    Flat = 0,

    /// <summary>
    /// The value is a proportion of the target's Max HP, expressed in whole
    /// percent — <c>CARD_RULES.md</c> §2's Heal and Shield, and §4.1's Tidal
    /// Barrier Heal and Shield ("20% of its Max HP" each). The proportion is
    /// interpreted against the active Pet's <c>MaxHP</c> by the effect's
    /// resolver at cast time; this member carries the interpretation only and
    /// resolves nothing. A percentage is <b>not</b> pre-resolved to an absolute
    /// amount: the Pet's <c>MaxHP</c> is battle state (<c>GAME_STATE.md</c>
    /// §2.3) and is read when the effect is applied (<c>DATABASE.md</c> §1
    /// item 1).
    /// </summary>
    PercentMaxHp = 1,

    /// <summary>
    /// The owning document states no magnitude for this effect yet, so there is
    /// none to interpret. It is the explicit record of an unauthored content gap
    /// (<c>DATABASE.md</c> §1 item 9), and it exists so no magnitude has to be
    /// invented to make an element representable. An element carrying it must
    /// have <b>no</b> <c>value</c> member: it is never <c>0</c>, never a
    /// placeholder, and it is <b>not</b> an interpretation any resolver may act
    /// on.
    /// </summary>
    Undetermined = 2,

    /// <summary>
    /// The value is a percentage-<b>point</b> adjustment — <c>CARD_RULES.md</c>
    /// §4.1's Iron Fang Crit increase ("increase Crit chance by 10 percentage
    /// points for the next attack only"), whose 10 percentage points are the
    /// effect's own value and are independent of Bạch Hổ's Passive configuration
    /// value (<c>PASSIVE_RULES.md</c> §7/§8).
    ///
    /// It is not a proportion of any stat, which is exactly why it is a separate
    /// member from <see cref="PercentMaxHp"/>: a percentage point is an additive
    /// adjustment to a rate, not a share of a pool, and collapsing the two would
    /// misstate the magnitude (TASK-111 D-3).
    ///
    /// It carries no Crit roll, no crit-chance formula, and no RNG: Crit
    /// application is owned by <c>COMBAT_RULES.md</c> §3.3, which does not exist
    /// in MVP. The matching extra <c>scope</c> member
    /// (<see cref="CardEffectDefinition.Scope"/>) states which damage instances
    /// the increase applies to.
    /// </summary>
    PercentagePoints = 3,
}
