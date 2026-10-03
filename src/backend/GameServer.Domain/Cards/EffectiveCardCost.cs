using GameServer.Domain.Battle;

namespace GameServer.Domain.Cards;

/// <summary>
/// <c>EffectiveCardCost</c> — what a Card cast actually costs, composed from the
/// authored base cost and the active Pet's applied Card-cost modifiers
/// (<c>CARD_RULES.md</c> §3.6, which is the canonical owner of this value).
///
/// <code>
/// CardDefinition.PowerCost                                    authored/base
///   ↓
/// PetState.CardCostModifiers[]                                applied modifiers
///   ↓
/// TotalReduction = min( Σ CostReductionPercentage, 100 )
///   ↓
/// EffectiveCardCost = truncate( PowerCost × (100 − TotalReduction) / 100 )
/// </code>
///
/// <b>The authored cost is never mutated.</b> §3.6 item 1 keeps
/// <c>CardDefinition.PowerCost</c> the content-side base value: no modifier writes
/// it, no resolution rewrites it, and no column or definition member is introduced
/// for the runtime result. It is the <i>input</i> to this calculation.
///
/// <b>The composed cost is derived, never stored.</b> §3.6 item 2 makes
/// <c>EffectiveCardCost</c> a runtime value for the cast being resolved —
/// explicitly not a <c>PetState</c> member, not a <c>BattleState</c> member, not a
/// Redis field, and not a wire member. This is a pure function returning an
/// <c>int</c>, which is exactly that statement in code.
///
/// <b>One calculation, before validation, deduction, and reporting.</b> §3.6 item 7
/// requires the value to be computed once at the start of the cast resolution and
/// used by all three consumers, "so a cast cannot be validated against one cost and
/// charged another". The caller therefore composes once and passes the same value
/// to all three; this type composes nothing on its own.
///
/// <b>Truncation is toward zero, and there is no minimum cost.</b> §3.6 item 4 fixes
/// <c>truncate(RawEffectiveCardCost)</c> — not <c>floor</c>, <c>ceil</c>,
/// round-half-up, or banker's rounding — and §3.6 item 5 states there is no
/// "minimum cost of 1" floor: a 100% total reduction yields <c>0</c>, and a cost of
/// <c>0</c> is validated and deducted as <c>0</c>. The arithmetic below is integer
/// arithmetic, so the result does not depend on floating-point representation
/// (<c>TDD.md</c> §6).
///
/// <b>The cap is <c>CardCost</c>-specific.</b> §3.6 item 3 and <c>ADR-018</c>
/// decision 12f state the additive composition and its 100% cap govern the
/// <c>CardCost</c> effect type alone and define no stacking, scaling, or interaction
/// for <c>ATK</c>, <c>Power</c>, <c>Crit</c>, or any other <c>effectType</c>.
/// </summary>
public static class EffectiveCardCost
{
    /// <summary>
    /// The reduction cap <c>CARD_RULES.md</c> §3.6 item 3 fixes
    /// (<c>TotalReduction = min( Σ CostReductionPercentage, 100 )</c>): the cap
    /// bounds the composed percentage, which is what makes
    /// <c>RawEffectiveCardCost</c> never negative and <c>EffectiveCardCost</c>
    /// never below <c>0</c>.
    /// </summary>
    public const int MaximumTotalReduction = 100;

    /// <summary>
    /// Composes the effective cost of one cast (<c>CARD_RULES.md</c> §3.6).
    /// </summary>
    /// <param name="powerCost">
    /// The authored base cost — <c>CardDefinition.PowerCost</c> (§3.6 item 1). It is
    /// read and never written.
    /// </param>
    /// <param name="modifiers">
    /// The applied, Battle-scoped Card-cost modifiers active on the active Pet at
    /// the moment the cast resolves (<c>GAME_STATE.md</c> §2.3.5, §5.1.3) — always a
    /// collection and never <c>null</c> (§2.3.5 item 6), because "no modifier
    /// active" is an <b>empty</b> collection. Which modifiers participate is decided
    /// by that committed state; this composition creates, refreshes, and removes
    /// none of them (§3.6 item 8, <c>AGENTS.md</c> §12).
    /// </param>
    /// <returns>
    /// The integer cost the cast is validated against, deducted by, and reported
    /// with — <c>CardDefinition.PowerCost</c> when no modifier is active.
    /// <c>CARD_RULES.md</c> §3.6 item 4's worked values fall out of it exactly:
    /// <c>15</c> at 50% → <c>7</c>, <c>25</c> at 50% → <c>12</c>, <c>15</c> at 100% →
    /// <c>0</c>, and <c>20</c> at 50% → <c>10</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="modifiers"/> is <c>null</c> — not a representable state
    /// (<c>GAME_STATE.md</c> §2.3.5 item 6).
    /// </exception>
    public static int Compose(int powerCost, IReadOnlyList<CardCostModifier> modifiers)
    {
        ArgumentNullException.ThrowIfNull(modifiers);

        // §3.6 item 3: multiple simultaneously-active modifiers compose by ADDING
        // their CostReductionPercentage values. Widened to long so a large set cannot
        // overflow before the cap is applied.
        var totalReduction = 0L;

        for (var index = 0; index < modifiers.Count; index++)
        {
            totalReduction += modifiers[index].CostReductionPercentage;
        }

        if (totalReduction > MaximumTotalReduction)
        {
            totalReduction = MaximumTotalReduction;
        }

        // §3.6 item 4: apply the (capped) reduction and truncate TOWARD ZERO. Both
        // operands are integral, so C#'s integer division is exactly that truncation —
        // no double, no rounding mode, and nothing platform-dependent.
        return (int)(powerCost * (100 - totalReduction) / 100);
    }
}
