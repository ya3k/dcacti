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
/// The MVP content ownership composition — <c>DATABASE.md</c> §2 item 1,
/// <c>MVP_SCOPE.md</c> §1 "Content ownership &amp; reachability" (TASK-213
/// decision; TASK-221 implementation).
///
/// <code>
/// provisioned definitions
///         ↓  PlayerStarterGrantFactory
/// 5 Pets   — one owned instance per MVP Pet definition
/// 3 Cards  — card-heal, card-shield, card-power-charge
/// 10 Relics — one owned instance per MVP Relic definition
/// </code>
///
/// The composition is a fixed server-side set drawn only from provisioned
/// content, so these tests assert three things about it: <b>what</b> it contains,
/// <b>which creation values</b> each Pet instance carries, and <b>what it
/// refuses</b> — a missing definition row aborts the whole grant rather than
/// producing a partial or substituted set.
///
/// The composed rows are deliberately owner-less: the owning <c>PlayerId</c> is
/// minted by the creation boundary, which is the only place the creation branch
/// is observable. That is what makes "granted on creation only" structural, and
/// it is asserted here rather than in a comment.
/// </summary>
public class PlayerStarterGrantFactoryTests
{
    /// <summary>The five MVP Pet definitions (<c>PET_RULES.md</c> §8).</summary>
    private static readonly string[] MvpPetDefinitionIds =
    [
        "pet-xich-lang",
        "pet-bach-ho",
        "pet-huyen-quy",
        "pet-thanh-xa",
        "pet-son-hung",
    ];

    /// <summary>The three Basic Cards (<c>CARD_RULES.md</c> §2).</summary>
    private static readonly string[] MvpBasicCardDefinitionIds =
    [
        "card-heal",
        "card-shield",
        "card-power-charge",
    ];

    /// <summary>The ten MVP Relic definitions (<c>RELIC_RULES.md</c> §6).</summary>
    private static readonly string[] MvpRelicDefinitionIds =
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

    /// <summary>
    /// A content store holding exactly the provisioned definitions the grant
    /// references — the TASK-085 / TASK-172 / TASK-173 rows
    /// (<c>DATABASE.md</c> §5 item 4).
    /// </summary>
    private static (PlayerStarterGrantFactory Factory, ContentStore Content) BuildFactory()
    {
        var content = new ContentStore();

        var signatureSkills = new[]
        {
            ("pet-xich-lang", "Xích Lang", Element.Hoa, "card-inferno"),
            ("pet-bach-ho", "Bạch Hổ", Element.Kim, "card-iron-fang"),
            ("pet-huyen-quy", "Huyền Quy", Element.Thuy, "card-tidal-barrier"),
            ("pet-thanh-xa", "Thanh Xà", Element.Moc, "card-venomous-bloom"),
            ("pet-son-hung", "Sơn Hùng", Element.Tho, "card-earthshaker"),
        };

        foreach (var (id, identity, element, signatureSkill) in signatureSkills)
        {
            content.AddPet(id, identity, element, signatureSkill);
        }

        content.AddCard("card-heal", "Heal", CardCategory.Basic);
        content.AddCard("card-shield", "Shield", CardCategory.Basic);
        content.AddCard("card-power-charge", "Power Charge", CardCategory.Basic);

        foreach (var (id, name) in new[]
        {
            ("card-inferno", "Inferno"),
            ("card-tidal-barrier", "Tidal Barrier"),
            ("card-iron-fang", "Iron Fang"),
            ("card-venomous-bloom", "Venomous Bloom"),
            ("card-earthshaker", "Earthshaker"),
        })
        {
            content.AddCard(id, name, CardCategory.PetSkill);
        }

        foreach (var relicDefinitionId in MvpRelicDefinitionIds)
        {
            content.AddRelic(relicDefinitionId, relicDefinitionId);
        }

        return (
            new PlayerStarterGrantFactory(
                new StubPetRepository(content),
                new StubCardRepository(content),
                new StubRelicRepository(content)),
            content);
    }

    // -----------------------------------------------------------------------
    // A. The composition: 5 Pets / 3 Basic Cards / 10 Relics
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldComposeTheWholeMvpContentSet()
    {
        // MVP_SCOPE.md §1 / DATABASE.md §2 item 1: the grant IS the MVP content
        // set — 5 Pets, 3 Basic Cards, and 10 Relics — because MVP has no
        // post-creation acquisition. The counts bind exactly, not as ranges.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(5, grant.StarterPets.Count);
        Assert.Equal(3, grant.StarterCards.Count);
        Assert.Equal(10, grant.StarterRelics.Count);

        // 18 ownership rows: the whole grant, or none (DATABASE.md §2 item 4).
        Assert.Equal(18, grant.StarterPets.Count + grant.StarterCards.Count + grant.StarterRelics.Count);
    }

    // -----------------------------------------------------------------------
    // B. The exact granted contents
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldOwnEveryProvisionedMvpPet()
    {
        // MVP_SCOPE.md §1: 5 Pets, one owned instance per MVP Pet definition.
        // The set — not a subset — is what makes the active-Pet choice a real
        // loadout decision (GDD.md §2, PET_RULES.md §2.1 item 1).
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(
            MvpPetDefinitionIds.OrderBy(id => id, StringComparer.Ordinal),
            grant.StarterPets
                .Select(pet => pet.PetDefinitionId)
                .OrderBy(id => id, StringComparer.Ordinal));
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
    public async Task CreateAsync_ShouldOwnEveryProvisionedMvpRelic()
    {
        // MVP_SCOPE.md §1 / RELIC_RULES.md §6: all ten Relics. Ownership is what
        // makes the 3–5 Relic loadout a real build decision (§2.1 item 1): with
        // ten owned instances every legal combination is reachable and no
        // acquisition, currency, or RNG is involved.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(
            MvpRelicDefinitionIds.OrderBy(id => id, StringComparer.Ordinal),
            grant.StarterRelics
                .Select(relic => relic.RelicDefinitionId)
                .OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task CreateAsync_ShouldGrantExactlyOneOwnedInstancePerDefinition()
    {
        // RELIC_RULES.md §2.4: the model permits two instances of one definition,
        // but MVP grants one per definition, so the grant opens no
        // duplicate-selection question and the owned set is exactly the content
        // set. PET_RULES.md §2.1 item 1 gives the same shape for Pets.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(
            grant.StarterPets.Count,
            grant.StarterPets.Select(pet => pet.PetDefinitionId).Distinct(StringComparer.Ordinal).Count());

        Assert.Equal(
            grant.StarterRelics.Count,
            grant.StarterRelics.Select(relic => relic.RelicDefinitionId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task CreateAsync_ShouldNotGrantAnyPetSkillCard()
    {
        // CARD_RULES.md §1 item 4: a PetSkill Card is derived from the active
        // Pet's SignatureSkillCardId at battle start and "can never satisfy this
        // section's Basic-Card composition rule". It is therefore never an
        // unlock row (DATABASE.md §2 item 1, *Excluded*) — including now that all
        // five Signature Skills are provisioned.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.DoesNotContain(
            grant.StarterCards,
            card => card.CardDefinitionId is
                "card-inferno"
                or "card-tidal-barrier"
                or "card-iron-fang"
                or "card-venomous-bloom"
                or "card-earthshaker");
    }

    [Fact]
    public async Task CreateAsync_ShouldGrantNothingOutsideTheProvisionedContentSet()
    {
        // AGENTS.md §7 / §20: the grant may only reference content that exists.
        // The store deliberately holds more than the grant uses (every PetSkill
        // Card), so this is a real assertion about the grant's membership rather
        // than an accident of an empty store.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.All(
            grant.StarterPets,
            pet => Assert.Contains(pet.PetDefinitionId, MvpPetDefinitionIds));

        Assert.All(
            grant.StarterCards,
            card => Assert.Contains(card.CardDefinitionId, MvpBasicCardDefinitionIds));

        Assert.All(
            grant.StarterRelics,
            relic => Assert.Contains(relic.RelicDefinitionId, MvpRelicDefinitionIds));
    }

    // -----------------------------------------------------------------------
    // C. Each Pet instance's documented creation values
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldGiveEveryStarterPetItsDocumentedCreationValues()
    {
        // DATABASE.md §2 item 1 / §3; PET_RULES.md §3, §4, §5.2: Tier Common
        // (the MVP data default), Star = Pet.MinStar (the documented 1–5 floor),
        // XP = Pet.InitialXp (0), Level = Pet.InitialLevel (1) — on every granted
        // instance, because none of them is privileged as "the" starter Pet.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.All(grant.StarterPets, pet =>
        {
            Assert.Equal(PetTier.Common, pet.Tier);
            Assert.Equal(Pet.MinStar, pet.Star);
            Assert.Equal(1, pet.Star);
            Assert.Equal(Pet.InitialXp, pet.XP);
            Assert.Equal(0, pet.XP);
            Assert.Equal(Pet.InitialLevel, pet.Level);
            Assert.Equal(1, pet.Level);
        });
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

        Assert.All(grant.StarterPets, pet => Assert.Equal(acquiredAt, pet.AcquiredAt));
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
    // D. Instance identity
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldMintDistinctRelicInstanceIds()
    {
        // RELIC_RULES.md §2.2: the owned instance's identity is distinct from
        // its definition's and the two are never collapsed. Every one of the ten
        // granted instances must carry its own identity, so no two are
        // indistinguishable.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        var instanceIds = grant.StarterRelics
            .Select(relic => relic.RelicInstanceId)
            .ToArray();

        Assert.Equal(10, instanceIds.Length);
        Assert.Equal(10, instanceIds.Distinct(StringComparer.Ordinal).Count());
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
    public async Task CreateAsync_ShouldMintDistinctPetInstanceIdsWithinOneGrant()
    {
        // DATABASE.md §1: PetInstanceId is a PK, and the grant now creates five
        // Pet rows in one commit — so the identities must be distinct inside a
        // single grant, not only across Players.
        var (factory, _) = BuildFactory();

        var grant = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(
            5,
            grant.StarterPets.Select(pet => pet.PetInstanceId).Distinct(StringComparer.Ordinal).Count());
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

        Assert.Empty(
            first.StarterPets
                .Select(pet => pet.PetInstanceId)
                .Intersect(second.StarterPets.Select(pet => pet.PetInstanceId), StringComparer.Ordinal));
    }

    // -----------------------------------------------------------------------
    // E. Determinism and ownership
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_ShouldComposeTheSameDefinitionReferencesEveryTime()
    {
        // TASK-083 §"Implementation Notes" — Determinism: the grant is a fixed
        // server-side set with no RNG and no per-Player variation. Only the
        // minted instance identities and the timestamps differ.
        var (factory, _) = BuildFactory();

        var first = await factory.CreateAsync(DateTimeOffset.UtcNow);
        var second = await factory.CreateAsync(DateTimeOffset.UtcNow);

        Assert.Equal(
            first.StarterPets.Select(pet => pet.PetDefinitionId).OrderBy(id => id, StringComparer.Ordinal),
            second.StarterPets.Select(pet => pet.PetDefinitionId).OrderBy(id => id, StringComparer.Ordinal));

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

        Assert.All(grant.StarterPets, pet => Assert.Empty(pet.PlayerId));
        Assert.All(grant.StarterCards, card => Assert.Empty(card.PlayerId));
        Assert.All(grant.StarterRelics, relic => Assert.Empty(relic.PlayerId));
    }

    // -----------------------------------------------------------------------
    // F. A missing definition aborts the whole grant
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("pet-xich-lang")]
    [InlineData("pet-bach-ho")]
    [InlineData("pet-huyen-quy")]
    [InlineData("pet-thanh-xa")]
    [InlineData("pet-son-hung")]
    public async Task CreateAsync_WithoutAPetDefinition_ShouldRefuseTheWholeGrant(
        string missingPetDefinitionId)
    {
        // TASK-083 §"Key Edge Cases" / §"Stop Conditions": a required
        // provisioned definition row that is absent must not produce a partial
        // grant, and no value may be invented to fill the gap (AGENTS.md §7).
        var (factory, content) = BuildFactory();
        content.RemovePet(missingPetDefinitionId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.CreateAsync(DateTimeOffset.UtcNow));

        Assert.Contains(missingPetDefinitionId, error.Message, StringComparison.Ordinal);
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
    [InlineData("relic-emergency-core")]
    [InlineData("relic-burning-curse")]
    [InlineData("relic-combo-fang")]
    [InlineData("relic-arcane-battery")]
    [InlineData("relic-execution-mark")]
    [InlineData("relic-cascade-core")]
    [InlineData("relic-battle-instinct")]
    public async Task CreateAsync_WithoutARelicDefinition_ShouldRefuseTheWholeGrant(
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

        public Task<IReadOnlyList<CardDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> cardDefinitionIds,
            CancellationToken cancellationToken = default) =>
            // The bulk content read the collection projection uses for a Pet's
            // derived Signature Skill (API_CONTRACTS.md §5.1). The composition
            // resolves its starter definitions one identity at a time through
            // GetDefinitionAsync, so this path is not exercised here.
            Task.FromResult<IReadOnlyList<CardDefinition>>(
                cardDefinitionIds
                    .Where(content.Cards.ContainsKey)
                    .Select(cardDefinitionId => content.Cards[cardDefinitionId])
                    .ToList());

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
