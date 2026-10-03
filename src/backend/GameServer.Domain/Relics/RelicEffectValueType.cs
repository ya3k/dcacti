namespace GameServer.Domain.Relics;

/// <summary>
/// How a <see cref="RelicEffectDefinition"/>'s
/// <see cref="RelicEffectDefinition.Value"/> is interpreted — the
/// <c>valueType</c> member of the Relic representation <c>RELIC_RULES.md</c>
/// §8.2 item 2 defines.
///
/// <code>
/// Flat              an absolute amount
/// Percentage        a proportion of the stat's own value
/// PercentagePoints  a proportion expressed in percentage points
/// Undetermined      the owning document states no magnitude yet
/// </code>
///
/// <b>These four are the whole set</b> (§8.2 item 2, TASK-131 <b>D2</b>), and it
/// is deliberately <b>not</b> the Card set: the Card contract's interpretations
/// are <c>Flat | PercentMaxHp | PercentagePoints | Undetermined</c>
/// (<c>DATABASE.md</c> §1). The difference is substantive rather than cosmetic —
/// §8.2 item 2 defines <see cref="Percentage"/> as "a proportion of the stat's
/// own value", which is not the Card set's <c>PercentMaxHp</c> (a proportion of
/// the target's Max HP). Reusing <c>CardEffectValueType</c> would therefore
/// misstate a Relic magnitude's unit.
///
/// <b><see cref="Percentage"/> is never pre-resolved.</b> §8.2 item 2 states it:
/// "A percentage is never pre-resolved to an absolute amount: the value it
/// applies to is read from battle state when the effect is applied." The stored
/// value is the proportion §8.5 records, and the stat it applies to is battle
/// state read at application time.
///
/// <b><see cref="PercentagePoints"/> is not a proportion of anything.</b> §8.2
/// item 2 calls it "the interpretation a Crit-chance increase states
/// (<c>COMBAT_RULES.md</c> §2 item 2)" — an additive adjustment to a rate, which
/// is why it is a separate member. Collapsing it into
/// <see cref="Percentage"/> would reinterpret Assassin Eye's <c>+10</c>.
///
/// <b><see cref="Undetermined"/> is not an interpretation — it records the
/// absence of one.</b> §8.2 item 2: it "records that the owning document states
/// no magnitude yet, and it <b>remains valid</b> for such an effect. It is why
/// no magnitude has to be invented to make such a row representable." §8.2 item
/// 3 fixes the consequence: an <c>Undetermined</c> element carries <b>no</b>
/// <c>value</c> member at all — never <c>0</c> and never <c>null</c> — so an
/// unauthored magnitude can never be read as a number.
///
/// <b>No provisioned Relic row uses it.</b> §8.5 authors every magnitude of all
/// four provisioned rows, so none is in this state (TASK-132's acceptance
/// criteria record that none is introduced). The member stays in the vocabulary
/// because §8.2 item 2 keeps it valid, and an unauthored magnitude must remain
/// representable rather than invented.
///
/// <b>Member order carries no documented meaning.</b> The numeric values are
/// storage identifiers and no rule derives from them; the persisted contract
/// stores the member <b>names</b>, never these ordinals. The order below is a
/// stable reading aid and follows §8.2 item 2's listing.
/// </summary>
public enum RelicEffectValueType
{
    /// <summary>
    /// The value is an absolute amount — §8.2 item 2: "<c>Flat</c> is an absolute
    /// amount". §8.3's allowed combination uses it for the <c>Power</c> effect,
    /// whose §6 row states a plain grant ("+10 Power").
    /// </summary>
    Flat = 0,

    /// <summary>
    /// The value is a proportion of the stat's own value — §8.2 item 2:
    /// "<c>Percentage</c> is a proportion of the stat's own value". §8.3's allowed
    /// combination uses it for <c>ATK</c> and <c>CardCost</c>, whose §6 rows
    /// state proportions ("+5% ATK", "Heal Card cost −50%").
    ///
    /// The proportion is <b>not</b> pre-resolved against an assumed stat value;
    /// the value it applies to is battle state and is read when the effect is
    /// applied (§8.2 item 2). This member carries the interpretation only.
    /// </summary>
    Percentage = 1,

    /// <summary>
    /// The value is a proportion expressed in percentage <b>points</b> — §8.2
    /// item 2: "the interpretation a Crit-chance increase states
    /// (<c>COMBAT_RULES.md</c> §2 item 2)". §8.3's allowed combination uses it
    /// for <c>Crit</c>, whose §8.5 magnitude is <c>+10</c> percentage points.
    ///
    /// It is not a proportion of any stat, which is exactly why it is separate
    /// from <see cref="Percentage"/> (§8.2 item 2). It performs no Crit roll and
    /// touches no crit-chance formula; <c>COMBAT_RULES.md</c> §2 item 7 owns the
    /// composition it participates in.
    /// </summary>
    PercentagePoints = 2,

    /// <summary>
    /// The owning document states no magnitude for this effect yet, so there is
    /// none to interpret — §8.2 item 2 keeps it valid for exactly that case.
    ///
    /// An element carrying it must have <b>no</b> <c>value</c> member: never
    /// <c>0</c>, never <c>null</c>, never a placeholder (§8.2 item 3). Such an
    /// effect is <b>not resolvable</b>, and a resolver must treat it as an open
    /// content gap and never substitute a value (<c>AGENTS.md</c> §7).
    /// </summary>
    Undetermined = 3,
}
