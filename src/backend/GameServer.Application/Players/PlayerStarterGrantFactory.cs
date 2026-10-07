using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Relics;
using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;

namespace GameServer.Application.Players;

/// <summary>
/// Builds the deterministic MVP content ownership set for a newly created Player
/// (<c>DATABASE.md</c> §2 items 1–4; <c>MVP_SCOPE.md</c> §1 "Content ownership &
/// reachability"; TASK-213 decision; TASK-221 implementation).
///
/// <code>
/// provisioned definition rows   (TASK-085 / TASK-172 / TASK-173 — DATABASE.md §1)
///         ↓  resolve by the canonical MVP content identities
/// PlayerStarterGrant
///   5 Pets   one owned instance per MVP Pet definition
///   3 Cards  card-heal, card-shield, card-power-charge
///   10 Relics one owned instance per MVP Relic definition
/// </code>
///
/// <b>The grant IS the MVP content grant.</b> MVP has no post-creation
/// acquisition system (<c>MVP_SCOPE.md</c> §1, §3): an account's owned content set
/// is fixed at creation, so this composition delivers the whole provisioned MVP
/// content set rather than a minimum bootstrap. That is what makes the documented
/// pre-battle flow real — choosing one of five Pets
/// (<c>GDD.md</c> §2, <c>PET_RULES.md</c> §2.1) and selecting 3–5 of ten owned
/// Relics (<c>RELIC_RULES.md</c> §2.1 item 1) — using only content that is
/// already provisioned.
///
/// <b>No content is authored here and nothing is acquired after creation.</b>
/// Every identity below is read against the provisioned <c>PetDefinition</c> /
/// <c>CardDefinition</c> / <c>RelicDefinition</c> rows (<c>DATABASE.md</c> §5
/// item 4), and a missing row is reported rather than substituted
/// (<c>AGENTS.md</c> §7, TASK-083 §"Stop Conditions"). No name, cost, effect,
/// trigger, condition, or magnitude is copied onto an ownership row: those live
/// on the definition and are read through the foreign key
/// (<c>GAME_STATE.md</c> §0 item 5 — no parallel representation).
///
/// <b>The composition is an Application concern.</b> Which content defines the
/// grant is orchestration, not a game rule (<c>ARCHITECTURE.md</c> §2.1:
/// "Application orchestrates Domain calls … It does not contain game rule logic
/// itself — only sequencing and coordination"). The rows it builds are Domain
/// types, and their values come from the provisioned definitions, never from
/// this class.
///
/// <b>It is deterministic and server-only.</b> The set is a fixed server-side
/// set: no RNG, no ordering by time, no per-Player variation, and no request
/// value selects, adds to, or replaces any of it (<c>GAME_RULES.md</c> §18,
/// ADR-001). Two newly created Players receive the same definition references;
/// only the minted instance identities and the acquisition timestamps differ,
/// and <c>RELIC_RULES.md</c> §2.3 item 2 forbids ordering a Relic loadout by that
/// timestamp.
///
/// <b>Card ownership does not create a Card build decision.</b> All three
/// content-defined Basic Cards are granted, because the loadout is exactly three
/// Basic Cards and exactly three exist (<c>CARD_RULES.md</c> §1, §2): the Card
/// slot is fixed by content, not by ownership. A <c>Category = PetSkill</c> Card
/// is excluded by contract (<c>CARD_RULES.md</c> §1 item 4): the Pet Skill is
/// derived from the active Pet's <c>SignatureSkillCardId</c> at battle start and
/// is never an unlock row.
/// </summary>
public sealed class PlayerStarterGrantFactory
{
    /// <summary>
    /// The five MVP Pet definitions, one owned instance each
    /// (<c>MVP_SCOPE.md</c> §1 "5 Pets"; <c>PET_RULES.md</c> §8).
    ///
    /// They are <b>all five</b> provisioned Pets — not a selection. Which one is
    /// the active Pet for a battle stays the player's choice
    /// (<c>PET_RULES.md</c> §2.1 item 1: a Player may own an arbitrary number of
    /// Pets; the active one is a loadout decision), and granting all five is what
    /// makes that decision reachable.
    ///
    /// The order is <c>PET_RULES.md</c> §8's own document order. It is
    /// <b>presentation-neutral</b>: no rule reads a Pet array position, and the
    /// ownership read (<c>IPetRepository.ListByPlayerIdAsync</c>) is not an
    /// ordered contract.
    /// </summary>
    public static readonly string[] StarterPetDefinitionIds =
    [
        "pet-xich-lang",
        "pet-bach-ho",
        "pet-huyen-quy",
        "pet-thanh-xa",
        "pet-son-hung",
    ];

    /// <summary>
    /// The three starter Basic Cards (<c>DATABASE.md</c> §2 item 1;
    /// <c>CARD_RULES.md</c> §2): Heal, Shield, and Power Charge.
    ///
    /// They are <b>all three</b> content-defined Basic Cards — not a selection:
    /// <c>CARD_RULES.md</c> §1 fixes the submitted Basic loadout at exactly 3
    /// Basic Cards and §2 defines exactly three.
    /// </summary>
    public static readonly string[] StarterCardDefinitionIds =
    [
        "card-heal",
        "card-shield",
        "card-power-charge",
    ];

    /// <summary>
    /// The ten MVP Relic definitions, one owned instance each
    /// (<c>MVP_SCOPE.md</c> §1 "10 Relics"; <c>RELIC_RULES.md</c> §6).
    ///
    /// They are <b>all ten</b> provisioned Relics — not a selection. Ownership is
    /// what makes the Relic slot a real build decision: the battle loadout is
    /// 3–5 pairwise-distinct owned instances (<c>RELIC_RULES.md</c> §2.1 item 1),
    /// so owning all ten makes every legal combination reachable without any
    /// acquisition, currency, or RNG.
    ///
    /// Exactly one owned instance is granted per definition
    /// (<c>RELIC_RULES.md</c> §2.4): the data model permits two instances of one
    /// definition, but MVP grants no duplicates, so no duplicate-selection
    /// question is opened.
    ///
    /// The order is <c>RELIC_RULES.md</c> §6's own table order. It is
    /// <b>presentation-neutral</b> for the same reason as the Pet list: equip
    /// slot order is the submitted request order (<c>RELIC_RULES.md</c> §2.3), and
    /// the ownership read is not an ordered contract.
    /// </summary>
    public static readonly string[] StarterRelicDefinitionIds =
    [
        "relic-berserker-core",
        "relic-mana-crystal",
        "relic-assassin-eye",
        "relic-emergency-core",
        "relic-burning-curse",
        "relic-combo-fang",
        "relic-arcane-battery",
        "relic-execution-mark",
        "relic-cascade-core",
        "relic-battle-instinct",
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
    /// Resolves the grant's content and returns it staged as one
    /// <see cref="PlayerStarterGrant"/>.
    ///
    /// <b>Every definition is verified before anything is staged.</b> All
    /// eighteen definition references are resolved first, and a missing row
    /// aborts the whole grant — so a partially-resolvable content set is never
    /// handed to the persistence boundary, and no value is fabricated to fill a
    /// gap (<c>AGENTS.md</c> §7; TASK-083 §"Stop Conditions": "a required
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
    /// A required provisioned definition row does not exist. The grant is not
    /// partially created and no substitute is used.
    /// </exception>
    public async Task<PlayerStarterGrant> CreateAsync(
        DateTimeOffset acquiredAt,
        CancellationToken cancellationToken = default)
    {
        // Verify every definition first, then every content set, before any row
        // is constructed. A missing definition fails the whole grant.
        var petDefinitions = await ResolveAsync(
            StarterPetDefinitionIds,
            "PetDefinition",
            _pets.GetDefinitionAsync,
            cancellationToken).ConfigureAwait(false);

        // The Card definitions are resolved through the unrestricted content
        // lookup, not through ListUnlockedDefinitionsAsync: that read is
        // Player-scoped by contract and would report these definitions as
        // absent for a Player who does not own them yet — which is exactly the
        // Player being created here (ICardRepository.GetDefinitionAsync is
        // explicitly documented as "not an ownership check").
        var cardDefinitions = await ResolveAsync(
            StarterCardDefinitionIds,
            "CardDefinition",
            _cards.GetDefinitionAsync,
            cancellationToken).ConfigureAwait(false);

        var relicDefinitions = await ResolveAsync(
            StarterRelicDefinitionIds,
            "RelicDefinition",
            _relics.GetDefinitionAsync,
            cancellationToken).ConfigureAwait(false);

        // DATABASE.md §2 item 1: every granted Pet instance carries the
        // documented creation values — Tier Common (the MVP data default),
        // Star = Pet.MinStar (the documented 1-5 floor), XP = Pet.InitialXp (0),
        // Level = Pet.InitialLevel (1), AcquiredAt server-set (DATABASE.md §3;
        // PET_RULES.md §3, §4, §5.2). No Pet is privileged: the grant creates
        // one instance per definition, and the active Pet is a battle-loadout
        // choice, never an ownership property.
        var starterPets = new Pet[petDefinitions.Length];

        for (var index = 0; index < petDefinitions.Length; index++)
        {
            starterPets[index] = new Pet
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

                PetDefinitionId = petDefinitions[index].PetDefinitionId,
                Tier = PetTier.Common,
                Star = Pet.MinStar,
                XP = Pet.InitialXp,
                Level = Pet.InitialLevel,
                AcquiredAt = acquiredAt,
            };
        }

        var starterCards = new PlayerUnlockedCard[cardDefinitions.Length];

        for (var index = 0; index < cardDefinitions.Length; index++)
        {
            // DATABASE.md §1: the unlock row has exactly two members and no
            // third column exists. No acquisition timestamp is recorded: the
            // table has no such column, and ownership is a flag rather than an
            // inventory row (ADR-012 item 9).
            starterCards[index] = new PlayerUnlockedCard
            {
                PlayerId = string.Empty,
                CardDefinitionId = cardDefinitions[index].CardDefinitionId,
            };
        }

        var starterRelics = new Relic[relicDefinitions.Length];

        for (var index = 0; index < relicDefinitions.Length; index++)
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

        return new PlayerStarterGrant(starterPets, starterCards, starterRelics);
    }

    /// <summary>
    /// Resolves one content set's definitions, in the declared identity order,
    /// before any ownership row is built.
    ///
    /// It is the whole-grant verification step:
    /// <c>AGENTS.md</c> §7 and TASK-083 §"Stop Conditions" require a missing
    /// provisioned definition to abort the entire grant rather than produce a
    /// partial set, so the read is deliberately a separate pass over the whole
    /// identity list.
    /// </summary>
    private static async Task<TDefinition[]> ResolveAsync<TDefinition>(
        string[] definitionIds,
        string definitionTable,
        Func<string, CancellationToken, Task<TDefinition?>> read,
        CancellationToken cancellationToken)
        where TDefinition : class
    {
        var resolved = new TDefinition[definitionIds.Length];

        for (var index = 0; index < definitionIds.Length; index++)
        {
            var definitionId = definitionIds[index];

            resolved[index] = await read(definitionId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw MissingDefinition(definitionTable, definitionId);
        }

        return resolved;
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
