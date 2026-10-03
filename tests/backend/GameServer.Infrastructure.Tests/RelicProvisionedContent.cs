using GameServer.Domain.Relics;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The four provisioned <c>RelicDefinition</c> rows as their
/// <b>structured</b> values — <c>RELIC_RULES.md</c> §8.5's "Provisioned Relic
/// Contract" table, which owns every value below and states that none of them is
/// authored by it (each is transcribed from §6 and §8.1–§8.4).
///
/// <b>Why a shared helper.</b> Two Infrastructure tests need the same four rows:
/// the per-row PostgreSQL assertion in
/// <see cref="PetCardRelicDefinitionPostgresProvisioningTests"/> and the
/// storage-contract suite in <see cref="RelicStructuredStorageTests"/>
/// (TASK-132). Holding them once keeps the transcription in a single place
/// instead of letting two files drift.
///
/// <b>These values are transcribed, never computed or invented</b>
/// (<c>DATABASE.md</c> §5 item 4 rule (b)). They mirror exactly what TASK-132's
/// migration
/// <c>20261003074309_StructureRelicDefinitionStructuredColumns</c> writes and
/// what <c>RELIC_RULES.md</c> §8.5 records. A test that asserts them is asserting
/// the documented contract, not the implementation's current output.
///
/// <b><c>Trigger</c> is included but is not part of §8's restructuring.</b>
/// <c>RELIC_RULES.md</c> §8.5 item 3 (TASK-131 D8) leaves §3's closed list
/// unchanged, so it stays the prose §3 identity the provisioning migration
/// stored.
/// </summary>
internal static class RelicProvisionedContent
{
    /// <summary>One provisioned row: its key, §3 Trigger, and §8.1/§8.2 values.</summary>
    internal readonly record struct Row(
        string Id,
        string Name,
        string Trigger,
        RelicCondition Condition,
        RelicEffectDefinitions Effects);

    /// <summary>
    /// The four provisioned rows, in the order <c>RELIC_RULES.md</c> §8.5 lists
    /// them (the same order §6's MVP Relic Reference uses).
    /// </summary>
    internal static readonly Row[] Rows =
    [
        // §8.5: Trigger OnMatchCount (§3); Condition MatchCountAtLeast(3);
        // ATK, Percentage, 5, Pet, Battle — from §6's "+5% ATK".
        new(
            "relic-berserker-core",
            "Berserker Core",
            "OnMatchCount",
            RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 3),
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.ATK,
                    RelicEffectValueType.Percentage,
                    5,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.Battle))),

        // §8.5: Trigger OnMatchCount (§3); Condition MatchCountAtLeast(4);
        // Power, Flat, 10, Pet, Immediate — from §6's "+10 Power".
        new(
            "relic-mana-crystal",
            "Mana Crystal",
            "OnMatchCount",
            RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 4),
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.Power,
                    RelicEffectValueType.Flat,
                    10,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.Immediate))),

        // §8.5: Trigger OnCombo (§3); Condition ComboAtLeast(3);
        // Crit, PercentagePoints, 10, Pet, NextAttack — from §6's "Increased
        // Crit chance", whose magnitude §8.5 item 1 authors as +10 points.
        new(
            "relic-assassin-eye",
            "Assassin Eye",
            "OnCombo",
            RelicCondition.Create(RelicConditionType.ComboAtLeast, 3),
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.Crit,
                    RelicEffectValueType.PercentagePoints,
                    10,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.NextAttack))),

        // §8.5: Trigger OnHpBelow (§3); Condition HpPercentageBelow(30);
        // CardCost, Percentage, 50, Pet, Battle — from §6's
        // "Heal Card cost −50%".
        new(
            "relic-emergency-core",
            "Emergency Core",
            "OnHpBelow",
            RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30),
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.CardCost,
                    RelicEffectValueType.Percentage,
                    50,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.Battle))),
    ];

    /// <summary>The provisioned row with <paramref name="id"/>.</summary>
    internal static Row Single(string id) =>
        Rows.Single(row => string.Equals(row.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The provisioned row with <paramref name="id"/> as the Domain
    /// <see cref="RelicDefinition"/> a resolution reads — the same four members the
    /// stored columns carry, with <c>Trigger</c> kept as §3's prose identity
    /// (<c>RELIC_RULES.md</c> §8.5 item 3).
    /// </summary>
    internal static RelicDefinition Definition(string id)
    {
        var row = Single(id);

        return new RelicDefinition
        {
            RelicDefinitionId = row.Id,
            Name = row.Name,
            Trigger = row.Trigger,
            Condition = row.Condition,
            EffectDefinition = row.Effects,
        };
    }

    /// <summary>The four provisioned definition keys.</summary>
    internal static string[] Ids => Rows.Select(row => row.Id).ToArray();
}
