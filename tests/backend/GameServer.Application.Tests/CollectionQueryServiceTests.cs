using GameServer.Application.Cards;
using GameServer.Application.Collection;
using GameServer.Application.Pets;
using GameServer.Application.Relics;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;

namespace GameServer.Application.Tests;

/// <summary>
/// The collection read behind <c>API_CONTRACTS.md</c> §5.1–§5.6 — the
/// Application boundary of TASK-071.
///
/// <code>
/// authenticated PlayerId   (§2.3 — the session claim)
///         ↓
/// CollectionQueryService
///         ↓
/// Pet / Card / Relic ownership rows + their definitions
///         ↓
/// §5.1 / §5.3 / §5.4 member sets
/// </code>
///
/// <b>What these tests establish.</b> The four reads' documented membership,
/// ownership scoping, the indistinguishable missing-or-foreign Pet detail, the
/// §5.5 empty-collection and no-pagination semantics, and the §5.6 absence of
/// every equip/loadout member. They assert the projected member set rather than
/// "the call returned something": a forbidden field is asserted absent by name,
/// because the wire DTOs are what make that structural and the DTO shape is
/// asserted separately at the Api layer.
///
/// <b>Nothing here reads a client-supplied identity.</b> Every entry point takes
/// the caller's PlayerId as an argument, which is the shape that makes §5's
/// "ownership comes solely from the authenticated session" checkable.
/// </summary>
public class CollectionQueryServiceTests
{
    private const string Owner = "player_1";
    private const string OtherPlayer = "player_2";

    private static CollectionQueryService CreateService(
        CollectionFixture fixture) =>
        new(
            new FakePetRepository(fixture),
            new FakeCardRepository(fixture),
            new FakeRelicRepository(fixture));

    // -----------------------------------------------------------------------
    // GET /api/pets — API_CONTRACTS.md §5.1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListPets_ShouldReturnOnlyTheCallersOwnPets()
    {
        // §5: "a caller reads only their own collection". The fixture holds one
        // Pet for the caller and one for another Player.
        var fixture = new CollectionFixture()
            .WithPet("pet_mine", Owner, "def_mine", Element.Hoa, PetTier.Rare, star: 3, level: 12)
            .WithPet("pet_theirs", OtherPlayer, "def_theirs", Element.Kim, PetTier.Epic, star: 2, level: 7);

        var pets = await CreateService(fixture).ListPetsAsync(Owner);

        var pet = Assert.Single(pets);

        Assert.Equal("pet_mine", pet.PetId);

        // And the other Player's view is their own Pet, so the filter is a real
        // Player scope rather than a fixed answer.
        var theirs = await CreateService(fixture).ListPetsAsync(OtherPlayer);

        Assert.Equal("pet_theirs", Assert.Single(theirs).PetId);
    }

    [Fact]
    public async Task ListPets_ShouldProjectExactlyTheSevenDocumentedMembers()
    {
        // §5.1: the member list is "binding and exhaustive" —
        // petId, identity, element, tier, star, level, signatureSkill.
        var fixture = new CollectionFixture()
            .WithPet(
                "pet_xich_lang",
                Owner,
                "def_xich_lang",
                Element.Hoa,
                PetTier.Common,
                star: 1,
                level: 12,
                identity: "Xích Lang",
                signatureSkillCardId: "card-inferno",
                signatureSkillName: "Inferno");

        var pet = Assert.Single(await CreateService(fixture).ListPetsAsync(Owner));

        // The projected record declares exactly these seven members, so an eighth
        // cannot be added without failing here.
        var members = typeof(PetCollectionItem)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "Element", "Identity", "Level", "PetId", "SignatureSkill", "Star", "Tier" },
            members);

        // petId = Pet.PetInstanceId (§5.1).
        Assert.Equal("pet_xich_lang", pet.PetId);

        // identity = PetDefinition.Identity (§5.1).
        Assert.Equal("Xích Lang", pet.Identity);

        // element = PetDefinition.Element as its wire value (§5.1).
        Assert.Equal("Fire", pet.Element);

        // tier = Pet.Tier, as the documented STRING value (§5.1) — not a number.
        Assert.IsType<string>(pet.Tier);
        Assert.Equal("Common", pet.Tier);

        // star = Pet.Star and level = Pet.Level, both integers (§5.1).
        Assert.Equal(1, pet.Star);
        Assert.Equal(12, pet.Level);
    }

    [Fact]
    public async Task ListPets_ShouldProjectThePetsOwnDerivedSignatureSkillReference()
    {
        // §5.1: `signatureSkill` is the Pet's DERIVED Signature Skill reference —
        // PetDefinition.SignatureSkillCardId, resolved through the CardDefinition
        // it names (CARD_RULES.md §4 item 1). The cardId is the FK verbatim and
        // the name and category are that definition's own stored values.
        var fixture = new CollectionFixture()
            .WithPet(
                "pet_xich_lang",
                Owner,
                "def_xich_lang",
                Element.Hoa,
                PetTier.Common,
                star: 1,
                level: 12,
                signatureSkillCardId: "card-inferno",
                signatureSkillName: "Inferno");

        var pet = Assert.Single(await CreateService(fixture).ListPetsAsync(Owner));

        Assert.NotNull(pet.SignatureSkill);

        // §5.1: exactly these three members, so a fourth cannot slip in.
        var members = typeof(PetSignatureSkillItem)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "CardId", "Category", "Name" }, members);

        Assert.Equal("card-inferno", pet.SignatureSkill.CardId);
        Assert.Equal("Inferno", pet.SignatureSkill.Name);
        Assert.Equal("PetSkill", pet.SignatureSkill.Category);

        // The value is the definition's, not this fixture's guess: the stored
        // row carries exactly those members.
        Assert.Equal(
            pet.SignatureSkill.Name,
            fixture.Definitions["card-inferno"].Name);
        Assert.Equal(
            pet.SignatureSkill.CardId,
            fixture.Definitions["card-inferno"].CardDefinitionId);
    }

    [Fact]
    public async Task ListPets_ShouldResolveEveryProvisionedPetsSignatureSkill()
    {
        // §5.1 + PET_RULES.md §8: all five provisioned MVP Pets derive a
        // Signature Skill, and each row's reference is its own PetDefinition's
        // SignatureSkillCardId (CARD_RULES.md §4.1). The mapping is data, so the
        // read must resolve all five — not one privileged Pet.
        var provisioned = new[]
        {
            (PetDefinitionId: "pet-xich-lang", CardId: "card-inferno", Name: "Inferno"),
            (PetDefinitionId: "pet-bach-ho", CardId: "card-iron-fang", Name: "Iron Fang"),
            (PetDefinitionId: "pet-huyen-quy", CardId: "card-tidal-barrier", Name: "Tidal Barrier"),
            (PetDefinitionId: "pet-thanh-xa", CardId: "card-venomous-bloom", Name: "Venomous Bloom"),
            (PetDefinitionId: "pet-son-hung", CardId: "card-earthshaker", Name: "Earthshaker"),
        };

        var fixture = new CollectionFixture();

        for (var index = 0; index < provisioned.Length; index++)
        {
            fixture.WithPet(
                $"pet_instance_{index}",
                Owner,
                provisioned[index].PetDefinitionId,
                Element.Hoa,
                PetTier.Common,
                star: 1,
                level: 1,
                identity: provisioned[index].PetDefinitionId,
                signatureSkillCardId: provisioned[index].CardId,
                signatureSkillName: provisioned[index].Name);
        }

        var pets = await CreateService(fixture).ListPetsAsync(Owner);

        Assert.Equal(5, pets.Count);

        foreach (var expected in provisioned)
        {
            var pet = Assert.Single(
                pets,
                candidate => candidate.Identity == expected.PetDefinitionId);

            Assert.Equal(expected.CardId, pet.SignatureSkill.CardId);
            Assert.Equal(expected.Name, pet.SignatureSkill.Name);
            Assert.Equal("PetSkill", pet.SignatureSkill.Category);
        }

        // All five distinct references were resolved in ONE read (no N+1), and
        // the read asked for exactly the five derived references.
        Assert.Equal(
            provisioned.Select(entry => entry.CardId).OrderBy(id => id, StringComparer.Ordinal),
            fixture.LastRequestedSignatureSkillIds.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListPets_ShouldNeverReachTheSignatureSkillThroughTheUnlockedCardCollection()
    {
        // §5.1 + CARD_RULES.md §1 item 4 / ADR-012 item 9: a Signature Skill is
        // DERIVED, so it has no PlayerUnlockedCard row and its Pet must still
        // project a complete reference. This is the property the contract exists
        // for — the unlocked collection is not the identification source.
        var fixture = new CollectionFixture()
            .WithPet(
                "pet_xich_lang",
                Owner,
                "def_xich_lang",
                Element.Hoa,
                PetTier.Common,
                star: 1,
                level: 1,
                signatureSkillCardId: "card-inferno",
                signatureSkillName: "Inferno");

        var service = CreateService(fixture);

        // The unlocked Card collection holds nothing at all.
        Assert.Empty(await service.ListCardsAsync(Owner));

        // The Signature Skill Card is content, never an unlock row.
        Assert.DoesNotContain(
            fixture.Unlocks,
            unlock => unlock.CardDefinitionId == "card-inferno");

        // And the Pet still reports its derived reference.
        var pet = Assert.Single(await service.ListPetsAsync(Owner));

        Assert.Equal("card-inferno", pet.SignatureSkill.CardId);
        Assert.Equal("Inferno", pet.SignatureSkill.Name);
    }

    [Fact]
    public async Task ListPets_ShouldRefuseAPetWhoseSignatureSkillCardDoesNotResolve()
    {
        // §5.1: SignatureSkillCardId is a required FK and a Pet has exactly one
        // Signature Skill (CARD_RULES.md §4 item 1), so an unresolvable reference
        // is a broken DATABASE.md §1 state rather than a contract case. No
        // placeholder is fabricated (AGENTS.md §7) — the same posture §5.4 takes
        // for an unresolvable RelicDefinition.
        var fixture = new CollectionFixture()
            .WithPet(
                "pet_1",
                Owner,
                "def_1",
                Element.Hoa,
                PetTier.Common,
                star: 1,
                level: 1,
                signatureSkillCardId: "card_skill_test",
                signatureSkillName: "Skill Test");

        fixture.Definitions.Remove("card_skill_test");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(fixture).ListPetsAsync(Owner));

        Assert.Contains("card_skill_test", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Element.Moc, "Wood")]
    [InlineData(Element.Tho, "Earth")]
    [InlineData(Element.Thuy, "Water")]
    [InlineData(Element.Hoa, "Fire")]
    [InlineData(Element.Kim, "Metal")]
    public async Task ListPets_ShouldMapEveryElementToItsDocumentedWireValue(
        Element element,
        string expected)
    {
        // §5.1 / TASK-072: the element member is the English wire value for all
        // five domain values — never Element.ToString().
        var fixture = new CollectionFixture()
            .WithPet("pet_1", Owner, "def_1", element, PetTier.Common, star: 1, level: 1);

        var pet = Assert.Single(await CreateService(fixture).ListPetsAsync(Owner));

        Assert.Equal(expected, pet.Element);
        Assert.NotEqual(element.ToString(), pet.Element);
    }

    [Theory]
    [InlineData(PetTier.Common, "Common")]
    [InlineData(PetTier.Rare, "Rare")]
    [InlineData(PetTier.Epic, "Epic")]
    [InlineData(PetTier.Legendary, "Legendary")]
    [InlineData(PetTier.Mythic, "Mythic")]
    public async Task ListPets_ShouldReportTierAsItsDocumentedName(
        PetTier tier,
        string expected)
    {
        // §5.1: tier is a STRING whose values are the five documented Tier names
        // (PET_RULES.md §3, DATABASE.md §3) — not the enum ordinal.
        var fixture = new CollectionFixture()
            .WithPet("pet_1", Owner, "def_1", Element.Hoa, tier, star: 1, level: 1);

        var pet = Assert.Single(await CreateService(fixture).ListPetsAsync(Owner));

        Assert.Equal(expected, pet.Tier);
    }

    [Fact]
    public async Task ListPets_ShouldExposeNoPersistedValueTheContractDoesNotBind()
    {
        // §5.1: "Persisted but NOT exposed: xp, acquiredAt, playerId,
        // petDefinitionId." The projected type carries none of them, and this
        // asserts that by name so a later addition cannot slip through.
        var fixture = new CollectionFixture()
            .WithPet(
                "pet_1",
                Owner,
                "def_1",
                Element.Hoa,
                PetTier.Common,
                star: 2,
                level: 9,
                acquiredAt: DateTimeOffset.UnixEpoch);

        var members = typeof(PetCollectionItem)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "Xp", "XP", "AcquiredAt", "PlayerId", "PetDefinitionId",
                     // §5.6 — no equip/loadout member on any §5 response.
                     "IsEquipped", "Equipped", "Slot", "LoadoutPosition", "Active",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }

        // The stored instance really does carry those values, so this proves the
        // projection omits them rather than that they do not exist.
        Assert.Equal(Owner, fixture.Pets["pet_1"].PlayerId);
        Assert.Equal("def_1", fixture.Pets["pet_1"].PetDefinitionId);
        Assert.Equal(DateTimeOffset.UnixEpoch, fixture.Pets["pet_1"].AcquiredAt);
    }

    [Fact]
    public async Task ListPets_ForAPlayerOwningNothing_ShouldReturnAnEmptyCollection()
    {
        // §5.5: "empty collection → 200 with []". At this layer that is an empty
        // list — not null, and not an error.
        var fixture = new CollectionFixture()
            .WithPet("pet_theirs", OtherPlayer, "def_1", Element.Hoa, PetTier.Common, 1, 1);

        var pets = await CreateService(fixture).ListPetsAsync(Owner);

        Assert.NotNull(pets);
        Assert.Empty(pets);
    }

    [Fact]
    public async Task ListPets_ShouldRequireARequestingPlayer()
    {
        // Without an authenticated identity the read cannot be scoped, and
        // defaulting one would be the client-authoritative claim §5 forbids
        // (GAME_RULES.md §18, ADR-001).
        var fixture = new CollectionFixture();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => CreateService(fixture).ListPetsAsync("  "));
    }

    [Fact]
    public async Task ListPets_ShouldNotReadContentItWasNotAskedFor()
    {
        // The definition read is a set lookup of the definitions the owned
        // instances reference — not a listing of all content. A definition owned
        // by nobody must not be materialized.
        var fixture = new CollectionFixture()
            .WithPet("pet_1", Owner, "def_mine", Element.Moc, PetTier.Common, 1, 1)
            .WithPetDefinition("def_unrelated", "Unrelated");

        var pets = await CreateService(fixture).ListPetsAsync(Owner);

        Assert.Equal("def_mine", fixture.LastRequestedDefinitionIds.Single());
        Assert.Single(pets);
    }

    // -----------------------------------------------------------------------
    // GET /api/pets/{petId} — API_CONTRACTS.md §5.2
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetOwnedPet_ForAnOwnedPet_ShouldReturnTheSameShapeAsAListElement()
    {
        // §5.2: "200 is the same object as one /api/pets array element (§5.1) —
        // no wrapper." Both paths run the same projection, so the two are equal
        // member for member.
        var fixture = new CollectionFixture()
            .WithPet("pet_1", Owner, "def_1", Element.Thuy, PetTier.Legendary, star: 4, level: 33);

        var service = CreateService(fixture);

        var listed = Assert.Single(await service.ListPetsAsync(Owner));
        var detail = await service.GetOwnedPetAsync("pet_1", Owner);

        Assert.NotNull(detail);
        Assert.Equal(listed, detail);
    }

    [Fact]
    public async Task GetOwnedPet_ForAMissingPet_ShouldReturnNothing()
    {
        // §5.2: a petId that does not exist is not readable.
        var fixture = new CollectionFixture();

        Assert.Null(await CreateService(fixture).GetOwnedPetAsync("pet_ghost", Owner));
    }

    [Fact]
    public async Task GetOwnedPet_ForAForeignPet_ShouldBeIndistinguishableFromAMissingOne()
    {
        // §5.2: "A petId that does not exist and a petId owned by another Player
        // return the identical response, so the endpoint never discloses whether
        // a Pet exists." At this layer the two produce the same single result —
        // the caller's own foreign read cannot differ from the missing read.
        var fixture = new CollectionFixture()
            .WithPet("pet_theirs", OtherPlayer, "def_1", Element.Kim, PetTier.Mythic, star: 5, level: 50);

        var service = CreateService(fixture);

        var missing = await service.GetOwnedPetAsync("pet_ghost", Owner);
        var foreign = await service.GetOwnedPetAsync("pet_theirs", Owner);

        Assert.Null(missing);
        Assert.Null(foreign);
        Assert.Equal(missing, foreign);

        // The Pet really does exist, so this proves the ownership comparison
        // rather than an absent row.
        Assert.True(fixture.Pets.ContainsKey("pet_theirs"));

        // And it is still readable by its own owner.
        Assert.NotNull(await service.GetOwnedPetAsync("pet_theirs", OtherPlayer));
    }

    [Fact]
    public async Task GetOwnedPet_ShouldProjectTheSameSixMembers()
    {
        // The detail response's member set is §5.1's, because §5.2 defines it as
        // the same object.
        var fixture = new CollectionFixture()
            .WithPet(
                "pet_1",
                Owner,
                "def_1",
                Element.Kim,
                PetTier.Epic,
                star: 3,
                level: 21,
                identity: "Bạch Hổ");

        var pet = await CreateService(fixture).GetOwnedPetAsync("pet_1", Owner);

        Assert.NotNull(pet);
        Assert.Equal("pet_1", pet!.PetId);
        Assert.Equal("Bạch Hổ", pet.Identity);
        Assert.Equal("Metal", pet.Element);
        Assert.Equal("Epic", pet.Tier);
        Assert.Equal(3, pet.Star);
        Assert.Equal(21, pet.Level);
    }

    [Fact]
    public async Task GetOwnedPet_ShouldRequireARequestingPlayer()
    {
        var fixture = new CollectionFixture();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => CreateService(fixture).GetOwnedPetAsync("pet_1", ""));
    }

    [Fact]
    public async Task GetOwnedPet_ShouldRequireAPetIdentity()
    {
        var fixture = new CollectionFixture();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => CreateService(fixture).GetOwnedPetAsync("  ", Owner));
    }

    // -----------------------------------------------------------------------
    // GET /api/cards — API_CONTRACTS.md §5.3
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListCards_ShouldReturnOnlyTheCallersUnlockedDefinitions()
    {
        // §5.3: membership comes from the Player's PlayerUnlockedCard rows, and
        // "presence in this array IS the unlocked state" (ADR-012 item 9 — no
        // Card instance exists).
        var fixture = new CollectionFixture()
            .WithCard("card_heal", "Heal", CardCategory.Basic, unlockedBy: Owner)
            .WithCard("card_shield", "Shield", CardCategory.Basic, unlockedBy: Owner)
            .WithCard("card_skill_inferno", "Inferno", CardCategory.PetSkill, unlockedBy: Owner)
            .WithCard("card_theirs", "Theirs", CardCategory.Basic, unlockedBy: OtherPlayer)
            .WithCard("card_unowned", "Unowned", CardCategory.Basic, unlockedBy: null);

        var cards = await CreateService(fixture).ListCardsAsync(Owner);

        Assert.Equal(
            new[] { "card_heal", "card_shield", "card_skill_inferno" },
            cards.Select(card => card.CardId).OrderBy(id => id, StringComparer.Ordinal));

        // The unowned definition really exists as content, so this proves the
        // unlock filter rather than an absent row.
        Assert.True(fixture.Definitions.ContainsKey("card_unowned"));
    }

    [Fact]
    public async Task ListCards_ShouldProjectExactlyTheFourDocumentedMembers()
    {
        // §5.3: cardId, name, category, effectDefinition — no more, no fewer.
        var members = typeof(CardCollectionItem)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "CardId", "Category", "EffectDefinition", "Name" }, members);

        // And no `unlocked` member exists: §5.3 makes array membership the state
        // ("there is no unlocked member").
        Assert.DoesNotContain("Unlocked", members);
        Assert.DoesNotContain("IsUnlocked", members);

        var fixture = new CollectionFixture()
            .WithCard("card_heal", "Heal", CardCategory.Basic, unlockedBy: Owner);

        var card = Assert.Single(await CreateService(fixture).ListCardsAsync(Owner));

        // cardId = CardDefinition.CardDefinitionId (§5.3).
        Assert.Equal("card_heal", card.CardId);

        // name = CardDefinition.Name (§5.3).
        Assert.Equal("Heal", card.Name);

        // category = CardDefinition.Category, "Basic" | "PetSkill" (§5.3).
        Assert.Contains(card.Category, new[] { "Basic", "PetSkill" });

        // effectDefinition = the stored CardDefinition.EffectDefinition,
        // element for element and member for member (DATABASE.md §1).
        Assert.Equal(TestCardEffects.FlatPower, card.EffectDefinition);
    }

    [Fact]
    public async Task ListCards_ShouldCarryAMultiEffectDefinitionUnchanged()
    {
        // §5.3 / DATABASE.md §1 item 1 / TASK-111 D-1b: one element per effect,
        // in stored order, with the effect-specific extra members the contract's
        // present-iff rules define. The projection is a field copy — nothing is
        // reordered, merged, or recomputed.
        var stored = CardEffectDefinitions.Create(
            CardEffectDefinition.Create(CardEffectType.Damage, CardEffectValueType.Flat, 100),
            CardEffectDefinition.Burn(CardEffectValueType.Flat, 50, duration: 2));

        var fixture = new CollectionFixture()
            .WithCard(
                "card_inferno",
                "Inferno",
                CardCategory.PetSkill,
                unlockedBy: Owner,
                effectDefinition: stored);

        var card = Assert.Single(await CreateService(fixture).ListCardsAsync(Owner));

        Assert.Equal(stored, card.EffectDefinition);
        Assert.Equal(2, card.EffectDefinition.Count);

        // The element identity, interpretation, magnitude, and Burn-only
        // duration survive exactly; the Crit-only scope is still absent.
        Assert.Equal(CardEffectType.Damage, card.EffectDefinition[0].EffectType);
        Assert.Equal(CardEffectValueType.Flat, card.EffectDefinition[0].ValueType);
        Assert.Equal(100, card.EffectDefinition[0].Value);
        Assert.Null(card.EffectDefinition[0].Duration);
        Assert.Null(card.EffectDefinition[0].Scope);

        Assert.Equal(CardEffectType.Burn, card.EffectDefinition[1].EffectType);
        Assert.Equal(50, card.EffectDefinition[1].Value);
        Assert.Equal(2, card.EffectDefinition[1].Duration);
        Assert.Null(card.EffectDefinition[1].Scope);
    }

    [Fact]
    public async Task ListCards_ShouldNotExposeCostLegalityOrAffordability()
    {
        // The content/cost boundary: §5.3 answers "what does this Card do?" and
        // never "can I afford to cast it right now" (CARD_RULES.md §3.6,
        // SIGNALR_PROTOCOL.md §4 item 15). The stored definition carries the cost
        // and the copy limit; the projection carries neither.
        var fixture = new CollectionFixture()
            .WithCard(
                "card_heal",
                "Heal",
                CardCategory.Basic,
                unlockedBy: Owner,
                powerCost: 25,
                loadoutCopyLimit: 3,
                effectDefinition: TestCardEffects.PercentMaxHp);

        var members = typeof(CardCollectionItem)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "PlayerId", "PowerCost", "LoadoutCopyLimit",
                     "EffectiveCost", "CardCostModifier", "Affordable", "CanCast",
                     "Legality", "Unlocked", "IsUnlocked",
                     "IsEquipped", "Equipped", "Slot", "LoadoutPosition", "Active",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }

        var card = Assert.Single(await CreateService(fixture).ListCardsAsync(Owner));

        Assert.Equal(TestCardEffects.PercentMaxHp, card.EffectDefinition);
        Assert.Equal(25, fixture.Definitions["card_heal"].PowerCost);
        Assert.Equal(3, fixture.Definitions["card_heal"].LoadoutCopyLimit);
    }

    [Theory]
    [InlineData(CardCategory.Basic, "Basic")]
    [InlineData(CardCategory.PetSkill, "PetSkill")]
    public async Task ListCards_ShouldReportCategoryAsItsDocumentedWireValue(
        CardCategory category,
        string expected)
    {
        // §5.3 / CARD_RULES.md §1 / DATABASE.md §3: the closed two-member set.
        var fixture = new CollectionFixture()
            .WithCard("card_1", "Card", category, unlockedBy: Owner);

        var card = Assert.Single(await CreateService(fixture).ListCardsAsync(Owner));

        Assert.Equal(expected, card.Category);
    }

    [Fact]
    public async Task ListCards_ShouldExposeNoExcludedColumn()
    {
        // §5.3: "playerId, powerCost, loadoutCopyLimit" are persisted or
        // definition data but NOT exposed; §5.6 excludes the equip members.
        // effectDefinition IS exposed (this task's amendment). The stored
        // definition carries the withheld values, so their absence at the
        // projection is a real omission.
        var fixture = new CollectionFixture()
            .WithCard(
                "card_heal",
                "Heal",
                CardCategory.Basic,
                unlockedBy: Owner,
                powerCost: 25,
                loadoutCopyLimit: 3,
                effectDefinition: TestCardEffects.PercentMaxHp);

        var members = typeof(CardCollectionItem)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "PlayerId", "PowerCost", "LoadoutCopyLimit",
                     "Unlocked", "IsUnlocked",
                     "IsEquipped", "Equipped", "Slot", "LoadoutPosition", "Active",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }

        Assert.Equal(25, fixture.Definitions["card_heal"].PowerCost);
        Assert.Equal(3, fixture.Definitions["card_heal"].LoadoutCopyLimit);
        Assert.Equal(
            TestCardEffects.PercentMaxHp,
            fixture.Definitions["card_heal"].EffectDefinition);

        Assert.Single(await CreateService(fixture).ListCardsAsync(Owner));
    }

    [Fact]
    public async Task ListCards_ForAPlayerWithNoUnlocks_ShouldReturnAnEmptyCollection()
    {
        // §5.5: an empty unlock set is the documented empty array.
        var fixture = new CollectionFixture()
            .WithCard("card_theirs", "Theirs", CardCategory.Basic, unlockedBy: OtherPlayer);

        var cards = await CreateService(fixture).ListCardsAsync(Owner);

        Assert.NotNull(cards);
        Assert.Empty(cards);
    }

    [Fact]
    public async Task ListCards_ShouldRequireARequestingPlayer()
    {
        var fixture = new CollectionFixture();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => CreateService(fixture).ListCardsAsync(""));
    }

    // -----------------------------------------------------------------------
    // GET /api/relics — API_CONTRACTS.md §5.4
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ListRelics_ShouldReturnOnlyTheCallersOwnedInstances()
    {
        // §5.4: results are the Player's owned Relic INSTANCES
        // (DATABASE.md §2: Player 1 ── N Relic).
        var fixture = new CollectionFixture()
            .WithRelic("relic_1", Owner, "relic_def_berserker")
            .WithRelic("relic_2", Owner, "relic_def_mana")
            .WithRelic("relic_theirs", OtherPlayer, "relic_def_berserker");

        var relics = await CreateService(fixture).ListRelicsAsync(Owner);

        Assert.Equal(
            new[] { "relic_1", "relic_2" },
            relics.Select(relic => relic.RelicId).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListRelics_ShouldProjectExactlyTheFiveDocumentedMembers()
    {
        // §5.4: relicId, name, trigger, condition, effectDefinition — no more,
        // no fewer.
        var members = typeof(RelicCollectionItem)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "Condition", "EffectDefinition", "Name", "RelicId", "Trigger" },
            members);

        var fixture = new CollectionFixture()
            .WithRelic("relic_instance_berserker", Owner, "relic_def_berserker")
            .WithRelicDefinition("relic_def_berserker", "Berserker Core");

        var relic = Assert.Single(await CreateService(fixture).ListRelicsAsync(Owner));

        // relicId = Relic.RelicInstanceId (§5.4) — the INSTANCE, not the
        // definition identity.
        Assert.Equal("relic_instance_berserker", relic.RelicId);
        Assert.NotEqual("relic_def_berserker", relic.RelicId);

        // name = RelicDefinition.Name (§5.4) — read through the instance's
        // definition reference.
        Assert.Equal("Berserker Core", relic.Name);

        // trigger / condition / effectDefinition = the same definition row's own
        // content (§5.4, RELIC_RULES.md §3/§8.1-§8.3).
        Assert.Equal("OnTurnEnd", relic.Trigger);
        Assert.Null(relic.Condition);
        Assert.Equal(TestRelicEffects.Effect, relic.EffectDefinition);
    }

    [Fact]
    public async Task ListRelics_ShouldCarryTheStructuredConditionWhenTheDefinitionHasOne()
    {
        // §5.4: the condition is the §8.1 form plus its threshold, carried
        // unchanged, and it stays nullable for a definition that declares none.
        var condition = RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30);

        var fixture = new CollectionFixture()
            .WithRelic("relic_1", Owner, "relic_def_1")
            .WithRelicDefinition(
                "relic_def_1",
                "Emergency Core",
                trigger: "OnHpBelow",
                condition: condition,
                effectDefinition: RelicEffectDefinitions.Create(
                    RelicEffectDefinition.Create(
                        RelicEffectType.CardCost,
                        RelicEffectValueType.Percentage,
                        50,
                        RelicEffectTarget.Pet,
                        RelicEffectLifetime.Battle)));

        var relic = Assert.Single(await CreateService(fixture).ListRelicsAsync(Owner));

        Assert.Equal("OnHpBelow", relic.Trigger);
        Assert.Equal(condition, relic.Condition);
        Assert.Equal(RelicConditionType.HpPercentageBelow, relic.Condition!.Value.ConditionType);
        Assert.Equal(30, relic.Condition!.Value.Threshold);

        // The effect element's own members survive the projection exactly.
        Assert.Equal(1, relic.EffectDefinition.Count);
        Assert.Equal(RelicEffectType.CardCost, relic.EffectDefinition[0].EffectType);
        Assert.Equal(RelicEffectValueType.Percentage, relic.EffectDefinition[0].ValueType);
        Assert.Equal(50, relic.EffectDefinition[0].Value);
        Assert.Equal(RelicEffectTarget.Pet, relic.EffectDefinition[0].Target);
        Assert.Equal(RelicEffectLifetime.Battle, relic.EffectDefinition[0].Lifetime);
    }

    [Fact]
    public async Task ListRelics_ShouldCarryTheInstanceIdentityAndTheDefinitionName()
    {
        // §5.4: relicId is the owned INSTANCE and name resolves through the
        // instance's definition reference — the mapping the content members ride
        // on, unchanged by this amendment.
        var fixture = new CollectionFixture()
            .WithRelic("relic_instance_berserker", Owner, "relic_def_berserker")
            .WithRelicDefinition("relic_def_berserker", "Berserker Core");

        var relic = Assert.Single(await CreateService(fixture).ListRelicsAsync(Owner));

        Assert.Equal("relic_instance_berserker", relic.RelicId);
        Assert.Equal("Berserker Core", relic.Name);
    }

    [Fact]
    public async Task ListRelics_ShouldReturnTwoElementsForTwoInstancesOfOneDefinition()
    {
        // RELIC_RULES.md §2.4 item 3 / §2.2: ownership is per instance, so two
        // owned copies of one definition are two elements with distinct
        // relicIds and the same name.
        var fixture = new CollectionFixture()
            .WithRelic("relic_a", Owner, "relic_def_berserker")
            .WithRelic("relic_b", Owner, "relic_def_berserker")
            .WithRelicDefinition("relic_def_berserker", "Berserker Core");

        var relics = await CreateService(fixture).ListRelicsAsync(Owner);

        Assert.Equal(2, relics.Count);
        Assert.Equal(
            new[] { "relic_a", "relic_b" },
            relics.Select(relic => relic.RelicId).OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(relics, relic => Assert.Equal("Berserker Core", relic.Name));
    }

    [Fact]
    public async Task ListRelics_ShouldExposeNoExcludedMember()
    {
        // §5.4: "Not exposed: playerId, acquiredAt, definitionId". §5.6 adds the
        // equip members. trigger / condition / effectDefinition ARE exposed (this
        // task's amendment). The stored rows carry all of them, so the omission of
        // the still-withheld ones is real.
        //
        // RELIC_RULES.md §8.1/§8.2 (TASK-132) make Condition and EffectDefinition
        // STRUCTURED values on the definition; §5.4 now carries them inline.
        var condition = RelicCondition.Create(RelicConditionType.ComboAtLeast, 3);
        var effects = TestRelicEffects.Effect;

        var fixture = new CollectionFixture()
            .WithRelic("relic_1", Owner, "relic_def_1")
            .WithRelicDefinition(
                "relic_def_1",
                "Berserker Core",
                trigger: "OnCombo3Plus",
                condition: condition,
                effectDefinition: effects);

        var members = typeof(RelicCollectionItem)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (var forbidden in new[]
                 {
                     "PlayerId", "AcquiredAt", "DefinitionId", "RelicDefinitionId",
                     "IsEquipped", "Equipped", "Slot", "LoadoutPosition", "Active",
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }

        var definition = fixture.RelicDefinitionRows.Single(
            relic => relic.RelicDefinitionId == "relic_def_1");

        Assert.Equal("OnCombo3Plus", definition.Trigger);
        Assert.Equal(condition, definition.Condition);
        Assert.Equal(effects, definition.EffectDefinition);

        // The projection carries the same three values, unchanged.
        var relic = Assert.Single(await CreateService(fixture).ListRelicsAsync(Owner));

        Assert.Equal("OnCombo3Plus", relic.Trigger);
        Assert.Equal(condition, relic.Condition);
        Assert.Equal(effects, relic.EffectDefinition);
    }

    [Fact]
    public async Task ListRelics_ForAPlayerOwningNothing_ShouldReturnAnEmptyCollection()
    {
        // §5.5: empty → [].
        var fixture = new CollectionFixture()
            .WithRelic("relic_theirs", OtherPlayer, "relic_def_1");

        var relics = await CreateService(fixture).ListRelicsAsync(Owner);

        Assert.NotNull(relics);
        Assert.Empty(relics);
    }

    [Fact]
    public async Task ListRelics_ShouldResolveNamesInOneBulkRead()
    {
        // The projection issues one content read for the whole collection rather
        // than one per instance — the set passed is exactly the distinct
        // definitions the owned instances reference.
        var fixture = new CollectionFixture()
            .WithRelic("relic_a", Owner, "relic_def_berserker")
            .WithRelic("relic_b", Owner, "relic_def_berserker")
            .WithRelic("relic_c", Owner, "relic_def_mana")
            .WithRelicDefinition("relic_def_berserker", "Berserker Core")
            .WithRelicDefinition("relic_def_mana", "Mana Crystal");

        await CreateService(fixture).ListRelicsAsync(Owner);

        Assert.Equal(
            new[] { "relic_def_berserker", "relic_def_mana" },
            fixture.LastRequestedDefinitionIds.OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ListRelics_ShouldRequireARequestingPlayer()
    {
        var fixture = new CollectionFixture();

        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => CreateService(fixture).ListRelicsAsync("  "));
    }

    // -----------------------------------------------------------------------
    // §5.5 / §5.6 — semantics shared by all four reads
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AllCollections_ShouldReturnAnEmptyArrayRatherThanAnError()
    {
        // §5.5 states one rule for all three list routes: "empty collection →
        // 200 with []". At this layer that is an empty sequence for each, and
        // the endpoints above it map that to the same 200.
        var fixture = new CollectionFixture();
        var service = CreateService(fixture);

        Assert.Empty(await service.ListPetsAsync(Owner));
        Assert.Empty(await service.ListCardsAsync(Owner));
        Assert.Empty(await service.ListRelicsAsync(Owner));
    }

    [Fact]
    public void CollectionReadModels_ShouldDeclareNoPaginationWrapperMember()
    {
        // §5.5: MVP has "no page/limit/cursor/sort/filter/search parameters; the
        // array is the full collection", and §5 states the response is bare — no
        // envelope. No member of any projection may be a wrapper member.
        var projections = new[]
        {
            typeof(PetCollectionItem),
            typeof(PetSignatureSkillItem),
            typeof(CardCollectionItem),
            typeof(RelicCollectionItem),
        };

        foreach (var projection in projections)
        {
            var members = projection.GetProperties().Select(p => p.Name).ToArray();

            foreach (var forbidden in new[]
                     {
                         "Page", "Limit", "Cursor", "Sort", "Filter", "Search",
                         "Total", "Items", "Data", "Next", "Offset",
                     })
            {
                Assert.DoesNotContain(forbidden, members);
            }
        }
    }

    [Fact]
    public async Task ListPets_ShouldReturnEveryOwnedInstanceWithNoPageLimit()
    {
        // §5.5: "the array is the full collection" — no page, no limit, no
        // truncation. A Player with many Pets receives all of them.
        var fixture = new CollectionFixture();

        for (var index = 0; index < 25; index++)
        {
            fixture.WithPet($"pet_{index:D2}", Owner, "def_1", Element.Hoa, PetTier.Common, 1, 1);
        }

        var pets = await CreateService(fixture).ListPetsAsync(Owner);

        Assert.Equal(25, pets.Count);
        Assert.Equal(
            Enumerable.Range(0, 25).Select(index => $"pet_{index:D2}"),
            pets.Select(pet => pet.PetId).OrderBy(id => id, StringComparer.Ordinal));
    }

    // -----------------------------------------------------------------------
    // Fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// The collection rows the four reads are exercised against.
    ///
    /// <b>It seeds real definitions, not just instances.</b> §5.1 and §5.4 define
    /// members that live on the definition row, so a fixture without one could
    /// not produce a contract-valid projection.
    /// </summary>
    private sealed class CollectionFixture
    {
        public Dictionary<string, Pet> Pets { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, CardDefinition> Definitions { get; } =
            new(StringComparer.Ordinal);

        public List<PlayerUnlockedCard> Unlocks { get; } = [];

        public Dictionary<string, Relic> Relics { get; } = new(StringComparer.Ordinal);

        /// <summary>The definition set the last bulk Pet-definition read was asked for.</summary>
        public IReadOnlyCollection<string> LastRequestedDefinitionIds { get; set; } = [];

        /// <summary>
        /// The definition set the last bulk Card-definition read was asked for —
        /// the derived Signature Skill references of the projected Pets
        /// (<c>API_CONTRACTS.md</c> §5.1).
        /// </summary>
        public IReadOnlyCollection<string> LastRequestedSignatureSkillIds { get; set; } = [];

        private readonly Dictionary<string, PetDefinition> _petDefinitions =
            new(StringComparer.Ordinal);

        private readonly Dictionary<string, RelicDefinition> _relicDefinitions =
            new(StringComparer.Ordinal);

        public CollectionFixture WithPet(
            string petInstanceId,
            string playerId,
            string petDefinitionId,
            Element element,
            PetTier tier,
            int star,
            int level,
            string identity = "Pet",
            DateTimeOffset? acquiredAt = null,
            string signatureSkillCardId = "card_skill_test",
            string signatureSkillName = "Skill Test")
        {
            SeedSignatureSkill(signatureSkillCardId, signatureSkillName);

            _petDefinitions[petDefinitionId] = new PetDefinition
            {
                PetDefinitionId = petDefinitionId,
                Identity = identity,
                Element = element,
                PassiveId = new PassiveId("test-passive"),
                PassiveThreshold = 5,
                SignatureSkillCardId = signatureSkillCardId,
            };

            Pets[petInstanceId] = new Pet
            {
                PetInstanceId = petInstanceId,
                PlayerId = playerId,
                PetDefinitionId = petDefinitionId,
                Tier = tier,
                Star = star,
                XP = Pet.InitialXp,
                Level = level,
                AcquiredAt = acquiredAt ?? DateTimeOffset.UtcNow,
            };

            return this;
        }

        /// <summary>
        /// Seeds the <c>CardDefinition</c> row a Pet's required
        /// <c>SignatureSkillCardId</c> FK points at
        /// (<c>DATABASE.md</c> §2: PetDefinition N ── 1 CardDefinition).
        ///
        /// It is <b>not</b> an unlock: the row is content the Pet's definition
        /// references and never a <c>PlayerUnlockedCard</c> row
        /// (<c>CARD_RULES.md</c> §1 item 4, ADR-012 item 9), which is exactly why
        /// it must not reach <c>GET /api/cards</c> (<c>API_CONTRACTS.md</c> §5.3).
        /// A definition already seeded by a test is left untouched.
        /// </summary>
        public CollectionFixture SeedSignatureSkill(string cardDefinitionId, string name)
        {
            Definitions.TryAdd(
                cardDefinitionId,
                new CardDefinition
                {
                    CardDefinitionId = cardDefinitionId,
                    Name = name,
                    Category = CardCategory.PetSkill,
                    PowerCost = 100,
                    EffectDefinition = TestCardEffects.FlatPower,
                    LoadoutCopyLimit = 1,
                });

            return this;
        }

        /// <summary>
        /// Adds a Pet definition that no owned instance references — content the
        /// collection read must not materialize.
        /// </summary>
        public CollectionFixture WithPetDefinition(string petDefinitionId, string identity)
        {
            SeedSignatureSkill("card_skill_test", "Skill Test");

            _petDefinitions[petDefinitionId] = new PetDefinition
            {
                PetDefinitionId = petDefinitionId,
                Identity = identity,
                Element = Element.Moc,
                PassiveId = new PassiveId("test-passive"),
                PassiveThreshold = 5,
                SignatureSkillCardId = "card_skill_test",
            };

            return this;
        }

        public CollectionFixture WithCard(
            string cardDefinitionId,
            string name,
            CardCategory category,
            string? unlockedBy,
            int powerCost = 0,
            int loadoutCopyLimit = 1,
            CardEffectDefinitions? effectDefinition = null)
        {
            Definitions[cardDefinitionId] = new CardDefinition
            {
                CardDefinitionId = cardDefinitionId,
                Name = name,
                Category = category,
                PowerCost = powerCost,
                EffectDefinition = effectDefinition ?? TestCardEffects.FlatPower,
                LoadoutCopyLimit = loadoutCopyLimit,
            };

            if (unlockedBy is not null)
            {
                Unlocks.Add(new PlayerUnlockedCard
                {
                    PlayerId = unlockedBy,
                    CardDefinitionId = cardDefinitionId,
                });
            }

            return this;
        }

        /// <summary>
        /// Seeds one owned Relic instance.
        ///
        /// <b>Its definition row is created too, unless the fixture already holds
        /// one.</b> <c>DATABASE.md</c> §2 makes the instance's
        /// <c>RelicDefinitionId</c> a foreign key (Relic N ── 1 RelicDefinition),
        /// so an instance whose definition does not exist is a state the
        /// documented schema cannot hold — a fixture that produced one would be
        /// testing an impossible database rather than the contract. A test that
        /// wants a specific definition name supplies it with
        /// <see cref="WithRelicDefinition"/> first.
        /// </summary>
        public CollectionFixture WithRelic(
            string relicInstanceId,
            string playerId,
            string relicDefinitionId)
        {
            Relics[relicInstanceId] = new Relic
            {
                RelicInstanceId = relicInstanceId,
                PlayerId = playerId,
                RelicDefinitionId = relicDefinitionId,
                AcquiredAt = DateTimeOffset.UtcNow,
            };

            _relicDefinitions.TryAdd(
                relicDefinitionId,
                new RelicDefinition
                {
                    RelicDefinitionId = relicDefinitionId,
                    Name = relicDefinitionId,
                    Trigger = "OnTurnEnd",
                    EffectDefinition = TestRelicEffects.Effect,
                });

            return this;
        }

        public CollectionFixture WithRelicDefinition(
            string relicDefinitionId,
            string name,
            string trigger = "OnTurnEnd",
            RelicCondition? condition = null,
            RelicEffectDefinitions? effectDefinition = null)
        {
            _relicDefinitions[relicDefinitionId] = new RelicDefinition
            {
                RelicDefinitionId = relicDefinitionId,
                Name = name,
                Trigger = trigger,
                Condition = condition,
                EffectDefinition = effectDefinition ?? TestRelicEffects.Effect,
            };

            return this;
        }

        /// <summary>The Pet definitions the fixture holds, for the boundary reads.</summary>
        internal IReadOnlyCollection<PetDefinition> PetDefinitions =>
            _petDefinitions.Values;

        /// <summary>The Relic definitions the fixture holds.</summary>
        internal IReadOnlyCollection<RelicDefinition> RelicDefinitionRows =>
            _relicDefinitions.Values;
    }

    /// <summary>
    /// The Pet persistence boundary over the fixture. Its reads are the real
    /// Player-scoped ones; its writes are defects on this path, exactly as the
    /// other Application test doubles state.
    /// </summary>
    private sealed class FakePetRepository : IPetRepository
    {
        private readonly CollectionFixture _fixture;

        public FakePetRepository(CollectionFixture fixture)
        {
            _fixture = fixture;
        }

        public Task AddAsync(Pet pet, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The collection read never writes Pet rows.");

        public Task<IReadOnlyList<Pet>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Pet>>(
                _fixture.Pets.Values
                    .Where(pet => pet.PlayerId == playerId)
                    .ToList());

        public Task<IReadOnlyList<PetDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> petDefinitionIds,
            CancellationToken cancellationToken = default)
        {
            _fixture.LastRequestedDefinitionIds = petDefinitionIds;

            return Task.FromResult<IReadOnlyList<PetDefinition>>(
                _fixture.PetDefinitions
                    .Where(definition => petDefinitionIds.Contains(definition.PetDefinitionId))
                    .ToList());
        }

        public Task<PetDefinition?> GetDefinitionAsync(
            string petDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _fixture.PetDefinitions
                    .FirstOrDefault(definition => definition.PetDefinitionId == petDefinitionId));

        public Task<Pet?> GetByIdAsync(
            string petInstanceId,
            CancellationToken cancellationToken = default) =>
            // Deliberately NOT Player-filtered: §5.2's caller must be able to tell
            // "no such Pet" from "another Player's Pet", which a Player-filtered
            // query would collapse. The service applies that comparison.
            Task.FromResult(
                _fixture.Pets.TryGetValue(petInstanceId, out var pet) ? pet : null);

        public Task<bool> SaveProgressionAsync(
            Pet pet,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The collection read never writes progression.");
    }

    /// <summary>The Card persistence boundary over the fixture.</summary>
    private sealed class FakeCardRepository : ICardRepository
    {
        private readonly CollectionFixture _fixture;

        public FakeCardRepository(CollectionFixture fixture)
        {
            _fixture = fixture;
        }

        public Task AddDefinitionAsync(
            CardDefinition definition,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The collection read never writes definition rows.");

        public Task AddUnlockAsync(
            PlayerUnlockedCard unlockedCard,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The collection read never writes unlock rows.");

        public Task<IReadOnlyList<CardDefinition>> ListUnlockedAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            // The unlock set joined to its definitions, Player-filtered at the
            // query itself (API_CONTRACTS.md §5.3).
            Task.FromResult<IReadOnlyList<CardDefinition>>(
                _fixture.Unlocks
                    .Where(unlock => unlock.PlayerId == playerId)
                    .Select(unlock => _fixture.Definitions.GetValueOrDefault(unlock.CardDefinitionId))
                    .Where(definition => definition is not null)
                    .Select(definition => definition!)
                    .ToList());

        public Task<IReadOnlyList<CardDefinition>> ListUnlockedDefinitionsAsync(
            string playerId,
            IReadOnlyCollection<string> cardDefinitionIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CardDefinition>>([]);

        public Task<IReadOnlyList<CardDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> cardDefinitionIds,
            CancellationToken cancellationToken = default)
        {
            // API_CONTRACTS.md §5.1's derived Signature Skill lookup: an
            // unrestricted content read on the definition table, deliberately not
            // filtered by the unlock rows.
            _fixture.LastRequestedSignatureSkillIds = cardDefinitionIds;

            return Task.FromResult<IReadOnlyList<CardDefinition>>(
                cardDefinitionIds
                    .Select(_fixture.Definitions.GetValueOrDefault)
                    .Where(definition => definition is not null)
                    .Select(definition => definition!)
                    .ToList());
        }

        public Task<CardDefinition?> GetDefinitionAsync(
            string cardDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_fixture.Definitions.GetValueOrDefault(cardDefinitionId));
    }

    /// <summary>The Relic persistence boundary over the fixture.</summary>
    private sealed class FakeRelicRepository : IRelicRepository
    {
        private readonly CollectionFixture _fixture;

        public FakeRelicRepository(CollectionFixture fixture)
        {
            _fixture = fixture;
        }

        public Task AddAsync(Relic relic, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The collection read never writes ownership rows.");

        public Task AddDefinitionAsync(
            RelicDefinition definition,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The collection read never writes definition rows.");

        public Task<IReadOnlyList<Relic>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Relic>>(
                _fixture.Relics.Values
                    .Where(relic => relic.PlayerId == playerId)
                    .ToList());

        public Task<IReadOnlyList<Relic>> ListOwnedInstancesAsync(
            string playerId,
            IReadOnlyCollection<string> relicInstanceIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Relic>>(
                _fixture.Relics.Values
                    .Where(relic => relic.PlayerId == playerId)
                    .Where(relic => relicInstanceIds.Contains(relic.RelicInstanceId))
                    .ToList());

        public Task<RelicDefinition?> GetDefinitionAsync(
            string relicDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                _fixture.RelicDefinitionRows
                    .FirstOrDefault(definition => definition.RelicDefinitionId == relicDefinitionId));

        public Task<IReadOnlyList<RelicDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> relicDefinitionIds,
            CancellationToken cancellationToken = default)
        {
            _fixture.LastRequestedDefinitionIds = relicDefinitionIds;

            return Task.FromResult<IReadOnlyList<RelicDefinition>>(
                _fixture.RelicDefinitionRows
                    .Where(definition => relicDefinitionIds.Contains(definition.RelicDefinitionId))
                    .ToList());
        }
    }
}
