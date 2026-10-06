using GameServer.Application.Battle;
using GameServer.Domain.Battle;
using GameServer.Domain.Bosses;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;

namespace GameServer.Application.Tests.Balance;

/// <summary>
/// Canonical definitions and variants for the TASK-197 controlled balance experiment
/// matrices (EXP-01 through EXP-05).
///
/// <b>These definitions introduce zero unauthored gameplay mechanics.</b> Every Card,
/// Relic, Boss, and Pet definition adheres strictly to authoritative rules:
/// <list type="bullet">
/// <item><c>CARD_RULES.md</c> §2 / §4.1 for the 8 canonical Cards;</item>
/// <item><c>RELIC_RULES.md</c> §6 / §8.5 for the 10 canonical Relics;</item>
/// <item><c>BOSS_RULES.md</c> §6.1–§6.4 for the 5 MVP Bosses;</item>
/// <item><c>PET_RULES.md</c> §1 / §8 for the 5 MVP Pets.</item>
/// </list>
/// </summary>
internal static class BalanceControlledExperimentDefinitions
{
    public static readonly PlayerId Owner = new("player-balance-exp");

    public static readonly IReadOnlyList<ulong> Seeds =
        [20261001UL, 20261002UL, 20261003UL, 20261004UL, 20261005UL];

    // =========================================================================
    // Canonical 8 Cards (CARD_RULES.md §2 / §4.1)
    // =========================================================================

    public static CardDefinition CardHeal(int cost = 20) => new()
    {
        CardDefinitionId = "card-heal",
        Name = "Heal",
        Category = CardCategory.Basic,
        PowerCost = cost,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20)),
    };

    public static CardDefinition CardShield(int cost = 20) => new()
    {
        CardDefinitionId = "card-shield",
        Name = "Shield",
        Category = CardCategory.Basic,
        PowerCost = cost,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)),
    };

    public static CardDefinition CardPowerCharge(int cost = 0) => new()
    {
        CardDefinitionId = "card-power-charge",
        Name = "Power Charge",
        Category = CardCategory.Basic,
        PowerCost = cost,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25)),
    };

    public static CardDefinition CardInferno(int cost = 100) => new()
    {
        CardDefinitionId = "card-inferno",
        Name = "Inferno",
        Category = CardCategory.PetSkill,
        PowerCost = cost,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2)),
    };

    public static CardDefinition CardTidalBarrier(int cost = 80) => new()
    {
        CardDefinitionId = "card-tidal-barrier",
        Name = "Tidal Barrier",
        Category = CardCategory.PetSkill,
        PowerCost = cost,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20),
            CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20)),
    };

    public static CardDefinition CardIronFang(int cost = 100) => new()
    {
        CardDefinitionId = "card-iron-fang",
        Name = "Iron Fang",
        Category = CardCategory.PetSkill,
        PowerCost = cost,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120),
            CardEffectDefinition.Crit(10, "NextAttack")),
    };

    public static CardDefinition CardVenomousBloom(int cost = 80) => new()
    {
        CardDefinitionId = "card-venomous-bloom",
        Name = "Venomous Bloom",
        Category = CardCategory.PetSkill,
        PowerCost = cost,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 80),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 25, 2)),
    };

    public static CardDefinition CardEarthshaker(int cost = 100) => new()
    {
        CardDefinitionId = "card-earthshaker",
        Name = "Earthshaker",
        Category = CardCategory.PetSkill,
        PowerCost = cost,
        LoadoutCopyLimit = 1,
        EffectDefinition = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 150)),
    };

    /// <summary>
    /// Baseline card set where Iron Fang has PowerCost = 40 (for continuity with TASK-194 baseline).
    /// </summary>
    public static IReadOnlyList<CardDefinition> BaselineCards(int powerChargeCost = 0) =>
    [
        CardHeal(),
        CardShield(),
        CardPowerCharge(powerChargeCost),
        CardIronFang(cost: 40),
    ];

    /// <summary>
    /// Canonical 8 cards with authored costs (Iron Fang at 100).
    /// </summary>
    public static IReadOnlyList<CardDefinition> CanonicalEightCards(int powerChargeCost = 0) =>
    [
        CardHeal(),
        CardShield(),
        CardPowerCharge(powerChargeCost),
        CardInferno(),
        CardTidalBarrier(),
        CardIronFang(cost: 100),
        CardVenomousBloom(),
        CardEarthshaker(),
    ];

    // =========================================================================
    // Canonical 10 Relics (RELIC_RULES.md §6 / §8.5)
    // =========================================================================

    public static readonly IReadOnlyList<RelicDefinition> CanonicalTenRelics =
    [
        new RelicDefinition
        {
            RelicDefinitionId = "relic-berserker-core",
            Name = "Berserker Core",
            Trigger = "OnMatchCount",
            Condition = RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 3),
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.ATK, RelicEffectValueType.Percentage, 5, RelicEffectTarget.Pet, RelicEffectLifetime.Battle)),
        },
        new RelicDefinition
        {
            RelicDefinitionId = "relic-mana-crystal",
            Name = "Mana Crystal",
            Trigger = "OnMatchCount",
            Condition = RelicCondition.Create(RelicConditionType.MatchCountAtLeast, 4),
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.Power, RelicEffectValueType.Flat, 10, RelicEffectTarget.Pet, RelicEffectLifetime.Immediate)),
        },
        new RelicDefinition
        {
            RelicDefinitionId = "relic-assassin-eye",
            Name = "Assassin Eye",
            Trigger = "OnCombo",
            Condition = RelicCondition.Create(RelicConditionType.ComboAtLeast, 3),
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 10, RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack)),
        },
        new RelicDefinition
        {
            RelicDefinitionId = "relic-emergency-core",
            Name = "Emergency Core",
            Trigger = "OnHpBelow",
            Condition = RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30),
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.CardCost, RelicEffectValueType.Percentage, 50, RelicEffectTarget.Pet, RelicEffectLifetime.Battle)),
        },
        new RelicDefinition
        {
            RelicDefinitionId = "relic-burning-curse",
            Name = "Burning Curse",
            Trigger = "OnBattleStart",
            Condition = null,
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.BurnDamage, RelicEffectValueType.Percentage, 30, RelicEffectTarget.Pet, RelicEffectLifetime.Battle)),
        },
        new RelicDefinition
        {
            RelicDefinitionId = "relic-combo-fang",
            Name = "Combo Fang",
            Trigger = "OnCombo",
            Condition = RelicCondition.Create(RelicConditionType.ComboAtLeast, 5),
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 20, RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack)),
        },
        new RelicDefinition
        {
            RelicDefinitionId = "relic-arcane-battery",
            Name = "Arcane Battery",
            Trigger = "OnPowerGain",
            Condition = null,
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.Power, RelicEffectValueType.Flat, 5, RelicEffectTarget.Pet, RelicEffectLifetime.Immediate)),
        },
        new RelicDefinition
        {
            RelicDefinitionId = "relic-execution-mark",
            Name = "Execution Mark",
            Trigger = "OnHpBelow",
            Condition = RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30),
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.Crit, RelicEffectValueType.PercentagePoints, 15, RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack)),
        },
        new RelicDefinition
        {
            RelicDefinitionId = "relic-cascade-core",
            Name = "Cascade Core",
            Trigger = "OnCascade",
            Condition = null,
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.Power, RelicEffectValueType.Flat, 5, RelicEffectTarget.Pet, RelicEffectLifetime.Immediate)),
        },
        new RelicDefinition
        {
            RelicDefinitionId = "relic-battle-instinct",
            Name = "Battle Instinct",
            Trigger = "OnDamageTaken",
            Condition = null,
            EffectDefinition = RelicEffectDefinitions.Create(
                RelicEffectDefinition.Create(RelicEffectType.ATK, RelicEffectValueType.Percentage, 10, RelicEffectTarget.Pet, RelicEffectLifetime.NextAttack)),
        },
    ];

    // =========================================================================
    // Canonical 5 Pets (PET_RULES.md §1 / §8)
    // =========================================================================

    public static BattleStateService.PetConfiguration PetXichLang(
        EquippedCardIdentity[] cards,
        EquippedRelicIdentity[]? relics = null) => new(
        PetId: new PetId("pet-xich-lang"),
        Element: Element.Hoa,
        PassiveId: new PassiveId("passive-xich-lang"),
        PassiveThreshold: 5,
        PassiveResetOverride: null,
        EquippedRelics: relics,
        EquippedCards: cards);

    public static BattleStateService.PetConfiguration PetHuyenQuy(
        EquippedCardIdentity[] cards,
        EquippedRelicIdentity[]? relics = null) => new(
        PetId: new PetId("pet-huyen-quy"),
        Element: Element.Thuy,
        PassiveId: new PassiveId("passive-huyen-quy"),
        PassiveThreshold: 6,
        PassiveResetOverride: null,
        EquippedRelics: relics,
        EquippedCards: cards);

    public static BattleStateService.PetConfiguration PetBachHo(
        EquippedCardIdentity[] cards,
        EquippedRelicIdentity[]? relics = null) => new(
        PetId: new PetId("pet-bach-ho"),
        Element: Element.Kim,
        PassiveId: new PassiveId("passive-bach-ho"),
        PassiveThreshold: 4,
        PassiveResetOverride: null,
        EquippedRelics: relics,
        EquippedCards: cards);

    public static BattleStateService.PetConfiguration PetThanhXa(
        EquippedCardIdentity[] cards,
        EquippedRelicIdentity[]? relics = null) => new(
        PetId: new PetId("pet-thanh-xa"),
        Element: Element.Moc,
        PassiveId: new PassiveId("passive-thanh-xa"),
        PassiveThreshold: 7,
        PassiveResetOverride: null,
        EquippedRelics: relics,
        EquippedCards: cards);

    public static BattleStateService.PetConfiguration PetSonHung(
        EquippedCardIdentity[] cards,
        EquippedRelicIdentity[]? relics = null) => new(
        PetId: new PetId("pet-son-hung"),
        Element: Element.Tho,
        PassiveId: new PassiveId("passive-son-hung"),
        PassiveThreshold: 5,
        PassiveResetOverride: null,
        EquippedRelics: relics,
        EquippedCards: cards);

    // =========================================================================
    // Boss Variants (EXP-01)
    // =========================================================================

    public static BossDefinition MocYeuAuthored() =>
        BossDefinitions.All.First(b => b.BossId.Value == "boss-moc-yeu") with
        {
            // Historical baseline variant for EXP-01 (5% MaxHP every 5 matches)
            PassiveDefinition = new BossPassiveDefinition(
                new PassiveId("boss-moc-yeu-regen"),
                5,
                "Default"),
        };

    public static BossDefinition MocYeuThreshold8() =>
        BossDefinitions.All.First(b => b.BossId.Value == "boss-moc-yeu") with
        {
            PassiveDefinition = new BossPassiveDefinition(
                new PassiveId("boss-moc-yeu-regen"),
                8,
                "Default"),
        };
}
