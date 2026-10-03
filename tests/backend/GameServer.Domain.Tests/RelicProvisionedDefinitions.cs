using GameServer.Domain.Relics;

namespace GameServer.Domain.Tests;

/// <summary>
/// The four provisioned <c>RelicDefinition</c> rows as their <b>structured</b>
/// values — <c>RELIC_RULES.md</c> §8.5's "Provisioned Relic Contract" table,
/// which owns every value below and states that none of them is authored by it
/// (each is transcribed from §6 and §8.1–§8.4).
///
/// <b>Why a shared helper.</b> The Relic resolution suites need the same four
/// rows the provisioning migration writes, so holding them once keeps the
/// transcription in a single place instead of letting several files drift. The
/// Infrastructure suite holds its own copy
/// (<c>GameServer.Infrastructure.Tests.RelicProvisionedContent</c>) because that
/// suite asserts the stored rows; this one supplies the content a resolution
/// reads.
///
/// <b>These values are transcribed, never computed or invented</b>
/// (<c>DATABASE.md</c> §5 item 4 rule (b)). They mirror exactly what TASK-132's
/// migration <c>20261003074309_StructureRelicDefinitionStructuredColumns</c>
/// writes and what <c>RELIC_RULES.md</c> §8.5 records, so a test using them is
/// asserting the documented contract rather than a fixture's convenience value.
///
/// <b><c>Trigger</c> is the §3 prose identity.</b> <c>RELIC_RULES.md</c> §8.5
/// item 3 (TASK-131 D8) leaves §3's closed list unchanged and deliberately does
/// not restructure <c>Trigger</c>, so it is the value the provisioning migration
/// stored.
/// </summary>
internal static class RelicProvisionedDefinitions
{
    /// <summary>The provisioned definition identity of Berserker Core.</summary>
    internal const string BerserkerCoreId = "relic-berserker-core";

    /// <summary>The provisioned definition identity of Mana Crystal.</summary>
    internal const string ManaCrystalId = "relic-mana-crystal";

    /// <summary>The provisioned definition identity of Assassin Eye.</summary>
    internal const string AssassinEyeId = "relic-assassin-eye";

    /// <summary>The provisioned definition identity of Emergency Core.</summary>
    internal const string EmergencyCoreId = "relic-emergency-core";

    /// <summary>
    /// §8.5: Trigger <c>OnMatchCount</c>; Condition <c>MatchCountAtLeast(3)</c>;
    /// <c>ATK, Percentage, 5, Pet, Battle</c> — from §6's "+5% ATK".
    /// </summary>
    internal static RelicDefinition BerserkerCore() => new()
    {
        RelicDefinitionId = BerserkerCoreId,
        Name = "Berserker Core",
        Trigger = "OnMatchCount",
        Condition = RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 3),
        EffectDefinition = RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.ATK,
                RelicEffectValueType.Percentage,
                5,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.Battle)),
    };

    /// <summary>
    /// §8.5: Trigger <c>OnMatchCount</c>; Condition <c>MatchCountAtLeast(4)</c>;
    /// <c>Power, Flat, 10, Pet, Immediate</c> — from §6's "+10 Power".
    /// </summary>
    internal static RelicDefinition ManaCrystal() => new()
    {
        RelicDefinitionId = ManaCrystalId,
        Name = "Mana Crystal",
        Trigger = "OnMatchCount",
        Condition = RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 4),
        EffectDefinition = RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.Power,
                RelicEffectValueType.Flat,
                10,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.Immediate)),
    };

    /// <summary>
    /// §8.5: Trigger <c>OnCombo</c>; Condition <c>ComboAtLeast(3)</c>;
    /// <c>Crit, PercentagePoints, 10, Pet, NextAttack</c> — from §6's "Increased
    /// Crit chance", whose magnitude §8.5 item 1 authors as +10 points.
    /// </summary>
    internal static RelicDefinition AssassinEye() => new()
    {
        RelicDefinitionId = AssassinEyeId,
        Name = "Assassin Eye",
        Trigger = "OnCombo",
        Condition = RelicCondition.Create(RelicConditionType.ComboAtLeast, 3),
        EffectDefinition = RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.Crit,
                RelicEffectValueType.PercentagePoints,
                10,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.NextAttack)),
    };

    /// <summary>
    /// §8.5: Trigger <c>OnHpBelow</c>; Condition <c>HpPercentageBelow(30)</c>;
    /// <c>CardCost, Percentage, 50, Pet, Battle</c> — from §6's "Heal Card cost
    /// −50%".
    /// </summary>
    internal static RelicDefinition EmergencyCore() => new()
    {
        RelicDefinitionId = EmergencyCoreId,
        Name = "Emergency Core",
        Trigger = "OnHpBelow",
        Condition = RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30),
        EffectDefinition = RelicEffectDefinitions.Create(
            RelicEffectDefinition.Create(
                RelicEffectType.CardCost,
                RelicEffectValueType.Percentage,
                50,
                RelicEffectTarget.Pet,
                RelicEffectLifetime.Battle)),
    };
}
