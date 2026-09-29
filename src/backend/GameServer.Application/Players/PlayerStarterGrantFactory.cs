using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Relics;
using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;

namespace GameServer.Application.Players;

/// <summary>
/// Builds the deterministic starter ownership set for a newly created Player
/// (<c>DATABASE.md</c> §2 items 1–4; TASK-084).
///
/// <code>
/// provisioned definition rows   (TASK-085 — DATABASE.md §1)
///         ↓  resolve by the canonical starter identities
/// PlayerStarterGrant
///   1 Pet    pet-xich-lang
///   3 Cards  card-heal, card-shield, card-power-charge
///   3 Relics relic-berserker-core, relic-mana-crystal, relic-assassin-eye
/// </code>
///
/// <b>The composition is an Application concern.</b> Which content defines a
/// starter set is orchestration, not a game rule (<c>ARCHITECTURE.md</c> §2.1:
/// "Application orchestrates Domain calls … It does not contain game rule logic
/// itself — only sequencing and coordination"). The rows it builds are Domain
/// types, and their values come from the provisioned definitions, never from
/// this class.
///
/// <b>It references provisioned definitions; it never authors content.</b> Every
/// starter identity below is read against the provisioned <c>PetDefinition</c> /
/// <c>CardDefinition</c> / <c>RelicDefinition</c> rows (<c>DATABASE.md</c> §5
/// item 4), and a missing row is reported rather than substituted
/// (<c>AGENTS.md</c> §7, TASK-083 §"Stop Conditions"). No name, cost, effect,
/// trigger, or condition is copied onto an ownership row: those live on the
/// definition and are read through the foreign key
/// (<c>GAME_STATE.md</c> §0 item 5 — no parallel representation).
///
/// <b>It is deterministic and server-only.</b> The set is a fixed server-side
/// set: no RNG, no ordering by time, no per-Player variation, and no request
/// value selects, adds to, or replaces any of it (<c>GAME_RULES.md</c> §18,
/// ADR-001). Two newly created Players receive the same definition references;
/// only the minted instance identities and the acquisition timestamps differ,
/// and <c>RELIC_RULES.md</c> §2.3 item 2 forbids ordering a loadout by that
/// timestamp.
/// </summary>
public sealed class PlayerStarterGrantFactory
{
    /// <summary>
    /// The starter Pet — <b>Xích Lang</b> (<c>DATABASE.md</c> §2 item 1).
    ///
    /// It is one of the three Pets <c>PET_RULES.md</c> §8 records as
    /// provisioned (Xích Lang / Bạch Hổ / Huyền Quy) and is the one the
    /// TASK-084 contract selects. The deferred rows are never used.
    /// </summary>
    public const string StarterPetDefinitionId = "pet-xich-lang";

    /// <summary>
    /// The three starter Basic Cards (<c>DATABASE.md</c> §2 item 1;
    /// <c>CARD_RULES.md</c> §2): Heal, Shield, and Power Charge.
    ///
    /// They are <b>all three</b> content-defined Basic Cards — not a selection:
    /// <c>CARD_RULES.md</c> §1 fixes the submitted Basic loadout at exactly 3
    /// Basic Cards and §2 defines exactly three. A <c>Category = PetSkill</c>
    /// Card is excluded by contract (<c>CARD_RULES.md</c> §1 item 4): the Pet
    /// Skill is derived from the active Pet's <c>SignatureSkillCardId</c> at
    /// battle start and is never an unlock row.
    /// </summary>
    public static readonly string[] StarterCardDefinitionIds =
    [
        "card-heal",
        "card-shield",
        "card-power-charge",
    ];

    /// <summary>
    /// The three starter Relic definitions (<c>DATABASE.md</c> §2 item 1;
    /// <c>RELIC_RULES.md</c> §6 note 3): Berserker Core, Mana Crystal, and
    /// Assassin Eye.
    ///
    /// This is an <b>explicit named Product Owner selection</b>, not an
    /// ordering rule: it is deliberately not "the first three in document
    /// order". <c>Emergency Core</c> is provisioned but not selected, and
    /// <c>Burning Curse</c> is deferred.
    ///
    /// Three instances satisfy <c>RELIC_RULES.md</c> §2.1's lower bound of
    /// three, and exactly one owned instance is granted per definition — so no
    /// duplicate-selection question is opened (<c>RELIC_RULES.md</c> §2.4).
    /// </summary>
    public static readonly string[] StarterRelicDefinitionIds =
    [
        "relic-berserker-core",
        "relic-mana-crystal",
        "relic-assassin-eye",
    ];

    private readonly IPetRepository _pets;
    private readonly ICardRepository _cards;
    private readonly IRelicRepository _relics;

    public PlayerStarterGrantFactory(
        IPetRepository pets,
        ICardRepository cards,
        IRelicRepository relics)
    {
        _pets = pets ?? throw new ArgumentNullException(nameof(pets));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _relics = relics ?? throw new ArgumentNullException(nameof(relics));
    }

    /// <summary>
    /// Resolves the starter set and returns it staged as one
    /// <see cref="PlayerStarterGrant"/>.
    ///
    /// <b>Every definition is verified before anything is staged.</b> All seven
    /// definition references are resolved first, and a missing row aborts the
    /// whole grant — so a partially-resolvable starter set is never handed to
    /// the persistence boundary, and no value is fabricated to fill a gap
    /// (<c>AGENTS.md</c> §7; TASK-083 §"Stop Conditions": "a required
    /// provisioned definition row is absent → the starter set must not be
    /// partially created").
    ///
    /// <b>Why no owner is taken here.</b> The owning <c>PlayerId</c> is minted
    /// by the creation boundary, which is also the only place the creation
    /// branch can be observed — so the composition deliberately addresses no
    /// Player and the boundary binds the owner when it stages the rows. This
    /// keeps "creation-time only" structural: there is no overload of this
    /// operation that could be called for an already-existing Player.
    /// </summary>
    /// <param name="acquiredAt">
    /// The server clock reading stamped on the Pet and Relic rows. It is
    /// assigned by the caller from the server's clock and is never taken from
    /// client input (<c>DATABASE.md</c> §1: <c>AcquiredAt</c> is a creation
    /// timestamp, set once).
    /// </param>
    /// <param name="cancellationToken">Cancels the definition reads.</param>
    /// <exception cref="InvalidOperationException">
    /// A required provisioned definition row does not exist. The starter set is
    /// not partially created and no substitute is used.
    /// </exception>
    public async Task<PlayerStarterGrant> CreateAsync(
        DateTimeOffset acquiredAt,
        CancellationToken cancellationToken = default)
    {
        // Verify the Pet definition first, then both content sets, before any
        // row is constructed. A missing definition fails the whole grant.
        var petDefinition = await _pets
            .GetDefinitionAsync(StarterPetDefinitionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw MissingDefinition("PetDefinition", StarterPetDefinitionId);

        // The Card definitions are resolved through the unrestricted content
        // lookup, not through ListUnlockedDefinitionsAsync: that read is
        // Player-scoped by contract and would report these definitions as
        // absent for a Player who does not own them yet — which is exactly the
        // Player being created here (ICardRepository.GetDefinitionAsync is
        // explicitly documented as "not an ownership check").
        var resolvedCards = new CardDefinition[StarterCardDefinitionIds.Length];

        for (var index = 0; index < StarterCardDefinitionIds.Length; index++)
        {
            var cardDefinitionId = StarterCardDefinitionIds[index];

            resolvedCards[index] = await _cards
                .GetDefinitionAsync(cardDefinitionId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw MissingDefinition("CardDefinition", cardDefinitionId);
        }

        var relicDefinitions = new RelicDefinition[StarterRelicDefinitionIds.Length];

        for (var index = 0; index < StarterRelicDefinitionIds.Length; index++)
        {
            var relicDefinitionId = StarterRelicDefinitionIds[index];

            relicDefinitions[index] = await _relics
                .GetDefinitionAsync(relicDefinitionId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw MissingDefinition("RelicDefinition", relicDefinitionId);
        }

        // DATABASE.md §2 item 1: the starter Pet is the one instance carrying
        // the documented creation values — Tier Common (the MVP data default),
        // Star = Pet.MinStar (the documented 1-5 floor), XP = Pet.InitialXp (0),
        // Level = Pet.InitialLevel (1), AcquiredAt server-set (DATABASE.md §3;
        // PET_RULES.md §3, §4, §5.2).
        var starterPet = new Pet
        {
            // The instance identity is minted server-side and is never the
            // definition id (DATABASE.md §1: PetInstanceId is a PK distinct
            // from PetDefinitionId). It is the value GAME_STATE.md §2.3 carries
            // as PetState.PetId and what POST /api/battle/start submits as
            // `petId`.
            PetInstanceId = $"petinst_{Guid.NewGuid():N}",

            // The owner is not known here: the creation boundary mints the
            // PlayerId and binds it when it stages the rows. It is never
            // empty in a persisted row.
            PlayerId = string.Empty,

            PetDefinitionId = petDefinition.PetDefinitionId,
            Tier = PetTier.Common,
            Star = Pet.MinStar,
            XP = Pet.InitialXp,
            Level = Pet.InitialLevel,
            AcquiredAt = acquiredAt,
        };

        var starterCards = new PlayerUnlockedCard[StarterCardDefinitionIds.Length];

        for (var index = 0; index < StarterCardDefinitionIds.Length; index++)
        {
            // DATABASE.md §1: the unlock row has exactly two members and no
            // third column exists. No acquisition timestamp is recorded: the
            // table has no such column, and ownership is a flag rather than an
            // inventory row (ADR-012 item 9).
            starterCards[index] = new PlayerUnlockedCard
            {
                PlayerId = string.Empty,
                CardDefinitionId = resolvedCards[index].CardDefinitionId,
            };
        }

        var starterRelics = new Relic[StarterRelicDefinitionIds.Length];

        for (var index = 0; index < StarterRelicDefinitionIds.Length; index++)
        {
            // RELIC_RULES.md §2.2: the owned instance's identity is distinct
            // from its definition's and the two are never collapsed. No
            // document fixes an instance-id format, so it is minted here and
            // must never be the RelicDefinitionId.
            starterRelics[index] = new Relic
            {
                RelicInstanceId = $"relicinst_{Guid.NewGuid():N}",
                PlayerId = string.Empty,
                RelicDefinitionId = relicDefinitions[index].RelicDefinitionId,
                AcquiredAt = acquiredAt,
            };
        }

        return new PlayerStarterGrant(starterPet, starterCards, starterRelics);
    }

    private static InvalidOperationException MissingDefinition(
        string definitionTable,
        string definitionId) =>
        new(
            $"The starter ownership set requires {definitionTable} row "
            + $"'{definitionId}', which does not exist. The starter set is not "
            + "partially created and no substitute definition is used "
            + "(DATABASE.md §2 item 1; AGENTS.md §7).");
}
