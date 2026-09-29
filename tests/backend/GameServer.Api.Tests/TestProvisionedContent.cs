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
    /// The three Pet definitions TASK-085 provisioned (<c>PET_RULES.md</c> §8).
    /// </summary>
    internal static readonly string[] ProvisionedPetDefinitionIds =
        ["pet-xich-lang", "pet-bach-ho", "pet-huyen-quy"];

    /// <summary>
    /// The six Card definitions TASK-085 provisioned — the three Basic Cards
    /// (<c>CARD_RULES.md</c> §2) and the three Pet Skill Cards (§4.1).
    /// </summary>
    internal static readonly string[] ProvisionedCardDefinitionIds =
    [
        "card-heal", "card-shield", "card-power-charge",
        "card-inferno", "card-tidal-barrier", "card-iron-fang",
    ];

    /// <summary>
    /// The four Relic definitions TASK-085 provisioned
    /// (<c>RELIC_RULES.md</c> §6 note 3).
    /// </summary>
    internal static readonly string[] ProvisionedRelicDefinitionIds =
    [
        "relic-berserker-core", "relic-mana-crystal",
        "relic-assassin-eye", "relic-emergency-core",
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
        var cards = new (string Id, string Name, CardCategory Category)[]
        {
            ("card-heal", "Heal", CardCategory.Basic),
            ("card-shield", "Shield", CardCategory.Basic),
            ("card-power-charge", "Power Charge", CardCategory.Basic),
            ("card-inferno", "Inferno", CardCategory.PetSkill),
            ("card-tidal-barrier", "Tidal Barrier", CardCategory.PetSkill),
            ("card-iron-fang", "Iron Fang", CardCategory.PetSkill),
        };

        foreach (var (id, name, category) in cards)
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
                PowerCost = 0,
                EffectDefinition = "seeded for the starter-ownership bootstrap tests",
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
                Condition = "seeded",
                EffectDefinition = "seeded for the starter-ownership bootstrap tests",
            });
        }
    }
}
