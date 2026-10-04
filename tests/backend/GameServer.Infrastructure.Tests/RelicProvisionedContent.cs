using GameServer.Domain.Relics;

namespace GameServer.Infrastructure.Tests;

/// <summary>
/// The ten provisioned <c>RelicDefinition</c> rows as their
/// <b>structured</b> values — <c>RELIC_RULES.md</c> §8.5's "Canonical Relic
/// Contract" table, which owns every value below and states that none of them is
/// authored by it (each is transcribed from §6 and §8.1–§8.4).
///
/// <b>Why a shared helper.</b> Two Infrastructure tests need the same rows:
/// the per-row PostgreSQL assertion in
/// <see cref="PetCardRelicDefinitionPostgresProvisioningTests"/> and the
/// storage-contract suite in <see cref="RelicStructuredStorageTests"/>
/// (TASK-132). Holding them once keeps the transcription in a single place
/// instead of letting two files drift.
///
/// <b>These values are transcribed, never computed or invented</b>
/// (<c>DATABASE.md</c> §5 item 4 rule (b)). They mirror exactly what the three
/// Relic migrations write and what <c>RELIC_RULES.md</c> §8.5 records:
/// <list type="bullet">
/// <item>the first four rows — <c>20260929152651_ProvisionPetCardRelicContentDefinitions</c>
/// (identity and Trigger) and <c>20261003074309_StructureRelicDefinitionStructuredColumns</c>
/// (the structured Condition and EffectDefinition);</item>
/// <item>the remaining six — <c>20261004153916_ProvisionRemainingMvpRelicDefinitions</c>
/// (TASK-184), which writes them born structured.</item>
/// </list>
/// A test that asserts them is asserting the documented contract, not the
/// implementation's current output.
///
/// <b>All ten are held here, and that is deliberate.</b> The MVP Relic set is
/// closed at ten by <c>RELIC_RULES.md</c> §6, so the table is complete rather
/// than partial: a helper holding only the first four would make every
/// "exactly the canonical set" assertion depend on a second, separately
/// maintained list.
///
/// <b><c>Trigger</c> is included but is not part of §8's restructuring.</b>
/// <c>RELIC_RULES.md</c> §8.5 item 3 (TASK-131 D8) leaves §3's closed list
/// unchanged, so it stays the prose §3 identity the provisioning migration
/// stored.
/// </summary>
internal static class RelicProvisionedContent
{
    /// <summary>
    /// One provisioned row: its key, §3 Trigger, and §8.1/§8.2 values.
    ///
    /// <see cref="Condition"/> is <b>nullable on purpose</b>: <c>RELIC_RULES.md</c>
    /// §8.1 item 4 makes the member optional, and §8.5 records <c>null</c> for the
    /// four Relics whose Trigger alone is their complete condition (Burning Curse,
    /// Arcane Battery, Cascade Core, Battle Instinct). An absent condition is
    /// <see langword="null"/> — never a sentinel condition, which would read as a
    /// real one.
    /// </summary>
    internal readonly record struct Row(
        string Id,
        string Name,
        string Trigger,
        RelicCondition? Condition,
        RelicEffectDefinitions Effects);

    /// <summary>
    /// A row whose Trigger alone is its complete condition — the §8.5 rows that
    /// record <c>null</c> for the §8.1 <c>Condition</c>.
    /// </summary>
    private static RelicCondition? NoCondition => null;

    /// <summary>
    /// The ten provisioned rows, in the order <c>RELIC_RULES.md</c> §8.5 lists
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

        // §8.5: Trigger OnBattleStart (§3); Condition null (§8.1 item 4 — the
        // Trigger alone is the complete condition); BurnDamage, Percentage, 30,
        // Pet, Battle — from §6's "+30% Burn damage" and §6 note 1, whose
        // Pet-owned-only reach is a runtime rule that has no column here.
        new(
            "relic-burning-curse",
            "Burning Curse",
            "OnBattleStart",
            NoCondition,
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.BurnDamage,
                    RelicEffectValueType.Percentage,
                    30,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.Battle))),

        // §8.5: Trigger OnCombo (§3); Condition ComboAtLeast(5);
        // Crit, PercentagePoints, 20, Pet, NextAttack — from §6's "+20pp Crit".
        new(
            "relic-combo-fang",
            "Combo Fang",
            "OnCombo",
            RelicCondition.Create(RelicConditionType.ComboAtLeast, 5),
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.Crit,
                    RelicEffectValueType.PercentagePoints,
                    20,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.NextAttack))),

        // §8.5: Trigger OnPowerGain (§3, qualification boundary §3.1);
        // Condition null; Power, Flat, 5, Pet, Immediate — from §6's "+5 Power".
        new(
            "relic-arcane-battery",
            "Arcane Battery",
            "OnPowerGain",
            NoCondition,
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.Power,
                    RelicEffectValueType.Flat,
                    5,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.Immediate))),

        // §8.5: Trigger OnHpBelow (§3); Condition HpPercentageBelow(30);
        // Crit, PercentagePoints, 15, Pet, NextAttack — from §6's "+15pp Crit"
        // and §6 note 3.
        new(
            "relic-execution-mark",
            "Execution Mark",
            "OnHpBelow",
            RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30),
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.Crit,
                    RelicEffectValueType.PercentagePoints,
                    15,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.NextAttack))),

        // §8.5: Trigger OnCascade (§3, per-iteration event boundary §3.2);
        // Condition null; Power, Flat, 5, Pet, Immediate — from §6's "+5 Power".
        new(
            "relic-cascade-core",
            "Cascade Core",
            "OnCascade",
            NoCondition,
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.Power,
                    RelicEffectValueType.Flat,
                    5,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.Immediate))),

        // §8.5: Trigger OnDamageTaken (§3); Condition null; ATK, Percentage, 10,
        // Pet, NextAttack — from §6's "+10% ATK". This is the §8.3 table's
        // second ATK row (TASK-176; §8.5 item 10).
        new(
            "relic-battle-instinct",
            "Battle Instinct",
            "OnDamageTaken",
            NoCondition,
            RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(
                    RelicEffectType.ATK,
                    RelicEffectValueType.Percentage,
                    10,
                    RelicEffectTarget.Pet,
                    RelicEffectLifetime.NextAttack))),
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

    /// <summary>
    /// The ten provisioned definition keys — the complete canonical MVP Relic
    /// set (<c>RELIC_RULES.md</c> §6, §8.5).
    /// </summary>
    internal static string[] Ids => Rows.Select(row => row.Id).ToArray();
}
