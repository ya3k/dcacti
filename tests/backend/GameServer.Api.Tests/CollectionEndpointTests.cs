using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameServer.Application.Battle;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Players;
using GameServer.Domain.Relics;
using GameServer.Infrastructure.Postgres;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace GameServer.Api.Tests;

/// <summary>
/// The four collection read endpoints — <c>API_CONTRACTS.md</c> §5.1–§5.6,
/// §2.8, §6 (TASK-071).
///
/// <code>
/// Authorization: Bearer &lt;sessionToken&gt;
///         ↓
/// GET /api/pets | /api/pets/{petId} | /api/cards | /api/relics
///         ↓
/// authenticated PlayerId          (the player_id claim — API_CONTRACTS.md §2.8)
///         ↓
/// owned rows + their definitions  (DATABASE.md §1–§2, §4)
///         ↓
/// 200 raw array / bare object | 404 PET_NOT_FOUND | 401 UNAUTHENTICATED
/// </code>
///
/// <b>What these tests establish.</b> The documented wire contracts byte for
/// byte in membership: the six-member Pet shape with the TASK-072 element wire
/// values, the three-member Card shape with unlock membership as the state, the
/// two-member Relic shape carrying the instance identity, the empty-collection
/// answer on all three list routes, and the §5.2 non-disclosure rule — a foreign
/// <c>petId</c> and a nonexistent one are one response, never a <c>403</c>.
///
/// <b>Every assertion is a negative contract as well as a positive one.</b> The
/// forbidden members (<c>xp</c>, <c>acquiredAt</c>, <c>playerId</c>,
/// <c>petDefinitionId</c>, <c>powerCost</c>, <c>loadoutCopyLimit</c>,
/// <c>effectDefinition</c>, <c>definitionId</c>, <c>trigger</c>,
/// <c>condition</c>, and every §5.6 equip member) are asserted absent by name, so
/// a leak fails here rather than in review.
///
/// <b>The identity is never supplied by the request.</b> These tests present
/// real issued sessions through the production authentication pipeline, so the
/// Player a collection belongs to is the session's own
/// (<c>ADR-015</c> D3) — and the scoping is exercised by two different Players
/// reading the same store.
/// </summary>
public class CollectionEndpointTests
{
    private const string PetsRoute = "/api/pets";
    private const string CardsRoute = "/api/cards";
    private const string RelicsRoute = "/api/relics";

    // -----------------------------------------------------------------------
    // 401 — API_CONTRACTS.md §1, §2.8 "Failure behavior"
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(PetsRoute)]
    [InlineData("/api/pets/some-pet")]
    [InlineData(CardsRoute)]
    [InlineData(RelicsRoute)]
    public async Task EveryCollectionEndpoint_WithoutASession_ShouldReturnUnauthenticated(string route)
    {
        // §1: "All endpoints (except /api/auth/discord) require an authenticated
        // session"; §2.8 gives missing, invalid, tampered, and expired sessions
        // ONE response — 401 with the §6 envelope and the UNAUTHENTICATED code.
        using var factory = new CollectionFactory();

        var response = await factory.GetAsync(factory.CreateClient(), route, session: null);

        await AssertUnauthenticatedAsync(response);
    }

    [Theory]
    [InlineData(PetsRoute)]
    [InlineData("/api/pets/some-pet")]
    [InlineData(CardsRoute)]
    [InlineData(RelicsRoute)]
    public async Task EveryCollectionEndpoint_WithATamperedSession_ShouldReturnUnauthenticated(string route)
    {
        // A token signed with a key outside the configured validation set
        // (ADR-015 D11: exactly the current and previous key are accepted) gets
        // the same single response — no distinct code, no validation detail.
        using var factory = new CollectionFactory();

        var response = await factory.GetAsync(
            factory.CreateClient(),
            route,
            TestApplicationSession.MintWithUnknownKey("player_anyone"));

        await AssertUnauthenticatedAsync(response);
    }

    [Theory]
    [InlineData(PetsRoute)]
    [InlineData(CardsRoute)]
    [InlineData(RelicsRoute)]
    public async Task EveryCollectionEndpoint_WithASessionCarryingNoPlayerIdentity_ShouldReturnUnauthenticated(
        string route)
    {
        // A token that validates but carries no player_id identifies nobody.
        // §5 permits no request-supplied substitute for the identity, and an
        // empty collection would be a disclosure of a Player that does not
        // exist, so the documented unauthenticated response is the answer
        // (PlayerIdClaimRequirement, ADR-001).
        using var factory = new CollectionFactory();

        var response = await factory.GetAsync(
            factory.CreateClient(),
            route,
            TestApplicationSession.Mint(playerId: null));

        await AssertUnauthenticatedAsync(response);
    }

    // -----------------------------------------------------------------------
    // GET /api/pets — API_CONTRACTS.md §5.1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Pets_ForTheOwner_ShouldReturnExactlyTheSixDocumentedMembers()
    {
        // §5.1: the member list is "binding and exhaustive" — petId, identity,
        // element, tier, star, level.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddPetAsync(
            owner,
            "pet_xich_lang",
            Element.Hoa,
            PetTier.Common,
            star: 1,
            level: 12,
            identity: "Xích Lang");

        var response = await factory.GetAsync(
            factory.CreateClient(),
            PetsRoute,
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // §5.5 / §5: the response is a bare JSON array — no envelope member such
        // as `items` or `data`.
        Assert.Equal(JsonValueKind.Array, body.ValueKind);

        var item = Assert.Single(body.EnumerateArray().ToArray());

        Assert.Equal(
            new[] { "element", "identity", "level", "petId", "star", "tier" },
            item.EnumerateObject().Select(member => member.Name).OrderBy(n => n, StringComparer.Ordinal));

        // petId = Pet.PetInstanceId (§5.1).
        Assert.Equal("pet_xich_lang", item.GetProperty("petId").GetString());

        // identity = PetDefinition.Identity (§5.1).
        Assert.Equal("Xích Lang", item.GetProperty("identity").GetString());

        // element = PetDefinition.Element as its documented wire value (§5.1) —
        // "Fire" for the Hoa definition, never "Hoa".
        Assert.Equal("Fire", item.GetProperty("element").GetString());

        // tier = Pet.Tier as the documented STRING (§5.1) — not a number.
        Assert.Equal("Common", item.GetProperty("tier").GetString());

        // star = Pet.Star and level = Pet.Level, both integers (§5.1).
        Assert.Equal(JsonValueKind.Number, item.GetProperty("star").ValueKind);
        Assert.Equal(1, item.GetProperty("star").GetInt32());
        Assert.Equal(12, item.GetProperty("level").GetInt32());
    }

    [Theory]
    [InlineData(Element.Moc, "Wood")]
    [InlineData(Element.Tho, "Earth")]
    [InlineData(Element.Thuy, "Water")]
    [InlineData(Element.Hoa, "Fire")]
    [InlineData(Element.Kim, "Metal")]
    public async Task Pets_ShouldEmitEveryDocumentedElementWireValue(
        Element element,
        string expected)
    {
        // §5.1 / TASK-072: `element` is exactly one of
        // "Fire" | "Water" | "Earth" | "Wood" | "Metal" — the English form, never
        // the enum's Vietnamese-derived member name.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_1", element, PetTier.Common, star: 1, level: 1);

        var body = await factory.GetJsonAsync(
            factory.CreateClient(),
            PetsRoute,
            TestApplicationSession.Mint(owner));

        var item = Assert.Single(body.EnumerateArray().ToArray());

        Assert.Equal(expected, item.GetProperty("element").GetString());
        Assert.Contains(
            item.GetProperty("element").GetString(),
            new[] { "Fire", "Water", "Earth", "Wood", "Metal" });
    }

    [Theory]
    [InlineData(PetTier.Common, "Common")]
    [InlineData(PetTier.Rare, "Rare")]
    [InlineData(PetTier.Epic, "Epic")]
    [InlineData(PetTier.Legendary, "Legendary")]
    [InlineData(PetTier.Mythic, "Mythic")]
    public async Task Pets_ShouldEmitTierAsItsDocumentedStringValue(
        PetTier tier,
        string expected)
    {
        // §5.1: `tier` is a string whose value is one of the five documented
        // Tier names (PET_RULES.md §3) — not the enum ordinal.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_1", Element.Hoa, tier, star: 1, level: 1);

        var body = await factory.GetJsonAsync(
            factory.CreateClient(),
            PetsRoute,
            TestApplicationSession.Mint(owner));

        var item = Assert.Single(body.EnumerateArray().ToArray());

        Assert.Equal(JsonValueKind.String, item.GetProperty("tier").ValueKind);
        Assert.Equal(expected, item.GetProperty("tier").GetString());
    }

    [Fact]
    public async Task Pets_ShouldExposeNoPersistedValueTheContractDoesNotBind()
    {
        // §5.1: "Persisted but NOT exposed: xp, acquiredAt, playerId,
        // petDefinitionId"; §5.6: no equip/loadout member.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddPetAsync(
            owner,
            "pet_1",
            Element.Hoa,
            PetTier.Common,
            star: 2,
            level: 9,
            xp: 800,
            acquiredAt: DateTimeOffset.UnixEpoch);

        var client = factory.CreateClient();

        var response = await factory.GetAsync(client, PetsRoute, TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(body.EnumerateArray().ToArray());

        var members = item.EnumerateObject().Select(member => member.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "xp", "acquiredAt", "playerId", "petDefinitionId",
                     "isEquipped", "equipped", "slot", "loadoutPosition", "active",
                 })
        {
            Assert.DoesNotContain(forbidden, members, StringComparer.OrdinalIgnoreCase);
        }

        // The stored row really does carry the internal values, so this proves
        // the projection omits them rather than that they do not exist.
        var stored = await factory.FindPetAsync("pet_1");

        Assert.NotNull(stored);
        Assert.Equal(800, stored!.XP);
        Assert.Equal(owner, stored.PlayerId);
    }

    [Fact]
    public async Task Pets_ShouldReturnOnlyTheAuthenticatedPlayersOwnPets()
    {
        // §5: "a caller reads only their own collection". Two Players read the
        // same store through their own sessions.
        using var factory = new CollectionFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_mine", Element.Hoa, PetTier.Rare, star: 2, level: 5);
        await factory.AddPetAsync(stranger, "pet_theirs", Element.Kim, PetTier.Epic, star: 1, level: 3);

        var client = factory.CreateClient();

        var mine = await factory.GetJsonAsync(client, PetsRoute, TestApplicationSession.Mint(owner));
        var theirs = await factory.GetJsonAsync(client, PetsRoute, TestApplicationSession.Mint(stranger));

        Assert.Equal("pet_mine", Assert.Single(mine.EnumerateArray().ToArray()).GetProperty("petId").GetString());
        Assert.Equal("pet_theirs", Assert.Single(theirs.EnumerateArray().ToArray()).GetProperty("petId").GetString());
    }

    [Fact]
    public async Task Pets_ShouldIgnoreAClientSuppliedPlayerId()
    {
        // §5: "no request member, query parameter, or header selects a playerId".
        // A stranger who names the owner receives the stranger's own collection —
        // here, the empty one.
        using var factory = new CollectionFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_mine", Element.Hoa, PetTier.Common, star: 1, level: 1);

        var client = factory.CreateClient();

        var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"{PetsRoute}?playerId={owner}&petId=pet_mine");

        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestApplicationSession.Mint(stranger));

        // Every request-supplied spelling of an identity that §5 forbids.
        message.Headers.Add("playerId", owner);
        message.Headers.Add("X-Player-Id", owner);

        var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Not the owner's Pet: the collection is the stranger's, and it is empty.
        Assert.Empty(body.EnumerateArray().ToArray());
    }

    [Fact]
    public async Task Pets_ForAPlayerOwningNothing_ShouldReturnOkWithAnEmptyArray()
    {
        // §5.5: "empty collection → 200 with []" — not 204, not 404, not a
        // wrapper.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        var response = await factory.GetAsync(
            factory.CreateClient(),
            PetsRoute,
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Empty(body.EnumerateArray().ToArray());
    }

    [Fact]
    public async Task Pets_ShouldReturnEveryOwnedInstanceWithNoPaginationMember()
    {
        // §5.5: "pagination — none in MVP — no page/limit/cursor/sort/filter/
        // search parameters; the array is the full collection". Five Pets are all
        // returned, and the response is the array itself.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        for (var index = 0; index < 5; index++)
        {
            await factory.AddPetAsync(owner, $"pet_{index}", Element.Hoa, PetTier.Common, star: 1, level: 1);
        }

        var body = await factory.GetJsonAsync(
            factory.CreateClient(),
            PetsRoute,
            TestApplicationSession.Mint(owner));

        Assert.Equal(5, body.EnumerateArray().Count());

        // No envelope member of any kind: the top level is an array.
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
    }

    // -----------------------------------------------------------------------
    // GET /api/pets/{petId} — API_CONTRACTS.md §5.2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PetDetail_ForAnOwnedPet_ShouldReturnTheSameBareObjectAsAListElement()
    {
        // §5.2: "200 is the same object as one /api/pets array element (§5.1) —
        // no wrapper."
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_1", Element.Thuy, PetTier.Legendary, star: 4, level: 33);

        var client = factory.CreateClient();
        var session = TestApplicationSession.Mint(owner);

        var list = await factory.GetJsonAsync(client, PetsRoute, session);
        var listed = Assert.Single(list.EnumerateArray().ToArray());

        var detailResponse = await factory.GetAsync(client, "/api/pets/pet_1", session);

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();

        // A bare object at the top level — no wrapper.
        Assert.Equal(JsonValueKind.Object, detail.ValueKind);

        Assert.Equal(
            listed.EnumerateObject().Select(member => member.Name).OrderBy(n => n, StringComparer.Ordinal),
            detail.EnumerateObject().Select(member => member.Name).OrderBy(n => n, StringComparer.Ordinal));

        Assert.Equal(listed.GetRawText(), detail.GetRawText());
    }

    [Fact]
    public async Task PetDetail_ForAMissingPet_ShouldReturnPetNotFound()
    {
        // §5.2: a petId that does not exist returns the §6 envelope with the
        // PET_NOT_FOUND code.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        var response = await factory.GetAsync(
            factory.CreateClient(),
            "/api/pets/pet-that-does-not-exist",
            TestApplicationSession.Mint(owner));

        await AssertPetNotFoundAsync(response);
    }

    [Fact]
    public async Task PetDetail_ForAForeignPet_ShouldReturnTheIdenticalResponseAsAMissingOne()
    {
        // §5.2: "A petId that does not exist and a petId owned by another Player
        // return the identical response, so the endpoint never discloses whether
        // a Pet exists." Both the status and the body are compared, so a
        // distinguishable body would fail here too.
        using var factory = new CollectionFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_owners", Element.Hoa, PetTier.Mythic, star: 5, level: 50);

        var client = factory.CreateClient();
        var strangerSession = TestApplicationSession.Mint(stranger);

        var foreign = await factory.GetAsync(client, "/api/pets/pet_owners", strangerSession);
        var missing = await factory.GetAsync(client, "/api/pets/pet-absent", strangerSession);

        await AssertPetNotFoundAsync(foreign);
        await AssertPetNotFoundAsync(missing);

        Assert.Equal(missing.StatusCode, foreign.StatusCode);

        Assert.Equal(
            await missing.Content.ReadAsStringAsync(),
            await foreign.Content.ReadAsStringAsync());

        // The Pet really does exist and is readable by its own owner, so this
        // proves the ownership comparison rather than an absent row.
        var asOwner = await factory.GetAsync(
            client,
            "/api/pets/pet_owners",
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
    }

    [Fact]
    public async Task PetDetail_ForAForeignPet_ShouldNeverReturnForbidden()
    {
        // §5.2 requires the 404 for the foreign case: a 403 would distinguish
        // "exists but not yours" from "does not exist", which is exactly the
        // disclosure the section forbids.
        using var factory = new CollectionFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_owners", Element.Hoa, PetTier.Common, star: 1, level: 1);

        var response = await factory.GetAsync(
            factory.CreateClient(),
            "/api/pets/pet_owners",
            TestApplicationSession.Mint(stranger));

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        // No ownership information is disclosed: the body names only the code and
        // a human-readable detail. It must not name the owner, the definition, or
        // any evidence that the row exists.
        var serialized = body.GetRawText();

        Assert.DoesNotContain(owner, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("pet_def", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("owner", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("forbidden", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PetDetail_ShouldIgnoreAClientSuppliedPlayerId()
    {
        // §5's identity rule applies to the detail route too: no query
        // parameter, header, or body field may select whose Pet is read.
        using var factory = new CollectionFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_owners", Element.Hoa, PetTier.Common, star: 1, level: 1);

        var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/pets/pet_owners?playerId={owner}");

        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            TestApplicationSession.Mint(stranger));
        message.Headers.Add("playerId", owner);
        message.Headers.Add("X-Player-Id", owner);

        var response = await factory.CreateClient().SendAsync(message);

        await AssertPetNotFoundAsync(response);
    }

    // -----------------------------------------------------------------------
    // GET /api/cards — API_CONTRACTS.md §5.3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Cards_ForTheOwner_ShouldReturnExactlyTheThreeDocumentedMembers()
    {
        // §5.3: cardId, name, category.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddUnlockedCardAsync(owner, "card_heal", "Heal", CardCategory.Basic);

        var body = await factory.GetJsonAsync(
            factory.CreateClient(),
            CardsRoute,
            TestApplicationSession.Mint(owner));

        var item = Assert.Single(body.EnumerateArray().ToArray());

        Assert.Equal(
            new[] { "cardId", "category", "name" },
            item.EnumerateObject().Select(member => member.Name).OrderBy(n => n, StringComparer.Ordinal));

        // cardId = CardDefinition.CardDefinitionId (§5.3).
        Assert.Equal("card_heal", item.GetProperty("cardId").GetString());

        // name = CardDefinition.Name (§5.3).
        Assert.Equal("Heal", item.GetProperty("name").GetString());

        // category = CardDefinition.Category — "Basic" | "PetSkill" (§5.3).
        Assert.Equal("Basic", item.GetProperty("category").GetString());
    }

    [Theory]
    [InlineData(CardCategory.Basic, "Basic")]
    [InlineData(CardCategory.PetSkill, "PetSkill")]
    public async Task Cards_ShouldEmitBothDocumentedCategoryValues(
        CardCategory category,
        string expected)
    {
        // §5.3 / CARD_RULES.md §1 / DATABASE.md §3: the closed two-member set.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddUnlockedCardAsync(owner, "card_1", "Card", category);

        var body = await factory.GetJsonAsync(
            factory.CreateClient(),
            CardsRoute,
            TestApplicationSession.Mint(owner));

        var item = Assert.Single(body.EnumerateArray().ToArray());

        Assert.Equal(expected, item.GetProperty("category").GetString());
        Assert.Contains(
            item.GetProperty("category").GetString(),
            new[] { "Basic", "PetSkill" });
    }

    [Fact]
    public async Task Cards_ShouldReturnExactlyTheUnlockedDefinitions()
    {
        // §5.3: "presence in this array IS the unlocked state" — the membership
        // is the Player's PlayerUnlockedCard rows joined to their definitions,
        // and an unlocked-by-nobody definition is not listed.
        using var factory = new CollectionFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        await factory.AddUnlockedCardAsync(owner, "card_heal", "Heal", CardCategory.Basic);
        await factory.AddUnlockedCardAsync(owner, "card_shield", "Shield", CardCategory.Basic);
        await factory.AddUnlockedCardAsync(owner, "card_skill", "Inferno", CardCategory.PetSkill);
        await factory.AddUnlockedCardAsync(stranger, "card_theirs", "Theirs", CardCategory.Basic);

        // Content that exists but is unlocked by nobody.
        await factory.AddCardDefinitionAsync("card_unowned", "Unowned", CardCategory.Basic);

        var body = await factory.GetJsonAsync(
            factory.CreateClient(),
            CardsRoute,
            TestApplicationSession.Mint(owner));

        Assert.Equal(
            new[] { "card_heal", "card_shield", "card_skill" },
            body.EnumerateArray()
                .Select(item => item.GetProperty("cardId").GetString()!)
                .OrderBy(id => id, StringComparer.Ordinal));

        // And the unowned definition really exists as content.
        Assert.True(await factory.CardDefinitionExistsAsync("card_unowned"));
    }

    [Fact]
    public async Task Cards_ShouldCarryNoUnlockedMember()
    {
        // §5.3: "there is no `unlocked` member" — the array's membership is the
        // state. A boolean would be a second representation of the same fact.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddUnlockedCardAsync(owner, "card_heal", "Heal", CardCategory.Basic);

        var response = await factory.GetAsync(
            factory.CreateClient(),
            CardsRoute,
            TestApplicationSession.Mint(owner));

        var serialized = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("unlocked", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cards_ShouldExposeNoExcludedColumn()
    {
        // §5.3: "playerId, powerCost, loadoutCopyLimit, effectDefinition" are
        // persisted or definition data but NOT exposed; §5.6 adds the equip
        // members.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddUnlockedCardAsync(
            owner,
            "card_heal",
            "Heal",
            CardCategory.Basic,
            powerCost: 25,
            loadoutCopyLimit: 3,
            effectDefinition: TestCardEffects.PercentMaxHp);

        var body = await factory.GetJsonAsync(
            factory.CreateClient(),
            CardsRoute,
            TestApplicationSession.Mint(owner));

        var item = Assert.Single(body.EnumerateArray().ToArray());
        var members = item.EnumerateObject().Select(member => member.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "playerId", "powerCost", "loadoutCopyLimit", "effectDefinition",
                     "unlocked", "isEquipped", "equipped", "slot", "loadoutPosition", "active",
                 })
        {
            Assert.DoesNotContain(forbidden, members, StringComparer.OrdinalIgnoreCase);
        }

        // The stored definition really does carry the excluded values.
        var stored = await factory.FindCardDefinitionAsync("card_heal");

        Assert.NotNull(stored);
        Assert.Equal(25, stored!.PowerCost);
        Assert.Equal(3, stored.LoadoutCopyLimit);
        Assert.Equal(TestCardEffects.PercentMaxHp, stored.EffectDefinition);
    }

    [Fact]
    public async Task Cards_ForAPlayerWithNoUnlocks_ShouldReturnOkWithAnEmptyArray()
    {
        // §5.5: empty → 200 [].
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        var response = await factory.GetAsync(
            factory.CreateClient(),
            CardsRoute,
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Empty(body.EnumerateArray().ToArray());
    }

    // -----------------------------------------------------------------------
    // GET /api/relics — API_CONTRACTS.md §5.4
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Relics_ForTheOwner_ShouldReturnExactlyTheTwoDocumentedMembers()
    {
        // §5.4: relicId, name.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddRelicAsync(owner, "relic_instance_berserker", "Berserker Core");

        var body = await factory.GetJsonAsync(
            factory.CreateClient(),
            RelicsRoute,
            TestApplicationSession.Mint(owner));

        var item = Assert.Single(body.EnumerateArray().ToArray());

        Assert.Equal(
            new[] { "name", "relicId" },
            item.EnumerateObject().Select(member => member.Name).OrderBy(n => n, StringComparer.Ordinal));

        // relicId = Relic.RelicInstanceId (§5.4) — the owned INSTANCE, never its
        // definition identity.
        Assert.Equal("relic_instance_berserker", item.GetProperty("relicId").GetString());

        // name = RelicDefinition.Name (§5.4).
        Assert.Equal("Berserker Core", item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Relics_ShouldReturnBothInstancesOfOneDefinition()
    {
        // RELIC_RULES.md §2.4 item 3 / §2.2: ownership is per instance, so two
        // owned copies of one definition are two elements with distinct relicIds
        // and the same name.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddRelicAsync(owner, "relic_a", "Berserker Core");
        await factory.AddRelicAsync(owner, "relic_b", "Berserker Core");

        var body = await factory.GetJsonAsync(
            factory.CreateClient(),
            RelicsRoute,
            TestApplicationSession.Mint(owner));

        var items = body.EnumerateArray().ToArray();

        Assert.Equal(2, items.Length);
        Assert.Equal(
            new[] { "relic_a", "relic_b" },
            items.Select(item => item.GetProperty("relicId").GetString()!)
                .OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(items, item => Assert.Equal("Berserker Core", item.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Relics_ShouldReturnOnlyTheAuthenticatedPlayersOwnInstances()
    {
        // §5.4: results are the Player's owned instances (DATABASE.md §2:
        // Player 1 ── N Relic).
        using var factory = new CollectionFactory();

        var owner = await factory.NewPlayerAsync();
        var stranger = await factory.NewPlayerAsync();

        await factory.AddRelicAsync(owner, "relic_mine", "Berserker Core");
        await factory.AddRelicAsync(stranger, "relic_theirs", "Mana Crystal");

        var client = factory.CreateClient();

        var mine = await factory.GetJsonAsync(client, RelicsRoute, TestApplicationSession.Mint(owner));
        var theirs = await factory.GetJsonAsync(client, RelicsRoute, TestApplicationSession.Mint(stranger));

        Assert.Equal(
            "relic_mine",
            Assert.Single(mine.EnumerateArray().ToArray()).GetProperty("relicId").GetString());
        Assert.Equal(
            "relic_theirs",
            Assert.Single(theirs.EnumerateArray().ToArray()).GetProperty("relicId").GetString());
    }

    [Fact]
    public async Task Relics_ShouldExposeNoExcludedMember()
    {
        // §5.4: "Not exposed: playerId, acquiredAt, definitionId, and
        // Trigger/Condition/EffectDefinition"; §5.6 adds the equip members.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddRelicAsync(
            owner,
            "relic_1",
            "Berserker Core",
            trigger: "OnCombo3Plus",
            condition: "Combo ≥ 3",
            effectDefinition: "berserker_effect");

        var response = await factory.GetAsync(
            factory.CreateClient(),
            RelicsRoute,
            TestApplicationSession.Mint(owner));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(body.EnumerateArray().ToArray());
        var members = item.EnumerateObject().Select(member => member.Name).ToArray();

        foreach (var forbidden in new[]
                 {
                     "playerId", "acquiredAt", "definitionId", "relicDefinitionId",
                     "trigger", "condition", "effect", "effectDefinition",
                     "isEquipped", "equipped", "slot", "loadoutPosition", "active",
                 })
        {
            Assert.DoesNotContain(forbidden, members, StringComparer.OrdinalIgnoreCase);
        }

        var serialized = body.GetRawText();

        // The rule texts themselves must not leak either, at any depth.
        Assert.DoesNotContain("OnCombo3Plus", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("berserker_effect", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(owner, serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Relics_ForAPlayerOwningNothing_ShouldReturnOkWithAnEmptyArray()
    {
        // §5.5: empty → 200 [].
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        var response = await factory.GetAsync(
            factory.CreateClient(),
            RelicsRoute,
            TestApplicationSession.Mint(owner));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Empty(body.EnumerateArray().ToArray());
    }

    // -----------------------------------------------------------------------
    // §5.5 / §5.6 — semantics shared by all four routes
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NoCollectionResponse_ShouldCarryAnyEquipOrLoadoutMember()
    {
        // §5.6: "No §5 response carries isEquipped, equipped, slot,
        // loadoutPosition, or active members: equip state is battle-scoped and
        // unpersisted (DATABASE.md §2, ADR-011)."
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_1", Element.Hoa, PetTier.Common, star: 1, level: 1);
        await factory.AddUnlockedCardAsync(owner, "card_heal", "Heal", CardCategory.Basic);
        await factory.AddRelicAsync(owner, "relic_1", "Berserker Core");

        var client = factory.CreateClient();
        var session = TestApplicationSession.Mint(owner);

        foreach (var route in new[] { PetsRoute, "/api/pets/pet_1", CardsRoute, RelicsRoute })
        {
            var response = await factory.GetAsync(client, route, session);
            var serialized = await response.Content.ReadAsStringAsync();

            foreach (var forbidden in new[]
                     {
                         "isEquipped", "equipped", "\"slot\"", "loadoutPosition", "\"active\"",
                         "loadout",
                     })
            {
                Assert.DoesNotContain(forbidden, serialized, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task NoCollectionResponse_ShouldCarryAnEnvelopeOrPaginationMember()
    {
        // §5.5 / §5: the response is the bare array (or the bare object, for
        // §5.2) — no wrapper, no pagination, no ordering, no totals.
        using var factory = new CollectionFactory();
        var owner = await factory.NewPlayerAsync();

        await factory.AddPetAsync(owner, "pet_1", Element.Hoa, PetTier.Common, star: 1, level: 1);
        await factory.AddUnlockedCardAsync(owner, "card_heal", "Heal", CardCategory.Basic);
        await factory.AddRelicAsync(owner, "relic_1", "Berserker Core");

        var client = factory.CreateClient();
        var session = TestApplicationSession.Mint(owner);

        foreach (var route in new[] { PetsRoute, CardsRoute, RelicsRoute })
        {
            var body = await factory.GetJsonAsync(client, route, session);

            // The top level is the array itself.
            Assert.Equal(JsonValueKind.Array, body.ValueKind);

            foreach (var item in body.EnumerateArray())
            {
                var members = item.EnumerateObject().Select(member => member.Name).ToArray();

                foreach (var forbidden in new[]
                         {
                             "page", "limit", "cursor", "sort", "filter", "search",
                             "total", "items", "data", "next", "offset",
                         })
                {
                    Assert.DoesNotContain(forbidden, members, StringComparer.OrdinalIgnoreCase);
                }
            }
        }
    }

    // -----------------------------------------------------------------------
    // Assertions
    // -----------------------------------------------------------------------

    /// <summary>
    /// Asserts the one documented unauthenticated response
    /// (<c>API_CONTRACTS.md</c> §2.8 "Failure behavior", §6): <c>401</c> with the
    /// <c>UNAUTHENTICATED</c> code, and nothing that distinguishes one validation
    /// failure from another.
    /// </summary>
    private static async Task AssertUnauthenticatedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("UNAUTHENTICATED", body.GetProperty("error").GetString());

        var raw = body.GetRawText();

        foreach (var disclosure in new[]
                 {
                     "signature", "expired", "audience", "issuer", "kid", "alg", "claim",
                 })
        {
            Assert.DoesNotContain(disclosure, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Asserts the one documented not-found response (<c>API_CONTRACTS.md</c>
    /// §5.2, §6): <c>404</c> with the <c>PET_NOT_FOUND</c> code in the §6
    /// envelope <c>{ "error", "message" }</c>.
    /// </summary>
    private static async Task AssertPetNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("PET_NOT_FOUND", body.GetProperty("error").GetString());

        // The §6 envelope: the machine-readable code plus a human-readable
        // detail. No other member exists — no identifier, no owner, no
        // existence evidence.
        Assert.Equal(
            new[] { "error", "message" },
            body.EnumerateObject().Select(member => member.Name).OrderBy(n => n, StringComparer.Ordinal));

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    // -----------------------------------------------------------------------
    // Test host
    // -----------------------------------------------------------------------

    /// <summary>
    /// A host exposing the four real endpoints, the real persistence
    /// composition, and the real application-session pipeline over an isolated
    /// in-memory store — so the whole documented read path is exercised without
    /// a live PostgreSQL.
    ///
    /// Redis is not needed: these are collection reads, and the active-state
    /// store plays no part in them.
    /// </summary>
    private sealed class CollectionFactory : IDisposable
    {
        private readonly CollectionHost _host = new();

        public HttpClient CreateClient() => _host.CreateClient();

        public IServiceProvider Services => _host.Services;

        /// <summary>
        /// Creates a Player row and returns the Player's identity.
        ///
        /// Pet, Card, and Relic definitions are added by the helpers below, one
        /// per row that needs one, so a test states exactly the content it is
        /// asserting on (<c>DATABASE.md</c> §1–§2).
        /// </summary>
        public async Task<string> NewPlayerAsync()
        {
            var playerId = $"player_collection_{Guid.NewGuid():N}";

            await MutateAsync(context => context.Players.Add(new Player
            {
                PlayerId = playerId,
                DiscordUserId = $"9{Random.Shared.NextInt64(1_000_000_000_000_000L):D16}",
                Level = Player.InitialLevel,
                CreatedAt = DateTimeOffset.UtcNow,
            }));

            return playerId;
        }

        /// <summary>
        /// Adds an owned Pet instance and the definition it references, so the
        /// documented <c>identity</c> and <c>element</c> projections can be
        /// exercised value by value.
        /// </summary>
        public Task AddPetAsync(
            string playerId,
            string petInstanceId,
            Element element,
            PetTier tier,
            int star,
            int level,
            string? identity = null,
            int xp = 0,
            DateTimeOffset? acquiredAt = null)
        {
            var petDefinitionId = $"pet_def_{petInstanceId}";

            return MutateAsync(context =>
            {
                context.PetDefinitions.Add(new PetDefinition
                {
                    PetDefinitionId = petDefinitionId,
                    Identity = identity ?? $"Pet {element}",
                    Element = element,
                    PassiveId = new PassiveId("test-passive"),
                    PassiveThreshold = 5,
                    SignatureSkillCardId = $"card_skill_{petInstanceId}",
                });

                context.Pets.Add(new Pet
                {
                    PetInstanceId = petInstanceId,
                    PlayerId = playerId,
                    PetDefinitionId = petDefinitionId,
                    Tier = tier,
                    Star = star,
                    XP = xp,
                    Level = level,
                    AcquiredAt = acquiredAt ?? DateTimeOffset.UtcNow,
                });
            });
        }

        public Task AddCardDefinitionAsync(
            string cardDefinitionId,
            string name,
            CardCategory category,
            int powerCost = 0,
            int loadoutCopyLimit = 1,
            CardEffectDefinitions? effectDefinition = null) =>
            MutateAsync(context => context.CardDefinitions.Add(new CardDefinition
            {
                CardDefinitionId = cardDefinitionId,
                Name = name,
                Category = category,
                PowerCost = powerCost,
                EffectDefinition = effectDefinition ?? TestCardEffects.FlatPower,
                LoadoutCopyLimit = loadoutCopyLimit,
            }));

        /// <summary>Adds a definition and unlocks it for the Player (§5.3).</summary>
        public async Task AddUnlockedCardAsync(
            string playerId,
            string cardDefinitionId,
            string name,
            CardCategory category,
            int powerCost = 0,
            int loadoutCopyLimit = 1,
            CardEffectDefinitions? effectDefinition = null)
        {
            await AddCardDefinitionAsync(
                cardDefinitionId,
                name,
                category,
                powerCost,
                loadoutCopyLimit,
                effectDefinition);

            await MutateAsync(context => context.PlayerUnlockedCards.Add(new PlayerUnlockedCard
            {
                PlayerId = playerId,
                CardDefinitionId = cardDefinitionId,
            }));
        }

        /// <summary>
        /// Adds an owned Relic instance and the definition it references
        /// (<c>DATABASE.md</c> §2: Relic N ── 1 RelicDefinition). Several
        /// instances may share one definition (<c>RELIC_RULES.md</c> §2.4 item 3),
        /// so an existing definition row is reused rather than replaced.
        /// </summary>
        public Task AddRelicAsync(
            string playerId,
            string relicInstanceId,
            string name,
            string trigger = "OnTurnEnd",
            string? condition = null,
            string effectDefinition = "effect")
        {
            var relicDefinitionId = $"relic_def_{name.Replace(' ', '_')}";

            return MutateAsync(context =>
            {
                if (!context.RelicDefinitions.Any(definition =>
                        definition.RelicDefinitionId == relicDefinitionId))
                {
                    context.RelicDefinitions.Add(new RelicDefinition
                    {
                        RelicDefinitionId = relicDefinitionId,
                        Name = name,
                        Trigger = trigger,
                        Condition = condition,
                        EffectDefinition = "increase ATK by 5%",
                    });
                }

                context.Relics.Add(new Relic
                {
                    RelicInstanceId = relicInstanceId,
                    PlayerId = playerId,
                    RelicDefinitionId = relicDefinitionId,
                    AcquiredAt = DateTimeOffset.UtcNow,
                });
            });
        }

        /// <summary>The stored Pet row, so a test can prove a value was omitted rather than absent.</summary>
        public Task<Pet?> FindPetAsync(string petInstanceId) => ReadAsync(context =>
            context.Pets.FirstOrDefaultAsync(pet => pet.PetInstanceId == petInstanceId));

        /// <summary>The stored Card definition row.</summary>
        public Task<CardDefinition?> FindCardDefinitionAsync(string cardDefinitionId) =>
            ReadAsync(context => context.CardDefinitions.FirstOrDefaultAsync(
                definition => definition.CardDefinitionId == cardDefinitionId));

        public Task<bool> CardDefinitionExistsAsync(string cardDefinitionId) =>
            ReadAsync(context => context.CardDefinitions.AnyAsync(
                definition => definition.CardDefinitionId == cardDefinitionId));

        public Task<HttpResponseMessage> GetAsync(HttpClient client, string route, string? session)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, route);

            if (session is not null)
            {
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session);
            }

            return client.SendAsync(message);
        }

        public async Task<JsonElement> GetJsonAsync(HttpClient client, string route, string session)
        {
            var response = await GetAsync(client, route, session);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            return await response.Content.ReadFromJsonAsync<JsonElement>();
        }

        public void Dispose() => _host.Dispose();

        private async Task MutateAsync(Action<GameDbContext> mutate)
        {
            using var scope = Services.CreateScope();

            mutate(scope.ServiceProvider.GetRequiredService<GameDbContext>());

            await scope.ServiceProvider.GetRequiredService<GameDbContext>().SaveChangesAsync();
        }

        private async Task<T> ReadAsync<T>(Func<GameDbContext, Task<T>> read)
        {
            using var scope = Services.CreateScope();

            return await read(scope.ServiceProvider.GetRequiredService<GameDbContext>());
        }

        /// <summary>
        /// The production pipeline over an isolated in-memory store, with the
        /// application session's signing key supplied through configuration as
        /// <c>ADR-015</c> D10 requires.
        /// </summary>
        private sealed class CollectionHost : WebApplicationFactory<Program>
        {
            private readonly string _storeName = $"collection-{Guid.NewGuid():N}";

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                // Blank both connection strings so the production composition
                // registers neither the Npgsql provider nor the Redis store; this
                // host supplies the isolated in-memory context instead — the
                // established API test-host pattern.
                builder.UseSetting("ConnectionStrings:DefaultConnection", "");
                builder.UseSetting("ConnectionStrings:Redis", "");

                foreach (var (key, value) in TestApplicationSession.RotatedKeyConfiguration)
                {
                    builder.UseSetting(key, value);
                }

                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<GameDbContext>>();
                    services.RemoveAll<GameDbContext>();
                    services.AddDbContext<GameDbContext>(options =>
                        options.UseInMemoryDatabase(_storeName));

                    // Blanking the Redis connection string means the production
                    // composition registers no IBattleStateRepository at all, and
                    // the Application layer validates its descriptors on build —
                    // so the isolated in-memory stand-in the other API test hosts
                    // use is substituted here too. These tests never touch active
                    // battle state: REDIS_STATE.md §1–§4 plays no part in a
                    // collection read.
                    services.AddSingleton<IBattleStateRepository, ApiTestBattleStateRepository>();
                });
            }
        }
    }
}
