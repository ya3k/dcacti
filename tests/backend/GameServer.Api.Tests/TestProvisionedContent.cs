using GameServer.Application.Cards;
using GameServer.Application.Players;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Api.Tests;

/// <summary>
/// Seeds the provisioned content the starter-ownership bootstrap references into
/// an API test host's isolated store.
///
/// <b>Why the API hosts need this.</b> A newly created Player receives the
/// starter ownership set (<c>DATABASE.md</c> §2 item 1), and its rows are foreign
/// keys into the provisioned <c>PetDefinition</c> / <c>CardDefinition</c> /
/// <c>RelicDefinition</c> content. In production those rows exist because TASK-085
/// provisioned them by migration; an in-memory test store applies no migration,
/// so the content must be supplied for the authentication path to exercise its
/// real behavior.
///
/// <b>This helper does not define the starter set.</b> It inserts the content the
/// starter set <i>references</i>, using the canonical identities TASK-085
/// provisioned. Which of those the starter set selects, and with what creation
/// values, is owned by <see cref="PlayerStarterGrantFactory"/> and
/// <c>DATABASE.md</c> §2 item 1 — no selection is restated here. The non-starter
/// definitions are included deliberately, so a test can assert they are
/// <b>not</b> granted.
/// </summary>
internal static class TestProvisionedContent
{
    /// <summary>
    /// The five Pet definitions the provisioning migrations created
    /// (<c>PET_RULES.md</c> §8: TASK-085's three plus TASK-173's two).
    /// </summary>
    internal static readonly string[] ProvisionedPetDefinitionIds =
        ["pet-xich-lang", "pet-bach-ho", "pet-huyen-quy", "pet-thanh-xa", "pet-son-hung"];

    /// <summary>
    /// The eight Card definitions the provisioning migrations created — the
    /// three Basic Cards (<c>CARD_RULES.md</c> §2) and the five Pet Skill Cards
    /// (§4.1, one per Pet).
    /// </summary>
    internal static readonly string[] ProvisionedCardDefinitionIds =
    [
        "card-heal", "card-shield", "card-power-charge",
        "card-inferno", "card-tidal-barrier", "card-iron-fang",
        "card-venomous-bloom", "card-earthshaker",
    ];

    /// <summary>
    /// The ten Relic definitions the provisioning migrations created
    /// (<c>RELIC_RULES.md</c> §6).
    /// </summary>
    internal static readonly string[] ProvisionedRelicDefinitionIds =
    [
        "relic-berserker-core", "relic-mana-crystal",
        "relic-assassin-eye", "relic-emergency-core",
        "relic-burning-curse", "relic-combo-fang",
        "relic-arcane-battery", "relic-execution-mark",
        "relic-cascade-core", "relic-battle-instinct",
    ];

    /// <summary>
    /// Inserts the provisioned definition rows into the store behind
    /// <paramref name="services"/>, if they are not already present, so an
    /// existing-Player host that already seeded its own content is unaffected.
    /// </summary>
    internal static void Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        SeedPets(context);
        SeedCards(context);
        SeedRelics(context);

        context.SaveChanges();
    }

    private static void SeedPets(GameDbContext context)
    {
        // PET_RULES.md §8 records each provisioned Pet's Element; the Signature
        // Skill Card must resolve to the PetSkill Card of the same Pet.
        var pets = new (string Id, string Identity, Element Element, string SkillCardId)[]
        {
            ("pet-xich-lang", "Xích Lang", Element.Hoa, "card-inferno"),
            ("pet-bach-ho", "Bạch Hổ", Element.Kim, "card-iron-fang"),
            ("pet-huyen-quy", "Huyền Quy", Element.Thuy, "card-tidal-barrier"),
            ("pet-thanh-xa", "Thanh Xà", Element.Moc, "card-venomous-bloom"),
            ("pet-son-hung", "Sơn Hùng", Element.Tho, "card-earthshaker"),
        };

        foreach (var (id, identity, element, skillCardId) in pets)
        {
            if (context.PetDefinitions.Any(definition => definition.PetDefinitionId == id))
            {
                continue;
            }

            context.PetDefinitions.Add(new PetDefinition
            {
                PetDefinitionId = id,
                Identity = identity,
                Element = element,
                PassiveId = new PassiveId($"passive-{id["pet-".Length..]}"),
                PassiveThreshold = 5,
                SignatureSkillCardId = skillCardId,
            });
        }
    }

    private static void SeedCards(GameDbContext context)
    {
        var cards = new (string Id, string Name, CardCategory Category, int PowerCost, CardEffectDefinitions Effects)[]
        {
            (
                "card-heal",
                "Heal",
                CardCategory.Basic,
                20,
                CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 20))),
            (
                "card-shield",
                "Shield",
                CardCategory.Basic,
                20,
                CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 20))),
            (
                "card-power-charge",
                "Power Charge",
                CardCategory.Basic,
                0,
                CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(CardEffectType.Power, CardEffectValueType.Flat, 25))),
            (
                "card-inferno",
                "Inferno",
                CardCategory.PetSkill,
                40,
                CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
                    CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, 2))),
            (
                "card-tidal-barrier",
                "Tidal Barrier",
                CardCategory.PetSkill,
                40,
                CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(CardEffectType.Heal, CardEffectValueType.PercentMaxHp, 30),
                    CardEffectDefinition.Create(CardEffectType.Shield, CardEffectValueType.PercentMaxHp, 30))),
            (
                "card-iron-fang",
                "Iron Fang",
                CardCategory.PetSkill,
                40,
                CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 150),
                    CardEffectDefinition.Crit(30, "NextAttack"))),
            (
                "card-venomous-bloom",
                "Venomous Bloom",
                CardCategory.PetSkill,
                40,
                CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 120))),
            (
                "card-earthshaker",
                "Earthshaker",
                CardCategory.PetSkill,
                40,
                CardEffectDefinitions.Create(
                    CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 140))),
        };

        foreach (var (id, name, category, powerCost, effects) in cards)
        {
            if (context.CardDefinitions.Any(definition => definition.CardDefinitionId == id))
            {
                continue;
            }

            context.CardDefinitions.Add(new CardDefinition
            {
                CardDefinitionId = id,
                Name = name,
                Category = category,
                PowerCost = powerCost,
                EffectDefinition = effects,
                LoadoutCopyLimit = 1,
            });
        }
    }

    private static void SeedRelics(GameDbContext context)
    {
        var relics = new (string Id, string Name)[]
        {
            ("relic-berserker-core", "Berserker Core"),
            ("relic-mana-crystal", "Mana Crystal"),
            ("relic-assassin-eye", "Assassin Eye"),
            ("relic-emergency-core", "Emergency Core"),
            ("relic-burning-curse", "Burning Curse"),
            ("relic-combo-fang", "Combo Fang"),
            ("relic-arcane-battery", "Arcane Battery"),
            ("relic-execution-mark", "Execution Mark"),
            ("relic-cascade-core", "Cascade Core"),
            ("relic-battle-instinct", "Battle Instinct"),
        };

        foreach (var (id, name) in relics)
        {
            if (context.RelicDefinitions.Any(definition => definition.RelicDefinitionId == id))
            {
                continue;
            }

            context.RelicDefinitions.Add(new RelicDefinition
            {
                RelicDefinitionId = id,
                Name = name,
                Trigger = "seeded",
                Condition = TestRelicEffects.Condition,
                EffectDefinition = TestRelicEffects.Effect,
            });
        }
    }
}
