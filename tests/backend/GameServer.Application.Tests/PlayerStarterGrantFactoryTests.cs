using GameServer.Application.Cards;
using GameServer.Application.Pets;
using GameServer.Application.Players;
using GameServer.Application.Relics;
using GameServer.Domain.Cards;
using GameServer.Domain.Elements;
using GameServer.Domain.Passives;
using GameServer.Domain.Pets;
using GameServer.Domain.Relics;

namespace GameServer.Application.Tests;

/// <summary>
/// The starter ownership composition — <c>DATABASE.md</c> §2 item 1 (TASK-084 /
/// TASK-082 decision E / R1-4).
///
/// <code>
/// provisioned definitions
///         ↓  PlayerStarterGrantFactory
/// 1 Pet  — pet-xich-lang
/// 3 Cards — card-heal, card-shield, card-power-charge
/// 3 Relics — relic-berserker-core, relic-mana-crystal, relic-assassin-eye
/// </code>
///
/// The composition is a fixed server-side set drawn only from provisioned
/// content, so these tests assert three things about it: <b>what</b> it contains,
/// <b>which creation values</b> the Pet carries, and <b>what it refuses</b> — a
/// missing definition row aborts the whole grant rather than producing a partial
/// or substituted set.
///
/// The composed rows are deliberately owner-less: the owning <c>PlayerId</c> is
/// minted by the creation boundary, which is the only place the creation branch
/// is observable. That is what makes "starter set on creation only" structural,
/// and it is asserted here rather than in a comment.
/// </summary>
public class PlayerStarterGrantFactoryTests
{
    private const string PetDefinitionId = "pet-xich-lang";

    /// <summary>
    /// A content store holding exactly the provisioned definitions the starter
    /// set references — the TASK-085 rows (<c>DATABASE.md</c> §5 item 4).
    /// </summary>
    private static (PlayerStarterGrantFactory Factory, ContentStore Content) BuildFactory()
    {
        var content = new ContentStore();

        content.AddPet(PetDefinitionId, "Xích Lang", Element.Hoa, "card-inferno");
        content.AddCard("card-heal", "Heal", CardCategory.Basic);
        content.AddCard("card-shield", "Shield", CardCategory.Basic);
        content.AddCard("card-power-charge", "Power Charge", CardCategory.Basic);
        content.AddRelic("relic-berserker-core", "Berserker Core");
        content.AddRelic("relic-mana-crystal", "Mana Crystal");
        content.AddRelic("relic-assassin-eye", "Assassin Eye");

        return (
            new PlayerStarterGrantFactory(
                new StubPetRepository(content),
                new StubCardRepository(content),
                new StubRelicRepository(content)),
            content);
    }

    // -----------------------------------------------------------------------
    // A. The composition: exactly 1 Pet / 3 Cards / 3 Relics
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldComposeExactlyOnePetThreeCardsAndThreeRelics()
    {
        // DATABASE.md §2 item 1: "1 Pet, 3 Cards, 3 Relics" — and TASK-083's
        // acceptance criteria bind those counts exactly, not as a range.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.NotNull(grant.StarterPet);
        Assert.Equal(3, grant.StarterCards.Count);
        Assert.Equal(3, grant.StarterRelics.Count);
    }

    // -----------------------------------------------------------------------
    // B. The exact starter contents
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldReferenceTheXichLangPetDefinition()
    {
        // DATABASE.md §2 item 1 — the starter Pet is pet-xich-lang, one of the
        // three Pets PET_RULES.md §8 records as provisioned. Bạch Hổ and
        // Huyền Quy are never starter Pets.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(PetDefinitionId, grant.StarterPet.PetDefinitionId);
    }

    [Fact]
    public async Task CreateAsync_ShouldReferenceExactlyTheThreeBasicStarterCards()
    {
        // DATABASE.md §2 item 1 / CARD_RULES.md §2: Heal, Shield, Power Charge —
        // all three content-defined Basic Cards, in any order (no rule reads a
        // Card array position: GAME_STATE.md §2.3).
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(
            ["card-heal", "card-power-charge", "card-shield"],
            grant.StarterCards
                .Select(card => card.CardDefinitionId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task CreateAsync_ShouldReferenceExactlyTheThreeSelectedStarterRelics()
    {
        // DATABASE.md §2 item 1: Berserker Core, Mana Crystal, Assassin Eye —
        // explicitly selected, NOT derived from document ordering, and never
        // Emergency Core (which is provisioned but not selected).
        var (factory, content) = BuildFactory();

        // Emergency Core is provisioned: its absence from the starter set is a
        // selection, not a content gap.
        content.AddRelic("relic-emergency-core", "Emergency Core");

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(
            ["relic-assassin-eye", "relic-berserker-core", "relic-mana-crystal"],
            grant.StarterRelics
                .Select(relic => relic.RelicDefinitionId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public async Task CreateAsync_ShouldNotGrantAnyPetSkillCard()
    {
        // CARD_RULES.md §1 item 4: a PetSkill Card is derived from the active
        // Pet's SignatureSkillCardId at battle start and "can never satisfy this
        // section's Basic-Card composition rule". It is therefore never an
        // unlock row (DATABASE.md §2 item 1, *Excluded*).
        var (factory, content) = BuildFactory();

        content.AddCard("card-inferno", "Inferno", CardCategory.PetSkill);
        content.AddCard("card-tidal-barrier", "Tidal Barrier", CardCategory.PetSkill);
        content.AddCard("card-iron-fang", "Iron Fang", CardCategory.PetSkill);

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.DoesNotContain(
            grant.StarterCards,
            card => card.CardDefinitionId is
                "card-inferno" or "card-tidal-barrier" or "card-iron-fang");
    }

    [Fact]
    public async Task CreateAsync_ShouldNotGrantAnyDeferredOrUnselectedContent()
    {
        // DATABASE.md §2 item 1 / TASK-083 §"Testing Requirements" item I: the
        // deferred Pets and the deferred Relic are absent, and so is every
        // provisioned-but-unselected definition. They are added to the store
        // here precisely so their absence is a real assertion rather than an
        // accident of an empty store.
        var (factory, content) = BuildFactory();

        content.AddPet("pet-bach-ho", "Bạch Hổ", Element.Kim, "card-iron-fang");
        content.AddPet("pet-huyen-quy", "Huyền Quy", Element.Thuy, "card-tidal-barrier");
        content.AddPet("pet-thanh-xa", "Thanh Xà", Element.Moc, "card-tbd");
        content.AddPet("pet-son-hung", "Sơn Hùng", Element.Tho, "card-tbd");
        content.AddRelic("relic-emergency-core", "Emergency Core");
        content.AddRelic("relic-burning-curse", "Burning Curse");

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.DoesNotContain(
            grant.StarterPet.PetDefinitionId,
            new[] { "pet-bach-ho", "pet-huyen-quy", "pet-thanh-xa", "pet-son-hung" });

        Assert.DoesNotContain(
            grant.StarterRelics,
            relic => relic.RelicDefinitionId is
                "relic-emergency-core" or "relic-burning-curse");
    }

    // -----------------------------------------------------------------------
    // C. The Pet's documented creation values
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldGiveTheStarterPetItsDocumentedCreationValues()
    {
        // DATABASE.md §2 item 1 / §3; PET_RULES.md §3, §4, §5.2: Tier Common
        // (the MVP data default), Star = Pet.MinStar (the documented 1–5 floor),
        // XP = Pet.InitialXp (0), Level = Pet.InitialLevel (1).
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);
        var pet = grant.StarterPet;

        Assert.Equal(PetTier.Common, pet.Tier);
        Assert.Equal(Pet.MinStar, pet.Star);
        Assert.Equal(1, pet.Star);
        Assert.Equal(Pet.InitialXp, pet.XP);
        Assert.Equal(0, pet.XP);
        Assert.Equal(Pet.InitialLevel, pet.Level);
        Assert.Equal(1, pet.Level);
    }

    [Fact]
    public async Task CreateAsync_ShouldStampTheServerSuppliedAcquiredAt()
    {
        // DATABASE.md §1: AcquiredAt is a creation timestamp set once, and
        // TASK-083 §14 makes it server-assigned. The factory stamps the reading
        // it is given rather than reading a clock of its own, so the value is
        // observable end to end.
        var (factory, _) = BuildFactory();
        var acquiredAt = new DateTimeOffset(2026, 9, 29, 12, 34, 56, TimeSpan.Zero);

        var grant = await factory.CreateAsync(acquiredAt);

        Assert.Equal(acquiredAt, grant.StarterPet.AcquiredAt);
        Assert.All(grant.StarterRelics, relic => Assert.Equal(acquiredAt, relic.AcquiredAt));
    }

    [Fact]
    public async Task CreateAsync_ShouldNotStampAnAcquisitionValueOnTheCardUnlockRows()
    {
        // DATABASE.md §1: PlayerUnlockedCard has exactly two columns and no
        // third may be added — card ownership is an unlock flag, not an
        // inventory row (ADR-012 item 9). This asserts the row carries an owner
        // slot and a definition, and nothing else exists to carry a timestamp.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.All(grant.StarterCards, card =>
        {
            Assert.False(string.IsNullOrWhiteSpace(card.CardDefinitionId));
            Assert.Equal(typeof(PlayerUnlockedCard), card.GetType());
        });
    }

    // -----------------------------------------------------------------------
    // D. Relic instance identity
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldMintDistinctRelicInstanceIds()
    {
        // RELIC_RULES.md §2.2: the owned instance's identity is distinct from
        // its definition's and the two are never collapsed. TASK-083 §15
        // requires all three instance ids to differ from one another.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        var instanceIds = grant.StarterRelics
            .Select(relic => relic.RelicInstanceId)
            .ToArray();

        Assert.Equal(3, instanceIds.Distinct(StringComparer.Ordinal).Count());

        // A == B, B == C, A == C must all be false.
        Assert.NotEqual(instanceIds[0], instanceIds[1]);
        Assert.NotEqual(instanceIds[1], instanceIds[2]);
        Assert.NotEqual(instanceIds[0], instanceIds[2]);
    }

    [Fact]
    public async Task CreateAsync_ShouldNeverUseARelicDefinitionIdAsAnInstanceId()
    {
        // RELIC_RULES.md §2.2 item 1: the two identities are distinct. A
        // deterministic, definition-derived instance id would collapse them and
        // make two owned copies indistinguishable.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.All(grant.StarterRelics, relic =>
            Assert.NotEqual(relic.RelicDefinitionId, relic.RelicInstanceId));
    }

    [Fact]
    public async Task CreateAsync_ShouldMintDistinctPetInstanceIdsAcrossPlayers()
    {
        // DATABASE.md §1: PetInstanceId is a PK. Two newly created Players must
        // not collide on a Pet instance identity, which is why it is minted
        // rather than derived from the definition.
        var (factory, _) = BuildFactory();

        var first = await factory.CreateAsync(DateTimeOffset.UtcNow);
        var second = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.NotEqual(first.StarterPet.PetInstanceId, second.StarterPet.PetInstanceId);
    }

    // -----------------------------------------------------------------------
    // E. Determinism and ownership
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldComposeTheSameDefinitionReferencesEveryTime()
    {
        // TASK-083 §"Implementation Notes" — Determinism: the starter set is a
        // fixed server-side set with no RNG and no per-Player variation. Only
        // the minted instance identities and the timestamps differ.
        var (factory, _) = BuildFactory();

        var first = await factory.CreateAsync(DateTimeOffset.UtcNow);
        var second = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(first.StarterPet.PetDefinitionId, second.StarterPet.PetDefinitionId);

        Assert.Equal(
            first.StarterCards.Select(card => card.CardDefinitionId).OrderBy(id => id, StringComparer.Ordinal),
            second.StarterCards.Select(card => card.CardDefinitionId).OrderBy(id => id, StringComparer.Ordinal));

        Assert.Equal(
            first.StarterRelics.Select(relic => relic.RelicDefinitionId).OrderBy(id => id, StringComparer.Ordinal),
            second.StarterRelics.Select(relic => relic.RelicDefinitionId).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task CreateAsync_ShouldAddressNoPlayer()
    {
        // The composition deliberately takes no owner: the creation boundary
        // mints the PlayerId and binds it when staging, so there is no overload
        // of this operation that could grant a set to an existing Player.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Empty(grant.StarterPet.PlayerId);
        Assert.All(grant.StarterCards, card => Assert.Empty(card.PlayerId));
        Assert.All(grant.StarterRelics, relic => Assert.Empty(relic.PlayerId));
    }

    // -----------------------------------------------------------------------
    // F. A missing definition aborts the whole grant
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_WithoutTheStarterPetDefinition_ShouldRefuseTheWholeGrant()
    {
        // TASK-083 §"Key Edge Cases" / §"Stop Conditions": a required
        // provisioned definition row that is absent must not produce a partial
        // starter set, and no value may be invented to fill the gap
        // (AGENTS.md §7).
        var (factory, content) = BuildFactory();
        content.RemovePet(PetDefinitionId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.CreateAsync(DateTimeOffset.UtcNow));

        Assert.Contains(PetDefinitionId, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("card-heal")]
    [InlineData("card-shield")]
    [InlineData("card-power-charge")]
    public async Task CreateAsync_WithoutAStarterCardDefinition_ShouldRefuseTheWholeGrant(
        string missingCardDefinitionId)
    {
        var (factory, content) = BuildFactory();
        content.RemoveCard(missingCardDefinitionId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.CreateAsync(DateTimeOffset.UtcNow));

        Assert.Contains(missingCardDefinitionId, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("relic-berserker-core")]
    [InlineData("relic-mana-crystal")]
    [InlineData("relic-assassin-eye")]
    public async Task CreateAsync_WithoutAStarterRelicDefinition_ShouldRefuseTheWholeGrant(
        string missingRelicDefinitionId)
    {
        var (factory, content) = BuildFactory();
        content.RemoveRelic(missingRelicDefinitionId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.CreateAsync(DateTimeOffset.UtcNow));

        Assert.Contains(missingRelicDefinitionId, error.Message, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // Content store and repository doubles
    // -----------------------------------------------------------------------

    /// <summary>
    /// The provisioned content this composition reads. It holds only
    /// definition rows — no ownership — so a test cannot accidentally rely on
    /// pre-existing ownership when asserting what the grant creates.
    /// </summary>
    private sealed class ContentStore
    {
        public Dictionary<string, PetDefinition> Pets { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, CardDefinition> Cards { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, RelicDefinition> Relics { get; } = new(StringComparer.Ordinal);

        public void AddPet(string id, string identity, Element element, string signatureSkillCardId) =>
            Pets[id] = new PetDefinition
            {
                PetDefinitionId = id,
                Identity = identity,
                Element = element,
                PassiveId = new PassiveId($"passive-{id}"),
                PassiveThreshold = 5,
                SignatureSkillCardId = signatureSkillCardId,
            };

        public void AddCard(string id, string name, CardCategory category) =>
            Cards[id] = new CardDefinition
            {
                CardDefinitionId = id,
                Name = name,
                Category = category,
                PowerCost = 0,
                EffectDefinition = TestCardEffects.FlatPower,
                LoadoutCopyLimit = 1,
            };

        public void AddRelic(string id, string name) =>
            Relics[id] = new RelicDefinition
            {
                RelicDefinitionId = id,
                Name = name,
                Trigger = "OnMatchCount",
                Condition = TestRelicEffects.Condition,
                EffectDefinition = TestRelicEffects.Effect,
            };

        public void RemovePet(string id) => Pets.Remove(id);

        public void RemoveCard(string id) => Cards.Remove(id);

        public void RemoveRelic(string id) => Relics.Remove(id);
    }

    private sealed class StubPetRepository(ContentStore content) : IPetRepository
    {
        public Task AddAsync(Pet pet, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition writes nothing (DATABASE.md §2 item 4).");

        public Task<IReadOnlyList<Pet>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition reads no ownership.");

        public Task<PetDefinition?> GetDefinitionAsync(
            string petDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                content.Pets.TryGetValue(petDefinitionId, out var definition) ? definition : null);

        public Task<IReadOnlyList<PetDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> petDefinitionIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition resolves definitions one at a time.");

        public Task<Pet?> GetByIdAsync(
            string petInstanceId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition reads no ownership.");

        public Task<bool> SaveProgressionAsync(
            Pet pet,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition writes nothing (DATABASE.md §2 item 4).");
    }

    private sealed class StubCardRepository(ContentStore content) : ICardRepository
    {
        public Task AddDefinitionAsync(
            CardDefinition definition,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition provisions no content.");

        public Task AddUnlockAsync(
            PlayerUnlockedCard unlockedCard,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition writes nothing (DATABASE.md §2 item 4).");

        public Task<IReadOnlyList<CardDefinition>> ListUnlockedAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition reads no ownership.");

        public Task<IReadOnlyList<CardDefinition>> ListUnlockedDefinitionsAsync(
            string playerId,
            IReadOnlyCollection<string> cardDefinitionIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition reads no ownership.");

        public Task<CardDefinition?> GetDefinitionAsync(
            string cardDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                content.Cards.TryGetValue(cardDefinitionId, out var definition) ? definition : null);
    }

    private sealed class StubRelicRepository(ContentStore content) : IRelicRepository
    {
        public Task AddAsync(Relic relic, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition writes nothing (DATABASE.md §2 item 4).");

        public Task AddDefinitionAsync(
            RelicDefinition definition,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition provisions no content.");

        public Task<IReadOnlyList<Relic>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition reads no ownership.");

        public Task<IReadOnlyList<Relic>> ListOwnedInstancesAsync(
            string playerId,
            IReadOnlyCollection<string> relicInstanceIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition reads no ownership.");

        public Task<RelicDefinition?> GetDefinitionAsync(
            string relicDefinitionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                content.Relics.TryGetValue(relicDefinitionId, out var definition) ? definition : null);

        public Task<IReadOnlyList<RelicDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> relicDefinitionIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The composition resolves definitions one at a time.");
    }
}
