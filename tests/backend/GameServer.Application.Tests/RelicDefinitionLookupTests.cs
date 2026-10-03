using GameServer.Application;
using GameServer.Application.Relics;
using GameServer.Domain.Relics;
using Microsoft.Extensions.DependencyInjection;

namespace GameServer.Application.Tests;

/// <summary>
/// The Application-level Relic definition lookup boundary —
/// <c>IRelicDefinitionLookup</c> / <c>ScopedRelicDefinitionLookup</c>
/// (<c>DATABASE.md</c> §1's <c>RelicDefinition</c> block and §2's
/// definition-versus-instance split; <c>RELIC_RULES.md</c> §2.2, §8.1–§8.2).
///
/// What is verified is the boundary contract only: a definition identity in,
/// the authoritative <b>Domain</b> <c>RelicDefinition</c> out; an unknown
/// identity yielding <c>null</c> rather than a fabricated definition; the
/// structured <c>Condition</c> and the complete ordered
/// <c>EffectDefinition[]</c> arriving intact; one scope per lookup so the
/// singleton registration never captures the scoped repository; and a blank
/// identity rejected before any query.
///
/// <b>No ownership context is involved.</b> The definition read is shared static
/// content, not Player-owned data (<c>DATABASE.md</c> §2), so the test double
/// throws on every ownership member: a lookup that consulted ownership at all
/// would fail here rather than pass silently.
///
/// <b>No test here resolves a Relic.</b> No trigger is evaluated, no condition is
/// compared, no effect is applied, and no <c>RelicTriggered</c> event is
/// emitted — <c>RELIC_RULES.md</c> §8.7 records every one of those as NOT
/// IMPLEMENTED.
/// </summary>
public class RelicDefinitionLookupTests
{
    private const string DefinitionId = "relic-definition-fixture";

    private static RelicDefinition NewDefinition(
        string id,
        RelicCondition? condition,
        RelicEffectDefinitions effects) => new()
    {
        RelicDefinitionId = id,
        Name = "Fixture Relic",
        Trigger = "OnMatchCount",
        Condition = condition,
        EffectDefinition = effects,
    };

    /// <summary>
    /// The definition read the adapter depends on, backed by one fixture
    /// definition. Every write and every ownership read throws — this boundary
    /// performs none of them (<c>DATABASE.md</c> §2; <c>RELIC_RULES.md</c> §2.1
    /// item 2).
    /// </summary>
    private sealed class FakeRelicRepository : IRelicRepository
    {
        private readonly RelicDefinition? _definition;

        public FakeRelicRepository(RelicDefinition? definition)
        {
            _definition = definition;
        }

        public int DefinitionReads { get; private set; }

        public string? LastRequestedId { get; private set; }

        public Task<RelicDefinition?> GetDefinitionAsync(
            string relicDefinitionId,
            CancellationToken cancellationToken = default)
        {
            DefinitionReads++;
            LastRequestedId = relicDefinitionId;

            return Task.FromResult(
                _definition is not null &&
                string.Equals(_definition.RelicDefinitionId, relicDefinitionId, StringComparison.Ordinal)
                    ? _definition
                    : null);
        }

        public Task AddAsync(Relic relic, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The definition lookup writes no ownership row.");

        public Task AddDefinitionAsync(
            RelicDefinition definition,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The definition lookup writes no definition row.");

        public Task<IReadOnlyList<Relic>> ListByPlayerIdAsync(
            string playerId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "A shared definition read requires no Player context (DATABASE.md §2).");

        public Task<IReadOnlyList<Relic>> ListOwnedInstancesAsync(
            string playerId,
            IReadOnlyCollection<string> relicInstanceIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(
                "A shared definition read performs no ownership check (RELIC_RULES.md §2.1 item 2).");

        public Task<IReadOnlyList<RelicDefinition>> ListDefinitionsAsync(
            IReadOnlyCollection<string> relicDefinitionIds,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The single-definition lookup reads no bulk set.");
    }

    /// <summary>
    /// Counts the scopes the adapter creates, delegating to the container's own
    /// factory. It is how "one scope per lookup" is observed without asserting on
    /// implementation detail: the count is the adapter's own documented behavior.
    /// </summary>
    private sealed class CountingScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;

        public CountingScopeFactory(IServiceScopeFactory inner)
        {
            _inner = inner;
        }

        public int Scopes { get; private set; }

        public IServiceScope CreateScope()
        {
            Scopes++;
            return _inner.CreateScope();
        }
    }

    private static (ScopedRelicDefinitionLookup Lookup, CountingScopeFactory Scopes, FakeRelicRepository Repository)
        CreateLookup(RelicDefinition? definition)
    {
        var repository = new FakeRelicRepository(definition);

        var services = new ServiceCollection();
        services.AddScoped<IRelicRepository>(_ => repository);

        var provider = services.BuildServiceProvider();
        var scopes = new CountingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());

        return (new ScopedRelicDefinitionLookup(scopes), scopes, repository);
    }

    // -----------------------------------------------------------------------
    // The contract shape — DATABASE.md §1, RELIC_RULES.md §2.2
    // -----------------------------------------------------------------------

    [Fact]
    public void Contract_ShouldResolveOneDefinitionByIdentityAndRequireNoPlayerContext()
    {
        // The lookup takes the canonical definition identity (DATABASE.md §1's
        // RelicDefinitionId) and a cancellation token, and nothing else — no
        // PlayerId, no Discord identity, no equipped or inventory state: a
        // definition is shared content (DATABASE.md §2, RELIC_RULES.md §2.2
        // item 4). The returned value is the Domain RelicDefinition, so no EF
        // entity, DbContext, or JSON payload crosses the boundary
        // (ARCHITECTURE.md §2 item 3).
        var method = Assert.Single(typeof(IRelicDefinitionLookup).GetMethods());
        var parameters = method.GetParameters();

        Assert.Equal(nameof(IRelicDefinitionLookup.GetDefinitionAsync), method.Name);
        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
        Assert.Equal("relicDefinitionId", parameters[0].Name);
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
        Assert.Equal(typeof(Task<RelicDefinition>), method.ReturnType);
        Assert.Equal(typeof(RelicDefinition), method.ReturnType.GetGenericArguments()[0]);
        Assert.Equal("GameServer.Domain", typeof(RelicDefinition).Assembly.GetName().Name);
    }

    [Fact]
    public void Adapter_ShouldRequireOnlyTheScopeFactory()
    {
        // The singleton adapter must hold no scoped dependency: its only
        // constructor parameter is the scope factory, which is what makes a
        // captured scoped GameDbContext impossible rather than merely unlikely.
        var constructor = Assert.Single(typeof(ScopedRelicDefinitionLookup).GetConstructors());

        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(typeof(IServiceScopeFactory), parameter.ParameterType);
    }

    // -----------------------------------------------------------------------
    // Existing definition — DATABASE.md §1
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetDefinitionAsync_ShouldReturnTheDomainDefinitionForAKnownIdentity()
    {
        var definition = NewDefinition(
            DefinitionId, TestRelicEffects.Condition, TestRelicEffects.Effect);
        var (lookup, _, repository) = CreateLookup(definition);

        var found = await lookup.GetDefinitionAsync(DefinitionId);

        // The adapter shapes the call and returns the repository's Domain value
        // unchanged — it wraps nothing, copies nothing, and caches nothing.
        Assert.NotNull(found);
        Assert.Same(definition, found);
        Assert.Equal(DefinitionId, found!.RelicDefinitionId);
        Assert.Equal("GameServer.Domain.Relics.RelicDefinition", found.GetType().FullName);
        Assert.Equal(DefinitionId, repository.LastRequestedId);
        Assert.Equal(1, repository.DefinitionReads);
    }

    [Fact]
    public async Task GetDefinitionAsync_ShouldPreserveTheStructuredCondition()
    {
        // RELIC_RULES.md §8.1: the condition is a structured form plus its
        // threshold, and DATABASE.md §1 item 7 stores it as an object. It crosses
        // the boundary as the Domain RelicCondition — never as prose, never as a
        // parsed string, and never defaulted.
        var condition = RelicCondition.Create(RelicConditionType.HpPercentageBelow, 30);
        var (lookup, _, _) = CreateLookup(
            NewDefinition(DefinitionId, condition, TestRelicEffects.Effect));

        var found = await lookup.GetDefinitionAsync(DefinitionId);

        Assert.NotNull(found);
        Assert.Equal(condition, found!.Condition);
        Assert.Equal(RelicConditionType.HpPercentageBelow, found.Condition!.Value.ConditionType);
        Assert.Equal(30, found.Condition!.Value.Threshold);
    }

    [Fact]
    public async Task GetDefinitionAsync_ShouldPreserveTheCompleteOrderedEffectDefinitionArray()
    {
        // RELIC_RULES.md §8.2: EffectDefinition is a structured ARRAY, and §8.2
        // item 4 fixes no canonical order — so the stored order is preserved
        // exactly, with every element and every one of its five members intact
        // (§8.3's allowed combinations). Element positions are read here only to
        // observe the stored sequence; no position carries gameplay meaning.
        var atk = RelicEffectDefinition.Create(
            RelicEffectType.ATK,
            RelicEffectValueType.Percentage,
            5,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Battle);

        var power = RelicEffectDefinition.Create(
            RelicEffectType.Power,
            RelicEffectValueType.Flat,
            10,
            RelicEffectTarget.Pet,
            RelicEffectLifetime.Immediate);

        var effects = RelicEffectDefinitions.Create(atk, power);
        var (lookup, _, _) = CreateLookup(
            NewDefinition(DefinitionId, TestRelicEffects.Condition, effects));

        var found = await lookup.GetDefinitionAsync(DefinitionId);

        Assert.NotNull(found);
        Assert.Equal(effects, found!.EffectDefinition);
        Assert.Equal(2, found.EffectDefinition.Count);

        Assert.Equal(RelicEffectType.ATK, found.EffectDefinition[0].EffectType);
        Assert.Equal(RelicEffectValueType.Percentage, found.EffectDefinition[0].ValueType);
        Assert.Equal(5, found.EffectDefinition[0].Value);
        Assert.Equal(RelicEffectTarget.Pet, found.EffectDefinition[0].Target);
        Assert.Equal(RelicEffectLifetime.Battle, found.EffectDefinition[0].Lifetime);

        Assert.Equal(RelicEffectType.Power, found.EffectDefinition[1].EffectType);
        Assert.Equal(RelicEffectValueType.Flat, found.EffectDefinition[1].ValueType);
        Assert.Equal(10, found.EffectDefinition[1].Value);
        Assert.Equal(RelicEffectTarget.Pet, found.EffectDefinition[1].Target);
        Assert.Equal(RelicEffectLifetime.Immediate, found.EffectDefinition[1].Lifetime);
    }

    [Fact]
    public async Task GetDefinitionAsync_ShouldReturnANullConditionWhenTheDefinitionDeclaresNone()
    {
        // RELIC_RULES.md §8.1 item 4: Condition stays optional, so "no extra
        // condition" crosses the boundary as null rather than as a sentinel
        // condition that would read as a real one.
        var (lookup, _, _) = CreateLookup(
            NewDefinition(DefinitionId, condition: null, TestRelicEffects.Effect));

        var found = await lookup.GetDefinitionAsync(DefinitionId);

        Assert.NotNull(found);
        Assert.Null(found!.Condition);

        // An absent condition is not an absent effect: EffectDefinition is a
        // required member (DATABASE.md §1: the column is NOT NULL), and it
        // crosses the boundary as at least one element (§8.2).
        Assert.True(found.EffectDefinition.Count >= 1);
    }

    // -----------------------------------------------------------------------
    // Missing definition — the established not-found convention
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetDefinitionAsync_ShouldReturnNullForAnUnknownIdentity()
    {
        // RELIC_RULES.md §8 / DATABASE.md §1: an identity with no row yields
        // null — the convention ICardDefinitionLookup, IRelicRepository, and
        // IBossDefinitionLookup already fix. No empty, default, or synthetic
        // definition is produced (AGENTS.md §7).
        var (lookup, _, repository) = CreateLookup(
            NewDefinition(DefinitionId, TestRelicEffects.Condition, TestRelicEffects.Effect));

        var found = await lookup.GetDefinitionAsync("relic-non-existent");

        Assert.Null(found);
        Assert.Equal("relic-non-existent", repository.LastRequestedId);
        Assert.Equal(1, repository.DefinitionReads);
    }

    [Fact]
    public async Task GetDefinitionAsync_ShouldReturnNullWhenTheStoreHoldsNothing()
    {
        var (lookup, _, _) = CreateLookup(definition: null);

        var found = await lookup.GetDefinitionAsync(DefinitionId);

        Assert.Null(found);
    }

    // -----------------------------------------------------------------------
    // Scope handling and argument validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetDefinitionAsync_ShouldCreateOneScopePerLookup()
    {
        // The singleton adapter must never hold the scoped repository or its
        // GameDbContext (ARCHITECTURE.md §2.1 / §3): it creates a scope per call
        // and disposes it with the call, so two lookups are two independent
        // scopes and no definition is retained between them.
        var definition = NewDefinition(
            DefinitionId, TestRelicEffects.Condition, TestRelicEffects.Effect);
        var (lookup, scopes, _) = CreateLookup(definition);

        Assert.Equal(0, scopes.Scopes);

        await lookup.GetDefinitionAsync(DefinitionId);
        Assert.Equal(1, scopes.Scopes);

        await lookup.GetDefinitionAsync("relic-non-existent");
        Assert.Equal(2, scopes.Scopes);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task GetDefinitionAsync_ShouldRejectABlankIdentity(string relicDefinitionId)
    {
        // An absent identity names no row (DATABASE.md §1: every definition
        // identity is a real value form), so it is rejected rather than resolving
        // to the same null an unknown — but well-formed — identity produces.
        var (lookup, scopes, repository) = CreateLookup(
            NewDefinition(DefinitionId, TestRelicEffects.Condition, TestRelicEffects.Effect));

        await Assert.ThrowsAsync<ArgumentException>(
            () => lookup.GetDefinitionAsync(relicDefinitionId));

        // Rejected before any scope or query is created.
        Assert.Equal(0, scopes.Scopes);
        Assert.Equal(0, repository.DefinitionReads);
    }

    [Fact]
    public async Task GetDefinitionAsync_ShouldRejectANullIdentity()
    {
        var (lookup, scopes, _) = CreateLookup(
            NewDefinition(DefinitionId, TestRelicEffects.Condition, TestRelicEffects.Effect));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => lookup.GetDefinitionAsync(null!));

        Assert.Equal(0, scopes.Scopes);
    }

    [Fact]
    public void Constructor_ShouldRejectANullScopeFactory()
    {
        Assert.Throws<ArgumentNullException>(() => new ScopedRelicDefinitionLookup(null!));
    }

    // -----------------------------------------------------------------------
    // Composition — the singleton/scoped adaptation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task RegisteredLookup_ShouldBeASingletonUsableFromTheRootScope()
    {
        // The registration the composition root lands: a singleton whose scoped
        // work happens in a scope it creates itself. ValidateScopes makes a
        // captured scoped dependency fail rather than pass, so this proves the
        // lookup is safely consumable by the existing singleton consumer
        // (BattleStateService) without changing any lifetime.
        var services = new ServiceCollection();
        services.AddApplicationServices();

        var definition = NewDefinition(
            DefinitionId, TestRelicEffects.Condition, TestRelicEffects.Effect);
        var repository = new FakeRelicRepository(definition);
        services.AddScoped<IRelicRepository>(_ => repository);

        await using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        var lookup = provider.GetRequiredService<IRelicDefinitionLookup>();

        Assert.IsType<ScopedRelicDefinitionLookup>(lookup);
        Assert.Same(lookup, provider.GetRequiredService<IRelicDefinitionLookup>());

        var found = await lookup.GetDefinitionAsync(DefinitionId);

        Assert.NotNull(found);
        Assert.Equal(DefinitionId, found!.RelicDefinitionId);
    }
}
