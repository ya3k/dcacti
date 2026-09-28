using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Relics;
using GameServer.Domain.Cards;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;

namespace GameServer.Application.Collection;

/// <summary>
/// The collection read behind the four endpoints of <c>API_CONTRACTS.md</c> §5.
///
/// <code>
/// GET /api/pets               → ListPetsAsync
/// GET /api/pets/{petId}       → GetOwnedPetAsync
/// GET /api/cards              → ListCardsAsync
/// GET /api/relics             → ListRelicsAsync
///
/// authenticated PlayerId      (API_CONTRACTS.md §2.8 — the session claim)
///         ↓
/// repository interfaces       (this layer's persistence boundary)
///         ↓
/// owned rows + their definitions
///         ↓
/// §5.1 / §5.3 / §5.4 member sets
/// </code>
///
/// <b>Ownership is established from the authenticated identity, never from the
/// request.</b> §5: "Ownership comes solely from the authenticated session: a
/// caller reads only their own collection, and no request member, query
/// parameter, or header selects a <c>playerId</c>." Every member here therefore
/// takes the caller's identity as an argument supplied by the authentication
/// boundary and reads no request value at all. There is deliberately no overload
/// that could supply an identity by another route.
///
/// <b>It computes no gameplay value.</b> Tier, Star, Level, Category, and Name
/// are read from the stored rows; the Element is a wire projection of the stored
/// Element (<see cref="ElementWireValues"/>). Nothing is recomputed, advanced,
/// or derived from the current time, and no response is written: these are
/// read-only collection queries.
///
/// <b>It sorts nothing.</b> §5.5 defines no ordering and states clients must not
/// rely on any, so no sort, filter, page, limit, cursor, or search is applied —
/// the returned sequence is the ownership read's own.
///
/// <b>It exposes no equip/loadout state.</b> §5.6 excludes equip members from
/// every §5 response; equip is battle-scoped and unpersisted
/// (<c>DATABASE.md</c> §2, ADR-011), so nothing here reads it.
///
/// <b>It fabricates no content.</b> A Pet whose definition row is absent is
/// refused rather than given a placeholder identity or element
/// (<c>AGENTS.md</c> §7): §5.1 defines <c>identity</c> and <c>element</c> as the
/// definition's values, so there is no contract-valid way to report such an
/// instance.
/// </summary>
public sealed class CollectionQueryService
{
    private readonly IPetRepository _pets;
    private readonly ICardRepository _cards;
    private readonly IRelicRepository _relics;

    public CollectionQueryService(
        IPetRepository pets,
        ICardRepository cards,
        IRelicRepository relics)
    {
        _pets = pets ?? throw new ArgumentNullException(nameof(pets));
        _cards = cards ?? throw new ArgumentNullException(nameof(cards));
        _relics = relics ?? throw new ArgumentNullException(nameof(relics));
    }

    /// <summary>
    /// Every Pet the authenticated Player owns, as §5.1 elements
    /// (<c>GET /api/pets</c>).
    ///
    /// An empty collection is an empty list — §5.5: "empty collection → 200 with
    /// <c>[]</c>" — not a null, not an error, and not a 404.
    /// </summary>
    /// <param name="callerPlayerId">
    /// The authenticated caller's Player identity (<c>API_CONTRACTS.md</c> §2.8)
    /// — resolved server-side from the session's <c>player_id</c> claim, never
    /// from request input.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<IReadOnlyList<PetCollectionItem>> ListPetsAsync(
        string callerPlayerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerPlayerId);

        // The ownership read. It is Player-filtered at the query itself, so
        // another Player's instance is never materialized for this caller
        // (GAME_RULES.md §18, ADR-001).
        var owned = await _pets
            .ListByPlayerIdAsync(callerPlayerId, cancellationToken)
            .ConfigureAwait(false);

        if (owned.Count == 0)
        {
            // §5.5: nothing owned is the documented empty array, and the
            // definition read below is unnecessary.
            return [];
        }

        var definitions = await LoadDefinitionsAsync(owned, cancellationToken)
            .ConfigureAwait(false);

        var items = new PetCollectionItem[owned.Count];

        for (var index = 0; index < owned.Count; index++)
        {
            items[index] = Project(owned[index], definitions);
        }

        return items;
    }

    /// <summary>
    /// One Pet the authenticated Player owns, as a §5.2 object
    /// (<c>GET /api/pets/{petId}</c>), or <c>null</c> when the caller cannot
    /// read it.
    ///
    /// <b>A Pet that does not exist and a Pet owned by another Player are one
    /// answer.</b> §5.2 requires the identical response for both "so the endpoint
    /// never discloses whether a Pet exists": that is why this read resolves the
    /// instance first and then compares its owner, rather than issuing a
    /// Player-filtered query that would collapse the two into one absent result
    /// and make a distinguishable status impossible to state. Both paths return
    /// <c>null</c> here, and the endpoint maps that single outcome to the one
    /// documented <c>404 PET_NOT_FOUND</c> — never a <c>403</c>.
    ///
    /// <b>It returns the same object shape as one list element.</b> §5.2: "200 is
    /// the same object as one <c>/api/pets</c> array element (§5.1) — no
    /// wrapper." Both paths project through the same
    /// <see cref="Project(Pet, IReadOnlyDictionary{string, PetDefinition})"/>.
    /// </summary>
    /// <param name="petId">
    /// The owned instance identity from the route (<c>DATABASE.md</c> §1:
    /// <c>PetInstanceId</c>).
    /// </param>
    /// <param name="callerPlayerId">
    /// The authenticated caller's Player identity (<c>API_CONTRACTS.md</c> §2.8),
    /// never from request input.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The owned Pet, or <c>null</c> for both the missing and the foreign case.</returns>
    public async Task<PetCollectionItem?> GetOwnedPetAsync(
        string petId,
        string callerPlayerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(petId);
        ArgumentException.ThrowIfNullOrWhiteSpace(callerPlayerId);

        var pet = await _pets
            .GetByIdAsync(petId, cancellationToken)
            .ConfigureAwait(false);

        // API_CONTRACTS.md §5.2: missing and foreign are indistinguishable. The
        // comparison is ordinal — both sides are opaque server-authored identity
        // strings, not culture-sensitive text. There is one return statement for
        // both cases, so no branch, status, header, or body can distinguish them.
        if (pet is null
            || !string.Equals(pet.PlayerId, callerPlayerId, StringComparison.Ordinal))
        {
            return null;
        }

        var definitions = await LoadDefinitionsAsync([pet], cancellationToken)
            .ConfigureAwait(false);

        return Project(pet, definitions);
    }

    /// <summary>
    /// Every Card the authenticated Player has unlocked, as §5.3 elements
    /// (<c>GET /api/cards</c>).
    ///
    /// <b>Membership is the unlocked set.</b> §5.3: "presence in this array is
    /// the unlocked state". The read is the Player's
    /// <c>PlayerUnlockedCard</c> rows joined to their definitions
    /// (<c>DATABASE.md</c> §1–§2, ADR-012 item 9) — there is no <c>Card</c>
    /// entity and none is introduced.
    ///
    /// An empty collection is an empty list (§5.5).
    /// </summary>
    /// <param name="callerPlayerId">
    /// The authenticated caller's Player identity (<c>API_CONTRACTS.md</c> §2.8),
    /// never from request input.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<IReadOnlyList<CardCollectionItem>> ListCardsAsync(
        string callerPlayerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerPlayerId);

        // The unlock set, Player-filtered at the query itself and joined to the
        // definitions in one read — no per-Card follow-up query.
        var unlocked = await _cards
            .ListUnlockedAsync(callerPlayerId, cancellationToken)
            .ConfigureAwait(false);

        var items = new CardCollectionItem[unlocked.Count];

        for (var index = 0; index < unlocked.Count; index++)
        {
            var definition = unlocked[index];

            items[index] = new CardCollectionItem(
                definition.CardDefinitionId,
                definition.Name,

                // API_CONTRACTS.md §5.3 / CARD_RULES.md §1,
                // DATABASE.md §3: the two-member category set. Its enum member
                // names are the documented wire values, so the name is the
                // contract value — stated once here.
                definition.Category.ToString());
        }

        return items;
    }

    /// <summary>
    /// Every Relic instance the authenticated Player owns, as §5.4 elements
    /// (<c>GET /api/relics</c>).
    ///
    /// <b>The identity is the owned instance.</b> §5.4: <c>relicId</c> is
    /// <c>Relic.RelicInstanceId</c> — the owned copy, not its definition, which
    /// is what §3's <c>relicLoadout</c> submits (<c>RELIC_RULES.md</c> §2.2).
    /// Two owned instances of one definition are two elements.
    ///
    /// An empty collection is an empty list (§5.5).
    /// </summary>
    /// <param name="callerPlayerId">
    /// The authenticated caller's Player identity (<c>API_CONTRACTS.md</c> §2.8),
    /// never from request input.
    /// </param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<IReadOnlyList<RelicCollectionItem>> ListRelicsAsync(
        string callerPlayerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerPlayerId);

        // The ownership read. It is Player-filtered at the query itself, so
        // another Player's instance is never materialized for this caller
        // (GAME_RULES.md §18, ADR-001).
        var owned = await _relics
            .ListByPlayerIdAsync(callerPlayerId, cancellationToken)
            .ConfigureAwait(false);

        if (owned.Count == 0)
        {
            return [];
        }

        // One bulk definition read for the whole collection, so a Player with N
        // relics issues two queries rather than N+1.
        var definitionIds = owned
            .Select(relic => relic.RelicDefinitionId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var definitions = await _relics
            .ListDefinitionsAsync(definitionIds, cancellationToken)
            .ConfigureAwait(false);

        var nameByDefinitionId = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            nameByDefinitionId[definition.RelicDefinitionId] = definition.Name;
        }

        var items = new RelicCollectionItem[owned.Count];

        for (var index = 0; index < owned.Count; index++)
        {
            var relic = owned[index];

            // §5.4's `name` is RelicDefinition.Name, so a definition row that
            // does not resolve leaves no contract-valid name to report. A
            // placeholder would be invented content (AGENTS.md §7), so it is
            // refused instead.
            if (!nameByDefinitionId.TryGetValue(relic.RelicDefinitionId, out var name))
            {
                throw new InvalidOperationException(
                    $"Relic '{relic.RelicInstanceId}' references definition "
                    + $"'{relic.RelicDefinitionId}', which does not exist "
                    + "(DATABASE.md §2: Relic N ── 1 RelicDefinition; API_CONTRACTS.md §5.4).");
            }

            items[index] = new RelicCollectionItem(relic.RelicInstanceId, name);
        }

        return items;
    }

    /// <summary>
    /// Loads the definitions the owned instances reference in one read.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, PetDefinition>> LoadDefinitionsAsync(
        IReadOnlyList<Pet> owned,
        CancellationToken cancellationToken)
    {
        var definitionIds = owned
            .Select(pet => pet.PetDefinitionId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var definitions = await _pets
            .ListDefinitionsAsync(definitionIds, cancellationToken)
            .ConfigureAwait(false);

        var byId = new Dictionary<string, PetDefinition>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            byId[definition.PetDefinitionId] = definition;
        }

        return byId;
    }

    /// <summary>
    /// Projects one owned instance and its definition onto the §5.1/§5.2 member
    /// set — the single projection both the list and the detail endpoint use, so
    /// §5.2's "the same object as one <c>/api/pets</c> array element" cannot
    /// drift into two shapes.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The instance's definition row does not exist. §5.1 defines
    /// <c>identity</c> and <c>element</c> as the definition's values, so there is
    /// no contract-valid projection of such an instance and no placeholder is
    /// substituted for one (<c>AGENTS.md</c> §7).
    /// </exception>
    private static PetCollectionItem Project(
        Pet pet,
        IReadOnlyDictionary<string, PetDefinition> definitions)
    {
        if (!definitions.TryGetValue(pet.PetDefinitionId, out var definition))
        {
            throw new InvalidOperationException(
                $"Pet '{pet.PetInstanceId}' references definition "
                + $"'{pet.PetDefinitionId}', which does not exist "
                + "(DATABASE.md §2: Pet N ── 1 PetDefinition; API_CONTRACTS.md §5.1).");
        }

        return new PetCollectionItem(
            pet.PetInstanceId,
            definition.Identity,

            // API_CONTRACTS.md §5.1: `element` is the definition's Element as its
            // documented wire value. Element.ToString() would emit the enum's
            // Vietnamese-derived member name, which §5.1 does not contain.
            ElementWireValues.ToWireValue(definition.Element),

            // API_CONTRACTS.md §5.1: the documented string member. PetTier's
            // member names are the documented Tier names (PET_RULES.md §3), so
            // the name is the contract value — stated once here.
            pet.Tier.ToString(),

            pet.Star,
            pet.Level);
    }
}
