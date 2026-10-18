using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Passives;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// One <c>TASK-298</c> controlled tuning experiment — the exact parameter deltas
/// applied to the <b>unchanged</b> <see cref="BalanceBaselineMatrix"/> inputs, and
/// nothing else.
///
/// <b>It is data, and it is the whole of the experiment.</b> Every member is a
/// <c>with</c>-expression delta over the canonical matrix cell
/// (<see cref="BalanceBaselineMatrix.Configuration"/>): the seed, the policy, the
/// Boss identity, the Element, the Passive identity, the Skill timing, and the
/// Relic snapshot are <b>never</b> varied. A run is therefore comparable to its
/// TASK-296 counterpart by construction, not by intention
/// (<c>TASK-193</c> H-07).
///
/// <b>It computes no gameplay value.</b> The deltas below are parameter values —
/// a Card's <c>PowerCost</c>, a Card effect's magnitude, and a Boss definition's
/// <c>DEF</c>/<c>MaxHP</c>/Passive <c>Threshold</c>. Applying one produces a
/// <see cref="CardDefinition"/> or a <see cref="BossDefinition"/> of exactly the
/// shape the production pipeline already accepts; the pipeline then decides
/// everything, exactly as it does for the canonical matrix.
///
/// <b>Nothing here reaches production.</b> No value in this file is read by
/// <c>GameServer.Domain</c>, <c>GameServer.Application</c>, or
/// <c>GameServer.Api</c>; the file exists only in the Application test project,
/// and the production defaults it deltas against are left byte-identical
/// (<c>TASK-298</c> §4 "keep changes minimal and reversible", §6 "keep
/// experiment outputs under new task-specific filenames").
/// </summary>
internal sealed record TASK298ExperimentDefinition
{
    /// <summary>The experiment identity a result records.</summary>
    public required string Id { get; init; }

    /// <summary>The one-line statement of what is varied, for the artifact.</summary>
    public required string ParameterGroup { get; init; }

    /// <summary>
    /// The falsifiable hypothesis this experiment tests, in terms of the
    /// mechanism it targets rather than the outcome it hopes for.
    /// </summary>
    public required string Hypothesis { get; init; }

    /// <summary>
    /// The player-facing effect the change is intended to produce, stated so a
    /// reviewer can disagree with it.
    /// </summary>
    public required string IntendedPlayerFacingEffect { get; init; }

    /// <summary>
    /// <c>card-heal</c>'s authored <c>PowerCost</c> after the delta, or
    /// <c>null</c> to leave it as <see cref="BalanceBaselineMatrix"/> authored it.
    /// </summary>
    public int? CardHealPowerCost { get; init; }

    /// <summary>
    /// <c>card-heal</c>'s authored Heal magnitude in percent of MaxHP after the
    /// delta, or <c>null</c> to leave it unchanged.
    /// </summary>
    public int? CardHealPercentMaxHp { get; init; }

    /// <summary>
    /// <c>card-power-charge</c>'s authored <c>PowerCost</c> after the delta, or
    /// <c>null</c> to leave it unchanged.
    /// </summary>
    public int? CardPowerChargePowerCost { get; init; }

    /// <summary>
    /// <c>card-power-charge</c>'s authored flat Power grant after the delta, or
    /// <c>null</c> to leave it unchanged.
    /// </summary>
    public int? CardPowerChargeGrant { get; init; }

    /// <summary>
    /// The <c>MaxHP</c> applied only to the Bosses whose <c>BOSS_RULES.md</c> §6.1
    /// row authors the shared <c>5000</c>, or <c>null</c> to leave every Boss's
    /// §6.1 value in place.
    ///
    /// <b>The delta is scoped to the row that actually defines the value.</b>
    /// §6.1 gives Sơn Thạch Vệ <c>3000</c> and Kim Lôi Vương <c>2800</c> as their
    /// own authored values; a scaling experiment that flattened them to the same
    /// number would change two Bosses it says nothing about and would erase the
    /// only authored variation in the set.
    /// </summary>
    public int? SharedRowBossMaxHp { get; init; }

    /// <summary>
    /// The <c>DEF</c> applied only to the Bosses whose <c>BOSS_RULES.md</c> §6.1
    /// row authors <c>DEF = 50</c>, or <c>null</c> to leave every Boss's §6.1
    /// value in place.
    ///
    /// <b>The delta is scoped to the Bosses that actually change.</b> §6.1 gives
    /// Hỏa Long, Thủy Ma, and Mộc Yêu the shared <c>DEF = 50</c> row and gives Sơn
    /// Thạch Vệ and Kim Lôi Vương <c>DEF = 0</c>. An experiment that
    /// <i>lowered</i> DEF must therefore move exactly those three and leave the
    /// other two at the <c>0</c> §6.1 already authors — moving them <i>up</i> to
    /// 25 would silently change two Bosses the hypothesis says nothing about and
    /// would confound the comparison.
    /// </summary>
    public int? DefensiveBossDefense { get; init; }

    /// <summary>
    /// The <c>MaxHP</c> every Boss definition carries after the delta, or
    /// <c>null</c> to leave each Boss's §6.1 value in place.
    /// </summary>
    public int? BossMaxHp { get; init; }

    /// <summary>
    /// The match-charged Passive Threshold Mộc Yêu carries after the delta, or
    /// <c>null</c> to leave <see cref="BalanceBaselineMatrix"/>'s historical
    /// baseline value (<c>5</c>) in place. Applied only to Mộc Yêu, because only
    /// Mộc Yêu's Passive is the regeneration the group targets.
    /// </summary>
    public int? MocYeuPassiveThreshold { get; init; }

    /// <summary>
    /// The canonical Card content set with this experiment's Card deltas applied.
    ///
    /// <b>Every other member is carried across unchanged.</b> The Card identity,
    /// name, category, copy limit, and effect structure stay exactly as
    /// <see cref="BalanceBaselineMatrix.Cards"/> authored them; only the members
    /// this experiment names move. A <c>null</c> delta on every Card member
    /// reproduces the baseline list value-for-value.
    /// </summary>
    public IReadOnlyList<CardDefinition> ApplyToCards() =>
        BalanceBaselineMatrix.Cards.Select(ApplyToCard).ToArray();

    /// <summary>
    /// One canonical Card with this experiment's deltas applied.
    ///
    /// <see cref="CardDefinition"/> is a class rather than a record, so the
    /// delta is expressed by constructing a new definition from the canonical
    /// one's own values rather than by a <c>with</c> expression. Every member
    /// this experiment does not name is copied from <paramref name="card"/>
    /// verbatim, so a Card with no applicable delta is value-for-value the
    /// canonical definition.
    /// </summary>
    private CardDefinition ApplyToCard(CardDefinition card) => card.CardDefinitionId switch
    {
        "card-heal" => Rebuild(
            card,
            PowerCost: CardHealPowerCost ?? card.PowerCost,
            EffectDefinition: CardHealPercentMaxHp is { } healPercent
                ? CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(
                        CardEffectType.Heal,
                        CardEffectValueType.PercentMaxHp,
                        healPercent))
                : null),

        "card-power-charge" => Rebuild(
            card,
            PowerCost: CardPowerChargePowerCost ?? card.PowerCost,
            EffectDefinition: CardPowerChargeGrant is { } powerGrant
                ? CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(
                        CardEffectType.Power,
                        CardEffectValueType.Flat,
                        powerGrant))
                : null),

        // card-shield and card-iron-fang are not part of any TASK-298 group and
        // are carried across untouched.
        _ => card,
    };

    /// <summary>
    /// A new definition carrying <paramref name="card"/>'s members, with the
    /// named overrides applied. A <c>null</c> override means "keep the canonical
    /// value".
    /// </summary>
    private static CardDefinition Rebuild(
        CardDefinition card,
        int PowerCost,
        CardEffectDefinitions? EffectDefinition) => new()
    {
        CardDefinitionId = card.CardDefinitionId,
        Name = card.Name,
        Category = card.Category,
        PowerCost = PowerCost,
        EffectDefinition = EffectDefinition ?? card.EffectDefinition,
        LoadoutCopyLimit = card.LoadoutCopyLimit,
    };

    /// <summary>
    /// The Boss definition one matrix cell fights, with this experiment's Boss
    /// deltas applied.
    ///
    /// <b>Every member not named by the experiment is carried across
    /// unchanged</b> — the persistence key, the canonical technical Identity, the
    /// Element, the <c>ATK</c>, the <c>EnrageThreshold</c>, the Skill's identity,
    /// base damage, charge requirement, cooldown, and secondary effect, and the
    /// Passive's identity and reset behavior. So a Boss this experiment does not
    /// target is returned as the very instance the canonical matrix supplies.
    /// </summary>
    /// <param name="canonical">The Boss the canonical matrix supplies this cell.</param>
    public BossDefinition ApplyToBoss(BossDefinition canonical)
    {
        var boss = canonical;

        // The MaxHP delta is scoped to the shared §6.1 row (Hỏa Long, Thủy Ma,
        // Mộc Yêu at 5000); Sơn Thạch Vệ (3000) and Kim Lôi Vương (2800) keep
        // their own authored values, so this experiment leaves them untouched.
        if (SharedRowBossMaxHp is { } maxHp && canonical.MaxHP == BossRules.BaseMaxHP)
        {
            boss = boss with { MaxHP = maxHp };
        }

        // The DEF delta is scoped to the shared §6.1 row (Hỏa Long, Thủy Ma, Mộc
        // Yêu), so the two Bosses §6.1 already authors at DEF = 0 are untouched.
        if (DefensiveBossDefense is { } defense && canonical.DEF > 0)
        {
            boss = boss with { DEF = defense };
        }

        if (MocYeuPassiveThreshold is { } regenThreshold
            && string.Equals(boss.BossId.Value, "boss-moc-yeu", StringComparison.Ordinal))
        {
            // The Passive identity and reset behavior are the definition's own;
            // only the Threshold moves, which is the regeneration cadence
            // BOSS_RULES.md §6.2.3 owns.
            boss = boss with
            {
                PassiveDefinition = new BossPassiveDefinition(
                    boss.PassiveDefinition.PassiveId,
                    regenThreshold,
                    boss.PassiveDefinition.ResetBehavior),
            };
        }

        return boss;
    }
}
